using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Booking
    {
        public Booking()
        {
            AdvertisementEvents = new HashSet<AdvertisementEvent>();
            BookingAdditionalServices = new HashSet<BookingAdditionalService>();
            BookingRoomUnits = new HashSet<BookingRoomUnit>();
            CommissionTransactions = new HashSet<CommissionTransaction>();
            RefundRequests = new HashSet<RefundRequest>();
            Reports = new HashSet<Report>();
        }

        public int BookingId { get; set; }
        public int? CustomerId { get; set; }
        public int PlaceId { get; set; }
        public int? RoomId { get; set; }
        public int? PackageId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string? BookingState { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string PaymentStatus { get; set; } = null!;
        public string? PaymentMethod { get; set; }
        public string? PaymentNotificationImage { get; set; }
        public DateTime? TimerExpiredAt { get; set; }
        public string RefundStatus { get; set; } = null!;
        public int Quantity { get; set; }
        public int AdultsCount { get; set; }
        public int ChildrenCount { get; set; }
        public string? BookingType { get; set; }
        public string? SeatingSetup { get; set; }
        public DateTime? LastStatusChangedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? PaymentApprovedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? NoShowAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ClosedByUserId { get; set; }
        public string? ClosedByType { get; set; }
        public string? CancellationReason { get; set; }
        public string? OwnerCancellationReason { get; set; }
        public string? PaymentProofRejectionReason { get; set; }
        public DateTime? PaymentProofRejectedAt { get; set; }
        public int PaymentProofReviewCount { get; set; }
        public byte[] RowVersion { get; set; } = null!;
        public int? PromotionId { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? PromotionTitleSnapshot { get; set; }
        public string? PromotionCodeSnapshot { get; set; }
        public string? DiscountTypeSnapshot { get; set; }
        public decimal? DiscountValueSnapshot { get; set; }
        public int? AdvertisementId { get; set; }
        public string BookingSource { get; set; } = null!;
        public string? ManualBookingChannel { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? GuestName { get; set; }
        public string? GuestPhone { get; set; }
        public string? GuestEmail { get; set; }
        public string? OwnerInternalNotes { get; set; }

        public virtual Advertisement? Advertisement { get; set; }
        public virtual User? CreatedByUser { get; set; }
        public virtual User? Customer { get; set; }
        public virtual PlacePackage? Package { get; set; }
        public virtual Place Place { get; set; } = null!;
        public virtual Promotion? Promotion { get; set; }
        public virtual Room? Room { get; set; }
        public virtual FinancialRecord? FinancialRecord { get; set; }
        public virtual ICollection<AdvertisementEvent> AdvertisementEvents { get; set; }
        public virtual ICollection<BookingAdditionalService> BookingAdditionalServices { get; set; }
        public virtual ICollection<BookingRoomUnit> BookingRoomUnits { get; set; }
        public virtual ICollection<CommissionTransaction> CommissionTransactions { get; set; }
        public virtual ICollection<RefundRequest> RefundRequests { get; set; }
        public virtual ICollection<Report> Reports { get; set; }
    }
}
