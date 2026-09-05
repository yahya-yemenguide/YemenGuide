using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace YemenGuide.Web.Maintenance
{
    public interface IMaintenanceService
    {
        Task<MaintenanceState> GetStateAsync(
            CancellationToken cancellationToken = default);

        bool IsUserAffected(
            ClaimsPrincipal? user,
            MaintenanceState state);

        Task<bool> IsOperationBlockedAsync(
            MaintenanceOperation operation,
            ClaimsPrincipal? user,
            CancellationToken cancellationToken = default);
    }
}
