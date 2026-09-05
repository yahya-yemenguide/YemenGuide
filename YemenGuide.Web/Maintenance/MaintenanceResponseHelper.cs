using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace YemenGuide.Web.Maintenance
{
    /// <summary>
    /// أدوات موحدة لاستجابات وضع الصيانة.
    /// الهدف أن تكون رسائل الـ AJAX/Fetch والتحويل لصفحة الخدمة المتوقفة
    /// متطابقة في جميع أجزاء المحرك.
    /// </summary>
    public static class MaintenanceResponseHelper
    {
        public static bool ExpectsJson(HttpRequest request)
        {
            if (request.Headers.TryGetValue(
                    "X-Requested-With",
                    out var requestedWith) &&
                string.Equals(
                    requestedWith.ToString(),
                    "XMLHttpRequest",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var accept =
                request.Headers.Accept.ToString();

            if (accept.Contains(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(
                    request.ContentType) &&
                request.ContentType.Contains(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // fetch/XHR الحديثة غالبًا تكون Sec-Fetch-Dest = empty.
            if (request.Headers.TryGetValue(
                    "Sec-Fetch-Dest",
                    out var fetchDest) &&
                string.Equals(
                    fetchDest.ToString(),
                    "empty",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static void PrepareServiceUnavailableResponse(
            HttpResponse response,
            MaintenanceState state)
        {
            response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";

            response.Headers.Pragma =
                "no-cache";

            response.Headers.Expires =
                "0";

            if (!state.EndAtUtc.HasValue)
            {
                return;
            }

            var seconds =
                (int)Math.Ceiling(
                    (state.EndAtUtc.Value -
                     DateTime.UtcNow)
                    .TotalSeconds);

            if (seconds > 0)
            {
                response.Headers["Retry-After"] =
                    seconds.ToString();
            }
        }

        public static JsonResult CreateBlockedJsonResult(
            MaintenanceOperation operation,
            MaintenanceState state,
            HttpRequest request)
        {
            var redirectUrl =
                BuildRestrictedUrl(
                    operation,
                    BuildReturnUrl(request));

            return new JsonResult(new
            {
                success = false,
                maintenance = true,
                type = MaintenanceTypeNames.Partial,
                operation = operation.ToString(),
                serviceName = GetOperationName(operation),
                message = GetOperationMessage(operation),
                redirectUrl,
                retryAtUtc = state.EndAtUtc
            })
            {
                StatusCode =
                    StatusCodes.Status503ServiceUnavailable
            };
        }

        public static string BuildRestrictedUrl(
            MaintenanceOperation operation,
            string? returnUrl)
        {
            var url =
                "/maintenance/restricted" +
                "?operation=" +
                Uri.EscapeDataString(
                    operation.ToString());

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                url +=
                    "&returnUrl=" +
                    Uri.EscapeDataString(returnUrl);
            }

            return url;
        }

        public static string BuildReadOnlyUrl(
            string? returnUrl)
        {
            var url =
                "/maintenance/restricted?mode=readonly";

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                url +=
                    "&returnUrl=" +
                    Uri.EscapeDataString(returnUrl);
            }

            return url;
        }

        public static string BuildReturnUrl(
            HttpRequest request)
        {
            return
                $"{request.PathBase}" +
                $"{request.Path}" +
                $"{request.QueryString}";
        }

        public static string GetOperationName(
            MaintenanceOperation? operation)
        {
            return operation switch
            {
                MaintenanceOperation.Bookings =>
                    "الحجوزات",

                MaintenanceOperation.Payments =>
                    "المدفوعات",

                MaintenanceOperation.OwnerChanges =>
                    "تعديلات المالك",

                MaintenanceOperation.Messages =>
                    "الرسائل",

                MaintenanceOperation.Reviews =>
                    "التقييمات",

                MaintenanceOperation.Registrations =>
                    "إنشاء الحسابات",

                _ =>
                    "هذه الخدمة"
            };
        }

        public static string GetOperationMessage(
            MaintenanceOperation? operation)
        {
            return operation switch
            {
                MaintenanceOperation.Bookings =>
                    "الحجوزات متوقفة مؤقتًا أثناء أعمال الصيانة. يمكنك مواصلة تصفح المنشآت ومشاهدة حجوزاتك السابقة، لكن تنفيذ عمليات الحجز أو تغيير حالتها متوقف حاليًا.",

                MaintenanceOperation.Payments =>
                    "عمليات الدفع متوقفة مؤقتًا أثناء أعمال الصيانة. يمكنك متابعة بياناتك الحالية، لكن رفع إثباتات الدفع أو مراجعتها أو تنفيذ العمليات المالية المتأثرة متوقف حاليًا.",

                MaintenanceOperation.OwnerChanges =>
                    "تعديلات المالك على المنشآت والغرف والباقات والخدمات والسياسات والعروض متوقفة مؤقتًا، بينما تظل البيانات الحالية متاحة للعرض.",

                MaintenanceOperation.Messages =>
                    "إرسال الرسائل أو تعديلها أو حذفها متوقف مؤقتًا أثناء أعمال الصيانة. يمكنك الاستمرار في مشاهدة المحادثات السابقة.",

                MaintenanceOperation.Reviews =>
                    "إضافة أو تعديل أو حذف التقييمات والردود والتفاعلات عليها متوقف مؤقتًا. يمكنك متابعة مشاهدة التقييمات الحالية.",

                MaintenanceOperation.Registrations =>
                    "إنشاء الحسابات الجديدة واستكمال خطوات التسجيل الجديدة متوقف مؤقتًا أثناء أعمال الصيانة. تسجيل الدخول للحسابات الموجودة يظل متاحًا.",

                _ =>
                    "هذه الخدمة متوقفة مؤقتًا أثناء أعمال الصيانة، بينما تظل بقية المنصة متاحة حسب الإعدادات الحالية."
            };
        }

        public static string GetOperationIcon(
            MaintenanceOperation? operation)
        {
            return operation switch
            {
                MaintenanceOperation.Bookings =>
                    "bi-calendar2-check",

                MaintenanceOperation.Payments =>
                    "bi-credit-card-2-front",

                MaintenanceOperation.OwnerChanges =>
                    "bi-building-gear",

                MaintenanceOperation.Messages =>
                    "bi-chat-dots",

                MaintenanceOperation.Reviews =>
                    "bi-star",

                MaintenanceOperation.Registrations =>
                    "bi-person-plus",

                _ =>
                    "bi-tools"
            };
        }
    }
}
