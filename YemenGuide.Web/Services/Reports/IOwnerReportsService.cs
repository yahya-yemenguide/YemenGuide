using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YemenGuide.Core.DTOs.Owner;

namespace YemenGuide.Web.Services.Reports
{
    public interface IOwnerReportsService
    {
        Task<OwnerReportsPageDto> GetReportsPageAsync(
            int ownerId,
            OwnerReportsFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<OwnerCreateReportPageDto> GetCreatePageAsync(
            int ownerId,
            OwnerCreateReportRequestDto? form = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OwnerReportReferenceOptionDto>> SearchBookingsAsync(
            int ownerId,
            string? search,
            int take = 10,
            int skip = 0,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OwnerReportReferenceOptionDto>> SearchReviewsAsync(
            int ownerId,
            string? search,
            int take = 10,
            int skip = 0,
            CancellationToken cancellationToken = default);

        Task<OwnerReportDetailsDto?> GetDetailsAsync(
            int ownerId,
            int reportId,
            CancellationToken cancellationToken = default);

        Task<OwnerReportOperationResultDto> CreateAsync(
            int ownerId,
            OwnerCreateReportRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default);

        Task<OwnerReportOperationResultDto> EditAsync(
            int ownerId, int reportId, OwnerReportEditRequestDto request,
            IFormFile? evidenceFile, CancellationToken cancellationToken = default);
        Task<OwnerReportOperationResultDto> WithdrawAsync(
            int ownerId, int reportId, OwnerReportWithdrawRequestDto request,
            CancellationToken cancellationToken = default);
        Task<OwnerReportOperationResultDto> RequestReopenAsync(
            int ownerId, int reportId, OwnerReportReopenRequestDto request,
            IFormFile? evidenceFile, CancellationToken cancellationToken = default);

        Task<OwnerReportOperationResultDto> SubmitResponseAsync(
            int ownerId,
            int reportId,
            OwnerReportResponseRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default);
    }
}
