using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;
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

        // الخدمة Scoped، لذلك يكفي تحميل أسعار النظام مرة واحدة داخل الطلب.
        // هذا يمنع استعلام SystemSettings من التكرر عشرات المرات عند تحويل
        // جدول مالي كامل للمالك.
        private bool _ratesLoaded;
        private decimal _sanaaUsdRate;
        private decimal _sanaaSarRate;
        private decimal _adenUsdRate;
        private decimal _adenSarRate;

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
        ///
        /// ملاحظة مالية مهمة: عند بقاء العملة YER نحافظ على خانتين عشريتين
        /// في القيمة الرقمية ولا نقربها إلى عدد صحيح. التقريب إلى N0 إن أريد
        /// يكون في التنسيق البصري فقط، حتى لا نفقد كسورًا محاسبية داخل DTOs.
        /// </summary>
        public decimal ConvertAmount(
            decimal priceInYer,
            string targetCurrency,
            string? exchangeRateRegion)
        {
            string currency =
                CurrencyDisplayService.NormalizeCurrency(
                    targetCurrency);

            if (string.IsNullOrWhiteSpace(currency) ||
                currency == CurrencyDisplayService.YemeniRial)
            {
                return RoundMoney(priceInYer);
            }

            var rates = GetRates(exchangeRateRegion);

            if (currency == CurrencyDisplayService.UsDollar)
            {
                return RoundMoney(
                    priceInYer / rates.UsdRate);
            }

            if (currency == CurrencyDisplayService.SaudiRiyal)
            {
                return RoundMoney(
                    priceInYer / rates.SarRate);
            }

            return RoundMoney(priceInYer);
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
            string currency =
                CurrencyDisplayService.NormalizeCurrency(
                    sourceCurrency);

            if (string.IsNullOrWhiteSpace(currency) ||
                currency == CurrencyDisplayService.YemeniRial)
            {
                return RoundMoney(amountInSourceCurrency);
            }

            var rates = GetRates(exchangeRateRegion);

            if (currency == CurrencyDisplayService.UsDollar)
            {
                return RoundMoney(
                    amountInSourceCurrency * rates.UsdRate);
            }

            if (currency == CurrencyDisplayService.SaudiRiyal)
            {
                return RoundMoney(
                    amountInSourceCurrency * rates.SarRate);
            }

            return RoundMoney(amountInSourceCurrency);
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

            return $"{Math.Round(convertedAmount, 0, MidpointRounding.AwayFromZero):N0} ر.ي";
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
            EnsureRatesLoaded();

            bool useAdenMarket =
                string.Equals(
                    exchangeRateRegion?.Trim(),
                    ExchangeRateRegionResolver.AdenMarket,
                    StringComparison.OrdinalIgnoreCase);

            return useAdenMarket
                ? (_adenUsdRate, _adenSarRate)
                : (_sanaaUsdRate, _sanaaSarRate);
        }

        private void EnsureRatesLoaded()
        {
            if (_ratesLoaded)
            {
                return;
            }

            var settings =
                _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(setting => setting.Id)
                    .Select(setting => new
                    {
                        setting.SanaaUsdRate,
                        setting.SanaaSarRate,
                        setting.AdenUsdRate,
                        setting.AdenSarRate
                    })
                    .FirstOrDefault();

            _sanaaUsdRate = PositiveOrDefault(
                settings?.SanaaUsdRate,
                DefaultSanaaUsdRate);
            _sanaaSarRate = PositiveOrDefault(
                settings?.SanaaSarRate,
                DefaultSanaaSarRate);
            _adenUsdRate = PositiveOrDefault(
                settings?.AdenUsdRate,
                DefaultAdenUsdRate);
            _adenSarRate = PositiveOrDefault(
                settings?.AdenSarRate,
                DefaultAdenSarRate);

            _ratesLoaded = true;
        }

        private static decimal PositiveOrDefault(
            decimal? value,
            decimal fallback)
        {
            return value.HasValue && value.Value > 0m
                ? value.Value
                : fallback;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(
                value,
                2,
                MidpointRounding.AwayFromZero);
        }
    }
}
