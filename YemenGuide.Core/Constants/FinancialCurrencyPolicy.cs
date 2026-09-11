using System;

namespace YemenGuide.Core.Constants
{
    public static class FinancialCurrencyPolicy
    {
        // rate is YER per one unit of the foreign currency, in the property's market.
        public static decimal FromYer(decimal amount, decimal rate) =>
            BookingMoneyPolicy.Round(amount / ValidRate(rate));
        public static decimal ToYer(decimal amount, decimal rate) =>
            BookingMoneyPolicy.Round(amount * ValidRate(rate));
        private static decimal ValidRate(decimal rate) => rate > 0m
            ? rate : throw new ArgumentOutOfRangeException(nameof(rate), "سعر الصرف يجب أن يكون أكبر من صفر.");
    }
}
