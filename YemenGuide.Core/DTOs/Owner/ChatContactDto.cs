using System;

namespace YemenGuide.Core.DTOs.Owner
{
    /// <summary>
    /// يمثل عميلاً واحدًا داخل قائمة محادثات المنشأة المحددة في هيدر المالك.
    /// قد يظهر العميل بسبب وجود محادثة سابقة أو بسبب وجود حجز في المنشأة.
    /// </summary>
    public class ChatContactDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "عميل";
        public string? ProfileImage { get; set; }
        public string InitialLetter { get; set; } = "ع";

        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;

        public string LastMessage { get; set; } = string.Empty;
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }

        public bool HasPreviousConversation { get; set; }
        public bool HasBooking { get; set; }
        public bool CanStartConversation { get; set; }

        public int? LatestBookingId { get; set; }
        public string? LatestBookingState { get; set; }
        public DateTime? LatestBookingStartDate { get; set; }
        public DateTime? LatestBookingEndDate { get; set; }

        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }

        /// <summary>
        /// حالة حساب العميل على مستوى النظام، وليست حالة حظر داخل محادثة.
        /// لا يجوز للمالك تعديل هذه القيمة من شاشة الدردشة.
        /// </summary>
        public string AccountState { get; set; } = "Active";

        public bool IsCustomerAccountActive =>
            string.Equals(AccountState, "Active", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Conversation = توجد رسائل سابقة، Booking = عميل حجز ولم تبدأ محادثته بعد.
        /// </summary>
        public string ContactSource =>
            HasPreviousConversation ? "Conversation" : "Booking";
    }
}
