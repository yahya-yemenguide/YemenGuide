using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Admin;

namespace YemenGuide.Web.Services.Employees
{
    public interface IAdminEmployeeService
    {
        Task<AdminEmployeeManagementPageDto> GetManagementPageAsync(
            int currentAdminUserId,
            string? requestedTab,
            CancellationToken cancellationToken = default);

        Task<AdminEmployeesTabDto> GetEmployeesAsync(
            AdminEmployeeFilterDto filter,
            int currentAdminUserId,
            CancellationToken cancellationToken = default);

        Task<AdminEmployeeFormDto?> GetEmployeeFormAsync(
            int? userId,
            CancellationToken cancellationToken = default);

        Task<AdminOperationResultDto> CreateEmployeeAsync(
            AdminEmployeeSaveDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);

        Task<AdminOperationResultDto> UpdateEmployeeAsync(
            AdminEmployeeSaveDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);

        Task<AdminOperationResultDto> ChangeEmployeeStatusAsync(
            AdminEmployeeStatusDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);


        Task<AdminRolesTabDto> GetRolesAsync(
            string? search,
            int currentAdminUserId,
            CancellationToken cancellationToken = default);

        Task<AdminRoleFormDto?> GetRoleFormAsync(
            int? adminRoleId,
            CancellationToken cancellationToken = default);

        Task<AdminOperationResultDto> SaveRoleAsync(
            AdminRoleSaveDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);

        Task<AdminOperationResultDto> ChangeRoleStatusAsync(
            AdminRoleStatusDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);

        Task<AdminAuditLogsTabDto> GetAuditLogsAsync(
            AdminAuditLogFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<bool> HasAnyPermissionAsync(
            int currentAdminUserId,
            CancellationToken cancellationToken = default,
            params string[] permissionKeys);

        Task<(bool usernameAvailable, bool emailAvailable)> CheckAccountAvailabilityAsync(
            string? username,
            string? email,
            int? excludeUserId,
            CancellationToken cancellationToken = default);
    }
}
