using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Chat
    {
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string? MessageText { get; set; }
        public DateTime? SendTime { get; set; }
        public bool IsRead { get; set; }
        public string? AttachmentUrl { get; set; }
        public int? PlaceId { get; set; }
        public bool IsDeletedForSender { get; set; }
        public bool IsDeletedForReceiver { get; set; }
        public bool IsEdited { get; set; }
        public int? ReportId { get; set; }
        public bool IsReportMessage { get; set; }

        public virtual Place? Place { get; set; }
        public virtual User Receiver { get; set; } = null!;
        public virtual Report? Report { get; set; }
        public virtual User Sender { get; set; } = null!;
    }
}
