using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    public partial class ReservationController
    {
        private sealed class CustomerRequestCancellationQuote
        {
            public bool CanCancel { get; init; }
            public string Message { get; init; } = string.Empty;
            public decimal TotalPrice { get; init; }
            public decimal PaidAmount { get; init; }
            public decimal CancellationFee { get; init; }
            public decimal RefundAmount { get; init; }
            public bool IsFreeCancellation { get; init; }
            public int FreeCancellationHours { get; init; }
            public double HoursUntilStart { get; init; }
            public string PenaltyType { get; init; } = string.Empty;
            public decimal PenaltyValue { get; init; }
            public string PolicyLabel { get; init; } = string.Empty;
            public int RefundProcessingDays { get; init; }
            public string RefundTransferFee { get; init; } = string.Empty;
            public string RefundTransferFeeLabel { get; init; } = string.Empty;
            public DateTime OperationalStartAt { get; init; }

            public static CustomerRequestCancellationQuote NotAllowed(
                Booking booking,
                string message)
            {
                return new CustomerRequestCancellationQuote
                {
                    CanCancel = false,
                    Message = message,
                    TotalPrice = Math.Round(Math.Max(0m, booking.TotalPrice), 2),
                    OperationalStartAt = GetOperationalStartAt(booking)
                };
            }
        }

        private sealed class CustomerRequestCommissionResult
        {
            public bool Success { get; init; }
            public string Message { get; init; } = string.Empty;
            public decimal PlatformCommission { get; init; }
            public decimal CommissionReleased { get; init; }
            public decimal OwnerShare { get; init; }

            public static CustomerRequestCommissionResult Failed(string message) =>
                new CustomerRequestCommissionResult
                {
                    Success = false,
                    Message = message
                };

            public static CustomerRequestCommissionResult Succeeded(
                decimal platformCommission,
                decimal commissionReleased,
                decimal ownerShare) =>
                new CustomerRequestCommissionResult
                {
                    Success = true,
                    PlatformCommission = Math.Round(Math.Max(0m, platformCommission), 2),
                    CommissionReleased = Math.Round(Math.Max(0m, commissionReleased), 2),
                    OwnerShare = Math.Round(Math.Max(0m, ownerShare), 2)
                };
        }

        [HttpGet]
        public async Task<IActionResult> CustomerCancellationQuote(int id)
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

            var booking = await BuildCustomerRequestCancellationQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.BookingId == id &&
                    item.Place.OwnerId == ownerId);

            if (booking == null)
            {
                return Json(new
                {
                    success = false,
                    message = "الحجز غير موجود أو لا يتبع منشآتك."
                });
            }

            var quote = BuildCustomerRequestCancellationQuote(
                booking,
                DateTime.Now);

            return Json(new
            {
                success = quote.CanCancel,
                message = quote.Message,
                data = quote
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelByCustomerRequest(
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
                var booking = await BuildCustomerRequestCancellationQuery()
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

                var now = DateTime.Now;
                var quote = BuildCustomerRequestCancellationQuote(
                    booking,
                    now);

                if (!quote.CanCancel)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = quote.Message
                    });
                }

                if (HasOpenRefundRequest(booking))
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "يوجد طلب استرداد مفتوح لهذا الحجز مسبقًا."
                    });
                }

                var stateBeforeCancellation =
                    booking.BookingState?.Trim() ?? string.Empty;

                var commissionResult =
                    await ApplyCustomerRequestCancellationCommissionAsync(
                        booking,
                        stateBeforeCancellation,
                        quote,
                        ownerId);

                if (!commissionResult.Success)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = commissionResult.Message
                    });
                }

                booking.BookingState = "Cancelled";
                booking.TimerExpiredAt = null;
                booking.RejectionReason = null;
                booking.OwnerCancellationReason = null;
                booking.CancellationReason =
                    $"بطلب العميل عبر المالك: {reason}";

                ReleaseRoomAssignments(booking, now);
                CloseBooking(
                    booking,
                    now,
                    ownerId,
                    "CustomerViaOwner");

                if (booking.FinancialRecord != null)
                {
                    booking.FinancialRecord.AmountToPayAtProperty = 0m;
                    booking.FinancialRecord.UpdatedAt = now;
                }

                RefundRequest? refundRequest = null;

                if (quote.PaidAmount > 0m)
                {
                    refundRequest = CreateRefundRequest(
                        booking,
                        quote.PaidAmount,
                        quote.RefundAmount,
                        quote.CancellationFee,
                        commissionResult.PlatformCommission,
                        commissionResult.CommissionReleased,
                        $"إلغاء بطلب العميل عبر المالك. السبب: {reason}",
                        now);

                    if (refundRequest == null)
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message = "تعذر إنشاء تسوية الاسترداد لهذا الحجز. لم يتم تنفيذ الإلغاء."
                        });
                    }

                    booking.RefundStatus = refundRequest.Status;
                    booking.PaymentStatus =
                        quote.RefundAmount > 0m
                            ? "RefundPending"
                            : "Cancelled";
                }
                else
                {
                    booking.RefundStatus = "NotApplicable";
                    booking.PaymentStatus = "Cancelled";
                }

                if (booking.CustomerId.HasValue && booking.CustomerId.Value > 0)
                {
                    if (refundRequest != null && quote.RefundAmount > 0m)
                    {
                        AddStructuredNotification(
                            booking.CustomerId,
                            "تم إلغاء الحجز بطلبك",
                            $"تم تسجيل إلغاء الحجز رقم {booking.BookingId} بطلبك. " +
                            $"المبلغ المستحق للاسترداد {quote.RefundAmount:N0} ريال يمني بعد تطبيق سياسة الإلغاء. " +
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
                            "تم إلغاء الحجز بطلبك",
                            quote.PaidAmount > 0m
                                ? $"تم إلغاء الحجز رقم {booking.BookingId} بطلبك وتطبيق سياسة الإلغاء. لا يوجد مبلغ مطلوب تحويله بعد التسوية."
                                : $"تم إلغاء الحجز رقم {booking.BookingId} بطلبك.",
                            NotificationTypes.BookingCancelled,
                            $"/Places/BookingDetails/{booking.BookingId}",
                            NotificationReferenceTypes.Booking,
                            booking.BookingId);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = quote.PaidAmount <= 0m
                        ? "تم إلغاء الحجز بطلب العميل ولا يوجد مبلغ مدفوع يحتاج إلى استرداد."
                        : quote.RefundAmount > 0m
                            ? "تم إلغاء الحجز بطلب العميل وإنشاء طلب الاسترداد حسب سياسة المنشأة."
                            : "تم إلغاء الحجز بطلب العميل وتطبيق غرامة الإلغاء حسب سياسة المنشأة.",
                    nextState = "Cancelled",
                    paidAmount = quote.PaidAmount,
                    cancellationFee = quote.CancellationFee,
                    refundAmount = quote.RefundAmount,
                    policyLabel = quote.PolicyLabel,
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
                    message = "حدث خطأ أثناء تنفيذ إلغاء العميل. لم يتم تغيير بيانات الحجز."
                });
            }
        }

        private IQueryable<Booking> BuildCustomerRequestCancellationQuery()
        {
            return _context.Bookings
                .Include(item => item.Place)
                    .ThenInclude(place => place.PlacePolicy)
                .Include(item => item.Room)
                .Include(item => item.Package)
                .Include(item => item.FinancialRecord)
                .Include(item => item.BookingRoomUnits)
                .Include(item => item.RefundRequests);
        }

        private static CustomerRequestCancellationQuote
            BuildCustomerRequestCancellationQuote(
                Booking booking,
                DateTime now)
        {
            var state = booking.BookingState?.Trim() ?? string.Empty;

            if (!IsState(state, "Confirmed"))
            {
                return CustomerRequestCancellationQuote.NotAllowed(
                    booking,
                    "إلغاء بطلب العميل من شاشة المالك متاح للحجوزات المؤكدة فقط.");
            }

            if (booking.StartDate.Date < now.Date)
            {
                return CustomerRequestCancellationQuote.NotAllowed(
                    booking,
                    "لا يمكن تطبيق إلغاء العميل على حجز بدأ في يوم سابق.");
            }

            var policy = booking.Place?.PlacePolicy;
            var financial = booking.FinancialRecord;
            var operationalStart = GetOperationalStartAt(booking);

            var paidOnline =
                IsPaymentApproved(booking)
                    ? Math.Max(0m, financial?.AmountPaidOnline ?? 0m)
                    : 0m;

            var collectedAtProperty =
                Math.Max(0m, financial?.AmountCollectedAtProperty ?? 0m);

            var paidAmount = Math.Round(
                Math.Min(
                    Math.Max(0m, booking.TotalPrice),
                    paidOnline + collectedAtProperty),
                2);

            var freeCancellationHours =
                Math.Max(0, policy?.FreeCancellationHours ?? 48);

            var refundProcessingDays =
                Math.Max(1, policy?.RefundProcessingDays ?? 3);

            var refundTransferFee =
                policy?.RefundTransferFee?.Trim() ?? "CustomerPays";

            var hoursUntilStart = Math.Max(
                0d,
                (operationalStart - now).TotalHours);

            var isWithinNormalFreeWindow =
                hoursUntilStart >= freeCancellationHours;

            var isWithinImmediateGrace =
                paidAmount > 0m &&
                BookingMoneyPolicy.IsWithinImmediateCancellationGrace(
                    policy?.ImmediateGracePeriod == true,
                    booking.CreatedAt,
                    financial?.UpdatedAt,
                    now);

            var isFreeCancellation =
                paidAmount <= 0m ||
                isWithinNormalFreeWindow ||
                isWithinImmediateGrace;

            var penaltyType =
                policy?.PenaltyType?.Trim() ?? "LoseDeposit";

            var penaltyValue =
                Math.Max(0m, policy?.PenaltyValue ?? 0m);

            decimal cancellationFee = 0m;
            string policyLabel;

            if (paidAmount <= 0m)
            {
                policyLabel = "إلغاء مباشر بدون استرداد مالي";
            }
            else if (isFreeCancellation)
            {
                policyLabel = isWithinImmediateGrace
                    ? "إلغاء مجاني خلال فترة التراجع"
                    : "إلغاء مجاني ضمن المدة المحددة";
            }
            else
            {
                cancellationFee = CalculateCustomerRequestCancellationFee(
                    booking,
                    paidAmount,
                    penaltyType,
                    penaltyValue);

                policyLabel = GetCustomerRequestPenaltyLabel(
                    penaltyType,
                    penaltyValue);
            }

            cancellationFee = Math.Round(
                Math.Min(
                    paidAmount,
                    Math.Max(0m, cancellationFee)),
                2);

            var refundAmount = Math.Round(
                Math.Max(0m, paidAmount - cancellationFee),
                2);

            var refundTransferFeeLabel =
                string.Equals(
                    refundTransferFee,
                    "FacilityPays",
                    StringComparison.OrdinalIgnoreCase)
                    ? "تتحمل المنشأة رسوم التحويل"
                    : "يتحمل العميل رسوم التحويل";

            return new CustomerRequestCancellationQuote
            {
                CanCancel = true,
                Message = "يمكن تسجيل الإلغاء بطلب العميل. راجع التسوية قبل التأكيد.",
                TotalPrice = Math.Round(Math.Max(0m, booking.TotalPrice), 2),
                PaidAmount = paidAmount,
                CancellationFee = cancellationFee,
                RefundAmount = refundAmount,
                IsFreeCancellation = isFreeCancellation,
                FreeCancellationHours = freeCancellationHours,
                HoursUntilStart = Math.Round(hoursUntilStart, 1),
                PenaltyType = penaltyType,
                PenaltyValue = penaltyValue,
                PolicyLabel = policyLabel,
                RefundProcessingDays = refundProcessingDays,
                RefundTransferFee = refundTransferFee,
                RefundTransferFeeLabel = refundTransferFeeLabel,
                OperationalStartAt = operationalStart
            };
        }

        private static decimal CalculateCustomerRequestCancellationFee(
            Booking booking,
            decimal paidAmount,
            string penaltyType,
            decimal penaltyValue)
        {
            var configuredDeposit =
                Math.Max(0m, booking.FinancialRecord?.DepositAmount ?? 0m);

            var requiredDeposit = Math.Round(
                configuredDeposit > 0m
                    ? Math.Min(configuredDeposit, Math.Max(0m, paidAmount))
                    : Math.Max(0m, paidAmount),
                2);

            if (string.Equals(
                    penaltyType,
                    "DeductPercentage",
                    StringComparison.OrdinalIgnoreCase))
            {
                var percentage = Math.Min(
                    100m,
                    Math.Max(0m, penaltyValue));

                return requiredDeposit * percentage / 100m;
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
                return booking.Room != null
                    ? booking.Room.Price * Math.Max(1, booking.Quantity)
                    : booking.Package != null
                        ? booking.Package.Price
                        : booking.TotalPrice;
            }

            return requiredDeposit;
        }

        private static string GetCustomerRequestPenaltyLabel(
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

        private async Task<CustomerRequestCommissionResult>
            ApplyCustomerRequestCancellationCommissionAsync(
                Booking booking,
                string stateBeforeCancellation,
                CustomerRequestCancellationQuote quote,
                int performedByUserId)
        {
            var financial = booking.FinancialRecord;

            if (financial == null)
            {
                return CustomerRequestCommissionResult.Failed(
                    "السجل المالي المرتبط بالحجز غير موجود.");
            }

            // الحجز اليدوي خارج مسار اكتساب المنصة ولا يخضع للعمولة حتى عند إلغائه بطلب العميل.
            if (IsState(booking.BookingSource, "OwnerManual"))
            {
                financial.PlatformCommission = 0m;
                financial.CommissionRateSnapshot = 0m;
                financial.CommissionBaseAmount = 0m;
                financial.CommissionStatus = "NotApplicable";

                return CustomerRequestCommissionResult.Succeeded(
                    0m,
                    0m,
                    quote.CancellationFee);
            }

            var originalCommission = Math.Round(
                Math.Max(0m, financial.PlatformCommission),
                2);

            var commissionStatus =
                financial.CommissionStatus?.Trim() ?? string.Empty;

            var wasConfirmed = IsState(
                stateBeforeCancellation,
                "Confirmed");

            if (!wasConfirmed)
            {
                return CustomerRequestCommissionResult.Succeeded(0m, 0m, 0m);
            }

            var returnCommission =
                financial.ReturnCommissionOnCancellationSnapshot ??
                await _context.SystemSettings
                    .OrderBy(setting => setting.Id)
                    .Select(setting => (bool?)setting.ReturnCommissionOnCancellation)
                    .FirstOrDefaultAsync() ??
                true;

            financial.ReturnCommissionOnCancellationSnapshot =
                returnCommission;

            if (!returnCommission)
            {
                if (IsState(
                        financial.CommissionStatus,
                        FinancialCommissionStatuses.Calculated))
                {
                    var held = await _commissionFinanceService
                        .HoldBookingCommissionAsync(
                            booking.BookingId,
                            performedByUserId,
                            HttpContext.RequestAborted);

                    if (!held.Success)
                    {
                        return CustomerRequestCommissionResult.Failed(
                            held.Message);
                    }
                }

                var charged = await _commissionFinanceService
                    .ChargeBookingCommissionAsync(
                        booking.BookingId,
                        performedByUserId,
                        HttpContext.RequestAborted);

                if (!charged.Success)
                {
                    return CustomerRequestCommissionResult.Failed(
                        charged.Message);
                }

                return CustomerRequestCommissionResult.Succeeded(
                    financial.PlatformCommission,
                    0m,
                    Math.Max(
                        0m,
                        quote.CancellationFee -
                        financial.PlatformCommission));
            }

            if (quote.CancellationFee <= 0m)
            {
                var releaseResult = await _commissionFinanceService
                    .ReleaseBookingCommissionAsync(
                        booking.BookingId,
                        performedByUserId,
                        "تحرير عمولة الحجز بسبب إلغاء العميل عبر المالك ضمن الإلغاء المجاني.",
                        HttpContext.RequestAborted);

                if (!releaseResult.Success)
                {
                    return CustomerRequestCommissionResult.Failed(
                        releaseResult.Message);
                }

                return CustomerRequestCommissionResult.Succeeded(
                    0m,
                    originalCommission,
                    0m);
            }

            if (IsState(
                    commissionStatus,
                    FinancialCommissionStatuses.Held) ||
                IsState(
                    commissionStatus,
                    FinancialCommissionStatuses.Debt) ||
                IsState(
                    commissionStatus,
                    FinancialCommissionStatuses.Charged))
            {
                var releaseForAdjustment = await _commissionFinanceService
                    .ReleaseBookingCommissionAsync(
                        booking.BookingId,
                        performedByUserId,
                        "تحرير العمولة السابقة لإعادة احتسابها على غرامة إلغاء العميل فقط.",
                        HttpContext.RequestAborted);

                if (!releaseForAdjustment.Success)
                {
                    return CustomerRequestCommissionResult.Failed(
                        releaseForAdjustment.Message);
                }
            }

            var recalculateResult = await _commissionFinanceService
                .RecalculateBookingCommissionAsync(
                    booking.BookingId,
                    quote.CancellationFee,
                    performedByUserId,
                    "إعادة احتساب عمولة المنصة على غرامة إلغاء العميل فقط.",
                    HttpContext.RequestAborted);

            if (!recalculateResult.Success)
            {
                return CustomerRequestCommissionResult.Failed(
                    recalculateResult.Message);
            }

            var adjustedCommission = Math.Round(
                Math.Max(0m, financial.PlatformCommission),
                2);

            var holdResult = await _commissionFinanceService
                .HoldBookingCommissionAsync(
                    booking.BookingId,
                    performedByUserId,
                    HttpContext.RequestAborted);

            if (!holdResult.Success)
            {
                return CustomerRequestCommissionResult.Failed(
                    holdResult.Message);
            }

            var chargeResult = await _commissionFinanceService
                .ChargeBookingCommissionAsync(
                    booking.BookingId,
                    performedByUserId,
                    HttpContext.RequestAborted);

            if (!chargeResult.Success)
            {
                return CustomerRequestCommissionResult.Failed(
                    chargeResult.Message);
            }

            return CustomerRequestCommissionResult.Succeeded(
                adjustedCommission,
                Math.Max(0m, originalCommission - adjustedCommission),
                Math.Max(0m, quote.CancellationFee - adjustedCommission));
        }
    }
}
