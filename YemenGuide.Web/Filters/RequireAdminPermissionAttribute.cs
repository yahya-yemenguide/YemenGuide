using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using YemenGuide.Web.Services.Employees;

namespace YemenGuide.Web.Filters
{
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class RequireAdminPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public RequireAdminPermissionAttribute(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException(
                    "Permission key cannot be empty.",
                    nameof(permissionKey));
            }

            PermissionKey = permissionKey.Trim();
        }

        public string PermissionKey { get; }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var endpoint = context.HttpContext.GetEndpoint();

            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
            {
                return;
            }

            var identity = context.HttpContext.User.Identity;

            if (identity?.IsAuthenticated != true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var userIdValue = context.HttpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
            {
                context.Result = BuildForbiddenResult(context.HttpContext);
                return;
            }

            var permissionService = context.HttpContext.RequestServices
                .GetRequiredService<IAdminPermissionService>();

            var allowed = await permissionService.HasPermissionAsync(
                userId,
                PermissionKey,
                context.HttpContext.RequestAborted);

            if (!allowed)
            {
                context.Result = BuildForbiddenResult(context.HttpContext);
            }
        }

        private static IActionResult BuildForbiddenResult(HttpContext httpContext)
        {
            var request = httpContext.Request;
            var acceptsJson = request.Headers.Accept.ToString()
                .Contains("application/json", StringComparison.OrdinalIgnoreCase);

            var isAjax = string.Equals(
                request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            if (acceptsJson || isAjax)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "ليس لديك صلاحية لتنفيذ هذه العملية."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
