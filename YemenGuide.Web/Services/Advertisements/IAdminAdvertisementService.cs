using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Admin;

namespace YemenGuide.Web.Services.Advertisements
{
    public interface IAdminAdvertisementService
    {
        Task<AdminAdvertisementManagementPageDto> GetManagementPageAsync(int adminUserId, string? tab, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
        Task<AdminAdvertisementsTabDto> GetAdvertisementsAsync(AdminAdvertisementFilterDto filter, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementDetailsDto?> GetAdvertisementDetailsAsync(int advertisementId, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminPlacementPlansTabDto> GetPlansAsync(int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementPlanUpsertDto?> GetPlanFormAsync(int? planId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementReportsDto> GetReportsAsync(AdminAdvertisementReportFilterDto filter, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOperationResult> SavePlanAsync(AdminAdvertisementPlanUpsertDto request, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOperationResult> ChangePlanStatusAsync(AdminAdvertisementPlanStatusDto request, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOperationResult> DeletePlanAsync(int advertisementPlanId, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOperationResult> ReviewPaymentAsync(AdminAdvertisementPaymentReviewDto request, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOperationResult> ReviewAdvertisementAsync(AdminAdvertisementReviewDto request, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOperationResult> ChangeAdvertisementStatusAsync(AdminAdvertisementControlDto request, int adminUserId, CancellationToken cancellationToken = default);
        Task<AdminAdvertisementOperationResult> ProcessRefundAsync(AdminAdvertisementRefundDto request, int adminUserId, CancellationToken cancellationToken = default);
    }
}
