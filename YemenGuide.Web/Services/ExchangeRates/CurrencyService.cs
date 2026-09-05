using System;
using System.Linq;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Services
{
    /// <summary>
    /// خدمة التحويل الأساسية من الريال اليمني إلى العملة المطلوبة.
    ///
    /// North = سوق صنعاء.
    /// South = سوق عدن.
    ///
    /// هذه الخدمة لا تقرر عملة المستخدم؛ هي فقط تنفذ التحويل.
    /// تحديد عملة المستخدم يتم في CurrencyDisplayService.
    /// </summary>
    public class CurrencyService
    {
        private const decimal DefaultSanaaUsdRate = 537m;
        private const decimal DefaultSanaaSarRate = 140m;
        private const decimal DefaultAdenUsdRate = 1900m;
        private const decimal DefaultAdenSarRate = 500m;

        private readonly AppDbContext _context;

        public CurrencyService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// توافق مع الاستدعاءات القديمة التي لا تمرر مدينة المنشأة.
        /// سوق صنعاء هو المرجع الاحتياطي فقط.
        /// </summary>
        public string ConvertPrice(
            decimal priceInYer,
            string targetCurrency)
        {
            return ConvertPrice(
                priceInYer,
                targetCurrency,
                ExchangeRateRegionResolver.SanaaMarket);
        }

        /// <summary>
        /// تحويل وعرض المبلغ بحسب رقم مدينة المنشأة.
        /// </summary>
        public string ConvertPrice(
            decimal priceInYer,
            string targetCurrency,
            int cityId)
        {
            string? region = GetRegionByCityId(cityId);

            return ConvertPrice(
                priceInYer,
                targetCurrency,
                region);
        }

        /// <summary>
        /// تحويل وعرض المبلغ بحسب سوق الصرف مباشرة.
        /// </summary>
        public string ConvertPrice(
            decimal priceInYer,
            string targetCurrency,
            string? exchangeRateRegion)
        {
            decimal converted = ConvertAmount(
                priceInYer,
                targetCurrency,
                exchangeRateRegion);

            return FormatAmount(
                converted,
                targetCurrency);
        }

        /// <summary>
        /// يرجع القيمة الرقمية بعد التحويل بحسب رقم المدينة.
        /// يفيد في الإجماليات قبل تنسيق النص النهائي.
        /// </summary>
        public decimal ConvertAmount(
            decimal priceInYer,
            string targetCurrency,
            int cityId)
        {
            string? region = GetRegionByCityId(cityId);

            return ConvertAmount(
                priceInYer,
                targetCurrency,
                region);
        }

        /// <summary>
        /// يرجع القيمة الرقمية بعد التحويل بحسب السوق.
        /// </summary>
        public decimal ConvertAmount(
            decimal priceInYer,
            string targetCurrency,
            string? exchangeRateRegion)
        {
            string currency =
                CurrencyDisplayService.NormalizeCurrency(
                    targetCurrency);

            // أي قيمة قديمة أو غير معروفة تعامل كريال يمني حفاظًا على التوافق.
            if (string.IsNullOrWhiteSpace(currency) ||
                currency == CurrencyDisplayService.YemeniRial)
            {
                return Math.Round(
                    priceInYer,
                    0,
                    MidpointRounding.AwayFromZero);
            }

            var rates = GetRates(exchangeRateRegion);

            if (currency == CurrencyDisplayService.UsDollar)
            {
                return Math.Round(
                    priceInYer / rates.UsdRate,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            if (currency == CurrencyDisplayService.SaudiRiyal)
            {
                return Math.Round(
                    priceInYer / rates.SarRate,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            return Math.Round(
                priceInYer,
                0,
                MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// تنسيق مبلغ سبق تحويله بالفعل.
        /// </summary>
        public string FormatAmount(
            decimal convertedAmount,
            string targetCurrency)
        {
            string currency =
                CurrencyDisplayService.NormalizeCurrency(
                    targetCurrency);

            if (currency == CurrencyDisplayService.UsDollar)
            {
                return $"${convertedAmount:N2}";
            }

            if (currency == CurrencyDisplayService.SaudiRiyal)
            {
                return $"{convertedAmount:N2} ر.س";
            }

            return $"{Math.Round(convertedAmount, 0):N0} ر.ي";
        }

        private string? GetRegionByCityId(int cityId)
        {
            if (cityId <= 0)
            {
                return ExchangeRateRegionResolver.SanaaMarket;
            }

            return _context.Cities
                .Where(city => city.CityId == cityId)
                .Select(city => city.ExchangeRateRegion)
                .FirstOrDefault();
        }

        private (decimal UsdRate, decimal SarRate) GetRates(
            string? exchangeRateRegion)
        {
            var settings =
                _context.SystemSettings
                    .OrderBy(setting => setting.Id)
                    .FirstOrDefault();

            bool useAdenMarket =
                string.Equals(
                    exchangeRateRegion,
                    ExchangeRateRegionResolver.AdenMarket,
                    StringComparison.OrdinalIgnoreCase);

            decimal usdRate =
                useAdenMarket
                    ? settings?.AdenUsdRate ?? DefaultAdenUsdRate
                    : settings?.SanaaUsdRate ?? DefaultSanaaUsdRate;

            decimal sarRate =
                useAdenMarket
                    ? settings?.AdenSarRate ?? DefaultAdenSarRate
                    : settings?.SanaaSarRate ?? DefaultSanaaSarRate;

            if (usdRate <= 0)
            {
                usdRate = useAdenMarket
                    ? DefaultAdenUsdRate
                    : DefaultSanaaUsdRate;
            }

            if (sarRate <= 0)
            {
                sarRate = useAdenMarket
                    ? DefaultAdenSarRate
                    : DefaultSanaaSarRate;
            }

            return (usdRate, sarRate);
        }
    }
}
