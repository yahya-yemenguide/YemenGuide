using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Customer
{
    /// <summary>
    /// أسماء أماكن ظهور الإعلان المستخدمة في واجهة العميل.
    /// تبقى مطابقة لقيم قاعدة البيانات وخيارات المالك/الإدارة.
    /// </summary>
    public static class CustomerAdvertisementPlacements
    {
        public const string HomeSlider = "HomeSlider";
        public const string FeaturedPlaces = "FeaturedPlaces";
        public const string SearchTop = "SearchTop";
        public const string CategoryTop = "CategoryTop";
        public const string CityPage = "CityPage";
        public const string HomeBanner = "HomeBanner";
        public const string MapPin = "MapPin";
        public const string PlaceDetails = "PlaceDetails";
    }

    public static class CustomerAdvertisementEventTypes
    {
        public const string Impression = "Impression";
        public const string Click = "Click";
        public const string BookingStarted = "BookingStarted";
        public const string BookingCompleted = "BookingCompleted";
    }

    /// <summary>
    /// نموذج إعلان جاهز للعرض في واجهات العميل.
    /// لا يمرر بيانات مالية أو إدارية لا يحتاجها المتصفح.
    /// </summary>
    public sealed class CustomerAdvertisementDto
    {
        public int AdvertisementId { get; set; }
        public int PlaceId { get; set; }
        public int? PromotionId { get; set; }

        public string PlacementType { get; set; } = string.Empty;
        public string AdvertisementTitle { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string ButtonText { get; set; } = "عرض المنشأة";

        public string PlaceName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;

        public string MediaType { get; set; } = "Image";
        public string MediaPath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }

        public string DestinationType { get; set; } = "Place";
        public string FallbackUrl { get; set; } = string.Empty;

        public bool IsVideo =>
            string.Equals(MediaType, "Video", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// الصفحة الرئيسية لا تستقبل أكثر من إعلان رئيسي واحد
    /// (Slider أو Banner) وإعلان ثانوي واحد داخل الأماكن المميزة.
    /// </summary>
    public sealed class CustomerHomeAdvertisementsDto
    {
        public CustomerAdvertisementDto? PrimaryAdvertisement { get; set; }
        public CustomerAdvertisementDto? FeaturedAdvertisement { get; set; }

        public bool HasAny =>
            PrimaryAdvertisement != null ||
            FeaturedAdvertisement != null;

        public bool HasHomeSlider =>
            string.Equals(
                PrimaryAdvertisement?.PlacementType,
                CustomerAdvertisementPlacements.HomeSlider,
                StringComparison.OrdinalIgnoreCase);

        public bool HasHomeBanner =>
            string.Equals(
                PrimaryAdvertisement?.PlacementType,
                CustomerAdvertisementPlacements.HomeBanner,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// سياق صفحة الاستكشاف. الإعلان الممول لا يظهر إلا إذا كانت منشأته
    /// ضمن نتائج البحث الحقيقية بعد تطبيق البحث والفلاتر الحالية.
    /// </summary>
    public sealed class CustomerAdvertisementDiscoveryRequestDto
    {
        public string? SearchQuery { get; set; }
        public int? CategoryId { get; set; }
        public int? CityId { get; set; }
        public List<int> EligiblePlaceIds { get; set; } = new();
    }


    /// <summary>
    /// سياق الإعلان الجانبي داخل صفحة تفاصيل منشأة.
    /// الإعلان نفسه لا يكون للمنشأة المفتوحة حتى لا يتكرر المحتوى أمام العميل.
    /// </summary>
    public sealed class CustomerAdvertisementPlaceDetailsRequestDto
    {
        public int CurrentPlaceId { get; set; }
        public int CurrentCityId { get; set; }
        public int CurrentCategoryId { get; set; }
    }

    /// <summary>
    /// سياق إعلانات الخريطة. EligiblePlaceIds هي فقط المنشآت التي اجتازت
    /// النطاق الجغرافي وفلاتر قريب منك قبل الوصول إلى محرك الإعلانات.
    /// </summary>
    public sealed class CustomerAdvertisementMapRequestDto
    {
        public double UserLatitude { get; set; }
        public double UserLongitude { get; set; }
        public int? CategoryId { get; set; }
        public int MaxAdvertisements { get; set; } = 2;
        public List<int> EligiblePlaceIds { get; set; } = new();
    }

    /// <summary>
    /// تسجيل بداية رحلة حجز جاءت من إعلان سبق أن ضغط عليه نفس الزائر/المستخدم.
    /// </summary>
    public sealed class CustomerAdvertisementBookingStartRequestDto
    {
        public int AdvertisementId { get; set; }
        public int PlaceId { get; set; }
        public string? RequestKey { get; set; }
    }

    /// <summary>
    /// الطلب الصغير الذي يرسله المتصفح لتسجيل ظهور أو نقرة.
    /// PlacementType يُعاد التحقق منه في الخادم ولا يُوثق به وحده.
    /// </summary>
    public sealed class CustomerAdvertisementTrackRequestDto
    {
        public int AdvertisementId { get; set; }
        public string PlacementType { get; set; } = string.Empty;
        public string? RequestKey { get; set; }
    }

    public sealed class CustomerAdvertisementTrackResultDto
    {
        public bool Success { get; set; }
        public bool AlreadyRecorded { get; set; }
        public string? Message { get; set; }
        public string? RedirectUrl { get; set; }

        public static CustomerAdvertisementTrackResultDto Ok(
            string? redirectUrl = null,
            bool alreadyRecorded = false) =>
            new CustomerAdvertisementTrackResultDto
            {
                Success = true,
                RedirectUrl = redirectUrl,
                AlreadyRecorded = alreadyRecorded
            };

        public static CustomerAdvertisementTrackResultDto Fail(
            string message,
            string? redirectUrl = null) =>
            new CustomerAdvertisementTrackResultDto
            {
                Success = false,
                Message = message,
                RedirectUrl = redirectUrl
            };
    }
}
