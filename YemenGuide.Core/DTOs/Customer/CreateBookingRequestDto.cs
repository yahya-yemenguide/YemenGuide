using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Customer
{
    public class CreateBookingRequestDto
    {
        /// <summary>
        /// يرسل المتصفح نوع الغرفة والكمية فقط.
        /// أرقام RoomUnits لا تُقبل من العميل؛ الخادم هو الذي يختارها
        /// داخل معاملة قاعدة البيانات لمنع التلاعب والحجز المزدوج.
        /// </summary>
        [Required]
        public int PlaceId { get; set; }

        public int? RoomId { get; set; }

        public int? PackageId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        [Range(0, int.MaxValue)]
        public int AdultsCount { get; set; } = 1;

        [Range(0, int.MaxValue)]
        public int ChildrenCount { get; set; }

        [MaxLength(50)]
        public string? BookingType { get; set; }

        [MaxLength(100)]
        public string? SeatingSetup { get; set; }

        /// <summary>
        /// رقم العرض المختار. الخادم يعيد التحقق منه ولا يعتمد عليه مباشرة.
        /// </summary>
        public int? PromotionId { get; set; }

        /// <summary>
        /// كود العرض عند الحاجة.
        /// </summary>
        [MaxLength(50)]
        public string? PromotionCode { get; set; }

        /// <summary>
        /// إعلان اختياري نُسبت إليه رحلة الحجز. لا يثق الخادم بهذه القيمة؛
        /// يعيد التحقق من النقرة والجلسة وتطابق المنشأة قبل حفظها.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int? AdvertisementId { get; set; }

        public List<BookingAdditionalServiceRequestDto>
            AdditionalServices
        { get; set; } = new();
    }

    public class BookingAdditionalServiceRequestDto
    {
        [Required]
        public int ServiceId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
    }
}
