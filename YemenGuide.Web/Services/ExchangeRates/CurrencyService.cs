using System;
using System.Linq;
using YemenGuide.Data;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Services
{
    /// <summary>
    /// خدمة التحويل الأساسية بين الريال اليمني وعملة العرض المطلوبة.
    ///
    /// North = سوق صنعاء.
    /// South = سوق عدن.
    ///
    /// هذه الخدمة لا تقرر عملة المستخدم؛ هي فقط تنفذ التحويل.
    /// تحديد عملة المستخدم يتم في CurrencyDisplayService.
    /// القيم المالية داخل قاعدة البيانات تبقى YER.
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
        /// YER -> العملة المطلوبة.
        /// </summary>
        public decimal ConvertAmount(
            decimal priceInYer,
            string targetCurrency,
            string? exchangeRateRegion)
        {
            var currency = CurrencyDisplayService.NormalizeCurrency(targetCurrency);
            if (string.IsNullOrEmpty(currency))
                throw new ArgumentException("عملة العرض غير مدعومة.", nameof(targetCurrency));
            if (currency == CurrencyDisplayService.YemeniRial)
                return BookingMoneyPolicy.Round(priceInYer);
            var rates = GetRates(exchangeRateRegion);
            return FinancialCurrencyPolicy.FromYer(priceInYer,
                currency == CurrencyDisplayService.UsDollar ? rates.UsdRate : rates.SarRate);
        }

        /// <summary>
        /// تحويل عكسي من عملة الإدخال إلى YER باستخدام سوق صنعاء كمرجع احتياطي.
        /// </summary>
        public decimal ConvertToYer(
            decimal amountInSourceCurrency,
            string sourceCurrency)
        {
            return ConvertToYer(
                amountInSourceCurrency,
                sourceCurrency,
                ExchangeRateRegionResolver.SanaaMarket);
        }

        /// <summary>
        /// تحويل عكسي من عملة الإدخال إلى YER بحسب رقم المدينة.
        /// </summary>
        public decimal ConvertToYer(
            decimal amountInSourceCurrency,
            string sourceCurrency,
            int cityId)
        {
            string? region = GetRegionByCityId(cityId);

            return ConvertToYer(
                amountInSourceCurrency,
                sourceCurrency,
                region);
        }

        /// <summary>
        /// تحويل عكسي من عملة الإدخال إلى YER بحسب سوق الصرف.
        /// يستخدم قبل الحفظ عندما أدخل المستخدم القيمة بالدولار أو السعودي.
        /// </summary>
        public decimal ConvertToYer(
            decimal amountInSourceCurrency,
            string sourceCurrency,
            string? exchangeRateRegion)
        {
            var currency = CurrencyDisplayService.NormalizeCurrency(sourceCurrency);
            if (string.IsNullOrEmpty(currency))
                throw new ArgumentException("عملة المبلغ غير مدعومة.", nameof(sourceCurrency));
            if (currency == CurrencyDisplayService.YemeniRial)
                return BookingMoneyPolicy.Round(amountInSourceCurrency);
            var rates = GetRates(exchangeRateRegion);
            return FinancialCurrencyPolicy.ToYer(amountInSourceCurrency,
                currency == CurrencyDisplayService.UsDollar ? rates.UsdRate : rates.SarRate);
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

            return $"{convertedAmount:N2} ر.ي";
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

            if (settings == null)
                throw new InvalidOperationException("إعدادات أسعار الصرف غير موجودة.");
            decimal usdRate = useAdenMarket ? settings.AdenUsdRate : settings.SanaaUsdRate;
            decimal sarRate = useAdenMarket ? settings.AdenSarRate : settings.SanaaSarRate;
            if (usdRate <= 0m || sarRate <= 0m)
                throw new InvalidOperationException("أسعار الصرف المحفوظة غير صحيحة؛ راجع إعدادات الإدارة.");

            return (usdRate, sarRate);
        }
    }
}

