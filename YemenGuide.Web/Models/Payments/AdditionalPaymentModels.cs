using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using YemenGuide.Web.Services.Payments;

namespace YemenGuide.Web.Models.Payments
{
    public sealed class AdditionalPaymentWalletViewModel
    {
        public int OwnerWalletId { get; set; }
        public int WalletId { get; set; }
        public string WalletName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public string? LogoPath { get; set; }
    }

    public sealed class AdditionalPaymentPageViewModel
    {
        public int BookingId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public decimal AmountPaidOnline { get; set; }
        public decimal RemainingAmount { get; set; }
        public DateTime OperationalStartAt { get; set; }
        public IReadOnlyList<AdditionalPaymentWalletViewModel> Wallets { get; set; }
            = Array.Empty<AdditionalPaymentWalletViewModel>();
    }

    public sealed class AdditionalPaymentSubmitModel
    {
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
        public int OwnerWalletId { get; set; }
        public string? PaymentReference { get; set; }
        public IFormFile? ProofFile { get; set; }
    }

    public sealed class BookingPaymentReceiptViewModel
    {
        public int BookingId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public string PaymentTypeLabel { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public string? WalletName { get; set; }
        public string? PaymentReference { get; set; }
        public decimal CumulativePaidOnline { get; set; }
        public decimal RemainingAmount { get; set; }
        public string PaymentId { get; set; } = string.Empty;
    }

    public sealed class BookingPaymentsPageViewModel
    {
        public int BookingId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public decimal AmountPaidOnline { get; set; }
        public decimal RemainingAmount { get; set; }
        public BookingPaymentReceiptViewModel? DepositReceipt { get; set; }
        public IReadOnlyList<BookingPaymentTransaction> AdditionalPayments { get; set; }
            = Array.Empty<BookingPaymentTransaction>();
    }
}
