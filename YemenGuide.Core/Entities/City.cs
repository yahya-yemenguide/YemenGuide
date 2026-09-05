using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class City
    {
        public City()
        {
            AdvertisementTargets = new HashSet<AdvertisementTarget>();
            Places = new HashSet<Place>();
        }

        public int CityId { get; set; }
        public string CityName { get; set; } = null!;
        public string? ThemeColor { get; set; }
        public string? ExchangeRateRegion { get; set; }

        public virtual ICollection<AdvertisementTarget> AdvertisementTargets { get; set; }
        public virtual ICollection<Place> Places { get; set; }
    }
}
