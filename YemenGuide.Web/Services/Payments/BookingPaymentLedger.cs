using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace YemenGuide.Web.Services.Payments
{
    public sealed class BookingPaymentTransaction
    {
        public string PaymentId { get; set; } = string.Empty;
        public string PaymentType { get; set; } = "Additional";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";

        public int? OwnerWalletId { get; set; }
        public int? WalletId { get; set; }
        public string? WalletName { get; set; }
        public string? AccountNumber { get; set; }
        public string? BeneficiaryName { get; set; }

        public string? ProofPath { get; set; }
        public string? OriginalFileName { get; set; }
        public string? PaymentReference { get; set; }

        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? RejectionReason { get; set; }

        public string? ReceiptNumber { get; set; }
        public DateTime? ReceiptIssuedAt { get; set; }
        public decimal? CumulativePaidOnlineAfterApproval { get; set; }
        public decimal? RemainingAfterApproval { get; set; }
    }

    public static class BookingPaymentLedger
    {
        private static readonly JsonSerializerOptions SerializerOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

        public static bool TryRead(
            string? json,
            out List<BookingPaymentTransaction> transactions)
        {
            transactions = new List<BookingPaymentTransaction>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                var parsed =
                    JsonSerializer.Deserialize<List<BookingPaymentTransaction>>(
                        json,
                        SerializerOptions);

                if (parsed != null)
                {
                    transactions = parsed
                        .Where(item => item != null)
                        .ToList();
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static string Write(
            IReadOnlyCollection<BookingPaymentTransaction> transactions)
        {
            return JsonSerializer.Serialize(
                transactions ?? Array.Empty<BookingPaymentTransaction>(),
                SerializerOptions);
        }

        public static bool IsPending(BookingPaymentTransaction item) =>
            string.Equals(
                item.Status?.Trim(),
                "Pending",
                StringComparison.OrdinalIgnoreCase);

        public static bool IsApproved(BookingPaymentTransaction item) =>
            string.Equals(
                item.Status?.Trim(),
                "Approved",
                StringComparison.OrdinalIgnoreCase);

        public static bool IsRejected(BookingPaymentTransaction item) =>
            string.Equals(
                item.Status?.Trim(),
                "Rejected",
                StringComparison.OrdinalIgnoreCase);
    }
}
