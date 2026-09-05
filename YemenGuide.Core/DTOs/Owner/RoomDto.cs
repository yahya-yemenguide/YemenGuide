using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Owner
{
    public class RoomDto
    {
        public int RoomID { get; set; }

        [Required(ErrorMessage = "يرجى تحديد المنشأة")]
        public int PlaceID { get; set; }

        [Required(ErrorMessage = "يرجى إدخال اسم عرض النوع")]
        [StringLength(50, ErrorMessage = "اسم العرض لا يتجاوز 50 حرفًا")]
        public string RoomNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى تحديد نوع الوحدة")]
        [StringLength(50, ErrorMessage = "اسم نوع الوحدة لا يتجاوز 50 حرفًا")]
        public string RoomType { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى تحديد سعر الليلة")]
        // التحقق العددي يتم في الكنترولر لتجنب تعارض RangeAttribute مع ثقافة الخادم.
        public decimal Price { get; set; }

        public decimal? AreaSquareMeters { get; set; }

        [Range(1, 100, ErrorMessage = "عدد البالغين يجب أن يكون بين 1 و100")]
        public int MaxAdults { get; set; } = 1;

        [Range(0, 100, ErrorMessage = "عدد الأطفال يجب أن يكون بين 0 و100")]
        public int MaxChildren { get; set; }

        [Range(0, 50, ErrorMessage = "عدد الأسرّة يجب أن يكون بين 0 و50")]
        public int? BedsCount { get; set; }

        [StringLength(50, ErrorMessage = "نوع السرير لا يتجاوز 50 حرفًا")]
        public string? BedType { get; set; }

        // يكون فارغًا للغرف العادية، ويحتوي عدد الغرف الداخلية عند كون النوع جناحًا.
        [Range(1, 100, ErrorMessage = "عدد الغرف الداخلية للجناح يجب أن يكون بين 1 و100")]
        public int? InternalRoomsCount { get; set; }

        [StringLength(500, ErrorMessage = "الوصف لا يتجاوز 500 حرف")]
        public string? Description { get; set; }

        public bool IsAvailable { get; set; } = true;

        // الحقل القديم محفوظ لعدم كسر أي فورم حالي.
        public IFormFile? Image { get; set; }

        // الصور المتعددة المستخدمة في مدير تفاصيل المنشأة.
        public List<IFormFile> Images { get; set; } = new();

        [Required(ErrorMessage = "يرجى تحديد العدد المتوفر")]
        [Range(1, int.MaxValue, ErrorMessage = "يجب أن يكون العدد المتوفر واحدًا على الأقل")]
        public int Quantity { get; set; }

        public List<RoomUnitDto> Units { get; set; } = new();
    }

    public class RoomUnitDto
    {
        public int RoomUnitID { get; set; }

        [Required(ErrorMessage = "يرجى تحديد المنشأة")]
        public int PlaceID { get; set; }

        [Required(ErrorMessage = "يرجى تحديد نوع الغرفة")]
        public int RoomID { get; set; }

        [Required(ErrorMessage = "يرجى إدخال رقم الغرفة")]
        [StringLength(50, ErrorMessage = "رقم الغرفة لا يتجاوز 50 حرفًا")]
        public string RoomNumber { get; set; } = string.Empty;

        [Range(-100, 1000, ErrorMessage = "رقم الطابق غير صالح")]
        public int FloorNumber { get; set; }

        [StringLength(50, ErrorMessage = "اسم الطابق لا يتجاوز 50 حرفًا")]
        public string? FloorLabel { get; set; }

        public bool IsAvailable { get; set; } = true;

        [StringLength(250, ErrorMessage = "الملاحظات لا تتجاوز 250 حرفًا")]
        public string? Notes { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
