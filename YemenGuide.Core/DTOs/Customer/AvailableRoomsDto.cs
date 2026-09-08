using System;
using System.Collections.Generic;
using System.Linq;

namespace YemenGuide.Core.DTOs.Customer
{
    /// <summary>
    /// نموذج صفحة الغرف المتاحة.
    /// جميع البيانات المعروضة فيه مجهزة من الخادم وقاعدة البيانات.
    /// </summary>
    public sealed class AvailableRoomsViewModel
    {
        // ================================================================
        // بيانات المنشأة
        // ================================================================

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public string CityName { get; set; } = string.Empty;

        public string AddressDetail { get; set; } = string.Empty;

        public string PlaceImageUrl { get; set; } = string.Empty;

        public string CheckInTime { get; set; } = string.Empty;

        public string CheckOutTime { get; set; } = string.Empty;

        public string CurrencyLabel { get; set; } = "ريال يمني";
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";

        // ================================================================
        // فترة الإقامة
        // ================================================================

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int Nights
        {
            get
            {
                var days =
                    (EndDate.Date - StartDate.Date).Days;

                return Math.Max(1, days);
            }
        }

        // ================================================================
        // الخدمات المختارة من صفحة التفاصيل
        // ================================================================

        public List<int> SelectedServiceIds { get; set; } = new();

        // ================================================================
        // العرض القادم من صفحة التفاصيل
        // ================================================================

        /// <summary>
        /// رقم العرض المطلوب في الرابط، حتى لو تعذر تطبيقه على الفترة الحالية.
        /// يستخدم فقط لعرض رسالة دقيقة للمستخدم ولا يعني قبول العرض ماليًا.
        /// </summary>
        public int? RequestedPromotionId { get; set; }

        /// <summary>
        /// العرض الذي اجتاز التحقق الأولي لهذه الصفحة ويمكن تمريره إلى Checkout.
        /// Checkout يعيد التحقق منه مرة أخرى قبل اعتماد أي خصم مالي.
        /// </summary>
        public int? SelectedPromotionId { get; set; }

        public string SelectedPromotionTitle { get; set; } = string.Empty;
        public string PromotionOfferMode { get; set; } = "Discount";
        public string PromotionRewardType { get; set; } = string.Empty;
        public int? PromotionRequiredQuantity { get; set; }
        public int? PromotionRewardQuantity { get; set; }
        public bool PromotionIsRewardRepeatable { get; set; }
        public int PromotionAppliedFreeQuantity { get; set; }
        public int PromotionRepeatCount { get; set; }
        public int? PromotionRewardServiceId { get; set; }
        public string PromotionRewardServiceName { get; set; } = string.Empty;
        public decimal? PromotionSpecialPrice { get; set; }
        public decimal? PromotionSpecialPriceDisplay { get; set; }
        public string PromotionSpecialPriceText { get; set; } = string.Empty;
        public string PromotionOfferSummary { get; set; } = string.Empty;
        public string PromotionTargetType { get; set; } = string.Empty;
        public int? PromotionTargetRoomId { get; set; }
        public string PromotionDiscountDisplay { get; set; } = string.Empty;
        public string PromotionStateMessage { get; set; } = string.Empty;
        public bool IsPromotionEligibleForStay { get; set; }
        public bool IsSelectedPromotionRoomUnavailable { get; set; }
        public bool PromotionRequiresPeopleConfirmation { get; set; }
        public int? PromotionMinimumPeople { get; set; }
        public bool PromotionRequiresCode { get; set; }
        public string PromotionCode { get; set; } = string.Empty;

        public bool HasRequestedPromotion =>
            RequestedPromotionId.HasValue &&
            RequestedPromotionId.Value > 0;

        public bool HasSelectedPromotion =>
            SelectedPromotionId.HasValue &&
            SelectedPromotionId.Value > 0;

        // ================================================================
        // الغرف المتاحة
        // ================================================================

        public List<AvailableRoomItemDto> Rooms { get; set; } = new();

        public bool HasAvailableRooms =>
            Rooms.Count > 0;

        public int AvailableRoomTypesCount =>
            Rooms.Count;

        public int AvailableUnitsCount =>
            Rooms.Sum(room =>
                Math.Max(
                    0,
                    room.RemainingQuantity));
    }

