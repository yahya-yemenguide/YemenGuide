using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Advertisement
    {
        public Advertisement()
        {
            AdvertisementEvents = new HashSet<AdvertisementEvent>();
            AdvertisementTargets = new HashSet<AdvertisementTarget>();
            Bookings = new HashSet<Booking>();
            Reports = new HashSet<Report>();
        }

        public int AdvertisementId { get; set; }
        public int OwnerId { get; set; }
        public int PlaceId { get; set; }
        public int AdvertisementPlanId { get; set; }
        public int? PromotionId { get; set; }
        public string AdvertisementTitle { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? ImagePath { get; set; }
        public string ButtonText { get; set; } = null!;
        public string PlacementTypeSnapshot { get; set; } = null!;
        public string DurationTypeSnapshot { get; set; } = null!;
        public int DurationDaysSnapshot { get; set; }
        public decimal PriceSnapshot { get; set; }
        public string Currency { get; set; } = null!;
        public bool? StartImmediately { get; set; }
        public DateTime? RequestedStartAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int? PlatformWalletId { get; set; }
        public decimal? TransferredAmount { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime? TransferDate { get; set; }
        public string? ReceiptImage { get; set; }

        // بيانات استرداد الإعلان - مستقلة عن بيانات الدفع الأصلية.
        public DateTime? RefundRequestedAt { get; set; }
        public decimal? RefundAmount { get; set; }
        public int? RefundWalletId { get; set; }
        public string? RefundAccountNumber { get; set; }
        public string? RefundBeneficiaryName { get; set; }
        public string? RefundReferenceNumber { get; set; }
        public string? RefundProofImage { get; set; }
        public DateTime? RefundedAt { get; set; }
        public int? RefundedByUserId { get; set; }
        public string? RefundNote { get; set; }

        public string PaymentStatus { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? PausedAt { get; set; }
        public long ImpressionsCount { get; set; }
        public long ClicksCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;
        public string BillingModelSnapshot { get; set; } = null!;
        public decimal? CostPerClickSnapshot { get; set; }
        public decimal? CostPerThousandSnapshot { get; set; }
        public decimal? TotalBudget { get; set; }
        public decimal? DailyBudget { get; set; }
        public decimal SpentAmount { get; set; }
        public long? MaxImpressions { get; set; }
        public long? MaxClicks { get; set; }
        public int FrequencyCapPerUserPerDay { get; set; }
        public string TargetingMode { get; set; } = null!;
        public string DeviceTarget { get; set; } = null!;
        public string DestinationType { get; set; } = null!;
        public string? DestinationPath { get; set; }
        public int RotationWeight { get; set; }
        public int PrioritySnapshot { get; set; }
        public long UniqueImpressionsCount { get; set; }
        public long ConversionsCount { get; set; }
        public DateTime? LastImpressionAt { get; set; }
        public DateTime? LastClickAt { get; set; }
        public DateTime? LastServedAt { get; set; }
        public decimal? TargetLatitude { get; set; }
        public decimal? TargetLongitude { get; set; }
        public decimal? TargetRadiusKm { get; set; }
        public bool AutoRenew { get; set; }
        public int RenewalCount { get; set; }
        public string? PauseReason { get; set; }
        public string? CancellationReason { get; set; }
        public string? ArchivedDailyStatisticsJson { get; set; }
        public long BookingStartsCount { get; set; }

        public virtual AdvertisementPlan AdvertisementPlan { get; set; } = null!;
        public virtual User Owner { get; set; } = null!;
        public virtual Place Place { get; set; } = null!;
        public virtual PlatformWallet? PlatformWallet { get; set; }
        public virtual Promotion? Promotion { get; set; }
        public virtual User? ReviewedByUser { get; set; }
        public virtual AdvertisementMedium? AdvertisementMedium { get; set; }
        public virtual ICollection<AdvertisementEvent> AdvertisementEvents { get; set; }
        public virtual ICollection<AdvertisementTarget> AdvertisementTargets { get; set; }
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<Report> Reports { get; set; }
    }
}
