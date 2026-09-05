using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Owner
{
    /// <summary>
    /// البيانات الكاملة لصفحة لوحة تحكم المالك.
    /// كل القيم في هذا الملف بيانات عرض فقط ولا تنفذ أي عملية حفظ.
    /// </summary>
    public sealed class OwnerDashboardDto
    {
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = "المالك";

        public bool HasPlace { get; set; }
        public int? PlaceId { get; set; }
        public string PlaceName { get; set; } = "منشأتك";
        public string CategoryName { get; set; } = string.Empty;
        public int BookingSystemId { get; set; }
        public string PlaceStatus { get; set; } = string.Empty;
        public string PlaceStatusLabel { get; set; } = string.Empty;
        public bool IsPlaceAvailable { get; set; }
        public string Currency { get; set; } = "ريال يمني";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public OwnerDashboardKpiDto Kpis { get; set; } = new();
        public OwnerDashboardBookingSummaryDto BookingSummary { get; set; } = new();
        public OwnerDashboardBookingTrendDto BookingTrend { get; set; } = new();
        public OwnerDashboardFinanceDto Finance { get; set; } = new();
        public OwnerDashboardGuaranteeDto Guarantee { get; set; } = new();
        public OwnerDashboardTodayOperationDto TodayOperation { get; set; } = new();
        public OwnerDashboardReviewsDto Reviews { get; set; } = new();
        public OwnerDashboardMarketingDto Marketing { get; set; } = new();

        public List<OwnerDashboardAttentionItemDto> AttentionItems { get; set; } = new();
        public List<OwnerDashboardActivityDto> RecentActivities { get; set; } = new();
        public List<OwnerDashboardCalendarEventDto> CalendarEvents { get; set; } = new();

        // حالة الفترة التجريبية لحساب المالك داخل الداشبورد.
        // مستقلة عن منبثقة الترحيب حتى تبقى حالة العدّاد والتنبيهات متاحة دائمًا.
        public OwnerTrialStatusDto TrialStatus { get; set; } = new();

        // تبقى هذه البيانات مخصصة لمنبثقة الفترة التجريبية الحالية.
        public OwnerTrialWelcomeDto TrialWelcome { get; set; } = new();
    }

    public sealed class OwnerDashboardKpiDto
    {
        public int NewBookingsToday { get; set; }
        public int TodayBookings { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal PreviousMonthRevenue { get; set; }
        public decimal RevenueChangePercent { get; set; }
        public decimal OccupancyPercent { get; set; }
        public bool HasRoomUnits { get; set; }
        public double AverageRating { get; set; }
        public int ReviewsCount { get; set; }
        public decimal AvailableGuaranteeBalance { get; set; }
        public string GuaranteeStatus { get; set; } = "NeedsTopUp";
        public int FavoritesCount { get; set; }
    }

    public sealed class OwnerDashboardBookingSummaryDto
    {
        public int TotalThisMonth { get; set; }
        public int Pending { get; set; }
        public int Confirmed { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int NoShow { get; set; }
        public decimal CancellationRate { get; set; }
    }

    public sealed class OwnerDashboardBookingTrendDto
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Bookings { get; set; } = new();
        public List<int> Completed { get; set; } = new();
        public List<int> Cancelled { get; set; } = new();
    }

    public sealed class OwnerDashboardFinanceDto
    {
        public decimal GrossBookingValue { get; set; }
        public decimal PaidOnline { get; set; }
        public decimal CollectedAtProperty { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal PlatformCommission { get; set; }
        public decimal NetCollectedRevenue { get; set; }
        public List<string> MonthlyLabels { get; set; } = new();
        public List<decimal> MonthlyCollected { get; set; } = new();
        public List<decimal> MonthlyCommission { get; set; } = new();
    }

    public sealed class OwnerDashboardGuaranteeDto
    {
        public decimal AvailableBalance { get; set; }
        public decimal HeldBalance { get; set; }
        public decimal DebtAmount { get; set; }
        public decimal RequiredBalance { get; set; }
        public decimal LowBalanceThreshold { get; set; }
        public decimal FundingPercent { get; set; }
        public string AccountStatus { get; set; } = "NeedsTopUp";
        public string AccountStatusLabel { get; set; } = "يحتاج شحن";
    }

    public sealed class OwnerDashboardAttentionItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-info-circle";
        public string Tone { get; set; } = "info";
        public int Count { get; set; }
        public string Url { get; set; } = "#";
        public string ActionLabel { get; set; } = "فتح";
    }

    public sealed class OwnerDashboardTodayOperationDto
    {
        public int ArrivalsToday { get; set; }
        public int DeparturesToday { get; set; }
        public int ActiveNow { get; set; }
        public int TotalRoomUnits { get; set; }
        public int OccupiedRoomUnits { get; set; }
        public int AvailableRoomUnits { get; set; }
        public bool HasRoomUnits { get; set; }
        public List<OwnerDashboardTodayBookingDto> Bookings { get; set; } = new();
    }

    public sealed class OwnerDashboardTodayBookingDto
    {
        public int BookingId { get; set; }
        public string CustomerName { get; set; } = "عميل";
        public string ResourceName { get; set; } = "الحجز";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string BookingState { get; set; } = string.Empty;
        public string BookingStateLabel { get; set; } = string.Empty;
        public string BookingSource { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Url { get; set; } = "#";
    }

    public sealed class OwnerDashboardReviewsDto
    {
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int AwaitingReply { get; set; }
        public List<int> StarCounts { get; set; } = new() { 0, 0, 0, 0, 0 };
        public List<OwnerDashboardReviewItemDto> LatestReviews { get; set; } = new();
    }

    public sealed class OwnerDashboardReviewItemDto
    {
        public int ReviewId { get; set; }
        public string CustomerName { get; set; } = "عميل";
        public int Stars { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public bool HasOwnerReply { get; set; }
        public string Url { get; set; } = "#";
    }

    public sealed class OwnerDashboardMarketingDto
    {
        public int ActivePromotions { get; set; }
        public int ActiveAdvertisements { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long BookingStarts { get; set; }
        public long Conversions { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal ConversionRate { get; set; }
        public int Favorites { get; set; }
    }

    public sealed class OwnerDashboardActivityDto
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-activity";
        public string Tone { get; set; } = "info";
        public DateTime OccurredAt { get; set; }
        public string Url { get; set; } = "#";
    }

    public sealed class OwnerDashboardCalendarEventDto
    {
        public int BookingId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string State { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = "#";
    }


    /// <summary>
    /// الحالة المستمرة للفترة التجريبية داخل لوحة المالك.
    /// التجربة مرتبطة بحساب المالك، بينما الرصيد والبيانات التشغيلية مرتبطة بالمنشأة المختارة.
    /// </summary>
    public sealed class OwnerTrialStatusDto
    {
        public bool HasTrialHistory { get; set; }
        public bool IsActive { get; set; }
        public bool IsExpired { get; set; }
        public bool IsEndingSoon { get; set; }
        public int RemainingTrialDays { get; set; }
        public int TotalTrialDays { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public decimal RequiredGuaranteeBalance { get; set; }
        public decimal AvailableGuaranteeBalance { get; set; }
        public decimal MissingGuaranteeBalance { get; set; }
        public bool CanAcceptNewBookings { get; set; }
        public bool NeedsTopUp { get; set; }
        public string Currency { get; set; } = "ريال يمني";
        public string AlertLevel { get; set; } = "normal";
        public string ReminderKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// نفس بيانات الرسالة الحالية للمالك الجديد؛ لا تغيّر نص الرسالة أو منطق ظهورها.
    /// </summary>
    public sealed class OwnerTrialWelcomeDto
    {
        public bool ShouldShow { get; set; }
        public bool IsActive { get; set; }
        public int? PlaceId { get; set; }
        public string PlaceName { get; set; } = "منشأتك";
        public DateTime? StartedAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public int TotalTrialDays { get; set; }
        public int RemainingTrialDays { get; set; }
        public decimal RequiredGuaranteeBalance { get; set; }
        public string Currency { get; set; } = "ريال يمني";
        public bool CanAcceptNewBookings { get; set; }
        public string StorageKey { get; set; } = string.Empty;
    }
}
