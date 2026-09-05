using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlacePolicy
    {
        public int PlaceId { get; set; }
        public string BookingApprovalMode { get; set; } = null!;
        public string? DepositPoliciesJson { get; set; }
        public string? SecurityPoliciesJson { get; set; }
        public int PaymentTimeout { get; set; }
        public bool? IsSarSupported { get; set; }
        public bool? IsUsdSupported { get; set; }
        public decimal CleaningFee { get; set; }
        public decimal TaxPercentage { get; set; }
        public bool IsNonRefundableEnabled { get; set; }
        public int NonRefundableDiscount { get; set; }
        public int FreeCancellationHours { get; set; }
        public bool? ImmediateGracePeriod { get; set; }
        public string PenaltyType { get; set; } = null!;
        public decimal PenaltyValue { get; set; }
        public bool? ConfiscateOnNoShow { get; set; }
        public string RefundTransferFee { get; set; } = null!;
        public int RefundProcessingDays { get; set; }
        public string? ExceptionPoliciesJson { get; set; }
        public string OperationSystemMode { get; set; } = null!;
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public string? ShiftsJson { get; set; }
        public int BufferTimeHours { get; set; }
        public int MinimumNoticeHours { get; set; }
        public int MaximumHorizonMonths { get; set; }
        public string? ClosedDaysCsv { get; set; }
        public bool? IsChatEnabled { get; set; }
        public string? WelcomeMessage { get; set; }
        public bool OfflineReplyToggle { get; set; }
        public string? OfflineMessage { get; set; }
        public string? QuickRepliesJson { get; set; }
        public string? EmergencyContact { get; set; }
        public string? ContactEmail { get; set; }
        public bool? IsSoundAlertEnabled { get; set; }
        public bool? IsPushNoticeEnabled { get; set; }
        public bool ForFamiliesOnly { get; set; }
        public bool AllowSmoking { get; set; }
        public bool AllowParties { get; set; }
        public bool AllowQatInside { get; set; }
        public DateTime CreatedAt { get; set; }
        public int NoShowGraceMinutes { get; set; }

        public virtual Place Place { get; set; } = null!;
    }
}
