using System.Threading;
using System.Threading.Tasks;

namespace YemenGuide.Web.Services.Employees
{
    public interface IAdminAuditService
    {
        Task LogAsync(
            int? userId,
            string actionType,
            string? entityType = null,
            string? entityId = null,
            string? description = null,
            object? oldValues = null,
            object? newValues = null,
            bool isSuccessful = true,
            string? failureReason = null,
            CancellationToken cancellationToken = default);
    }
}
