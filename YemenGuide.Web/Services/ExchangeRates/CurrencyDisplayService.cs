using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Services.ExchangeRates
{
    /// <summary>
    /// الطبقة المركزية لعملة العرض في الموقع.
    ///
    /// الأولوية:
    /// 1) Users.PreferredCurrency للمستخدم المسجل.
    /// 2) SystemSettings.DefaultCurrency للزائر أو عند عدم وجود تفضيل.
    /// 3) الريال اليمني كقيمة احتياطية نهائية.
    ///
    /// السوق لا يختاره المستخدم:
    /// - المنشأة في North تستخدم أسعار صنعاء.
    /// - المنشأة في South تستخدم أسعار عدن.
    /// </summary>
    public sealed class CurrencyDisplayService
    {
        public const string YemeniRial = "ريال يمني";
        public const string SaudiRiyal = "ريال سعودي";
        public const string UsDollar = "دولار أمريكي";

        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CurrencyService _currencyService;
        private readonly ExchangeRateRegionResolver _regionResolver;

        private string? _cachedPreferredCurrency;

        public CurrencyDisplayService(
            AppDbContext context,
            IHttpContextAccessor httpContextAccessor,
            CurrencyService currencyService,
            ExchangeRateRegionResolver regionResolver)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _currencyService = currencyService;
            _regionResolver = regionResolver;
        }

        /// <summary>
        /// توحيد القيم الجديدة والقديمة إلى ثلاث قيم فقط.
        /// القيم القديمة (قديم/جديد) تتحول إلى "ريال يمني".
        /// يعيد string.Empty عند قيمة غير مدعومة.
        /// </summary>
        public static string NormalizeCurrency(string? currency)
        {
            string value =
                (currency ?? string.Empty)
                    .Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.Equals(YemeniRial, StringComparison.OrdinalIgnoreCase) ||
                value.Equals("ريال يمني (قديم)", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("ريال يمني (جديد)", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("YER", StringComparison.OrdinalIgnoreCase))
            {
                return YemeniRial;
            }

            if (value.Equals(SaudiRiyal, StringComparison.OrdinalIgnoreCase) ||
                value.Equals("SAR", StringComparison.OrdinalIgnoreCase))
            {
                return SaudiRiyal;
            }

            if (value.Equals(UsDollar, StringComparison.OrdinalIgnoreCase) ||
                value.Equals("دولار امريكي", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                return UsDollar;
            }

            return string.Empty;
        }

        public static bool IsSupportedCurrency(string? currency)
        {
            return !string.IsNullOrWhiteSpace(
                NormalizeCurrency(currency));
        }

        public static string GetCurrencyCode(string? currency)
        {
            return NormalizeCurrency(currency) switch
            {
                SaudiRiyal => "SAR",
                UsDollar => "USD",
                _ => "YER"
            };
        }

        public static string GetCurrencySymbol(string? currency)
        {
            return NormalizeCurrency(currency) switch
            {
                SaudiRiyal => "ر.س",
                UsDollar => "$",
                _ => "ر.ي"
            };
        }

        /// <summary>
        /// يحدد عملة العرض الفعلية للمستخدم الحالي.
        /// </summary>
        public async Task<string> GetPreferredCurrencyAsync(
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(
                    _cachedPreferredCurrency))
            {
                return _cachedPreferredCurrency;
            }

            var httpContext =
                _httpContextAccessor.HttpContext;

            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                string? userIdText =
                    httpContext.User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                if (int.TryParse(
                        userIdText,
                        out int userId) &&
                    userId > 0)
                {
                    string? userCurrency =
                        await _context.Users
                            .AsNoTracking()
                            .Where(user =>
                                user.UserId == userId)
                            .Select(user =>
                                user.PreferredCurrency)
                            .FirstOrDefaultAsync(
                                cancellationToken);

                    string normalizedUserCurrency =
                        NormalizeCurrency(
                            userCurrency);

                    if (!string.IsNullOrWhiteSpace(
                            normalizedUserCurrency))
                    {
                        _cachedPreferredCurrency =
                            normalizedUserCurrency;

                        return _cachedPreferredCurrency;
                    }
                }
            }

            string? defaultCurrency =
                await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(setting => setting.Id)
                    .Select(setting => setting.DefaultCurrency)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            string normalizedDefaultCurrency =
                NormalizeCurrency(
                    defaultCurrency);

            _cachedPreferredCurrency =
                string.IsNullOrWhiteSpace(
                    normalizedDefaultCurrency)
                    ? YemeniRial
                    : normalizedDefaultCurrency;

            return _cachedPreferredCurrency;
        }

        /// <summary>
        /// تنسيق مبلغ مرتبط بمنشأة حسب CityId الخاص بها.
        /// </summary>
        public async Task<string> FormatAsync(
            decimal amountInYer,
            int cityId,
            CancellationToken cancellationToken = default)
        {
            string currency =
                await GetPreferredCurrencyAsync(
                    cancellationToken);

            return _currencyService.ConvertPrice(
                amountInYer,
                currency,
                cityId);
        }

        /// <summary>
        /// تنسيق مبلغ عندما يكون ExchangeRateRegion معروفًا مسبقًا.
        /// </summary>
        public async Task<string> FormatByRegionAsync(
            decimal amountInYer,
            string? exchangeRateRegion,
            CancellationToken cancellationToken = default)
        {
            string currency =
                await GetPreferredCurrencyAsync(
                    cancellationToken);

            return _currencyService.ConvertPrice(
                amountInYer,
                currency,
                exchangeRateRegion);
        }

        /// <summary>
        /// تنسيق مبلغ بحسب اسم مدينة. يستخدم كبنية احتياطية للبروفايل
        /// والمواضع التي لا تملك CityId مباشرًا.
        /// </summary>
        public async Task<string> FormatByCityNameAsync(
            decimal amountInYer,
            string? cityName,
            CancellationToken cancellationToken = default)
        {
            string region =
                _regionResolver.Resolve(
                    cityName);

            return await FormatByRegionAsync(
                amountInYer,
                region,
                cancellationToken);
        }

        /// <summary>
        /// القيمة الرقمية بعد التحويل حسب CityId.
        /// استخدمها عند الحاجة إلى جمع مبالغ محولة ثم اعرض الناتج لاحقًا.
        /// </summary>
        public async Task<decimal> ConvertAmountAsync(
            decimal amountInYer,
            int cityId,
            CancellationToken cancellationToken = default)
        {
            string currency =
                await GetPreferredCurrencyAsync(
                    cancellationToken);

            return _currencyService.ConvertAmount(
                amountInYer,
                currency,
                cityId);
        }

        /// <summary>
        /// القيمة الرقمية بعد التحويل حسب السوق.
        /// </summary>
        public async Task<decimal> ConvertAmountByRegionAsync(
            decimal amountInYer,
            string? exchangeRateRegion,
            CancellationToken cancellationToken = default)
        {
            string currency =
                await GetPreferredCurrencyAsync(
                    cancellationToken);

            return _currencyService.ConvertAmount(
                amountInYer,
                currency,
                exchangeRateRegion);
        }

        /// <summary>
        /// القيمة الرقمية بعد التحويل حسب اسم المدينة.
        /// </summary>
        public async Task<decimal> ConvertAmountByCityNameAsync(
            decimal amountInYer,
            string? cityName,
            CancellationToken cancellationToken = default)
        {
            string region =
                _regionResolver.Resolve(
                    cityName);

            return await ConvertAmountByRegionAsync(
                amountInYer,
                region,
                cancellationToken);
        }

        /// <summary>
        /// تنسيق رقم محول بدون تكرار رمز العملة.
        /// </summary>
        public static string FormatNumber(
            decimal convertedAmount,
            string? currency)
        {
            return convertedAmount.ToString("N2");
        }
    }
}

