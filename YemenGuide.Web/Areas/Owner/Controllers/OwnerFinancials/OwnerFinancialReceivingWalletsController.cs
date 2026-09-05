using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Owner.Controllers.OwnerFinancials
{
    [Area("Owner")]
    [Authorize]
    [AuthorizeActiveOwner]
    [Route("Owner/Financials/ReceivingWallets")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public sealed class OwnerFinancialReceivingWalletsController
        : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Owner/Views/OwnerFinancials/" +
            "_OwnerFinancialReceivingWallets.cshtml";

        private readonly IOwnerFinancialService
            _ownerFinancialService;

        private readonly ILogger<
            OwnerFinancialReceivingWalletsController
        > _logger;

        public OwnerFinancialReceivingWalletsController(
            IOwnerFinancialService ownerFinancialService,
            ILogger<
                OwnerFinancialReceivingWalletsController
            > logger)
        {
            _ownerFinancialService =
                ownerFinancialService ??
                throw new ArgumentNullException(
                    nameof(ownerFinancialService));

            _logger =
                logger ??
                throw new ArgumentNullException(
                    nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("~/Owner/Financials/Wallets")]
        public async Task<IActionResult> Index(
            CancellationToken cancellationToken = default)
        {
            if (!IsAjaxRequest())
            {
                return Redirect(
                    "/Owner/Financials?financeTab=wallets");
            }

            if (!TryGetCurrentOwnerId(
                    out var ownerId))
            {
                return Unauthorized(
                    "تعذر تحديد حساب المالك الحالي.");
            }

            try
            {
                var model =
                    await _ownerFinancialService
                        .GetReceivingWalletsAsync(
                            ownerId,
                            cancellationToken);

                return PartialView(
                    PartialViewPath,
                    model);
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تحميل حسابات استقبال الدفعات " +
                    "للمالك {OwnerId}.",
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    "تعذر تحميل حسابات استقبال الدفعات حاليًا.");
            }
        }

        [HttpPost("Save")]
        [HttpPost("~/Owner/Financials/Wallets/Save")]
        [ValidateAntiForgeryToken]
        [Consumes("application/json")]
        public async Task<IActionResult> Save(
            [FromBody] SaveOwnerWalletRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentOwnerId(
                    out var ownerId))
            {
                return Unauthorized(
                    new
                    {
                        success = false,
                        message =
                            "تعذر تحديد حساب المالك الحالي."
                    });
            }

            if (request == null)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "بيانات حساب الاستقبال غير مكتملة."
                    });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(
                    BuildValidationResponse());
            }

            try
            {
                var result =
                    await _ownerFinancialService
                        .SaveReceivingWalletAsync(
                            ownerId,
                            request,
                            cancellationToken);

                if (!result.Success)
                {
                    return BadRequest(
                        new
                        {
                            success = false,
                            message =
                                result.Message
                        });
                }

                return Ok(
                    new
                    {
                        success = true,
                        message =
                            result.Message,
                        ownerWalletId =
                            result.EntityId
                    });
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر حفظ حساب استقبال الدفعات " +
                    "للمالك {OwnerId} على الشبكة {WalletId}.",
                    ownerId,
                    request.WalletId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "تعذر حفظ حساب الاستقبال حاليًا."
                    });
            }
        }

        [HttpPost("Status")]
        [HttpPost("~/Owner/Financials/Wallets/Status")]
        [ValidateAntiForgeryToken]
        [Consumes("application/json")]
        public async Task<IActionResult> ChangeStatus(
            [FromBody] ChangeOwnerWalletStatusRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentOwnerId(
                    out var ownerId))
            {
                return Unauthorized(
                    new
                    {
                        success = false,
                        message =
                            "تعذر تحديد حساب المالك الحالي."
                    });
            }

            if (request == null)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "بيانات تغيير الحالة غير مكتملة."
                    });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(
                    BuildValidationResponse());
            }

            try
            {
                var result =
                    await _ownerFinancialService
                        .ChangeReceivingWalletStatusAsync(
                            ownerId,
                            request.OwnerWalletId,
                            request.IsActive,
                            cancellationToken);

                if (!result.Success)
                {
                    return BadRequest(
                        new
                        {
                            success = false,
                            message =
                                result.Message
                        });
                }

                return Ok(
                    new
                    {
                        success = true,
                        message =
                            result.Message,
                        ownerWalletId =
                            result.EntityId,
                        isActive =
                            request.IsActive
                    });
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تغيير حالة حساب الاستقبال " +
                    "{OwnerWalletId} للمالك {OwnerId}.",
                    request.OwnerWalletId,
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "تعذر تغيير حالة الحساب حاليًا."
                    });
            }
        }

        private object BuildValidationResponse()
        {
            var errors =
                ModelState
                    .Where(item =>
                        item.Value != null &&
                        item.Value.Errors.Count > 0)
                    .ToDictionary(
                        item =>
                            item.Key,
                        item =>
                            item.Value!
                                .Errors
                                .Select(error =>
                                    string.IsNullOrWhiteSpace(
                                        error.ErrorMessage)
                                        ? "القيمة المدخلة غير صحيحة."
                                        : error.ErrorMessage)
                                .ToArray());

            var firstMessage =
                errors
                    .SelectMany(item =>
                        item.Value)
                    .FirstOrDefault()
                ??
                "تحقق من البيانات المدخلة.";

            return new
            {
                success = false,
                message = firstMessage,
                errors
            };
        }

        private bool TryGetCurrentOwnerId(
            out int ownerId)
        {
            ownerId = 0;

            return int.TryParse(
                       User.FindFirstValue(
                           ClaimTypes.NameIdentifier),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out ownerId) &&
                   ownerId > 0;
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(
                Request.Headers[
                    "X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class
        ChangeOwnerWalletStatusRequestDto
    {
        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "رقم حساب الاستقبال غير صحيح.")]
        public int OwnerWalletId { get; set; }

        public bool IsActive { get; set; }
    }
}
