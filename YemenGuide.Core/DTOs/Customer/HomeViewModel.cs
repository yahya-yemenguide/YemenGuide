using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Customer
{
    public class HomeViewModel
    {
        public List<FeaturedPlaceDto> FeaturedPlaces { get; set; } = new();
        public List<HomePromotionDto> Promotions { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<CityDto> Cities { get; set; } = new();

        // ✅ إضافة خاصية شرائح السلايدر
        public List<CustomerHeroSlideDto> HeroSlides { get; set; } = new();

        // ==========================================
        // 📊 1. خصائص الإحصائيات (جديد)
        // ==========================================
        public int TotalTrustedPlaces { get; set; }
        public int TotalSuccessfulBookings { get; set; }
        public int TotalHappyCustomers { get; set; }
        public int TotalCoveredCities { get; set; }

        // ==========================================
        // 💱 2. خصائص أسعار الصرف (جديد)
        // ==========================================
        public decimal SanaaUsdRate { get; set; }
        public decimal SanaaSarRate { get; set; }
        public decimal AdenUsdRate { get; set; }
        public decimal AdenSarRate { get; set; }

        // ==========================================
        // 💱 3. عملة العرض الفعلية للمستخدم الحالي
        // ==========================================
        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string DisplayCurrencyCode { get; set; } = "YER";
        public string DisplayCurrencySymbol { get; set; } = "ر.ي";
    }

    public class HomePromotionDto
    {
        public int PromotionId { get; set; }
        public int PlaceId { get; set; }

        public string PromotionTitle { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? ImagePath { get; set; }

        public string PlaceName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;

        // منطق العرض الموحد الجديد.
        public string OfferMode { get; set; } = "Discount";
        public string RewardType { get; set; } = string.Empty;
        public int? RequiredQuantity { get; set; }
        public int? RewardQuantity { get; set; }
        public bool IsRewardRepeatable { get; set; }
        public int? RewardServiceId { get; set; }
        public string RewardServiceName { get; set; } = string.Empty;
        public decimal? SpecialPrice { get; set; }
        public string OfferSummary { get; set; } = string.Empty;
        public string OfferBadgeCaption { get; set; } = "عرض";
        public string OfferBadgeValue { get; set; } = string.Empty;

        // حقول الخصم القديمة تبقى للتوافق مع العروض المباشرة.
        public string DiscountType { get; set; } = "Percentage";
        public decimal DiscountValue { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public decimal? MinimumBookingAmount { get; set; }
        public int? MinimumNights { get; set; }
        public int? MinimumPeople { get; set; }

        public bool IsAutomatic { get; set; }

        public string TargetType { get; set; } = "Place";
        public string TargetLabel { get; set; } = "المنشأة كاملة";
        public string? TargetName { get; set; }

        public DateTime EndDate { get; set; }
        public int DaysRemaining { get; set; }

        public decimal? OriginalTargetPrice { get; set; }
        public decimal? DiscountedTargetPrice { get; set; }

        // قيم العرض فقط حسب عملة المستخدم وسوق مدينة المنشأة.
        // القيم الأصلية أعلاه تظل بالريال اليمني للحسابات.
        public string? OriginalTargetPriceText { get; set; }
        public string? DiscountedTargetPriceText { get; set; }
        public string? DiscountValueText { get; set; }
        public string? MaximumDiscountAmountText { get; set; }
        public string? MinimumBookingAmountText { get; set; }

        public bool HasDeterministicPrice =>
            OriginalTargetPrice.HasValue &&
            DiscountedTargetPrice.HasValue;

        public bool EndsToday => DaysRemaining == 0;
        public bool EndsSoon => DaysRemaining > 0 && DaysRemaining <= 3;
    }

    public class FeaturedPlaceDto
    {
        public int PlaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? CityName { get; set; }
        public string? Address { get; set; }
        public decimal Price { get; set; }

        // سعر عرض جاهز حسب عملة المستخدم وسوق المدينة.
        // لا يغيّر Price الأصلي المخزن بالريال اليمني.
        public string? DisplayPriceText { get; set; }

        public string? MainImage { get; set; }
        public bool HasBooking { get; set; }

        // ✅ إضافة قائمة للصور الإضافية لشبكة الـ 4 صور في البطاقة
        public List<string> GalleryImages { get; set; } = new();

        // ==========================================
        // 💎 خصائص البطاقة الديناميكية الجديدة 💎
        // ==========================================
        public double AverageRating { get; set; } // متوسط التقييم (مثال: 4.8)
        public int ReviewCount { get; set; }      // عدد التقييمات (مثال: 283)
        public bool IsFavorited { get; set; }     // حالة زر المفضلة (القلب) للمستخدم الحالي

        // 🎁 إذا كانت المنشأة المميزة لديها عرض فعّال الآن
        public bool HasActivePromotion { get; set; }
        public int? ActivePromotionId { get; set; }
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImagePath { get; set; }

        // ✅ إضافة الأيقونة واللون لنجلبها من قاعدة البيانات لصندوق البحث
        public string? IconClass { get; set; }
        public string? ThemeColor { get; set; }
    }

    public class CityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // كلاس التواصل
    public class ContactMessageDto
    {
        public string SenderName { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
    }

    // ✅ كلاس شرائح السلايدر الخاص بالعميل (تم تحديث المسميات لتتطابق مع قاعدة البيانات)
    public class CustomerHeroSlideDto
    {
        public int HeroSlideId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string TypingText { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        // ✨ الخصائص الجديدة لكي يقرأها الجافاسكريبت في الواجهة الرئيسية ويطبق التناغم اللوني
        public string MainTextColor { get; set; } = "#ffffff";
        public string AccentColor { get; set; } = "#ff5a5f";
    }
}
