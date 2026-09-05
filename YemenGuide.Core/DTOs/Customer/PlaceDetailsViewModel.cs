using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using YemenGuide.Core.Entities;

namespace YemenGuide.Core.DTOs.Customer
{
    /// <summary>
    /// عنصر من سياسة العربون المخزنة بصيغة JSON.
    /// أبقينا الأسماء القديمة val/type/desc للتوافق مع الواجهة الحالية.
    /// </summary>
    public sealed class DepositPolicyItem
    {
        [JsonPropertyName("val")]
        public string val { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string type { get; set; } = string.Empty;

        [JsonPropertyName("desc")]
        public string desc { get; set; } = string.Empty;
    }

    /// <summary>
    /// قسم سياسة جاهز للعرض بدون HTML خام أو نصوص افتراضية مضللة.
    /// </summary>
    public sealed class PlacePolicySectionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-info-circle";
        public List<string> Items { get; set; } = new();
    }

    public sealed class PlaceDetailsViewModel
    {
        // =====================================================================
        // الهوية والحالة
        // =====================================================================

        public int PlaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string MarketingDescription { get; set; } = string.Empty;
        public string TechnicalDetails { get; set; } = string.Empty;

        public string? CityName { get; set; }
        public string? AddressDetails { get; set; }
        public string? MainImage { get; set; }
        public string? CategoryName { get; set; }
        public int CategoryId { get; set; }
        public string? FloorsData { get; set; }

        // =====================================================================
        // نسبة الحجز إلى الإعلان (مصدر الإعلان يأتي من الخريطة فقط في هذه المرحلة)
        // =====================================================================
        /// <summary>
        /// إعلان صالح نُسبت إليه زيارة المنشأة بعد نقرة موثقة. تُمرر هذه
        /// القيمة للواجهة كي تحفظها مؤقتًا حتى بدء الحجز ثم يتحقق منها الخادم
        /// مرة أخرى عند إنشاء الحجز.
        /// </summary>
        public int? AttributionAdvertisementId { get; set; }

        // =====================================================================
        // التواصل مع المنشأة
        // =====================================================================

        /// <summary>
        /// رقم مالك المنشأة. تكون القيمة صفرًا عندما لا يوجد مالك مرتبط.
        /// </summary>
        public int OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        /// <summary>
        /// يحدد ما إذا كانت الدردشة مفعلة في سياسة المنشأة.
        /// </summary>
        public bool IsChatEnabled { get; set; }

        /// <summary>
        /// النتيجة النهائية لصلاحية فتح دردشة العميل مع المنشأة.
        /// </summary>
        public bool CanContactPlace { get; set; }

        /// <summary>
        /// سبب عدم إتاحة التواصل، ويُعرض للمستخدم بدل إنشاء رابط غير صالح.
        /// </summary>
        public string ChatUnavailableMessage { get; set; } = string.Empty;

        public bool HasOwner => OwnerId > 0;

        public bool IsFavorited { get; set; }
        public bool IsPlaceActive { get; set; }
        public bool IsPlaceAvailable { get; set; }
        public bool RequiresBooking { get; set; }
        public bool CanBook { get; set; }
        public bool HasBookingOptions { get; set; }
        public string BookingAvailabilityMessage { get; set; } = string.Empty;

        public List<string> DynamicRules { get; set; } = new();

        // =====================================================================
        // السعر ومعلومات التشغيل
        // =====================================================================

        /// <summary>
        /// للتوافق المؤقت مع الواجهة القديمة فقط. لا يُستخدم كسعر غرفة للفندق.
        /// </summary>
        public decimal BasePrice { get; set; }

        // قيم العرض فقط حسب عملة العميل الحالية.
        // BasePrice وLowestRoomPrice أعلاه يظلان بالريال اليمني للحسابات.
        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string DisplayCurrencyCode { get; set; } = "YER";
        public string DisplayCurrencySymbol { get; set; } = "ر.ي";

        public decimal BasePriceDisplay { get; set; }
        public string BasePriceText { get; set; } = string.Empty;

        public decimal? LowestRoomPrice { get; set; }
        public decimal? LowestRoomPriceDisplay { get; set; }
        public string LowestRoomPriceText { get; set; } = string.Empty;
        public int AvailableRoomTypesCount { get; set; }
        public int AvailableRoomUnitsCount { get; set; }
        public List<PlaceRoomTypeSummaryDto> RoomTypes { get; set; } = new();
        public int ActivePackagesCount { get; set; }

