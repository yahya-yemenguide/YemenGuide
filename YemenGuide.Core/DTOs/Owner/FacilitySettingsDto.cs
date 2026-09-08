using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Owner
{
    public class FacilitySettingsDto
    {
        [Range(1, int.MaxValue)]
        public int PlaceId { get; set; }

        [Required]
        [MaxLength(20)]
        public string BookingApprovalMode { get; set; } = "Manual";

        public string? DepositPoliciesJson { get; set; }
        public string? SecurityPoliciesJson { get; set; }

        [Range(1, 72)]
        public int PaymentTimeout { get; set; } = 12;

        public bool? IsSarSupported { get; set; } = true;
        public bool? IsUsdSupported { get; set; } = true;

        [Range(typeof(decimal), "0", "999999999")]
        public decimal CleaningFee { get; set; }

        [Range(typeof(decimal), "0", "100")]
        public decimal TaxPercentage { get; set; }

        public bool IsNonRefundableEnabled { get; set; }

        [Range(0, 100)]
        public int NonRefundableDiscount { get; set; } = 10;

        [Range(0, 8760)]
        public int FreeCancellationHours { get; set; } = 48;

        public bool? ImmediateGracePeriod { get; set; } = true;

        [Required]
        [MaxLength(30)]
        public string PenaltyType { get; set; } = "LoseDeposit";

        [Range(typeof(decimal), "0", "999999999")]
        public decimal PenaltyValue { get; set; }

        public bool? ConfiscateOnNoShow { get; set; } = true;

        [Range(0, 1440)]
        public int NoShowGraceMinutes { get; set; } = 60;

        [Required]
        [MaxLength(20)]
        public string RefundTransferFee { get; set; } = "CustomerPays";

        [Range(1, 60)]
        public int RefundProcessingDays { get; set; } = 3;

        public string? ExceptionPoliciesJson { get; set; }

        [Required]
        [MaxLength(20)]
        public string OperationSystemMode { get; set; } = "FullDay";

        [MaxLength(50)]
        public string? CheckInTime { get; set; } = "14:00";

        [MaxLength(50)]
        public string? CheckOutTime { get; set; } = "12:00";

        public string? ShiftsJson { get; set; }

        // تم إلغاء خيار وقت التجهيز والتنظيف من الواجهة.
        // نبقي الخاصية للتوافق مع الـ API الحالي، لكن بدون أي فاصل مخفي.
        [Range(0, 24)]
        public int BufferTimeHours
        {
            get => 0;
            set { }
        }

        [Range(0, 8760)]
        public int MinimumNoticeHours { get; set; } = 24;

        [Range(1, 60)]
        public int MaximumHorizonMonths { get; set; } = 6;

        public string? ClosedDaysCsv { get; set; }

        public bool? IsChatEnabled { get; set; } = true;
        public string? WelcomeMessage { get; set; }
        public bool OfflineReplyToggle { get; set; }
        public string? OfflineMessage { get; set; }
        public string? QuickRepliesJson { get; set; }

        [Required]
        [MaxLength(100)]
        public string EmergencyContact { get; set; } = string.Empty;

        private string? _contactEmail;

        [EmailAddress]
        [MaxLength(255)]
        public string? ContactEmail
        {
            get => _contactEmail;
            set => _contactEmail =
                string.IsNullOrWhiteSpace(value)
                    ? null
                    : value.Trim();
        }

        public bool? IsSoundAlertEnabled { get; set; } = true;
        public bool? IsPushNoticeEnabled { get; set; } = true;

        public bool ForFamiliesOnly { get; set; }
        public bool AllowSmoking { get; set; }
        public bool AllowParties { get; set; }
        public bool AllowQatInside { get; set; }
    }
}
