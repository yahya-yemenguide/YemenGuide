using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Owner
{
    public static class OwnerAdvertisementTabs
    {
        public const string Overview = "overview";
        public const string Advertisements = "advertisements";

        public static string Normalize(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                Advertisements => Advertisements,
                _ => Overview
            };
        }
    }

    public static class OwnerAdvertisementStatuses
    {
        public const string Draft = "Draft";
        public const string AwaitingPayment = "AwaitingPayment";
        public const string Pending = "Pending";
        public const string Scheduled = "Scheduled";
        public const string Active = "Active";
        public const string Paused = "Paused";
        public const string Expired = "Expired";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyCollection<string> All = new[]
        {
            Draft, AwaitingPayment, Pending, Scheduled, Active,
            Paused, Expired, Rejected, Cancelled
        };
    }

    public static class OwnerAdvertisementPaymentStatuses
    {
        public const string Unpaid = "Unpaid";
        public const string PendingReview = "PendingReview";
        public const string Paid = "Paid";
        public const string Rejected = "Rejected";
        public const string Refunded = "Refunded";
    }

    public static class OwnerAdvertisementBillingModels
    {
        public const string Flat = "Flat";
        public const string Cpc = "CPC";
        public const string Cpm = "CPM";
    }

    public static class OwnerAdvertisementPlacementTypes
    {
        // القناة الوحيدة للإعلانات الجديدة. لا تُعرض كاختيار للمالك.
        public const string SearchTop = "SearchTop";
    }

    public static class OwnerAdvertisementTargetingModes
    {
        public const string All = "All";
        public const string Custom = "Custom";
    }

    public static class OwnerAdvertisementDeviceTargets
    {
        public const string All = "All";
        public const string Desktop = "Desktop";
        public const string Mobile = "Mobile";
    }

    public static class OwnerAdvertisementDestinationTypes
    {
        // الوجهة ثابتة على صفحة المنشأة؛ PromotionId يبقى ارتباطًا اختياريًا فقط.
        public const string Place = "Place";
    }

    public static class OwnerAdvertisementMediaTypes
    {
        public const string Image = "Image";
        public const string Video = "Video";
    }

    public class OwnerAdvertisementOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class OwnerAdvertisementManagementPageDto
    {
        public string DefaultTab { get; set; } = OwnerAdvertisementTabs.Overview;
        public bool HasPlace { get; set; }
        public int? PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string? PlaceImage { get; set; }
        public string PlaceStatus { get; set; } = string.Empty;
        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string DisplayCurrencyCode { get; set; } = "YER";
        public string DisplayCurrencySymbol { get; set; } = "ر.ي";
        public int DraftCount { get; set; }
        public int AwaitingPaymentCount { get; set; }
        public int PendingCount { get; set; }
        public int ActiveCount { get; set; }
        public int ScheduledCount { get; set; }
        public int PausedCount { get; set; }
        public int ExpiredCount { get; set; }
        public int CancelledCount { get; set; }
        public int RejectedCount { get; set; }
        public int NeedsActionCount => DraftCount + AwaitingPaymentCount + RejectedCount;
        public int RunningCount => ActiveCount + ScheduledCount;

        // Single-view page payload: the page no longer depends on partial views.
        public OwnerAdvertisementOverviewDto Overview { get; set; } = new();
        public OwnerAdvertisementsTabDto Advertisements { get; set; } = new();
        public OwnerAdvertisementWizardDto Wizard { get; set; } = new();
    }

    public class OwnerAdvertisementOverviewDto
    {
        public bool HasPlace { get; set; }
        public int? PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string? PlaceImage { get; set; }
        public int TotalAdvertisements { get; set; }
        public int ActiveAdvertisements { get; set; }
        public int ScheduledAdvertisements { get; set; }
        public int PendingAdvertisements { get; set; }
        public int DraftAdvertisements { get; set; }
        public int AwaitingPaymentAdvertisements { get; set; }
        public int PaymentRejectedAdvertisements { get; set; }
        public int PausedAdvertisements { get; set; }
        public int EndingSoonAdvertisements { get; set; }
        public int BudgetRiskAdvertisements { get; set; }
        public int RejectedAdvertisements { get; set; }
        public long ImpressionsCount { get; set; }
        public long UniqueImpressionsCount { get; set; }
        public long ClicksCount { get; set; }
        public long BookingStartsCount { get; set; }
        public long ConversionsCount { get; set; }
        public decimal ClickThroughRate { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingBudget { get; set; }
        public string SpentAmountText { get; set; } = string.Empty;
        public string RemainingBudgetText { get; set; } = string.Empty;
        public List<OwnerAdvertisementListItemDto> RecentAdvertisements { get; set; } = new();
        public List<OwnerAdvertisementPlanCardDto> RecommendedPlans { get; set; } = new();
    }

    public class OwnerAdvertisementFilterDto
    {
        [StringLength(100)]
        public string? Search { get; set; }

        // All / NeedsAction / Running / Paused / Expired / Cancelled
        [StringLength(30)]
        public string Group { get; set; } = "All";

        [StringLength(30)]
        public string? PaymentStatus { get; set; }

        [StringLength(30)]
        public string SortBy { get; set; } = "Newest";

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 12;
    }

    public class OwnerAdvertisementsTabDto
    {
        public bool HasPlace { get; set; }
        public OwnerAdvertisementFilterDto Filter { get; set; } = new();
        public List<OwnerAdvertisementListItemDto> Items { get; set; } = new();
        public List<OwnerAdvertisementOptionDto> PaymentStatusOptions { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalPages { get; set; } = 1;
        public int AllCount { get; set; }
        public int NeedsActionCount { get; set; }
        public int RunningCount { get; set; }
        public int PausedCount { get; set; }
        public int ExpiredCount { get; set; }
        public int CancelledCount { get; set; }
    }

    public class OwnerAdvertisementListItemDto
    {
        public int AdvertisementId { get; set; }
        public int AdvertisementPlanId { get; set; }
        public string AdvertisementTitle { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? PrimaryMediaPath { get; set; }
        public string? PrimaryMediaType { get; set; }
        public string PlanName { get; set; } = string.Empty;

        // قوة الظهور ثابتة عند شراء الإعلان ومأخوذة من الباقة.
        // لا يملك المالك تعديلها من بطاقة الإعلان.
        public int DisplayWeight { get; set; }

        public string BillingModel { get; set; } = string.Empty;
        public string BillingModelLabel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = string.Empty;
        public decimal PriceSnapshot { get; set; }
        public decimal? TotalBudget { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingBudget { get; set; }
        public string PriceText { get; set; } = string.Empty;
        public string SpentAmountText { get; set; } = string.Empty;
        public string RemainingBudgetText { get; set; } = string.Empty;
        public long ImpressionsCount { get; set; }
        public long UniqueImpressionsCount { get; set; }
        public long ClicksCount { get; set; }
        public long BookingStartsCount { get; set; }
        public long ConversionsCount { get; set; }
        public decimal ClickThroughRate { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanPay { get; set; }
        public bool CanPause { get; set; }
        public bool CanResume { get; set; }
        public bool CanCancel { get; set; }
        public bool CanRenew { get; set; }
        public bool CanRestore { get; set; }
        public string CardMode { get; set; } = "waiting";
        public string PrimaryAction { get; set; } = "details";
        public string PrimaryActionLabel { get; set; } = "عرض التفاصيل";
        public string? AttentionText { get; set; }
    }

    public class OwnerAdvertisementPlanCardDto
    {
        public int AdvertisementPlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string BillingModel { get; set; } = string.Empty;
        public string BillingModelLabel { get; set; } = string.Empty;
        // سعر اليوم للباقة.
        public decimal Price { get; set; }

        // DisplayPriority في قاعدة البيانات = وزن الظهور النسبي للباقة.
        public int DisplayWeight { get; set; }

        public decimal? CostPerClick { get; set; }
        public decimal? CostPerThousandImpressions { get; set; }
        public decimal? MinimumTotalBudget { get; set; }
        public decimal? DefaultDailyBudget { get; set; }
        public string PriceText { get; set; } = string.Empty;
        public string? UnitCostText { get; set; }
        public string? MinimumBudgetText { get; set; }
        public string? Description { get; set; }
        public int MaxActiveAdvertisements { get; set; }
        public int MaxMediaItems { get; set; }
        public bool AllowVideo { get; set; }
        public bool IsAvailable { get; set; }
        public string AvailabilityMessage { get; set; } = string.Empty;
        public int ActiveAdvertisementsCount { get; set; }
    }

    public class OwnerAdvertisementWizardDto
    {
        public bool HasPlace { get; set; }
        public int? PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string? PlaceImage { get; set; }
        public OwnerAdvertisementSaveDto Advertisement { get; set; } = new();
        public List<OwnerAdvertisementPlanCardDto> Plans { get; set; } = new();
        public List<OwnerAdvertisementOptionDto> Cities { get; set; } = new();
        public List<OwnerAdvertisementOptionDto> Categories { get; set; } = new();
        public List<OwnerAdvertisementOptionDto> Promotions { get; set; } = new();
        public List<OwnerAdvertisementWalletDto> PlatformWallets { get; set; } = new();
        public List<OwnerAdvertisementMediaDto> Media { get; set; } = new();
        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string DisplayCurrencyCode { get; set; } = "YER";
        public string DisplayCurrencySymbol { get; set; } = "ر.ي";
        public bool IsEdit => Advertisement.AdvertisementId.HasValue;
    }

    public class OwnerAdvertisementSaveDto
    {
        public int? AdvertisementId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر باقة إعلانية صحيحة.")]
        public int AdvertisementPlanId { get; set; }

        [Required(ErrorMessage = "عنوان الإعلان مطلوب.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "عنوان الإعلان بين 3 و150 حرفًا.")]
        public string AdvertisementTitle { get; set; } = string.Empty;

        [StringLength(300, ErrorMessage = "الوصف المختصر لا يتجاوز 300 حرف.")]
        public string? ShortDescription { get; set; }

        [Required(ErrorMessage = "نص الزر مطلوب.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "نص الزر بين حرفين و50 حرفًا.")]
        public string ButtonText { get; set; } = "عرض المنشأة";

        public int? PromotionId { get; set; }
        public bool StartImmediately { get; set; } = true;
        public DateTime? RequestedStartAt { get; set; }

        [Range(1, 365, ErrorMessage = "مدة الإعلان يجب أن تكون بين يوم و365 يومًا.")]
        public int DurationDays { get; set; } = 1;

        [Range(0.01d, 9999999999999999d, ErrorMessage = "الميزانية الكلية يجب أن تكون أكبر من صفر.")]
        public decimal? TotalBudget { get; set; }

        [Range(0.01d, 9999999999999999d, ErrorMessage = "الميزانية اليومية يجب أن تكون أكبر من صفر.")]
        public decimal? DailyBudget { get; set; }

        [Required]
        [StringLength(20)]
        public string TargetingMode { get; set; } = OwnerAdvertisementTargetingModes.All;

        public List<int> CityIds { get; set; } = new();
        public List<int> CategoryIds { get; set; } = new();

        [Required]
        [StringLength(20)]
        public string DeviceTarget { get; set; } = OwnerAdvertisementDeviceTargets.All;

        [StringLength(200)]
        public string? RowVersion { get; set; }
    }

    public class OwnerAdvertisementPaymentSaveDto
    {
        [Range(1, int.MaxValue)]
        public int AdvertisementId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر محفظة دفع صحيحة.")]
        public int PlatformWalletId { get; set; }

        [Range(0.01d, 9999999999999999d, ErrorMessage = "المبلغ المحول يجب أن يكون أكبر من صفر.")]
        public decimal TransferredAmount { get; set; }

        [Required(ErrorMessage = "رقم الحوالة مطلوب.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "رقم الحوالة بين 3 و100 رقم.")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "رقم الحوالة يجب أن يحتوي على أرقام فقط دون حروف أو رموز.")]
        public string ReferenceNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاريخ الحوالة مطلوب.")]
        public DateTime? TransferDate { get; set; }

        [StringLength(255)]
        public string? ReceiptImagePath { get; set; }

        [Required]
        [StringLength(200)]
        public string RowVersion { get; set; } = string.Empty;
    }

    public class OwnerAdvertisementSubmitDto
    {
        [Range(1, int.MaxValue)]
        public int AdvertisementId { get; set; }

        [Required]
        [StringLength(200)]
        public string RowVersion { get; set; } = string.Empty;
    }

    public class OwnerAdvertisementStateChangeDto : OwnerAdvertisementSubmitDto
    {
        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public class OwnerAdvertisementRenewDto : OwnerAdvertisementSubmitDto
    {
        public bool StartImmediately { get; set; } = true;
        public DateTime? RequestedStartAt { get; set; }
    }

    public class OwnerAdvertisementMediaSaveDto
    {
        [Range(1, int.MaxValue)]
        public int AdvertisementId { get; set; }

        [Required]
        [StringLength(20)]
        public string MediaType { get; set; } = OwnerAdvertisementMediaTypes.Image;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ThumbnailPath { get; set; }

        [StringLength(200)]
        public string? AltText { get; set; }

        public bool IsPrimary { get; set; }

        [Required]
        [StringLength(200)]
        public string RowVersion { get; set; } = string.Empty;
    }

    public class OwnerAdvertisementMediaDeleteDto : OwnerAdvertisementSubmitDto
    {
        [Range(1, int.MaxValue)]
        public int AdvertisementMediaId { get; set; }
    }

    public class OwnerAdvertisementMediaDto
    {
        public int AdvertisementMediaId { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public string? AltText { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
    }

    public class OwnerAdvertisementWalletDto
    {
        public int PlatformWalletId { get; set; }
        public string WalletName { get; set; } = string.Empty;
        public string? LogoPath { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class OwnerAdvertisementTargetDto
    {
        public int AdvertisementTargetId { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public int? CityId { get; set; }
        public string? CityName { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string TargetLabel { get; set; } = string.Empty;
    }

    public class OwnerAdvertisementDetailsDto
    {
        public int AdvertisementId { get; set; }
        public int PlaceId { get; set; }
        public int AdvertisementPlanId { get; set; }
        public int? PromotionId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string? PromotionTitle { get; set; }
        public string AdvertisementTitle { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string ButtonText { get; set; } = string.Empty;
        public string DurationLabel { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public string BillingModel { get; set; } = string.Empty;
        public string BillingModelLabel { get; set; } = string.Empty;
        public decimal PriceSnapshot { get; set; }
        public decimal? CostPerClick { get; set; }
        public decimal? CostPerThousandImpressions { get; set; }
        public decimal? TotalBudget { get; set; }
        public decimal? DailyBudget { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal ExpectedPaymentAmount { get; set; }
        public string PriceText { get; set; } = string.Empty;
        public string SpentAmountText { get; set; } = string.Empty;
        public string RemainingBudgetText { get; set; } = string.Empty;
        public string ExpectedPaymentText { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public string? PauseReason { get; set; }
        public string? CancellationReason { get; set; }
        public bool StartImmediately { get; set; }
        public DateTime? RequestedStartAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public string TargetingMode { get; set; } = string.Empty;
        public string TargetingModeLabel { get; set; } = string.Empty;
        public string DeviceTarget { get; set; } = string.Empty;
        public string DeviceTargetLabel { get; set; } = string.Empty;
        public long ImpressionsCount { get; set; }
        public long UniqueImpressionsCount { get; set; }
        public long ClicksCount { get; set; }
        public long BookingStartsCount { get; set; }
        public long ConversionsCount { get; set; }
        public decimal ClickThroughRate { get; set; }
        public decimal ConversionRate { get; set; }
        public int? PlatformWalletId { get; set; }
        public string? WalletName { get; set; }
        public string? WalletAccountNumber { get; set; }
        public string? WalletBeneficiaryName { get; set; }
        public decimal? TransferredAmount { get; set; }
        public string? TransferredAmountText { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime? TransferDate { get; set; }
        public string? ReceiptImage { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanPay { get; set; }
        public bool CanPause { get; set; }
        public bool CanResume { get; set; }
        public bool CanCancel { get; set; }
        public bool CanRenew { get; set; }
        public bool CanRestore { get; set; }
        public List<OwnerAdvertisementMediaDto> Media { get; set; } = new();
        public List<OwnerAdvertisementTargetDto> Targets { get; set; } = new();
    }

    public class OwnerAdvertisementOperationResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? AdvertisementId { get; set; }
        public string? Status { get; set; }
        public string? PaymentStatus { get; set; }
        public string? RowVersion { get; set; }
        public decimal? ExpectedPaymentAmount { get; set; }
        public string? ExpectedPaymentText { get; set; }
        public string? AffectedFilePath { get; set; }

        public static OwnerAdvertisementOperationResultDto Ok(
            string message,
            int? advertisementId = null,
            string? status = null,
            string? paymentStatus = null,
            string? rowVersion = null,
            decimal? expectedPaymentAmount = null,
            string? expectedPaymentText = null,
            string? affectedFilePath = null)
        {
            return new OwnerAdvertisementOperationResultDto
            {
                Success = true,
                Message = message,
                AdvertisementId = advertisementId,
                Status = status,
                PaymentStatus = paymentStatus,
                RowVersion = rowVersion,
                ExpectedPaymentAmount = expectedPaymentAmount,
                ExpectedPaymentText = expectedPaymentText,
                AffectedFilePath = affectedFilePath
            };
        }

        public static OwnerAdvertisementOperationResultDto Fail(string message)
        {
            return new OwnerAdvertisementOperationResultDto
            {
                Success = false,
                Message = message
            };
        }
    }

    public class OwnerAdvertisementPriceQuoteRequestDto
    {
        [Range(1, int.MaxValue)]
        public int AdvertisementPlanId { get; set; }

        [Range(1, 365)]
        public int DurationDays { get; set; } = 1;

        [Range(0.01d, 9999999999999999d)]
        public decimal? TotalBudget { get; set; }

        [Range(0.01d, 9999999999999999d)]
        public decimal? DailyBudget { get; set; }
    }

    public class OwnerAdvertisementPriceQuoteDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AdvertisementPlanId { get; set; }
        public string BillingModel { get; set; } = string.Empty;
        public string BillingModelLabel { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public decimal? DailyAmount { get; set; }
        public long? EstimatedClicks { get; set; }
        public long? EstimatedImpressions { get; set; }
        public string FinalAmountText { get; set; } = string.Empty;
    }
}
