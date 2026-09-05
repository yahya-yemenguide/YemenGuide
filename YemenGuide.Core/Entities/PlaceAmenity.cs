using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlaceAmenity
    {
        public int AmenityId { get; set; }
        public int PlaceId { get; set; }
        public string AmenityName { get; set; } = null!;
        public string? IconClass { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual Place Place { get; set; } = null!;
    }
}
