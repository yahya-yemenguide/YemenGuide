using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.Entities;
using YemenGuide.Core.Constants;
using YemenGuide.Data;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// خدمة الإدارة المالية الموحدة.
    ///
    /// جميع تبويبات الأدمن تقرأ من هذه الخدمة، بينما العمليات المحاسبية
    /// الحساسة مثل اعتماد الشحن وحجز/خصم/تحرير العمولة تبقى في
    /// ICommissionFinanceService حتى لا يتكرر منطق الأرصدة.
    /// </summary>
    public sealed class AdminFinancialService : IAdminFinancialService
    {
        private const int DefaultPageSize = 25;
        private const int MaximumPageSize = 50;

        private static readonly HashSet<int> AllowedPageSizes =
            new()
            {
                10,
                25,
                50
            };

        private readonly AppDbContext _context;
        private readonly ICommissionFinanceService _commissionFinanceService;
        private readonly AdminFinancialExcelBuilder _excelBuilder;
        private readonly ILogger<AdminFinancialService> _logger;

        public AdminFinancialService(
            AppDbContext context,
            ICommissionFinanceService commissionFinanceService,
            AdminFinancialExcelBuilder excelBuilder,
            ILogger<AdminFinancialService> logger)
        {
            _context = context;
            _commissionFinanceService = commissionFinanceService;
            _excelBuilder = excelBuilder;
            _logger = logger;
        }

        /* ====================================================================
           Helpers
           ==================================================================== */

        private static bool IsState(
            string? value,
            string expected) =>
                string.Equals(
                    value?.Trim(),
                    expected,
                    StringComparison.OrdinalIgnoreCase);


        private static bool IsBookingPaymentConfirmed(
            Booking booking)
        {
            if (booking.PaymentApprovedAt.HasValue)
            {
                return true;
            }

            return IsState(booking.PaymentStatus, "PartiallyPaid") ||
                   IsState(booking.PaymentStatus, "Paid") ||
                   IsState(booking.PaymentStatus, "Completed") ||
                   IsState(booking.PaymentStatus, "Confirmed");
        }

        private static DateTime? GetFinancialSettlementDate(
            Booking booking,
            FinancialRecord? financial) =>
                financial?.CommissionChargedAt ??
                financial?.CommissionReleasedAt ??
                booking.CompletedAt ??
                financial?.UpdatedAt;

        private static string FirstNonEmpty(
            string fallback,
            params string?[] values) =>
                values.FirstOrDefault(value =>
                    !string.IsNullOrWhiteSpace(value))?.Trim() ??
                fallback;

        private static int NormalizePageNumber(
            int pageNumber) =>
                Math.Max(1, pageNumber);

        private static int NormalizePageSize(
            int pageSize) =>
                AllowedPageSizes.Contains(pageSize)
                    ? pageSize
                    : DefaultPageSize;

        private static (
        DateTime? FromDate,
        DateTime? ToDate)
        NormalizeDateRange(
            DateTime? fromDate,
            DateTime? toDate)
        {
            fromDate =
                fromDate?.Date;

            toDate =
                toDate?.Date;

            if (
                fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value >
                toDate.Value
            )
            {
                var temporaryDate =
                    fromDate;

                fromDate =
                    toDate;

                toDate =
                    temporaryDate;
            }

            return (
                fromDate,
                toDate);
        }
        private static string NormalizeSearch(
            string? value,
            int maximumLength = 120)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            return normalized.Length <= maximumLength
                ? normalized
                : normalized[..maximumLength];
        }

        private static string NormalizePlatformAccountNumber(
    string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalizedCharacters = value
                .Trim()
                .Select(character =>
                    character switch
                    {
                        '٠' or '۰' => '0',
                        '١' or '۱' => '1',
                        '٢' or '۲' => '2',
                        '٣' or '۳' => '3',
                        '٤' or '۴' => '4',
                        '٥' or '۵' => '5',
                        '٦' or '۶' => '6',
                        '٧' or '۷' => '7',
                        '٨' or '۸' => '8',
                        '٩' or '۹' => '9',
                        _ => character
                    })
                .Where(character =>
                    !char.IsWhiteSpace(character) &&
                    character != '-' &&
                    character != '–' &&
                    character != '—')
                .ToArray();

            return new string(
                normalizedCharacters);
        }

        private async Task<SystemSetting?> GetSettingsAsync(
            bool tracking,
            CancellationToken cancellationToken)
        {
            IQueryable<SystemSetting> query =
                _context.SystemSettings;

            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            return await query
                .OrderByDescending(setting => setting.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static string GetCurrency(SystemSetting? settings) => "ريال يمني";

        private static decimal EffectiveDebtLimit(
            PlaceCommissionAccount account,
            SystemSetting? settings) =>
                Math.Max(
                    0m,
                    account.DebtLimit ??
                    settings?.DefaultCommissionDebtLimit ??
                    0m);

        private static decimal EffectiveLowBalanceThreshold(
            PlaceCommissionAccount account,
            SystemSetting? settings) =>
                Math.Max(
                    0m,
                    account.LowBalanceThreshold ??
                    settings?.DefaultLowBalanceThreshold ??
                    0m);

        private static bool EffectiveAllowNegative(
            PlaceCommissionAccount account,
            SystemSetting? settings) =>
                account.AllowNegativeBalance ??
                settings?.AllowNegativeCommissionBalance ??
                false;

        private static string CalculateAccountStatus(
            PlaceCommissionAccount account,
            SystemSetting? settings)
        {
            var available =
                Math.Max(0m, account.AvailableBalance);

            var debt =
                Math.Max(0m, account.DebtAmount);

            var allowNegative =
                EffectiveAllowNegative(account, settings);

            var debtLimit =
                EffectiveDebtLimit(account, settings);

            var lowThreshold =
                EffectiveLowBalanceThreshold(account, settings);

            var suspendWhenInsufficient =
                settings?.SuspendBookingWhenBalanceInsufficient ??
                true;

            if (debt > 0m)
            {
                if (
                    suspendWhenInsufficient &&
                    (!allowNegative || debt >= debtLimit)
                )
                {
                    return AdminCommissionAccountStatuses.Suspended;
                }

                return AdminCommissionAccountStatuses.InDebt;
            }

            if (available <= 0m)
            {
                return suspendWhenInsufficient
                    ? AdminCommissionAccountStatuses.Suspended
                    : AdminCommissionAccountStatuses.NeedsTopUp;
            }

            return available <= lowThreshold
                ? AdminCommissionAccountStatuses.LowBalance
                : AdminCommissionAccountStatuses.Active;
        }

        private static void RefreshAccountStatus(
            PlaceCommissionAccount account,
            SystemSetting? settings)
        {
            account.AccountStatus =
                CalculateAccountStatus(account, settings);

            account.UpdatedAt = DateTime.Now;
        }

        private async Task EnsureAllPlaceAccountsAsync(
            CancellationToken cancellationToken)
        {
            var missingPlaceIds = await _context.Places
                .AsNoTracking()
                .Where(place =>
                    place.PlaceCommissionAccount == null)
                .Select(place => place.PlaceId)
                .ToListAsync(cancellationToken);

            if (missingPlaceIds.Count == 0)
            {
                return;
            }

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var now = DateTime.Now;

            foreach (var placeId in missingPlaceIds)
            {
                _context.PlaceCommissionAccounts.Add(
                    new PlaceCommissionAccount
                    {
                        PlaceId = placeId,
                        AvailableBalance = 0m,
                        HeldBalance = 0m,
                        DebtAmount = 0m,
                        AllowNegativeBalance =
                            settings?.AllowNegativeCommissionBalance ??
                            false,
                        DebtLimit =
                            settings?.DefaultCommissionDebtLimit ??
                            0m,
                        LowBalanceThreshold =
                            settings?.DefaultLowBalanceThreshold ??
                            0m,
                        AccountStatus =
                            AdminCommissionAccountStatuses.NeedsTopUp,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(
                    exception,
                    "تعذر إنشاء بعض حسابات ضمان العمولات تلقائيًا.");

                _context.ChangeTracker.Clear();
            }
        }

        private async Task<Dictionary<int, string>> GetUserNamesAsync(
            IEnumerable<int?> userIds,
            CancellationToken cancellationToken)
        {
            var ids = userIds
                .Where(id => id.HasValue && id.Value > 0)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            return await _context.Users
                .AsNoTracking()
                .Where(user => ids.Contains(user.UserId))
                .Select(user => new
                {
                    user.UserId,
                    Name = user.FullName ?? user.Username
                })
                .ToDictionaryAsync(
                    item => item.UserId,
                    item => string.IsNullOrWhiteSpace(item.Name)
                        ? "مستخدم"
                        : item.Name.Trim(),
                    cancellationToken);
        }

        private async Task<List<AdminFinancialOwnerOptionDto>>
            BuildOwnerOptionsAsync(
                int? selectedOwnerId,
                CancellationToken cancellationToken)
        {
            var owners = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId.HasValue)
                .GroupBy(place => place.OwnerId!.Value)
                .Select(group => new
                {
                    OwnerId = group.Key,
                    PlacesCount = group.Count()
                })
                .OrderBy(item => item.OwnerId)
                .ToListAsync(cancellationToken);

            var ownerIds = owners
                .Select(item => item.OwnerId)
                .ToList();

            var users = await _context.Users
                .AsNoTracking()
                .Where(user => ownerIds.Contains(user.UserId))
                .Select(user => new
                {
                    user.UserId,
                    user.FullName,
                    user.Username,
                    user.PhoneNumber,
                    user.ContactInfo
                })
                .ToDictionaryAsync(
                    user => user.UserId,
                    cancellationToken);

            return owners
                .Select(item =>
                {
                    users.TryGetValue(
                        item.OwnerId,
                        out var user);

                    return new AdminFinancialOwnerOptionDto
                    {
                        OwnerId = item.OwnerId,
                        OwnerName = FirstNonEmpty(
                            "مالك",
                            user?.FullName,
                            user?.Username),
                        PhoneNumber = user?.PhoneNumber?.Trim(),
                        ContactInfo = user?.ContactInfo?.Trim(),
                        PlacesCount = item.PlacesCount,
                        IsSelected =
                            selectedOwnerId == item.OwnerId
                    };
                })
                .OrderBy(item => item.OwnerName)
                .ToList();
        }

        private async Task<List<AdminFinancialPlaceOptionDto>>
            BuildPlaceOptionsAsync(
                int? selectedOwnerId,
                int? selectedPlaceId,
                CancellationToken cancellationToken)
        {
            await EnsureAllPlaceAccountsAsync(cancellationToken);

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var query = _context.Places
                .AsNoTracking()
                .Include(place => place.PlaceCommissionAccount)
                .Include(place => place.PlaceImages)
                .AsSplitQuery()
                .AsQueryable();

            if (selectedOwnerId.HasValue)
            {
                query = query.Where(place =>
                    place.OwnerId == selectedOwnerId.Value);
            }

            var places = await query
                .OrderBy(place => place.PlaceNameAndDesc)
                .ToListAsync(cancellationToken);

            var ownerNames = await GetUserNamesAsync(
                places.Select(place => place.OwnerId),
                cancellationToken);

            return places
                .Select(place =>
                {
                    var account = place.PlaceCommissionAccount;
                    var ownerName = place.OwnerId.HasValue &&
                        ownerNames.TryGetValue(
                            place.OwnerId.Value,
                            out var resolvedOwnerName)
                                ? resolvedOwnerName
                                : "بدون مالك";

                    var image = FirstNonEmpty(
                        string.Empty,
                        place.CoverImage,
                        place.LogoImage,
                        place.PlaceImages?
                            .OrderBy(item => item.ImageId)
                            .Select(item => item.ImagePath)
                            .FirstOrDefault());

                    return new AdminFinancialPlaceOptionDto
                    {
                        PlaceId = place.PlaceId,
                        OwnerId = place.OwnerId,
                        PlaceName = FirstNonEmpty(
                            "منشأة غير معروفة",
                            place.PlaceNameAndDesc),
                        OwnerName = ownerName,
                        PlaceImageUrl =
                            string.IsNullOrWhiteSpace(image)
                                ? null
                                : image,
                        AccountStatus = account == null
                            ? AdminCommissionAccountStatuses.NeedsTopUp
                            : string.IsNullOrWhiteSpace(account.AccountStatus)
                                ? CalculateAccountStatus(account, settings)
                                : account.AccountStatus.Trim(),
                        AvailableBalance =
                            account?.AvailableBalance ?? 0m,
                        HeldBalance =
                            account?.HeldBalance ?? 0m,
                        DebtAmount =
                            account?.DebtAmount ?? 0m,
                        IsSelected =
                            selectedPlaceId == place.PlaceId
                    };
                })
                .ToList();
        }

        private async Task<List<AdminPlatformWalletOptionDto>>
            BuildPlatformWalletOptionsAsync(
                int? selectedPlatformWalletId,
                CancellationToken cancellationToken)
        {
            return await _context.PlatformWallets
                .AsNoTracking()
                .Include(wallet => wallet.Wallet)
                .OrderByDescending(wallet => wallet.IsPrimary)
                .ThenBy(wallet => wallet.Wallet.WalletName)
                .ThenBy(wallet => wallet.AccountNumber)
                .Select(wallet => new AdminPlatformWalletOptionDto
                {
                    PlatformWalletId = wallet.PlatformWalletId,
                    WalletId = wallet.WalletId,
                    WalletName = wallet.Wallet.WalletName,
                    LogoPath = wallet.Wallet.LogoPath,
                    AccountNumber = wallet.AccountNumber,
                    BeneficiaryName = wallet.BeneficiaryName,
                    IsActive = wallet.IsActive == true,
                    IsPrimary = wallet.IsPrimary,
                    IsSelected =
                        selectedPlatformWalletId ==
                        wallet.PlatformWalletId
                })
                .ToListAsync(cancellationToken);
        }

        private static string TransactionTypeLabel(
            string? type)
        {
            if (IsState(type, AdminFinanceTransactionTypes.TopUp))
            {
                return "شحن رصيد";
            }

            if (IsState(type, AdminFinanceTransactionTypes.CommissionHold))
            {
                return "حجز عمولة";
            }

            if (IsState(type, AdminFinanceTransactionTypes.CommissionCharge))
            {
                return "خصم عمولة";
            }

            if (IsState(type, AdminFinanceTransactionTypes.CommissionRelease))
            {
                return "تحرير عمولة";
            }

            if (IsState(type, AdminFinanceTransactionTypes.Debt))
            {
                return "تسجيل دين";
            }

            if (IsState(type, AdminFinanceTransactionTypes.DebtPayment))
            {
                return "سداد دين";
            }

            if (IsState(type, AdminFinanceTransactionTypes.Adjustment))
            {
                return "تسوية إدارية";
            }

            return "حركة مالية";
        }

        private static string TransactionStatusLabel(
            string? status)
        {
            if (IsState(status, AdminFinanceTransactionStatuses.Pending))
            {
                return "قيد المراجعة";
            }

            if (IsState(status, AdminFinanceTransactionStatuses.Completed))
            {
                return "مكتملة";
            }

            if (IsState(status, AdminFinanceTransactionStatuses.Rejected))
            {
                return "مرفوضة";
            }

            if (IsState(status, AdminFinanceTransactionStatuses.Reversed))
            {
                return "معكوسة";
            }

            if (IsState(status, AdminFinanceTransactionStatuses.Failed))
            {
                return "فاشلة";
            }

            if (IsState(status, AdminFinanceTransactionStatuses.Cancelled))
            {
                return "ملغاة";
            }

            return "غير محددة";
        }

        private static string TransactionDirection(
            CommissionTransaction transaction)
        {
            var availableChange =
                transaction.AvailableBalanceAfter -
                transaction.AvailableBalanceBefore;

            if (availableChange > 0m)
            {
                return "Credit";
            }

            if (availableChange < 0m)
            {
                return "Debit";
            }

            var debtChange =
                transaction.DebtAfter -
                transaction.DebtBefore;

            if (debtChange > 0m)
            {
                return "DebtIncrease";
            }

            if (debtChange < 0m)
            {
                return "DebtDecrease";
            }

            return "Neutral";
        }

        private static string AccountStatusLabel(
            string? status)
        {
            if (IsState(status, AdminCommissionAccountStatuses.NeedsTopUp))
            {
                return "يحتاج شحن";
            }

            if (IsState(status, AdminCommissionAccountStatuses.Active))
            {
                return "نشط";
            }

            if (IsState(status, AdminCommissionAccountStatuses.LowBalance))
            {
                return "رصيد منخفض";
            }

            if (IsState(status, AdminCommissionAccountStatuses.InDebt))
            {
                return "مديون";
            }

            if (IsState(status, AdminCommissionAccountStatuses.Suspended))
            {
                return "موقوف";
            }

            return "غير محدد";
        }

        private static string CommissionStatusLabel(
            string? status)
        {
            if (IsState(status, AdminBookingCommissionStatuses.Calculated))
            {
                return "محسوبة";
            }

            if (IsState(status, AdminBookingCommissionStatuses.Held))
            {
                return "محجوزة";
            }

            if (IsState(status, AdminBookingCommissionStatuses.Charged))
            {
                return "مخصومة";
            }

            if (IsState(status, AdminBookingCommissionStatuses.Released))
            {
                return "محررة";
            }

            if (IsState(status, AdminBookingCommissionStatuses.Debt))
            {
                return "دين";
            }

            if (IsState(status, AdminBookingCommissionStatuses.NotApplicable))
            {
                return "غير مستحقة";
            }

            return "غير محددة";
        }

        private static List<AdminFinancialSelectOptionDto>
            BuildOptions(
                IEnumerable<string> values,
                Func<string, string> labelFactory,
                string? selectedValue)
        {
            return values
                .Select(value =>
                    new AdminFinancialSelectOptionDto
                    {
                        Value = value,
                        Label = labelFactory(value),
                        IsSelected = IsState(value, selectedValue ?? string.Empty)
                    })
                .ToList();
        }

        private static AdminFinancialPaginationDto BuildPagination(
            int pageNumber,
            int pageSize,
            int totalCount) =>
                new()
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

        /* ====================================================================
           Shell
           ==================================================================== */

        public async Task<AdminFinancialShellDto> GetShellAsync(
            string? selectedTab = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureAllPlaceAccountsAsync(cancellationToken);

            var normalizedTab =
                AdminFinancialTabs.All.Contains(
                    selectedTab ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                        ? selectedTab!.Trim().ToLowerInvariant()
                        : AdminFinancialTabs.Overview;

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var pendingTopUpsCount =
                await _context.CommissionTransactions
                    .AsNoTracking()
                    .CountAsync(transaction =>
                        transaction.TransactionType ==
                            AdminFinanceTransactionTypes.TopUp &&
                        transaction.Status ==
                            AdminCommissionTopUpStatuses.Pending,
                        cancellationToken);

            var accountCounts = await _context.PlaceCommissionAccounts
                .AsNoTracking()
                .GroupBy(account => account.AccountStatus)
                .Select(group => new
                {
                    Status = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            int CountStatus(string status) =>
                accountCounts
                    .Where(item => IsState(item.Status, status))
                    .Sum(item => item.Count);

            var model = new AdminFinancialShellDto
            {
                Currency = GetCurrency(settings),
                SelectedTab = normalizedTab,
                GeneratedAt = DateTime.Now,
                PendingTopUpsCount = pendingTopUpsCount,
                LowBalanceAccountsCount =
                    CountStatus(AdminCommissionAccountStatuses.LowBalance),
                InDebtAccountsCount =
                    CountStatus(AdminCommissionAccountStatuses.InDebt),
                SuspendedAccountsCount =
                    CountStatus(AdminCommissionAccountStatuses.Suspended)
            };

            model.TabCounters = new List<AdminFinancialTabCounterDto>
            {
                new()
                {
                    TabKey = AdminFinancialTabs.TopUps,
                    Count = model.PendingTopUpsCount,
                    IsAttentionRequired = model.PendingTopUpsCount > 0
                },
                new()
                {
                    TabKey = AdminFinancialTabs.CommissionGuarantee,
                    Count = model.LowBalanceAccountsCount +
                            model.InDebtAccountsCount +
                            model.SuspendedAccountsCount,
                    IsAttentionRequired =
                        model.InDebtAccountsCount > 0 ||
                        model.SuspendedAccountsCount > 0
                }
            };

            return model;
        }

        /* ====================================================================
           Overview
           ==================================================================== */

        public async Task<AdminFinancialOverviewPageDto> GetOverviewAsync(
            AdminFinancialOverviewFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminFinancialOverviewFilterDto();
            var normalizedDateRange =
     NormalizeDateRange(
         filter.FromDate,
         filter.ToDate);

            filter.FromDate =
                normalizedDateRange.FromDate;

            filter.ToDate =
                normalizedDateRange.ToDate;

            await EnsureAllPlaceAccountsAsync(cancellationToken);

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var placesQuery = _context.Places
                .AsNoTracking()
                .AsQueryable();

            if (filter.OwnerId.HasValue)
            {
                placesQuery = placesQuery.Where(place =>
                    place.OwnerId == filter.OwnerId.Value);
            }

            if (filter.PlaceId.HasValue)
            {
                placesQuery = placesQuery.Where(place =>
                    place.PlaceId == filter.PlaceId.Value);
            }

            var placeIds = await placesQuery
                .Select(place => place.PlaceId)
                .ToListAsync(cancellationToken);

            var model = new AdminFinancialOverviewPageDto
            {
                Currency = GetCurrency(settings),
                GeneratedAt = DateTime.Now,
                Filter = filter,
                Owners = await BuildOwnerOptionsAsync(
                    filter.OwnerId,
                    cancellationToken),
                Places = await BuildPlaceOptionsAsync(
                    filter.OwnerId,
                    filter.PlaceId,
                    cancellationToken),
                AccountStatusOptions = BuildOptions(
                    AdminCommissionAccountStatuses.All,
                    AccountStatusLabel,
                    filter.AccountStatus)
            };

            if (placeIds.Count == 0)
            {
                return model;
            }

            var accountsQuery = _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Where(account => placeIds.Contains(account.PlaceId));

            if (!string.IsNullOrWhiteSpace(filter.AccountStatus))
            {
                var accountStatus = filter.AccountStatus.Trim();
                accountsQuery = accountsQuery.Where(account =>
                    account.AccountStatus == accountStatus);
            }

            var accounts = await accountsQuery
                .Include(account => account.Place)
                .ToListAsync(cancellationToken);

            var recordsQuery = _context.FinancialRecords
                .AsNoTracking()
                .Where(record =>
                    placeIds.Contains(record.Booking.PlaceId));

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value;
                recordsQuery = recordsQuery.Where(record =>
                    record.Booking.CreatedAt.HasValue &&
                    record.Booking.CreatedAt.Value >= fromDate);
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive =
                    filter.ToDate.Value.AddDays(1);

                recordsQuery = recordsQuery.Where(record =>
                    record.Booking.CreatedAt.HasValue &&
                    record.Booking.CreatedAt.Value < toDateExclusive);
            }

            var records = await recordsQuery
                .Select(record => new
                {
                    record.BookingId,
                    record.PlatformCommission,
                    record.CommissionStatus,
                    CreatedAt = record.Booking.CreatedAt,
                    record.CommissionHeldAt,
                    record.CommissionChargedAt,
                    record.CommissionReleasedAt
                })
                .ToListAsync(cancellationToken);

            var topUpsQuery = _context.CommissionTransactions
                .AsNoTracking()
                .Where(transaction =>
                    placeIds.Contains(transaction.PlaceId) &&
                    transaction.TransactionType ==
                        AdminFinanceTransactionTypes.TopUp);

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value;
                topUpsQuery = topUpsQuery.Where(transaction =>
                    transaction.CreatedAt >= fromDate);
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive =
                    filter.ToDate.Value.AddDays(1);

                topUpsQuery = topUpsQuery.Where(transaction =>
                    transaction.CreatedAt < toDateExclusive);
            }

            var topUps = await topUpsQuery
                .Select(transaction => new
                {
                    transaction.Amount,
                    transaction.Status,
                    transaction.CreatedAt
                })
                .ToListAsync(cancellationToken);

            model.Summary = new AdminFinancialOverviewSummaryDto
            {
                TotalCommissionCalculated =
                    records.Sum(item => item.PlatformCommission),
                TotalCommissionHeld = records
                    .Where(item =>
                        IsState(
                            item.CommissionStatus,
                            AdminBookingCommissionStatuses.Held) ||
                        IsState(
                            item.CommissionStatus,
                            AdminBookingCommissionStatuses.Debt))
                    .Sum(item => item.PlatformCommission),
                TotalCommissionCharged = records
                    .Where(item => IsState(
                        item.CommissionStatus,
                        AdminBookingCommissionStatuses.Charged))
                    .Sum(item => item.PlatformCommission),
                TotalCommissionReleased = records
                    .Where(item => IsState(
                        item.CommissionStatus,
                        AdminBookingCommissionStatuses.Released))
                    .Sum(item => item.PlatformCommission),
                TotalCommissionDebt = records
                    .Where(item => IsState(
                        item.CommissionStatus,
                        AdminBookingCommissionStatuses.Debt))
                    .Sum(item => item.PlatformCommission),
                TotalAvailableGuaranteeBalance =
                    accounts.Sum(account => account.AvailableBalance),
                TotalHeldGuaranteeBalance =
                    accounts.Sum(account => account.HeldBalance),
                TotalCurrentDebt =
                    accounts.Sum(account => account.DebtAmount),
                TotalApprovedTopUps = topUps
                    .Where(item => IsState(
                        item.Status,
                        AdminCommissionTopUpStatuses.Completed))
                    .Sum(item => item.Amount),
                CommissionBookingsCount = records.Count,
                PendingTopUpsCount = topUps.Count(item =>
                    IsState(
                        item.Status,
                        AdminCommissionTopUpStatuses.Pending)),
                ActiveAccountsCount = accounts.Count(account =>
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.Active)),
                NeedsTopUpAccountsCount = accounts.Count(account =>
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.NeedsTopUp)),
                LowBalanceAccountsCount = accounts.Count(account =>
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.LowBalance)),
                InDebtAccountsCount = accounts.Count(account =>
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.InDebt)),
                SuspendedAccountsCount = accounts.Count(account =>
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.Suspended)),
                FailedTransactionsCount =
                    await _context.CommissionTransactions
                        .AsNoTracking()
                        .CountAsync(transaction =>
                            placeIds.Contains(transaction.PlaceId) &&
                            transaction.Status ==
                                AdminFinanceTransactionStatuses.Failed,
                            cancellationToken),
                UnsettledCommissionsCount = records.Count(item =>
                    IsState(
                        item.CommissionStatus,
                        AdminBookingCommissionStatuses.Calculated) ||
                    IsState(
                        item.CommissionStatus,
                        AdminBookingCommissionStatuses.Held) ||
                    IsState(
                        item.CommissionStatus,
                        AdminBookingCommissionStatuses.Debt))
            };

            var currentMonthStart =
                new DateTime(
                    DateTime.Today.Year,
                    DateTime.Today.Month,
                    1);

            var previousMonthStart =
                currentMonthStart.AddMonths(-1);

            model.Summary.CurrentMonthCommissionRevenue = records
                .Where(item =>
                    item.CommissionChargedAt.HasValue &&
                    item.CommissionChargedAt.Value >= currentMonthStart)
                .Sum(item => item.PlatformCommission);

            model.Summary.PreviousMonthCommissionRevenue = records
                .Where(item =>
                    item.CommissionChargedAt.HasValue &&
                    item.CommissionChargedAt.Value >= previousMonthStart &&
                    item.CommissionChargedAt.Value < currentMonthStart)
                .Sum(item => item.PlatformCommission);

            if (model.Summary.PreviousMonthCommissionRevenue > 0m)
            {
                model.Summary.CurrentMonthGrowthPercentage =
                    Math.Round(
                        (
                            model.Summary.CurrentMonthCommissionRevenue -
                            model.Summary.PreviousMonthCommissionRevenue
                        ) /
                        model.Summary.PreviousMonthCommissionRevenue *
                        100m,
                        2);
            }

            var trendStart = currentMonthStart.AddMonths(-5);

            for (var index = 0; index < 6; index++)
            {
                var monthStart = trendStart.AddMonths(index);
                var nextMonth = monthStart.AddMonths(1);

                var monthRecords = records
                    .Where(item =>
                        item.CreatedAt.HasValue &&
                        item.CreatedAt.Value >= monthStart &&
                        item.CreatedAt.Value < nextMonth)
                    .ToList();

                model.MonthlyTrend.Add(
                    new AdminFinancialTrendPointDto
                    {
                        Year = monthStart.Year,
                        Month = monthStart.Month,
                        Label = monthStart.ToString(
                            "MMMM yyyy",
                            CultureInfo.GetCultureInfo("ar-YE")),
                        CalculatedCommission = monthRecords
                            .Sum(item => item.PlatformCommission),
                        ChargedCommission = monthRecords
                            .Where(item => IsState(
                                item.CommissionStatus,
                                AdminBookingCommissionStatuses.Charged))
                            .Sum(item => item.PlatformCommission),
                        ReleasedCommission = monthRecords
                            .Where(item => IsState(
                                item.CommissionStatus,
                                AdminBookingCommissionStatuses.Released))
                            .Sum(item => item.PlatformCommission),
                        ApprovedTopUps = topUps
                            .Where(item =>
                                item.CreatedAt >= monthStart &&
                                item.CreatedAt < nextMonth &&
                                IsState(
                                    item.Status,
                                    AdminCommissionTopUpStatuses.Completed))
                            .Sum(item => item.Amount),
                        BookingsCount = monthRecords.Count
                    });
            }

            var totalAccounts = Math.Max(1, accounts.Count);

            model.AccountStatusBreakdown =
                AdminCommissionAccountStatuses.All
                    .Select(status =>
                    {
                        var statusAccounts = accounts
                            .Where(account =>
                                IsState(account.AccountStatus, status))
                            .ToList();

                        return new AdminFinancialStatusBreakdownDto
                        {
                            Status = status,
                            Label = AccountStatusLabel(status),
                            Count = statusAccounts.Count,
                            AvailableBalance = statusAccounts
                                .Sum(account => account.AvailableBalance),
                            HeldBalance = statusAccounts
                                .Sum(account => account.HeldBalance),
                            DebtAmount = statusAccounts
                                .Sum(account => account.DebtAmount),
                            Percentage = Math.Round(
                                statusAccounts.Count * 100m /
                                totalAccounts,
                                2)
                        };
                    })
                    .ToList();

            var recentTransactions = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(transaction => transaction.Place)
                .Include(transaction => transaction.CreatedByUser)
                .Where(transaction => placeIds.Contains(transaction.PlaceId))
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.CommissionTransactionId)
                .Take(10)
                .ToListAsync(cancellationToken);

            var recentOwnerNames = await GetUserNamesAsync(
                recentTransactions.Select(item => item.Place.OwnerId),
                cancellationToken);

            model.RecentTransactions = recentTransactions
                .Select(transaction =>
                    new AdminRecentFinancialMovementDto
                    {
                        CommissionTransactionId =
                            transaction.CommissionTransactionId,
                        PlaceId = transaction.PlaceId,
                        PlaceName = FirstNonEmpty(
                            "منشأة غير معروفة",
                            transaction.Place?.PlaceNameAndDesc),
                        OwnerName = transaction.Place?.OwnerId is int ownerId &&
                            recentOwnerNames.TryGetValue(ownerId, out var ownerName)
                                ? ownerName
                                : "بدون مالك",
                        BookingId = transaction.BookingId,
                        TransactionType = transaction.TransactionType,
                        TransactionTypeLabel =
                            TransactionTypeLabel(transaction.TransactionType),
                        Amount = transaction.Amount,
                        Status = transaction.Status,
                        StatusLabel =
                            TransactionStatusLabel(transaction.Status),
                        ReferenceNumber =
                            transaction.ReferenceNumber?.Trim(),
                        CreatedAt = transaction.CreatedAt ?? DateTime.MinValue,
                        CreatedByName = FirstNonEmpty(
                            string.Empty,
                            transaction.CreatedByUser?.FullName,
                            transaction.CreatedByUser?.Username)
                    })
                .ToList();

            var pendingTopUps = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(transaction => transaction.Place)
                .Where(transaction =>
                    placeIds.Contains(transaction.PlaceId) &&
                    transaction.TransactionType ==
                        AdminFinanceTransactionTypes.TopUp &&
                    transaction.Status ==
                        AdminCommissionTopUpStatuses.Pending)
                .OrderBy(transaction => transaction.CreatedAt)
                .Take(8)
                .ToListAsync(cancellationToken);

            var pendingOwnerNames = await GetUserNamesAsync(
                pendingTopUps.Select(item => item.Place.OwnerId),
                cancellationToken);

            var pendingReferences = pendingTopUps
                .Where(item => !string.IsNullOrWhiteSpace(item.ReferenceNumber))
                .Select(item => item.ReferenceNumber!.Trim())
                .Distinct()
                .ToList();

            var duplicateReferenceCounts = pendingReferences.Count == 0
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(item =>
                        item.TransactionType ==
                            AdminFinanceTransactionTypes.TopUp &&
                        item.ReferenceNumber != null &&
                        pendingReferences.Contains(item.ReferenceNumber))
                    .GroupBy(item => item.ReferenceNumber!)
                    .Select(group => new
                    {
                        Reference = group.Key,
                        Count = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.Reference,
                        item => item.Count,
                        cancellationToken);

            model.PendingTopUps = pendingTopUps
                .Select(item =>
                {
                    var reference = item.ReferenceNumber?.Trim();
                    var duplicate =
                        !string.IsNullOrWhiteSpace(reference) &&
                        duplicateReferenceCounts.TryGetValue(
                            reference,
                            out var count) &&
                        count > 1;

                    return new AdminPendingTopUpPreviewDto
                    {
                        CommissionTransactionId =
                            item.CommissionTransactionId,
                        PlaceId = item.PlaceId,
                        PlaceName = FirstNonEmpty(
                            "منشأة غير معروفة",
                            item.Place?.PlaceNameAndDesc),
                        OwnerId = item.Place?.OwnerId,
                        OwnerName = item.Place?.OwnerId is int ownerId &&
                            pendingOwnerNames.TryGetValue(ownerId, out var ownerName)
                                ? ownerName
                                : "بدون مالك",
                        Amount = item.Amount,
                        ReferenceNumber = reference,
                        TransferDate = item.TransferDate,
                        CreatedAt = item.CreatedAt ?? DateTime.MinValue,
                        HasReceipt =
                            !string.IsNullOrWhiteSpace(item.ReceiptImage),
                        HasDuplicateReference = duplicate
                    };
                })
                .ToList();

            var lowAccounts = accounts
                .Where(account =>
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.LowBalance) ||
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.NeedsTopUp) ||
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.InDebt) ||
                    IsState(
                        account.AccountStatus,
                        AdminCommissionAccountStatuses.Suspended))
                .OrderByDescending(account => account.DebtAmount)
                .ThenBy(account => account.AvailableBalance)
                .Take(8)
                .ToList();

            var lowOwnerNames = await GetUserNamesAsync(
                lowAccounts.Select(item => item.Place.OwnerId),
                cancellationToken);

            model.LowBalanceAccounts = lowAccounts
                .Select(account =>
                    new AdminLowBalanceAccountPreviewDto
                    {
                        PlaceCommissionAccountId =
                            account.PlaceCommissionAccountId,
                        PlaceId = account.PlaceId,
                        PlaceName = FirstNonEmpty(
                            "منشأة غير معروفة",
                            account.Place?.PlaceNameAndDesc),
                        OwnerName = account.Place?.OwnerId is int ownerId &&
                            lowOwnerNames.TryGetValue(ownerId, out var ownerName)
                                ? ownerName
                                : "بدون مالك",
                        AvailableBalance = account.AvailableBalance,
                        LowBalanceThreshold =
                            EffectiveLowBalanceThreshold(account, settings),
                        DebtAmount = account.DebtAmount,
                        AccountStatus = account.AccountStatus,
                        LastTopUpAt = account.LastTopUpAt
                    })
                .ToList();

            if (model.Summary.PendingTopUpsCount > 0)
            {
                model.Alerts.Add(new AdminFinancialOverviewAlertDto
                {
                    AlertKey = "PendingTopUps",
                    Severity = "Warning",
                    Title = "طلبات شحن تنتظر المراجعة",
                    Description = "راجع السندات واعتمد الطلبات الصحيحة.",
                    Count = model.Summary.PendingTopUpsCount,
                    TargetTab = AdminFinancialTabs.TopUps
                });
            }

            if (model.Summary.SuspendedAccountsCount > 0)
            {
                model.Alerts.Add(new AdminFinancialOverviewAlertDto
                {
                    AlertKey = "SuspendedAccounts",
                    Severity = "Danger",
                    Title = "حسابات ضمان موقوفة",
                    Description = "وصلت بعض المنشآت إلى حالة إيقاف مالي.",
                    Count = model.Summary.SuspendedAccountsCount,
                    Amount = model.Summary.TotalCurrentDebt,
                    TargetTab = AdminFinancialTabs.CommissionGuarantee,
                    TargetQuery = "AccountStatus=Suspended"
                });
            }

            if (model.Summary.FailedTransactionsCount > 0)
            {
                model.Alerts.Add(new AdminFinancialOverviewAlertDto
                {
                    AlertKey = "FailedTransactions",
                    Severity = "Danger",
                    Title = "حركات مالية فاشلة",
                    Description = "افتح سجل الحركات لمراجعة العمليات الفاشلة.",
                    Count = model.Summary.FailedTransactionsCount,
                    TargetTab = AdminFinancialTabs.Transactions,
                    TargetQuery = "Status=Failed"
                });
            }

            return model;
        }

        /* ====================================================================
           Top-up requests
           ==================================================================== */

        public async Task<AdminCommissionTopUpPageDto> GetTopUpsAsync(
            AdminCommissionTopUpFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminCommissionTopUpFilterDto();
            filter.PageNumber = NormalizePageNumber(filter.PageNumber);
            filter.PageSize = NormalizePageSize(filter.PageSize);
            filter.Search = NormalizeSearch(filter.Search);
            filter.ReferenceNumber = NormalizeSearch(filter.ReferenceNumber, 100);
            var normalizedDateRange =
     NormalizeDateRange(
         filter.FromDate,
         filter.ToDate);

            filter.FromDate =
                normalizedDateRange.FromDate;

            filter.ToDate =
                normalizedDateRange.ToDate;

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var query = _context.CommissionTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.TransactionType ==
                    AdminFinanceTransactionTypes.TopUp);

            if (filter.RequestId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.CommissionTransactionId ==
                    filter.RequestId.Value);
            }

            if (filter.OwnerId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.Place.OwnerId ==
                    filter.OwnerId.Value);
            }

            if (filter.PlaceId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.PlaceId ==
                    filter.PlaceId.Value);
            }

            if (filter.PlatformWalletId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.PlatformWalletId ==
                    filter.PlatformWalletId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = filter.Status.Trim();
                query = query.Where(transaction =>
                    transaction.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
            {
                var reference = filter.ReferenceNumber.Trim();
                query = query.Where(transaction =>
                    transaction.ReferenceNumber != null &&
                    transaction.ReferenceNumber.Contains(reference));
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value;

                query = filter.DateScope switch
                {
                    AdminFinancialDateScopes.TransferDate =>
                        query.Where(transaction =>
                            transaction.TransferDate.HasValue &&
                            transaction.TransferDate.Value >= fromDate),

                    AdminFinancialDateScopes.ReviewedAt =>
                        query.Where(transaction =>
                            transaction.ReviewedAt.HasValue &&
                            transaction.ReviewedAt.Value >= fromDate),

                    _ =>
                        query.Where(transaction =>
                            transaction.CreatedAt >= fromDate)
                };
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive = filter.ToDate.Value.AddDays(1);

                query = filter.DateScope switch
                {
                    AdminFinancialDateScopes.TransferDate =>
                        query.Where(transaction =>
                            transaction.TransferDate.HasValue &&
                            transaction.TransferDate.Value < toDateExclusive),

                    AdminFinancialDateScopes.ReviewedAt =>
                        query.Where(transaction =>
                            transaction.ReviewedAt.HasValue &&
                            transaction.ReviewedAt.Value < toDateExclusive),

                    _ =>
                        query.Where(transaction =>
                            transaction.CreatedAt < toDateExclusive)
                };
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                var hasNumericSearch = int.TryParse(search, out var numericSearch);

                query = query.Where(transaction =>
                    transaction.Place.PlaceNameAndDesc.Contains(search) ||
                    (transaction.ReferenceNumber != null &&
                     transaction.ReferenceNumber.Contains(search)) ||
                    (transaction.Notes != null &&
                     transaction.Notes.Contains(search)) ||
                    (hasNumericSearch &&
                     transaction.CommissionTransactionId == numericSearch));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var aggregate = await query
                .GroupBy(_ => 1)
                .Select(group => new AdminCommissionTopUpSummaryDto
                {
                    TotalCount = group.Count(),
                    PendingCount = group.Count(item =>
                        item.Status == AdminCommissionTopUpStatuses.Pending),
                    CompletedCount = group.Count(item =>
                        item.Status == AdminCommissionTopUpStatuses.Completed),
                    RejectedCount = group.Count(item =>
                        item.Status == AdminCommissionTopUpStatuses.Rejected),
                    CancelledCount = group.Count(item =>
                        item.Status == AdminCommissionTopUpStatuses.Cancelled),
                    FailedCount = group.Count(item =>
                        item.Status == AdminCommissionTopUpStatuses.Failed),
                    PendingAmount = group
                        .Where(item =>
                            item.Status == AdminCommissionTopUpStatuses.Pending)
                        .Sum(item => item.Amount),
                    CompletedAmount = group
                        .Where(item =>
                            item.Status == AdminCommissionTopUpStatuses.Completed)
                        .Sum(item => item.Amount),
                    RejectedAmount = group
                        .Where(item =>
                            item.Status == AdminCommissionTopUpStatuses.Rejected)
                        .Sum(item => item.Amount),
                    DebtPaidAmount = group
                        .Where(item =>
                            item.Status == AdminCommissionTopUpStatuses.Completed)
                        .Sum(item =>
                            item.DebtBefore > item.DebtAfter
                                ? item.DebtBefore - item.DebtAfter
                                : 0m),
                    AddedToAvailableBalanceAmount = group
                        .Where(item =>
                            item.Status == AdminCommissionTopUpStatuses.Completed)
                        .Sum(item =>
                            item.AvailableBalanceAfter > item.AvailableBalanceBefore
                                ? item.AvailableBalanceAfter - item.AvailableBalanceBefore
                                : 0m)
                })
                .FirstOrDefaultAsync(cancellationToken) ??
                new AdminCommissionTopUpSummaryDto();

            var todayStart = DateTime.Today;
            var tomorrow = todayStart.AddDays(1);

            aggregate.TodayCount = await query.CountAsync(item =>
                item.CreatedAt >= todayStart &&
                item.CreatedAt < tomorrow,
                cancellationToken);

            aggregate.DuplicateReferenceCount = await query
                .Where(item => item.ReferenceNumber != null && item.ReferenceNumber != string.Empty)
                .GroupBy(item => item.ReferenceNumber)
                .Where(group => group.Count() > 1)
                .CountAsync(cancellationToken);

            var totalPages = totalCount <= 0
                ? 0
                : (int)Math.Ceiling((double)totalCount / filter.PageSize);

            if (totalPages > 0 && filter.PageNumber > totalPages)
            {
                filter.PageNumber = totalPages;
            }

            var items = await query
                .Include(transaction => transaction.Place)
                .Include(transaction => transaction.PlatformWallet)
                    .ThenInclude(wallet => wallet.Wallet)
                .Include(transaction => transaction.CreatedByUser)
                .Include(transaction => transaction.ReviewedByUser)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.CommissionTransactionId)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            var ownerNames = await GetUserNamesAsync(
                items.Select(item => item.Place.OwnerId),
                cancellationToken);

            var references = items
                .Where(item => !string.IsNullOrWhiteSpace(item.ReferenceNumber))
                .Select(item => item.ReferenceNumber!.Trim())
                .Distinct()
                .ToList();

            var referenceCounts = references.Count == 0
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(item =>
                        item.TransactionType == AdminFinanceTransactionTypes.TopUp &&
                        item.ReferenceNumber != null &&
                        references.Contains(item.ReferenceNumber))
                    .GroupBy(item => item.ReferenceNumber!)
                    .Select(group => new
                    {
                        Reference = group.Key,
                        Count = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.Reference,
                        item => item.Count,
                        cancellationToken);

            var rows = items
                .Select(item =>
                {
                    var reference = item.ReferenceNumber?.Trim();
                    var debtPaid = Math.Max(0m, item.DebtBefore - item.DebtAfter);
                    var credited = Math.Max(
                        0m,
                        item.AvailableBalanceAfter -
                        item.AvailableBalanceBefore);

                    return new AdminCommissionTopUpRowDto
                    {
                        CommissionTransactionId = item.CommissionTransactionId,
                        PlaceId = item.PlaceId,
                        PlaceName = FirstNonEmpty(
                            "منشأة غير معروفة",
                            item.Place?.PlaceNameAndDesc),
                        PlaceImageUrl = FirstNonEmpty(
                            string.Empty,
                            item.Place?.CoverImage,
                            item.Place?.LogoImage),
                        OwnerId = item.Place?.OwnerId,
                        OwnerName = item.Place?.OwnerId is int ownerId &&
                            ownerNames.TryGetValue(ownerId, out var ownerName)
                                ? ownerName
                                : "بدون مالك",
                        PlatformWalletId = item.PlatformWalletId,
                        PlatformWalletName = FirstNonEmpty(
                            string.Empty,
                            item.PlatformWallet?.Wallet?.WalletName),
                        PlatformAccountNumber =
                            item.PlatformWallet?.AccountNumber?.Trim() ?? string.Empty,
                        PlatformBeneficiaryName =
                            item.PlatformWallet?.BeneficiaryName?.Trim() ?? string.Empty,
                        Amount = item.Amount,
                        DebtBefore = item.DebtBefore,
                        DebtAfter = item.DebtAfter,
                        AvailableBalanceBefore = item.AvailableBalanceBefore,
                        AvailableBalanceAfter = item.AvailableBalanceAfter,
                        HeldBalanceBefore = item.HeldBalanceBefore,
                        HeldBalanceAfter = item.HeldBalanceAfter,
                        DebtPaymentAmount = debtPaid,
                        CreditedToAvailableBalance = credited,
                        Status = item.Status,
                        StatusLabel = TransactionStatusLabel(item.Status),
                        ReferenceNumber = reference,
                        TransferDate = item.TransferDate,
                        ReceiptImage = item.ReceiptImage?.Trim(),
                        CreatedAt = item.CreatedAt ?? DateTime.MinValue,
                        CreatedByUserId = item.CreatedByUserId,
                        CreatedByName = FirstNonEmpty(
                            string.Empty,
                            item.CreatedByUser?.FullName,
                            item.CreatedByUser?.Username),
                        ReviewedAt = item.ReviewedAt,
                        ReviewedByUserId = item.ReviewedByUserId,
                        ReviewedByName = FirstNonEmpty(
                            string.Empty,
                            item.ReviewedByUser?.FullName,
                            item.ReviewedByUser?.Username),
                        RejectionReason = item.RejectionReason?.Trim(),
                        Notes = item.Notes?.Trim(),
                        DuplicateReferenceCount =
                            !string.IsNullOrWhiteSpace(reference) &&
                            referenceCounts.TryGetValue(reference, out var count)
                                ? count
                                : 0
                    };
                })
                .ToList();

            return new AdminCommissionTopUpPageDto
            {
                Currency = GetCurrency(settings),
                MinimumTopUpAmount = settings?.MinimumCommissionTopUp ?? 0m,
                Filter = filter,
                Summary = aggregate,
                Result = new AdminPagedResultDto<AdminCommissionTopUpRowDto>
                {
                    Items = rows,
                    Pagination = BuildPagination(
                        filter.PageNumber,
                        filter.PageSize,
                        totalCount)
                },
                Owners = await BuildOwnerOptionsAsync(
                    filter.OwnerId,
                    cancellationToken),
                Places = await BuildPlaceOptionsAsync(
                    filter.OwnerId,
                    filter.PlaceId,
                    cancellationToken),
                PlatformWallets = await BuildPlatformWalletOptionsAsync(
                    filter.PlatformWalletId,
                    cancellationToken),
                StatusOptions = BuildOptions(
                    AdminCommissionTopUpStatuses.All,
                    TransactionStatusLabel,
                    filter.Status),
                DateScopeOptions = new List<AdminFinancialSelectOptionDto>
                {
                    new()
                    {
                        Value = AdminFinancialDateScopes.CreatedAt,
                        Label = "تاريخ إرسال الطلب",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.CreatedAt)
                    },
                    new()
                    {
                        Value = AdminFinancialDateScopes.TransferDate,
                        Label = "تاريخ التحويل",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.TransferDate)
                    },
                    new()
                    {
                        Value = AdminFinancialDateScopes.ReviewedAt,
                        Label = "تاريخ المراجعة",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.ReviewedAt)
                    }
                }
            };
        }

        public async Task<AdminCommissionTopUpDetailsDto?> GetTopUpDetailsAsync(
            int commissionTransactionId,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return null;
            }

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var item = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(transaction => transaction.Place)
                    .ThenInclude(place => place.PlaceCommissionAccount)
                .Include(transaction => transaction.PlatformWallet)
                    .ThenInclude(wallet => wallet.Wallet)
                .Include(transaction => transaction.CreatedByUser)
                .Include(transaction => transaction.ReviewedByUser)
                .FirstOrDefaultAsync(transaction =>
                    transaction.CommissionTransactionId ==
                        commissionTransactionId &&
                    transaction.TransactionType ==
                        AdminFinanceTransactionTypes.TopUp,
                    cancellationToken);

            if (item == null)
            {
                return null;
            }

            var ownerNames = await GetUserNamesAsync(
                new[] { item.Place?.OwnerId },
                cancellationToken);

            var reference = item.ReferenceNumber?.Trim();
            var duplicateCount = string.IsNullOrWhiteSpace(reference)
                ? 0
                : await _context.CommissionTransactions
                    .AsNoTracking()
                    .CountAsync(transaction =>
                        transaction.TransactionType ==
                            AdminFinanceTransactionTypes.TopUp &&
                        transaction.ReferenceNumber == reference,
                        cancellationToken);

            var related = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(transaction => transaction.Place)
                .Include(transaction => transaction.CreatedByUser)
                .Where(transaction =>
                    transaction.PlaceId == item.PlaceId)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.CommissionTransactionId)
                .Take(15)
                .ToListAsync(cancellationToken);

            var account = item.Place?.PlaceCommissionAccount;

            return new AdminCommissionTopUpDetailsDto
            {
                CommissionTransactionId = item.CommissionTransactionId,
                PlaceId = item.PlaceId,
                PlaceName = FirstNonEmpty(
                    "منشأة غير معروفة",
                    item.Place?.PlaceNameAndDesc),
                PlaceImageUrl = FirstNonEmpty(
                    string.Empty,
                    item.Place?.CoverImage,
                    item.Place?.LogoImage),
                OwnerId = item.Place?.OwnerId,
                OwnerName = item.Place?.OwnerId is int ownerId &&
                    ownerNames.TryGetValue(ownerId, out var ownerName)
                        ? ownerName
                        : "بدون مالك",
                PlatformWalletId = item.PlatformWalletId,
                PlatformWalletName = FirstNonEmpty(
                    string.Empty,
                    item.PlatformWallet?.Wallet?.WalletName),
                PlatformAccountNumber =
                    item.PlatformWallet?.AccountNumber?.Trim() ?? string.Empty,
                PlatformBeneficiaryName =
                    item.PlatformWallet?.BeneficiaryName?.Trim() ?? string.Empty,
                Amount = item.Amount,
                DebtBefore = item.DebtBefore,
                DebtAfter = item.DebtAfter,
                AvailableBalanceBefore = item.AvailableBalanceBefore,
                AvailableBalanceAfter = item.AvailableBalanceAfter,
                HeldBalanceBefore = item.HeldBalanceBefore,
                HeldBalanceAfter = item.HeldBalanceAfter,
                DebtPaymentAmount = Math.Max(0m, item.DebtBefore - item.DebtAfter),
                CreditedToAvailableBalance = Math.Max(
                    0m,
                    item.AvailableBalanceAfter - item.AvailableBalanceBefore),
                Status = item.Status,
                StatusLabel = TransactionStatusLabel(item.Status),
                ReferenceNumber = reference,
                TransferDate = item.TransferDate,
                ReceiptImage = item.ReceiptImage?.Trim(),
                CreatedAt = item.CreatedAt ?? DateTime.MinValue,
                CreatedByUserId = item.CreatedByUserId,
                CreatedByName = FirstNonEmpty(
                    string.Empty,
                    item.CreatedByUser?.FullName,
                    item.CreatedByUser?.Username),
                ReviewedAt = item.ReviewedAt,
                ReviewedByUserId = item.ReviewedByUserId,
                ReviewedByName = FirstNonEmpty(
                    string.Empty,
                    item.ReviewedByUser?.FullName,
                    item.ReviewedByUser?.Username),
                RejectionReason = item.RejectionReason?.Trim(),
                Notes = item.Notes?.Trim(),
                DuplicateReferenceCount = duplicateCount,
                PlaceStatus = item.Place?.Status?.Trim(),
                PlaceIsAvailable = item.Place?.IsAvailable ?? false,
                AccountStatus = account?.AccountStatus ??
                    AdminCommissionAccountStatuses.NeedsTopUp,
                CurrentAvailableBalance = account?.AvailableBalance ?? 0m,
                CurrentHeldBalance = account?.HeldBalance ?? 0m,
                CurrentDebtAmount = account?.DebtAmount ?? 0m,
                EffectiveMinimumTopUpAmount =
                    settings?.MinimumCommissionTopUp ?? 0m,
                RelatedTransactions = related
                    .Select(transaction =>
                        new AdminRecentFinancialMovementDto
                        {
                            CommissionTransactionId =
                                transaction.CommissionTransactionId,
                            PlaceId = transaction.PlaceId,
                            PlaceName = FirstNonEmpty(
                                "منشأة غير معروفة",
                                transaction.Place?.PlaceNameAndDesc),
                            OwnerName = OwnerNameFromDictionary(
                                transaction.Place?.OwnerId,
                                ownerNames),
                            BookingId = transaction.BookingId,
                            TransactionType = transaction.TransactionType,
                            TransactionTypeLabel =
                                TransactionTypeLabel(transaction.TransactionType),
                            Amount = transaction.Amount,
                            Status = transaction.Status,
                            StatusLabel =
                                TransactionStatusLabel(transaction.Status),
                            ReferenceNumber =
                                transaction.ReferenceNumber?.Trim(),
                            CreatedAt = transaction.CreatedAt ?? DateTime.MinValue,
                            CreatedByName = FirstNonEmpty(
                                string.Empty,
                                transaction.CreatedByUser?.FullName,
                                transaction.CreatedByUser?.Username)
                        })
                    .ToList()
            };
        }

        private static string OwnerNameFromDictionary(
            int? ownerId,
            IReadOnlyDictionary<int, string> ownerNames)
        {
            return ownerId.HasValue &&
                ownerNames.TryGetValue(ownerId.Value, out var ownerName)
                    ? ownerName
                    : "بدون مالك";
        }

        public async Task<FinanceOperationResult> ApproveTopUpAsync(
            AdminApproveTopUpRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.CommissionTransactionId <= 0)
            {
                return FinanceOperationResult.Failed("بيانات طلب الشحن غير صحيحة.");
            }

            return await _commissionFinanceService.ApproveTopUpAsync(
                request.CommissionTransactionId,
                adminUserId,
                request.ReviewNote,
                cancellationToken);
        }

        public async Task<FinanceOperationResult> RejectTopUpAsync(
            AdminRejectTopUpRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.CommissionTransactionId <= 0)
            {
                return FinanceOperationResult.Failed("بيانات طلب الشحن غير صحيحة.");
            }

            return await _commissionFinanceService.RejectTopUpAsync(
                request.CommissionTransactionId,
                adminUserId,
                request.RejectionReason,
                cancellationToken);
        }

        /* ====================================================================
           Commission accounts
           ==================================================================== */


        /* ====================================================================
           Compatibility with the current five-tab admin finance interface.
           These methods compose the already existing account/commission methods
           without changing any balance or accounting logic.
           ==================================================================== */

        public async Task<AdminCommissionGuaranteePageDto> GetCommissionGuaranteeAsync(
            AdminCommissionGuaranteeFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminCommissionGuaranteeFilterDto();

            var accounts = await GetCommissionAccountsAsync(
                new AdminCommissionAccountFilterDto
                {
                    OwnerId = filter.OwnerId,
                    PlaceId = filter.PlaceId,
                    AccountStatus = filter.AccountStatus,
                    HasDebt = filter.HasDebt,
                    IsLowBalance = filter.IsLowBalance,
                    IsBookingSuspended = filter.IsBookingSuspended,
                    Search = filter.Search,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize
                },
                cancellationToken);

            var commissions = await GetBookingCommissionsAsync(
                new AdminBookingCommissionFilterDto
                {
                    OwnerId = filter.OwnerId,
                    PlaceId = filter.PlaceId,
                    HasDebt = filter.HasDebt,
                    Search = filter.Search,
                    PageNumber = 1,
                    PageSize = 10
                },
                cancellationToken);

            return new AdminCommissionGuaranteePageDto
            {
                Currency = accounts.Currency,
                Filter = filter,
                Accounts = accounts,
                CommissionSummary = commissions.Summary,
                RecentCommissions = commissions.Result.Items
                    .Take(10)
                    .ToList()
            };
        }

        public async Task<AdminCommissionGuaranteeDetailsDto?> GetCommissionGuaranteeDetailsAsync(
            int placeCommissionAccountId,
            CancellationToken cancellationToken = default)
        {
            var account = await GetCommissionAccountDetailsAsync(
                placeCommissionAccountId,
                cancellationToken);

            if (account == null)
            {
                return null;
            }

            var commissions = await GetBookingCommissionsAsync(
                new AdminBookingCommissionFilterDto
                {
                    OwnerId = account.OwnerId,
                    PlaceId = account.PlaceId,
                    PageNumber = 1,
                    PageSize = 10
                },
                cancellationToken);

            return new AdminCommissionGuaranteeDetailsDto
            {
                Currency = commissions.Currency,
                Account = account,
                CommissionSummary = commissions.Summary,
                RecentCommissions = commissions.Result.Items
                    .Take(10)
                    .ToList()
            };
        }

        public async Task<AdminCommissionAccountPageDto> GetCommissionAccountsAsync(
            AdminCommissionAccountFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminCommissionAccountFilterDto();
            filter.PageNumber = NormalizePageNumber(filter.PageNumber);
            filter.PageSize = NormalizePageSize(filter.PageSize);
            filter.Search = NormalizeSearch(filter.Search);

            await EnsureAllPlaceAccountsAsync(cancellationToken);

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var query = _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Include(account => account.Place)
                .AsQueryable();

            if (filter.OwnerId.HasValue)
            {
                query = query.Where(account =>
                    account.Place.OwnerId == filter.OwnerId.Value);
            }

            if (filter.PlaceId.HasValue)
            {
                query = query.Where(account =>
                    account.PlaceId == filter.PlaceId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.AccountStatus))
            {
                var status = filter.AccountStatus.Trim();
                query = query.Where(account =>
                    account.AccountStatus == status);
            }

            if (filter.AllowNegativeBalance.HasValue)
            {
                var allowNegative = filter.AllowNegativeBalance.Value;
                var globalAllowNegative =
                    settings?.AllowNegativeCommissionBalance ?? false;

                query = query.Where(account =>
                    (account.AllowNegativeBalance ?? globalAllowNegative) ==
                    allowNegative);
            }

            if (filter.HasDebt.HasValue)
            {
                query = filter.HasDebt.Value
                    ? query.Where(account => account.DebtAmount > 0m)
                    : query.Where(account => account.DebtAmount <= 0m);
            }

            if (filter.IsLowBalance == true)
            {
                var globalThreshold =
                    settings?.DefaultLowBalanceThreshold ?? 0m;

                query = query.Where(account =>
                    account.AvailableBalance <=
                    (account.LowBalanceThreshold ?? globalThreshold));
            }

            if (filter.IsBookingSuspended.HasValue)
            {
                query = filter.IsBookingSuspended.Value
                    ? query.Where(account =>
                        account.AccountStatus ==
                        AdminCommissionAccountStatuses.Suspended)
                    : query.Where(account =>
                        account.AccountStatus !=
                        AdminCommissionAccountStatuses.Suspended);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                var numeric = int.TryParse(search, out var numericSearch);

                query = query.Where(account =>
                    account.Place.PlaceNameAndDesc.Contains(search) ||
                    (numeric && account.PlaceId == numericSearch));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var globalAllowNegativeForSummary =
                settings?.AllowNegativeCommissionBalance ?? false;

            var summary = await query
                .GroupBy(_ => 1)
                .Select(group => new AdminCommissionAccountSummaryDto
                {
                    TotalCount = group.Count(),
                    NeedsTopUpCount = group.Count(account =>
                        account.AccountStatus ==
                        AdminCommissionAccountStatuses.NeedsTopUp),
                    ActiveCount = group.Count(account =>
                        account.AccountStatus ==
                        AdminCommissionAccountStatuses.Active),
                    LowBalanceCount = group.Count(account =>
                        account.AccountStatus ==
                        AdminCommissionAccountStatuses.LowBalance),
                    InDebtCount = group.Count(account =>
                        account.AccountStatus ==
                        AdminCommissionAccountStatuses.InDebt),
                    SuspendedCount = group.Count(account =>
                        account.AccountStatus ==
                        AdminCommissionAccountStatuses.Suspended),
                    AllowNegativeCount = group.Count(account =>
                        account.AllowNegativeBalance ??
                        globalAllowNegativeForSummary),
                    TotalAvailableBalance = group.Sum(account =>
                        account.AvailableBalance),
                    TotalHeldBalance = group.Sum(account =>
                        account.HeldBalance),
                    TotalDebtAmount = group.Sum(account =>
                        account.DebtAmount)
                })
                .FirstOrDefaultAsync(cancellationToken) ??
                new AdminCommissionAccountSummaryDto();

            var totalPages = totalCount <= 0
                ? 0
                : (int)Math.Ceiling((double)totalCount / filter.PageSize);

            if (totalPages > 0 && filter.PageNumber > totalPages)
            {
                filter.PageNumber = totalPages;
            }

            var accounts = await query
                .OrderByDescending(account => account.DebtAmount)
                .ThenBy(account => account.AvailableBalance)
                .ThenBy(account => account.Place.PlaceNameAndDesc)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            var ownerNames = await GetUserNamesAsync(
                accounts.Select(account => account.Place.OwnerId),
                cancellationToken);

            var placeIds = accounts.Select(account => account.PlaceId).ToList();

            var lastTransactions = placeIds.Count == 0
                ? new List<CommissionTransaction>()
                : await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(transaction => placeIds.Contains(transaction.PlaceId))
                    .OrderByDescending(transaction => transaction.CreatedAt)
                    .ThenByDescending(transaction => transaction.CommissionTransactionId)
                    .ToListAsync(cancellationToken);

            var lastTransactionByPlace = lastTransactions
                .GroupBy(transaction => transaction.PlaceId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First());

            var rows = accounts
                .Select(account =>
                {
                    lastTransactionByPlace.TryGetValue(
                        account.PlaceId,
                        out var lastTransaction);

                    var effectiveAllowNegative =
                        EffectiveAllowNegative(account, settings);

                    var effectiveDebtLimit =
                        EffectiveDebtLimit(account, settings);

                    var effectiveThreshold =
                        EffectiveLowBalanceThreshold(account, settings);

                    var status = string.IsNullOrWhiteSpace(account.AccountStatus)
                        ? CalculateAccountStatus(account, settings)
                        : account.AccountStatus.Trim();

                    return new AdminCommissionAccountRowDto
                    {
                        PlaceCommissionAccountId =
                            account.PlaceCommissionAccountId,
                        PlaceId = account.PlaceId,
                        PlaceName = FirstNonEmpty(
                            "منشأة غير معروفة",
                            account.Place?.PlaceNameAndDesc),
                        PlaceImageUrl = FirstNonEmpty(
                            string.Empty,
                            account.Place?.CoverImage,
                            account.Place?.LogoImage),
                        PlaceStatus = account.Place?.Status?.Trim(),
                        PlaceIsAvailable = account.Place?.IsAvailable ?? false,
                        OwnerId = account.Place?.OwnerId,
                        OwnerName = OwnerNameFromDictionary(
                            account.Place?.OwnerId,
                            ownerNames),
                        AvailableBalance = account.AvailableBalance,
                        HeldBalance = account.HeldBalance,
                        DebtAmount = account.DebtAmount,
                        AllowNegativeBalanceOverride =
                            account.AllowNegativeBalance,
                        DebtLimitOverride = account.DebtLimit,
                        LowBalanceThresholdOverride =
                            account.LowBalanceThreshold,
                        EffectiveAllowNegativeBalance =
                            effectiveAllowNegative,
                        EffectiveDebtLimit = effectiveDebtLimit,
                        EffectiveLowBalanceThreshold =
                            effectiveThreshold,
                        EffectiveMinimumTopUpAmount =
                            settings?.MinimumCommissionTopUp ?? 0m,
                        EffectiveDebtGraceDays =
                            settings?.CommissionDebtGraceDays ?? 0,
                        AccountStatus = status,
                        IsBookingSuspended =
                            IsState(
                                status,
                                AdminCommissionAccountStatuses.Suspended) &&
                            (settings?.SuspendBookingWhenBalanceInsufficient ?? true),
                        IsPlaceHiddenByFinancialPolicy =
                            IsState(
                                status,
                                AdminCommissionAccountStatuses.Suspended) &&
                            (settings?.HidePlaceWhenCommissionSuspended ?? false),
                        LastTopUpAt = account.LastTopUpAt,
                        CreatedAt = account.CreatedAt ?? DateTime.MinValue,
                        UpdatedAt = account.UpdatedAt,
                        LastTransactionAt = lastTransaction?.CreatedAt,
                        LastTransactionType =
                            lastTransaction?.TransactionType,
                        LastTransactionAmount =
                            lastTransaction?.Amount
                    };
                })
                .ToList();

            return new AdminCommissionAccountPageDto
            {
                Currency = GetCurrency(settings),
                GlobalMinimumTopUpAmount =
                    settings?.MinimumCommissionTopUp ?? 0m,
                GlobalLowBalanceThreshold =
                    settings?.DefaultLowBalanceThreshold ?? 0m,
                GlobalAllowNegativeBalance =
                    settings?.AllowNegativeCommissionBalance ?? false,
                GlobalDebtLimit =
                    settings?.DefaultCommissionDebtLimit ?? 0m,
                GlobalDebtGraceDays =
                    settings?.CommissionDebtGraceDays ?? 0,
                SuspendBookingWhenBalanceInsufficient =
                    settings?.SuspendBookingWhenBalanceInsufficient ?? true,
                HidePlaceWhenCommissionSuspended =
                    settings?.HidePlaceWhenCommissionSuspended ?? false,
                Filter = filter,
                Summary = summary,
                Result = new AdminPagedResultDto<AdminCommissionAccountRowDto>
                {
                    Items = rows,
                    Pagination = BuildPagination(
                        filter.PageNumber,
                        filter.PageSize,
                        totalCount)
                },
                Owners = await BuildOwnerOptionsAsync(
                    filter.OwnerId,
                    cancellationToken),
                Places = await BuildPlaceOptionsAsync(
                    filter.OwnerId,
                    filter.PlaceId,
                    cancellationToken),
                AccountStatusOptions = BuildOptions(
                    AdminCommissionAccountStatuses.All,
                    AccountStatusLabel,
                    filter.AccountStatus)
            };
        }

        public async Task<AdminCommissionAccountDetailsDto?> GetCommissionAccountDetailsAsync(
            int placeCommissionAccountId,
            CancellationToken cancellationToken = default)
        {
            if (placeCommissionAccountId <= 0)
            {
                return null;
            }

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var account = await _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Include(item => item.Place)
                .FirstOrDefaultAsync(item =>
                    item.PlaceCommissionAccountId ==
                    placeCommissionAccountId,
                    cancellationToken);

            if (account == null)
            {
                return null;
            }

            var ownerNames = await GetUserNamesAsync(
                new[] { account.Place?.OwnerId },
                cancellationToken);

            var transactions = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(item => item.Place)
                .Include(item => item.CreatedByUser)
                .Where(item => item.PlaceId == account.PlaceId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.CommissionTransactionId)
                .ToListAsync(cancellationToken);

            var relatedBookingsCount = await _context.FinancialRecords
                .AsNoTracking()
                .CountAsync(record =>
                    record.Booking.PlaceId == account.PlaceId,
                    cancellationToken);

            var status = string.IsNullOrWhiteSpace(account.AccountStatus)
                ? CalculateAccountStatus(account, settings)
                : account.AccountStatus.Trim();

            return new AdminCommissionAccountDetailsDto
            {
                PlaceCommissionAccountId =
                    account.PlaceCommissionAccountId,
                PlaceId = account.PlaceId,
                PlaceName = FirstNonEmpty(
                    "منشأة غير معروفة",
                    account.Place?.PlaceNameAndDesc),
                PlaceImageUrl = FirstNonEmpty(
                    string.Empty,
                    account.Place?.CoverImage,
                    account.Place?.LogoImage),
                PlaceStatus = account.Place?.Status?.Trim(),
                PlaceIsAvailable = account.Place?.IsAvailable ?? false,
                OwnerId = account.Place?.OwnerId,
                OwnerName = OwnerNameFromDictionary(
                    account.Place?.OwnerId,
                    ownerNames),
                AvailableBalance = account.AvailableBalance,
                HeldBalance = account.HeldBalance,
                DebtAmount = account.DebtAmount,
                AllowNegativeBalanceOverride =
                    account.AllowNegativeBalance,
                DebtLimitOverride = account.DebtLimit,
                LowBalanceThresholdOverride =
                    account.LowBalanceThreshold,
                EffectiveAllowNegativeBalance =
                    EffectiveAllowNegative(account, settings),
                EffectiveDebtLimit =
                    EffectiveDebtLimit(account, settings),
                EffectiveLowBalanceThreshold =
                    EffectiveLowBalanceThreshold(account, settings),
                EffectiveMinimumTopUpAmount =
                    settings?.MinimumCommissionTopUp ?? 0m,
                EffectiveDebtGraceDays =
                    settings?.CommissionDebtGraceDays ?? 0,
                AccountStatus = status,
                IsBookingSuspended =
                    IsState(
                        status,
                        AdminCommissionAccountStatuses.Suspended) &&
                    (settings?.SuspendBookingWhenBalanceInsufficient ?? true),
                IsPlaceHiddenByFinancialPolicy =
                    IsState(
                        status,
                        AdminCommissionAccountStatuses.Suspended) &&
                    (settings?.HidePlaceWhenCommissionSuspended ?? false),
                LastTopUpAt = account.LastTopUpAt,
                CreatedAt = account.CreatedAt ?? DateTime.MinValue,
                UpdatedAt = account.UpdatedAt,
                LastTransactionAt = transactions.FirstOrDefault()?.CreatedAt,
                LastTransactionType = transactions.FirstOrDefault()?.TransactionType,
                LastTransactionAmount = transactions.FirstOrDefault()?.Amount,
                TotalTopUps = transactions
                    .Where(item =>
                        IsState(
                            item.TransactionType,
                            AdminFinanceTransactionTypes.TopUp) &&
                        IsState(
                            item.Status,
                            AdminFinanceTransactionStatuses.Completed))
                    .Sum(item => item.Amount),
                TotalCommissionHeld = transactions
                    .Where(item => IsState(
                        item.TransactionType,
                        AdminFinanceTransactionTypes.CommissionHold))
                    .Sum(item => item.Amount),
                TotalCommissionCharged = transactions
                    .Where(item => IsState(
                        item.TransactionType,
                        AdminFinanceTransactionTypes.CommissionCharge))
                    .Sum(item => item.Amount),
                TotalCommissionReleased = transactions
                    .Where(item => IsState(
                        item.TransactionType,
                        AdminFinanceTransactionTypes.CommissionRelease))
                    .Sum(item => item.Amount),
                TotalDebtRegistered = transactions
                    .Where(item => item.DebtAfter > item.DebtBefore)
                    .Sum(item => item.DebtAfter - item.DebtBefore),
                TotalDebtPaid = transactions
                    .Where(item => item.DebtBefore > item.DebtAfter)
                    .Sum(item => item.DebtBefore - item.DebtAfter),
                RelatedBookingsCount = relatedBookingsCount,
                TransactionsCount = transactions.Count,
                RecentTransactions = transactions
                    .Take(20)
                    .Select(item =>
                        new AdminRecentFinancialMovementDto
                        {
                            CommissionTransactionId =
                                item.CommissionTransactionId,
                            PlaceId = item.PlaceId,
                            PlaceName = FirstNonEmpty(
                                "منشأة غير معروفة",
                                item.Place?.PlaceNameAndDesc),
                            OwnerName = OwnerNameFromDictionary(
                                item.Place?.OwnerId,
                                ownerNames),
                            BookingId = item.BookingId,
                            TransactionType = item.TransactionType,
                            TransactionTypeLabel =
                                TransactionTypeLabel(item.TransactionType),
                            Amount = item.Amount,
                            Status = item.Status,
                            StatusLabel =
                                TransactionStatusLabel(item.Status),
                            ReferenceNumber = item.ReferenceNumber?.Trim(),
                            CreatedAt = item.CreatedAt ?? DateTime.MinValue,
                            CreatedByName = FirstNonEmpty(
                                string.Empty,
                                item.CreatedByUser?.FullName,
                                item.CreatedByUser?.Username)
                        })
                    .ToList()
            };
        }

        public async Task<FinanceOperationResult> UpdateCommissionAccountPolicyAsync(
            AdminCommissionAccountPolicyUpdateDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request == null ||
                request.PlaceCommissionAccountId <= 0 ||
                request.PlaceId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "بيانات حساب الضمان غير صحيحة.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return FinanceOperationResult.Failed(
                    "اكتب سبب تعديل سياسة الحساب.");
            }

            var account = await _context.PlaceCommissionAccounts
                .FirstOrDefaultAsync(item =>
                    item.PlaceCommissionAccountId ==
                        request.PlaceCommissionAccountId &&
                    item.PlaceId == request.PlaceId,
                    cancellationToken);

            if (account == null)
            {
                return FinanceOperationResult.Failed(
                    "حساب ضمان المنشأة غير موجود.");
            }

            if (!request.UseGlobalAllowNegativeBalance &&
                !request.AllowNegativeBalance.HasValue)
            {
                return FinanceOperationResult.Failed(
                    "حدد هل يسمح الحساب بالدين أم لا.");
            }

            if (!request.UseGlobalDebtLimit &&
                (!request.DebtLimit.HasValue || request.DebtLimit.Value < 0m))
            {
                return FinanceOperationResult.Failed(
                    "حد الدين الخاص غير صحيح.");
            }

            if (!request.UseGlobalLowBalanceThreshold &&
                (!request.LowBalanceThreshold.HasValue ||
                 request.LowBalanceThreshold.Value < 0m))
            {
                return FinanceOperationResult.Failed(
                    "حد انخفاض الرصيد الخاص غير صحيح.");
            }

            var oldAllowNegative = account.AllowNegativeBalance;
            var oldDebtLimit = account.DebtLimit;
            var oldThreshold = account.LowBalanceThreshold;

            account.AllowNegativeBalance = request.UseGlobalAllowNegativeBalance
                ? null
                : request.AllowNegativeBalance;

            account.DebtLimit = request.UseGlobalDebtLimit
                ? null
                : Math.Round(request.DebtLimit ?? 0m, 2);

            account.LowBalanceThreshold = request.UseGlobalLowBalanceThreshold
                ? null
                : Math.Round(request.LowBalanceThreshold ?? 0m, 2);

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            RefreshAccountStatus(account, settings);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "عدل الأدمن {AdminUserId} سياسة حساب الضمان {AccountId}. " +
                "AllowNegative: {OldAllowNegative} -> {NewAllowNegative}, " +
                "DebtLimit: {OldDebtLimit} -> {NewDebtLimit}, " +
                "LowThreshold: {OldThreshold} -> {NewThreshold}. السبب: {Reason}",
                adminUserId,
                account.PlaceCommissionAccountId,
                oldAllowNegative,
                account.AllowNegativeBalance,
                oldDebtLimit,
                account.DebtLimit,
                oldThreshold,
                account.LowBalanceThreshold,
                request.Reason.Trim());

            return new FinanceOperationResult
            {
                Success = true,
                Message = "تم حفظ سياسة حساب ضمان المنشأة.",
                EntityId = account.PlaceCommissionAccountId,
                AvailableBalance = account.AvailableBalance,
                HeldBalance = account.HeldBalance,
                DebtAmount = account.DebtAmount
            };
        }

        public async Task<FinanceOperationResult> ChangeCommissionAccountStatusAsync(
            AdminCommissionAccountStatusUpdateDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request == null ||
                request.PlaceCommissionAccountId <= 0 ||
                request.PlaceId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "بيانات حساب الضمان غير صحيحة.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return FinanceOperationResult.Failed(
                    "اكتب سبب تغيير حالة الحساب.");
            }

            var account = await _context.PlaceCommissionAccounts
                .FirstOrDefaultAsync(item =>
                    item.PlaceCommissionAccountId ==
                        request.PlaceCommissionAccountId &&
                    item.PlaceId == request.PlaceId,
                    cancellationToken);

            if (account == null)
            {
                return FinanceOperationResult.Failed(
                    "حساب ضمان المنشأة غير موجود.");
            }

            var oldStatus = account.AccountStatus;
            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            if (IsState(
                request.TargetStatus,
                AdminCommissionAccountStatuses.Suspended))
            {
                account.AccountStatus =
                    AdminCommissionAccountStatuses.Suspended;
            }
            else if (IsState(
                request.TargetStatus,
                AdminCommissionAccountStatuses.Active))
            {
                RefreshAccountStatus(account, settings);

                if (IsState(
                    account.AccountStatus,
                    AdminCommissionAccountStatuses.Suspended))
                {
                    return FinanceOperationResult.Failed(
                        "لا يمكن تفعيل الحساب قبل معالجة نقص الرصيد أو تجاوز حد الدين.");
                }
            }
            else
            {
                return FinanceOperationResult.Failed(
                    "الحالة المطلوبة غير مسموحة من هذا الإجراء.");
            }

            account.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "غير الأدمن {AdminUserId} حالة حساب الضمان {AccountId} " +
                "من {OldStatus} إلى {NewStatus}. السبب: {Reason}",
                adminUserId,
                account.PlaceCommissionAccountId,
                oldStatus,
                account.AccountStatus,
                request.Reason.Trim());

            return new FinanceOperationResult
            {
                Success = true,
                Message = IsState(
                    account.AccountStatus,
                    AdminCommissionAccountStatuses.Suspended)
                        ? "تم تعليق حساب ضمان المنشأة."
                        : "تم تفعيل حساب ضمان المنشأة.",
                EntityId = account.PlaceCommissionAccountId,
                AvailableBalance = account.AvailableBalance,
                HeldBalance = account.HeldBalance,
                DebtAmount = account.DebtAmount
            };
        }

        /* ====================================================================
           Booking commissions
           ==================================================================== */

        private IQueryable<FinancialRecord> ApplyBookingCommissionFilter(
            IQueryable<FinancialRecord> query,
            AdminBookingCommissionFilterDto filter)
        {
            if (filter.BookingId.HasValue)
            {
                query = query.Where(record =>
                    record.BookingId == filter.BookingId.Value);
            }

            if (filter.CustomerId.HasValue)
            {
                query = query.Where(record =>
                    record.Booking.CustomerId == filter.CustomerId.Value);
            }

            if (filter.OwnerId.HasValue)
            {
                query = query.Where(record =>
                    record.Booking.Place.OwnerId == filter.OwnerId.Value);
            }

            if (filter.PlaceId.HasValue)
            {
                query = query.Where(record =>
                    record.Booking.PlaceId == filter.PlaceId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.BookingState))
            {
                var bookingState = filter.BookingState.Trim();
                query = query.Where(record =>
                    record.Booking.BookingState == bookingState);
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
            {
                var paymentStatus = filter.PaymentStatus.Trim();
                query = query.Where(record =>
                    record.Booking.PaymentStatus == paymentStatus);
            }

            if (!string.IsNullOrWhiteSpace(filter.CommissionStatus))
            {
                var commissionStatus = filter.CommissionStatus.Trim();
                query = query.Where(record =>
                    record.CommissionStatus == commissionStatus);
            }

            if (filter.HasDebt.HasValue)
            {
                query = filter.HasDebt.Value
                    ? query.Where(record =>
                        record.CommissionStatus ==
                        AdminBookingCommissionStatuses.Debt)
                    : query.Where(record =>
                        record.CommissionStatus !=
                        AdminBookingCommissionStatuses.Debt);
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value;

                query = filter.DateScope switch
                {
                    AdminFinancialDateScopes.CommissionHeldAt =>
                        query.Where(record =>
                            record.CommissionHeldAt.HasValue &&
                            record.CommissionHeldAt.Value >= fromDate),

                    AdminFinancialDateScopes.CommissionSettledAt =>
                        query.Where(record =>
                            (record.CommissionChargedAt.HasValue &&
                             record.CommissionChargedAt.Value >= fromDate) ||
                            (record.CommissionReleasedAt.HasValue &&
                             record.CommissionReleasedAt.Value >= fromDate)),

                    _ =>
                        query.Where(record =>
                            record.Booking.CreatedAt.HasValue &&
                            record.Booking.CreatedAt.Value >= fromDate)
                };
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive = filter.ToDate.Value.AddDays(1);

                query = filter.DateScope switch
                {
                    AdminFinancialDateScopes.CommissionHeldAt =>
                        query.Where(record =>
                            record.CommissionHeldAt.HasValue &&
                            record.CommissionHeldAt.Value < toDateExclusive),

                    AdminFinancialDateScopes.CommissionSettledAt =>
                        query.Where(record =>
                            (record.CommissionChargedAt.HasValue &&
                             record.CommissionChargedAt.Value < toDateExclusive) ||
                            (record.CommissionReleasedAt.HasValue &&
                             record.CommissionReleasedAt.Value < toDateExclusive)),

                    _ =>
                        query.Where(record =>
                            record.Booking.CreatedAt.HasValue &&
                            record.Booking.CreatedAt.Value < toDateExclusive)
                };
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                var numeric = int.TryParse(search, out var numericSearch);

                query = query.Where(record =>
                    record.Booking.Place.PlaceNameAndDesc.Contains(search) ||
                    record.Booking.Customer.FullName.Contains(search) ||
                    record.Booking.Customer.Username.Contains(search) ||
                    (record.Booking.Customer.PhoneNumber != null &&
                     record.Booking.Customer.PhoneNumber.Contains(search)) ||
                    (record.Booking.Customer.ContactInfo != null &&
                     record.Booking.Customer.ContactInfo.Contains(search)) ||
                    (record.TransferReferenceNumber != null &&
                     record.TransferReferenceNumber.Contains(search)) ||
                    (numeric &&
                     (record.BookingId == numericSearch ||
                      record.Booking.CustomerId == numericSearch)));
            }

            return query;
        }

        public async Task<AdminBookingCommissionPageDto> GetBookingCommissionsAsync(
            AdminBookingCommissionFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminBookingCommissionFilterDto();
            filter.PageNumber = NormalizePageNumber(filter.PageNumber);
            filter.PageSize = NormalizePageSize(filter.PageSize);
            filter.Search = NormalizeSearch(filter.Search);
            var normalizedDateRange =
      NormalizeDateRange(
          filter.FromDate,
          filter.ToDate);

            filter.FromDate =
                normalizedDateRange.FromDate;

            filter.ToDate =
                normalizedDateRange.ToDate;

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var query = ApplyBookingCommissionFilter(
                _context.FinancialRecords.AsNoTracking(),
                filter);

            var totalCount = await query.CountAsync(cancellationToken);

            var summary = await query
                .GroupBy(_ => 1)
                .Select(group => new AdminBookingCommissionSummaryDto
                {
                    TotalCount = group.Count(),
                    CalculatedCount = group.Count(record =>
                        record.CommissionStatus ==
                        AdminBookingCommissionStatuses.Calculated),
                    HeldCount = group.Count(record =>
                        record.CommissionStatus ==
                        AdminBookingCommissionStatuses.Held),
                    ChargedCount = group.Count(record =>
                        record.CommissionStatus ==
                        AdminBookingCommissionStatuses.Charged),
                    ReleasedCount = group.Count(record =>
                        record.CommissionStatus ==
                        AdminBookingCommissionStatuses.Released),
                    DebtCount = group.Count(record =>
                        record.CommissionStatus ==
                        AdminBookingCommissionStatuses.Debt),
                    NotApplicableCount = group.Count(record =>
                        record.CommissionStatus ==
                        AdminBookingCommissionStatuses.NotApplicable),
                    TotalBookingValue = group.Sum(record =>
                        record.Booking.TotalPrice),
                    TotalCommissionBaseAmount = group.Sum(record =>
                        record.CommissionBaseAmount),
                    TotalCommissionCalculated = group.Sum(record =>
                        record.PlatformCommission),
                    TotalCommissionHeld = group
                        .Where(record =>
                            record.CommissionStatus ==
                                AdminBookingCommissionStatuses.Held ||
                            record.CommissionStatus ==
                                AdminBookingCommissionStatuses.Debt)
                        .Sum(record => record.PlatformCommission),
                    TotalCommissionCharged = group
                        .Where(record =>
                            record.CommissionStatus ==
                            AdminBookingCommissionStatuses.Charged)
                        .Sum(record => record.PlatformCommission),
                    TotalCommissionReleased = group
                        .Where(record =>
                            record.CommissionStatus ==
                            AdminBookingCommissionStatuses.Released)
                        .Sum(record => record.PlatformCommission),
                    TotalCommissionDebt = group
                        .Where(record =>
                            record.CommissionStatus ==
                            AdminBookingCommissionStatuses.Debt)
                        .Sum(record => record.PlatformCommission)
                })
                .FirstOrDefaultAsync(cancellationToken) ??
                new AdminBookingCommissionSummaryDto();

            var effectiveValues = await query.Select(record => new
            {
                record.Booking.BookingState,
                record.Booking.TotalPrice,
                Paid = record.AmountPaidOnline + record.AmountCollectedAtProperty,
                Refunds = record.Booking.RefundRequests
                    .Where(refund => refund.Status == "Pending" || refund.Status == "Processing" || refund.Status == "Completed")
                    .Sum(refund => (decimal?)refund.RefundAmount) ?? 0m
            }).ToListAsync(cancellationToken);
            summary.TotalBookingValue = effectiveValues.Sum(item => BookingMoneyPolicy.EffectiveValue(
                item.BookingState, item.TotalPrice, item.Paid, item.Refunds, 0m));

            summary.NeedsReviewCount = await query.CountAsync(record =>
                record.CommissionStatus ==
                    AdminBookingCommissionStatuses.Calculated ||
                (record.CommissionStatus ==
                    AdminBookingCommissionStatuses.Held &&
                 !record.CommissionHeldAt.HasValue) ||
                (record.CommissionStatus ==
                    AdminBookingCommissionStatuses.Charged &&
                 !record.CommissionChargedAt.HasValue) ||
                (record.CommissionStatus ==
                    AdminBookingCommissionStatuses.Released &&
                 !record.CommissionReleasedAt.HasValue),
                cancellationToken);

            var totalPages = totalCount <= 0
                ? 0
                : (int)Math.Ceiling((double)totalCount / filter.PageSize);

            if (totalPages > 0 && filter.PageNumber > totalPages)
            {
                filter.PageNumber = totalPages;
            }

            var records = await query
                .Include(record => record.Booking)
                    .ThenInclude(booking => booking.Customer)
                .Include(record => record.Booking)
                    .ThenInclude(booking => booking.Place)
                .AsSplitQuery()
                .OrderByDescending(record => record.Booking.CreatedAt)
                .ThenByDescending(record => record.BookingId)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            var ownerNames = await GetUserNamesAsync(
                records.Select(record => record.Booking.Place.OwnerId),
                cancellationToken);

            var bookingIds = records
                .Select(record => record.BookingId)
                .ToList();

            var debtByBooking = bookingIds.Count == 0
                ? new Dictionary<int, decimal>()
                : await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        transaction.BookingId.HasValue &&
                        bookingIds.Contains(transaction.BookingId.Value) &&
                        transaction.DebtAfter > transaction.DebtBefore)
                    .GroupBy(transaction => transaction.BookingId!.Value)
                    .Select(group => new
                    {
                        BookingId = group.Key,
                        Debt = group.Sum(item =>
                            item.DebtAfter - item.DebtBefore)
                    })
                    .ToDictionaryAsync(
                        item => item.BookingId,
                        item => item.Debt,
                        cancellationToken);

            var rows = records
                .Select(record =>
                {
                    debtByBooking.TryGetValue(
                        record.BookingId,
                        out var debtAmount);

                    var status = record.CommissionStatus?.Trim() ??
                        AdminBookingCommissionStatuses.Calculated;

                    var customer = record.Booking.Customer;
                    var place = record.Booking.Place;

                    return new AdminBookingCommissionRowDto
                    {
                        RecordId = record.RecordId,
                        BookingId = record.BookingId,
                        CustomerId = record.Booking.CustomerId,
                        CustomerName = FirstNonEmpty(
                            "عميل",
                            customer?.FullName,
                            customer?.Username),
                        CustomerPhoneNumber = FirstNonEmpty(
                            string.Empty,
                            customer?.PhoneNumber,
                            customer?.ContactInfo),
                        PlaceId = record.Booking.PlaceId,
                        PlaceName = FirstNonEmpty(
                            "منشأة غير معروفة",
                            place?.PlaceNameAndDesc),
                        PlaceImageUrl = FirstNonEmpty(
                            string.Empty,
                            place?.CoverImage,
                            place?.LogoImage),
                        OwnerId = place?.OwnerId,
                        OwnerName = OwnerNameFromDictionary(
                            place?.OwnerId,
                            ownerNames),
                        BookingTotalPrice = record.Booking.TotalPrice,
                        CommissionRateSnapshot =
                            record.CommissionRateSnapshot,
                        CommissionBaseAmount =
                            record.CommissionBaseAmount,
                        PlatformCommission = record.PlatformCommission,
                        HeldAmount =
                            IsState(status, AdminBookingCommissionStatuses.Held) ||
                            IsState(status, AdminBookingCommissionStatuses.Debt)
                                ? record.PlatformCommission
                                : 0m,
                        ChargedAmount =
                            IsState(status, AdminBookingCommissionStatuses.Charged)
                                ? record.PlatformCommission
                                : 0m,
                        ReleasedAmount =
                            IsState(status, AdminBookingCommissionStatuses.Released)
                                ? record.PlatformCommission
                                : 0m,
                        DebtAmount = debtAmount,
                        BookingState =
                            record.Booking.BookingState?.Trim() ?? string.Empty,
                        PaymentStatus =
                            record.Booking.PaymentStatus?.Trim() ?? string.Empty,
                        CommissionPaymentStatus =
                            record.Booking.PaymentStatus?.Trim() ?? string.Empty,
                        CommissionStatus = status,
                        BookingCreatedAt = record.Booking.CreatedAt,
                        CommissionHeldAt = record.CommissionHeldAt,
                        CommissionChargedAt = record.CommissionChargedAt,
                        CommissionReleasedAt = record.CommissionReleasedAt,
                        SettledAt = GetFinancialSettlementDate(record.Booking, record),
                        UpdatedAt = record.UpdatedAt,
                        NeedsReview =
                            IsState(status, AdminBookingCommissionStatuses.Calculated) ||
                            (IsState(status, AdminBookingCommissionStatuses.Held) &&
                             !record.CommissionHeldAt.HasValue) ||
                            (IsState(status, AdminBookingCommissionStatuses.Charged) &&
                             !record.CommissionChargedAt.HasValue) ||
                            (IsState(status, AdminBookingCommissionStatuses.Released) &&
                             !record.CommissionReleasedAt.HasValue)
                    };
                })
                .ToList();

            return new AdminBookingCommissionPageDto
            {
                Currency = GetCurrency(settings),
                Filter = filter,
                Summary = summary,
                Result = new AdminPagedResultDto<AdminBookingCommissionRowDto>
                {
                    Items = rows,
                    Pagination = BuildPagination(
                        filter.PageNumber,
                        filter.PageSize,
                        totalCount)
                },
                Owners = await BuildOwnerOptionsAsync(
                    filter.OwnerId,
                    cancellationToken),
                Places = await BuildPlaceOptionsAsync(
                    filter.OwnerId,
                    filter.PlaceId,
                    cancellationToken),
                BookingStateOptions = BuildOptions(
                    AdminFinancialBookingStates.All,
                    value => value,
                    filter.BookingState),
                PaymentStatusOptions = new List<AdminFinancialSelectOptionDto>
                {
                    new() { Value = "Unpaid", Label = "غير مدفوع", IsSelected = IsState(filter.PaymentStatus, "Unpaid") },
                    new() { Value = "Pending", Label = "قيد المراجعة", IsSelected = IsState(filter.PaymentStatus, "Pending") },
                    new() { Value = "Paid", Label = "مدفوع", IsSelected = IsState(filter.PaymentStatus, "Paid") },
                    new() { Value = "Confirmed", Label = "مؤكد", IsSelected = IsState(filter.PaymentStatus, "Confirmed") },
                    new() { Value = "Rejected", Label = "مرفوض", IsSelected = IsState(filter.PaymentStatus, "Rejected") }
                },
                CommissionStatusOptions = BuildOptions(
                    AdminBookingCommissionStatuses.All,
                    CommissionStatusLabel,
                    filter.CommissionStatus),
                DateScopeOptions = new List<AdminFinancialSelectOptionDto>
                {
                    new()
                    {
                        Value = AdminFinancialDateScopes.BookingCreatedAt,
                        Label = "تاريخ إنشاء الحجز",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.BookingCreatedAt)
                    },
                    new()
                    {
                        Value = AdminFinancialDateScopes.CommissionHeldAt,
                        Label = "تاريخ حجز العمولة",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.CommissionHeldAt)
                    },
                    new()
                    {
                        Value = AdminFinancialDateScopes.CommissionSettledAt,
                        Label = "تاريخ تسوية العمولة",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.CommissionSettledAt)
                    }
                }
            };
        }

        public async Task<AdminBookingCommissionDetailsDto?> GetBookingCommissionDetailsAsync(
            int bookingId,
            CancellationToken cancellationToken = default)
        {
            if (bookingId <= 0)
            {
                return null;
            }

            var record = await _context.FinancialRecords
                .AsNoTracking()
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking.Customer)
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking.Place)
                .FirstOrDefaultAsync(item =>
                    item.BookingId == bookingId,
                    cancellationToken);

            if (record == null)
            {
                return null;
            }

            var transactions = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(item => item.Place)
                .Include(item => item.PlatformWallet)
                    .ThenInclude(wallet => wallet.Wallet)
                .Include(item => item.CreatedByUser)
                .Include(item => item.ReviewedByUser)
                .Where(item => item.BookingId == bookingId)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.CommissionTransactionId)
                .ToListAsync(cancellationToken);

            var ownerNames = await GetUserNamesAsync(
                new[] { record.Booking.Place.OwnerId },
                cancellationToken);

            var debtAmount = transactions
                .Where(item => item.DebtAfter > item.DebtBefore)
                .Sum(item => item.DebtAfter - item.DebtBefore);

            var status = record.CommissionStatus?.Trim() ??
                AdminBookingCommissionStatuses.Calculated;

            var result = new AdminBookingCommissionDetailsDto
            {
                RecordId = record.RecordId,
                BookingId = record.BookingId,
                CustomerId = record.Booking.CustomerId,
                CustomerName = FirstNonEmpty(
                    "عميل",
                    record.Booking.Customer?.FullName,
                    record.Booking.Customer?.Username),
                CustomerPhoneNumber = FirstNonEmpty(
                    string.Empty,
                    record.Booking.Customer?.PhoneNumber,
                    record.Booking.Customer?.ContactInfo),
                PlaceId = record.Booking.PlaceId,
                PlaceName = FirstNonEmpty(
                    "منشأة غير معروفة",
                    record.Booking.Place?.PlaceNameAndDesc),
                PlaceImageUrl = FirstNonEmpty(
                    string.Empty,
                    record.Booking.Place?.CoverImage,
                    record.Booking.Place?.LogoImage),
                OwnerId = record.Booking.Place?.OwnerId,
                OwnerName = OwnerNameFromDictionary(
                    record.Booking.Place?.OwnerId,
                    ownerNames),
                BookingTotalPrice = record.Booking.TotalPrice,
                CommissionRateSnapshot = record.CommissionRateSnapshot,
                CommissionBaseAmount = record.CommissionBaseAmount,
                PlatformCommission = record.PlatformCommission,
                HeldAmount =
                    IsState(status, AdminBookingCommissionStatuses.Held) ||
                    IsState(status, AdminBookingCommissionStatuses.Debt)
                        ? record.PlatformCommission
                        : 0m,
                ChargedAmount =
                    IsState(status, AdminBookingCommissionStatuses.Charged)
                        ? record.PlatformCommission
                        : 0m,
                ReleasedAmount =
                    IsState(status, AdminBookingCommissionStatuses.Released)
                        ? record.PlatformCommission
                        : 0m,
                DebtAmount = debtAmount,
                BookingState = record.Booking.BookingState?.Trim() ?? string.Empty,
                PaymentStatus = record.Booking.PaymentStatus?.Trim() ?? string.Empty,
                CommissionPaymentStatus =
                    record.Booking.PaymentStatus?.Trim() ?? string.Empty,
                CommissionStatus = status,
                BookingCreatedAt = record.Booking.CreatedAt,
                CommissionHeldAt = record.CommissionHeldAt,
                CommissionChargedAt = record.CommissionChargedAt,
                CommissionReleasedAt = record.CommissionReleasedAt,
                SettledAt = GetFinancialSettlementDate(record.Booking, record),
                UpdatedAt = record.UpdatedAt,
                NeedsReview =
                    IsState(status, AdminBookingCommissionStatuses.Calculated),
                BookingStartDate = record.Booking.StartDate,
                BookingEndDate = record.Booking.EndDate,
                PaymentMethod = record.Booking.PaymentMethod?.Trim(),
                DepositAmount = record.DepositAmount,
                AmountPaidOnline = record.AmountPaidOnline,
                AmountCollectedAtProperty = record.AmountCollectedAtProperty,
                AmountToPayAtProperty = record.AmountToPayAtProperty,
                IsPaymentConfirmed = IsBookingPaymentConfirmed(record.Booking),
                TransferReferenceNumber =
                    record.TransferReferenceNumber?.Trim(),
                Transactions = transactions
                    .Select(item => MapTransactionRow(item, ownerNames))
                    .ToList()
            };

            result.Timeline.Add(new AdminBookingCommissionTimelineItemDto
            {
                Status = AdminBookingCommissionStatuses.Calculated,
                Label = "احتساب العمولة",
                OccurredAt = record.Booking.CreatedAt,
                Amount = record.PlatformCommission,
                Description = $"نسبة العمولة المحفوظة: {record.CommissionRateSnapshot:N2}%.",
                IsCompleted = true,
                IsCurrent = IsState(
                    status,
                    AdminBookingCommissionStatuses.Calculated)
            });

            result.Timeline.Add(new AdminBookingCommissionTimelineItemDto
            {
                Status = AdminBookingCommissionStatuses.Held,
                Label = IsState(status, AdminBookingCommissionStatuses.Debt)
                    ? "حجز العمولة مع تسجيل دين"
                    : "حجز العمولة",
                OccurredAt = record.CommissionHeldAt,
                Amount = record.PlatformCommission,
                Description = debtAmount > 0m
                    ? $"الجزء المسجل كدين: {debtAmount:N2}."
                    : "حُجزت العمولة من رصيد الضمان.",
                IsCompleted = record.CommissionHeldAt.HasValue,
                IsCurrent =
                    IsState(status, AdminBookingCommissionStatuses.Held) ||
                    IsState(status, AdminBookingCommissionStatuses.Debt)
            });

            result.Timeline.Add(new AdminBookingCommissionTimelineItemDto
            {
                Status = AdminBookingCommissionStatuses.Charged,
                Label = "الخصم النهائي",
                OccurredAt = record.CommissionChargedAt,
                Amount = record.PlatformCommission,
                Description = "تم تثبيت عمولة المنصة بعد اكتمال الحجز.",
                IsCompleted = record.CommissionChargedAt.HasValue,
                IsCurrent = IsState(
                    status,
                    AdminBookingCommissionStatuses.Charged)
            });

            result.Timeline.Add(new AdminBookingCommissionTimelineItemDto
            {
                Status = AdminBookingCommissionStatuses.Released,
                Label = "تحرير العمولة",
                OccurredAt = record.CommissionReleasedAt,
                Amount = record.PlatformCommission,
                Description = "أعيدت العمولة المحجوزة وفق نتيجة الحجز.",
                IsCompleted = record.CommissionReleasedAt.HasValue,
                IsCurrent = IsState(
                    status,
                    AdminBookingCommissionStatuses.Released)
            });

            return result;
        }

        /* ====================================================================
           Financial transactions
           ==================================================================== */

        private IQueryable<CommissionTransaction> ApplyTransactionFilter(
            IQueryable<CommissionTransaction> query,
            AdminFinancialTransactionFilterDto filter)
        {
            if (filter.TransactionId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.CommissionTransactionId ==
                    filter.TransactionId.Value);
            }

            if (filter.OwnerId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.Place.OwnerId == filter.OwnerId.Value);
            }

            if (filter.PlaceId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.PlaceId == filter.PlaceId.Value);
            }

            if (filter.BookingId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.BookingId == filter.BookingId.Value);
            }

            if (filter.TopUpRequestId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.TransactionType ==
                        AdminFinanceTransactionTypes.TopUp &&
                    transaction.CommissionTransactionId ==
                        filter.TopUpRequestId.Value);
            }

            if (filter.PlatformWalletId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.PlatformWalletId ==
                    filter.PlatformWalletId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionType))
            {
                var transactionType = filter.TransactionType.Trim();
                query = query.Where(transaction =>
                    transaction.TransactionType == transactionType);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = filter.Status.Trim();
                query = query.Where(transaction =>
                    transaction.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
            {
                var reference = filter.ReferenceNumber.Trim();
                query = query.Where(transaction =>
                    transaction.ReferenceNumber != null &&
                    transaction.ReferenceNumber.Contains(reference));
            }

            if (filter.CreatedByUserId.HasValue)
            {
                query = query.Where(transaction =>
                    transaction.CreatedByUserId ==
                    filter.CreatedByUserId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value;

                query = filter.DateScope switch
                {
                    AdminFinancialDateScopes.TransferDate =>
                        query.Where(transaction =>
                            transaction.TransferDate.HasValue &&
                            transaction.TransferDate.Value >= fromDate),

                    AdminFinancialDateScopes.ReviewedAt =>
                        query.Where(transaction =>
                            transaction.ReviewedAt.HasValue &&
                            transaction.ReviewedAt.Value >= fromDate),

                    _ => query.Where(transaction =>
                        transaction.CreatedAt >= fromDate)
                };
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive = filter.ToDate.Value.AddDays(1);

                query = filter.DateScope switch
                {
                    AdminFinancialDateScopes.TransferDate =>
                        query.Where(transaction =>
                            transaction.TransferDate.HasValue &&
                            transaction.TransferDate.Value < toDateExclusive),

                    AdminFinancialDateScopes.ReviewedAt =>
                        query.Where(transaction =>
                            transaction.ReviewedAt.HasValue &&
                            transaction.ReviewedAt.Value < toDateExclusive),

                    _ => query.Where(transaction =>
                        transaction.CreatedAt < toDateExclusive)
                };
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                var numeric = int.TryParse(search, out var numericSearch);

                query = query.Where(transaction =>
                    transaction.Place.PlaceNameAndDesc.Contains(search) ||
                    (transaction.ReferenceNumber != null &&
                     transaction.ReferenceNumber.Contains(search)) ||
                    (transaction.Notes != null &&
                     transaction.Notes.Contains(search)) ||
                    (numeric &&
                     (transaction.CommissionTransactionId == numericSearch ||
                      transaction.BookingId == numericSearch)));
            }

            return query;
        }

        private static AdminFinancialTransactionRowDto MapTransactionRow(
            CommissionTransaction transaction,
            IReadOnlyDictionary<int, string> ownerNames)
        {
            return new AdminFinancialTransactionRowDto
            {
                CommissionTransactionId =
                    transaction.CommissionTransactionId,
                PlaceId = transaction.PlaceId,
                PlaceName = FirstNonEmpty(
                    "منشأة غير معروفة",
                    transaction.Place?.PlaceNameAndDesc),
                OwnerId = transaction.Place?.OwnerId,
                OwnerName = OwnerNameFromDictionary(
                    transaction.Place?.OwnerId,
                    ownerNames),
                BookingId = transaction.BookingId,
                PlatformWalletId = transaction.PlatformWalletId,
                PlatformWalletName = FirstNonEmpty(
                    string.Empty,
                    transaction.PlatformWallet?.Wallet?.WalletName),
                TransactionType = transaction.TransactionType,
                TransactionTypeLabel =
                    TransactionTypeLabel(transaction.TransactionType),
                Direction = TransactionDirection(transaction),
                Amount = transaction.Amount,
                AvailableBalanceBefore =
                    transaction.AvailableBalanceBefore,
                AvailableBalanceAfter =
                    transaction.AvailableBalanceAfter,
                HeldBalanceBefore =
                    transaction.HeldBalanceBefore,
                HeldBalanceAfter =
                    transaction.HeldBalanceAfter,
                DebtBefore = transaction.DebtBefore,
                DebtAfter = transaction.DebtAfter,
                Status = transaction.Status,
                StatusLabel =
                    TransactionStatusLabel(transaction.Status),
                ReferenceNumber =
                    transaction.ReferenceNumber?.Trim(),
                TransferDate = transaction.TransferDate,
                ReceiptImage = transaction.ReceiptImage?.Trim(),
                ReviewedAt = transaction.ReviewedAt,
                ReviewedByUserId = transaction.ReviewedByUserId,
                ReviewedByName = FirstNonEmpty(
                    string.Empty,
                    transaction.ReviewedByUser?.FullName,
                    transaction.ReviewedByUser?.Username),
                RejectionReason =
                    transaction.RejectionReason?.Trim(),
                CreatedAt = transaction.CreatedAt ?? DateTime.MinValue,
                CreatedByUserId = transaction.CreatedByUserId,
                CreatedByName = FirstNonEmpty(
                    string.Empty,
                    transaction.CreatedByUser?.FullName,
                    transaction.CreatedByUser?.Username),
                Notes = transaction.Notes?.Trim()
            };
        }

        public async Task<AdminFinancialTransactionPageDto> GetTransactionsAsync(
            AdminFinancialTransactionFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminFinancialTransactionFilterDto();
            filter.PageNumber = NormalizePageNumber(filter.PageNumber);
            filter.PageSize = NormalizePageSize(filter.PageSize);
            filter.Search = NormalizeSearch(filter.Search);
            filter.ReferenceNumber = NormalizeSearch(filter.ReferenceNumber, 100);
            var normalizedDateRange =
     NormalizeDateRange(
         filter.FromDate,
         filter.ToDate);

            filter.FromDate =
                normalizedDateRange.FromDate;

            filter.ToDate =
                normalizedDateRange.ToDate;

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var query = ApplyTransactionFilter(
                _context.CommissionTransactions.AsNoTracking(),
                filter);

            var totalCount = await query.CountAsync(cancellationToken);

            var summary = await query
                .GroupBy(_ => 1)
                .Select(group => new AdminFinancialTransactionSummaryDto
                {
                    TotalCount = group.Count(),
                    CompletedCount = group.Count(item =>
                        item.Status ==
                        AdminFinanceTransactionStatuses.Completed),
                    PendingCount = group.Count(item =>
                        item.Status ==
                        AdminFinanceTransactionStatuses.Pending),
                    FailedCount = group.Count(item =>
                        item.Status ==
                        AdminFinanceTransactionStatuses.Failed),
                    RejectedCount = group.Count(item =>
                        item.Status ==
                        AdminFinanceTransactionStatuses.Rejected),
                    TotalTopUps = group
                        .Where(item =>
                            item.TransactionType ==
                                AdminFinanceTransactionTypes.TopUp &&
                            item.Status ==
                                AdminFinanceTransactionStatuses.Completed)
                        .Sum(item => item.Amount),
                    TotalCommissionHeld = group
                        .Where(item =>
                            item.TransactionType ==
                            AdminFinanceTransactionTypes.CommissionHold)
                        .Sum(item => item.Amount),
                    TotalCommissionCharged = group
                        .Where(item =>
                            item.TransactionType ==
                            AdminFinanceTransactionTypes.CommissionCharge)
                        .Sum(item => item.Amount),
                    TotalCommissionReleased = group
                        .Where(item =>
                            item.TransactionType ==
                            AdminFinanceTransactionTypes.CommissionRelease)
                        .Sum(item => item.Amount),
                    TotalDebtRegistered = group
                        .Where(item => item.DebtAfter > item.DebtBefore)
                        .Sum(item => item.DebtAfter - item.DebtBefore),
                    TotalDebtPaid = group
                        .Where(item => item.DebtBefore > item.DebtAfter)
                        .Sum(item => item.DebtBefore - item.DebtAfter),
                    TotalAdjustments = group
                        .Where(item =>
                            item.TransactionType ==
                            AdminFinanceTransactionTypes.Adjustment)
                        .Sum(item => item.Amount)
                })
                .FirstOrDefaultAsync(cancellationToken) ??
                new AdminFinancialTransactionSummaryDto();

            var totalPages = totalCount <= 0
                ? 0
                : (int)Math.Ceiling((double)totalCount / filter.PageSize);

            if (totalPages > 0 && filter.PageNumber > totalPages)
            {
                filter.PageNumber = totalPages;
            }

            var transactions = await query
                .Include(transaction => transaction.Place)
                .Include(transaction => transaction.PlatformWallet)
                    .ThenInclude(wallet => wallet.Wallet)
                .Include(transaction => transaction.CreatedByUser)
                .Include(transaction => transaction.ReviewedByUser)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.CommissionTransactionId)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            var ownerNames = await GetUserNamesAsync(
                transactions.Select(transaction => transaction.Place.OwnerId),
                cancellationToken);

            return new AdminFinancialTransactionPageDto
            {
                Currency = GetCurrency(settings),
                Filter = filter,
                Summary = summary,
                Result = new AdminPagedResultDto<AdminFinancialTransactionRowDto>
                {
                    Items = transactions
                        .Select(transaction =>
                            MapTransactionRow(transaction, ownerNames))
                        .ToList(),
                    Pagination = BuildPagination(
                        filter.PageNumber,
                        filter.PageSize,
                        totalCount)
                },
                Owners = await BuildOwnerOptionsAsync(
                    filter.OwnerId,
                    cancellationToken),
                Places = await BuildPlaceOptionsAsync(
                    filter.OwnerId,
                    filter.PlaceId,
                    cancellationToken),
                PlatformWallets = await BuildPlatformWalletOptionsAsync(
                    filter.PlatformWalletId,
                    cancellationToken),
                TransactionTypeOptions = BuildOptions(
                    AdminFinanceTransactionTypes.All,
                    TransactionTypeLabel,
                    filter.TransactionType),
                StatusOptions = BuildOptions(
                    AdminFinanceTransactionStatuses.All,
                    TransactionStatusLabel,
                    filter.Status),
                DateScopeOptions = new List<AdminFinancialSelectOptionDto>
                {
                    new()
                    {
                        Value = AdminFinancialDateScopes.CreatedAt,
                        Label = "تاريخ الحركة",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.CreatedAt)
                    },
                    new()
                    {
                        Value = AdminFinancialDateScopes.TransferDate,
                        Label = "تاريخ التحويل",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.TransferDate)
                    },
                    new()
                    {
                        Value = AdminFinancialDateScopes.ReviewedAt,
                        Label = "تاريخ المراجعة",
                        IsSelected = IsState(
                            filter.DateScope,
                            AdminFinancialDateScopes.ReviewedAt)
                    }
                }
            };
        }

        public async Task<AdminFinancialTransactionDetailsDto?> GetTransactionDetailsAsync(
            int commissionTransactionId,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return null;
            }

            var transaction = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(item => item.Place)
                .Include(item => item.PlatformWallet)
                    .ThenInclude(wallet => wallet.Wallet)
                .Include(item => item.CreatedByUser)
                .Include(item => item.ReviewedByUser)
                .FirstOrDefaultAsync(item =>
                    item.CommissionTransactionId ==
                    commissionTransactionId,
                    cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            var ownerNames = await GetUserNamesAsync(
                new[] { transaction.Place?.OwnerId },
                cancellationToken);

            var row = MapTransactionRow(transaction, ownerNames);

            string? bookingState = null;
            string? bookingPaymentStatus = null;
            string? commissionStatus = null;

            if (transaction.BookingId.HasValue)
            {
                var bookingInfo = await _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.BookingId == transaction.BookingId.Value)
                    .Select(booking => new
                    {
                        booking.BookingState,
                        booking.PaymentStatus,
                        CommissionStatus = booking.FinancialRecord != null
                            ? booking.FinancialRecord.CommissionStatus
                            : null
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                bookingState = bookingInfo?.BookingState?.Trim();
                bookingPaymentStatus = bookingInfo?.PaymentStatus?.Trim();
                commissionStatus = bookingInfo?.CommissionStatus?.Trim();
            }

            return new AdminFinancialTransactionDetailsDto
            {
                CommissionTransactionId = row.CommissionTransactionId,
                PlaceId = row.PlaceId,
                PlaceName = row.PlaceName,
                OwnerId = row.OwnerId,
                OwnerName = row.OwnerName,
                BookingId = row.BookingId,
                PlatformWalletId = row.PlatformWalletId,
                PlatformWalletName = row.PlatformWalletName,
                TransactionType = row.TransactionType,
                TransactionTypeLabel = row.TransactionTypeLabel,
                Direction = row.Direction,
                Amount = row.Amount,
                AvailableBalanceBefore = row.AvailableBalanceBefore,
                AvailableBalanceAfter = row.AvailableBalanceAfter,
                HeldBalanceBefore = row.HeldBalanceBefore,
                HeldBalanceAfter = row.HeldBalanceAfter,
                DebtBefore = row.DebtBefore,
                DebtAfter = row.DebtAfter,
                Status = row.Status,
                StatusLabel = row.StatusLabel,
                ReferenceNumber = row.ReferenceNumber,
                TransferDate = row.TransferDate,
                ReceiptImage = row.ReceiptImage,
                ReviewedAt = row.ReviewedAt,
                ReviewedByUserId = row.ReviewedByUserId,
                ReviewedByName = row.ReviewedByName,
                RejectionReason = row.RejectionReason,
                CreatedAt = row.CreatedAt,
                CreatedByUserId = row.CreatedByUserId,
                CreatedByName = row.CreatedByName,
                Notes = row.Notes,
                PlaceStatus = transaction.Place?.Status?.Trim(),
                BookingState = bookingState,
                BookingPaymentStatus = bookingPaymentStatus,
                CommissionStatus = commissionStatus,
                PlatformAccountNumber =
                    transaction.PlatformWallet?.AccountNumber?.Trim(),
                PlatformBeneficiaryName =
                    transaction.PlatformWallet?.BeneficiaryName?.Trim()
            };
        }

        /* ====================================================================
           Platform wallets
           ==================================================================== */


        public async Task<AdminFinancialManagementPageDto> GetFinancialManagementAsync(
            AdminPlatformWalletFilterDto? walletFilter = null,
            CancellationToken cancellationToken = default)
        {
            var settings = await GetFinancialSettingsAsync(
                cancellationToken);

            var platformWallets = await GetPlatformWalletsAsync(
                walletFilter ?? new AdminPlatformWalletFilterDto(),
                cancellationToken);

            var systemSettings = await GetSettingsAsync(
                tracking: false,
                cancellationToken);

            return new AdminFinancialManagementPageDto
            {
                Currency = GetCurrency(systemSettings),
                LoadedAt = DateTime.Now,
                Settings = settings,
                PlatformWallets = platformWallets
            };
        }

        public async Task<AdminPlatformWalletPageDto> GetPlatformWalletsAsync(
            AdminPlatformWalletFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminPlatformWalletFilterDto();
            filter.Search = NormalizeSearch(filter.Search);

            var query = _context.PlatformWallets
                .AsNoTracking()
                .Include(wallet => wallet.Wallet)
                .AsQueryable();

            if (filter.WalletId.HasValue)
            {
                query = query.Where(wallet =>
                    wallet.WalletId == filter.WalletId.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(wallet =>
                    wallet.IsActive == filter.IsActive.Value);
            }

            if (filter.IsPrimary.HasValue)
            {
                query = query.Where(wallet =>
                    wallet.IsPrimary == filter.IsPrimary.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(wallet =>
                    wallet.Wallet.WalletName.Contains(search) ||
                    wallet.AccountNumber.Contains(search) ||
                    wallet.BeneficiaryName.Contains(search) ||
                    (wallet.Instructions != null &&
                     wallet.Instructions.Contains(search)));
            }

            var wallets = await query
                .OrderByDescending(wallet => wallet.IsPrimary)
                .ThenByDescending(wallet => wallet.IsActive)
                .ThenBy(wallet => wallet.Wallet.WalletName)
                .ThenBy(wallet => wallet.AccountNumber)
                .ToListAsync(cancellationToken);

            var walletIds = wallets
                .Select(wallet => wallet.PlatformWalletId)
                .ToList();

            var usage = walletIds.Count == 0
                ? new List<PlatformWalletUsageProjection>()
                : await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        transaction.PlatformWalletId.HasValue &&
                        walletIds.Contains(transaction.PlatformWalletId.Value) &&
                        transaction.TransactionType ==
                            AdminFinanceTransactionTypes.TopUp)
                    .GroupBy(transaction => transaction.PlatformWalletId!.Value)
                    .Select(group => new PlatformWalletUsageProjection
                    {
                        PlatformWalletId = group.Key,
                        TotalCount = group.Count(),
                        PendingCount = group.Count(item =>
                            item.Status ==
                            AdminCommissionTopUpStatuses.Pending),
                        CompletedAmount = group
                            .Where(item =>
                                item.Status ==
                                AdminCommissionTopUpStatuses.Completed)
                            .Sum(item => item.Amount),
                        LastUsedAt = group.Max(item => item.CreatedAt)
                    })
                    .ToListAsync(cancellationToken);

            var usageByWallet = usage.ToDictionary(
                item => item.PlatformWalletId);

            var pendingTopUpsAmount = await _context.CommissionTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.TransactionType ==
                        AdminFinanceTransactionTypes.TopUp &&
                    transaction.Status ==
                        AdminCommissionTopUpStatuses.Pending)
                .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0m;

            var completedTopUpsAmount = await _context.CommissionTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.TransactionType ==
                        AdminFinanceTransactionTypes.TopUp &&
                    transaction.Status ==
                        AdminCommissionTopUpStatuses.Completed)
                .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0m;

            return new AdminPlatformWalletPageDto
            {
                Filter = filter,
                Summary = new AdminPlatformWalletSummaryDto
                {
                    TotalCount = wallets.Count,
                    ActiveCount = wallets.Count(wallet => wallet.IsActive == true),
                    InactiveCount = wallets.Count(wallet => wallet.IsActive != true),
                    PrimaryCount = wallets.Count(wallet => wallet.IsPrimary),
                    UsedWalletsCount = usage.Count(item => item.TotalCount > 0),
                    PendingTopUpsCount = usage.Sum(item => item.PendingCount),
                    PendingTopUpsAmount = pendingTopUpsAmount,
                    CompletedTopUpsAmount = completedTopUpsAmount
                },
                Wallets = wallets
                    .Select(wallet =>
                    {
                        usageByWallet.TryGetValue(
                            wallet.PlatformWalletId,
                            out var walletUsage);

                        return new AdminPlatformWalletRowDto
                        {
                            PlatformWalletId = wallet.PlatformWalletId,
                            WalletId = wallet.WalletId,
                            WalletName = FirstNonEmpty(
                                "حساب منصة",
                                wallet.Wallet?.WalletName),
                            LogoPath = wallet.Wallet?.LogoPath?.Trim(),
                            AccountNumber = wallet.AccountNumber?.Trim() ?? string.Empty,
                            BeneficiaryName = wallet.BeneficiaryName?.Trim() ?? string.Empty,
                            IsActive = wallet.IsActive == true,
                            IsPrimary = wallet.IsPrimary,
                            Instructions = wallet.Instructions?.Trim(),
                            InstructionsEn = wallet.InstructionsEn?.Trim(),
                            CreatedAt = wallet.CreatedAt,
                            UpdatedAt = wallet.UpdatedAt,
                            TotalTopUpRequestsCount = walletUsage?.TotalCount ?? 0,
                            PendingTopUpRequestsCount = walletUsage?.PendingCount ?? 0,
                            CompletedTopUpAmount = walletUsage?.CompletedAmount ?? 0m,
                            LastUsedAt = walletUsage?.LastUsedAt,
                            CanDisable = walletUsage?.PendingCount is null or 0
                        };
                    })
                    .ToList(),
                SupportedWallets = await _context.SupportedWallets
                    .AsNoTracking()
                    .OrderBy(wallet => wallet.WalletName)
                    .Select(wallet => new AdminSupportedWalletOptionDto
                    {
                        WalletId = wallet.WalletId,
                        WalletName = wallet.WalletName,
                        LogoPath = wallet.LogoPath,
                        IsActive = wallet.IsActive == true,
                        IsSelected = filter.WalletId == wallet.WalletId
                    })
                    .ToListAsync(cancellationToken)
            };
        }

        private sealed class PlatformWalletUsageProjection
        {
            public int PlatformWalletId { get; set; }

            public int TotalCount { get; set; }

            public int PendingCount { get; set; }

            public decimal CompletedAmount { get; set; }

            public DateTime? LastUsedAt { get; set; }
        }

        private async Task AssignPrimaryPlatformWalletAsync(
            PlatformWallet selectedWallet,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (selectedWallet.PlatformWalletId <= 0)
            {
                throw new InvalidOperationException(
                    "يجب حفظ حساب المنصة قبل تعيينه كحساب رئيسي.");
            }

            if (selectedWallet.IsActive != true)
            {
                throw new InvalidOperationException(
                    "لا يمكن تعيين حساب معطل كحساب رئيسي.");
            }

            var oldPrimaryWallets = await _context.PlatformWallets
                .Where(wallet =>
                    wallet.IsPrimary &&
                    wallet.PlatformWalletId != selectedWallet.PlatformWalletId)
                .ToListAsync(cancellationToken);

            if (oldPrimaryWallets.Count > 0)
            {
                foreach (var oldPrimary in oldPrimaryWallets)
                {
                    oldPrimary.IsPrimary = false;
                    oldPrimary.UpdatedAt = now;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            if (!selectedWallet.IsPrimary)
            {
                selectedWallet.IsPrimary = true;
                selectedWallet.UpdatedAt = now;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<FinanceOperationResult> SavePlatformWalletAsync(
            AdminPlatformWalletEditDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.WalletId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "بيانات حساب المنصة غير مكتملة.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            if (request.IsPrimary && !request.IsActive)
            {
                return FinanceOperationResult.Failed(
                    "لا يمكن تعيين حساب معطل كحساب رئيسي.");
            }

            var accountNumber =
                NormalizePlatformAccountNumber(
                    request.AccountNumber);

            var beneficiaryName =
                request.BeneficiaryName?
                    .Trim() ??
                string.Empty;

            if (
                accountNumber.Length < 6 ||
                accountNumber.Length > 30
            )
            {
                return FinanceOperationResult.Failed(
                    "رقم الحساب يجب أن يتكون من 6 إلى 30 رقمًا.");
            }

            if (
                accountNumber.Any(character =>
                    character < '0' ||
                    character > '9')
            )
            {
                return FinanceOperationResult.Failed(
                    "رقم الحساب يجب أن يحتوي على أرقام فقط دون حروف.");
            }

            if (
                beneficiaryName.Length < 2 ||
                beneficiaryName.Length > 150
            )
            {
                return FinanceOperationResult.Failed(
                    "اسم المستفيد يجب أن يكون بين 2 و150 حرفًا.");
            }

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                var supportedWallet = await _context.SupportedWallets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(wallet =>
                        wallet.WalletId == request.WalletId,
                        cancellationToken);

                if (supportedWallet == null)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "شبكة الدفع أو البنك غير موجود.");
                }

                if (request.IsActive && supportedWallet.IsActive != true)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يمكن تفعيل حساب مرتبط بشبكة دفع معطلة.");
                }

                var duplicate = await _context.PlatformWallets
                    .AsNoTracking()
                    .AnyAsync(wallet =>
                        wallet.WalletId == request.WalletId &&
                        (
                            !request.PlatformWalletId.HasValue ||
                            wallet.PlatformWalletId !=
                                request.PlatformWalletId.Value
                        ),
                        cancellationToken);

                if (duplicate)
                {
                    await databaseTransaction.RollbackAsync(
                        cancellationToken);

                    return FinanceOperationResult.Failed(
                        "يوجد حساب منصة مسجل لهذه الشبكة بالفعل. عدّل الحساب الموجود بدل إضافة حساب جديد.");
                }

                PlatformWallet entity;
                var now = DateTime.Now;
                var isNew = !request.PlatformWalletId.HasValue;

                if (!isNew)
                {
                    entity = await _context.PlatformWallets
                        .FirstOrDefaultAsync(wallet =>
                            wallet.PlatformWalletId ==
                            request.PlatformWalletId!.Value,
                            cancellationToken) ??
                        throw new InvalidOperationException(
                            "حساب المنصة المطلوب تعديله غير موجود.");
                }
                else
                {
                    entity = new PlatformWallet
                    {
                        CreatedAt = now,
                        IsPrimary = false
                    };

                    _context.PlatformWallets.Add(entity);
                }

                var wasPrimary = entity.IsPrimary;

                if (!request.IsActive)
                {
                    var pendingRequests = !isNew
                        ? await _context.CommissionTransactions
                            .AsNoTracking()
                            .CountAsync(transaction =>
                                transaction.PlatformWalletId == entity.PlatformWalletId &&
                                transaction.TransactionType ==
                                    AdminFinanceTransactionTypes.TopUp &&
                                transaction.Status ==
                                    AdminCommissionTopUpStatuses.Pending,
                                cancellationToken)
                        : 0;

                    if (pendingRequests > 0)
                    {
                        await databaseTransaction.RollbackAsync(cancellationToken);
                        return FinanceOperationResult.Failed(
                            "لا يمكن تعطيل الحساب لوجود طلبات شحن معلقة مرتبطة به.");
                    }
                }

                entity.WalletId = request.WalletId;
                entity.AccountNumber = accountNumber;
                entity.BeneficiaryName = beneficiaryName;
                entity.IsActive = request.IsActive;
                entity.Instructions = string.IsNullOrWhiteSpace(request.Instructions)
                    ? null
                    : request.Instructions.Trim();
                
                entity.InstructionsEn = string.IsNullOrWhiteSpace(request.InstructionsEn)
                    ? null
                    : request.InstructionsEn.Trim();
                
                entity.UpdatedAt = now;

                if (!request.IsActive)
                {
                    entity.IsPrimary = false;
                }

                await _context.SaveChangesAsync(cancellationToken);

                PlatformWallet? replacementPrimary = null;

                if (!request.IsActive && wasPrimary)
                {
                    replacementPrimary = await _context.PlatformWallets
                        .Where(wallet =>
                            wallet.PlatformWalletId != entity.PlatformWalletId &&
                            wallet.IsActive == true)
                        .OrderBy(wallet => wallet.PlatformWalletId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (replacementPrimary == null)
                    {
                        await databaseTransaction.RollbackAsync(cancellationToken);
                        return FinanceOperationResult.Failed(
                            "فعّل حساب منصة آخر قبل تعطيل الحساب الرئيسي.");
                    }
                }

                var hasAnotherPrimary = await _context.PlatformWallets
                    .AsNoTracking()
                    .AnyAsync(wallet =>
                        wallet.IsPrimary &&
                        wallet.PlatformWalletId != entity.PlatformWalletId,
                        cancellationToken);

                var shouldBePrimary =
                    request.IsActive &&
                    (
                        request.IsPrimary ||
                        wasPrimary ||
                        !hasAnotherPrimary
                    );

                if (replacementPrimary != null)
                {
                    await AssignPrimaryPlatformWalletAsync(
                        replacementPrimary,
                        now,
                        cancellationToken);
                }
                else if (shouldBePrimary)
                {
                    await AssignPrimaryPlatformWalletAsync(
                        entity,
                        now,
                        cancellationToken);
                }

                await databaseTransaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "حفظ الأدمن {AdminUserId} حساب المنصة {PlatformWalletId}.",
                    adminUserId,
                    entity.PlatformWalletId);

                return FinanceOperationResult.Succeeded(
                    isNew
                        ? "تمت إضافة حساب المنصة."
                        : "تم تحديث حساب المنصة.",
                    entity.PlatformWalletId);
            }
            catch (InvalidOperationException exception)
            {
                await databaseTransaction.RollbackAsync(
                    cancellationToken);

                return FinanceOperationResult.Failed(
                    exception.Message);
            }
            catch (DbUpdateException exception)
            {
                await databaseTransaction.RollbackAsync(
                    cancellationToken);

                _logger.LogWarning(
                    exception,
                    "رفضت قاعدة البيانات حفظ حساب منصة مكرر للشبكة {WalletId} بواسطة الأدمن {AdminUserId}.",
                    request.WalletId,
                    adminUserId);

                return FinanceOperationResult.Failed(
                    "يوجد حساب منصة مسجل لهذه الشبكة بالفعل.");
            }
            catch (Exception exception)
            {
                await databaseTransaction.RollbackAsync(
                    cancellationToken);

                _logger.LogError(
                    exception,
                    "فشل حفظ حساب منصة بواسطة الأدمن {AdminUserId}.",
                    adminUserId);

                return FinanceOperationResult.Failed(
                    "تعذر حفظ حساب المنصة حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> ChangePlatformWalletStatusAsync(
            AdminPlatformWalletStatusRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.PlatformWalletId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "رقم حساب المنصة غير صحيح.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                var wallet = await _context.PlatformWallets
                    .Include(item => item.Wallet)
                    .FirstOrDefaultAsync(item =>
                        item.PlatformWalletId == request.PlatformWalletId,
                        cancellationToken);

                if (wallet == null)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "حساب المنصة غير موجود.");
                }

                if (request.IsActive && wallet.Wallet.IsActive != true)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يمكن تفعيل الحساب لأن شبكة الدفع المرتبطة به معطلة.");
                }

                if ((wallet.IsActive == true) == request.IsActive)
                {
                    await databaseTransaction.CommitAsync(cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        request.IsActive
                            ? "حساب المنصة مفعل مسبقًا."
                            : "حساب المنصة معطل مسبقًا.",
                        wallet.PlatformWalletId);
                }

                var now = DateTime.Now;
                PlatformWallet? replacement = null;

                if (!request.IsActive)
                {
                    var pendingRequests = await _context.CommissionTransactions
                        .AsNoTracking()
                        .CountAsync(transaction =>
                            transaction.PlatformWalletId == wallet.PlatformWalletId &&
                            transaction.TransactionType ==
                                AdminFinanceTransactionTypes.TopUp &&
                            transaction.Status ==
                                AdminCommissionTopUpStatuses.Pending,
                            cancellationToken);

                    if (pendingRequests > 0)
                    {
                        await databaseTransaction.RollbackAsync(cancellationToken);
                        return FinanceOperationResult.Failed(
                            "لا يمكن تعطيل الحساب لوجود طلبات شحن معلقة مرتبطة به.");
                    }

                    if (wallet.IsPrimary)
                    {
                        replacement = await _context.PlatformWallets
                            .Where(item =>
                                item.PlatformWalletId != wallet.PlatformWalletId &&
                                item.IsActive == true)
                            .OrderBy(item => item.PlatformWalletId)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (replacement == null)
                        {
                            await databaseTransaction.RollbackAsync(cancellationToken);
                            return FinanceOperationResult.Failed(
                                "فعّل حساب منصة آخر قبل تعطيل الحساب الرئيسي.");
                        }
                    }

                    wallet.IsPrimary = false;
                    wallet.IsActive = false;
                    wallet.UpdatedAt = now;
                    await _context.SaveChangesAsync(cancellationToken);

                    if (replacement != null)
                    {
                        await AssignPrimaryPlatformWalletAsync(
                            replacement,
                            now,
                            cancellationToken);
                    }
                }
                else
                {
                    wallet.IsActive = true;
                    wallet.UpdatedAt = now;
                    await _context.SaveChangesAsync(cancellationToken);

                    var hasPrimary = await _context.PlatformWallets
                        .AsNoTracking()
                        .AnyAsync(item => item.IsPrimary, cancellationToken);

                    if (!hasPrimary)
                    {
                        await AssignPrimaryPlatformWalletAsync(
                            wallet,
                            now,
                            cancellationToken);
                    }
                }

                await databaseTransaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "غير الأدمن {AdminUserId} حالة حساب المنصة {WalletId} إلى {IsActive}.",
                    adminUserId,
                    wallet.PlatformWalletId,
                    wallet.IsActive);

                return FinanceOperationResult.Succeeded(
                    wallet.IsActive == true
                        ? "تم تفعيل حساب المنصة."
                        : "تم تعطيل حساب المنصة.",
                    wallet.PlatformWalletId);
            }
            catch (Exception exception)
            {
                await databaseTransaction.RollbackAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "فشل تغيير حالة حساب المنصة {WalletId}.",
                    request.PlatformWalletId);

                return FinanceOperationResult.Failed(
                    "تعذر تغيير حالة حساب المنصة حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> SetPrimaryPlatformWalletAsync(
            AdminSetPrimaryPlatformWalletRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.PlatformWalletId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "رقم حساب المنصة غير صحيح.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                var selectedWallet = await _context.PlatformWallets
                    .FirstOrDefaultAsync(wallet =>
                        wallet.PlatformWalletId == request.PlatformWalletId,
                        cancellationToken);

                if (selectedWallet == null)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "حساب المنصة غير موجود.");
                }

                if (selectedWallet.IsActive != true)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يمكن تعيين حساب معطل كحساب رئيسي.");
                }

                if (selectedWallet.IsPrimary)
                {
                    await databaseTransaction.CommitAsync(cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "الحساب محدد كرئيسي مسبقًا.",
                        selectedWallet.PlatformWalletId);
                }

                await AssignPrimaryPlatformWalletAsync(
                    selectedWallet,
                    DateTime.Now,
                    cancellationToken);

                await databaseTransaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "عين الأدمن {AdminUserId} حساب المنصة {WalletId} كحساب رئيسي.",
                    adminUserId,
                    selectedWallet.PlatformWalletId);

                return FinanceOperationResult.Succeeded(
                    "تم تعيين حساب المنصة كحساب رئيسي.",
                    selectedWallet.PlatformWalletId);
            }
            catch (Exception exception)
            {
                await databaseTransaction.RollbackAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "فشل تعيين حساب المنصة {WalletId} كحساب رئيسي.",
                    request.PlatformWalletId);

                return FinanceOperationResult.Failed(
                    "تعذر تعيين الحساب الرئيسي حاليًا.");
            }
        }

        /* ====================================================================
           Financial settings
           ==================================================================== */

        public async Task<AdminFinancialSettingsPageDto> GetFinancialSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var defaultCommissionPercentage =
                Math.Round(
                    settings?.CommissionPercentage ?? 0m,
                    2);

            var categoryCommissions =
                await _context.Categories
                    .AsNoTracking()
                    .OrderByDescending(category =>
                        category.HasBookingSystem)
                    .ThenBy(category =>
                        category.CategoryName)
                    .Select(category =>
                        new AdminCategoryCommissionDto
                        {
                            CategoryId =
                                category.CategoryId,

                            CategoryName =
                                category.CategoryName,

                            IconClass =
                                category.IconClass,

                            ThemeColor =
                                category.ThemeColor,

                            ImagePath =
                                category.ImagePath,

                            HasBookingSystem =
                                category.HasBookingSystem,

                            PlacesCount =
                                category.Places.Count(),

                            CommissionPercentage =
                                category.CommissionPercentage,

                            EffectiveCommissionPercentage =
                                category.CommissionPercentage ??
                                defaultCommissionPercentage
                        })
                    .ToListAsync(
                        cancellationToken);

            return new AdminFinancialSettingsPageDto
            {
                Settings =
                    new AdminFinancialSettingsDto
                    {
                        Id =
                            settings?.Id ?? 0,

                        CommissionPercentage =
                            defaultCommissionPercentage,

                        DefaultCurrency =
                            GetCurrency(settings),

                        MinimumCommissionTopUp =
                            settings?.MinimumCommissionTopUp ??
                            20000m,

                        DefaultLowBalanceThreshold =
                            settings?.DefaultLowBalanceThreshold ??
                            2000m,

                        AllowNegativeCommissionBalance =
                            settings?.AllowNegativeCommissionBalance ??
                            false,

                        DefaultCommissionDebtLimit =
                            settings?.DefaultCommissionDebtLimit ??
                            0m,

                        CommissionDebtGraceDays =
                            settings?.CommissionDebtGraceDays ??
                            3,

                        SuspendBookingWhenBalanceInsufficient =
                            settings?.SuspendBookingWhenBalanceInsufficient ??
                            true,

                        OwnerFreeTrialEnabled = settings?.OwnerFreeTrialEnabled ?? false,
                        OwnerFreeTrialDays = settings?.OwnerFreeTrialDays ?? 0,
                        RequiredCommissionGuaranteeBalance = settings?.RequiredCommissionGuaranteeBalance ?? 0m,
                        ReturnCommissionOnCancellation = settings?.ReturnCommissionOnCancellation ?? true,
                        HidePlaceWhenCommissionSuspended =
                            settings?.HidePlaceWhenCommissionSuspended ??
                            false
                    },

                CategoryCommissions =
                    categoryCommissions,

                PlatformDisplayName =
                    FirstNonEmpty(
                        "دليلك اليمني",
                        settings?.SiteName),

                PlatformLogoPath =
                    settings?.LogoPath?.Trim(),

                ContactEmail =
                    settings?.ContactEmail?.Trim(),

                SupportPhone =
                    settings?.SupportPhone?.Trim(),

                LoadedAt =
                    DateTime.Now
            };
        }

        public async Task<FinanceOperationResult> SaveFinancialSettingsAsync(
            AdminFinancialSettingsUpdateDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (request != null && (request.OwnerFreeTrialDays < 0 || request.OwnerFreeTrialDays > 3650 ||
                (request.OwnerFreeTrialEnabled && request.OwnerFreeTrialDays == 0) || request.RequiredCommissionGuaranteeBalance < 0m))
                return FinanceOperationResult.Failed("مدة الفترة المجانية أو مبلغ ضمان العمولات غير صحيح.");
            if (request == null)
            {
                return FinanceOperationResult.Failed(
                    "بيانات الإعدادات المالية غير مكتملة.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            if (
                request.CommissionPercentage < 0m ||
                request.CommissionPercentage > 100m
            )
            {
                return FinanceOperationResult.Failed(
                    "نسبة العمولة الافتراضية يجب أن تكون بين 0 و100.");
            }

            if (request.MinimumCommissionTopUp <= 0m)
            {
                return FinanceOperationResult.Failed(
                    "الحد الأدنى للشحن يجب أن يكون أكبر من صفر.");
            }

            if (
                request.DefaultLowBalanceThreshold < 0m ||
                request.DefaultCommissionDebtLimit < 0m ||
                request.CommissionDebtGraceDays < 0
            )
            {
                return FinanceOperationResult.Failed(
                    "قيم الرصيد والدين لا يمكن أن تكون سالبة.");
            }

            if (
                request.CommissionDebtGraceDays >
                3650
            )
            {
                return FinanceOperationResult.Failed(
                    "مهلة سداد الدين يجب أن تكون بين 0 و3650 يومًا.");
            }

            if (
                request.AllowNegativeCommissionBalance &&
                request.DefaultCommissionDebtLimit <= 0m
            )
            {
                return FinanceOperationResult.Failed(
                    "حدد حد دين أكبر من صفر عند السماح بالرصيد السالب.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.DefaultCurrency))
            {
                return FinanceOperationResult.Failed(
                    "اكتب العملة الافتراضية.");
            }

            if (request.DefaultCurrency.Trim().Length > 50)
            {
                return FinanceOperationResult.Failed(
                    "اسم العملة يجب ألا يتجاوز 50 حرفًا.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.ChangeReason))
            {
                return FinanceOperationResult.Failed(
                    "اكتب سبب تعديل الإعدادات المالية.");
            }

            var categoryRequests =
                request.CategoryCommissions ??
                new List<AdminCategoryCommissionUpdateDto>();

            var invalidCategoryCommission =
                categoryRequests.FirstOrDefault(item =>
                    item.CategoryId <= 0 ||
                    (
                        item.CommissionPercentage.HasValue &&
                        (
                            item.CommissionPercentage.Value < 0m ||
                            item.CommissionPercentage.Value > 100m
                        )
                    ));

            if (invalidCategoryCommission != null)
            {
                return FinanceOperationResult.Failed(
                    "إحدى نسب عمولات الفئات غير صحيحة.");
            }

            var duplicatedCategoryId =
                categoryRequests
                    .GroupBy(item =>
                        item.CategoryId)
                    .Where(group =>
                        group.Count() > 1)
                    .Select(group =>
                        group.Key)
                    .FirstOrDefault();

            if (duplicatedCategoryId > 0)
            {
                return FinanceOperationResult.Failed(
                    $"الفئة رقم {duplicatedCategoryId} مكررة في طلب الحفظ.");
            }

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                var settings =
                    await GetSettingsAsync(
                        tracking: true,
                        cancellationToken);

                if (settings == null)
                {
                    settings =
                        new SystemSetting
                        {
                            SiteName =
                                "دليلك اليمني"
                        };

                    _context.SystemSettings.Add(
                        settings);
                }

                var requestedCategoryIds =
                    categoryRequests
                        .Select(item =>
                            item.CategoryId)
                        .Distinct()
                        .ToList();

                var categories =
                    requestedCategoryIds.Count == 0
                        ? new List<Category>()
                        : await _context.Categories
                            .Where(category =>
                                requestedCategoryIds.Contains(
                                    category.CategoryId))
                            .ToListAsync(
                                cancellationToken);

                if (
                    categories.Count !=
                    requestedCategoryIds.Count
                )
                {
                    var existingCategoryIds =
                        categories
                            .Select(category =>
                                category.CategoryId)
                            .ToHashSet();

                    var missingCategoryIds =
                        requestedCategoryIds
                            .Where(categoryId =>
                                !existingCategoryIds.Contains(
                                    categoryId))
                            .ToList();

                    await databaseTransaction.RollbackAsync(
                        cancellationToken);

                    return FinanceOperationResult.Failed(
                        $"بعض الفئات غير موجودة: {string.Join(", ", missingCategoryIds)}.");
                }

                var oldSettingsValues =
                    new
                    {
                        settings.CommissionPercentage,
                        settings.DefaultCurrency,
                        settings.MinimumCommissionTopUp,
                        settings.DefaultLowBalanceThreshold,
                        settings.AllowNegativeCommissionBalance,
                        settings.DefaultCommissionDebtLimit,
                        settings.CommissionDebtGraceDays,
                        settings.SuspendBookingWhenBalanceInsufficient,
                        settings.HidePlaceWhenCommissionSuspended
                    };

                var oldCategoryValues =
                    categories
                        .Select(category =>
                            new
                            {
                                category.CategoryId,
                                category.CategoryName,
                                category.CommissionPercentage
                            })
                        .ToList();

                settings.CommissionPercentage =
                    Math.Round(
                        request.CommissionPercentage,
                        2);

                settings.DefaultCurrency =
                    request.DefaultCurrency.Trim();

                settings.MinimumCommissionTopUp =
                    Math.Round(
                        request.MinimumCommissionTopUp,
                        2);

                settings.DefaultLowBalanceThreshold =
                    Math.Round(
                        request.DefaultLowBalanceThreshold,
                        2);

                settings.AllowNegativeCommissionBalance =
                    request.AllowNegativeCommissionBalance;

                settings.DefaultCommissionDebtLimit =
                    request.AllowNegativeCommissionBalance
                        ? Math.Round(
                            request.DefaultCommissionDebtLimit,
                            2)
                        : 0m;

                settings.CommissionDebtGraceDays =
                    request.CommissionDebtGraceDays;

                settings.SuspendBookingWhenBalanceInsufficient =
                    request.SuspendBookingWhenBalanceInsufficient;

                settings.HidePlaceWhenCommissionSuspended = request.HidePlaceWhenCommissionSuspended;
                settings.OwnerFreeTrialEnabled = request.OwnerFreeTrialEnabled;
                settings.OwnerFreeTrialDays = request.OwnerFreeTrialDays;
                settings.RequiredCommissionGuaranteeBalance = Math.Round(request.RequiredCommissionGuaranteeBalance, 2, MidpointRounding.AwayFromZero);
                if (request.ReturnCommissionOnCancellation.HasValue)
                    settings.ReturnCommissionOnCancellation = request.ReturnCommissionOnCancellation.Value;

                var categoryRequestById =
                    categoryRequests
                        .ToDictionary(
                            item =>
                                item.CategoryId);

                foreach (var category in categories)
                {
                    var categoryRequest =
                        categoryRequestById[
                            category.CategoryId];

                    category.CommissionPercentage =
                        categoryRequest.CommissionPercentage.HasValue
                            ? Math.Round(
                                categoryRequest.CommissionPercentage.Value,
                                2)
                            : null;
                }

                await _context.SaveChangesAsync(
                    cancellationToken);

                var accounts =
                    await _context.PlaceCommissionAccounts
                        .ToListAsync(
                            cancellationToken);

                foreach (var account in accounts)
                {
                    RefreshAccountStatus(
                        account,
                        settings);
                }

                await _context.SaveChangesAsync(
                    cancellationToken);

                await databaseTransaction.CommitAsync(
                    cancellationToken);

                var changedCategoryCount =
                    categories.Count(category =>
                    {
                        var oldCategory =
                            oldCategoryValues.First(item =>
                                item.CategoryId ==
                                category.CategoryId);

                        return oldCategory.CommissionPercentage !=
                            category.CommissionPercentage;
                    });

                _logger.LogInformation(
                    "عدل الأدمن {AdminUserId} الإعدادات المالية. " +
                    "القيم القديمة: {@OldSettingsValues}. " +
                    "القيم الجديدة: {@NewSettingsValues}. " +
                    "قيم الفئات القديمة: {@OldCategoryValues}. " +
                    "عدد نسب الفئات المعدلة: {ChangedCategoryCount}. " +
                    "السبب: {Reason}",
                    adminUserId,
                    oldSettingsValues,
                    request,
                    oldCategoryValues,
                    changedCategoryCount,
                    request.ChangeReason.Trim());

                return FinanceOperationResult.Succeeded(
                    changedCategoryCount > 0
                        ? $"تم حفظ الإعدادات المالية وتحديث عمولات {changedCategoryCount} فئة."
                        : "تم حفظ الإعدادات المالية وتحديث حالات حسابات الضمان.",
                    settings.Id);
            }
            catch (Exception exception)
            {
                await databaseTransaction.RollbackAsync(
                    cancellationToken);

                _logger.LogError(
                    exception,
                    "فشل حفظ الإعدادات المالية بواسطة الأدمن {AdminUserId}.",
                    adminUserId);

                return FinanceOperationResult.Failed(
                    "تعذر حفظ الإعدادات المالية حاليًا.");
            }
        }

        /* ====================================================================
           Reports
           ==================================================================== */

        private static AdminFinancialReportFilterDto NormalizeReportFilter(
            AdminFinancialReportFilterDto filter,
            bool includeAllRows)
        {
            filter ??= new AdminFinancialReportFilterDto();

            if (!AdminFinancialReportTypes.All.Contains(
                filter.ReportType,
                StringComparer.OrdinalIgnoreCase))
            {
                filter.ReportType =
                    AdminFinancialReportTypes.BookingCommissions;
            }

            filter.PageNumber = NormalizePageNumber(filter.PageNumber);
            filter.PageSize = includeAllRows
                ? MaximumPageSize
                : NormalizePageSize(filter.PageSize);
            filter.Search = NormalizeSearch(filter.Search);
            filter.ReferenceNumber = NormalizeSearch(filter.ReferenceNumber, 100);
            var normalizedDateRange =
     NormalizeDateRange(
         filter.FromDate,
         filter.ToDate);

            filter.FromDate =
                normalizedDateRange.FromDate;

            filter.ToDate =
                normalizedDateRange.ToDate;

            switch (filter.ReportType)
            {
                case AdminFinancialReportTypes.BookingCommissions:
                    filter.CommissionTransactionId = null;
                    filter.PlatformWalletId = null;
                    filter.AccountStatus = null;
                    filter.TransactionType = null;
                    filter.TransactionStatus = null;
                    filter.TopUpStatus = null;
                    filter.ReferenceNumber = null;
                    filter.IsLowBalance = null;
                    filter.IsSuspended = null;
                    filter.DateScope = new[]
                    {
                        AdminFinancialDateScopes.BookingCreatedAt,
                        AdminFinancialDateScopes.CommissionHeldAt,
                        AdminFinancialDateScopes.CommissionSettledAt
                    }.Contains(filter.DateScope, StringComparer.OrdinalIgnoreCase)
                        ? filter.DateScope
                        : AdminFinancialDateScopes.BookingCreatedAt;
                    break;

                case AdminFinancialReportTypes.TopUps:
                    filter.BookingId = null;
                    filter.CommissionStatus = null;
                    filter.AccountStatus = null;
                    filter.TransactionType = null;
                    filter.TransactionStatus = null;
                    filter.HasDebt = null;
                    filter.IsLowBalance = null;
                    filter.IsSuspended = null;
                    filter.DateScope = new[]
                    {
                        AdminFinancialDateScopes.CreatedAt,
                        AdminFinancialDateScopes.TransferDate,
                        AdminFinancialDateScopes.ReviewedAt
                    }.Contains(filter.DateScope, StringComparer.OrdinalIgnoreCase)
                        ? filter.DateScope
                        : AdminFinancialDateScopes.CreatedAt;
                    break;

                case AdminFinancialReportTypes.CommissionAccounts:
                case AdminFinancialReportTypes.Debts:
                case AdminFinancialReportTypes.LowBalanceAccounts:
                case AdminFinancialReportTypes.SuspendedAccounts:
                    filter.BookingId = null;
                    filter.CommissionTransactionId = null;
                    filter.PlatformWalletId = null;
                    filter.CommissionStatus = null;
                    filter.TransactionType = null;
                    filter.TransactionStatus = null;
                    filter.TopUpStatus = null;
                    filter.ReferenceNumber = null;
                    filter.DateScope = AdminFinancialDateScopes.UpdatedAt;
                    break;

                case AdminFinancialReportTypes.Transactions:
                    filter.CommissionStatus = null;
                    filter.AccountStatus = null;
                    filter.TopUpStatus = null;
                    filter.HasDebt = null;
                    filter.IsLowBalance = null;
                    filter.IsSuspended = null;
                    filter.DateScope = new[]
                    {
                        AdminFinancialDateScopes.CreatedAt,
                        AdminFinancialDateScopes.TransferDate,
                        AdminFinancialDateScopes.ReviewedAt
                    }.Contains(filter.DateScope, StringComparer.OrdinalIgnoreCase)
                        ? filter.DateScope
                        : AdminFinancialDateScopes.CreatedAt;
                    break;

                case AdminFinancialReportTypes.Comprehensive:
                    filter.CommissionTransactionId = null;
                    filter.PlatformWalletId = null;
                    filter.CommissionStatus = null;
                    filter.AccountStatus = null;
                    filter.TransactionType = null;
                    filter.TransactionStatus = null;
                    filter.TopUpStatus = null;
                    filter.ReferenceNumber = null;
                    filter.HasDebt = null;
                    filter.IsLowBalance = null;
                    filter.IsSuspended = null;
                    filter.DateScope = AdminFinancialDateScopes.BookingCreatedAt;
                    break;
            }

            return filter;
        }

        private static AdminFinancialReportFilterCapabilitiesDto
            BuildReportCapabilities(string reportType)
        {
            var capabilities = new AdminFinancialReportFilterCapabilitiesDto
            {
                ShowOwner = true,
                ShowPlace = true,
                ShowDateScope = true,
                ShowSearch = true
            };

            switch (reportType)
            {
                case AdminFinancialReportTypes.BookingCommissions:
                    capabilities.ShowBookingId = true;
                    capabilities.ShowCommissionStatus = true;
                    capabilities.ShowHasDebt = true;
                    break;

                case AdminFinancialReportTypes.TopUps:
                    capabilities.ShowTransactionId = true;
                    capabilities.ShowPlatformWallet = true;
                    capabilities.ShowTopUpStatus = true;
                    capabilities.ShowReferenceNumber = true;
                    break;

                case AdminFinancialReportTypes.CommissionAccounts:
                    capabilities.ShowAccountStatus = true;
                    capabilities.ShowHasDebt = true;
                    capabilities.ShowLowBalance = true;
                    capabilities.ShowSuspended = true;
                    capabilities.ShowDateScope = false;
                    break;

                case AdminFinancialReportTypes.Debts:
                    capabilities.ShowAccountStatus = true;
                    capabilities.ShowHasDebt = false;
                    capabilities.ShowDateScope = false;
                    break;

                case AdminFinancialReportTypes.Transactions:
                    capabilities.ShowBookingId = true;
                    capabilities.ShowTransactionId = true;
                    capabilities.ShowPlatformWallet = true;
                    capabilities.ShowTransactionType = true;
                    capabilities.ShowTransactionStatus = true;
                    capabilities.ShowReferenceNumber = true;
                    break;

                case AdminFinancialReportTypes.LowBalanceAccounts:
                    capabilities.ShowAccountStatus = true;
                    capabilities.ShowDateScope = false;
                    break;

                case AdminFinancialReportTypes.SuspendedAccounts:
                    capabilities.ShowAccountStatus = false;
                    capabilities.ShowDateScope = false;
                    break;

                case AdminFinancialReportTypes.Comprehensive:
                    capabilities.ShowBookingId = false;
                    break;
            }

            return capabilities;
        }

        private static string DateScopeLabel(string? dateScope) =>
            dateScope switch
            {
                AdminFinancialDateScopes.BookingCreatedAt => "تاريخ إنشاء الحجز",
                AdminFinancialDateScopes.CommissionHeldAt => "تاريخ حجز العمولة",
                AdminFinancialDateScopes.CommissionSettledAt => "تاريخ تسوية العمولة",
                AdminFinancialDateScopes.TransferDate => "تاريخ التحويل",
                AdminFinancialDateScopes.ReviewedAt => "تاريخ المراجعة",
                AdminFinancialDateScopes.LastTopUpAt => "تاريخ آخر شحن",
                AdminFinancialDateScopes.UpdatedAt => "تاريخ آخر تحديث",
                _ => "تاريخ السجل"
            };

        private static List<AdminFinancialSelectOptionDto>
            BuildReportDateScopeOptions(
                string reportType,
                string selectedDateScope)
        {
            IEnumerable<string> scopes = reportType switch
            {
                AdminFinancialReportTypes.BookingCommissions =>
                    new[]
                    {
                        AdminFinancialDateScopes.BookingCreatedAt,
                        AdminFinancialDateScopes.CommissionHeldAt,
                        AdminFinancialDateScopes.CommissionSettledAt
                    },

                AdminFinancialReportTypes.TopUps or
                AdminFinancialReportTypes.Transactions =>
                    new[]
                    {
                        AdminFinancialDateScopes.CreatedAt,
                        AdminFinancialDateScopes.TransferDate,
                        AdminFinancialDateScopes.ReviewedAt
                    },

                _ => Array.Empty<string>()
            };

            return scopes
                .Select(scope => new AdminFinancialSelectOptionDto
                {
                    Value = scope,
                    Label = DateScopeLabel(scope),
                    IsSelected = IsState(scope, selectedDateScope)
                })
                .ToList();
        }

        private static string ReportTitle(string reportType) =>
            reportType switch
            {
                AdminFinancialReportTypes.BookingCommissions =>
                    "تقرير عمولات الحجوزات",
                AdminFinancialReportTypes.TopUps =>
                    "تقرير طلبات شحن رصيد الضمان",
                AdminFinancialReportTypes.CommissionAccounts =>
                    "تقرير حسابات ضمان العمولات",
                AdminFinancialReportTypes.Debts =>
                    "تقرير ديون العمولات",
                AdminFinancialReportTypes.Transactions =>
                    "تقرير الحركات المالية",
                AdminFinancialReportTypes.LowBalanceAccounts =>
                    "تقرير المنشآت منخفضة الرصيد",
                AdminFinancialReportTypes.SuspendedAccounts =>
                    "تقرير الحسابات الموقوفة ماليًا",
                AdminFinancialReportTypes.Comprehensive =>
                    "التقرير المالي الشامل للمنصة",
                _ => "التقرير المالي"
            };

        private static string ReportSubtitle(string reportType) =>
            reportType switch
            {
                AdminFinancialReportTypes.BookingCommissions =>
                    "يعرض عمولة المنصة المحسوبة والمحجوزة والمخصومة والمحررة لكل حجز.",
                AdminFinancialReportTypes.TopUps =>
                    "يعرض طلبات الشحن ونتيجة سداد الدين وإضافة الرصيد.",
                AdminFinancialReportTypes.CommissionAccounts =>
                    "يعرض الرصيد المتاح والمحجوز والدين والحالة لكل منشأة.",
                AdminFinancialReportTypes.Debts =>
                    "يعرض المنشآت التي لديها دين عمولات حالي.",
                AdminFinancialReportTypes.Transactions =>
                    "يعرض سجل التدقيق الكامل لحركات ضمان العمولات.",
                AdminFinancialReportTypes.LowBalanceAccounts =>
                    "يعرض الحسابات التي وصلت إلى حد التنبيه أو أقل.",
                AdminFinancialReportTypes.SuspendedAccounts =>
                    "يعرض الحسابات التي أوقفها النظام ماليًا.",
                AdminFinancialReportTypes.Comprehensive =>
                    "ملخص موحد للعمولات والشحن والأرصدة والديون والحركات.",
                _ => string.Empty
            };

        private static string BuildPeriodLabel(
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!fromDate.HasValue && !toDate.HasValue)
            {
                return "جميع الفترات";
            }

            var fromText = fromDate.HasValue
                ? fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "البداية";

            var toText = toDate.HasValue
                ? toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "اليوم";

            return $"من {fromText} إلى {toText}";
        }

        private static string BuildFilterDescription(
            AdminFinancialReportFilterDto filter)
        {
            var parts = new List<string>();

            if (filter.OwnerId.HasValue)
            {
                parts.Add($"المالك #{filter.OwnerId.Value}");
            }

            if (filter.PlaceId.HasValue)
            {
                parts.Add($"المنشأة #{filter.PlaceId.Value}");
            }

            if (filter.BookingId.HasValue)
            {
                parts.Add($"الحجز #{filter.BookingId.Value}");
            }

            if (filter.CommissionTransactionId.HasValue)
            {
                parts.Add($"الحركة #{filter.CommissionTransactionId.Value}");
            }

            if (!string.IsNullOrWhiteSpace(filter.CommissionStatus))
            {
                parts.Add($"حالة العمولة: {CommissionStatusLabel(filter.CommissionStatus)}");
            }

            if (!string.IsNullOrWhiteSpace(filter.AccountStatus))
            {
                parts.Add($"حالة الحساب: {AccountStatusLabel(filter.AccountStatus)}");
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionType))
            {
                parts.Add($"نوع الحركة: {TransactionTypeLabel(filter.TransactionType)}");
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionStatus))
            {
                parts.Add($"حالة الحركة: {TransactionStatusLabel(filter.TransactionStatus)}");
            }

            if (!string.IsNullOrWhiteSpace(filter.TopUpStatus))
            {
                parts.Add($"حالة الشحن: {TransactionStatusLabel(filter.TopUpStatus)}");
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
            {
                parts.Add($"المرجع: {filter.ReferenceNumber}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                parts.Add($"البحث: {filter.Search}");
            }

            return parts.Count == 0
                ? "بدون فلاتر إضافية"
                : string.Join("، ", parts);
        }

        private static AdminFinancialReportColumnDto Column(
            string key,
            string label,
            string dataType = AdminFinancialReportDataTypes.Text,
            double width = 18d,
            string alignment = AdminFinancialReportAlignments.Right)
        {
            return new AdminFinancialReportColumnDto
            {
                Key = key,
                Label = label,
                DataType = dataType,
                Width = width,
                Alignment = alignment
            };
        }

        private static AdminFinancialReportCellDto TextCell(
            string? value,
            string? tone = null) =>
                new()
                {
                    Text = value?.Trim() ?? string.Empty,
                    DataType = AdminFinancialReportDataTypes.Text,
                    Tone = tone
                };

        private static AdminFinancialReportCellDto MoneyCell(
            decimal value,
            string currency,
            string? tone = null) =>
                new()
                {
                    Text = $"{value:N2} {currency}",
                    DataType = AdminFinancialReportDataTypes.Money,
                    DecimalValue = value,
                    Tone = tone
                };

        private static AdminFinancialReportCellDto NumberCell(
            long value) =>
                new()
                {
                    Text = value.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    DataType = AdminFinancialReportDataTypes.Number,
                    IntegerValue = value
                };

        private static AdminFinancialReportCellDto PercentageCell(
            decimal value) =>
                new()
                {
                    Text = $"{value:N2}%",
                    DataType = AdminFinancialReportDataTypes.Percentage,
                    DecimalValue = value / 100m
                };

        private static AdminFinancialReportCellDto DateCell(
            DateTime? value,
            bool includeTime = false) =>
                new()
                {
                    Text = value.HasValue
                        ? value.Value.ToString(
                            includeTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                        : "—",
                    DataType = includeTime
                        ? AdminFinancialReportDataTypes.DateTime
                        : AdminFinancialReportDataTypes.Date,
                    DateValue = value
                };

        private static AdminFinancialReportCellDto StatusCell(
            string value,
            string label,
            string? tone = null) =>
                new()
                {
                    Text = label,
                    DataType = AdminFinancialReportDataTypes.Status,
                    Tone = tone,
                    Tooltip = value
                };

        private static AdminFinancialReportRowDto ReportRow(
            string rowKey,
            IEnumerable<AdminFinancialReportCellDto> cells,
            string? tone = null,
            string? documentType = null,
            int? documentSourceId = null)
        {
            var typedCells = cells.ToList();

            return new AdminFinancialReportRowDto
            {
                RowKey = rowKey,
                Cells = typedCells.Select(cell => cell.Text).ToList(),
                TypedCells = typedCells,
                Tone = tone,
                DocumentType = documentType,
                DocumentSourceId = documentSourceId
            };
        }

        private async Task<List<AdminBookingCommissionRowDto>>
            LoadBookingCommissionReportRowsAsync(
                AdminFinancialReportFilterDto reportFilter,
                bool includeAllRows,
                CancellationToken cancellationToken)
        {
            var rows = new List<AdminBookingCommissionRowDto>();
            var pageNumber = includeAllRows ? 1 : reportFilter.PageNumber;

            while (true)
            {
                var page = await GetBookingCommissionsAsync(
                    new AdminBookingCommissionFilterDto
                    {
                        BookingId = reportFilter.BookingId,
                        OwnerId = reportFilter.OwnerId,
                        PlaceId = reportFilter.PlaceId,
                        CommissionStatus = reportFilter.CommissionStatus,
                        HasDebt = reportFilter.HasDebt,
                        DateScope = reportFilter.DateScope,
                        FromDate = reportFilter.FromDate,
                        ToDate = reportFilter.ToDate,
                        Search = reportFilter.Search,
                        PageNumber = pageNumber,
                        PageSize = reportFilter.PageSize
                    },
                    cancellationToken);

                rows.AddRange(page.Result.Items);

                if (!includeAllRows ||
                    !page.Result.Pagination.HasNextPage)
                {
                    break;
                }

                pageNumber += 1;
            }

            return rows;
        }

        private async Task<List<AdminCommissionTopUpRowDto>>
            LoadTopUpReportRowsAsync(
                AdminFinancialReportFilterDto reportFilter,
                bool includeAllRows,
                CancellationToken cancellationToken)
        {
            var rows = new List<AdminCommissionTopUpRowDto>();
            var pageNumber = includeAllRows ? 1 : reportFilter.PageNumber;

            while (true)
            {
                var page = await GetTopUpsAsync(
                    new AdminCommissionTopUpFilterDto
                    {
                        RequestId = reportFilter.CommissionTransactionId,
                        OwnerId = reportFilter.OwnerId,
                        PlaceId = reportFilter.PlaceId,
                        PlatformWalletId = reportFilter.PlatformWalletId,
                        Status = reportFilter.TopUpStatus,
                        ReferenceNumber = reportFilter.ReferenceNumber,
                        DateScope = reportFilter.DateScope,
                        FromDate = reportFilter.FromDate,
                        ToDate = reportFilter.ToDate,
                        Search = reportFilter.Search,
                        PageNumber = pageNumber,
                        PageSize = reportFilter.PageSize
                    },
                    cancellationToken);

                rows.AddRange(page.Result.Items);

                if (!includeAllRows ||
                    !page.Result.Pagination.HasNextPage)
                {
                    break;
                }

                pageNumber += 1;
            }

            return rows;
        }

        private async Task<List<AdminCommissionAccountRowDto>>
            LoadAccountReportRowsAsync(
                AdminFinancialReportFilterDto reportFilter,
                bool includeAllRows,
                string mode,
                CancellationToken cancellationToken)
        {
            var rows = new List<AdminCommissionAccountRowDto>();
            var pageNumber = includeAllRows ? 1 : reportFilter.PageNumber;

            while (true)
            {
                var page = await GetCommissionAccountsAsync(
                    new AdminCommissionAccountFilterDto
                    {
                        OwnerId = reportFilter.OwnerId,
                        PlaceId = reportFilter.PlaceId,
                        AccountStatus = mode == AdminFinancialReportTypes.SuspendedAccounts
                            ? AdminCommissionAccountStatuses.Suspended
                            : reportFilter.AccountStatus,
                        HasDebt = mode == AdminFinancialReportTypes.Debts
                            ? true
                            : reportFilter.HasDebt,
                        IsLowBalance = mode == AdminFinancialReportTypes.LowBalanceAccounts
                            ? true
                            : reportFilter.IsLowBalance,
                        IsBookingSuspended = mode == AdminFinancialReportTypes.SuspendedAccounts
                            ? true
                            : reportFilter.IsSuspended,
                        Search = reportFilter.Search,
                        PageNumber = pageNumber,
                        PageSize = reportFilter.PageSize
                    },
                    cancellationToken);

                rows.AddRange(page.Result.Items);

                if (!includeAllRows ||
                    !page.Result.Pagination.HasNextPage)
                {
                    break;
                }

                pageNumber += 1;
            }

            return rows;
        }

        private async Task<List<AdminFinancialTransactionRowDto>>
            LoadTransactionReportRowsAsync(
                AdminFinancialReportFilterDto reportFilter,
                bool includeAllRows,
                CancellationToken cancellationToken)
        {
            var rows = new List<AdminFinancialTransactionRowDto>();
            var pageNumber = includeAllRows ? 1 : reportFilter.PageNumber;

            while (true)
            {
                var page = await GetTransactionsAsync(
                    new AdminFinancialTransactionFilterDto
                    {
                        TransactionId = reportFilter.CommissionTransactionId,
                        OwnerId = reportFilter.OwnerId,
                        PlaceId = reportFilter.PlaceId,
                        BookingId = reportFilter.BookingId,
                        PlatformWalletId = reportFilter.PlatformWalletId,
                        TransactionType = reportFilter.TransactionType,
                        Status = reportFilter.TransactionStatus,
                        ReferenceNumber = reportFilter.ReferenceNumber,
                        DateScope = reportFilter.DateScope,
                        FromDate = reportFilter.FromDate,
                        ToDate = reportFilter.ToDate,
                        Search = reportFilter.Search,
                        PageNumber = pageNumber,
                        PageSize = reportFilter.PageSize
                    },
                    cancellationToken);

                rows.AddRange(page.Result.Items);

                if (!includeAllRows ||
                    !page.Result.Pagination.HasNextPage)
                {
                    break;
                }

                pageNumber += 1;
            }

            return rows;
        }

        public async Task<AdminFinancialReportPageDto> GetFinancialReportAsync(
            AdminFinancialReportFilterDto filter,
            bool includeAllRows = false,
            string? generatedByName = null,
            CancellationToken cancellationToken = default)
        {
            filter = NormalizeReportFilter(filter, includeAllRows);

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var model = new AdminFinancialReportPageDto
            {
                PlatformName = FirstNonEmpty(
                    "دليلك اليمني",
                    settings?.SiteName),
                PlatformLogoPath = settings?.LogoPath?.Trim(),
                Currency = GetCurrency(settings),
                ReportType = filter.ReportType,
                Title = ReportTitle(filter.ReportType),
                Subtitle = ReportSubtitle(filter.ReportType),
                DateScopeLabel = DateScopeLabel(filter.DateScope),
                PeriodLabel = BuildPeriodLabel(
                    filter.FromDate,
                    filter.ToDate),
                FilterDescription = BuildFilterDescription(filter),
                GeneratedAt = DateTime.Now,
                GeneratedByName = generatedByName,
                Filter = filter,
                Capabilities = BuildReportCapabilities(filter.ReportType),
                ReportTypeOptions = AdminFinancialReportTypes.All
                    .Select(type => new AdminFinancialReportOptionDto
                    {
                        Value = type,
                        Label = ReportTitle(type),
                        Description = ReportSubtitle(type),
                        IsSelected = IsState(type, filter.ReportType)
                    })
                    .ToList(),
                Owners = await BuildOwnerOptionsAsync(
                    filter.OwnerId,
                    cancellationToken),
                Places = await BuildPlaceOptionsAsync(
                    filter.OwnerId,
                    filter.PlaceId,
                    cancellationToken),
                PlatformWallets = await BuildPlatformWalletOptionsAsync(
                    filter.PlatformWalletId,
                    cancellationToken),
                CommissionStatusOptions = BuildOptions(
                    AdminBookingCommissionStatuses.All,
                    CommissionStatusLabel,
                    filter.CommissionStatus),
                AccountStatusOptions = BuildOptions(
                    AdminCommissionAccountStatuses.All,
                    AccountStatusLabel,
                    filter.AccountStatus),
                TransactionTypeOptions = BuildOptions(
                    AdminFinanceTransactionTypes.All,
                    TransactionTypeLabel,
                    filter.TransactionType),
                TransactionStatusOptions = BuildOptions(
                    AdminFinanceTransactionStatuses.All,
                    TransactionStatusLabel,
                    filter.TransactionStatus),
                TopUpStatusOptions = BuildOptions(
                    AdminCommissionTopUpStatuses.All,
                    TransactionStatusLabel,
                    filter.TopUpStatus),
                DateScopeOptions = BuildReportDateScopeOptions(
                    filter.ReportType,
                    filter.DateScope),
                HasActiveFilters =
                    filter.OwnerId.HasValue ||
                    filter.PlaceId.HasValue ||
                    filter.BookingId.HasValue ||
                    filter.CommissionTransactionId.HasValue ||
                    filter.PlatformWalletId.HasValue ||
                    filter.FromDate.HasValue ||
                    filter.ToDate.HasValue ||
                    !string.IsNullOrWhiteSpace(filter.CommissionStatus) ||
                    !string.IsNullOrWhiteSpace(filter.AccountStatus) ||
                    !string.IsNullOrWhiteSpace(filter.TransactionType) ||
                    !string.IsNullOrWhiteSpace(filter.TransactionStatus) ||
                    !string.IsNullOrWhiteSpace(filter.TopUpStatus) ||
                    !string.IsNullOrWhiteSpace(filter.ReferenceNumber) ||
                    filter.HasDebt.HasValue ||
                    filter.IsLowBalance.HasValue ||
                    filter.IsSuspended.HasValue ||
                    !string.IsNullOrWhiteSpace(filter.Search)
            };

            switch (filter.ReportType)
            {
                case AdminFinancialReportTypes.BookingCommissions:
                    await PopulateBookingCommissionReportAsync(
                        model,
                        includeAllRows,
                        cancellationToken);
                    break;

                case AdminFinancialReportTypes.TopUps:
                    await PopulateTopUpReportAsync(
                        model,
                        includeAllRows,
                        cancellationToken);
                    break;

                case AdminFinancialReportTypes.CommissionAccounts:
                case AdminFinancialReportTypes.Debts:
                case AdminFinancialReportTypes.LowBalanceAccounts:
                case AdminFinancialReportTypes.SuspendedAccounts:
                    await PopulateAccountReportAsync(
                        model,
                        includeAllRows,
                        filter.ReportType,
                        cancellationToken);
                    break;

                case AdminFinancialReportTypes.Transactions:
                    await PopulateTransactionReportAsync(
                        model,
                        includeAllRows,
                        cancellationToken);
                    break;

                case AdminFinancialReportTypes.Comprehensive:
                    await PopulateComprehensiveReportAsync(
                        model,
                        includeAllRows,
                        cancellationToken);
                    break;
            }

            return model;
        }

        private static string StatusTone(string? status)
        {
            if (
                IsState(status, AdminCommissionTopUpStatuses.Completed) ||
                IsState(status, AdminBookingCommissionStatuses.Charged) ||
                IsState(status, AdminCommissionAccountStatuses.Active)
            )
            {
                return "Success";
            }

            if (
                IsState(status, AdminCommissionTopUpStatuses.Pending) ||
                IsState(status, AdminBookingCommissionStatuses.Held) ||
                IsState(status, AdminBookingCommissionStatuses.Calculated) ||
                IsState(status, AdminCommissionAccountStatuses.LowBalance) ||
                IsState(status, AdminCommissionAccountStatuses.NeedsTopUp)
            )
            {
                return "Warning";
            }

            if (
                IsState(status, AdminCommissionTopUpStatuses.Rejected) ||
                IsState(status, AdminCommissionTopUpStatuses.Failed) ||
                IsState(status, AdminBookingCommissionStatuses.Debt) ||
                IsState(status, AdminCommissionAccountStatuses.InDebt) ||
                IsState(status, AdminCommissionAccountStatuses.Suspended)
            )
            {
                return "Danger";
            }

            if (
                IsState(status, AdminBookingCommissionStatuses.Released) ||
                IsState(status, AdminCommissionTopUpStatuses.Cancelled)
            )
            {
                return "Muted";
            }

            return "Default";
        }

        private async Task PopulateBookingCommissionReportAsync(
            AdminFinancialReportPageDto model,
            bool includeAllRows,
            CancellationToken cancellationToken)
        {
            var filter = model.Filter;

            var page = await GetBookingCommissionsAsync(
                new AdminBookingCommissionFilterDto
                {
                    BookingId = filter.BookingId,
                    OwnerId = filter.OwnerId,
                    PlaceId = filter.PlaceId,
                    CommissionStatus = filter.CommissionStatus,
                    HasDebt = filter.HasDebt,
                    DateScope = filter.DateScope,
                    FromDate = filter.FromDate,
                    ToDate = filter.ToDate,
                    Search = filter.Search,
                    PageNumber = includeAllRows ? 1 : filter.PageNumber,
                    PageSize = filter.PageSize
                },
                cancellationToken);

            var rows = includeAllRows
                ? await LoadBookingCommissionReportRowsAsync(
                    filter,
                    true,
                    cancellationToken)
                : page.Result.Items;

            model.TotalAvailableRows = page.Result.Pagination.TotalCount;
            model.Pagination = page.Result.Pagination;

            model.Metrics.AddRange(new[]
            {
                new AdminFinancialReportMetricDto
                {
                    Key = "TotalCount",
                    Label = "عدد الحجوزات",
                    Value = page.Summary.TotalCount.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    NumericValue = page.Summary.TotalCount,
                    IconClass = "bi bi-calendar-check"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Calculated",
                    Label = "إجمالي العمولات",
                    Value = $"{page.Summary.TotalCommissionCalculated:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalCommissionCalculated,
                    IconClass = "bi bi-percent"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Held",
                    Label = "العمولات المحجوزة",
                    Value = $"{page.Summary.TotalCommissionHeld:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalCommissionHeld,
                    Tone = "Warning"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Charged",
                    Label = "العمولات المخصومة",
                    Value = $"{page.Summary.TotalCommissionCharged:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalCommissionCharged,
                    Tone = "Success"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Released",
                    Label = "العمولات المحررة",
                    Value = $"{page.Summary.TotalCommissionReleased:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalCommissionReleased,
                    Tone = "Muted"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Debt",
                    Label = "عمولات بحالة دين",
                    Value = $"{page.Summary.TotalCommissionDebt:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalCommissionDebt,
                    Tone = "Danger"
                }
            });

            var section = new AdminFinancialReportSectionDto
            {
                Key = "BookingCommissions",
                Title = "عمولات الحجوزات",
                Description = "صف واحد لكل سجل مالي مرتبط بحجز.",
                IsPrimary = true,
                EmptyMessage = "لا توجد عمولات مطابقة للفلاتر.",
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("BookingId", "الحجز", AdminFinancialReportDataTypes.Number, 11d, AdminFinancialReportAlignments.Center),
                    Column("Owner", "المالك", width: 20d),
                    Column("Place", "المنشأة", width: 24d),
                    Column("BookingValue", "قيمة الحجز", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Rate", "النسبة", AdminFinancialReportDataTypes.Percentage, 12d, AdminFinancialReportAlignments.Center),
                    Column("Commission", "العمولة", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Held", "المحجوز", AdminFinancialReportDataTypes.Money, 16d, AdminFinancialReportAlignments.Left),
                    Column("Charged", "المخصوم", AdminFinancialReportDataTypes.Money, 16d, AdminFinancialReportAlignments.Left),
                    Column("Released", "المحرر", AdminFinancialReportDataTypes.Money, 16d, AdminFinancialReportAlignments.Left),
                    Column("Debt", "الدين", AdminFinancialReportDataTypes.Money, 16d, AdminFinancialReportAlignments.Left),
                    Column("Status", "حالة العمولة", AdminFinancialReportDataTypes.Status, 16d, AdminFinancialReportAlignments.Center),
                    Column("Date", "تاريخ الحجز", AdminFinancialReportDataTypes.DateTime, 18d, AdminFinancialReportAlignments.Center)
                }
            };

            foreach (var row in rows)
            {
                section.Rows.Add(ReportRow(
                    $"booking-{row.BookingId}",
                    new[]
                    {
                        NumberCell(row.BookingId),
                        TextCell(row.OwnerName),
                        TextCell(row.PlaceName),
                        MoneyCell(row.BookingTotalPrice, model.Currency),
                        PercentageCell(row.CommissionRateSnapshot),
                        MoneyCell(row.PlatformCommission, model.Currency),
                        MoneyCell(row.HeldAmount, model.Currency),
                        MoneyCell(row.ChargedAmount, model.Currency, "Success"),
                        MoneyCell(row.ReleasedAmount, model.Currency, "Muted"),
                        MoneyCell(row.DebtAmount, model.Currency, row.DebtAmount > 0m ? "Danger" : null),
                        StatusCell(
                            row.CommissionStatus,
                            CommissionStatusLabel(row.CommissionStatus),
                            StatusTone(row.CommissionStatus)),
                        DateCell(row.BookingCreatedAt, true)
                    },
                    row.NeedsReview ? "Warning" : null,
                    AdminFinancialDocumentTypes.BookingCommissionSettlement,
                    row.BookingId));

                model.Documents.Add(new AdminFinancialDocumentListItemDto
                {
                    DocumentType = AdminFinancialDocumentTypes.BookingCommissionSettlement,
                    SourceId = row.BookingId,
                    DocumentNumber = $"COM-{row.BookingId:D8}",
                    Title = "كشف تسوية عمولة الحجز",
                    Subtitle = row.PlaceName,
                    OwnerName = row.OwnerName,
                    PlaceName = row.PlaceName,
                    Amount = row.PlatformCommission,
                    Status = row.CommissionStatus,
                    DocumentDate = row.SettledAt ??
                        row.CommissionChargedAt ??
                        row.CommissionReleasedAt ??
                        row.CommissionHeldAt ??
                        row.BookingCreatedAt ??
                        row.UpdatedAt
                });
            }

            model.Sections.Add(section);
            model.StatusBreakdown = rows
                .GroupBy(row => row.CommissionStatus)
                .Select(group => new AdminFinancialReportBreakdownItemDto
                {
                    Key = group.Key,
                    Label = CommissionStatusLabel(group.Key),
                    Count = group.Count(),
                    Amount = group.Sum(item => item.PlatformCommission),
                    Percentage = rows.Count == 0
                        ? 0m
                        : Math.Round(group.Count() * 100m / rows.Count, 2)
                })
                .ToList();

            model.PlaceBreakdown = rows
                .GroupBy(row => new { row.PlaceId, row.PlaceName })
                .OrderByDescending(group => group.Sum(item => item.PlatformCommission))
                .Take(20)
                .Select(group => new AdminFinancialReportBreakdownItemDto
                {
                    Key = group.Key.PlaceId.ToString(CultureInfo.InvariantCulture),
                    Label = group.Key.PlaceName,
                    Count = group.Count(),
                    Amount = group.Sum(item => item.PlatformCommission),
                    Percentage = page.Summary.TotalCommissionCalculated <= 0m
                        ? 0m
                        : Math.Round(
                            group.Sum(item => item.PlatformCommission) * 100m /
                            page.Summary.TotalCommissionCalculated,
                            2)
                })
                .ToList();
        }

        private async Task PopulateTopUpReportAsync(
            AdminFinancialReportPageDto model,
            bool includeAllRows,
            CancellationToken cancellationToken)
        {
            var filter = model.Filter;

            var page = await GetTopUpsAsync(
                new AdminCommissionTopUpFilterDto
                {
                    RequestId = filter.CommissionTransactionId,
                    OwnerId = filter.OwnerId,
                    PlaceId = filter.PlaceId,
                    PlatformWalletId = filter.PlatformWalletId,
                    Status = filter.TopUpStatus,
                    ReferenceNumber = filter.ReferenceNumber,
                    DateScope = filter.DateScope,
                    FromDate = filter.FromDate,
                    ToDate = filter.ToDate,
                    Search = filter.Search,
                    PageNumber = includeAllRows ? 1 : filter.PageNumber,
                    PageSize = filter.PageSize
                },
                cancellationToken);

            var rows = includeAllRows
                ? await LoadTopUpReportRowsAsync(filter, true, cancellationToken)
                : page.Result.Items;

            model.TotalAvailableRows = page.Result.Pagination.TotalCount;
            model.Pagination = page.Result.Pagination;

            model.Metrics.AddRange(new[]
            {
                new AdminFinancialReportMetricDto
                {
                    Key = "TotalCount",
                    Label = "عدد الطلبات",
                    Value = page.Summary.TotalCount.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    NumericValue = page.Summary.TotalCount
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Pending",
                    Label = "طلبات معلقة",
                    Value = page.Summary.PendingCount.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    NumericValue = page.Summary.PendingCount,
                    Tone = "Warning"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "ApprovedAmount",
                    Label = "إجمالي الشحن المعتمد",
                    Value = $"{page.Summary.CompletedAmount:N2} {model.Currency}",
                    NumericValue = page.Summary.CompletedAmount,
                    Tone = "Success"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "DebtPaid",
                    Label = "الديون المسددة",
                    Value = $"{page.Summary.DebtPaidAmount:N2} {model.Currency}",
                    NumericValue = page.Summary.DebtPaidAmount
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Credited",
                    Label = "المضاف للرصيد",
                    Value = $"{page.Summary.AddedToAvailableBalanceAmount:N2} {model.Currency}",
                    NumericValue = page.Summary.AddedToAvailableBalanceAmount
                }
            });

            var section = new AdminFinancialReportSectionDto
            {
                Key = "TopUps",
                Title = "طلبات شحن رصيد الضمان",
                Description = "يوضح المبلغ المحول وما سدد الدين وما أضيف للرصيد.",
                IsPrimary = true,
                EmptyMessage = "لا توجد طلبات شحن مطابقة للفلاتر.",
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("RequestId", "الطلب", AdminFinancialReportDataTypes.Number, 11d, AdminFinancialReportAlignments.Center),
                    Column("Owner", "المالك", width: 20d),
                    Column("Place", "المنشأة", width: 24d),
                    Column("Wallet", "حساب المنصة", width: 22d),
                    Column("Amount", "مبلغ الشحن", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("DebtPayment", "سداد الدين", AdminFinancialReportDataTypes.Money, 16d, AdminFinancialReportAlignments.Left),
                    Column("Credit", "المضاف للرصيد", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Reference", "المرجع", width: 18d),
                    Column("Status", "الحالة", AdminFinancialReportDataTypes.Status, 14d, AdminFinancialReportAlignments.Center),
                    Column("Date", "تاريخ الطلب", AdminFinancialReportDataTypes.DateTime, 18d, AdminFinancialReportAlignments.Center)
                }
            };

            foreach (var row in rows)
            {
                section.Rows.Add(ReportRow(
                    $"topup-{row.CommissionTransactionId}",
                    new[]
                    {
                        NumberCell(row.CommissionTransactionId),
                        TextCell(row.OwnerName),
                        TextCell(row.PlaceName),
                        TextCell(row.PlatformWalletName),
                        MoneyCell(row.Amount, model.Currency),
                        MoneyCell(row.DebtPaymentAmount, model.Currency),
                        MoneyCell(row.CreditedToAvailableBalance, model.Currency),
                        TextCell(row.ReferenceNumber),
                        StatusCell(
                            row.Status,
                            row.StatusLabel,
                            StatusTone(row.Status)),
                        DateCell(row.CreatedAt, true)
                    },
                    row.HasDuplicateReference ? "Warning" : null,
                    IsState(row.Status, AdminCommissionTopUpStatuses.Completed)
                        ? AdminFinancialDocumentTypes.TopUpReceipt
                        : null,
                    IsState(row.Status, AdminCommissionTopUpStatuses.Completed)
                        ? row.CommissionTransactionId
                        : null));

                if (IsState(row.Status, AdminCommissionTopUpStatuses.Completed))
                {
                    model.Documents.Add(new AdminFinancialDocumentListItemDto
                    {
                        DocumentType = AdminFinancialDocumentTypes.TopUpReceipt,
                        SourceId = row.CommissionTransactionId,
                        DocumentNumber = $"TOP-{row.CommissionTransactionId:D8}",
                        Title = "إيصال اعتماد شحن رصيد الضمان",
                        Subtitle = row.PlaceName,
                        OwnerName = row.OwnerName,
                        PlaceName = row.PlaceName,
                        Amount = row.Amount,
                        Status = row.Status,
                        DocumentDate = row.ReviewedAt ?? row.CreatedAt
                    });
                }
            }

            model.Sections.Add(section);
            model.StatusBreakdown = rows
                .GroupBy(row => row.Status)
                .Select(group => new AdminFinancialReportBreakdownItemDto
                {
                    Key = group.Key,
                    Label = TransactionStatusLabel(group.Key),
                    Count = group.Count(),
                    Amount = group.Sum(item => item.Amount),
                    Percentage = rows.Count == 0
                        ? 0m
                        : Math.Round(group.Count() * 100m / rows.Count, 2)
                })
                .ToList();

            model.PlaceBreakdown = rows
                .GroupBy(row => new { row.PlaceId, row.PlaceName })
                .OrderByDescending(group => group.Sum(item => item.Amount))
                .Take(20)
                .Select(group => new AdminFinancialReportBreakdownItemDto
                {
                    Key = group.Key.PlaceId.ToString(CultureInfo.InvariantCulture),
                    Label = group.Key.PlaceName,
                    Count = group.Count(),
                    Amount = group.Sum(item => item.Amount),
                    Percentage = page.Summary.CompletedAmount <= 0m
                        ? 0m
                        : Math.Round(
                            group.Where(item => IsState(item.Status, AdminCommissionTopUpStatuses.Completed))
                                .Sum(item => item.Amount) * 100m /
                            page.Summary.CompletedAmount,
                            2)
                })
                .ToList();
        }

        private async Task PopulateAccountReportAsync(
            AdminFinancialReportPageDto model,
            bool includeAllRows,
            string mode,
            CancellationToken cancellationToken)
        {
            var filter = model.Filter;

            var page = await GetCommissionAccountsAsync(
                new AdminCommissionAccountFilterDto
                {
                    OwnerId = filter.OwnerId,
                    PlaceId = filter.PlaceId,
                    AccountStatus = mode == AdminFinancialReportTypes.SuspendedAccounts
                        ? AdminCommissionAccountStatuses.Suspended
                        : filter.AccountStatus,
                    HasDebt = mode == AdminFinancialReportTypes.Debts
                        ? true
                        : filter.HasDebt,
                    IsLowBalance = mode == AdminFinancialReportTypes.LowBalanceAccounts
                        ? true
                        : filter.IsLowBalance,
                    IsBookingSuspended = mode == AdminFinancialReportTypes.SuspendedAccounts
                        ? true
                        : filter.IsSuspended,
                    Search = filter.Search,
                    PageNumber = includeAllRows ? 1 : filter.PageNumber,
                    PageSize = filter.PageSize
                },
                cancellationToken);

            var rows = includeAllRows
                ? await LoadAccountReportRowsAsync(
                    filter,
                    true,
                    mode,
                    cancellationToken)
                : page.Result.Items;

            if (mode == AdminFinancialReportTypes.LowBalanceAccounts)
            {
                rows = rows
                    .Where(row =>
                        row.AvailableBalance <= row.EffectiveLowBalanceThreshold ||
                        IsState(row.AccountStatus, AdminCommissionAccountStatuses.LowBalance) ||
                        IsState(row.AccountStatus, AdminCommissionAccountStatuses.NeedsTopUp))
                    .ToList();
            }

            model.TotalAvailableRows = page.Result.Pagination.TotalCount;
            model.Pagination = page.Result.Pagination;

            model.Metrics.AddRange(new[]
            {
                new AdminFinancialReportMetricDto
                {
                    Key = "Accounts",
                    Label = "عدد الحسابات",
                    Value = page.Summary.TotalCount.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    NumericValue = page.Summary.TotalCount
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Available",
                    Label = "الرصيد المتاح",
                    Value = $"{page.Summary.TotalAvailableBalance:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalAvailableBalance,
                    Tone = "Success"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Held",
                    Label = "الرصيد المحجوز",
                    Value = $"{page.Summary.TotalHeldBalance:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalHeldBalance,
                    Tone = "Warning"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Debt",
                    Label = "الدين الحالي",
                    Value = $"{page.Summary.TotalDebtAmount:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalDebtAmount,
                    Tone = "Danger"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Suspended",
                    Label = "الحسابات الموقوفة",
                    Value = page.Summary.SuspendedCount.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    NumericValue = page.Summary.SuspendedCount,
                    Tone = "Danger"
                }
            });

            var sectionTitle = mode switch
            {
                AdminFinancialReportTypes.Debts => "حسابات العمولات المديونة",
                AdminFinancialReportTypes.LowBalanceAccounts => "المنشآت منخفضة الرصيد",
                AdminFinancialReportTypes.SuspendedAccounts => "الحسابات الموقوفة ماليًا",
                _ => "حسابات ضمان العمولات"
            };

            var section = new AdminFinancialReportSectionDto
            {
                Key = mode,
                Title = sectionTitle,
                Description = "الرصيد والدين والسياسة الفعالة لكل منشأة.",
                IsPrimary = true,
                EmptyMessage = "لا توجد حسابات مطابقة للفلاتر.",
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("Owner", "المالك", width: 20d),
                    Column("Place", "المنشأة", width: 25d),
                    Column("Available", "المتاح", AdminFinancialReportDataTypes.Money, 17d, AdminFinancialReportAlignments.Left),
                    Column("Held", "المحجوز", AdminFinancialReportDataTypes.Money, 17d, AdminFinancialReportAlignments.Left),
                    Column("Debt", "الدين", AdminFinancialReportDataTypes.Money, 17d, AdminFinancialReportAlignments.Left),
                    Column("LowThreshold", "حد التنبيه", AdminFinancialReportDataTypes.Money, 17d, AdminFinancialReportAlignments.Left),
                    Column("DebtLimit", "حد الدين", AdminFinancialReportDataTypes.Money, 17d, AdminFinancialReportAlignments.Left),
                    Column("AllowNegative", "السماح بالدين", AdminFinancialReportDataTypes.Boolean, 14d, AdminFinancialReportAlignments.Center),
                    Column("Status", "الحالة", AdminFinancialReportDataTypes.Status, 15d, AdminFinancialReportAlignments.Center),
                    Column("LastTopUp", "آخر شحن", AdminFinancialReportDataTypes.DateTime, 18d, AdminFinancialReportAlignments.Center)
                }
            };

            foreach (var row in rows)
            {
                section.Rows.Add(ReportRow(
                    $"account-{row.PlaceCommissionAccountId}",
                    new[]
                    {
                        TextCell(row.OwnerName),
                        TextCell(row.PlaceName),
                        MoneyCell(row.AvailableBalance, model.Currency),
                        MoneyCell(row.HeldBalance, model.Currency),
                        MoneyCell(row.DebtAmount, model.Currency, row.DebtAmount > 0m ? "Danger" : null),
                        MoneyCell(row.EffectiveLowBalanceThreshold, model.Currency),
                        MoneyCell(row.EffectiveDebtLimit, model.Currency),
                        TextCell(row.EffectiveAllowNegativeBalance ? "نعم" : "لا"),
                        StatusCell(
                            row.AccountStatus,
                            AccountStatusLabel(row.AccountStatus),
                            StatusTone(row.AccountStatus)),
                        DateCell(row.LastTopUpAt, true)
                    },
                    IsState(row.AccountStatus, AdminCommissionAccountStatuses.Suspended)
                        ? "Danger"
                        : null,
                    AdminFinancialDocumentTypes.CommissionAccountStatement,
                    row.PlaceCommissionAccountId));

                model.Documents.Add(new AdminFinancialDocumentListItemDto
                {
                    DocumentType = AdminFinancialDocumentTypes.CommissionAccountStatement,
                    SourceId = row.PlaceCommissionAccountId,
                    DocumentNumber = $"ACC-{row.PlaceCommissionAccountId:D8}",
                    Title = "كشف حساب ضمان العمولات",
                    Subtitle = row.PlaceName,
                    OwnerName = row.OwnerName,
                    PlaceName = row.PlaceName,
                    Amount = row.AvailableBalance - row.DebtAmount,
                    Status = row.AccountStatus,
                    DocumentDate = row.UpdatedAt ?? row.CreatedAt
                });
            }

            model.Sections.Add(section);
            model.StatusBreakdown = rows
                .GroupBy(row => row.AccountStatus)
                .Select(group => new AdminFinancialReportBreakdownItemDto
                {
                    Key = group.Key,
                    Label = AccountStatusLabel(group.Key),
                    Count = group.Count(),
                    Amount = group.Sum(item => item.AvailableBalance - item.DebtAmount),
                    Percentage = rows.Count == 0
                        ? 0m
                        : Math.Round(group.Count() * 100m / rows.Count, 2)
                })
                .ToList();
        }

        private async Task PopulateTransactionReportAsync(
            AdminFinancialReportPageDto model,
            bool includeAllRows,
            CancellationToken cancellationToken)
        {
            var filter = model.Filter;

            var page = await GetTransactionsAsync(
                new AdminFinancialTransactionFilterDto
                {
                    TransactionId = filter.CommissionTransactionId,
                    OwnerId = filter.OwnerId,
                    PlaceId = filter.PlaceId,
                    BookingId = filter.BookingId,
                    PlatformWalletId = filter.PlatformWalletId,
                    TransactionType = filter.TransactionType,
                    Status = filter.TransactionStatus,
                    ReferenceNumber = filter.ReferenceNumber,
                    DateScope = filter.DateScope,
                    FromDate = filter.FromDate,
                    ToDate = filter.ToDate,
                    Search = filter.Search,
                    PageNumber = includeAllRows ? 1 : filter.PageNumber,
                    PageSize = filter.PageSize
                },
                cancellationToken);

            var rows = includeAllRows
                ? await LoadTransactionReportRowsAsync(filter, true, cancellationToken)
                : page.Result.Items;

            model.TotalAvailableRows = page.Result.Pagination.TotalCount;
            model.Pagination = page.Result.Pagination;

            model.Metrics.AddRange(new[]
            {
                new AdminFinancialReportMetricDto
                {
                    Key = "Transactions",
                    Label = "عدد الحركات",
                    Value = page.Summary.TotalCount.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    NumericValue = page.Summary.TotalCount
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "TopUps",
                    Label = "إجمالي الشحن",
                    Value = $"{page.Summary.TotalTopUps:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalTopUps
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Charged",
                    Label = "العمولات المخصومة",
                    Value = $"{page.Summary.TotalCommissionCharged:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalCommissionCharged,
                    Tone = "Success"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Debt",
                    Label = "الدين المسجل",
                    Value = $"{page.Summary.TotalDebtRegistered:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalDebtRegistered,
                    Tone = "Danger"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "DebtPaid",
                    Label = "الدين المسدد",
                    Value = $"{page.Summary.TotalDebtPaid:N2} {model.Currency}",
                    NumericValue = page.Summary.TotalDebtPaid
                }
            });

            var section = new AdminFinancialReportSectionDto
            {
                Key = "Transactions",
                Title = "الحركات المالية",
                Description = "سجل غير قابل للحذف يوضح الأرصدة قبل وبعد كل حركة.",
                IsPrimary = true,
                EmptyMessage = "لا توجد حركات مالية مطابقة للفلاتر.",
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("TransactionId", "الحركة", AdminFinancialReportDataTypes.Number, 11d, AdminFinancialReportAlignments.Center),
                    Column("Owner", "المالك", width: 19d),
                    Column("Place", "المنشأة", width: 23d),
                    Column("BookingId", "الحجز", AdminFinancialReportDataTypes.Number, 11d, AdminFinancialReportAlignments.Center),
                    Column("Type", "النوع", AdminFinancialReportDataTypes.Status, 16d, AdminFinancialReportAlignments.Center),
                    Column("Amount", "المبلغ", AdminFinancialReportDataTypes.Money, 17d, AdminFinancialReportAlignments.Left),
                    Column("Available", "المتاح قبل/بعد", width: 23d),
                    Column("Held", "المحجوز قبل/بعد", width: 23d),
                    Column("Debt", "الدين قبل/بعد", width: 23d),
                    Column("Status", "الحالة", AdminFinancialReportDataTypes.Status, 14d, AdminFinancialReportAlignments.Center),
                    Column("Reference", "المرجع", width: 17d),
                    Column("Date", "التاريخ", AdminFinancialReportDataTypes.DateTime, 18d, AdminFinancialReportAlignments.Center)
                }
            };

            foreach (var row in rows)
            {
                section.Rows.Add(ReportRow(
                    $"transaction-{row.CommissionTransactionId}",
                    new[]
                    {
                        NumberCell(row.CommissionTransactionId),
                        TextCell(row.OwnerName),
                        TextCell(row.PlaceName),
                        row.BookingId.HasValue ? NumberCell(row.BookingId.Value) : TextCell("—"),
                        StatusCell(row.TransactionType, row.TransactionTypeLabel),
                        MoneyCell(row.Amount, model.Currency),
                        TextCell($"{row.AvailableBalanceBefore:N2} ← {row.AvailableBalanceAfter:N2}"),
                        TextCell($"{row.HeldBalanceBefore:N2} ← {row.HeldBalanceAfter:N2}"),
                        TextCell($"{row.DebtBefore:N2} ← {row.DebtAfter:N2}"),
                        StatusCell(row.Status, row.StatusLabel, StatusTone(row.Status)),
                        TextCell(row.ReferenceNumber),
                        DateCell(row.CreatedAt, true)
                    },
                    IsState(row.Status, AdminFinanceTransactionStatuses.Failed)
                        ? "Danger"
                        : null,
                    AdminFinancialDocumentTypes.TransactionDocument,
                    row.CommissionTransactionId));

                model.Documents.Add(new AdminFinancialDocumentListItemDto
                {
                    DocumentType = AdminFinancialDocumentTypes.TransactionDocument,
                    SourceId = row.CommissionTransactionId,
                    DocumentNumber = $"TRX-{row.CommissionTransactionId:D8}",
                    Title = "مستند حركة مالية",
                    Subtitle = row.TransactionTypeLabel,
                    OwnerName = row.OwnerName,
                    PlaceName = row.PlaceName,
                    Amount = row.Amount,
                    Status = row.Status,
                    DocumentDate = row.CreatedAt
                });
            }

            model.Sections.Add(section);
            model.StatusBreakdown = rows
                .GroupBy(row => row.TransactionType)
                .Select(group => new AdminFinancialReportBreakdownItemDto
                {
                    Key = group.Key,
                    Label = TransactionTypeLabel(group.Key),
                    Count = group.Count(),
                    Amount = group.Sum(item => item.Amount),
                    Percentage = rows.Count == 0
                        ? 0m
                        : Math.Round(group.Count() * 100m / rows.Count, 2)
                })
                .ToList();
        }

        private async Task PopulateComprehensiveReportAsync(
            AdminFinancialReportPageDto model,
            bool includeAllRows,
            CancellationToken cancellationToken)
        {
            var originalFilter = model.Filter;
            var sectionLimit = includeAllRows
                ? int.MaxValue
                : originalFilter.PageSize;

            var commissionRows = await LoadBookingCommissionReportRowsAsync(
                originalFilter,
                includeAllRows,
                cancellationToken);

            var topUpFilter = new AdminFinancialReportFilterDto
            {
                ReportType = AdminFinancialReportTypes.TopUps,
                OwnerId = originalFilter.OwnerId,
                PlaceId = originalFilter.PlaceId,
                FromDate = originalFilter.FromDate,
                ToDate = originalFilter.ToDate,
                DateScope = AdminFinancialDateScopes.CreatedAt,
                Search = originalFilter.Search,
                PageSize = originalFilter.PageSize,
                PageNumber = originalFilter.PageNumber
            };

            var topUpRows = await LoadTopUpReportRowsAsync(
                topUpFilter,
                includeAllRows,
                cancellationToken);

            var accountRows = await LoadAccountReportRowsAsync(
                originalFilter,
                includeAllRows,
                AdminFinancialReportTypes.CommissionAccounts,
                cancellationToken);

            var transactionFilter = new AdminFinancialReportFilterDto
            {
                ReportType = AdminFinancialReportTypes.Transactions,
                OwnerId = originalFilter.OwnerId,
                PlaceId = originalFilter.PlaceId,
                FromDate = originalFilter.FromDate,
                ToDate = originalFilter.ToDate,
                DateScope = AdminFinancialDateScopes.CreatedAt,
                Search = originalFilter.Search,
                PageSize = originalFilter.PageSize,
                PageNumber = originalFilter.PageNumber
            };

            var transactionRows = await LoadTransactionReportRowsAsync(
                transactionFilter,
                includeAllRows,
                cancellationToken);

            commissionRows = commissionRows.Take(sectionLimit).ToList();
            topUpRows = topUpRows.Take(sectionLimit).ToList();
            accountRows = accountRows.Take(sectionLimit).ToList();
            transactionRows = transactionRows.Take(sectionLimit).ToList();

            model.TotalAvailableRows =
                commissionRows.Count +
                topUpRows.Count +
                accountRows.Count +
                transactionRows.Count;

            model.Pagination = BuildPagination(
                1,
                originalFilter.PageSize,
                model.TotalAvailableRows);

            var totalCalculatedCommission = commissionRows.Sum(row => row.PlatformCommission);
            var totalChargedCommission = commissionRows.Sum(row => row.ChargedAmount);
            var totalTopUps = topUpRows
                .Where(row => IsState(row.Status, AdminCommissionTopUpStatuses.Completed))
                .Sum(row => row.Amount);
            var totalAvailable = accountRows.Sum(row => row.AvailableBalance);
            var totalHeld = accountRows.Sum(row => row.HeldBalance);
            var totalDebt = accountRows.Sum(row => row.DebtAmount);

            model.Metrics.AddRange(new[]
            {
                new AdminFinancialReportMetricDto
                {
                    Key = "CommissionCalculated",
                    Label = "العمولات المحسوبة",
                    Value = $"{totalCalculatedCommission:N2} {model.Currency}",
                    NumericValue = totalCalculatedCommission
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "CommissionCharged",
                    Label = "إيراد العمولة الفعلي",
                    Value = $"{totalChargedCommission:N2} {model.Currency}",
                    NumericValue = totalChargedCommission,
                    Tone = "Success"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "TopUps",
                    Label = "الشحن المعتمد",
                    Value = $"{totalTopUps:N2} {model.Currency}",
                    NumericValue = totalTopUps
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Available",
                    Label = "الرصيد المتاح",
                    Value = $"{totalAvailable:N2} {model.Currency}",
                    NumericValue = totalAvailable
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Held",
                    Label = "الرصيد المحجوز",
                    Value = $"{totalHeld:N2} {model.Currency}",
                    NumericValue = totalHeld,
                    Tone = "Warning"
                },
                new AdminFinancialReportMetricDto
                {
                    Key = "Debt",
                    Label = "الدين الحالي",
                    Value = $"{totalDebt:N2} {model.Currency}",
                    NumericValue = totalDebt,
                    Tone = "Danger"
                }
            });

            model.Insights.Add(new AdminFinancialReportInsightDto
            {
                Title = "صافي مركز ضمان العمولات",
                Value = $"{totalAvailable - totalDebt:N2} {model.Currency}",
                Description = "الرصيد المتاح مطروحًا منه الدين الحالي.",
                Tone = totalAvailable >= totalDebt ? "Success" : "Danger"
            });

            var commissionsSection = new AdminFinancialReportSectionDto
            {
                Key = "Commissions",
                Title = "ملخص عمولات الحجوزات",
                Description = "هذا القسم للحجوزات فقط ولا يخلط الشحن أو الحركات كسجلات حجز.",
                IsPrimary = true,
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("Booking", "الحجز", AdminFinancialReportDataTypes.Number, 11d, AdminFinancialReportAlignments.Center),
                    Column("Place", "المنشأة", width: 24d),
                    Column("Value", "قيمة الحجز", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Commission", "العمولة", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Status", "الحالة", AdminFinancialReportDataTypes.Status, 16d, AdminFinancialReportAlignments.Center)
                }
            };

            commissionsSection.Rows = commissionRows
                .Select(row => ReportRow(
                    $"comprehensive-booking-{row.BookingId}",
                    new[]
                    {
                        NumberCell(row.BookingId),
                        TextCell(row.PlaceName),
                        MoneyCell(row.BookingTotalPrice, model.Currency),
                        MoneyCell(row.PlatformCommission, model.Currency),
                        StatusCell(
                            row.CommissionStatus,
                            CommissionStatusLabel(row.CommissionStatus),
                            StatusTone(row.CommissionStatus))
                    },
                    documentType: AdminFinancialDocumentTypes.BookingCommissionSettlement,
                    documentSourceId: row.BookingId))
                .ToList();

            var accountsSection = new AdminFinancialReportSectionDto
            {
                Key = "Accounts",
                Title = "أرصدة حسابات الضمان",
                Description = "رصيد مستقل لكل منشأة.",
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("Place", "المنشأة", width: 25d),
                    Column("Available", "المتاح", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Held", "المحجوز", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Debt", "الدين", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Status", "الحالة", AdminFinancialReportDataTypes.Status, 16d, AdminFinancialReportAlignments.Center)
                }
            };

            accountsSection.Rows = accountRows
                .Select(row => ReportRow(
                    $"comprehensive-account-{row.PlaceCommissionAccountId}",
                    new[]
                    {
                        TextCell(row.PlaceName),
                        MoneyCell(row.AvailableBalance, model.Currency),
                        MoneyCell(row.HeldBalance, model.Currency),
                        MoneyCell(row.DebtAmount, model.Currency, row.DebtAmount > 0m ? "Danger" : null),
                        StatusCell(
                            row.AccountStatus,
                            AccountStatusLabel(row.AccountStatus),
                            StatusTone(row.AccountStatus))
                    },
                    documentType: AdminFinancialDocumentTypes.CommissionAccountStatement,
                    documentSourceId: row.PlaceCommissionAccountId))
                .ToList();

            var topUpsSection = new AdminFinancialReportSectionDto
            {
                Key = "TopUps",
                Title = "شحن رصيد الضمان",
                Description = "قسم مستقل عن سجلات الحجوزات.",
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("Request", "الطلب", AdminFinancialReportDataTypes.Number, 11d, AdminFinancialReportAlignments.Center),
                    Column("Place", "المنشأة", width: 24d),
                    Column("Amount", "المبلغ", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("DebtPaid", "سداد الدين", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Credited", "المضاف للرصيد", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Status", "الحالة", AdminFinancialReportDataTypes.Status, 15d, AdminFinancialReportAlignments.Center)
                }
            };

            topUpsSection.Rows = topUpRows
                .Select(row => ReportRow(
                    $"comprehensive-topup-{row.CommissionTransactionId}",
                    new[]
                    {
                        NumberCell(row.CommissionTransactionId),
                        TextCell(row.PlaceName),
                        MoneyCell(row.Amount, model.Currency),
                        MoneyCell(row.DebtPaymentAmount, model.Currency),
                        MoneyCell(row.CreditedToAvailableBalance, model.Currency),
                        StatusCell(row.Status, row.StatusLabel, StatusTone(row.Status))
                    },
                    documentType: IsState(row.Status, AdminCommissionTopUpStatuses.Completed)
                        ? AdminFinancialDocumentTypes.TopUpReceipt
                        : null,
                    documentSourceId: IsState(row.Status, AdminCommissionTopUpStatuses.Completed)
                        ? row.CommissionTransactionId
                        : null))
                .ToList();

            var transactionsSection = new AdminFinancialReportSectionDto
            {
                Key = "Transactions",
                Title = "آخر الحركات المالية",
                Description = "قسم تدقيق مستقل.",
                Columns = new List<AdminFinancialReportColumnDto>
                {
                    Column("Transaction", "الحركة", AdminFinancialReportDataTypes.Number, 11d, AdminFinancialReportAlignments.Center),
                    Column("Place", "المنشأة", width: 24d),
                    Column("Type", "النوع", AdminFinancialReportDataTypes.Status, 17d, AdminFinancialReportAlignments.Center),
                    Column("Amount", "المبلغ", AdminFinancialReportDataTypes.Money, 18d, AdminFinancialReportAlignments.Left),
                    Column("Status", "الحالة", AdminFinancialReportDataTypes.Status, 15d, AdminFinancialReportAlignments.Center),
                    Column("Date", "التاريخ", AdminFinancialReportDataTypes.DateTime, 18d, AdminFinancialReportAlignments.Center)
                }
            };

            transactionsSection.Rows = transactionRows
                .Select(row => ReportRow(
                    $"comprehensive-transaction-{row.CommissionTransactionId}",
                    new[]
                    {
                        NumberCell(row.CommissionTransactionId),
                        TextCell(row.PlaceName),
                        StatusCell(row.TransactionType, row.TransactionTypeLabel),
                        MoneyCell(row.Amount, model.Currency),
                        StatusCell(row.Status, row.StatusLabel, StatusTone(row.Status)),
                        DateCell(row.CreatedAt, true)
                    },
                    documentType: AdminFinancialDocumentTypes.TransactionDocument,
                    documentSourceId: row.CommissionTransactionId))
                .ToList();

            model.Sections.Add(commissionsSection);
            model.Sections.Add(accountsSection);
            model.Sections.Add(topUpsSection);
            model.Sections.Add(transactionsSection);
        }

        public async Task<AdminFinancialReportExportDto> BuildFinancialReportExportAsync(
            AdminFinancialReportFilterDto filter,
            string? generatedByName = null,
            CancellationToken cancellationToken = default)
        {
            var page = await GetFinancialReportAsync(
                filter,
                includeAllRows: true,
                generatedByName: generatedByName,
                cancellationToken: cancellationToken);

            var export = new AdminFinancialReportExportDto
            {
                PlatformName = page.PlatformName,
                PlatformLogoPath = page.PlatformLogoPath,
                Currency = page.Currency,
                ReportType = page.ReportType,
                Title = page.Title,
                Subtitle = page.Subtitle,
                DateScopeLabel = page.DateScopeLabel,
                PeriodLabel = page.PeriodLabel,
                FilterDescription = page.FilterDescription,
                GeneratedAt = page.GeneratedAt,
                GeneratedByName = page.GeneratedByName,
                TotalAvailableRows = page.TotalAvailableRows,
                Metrics = page.Metrics,
                Insights = page.Insights,
                PlaceBreakdown = page.PlaceBreakdown,
                StatusBreakdown = page.StatusBreakdown,
                SuggestedFileName = BuildReportFileName(page.ReportType, page.GeneratedAt)
            };

            foreach (var section in page.Sections)
            {
                var exportSection = new AdminFinancialReportExportSectionDto
                {
                    Key = section.Key,
                    Title = section.Title,
                    Description = section.Description,
                    Metrics = section.Metrics,
                    Columns = section.Columns
                        .Where(column => column.IsVisible)
                        .Select(column =>
                            new AdminFinancialReportExportColumnDto
                            {
                                Key = column.Key,
                                Label = column.Label,
                                DataType = column.DataType,
                                Alignment = column.Alignment,
                                Width = column.Width
                            })
                        .ToList()
                };

                foreach (var row in section.Rows)
                {
                    var cells = row.TypedCells.Count > 0
                        ? row.TypedCells
                        : row.Cells
                            .Select(value => TextCell(value))
                            .ToList();

                    exportSection.Rows.Add(
                        new AdminFinancialReportExportRowDto
                        {
                            Tone = row.Tone,
                            Cells = cells.Select(cell => cell.Text).ToList(),
                            TypedCells = cells
                                .Select(cell =>
                                    new AdminFinancialReportExportCellDto
                                    {
                                        Text = cell.Text,
                                        DataType = cell.DataType,
                                        DecimalValue = cell.DecimalValue,
                                        IntegerValue = cell.IntegerValue,
                                        DateValue = cell.DateValue,
                                        BooleanValue = cell.BooleanValue
                                    })
                                .ToList()
                        });
                }

                export.Sections.Add(exportSection);
            }

            return export;
        }

        public async Task<AdminFinancialExcelFileDto> BuildFinancialExcelAsync(
            AdminFinancialReportFilterDto filter,
            string? generatedByName = null,
            CancellationToken cancellationToken = default)
        {
            var report = await BuildFinancialReportExportAsync(
                filter,
                generatedByName,
                cancellationToken);

            return new AdminFinancialExcelFileDto
            {
                Content = _excelBuilder.Build(report),
                FileName = report.SuggestedFileName.EndsWith(
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase)
                        ? report.SuggestedFileName
                        : report.SuggestedFileName + ".xlsx"
            };
        }

        private static string BuildReportFileName(
            string reportType,
            DateTime generatedAt)
        {
            var key = reportType switch
            {
                AdminFinancialReportTypes.BookingCommissions => "booking-commissions",
                AdminFinancialReportTypes.TopUps => "topups",
                AdminFinancialReportTypes.CommissionAccounts => "commission-accounts",
                AdminFinancialReportTypes.Debts => "debts",
                AdminFinancialReportTypes.Transactions => "transactions",
                AdminFinancialReportTypes.LowBalanceAccounts => "low-balance",
                AdminFinancialReportTypes.SuspendedAccounts => "suspended-accounts",
                AdminFinancialReportTypes.Comprehensive => "comprehensive",
                _ => "financial-report"
            };

            return $"admin-{key}-{generatedAt:yyyyMMdd-HHmm}";
        }

        /* ====================================================================
           Financial documents
           ==================================================================== */

        public async Task<AdminFinancialDocumentDetailsDto?> GetFinancialDocumentAsync(
            string documentType,
            int sourceId,
            string? generatedByName = null,
            CancellationToken cancellationToken = default)
        {
            if (sourceId <= 0 ||
                !AdminFinancialDocumentTypes.All.Contains(
                    documentType,
                    StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            var settings =
                await GetSettingsAsync(
                    tracking: false,
                    cancellationToken);

            var document = documentType switch
            {
                AdminFinancialDocumentTypes.TopUpReceipt =>
                    await BuildTopUpReceiptAsync(
                        sourceId,
                        cancellationToken),

                AdminFinancialDocumentTypes.BookingCommissionSettlement =>
                    await BuildBookingCommissionSettlementAsync(
                        sourceId,
                        cancellationToken),

                AdminFinancialDocumentTypes.TransactionDocument =>
                    await BuildTransactionDocumentAsync(
                        sourceId,
                        cancellationToken),

                AdminFinancialDocumentTypes.CommissionAccountStatement =>
                    await BuildCommissionAccountStatementAsync(
                        sourceId,
                        cancellationToken),

                _ => null
            };

            if (document == null)
            {
                return null;
            }

            document.PlatformName = FirstNonEmpty(
                "دليلك اليمني",
                settings?.SiteName);
            document.PlatformLogoPath = settings?.LogoPath?.Trim();
            document.ContactEmail = settings?.ContactEmail?.Trim();
            document.SupportPhone = settings?.SupportPhone?.Trim();
            document.Currency = GetCurrency(settings);
            document.GeneratedAt = DateTime.Now;
            document.GeneratedByName = generatedByName;
            document.FooterText =
                "هذا المستند صادر إلكترونيًا من الإدارة المالية لمنصة دليلك اليمني.";

            return document;
        }

        private async Task<AdminFinancialDocumentDetailsDto?> BuildTopUpReceiptAsync(
            int sourceId,
            CancellationToken cancellationToken)
        {
            var details = await GetTopUpDetailsAsync(
                sourceId,
                cancellationToken);

            if (details == null ||
                !IsState(
                    details.Status,
                    AdminCommissionTopUpStatuses.Completed))
            {
                return null;
            }

            return new AdminFinancialDocumentDetailsDto
            {
                DocumentType = AdminFinancialDocumentTypes.TopUpReceipt,
                SourceId = details.CommissionTransactionId,
                DocumentNumber = $"TOP-{details.CommissionTransactionId:D8}",
                Title = "إيصال اعتماد شحن رصيد الضمان",
                Subtitle = details.PlaceName,
                Status = details.StatusLabel,
                DocumentDate = details.ReviewedAt ?? details.CreatedAt,
                TotalAmount = details.Amount,
                Sections = new List<AdminFinancialDocumentSectionDto>
                {
                    new()
                    {
                        Title = "بيانات المالك والمنشأة",
                        Fields = new List<AdminFinancialDocumentFieldDto>
                        {
                            new() { Label = "المالك", Value = details.OwnerName },
                            new() { Label = "المنشأة", Value = details.PlaceName },
                            new() { Label = "رقم حساب الضمان", Value = details.PlaceId.ToString(CultureInfo.InvariantCulture) }
                        }
                    },
                    new()
                    {
                        Title = "بيانات التحويل",
                        Fields = new List<AdminFinancialDocumentFieldDto>
                        {
                            new() { Label = "حساب المنصة", Value = details.PlatformWalletName },
                            new() { Label = "رقم حساب المنصة", Value = details.PlatformAccountNumber },
                            new() { Label = "اسم المستفيد", Value = details.PlatformBeneficiaryName },
                            new() { Label = "الرقم المرجعي", Value = details.ReferenceNumber ?? "—", IsEmphasized = true },
                            new() { Label = "تاريخ التحويل", Value = details.TransferDate?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—" },
                            new() { Label = "تاريخ الاعتماد", Value = details.ReviewedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—" },
                            new() { Label = "راجع الطلب", Value = details.ReviewedByName ?? "الإدارة" }
                        }
                    }
                },
                Amounts = new List<AdminFinancialDocumentAmountDto>
                {
                    new() { Label = "مبلغ الشحن", Amount = details.Amount },
                    new() { Label = "المستخدم لسداد الدين", Amount = details.DebtPaymentAmount, Tone = "Warning" },
                    new() { Label = "المضاف للرصيد المتاح", Amount = details.CreditedToAvailableBalance, Tone = "Success" },
                    new() { Label = "الرصيد المتاح بعد الاعتماد", Amount = details.AvailableBalanceAfter, IsTotal = true, Tone = "Success" }
                },
                Notes = details.Notes
            };
        }

        private async Task<AdminFinancialDocumentDetailsDto?>
            BuildBookingCommissionSettlementAsync(
                int sourceId,
                CancellationToken cancellationToken)
        {
            var details = await GetBookingCommissionDetailsAsync(
                sourceId,
                cancellationToken);

            if (details == null)
            {
                return null;
            }

            var settlementDate =
                details.SettledAt ??
                details.CommissionChargedAt ??
                details.CommissionReleasedAt ??
                details.CommissionHeldAt ??
                details.BookingCreatedAt ??
                details.UpdatedAt;

            return new AdminFinancialDocumentDetailsDto
            {
                DocumentType =
                    AdminFinancialDocumentTypes.BookingCommissionSettlement,
                SourceId = details.BookingId,
                DocumentNumber = $"COM-{details.BookingId:D8}",
                Title = "كشف تسوية عمولة الحجز",
                Subtitle = details.PlaceName,
                Status = CommissionStatusLabel(details.CommissionStatus),
                DocumentDate = settlementDate,
                TotalAmount = details.PlatformCommission,
                Sections = new List<AdminFinancialDocumentSectionDto>
                {
                    new()
                    {
                        Title = "بيانات الحجز",
                        Fields = new List<AdminFinancialDocumentFieldDto>
                        {
                            new() { Label = "رقم الحجز", Value = details.BookingId.ToString(CultureInfo.InvariantCulture), IsEmphasized = true },
                            new() { Label = "العميل", Value = details.CustomerName },
                            new() { Label = "المالك", Value = details.OwnerName },
                            new() { Label = "المنشأة", Value = details.PlaceName },
                            new() { Label = "حالة الحجز", Value = details.BookingState },
                            new() { Label = "حالة الدفع", Value = details.PaymentStatus },
                            new() { Label = "بداية الحجز", Value = details.BookingStartDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) },
                            new() { Label = "نهاية الحجز", Value = details.BookingEndDate?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—" }
                        }
                    },
                    new()
                    {
                        Title = "دورة العمولة",
                        Fields = details.Timeline
                            .Select(item => new AdminFinancialDocumentFieldDto
                            {
                                Label = item.Label,
                                Value = item.OccurredAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "لم تنفذ",
                                Hint = item.Description,
                                IsEmphasized = item.IsCurrent
                            })
                            .ToList()
                    }
                },
                Amounts = new List<AdminFinancialDocumentAmountDto>
                {
                    new() { Label = "قيمة الحجز", Amount = details.BookingTotalPrice },
                    new() { Label = "أساس احتساب العمولة", Amount = details.CommissionBaseAmount },
                    new() { Label = "العمولة المحجوزة", Amount = details.HeldAmount, Tone = "Warning" },
                    new() { Label = "العمولة المخصومة", Amount = details.ChargedAmount, Tone = "Success" },
                    new() { Label = "العمولة المحررة", Amount = details.ReleasedAmount, Tone = "Muted" },
                    new() { Label = "الدين الناتج", Amount = details.DebtAmount, Tone = details.DebtAmount > 0m ? "Danger" : "Default" },
                    new() { Label = $"عمولة المنصة ({details.CommissionRateSnapshot:N2}%)", Amount = details.PlatformCommission, IsTotal = true }
                }
            };
        }

        private async Task<AdminFinancialDocumentDetailsDto?> BuildTransactionDocumentAsync(
            int sourceId,
            CancellationToken cancellationToken)
        {
            var details = await GetTransactionDetailsAsync(
                sourceId,
                cancellationToken);

            if (details == null)
            {
                return null;
            }

            return new AdminFinancialDocumentDetailsDto
            {
                DocumentType = AdminFinancialDocumentTypes.TransactionDocument,
                SourceId = details.CommissionTransactionId,
                DocumentNumber = $"TRX-{details.CommissionTransactionId:D8}",
                Title = "مستند حركة مالية",
                Subtitle = details.TransactionTypeLabel,
                Status = details.StatusLabel,
                DocumentDate = details.CreatedAt,
                TotalAmount = details.Amount,
                Sections = new List<AdminFinancialDocumentSectionDto>
                {
                    new()
                    {
                        Title = "بيانات الحركة",
                        Fields = new List<AdminFinancialDocumentFieldDto>
                        {
                            new() { Label = "رقم الحركة", Value = details.CommissionTransactionId.ToString(CultureInfo.InvariantCulture), IsEmphasized = true },
                            new() { Label = "نوع الحركة", Value = details.TransactionTypeLabel },
                            new() { Label = "المالك", Value = details.OwnerName },
                            new() { Label = "المنشأة", Value = details.PlaceName },
                            new() { Label = "رقم الحجز", Value = details.BookingId?.ToString(CultureInfo.InvariantCulture) ?? "—" },
                            new() { Label = "المرجع", Value = details.ReferenceNumber ?? "—" },
                            new() { Label = "منشئ الحركة", Value = details.CreatedByName ?? "النظام" },
                            new() { Label = "تاريخ الحركة", Value = details.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) }
                        }
                    },
                    new()
                    {
                        Title = "الأرصدة قبل وبعد",
                        Fields = new List<AdminFinancialDocumentFieldDto>
                        {
                            new() { Label = "الرصيد المتاح", Value = $"{details.AvailableBalanceBefore:N2} ← {details.AvailableBalanceAfter:N2}" },
                            new() { Label = "الرصيد المحجوز", Value = $"{details.HeldBalanceBefore:N2} ← {details.HeldBalanceAfter:N2}" },
                            new() { Label = "الدين", Value = $"{details.DebtBefore:N2} ← {details.DebtAfter:N2}" }
                        }
                    }
                },
                Amounts = new List<AdminFinancialDocumentAmountDto>
                {
                    new() { Label = "مبلغ الحركة", Amount = details.Amount, IsTotal = true }
                },
                Notes = details.Notes ?? details.RejectionReason
            };
        }

        private async Task<AdminFinancialDocumentDetailsDto?>
            BuildCommissionAccountStatementAsync(
                int sourceId,
                CancellationToken cancellationToken)
        {
            var details = await GetCommissionAccountDetailsAsync(
                sourceId,
                cancellationToken);

            if (details == null)
            {
                return null;
            }

            return new AdminFinancialDocumentDetailsDto
            {
                DocumentType =
                    AdminFinancialDocumentTypes.CommissionAccountStatement,
                SourceId = details.PlaceCommissionAccountId,
                DocumentNumber = $"ACC-{details.PlaceCommissionAccountId:D8}",
                Title = "كشف حساب ضمان العمولات",
                Subtitle = details.PlaceName,
                Status = AccountStatusLabel(details.AccountStatus),
                DocumentDate = details.UpdatedAt ?? details.CreatedAt,
                TotalAmount = details.TotalAccountBalance,
                Sections = new List<AdminFinancialDocumentSectionDto>
                {
                    new()
                    {
                        Title = "بيانات الحساب",
                        Fields = new List<AdminFinancialDocumentFieldDto>
                        {
                            new() { Label = "رقم الحساب", Value = details.PlaceCommissionAccountId.ToString(CultureInfo.InvariantCulture), IsEmphasized = true },
                            new() { Label = "المالك", Value = details.OwnerName },
                            new() { Label = "المنشأة", Value = details.PlaceName },
                            new() { Label = "حالة الحساب", Value = AccountStatusLabel(details.AccountStatus) },
                            new() { Label = "السماح بالدين", Value = details.EffectiveAllowNegativeBalance ? "نعم" : "لا" },
                            new() { Label = "حد الدين", Value = details.EffectiveDebtLimit.ToString("N2", CultureInfo.GetCultureInfo("en-US")) },
                            new() { Label = "حد انخفاض الرصيد", Value = details.EffectiveLowBalanceThreshold.ToString("N2", CultureInfo.GetCultureInfo("en-US")) },
                            new() { Label = "آخر شحن", Value = details.LastTopUpAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—" }
                        }
                    },
                    new()
                    {
                        Title = "ملخص النشاط",
                        Fields = new List<AdminFinancialDocumentFieldDto>
                        {
                            new() { Label = "إجمالي الشحن", Value = details.TotalTopUps.ToString("N2", CultureInfo.GetCultureInfo("en-US")) },
                            new() { Label = "إجمالي العمولات المحجوزة", Value = details.TotalCommissionHeld.ToString("N2", CultureInfo.GetCultureInfo("en-US")) },
                            new() { Label = "إجمالي العمولات المخصومة", Value = details.TotalCommissionCharged.ToString("N2", CultureInfo.GetCultureInfo("en-US")) },
                            new() { Label = "إجمالي العمولات المحررة", Value = details.TotalCommissionReleased.ToString("N2", CultureInfo.GetCultureInfo("en-US")) },
                            new() { Label = "إجمالي الدين المسجل", Value = details.TotalDebtRegistered.ToString("N2", CultureInfo.GetCultureInfo("en-US")) },
                            new() { Label = "إجمالي الدين المسدد", Value = details.TotalDebtPaid.ToString("N2", CultureInfo.GetCultureInfo("en-US")) }
                        }
                    }
                },
                Amounts = new List<AdminFinancialDocumentAmountDto>
                {
                    new() { Label = "الرصيد المتاح", Amount = details.AvailableBalance, Tone = "Success" },
                    new() { Label = "الرصيد المحجوز", Amount = details.HeldBalance, Tone = "Warning" },
                    new() { Label = "الدين الحالي", Amount = details.DebtAmount, Tone = details.DebtAmount > 0m ? "Danger" : "Default" },
                    new() { Label = "صافي مركز الحساب", Amount = details.TotalAccountBalance, IsTotal = true }
                }
            };
        }
    }
}

