using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner,Admin")]
    [AuthorizeActiveOwner]
    public partial class ReservationController : Controller
    {
        private static readonly string[] PendingQueueStates =
        {
            "Pending",
            "AwaitingPayment",
            "PaymentReview"
        };

        private static readonly string[] BlockingBookingStates =
        {
            "Pending",
            "AwaitingPayment",
            "PaymentReview",
            "Confirmed"
        };

        private static readonly string[] HistoryBookingStates =
        {
            "Completed",
            "Rejected",
            "Cancelled",
            "NoShow",
            "Expired"
        };

        private static readonly DateTime SqlMinimumDate =
            new DateTime(1753, 1, 1);

        private readonly AppDbContext _context;
        private readonly ICommissionFinanceService
            _commissionFinanceService;

        private readonly INotificationService
            _notificationService;

        public ReservationController(
            AppDbContext context,
            ICommissionFinanceService commissionFinanceService,
            INotificationService notificationService)
        {
            _context = context;
            _commissionFinanceService = commissionFinanceService;
            _notificationService = notificationService;
        }

        // =====================================================================
        // أدوات عامة
        // =====================================================================

        private bool TryGetCurrentOwnerId(out int ownerId)
        {
            ownerId = 0;

            var ownerIdText =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(ownerIdText, out ownerId) &&
                   ownerId > 0;
        }

        private static bool IsState(string? value, string expected)
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

        private JsonResult UnauthorizedOwnerJson()
        {
            return Json(new
            {
                success = false,
                message = "تعذر التعرف على حساب المالك. أعد تسجيل الدخول."
            });
        }

        private static string FirstNonEmpty(
            string fallback,
            params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return fallback;
        }

        private static string NormalizeReason(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string? ValidateReason(string reason)
        {
            if (reason.Length < 5)
            {
                return "اكتب سببًا واضحًا لا يقل عن 5 أحرف.";
            }

            if (reason.Length > 500)
            {
                return "السبب طويل جدًا. الحد الأقصى 500 حرف.";
            }

            return null;
        }

        private async Task<int?> GetActiveOwnerPlaceIdAsync(int ownerId)
        {
            var ownerPlaces =
                await _context.Places
                    .AsNoTracking()
                    .Where(place => place.OwnerId == ownerId)
                    .OrderByDescending(place =>
                        place.Status == "Approved" ||
                        place.Status == "Active")
                    .ThenBy(place => place.PlaceNameAndDesc)
                    .Select(place => place.PlaceId)
                    .ToListAsync();

            if (ownerPlaces.Count == 0)
            {
                return null;
            }

            var cookieName = $"ownerActivePlace_{ownerId}";

            if (int.TryParse(
                    Request.Cookies[cookieName],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var cookiePlaceId) &&
                ownerPlaces.Contains(cookiePlaceId))
            {
                return cookiePlaceId;
            }

            var fallbackPlaceId = ownerPlaces[0];

            Response.Cookies.Append(
                cookieName,
                fallbackPlaceId.ToString(CultureInfo.InvariantCulture),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            return fallbackPlaceId;
        }

        private static string BuildRoomName(Room? room)
        {
            if (room == null)
            {
                return string.Empty;
            }

            var parts =
                new[]
                {
                    room.RoomType?.Trim(),
                    room.RoomNumber?.Trim()
                }
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value));

            return string.Join(" — ", parts);
        }

        private static string BuildPackageName(PlacePackage? package)
        {
            if (package == null)
            {
                return string.Empty;
            }

            var parts =
                new[]
                {
                    package.PackageName?.Trim(),
                    package.TimeFrame?.Trim()
                }
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value));

            return string.Join(" — ", parts);
        }

        private void AddStructuredNotification(
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

        private static void MarkStatusChanged(
            Booking booking,
            DateTime changedAt)
        {
            booking.LastStatusChangedAt = changedAt;
        }

        private static void CloseBooking(
            Booking booking,
            DateTime closedAt,
            int? closedByUserId,
            string closedByType)
        {
            booking.ClosedAt = closedAt;
            booking.ClosedByUserId = closedByUserId;
            booking.ClosedByType = closedByType;
            booking.LastStatusChangedAt = closedAt;
            booking.TimerExpiredAt = null;
        }

        private static bool IsActiveRoomAssignment(
            BookingRoomUnit assignment)
        {
            return IsState(assignment.AssignmentStatus, "Held") ||
                   IsState(assignment.AssignmentStatus, "Confirmed");
        }

        private static void ConfirmRoomAssignments(
            Booking booking)
        {
            foreach (var assignment in booking.BookingRoomUnits
                         .Where(item =>
                             IsState(item.AssignmentStatus, "Held")))
            {
                assignment.AssignmentStatus = "Confirmed";
                assignment.ReleasedAt = null;
            }
        }

        private static void ReleaseRoomAssignments(
            Booking booking,
            DateTime releasedAt)
        {
            foreach (var assignment in booking.BookingRoomUnits
                         .Where(IsActiveRoomAssignment))
            {
                assignment.AssignmentStatus = "Released";
                assignment.ReleasedAt = releasedAt;
            }
        }

        private static void CompleteRoomAssignments(
            Booking booking,
            DateTime completedAt)
        {
            foreach (var assignment in booking.BookingRoomUnits
                         .Where(IsActiveRoomAssignment))
            {
                assignment.AssignmentStatus = "Completed";
                assignment.ReleasedAt = completedAt;
            }
        }

        private IQueryable<Booking> BuildOwnerReservationsQuery(
            int ownerId,
            int activePlaceId)
        {
            return _context.Bookings
                .AsNoTracking()
                .AsSplitQuery()
                .Where(booking =>
                    booking.Place.OwnerId == ownerId &&
                    booking.PlaceId == activePlaceId)
                .Include(booking => booking.Customer)
                .Include(booking => booking.Place)
                    .ThenInclude(place => place.PlacePolicy)
                .Include(booking => booking.Room)
                .Include(booking => booking.BookingRoomUnits)
                    .ThenInclude(assignment => assignment.RoomUnit)
                .Include(booking => booking.Package)
                .Include(booking => booking.FinancialRecord)
                .Include(booking => booking.RefundRequests)
                .Include(booking => booking.BookingAdditionalServices)
                    .ThenInclude(selectedService => selectedService.Service);
        }

        private static OwnerReservationItemDto MapBookingToDto(
            Booking booking)
        {
            var financialRecord =
                booking.FinancialRecord;

            var latestRefund =
                booking.RefundRequests?
                    .OrderByDescending(item => item.RequestedAt)
                    .ThenByDescending(item => item.RefundRequestId)
                    .FirstOrDefault();

            var now = DateTime.Now;
            var operationalStartAt = GetOperationalStartAt(booking);
            var operationalEndAt = GetOperationalEndAt(booking);
            var noShowAvailableAt = GetNoShowAvailableAt(booking);

            var legacyPaymentProofReason =
                IsState(booking.BookingState, "AwaitingPayment") &&
                string.IsNullOrWhiteSpace(
                    booking.PaymentProofRejectionReason)
                    ? booking.RejectionReason?.Trim()
                    : null;

            return new OwnerReservationItemDto
            {
                BookingId = booking.BookingId,
                PlaceId = booking.PlaceId,
                PlaceName = FirstNonEmpty(
                    "منشأة غير معروفة",
                    booking.Place?.PlaceNameAndDesc),
                CustomerId = booking.CustomerId,
                CustomerName = FirstNonEmpty(
                    "ضيف",
                    booking.Customer?.FullName,
                    booking.Customer?.Username,
                    booking.GuestName),
                CustomerPhone = FirstNonEmpty(
                    "بدون هاتف",
                    booking.Customer?.PhoneNumber,
                    booking.Customer?.ContactInfo,
                    booking.GuestPhone),
                RoomId = booking.RoomId,
                RoomName = booking.Room == null
                    ? null
                    : BuildRoomName(booking.Room),
                AssignedRoomUnits = booking.BookingRoomUnits
                    .OrderBy(assignment =>
                        assignment.RoomUnit.FloorNumber)
                    .ThenBy(assignment =>
                        assignment.RoomUnit.RoomNumber)
                    .Select(assignment =>
                        new OwnerAssignedRoomUnitDto
                        {
                            RoomUnitId = assignment.RoomUnitId,
                            RoomNumber =
                                assignment.RoomUnit.RoomNumber?.Trim() ??
                                string.Empty,
                            FloorNumber =
                                assignment.RoomUnit.FloorNumber,
                            FloorLabel =
                                assignment.RoomUnit.FloorLabel?.Trim(),
                            AssignmentStatus =
                                assignment.AssignmentStatus?.Trim() ??
                                "Held"
                        })
                    .ToList(),
                PackageId = booking.PackageId,
                PackageName = booking.Package == null
                    ? null
                    : BuildPackageName(booking.Package),
                StartDate = booking.StartDate,
                EndDate = booking.EndDate,
                CreatedAt = booking.CreatedAt,
                TimerExpiredAt = booking.TimerExpiredAt,
                TotalPrice = booking.TotalPrice,
                PromotionId = booking.PromotionId,
                OriginalTotalPrice = booking.OriginalTotalPrice,
                DiscountAmount = Math.Max(0m, booking.DiscountAmount),
                PromotionTitleSnapshot =
                    booking.PromotionTitleSnapshot?.Trim(),
                PromotionCodeSnapshot =
                    booking.PromotionCodeSnapshot?.Trim(),
                DiscountTypeSnapshot =
                    booking.DiscountTypeSnapshot?.Trim(),
                DiscountValueSnapshot =
                    booking.DiscountValueSnapshot,
                BookingState = booking.BookingState?.Trim() ?? "Pending",
                PaymentStatus = booking.PaymentStatus?.Trim() ?? "Unpaid",
                PaymentMethod = booking.PaymentMethod?.Trim(),
                Quantity = Math.Max(1, booking.Quantity),
                AdultsCount = Math.Max(0, booking.AdultsCount),
                ChildrenCount = Math.Max(0, booking.ChildrenCount),
                BookingType = booking.BookingType?.Trim(),
                SeatingSetup = booking.SeatingSetup?.Trim(),
                RejectionReason = booking.RejectionReason?.Trim(),

                LastStatusChangedAt = booking.LastStatusChangedAt,
                AcceptedAt = booking.AcceptedAt,
                PaymentApprovedAt = booking.PaymentApprovedAt,
                CompletedAt = booking.CompletedAt,
                NoShowAt = booking.NoShowAt,
                ClosedAt = booking.ClosedAt,
                ClosedByUserId = booking.ClosedByUserId,
                ClosedByType = booking.ClosedByType?.Trim(),
                CancellationReason = booking.CancellationReason?.Trim(),
                OwnerCancellationReason =
                    booking.OwnerCancellationReason?.Trim(),
                PaymentProofRejectionReason =
                    booking.PaymentProofRejectionReason?.Trim() ??
                    legacyPaymentProofReason,
                PaymentProofRejectedAt =
                    booking.PaymentProofRejectedAt,
                PaymentProofReviewCount =
                    Math.Max(0, booking.PaymentProofReviewCount),

                OperationalStartAt = operationalStartAt,
                OperationalEndAt = operationalEndAt,
                NoShowAvailableAt = noShowAvailableAt,
                NoShowGraceMinutes = 60,
                CanAccept =
                    IsState(booking.BookingState, "Pending") &&
                    (
                        booking.PackageId.HasValue
                            ? now < operationalEndAt
                            : now < operationalStartAt
                    ),
                CanApprovePayment =
                    IsState(booking.BookingState, "PaymentReview") &&
                    (
                        booking.PackageId.HasValue
                            ? now < operationalEndAt
                            : now < operationalStartAt
                    ) &&
                    !string.IsNullOrWhiteSpace(
                        booking.PaymentNotificationImage),
                CanRejectPermanently =
                    IsState(booking.BookingState, "PaymentReview") ||
                    IsState(booking.BookingState, "AwaitingPayment"),
                CanOwnerCancel =
                    IsState(booking.BookingState, "Confirmed") &&
                    (financialRecord?.AmountCollectedAtProperty ?? 0m) <= 0m,
                CanCompleteNow =
                    IsState(booking.BookingState, "Confirmed") &&
                    now >= operationalEndAt,
                CanMarkNoShowNow =
                    IsState(booking.BookingState, "Confirmed") &&
                    now >= noShowAvailableAt &&
                    now < operationalEndAt &&
                    (financialRecord?.AmountCollectedAtProperty ?? 0m) <= 0m,
                ActionAvailabilityText =
                    BuildActionAvailabilityText(booking, now),

                DepositAmount = financialRecord?.DepositAmount ?? 0m,
                AmountPaidOnline =
                    financialRecord?.AmountPaidOnline ?? 0m,
                AmountToPayAtProperty =
                    financialRecord?.AmountToPayAtProperty ??
                    booking.TotalPrice,
                AmountCollectedAtProperty =
                    financialRecord?.AmountCollectedAtProperty ?? 0m,
                IsPaymentConfirmed =
                    IsPaymentApproved(booking),
                CommissionStatus =
                    financialRecord?.CommissionStatus?.Trim() ??
                    string.Empty,
                PaymentNotificationImage =
                    booking.PaymentNotificationImage?.Trim(),
                TransferReferenceNumber =
                    financialRecord?.TransferReferenceNumber?.Trim(),

                RefundStatus =
                    booking.RefundStatus?.Trim() ?? "NotApplicable",
                RefundRequestId = latestRefund?.RefundRequestId,
                RefundRequestStatus = latestRefund?.Status?.Trim(),
                RefundDepositPaid = latestRefund?.DepositPaid ?? 0m,
                RefundCancellationFee =
                    latestRefund?.CancellationFee ?? 0m,
                RefundAmount = latestRefund?.RefundAmount ?? 0m,
                RefundOwnerShare = latestRefund?.OwnerShare ?? 0m,
                RefundReason = latestRefund?.Reason?.Trim(),
                RefundRequestedAt = latestRefund?.RequestedAt,
                RefundProcessedAt = latestRefund?.ProcessedAt,
                RefundNotes = latestRefund?.Notes?.Trim(),

                Services = booking.BookingAdditionalServices
                    .OrderBy(service =>
                        service.Service?.ServiceName)
                    .Select(service =>
                        new OwnerReservationServiceDto
                        {
                            ServiceId = service.ServiceId,
                            ServiceName = FirstNonEmpty(
                                "خدمة إضافية",
                                service.Service?.ServiceName),
                            Quantity = Math.Max(1, service.Quantity),
                            UnitPrice = service.UnitPrice
                        })
                    .ToList()
            };
        }

        // =====================================================================
        // المواعيد التشغيلية
        // =====================================================================

        private static bool TryParseClock(
            string? value,
            out TimeSpan time)
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

            var matches = Regex.Matches(
                value,
                @"(?<!\d)([01]?\d|2[0-3]):([0-5]\d)(?!\d)");

            foreach (Match match in matches)
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
                return booking.StartDate.Date
                    .Add(booking.Package.StartTime.Value);
            }

            if (booking.StartDate.TimeOfDay != TimeSpan.Zero)
            {
                return booking.StartDate;
            }

            var packageTimes =
                ExtractTimeValues(booking.Package?.TimeFrame);

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

        private static DateTime GetOperationalEndAt(Booking booking)
        {
            var operationalStart = GetOperationalStartAt(booking);

            if (booking.RoomId.HasValue)
            {
                if (booking.EndDate.HasValue &&
                    booking.EndDate.Value.TimeOfDay != TimeSpan.Zero)
                {
                    return booking.EndDate.Value;
                }

                var checkoutDate =
                    booking.EndDate?.Date ??
                    booking.StartDate.Date.AddDays(1);

                if (TryParseClock(
                        booking.Place?.PlacePolicy?.CheckOutTime,
                        out var checkOutTime))
                {
                    return checkoutDate.Add(checkOutTime);
                }

                return checkoutDate;
            }

            if (booking.Package?.EndTime.HasValue == true)
            {
                var endAt = booking.StartDate.Date
                    .Add(booking.Package.EndTime.Value);

                if (endAt <= operationalStart)
                {
                    endAt = endAt.AddDays(1);
                }

                return endAt;
            }

            var packageTimes =
                ExtractTimeValues(booking.Package?.TimeFrame);

            if (packageTimes.Count >= 2)
            {
                var endAt = booking.StartDate.Date
                    .Add(packageTimes.Last());

                if (endAt <= operationalStart)
                {
                    endAt = endAt.AddDays(1);
                }

                return endAt;
            }

            if (booking.EndDate.HasValue)
            {
                if (booking.EndDate.Value.TimeOfDay != TimeSpan.Zero)
                {
                    return booking.EndDate.Value;
                }

                if (booking.EndDate.Value.Date > booking.StartDate.Date)
                {
                    return booking.EndDate.Value.Date;
                }
            }

            if (TryParseClock(
                    booking.Place?.PlacePolicy?.CheckOutTime,
                    out var policyEndTime))
            {
                var endAt = booking.StartDate.Date.Add(policyEndTime);

                if (endAt <= operationalStart)
                {
                    endAt = endAt.AddDays(1);
                }

                return endAt;
            }

            return booking.StartDate.Date.AddDays(1);
        }

        private static (
            DateTime Start,
            DateTime End)
            GetPackageOperationalInterval(
                DateTime bookingDate,
                PlacePackage package)
        {
            var start = bookingDate.Date.Add(
                package.StartTime ?? TimeSpan.Zero);

            var end = bookingDate.Date.Add(
                package.EndTime ??
                package.StartTime ??
                TimeSpan.Zero);

            if (end <= start)
            {
                end = end.AddDays(1);
            }

            return (start, end);
        }

        private static bool PackageIntervalsConflict(
            DateTime requestedDate,
            PlacePackage requestedPackage,
            DateTime existingDate,
            PlacePackage existingPackage,
            int bufferHours)
        {
            /*
             * الاعتماد يكون على الوقت الفعلي للباقة دائمًا، وليس على
             * مجرد تطابق التاريخ أو اسم الباقة/وضع التشغيل.
             * للسجلات القديمة ناقصة الوقت نمنع تكرار نفس الباقة فقط
             * بدل إغلاق اليوم كاملًا أمام بقية الباقات.
             */
            if (!requestedPackage.StartTime.HasValue ||
                !requestedPackage.EndTime.HasValue ||
                !existingPackage.StartTime.HasValue ||
                !existingPackage.EndTime.HasValue)
            {
                return
                    requestedPackage.PackageId ==
                        existingPackage.PackageId &&
                    requestedDate.Date ==
                        existingDate.Date;
            }

            var requested =
                GetPackageOperationalInterval(
                    requestedDate,
                    requestedPackage);

            var existing =
                GetPackageOperationalInterval(
                    existingDate,
                    existingPackage);

            var buffer =
                TimeSpan.FromHours(
                    Math.Clamp(
                        bufferHours,
                        0,
                        24));

            return
                existing.Start <
                    requested.End.Add(buffer) &&
                existing.End.Add(buffer) >
                    requested.Start;
        }

        private static string FormatOperationalDuration(
            TimeSpan duration)
        {
            var totalMinutes =
                Math.Max(
                    0,
                    (int)Math.Floor(
                        duration.TotalMinutes));

            if (totalMinutes <= 0)
            {
                return "أقل من دقيقة";
            }

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0 && minutes > 0)
            {
                return $"{hours} ساعة و{minutes} دقيقة";
            }

            if (hours > 0)
            {
                return $"{hours} ساعة";
            }

            return $"{minutes} دقيقة";
        }

        private static DateTime GetNoShowAvailableAt(Booking booking)
        {
            // قاعدة تشغيل موحدة: عدم الحضور يصبح متاحًا بعد ساعة واحدة
            // من بداية الفترة الفعلية للحجز.
            return GetOperationalStartAt(booking)
                .AddHours(1);
        }

        private static string BuildActionAvailabilityText(
            Booking booking,
            DateTime now)
        {
            var startAt = GetOperationalStartAt(booking);
            var endAt = GetOperationalEndAt(booking);
            var noShowAt = GetNoShowAvailableAt(booking);
            var collectedAtProperty =
                Math.Max(
                    0m,
                    booking.FinancialRecord?.AmountCollectedAtProperty ?? 0m);

            if (now < startAt)
            {
                return
                    $"التحصيل يتاح عند بداية الفترة في {startAt:yyyy-MM-dd HH:mm}. " +
                    $"عدم الحضور يتاح بعد ساعة في {noShowAt:yyyy-MM-dd HH:mm}. " +
                    $"الإنهاء يتاح بعد نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}.";
            }

            if (now < endAt)
            {
                if (collectedAtProperty > 0m)
                {
                    return
                        "تم تسجيل تحصيل عند المنشأة؛ لذلك أصبح عدم الحضور وإلغاء الحجز من جهة المالك غير متاحين. " +
                        $"يمكن استكمال تحصيل أي مبلغ متبقٍ حتى نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}، ثم إنهاء الحجز.";
                }

                if (now < noShowAt)
                {
                    return
                        "التحصيل متاح الآن. " +
                        $"عدم الحضور يتاح بعد ساعة من البداية في {noShowAt:yyyy-MM-dd HH:mm}. " +
                        $"الإنهاء يتاح في {endAt:yyyy-MM-dd HH:mm}.";
                }

                return
                    "التحصيل متاح الآن، وعدم الحضور متاح عند التأكد من غياب العميل. " +
                    $"الإنهاء يتاح بعد نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}.";
            }

            return
                "انتهت فترة الحجز؛ لم يعد عدم الحضور متاحًا، والحجز جاهز للإنهاء.";
        }

        private static bool HasOpenRefundRequest(Booking booking)
        {
            return booking.RefundRequests.Any(item =>
                !IsState(item.Status, "Cancelled") &&
                !IsState(item.Status, "Rejected") &&
                !IsState(item.Status, "Completed"));
        }

        private RefundRequest? CreateRefundRequest(
            Booking booking,
            decimal paidAmount,
            decimal refundAmount,
            decimal retainedAmount,
            decimal platformCommission,
            decimal commissionReleased,
            string reason,
            DateTime now)
        {
            paidAmount =
                Math.Round(
                    Math.Max(
                        0m,
                        paidAmount),
                    2);

            refundAmount =
                Math.Round(
                    Math.Min(
                        paidAmount,
                        Math.Max(
                            0m,
                            refundAmount)),
                    2);

            retainedAmount =
                Math.Round(
                    Math.Min(
                        paidAmount,
                        Math.Max(
                            0m,
                            retainedAmount)),
                    2);

            platformCommission =
                Math.Round(
                    Math.Min(
                        retainedAmount,
                        Math.Max(
                            0m,
                            platformCommission)),
                    2);

            commissionReleased =
                Math.Round(
                    Math.Max(
                        0m,
                        commissionReleased),
                    2);

            if (paidAmount <= 0m ||
                HasOpenRefundRequest(booking))
            {
                return null;
            }

            var requiresTransfer =
                refundAmount > 0m;

            var processingDays =
                Math.Max(
                    1,
                    booking.Place.PlacePolicy?
                        .RefundProcessingDays ??
                    3);

            var request =
                new RefundRequest
                {
                    BookingId =
                        booking.BookingId,

                    CustomerId =
                        booking.CustomerId,

                    PlaceId =
                        booking.PlaceId,

                    /*
                     * الحقل اسمه التاريخي DepositPaid، لكنه يمثل هنا
                     * كل ما دفعه العميل إلكترونيًا قبل التسوية.
                     */
                    DepositPaid =
                        paidAmount,

                    CancellationFee =
                        retainedAmount,

                    RefundAmount =
                        refundAmount,

                    OwnerShare =
                        Math.Round(
                            Math.Max(
                                0m,
                                retainedAmount -
                                platformCommission),
                            2),

                    PlatformCommission =
                        platformCommission,

                    CommissionReleased =
                        commissionReleased,

                    Status =
                        requiresTransfer
                            ? FinancialRefundStatuses.Pending
                            : FinancialRefundStatuses.Completed,

                    Reason =
                        reason,

                    ReferenceNumber =
                        null,

                    ProofImage =
                        null,

                    RequestedAt =
                        now,

                    ProcessedAt =
                        requiresTransfer
                            ? null
                            : now,

                    Notes =
                        refundAmount >= paidAmount
                            ? $"استرداد كامل. مدة المعالجة حسب سياسة المنشأة: {processingDays} أيام."
                            : refundAmount > 0m
                                ? $"استرداد جزئي بقيمة {refundAmount:N0} ريال، مع احتفاظ المنشأة بـ {retainedAmount:N0} ريال وفق السياسة. مدة المعالجة: {processingDays} أيام."
                                : $"لا يوجد مبلغ مطلوب تحويله للعميل بعد تطبيق السياسة. احتفظت المنشأة بـ {retainedAmount:N0} ريال."
                };

            _context.RefundRequests.Add(
                request);

            return request;
        }

        private async Task ExpireOverdueAwaitingPaymentsAsync(
            int ownerId,
            int activePlaceId)
        {
            var now = DateTime.Now;

            var overdueBookings =
                await _context.Bookings
                    .Include(booking => booking.BookingRoomUnits)
                    .Where(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == activePlaceId &&
                        booking.BookingState == "AwaitingPayment" &&
                        booking.TimerExpiredAt.HasValue &&
                        booking.TimerExpiredAt.Value <= now)
                    .ToListAsync();

            if (overdueBookings.Count == 0)
            {
                return;
            }

            foreach (var booking in overdueBookings)
            {
                booking.BookingState = "Expired";
                booking.PaymentStatus = "Expired";
                booking.RefundStatus = "NotApplicable";
                booking.RejectionReason = null;
                ReleaseRoomAssignments(booking, now);
                CloseBooking(
                    booking,
                    now,
                    null,
                    "System");

                AddStructuredNotification(
                    booking.CustomerId,
                    "انتهت مهلة الدفع",
                    $"انتهت مهلة دفع العربون للحجز رقم " +
                    $"{booking.BookingId}، وتم إغلاق الطلب تلقائيًا.",
                    NotificationTypes.BookingExpired,
                    $"/Places/BookingDetails/{booking.BookingId}",
                    NotificationReferenceTypes.Booking,
                    booking.BookingId);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string?> GetAvailabilityConflictMessageAsync(
            Booking booking)
        {
            var now = DateTime.Now;
            var currentCreatedAt = booking.CreatedAt ?? DateTime.Now;

            if (booking.RoomId.HasValue)
            {
                var room =
                    await _context.Rooms
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item =>
                            item.RoomId == booking.RoomId.Value &&
                            item.PlaceId == booking.PlaceId);

                if (room == null)
                {
                    return "الغرفة المرتبطة بهذا الحجز لم تعد موجودة.";
                }

                if (room.IsAvailable != true ||
                    room.Quantity <= 0)
                {
                    return "الغرفة المرتبطة بهذا الحجز غير متاحة حاليًا.";
                }

                var startDate = booking.StartDate;
                var endDate = booking.EndDate ??
                              booking.StartDate.AddDays(1);

                if (endDate <= startDate)
                {
                    return "تاريخ الخروج في الحجز غير صحيح.";
                }

                var reservedQuantity =
                    await _context.Bookings
                        .AsNoTracking()
                        .Where(other =>
                            other.BookingId != booking.BookingId &&
                            other.RoomId == booking.RoomId &&
                            BlockingBookingStates.Contains(
                                other.BookingState ?? string.Empty) &&
                            (
                                other.BookingState != "AwaitingPayment" ||
                                !other.TimerExpiredAt.HasValue ||
                                other.TimerExpiredAt.Value > now
                            ) &&
                            (
                                other.BookingState != "Pending" ||
                                (other.CreatedAt ?? SqlMinimumDate) <=
                                currentCreatedAt
                            ) &&
                            other.StartDate < endDate &&
                            (other.EndDate ??
                             other.StartDate.AddDays(1)) > startDate)
                        .SumAsync(other =>
                            (int?)other.Quantity) ?? 0;

                var requestedQuantity = Math.Max(1, booking.Quantity);
                var remainingQuantity =
                    room.Quantity - reservedQuantity;

                if (remainingQuantity < requestedQuantity)
                {
                    return remainingQuantity <= 0
                        ? "لم يعد هناك عدد متاح من هذه الغرفة خلال الفترة المطلوبة."
                        : $"المتبقي خلال الفترة المطلوبة هو " +
                          $"{remainingQuantity} فقط، بينما الطلب يحتاج " +
                          $"{requestedQuantity}.";
                }

                return null;
            }

            if (booking.PackageId.HasValue)
            {
                var selectedPackage =
                    booking.Package ??
                    await _context.PlacePackages
                        .AsNoTracking()
                        .FirstOrDefaultAsync(package =>
                            package.PackageId == booking.PackageId.Value &&
                            package.PlaceId == booking.PlaceId &&
                            package.IsActive == true);

                if (selectedPackage == null ||
                    selectedPackage.IsActive != true)
                {
                    return
                        "الباقة المرتبطة بهذا الحجز لم تعد موجودة أو غير مفعلة.";
                }

                var requestedDay =
                    booking.StartDate.Date;

                /*
                 * قد تعبر الباقة منتصف الليل، وقد يمتد BufferTimeHours
                 * إلى اليوم السابق/التالي؛ لذلك نبحث يومًا على الجانبين
                 * ثم نقرر بالتداخل الزمني الفعلي، لا بمجرد نفس التاريخ.
                 */
                var searchFrom =
                    requestedDay.AddDays(-1);

                var searchTo =
                    requestedDay.AddDays(1);

                var otherBookings =
                    await _context.Bookings
                        .AsNoTracking()
                        .Include(other => other.Package)
                        .Where(other =>
                            other.BookingId != booking.BookingId &&
                            other.PlaceId == booking.PlaceId &&
                            other.PackageId.HasValue &&
                            BlockingBookingStates.Contains(
                                other.BookingState ?? string.Empty) &&
                            (
                                other.BookingState != "AwaitingPayment" ||
                                !other.TimerExpiredAt.HasValue ||
                                other.TimerExpiredAt.Value > now
                            ) &&
                            (
                                other.BookingState != "Pending" ||
                                (other.CreatedAt ?? SqlMinimumDate) <=
                                currentCreatedAt
                            ) &&
                            other.StartDate.Date >= searchFrom &&
                            other.StartDate.Date <= searchTo)
                        .ToListAsync();

                var bufferHours =
                    Math.Clamp(
                        booking.Place?.PlacePolicy?
                            .BufferTimeHours ?? 0,
                        0,
                        24);

                foreach (var other in otherBookings)
                {
                    if (other.Package == null)
                    {
                        if (other.PackageId ==
                                selectedPackage.PackageId &&
                            other.StartDate.Date ==
                                requestedDay)
                        {
                            return
                                "هذه الباقة مرتبطة بحجز آخر في نفس التاريخ.";
                        }

                        continue;
                    }

                    if (PackageIntervalsConflict(
                            requestedDay,
                            selectedPackage,
                            other.StartDate,
                            other.Package,
                            bufferHours))
                    {
                        return
                            "توجد باقة أخرى متداخلة زمنيًا مع هذا الحجز أو مع وقت التجهيز بين الحجوزات.";
                    }
                }

                return null;
            }

            return "الحجز لا يحتوي على غرفة أو باقة صالحة.";
        }

        private static JsonResult ConcurrencyError()
        {
            return new JsonResult(new
            {
                success = false,
                message =
                    "تم تعديل الحجز من جلسة أخرى. حدّث الصفحة ثم أعد المحاولة."
            });
        }

        // =====================================================================
        // الصفحة الرئيسية والتبويبات
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> ManageReservations()
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Challenge();
            }

            var activePlaceId =
                await GetActiveOwnerPlaceIdAsync(ownerId);

            if (!activePlaceId.HasValue)
            {
                ViewBag.PendingCount = 0;
                ViewBag.ManualPendingCount = 0;
                ViewBag.AwaitingPaymentCount = 0;
                ViewBag.PaymentReviewCount = 0;
                ViewBag.ActiveCount = 0;
                ViewBag.HistoryCount = 0;
                ViewBag.PromotionBookingCount = 0;

                return View();
            }

            var selectedPlaceId = activePlaceId.Value;
            ViewBag.ActivePlaceId = selectedPlaceId;

            await ExpireOverdueAwaitingPaymentsAsync(
                ownerId,
                selectedPlaceId);

            ViewBag.PendingCount =
                await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == selectedPlaceId &&
                        PendingQueueStates.Contains(
                            booking.BookingState ?? string.Empty));

            ViewBag.ManualPendingCount =
                await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == selectedPlaceId &&
                        booking.BookingState == "Pending");

            ViewBag.AwaitingPaymentCount =
                await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == selectedPlaceId &&
                        booking.BookingState == "AwaitingPayment");

            ViewBag.PaymentReviewCount =
                await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == selectedPlaceId &&
                        booking.BookingState == "PaymentReview");

            ViewBag.ActiveCount =
                await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == selectedPlaceId &&
                        booking.BookingState == "Confirmed");

            ViewBag.HistoryCount =
                await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == selectedPlaceId &&
                        HistoryBookingStates.Contains(
                            booking.BookingState ?? string.Empty));

            ViewBag.PromotionBookingCount =
                await _context.Bookings
                    .AsNoTracking()
                    .CountAsync(booking =>
                        booking.Place.OwnerId == ownerId &&
                        booking.PlaceId == selectedPlaceId &&
                        booking.DiscountAmount > 0m);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> PendingRequests()
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized(
                    "تعذر التعرف على حساب المالك.");
            }

            var activePlaceId =
                await GetActiveOwnerPlaceIdAsync(ownerId);

            if (!activePlaceId.HasValue)
            {
                return PartialView(
                    "_PendingRequests",
                    Array.Empty<OwnerReservationItemDto>());
            }

            await ExpireOverdueAwaitingPaymentsAsync(
                ownerId,
                activePlaceId.Value);

            var bookings =
                await BuildOwnerReservationsQuery(
                        ownerId,
                        activePlaceId.Value)
                    .Where(booking =>
                        PendingQueueStates.Contains(
                            booking.BookingState ?? string.Empty))
                    .OrderBy(booking =>
                        booking.BookingState == "PaymentReview"
                            ? 0
                            : booking.BookingState == "Pending"
                                ? 1
                                : 2)
                    .ThenByDescending(booking =>
                        booking.LastStatusChangedAt ??
                        booking.CreatedAt)
                    .ToListAsync();

            return PartialView(
                "_PendingRequests",
                bookings.Select(MapBookingToDto).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> ActiveReservations()
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized(
                    "تعذر التعرف على حساب المالك.");
            }

            var activePlaceId =
                await GetActiveOwnerPlaceIdAsync(ownerId);

            if (!activePlaceId.HasValue)
            {
                return PartialView(
                    "_ActiveReservations",
                    Array.Empty<OwnerReservationItemDto>());
            }

            var bookings =
                await BuildOwnerReservationsQuery(
                        ownerId,
                        activePlaceId.Value)
                    .Where(booking =>
                        booking.BookingState == "Confirmed")
                    .OrderBy(booking => booking.StartDate)
                    .ThenBy(booking => booking.BookingId)
                    .ToListAsync();

            return PartialView(
                "_ActiveReservations",
                bookings.Select(MapBookingToDto).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> ReservationHistory()
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized(
                    "تعذر التعرف على حساب المالك.");
            }

            var activePlaceId =
                await GetActiveOwnerPlaceIdAsync(ownerId);

            if (!activePlaceId.HasValue)
            {
                return PartialView(
                    "_ReservationHistory",
                    Array.Empty<OwnerReservationItemDto>());
            }

            await ExpireOverdueAwaitingPaymentsAsync(
                ownerId,
                activePlaceId.Value);

            var bookings =
                await BuildOwnerReservationsQuery(
                        ownerId,
                        activePlaceId.Value)
                    .Where(booking =>
                        HistoryBookingStates.Contains(
                            booking.BookingState ?? string.Empty))
                    .OrderByDescending(booking =>
                        booking.LastStatusChangedAt ??
                        booking.ClosedAt ??
                        booking.CreatedAt)
                    .ThenByDescending(booking => booking.StartDate)
                    .ToListAsync();

            return PartialView(
                "_ReservationHistory",
                bookings.Select(MapBookingToDto).ToList());
        }

        // =====================================================================
        // التقويم والتوفر
        // تم نقله من PlaceController.Calendar إلى ReservationController
        // =====================================================================

        private async Task<bool> CanManageCalendarPlaceAsync(
            int placeId)
        {
            if (placeId <= 0 ||
                !TryGetCurrentOwnerId(out var ownerId))
            {
                return false;
            }

            return await _context.Places
                .AsNoTracking()
                .AnyAsync(place =>
                    place.PlaceId == placeId &&
                    place.OwnerId == ownerId);
        }

        [HttpGet]
        public async Task<IActionResult> CalendarManager()
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized(
                    "تعذر التعرف على حساب المالك.");
            }

            var calendarPlaces =
                await _context.Places
                    .Include(place => place.Category)
                    .AsNoTracking()
                    .Where(place =>
                        place.OwnerId == ownerId &&
                        place.Status != "WaitOwner" &&
                        place.Status != "RejectedByOwner" &&
                        place.Category.BookingSystemId > 0)
                    .OrderByDescending(place => place.PlaceId)
                    .ToListAsync();

            ViewBag.CalendarPlaces =
                calendarPlaces;

            return PartialView(
                "_CalendarManager");
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(
            int placeId)
        {
            if (placeId <= 0)
            {
                return Json(
                    Array.Empty<object>());
            }

            if (!await CanManageCalendarPlaceAsync(
                    placeId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message = "غير مصرح لك."
                    });
            }

            var rawBookings =
                await _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.PlaceId == placeId &&
                        booking.BookingState != "Cancelled" &&
                        booking.BookingState != "Rejected")
                    .Select(booking =>
                        new
                        {
                            booking.BookingId,
                            booking.StartDate,
                            booking.EndDate
                        })
                    .ToListAsync();

            var bookings =
                rawBookings
                    .Select(booking =>
                        new
                        {
                            id =
                                "B_" +
                                booking.BookingId,

                            title =
                                "محجوز",

                            start =
                                booking.StartDate
                                    .ToString(
                                        "yyyy-MM-dd"),

                            end =
                                booking.EndDate.HasValue
                                    ? booking.EndDate.Value
                                        .AddDays(1)
                                        .ToString(
                                            "yyyy-MM-dd")
                                    : booking.StartDate
                                        .AddDays(1)
                                        .ToString(
                                            "yyyy-MM-dd"),

                            classNames =
                                new[]
                                {
                                    "event-online"
                                },

                            allDay =
                                true
                        })
                    .Cast<object>()
                    .ToList();

            var rawExceptions =
                await _context.PlaceCalendarExceptions
                    .AsNoTracking()
                    .Where(exception =>
                        exception.PlaceId ==
                        placeId)
                    .Select(exception =>
                        new
                        {
                            exception.ExceptionId,
                            exception.ExceptionType,
                            exception.Reason,
                            exception.CustomPrice,
                            exception.ExceptionDate
                        })
                    .ToListAsync();

            var exceptions =
                rawExceptions
                    .Select(exception =>
                        new
                        {
                            id =
                                "E_" +
                                exception.ExceptionId,

                            title =
                                exception.ExceptionType ==
                                "Closed"
                                    ? "مغلق: " +
                                      (
                                          exception.Reason ??
                                          "بدون سبب"
                                      )
                                    : "تسعير: " +
                                      exception.CustomPrice,

                            start =
                                exception.ExceptionDate
                                    .ToString(
                                        "yyyy-MM-dd"),

                            classNames =
                                exception.ExceptionType ==
                                "Closed"
                                    ? new[]
                                    {
                                        "event-offline"
                                    }
                                    : new[]
                                    {
                                        "event-custom-price"
                                    },

                            allDay =
                                true
                        })
                    .Cast<object>()
                    .ToList();

            return Json(
                bookings
                    .Concat(exceptions)
                    .ToList());
        }

        [HttpPost]
        public async Task<IActionResult> AddCalendarException(
            [FromBody] PlaceCalendarException dto)
        {
            if (dto == null ||
                dto.PlaceId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "يرجى تحديد المنشأة أولاً."
                });
            }

            if (!await CanManageCalendarPlaceAsync(
                    dto.PlaceId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message = "غير مصرح لك."
                    });
            }

            try
            {
                var isBooked =
                    await _context.Bookings
                        .AsNoTracking()
                        .AnyAsync(booking =>
                            booking.PlaceId ==
                                dto.PlaceId &&
                            booking.StartDate.Date <=
                                dto.ExceptionDate.Date &&
                            (
                                booking.EndDate == null ||
                                booking.EndDate.Value.Date >=
                                    dto.ExceptionDate.Date
                            ) &&
                            booking.BookingState !=
                                "Cancelled" &&
                            booking.BookingState !=
                                "Rejected");

                if (isBooked)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "لا يمكن تعديل أو إغلاق هذا اليوم لوجود حجز مؤكد فيه."
                    });
                }

                var existingException =
                    await _context
                        .PlaceCalendarExceptions
                        .FirstOrDefaultAsync(
                            exception =>
                                exception.PlaceId ==
                                    dto.PlaceId &&
                                exception.ExceptionDate ==
                                    dto.ExceptionDate);

                if (existingException != null)
                {
                    existingException
                        .ExceptionType =
                        dto.ExceptionType;

                    existingException
                        .Reason =
                        dto.Reason;

                    existingException
                        .CustomPrice =
                        dto.CustomPrice;
                }
                else
                {
                    dto.CreatedAt =
                        DateTime.Now;

                    _context
                        .PlaceCalendarExceptions
                        .Add(dto);
                }

                await _context
                    .SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message =
                        "تم تحديث حالة اليوم في التقويم بنجاح."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "فشل تحديث التقويم: " +
                        ex.Message
                });
            }
        }

        // =====================================================================
        // قبول ورفض الطلبات
        // =====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptBooking(int id)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            if (id <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم الحجز غير صحيح."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                            .ThenInclude(place => place.PlacePolicy)
                        .Include(item => item.Package)
                        .Include(item => item.FinancialRecord)
                        .Include(item => item.BookingRoomUnits)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == id &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                if (!IsState(booking.BookingState, "Pending"))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "لا يمكن قبول هذا الحجز لأن حالته تغيرت مسبقًا."
                    });
                }

                var now = DateTime.Now;
                var actionDeadline =
                    booking.PackageId.HasValue
                        ? GetOperationalEndAt(booking)
                        : GetOperationalStartAt(booking);

                if (now >= actionDeadline)
                {
                    booking.BookingState = "Expired";
                    booking.PaymentStatus = "Expired";
                    booking.RefundStatus = "NotApplicable";
                    ReleaseRoomAssignments(booking, now);
                    CloseBooking(booking, now, null, "System");

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = false,
                        message = booking.PackageId.HasValue
                            ? "انتهى وقت الباقة بالكامل، وتم نقل الطلب إلى الأرشيف كطلب منتهي."
                            : "انتهى موعد بداية الحجز، وتم نقله إلى الأرشيف كطلب منتهي."
                    });
                }

                var conflictMessage =
                    await GetAvailabilityConflictMessageAsync(booking);

                if (!string.IsNullOrWhiteSpace(conflictMessage))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = conflictMessage
                    });
                }

                var financialRecord = booking.FinancialRecord;
                var depositAmount = Math.Max(
                    0m,
                    financialRecord?.DepositAmount ?? 0m);
                var requiresDeposit = depositAmount > 0m;

                booking.AcceptedAt = now;
                MarkStatusChanged(booking, now);
                booking.RejectionReason = null;
                booking.CancellationReason = null;
                booking.OwnerCancellationReason = null;
                booking.RefundStatus = "NotApplicable";
                booking.ClosedAt = null;
                booking.ClosedByUserId = null;
                booking.ClosedByType = null;

                if (requiresDeposit)
                {
                    var paymentTimeout =
                        booking.Place.PlacePolicy?.PaymentTimeout ?? 12;

                    if (paymentTimeout <= 0)
                    {
                        paymentTimeout = 12;
                    }

                    booking.BookingState = "AwaitingPayment";
                    booking.PaymentStatus = "Unpaid";

                    var paymentDeadline =
                        now.AddHours(paymentTimeout);

                    if (booking.PackageId.HasValue)
                    {
                        var packageEndAt =
                            GetOperationalEndAt(booking);

                        if (paymentDeadline > packageEndAt)
                        {
                            paymentDeadline = packageEndAt;
                        }
                    }

                    booking.TimerExpiredAt =
                        paymentDeadline;

                    if (financialRecord != null)
                    {
                        financialRecord.AmountPaidOnline = 0m;
                        financialRecord.AmountToPayAtProperty =
                            Math.Round(
                                Math.Max(
                                    0m,
                                    booking.TotalPrice - depositAmount),
                                2);
                        financialRecord.AmountCollectedAtProperty = 0m;
                        financialRecord.UpdatedAt = now;
                    }

                    AddStructuredNotification(
                        booking.CustomerId,
                        "تم قبول الحجز",
                        $"وافق المالك على طلب الحجز رقم " +
                        $"{booking.BookingId}. يرجى دفع العربون بقيمة " +
                        $"{depositAmount:N0} ريال قبل انتهاء المهلة.",
                        NotificationTypes.PaymentRequired,
                        $"/BookingFlow/Payment/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }
                else
                {
                    booking.BookingState = "Confirmed";
                    booking.PaymentStatus = "Unpaid";
                    booking.TimerExpiredAt = null;
                    ConfirmRoomAssignments(booking);

                    if (financialRecord != null)
                    {
                        financialRecord.AmountPaidOnline = 0m;
                        financialRecord.AmountToPayAtProperty =
                            booking.TotalPrice;
                        financialRecord.AmountCollectedAtProperty = 0m;
                        financialRecord.UpdatedAt = now;
                    }

                    var commissionResult =
                        await _commissionFinanceService
                            .HoldBookingCommissionAsync(
                                booking.BookingId,
                                ownerId,
                                HttpContext.RequestAborted);

                    if (!commissionResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message = commissionResult.Message
                        });
                    }

                    AddStructuredNotification(
                        booking.CustomerId,
                        "تم تأكيد الحجز",
                        $"تمت الموافقة على الحجز رقم " +
                        $"{booking.BookingId} وتأكيده بنجاح.",
                        NotificationTypes.BookingConfirmed,
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = requiresDeposit
                        ? "تم قبول الطلب، وهو الآن بانتظار دفع العربون من العميل."
                        : "تم قبول الطلب وتأكيد الحجز بنجاح.",
                    nextState = booking.BookingState,
                    requiresPayment = requiresDeposit,
                    timerExpiredAt = booking.TimerExpiredAt
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ أثناء قبول الحجز. لم يتم تغيير أي بيانات."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(
            int id,
            string reason)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            reason = NormalizeReason(reason);
            var reasonError = ValidateReason(reason);

            if (id <= 0 || reasonError != null)
            {
                return Json(new
                {
                    success = false,
                    message = id <= 0
                        ? "رقم الحجز غير صحيح."
                        : reasonError
                });
            }

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                        .Include(item => item.BookingRoomUnits)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == id &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                if (!IsState(booking.BookingState, "Pending"))
                {
                    return Json(new
                    {
                        success = false,
                        message =
                            "رفض الطلب من هذا الإجراء متاح للطلبات التي تنتظر الموافقة فقط."
                    });
                }

                var now = DateTime.Now;

                booking.BookingState = "Rejected";
                booking.PaymentStatus = "Rejected";
                booking.RejectionReason = reason;
                booking.RefundStatus = "NotApplicable";
                ReleaseRoomAssignments(booking, now);
                CloseBooking(booking, now, ownerId, "Owner");

                AddStructuredNotification(
                    booking.CustomerId,
                    "تم رفض الحجز",
                    $"تم رفض طلب الحجز رقم " +
                    $"{booking.BookingId}. السبب: {reason}",
                    NotificationTypes.BookingRejected,
                    $"/Places/BookingDetails/{booking.BookingId}",
                    NotificationReferenceTypes.Booking,
                    booking.BookingId);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message =
                        "تم رفض طلب الحجز وإبلاغ العميل بالسبب.",
                    nextState = "Rejected"
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyError();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBookingPermanently(
            int id,
            string reason)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            reason = NormalizeReason(reason);
            var reasonError = ValidateReason(reason);

            if (id <= 0 || reasonError != null)
            {
                return Json(new
                {
                    success = false,
                    message = id <= 0
                        ? "رقم الحجز غير صحيح."
                        : reasonError
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                        .Include(item => item.FinancialRecord)
                        .Include(item => item.BookingRoomUnits)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == id &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                var allowed =
                    IsState(booking.BookingState, "PaymentReview") ||
                    IsState(booking.BookingState, "AwaitingPayment");

                if (!allowed)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "الرفض النهائي متاح أثناء انتظار الدفع أو مراجعة إثبات الدفع فقط."
                    });
                }

                var financialRecord = booking.FinancialRecord;

                if (IsPaymentApproved(booking) ||
                    (financialRecord?.AmountPaidOnline ?? 0m) > 0m)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "تم اعتماد مبلغ لهذا الحجز. استخدم إلغاء الحجز من جهة المالك حتى يُنشأ استرداد كامل."
                    });
                }

                var now = DateTime.Now;
                var wasPaymentReview =
                    IsState(booking.BookingState, "PaymentReview");

                booking.BookingState = "Rejected";
                booking.PaymentStatus = "Rejected";
                booking.RejectionReason = reason;
                booking.RefundStatus = "NotApplicable";
                ReleaseRoomAssignments(booking, now);

                if (wasPaymentReview)
                {
                    booking.PaymentProofRejectionReason = reason;
                    booking.PaymentProofRejectedAt = now;
                    booking.PaymentProofReviewCount =
                        Math.Max(0, booking.PaymentProofReviewCount) + 1;
                }

                CloseBooking(booking, now, ownerId, "Owner");

                AddStructuredNotification(
                    booking.CustomerId,
                    "تم رفض الحجز نهائيًا",
                    $"تم رفض الحجز رقم {booking.BookingId} نهائيًا. " +
                    $"السبب: {reason}",
                    NotificationTypes.BookingRejected,
                    $"/Places/BookingDetails/{booking.BookingId}",
                    NotificationReferenceTypes.Booking,
                    booking.BookingId);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message =
                        "تم رفض الحجز نهائيًا وإبلاغ العميل.",
                    nextState = "Rejected"
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ أثناء رفض الحجز نهائيًا."
                });
            }
        }

        // =====================================================================
        // مراجعة إثبات الدفع
        // =====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewPayment(
            ReviewPaymentRequestDto request)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            if (request == null ||
                request.BookingId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "بيانات مراجعة الدفع غير صحيحة."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                            .ThenInclude(place => place.PlacePolicy)
                        .Include(item => item.Package)
                        .Include(item => item.FinancialRecord)
                        .Include(item => item.BookingRoomUnits)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == request.BookingId &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                if (!IsState(booking.BookingState, "PaymentReview"))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "لا يمكن مراجعة الدفع لأن حالة الحجز تغيرت."
                    });
                }

                var financialRecord = booking.FinancialRecord;

                if (financialRecord == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "السجل المالي للحجز غير موجود."
                    });
                }

                var now = DateTime.Now;

                if (request.IsApproved)
                {
                    var paymentApprovalDeadline =
                        booking.PackageId.HasValue
                            ? GetOperationalEndAt(booking)
                            : GetOperationalStartAt(booking);

                    if (now >= paymentApprovalDeadline)
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message = booking.PackageId.HasValue
                                ? "انتهى وقت الباقة بالكامل. لا يمكن اعتماد الدفع الآن؛ عالج الحجز حسب حالة الحضور أو تواصل مع الإدارة عند الحاجة."
                                : "بدأ موعد الحجز بالفعل. لا يمكن اعتماد الدفع الآن؛ ارفض الحجز نهائيًا أو تواصل مع الإدارة لمعالجة الحوالة."
                        });
                    }

                    if (string.IsNullOrWhiteSpace(
                            booking.PaymentNotificationImage))
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message =
                                "لا توجد صورة إشعار حوالة مرتبطة بالحجز."
                        });
                    }

                    var conflictMessage =
                        await GetAvailabilityConflictMessageAsync(booking);

                    if (!string.IsNullOrWhiteSpace(conflictMessage))
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message = conflictMessage
                        });
                    }

                    /*
                     * نوع الدفع اختاره العميل عند رفع إشعار الحوالة:
                     * Deposit = العربون فقط.
                     * Full    = كامل قيمة الحجز.
                     *
                     * الحقول المالية لا تعتبر أي مبلغ محصلًا قبل اعتماد
                     * المالك لإثبات الدفع. عند الاعتماد فقط نسجل القيمة
                     * الصحيحة في AmountPaidOnline.
                     */
                    var isFullOnlinePayment =
                        IsState(
                            financialRecord.OnlinePaymentType,
                            FinancialOnlinePaymentTypes.Full);

                    var expectedPaidOnline =
                        isFullOnlinePayment
                            ? Math.Max(0m, booking.TotalPrice)
                            : Math.Max(
                                0m,
                                financialRecord.DepositAmount);

                    var paidOnline = Math.Round(
                        Math.Min(
                            expectedPaidOnline,
                            Math.Max(0m, booking.TotalPrice)),
                        2);

                    var remainingAmount = Math.Round(
                        Math.Max(0m, booking.TotalPrice - paidOnline),
                        2);

                    var onlinePaymentLabel =
                        isFullOnlinePayment
                            ? "كامل قيمة الحجز"
                            : "العربون";

                    financialRecord.AmountPaidOnline = paidOnline;
                    financialRecord.AmountToPayAtProperty =
                        remainingAmount;
                    financialRecord.AmountCollectedAtProperty = 0m;
                    financialRecord.UpdatedAt = now;

                    booking.BookingState = "Confirmed";
                    booking.PaymentStatus = remainingAmount <= 0m
                        ? "FullyPaid"
                        : "PartiallyPaid";
                    booking.TimerExpiredAt = null;
                    booking.RejectionReason = null;
                    booking.PaymentProofRejectionReason = null;
                    booking.PaymentProofRejectedAt = null;
                    booking.PaymentApprovedAt = now;
                    booking.RefundStatus = "NotApplicable";
                    ConfirmRoomAssignments(booking);
                    MarkStatusChanged(booking, now);

                    var commissionResult =
                        await _commissionFinanceService
                            .HoldBookingCommissionAsync(
                                booking.BookingId,
                                ownerId,
                                HttpContext.RequestAborted);

                    if (!commissionResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message = commissionResult.Message
                        });
                    }

                    AddStructuredNotification(
                        booking.CustomerId,
                        "تم اعتماد الدفع",
                        $"تم اعتماد إشعار دفع {onlinePaymentLabel} للحجز رقم " +
                        $"{booking.BookingId}، وأصبح الحجز مؤكدًا.",
                        NotificationTypes.PaymentApproved,
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = isFullOnlinePayment
                            ? "تم تأكيد استلام كامل قيمة الحجز وتأكيد الحجز بنجاح."
                            : "تم تأكيد استلام العربون وتأكيد الحجز بنجاح.",
                        nextState = "Confirmed"
                    });
                }

                var rejectionReason =
                    NormalizeReason(request.RejectionReason);
                var reasonError = ValidateReason(rejectionReason);

                if (reasonError != null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = reasonError
                    });
                }

                var retryPaymentTimeout =
                    booking.Place.PlacePolicy?.PaymentTimeout ?? 12;

                if (retryPaymentTimeout <= 0)
                {
                    retryPaymentTimeout = 12;
                }

                booking.BookingState = "AwaitingPayment";
                booking.PaymentStatus = "Unpaid";
                booking.PaymentMethod = null;
                booking.PaymentNotificationImage = null;
                booking.TimerExpiredAt =
                    now.AddHours(retryPaymentTimeout);
                booking.PaymentProofRejectionReason =
                    rejectionReason;
                booking.PaymentProofRejectedAt = now;
                booking.PaymentProofReviewCount =
                    Math.Max(0, booking.PaymentProofReviewCount) + 1;
                booking.RejectionReason = null;
                booking.RefundStatus = "NotApplicable";
                MarkStatusChanged(booking, now);

                financialRecord.AmountPaidOnline = 0m;
                financialRecord.TransferReferenceNumber = null;
                financialRecord.AmountToPayAtProperty = Math.Round(
                    Math.Max(
                        0m,
                        booking.TotalPrice -
                        financialRecord.DepositAmount),
                    2);
                financialRecord.AmountCollectedAtProperty = 0m;
                financialRecord.UpdatedAt = now;

                AddStructuredNotification(
                    booking.CustomerId,
                    "تم رفض إثبات الدفع",
                    $"تم رفض إشعار الدفع للحجز رقم " +
                    $"{booking.BookingId}. السبب: {rejectionReason}. " +
                    $"يمكنك رفع إشعار جديد قبل انتهاء المهلة.",
                    NotificationTypes.PaymentRejected,
                    $"/BookingFlow/Payment/{booking.BookingId}",
                    NotificationReferenceTypes.Booking,
                    booking.BookingId);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message =
                        "تم رفض الإشعار وإعادة الحجز إلى انتظار الدفع.",
                    nextState = "AwaitingPayment",
                    timerExpiredAt = booking.TimerExpiredAt,
                    paymentProofReviewCount =
                        booking.PaymentProofReviewCount
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ أثناء مراجعة الدفع. لم يتم تغيير البيانات."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ApprovePayment(int id)
        {
            return ReviewPayment(
                new ReviewPaymentRequestDto
                {
                    BookingId = id,
                    IsApproved = true
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> RejectPayment(
            int id,
            string reason)
        {
            return ReviewPayment(
                new ReviewPaymentRequestDto
                {
                    BookingId = id,
                    IsApproved = false,
                    RejectionReason = reason
                });
        }

        // =====================================================================
        // تحصيل مبلغ أثناء فترة الحجز دون إنهاء الحجز
        // =====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CollectBookingPayment(
            int id,
            decimal collectedAmount)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            if (id <= 0 || collectedAmount <= 0m)
            {
                return Json(new
                {
                    success = false,
                    message = "بيانات التحصيل غير صحيحة."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                            .ThenInclude(place => place.PlacePolicy)
                        .Include(item => item.Package)
                        .Include(item => item.FinancialRecord)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == id &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                if (!IsState(booking.BookingState, "Confirmed"))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "التحصيل متاح للحجوزات المؤكدة النشطة فقط."
                    });
                }

                var now = DateTime.Now;
                var operationalStartAt = GetOperationalStartAt(booking);
                var operationalEndAt = GetOperationalEndAt(booking);

                if (now < operationalStartAt)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            $"يتاح التحصيل عند بداية فترة الحجز في " +
                            $"{operationalStartAt:yyyy-MM-dd HH:mm}."
                    });
                }

                if (now >= operationalEndAt)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "انتهت فترة الحجز. استخدم زر إنهاء الحجز."
                    });
                }

                var financialRecord = booking.FinancialRecord;

                if (financialRecord == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "السجل المالي للحجز غير موجود."
                    });
                }

                var outstandingAmount = Math.Round(
                    Math.Max(0m, financialRecord.AmountToPayAtProperty),
                    2);

                if (outstandingAmount <= 0m)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "تم سداد كامل المبلغ المتبقي مسبقًا."
                    });
                }

                var actualCollected = Math.Round(
                    Math.Max(0m, collectedAmount),
                    2);

                if (actualCollected > outstandingAmount &&
                    actualCollected - outstandingAmount <= 0.01m)
                {
                    actualCollected = outstandingAmount;
                }

                if (actualCollected <= 0m ||
                    actualCollected > outstandingAmount)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = actualCollected <= 0m
                            ? "المبلغ المحصل يجب أن يكون أكبر من صفر."
                            : $"المبلغ المحصل لا يمكن أن يتجاوز المتبقي " +
                              $"{outstandingAmount:N2}."
                    });
                }

                var remainingAfterCollection = Math.Round(
                    Math.Max(0m, outstandingAmount - actualCollected),
                    2);

                financialRecord.AmountCollectedAtProperty =
                    Math.Round(
                        Math.Max(0m, financialRecord.AmountCollectedAtProperty) +
                        actualCollected,
                        2);

                financialRecord.AmountToPayAtProperty =
                    remainingAfterCollection;
                financialRecord.CollectedAt = now;
                financialRecord.UpdatedAt = now;

                var totalPaid = Math.Round(
                    Math.Max(0m, financialRecord.AmountPaidOnline) +
                    Math.Max(0m, financialRecord.AmountCollectedAtProperty),
                    2);

                booking.PaymentStatus =
                    totalPaid >= booking.TotalPrice
                        ? "FullyPaid"
                        : totalPaid > 0m
                            ? "PartiallyPaid"
                            : "Unpaid";

                // التحصيل هنا مالي فقط:
                // لا نغيّر BookingState، ولا CompletedAt/ClosedAt،
                // ولا إسناد الغرف، ولا دورة العمولة.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    bookingId = booking.BookingId,
                    bookingState = booking.BookingState,
                    paymentStatus = booking.PaymentStatus,
                    amountCollectedNow = actualCollected,
                    amountCollectedAtProperty =
                        financialRecord.AmountCollectedAtProperty,
                    remainingAfterCollection,
                    message = remainingAfterCollection <= 0m
                        ? "تم تحصيل كامل المبلغ المتبقي، وسيبقى الحجز نشطًا حتى نهاية الفترة."
                        : "تم تسجيل التحصيل في المالية وتحديث المبلغ المتبقي دون إنهاء الحجز."
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ أثناء تسجيل التحصيل. لم يتم تغيير بيانات الحجز."
                });
            }
        }

        // =====================================================================
        // إنهاء الحجز
        // =====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteBooking(
            int id,
            decimal? collectedAmount = null,
            bool confirmPartialCollection = false)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            if (id <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم الحجز غير صحيح."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                            .ThenInclude(place => place.PlacePolicy)
                        .Include(item => item.Package)
                        .Include(item => item.FinancialRecord)
                        .Include(item => item.BookingRoomUnits)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == id &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                if (!IsState(booking.BookingState, "Confirmed"))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "لا يمكن إنهاء الحجز لأن حالته ليست مؤكدة."
                    });
                }

                var now = DateTime.Now;
                var completionAvailableAt =
                    GetOperationalEndAt(booking);

                if (now < completionAvailableAt)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            $"لا يمكن إنهاء الحجز قبل انتهاء موعده في " +
                            $"{completionAvailableAt:yyyy-MM-dd HH:mm}."
                    });
                }

                var financialRecord = booking.FinancialRecord;
                var outstandingAmount = Math.Round(
                    Math.Max(
                        0m,
                        financialRecord?.AmountToPayAtProperty ??
                        booking.TotalPrice),
                    2);

                var actualCollected = Math.Round(
                    Math.Max(
                        0m,
                        collectedAmount ?? outstandingAmount),
                    2);

                if (actualCollected > outstandingAmount)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            $"المبلغ المحصل لا يمكن أن يتجاوز المتبقي " +
                            $"{outstandingAmount:N0} ريال."
                    });
                }

                var remainingAfterCollection = Math.Round(
                    Math.Max(
                        0m,
                        outstandingAmount - actualCollected),
                    2);

                if (remainingAfterCollection > 0m &&
                    !confirmPartialCollection)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        requiresPartialConfirmation = true,
                        message =
                            $"سيبقى مبلغ {remainingAfterCollection:N0} ريال غير محصل. " +
                            $"أكد الإنهاء مع رصيد متبقٍ أو عدّل المبلغ.",
                        remainingAfterCollection
                    });
                }

                booking.BookingState = "Completed";
                booking.CompletedAt = now;
                booking.RefundStatus = "NotApplicable";
                booking.RejectionReason = null;
                CompleteRoomAssignments(booking, now);
                CloseBooking(booking, now, ownerId, "Owner");

                if (financialRecord != null)
                {
                    financialRecord.AmountPaidOnline = Math.Round(
                        Math.Min(
                            Math.Max(0m, financialRecord.AmountPaidOnline),
                            booking.TotalPrice),
                        2);

                    financialRecord.AmountCollectedAtProperty =
                        Math.Round(
                            Math.Max(
                                0m,
                                financialRecord
                                    .AmountCollectedAtProperty) +
                            actualCollected,
                            2);

                    financialRecord.AmountToPayAtProperty =
                        remainingAfterCollection;
                    financialRecord.CollectedAt =
                        actualCollected > 0m
                            ? now
                            : financialRecord.CollectedAt;
                    financialRecord.UpdatedAt = now;

                    var totalPaid =
                        financialRecord.AmountPaidOnline +
                        financialRecord.AmountCollectedAtProperty;

                    booking.PaymentStatus =
                        totalPaid >= booking.TotalPrice
                            ? "FullyPaid"
                            : totalPaid > 0m
                                ? "PartiallyPaid"
                                : "Unpaid";
                }
                else
                {
                    booking.PaymentStatus =
                        actualCollected >= booking.TotalPrice
                            ? "FullyPaid"
                            : actualCollected > 0m
                                ? "PartiallyPaid"
                                : "Unpaid";
                }

                AddStructuredNotification(
                    booking.CustomerId,
                    "اكتمل الحجز",
                    $"تم إنهاء الحجز رقم {booking.BookingId} " +
                    $"وتسجيله كمكتمل.",
                    "BookingCompleted",
                    $"/Places/BookingDetails/{booking.BookingId}",
                    NotificationReferenceTypes.Booking,
                    booking.BookingId);

                /*
                 * الحجوزات القديمة قد تكون مؤكدة قبل تفعيل الربط الآلي
                 * وتبقى عمولتها Calculated. نحجزها أولًا ثم نخصمها داخل
                 * المعاملة نفسها حتى لا يبقى الحجز مكتملًا دون حركة مالية.
                 */
                if (financialRecord != null &&
                    IsState(
                        financialRecord.CommissionStatus,
                        OwnerBookingCommissionStatuses.Calculated))
                {
                    var holdResult =
                        await _commissionFinanceService
                            .HoldBookingCommissionAsync(
                                booking.BookingId,
                                ownerId,
                                HttpContext.RequestAborted);

                    if (!holdResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message = holdResult.Message
                        });
                    }
                }

                var chargeResult =
                    await _commissionFinanceService
                        .ChargeBookingCommissionAsync(
                            booking.BookingId,
                            ownerId,
                            HttpContext.RequestAborted);

                if (!chargeResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = chargeResult.Message
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = actualCollected > 0m
                        ? $"تم إنهاء الحجز وتسجيل تحصيل " +
                          $"{actualCollected:N0} ريال عند المنشأة."
                        : "تم إنهاء الحجز وتسجيله كمكتمل.",
                    nextState = "Completed",
                    amountCollectedAtProperty = actualCollected,
                    remainingAfterCollection,
                    paymentStatus = booking.PaymentStatus
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ أثناء إنهاء الحجز. لم يتم تغيير البيانات."
                });
            }
        }

        // =====================================================================
        // عدم حضور العميل
        // =====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NoShowBooking(
            int id,
            bool confirmed = true)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            if (id <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم الحجز غير صحيح."
                });
            }

            if (!confirmed)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "يجب تأكيد أن العميل لم يحضر بعد انتهاء مهلة السماح."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                            .ThenInclude(place => place.PlacePolicy)
                        .Include(item => item.Package)
                        .Include(item => item.FinancialRecord)
                        .Include(item => item.RefundRequests)
                        .Include(item => item.BookingRoomUnits)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == id &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                if (!IsState(booking.BookingState, "Confirmed"))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "لا يمكن تسجيل عدم حضور لأن الحجز ليس مؤكدًا."
                    });
                }

                var now = DateTime.Now;
                var noShowAvailableAt =
                    GetNoShowAvailableAt(booking);

                if (now < noShowAvailableAt)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            $"لا يمكن تسجيل عدم حضور قبل مرور ساعة من بداية الفترة، " +
                            $"أي قبل {noShowAvailableAt:yyyy-MM-dd HH:mm}."
                    });
                }

                var operationalEndAt =
                    GetOperationalEndAt(booking);

                if (now >= operationalEndAt)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "انتهت فترة الحجز. لم يعد تسجيل عدم الحضور متاحًا؛ استخدم إنهاء الحجز."
                    });
                }

                if ((booking.FinancialRecord?.AmountCollectedAtProperty ?? 0m) > 0m)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "تم تسجيل تحصيل عند المنشأة لهذا الحجز، لذلك لا يمكن تسجيل العميل كعدم حضور."
                    });
                }

                booking.BookingState = "NoShow";
                booking.NoShowAt = now;
                booking.RejectionReason = null;
                ReleaseRoomAssignments(booking, now);
                CloseBooking(booking, now, ownerId, "Owner");

                var financialRecord =
                    booking.FinancialRecord;
                var paidOnline =
                    Math.Round(
                        Math.Min(
                            Math.Max(
                                0m,
                                financialRecord?
                                    .AmountPaidOnline ??
                                0m),
                            Math.Max(
                                0m,
                                booking.TotalPrice)),
                        2);

                var configuredDeposit =
                    Math.Round(
                        Math.Min(
                            Math.Max(
                                0m,
                                financialRecord?
                                    .DepositAmount ??
                                0m),
                            Math.Max(
                                0m,
                                booking.TotalPrice)),
                        2);

                var requiredDeposit =
                    Math.Round(
                        configuredDeposit > 0m
                            ? Math.Min(
                                paidOnline,
                                configuredDeposit)
                            : paidOnline,
                        2);

                var confiscateDeposit =
                    booking.Place.PlacePolicy?
                        .ConfiscateOnNoShow ??
                    true;

                /*
                 * قاعدة No-Show:
                 * - إذا كانت السياسة تصادر العربون: لا نحتجز من العميل
                 *   أكثر من العربون المطلوب حتى لو كان قد دفع Full.
                 * - أي مبلغ إلكتروني زائد عن العربون يعود للعميل.
                 * - إذا كانت السياسة لا تصادر العربون: يعود كامل المدفوع.
                 */
                var retainedAmount =
                    confiscateDeposit
                        ? requiredDeposit
                        : 0m;

                var refundAmount =
                    Math.Round(
                        Math.Max(
                            0m,
                            paidOnline -
                            retainedAmount),
                        2);

                var originalCommission =
                    Math.Round(
                        Math.Max(
                            0m,
                            financialRecord?
                                .PlatformCommission ??
                            0m),
                        2);

                decimal finalCommission =
                    0m;

                if (financialRecord != null)
                {
                    financialRecord.AmountToPayAtProperty =
                        0m;
                    financialRecord.UpdatedAt =
                        now;
                }

                /*
                 * أولًا نعيد أي عمولة كانت محجوزة/مديونة/مخصومة
                 * على كامل الحجز، ثم نعيد احتساب العمولة فقط على
                 * المبلغ الذي احتفظت به المنشأة فعلًا.
                 */
                if (retainedAmount > 0m)
                {
                    if (financialRecord != null &&
                        (
                            IsState(
                                financialRecord.CommissionStatus,
                                OwnerBookingCommissionStatuses.Held) ||
                            IsState(
                                financialRecord.CommissionStatus,
                                OwnerBookingCommissionStatuses.Debt) ||
                            IsState(
                                financialRecord.CommissionStatus,
                                OwnerBookingCommissionStatuses.Charged)
                        ))
                    {
                        var releaseForAdjustmentResult =
                            await _commissionFinanceService
                                .ReleaseBookingCommissionAsync(
                                    booking.BookingId,
                                    ownerId,
                                    "إرجاع العمولة السابقة لإعادة احتسابها على العربون المصادَر فقط عند عدم الحضور.",
                                    HttpContext.RequestAborted);

                        if (!releaseForAdjustmentResult.Success)
                        {
                            await transaction.RollbackAsync();

                            return Json(
                                new
                                {
                                    success = false,
                                    message =
                                        releaseForAdjustmentResult.Message
                                });
                        }
                    }

                    var recalculateResult =
                        await _commissionFinanceService
                            .RecalculateBookingCommissionAsync(
                                booking.BookingId,
                                retainedAmount,
                                ownerId,
                                "إعادة احتساب عمولة عدم الحضور على المبلغ المحتفظ به فقط.",
                                HttpContext.RequestAborted);

                    if (!recalculateResult.Success)
                    {
                        await transaction.RollbackAsync();

                        return Json(
                            new
                            {
                                success = false,
                                message =
                                    recalculateResult.Message
                            });
                    }

                    finalCommission =
                        Math.Round(
                            Math.Max(
                                0m,
                                financialRecord?
                                    .PlatformCommission ??
                                0m),
                            2);

                    var holdNoShowResult =
                        await _commissionFinanceService
                            .HoldBookingCommissionAsync(
                                booking.BookingId,
                                ownerId,
                                HttpContext.RequestAborted);

                    if (!holdNoShowResult.Success)
                    {
                        await transaction.RollbackAsync();

                        return Json(
                            new
                            {
                                success = false,
                                message =
                                    holdNoShowResult.Message
                            });
                    }

                    var chargeNoShowResult =
                        await _commissionFinanceService
                            .ChargeBookingCommissionAsync(
                                booking.BookingId,
                                ownerId,
                                HttpContext.RequestAborted);

                    if (!chargeNoShowResult.Success)
                    {
                        await transaction.RollbackAsync();

                        return Json(
                            new
                            {
                                success = false,
                                message =
                                    chargeNoShowResult.Message
                            });
                    }
                }
                else if (financialRecord != null)
                {
                    var releaseResult =
                        await _commissionFinanceService
                            .ReleaseBookingCommissionAsync(
                                booking.BookingId,
                                ownerId,
                                confiscateDeposit
                                    ? "تحرير العمولة لعدم وجود مبلغ إلكتروني يمكن مصادرته عند عدم الحضور."
                                    : "إرجاع العمولة لأن سياسة عدم الحضور تقضي بإعادة كامل المدفوع للعميل.",
                                HttpContext.RequestAborted);

                    if (!releaseResult.Success)
                    {
                        await transaction.RollbackAsync();

                        return Json(
                            new
                            {
                                success = false,
                                message =
                                    releaseResult.Message
                            });
                    }
                }

                var commissionReleased =
                    Math.Round(
                        Math.Max(
                            0m,
                            originalCommission -
                            finalCommission),
                        2);

                RefundRequest? refundRequest =
                    null;

                if (paidOnline > 0m)
                {
                    refundRequest =
                        CreateRefundRequest(
                            booking,
                            paidOnline,
                            refundAmount,
                            retainedAmount,
                            finalCommission,
                            commissionReleased,
                            refundAmount > 0m
                                ? (
                                    confiscateDeposit
                                        ? "استرداد المبلغ الزائد عن العربون المصادَر بسبب عدم حضور العميل."
                                        : "استرداد كامل وفق سياسة عدم مصادرة العربون عند عدم الحضور."
                                  )
                                : "تسوية عدم حضور دون مبلغ مسترد بعد مصادرة العربون المطلوب.",
                            now);
                }

                if (refundAmount > 0m &&
                    refundRequest != null)
                {
                    booking.RefundStatus =
                        FinancialRefundStatuses.Pending;
                    booking.PaymentStatus =
                        "RefundPending";
                }
                else
                {
                    booking.RefundStatus =
                        "NotApplicable";
                }

                if (refundRequest != null &&
                    refundAmount > 0m)
                {
                    AddStructuredNotification(
                        booking.CustomerId,
                        "لديك مبلغ مسترد",
                        $"تم تسجيل عدم حضور للحجز رقم {booking.BookingId}. " +
                        $"لديك استرداد بقيمة {refundAmount:N0} ريال يمني. " +
                        "حدد حساب استلام المبلغ لإكمال الاسترداد.",
                        NotificationTypes.RefundRequested,
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }
                else
                {
                    AddStructuredNotification(
                        booking.CustomerId,
                        "تم تسجيل عدم حضور",
                        refundRequest != null
                            ? $"تم تسجيل عدم حضور للحجز رقم {booking.BookingId} " +
                              "وإغلاق التسوية دون مبلغ مسترد."
                            : $"تم تسجيل عدم حضور للحجز رقم {booking.BookingId}.",
                        "BookingNoShow",
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = refundRequest != null &&
                              refundAmount > 0m
                        ? "تم تسجيل عدم الحضور وإنشاء طلب استرداد للمبلغ المستحق."
                        : refundRequest != null
                            ? "تم تسجيل عدم الحضور وإغلاق التسوية المالية وفق السياسة."
                            : "تم تسجيل حالة عدم حضور العميل.",
                    nextState = "NoShow",
                    refundStatus = booking.RefundStatus,
                    refundRequestId = refundRequest?.RefundRequestId
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ أثناء تسجيل عدم الحضور. لم يتم تغيير البيانات."
                });
            }
        }

        // =====================================================================
        // إلغاء الحجز المؤكد من جهة المالك
        // =====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBookingByOwner(
            int id,
            string reason)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return UnauthorizedOwnerJson();
            }

            reason = NormalizeReason(reason);
            var reasonError = ValidateReason(reason);

            if (id <= 0 || reasonError != null)
            {
                return Json(new
                {
                    success = false,
                    message = id <= 0
                        ? "رقم الحجز غير صحيح."
                        : reasonError
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var booking =
                    await _context.Bookings
                        .Include(item => item.Place)
                            .ThenInclude(place => place.PlacePolicy)
                        .Include(item => item.FinancialRecord)
                        .Include(item => item.RefundRequests)
                        .Include(item => item.BookingRoomUnits)
                        .FirstOrDefaultAsync(item =>
                            item.BookingId == id &&
                            item.Place.OwnerId == ownerId);

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "الحجز غير موجود أو لا يتبع منشآتك."
                    });
                }

                if (!IsState(booking.BookingState, "Confirmed"))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "إلغاء المالك متاح للحجوزات المؤكدة فقط."
                    });
                }

                if ((booking.FinancialRecord?.AmountCollectedAtProperty ?? 0m) > 0m)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "لا يمكن إلغاء الحجز من جهة المالك بعد تسجيل تحصيل عند المنشأة. أكمل الحجز حتى نهايته ثم استخدم إنهاء الحجز."
                    });
                }

                var now =
                    DateTime.Now;

                var financialRecord =
                    booking.FinancialRecord;

                var paidOnline =
                    Math.Round(
                        Math.Min(
                            Math.Max(
                                0m,
                                financialRecord?
                                    .AmountPaidOnline ??
                                0m),
                            Math.Max(
                                0m,
                                booking.TotalPrice)),
                        2);

                var originalCommission =
                    Math.Round(
                        Math.Max(
                            0m,
                            financialRecord?
                                .PlatformCommission ??
                            0m),
                        2);

                booking.BookingState =
                    "Cancelled";
                booking.OwnerCancellationReason =
                    reason;
                booking.CancellationReason =
                    null;
                booking.RejectionReason =
                    null;

                ReleaseRoomAssignments(
                    booking,
                    now);

                CloseBooking(
                    booking,
                    now,
                    ownerId,
                    "Owner");

                if (financialRecord != null)
                {
                    financialRecord.AmountToPayAtProperty =
                        0m;
                    financialRecord.UpdatedAt =
                        now;
                }

                /*
                 * إلغاء المنشأة يعني أن العميل لا يتحمل عمولة المنصة.
                 * حتى لو كانت العمولة قد خُصمت سابقًا نرجعها إلى حساب
                 * ضمان المالك بحركة CommissionRelease عكسية.
                 */
                var returnCommission = financialRecord?.ReturnCommissionOnCancellationSnapshot ??
                    await _context.SystemSettings.OrderBy(x => x.Id)
                        .Select(x => (bool?)x.ReturnCommissionOnCancellation).FirstOrDefaultAsync() ?? true;
                if (financialRecord != null) financialRecord.ReturnCommissionOnCancellationSnapshot = returnCommission;
                if (financialRecord != null && returnCommission)
                {
                    var releaseResult =
                        await _commissionFinanceService
                            .ReleaseBookingCommissionAsync(
                                booking.BookingId,
                                ownerId,
                                $"إرجاع عمولة الحجز بسبب إلغاء المنشأة. السبب: {reason}",
                                HttpContext.RequestAborted);

                    if (!releaseResult.Success)
                    {
                        await transaction.RollbackAsync();

                        return Json(
                            new
                            {
                                success = false,
                                message =
                                    releaseResult.Message
                            });
                    }
                }

                if (financialRecord != null && !returnCommission)
                {
                    if (IsState(financialRecord.CommissionStatus, OwnerBookingCommissionStatuses.Calculated))
                    {
                        var held = await _commissionFinanceService.HoldBookingCommissionAsync(booking.BookingId, ownerId, HttpContext.RequestAborted);
                        if (!held.Success) { await transaction.RollbackAsync(); return Json(new { success = false, message = held.Message }); }
                    }
                    var charged = await _commissionFinanceService.ChargeBookingCommissionAsync(booking.BookingId, ownerId, HttpContext.RequestAborted);
                    if (!charged.Success) { await transaction.RollbackAsync(); return Json(new { success = false, message = charged.Message }); }
                }
                var refundRequest =
                    paidOnline > 0m
                        ? CreateRefundRequest(
                            booking,
                            paidOnline,
                            paidOnline,
                            0m,
                            returnCommission ? 0m : financialRecord?.PlatformCommission ?? 0m,
                            returnCommission ? originalCommission : 0m,
                            $"استرداد كامل بسبب إلغاء المالك للحجز. السبب: {reason}",
                            now)
                        : null;

                if (paidOnline > 0m &&
                    refundRequest != null)
                {
                    booking.RefundStatus =
                        FinancialRefundStatuses.Pending;
                    booking.PaymentStatus =
                        "RefundPending";
                }
                else
                {
                    booking.RefundStatus =
                        "NotApplicable";
                    booking.PaymentStatus =
                        "Cancelled";
                }

                if (refundRequest != null &&
                    refundRequest.RefundAmount > 0m)
                {
                    AddStructuredNotification(
                        booking.CustomerId,
                        "لديك استرداد كامل",
                        $"ألغت المنشأة الحجز رقم {booking.BookingId}. " +
                        $"تم إنشاء استرداد بقيمة {refundRequest.RefundAmount:N0} ريال يمني. " +
                        "حدد حساب استلام المبلغ لإكمال الاسترداد.",
                        NotificationTypes.RefundRequested,
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }
                else
                {
                    AddStructuredNotification(
                        booking.CustomerId,
                        "تم إلغاء الحجز",
                        $"ألغت المنشأة الحجز رقم {booking.BookingId}. السبب: {reason}.",
                        NotificationTypes.BookingCancelled,
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = paidOnline > 0m
                        ? "تم إلغاء الحجز وإنشاء طلب استرداد كامل للعميل."
                        : "تم إلغاء الحجز وإبلاغ العميل.",
                    nextState = "Cancelled",
                    refundStatus = booking.RefundStatus,
                    refundAmount = paidOnline,
                    refundRequestId = refundRequest?.RefundRequestId
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ أثناء إلغاء الحجز من جهة المالك."
                });
            }
        }
    }
}
