using System;
using YemenGuide.Core.DTOs.Admin;

namespace YemenGuide.Web.Services.Advertisements
{
    public sealed class AdvertisementPricingService : IAdvertisementPricingService
    {
        public AdvertisementPriceQuoteDto Calculate(AdvertisementPriceQuoteRequestDto request)
        {
            var model = NormalizeBillingModel(request.BillingModel);
            if (!AdvertisementLookups.IsBillingModel(model))
                return Fail("طريقة المحاسبة المحددة غير صحيحة.");

            if (request.UnitPrice <= 0)
                return Fail("سعر وحدة المحاسبة يجب أن يكون أكبر من صفر.");

            if (request.DurationDays < 1 || request.DurationDays > 365)
                return Fail("مدة الإعلان يجب أن تكون بين يوم و365 يوماً.");

            if (model == "Flat")
            {
                var total = decimal.Round(request.UnitPrice * request.DurationDays, 2, MidpointRounding.AwayFromZero);
                return new AdvertisementPriceQuoteDto
                {
                    Success = true,
                    Message = "تم احتساب السعر.",
                    FinalAmount = total,
                    DailyAmount = request.UnitPrice
                };
            }

            var budget = request.TotalBudget.GetValueOrDefault();
            if (budget <= 0)
                return Fail("أدخل ميزانية كلية أكبر من صفر.");

            if (request.MinimumTotalBudget.HasValue && budget < request.MinimumTotalBudget.Value)
                return Fail($"الميزانية لا يمكن أن تقل عن {request.MinimumTotalBudget.Value:N0}.");

            if (request.DailyBudget.HasValue && request.DailyBudget.Value <= 0)
                return Fail("الميزانية اليومية يجب أن تكون أكبر من صفر.");

            if (request.DailyBudget.HasValue && request.DailyBudget.Value > budget)
                return Fail("الميزانية اليومية لا يمكن أن تتجاوز الميزانية الكلية.");

            var minimumCharge = model == "CPC"
                ? request.UnitPrice
                : decimal.Round(request.UnitPrice / 1000m, 2, MidpointRounding.AwayFromZero);
            minimumCharge = Math.Max(0.01m, minimumCharge);

            if (budget < minimumCharge)
                return Fail(model == "CPC"
                    ? $"الميزانية الكلية لا تكفي لنقرة واحدة. يجب ألا تقل عن {minimumCharge:N2}."
                    : $"الميزانية الكلية لا تكفي لظهور واحد. يجب ألا تقل عن {minimumCharge:N2}.");

            if (request.DailyBudget.HasValue && request.DailyBudget.Value < minimumCharge)
                return Fail(model == "CPC"
                    ? $"الميزانية اليومية يجب أن تسمح بنقرة واحدة على الأقل، وألا تقل عن {minimumCharge:N2}."
                    : $"الميزانية اليومية يجب أن تسمح بظهور واحد على الأقل، وألا تقل عن {minimumCharge:N2}.");

            var estimatedClicks = model == "CPC"
                ? (long)Math.Floor(budget / request.UnitPrice)
                : (long?)null;
            var estimatedImpressions = model == "CPM"
                ? (long)Math.Floor(budget / request.UnitPrice * 1000m)
                : (long?)null;

            if (model == "CPC" && estimatedClicks.GetValueOrDefault() < 1)
                return Fail("الميزانية الكلية لا تنتج نقرة واحدة. ارفع الميزانية أو اختر باقة أخرى.");

            if (model == "CPM" && estimatedImpressions.GetValueOrDefault() < 1)
                return Fail("الميزانية الكلية لا تنتج ظهوراً واحداً. ارفع الميزانية أو اختر باقة أخرى.");

            return new AdvertisementPriceQuoteDto
            {
                Success = true,
                Message = "تم احتساب النتائج المتوقعة.",
                FinalAmount = budget,
                DailyAmount = request.DailyBudget,
                EstimatedClicks = estimatedClicks,
                EstimatedImpressions = estimatedImpressions
            };
        }

