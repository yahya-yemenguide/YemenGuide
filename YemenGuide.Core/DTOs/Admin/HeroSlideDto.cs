using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YemenGuide.Core.DTOs.Admin
{
    public class HeroSlideDto
    {
        public int Id { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string TypingText { get; set; } = string.Empty;

        // ==========================================
        // 🎨 إعدادات التناغم اللوني لكل شريحة (VIP)
        // ==========================================
        public string MainTextColor { get; set; } = "#ffffff";
        public string AccentColor { get; set; } = "#ff5a5f";

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
