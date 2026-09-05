using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class BookingAdditionalService
    {
        public int BookingAdditionalServiceId { get; set; }
        public int BookingId { get; set; }
        public int ServiceId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public virtual Booking Booking { get; set; } = null!;
        public virtual PlaceAdditionalService Service { get; set; } = null!;
    }
}
