using System;
using System.Collections.Generic;
using System.Linq;

namespace YemenGuide.Core.DTOs.Customer
{
    /// <summary>
    /// النموذج الرئيسي لصفحة حجوزات العميل.
    /// يحتوي على قائمة الحجوزات والعدادات المستخدمة في الفلاتر.
    /// </summary>
    public class CustomerBookingsPageDto
    {
        public IReadOnlyList<CustomerBookingItemDto> Bookings { get; set; }
            = Array.Empty<CustomerBookingItemDto>();

        public int TotalCount =>
            Bookings.Count;

        public int PendingCount =>
            Bookings.Count(booking =>
                booking.IsPending);

        public int AwaitingPaymentCount =>
            Bookings.Count(booking =>
                booking.IsAwaitingPayment);

        public int PaymentReviewCount =>
            Bookings.Count(booking =>
                booking.IsPaymentReview);

        public int ConfirmedCount =>
            Bookings.Count(booking =>
                booking.IsConfirmed);

        public int CompletedCount =>
            Bookings.Count(booking =>
                booking.IsCompleted);

        public int ExpiredCount =>
            Bookings.Count(booking =>
                booking.IsExpired);

        public int NoShowCount =>
            Bookings.Count(booking =>
                booking.IsNoShow);

        public int CancelledCount =>
            Bookings.Count(booking =>
                booking.IsCancelled);

        public int RejectedCount =>
            Bookings.Count(booking =>
                booking.IsRejected);

        /// <summary>
        /// الحجوزات التي تحتاج إجراء مباشر من العميل.
        /// مثل دفع العربون.
        /// </summary>
        public int RequiresActionCount =>
            Bookings.Count(booking =>
                booking.CanPayDeposit);
    }

    /// <summary>
    /// بيانات الحجز المختصرة المستخدمة داخل صفحة حجوزاتي.
    /// التفاصيل الكاملة تبقى في صفحة BookingDetails.
    /// </summary>
    public class CustomerBookingItemDto
    {
        public int BookingId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; }
            = string.Empty;

        public string? PlaceType { get; set; }
        public string PlaceImageUrl { get; set; } = "/images/bg1.jpg";

        public int? RoomId { get; set; }

        public string? RoomName { get; set; }

        public List<CustomerAssignedRoomUnitDto> AssignedRoomUnits { get; set; }
            = new List<CustomerAssignedRoomUnitDto>();

        public int? PackageId { get; set; }

