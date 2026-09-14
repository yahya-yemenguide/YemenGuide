using System;
using System.Threading;
using System.Threading.Tasks;

namespace YemenGuide.Web.Services.ExchangeRates
{
    public sealed class ExchangeRateService
    {
        private static readonly SemaphoreSlim UpdateGate = new(1, 1);
        private readonly IExchangeRateProvider _provider;

        public ExchangeRateService(IExchangeRateProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public string SourceName => _provider.ProviderName;
        public string SourceUrl => _provider.SourceUrl;

        /// <summary>
        /// يمنع تزامن تحديث زر الإدارة مع تحديث خدمة الخلفية داخل نفس نسخة التطبيق.
        /// يجب إبقاء القفل حتى تنتهي عملية الجلب وحفظ النتيجة.
        /// </summary>
        public async Task<IDisposable> AcquireUpdateLockAsync(
            CancellationToken cancellationToken = default)
        {
            await UpdateGate.WaitAsync(cancellationToken);
            return new UpdateLockHandle(UpdateGate);
        }

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

        private sealed class UpdateLockHandle : IDisposable
        {
            private SemaphoreSlim? _gate;

            public UpdateLockHandle(SemaphoreSlim gate)
            {
                _gate = gate;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _gate, null)?.Release();
            }
        }
    }
}
