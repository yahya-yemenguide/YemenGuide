using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Models.Payments;
using YemenGuide.Web.Services.Notifications;
using YemenGuide.Web.Services.Payments;

namespace YemenGuide.Web.Controllers
{
    [Authorize]
    public sealed class AdditionalPaymentsController : Controller
    {
        private const long MaximumProofSize = 5 * 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp"
            };

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly INotificationService _notificationService;

        public AdditionalPaymentsController(
            AppDbContext context,
            IWebHostEnvironment environment,
            INotificationService notificationService)
        {
            _context = context;
            _environment = environment;
            _notificationService = notificationService;
        }

        private bool TryGetCustomerId(out int customerId)
        {
            return int.TryParse(
                       User.FindFirstValue(ClaimTypes.NameIdentifier),
                       out customerId) &&
                   customerId > 0;
        }

        private static bool IsState(string? value, string expected)
        {
            return string.Equals(
                value?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseClock(string? value, out TimeSpan time)
        {
            time = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var formats = new[]
            {
                @"h\:mm",
                @"hh\:mm",
                @"h\:mm\:ss",
                @"hh\:mm\:ss"
            };

            if (TimeSpan.TryParseExact(
                    value.Trim(),
                    formats,
                    CultureInfo.InvariantCulture,
                    out time))
            {
                return true;
            }

            if (DateTime.TryParse(
                    value.Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsed))
            {
                time = parsed.TimeOfDay;
                return true;
            }

            return false;
        }

        private static List<TimeSpan> ExtractTimeValues(string? value)
        {
            var result = new List<TimeSpan>();

            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            foreach (Match match in Regex.Matches(
                         value,
                         @"(?<!\d)([01]?\d|2[0-3]):([0-5]\d)(?!\d)"))
            {
                if (TimeSpan.TryParseExact(
                        match.Value,
                        new[] { @"h\:mm", @"hh\:mm" },
                        CultureInfo.InvariantCulture,
                        out var parsed))
                {
                    result.Add(parsed);
                }
            }

            return result;
        }

        private static DateTime GetOperationalStartAt(Booking booking)
        {
            if (booking.Package?.StartTime.HasValue == true)
            {
                return booking.StartDate.Date.Add(
                    booking.Package.StartTime.Value);
            }

            if (booking.StartDate.TimeOfDay != TimeSpan.Zero)
            {
                return booking.StartDate;
            }

            var packageTimes = ExtractTimeValues(
                booking.Package?.TimeFrame);

            if (packageTimes.Count > 0)
            {
                return booking.StartDate.Date.Add(packageTimes.First());
            }

            if (TryParseClock(
                    booking.Place?.PlacePolicy?.CheckInTime,
                    out var checkInTime))
            {
                return booking.StartDate.Date.Add(checkInTime);
            }

            return booking.StartDate.Date;
        }

        private IQueryable<Booking> BuildBookingQuery()
        {
            return _context.Bookings
                .Include(item => item.Place)
                    .ThenInclude(place => place.PlacePolicy)
                .Include(item => item.Package)
                .Include(item => item.FinancialRecord);
        }

        private static string GetEligibilityMessage(
            Booking booking,
            DateTime now,
            decimal remaining,
            bool hasPending)
        {
            if (!IsState(booking.BookingState, "Confirmed"))
            {
                return "الدفع الإضافي متاح للحجز المؤكد فقط.";
            }

            var financial = booking.FinancialRecord;
            var requiredDeposit = Math.Round(
                Math.Min(
                    Math.Max(0m, financial?.DepositAmount ?? 0m),
                    Math.Max(0m, booking.TotalPrice)),
                2);

            var paidOnline = Math.Round(
                Math.Max(0m, financial?.AmountPaidOnline ?? 0m),
                2);

            if (requiredDeposit <= 0m ||
                !booking.PaymentApprovedAt.HasValue ||
                paidOnline + 0.01m < requiredDeposit)
            {
                return "الدفع الإضافي يتاح بعد اعتماد دفع العربون أولًا.";
            }

            if (remaining <= 0m)
            {
                return "لا يوجد مبلغ متبقٍ على هذا الحجز.";
            }

            if (now >= GetOperationalStartAt(booking))
            {
                return "بدأت فترة الحجز؛ أصبح تحصيل المتبقي من خلال المنشأة.";
            }

            if (hasPending)
            {
                return "يوجد دفع إضافي قيد المراجعة. انتظر نتيجة المراجعة أولًا.";
            }

            return string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Status(int bookingId)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب العميل الحالي."
                });
            }

