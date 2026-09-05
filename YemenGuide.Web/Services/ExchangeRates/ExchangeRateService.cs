using System;
using System.Threading;
using System.Threading.Tasks;

namespace YemenGuide.Web.Services.ExchangeRates
{
    public sealed class ExchangeRateService
    {
        private readonly IExchangeRateProvider _provider;

        public ExchangeRateService(IExchangeRateProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public string SourceName => _provider.ProviderName;
        public string SourceUrl => _provider.SourceUrl;

        public async Task<YemenExchangeRateResult> GetLatestExchangeRatesAsync(
            CancellationToken cancellationToken = default)
        {
            YemenExchangeRateResult result =
                await _provider.GetLatestRatesAsync(cancellationToken);

            if (!result.IsValid(out string validationError))
            {
                throw new InvalidOperationException(
                    "أسعار الصرف المستلمة غير صالحة: " + validationError);
            }

            return result;
        }

        public Task<YemenExchangeRateResult> GetExchangeRatesAsync(
            CancellationToken cancellationToken = default)
        {
            return GetLatestExchangeRatesAsync(cancellationToken);
        }
    }
}