        public string? PackageName { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal TotalPrice { get; set; }
        public decimal TotalPriceDisplay { get; set; }
        public string TotalPriceText { get; set; } = string.Empty;

        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string DisplayCurrencyCode { get; set; } = "YER";
        public string DisplayCurrencySymbol { get; set; } = "ر.ي";

        /* ============================================================
           العرض المحفوظ مع الحجز
           ============================================================ */

        public int? PromotionId { get; set; }

        public decimal OriginalTotalPrice { get; set; }
        public decimal OriginalTotalPriceDisplay { get; set; }
        public string OriginalTotalPriceText { get; set; } = string.Empty;

        public decimal DiscountAmount { get; set; }
        public decimal DiscountAmountDisplay { get; set; }
        public string DiscountAmountText { get; set; } = string.Empty;

        public string? PromotionTitleSnapshot { get; set; }

        public string? PromotionCodeSnapshot { get; set; }

        public string? DiscountTypeSnapshot { get; set; }

        public decimal? DiscountValueSnapshot { get; set; }
        public decimal? DiscountValueSnapshotDisplay { get; set; }
        public string DiscountValueSnapshotText { get; set; } = string.Empty;

        public bool HasPromotion =>
            PromotionId.HasValue &&
            PromotionId.Value > 0 &&
            DiscountAmount > 0m;

        public decimal DisplayOriginalTotalPrice =>
            OriginalTotalPrice > 0m
                ? OriginalTotalPrice
                : TotalPrice + Math.Max(0m, DiscountAmount);

        public string PromotionDiscountLabel
        {
            get
            {
                var value = Math.Max(0m, DiscountValueSnapshot ?? 0m);

                if (string.Equals(
                        DiscountTypeSnapshot,
                        "Percentage",
                        StringComparison.OrdinalIgnoreCase) &&
                    value > 0m)
                {
                    return $"خصم {value:N0}%";
                }

                if (string.Equals(
                        DiscountTypeSnapshot,
                        "FixedAmount",
                        StringComparison.OrdinalIgnoreCase) &&
                    value > 0m)
                {
                    return !string.IsNullOrWhiteSpace(DiscountValueSnapshotText)
                        ? $"خصم {DiscountValueSnapshotText}"
                        : $"خصم {value:N0} ر.ي";
                }

                return "عرض مطبّق";
            }
        }

        public string BookingState { get; set; }
            = "Pending";

        public string PaymentStatus { get; set; }
            = "Unpaid";

        public string? PaymentMethod { get; set; }

        public decimal DepositAmount { get; set; }
        public decimal DepositAmountDisplay { get; set; }
        public string DepositAmountText { get; set; } = string.Empty;

        public decimal AmountPaidOnline { get; set; }
        public decimal AmountPaidOnlineDisplay { get; set; }
        public string AmountPaidOnlineText { get; set; } = string.Empty;

        public bool IsPaymentConfirmed { get; set; }

        public bool HasPaymentProof { get; set; }

        public string RefundStatus { get; set; }
            = "NotApplicable";

        public string? RefundRequestStatus { get; set; }

        /// <summary>
        /// آخر طلب استرداد مرتبط بالحجز، إن وجد.
        /// </summary>
        public int? RefundRequestId { get; set; }

        /// <summary>
        /// وجهة الاسترداد التي اختارها العميل.
        /// </summary>
        public int? RefundWalletId { get; set; }

        public string? RefundAccountNumber { get; set; }

        public string? RefundBeneficiaryName { get; set; }

        /// <summary>
        /// الرقم المرجعي الذي سجله المالك بعد تنفيذ التحويل.
        /// </summary>
        public string? RefundReferenceNumber { get; set; }

        public DateTime? RefundTransferredAt { get; set; }

        public DateTime? RefundCustomerConfirmedAt { get; set; }

        public bool HasRefundProof { get; set; }

        public string? RefundNotes { get; set; }

        public decimal RefundAmount { get; set; }
        public decimal RefundAmountDisplay { get; set; }
        public string RefundAmountText { get; set; } = string.Empty;

        public decimal CancellationFee { get; set; }
        public decimal CancellationFeeDisplay { get; set; }
        public string CancellationFeeText { get; set; } = string.Empty;

        public string? CancellationReason { get; set; }

        public string? OwnerCancellationReason { get; set; }

        public string? PaymentProofRejectionReason { get; set; }

        public DateTime? PaymentProofRejectedAt { get; set; }

        public int PaymentProofReviewCount { get; set; }

        public DateTime? LastStatusChangedAt { get; set; }

        public DateTime? AcceptedAt { get; set; }

        public DateTime? PaymentApprovedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? NoShowAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        public int? ClosedByUserId { get; set; }

        public string? ClosedByType { get; set; }

        public int RefundProcessingDays { get; set; } = 3;

        public DateTime? TimerExpiredAt { get; set; }

        public string? RejectionReason { get; set; }

        public DateTime? CreatedAt { get; set; }

        public int Quantity { get; set; } = 1;

        public int AdultsCount { get; set; }

        public int ChildrenCount { get; set; }

        public string? BookingType { get; set; }

        public string? SeatingSetup { get; set; }


        /* ============================================================
           معلومات الوحدة
           ============================================================ */

        public bool HasRoom =>
            RoomId.HasValue;

        public bool HasPackage =>
            PackageId.HasValue;

        public bool HasAssignedRoomUnits =>
            AssignedRoomUnits != null && AssignedRoomUnits.Count > 0;

        public string AssignedRoomUnitsText =>
            HasAssignedRoomUnits
                ? string.Join(
                    "، ",
                    AssignedRoomUnits.Select(unit => unit.DisplayText))
                : "لم تُحدد أرقام الغرف بعد";

        public string ReservationUnitName
        {
            get
            {
                if (HasRoom &&
                    !string.IsNullOrWhiteSpace(RoomName))
                {
                    return RoomName.Trim();
                }

                if (HasPackage &&
                    !string.IsNullOrWhiteSpace(PackageName))
                {
                    return PackageName.Trim();
                }

                return "حجز المنشأة";
            }
        }

        public string ReservationUnitType
        {
            get
            {
                if (HasRoom)
                {
                    return "غرفة";
                }

                if (HasPackage)
                {
                    return "باقة";
                }

                return "حجز";
            }
        }


        /* ============================================================
           معلومات المدة والضيوف
           ============================================================ */

        public int NightsCount
        {
            get
            {
                if (!EndDate.HasValue)
                {
                    return 0;
                }

                var nights =
                    (EndDate.Value.Date -
                     StartDate.Date).Days;

                return Math.Max(0, nights);
            }
        }

        public int TotalGuests =>
            Math.Max(0, AdultsCount) +
            Math.Max(0, ChildrenCount);

        public bool IsSameDayBooking =>
            !EndDate.HasValue ||
            EndDate.Value.Date == StartDate.Date;


        /* ============================================================
           حالات الحجز
           ============================================================ */

        public bool IsPending =>
            IsState("Pending");

        public bool IsAwaitingPayment =>
            IsState("AwaitingPayment");

        public bool IsPaymentReview =>
            IsState("PaymentReview");

        public bool IsConfirmed =>
            IsState("Confirmed");

        public bool IsCompleted =>
            IsState("Completed");

        public bool IsCancelled =>
            IsState("Cancelled");

        public bool IsRejected =>
            IsState("Rejected");

        public bool IsExpired =>
            IsState("Expired");

        public bool IsNoShow =>
            IsState("NoShow");


        /* ============================================================
           الدفع والمهلة
           ============================================================ */

        public bool HasPaymentTimer =>
            TimerExpiredAt.HasValue;

        public bool IsPaymentTimerExpired =>
            IsAwaitingPayment &&
            TimerExpiredAt.HasValue &&
            TimerExpiredAt.Value <= DateTime.Now;

        public bool CanPayDeposit =>
            IsAwaitingPayment &&
            !IsPaymentTimerExpired;

        public bool HasRejectionReason =>
            !string.IsNullOrWhiteSpace(
                RejectionReason);

        public bool HasConfirmedDeposit =>
            IsPaymentConfirmed &&
            AmountPaidOnline > 0m;

        public bool HasPaymentUnderReview =>
            IsPaymentReview &&
            HasPaymentProof;

        public bool HasRefundRequest =>
            RefundRequestId.HasValue &&
            RefundRequestId.Value > 0 &&
            !string.IsNullOrWhiteSpace(
                RefundRequestStatus);

        public bool HasRefundDestination =>
            RefundWalletId.HasValue &&
            RefundWalletId.Value > 0 &&
            !string.IsNullOrWhiteSpace(
                RefundAccountNumber) &&
            !string.IsNullOrWhiteSpace(
                RefundBeneficiaryName);

        public bool IsRefundTransferred =>
            RefundTransferredAt.HasValue &&
            !string.IsNullOrWhiteSpace(
                RefundReferenceNumber);

        public bool IsRefundCompleted =>
            string.Equals(
                RefundRequestStatus,
                "Completed",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                RefundStatus,
                "Completed",
                StringComparison.OrdinalIgnoreCase) ||
            RefundCustomerConfirmedAt.HasValue;

        public bool CanSetRefundDestination =>
            HasRefundRequest &&
            RefundAmount > 0m &&
            !IsRefundTransferred &&
            !IsRefundCompleted &&
            !string.Equals(
                RefundRequestStatus,
                "Cancelled",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                RefundRequestStatus,
                "Rejected",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                RefundRequestStatus,
                "Failed",
                StringComparison.OrdinalIgnoreCase);

        public bool CanConfirmRefundReceived =>
            HasRefundRequest &&
            RefundAmount > 0m &&
            IsRefundTransferred &&
            !IsRefundCompleted &&
            string.Equals(
                RefundRequestStatus,
                "Processing",
                StringComparison.OrdinalIgnoreCase);

        public bool IsRefundPending =>
            string.Equals(
                RefundStatus,
                "Pending",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                RefundRequestStatus,
                "Pending",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                RefundRequestStatus,
                "Processing",
                StringComparison.OrdinalIgnoreCase);

        public bool HasCustomerCancellationReason =>
            !string.IsNullOrWhiteSpace(
                CancellationReason);

        public bool HasOwnerCancellationReason =>
            !string.IsNullOrWhiteSpace(
                OwnerCancellationReason);

        public bool HasCancellationReason =>
            HasCustomerCancellationReason ||
            HasOwnerCancellationReason;

        public bool HasPaymentProofRejection =>
            !string.IsNullOrWhiteSpace(
                PaymentProofRejectionReason);

        public bool CanUploadPaymentProofAgain =>
            CanPayDeposit &&
            HasPaymentProofRejection;

        public bool IsCancelledByOwner =>
            IsCancelled &&
            IsClosedBy("Owner");

        public bool IsCancelledByCustomer =>
            IsCancelled &&
            IsClosedBy("Customer");

        public bool IsClosedBySystem =>
            IsClosedBy("System");

        public bool IsClosedByAdmin =>
            IsClosedBy("Admin");

        public bool CanCancel =>
            (IsPending ||
             IsAwaitingPayment ||
             IsPaymentReview ||
             IsConfirmed) &&
            StartDate.Date >= DateTime.Today;

        public bool CanShowRefundSummary =>
            IsCancelled &&
            (HasRefundRequest ||
             RefundAmount > 0m ||
             CancellationFee > 0m);

        public DateTime EffectiveStatusChangedAt =>
            LastStatusChangedAt ??
            ClosedAt ??
            CreatedAt ??
            StartDate;

        public string ClosureSourceLabel
        {
            get
            {
                if (IsClosedBy("Owner"))
                {
                    return "المنشأة";
                }

                if (IsClosedBy("Customer"))
                {
                    return "العميل";
                }

                if (IsClosedBy("Admin"))
                {
                    return "إدارة المنصة";
                }

                if (IsClosedBy("System"))
                {
                    return "النظام تلقائيًا";
                }

                return "غير محدد";
            }
        }

        public string? ClosureReason
        {
            get
            {
                if (IsCancelled &&
                    HasOwnerCancellationReason)
                {
                    return OwnerCancellationReason!.Trim();
                }

                if (IsCancelled &&
                    HasCustomerCancellationReason)
                {
                    return CancellationReason!.Trim();
                }

                if (IsRejected &&
                    HasRejectionReason)
                {
                    return RejectionReason!.Trim();
                }

                if (IsExpired)
                {
                    return "انتهت مهلة دفع العربون.";
                }

                return null;
            }
        }

        public string RefundStatusLabel
        {
            get
            {
                if (IsRefundCompleted)
                {
                    return RefundAmount > 0m
                        ? "تم تأكيد استلام الاسترداد"
                        : "لا يوجد مبلغ مسترد";
                }

                if (CanConfirmRefundReceived)
                {
                    return "تم تحويل الاسترداد — بانتظار تأكيدك";
                }

                if (string.Equals(
                        RefundRequestStatus,
                        "Processing",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "تم تسجيل تحويل الاسترداد";
                }

                if (string.Equals(
                        RefundRequestStatus,
                        "Rejected",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "تم رفض طلب الاسترداد";
                }

                if (string.Equals(
                        RefundRequestStatus,
                        "Failed",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "تعذر تنفيذ الاسترداد";
                }

                if (string.Equals(
                        RefundRequestStatus,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "تم إلغاء طلب الاسترداد";
                }

                if (CanSetRefundDestination &&
                    !HasRefundDestination)
                {
                    return "حدد حساب استلام الاسترداد";
                }

                if (CanSetRefundDestination &&
                    HasRefundDestination)
                {
                    return "تم حفظ حساب الاسترداد";
                }

                if (IsRefundPending)
                {
                    return "طلب الاسترداد قيد المراجعة";
                }

                return "لا يوجد طلب استرداد";
            }
        }


        /* ============================================================
           بيانات العرض والفلترة
           ============================================================ */

        /// <summary>
        /// القيمة التي ستوضع داخل data-booking-status
        /// وتستخدمها فلاتر JavaScript.
        /// </summary>
        public string FilterStatus
        {
            get
            {
                if (IsPending)
                {
                    return "pending";
                }

                if (IsAwaitingPayment)
                {
                    return "awaiting-payment";
                }

                if (IsPaymentReview)
                {
                    return "payment-review";
                }

                if (IsConfirmed)
                {
                    return "confirmed";
                }

                if (IsCompleted)
                {
                    return "completed";
                }

                if (IsExpired)
                {
                    return "expired";
                }

                if (IsNoShow)
                {
                    return "no-show";
                }

                if (IsCancelled)
                {
                    return "cancelled";
                }

                if (IsRejected)
                {
                    return "rejected";
                }

                return "other";
            }
        }

        public string StateLabel
        {
            get
            {
                if (IsPending)
                {
                    return "بانتظار موافقة المالك";
                }

                if (IsAwaitingPayment)
                {
                    return IsPaymentTimerExpired
                        ? "انتهت مهلة الدفع"
                        : "مطلوب دفع العربون";
                }

                if (IsPaymentReview)
                {
                    return "إثبات الدفع قيد المراجعة";
                }

                if (IsConfirmed)
                {
                    return "حجز مؤكد";
                }

                if (IsCompleted)
                {
                    return "حجز مكتمل";
                }

                if (IsCancelled)
                {
                    if (IsCancelledByOwner)
                    {
                        return "ألغت المنشأة الحجز";
                    }

                    if (IsCancelledByCustomer)
                    {
                        return "حجز ملغي من العميل";
                    }

                    return "حجز ملغي";
                }

                if (IsRejected)
                {
                    return "طلب مرفوض";
                }

                if (IsExpired)
                {
                    return "انتهت مهلة الدفع";
                }

                if (IsNoShow)
                {
                    return "عدم حضور";
                }

                return "حالة غير محددة";
            }
        }

        public string StateCssClass
        {
            get
            {
                if (IsPending)
                {
                    return "booking-status-pending";
                }

                if (IsAwaitingPayment)
                {
                    return IsPaymentTimerExpired
                        ? "booking-status-expired"
                        : "booking-status-payment";
                }

                if (IsPaymentReview)
                {
                    return "booking-status-review";
                }

                if (IsConfirmed)
                {
                    return "booking-status-confirmed";
                }

                if (IsCompleted)
                {
                    return "booking-status-completed";
                }

                if (IsCancelled)
                {
                    return "booking-status-cancelled";
                }

                if (IsRejected)
                {
                    return "booking-status-rejected";
                }

                if (IsExpired)
                {
                    return "booking-status-expired";
                }

                if (IsNoShow)
                {
                    return "booking-status-noshow";
                }

                return "booking-status-default";
            }
        }

        public string StateIcon
        {
            get
            {
                if (IsPending)
                {
                    return "bi-hourglass-split";
                }

                if (IsAwaitingPayment)
                {
                    return IsPaymentTimerExpired
                        ? "bi-hourglass-bottom"
                        : "bi-wallet2";
                }

                if (IsPaymentReview)
                {
                    return "bi-file-earmark-check";
                }

                if (IsConfirmed)
                {
                    return "bi-calendar-check-fill";
                }

                if (IsCompleted)
                {
                    return "bi-check2-circle";
                }

                if (IsCancelled)
                {
                    return "bi-slash-circle";
                }

                if (IsRejected)
                {
                    return "bi-x-octagon";
                }

                if (IsExpired)
                {
                    return "bi-hourglass-bottom";
                }

                if (IsNoShow)
                {
                    return "bi-person-x";
                }

                return "bi-info-circle";
            }
        }


        /* ============================================================
           روابط الصفحة
           ============================================================ */

        public string DetailsUrl =>
            $"/Places/BookingDetails/{BookingId}";

        public string PaymentUrl =>
            $"/BookingFlow/Payment?id={BookingId}";

        public string CancellationQuoteUrl =>
            $"/Bookings/CancellationQuote?id={BookingId}";

        public string CancelBookingUrl =>
            "/Bookings/Cancel";


        /* ============================================================
           دوال داخلية
           ============================================================ */

        private bool IsState(string state)
        {
            return string.Equals(
                BookingState?.Trim(),
                state,
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsClosedBy(string source)
        {
            return string.Equals(
                ClosedByType?.Trim(),
                source,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class CustomerAssignedRoomUnitDto
    {
        public int RoomUnitId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int FloorNumber { get; set; }
        public string? FloorLabel { get; set; }
        public string AssignmentStatus { get; set; } = "Held";

        public string FloorDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FloorLabel))
                {
                    return FloorLabel.Trim();
                }

                if (FloorNumber == 0)
                {
                    return "الدور الأرضي";
                }

                if (FloorNumber < 0)
                {
                    return $"القبو {Math.Abs(FloorNumber)}";
                }

                return $"الطابق {FloorNumber}";
            }
        }

        public string DisplayText =>
            string.IsNullOrWhiteSpace(RoomNumber)
                ? FloorDisplay
                : $"غرفة {RoomNumber.Trim()} — {FloorDisplay}";
    }

    /// <summary>
    /// البيانات التي يرسلها العميل عند تأكيد إلغاء الحجز.
    /// </summary>
    public sealed class CancelBookingRequestDto
    {
        public int BookingId { get; set; }

        public string Reason { get; set; }
            = string.Empty;
    }

    /// <summary>
    /// ملخص سياسة الإلغاء الذي يعرض للعميل قبل التأكيد.
    /// جميع المبالغ تحسب في الخادم من بيانات الحجز وسياسات المالك.
    /// </summary>
    public sealed class CancellationQuoteDto
    {
        public int BookingId { get; set; }
        public bool CanCancel { get; set; }
        public string BookingState { get; set; } = string.Empty;
        public bool IsPaymentUnderReview { get; set; }
        public bool HasConfirmedPayment { get; set; }
        public decimal DepositPaid { get; set; }
        public decimal CancellationFee { get; set; }
        public decimal RefundAmount { get; set; }

        // قيم عرض مستقلة عن القيم المالية الأصلية.
        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string DisplayCurrencyCode { get; set; } = "YER";
        public string DisplayCurrencySymbol { get; set; } = "ر.ي";
        public decimal DepositPaidDisplay { get; set; }
        public string DepositPaidText { get; set; } = string.Empty;
        public decimal CancellationFeeDisplay { get; set; }
        public string CancellationFeeText { get; set; } = string.Empty;
        public decimal RefundAmountDisplay { get; set; }
        public string RefundAmountText { get; set; } = string.Empty;
        public bool IsFreeCancellation { get; set; }
        public int FreeCancellationHours { get; set; }
        public double HoursUntilStart { get; set; }
        public string PenaltyType { get; set; } = string.Empty;
        public decimal PenaltyValue { get; set; }
        public decimal? PenaltyValueDisplay { get; set; }
        public string PenaltyValueText { get; set; } = string.Empty;
        public string PolicyLabel { get; set; } = string.Empty;
        public int RefundProcessingDays { get; set; }
        public string RefundTransferFee { get; set; } = string.Empty;
        public string RefundTransferFeeLabel { get; set; } = string.Empty;
        public bool RequiresRefundRequest { get; set; }
        public string CustomerRefundMessage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public static CancellationQuoteDto NotAllowed(
            int bookingId,
            string message)
        {
            return new CancellationQuoteDto
            {
                BookingId = bookingId,
                CanCancel = false,
                Message = message
            };
        }
    }
}
