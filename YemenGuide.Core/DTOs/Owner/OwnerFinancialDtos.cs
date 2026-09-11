using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YemenGuide.Core.Constants;

namespace YemenGuide.Core.DTOs.Owner
{
    /* =====================================================================
       ثوابت أنواع الحركات المالية
       ===================================================================== */

    /* =====================================================================
       توافق مؤقت مع الأسماء القديمة
       المصدر الحقيقي للقيم أصبح FinancialConstants.cs
       ===================================================================== */
    public static class OwnerFinanceTransactionTypes
    {
        public const string TopUp = FinancialTransactionTypes.TopUp;
        public const string CommissionHold = FinancialTransactionTypes.CommissionHold;
        public const string CommissionCharge = FinancialTransactionTypes.CommissionCharge;
        public const string CommissionRelease = FinancialTransactionTypes.CommissionRelease;
        public const string Debt = FinancialTransactionTypes.Debt;
        public const string DebtPayment = FinancialTransactionTypes.DebtPayment;
        public const string Adjustment = FinancialTransactionTypes.Adjustment;
        public static IReadOnlyCollection<string> All => FinancialTransactionTypes.All;
    }
    public static class OwnerFinanceTransactionStatuses
    {
        public const string Pending = FinancialTransactionStatuses.Pending;
        public const string Completed = FinancialTransactionStatuses.Completed;
        public const string Rejected = FinancialTransactionStatuses.Rejected;
        public const string Reversed = FinancialTransactionStatuses.Reversed;
        public const string Failed = FinancialTransactionStatuses.Failed;
        public const string Cancelled = FinancialTransactionStatuses.Cancelled;
        public static IReadOnlyCollection<string> All => FinancialTransactionStatuses.All;
    }
    public static class OwnerFinanceTransactionDirections
    {
        public const string Credit = FinancialTransactionDirections.Credit;
        public const string Debit = FinancialTransactionDirections.Debit;
        public const string DebtIncrease = FinancialTransactionDirections.DebtIncrease;
        public const string DebtDecrease = FinancialTransactionDirections.DebtDecrease;
        public const string Neutral = FinancialTransactionDirections.Neutral;
    }
    public static class OwnerCommissionAccountStatuses
    {
        public const string NeedsTopUp = FinancialAccountStatuses.NeedsTopUp;
        public const string Active = FinancialAccountStatuses.Active;
        public const string LowBalance = FinancialAccountStatuses.LowBalance;
        public const string InDebt = FinancialAccountStatuses.InDebt;
        public const string Suspended = FinancialAccountStatuses.Suspended;
        public static IReadOnlyCollection<string> All => FinancialAccountStatuses.All;
    }
    public static class OwnerBookingCommissionStatuses
    {
        public const string Calculated = FinancialCommissionStatuses.Calculated;
        public const string Held = FinancialCommissionStatuses.Held;
        public const string Charged = FinancialCommissionStatuses.Charged;
        public const string Released = FinancialCommissionStatuses.Released;
        public const string Debt = FinancialCommissionStatuses.Debt;
        public const string NotApplicable = FinancialCommissionStatuses.NotApplicable;
        public static IReadOnlyCollection<string> All => FinancialCommissionStatuses.All;
    }
    public static class OwnerRefundStatuses
    {
        public const string Pending = FinancialRefundStatuses.Pending;
        public const string Processing = FinancialRefundStatuses.Processing;
        public const string Completed = FinancialRefundStatuses.Completed;
        public const string Rejected = FinancialRefundStatuses.Rejected;
        public const string Failed = FinancialRefundStatuses.Failed;
        public const string Cancelled = FinancialRefundStatuses.Cancelled;
        public static IReadOnlyCollection<string> All => FinancialRefundStatuses.All;
    }

    /* =====================================================================
       الصفحة الرئيسية للمالية
       ===================================================================== */

    public class OwnerFinancialPageDto
    {
        public string Currency { get; set; } =
            "ريال يمني";

        public int? SelectedPlaceId { get; set; }

        public string SelectedPlaceName { get; set; } =
            "كل المنشآت";

        public List<OwnerFinancialPlaceOptionDto> Places { get; set; } =
            new List<OwnerFinancialPlaceOptionDto>();

        public int PendingTopUpsCount { get; set; }

        public int PendingRefundsCount { get; set; }

        public int LowBalanceAccountsCount { get; set; }

        public int SuspendedAccountsCount { get; set; }

        public bool HasPlaces =>
            Places.Count > 0;
    }

    /* =====================================================================
       عنصر منشأة في قائمة اختيار المنشآت
       ===================================================================== */

    public class OwnerFinancialPlaceOptionDto
    {
        public int PlaceId { get; set; }

        public string PlaceName { get; set; } =
            string.Empty;

        public string? CoverImage { get; set; }

        public string AccountStatus { get; set; } =
            OwnerCommissionAccountStatuses.NeedsTopUp;

        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public bool IsSelected { get; set; }

        public bool CanReceiveBookings =>
            !string.Equals(
                AccountStatus,
                OwnerCommissionAccountStatuses.Suspended,
                StringComparison.OrdinalIgnoreCase);
    }

    /* =====================================================================
       التبويب الأول: النظرة العامة
       ===================================================================== */

    public class FinancialOverviewTabDto
    {
        public string Currency { get; set; } =
            "ريال يمني";

        public int? SelectedPlaceId { get; set; }

        public string SelectedPlaceName { get; set; } =
            "كل المنشآت";

        public DateTime PeriodStart { get; set; } =
            new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1);

        public DateTime PeriodEnd { get; set; } =
            DateTime.Today;

        public FinancialOverviewDto Summary { get; set; } =
            new FinancialOverviewDto();

        public List<PlaceCommissionAccountDto> Accounts { get; set; } =
            new List<PlaceCommissionAccountDto>();

