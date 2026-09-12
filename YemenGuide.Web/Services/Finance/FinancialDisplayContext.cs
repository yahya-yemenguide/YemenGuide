using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// Read-only formatting. DTOs, ledgers and monetary form fields remain YER.
    /// Cross-market totals are split by the original query filters before conversion.
    /// No reflection-based mutation of DTOs or database entities is performed.
    /// </summary>
    public sealed class FinancialDisplayContext
    {
        private readonly Dictionary<int, string> _markets = new();
        private decimal _northRate = 1m;
        private decimal _southRate = 1m;
        private int? _placeId;
        public string Currency { get; private set; } = CurrencyDisplayService.YemeniRial;
        public bool IsForeign => Currency != CurrencyDisplayService.YemeniRial;
        public string Note => !IsForeign
            ? "المبالغ بالريال اليمني."
            : $"عملة العرض: {Currency}. أسعار العرض الحالية: صنعاء {_northRate.ToString("0.####", CultureInfo.InvariantCulture)}، عدن {_southRate.ToString("0.####", CultureInfo.InvariantCulture)} ريال يمني للوحدة. الحقول المدخلة والسجلات الأصلية بالريال اليمني.";

        public static async Task<FinancialDisplayContext> CreateAsync(
            HttpContext http, object? model, CancellationToken cancellationToken = default)
        {
            if (model is AdminFinancialReportPrintDto print) model = print.Report;
            if (model is AdminFinancialDocumentPrintDto document) model = document.Document;
            var result = new FinancialDisplayContext();
            result.Currency = await http.RequestServices.GetRequiredService<CurrencyDisplayService>()
                .GetPreferredCurrencyAsync(cancellationToken);
            if (!result.IsForeign) return result;

            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var settings = await db.SystemSettings.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new { x.SanaaUsdRate, x.SanaaSarRate, x.AdenUsdRate, x.AdenSarRate })
                .FirstOrDefaultAsync(cancellationToken);
            if (settings == null) throw new InvalidOperationException("إعدادات أسعار الصرف غير موجودة.");
            bool usd = result.Currency == CurrencyDisplayService.UsDollar;
            result._northRate = usd ? settings.SanaaUsdRate : settings.SanaaSarRate;
            result._southRate = usd ? settings.AdenUsdRate : settings.AdenSarRate;
            if (result._northRate <= 0m || result._southRate <= 0m)
                throw new InvalidOperationException("صحح أسعار الصرف في إعدادات الإدارة قبل عرض المبالغ بعملة أجنبية.");

            result._placeId = ReadId(model, "PlaceId") ?? ReadId(model, "SelectedPlaceId")
                ?? ReadId(model, "Filter.PlaceId") ?? ReadId(model, "Transactions.Filter.PlaceId")
                ?? ReadId(model, "TopUp.SelectedPlaceId");
            bool adminPage = model?.GetType().Namespace == typeof(AdminFinancialOverviewPageDto).Namespace;
            var placesQuery = db.Places.AsNoTracking().AsQueryable();
            if (!adminPage)
            {
                if (!int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
                    throw new InvalidOperationException("تعذر تحديد المالك لعرض المبالغ.");
                placesQuery = placesQuery.Where(p => p.OwnerId == ownerId);
            }
            var places = await placesQuery.Select(p => new
            {
                p.PlaceId, Region = p.City.ExchangeRateRegion
            }).ToListAsync(cancellationToken);
            foreach (var place in places)
                result._markets[place.PlaceId] = string.Equals(place.Region, "South", StringComparison.OrdinalIgnoreCase)
                    ? "South" : "North";
            // Missing scope never guesses a market for a mixed total.
            if (!result._placeId.HasValue && !adminPage && places.Count == 1)
                result._placeId = places[0].PlaceId;
            if (result._placeId.HasValue && !result._markets.ContainsKey(result._placeId.Value))
                result._placeId = null;

            // لا ننفذ استعلامات التقارير مرة أخرى من داخل Razor؛ كان ذلك
            // يعيد تحميل التبويب مرتين ويترك واجهة AJAX على شاشة الانتظار.
            // صفوف المنشآت تُعرض بعملة البروفايل، أما الإجمالي المختلط بلا
            // منشأة محددة فيبقى YER حتى لا يطبق عليه سعر سوق غير صحيح.
            return result;
        }

        private static object? Read(object? model, string path)
        {
            foreach (var part in path.Split('.')) model = model?.GetType().GetProperty(part)?.GetValue(model);
            return model;
        }
        private static int? ReadId(object? model, string path) => Read(model, path) is int id && id > 0 ? id : null;
        private static string Render(decimal value, string currency) =>
            $"{BookingMoneyPolicy.Round(value).ToString("N2", CultureInfo.InvariantCulture)} {currency}";
        private decimal Rate(int placeId) => _markets[placeId] == "South" ? _southRate : _northRate;

        public string Money(decimal yer, int? placeId = null)
        {
            var id = placeId ?? _placeId;
            if (!IsForeign || !id.HasValue || !_markets.ContainsKey(id.Value))
                return Render(yer, CurrencyDisplayService.YemeniRial);
            return Render(yer / Rate(id.Value), Currency);
        }

        public string Summary(string path, decimal yer)
        {
            return !IsForeign || _placeId.HasValue
                ? Money(yer)
                : Render(yer, CurrencyDisplayService.YemeniRial);
        }

        public string Metric(AdminFinancialReportMetricDto metric)
        {
            if (!metric.NumericValue.HasValue || !metric.Value.Contains(CurrencyDisplayService.YemeniRial))
                return metric.Value;
            return !IsForeign || _placeId.HasValue
                ? Money(metric.NumericValue.Value)
                : metric.Value;
        }

        public string Cell(AdminFinancialReportCellDto? cell, int placeId) =>
            cell?.DataType == AdminFinancialReportDataTypes.Money && cell.DecimalValue.HasValue
                ? Money(cell.DecimalValue.Value, placeId) : cell?.Text ?? "—";
        public string Cell(AdminFinancialReportExportCellDto? cell, int placeId) =>
            cell?.DataType == AdminFinancialReportDataTypes.Money && cell.DecimalValue.HasValue
                ? Money(cell.DecimalValue.Value, placeId) : cell?.Text ?? "—";
    }
}
