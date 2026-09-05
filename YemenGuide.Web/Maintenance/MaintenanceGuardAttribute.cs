using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace YemenGuide.Web.Maintenance
{
    /// <summary>
    /// Guard اختياري للحالات التي تريد ربط Action بها صراحةً.
    /// المحرك الحالي يعتمد أساسًا على MaintenancePartialFilter المركزي.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Method,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class MaintenanceGuardAttribute
        : Attribute,
          IAsyncActionFilter
    {
        public MaintenanceGuardAttribute(
            MaintenanceOperation operation)
        {
            Operation = operation;
        }

        public MaintenanceOperation Operation
        {
            get;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var service =
                context.HttpContext
                    .RequestServices
                    .GetRequiredService<
                        IMaintenanceService>();

            var state =
                await service.GetStateAsync(
                    context.HttpContext
                        .RequestAborted);

            if (!state.IsActive ||
                !service.IsUserAffected(
                    context.HttpContext.User,
                    state) ||
                !state.IsOperationBlocked(
                    Operation))
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
                            Operation,
                            state,
                            context.HttpContext.Request);

                return;
            }

            context.Result =
                new RedirectResult(
                    MaintenanceResponseHelper
                        .BuildRestrictedUrl(
                            Operation,
                            MaintenanceResponseHelper
                                .BuildReturnUrl(
                                    context.HttpContext.Request)));
        }
    }
}
