using System.Threading;
using System.Threading.Tasks;

namespace YemenGuide.Web.Services.ExchangeRates
{
    public interface IExchangeRateProvider
    {
        string ProviderName { get; }
        string SourceUrl { get; }

        Task<YemenExchangeRateResult> GetLatestRatesAsync(
            CancellationToken cancellationToken = default);
    }
}
