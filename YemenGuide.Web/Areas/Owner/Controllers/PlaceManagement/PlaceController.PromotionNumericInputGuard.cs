using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    /// <summary>
    /// المرحلة الأولى من ضبط حقول العروض والخصومات.
    /// يفحص النص الخام القادم من النموذج قبل تنفيذ AddPromotion / EditPromotion حتى لا
    /// تُقبل صيغ رقمية لا نريدها مثل +5 أو -5 أو 1e3 حتى لو استطاع Model Binder تفسيرها.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal sealed class StrictPromotionNumericInputAttribute : ActionFilterAttribute
    {
        private static readonly Regex DecimalPattern = new(
            @"^[0-9]+(?:\.[0-9]+)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex IntegerPattern = new(
            @"^[0-9]+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly IReadOnlyDictionary<string, string> DecimalFields =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DiscountValue"] = "قيمة الخصم",
                ["MaximumDiscountAmount"] = "أقصى مبلغ للخصم",
                ["MinimumBookingAmount"] = "أقل قيمة للحجز",
                ["SpecialPrice"] = "السعر الخاص"
            };

        private static readonly IReadOnlyDictionary<string, string> IntegerFields =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PromotionId"] = "رقم العرض",
                ["RequiredQuantity"] = "الكمية المطلوبة",
                ["RewardQuantity"] = "كمية المكافأة",
                ["MinimumNights"] = "عدد الليالي",
                ["MinimumPeople"] = "عدد الأشخاص",
                ["TotalUsageLimit"] = "إجمالي مرات الاستخدام",
                ["PerCustomerUsageLimit"] = "مرات الاستخدام للعميل",
                ["RoomId"] = "الغرفة",
                ["PackageId"] = "الباقة",
                ["ServiceId"] = "الخدمة",
                ["RewardServiceId"] = "خدمة المكافأة"
            };

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            string actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
            bool isPromotionWrite =
                actionName.Equals("AddPromotion", StringComparison.OrdinalIgnoreCase) ||
                actionName.Equals("EditPromotion", StringComparison.OrdinalIgnoreCase);

            if (!isPromotionWrite)
                return;

            var request = context.HttpContext.Request;
            if (!request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                !request.HasFormContentType)
            {
                return;
            }

            var form = request.Form;

            if (!ValidateFields(form, DecimalFields, DecimalPattern, allowEmpty: true, out string? decimalError))
            {
                context.Result = Failure(decimalError!);
                return;
            }

            if (!ValidateFields(form, IntegerFields, IntegerPattern, allowEmpty: true, out string? integerError))
            {
                context.Result = Failure(integerError!);
            }
        }

        private static bool ValidateFields(
            Microsoft.AspNetCore.Http.IFormCollection form,
            IReadOnlyDictionary<string, string> fields,
            Regex pattern,
            bool allowEmpty,
            out string? error)
        {
            error = null;

            foreach (var field in fields)
            {
                if (!form.TryGetValue(field.Key, out StringValues values))
                    continue;

                foreach (string? rawValue in values)
                {
                    string raw = rawValue ?? string.Empty;

                    if (string.IsNullOrEmpty(raw) && allowEmpty)
                        continue;

                    // لا نستخدم Trim هنا عمدًا: المسافات والرموز ليست جزءًا من صيغة الرقم المسموحة.
                    if (!pattern.IsMatch(raw))
                    {
                        error = $"{field.Value}: استخدم الأرقام فقط بدون + أو - أو حروف أو رموز. الأسعار والقيم المالية تسمح بالنقطة العشرية فقط.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static JsonResult Failure(string message)
        {
            return new JsonResult(new
            {
                success = false,
                message
            });
        }
    }

    // وضع السمة في ملف partial مستقل يجعل الحماية جزءًا من PlaceController
    // بدون تعديل أي منطق قائم داخل ملف العروض نفسه.
    [StrictPromotionNumericInput]
    public partial class PlaceController
    {
    }
}