            var booking = await BuildBookingQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.BookingId == bookingId &&
                    item.CustomerId == customerId,
                    HttpContext.RequestAborted);

            if (booking == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الحجز غير موجود أو لا يتبع حسابك."
                });
            }

            var financial = booking.FinancialRecord;
            if (financial == null)
            {
                return Json(new
                {
                    success = true,
                    allowed = false,
                    pending = false,
                    approvedAdditionalCount = 0,
                    message = "السجل المالي للحجز غير موجود."
                });
            }

            if (!BookingPaymentLedger.TryRead(
                    financial.PaymentTransactionsJson,
                    out var transactions))
            {
                return Json(new
                {
                    success = true,
                    allowed = false,
                    pending = false,
                    approvedAdditionalCount = 0,
                    message = "تعذر قراءة سجل الدفعات الإضافية. راجع الإدارة قبل إضافة دفعة جديدة."
                });
            }

            var pending = transactions
                .Where(BookingPaymentLedger.IsPending)
                .OrderByDescending(item => item.SubmittedAt)
                .FirstOrDefault();

            var approvedPayments = transactions
                .Where(BookingPaymentLedger.IsApproved)
                .OrderByDescending(item =>
                    item.ReceiptIssuedAt ??
                    item.ReviewedAt ??
                    item.SubmittedAt)
                .ToList();

            var approvedCount = approvedPayments.Count;
            var latestApproved = approvedPayments.FirstOrDefault();

            var remaining = Math.Round(
                Math.Max(0m, financial.AmountToPayAtProperty),
                2);

            var message = GetEligibilityMessage(
                booking,
                DateTime.Now,
                remaining,
                pending != null);

            return Json(new
            {
                success = true,
                allowed = string.IsNullOrWhiteSpace(message),
                pending = pending != null,
                pendingAmount = pending?.Amount ?? 0m,
                amountPaidOnline = Math.Max(0m, financial.AmountPaidOnline),
                remaining,
                approvedAdditionalCount = approvedCount,
                latestApprovedPaymentId = latestApproved?.PaymentId,
                latestReceiptUrl = latestApproved == null
                    ? null
                    : Url.Action(
                        nameof(Receipt),
                        new
                        {
                            bookingId = booking.BookingId,
                            paymentId = latestApproved.PaymentId
                        }),
                message,
                payUrl = Url.Action(nameof(Pay), new { id = booking.BookingId }),
                paymentsUrl = Url.Action(nameof(Payments), new { id = booking.BookingId })
            });
        }

        [HttpGet]
        public async Task<IActionResult> Pay(int id)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            var modelResult = await BuildPaymentPageModelAsync(
                id,
                customerId);

            if (modelResult.Model == null)
            {
                TempData["AdditionalPaymentError"] = modelResult.Error;
                return RedirectToAction("MyBookings", "Bookings");
            }

            return View(modelResult.Model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            AdditionalPaymentSubmitModel request)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            if (request.BookingId <= 0 || request.Amount <= 0m)
            {
                TempData["AdditionalPaymentError"] =
                    "أدخل مبلغًا صحيحًا أكبر من صفر.";
                return RedirectToAction(nameof(Pay), new { id = request.BookingId });
            }

            if (request.ProofFile == null || request.ProofFile.Length <= 0)
            {
                TempData["AdditionalPaymentError"] =
                    "ارفع صورة إثبات الدفع.";
                return RedirectToAction(nameof(Pay), new { id = request.BookingId });
            }

            if (request.ProofFile.Length > MaximumProofSize)
            {
                TempData["AdditionalPaymentError"] =
                    "حجم إثبات الدفع يجب ألا يتجاوز 5 ميجابايت.";
                return RedirectToAction(nameof(Pay), new { id = request.BookingId });
            }

            var extension = Path.GetExtension(request.ProofFile.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                TempData["AdditionalPaymentError"] =
                    "نوع الملف غير مدعوم. استخدم JPG أو PNG أو WEBP.";
                return RedirectToAction(nameof(Pay), new { id = request.BookingId });
            }

            await using var transaction = await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    HttpContext.RequestAborted);

            string? savedPhysicalPath = null;

            try
            {
                var booking = await BuildBookingQuery()
                    .FirstOrDefaultAsync(item =>
                        item.BookingId == request.BookingId &&
                        item.CustomerId == customerId,
                        HttpContext.RequestAborted);

                if (booking == null || booking.FinancialRecord == null)
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["AdditionalPaymentError"] =
                        "الحجز أو سجله المالي غير موجود.";
                    return RedirectToAction("MyBookings", "Bookings");
                }

                if (!booking.Place.OwnerId.HasValue ||
                    booking.Place.OwnerId.Value <= 0)
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["AdditionalPaymentError"] =
                        "لا يوجد مالك صالح مرتبط بهذه المنشأة.";
                    return RedirectToAction(nameof(Pay), new { id = request.BookingId });
                }

                var financial = booking.FinancialRecord;

                if (!BookingPaymentLedger.TryRead(
                        financial.PaymentTransactionsJson,
                        out var transactions))
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["AdditionalPaymentError"] =
                        "تعذر قراءة سجل الدفعات. لم يتم حفظ أي شيء.";
                    return RedirectToAction(nameof(Pay), new { id = request.BookingId });
                }

                var hasPending = transactions.Any(
                    BookingPaymentLedger.IsPending);

                var remaining = Math.Round(
                    Math.Max(0m, financial.AmountToPayAtProperty),
                    2);

                var eligibilityMessage = GetEligibilityMessage(
                    booking,
                    DateTime.Now,
                    remaining,
                    hasPending);

                if (!string.IsNullOrWhiteSpace(eligibilityMessage))
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["AdditionalPaymentError"] = eligibilityMessage;
                    return RedirectToAction(nameof(Pay), new { id = request.BookingId });
                }

                var amount = Math.Round(request.Amount, 2);
                if (amount <= 0m || amount > remaining)
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["AdditionalPaymentError"] =
                        $"المبلغ يجب أن يكون أكبر من صفر ولا يتجاوز المتبقي {remaining:N0} ريال.";
                    return RedirectToAction(nameof(Pay), new { id = request.BookingId });
                }

                var ownerId = booking.Place.OwnerId.Value;

                var wallet = await _context.OwnerWallets
                    .Include(item => item.Wallet)
                    .FirstOrDefaultAsync(item =>
                        item.OwnerWalletId == request.OwnerWalletId &&
                        item.OwnerId == ownerId &&
                        item.IsActive == true &&
                        item.Wallet.IsActive == true,
                        HttpContext.RequestAborted);

                if (wallet == null)
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["AdditionalPaymentError"] =
                        "حساب الدفع المختار غير متاح حاليًا.";
                    return RedirectToAction(nameof(Pay), new { id = request.BookingId });
                }

                var paymentReference = request.PaymentReference?.Trim();
                if (!string.IsNullOrWhiteSpace(paymentReference) &&
                    paymentReference.Length > 150)
                {
                    await transaction.RollbackAsync(HttpContext.RequestAborted);
                    TempData["AdditionalPaymentError"] =
                        "رقم/مرجع الحوالة طويل جدًا.";
                    return RedirectToAction(nameof(Pay), new { id = request.BookingId });
                }

                var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                    ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                    : _environment.WebRootPath;

                var relativeDirectory = Path.Combine(
                    "uploads",
                    "payments",
                    "additional",
                    booking.BookingId.ToString(CultureInfo.InvariantCulture));

                var physicalDirectory = Path.Combine(
                    webRoot,
                    relativeDirectory);

                Directory.CreateDirectory(physicalDirectory);

                var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                savedPhysicalPath = Path.Combine(physicalDirectory, fileName);

                await using (var stream = new FileStream(
                                 savedPhysicalPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None))
                {
                    await request.ProofFile.CopyToAsync(
                        stream,
                        HttpContext.RequestAborted);
                }

                var relativeWebPath = "/" + Path.Combine(
                        relativeDirectory,
                        fileName)
                    .Replace('\\', '/');

                var now = DateTime.Now;
                var payment = new BookingPaymentTransaction
                {
                    PaymentId = Guid.NewGuid().ToString("N"),
                    PaymentType = "Additional",
                    Amount = amount,
                    Status = "Pending",
                    OwnerWalletId = wallet.OwnerWalletId,
                    WalletId = wallet.WalletId,
                    WalletName = wallet.Wallet?.WalletName?.Trim(),
                    AccountNumber = wallet.AccountNumber?.Trim(),
                    BeneficiaryName = wallet.BeneficiaryName?.Trim(),
                    ProofPath = relativeWebPath,
                    OriginalFileName = Path.GetFileName(request.ProofFile.FileName),
                    PaymentReference = paymentReference,
                    SubmittedAt = now
                };

                transactions.Add(payment);
                financial.PaymentTransactionsJson =
                    BookingPaymentLedger.Write(transactions);
                financial.UpdatedAt = now;

                _notificationService.Queue(
                    new CreateNotificationDto
                    {
                        UserId = ownerId,
                        NotificationTitle = "دفعة إضافية قيد المراجعة",
                        NotificationText =
                            $"رفع العميل إثبات دفعة إضافية بقيمة {amount:N0} ريال " +
                            $"للحجز رقم {booking.BookingId}. يرجى مراجعتها.",
                        NotificationType = "AdditionalPaymentSubmitted",
                        TargetUrl =
                            $"/Owner/Reservation/ManageReservations?bookingId={booking.BookingId}",
                        ReferenceType = "Booking",
                        ReferenceId = booking.BookingId
                    });

                await _context.SaveChangesAsync(HttpContext.RequestAborted);
                await transaction.CommitAsync(HttpContext.RequestAborted);

                TempData["AdditionalPaymentSuccess"] =
                    "تم رفع الدفعة الإضافية وإثباتها بنجاح، وهي الآن قيد مراجعة المنشأة.";

                return RedirectToAction(
                    nameof(Payments),
                    new { id = booking.BookingId });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(HttpContext.RequestAborted);
                DeleteFileQuietly(savedPhysicalPath);
                TempData["AdditionalPaymentError"] =
                    "تم تحديث الحجز من جلسة أخرى. حدّث الصفحة ثم حاول مرة أخرى.";
                return RedirectToAction(nameof(Pay), new { id = request.BookingId });
            }
            catch
            {
                await transaction.RollbackAsync(HttpContext.RequestAborted);
                DeleteFileQuietly(savedPhysicalPath);
                TempData["AdditionalPaymentError"] =
                    "حدث خطأ أثناء حفظ الدفعة. لم تتغير المبالغ المالية.";
                return RedirectToAction(nameof(Pay), new { id = request.BookingId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Payments(int id)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            var booking = await BuildBookingQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.BookingId == id &&
                    item.CustomerId == customerId,
                    HttpContext.RequestAborted);

            if (booking == null || booking.FinancialRecord == null)
            {
                return NotFound();
            }

            if (!BookingPaymentLedger.TryRead(
                    booking.FinancialRecord.PaymentTransactionsJson,
                    out var transactions))
            {
                return StatusCode(
                    500,
                    "تعذر قراءة سجل الدفعات لهذا الحجز.");
            }

            var model = new BookingPaymentsPageViewModel
            {
                BookingId = booking.BookingId,
                PlaceName =
                    booking.Place.PlaceNameAndDesc?.Trim() ?? "المنشأة",
                TotalPrice = booking.TotalPrice,
                AmountPaidOnline =
                    Math.Max(0m, booking.FinancialRecord.AmountPaidOnline),
                RemainingAmount =
                    Math.Max(0m, booking.FinancialRecord.AmountToPayAtProperty),
                DepositReceipt = BuildDepositReceipt(booking),
                AdditionalPayments = transactions
                    .OrderByDescending(item => item.SubmittedAt)
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(
            int bookingId,
            string paymentId)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            var booking = await BuildBookingQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.BookingId == bookingId &&
                    item.CustomerId == customerId,
                    HttpContext.RequestAborted);

            if (booking == null || booking.FinancialRecord == null)
            {
                return NotFound();
            }

            BookingPaymentReceiptViewModel? receipt;

            if (string.Equals(
                    paymentId,
                    "deposit",
                    StringComparison.OrdinalIgnoreCase))
            {
                receipt = BuildDepositReceipt(booking);
            }
            else
            {
                if (!BookingPaymentLedger.TryRead(
                        booking.FinancialRecord.PaymentTransactionsJson,
                        out var transactions))
                {
                    return StatusCode(500);
                }

                var payment = transactions.FirstOrDefault(item =>
                    string.Equals(
                        item.PaymentId,
                        paymentId,
                        StringComparison.OrdinalIgnoreCase) &&
                    BookingPaymentLedger.IsApproved(item));

                receipt = payment == null
                    ? null
                    : new BookingPaymentReceiptViewModel
                    {
                        BookingId = booking.BookingId,
                        PlaceName =
                            booking.Place.PlaceNameAndDesc?.Trim() ?? "المنشأة",
                        ReceiptNumber =
                            payment.ReceiptNumber ??
                            $"AP-{booking.BookingId}-{payment.PaymentId[..Math.Min(8, payment.PaymentId.Length)]}",
                        PaymentTypeLabel = "دفعة إضافية",
                        Amount = payment.Amount,
                        PaidAt =
                            payment.ReceiptIssuedAt ??
                            payment.ReviewedAt ??
                            payment.SubmittedAt,
                        WalletName = payment.WalletName,
                        PaymentReference = payment.PaymentReference,
                        CumulativePaidOnline =
                            payment.CumulativePaidOnlineAfterApproval ?? 0m,
                        RemainingAmount =
                            payment.RemainingAfterApproval ?? 0m,
                        PaymentId = payment.PaymentId
                    };
            }

            if (receipt == null)
            {
                return NotFound();
            }

            return View(receipt);
        }

        private async Task<(AdditionalPaymentPageViewModel? Model, string Error)>
            BuildPaymentPageModelAsync(
                int bookingId,
                int customerId)
        {
            var booking = await BuildBookingQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.BookingId == bookingId &&
                    item.CustomerId == customerId,
                    HttpContext.RequestAborted);

            if (booking == null || booking.FinancialRecord == null)
            {
                return (null, "الحجز أو سجله المالي غير موجود.");
            }

            if (!booking.Place.OwnerId.HasValue ||
                booking.Place.OwnerId.Value <= 0)
            {
                return (null, "لا يوجد مالك صالح مرتبط بهذه المنشأة.");
            }

            if (!BookingPaymentLedger.TryRead(
                    booking.FinancialRecord.PaymentTransactionsJson,
                    out var transactions))
            {
                return (
                    null,
                    "تعذر قراءة سجل الدفعات. راجع الإدارة قبل إضافة دفعة جديدة.");
            }

            var remaining = Math.Round(
                Math.Max(0m, booking.FinancialRecord.AmountToPayAtProperty),
                2);

            var message = GetEligibilityMessage(
                booking,
                DateTime.Now,
                remaining,
                transactions.Any(BookingPaymentLedger.IsPending));

            if (!string.IsNullOrWhiteSpace(message))
            {
                return (null, message);
            }

            var ownerId = booking.Place.OwnerId.Value;

            var wallets = await _context.OwnerWallets
                .AsNoTracking()
                .Include(item => item.Wallet)
                .Where(item =>
                    item.OwnerId == ownerId &&
                    item.IsActive == true &&
                    item.Wallet.IsActive == true)
                .OrderBy(item => item.Wallet.WalletName)
                .Select(item => new AdditionalPaymentWalletViewModel
                {
                    OwnerWalletId = item.OwnerWalletId,
                    WalletId = item.WalletId,
                    WalletName = item.Wallet.WalletName,
                    AccountNumber = item.AccountNumber,
                    BeneficiaryName = item.BeneficiaryName,
                    LogoPath = item.Wallet.LogoPath
                })
                .ToListAsync(HttpContext.RequestAborted);

            if (wallets.Count == 0)
            {
                return (
                    null,
                    "لا توجد وسيلة دفع إلكترونية مفعلة لهذه المنشأة حاليًا.");
            }

            return (
                new AdditionalPaymentPageViewModel
                {
                    BookingId = booking.BookingId,
                    PlaceName =
                        booking.Place.PlaceNameAndDesc?.Trim() ?? "المنشأة",
                    TotalPrice = booking.TotalPrice,
                    AmountPaidOnline =
                        Math.Max(0m, booking.FinancialRecord.AmountPaidOnline),
                    RemainingAmount = remaining,
                    OperationalStartAt = GetOperationalStartAt(booking),
                    Wallets = wallets
                },
                string.Empty);
        }

        private static BookingPaymentReceiptViewModel? BuildDepositReceipt(
            Booking booking)
        {
            var financial = booking.FinancialRecord;
            if (financial == null || financial.AmountPaidOnline <= 0m)
            {
                return null;
            }

            var initialAmount = IsState(financial.OnlinePaymentType, "Full")
                ? Math.Min(booking.TotalPrice, financial.AmountPaidOnline)
                : Math.Min(
                    Math.Max(0m, financial.DepositAmount),
                    Math.Max(0m, financial.AmountPaidOnline));

            if (initialAmount <= 0m)
            {
                return null;
            }

            var isFull = IsState(financial.OnlinePaymentType, "Full");

            return new BookingPaymentReceiptViewModel
            {
                BookingId = booking.BookingId,
                PlaceName =
                    booking.Place.PlaceNameAndDesc?.Trim() ?? "المنشأة",
                ReceiptNumber = $"DP-{booking.BookingId}",
                PaymentTypeLabel =
                    isFull ? "سداد إلكتروني كامل" : "دفع العربون",
                Amount = Math.Round(initialAmount, 2),
                PaidAt = booking.PaymentApprovedAt ?? financial.UpdatedAt,
                PaymentReference = financial.TransferReferenceNumber,
                CumulativePaidOnline = Math.Round(initialAmount, 2),
                RemainingAmount = Math.Round(
                    Math.Max(0m, booking.TotalPrice - initialAmount),
                    2),
                PaymentId = "deposit"
            };
        }

        private static void DeleteFileQuietly(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch
            {
                // تنظيف الملف المرفوع لا يجب أن يخفي الخطأ الأصلي.
            }
        }
    }
}
