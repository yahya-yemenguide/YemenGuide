using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Owner
{
    public class PromotionManagementDto
    {
        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string? PlaceImage { get; set; }
        public int BookingSystemId { get; set; }

        public bool HasActivePlace => PlaceId > 0;
        public bool IsBookable => BookingSystemId >= 1 && BookingSystemId <= 4;
        public bool IsHotel => BookingSystemId == 1;
        public bool IsChalet => BookingSystemId == 2;
        public bool IsHall => BookingSystemId == 3 || BookingSystemId == 4;
        public bool IsNonBooking => !IsBookable;

        public bool CanTargetRooms => IsHotel && Rooms.Count > 0;
        public bool CanTargetPackages => IsBookable && Packages.Count > 0;
        public bool CanTargetServices => IsBookable && Services.Count > 0;
        public bool SupportsStayRange => IsHotel || IsChalet;
        public bool SupportsMinimumNights => IsHotel;
        public bool SupportsMinimumPeople => IsBookable;
        public bool SupportsBookingAmount => IsBookable;
        public bool SupportsPromoCode => IsBookable;
        public bool SupportsUsageLimits => IsBookable;
        public bool SupportsStacking => IsBookable;

        public string PlaceKindLabel
        {
            get
            {
                if (IsHotel) return "فندق";
                if (IsChalet) return "شاليه / مزرعة";
                if (IsHall) return "قاعة / صالة";
                return "منشأة غير حجزية";
            }
        }

        public int ActiveCount { get; set; }
        public int ScheduledCount { get; set; }
        public int DraftCount { get; set; }
        public int ArchivedCount { get; set; }

        public List<PromotionCardDto> Promotions { get; set; } = new();
        public List<PromotionTargetOptionDto> Rooms { get; set; } = new();
        public List<PromotionTargetOptionDto> Packages { get; set; } = new();
        public List<PromotionTargetOptionDto> Services { get; set; } = new();
    }

    public class PromotionCardDto
    {
        public int PromotionId { get; set; }
        public string PromotionTitle { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public string? ImagePath { get; set; }

        public string OfferMode { get; set; } = "Discount";
        public string? RewardType { get; set; }
        public int? RequiredQuantity { get; set; }
        public int? RewardQuantity { get; set; }
        public bool IsRewardRepeatable { get; set; }

        // الحقل القديم يبقى محفوظًا للتوافق مع العروض السابقة.
        public int? RewardServiceId { get; set; }

        // المرحلة 5: الاختيارات المتعددة. للعروض القديمة تُملأ تلقائيًا من RewardServiceId.
        public List<int> RewardServiceIds { get; set; } = new();

        public decimal? SpecialPrice { get; set; }
        public decimal? BundleOriginalPrice { get; set; }
        public decimal? BundleFinalPrice { get; set; }
        public decimal? BundleSavingsAmount { get; set; }
        public string OfferSummary { get; set; } = string.Empty;

        public string? DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public decimal? MinimumBookingAmount { get; set; }
        public int? MinimumNights { get; set; }
        public int? MinimumPeople { get; set; }
        public int? TotalUsageLimit { get; set; }
        public int? PerCustomerUsageLimit { get; set; }

        public bool IsAutomatic { get; set; }
        public string? PromoCode { get; set; }
        public bool IsStackable { get; set; }

        public int? RoomId { get; set; }
        public int? PackageId { get; set; }
        public int? ServiceId { get; set; }
        public string TargetType { get; set; } = "Place";
        public string TargetLabel { get; set; } = "المنشأة كاملة";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? StayStartDate { get; set; }
        public DateTime? StayEndDate { get; set; }
        public string? ApplicableDaysCsv { get; set; }

        public string Status { get; set; } = "Draft";
        public string EffectiveStatus { get; set; } = "Draft";
        public string StatusLabel { get; set; } = "مسودة";
        public bool IsEnabled { get; set; }
        public bool HasBookings { get; set; }
        public int UsedCount { get; set; }
    }

    public class PromotionTargetOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Meta { get; set; }
        public decimal Price { get; set; }
    }

    public class PromotionUpsertDto
    {
        public int PromotionId { get; set; }

        [Required, StringLength(150, MinimumLength = 3)]
        public string PromotionTitle { get; set; } = string.Empty;

        [StringLength(250)]
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string OfferMode { get; set; } = "Discount";

        [StringLength(30)]
        public string? RewardType { get; set; }

        [Range(1, int.MaxValue)]
        public int? RequiredQuantity { get; set; }

        [Range(1, int.MaxValue)]
        public int? RewardQuantity { get; set; }

        public bool IsRewardRepeatable { get; set; }

        // Legacy single-service field remains intact.
        public int? RewardServiceId { get; set; }
        public List<int> RewardServiceIds { get; set; } = new();

        // Server-computed snapshot JSON; it is always rebuilt from DB before saving.
        public string? RewardServicesJson { get; set; }

        public decimal? SpecialPrice { get; set; }

        // Computed values for validation/summary only; they are not extra DB columns.
        public decimal? BundleOriginalPrice { get; set; }
        public decimal? BundleFinalPrice { get; set; }
        public decimal? BundleSavingsAmount { get; set; }

        [StringLength(20)]
        public string? DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public decimal? MinimumBookingAmount { get; set; }
        public int? MinimumNights { get; set; }
        public int? MinimumPeople { get; set; }
        public int? TotalUsageLimit { get; set; }
        public int? PerCustomerUsageLimit { get; set; }

        public bool IsAutomatic { get; set; } = true;

        [StringLength(50)]
        public string? PromoCode { get; set; }

        public bool IsStackable { get; set; }
        public string TargetType { get; set; } = "Place";
        public int? RoomId { get; set; }
        public int? PackageId { get; set; }
        public int? ServiceId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? StayStartDate { get; set; }
        public DateTime? StayEndDate { get; set; }

        [StringLength(100)]
        public string? ApplicableDaysCsv { get; set; }

        public bool RemoveImage { get; set; }
        public string SaveMode { get; set; } = "Draft";
    }

    public class PromotionStatusRequestDto
    {
        public int PromotionId { get; set; }
        public bool IsActive { get; set; }
    }

    public class PromotionDeleteRequestDto
    {
        public int PromotionId { get; set; }
    }
}
