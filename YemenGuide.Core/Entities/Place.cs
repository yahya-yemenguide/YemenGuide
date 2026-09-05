using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Place
    {
        public Place()
        {
            Advertisements = new HashSet<Advertisement>();
            Bookings = new HashSet<Booking>();
            Chats = new HashSet<Chat>();
            CommissionTransactions = new HashSet<CommissionTransaction>();
            Favorites = new HashSet<Favorite>();
            PlaceAdditionalServices = new HashSet<PlaceAdditionalService>();
            PlaceAmenities = new HashSet<PlaceAmenity>();
            PlaceCalendarExceptions = new HashSet<PlaceCalendarException>();
            PlaceImages = new HashSet<PlaceImage>();
            PlacePackages = new HashSet<PlacePackage>();
            PlaceSpaces = new HashSet<PlaceSpace>();
            Promotions = new HashSet<Promotion>();
            RefundRequests = new HashSet<RefundRequest>();
            Reports = new HashSet<Report>();
            Reviews = new HashSet<Review>();
            RoomUnits = new HashSet<RoomUnit>();
            Rooms = new HashSet<Room>();
        }

        public int PlaceId { get; set; }
        public int? OwnerId { get; set; }
        public int CategoryId { get; set; }
        public int CityId { get; set; }
        public string PlaceNameAndDesc { get; set; } = null!;
        public string? Description { get; set; }
        public string? Coordinates { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = null!;
        public bool? IsFeatured { get; set; }
        public bool? IsAvailable { get; set; }
        public bool? AllowCashOnArrival { get; set; }
        public string? LogoImage { get; set; }
        public string? PhoneNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? AddressDetail { get; set; }
        public string? CoverImage { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public string? CancellationPolicy { get; set; }
        public bool? IsCashOnArrival { get; set; }
        public bool IsDepositRequired { get; set; }
        public decimal DepositAmount { get; set; }
        public string? DepositCurrency { get; set; }
        public string? DepositPolicy { get; set; }
        public string? InsurancePolicy { get; set; }
        public string? Terms { get; set; }
        public string? FloorsData { get; set; }
        public int? RoomsCount { get; set; }
        public int? SuitesCount { get; set; }
        public string? RejectionReason { get; set; }

        public virtual Category Category { get; set; } = null!;
        public virtual City City { get; set; } = null!;
        public virtual User? Owner { get; set; }
        public virtual PlaceCommissionAccount? PlaceCommissionAccount { get; set; }
        public virtual PlacePolicy? PlacePolicy { get; set; }
        public virtual ICollection<Advertisement> Advertisements { get; set; }
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<Chat> Chats { get; set; }
        public virtual ICollection<CommissionTransaction> CommissionTransactions { get; set; }
        public virtual ICollection<Favorite> Favorites { get; set; }
        public virtual ICollection<PlaceAdditionalService> PlaceAdditionalServices { get; set; }
        public virtual ICollection<PlaceAmenity> PlaceAmenities { get; set; }
        public virtual ICollection<PlaceCalendarException> PlaceCalendarExceptions { get; set; }
        public virtual ICollection<PlaceImage> PlaceImages { get; set; }
        public virtual ICollection<PlacePackage> PlacePackages { get; set; }
        public virtual ICollection<PlaceSpace> PlaceSpaces { get; set; }
        public virtual ICollection<Promotion> Promotions { get; set; }
        public virtual ICollection<RefundRequest> RefundRequests { get; set; }
        public virtual ICollection<Report> Reports { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<RoomUnit> RoomUnits { get; set; }
        public virtual ICollection<Room> Rooms { get; set; }
    }
}
