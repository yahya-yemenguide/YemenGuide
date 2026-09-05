using System;
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
    [Route("Admin/Financials/CommissionGuarantee")]
    public sealed class AdminCommissionGuaranteeController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminCommissionGuarantee.cshtml";

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly IAdminPermissionService _adminPermissionService;
        private readonly ILogger<AdminCommissionGuaranteeController> _logger;

        public AdminCommissionGuaranteeController(
            IAdminFinancialService adminFinancialService,
            IAdminPermissionService adminPermissionService,
            ILogger<AdminCommissionGuaranteeController> logger)
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
            [FromQuery] AdminCommissionGuaranteeFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminCommissionGuaranteeFilterDto();

            try
            {
                DisableResponseCaching();

                var model =
                    await _adminFinancialService.GetCommissionGuaranteeAsync(
                        filter,
                        cancellationToken);

                ViewData["CanManageCommissionAccounts"] =
                    await CurrentAdminHasPermissionAsync(
                        AdminPermissionKeys.Finance.ManageCommissionAccounts,
                        cancellationToken);

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
                    "حدث خطأ أثناء تحميل تبويب العمولات والضمان.");

                return StatusCode(
                    500,
                    "تعذر تحميل العمولات والضمان. أعد المحاولة.");
            }
        }

        [HttpGet("Details/{placeCommissionAccountId:int}")]
        [HttpGet("Details")]
        public async Task<IActionResult> Details(
            int placeCommissionAccountId,
            CancellationToken cancellationToken = default)
        {
            if (placeCommissionAccountId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم حساب الضمان غير صحيح."
                });
            }

            try
            {
                DisableResponseCaching();

                var details =
                    await _adminFinancialService
                        .GetCommissionGuaranteeDetailsAsync(
                            placeCommissionAccountId,
                            cancellationToken);

                if (details == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "لم يتم العثور على حساب الضمان المطلوب."
                    });
                }

                var canManage =
                    await CurrentAdminHasPermissionAsync(
                        AdminPermissionKeys.Finance.ManageCommissionAccounts,
                        cancellationToken);

                return Json(new
                {
                    success = true,
                    canManageCommissionAccounts = canManage,
                    data = details
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
                    "حدث خطأ أثناء تحميل تفاصيل حساب الضمان {AccountId}.",
                    placeCommissionAccountId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تحميل تفاصيل حساب الضمان."
                });
            }
        }

        [HttpGet("Booking/{bookingId:int}")]
        public async Task<IActionResult> BookingDetails(
            int bookingId,
            CancellationToken cancellationToken = default)
        {
            if (bookingId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم الحجز غير صحيح."
                });
            }

            try
            {
                DisableResponseCaching();

                var details =
                    await _adminFinancialService
                        .GetBookingCommissionDetailsAsync(
                            bookingId,
                            cancellationToken);

                if (details == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "لم يتم العثور على السجل المالي لهذا الحجز."
                    });
                }

                return Json(new
                {
                    success = true,
                    data = details
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
                    "حدث خطأ أثناء تحميل تفاصيل عمولة الحجز {BookingId}.",
                    bookingId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تحميل تفاصيل عمولة الحجز."
                });
            }
        }

        [HttpPost("Policy")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManageCommissionAccounts)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePolicy(
            [FromForm] AdminCommissionAccountPolicyUpdateDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات سياسة حساب الضمان غير مكتملة."
                });
            }

            NormalizePolicyRequest(request);
            ValidatePolicyRequest(request);

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
                    await _adminFinancialService
                        .UpdateCommissionAccountPolicyAsync(
                            request,
                            adminUserId,
                            cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    availableBalance = result.AvailableBalance,
                    heldBalance = result.HeldBalance,
                    debtAmount = result.DebtAmount
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
                    "حدث خطأ أثناء تعديل سياسة حساب الضمان {AccountId} بواسطة الأدمن {AdminUserId}.",
                    request.PlaceCommissionAccountId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر حفظ سياسة حساب الضمان. لم يتم تنفيذ أي تغيير مالي."
                });
            }
        }

        [HttpPost("Status")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManageCommissionAccounts)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(
            [FromForm] AdminCommissionAccountStatusUpdateDto? request,
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

            request.TargetStatus = request.TargetStatus?.Trim() ?? string.Empty;
            request.Reason = request.Reason?.Trim() ?? string.Empty;

            if (!IsAllowedManualAccountStatus(request.TargetStatus))
            {
                ModelState.AddModelError(
                    nameof(request.TargetStatus),
                    "الحالة المطلوبة غير مسموحة.");
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
                    await _adminFinancialService
                        .ChangeCommissionAccountStatusAsync(
                            request,
                            adminUserId,
                            cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    availableBalance = result.AvailableBalance,
                    heldBalance = result.HeldBalance,
                    debtAmount = result.DebtAmount,
                    targetStatus = request.TargetStatus
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
                    "حدث خطأ أثناء تغيير حالة حساب الضمان {AccountId} بواسطة الأدمن {AdminUserId}.",
                    request.PlaceCommissionAccountId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تغيير حالة حساب الضمان. لم يتم تنفيذ أي تغيير مالي."
                });
            }
        }

        private static bool IsAllowedManualAccountStatus(string status)
        {
            return string.Equals(
                       status,
                       AdminCommissionAccountStatuses.Active,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       status,
                       AdminCommissionAccountStatuses.Suspended,
                       StringComparison.OrdinalIgnoreCase);
        }

        private void NormalizePolicyRequest(
            AdminCommissionAccountPolicyUpdateDto request)
        {
            request.Reason = request.Reason?.Trim() ?? string.Empty;

            if (request.UseGlobalAllowNegativeBalance)
            {
                request.AllowNegativeBalance = null;
            }

            if (request.UseGlobalDebtLimit)
            {
                request.DebtLimit = null;
            }

            if (request.UseGlobalLowBalanceThreshold)
            {
                request.LowBalanceThreshold = null;
            }
        }

        private void ValidatePolicyRequest(
            AdminCommissionAccountPolicyUpdateDto request)
        {
            if (!request.UseGlobalAllowNegativeBalance &&
                !request.AllowNegativeBalance.HasValue)
            {
                ModelState.AddModelError(
                    nameof(request.AllowNegativeBalance),
                    "حدد هل يسمح الحساب بالدين أم لا.");
            }

            if (!request.UseGlobalDebtLimit &&
                (!request.DebtLimit.HasValue || request.DebtLimit.Value < 0m))
            {
                ModelState.AddModelError(
                    nameof(request.DebtLimit),
                    "حد الدين الخاص غير صحيح.");
            }

            if (!request.UseGlobalLowBalanceThreshold &&
                (!request.LowBalanceThreshold.HasValue ||
                 request.LowBalanceThreshold.Value < 0m))
            {
                ModelState.AddModelError(
                    nameof(request.LowBalanceThreshold),
                    "حد انخفاض الرصيد الخاص غير صحيح.");
            }
        }

        private async Task<bool> CurrentAdminHasPermissionAsync(
            string permissionKey,
            CancellationToken cancellationToken)
        {
            return TryGetCurrentAdminUserId(out var adminUserId) &&
                   await _adminPermissionService.HasPermissionAsync(
                       adminUserId,
                       permissionKey,
                       cancellationToken);
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
