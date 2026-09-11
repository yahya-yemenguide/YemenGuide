using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using YemenGuide.Core.Constants;

namespace YemenGuide.Core.DTOs.Admin
{
    /// <summary>
    /// قيم العرض الخاصة ببلاغات الإدارة بعد تبسيط المسار.
    /// لا يوجد هنا أي منطق إسناد أو إدارة موظفين؛ الصلاحيات تدار من لوحة الموظفين المستقلة.
    /// </summary>
    public static class AdminReportValues
    {
        public const string TabAll = "all";
        public const string TabNew = "new";
        public const string TabAction = "action";
        public const string TabWaiting = "waiting";
        public const string TabEscalated = "escalated";
        public const string TabCompleted = "completed";

        // المسار الرئيسي للقضية داخل مركز البلاغات.
        public const string ScopeAll = "all";
        public const string ScopeCustomerAgainstOwner = "customer-owner";
        public const string ScopeOwnerAgainstCustomer = "owner-customer";
        public const string ScopePlatform = "platform";

        public const string ScenarioCustomerAgainstOwner = "CustomerAgainstOwner";
        public const string ScenarioOwnerAgainstCustomer = "OwnerAgainstCustomer";
        public const string ScenarioPlatform = "Platform";
        public const string ScenarioGeneral = "General";

        public static readonly IReadOnlyCollection<string> Tabs = new[]
        {
            TabAll,
            TabNew,
            TabAction,
            TabWaiting,
            TabEscalated,
            TabCompleted
        };

        public static readonly IReadOnlyCollection<string> Scopes = new[]
        {
            ScopeAll,
            ScopeCustomerAgainstOwner,
            ScopeOwnerAgainstCustomer,
            ScopePlatform
        };