    /// <summary>
    /// غرفة أو جناح متاح خلال الفترة المختارة.
    /// </summary>
    public sealed class AvailableRoomItemDto
    {
        public int RoomId { get; set; }

        public int PlaceId { get; set; }

        public string RoomNumber { get; set; } = string.Empty;

        public string RoomType { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // ================================================================
        // الأسعار
        // ================================================================

        public decimal NightlyPrice { get; set; }
        public decimal NightlyPriceDisplay { get; set; }
        public string NightlyPriceText { get; set; } = string.Empty;

        public decimal StayPrice { get; set; }
        public decimal StayPriceDisplay { get; set; }
        public string StayPriceText { get; set; } = string.Empty;

        // ================================================================
        // معاينة العرض على هذه الغرفة
        // ================================================================

        public int? PromotionId { get; set; }
        public string PromotionTitle { get; set; } = string.Empty;
        public string PromotionOfferMode { get; set; } = "Discount";
        public string PromotionRewardType { get; set; } = string.Empty;
        public int? PromotionRequiredQuantity { get; set; }
        public int? PromotionRewardQuantity { get; set; }
        public bool PromotionIsRewardRepeatable { get; set; }
        public int PromotionAppliedFreeQuantity { get; set; }
        public int PromotionRepeatCount { get; set; }
        public int? PromotionRewardServiceId { get; set; }
        public string PromotionRewardServiceName { get; set; } = string.Empty;
        public decimal? PromotionSpecialPrice { get; set; }
        public decimal? PromotionSpecialPriceDisplay { get; set; }
        public string PromotionSpecialPriceText { get; set; } = string.Empty;
        public string PromotionOfferSummary { get; set; } = string.Empty;
        public string PromotionDiscountDisplay { get; set; } = string.Empty;
        public string PromotionDiscountType { get; set; } = string.Empty;
        public decimal PromotionDiscountValue { get; set; }
        public decimal PromotionDiscountValueDisplay { get; set; }
        public string PromotionDiscountValueText { get; set; } = string.Empty;

        public decimal? PromotionMaximumDiscountAmount { get; set; }
        public decimal? PromotionMaximumDiscountAmountDisplay { get; set; }
        public string PromotionMaximumDiscountAmountText { get; set; } = string.Empty;

        public decimal? PromotionMinimumBookingAmount { get; set; }
        public decimal? PromotionMinimumBookingAmountDisplay { get; set; }
        public string PromotionMinimumBookingAmountText { get; set; } = string.Empty;
        public bool IsPromotionSelected { get; set; }
        public bool IsPromotionApplicable { get; set; }
        public bool PromotionRequiresPeopleConfirmation { get; set; }
        public int? PromotionMinimumPeople { get; set; }
        public bool PromotionRequiresCode { get; set; }
        public string PromotionCode { get; set; } = string.Empty;
        public string PromotionNote { get; set; } = string.Empty;

        /// <summary>
        /// سعر الإقامة قبل العرض لوحدة واحدة طوال الفترة.
        /// </summary>
        public decimal OriginalStayPrice { get; set; }
        public decimal OriginalStayPriceDisplay { get; set; }
        public string OriginalStayPriceText { get; set; } = string.Empty;

        /// <summary>
        /// خصم تقديري لوحدة واحدة طوال الفترة؛ لا يعد اعتمادًا ماليًا نهائيًا.
        /// </summary>
        public decimal PromotionDiscountAmount { get; set; }
        public decimal PromotionDiscountAmountDisplay { get; set; }
        public string PromotionDiscountAmountText { get; set; } = string.Empty;

        public decimal DiscountedStayPrice { get; set; }
        public decimal DiscountedStayPriceDisplay { get; set; }
        public string DiscountedStayPriceText { get; set; } = string.Empty;

        public decimal DiscountedNightlyPrice { get; set; }
        public decimal DiscountedNightlyPriceDisplay { get; set; }
        public string DiscountedNightlyPriceText { get; set; } = string.Empty;

        public bool HasPromotionOffer =>
            PromotionId.HasValue &&
            PromotionId.Value > 0 &&
            IsPromotionApplicable;

        public bool HasPromotionPreview =>
            HasPromotionOffer &&
            PromotionDiscountAmount > 0 &&
            DiscountedStayPrice >= 0 &&
            DiscountedStayPrice < StayPrice;

        public decimal EffectiveStayPrice =>
            HasPromotionPreview
                ? DiscountedStayPrice
                : StayPrice;

        public decimal EffectiveStayPriceDisplay =>
            HasPromotionPreview
                ? DiscountedStayPriceDisplay
                : StayPriceDisplay;

        public string EffectiveStayPriceText =>
            HasPromotionPreview
                ? DiscountedStayPriceText
                : StayPriceText;

        // ================================================================
        // الكميات
        // ================================================================

        /// <summary>
        /// إجمالي وحدات الغرف الفعلية النشطة المرتبطة بهذا النوع.
        /// لا تعتمد هذه القيمة على Rooms.Quantity.
        /// </summary>
        public int TotalQuantity { get; set; }

        /// <summary>
        /// عدد الوحدات الفعلية المحجوزة خلال الفترة المختارة.
        /// تشمل الربط الجديد، مع توافق آمن للحجوزات القديمة غير المربوطة.
        /// </summary>
        public int ReservedQuantity { get; set; }

        /// <summary>
        /// الكمية التي ما زالت متاحة للحجز.
        /// </summary>
        public int RemainingQuantity { get; set; }

        // ================================================================
        // المواصفات والسعة
        // ================================================================

        public int? Capacity { get; set; }

        public decimal? AreaSquareMeters { get; set; }

        public int MaxAdults { get; set; }

        public int MaxChildren { get; set; }

        public int? BedsCount { get; set; }

        public string BedType { get; set; } = string.Empty;

        /// <summary>
        /// عدد الطوابق التي توجد عليها وحدات نشطة من هذا النوع.
        /// يبقى للتوافق مع بقية الكود، بينما العرض يستخدم أسماء/أرقام الطوابق الفعلية.
        /// </summary>
        public int FloorsCount { get; set; }

        /// <summary>
        /// أسماء/أرقام الطوابق الفعلية التي توجد عليها وحدات نشطة من هذا النوع.
        /// مثال: الدور الأرضي، الطابق 2.
        /// </summary>
        public List<string> FloorDisplayNames { get; set; } = new();

        public string FloorsDisplayText =>
            string.Join(
                "، ",
                FloorDisplayNames
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value)));

