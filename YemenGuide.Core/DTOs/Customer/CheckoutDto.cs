using System;
using System.Collections.Generic;
using System.Linq;

namespace YemenGuide.Core.DTOs.Customer
{
    /// <summary>
    /// الاختيارات التي تصل إلى صفحة مراجعة الحجز.
    /// لا يستقبل هذا النموذج أي سعر أو اسم من المتصفح؛ الخادم يعيد قراءة
    /// كل الأسعار وبيانات العرض من قاعدة البيانات.
    /// </summary>
    public sealed class CheckoutSelectionRequestDto
    {
        public int PlaceId { get; set; }

        public int? RoomId { get; set; }

        public int? PackageId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int Quantity { get; set; } = 1;

        public int AdultsCount { get; set; } = 1;

        public int ChildrenCount { get; set; }

        public string? BookingType { get; set; }

        public string? SeatingSetup { get; set; }

        public List<int> ServiceIds { get; set; } = new();

        /// <summary>
        /// العرض الذي اختاره العميل من التفاصيل/الغرف. لا يعني وصول الرقم
        /// أن العرض سيطبّق؛ Checkout يعيد التحقق من جميع شروطه.
        /// </summary>
        public int? PromotionId { get; set; }

        /// <summary>
        /// كود العرض عند العروض غير التلقائية. لا يغيّر السعر إلا بعد
        /// التحقق منه على الخادم.
        /// </summary>
        public string? PromotionCode { get; set; }
    }

