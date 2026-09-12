using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace YemenGuide.Web.Maintenance
{
    /// <summary>
    /// مستوى المنع العام:
    /// Full     = إيقاف المنصة للمستخدم المتأثر.
    /// ReadOnly = السماح بالقراءة ومنع طلبات الكتابة.
    /// Partial  = يمر إلى MVC ثم يعالجه MaintenancePartialFilter.
    /// </summary>
    public sealed class MaintenanceMiddleware
    {
        private readonly RequestDelegate _next;

        public MaintenanceMiddleware(
            RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IMaintenanceService maintenanceService)
        {
            if (IsAlwaysAllowedPath(
                    context.Request.Path))
            {
                await _next(context);
                return;
            }

            var state =
                await maintenanceService
                    .GetStateAsync(
                        context.RequestAborted);

            if (!state.IsActive ||
                !maintenanceService.IsUserAffected(
                    context.User,
                    state))
            {
                await _next(context);
                return;
            }

            if (state.IsFull)
            {
                await BlockForFullMaintenanceAsync(
                    context,
                    state);

                return;
            }

            if (state.IsReadOnly &&
                IsWriteRequest(
                    context.Request.Method))
            {
                await BlockWriteRequestAsync(
                    context,
                    state);

                return;
            }

            // Partial لا يُمنع هنا بشكل عام.
            // بعد اختيار الـ Controller/Action يقوم
            // MaintenancePartialFilter بتحديد الخدمة الصحيحة.
            await _next(context);
        }

        private static bool IsAlwaysAllowedPath(
            PathString path)
        {
            return
                path.StartsWithSegments(
                    "/maintenance",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWithSegments(
                    "/Account/Login",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWithSegments(
                    "/Account/Logout",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWithSegments(
                    "/css",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWithSegments(
                    "/js",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWithSegments(
                    "/lib",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWithSegments(
                    "/images",
                    StringComparison.OrdinalIgnoreCase) ||

                path.StartsWithSegments(
                    "/favicon.ico",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWriteRequest(
            string method)
        {
            return
                !HttpMethods.IsGet(method) &&
                !HttpMethods.IsHead(method) &&
                !HttpMethods.IsOptions(method);
        }

        private static async Task
            BlockForFullMaintenanceAsync(
                HttpContext context,
                MaintenanceState state)
        {
            MaintenanceResponseHelper
                .PrepareServiceUnavailableResponse(
                    context.Response,
                    state);

            if (MaintenanceResponseHelper
                .ExpectsJson(
                    context.Request))
            {
                context.Response.StatusCode =
                    StatusCodes
                        .Status503ServiceUnavailable;

                await context.Response
                    .WriteAsJsonAsync(
                        new
                        {
                            success = false,
                            maintenance = true,
                            type =
                                MaintenanceTypeNames.Full,
                            message =
                                "النظام تحت الصيانة حاليًا.",
                            redirectUrl =
                                "/maintenance",
                            retryAtUtc =
                                state.EndAtUtc
                        });

                return;
            }

            context.Response.Redirect(
                "/maintenance");
        }

        private static async Task
            BlockWriteRequestAsync(
                HttpContext context,
                MaintenanceState state)
        {
            MaintenanceResponseHelper
                .PrepareServiceUnavailableResponse(
                    context.Response,
                    state);

            var redirectUrl =
                MaintenanceResponseHelper
                    .BuildReadOnlyUrl(
                        MaintenanceResponseHelper
                            .BuildReturnUrl(
                                context.Request));

            if (MaintenanceResponseHelper
                .ExpectsJson(
                    context.Request))
            {
                context.Response.StatusCode =
                    StatusCodes
                        .Status503ServiceUnavailable;

                await context.Response
                    .WriteAsJsonAsync(
                        new
                        {
                            success = false,
                            maintenance = true,
                            type =
                                MaintenanceTypeNames.ReadOnly,
                            message =
                                "النظام متاح للتصفح فقط حاليًا بسبب أعمال الصيانة.",
                            redirectUrl,
                            retryAtUtc =
                                state.EndAtUtc
                        });

                return;
            }

            context.Response.Redirect(
                redirectUrl);
        }
    }
}
