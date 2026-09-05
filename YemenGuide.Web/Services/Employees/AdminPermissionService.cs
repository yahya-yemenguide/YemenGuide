using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Data;

namespace YemenGuide.Web.Services.Employees
{
    public sealed class AdminPermissionService : IAdminPermissionService
    {
        private readonly AppDbContext _context;

        public AdminPermissionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CanAccessAdminAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var state = await GetAdminStateAsync(userId, cancellationToken);
            return IsAllowedAdminState(state);
        }

        public async Task<bool> IsSuperAdminAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var state = await GetAdminStateAsync(userId, cancellationToken);

            return IsAllowedAdminState(state) &&
                   state!.IsSystemRole;
        }

        public async Task<bool> HasPermissionAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(permissionKey))
            {
                return false;
            }

            var normalizedKey = permissionKey.Trim();
            var state = await GetAdminStateAsync(userId, cancellationToken);

            if (!IsAllowedAdminState(state))
            {
                return false;
            }

            if (state!.IsSystemRole)
            {
                return await _context.AdminPermissions
                    .AsNoTracking()
                    .AnyAsync(permission =>
                        permission.IsActive == true &&
                        permission.PermissionKey == normalizedKey,
                        cancellationToken);
            }

            var rolePermissionKeys = await _context.AdminRolePermissions
                .AsNoTracking()
                .Where(link =>
                    link.AdminRoleId == state.AdminRoleId &&
                    link.AdminPermission.IsActive == true)
                .Select(link => link.AdminPermission.PermissionKey)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (rolePermissionKeys.Contains(
                    normalizedKey,
                    StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            return HasImpliedViewPermission(
                normalizedKey,
                rolePermissionKeys);
        }

        public async Task<IReadOnlyCollection<string>> GetPermissionKeysAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var state = await GetAdminStateAsync(userId, cancellationToken);

            if (!IsAllowedAdminState(state))
            {
                return Array.Empty<string>();
            }

            if (state!.IsSystemRole)
            {
                return await _context.AdminPermissions
                    .AsNoTracking()
                    .Where(permission => permission.IsActive == true)
                    .OrderBy(permission => permission.PermissionGroup)
                    .ThenBy(permission => permission.PermissionName)
                    .Select(permission => permission.PermissionKey)
                    .ToListAsync(cancellationToken);
            }

            var rolePermissionKeys = await _context.AdminRolePermissions
                .AsNoTracking()
                .Where(link =>
                    link.AdminRoleId == state.AdminRoleId &&
                    link.AdminPermission.IsActive == true)
                .OrderBy(link => link.AdminPermission.PermissionGroup)
                .ThenBy(link => link.AdminPermission.PermissionName)
                .Select(link => link.AdminPermission.PermissionKey)
                .Distinct()
                .ToListAsync(cancellationToken);

            return ExpandImpliedViewPermissions(rolePermissionKeys);
        }

        public async Task<IReadOnlyList<int>> GetAdminUserIdsWithPermissionAsync(
            string permissionKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                return Array.Empty<int>();
            }

            var normalizedKey = permissionKey.Trim();

            // لا نوزع إشعارًا على مفتاح غير موجود أو معطل.
            var permissionExists = await _context.AdminPermissions
                .AsNoTracking()
                .AnyAsync(
                    permission =>
                        permission.IsActive == true &&
                        permission.PermissionKey == normalizedKey,
                    cancellationToken);

            if (!permissionExists)
            {
                return Array.Empty<int>();
            }

            var adminUserIds = await _context.Users
                .AsNoTracking()
                .Where(user =>
                    user.Role == "Admin" &&
                    user.AccountState == "Active" &&
                    user.EmploymentStatus == AdminEmployeeStatuses.Active &&
                    user.MustChangePassword == false &&
                    user.AdminRoleId.HasValue &&
                    user.AdminRole != null &&
                    user.AdminRole.IsActive == true &&
                    (
                        user.AdminRole.IsSystemRole ||
                        user.AdminRole.AdminRolePermissions.Any(link =>
                            link.AdminPermission.IsActive == true &&
                            link.AdminPermission.PermissionKey == normalizedKey)
                    ))
                .Select(user => user.UserId)
                .Distinct()
                .OrderBy(userId => userId)
                .ToListAsync(cancellationToken);

            return adminUserIds;
        }

        public async Task<IReadOnlyList<int>> GetAdminUserIdsWithAnyPermissionAsync(
            IReadOnlyCollection<string> permissionKeys,
            CancellationToken cancellationToken = default)
        {
            if (permissionKeys == null || permissionKeys.Count == 0)
            {
                return Array.Empty<int>();
            }

            var normalizedKeys = permissionKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedKeys.Length == 0)
            {
                return Array.Empty<int>();
            }

            var userIds = new HashSet<int>();

            foreach (var permissionKey in normalizedKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var matchingUserIds =
                    await GetAdminUserIdsWithPermissionAsync(
                        permissionKey,
                        cancellationToken);

                foreach (var userId in matchingUserIds)
                {
                    userIds.Add(userId);
                }
            }

            return userIds
                .OrderBy(userId => userId)
                .ToArray();
        }


        private static IReadOnlyCollection<string> ExpandImpliedViewPermissions(
            IReadOnlyCollection<string> permissionKeys)
        {
            var expanded = new HashSet<string>(
                permissionKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key.Trim()),
                StringComparer.OrdinalIgnoreCase);

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Users.View,
                "Users.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Places.View,
                "Places.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.MasterData.View,
                "MasterData.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Cases.View,
                "Cases.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Finance.View,
                "Finance.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Advertisements.View,
                "Advertisements.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Settings.View,
                "Settings.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Employees.View,
                "Employees.");

            AddImpliedView(
                expanded,
                AdminPermissionKeys.Roles.View,
                "Roles.");

            return expanded.ToArray();
        }

        private static bool HasImpliedViewPermission(
            string requestedPermissionKey,
            IReadOnlyCollection<string> assignedPermissionKeys)
        {
            var expanded =
                ExpandImpliedViewPermissions(assignedPermissionKeys);

            return expanded.Contains(
                requestedPermissionKey,
                StringComparer.OrdinalIgnoreCase);
        }

        private static void AddImpliedView(
            HashSet<string> permissionKeys,
            string viewPermissionKey,
            string modulePrefix)
        {
            if (permissionKeys.Contains(viewPermissionKey))
            {
                return;
            }

            if (permissionKeys.Any(key =>
                    key.StartsWith(
                        modulePrefix,
                        StringComparison.OrdinalIgnoreCase)))
            {
                permissionKeys.Add(viewPermissionKey);
            }
        }

        private async Task<AdminAccessState?> GetAdminStateAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            if (userId <= 0)
            {
                return null;
            }

            return await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => new AdminAccessState
                {
                    Role = user.Role,
                    AccountState = user.AccountState,
                    EmploymentStatus = user.EmploymentStatus,
                    MustChangePassword = user.MustChangePassword,
                    AdminRoleId = user.AdminRoleId,
                    AdminRoleIsActive = user.AdminRole != null &&
                                        user.AdminRole.IsActive == true,
                    IsSystemRole = user.AdminRole != null &&
                                   user.AdminRole.IsSystemRole
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static bool IsAllowedAdminState(AdminAccessState? state)
        {
            if (state == null)
            {
                return false;
            }

            if (!string.Equals(
                    state.Role,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(
                    state.AccountState,
                    "Active",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(
                    state.EmploymentStatus,
                    "Active",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (state.MustChangePassword)
            {
                return false;
            }

            return state.AdminRoleId.HasValue &&
                   state.AdminRoleId.Value > 0 &&
                   state.AdminRoleIsActive;
        }

        private sealed class AdminAccessState
        {
            public string? Role { get; set; }
            public string? AccountState { get; set; }
            public string? EmploymentStatus { get; set; }
            public bool MustChangePassword { get; set; }
            public int? AdminRoleId { get; set; }
            public bool AdminRoleIsActive { get; set; }
            public bool IsSystemRole { get; set; }
        }
    }
}
