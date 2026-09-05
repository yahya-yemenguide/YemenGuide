using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YemenGuide.Core.Constants;

namespace YemenGuide.Core.DTOs.Owner
{
    /// <summary>
    /// قيم العرض الخاصة بواجهة بلاغات المالك فقط.
    /// لا تمثل قيودًا جديدة في قاعدة البيانات.
    /// </summary>
    public static class OwnerReportValues
    {
        public const string ScopeIncoming = "incoming";
        public const string ScopeMine = "mine";

        public const string StatusAll = "all";
        public const string StatusNeedsResponse = "needs-response";
        public const string StatusActive = "active";
        public const string StatusCompleted = "completed";

        public const string ReferenceBooking = "Booking";
        public const string ReferenceReview = "Review";

        public static string NormalizeScope(string? value)
        {
            if (string.Equals(value, ScopeMine, StringComparison.OrdinalIgnoreCase))
                return ScopeMine;

            return ScopeIncoming;
        }

        public static string NormalizeStatusGroup(string? value)
        {
            if (string.Equals(value, StatusNeedsResponse, StringComparison.OrdinalIgnoreCase))
                return StatusNeedsResponse;
            if (string.Equals(value, StatusActive, StringComparison.OrdinalIgnoreCase))
                return StatusActive;
            if (string.Equals(value, StatusCompleted, StringComparison.OrdinalIgnoreCase))
                return StatusCompleted;

            return StatusAll;
        }
    }

