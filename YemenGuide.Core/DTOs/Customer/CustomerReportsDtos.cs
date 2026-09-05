using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YemenGuide.Core.Constants;

namespace YemenGuide.Core.DTOs.Customer
{
    /// <summary>
    /// خيار بسيط تستخدمه واجهات العميل للقوائم والأزرار الاختيارية.
    /// </summary>
    public sealed class CustomerReportOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
    }

    /// <summary>
    /// مرجع يستطيع العميل ربط القضية به مثل حجز أو منشأة.
    /// </summary>
    public sealed class CustomerReportReferenceOptionDto
    {
        public string ReferenceType { get; set; } = string.Empty;
        public int ReferenceId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public int? PlaceId { get; set; }
        public string? PlaceName { get; set; }
        public DateTime? Date { get; set; }
    }

    /// <summary>
    /// نموذج الفلاتر في صفحة "الشكاوى والبلاغات" لدى العميل.
    /// </summary>
    public sealed class CustomerReportsFilterDto
    {
        public string StatusGroup { get; set; } = ReportConstants.StatusGroups.All;
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = ReportConstants.Limits.DefaultPageSize;
    }

    public sealed class CustomerReportSummaryDto
    {
        public int TotalCount { get; set; }
        public int NeedsResponseCount { get; set; }
        public int ActiveCount { get; set; }
        public int CompletedCount { get; set; }
    }

    public sealed class CustomerReportListItemDto
    {
        public int ReportId { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string RequestKind { get; set; } = ReportConstants.RequestKinds.Report;
        public string RequestKindLabel { get; set; } = "بلاغ";
        public string ReportType { get; set; } = ReportConstants.Types.Other;
        public string ReportTypeLabel { get; set; } = "أخرى";
        public string? OtherReportTypeDescription { get; set; }
        public string? Category { get; set; }
        public string? CategoryLabel { get; set; }
        public string? OtherCategoryDescription { get; set; }
        public string? RequiredAction { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReportStatus { get; set; } = ReportConstants.Statuses.Pending;
        public string StatusLabel { get; set; } = "جديد";
        public string StatusTone { get; set; } = "new";
        public string Priority { get; set; } = ReportConstants.Priorities.Normal;
        public int? PlaceId { get; set; }
        public string? PlaceName { get; set; }
        public int? BookingId { get; set; }
        public int? ReviewId { get; set; }
        public int? PromotionId { get; set; }
        public int? AdvertisementId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ReplyDeadline { get; set; }
        public bool NeedsCustomerAction { get; set; }
        public bool IsCompleted { get; set; }
    }

    public sealed class CustomerReportsPageDto
    {
        public CustomerReportsFilterDto Filter { get; set; } = new CustomerReportsFilterDto();
        public CustomerReportSummaryDto Summary { get; set; } = new CustomerReportSummaryDto();
        public IReadOnlyList<CustomerReportListItemDto> Items { get; set; } = Array.Empty<CustomerReportListItemDto>();
        public int FilteredCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = ReportConstants.Limits.DefaultPageSize;
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    /// <summary>
    /// البيانات التي يرسلها العميل عند إنشاء قضية جديدة.
    /// المالك أو المستخدم المبلّغ ضده لا يُقبل من المتصفح؛ الخدمة تستخرجه من المرجع.
    /// </summary>
    public sealed class CustomerCreateReportRequestDto
    {
        [Required(ErrorMessage = "اختر نوع الطلب.")]
        [StringLength(20)]
        public string RequestKind { get; set; } = ReportConstants.RequestKinds.Report;

        [Required(ErrorMessage = "اختر موضوع الطلب.")]
        [StringLength(30)]
        public string ReportType { get; set; } = ReportConstants.Types.Booking;

        [StringLength(
            ReportConstants.Limits.CategoryMaxLength,
            ErrorMessage = "تصنيف الطلب طويل جدًا.")]
        public string? Category { get; set; }

        [StringLength(
            ReportConstants.Limits.OtherDescriptionMaxLength,
            ErrorMessage = "وصف الخيار الآخر يجب ألا يتجاوز 50 حرفًا.")]
        public string? OtherTypeDescription { get; set; }

        [StringLength(
            ReportConstants.Limits.OtherDescriptionMaxLength,
            ErrorMessage = "وصف التصنيف الآخر يجب ألا يتجاوز 50 حرفًا.")]
        public string? OtherCategoryDescription { get; set; }

        [Required(ErrorMessage = "اكتب عنوانًا مختصرًا للمشكلة.")]
        [StringLength(
            ReportConstants.Limits.TitleMaxLength,
            MinimumLength = 5,
            ErrorMessage = "عنوان الطلب يجب أن يكون بين 5 و200 حرف.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اشرح المشكلة بوضوح.")]
        [StringLength(
            ReportConstants.Limits.ReasonMaxLength,
            MinimumLength = 15,
            ErrorMessage = "تفاصيل المشكلة يجب أن تكون بين 15 و4000 حرف.")]
        public string ReportReason { get; set; } = string.Empty;

        public int? BookingId { get; set; }
        public int? PlaceId { get; set; }
        public int? ReviewId { get; set; }
        public int? PromotionId { get; set; }
        public int? AdvertisementId { get; set; }
    }

    public sealed class CustomerCreateReportPageDto
    {
        public CustomerCreateReportRequestDto Form { get; set; } = new CustomerCreateReportRequestDto();
        public IReadOnlyList<CustomerReportOptionDto> RequestKinds { get; set; } = Array.Empty<CustomerReportOptionDto>();
        public IReadOnlyList<CustomerReportOptionDto> ReportTypes { get; set; } = Array.Empty<CustomerReportOptionDto>();
        public IReadOnlyList<CustomerReportOptionDto> Categories { get; set; } = Array.Empty<CustomerReportOptionDto>();
        public IReadOnlyList<CustomerReportReferenceOptionDto> RecentBookings { get; set; } = Array.Empty<CustomerReportReferenceOptionDto>();
        public IReadOnlyList<CustomerReportReferenceOptionDto> RelatedPlaces { get; set; } = Array.Empty<CustomerReportReferenceOptionDto>();
        public CustomerReportReferenceOptionDto? SelectedReference { get; set; }
    }

    public sealed class CustomerReportTimelineItemDto
    {
        public DateTime Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Actor { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-circle-fill";
        public string Tone { get; set; } = "info";
        public string? AttachmentUrl { get; set; }
        public bool IsCustomerMessage { get; set; }
        public bool IsOwnerMessage { get; set; }
        public bool IsAdminMessage { get; set; }
    }

    public sealed class CustomerReportDetailsDto
    {
        public int ReportId { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string RequestKind { get; set; } = ReportConstants.RequestKinds.Report;
        public string RequestKindLabel { get; set; } = "بلاغ";
        public string ReportType { get; set; } = ReportConstants.Types.Other;
        public string ReportTypeLabel { get; set; } = "أخرى";
        public string? OtherReportTypeDescription { get; set; }
        public string? Category { get; set; }
        public string? CategoryLabel { get; set; }
        public string? OtherCategoryDescription { get; set; }
        public string? RequiredAction { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReportReason { get; set; } = string.Empty;
        public string ReportStatus { get; set; } = ReportConstants.Statuses.Pending;
        public string StatusLabel { get; set; } = "جديد";
        public string StatusTone { get; set; } = "new";
        public string Priority { get; set; } = ReportConstants.Priorities.Normal;
        public string? EvidenceImage { get; set; }
        public string? AdminComment { get; set; }
        public string? Resolution { get; set; }
        public string? ResolutionAction { get; set; }
        public string? WithdrawnReason { get; set; }
        public int? PlaceId { get; set; }
        public string? PlaceName { get; set; }
        public int? BookingId { get; set; }
        public DateTime? BookingStartDate { get; set; }
        public DateTime? BookingEndDate { get; set; }
        public string? BookingState { get; set; }
        public int? ReviewId { get; set; }
        public int? PromotionId { get; set; }
        public int? AdvertisementId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ReporterReplyDeadline { get; set; }
        public DateTime? ReportedPartyReplyDeadline { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? WithdrawnAt { get; set; }
        public bool NeedsCustomerAction { get; set; }
        public bool CanAddClarification { get; set; }
        public bool CanConfirmResolution { get; set; }
        public bool CanRequestReopen { get; set; }
        public bool CanWithdraw { get; set; }
        public IReadOnlyList<CustomerReportTimelineItemDto> Timeline { get; set; } = Array.Empty<CustomerReportTimelineItemDto>();
    }

    public sealed class CustomerReportClarificationRequestDto
    {
        [StringLength(
            ReportConstants.Limits.ClarificationMaxLength,
            ErrorMessage = "التوضيح يجب ألا يتجاوز 4000 حرف.")]
        public string? Message { get; set; }
    }

    public sealed class CustomerReportResolutionConfirmationDto
    {
        public bool IsResolved { get; set; }

        [StringLength(
            ReportConstants.Limits.ClarificationMaxLength,
            ErrorMessage = "التوضيح يجب ألا يتجاوز 4000 حرف.")]
        public string? Note { get; set; }
    }

    public sealed class CustomerReportReopenRequestDto
    {
        [Required(ErrorMessage = "اكتب سبب طلب إعادة المراجعة.")]
        [StringLength(
            ReportConstants.Limits.ClarificationMaxLength,
            MinimumLength = 10,
            ErrorMessage = "سبب إعادة المراجعة يجب أن يكون بين 10 و4000 حرف.")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class CustomerReportWithdrawRequestDto
    {
        [Required(ErrorMessage = "اكتب سبب سحب القضية.")]
        [StringLength(
            ReportConstants.Limits.WithdrawReasonMaxLength,
            ErrorMessage = "سبب السحب يجب ألا يتجاوز 500 حرف.")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// نتيجة موحدة لجميع عمليات القضايا، لتبقى استجابات Controller وAJAX بسيطة.
    /// </summary>
    public sealed class CustomerReportOperationResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public int? ReportId { get; set; }
        public string? ReportNumber { get; set; }

        public static CustomerReportOperationResultDto Ok(
            string message,
            int? reportId = null,
            string? reportNumber = null)
        {
            return new CustomerReportOperationResultDto
            {
                Success = true,
                Message = message,
                ReportId = reportId,
                ReportNumber = reportNumber
            };
        }

        public static CustomerReportOperationResultDto Fail(
            string message,
            string? errorCode = null)
        {
            return new CustomerReportOperationResultDto
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode
            };
        }
    }
}
