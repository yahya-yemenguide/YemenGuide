using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdvertisementTarget
    {
        public int AdvertisementTargetId { get; set; }
        public int AdvertisementId { get; set; }
        public int? CityId { get; set; }
        public int? CategoryId { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Advertisement Advertisement { get; set; } = null!;
        public virtual Category? Category { get; set; }
        public virtual City? City { get; set; }
    }
}
