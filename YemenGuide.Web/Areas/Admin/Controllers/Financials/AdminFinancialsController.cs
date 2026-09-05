using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Admin.Controllers.Financials
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [RequireAdminPermission(AdminPermissionKeys.Finance.View)]
    [Route("Admin/Financials")]
    public sealed class AdminFinancialsController : Controller
    {
        private const string MainViewPath =
            "~/Areas/Admin/Views/AdminFinancials/ManageAdminFinancials.cshtml";

        private static readonly HashSet<string> AllowedTabs =
            new(AdminFinancialTabs.All, StringComparer.OrdinalIgnoreCase);

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly IAdminPermissionService _adminPermissionService;
        private readonly ILogger<AdminFinancialsController> _logger;

        public AdminFinancialsController(
            IAdminFinancialService adminFinancialService,
            IAdminPermissionService adminPermissionService,
            ILogger<AdminFinancialsController> logger)
        {
            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _adminPermissionService = adminPermissionService ??
                throw new ArgumentNullException(nameof(adminPermissionService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Manage")]
        public async Task<IActionResult> ManageAdminFinancials(
            [FromQuery] string? financeTab = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Forbid();
            }

            try
            {
                DisableResponseCaching();

                var permissions =
                    await GetPermissionSetAsync(
                        adminUserId,
                        cancellationToken);

                var access = BuildAccess(permissions);
                if (!access.CanView)
                {
                    return Forbid();
                }

                var requestedTab = NormalizeRequestedTab(financeTab);
                var activeTab = IsTabAllowed(requestedTab, access)
                    ? requestedTab
                    : GetFirstAllowedTab(access);

                var shell =
                    await _adminFinancialService.GetShellAsync(
                        activeTab,
                        cancellationToken);

                shell.SelectedTab = activeTab;

                var model = new AdminFinancialManagePageDto
                {
                    Shell = shell,
                    Access = access
                };

                ViewData["Title"] = "الإدارة المالية";

                return View(MainViewPath, model);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء فتح الإدارة المالية للأدمن {AdminUserId}.",
                    adminUserId);

                return StatusCode(
                    500,
                    "تعذر فتح الإدارة المالية. أعد المحاولة.");
            }
        }

        /// <summary>
        /// يعيد بيانات الغلاف والعدادات والصلاحيات للواجهة بعد تنفيذ عملية
        /// مالية أو عند الضغط على تحديث، دون إعادة تحميل الصفحة كاملة.
        /// </summary>
        [HttpGet("Shell")]
        public async Task<IActionResult> Shell(
            [FromQuery] string? financeTab = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Forbid();
            }

            try
            {
                DisableResponseCaching();

                var permissions =
                    await GetPermissionSetAsync(
                        adminUserId,
                        cancellationToken);

                var access = BuildAccess(permissions);
                if (!access.CanView)
                {
                    return Forbid();
                }

                var requestedTab = NormalizeRequestedTab(financeTab);
                var activeTab = IsTabAllowed(requestedTab, access)
                    ? requestedTab
                    : GetFirstAllowedTab(access);

                var shell =
                    await _adminFinancialService.GetShellAsync(
                        activeTab,
                        cancellationToken);

                shell.SelectedTab = activeTab;

                return Json(new
                {
                    success = true,
                    selectedTab = activeTab,
                    shell,
                    access
                });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء تحديث غلاف الإدارة المالية للأدمن {AdminUserId}.",
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تحديث بيانات الإدارة المالية."
                });
            }
        }

        private async Task<HashSet<string>> GetPermissionSetAsync(
            int adminUserId,
            CancellationToken cancellationToken)
        {
            var permissionKeys =
                await _adminPermissionService.GetPermissionKeysAsync(
                    adminUserId,
                    cancellationToken);

            return permissionKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static AdminFinancialAccessDto BuildAccess(
            HashSet<string> permissions)
        {
            return new AdminFinancialAccessDto
            {
                CanView = permissions.Contains(
                    AdminPermissionKeys.Finance.View),

                CanReviewTopUps = permissions.Contains(
                    AdminPermissionKeys.Finance.ReviewTopUps),

                CanManageCommissionAccounts = permissions.Contains(
                    AdminPermissionKeys.Finance.ManageCommissionAccounts),

                CanManagePlatformWallets = permissions.Contains(
                    AdminPermissionKeys.Finance.ManagePlatformWallets),

                CanManageSettings = permissions.Contains(
                    AdminPermissionKeys.Finance.ManageSettings),

                CanViewReports = permissions.Contains(
                    AdminPermissionKeys.Finance.Reports)
            };
        }

        private static string NormalizeRequestedTab(string? financeTab)
        {
            if (string.IsNullOrWhiteSpace(financeTab))
            {
                return AdminFinancialTabs.Overview;
            }

            var normalized = financeTab.Trim().ToLowerInvariant();

            return AllowedTabs.Contains(normalized)
                ? normalized
                : AdminFinancialTabs.Overview;
        }

        private static bool IsTabAllowed(
            string tab,
            AdminFinancialAccessDto access)
        {
            return tab switch
            {
                AdminFinancialTabs.Overview =>
                    access.CanView,

                AdminFinancialTabs.CommissionGuarantee =>
                    access.CanView,

                AdminFinancialTabs.TopUps =>
                    access.CanReviewTopUps,

                AdminFinancialTabs.Transactions =>
                    access.CanView,

                AdminFinancialTabs.Reports =>
                    access.CanViewReports,

                _ => false
            };
        }

        private static string GetFirstAllowedTab(
            AdminFinancialAccessDto access)
        {
            if (access.CanView)
            {
                return AdminFinancialTabs.Overview;
            }

            if (access.CanReviewTopUps)
            {
                return AdminFinancialTabs.TopUps;
            }

            if (access.CanViewReports)
            {
                return AdminFinancialTabs.Reports;
            }

            return AdminFinancialTabs.Overview;
        }

        private bool TryGetCurrentAdminUserId(out int adminUserId)
        {
            adminUserId = 0;

            var rawValue =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("UserID") ??
                User.FindFirstValue("UserId") ??
                User.FindFirstValue("user_id") ??
                User.FindFirstValue("sub");

            return int.TryParse(rawValue, out adminUserId) &&
                   adminUserId > 0;
        }

        private void DisableResponseCaching()
        {
            Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }
    }
}
