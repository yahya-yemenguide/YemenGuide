using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class BookingRoomUnit
    {
        public int BookingRoomUnitId { get; set; }
        public int BookingId { get; set; }
        public int RoomUnitId { get; set; }
        public string AssignmentStatus { get; set; } = null!;
        public DateTime AssignedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }

        public virtual Booking Booking { get; set; } = null!;
        public virtual RoomUnit RoomUnit { get; set; } = null!;
    }
}
