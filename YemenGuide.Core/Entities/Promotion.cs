using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace YemenGuide.Core.Entities
{
    public partial class Promotion
    {
        public Promotion()
        {
            Advertisements = new HashSet<Advertisement>();
            Bookings = new HashSet<Booking>();
            Reports = new HashSet<Report>();
        }

        public int PromotionId { get; set; }
        public int OwnerId { get; set; }
        public int PlaceId { get; set; }
        public string PromotionTitle { get; set; } = null!;
        public string? DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public string? PromoCode { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public decimal? MinimumBookingAmount { get; set; }
        public int? MinimumNights { get; set; }
        public int? MinimumPeople { get; set; }
        public int? TotalUsageLimit { get; set; }
        public int? PerCustomerUsageLimit { get; set; }
        public bool? IsAutomatic { get; set; }
        public bool IsStackable { get; set; }
        public int? RoomId { get; set; }
        public int? PackageId { get; set; }
        public int? ServiceId { get; set; }
        public DateTime? StayStartDate { get; set; }
        public DateTime? StayEndDate { get; set; }
        public string? ApplicableDaysCsv { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? PausedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string OfferMode { get; set; } = null!;
        public string? RewardType { get; set; }
        public int? RequiredQuantity { get; set; }
        public int? RewardQuantity { get; set; }

        // الحقل القديم لا يُحذف. أول خدمة من الاختيار المتعدد تُحفظ فيه للتوافق.
        public int? RewardServiceId { get; set; }

        // المرحلة 5/6: Snapshot متعدد للخدمات (المعرف + الاسم + السعر الأصلي وقت إنشاء العرض).
        // RewardServiceId القديم يبقى كما هو للتوافق مع العروض القديمة.
        [Column("RewardServicesJson")]
        public string? RewardServicesJson { get; set; }

        public decimal? SpecialPrice { get; set; }

        public bool IsRewardRepeatable { get; set; }

        public virtual User Owner { get; set; } = null!;
        public virtual PlacePackage? P { get; set; }
        public virtual Place Place { get; set; } = null!;
        public virtual PlaceAdditionalService? PlaceAdditionalService { get; set; }
        public virtual PlaceAdditionalService? PlaceAdditionalServiceNavigation { get; set; }
        public virtual User? ReviewedByUser { get; set; }
        public virtual Room? Room { get; set; }
        public virtual ICollection<Advertisement> Advertisements { get; set; }
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<Report> Reports { get; set; }
    }
}
