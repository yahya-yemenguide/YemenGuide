using System;
using System.Collections.Generic;
using System.Linq;

namespace YemenGuide.Core.DTOs.Customer
{
    /// <summary>
    /// يمثل محادثة واحدة للعميل داخل منشأة محددة.
    /// المالك نفسه قد يظهر أكثر من مرة عندما يملك أكثر من منشأة،
    /// لذلك هوية المحادثة الفعلية هي OwnerId + PlaceId.
    /// </summary>
    public class CustomerChatContactDto
    {
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = "مالك";
        public string? ProfileImage { get; set; }
        public string InitialLetter { get; set; } = "م";

        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string? PlaceImage { get; set; }

        public string LastMessage { get; set; } = string.Empty;
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }

        public bool HasPreviousConversation { get; set; }
        public bool HasBooking { get; set; }
        public bool CanStartConversation { get; set; }
        public bool IsChatEnabled { get; set; }

        public int? LatestBookingId { get; set; }
        public string? LatestBookingState { get; set; }
        public DateTime? LatestBookingStartDate { get; set; }
        public DateTime? LatestBookingEndDate { get; set; }

        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }

        /// <summary>
        /// حالة حساب المالك على مستوى النظام.
        /// لا تمثل حظرًا خاصًا بمحادثة واحدة.
        /// </summary>
        public string OwnerAccountState { get; set; } = "Active";

        public bool IsOwnerAccountActive =>
            string.IsNullOrWhiteSpace(OwnerAccountState) ||
            string.Equals(OwnerAccountState, "Active", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// أبقينا الخاصية القديمة حتى لا تنكسر الواجهة الحالية قبل المرحلة الثانية.
        /// لا توجد حاليًا بنية بيانات صحيحة لحظر عميل من مالك داخل منشأة واحدة،
        /// لذلك لا يجوز اشتقاقها من Users.AccountState.
        /// </summary>
        public bool IsBlockedByOwner { get; set; }

        public string ContactSource =>
            HasPreviousConversation
                ? "Conversation"
                : HasBooking
                    ? "Booking"
                    : "Place";

        public string ConversationKey => $"{PlaceId}:{OwnerId}";
    }

    /// <summary>
    /// نموذج صفحة دردشة العميل.
    /// </summary>
    public class CustomerChatPageModel
    {
        public List<CustomerChatContactDto> ChatContacts { get; set; } =
            new List<CustomerChatContactDto>();

        public int? RequestedPlaceId { get; set; }
        public int? RequestedOwnerId { get; set; }

        public int TotalUnreadConversations =>
            ChatContacts.Count(contact => contact.UnreadCount > 0);
    }

    /// <summary>
    /// رسالة واحدة بين العميل والمالك داخل منشأة محددة.
    /// </summary>
    public class ChatMessageDto
    {
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public int PlaceId { get; set; }

        public string? Text { get; set; }
        public string? Time { get; set; }
        public bool IsSentByMe { get; set; }
        public bool IsRead { get; set; }
        public bool IsEdited { get; set; }

        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentSize { get; set; }
        public bool IsImage { get; set; }
        public bool IsVideo { get; set; }

        public DateTime? SendTimeRaw { get; set; }
    }
}