    /// <summary>
    /// نموذج صفحة مراجعة الحجز.
    /// جميع الأسماء والصور والأسعار والسياسات والعروض تُقرأ من قاعدة البيانات.
    /// </summary>
    public sealed class CheckoutViewModel
    {
        // ================================================================
        // بيانات المنشأة
        // ================================================================

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string PlaceDescription { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public string CityName { get; set; } = string.Empty;

        public int BookingSystemId { get; set; }

        public string PlaceImageUrl { get; set; } = string.Empty;

        public string AddressDetail { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public bool AllowsCashOnArrival { get; set; }

        // ================================================================
        // الغرفة أو الباقة
        // ================================================================

        public int? RoomId { get; set; }

        public int? PackageId { get; set; }

        public bool IsRoomBooking { get; set; }

        public bool IsPackageBooking { get; set; }

        public bool IsHallBooking { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string ItemDescription { get; set; } = string.Empty;

        public string ItemImageUrl { get; set; } = string.Empty;

        public List<string> ItemImages { get; set; } = new();

        public string ItemSecondaryText { get; set; } = string.Empty;

        /// <summary>
        /// سعر الوحدة لليلة الواحدة أو سعر الباقة الواحدة قبل أي عرض.
        /// </summary>
        public decimal ItemUnitPrice { get; set; }
        public decimal ItemUnitPriceDisplay { get; set; }
        public string ItemUnitPriceText { get; set; } = string.Empty;

        public int? ItemCapacity { get; set; }

        /// <summary>
        /// مواصفات نوع الغرفة كما حفظها المالك.
        /// لا تحتوي هذه البيانات على رقم غرفة فعلية قبل إنشاء الحجز.
        /// </summary>
        public decimal? AreaSquareMeters { get; set; }

        public int MaxAdults { get; set; }

        public int MaxChildren { get; set; }

        public int? BedsCount { get; set; }

        public string BedType { get; set; } = string.Empty;

        public int FloorsCount { get; set; }

        public string RoomAssignmentNotice { get; set; } =
            string.Empty;

        public bool HasArea =>
            AreaSquareMeters.HasValue &&
            AreaSquareMeters.Value > 0;

        public bool HasBeds =>
            BedsCount.HasValue &&
            BedsCount.Value > 0;

        public bool HasBedType =>
            !string.IsNullOrWhiteSpace(
                BedType);

        public bool HasRoomSpecifications =>
            IsRoomBooking &&
            (
                HasArea ||
                MaxAdults > 0 ||
                MaxChildren > 0 ||
                HasBeds ||
                HasBedType ||
                FloorsCount > 0
            );

        /// <summary>
        /// الكمية المتبقية فعلًا في الفترة المختارة.
        /// تكون 1 للباقات والقاعات.
        /// </summary>
        public int AvailableQuantity { get; set; } = 1;

        public bool HasItemImages =>
            ItemImages.Count > 0;

        public bool HasMultipleItemImages =>
            ItemImages.Count > 1;

        // ================================================================
        // اختيارات العميل
        // ================================================================

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int Quantity { get; set; } = 1;

        public int AdultsCount { get; set; } = 1;

        public int ChildrenCount { get; set; }

        public string BookingType { get; set; } = string.Empty;

        public string SeatingSetup { get; set; } = string.Empty;

        public int TotalGuests =>
            Math.Max(0, AdultsCount) +
            Math.Max(0, ChildrenCount);

        public int BookingDays
        {
            get
            {
                if (!EndDate.HasValue)
                {
                    return 1;
                }

                var days =
                    (EndDate.Value.Date -
                     StartDate.Date).Days;

                return Math.Max(1, days);
            }
        }

        public bool IsSingleDayBooking =>
            !EndDate.HasValue ||
            EndDate.Value.Date <=
            StartDate.Date;

        // ================================================================
        // العرض المطبق/المطلوب
        // ================================================================

        /// <summary>
        /// رقم العرض الذي وصل إلى Checkout حتى لو لم يتحقق بعد.
        /// يفيد في إظهار حالة العرض أو طلب الكود دون تطبيق خصم وهمي.
        /// </summary>
        public int? RequestedPromotionId { get; set; }

        /// <summary>
        /// رقم العرض الذي اجتاز التحقق النهائي في GET وتم تطبيقه على المعاينة.
        /// Create يعيد التحقق منه مرة أخرى داخل Transaction.
        /// </summary>
        public int? PromotionId { get; set; }

        public string PromotionTitle { get; set; } = string.Empty;

        public string PromotionCode { get; set; } = string.Empty;

        // ================================================================
        // منطق العرض العام (خصم مباشر أو شرط + مكافأة)
        // ================================================================
        public string PromotionOfferMode { get; set; } = "Discount";
        public string PromotionRewardType { get; set; } = string.Empty;
        public int? PromotionRequiredQuantity { get; set; }
        public int? PromotionRewardQuantity { get; set; }
        public bool PromotionIsRewardRepeatable { get; set; }
        public int AppliedFreeQuantity { get; set; }
        public int PromotionRepeatCount { get; set; }
        public int? PromotionRewardServiceId { get; set; }
        public string PromotionRewardServiceName { get; set; } = string.Empty;
        public decimal? PromotionSpecialPrice { get; set; }
        public decimal? PromotionSpecialPriceDisplay { get; set; }
        public string PromotionSpecialPriceText { get; set; } = string.Empty;
        public string PromotionOfferSummary { get; set; } = string.Empty;

        public string PromotionDiscountType { get; set; } = string.Empty;

        public decimal PromotionDiscountValue { get; set; }
        public decimal PromotionDiscountValueDisplay { get; set; }
        public string PromotionDiscountValueText { get; set; } = string.Empty;

        public decimal? PromotionMaximumDiscountAmount { get; set; }
        public decimal? PromotionMaximumDiscountAmountDisplay { get; set; }
        public string PromotionMaximumDiscountAmountText { get; set; } = string.Empty;

        public string PromotionTargetType { get; set; } = string.Empty;

        public string PromotionTargetLabel { get; set; } = string.Empty;

        public decimal PromotionEligibleAmount { get; set; }
        public decimal PromotionEligibleAmountDisplay { get; set; }
        public string PromotionEligibleAmountText { get; set; } = string.Empty;

        public decimal DiscountAmount { get; set; }
        public decimal DiscountAmountDisplay { get; set; }
        public string DiscountAmountText { get; set; } = string.Empty;

        public bool PromotionRequiresCode { get; set; }

        public bool PromotionIsAutomatic { get; set; }

        public string PromotionMessage { get; set; } = string.Empty;

        public bool PromotionWasRequested =>
            RequestedPromotionId.HasValue &&
            RequestedPromotionId.Value > 0;

        public bool HasAppliedPromotion =>
            PromotionId.HasValue &&
            PromotionId.Value > 0;

        public bool HasMonetaryPromotion =>
            HasAppliedPromotion &&
            DiscountAmount > 0;

        public bool HasRewardPromotion =>
            HasAppliedPromotion &&
            string.Equals(
                PromotionOfferMode,
                "ConditionalReward",
                StringComparison.OrdinalIgnoreCase);

        public bool HasPendingPromotionCode =>
            PromotionWasRequested &&
            !HasAppliedPromotion &&
            PromotionRequiresCode;

        public string PromotionDiscountLabel =>
            string.Equals(
                PromotionDiscountType,
                "Percentage",
                StringComparison.OrdinalIgnoreCase)
                ? $"{PromotionDiscountValue:0.##}%"
                : !string.IsNullOrWhiteSpace(PromotionDiscountValueText)
                    ? PromotionDiscountValueText
                    : $"{PromotionDiscountValue:N0} ر.ي";

        public string PromotionBenefitLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(PromotionOfferSummary))
                {
                    return PromotionOfferSummary;
                }

                return PromotionDiscountLabel;
            }
        }

        // ================================================================
        // الموافقة والدفع والسياسات
        // ================================================================

        public string BookingApprovalMode { get; set; } =
            "Instant";

        public bool RequiresOwnerApproval =>
            string.Equals(
                BookingApprovalMode,
                "Manual",
                StringComparison.OrdinalIgnoreCase);

        public bool IsInstantBooking =>
            !RequiresOwnerApproval;

        public int PaymentTimeoutHours { get; set; } = 12;

        public string CheckInTime { get; set; } = string.Empty;

        public string CheckOutTime { get; set; } = string.Empty;

        public bool RequiresDeposit { get; set; }

        public decimal DepositAmount { get; set; }
        public decimal DepositAmountDisplay { get; set; }
        public string DepositAmountText { get; set; } = string.Empty;

