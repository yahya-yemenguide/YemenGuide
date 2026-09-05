using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace YemenGuide.Web.Services.ExchangeRates
{
    public sealed class YemenSulExchangeRateProvider : IExchangeRateProvider
    {
        private const string DefaultSourceUrl = "https://t.me/s/yemensul";
        private const int DefaultTimeoutSeconds = 15;

        private static readonly Regex BreakRegex =
            new Regex(
                @"<br\s*/?>",
                RegexOptions.IgnoreCase |
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

        private static readonly Regex ScriptRegex =
            new Regex(
                @"<script\b[^>]*>.*?</script>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline |
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

        private static readonly Regex StyleRegex =
            new Regex(
                @"<style\b[^>]*>.*?</style>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline |
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

        private static readonly Regex TagRegex =
            new Regex(
                @"<[^>]+>",
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

        private static readonly Regex MultiWhitespaceRegex =
            new Regex(
                @"[ \t\f\v]+",
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

        private static readonly Regex DollarSellRegex =
            new Regex(
                @"بيع\s+الدولار[^\d٠-٩۰-۹]*([0-9٠-٩۰-۹][0-9٠-٩۰-۹\.,٫٬]*)",
                RegexOptions.IgnoreCase |
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

        private static readonly Regex SaudiSellRegex =
            new Regex(
                @"بيع\s+(?:الريال\s+)?السعودي[^\d٠-٩۰-۹]*([0-9٠-٩۰-۹][0-9٠-٩۰-۹\.,٫٬]*)",
                RegexOptions.IgnoreCase |
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

        private readonly HttpClient _httpClient;
        private readonly ILogger<YemenSulExchangeRateProvider> _logger;
        private readonly string _sourceUrl;
        private readonly int _timeoutSeconds;

        public YemenSulExchangeRateProvider(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<YemenSulExchangeRateProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _sourceUrl =
                configuration["ExchangeRates:SourceUrl"]?.Trim()
                ?? DefaultSourceUrl;

            _timeoutSeconds = ReadPositiveInt(
                configuration["ExchangeRates:RequestTimeoutSeconds"],
                DefaultTimeoutSeconds);

            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (compatible; YemenGuide/1.0; ExchangeRateFetcher)");
            }

            if (!_httpClient.DefaultRequestHeaders.AcceptLanguage.Any())
            {
                _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ar,en;q=0.8");
            }
        }

        public string ProviderName => "YemenSul";

        public string SourceUrl => _sourceUrl;

        public async Task<YemenExchangeRateResult> GetLatestRatesAsync(
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            string html;

            try
            {
                using var request =
                    new HttpRequestMessage(HttpMethod.Get, _sourceUrl);

                using HttpResponseMessage response =
                    await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"مصدر أسعار الصرف أعاد HTTP {(int)response.StatusCode}.");
                }

                html =
                    await response.Content.ReadAsStringAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"انتهت مهلة الاتصال بمصدر أسعار الصرف بعد {_timeoutSeconds} ثانية.");
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    "تعذر الاتصال بمصدر أسعار الصرف.",
                    ex);
            }

            if (string.IsNullOrWhiteSpace(html))
            {
                throw new InvalidOperationException(
                    "مصدر أسعار الصرف أعاد صفحة فارغة.");
            }

            YemenExchangeRateResult? result =
                ParseLatestRatesFromTelegramHtml(html);

            if (result == null)
            {
                throw new InvalidOperationException(
                    "تم الوصول إلى YemenSul لكن تعذر العثور على منشور صالح يحتوي أسعار صنعاء وعدن للدولار والسعودي.");
            }

            result.SourceName = ProviderName;
            result.SourceUrl = SourceUrl;
            result.FetchedAtUtc = DateTime.UtcNow;
            result.RateType = "Sell";

            if (!result.IsValid(out string validationError))
            {
                throw new InvalidOperationException(
                    "تمت قراءة بيانات من المصدر لكنها لم تجتز فحص السلامة: " +
                    validationError);
            }

            _logger.LogInformation(
                "تمت قراءة أسعار YemenSul بنجاح: صنعاء USD={SanaaUsd}, SAR={SanaaSar}; عدن USD={AdenUsd}, SAR={AdenSar}.",
                result.SanaaUsdRate,
                result.SanaaSarRate,
                result.AdenUsdRate,
                result.AdenSarRate);

            return result;
        }

        private static YemenExchangeRateResult? ParseLatestRatesFromTelegramHtml(
            string html)
        {
            string[] blocks =
                html.Split(
                    new[] { "tgme_widget_message_wrap" },
                    StringSplitOptions.RemoveEmptyEntries);

            for (int i = blocks.Length - 1; i >= 0; i--)
            {
                string plainText = HtmlToPlainText(blocks[i]);

                if (!LooksLikeCompleteExchangePost(plainText))
                {
                    continue;
                }

                YemenExchangeRateResult? parsed =
                    TryParseExchangePost(plainText);

                if (parsed != null)
                {
                    return parsed;
                }
            }

            string wholePageText = HtmlToPlainText(html);

            if (LooksLikeCompleteExchangePost(wholePageText))
            {
                foreach (string candidate in SplitWholePageIntoRateCandidates(wholePageText))
                {
                    YemenExchangeRateResult? parsed =
                        TryParseExchangePost(candidate);

                    if (parsed != null)
                    {
                        return parsed;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> SplitWholePageIntoRateCandidates(
            string text)
        {
            string normalized = NormalizeArabicText(text);
            const string marker = "اسعار الصرف العملات الاجنبيه";

            int searchFrom = normalized.Length;

            while (searchFrom > 0)
            {
                int start = normalized.LastIndexOf(
                    marker,
                    searchFrom - 1,
                    StringComparison.Ordinal);

                if (start < 0)
                {
                    yield break;
                }

                int nextStart = normalized.IndexOf(
                    marker,
                    start + marker.Length,
                    StringComparison.Ordinal);

                int length =
                    nextStart > start
                        ? nextStart - start
                        : normalized.Length - start;

                yield return normalized.Substring(start, length);
                searchFrom = start;
            }
        }

        private static bool LooksLikeCompleteExchangePost(string text)
        {
            string normalized = NormalizeArabicText(text);

            return normalized.Contains("صنعاء", StringComparison.Ordinal) &&
                   normalized.Contains("عدن", StringComparison.Ordinal) &&
                   normalized.Contains("بيع الدولار", StringComparison.Ordinal) &&
                   normalized.Contains("بيع السعودي", StringComparison.Ordinal);
        }

        private static YemenExchangeRateResult? TryParseExchangePost(
            string text)
        {
            string normalized = NormalizeArabicText(text);

            int sanaaIndex =
                normalized.IndexOf("صنعاء", StringComparison.Ordinal);

            int adenIndex =
                normalized.IndexOf("عدن", StringComparison.Ordinal);

            if (sanaaIndex < 0 || adenIndex < 0 || sanaaIndex == adenIndex)
            {
                return null;
            }

            string sanaaSection;
            string adenSection;

            if (sanaaIndex < adenIndex)
            {
                sanaaSection =
                    normalized.Substring(
                        sanaaIndex,
                        adenIndex - sanaaIndex);

                adenSection =
                    normalized.Substring(adenIndex);
            }
            else
            {
                adenSection =
                    normalized.Substring(
                        adenIndex,
                        sanaaIndex - adenIndex);

                sanaaSection =
                    normalized.Substring(sanaaIndex);
            }

            if (!TryExtractSellRate(
                    sanaaSection,
                    DollarSellRegex,
                    out decimal sanaaUsd) ||
                !TryExtractSellRate(
                    sanaaSection,
                    SaudiSellRegex,
                    out decimal sanaaSar) ||
                !TryExtractSellRate(
                    adenSection,
                    DollarSellRegex,
                    out decimal adenUsd) ||
                !TryExtractSellRate(
                    adenSection,
                    SaudiSellRegex,
                    out decimal adenSar))
            {
                return null;
            }

            var result =
                new YemenExchangeRateResult
                {
                    SanaaUsdRate = sanaaUsd,
                    SanaaSarRate = sanaaSar,
                    AdenUsdRate = adenUsd,
                    AdenSarRate = adenSar,
                    RateType = "Sell"
                };

            return result.IsValid(out _)
                ? result
                : null;
        }

        private static bool TryExtractSellRate(
            string section,
            Regex regex,
            out decimal value)
        {
            value = 0m;

            Match match = regex.Match(section);

            if (!match.Success || match.Groups.Count < 2)
            {
                return false;
            }

            return TryParseLocalizedDecimal(
                match.Groups[1].Value,
                out value);
        }

        private static bool TryParseLocalizedDecimal(
            string raw,
            out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string number = ConvertArabicDigits(raw)
                .Replace("٬", string.Empty, StringComparison.Ordinal)
                .Replace("٫", ".", StringComparison.Ordinal)
                .Trim();

            if (number.Contains(",", StringComparison.Ordinal))
            {
                if (number.Contains(".", StringComparison.Ordinal))
                {
                    number =
                        number.Replace(",", string.Empty, StringComparison.Ordinal);
                }
                else
                {
                    int lastComma = number.LastIndexOf(',');
                    int digitsAfter = number.Length - lastComma - 1;
                    int commaCount = number.Count(ch => ch == ',');

                    if (commaCount > 1 || digitsAfter == 3)
                    {
                        number =
                            number.Replace(",", string.Empty, StringComparison.Ordinal);
                    }
                    else
                    {
                        number =
                            number.Replace(",", ".", StringComparison.Ordinal);
                    }
                }
            }

            return decimal.TryParse(
                number,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string ConvertArabicDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);

            foreach (char ch in value)
            {
                builder.Append(
                    ch switch
                    {
                        '٠' or '۰' => '0',
                        '١' or '۱' => '1',
                        '٢' or '۲' => '2',
                        '٣' or '۳' => '3',
                        '٤' or '۴' => '4',
                        '٥' or '۵' => '5',
                        '٦' or '۶' => '6',
                        '٧' or '۷' => '7',
                        '٨' or '۸' => '8',
                        '٩' or '۹' => '9',
                        _ => ch
                    });
            }

            return builder.ToString();
        }

        private static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            string text = ScriptRegex.Replace(html, " ");
            text = StyleRegex.Replace(text, " ");
            text = BreakRegex.Replace(text, "\n");
            text = TagRegex.Replace(text, " ");
            text = WebUtility.HtmlDecode(text);

            text = text
                .Replace('\u00A0', ' ')
                .Replace('\u200F', ' ')
                .Replace('\u200E', ' ');

            text = MultiWhitespaceRegex.Replace(text, " ");

            return text.Trim();
        }

        private static string NormalizeArabicText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string text = ConvertArabicDigits(value);

            text = text
                .Replace('أ', 'ا')
                .Replace('إ', 'ا')
                .Replace('آ', 'ا')
                .Replace('ٱ', 'ا')
                .Replace('ة', 'ه')
                .Replace('ى', 'ي')
                .Replace("ـ", string.Empty, StringComparison.Ordinal);

            text = Regex.Replace(
                text,
                @"[\u064B-\u065F\u0670]",
                string.Empty,
                RegexOptions.CultureInvariant);

            text = Regex.Replace(
                text,
                @"[ \t\f\v]+",
                " ",
                RegexOptions.CultureInvariant);

            return text.Trim();
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