        public string? ValidatePlan(AdminAdvertisementPlanUpsertDto request)
        {
            if (request == null)
                return "بيانات الباقة غير صحيحة.";

            request.PlanName = request.PlanName?.Trim() ?? string.Empty;

            if (request.PlanName.Length < 2 || request.PlanName.Length > 100)
                return "اسم الباقة يجب أن يكون بين 2 و100 حرف.";

            if (request.DailyPrice <= 0m || request.DailyPrice > 999999999m)
                return "سعر اليوم يجب أن يكون أكبر من صفر.";

            if (request.DisplayWeight < 1 || request.DisplayWeight > 100)
                return "وزن الظهور يجب أن يكون بين 1 و100.";

            if (!AdvertisementLookups.IsPlacement(request.PlacementType))
                return "مكان الظهور الداخلي غير صحيح.";

            return null;
        }

        private static string NormalizeBillingModel(string? value) =>
            string.Equals(value, "CPC", StringComparison.OrdinalIgnoreCase) ? "CPC" :
            string.Equals(value, "CPM", StringComparison.OrdinalIgnoreCase) ? "CPM" :
            string.Equals(value, "Flat", StringComparison.OrdinalIgnoreCase) ? "Flat" :
            value?.Trim() ?? string.Empty;

        private static AdvertisementPriceQuoteDto Fail(string message) =>
            new() { Success = false, Message = message };
    }

    internal static class AdvertisementLookups
    {
        // أماكن الظهور الفعلية في واجهة العميل.
        // الفئة والمدينة هما استهداف داخل SearchTop وليستا مكانَي ظهور مستقلين.
        internal static readonly string[] Placements =
        {
            "SearchTop"
        };

        internal static bool IsPlacement(string? value) =>
            Array.Exists(Placements, item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));

        internal static bool IsBillingModel(string? value) =>
            string.Equals(value, "Flat", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "CPC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "CPM", StringComparison.OrdinalIgnoreCase);

        internal static string PlacementLabel(string? value) => value switch
        {
            "HomeSlider" => "السلايدر الرئيسي",
            "FeaturedPlaces" => "الأماكن المميزة",
            "SearchTop" => "صفحة البحث",
            // توافق مع البيانات التاريخية فقط؛ لا تُعرض كأماكن جديدة للأدمن.
            "CategoryTop" => "أعلى نتائج البحث والاستكشاف",
            "CityPage" => "أعلى نتائج البحث والاستكشاف",
            "HomeBanner" => "بنر الصفحة الرئيسية",
            "MapPin" => "علامة مميزة على الخريطة",
            "PlaceDetails" => "صفحة تفاصيل المنشأة",
            _ => value ?? "غير محدد"
        };

        internal static string PlacementDescription(string value) =>
            "كل إعلان معتمد يظهر في مساحة الإعلانات داخل صفحة البحث أعلى الفئات.";

        internal static string PlacementIcon(string value) => "bi-search-heart";

        internal static string BillingLabel(string? value) => value switch
        {
            "Flat" => "سعر يومي",
            // القيم التالية للتوافق التاريخي مع إعلانات قديمة فقط.
            "CPC" => "مقابل النقرات",
            "CPM" => "مقابل ألف ظهور",
            _ => value ?? "غير محدد"
        };

        internal static string StatusLabel(string? value) => value switch
        {
            "Draft" => "مسودة",
            "AwaitingPayment" => "بانتظار الدفع",
            "Pending" => "قيد المراجعة",
            "Scheduled" => "مجدول",
            "Active" => "نشط",
            "Paused" => "متوقف مؤقتاً",
            "Expired" => "منتهي",
            "Rejected" => "مرفوض / مطلوب تعديل",
            "Cancelled" => "ملغى",
            _ => value ?? "غير محدد"
        };

        internal static string PaymentLabel(string? value) => value switch
        {
            "Unpaid" => "غير مدفوع",
            "PendingReview" => "الدفع قيد المراجعة",
            "Paid" => "مدفوع",
            "Rejected" => "إثبات الدفع مرفوض",
            "Refunded" => "مسترد",
            _ => value ?? "غير محدد"
        };
    }
}
