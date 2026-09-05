using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace YemenGuide.Web.Maintenance
{
    [AllowAnonymous]
    [Route("maintenance")]
    public sealed class MaintenanceController
        : Controller
    {
        private readonly IMaintenanceService
            _maintenanceService;

        public MaintenanceController(
            IMaintenanceService maintenanceService)
        {
            _maintenanceService =
                maintenanceService;
        }

        /// <summary>
        /// /maintenance
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult>
            MaintenancePage(
                CancellationToken cancellationToken)
        {
            var state =
                await _maintenanceService
                    .GetStateAsync(
                        cancellationToken);

            if (!state.IsActive ||
                !state.IsFull ||
                !_maintenanceService.IsUserAffected(
                    User,
                    state))
            {
                return Redirect("/");
            }

            PrepareMaintenanceResponse(
                state);

            return View(
                "~/Views/Maintenance/MaintenancePage.cshtml",
                state);
        }

        /// <summary>
        /// /maintenance/restricted
        /// </summary>
        [HttpGet("restricted")]
        public async Task<IActionResult>
            RestrictedService(
                string? mode,
                string? operation,
                string? returnUrl,
                CancellationToken cancellationToken)
        {
            var state =
                await _maintenanceService
                    .GetStateAsync(
                        cancellationToken);

            if (!state.IsActive ||
                !_maintenanceService.IsUserAffected(
                    User,
                    state))
            {
                return RedirectToSafeUrl(
                    returnUrl);
            }

            if (state.IsFull)
            {
                return Redirect(
                    "/maintenance");
            }

            var parsedOperation =
                TryParseOperation(
                    operation);

            if (state.IsPartial)
            {
                if (!parsedOperation.HasValue ||
                    !state.IsOperationBlocked(
                        parsedOperation.Value))
                {
                    return RedirectToSafeUrl(
                        returnUrl);
                }
            }

            var isReadOnly =
                state.IsReadOnly ||
                string.Equals(
                    mode,
                    "readonly",
                    StringComparison.OrdinalIgnoreCase);

            ViewData["RestrictionMode"] =
                isReadOnly
                    ? "ReadOnly"
                    : "Partial";

            ViewData["ServiceName"] =
                isReadOnly
                    ? "تنفيذ العمليات"
                    : MaintenanceResponseHelper
                        .GetOperationName(
                            parsedOperation);

            ViewData["ServiceMessage"] =
                isReadOnly
                    ? "المنصة متاحة للتصفح فقط مؤقتًا. يمكنك مشاهدة المحتوى والبيانات، لكن تنفيذ عمليات التعديل متوقف حتى انتهاء أعمال الصيانة."
                    : MaintenanceResponseHelper
                        .GetOperationMessage(
                            parsedOperation);

            ViewData["ServiceIcon"] =
                isReadOnly
                    ? "bi-eye"
                    : MaintenanceResponseHelper
                        .GetOperationIcon(
                            parsedOperation);

            ViewData["ReturnUrl"] =
                GetSafeReturnUrl(
                    returnUrl);

            PrepareMaintenanceResponse(
                state);

            return View(
                "~/Views/Maintenance/RestrictedService.cshtml",
                state);
        }

        private void PrepareMaintenanceResponse(
            MaintenanceState state)
        {
            Response.StatusCode =
                StatusCodes
                    .Status503ServiceUnavailable;

            MaintenanceResponseHelper
                .PrepareServiceUnavailableResponse(
                    Response,
                    state);
        }

        private IActionResult RedirectToSafeUrl(
            string? returnUrl)
        {
            return Redirect(
                GetSafeReturnUrl(
                    returnUrl));
        }

        private string GetSafeReturnUrl(
            string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl) &&
                !returnUrl!.StartsWith(
                    "/maintenance",
                    StringComparison.OrdinalIgnoreCase))
            {
                return returnUrl;
            }

            return "/";
        }

        private static MaintenanceOperation?
            TryParseOperation(
                string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return null;
            }

            return Enum.TryParse<
                    MaintenanceOperation>(
                    value,
                    ignoreCase: true,
                    out var operation)
                ? operation
                : null;
        }
    }
}
