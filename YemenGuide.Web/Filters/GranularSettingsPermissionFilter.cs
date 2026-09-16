using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Services.Employees;

namespace YemenGuide.Web.Filters
{
    /// <summary>
    /// طبقة صلاحيات دقيقة مخصصة لإعدادات النظام والنسخ الاحتياطي.
    ///
    /// المشروع القديم كان يستخدم Settings.ManageGeneral و
    /// Settings.ManagePaymentNetworks بصلاحيات واسعة. هذا الفلتر يضيف
    /// طبقة أدق على مستوى كل تبويب/عملية، بينما تظل الحماية القديمة موجودة
    /// للتوافق مع الكود الحالي.
    /// </summary>
    public sealed class GranularSettingsPermissionFilter : IAsyncActionFilter
    {
        private readonly IAdminPermissionService _permissionService;

        public GranularSettingsPermissionFilter(
            IAdminPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                await next();
                return;
            }

            var area =
                descriptor.ControllerTypeInfo
                    .GetCustomAttributes(
                        typeof(AreaAttribute),
                        inherit: true)
                    .OfType<AreaAttribute>()
                    .FirstOrDefault()
                    ?.RouteValue;

            if (!string.Equals(
                    area,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            string controller =
                descriptor.ControllerName ?? string.Empty;

            string action =
                descriptor.ActionName ?? string.Empty;

            PermissionRequirement? requirement =
                ResolveRequirement(
                    controller,
                    action,
                    context);

            if (requirement == null)
            {
                await next();
                return;
            }

            int userId =
                int.TryParse(
                    context.HttpContext.User.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var parsedUserId)
                    ? parsedUserId
                    : 0;

            if (userId <= 0)
            {
                context.Result =
                    new ForbidResult();
                return;
            }

            bool allowed = false;

            foreach (string permissionKey in requirement.PermissionKeys)
            {
                if (await _permissionService.HasPermissionAsync(
                        userId,
                        permissionKey,
                        context.HttpContext.RequestAborted))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                context.Result =
                    BuildDeniedResult(
                        context.HttpContext,
                        requirement.Message);

                return;
            }

            await next();
        }

        private static PermissionRequirement? ResolveRequirement(
            string controller,
            string action,
            ActionExecutingContext context)
        {
            if (string.Equals(
                    controller,
                    "Settings",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ResolveSettingsRequirement(
                    action,
                    context);
            }

            if (string.Equals(
                    controller,
                    "Backup",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ResolveBackupRequirement(
                    action);
            }

            return null;
        }

        private static PermissionRequirement? ResolveSettingsRequirement(
            string action,
            ActionExecutingContext context)
        {
            if (EqualsAction(action, "SystemSettings"))
            {
                return Require(
                    "ليس لديك صلاحية لدخول إعدادات النظام.",
                    AdminPermissionKeys.Settings.View);
            }

            if (EqualsAction(action, "SaveSettings"))
            {
                string section =
                    context.ActionArguments
                        .TryGetValue(
                            "Section",
                            out object? value)
                        ? Convert.ToString(value)?.Trim().ToLowerInvariant()
                          ?? string.Empty
                        : string.Empty;

                return section switch
                {
                    "branding" =>
                        Require(
                            "ليس لديك صلاحية لتعديل الهوية والماركة.",
                            AdminPermissionKeys.Settings.ManageBranding),

                    "policies" =>
                        Require(
                            "ليس لديك صلاحية لتعديل سياسات التشغيل أو الإعدادات المالية.",
                            AdminPermissionKeys.Settings.ManagePolicies,
                            AdminPermissionKeys.Finance.ManageSettings),

                    "contact" =>
                        Require(
                            "ليس لديك صلاحية لتعديل التواصل والروابط.",
                            AdminPermissionKeys.Settings.ManageContact),

                    "maintenance" =>
                        Require(
                            "ليس لديك صلاحية لإدارة الصيانة والطوارئ.",
                            AdminPermissionKeys.Settings.ManageMaintenance),

                    _ =>
                        Require(
                            "قسم الإعدادات المطلوب غير مسموح.",
                            "__DENY__")
                };
            }

            if (EqualsAction(action, "GetExchangeRateStatus"))
            {
                return Require(
                    "ليس لديك صلاحية لمشاهدة إعدادات أسعار الصرف.",
                    AdminPermissionKeys.Settings.ViewPolicies,
                    AdminPermissionKeys.Finance.ManageSettings);
            }

            if (EqualsAction(action, "RefreshExchangeRatesNow"))
            {
                return Require(
                    "ليس لديك صلاحية لتحديث أسعار الصرف.",
                    AdminPermissionKeys.Finance.ManageSettings);
            }

            if (EqualsAction(action, "UpdateDefaultWalletLogo"))
            {
                return Require(
                    "ليس لديك صلاحية لتغيير الشعار الافتراضي لشبكات الدفع.",
                    AdminPermissionKeys.Settings.ManageDefaultWalletLogo);
            }

            if (EqualsAction(action, "GetSupportedWallets"))
            {
                return Require(
                    "ليس لديك صلاحية لمشاهدة شبكات الدفع.",
                    AdminPermissionKeys.Settings.ViewPaymentNetworks);
            }

            if (EqualsAction(action, "AddSupportedWallet"))
            {
                return Require(
                    "ليس لديك صلاحية لإضافة شبكة دفع.",
                    AdminPermissionKeys.Settings.AddPaymentNetwork);
            }

            if (EqualsAction(action, "EditSupportedWalletNames"))
            {
                return Require(
                    "ليس لديك صلاحية لتعديل شبكة الدفع.",
                    AdminPermissionKeys.Settings.EditPaymentNetwork);
            }

            if (EqualsAction(action, "ToggleWalletStatus"))
            {
                return Require(
                    "ليس لديك صلاحية لتفعيل أو إيقاف شبكة الدفع.",
                    AdminPermissionKeys.Settings.ChangePaymentNetworkStatus);
            }

            if (EqualsAction(action, "DeleteSupportedWallet"))
            {
                return Require(
                    "ليس لديك صلاحية لحذف شبكة الدفع.",
                    AdminPermissionKeys.Settings.DeletePaymentNetwork);
            }

            if (EqualsAction(action, "GetHeroSlides"))
            {
                return Require(
                    "ليس لديك صلاحية لمشاهدة شرائح الواجهة الرئيسية.",
                    AdminPermissionKeys.Settings.ViewHeroSlides);
            }

            if (EqualsAction(
                    action,
                    "AddHeroSlide",
                    "ToggleHeroSlideStatus",
                    "EditHeroSlideText",
                    "MoveHeroSlideOrder",
                    "DeleteHeroSlide"))
            {
                return Require(
                    "ليس لديك صلاحية لإدارة شرائح الواجهة الرئيسية.",
                    AdminPermissionKeys.Settings.ManageHeroSlides);
            }

            return null;
        }

        private static PermissionRequirement? ResolveBackupRequirement(
            string action)
        {
            if (EqualsAction(action, "Dashboard"))
            {
                return Require(
                    "ليس لديك صلاحية لمشاهدة النسخ الاحتياطية.",
                    AdminPermissionKeys.Settings.ViewBackups);
            }

            if (EqualsAction(action, "CreateFull"))
            {
                return Require(
                    "ليس لديك صلاحية لإنشاء نسخة احتياطية.",
                    AdminPermissionKeys.Settings.CreateBackup);
            }

            if (EqualsAction(action, "SaveSettings"))
            {
                return Require(
                    "ليس لديك صلاحية لإدارة النسخ التلقائي.",
                    AdminPermissionKeys.Settings.ManageBackupSchedule);
            }

            if (EqualsAction(action, "Download"))
            {
                return Require(
                    "ليس لديك صلاحية لتنزيل النسخ الاحتياطية.",
                    AdminPermissionKeys.Settings.DownloadBackup);
            }

            if (EqualsAction(
                    action,
                    "RestoreStored",
                    "RestoreUploaded"))
            {
                return Require(
                    "ليس لديك صلاحية لاستعادة النسخ الاحتياطية.",
                    AdminPermissionKeys.Settings.RestoreBackup);
            }

            if (EqualsAction(
                    action,
                    "GoogleDriveConnect",
                    "GoogleDriveCallback",
                    "GoogleDriveSync",
                    "GoogleDriveDisconnect"))
            {
                return Require(
                    "ليس لديك صلاحية لإدارة Google Drive للنسخ الاحتياطية.",
                    AdminPermissionKeys.Settings.ManageBackupCloud);
            }

            if (EqualsAction(action, "Delete"))
            {
                return Require(
                    "ليس لديك صلاحية لحذف النسخ الاحتياطية.",
                    AdminPermissionKeys.Settings.DeleteBackup);
            }

            return null;
        }

        private static PermissionRequirement Require(
            string message,
            params string[] permissionKeys)
        {
            return new PermissionRequirement(
                permissionKeys,
                message);
        }

        private static bool EqualsAction(
            string action,
            params string[] names)
        {
            return names.Any(name =>
                string.Equals(
                    action,
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static IActionResult BuildDeniedResult(
            HttpContext httpContext,
            string message)
        {
            bool ajax =
                string.Equals(
                    httpContext.Request.Headers[
                        "X-Requested-With"],
                    "XMLHttpRequest",
                    StringComparison.OrdinalIgnoreCase) ||
                httpContext.Request.Headers[
                    "Accept"]
                    .Any(value =>
                        value?.Contains(
                            "application/json",
                            StringComparison.OrdinalIgnoreCase) ==
                        true);

            if (!ajax)
            {
                return new ForbidResult();
            }

            return new JsonResult(
                new
                {
                    success = false,
                    message
                })
            {
                StatusCode =
                    StatusCodes.Status403Forbidden
            };
        }

        private sealed record PermissionRequirement(
            string[] PermissionKeys,
            string Message);
    }
}
