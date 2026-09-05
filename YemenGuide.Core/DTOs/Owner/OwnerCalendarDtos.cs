using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Owner
{
    public sealed class OwnerCalendarContextDto
    {
        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int BookingSystemId { get; set; }
        public bool SupportsRooms { get; set; }
        public bool SupportsPackages { get; set; }
        public string ManualBookingTemplate { get; set; } = "Hybrid";
        public string ManualBookingTemplateLabel { get; set; } = string.Empty;
        public int TotalActiveRoomUnits { get; set; }
        public int OccupiedRoomUnitsToday { get; set; }
        public int AvailableRoomUnitsToday { get; set; }
        public decimal OccupancyPercentToday { get; set; }
        public int TotalBookingsInRange { get; set; }
        public int ConfirmedBookingsInRange { get; set; }
        public int AwaitingPaymentBookingsInRange { get; set; }
        public int ManualBookingsInRange { get; set; }
        public int OnlineBookingsInRange { get; set; }
        public int ClosedDaysInRange { get; set; }
        public string BookingApprovalMode { get; set; } = "Manual";
        public int MinimumNoticeHours { get; set; }
        public int BufferTimeHours { get; set; }
        public int MaximumHorizonMonths { get; set; }
        public string ClosedDaysCsv { get; set; } = string.Empty;
        public string CheckInTime { get; set; } = string.Empty;
        public string CheckOutTime { get; set; } = string.Empty;
    }

    public sealed class OwnerCalendarEventDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public bool AllDay { get; set; } = true;
        public string EventType { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int? BookingId { get; set; }
        public int? ExceptionId { get; set; }
        public int? OccupiedUnits { get; set; }
        public int? TotalUnits { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime? TimerExpiredAt { get; set; }
        public bool IsHistorical { get; set; }
        public List<string> ClassNames { get; set; } = new();
    }

    public sealed class OwnerCalendarDayDetailsDto
    {
        public DateTime Date { get; set; }
        public bool IsWeeklyClosed { get; set; }
        public bool IsExceptionClosed { get; set; }
        public string ClosedReason { get; set; } = string.Empty;
        public int TotalActiveRoomUnits { get; set; }
        public int OccupiedRoomUnits { get; set; }
        public int AvailableRoomUnits { get; set; }
        public decimal OccupancyPercent { get; set; }
        public int BookingCount { get; set; }
        public int ConfirmedCount { get; set; }
        public int AwaitingPaymentCount { get; set; }
        public int PaymentReviewCount { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int NoShowCount { get; set; }
        public int CancelledCount { get; set; }
        public int RejectedCount { get; set; }
        public int ExpiredCount { get; set; }
        public int HistoricalCount { get; set; }
        public int ManualCount { get; set; }
        public int OnlineCount { get; set; }
        public List<OwnerCalendarBookingItemDto> Bookings { get; set; } = new();
    }

    public sealed class OwnerCalendarBookingItemDto
    {
        public int BookingId { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string BookingSource { get; set; } = "Online";
        public string ManualBookingChannel { get; set; } = string.Empty;
        public string BookingState { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string RefundStatus { get; set; } = string.Empty;
        public string StatusReason { get; set; } = string.Empty;
        public DateTime? ClosedAt { get; set; }
        public int Quantity { get; set; }
        public int AdultsCount { get; set; }
        public int ChildrenCount { get; set; }
        public int? RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public int? PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string OwnerInternalNotes { get; set; } = string.Empty;
        public string AssignedUnitsText { get; set; } = string.Empty;
        public DateTime? TimerExpiredAt { get; set; }
        public bool IsTimerExpired { get; set; }
        public bool IsHistorical { get; set; }
        public bool CanRecordManualPayment { get; set; }
    }

    public sealed class OwnerManualBookingCustomerResultDto
    {
        public bool Found { get; set; }
        public int? CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
    }

    public sealed class OwnerManualBookingOptionsDto
    {
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsClosedByPolicy { get; set; }
        public bool IsClosedByException { get; set; }
        public string ClosureMessage { get; set; } = string.Empty;
        public List<OwnerManualRoomOptionDto> Rooms { get; set; } = new();
        public List<OwnerManualPackageOptionDto> Packages { get; set; } = new();
        public List<OwnerManualServiceOptionDto> Services { get; set; } = new();
    }

    public sealed class OwnerManualRoomOptionDto
    {
        public int RoomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int ActiveUnits { get; set; }
        public int AvailableUnits { get; set; }
        public int MaxAdults { get; set; }
        public int MaxChildren { get; set; }
        public int? BedsCount { get; set; }
        public string BedType { get; set; } = string.Empty;
        public decimal? AreaSquareMeters { get; set; }
        public List<OwnerManualRoomUnitOptionDto> AvailableRoomUnits { get; set; } = new();
        public List<OwnerManualRoomUnitOptionDto> RoomUnits { get; set; } = new();
        public bool IsAvailable => AvailableUnits > 0;
    }

    public sealed class OwnerManualRoomUnitOptionDto
    {
        public int RoomUnitId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int FloorNumber { get; set; }
        public string FloorLabel { get; set; } = string.Empty;
        public bool IsSelectable { get; set; }
        public string AvailabilityState { get; set; } = "Available";
        public string AvailabilityLabel { get; set; } = "متاحة";
        public int? BlockingBookingId { get; set; }
        public DateTime? TimerExpiredAt { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string DisplayText => string.IsNullOrWhiteSpace(FloorLabel)
            ? RoomNumber
            : $"{RoomNumber} · {FloorLabel}";
    }

    public sealed class OwnerManualPackageOptionDto
    {
        public int PackageId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TimeFrame { get; set; } = string.Empty;
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public decimal Price { get; set; }
        public int Capacity { get; set; }
        public bool IsAvailable { get; set; }
        public string AvailabilityMessage { get; set; } = string.Empty;
    }

    public sealed class OwnerManualServiceOptionDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public sealed class OwnerManualBookingServiceInputDto
    {
        [Range(1, int.MaxValue)]
        public int ServiceId { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; } = 1;
    }

    public sealed class OwnerManualBookingRequestDto
    {
        public int? CustomerId { get; set; }

        [StringLength(150)]
        public string? GuestName { get; set; }

        [StringLength(50)]
        public string? GuestPhone { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string? GuestEmail { get; set; }

        [Required]
        [StringLength(20)]
        public string ManualBookingChannel { get; set; } = "Phone";

        [StringLength(100)]
        public string? ManualBookingChannelOther { get; set; }

        public int? RoomId { get; set; }
        public int? PackageId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; } = 1;

        public List<int> SelectedRoomUnitIds { get; set; } = new();

        [Range(0, 1000)]
        public int AdultsCount { get; set; } = 1;

        [Range(0, 1000)]
        public int ChildrenCount { get; set; }

        [StringLength(50)]
        public string? BookingType { get; set; }

        [StringLength(100)]
        public string? SeatingSetup { get; set; }

        [StringLength(1000)]
        public string? OwnerInternalNotes { get; set; }

        public List<OwnerManualBookingServiceInputDto> AdditionalServices { get; set; } = new();

        public int? PromotionId { get; set; }

        [StringLength(50)]
        public string? PromotionCode { get; set; }

        [Required]
        [StringLength(30)]
        public string PaymentMode { get; set; } = "Unpaid";

        [Range(0, 1000000000)]
        public decimal PaidAmount { get; set; }

        [StringLength(30)]
        public string? PaymentMethod { get; set; }

        [StringLength(100)]
        public string? PaymentReferenceNumber { get; set; }
    }

    public sealed class OwnerManualBookingQuoteDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string CustomerDisplayName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int Nights { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal ServicesTotal { get; set; }
        public decimal CleaningFee { get; set; }
        public decimal TaxPercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal CommissionPercentage { get; set; }
        public decimal PlatformCommission { get; set; }
        public int? PromotionId { get; set; }
        public string PromotionTitle { get; set; } = string.Empty;
        public string PromotionCode { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal? DiscountValue { get; set; }
        public string PromotionMessage { get; set; } = string.Empty;
        public bool RequiresDeposit => DepositAmount > 0m;
        public bool CanConfirmImmediately { get; set; }
        public string ProposedBookingState { get; set; } = string.Empty;
        public DateTime? TimerExpiredAt { get; set; }
        public List<OwnerManualAssignedUnitPreviewDto> AvailableUnits { get; set; } = new();
    }

    public sealed class OwnerManualAssignedUnitPreviewDto
    {
        public int RoomUnitId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int FloorNumber { get; set; }
        public string FloorLabel { get; set; } = string.Empty;
    }

    public sealed class OwnerCalendarRangeActionDto
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [StringLength(255)]
        public string? Reason { get; set; }
    }

    public sealed class OwnerManualPaymentInputDto
    {
        [Range(1, int.MaxValue)]
        public int BookingId { get; set; }

        [Range(0.01, 1000000000)]
        public decimal PaidAmount { get; set; }

        [Required]
        [StringLength(30)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(100)]
        public string? PaymentReferenceNumber { get; set; }
    }
}
