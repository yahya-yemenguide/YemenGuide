using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class RefundRequest
    {
        public int RefundRequestId { get; set; }
        public int BookingId { get; set; }
        public int? CustomerId { get; set; }
        public int PlaceId { get; set; }
        public decimal DepositPaid { get; set; }
        public decimal CancellationFee { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal OwnerShare { get; set; }
        public decimal PlatformCommission { get; set; }
        public decimal CommissionReleased { get; set; }
        public string Status { get; set; } = null!;
        public string? Reason { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? ProofImage { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedByUserId { get; set; }
        public string? Notes { get; set; }
        public int? RefundWalletId { get; set; }
        public string? RefundAccountNumber { get; set; }
        public string? RefundBeneficiaryName { get; set; }
        public DateTime? TransferredAt { get; set; }
        public DateTime? CustomerConfirmedAt { get; set; }

        public virtual Booking Booking { get; set; } = null!;
        public virtual User? Customer { get; set; }
        public virtual Place Place { get; set; } = null!;
        public virtual User? ProcessedByUser { get; set; }
        public virtual SupportedWallet? RefundWallet { get; set; }
    }
}
