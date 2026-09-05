using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlaceImage
    {
        public int ImageId { get; set; }
        public int PlaceId { get; set; }
        public string ImagePath { get; set; } = null!;

        public virtual Place Place { get; set; } = null!;
    }
}
