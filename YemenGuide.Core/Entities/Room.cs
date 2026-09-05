using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Room
    {
        public Room()
        {
            Bookings = new HashSet<Booking>();
            Promotions = new HashSet<Promotion>();
            RoomUnits = new HashSet<RoomUnit>();
        }

        public int RoomId { get; set; }
        public int PlaceId { get; set; }
        public string RoomNumber { get; set; } = null!;
        public string RoomType { get; set; } = null!;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool? IsAvailable { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public decimal? AreaSquareMeters { get; set; }
        public int MaxAdults { get; set; }
        public int MaxChildren { get; set; }
        public int? BedsCount { get; set; }
        public string? BedType { get; set; }
        public int? InternalRoomsCount { get; set; }

        public virtual Place Place { get; set; } = null!;
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<Promotion> Promotions { get; set; }
        public virtual ICollection<RoomUnit> RoomUnits { get; set; }
    }
}
