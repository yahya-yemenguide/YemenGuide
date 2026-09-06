using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdvertisementPlan
    {
        public AdvertisementPlan()
        {
            Advertisements = new HashSet<Advertisement>();
        }

        public int AdvertisementPlanId { get; set; }
        public string PlanName { get; set; } = null!;
        public string PlacementType { get; set; } = null!;
        public string DurationType { get; set; } = null!;
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = null!;
        public int MaxActiveAdvertisements { get; set; }
        public int DisplayPriority { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public string BillingModel { get; set; } = null!;
        public decimal? CostPerClick { get; set; }
        public decimal? CostPerThousandImpressions { get; set; }
        public decimal? MinimumTotalBudget { get; set; }
        public decimal? DefaultDailyBudget { get; set; }
        public int MaxMediaItems { get; set; }
        public bool AllowVideo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<Advertisement> Advertisements { get; set; }
    }
}
