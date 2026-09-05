using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace YemenGuide.Web.Maintenance
{
    /// <summary>
    /// فلتر مركزي للصيانة الجزئية.
    ///
    /// يعمل قبل تنفيذ Action الحقيقي، لذلك يمنع:
    /// - SaveChanges
    /// - رفع الملفات
    /// - إرسال الرسالة عبر Controller
    /// - تنفيذ الدفع أو الحجز
    ///
    /// بدون الحاجة لتعديل Controllers العميل والمالك.
    /// </summary>
    public sealed class MaintenancePartialFilter
        : IAsyncActionFilter
    {
        private readonly IMaintenanceService
            _maintenanceService;

        private readonly MaintenanceOperationPolicy
            _operationPolicy;

        public MaintenancePartialFilter(
            IMaintenanceService maintenanceService,
            MaintenanceOperationPolicy operationPolicy)
        {
            _maintenanceService =
                maintenanceService;

            _operationPolicy =
                operationPolicy;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var cancellationToken =
                context.HttpContext
                    .RequestAborted;

            var state =
                await _maintenanceService
                    .GetStateAsync(
                        cancellationToken);

            // Full و ReadOnly مسؤول عنهما Middleware.
            if (!state.IsActive ||
                !state.IsPartial ||
                !_maintenanceService.IsUserAffected(
                    context.HttpContext.User,
                    state))
            {
                await next();
                return;
            }

            var operations =
                await _operationPolicy
                    .ResolveOperationsAsync(
                        context,
                        cancellationToken);

            if (operations.Count == 0)
            {
                await next();
                return;
            }

            MaintenanceOperation? blockedOperation =
                null;

            foreach (var operation in
                     operations.Distinct())
            {
                if (state.IsOperationBlocked(
                        operation))
                {
                    blockedOperation =
                        operation;

                    break;
                }
            }

            if (!blockedOperation.HasValue)
            {
                await next();
                return;
            }

            MaintenanceResponseHelper
                .PrepareServiceUnavailableResponse(
                    context.HttpContext.Response,
                    state);

            if (MaintenanceResponseHelper
                .ExpectsJson(
                    context.HttpContext.Request))
            {
                context.Result =
                    MaintenanceResponseHelper
                        .CreateBlockedJsonResult(
                            blockedOperation.Value,
                            state,
                            context.HttpContext.Request);

                return;
            }

            var returnUrl =
                MaintenanceResponseHelper
                    .BuildReturnUrl(
                        context.HttpContext.Request);

            context.Result =
                new RedirectResult(
                    MaintenanceResponseHelper
                        .BuildRestrictedUrl(
                            blockedOperation.Value,
                            returnUrl));
        }
    }
}