        public string? PriceUnit { get; set; }
        public string? OpeningHours { get; set; }
        public string? StatusMessage { get; set; }
        public string? PhoneNumber { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public int MaxGuestCapacity { get; set; }

        // =====================================================================
        // الموقع
        // =====================================================================

        public bool HasValidCoordinates { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? GpsCoordinates { get; set; }

        // =====================================================================
        // التقييمات
        // =====================================================================

        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> StarDistribution { get; set; } = new();
        public List<ReviewDto> Reviews { get; set; } = new();

        // =====================================================================
        // الصور والمحتوى القابل للحجز
        // =====================================================================

        public List<string> GalleryImages { get; set; } = new();
        public List<string> AvailableTimeSlots { get; set; } = new();
        public List<string> EventTypes { get; set; } = new();
        public List<string> SeatingSetups { get; set; } = new();
        public List<string> TargetAudiences { get; set; } = new();

        public List<PlaceAdditionalService> AdditionalServices { get; set; } = new();
        public List<PlaceAmenity> Amenities { get; set; } = new();
        public List<PlaceSpace> Spaces { get; set; } = new();
        public List<PlacePackage> Packages { get; set; } = new();

        // أسعار العرض للخدمات والباقات. المفاتيح هي ServiceId / PackageId.
        // الكيانات الأصلية تبقى بقيم YER حتى لا تتأثر عمليات الحجز.
        public Dictionary<int, decimal> AdditionalServiceDisplayPrices { get; set; } = new();
        public Dictionary<int, string> AdditionalServiceDisplayPriceTexts { get; set; } = new();
        public Dictionary<int, decimal> PackageDisplayPrices { get; set; } = new();
        public Dictionary<int, string> PackageDisplayPriceTexts { get; set; } = new();

        // =====================================================================
        // العروض والخصومات
        // =====================================================================

        /// <summary>
        /// العروض الفعالة والقابلة للعرض حاليًا لهذه المنشأة.
        /// لا تعتبر الأسعار الموجودة هنا تسعيرًا نهائيًا للحجز؛
        /// يتم التحقق من جميع الشروط مرة أخرى داخل مسار الحجز.
        /// </summary>
        public List<PlacePromotionDto> Promotions { get; set; } = new();

        /// <summary>
        /// العرض القادم من promotionId في رابط صفحة التفاصيل - إن كان صالحًا.
        /// </summary>
        public int? SelectedPromotionId { get; set; }

        public bool HasPromotions =>
            Promotions != null &&
            Promotions.Count > 0;

        // =====================================================================
        // السياسات
        // =====================================================================

        public string DepositPolicy { get; set; } = string.Empty;
        public string InsurancePolicy { get; set; } = string.Empty;
        public List<string> TermsList { get; set; } = new();
        public PlacePolicy? PlacePolicy { get; set; }

        public List<DepositPolicyItem> ParsedDepositPolicies { get; set; } = new();
        public List<string> ParsedSecurityPolicies { get; set; } = new();
        public List<string> ParsedExceptionPolicies { get; set; } = new();
        public List<PlacePolicySectionDto> PolicySections { get; set; } = new();

        // =====================================================================
        // نظام الحجز
        // =====================================================================

        public int BookingSystemId { get; set; }
        public bool IsHotelSystem => BookingSystemId == 1;
        public bool IsPackageSystem => BookingSystemId == 2;
        public bool IsHallSystem => BookingSystemId == 3 || BookingSystemId == 4;
    }

    /// <summary>
    /// نموذج عرض آمن ومخصص لواجهة العميل في صفحة تفاصيل المنشأة.
    /// </summary>
    public sealed class PlacePromotionDto
    {
        public int PromotionId { get; set; }
        public string PromotionTitle { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;

        // منطق العرض الجديد.
        public string OfferMode { get; set; } = "Discount";
        public string RewardType { get; set; } = string.Empty;
        public int? RequiredQuantity { get; set; }
        public int? RewardQuantity { get; set; }
        public bool IsRewardRepeatable { get; set; }
        public int? RewardServiceId { get; set; }
        public string RewardServiceName { get; set; } = string.Empty;
        public decimal? SpecialPrice { get; set; }
        public string OfferSummary { get; set; } = string.Empty;

        // الخصم القديم يبقى للتوافق مع العروض النقدية.
        public string DiscountType { get; set; } = "Percentage";
        public decimal DiscountValue { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }

        public decimal? MinimumBookingAmount { get; set; }
        public int? MinimumNights { get; set; }
        public int? MinimumPeople { get; set; }

        public bool IsAutomatic { get; set; } = true;
        public string PromoCode { get; set; } = string.Empty;
        public bool IsStackable { get; set; }

        public int? RoomId { get; set; }
        public int? PackageId { get; set; }
        public int? ServiceId { get; set; }

        public string TargetType { get; set; } = "Place";
        public string TargetLabel { get; set; } = "المنشأة كاملة";
        public string TargetAnchor { get; set; } = "placeOverviewSection";
        public string TargetIconClass { get; set; } = "bi-building";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? StayStartDate { get; set; }
        public DateTime? StayEndDate { get; set; }
        public string ApplicableDaysCsv { get; set; } = string.Empty;
        public string ApplicableDaysLabel { get; set; } = string.Empty;

        public int UsedCount { get; set; }
        public int? TotalUsageLimit { get; set; }
        public int? PerCustomerUsageLimit { get; set; }

        public bool IsSelected { get; set; }

        /// <summary>
        /// سعر مرجعي للمعاينة فقط، حسب هدف العرض:
        /// الغرفة/الباقة/الخدمة أو أقل سعر متاح للمنشأة.
        /// </summary>
        public decimal? OriginalPrice { get; set; }
        public decimal? EstimatedDiscountAmount { get; set; }
        public decimal? EstimatedPriceAfterDiscount { get; set; }

        // نسخ العرض المحولة. القيم الأصلية أعلاه تبقى YER.
        public decimal? DisplayDiscountValue { get; set; }
        public string DisplayDiscountValueText { get; set; } = string.Empty;
        public decimal? DisplayMaximumDiscountAmount { get; set; }
        public string DisplayMaximumDiscountAmountText { get; set; } = string.Empty;
        public decimal? DisplayMinimumBookingAmount { get; set; }
        public string DisplayMinimumBookingAmountText { get; set; } = string.Empty;
        public decimal? DisplayOriginalPrice { get; set; }
        public string DisplayOriginalPriceText { get; set; } = string.Empty;
        public decimal? DisplayEstimatedDiscountAmount { get; set; }
        public string DisplayEstimatedDiscountAmountText { get; set; } = string.Empty;
        public decimal? DisplayEstimatedPriceAfterDiscount { get; set; }
        public string DisplayEstimatedPriceAfterDiscountText { get; set; } = string.Empty;

        public List<string> Conditions { get; set; } = new();

        public bool IsPercentage =>
            string.Equals(
                DiscountType,
                "Percentage",
                StringComparison.OrdinalIgnoreCase);

        public bool IsCodeRequired =>
            !IsAutomatic &&
            !string.IsNullOrWhiteSpace(PromoCode);

        public bool HasEstimatedPrice =>
            OriginalPrice.HasValue &&
            EstimatedPriceAfterDiscount.HasValue &&
            (EstimatedDiscountAmount.GetValueOrDefault() > 0 ||
             (SpecialPrice.HasValue && SpecialPrice.Value >= 0));

        public bool HasImage =>
            !string.IsNullOrWhiteSpace(ImagePath);

        public string DiscountDisplay =>
            IsPercentage
                ? $"{DiscountValue:0.##}%"
                : !string.IsNullOrWhiteSpace(DisplayDiscountValueText)
                    ? DisplayDiscountValueText
                    : $"{DiscountValue:N0} ر.ي";

        public string OfferDisplay =>
            !string.IsNullOrWhiteSpace(OfferSummary)
                ? OfferSummary
                : DiscountDisplay;

        public string RemainingTimeLabel
        {
            get
            {
                int days =
                    Math.Max(
                        0,
                        (EndDate.Date - DateTime.Today).Days);

                if (days == 0)
                    return "ينتهي اليوم";

                if (days == 1)
                    return "متبقٍ يوم واحد";

                if (days <= 10)
                    return $"متبقي {days} أيام";

                return $"حتى {EndDate:dd/MM/yyyy}";
            }
        }
    }

    /// <summary>
    /// ملخص آمن لنوع الغرفة في صفحة المنشأة.
    /// يعرض المواصفات والمخزون العام دون كشف أرقام الوحدات قبل الحجز.
    /// </summary>
    public sealed class PlaceRoomTypeSummaryDto
    {
        public int RoomId { get; set; }
        public string RoomType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal NightlyPrice { get; set; }

        // سعر العرض بعملة العميل، مع إبقاء NightlyPrice الأصلي YER.
        public decimal NightlyPriceDisplay { get; set; }
        public string NightlyPriceText { get; set; } = string.Empty;

        public decimal? AreaSquareMeters { get; set; }
        public int MaxAdults { get; set; }
        public int MaxChildren { get; set; }
        public int? BedsCount { get; set; }
        public string BedType { get; set; } = string.Empty;
        public int ActiveUnitsCount { get; set; }
        public int FloorsCount { get; set; }

        public bool HasArea =>
            AreaSquareMeters.HasValue &&
            AreaSquareMeters.Value > 0;

        public bool HasBeds =>
            BedsCount.HasValue &&
            BedsCount.Value > 0;

        public bool HasBedType =>
            !string.IsNullOrWhiteSpace(BedType);

        public int MaximumGuests =>
            Math.Max(0, MaxAdults) +
            Math.Max(0, MaxChildren);

        public bool IsSuite =>
            RoomType.Contains(
                "جناح",
                StringComparison.OrdinalIgnoreCase);
    }

    public sealed class AmenityDto
    {
        public string Name { get; set; } = string.Empty;
        public string? IconClass { get; set; }
    }
}
