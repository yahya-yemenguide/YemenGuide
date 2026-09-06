using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdvertisementEvent
    {
        public long AdvertisementEventId { get; set; }
        public int AdvertisementId { get; set; }
        public int? UserId { get; set; }
        public int? BookingId { get; set; }
        public string EventType { get; set; } = null!;
        public string PlacementType { get; set; } = null!;
        public string? SessionKey { get; set; }
        public string? VisitorHash { get; set; }
        public string? RequestKey { get; set; }
        public string? DeviceType { get; set; }
        public string? IpHash { get; set; }
        public string? UserAgentHash { get; set; }
        public decimal CostAmount { get; set; }
        public DateTime EventAt { get; set; }

        public virtual Advertisement Advertisement { get; set; } = null!;
        public virtual Booking? Booking { get; set; }
        public virtual User? User { get; set; }
    }
}
