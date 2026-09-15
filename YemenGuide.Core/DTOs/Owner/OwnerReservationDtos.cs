using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace YemenGuide.Core.DTOs.Owner
{
    /// <summary>
    /// النموذج الموحد المعروض في تبويبات حجوزات المالك.
    /// </summary>
    public class OwnerReservationItemDto
    {
        // =========================================================
        // بيانات الحجز الأساسية
        // =========================================================

        public int BookingId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? TimerExpiredAt { get; set; }
        public decimal TotalPrice { get; set; }

        // =========================================================
        // لقطة العرض المحفوظة وقت إنشاء الحجز
        // =========================================================

        public int? PromotionId { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? PromotionTitleSnapshot { get; set; }
        public string? PromotionCodeSnapshot { get; set; }
        public string? DiscountTypeSnapshot { get; set; }
        public decimal? DiscountValueSnapshot { get; set; }

        /// <summary>
        /// نعتبر الحجز حجز عرض إذا كان هناك خصم فعلي محفوظ.
        /// الاعتماد على الـ Snapshot يحافظ على تاريخ الحجز حتى بعد انتهاء العرض.
        /// </summary>
        public bool HasPromotion =>
            DiscountAmount > 0m &&
            (
                (PromotionId.HasValue && PromotionId.Value > 0) ||
                !string.IsNullOrWhiteSpace(PromotionTitleSnapshot) ||
                !string.IsNullOrWhiteSpace(DiscountTypeSnapshot)
            );

        public bool HasPromotionCode =>
            !string.IsNullOrWhiteSpace(PromotionCodeSnapshot);

        public decimal DisplayOriginalTotalPrice =>
            OriginalTotalPrice > 0m
                ? OriginalTotalPrice
                : TotalPrice + Math.Max(0m, DiscountAmount);

        /// <summary>
        /// التوفير النهائي الحقيقي = السعر الأصلي المحفوظ - السعر النهائي.
        /// قد يختلف عن DiscountAmount عند وجود ضريبة يعاد حسابها بعد الخصم.
        /// </summary>
        public decimal FinalSavingsAmount =>
            Math.Round(
                Math.Max(
                    0m,
                    DisplayOriginalTotalPrice - TotalPrice),
                2);

        public string PromotionTitleDisplay =>
            !string.IsNullOrWhiteSpace(PromotionTitleSnapshot)
                ? PromotionTitleSnapshot.Trim()
                : "عرض خاص";

        public string PromotionDiscountLabel
        {
            get
            {
                var value =
                    Math.Max(
                        0m,
                        DiscountValueSnapshot ?? 0m);

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
                    return $"خصم {value:N0} ريال";
                }

                return "عرض مطبّق";
            }
        }
        public int Quantity { get; set; } = 1;
        public int AdultsCount { get; set; } = 1;
        public int ChildrenCount { get; set; }
        public string BookingState { get; set; } = "Pending";
        public string BookingSource { get; set; } = "Online";
        public string? BookingType { get; set; }
        public string? SeatingSetup { get; set; }

        /// <summary>
        /// سبب رفض الحجز نهائيًا فقط.
        /// </summary>
        public string? RejectionReason { get; set; }

        // =========================================================
        // تواريخ انتقالات الحالة
        // =========================================================

        public DateTime? LastStatusChangedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? PaymentApprovedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? NoShowAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        // =========================================================
        // بيانات الإغلاق والإلغاء
        // =========================================================

        public int? ClosedByUserId { get; set; }
        public string? ClosedByType { get; set; }
        public string? CancellationReason { get; set; }
        public string? OwnerCancellationReason { get; set; }

        // =========================================================
        // بيانات رفض إثبات الدفع
        // =========================================================

        public string? PaymentProofRejectionReason { get; set; }
        public DateTime? PaymentProofRejectedAt { get; set; }
        public int PaymentProofReviewCount { get; set; }

        // =========================================================
        // المواعيد التشغيلية والصلاحيات
        // =========================================================

        public DateTime OperationalStartAt { get; set; }
        public DateTime OperationalEndAt { get; set; }
        public DateTime NoShowAvailableAt { get; set; }
        public int NoShowGraceMinutes { get; set; }

        public bool CanAccept { get; set; }
        public bool CanApprovePayment { get; set; }
        public bool CanRejectPermanently { get; set; }
        public bool CanOwnerCancel { get; set; }
        public bool CanCompleteNow { get; set; }
        public bool CanMarkNoShowNow { get; set; }

        public string ActionAvailabilityText { get; set; } = string.Empty;

        // =========================================================
        // بيانات المنشأة والعميل والوحدة
        // =========================================================

        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;

        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public int? RoomId { get; set; }
        public string? RoomName { get; set; }

        /// <summary>
        /// الغرف الفعلية المسندة لهذا الحجز، مع رقم كل غرفة وطابقها.
        /// تبقى السجلات المنتهية ظاهرة في الأرشيف للحفاظ على تاريخ الحجز.
        /// </summary>
        public List<OwnerAssignedRoomUnitDto> AssignedRoomUnits { get; set; }
            = new List<OwnerAssignedRoomUnitDto>();

        public int? PackageId { get; set; }
        public string? PackageName { get; set; }

        // =========================================================
        // بيانات الدفع والتسوية
        // =========================================================

        public string PaymentStatus { get; set; } = "Unpaid";
        public string? PaymentMethod { get; set; }

        /// <summary>
        /// نوع الدفع الإلكتروني الذي اختاره العميل:
        /// Deposit = دفع العربون فقط
        /// Full = دفع كامل قيمة الحجز
        /// </summary>
        public string? OnlinePaymentType { get; set; }

        public decimal DepositAmount { get; set; }
        public decimal AmountPaidOnline { get; set; }
        public decimal AmountToPayAtProperty { get; set; }
        public decimal AmountCollectedAtProperty { get; set; }
        public bool IsPaymentConfirmed { get; set; }
        public string CommissionStatus { get; set; } = string.Empty;
        public string? PaymentNotificationImage { get; set; }
        public string? TransferReferenceNumber { get; set; }

        // =========================================================
        // بيانات الاسترداد
        // =========================================================

        public string RefundStatus { get; set; } = "NotApplicable";
        public int? RefundRequestId { get; set; }
        public string? RefundRequestStatus { get; set; }
        public decimal RefundDepositPaid { get; set; }
        public decimal RefundCancellationFee { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal RefundOwnerShare { get; set; }
        public string? RefundReason { get; set; }
        public DateTime? RefundRequestedAt { get; set; }
        public DateTime? RefundProcessedAt { get; set; }
        public string? RefundNotes { get; set; }

        // =========================================================
        // الخدمات الإضافية
        // =========================================================

        public List<OwnerReservationServiceDto> Services { get; set; }
            = new List<OwnerReservationServiceDto>();

        // =========================================================
        // الخصائص المساعدة للعرض
        // =========================================================

        public bool HasRoom => RoomId.HasValue;
        public bool HasPackage => PackageId.HasValue;

        public bool HasAssignedRoomUnits =>
            AssignedRoomUnits != null && AssignedRoomUnits.Count > 0;

        public string AssignedRoomUnitsText =>
            HasAssignedRoomUnits
                ? string.Join(
                    "، ",
                    AssignedRoomUnits.Select(unit => unit.DisplayText))
                : "لم تُسند أرقام غرف فعلية";

        public bool HasAdditionalServices =>
            Services != null && Services.Count > 0;

        public bool HasPaymentProof =>
            !string.IsNullOrWhiteSpace(PaymentNotificationImage);

        public bool HasTransferReference =>
            !string.IsNullOrWhiteSpace(TransferReferenceNumber);

        public bool HasRejectionReason =>
            !string.IsNullOrWhiteSpace(RejectionReason);

        public bool HasPaymentProofRejection =>
            !string.IsNullOrWhiteSpace(PaymentProofRejectionReason);

        public bool HasCancellationReason =>
            !string.IsNullOrWhiteSpace(CancellationReason) ||
            !string.IsNullOrWhiteSpace(OwnerCancellationReason);

        public bool HasRefundRequest =>
            RefundRequestId.HasValue ||
            !string.IsNullOrWhiteSpace(RefundRequestStatus);

        public bool IsRefundPending =>
            IsText(RefundStatus, "Pending") ||
            IsText(RefundRequestStatus, "Pending") ||
            IsText(RefundRequestStatus, "Processing");

        public bool IsRefundCompleted =>
            IsText(RefundStatus, "Completed") ||
            IsText(RefundStatus, "Refunded") ||
            IsText(RefundRequestStatus, "Completed");

        public int TotalGuests =>
            Math.Max(0, AdultsCount) +
            Math.Max(0, ChildrenCount);

        public string ReservationUnitName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(RoomName))
                {
                    return RoomName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(PackageName))
                {
                    return PackageName.Trim();
                }

                return "وحدة حجز";
            }
        }

        public string ReservationUnitType =>
            HasRoom
                ? "غرفة"
                : HasPackage
                    ? "باقة"
                    : "حجز";

        public int NightsCount
        {
            get
            {
                if (!EndDate.HasValue)
                {
                    return 1;
                }

                return Math.Max(
                    1,
                    (EndDate.Value.Date - StartDate.Date).Days);
            }
        }

        public decimal ServicesTotal =>
            Services?.Sum(service => service.TotalPrice) ?? 0m;

        public decimal RemainingAmount =>
            Math.Max(0m, AmountToPayAtProperty);

        public decimal TotalCollectedAmount =>
            Math.Max(0m, AmountPaidOnline) +
            Math.Max(0m, AmountCollectedAtProperty);

        public bool HasDeposit => DepositAmount > 0m;

        public bool IsFullOnlinePayment =>
            string.Equals(
                OnlinePaymentType?.Trim(),
                "Full",
                StringComparison.OrdinalIgnoreCase) ||
            (
                string.IsNullOrWhiteSpace(OnlinePaymentType) &&
                TotalPrice > 0m &&
                AmountPaidOnline >= TotalPrice
            );

        public bool IsDepositOnlinePayment =>
            !IsFullOnlinePayment;

        public string OnlinePaymentTypeLabel =>
            IsFullOnlinePayment
                ? "دفع كامل الحجز"
                : "دفع العربون";

        public decimal ExpectedOnlinePaymentAmount =>
            Math.Round(
                IsFullOnlinePayment
                    ? Math.Max(0m, TotalPrice)
                    : Math.Min(
                        Math.Max(0m, TotalPrice),
                        Math.Max(0m, DepositAmount)),
                2);

        public decimal ExpectedRemainingAfterApproval =>
            Math.Round(
                Math.Max(
                    0m,
                    TotalPrice - ExpectedOnlinePaymentAmount),
                2);

        public bool HasCollectedAtProperty =>
            AmountCollectedAtProperty > 0m;

        public bool IsPaymentTimerExpired =>
            IsAwaitingPayment &&
            TimerExpiredAt.HasValue &&
            TimerExpiredAt.Value <= DateTime.Now;

        public bool IsOwnerManual =>
            string.Equals(
                BookingSource?.Trim(),
                "OwnerManual",
                StringComparison.OrdinalIgnoreCase);

        public bool CanRecordManualPayment =>
            IsOwnerManual &&
            IsConfirmed &&
            RemainingAmount > 0m &&
            (
                string.IsNullOrWhiteSpace(RefundStatus) ||
                string.Equals(
                    RefundStatus.Trim(),
                    "NotApplicable",
                    StringComparison.OrdinalIgnoreCase)
            );

        public bool IsPending => IsState("Pending");
        public bool IsAwaitingPayment => IsState("AwaitingPayment");
        public bool IsPaymentReview => IsState("PaymentReview");
        public bool IsConfirmed => IsState("Confirmed");
        public bool IsCompleted => IsState("Completed");
        public bool IsRejected => IsState("Rejected");
        public bool IsCancelled => IsState("Cancelled");
        public bool IsNoShow => IsState("NoShow");
        public bool IsExpired => IsState("Expired");

        public bool IsClosed =>
            IsCompleted ||
            IsRejected ||
            IsCancelled ||
            IsNoShow ||
            IsExpired;

        public DateTime LastActionAt =>
            LastStatusChangedAt ??
            ClosedAt ??
            CompletedAt ??
            NoShowAt ??
            PaymentApprovedAt ??
            AcceptedAt ??
            CreatedAt ??
            StartDate;

        public string ClosedByLabel
        {
            get
            {
                if (IsText(ClosedByType, "Customer"))
                {
                    return "العميل";
                }

                if (IsText(ClosedByType, "Owner"))
                {
                    return "المالك";
                }

                if (IsText(ClosedByType, "Admin"))
                {
                    return "الإدارة";
                }

                if (IsText(ClosedByType, "System"))
                {
                    return "النظام";
                }

                return "غير محدد";
            }
        }

        public string ClosureReason
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(OwnerCancellationReason))
                {
                    return OwnerCancellationReason.Trim();
                }

                if (!string.IsNullOrWhiteSpace(CancellationReason))
                {
                    return CancellationReason.Trim();
                }

                if (!string.IsNullOrWhiteSpace(RejectionReason))
                {
                    return RejectionReason.Trim();
                }

                if (!string.IsNullOrWhiteSpace(RefundReason))
                {
                    return RefundReason.Trim();
                }

                if (IsExpired)
                {
                    return "انتهت مهلة الدفع.";
                }

                if (IsNoShow)
                {
                    return "تم تسجيل عدم حضور العميل.";
                }

                return string.Empty;
            }
        }

        public string LastActionLabel
        {
            get
            {
                if (IsCompleted)
                {
                    return "تم إكمال الحجز";
                }

                if (IsNoShow)
                {
                    return "تم تسجيل عدم حضور";
                }

                if (IsCancelled)
                {
                    return ClosedByLabel == "المالك"
                        ? "ألغاه المالك"
                        : ClosedByLabel == "العميل"
                            ? "ألغاه العميل"
                            : "تم إلغاء الحجز";
                }

                if (IsRejected)
                {
                    return "رفضه المالك";
                }

                if (IsExpired)
                {
                    return "أغلقه النظام لانتهاء المهلة";
                }

                if (IsConfirmed)
                {
                    return "حجز مؤكد";
                }

                if (IsPaymentReview)
                {
                    return "إثبات الدفع قيد المراجعة";
                }

                if (IsAwaitingPayment)
                {
                    return "بانتظار دفع العميل";
                }

                return "بانتظار موافقة المالك";
            }
        }

        public string RefundStatusLabel
        {
            get
            {
                if (IsText(RefundRequestStatus, "Rejected"))
                {
                    return "تم رفض طلب الاسترداد";
                }

                if (IsRefundCompleted)
                {
                    return RefundAmount > 0m
                        ? "تم تنفيذ الاسترداد"
                        : "اكتملت المعالجة دون مبلغ مسترد";
                }

                if (IsRefundPending)
                {
                    return "الاسترداد قيد المعالجة";
                }

                return "لا يوجد طلب استرداد";
            }
        }

        private bool IsState(string expected)
        {
            return IsText(BookingState, expected);
        }

        private static bool IsText(string? value, string expected)
        {
            return string.Equals(
                value?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// غرفة فعلية مرتبطة بالحجز وليست مجرد نوع غرفة.
    /// </summary>
    public class OwnerAssignedRoomUnitDto
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

    public class OwnerReservationServiceDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }

        public decimal TotalPrice =>
            Math.Max(1, Quantity) * UnitPrice;
    }

    public class RejectReservationRequestDto
    {
        [Range(1, int.MaxValue)]
        public int BookingId { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public class ReviewPaymentRequestDto
    {
        [Range(1, int.MaxValue)]
        public int BookingId { get; set; }

        public bool IsApproved { get; set; }

        [StringLength(500, MinimumLength = 5)]
        public string? RejectionReason { get; set; }
    }

    public class PermanentRejectBookingRequestDto
    {
        [Range(1, int.MaxValue)]
        public int BookingId { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public class OwnerCancelBookingRequestDto
    {
        [Range(1, int.MaxValue)]
        public int BookingId { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public class CompleteBookingRequestDto
    {
        [Range(1, int.MaxValue)]
        public int BookingId { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal CollectedAmount { get; set; }

        public bool ConfirmPartialCollection { get; set; }
    }

    public class NoShowBookingRequestDto
    {
        [Range(1, int.MaxValue)]
        public int BookingId { get; set; }

        public bool Confirmed { get; set; }
    }
}
