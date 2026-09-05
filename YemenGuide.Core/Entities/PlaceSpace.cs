using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlaceSpace
    {
        public int SpaceId { get; set; }
        public int PlaceId { get; set; }
        public string SpaceName { get; set; } = null!;
        public string? IconClass { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual Place Place { get; set; } = null!;
    }
}
