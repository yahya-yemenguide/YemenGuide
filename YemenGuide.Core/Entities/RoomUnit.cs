using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class RoomUnit
    {
        public RoomUnit()
        {
            BookingRoomUnits = new HashSet<BookingRoomUnit>();
        }

        public int RoomUnitId { get; set; }
        public int PlaceId { get; set; }
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = null!;
        public int FloorNumber { get; set; }
        public string? FloorLabel { get; set; }
        public bool? IsAvailable { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Place Place { get; set; } = null!;
        public virtual Room Room { get; set; } = null!;
        public virtual ICollection<BookingRoomUnit> BookingRoomUnits { get; set; }
    }
}
