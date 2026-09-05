using System;

namespace YemenGuide.Web.Services.ExchangeRates
{
    public sealed class YemenExchangeRateResult
    {
        public decimal SanaaUsdRate { get; set; }
        public decimal SanaaSarRate { get; set; }
        public decimal AdenUsdRate { get; set; }
        public decimal AdenSarRate { get; set; }

        public string SourceName { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public DateTime FetchedAtUtc { get; set; }
        public string RateType { get; set; } = "Sell";

        public bool IsValid(out string error)
        {
            if (SanaaUsdRate <= 0 ||
                SanaaSarRate <= 0 ||
                AdenUsdRate <= 0 ||
                AdenSarRate <= 0)
            {
                error = "أحد أسعار الصرف المستلمة يساوي صفراً أو أقل.";
                return false;
            }

            if (SanaaUsdRate > 100000m ||
                SanaaSarRate > 100000m ||
                AdenUsdRate > 100000m ||
                AdenSarRate > 100000m)
            {
                error = "أحد أسعار الصرف المستلمة خارج النطاق الآمن.";
                return false;
            }

            decimal sanaaRatio = SanaaUsdRate / SanaaSarRate;
            decimal adenRatio = AdenUsdRate / AdenSarRate;

            if (sanaaRatio < 3m || sanaaRatio > 5m)
            {
                error = "نسبة الدولار إلى السعودي في بيانات صنعاء غير منطقية.";
                return false;
            }

            if (adenRatio < 3m || adenRatio > 5m)
            {
                error = "نسبة الدولار إلى السعودي في بيانات عدن غير منطقية.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
