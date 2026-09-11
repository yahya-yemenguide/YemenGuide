using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class HeroSlide
    {
        public int HeroSlideId { get; set; }
        public string ImagePath { get; set; } = null!;
        public string TypingText { get; set; } = null!;
        public string? TypingTextEn { get; set; }
        public int DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string MainTextColor { get; set; } = null!;
        public string AccentColor { get; set; } = null!;
    }
}
