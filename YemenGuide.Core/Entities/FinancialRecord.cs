using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class FinancialRecord
    {
        public int RecordId { get; set; }
        public int BookingId { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PlatformCommission { get; set; }
        public string? OnlinePaymentType { get; set; }
        public string? TransferReferenceNumber { get; set; }
        public decimal AmountPaidOnline { get; set; }
        public decimal AmountToPayAtProperty { get; set; }
        public decimal CommissionRateSnapshot { get; set; }
        public decimal CommissionBaseAmount { get; set; }
        public string CommissionStatus { get; set; } = null!;
        public DateTime? CommissionHeldAt { get; set; }
        public DateTime? CommissionChargedAt { get; set; }
        public DateTime? CommissionReleasedAt { get; set; }
        public decimal AmountCollectedAtProperty { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public virtual Booking Booking { get; set; } = null!;
    }
}
