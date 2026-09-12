using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Security.Claims;
using YemenGuide.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Threading.Tasks;

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

    /// <summary>
    /// تطبيق حي لحالة الحساب على كل طلب MVC.
    /// - Blocked: يمنع العميل/المالك من الاستمرار ويُنهي الجلسة الحالية.
    /// - Suspended للعميل/المالك: يسمح بالقراءة والتصفح فقط ويمنع أي طلب كتابي.
    /// - موظف الإدارة الموقوف وظيفيًا: تُنهى جلسته فورًا ولا يستطيع دخول لوحة الإدارة.
    ///
    /// يقرأ الحالة من قاعدة البيانات في كل Action مهم، لذلك يسري قرار الأدمن
    /// حتى لو كان المستخدم مسجل الدخول قبل تغيير حالته.
    /// </summary>
    public sealed class AccountStateEnforcementFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _db;

        public AccountStateEnforcementFilter(AppDbContext db)
        {
            _db = db;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var principal = context.HttpContext.User;
            if (principal.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            var userIdText = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdText, out var userId) || userId <= 0)
            {
                await next();
                return;
            }

            var liveAccount = await _db.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => new
                {
                    user.UserId,
                    user.Role,
                    user.AccountState,
                    user.EmploymentStatus,
                    IsSystemAdmin =
                        user.AdminRole != null &&
                        user.AdminRole.IsSystemRole
                })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            if (liveAccount == null)
            {
                await SignOutAsync(context.HttpContext);
                context.Result = new RedirectResult("/Account/Login");
                return;
            }

            // موظفو الإدارة: التوقيف الوظيفي يعني منع الوصول بالكامل.
            if (string.Equals(liveAccount.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(
                        liveAccount.EmploymentStatus,
                        "Active",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await SignOutAsync(context.HttpContext);

                    var employeeMessage = IsArabic()
                        ? "تم إيقاف حساب الموظف. راجع مدير النظام."
                        : "Your staff account is suspended. Please contact the system administrator.";

                    context.Result = ExpectsJson(context.HttpContext.Request)
                        ? new JsonResult(new
                        {
                            success = false,
                            employeeSuspended = true,
                            message = employeeMessage
                        })
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        }
                        : new RedirectResult("/Account/Login");

                    return;
                }

                await next();
                return;
            }

            var isCustomer = string.Equals(
                liveAccount.Role,
                "Customer",
                StringComparison.OrdinalIgnoreCase);

            var isOwner = string.Equals(
                liveAccount.Role,
                "Owner",
                StringComparison.OrdinalIgnoreCase);

            if (!isCustomer && !isOwner)
            {
                await next();
                return;
            }

            if (string.Equals(
                    liveAccount.AccountState,
                    "Blocked",
                    StringComparison.OrdinalIgnoreCase))
            {
                await SignOutAsync(context.HttpContext);

                var blockedMessage = IsArabic()
                    ? "هذا الحساب محظور من قِبل الإدارة ولا يمكنه استخدام المنصة."
                    : "This account has been blocked by the administration and cannot use the platform.";

                context.Result = ExpectsJson(context.HttpContext.Request)
                    ? new JsonResult(new
                    {
                        success = false,
                        accountBlocked = true,
                        message = blockedMessage
                    })
                    {
                        StatusCode = StatusCodes.Status401Unauthorized
                    }
                    : new RedirectResult("/Account/Login");

                return;
            }

            if (!string.Equals(
                    liveAccount.AccountState,
                    "Suspended",
                    StringComparison.OrdinalIgnoreCase) ||
                IsReadRequest(context.HttpContext.Request.Method) ||
                IsAllowedSuspendedAccountAction(context))
            {
                await next();
                return;
            }

            var suspendedMessage = IsArabic()
                ? "حسابك موقوف مؤقتًا من قِبل الإدارة. يمكنك تسجيل الدخول والتصفح ومشاهدة بياناتك فقط، لكن لا يمكنك تنفيذ أي إضافة أو تعديل أو حجز أو دفع أو إلغاء أو مراسلة أو تقييم حتى تعيد الإدارة تفعيل الحساب."
                : "Your account is temporarily suspended by the administration. You can sign in, browse, and view your data, but you cannot create, edit, book, pay, cancel, message, review, or perform other write actions until the account is reactivated.";

            if (ExpectsJson(context.HttpContext.Request))
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    accountSuspended = true,
                    readOnly = true,
                    message = suspendedMessage
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };

                return;
            }

            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status403Forbidden,
                ContentType = "text/html; charset=utf-8",
                Content = BuildSuspendedHtml(
                    suspendedMessage,
                    isOwner)
            };
        }

        private static bool IsAllowedSuspendedAccountAction(
            ActionExecutingContext context)
        {
            var controller =
                context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            var action =
                context.RouteData.Values["action"]?.ToString() ?? string.Empty;

            // تسجيل الخروج يجب أن يظل متاحًا دائمًا.
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // إجراءات الأمان اللازمة لحساب Google الجديد لا تعد نشاطًا تجاريًا.
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(action, "ForceChangePassword", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(action, "SetGooglePassword", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // تبديل اللغة يظل متاحًا حتى أثناء وضع القراءة فقط.
            if (string.Equals(action, "SetLanguage", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller, "Language", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller, "Localization", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool IsReadRequest(string method)
        {
            return HttpMethods.IsGet(method) ||
                   HttpMethods.IsHead(method) ||
                   HttpMethods.IsOptions(method);
        }

        private static bool ExpectsJson(HttpRequest request)
        {
            if (request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
                string.Equals(
                    requestedWith.ToString(),
                    "XMLHttpRequest",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var accept = request.Headers.Accept.ToString();
            if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(request.ContentType) &&
                request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (request.Headers.TryGetValue("Sec-Fetch-Dest", out var fetchDest) &&
                string.Equals(fetchDest.ToString(), "empty", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static async Task SignOutAsync(HttpContext context)
        {
            await context.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
        }

        private static bool IsArabic()
        {
            return string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "ar",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSuspendedHtml(
            string message,
            bool isOwner)
        {
            var ar = IsArabic();
            var lang = ar ? "ar" : "en";
            var dir = ar ? "rtl" : "ltr";
            var title = ar ? "الحساب موقوف مؤقتًا" : "Account temporarily suspended";
            var note = ar
                ? "وضع حسابك الآن للتصفح فقط. لم يتم تنفيذ العملية التي حاولت القيام بها."
                : "Your account is currently read-only. The action you attempted was not performed.";
            var backLabel = ar ? "العودة للتصفح" : "Back to browsing";
            var home = isOwner
                ? "/Owner/OwnerDashboard/DashboardOwner"
                : "/Home/Index";

            return $@"<!doctype html>
<html lang=""{lang}"" dir=""{dir}"">
<head>
<meta charset=""utf-8"" />
<meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
<title>{title}</title>
<style>
body{{margin:0;min-height:100vh;display:grid;place-items:center;background:#05203c;color:#fff;font-family:Arial,Tahoma,sans-serif;padding:24px;box-sizing:border-box}}
.card{{max-width:720px;width:100%;background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.15);border-radius:24px;padding:32px;box-shadow:0 24px 70px rgba(0,0,0,.28)}}
h1{{margin:0 0 14px;font-size:clamp(24px,4vw,38px)}}p{{line-height:1.9;color:#e7f6ff}}.note{{padding:14px 16px;border-radius:14px;background:rgba(245,158,11,.14);border:1px solid rgba(245,158,11,.35)}}
a{{display:inline-block;margin-top:18px;padding:12px 20px;border-radius:12px;background:#00a6da;color:#fff;text-decoration:none;font-weight:700}}
</style>
</head>
<body><main class=""card""><h1>{title}</h1><p>{message}</p><p class=""note"">{note}</p><a href=""{home}"">{backLabel}</a></main></body></html>";
        }
    }

}
