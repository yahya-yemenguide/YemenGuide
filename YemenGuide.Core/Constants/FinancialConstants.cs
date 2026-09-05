using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Constants
{
    /// <summary>
    /// المصدر المركزي والثابت لجميع القيم النصية الخاصة بالنظام المالي.
    /// يجب استخدام هذه الثوابت من الحجز والمالك والإدارة بدل تكرار السلاسل النصية.
    /// </summary>
    public static class FinancialTransactionTypes
    {
        public const string TopUp = "TopUp";
        public const string CommissionHold = "CommissionHold";
        public const string CommissionCharge = "CommissionCharge";
        public const string CommissionRelease = "CommissionRelease";
        public const string Debt = "Debt";
        public const string DebtPayment = "DebtPayment";
        public const string Adjustment = "Adjustment";

        public static readonly IReadOnlyCollection<string> All = new[]
        {
            TopUp,
            CommissionHold,
            CommissionCharge,
            CommissionRelease,
            Debt,
            DebtPayment,
            Adjustment
        };
    }


    public static class FinancialOnlinePaymentTypes
    {
        public const string Deposit = "Deposit";
        public const string Full = "Full";
    }
    public static class FinancialTransactionStatuses
    {
        public const string Pending = "Pending";
        public const string Completed = "Completed";
        public const string Rejected = "Rejected";
        public const string Reversed = "Reversed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyCollection<string> All = new[]
        {
            Pending,
            Completed,
            Rejected,
            Reversed,
            Failed,
            Cancelled
        };
    }

    public static class FinancialAccountStatuses
    {
        public const string NeedsTopUp = "NeedsTopUp";
        public const string Active = "Active";
        public const string LowBalance = "LowBalance";
        public const string InDebt = "InDebt";
        public const string Suspended = "Suspended";

        public static readonly IReadOnlyCollection<string> All = new[]
        {
            NeedsTopUp,
            Active,
            LowBalance,
            InDebt,
            Suspended
        };
    }

    public static class FinancialCommissionStatuses
    {
        public const string Calculated = "Calculated";
        public const string Held = "Held";
        public const string Charged = "Charged";
        public const string Released = "Released";
        public const string Debt = "Debt";
        public const string NotApplicable = "NotApplicable";

        public static readonly IReadOnlyCollection<string> All = new[]
        {
            Calculated,
            Held,
            Charged,
            Released,
            Debt,
            NotApplicable
        };
    }

    public static class FinancialRefundStatuses
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Rejected = "Rejected";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyCollection<string> All = new[]
        {
            Pending,
            Processing,
            Completed,
            Rejected,
            Failed,
            Cancelled
        };
    }

    /// <summary>
    /// اتجاه الحركة لغرض العرض والتقارير فقط؛ لا يغيّر الرصيد بذاته.
    /// </summary>
    public static class FinancialTransactionDirections
    {
        public const string Credit = "Credit";
        public const string Debit = "Debit";
        public const string DebtIncrease = "DebtIncrease";
        public const string DebtDecrease = "DebtDecrease";
        public const string Neutral = "Neutral";
    }

    public static class FinancialConstantsGuard
    {
        public static bool Is(string? value, string expected) =>
            string.Equals(
                value?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
    }
}
