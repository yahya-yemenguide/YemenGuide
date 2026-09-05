using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Security.Claims;
using YemenGuide.Data;

namespace YemenGuide.Web.Filters
{
    /// <summary>
    /// يوضع فقط على Actions المطلوبة داخل رحلة توثيق المالك قبل التفعيل.
    /// لا يلغي [Authorize] ولا يمنح دخولاً عاماً؛ هو فقط يمنع AuthorizeActiveOwner
    /// من إعادة المالك Pending إلى VerifyAccount قبل أن يصل الـAction المحدد.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AllowPendingOwnerVerificationAttribute : Attribute
    {
    }

    public class AuthorizeActiveOwnerAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            {
                return;
            }

            var user = context.HttpContext.User;

            if (user.Identity != null && user.Identity.IsAuthenticated && user.IsInRole("Owner"))
            {
                var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var accountState = db.Users
                        .Where(u => u.UserId == userId)
                        .Select(u => u.AccountState)
                        .FirstOrDefault();

                    bool isPreActivationState =
                        string.Equals(accountState, "Pending", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(accountState, "Rejected", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(accountState, "Accepted", StringComparison.OrdinalIgnoreCase);

                    if (isPreActivationState)
                    {
                        // السماح فقط للـActions الموسومة؛ [Authorize] على الكنترولر يبقى فعالاً.
                        bool verificationAction = context.ActionDescriptor.EndpointMetadata
                            .OfType<AllowPendingOwnerVerificationAttribute>()
                            .Any();

                        if (verificationAction)
                        {
                            return;
                        }

                        context.Result = new RedirectResult("/Account/VerifyAccount");
                        return;
                    }
                }
            }

            base.OnActionExecuting(context);
        }
    }
}
