using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Notification
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string? NotificationTitle { get; set; }
        public string NotificationText { get; set; } = null!;
        public string? NotificationType { get; set; }
        public string? TargetUrl { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public string? NotificationState { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
