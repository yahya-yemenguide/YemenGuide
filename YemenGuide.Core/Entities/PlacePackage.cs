using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlacePackage
    {
        public PlacePackage()
        {
            Bookings = new HashSet<Booking>();
            Promotions = new HashSet<Promotion>();
        }

        public int PackageId { get; set; }
        public int PlaceId { get; set; }
        public string PackageName { get; set; } = null!;
        public decimal Price { get; set; }
        public int Capacity { get; set; }
        public string? TimeFrame { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        public virtual Place Place { get; set; } = null!;
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<Promotion> Promotions { get; set; }
    }
}