    public sealed class OwnerReportOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
    }

    public sealed class OwnerReportsFilterDto
    {
        public string Scope { get; set; } = OwnerReportValues.ScopeIncoming;
        public string StatusGroup { get; set; } = OwnerReportValues.StatusAll;
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    public sealed class OwnerReportSummaryDto
    {
        public int IncomingCount { get; set; }
        public int IncomingNeedsResponseCount { get; set; }
        public int MineCount { get; set; }
        public int MineActiveCount { get; set; }
        public int MineCompletedCount { get; set; }
    }

    public sealed class OwnerReportListItemDto
    {
        public int ReportId { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string Scope { get; set; } = OwnerReportValues.ScopeIncoming;
        public string Title { get; set; } = string.Empty;
        public string ReportType { get; set; } = ReportConstants.Types.Other;
        public string ReportTypeLabel { get; set; } = "أخرى";
        public string? Category { get; set; }
        public string CategoryLabel { get; set; } = "أخرى";
        public string ReportStatus { get; set; } = ReportConstants.Statuses.Pending;
        public string StatusLabel { get; set; } = "جديد";
        public string StatusTone { get; set; } = "new";
        public string Priority { get; set; } = ReportConstants.Priorities.Normal;
        public string PriorityLabel { get; set; } = "عادية";
        public bool NeedsOwnerResponse { get; set; }
        public DateTime? ReplyDeadline { get; set; }
        public bool IsOverdue { get; set; }
        public int? PlaceId { get; set; }
        public string? PlaceName { get; set; }
        public int? BookingId { get; set; }
        public int? ReviewId { get; set; }
        public string? ReferenceLabel { get; set; }
        public string? CustomerName { get; set; }
        public int CustomerWarningCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsCompleted { get; set; }
    }

    public sealed class OwnerReportsPageDto
    {
        public OwnerReportsFilterDto Filter { get; set; } = new OwnerReportsFilterDto();
        public OwnerReportSummaryDto Summary { get; set; } = new OwnerReportSummaryDto();
        public IReadOnlyList<OwnerReportListItemDto> Items { get; set; } = Array.Empty<OwnerReportListItemDto>();
        public int FilteredCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public sealed class OwnerReportReferenceOptionDto
    {
        public string ReferenceType { get; set; } = string.Empty;
        public int ReferenceId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public int? PlaceId { get; set; }
        public string? PlaceName { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int CustomerWarningCount { get; set; }
        public DateTime? Date { get; set; }
    }

    /// <summary>
    /// إنشاء بلاغ من المالك ضد عميل.
    /// لا يوجد CustomerId في الطلب؛ الخدمة تستخرجه من الحجز/التقييم بعد التحقق من ملكية المنشأة.
    /// </summary>
    public sealed class OwnerCreateReportRequestDto
    {
        [Required(ErrorMessage = "اختر المرجع المرتبط بالبلاغ.")]
        [StringLength(20)]
        public string ReferenceType { get; set; } = OwnerReportValues.ReferenceBooking;

        [Range(1, int.MaxValue, ErrorMessage = "اختر مرجعًا صحيحًا.")]
        public int ReferenceId { get; set; }

        [Required(ErrorMessage = "اختر نوع البلاغ.")]
        [StringLength(30)]
        public string ReportType { get; set; } = ReportConstants.Types.Booking;

        [StringLength(50, ErrorMessage = "وصف النوع الآخر يجب ألا يتجاوز 50 حرفًا.")]
        public string? OtherTypeDescription { get; set; }

        [StringLength(50, ErrorMessage = "تصنيف المشكلة يجب ألا يتجاوز 50 حرفًا.")]
        public string? Category { get; set; }

        [StringLength(50, ErrorMessage = "وصف التصنيف الآخر يجب ألا يتجاوز 50 حرفًا.")]
        public string? OtherCategoryDescription { get; set; }

        [Required(ErrorMessage = "اكتب عنوانًا مختصرًا للبلاغ.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "العنوان يجب أن يكون بين 5 و200 حرف.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اشرح المخالفة أو المشكلة بوضوح.")]
        [StringLength(4000, MinimumLength = 15, ErrorMessage = "التفاصيل يجب أن تكون بين 15 و4000 حرف.")]
        public string ReportReason { get; set; } = string.Empty;
    }

    public sealed class OwnerCreateReportPageDto
    {
        public OwnerCreateReportRequestDto Form { get; set; } = new OwnerCreateReportRequestDto();
        public IReadOnlyList<OwnerReportOptionDto> ReportTypes { get; set; } = Array.Empty<OwnerReportOptionDto>();
        public IReadOnlyList<OwnerReportOptionDto> Categories { get; set; } = Array.Empty<OwnerReportOptionDto>();
        public IReadOnlyList<OwnerReportReferenceOptionDto> RecentBookings { get; set; } = Array.Empty<OwnerReportReferenceOptionDto>();
        public IReadOnlyList<OwnerReportReferenceOptionDto> RecentReviews { get; set; } = Array.Empty<OwnerReportReferenceOptionDto>();
        public OwnerReportReferenceOptionDto? SelectedReference { get; set; }
    }

    public sealed class OwnerReportTimelineItemDto
    {
        public DateTime Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Actor { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-circle-fill";
        public string Tone { get; set; } = "info";
        public string? AttachmentUrl { get; set; }
        public bool IsOwnerMessage { get; set; }
        public bool IsAdminMessage { get; set; }
    }

    public sealed class OwnerReportDetailsDto
    {
        public int ReportId { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string Scope { get; set; } = OwnerReportValues.ScopeIncoming;
        public bool IsIncomingFromCustomer => Scope == OwnerReportValues.ScopeIncoming;
        public bool IsOwnerCreated => Scope == OwnerReportValues.ScopeMine;

        public string Title { get; set; } = string.Empty;
        public string ReportReason { get; set; } = string.Empty;
        public string ReportType { get; set; } = ReportConstants.Types.Other;
        public string ReportTypeLabel { get; set; } = "أخرى";
        public string? Category { get; set; }
        public string CategoryLabel { get; set; } = "أخرى";
        public string ReportStatus { get; set; } = ReportConstants.Statuses.Pending;
        public string StatusLabel { get; set; } = "جديد";
        public string StatusTone { get; set; } = "new";
        public string Priority { get; set; } = ReportConstants.Priorities.Normal;
        public string PriorityLabel { get; set; } = "عادية";
        public string? EvidenceImage { get; set; }
        public string? Resolution { get; set; }
        public string? ResolutionAction { get; set; }

        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerProfileImage { get; set; }
        public int CustomerWarningCount { get; set; }

        public int? PlaceId { get; set; }
        public string? PlaceName { get; set; }
        public string? PlaceImage { get; set; }
        public int? BookingId { get; set; }
        public DateTime? BookingStartDate { get; set; }
        public DateTime? BookingEndDate { get; set; }
        public string? BookingState { get; set; }
        public int? ReviewId { get; set; }
        public string? ReviewText { get; set; }

        public string? RequiredAction { get; set; }
        public bool NeedsOwnerResponse { get; set; }
        public DateTime? ReplyDeadline { get; set; }
        public bool IsReplyOverdue { get; set; }
        public bool CanSubmitResponse { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public IReadOnlyList<OwnerReportTimelineItemDto> Timeline { get; set; } = Array.Empty<OwnerReportTimelineItemDto>();
    }

    public sealed class OwnerReportResponseRequestDto
    {
        [Required(ErrorMessage = "اكتب ردك على القضية.")]
        [StringLength(4000, MinimumLength = 5, ErrorMessage = "الرد يجب أن يكون بين 5 و4000 حرف.")]
        public string Message { get; set; } = string.Empty;
    }

    public sealed class OwnerReportOperationResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public int? ReportId { get; set; }
        public string? ReportNumber { get; set; }

        public static OwnerReportOperationResultDto Ok(
            string message,
            int reportId,
            string? reportNumber = null)
        {
            return new OwnerReportOperationResultDto
            {
                Success = true,
                Message = message,
                ReportId = reportId,
                ReportNumber = reportNumber
            };
        }

        public static OwnerReportOperationResultDto Fail(
            string message,
            string? errorCode = null)
        {
            return new OwnerReportOperationResultDto
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode
            };
        }
    }
}
