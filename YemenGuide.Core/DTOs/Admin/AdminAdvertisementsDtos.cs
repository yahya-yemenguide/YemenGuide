using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Admin
{
    public sealed class AdminAdvertisementManagementPageDto
    {
        public string DefaultTab { get; set; } = "overview";
        public bool CanView { get; set; }
        public bool CanReview { get; set; }
        public bool CanManagePlans { get; set; }
        public bool CanControl { get; set; }
        public bool CanViewReports { get; set; }
        public AdminAdvertisementOverviewDto Overview { get; set; } = new();
        public AdminAdvertisementsTabDto Advertisements { get; set; } = new();
        public AdminPlacementPlansTabDto Plans { get; set; } = new();
        public AdminAdvertisementReportsDto Reports { get; set; } = new();
    }

    public sealed class AdminAdvertisementOverviewDto
    {
        public int AwaitingPaymentCount { get; set; }
        public int PendingPaymentReviewCount { get; set; }
        public int PendingReviewCount { get; set; }
        public int RefundPendingCount { get; set; }
        public int ScheduledCount { get; set; }
        public int ActiveCount { get; set; }
        public int PausedCount { get; set; }
        public int ExpiredCount { get; set; }
        public int EndingSoonCount { get; set; }
        public int BudgetRiskCount { get; set; }
        public int ActivePlacementCount { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal SpentAmount { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long Conversions { get; set; }
        public decimal TodaySpentAmount { get; set; }
        public long TodayImpressions { get; set; }
        public long TodayClicks { get; set; }
        public long TodayBookingStarts { get; set; }
        public long TodayConversions { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public decimal ClickThroughRate => Impressions <= 0 ? 0 : Math.Round((decimal)Clicks * 100m / Impressions, 2);
        public decimal ConversionRate => Clicks <= 0 ? 0 : Math.Round((decimal)Conversions * 100m / Clicks, 2);
        public decimal TodayClickThroughRate => TodayImpressions <= 0 ? 0 : Math.Round((decimal)TodayClicks * 100m / TodayImpressions, 2);
        public decimal TodayConversionRate => TodayClicks <= 0 ? 0 : Math.Round((decimal)TodayConversions * 100m / TodayClicks, 2);
        public int NeedsActionCount => PendingPaymentReviewCount + PendingReviewCount + RefundPendingCount;
        public int RunningCount => ActiveCount + ScheduledCount;
        public int OperationalAlertsCount => EndingSoonCount + BudgetRiskCount + PausedCount;
        public List<AdminAdvertisementCardDto> NeedsAttention { get; set; } = new();
        public List<AdminPlacementMetricDto> PlacementMetrics { get; set; } = new();
    }

    public sealed class AdminAdvertisementFilterDto
    {
        public string? Search { get; set; }
        public string Status { get; set; } = "All";
        public string BillingModel { get; set; } = "All";
        public string PlacementType { get; set; } = "All"; // توافق تاريخي فقط؛ لا يظهر في واجهة الإدارة الجديدة.
        public string PaymentStatus { get; set; } = "All";
        public string ExactStatus { get; set; } = "All";
        public string SortBy { get; set; } = "Newest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    public sealed class AdminAdvertisementsTabDto
    {
        public AdminAdvertisementFilterDto Filter { get; set; } = new();
        public List<AdminAdvertisementCardDto> Items { get; set; } = new();
        public int AllCount { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int AwaitingPaymentCount { get; set; }
        public int PendingCount { get; set; }
        public int ScheduledCount { get; set; }
        public int ActiveCount { get; set; }
        public int PausedCount { get; set; }
        public int ExpiredCount { get; set; }
        public int RejectedCount { get; set; }
        public int NeedsActionCount { get; set; }
        public int RunningCount { get; set; }
        public int ClosedCount { get; set; }
        public bool CanReview { get; set; }
        public bool CanControl { get; set; }
    }

    public sealed class AdminAdvertisementCardDto
    {
        public int AdvertisementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string PlaceName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int DisplayWeight { get; set; }
        public int DurationDays { get; set; }
        public string PlacementType { get; set; } = string.Empty;
        public string PlacementLabel { get; set; } = string.Empty;
        public string BillingModel { get; set; } = string.Empty;
        public string BillingLabel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "ريال يمني";
        public decimal? TotalBudget { get; set; }
        public decimal SpentAmount { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long Conversions { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public bool NeedsAttention { get; set; }
        public string? AttentionText { get; set; }
    }

    public sealed class AdminAdvertisementDetailsDto
    {
        public int AdvertisementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? ImagePath { get; set; }
        public string ButtonText { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? OwnerPhone { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string? PromotionName { get; set; }

        // بيانات النموذج الجديد التي يحتاجها الأدمن عند مراجعة الإعلان.
        public string PlanName { get; set; } = string.Empty;
        public int DisplayWeight { get; set; }
        public int DurationDays { get; set; }
        public decimal DailyPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ClickThroughRate =>
            Impressions <= 0 ? 0m : Math.Round((decimal)Clicks * 100m / Impressions, 2);

        public string PlacementType { get; set; } = string.Empty;
        public string PlacementLabel { get; set; } = string.Empty;
        public string BillingModel { get; set; } = string.Empty;
        public string BillingLabel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CostPerClick { get; set; }
        public decimal? CostPerThousand { get; set; }
        public decimal? TotalBudget { get; set; }
        public decimal? DailyBudget { get; set; }
        public decimal SpentAmount { get; set; }
        public string Currency { get; set; } = "ريال يمني";
        public bool StartImmediately { get; set; }
        public DateTime? RequestedStartAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public string TargetingMode { get; set; } = string.Empty;
        public string DeviceTarget { get; set; } = string.Empty;
        public int FrequencyCap { get; set; }
        public decimal? TargetLatitude { get; set; }
        public decimal? TargetLongitude { get; set; }
        public decimal? TargetRadiusKm { get; set; }
        public string DestinationType { get; set; } = string.Empty;
        public string? DestinationPath { get; set; }
        public int? PlatformWalletId { get; set; }
        public decimal? TransferredAmount { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime? TransferDate { get; set; }
        public string? ReceiptImage { get; set; }

        // بيانات الاسترداد الموثقة.
        public DateTime? RefundRequestedAt { get; set; }
        public decimal? RefundAmount { get; set; }
        public int? RefundWalletId { get; set; }
        public string? RefundWalletName { get; set; }
        public string? RefundAccountNumber { get; set; }
        public string? RefundBeneficiaryName { get; set; }
        public string? RefundReferenceNumber { get; set; }
        public string? RefundProofImage { get; set; }
        public DateTime? RefundedAt { get; set; }
        public string? RefundedByName { get; set; }
        public string? RefundNote { get; set; }

        public List<AdminAdvertisementRefundDestinationDto> RefundDestinations { get; set; } = new();
        public List<AdminAdvertisementRefundWalletDto> RefundWallets { get; set; } = new();

        public string? RejectionReason { get; set; }
        public string? PauseReason { get; set; }
        public string? CancellationReason { get; set; }
        public long Impressions { get; set; }
        public long UniqueImpressions { get; set; }
        public long Clicks { get; set; }
        public long BookingStarts { get; set; }
        public long Conversions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public List<string> Cities { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public bool CanReview { get; set; }
        public bool CanControl { get; set; }
        public bool CanRefund { get; set; }
    }

    public sealed class AdminPlacementPlansTabDto
    {
        public List<AdminPlacementGroupDto> Placements { get; set; } = new();
        public bool CanManagePlans { get; set; }
        public int ActiveOptionsCount { get; set; }
        public int ConfiguredPlacementsCount { get; set; }
    }

    public sealed class AdminPlacementGroupDto
    {
        public string PlacementType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-megaphone";
        public List<AdminAdvertisementPlanCardDto> PricingOptions { get; set; } = new();
    }

    public sealed class AdminAdvertisementPlanCardDto
    {
        public int AdvertisementPlanId { get; set; }

        public string PlanName { get; set; } = string.Empty;

        // النظام الجديد: Price في قاعدة البيانات = سعر اليوم.
        public decimal DailyPrice { get; set; }

        // النظام الجديد: DisplayPriority في قاعدة البيانات = وزن الظهور.
        // الوزن لا يمثل نسبة ظهور مضمونة؛ بل يدخل في التوزيع الاحتمالي بين الإعلانات.
        public int DisplayWeight { get; set; }

        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public int AdvertisementCount { get; set; }
        public int ActiveAdvertisementCount { get; set; }

        // حقول توافق داخلي مؤقتة حتى لا تتكسر أجزاء قديمة أثناء انتقال النظام.
        public string PlacementType { get; set; } = "SearchTop";
        public string PlacementLabel { get; set; } = "صفحة البحث";
        public string BillingModel { get; set; } = "Flat";
        public string BillingLabel { get; set; } = "سعر يومي";
        public decimal UnitPrice { get; set; }
        public decimal? MinimumTotalBudget { get; set; }
        public decimal? DefaultDailyBudget { get; set; }
        public int MaxActiveAdvertisements { get; set; }
        public int DisplayPriority { get; set; }
        public int MaxMediaItems { get; set; } = 1;
        public bool AllowVideo { get; set; } = true;
    }

    public sealed class AdminAdvertisementPlanUpsertDto
    {
        public int? AdvertisementPlanId { get; set; }

        [Required(ErrorMessage = "اسم الباقة مطلوب.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "اسم الباقة يجب أن يكون بين 2 و100 حرف.")]
        public string PlanName { get; set; } = string.Empty;

        // لا نستخدم RangeAttribute للـ decimal هنا لأن Culture العربية
        // قد تجعل ASP.NET يحاول تفسير "0.01" بفاصل عشري مختلف ويعيد HTTP 500
        // قبل دخول الطلب إلى الـ Controller. التحقق الفعلي موجود في PricingService.
        public decimal DailyPrice { get; set; }

        [Range(1, 100, ErrorMessage = "وزن الظهور يجب أن يكون بين 1 و100.")]
        public int DisplayWeight { get; set; } = 0;

        [StringLength(500, ErrorMessage = "وصف الباقة لا يتجاوز 500 حرف.")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        // قيم داخلية ثابتة للنظام الجديد. لا تظهر للأدمن في الواجهة.
        public string PlacementType { get; set; } = "SearchTop";
        public string BillingModel { get; set; } = "Flat";
        public decimal UnitPrice { get; set; }
        public decimal? MinimumTotalBudget { get; set; }
        public decimal? DefaultDailyBudget { get; set; }
        public int MaxActiveAdvertisements { get; set; } = int.MaxValue;
        public int DisplayPriority { get; set; } = 0;
        public int MaxMediaItems { get; set; } = 1;
        public bool AllowVideo { get; set; } = true;
    }

    public sealed class AdminAdvertisementReportFilterDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? PlaceId { get; set; }
        public int? AdvertisementId { get; set; }
        public int? AdvertisementPlanId { get; set; }

        [StringLength(30)]
        public string PlacementType { get; set; } = "All";

        [StringLength(20)]
        public string BillingModel { get; set; } = "All";

        [StringLength(30)]
        public string Status { get; set; } = "All";

        [StringLength(30)]
        public string PaymentStatus { get; set; } = "All";

        [StringLength(100)]
        public string? Search { get; set; }

        [StringLength(30)]
        public string SortBy { get; set; } = "Impressions";

        [StringLength(4)]
        public string SortDirection { get; set; } = "Desc";
    }

    public sealed class AdminAdvertisementReportOptionDto
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? SecondaryLabel { get; set; }
    }

    public sealed class AdminAdvertisementReportsDto
    {
        public AdminAdvertisementReportFilterDto Filter { get; set; } = new();
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string ScopeLabel { get; set; } = "التقرير العام للمنصة";
        public List<string> AppliedFilters { get; set; } = new();
        public List<AdminAdvertisementReportOptionDto> PlaceOptions { get; set; } = new();
        public List<AdminAdvertisementReportOptionDto> AdvertisementOptions { get; set; } = new();
        public List<AdminAdvertisementReportOptionDto> PlanOptions { get; set; } = new();
        public int TotalAdvertisements { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingBudget { get; set; }
        public int PaidAdvertisements { get; set; }
        public int RefundedAdvertisements { get; set; }
        public int PendingPayments { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long BookingStarts { get; set; }
        public long Conversions { get; set; }
        public decimal ClickThroughRate => Impressions <= 0 ? 0 : Math.Round((decimal)Clicks * 100m / Impressions, 2);
        public decimal ConversionRate => Clicks <= 0 ? 0 : Math.Round((decimal)Conversions * 100m / Clicks, 2);
        public List<AdminAdvertisementDailyMetricDto> DailyMetrics { get; set; } = new();
        public List<AdminPlacementMetricDto> PlacementMetrics { get; set; } = new();
        public List<AdminBillingMetricDto> BillingMetrics { get; set; } = new();
        public List<AdminAdvertisementReportItemDto> TopAdvertisements { get; set; } = new();
        public List<AdminPaymentMetricDto> PaymentMetrics { get; set; } = new();
    }

    public sealed class AdminAdvertisementDailyMetricDto
    {
        public DateTime Date { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long BookingStarts { get; set; }
        public long Conversions { get; set; }
        public decimal SpentAmount { get; set; }
    }

    public sealed class AdminPlacementMetricDto
    {
        public string PlacementType { get; set; } = string.Empty;
        public string PlacementLabel { get; set; } = string.Empty;
        public int Advertisements { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long Conversions { get; set; }
        public decimal SpentAmount { get; set; }
    }

    public sealed class AdminBillingMetricDto
    {
        public string BillingModel { get; set; } = string.Empty;
        public string BillingLabel { get; set; } = string.Empty;
        public int Advertisements { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal SpentAmount { get; set; }
    }

    public sealed class AdminAdvertisementReportItemDto
    {
        public int AdvertisementId { get; set; }
        public int PlaceId { get; set; }
        public int AdvertisementPlanId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PlaceName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int DisplayWeight { get; set; }
        public int DurationDays { get; set; }
        public decimal DailyPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string PlacementLabel { get; set; } = string.Empty;
        public string BillingLabel { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal SpentAmount { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public long BookingStarts { get; set; }
        public long Conversions { get; set; }
        public decimal ClickThroughRate => Impressions <= 0 ? 0 : Math.Round((decimal)Clicks * 100m / Impressions, 2);
        public decimal ConversionRate => Clicks <= 0 ? 0 : Math.Round((decimal)Conversions * 100m / Clicks, 2);
    }

    public sealed class AdminPaymentMetricDto
    {
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = string.Empty;
        public int Advertisements { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class AdminAdvertisementPaymentReviewDto
    {
        public int AdvertisementId { get; set; }
        [Required, StringLength(20)]
        public string Decision { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public sealed class AdminAdvertisementReviewDto
    {
        public int AdvertisementId { get; set; }
        [Required, StringLength(20)]
        public string Decision { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public sealed class AdminAdvertisementControlDto
    {
        public int AdvertisementId { get; set; }
        [Required, StringLength(20)]
        public string Action { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public sealed class AdminAdvertisementRefundDto
    {
        [Range(1, int.MaxValue)]
        public int AdvertisementId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر وسيلة الاسترداد.")]
        public int RefundWalletId { get; set; }

        public int? OwnerWalletId { get; set; }

        [Required(ErrorMessage = "رقم الحساب أو المحفظة مطلوب.")]
        [StringLength(100)]
        public string RefundAccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم المستفيد مطلوب.")]
        [StringLength(150)]
        public string RefundBeneficiaryName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم مرجع الاسترداد مطلوب.")]
        [StringLength(100, MinimumLength = 3)]
        public string RefundReferenceNumber { get; set; } = string.Empty;

        [StringLength(500)]
        public string? RefundNote { get; set; }

        [StringLength(255)]
        public string? RefundProofImage { get; set; }
    }

    public sealed class AdminAdvertisementRefundDestinationDto
    {
        public int OwnerWalletId { get; set; }
        public int WalletId { get; set; }
        public string WalletName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
    }

    public sealed class AdminAdvertisementRefundWalletDto
    {
        public int WalletId { get; set; }
        public string WalletName { get; set; } = string.Empty;
    }

    public sealed class AdminAdvertisementPlanStatusDto
    {
        public int AdvertisementPlanId { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class AdvertisementPriceQuoteRequestDto
    {
        public string BillingModel { get; set; } = "Flat";
        public decimal UnitPrice { get; set; }
        public int DurationDays { get; set; }
        public decimal? TotalBudget { get; set; }
        public decimal? DailyBudget { get; set; }
        public decimal? MinimumTotalBudget { get; set; }
    }

    public sealed class AdvertisementPriceQuoteDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public decimal? DailyAmount { get; set; }
        public long? EstimatedClicks { get; set; }
        public long? EstimatedImpressions { get; set; }
    }

    public sealed class AdminAdvertisementOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? EntityId { get; set; }

        public static AdminAdvertisementOperationResult Ok(string message, int? entityId = null) =>
            new() { Success = true, Message = message, EntityId = entityId };

        public static AdminAdvertisementOperationResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