        public bool HasCapacity =>
            Capacity.HasValue &&
            Capacity.Value > 0;

        public bool HasArea =>
            AreaSquareMeters.HasValue &&
            AreaSquareMeters.Value > 0;

        public bool HasAdultsCapacity =>
            MaxAdults > 0;

        public bool HasChildrenCapacity =>
            MaxChildren > 0;

        public bool HasBeds =>
            BedsCount.HasValue &&
            BedsCount.Value > 0;

        public bool HasBedType =>
            !string.IsNullOrWhiteSpace(BedType);

        public bool HasFloorDistribution =>
            FloorDisplayNames.Any(value =>
                !string.IsNullOrWhiteSpace(value));

        public bool HasSpecifications =>
            HasArea ||
            HasAdultsCapacity ||
            HasChildrenCapacity ||
            HasBeds ||
            HasBedType ||
            HasFloorDistribution;

        // ================================================================
        // الصور
        // ================================================================

        /// <summary>
        /// الصور الحقيقية الموجودة داخل Rooms.ImageUrl.
        /// يدعم صورة واحدة، قائمة مفصولة بفواصل، أو JSON Array.
        /// </summary>
        public List<string> Images { get; set; } = new();

        /// <summary>
        /// الصورة التي تظهر في بطاقة الغرفة.
        /// عند عدم وجود صورة للغرفة تُستخدم صورة المنشأة كصورة بديلة.
        /// </summary>
        public string MainImageUrl { get; set; } = string.Empty;

        public bool HasRoomImages =>
            Images.Count > 0;

        public bool HasMultipleRoomImages =>
            Images.Count > 1;

        public int ImagesCount =>
            Images.Count;

        public bool IsSuite =>
            RoomType.Contains(
                "جناح",
                StringComparison.OrdinalIgnoreCase);
    }
}
