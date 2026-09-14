using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Data;

namespace YemenGuide.Web.Services.ExchangeRates
{
    public sealed class ExchangeRateBackgroundService : BackgroundService
    {
        private const int DefaultCheckIntervalMinutes = 60;
        private const int DefaultRefreshAfterHours = 6;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExchangeRateBackgroundService> _logger;
        private readonly TimeSpan _checkInterval;
        private readonly TimeSpan _refreshAfter;

        public ExchangeRateBackgroundService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<ExchangeRateBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            int checkIntervalMinutes =
                ReadPositiveInt(
                    configuration["ExchangeRates:CheckIntervalMinutes"],
                    DefaultCheckIntervalMinutes);

            int refreshAfterHours =
                ReadPositiveInt(
                    configuration["ExchangeRates:RefreshAfterHours"],
                    DefaultRefreshAfterHours);

            _checkInterval = TimeSpan.FromMinutes(checkIntervalMinutes);
            _refreshAfter = TimeSpan.FromHours(refreshAfterHours);
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "تم تشغيل خدمة أسعار الصرف التلقائية. الفحص كل {CheckMinutes} دقيقة، والتحديث عند مرور {RefreshHours} ساعة على آخر نجاح.",
                _checkInterval.TotalMinutes,
                _refreshAfter.TotalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryRefreshIfNeededAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "حدث خطأ غير متوقع في دورة خدمة أسعار الصرف.");
                }

                try
                {
                    await Task.Delay(
                        _checkInterval,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task TryRefreshIfNeededAsync(
            CancellationToken cancellationToken)
        {
            using IServiceScope scope =
                _serviceProvider.CreateScope();

            AppDbContext context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            ExchangeRateService exchangeRateService =
                scope.ServiceProvider.GetRequiredService<ExchangeRateService>();

            var settings =
                await context.SystemSettings
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
            {
                _logger.LogWarning(
                    "لا يوجد سجل SystemSettings؛ تم تجاوز تحديث أسعار الصرف.");
                return;
            }

            if (settings.AutoUpdateExchangeRates != true)
            {
                return;
            }

            if (!NeedsRefresh(settings.ExchangeRateLastUpdatedAt))
            {
                return;
            }

            using IDisposable updateLock =
                await exchangeRateService.AcquireUpdateLockAsync(cancellationToken);

            // قد يكون الأدمن عطّل الوضع التلقائي أثناء انتظار القفل.
            context.ChangeTracker.Clear();
            settings = await context.SystemSettings
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (settings == null ||
                settings.AutoUpdateExchangeRates != true ||
                !NeedsRefresh(settings.ExchangeRateLastUpdatedAt))
            {
                return;
            }

            settings.ExchangeRateLastAttemptAt = DateTime.UtcNow;
            settings.ExchangeRateLastSource = exchangeRateService.SourceName;

            try
            {
                YemenExchangeRateResult latestRates =
                    await exchangeRateService
                        .GetLatestExchangeRatesAsync(cancellationToken);

                bool autoModeStillEnabled =
                    await context.SystemSettings
                        .AsNoTracking()
                        .Where(s => s.Id == settings.Id)
                        .Select(s => s.AutoUpdateExchangeRates == true)
                        .FirstOrDefaultAsync(cancellationToken);

                if (!autoModeStillEnabled)
                {
                    _logger.LogInformation(
                        "تم تجاهل نتيجة تحديث أسعار الصرف لأن الأدمن عطّل الوضع التلقائي أثناء الجلب.");
                    return;
                }

                settings.SanaaUsdRate = latestRates.SanaaUsdRate;
                settings.SanaaSarRate = latestRates.SanaaSarRate;
                settings.AdenUsdRate = latestRates.AdenUsdRate;
                settings.AdenSarRate = latestRates.AdenSarRate;

                settings.ExchangeRateLastUpdatedAt = DateTime.UtcNow;
                settings.ExchangeRateLastSource = latestRates.SourceName;
                settings.ExchangeRateLastStatus = "Success";
                settings.ExchangeRateLastError = null;

                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "تم تحديث أسعار الصرف تلقائيًا. صنعاء: USD={SanaaUsd}, SAR={SanaaSar}. عدن: USD={AdenUsd}, SAR={AdenSar}. المصدر={Source}.",
                    latestRates.SanaaUsdRate,
                    latestRates.SanaaSarRate,
                    latestRates.AdenUsdRate,
                    latestRates.AdenSarRate,
                    latestRates.SourceName);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                /*
                 * عند فشل المصدر لا يتم تغيير أي سعر من الأسعار الأربع.
                 * نسجل فقط بيانات المحاولة والفشل.
                 */
                settings.ExchangeRateLastStatus = "Failed";
                settings.ExchangeRateLastSource = exchangeRateService.SourceName;
                settings.ExchangeRateLastError =
                    Truncate(GetUsefulErrorMessage(ex), 500);

                bool autoModeStillEnabled =
                    await context.SystemSettings
                        .AsNoTracking()
                        .Where(s => s.Id == settings.Id)
                        .Select(s => s.AutoUpdateExchangeRates == true)
                        .FirstOrDefaultAsync(cancellationToken);

                if (!autoModeStillEnabled)
                {
                    return;
                }

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(
                        saveEx,
                        "فشل حفظ حالة تعثر تحديث أسعار الصرف في SystemSettings.");
                }

                _logger.LogWarning(
                    ex,
                    "تعذر تحديث أسعار الصرف؛ تم الاحتفاظ بالأسعار الحالية دون تغيير.");
            }
        }

        private bool NeedsRefresh(DateTime? lastUpdatedAt)
        {
            if (!lastUpdatedAt.HasValue)
            {
                return true;
            }

            DateTime lastUtc =
                lastUpdatedAt.Value.Kind switch
                {
                    DateTimeKind.Utc => lastUpdatedAt.Value,
                    DateTimeKind.Local => lastUpdatedAt.Value.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(
                        lastUpdatedAt.Value,
                        DateTimeKind.Utc)
                };

            return DateTime.UtcNow - lastUtc >= _refreshAfter;
        }

        private static string GetUsefulErrorMessage(Exception ex)
        {
            Exception current = ex;

            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return string.IsNullOrWhiteSpace(current.Message)
                ? "فشل غير معروف أثناء تحديث أسعار الصرف."
                : current.Message.Trim();
        }

        private static string Truncate(
            string value,
            int maxLength)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static int ReadPositiveInt(
            string? raw,
            int fallback)
        {
            return int.TryParse(
                       raw,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out int parsed) &&
                   parsed > 0
                ? parsed
                : fallback;
        }
    }
}
