using System;

namespace YemenGuide.Core.Constants
{
    /// <summary>All inputs and outputs are stored YER amounts, never display currency.</summary>
    public static class BookingMoneyPolicy
    {
        public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        public static bool IsClosed(string? state) =>
            string.Equals(state?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state?.Trim(), "Rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state?.Trim(), "Expired", StringComparison.OrdinalIgnoreCase);

        public static decimal NetPaid(decimal paid, decimal refunded) =>
            Round(Math.Max(0m, paid - Math.Max(0m, refunded)));

        public static decimal EffectiveValue(string? state, decimal original, decimal collected,
            decimal refunded, decimal pendingRefund) =>
            IsClosed(state)
                ? Round(Math.Max(0m, collected - Math.Max(0m, refunded) - Math.Max(0m, pendingRefund)))
                : Round(Math.Max(0m, original));

        public static decimal Remaining(string? state, decimal remaining) =>
            IsClosed(state) ? 0m : Round(Math.Max(0m, remaining));

        public static bool InTrial(DateTime? start, DateTime? end, DateTime at) =>
            start.HasValue && end.HasValue && start.Value <= at && at < end.Value;
    }
}
