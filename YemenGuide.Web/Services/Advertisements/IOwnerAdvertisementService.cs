using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Owner;

namespace YemenGuide.Web.Services.Advertisements
{
    public interface IOwnerAdvertisementService
    {
        Task<OwnerAdvertisementManagementPageDto> GetManagementPageAsync(
            int ownerId,
            int? requestedPlaceId,
            string? requestedTab,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOverviewDto> GetOverviewAsync(
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementsTabDto> GetAdvertisementsAsync(
            OwnerAdvertisementFilterDto filter,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementWizardDto> GetWizardAsync(
            int? advertisementId,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementPriceQuoteDto> GetPriceQuoteAsync(
            OwnerAdvertisementPriceQuoteRequestDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementDetailsDto?> GetDetailsAsync(
            int advertisementId,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> SaveDraftAsync(
            OwnerAdvertisementSaveDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> SubmitAsync(
            OwnerAdvertisementSubmitDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> SavePaymentProofAsync(
            OwnerAdvertisementPaymentSaveDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> AddMediaAsync(
            OwnerAdvertisementMediaSaveDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> DeleteMediaAsync(
            OwnerAdvertisementMediaDeleteDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> PauseAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> ResumeAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> CancelAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> RestoreAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerAdvertisementOperationResultDto> RenewAsync(
            OwnerAdvertisementRenewDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default);
    }
}
