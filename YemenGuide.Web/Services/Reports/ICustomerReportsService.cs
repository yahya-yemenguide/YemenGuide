using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YemenGuide.Core.DTOs.Customer;

namespace YemenGuide.Web.Services.Reports
{
    /// <summary>
    /// واجهة نظام الشكاوى والبلاغات لدى العميل.
    ///
    /// جميع العمليات محصورة في قضايا العميل نفسه، ولا يقبل أي إجراء
    /// CustomerId أو OwnerId قادمًا من المتصفح كمرجع موثوق.
    /// </summary>
    public interface ICustomerReportsService
    {
        Task<CustomerReportsPageDto> GetMyReportsAsync(
            int customerId,
            CustomerReportsFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<CustomerCreateReportPageDto> GetCreatePageAsync(
            int customerId,
            CustomerCreateReportRequestDto? form = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CustomerReportReferenceOptionDto>> SearchBookingsAsync(
            int customerId,
            string? search,
            int take = 10,
            int skip = 0,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CustomerReportReferenceOptionDto>> SearchPlacesAsync(
            int customerId,
            string? search,
            int take = 12,
            CancellationToken cancellationToken = default);

        Task<CustomerReportDetailsDto?> GetDetailsAsync(
            int customerId,
            int reportId,
            CancellationToken cancellationToken = default);

        Task<CustomerReportOperationResultDto> CreateAsync(
            int customerId,
            CustomerCreateReportRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default);

        Task<CustomerReportOperationResultDto> AddClarificationAsync(
            int customerId,
            int reportId,
            CustomerReportClarificationRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default);

        Task<CustomerReportOperationResultDto> ConfirmResolutionAsync(
            int customerId,
            int reportId,
            CustomerReportResolutionConfirmationDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default);

        Task<CustomerReportOperationResultDto> RequestReopenAsync(
            int customerId,
            int reportId,
            CustomerReportReopenRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default);

        Task<CustomerReportOperationResultDto> WithdrawAsync(
            int customerId,
            int reportId,
            CustomerReportWithdrawRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
