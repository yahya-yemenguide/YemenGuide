using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Owner;

namespace YemenGuide.Web.Services.OwnerDashboard
{
    public interface IOwnerDashboardService
    {
        Task<OwnerDashboardDto> GetDashboardAsync(
            int ownerId,
            int? requestedPlaceId = null,
            CancellationToken cancellationToken = default);
    }
}
