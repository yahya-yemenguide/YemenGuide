using YemenGuide.Core.Entities;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Admin
{
    public class ModerationDto
    {
        public IEnumerable<Report> PendingReports { get; set; } = new List<Report>();
        public IEnumerable<Review> AllReviews { get; set; } = new List<Review>();

        // ✨ السطر الجديد: قائمة المحظورين
        public IEnumerable<User> BlacklistedUsers { get; set; } = new List<User>();
    }
}