        // DepositCurrency الأصلي خاص ببيانات المنشأة. العرض الفعلي يعتمد CurrencyLabel أدناه.
        public string DepositCurrency { get; set; } =
            "ريال يمني";

        public List<CheckoutPolicyItemDto> Policies { get; set; } =
            new();

        public bool HasPolicies =>
            Policies.Count > 0;

        // ================================================================
        // الخدمات الإضافية
        // ================================================================

        public List<CheckoutAdditionalServiceDto>
            AdditionalServices
        { get; set; } = new();

        public bool HasAdditionalServices =>
            AdditionalServices.Count > 0;

        public List<int> SelectedServiceIds =>
            AdditionalServices
                .Select(service =>
                    service.ServiceId)
                .Distinct()
                .ToList();

        // ================================================================
        // السعر المحسوب من الخادم
        // ================================================================

        /// <summary>
        /// سعر الغرفة أو الباقة قبل الخدمات والرسوم والضريبة وقبل العرض.
        /// </summary>
        public decimal BaseAmount { get; set; }
        public decimal BaseAmountDisplay { get; set; }
        public string BaseAmountText { get; set; } = string.Empty;

        public decimal ServicesTotal { get; set; }
        public decimal ServicesTotalDisplay { get; set; }
        public string ServicesTotalText { get; set; } = string.Empty;

        public decimal CleaningFee { get; set; }
        public decimal CleaningFeeDisplay { get; set; }
        public string CleaningFeeText { get; set; } = string.Empty;

        public decimal TaxPercentage { get; set; }

        /// <summary>
        /// الضريبة بعد تطبيق الخصم على الجزء المؤهل.
        /// </summary>
        public decimal TaxAmount { get; set; }
        public decimal TaxAmountDisplay { get; set; }
        public string TaxAmountText { get; set; } = string.Empty;

        /// <summary>
        /// الضريبة التي كانت ستُحسب لو لم يوجد عرض.
        /// </summary>
        public decimal OriginalTaxAmount { get; set; }
        public decimal OriginalTaxAmountDisplay { get; set; }
        public string OriginalTaxAmountText { get; set; } = string.Empty;

        /// <summary>
        /// الإجمالي الكامل قبل الخصم (شامل الضريبة الأصلية).
        /// يخزن لاحقًا في Bookings.OriginalTotalPrice.
        /// </summary>
        public decimal OriginalTotalPrice { get; set; }
        public decimal OriginalTotalPriceDisplay { get; set; }
        public string OriginalTotalPriceText { get; set; } = string.Empty;

        /// <summary>
        /// المجموع قبل الضريبة بعد خصم العرض.
        /// </summary>
        public decimal DiscountedBeforeTaxAmount { get; set; }
        public decimal DiscountedBeforeTaxAmountDisplay { get; set; }
        public string DiscountedBeforeTaxAmountText { get; set; } = string.Empty;

        /// <summary>
        /// الإجمالي النهائي بعد العرض وإعادة حساب الضريبة.
        /// </summary>
        public decimal FinalTotal { get; set; }
        public decimal FinalTotalDisplay { get; set; }
        public string FinalTotalText { get; set; } = string.Empty;

        public string CurrencyLabel { get; set; } =
            "ريال يمني";
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";

        public decimal BeforeTaxAmount =>
            Math.Round(
                BaseAmount +
                ServicesTotal +
                CleaningFee,
                2);

        public decimal RemainingAfterDeposit =>
            Math.Round(
                Math.Max(
                    0m,
                    FinalTotal -
                    DepositAmount),
                2);

        public decimal BeforeTaxAmountDisplay { get; set; }
        public string BeforeTaxAmountText { get; set; } = string.Empty;
        public decimal RemainingAfterDepositDisplay { get; set; }
        public string RemainingAfterDepositText { get; set; } = string.Empty;

        /// <summary>
        /// التوفير الكلي في الفاتورة، ويشمل أثر انخفاض الضريبة بعد الخصم.
        /// </summary>
        public decimal TotalSavings =>
            Math.Round(
                Math.Max(
                    0m,
                    OriginalTotalPrice -
                    FinalTotal),
                2);

        public decimal TotalSavingsDisplay { get; set; }
        public string TotalSavingsText { get; set; } = string.Empty;

        public bool HasCleaningFee =>
            CleaningFee > 0;

        public bool HasTax =>
            TaxAmount > 0 &&
            TaxPercentage > 0;
    }

    /// <summary>
    /// خدمة إضافية بعد التحقق منها وقراءة سعرها من قاعدة البيانات.
    /// </summary>
    public sealed class CheckoutAdditionalServiceDto
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }
        public decimal UnitPriceDisplay { get; set; }
        public string UnitPriceText { get; set; } = string.Empty;

        public decimal TotalPrice =>
            Math.Round(
                Math.Max(1, Quantity) *
                Math.Max(0m, UnitPrice),
                2);

        public decimal TotalPriceDisplay { get; set; }
        public string TotalPriceText { get; set; } = string.Empty;
    }

    /// <summary>
    /// سياسة حقيقية تخص المنشأة.
    /// </summary>
    public sealed class CheckoutPolicyItemDto
    {
        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
