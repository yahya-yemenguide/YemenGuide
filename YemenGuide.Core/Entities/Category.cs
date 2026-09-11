using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Category
    {
        public Category()
        {
            AdvertisementTargets = new HashSet<AdvertisementTarget>();
            Places = new HashSet<Place>();
        }

        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public string? CategoryNameEn { get; set; }
        public string? IconClass { get; set; }
        public bool HasBookingSystem { get; set; }
        public string? ThemeColor { get; set; }
        public string? ImagePath { get; set; }
        public int BookingSystemId { get; set; }
        public decimal? CommissionPercentage { get; set; }
        public bool? OwnerCanAddPlaces { get; set; }

        public virtual ICollection<AdvertisementTarget> AdvertisementTargets { get; set; }
        public virtual ICollection<Place> Places { get; set; }
    }
}
