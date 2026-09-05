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
    [Route("Admin/Financials/Management")]
    public sealed class AdminFinancialManagementController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminFinancialManagement.cshtml";

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly IAdminPermissionService _adminPermissionService;
        private readonly ILogger<AdminFinancialManagementController> _logger;

        public AdminFinancialManagementController(
            IAdminFinancialService adminFinancialService,
            IAdminPermissionService adminPermissionService,
            ILogger<AdminFinancialManagementController> logger)
        {
            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _adminPermissionService = adminPermissionService ??
                throw new ArgumentNullException(nameof(adminPermissionService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            [FromQuery] AdminPlatformWalletFilterDto? walletFilter,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Forbid();
            }

            try
            {
                DisableResponseCaching();

                var canManageSettings =
                    await _adminPermissionService.HasPermissionAsync(
                        adminUserId,
                        AdminPermissionKeys.Finance.ManageSettings,
                        cancellationToken);

                var canManagePlatformWallets =
                    await _adminPermissionService.HasPermissionAsync(
                        adminUserId,
                        AdminPermissionKeys.Finance.ManagePlatformWallets,
                        cancellationToken);

                if (!canManageSettings && !canManagePlatformWallets)
                {
                    return Forbid();
                }

                var model = new AdminFinancialManagementPageDto
                {
                    LoadedAt = DateTime.Now
                };

                // تحميل كل قسم حسب صلاحية الموظف فقط حتى لا تُرسل بيانات
                // قسم غير مسموح به إلى المتصفح.
                if (canManageSettings)
                {
                    model.Settings =
                        await _adminFinancialService.GetFinancialSettingsAsync(
                            cancellationToken);

                    model.Currency =
                        string.IsNullOrWhiteSpace(
                            model.Settings.Settings.DefaultCurrency)
                            ? "ريال يمني"
                            : model.Settings.Settings.DefaultCurrency.Trim();
                }
                else
                {
                    model.Settings = new AdminFinancialSettingsPageDto();
                }

                if (canManagePlatformWallets)
                {
                    model.PlatformWallets =
                        await _adminFinancialService.GetPlatformWalletsAsync(
                            walletFilter ?? new AdminPlatformWalletFilterDto(),
                            cancellationToken);

                    if (!canManageSettings)
                    {
                        var shell =
                            await _adminFinancialService.GetShellAsync(
                                AdminFinancialTabs.Overview,
                                cancellationToken);

                        model.Currency = string.IsNullOrWhiteSpace(shell.Currency)
                            ? "ريال يمني"
                            : shell.Currency.Trim();
                    }
                }
                else
                {
                    model.PlatformWallets = new AdminPlatformWalletPageDto();
                }

                ViewData["CanManageSettings"] = canManageSettings;
                ViewData["CanManagePlatformWallets"] =
                    canManagePlatformWallets;

                return PartialView(
                    PartialViewPath,
                    model);
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
                    "حدث خطأ أثناء تحميل نافذة إدارة المالية للأدمن {AdminUserId}.",
                    adminUserId);

                return StatusCode(
                    500,
                    "تعذر تحميل إدارة المالية. أعد المحاولة.");
            }
        }

        [HttpPost("Settings/Save")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManageSettings)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(
            [FromForm] AdminFinancialSettingsUpdateDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات الإعدادات المالية غير مكتملة."
                });
            }

            request.DefaultCurrency =
                request.DefaultCurrency?.Trim() ?? string.Empty;
            request.ChangeReason =
                request.ChangeReason?.Trim() ?? string.Empty;
            request.CategoryCommissions ??=
                new List<AdminCategoryCommissionUpdateDto>();

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.SaveFinancialSettingsAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                if (!result.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message
                    });
                }

                var updated =
                    await _adminFinancialService.GetFinancialSettingsAsync(
                        cancellationToken);

                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    entityId = result.EntityId,
                    settings = updated.Settings,
                    categoryCommissions = updated.CategoryCommissions,
                    loadedAt = updated.LoadedAt
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
                    "حدث خطأ أثناء حفظ الإعدادات المالية بواسطة الأدمن {AdminUserId}.",
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر حفظ الإعدادات المالية حاليًا."
                });
            }
        }

        [HttpPost("Wallets/Save")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManagePlatformWallets)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveWallet(
            [FromForm] AdminPlatformWalletEditDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات حساب المنصة غير مكتملة."
                });
            }

            request.AccountNumber =
                request.AccountNumber?.Trim() ?? string.Empty;
            request.BeneficiaryName =
                request.BeneficiaryName?.Trim() ?? string.Empty;
            request.Instructions = string.IsNullOrWhiteSpace(request.Instructions)
                ? null
                : request.Instructions.Trim();

            if (request.PlatformWalletId.HasValue &&
                request.PlatformWalletId.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(request.PlatformWalletId),
                    "رقم حساب المنصة غير صحيح.");
            }

            if (request.IsPrimary && !request.IsActive)
            {
                ModelState.AddModelError(
                    nameof(request.IsPrimary),
                    "لا يمكن جعل حساب موقوف هو الحساب الرئيسي.");
            }

            if (request.Instructions?.Length > 2000)
            {
                ModelState.AddModelError(
                    nameof(request.Instructions),
                    "تعليمات التحويل يجب ألا تتجاوز 2000 حرف.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.SavePlatformWalletAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
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
                    "حدث خطأ أثناء حفظ حساب المنصة {PlatformWalletId} بواسطة الأدمن {AdminUserId}.",
                    request.PlatformWalletId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر حفظ حساب المنصة. لم يتم تنفيذ التغيير."
                });
            }
        }

        [HttpPost("Wallets/Status")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManagePlatformWallets)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeWalletStatus(
            [FromForm] AdminPlatformWalletStatusRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات تغيير حالة الحساب غير مكتملة."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.ChangePlatformWalletStatusAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    isActive = request.IsActive
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
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
                    "حدث خطأ أثناء تغيير حالة حساب المنصة {PlatformWalletId} بواسطة الأدمن {AdminUserId}.",
                    request.PlatformWalletId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تغيير حالة حساب المنصة. لم يتم تنفيذ التغيير."
                });
            }
        }

        [HttpPost("Wallets/SetPrimary")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManagePlatformWallets)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryWallet(
            [FromForm] AdminSetPrimaryPlatformWalletRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات تعيين الحساب الرئيسي غير مكتملة."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.SetPrimaryPlatformWalletAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
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
                    "حدث خطأ أثناء تعيين حساب المنصة {PlatformWalletId} كحساب رئيسي بواسطة الأدمن {AdminUserId}.",
                    request.PlatformWalletId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تعيين الحساب الرئيسي. لم يتم تنفيذ التغيير."
                });
            }
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

        private string FirstModelError()
        {
            return ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error =>
                    !string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.ErrorMessage
                        : error.Exception?.Message)
                .FirstOrDefault(message =>
                    !string.IsNullOrWhiteSpace(message)) ??
                "البيانات المرسلة غير صحيحة.";
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