        public List<CommissionTransactionItemDto> RecentTransactions
        {
            get;
            set;
        } = new List<CommissionTransactionItemDto>();
    }

    public class FinancialOverviewDto
    {
        public int TotalBookingsCount { get; set; }

        public int ConfirmedBookingsCount { get; set; }

        public int CompletedBookingsCount { get; set; }

        public int CancelledBookingsCount { get; set; }

        public decimal TotalBookingValue { get; set; }

        public decimal TotalDepositRequired { get; set; }

        public decimal TotalPaidOnline { get; set; }

        public decimal TotalRemainingAtProperty { get; set; }

        public decimal TotalCollectedAtProperty { get; set; }

        public decimal TotalCommissionCalculated { get; set; }

        public decimal TotalCommissionHeld { get; set; }

        public decimal TotalCommissionCharged { get; set; }

        public decimal TotalCommissionReleased { get; set; }

        public decimal AvailableGuaranteeBalance { get; set; }

        public decimal HeldGuaranteeBalance { get; set; }

        public decimal TotalDebt { get; set; }

        public decimal CurrentMonthRevenue { get; set; }

        public decimal PreviousMonthRevenue { get; set; }

        public int PendingTopUpsCount { get; set; }

        public int PendingRefundsCount { get; set; }

        public decimal TotalCollected =>
            Math.Max(
                0m,
                TotalPaidOnline) +
            Math.Max(
                0m,
                TotalCollectedAtProperty);

        public decimal NetCollectedRevenue =>
            Math.Max(
                0m,
                TotalCollected -
                TotalCommissionCharged);

        public decimal RevenueChangePercentage
        {
            get
            {
                if (PreviousMonthRevenue <= 0m)
                {
                    return CurrentMonthRevenue > 0m
                        ? 100m
                        : 0m;
                }

                return Math.Round(
                    (
                        (
                            CurrentMonthRevenue -
                            PreviousMonthRevenue
                        ) /
                        PreviousMonthRevenue
                    ) * 100m,
                    2);
            }
        }
    }

    /* =====================================================================
       التبويب الثاني: الحسابات المالية للحجوزات
       ===================================================================== */

    public class BookingAccountsTabDto
    {
        public string Currency { get; set; } =
            "ريال يمني";

        public BookingFinancialFilterDto Filter { get; set; } =
            new BookingFinancialFilterDto();

        public List<OwnerFinancialPlaceOptionDto> Places { get; set; } =
            new List<OwnerFinancialPlaceOptionDto>();

        public BookingAccountsSummaryDto Summary { get; set; } =
            new BookingAccountsSummaryDto();

        public List<BookingFinancialItemDto> Bookings { get; set; } =
            new List<BookingFinancialItemDto>();

        public int TotalCount { get; set; }

        public int PageNumber { get; set; } =
            1;

        public int PageSize { get; set; } =
            20;

        public int TotalPages =>
            PageSize <= 0
                ? 0
                : (int)Math.Ceiling(
                    (double)TotalCount /
                    PageSize);

        public bool HasBookings =>
            TotalCount > 0;
    }

    public class BookingAccountsSummaryDto
    {
        public int BookingsCount { get; set; }

        /// <summary>إجمالي الحجوزات قبل الخصومات (Snapshot وقت الحجز).</summary>
        public decimal TotalOriginalBookingValue { get; set; }

        /// <summary>إجمالي الخصومات المباشرة المحفوظة على الحجوزات.</summary>
        public decimal TotalDiscountAmount { get; set; }

        public int DiscountedBookingsCount { get; set; }

        /// <summary>إجمالي الحجوزات النهائي بعد الخصم والضريبة المعاد حسابها.</summary>
        public decimal TotalBookingValue { get; set; }

        public decimal TotalSavingsAmount =>
            Math.Round(
                Math.Max(0m, TotalOriginalBookingValue - TotalBookingValue),
                2);

        public decimal TotalDepositRequired { get; set; }

        public decimal TotalPaidOnline { get; set; }

        public decimal TotalCollectedAtProperty { get; set; }

        public decimal TotalRemainingAtProperty { get; set; }

        public decimal TotalCommission { get; set; }

        public decimal TotalCollected =>
            Math.Max(
                0m,
                TotalPaidOnline) +
            Math.Max(
                0m,
                TotalCollectedAtProperty);

        public decimal OwnerNetAmount =>
            Math.Max(
                0m,
                TotalCollected -
                TotalCommission);
    }

    public class BookingFinancialFilterDto
    {
        public int? PlaceId { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        [MaxLength(30)]
        public string? BookingState { get; set; }

        [MaxLength(30)]
        public string? PaymentStatus { get; set; }

        [MaxLength(30)]
        public string? CommissionStatus { get; set; }

        [MaxLength(100)]
        public string? Search { get; set; }

        [Range(
            1,
            int.MaxValue)]
        public int PageNumber { get; set; } =
            1;

        [Range(
            1,
            100)]
        public int PageSize { get; set; } =
            20;
    }

    public class BookingFinancialItemDto
    {
        public int RecordId { get; set; }

        public int BookingId { get; set; }

        public int? CustomerId { get; set; }

        public string CustomerName { get; set; } =
            "عميل";

        public string? CustomerPhone { get; set; }

        public string? CustomerEmail { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } =
            "منشأة غير معروفة";

        public string? PlaceImageUrl { get; set; }

        public int? RoomId { get; set; }

        public string? RoomName { get; set; }

        public int? PackageId { get; set; }

        public string? PackageName { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string BookingState { get; set; } =
            "Pending";

        public string PaymentStatus { get; set; } =
            "Unpaid";

        public string RefundStatus { get; set; } =
            "NotApplicable";

        public string? PaymentMethod { get; set; }

        /// <summary>
        /// نوع الدفع الإلكتروني الذي اختاره العميل للحوالة الخارجية.
        /// القيم الجديدة المعتمدة: Deposit أو Full.
        /// قد يكون Null في السجلات القديمة أو قبل اختيار طريقة الدفع.
        /// </summary>
        public string? OnlinePaymentType { get; set; }

        /// <summary>الإجمالي النهائي بعد الخصم.</summary>
        public decimal TotalPrice { get; set; }

        public decimal FinalTotalPrice => TotalPrice;

        // =========================================================
        // لقطة العرض/الخصم المحفوظة وقت إنشاء الحجز
        // =========================================================
        public int? PromotionId { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? PromotionTitleSnapshot { get; set; }
        public string? PromotionCodeSnapshot { get; set; }
        public string? DiscountTypeSnapshot { get; set; }
        public decimal? DiscountValueSnapshot { get; set; }

        public bool HasPromotion =>
            DiscountAmount > 0m &&
            (
                (PromotionId.HasValue && PromotionId.Value > 0) ||
                !string.IsNullOrWhiteSpace(PromotionTitleSnapshot) ||
                !string.IsNullOrWhiteSpace(DiscountTypeSnapshot)
            );

        public decimal DisplayOriginalTotalPrice =>
            OriginalTotalPrice > 0m
                ? OriginalTotalPrice
                : TotalPrice + Math.Max(0m, DiscountAmount);

        /// <summary>
        /// التوفير النهائي الحقيقي بين الإجمالي الأصلي والإجمالي النهائي.
        /// قد يختلف عن DiscountAmount بسبب إعادة احتساب الضريبة بعد الخصم.
        /// </summary>
        public decimal FinalSavingsAmount =>
            Math.Round(
                Math.Max(0m, DisplayOriginalTotalPrice - TotalPrice),
                2);

        public string PromotionTitleDisplay =>
            !string.IsNullOrWhiteSpace(PromotionTitleSnapshot)
                ? PromotionTitleSnapshot.Trim()
                : "عرض خاص";

        public string PromotionDiscountLabel
        {
            get
            {
                var value = Math.Max(0m, DiscountValueSnapshot ?? 0m);

                if (string.Equals(
                        DiscountTypeSnapshot,
                        "Percentage",
                        StringComparison.OrdinalIgnoreCase) &&
                    value > 0m)
                {
                    return $"خصم {value:N0}%";
                }

                if (string.Equals(
                        DiscountTypeSnapshot,
                        "FixedAmount",
                        StringComparison.OrdinalIgnoreCase) &&
                    value > 0m)
                {
                    return $"خصم {value:N2}";
                }

                return HasPromotion ? "عرض مطبّق" : string.Empty;
            }
        }

        public decimal DepositAmount { get; set; }

        public decimal AmountPaidOnline { get; set; }

        public decimal AmountToPayAtProperty { get; set; }

        public decimal AmountCollectedAtProperty { get; set; }

        public bool IsPaymentConfirmed { get; set; }

        public string? TransferReferenceNumber { get; set; }

        public string? PaymentNotificationImage { get; set; }

        public decimal CommissionRateSnapshot { get; set; }

        public decimal CommissionBaseAmount { get; set; }

        public decimal PlatformCommission { get; set; }

        public string CommissionStatus { get; set; } =
            OwnerBookingCommissionStatuses.Calculated;

        public string CommissionPaymentStatus { get; set; } =
            "Unpaid";

        public DateTime? CommissionHeldAt { get; set; }

        public DateTime? CommissionChargedAt { get; set; }

        public DateTime? CommissionReleasedAt { get; set; }

        public DateTime? CollectedAt { get; set; }

        public DateTime? SettledAt { get; set; }

        public string ReservationUnitName =>
            !string.IsNullOrWhiteSpace(
                RoomName)
                ? RoomName
                : !string.IsNullOrWhiteSpace(
                    PackageName)
                    ? PackageName
                    : "وحدة حجز";

        public decimal TotalCollectedAmount =>
            Math.Max(
                0m,
                AmountPaidOnline) +
            Math.Max(
                0m,
                AmountCollectedAtProperty);

        public decimal OwnerNetAmount =>
            Math.Max(
                0m,
                TotalCollectedAmount -
                PlatformCommission);

        public bool IsFullyCollected =>
            TotalCollectedAmount >=
                TotalPrice &&
            AmountToPayAtProperty <=
                0m;

        public bool HasPaymentProof =>
            !string.IsNullOrWhiteSpace(
                PaymentNotificationImage);
    }

    /* =====================================================================
       التبويب الثالث: حساب ضمان العمولة
       ===================================================================== */

    public class CommissionGuaranteeTabDto
    {
        public string Currency { get; set; } =
            "ريال يمني";

        public decimal DefaultLowBalanceThreshold { get; set; }

        public bool DefaultAllowNegativeBalance { get; set; }

        public decimal DefaultDebtLimit { get; set; }

        public bool SuspendBookingWhenBalanceInsufficient { get; set; }

        public bool HidePlaceWhenCommissionSuspended { get; set; }

        public List<PlaceCommissionAccountDto> Accounts { get; set; } =
            new List<PlaceCommissionAccountDto>();
    }

    public class PlaceCommissionAccountDto
    {
        public int PlaceCommissionAccountId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } =
            string.Empty;

        public string? PlaceImageUrl { get; set; }

        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public bool AllowNegativeBalance { get; set; }

        public decimal DebtLimit { get; set; }

        public decimal LowBalanceThreshold { get; set; }

        public string AccountStatus { get; set; } =
            OwnerCommissionAccountStatuses.NeedsTopUp;

        public DateTime? LastTopUpAt { get; set; }

        public DateTime? DebtStartedAt { get; set; }

        public DateTime? DebtDueAt { get; set; }

        public bool IsDebtOverdue =>
            DebtAmount > 0m &&
            DebtDueAt.HasValue &&
            DebtDueAt.Value <= DateTime.Now;

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public decimal RemainingDebtCapacity =>
            AllowNegativeBalance
                ? Math.Max(
                    0m,
                    DebtLimit -
                    DebtAmount)
                : 0m;

        public decimal UsableCapacity =>
            Math.Max(
                0m,
                AvailableBalance) +
            RemainingDebtCapacity;

        public bool IsLowBalance =>
            AvailableBalance <=
            LowBalanceThreshold;

        public bool IsInDebt =>
            DebtAmount > 0m;

        public bool IsSuspended =>
            string.Equals(
                AccountStatus,
                OwnerCommissionAccountStatuses.Suspended,
                StringComparison.OrdinalIgnoreCase);

        public bool CanReceiveBookings =>
            !IsSuspended &&
            (
                AvailableBalance > 0m ||
                RemainingDebtCapacity > 0m
            );
    }

    /* =====================================================================
       التبويب الرابع: شحن رصيد الضمان
       ===================================================================== */

    public class TopUpBalanceTabDto
    {
        public string Currency { get; set; } =
            "ريال يمني";

        public decimal MinimumTopUpAmount { get; set; }

        public int? SelectedPlaceId { get; set; }

        public List<PlaceCommissionAccountDto> Accounts { get; set; } =
            new List<PlaceCommissionAccountDto>();

        public List<PlatformWalletOptionDto> PlatformWallets { get; set; } =
            new List<PlatformWalletOptionDto>();

        public List<CommissionTransactionItemDto> RecentTopUps
        {
            get;
            set;
        } = new List<CommissionTransactionItemDto>();
    }

    public class PlatformWalletOptionDto
    {
        public int PlatformWalletId { get; set; }

        public int WalletId { get; set; }

        public string WalletName { get; set; } =
            string.Empty;

        public string? LogoPath { get; set; }

        public string AccountNumber { get; set; } =
            string.Empty;

        public string BeneficiaryName { get; set; } =
            string.Empty;

        public bool IsPrimary { get; set; }

        public string? Instructions { get; set; }
    }

    public class TopUpRequestDto :
        IValidatableObject
    {
        private const decimal MaximumAllowedAmount =
            9999999999999999.99m;

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "اختر منشأة محددة من أعلى الصفحة.")]
        public int PlaceId { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "اختر حساب المنصة الذي حوّلت إليه.")]
        public int PlatformWalletId { get; set; }

        /*
         * التحقق العددي هنا مستقل عن Culture الخادم؛
         * لذلك لا نستخدم Range النصي الخاص بنوع decimal.
         * الحد الأدنى الذي يحدده الأدمن يُتحقق منه
         * داخل الكنترولر والخدمة لأنه قيمة ديناميكية.
         */
        public decimal Amount { get; set; }

        [Required(
            ErrorMessage =
                "أدخل رقم عملية التحويل من الإيصال.")]
        [StringLength(
            50,
            MinimumLength = 4,
            ErrorMessage =
                "رقم العملية يجب أن يكون بين 4 و50 خانة.")]
        [RegularExpression(
            @"^[A-Za-z0-9/-]{4,50}$",
            ErrorMessage =
                "رقم العملية يقبل الحروف الإنجليزية والأرقام والرمزين - و/ فقط، بدون مسافات أو حروف عربية.")]
        public string ReferenceNumber { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage =
                "أدخل تاريخ ووقت التحويل.")]
        public DateTime TransferDate { get; set; } =
            DateTime.Now;

        [MaxLength(
            2000,
            ErrorMessage =
                "الملاحظة يجب ألا تتجاوز 2000 حرف.")]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (Amount <= 0m)
            {
                yield return
                    new ValidationResult(
                        "أدخل مبلغ شحن صحيحًا أكبر من صفر.",
                        new[]
                        {
                            nameof(Amount)
                        });
            }
            else if (Amount > MaximumAllowedAmount)
            {
                yield return
                    new ValidationResult(
                        "مبلغ الشحن أكبر من الحد المسموح.",
                        new[]
                        {
                            nameof(Amount)
                        });
            }

            if (TransferDate == default)
            {
                yield return
                    new ValidationResult(
                        "أدخل تاريخ ووقت التحويل.",
                        new[]
                        {
                            nameof(TransferDate)
                        });
            }
            else if (
                TransferDate >
                DateTime.Now.AddMinutes(10)
            )
            {
                yield return
                    new ValidationResult(
                        "تاريخ التحويل لا يمكن أن يكون في المستقبل.",
                        new[]
                        {
                            nameof(TransferDate)
                        });
            }
        }
    }

    /* =====================================================================
       التبويب الخامس: كشف الحساب والحركات
       ===================================================================== */

    public class TransactionsTabDto
    {
        public string Currency { get; set; } =
            "ريال يمني";

        public int? SelectedPlaceId { get; set; }

        public string SelectedPlaceName { get; set; } =
            "كل المنشآت";

        public CommissionTransactionFilterDto Filter { get; set; } =
            new CommissionTransactionFilterDto();

        public CommissionTransactionsSummaryDto Summary { get; set; } =
            new CommissionTransactionsSummaryDto();

        public List<FinancialStatusOptionDto> TransactionTypeOptions
        {
            get;
            set;
        } = new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> StatusOptions
        {
            get;
            set;
        } = new List<FinancialStatusOptionDto>();

        public List<CommissionTransactionItemDto> Transactions
        {
            get;
            set;
        } = new List<CommissionTransactionItemDto>();

        public int TotalCount { get; set; }

        public int PageNumber { get; set; } =
            1;

        public int PageSize { get; set; } =
            25;

        public int TotalPages =>
            PageSize <= 0
                ? 0
                : (int)Math.Ceiling(
                    (double)TotalCount /
                    PageSize);

        public bool HasTransactions =>
            TotalCount > 0;

        public bool HasActiveFilters =>
            Filter.BookingId.HasValue ||
            !string.IsNullOrWhiteSpace(
                Filter.TransactionType) ||
            !string.IsNullOrWhiteSpace(
                Filter.Status) ||
            Filter.FromDate.HasValue ||
            Filter.ToDate.HasValue ||
            !string.IsNullOrWhiteSpace(
                Filter.Search);

        public int FirstItemNumber =>
            !HasTransactions ||
            Transactions.Count == 0
                ? 0
                : (
                    (
                        PageNumber -
                        1
                    ) *
                    PageSize
                ) +
                1;

        public int LastItemNumber =>
            !HasTransactions ||
            Transactions.Count == 0
                ? 0
                : Math.Min(
                    TotalCount,
                    FirstItemNumber +
                    Transactions.Count -
                    1);
    }

    public class CommissionTransactionsSummaryDto
    {
        public int RecordsCount { get; set; }

        public int PendingCount { get; set; }

        public int CompletedCount { get; set; }

        public int RejectedCount { get; set; }

        public decimal CompletedTopUpsAmount { get; set; }

        public decimal PendingTopUpsAmount { get; set; }

        public decimal CommissionHeldAmount { get; set; }

        public decimal CommissionChargedAmount { get; set; }

        public decimal CommissionReleasedAmount { get; set; }

        public decimal DebtCreatedAmount { get; set; }

        public decimal DebtPaidAmount { get; set; }

        public decimal AdjustmentsNetAmount { get; set; }

        public decimal CreditMovementsAmount { get; set; }

        public decimal DebitMovementsAmount { get; set; }

        public decimal AvailableBalanceNetChange { get; set; }

        public decimal HeldBalanceNetChange { get; set; }

        public decimal DebtNetChange { get; set; }

        public bool HasData =>
            RecordsCount > 0;
    }

    public class CommissionTransactionFilterDto :
        IValidatableObject
    {
        public int? PlaceId { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "رقم الحجز غير صحيح.")]
        public int? BookingId { get; set; }

        [MaxLength(
            30,
            ErrorMessage =
                "نوع الحركة غير صحيح.")]
        public string? TransactionType { get; set; }

        [MaxLength(
            20,
            ErrorMessage =
                "حالة الحركة غير صحيحة.")]
        public string? Status { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        [MaxLength(
            100,
            ErrorMessage =
                "عبارة البحث يجب ألا تتجاوز 100 حرف.")]
        public string? Search { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "رقم الصفحة غير صحيح.")]
        public int PageNumber { get; set; } =
            1;

        [Range(
            1,
            100,
            ErrorMessage =
                "عدد النتائج في الصفحة غير صحيح.")]
        public int PageSize { get; set; } =
            25;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (
                FromDate.HasValue &&
                ToDate.HasValue &&
                FromDate.Value.Date >
                ToDate.Value.Date
            )
            {
                yield return
                    new ValidationResult(
                        "تاريخ البداية يجب أن يسبق تاريخ النهاية.",
                        new[]
                        {
                            nameof(FromDate),
                            nameof(ToDate)
                        });
            }
        }
    }

    public class CommissionTransactionItemDto
    {
        public int CommissionTransactionId { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } =
            string.Empty;

        public string? PlaceImageUrl { get; set; }

        public int? BookingId { get; set; }

        public int? PlatformWalletId { get; set; }

        public string? PlatformWalletName { get; set; }

        public string TransactionType { get; set; } =
            string.Empty;

        public string TransactionTypeLabel { get; set; } =
            "حركة مالية";

        public string Direction { get; set; } =
            OwnerFinanceTransactionDirections.Neutral;

        public decimal Amount { get; set; }

        public decimal AvailableBalanceBefore { get; set; }

        public decimal AvailableBalanceAfter { get; set; }

        public decimal HeldBalanceBefore { get; set; }

        public decimal HeldBalanceAfter { get; set; }

        public decimal DebtBefore { get; set; }

        public decimal DebtAfter { get; set; }

        public string Status { get; set; } =
            OwnerFinanceTransactionStatuses.Completed;

        public string StatusLabel { get; set; } =
            "مكتملة";

        public string? ReferenceNumber { get; set; }

        public DateTime? TransferDate { get; set; }

        public string? ReceiptImage { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedByUserId { get; set; }

        public string? ReviewedByName { get; set; }

        public string? RejectionReason { get; set; }

        public DateTime? CreatedAt { get; set; }

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

        public DateTime? EffectiveDate =>
            CreatedAt ??
            TransferDate;

        public bool IsPending =>
            string.Equals(
                Status,
                OwnerFinanceTransactionStatuses.Pending,
                StringComparison.OrdinalIgnoreCase);

        public bool IsCompleted =>
            string.Equals(
                Status,
                OwnerFinanceTransactionStatuses.Completed,
                StringComparison.OrdinalIgnoreCase);

        public bool IsRejected =>
            string.Equals(
                Status,
                OwnerFinanceTransactionStatuses.Rejected,
                StringComparison.OrdinalIgnoreCase);

        public bool IsCancelled =>
            string.Equals(
                Status,
                OwnerFinanceTransactionStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase);

        public bool HasBooking =>
            BookingId.HasValue &&
            BookingId.Value > 0;

        public bool HasReference =>
            !string.IsNullOrWhiteSpace(
                ReferenceNumber);

        public bool HasReceipt =>
            !string.IsNullOrWhiteSpace(
                ReceiptImage);

        public bool HasReviewer =>
            ReviewedByUserId.HasValue ||
            !string.IsNullOrWhiteSpace(
                ReviewedByName);

        public bool HasRejectionReason =>
            !string.IsNullOrWhiteSpace(
                RejectionReason);

        public bool HasNotes =>
            !string.IsNullOrWhiteSpace(
                Notes);

        public bool HasBalanceEffect =>
            AvailableBalanceChange != 0m ||
            HeldBalanceChange != 0m ||
            DebtChange != 0m;
    }

    /* =====================================================================
       التبويب السادس: الاستردادات والإلغاءات
       ===================================================================== */

    public class RefundsTabDto
    {
        public string Currency { get; set; } =
            "ريال يمني";

        public int? SelectedPlaceId { get; set; }

        public string SelectedPlaceName { get; set; } =
            "كل المنشآت";

        public RefundFilterDto Filter { get; set; } =
            new RefundFilterDto();

        public RefundsSummaryDto Summary { get; set; } =
            new RefundsSummaryDto();

        public List<FinancialStatusOptionDto> StatusOptions
        {
            get;
            set;
        } = new List<FinancialStatusOptionDto>();

        public List<RefundRequestItemDto> Refunds
        {
            get;
            set;
        } = new List<RefundRequestItemDto>();

        public int TotalCount { get; set; }

        public int PageNumber { get; set; } =
            1;

        public int PageSize { get; set; } =
            25;

        public int TotalPages =>
            PageSize <= 0
                ? 0
                : (int)Math.Ceiling(
                    (double)TotalCount /
                    PageSize);

        public bool HasRefunds =>
            TotalCount > 0;

        public bool HasActiveFilters =>
            Filter.BookingId.HasValue ||
            Filter.CustomerId.HasValue ||
            !string.IsNullOrWhiteSpace(
                Filter.Status) ||
            Filter.FromDate.HasValue ||
            Filter.ToDate.HasValue ||
            !string.IsNullOrWhiteSpace(
                Filter.Search);

        public int FirstItemNumber =>
            !HasRefunds ||
            Refunds.Count == 0
                ? 0
                : (
                    (
                        PageNumber -
                        1
                    ) *
                    PageSize
                ) +
                1;

        public int LastItemNumber =>
            !HasRefunds ||
            Refunds.Count == 0
                ? 0
                : Math.Min(
                    TotalCount,
                    FirstItemNumber +
                    Refunds.Count -
                    1);
    }

    public class RefundsSummaryDto
    {
        public int RecordsCount { get; set; }

        public int PendingCount { get; set; }

        public int ProcessingCount { get; set; }

        public int CompletedCount { get; set; }

        public int RejectedCount { get; set; }

        public int FailedCount { get; set; }

        public int CancelledCount { get; set; }

        public decimal TotalDepositPaid { get; set; }

        public decimal RequestedRefundAmount { get; set; }

        public decimal PendingRefundAmount { get; set; }

        /// <summary>
        /// إجمالي الاستردادات المفتوحة التي يغطيها فعليًا
        /// AmountPaidOnline المعتمد ويمكن التعامل معها ماليًا.
        /// </summary>
        public decimal VerifiedOpenRefundAmount { get; set; }

        /// <summary>
        /// عدد السجلات المفتوحة التي مبلغ الاسترداد فيها أكبر من
        /// AmountPaidOnline المعتمد، ولذلك تُعرض كسجلات تحتاج مراجعة
        /// ولا يسمح بتنفيذ تحويل منها.
        /// </summary>
        public int FundingReviewCount { get; set; }

        public int ReadyForTransferCount { get; set; }

        public int WaitingDestinationCount { get; set; }

        public int WaitingCustomerConfirmationCount { get; set; }

        public decimal CompletedRefundAmount { get; set; }

        public decimal CompletedCancellationFees { get; set; }

        public decimal CompletedOwnerShare { get; set; }

        public decimal CompletedPlatformCommission { get; set; }

        public decimal CompletedCommissionReleased { get; set; }

        public decimal CompletedRetainedAmount { get; set; }

        /// <summary>
        /// إجمالي العمولة التي عادت إلى حساب ضمان المالك من التسويات
        /// الفعلية غير المرفوضة/الملغاة/الفاشلة، حتى لو كان تحويل العميل
        /// ما يزال Pending أو Processing.
        /// </summary>
        public decimal TotalCommissionReleased { get; set; }

        public decimal TotalRetainedAmount { get; set; }

        public bool HasData =>
            RecordsCount > 0;

        public int OpenRequestsCount =>
            PendingCount +
            ProcessingCount;
    }

    public class RefundFilterDto :
        IValidatableObject
    {
        public int? PlaceId { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "رقم الحجز غير صحيح.")]
        public int? BookingId { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "رقم العميل غير صحيح.")]
        public int? CustomerId { get; set; }

        [MaxLength(
            20,
            ErrorMessage =
                "حالة الاسترداد غير صحيحة.")]
        public string? Status { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        [MaxLength(
            100,
            ErrorMessage =
                "عبارة البحث يجب ألا تتجاوز 100 حرف.")]
        public string? Search { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "رقم الصفحة غير صحيح.")]
        public int PageNumber { get; set; } =
            1;

        [Range(
            1,
            100,
            ErrorMessage =
                "عدد النتائج في الصفحة غير صحيح.")]
        public int PageSize { get; set; } =
            25;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (
                FromDate.HasValue &&
                ToDate.HasValue &&
                FromDate.Value.Date >
                ToDate.Value.Date
            )
            {
                yield return
                    new ValidationResult(
                        "تاريخ البداية يجب أن يسبق تاريخ النهاية.",
                        new[]
                        {
                            nameof(FromDate),
                            nameof(ToDate)
                        });
            }
        }
    }

    public class RefundRequestItemDto
    {
        public int RefundRequestId { get; set; }

        public int BookingId { get; set; }

        public int? CustomerId { get; set; }

        public string CustomerName { get; set; } =
            "عميل";

        public string? CustomerPhone { get; set; }

        public string? CustomerEmail { get; set; }

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } =
            string.Empty;

        public string? PlaceImageUrl { get; set; }

        public DateTime? BookingStartDate { get; set; }

        public DateTime? BookingEndDate { get; set; }

        public string BookingState { get; set; } =
            string.Empty;

        public string PaymentStatus { get; set; } =
            string.Empty;

        /// <summary>
        /// المبلغ الإلكتروني الذي كان قد دُفع فعليًا قبل إنشاء طلب الاسترداد.
        /// الاسم الأصلي في قاعدة البيانات RefundRequests.DepositPaid محفوظ للتوافق.
        /// </summary>
        public decimal DepositPaid { get; set; }

        public decimal BookingOriginalTotalPrice { get; set; }

        public decimal BookingDiscountAmount { get; set; }

        public decimal BookingTotalPrice { get; set; }

        public decimal RequiredDepositAmount { get; set; }

        public decimal AmountPaidOnline { get; set; }

        public string? OnlinePaymentType { get; set; }

        public int? PromotionId { get; set; }

        public string? PromotionTitleSnapshot { get; set; }

        public string? PromotionCodeSnapshot { get; set; }

        public decimal CommissionRateSnapshot { get; set; }

        public decimal CommissionBaseAmount { get; set; }

        public decimal CancellationFee { get; set; }

        public decimal RefundAmount { get; set; }

        public decimal OwnerShare { get; set; }

        public decimal PlatformCommission { get; set; }

        public decimal CommissionReleased { get; set; }

        public string Status { get; set; } =
            OwnerRefundStatuses.Pending;

        public string StatusLabel { get; set; } =
            "قيد المراجعة";

        public string? Reason { get; set; }

        public string? ReferenceNumber { get; set; }

        public string? ProofImage { get; set; }

        public int? RefundWalletId { get; set; }

        public string? RefundWalletName { get; set; }

        public string? RefundAccountNumber { get; set; }

        public string? RefundBeneficiaryName { get; set; }

        public DateTime? TransferredAt { get; set; }

        public DateTime? CustomerConfirmedAt { get; set; }

        public DateTime? RequestedAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public int? ProcessedByUserId { get; set; }

        public string? ProcessedByName { get; set; }

        public string? Notes { get; set; }

        /// <summary>
        /// المبلغ الذي بقي لدى المنشأة بعد نتيجة الإلغاء/عدم الحضور،
        /// قبل طرح عمولة المنصة النهائية.
        /// </summary>
        public decimal SettlementPaidAmount =>
            Math.Max(
                0m,
                DepositPaid);

        public decimal RetainedAmount =>
            Math.Max(
                0m,
                SettlementPaidAmount -
                RefundAmount);

        public decimal PreviousPlatformCommission =>
            Math.Max(
                0m,
                PlatformCommission +
                CommissionReleased);

        public decimal OwnerNetRetainedAmount =>
            Math.Max(
                0m,
                RetainedAmount -
                PlatformCommission);

        public decimal UnreleasedCommission =>
            Math.Max(
                0m,
                PlatformCommission -
                CommissionReleased);

        public decimal RefundPercentage =>
            DepositPaid <= 0m
                ? 0m
                : Math.Round(
                    (
                        RefundAmount /
                        DepositPaid
                    ) *
                    100m,
                    2);

        public DateTime? EffectiveDate =>
            ProcessedAt ??
            RequestedAt;

        public bool IsPending =>
            string.Equals(
                Status,
                OwnerRefundStatuses.Pending,
                StringComparison.OrdinalIgnoreCase);

        public bool IsProcessing =>
            string.Equals(
                Status,
                OwnerRefundStatuses.Processing,
                StringComparison.OrdinalIgnoreCase);

        public bool IsCompleted =>
            string.Equals(
                Status,
                OwnerRefundStatuses.Completed,
                StringComparison.OrdinalIgnoreCase);

        public bool IsRejected =>
            string.Equals(
                Status,
                OwnerRefundStatuses.Rejected,
                StringComparison.OrdinalIgnoreCase);

        public bool IsFailed =>
            string.Equals(
                Status,
                OwnerRefundStatuses.Failed,
                StringComparison.OrdinalIgnoreCase);

        public bool IsCancelled =>
            string.Equals(
                Status,
                OwnerRefundStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase);

        public bool IsOpen =>
            IsPending ||
            IsProcessing;

        public bool IsFullRefund =>
            DepositPaid > 0m &&
            RefundAmount >=
                DepositPaid;

        public bool HasProof =>
            !string.IsNullOrWhiteSpace(
                ProofImage);

        public bool HasRefundDestination =>
            RefundWalletId.HasValue &&
            RefundWalletId.Value > 0 &&
            !string.IsNullOrWhiteSpace(
                RefundAccountNumber) &&
            !string.IsNullOrWhiteSpace(
                RefundBeneficiaryName);

        public bool IsRefundFundingVerified =>
            RefundAmount <= 0m ||
            AmountPaidOnline + 0.01m >=
                RefundAmount;

        public bool CanMarkTransferred =>
            IsPending &&
            RefundAmount > 0m &&
            IsRefundFundingVerified &&
            HasRefundDestination &&
            !TransferredAt.HasValue;

        public bool WaitingForCustomerConfirmation =>
            IsProcessing &&
            TransferredAt.HasValue &&
            !CustomerConfirmedAt.HasValue;

        public bool HasReference =>
            !string.IsNullOrWhiteSpace(
                ReferenceNumber);

        public bool HasReason =>
            !string.IsNullOrWhiteSpace(
                Reason);

        public bool HasNotes =>
            !string.IsNullOrWhiteSpace(
                Notes);

        public bool HasProcessor =>
            ProcessedByUserId.HasValue ||
            !string.IsNullOrWhiteSpace(
                ProcessedByName);

        public bool HasCustomerContact =>
            !string.IsNullOrWhiteSpace(
                CustomerPhone) ||
            !string.IsNullOrWhiteSpace(
                CustomerEmail);

        public bool IsCommissionFullyReleased =>
            PlatformCommission <= 0m ||
            CommissionReleased >=
                PlatformCommission;
    }

    /* =====================================================================
       التبويب السابع: حسابات استقبال دفعات العملاء
       ===================================================================== */

    public class OwnerWalletsTabDto
    {
        public OwnerWalletsSummaryDto Summary { get; set; } =
            new OwnerWalletsSummaryDto();

        public List<OwnerWalletItemDto> Wallets { get; set; } =
            new List<OwnerWalletItemDto>();

        public List<SupportedWalletOptionDto> SupportedWallets
        {
            get;
            set;
        } = new List<SupportedWalletOptionDto>();

        public bool HasWallets =>
            Wallets.Count > 0;

        public bool HasSupportedWallets =>
            SupportedWallets.Count > 0;

        public bool CanAddWallet =>
            SupportedWallets.Exists(
                wallet =>
                    wallet.CanAdd);

        public DateTime? LastUpdatedAt { get; set; }
    }

    public class OwnerWalletsSummaryDto
    {
        public int TotalAccounts { get; set; }

        public int ActiveAccounts { get; set; }

        public int InactiveAccounts { get; set; }

        public int ActiveSupportedWallets { get; set; }

        public int LinkedWallets { get; set; }

        public int AvailableWallets { get; set; }

        public int UnavailableLinkedWallets { get; set; }

        public bool HasActiveReceivingAccount =>
            ActiveAccounts > 0;

        public bool NeedsReceivingAccount =>
            ActiveAccounts == 0;

        public decimal CoveragePercentage =>
            ActiveSupportedWallets <= 0
                ? 0m
                : Math.Round(
                    Math.Min(
                        100m,
                        (
                            LinkedWallets /
                            (decimal)ActiveSupportedWallets
                        ) *
                        100m),
                    2);
    }

    public class OwnerWalletItemDto
    {
        public int OwnerWalletId { get; set; }

        public int WalletId { get; set; }

        public string WalletName { get; set; } =
            string.Empty;

        public string? LogoPath { get; set; }

        public string AccountNumber { get; set; } =
            string.Empty;

        public string BeneficiaryName { get; set; } =
            string.Empty;

        public bool IsActive { get; set; }

        public bool IsProviderActive { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string StatusLabel =>
            IsActive
                ? (
                    IsProviderActive
                        ? "نشط ويظهر للعملاء"
                        : "موقوف من إدارة المنصة"
                )
                : "متوقف";

        public string ProviderStatusLabel =>
            IsProviderActive
                ? "الشبكة متاحة"
                : "الشبكة غير متاحة";

        public bool IsAvailableToCustomers =>
            IsActive &&
            IsProviderActive;

        public bool CanActivate =>
            !IsActive &&
            IsProviderActive;

        public bool CanDeactivate =>
            IsActive;

        public bool CanEdit =>
            IsProviderActive;

        public string MaskedAccountNumber
        {
            get
            {
                var value =
                    AccountNumber?
                        .Trim() ??
                    string.Empty;

                if (value.Length <= 2)
                {
                    return value;
                }

                return string.Concat(
                    new string(
                        '•',
                        value.Length - 2),
                    value.Substring(
                        value.Length - 2));
            }
        }

        public string AccountNumberDisplay
        {
            get
            {
                var value =
                    AccountNumber?
                        .Trim() ??
                    string.Empty;

                if (value.Length == 6)
                {
                    return string.Concat(
                        value.Substring(
                            0,
                            3),
                        " ",
                        value.Substring(
                            3,
                            3));
                }

                return value;
            }
        }

        public string WalletInitial =>
            string.IsNullOrWhiteSpace(
                WalletName)
                ? "م"
                : WalletName.Trim()
                    .Substring(
                        0,
                        1)
                    .ToUpperInvariant();
    }

    public class SupportedWalletOptionDto
    {
        public int WalletId { get; set; }

        public string WalletName { get; set; } =
            string.Empty;

        public string? LogoPath { get; set; }

        public bool IsActive { get; set; }

        public bool IsLinked { get; set; }

        public int? OwnerWalletId { get; set; }

        public string? LinkedAccountNumber { get; set; }

        public string? LinkedBeneficiaryName { get; set; }

        public bool LinkedAccountIsActive { get; set; }

        public bool CanAdd =>
            IsActive &&
            !IsLinked;

        public string AvailabilityLabel =>
            !IsActive
                ? "غير متاحة من إدارة المنصة"
                : IsLinked
                    ? "مرتبطة بحسابك"
                    : "متاحة للإضافة";

        public string WalletInitial =>
            string.IsNullOrWhiteSpace(
                WalletName)
                ? "م"
                : WalletName.Trim()
                    .Substring(
                        0,
                        1)
                    .ToUpperInvariant();
    }

    public class SaveOwnerWalletRequestDto
    {
        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "رقم حساب الاستقبال غير صحيح.")]
        public int? OwnerWalletId { get; set; }

        [Range(
            1,
            int.MaxValue,
            ErrorMessage =
                "اختر شبكة الدفع.")]
        public int WalletId { get; set; }

        [Required(
            ErrorMessage =
                "أدخل رقم الحساب التجاري.")]
        [StringLength(
            6,
            MinimumLength = 6,
            ErrorMessage =
                "رقم الحساب التجاري يجب أن يتكون من 6 أرقام.")]
        [RegularExpression(
            "^[0-9]{6}$",
            ErrorMessage =
                "رقم الحساب التجاري يجب أن يتكون من 6 أرقام إنجليزية فقط.")]
        public string AccountNumber { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage =
                "أدخل اسم المستفيد.")]
        [StringLength(
            150,
            MinimumLength = 2,
            ErrorMessage =
                "اسم المستفيد يجب أن يكون بين حرفين و150 حرفًا.")]
        public string BeneficiaryName { get; set; } =
            string.Empty;

        public bool IsActive { get; set; } =
            true;
    }

    /* =====================================================================
       التبويب الثامن: الفواتير والتقارير
       ===================================================================== */

    public static class OwnerFinancialReportTypes
    {
        public const string Documents = "Documents";
        public const string Bookings = "Bookings";
        public const string Payments = "Payments";
        public const string Commissions = "Commissions";
        public const string Refunds = "Refunds";
        public const string Transactions = "Transactions";
        public const string Comprehensive = "Comprehensive";

        public static readonly IReadOnlyCollection<string> All =
            new[]
            {
                Comprehensive,
                Bookings,
                Payments,
                Commissions,
                Refunds,
                Transactions,
                Documents
            };
    }

    public static class OwnerFinancialDocumentTypes
    {
        public const string BookingInvoice = "BookingInvoice";
        public const string DepositReceipt = "DepositReceipt";
        public const string CollectionReceipt = "CollectionReceipt";
        public const string RefundReceipt = "RefundReceipt";
        public const string TopUpReceipt = "TopUpReceipt";
        public const string BookingSettlement = "BookingSettlement";

        public static readonly IReadOnlyCollection<string> All =
            new[]
            {
                BookingInvoice,
                DepositReceipt,
                CollectionReceipt,
                RefundReceipt,
                TopUpReceipt,
                BookingSettlement
            };
    }

    public static class OwnerFinancialExportFormats
    {
        public const string View = "View";
        public const string Excel = "Excel";
        public const string Print = "Print";

        /*
         * أبقيناه مؤقتًا حتى لا تنكسر الروابط القديمة.
         * سيتم تحويل زر التصدير إلى Excel في الكنترولر والواجهة.
         */
        public const string Csv = "Csv";

        public static readonly IReadOnlyCollection<string> All =
            new[]
            {
                View,
                Excel,
                Print,
                Csv
            };
    }

    public static class OwnerFinancialDateScopes
    {
        public const string DocumentDate = "DocumentDate";
        public const string BookingCreatedDate = "BookingCreatedDate";
        public const string StayStartDate = "StayStartDate";
        public const string PaymentActivityDate = "PaymentActivityDate";
        public const string OnlinePaymentDate = "OnlinePaymentDate";
        public const string PropertyCollectionDate = "PropertyCollectionDate";
        public const string CommissionActivityDate = "CommissionActivityDate";
        public const string RefundRequestedDate = "RefundRequestedDate";
        public const string RefundProcessedDate = "RefundProcessedDate";
        public const string TransactionDate = "TransactionDate";

        public static readonly IReadOnlyCollection<string> All =
            new[]
            {
                DocumentDate,
                BookingCreatedDate,
                StayStartDate,
                PaymentActivityDate,
                OnlinePaymentDate,
                PropertyCollectionDate,
                CommissionActivityDate,
                RefundRequestedDate,
                RefundProcessedDate,
                TransactionDate
            };
    }

    public static class OwnerFinancialPaymentChannels
    {
        public const string All = "All";
        public const string Online = "Online";
        public const string AtProperty = "AtProperty";

        public static readonly IReadOnlyCollection<string> Values =
            new[]
            {
                All,
                Online,
                AtProperty
            };
    }

    public class InvoicesReportsTabDto
    {
        public string Currency { get; set; } = "ريال يمني";

        public int? SelectedPlaceId { get; set; }

        public string SelectedPlaceName { get; set; } = "كل المنشآت";

        public string ReportTitle { get; set; } = "التقرير المالي الشامل";

        public string ReportDescription { get; set; } =
            "تدقيق موحد للحجوزات والتحصيل والعمولات والاستردادات وحركات الرصيد.";

        public FinancialReportFilterDto Filter { get; set; } =
            new FinancialReportFilterDto();

        public FinancialReconciliationDto Reconciliation { get; set; } =
            new FinancialReconciliationDto();

        public FinancialReportSummaryDto Summary { get; set; } =
            new FinancialReportSummaryDto();

        public ComprehensiveFinancialAnalyticsDto Analytics { get; set; } =
            new ComprehensiveFinancialAnalyticsDto();

        public FinancialReportCapabilitiesDto Capabilities { get; set; } =
            new FinancialReportCapabilitiesDto();

        public List<FinancialStatusOptionDto> ReportTypeOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> DocumentTypeOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> DateScopeOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> PaymentChannelOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> BookingStateOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> PaymentStatusOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> CommissionStatusOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> RefundStatusOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> TransactionTypeOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialStatusOptionDto> TransactionStatusOptions { get; set; } =
            new List<FinancialStatusOptionDto>();

        public List<FinancialDocumentItemDto> Documents { get; set; } =
            new List<FinancialDocumentItemDto>();

        /*
         * القائمة العامة التي تستخدمها الشاشة الحالية.
         * كل منشئ تقرير يملؤها بأعمدته وقيمه الخاصة، ولا تتم مشاركة
         * حسابات تقرير مع تقرير آخر.
         */
        public List<FinancialReportRowDto> ReportRows { get; set; } =
            new List<FinancialReportRowDto>();

        public int TotalCount { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 25;

        public bool IsDocumentsMode =>
            string.Equals(
                Filter.ReportType,
                OwnerFinancialReportTypes.Documents,
                StringComparison.OrdinalIgnoreCase);

        public bool HasRows =>
            IsDocumentsMode
                ? Documents.Count > 0
                : ReportRows.Count > 0;

        public bool HasActiveFilters =>
            Filter.PlaceId.HasValue ||
            Filter.BookingId.HasValue ||
            Filter.CustomerId.HasValue ||
            Filter.FromDate.HasValue ||
            Filter.ToDate.HasValue ||
            !string.IsNullOrWhiteSpace(Filter.DocumentType) ||
            !string.IsNullOrWhiteSpace(Filter.DateScope) ||
            !string.IsNullOrWhiteSpace(Filter.PaymentChannel) ||
            !string.IsNullOrWhiteSpace(Filter.BookingState) ||
            !string.IsNullOrWhiteSpace(Filter.PaymentStatus) ||
            !string.IsNullOrWhiteSpace(Filter.CommissionStatus) ||
            !string.IsNullOrWhiteSpace(Filter.RefundStatus) ||
            !string.IsNullOrWhiteSpace(Filter.TransactionType) ||
            !string.IsNullOrWhiteSpace(Filter.TransactionStatus) ||
            !string.IsNullOrWhiteSpace(Filter.ReferenceNumber) ||
            !string.IsNullOrWhiteSpace(Filter.Search);

        public int TotalPages =>
            TotalCount <= 0 || PageSize <= 0
                ? 0
                : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public int FirstItemNumber =>
            TotalCount <= 0
                ? 0
                : ((PageNumber - 1) * PageSize) + 1;

        public int LastItemNumber =>
            TotalCount <= 0
                ? 0
                : Math.Min(TotalCount, PageNumber * PageSize);
    }

    public class FinancialReportCapabilitiesDto
    {
        public bool ShowDocumentType { get; set; }
        public bool ShowDateScope { get; set; }
        public bool ShowPaymentChannel { get; set; }
        public bool ShowBookingId { get; set; }
        public bool ShowCustomerId { get; set; }
        public bool ShowBookingState { get; set; }
        public bool ShowPaymentStatus { get; set; }
        public bool ShowCommissionStatus { get; set; }
        public bool ShowRefundStatus { get; set; }
        public bool ShowTransactionType { get; set; }
        public bool ShowTransactionStatus { get; set; }
        public bool ShowReferenceNumber { get; set; }
        public bool ShowSearch { get; set; } = true;
    }

    public class FinancialReportFilterDto : IValidatableObject
    {
        public int? PlaceId { get; set; }

        public int? BookingId { get; set; }

        public int? CustomerId { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        [MaxLength(40)]
        public string ReportType { get; set; } =
            OwnerFinancialReportTypes.Comprehensive;

        [MaxLength(40)]
        public string? DocumentType { get; set; }

        [MaxLength(40)]
        public string? DateScope { get; set; }

        [MaxLength(30)]
        public string? PaymentChannel { get; set; }

        [MaxLength(30)]
        public string? BookingState { get; set; }

        [MaxLength(30)]
        public string? PaymentStatus { get; set; }

        [MaxLength(30)]
        public string? CommissionStatus { get; set; }

        [MaxLength(30)]
        public string? RefundStatus { get; set; }

        [MaxLength(40)]
        public string? TransactionType { get; set; }

        [MaxLength(30)]
        public string? TransactionStatus { get; set; }

        [MaxLength(80)]
        public string? ReferenceNumber { get; set; }

        [MaxLength(100)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "رقم الصفحة غير صحيح.")]
        public int PageNumber { get; set; } = 1;

        [Range(10, 50, ErrorMessage = "عدد النتائج يجب أن يكون 10 أو 25 أو 50.")]
        public int PageSize { get; set; } = 25;

        [MaxLength(20)]
        public string ExportFormat { get; set; } =
            OwnerFinancialExportFormats.View;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (BookingId.HasValue && BookingId.Value <= 0)
            {
                yield return new ValidationResult(
                    "رقم الحجز غير صحيح.",
                    new[] { nameof(BookingId) });
            }

            if (CustomerId.HasValue && CustomerId.Value <= 0)
            {
                yield return new ValidationResult(
                    "رقم العميل غير صحيح.",
                    new[] { nameof(CustomerId) });
            }

            if (
                FromDate.HasValue &&
                ToDate.HasValue &&
                FromDate.Value.Date > ToDate.Value.Date)
            {
                yield return new ValidationResult(
                    "تاريخ البداية يجب أن يسبق تاريخ النهاية.",
                    new[]
                    {
                        nameof(FromDate),
                        nameof(ToDate)
                    });
            }
        }
    }

    /// <summary>
    /// فحص اتساق محاسبي غير مخزّن في قاعدة البيانات.
    /// يعتمد فقط على صفوف التقرير الكاملة بعد تطبيق الفلاتر وقبل Pagination.
    /// لا يغيّر الأرصدة ولا يعيد كتابة أي حركة مالية.
    /// </summary>
    public class FinancialReconciliationDto
    {
        public bool IsApplicable { get; set; }

        public int CheckedRecords { get; set; }

        public int CollectionMismatchCount { get; set; }

        public int RefundFundingMismatchCount { get; set; }

        public int PriceMismatchCount { get; set; }

        public int NegativeBalanceCount { get; set; }

        public decimal MaximumCollectionDifference { get; set; }

        public int IssueCount =>
            CollectionMismatchCount +
            RefundFundingMismatchCount +
            PriceMismatchCount +
            NegativeBalanceCount;

        public bool IsBalanced =>
            !IsApplicable ||
            IssueCount == 0;

        public string StatusLabel =>
            !IsApplicable
                ? "غير مطلوب لهذا التقرير"
                : IsBalanced
                    ? "متطابق"
                    : "يحتاج مراجعة";
    }

    public class FinancialReportSummaryDto
    {
        public int RecordsCount { get; set; }
        public int DocumentsCount { get; set; }
        public int BookingInvoicesCount { get; set; }
        public int DepositReceiptsCount { get; set; }
        public int CollectionReceiptsCount { get; set; }
        public int RefundReceiptsCount { get; set; }
        public int TopUpReceiptsCount { get; set; }
        public int SettlementStatementsCount { get; set; }

        public int BookingCount { get; set; }
        public int ConfirmedBookingsCount { get; set; }
        public int CompletedBookingsCount { get; set; }
        public int CancelledBookingsCount { get; set; }
        public int CustomerCount { get; set; }
        public int PlaceCount { get; set; }

        public int FullyPaidBookingsCount { get; set; }
        public int PartiallyPaidBookingsCount { get; set; }
        public int UnpaidBookingsCount { get; set; }

        public int CompletedRefundsCount { get; set; }
        public int OpenRefundsCount { get; set; }
        public int RejectedRefundsCount { get; set; }
        public int FailedRefundsCount { get; set; }
        public int CancelledRefundsCount { get; set; }

        public int CompletedTransactionsCount { get; set; }
        public int PendingTransactionsCount { get; set; }

        public decimal TotalDocumentValue { get; set; }
        public decimal TotalOriginalBookingValue { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public int DiscountedBookingsCount { get; set; }
        public decimal TotalBookingValue { get; set; }
        public decimal TotalSavingsAmount =>
            Math.Round(
                Math.Max(0m, TotalOriginalBookingValue - TotalBookingValue),
                2);
        public decimal TotalDepositRequired { get; set; }
        public decimal TotalPaidOnline { get; set; }
        public decimal TotalCollectedAtProperty { get; set; }
        public decimal TotalActualCollected { get; set; }
        public decimal TotalRemainingAtProperty { get; set; }
        public decimal ActualRevenue { get; set; }

        public decimal TotalCommission { get; set; }
        public decimal TotalCommissionHeld { get; set; }
        public decimal TotalCommissionCharged { get; set; }
        public decimal TotalCommissionReleased { get; set; }
        public decimal TotalCommissionDebt { get; set; }
        public decimal TotalCommissionOutstanding { get; set; }

        public decimal RequestedRefundAmount { get; set; }
        public decimal PendingRefundAmount { get; set; }
        public decimal TotalRefunded { get; set; }
        public decimal TotalCancellationFees { get; set; }
        public decimal TotalOwnerShare { get; set; }

        public decimal TotalTopUps { get; set; }
        public decimal TotalPendingTopUps { get; set; }
        public decimal CurrentDebt { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal NetCashFlow { get; set; }
        public decimal GuaranteeNetFlow { get; set; }

        public decimal TotalPaid =>
            Math.Max(
                0m,
                TotalActualCollected > 0m
                    ? TotalActualCollected
                    : TotalPaidOnline + TotalCollectedAtProperty);

        public decimal CollectionRate =>
            TotalBookingValue <= 0m
                ? 0m
                : Math.Round(
                    Math.Min(100m, (TotalPaid / TotalBookingValue) * 100m),
                    2);

        public decimal AverageBookingValue =>
            BookingCount <= 0
                ? 0m
                : Math.Round(TotalBookingValue / BookingCount, 2);

        public decimal RefundRate =>
            TotalPaid <= 0m
                ? 0m
                : Math.Round(
                    Math.Min(100m, (TotalRefunded / TotalPaid) * 100m),
                    2);
    }

    public class ComprehensiveFinancialAnalyticsDto
    {
        public decimal CompletedTopUpsAmount { get; set; }
        public decimal PendingTopUpsAmount { get; set; }
        public int PendingTopUpsCount { get; set; }

        public decimal CommissionHeldAmount { get; set; }
        public decimal CommissionChargedAmount { get; set; }
        public decimal CommissionReleasedAmount { get; set; }
        public decimal CommissionDebtCreatedAmount { get; set; }

        public decimal CurrentAvailableGuaranteeBalance { get; set; }
        public decimal CurrentHeldGuaranteeBalance { get; set; }
        public decimal CurrentDebt { get; set; }

        public decimal CompletedRefundAmount { get; set; }
        public decimal PendingRefundAmount { get; set; }
        public int CompletedRefundCount { get; set; }
        public int PendingRefundCount { get; set; }

        public decimal OperationalNetCashFlow { get; set; }
        public decimal GuaranteeNetFlow { get; set; }
    }

    public class FinancialDocumentItemDto
    {
        public string DocumentKey { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int? BookingId { get; set; }
        public int? CustomerId { get; set; }
        public int PlaceId { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocumentType { get; set; } =
            OwnerFinancialDocumentTypes.BookingInvoice;
        public string DocumentTypeLabel { get; set; } = "فاتورة حجز";
        public DateTime DocumentDate { get; set; }
        public string CustomerName { get; set; } = "عميل";
        public string PlaceName { get; set; } = "منشأة غير معروفة";
        public string? PlaceImageUrl { get; set; }
        public decimal PrimaryAmount { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? PromotionId { get; set; }
        public string? PromotionTitleSnapshot { get; set; }
        public string? PromotionCodeSnapshot { get; set; }
        public string? DiscountTypeSnapshot { get; set; }
        public decimal? DiscountValueSnapshot { get; set; }
        public bool HasPromotion => DiscountAmount > 0m;
        public decimal FinalSavingsAmount =>
            Math.Round(
                Math.Max(0m, OriginalTotalPrice - TotalPrice),
                2);
        public string PromotionTitleDisplay =>
            !string.IsNullOrWhiteSpace(PromotionTitleSnapshot)
                ? PromotionTitleSnapshot.Trim()
                : "عرض خاص";
        public decimal DepositAmount { get; set; }
        public decimal AmountPaidOnline { get; set; }
        public decimal AmountCollectedAtProperty { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal PlatformCommission { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal CancellationFee { get; set; }
        public decimal OwnerShare { get; set; }
        public decimal NetOwnerAmount { get; set; }
        public string BookingState { get; set; } = string.Empty;
        public string BookingStateLabel { get; set; } = "غير محددة";
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = "غير محددة";
        public string CommissionStatus { get; set; } = string.Empty;
        public string CommissionStatusLabel { get; set; } = "غير محددة";
        public string RecordStatus { get; set; } = string.Empty;
        public string RecordStatusLabel { get; set; } = "غير محددة";
        public string? ReferenceNumber { get; set; }
        public string? Description { get; set; }
        public bool CanPrint { get; set; } = true;
        public bool CanExport { get; set; } = true;
    }

    public class FinancialReportRowDto
    {
        public int RecordId { get; set; }
        public string RecordType { get; set; } = string.Empty;
        public string RecordTypeLabel { get; set; } = string.Empty;
        public string RecordNumber { get; set; } = string.Empty;
        public DateTime RecordDate { get; set; }

        public int? BookingId { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = "عميل";
        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = "منشأة غير معروفة";
        public string? PlaceImageUrl { get; set; }

        public DateTime? BookingCreatedAt { get; set; }
        public DateTime? BookingStartDate { get; set; }
        public DateTime? BookingEndDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public DateTime? CollectionDate { get; set; }
        public DateTime? CommissionDate { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        public string BookingState { get; set; } = string.Empty;
        public string BookingStateLabel { get; set; } = "غير محددة";
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentStatusLabel { get; set; } = "غير محددة";
        public string PaymentChannel { get; set; } = string.Empty;
        public string PaymentChannelLabel { get; set; } = string.Empty;
        public string CommissionStatus { get; set; } = string.Empty;
        public string CommissionStatusLabel { get; set; } = "غير محددة";
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = "غير محددة";
        public string? TransactionType { get; set; }
        public string? TransactionTypeLabel { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Description { get; set; }

        public decimal PrimaryAmount { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? PromotionId { get; set; }
        public string? PromotionTitleSnapshot { get; set; }
        public string? PromotionCodeSnapshot { get; set; }
        public string? DiscountTypeSnapshot { get; set; }
        public decimal? DiscountValueSnapshot { get; set; }
        public bool HasPromotion => DiscountAmount > 0m;
        public decimal FinalSavingsAmount =>
            Math.Round(
                Math.Max(0m, OriginalTotalPrice - TotalPrice),
                2);
        public string PromotionTitleDisplay =>
            !string.IsNullOrWhiteSpace(PromotionTitleSnapshot)
                ? PromotionTitleSnapshot.Trim()
                : "عرض خاص";
        public decimal DepositAmount { get; set; }
        public decimal AmountPaidOnline { get; set; }
        public decimal AmountCollectedAtProperty { get; set; }
        public decimal ActualPaidOnline { get; set; }
        public decimal ActualCollectedAtProperty { get; set; }
        public decimal ActualCollectedAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal ActualRevenue { get; set; }

        public decimal PlatformCommission { get; set; }
        public decimal CommissionHeld { get; set; }
        public decimal CommissionCharged { get; set; }
        public decimal CommissionReleased { get; set; }
        public decimal CommissionDebt { get; set; }
        public decimal CommissionOutstanding { get; set; }

        public decimal RequestedRefundAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal CancellationFee { get; set; }
        public decimal OwnerShare { get; set; }

        public decimal TopUpAmount { get; set; }
        public decimal NetOwnerAmount { get; set; }

        public decimal AvailableBalanceBefore { get; set; }
        public decimal AvailableBalanceAfter { get; set; }
        public decimal HeldBalanceBefore { get; set; }
        public decimal HeldBalanceAfter { get; set; }
        public decimal DebtBefore { get; set; }
        public decimal DebtAfter { get; set; }
    }

    public class FinancialDocumentDetailsDto
    {
        public string Currency { get; set; } = "ريال يمني";
        public string PlatformName { get; set; } = "دليلك اليمني";
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentTypeLabel { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int? BookingId { get; set; }
        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = "منشأة غير معروفة";
        public string? PlaceLogoUrl { get; set; }
        public string? PlaceAddress { get; set; }
        public string? PlacePhone { get; set; }
        public string? PlaceEmail { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = "عميل";
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public DateTime? BookingStartDate { get; set; }
        public DateTime? BookingEndDate { get; set; }
        // لقطة العرض المرتبطة بالحجز عند إصدار المستند.
        public int? PromotionId { get; set; }
        public decimal OriginalTotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalTotalPrice { get; set; }
        public string? PromotionTitleSnapshot { get; set; }
        public string? PromotionCodeSnapshot { get; set; }
        public string? DiscountTypeSnapshot { get; set; }
        public decimal? DiscountValueSnapshot { get; set; }
        public bool HasPromotion => DiscountAmount > 0m;
        public decimal FinalSavingsAmount =>
            Math.Round(
                Math.Max(0m, OriginalTotalPrice - FinalTotalPrice),
                2);
        public string PromotionTitleDisplay =>
            !string.IsNullOrWhiteSpace(PromotionTitleSnapshot)
                ? PromotionTitleSnapshot.Trim()
                : "عرض خاص";
        public string BookingStateLabel { get; set; } = "غير محددة";
        public string PaymentStatusLabel { get; set; } = "غير محددة";
        public string CommissionStatusLabel { get; set; } = "غير محددة";
        public string RecordStatusLabel { get; set; } = "غير محددة";
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public string VerificationText { get; set; } =
            "هذا المستند مولد إلكترونيًا من منصة دليلك اليمني.";
        public string GrandTotalLabel { get; set; } = "الإجمالي";
        public decimal GrandTotal { get; set; }
        public List<FinancialDocumentLineDto> Lines { get; set; } =
            new List<FinancialDocumentLineDto>();
        public List<FinancialDocumentAmountLineDto> AmountLines { get; set; } =
            new List<FinancialDocumentAmountLineDto>();
    }

    public class FinancialDocumentLineDto
    {
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public class FinancialDocumentAmountLineDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsHighlight { get; set; }
        public bool IsNegative { get; set; }
    }

    public class FinancialReportExportDto
    {
        public string PlatformName { get; set; } = "دليلك اليمني";
        public string Currency { get; set; } = "ريال يمني";
        public string ReportType { get; set; } =
            OwnerFinancialReportTypes.Comprehensive;
        public string Title { get; set; } = "التقرير المالي";
        public string Subtitle { get; set; } = string.Empty;
        public string SelectedPlaceName { get; set; } = "كل المنشآت";
        public string PeriodLabel { get; set; } = "كل الفترات";
        public string DateScopeLabel { get; set; } = string.Empty;
        public string FilterDescription { get; set; } = "بدون فلاتر إضافية";
        public string SuggestedFileName { get; set; } =
            "Dalilak-Financial-Report";
        public DateTime GeneratedAt { get; set; }
        public int TotalAvailableRows { get; set; }
        public bool IsTruncated { get; set; }
        public FinancialReportSummaryDto Summary { get; set; } =
            new FinancialReportSummaryDto();
        public ComprehensiveFinancialAnalyticsDto Analytics { get; set; } =
            new ComprehensiveFinancialAnalyticsDto();
        public FinancialReconciliationDto Reconciliation { get; set; } =
            new FinancialReconciliationDto();
        public List<FinancialReportExportColumnDto> Columns { get; set; } =
            new List<FinancialReportExportColumnDto>();
        public List<FinancialReportExportRowDto> Rows { get; set; } =
            new List<FinancialReportExportRowDto>();
        public List<FinancialReportExportSectionDto> Sections { get; set; } =
            new List<FinancialReportExportSectionDto>();
        public List<FinancialReportMetricDto> Metrics { get; set; } =
            new List<FinancialReportMetricDto>();
        public List<FinancialReportInsightDto> Insights { get; set; } =
            new List<FinancialReportInsightDto>();
        public List<FinancialReportBreakdownItemDto> PlaceBreakdown { get; set; } =
            new List<FinancialReportBreakdownItemDto>();
        public List<FinancialReportBreakdownItemDto> StatusBreakdown { get; set; } =
            new List<FinancialReportBreakdownItemDto>();
    }

    public class FinancialReportExportSectionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<FinancialReportExportColumnDto> Columns { get; set; } =
            new List<FinancialReportExportColumnDto>();
        public List<FinancialReportExportRowDto> Rows { get; set; } =
            new List<FinancialReportExportRowDto>();
    }

    public class FinancialReportExportColumnDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string DataType { get; set; } = "Text";
        public string Alignment { get; set; } = "Right";
        public double Width { get; set; } = 16d;
    }

    public class FinancialReportExportRowDto
    {
        public List<string> Cells { get; set; } = new List<string>();
        public List<FinancialReportExportCellDto> TypedCells { get; set; } =
            new List<FinancialReportExportCellDto>();
    }

    public class FinancialReportExportCellDto
    {
        public string Text { get; set; } = string.Empty;
        public string DataType { get; set; } = "Text";
        public decimal? DecimalValue { get; set; }
        public DateTime? DateValue { get; set; }
        public long? IntegerValue { get; set; }
    }

    public class FinancialReportMetricDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Hint { get; set; }
        public string Tone { get; set; } = "Neutral";
    }

    public class FinancialReportInsightDto
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-lightbulb";
        public string Tone { get; set; } = "Info";
    }

    public class FinancialReportBreakdownItemDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class FinancialExcelFileDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = "Dalilak-Financial-Report.xlsx";
        public string ContentType { get; set; } =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }


    /* =====================================================================
       DTOs الجديدة للنظام المالي المبسط (6 تبويبات)
       تبقى DTOs القديمة مؤقتًا حتى يتم تحويل الكنترولر والواجهات بالكامل.
       ===================================================================== */

    public class OwnerFinancialOverviewDto
    {
        public string Currency { get; set; } = "ريال يمني";
        public int? PlaceId { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal HeldBalance { get; set; }
        public decimal DebtAmount { get; set; }
        public decimal DebtLimit { get; set; }
        public DateTime? DebtDueAt { get; set; }
        public string AccountStatus { get; set; } = FinancialAccountStatuses.NeedsTopUp;
        public int BookingsCount { get; set; }
        public decimal TotalBookingValue { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal OwnerNet { get; set; }
        public decimal TotalRefunded { get; set; }
        public List<CommissionTransactionItemDto> RecentTransactions { get; set; } = new();
    }

    public class OwnerBookingAccountsPageDto
    {
        public string Currency { get; set; } = "ريال يمني";
        public BookingFinancialFilterDto Filter { get; set; } = new();
        public BookingAccountsSummaryDto Summary { get; set; } = new();
        public List<BookingFinancialItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class OwnerCommissionBalancePageDto
    {
        public string Currency { get; set; } = "ريال يمني";
        public PlaceCommissionAccountDto? Account { get; set; }
        public decimal MinimumTopUpAmount { get; set; }
        public List<PlatformWalletOptionDto> PlatformWallets { get; set; } = new();
        public List<CommissionTransactionItemDto> RecentTopUps { get; set; } = new();
        public List<CommissionTransactionItemDto> RecentTransactions { get; set; } = new();
    }

    public class OwnerRefundsPageDto
    {
        public string Currency { get; set; } = "ريال يمني";
        public RefundFilterDto Filter { get; set; } = new();
        public RefundsSummaryDto Summary { get; set; } = new();
        public List<RefundRequestItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class OwnerReceivingWalletsPageDto
    {
        public OwnerWalletsSummaryDto Summary { get; set; } = new();
        public List<OwnerWalletItemDto> Wallets { get; set; } = new();
        public List<SupportedWalletOptionDto> SupportedWallets { get; set; } = new();
    }

    public class OwnerFinancialReportsPageDto
    {
        public string Currency { get; set; } = "ريال يمني";
        public FinancialReportFilterDto Filter { get; set; } = new();
        public FinancialReportSummaryDto Summary { get; set; } = new();
        public List<FinancialReportRowDto> Rows { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class SetRefundDestinationRequestDto
    {
        [Range(1, int.MaxValue)]
        public int RefundRequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int RefundWalletId { get; set; }

        [Required, StringLength(100)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string BeneficiaryName { get; set; } = string.Empty;
    }

    public class MarkRefundTransferredRequestDto
    {
        [Range(1, int.MaxValue)]
        public int RefundRequestId { get; set; }

        [Required, StringLength(100)]
        public string ReferenceNumber { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }

    public class ConfirmRefundReceivedRequestDto
    {
        [Range(1, int.MaxValue)]
        public int RefundRequestId { get; set; }
    }

    /* =====================================================================
       عنصر عام للقوائم المنسدلة
       ===================================================================== */

    public class FinancialStatusOptionDto
    {
        public string Value { get; set; } =
            string.Empty;

        public string Label { get; set; } =
            string.Empty;

        public bool IsSelected { get; set; }
    }
}