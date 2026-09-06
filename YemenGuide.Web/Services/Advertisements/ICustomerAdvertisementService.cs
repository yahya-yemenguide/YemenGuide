using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Customer;

namespace YemenGuide.Web.Services.Advertisements
{
    public interface ICustomerAdvertisementService
    {
        Task<CustomerHomeAdvertisementsDto> GetHomeAdvertisementsAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CustomerAdvertisementDto>> GetDiscoveryAdvertisementsAsync(
            CustomerAdvertisementDiscoveryRequestDto request,
            CancellationToken cancellationToken = default);

        Task<CustomerAdvertisementDto?> GetDiscoveryAdvertisementAsync(
            CustomerAdvertisementDiscoveryRequestDto request,
            CancellationToken cancellationToken = default);

        Task<CustomerAdvertisementDto?> GetPlaceDetailsAdvertisementAsync(
            CustomerAdvertisementPlaceDetailsRequestDto request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CustomerAdvertisementDto>> GetMapAdvertisementsAsync(
            CustomerAdvertisementMapRequestDto request,
            CancellationToken cancellationToken = default);

        Task<int?> ResolveAttributionAdvertisementIdAsync(
            int? advertisementId,
            int placeId,
            CancellationToken cancellationToken = default);

        Task<CustomerAdvertisementTrackResultDto> RecordBookingStartedAsync(
            CustomerAdvertisementBookingStartRequestDto request,
            CancellationToken cancellationToken = default);

        Task RecordBookingCompletedAsync(
            int advertisementId,
            int bookingId,
            int placeId,
            CancellationToken cancellationToken = default);

        Task<CustomerAdvertisementTrackResultDto> RecordImpressionAsync(
            CustomerAdvertisementTrackRequestDto request,
            CancellationToken cancellationToken = default);

        Task<CustomerAdvertisementTrackResultDto> RecordClickAsync(
            CustomerAdvertisementTrackRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
