using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Admin
{
    /* ========================================================================
       ثوابت تبويبات الإدارة المالية
       ======================================================================== */

    public static class AdminFinancialTabs
    {
        // الواجهة الجديدة تعتمد خمسة تبويبات فقط.
        public const string Overview = "overview";
        public const string CommissionGuarantee = "commission-guarantee";
        public const string TopUps = "topups";
        public const string Transactions = "transactions";
        public const string Reports = "reports";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                Overview,
                CommissionGuarantee,
                TopUps,
                Transactions,
                Reports
            };
    }

    public static class AdminCommissionTopUpStatuses
    {
        public const string Pending = "Pending";
        public const string Completed = "Completed";
        public const string Rejected = "Rejected";
        public const string Reversed = "Reversed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                Pending,
                Completed,
                Rejected,
                Reversed,
                Failed,
                Cancelled
            };
    }

    public static class AdminCommissionAccountStatuses
    {
        public const string NeedsTopUp = "NeedsTopUp";
        public const string Active = "Active";
        public const string LowBalance = "LowBalance";
        public const string InDebt = "InDebt";
        public const string Suspended = "Suspended";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                NeedsTopUp,
                Active,
                LowBalance,
                InDebt,
                Suspended
            };
    }

    public static class AdminBookingCommissionStatuses
    {
        public const string Calculated = "Calculated";
        public const string Held = "Held";
        public const string Charged = "Charged";
        public const string Released = "Released";
        public const string Debt = "Debt";
        public const string NotApplicable = "NotApplicable";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                Calculated,
                Held,
                Charged,
                Released,
                Debt,
                NotApplicable
            };
    }

    public static class AdminFinanceTransactionTypes
    {
        public const string TopUp = "TopUp";
        public const string CommissionHold = "CommissionHold";
        public const string CommissionCharge = "CommissionCharge";
        public const string CommissionRelease = "CommissionRelease";
        public const string Debt = "Debt";
        public const string DebtPayment = "DebtPayment";
        public const string Adjustment = "Adjustment";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                TopUp,
                CommissionHold,
                CommissionCharge,
                CommissionRelease,
                Debt,
                DebtPayment,
                Adjustment
            };
    }

    public static class AdminFinanceTransactionStatuses
    {
        public const string Pending = "Pending";
        public const string Completed = "Completed";
        public const string Rejected = "Rejected";
        public const string Reversed = "Reversed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                Pending,
                Completed,
                Rejected,
                Reversed,
                Failed,
                Cancelled
            };
    }

    public static class AdminFinancialBookingStates
    {
        public const string Pending = "Pending";
        public const string AwaitingPayment = "AwaitingPayment";
        public const string PaymentReview = "PaymentReview";
        public const string Confirmed = "Confirmed";
        public const string Completed = "Completed";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";
        public const string Rejected = "Rejected";
        public const string NoShow = "NoShow";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                Pending,
                AwaitingPayment,
                PaymentReview,
                Confirmed,
                Completed,
                Expired,
                Cancelled,
                Rejected,
                NoShow
            };
    }

    public static class AdminFinancialDateScopes
    {
        public const string CreatedAt = "CreatedAt";
        public const string TransferDate = "TransferDate";
        public const string ReviewedAt = "ReviewedAt";
        public const string BookingCreatedAt = "BookingCreatedAt";
        public const string CommissionHeldAt = "CommissionHeldAt";
        public const string CommissionSettledAt = "CommissionSettledAt";
        public const string LastTopUpAt = "LastTopUpAt";
        public const string UpdatedAt = "UpdatedAt";
    }

    /* ========================================================================
       DTOs مشتركة
       ======================================================================== */

    public class AdminFinancialSelectOptionDto
    {
        public string Value { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? IconClass { get; set; }

        public bool IsSelected { get; set; }

        public bool IsDisabled { get; set; }

        public int? Count { get; set; }
    }

    public class AdminFinancialOwnerOptionDto
    {
        public int OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string? ContactInfo { get; set; }

        public int PlacesCount { get; set; }

        public bool IsSelected { get; set; }
    }

    public class AdminFinancialPlaceOptionDto
    {
        public int PlaceId { get; set; }

        public int? OwnerId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public string? PlaceImageUrl { get; set; }

        public string? CityName { get; set; }

        public string AccountStatus { get; set; } =
            AdminCommissionAccountStatuses.NeedsTopUp;

        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public bool IsSelected { get; set; }
    }

    public class AdminSupportedWalletOptionDto
    {
        public int WalletId { get; set; }

        public string WalletName { get; set; } = string.Empty;

        public string? LogoPath { get; set; }

        public bool IsActive { get; set; }

        public bool IsSelected { get; set; }
    }

    public class AdminPlatformWalletOptionDto
    {
        public int PlatformWalletId { get; set; }

        public int WalletId { get; set; }

        public string WalletName { get; set; } = string.Empty;

        public string? LogoPath { get; set; }

        public string AccountNumber { get; set; } = string.Empty;

        public string BeneficiaryName { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public bool IsPrimary { get; set; }

        public bool IsSelected { get; set; }
    }

    public class AdminFinancialPaginationDto
    {
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 25;

        public int TotalCount { get; set; }

        public int TotalPages =>
            TotalCount <= 0 || PageSize <= 0
                ? 0
                : (int)Math.Ceiling((double)TotalCount / PageSize);

        public bool HasPreviousPage =>
            PageNumber > 1;

        public bool HasNextPage =>
            TotalPages > 0 &&
            PageNumber < TotalPages;

        public int FirstItemNumber =>
            TotalCount <= 0
                ? 0
                : ((Math.Max(1, PageNumber) - 1) * Math.Max(1, PageSize)) + 1;

        public int LastItemNumber =>
            TotalCount <= 0
                ? 0
                : Math.Min(
                    Math.Max(1, PageNumber) * Math.Max(1, PageSize),
                    TotalCount);
    }

    public class AdminPagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();

        public AdminFinancialPaginationDto Pagination { get; set; } = new();
    }

    public class AdminFinancialBalanceSnapshotDto
    {
        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public decimal NetPosition =>
            AvailableBalance -
            DebtAmount;
    }

    public class AdminFinancialTabCounterDto
    {
        public string TabKey { get; set; } = string.Empty;

        public int Count { get; set; }

        public bool IsAttentionRequired { get; set; }
    }

    /* ========================================================================
       غلاف الإدارة المالية
       ======================================================================== */

    public class AdminFinancialShellDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public string SelectedTab { get; set; } =
            AdminFinancialTabs.Overview;

        public DateTime GeneratedAt { get; set; } =
            DateTime.Now;

        public int PendingTopUpsCount { get; set; }

        public int LowBalanceAccountsCount { get; set; }

        public int InDebtAccountsCount { get; set; }

        public int SuspendedAccountsCount { get; set; }

        public List<AdminFinancialTabCounterDto> TabCounters { get; set; } =
            new();
    }

    /// <summary>
    /// صلاحيات واجهة الإدارة المالية. يتم تعبئتها من الكنترولر اعتمادًا على
    /// صلاحيات موظف الإدارة، ولا تقوم الخدمة المالية بافتراضها.
    /// </summary>
    public class AdminFinancialAccessDto
    {
        public bool CanView { get; set; }

        public bool CanReviewTopUps { get; set; }

        public bool CanManageCommissionAccounts { get; set; }

        public bool CanManagePlatformWallets { get; set; }

        public bool CanManageSettings { get; set; }

        public bool CanViewReports { get; set; }

        public bool CanOpenManagement =>
            CanManagePlatformWallets || CanManageSettings;
    }

    public class AdminFinancialManagePageDto
    {
        public AdminFinancialShellDto Shell { get; set; } = new();

        public AdminFinancialAccessDto Access { get; set; } = new();
    }

    /* ========================================================================
       التبويب الأول: النظرة العامة
       ======================================================================== */

    public class AdminFinancialOverviewFilterDto
    {
        public int? OwnerId { get; set; }

        public int? PlaceId { get; set; }

        // Internal read scope; does not change stored financial amounts.
        public string? DisplayMarket { get; set; }

        public string? AccountStatus { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }
    }

    public class AdminFinancialOverviewPageDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public DateTime GeneratedAt { get; set; } =
            DateTime.Now;

        public AdminFinancialOverviewFilterDto Filter { get; set; } =
            new();

        public AdminFinancialOverviewSummaryDto Summary { get; set; } =
            new();

        public List<AdminFinancialOwnerOptionDto> Owners { get; set; } =
            new();

        public List<AdminFinancialPlaceOptionDto> Places { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> AccountStatusOptions { get; set; } =
            new();

        public List<AdminFinancialTrendPointDto> MonthlyTrend { get; set; } =
            new();

        public List<AdminFinancialStatusBreakdownDto> AccountStatusBreakdown { get; set; } =
            new();

        public List<AdminFinancialOverviewAlertDto> Alerts { get; set; } =
            new();

        public List<AdminRecentFinancialMovementDto> RecentTransactions { get; set; } =
            new();

        public List<AdminPendingTopUpPreviewDto> PendingTopUps { get; set; } =
            new();

        public List<AdminLowBalanceAccountPreviewDto> LowBalanceAccounts { get; set; } =
            new();
    }

    public class AdminFinancialOverviewSummaryDto
    {
        public decimal TotalCommissionCalculated { get; set; }

        public decimal TotalCommissionHeld { get; set; }

        public decimal TotalCommissionCharged { get; set; }

        public decimal TotalCommissionReleased { get; set; }

        public decimal TotalCommissionDebt { get; set; }

        public decimal TotalAvailableGuaranteeBalance { get; set; }

        public decimal TotalHeldGuaranteeBalance { get; set; }

        public decimal TotalCurrentDebt { get; set; }

        public decimal TotalApprovedTopUps { get; set; }

        public decimal CurrentMonthCommissionRevenue { get; set; }

        public decimal PreviousMonthCommissionRevenue { get; set; }

        public decimal CurrentMonthGrowthPercentage { get; set; }

        public int CommissionBookingsCount { get; set; }

        public int PendingTopUpsCount { get; set; }

        public int ActiveAccountsCount { get; set; }

        public int NeedsTopUpAccountsCount { get; set; }

        public int LowBalanceAccountsCount { get; set; }

        public int InDebtAccountsCount { get; set; }

        public int SuspendedAccountsCount { get; set; }

        public int FailedTransactionsCount { get; set; }

        public int UnsettledCommissionsCount { get; set; }

        public decimal AverageCommissionPerBooking =>
            CommissionBookingsCount <= 0
                ? 0m
                : TotalCommissionCalculated / CommissionBookingsCount;
    }

    public class AdminFinancialTrendPointDto
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public string Label { get; set; } = string.Empty;

        public decimal CalculatedCommission { get; set; }

        public decimal ChargedCommission { get; set; }

        public decimal ReleasedCommission { get; set; }

        public decimal ApprovedTopUps { get; set; }

        public int BookingsCount { get; set; }
    }

    public class AdminFinancialStatusBreakdownDto
    {
        public string Status { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }

        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public decimal Percentage { get; set; }
    }

    public class AdminFinancialOverviewAlertDto
    {
        public string AlertKey { get; set; } = string.Empty;

        public string Severity { get; set; } = "Info";

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int Count { get; set; }

        public decimal? Amount { get; set; }

        public string? TargetTab { get; set; }

        public string? TargetQuery { get; set; }

        public DateTime? OccurredAt { get; set; }
    }

    public class AdminRecentFinancialMovementDto
    {
        public int CommissionTransactionId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public int? BookingId { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public string TransactionTypeLabel { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Status { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public string? ReferenceNumber { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? CreatedByName { get; set; }
    }

    public class AdminPendingTopUpPreviewDto
    {
        public int CommissionTransactionId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? ReferenceNumber { get; set; }

        public DateTime? TransferDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool HasReceipt { get; set; }

        public bool HasDuplicateReference { get; set; }
    }

    public class AdminLowBalanceAccountPreviewDto
    {
        public int PlaceCommissionAccountId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public decimal AvailableBalance { get; set; }

        public decimal LowBalanceThreshold { get; set; }

        public decimal DebtAmount { get; set; }

        public string AccountStatus { get; set; } = string.Empty;

        public DateTime? LastTopUpAt { get; set; }

        public decimal ShortageAmount =>
            Math.Max(
                0m,
                LowBalanceThreshold -
                AvailableBalance);
    }

    /* ========================================================================
       التبويب الثاني: طلبات شحن رصيد الضمان
       ======================================================================== */

    public class AdminCommissionTopUpFilterDto
    {
        [Range(1, int.MaxValue)]
        public int? RequestId { get; set; }

        public int? OwnerId { get; set; }

        public int? PlaceId { get; set; }

        // Internal read scope; does not change stored financial amounts.
        public string? DisplayMarket { get; set; }

        public int? PlatformWalletId { get; set; }

        public string? Status { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        public string DateScope { get; set; } =
            AdminFinancialDateScopes.CreatedAt;

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        [StringLength(120)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(10, 50)]
        public int PageSize { get; set; } = 25;
    }

    public class AdminCommissionTopUpPageDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public decimal MinimumTopUpAmount { get; set; }

        public AdminCommissionTopUpFilterDto Filter { get; set; } =
            new();

        public AdminCommissionTopUpSummaryDto Summary { get; set; } =
            new();

        public AdminPagedResultDto<AdminCommissionTopUpRowDto> Result { get; set; } =
            new();

        public List<AdminFinancialOwnerOptionDto> Owners { get; set; } =
            new();

        public List<AdminFinancialPlaceOptionDto> Places { get; set; } =
            new();

        public List<AdminPlatformWalletOptionDto> PlatformWallets { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> StatusOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> DateScopeOptions { get; set; } =
            new();
    }

    public class AdminCommissionTopUpSummaryDto
    {
        public int TotalCount { get; set; }

        public int PendingCount { get; set; }

        public int CompletedCount { get; set; }

        public int RejectedCount { get; set; }

        public int CancelledCount { get; set; }

        public int FailedCount { get; set; }

        public int TodayCount { get; set; }

        public int DuplicateReferenceCount { get; set; }

        public decimal PendingAmount { get; set; }

        public decimal CompletedAmount { get; set; }

        public decimal RejectedAmount { get; set; }

        public decimal DebtPaidAmount { get; set; }

        public decimal AddedToAvailableBalanceAmount { get; set; }
    }

    public class AdminCommissionTopUpRowDto
    {
        public int CommissionTransactionId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string? PlaceImageUrl { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public string? OwnerPhoneNumber { get; set; }

        public int? PlatformWalletId { get; set; }

        public string PlatformWalletName { get; set; } = string.Empty;

        public string PlatformAccountNumber { get; set; } = string.Empty;

        public string PlatformBeneficiaryName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal DebtBefore { get; set; }

        public decimal DebtAfter { get; set; }

        public decimal AvailableBalanceBefore { get; set; }

        public decimal AvailableBalanceAfter { get; set; }

        public decimal HeldBalanceBefore { get; set; }

        public decimal HeldBalanceAfter { get; set; }

        public decimal DebtPaymentAmount { get; set; }

        public decimal CreditedToAvailableBalance { get; set; }

        public string Status { get; set; } =
            AdminCommissionTopUpStatuses.Pending;

        public string StatusLabel { get; set; } = string.Empty;

        public string? ReferenceNumber { get; set; }

        public DateTime? TransferDate { get; set; }

        public string? ReceiptImage { get; set; }

        public bool HasReceipt =>
            !string.IsNullOrWhiteSpace(ReceiptImage);

        public DateTime CreatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public string? CreatedByName { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedByUserId { get; set; }

        public string? ReviewedByName { get; set; }

        public string? RejectionReason { get; set; }

        public string? Notes { get; set; }

        public int DuplicateReferenceCount { get; set; }

        public bool HasDuplicateReference =>
            DuplicateReferenceCount > 1;

        public bool CanApprove =>
            string.Equals(
                Status,
                AdminCommissionTopUpStatuses.Pending,
                StringComparison.OrdinalIgnoreCase);

        public bool CanReject =>
            CanApprove;
    }

    public class AdminCommissionTopUpDetailsDto :
        AdminCommissionTopUpRowDto
    {
        public string? PlaceStatus { get; set; }

        public bool PlaceIsAvailable { get; set; }

        public string AccountStatus { get; set; } = string.Empty;

        public decimal CurrentAvailableBalance { get; set; }

        public decimal CurrentHeldBalance { get; set; }

        public decimal CurrentDebtAmount { get; set; }

        public decimal EffectiveMinimumTopUpAmount { get; set; }

        public List<AdminRecentFinancialMovementDto> RelatedTransactions { get; set; } =
            new();
    }

    public class AdminApproveTopUpRequestDto
    {
        [Range(1, int.MaxValue)]
        public int CommissionTransactionId { get; set; }

        [StringLength(500)]
        public string? ReviewNote { get; set; }
    }

    public class AdminRejectTopUpRequestDto
    {
        [Range(1, int.MaxValue)]
        public int CommissionTransactionId { get; set; }

        [Required(ErrorMessage = "اكتب سبب رفض طلب الشحن.")]
        [StringLength(
            500,
            MinimumLength = 3,
            ErrorMessage = "سبب الرفض يجب أن يكون بين 3 و500 حرف.")]
        public string RejectionReason { get; set; } = string.Empty;
    }

    /* ========================================================================
       التبويب الثاني الجديد: العمولات والضمان
       يجمع حساب الضمان مع ملخص عمولات الحجوزات دون تكرار منطق الحساب.
       ======================================================================== */

    public class AdminCommissionGuaranteeFilterDto
    {
        public int? OwnerId { get; set; }

        public int? PlaceId { get; set; }

        // Internal read scope; does not change stored financial amounts.
        public string? DisplayMarket { get; set; }

        public string? AccountStatus { get; set; }

        public bool? HasDebt { get; set; }

        public bool? IsLowBalance { get; set; }

        public bool? IsBookingSuspended { get; set; }

        [StringLength(120)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(10, 50)]
        public int PageSize { get; set; } = 25;
    }

    public class AdminCommissionGuaranteePageDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public AdminCommissionGuaranteeFilterDto Filter { get; set; } =
            new();

        /// <summary>
        /// قائمة حسابات الضمان وهي القائمة الرئيسية في التبويب.
        /// </summary>
        public AdminCommissionAccountPageDto Accounts { get; set; } =
            new();

        /// <summary>
        /// ملخص عمولات الحجوزات المطابقة للمالك/المنشأة/البحث الحالي.
        /// </summary>
        public AdminBookingCommissionSummaryDto CommissionSummary { get; set; } =
            new();

        /// <summary>
        /// آخر عمولات مرتبطة بالفلاتر الحالية لعرضها بشكل مختصر عند الحاجة.
        /// </summary>
        public List<AdminBookingCommissionRowDto> RecentCommissions { get; set; } =
            new();
    }

    public class AdminCommissionGuaranteeDetailsDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public AdminCommissionAccountDetailsDto Account { get; set; } =
            new();

        public AdminBookingCommissionSummaryDto CommissionSummary { get; set; } =
            new();

        public List<AdminBookingCommissionRowDto> RecentCommissions { get; set; } =
            new();
    }

    /* ========================================================================
       مكونات داخلية للتبويب الثاني: حسابات ضمان العمولات
       ======================================================================== */

    public class AdminCommissionAccountFilterDto
    {
        public int? OwnerId { get; set; }

        public int? PlaceId { get; set; }

        // Internal read scope; does not change stored financial amounts.
        public string? DisplayMarket { get; set; }

        public string? AccountStatus { get; set; }

        public bool? AllowNegativeBalance { get; set; }

        public bool? HasDebt { get; set; }

        public bool? IsLowBalance { get; set; }

        public bool? IsBookingSuspended { get; set; }

        [StringLength(120)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(10, 50)]
        public int PageSize { get; set; } = 25;
    }

    public class AdminCommissionAccountPageDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public decimal GlobalMinimumTopUpAmount { get; set; }

        public decimal GlobalLowBalanceThreshold { get; set; }

        public bool GlobalAllowNegativeBalance { get; set; }

        public decimal GlobalDebtLimit { get; set; }

        public int GlobalDebtGraceDays { get; set; }

        public bool SuspendBookingWhenBalanceInsufficient { get; set; }

        public bool HidePlaceWhenCommissionSuspended { get; set; }

        public AdminCommissionAccountFilterDto Filter { get; set; } =
            new();

        public AdminCommissionAccountSummaryDto Summary { get; set; } =
            new();

        public AdminPagedResultDto<AdminCommissionAccountRowDto> Result { get; set; } =
            new();

        public List<AdminFinancialOwnerOptionDto> Owners { get; set; } =
            new();

        public List<AdminFinancialPlaceOptionDto> Places { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> AccountStatusOptions { get; set; } =
            new();
    }

    public class AdminCommissionAccountSummaryDto
    {
        public int TotalCount { get; set; }

        public int NeedsTopUpCount { get; set; }

        public int ActiveCount { get; set; }

        public int LowBalanceCount { get; set; }

        public int InDebtCount { get; set; }

        public int SuspendedCount { get; set; }

        public int AllowNegativeCount { get; set; }

        public decimal TotalAvailableBalance { get; set; }

        public decimal TotalHeldBalance { get; set; }

        public decimal TotalDebtAmount { get; set; }

        public decimal NetGuaranteePosition =>
            TotalAvailableBalance -
            TotalDebtAmount;
    }

    public class AdminCommissionAccountRowDto
    {
        public int PlaceCommissionAccountId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string? PlaceImageUrl { get; set; }

        public string? PlaceStatus { get; set; }

        public bool PlaceIsAvailable { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public string? OwnerPhoneNumber { get; set; }

        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public decimal TotalAccountBalance =>
            AvailableBalance +
            HeldBalance -
            DebtAmount;

        public bool? AllowNegativeBalanceOverride { get; set; }

        public decimal? DebtLimitOverride { get; set; }

        public decimal? LowBalanceThresholdOverride { get; set; }

        public bool EffectiveAllowNegativeBalance { get; set; }

        public decimal EffectiveDebtLimit { get; set; }

        public decimal EffectiveLowBalanceThreshold { get; set; }

        public decimal EffectiveMinimumTopUpAmount { get; set; }

        public int EffectiveDebtGraceDays { get; set; }

        public string AccountStatus { get; set; } =
            AdminCommissionAccountStatuses.NeedsTopUp;

        public bool IsBookingSuspended { get; set; }

        public bool IsPlaceHiddenByFinancialPolicy { get; set; }

        public DateTime? LastTopUpAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastTransactionAt { get; set; }

        public string? LastTransactionType { get; set; }

        public decimal? LastTransactionAmount { get; set; }

        public decimal LowBalanceShortage =>
            Math.Max(
                0m,
                EffectiveLowBalanceThreshold -
                AvailableBalance);

        public decimal RemainingDebtCapacity =>
            EffectiveAllowNegativeBalance
                ? Math.Max(
                    0m,
                    EffectiveDebtLimit -
                    DebtAmount)
                : 0m;
    }

    public class AdminCommissionAccountDetailsDto :
        AdminCommissionAccountRowDto
    {
        public decimal TotalTopUps { get; set; }

        public decimal TotalCommissionHeld { get; set; }

        public decimal TotalCommissionCharged { get; set; }

        public decimal TotalCommissionReleased { get; set; }

        public decimal TotalDebtRegistered { get; set; }

        public decimal TotalDebtPaid { get; set; }

        public int RelatedBookingsCount { get; set; }

        public int TransactionsCount { get; set; }

        public List<AdminRecentFinancialMovementDto> RecentTransactions { get; set; } =
            new();
    }

    public class AdminCommissionAccountPolicyUpdateDto
    {
        [Range(1, int.MaxValue)]
        public int PlaceCommissionAccountId { get; set; }

        [Range(1, int.MaxValue)]
        public int PlaceId { get; set; }

        public bool UseGlobalAllowNegativeBalance { get; set; } = true;

        public bool? AllowNegativeBalance { get; set; }

        public bool UseGlobalDebtLimit { get; set; } = true;

        [Range(
            0,
            9999999999999999d,
            ErrorMessage = "حد الدين يجب ألا يكون سالبًا.")]
        public decimal? DebtLimit { get; set; }

        public bool UseGlobalLowBalanceThreshold { get; set; } = true;

        [Range(
            0,
            9999999999999999d,
            ErrorMessage = "حد انخفاض الرصيد يجب ألا يكون سالبًا.")]
        public decimal? LowBalanceThreshold { get; set; }

        [Required(ErrorMessage = "اكتب سبب تعديل سياسة الحساب.")]
        [StringLength(
            500,
            MinimumLength = 3,
            ErrorMessage = "سبب التعديل يجب أن يكون بين 3 و500 حرف.")]
        public string Reason { get; set; } = string.Empty;
    }

    public class AdminCommissionAccountStatusUpdateDto
    {
        [Range(1, int.MaxValue)]
        public int PlaceCommissionAccountId { get; set; }

        [Range(1, int.MaxValue)]
        public int PlaceId { get; set; }

        [Required]
        [StringLength(30)]
        public string TargetStatus { get; set; } =
            AdminCommissionAccountStatuses.Active;

        [Required(ErrorMessage = "اكتب سبب تغيير حالة الحساب.")]
        [StringLength(
            500,
            MinimumLength = 3,
            ErrorMessage = "السبب يجب أن يكون بين 3 و500 حرف.")]
        public string Reason { get; set; } = string.Empty;
    }

    /* ========================================================================
       مكوّن داخلي للتبويب الثاني: عمولات الحجوزات
       ======================================================================== */

    public class AdminBookingCommissionFilterDto
    {
        [Range(1, int.MaxValue)]
        public int? BookingId { get; set; }

        public int? CustomerId { get; set; }

        public int? OwnerId { get; set; }

        public int? PlaceId { get; set; }

        // Internal read scope; does not change stored financial amounts.
        public string? DisplayMarket { get; set; }

        public string? BookingState { get; set; }

        public string? PaymentStatus { get; set; }

        public string? CommissionStatus { get; set; }

        public bool? HasDebt { get; set; }

        public string DateScope { get; set; } =
            AdminFinancialDateScopes.BookingCreatedAt;

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        [StringLength(120)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(10, 50)]
        public int PageSize { get; set; } = 25;
    }

    public class AdminBookingCommissionPageDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public AdminBookingCommissionFilterDto Filter { get; set; } =
            new();

        public AdminBookingCommissionSummaryDto Summary { get; set; } =
            new();

        public AdminPagedResultDto<AdminBookingCommissionRowDto> Result { get; set; } =
            new();

        public List<AdminFinancialOwnerOptionDto> Owners { get; set; } =
            new();

        public List<AdminFinancialPlaceOptionDto> Places { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> BookingStateOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> PaymentStatusOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> CommissionStatusOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> DateScopeOptions { get; set; } =
            new();
    }

    public class AdminBookingCommissionSummaryDto
    {
        public int TotalCount { get; set; }

        public int CalculatedCount { get; set; }

        public int HeldCount { get; set; }

        public int ChargedCount { get; set; }

        public int ReleasedCount { get; set; }

        public int DebtCount { get; set; }

        public int NotApplicableCount { get; set; }

        public int NeedsReviewCount { get; set; }

        public decimal TotalBookingValue { get; set; }

        public decimal TotalCommissionBaseAmount { get; set; }

        public decimal TotalCommissionCalculated { get; set; }

        public decimal TotalCommissionHeld { get; set; }

        public decimal TotalCommissionCharged { get; set; }

        public decimal TotalCommissionReleased { get; set; }

        public decimal TotalCommissionDebt { get; set; }

        public decimal AverageCommission =>
            TotalCount <= 0
                ? 0m
                : TotalCommissionCalculated / TotalCount;
    }

    public class AdminBookingCommissionRowDto
    {
        public decimal EffectiveBookingValue { get; set; }
        public int RecordId { get; set; }

        public int BookingId { get; set; }

        public int? CustomerId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string? CustomerPhoneNumber { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public string? PlaceImageUrl { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public decimal BookingTotalPrice { get; set; }

        public decimal CommissionRateSnapshot { get; set; }

        public decimal CommissionBaseAmount { get; set; }

        public decimal PlatformCommission { get; set; }

        public decimal HeldAmount { get; set; }

        public decimal ChargedAmount { get; set; }

        public decimal ReleasedAmount { get; set; }

        public decimal DebtAmount { get; set; }

        public string BookingState { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public string CommissionPaymentStatus { get; set; } = string.Empty;

        public string CommissionStatus { get; set; } =
            AdminBookingCommissionStatuses.Calculated;

        public DateTime? BookingCreatedAt { get; set; }

        public DateTime? CommissionHeldAt { get; set; }

        public DateTime? CommissionChargedAt { get; set; }

        public DateTime? CommissionReleasedAt { get; set; }

        public DateTime? SettledAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool HasDebt =>
            DebtAmount > 0m ||
            string.Equals(
                CommissionStatus,
                AdminBookingCommissionStatuses.Debt,
                StringComparison.OrdinalIgnoreCase);

        public bool NeedsReview { get; set; }
    }

    public class AdminBookingCommissionDetailsDto :
        AdminBookingCommissionRowDto
    {
        public DateTime BookingStartDate { get; set; }

        public DateTime? BookingEndDate { get; set; }

        public string? PaymentMethod { get; set; }

        public decimal DepositAmount { get; set; }

        public decimal AmountPaidOnline { get; set; }

        public decimal AmountCollectedAtProperty { get; set; }

        public decimal AmountToPayAtProperty { get; set; }

        public bool IsPaymentConfirmed { get; set; }

        public string? TransferReferenceNumber { get; set; }

        public List<AdminBookingCommissionTimelineItemDto> Timeline { get; set; } =
            new();

        public List<AdminFinancialTransactionRowDto> Transactions { get; set; } =
            new();
    }

    public class AdminBookingCommissionTimelineItemDto
    {
        public string Status { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public DateTime? OccurredAt { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsCurrent { get; set; }
    }

    /* ========================================================================
       التبويب الرابع: كشف الحركات المالية
       ======================================================================== */

    public class AdminFinancialTransactionFilterDto
    {
        [Range(1, int.MaxValue)]
        public int? TransactionId { get; set; }

        public int? OwnerId { get; set; }

        public int? PlaceId { get; set; }

        // Internal read scope; does not change stored financial amounts.
        public string? DisplayMarket { get; set; }

        [Range(1, int.MaxValue)]
        public int? BookingId { get; set; }

        [Range(1, int.MaxValue)]
        public int? TopUpRequestId { get; set; }

        public int? PlatformWalletId { get; set; }

        public string? TransactionType { get; set; }

        public string? Status { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [Range(1, int.MaxValue)]
        public int? CreatedByUserId { get; set; }

        public string DateScope { get; set; } =
            AdminFinancialDateScopes.CreatedAt;

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        [StringLength(120)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(10, 50)]
        public int PageSize { get; set; } = 25;
    }

    public class AdminFinancialTransactionPageDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public AdminFinancialTransactionFilterDto Filter { get; set; } =
            new();

        public AdminFinancialTransactionSummaryDto Summary { get; set; } =
            new();

        public AdminPagedResultDto<AdminFinancialTransactionRowDto> Result { get; set; } =
            new();

        public List<AdminFinancialOwnerOptionDto> Owners { get; set; } =
            new();

        public List<AdminFinancialPlaceOptionDto> Places { get; set; } =
            new();

        public List<AdminPlatformWalletOptionDto> PlatformWallets { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> TransactionTypeOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> StatusOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> DateScopeOptions { get; set; } =
            new();
    }

    public class AdminFinancialTransactionSummaryDto
    {
        public int TotalCount { get; set; }

        public int CompletedCount { get; set; }

        public int PendingCount { get; set; }

        public int FailedCount { get; set; }

        public int RejectedCount { get; set; }

        public decimal TotalTopUps { get; set; }

        public decimal TotalCommissionHeld { get; set; }

        public decimal TotalCommissionCharged { get; set; }

        public decimal TotalCommissionReleased { get; set; }

        public decimal TotalDebtRegistered { get; set; }

        public decimal TotalDebtPaid { get; set; }

        public decimal TotalAdjustments { get; set; }
    }

    public class AdminFinancialTransactionRowDto
    {
        public int CommissionTransactionId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int? BookingId { get; set; }

        public int? PlatformWalletId { get; set; }

        public string? PlatformWalletName { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public string TransactionTypeLabel { get; set; } = string.Empty;

        public string Direction { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal AvailableBalanceBefore { get; set; }

        public decimal AvailableBalanceAfter { get; set; }

        public decimal HeldBalanceBefore { get; set; }

        public decimal HeldBalanceAfter { get; set; }

        public decimal DebtBefore { get; set; }

        public decimal DebtAfter { get; set; }

        public string Status { get; set; } = string.Empty;

        public string StatusLabel { get; set; } = string.Empty;

        public string? ReferenceNumber { get; set; }

        public DateTime? TransferDate { get; set; }

        public string? ReceiptImage { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedByUserId { get; set; }

        public string? ReviewedByName { get; set; }

        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public string? CreatedByName { get; set; }

        public string? Notes { get; set; }

        public decimal AvailableBalanceChange =>
            AvailableBalanceAfter -
            AvailableBalanceBefore;

        public decimal HeldBalanceChange =>
            HeldBalanceAfter -
            HeldBalanceBefore;

        public decimal DebtChange =>
            DebtAfter -
            DebtBefore;
    }

    public class AdminFinancialTransactionDetailsDto :
        AdminFinancialTransactionRowDto
    {
        public string? PlaceStatus { get; set; }

        public string? BookingState { get; set; }

        public string? BookingPaymentStatus { get; set; }

        public string? CommissionStatus { get; set; }

        public string? PlatformAccountNumber { get; set; }

        public string? PlatformBeneficiaryName { get; set; }
    }

    /* ========================================================================
       إدارة المالية (Drawer خارج التبويبات)
       ======================================================================== */

    public class AdminFinancialManagementPageDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public DateTime LoadedAt { get; set; } = DateTime.Now;

        public AdminFinancialSettingsPageDto Settings { get; set; } =
            new();

        public AdminPlatformWalletPageDto PlatformWallets { get; set; } =
            new();
    }

    /* ========================================================================
       مكوّن إدارة المالية: حسابات المنصة
       ======================================================================== */

    public class AdminPlatformWalletFilterDto
    {
        public int? WalletId { get; set; }

        public bool? IsActive { get; set; }

        public bool? IsPrimary { get; set; }

        [StringLength(120)]
        public string? Search { get; set; }
    }

    public class AdminPlatformWalletPageDto
    {
        public AdminPlatformWalletFilterDto Filter { get; set; } =
            new();

        public AdminPlatformWalletSummaryDto Summary { get; set; } =
            new();

        public List<AdminPlatformWalletRowDto> Wallets { get; set; } =
            new();

        public List<AdminSupportedWalletOptionDto> SupportedWallets { get; set; } =
            new();
    }

    public class AdminPlatformWalletSummaryDto
    {
        public int TotalCount { get; set; }

        public int ActiveCount { get; set; }

        public int InactiveCount { get; set; }

        public int PrimaryCount { get; set; }

        public int UsedWalletsCount { get; set; }

        public int PendingTopUpsCount { get; set; }

        public decimal PendingTopUpsAmount { get; set; }

        public decimal CompletedTopUpsAmount { get; set; }
    }

    public class AdminPlatformWalletRowDto
    {
        public int PlatformWalletId { get; set; }

        public int WalletId { get; set; }

        public string WalletName { get; set; } = string.Empty;

        public string? LogoPath { get; set; }

        public string AccountNumber { get; set; } = string.Empty;

        public string BeneficiaryName { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public bool IsPrimary { get; set; }

        public string? Instructions { get; set; }

        public string? InstructionsEn { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int TotalTopUpRequestsCount { get; set; }

        public int PendingTopUpRequestsCount { get; set; }

        public decimal CompletedTopUpAmount { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public bool CanDisable { get; set; } = true;

        public bool CanSetPrimary =>
            IsActive &&
            !IsPrimary;
    }

    public class AdminPlatformWalletEditDto
    {
        public int? PlatformWalletId { get; set; }

        [Required(ErrorMessage = "اختر شبكة الدفع أو البنك.")]
        [Range(1, int.MaxValue)]
        public int WalletId { get; set; }

        [Required(ErrorMessage = "اكتب رقم الحساب.")]
        [StringLength(
            30,
            MinimumLength = 6,
            ErrorMessage = "رقم الحساب يجب أن يتكون من 6 إلى 30 رقمًا.")]
        [RegularExpression(
            @"^[0-9٠-٩۰-۹]+$",
            ErrorMessage = "رقم الحساب يجب أن يحتوي على أرقام فقط دون حروف أو رموز.")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "اكتب اسم المستفيد.")]
        [StringLength(
            150,
            MinimumLength = 2,
            ErrorMessage = "اسم المستفيد يجب أن يكون بين 2 و150 حرف.")]
        public string BeneficiaryName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsPrimary { get; set; }

        [StringLength(2000)]
        public string? Instructions { get; set; }

        [StringLength(2000)]
        public string? InstructionsEn { get; set; }
    }

    public class AdminPlatformWalletStatusRequestDto
    {
        [Range(1, int.MaxValue)]
        public int PlatformWalletId { get; set; }

        public bool IsActive { get; set; }
    }

    public class AdminSetPrimaryPlatformWalletRequestDto
    {
        [Range(1, int.MaxValue)]
        public int PlatformWalletId { get; set; }
    }

    /* ========================================================================
       مكوّن إدارة المالية: الإعدادات المالية
       ======================================================================== */
    public class AdminCategoryCommissionDto
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; } =
            string.Empty;

        public string? IconClass { get; set; }

        public string? ThemeColor { get; set; }

        public string? ImagePath { get; set; }

        public bool HasBookingSystem { get; set; }

        public int PlacesCount { get; set; }

        /// <summary>
        /// النسبة الخاصة بالفئة.
        /// NULL يعني استخدام النسبة العامة.
        /// </summary>
        public decimal? CommissionPercentage { get; set; }

        /// <summary>
        /// النسبة التي ستُستخدم فعليًا بعد تطبيق النسبة العامة عند الحاجة.
        /// </summary>
        public decimal EffectiveCommissionPercentage { get; set; }

        public bool UsesDefaultCommission =>
            !CommissionPercentage.HasValue;
    }

    public class AdminCategoryCommissionUpdateDto
    {
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "رقم الفئة غير صحيح.")]
        public int CategoryId { get; set; }

        /// <summary>
        /// اتركها NULL لاستخدام النسبة العامة.
        /// </summary>
        [Range(
            0,
            100,
            ErrorMessage = "نسبة عمولة الفئة يجب أن تكون بين 0 و100.")]
        public decimal? CommissionPercentage { get; set; }
    }
    public class AdminFinancialSettingsPageDto
    {
        public AdminFinancialSettingsDto Settings { get; set; } =
            new();

        public string PlatformDisplayName { get; set; } =
            "دليلك اليمني";
        public List<AdminCategoryCommissionDto> CategoryCommissions { get; set; } =
    new();
        public string? PlatformLogoPath { get; set; }

        public string? ContactEmail { get; set; }

        public string? SupportPhone { get; set; }

        public DateTime LoadedAt { get; set; } =
            DateTime.Now;
    }

    public class AdminFinancialSettingsDto
    {
        public int Id { get; set; }

        public decimal CommissionPercentage { get; set; }

        public string DefaultCurrency { get; set; } =
            "ريال يمني";

        public decimal MinimumCommissionTopUp { get; set; }

        public decimal DefaultLowBalanceThreshold { get; set; }

        public bool AllowNegativeCommissionBalance { get; set; }

        public decimal DefaultCommissionDebtLimit { get; set; }

        public int CommissionDebtGraceDays { get; set; }

        public bool SuspendBookingWhenBalanceInsufficient { get; set; }

        public bool HidePlaceWhenCommissionSuspended { get; set; }

        // Null in an older form means preserve the saved platform policy.
        public bool? ReturnCommissionOnCancellation { get; set; }
        public bool OwnerFreeTrialEnabled { get; set; }

        public int OwnerFreeTrialDays { get; set; }

        public decimal RequiredCommissionGuaranteeBalance { get; set; }
    }

    public class AdminFinancialSettingsUpdateDto
    {
        // Each management tab posts only its own section. "all" keeps
        // compatibility with the original full settings form.
        [StringLength(30)]
        public string SettingsSection { get; set; } = "all";

        [Range(
            0,
            100,
            ErrorMessage = "نسبة العمولة يجب أن تكون بين 0 و100.")]
        public decimal CommissionPercentage { get; set; }

        [Required(ErrorMessage = "اكتب العملة الافتراضية.")]
        [StringLength(50)]
        public string DefaultCurrency { get; set; } =
            "ريال يمني";

        [Range(
            1,
            9999999999999999d,
            ErrorMessage = "الحد الأدنى للشحن يجب أن يكون أكبر من صفر.")]
        public decimal MinimumCommissionTopUp { get; set; } =
            20000m;

        [Range(
            0,
            9999999999999999d,
            ErrorMessage = "حد انخفاض الرصيد يجب ألا يكون سالبًا.")]
        public decimal DefaultLowBalanceThreshold { get; set; } =
            2000m;

        public bool AllowNegativeCommissionBalance { get; set; }

        [Range(
            0,
            9999999999999999d,
            ErrorMessage = "حد الدين يجب ألا يكون سالبًا.")]
        public decimal DefaultCommissionDebtLimit { get; set; }

        [Range(
            0,
            3650,
            ErrorMessage = "مهلة سداد الدين يجب أن تكون بين 0 و3650 يومًا.")]
        public int CommissionDebtGraceDays { get; set; } =
            3;

        public bool SuspendBookingWhenBalanceInsufficient { get; set; } =
            true;

        public bool HidePlaceWhenCommissionSuspended { get; set; }

        // Null in an older form means preserve the saved platform policy.
        public bool? ReturnCommissionOnCancellation { get; set; }
        public bool OwnerFreeTrialEnabled { get; set; }

        [Range(
            0,
            3650,
            ErrorMessage = "مدة الفترة التجريبية يجب أن تكون بين 0 و3650 يومًا.")]
        public int OwnerFreeTrialDays { get; set; } =
            14;

        [Range(
            0,
            9999999999999999d,
            ErrorMessage = "رصيد ضمان التفعيل لا يمكن أن يكون سالبًا.")]
        public decimal RequiredCommissionGuaranteeBalance { get; set; }

        [Required(ErrorMessage = "اكتب سبب تعديل الإعدادات المالية.")]
        [StringLength(
            500,
            MinimumLength = 3,
            ErrorMessage = "سبب التعديل يجب أن يكون بين 3 و500 حرف.")]
        public string ChangeReason { get; set; } = string.Empty;
        public List<AdminCategoryCommissionUpdateDto> CategoryCommissions { get; set; } =
    new();
    }
}
