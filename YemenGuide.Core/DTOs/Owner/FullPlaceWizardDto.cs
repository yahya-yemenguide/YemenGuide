using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Owner // تأكدي أن مسار الـ namespace يطابق مشروعك
{
    public class FullPlaceWizardDto
    {
        // --- البيانات العامة ---
        public int CategoryId { get; set; }
        public int CityId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? AddressDetail { get; set; }
        public string? Coordinates { get; set; }
        public string? PhoneNumber { get; set; }
        public decimal BasePrice { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public int? OwnerId { get; set; }
        public string? Status { get; set; }
        public bool? IsAvailableStr { get; set; }

        // ==========================================
        // 🏨 البنية الجديدة لأنواع الغرف والطوابق
        // تظل الخصائص القديمة أدناه مؤقتًا حتى لا ينكسر الفورم الحالي
        // ==========================================
        public List<HotelRoomTypeItemDto> HotelRoomTypes { get; set; } = new();
        public List<HotelFloorItemDto> HotelFloors { get; set; } = new();

        // ==========================================
        // 🏨 بيانات الغرف الفندقية
        // ==========================================
        public bool HasSingleRoom { get; set; }
        public string? SingleRoomName { get; set; }
        public int? SingleRoomQty { get; set; }
        public decimal? SingleRoomPrice { get; set; }
        public string? SingleRoomArea { get; set; }
        public int? SingleRoomMaxPersons { get; set; }
        public string? SingleRoomSpecs { get; set; }

        public bool HasDoubleRoom { get; set; }
        public string? DoubleRoomName { get; set; }
        public int? DoubleRoomQty { get; set; }
        public decimal? DoubleRoomPrice { get; set; }
        public string? DoubleRoomArea { get; set; }
        public int? DoubleRoomMaxPersons { get; set; }
        public string? DoubleRoomSpecs { get; set; }

        public bool HasTwinRoom { get; set; }
        public string? TwinRoomName { get; set; }
        public int? TwinRoomQty { get; set; }
        public decimal? TwinRoomPrice { get; set; }
        public string? TwinRoomArea { get; set; }
        public int? TwinRoomMaxPersons { get; set; }

        public bool HasTripleRoom { get; set; }
        public string? TripleRoomName { get; set; }
        public int? TripleRoomQty { get; set; }
        public decimal? TripleRoomPrice { get; set; }
        public string? TripleRoomArea { get; set; }
        public int? TripleRoomMaxPersons { get; set; }

        public bool HasStudio { get; set; }
        public string? StudioName { get; set; }
        public int? StudioQty { get; set; }
        public decimal? StudioPrice { get; set; }
        public string? StudioArea { get; set; }
        public int? StudioMaxPersons { get; set; }

        // ==========================================
        // 💎 بيانات الأجنحة
        // ==========================================
        public bool HasRoyalSuite { get; set; }
        public string? RoyalSuiteName { get; set; }
        public int? RoyalSuiteQty { get; set; }
        public decimal? RoyalSuitePrice { get; set; }
        public string? RoyalSuiteArea { get; set; }
        public int? RoyalSuiteMaxPersons { get; set; }
        public int? RoyalSuiteRoomsCount { get; set; }
        public string? RoyalSuiteSpecs { get; set; }

        public bool HasFamilySuite { get; set; }
        public string? FamilySuiteName { get; set; }
        public int? FamilySuiteQty { get; set; }
        public decimal? FamilySuitePrice { get; set; }
        public string? FamilySuiteArea { get; set; }
        public int? FamilySuiteMaxPersons { get; set; }
        public int? FamilySuiteRoomsCount { get; set; }
        public string? FamilySuiteSpecs { get; set; }

        public bool HasExecutiveSuite { get; set; }
        public string? ExecutiveSuiteName { get; set; }
        public int? ExecutiveSuiteQty { get; set; }
        public decimal? ExecutiveSuitePrice { get; set; }
        public string? ExecutiveSuiteArea { get; set; }
        public int? ExecutiveSuiteMaxPersons { get; set; }

        // ==========================================
        // 🏢 المرافق الفندقية الإضافية (شاليهات / قاعات / طيرمانات تابعة لفندق)
        // ==========================================
        public bool HasHotelChalet { get; set; }
        public int? HotelChaletQty { get; set; }
        public decimal? HotelChaletPrice { get; set; }
        public string? HotelChaletArea { get; set; }
        public int? HotelChaletMaxPersons { get; set; }

        public bool HasHotelHall { get; set; }
        public int? HotelHallQty { get; set; }
        public decimal? HotelHallPrice { get; set; }
        public string? HotelHallArea { get; set; }
        public int? HotelHallMaxPersons { get; set; }

        public bool HasTairamana { get; set; }
        public int? TairamanaQty { get; set; }
        public decimal? TairamanaPrice { get; set; }
        public string? TairamanaArea { get; set; }
        public int? TairamanaMaxPersons { get; set; }

        public int? ChaletsCount { get; set; }
        public int? TermanCount { get; set; }
        public int? HallsCount { get; set; }
        public int? SuitesCount { get; set; }

        // ==========================================
        // ✨ تفاصيل القاعات والصالات (المستقلة)
        // ==========================================
        public string? HallArea { get; set; }
        public int? Capacity { get; set; }
        public int? HallTablesCapacity { get; set; }
        public List<string>? EventTypes { get; set; }
        public List<string>? SeatingTypes { get; set; }
        public int? HallBathroomsCount { get; set; }
        public int? ACsCount { get; set; }
        public int? StorageRoomsCount { get; set; }
        public int? PrivateRoomsCount { get; set; }
        public int? KitchensCount { get; set; }
        public int? PrayerRoomsCount { get; set; }
        public int? SpeakersCount { get; set; }
        public int? ParkingCount { get; set; }
        public string? CustomHallFacilities { get; set; }

        // ==========================================
        // 🌴 تفاصيل الشاليهات والمزارع (المستقلة)
        // ==========================================
        public int? RoomsCount { get; set; }
        public int? BathroomsCount { get; set; }
        public int? MajlisCount { get; set; }
        public int? ReceptionHallsCount { get; set; }

        public List<string>? ChaletEntertainment { get; set; }
        public string? CustomChaletEntertainment { get; set; }

        public List<string>? ChaletServices { get; set; }
        public string? CustomChaletServices { get; set; }

        public List<string>? ChaletHVAC { get; set; }
        public string? CustomChaletHVAC { get; set; }

        public List<string>? ChaletYard { get; set; }
        public string? CustomChaletYard { get; set; }

        // ==========================================
        // ⚖️ السياسات (إن وجدت)
        // ==========================================
        public string? DepositPolicy { get; set; }
        public string? InsurancePolicy { get; set; }
        public string? CancellationPolicy { get; set; }
        public List<string>? TermsList { get; set; }

        // ==========================================
        // 📦 القوائم الديناميكية
        // ==========================================
        public List<ServiceItemDto>? Services { get; set; }
        public List<AmenityItemDto>? Amenities { get; set; }
        public List<SpaceItemDto>? Spaces { get; set; }
        public List<PackageItemDto>? Packages { get; set; }


        // ==========================================
        // 🏨 أنواع الغرف الفندقية الجديدة
        // ==========================================
        public class HotelRoomTypeItemDto
        {
            [Required(ErrorMessage = "معرّف نوع الغرفة مفقود")]
            [StringLength(60)]
            public string ClientKey { get; set; } = string.Empty;

            [Required(ErrorMessage = "يرجى تحديد نوع الغرفة")]
            [StringLength(50, ErrorMessage = "اسم نوع الغرفة لا يتجاوز 50 حرفًا")]
            public string RoomType { get; set; } = string.Empty;

            [StringLength(50, ErrorMessage = "اسم العرض لا يتجاوز 50 حرفًا")]
            public string? DisplayName { get; set; }

            // يحدد شكل الحقول في الفورم فقط. في قاعدة البيانات يُعرف الجناح
            // من وجود عدد الغرف الداخلية InternalRoomsCount.
            public bool IsSuite { get; set; }

            [Range(1, 100, ErrorMessage = "عدد الغرف الداخلية للجناح يجب أن يكون بين 1 و100")]
            public int? InternalRoomsCount { get; set; }

            // لا نستخدم RangeAttribute مع decimal هنا لأنه قد يفشل عند اختلاف
            // ثقافة الخادم. يتم التحقق من السعر في JavaScript والـ Controller.
            public decimal Price { get; set; }

            // التحقق من كون المساحة أكبر من صفر موجود في الـ Controller.
            public decimal? AreaSquareMeters { get; set; }

            [Range(1, 100, ErrorMessage = "عدد البالغين يجب أن يكون بين 1 و100")]
            public int MaxAdults { get; set; } = 1;

            [Range(0, 100, ErrorMessage = "عدد الأطفال يجب أن يكون بين 0 و100")]
            public int MaxChildren { get; set; }

            [Range(0, 50, ErrorMessage = "عدد الأسرّة يجب أن يكون بين 0 و50")]
            public int? BedsCount { get; set; }

            [StringLength(50, ErrorMessage = "نوع السرير لا يتجاوز 50 حرفًا")]
            public string? BedType { get; set; }

            [StringLength(500, ErrorMessage = "الوصف لا يتجاوز 500 حرف")]
            public string? Description { get; set; }

            public bool IsAvailable { get; set; } = true;

            public List<IFormFile> Images { get; set; } = new();
        }

        // ==========================================
        // 🏢 الطوابق وما بداخلها من وحدات فعلية (غرف أو أجنحة)
        // FloorNumber: السالب للبدروم، صفر للأرضي، والموجب للأدوار العليا
        // ==========================================
        public class HotelFloorItemDto
        {
            [Range(-100, 1000, ErrorMessage = "رقم الطابق غير صالح")]
            public int FloorNumber { get; set; }

            [StringLength(50, ErrorMessage = "اسم الطابق لا يتجاوز 50 حرفًا")]
            public string? FloorLabel { get; set; }

            public List<HotelRoomUnitItemDto> Units { get; set; } = new();
        }

        // ==========================================
        // 🚪 الوحدة الفعلية داخل الطابق
        // RoomTypeClientKey يربط الوحدة بنوعها داخل HotelRoomTypes
        // ==========================================
        public class HotelRoomUnitItemDto
        {
            [Required(ErrorMessage = "يرجى إدخال رقم الوحدة")]
            [StringLength(50, ErrorMessage = "رقم الوحدة لا يتجاوز 50 حرفًا")]
            public string RoomNumber { get; set; } = string.Empty;

            [Required(ErrorMessage = "يرجى تحديد نوع الوحدة")]
            [StringLength(60)]
            public string RoomTypeClientKey { get; set; } = string.Empty;

            public bool IsAvailable { get; set; } = true;

            [StringLength(250, ErrorMessage = "ملاحظات الوحدة لا تتجاوز 250 حرفًا")]
            public string? Notes { get; set; }
        }

        // 🌟 الكلاسات الديناميكية الحالية يجب أن تظل داخل FullPlaceWizardDto
        public class ServiceItemDto { public string? Name { get; set; } public decimal Price { get; set; } }
        public class AmenityItemDto { public string? Name { get; set; } public string? IconClass { get; set; } }
        public class SpaceItemDto { public string? Name { get; set; } public string? IconClass { get; set; } }
        public class PackageItemDto
        {
            [Required(ErrorMessage = "يرجى إدخال اسم الباقة")]
            [StringLength(100, ErrorMessage = "اسم الباقة لا يتجاوز 100 حرف")]
            public string Name { get; set; } = string.Empty;

            public decimal Price { get; set; }

            public int Capacity { get; set; }

            public string? TimeFrame { get; set; }

            public TimeSpan? StartTime { get; set; }

            public TimeSpan? EndTime { get; set; }
        }
        public class PlaceRejectbyOwnerModel
        {
            public int Id { get; set; }
            public string Reason { get; set; }
        }
    }
}
