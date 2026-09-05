using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Report
    {
        public Report()
        {
            Chats = new HashSet<Chat>();
        }

        public int ReportId { get; set; }
        public int ReporterId { get; set; }
        public int? AgainstUserId { get; set; }
        public string ReportReason { get; set; } = null!;
        public string? RequiredAction { get; set; }
        public int? PlaceId { get; set; }
        public string? EvidenceImage { get; set; }
        public string ReportStatus { get; set; } = null!;
        public string? AdminComment { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ReportType { get; set; } = null!;
        public int? ReviewId { get; set; }
        public DateTime? WithdrawnAt { get; set; }
        public string? WithdrawnReason { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? OwnerReplyDate { get; set; }
        public DateTime? EscalatedAt { get; set; }
        public DateTime? WarningIssuedAt { get; set; }
        public string ReportNumber { get; set; } = null!;
        public string RequestKind { get; set; } = null!;
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string Priority { get; set; } = null!;
        public int? BookingId { get; set; }
        public int? PromotionId { get; set; }
        public int? AdvertisementId { get; set; }
        public int? AssignedAdminUserId { get; set; }
        public DateTime? ReporterReplyDeadline { get; set; }
        public DateTime? ReportedPartyReplyDeadline { get; set; }
        public string? Resolution { get; set; }
        public string? ResolutionAction { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedByUserId { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;
        public string? ReporterReply { get; set; }
        public DateTime? ReporterReplyDate { get; set; }
        public string? OtherReportTypeDescription { get; set; }
        public string? OtherCategoryDescription { get; set; }

        public virtual Advertisement? Advertisement { get; set; }
        public virtual User? AgainstUser { get; set; }
        public virtual User? AssignedAdminUser { get; set; }
        public virtual Booking? Booking { get; set; }
        public virtual Place? Place { get; set; }
        public virtual Promotion? Promotion { get; set; }
        public virtual User Reporter { get; set; } = null!;
        public virtual User? ResolvedByUser { get; set; }
        public virtual Review? Review { get; set; }
        public virtual ICollection<Chat> Chats { get; set; }
    }
}
