using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class CommissionTransaction
    {
        public int CommissionTransactionId { get; set; }
        public int PlaceId { get; set; }
        public int? BookingId { get; set; }
        public int? PlatformWalletId { get; set; }
        public string TransactionType { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal AvailableBalanceBefore { get; set; }
        public decimal AvailableBalanceAfter { get; set; }
        public decimal HeldBalanceBefore { get; set; }
        public decimal HeldBalanceAfter { get; set; }
        public decimal DebtBefore { get; set; }
        public decimal DebtAfter { get; set; }
        public string Status { get; set; } = null!;
        public string? ReferenceNumber { get; set; }
        public DateTime? TransferDate { get; set; }
        public string? ReceiptImage { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? Notes { get; set; }

        public virtual Booking? Booking { get; set; }
        public virtual User? CreatedByUser { get; set; }
        public virtual Place Place { get; set; } = null!;
        public virtual PlatformWallet? PlatformWallet { get; set; }
        public virtual User? ReviewedByUser { get; set; }
    }
}
