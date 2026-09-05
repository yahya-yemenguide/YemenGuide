using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdminAuditLog
    {
        public long AdminAuditLogId { get; set; }
        public int? UserId { get; set; }
        public string ActionType { get; set; } = null!;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Description { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
        public string? Ipaddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime OccurredAt { get; set; }
        public bool? IsSuccessful { get; set; }
        public string? FailureReason { get; set; }

        public virtual User? User { get; set; }
    }
}
