using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;

namespace YemenGuide.Web.Maintenance
{
    /// <summary>
    /// الخريطة المركزية للصيانة الجزئية.
    ///
    /// لا نضيف MaintenanceGuard إلى عشرات الـ Controllers.
    /// بدل ذلك نقرأ اسم الـ Area + Controller + Action
    /// ونصنّف العملية قبل دخول Action نفسه.
    ///
    /// بهذه الطريقة:
    /// - العرض والتصفح يظلان متاحين.
    /// - العملية الموقوفة تُمنع قبل أي SaveChanges أو رفع ملف.
    /// - الأدمن مستثنى من خلال IMaintenanceService.
    /// - المالك يتأثر فقط عند MaintenanceAffectOwners = true.
    /// </summary>
    public sealed class MaintenanceOperationPolicy
    {
        private readonly AppDbContext _db;

        private static readonly HashSet<string>
            CustomerMessageWriteActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "SendMessage",
                    "EditMessage",
                    "DeleteMessageForMe",
                    "DeleteMessageForEveryone",
                    "ClearChatForMe"
                };

        private static readonly HashSet<string>
            OwnerChatWriteActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "SendMessage",
                    "EditMessage",
                    "DeleteMessageForMe",
                    "DeleteMessageForEveryone",
                    "ClearChatForMe"
                };

        private static readonly HashSet<string>
            CustomerReviewActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "AddReview",
                    "AddReply",
                    "EditReview",
                    "DeleteReview",
                    "ToggleReviewLike"
                };

        private static readonly HashSet<string>
            OwnerReviewActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "ToggleLike",
                    "AddReply",
                    "EditReply",
                    "DeleteReply"
                };

        private static readonly HashSet<string>
            OwnerBookingActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "PreviewManualBooking",
                    "CreateManualBooking",
                    "AcceptBooking",
                    "RejectBooking",
                    "RejectBookingPermanently",
                    "CompleteBooking",
                    "NoShowBooking",
                    "CancelBookingByOwner"
                };

        private static readonly HashSet<string>
            OwnerPaymentActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "RecordManualBookingPayment",
                    "ReviewPayment",
                    "ApprovePayment",
                    "RejectPayment"
                };

        private static readonly HashSet<string>
            OwnerCalendarChangeActions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "AddCalendarException",
                    "CloseCalendarRange",
                    "ReopenCalendarRange"
                };

        public MaintenanceOperationPolicy(
            AppDbContext db)
        {
            _db = db;
        }

        public async Task<
            IReadOnlyList<MaintenanceOperation>>
            ResolveOperationsAsync(
                ActionExecutingContext context,
                CancellationToken cancellationToken)
        {
            if (context.ActionDescriptor
                is not ControllerActionDescriptor descriptor)
            {
                return Array.Empty<MaintenanceOperation>();
            }

            var controller =
                descriptor.ControllerName;

            var action =
                descriptor.ActionName;

            descriptor.RouteValues.TryGetValue(
                "area",
                out var area);

            area ??= string.Empty;

            var request =
                context.HttpContext.Request;

            // =========================================================
            // 1) التسجيل وإنشاء الحسابات
            // =========================================================
            if (EqualsValue(
                    controller,
                    "Account"))
            {
                if (EqualsValue(
                        action,
                        "Register"))
                {
                    // GET صفحة التسجيل + POST إنشاء الحساب.
                    return One(
                        MaintenanceOperation.Registrations);
                }

                if (EqualsValue(
                        action,
                        "UploadVerification") ||
                    EqualsValue(
                        action,
                        "FinalizeActivation"))
                {
                    // استكمال رحلة تسجيل/اعتماد المالك.
                    return One(
                        MaintenanceOperation.Registrations);
                }

                if (EqualsValue(
                        action,
                        "GoogleResponse") &&
                    await IsGoogleResponseCreatingNewUserAsync(
                        context,
                        cancellationToken))
                {
                    // لا نمنع دخول Google للحساب الموجود.
                    // المنع فقط إذا كان callback سيُنشئ User جديدًا.
                    return One(
                        MaintenanceOperation.Registrations);
                }

                return Array.Empty<MaintenanceOperation>();
            }

            // =========================================================
            // 2) العميل - الحجوزات
            // =========================================================
            if (string.IsNullOrEmpty(area) &&
                EqualsValue(
                    controller,
                    "BookingFlow"))
            {
                if (EqualsValue(
                        action,
                        "Checkout") ||
                    EqualsValue(
                        action,
                        "Create"))
                {
                    return One(
                        MaintenanceOperation.Bookings);
                }

                if (EqualsValue(
                        action,
                        "Payment") ||
                    EqualsValue(
                        action,
                        "SubmitPaymentProof"))
                {
                    return One(
                        MaintenanceOperation.Payments);
                }

                // ExpirePayment عملية اتساق/تنظيف للحجز
                // ولا نوقفها حتى أثناء Partial.
                return Array.Empty<MaintenanceOperation>();
            }

            if (string.IsNullOrEmpty(area) &&
                EqualsValue(
                    controller,
                    "Bookings"))
            {
                if (EqualsValue(
                        action,
                        "GetCancellationQuote") ||
                    EqualsValue(
                        action,
                        "CancelBooking"))
                {
                    return One(
                        MaintenanceOperation.Bookings);
                }

                return Array.Empty<MaintenanceOperation>();
            }

            // =========================================================
            // 3) العميل - الرسائل
            // =========================================================
            if (string.IsNullOrEmpty(area) &&
                EqualsValue(
                    controller,
                    "Messages") &&
                CustomerMessageWriteActions.Contains(
                    action))
            {
                return One(
                    MaintenanceOperation.Messages);
            }

            // =========================================================
            // 4) العميل - التقييمات
            // =========================================================
            if (string.IsNullOrEmpty(area) &&
                EqualsValue(
                    controller,
                    "Places") &&
                CustomerReviewActions.Contains(
                    action))
            {
                return One(
                    MaintenanceOperation.Reviews);
            }

            // =========================================================
            // 5) المالك - الرسائل
            // =========================================================
            if (EqualsValue(
                    area,
                    "Owner") &&
                EqualsValue(
                    controller,
                    "Chat") &&
                OwnerChatWriteActions.Contains(
                    action))
            {
                return One(
                    MaintenanceOperation.Messages);
            }

            // BlockUser / UnblockUser مقصود أن يظلا متاحين
            // كأدوات أمان حتى لو توقفت خدمة الرسائل.

            // =========================================================
            // 6) المالك - التقييمات
            // =========================================================
            if (EqualsValue(
                    area,
                    "Owner") &&
                EqualsValue(
                    controller,
                    "OwnerReviews") &&
                OwnerReviewActions.Contains(
                    action))
            {
                return One(
                    MaintenanceOperation.Reviews);
            }

            // =========================================================
            // 7) المالك - الحجوزات/المدفوعات/التقويم
            // =========================================================
            if (EqualsValue(
                    area,
                    "Owner") &&
                EqualsValue(
                    controller,
                    "Reservation"))
            {
                if (OwnerPaymentActions.Contains(
                        action))
                {
                    return One(
                        MaintenanceOperation.Payments);
                }

                if (OwnerCalendarChangeActions.Contains(
                        action))
                {
                    return One(
                        MaintenanceOperation.OwnerChanges);
                }

                if (OwnerBookingActions.Contains(
                        action))
                {
                    var operations =
                        new List<MaintenanceOperation>
                        {
                            MaintenanceOperation.Bookings
                        };

                    if (EqualsValue(
                            action,
                            "CreateManualBooking") &&
                        ManualBookingContainsCollectedPayment(
                            context))
                    {
                        operations.Add(
                            MaintenanceOperation.Payments);
                    }

                    if (EqualsValue(
                            action,
                            "CompleteBooking") &&
                        ActionDecimalArgumentIsPositive(
                            context,
                            "collectedAmount"))
                    {
                        operations.Add(
                            MaintenanceOperation.Payments);
                    }

                    return operations;
                }

                return Array.Empty<MaintenanceOperation>();
            }

            // =========================================================
            // 8) المالك - تعديلات المنشأة
            // =========================================================
            if (EqualsValue(
                    area,
                    "Owner") &&
                EqualsValue(
                    controller,
                    "Place") &&
                IsWriteRequest(request.Method))
            {
                // يشمل:
                // AddFullPlace / EditPlace / Delete...
                // الغرف / الخدمات / المرافق / الباقات / الصور
                // العروض والترويجات / حالة المنشأة...
                return One(
                    MaintenanceOperation.OwnerChanges);
            }

            if (EqualsValue(
                    area,
                    "Owner") &&
                EqualsValue(
                    controller,
                    "Settings") &&
                IsWriteRequest(request.Method))
            {
                // إعدادات وسياسات المنشأة.
                return One(
                    MaintenanceOperation.OwnerChanges);
            }

            // =========================================================
            // 9) المالك - العمليات المالية
            // =========================================================
            if (EqualsValue(
                    area,
                    "Owner") &&
                controller.StartsWith(
                    "OwnerFinancial",
                    StringComparison.OrdinalIgnoreCase) &&
                IsWriteRequest(request.Method))
            {
                // يشمل حاليًا:
                // BalanceTopUp.Submit / Cancel
                // ReceivingWallets.Save / ChangeStatus
                // وأي عملية مالية كتابية تضاف لاحقًا في نفس الوحدة.
                return One(
                    MaintenanceOperation.Payments);
            }

            // =========================================================
            // 10) ما لا يدخل ضمن الخدمات الست يبقى طبيعيًا
            // =========================================================
            // أمثلة مقصودة:
            // - الملف الشخصي
            // - تغيير اللغة
            // - المفضلة
            // - قراءة الإشعارات/تعليمها كمقروءة
            // - الدعم والبلاغات
            // - Contact.SendMessage (نموذج تواصل وليس محادثة)
            return Array.Empty<MaintenanceOperation>();
        }

        private async Task<bool>
            IsGoogleResponseCreatingNewUserAsync(
                ActionExecutingContext context,
                CancellationToken cancellationToken)
        {
            try
            {
                var authResult =
                    await context.HttpContext
                        .AuthenticateAsync(
                            CookieAuthenticationDefaults
                                .AuthenticationScheme);

                if (!authResult.Succeeded ||
                    authResult.Principal == null)
                {
                    return false;
                }

                var email =
                    authResult.Principal
                        .FindFirstValue(
                            ClaimTypes.Email);

                if (string.IsNullOrWhiteSpace(email))
                {
                    return false;
                }

                email =
                    email.Trim();

                return !await _db.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user =>
                            user.ContactInfo == email,
                        cancellationToken);
            }
            catch
            {
                // إذا تعذر فحص Google لا نكسر تسجيل الدخول.
                // الـ AccountController يظل مسؤولًا عن التحقق النهائي.
                return false;
            }
        }

        private static bool
            ManualBookingContainsCollectedPayment(
                ActionExecutingContext context)
        {
            if (!context.ActionArguments
                    .TryGetValue(
                        "request",
                        out var request) ||
                request == null)
            {
                return false;
            }

            var paymentMode =
                ReadStringProperty(
                    request,
                    "PaymentMode");

            var paidAmount =
                ReadDecimalProperty(
                    request,
                    "PaidAmount");

            return
                string.Equals(
                    paymentMode,
                    "Collected",
                    StringComparison.OrdinalIgnoreCase) &&
                paidAmount > 0m;
        }

        private static bool
            ActionDecimalArgumentIsPositive(
                ActionExecutingContext context,
                string argumentName)
        {
            if (!context.ActionArguments.TryGetValue(
                    argumentName,
                    out var value) ||
                value == null)
            {
                return false;
            }

            return ToDecimal(value) > 0m;
        }

        private static string?
            ReadStringProperty(
                object instance,
                string propertyName)
        {
            var property =
                instance.GetType()
                    .GetProperty(
                        propertyName);

            return property?
                .GetValue(instance)?
                .ToString();
        }

        private static decimal
            ReadDecimalProperty(
                object instance,
                string propertyName)
        {
            var property =
                instance.GetType()
                    .GetProperty(
                        propertyName);

            if (property == null)
            {
                return 0m;
            }

            return ToDecimal(
                property.GetValue(instance));
        }

        private static decimal ToDecimal(
            object? value)
        {
            if (value == null)
            {
                return 0m;
            }

            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            return decimal.TryParse(
                    value.ToString(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                ? parsed
                : 0m;
        }

        private static bool IsWriteRequest(
            string method)
        {
            return
                !HttpMethods.IsGet(method) &&
                !HttpMethods.IsHead(method) &&
                !HttpMethods.IsOptions(method);
        }

        private static bool EqualsValue(
            string? value,
            string expected)
        {
            return string.Equals(
                value,
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<
            MaintenanceOperation> One(
                MaintenanceOperation operation)
        {
            return new[] { operation };
        }
    }
}
