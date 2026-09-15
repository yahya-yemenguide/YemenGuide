using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Finance;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Controllers
{
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICommissionFinanceService
            _commissionFinanceService;
        private readonly INotificationService
            _notificationService;
        private readonly CurrencyDisplayService
            _currencyDisplayService;
        private readonly IWebHostEnvironment
            _environment;

        public BookingsController(
            AppDbContext context,
            ICommissionFinanceService commissionFinanceService,
            INotificationService notificationService,
            CurrencyDisplayService currencyDisplayService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _commissionFinanceService = commissionFinanceService;
            _notificationService = notificationService;
            _currencyDisplayService = currencyDisplayService;
            _environment = environment;
        }

        private sealed class CustomerCurrencyContext
        {
            public string Currency { get; init; } = CurrencyDisplayService.YemeniRial;
            public string Code { get; init; } = "YER";
            public string Symbol { get; init; } = "ر.ي";
            public decimal SanaaUsdRate { get; init; } = 537m;
            public decimal SanaaSarRate { get; init; } = 140m;
            public decimal AdenUsdRate { get; init; } = 1900m;
            public decimal AdenSarRate { get; init; } = 500m;

            public decimal Convert(decimal amountInYer, string? exchangeRateRegion)
            {
                if (Currency == CurrencyDisplayService.YemeniRial)
                {
                    return Math.Round(amountInYer, 0, MidpointRounding.AwayFromZero);
                }

                bool useAdenMarket = string.Equals(
                    exchangeRateRegion,
                    "South",
                    StringComparison.OrdinalIgnoreCase);

                decimal rate = Currency == CurrencyDisplayService.UsDollar
                    ? useAdenMarket ? AdenUsdRate : SanaaUsdRate
                    : useAdenMarket ? AdenSarRate : SanaaSarRate;

                if (rate <= 0m)
                {
                    rate = Currency == CurrencyDisplayService.UsDollar
                        ? useAdenMarket ? 1900m : 537m
                        : useAdenMarket ? 500m : 140m;
                }

                return Math.Round(
                    amountInYer / rate,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            public string Format(decimal amountInYer, string? exchangeRateRegion)
            {
                decimal converted = Convert(amountInYer, exchangeRateRegion);

                if (Currency == CurrencyDisplayService.UsDollar)
                {
                    return $"${converted:N2}";
                }

                if (Currency == CurrencyDisplayService.SaudiRiyal)
                {
                    return $"{converted:N2} ر.س";
                }

                return $"{converted:N0} ر.ي";
            }
        }

        private async Task<CustomerCurrencyContext> GetCustomerCurrencyContextAsync()
        {
            string currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(HttpContext.RequestAborted);

            var rates = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .Select(setting => new
                {
                    setting.SanaaUsdRate,
                    setting.SanaaSarRate,
                    setting.AdenUsdRate,
                    setting.AdenSarRate
                })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            return new CustomerCurrencyContext
            {
                Currency = currency,
                Code = CurrencyDisplayService.GetCurrencyCode(currency),
                Symbol = CurrencyDisplayService.GetCurrencySymbol(currency),
                SanaaUsdRate = rates?.SanaaUsdRate > 0m ? rates.SanaaUsdRate : 537m,
                SanaaSarRate = rates?.SanaaSarRate > 0m ? rates.SanaaSarRate : 140m,
                AdenUsdRate = rates?.AdenUsdRate > 0m ? rates.AdenUsdRate : 1900m,
                AdenSarRate = rates?.AdenSarRate > 0m ? rates.AdenSarRate : 500m
            };
        }

        /* ================================================================
           الحالات التي يستطيع العميل إلغاءها
           ================================================================ */

        private static readonly HashSet<string>
            CustomerCancellableStates =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "Pending",
                    "AwaitingPayment",
                    "Confirmed"
                };

        private static bool IsState(
            string? value,
            string expected)
        {
            return string.Equals(
                value?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPaymentApproved(Booking booking)
        {
            return booking.PaymentApprovedAt.HasValue ||
                   IsState(booking.PaymentStatus, "PartiallyPaid") ||
                   IsState(booking.PaymentStatus, "Paid") ||
                   IsState(booking.PaymentStatus, "FullyPaid");
        }

        private static void ReleaseRoomAssignments(
            Booking booking,
            DateTime releasedAt)
        {
            foreach (var assignment in booking.BookingRoomUnits.Where(item =>
                         IsState(item.AssignmentStatus, "Held") ||
                         IsState(item.AssignmentStatus, "Confirmed")))
            {
                assignment.AssignmentStatus = "Released";
                assignment.ReleasedAt = releasedAt;
            }
        }

        private static bool CanCustomerCancel(
            Booking booking,
            DateTime now)
        {
            var state =
                booking.BookingState?.Trim() ??
                string.Empty;

            return CustomerCancellableStates
                       .Contains(state) &&
                   booking.StartDate.Date >=
                       now.Date;
        }

        private void AddNotification(
            int? userId,
            string title,
            string text,
            string notificationType,
            string? targetUrl,
            string? referenceType = null,
            int? referenceId = null)
        {
            if (!userId.HasValue ||
                userId.Value <= 0 ||
                string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _notificationService.Queue(
                new CreateNotificationDto
                {
                    UserId = userId.Value,
                    NotificationTitle = title,
                    NotificationText = text.Trim(),
                    NotificationType = notificationType,
                    TargetUrl = targetUrl,
                    ReferenceType = referenceType,
                    ReferenceId = referenceId
                });
        }

        private sealed class
            CustomerCancellationCommissionResult
        {
            public bool Success { get; set; }

            public string Message { get; set; } =
                string.Empty;

            public decimal PlatformCommission { get; set; }

            public decimal CommissionReleased { get; set; }

            public decimal OwnerShare { get; set; }

            public static
                CustomerCancellationCommissionResult Failed(
                    string message)
            {
                return new CustomerCancellationCommissionResult
                {
                    Success = false,
                    Message = message
                };
            }

            public static
                CustomerCancellationCommissionResult Succeeded(
                    decimal platformCommission,
                    decimal commissionReleased,
                    decimal ownerShare)
            {
                return new CustomerCancellationCommissionResult
                {
                    Success = true,
                    PlatformCommission = Math.Round(
                        Math.Max(0m, platformCommission),
                        2),
                    CommissionReleased = Math.Round(
                        Math.Max(0m, commissionReleased),
                        2),
                    OwnerShare = Math.Round(
                        Math.Max(0m, ownerShare),
                        2)
                };
            }
        }

        /// <summary>
        /// يطبّق الأثر المالي لإلغاء العميل داخل نفس معاملة إلغاء الحجز.
        /// قبل التأكيد لا توجد عمولة محجوزة مستحقة. بعد التأكيد تُحرر
        /// العمولة عند الإلغاء المجاني، أو يعاد احتسابها على غرامة
        /// الإلغاء فقط ثم تُخصم نهائيًا.
        /// </summary>
        private async Task<
            CustomerCancellationCommissionResult>
            ApplyCustomerCancellationCommissionAsync(
                Booking booking,
                string bookingStateBeforeCancellation,
                CancellationQuoteDto quote,
                int performedByUserId)
        {
            var financial =
                booking.FinancialRecord;

            if (financial == null)
            {
                return CustomerCancellationCommissionResult
                    .Failed(
                        "السجل المالي المرتبط بالحجز غير موجود.");
            }

            var originalCommission = Math.Round(
                Math.Max(
                    0m,
                    financial.PlatformCommission),
                2);

            var commissionStatus =
                financial.CommissionStatus?.Trim() ??
                string.Empty;

            var wasConfirmed =
                IsState(
                    bookingStateBeforeCancellation,
                    "Confirmed");

            /*
             * Pending / AwaitingPayment / PaymentReview لا يفترض أن تكون
             * عمولتها محجوزة. نغلق العمولة المحسوبة كغير مستحقة دون
             * إنشاء حركة رصيد. وإذا وجد حجز قديم حُجزت عمولته فعلًا
             * نحررها حفاظًا على سلامة رصيد الضمان.
             */
            if (!wasConfirmed)
            {
                if (IsState(
                        commissionStatus,
                        OwnerBookingCommissionStatuses.Held) ||
                    IsState(
                        commissionStatus,
                        OwnerBookingCommissionStatuses.Debt) ||
                    IsState(
                        commissionStatus,
                        OwnerBookingCommissionStatuses.Charged))
                {
                    var releaseResult =
                        await _commissionFinanceService
                            .ReleaseBookingCommissionAsync(
                                booking.BookingId,
                                performedByUserId,
                                "تحرير عمولة حجز ألغاه العميل قبل التأكيد النهائي.",
                                HttpContext.RequestAborted);

                    if (!releaseResult.Success)
                    {
                        return CustomerCancellationCommissionResult
                            .Failed(releaseResult.Message);
                    }

                    return CustomerCancellationCommissionResult
                        .Succeeded(
                            0m,
                            originalCommission,
                            0m);
                }

                if (IsState(
                        commissionStatus,
                        OwnerBookingCommissionStatuses.Calculated))
                {
                    var resetResult =
                        await _commissionFinanceService
                            .RecalculateBookingCommissionAsync(
                                booking.BookingId,
                                0m,
                                performedByUserId,
                                null,
                                HttpContext.RequestAborted);

                    if (!resetResult.Success)
                    {
                        return CustomerCancellationCommissionResult
                            .Failed(resetResult.Message);
                    }
                }

                return CustomerCancellationCommissionResult
                    .Succeeded(
                        0m,
                        0m,
                        0m);
            }

            var returnCommission = financial.ReturnCommissionOnCancellationSnapshot ??
                await _context.SystemSettings.OrderBy(x => x.Id)
                    .Select(x => (bool?)x.ReturnCommissionOnCancellation).FirstOrDefaultAsync() ?? true;
            financial.ReturnCommissionOnCancellationSnapshot = returnCommission;
            if (!returnCommission)
            {
                if (IsState(financial.CommissionStatus, OwnerBookingCommissionStatuses.Calculated))
                {
                    var held = await _commissionFinanceService.HoldBookingCommissionAsync(booking.BookingId, performedByUserId, HttpContext.RequestAborted);
                    if (!held.Success) return CustomerCancellationCommissionResult.Failed(held.Message);
                }
                var charged = await _commissionFinanceService.ChargeBookingCommissionAsync(booking.BookingId, performedByUserId, HttpContext.RequestAborted);
                if (!charged.Success) return CustomerCancellationCommissionResult.Failed(charged.Message);
                return CustomerCancellationCommissionResult.Succeeded(financial.PlatformCommission, 0m,
                    Math.Max(0m, quote.CancellationFee - financial.PlatformCommission));
            }

            /* الإلغاء المجاني أو حجز بلا مبلغ محتفظ به. */
            if (quote.CancellationFee <= 0m)
            {
                var releaseResult =
                    await _commissionFinanceService
                        .ReleaseBookingCommissionAsync(
                            booking.BookingId,
                            performedByUserId,
                            "تحرير عمولة الحجز بسبب الإلغاء المجاني من العميل.",
                            HttpContext.RequestAborted);

                if (!releaseResult.Success)
                {
                    return CustomerCancellationCommissionResult
                        .Failed(releaseResult.Message);
                }

                return CustomerCancellationCommissionResult
                    .Succeeded(
                        0m,
                        originalCommission,
                        0m);
            }

            /*
             * عند وجود غرامة نحرر العمولة المحجوزة على كامل الحجز،
             * ثم نعيد حسابها على الغرامة المحتفظ بها فقط.
             */
            if (IsState(
                    commissionStatus,
                    OwnerBookingCommissionStatuses.Held) ||
                IsState(
                    commissionStatus,
                    OwnerBookingCommissionStatuses.Debt) ||
                IsState(
                    commissionStatus,
                    OwnerBookingCommissionStatuses.Charged))
            {
                var releaseForAdjustmentResult =
                    await _commissionFinanceService
                        .ReleaseBookingCommissionAsync(
                            booking.BookingId,
                            performedByUserId,
                            "تحرير العمولة المحجوزة لإعادة احتسابها على غرامة إلغاء العميل فقط.",
                            HttpContext.RequestAborted);

                if (!releaseForAdjustmentResult.Success)
                {
                    return CustomerCancellationCommissionResult
                        .Failed(
                            releaseForAdjustmentResult.Message);
                }
            }
            var recalculateResult =
                await _commissionFinanceService
                    .RecalculateBookingCommissionAsync(
                        booking.BookingId,
                        quote.CancellationFee,
                        performedByUserId,
                        "إعادة احتساب عمولة المنصة على غرامة إلغاء العميل فقط.",
                        HttpContext.RequestAborted);

            if (!recalculateResult.Success)
            {
                return CustomerCancellationCommissionResult
                    .Failed(recalculateResult.Message);
            }

            var adjustedCommission = Math.Round(
                Math.Max(
                    0m,
                    financial.PlatformCommission),
                2);

            var holdResult =
                await _commissionFinanceService
                    .HoldBookingCommissionAsync(
                        booking.BookingId,
                        performedByUserId,
                        HttpContext.RequestAborted);

            if (!holdResult.Success)
            {
                return CustomerCancellationCommissionResult
                    .Failed(holdResult.Message);
            }

            var chargeResult =
                await _commissionFinanceService
                    .ChargeBookingCommissionAsync(
                        booking.BookingId,
                        performedByUserId,
                        HttpContext.RequestAborted);

            if (!chargeResult.Success)
            {
                return CustomerCancellationCommissionResult
                    .Failed(chargeResult.Message);
            }

            return CustomerCancellationCommissionResult
                .Succeeded(
                    adjustedCommission,
                    Math.Max(
                        0m,
                        originalCommission -
                        adjustedCommission),
                    Math.Max(
                        0m,
                        quote.CancellationFee -
                        adjustedCommission));
        }

        /* ================================================================
           قراءة رقم العميل المسجل
           ================================================================ */

        private bool TryGetCustomerId(
            out int customerId)
        {
            var customerIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                customerIdText,
                out customerId);
        }

        /* ================================================================
           تجهيز اسم الغرفة
           ================================================================ */

        private static string? BuildRoomDisplayName(
            string? roomType,
            string? roomNumber)
        {
            var values =
                new[]
                {
                    roomType?.Trim(),
                    roomNumber?.Trim()
                }
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            return values.Count == 0
                ? null
                : string.Join(
                    " — ",
                    values);
        }

        /* ================================================================
           توحيد مسار الصورة
           ================================================================ */

        private static string NormalizeImagePath(
            string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized =
                path
                    .Trim()
                    .Trim('"', '\'')
                    .Replace("\\", "/");

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (normalized.StartsWith(
                    "http://",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(
                    "https://",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(
                    "data:",
                    StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.StartsWith("~/"))
            {
                normalized =
                    "/" + normalized.Substring(2);
            }
            else if (!normalized.StartsWith("/"))
            {
                normalized =
                    "/" + normalized;
            }

            return normalized;
        }

        /* ================================================================
           قراءة الصور سواء كانت مسارًا واحدًا أو JSON أو قائمة نصية
           ================================================================ */

        private static List<string> ReadImagePaths(
            string? value)
        {
            var images =
                new List<string>();

            if (string.IsNullOrWhiteSpace(value))
            {
                return images;
            }

            var trimmedValue =
                value.Trim();

            /*
             * بعض حقول الصور محفوظة بصيغة:
             * ["/images/1.jpg","/images/2.jpg"]
             */
            if (trimmedValue.StartsWith("[") &&
                trimmedValue.EndsWith("]"))
            {
                try
                {
                    using var document =
                        JsonDocument.Parse(
                            trimmedValue);

                    if (document.RootElement.ValueKind ==
                        JsonValueKind.Array)
                    {
                        foreach (
                            var item in
                            document.RootElement
                                .EnumerateArray())
                        {
                            if (item.ValueKind !=
                                JsonValueKind.String)
                            {
                                continue;
                            }

                            var imagePath =
                                NormalizeImagePath(
                                    item.GetString());

                            if (!string.IsNullOrWhiteSpace(
                                    imagePath))
                            {
                                images.Add(
                                    imagePath);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    /*
                     * عند وجود قيمة JSON غير صحيحة،
                     * ننتقل للتقسيم النصي في الأسفل.
                     */
                }
            }

            /*
             * يدعم مسارًا واحدًا أو مسارات مفصولة
             * بفاصلة أو فاصلة منقوطة أو سطر جديد.
             */
            if (images.Count == 0)
            {
                var parts =
                    trimmedValue.Split(
                        new[]
                        {
                            ',',
                            ';',
                            '|',
                            '\r',
                            '\n'
                        },
                        StringSplitOptions
                            .RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    var imagePath =
                        NormalizeImagePath(
                            part);

                    if (!string.IsNullOrWhiteSpace(
                            imagePath))
                    {
                        images.Add(
                            imagePath);
                    }
                }
            }

            return images
                .Where(image =>
                    !string.IsNullOrWhiteSpace(image))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /* ================================================================
           تحديد صورة المنشأة التي ستظهر في حجوزاتي
           ================================================================ */

        private static string GetPlaceImageUrl(
            string? coverImage,
            string? logoImage,
            string? galleryImage,
            string? categoryImage)
        {
            var imageCandidates =
                new[]
                {
                    ReadImagePaths(
                        coverImage)
                        .FirstOrDefault(),

                    ReadImagePaths(
                        logoImage)
                        .FirstOrDefault(),

                    NormalizeImagePath(
                        galleryImage),

                    ReadImagePaths(
                        categoryImage)
                        .FirstOrDefault()
                };

            return imageCandidates
                .FirstOrDefault(image =>
                    !string.IsNullOrWhiteSpace(image))
                ?? "/images/bg1.jpg";
        }

        /* ================================================================
           إنهاء مهلة دفع العربون
           ================================================================ */

        private async Task ExpireOverduePaymentsAsync(
            int customerId)
        {
            var now =
                DateTime.Now;

            var expiredBookings =
                await _context.Bookings
                    .Include(booking =>
                        booking.BookingRoomUnits)
                    .Where(booking =>
                        booking.CustomerId ==
                            customerId &&

                        booking.BookingState ==
                            "AwaitingPayment" &&

                        booking.TimerExpiredAt
                            .HasValue &&

                        booking.TimerExpiredAt
                            .Value <= now)
                    .ToListAsync();

            if (expiredBookings.Count == 0)
            {
                return;
            }

            foreach (var booking in expiredBookings)
            {
                booking.BookingState =
                    "Expired";

                booking.PaymentStatus =
                    "Expired";

                booking.RefundStatus =
                    "NotApplicable";

                booking.TimerExpiredAt =
                    null;

                booking.ClosedByUserId =
                    null;

                booking.ClosedByType =
                    "System";

                booking.ClosedAt =
                    now;

                booking.LastStatusChangedAt =
                    now;

                ReleaseRoomAssignments(
                    booking,
                    now);
            }

            await _context
                .SaveChangesAsync();
        }

        /* ================================================================
           جلب ملخص الإلغاء قبل التنفيذ
           ================================================================ */

        [HttpGet("/Bookings/CancellationQuote")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult>
            GetCancellationQuote(
                int id)
        {
            if (!TryGetCustomerId(
                    out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message =
                        "تعذر التعرف على حساب العميل. أعد تسجيل الدخول."
                });
            }

            if (id <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "رقم الحجز غير صحيح."
                });
            }

            await ExpireOverduePaymentsAsync(
                customerId);

            var booking =
                await BuildCancellationQuery()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.BookingId == id &&
                        item.CustomerId == customerId);

            if (booking == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "الحجز غير موجود أو لا يتبع حسابك."
                });
            }

            var quote =
                BuildCancellationQuote(
                    booking,
                    DateTime.Now);

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            ApplyCurrencyToCancellationQuote(
                quote,
                booking,
                currencyContext);

            return Json(new
            {
                success = quote.CanCancel,
                message = quote.Message,
                data = quote
            });
        }

        /* ================================================================
           تنفيذ إلغاء العميل للحجز
           ================================================================ */

        [HttpPost("/Bookings/Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            CancelBooking(
                CancelBookingRequestDto request)
        {
            if (!TryGetCustomerId(
                    out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message =
                        "تعذر التعرف على حساب العميل. أعد تسجيل الدخول."
                });
            }

            if (request == null ||
                request.BookingId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "بيانات الإلغاء غير صحيحة."
                });
            }

            var reason =
                request.Reason?.Trim() ??
                string.Empty;

            if (reason.Length < 5)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "اكتب سبب الإلغاء بوضوح، على ألا يقل عن 5 أحرف."
                });
            }

            if (reason.Length > 500)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "سبب الإلغاء طويل جدًا. الحد الأقصى 500 حرف."
                });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            try
            {
                var booking =
                    await BuildCancellationQuery()
                        .FirstOrDefaultAsync(item =>
                            item.BookingId ==
                                request.BookingId &&
                            item.CustomerId ==
                                customerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع حسابك."
                    });
                }

                var now =
                    DateTime.Now;

                var quote =
                    BuildCancellationQuote(
                        booking,
                        now);

                var currencyContext =
                    await GetCustomerCurrencyContextAsync();

                if (!quote.CanCancel)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message = quote.Message
                    });
                }

                /*
                 * حاجز نهائي ضد إنشاء Refund أكبر من المال
                 * الذي تم اعتماده فعليًا لهذا الحجز.
                 */
                var verifiedPaidAmount =
                    Math.Round(
                        Math.Min(
                            Math.Max(0m, booking.TotalPrice),
                            Math.Max(0m, booking.FinancialRecord?.AmountPaidOnline ?? 0m) +
                            Math.Max(0m, booking.FinancialRecord?.AmountCollectedAtProperty ?? 0m)),
                        2);

                var verifiedPaidDeposit =
                    Math.Round(
                        Math.Min(
                            Math.Max(0m, booking.FinancialRecord?.DepositAmount ?? 0m),
                            Math.Min(
                                Math.Max(0m, booking.FinancialRecord?.AmountPaidOnline ?? 0m),
                                verifiedPaidAmount)),
                        2);

                if (quote.CancellationFee > verifiedPaidDeposit + 0.01m)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message = "تعذر تطبيق سياسة الإلغاء لأن الغرامة المحسوبة تتجاوز العربون المدفوع فعليًا."
                    });
                }

                if (quote.RefundAmount >
                    verifiedPaidAmount + 0.01m)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "تعذر إنشاء الاسترداد لأن مبلغ الاسترداد يتجاوز إجمالي المبلغ المحصل فعليًا للحجز."
                    });
                }

                var hasExistingRefund =
                    booking.RefundRequests.Any(item =>
                        !string.Equals(
                            item.Status,
                            "Cancelled",
                            StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(
                            item.Status,
                            "Rejected",
                            StringComparison.OrdinalIgnoreCase));

                if (hasExistingRefund)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "يوجد طلب استرداد مسجل لهذا الحجز مسبقًا."
                    });
                }

                var bookingStateBeforeCancellation =
                    booking.BookingState?.Trim() ??
                    string.Empty;

                booking.BookingState =
                    "Cancelled";

                booking.TimerExpiredAt =
                    null;

                booking.RejectionReason =
                    null;

                booking.CancellationReason =
                    reason;

                booking.OwnerCancellationReason =
                    null;

                booking.ClosedByUserId =
                    customerId;

                booking.ClosedByType =
                    "Customer";

                booking.ClosedAt =
                    now;

                booking.LastStatusChangedAt =
                    now;

                ReleaseRoomAssignments(
                    booking,
                    now);

                var cancellationCommission =
                    await ApplyCustomerCancellationCommissionAsync(
                        booking,
                        bookingStateBeforeCancellation,
                        quote,
                        customerId);

                if (!cancellationCommission.Success)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message =
                            cancellationCommission.Message
                    });
                }

                RefundRequest? refundRequest =
                    null;

                if (quote.RequiresRefundRequest)
                {
                    var refundRequestStatus =
                        quote.IsPaymentUnderReview ||
                        quote.RefundAmount > 0m
                            ? "Pending"
                            : "Completed";

                    refundRequest =
                        new RefundRequest
                        {
                            BookingId =
                                booking.BookingId,

                            CustomerId =
                                booking.CustomerId,

                            PlaceId =
                                booking.PlaceId,

                            /*
                             * DepositPaid اسم تاريخي في الجدول ويجب أن يبقى Snapshot
                             * لإجمالي المبلغ المحصل حتى يظل RefundAmount <= DepositPaid
                             * متوافقًا مع القيد القديم والتقارير السابقة.
                             * سياسة الإلغاء نفسها أعلاه مقيدة بالعربون المدفوع فعليًا فقط.
                             */
                            DepositPaid =
                                verifiedPaidAmount,

                            CancellationFee =
                                Math.Round(
                                    Math.Min(
                                        verifiedPaidDeposit,
                                        Math.Max(
                                            0m,
                                            quote.CancellationFee)),
                                    2),

                            RefundAmount =
                                Math.Round(
                                    Math.Min(
                                        verifiedPaidAmount,
                                        Math.Max(
                                            0m,
                                            quote.RefundAmount)),
                                    2),

                            OwnerShare =
                                cancellationCommission
                                    .OwnerShare,

                            PlatformCommission =
                                cancellationCommission
                                    .PlatformCommission,

                            CommissionReleased =
                                cancellationCommission
                                    .CommissionReleased,

                            Status =
                                refundRequestStatus,

                            Reason =
                                reason,

                            ReferenceNumber =
                                null,

                            ProofImage =
                                null,

                            RequestedAt =
                                now,

                            ProcessedAt =
                                refundRequestStatus ==
                                "Completed"
                                    ? now
                                    : null,

                            Notes =
                                BuildRefundNotes(
                                    quote)
                        };

                    _context.RefundRequests.Add(
                        refundRequest);

                    booking.RefundStatus =
                        refundRequestStatus ==
                        "Pending"
                            ? "Pending"
                            : "Completed";

                    booking.PaymentStatus =
                        refundRequestStatus ==
                        "Pending"
                            ? "RefundPending"
                            : "Cancelled";
                }
                else
                {
                    booking.RefundStatus =
                        "NotApplicable";

                    booking.PaymentStatus =
                        "Cancelled";
                }

                // بعد حفظ القيم المالية الخام وNotes، نجهز نسخة العرض
                // بعملة العميل للإشعار والـJSON فقط.
                ApplyCurrencyToCancellationQuote(
                    quote,
                    booking,
                    currencyContext);

                var placeName =
                    !string.IsNullOrWhiteSpace(
                        booking.Place?
                            .PlaceNameAndDesc)
                        ? booking.Place
                            .PlaceNameAndDesc
                            .Trim()
                        : "المنشأة";

                var hasRefundToReceive =
                    refundRequest != null &&
                    refundRequest.RefundAmount > 0m;

                AddNotification(
                    booking.CustomerId,
                    hasRefundToReceive
                        ? "لديك مبلغ مسترد"
                        : "تم إلغاء الحجز",
                    hasRefundToReceive
                        ? $"تم إلغاء الحجز رقم {booking.BookingId}. " +
                          $"لديك استرداد بقيمة {refundRequest!.RefundAmount:N0} ريال يمني. " +
                          "حدد حساب استلام المبلغ لإكمال الاسترداد."
                        : quote.RequiresRefundRequest
                            ? $"تم إلغاء الحجز رقم {booking.BookingId}. " +
                              "اكتملت التسوية حسب السياسة ولا يوجد مبلغ يحتاج إلى تحويل."
                            : $"تم إلغاء الحجز رقم {booking.BookingId} بنجاح.",
                    hasRefundToReceive
                        ? NotificationTypes.RefundRequested
                        : NotificationTypes.BookingCancelled,
                    $"/Places/BookingDetails/{booking.BookingId}",
                    NotificationReferenceTypes.Booking,
                    booking.BookingId);

                if (booking.Place?.OwnerId is int ownerId &&
                    ownerId > 0)
                {
                    AddNotification(
                        ownerId,
                        "ألغى العميل الحجز",
                        $"ألغى العميل الحجز رقم {booking.BookingId} " +
                        $"الخاص بمنشأة {placeName}. السبب: {reason}",
                        NotificationTypes.BookingCancelled,
                        $"/Owner/Reservation/ManageReservations?bookingId={booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message =
                        quote.RequiresRefundRequest
                            ? "تم إلغاء الحجز وإنشاء طلب الاسترداد بنجاح."
                            : "تم إلغاء الحجز بنجاح.",
                    bookingId =
                        booking.BookingId,
                    nextState =
                        "Cancelled",
                    filterStatus =
                        "cancelled",
                    refundStatus =
                        booking.RefundStatus,
                    refundRequestId =
                        refundRequest?
                            .RefundRequestId,
                    depositPaid =
                        quote.DepositPaid,
                    depositPaidDisplay =
                        quote.DepositPaidDisplay,
                    depositPaidText =
                        quote.DepositPaidText,
                    cancellationFee =
                        quote.CancellationFee,
                    cancellationFeeDisplay =
                        quote.CancellationFeeDisplay,
                    cancellationFeeText =
                        quote.CancellationFeeText,
                    refundAmount =
                        quote.RefundAmount,
                    refundAmountDisplay =
                        quote.RefundAmountDisplay,
                    refundAmountText =
                        quote.RefundAmountText,
                    displayCurrency =
                        quote.DisplayCurrency,
                    displayCurrencyCode =
                        quote.DisplayCurrencyCode,
                    displayCurrencySymbol =
                        quote.DisplayCurrencySymbol,
                    refundProcessingDays =
                        quote.RefundProcessingDays,
                    customerRefundMessage =
                        quote.CustomerRefundMessage
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    success = false,
                    message =
                        "تم تحديث الحجز من جلسة أخرى. حدّث الصفحة ثم حاول مرة أخرى."
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message =
                            "حدث خطأ أثناء إلغاء الحجز. لم يتم تغيير البيانات."
                    });
            }
        }

        /* ================================================================
           استعلام الإلغاء الموحد
           ================================================================ */

        private IQueryable<Booking>
            BuildCancellationQuery()
        {
            return _context.Bookings
                .Include(item =>
                    item.Place)
                .ThenInclude(place =>
                    place.PlacePolicy)
                .Include(item =>
                    item.Place)
                .ThenInclude(place =>
                    place.City)
                .Include(item =>
                    item.Room)
                .Include(item =>
                    item.Package)
                .Include(item =>
                    item.FinancialRecord)
                .Include(item =>
                    item.BookingRoomUnits)
                .Include(item =>
                    item.RefundRequests);
        }

        /* ================================================================
           حساب نتيجة سياسة الإلغاء
           ================================================================ */

        private static CancellationQuoteDto
            BuildCancellationQuote(
                Booking booking,
                DateTime now)
        {
            var state =
                booking.BookingState?.Trim() ??
                string.Empty;

            /*
             * قاعدة نهائية لحماية الاستردادات:
             * لا نسمح بإلغاء العميل أثناء مراجعة إثبات الدفع.
             * السبب: AmountPaidOnline لا يصبح مصدر حقيقة إلا بعد اعتماد الدفع.
             * بعد المراجعة:
             * - إذا رُفض الدفع يعود الحجز لحالة تسمح بالإلغاء بدون Refund مالي.
             * - إذا اعتُمد الدفع يصبح Confirmed ونحسب Refund من AmountPaidOnline الفعلي.
             */
            if (IsState(
                    state,
                    "PaymentReview"))
            {
                return CancellationQuoteDto.NotAllowed(
                    booking.BookingId,
                    "لا يمكن إلغاء الحجز أثناء مراجعة إثبات الدفع. انتظر اعتماد أو رفض الدفع أولًا ثم أعد المحاولة.");
            }

            if (!CustomerCancellableStates
                .Contains(state))
            {
                return CancellationQuoteDto.NotAllowed(
                    booking.BookingId,
                    "لا يمكن إلغاء الحجز في حالته الحالية.");
            }

            if (booking.StartDate.Date <
                now.Date)
            {
                return CancellationQuoteDto.NotAllowed(
                    booking.BookingId,
                    "لا يمكن إلغاء الحجز بعد انتهاء تاريخ بدايته.");
            }

            var policy =
                booking.Place?
                    .PlacePolicy;

            var financialRecord =
                booking.FinancialRecord;

            var isPaymentUnderReview =
                false;

            var isConfirmedPayment =
                IsPaymentApproved(booking);

            /*
             * إجمالي ما تم تحصيله فعليًا يحدد أقصى مبلغ يمكن استرداده.
             * أما سياسة الإلغاء نفسها فتطبق فقط على العربون الذي دُفع فعليًا
             * (ولا تشمل باقي المبلغ أو المبالغ المحصلة في المنشأة).
             */
            var totalPaid =
                isConfirmedPayment
                    ? Math.Max(0m, financialRecord?.AmountPaidOnline ?? 0m) +
                      Math.Max(0m, financialRecord?.AmountCollectedAtProperty ?? 0m)
                    : 0m;

            totalPaid =
                Math.Round(
                    Math.Min(
                        totalPaid,
                        Math.Max(0m, booking.TotalPrice)),
                    2);

            var depositPaid =
                isConfirmedPayment
                    ? Math.Round(
                        Math.Min(
                            Math.Max(0m, financialRecord?.DepositAmount ?? 0m),
                            Math.Max(0m, financialRecord?.AmountPaidOnline ?? 0m)),
                        2)
                    : 0m;

            depositPaid = Math.Min(depositPaid, totalPaid);

            var freeCancellationHours =
                Math.Max(
                    0,
                    policy?
                        .FreeCancellationHours ??
                    48);

            var refundProcessingDays =
                Math.Max(
                    1,
                    policy?
                        .RefundProcessingDays ??
                    3);

            var refundTransferFee =
                policy?
                    .RefundTransferFee?
                    .Trim() ??
                "CustomerPays";

            var cancellationStartAt =
                booking.Package?.StartTime.HasValue == true
                    ? booking.StartDate.Date.Add(booking.Package.StartTime.Value)
                    : booking.StartDate;

            if (!booking.PackageId.HasValue &&
                booking.StartDate.TimeOfDay == TimeSpan.Zero &&
                TimeSpan.TryParse(
                    policy?.CheckInTime,
                    out var cancellationCheckInTime))
            {
                cancellationStartAt =
                    booking.StartDate.Date.Add(cancellationCheckInTime);
            }

            var hoursUntilStart =
                Math.Max(
                    0,
                    (cancellationStartAt - now)
                        .TotalHours);

            var isWithinNormalFreeWindow =
                hoursUntilStart >=
                freeCancellationHours;

            var isWithinImmediateGrace =
                depositPaid > 0m &&
                BookingMoneyPolicy
                    .IsWithinImmediateCancellationGrace(
                        policy?.ImmediateGracePeriod == true,
                        booking.CreatedAt,
                        financialRecord?.UpdatedAt,
                        now);

            var isFreeCancellation =
                depositPaid <= 0m ||
                isWithinNormalFreeWindow ||
                isWithinImmediateGrace;

            var penaltyType =
                policy?
                    .PenaltyType?
                    .Trim() ??
                "LoseDeposit";

            var penaltyValue =
                Math.Max(
                    0m,
                    policy?
                        .PenaltyValue ??
                    0m);

            decimal cancellationFee =
                0m;

            string policyLabel;

            if (depositPaid <= 0m)
            {
                policyLabel =
                    "إلغاء مباشر بدون استرداد مالي";
            }
            else if (isFreeCancellation)
            {
                policyLabel =
                    isWithinImmediateGrace
                        ? "إلغاء مجاني خلال فترة التراجع"
                        : "إلغاء مجاني ضمن المدة المحددة";
            }
            else
            {
                cancellationFee =
                    CalculateCancellationFee(
                        booking,
                        depositPaid,
                        penaltyType,
                        penaltyValue);

                policyLabel =
                    GetPenaltyLabel(
                        penaltyType,
                        penaltyValue);
            }

            cancellationFee =
                Math.Round(
                    Math.Min(
                        depositPaid,
                        Math.Max(
                            0m,
                            cancellationFee)),
                    2);

            var refundAmount =
                Math.Round(
                    Math.Max(
                        0m,
                        totalPaid -
                        cancellationFee),
                    2);

            var requiresRefundRequest =
                totalPaid > 0m;

            var refundTransferFeeLabel =
                string.Equals(
                    refundTransferFee,
                    "FacilityPays",
                    StringComparison.OrdinalIgnoreCase)
                    ? "تتحمل المنشأة رسوم التحويل"
                    : "يتحمل العميل رسوم التحويل";

            string customerRefundMessage;

            if (!requiresRefundRequest)
            {
                customerRefundMessage =
                    "لا يوجد مبلغ مدفوع يحتاج إلى استرداد.";
            }
            else if (refundAmount <= 0m)
            {
                customerRefundMessage =
                    "لا يوجد مبلغ مسترد بعد تطبيق سياسة الإلغاء.";
            }
            else
            {
                customerRefundMessage =
                    $"المبلغ المتوقع استرداده {refundAmount:N0} ريال " +
                    $"خلال {refundProcessingDays} أيام عمل.";
            }

            return new CancellationQuoteDto
            {
                BookingId =
                    booking.BookingId,
                CanCancel =
                    true,
                BookingState =
                    state,
                IsPaymentUnderReview =
                    isPaymentUnderReview,
                HasConfirmedPayment =
                    isConfirmedPayment,
                DepositPaid =
                    depositPaid,
                CancellationFee =
                    cancellationFee,
                RefundAmount =
                    refundAmount,
                IsFreeCancellation =
                    isFreeCancellation,
                FreeCancellationHours =
                    freeCancellationHours,
                HoursUntilStart =
                    Math.Round(
                        hoursUntilStart,
                        1),
                PenaltyType =
                    penaltyType,
                PenaltyValue =
                    penaltyValue,
                PolicyLabel =
                    policyLabel,
                RefundProcessingDays =
                    refundProcessingDays,
                RefundTransferFee =
                    refundTransferFee,
                RefundTransferFeeLabel =
                    refundTransferFeeLabel,
                RequiresRefundRequest =
                    requiresRefundRequest,
                CustomerRefundMessage =
                    customerRefundMessage,
                Message =
                    "يمكن إلغاء الحجز. راجع ملخص الاسترداد قبل التأكيد."
            };
        }

        private static void ApplyCurrencyToCancellationQuote(
            CancellationQuoteDto quote,
            Booking booking,
            CustomerCurrencyContext currencyContext)
        {
            string? exchangeRateRegion =
                booking.Place?
                    .City?
                    .ExchangeRateRegion;

            quote.DisplayCurrency =
                currencyContext.Currency;
            quote.DisplayCurrencyCode =
                currencyContext.Code;
            quote.DisplayCurrencySymbol =
                currencyContext.Symbol;

            quote.DepositPaidDisplay =
                currencyContext.Convert(
                    quote.DepositPaid,
                    exchangeRateRegion);
            quote.DepositPaidText =
                currencyContext.Format(
                    quote.DepositPaid,
                    exchangeRateRegion);

            quote.CancellationFeeDisplay =
                currencyContext.Convert(
                    quote.CancellationFee,
                    exchangeRateRegion);
            quote.CancellationFeeText =
                currencyContext.Format(
                    quote.CancellationFee,
                    exchangeRateRegion);

            quote.RefundAmountDisplay =
                currencyContext.Convert(
                    quote.RefundAmount,
                    exchangeRateRegion);
            quote.RefundAmountText =
                currencyContext.Format(
                    quote.RefundAmount,
                    exchangeRateRegion);

            if (string.Equals(
                    quote.PenaltyType,
                    "DeductFixed",
                    StringComparison.OrdinalIgnoreCase))
            {
                quote.PenaltyValueDisplay =
                    currencyContext.Convert(
                        quote.PenaltyValue,
                        exchangeRateRegion);
                quote.PenaltyValueText =
                    currencyContext.Format(
                        quote.PenaltyValue,
                        exchangeRateRegion);
                quote.PolicyLabel =
                    $"خصم مبلغ ثابت قدره {quote.PenaltyValueText}";
            }

            if (quote.RequiresRefundRequest &&
                quote.RefundAmount > 0m)
            {
                quote.CustomerRefundMessage =
                    $"المبلغ المتوقع استرداده {quote.RefundAmountText} " +
                    $"خلال {quote.RefundProcessingDays} أيام عمل.";
            }
        }

        private static decimal
            CalculateCancellationFee(
                Booking booking,
                decimal depositPaid,
                string penaltyType,
                decimal penaltyValue)
        {
            var configuredDeposit =
                Math.Max(
                    0m,
                    booking.FinancialRecord?
                        .DepositAmount ??
                    0m);

            var requiredDeposit =
                Math.Round(
                    configuredDeposit > 0m
                        ? Math.Min(
                            configuredDeposit,
                            Math.Max(
                                0m,
                                depositPaid))
                        : Math.Max(
                            0m,
                            depositPaid),
                    2);

            if (string.Equals(
                    penaltyType,
                    "DeductPercentage",
                    StringComparison.OrdinalIgnoreCase))
            {
                var percentage =
                    Math.Min(
                        100m,
                        Math.Max(
                            0m,
                            penaltyValue));

                /*
                 * إذا كان العميل قد دفع كامل قيمة الحجز إلكترونيًا،
                 * لا نحسب نسبة الغرامة على كامل الدفعة؛ بل على العربون
                 * المطلوب للحجز لأنه هو أساس سياسة الإلغاء.
                 */
                return requiredDeposit *
                       percentage /
                       100m;
            }

            if (string.Equals(
                    penaltyType,
                    "DeductFixed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return penaltyValue;
            }

            if (string.Equals(
                    penaltyType,
                    "LoseFirstNight",
                    StringComparison.OrdinalIgnoreCase))
            {
                var firstNightAmount =
                    booking.Room != null
                        ? booking.Room.Price *
                          Math.Max(
                              1,
                              booking.Quantity)
                        : booking.Package != null
                            ? booking.Package.Price
                            : booking.TotalPrice;

                return firstNightAmount;
            }

            /*
             * LoseDeposit وأي قيمة غير معروفة:
             * نخسر العربون المطلوب فقط. إذا كان العميل قد دفع Full
             * فيبقى أي مبلغ زائد عن العربون مستحقًا للاسترداد.
             */
            return requiredDeposit;
        }

        private static string
            GetPenaltyLabel(
                string penaltyType,
                decimal penaltyValue)
        {
            if (string.Equals(
                    penaltyType,
                    "DeductPercentage",
                    StringComparison.OrdinalIgnoreCase))
            {
                return $"خصم {penaltyValue:N0}% من العربون";
            }

            if (string.Equals(
                    penaltyType,
                    "DeductFixed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return $"خصم مبلغ ثابت قدره {penaltyValue:N0} ريال";
            }

            if (string.Equals(
                    penaltyType,
                    "LoseFirstNight",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "خصم قيمة الليلة الأولى";
            }

            return "مصادرة العربون بالكامل";
        }

        private static string
            BuildRefundNotes(
                CancellationQuoteDto quote)
        {
            return
                $"السياسة المطبقة: {quote.PolicyLabel}. " +
                $"{quote.RefundTransferFeeLabel}. " +
                $"مدة المعالجة: {quote.RefundProcessingDays} أيام عمل.";
        }

        /* ================================================================
           صفحة حجوزاتي
           ================================================================ */

        // ================================================================
        // بيانات وجهة الاسترداد + تأكيد الاستلام
        // ================================================================

        [HttpGet("/Bookings/RefundWallets")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> RefundWallets()
        {
            if (!TryGetCustomerId(out _))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب العميل الحالي."
                });
            }

            var wallets =
                await _context.SupportedWallets
                    .AsNoTracking()
                    .Where(item =>
                        item.IsActive == true)
                    .OrderBy(item =>
                        item.WalletName)
                    .Select(item =>
                        new
                        {
                            walletId =
                                item.WalletId,

                            walletName =
                                item.WalletName,

                            logoPath =
                                item.LogoPath
                        })
                    .ToListAsync();

            return Json(new
            {
                success = true,
                wallets
            });
        }

        [HttpGet("/Bookings/RefundProof/{id:int}")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> RefundProof(
            int id)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Unauthorized();
            }

            if (id <= 0)
            {
                return NotFound();
            }

            var proofPath =
                await _context.RefundRequests
                    .AsNoTracking()
                    .Where(item =>
                        item.RefundRequestId == id &&
                        item.CustomerId == customerId)
                    .Select(item =>
                        item.ProofImage)
                    .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(proofPath))
            {
                return NotFound();
            }

            var webRootPath =
                string.IsNullOrWhiteSpace(
                    _environment.WebRootPath)
                    ? Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot")
                    : _environment.WebRootPath;

            var normalizedRelativePath =
                proofPath
                    .Trim()
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar)
                    .Replace(
                        '\\',
                        Path.DirectorySeparatorChar)
                    .TrimStart(
                        Path.DirectorySeparatorChar);

            var fullWebRoot =
                Path.GetFullPath(
                    webRootPath);

            var physicalPath =
                Path.GetFullPath(
                    Path.Combine(
                        fullWebRoot,
                        normalizedRelativePath));

            if (!physicalPath.StartsWith(
                    fullWebRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !System.IO.File.Exists(
                    physicalPath))
            {
                return NotFound();
            }

            var extension =
                Path.GetExtension(
                        physicalPath)
                    .ToLowerInvariant();

            var contentType =
                extension switch
                {
                    ".jpg" or ".jpeg" =>
                        "image/jpeg",
                    ".png" =>
                        "image/png",
                    ".webp" =>
                        "image/webp",
                    _ =>
                        "application/octet-stream"
                };

            Response.Headers[
                "Cache-Control"] =
                "private, no-store, max-age=0";

            return PhysicalFile(
                physicalPath,
                contentType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRefundDestination(
            [FromBody] SetRefundDestinationRequestDto? request)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب العميل الحالي."
                });
            }

            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات حساب الاسترداد غير مكتملة."
                });
            }

            if (!ModelState.IsValid)
            {
                var message = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                    ?? "تحقق من بيانات حساب الاسترداد.";

                return BadRequest(new { success = false, message });
            }

            var refund = await _context.RefundRequests
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking.Place)
                .FirstOrDefaultAsync(item =>
                    item.RefundRequestId == request.RefundRequestId &&
                    item.CustomerId == customerId);

            if (refund == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "طلب الاسترداد غير موجود."
                });
            }

            if (refund.RefundAmount <= 0m)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "لا يوجد مبلغ مسترد يحتاج إلى حساب استلام."
                });
            }

            if (IsState(refund.Status, FinancialRefundStatuses.Completed) ||
                IsState(refund.Status, FinancialRefundStatuses.Cancelled) ||
                IsState(refund.Status, FinancialRefundStatuses.Rejected) ||
                IsState(refund.Status, FinancialRefundStatuses.Failed))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "لا يمكن تعديل وجهة الاسترداد في الحالة الحالية."
                });
            }

            if (refund.TransferredAt.HasValue)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "تم تحويل الاسترداد بالفعل، لذلك لا يمكن تغيير حساب الاستلام."
                });
            }

            var wallet = await _context.SupportedWallets
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.WalletId == request.RefundWalletId &&
                    item.IsActive == true);

            if (wallet == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "شبكة الاسترداد غير متاحة حاليًا."
                });
            }

            var accountNumber = request.AccountNumber?.Trim() ?? string.Empty;
            var beneficiaryName = request.BeneficiaryName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(accountNumber) ||
                accountNumber.Length > 100)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم حساب الاسترداد غير صحيح."
                });
            }

            if (beneficiaryName.Length < 2 || beneficiaryName.Length > 150)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "اسم المستفيد غير صحيح."
                });
            }

            var hadRefundDestination =
                refund.RefundWalletId.HasValue &&
                refund.RefundWalletId.Value > 0 &&
                !string.IsNullOrWhiteSpace(refund.RefundAccountNumber) &&
                !string.IsNullOrWhiteSpace(refund.RefundBeneficiaryName);

            refund.RefundWalletId = request.RefundWalletId;
            refund.RefundAccountNumber = accountNumber;
            refund.RefundBeneficiaryName = beneficiaryName;

            if (!hadRefundDestination &&
                refund.Booking?.Place?.OwnerId is int ownerId &&
                ownerId > 0)
            {
                AddNotification(
                    ownerId,
                    "الاسترداد جاهز للتحويل",
                    $"حدد العميل حساب استلام استرداد الحجز رقم {refund.BookingId} " +
                    $"بقيمة {refund.RefundAmount:N0} ريال يمني. " +
                    "يمكنك الآن تنفيذ التحويل من تبويب الاستردادات.",
                    NotificationTypes.RefundReadyForTransfer,
                    $"/Owner/Financials/Manage?financeTab=refunds&BookingId={refund.BookingId}",
                    NotificationReferenceTypes.Booking,
                    refund.BookingId);
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "تم حفظ حساب استلام الاسترداد بنجاح.",
                refundRequestId = refund.RefundRequestId,
                walletId = refund.RefundWalletId,
                walletName = wallet.WalletName,
                accountNumber = refund.RefundAccountNumber,
                beneficiaryName = refund.RefundBeneficiaryName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmRefundReceived(
            [FromBody] ConfirmRefundReceivedRequestDto? request)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب العميل الحالي."
                });
            }

            if (request == null || request.RefundRequestId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "طلب الاسترداد غير صحيح."
                });
            }

            await using var receiptTransaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, HttpContext.RequestAborted);
            var refund = await _context.RefundRequests
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking.Place)
                .FirstOrDefaultAsync(item =>
                    item.RefundRequestId == request.RefundRequestId &&
                    item.CustomerId == customerId);

            if (refund == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "طلب الاسترداد غير موجود."
                });
            }

            if (IsState(refund.Status, FinancialRefundStatuses.Completed))
            {
                return Json(new
                {
                    success = true,
                    message = "تم تأكيد استلام الاسترداد مسبقًا.",
                    refundRequestId = refund.RefundRequestId
                });
            }

            if (!IsState(refund.Status, FinancialRefundStatuses.Processing) ||
                !refund.TransferredAt.HasValue ||
                string.IsNullOrWhiteSpace(refund.ReferenceNumber))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "لم يسجل المالك تحويل الاسترداد بعد."
                });
            }

            var now = DateTime.Now;

            refund.CustomerConfirmedAt = now;
            refund.ProcessedAt = now;
            refund.Status = FinancialRefundStatuses.Completed;

            if (refund.Booking != null)
            {
                refund.Booking.RefundStatus = FinancialRefundStatuses.Completed;

                if (IsState(refund.Booking.PaymentStatus, "RefundPending"))
                {
                    refund.Booking.PaymentStatus = "Cancelled";
                }
            }

            AddNotification(
                refund.CustomerId,
                "اكتمل استردادك",
                $"تم تأكيد استلام استرداد الحجز رقم {refund.BookingId} " +
                $"بقيمة {refund.RefundAmount:N0} ريال يمني. اكتملت التسوية بنجاح.",
                NotificationTypes.RefundCompleted,
                $"/Places/BookingDetails/{refund.BookingId}",
                NotificationReferenceTypes.Booking,
                refund.BookingId);

            if (refund.Booking?.Place?.OwnerId is int ownerId &&
                ownerId > 0)
            {
                AddNotification(
                    ownerId,
                    "أكد العميل استلام الاسترداد",
                    $"أكد العميل استلام مبلغ {refund.RefundAmount:N0} ريال يمني " +
                    $"للحجز رقم {refund.BookingId}. اكتملت التسوية المالية.",
                    NotificationTypes.RefundCompleted,
                    $"/Owner/Financials/Manage?financeTab=refunds&BookingId={refund.BookingId}",
                    NotificationReferenceTypes.Booking,
                    refund.BookingId);
            }

            await _context.SaveChangesAsync();
            await receiptTransaction.CommitAsync(HttpContext.RequestAborted);

            return Json(new
            {
                success = true,
                message = "تم تأكيد استلام مبلغ الاسترداد بنجاح.",
                refundRequestId = refund.RefundRequestId,
                completedAt = now
            });
        }

        [HttpGet("/Bookings/MyBookings")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> MyBookings()
        {
            if (!TryGetCustomerId(
                    out var customerId))
            {
                return Challenge();
            }

            /*
             * تحديث الحجوزات التي انتهت
             * مهلة دفعها قبل عرض الصفحة.
             */
            await ExpireOverduePaymentsAsync(
                customerId);

            /*
             * تحميل حجوزات العميل فقط،
             * مع المنشأة والتصنيف والصور والغرفة والباقة.
             */
            var bookings =
                await _context.Bookings
                    .AsNoTracking()

                    .Include(booking =>
                        booking.Place)
                        .ThenInclude(place =>
                            place.Category)

                    .Include(booking =>
                        booking.Place)
                        .ThenInclude(place =>
                            place.City)

                    .Include(booking =>
                        booking.Place)
                        .ThenInclude(place =>
                            place.PlaceImages)

                    .Include(booking =>
                        booking.Place)
                        .ThenInclude(place =>
                            place.PlacePolicy)

                    .Include(booking =>
                        booking.Room)

                    .Include(booking =>
                        booking.BookingRoomUnits)
                        .ThenInclude(assignment =>
                            assignment.RoomUnit)

                    .Include(booking =>
                        booking.Package)

                    .Include(booking =>
                        booking.FinancialRecord)

                    .Include(booking =>
                        booking.RefundRequests)

                    .Where(booking =>
                        booking.CustomerId ==
                            customerId)

                    .OrderByDescending(booking =>
                        booking.LastStatusChangedAt ??
                        booking.CreatedAt)

                    .ThenByDescending(booking =>
                        booking.BookingId)

                    .ToListAsync();

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            /*
             * تحويل البيانات إلى DTO مختصر
             * خاص بصفحة حجوزاتي.
             */
            var bookingItems =
                bookings
                    .Select(booking =>
                    {
                        var firstGalleryImage =
                            booking.Place?
                                .PlaceImages?
                                .OrderBy(image =>
                                    image.ImageId)
                                .Select(image =>
                                    image.ImagePath)
                                .FirstOrDefault();

                        var latestRefundRequest =
                            booking.RefundRequests
                                .OrderByDescending(item =>
                                    item.RequestedAt)
                                .ThenByDescending(item =>
                                    item.RefundRequestId)
                                .FirstOrDefault();

                        var financialRecord =
                            booking.FinancialRecord;

                        string? exchangeRateRegion =
                            booking.Place?
                                .City?
                                .ExchangeRateRegion;

                        decimal effectiveOriginalTotalPrice =
                            booking.OriginalTotalPrice > 0m
                                ? booking.OriginalTotalPrice
                                : booking.TotalPrice +
                                  Math.Max(
                                      0m,
                                      booking.DiscountAmount);

                        decimal depositAmount =
                            Math.Max(
                                0m,
                                financialRecord?
                                    .DepositAmount ??
                                0m);

                        decimal amountPaidOnline =
                            Math.Max(
                                0m,
                                financialRecord?
                                    .AmountPaidOnline ??
                                0m);

                        decimal refundAmount =
                            Math.Max(
                                0m,
                                latestRefundRequest?
                                    .RefundAmount ??
                                0m);

                        decimal cancellationFee =
                            Math.Max(
                                0m,
                                latestRefundRequest?
                                    .CancellationFee ??
                                0m);

                        return new CustomerBookingItemDto
                        {
                            BookingId =
                                booking.BookingId,

                            PlaceId =
                                booking.PlaceId,

                            PlaceName =
                                !string.IsNullOrWhiteSpace(
                                    booking.Place?
                                        .PlaceNameAndDesc)
                                    ? booking.Place
                                        .PlaceNameAndDesc
                                        .Trim()
                                    : "منشأة غير معروفة",

                            PlaceType =
                                booking.Place?
                                    .Category?
                                    .CategoryName?
                                    .Trim(),

                            PlaceImageUrl =
                                GetPlaceImageUrl(
                                    booking.Place?
                                        .CoverImage,

                                    booking.Place?
                                        .LogoImage,

                                    firstGalleryImage,

                                    booking.Place?
                                        .Category?
                                        .ImagePath),

                            RoomId =
                                booking.RoomId,

                            RoomName =
                                BuildRoomDisplayName(
                                    booking.Room?
                                        .RoomType,

                                    booking.Room?
                                        .RoomNumber),

                            AssignedRoomUnits =
                                booking.BookingRoomUnits
                                    .OrderBy(assignment =>
                                        assignment.RoomUnit.FloorNumber)
                                    .ThenBy(assignment =>
                                        assignment.RoomUnit.RoomNumber)
                                    .Select(assignment =>
                                        new CustomerAssignedRoomUnitDto
                                        {
                                            RoomUnitId =
                                                assignment.RoomUnitId,
                                            RoomNumber =
                                                assignment.RoomUnit.RoomNumber?
                                                    .Trim() ??
                                                string.Empty,
                                            FloorNumber =
                                                assignment.RoomUnit.FloorNumber,
                                            FloorLabel =
                                                assignment.RoomUnit.FloorLabel?
                                                    .Trim(),
                                            AssignmentStatus =
                                                assignment.AssignmentStatus?
                                                    .Trim() ??
                                                "Held"
                                        })
                                    .ToList(),

                            PackageId =
                                booking.PackageId,

                            PackageName =
                                booking.Package?
                                    .PackageName?
                                    .Trim(),

                            StartDate =
                                booking.StartDate,

                            EndDate =
                                booking.EndDate,

                            TotalPrice =
                                booking.TotalPrice,

                            TotalPriceDisplay =
                                currencyContext.Convert(
                                    booking.TotalPrice,
                                    exchangeRateRegion),

                            TotalPriceText =
                                currencyContext.Format(
                                    booking.TotalPrice,
                                    exchangeRateRegion),

                            DisplayCurrency =
                                currencyContext.Currency,

                            DisplayCurrencyCode =
                                currencyContext.Code,

                            DisplayCurrencySymbol =
                                currencyContext.Symbol,

                            PromotionId =
                                booking.PromotionId,

                            OriginalTotalPrice =
                                booking.OriginalTotalPrice,

                            OriginalTotalPriceDisplay =
                                currencyContext.Convert(
                                    effectiveOriginalTotalPrice,
                                    exchangeRateRegion),

                            OriginalTotalPriceText =
                                currencyContext.Format(
                                    effectiveOriginalTotalPrice,
                                    exchangeRateRegion),

                            DiscountAmount =
                                Math.Max(0m, booking.DiscountAmount),

                            DiscountAmountDisplay =
                                currencyContext.Convert(
                                    Math.Max(0m, booking.DiscountAmount),
                                    exchangeRateRegion),

                            DiscountAmountText =
                                currencyContext.Format(
                                    Math.Max(0m, booking.DiscountAmount),
                                    exchangeRateRegion),

                            PromotionTitleSnapshot =
                                booking.PromotionTitleSnapshot?.Trim(),

                            PromotionCodeSnapshot =
                                booking.PromotionCodeSnapshot?.Trim(),

                            DiscountTypeSnapshot =
                                booking.DiscountTypeSnapshot?.Trim(),

                            DiscountValueSnapshot =
                                booking.DiscountValueSnapshot,

                            DiscountValueSnapshotDisplay =
                                booking.DiscountValueSnapshot.HasValue &&
                                string.Equals(
                                    booking.DiscountTypeSnapshot,
                                    "FixedAmount",
                                    StringComparison.OrdinalIgnoreCase)
                                    ? currencyContext.Convert(
                                        booking.DiscountValueSnapshot.Value,
                                        exchangeRateRegion)
                                    : null,

                            DiscountValueSnapshotText =
                                booking.DiscountValueSnapshot.HasValue &&
                                string.Equals(
                                    booking.DiscountTypeSnapshot,
                                    "FixedAmount",
                                    StringComparison.OrdinalIgnoreCase)
                                    ? currencyContext.Format(
                                        booking.DiscountValueSnapshot.Value,
                                        exchangeRateRegion)
                                    : string.Empty,

                            BookingState =
                                !string.IsNullOrWhiteSpace(
                                    booking.BookingState)
                                    ? booking.BookingState
                                        .Trim()
                                    : "Pending",

                            PaymentStatus =
                                !string.IsNullOrWhiteSpace(
                                    booking.PaymentStatus)
                                    ? booking.PaymentStatus
                                        .Trim()
                                    : "Unpaid",

                            PaymentMethod =
                                booking.PaymentMethod?
                                    .Trim(),

                            DepositAmount =
                                depositAmount,

                            DepositAmountDisplay =
                                currencyContext.Convert(
                                    depositAmount,
                                    exchangeRateRegion),

                            DepositAmountText =
                                currencyContext.Format(
                                    depositAmount,
                                    exchangeRateRegion),

                            AmountPaidOnline =
                                amountPaidOnline,

                            AmountPaidOnlineDisplay =
                                currencyContext.Convert(
                                    amountPaidOnline,
                                    exchangeRateRegion),

                            AmountPaidOnlineText =
                                currencyContext.Format(
                                    amountPaidOnline,
                                    exchangeRateRegion),

                            IsPaymentConfirmed =
                                IsPaymentApproved(booking),

                            HasPaymentProof =
                                !string.IsNullOrWhiteSpace(
                                    booking
                                        .PaymentNotificationImage),

                            RefundStatus =
                                booking.RefundStatus?
                                    .Trim() ??
                                "NotApplicable",

                            RefundRequestStatus =
                                latestRefundRequest?
                                    .Status?
                                    .Trim(),

                            RefundRequestId =
                                latestRefundRequest?
                                    .RefundRequestId,

                            RefundWalletId =
                                latestRefundRequest?
                                    .RefundWalletId,

                            RefundAccountNumber =
                                latestRefundRequest?
                                    .RefundAccountNumber?
                                    .Trim(),

                            RefundBeneficiaryName =
                                latestRefundRequest?
                                    .RefundBeneficiaryName?
                                    .Trim(),

                            RefundReferenceNumber =
                                latestRefundRequest?
                                    .ReferenceNumber?
                                    .Trim(),

                            RefundTransferredAt =
                                latestRefundRequest?
                                    .TransferredAt,

                            RefundCustomerConfirmedAt =
                                latestRefundRequest?
                                    .CustomerConfirmedAt,

                            HasRefundProof =
                                !string.IsNullOrWhiteSpace(
                                    latestRefundRequest?
                                        .ProofImage),

                            RefundNotes =
                                latestRefundRequest?
                                    .Notes?
                                    .Trim(),

                            RefundAmount =
                                refundAmount,

                            RefundAmountDisplay =
                                currencyContext.Convert(
                                    refundAmount,
                                    exchangeRateRegion),

                            RefundAmountText =
                                currencyContext.Format(
                                    refundAmount,
                                    exchangeRateRegion),

                            CancellationFee =
                                cancellationFee,

                            CancellationFeeDisplay =
                                currencyContext.Convert(
                                    cancellationFee,
                                    exchangeRateRegion),

                            CancellationFeeText =
                                currencyContext.Format(
                                    cancellationFee,
                                    exchangeRateRegion),

                            CancellationReason =
                                booking.CancellationReason?
                                    .Trim() ??
                                (IsState(
                                     booking.BookingState,
                                     "Cancelled") &&
                                 !IsState(
                                     booking.ClosedByType,
                                     "Owner")
                                    ? latestRefundRequest?
                                        .Reason?
                                        .Trim() ??
                                      booking.RejectionReason?
                                        .Trim()
                                    : null),

                            OwnerCancellationReason =
                                booking.OwnerCancellationReason?
                                    .Trim() ??
                                (IsState(
                                     booking.BookingState,
                                     "Cancelled") &&
                                 IsState(
                                     booking.ClosedByType,
                                     "Owner")
                                    ? latestRefundRequest?
                                        .Reason?
                                        .Trim() ??
                                      booking.RejectionReason?
                                        .Trim()
                                    : null),

                            PaymentProofRejectionReason =
                                booking.PaymentProofRejectionReason?
                                    .Trim(),

                            PaymentProofRejectedAt =
                                booking.PaymentProofRejectedAt,

                            PaymentProofReviewCount =
                                Math.Max(
                                    0,
                                    booking.PaymentProofReviewCount),

                            LastStatusChangedAt =
                                booking.LastStatusChangedAt,

                            AcceptedAt =
                                booking.AcceptedAt,

                            PaymentApprovedAt =
                                booking.PaymentApprovedAt,

                            CompletedAt =
                                booking.CompletedAt,

                            NoShowAt =
                                booking.NoShowAt,

                            ClosedAt =
                                booking.ClosedAt,

                            ClosedByUserId =
                                booking.ClosedByUserId,

                            ClosedByType =
                                booking.ClosedByType?
                                    .Trim(),

                            RefundProcessingDays =
                                Math.Max(
                                    1,
                                    booking.Place?
                                        .PlacePolicy?
                                        .RefundProcessingDays ??
                                    3),

                            TimerExpiredAt =
                                booking.TimerExpiredAt,

                            RejectionReason =
                                booking.RejectionReason?
                                    .Trim(),

                            CreatedAt =
                                booking.CreatedAt,

                            Quantity =
                                Math.Max(
                                    1,
                                    booking.Quantity),

                            AdultsCount =
                                Math.Max(
                                    0,
                                    booking.AdultsCount),

                            ChildrenCount =
                                Math.Max(
                                    0,
                                    booking.ChildrenCount),

                            BookingType =
                                booking.BookingType?
                                    .Trim(),

                            SeatingSetup =
                                booking.SeatingSetup?
                                    .Trim()
                        };
                    })
                    .ToList();

            var model =
                new CustomerBookingsPageDto
                {
                    Bookings =
                        bookingItems
                };

            return View(
                "~/Views/Panel/Bookings/MyBookings.cshtml",
                model);
        }
    }
}