        public static string NormalizeScope(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ScopeAll;

            return Scopes.FirstOrDefault(item =>
                       string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? ScopeAll;
        }

        public static string GetScopeLabel(string? value)
        {
            return NormalizeScope(value) switch
            {
                ScopeCustomerAgainstOwner => "العميل ضد المالك",
                ScopeOwnerAgainstCustomer => "المالك ضد العميل",
                ScopePlatform => "المنصة والتقني",
                _ => "كل القضايا"
            };
        }

        public static string GetScopeIcon(string? value)
        {
            return NormalizeScope(value) switch
            {
                ScopeCustomerAgainstOwner => "bi-person-fill-exclamation",
                ScopeOwnerAgainstCustomer => "bi-building-fill-exclamation",
                ScopePlatform => "bi-window-stack",
                _ => "bi-collection-fill"
            };
        }

        public static string NormalizeTab(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return TabAll;

            return Tabs.FirstOrDefault(item =>
                       string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? TabAll;
        }

        public static string GetAdminStatusLabel(string? value)
        {
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.Pending)) return "جديدة";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.InReview)) return "قيد المتابعة";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.WaitingForReporter)) return "بانتظار رد المبلّغ";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.WaitingForReportedParty)) return "بانتظار رد المالك";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.Escalated)) return "مصعّدة";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.Resolved)) return "تم الحل";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.Rejected)) return "مرفوضة";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.Closed)) return "مغلقة";
            if (ReportConstants.EqualsState(value, ReportConstants.Statuses.Withdrawn)) return "مسحوبة";
            return "غير محددة";
        }

        public static string GetPriorityLabel(string? value)
        {
            if (ReportConstants.EqualsState(value, ReportConstants.Priorities.Low)) return "منخفضة";
            if (ReportConstants.EqualsState(value, ReportConstants.Priorities.High)) return "عالية";
            if (ReportConstants.EqualsState(value, ReportConstants.Priorities.Urgent)) return "عاجلة";
            return "عادية";
        }

        public static string GetReportTypeLabel(string? value, string? otherDescription)
        {
            if (ReportConstants.EqualsState(value, ReportConstants.Types.Other) &&
                !string.IsNullOrWhiteSpace(otherDescription))
            {
                return otherDescription.Trim();
            }

            return ReportConstants.GetTypeLabel(value);
        }

        public static string GetCategoryLabel(string? value, string? otherDescription)
        {
            if (ReportConstants.EqualsState(value, ReportConstants.Categories.Other) &&
                !string.IsNullOrWhiteSpace(otherDescription))
            {
                return otherDescription.Trim();
            }

            return ReportConstants.GetCategoryLabel(value);
        }

        public static string GetRequiredActionLabel(string? value)
        {
            if (string.Equals(value, ReportConstants.RequiredActions.Clarification, StringComparison.OrdinalIgnoreCase))
                return "توضيح من المبلّغ";

            if (string.Equals(value, ReportConstants.RequiredActions.ResolutionConfirmation, StringComparison.OrdinalIgnoreCase))
                return "تأكيد الحل من العميل";

            if (string.Equals(value, ReportConstants.RequiredActions.ReportedPartyResponse, StringComparison.OrdinalIgnoreCase))
                return "رد من المالك";

            return string.Empty;
        }

        public static string GetRoleLabel(string? role)
        {
            if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)) return "مالك";
            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase)) return "عميل";
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return "الإدارة";
            return "مستخدم";
        }

        public static string GetScenarioLabel(string scenario)
        {
            return scenario switch
            {
                ScenarioCustomerAgainstOwner => "بلاغ عميل ضد مالك/منشأة",
                ScenarioOwnerAgainstCustomer => "بلاغ مالك ضد عميل",
                ScenarioPlatform => "بلاغ عن المنصة أو مشكلة تقنية",
                _ => "بلاغ عام"
            };
        }

        public static string GetTypeIcon(string? type)
        {
            if (ReportConstants.EqualsState(type, ReportConstants.Types.Booking)) return "bi-calendar2-check-fill";
            if (ReportConstants.EqualsState(type, ReportConstants.Types.Place)) return "bi-building-fill";
            if (ReportConstants.EqualsState(type, ReportConstants.Types.Review)) return "bi-star-fill";
            if (ReportConstants.EqualsState(type, ReportConstants.Types.Promotion)) return "bi-tags-fill";
            if (ReportConstants.EqualsState(type, ReportConstants.Types.Advertisement)) return "bi-megaphone-fill";
            if (ReportConstants.EqualsState(type, ReportConstants.Types.Technical)) return "bi-tools";
            if (ReportConstants.EqualsState(type, ReportConstants.Types.Platform)) return "bi-window-stack";
            return "bi-shield-exclamation";
        }
    }

    public sealed class AdminReportsCenterDto
    {
        public string ActiveTab { get; set; } = AdminReportValues.TabAll;
        public AdminReportSummaryDto Summary { get; set; } = new();
        public IReadOnlyList<AdminReportTabDto> Tabs { get; set; } = Array.Empty<AdminReportTabDto>();
        public AdminReportsListPageDto Reports { get; set; } = new();
    }

    public sealed class AdminReportTabDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-circle";
        public int Count { get; set; }
    }

    public sealed class AdminReportSummaryDto
    {
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ActionCount { get; set; }
        public int WaitingCount { get; set; }
        public int EscalatedCount { get; set; }
        public int CompletedCount { get; set; }
        public int UrgentOpenCount { get; set; }
        public int OverdueCount { get; set; }

        // عدادات المسارات الرئيسية.
        public int CustomerAgainstOwnerCount { get; set; }
        public int OwnerAgainstCustomerCount { get; set; }
        public int PlatformCount { get; set; }
    }

    public sealed class AdminReportFilterDto
    {
        [MaxLength(150)]
        public string? Search { get; set; }

        [MaxLength(30)]
        public string? Scope { get; set; } = AdminReportValues.ScopeAll;

        [MaxLength(30)]
        public string? Status { get; set; }

        [MaxLength(30)]
        public string? ReportType { get; set; }

        [MaxLength(20)]
        public string? Priority { get; set; }

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool IsOverdueOnly { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(5, 50)]
        public int PageSize { get; set; } = 15;
    }

    public sealed class AdminReportsListPageDto
    {
        public string Tab { get; set; } = AdminReportValues.TabAll;
        public AdminReportFilterDto Filter { get; set; } = new();
        public IReadOnlyList<AdminReportListItemDto> Items { get; set; } = Array.Empty<AdminReportListItemDto>();
        public int FilteredCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public sealed class AdminReportListItemDto
    {
        public int ReportId { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string RequestKind { get; set; } = ReportConstants.RequestKinds.Report;
        public string RequestKindLabel { get; set; } = "بلاغ";
        public string ReportType { get; set; } = ReportConstants.Types.Other;
        public string ReportTypeLabel { get; set; } = "أخرى";
        public string TypeIcon { get; set; } = "bi-shield-exclamation";
        public string? Category { get; set; }
        public string CategoryLabel { get; set; } = "أخرى";
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = ReportConstants.Statuses.Pending;
        public string StatusLabel { get; set; } = "جديدة";
        public string Priority { get; set; } = ReportConstants.Priorities.Normal;
        public string PriorityLabel { get; set; } = "عادية";
        public string? RequiredAction { get; set; }
        public string? ResponseContentType { get; set; }
        public string RequiredActionLabel { get; set; } = string.Empty;

        public string Scenario { get; set; } = AdminReportValues.ScenarioGeneral;
        public string ScenarioLabel { get; set; } = "بلاغ عام";
        public string DirectionLabel { get; set; } = string.Empty;

        public int ReporterUserId { get; set; }
        public string ReporterName { get; set; } = "مستخدم";
        public string ReporterRole { get; set; } = string.Empty;
        public string ReporterRoleLabel { get; set; } = "مستخدم";

        public int? AgainstUserId { get; set; }
        public string AgainstUserName { get; set; } = "لا يوجد طرف مبلّغ ضده";
        public string AgainstUserRole { get; set; } = string.Empty;
        public string AgainstUserRoleLabel { get; set; } = "—";
        public int AgainstUserWarningCount { get; set; }

        // سجل الطرف المبلّغ ضده في مركز القضايا، سواء كان مالكًا أو عميلًا.
        public int AgainstUserPreviousCasesCount { get; set; }
        public int AgainstUserConfirmedViolationsCount { get; set; }

        public string? PlaceName { get; set; }
        public string ReferenceLabel { get; set; } = "بدون مرجع";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ReplyDeadline { get; set; }
        public bool HasEvidence { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsUrgent { get; set; }
        public bool NeedsAdminAction { get; set; }
    }

    public sealed class AdminReportDetailsDto
    {
        public int ReportId { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string RequestKind { get; set; } = ReportConstants.RequestKinds.Report;
        public string RequestKindLabel { get; set; } = "بلاغ";
        public string ReportType { get; set; } = ReportConstants.Types.Other;
        public string ReportTypeLabel { get; set; } = "أخرى";
        public string TypeIcon { get; set; } = "bi-shield-exclamation";
        public string? Category { get; set; }
        public string CategoryLabel { get; set; } = "أخرى";
        public string Title { get; set; } = string.Empty;
        public string ReportReason { get; set; } = string.Empty;

        public string Status { get; set; } = ReportConstants.Statuses.Pending;
        public string StatusLabel { get; set; } = "جديدة";
        public string Priority { get; set; } = ReportConstants.Priorities.Normal;
        public string PriorityLabel { get; set; } = "عادية";
        public string? RequiredAction { get; set; }
        public string? ResponseContentType { get; set; }
        public string RequiredActionLabel { get; set; } = string.Empty;

        public string Scenario { get; set; } = AdminReportValues.ScenarioGeneral;
        public string ScenarioLabel { get; set; } = "بلاغ عام";
        public string DirectionLabel { get; set; } = string.Empty;
        public bool IsCustomerAgainstOwner { get; set; }
        public bool IsAdministrationProperty { get; set; }
        public bool IsOwnerAgainstCustomer { get; set; }
        public bool IsPlatformCase { get; set; }

        public string CurrentStepLabel { get; set; } = string.Empty;
        public string CurrentStepDescription { get; set; } = string.Empty;
        public string? EvidenceImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ReplyDeadline { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsUrgent { get; set; }

        public string? AdminComment { get; set; }
        public string? ReporterReply { get; set; }
        public DateTime? ReporterReplyDate { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? OwnerReplyDate { get; set; }
        public DateTime? EscalatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? Resolution { get; set; }
        public string? ResolutionAction { get; set; }
        public string? ResolvedByAdminName { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? WithdrawnAt { get; set; }
        public string? WithdrawnReason { get; set; }
        public DateTime? WarningIssuedAt { get; set; }
        public string CustomerResolutionConfirmationState { get; set; } = "None";

        public AdminReportPartyDto Reporter { get; set; } = new();
        public AdminReportPartyDto ReportedParty { get; set; } = new();
        public AdminReportReferenceDto Reference { get; set; } = new();
        public IReadOnlyList<AdminReportTimelineItemDto> Timeline { get; set; } = Array.Empty<AdminReportTimelineItemDto>();
        public IReadOnlyList<AdminReportInternalNoteDto> InternalNotes { get; set; } = Array.Empty<AdminReportInternalNoteDto>();

        public bool CanStartReview { get; set; }
        public bool CanRequestReporterClarification { get; set; }
        public bool CanRequestOwnerResponse { get; set; }
        public bool CanRequestResolutionConfirmation { get; set; }
        public bool CanChangePriority { get; set; }
        public bool CanEscalate { get; set; }
        public bool CanAddInternalNote { get; set; }
        public bool CanResolve { get; set; }
        public bool CanReject { get; set; }
        public bool CanConfirmCustomerViolation { get; set; }
        public bool CanDismissCustomerViolation { get; set; }
        public bool CanClose { get; set; }
        public bool CanReopen { get; set; }
    }

    public sealed class AdminReportPartyDto
    {
        public int? UserId { get; set; }
        public string FullName { get; set; } = "مستخدم";
        public string Role { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = "مستخدم";
        public string? ProfileImage { get; set; }
        public string AccountState { get; set; } = string.Empty;
        public string AccountStateLabel { get; set; } = "غير محدد";
        public int WarningCount { get; set; }

        // إحصاءات منفصلة حتى يعرف الأدمن هل على الشخص سوابق كطرف مبلّغ ضده.
        public int PreviousReportsCount { get; set; }
        public int ReportsFiledCount { get; set; }
        public int ComplaintsFiledCount { get; set; }
        public int ReportsAgainstCount { get; set; }
        public int ComplaintsAgainstCount { get; set; }
        public int ConfirmedViolationsCount { get; set; }

        public IReadOnlyList<AdminReportPartyHistoryItemDto> RecentCasesAgainst { get; set; } =
            Array.Empty<AdminReportPartyHistoryItemDto>();
    }

    public sealed class AdminReportPartyHistoryItemDto
    {
        public int ReportId { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string RequestKindLabel { get; set; } = "بلاغ";
        public string Title { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool HasWarning { get; set; }
    }

    public sealed class AdminReportReferenceDto
    {
        public string ReferenceType { get; set; } = "None";
        public string PrimaryLabel { get; set; } = "لا يوجد مرجع مرتبط";
        public string? SecondaryLabel { get; set; }
        public int? ReferenceId { get; set; }
        public int? PlaceId { get; set; }
        public string? PlaceName { get; set; }
        public string? ExtraInfo { get; set; }
    }

    public sealed class AdminReportTimelineItemDto
    {
        public string EventType { get; set; } = "Event";
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime OccurredAt { get; set; }
        public string ActorName { get; set; } = "النظام";
        public string ActorRoleLabel { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-circle-fill";
        public string Tone { get; set; } = "info";
        public string? AttachmentUrl { get; set; }
        public bool IsMessage { get; set; }
    }

    public sealed class AdminReportInternalNoteDto
    {
        public long AuditLogId { get; set; }
        public string AdminName { get; set; } = "الإدارة";
        public string NoteText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class AdminReportSimpleActionDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }
    }

    public sealed class AdminReportReplyRequestDto
    {
        [Required, RegularExpression("^(Text|Image|TextAndImage)$")]
        public string ResponseContentType { get; set; } = ReportConstants.ResponseContentTypes.Text;

        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(4000, MinimumLength = 5)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public DateTime? Deadline { get; set; }
    }

    public sealed class AdminReportReporterMessageDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }
        [Required, StringLength(4000, MinimumLength = 3)]
        public string Message { get; set; } = string.Empty;
    }

    public sealed class AdminReportResolutionConfirmationRequestDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [StringLength(1000)]
        public string? Message { get; set; }

        [Required]
        public DateTime? Deadline { get; set; }
    }

    public sealed class AdminReportPriorityDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(20)]
        public string Priority { get; set; } = ReportConstants.Priorities.Normal;

        [Required, StringLength(500, MinimumLength = 3)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AdminReportEscalationDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(1000, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AdminReportInternalNoteRequestDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(2000, MinimumLength = 2)]
        public string NoteText { get; set; } = string.Empty;
    }

    public sealed class AdminReportResolutionDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(4000, MinimumLength = 5)]
        public string Resolution { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 2)]
        public string ResolutionAction { get; set; } = string.Empty;

        public bool ReferToFinance { get; set; }
        public bool ReferToTechnicalSupport { get; set; }
    }

    public sealed class AdminReportRejectionDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(2000, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AdminCustomerViolationDecisionDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(2000, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AdminReportCloseDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(1000, MinimumLength = 3)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AdminReportReopenDto
    {
        [Range(1, int.MaxValue)]
        public int ReportId { get; set; }

        [Required, StringLength(1000, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AdminReportRequestContextDto
    {
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public sealed class AdminReportOperationResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public int? ReportId { get; set; }

        public static AdminReportOperationResultDto Ok(string message, int reportId) =>
            new() { Success = true, Message = message, ReportId = reportId };

        public static AdminReportOperationResultDto Fail(string message, string? errorCode = null) =>
            new() { Success = false, Message = message, ErrorCode = errorCode };
    }
}
