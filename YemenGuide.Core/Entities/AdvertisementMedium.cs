using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdvertisementMedium
    {
        public int AdvertisementMediaId { get; set; }
        public int AdvertisementId { get; set; }
        public string MediaType { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public string? ThumbnailPath { get; set; }
        public string? AltText { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; }
        public bool? IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Advertisement Advertisement { get; set; } = null!;
    }
}
