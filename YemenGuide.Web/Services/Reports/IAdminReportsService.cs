using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Admin;

namespace YemenGuide.Web.Services.Reports
{
    public interface IAdminReportsService
    {
        Task<AdminReportsCenterDto> GetReportsCenterAsync(
            string? activeTab,
            AdminReportFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminReportSummaryDto> GetSummaryAsync(
            CancellationToken cancellationToken = default);

        Task<AdminReportsListPageDto> GetReportsListAsync(
            string? tab,
            AdminReportFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminReportDetailsDto?> GetReportDetailsAsync(
            int reportId,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> SendReporterMessageAsync(
            AdminReportReporterMessageDto input, int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> StartReviewAsync(
            AdminReportSimpleActionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> RequestClarificationAsync(
            AdminReportReplyRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> RequestOwnerResponseAsync(
            AdminReportReplyRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> RequestResolutionConfirmationAsync(
            AdminReportResolutionConfirmationRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> ChangePriorityAsync(
            AdminReportPriorityDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> EscalateAsync(
            AdminReportEscalationDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> AddInternalNoteAsync(
            AdminReportInternalNoteRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> ResolveAsync(
            AdminReportResolutionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> RejectAsync(
            AdminReportRejectionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> ConfirmCustomerViolationAsync(
            AdminCustomerViolationDecisionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> DismissCustomerViolationAsync(
            AdminCustomerViolationDecisionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> CloseAsync(
            AdminReportCloseDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);

        Task<AdminReportOperationResultDto> ReopenAsync(
            AdminReportReopenDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default);
    }
}
