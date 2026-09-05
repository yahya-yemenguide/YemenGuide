using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Notifications
{
    public class CreateNotificationDto
    {
        public int UserId { get; set; }
        public string? NotificationTitle { get; set; }
        public string NotificationText { get; set; } = string.Empty;
        public string? NotificationType { get; set; }
        public string? TargetUrl { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
    }

    public class NotificationItemDto
    {
        public int NotificationId { get; set; }
        public string NotificationTitle { get; set; } = "إشعار";
        public string NotificationText { get; set; } = string.Empty;
        public string NotificationType { get; set; } = "General";
        public string NotificationState { get; set; } = "Unread";
        public bool IsRead { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public string? TargetUrl { get; set; }
        public string OpenUrl { get; set; } = string.Empty;
    }

    public class NotificationHeaderDto
    {
        public int UnreadCount { get; set; }
        public List<NotificationItemDto> Items { get; set; } = new();
    }

    public class NotificationCenterDto
    {
        public string Filter { get; set; } = "all";
        public int FocusNotificationId { get; set; }

        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int BookingCount { get; set; }
        public int PaymentCount { get; set; }
        public int ReviewCount { get; set; }
        public int MessageCount { get; set; }
        public int ReportCount { get; set; }

        // عدادات مركز إشعارات الإدارة. تبقى حقول العميل أعلاه كما هي.
        public int CaseCount { get; set; }
        public int FinanceCount { get; set; }
        public int AdvertisementCount { get; set; }
        public int ManagementCount { get; set; }
        public int SystemCount { get; set; }

        public int FilteredCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalPages { get; set; }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public List<NotificationItemDto> Items { get; set; } = new();
    }
}
