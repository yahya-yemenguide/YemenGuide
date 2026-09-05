using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Customer
{
    public class PlaceSearchViewModel
    {
        public string? SearchQuery { get; set; }
        public int? SelectedCategoryId { get; set; }
        public int? SelectedCityId { get; set; }
        public List<string> SortOptions { get; set; } = new();

        // فلاتر البحث الحقيقية.
        public bool OnlyWithOffers { get; set; }
        public bool OnlyBookable { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 12;
        public int TotalItems { get; set; }

        // السلايدر الإعلاني الموحد أعلى صفحة البحث.
        // الحد النهائي يطبقه CustomerAdvertisementService (حتى 5 حملات مؤهلة).
        // لا توجد أماكن ظهور متعددة في واجهة العميل.
        public List<CustomerAdvertisementDto> Advertisements { get; set; } = new();

        // للتوافق مع أي كود قديم ما زال يتوقع إعلانًا واحدًا.
        public CustomerAdvertisementDto? Advertisement { get; set; }

        public List<PlaceSearchResultDto> Results { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<CityDto> Cities { get; set; } = new();
    }

    public class PlaceSearchResultDto
    {
        public int PlaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? CityName { get; set; }
        public string? Address { get; set; }
        // سعر الفرز الحقيقي بحسب نظام الحجز:
        // الفندق = أقل غرفة متاحة، القاعة/الشاليه = أقل باقة فعالة،
        // وغير الحجزي = سعر المنشأة إن وُجد.
        public decimal Price { get; set; }
        public decimal DisplayPrice { get; set; }
        public string DisplayPriceText { get; set; } = string.Empty;
        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string DisplayCurrencyCode { get; set; } = "YER";
        public string DisplayCurrencySymbol { get; set; } = "ر.ي";
        public string ExchangeRateRegion { get; set; } = string.Empty;
        public bool HasSearchPrice { get; set; }
        public bool HasBooking { get; set; }
        public string? MainImage { get; set; }
        public List<string> GalleryImages { get; set; } = new();

        // التقييم والمفضلة.
        public double Rating { get; set; }
        public int ReviewsCount { get; set; }
        public bool IsFavorited { get; set; }

        // العرض الفعّال المختار للبطاقة.
        public bool HasActivePromotion { get; set; }
        public int? ActivePromotionId { get; set; }
        public string? ActivePromotionTitle { get; set; }
        public string? PromotionTargetLabel { get; set; }
        // الاسم القديم يبقى للتوافق، والنص الجديد يستخدم لأي نوع عرض وليس الخصم فقط.
        public string? PromotionDiscountText { get; set; }
        public string? PromotionOfferText { get; set; }
        public DateTime? PromotionEndDate { get; set; }
    }
}
