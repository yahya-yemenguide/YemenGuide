using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Finance
{
    public class CommissionFinanceService : ICommissionFinanceService
    {
        private const long MaximumReceiptSize = 5 * 1024 * 1024;

        private static readonly HashSet<string> AllowedReceiptExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp", ".pdf"
            };

        private static readonly HashSet<int> AllowedTransactionPageSizes =
            new()
            {
                10,
                25,
                50
            };

        private static readonly HashSet<int> AllowedRefundPageSizes =
            new()
            {
                10,
                25,
                50
            };

        private static readonly HashSet<int> AllowedReportPageSizes =
            new()
            {
                10,
                25,
                50
            };

        private const int MaximumReportExportRows =
            10000;

        private const int RefundLookupBatchSize =
            1000;

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CommissionFinanceService> _logger;
        private readonly INotificationService _notificationService;

        public CommissionFinanceService(
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<CommissionFinanceService> logger,
            INotificationService notificationService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _notificationService = notificationService;
        }

        /// <summary>
        /// تبدأ معاملة مالية فقط عندما لا تكون العملية المستدعية قد بدأت
        /// معاملة على نفس DbContext. بهذا تستطيع عمليات دورة العمولة العمل
        /// منفردة أو كجزء ذري من تغيير حالة الحجز دون إنشاء معاملة متداخلة.
        /// </summary>
        private async Task<IDbContextTransaction?>
            BeginCommissionTransactionIfNeededAsync(
                CancellationToken cancellationToken)
        {
            if (_context.Database.CurrentTransaction != null)
            {
                return null;
            }

            return await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
        }

        private static async Task CommitIfOwnedAsync(
            IDbContextTransaction? transaction,
            CancellationToken cancellationToken)
        {
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        private static async Task RollbackIfOwnedAsync(
            IDbContextTransaction? transaction,
            CancellationToken cancellationToken)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        private static bool IsState(string? value, string expected) =>
            string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        private static string FirstNonEmpty(string fallback, params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? fallback;

        private static string? NormalizeAllowedValue(
            string? value,
            IReadOnlyCollection<string> allowedValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized =
                value.Trim();

            return allowedValues.FirstOrDefault(
                allowed =>
                    IsState(
                        allowed,
                        normalized));
        }

        private static string GetTransactionTypeLabel(
            string? transactionType)
        {
            if (
                IsState(
                    transactionType,
                    FinancialTransactionTypes.TopUp)
            )
            {
                return "شحن رصيد";
            }

            if (
                IsState(
                    transactionType,
                    FinancialTransactionTypes.CommissionHold)
            )
            {
                return "حجز عمولة";
            }

            if (
                IsState(
                    transactionType,
                    FinancialTransactionTypes.CommissionCharge)
            )
            {
                return "خصم عمولة";
            }

            if (
                IsState(
                    transactionType,
                    FinancialTransactionTypes.CommissionRelease)
            )
            {
                return "تحرير عمولة";
            }

            if (
                IsState(
                    transactionType,
                    FinancialTransactionTypes.Debt)
            )
            {
                return "تسجيل دين";
            }

            if (
                IsState(
                    transactionType,
                    FinancialTransactionTypes.DebtPayment)
            )
            {
                return "سداد دين";
            }

            if (
                IsState(
                    transactionType,
                    FinancialTransactionTypes.Adjustment)
            )
            {
                return "تعديل إداري";
            }

            return "حركة مالية";
        }

        private static string GetTransactionStatusLabel(
            string? status)
        {
            if (
                IsState(
                    status,
                    FinancialTransactionStatuses.Pending)
            )
            {
                return "قيد المراجعة";
            }

            if (
                IsState(
                    status,
                    FinancialTransactionStatuses.Completed)
            )
            {
                return "مكتملة";
            }

            if (
                IsState(
                    status,
                    FinancialTransactionStatuses.Rejected)
            )
            {
                return "مرفوضة";
            }

            if (
                IsState(
                    status,
                    FinancialTransactionStatuses.Reversed)
            )
            {
                return "معكوسة";
            }

            if (
                IsState(
                    status,
                    FinancialTransactionStatuses.Failed)
            )
            {
                return "فشلت";
            }

            if (
                IsState(
                    status,
                    FinancialTransactionStatuses.Cancelled)
            )
            {
                return "ملغاة";
            }

            return "غير محددة";
        }

        private static string GetTransactionDirection(
            CommissionTransaction transaction)
        {
            if (
                IsState(
                    transaction.TransactionType,
                    FinancialTransactionTypes.TopUp) ||
                IsState(
                    transaction.TransactionType,
                    FinancialTransactionTypes.CommissionRelease) ||
                IsState(
                    transaction.TransactionType,
                    FinancialTransactionTypes.DebtPayment)
            )
            {
                return FinancialTransactionDirections.Credit;
            }

            if (
                IsState(
                    transaction.TransactionType,
                    FinancialTransactionTypes.CommissionHold) ||
                IsState(
                    transaction.TransactionType,
                    FinancialTransactionTypes.CommissionCharge) ||
                IsState(
                    transaction.TransactionType,
                    FinancialTransactionTypes.Debt)
            )
            {
                return FinancialTransactionDirections.Debit;
            }

            var availableChange =
                transaction.AvailableBalanceAfter -
                transaction.AvailableBalanceBefore;

            if (availableChange > 0m)
            {
                return FinancialTransactionDirections.Credit;
            }

            if (availableChange < 0m)
            {
                return FinancialTransactionDirections.Debit;
            }

            var debtChange =
                transaction.DebtAfter -
                transaction.DebtBefore;

            if (debtChange > 0m)
            {
                return FinancialTransactionDirections.DebtIncrease;
            }

            if (debtChange < 0m)
            {
                return FinancialTransactionDirections.DebtDecrease;
            }

            return FinancialTransactionDirections.Neutral;
        }

        private static List<FinancialStatusOptionDto>
            BuildTransactionTypeOptions(
                string? selectedValue)
        {
            return FinancialTransactionTypes.All
                .Select(value =>
                    new FinancialStatusOptionDto
                    {
                        Value =
                            value,

                        Label =
                            GetTransactionTypeLabel(
                                value),

                        IsSelected =
                            IsState(
                                value,
                                selectedValue ??
                                string.Empty)
                    })
                .ToList();
        }

        private static List<FinancialStatusOptionDto>
            BuildTransactionStatusOptions(
                string? selectedValue)
        {
            return FinancialTransactionStatuses.All
                .Select(value =>
                    new FinancialStatusOptionDto
                    {
                        Value =
                            value,

                        Label =
                            GetTransactionStatusLabel(
                                value),

                        IsSelected =
                            IsState(
                                value,
                                selectedValue ??
                                string.Empty)
                    })
                .ToList();
        }

        private static string GetRefundStatusLabel(
            string? status)
        {
            if (
                IsState(
                    status,
                    FinancialRefundStatuses.Pending)
            )
            {
                return "قيد المراجعة";
            }

            if (
                IsState(
                    status,
                    FinancialRefundStatuses.Processing)
            )
            {
                return "قيد المعالجة";
            }

            if (
                IsState(
                    status,
                    FinancialRefundStatuses.Completed)
            )
            {
                return "مكتمل";
            }

            if (
                IsState(
                    status,
                    FinancialRefundStatuses.Rejected)
            )
            {
                return "مرفوض";
            }

            if (
                IsState(
                    status,
                    FinancialRefundStatuses.Failed)
            )
            {
                return "فشل";
            }

            if (
                IsState(
                    status,
                    FinancialRefundStatuses.Cancelled)
            )
            {
                return "ملغى";
            }

            return "غير محدد";
        }

        private static List<FinancialStatusOptionDto>
            BuildRefundStatusOptions(
                string? selectedValue)
        {
            return FinancialRefundStatuses.All
                .Select(value =>
                    new FinancialStatusOptionDto
                    {
                        Value =
                            value,

                        Label =
                            GetRefundStatusLabel(
                                value),

                        IsSelected =
                            IsState(
                                value,
                                selectedValue ??
                                string.Empty)
                    })
                .ToList();
        }

        private static RefundRequestItemDto MapRefundRequest(
            RefundRequest refund)
        {
            var contactInfo =
                refund.Customer?
                    .ContactInfo?
                    .Trim();

            var contactInfoIsEmail =
                !string.IsNullOrWhiteSpace(
                    contactInfo) &&
                contactInfo.Contains(
                    '@');

            var customerEmail =
                contactInfoIsEmail
                    ? contactInfo
                    : null;

            var customerPhone =
                FirstNonEmpty(
                    string.Empty,
                    refund.Customer?
                        .PhoneNumber,
                    contactInfoIsEmail
                        ? null
                        : contactInfo);

            return new RefundRequestItemDto
            {
                RefundRequestId =
                    refund.RefundRequestId,

                BookingId =
                    refund.BookingId,

                CustomerId =
                    refund.CustomerId,

                CustomerName =
                    FirstNonEmpty(
                        "عميل",
                        refund.Customer?
                            .FullName,
                        refund.Customer?
                            .Username),

                CustomerPhone =
                    string.IsNullOrWhiteSpace(
                        customerPhone)
                        ? null
                        : customerPhone,

                CustomerEmail =
                    customerEmail,

                PlaceId =
                    refund.PlaceId,

                PlaceName =
                    FirstNonEmpty(
                        "منشأة غير معروفة",
                        refund.Place?
                            .PlaceNameAndDesc),

                PlaceImageUrl =
                    FirstNonEmpty(
                        string.Empty,
                        refund.Place?
                            .CoverImage,
                        refund.Place?
                            .LogoImage),

                BookingStartDate =
                    refund.Booking?
                        .StartDate,

                BookingEndDate =
                    refund.Booking?
                        .EndDate,

                BookingState =
                    refund.Booking?
                        .BookingState?
                        .Trim() ??
                    string.Empty,

                PaymentStatus =
                    refund.Booking?
                        .PaymentStatus?
                        .Trim() ??
                    string.Empty,

                DepositPaid =
                    refund.DepositPaid,

                BookingOriginalTotalPrice =
                    refund.Booking == null
                        ? 0m
                        : (
                            refund.Booking.OriginalTotalPrice > 0m
                                ? refund.Booking.OriginalTotalPrice
                                : refund.Booking.TotalPrice +
                                  Math.Max(
                                      0m,
                                      refund.Booking.DiscountAmount)
                        ),

                BookingDiscountAmount =
                    Math.Max(
                        0m,
                        refund.Booking?
                            .DiscountAmount ??
                        0m),

                BookingTotalPrice =
                    Math.Max(
                        0m,
                        refund.Booking?
                            .TotalPrice ??
                        0m),

                RequiredDepositAmount =
                    Math.Max(
                        0m,
                        refund.Booking?
                            .FinancialRecord?
                            .DepositAmount ??
                        0m),

                AmountPaidOnline =
                    Math.Max(
                        0m,
                        refund.Booking?
                            .FinancialRecord?
                            .AmountPaidOnline ??
                        0m),

                OnlinePaymentType =
                    refund.Booking?
                        .FinancialRecord?
                        .OnlinePaymentType?
                        .Trim(),

                PromotionId =
                    refund.Booking?
                        .PromotionId,

                PromotionTitleSnapshot =
                    refund.Booking?
                        .PromotionTitleSnapshot?
                        .Trim(),

                PromotionCodeSnapshot =
                    refund.Booking?
                        .PromotionCodeSnapshot?
                        .Trim(),

                CommissionRateSnapshot =
                    Math.Max(
                        0m,
                        refund.Booking?
                            .FinancialRecord?
                            .CommissionRateSnapshot ??
                        0m),

                CommissionBaseAmount =
                    refund.PlatformCommission <= 0m
                        ? 0m
                        : Math.Max(
                            0m,
                            refund.Booking?
                                .FinancialRecord?
                                .CommissionBaseAmount ??
                            0m),

                CancellationFee =
                    refund.CancellationFee,

                RefundAmount =
                    refund.RefundAmount,

                OwnerShare =
                    refund.OwnerShare,

                PlatformCommission =
                    refund.PlatformCommission,

                CommissionReleased =
                    refund.CommissionReleased,

                Status =
                    refund.Status?
                        .Trim() ??
                    FinancialRefundStatuses.Pending,

                StatusLabel =
                    GetRefundStatusLabel(
                        refund.Status),

                Reason =
                    refund.Reason?
                        .Trim(),

                ReferenceNumber =
                    refund.ReferenceNumber?
                        .Trim(),

                ProofImage =
                    refund.ProofImage?
                        .Trim(),

                RefundWalletId =
                    refund.RefundWalletId,

                RefundWalletName =
                    refund.RefundWallet?
                        .WalletName?
                        .Trim(),

                RefundAccountNumber =
                    refund.RefundAccountNumber?
                        .Trim(),

                RefundBeneficiaryName =
                    refund.RefundBeneficiaryName?
                        .Trim(),

                TransferredAt =
                    refund.TransferredAt,

                CustomerConfirmedAt =
                    refund.CustomerConfirmedAt,

                RequestedAt =
                    refund.RequestedAt,

                ProcessedAt =
                    refund.ProcessedAt,

                ProcessedByUserId =
                    refund.ProcessedByUserId,

                ProcessedByName =
                    FirstNonEmpty(
                        string.Empty,
                        refund.ProcessedByUser?
                            .FullName,
                        refund.ProcessedByUser?
                            .Username),

                Notes =
                    refund.Notes?
                        .Trim()
            };
        }

        private static string BuildRoomName(Room? room) =>
            room == null
                ? string.Empty
                : string.Join(" — ", new[] { room.RoomType?.Trim(), room.RoomNumber?.Trim() }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

        private static string BuildPackageName(PlacePackage? package) =>
            package == null
                ? string.Empty
                : string.Join(" — ", new[] { package.PackageName?.Trim(), package.TimeFrame?.Trim() }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

        private async Task<SystemSetting?> GetSettingsAsync(CancellationToken cancellationToken) =>
            await _context.SystemSettings
                .AsNoTracking()
                .OrderByDescending(setting => setting.Id)
                .FirstOrDefaultAsync(cancellationToken);

        private static string GetCurrency(SystemSetting? settings) =>
            string.IsNullOrWhiteSpace(settings?.DefaultCurrency)
                ? "ريال يمني"
                : settings.DefaultCurrency.Trim();

        private IQueryable<Place> OwnerPlaces(int ownerId) =>
            _context.Places.Where(place => place.OwnerId == ownerId);


        private static bool IsBookingEnabledCategory(Category? category) =>
            category != null &&
            category.BookingSystemId >= 1 &&
            category.BookingSystemId <= 4;

        private static bool IsOperationalPlace(Place place) =>
            (IsState(place.Status, "Active") ||
             IsState(place.Status, "Approved")) &&
            place.IsAvailable == true;

        private async Task<List<int>> GetOwnerPlaceIdsAsync(
            int ownerId,
            CancellationToken cancellationToken) =>
            await OwnerPlaces(ownerId)
                .AsNoTracking()
                .Select(place => place.PlaceId)
                .ToListAsync(cancellationToken);

        private async Task<bool> OwnerOwnsPlaceAsync(
            int ownerId,
            int placeId,
            CancellationToken cancellationToken) =>
            await _context.Places.AsNoTracking().AnyAsync(
                place => place.PlaceId == placeId && place.OwnerId == ownerId,
                cancellationToken);

        private async Task<int?> ValidatePlaceAsync(
            int ownerId,
            int? placeId,
            CancellationToken cancellationToken)
        {
            if (!placeId.HasValue || placeId.Value <= 0)
            {
                return null;
            }

            return await OwnerOwnsPlaceAsync(ownerId, placeId.Value, cancellationToken)
                ? placeId
                : null;
        }

        private static string CalculateAccountStatus(
            decimal available,
            decimal debt,
            bool allowNegative,
            decimal debtLimit,
            decimal lowThreshold,
            bool suspendWhenInsufficient,
            DateTime? debtDueAt,
            DateTime now)
        {
            available = Math.Max(0m, available);
            debt = Math.Max(0m, debt);
            debtLimit = Math.Max(0m, debtLimit);
            lowThreshold = Math.Max(0m, lowThreshold);

            if (debt > 0m)
            {
                var debtPolicyViolated =
                    !allowNegative ||
                    debtLimit <= 0m ||
                    debt > debtLimit + 0.01m;

                var debtOverdue =
                    debtDueAt.HasValue &&
                    debtDueAt.Value <= now;

                if (suspendWhenInsufficient &&
                    (debtPolicyViolated || debtOverdue))
                {
                    return FinancialAccountStatuses.Suspended;
                }

                return FinancialAccountStatuses.InDebt;
            }

            if (available <= 0m)
            {
                return !allowNegative && suspendWhenInsufficient
                    ? FinancialAccountStatuses.Suspended
                    : FinancialAccountStatuses.NeedsTopUp;
            }

            return available <= lowThreshold
                ? FinancialAccountStatuses.LowBalance
                : FinancialAccountStatuses.Active;
        }

        private static void SynchronizeDebtDates(
            PlaceCommissionAccount account,
            SystemSetting? settings,
            DateTime now)
        {
            if (account.DebtAmount <= 0m)
            {
                account.DebtAmount = 0m;
                account.DebtStartedAt = null;
                account.DebtDueAt = null;
                return;
            }

            if (!account.DebtStartedAt.HasValue)
            {
                account.DebtStartedAt = now;
            }

            if (!account.DebtDueAt.HasValue)
            {
                var graceDays = Math.Max(0, settings?.CommissionDebtGraceDays ?? 0);
                account.DebtDueAt = account.DebtStartedAt.Value.AddDays(graceDays);
            }
        }

        private static void RefreshAccountStatus(
            PlaceCommissionAccount account,
            SystemSetting? settings)
        {
            var now = DateTime.Now;
            SynchronizeDebtDates(account, settings, now);

            account.AccountStatus = CalculateAccountStatus(
                account.AvailableBalance,
                account.DebtAmount,
                account.AllowNegativeBalance ?? settings?.AllowNegativeCommissionBalance ?? false,
                account.DebtLimit ?? settings?.DefaultCommissionDebtLimit ?? 0m,
                account.LowBalanceThreshold ?? settings?.DefaultLowBalanceThreshold ?? 0m,
                settings?.SuspendBookingWhenBalanceInsufficient ?? true,
                account.DebtDueAt,
                now);

            account.UpdatedAt = now;
        }

        private static PlaceCommissionAccountDto MapAccount(
            PlaceCommissionAccount account,
            SystemSetting? settings)
        {
            var allowNegative =
                account.AllowNegativeBalance ?? settings?.AllowNegativeCommissionBalance ?? false;
            var debtLimit =
                account.DebtLimit ?? settings?.DefaultCommissionDebtLimit ?? 0m;
            var threshold =
                account.LowBalanceThreshold ?? settings?.DefaultLowBalanceThreshold ?? 0m;

            return new PlaceCommissionAccountDto
            {
                PlaceCommissionAccountId = account.PlaceCommissionAccountId,
                PlaceId = account.PlaceId,
                PlaceName = FirstNonEmpty("منشأة غير معروفة", account.Place?.PlaceNameAndDesc),
                PlaceImageUrl = FirstNonEmpty(
                    string.Empty,
                    account.Place?.CoverImage,
                    account.Place?.LogoImage),
                AvailableBalance = account.AvailableBalance,
                HeldBalance = account.HeldBalance,
                DebtAmount = account.DebtAmount,
                AllowNegativeBalance = allowNegative,
                DebtLimit = debtLimit,
                LowBalanceThreshold = threshold,
                AccountStatus = string.IsNullOrWhiteSpace(account.AccountStatus)
                    ? CalculateAccountStatus(
                        account.AvailableBalance,
                        account.DebtAmount,
                        allowNegative,
                        debtLimit,
                        threshold,
                        settings?.SuspendBookingWhenBalanceInsufficient ?? true,
                        account.DebtDueAt,
                        DateTime.Now)
                    : account.AccountStatus.Trim(),
                LastTopUpAt = account.LastTopUpAt,
                DebtStartedAt = account.DebtStartedAt,
                DebtDueAt = account.DebtDueAt,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            };
        }

        private async Task EnsureOwnerAccountsAsync(
            int ownerId,
            CancellationToken cancellationToken)
        {
            var settings = await GetSettingsAsync(cancellationToken);
            var placeIds = await OwnerPlaces(ownerId)
                .Where(place => place.PlaceCommissionAccount == null)
                .Select(place => place.PlaceId)
                .ToListAsync(cancellationToken);

            if (placeIds.Count == 0)
            {
                return;
            }

            var now = DateTime.Now;
            foreach (var placeId in placeIds)
            {
                var account = new PlaceCommissionAccount
                {
                    PlaceId = placeId,
                    AvailableBalance = 0m,
                    HeldBalance = 0m,
                    DebtAmount = 0m,
                    AllowNegativeBalance = null,
                    DebtLimit = null,
                    LowBalanceThreshold = null,
                    AccountStatus = FinancialAccountStatuses.NeedsTopUp,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                RefreshAccountStatus(account, settings);
                _context.PlaceCommissionAccounts.Add(account);
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(exception, "تعذر إنشاء بعض حسابات ضمان العمولة تلقائيًا.");
                _context.ChangeTracker.Clear();
            }
        }

        private async Task<PlaceCommissionAccount> GetOrCreateAccountAsync(
            int placeId,
            SystemSetting? settings,
            CancellationToken cancellationToken)
        {
            var account = await _context.PlaceCommissionAccounts
                .FirstOrDefaultAsync(item => item.PlaceId == placeId, cancellationToken);

            if (account != null)
            {
                return account;
            }

            if (!await _context.Places.AnyAsync(place => place.PlaceId == placeId, cancellationToken))
            {
                throw new InvalidOperationException("المنشأة غير موجودة.");
            }

            var now = DateTime.Now;
            account = new PlaceCommissionAccount
            {
                PlaceId = placeId,
                AvailableBalance = 0m,
                HeldBalance = 0m,
                DebtAmount = 0m,
                AllowNegativeBalance = null,
                DebtLimit = null,
                LowBalanceThreshold = null,
                AccountStatus = FinancialAccountStatuses.NeedsTopUp,
                CreatedAt = now,
                UpdatedAt = now
            };

            RefreshAccountStatus(account, settings);
            _context.PlaceCommissionAccounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);
            return account;
        }

        private static CommissionTransaction CreateMovement(
            PlaceCommissionAccount account,
            int placeId,
            int? bookingId,
            string type,
            decimal amount,
            decimal availableBefore,
            decimal heldBefore,
            decimal debtBefore,
            int? createdBy,
            string? notes)
        {
            return new CommissionTransaction
            {
                PlaceId = placeId,
                BookingId = bookingId,
                TransactionType = type,
                Amount = Math.Round(Math.Max(0m, amount), 2),
                AvailableBalanceBefore = availableBefore,
                AvailableBalanceAfter = account.AvailableBalance,
                HeldBalanceBefore = heldBefore,
                HeldBalanceAfter = account.HeldBalance,
                DebtBefore = debtBefore,
                DebtAfter = account.DebtAmount,
                Status = FinancialTransactionStatuses.Completed,
                CreatedAt = DateTime.Now,
                CreatedByUserId = createdBy > 0 ? createdBy : null,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };
        }

        private static CommissionTransactionItemDto MapTransaction(
            CommissionTransaction transaction)
        {
            return new CommissionTransactionItemDto
            {
                CommissionTransactionId =
                    transaction.CommissionTransactionId,

                PlaceId =
                    transaction.PlaceId,

                PlaceName =
                    FirstNonEmpty(
                        "منشأة غير معروفة",
                        transaction.Place?.PlaceNameAndDesc),

                PlaceImageUrl =
                    FirstNonEmpty(
                        string.Empty,
                        transaction.Place?.CoverImage,
                        transaction.Place?.LogoImage),

                BookingId =
                    transaction.BookingId,

                PlatformWalletId =
                    transaction.PlatformWalletId,

                PlatformWalletName =
                    transaction.PlatformWallet?
                        .Wallet?
                        .WalletName?
                        .Trim(),

                TransactionType =
                    transaction.TransactionType?
                        .Trim() ??
                    string.Empty,

                TransactionTypeLabel =
                    GetTransactionTypeLabel(
                        transaction.TransactionType),

                Direction =
                    GetTransactionDirection(
                        transaction),

                Amount =
                    transaction.Amount,

                AvailableBalanceBefore =
                    transaction.AvailableBalanceBefore,

                AvailableBalanceAfter =
                    transaction.AvailableBalanceAfter,

                HeldBalanceBefore =
                    transaction.HeldBalanceBefore,

                HeldBalanceAfter =
                    transaction.HeldBalanceAfter,

                DebtBefore =
                    transaction.DebtBefore,

                DebtAfter =
                    transaction.DebtAfter,

                Status =
                    transaction.Status?
                        .Trim() ??
                    FinancialTransactionStatuses.Completed,

                StatusLabel =
                    GetTransactionStatusLabel(
                        transaction.Status),

                ReferenceNumber =
                    transaction.ReferenceNumber?
                        .Trim(),

                TransferDate =
                    transaction.TransferDate,

                ReceiptImage =
                    transaction.ReceiptImage?
                        .Trim(),

                ReviewedAt =
                    transaction.ReviewedAt,

                ReviewedByUserId =
                    transaction.ReviewedByUserId,

                ReviewedByName =
                    FirstNonEmpty(
                        string.Empty,
                        transaction.ReviewedByUser?
                            .FullName,
                        transaction.ReviewedByUser?
                            .Username),

                RejectionReason =
                    transaction.RejectionReason?
                        .Trim(),

                CreatedAt =
                    transaction.CreatedAt,

                CreatedByUserId =
                    transaction.CreatedByUserId,

                CreatedByName =
                    FirstNonEmpty(
                        string.Empty,
                        transaction.CreatedByUser?
                            .FullName,
                        transaction.CreatedByUser?
                            .Username),

                Notes =
                    transaction.Notes?
                        .Trim()
            };
        }

        private string WebRootPath() =>
            string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _environment.WebRootPath;

        private async Task<string> SaveReceiptAsync(
            int ownerId,
            IFormFile receiptFile,
            CancellationToken cancellationToken)
        {
            if (receiptFile == null || receiptFile.Length <= 0)
            {
                throw new InvalidOperationException("أرفق سند التحويل.");
            }

            if (receiptFile.Length > MaximumReceiptSize)
            {
                throw new InvalidOperationException("حجم السند يجب ألا يتجاوز 5 ميجابايت.");
            }

            var extension = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
            if (!AllowedReceiptExtensions.Contains(extension))
            {
                throw new InvalidOperationException("الصيغة غير مدعومة. استخدم JPG أو PNG أو WEBP أو PDF.");
            }

            var now = DateTime.Now;
            var relativeDirectory = Path.Combine(
                "uploads", "finance", "topups", ownerId.ToString(),
                now.Year.ToString(), now.Month.ToString("00"));
            var physicalDirectory = Path.Combine(WebRootPath(), relativeDirectory);
            Directory.CreateDirectory(physicalDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(physicalDirectory, fileName);

            await using var stream = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await receiptFile.CopyToAsync(stream, cancellationToken);

            return "/" + Path.Combine(relativeDirectory, fileName).Replace("\\", "/");
        }

        private void DeleteStoredFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            try
            {
                var root = Path.GetFullPath(WebRootPath());
                var path = Path.GetFullPath(Path.Combine(
                    root,
                    relativePath.Trim().TrimStart('/', '\\')));

                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "تعذر حذف الملف المالي {Path}.", relativePath);
            }
        }

        public async Task<OwnerFinancialPageDto> GetFinancialPageAsync(
            int ownerId,
            int? selectedPlaceId = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureOwnerAccountsAsync(ownerId, cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var validPlaceId = await ValidatePlaceAsync(ownerId, selectedPlaceId, cancellationToken);

            var places = await OwnerPlaces(ownerId)
                .AsNoTracking()
                .Include(place => place.PlaceCommissionAccount)
                .OrderBy(place => place.PlaceNameAndDesc)
                .ToListAsync(cancellationToken);

            var model = new OwnerFinancialPageDto
            {
                Currency = GetCurrency(settings),
                SelectedPlaceId = validPlaceId,
                SelectedPlaceName = validPlaceId.HasValue
                    ? places.FirstOrDefault(place => place.PlaceId == validPlaceId.Value)
                        ?.PlaceNameAndDesc?.Trim() ?? "كل المنشآت"
                    : "كل المنشآت"
            };

            foreach (var place in places)
            {
                if (place.PlaceCommissionAccount == null)
                {
                    continue;
                }

                var account = MapAccount(place.PlaceCommissionAccount, settings);
                model.Places.Add(new OwnerFinancialPlaceOptionDto
                {
                    PlaceId = place.PlaceId,
                    PlaceName = account.PlaceName,
                    CoverImage = place.CoverImage?.Trim(),
                    AccountStatus = account.AccountStatus,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount,
                    IsSelected = validPlaceId == place.PlaceId
                });
            }

            var placeIds = places.Select(place => place.PlaceId).ToList();
            if (placeIds.Count > 0)
            {
                model.PendingTopUpsCount = await _context.CommissionTransactions
                    .AsNoTracking()
                    .CountAsync(transaction =>
                        placeIds.Contains(transaction.PlaceId) &&
                        transaction.TransactionType == FinancialTransactionTypes.TopUp &&
                        transaction.Status == FinancialTransactionStatuses.Pending,
                        cancellationToken);

                model.PendingRefundsCount = await _context.RefundRequests
                    .AsNoTracking()
                    .CountAsync(refund =>
                        placeIds.Contains(refund.PlaceId) &&
                        (refund.Status == FinancialRefundStatuses.Pending ||
                         refund.Status == FinancialRefundStatuses.Processing),
                        cancellationToken);
            }

            model.LowBalanceAccountsCount = model.Places.Count(place =>
                IsState(place.AccountStatus, FinancialAccountStatuses.LowBalance) ||
                IsState(place.AccountStatus, FinancialAccountStatuses.NeedsTopUp));

            model.SuspendedAccountsCount = model.Places.Count(place =>
                IsState(place.AccountStatus, FinancialAccountStatuses.Suspended));

            return model;
        }

        public async Task<FinancialOverviewTabDto> GetOverviewAsync(
            int ownerId,
            int? placeId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureOwnerAccountsAsync(ownerId, cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var validPlaceId = await ValidatePlaceAsync(ownerId, placeId, cancellationToken);

            var start = (fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var endExclusive = (toDate ?? DateTime.Today).Date.AddDays(1);
            if (endExclusive <= start)
            {
                endExclusive = start.AddDays(1);
            }

            var placeIds = await GetOwnerPlaceIdsAsync(ownerId, cancellationToken);
            if (validPlaceId.HasValue)
            {
                placeIds = placeIds.Where(id => id == validPlaceId.Value).ToList();
            }

            var model = new FinancialOverviewTabDto
            {
                Currency = GetCurrency(settings),
                SelectedPlaceId = validPlaceId,
                PeriodStart = start,
                PeriodEnd = endExclusive.AddDays(-1)
            };

            if (validPlaceId.HasValue)
            {
                model.SelectedPlaceName = await _context.Places
                    .AsNoTracking()
                    .Where(place => place.PlaceId == validPlaceId.Value)
                    .Select(place => place.PlaceNameAndDesc)
                    .FirstOrDefaultAsync(cancellationToken) ?? "منشأة غير معروفة";
            }

            if (placeIds.Count == 0)
            {
                return model;
            }

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    placeIds.Contains(booking.PlaceId) &&
                    booking.CreatedAt.HasValue &&
                    booking.CreatedAt.Value >= start &&
                    booking.CreatedAt.Value < endExclusive)
                .Select(booking => new
                {
                    booking.BookingId,
                    booking.TotalPrice,
                    booking.BookingState
                })
                .ToListAsync(cancellationToken);

            var records = await _context.FinancialRecords
                .AsNoTracking()
                .Where(record =>
                    placeIds.Contains(record.Booking.PlaceId) &&
                    record.Booking.CreatedAt.HasValue &&
                    record.Booking.CreatedAt.Value >= start &&
                    record.Booking.CreatedAt.Value < endExclusive)
                .Select(record => new
                {
                    record.DepositAmount,
                    record.AmountPaidOnline,
                    record.AmountToPayAtProperty,
                    record.AmountCollectedAtProperty,
                    record.PlatformCommission,
                    record.CommissionStatus
                })
                .ToListAsync(cancellationToken);

            var currentMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var nextMonthStart = currentMonthStart.AddMonths(1);

            var monthlyRecords = await _context.FinancialRecords
                .AsNoTracking()
                .Where(record =>
                    placeIds.Contains(record.Booking.PlaceId) &&
                    record.Booking.CreatedAt.HasValue &&
                    record.Booking.CreatedAt.Value >= previousMonthStart &&
                    record.Booking.CreatedAt.Value < nextMonthStart)
                .Select(record => new
                {
                    CreatedAt = record.Booking.CreatedAt!.Value,
                    Revenue = record.AmountPaidOnline + record.AmountCollectedAtProperty -
                        (record.CommissionStatus == FinancialCommissionStatuses.Charged
                            ? record.PlatformCommission
                            : 0m)
                })
                .ToListAsync(cancellationToken);

            model.Summary = new FinancialOverviewDto
            {
                TotalBookingsCount = bookings.Count,
                ConfirmedBookingsCount = bookings.Count(item => IsState(item.BookingState, "Confirmed")),
                CompletedBookingsCount = bookings.Count(item => IsState(item.BookingState, "Completed")),
                CancelledBookingsCount = bookings.Count(item =>
                    IsState(item.BookingState, "Cancelled") ||
                    IsState(item.BookingState, "Rejected") ||
                    IsState(item.BookingState, "Expired")),
                TotalBookingValue = bookings.Sum(item => item.TotalPrice),
                TotalDepositRequired = records.Sum(item => item.DepositAmount),
                TotalPaidOnline = records.Sum(item => item.AmountPaidOnline),
                TotalRemainingAtProperty = records.Sum(item => item.AmountToPayAtProperty),
                TotalCollectedAtProperty = records.Sum(item => item.AmountCollectedAtProperty),
                TotalCommissionCalculated = records.Sum(item => item.PlatformCommission),
                TotalCommissionHeld = records
                    .Where(item =>
                        IsState(item.CommissionStatus, FinancialCommissionStatuses.Held) ||
                        IsState(item.CommissionStatus, FinancialCommissionStatuses.Debt))
                    .Sum(item => item.PlatformCommission),
                TotalCommissionCharged = records
                    .Where(item => IsState(item.CommissionStatus, FinancialCommissionStatuses.Charged))
                    .Sum(item => item.PlatformCommission),
                TotalCommissionReleased = records
                    .Where(item => IsState(item.CommissionStatus, FinancialCommissionStatuses.Released))
                    .Sum(item => item.PlatformCommission),
                CurrentMonthRevenue = monthlyRecords
                    .Where(item => item.CreatedAt >= currentMonthStart)
                    .Sum(item => Math.Max(0m, item.Revenue)),
                PreviousMonthRevenue = monthlyRecords
                    .Where(item => item.CreatedAt >= previousMonthStart && item.CreatedAt < currentMonthStart)
                    .Sum(item => Math.Max(0m, item.Revenue))
            };

            var accounts = await _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Include(account => account.Place)
                .Where(account => placeIds.Contains(account.PlaceId))
                .OrderBy(account => account.Place.PlaceNameAndDesc)
                .ToListAsync(cancellationToken);

            model.Accounts = accounts.Select(account => MapAccount(account, settings)).ToList();
            model.Summary.AvailableGuaranteeBalance = model.Accounts.Sum(account => account.AvailableBalance);
            model.Summary.HeldGuaranteeBalance = model.Accounts.Sum(account => account.HeldBalance);
            model.Summary.TotalDebt = model.Accounts.Sum(account => account.DebtAmount);

            model.Summary.PendingTopUpsCount = await _context.CommissionTransactions
                .AsNoTracking()
                .CountAsync(transaction =>
                    placeIds.Contains(transaction.PlaceId) &&
                    transaction.TransactionType == FinancialTransactionTypes.TopUp &&
                    transaction.Status == FinancialTransactionStatuses.Pending,
                    cancellationToken);

            model.Summary.PendingRefundsCount = await _context.RefundRequests
                .AsNoTracking()
                .CountAsync(refund =>
                    placeIds.Contains(refund.PlaceId) &&
                    (refund.Status == FinancialRefundStatuses.Pending ||
                     refund.Status == FinancialRefundStatuses.Processing),
                    cancellationToken);

            var recent = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(transaction => transaction.Place)
                .Include(transaction => transaction.PlatformWallet)
                    .ThenInclude(wallet => wallet.Wallet)
                .Include(transaction => transaction.CreatedByUser)
                .Include(transaction => transaction.ReviewedByUser)
                .Where(transaction => placeIds.Contains(transaction.PlaceId))
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.CommissionTransactionId)
                .Take(10)
                .ToListAsync(cancellationToken);

            model.RecentTransactions = recent.Select(MapTransaction).ToList();
            return model;
        }

        public async Task<BookingAccountsTabDto> GetBookingAccountsAsync(
            int ownerId,
            BookingFinancialFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            /*
             * تجهيز حسابات ضمان منشآت المالك أولًا؛
             * حتى تظهر الأرصدة والحالات في قائمة المنشآت.
             */
            await EnsureOwnerAccountsAsync(
                ownerId,
                cancellationToken);

            var settings =
                await GetSettingsAsync(
                    cancellationToken);

            filter ??=
                new BookingFinancialFilterDto();

            filter.PageNumber =
                Math.Max(
                    1,
                    filter.PageNumber);

            filter.PageSize =
                Math.Clamp(
                    filter.PageSize,
                    1,
                    100);

            /*
             * لا نقبل PlaceId لمنشأة لا تتبع المالك.
             */
            filter.PlaceId =
                await ValidatePlaceAsync(
                    ownerId,
                    filter.PlaceId,
                    cancellationToken);

            /*
             * تصحيح الفترة إذا أُرسلت معكوسة.
             */
            if (
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue &&
                filter.FromDate.Value.Date >
                filter.ToDate.Value.Date
            )
            {
                var temporaryDate =
                    filter.FromDate;

                filter.FromDate =
                    filter.ToDate;

                filter.ToDate =
                    temporaryDate;
            }

            /*
             * جلب جميع منشآت المالك لقائمة الفلترة.
             * القائمة لا تتأثر بالصفحة الحالية أو نتائج البحث.
             */
            var ownerPlaces =
                await OwnerPlaces(ownerId)
                    .AsNoTracking()
                    .Include(place =>
                        place.PlaceCommissionAccount)
                    .Include(place =>
                        place.PlaceImages)
                    .AsSplitQuery()
                    .OrderBy(place =>
                        place.PlaceNameAndDesc)
                    .ToListAsync(
                        cancellationToken);

            var placeOptions =
                ownerPlaces
                    .Select(place =>
                    {
                        var account =
                            place.PlaceCommissionAccount;

                        var accountDto =
                            account == null
                                ? null
                                : MapAccount(
                                    account,
                                    settings);

                        var galleryImage =
                            place.PlaceImages?
                                .OrderBy(image =>
                                    image.ImageId)
                                .Select(image =>
                                    image.ImagePath)
                                .FirstOrDefault(path =>
                                    !string.IsNullOrWhiteSpace(
                                        path));

                        var placeImage =
                            FirstNonEmpty(
                                "/images/bg1.jpg",
                                place.CoverImage,
                                place.LogoImage,
                                galleryImage);

                        return new OwnerFinancialPlaceOptionDto
                        {
                            PlaceId =
                                place.PlaceId,

                            PlaceName =
                                FirstNonEmpty(
                                    "منشأة غير معروفة",
                                    place.PlaceNameAndDesc),

                            CoverImage =
                                placeImage,

                            AccountStatus =
                                accountDto?.AccountStatus ??
                                OwnerCommissionAccountStatuses
                                    .NeedsTopUp,

                            AvailableBalance =
                                accountDto?.AvailableBalance ??
                                0m,

                            HeldBalance =
                                accountDto?.HeldBalance ??
                                0m,

                            DebtAmount =
                                accountDto?.DebtAmount ??
                                0m,

                            IsSelected =
                                filter.PlaceId.HasValue &&
                                filter.PlaceId.Value ==
                                place.PlaceId
                        };
                    })
                    .ToList();

            /*
             * الاستعلام الأساسي.
             * جميع الفلاتر تطبق عليه قبل حساب الملخص والتقسيم.
             */
            IQueryable<Booking> query =
                _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.Place.OwnerId ==
                        ownerId);

            /*
             * فلتر المنشأة.
             */
            if (filter.PlaceId.HasValue)
            {
                var selectedPlaceId =
                    filter.PlaceId.Value;

                query =
                    query.Where(booking =>
                        booking.PlaceId ==
                        selectedPlaceId);
            }

            /*
             * فلتر بداية فترة الحجز.
             */
            if (filter.FromDate.HasValue)
            {
                var fromDate =
                    filter.FromDate.Value.Date;

                query =
                    query.Where(booking =>
                        booking.StartDate >=
                        fromDate);
            }

            /*
             * فلتر نهاية فترة الحجز.
             * AddDays(1) يجعل تاريخ النهاية مشمولًا كاملًا.
             */
            if (filter.ToDate.HasValue)
            {
                var toDateExclusive =
                    filter.ToDate.Value.Date
                        .AddDays(1);

                query =
                    query.Where(booking =>
                        booking.StartDate <
                        toDateExclusive);
            }

            /*
             * فلتر حالة الحجز.
             */
            if (
                !string.IsNullOrWhiteSpace(
                    filter.BookingState)
            )
            {
                var bookingState =
                    filter.BookingState.Trim();

                query =
                    query.Where(booking =>
                        booking.BookingState ==
                        bookingState);
            }

            /*
             * فلتر حالة الدفع.
             */
            if (
                !string.IsNullOrWhiteSpace(
                    filter.PaymentStatus)
            )
            {
                var paymentStatus =
                    filter.PaymentStatus.Trim();

                query =
                    query.Where(booking =>
                        booking.PaymentStatus ==
                        paymentStatus);
            }

            /*
             * فلتر حالة العمولة.
             */
            if (
                !string.IsNullOrWhiteSpace(
                    filter.CommissionStatus)
            )
            {
                var commissionStatus =
                    filter.CommissionStatus.Trim();

                query =
                    query.Where(booking =>
                        booking.FinancialRecord != null &&
                        booking.FinancialRecord
                            .CommissionStatus ==
                        commissionStatus);
            }

            /*
             * البحث يشمل:
             * رقم الحجز، رقم العميل، اسم العميل،
             * اسم المستخدم، الهاتف، البريد/وسيلة التواصل،
             * واسم المنشأة.
             */
            if (
                !string.IsNullOrWhiteSpace(
                    filter.Search)
            )
            {
                var search =
                    filter.Search.Trim();

                if (
                    int.TryParse(
                        search,
                        out var numericSearch)
                )
                {
                    query =
                        query.Where(booking =>
                            booking.BookingId ==
                                numericSearch ||

                            booking.CustomerId ==
                                numericSearch ||

                            booking.Place
                                .PlaceNameAndDesc
                                .Contains(search) ||

                            booking.Customer
                                .FullName
                                .Contains(search) ||

                            booking.Customer
                                .Username
                                .Contains(search) ||

                            (
                                booking.Customer
                                    .PhoneNumber != null &&

                                booking.Customer
                                    .PhoneNumber
                                    .Contains(search)
                            ) ||

                            (
                                booking.Customer
                                    .ContactInfo != null &&

                                booking.Customer
                                    .ContactInfo
                                    .Contains(search)
                            ));
                }
                else
                {
                    query =
                        query.Where(booking =>
                            booking.Place
                                .PlaceNameAndDesc
                                .Contains(search) ||

                            booking.Customer
                                .FullName
                                .Contains(search) ||

                            booking.Customer
                                .Username
                                .Contains(search) ||

                            (
                                booking.Customer
                                    .PhoneNumber != null &&

                                booking.Customer
                                    .PhoneNumber
                                    .Contains(search)
                            ) ||

                            (
                                booking.Customer
                                    .ContactInfo != null &&

                                booking.Customer
                                    .ContactInfo
                                    .Contains(search)
                            ));
                }
            }

            /*
             * حساب ملخص كل النتائج المفلترة.
             * الحساب يتم قبل Skip وTake، لذلك الملخص لا يقتصر
             * على الحجوزات الظاهرة في الصفحة الحالية.
             */
            /*
             * ملخص كل النتائج المفلترة.
             *
             * مهم:
             * نتجنب Math.Max داخل IQueryable لأن بعض إصدارات EF Core
             * قد تترجمه إلى GREATEST، وهذا قد يفشل على إصدارات/مستويات
             * توافق SQL Server الأقدم.
             *
             * لذلك نستخدم CASE عبر التعبير الشرطي، وهو مترجم بصورة
             * مستقرة إلى SQL Server ويحافظ على نفس الحسابات.
             */
            var summary =
                await query
                    .GroupBy(booking =>
                        1)
                    .Select(group =>
                        new BookingAccountsSummaryDto
                        {
                            BookingsCount =
                                group.Count(),

                            TotalOriginalBookingValue =
                                group.Sum(booking =>
                                    booking.OriginalTotalPrice > 0m
                                        ? booking.OriginalTotalPrice
                                        : booking.TotalPrice +
                                          booking.DiscountAmount),

                            TotalDiscountAmount =
                                group.Sum(booking =>
                                    booking.DiscountAmount > 0m
                                        ? booking.DiscountAmount
                                        : 0m),

                            DiscountedBookingsCount =
                                group.Count(booking =>
                                    booking.DiscountAmount > 0m),

                            TotalBookingValue =
                                group.Sum(booking =>
                                    booking.TotalPrice),

                            TotalDepositRequired =
                                group.Sum(booking =>
                                    booking.FinancialRecord != null
                                        ? booking.FinancialRecord
                                            .DepositAmount
                                        : 0m),

                            TotalPaidOnline =
                                group.Sum(booking =>
                                    booking.FinancialRecord != null
                                        ? booking.FinancialRecord
                                            .AmountPaidOnline
                                        : 0m),

                            TotalCollectedAtProperty =
                                group.Sum(booking =>
                                    booking.FinancialRecord != null
                                        ? booking.FinancialRecord
                                            .AmountCollectedAtProperty
                                        : 0m),

                            TotalRemainingAtProperty =
                                group.Sum(booking =>
                                    booking.FinancialRecord != null
                                        ? booking.FinancialRecord
                                            .AmountToPayAtProperty
                                        : booking.TotalPrice),

                            TotalCommission =
                                group.Sum(booking =>
                                    booking.FinancialRecord != null
                                        ? booking.FinancialRecord
                                            .PlatformCommission
                                        : 0m)
                        })
                    .FirstOrDefaultAsync(
                        cancellationToken)
                ??
                new BookingAccountsSummaryDto();

            var totalCount =
                summary.BookingsCount;

            /*
             * منع طلب صفحة أكبر من عدد الصفحات المتوفرة.
             */
            var totalPages =
                totalCount <= 0
                    ? 0
                    : (int)Math.Ceiling(
                        (double)totalCount /
                        filter.PageSize);

            if (totalPages == 0)
            {
                filter.PageNumber =
                    1;
            }
            else if (
                filter.PageNumber >
                totalPages
            )
            {
                filter.PageNumber =
                    totalPages;
            }

            /*
             * جلب بيانات الصفحة الحالية فقط بعد حساب الملخص.
             */
            var bookings =
                await query
                    .Include(booking =>
                        booking.Customer)
                    .Include(booking =>
                        booking.Place)
                        .ThenInclude(place =>
                            place.PlaceImages)
                    .Include(booking =>
                        booking.Room)
                    .Include(booking =>
                        booking.Package)
                    .Include(booking =>
                        booking.FinancialRecord)
                    .AsSplitQuery()
                    .OrderByDescending(booking =>
                        booking.CreatedAt)
                    .ThenByDescending(booking =>
                        booking.BookingId)
                    .Skip(
                        (
                            filter.PageNumber -
                            1
                        ) *
                        filter.PageSize)
                    .Take(
                        filter.PageSize)
                    .ToListAsync(
                        cancellationToken);

            /*
             * تحويل الحجوزات إلى DTO.
             */
            var items =
                bookings
                    .Select(booking =>
                    {
                        var financial =
                            booking.FinancialRecord;

                        var roomName =
                            BuildRoomName(
                                booking.Room);

                        var packageName =
                            BuildPackageName(
                                booking.Package);

                        var contactInfo =
                            booking.Customer?
                                .ContactInfo?
                                .Trim();

                        var contactInfoIsEmail =
                            !string.IsNullOrWhiteSpace(
                                contactInfo) &&
                            contactInfo.Contains(
                                '@');

                        var customerEmail =
                            contactInfoIsEmail
                                ? contactInfo
                                : null;

                        var customerPhone =
                            FirstNonEmpty(
                                string.Empty,
                                booking.Customer?
                                    .PhoneNumber,
                                contactInfoIsEmail
                                    ? null
                                    : contactInfo);

                        var galleryImage =
                            booking.Place?
                                .PlaceImages?
                                .OrderBy(image =>
                                    image.ImageId)
                                .Select(image =>
                                    image.ImagePath)
                                .FirstOrDefault(path =>
                                    !string.IsNullOrWhiteSpace(
                                        path));

                        var placeImage =
                            FirstNonEmpty(
                                "/images/bg1.jpg",
                                booking.Place?
                                    .CoverImage,
                                booking.Place?
                                    .LogoImage,
                                galleryImage);

                        return new BookingFinancialItemDto
                        {
                            RecordId =
                                financial?.RecordId ??
                                0,

                            BookingId =
                                booking.BookingId,

                            CustomerId =
                                booking.CustomerId,

                            CustomerName =
                                FirstNonEmpty(
                                    "عميل",
                                    booking.Customer?
                                        .FullName,
                                    booking.Customer?
                                        .Username),

                            CustomerPhone =
                                string.IsNullOrWhiteSpace(
                                    customerPhone)
                                    ? null
                                    : customerPhone,

                            CustomerEmail =
                                customerEmail,

                            PlaceId =
                                booking.PlaceId,

                            PlaceName =
                                FirstNonEmpty(
                                    "منشأة غير معروفة",
                                    booking.Place?
                                        .PlaceNameAndDesc),

                            PlaceImageUrl =
                                placeImage,

                            RoomId =
                                booking.RoomId,

                            RoomName =
                                string.IsNullOrWhiteSpace(
                                    roomName)
                                    ? null
                                    : roomName,

                            PackageId =
                                booking.PackageId,

                            PackageName =
                                string.IsNullOrWhiteSpace(
                                    packageName)
                                    ? null
                                    : packageName,

                            StartDate =
                                booking.StartDate,

                            EndDate =
                                booking.EndDate,

                            CreatedAt =
                                booking.CreatedAt,

                            UpdatedAt =
                                financial?.UpdatedAt ??
                                booking.CreatedAt ??
                                DateTime.Now,

                            BookingState =
                                booking.BookingState?
                                    .Trim() ??
                                "Pending",

                            PaymentStatus =
                                booking.PaymentStatus?
                                    .Trim() ??
                                "Unpaid",

                            RefundStatus =
                                booking.RefundStatus?
                                    .Trim() ??
                                "NotApplicable",

                            PaymentMethod =
                                booking.PaymentMethod?
                                    .Trim(),

                            OnlinePaymentType =
                                financial?
                                    .OnlinePaymentType?
                                    .Trim(),

                            TotalPrice =
                                booking.TotalPrice,

                            PromotionId =
                                booking.PromotionId,

                            OriginalTotalPrice =
                                booking.OriginalTotalPrice > 0m
                                    ? booking.OriginalTotalPrice
                                    : booking.TotalPrice + booking.DiscountAmount,

                            DiscountAmount =
                                booking.DiscountAmount > 0m
                                    ? booking.DiscountAmount
                                    : 0m,

                            PromotionTitleSnapshot =
                                booking.PromotionTitleSnapshot?.Trim(),

                            PromotionCodeSnapshot =
                                booking.PromotionCodeSnapshot?.Trim(),

                            DiscountTypeSnapshot =
                                booking.DiscountTypeSnapshot?.Trim(),

                            DiscountValueSnapshot =
                                booking.DiscountValueSnapshot,

                            DepositAmount =
                                financial?
                                    .DepositAmount ??
                                0m,

                            AmountPaidOnline =
                                financial?
                                    .AmountPaidOnline ??
                                0m,

                            AmountToPayAtProperty =
                                financial?
                                    .AmountToPayAtProperty ??
                                booking.TotalPrice,

                            AmountCollectedAtProperty =
                                financial?
                                    .AmountCollectedAtProperty ??
                                0m,

                            IsPaymentConfirmed =
                                IsBookingPaymentConfirmed(booking),

                            TransferReferenceNumber =
                                financial?
                                    .TransferReferenceNumber?
                                    .Trim(),

                            PaymentNotificationImage =
                                booking
                                    .PaymentNotificationImage?
                                    .Trim(),

                            CommissionRateSnapshot =
                                financial?
                                    .CommissionRateSnapshot ??
                                0m,

                            CommissionBaseAmount =
                                financial?
                                    .CommissionBaseAmount ??
                                booking.TotalPrice,

                            PlatformCommission =
                                financial?
                                    .PlatformCommission ??
                                0m,

                            CommissionStatus =
                                financial?
                                    .CommissionStatus?
                                    .Trim() ??
                                OwnerBookingCommissionStatuses
                                    .Calculated,

                            CommissionPaymentStatus =
                                financial?
                                    .CommissionStatus?
                                    .Trim() ??
                                FinancialCommissionStatuses.Calculated,

                            CommissionHeldAt =
                                financial?
                                    .CommissionHeldAt,

                            CommissionChargedAt =
                                financial?
                                    .CommissionChargedAt,

                            CommissionReleasedAt =
                                financial?
                                    .CommissionReleasedAt,

                            CollectedAt =
                                financial?
                                    .CollectedAt,

                            SettledAt =
                                GetFinancialSettlementDate(booking, financial)
                        };
                    })
                    .ToList();

            /*
             * النموذج النهائي للتبويب.
             */
            return new BookingAccountsTabDto
            {
                Currency =
                    GetCurrency(
                        settings),

                Filter =
                    filter,

                Places =
                    placeOptions,

                Summary =
                    summary,

                Bookings =
                    items,

                TotalCount =
                    totalCount,

                PageNumber =
                    filter.PageNumber,

                PageSize =
                    filter.PageSize
            };
        }

        public async Task<CommissionGuaranteeTabDto> GetCommissionGuaranteeAsync(
            int ownerId,
            CancellationToken cancellationToken = default)
        {
            await EnsureOwnerAccountsAsync(ownerId, cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var accounts = await _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Include(account => account.Place)
                .Where(account => account.Place.OwnerId == ownerId)
                .OrderBy(account => account.Place.PlaceNameAndDesc)
                .ToListAsync(cancellationToken);

            return new CommissionGuaranteeTabDto
            {
                Currency = GetCurrency(settings),
                DefaultLowBalanceThreshold = settings?.DefaultLowBalanceThreshold ?? 0m,
                DefaultAllowNegativeBalance = settings?.AllowNegativeCommissionBalance ?? false,
                DefaultDebtLimit = settings?.DefaultCommissionDebtLimit ?? 0m,
                SuspendBookingWhenBalanceInsufficient = settings?.SuspendBookingWhenBalanceInsufficient ?? true,
                HidePlaceWhenCommissionSuspended = settings?.HidePlaceWhenCommissionSuspended ?? false,
                Accounts = accounts.Select(account => MapAccount(account, settings)).ToList()
            };
        }

        public async Task<TopUpBalanceTabDto> GetTopUpBalanceAsync(
            int ownerId,
            int? selectedPlaceId = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureOwnerAccountsAsync(ownerId, cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var validPlaceId = await ValidatePlaceAsync(ownerId, selectedPlaceId, cancellationToken);

            var accounts = await _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Include(account => account.Place)
                .Where(account => account.Place.OwnerId == ownerId)
                .OrderBy(account => account.Place.PlaceNameAndDesc)
                .ToListAsync(cancellationToken);

            var platformWallets = await _context.PlatformWallets
                .AsNoTracking()
                .Include(wallet => wallet.Wallet)
                .Where(wallet => wallet.IsActive == true && wallet.Wallet.IsActive == true)
                .OrderByDescending(wallet => wallet.IsPrimary)
                .ThenBy(wallet => wallet.Wallet.WalletName)
                .ThenBy(wallet => wallet.AccountNumber)
                .ToListAsync(cancellationToken);

            var recentQuery =
     _context.CommissionTransactions
         .AsNoTracking()
         .Include(transaction =>
             transaction.Place)
         .Include(transaction =>
             transaction.PlatformWallet)
             .ThenInclude(wallet =>
                 wallet.Wallet)
         .Include(transaction =>
             transaction.CreatedByUser)
         .Include(transaction =>
             transaction.ReviewedByUser)
         .Where(transaction =>
             transaction.Place.OwnerId ==
                 ownerId &&
             transaction.TransactionType ==
                 FinancialTransactionTypes.TopUp);

            if (validPlaceId.HasValue)
            {
                recentQuery =
                    recentQuery.Where(transaction =>
                        transaction.PlaceId ==
                            validPlaceId.Value);
            }

            var recent =
                await recentQuery
                    .OrderByDescending(transaction =>
                        transaction.CreatedAt)
                    .ThenByDescending(transaction =>
                        transaction.CommissionTransactionId)
                    .Take(15)
                    .ToListAsync(
                        cancellationToken);

            return new TopUpBalanceTabDto
            {
                Currency = GetCurrency(settings),
                MinimumTopUpAmount = settings?.MinimumCommissionTopUp ?? 0m,
                SelectedPlaceId = validPlaceId,
                Accounts = accounts.Select(account => MapAccount(account, settings)).ToList(),
                PlatformWallets = platformWallets.Select(wallet => new PlatformWalletOptionDto
                {
                    PlatformWalletId = wallet.PlatformWalletId,
                    WalletId = wallet.WalletId,
                    WalletName = FirstNonEmpty("محفظة", wallet.Wallet?.WalletName),
                    LogoPath = wallet.Wallet?.LogoPath?.Trim(),
                    AccountNumber = wallet.AccountNumber?.Trim() ?? string.Empty,
                    BeneficiaryName = wallet.BeneficiaryName?.Trim() ?? string.Empty,
                    IsPrimary = wallet.IsPrimary,
                    Instructions = wallet.Instructions?.Trim()
                }).ToList(),
                RecentTopUps = recent.Select(MapTransaction).ToList()
            };
        }

        public async Task<FinanceOperationResult> SubmitTopUpRequestAsync(
            int ownerId,
            TopUpRequestDto request,
            IFormFile receiptFile,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return FinanceOperationResult.Failed("بيانات الطلب غير مكتملة.");
            }

            if (!await OwnerOwnsPlaceAsync(ownerId, request.PlaceId, cancellationToken))
            {
                return FinanceOperationResult.Failed("المنشأة غير موجودة أو لا تملك صلاحية شحن رصيدها.");
            }

            var settings = await GetSettingsAsync(cancellationToken);
            var minimum = Math.Max(0m, settings?.MinimumCommissionTopUp ?? 0m);

            if (request.Amount <= 0m)
            {
                return FinanceOperationResult.Failed("مبلغ الشحن يجب أن يكون أكبر من صفر.");
            }

            if (request.Amount < minimum)
            {
                return FinanceOperationResult.Failed(
                    $"الحد الأدنى للشحن هو {minimum:N2} {GetCurrency(settings)}.");
            }

            if (string.IsNullOrWhiteSpace(request.ReferenceNumber))
            {
                return FinanceOperationResult.Failed("أدخل الرقم المرجعي للتحويل.");
            }

            if (request.TransferDate > DateTime.Now.AddMinutes(10))
            {
                return FinanceOperationResult.Failed("تاريخ التحويل لا يمكن أن يكون في المستقبل.");
            }

            var platformWalletExists = await _context.PlatformWallets
                .AsNoTracking()
                .AnyAsync(wallet =>
                    wallet.PlatformWalletId == request.PlatformWalletId &&
                    wallet.IsActive == true &&
                    wallet.Wallet.IsActive == true,
                    cancellationToken);

            if (!platformWalletExists)
            {
                return FinanceOperationResult.Failed("حساب المنصة غير موجود أو غير مفعل.");
            }

            var reference = request.ReferenceNumber.Trim();
            var duplicate = await _context.CommissionTransactions
                .AsNoTracking()
                .AnyAsync(transaction =>
                    transaction.TransactionType == FinancialTransactionTypes.TopUp &&
                    transaction.PlatformWalletId == request.PlatformWalletId &&
                    transaction.ReferenceNumber == reference,
                    cancellationToken);

            if (duplicate)
            {
                return FinanceOperationResult.Failed(
                    "الرقم المرجعي مستخدم مسبقًا مع حساب المنصة المحدد. استخدم رقم حوالة مختلفًا.");
            }

            string? receiptPath = null;
            try
            {
                receiptPath = await SaveReceiptAsync(ownerId, receiptFile, cancellationToken);
                var account = await _context.PlaceCommissionAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.PlaceId == request.PlaceId, cancellationToken);

                var transaction = new CommissionTransaction
                {
                    PlaceId = request.PlaceId,
                    PlatformWalletId = request.PlatformWalletId,
                    TransactionType = FinancialTransactionTypes.TopUp,
                    Amount = Math.Round(request.Amount, 2),
                    AvailableBalanceBefore = account?.AvailableBalance ?? 0m,
                    AvailableBalanceAfter = account?.AvailableBalance ?? 0m,
                    HeldBalanceBefore = account?.HeldBalance ?? 0m,
                    HeldBalanceAfter = account?.HeldBalance ?? 0m,
                    DebtBefore = account?.DebtAmount ?? 0m,
                    DebtAfter = account?.DebtAmount ?? 0m,
                    Status = FinancialTransactionStatuses.Pending,
                    ReferenceNumber = reference,
                    TransferDate = request.TransferDate,
                    ReceiptImage = receiptPath,
                    CreatedAt = DateTime.Now,
                    CreatedByUserId = ownerId,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
                };

                await using var databaseTransaction =
                    await _context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    _context.CommissionTransactions.Add(transaction);
                    await _context.SaveChangesAsync(cancellationToken);

                    var placeName = await _context.Places
                        .AsNoTracking()
                        .Where(place => place.PlaceId == request.PlaceId)
                        .Select(place => place.PlaceNameAndDesc)
                        .FirstOrDefaultAsync(cancellationToken);

                    await _notificationService.QueueForAdminPermissionAsync(
                        AdminPermissionKeys.Finance.ReviewTopUps,
                        new CreateNotificationDto
                        {
                            NotificationTitle = "طلب شحن جديد يحتاج مراجعة",
                            NotificationText =
                                $"أرسل مالك {placeName ?? "المنشأة"} طلب شحن بقيمة {transaction.Amount:N2} ويحتاج مراجعة إثبات التحويل.",
                            NotificationType = NotificationTypes.AdminFinanceActionRequired,
                            TargetUrl =
                                $"/Admin/Financials/Manage?financeTab=topups&focusTopUp={transaction.CommissionTransactionId}",
                            ReferenceType = NotificationReferenceTypes.CommissionTransaction,
                            ReferenceId = transaction.CommissionTransactionId
                        },
                        cancellationToken);

                    await _context.SaveChangesAsync(cancellationToken);
                    await databaseTransaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    throw;
                }

                return FinanceOperationResult.Succeeded(
                    "تم إرسال طلب الشحن، وسيضاف الرصيد بعد اعتماد الإدارة.",
                    transaction.CommissionTransactionId,
                    transaction.ReferenceNumber);
            }
            catch (InvalidOperationException exception)
            {
                DeleteStoredFile(receiptPath);
                return FinanceOperationResult.Failed(exception.Message);
            }
            catch (Exception exception)
            {
                DeleteStoredFile(receiptPath);
                _logger.LogError(
                    exception,
                    "فشل إرسال طلب شحن للمالك {OwnerId} والمنشأة {PlaceId}.",
                    ownerId,
                    request.PlaceId);
                return FinanceOperationResult.Failed("تعذر إرسال طلب الشحن حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> CancelPendingTopUpAsync(
            int ownerId,
            int commissionTransactionId,
            CancellationToken cancellationToken = default)
        {
            var transaction = await _context.CommissionTransactions
                .Include(item => item.Place)
                .FirstOrDefaultAsync(item =>
                    item.CommissionTransactionId == commissionTransactionId &&
                    item.Place.OwnerId == ownerId,
                    cancellationToken);

            if (transaction == null)
            {
                return FinanceOperationResult.Failed("طلب الشحن غير موجود أو لا تملك صلاحية تعديله.");
            }

            if (!IsState(transaction.TransactionType, FinancialTransactionTypes.TopUp) ||
                !IsState(transaction.Status, FinancialTransactionStatuses.Pending))
            {
                return FinanceOperationResult.Failed("لا يمكن إلغاء الطلب بعد مراجعته أو تنفيذه.");
            }

            transaction.Status = FinancialTransactionStatuses.Cancelled;
            transaction.Notes = string.IsNullOrWhiteSpace(transaction.Notes)
                ? "أُلغي طلب الشحن بواسطة المالك."
                : transaction.Notes.Trim() + Environment.NewLine + "أُلغي طلب الشحن بواسطة المالك.";

            await _context.SaveChangesAsync(cancellationToken);
            return FinanceOperationResult.Succeeded(
                "تم إلغاء طلب الشحن.",
                transaction.CommissionTransactionId,
                transaction.ReferenceNumber);
        }

        public async Task<FinanceOperationResult> ApproveTopUpAsync(
            int commissionTransactionId,
            int adminUserId,
            string? reviewNote = null,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return FinanceOperationResult.Failed("رقم طلب الشحن غير صحيح.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed("تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                var topUp = await _context.CommissionTransactions
                    .Include(item => item.Place)
                    .FirstOrDefaultAsync(item =>
                        item.CommissionTransactionId == commissionTransactionId,
                        cancellationToken);

                if (topUp == null ||
                    !IsState(
                        topUp.TransactionType,
                        FinancialTransactionTypes.TopUp))
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed("طلب الشحن غير موجود.");
                }

                if (IsState(
                    topUp.Status,
                    FinancialTransactionStatuses.Completed))
                {
                    await databaseTransaction.CommitAsync(cancellationToken);

                    return new FinanceOperationResult
                    {
                        Success = true,
                        Message = "طلب الشحن معتمد مسبقًا.",
                        EntityId = topUp.CommissionTransactionId,
                        ReferenceNumber = topUp.ReferenceNumber,
                        AvailableBalance = topUp.AvailableBalanceAfter,
                        HeldBalance = topUp.HeldBalanceAfter,
                        DebtAmount = topUp.DebtAfter
                    };
                }

                if (!IsState(
                    topUp.Status,
                    FinancialTransactionStatuses.Pending))
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يمكن اعتماد طلب شحن تمت مراجعته أو إلغاؤه.");
                }

                if (topUp.Amount <= 0m)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed("مبلغ طلب الشحن غير صحيح.");
                }

                if (!topUp.PlatformWalletId.HasValue)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "طلب الشحن غير مرتبط بحساب منصة.");
                }

                var walletExists = await _context.PlatformWallets
                    .AsNoTracking()
                    .AnyAsync(wallet =>
                        wallet.PlatformWalletId == topUp.PlatformWalletId.Value,
                        cancellationToken);

                if (!walletExists)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "حساب المنصة المرتبط بالطلب غير موجود.");
                }

                var normalizedReference =
                    topUp.ReferenceNumber?.Trim();

                if (!string.IsNullOrWhiteSpace(normalizedReference))
                {
                    var duplicateApprovedReference =
                        await _context.CommissionTransactions
                            .AsNoTracking()
                            .AnyAsync(item =>
                                item.CommissionTransactionId !=
                                    topUp.CommissionTransactionId &&
                                item.TransactionType ==
                                    FinancialTransactionTypes.TopUp &&
                                item.Status ==
                                    FinancialTransactionStatuses.Completed &&
                                item.PlatformWalletId ==
                                    topUp.PlatformWalletId &&
                                item.ReferenceNumber ==
                                    normalizedReference,
                                cancellationToken);

                    if (duplicateApprovedReference)
                    {
                        await databaseTransaction.RollbackAsync(cancellationToken);
                        return FinanceOperationResult.Failed(
                            "الرقم المرجعي مستخدم في طلب شحن معتمد سابقًا.");
                    }
                }

                var settings = await GetSettingsAsync(cancellationToken);

                var minimumTopUp = Math.Round(
                    Math.Max(
                        0m,
                        settings?.MinimumCommissionTopUp ?? 0m),
                    2);

                if (minimumTopUp > 0m && topUp.Amount < minimumTopUp)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        $"مبلغ الشحن أقل من الحد الأدنى المعتمد وهو {minimumTopUp:N2}.");
                }

                var account = await GetOrCreateAccountAsync(
                    topUp.PlaceId,
                    settings,
                    cancellationToken);

                var availableBefore =
                    Math.Round(Math.Max(0m, account.AvailableBalance), 2);

                var heldBefore =
                    Math.Round(Math.Max(0m, account.HeldBalance), 2);

                var debtBefore =
                    Math.Round(Math.Max(0m, account.DebtAmount), 2);

                var approvedAmount =
                    Math.Round(Math.Max(0m, topUp.Amount), 2);

                var debtPaymentAmount =
                    Math.Round(
                        Math.Min(debtBefore, approvedAmount),
                        2);

                var creditedAmount =
                    Math.Round(
                        approvedAmount - debtPaymentAmount,
                        2);

                var now = DateTime.Now;

                if (debtPaymentAmount > 0m)
                {
                    account.DebtAmount =
                        Math.Round(
                            Math.Max(0m, debtBefore - debtPaymentAmount),
                            2);

                    _context.CommissionTransactions.Add(
                        CreateMovement(
                            account,
                            topUp.PlaceId,
                            null,
                            FinancialTransactionTypes.DebtPayment,
                            debtPaymentAmount,
                            availableBefore,
                            heldBefore,
                            debtBefore,
                            adminUserId,
                            $"سداد دين من طلب الشحن رقم {topUp.CommissionTransactionId}."));
                }

                if (creditedAmount > 0m)
                {
                    account.AvailableBalance =
                        Math.Round(
                            availableBefore + creditedAmount,
                            2);
                }

                account.LastTopUpAt = now;
                RefreshAccountStatus(account, settings);

                topUp.AvailableBalanceBefore = availableBefore;
                topUp.AvailableBalanceAfter = account.AvailableBalance;
                topUp.HeldBalanceBefore = heldBefore;
                topUp.HeldBalanceAfter = account.HeldBalance;
                topUp.DebtBefore = debtBefore;
                topUp.DebtAfter = account.DebtAmount;
                topUp.Status = FinancialTransactionStatuses.Completed;
                topUp.ReferenceNumber = normalizedReference;
                topUp.ReviewedAt = now;
                topUp.ReviewedByUserId = adminUserId;
                topUp.RejectionReason = null;

                var approvalDetails =
                    $"اعتماد طلب الشحن. سداد الدين: {debtPaymentAmount:N2}، " +
                    $"المضاف للرصيد: {creditedAmount:N2}.";

                if (!string.IsNullOrWhiteSpace(reviewNote))
                {
                    approvalDetails +=
                        Environment.NewLine +
                        reviewNote.Trim();
                }

                topUp.Notes = string.IsNullOrWhiteSpace(topUp.Notes)
                    ? approvalDetails
                    : topUp.Notes.Trim() +
                      Environment.NewLine +
                      approvalDetails;

                await _context.SaveChangesAsync(cancellationToken);
                await databaseTransaction.CommitAsync(cancellationToken);

                return new FinanceOperationResult
                {
                    Success = true,
                    Message = debtPaymentAmount > 0m
                        ? "تم اعتماد الشحن، وسداد الدين أولًا، وإضافة المتبقي إلى الرصيد."
                        : "تم اعتماد الشحن وإضافة المبلغ إلى رصيد الضمان.",
                    EntityId = topUp.CommissionTransactionId,
                    ReferenceNumber = topUp.ReferenceNumber,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount
                };
            }
            catch (Exception exception)
            {
                await databaseTransaction.RollbackAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "فشل اعتماد طلب شحن رصيد الضمان {TransactionId} بواسطة الأدمن {AdminUserId}.",
                    commissionTransactionId,
                    adminUserId);

                return FinanceOperationResult.Failed(
                    "تعذر اعتماد طلب الشحن حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> RejectTopUpAsync(
            int commissionTransactionId,
            int adminUserId,
            string rejectionReason,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return FinanceOperationResult.Failed("رقم طلب الشحن غير صحيح.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed("تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                return FinanceOperationResult.Failed("اكتب سبب رفض طلب الشحن.");
            }

            var normalizedReason =
                rejectionReason.Trim();

            if (normalizedReason.Length > 500)
            {
                return FinanceOperationResult.Failed(
                    "سبب الرفض يجب ألا يتجاوز 500 حرف.");
            }

            await using var databaseTransaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                var topUp = await _context.CommissionTransactions
                    .FirstOrDefaultAsync(item =>
                        item.CommissionTransactionId == commissionTransactionId,
                        cancellationToken);

                if (topUp == null ||
                    !IsState(
                        topUp.TransactionType,
                        FinancialTransactionTypes.TopUp))
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed("طلب الشحن غير موجود.");
                }

                if (!IsState(
                    topUp.Status,
                    FinancialTransactionStatuses.Pending))
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يمكن رفض طلب شحن تمت مراجعته أو إلغاؤه.");
                }

                topUp.Status = FinancialTransactionStatuses.Rejected;
                topUp.ReviewedAt = DateTime.Now;
                topUp.ReviewedByUserId = adminUserId;
                topUp.RejectionReason = normalizedReason;

                var rejectionNote =
                    "رُفض طلب الشحن بواسطة الإدارة: " +
                    normalizedReason;

                topUp.Notes = string.IsNullOrWhiteSpace(topUp.Notes)
                    ? rejectionNote
                    : topUp.Notes.Trim() +
                      Environment.NewLine +
                      rejectionNote;

                await _context.SaveChangesAsync(cancellationToken);
                await databaseTransaction.CommitAsync(cancellationToken);

                return FinanceOperationResult.Succeeded(
                    "تم رفض طلب الشحن وتسجيل السبب.",
                    topUp.CommissionTransactionId,
                    topUp.ReferenceNumber);
            }
            catch (Exception exception)
            {
                await databaseTransaction.RollbackAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "فشل رفض طلب شحن رصيد الضمان {TransactionId} بواسطة الأدمن {AdminUserId}.",
                    commissionTransactionId,
                    adminUserId);

                return FinanceOperationResult.Failed(
                    "تعذر رفض طلب الشحن حاليًا.");
            }
        }

        public async Task<TransactionsTabDto> GetTransactionsAsync(
            int ownerId,
            CommissionTransactionFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??=
                new CommissionTransactionFilterDto();

            filter.PageNumber =
                Math.Max(
                    1,
                    filter.PageNumber);

            if (
                !AllowedTransactionPageSizes.Contains(
                    filter.PageSize)
            )
            {
                filter.PageSize =
                    25;
            }

            filter.PlaceId =
                await ValidatePlaceAsync(
                    ownerId,
                    filter.PlaceId,
                    cancellationToken);

            if (
                filter.BookingId.HasValue &&
                filter.BookingId.Value <= 0
            )
            {
                filter.BookingId =
                    null;
            }

            filter.TransactionType =
                NormalizeAllowedValue(
                    filter.TransactionType,
                    FinancialTransactionTypes.All);

            filter.Status =
                NormalizeAllowedValue(
                    filter.Status,
                    FinancialTransactionStatuses.All);

            filter.Search =
                string.IsNullOrWhiteSpace(
                    filter.Search)
                    ? null
                    : filter.Search.Trim();

            if (
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue &&
                filter.FromDate.Value.Date >
                filter.ToDate.Value.Date
            )
            {
                var temporaryDate =
                    filter.FromDate;

                filter.FromDate =
                    filter.ToDate;

                filter.ToDate =
                    temporaryDate;
            }

            var settings =
                await GetSettingsAsync(
                    cancellationToken);

            var selectedPlaceName =
                "كل المنشآت";

            if (filter.PlaceId.HasValue)
            {
                selectedPlaceName =
                    await _context.Places
                        .AsNoTracking()
                        .Where(place =>
                            place.PlaceId ==
                                filter.PlaceId.Value &&
                            place.OwnerId ==
                                ownerId)
                        .Select(place =>
                            place.PlaceNameAndDesc)
                        .FirstOrDefaultAsync(
                            cancellationToken)
                    ??
                    "منشأة غير معروفة";
            }

            IQueryable<CommissionTransaction> query =
                _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        transaction.Place.OwnerId ==
                        ownerId);

            if (filter.PlaceId.HasValue)
            {
                var selectedPlaceId =
                    filter.PlaceId.Value;

                query =
                    query.Where(transaction =>
                        transaction.PlaceId ==
                        selectedPlaceId);
            }

            if (filter.BookingId.HasValue)
            {
                var bookingId =
                    filter.BookingId.Value;

                query =
                    query.Where(transaction =>
                        transaction.BookingId ==
                        bookingId);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    filter.TransactionType)
            )
            {
                var transactionType =
                    filter.TransactionType;

                query =
                    query.Where(transaction =>
                        transaction.TransactionType ==
                        transactionType);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    filter.Status)
            )
            {
                var status =
                    filter.Status;

                query =
                    query.Where(transaction =>
                        transaction.Status ==
                        status);
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate =
                    filter.FromDate.Value.Date;

                query =
                    query.Where(transaction =>
                        transaction.CreatedAt.HasValue &&
                        transaction.CreatedAt.Value >=
                        fromDate);
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive =
                    filter.ToDate.Value.Date
                        .AddDays(1);

                query =
                    query.Where(transaction =>
                        transaction.CreatedAt.HasValue &&
                        transaction.CreatedAt.Value <
                        toDateExclusive);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    filter.Search)
            )
            {
                var search =
                    filter.Search;

                if (
                    int.TryParse(
                        search,
                        out var numericSearch)
                )
                {
                    query =
                        query.Where(transaction =>
                            transaction.CommissionTransactionId ==
                                numericSearch ||

                            transaction.BookingId ==
                                numericSearch ||

                            transaction.Place
                                .PlaceNameAndDesc
                                .Contains(search) ||

                            (
                                transaction.ReferenceNumber != null &&
                                transaction.ReferenceNumber
                                    .Contains(search)
                            ) ||

                            (
                                transaction.TransactionType != null &&
                                transaction.TransactionType
                                    .Contains(search)
                            ) ||

                            (
                                transaction.Status != null &&
                                transaction.Status
                                    .Contains(search)
                            ));
                }
                else
                {
                    query =
                        query.Where(transaction =>
                            transaction.Place
                                .PlaceNameAndDesc
                                .Contains(search) ||

                            (
                                transaction.ReferenceNumber != null &&
                                transaction.ReferenceNumber
                                    .Contains(search)
                            ) ||

                            (
                                transaction.TransactionType != null &&
                                transaction.TransactionType
                                    .Contains(search)
                            ) ||

                            (
                                transaction.Status != null &&
                                transaction.Status
                                    .Contains(search)
                            ) ||

                            (
                                transaction.PlatformWallet != null &&
                                transaction.PlatformWallet
                                    .Wallet != null &&
                                transaction.PlatformWallet
                                    .Wallet
                                    .WalletName
                                    .Contains(search)
                            ) ||

                            (
                                transaction.CreatedByUser != null &&
                                (
                                    transaction.CreatedByUser
                                        .FullName
                                        .Contains(search) ||

                                    transaction.CreatedByUser
                                        .Username
                                        .Contains(search)
                                )
                            ) ||

                            (
                                transaction.ReviewedByUser != null &&
                                (
                                    transaction.ReviewedByUser
                                        .FullName
                                        .Contains(search) ||

                                    transaction.ReviewedByUser
                                        .Username
                                        .Contains(search)
                                )
                            ));
                }
            }

            var summary =
                await query
                    .GroupBy(transaction =>
                        1)
                    .Select(group =>
                        new CommissionTransactionsSummaryDto
                        {
                            RecordsCount =
                                group.Count(),

                            PendingCount =
                                group.Count(transaction =>
                                    transaction.Status ==
                                    FinancialTransactionStatuses.Pending),

                            CompletedCount =
                                group.Count(transaction =>
                                    transaction.Status ==
                                    FinancialTransactionStatuses.Completed),

                            RejectedCount =
                                group.Count(transaction =>
                                    transaction.Status ==
                                    FinancialTransactionStatuses.Rejected),

                            CompletedTopUpsAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.TopUp &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Completed)
                                    .Sum(transaction =>
                                        (decimal?)transaction.Amount)
                                ??
                                0m,

                            PendingTopUpsAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.TopUp &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Pending)
                                    .Sum(transaction =>
                                        (decimal?)transaction.Amount)
                                ??
                                0m,

                            CommissionHeldAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.CommissionHold &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Completed)
                                    .Sum(transaction =>
                                        (decimal?)transaction.Amount)
                                ??
                                0m,

                            CommissionChargedAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.CommissionCharge &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Completed)
                                    .Sum(transaction =>
                                        (decimal?)transaction.Amount)
                                ??
                                0m,

                            CommissionReleasedAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.CommissionRelease &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Completed)
                                    .Sum(transaction =>
                                        (decimal?)transaction.Amount)
                                ??
                                0m,

                            DebtCreatedAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.Debt &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Completed)
                                    .Sum(transaction =>
                                        (decimal?)transaction.Amount)
                                ??
                                0m,

                            DebtPaidAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.DebtPayment &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Completed)
                                    .Sum(transaction =>
                                        (decimal?)transaction.Amount)
                                ??
                                0m,

                            AdjustmentsNetAmount =
                                group
                                    .Where(transaction =>
                                        transaction.TransactionType ==
                                            FinancialTransactionTypes.Adjustment &&
                                        transaction.Status ==
                                            FinancialTransactionStatuses.Completed)
                                    .Sum(transaction =>
                                        (decimal?)(
                                            transaction.AvailableBalanceAfter -
                                            transaction.AvailableBalanceBefore))
                                ??
                                0m,

                            CreditMovementsAmount =
                                group.Sum(transaction =>
                                    (decimal?)(
                                        transaction.AvailableBalanceAfter >
                                        transaction.AvailableBalanceBefore
                                            ? transaction.AvailableBalanceAfter -
                                                transaction.AvailableBalanceBefore
                                            : 0m))
                                ??
                                0m,

                            DebitMovementsAmount =
                                group.Sum(transaction =>
                                    (decimal?)(
                                        transaction.AvailableBalanceAfter <
                                        transaction.AvailableBalanceBefore
                                            ? transaction.AvailableBalanceBefore -
                                                transaction.AvailableBalanceAfter
                                            : 0m))
                                ??
                                0m,

                            AvailableBalanceNetChange =
                                group.Sum(transaction =>
                                    (decimal?)(
                                        transaction.AvailableBalanceAfter -
                                        transaction.AvailableBalanceBefore))
                                ??
                                0m,

                            HeldBalanceNetChange =
                                group.Sum(transaction =>
                                    (decimal?)(
                                        transaction.HeldBalanceAfter -
                                        transaction.HeldBalanceBefore))
                                ??
                                0m,

                            DebtNetChange =
                                group.Sum(transaction =>
                                    (decimal?)(
                                        transaction.DebtAfter -
                                        transaction.DebtBefore))
                                ??
                                0m
                        })
                    .FirstOrDefaultAsync(
                        cancellationToken)
                ??
                new CommissionTransactionsSummaryDto();

            var totalCount =
                summary.RecordsCount;

            var totalPages =
                totalCount <= 0
                    ? 0
                    : (int)Math.Ceiling(
                        (double)totalCount /
                        filter.PageSize);

            if (totalPages == 0)
            {
                filter.PageNumber =
                    1;
            }
            else if (
                filter.PageNumber >
                totalPages
            )
            {
                filter.PageNumber =
                    totalPages;
            }

            var transactions =
                await query
                    .Include(transaction =>
                        transaction.Place)
                    .Include(transaction =>
                        transaction.PlatformWallet)
                        .ThenInclude(wallet =>
                            wallet.Wallet)
                    .Include(transaction =>
                        transaction.CreatedByUser)
                    .Include(transaction =>
                        transaction.ReviewedByUser)
                    .AsSplitQuery()
                    .OrderByDescending(transaction =>
                        transaction.CreatedAt)
                    .ThenByDescending(transaction =>
                        transaction.CommissionTransactionId)
                    .Skip(
                        (
                            filter.PageNumber -
                            1
                        ) *
                        filter.PageSize)
                    .Take(
                        filter.PageSize)
                    .ToListAsync(
                        cancellationToken);

            return new TransactionsTabDto
            {
                Currency =
                    GetCurrency(
                        settings),

                SelectedPlaceId =
                    filter.PlaceId,

                SelectedPlaceName =
                    selectedPlaceName,

                Filter =
                    filter,

                Summary =
                    summary,

                TransactionTypeOptions =
                    BuildTransactionTypeOptions(
                        filter.TransactionType),

                StatusOptions =
                    BuildTransactionStatusOptions(
                        filter.Status),

                Transactions =
                    transactions
                        .Select(
                            MapTransaction)
                        .ToList(),

                TotalCount =
                    totalCount,

                PageNumber =
                    filter.PageNumber,

                PageSize =
                    filter.PageSize
            };
        }

        public async Task<RefundsTabDto> GetRefundsAsync(
            int ownerId,
            RefundFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??=
                new RefundFilterDto();

            filter.PageNumber =
                Math.Max(
                    1,
                    filter.PageNumber);

            if (
                !AllowedRefundPageSizes.Contains(
                    filter.PageSize)
            )
            {
                filter.PageSize =
                    25;
            }

            filter.PlaceId =
                await ValidatePlaceAsync(
                    ownerId,
                    filter.PlaceId,
                    cancellationToken);

            if (
                filter.BookingId.HasValue &&
                filter.BookingId.Value <= 0
            )
            {
                filter.BookingId =
                    null;
            }

            if (
                filter.CustomerId.HasValue &&
                filter.CustomerId.Value <= 0
            )
            {
                filter.CustomerId =
                    null;
            }

            filter.Status =
                NormalizeAllowedValue(
                    filter.Status,
                    FinancialRefundStatuses.All);

            filter.Search =
                string.IsNullOrWhiteSpace(
                    filter.Search)
                    ? null
                    : filter.Search.Trim();

            if (
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue &&
                filter.FromDate.Value.Date >
                filter.ToDate.Value.Date
            )
            {
                var temporaryDate =
                    filter.FromDate;

                filter.FromDate =
                    filter.ToDate;

                filter.ToDate =
                    temporaryDate;
            }

            var settings =
                await GetSettingsAsync(
                    cancellationToken);

            var selectedPlaceName =
                "كل المنشآت";

            if (filter.PlaceId.HasValue)
            {
                selectedPlaceName =
                    await _context.Places
                        .AsNoTracking()
                        .Where(place =>
                            place.PlaceId ==
                                filter.PlaceId.Value &&
                            place.OwnerId ==
                                ownerId)
                        .Select(place =>
                            place.PlaceNameAndDesc)
                        .FirstOrDefaultAsync(
                            cancellationToken)
                    ??
                    "منشأة غير معروفة";
            }

            IQueryable<RefundRequest> query =
                _context.RefundRequests
                    .AsNoTracking()
                    .Where(refund =>
                        refund.Place.OwnerId ==
                        ownerId);

            if (filter.PlaceId.HasValue)
            {
                var selectedPlaceId =
                    filter.PlaceId.Value;

                query =
                    query.Where(refund =>
                        refund.PlaceId ==
                        selectedPlaceId);
            }

            if (filter.BookingId.HasValue)
            {
                var bookingId =
                    filter.BookingId.Value;

                query =
                    query.Where(refund =>
                        refund.BookingId ==
                        bookingId);
            }

            if (filter.CustomerId.HasValue)
            {
                var customerId =
                    filter.CustomerId.Value;

                query =
                    query.Where(refund =>
                        refund.CustomerId ==
                        customerId);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    filter.Status)
            )
            {
                var status =
                    filter.Status;

                query =
                    query.Where(refund =>
                        refund.Status ==
                        status);
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate =
                    filter.FromDate.Value.Date;

                query =
                    query.Where(refund =>
                        refund.RequestedAt.HasValue &&
                        refund.RequestedAt.Value >=
                        fromDate);
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive =
                    filter.ToDate.Value.Date
                        .AddDays(1);

                query =
                    query.Where(refund =>
                        refund.RequestedAt.HasValue &&
                        refund.RequestedAt.Value <
                        toDateExclusive);
            }

            if (
                !string.IsNullOrWhiteSpace(
                    filter.Search)
            )
            {
                var search =
                    filter.Search;

                if (
                    int.TryParse(
                        search,
                        out var numericSearch)
                )
                {
                    query =
                        query.Where(refund =>
                            refund.RefundRequestId ==
                                numericSearch ||

                            refund.BookingId ==
                                numericSearch ||

                            refund.CustomerId ==
                                numericSearch ||

                            (
                                refund.ReferenceNumber != null &&
                                refund.ReferenceNumber
                                    .Contains(search)
                            ) ||

                            refund.Place
                                .PlaceNameAndDesc
                                .Contains(search) ||

                            refund.Customer
                                .FullName
                                .Contains(search) ||

                            refund.Customer
                                .Username
                                .Contains(search));
                }
                else
                {
                    query =
                        query.Where(refund =>
                            refund.Place
                                .PlaceNameAndDesc
                                .Contains(search) ||

                            refund.Customer
                                .FullName
                                .Contains(search) ||

                            refund.Customer
                                .Username
                                .Contains(search) ||

                            (
                                refund.Customer
                                    .PhoneNumber != null &&
                                refund.Customer
                                    .PhoneNumber
                                    .Contains(search)
                            ) ||

                            (
                                refund.Customer
                                    .ContactInfo != null &&
                                refund.Customer
                                    .ContactInfo
                                    .Contains(search)
                            ) ||

                            (
                                refund.ReferenceNumber != null &&
                                refund.ReferenceNumber
                                    .Contains(search)
                            ) ||

                            (
                                refund.Reason != null &&
                                refund.Reason
                                    .Contains(search)
                            ) ||

                            (
                                refund.Notes != null &&
                                refund.Notes
                                    .Contains(search)
                            ) ||

                            (
                                refund.Status != null &&
                                refund.Status
                                    .Contains(search)
                            ) ||

                            (
                                refund.ProcessedByUser != null &&
                                (
                                    refund.ProcessedByUser
                                        .FullName
                                        .Contains(search) ||

                                    refund.ProcessedByUser
                                        .Username
                                        .Contains(search)
                                )
                            ));
                }
            }

            var summary =
                await query
                    .GroupBy(refund =>
                        1)
                    .Select(group =>
                        new RefundsSummaryDto
                        {
                            RecordsCount =
                                group.Count(),

                            PendingCount =
                                group.Count(refund =>
                                    refund.Status ==
                                    FinancialRefundStatuses.Pending),

                            ProcessingCount =
                                group.Count(refund =>
                                    refund.Status ==
                                    FinancialRefundStatuses.Processing),

                            CompletedCount =
                                group.Count(refund =>
                                    refund.Status ==
                                    FinancialRefundStatuses.Completed),

                            RejectedCount =
                                group.Count(refund =>
                                    refund.Status ==
                                    FinancialRefundStatuses.Rejected),

                            FailedCount =
                                group.Count(refund =>
                                    refund.Status ==
                                    FinancialRefundStatuses.Failed),

                            CancelledCount =
                                group.Count(refund =>
                                    refund.Status ==
                                    FinancialRefundStatuses.Cancelled),

                            TotalDepositPaid =
                                group.Sum(refund =>
                                    (decimal?)refund.DepositPaid)
                                ??
                                0m,

                            RequestedRefundAmount =
                                group.Sum(refund =>
                                    (decimal?)refund.RefundAmount)
                                ??
                                0m,

                            PendingRefundAmount =
                                group
                                    .Where(refund =>
                                        refund.Status ==
                                            FinancialRefundStatuses.Pending ||
                                        refund.Status ==
                                            FinancialRefundStatuses.Processing)
                                    .Sum(refund =>
                                        (decimal?)refund.RefundAmount)
                                ??
                                0m,

                            VerifiedOpenRefundAmount =
                                group
                                    .Where(refund =>
                                        (
                                            refund.Status ==
                                                FinancialRefundStatuses.Pending ||
                                            refund.Status ==
                                                FinancialRefundStatuses.Processing
                                        ) &&
                                        refund.RefundAmount > 0m &&
                                        refund.Booking.FinancialRecord != null &&
                                        refund.Booking.FinancialRecord.AmountPaidOnline + 0.01m >=
                                            refund.RefundAmount)
                                    .Sum(refund =>
                                        (decimal?)refund.RefundAmount)
                                ??
                                0m,

                            FundingReviewCount =
                                group.Count(refund =>
                                    refund.Status ==
                                        FinancialRefundStatuses.Pending &&
                                    refund.RefundAmount > 0m &&
                                    (
                                        refund.Booking.FinancialRecord == null ||
                                        refund.Booking.FinancialRecord.AmountPaidOnline + 0.01m <
                                            refund.RefundAmount
                                    )),

                            ReadyForTransferCount =
                                group.Count(refund =>
                                    refund.Status ==
                                        FinancialRefundStatuses.Pending &&
                                    refund.RefundAmount > 0m &&
                                    refund.Booking.FinancialRecord != null &&
                                    refund.Booking.FinancialRecord.AmountPaidOnline + 0.01m >=
                                        refund.RefundAmount &&
                                    refund.RefundWalletId.HasValue &&
                                    refund.RefundWalletId.Value > 0 &&
                                    refund.RefundAccountNumber != null &&
                                    refund.RefundAccountNumber != "" &&
                                    refund.RefundBeneficiaryName != null &&
                                    refund.RefundBeneficiaryName != "" &&
                                    !refund.TransferredAt.HasValue),

                            WaitingDestinationCount =
                                group.Count(refund =>
                                    refund.Status ==
                                        FinancialRefundStatuses.Pending &&
                                    refund.RefundAmount > 0m &&
                                    refund.Booking.FinancialRecord != null &&
                                    refund.Booking.FinancialRecord.AmountPaidOnline + 0.01m >=
                                        refund.RefundAmount &&
                                    (
                                        !refund.RefundWalletId.HasValue ||
                                        refund.RefundWalletId.Value <= 0 ||
                                        refund.RefundAccountNumber == null ||
                                        refund.RefundAccountNumber == "" ||
                                        refund.RefundBeneficiaryName == null ||
                                        refund.RefundBeneficiaryName == ""
                                    )),

                            WaitingCustomerConfirmationCount =
                                group.Count(refund =>
                                    refund.Status ==
                                        FinancialRefundStatuses.Processing &&
                                    refund.TransferredAt.HasValue &&
                                    !refund.CustomerConfirmedAt.HasValue),

                            CompletedRefundAmount =
                                group
                                    .Where(refund =>
                                        refund.Status ==
                                        FinancialRefundStatuses.Completed)
                                    .Sum(refund =>
                                        (decimal?)refund.RefundAmount)
                                ??
                                0m,

                            CompletedCancellationFees =
                                group
                                    .Where(refund =>
                                        refund.Status ==
                                        FinancialRefundStatuses.Completed)
                                    .Sum(refund =>
                                        (decimal?)refund.CancellationFee)
                                ??
                                0m,

                            CompletedOwnerShare =
                                group
                                    .Where(refund =>
                                        refund.Status ==
                                        FinancialRefundStatuses.Completed)
                                    .Sum(refund =>
                                        (decimal?)refund.OwnerShare)
                                ??
                                0m,

                            CompletedPlatformCommission =
                                group
                                    .Where(refund =>
                                        refund.Status ==
                                        FinancialRefundStatuses.Completed)
                                    .Sum(refund =>
                                        (decimal?)refund.PlatformCommission)
                                ??
                                0m,

                            CompletedCommissionReleased =
                                group
                                    .Where(refund =>
                                        refund.Status ==
                                        FinancialRefundStatuses.Completed)
                                    .Sum(refund =>
                                        (decimal?)refund.CommissionReleased)
                                ??
                                0m,

                            CompletedRetainedAmount =
                                group
                                    .Where(refund =>
                                        refund.Status ==
                                        FinancialRefundStatuses.Completed)
                                    .Sum(refund =>
                                        (decimal?)(
                                            refund.DepositPaid >
                                                refund.RefundAmount
                                                ? refund.DepositPaid -
                                                  refund.RefundAmount
                                                : 0m))
                                ??
                                0m,

                            TotalCommissionReleased =
                                group
                                    .Where(refund =>
                                        refund.Status !=
                                            FinancialRefundStatuses.Rejected &&
                                        refund.Status !=
                                            FinancialRefundStatuses.Cancelled &&
                                        refund.Status !=
                                            FinancialRefundStatuses.Failed)
                                    .Sum(refund =>
                                        (decimal?)refund.CommissionReleased)
                                ??
                                0m,

                            TotalRetainedAmount =
                                group
                                    .Where(refund =>
                                        refund.Status !=
                                            FinancialRefundStatuses.Rejected &&
                                        refund.Status !=
                                            FinancialRefundStatuses.Cancelled &&
                                        refund.Status !=
                                            FinancialRefundStatuses.Failed)
                                    .Sum(refund =>
                                        (decimal?)(
                                            refund.DepositPaid >
                                                refund.RefundAmount
                                                ? refund.DepositPaid -
                                                  refund.RefundAmount
                                                : 0m))
                                ??
                                0m
                        })
                    .FirstOrDefaultAsync(
                        cancellationToken)
                ??
                new RefundsSummaryDto();

            var totalCount =
                summary.RecordsCount;

            var totalPages =
                totalCount <= 0
                    ? 0
                    : (int)Math.Ceiling(
                        (double)totalCount /
                        filter.PageSize);

            if (totalPages == 0)
            {
                filter.PageNumber =
                    1;
            }
            else if (
                filter.PageNumber >
                totalPages
            )
            {
                filter.PageNumber =
                    totalPages;
            }

            var refunds =
                await query
                    .Include(refund =>
                        refund.Booking)
                        .ThenInclude(booking =>
                            booking.FinancialRecord)
                    .Include(refund =>
                        refund.Customer)
                    .Include(refund =>
                        refund.Place)
                    .Include(refund =>
                        refund.ProcessedByUser)
                    .Include(refund =>
                        refund.RefundWallet)
                    .AsSplitQuery()
                    .OrderByDescending(refund =>
                        refund.RequestedAt)
                    .ThenByDescending(refund =>
                        refund.RefundRequestId)
                    .Skip(
                        (
                            filter.PageNumber -
                            1
                        ) *
                        filter.PageSize)
                    .Take(
                        filter.PageSize)
                    .ToListAsync(
                        cancellationToken);

            return new RefundsTabDto
            {
                Currency =
                    GetCurrency(
                        settings),

                SelectedPlaceId =
                    filter.PlaceId,

                SelectedPlaceName =
                    selectedPlaceName,

                Filter =
                    filter,

                Summary =
                    summary,

                StatusOptions =
                    BuildRefundStatusOptions(
                        filter.Status),

                Refunds =
                    refunds
                        .Select(
                            MapRefundRequest)
                        .ToList(),

                TotalCount =
                    totalCount,

                PageNumber =
                    filter.PageNumber,

                PageSize =
                    filter.PageSize
            };
        }

        public async Task<OwnerWalletsTabDto> GetOwnerWalletsAsync(
            int ownerId,
            CancellationToken cancellationToken = default)
        {
            if (ownerId <= 0)
            {
                return new OwnerWalletsTabDto();
            }

            var ownerWallets =
                await _context.OwnerWallets
                    .AsNoTracking()
                    .Include(ownerWallet =>
                        ownerWallet.Wallet)
                    .Where(ownerWallet =>
                        ownerWallet.OwnerId ==
                        ownerId)
                    .OrderByDescending(ownerWallet =>
                        ownerWallet.IsActive)
                    .ThenByDescending(ownerWallet =>
                        ownerWallet.UpdatedAt)
                    .ThenBy(ownerWallet =>
                        ownerWallet.Wallet.WalletName)
                    .ThenBy(ownerWallet =>
                        ownerWallet.AccountNumber)
                    .ToListAsync(
                        cancellationToken);

            /*
             * نجلب الشبكات كلها حتى تبقى الشبكة المرتبطة ظاهرة
             * إذا قامت الإدارة بإيقافها لاحقًا.
             * الإضافة والتفعيل مسموحان للشبكات النشطة فقط.
             */
            var supportedWallets =
                await _context.SupportedWallets
                    .AsNoTracking()
                    .OrderByDescending(wallet =>
                        wallet.IsActive)
                    .ThenBy(wallet =>
                        wallet.WalletName)
                    .ToListAsync(
                        cancellationToken);

            var walletItems =
                ownerWallets
                    .Select(ownerWallet =>
                        new OwnerWalletItemDto
                        {
                            OwnerWalletId =
                                ownerWallet.OwnerWalletId,

                            WalletId =
                                ownerWallet.WalletId,

                            WalletName =
                                FirstNonEmpty(
                                    "محفظة",
                                    ownerWallet.Wallet?
                                        .WalletName),

                            LogoPath =
                                ownerWallet.Wallet?
                                    .LogoPath?
                                    .Trim(),

                            AccountNumber =
                                ownerWallet.AccountNumber?
                                    .Trim() ??
                                string.Empty,

                            BeneficiaryName =
                                ownerWallet.BeneficiaryName?
                                    .Trim() ??
                                string.Empty,

                            IsActive =
                                ownerWallet.IsActive ??
                                false,

                            IsProviderActive =
                                ownerWallet.Wallet?
                                    .IsActive ??
                                false,

                            UpdatedAt =
                                ownerWallet.UpdatedAt
                        })
                    .ToList();

            /*
             * المفترض وجود حساب واحد فقط لكل شبكة.
             * GroupBy يمنع انهيار الشاشة إذا كانت القاعدة القديمة
             * تحتوي بالخطأ على أكثر من سجل للشبكة نفسها.
             */
            var linkedWalletsByWalletId =
                walletItems
                    .GroupBy(wallet =>
                        wallet.WalletId)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group
                                .OrderByDescending(wallet =>
                                    wallet.IsActive)
                                .ThenByDescending(wallet =>
                                    wallet.UpdatedAt)
                                .First());

            var supportedWalletOptions =
                supportedWallets
                    .Select(wallet =>
                    {
                        linkedWalletsByWalletId
                            .TryGetValue(
                                wallet.WalletId,
                                out var linkedWallet);

                        return new SupportedWalletOptionDto
                        {
                            WalletId =
                                wallet.WalletId,

                            WalletName =
                                FirstNonEmpty(
                                    "محفظة",
                                    wallet.WalletName),

                            LogoPath =
                                wallet.LogoPath?
                                    .Trim(),

                            IsActive =
                                wallet.IsActive ??
                                false,

                            IsLinked =
                                linkedWallet !=
                                null,

                            OwnerWalletId =
                                linkedWallet?
                                    .OwnerWalletId,

                            LinkedAccountNumber =
                                linkedWallet?
                                    .AccountNumber,

                            LinkedBeneficiaryName =
                                linkedWallet?
                                    .BeneficiaryName,

                            LinkedAccountIsActive =
                                linkedWallet?
                                    .IsActive ??
                                false
                        };
                    })
                    .ToList();

            /*
             * إذا وجد سجل مالك مرتبط بشبكة حُذفت من SupportedWallets
             * نظهره أيضًا بدل أن يختفي من واجهة المالك.
             */
            var knownWalletIds =
                supportedWalletOptions
                    .Select(wallet =>
                        wallet.WalletId)
                    .ToHashSet();

            foreach (
                var linkedWallet
                in walletItems.Where(wallet =>
                    !knownWalletIds.Contains(
                        wallet.WalletId))
            )
            {
                supportedWalletOptions.Add(
                    new SupportedWalletOptionDto
                    {
                        WalletId =
                            linkedWallet.WalletId,

                        WalletName =
                            linkedWallet.WalletName,

                        LogoPath =
                            linkedWallet.LogoPath,

                        IsActive =
                            false,

                        IsLinked =
                            true,

                        OwnerWalletId =
                            linkedWallet.OwnerWalletId,

                        LinkedAccountNumber =
                            linkedWallet.AccountNumber,

                        LinkedBeneficiaryName =
                            linkedWallet.BeneficiaryName,

                        LinkedAccountIsActive =
                            linkedWallet.IsActive
                    });
            }

            supportedWalletOptions =
                supportedWalletOptions
                    .OrderByDescending(wallet =>
                        wallet.IsActive)
                    .ThenByDescending(wallet =>
                        wallet.IsLinked)
                    .ThenBy(wallet =>
                        wallet.WalletName)
                    .ToList();

            var activeSupportedWallets =
                supportedWalletOptions.Count(wallet =>
                    wallet.IsActive);

            var linkedActiveWalletIds =
                supportedWalletOptions
                    .Where(wallet =>
                        wallet.IsActive &&
                        wallet.IsLinked)
                    .Select(wallet =>
                        wallet.WalletId)
                    .Distinct()
                    .Count();

            return new OwnerWalletsTabDto
            {
                Summary =
                    new OwnerWalletsSummaryDto
                    {
                        TotalAccounts =
                            walletItems.Count,

                        ActiveAccounts =
                            walletItems.Count(wallet =>
                                wallet.IsAvailableToCustomers),

                        InactiveAccounts =
                            walletItems.Count(wallet =>
                                !wallet.IsAvailableToCustomers),

                        ActiveSupportedWallets =
                            activeSupportedWallets,

                        LinkedWallets =
                            linkedActiveWalletIds,

                        AvailableWallets =
                            supportedWalletOptions.Count(wallet =>
                                wallet.CanAdd),

                        UnavailableLinkedWallets =
                            supportedWalletOptions.Count(wallet =>
                                wallet.IsLinked &&
                                !wallet.IsActive)
                    },

                Wallets =
                    walletItems,

                SupportedWallets =
                    supportedWalletOptions,

                LastUpdatedAt =
                    walletItems
                        .Where(wallet =>
                            wallet.UpdatedAt.HasValue)
                        .Select(wallet =>
                            wallet.UpdatedAt)
                        .OrderByDescending(value =>
                            value)
                        .FirstOrDefault()
            };
        }

        public async Task<FinanceOperationResult> SaveOwnerWalletAsync(
            int ownerId,
            SaveOwnerWalletRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (ownerId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب المالك الحالي.");
            }

            if (request == null)
            {
                return FinanceOperationResult.Failed(
                    "بيانات حساب الاستقبال غير مكتملة.");
            }

            var accountNumber =
                request.AccountNumber?
                    .Trim() ??
                string.Empty;

            var beneficiaryName =
                request.BeneficiaryName?
                    .Trim() ??
                string.Empty;

            if (
                request.OwnerWalletId.HasValue &&
                request.OwnerWalletId.Value <= 0
            )
            {
                return FinanceOperationResult.Failed(
                    "رقم حساب الاستقبال غير صحيح.");
            }

            if (request.WalletId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "اختر شبكة الدفع.");
            }

            if (
                accountNumber.Length != 6 ||
                accountNumber.Any(character =>
                    character < '0' ||
                    character > '9')
            )
            {
                return FinanceOperationResult.Failed(
                    "رقم الحساب التجاري يجب أن يتكون من 6 أرقام إنجليزية فقط.");
            }

            if (
                beneficiaryName.Length < 2 ||
                beneficiaryName.Length > 150 ||
                beneficiaryName.Any(
                    char.IsControl)
            )
            {
                return FinanceOperationResult.Failed(
                    "اسم المستفيد يجب أن يكون بين حرفين و150 حرفًا.");
            }

            OwnerWallet? ownerWallet =
                null;

            if (request.OwnerWalletId.HasValue)
            {
                ownerWallet =
                    await _context.OwnerWallets
                        .FirstOrDefaultAsync(
                            wallet =>
                                wallet.OwnerWalletId ==
                                    request.OwnerWalletId.Value &&
                                wallet.OwnerId ==
                                    ownerId,
                            cancellationToken);

                if (ownerWallet == null)
                {
                    return FinanceOperationResult.Failed(
                        "الحساب غير موجود أو لا تملك صلاحية تعديله.");
                }
            }

            var supportedWallet =
                await _context.SupportedWallets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        wallet =>
                            wallet.WalletId ==
                                request.WalletId,
                        cancellationToken);

            if (
                supportedWallet == null ||
                supportedWallet.IsActive !=
                    true
            )
            {
                return FinanceOperationResult.Failed(
                    "شبكة الدفع غير موجودة أو غير مفعلة من إدارة المنصة.");
            }

            /*
             * قاعدة المشروع الحالية:
             * حساب تجاري واحد فقط لكل شبكة دفع لكل مالك.
             */
            var walletTypeAlreadyLinked =
                await _context.OwnerWallets
                    .AsNoTracking()
                    .AnyAsync(
                        wallet =>
                            wallet.OwnerId ==
                                ownerId &&
                            wallet.WalletId ==
                                request.WalletId &&
                            (
                                !request.OwnerWalletId.HasValue ||
                                wallet.OwnerWalletId !=
                                    request.OwnerWalletId.Value
                            ),
                        cancellationToken);

            if (walletTypeAlreadyLinked)
            {
                return FinanceOperationResult.Failed(
                    "هذه الشبكة مرتبطة بحساب آخر مسبقًا. يمكنك تعديل الحساب الموجود بدل إضافة حساب جديد.");
            }

            if (ownerWallet == null)
            {
                ownerWallet =
                    new OwnerWallet
                    {
                        OwnerId =
                            ownerId,

                        WalletId =
                            request.WalletId,

                        AccountNumber =
                            accountNumber,

                        BeneficiaryName =
                            beneficiaryName,

                        IsActive =
                            request.IsActive,

                        UpdatedAt =
                            DateTime.Now
                    };

                _context.OwnerWallets.Add(
                    ownerWallet);
            }
            else
            {
                ownerWallet.WalletId =
                    request.WalletId;

                ownerWallet.AccountNumber =
                    accountNumber;

                ownerWallet.BeneficiaryName =
                    beneficiaryName;

                ownerWallet.IsActive =
                    request.IsActive;

                ownerWallet.UpdatedAt =
                    DateTime.Now;
            }

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);

                return FinanceOperationResult.Succeeded(
                    request.OwnerWalletId.HasValue
                        ? "تم تحديث حساب استقبال الدفعات بنجاح."
                        : "تمت إضافة حساب استقبال الدفعات بنجاح.",
                    ownerWallet.OwnerWalletId);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(
                    exception,
                    "تعذر حفظ حساب الاستقبال للمالك {OwnerId} على الشبكة {WalletId}.",
                    ownerId,
                    request.WalletId);

                return FinanceOperationResult.Failed(
                    "تعذر حفظ الحساب. تأكد أن الشبكة غير مرتبطة بحساب آخر.");
            }
        }

        public async Task<FinanceOperationResult> ChangeOwnerWalletStatusAsync(
            int ownerId,
            int ownerWalletId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            if (
                ownerId <= 0 ||
                ownerWalletId <= 0
            )
            {
                return FinanceOperationResult.Failed(
                    "بيانات حساب الاستقبال غير صحيحة.");
            }

            var ownerWallet =
                await _context.OwnerWallets
                    .Include(wallet =>
                        wallet.Wallet)
                    .FirstOrDefaultAsync(
                        wallet =>
                            wallet.OwnerWalletId ==
                                ownerWalletId &&
                            wallet.OwnerId ==
                                ownerId,
                        cancellationToken);

            if (ownerWallet == null)
            {
                return FinanceOperationResult.Failed(
                    "الحساب غير موجود أو لا تملك صلاحية تعديله.");
            }

            if (
                isActive &&
                (
                    ownerWallet.Wallet ==
                        null ||
                    ownerWallet.Wallet.IsActive !=
                        true
                )
            )
            {
                return FinanceOperationResult.Failed(
                    "لا يمكن تفعيل الحساب لأن شبكة الدفع موقوفة من إدارة المنصة.");
            }

            var currentStatus =
                ownerWallet.IsActive ??
                false;

            if (currentStatus == isActive)
            {
                return FinanceOperationResult.Succeeded(
                    isActive
                        ? "الحساب مفعل بالفعل."
                        : "الحساب متوقف بالفعل.",
                    ownerWallet.OwnerWalletId);
            }

            ownerWallet.IsActive =
                isActive;

            ownerWallet.UpdatedAt =
                DateTime.Now;

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);

                return FinanceOperationResult.Succeeded(
                    isActive
                        ? "تم تفعيل حساب استقبال الدفعات."
                        : "تم إيقاف حساب استقبال الدفعات.",
                    ownerWallet.OwnerWalletId);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(
                    exception,
                    "تعذر تغيير حالة حساب الاستقبال {OwnerWalletId} للمالك {OwnerId}.",
                    ownerWalletId,
                    ownerId);

                return FinanceOperationResult.Failed(
                    "تعذر تغيير حالة الحساب حاليًا.");
            }
        }

        private sealed class BookingRefundAggregate
        {
            public decimal CompletedRefundAmount { get; set; }
            public decimal CompletedCancellationFee { get; set; }
            public decimal CompletedOwnerShare { get; set; }
            public decimal CompletedPlatformCommission { get; set; }
            public decimal CompletedCommissionReleased { get; set; }
            public decimal PendingRefundAmount { get; set; }
            public int CompletedCount { get; set; }
            public int PendingCount { get; set; }

            /* توافق مع منشئ تفاصيل المستندات الحالي. */
            public decimal RefundAmount => CompletedRefundAmount;
            public decimal CancellationFee => CompletedCancellationFee;
            public decimal OwnerShare => CompletedOwnerShare;
            public decimal PlatformCommission => CompletedPlatformCommission;
            public decimal CommissionReleased => CompletedCommissionReleased;
        }

        private sealed class BookingCommissionMovementAggregate
        {
            public decimal HoldMovementsAmount { get; set; }
            public decimal ChargedAmount { get; set; }
            public decimal ReleasedAmount { get; set; }
            public decimal DebtCreatedAmount { get; set; }
            public DateTime? LastActivityAt { get; set; }
            public string? LastReferenceNumber { get; set; }

            public decimal OutstandingHeldAmount =>
                Math.Max(
                    0m,
                    HoldMovementsAmount -
                    ChargedAmount -
                    ReleasedAmount);
        }

        private sealed class FinancialReportEngineResult
        {
            public FinancialReportFilterDto Filter { get; set; } =
                new FinancialReportFilterDto();

            public List<FinancialDocumentItemDto> Documents { get; set; } =
                new List<FinancialDocumentItemDto>();

            public List<FinancialReportRowDto> Rows { get; set; } =
                new List<FinancialReportRowDto>();

            public FinancialReportSummaryDto Summary { get; set; } =
                new FinancialReportSummaryDto();

            public ComprehensiveFinancialAnalyticsDto Analytics { get; set; } =
                new ComprehensiveFinancialAnalyticsDto();

            public FinancialReconciliationDto Reconciliation { get; set; } =
                new FinancialReconciliationDto();
        }

        private static readonly IReadOnlyCollection<string>
            ReportBookingStates =
                new[]
                {
                    "Pending",
                    "AwaitingPayment",
                    "PaymentReview",
                    "Confirmed",
                    "Completed",
                    "Cancelled",
                    "Rejected",
                    "Expired",
                    "NoShow"
                };

        private static readonly IReadOnlyCollection<string>
            ReportPaymentStatuses =
                new[]
                {
                    "Unpaid",
                    "Pending",
                    "UnderReview",
                    "PartiallyPaid",
                    "Paid",
                    "Completed",
                    "PartiallyRefunded",
                    "Refunded",
                    "Failed",
                    "Expired",
                    "Cancelled"
                };

        private static IReadOnlyCollection<string>
            ReportCommissionStatuses =>
                new[]
                {
                    FinancialCommissionStatuses.Calculated,
                    FinancialCommissionStatuses.Held,
                    FinancialCommissionStatuses.Charged,
                    FinancialCommissionStatuses.Released,
                    FinancialCommissionStatuses.Debt,
                    FinancialCommissionStatuses.NotApplicable
                };

        private static string GetFinancialReportTypeLabel(
            string? value)
        {
            if (IsState(value, OwnerFinancialReportTypes.Documents))
            {
                return "المستندات المالية";
            }

            if (IsState(value, OwnerFinancialReportTypes.Bookings))
            {
                return "تقرير الحجوزات والإيرادات";
            }

            if (IsState(value, OwnerFinancialReportTypes.Payments))
            {
                return "تقرير المدفوعات والتحصيلات";
            }

            if (IsState(value, OwnerFinancialReportTypes.Commissions))
            {
                return "تقرير عمولات المنصة";
            }

            if (IsState(value, OwnerFinancialReportTypes.Refunds))
            {
                return "تقرير الاستردادات والإلغاءات";
            }

            if (IsState(value, OwnerFinancialReportTypes.Transactions))
            {
                return "تقرير الحركات المالية";
            }

            return "الكشف المالي الشامل";
        }

        private static string GetFinancialReportDescription(
            string? value)
        {
            if (IsState(value, OwnerFinancialReportTypes.Documents))
            {
                return "أرشيف الفواتير والإيصالات وكشوف التسوية الجاهزة للمعاينة والطباعة.";
            }

            if (IsState(value, OwnerFinancialReportTypes.Bookings))
            {
                return "صف واحد لكل حجز مع قيمة الحجز والتحصيل والمتبقي والإيراد الفعلي.";
            }

            if (IsState(value, OwnerFinancialReportTypes.Payments))
            {
                return "المدفوع إلكترونيًا والمحصّل في المنشأة والمتبقي حسب تاريخ الدفع الفعلي.";
            }

            if (IsState(value, OwnerFinancialReportTypes.Commissions))
            {
                return "العمولة المحسوبة والمحجوزة والمخصومة والمحررة والدين حسب تاريخ التسوية.";
            }

            if (IsState(value, OwnerFinancialReportTypes.Refunds))
            {
                return "طلبات الاسترداد ورسوم الإلغاء والمسترد فعليًا وحصة المنشأة.";
            }

            if (IsState(value, OwnerFinancialReportTypes.Transactions))
            {
                return "كل حركة فعلية في رصيد ضمان العمولة مع الرصيد قبل الحركة وبعدها.";
            }

            return "كشف موحّد للحجوزات مع تحليلات مستقلة للشحن والعمولة والدين والاستردادات.";
        }

        private static string GetFinancialDocumentTypeLabel(
            string? value)
        {
            if (IsState(value, OwnerFinancialDocumentTypes.BookingInvoice))
            {
                return "فاتورة حجز";
            }

            if (IsState(value, OwnerFinancialDocumentTypes.DepositReceipt))
            {
                return "إيصال دفع عربون";
            }

            if (IsState(value, OwnerFinancialDocumentTypes.CollectionReceipt))
            {
                return "إيصال تحصيل في المنشأة";
            }

            if (IsState(value, OwnerFinancialDocumentTypes.RefundReceipt))
            {
                return "إيصال استرداد";
            }

            if (IsState(value, OwnerFinancialDocumentTypes.TopUpReceipt))
            {
                return "إيصال شحن رصيد";
            }

            if (IsState(value, OwnerFinancialDocumentTypes.BookingSettlement))
            {
                return "كشف تسوية حجز";
            }

            return "مستند مالي";
        }

        private static string BuildFinancialDocumentNumber(
            string documentType,
            int sourceId)
        {
            var prefix =
                IsState(documentType, OwnerFinancialDocumentTypes.BookingInvoice)
                    ? "INV"
                    : IsState(documentType, OwnerFinancialDocumentTypes.DepositReceipt)
                        ? "DEP"
                        : IsState(documentType, OwnerFinancialDocumentTypes.CollectionReceipt)
                            ? "COL"
                            : IsState(documentType, OwnerFinancialDocumentTypes.RefundReceipt)
                                ? "REF"
                                : IsState(documentType, OwnerFinancialDocumentTypes.TopUpReceipt)
                                    ? "TOP"
                                    : "SET";

            return string.Concat(
                prefix,
                "-",
                sourceId.ToString(
                    "D8",
                    CultureInfo.InvariantCulture));
        }

        private static string GetReportBookingStateLabel(
            string? value)
        {
            if (IsState(value, "Pending")) return "قيد الانتظار";
            if (IsState(value, "AwaitingPayment")) return "بانتظار الدفع";
            if (IsState(value, "PaymentReview")) return "مراجعة الدفع";
            if (IsState(value, "Confirmed")) return "مؤكد";
            if (IsState(value, "Completed")) return "مكتمل";
            if (IsState(value, "Cancelled")) return "ملغى";
            if (IsState(value, "Rejected")) return "مرفوض";
            if (IsState(value, "Expired")) return "منتهي";
            if (IsState(value, "NoShow")) return "عدم حضور";
            return "غير محددة";
        }

        private static string GetReportPaymentStatusLabel(
            string? value)
        {
            if (IsState(value, "Unpaid")) return "غير مدفوع";
            if (IsState(value, "Pending")) return "قيد الانتظار";
            if (IsState(value, "UnderReview")) return "قيد المراجعة";
            if (IsState(value, "PartiallyPaid")) return "مدفوع جزئيًا";
            if (IsState(value, "Paid")) return "مدفوع";
            if (IsState(value, "Completed")) return "مكتمل";
            if (IsState(value, "PartiallyRefunded")) return "مسترد جزئيًا";
            if (IsState(value, "Refunded")) return "مسترد";
            if (IsState(value, "Failed")) return "فشل";
            if (IsState(value, "Expired")) return "منتهي";
            if (IsState(value, "Cancelled")) return "ملغى";
            return "غير محددة";
        }

        private static string GetReportCommissionStatusLabel(
            string? value)
        {
            if (IsState(value, FinancialCommissionStatuses.Calculated)) return "محسوبة";
            if (IsState(value, FinancialCommissionStatuses.Held)) return "محجوزة";
            if (IsState(value, FinancialCommissionStatuses.Charged)) return "مخصومة";
            if (IsState(value, FinancialCommissionStatuses.Released)) return "محررة";
            if (IsState(value, FinancialCommissionStatuses.Debt)) return "دين";
            if (IsState(value, FinancialCommissionStatuses.NotApplicable)) return "غير مطبقة";
            return "غير محددة";
        }

        private static string GetFinancialDateScopeLabel(
            string? value)
        {
            if (IsState(value, OwnerFinancialDateScopes.DocumentDate)) return "تاريخ المستند";
            if (IsState(value, OwnerFinancialDateScopes.BookingCreatedDate)) return "تاريخ إنشاء الحجز";
            if (IsState(value, OwnerFinancialDateScopes.StayStartDate)) return "تاريخ بداية الإقامة";
            if (IsState(value, OwnerFinancialDateScopes.PaymentActivityDate)) return "تاريخ الدفع أو التحصيل";
            if (IsState(value, OwnerFinancialDateScopes.OnlinePaymentDate)) return "تاريخ الدفع الإلكتروني";
            if (IsState(value, OwnerFinancialDateScopes.PropertyCollectionDate)) return "تاريخ التحصيل في المنشأة";
            if (IsState(value, OwnerFinancialDateScopes.CommissionActivityDate)) return "تاريخ حركة العمولة أو التسوية";
            if (IsState(value, OwnerFinancialDateScopes.RefundRequestedDate)) return "تاريخ طلب الاسترداد";
            if (IsState(value, OwnerFinancialDateScopes.RefundProcessedDate)) return "تاريخ معالجة الاسترداد";
            if (IsState(value, OwnerFinancialDateScopes.TransactionDate)) return "تاريخ الحركة المالية";
            return "التاريخ";
        }

        private static string GetPaymentChannelLabel(
            string? value)
        {
            if (IsState(value, OwnerFinancialPaymentChannels.Online))
            {
                return "إلكتروني";
            }

            if (IsState(value, OwnerFinancialPaymentChannels.AtProperty))
            {
                return "في المنشأة";
            }

            return "كل قنوات التحصيل";
        }

        private static List<FinancialStatusOptionDto>
            BuildReportTypeOptions(
                string? selected) =>
                OwnerFinancialReportTypes.All
                    .Select(value =>
                        new FinancialStatusOptionDto
                        {
                            Value = value,
                            Label = GetFinancialReportTypeLabel(value),
                            IsSelected = IsState(selected, value)
                        })
                    .ToList();

        private static List<FinancialStatusOptionDto>
            BuildDocumentTypeOptions(
                string? selected) =>
                OwnerFinancialDocumentTypes.All
                    .Select(value =>
                        new FinancialStatusOptionDto
                        {
                            Value = value,
                            Label = GetFinancialDocumentTypeLabel(value),
                            IsSelected = IsState(selected, value)
                        })
                    .ToList();

        private static List<FinancialStatusOptionDto>
            BuildReportOptions(
                IEnumerable<string> values,
                string? selected,
                Func<string, string> labelFactory) =>
                values
                    .Select(value =>
                        new FinancialStatusOptionDto
                        {
                            Value = value,
                            Label = labelFactory(value),
                            IsSelected = IsState(selected, value)
                        })
                    .ToList();

        private static IReadOnlyCollection<string>
            GetAllowedDateScopes(
                string reportType)
        {
            if (IsState(reportType, OwnerFinancialReportTypes.Documents))
            {
                return new[] { OwnerFinancialDateScopes.DocumentDate };
            }

            if (
                IsState(reportType, OwnerFinancialReportTypes.Bookings) ||
                IsState(reportType, OwnerFinancialReportTypes.Comprehensive))
            {
                return new[]
                {
                    OwnerFinancialDateScopes.BookingCreatedDate,
                    OwnerFinancialDateScopes.StayStartDate
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Payments))
            {
                return new[]
                {
                    OwnerFinancialDateScopes.PaymentActivityDate,
                    OwnerFinancialDateScopes.OnlinePaymentDate,
                    OwnerFinancialDateScopes.PropertyCollectionDate
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Commissions))
            {
                return new[] { OwnerFinancialDateScopes.CommissionActivityDate };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Refunds))
            {
                return new[]
                {
                    OwnerFinancialDateScopes.RefundRequestedDate,
                    OwnerFinancialDateScopes.RefundProcessedDate
                };
            }

            return new[] { OwnerFinancialDateScopes.TransactionDate };
        }

        private static string GetDefaultDateScope(
            string reportType) =>
                GetAllowedDateScopes(reportType).First();

        private static FinancialReportCapabilitiesDto
            BuildReportCapabilities(
                string reportType)
        {
            if (IsState(reportType, OwnerFinancialReportTypes.Documents))
            {
                return new FinancialReportCapabilitiesDto
                {
                    ShowDocumentType = true,
                    ShowDateScope = true,
                    ShowBookingId = true,
                    ShowCustomerId = true,
                    ShowReferenceNumber = true
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Bookings))
            {
                return new FinancialReportCapabilitiesDto
                {
                    ShowDateScope = true,
                    ShowBookingId = true,
                    ShowCustomerId = true,
                    ShowBookingState = true,
                    ShowPaymentStatus = true
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Payments))
            {
                return new FinancialReportCapabilitiesDto
                {
                    ShowDateScope = true,
                    ShowPaymentChannel = true,
                    ShowBookingId = true,
                    ShowCustomerId = true,
                    ShowPaymentStatus = true,
                    ShowReferenceNumber = true
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Commissions))
            {
                return new FinancialReportCapabilitiesDto
                {
                    ShowDateScope = true,
                    ShowBookingId = true,
                    ShowCommissionStatus = true
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Refunds))
            {
                return new FinancialReportCapabilitiesDto
                {
                    ShowDateScope = true,
                    ShowBookingId = true,
                    ShowCustomerId = true,
                    ShowRefundStatus = true,
                    ShowReferenceNumber = true
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Transactions))
            {
                return new FinancialReportCapabilitiesDto
                {
                    ShowDateScope = true,
                    ShowBookingId = true,
                    ShowTransactionType = true,
                    ShowTransactionStatus = true,
                    ShowReferenceNumber = true
                };
            }

            return new FinancialReportCapabilitiesDto
            {
                ShowDateScope = true,
                ShowBookingId = true,
                ShowCustomerId = true,
                ShowBookingState = true,
                ShowPaymentStatus = true,
                ShowCommissionStatus = true
            };
        }

        private async Task<FinancialReportFilterDto>
            NormalizeFinancialReportFilterAsync(
                int ownerId,
                FinancialReportFilterDto? filter,
                CancellationToken cancellationToken)
        {
            filter ??= new FinancialReportFilterDto();

            filter.PageNumber = Math.Max(1, filter.PageNumber);

            if (!AllowedReportPageSizes.Contains(filter.PageSize))
            {
                filter.PageSize = 25;
            }

            filter.PlaceId =
                await ValidatePlaceAsync(
                    ownerId,
                    filter.PlaceId,
                    cancellationToken);

            if (filter.BookingId.HasValue && filter.BookingId.Value <= 0)
            {
                filter.BookingId = null;
            }

            if (filter.CustomerId.HasValue && filter.CustomerId.Value <= 0)
            {
                filter.CustomerId = null;
            }

            filter.ReportType =
                NormalizeAllowedValue(
                    filter.ReportType,
                    OwnerFinancialReportTypes.All)
                ?? OwnerFinancialReportTypes.Comprehensive;

            filter.ExportFormat =
                NormalizeAllowedValue(
                    filter.ExportFormat,
                    OwnerFinancialExportFormats.All)
                ?? OwnerFinancialExportFormats.View;

            filter.Search = NormalizeLimitedText(filter.Search, 100);
            filter.ReferenceNumber = NormalizeLimitedText(filter.ReferenceNumber, 80);

            if (
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue &&
                filter.FromDate.Value.Date > filter.ToDate.Value.Date)
            {
                var temporaryDate = filter.FromDate;
                filter.FromDate = filter.ToDate;
                filter.ToDate = temporaryDate;
            }

            filter.FromDate = filter.FromDate?.Date;
            filter.ToDate = filter.ToDate?.Date;

            var allowedDateScopes =
                GetAllowedDateScopes(filter.ReportType);

            filter.DateScope =
                NormalizeAllowedValue(
                    filter.DateScope,
                    allowedDateScopes)
                ?? GetDefaultDateScope(filter.ReportType);

            var capabilities =
                BuildReportCapabilities(filter.ReportType);

            filter.DocumentType = capabilities.ShowDocumentType
                ? NormalizeAllowedValue(
                    filter.DocumentType,
                    OwnerFinancialDocumentTypes.All)
                : null;

            filter.PaymentChannel = capabilities.ShowPaymentChannel
                ? NormalizeAllowedValue(
                    filter.PaymentChannel,
                    OwnerFinancialPaymentChannels.Values)
                    ?? OwnerFinancialPaymentChannels.All
                : null;

            filter.BookingState = capabilities.ShowBookingState
                ? NormalizeAllowedValue(
                    filter.BookingState,
                    ReportBookingStates)
                : null;

            filter.PaymentStatus = capabilities.ShowPaymentStatus
                ? NormalizeAllowedValue(
                    filter.PaymentStatus,
                    ReportPaymentStatuses)
                : null;

            filter.CommissionStatus = capabilities.ShowCommissionStatus
                ? NormalizeAllowedValue(
                    filter.CommissionStatus,
                    ReportCommissionStatuses)
                : null;

            filter.RefundStatus = capabilities.ShowRefundStatus
                ? NormalizeAllowedValue(
                    filter.RefundStatus,
                    FinancialRefundStatuses.All)
                : null;

            filter.TransactionType = capabilities.ShowTransactionType
                ? NormalizeAllowedValue(
                    filter.TransactionType,
                    FinancialTransactionTypes.All)
                : null;

            filter.TransactionStatus = capabilities.ShowTransactionStatus
                ? NormalizeAllowedValue(
                    filter.TransactionStatus,
                    FinancialTransactionStatuses.All)
                : null;

            if (!capabilities.ShowBookingId)
            {
                filter.BookingId = null;
            }

            if (!capabilities.ShowCustomerId)
            {
                filter.CustomerId = null;
            }

            if (!capabilities.ShowReferenceNumber)
            {
                filter.ReferenceNumber = null;
            }

            return filter;
        }

        private static string? NormalizeLimitedText(
            string? value,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();

            return normalized.Substring(
                0,
                Math.Min(maximumLength, normalized.Length));
        }

        private static FinancialReportFilterDto
            CloneFinancialReportFilter(
                FinancialReportFilterDto filter) =>
                new FinancialReportFilterDto
                {
                    PlaceId = filter.PlaceId,
                    BookingId = filter.BookingId,
                    CustomerId = filter.CustomerId,
                    FromDate = filter.FromDate,
                    ToDate = filter.ToDate,
                    ReportType = filter.ReportType,
                    DocumentType = filter.DocumentType,
                    DateScope = filter.DateScope,
                    PaymentChannel = filter.PaymentChannel,
                    BookingState = filter.BookingState,
                    PaymentStatus = filter.PaymentStatus,
                    CommissionStatus = filter.CommissionStatus,
                    RefundStatus = filter.RefundStatus,
                    TransactionType = filter.TransactionType,
                    TransactionStatus = filter.TransactionStatus,
                    ReferenceNumber = filter.ReferenceNumber,
                    Search = filter.Search,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    ExportFormat = filter.ExportFormat
                };

        private async Task<string>
            GetSelectedFinancialPlaceNameAsync(
                int ownerId,
                int? placeId,
                CancellationToken cancellationToken)
        {
            if (!placeId.HasValue)
            {
                return "كل المنشآت";
            }

            return await _context.Places
                .AsNoTracking()
                .Where(place =>
                    place.PlaceId == placeId.Value &&
                    place.OwnerId == ownerId)
                .Select(place => place.PlaceNameAndDesc)
                .FirstOrDefaultAsync(cancellationToken)
                ?? "منشأة غير معروفة";
        }

        private static bool IsDateWithinFilter(
            DateTime? value,
            FinancialReportFilterDto filter)
        {
            if (!value.HasValue)
            {
                return !filter.FromDate.HasValue &&
                    !filter.ToDate.HasValue;
            }

            var date = value.Value.Date;

            if (
                filter.FromDate.HasValue &&
                date < filter.FromDate.Value.Date)
            {
                return false;
            }

            if (
                filter.ToDate.HasValue &&
                date > filter.ToDate.Value.Date)
            {
                return false;
            }

            return true;
        }

        private static DateTime? LatestDate(
            params DateTime?[] values)
        {
            var availableValues = values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();

            return availableValues.Count == 0
                ? null
                : availableValues.Max();
        }

        private static bool IsFullyPaidReportRow(
            FinancialReportRowDto row)
        {
            if (row.TotalPrice <= 0m)
            {
                return false;
            }

            return row.ActualCollectedAmount >= row.TotalPrice ||
                (
                    (
                        IsState(row.PaymentStatus, "Paid") ||
                        IsState(row.PaymentStatus, "Completed")
                    ) &&
                    row.RemainingAmount <= 0m
                );
        }

        private static bool IsPartiallyPaidReportRow(
            FinancialReportRowDto row) =>
                !IsFullyPaidReportRow(row) &&
                row.ActualCollectedAmount > 0m;

        private static bool IsUnpaidReportRow(
            FinancialReportRowDto row) =>
                row.TotalPrice > 0m &&
                !IsFullyPaidReportRow(row) &&
                !IsPartiallyPaidReportRow(row);

        private static bool IsCancelledBookingState(
            string? state) =>
                IsState(state, "Cancelled") ||
                IsState(state, "Rejected") ||
                IsState(state, "Expired");

        private static bool IsFailedPaymentState(
            string? state) =>
                IsState(state, "Failed") ||
                IsState(state, "Cancelled") ||
                IsState(state, "Expired");

        private static bool IsBookingPaymentConfirmed(Booking booking)
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

        private static decimal GetActualOnlineAmount(
            Booking booking)
        {
            var financial = booking.FinancialRecord;

            if (
                financial == null ||
                !IsBookingPaymentConfirmed(booking) ||
                IsFailedPaymentState(booking.PaymentStatus))
            {
                return 0m;
            }

            // المصدر الوحيد للدفع الإلكتروني الفعلي في التقارير.
            return Math.Max(0m, financial.AmountPaidOnline);
        }

        private static decimal GetActualPropertyAmount(
            Booking booking)
        {
            var financial = booking.FinancialRecord;

            if (
                financial == null ||
                IsFailedPaymentState(booking.PaymentStatus) ||
                financial.CollectedAt == null)
            {
                return 0m;
            }

            return Math.Max(0m, financial.AmountCollectedAtProperty);
        }

        private static DateTime? GetOnlinePaymentDate(
            Booking booking,
            FinancialRecord? financial)
        {
            if (financial == null || financial.AmountPaidOnline <= 0m)
            {
                return null;
            }

            return booking.PaymentApprovedAt ?? financial.UpdatedAt;
        }

        private static DateTime? GetPropertyCollectionDate(
            FinancialRecord? financial)
        {
            if (
                financial == null ||
                financial.AmountCollectedAtProperty <= 0m)
            {
                return null;
            }

            return financial.CollectedAt;
        }

        private static DateTime GetCommissionActivityDate(
            FinancialRecord financial) =>
                financial.CommissionChargedAt ??
                financial.CommissionReleasedAt ??
                financial.CommissionHeldAt ??
                financial.Booking?.CompletedAt ??
                financial.UpdatedAt;

        private static IQueryable<Booking>
            ApplyBookingIdentityFilters(
                IQueryable<Booking> query,
                int ownerId,
                FinancialReportFilterDto filter,
                bool applyBookingState,
                bool applyPaymentStatus,
                bool applyCommissionStatus)
        {
            query = query.Where(booking =>
                booking.Place.OwnerId == ownerId);

            if (filter.PlaceId.HasValue)
            {
                var placeId = filter.PlaceId.Value;
                query = query.Where(booking =>
                    booking.PlaceId == placeId);
            }

            if (filter.BookingId.HasValue)
            {
                var bookingId = filter.BookingId.Value;
                query = query.Where(booking =>
                    booking.BookingId == bookingId);
            }

            if (filter.CustomerId.HasValue)
            {
                var customerId = filter.CustomerId.Value;
                query = query.Where(booking =>
                    booking.CustomerId == customerId);
            }

            if (
                applyBookingState &&
                !string.IsNullOrWhiteSpace(filter.BookingState))
            {
                var bookingState = filter.BookingState;
                query = query.Where(booking =>
                    booking.BookingState == bookingState);
            }

            if (
                applyPaymentStatus &&
                !string.IsNullOrWhiteSpace(filter.PaymentStatus))
            {
                var paymentStatus = filter.PaymentStatus;
                query = query.Where(booking =>
                    booking.PaymentStatus == paymentStatus);
            }

            if (
                applyCommissionStatus &&
                !string.IsNullOrWhiteSpace(filter.CommissionStatus))
            {
                var commissionStatus = filter.CommissionStatus;
                query = query.Where(booking =>
                    booking.FinancialRecord != null &&
                    booking.FinancialRecord.CommissionStatus == commissionStatus);
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
            {
                var reference = filter.ReferenceNumber;
                query = query.Where(booking =>
                    booking.FinancialRecord != null &&
                    booking.FinancialRecord.TransferReferenceNumber != null &&
                    booking.FinancialRecord.TransferReferenceNumber.Contains(reference));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search;

                if (int.TryParse(search, out var numericSearch))
                {
                    query = query.Where(booking =>
                        booking.BookingId == numericSearch ||
                        booking.CustomerId == numericSearch ||
                        booking.Place.PlaceNameAndDesc.Contains(search) ||
                        booking.Customer.FullName.Contains(search) ||
                        booking.Customer.Username.Contains(search) ||
                        (booking.Customer.PhoneNumber != null &&
                         booking.Customer.PhoneNumber.Contains(search)) ||
                        (booking.Customer.ContactInfo != null &&
                         booking.Customer.ContactInfo.Contains(search)) ||
                        (booking.FinancialRecord != null &&
                         booking.FinancialRecord.TransferReferenceNumber != null &&
                         booking.FinancialRecord.TransferReferenceNumber.Contains(search)));
                }
                else
                {
                    query = query.Where(booking =>
                        booking.Place.PlaceNameAndDesc.Contains(search) ||
                        booking.Customer.FullName.Contains(search) ||
                        booking.Customer.Username.Contains(search) ||
                        (booking.Customer.PhoneNumber != null &&
                         booking.Customer.PhoneNumber.Contains(search)) ||
                        (booking.Customer.ContactInfo != null &&
                         booking.Customer.ContactInfo.Contains(search)) ||
                        (booking.FinancialRecord != null &&
                         booking.FinancialRecord.TransferReferenceNumber != null &&
                         booking.FinancialRecord.TransferReferenceNumber.Contains(search)));
                }
            }

            return query;
        }

        private static IQueryable<Booking>
            ApplyBookingPeriodFilter(
                IQueryable<Booking> query,
                FinancialReportFilterDto filter)
        {
            if (
                IsState(
                    filter.DateScope,
                    OwnerFinancialDateScopes.StayStartDate))
            {
                if (filter.FromDate.HasValue)
                {
                    var fromDate = filter.FromDate.Value.Date;
                    query = query.Where(booking =>
                        booking.StartDate >= fromDate);
                }

                if (filter.ToDate.HasValue)
                {
                    var toExclusive = filter.ToDate.Value.Date.AddDays(1);
                    query = query.Where(booking =>
                        booking.StartDate < toExclusive);
                }

                return query;
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value.Date;
                query = query.Where(booking =>
                    booking.CreatedAt.HasValue &&
                    booking.CreatedAt.Value >= fromDate);
            }

            if (filter.ToDate.HasValue)
            {
                var toExclusive = filter.ToDate.Value.Date.AddDays(1);
                query = query.Where(booking =>
                    booking.CreatedAt.HasValue &&
                    booking.CreatedAt.Value < toExclusive);
            }

            return query;
        }

        private static IQueryable<RefundRequest>
            ApplyRefundIdentityFilters(
                IQueryable<RefundRequest> query,
                int ownerId,
                FinancialReportFilterDto filter)
        {
            query = query.Where(refund =>
                refund.Place.OwnerId == ownerId);

            if (filter.PlaceId.HasValue)
            {
                var placeId = filter.PlaceId.Value;
                query = query.Where(refund =>
                    refund.PlaceId == placeId);
            }

            if (filter.BookingId.HasValue)
            {
                var bookingId = filter.BookingId.Value;
                query = query.Where(refund =>
                    refund.BookingId == bookingId);
            }

            if (filter.CustomerId.HasValue)
            {
                var customerId = filter.CustomerId.Value;
                query = query.Where(refund =>
                    refund.CustomerId == customerId);
            }

            if (!string.IsNullOrWhiteSpace(filter.RefundStatus))
            {
                var status = filter.RefundStatus;
                query = query.Where(refund =>
                    refund.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
            {
                var reference = filter.ReferenceNumber;
                query = query.Where(refund =>
                    refund.ReferenceNumber != null &&
                    refund.ReferenceNumber.Contains(reference));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search;

                if (int.TryParse(search, out var numericSearch))
                {
                    query = query.Where(refund =>
                        refund.RefundRequestId == numericSearch ||
                        refund.BookingId == numericSearch ||
                        refund.CustomerId == numericSearch ||
                        refund.Place.PlaceNameAndDesc.Contains(search) ||
                        refund.Customer.FullName.Contains(search) ||
                        refund.Customer.Username.Contains(search) ||
                        (refund.ReferenceNumber != null &&
                         refund.ReferenceNumber.Contains(search)) ||
                        (refund.Reason != null &&
                         refund.Reason.Contains(search)));
                }
                else
                {
                    query = query.Where(refund =>
                        refund.Place.PlaceNameAndDesc.Contains(search) ||
                        refund.Customer.FullName.Contains(search) ||
                        refund.Customer.Username.Contains(search) ||
                        (refund.ReferenceNumber != null &&
                         refund.ReferenceNumber.Contains(search)) ||
                        (refund.Reason != null &&
                         refund.Reason.Contains(search)));
                }
            }

            return query;
        }

        private static IQueryable<CommissionTransaction>
            ApplyTransactionIdentityFilters(
                IQueryable<CommissionTransaction> query,
                int ownerId,
                FinancialReportFilterDto filter)
        {
            query = query.Where(transaction =>
                transaction.Place.OwnerId == ownerId);

            if (filter.PlaceId.HasValue)
            {
                var placeId = filter.PlaceId.Value;
                query = query.Where(transaction =>
                    transaction.PlaceId == placeId);
            }

            if (filter.BookingId.HasValue)
            {
                var bookingId = filter.BookingId.Value;
                query = query.Where(transaction =>
                    transaction.BookingId == bookingId);
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionType))
            {
                var type = filter.TransactionType;
                query = query.Where(transaction =>
                    transaction.TransactionType == type);
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionStatus))
            {
                var status = filter.TransactionStatus;
                query = query.Where(transaction =>
                    transaction.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
            {
                var reference = filter.ReferenceNumber;
                query = query.Where(transaction =>
                    transaction.ReferenceNumber != null &&
                    transaction.ReferenceNumber.Contains(reference));
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value.Date;
                query = query.Where(transaction =>
                    (transaction.CreatedAt.HasValue &&
                     transaction.CreatedAt.Value >= fromDate) ||
                    (!transaction.CreatedAt.HasValue &&
                     transaction.TransferDate.HasValue &&
                     transaction.TransferDate.Value >= fromDate));
            }

            if (filter.ToDate.HasValue)
            {
                var toExclusive = filter.ToDate.Value.Date.AddDays(1);
                query = query.Where(transaction =>
                    (transaction.CreatedAt.HasValue &&
                     transaction.CreatedAt.Value < toExclusive) ||
                    (!transaction.CreatedAt.HasValue &&
                     transaction.TransferDate.HasValue &&
                     transaction.TransferDate.Value < toExclusive));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search;

                if (int.TryParse(search, out var numericSearch))
                {
                    query = query.Where(transaction =>
                        transaction.CommissionTransactionId == numericSearch ||
                        transaction.BookingId == numericSearch ||
                        transaction.Place.PlaceNameAndDesc.Contains(search) ||
                        (transaction.ReferenceNumber != null &&
                         transaction.ReferenceNumber.Contains(search)) ||
                        (transaction.Notes != null &&
                         transaction.Notes.Contains(search)));
                }
                else
                {
                    query = query.Where(transaction =>
                        transaction.Place.PlaceNameAndDesc.Contains(search) ||
                        (transaction.ReferenceNumber != null &&
                         transaction.ReferenceNumber.Contains(search)) ||
                        (transaction.Notes != null &&
                         transaction.Notes.Contains(search)) ||
                        transaction.TransactionType.Contains(search) ||
                        transaction.Status.Contains(search));
                }
            }

            return query;
        }

        private async Task<Dictionary<int, BookingRefundAggregate>>
            LoadBookingRefundAggregatesAsync(
                IReadOnlyCollection<int> bookingIds,
                CancellationToken cancellationToken)
        {
            var result =
                new Dictionary<int, BookingRefundAggregate>();

            if (bookingIds.Count == 0)
            {
                return result;
            }

            var distinctIds = bookingIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            for (
                var offset = 0;
                offset < distinctIds.Length;
                offset += RefundLookupBatchSize)
            {
                var batch = distinctIds
                    .Skip(offset)
                    .Take(RefundLookupBatchSize)
                    .ToArray();

                var refunds = await _context.RefundRequests
                    .AsNoTracking()
                    .Where(refund => batch.Contains(refund.BookingId))
                    .Select(refund =>
                        new
                        {
                            refund.BookingId,
                            refund.Status,
                            refund.RefundAmount,
                            refund.CancellationFee,
                            refund.OwnerShare,
                            refund.PlatformCommission,
                            refund.CommissionReleased
                        })
                    .ToListAsync(cancellationToken);

                foreach (var refund in refunds)
                {
                    if (!result.TryGetValue(refund.BookingId, out var aggregate))
                    {
                        aggregate = new BookingRefundAggregate();
                        result[refund.BookingId] = aggregate;
                    }

                    if (IsState(refund.Status, FinancialRefundStatuses.Completed))
                    {
                        aggregate.CompletedRefundAmount +=
                            Math.Max(0m, refund.RefundAmount);

                        aggregate.CompletedCancellationFee +=
                            Math.Max(0m, refund.CancellationFee);

                        aggregate.CompletedOwnerShare +=
                            Math.Max(0m, refund.OwnerShare);

                        aggregate.CompletedPlatformCommission +=
                            Math.Max(0m, refund.PlatformCommission);

                        aggregate.CompletedCommissionReleased +=
                            Math.Max(0m, refund.CommissionReleased);

                        aggregate.CompletedCount += 1;
                    }
                    else if (
                        IsState(refund.Status, FinancialRefundStatuses.Pending) ||
                        IsState(refund.Status, FinancialRefundStatuses.Processing))
                    {
                        aggregate.PendingRefundAmount +=
                            Math.Max(0m, refund.RefundAmount);

                        aggregate.PendingCount += 1;
                    }
                }
            }

            return result;
        }

        private async Task<Dictionary<int, BookingCommissionMovementAggregate>>
            LoadBookingCommissionMovementAggregatesAsync(
                IReadOnlyCollection<int> bookingIds,
                CancellationToken cancellationToken)
        {
            var result =
                new Dictionary<int, BookingCommissionMovementAggregate>();

            var distinctIds = bookingIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (distinctIds.Length == 0)
            {
                return result;
            }

            for (
                var offset = 0;
                offset < distinctIds.Length;
                offset += RefundLookupBatchSize)
            {
                var batch = distinctIds
                    .Skip(offset)
                    .Take(RefundLookupBatchSize)
                    .ToArray();

                var movements = await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        transaction.BookingId.HasValue &&
                        batch.Contains(transaction.BookingId.Value) &&
                        transaction.Status ==
                            FinancialTransactionStatuses.Completed &&
                        (
                            transaction.TransactionType ==
                                FinancialTransactionTypes.CommissionHold ||
                            transaction.TransactionType ==
                                FinancialTransactionTypes.CommissionCharge ||
                            transaction.TransactionType ==
                                FinancialTransactionTypes.CommissionRelease ||
                            transaction.TransactionType ==
                                FinancialTransactionTypes.Debt
                        ))
                    .Select(transaction =>
                        new
                        {
                            BookingId = transaction.BookingId!.Value,
                            transaction.CommissionTransactionId,
                            transaction.TransactionType,
                            transaction.Amount,
                            transaction.DebtBefore,
                            transaction.DebtAfter,
                            transaction.CreatedAt,
                            transaction.TransferDate,
                            transaction.ReferenceNumber
                        })
                    .ToListAsync(cancellationToken);

                foreach (var movement in movements)
                {
                    if (!result.TryGetValue(
                            movement.BookingId,
                            out var aggregate))
                    {
                        aggregate =
                            new BookingCommissionMovementAggregate();

                        result[movement.BookingId] =
                            aggregate;
                    }

                    var amount =
                        Math.Max(0m, movement.Amount);

                    if (IsState(
                            movement.TransactionType,
                            FinancialTransactionTypes.CommissionHold))
                    {
                        aggregate.HoldMovementsAmount += amount;
                    }
                    else if (IsState(
                                 movement.TransactionType,
                                 FinancialTransactionTypes.CommissionCharge))
                    {
                        aggregate.ChargedAmount += amount;
                    }
                    else if (IsState(
                                 movement.TransactionType,
                                 FinancialTransactionTypes.CommissionRelease))
                    {
                        aggregate.ReleasedAmount += amount;
                    }

                    var debtIncrease = Math.Max(
                        0m,
                        movement.DebtAfter - movement.DebtBefore);

                    if (debtIncrease > 0m)
                    {
                        aggregate.DebtCreatedAmount +=
                            debtIncrease;
                    }
                    else if (IsState(
                                 movement.TransactionType,
                                 FinancialTransactionTypes.Debt))
                    {
                        aggregate.DebtCreatedAmount += amount;
                    }

                    var activityDate =
                        movement.CreatedAt ??
                        movement.TransferDate;

                    if (
                        activityDate.HasValue &&
                        (
                            !aggregate.LastActivityAt.HasValue ||
                            activityDate.Value >
                                aggregate.LastActivityAt.Value
                        ))
                    {
                        aggregate.LastActivityAt =
                            activityDate;

                        aggregate.LastReferenceNumber =
                            string.IsNullOrWhiteSpace(
                                movement.ReferenceNumber)
                                ? aggregate.LastReferenceNumber
                                : movement.ReferenceNumber.Trim();
                    }
                }
            }

            return result;
        }

        private static decimal GetChargedCommissionAmount(
            FinancialRecord? financial,
            BookingCommissionMovementAggregate? movements)
        {
            if (movements?.ChargedAmount > 0m)
            {
                return movements.ChargedAmount;
            }

            return financial != null &&
                IsState(
                    financial.CommissionStatus,
                    FinancialCommissionStatuses.Charged)
                    ? Math.Max(0m, financial.PlatformCommission)
                    : 0m;
        }

        private static decimal GetHeldCommissionAmount(
            FinancialRecord? financial,
            BookingCommissionMovementAggregate? movements)
        {
            if (movements?.OutstandingHeldAmount > 0m)
            {
                return movements.OutstandingHeldAmount;
            }

            return financial != null &&
                (
                    IsState(
                        financial.CommissionStatus,
                        FinancialCommissionStatuses.Held) ||
                    IsState(
                        financial.CommissionStatus,
                        FinancialCommissionStatuses.Debt)
                )
                    ? Math.Max(0m, financial.PlatformCommission)
                    : 0m;
        }

        private static decimal GetReleasedCommissionAmount(
            FinancialRecord? financial,
            BookingCommissionMovementAggregate? movements)
        {
            if (movements?.ReleasedAmount > 0m)
            {
                return movements.ReleasedAmount;
            }

            return financial != null &&
                IsState(
                    financial.CommissionStatus,
                    FinancialCommissionStatuses.Released)
                    ? Math.Max(0m, financial.PlatformCommission)
                    : 0m;
        }

        private static decimal GetCommissionDebtAmount(
            FinancialRecord? financial,
            BookingCommissionMovementAggregate? movements)
        {
            if (
                financial == null ||
                !IsState(
                    financial.CommissionStatus,
                    FinancialCommissionStatuses.Debt)
            )
            {
                return 0m;
            }

            return movements?.DebtCreatedAmount > 0m
                ? Math.Min(
                    Math.Max(0m, financial.PlatformCommission),
                    movements.DebtCreatedAmount)
                : Math.Max(0m, financial.PlatformCommission);
        }

        private static FinancialReportRowDto MapBookingRevenueRow(
            Booking booking,
            BookingRefundAggregate? refund,
            BookingCommissionMovementAggregate? commissionMovements)
        {
            var financial = booking.FinancialRecord;
            var actualOnline = GetActualOnlineAmount(booking);
            var actualProperty = GetActualPropertyAmount(booking);
            var actualCollected = actualOnline + actualProperty;
            var completedRefund =
                Math.Max(0m, refund?.CompletedRefundAmount ?? 0m);
            var chargedCommission =
                GetChargedCommissionAmount(
                    financial,
                    commissionMovements);

            var actualRevenue = IsCancelledBookingState(booking.BookingState)
                ? 0m
                : Math.Max(
                    0m,
                    actualCollected -
                    completedRefund -
                    chargedCommission);

            return new FinancialReportRowDto
            {
                RecordId = booking.BookingId,
                RecordType = OwnerFinancialReportTypes.Bookings,
                RecordTypeLabel = GetFinancialReportTypeLabel(OwnerFinancialReportTypes.Bookings),
                RecordNumber = string.Concat("BKG-", booking.BookingId.ToString("D8", CultureInfo.InvariantCulture)),
                RecordDate = booking.CreatedAt ?? booking.StartDate,
                BookingId = booking.BookingId,
                CustomerId = booking.CustomerId,
                CustomerName = FirstNonEmpty("عميل", booking.Customer?.FullName, booking.Customer?.Username),
                PlaceId = booking.PlaceId,
                PlaceName = FirstNonEmpty("منشأة غير معروفة", booking.Place?.PlaceNameAndDesc),
                PlaceImageUrl = FirstNonEmpty(string.Empty, booking.Place?.CoverImage, booking.Place?.LogoImage),
                BookingCreatedAt = booking.CreatedAt,
                BookingStartDate = booking.StartDate,
                BookingEndDate = booking.EndDate,
                BookingState = booking.BookingState?.Trim() ?? string.Empty,
                BookingStateLabel = GetReportBookingStateLabel(booking.BookingState),
                PaymentStatus = booking.PaymentStatus?.Trim() ?? string.Empty,
                PaymentStatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                CommissionStatus = financial?.CommissionStatus?.Trim() ?? string.Empty,
                CommissionStatusLabel = GetReportCommissionStatusLabel(financial?.CommissionStatus),
                Status = booking.BookingState?.Trim() ?? string.Empty,
                StatusLabel = GetReportBookingStateLabel(booking.BookingState),
                ReferenceNumber = financial?.TransferReferenceNumber?.Trim(),
                Description = "القيمة المالية للحجز",
                PrimaryAmount = Math.Max(0m, booking.TotalPrice),
                OriginalTotalPrice = booking.OriginalTotalPrice > 0m
                    ? booking.OriginalTotalPrice
                    : booking.TotalPrice + booking.DiscountAmount,
                DiscountAmount = Math.Max(0m, booking.DiscountAmount),
                PromotionId = booking.PromotionId,
                PromotionTitleSnapshot = booking.PromotionTitleSnapshot?.Trim(),
                PromotionCodeSnapshot = booking.PromotionCodeSnapshot?.Trim(),
                DiscountTypeSnapshot = booking.DiscountTypeSnapshot?.Trim(),
                DiscountValueSnapshot = booking.DiscountValueSnapshot,
                TotalPrice = Math.Max(0m, booking.TotalPrice),
                DepositAmount = Math.Max(0m, financial?.DepositAmount ?? 0m),
                AmountPaidOnline = Math.Max(0m, financial?.AmountPaidOnline ?? 0m),
                AmountCollectedAtProperty = Math.Max(0m, financial?.AmountCollectedAtProperty ?? 0m),
                ActualPaidOnline = actualOnline,
                ActualCollectedAtProperty = actualProperty,
                ActualCollectedAmount = actualCollected,
                RemainingAmount = Math.Max(0m, financial?.AmountToPayAtProperty ?? booking.TotalPrice - actualCollected),
                ActualRevenue = actualRevenue,
                PlatformCommission = Math.Max(0m, financial?.PlatformCommission ?? 0m),
                CommissionHeld = GetHeldCommissionAmount(financial, commissionMovements),
                CommissionCharged = chargedCommission,
                CommissionReleased = GetReleasedCommissionAmount(financial, commissionMovements),
                CommissionDebt = GetCommissionDebtAmount(financial, commissionMovements),
                CommissionOutstanding = Math.Max(
                    0m,
                    Math.Max(0m, financial?.PlatformCommission ?? 0m) -
                    chargedCommission -
                    GetReleasedCommissionAmount(financial, commissionMovements)),
                RefundAmount = completedRefund,
                RequestedRefundAmount = completedRefund + Math.Max(0m, refund?.PendingRefundAmount ?? 0m),
                CancellationFee = Math.Max(0m, refund?.CompletedCancellationFee ?? 0m),
                OwnerShare = Math.Max(0m, refund?.CompletedOwnerShare ?? 0m),
                NetOwnerAmount = actualRevenue
            };
        }

        private async Task<List<FinancialReportRowDto>>
            BuildBookingsReportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var query = ApplyBookingIdentityFilters(
                _context.Bookings.AsNoTracking(),
                ownerId,
                filter,
                applyBookingState: true,
                applyPaymentStatus: true,
                applyCommissionStatus: false);

            query = ApplyBookingPeriodFilter(query, filter);

            var loadedQuery = query
                .Include(booking => booking.Customer)
                .Include(booking => booking.Place)
                .Include(booking => booking.FinancialRecord)
                .AsSplitQuery();

            List<Booking> bookings;

            if (IsState(
                    filter.DateScope,
                    OwnerFinancialDateScopes.StayStartDate))
            {
                bookings = await loadedQuery
                    .OrderByDescending(booking => booking.StartDate)
                    .ThenByDescending(booking => booking.BookingId)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                bookings = await loadedQuery
                    .OrderByDescending(booking =>
                        booking.CreatedAt ?? booking.StartDate)
                    .ThenByDescending(booking => booking.BookingId)
                    .ToListAsync(cancellationToken);
            }

            var bookingIds = bookings
                .Select(booking => booking.BookingId)
                .ToArray();

            var refunds = await LoadBookingRefundAggregatesAsync(
                bookingIds,
                cancellationToken);

            var commissionMovements =
                await LoadBookingCommissionMovementAggregatesAsync(
                    bookingIds,
                    cancellationToken);

            return bookings
                .Select(booking =>
                {
                    refunds.TryGetValue(
                        booking.BookingId,
                        out var refundAggregate);

                    commissionMovements.TryGetValue(
                        booking.BookingId,
                        out var commissionAggregate);

                    var row = MapBookingRevenueRow(
                        booking,
                        refundAggregate,
                        commissionAggregate);

                    row.RecordDate =
                        IsState(
                            filter.DateScope,
                            OwnerFinancialDateScopes.StayStartDate)
                            ? booking.StartDate
                            : booking.CreatedAt ?? booking.StartDate;

                    return row;
                })
                .ToList();
        }

        private async Task<List<FinancialReportRowDto>>
            BuildPaymentsReportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var query = ApplyBookingIdentityFilters(
                    _context.Bookings.AsNoTracking(),
                    ownerId,
                    filter,
                    applyBookingState: false,
                    applyPaymentStatus: true,
                    applyCommissionStatus: false)
                .Where(booking => booking.FinancialRecord != null);

            var bookings = await query
                .Include(booking => booking.Customer)
                .Include(booking => booking.Place)
                .Include(booking => booking.FinancialRecord)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var rows = new List<FinancialReportRowDto>();

            foreach (var booking in bookings)
            {
                var financial = booking.FinancialRecord;

                if (financial == null)
                {
                    continue;
                }

                var onlineDate = GetOnlinePaymentDate(booking, financial);
                var propertyDate = GetPropertyCollectionDate(financial);

                var paymentChannel =
                    filter.PaymentChannel ??
                    OwnerFinancialPaymentChannels.All;

                DateTime? activityDate;

                if (IsState(paymentChannel, OwnerFinancialPaymentChannels.Online))
                {
                    activityDate = onlineDate;
                }
                else if (IsState(paymentChannel, OwnerFinancialPaymentChannels.AtProperty))
                {
                    activityDate = propertyDate;
                }
                else if (IsState(filter.DateScope, OwnerFinancialDateScopes.OnlinePaymentDate))
                {
                    activityDate = onlineDate;
                }
                else if (IsState(filter.DateScope, OwnerFinancialDateScopes.PropertyCollectionDate))
                {
                    activityDate = propertyDate;
                }
                else
                {
                    activityDate = LatestDate(onlineDate, propertyDate);
                }

                if (!IsDateWithinFilter(activityDate, filter))
                {
                    continue;
                }

                var rawOnline = Math.Max(0m, financial.AmountPaidOnline);
                var rawProperty = Math.Max(0m, financial.AmountCollectedAtProperty);
                var actualOnline = GetActualOnlineAmount(booking);
                var actualProperty = GetActualPropertyAmount(booking);

                if (IsState(paymentChannel, OwnerFinancialPaymentChannels.Online))
                {
                    rawProperty = 0m;
                    actualProperty = 0m;
                }
                else if (IsState(paymentChannel, OwnerFinancialPaymentChannels.AtProperty))
                {
                    rawOnline = 0m;
                    actualOnline = 0m;
                }

                var actualTotal = actualOnline + actualProperty;
                var remaining = Math.Max(0m, booking.TotalPrice - actualTotal);

                rows.Add(new FinancialReportRowDto
                {
                    RecordId = financial.RecordId,
                    RecordType = OwnerFinancialReportTypes.Payments,
                    RecordTypeLabel = GetFinancialReportTypeLabel(OwnerFinancialReportTypes.Payments),
                    RecordNumber = string.Concat("PAY-", financial.RecordId.ToString("D8", CultureInfo.InvariantCulture)),
                    RecordDate = activityDate ?? financial.UpdatedAt,
                    BookingId = booking.BookingId,
                    CustomerId = booking.CustomerId,
                    CustomerName = FirstNonEmpty("عميل", booking.Customer?.FullName, booking.Customer?.Username),
                    PlaceId = booking.PlaceId,
                    PlaceName = FirstNonEmpty("منشأة غير معروفة", booking.Place?.PlaceNameAndDesc),
                    PlaceImageUrl = FirstNonEmpty(string.Empty, booking.Place?.CoverImage, booking.Place?.LogoImage),
                    BookingCreatedAt = booking.CreatedAt,
                    BookingStartDate = booking.StartDate,
                    BookingEndDate = booking.EndDate,
                    PaymentDate = onlineDate,
                    CollectionDate = propertyDate,
                    BookingState = booking.BookingState?.Trim() ?? string.Empty,
                    BookingStateLabel = GetReportBookingStateLabel(booking.BookingState),
                    PaymentStatus = booking.PaymentStatus?.Trim() ?? string.Empty,
                    PaymentStatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                    PaymentChannel = paymentChannel,
                    PaymentChannelLabel = GetPaymentChannelLabel(paymentChannel),
                    Status = booking.PaymentStatus?.Trim() ?? string.Empty,
                    StatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                    ReferenceNumber = financial.TransferReferenceNumber?.Trim(),
                    Description = "سجل الدفع والتحصيل المرتبط بالحجز",
                    PrimaryAmount = actualTotal,
                    OriginalTotalPrice = booking.OriginalTotalPrice > 0m
                    ? booking.OriginalTotalPrice
                    : booking.TotalPrice + booking.DiscountAmount,
                    DiscountAmount = Math.Max(0m, booking.DiscountAmount),
                    PromotionId = booking.PromotionId,
                    PromotionTitleSnapshot = booking.PromotionTitleSnapshot?.Trim(),
                    PromotionCodeSnapshot = booking.PromotionCodeSnapshot?.Trim(),
                    DiscountTypeSnapshot = booking.DiscountTypeSnapshot?.Trim(),
                    DiscountValueSnapshot = booking.DiscountValueSnapshot,
                    TotalPrice = Math.Max(0m, booking.TotalPrice),
                    DepositAmount = Math.Max(0m, financial.DepositAmount),
                    AmountPaidOnline = rawOnline,
                    AmountCollectedAtProperty = rawProperty,
                    ActualPaidOnline = actualOnline,
                    ActualCollectedAtProperty = actualProperty,
                    ActualCollectedAmount = actualTotal,
                    RemainingAmount = remaining,
                    ActualRevenue = actualTotal,
                    NetOwnerAmount = actualTotal
                });
            }

            return rows
                .OrderByDescending(row => row.RecordDate)
                .ThenByDescending(row => row.RecordId)
                .ToList();
        }

        private async Task<List<FinancialReportRowDto>>
            BuildCommissionsReportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var query = ApplyBookingIdentityFilters(
                    _context.Bookings.AsNoTracking(),
                    ownerId,
                    filter,
                    applyBookingState: false,
                    applyPaymentStatus: false,
                    applyCommissionStatus: true)
                .Where(booking => booking.FinancialRecord != null);

            var bookings = await query
                .Include(booking => booking.Customer)
                .Include(booking => booking.Place)
                .Include(booking => booking.FinancialRecord)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var movementMap =
                await LoadBookingCommissionMovementAggregatesAsync(
                    bookings
                        .Select(booking => booking.BookingId)
                        .ToArray(),
                    cancellationToken);

            var rows = new List<FinancialReportRowDto>();

            foreach (var booking in bookings)
            {
                var financial = booking.FinancialRecord;

                if (financial == null)
                {
                    continue;
                }

                movementMap.TryGetValue(
                    booking.BookingId,
                    out var movements);

                var activityDate =
                    movements?.LastActivityAt ??
                    GetCommissionActivityDate(financial);

                if (!IsDateWithinFilter(activityDate, filter))
                {
                    continue;
                }

                var calculated =
                    Math.Max(0m, financial.PlatformCommission);

                var held = GetHeldCommissionAmount(
                    financial,
                    movements);

                var charged = GetChargedCommissionAmount(
                    financial,
                    movements);

                var released = GetReleasedCommissionAmount(
                    financial,
                    movements);

                var debt = GetCommissionDebtAmount(
                    financial,
                    movements);

                var outstanding = Math.Max(
                    0m,
                    calculated - charged - released);

                rows.Add(new FinancialReportRowDto
                {
                    RecordId = financial.RecordId,
                    RecordType = OwnerFinancialReportTypes.Commissions,
                    RecordTypeLabel = GetFinancialReportTypeLabel(OwnerFinancialReportTypes.Commissions),
                    RecordNumber = string.Concat("COM-", financial.RecordId.ToString("D8", CultureInfo.InvariantCulture)),
                    RecordDate = activityDate,
                    BookingId = booking.BookingId,
                    CustomerId = booking.CustomerId,
                    CustomerName = FirstNonEmpty("عميل", booking.Customer?.FullName, booking.Customer?.Username),
                    PlaceId = booking.PlaceId,
                    PlaceName = FirstNonEmpty("منشأة غير معروفة", booking.Place?.PlaceNameAndDesc),
                    PlaceImageUrl = FirstNonEmpty(string.Empty, booking.Place?.CoverImage, booking.Place?.LogoImage),
                    BookingCreatedAt = booking.CreatedAt,
                    BookingStartDate = booking.StartDate,
                    BookingEndDate = booking.EndDate,
                    CommissionDate = activityDate,
                    BookingState = booking.BookingState?.Trim() ?? string.Empty,
                    BookingStateLabel = GetReportBookingStateLabel(booking.BookingState),
                    CommissionStatus = financial.CommissionStatus?.Trim() ?? string.Empty,
                    CommissionStatusLabel = GetReportCommissionStatusLabel(financial.CommissionStatus),
                    Status = financial.CommissionStatus?.Trim() ?? string.Empty,
                    StatusLabel = GetReportCommissionStatusLabel(financial.CommissionStatus),
                    ReferenceNumber = FirstNonEmpty(
                        string.Empty,
                        movements?.LastReferenceNumber,
                        financial.TransferReferenceNumber),
                    Description = "سجل عمولة مرتبط بالحجز",
                    PrimaryAmount = calculated,
                    OriginalTotalPrice = booking.OriginalTotalPrice > 0m
                    ? booking.OriginalTotalPrice
                    : booking.TotalPrice + booking.DiscountAmount,
                    DiscountAmount = Math.Max(0m, booking.DiscountAmount),
                    PromotionId = booking.PromotionId,
                    PromotionTitleSnapshot = booking.PromotionTitleSnapshot?.Trim(),
                    PromotionCodeSnapshot = booking.PromotionCodeSnapshot?.Trim(),
                    DiscountTypeSnapshot = booking.DiscountTypeSnapshot?.Trim(),
                    DiscountValueSnapshot = booking.DiscountValueSnapshot,
                    TotalPrice = Math.Max(0m, booking.TotalPrice),
                    PlatformCommission = calculated,
                    CommissionHeld = held,
                    CommissionCharged = charged,
                    CommissionReleased = released,
                    CommissionDebt = debt,
                    CommissionOutstanding = outstanding,
                    NetOwnerAmount = Math.Max(0m, booking.TotalPrice - charged)
                });
            }

            return rows
                .OrderByDescending(row => row.RecordDate)
                .ThenByDescending(row => row.RecordId)
                .ToList();
        }

        private async Task<List<FinancialReportRowDto>>
            BuildRefundsReportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var query = ApplyRefundIdentityFilters(
                _context.RefundRequests.AsNoTracking(),
                ownerId,
                filter);

            var refunds = await query
                .Include(refund => refund.Booking)
                    .ThenInclude(booking => booking.FinancialRecord)
                .Include(refund => refund.Customer)
                .Include(refund => refund.Place)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var rows = new List<FinancialReportRowDto>();

            foreach (var refund in refunds)
            {
                var activityDate =
                    IsState(
                        filter.DateScope,
                        OwnerFinancialDateScopes.RefundProcessedDate)
                        ? refund.ProcessedAt
                        : refund.RequestedAt;

                if (!IsDateWithinFilter(activityDate, filter))
                {
                    continue;
                }

                var completed =
                    IsState(refund.Status, FinancialRefundStatuses.Completed);

                var requestedAmount = Math.Max(0m, refund.RefundAmount);
                var actualRefunded = completed
                    ? requestedAmount
                    : 0m;

                rows.Add(new FinancialReportRowDto
                {
                    RecordId = refund.RefundRequestId,
                    RecordType = OwnerFinancialReportTypes.Refunds,
                    RecordTypeLabel = GetFinancialReportTypeLabel(OwnerFinancialReportTypes.Refunds),
                    RecordNumber = string.Concat("REF-", refund.RefundRequestId.ToString("D8", CultureInfo.InvariantCulture)),
                    RecordDate = activityDate ?? refund.RequestedAt ?? DateTime.MinValue,
                    BookingId = refund.BookingId,
                    CustomerId = refund.CustomerId,
                    CustomerName = FirstNonEmpty("عميل", refund.Customer?.FullName, refund.Customer?.Username),
                    PlaceId = refund.PlaceId,
                    PlaceName = FirstNonEmpty("منشأة غير معروفة", refund.Place?.PlaceNameAndDesc),
                    PlaceImageUrl = FirstNonEmpty(string.Empty, refund.Place?.CoverImage, refund.Place?.LogoImage),
                    BookingCreatedAt = refund.Booking?.CreatedAt,
                    BookingStartDate = refund.Booking?.StartDate,
                    BookingEndDate = refund.Booking?.EndDate,
                    RequestedAt = refund.RequestedAt,
                    ProcessedAt = refund.ProcessedAt,
                    BookingState = refund.Booking?.BookingState?.Trim() ?? string.Empty,
                    BookingStateLabel = GetReportBookingStateLabel(refund.Booking?.BookingState),
                    PaymentStatus = refund.Booking?.PaymentStatus?.Trim() ?? string.Empty,
                    PaymentStatusLabel = GetReportPaymentStatusLabel(refund.Booking?.PaymentStatus),
                    CommissionStatus = refund.Booking?.FinancialRecord?.CommissionStatus?.Trim() ?? string.Empty,
                    CommissionStatusLabel = GetReportCommissionStatusLabel(refund.Booking?.FinancialRecord?.CommissionStatus),
                    Status = refund.Status?.Trim() ?? FinancialRefundStatuses.Pending,
                    StatusLabel = GetRefundStatusLabel(refund.Status),
                    ReferenceNumber = refund.ReferenceNumber?.Trim(),
                    Description = FirstNonEmpty("طلب استرداد", refund.Reason, refund.Notes),
                    PrimaryAmount = actualRefunded,
                    OriginalTotalPrice = refund.Booking == null
                            ? 0m
                            : refund.Booking.OriginalTotalPrice > 0m
                                ? refund.Booking.OriginalTotalPrice
                                : refund.Booking.TotalPrice + refund.Booking.DiscountAmount,
                    DiscountAmount = Math.Max(0m, refund.Booking?.DiscountAmount ?? 0m),
                    PromotionId = refund.Booking?.PromotionId,
                    PromotionTitleSnapshot = refund.Booking?.PromotionTitleSnapshot?.Trim(),
                    PromotionCodeSnapshot = refund.Booking?.PromotionCodeSnapshot?.Trim(),
                    DiscountTypeSnapshot = refund.Booking?.DiscountTypeSnapshot?.Trim(),
                    DiscountValueSnapshot = refund.Booking?.DiscountValueSnapshot,
                    TotalPrice = Math.Max(0m, refund.Booking?.TotalPrice ?? 0m),
                    DepositAmount = Math.Max(0m, refund.DepositPaid),
                    AmountPaidOnline = Math.Max(
                        0m,
                        refund.Booking?.FinancialRecord?.AmountPaidOnline ?? 0m),
                    ActualPaidOnline = Math.Max(
                        0m,
                        refund.Booking?.FinancialRecord?.AmountPaidOnline ?? 0m),
                    RequestedRefundAmount = requestedAmount,
                    RefundAmount = actualRefunded,
                    CancellationFee = completed ? Math.Max(0m, refund.CancellationFee) : 0m,
                    OwnerShare = completed ? Math.Max(0m, refund.OwnerShare) : 0m,
                    PlatformCommission = completed ? Math.Max(0m, refund.PlatformCommission) : 0m,
                    CommissionReleased = completed ? Math.Max(0m, refund.CommissionReleased) : 0m,
                    NetOwnerAmount = completed ? Math.Max(0m, refund.OwnerShare) : 0m
                });
            }

            return rows
                .OrderByDescending(row => row.RecordDate)
                .ThenByDescending(row => row.RecordId)
                .ToList();
        }

        private async Task<List<FinancialReportRowDto>>
            BuildTransactionsReportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var query = ApplyTransactionIdentityFilters(
                _context.CommissionTransactions.AsNoTracking(),
                ownerId,
                filter);

            var transactions = await query
                .Include(transaction => transaction.Place)
                .Include(transaction => transaction.PlatformWallet)
                    .ThenInclude(wallet => wallet.Wallet)
                .AsSplitQuery()
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.CommissionTransactionId)
                .ToListAsync(cancellationToken);

            return transactions
                .Select(transaction =>
                {
                    var effectiveDate =
                        transaction.CreatedAt ??
                        transaction.TransferDate ??
                        DateTime.MinValue;

                    return new FinancialReportRowDto
                    {
                        RecordId = transaction.CommissionTransactionId,
                        RecordType = OwnerFinancialReportTypes.Transactions,
                        RecordTypeLabel = GetFinancialReportTypeLabel(OwnerFinancialReportTypes.Transactions),
                        RecordNumber = string.Concat("TRX-", transaction.CommissionTransactionId.ToString("D8", CultureInfo.InvariantCulture)),
                        RecordDate = effectiveDate,
                        BookingId = transaction.BookingId,
                        PlaceId = transaction.PlaceId,
                        PlaceName = FirstNonEmpty("منشأة غير معروفة", transaction.Place?.PlaceNameAndDesc),
                        PlaceImageUrl = FirstNonEmpty(string.Empty, transaction.Place?.CoverImage, transaction.Place?.LogoImage),
                        Status = transaction.Status?.Trim() ?? string.Empty,
                        StatusLabel = GetTransactionStatusLabel(transaction.Status),
                        TransactionType = transaction.TransactionType?.Trim(),
                        TransactionTypeLabel = GetTransactionTypeLabel(transaction.TransactionType),
                        ReferenceNumber = transaction.ReferenceNumber?.Trim(),
                        Description = FirstNonEmpty("حركة مالية", transaction.Notes, transaction.PlatformWallet?.Wallet?.WalletName),
                        PrimaryAmount = Math.Max(0m, transaction.Amount),
                        TopUpAmount = IsState(transaction.TransactionType, FinancialTransactionTypes.TopUp)
                            ? Math.Max(0m, transaction.Amount)
                            : 0m,
                        AvailableBalanceBefore = transaction.AvailableBalanceBefore,
                        AvailableBalanceAfter = transaction.AvailableBalanceAfter,
                        HeldBalanceBefore = transaction.HeldBalanceBefore,
                        HeldBalanceAfter = transaction.HeldBalanceAfter,
                        DebtBefore = transaction.DebtBefore,
                        DebtAfter = transaction.DebtAfter,
                        CommissionHeld = IsState(transaction.TransactionType, FinancialTransactionTypes.CommissionHold)
                            ? Math.Max(0m, transaction.Amount)
                            : 0m,
                        CommissionCharged = IsState(transaction.TransactionType, FinancialTransactionTypes.CommissionCharge)
                            ? Math.Max(0m, transaction.Amount)
                            : 0m,
                        CommissionReleased = IsState(transaction.TransactionType, FinancialTransactionTypes.CommissionRelease)
                            ? Math.Max(0m, transaction.Amount)
                            : 0m,
                        CommissionDebt = IsState(transaction.TransactionType, FinancialTransactionTypes.Debt)
                            ? Math.Max(0m, transaction.Amount)
                            : 0m
                    };
                })
                .ToList();
        }

        private async Task<List<FinancialReportRowDto>>
            BuildComprehensiveReportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var query = ApplyBookingIdentityFilters(
                _context.Bookings.AsNoTracking(),
                ownerId,
                filter,
                applyBookingState: true,
                applyPaymentStatus: true,
                applyCommissionStatus: true);

            query = ApplyBookingPeriodFilter(query, filter);

            var loadedQuery = query
                .Include(booking => booking.Customer)
                .Include(booking => booking.Place)
                .Include(booking => booking.FinancialRecord)
                .AsSplitQuery();

            List<Booking> bookings;

            if (IsState(
                    filter.DateScope,
                    OwnerFinancialDateScopes.StayStartDate))
            {
                bookings = await loadedQuery
                    .OrderByDescending(booking => booking.StartDate)
                    .ThenByDescending(booking => booking.BookingId)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                bookings = await loadedQuery
                    .OrderByDescending(booking =>
                        booking.CreatedAt ?? booking.StartDate)
                    .ThenByDescending(booking => booking.BookingId)
                    .ToListAsync(cancellationToken);
            }

            var bookingIds = bookings
                .Select(booking => booking.BookingId)
                .ToArray();

            var refunds = await LoadBookingRefundAggregatesAsync(
                bookingIds,
                cancellationToken);

            var commissionMovements =
                await LoadBookingCommissionMovementAggregatesAsync(
                    bookingIds,
                    cancellationToken);

            return bookings
                .Select(booking =>
                {
                    refunds.TryGetValue(
                        booking.BookingId,
                        out var refundAggregate);

                    commissionMovements.TryGetValue(
                        booking.BookingId,
                        out var commissionAggregate);

                    var financial = booking.FinancialRecord;
                    var actualOnline = GetActualOnlineAmount(booking);
                    var actualProperty = GetActualPropertyAmount(booking);
                    var actualCollected = actualOnline + actualProperty;
                    var completedRefund = Math.Max(
                        0m,
                        refundAggregate?.CompletedRefundAmount ?? 0m);
                    var chargedCommission = GetChargedCommissionAmount(
                        financial,
                        commissionAggregate);
                    var releasedCommission = GetReleasedCommissionAmount(
                        financial,
                        commissionAggregate);
                    var netOwner = Math.Max(
                        0m,
                        actualCollected -
                        completedRefund -
                        chargedCommission);

                    return new FinancialReportRowDto
                    {
                        RecordId = booking.BookingId,
                        RecordType = OwnerFinancialReportTypes.Comprehensive,
                        RecordTypeLabel = GetFinancialReportTypeLabel(OwnerFinancialReportTypes.Comprehensive),
                        RecordNumber = string.Concat("BKG-", booking.BookingId.ToString("D8", CultureInfo.InvariantCulture)),
                        RecordDate = IsState(filter.DateScope, OwnerFinancialDateScopes.StayStartDate)
                            ? booking.StartDate
                            : booking.CreatedAt ?? booking.StartDate,
                        BookingId = booking.BookingId,
                        CustomerId = booking.CustomerId,
                        CustomerName = FirstNonEmpty("عميل", booking.Customer?.FullName, booking.Customer?.Username),
                        PlaceId = booking.PlaceId,
                        PlaceName = FirstNonEmpty("منشأة غير معروفة", booking.Place?.PlaceNameAndDesc),
                        PlaceImageUrl = FirstNonEmpty(string.Empty, booking.Place?.CoverImage, booking.Place?.LogoImage),
                        BookingCreatedAt = booking.CreatedAt,
                        BookingStartDate = booking.StartDate,
                        BookingEndDate = booking.EndDate,
                        PaymentDate = GetOnlinePaymentDate(booking, financial),
                        CollectionDate = GetPropertyCollectionDate(financial),
                        CommissionDate = commissionAggregate?.LastActivityAt ??
                            (financial == null
                                ? null
                                : GetCommissionActivityDate(financial)),
                        BookingState = booking.BookingState?.Trim() ?? string.Empty,
                        BookingStateLabel = GetReportBookingStateLabel(booking.BookingState),
                        PaymentStatus = booking.PaymentStatus?.Trim() ?? string.Empty,
                        PaymentStatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                        CommissionStatus = financial?.CommissionStatus?.Trim() ?? string.Empty,
                        CommissionStatusLabel = GetReportCommissionStatusLabel(financial?.CommissionStatus),
                        Status = booking.BookingState?.Trim() ?? string.Empty,
                        StatusLabel = GetReportBookingStateLabel(booking.BookingState),
                        ReferenceNumber = FirstNonEmpty(
                            string.Empty,
                            financial?.TransferReferenceNumber,
                            commissionAggregate?.LastReferenceNumber),
                        Description = "الكشف المالي الموحد للحجز",
                        PrimaryAmount = netOwner,
                        OriginalTotalPrice = booking.OriginalTotalPrice > 0m
                    ? booking.OriginalTotalPrice
                    : booking.TotalPrice + booking.DiscountAmount,
                        DiscountAmount = Math.Max(0m, booking.DiscountAmount),
                        PromotionId = booking.PromotionId,
                        PromotionTitleSnapshot = booking.PromotionTitleSnapshot?.Trim(),
                        PromotionCodeSnapshot = booking.PromotionCodeSnapshot?.Trim(),
                        DiscountTypeSnapshot = booking.DiscountTypeSnapshot?.Trim(),
                        DiscountValueSnapshot = booking.DiscountValueSnapshot,
                        TotalPrice = Math.Max(0m, booking.TotalPrice),
                        DepositAmount = Math.Max(0m, financial?.DepositAmount ?? 0m),
                        AmountPaidOnline = Math.Max(0m, financial?.AmountPaidOnline ?? 0m),
                        AmountCollectedAtProperty = Math.Max(0m, financial?.AmountCollectedAtProperty ?? 0m),
                        ActualPaidOnline = actualOnline,
                        ActualCollectedAtProperty = actualProperty,
                        ActualCollectedAmount = actualCollected,
                        RemainingAmount = Math.Max(0m, financial?.AmountToPayAtProperty ?? booking.TotalPrice - actualCollected),
                        ActualRevenue = netOwner,
                        PlatformCommission = Math.Max(0m, financial?.PlatformCommission ?? 0m),
                        CommissionHeld = GetHeldCommissionAmount(financial, commissionAggregate),
                        CommissionCharged = chargedCommission,
                        CommissionReleased = releasedCommission,
                        CommissionDebt = GetCommissionDebtAmount(financial, commissionAggregate),
                        CommissionOutstanding = Math.Max(
                            0m,
                            Math.Max(0m, financial?.PlatformCommission ?? 0m) -
                            chargedCommission -
                            releasedCommission),
                        RequestedRefundAmount = completedRefund +
                            Math.Max(0m, refundAggregate?.PendingRefundAmount ?? 0m),
                        RefundAmount = completedRefund,
                        CancellationFee = Math.Max(0m, refundAggregate?.CompletedCancellationFee ?? 0m),
                        OwnerShare = Math.Max(0m, refundAggregate?.CompletedOwnerShare ?? 0m),
                        NetOwnerAmount = netOwner
                    };
                })
                .ToList();
        }

        private async Task<ComprehensiveFinancialAnalyticsDto>
            BuildComprehensiveAnalyticsAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                IReadOnlyCollection<FinancialReportRowDto> rows,
                CancellationToken cancellationToken)
        {
            var movementFilter = new FinancialReportFilterDto
            {
                PlaceId = filter.PlaceId,
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                ReportType = OwnerFinancialReportTypes.Transactions,
                DateScope = OwnerFinancialDateScopes.TransactionDate,
                PageNumber = 1,
                PageSize = 25
            };

            var movementQuery = ApplyTransactionIdentityFilters(
                _context.CommissionTransactions.AsNoTracking(),
                ownerId,
                movementFilter);

            var movements = await movementQuery
                .Select(transaction =>
                    new
                    {
                        transaction.TransactionType,
                        transaction.Status,
                        transaction.Amount
                    })
                .ToListAsync(cancellationToken);

            var accountQuery = _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Where(account => account.Place.OwnerId == ownerId);

            if (filter.PlaceId.HasValue)
            {
                var placeId = filter.PlaceId.Value;
                accountQuery = accountQuery.Where(account =>
                    account.PlaceId == placeId);
            }

            var accountTotals = await accountQuery
                .GroupBy(account => 1)
                .Select(group =>
                    new
                    {
                        Available = group.Sum(account => account.AvailableBalance),
                        Held = group.Sum(account => account.HeldBalance),
                        Debt = group.Sum(account => account.DebtAmount)
                    })
                .FirstOrDefaultAsync(cancellationToken);

            var refundQuery = _context.RefundRequests
                .AsNoTracking()
                .Where(refund => refund.Place.OwnerId == ownerId);

            if (filter.PlaceId.HasValue)
            {
                var placeId = filter.PlaceId.Value;
                refundQuery = refundQuery.Where(refund =>
                    refund.PlaceId == placeId);
            }

            var refunds = await refundQuery
                .Select(refund =>
                    new
                    {
                        refund.Status,
                        refund.RefundAmount,
                        refund.RequestedAt,
                        refund.ProcessedAt
                    })
                .ToListAsync(cancellationToken);

            var refundsInPeriod = refunds
                .Where(refund =>
                {
                    var date = IsState(refund.Status, FinancialRefundStatuses.Completed)
                        ? refund.ProcessedAt ?? refund.RequestedAt
                        : refund.RequestedAt;

                    return IsDateWithinFilter(date, filter);
                })
                .ToList();

            var completedTopUps = movements
                .Where(item =>
                    IsState(item.TransactionType, FinancialTransactionTypes.TopUp) &&
                    IsState(item.Status, FinancialTransactionStatuses.Completed))
                .Sum(item => Math.Max(0m, item.Amount));

            var pendingTopUps = movements
                .Where(item =>
                    IsState(item.TransactionType, FinancialTransactionTypes.TopUp) &&
                    IsState(item.Status, FinancialTransactionStatuses.Pending))
                .Sum(item => Math.Max(0m, item.Amount));

            var commissionCharged = movements
                .Where(item =>
                    IsState(item.TransactionType, FinancialTransactionTypes.CommissionCharge) &&
                    IsState(item.Status, FinancialTransactionStatuses.Completed))
                .Sum(item => Math.Max(0m, item.Amount));

            return new ComprehensiveFinancialAnalyticsDto
            {
                CompletedTopUpsAmount = completedTopUps,
                PendingTopUpsAmount = pendingTopUps,
                PendingTopUpsCount = movements.Count(item =>
                    IsState(item.TransactionType, FinancialTransactionTypes.TopUp) &&
                    IsState(item.Status, FinancialTransactionStatuses.Pending)),
                CommissionHeldAmount = movements
                    .Where(item =>
                        IsState(item.TransactionType, FinancialTransactionTypes.CommissionHold) &&
                        IsState(item.Status, FinancialTransactionStatuses.Completed))
                    .Sum(item => Math.Max(0m, item.Amount)),
                CommissionChargedAmount = commissionCharged,
                CommissionReleasedAmount = movements
                    .Where(item =>
                        IsState(item.TransactionType, FinancialTransactionTypes.CommissionRelease) &&
                        IsState(item.Status, FinancialTransactionStatuses.Completed))
                    .Sum(item => Math.Max(0m, item.Amount)),
                CommissionDebtCreatedAmount = movements
                    .Where(item =>
                        IsState(item.TransactionType, FinancialTransactionTypes.Debt) &&
                        IsState(item.Status, FinancialTransactionStatuses.Completed))
                    .Sum(item => Math.Max(0m, item.Amount)),
                CurrentAvailableGuaranteeBalance = accountTotals?.Available ?? 0m,
                CurrentHeldGuaranteeBalance = accountTotals?.Held ?? 0m,
                CurrentDebt = accountTotals?.Debt ?? 0m,
                CompletedRefundAmount = refundsInPeriod
                    .Where(item => IsState(item.Status, FinancialRefundStatuses.Completed))
                    .Sum(item => Math.Max(0m, item.RefundAmount)),
                PendingRefundAmount = refundsInPeriod
                    .Where(item =>
                        IsState(item.Status, FinancialRefundStatuses.Pending) ||
                        IsState(item.Status, FinancialRefundStatuses.Processing))
                    .Sum(item => Math.Max(0m, item.RefundAmount)),
                CompletedRefundCount = refundsInPeriod.Count(item =>
                    IsState(item.Status, FinancialRefundStatuses.Completed)),
                PendingRefundCount = refundsInPeriod.Count(item =>
                    IsState(item.Status, FinancialRefundStatuses.Pending) ||
                    IsState(item.Status, FinancialRefundStatuses.Processing)),
                OperationalNetCashFlow = rows.Sum(row => row.NetOwnerAmount),
                GuaranteeNetFlow = completedTopUps - commissionCharged
            };
        }

        private static FinancialDocumentItemDto CreateBookingDocument(
            Booking booking,
            BookingRefundAggregate? refund,
            string documentType,
            DateTime documentDate,
            decimal primaryAmount,
            string description)
        {
            var financial = booking.FinancialRecord;
            var paidOnline = GetActualOnlineAmount(booking);
            var collected = GetActualPropertyAmount(booking);
            var refunded = Math.Max(0m, refund?.CompletedRefundAmount ?? 0m);
            var chargedCommission =
                financial != null &&
                IsState(financial.CommissionStatus, FinancialCommissionStatuses.Charged)
                    ? Math.Max(0m, financial.PlatformCommission)
                    : 0m;
            var netOwner = Math.Max(
                0m,
                paidOnline + collected - refunded - chargedCommission);

            return new FinancialDocumentItemDto
            {
                DocumentKey = string.Concat(documentType, ":", booking.BookingId),
                SourceId = booking.BookingId,
                BookingId = booking.BookingId,
                CustomerId = booking.CustomerId,
                PlaceId = booking.PlaceId,
                DocumentNumber = BuildFinancialDocumentNumber(documentType, booking.BookingId),
                DocumentType = documentType,
                DocumentTypeLabel = GetFinancialDocumentTypeLabel(documentType),
                DocumentDate = documentDate,
                CustomerName = FirstNonEmpty("عميل", booking.Customer?.FullName, booking.Customer?.Username),
                PlaceName = FirstNonEmpty("منشأة غير معروفة", booking.Place?.PlaceNameAndDesc),
                PlaceImageUrl = FirstNonEmpty(string.Empty, booking.Place?.CoverImage, booking.Place?.LogoImage),
                PrimaryAmount = Math.Max(0m, primaryAmount),
                OriginalTotalPrice = booking.OriginalTotalPrice > 0m
                    ? booking.OriginalTotalPrice
                    : booking.TotalPrice + booking.DiscountAmount,
                DiscountAmount = Math.Max(0m, booking.DiscountAmount),
                PromotionId = booking.PromotionId,
                PromotionTitleSnapshot = booking.PromotionTitleSnapshot?.Trim(),
                PromotionCodeSnapshot = booking.PromotionCodeSnapshot?.Trim(),
                DiscountTypeSnapshot = booking.DiscountTypeSnapshot?.Trim(),
                DiscountValueSnapshot = booking.DiscountValueSnapshot,
                TotalPrice = Math.Max(0m, booking.TotalPrice),
                DepositAmount = Math.Max(0m, financial?.DepositAmount ?? 0m),
                AmountPaidOnline = paidOnline,
                AmountCollectedAtProperty = collected,
                RemainingAmount = Math.Max(0m, financial?.AmountToPayAtProperty ?? booking.TotalPrice - paidOnline - collected),
                PlatformCommission = chargedCommission,
                RefundedAmount = refunded,
                CancellationFee = Math.Max(0m, refund?.CompletedCancellationFee ?? 0m),
                OwnerShare = Math.Max(0m, refund?.CompletedOwnerShare ?? 0m),
                NetOwnerAmount = netOwner,
                BookingState = booking.BookingState?.Trim() ?? string.Empty,
                BookingStateLabel = GetReportBookingStateLabel(booking.BookingState),
                PaymentStatus = booking.PaymentStatus?.Trim() ?? string.Empty,
                PaymentStatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                CommissionStatus = financial?.CommissionStatus?.Trim() ?? string.Empty,
                CommissionStatusLabel = GetReportCommissionStatusLabel(financial?.CommissionStatus),
                RecordStatus = booking.PaymentStatus?.Trim() ?? string.Empty,
                RecordStatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                ReferenceNumber = financial?.TransferReferenceNumber?.Trim(),
                Description = description
            };
        }

        private async Task<List<FinancialDocumentItemDto>>
            BuildFinancialDocumentsAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var requestedType = filter.DocumentType;
            var documents = new List<FinancialDocumentItemDto>();

            var needsBookingDocuments =
                string.IsNullOrWhiteSpace(requestedType) ||
                IsState(requestedType, OwnerFinancialDocumentTypes.BookingInvoice) ||
                IsState(requestedType, OwnerFinancialDocumentTypes.DepositReceipt) ||
                IsState(requestedType, OwnerFinancialDocumentTypes.CollectionReceipt) ||
                IsState(requestedType, OwnerFinancialDocumentTypes.BookingSettlement);

            if (needsBookingDocuments)
            {
                var sourceFilter = CloneFinancialReportFilter(filter);
                sourceFilter.Search = null;
                sourceFilter.ReferenceNumber = null;

                var bookingQuery = ApplyBookingIdentityFilters(
                    _context.Bookings.AsNoTracking(),
                    ownerId,
                    sourceFilter,
                    applyBookingState: false,
                    applyPaymentStatus: false,
                    applyCommissionStatus: false);

                var bookings = await bookingQuery
                    .Include(booking => booking.Customer)
                    .Include(booking => booking.Place)
                    .Include(booking => booking.FinancialRecord)
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken);

                var refundAggregates = await LoadBookingRefundAggregatesAsync(
                    bookings.Select(booking => booking.BookingId).ToArray(),
                    cancellationToken);

                foreach (var booking in bookings)
                {
                    refundAggregates.TryGetValue(booking.BookingId, out var aggregate);
                    var financial = booking.FinancialRecord;

                    if (
                        string.IsNullOrWhiteSpace(requestedType) ||
                        IsState(requestedType, OwnerFinancialDocumentTypes.BookingInvoice))
                    {
                        documents.Add(CreateBookingDocument(
                            booking,
                            aggregate,
                            OwnerFinancialDocumentTypes.BookingInvoice,
                            booking.CreatedAt ?? booking.StartDate,
                            booking.TotalPrice,
                            "فاتورة قيمة الحجز"));
                    }

                    if (
                        (string.IsNullOrWhiteSpace(requestedType) ||
                         IsState(requestedType, OwnerFinancialDocumentTypes.DepositReceipt)) &&
                        IsBookingPaymentConfirmed(booking) &&
                        GetActualOnlineAmount(booking) > 0m)
                    {
                        documents.Add(CreateBookingDocument(
                            booking,
                            aggregate,
                            OwnerFinancialDocumentTypes.DepositReceipt,
                            GetOnlinePaymentDate(booking, financial) ?? financial.UpdatedAt,
                            GetActualOnlineAmount(booking),
                            "إيصال المبلغ المدفوع إلكترونيًا"));
                    }

                    if (
                        (string.IsNullOrWhiteSpace(requestedType) ||
                         IsState(requestedType, OwnerFinancialDocumentTypes.CollectionReceipt)) &&
                        GetActualPropertyAmount(booking) > 0m)
                    {
                        documents.Add(CreateBookingDocument(
                            booking,
                            aggregate,
                            OwnerFinancialDocumentTypes.CollectionReceipt,
                            GetPropertyCollectionDate(financial) ?? financial!.UpdatedAt,
                            GetActualPropertyAmount(booking),
                            "إيصال المبلغ المحصّل في المنشأة"));
                    }

                    if (
                        (string.IsNullOrWhiteSpace(requestedType) ||
                         IsState(requestedType, OwnerFinancialDocumentTypes.BookingSettlement)) &&
                        financial != null &&
                        (financial.CommissionChargedAt.HasValue ||
                         financial.CommissionReleasedAt.HasValue ||
                         booking.CompletedAt.HasValue ||
                         IsState(booking.BookingState, "Completed")))
                    {
                        var netOwner = Math.Max(
                            0m,
                            GetActualOnlineAmount(booking) +
                            GetActualPropertyAmount(booking) -
                            Math.Max(0m, aggregate?.CompletedRefundAmount ?? 0m) -
                            (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Charged)
                                ? Math.Max(0m, financial.PlatformCommission)
                                : 0m));

                        documents.Add(CreateBookingDocument(
                            booking,
                            aggregate,
                            OwnerFinancialDocumentTypes.BookingSettlement,
                            GetFinancialSettlementDate(booking, financial) ?? financial.UpdatedAt,
                            netOwner,
                            "كشف التسوية المالية للحجز"));
                    }
                }
            }

            if (
                string.IsNullOrWhiteSpace(requestedType) ||
                IsState(requestedType, OwnerFinancialDocumentTypes.RefundReceipt))
            {
                var refundQuery = _context.RefundRequests
                    .AsNoTracking()
                    .Where(refund =>
                        refund.Place.OwnerId == ownerId &&
                        refund.Status == FinancialRefundStatuses.Completed);

                if (filter.PlaceId.HasValue)
                {
                    var placeId = filter.PlaceId.Value;
                    refundQuery = refundQuery.Where(refund => refund.PlaceId == placeId);
                }

                if (filter.BookingId.HasValue)
                {
                    var bookingId = filter.BookingId.Value;
                    refundQuery = refundQuery.Where(refund => refund.BookingId == bookingId);
                }

                if (filter.CustomerId.HasValue)
                {
                    var customerId = filter.CustomerId.Value;
                    refundQuery = refundQuery.Where(refund => refund.CustomerId == customerId);
                }

                var refunds = await refundQuery
                    .Include(refund => refund.Booking)
                        .ThenInclude(booking => booking.FinancialRecord)
                    .Include(refund => refund.Customer)
                    .Include(refund => refund.Place)
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken);

                documents.AddRange(refunds.Select(refund =>
                    new FinancialDocumentItemDto
                    {
                        DocumentKey = string.Concat(
                            OwnerFinancialDocumentTypes.RefundReceipt,
                            ":",
                            refund.RefundRequestId),
                        SourceId = refund.RefundRequestId,
                        BookingId = refund.BookingId,
                        CustomerId = refund.CustomerId,
                        PlaceId = refund.PlaceId,
                        DocumentNumber = BuildFinancialDocumentNumber(
                            OwnerFinancialDocumentTypes.RefundReceipt,
                            refund.RefundRequestId),
                        DocumentType = OwnerFinancialDocumentTypes.RefundReceipt,
                        DocumentTypeLabel = GetFinancialDocumentTypeLabel(
                            OwnerFinancialDocumentTypes.RefundReceipt),
                        DocumentDate = refund.ProcessedAt ?? refund.RequestedAt ?? DateTime.Now,
                        CustomerName = FirstNonEmpty("عميل", refund.Customer?.FullName, refund.Customer?.Username),
                        PlaceName = FirstNonEmpty("منشأة غير معروفة", refund.Place?.PlaceNameAndDesc),
                        PlaceImageUrl = FirstNonEmpty(string.Empty, refund.Place?.CoverImage, refund.Place?.LogoImage),
                        PrimaryAmount = Math.Max(0m, refund.RefundAmount),
                        OriginalTotalPrice = refund.Booking == null
                            ? 0m
                            : refund.Booking.OriginalTotalPrice > 0m
                                ? refund.Booking.OriginalTotalPrice
                                : refund.Booking.TotalPrice + refund.Booking.DiscountAmount,
                        DiscountAmount = Math.Max(0m, refund.Booking?.DiscountAmount ?? 0m),
                        PromotionId = refund.Booking?.PromotionId,
                        PromotionTitleSnapshot = refund.Booking?.PromotionTitleSnapshot?.Trim(),
                        PromotionCodeSnapshot = refund.Booking?.PromotionCodeSnapshot?.Trim(),
                        DiscountTypeSnapshot = refund.Booking?.DiscountTypeSnapshot?.Trim(),
                        DiscountValueSnapshot = refund.Booking?.DiscountValueSnapshot,
                        TotalPrice = Math.Max(0m, refund.Booking?.TotalPrice ?? 0m),
                        DepositAmount = Math.Max(0m, refund.DepositPaid),
                        AmountPaidOnline = Math.Max(0m, refund.Booking?.FinancialRecord?.AmountPaidOnline ?? 0m),
                        AmountCollectedAtProperty = Math.Max(0m, refund.Booking?.FinancialRecord?.AmountCollectedAtProperty ?? 0m),
                        RemainingAmount = Math.Max(0m, refund.Booking?.FinancialRecord?.AmountToPayAtProperty ?? 0m),
                        PlatformCommission = Math.Max(0m, refund.PlatformCommission),
                        RefundedAmount = Math.Max(0m, refund.RefundAmount),
                        CancellationFee = Math.Max(0m, refund.CancellationFee),
                        OwnerShare = Math.Max(0m, refund.OwnerShare),
                        NetOwnerAmount = Math.Max(0m, refund.OwnerShare),
                        BookingState = refund.Booking?.BookingState?.Trim() ?? string.Empty,
                        BookingStateLabel = GetReportBookingStateLabel(refund.Booking?.BookingState),
                        PaymentStatus = refund.Booking?.PaymentStatus?.Trim() ?? string.Empty,
                        PaymentStatusLabel = GetReportPaymentStatusLabel(refund.Booking?.PaymentStatus),
                        CommissionStatus = refund.Booking?.FinancialRecord?.CommissionStatus?.Trim() ?? string.Empty,
                        CommissionStatusLabel = GetReportCommissionStatusLabel(refund.Booking?.FinancialRecord?.CommissionStatus),
                        RecordStatus = refund.Status?.Trim() ?? string.Empty,
                        RecordStatusLabel = GetRefundStatusLabel(refund.Status),
                        ReferenceNumber = refund.ReferenceNumber?.Trim(),
                        Description = "إيصال استرداد مكتمل"
                    }));
            }

            if (
                (string.IsNullOrWhiteSpace(requestedType) ||
                 IsState(requestedType, OwnerFinancialDocumentTypes.TopUpReceipt)) &&
                !filter.BookingId.HasValue &&
                !filter.CustomerId.HasValue)
            {
                var topUpQuery = _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        transaction.Place.OwnerId == ownerId &&
                        transaction.TransactionType == FinancialTransactionTypes.TopUp &&
                        transaction.Status == FinancialTransactionStatuses.Completed);

                if (filter.PlaceId.HasValue)
                {
                    var placeId = filter.PlaceId.Value;
                    topUpQuery = topUpQuery.Where(transaction =>
                        transaction.PlaceId == placeId);
                }

                var topUps = await topUpQuery
                    .Include(transaction => transaction.Place)
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken);

                documents.AddRange(topUps.Select(transaction =>
                    new FinancialDocumentItemDto
                    {
                        DocumentKey = string.Concat(
                            OwnerFinancialDocumentTypes.TopUpReceipt,
                            ":",
                            transaction.CommissionTransactionId),
                        SourceId = transaction.CommissionTransactionId,
                        BookingId = transaction.BookingId,
                        PlaceId = transaction.PlaceId,
                        DocumentNumber = BuildFinancialDocumentNumber(
                            OwnerFinancialDocumentTypes.TopUpReceipt,
                            transaction.CommissionTransactionId),
                        DocumentType = OwnerFinancialDocumentTypes.TopUpReceipt,
                        DocumentTypeLabel = GetFinancialDocumentTypeLabel(
                            OwnerFinancialDocumentTypes.TopUpReceipt),
                        DocumentDate = transaction.ReviewedAt ?? transaction.CreatedAt ?? transaction.TransferDate ?? DateTime.Now,
                        CustomerName = "حساب المالك",
                        PlaceName = FirstNonEmpty("منشأة غير معروفة", transaction.Place?.PlaceNameAndDesc),
                        PlaceImageUrl = FirstNonEmpty(string.Empty, transaction.Place?.CoverImage, transaction.Place?.LogoImage),
                        PrimaryAmount = Math.Max(0m, transaction.Amount),
                        NetOwnerAmount = Math.Max(0m, transaction.Amount),
                        RecordStatus = transaction.Status?.Trim() ?? string.Empty,
                        RecordStatusLabel = GetTransactionStatusLabel(transaction.Status),
                        ReferenceNumber = transaction.ReferenceNumber?.Trim(),
                        Description = "إيصال شحن رصيد ضمان العمولة"
                    }));
            }

            var filtered = documents
                .Where(document => IsDateWithinFilter(document.DocumentDate, filter))
                .Where(document =>
                    string.IsNullOrWhiteSpace(filter.ReferenceNumber) ||
                    (!string.IsNullOrWhiteSpace(document.ReferenceNumber) &&
                     document.ReferenceNumber.Contains(filter.ReferenceNumber, StringComparison.OrdinalIgnoreCase)))
                .Where(document => MatchesDocumentSearch(document, filter.Search))
                .GroupBy(document => document.DocumentKey)
                .Select(group => group
                    .OrderByDescending(document => document.DocumentDate)
                    .First())
                .OrderByDescending(document => document.DocumentDate)
                .ThenByDescending(document => document.SourceId)
                .ToList();

            return filtered;
        }

        private static bool MatchesDocumentSearch(
            FinancialDocumentItemDto document,
            string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return document.DocumentNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                document.DocumentTypeLabel.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                document.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                document.PlaceName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(document.ReferenceNumber) &&
                 document.ReferenceNumber.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(document.Description) &&
                 document.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (document.BookingId.HasValue &&
                 document.BookingId.Value.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        private static FinancialReportRowDto MapDocumentToReportRow(
            FinancialDocumentItemDto document) =>
                new FinancialReportRowDto
                {
                    RecordId = document.SourceId,
                    RecordType = OwnerFinancialReportTypes.Documents,
                    RecordTypeLabel = document.DocumentTypeLabel,
                    RecordNumber = document.DocumentNumber,
                    RecordDate = document.DocumentDate,
                    BookingId = document.BookingId,
                    CustomerId = document.CustomerId,
                    CustomerName = document.CustomerName,
                    PlaceId = document.PlaceId,
                    PlaceName = document.PlaceName,
                    PlaceImageUrl = document.PlaceImageUrl,
                    BookingState = document.BookingState,
                    BookingStateLabel = document.BookingStateLabel,
                    PaymentStatus = document.PaymentStatus,
                    PaymentStatusLabel = document.PaymentStatusLabel,
                    CommissionStatus = document.CommissionStatus,
                    CommissionStatusLabel = document.CommissionStatusLabel,
                    Status = document.RecordStatus,
                    StatusLabel = document.RecordStatusLabel,
                    ReferenceNumber = document.ReferenceNumber,
                    Description = document.Description,
                    PrimaryAmount = document.PrimaryAmount,
                    OriginalTotalPrice = document.OriginalTotalPrice,
                    DiscountAmount = document.DiscountAmount,
                    PromotionId = document.PromotionId,
                    PromotionTitleSnapshot = document.PromotionTitleSnapshot,
                    PromotionCodeSnapshot = document.PromotionCodeSnapshot,
                    DiscountTypeSnapshot = document.DiscountTypeSnapshot,
                    DiscountValueSnapshot = document.DiscountValueSnapshot,
                    TotalPrice = document.TotalPrice,
                    DepositAmount = document.DepositAmount,
                    AmountPaidOnline = document.AmountPaidOnline,
                    AmountCollectedAtProperty = document.AmountCollectedAtProperty,
                    ActualPaidOnline = document.AmountPaidOnline,
                    ActualCollectedAtProperty = document.AmountCollectedAtProperty,
                    ActualCollectedAmount = document.AmountPaidOnline + document.AmountCollectedAtProperty,
                    RemainingAmount = document.RemainingAmount,
                    PlatformCommission = document.PlatformCommission,
                    RefundAmount = document.RefundedAmount,
                    CancellationFee = document.CancellationFee,
                    OwnerShare = document.OwnerShare,
                    TopUpAmount = IsState(document.DocumentType, OwnerFinancialDocumentTypes.TopUpReceipt)
                        ? document.PrimaryAmount
                        : 0m,
                    NetOwnerAmount = document.NetOwnerAmount
                };

        private static FinancialReportSummaryDto BuildFinancialReportSummary(
            string reportType,
            IReadOnlyCollection<FinancialReportRowDto> rows,
            IReadOnlyCollection<FinancialDocumentItemDto>? documents,
            ComprehensiveFinancialAnalyticsDto analytics)
        {
            var summary = new FinancialReportSummaryDto
            {
                RecordsCount = documents?.Count ?? rows.Count,
                BookingCount = rows
                    .Where(row => row.BookingId.HasValue)
                    .Select(row => row.BookingId!.Value)
                    .Distinct()
                    .Count(),
                CustomerCount = rows
                    .Where(row => row.CustomerId.HasValue)
                    .Select(row => row.CustomerId!.Value)
                    .Distinct()
                    .Count(),
                PlaceCount = rows
                    .Select(row => row.PlaceId)
                    .Where(id => id > 0)
                    .Distinct()
                    .Count(),
                ConfirmedBookingsCount = rows.Count(row => IsState(row.BookingState, "Confirmed")),
                CompletedBookingsCount = rows.Count(row => IsState(row.BookingState, "Completed")),
                CancelledBookingsCount = rows.Count(row => IsCancelledBookingState(row.BookingState)),
                FullyPaidBookingsCount = rows.Count(
                    IsFullyPaidReportRow),
                PartiallyPaidBookingsCount = rows.Count(
                    IsPartiallyPaidReportRow),
                UnpaidBookingsCount = rows.Count(
                    IsUnpaidReportRow),
                CompletedRefundsCount = rows.Count(row =>
                    IsState(row.RecordType, OwnerFinancialReportTypes.Refunds) &&
                    IsState(row.Status, FinancialRefundStatuses.Completed)),
                OpenRefundsCount = rows.Count(row =>
                    IsState(row.RecordType, OwnerFinancialReportTypes.Refunds) &&
                    (IsState(row.Status, FinancialRefundStatuses.Pending) ||
                     IsState(row.Status, FinancialRefundStatuses.Processing))),
                RejectedRefundsCount = rows.Count(row =>
                    IsState(row.RecordType, OwnerFinancialReportTypes.Refunds) &&
                    IsState(row.Status, FinancialRefundStatuses.Rejected)),
                FailedRefundsCount = rows.Count(row =>
                    IsState(row.RecordType, OwnerFinancialReportTypes.Refunds) &&
                    IsState(row.Status, FinancialRefundStatuses.Failed)),
                CancelledRefundsCount = rows.Count(row =>
                    IsState(row.RecordType, OwnerFinancialReportTypes.Refunds) &&
                    IsState(row.Status, FinancialRefundStatuses.Cancelled)),
                CompletedTransactionsCount = rows.Count(row =>
                    IsState(row.RecordType, OwnerFinancialReportTypes.Transactions) &&
                    IsState(row.Status, FinancialTransactionStatuses.Completed)),
                PendingTransactionsCount = rows.Count(row =>
                    IsState(row.RecordType, OwnerFinancialReportTypes.Transactions) &&
                    IsState(row.Status, FinancialTransactionStatuses.Pending)),
                TotalOriginalBookingValue = rows.Sum(row =>
                    row.OriginalTotalPrice > 0m
                        ? row.OriginalTotalPrice
                        : row.TotalPrice + row.DiscountAmount),
                TotalDiscountAmount = rows.Sum(row => Math.Max(0m, row.DiscountAmount)),
                DiscountedBookingsCount = rows
                    .Where(row => row.BookingId.HasValue && row.DiscountAmount > 0m)
                    .Select(row => row.BookingId!.Value)
                    .Distinct()
                    .Count(),
                TotalBookingValue = rows.Sum(row => row.TotalPrice),
                TotalDepositRequired = rows.Sum(row => row.DepositAmount),
                TotalPaidOnline = rows.Sum(row => row.ActualPaidOnline),
                TotalCollectedAtProperty = rows.Sum(row => row.ActualCollectedAtProperty),
                TotalActualCollected = rows.Sum(row => row.ActualCollectedAmount),
                TotalRemainingAtProperty = rows.Sum(row => row.RemainingAmount),
                ActualRevenue = rows.Sum(row => row.ActualRevenue),
                TotalCommission = rows.Sum(row => row.PlatformCommission),
                TotalCommissionHeld = rows.Sum(row => row.CommissionHeld),
                TotalCommissionCharged = rows.Sum(row => row.CommissionCharged),
                TotalCommissionReleased = rows.Sum(row => row.CommissionReleased),
                TotalCommissionDebt = rows.Sum(row => row.CommissionDebt),
                TotalCommissionOutstanding = rows.Sum(row => row.CommissionOutstanding),
                RequestedRefundAmount = rows.Sum(row => row.RequestedRefundAmount),
                PendingRefundAmount = rows
                    .Where(row =>
                        IsState(row.Status, FinancialRefundStatuses.Pending) ||
                        IsState(row.Status, FinancialRefundStatuses.Processing))
                    .Sum(row => row.RequestedRefundAmount),
                TotalRefunded = rows.Sum(row => row.RefundAmount),
                TotalCancellationFees = rows.Sum(row => row.CancellationFee),
                TotalOwnerShare = rows.Sum(row => row.OwnerShare),
                TotalTopUps = rows
                    .Where(row =>
                        row.TopUpAmount > 0m &&
                        IsState(row.Status, FinancialTransactionStatuses.Completed))
                    .Sum(row => row.TopUpAmount),
                TotalPendingTopUps = rows
                    .Where(row =>
                        row.TopUpAmount > 0m &&
                        IsState(row.Status, FinancialTransactionStatuses.Pending))
                    .Sum(row => row.TopUpAmount),
                CurrentDebt = analytics.CurrentDebt,
                NetRevenue = rows.Sum(row => row.NetOwnerAmount),
                NetCashFlow = analytics.OperationalNetCashFlow,
                GuaranteeNetFlow = analytics.GuaranteeNetFlow
            };

            if (documents != null)
            {
                summary.DocumentsCount = documents.Count;
                summary.BookingInvoicesCount = documents.Count(item =>
                    IsState(item.DocumentType, OwnerFinancialDocumentTypes.BookingInvoice));
                summary.DepositReceiptsCount = documents.Count(item =>
                    IsState(item.DocumentType, OwnerFinancialDocumentTypes.DepositReceipt));
                summary.CollectionReceiptsCount = documents.Count(item =>
                    IsState(item.DocumentType, OwnerFinancialDocumentTypes.CollectionReceipt));
                summary.RefundReceiptsCount = documents.Count(item =>
                    IsState(item.DocumentType, OwnerFinancialDocumentTypes.RefundReceipt));
                summary.TopUpReceiptsCount = documents.Count(item =>
                    IsState(item.DocumentType, OwnerFinancialDocumentTypes.TopUpReceipt));
                summary.SettlementStatementsCount = documents.Count(item =>
                    IsState(item.DocumentType, OwnerFinancialDocumentTypes.BookingSettlement));
                summary.TotalDocumentValue = documents.Sum(item => item.PrimaryAmount);
                summary.TotalTopUps = documents
                    .Where(item => IsState(item.DocumentType, OwnerFinancialDocumentTypes.TopUpReceipt))
                    .Sum(item => item.PrimaryAmount);
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Bookings))
            {
                summary.NetRevenue = summary.ActualRevenue;
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Payments))
            {
                summary.NetRevenue = summary.TotalActualCollected;
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Comprehensive))
            {
                summary.TotalTopUps = analytics.CompletedTopUpsAmount;
                summary.TotalPendingTopUps = analytics.PendingTopUpsAmount;
                summary.TotalCommissionHeld = analytics.CommissionHeldAmount;
                summary.TotalCommissionCharged = analytics.CommissionChargedAmount;
                summary.TotalCommissionReleased = analytics.CommissionReleasedAmount;
                summary.CurrentDebt = analytics.CurrentDebt;
                summary.PendingRefundAmount = analytics.PendingRefundAmount;
                summary.TotalRefunded = rows.Sum(row => row.RefundAmount);
                summary.NetCashFlow = analytics.OperationalNetCashFlow;
                summary.GuaranteeNetFlow = analytics.GuaranteeNetFlow;
            }

            return summary;
        }

        private static FinancialReconciliationDto
            BuildFinancialReportReconciliation(
                string reportType,
                IReadOnlyCollection<FinancialReportRowDto> rows)
        {
            var result = new FinancialReconciliationDto();

            var checksCollection =
                IsState(reportType, OwnerFinancialReportTypes.Bookings) ||
                IsState(reportType, OwnerFinancialReportTypes.Payments) ||
                IsState(reportType, OwnerFinancialReportTypes.Comprehensive);

            var checksRefundFunding =
                IsState(reportType, OwnerFinancialReportTypes.Refunds) ||
                IsState(reportType, OwnerFinancialReportTypes.Comprehensive);

            var checksPrices =
                IsState(reportType, OwnerFinancialReportTypes.Bookings) ||
                IsState(reportType, OwnerFinancialReportTypes.Payments) ||
                IsState(reportType, OwnerFinancialReportTypes.Refunds) ||
                IsState(reportType, OwnerFinancialReportTypes.Commissions) ||
                IsState(reportType, OwnerFinancialReportTypes.Comprehensive);

            var checksBalances =
                IsState(reportType, OwnerFinancialReportTypes.Transactions);

            result.IsApplicable =
                checksCollection ||
                checksRefundFunding ||
                checksPrices ||
                checksBalances;

            if (!result.IsApplicable)
            {
                return result;
            }

            result.CheckedRecords = rows.Count;

            foreach (var row in rows)
            {
                if (checksCollection)
                {
                    var expectedCollected = Math.Round(
                        Math.Max(0m, row.ActualPaidOnline) +
                        Math.Max(0m, row.ActualCollectedAtProperty),
                        2);

                    var actualCollected = Math.Round(
                        Math.Max(0m, row.ActualCollectedAmount),
                        2);

                    var difference = Math.Abs(
                        expectedCollected - actualCollected);

                    result.MaximumCollectionDifference = Math.Max(
                        result.MaximumCollectionDifference,
                        difference);

                    if (difference > 0.01m)
                    {
                        result.CollectionMismatchCount++;
                    }
                }

                if (checksRefundFunding)
                {
                    var requestedRefund = Math.Max(
                        0m,
                        row.RequestedRefundAmount);

                    var verifiedPaidOnline = Math.Max(
                        0m,
                        row.ActualPaidOnline);

                    if (
                        requestedRefund > 0m &&
                        requestedRefund > verifiedPaidOnline + 0.01m)
                    {
                        result.RefundFundingMismatchCount++;
                    }
                }

                if (checksPrices)
                {
                    var original = Math.Max(
                        0m,
                        row.OriginalTotalPrice);

                    var final = Math.Max(
                        0m,
                        row.TotalPrice);

                    if (
                        original > 0m &&
                        final > original + 0.01m)
                    {
                        result.PriceMismatchCount++;
                    }
                }

                if (checksBalances)
                {
                    if (
                        row.AvailableBalanceBefore < -0.01m ||
                        row.AvailableBalanceAfter < -0.01m ||
                        row.HeldBalanceBefore < -0.01m ||
                        row.HeldBalanceAfter < -0.01m ||
                        row.DebtBefore < -0.01m ||
                        row.DebtAfter < -0.01m)
                    {
                        result.NegativeBalanceCount++;
                    }
                }
            }

            result.MaximumCollectionDifference = Math.Round(
                result.MaximumCollectionDifference,
                2);

            return result;
        }

        private async Task<FinancialReportEngineResult>
            BuildFinancialReportEngineAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken)
        {
            var result = new FinancialReportEngineResult
            {
                Filter = filter
            };

            if (IsState(filter.ReportType, OwnerFinancialReportTypes.Documents))
            {
                result.Documents = await BuildFinancialDocumentsAsync(
                    ownerId,
                    filter,
                    cancellationToken);
                result.Rows = result.Documents
                    .Select(MapDocumentToReportRow)
                    .ToList();
            }
            else if (IsState(filter.ReportType, OwnerFinancialReportTypes.Bookings))
            {
                result.Rows = await BuildBookingsReportAsync(
                    ownerId,
                    filter,
                    cancellationToken);
            }
            else if (IsState(filter.ReportType, OwnerFinancialReportTypes.Payments))
            {
                result.Rows = await BuildPaymentsReportAsync(
                    ownerId,
                    filter,
                    cancellationToken);
            }
            else if (IsState(filter.ReportType, OwnerFinancialReportTypes.Commissions))
            {
                result.Rows = await BuildCommissionsReportAsync(
                    ownerId,
                    filter,
                    cancellationToken);
            }
            else if (IsState(filter.ReportType, OwnerFinancialReportTypes.Refunds))
            {
                result.Rows = await BuildRefundsReportAsync(
                    ownerId,
                    filter,
                    cancellationToken);
            }
            else if (IsState(filter.ReportType, OwnerFinancialReportTypes.Transactions))
            {
                result.Rows = await BuildTransactionsReportAsync(
                    ownerId,
                    filter,
                    cancellationToken);
            }
            else
            {
                result.Rows = await BuildComprehensiveReportAsync(
                    ownerId,
                    filter,
                    cancellationToken);

                result.Analytics = await BuildComprehensiveAnalyticsAsync(
                    ownerId,
                    filter,
                    result.Rows,
                    cancellationToken);
            }

            result.Summary = BuildFinancialReportSummary(
                filter.ReportType,
                result.Rows,
                IsState(filter.ReportType, OwnerFinancialReportTypes.Documents)
                    ? result.Documents
                    : null,
                result.Analytics);

            result.Reconciliation = BuildFinancialReportReconciliation(
                filter.ReportType,
                result.Rows);

            return result;
        }

        public async Task<InvoicesReportsTabDto> GetInvoicesReportsAsync(
            int ownerId,
            FinancialReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter = await NormalizeFinancialReportFilterAsync(
                ownerId,
                filter,
                cancellationToken);

            var settings = await GetSettingsAsync(cancellationToken);
            var selectedPlaceName = await GetSelectedFinancialPlaceNameAsync(
                ownerId,
                filter.PlaceId,
                cancellationToken);

            var engine = await BuildFinancialReportEngineAsync(
                ownerId,
                filter,
                cancellationToken);

            var totalCount = IsState(filter.ReportType, OwnerFinancialReportTypes.Documents)
                ? engine.Documents.Count
                : engine.Rows.Count;

            var totalPages = totalCount <= 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize);

            filter.PageNumber = totalPages == 0
                ? 1
                : Math.Min(filter.PageNumber, totalPages);

            var skip = (filter.PageNumber - 1) * filter.PageSize;

            var pagedDocuments = IsState(filter.ReportType, OwnerFinancialReportTypes.Documents)
                ? engine.Documents.Skip(skip).Take(filter.PageSize).ToList()
                : new List<FinancialDocumentItemDto>();

            var pagedRows = IsState(filter.ReportType, OwnerFinancialReportTypes.Documents)
                ? new List<FinancialReportRowDto>()
                : engine.Rows.Skip(skip).Take(filter.PageSize).ToList();

            var capabilities = BuildReportCapabilities(filter.ReportType);
            var dateScopes = GetAllowedDateScopes(filter.ReportType);

            return new InvoicesReportsTabDto
            {
                Currency = GetCurrency(settings),
                SelectedPlaceId = filter.PlaceId,
                SelectedPlaceName = selectedPlaceName,
                ReportTitle = GetFinancialReportTypeLabel(filter.ReportType),
                ReportDescription = GetFinancialReportDescription(filter.ReportType),
                Filter = filter,
                Summary = engine.Summary,
                Analytics = engine.Analytics,
                Reconciliation = engine.Reconciliation,
                Capabilities = capabilities,
                ReportTypeOptions = BuildReportTypeOptions(filter.ReportType),
                DocumentTypeOptions = BuildDocumentTypeOptions(filter.DocumentType),
                DateScopeOptions = BuildReportOptions(
                    dateScopes,
                    filter.DateScope,
                    GetFinancialDateScopeLabel),
                PaymentChannelOptions = BuildReportOptions(
                    OwnerFinancialPaymentChannels.Values,
                    filter.PaymentChannel,
                    GetPaymentChannelLabel),
                BookingStateOptions = BuildReportOptions(
                    ReportBookingStates,
                    filter.BookingState,
                    GetReportBookingStateLabel),
                PaymentStatusOptions = BuildReportOptions(
                    ReportPaymentStatuses,
                    filter.PaymentStatus,
                    GetReportPaymentStatusLabel),
                CommissionStatusOptions = BuildReportOptions(
                    ReportCommissionStatuses,
                    filter.CommissionStatus,
                    GetReportCommissionStatusLabel),
                RefundStatusOptions = BuildRefundStatusOptions(filter.RefundStatus),
                TransactionTypeOptions = BuildTransactionTypeOptions(filter.TransactionType),
                TransactionStatusOptions = BuildTransactionStatusOptions(filter.TransactionStatus),
                Documents = pagedDocuments,
                ReportRows = pagedRows,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }


        private static void AddDocumentAmountLine(
            ICollection<FinancialDocumentAmountLineDto> lines,
            string label,
            decimal amount,
            bool isHighlight = false,
            bool isNegative = false)
        {
            lines.Add(new FinancialDocumentAmountLineDto
            {
                Label = label,
                Amount = Math.Max(0m, amount),
                IsHighlight = isHighlight,
                IsNegative = isNegative
            });
        }

        private static void ApplyContactInfo(
            FinancialDocumentDetailsDto details,
            User? customer)
        {
            var contact = customer?.ContactInfo?.Trim();
            var isEmail = !string.IsNullOrWhiteSpace(contact) && contact.Contains('@');

            details.CustomerPhone = FirstNonEmpty(
                string.Empty,
                customer?.PhoneNumber,
                isEmail ? null : contact);
            details.CustomerEmail = isEmail ? contact : null;
        }

        private async Task<FinancialDocumentDetailsDto?> BuildBookingDocumentDetailsAsync(
            int ownerId,
            string documentType,
            int bookingId,
            string currency,
            CancellationToken cancellationToken)
        {
            var booking = await _context.Bookings.AsNoTracking()
                .Include(item => item.Customer)
                .Include(item => item.Place)
                .Include(item => item.FinancialRecord)
                .AsSplitQuery()
                .FirstOrDefaultAsync(item =>
                    item.BookingId == bookingId && item.Place.OwnerId == ownerId,
                    cancellationToken);

            if (booking == null) return null;

            var financial = booking.FinancialRecord;
            var paidOnline = Math.Max(0m, financial?.AmountPaidOnline ?? 0m);
            var collected = Math.Max(0m, financial?.AmountCollectedAtProperty ?? 0m);

            if (IsState(documentType, OwnerFinancialDocumentTypes.DepositReceipt) &&
                (!IsBookingPaymentConfirmed(booking) || paidOnline <= 0m)) return null;
            if (IsState(documentType, OwnerFinancialDocumentTypes.CollectionReceipt) && collected <= 0m) return null;
            if (IsState(documentType, OwnerFinancialDocumentTypes.BookingSettlement) && financial == null) return null;

            var refundMap = await LoadBookingRefundAggregatesAsync(
                new[] { bookingId },
                cancellationToken);
            refundMap.TryGetValue(bookingId, out var refund);

            var commissionMap =
                await LoadBookingCommissionMovementAggregatesAsync(
                    new[] { bookingId },
                    cancellationToken);
            commissionMap.TryGetValue(
                bookingId,
                out var commissionMovements);

            var remaining = Math.Max(0m, financial?.AmountToPayAtProperty ??
                Math.Max(0m, booking.TotalPrice - paidOnline - collected));
            var commission = GetChargedCommissionAmount(
                financial,
                commissionMovements);
            var refunded = Math.Max(0m, refund?.RefundAmount ?? 0m);
            var netOwner = Math.Max(0m, paidOnline + collected - commission - refunded);

            var documentDate = IsState(documentType, OwnerFinancialDocumentTypes.DepositReceipt)
                ? financial?.UpdatedAt ?? booking.CreatedAt ?? booking.StartDate
                : IsState(documentType, OwnerFinancialDocumentTypes.CollectionReceipt)
                    ? financial?.CollectedAt ?? financial?.UpdatedAt ?? booking.StartDate
                    : IsState(documentType, OwnerFinancialDocumentTypes.BookingSettlement)
                        ? GetFinancialSettlementDate(booking, financial) ?? booking.StartDate
                        : booking.CreatedAt ?? booking.StartDate;

            var grandTotal = IsState(documentType, OwnerFinancialDocumentTypes.BookingInvoice)
                ? Math.Max(0m, booking.TotalPrice)
                : IsState(documentType, OwnerFinancialDocumentTypes.DepositReceipt)
                    ? paidOnline
                    : IsState(documentType, OwnerFinancialDocumentTypes.CollectionReceipt)
                        ? collected
                        : netOwner;

            var details = new FinancialDocumentDetailsDto
            {
                Currency = currency,
                DocumentType = documentType,
                DocumentTypeLabel = GetFinancialDocumentTypeLabel(documentType),
                DocumentNumber = BuildFinancialDocumentNumber(documentType, bookingId),
                DocumentDate = documentDate,
                Title = GetFinancialDocumentTypeLabel(documentType),
                Subtitle = $"الحجز رقم {bookingId.ToString(CultureInfo.InvariantCulture)}",
                SourceId = bookingId,
                BookingId = bookingId,
                PlaceId = booking.PlaceId,
                PlaceName = FirstNonEmpty("منشأة غير معروفة", booking.Place?.PlaceNameAndDesc),
                PlaceLogoUrl = FirstNonEmpty(string.Empty, booking.Place?.LogoImage, booking.Place?.CoverImage),
                PlaceAddress = booking.Place?.AddressDetail?.Trim(),
                PlacePhone = booking.Place?.PhoneNumber?.Trim(),
                PlaceEmail = booking.Place?.EmailAddress?.Trim(),
                CustomerId = booking.CustomerId,
                CustomerName = FirstNonEmpty("عميل", booking.Customer?.FullName, booking.Customer?.Username),
                BookingStartDate = booking.StartDate,
                BookingEndDate = booking.EndDate,
                PromotionId = booking.PromotionId,
                OriginalTotalPrice = booking.OriginalTotalPrice > 0m
                    ? booking.OriginalTotalPrice
                    : booking.TotalPrice + booking.DiscountAmount,
                DiscountAmount = Math.Max(0m, booking.DiscountAmount),
                FinalTotalPrice = Math.Max(0m, booking.TotalPrice),
                PromotionTitleSnapshot = booking.PromotionTitleSnapshot?.Trim(),
                PromotionCodeSnapshot = booking.PromotionCodeSnapshot?.Trim(),
                DiscountTypeSnapshot = booking.DiscountTypeSnapshot?.Trim(),
                DiscountValueSnapshot = booking.DiscountValueSnapshot,
                BookingStateLabel = GetReportBookingStateLabel(booking.BookingState),
                PaymentStatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                CommissionStatusLabel = GetReportCommissionStatusLabel(financial?.CommissionStatus),
                RecordStatusLabel = GetReportPaymentStatusLabel(booking.PaymentStatus),
                ReferenceNumber = financial?.TransferReferenceNumber?.Trim(),
                VerificationText = $"هذا المستند مولد إلكترونيًا من منصة دليلك اليمني، ورقمه المرجعي {BuildFinancialDocumentNumber(documentType, bookingId)}.",
                GrandTotalLabel = IsState(documentType, OwnerFinancialDocumentTypes.BookingInvoice)
                    ? "إجمالي قيمة الحجز"
                    : IsState(documentType, OwnerFinancialDocumentTypes.DepositReceipt)
                        ? "المبلغ المدفوع إلكترونيًا"
                        : IsState(documentType, OwnerFinancialDocumentTypes.CollectionReceipt)
                            ? "المبلغ المحصّل"
                            : "صافي المالك",
                GrandTotal = grandTotal
            };

            ApplyContactInfo(details, booking.Customer);

            details.Lines.Add(new FinancialDocumentLineDto
            {
                Description = "خدمة حجز",
                Details = booking.EndDate.HasValue
                    ? $"{booking.StartDate:yyyy-MM-dd} إلى {booking.EndDate.Value:yyyy-MM-dd}"
                    : $"{booking.StartDate:yyyy-MM-dd}",
                Quantity = Math.Max(1, booking.Quantity),
                UnitPrice = booking.Quantity > 0
                    ? Math.Round(booking.TotalPrice / booking.Quantity, 2)
                    : booking.TotalPrice,
                Total = Math.Max(0m, booking.TotalPrice)
            });

            var originalTotalForDocument = booking.OriginalTotalPrice > 0m
                ? booking.OriginalTotalPrice
                : booking.TotalPrice + booking.DiscountAmount;
            var appliedDiscountForDocument = Math.Max(0m, booking.DiscountAmount);

            AddDocumentAmountLine(
                details.AmountLines,
                appliedDiscountForDocument > 0m ? "السعر قبل الخصم" : "قيمة الحجز",
                originalTotalForDocument);

            if (appliedDiscountForDocument > 0m)
            {
                var promotionLabel = string.IsNullOrWhiteSpace(booking.PromotionTitleSnapshot)
                    ? "خصم العرض"
                    : $"خصم العرض - {booking.PromotionTitleSnapshot.Trim()}";

                AddDocumentAmountLine(
                    details.AmountLines,
                    promotionLabel,
                    appliedDiscountForDocument,
                    false,
                    true);

                // لأن الضريبة يعاد حسابها بعد الخصم، قد يكون إجمالي التوفير
                // أكبر من DiscountAmount. نظهر الفرق حتى تتطابق الفاتورة حسابيًا.
                var totalSavings = Math.Round(
                    Math.Max(0m, originalTotalForDocument - booking.TotalPrice),
                    2);
                var taxSaving = Math.Round(
                    Math.Max(0m, totalSavings - appliedDiscountForDocument),
                    2);

                if (taxSaving > 0m)
                {
                    AddDocumentAmountLine(
                        details.AmountLines,
                        "التوفير الناتج عن إعادة احتساب الضريبة",
                        taxSaving,
                        false,
                        true);
                }

                AddDocumentAmountLine(
                    details.AmountLines,
                    "إجمالي الحجز بعد الخصم",
                    booking.TotalPrice,
                    true);
            }

            AddDocumentAmountLine(details.AmountLines, "العربون المطلوب", financial?.DepositAmount ?? 0m);
            AddDocumentAmountLine(details.AmountLines, "المدفوع إلكترونيًا", paidOnline);
            AddDocumentAmountLine(details.AmountLines, "المحصّل في المنشأة", collected);
            AddDocumentAmountLine(details.AmountLines, "المتبقي للتحصيل", remaining);
            AddDocumentAmountLine(details.AmountLines, "عمولة المنصة", commission, false, true);
            AddDocumentAmountLine(details.AmountLines, "الاستردادات المكتملة", refunded, false, true);
            AddDocumentAmountLine(details.AmountLines, "صافي المالك", netOwner, true);

            return details;
        }

        private async Task<FinancialDocumentDetailsDto?> BuildRefundDocumentDetailsAsync(
            int ownerId,
            int refundRequestId,
            string currency,
            CancellationToken cancellationToken)
        {
            var refund = await _context.RefundRequests.AsNoTracking()
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking.FinancialRecord)
                .Include(item => item.Customer)
                .Include(item => item.Place)
                .Include(item => item.ProcessedByUser)
                .AsSplitQuery()
                .FirstOrDefaultAsync(item =>
                    item.RefundRequestId == refundRequestId &&
                    item.Place.OwnerId == ownerId &&
                    item.Status == FinancialRefundStatuses.Completed,
                    cancellationToken);

            if (refund == null) return null;

            var details = new FinancialDocumentDetailsDto
            {
                Currency = currency,
                DocumentType = OwnerFinancialDocumentTypes.RefundReceipt,
                DocumentTypeLabel = GetFinancialDocumentTypeLabel(OwnerFinancialDocumentTypes.RefundReceipt),
                DocumentNumber = BuildFinancialDocumentNumber(
                    OwnerFinancialDocumentTypes.RefundReceipt,
                    refundRequestId),
                DocumentDate = refund.ProcessedAt ?? refund.RequestedAt ?? DateTime.Now,
                Title = "إيصال استرداد",
                Subtitle = $"الحجز رقم {refund.BookingId.ToString(CultureInfo.InvariantCulture)}",
                SourceId = refundRequestId,
                BookingId = refund.BookingId,
                PlaceId = refund.PlaceId,
                PlaceName = FirstNonEmpty("منشأة غير معروفة", refund.Place?.PlaceNameAndDesc),
                PlaceLogoUrl = FirstNonEmpty(string.Empty, refund.Place?.LogoImage, refund.Place?.CoverImage),
                PlaceAddress = refund.Place?.AddressDetail?.Trim(),
                PlacePhone = refund.Place?.PhoneNumber?.Trim(),
                PlaceEmail = refund.Place?.EmailAddress?.Trim(),
                CustomerId = refund.CustomerId,
                CustomerName = FirstNonEmpty("عميل", refund.Customer?.FullName, refund.Customer?.Username),
                BookingStartDate = refund.Booking?.StartDate,
                BookingEndDate = refund.Booking?.EndDate,
                PromotionId = refund.Booking?.PromotionId,
                OriginalTotalPrice = refund.Booking == null
                    ? 0m
                    : refund.Booking.OriginalTotalPrice > 0m
                        ? refund.Booking.OriginalTotalPrice
                        : refund.Booking.TotalPrice + refund.Booking.DiscountAmount,
                DiscountAmount = Math.Max(0m, refund.Booking?.DiscountAmount ?? 0m),
                FinalTotalPrice = Math.Max(0m, refund.Booking?.TotalPrice ?? 0m),
                PromotionTitleSnapshot = refund.Booking?.PromotionTitleSnapshot?.Trim(),
                PromotionCodeSnapshot = refund.Booking?.PromotionCodeSnapshot?.Trim(),
                DiscountTypeSnapshot = refund.Booking?.DiscountTypeSnapshot?.Trim(),
                DiscountValueSnapshot = refund.Booking?.DiscountValueSnapshot,
                BookingStateLabel = GetReportBookingStateLabel(refund.Booking?.BookingState),
                PaymentStatusLabel = GetReportPaymentStatusLabel(refund.Booking?.PaymentStatus),
                CommissionStatusLabel = GetReportCommissionStatusLabel(
                    refund.Booking?.FinancialRecord?.CommissionStatus),
                RecordStatusLabel = GetRefundStatusLabel(refund.Status),
                ReferenceNumber = refund.ReferenceNumber?.Trim(),
                Notes = FirstNonEmpty(string.Empty, refund.Reason, refund.Notes),
                VerificationText = $"هذا إيصال استرداد إلكتروني صادر من منصة دليلك اليمني، ورقمه {BuildFinancialDocumentNumber(OwnerFinancialDocumentTypes.RefundReceipt, refundRequestId)}.",
                GrandTotalLabel = "صافي المبلغ المسترد",
                GrandTotal = Math.Max(0m, refund.RefundAmount)
            };

            ApplyContactInfo(details, refund.Customer);

            details.Lines.Add(new FinancialDocumentLineDto
            {
                Description = "استرداد مرتبط بالحجز",
                Details = refund.Reason?.Trim(),
                Quantity = 1m,
                UnitPrice = Math.Max(0m, refund.RefundAmount),
                Total = Math.Max(0m, refund.RefundAmount)
            });

            AddDocumentAmountLine(details.AmountLines, "العربون المدفوع", refund.DepositPaid);
            AddDocumentAmountLine(details.AmountLines, "رسوم الإلغاء", refund.CancellationFee, false, true);
            AddDocumentAmountLine(details.AmountLines, "المبلغ المسترد", refund.RefundAmount, true);
            AddDocumentAmountLine(details.AmountLines, "حصة المنشأة", refund.OwnerShare);
            AddDocumentAmountLine(details.AmountLines, "عمولة المنصة", refund.PlatformCommission);
            AddDocumentAmountLine(details.AmountLines, "العمولة المحررة", refund.CommissionReleased);

            return details;
        }

        private async Task<FinancialDocumentDetailsDto?> BuildTopUpDocumentDetailsAsync(
            int ownerId,
            int transactionId,
            string currency,
            CancellationToken cancellationToken)
        {
            var transaction = await _context.CommissionTransactions.AsNoTracking()
                .Include(item => item.Place)
                .Include(item => item.ReviewedByUser)
                .AsSplitQuery()
                .FirstOrDefaultAsync(item =>
                    item.CommissionTransactionId == transactionId &&
                    item.Place.OwnerId == ownerId &&
                    item.TransactionType == FinancialTransactionTypes.TopUp &&
                    item.Status == FinancialTransactionStatuses.Completed,
                    cancellationToken);

            if (transaction == null) return null;

            var details = new FinancialDocumentDetailsDto
            {
                Currency = currency,
                DocumentType = OwnerFinancialDocumentTypes.TopUpReceipt,
                DocumentTypeLabel = GetFinancialDocumentTypeLabel(OwnerFinancialDocumentTypes.TopUpReceipt),
                DocumentNumber = BuildFinancialDocumentNumber(
                    OwnerFinancialDocumentTypes.TopUpReceipt,
                    transactionId),
                DocumentDate = transaction.ReviewedAt ?? transaction.CreatedAt ?? DateTime.Now,
                Title = "إيصال شحن رصيد",
                Subtitle = "شحن رصيد ضمان العمولة",
                SourceId = transactionId,
                BookingId = transaction.BookingId,
                PlaceId = transaction.PlaceId,
                PlaceName = FirstNonEmpty("منشأة غير معروفة", transaction.Place?.PlaceNameAndDesc),
                PlaceLogoUrl = FirstNonEmpty(string.Empty, transaction.Place?.LogoImage, transaction.Place?.CoverImage),
                PlaceAddress = transaction.Place?.AddressDetail?.Trim(),
                PlacePhone = transaction.Place?.PhoneNumber?.Trim(),
                PlaceEmail = transaction.Place?.EmailAddress?.Trim(),
                CustomerName = "حساب المالك",
                RecordStatusLabel = GetTransactionStatusLabel(transaction.Status),
                ReferenceNumber = transaction.ReferenceNumber?.Trim(),
                Notes = transaction.Notes?.Trim(),
                VerificationText = $"هذا إيصال شحن إلكتروني صادر من منصة دليلك اليمني، ورقمه {BuildFinancialDocumentNumber(OwnerFinancialDocumentTypes.TopUpReceipt, transactionId)}.",
                GrandTotalLabel = "مبلغ الشحن",
                GrandTotal = Math.Max(0m, transaction.Amount)
            };

            details.Lines.Add(new FinancialDocumentLineDto
            {
                Description = "شحن رصيد ضمان العمولة",
                Details = transaction.ReferenceNumber?.Trim(),
                Quantity = 1m,
                UnitPrice = Math.Max(0m, transaction.Amount),
                Total = Math.Max(0m, transaction.Amount)
            });

            AddDocumentAmountLine(details.AmountLines, "الرصيد قبل الشحن", transaction.AvailableBalanceBefore);
            AddDocumentAmountLine(details.AmountLines, "مبلغ الشحن", transaction.Amount, true);
            AddDocumentAmountLine(details.AmountLines, "الرصيد بعد الشحن", transaction.AvailableBalanceAfter);

            return details;
        }

        public async Task<FinancialDocumentDetailsDto?> GetFinancialDocumentAsync(
            int ownerId,
            string documentType,
            int sourceId,
            CancellationToken cancellationToken = default)
        {
            if (ownerId <= 0 || sourceId <= 0) return null;

            documentType = NormalizeAllowedValue(documentType, OwnerFinancialDocumentTypes.All)
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(documentType)) return null;

            var settings = await GetSettingsAsync(cancellationToken);
            var currency = GetCurrency(settings);

            if (IsState(documentType, OwnerFinancialDocumentTypes.RefundReceipt))
                return await BuildRefundDocumentDetailsAsync(
                    ownerId,
                    sourceId,
                    currency,
                    cancellationToken);

            if (IsState(documentType, OwnerFinancialDocumentTypes.TopUpReceipt))
                return await BuildTopUpDocumentDetailsAsync(
                    ownerId,
                    sourceId,
                    currency,
                    cancellationToken);

            return await BuildBookingDocumentDetailsAsync(
                ownerId,
                documentType,
                sourceId,
                currency,
                cancellationToken);
        }

        private static List<FinancialReportExportColumnDto>
            BuildExportColumns(
                string reportType)
        {
            FinancialReportExportColumnDto Column(
                string key,
                string label,
                string dataType = "Text",
                string alignment = "Right",
                double width = 16d) =>
                    new FinancialReportExportColumnDto
                    {
                        Key = key,
                        Label = label,
                        DataType = dataType,
                        Alignment = alignment,
                        Width = width
                    };

            if (IsState(reportType, OwnerFinancialReportTypes.Documents))
            {
                return new List<FinancialReportExportColumnDto>
                {
                    Column("RecordNumber", "رقم المستند", width: 20d),
                    Column("RecordTypeLabel", "نوع المستند", width: 24d),
                    Column("RecordDate", "تاريخ المستند", "DateTime", "Center", 20d),
                    Column("BookingId", "رقم الحجز", "Number", "Center", 13d),
                    Column("CustomerName", "العميل", width: 22d),
                    Column("PlaceName", "المنشأة", width: 26d),
                    Column("OriginalTotalPrice", "قبل الخصم", "Money", "Left", 18d),
                    Column("DiscountAmount", "الخصم", "Money", "Left", 16d),
                    Column("PromotionTitleSnapshot", "العرض", width: 22d),
                    Column("ReferenceNumber", "المرجع", width: 20d),
                    Column("StatusLabel", "الحالة", width: 16d),
                    Column("PrimaryAmount", "قيمة المستند", "Money", "Left", 18d)
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Bookings))
            {
                return new List<FinancialReportExportColumnDto>
                {
                    Column("BookingId", "رقم الحجز", "Number", "Center", 13d),
                    Column("CustomerName", "العميل", width: 22d),
                    Column("PlaceName", "المنشأة", width: 25d),
                    Column("BookingCreatedAt", "تاريخ الإنشاء", "DateTime", "Center", 20d),
                    Column("BookingStartDate", "بداية الإقامة", "Date", "Center", 16d),
                    Column("BookingEndDate", "نهاية الإقامة", "Date", "Center", 16d),
                    Column("BookingStateLabel", "حالة الحجز", width: 16d),
                    Column("PaymentStatusLabel", "حالة الدفع", width: 16d),
                    Column("OriginalTotalPrice", "قبل الخصم", "Money", "Left", 18d),
                    Column("DiscountAmount", "الخصم", "Money", "Left", 16d),
                    Column("PromotionTitleSnapshot", "العرض", width: 22d),
                    Column("TotalPrice", "بعد الخصم", "Money", "Left", 18d),
                    Column("DepositAmount", "العربون", "Money", "Left", 16d),
                    Column("ActualCollectedAmount", "إجمالي المحصّل", "Money", "Left", 18d),
                    Column("RemainingAmount", "المتبقي", "Money", "Left", 17d),
                    Column("ActualRevenue", "الإيراد الفعلي", "Money", "Left", 18d)
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Payments))
            {
                return new List<FinancialReportExportColumnDto>
                {
                    Column("BookingId", "رقم الحجز", "Number", "Center", 13d),
                    Column("CustomerName", "العميل", width: 22d),
                    Column("PlaceName", "المنشأة", width: 25d),
                    Column("PaymentStatusLabel", "حالة الدفع", width: 17d),
                    Column("PaymentChannelLabel", "قناة التحصيل", width: 17d),
                    Column("OriginalTotalPrice", "قبل الخصم", "Money", "Left", 18d),
                    Column("DiscountAmount", "الخصم", "Money", "Left", 16d),
                    Column("PromotionTitleSnapshot", "العرض", width: 22d),
                    Column("TotalPrice", "بعد الخصم", "Money", "Left", 18d),
                    Column("ActualPaidOnline", "مدفوع إلكترونيًا", "Money", "Left", 19d),
                    Column("ActualCollectedAtProperty", "محصّل في المنشأة", "Money", "Left", 20d),
                    Column("ActualCollectedAmount", "إجمالي المحصّل", "Money", "Left", 18d),
                    Column("RemainingAmount", "المتبقي", "Money", "Left", 17d),
                    Column("ReferenceNumber", "مرجع التحويل", width: 20d),
                    Column("PaymentDate", "تاريخ الدفع", "DateTime", "Center", 20d),
                    Column("CollectionDate", "تاريخ التحصيل", "DateTime", "Center", 20d)
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Commissions))
            {
                return new List<FinancialReportExportColumnDto>
                {
                    Column("BookingId", "رقم الحجز", "Number", "Center", 13d),
                    Column("PlaceName", "المنشأة", width: 25d),
                    Column("OriginalTotalPrice", "قبل الخصم", "Money", "Left", 18d),
                    Column("DiscountAmount", "الخصم", "Money", "Left", 16d),
                    Column("TotalPrice", "بعد الخصم", "Money", "Left", 18d),
                    Column("PlatformCommission", "العمولة المحسوبة", "Money", "Left", 19d),
                    Column("CommissionHeld", "المحجوزة", "Money", "Left", 16d),
                    Column("CommissionCharged", "المخصومة", "Money", "Left", 16d),
                    Column("CommissionReleased", "المحررة", "Money", "Left", 16d),
                    Column("CommissionDebt", "الدين", "Money", "Left", 16d),
                    Column("CommissionOutstanding", "المتبقية", "Money", "Left", 16d),
                    Column("CommissionStatusLabel", "حالة العمولة", width: 17d),
                    Column("CommissionDate", "تاريخ التسوية", "DateTime", "Center", 20d)
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Refunds))
            {
                return new List<FinancialReportExportColumnDto>
                {
                    Column("RecordId", "رقم الطلب", "Number", "Center", 13d),
                    Column("BookingId", "رقم الحجز", "Number", "Center", 13d),
                    Column("CustomerName", "العميل", width: 22d),
                    Column("PlaceName", "المنشأة", width: 25d),
                    Column("OriginalTotalPrice", "قبل الخصم", "Money", "Left", 18d),
                    Column("DiscountAmount", "الخصم", "Money", "Left", 16d),
                    Column("PromotionTitleSnapshot", "العرض", width: 22d),
                    Column("TotalPrice", "بعد الخصم", "Money", "Left", 18d),
                    Column("DepositAmount", "العربون", "Money", "Left", 16d),
                    Column("CancellationFee", "رسوم الإلغاء", "Money", "Left", 17d),
                    Column("RequestedRefundAmount", "المطلوب استرداده", "Money", "Left", 19d),
                    Column("RefundAmount", "المسترد فعليًا", "Money", "Left", 18d),
                    Column("OwnerShare", "حصة المنشأة", "Money", "Left", 17d),
                    Column("PlatformCommission", "عمولة المنصة", "Money", "Left", 17d),
                    Column("CommissionReleased", "العمولة المحررة", "Money", "Left", 18d),
                    Column("StatusLabel", "حالة الطلب", width: 16d),
                    Column("RequestedAt", "تاريخ الطلب", "DateTime", "Center", 20d),
                    Column("ProcessedAt", "تاريخ المعالجة", "DateTime", "Center", 20d),
                    Column("ReferenceNumber", "المرجع", width: 20d)
                };
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Transactions))
            {
                return new List<FinancialReportExportColumnDto>
                {
                    Column("RecordNumber", "رقم الحركة", width: 18d),
                    Column("TransactionTypeLabel", "نوع الحركة", width: 19d),
                    Column("PlaceName", "المنشأة", width: 25d),
                    Column("BookingId", "رقم الحجز", "Number", "Center", 13d),
                    Column("ReferenceNumber", "المرجع", width: 20d),
                    Column("StatusLabel", "الحالة", width: 15d),
                    Column("PrimaryAmount", "المبلغ", "Money", "Left", 17d),
                    Column("AvailableBalanceBefore", "المتاح قبل", "Money", "Left", 17d),
                    Column("AvailableBalanceAfter", "المتاح بعد", "Money", "Left", 17d),
                    Column("HeldBalanceBefore", "المحجوز قبل", "Money", "Left", 17d),
                    Column("HeldBalanceAfter", "المحجوز بعد", "Money", "Left", 17d),
                    Column("DebtBefore", "الدين قبل", "Money", "Left", 16d),
                    Column("DebtAfter", "الدين بعد", "Money", "Left", 16d),
                    Column("RecordDate", "تاريخ الحركة", "DateTime", "Center", 20d)
                };
            }

            return new List<FinancialReportExportColumnDto>
            {
                Column("BookingId", "رقم الحجز", "Number", "Center", 13d),
                Column("CustomerName", "العميل", width: 22d),
                Column("PlaceName", "المنشأة", width: 25d),
                Column("BookingStateLabel", "حالة الحجز", width: 16d),
                Column("PaymentStatusLabel", "حالة الدفع", width: 16d),
                Column("OriginalTotalPrice", "قبل الخصم", "Money", "Left", 18d),
                Column("DiscountAmount", "الخصم", "Money", "Left", 16d),
                Column("PromotionTitleSnapshot", "العرض", width: 22d),
                Column("TotalPrice", "بعد الخصم", "Money", "Left", 18d),
                Column("ActualPaidOnline", "مدفوع إلكترونيًا", "Money", "Left", 19d),
                Column("ActualCollectedAtProperty", "محصّل في المنشأة", "Money", "Left", 20d),
                Column("RemainingAmount", "المتبقي", "Money", "Left", 17d),
                Column("CommissionCharged", "العمولة المخصومة", "Money", "Left", 19d),
                Column("RefundAmount", "الاسترداد المكتمل", "Money", "Left", 19d),
                Column("CancellationFee", "رسوم الإلغاء", "Money", "Left", 17d),
                Column("OwnerShare", "حصة المنشأة", "Money", "Left", 17d),
                Column("NetOwnerAmount", "صافي المالك", "Money", "Left", 18d)
            };
        }

        private static FinancialReportExportCellDto TextCell(
            string? value) =>
                new FinancialReportExportCellDto
                {
                    Text = value?.Trim() ?? string.Empty,
                    DataType = "Text"
                };

        private static FinancialReportExportCellDto NumberCell(
            long? value) =>
                new FinancialReportExportCellDto
                {
                    Text = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    DataType = "Number",
                    IntegerValue = value
                };

        private static FinancialReportExportCellDto MoneyCell(
            decimal value) =>
                new FinancialReportExportCellDto
                {
                    Text = value.ToString("0.##", CultureInfo.InvariantCulture),
                    DataType = "Money",
                    DecimalValue = value
                };

        private static FinancialReportExportCellDto DateCell(
            DateTime? value,
            bool includeTime) =>
                new FinancialReportExportCellDto
                {
                    Text = value.HasValue
                        ? value.Value.ToString(
                            includeTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)
                        : string.Empty,
                    DataType = includeTime ? "DateTime" : "Date",
                    DateValue = value
                };

        private static FinancialReportExportCellDto GetExportCell(
            FinancialReportRowDto row,
            string key)
        {
            switch (key)
            {
                case "RecordId": return NumberCell(row.RecordId);
                case "RecordNumber": return TextCell(row.RecordNumber);
                case "RecordTypeLabel": return TextCell(row.RecordTypeLabel);
                case "RecordDate": return DateCell(row.RecordDate, true);
                case "BookingId": return NumberCell(row.BookingId);
                case "CustomerName": return TextCell(row.CustomerName);
                case "PlaceName": return TextCell(row.PlaceName);
                case "BookingCreatedAt": return DateCell(row.BookingCreatedAt, true);
                case "BookingStartDate": return DateCell(row.BookingStartDate, false);
                case "BookingEndDate": return DateCell(row.BookingEndDate, false);
                case "PaymentDate": return DateCell(row.PaymentDate, true);
                case "CollectionDate": return DateCell(row.CollectionDate, true);
                case "CommissionDate": return DateCell(row.CommissionDate, true);
                case "RequestedAt": return DateCell(row.RequestedAt, true);
                case "ProcessedAt": return DateCell(row.ProcessedAt, true);
                case "BookingStateLabel": return TextCell(row.BookingStateLabel);
                case "PaymentStatusLabel": return TextCell(row.PaymentStatusLabel);
                case "PaymentChannelLabel": return TextCell(row.PaymentChannelLabel);
                case "CommissionStatusLabel": return TextCell(row.CommissionStatusLabel);
                case "StatusLabel": return TextCell(row.StatusLabel);
                case "TransactionTypeLabel": return TextCell(row.TransactionTypeLabel);
                case "ReferenceNumber": return TextCell(row.ReferenceNumber);
                case "Description": return TextCell(row.Description);
                case "PrimaryAmount": return MoneyCell(row.PrimaryAmount);
                case "OriginalTotalPrice": return MoneyCell(row.OriginalTotalPrice);
                case "DiscountAmount": return MoneyCell(row.DiscountAmount);
                case "PromotionTitleSnapshot": return TextCell(row.PromotionTitleSnapshot);
                case "PromotionCodeSnapshot": return TextCell(row.PromotionCodeSnapshot);
                case "DiscountTypeSnapshot": return TextCell(row.DiscountTypeSnapshot);
                case "DiscountValueSnapshot":
                    return row.DiscountValueSnapshot.HasValue
                    ? MoneyCell(row.DiscountValueSnapshot.Value)
                    : TextCell(string.Empty);
                case "TotalPrice": return MoneyCell(row.TotalPrice);
                case "DepositAmount": return MoneyCell(row.DepositAmount);
                case "AmountPaidOnline": return MoneyCell(row.AmountPaidOnline);
                case "AmountCollectedAtProperty": return MoneyCell(row.AmountCollectedAtProperty);
                case "ActualPaidOnline": return MoneyCell(row.ActualPaidOnline);
                case "ActualCollectedAtProperty": return MoneyCell(row.ActualCollectedAtProperty);
                case "ActualCollectedAmount": return MoneyCell(row.ActualCollectedAmount);
                case "RemainingAmount": return MoneyCell(row.RemainingAmount);
                case "ActualRevenue": return MoneyCell(row.ActualRevenue);
                case "PlatformCommission": return MoneyCell(row.PlatformCommission);
                case "CommissionHeld": return MoneyCell(row.CommissionHeld);
                case "CommissionCharged": return MoneyCell(row.CommissionCharged);
                case "CommissionReleased": return MoneyCell(row.CommissionReleased);
                case "CommissionDebt": return MoneyCell(row.CommissionDebt);
                case "CommissionOutstanding": return MoneyCell(row.CommissionOutstanding);
                case "RequestedRefundAmount": return MoneyCell(row.RequestedRefundAmount);
                case "RefundAmount": return MoneyCell(row.RefundAmount);
                case "CancellationFee": return MoneyCell(row.CancellationFee);
                case "OwnerShare": return MoneyCell(row.OwnerShare);
                case "TopUpAmount": return MoneyCell(row.TopUpAmount);
                case "NetOwnerAmount": return MoneyCell(row.NetOwnerAmount);
                case "AvailableBalanceBefore": return MoneyCell(row.AvailableBalanceBefore);
                case "AvailableBalanceAfter": return MoneyCell(row.AvailableBalanceAfter);
                case "HeldBalanceBefore": return MoneyCell(row.HeldBalanceBefore);
                case "HeldBalanceAfter": return MoneyCell(row.HeldBalanceAfter);
                case "DebtBefore": return MoneyCell(row.DebtBefore);
                case "DebtAfter": return MoneyCell(row.DebtAfter);
                default: return TextCell(string.Empty);
            }
        }

        private static FinancialReportExportRowDto BuildExportRow(
            FinancialReportRowDto row,
            IReadOnlyCollection<FinancialReportExportColumnDto> columns)
        {
            var typedCells = columns
                .Select(column => GetExportCell(row, column.Key))
                .ToList();

            return new FinancialReportExportRowDto
            {
                TypedCells = typedCells,
                Cells = typedCells
                    .Select(cell => cell.Text)
                    .ToList()
            };
        }

        private static string FormatReportMoney(
            decimal value,
            string currency) =>
                string.Concat(
                    value.ToString("N2", CultureInfo.GetCultureInfo("en-US")),
                    " ",
                    currency);

        private static List<FinancialReportMetricDto> BuildExportMetrics(
            FinancialReportSummaryDto summary,
            string reportType,
            string currency)
        {
            var metrics = new List<FinancialReportMetricDto>
            {
                new FinancialReportMetricDto
                {
                    Label = "السجلات المطابقة",
                    Value = summary.RecordsCount.ToString("N0", CultureInfo.GetCultureInfo("en-US")),
                    Hint = "كل النتائج المطابقة للفلاتر",
                    Tone = "Info"
                }
            };

            if (IsState(reportType, OwnerFinancialReportTypes.Documents))
            {
                metrics.Add(new FinancialReportMetricDto
                {
                    Label = "قيمة المستندات",
                    Value = FormatReportMoney(summary.TotalDocumentValue, currency),
                    Hint = "أرشيف المستندات الظاهرة",
                    Tone = "Primary"
                });

                return metrics;
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Bookings))
            {
                metrics.AddRange(new[]
                {
                    new FinancialReportMetricDto
                    {
                        Label = "قيمة الحجوزات قبل الخصم",
                        Value = FormatReportMoney(summary.TotalOriginalBookingValue, currency),
                        Hint = string.Concat(summary.DiscountedBookingsCount, " حجز بخصم"),
                        Tone = "Info"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "إجمالي الخصومات",
                        Value = FormatReportMoney(summary.TotalDiscountAmount, currency),
                        Hint = "الخصومات المحفوظة وقت إنشاء الحجوزات",
                        Tone = "Warning"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "قيمة الحجوزات بعد الخصم",
                        Value = FormatReportMoney(summary.TotalBookingValue, currency),
                        Hint = string.Concat("متوسط الحجز ", FormatReportMoney(summary.AverageBookingValue, currency)),
                        Tone = "Primary"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "الإيراد الفعلي",
                        Value = FormatReportMoney(summary.ActualRevenue, currency),
                        Hint = "الحجوزات الملغاة مستبعدة من الإيراد",
                        Tone = "Success"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "إجمالي المحصّل",
                        Value = FormatReportMoney(summary.TotalPaid, currency),
                        Hint = string.Concat("نسبة التحصيل ", summary.CollectionRate.ToString("N2", CultureInfo.InvariantCulture), "%"),
                        Tone = "Info"
                    }
                });

                return metrics;
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Payments))
            {
                metrics.AddRange(new[]
                {
                    new FinancialReportMetricDto
                    {
                        Label = "مدفوع إلكترونيًا",
                        Value = FormatReportMoney(summary.TotalPaidOnline, currency),
                        Tone = "Primary"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "محصّل في المنشأة",
                        Value = FormatReportMoney(summary.TotalCollectedAtProperty, currency),
                        Tone = "Success"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "المتبقي",
                        Value = FormatReportMoney(summary.TotalRemainingAtProperty, currency),
                        Hint = string.Concat("نسبة التحصيل ", summary.CollectionRate.ToString("N2", CultureInfo.InvariantCulture), "%"),
                        Tone = "Warning"
                    }
                });

                return metrics;
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Commissions))
            {
                metrics.AddRange(new[]
                {
                    new FinancialReportMetricDto
                    {
                        Label = "العمولة المحسوبة",
                        Value = FormatReportMoney(summary.TotalCommission, currency),
                        Tone = "Primary"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "المخصومة",
                        Value = FormatReportMoney(summary.TotalCommissionCharged, currency),
                        Tone = "Success"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "المتبقية أو الدين",
                        Value = FormatReportMoney(
                            summary.TotalCommissionOutstanding + summary.TotalCommissionDebt,
                            currency),
                        Tone = "Warning"
                    }
                });

                return metrics;
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Refunds))
            {
                metrics.AddRange(new[]
                {
                    new FinancialReportMetricDto
                    {
                        Label = "طلبات معلقة",
                        Value = FormatReportMoney(summary.PendingRefundAmount, currency),
                        Hint = string.Concat(summary.OpenRefundsCount, " طلب"),
                        Tone = "Warning"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "مسترد فعليًا",
                        Value = FormatReportMoney(summary.TotalRefunded, currency),
                        Hint = string.Concat(summary.CompletedRefundsCount, " طلب مكتمل"),
                        Tone = "Success"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "حصة المنشأة",
                        Value = FormatReportMoney(summary.TotalOwnerShare, currency),
                        Tone = "Primary"
                    }
                });

                return metrics;
            }

            if (IsState(reportType, OwnerFinancialReportTypes.Transactions))
            {
                metrics.AddRange(new[]
                {
                    new FinancialReportMetricDto
                    {
                        Label = "شحن مكتمل",
                        Value = FormatReportMoney(summary.TotalTopUps, currency),
                        Tone = "Success"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "شحن معلق",
                        Value = FormatReportMoney(summary.TotalPendingTopUps, currency),
                        Tone = "Warning"
                    },
                    new FinancialReportMetricDto
                    {
                        Label = "حركات مكتملة",
                        Value = summary.CompletedTransactionsCount.ToString("N0", CultureInfo.InvariantCulture),
                        Tone = "Info"
                    }
                });

                return metrics;
            }

            metrics.AddRange(new[]
            {
                new FinancialReportMetricDto
                {
                    Label = "صافي المالك",
                    Value = FormatReportMoney(summary.NetRevenue, currency),
                    Tone = "Success"
                },
                new FinancialReportMetricDto
                {
                    Label = "شحن الرصيد المكتمل",
                    Value = FormatReportMoney(summary.TotalTopUps, currency),
                    Hint = string.Concat("المعلق ", FormatReportMoney(summary.TotalPendingTopUps, currency)),
                    Tone = "Primary"
                },
                new FinancialReportMetricDto
                {
                    Label = "الدين الحالي",
                    Value = FormatReportMoney(summary.CurrentDebt, currency),
                    Tone = summary.CurrentDebt > 0m ? "Warning" : "Neutral"
                }
            });

            return metrics;
        }

        private static List<FinancialReportInsightDto> BuildExportInsights(
            IReadOnlyCollection<FinancialReportRowDto> rows,
            FinancialReportSummaryDto summary,
            string reportType,
            string currency)
        {
            var insights = new List<FinancialReportInsightDto>();

            if (rows.Count == 0)
            {
                insights.Add(new FinancialReportInsightDto
                {
                    Title = "لا توجد نتائج",
                    Value = "0",
                    Description = "لم يتم العثور على سجلات مطابقة للفلاتر الحالية.",
                    Icon = "bi-inbox",
                    Tone = "Neutral"
                });

                return insights;
            }

            var largest = rows
                .OrderByDescending(row => row.PrimaryAmount)
                .First();

            insights.Add(new FinancialReportInsightDto
            {
                Title = "أكبر قيمة في النتائج",
                Value = FormatReportMoney(largest.PrimaryAmount, currency),
                Description = FirstNonEmpty("سجل مالي", largest.RecordNumber, largest.PlaceName),
                Icon = "bi-graph-up-arrow",
                Tone = "Info"
            });

            if (IsState(reportType, OwnerFinancialReportTypes.Bookings))
            {
                insights.Add(new FinancialReportInsightDto
                {
                    Title = "الحجوزات الملغاة",
                    Value = summary.CancelledBookingsCount.ToString("N0", CultureInfo.InvariantCulture),
                    Description = "تظهر في التقرير ولا تدخل في الإيراد الفعلي.",
                    Icon = "bi-calendar-x",
                    Tone = "Warning"
                });
            }
            else if (IsState(reportType, OwnerFinancialReportTypes.Refunds))
            {
                insights.Add(new FinancialReportInsightDto
                {
                    Title = "المسترد المكتمل فقط",
                    Value = FormatReportMoney(summary.TotalRefunded, currency),
                    Description = "المعلق والمرفوض والفاشل والملغى غير داخل في المبلغ المسترد فعليًا.",
                    Icon = "bi-shield-check",
                    Tone = "Success"
                });
            }
            else if (IsState(reportType, OwnerFinancialReportTypes.Comprehensive))
            {
                insights.Add(new FinancialReportInsightDto
                {
                    Title = "صافي التدفق التشغيلي",
                    Value = FormatReportMoney(summary.NetCashFlow, currency),
                    Description = "التحصيل الفعلي ناقص الاستردادات المكتملة والعمولة المخصومة.",
                    Icon = "bi-cash-coin",
                    Tone = "Success"
                });
            }

            return insights;
        }

        private static List<FinancialReportBreakdownItemDto>
            BuildPlaceBreakdown(
                IReadOnlyCollection<FinancialReportRowDto> rows)
        {
            var totalAmount = rows.Sum(row => Math.Max(0m, row.PrimaryAmount));

            return rows
                .Where(row => row.PlaceId > 0)
                .GroupBy(row => new { row.PlaceId, row.PlaceName })
                .Select(group =>
                {
                    var amount = group.Sum(row => Math.Max(0m, row.PrimaryAmount));

                    return new FinancialReportBreakdownItemDto
                    {
                        Label = group.Key.PlaceName,
                        Count = group.Count(),
                        Amount = amount,
                        Percentage = totalAmount <= 0m
                            ? 0m
                            : Math.Round((amount / totalAmount) * 100m, 2)
                    };
                })
                .OrderByDescending(item => item.Amount)
                .ThenByDescending(item => item.Count)
                .Take(12)
                .ToList();
        }

        private static List<FinancialReportBreakdownItemDto>
            BuildStatusBreakdown(
                IReadOnlyCollection<FinancialReportRowDto> rows)
        {
            var totalAmount = rows.Sum(row => Math.Max(0m, row.PrimaryAmount));

            return rows
                .GroupBy(row => FirstNonEmpty(
                    "غير محددة",
                    row.StatusLabel,
                    row.BookingStateLabel,
                    row.PaymentStatusLabel,
                    row.CommissionStatusLabel))
                .Select(group =>
                {
                    var amount = group.Sum(row => Math.Max(0m, row.PrimaryAmount));

                    return new FinancialReportBreakdownItemDto
                    {
                        Label = group.Key,
                        Count = group.Count(),
                        Amount = amount,
                        Percentage = totalAmount <= 0m
                            ? 0m
                            : Math.Round((amount / totalAmount) * 100m, 2)
                    };
                })
                .OrderByDescending(item => item.Count)
                .ThenByDescending(item => item.Amount)
                .Take(12)
                .ToList();
        }

        private static string BuildReportPeriodLabel(
            FinancialReportFilterDto filter)
        {
            var scope = GetFinancialDateScopeLabel(filter.DateScope);

            if (!filter.FromDate.HasValue && !filter.ToDate.HasValue)
            {
                return string.Concat(scope, ": كل الفترات");
            }

            if (filter.FromDate.HasValue && filter.ToDate.HasValue)
            {
                return string.Concat(
                    scope,
                    ": من ",
                    filter.FromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    " إلى ",
                    filter.ToDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            if (filter.FromDate.HasValue)
            {
                return string.Concat(
                    scope,
                    ": من ",
                    filter.FromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return string.Concat(
                scope,
                ": حتى ",
                filter.ToDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        private static string BuildReportFilterDescription(
            FinancialReportFilterDto filter)
        {
            var values = new List<string>();

            if (filter.BookingId.HasValue)
            {
                values.Add(string.Concat("الحجز #", filter.BookingId.Value));
            }

            if (filter.CustomerId.HasValue)
            {
                values.Add(string.Concat("العميل #", filter.CustomerId.Value));
            }

            if (!string.IsNullOrWhiteSpace(filter.DocumentType))
            {
                values.Add(GetFinancialDocumentTypeLabel(filter.DocumentType));
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentChannel) &&
                !IsState(filter.PaymentChannel, OwnerFinancialPaymentChannels.All))
            {
                values.Add(string.Concat("القناة: ", GetPaymentChannelLabel(filter.PaymentChannel)));
            }

            if (!string.IsNullOrWhiteSpace(filter.BookingState))
            {
                values.Add(string.Concat("الحجز: ", GetReportBookingStateLabel(filter.BookingState)));
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentStatus))
            {
                values.Add(string.Concat("الدفع: ", GetReportPaymentStatusLabel(filter.PaymentStatus)));
            }

            if (!string.IsNullOrWhiteSpace(filter.CommissionStatus))
            {
                values.Add(string.Concat("العمولة: ", GetReportCommissionStatusLabel(filter.CommissionStatus)));
            }

            if (!string.IsNullOrWhiteSpace(filter.RefundStatus))
            {
                values.Add(string.Concat("الاسترداد: ", GetRefundStatusLabel(filter.RefundStatus)));
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionType))
            {
                values.Add(string.Concat("الحركة: ", GetTransactionTypeLabel(filter.TransactionType)));
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionStatus))
            {
                values.Add(string.Concat("حالة الحركة: ", GetTransactionStatusLabel(filter.TransactionStatus)));
            }

            if (!string.IsNullOrWhiteSpace(filter.ReferenceNumber))
            {
                values.Add(string.Concat("المرجع: ", filter.ReferenceNumber));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                values.Add(string.Concat("البحث: ", filter.Search));
            }

            return values.Count == 0
                ? "بدون فلاتر إضافية"
                : string.Join(" • ", values);
        }

        private static FinancialReportExportSectionDto BuildMainExportSection(
            string reportType,
            IReadOnlyCollection<FinancialReportRowDto> rows)
        {
            var columns = BuildExportColumns(reportType);

            return new FinancialReportExportSectionDto
            {
                Key = reportType,
                Title = GetFinancialReportTypeLabel(reportType),
                Description = GetFinancialReportDescription(reportType),
                Columns = columns,
                Rows = rows
                    .Select(row => BuildExportRow(row, columns))
                    .ToList()
            };
        }

        private static FinancialReportExportSectionDto
            BuildReconciliationExportSection(
                FinancialReconciliationDto reconciliation)
        {
            var columns = new List<FinancialReportExportColumnDto>
            {
                new FinancialReportExportColumnDto
                {
                    Key = "Check",
                    Label = "فحص المطابقة",
                    DataType = "Text",
                    Width = 42d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Result",
                    Label = "النتيجة",
                    DataType = "Text",
                    Width = 20d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Issues",
                    Label = "عدد الاختلافات",
                    DataType = "Number",
                    Alignment = "Center",
                    Width = 18d
                }
            };

            FinancialReportExportRowDto Row(
                string label,
                int issues) =>
                new FinancialReportExportRowDto
                {
                    Cells = new List<string>
                    {
                        label,
                        issues == 0 ? "مطابق" : "يحتاج مراجعة",
                        issues.ToString(CultureInfo.InvariantCulture)
                    },
                    TypedCells = new List<FinancialReportExportCellDto>
                    {
                        TextCell(label),
                        TextCell(issues == 0 ? "مطابق" : "يحتاج مراجعة"),
                        NumberCell(issues)
                    }
                };

            return new FinancialReportExportSectionDto
            {
                Key = "Reconciliation",
                Title = "سلامة المطابقة المالية",
                Description = string.Concat(
                    "فحص قراءة فقط على ",
                    reconciliation.CheckedRecords.ToString(
                        CultureInfo.InvariantCulture),
                    " سجل قبل Pagination. النتيجة العامة: ",
                    reconciliation.StatusLabel,
                    "."),
                Columns = columns,
                Rows = new List<FinancialReportExportRowDto>
                {
                    Row("مطابقة التحصيل", reconciliation.CollectionMismatchCount),
                    Row("تمويل الاستردادات", reconciliation.RefundFundingMismatchCount),
                    Row("اتساق السعر والخصم", reconciliation.PriceMismatchCount),
                    Row("سلامة قيم الأرصدة", reconciliation.NegativeBalanceCount)
                }
            };
        }

        private static FinancialReportExportSectionDto
            BuildComprehensiveAnalyticsSection(
                ComprehensiveFinancialAnalyticsDto analytics,
                string currency)
        {
            var columns = new List<FinancialReportExportColumnDto>
            {
                new FinancialReportExportColumnDto
                {
                    Key = "Label",
                    Label = "المؤشر",
                    DataType = "Text",
                    Width = 32d
                },
                new FinancialReportExportColumnDto
                {
                    Key = "Value",
                    Label = "القيمة",
                    DataType = "Money",
                    Alignment = "Left",
                    Width = 22d
                }
            };

            FinancialReportExportRowDto Row(string label, decimal value) =>
                new FinancialReportExportRowDto
                {
                    Cells = new List<string>
                    {
                        label,
                        value.ToString("0.##", CultureInfo.InvariantCulture)
                    },
                    TypedCells = new List<FinancialReportExportCellDto>
                    {
                        TextCell(label),
                        MoneyCell(value)
                    }
                };

            return new FinancialReportExportSectionDto
            {
                Key = "ComprehensiveAnalytics",
                Title = "التحليلات المالية المستقلة",
                Description = string.Concat(
                    "الحركات وشحن الرصيد والدين والاستردادات داخل الفترة. العملة: ",
                    currency),
                Columns = columns,
                Rows = new List<FinancialReportExportRowDto>
                {
                    Row("شحن الرصيد المكتمل", analytics.CompletedTopUpsAmount),
                    Row("شحن الرصيد المعلق", analytics.PendingTopUpsAmount),
                    Row("العمولة المحجوزة", analytics.CommissionHeldAmount),
                    Row("العمولة المخصومة", analytics.CommissionChargedAmount),
                    Row("العمولة المحررة", analytics.CommissionReleasedAmount),
                    Row("الدين المسجل خلال الفترة", analytics.CommissionDebtCreatedAmount),
                    Row("الرصيد المتاح الحالي", analytics.CurrentAvailableGuaranteeBalance),
                    Row("الرصيد المحجوز الحالي", analytics.CurrentHeldGuaranteeBalance),
                    Row("الدين الحالي", analytics.CurrentDebt),
                    Row("الاستردادات المكتملة", analytics.CompletedRefundAmount),
                    Row("الاستردادات المعلقة", analytics.PendingRefundAmount),
                    Row("صافي التدفق التشغيلي", analytics.OperationalNetCashFlow),
                    Row("صافي تدفق رصيد الضمان", analytics.GuaranteeNetFlow)
                }
            };
        }

        public async Task<FinancialReportExportDto>
            BuildFinancialReportExportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken = default)
        {
            filter = await NormalizeFinancialReportFilterAsync(
                ownerId,
                filter,
                cancellationToken);

            var settings = await GetSettingsAsync(cancellationToken);
            var currency = GetCurrency(settings);
            var selectedPlaceName = await GetSelectedFinancialPlaceNameAsync(
                ownerId,
                filter.PlaceId,
                cancellationToken);

            var engine = await BuildFinancialReportEngineAsync(
                ownerId,
                filter,
                cancellationToken);

            var mainSection = BuildMainExportSection(
                filter.ReportType,
                engine.Rows);

            var sections = new List<FinancialReportExportSectionDto>
            {
                mainSection
            };

            if (IsState(filter.ReportType, OwnerFinancialReportTypes.Comprehensive))
            {
                sections.Add(BuildComprehensiveAnalyticsSection(
                    engine.Analytics,
                    currency));
            }

            if (engine.Reconciliation.IsApplicable)
            {
                sections.Add(
                    BuildReconciliationExportSection(
                        engine.Reconciliation));
            }

            var fromPart = filter.FromDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "all";
            var toPart = filter.ToDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "all";

            return new FinancialReportExportDto
            {
                Currency = currency,
                ReportType = filter.ReportType,
                Title = GetFinancialReportTypeLabel(filter.ReportType),
                Subtitle = GetFinancialReportDescription(filter.ReportType),
                SelectedPlaceName = selectedPlaceName,
                PeriodLabel = BuildReportPeriodLabel(filter),
                DateScopeLabel = GetFinancialDateScopeLabel(filter.DateScope),
                FilterDescription = BuildReportFilterDescription(filter),
                SuggestedFileName = string.Concat(
                    "Dalilak-",
                    filter.ReportType,
                    "-",
                    fromPart,
                    "-",
                    toPart),
                GeneratedAt = DateTime.Now,
                TotalAvailableRows = engine.Rows.Count,
                IsTruncated = false,
                Summary = engine.Summary,
                Analytics = engine.Analytics,
                Reconciliation = engine.Reconciliation,
                Columns = mainSection.Columns,
                Rows = mainSection.Rows,
                Sections = sections,
                Metrics = BuildExportMetrics(
                    engine.Summary,
                    filter.ReportType,
                    currency),
                Insights = BuildExportInsights(
                    engine.Rows,
                    engine.Summary,
                    filter.ReportType,
                    currency),
                PlaceBreakdown = BuildPlaceBreakdown(engine.Rows),
                StatusBreakdown = BuildStatusBreakdown(engine.Rows)
            };
        }

        public async Task<FinancialExcelFileDto> BuildFinancialExcelAsync(
            int ownerId,
            FinancialReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new FinancialReportFilterDto();
            filter.ExportFormat = OwnerFinancialExportFormats.Excel;

            var report = await BuildFinancialReportExportAsync(
                ownerId,
                filter,
                cancellationToken);

            var builder = new OwnerFinancialExcelBuilder();
            var content = builder.Build(report);

            var safeName = string.IsNullOrWhiteSpace(report.SuggestedFileName)
                ? "Dalilak-Financial-Report"
                : report.SuggestedFileName.Trim();

            return new FinancialExcelFileDto
            {
                Content = content,
                FileName = string.Concat(safeName, ".xlsx")
            };
        }


        /* ====================================================================
           Free trial + booking guarantee access
           ==================================================================== */

        public async Task<FinanceOperationResult> TryStartOwnerFreeTrialForPlaceAsync(
            int placeId,
            CancellationToken cancellationToken = default)
        {
            if (placeId <= 0)
            {
                return FinanceOperationResult.Failed("رقم المنشأة غير صحيح.");
            }

            await using var databaseTransaction =
                await BeginCommissionTransactionIfNeededAsync(cancellationToken);

            try
            {
                var settings = await GetSettingsAsync(cancellationToken);

                var place = await _context.Places
                    .Include(item => item.Category)
                    .FirstOrDefaultAsync(
                        item => item.PlaceId == placeId,
                        cancellationToken);

                if (place == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed("المنشأة غير موجودة.");
                }

                if (!place.OwnerId.HasValue || place.OwnerId.Value <= 0)
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "المنشأة لا ترتبط بحساب مالك يمكن منحه تجربة مجانية.",
                        place.PlaceId);
                }

                if (!IsBookingEnabledCategory(place.Category))
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "الفئة لا تستخدم نظام الحجوزات، لذلك لا تحتاج تجربة ضمان العمولات.",
                        place.PlaceId);
                }

                if (!IsOperationalPlace(place))
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "لم تبدأ التجربة لأن المنشأة لم تصبح جاهزة لاستقبال الحجوزات بعد.",
                        place.PlaceId);
                }

                if (settings?.PlatformOwnerUserId.HasValue == true &&
                    settings.PlatformOwnerUserId.Value == place.OwnerId.Value)
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "حساب تشغيل المنصة مستثنى من التجربة المجانية للملاك.",
                        place.PlaceId);
                }

                var owner = await _context.Users
                    .FirstOrDefaultAsync(
                        item => item.UserId == place.OwnerId.Value,
                        cancellationToken);

                if (owner == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed("تعذر العثور على حساب مالك المنشأة.");
                }

                /*
                 * بمجرد وجود أي تاريخ للتجربة نعتبر أن المالك سبق أن دخل
                 * دورة التجربة. لا نعيد ضبط التواريخ عند اعتماد منشأة ثانية
                 * أو عند إعادة تنشيط منشأة قديمة.
                 */
                if (owner.FreeTrialStartedAt.HasValue ||
                    owner.FreeTrialEndsAt.HasValue)
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "تم تحديد دورة التجربة لهذا المالك مسبقًا.",
                        place.PlaceId);
                }

                var trialDays = Math.Max(0, settings?.OwnerFreeTrialDays ?? 0);
                var trialEnabled = settings?.OwnerFreeTrialEnabled == true;

                if (!trialEnabled || trialDays <= 0)
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "الفترة التجريبية غير مفعلة للملاك الجدد حاليًا.",
                        place.PlaceId);
                }

                var now = DateTime.Now;

                owner.FreeTrialStartedAt = now;
                owner.FreeTrialEndsAt = now.AddDays(trialDays);

                await _context.SaveChangesAsync(cancellationToken);
                await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                return FinanceOperationResult.Succeeded(
                    $"بدأت الفترة التجريبية للمالك لمدة {trialDays} يومًا.",
                    place.PlaceId);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);

                _logger.LogWarning(
                    exception,
                    "تعارض متزامن أثناء بدء تجربة المالك للمنشأة {PlaceId}.",
                    placeId);

                return new FinanceOperationResult
                {
                    Success = false,
                    ConcurrencyConflict = true,
                    Message = "تغيرت بيانات المالك أثناء بدء التجربة. أعد المحاولة."
                };
            }
            catch (Exception exception)
            {
                await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);

                _logger.LogError(
                    exception,
                    "تعذر بدء تجربة المالك للمنشأة {PlaceId}.",
                    placeId);

                return FinanceOperationResult.Failed(
                    "تعذر بدء الفترة التجريبية للمالك حاليًا.");
            }
        }

        public async Task<BookingFinancialAccessResult> GetBookingFinancialAccessAsync(
            int placeId,
            CancellationToken cancellationToken = default)
        {
            var result = new BookingFinancialAccessResult
            {
                CanAcceptNewBookings = false,
                Message = "تعذر التحقق من السماح المالي بالحجز."
            };

            if (placeId <= 0)
            {
                result.Message = "رقم المنشأة غير صحيح.";
                return result;
            }

            var place = await _context.Places
                .AsNoTracking()
                .Include(item => item.Category)
                .FirstOrDefaultAsync(
                    item => item.PlaceId == placeId,
                    cancellationToken);

            if (place == null)
            {
                result.Message = "المنشأة غير موجودة.";
                return result;
            }

            result.IsBookingCategory =
                IsBookingEnabledCategory(place.Category);

            /*
             * الفئات غير المرتبطة بنظام الحجوزات لا تدخل في بوابة
             * التجربة/الضمان من الأساس.
             */
            if (!result.IsBookingCategory)
            {
                result.CanAcceptNewBookings = true;
                result.Message = "الفئة لا تستخدم نظام الحجوزات.";
                return result;
            }

            if (!IsOperationalPlace(place))
            {
                result.Message =
                    "المنشأة ليست جاهزة حاليًا لاستقبال حجوزات جديدة.";
                return result;
            }

            if (!place.OwnerId.HasValue || place.OwnerId.Value <= 0)
            {
                result.Message =
                    "لا يوجد مالك مرتبط بالمنشأة للتحقق من سياستها المالية.";
                return result;
            }

            var settings = await GetSettingsAsync(cancellationToken);
            var owner = await _context.Users
                .AsNoTracking()
                .Where(item => item.UserId == place.OwnerId.Value)
                .Select(item => new
                {
                    item.FreeTrialStartedAt,
                    item.FreeTrialEndsAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (owner == null)
            {
                result.Message = "تعذر العثور على حساب مالك المنشأة.";
                return result;
            }

            result.FreeTrialStartedAt = owner.FreeTrialStartedAt;
            result.FreeTrialEndsAt = owner.FreeTrialEndsAt;

            var now = DateTime.Now;
            var trialActive =
                owner.FreeTrialStartedAt.HasValue &&
                owner.FreeTrialEndsAt.HasValue &&
                owner.FreeTrialStartedAt.Value <= now &&
                owner.FreeTrialEndsAt.Value > now;

            if (trialActive)
            {
                var remaining =
                    owner.FreeTrialEndsAt!.Value - now;

                result.IsFreeTrialActive = true;
                result.RemainingTrialDays =
                    Math.Max(1, (int)Math.Ceiling(remaining.TotalDays));
                result.CanAcceptNewBookings = true;
                result.RequiredGuaranteeBalance =
                    Math.Round(
                        Math.Max(
                            0m,
                            settings?.RequiredCommissionGuaranteeBalance ?? 0m),
                        2);
                result.Message =
                    $"الفترة التجريبية فعالة، متبقي نحو {result.RemainingTrialDays} يوم.";

                return result;
            }

            var account = await GetOrCreateAccountAsync(
                placeId,
                settings,
                cancellationToken);

            RefreshAccountStatus(account, settings);
            await _context.SaveChangesAsync(cancellationToken);

            var requiredGuarantee = Math.Round(
                Math.Max(
                    0m,
                    settings?.RequiredCommissionGuaranteeBalance ?? 0m),
                2);

            var available = Math.Round(
                Math.Max(0m, account.AvailableBalance),
                2);

            result.RequiredGuaranteeBalance = requiredGuarantee;
            result.AvailableBalance = available;
            result.MissingGuaranteeBalance = Math.Round(
                Math.Max(0m, requiredGuarantee - available),
                2);
            result.AccountStatus = account.AccountStatus?.Trim() ?? string.Empty;

            if (available + 0.01m < requiredGuarantee)
            {
                result.Message =
                    result.MissingGuaranteeBalance > 0m
                        ? $"يجب استكمال رصيد ضمان العمولة بمبلغ {result.MissingGuaranteeBalance:N2} قبل استقبال حجوزات جديدة."
                        : "رصيد ضمان العمولة غير كافٍ لاستقبال حجوزات جديدة.";

                return result;
            }

            if (IsState(
                    account.AccountStatus,
                    FinancialAccountStatuses.Suspended) &&
                (settings?.SuspendBookingWhenBalanceInsufficient ?? true))
            {
                result.Message =
                    account.DebtAmount > 0m
                        ? "حساب ضمان العمولة موقوف حاليًا بسبب سياسة الدين."
                        : "حساب ضمان العمولة موقوف حاليًا. اشحن الرصيد أو راجع حالة الحساب.";

                return result;
            }

            result.CanAcceptNewBookings = true;
            result.Message =
                owner.FreeTrialEndsAt.HasValue &&
                owner.FreeTrialEndsAt.Value <= now
                    ? "انتهت الفترة التجريبية ورصيد الضمان المطلوب متوفر."
                    : "رصيد ضمان العمولة المطلوب متوفر.";

            return result;
        }

        public async Task<FinanceOperationResult> EnsurePlaceCommissionAccountAsync(
            int placeId,
            CancellationToken cancellationToken = default)
        {
            if (placeId <= 0)
            {
                return FinanceOperationResult.Failed("رقم المنشأة غير صحيح.");
            }

            var placeExists = await _context.Places
                .AsNoTracking()
                .AnyAsync(place => place.PlaceId == placeId, cancellationToken);

            if (!placeExists)
            {
                return FinanceOperationResult.Failed("المنشأة غير موجودة.");
            }

            try
            {
                var settings = await GetSettingsAsync(cancellationToken);
                var account = await GetOrCreateAccountAsync(
                    placeId,
                    settings,
                    cancellationToken);

                RefreshAccountStatus(account, settings);
                await _context.SaveChangesAsync(cancellationToken);

                return new FinanceOperationResult
                {
                    Success = true,
                    Message = "حساب ضمان العمولة جاهز.",
                    EntityId = account.PlaceCommissionAccountId,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount
                };
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تجهيز حساب ضمان العمولة للمنشأة {PlaceId}.",
                    placeId);

                return FinanceOperationResult.Failed(
                    "تعذر تجهيز حساب ضمان العمولة.");
            }
        }

        public async Task<FinanceOperationResult> HoldBookingCommissionAsync(
            int bookingId,
            int performedByUserId,
            CancellationToken cancellationToken = default)
        {
            await using var databaseTransaction =
                await BeginCommissionTransactionIfNeededAsync(
                    cancellationToken);

            try
            {
                var booking = await _context.Bookings
                    .Include(item => item.Place)
                    .Include(item => item.FinancialRecord)
                    .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);

                if (booking == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed("الحجز غير موجود.");
                }

                var financial = booking.FinancialRecord;
                if (financial == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يوجد سجل مالي مرتبط بهذا الحجز.");
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Held) ||
                    IsState(financial.CommissionStatus, FinancialCommissionStatuses.Debt))
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "عمولة الحجز محجوزة مسبقًا.",
                        financial.RecordId);
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Charged))
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "تم خصم عمولة هذا الحجز مسبقًا.",
                        financial.RecordId);
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Released))
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "تم تحرير عمولة هذا الحجز. أعد احتسابها قبل محاولة الحجز من جديد.");
                }

                var now = DateTime.Now;

                /*
                 * الحجز الذي أُنشئ أثناء الفترة التجريبية المجانية يكون
                 * معفى نهائيًا من عمولة المنصة. شحن رصيد الضمان خلال
                 * التجربة لا ينهيها ولا يفعّل الخصم مبكرًا؛ الرصيد يبقى
                 * متاحًا لاستخدامه بعد انتهاء FreeTrialEndsAt.
                 *
                 * نعتمد وقت إنشاء الحجز كمرجع حتى يظل الحجز الذي بدأ داخل
                 * التجربة مجانيًا حتى لو تم قبوله أو إكماله بعد انتهائها.
                 * كما نصفر Snapshot العمولة لمنع أي إعادة احتساب لاحقة من
                 * إنشاء عمولة على حجز كان مشمولًا بالتجربة المجانية.
                 */
                if (booking.Place?.OwnerId.HasValue == true &&
                    booking.Place.OwnerId.Value > 0)
                {
                    var ownerTrial = await _context.Users
                        .AsNoTracking()
                        .Where(user =>
                            user.UserId == booking.Place.OwnerId.Value)
                        .Select(user => new
                        {
                            user.FreeTrialStartedAt,
                            user.FreeTrialEndsAt
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    var commissionReferenceAt =
                        booking.CreatedAt ?? now;

                    var freeTrialApplies =
                        ownerTrial != null &&
                        ownerTrial.FreeTrialStartedAt.HasValue &&
                        ownerTrial.FreeTrialEndsAt.HasValue &&
                        commissionReferenceAt >= ownerTrial.FreeTrialStartedAt.Value &&
                        commissionReferenceAt < ownerTrial.FreeTrialEndsAt.Value;

                    if (freeTrialApplies)
                    {
                        financial.CommissionRateSnapshot = 0m;
                        financial.CommissionBaseAmount = 0m;
                        financial.PlatformCommission = 0m;
                        financial.CommissionStatus =
                            FinancialCommissionStatuses.NotApplicable;
                        financial.CommissionHeldAt = null;
                        financial.CommissionChargedAt = null;
                        financial.CommissionReleasedAt = null;
                        financial.UpdatedAt = now;

                        await _context.SaveChangesAsync(cancellationToken);
                        await CommitIfOwnedAsync(
                            databaseTransaction,
                            cancellationToken);

                        return FinanceOperationResult.Succeeded(
                            "الحجز ضمن الفترة التجريبية المجانية؛ لم تُحجز أو تُخصم أي عمولة.",
                            financial.RecordId);
                    }
                }

                var amount = Math.Round(
                    Math.Max(0m, financial.PlatformCommission),
                    2);

                if (amount <= 0m)
                {
                    financial.CommissionStatus =
                        FinancialCommissionStatuses.NotApplicable;
                    financial.CommissionHeldAt = null;
                    financial.UpdatedAt = now;

                    await _context.SaveChangesAsync(cancellationToken);
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                    return FinanceOperationResult.Succeeded(
                        "لا توجد عمولة مستحقة على هذا الحجز.",
                        financial.RecordId);
                }

                var settings = await GetSettingsAsync(cancellationToken);
                var account = await GetOrCreateAccountAsync(
                    booking.PlaceId,
                    settings,
                    cancellationToken);

                RefreshAccountStatus(account, settings);

                var availableBefore = Math.Round(Math.Max(0m, account.AvailableBalance), 2);
                var heldBefore = Math.Round(Math.Max(0m, account.HeldBalance), 2);
                var debtBefore = Math.Round(Math.Max(0m, account.DebtAmount), 2);

                var availableAmount = availableBefore;
                var amountFromAvailable = Math.Min(availableAmount, amount);
                var shortage = Math.Round(amount - amountFromAvailable, 2);

                var allowNegative =
                    account.AllowNegativeBalance ??
                    settings?.AllowNegativeCommissionBalance ??
                    false;

                var debtLimit = Math.Round(
                    Math.Max(0m, account.DebtLimit ?? settings?.DefaultCommissionDebtLimit ?? 0m),
                    2);

                var suspendWhenInsufficient =
                    settings?.SuspendBookingWhenBalanceInsufficient ?? true;

                if (account.DebtAmount > 0m &&
                    account.DebtDueAt.HasValue &&
                    account.DebtDueAt.Value <= now &&
                    suspendWhenInsufficient)
                {
                    account.AccountStatus = FinancialAccountStatuses.Suspended;
                    await _context.SaveChangesAsync(cancellationToken);
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                    return new FinanceOperationResult
                    {
                        Success = false,
                        Message = "الحساب المالي موقوف بسبب تجاوز موعد سداد الدين. سدد الدين أولًا.",
                        EntityId = account.PlaceCommissionAccountId,
                        AvailableBalance = account.AvailableBalance,
                        HeldBalance = account.HeldBalance,
                        DebtAmount = account.DebtAmount
                    };
                }

                if (shortage > 0m)
                {
                    if (!allowNegative)
                    {
                        if (suspendWhenInsufficient)
                        {
                            account.AccountStatus = FinancialAccountStatuses.Suspended;
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                        await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                        return new FinanceOperationResult
                        {
                            Success = false,
                            Message = "الرصيد غير كافٍ والدين غير مسموح لهذا الحساب. اشحن رصيد العمولات أولًا.",
                            EntityId = account.PlaceCommissionAccountId,
                            AvailableBalance = account.AvailableBalance,
                            HeldBalance = account.HeldBalance,
                            DebtAmount = account.DebtAmount
                        };
                    }

                    var proposedDebt = Math.Round(debtBefore + shortage, 2);
                    if (debtLimit <= 0m || proposedDebt > debtLimit + 0.01m)
                    {
                        if (suspendWhenInsufficient)
                        {
                            account.AccountStatus = FinancialAccountStatuses.Suspended;
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                        await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                        return new FinanceOperationResult
                        {
                            Success = false,
                            Message = debtLimit <= 0m
                                ? "الدين مسموح لكن حد الدين للحساب يساوي صفر. حدّث سياسة الدين أو اشحن الرصيد."
                                : $"الحجز يتجاوز حد الدين المسموح ({debtLimit:N2}). المتاح من حد الدين لا يكفي لتغطية العمولة.",
                            EntityId = account.PlaceCommissionAccountId,
                            AvailableBalance = account.AvailableBalance,
                            HeldBalance = account.HeldBalance,
                            DebtAmount = account.DebtAmount
                        };
                    }
                }

                account.AvailableBalance = Math.Round(
                    Math.Max(0m, account.AvailableBalance - amountFromAvailable),
                    2);

                account.HeldBalance = Math.Round(
                    Math.Max(0m, account.HeldBalance) + amount,
                    2);

                if (shortage > 0m)
                {
                    account.DebtAmount = Math.Round(
                        Math.Max(0m, account.DebtAmount) + shortage,
                        2);
                }

                RefreshAccountStatus(account, settings);

                financial.CommissionStatus = shortage > 0m
                    ? FinancialCommissionStatuses.Debt
                    : FinancialCommissionStatuses.Held;
                financial.CommissionHeldAt = now;
                financial.CommissionChargedAt = null;
                financial.CommissionReleasedAt = null;
                financial.UpdatedAt = now;

                _context.CommissionTransactions.Add(
                    CreateMovement(
                        account,
                        booking.PlaceId,
                        booking.BookingId,
                        FinancialTransactionTypes.CommissionHold,
                        amount,
                        availableBefore,
                        heldBefore,
                        debtBefore,
                        performedByUserId,
                        shortage > 0m
                            ? $"حجز العمولة؛ استُخدم دين بقيمة {shortage:N2}."
                            : "حجز عمولة الحجز من الرصيد المتاح."));

                await _context.SaveChangesAsync(cancellationToken);
                await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                return new FinanceOperationResult
                {
                    Success = true,
                    Message = shortage > 0m
                        ? "تم حجز العمولة، وسُجل الجزء غير المغطى كدين."
                        : "تم حجز عمولة الحجز من رصيد الضمان.",
                    EntityId = financial.RecordId,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount
                };
            }
            catch (Exception exception)
            {
                await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                _logger.LogError(
                    exception,
                    "فشل حجز عمولة الحجز {BookingId}.",
                    bookingId);

                return FinanceOperationResult.Failed(
                    "تعذر حجز عمولة الحجز حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> ChargeBookingCommissionAsync(
            int bookingId,
            int performedByUserId,
            CancellationToken cancellationToken = default)
        {
            await using var databaseTransaction =
                await BeginCommissionTransactionIfNeededAsync(
                    cancellationToken);

            try
            {
                var booking = await _context.Bookings
                    .Include(item => item.FinancialRecord)
                    .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);

                if (booking == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed("الحجز غير موجود.");
                }

                var financial = booking.FinancialRecord;
                if (financial == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يوجد سجل مالي مرتبط بهذا الحجز.");
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Charged))
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "تم خصم عمولة هذا الحجز مسبقًا.",
                        financial.RecordId);
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.NotApplicable))
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "لا توجد عمولة مستحقة على هذا الحجز.",
                        financial.RecordId);
                }

                if (!IsState(financial.CommissionStatus, FinancialCommissionStatuses.Held) &&
                    !IsState(financial.CommissionStatus, FinancialCommissionStatuses.Debt))
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "يجب حجز العمولة أولًا قبل خصمها نهائيًا.");
                }

                var amount = Math.Round(Math.Max(0m, financial.PlatformCommission), 2);
                var settings = await GetSettingsAsync(cancellationToken);
                var account = await GetOrCreateAccountAsync(
                    booking.PlaceId,
                    settings,
                    cancellationToken);

                if (account.HeldBalance + 0.01m < amount)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "الرصيد المحجوز لا يطابق عمولة الحجز. راجع كشف الحركات.");
                }

                var availableBefore = account.AvailableBalance;
                var heldBefore = account.HeldBalance;
                var debtBefore = account.DebtAmount;
                var now = DateTime.Now;

                account.HeldBalance = Math.Round(
                    Math.Max(0m, account.HeldBalance - amount),
                    2);
                RefreshAccountStatus(account, settings);

                financial.CommissionStatus = FinancialCommissionStatuses.Charged;
                financial.CommissionChargedAt = now;
                financial.UpdatedAt = now;

                _context.CommissionTransactions.Add(
                    CreateMovement(
                        account,
                        booking.PlaceId,
                        booking.BookingId,
                        FinancialTransactionTypes.CommissionCharge,
                        amount,
                        availableBefore,
                        heldBefore,
                        debtBefore,
                        performedByUserId,
                        IsState(booking.BookingState, "NoShow")
                            ? "خصم العمولة المستحقة على العربون المصادَر بسبب عدم حضور العميل."
                            : IsState(booking.BookingState, "Cancelled")
                                ? "خصم العمولة المستحقة على غرامة إلغاء العميل."
                                : "تحويل العمولة المحجوزة إلى عمولة نهائية بعد إكمال الحجز."));

                await _context.SaveChangesAsync(cancellationToken);
                await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                return new FinanceOperationResult
                {
                    Success = true,
                    Message = "تم خصم عمولة الحجز نهائيًا.",
                    EntityId = financial.RecordId,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount
                };
            }
            catch (Exception exception)
            {
                await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                _logger.LogError(
                    exception,
                    "فشل خصم عمولة الحجز {BookingId}.",
                    bookingId);

                return FinanceOperationResult.Failed(
                    "تعذر خصم عمولة الحجز حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> ReleaseBookingCommissionAsync(
            int bookingId,
            int performedByUserId,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            await using var databaseTransaction =
                await BeginCommissionTransactionIfNeededAsync(
                    cancellationToken);

            try
            {
                var booking = await _context.Bookings
                    .Include(item => item.FinancialRecord)
                    .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);

                if (booking == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed("الحجز غير موجود.");
                }

                var financial = booking.FinancialRecord;
                if (financial == null)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا يوجد سجل مالي مرتبط بهذا الحجز.");
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Released))
                {
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Succeeded(
                        "تم تحرير عمولة هذا الحجز مسبقًا.",
                        financial.RecordId);
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.NotApplicable) ||
                    financial.PlatformCommission <= 0m)
                {
                    financial.CommissionStatus =
                        FinancialCommissionStatuses.NotApplicable;
                    financial.UpdatedAt = DateTime.Now;

                    await _context.SaveChangesAsync(cancellationToken);
                    await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                    return FinanceOperationResult.Succeeded(
                        "لا توجد عمولة محجوزة لهذا الحجز.",
                        financial.RecordId);
                }

                if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Charged))
                {
                    /*
                     * قد يُلغى الحجز بعد أن تكون العمولة قد خُصمت نهائيًا.
                     * في هذه الحالة لا نطلب تسوية يدوية من الإدارة:
                     * نعيد العمولة إلى حساب ضمان المنشأة بحركة عكسية واضحة،
                     * ثم يستطيع المستدعي إعادة احتساب العمولة على المبلغ
                     * المحتفظ به فقط إن وُجد.
                     */
                    var chargedAmount = Math.Round(
                        Math.Max(0m, financial.PlatformCommission),
                        2);

                    var chargedSettings =
                        await GetSettingsAsync(cancellationToken);

                    var chargedAccount =
                        await GetOrCreateAccountAsync(
                            booking.PlaceId,
                            chargedSettings,
                            cancellationToken);

                    var lastHoldMovement =
                        await _context.CommissionTransactions
                            .AsNoTracking()
                            .Where(transaction =>
                                transaction.BookingId == bookingId &&
                                transaction.TransactionType ==
                                    FinancialTransactionTypes.CommissionHold &&
                                transaction.Status ==
                                    FinancialTransactionStatuses.Completed)
                            .OrderByDescending(transaction =>
                                transaction.CreatedAt)
                            .ThenByDescending(transaction =>
                                transaction.CommissionTransactionId)
                            .FirstOrDefaultAsync(cancellationToken);

                    var chargedDebtPart =
                        lastHoldMovement == null
                            ? 0m
                            : Math.Max(
                                0m,
                                lastHoldMovement.DebtAfter -
                                lastHoldMovement.DebtBefore);

                    chargedDebtPart =
                        Math.Round(
                            Math.Min(
                                chargedAmount,
                                Math.Min(
                                    Math.Max(
                                        0m,
                                        chargedAccount.DebtAmount),
                                    chargedDebtPart)),
                            2);

                    var chargedAvailablePart =
                        Math.Round(
                            Math.Max(
                                0m,
                                chargedAmount -
                                chargedDebtPart),
                            2);

                    var chargedAvailableBefore =
                        chargedAccount.AvailableBalance;
                    var chargedHeldBefore =
                        chargedAccount.HeldBalance;
                    var chargedDebtBefore =
                        chargedAccount.DebtAmount;
                    var chargedReleasedAt =
                        DateTime.Now;

                    chargedAccount.DebtAmount =
                        Math.Round(
                            Math.Max(
                                0m,
                                chargedAccount.DebtAmount -
                                chargedDebtPart),
                            2);

                    chargedAccount.AvailableBalance =
                        Math.Round(
                            Math.Max(
                                0m,
                                chargedAccount.AvailableBalance) +
                            chargedAvailablePart,
                            2);

                    RefreshAccountStatus(
                        chargedAccount,
                        chargedSettings);

                    financial.CommissionStatus =
                        FinancialCommissionStatuses.Released;
                    financial.CommissionReleasedAt =
                        chargedReleasedAt;
                    financial.UpdatedAt =
                        chargedReleasedAt;

                    var chargedNotes =
                        string.IsNullOrWhiteSpace(reason)
                            ? "إرجاع عمولة سبق خصمها نهائيًا بسبب تسوية إلغاء/استرداد الحجز."
                            : reason.Trim();

                    _context.CommissionTransactions.Add(
                        CreateMovement(
                            chargedAccount,
                            booking.PlaceId,
                            booking.BookingId,
                            FinancialTransactionTypes.CommissionRelease,
                            chargedAmount,
                            chargedAvailableBefore,
                            chargedHeldBefore,
                            chargedDebtBefore,
                            performedByUserId,
                            chargedNotes));

                    await _context.SaveChangesAsync(cancellationToken);
                    await CommitIfOwnedAsync(
                        databaseTransaction,
                        cancellationToken);

                    return new FinanceOperationResult
                    {
                        Success = true,
                        Message =
                            "تم إرجاع العمولة المخصومة إلى حساب ضمان المنشأة.",
                        EntityId = financial.RecordId,
                        AvailableBalance =
                            chargedAccount.AvailableBalance,
                        HeldBalance =
                            chargedAccount.HeldBalance,
                        DebtAmount =
                            chargedAccount.DebtAmount
                    };
                }

                if (!IsState(financial.CommissionStatus, FinancialCommissionStatuses.Held) &&
                    !IsState(financial.CommissionStatus, FinancialCommissionStatuses.Debt))
                {
                    /*
                     * توافق مع الحجوزات المؤكدة التي أُنشئت قبل ربط دورة
                     * العمولة آليًا: قد تكون العمولة محسوبة ولكنها لم تُحجز.
                     * نغلقها كعمولة محررة دون تغيير أرصدة الحساب، مع إنشاء
                     * حركة تدقيق واضحة بدل إبقائها بحالة Calculated.
                     */
                    if (IsState(
                            financial.CommissionStatus,
                            FinancialCommissionStatuses.Calculated))
                    {
                        var calculatedSettings =
                            await GetSettingsAsync(cancellationToken);

                        var calculatedAccount =
                            await GetOrCreateAccountAsync(
                                booking.PlaceId,
                                calculatedSettings,
                                cancellationToken);

                        var calculatedAvailableBefore =
                            calculatedAccount.AvailableBalance;
                        var calculatedHeldBefore =
                            calculatedAccount.HeldBalance;
                        var calculatedDebtBefore =
                            calculatedAccount.DebtAmount;
                        var calculatedAt = DateTime.Now;
                        var calculatedAmount = Math.Round(
                            Math.Max(0m, financial.PlatformCommission),
                            2);

                        financial.CommissionStatus =
                            FinancialCommissionStatuses.Released;
                        financial.CommissionReleasedAt = calculatedAt;
                        financial.UpdatedAt = calculatedAt;

                        var calculatedNotes =
                            string.IsNullOrWhiteSpace(reason)
                                ? "إلغاء عمولة محسوبة قبل حجزها من رصيد الضمان."
                                : reason.Trim();

                        _context.CommissionTransactions.Add(
                            CreateMovement(
                                calculatedAccount,
                                booking.PlaceId,
                                booking.BookingId,
                                FinancialTransactionTypes.CommissionRelease,
                                calculatedAmount,
                                calculatedAvailableBefore,
                                calculatedHeldBefore,
                                calculatedDebtBefore,
                                performedByUserId,
                                calculatedNotes));

                        await _context.SaveChangesAsync(cancellationToken);
                        await CommitIfOwnedAsync(
                            databaseTransaction,
                            cancellationToken);

                        return FinanceOperationResult.Succeeded(
                            "تم إلغاء العمولة المحسوبة لهذا الحجز.",
                            financial.RecordId);
                    }

                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "لا توجد عمولة محجوزة يمكن تحريرها.");
                }

                var amount = Math.Round(Math.Max(0m, financial.PlatformCommission), 2);
                var settings = await GetSettingsAsync(cancellationToken);
                var account = await GetOrCreateAccountAsync(
                    booking.PlaceId,
                    settings,
                    cancellationToken);

                if (account.HeldBalance + 0.01m < amount)
                {
                    await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                    return FinanceOperationResult.Failed(
                        "الرصيد المحجوز لا يطابق عمولة الحجز. راجع كشف الحركات.");
                }

                var holdMovement = await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        transaction.BookingId == bookingId &&
                        transaction.TransactionType ==
                            FinancialTransactionTypes.CommissionHold &&
                        transaction.Status ==
                            FinancialTransactionStatuses.Completed)
                    .OrderByDescending(transaction => transaction.CreatedAt)
                    .ThenByDescending(transaction => transaction.CommissionTransactionId)
                    .FirstOrDefaultAsync(cancellationToken);

                var debtPart = holdMovement == null
                    ? 0m
                    : Math.Max(0m, holdMovement.DebtAfter - holdMovement.DebtBefore);

                debtPart = Math.Min(
                    amount,
                    Math.Min(Math.Max(0m, account.DebtAmount), debtPart));

                var availablePart = Math.Round(amount - debtPart, 2);

                var availableBefore = account.AvailableBalance;
                var heldBefore = account.HeldBalance;
                var debtBefore = account.DebtAmount;
                var now = DateTime.Now;

                account.HeldBalance = Math.Round(
                    Math.Max(0m, account.HeldBalance - amount),
                    2);

                account.DebtAmount = Math.Round(
                    Math.Max(0m, account.DebtAmount - debtPart),
                    2);

                account.AvailableBalance = Math.Round(
                    Math.Max(0m, account.AvailableBalance) + availablePart,
                    2);

                RefreshAccountStatus(account, settings);

                financial.CommissionStatus = FinancialCommissionStatuses.Released;
                financial.CommissionReleasedAt = now;
                financial.UpdatedAt = now;

                var notes = string.IsNullOrWhiteSpace(reason)
                    ? "تحرير عمولة الحجز وإعادتها إلى حساب ضمان المنشأة."
                    : reason.Trim();

                _context.CommissionTransactions.Add(
                    CreateMovement(
                        account,
                        booking.PlaceId,
                        booking.BookingId,
                        FinancialTransactionTypes.CommissionRelease,
                        amount,
                        availableBefore,
                        heldBefore,
                        debtBefore,
                        performedByUserId,
                        notes));

                await _context.SaveChangesAsync(cancellationToken);
                await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                return new FinanceOperationResult
                {
                    Success = true,
                    Message = "تم تحرير عمولة الحجز وإعادة الجزء الممول إلى الرصيد.",
                    EntityId = financial.RecordId,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount
                };
            }
            catch (Exception exception)
            {
                await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);
                _logger.LogError(
                    exception,
                    "فشل تحرير عمولة الحجز {BookingId}.",
                    bookingId);

                return FinanceOperationResult.Failed(
                    "تعذر تحرير عمولة الحجز حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> RecalculateBookingCommissionAsync(
            int bookingId,
            decimal commissionBaseAmount,
            int performedByUserId,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            if (commissionBaseAmount < 0m)
            {
                return FinanceOperationResult.Failed(
                    "أساس احتساب العمولة لا يمكن أن يكون سالبًا.");
            }

            var financial = await _context.FinancialRecords
                .Include(record => record.Booking)
                .FirstOrDefaultAsync(record => record.BookingId == bookingId, cancellationToken);

            if (financial == null)
            {
                return FinanceOperationResult.Failed(
                    "لا يوجد سجل مالي مرتبط بهذا الحجز.");
            }

            if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Charged))
            {
                return FinanceOperationResult.Failed(
                    "لا يمكن إعادة احتساب عمولة خُصمت نهائيًا دون تسوية إدارية.");
            }

            if (IsState(financial.CommissionStatus, FinancialCommissionStatuses.Held) ||
                IsState(financial.CommissionStatus, FinancialCommissionStatuses.Debt))
            {
                return FinanceOperationResult.Failed(
                    "حرر العمولة المحجوزة أولًا، ثم أعد احتسابها.");
            }

            /*
             * إعادة الحساب تستخدم النسبة المحفوظة وقت إنشاء الحجز.
             * لا نستخدم النسبة الحالية للفئة أو النسبة العامة؛
             * حتى لا تتغير عمولات الحجوزات القديمة.
             */
            var rate =
                Math.Clamp(
                    financial.CommissionRateSnapshot,
                    0m,
                    100m);


            var previousCommission = Math.Round(
                Math.Max(0m, financial.PlatformCommission),
                2);
            var baseAmount = Math.Round(commissionBaseAmount, 2);
            var commission = Math.Round(baseAmount * rate / 100m, 2);
            var adjustmentAmount = Math.Round(
                Math.Abs(commission - previousCommission),
                2);
            var now = DateTime.Now;

            financial.CommissionRateSnapshot = rate;
            financial.CommissionBaseAmount = baseAmount;
            financial.PlatformCommission = commission;
            financial.CommissionStatus = commission > 0m
                ? FinancialCommissionStatuses.Calculated
                : FinancialCommissionStatuses.NotApplicable;
            financial.CommissionHeldAt = null;
            financial.CommissionChargedAt = null;
            financial.CommissionReleasedAt = null;
            financial.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(reason) && adjustmentAmount > 0m)
            {
                _context.CommissionTransactions.Add(new CommissionTransaction
                {
                    PlaceId = financial.Booking.PlaceId,
                    BookingId = financial.BookingId,
                    TransactionType = FinancialTransactionTypes.Adjustment,
                    Amount = adjustmentAmount,
                    AvailableBalanceBefore = 0m,
                    AvailableBalanceAfter = 0m,
                    HeldBalanceBefore = 0m,
                    HeldBalanceAfter = 0m,
                    DebtBefore = 0m,
                    DebtAfter = 0m,
                    Status = FinancialTransactionStatuses.Completed,
                    CreatedAt = now,
                    CreatedByUserId = performedByUserId > 0
                        ? performedByUserId
                        : null,
                    Notes =
                        $"{reason.Trim()} | العمولة السابقة: {previousCommission:N2}، العمولة الجديدة: {commission:N2}."
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            return FinanceOperationResult.Succeeded(
                commission > 0m
                    ? $"تم إعادة احتساب العمولة بقيمة {commission:N2}."
                    : "تم تحديث الحجز دون عمولة مستحقة.",
                financial.RecordId);
        }

        public async Task<FinanceOperationResult> RefreshCommissionAccountStatusAsync(
            int placeId,
            CancellationToken cancellationToken = default)
        {
            if (placeId <= 0)
            {
                return FinanceOperationResult.Failed("رقم المنشأة غير صحيح.");
            }

            try
            {
                var settings = await GetSettingsAsync(cancellationToken);
                var account = await GetOrCreateAccountAsync(
                    placeId,
                    settings,
                    cancellationToken);

                RefreshAccountStatus(account, settings);
                await _context.SaveChangesAsync(cancellationToken);

                return new FinanceOperationResult
                {
                    Success = true,
                    Message = "تم تحديث حالة حساب العمولات.",
                    EntityId = account.PlaceCommissionAccountId,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount
                };
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "تعارض متزامن أثناء تحديث حالة حساب عمولات المنشأة {PlaceId}.",
                    placeId);

                return new FinanceOperationResult
                {
                    Success = false,
                    ConcurrencyConflict = true,
                    Message = "تغير الحساب المالي أثناء التنفيذ. أعد المحاولة."
                };
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "فشل تحديث حالة حساب عمولات المنشأة {PlaceId}.",
                    placeId);

                return FinanceOperationResult.Failed(
                    "تعذر تحديث حالة الحساب المالي حاليًا.");
            }
        }

        public async Task<FinanceOperationResult> ApplyManualAdjustmentAsync(
            int placeId,
            decimal amount,
            int adminUserId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            if (placeId <= 0)
            {
                return FinanceOperationResult.Failed("رقم المنشأة غير صحيح.");
            }

            if (adminUserId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب الأدمن المنفذ للعملية.");
            }

            amount = Math.Round(amount, 2);
            if (amount == 0m)
            {
                return FinanceOperationResult.Failed(
                    "قيمة التعديل المالي لا يمكن أن تكون صفرًا.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return FinanceOperationResult.Failed(
                    "سبب التعديل الإداري إلزامي.");
            }

            var normalizedReason = reason.Trim();
            if (normalizedReason.Length > 1000)
            {
                return FinanceOperationResult.Failed(
                    "سبب التعديل طويل جدًا. الحد الأقصى 1000 حرف.");
            }

            await using var databaseTransaction =
                await BeginCommissionTransactionIfNeededAsync(cancellationToken);

            try
            {
                var settings = await GetSettingsAsync(cancellationToken);
                var account = await GetOrCreateAccountAsync(
                    placeId,
                    settings,
                    cancellationToken);

                RefreshAccountStatus(account, settings);

                var availableBefore = Math.Round(Math.Max(0m, account.AvailableBalance), 2);
                var heldBefore = Math.Round(Math.Max(0m, account.HeldBalance), 2);
                var debtBefore = Math.Round(Math.Max(0m, account.DebtAmount), 2);
                var absoluteAmount = Math.Abs(amount);

                if (amount > 0m)
                {
                    var debtPayment = Math.Round(
                        Math.Min(account.DebtAmount, absoluteAmount),
                        2);

                    if (debtPayment > 0m)
                    {
                        account.DebtAmount = Math.Round(
                            Math.Max(0m, account.DebtAmount - debtPayment),
                            2);
                    }

                    var availableCredit = Math.Round(
                        absoluteAmount - debtPayment,
                        2);

                    if (availableCredit > 0m)
                    {
                        account.AvailableBalance = Math.Round(
                            Math.Max(0m, account.AvailableBalance) + availableCredit,
                            2);
                    }
                }
                else
                {
                    var fromAvailable = Math.Round(
                        Math.Min(account.AvailableBalance, absoluteAmount),
                        2);

                    account.AvailableBalance = Math.Round(
                        Math.Max(0m, account.AvailableBalance - fromAvailable),
                        2);

                    var shortage = Math.Round(
                        absoluteAmount - fromAvailable,
                        2);

                    if (shortage > 0m)
                    {
                        var allowDebt =
                            account.AllowNegativeBalance ??
                            settings?.AllowNegativeCommissionBalance ??
                            false;

                        var debtLimit = Math.Round(
                            Math.Max(
                                0m,
                                account.DebtLimit ??
                                settings?.DefaultCommissionDebtLimit ??
                                0m),
                            2);

                        var proposedDebt = Math.Round(
                            account.DebtAmount + shortage,
                            2);

                        if (!allowDebt ||
                            debtLimit <= 0m ||
                            proposedDebt > debtLimit + 0.01m)
                        {
                            await RollbackIfOwnedAsync(
                                databaseTransaction,
                                cancellationToken);

                            return FinanceOperationResult.Failed(
                                "التعديل يتجاوز الرصيد المتاح وحد الدين المسموح للحساب.");
                        }

                        account.DebtAmount = proposedDebt;
                    }
                }

                RefreshAccountStatus(account, settings);

                var directionText = amount > 0m
                    ? "إضافة إدارية"
                    : "خصم إداري";

                _context.CommissionTransactions.Add(
                    CreateMovement(
                        account,
                        placeId,
                        null,
                        FinancialTransactionTypes.Adjustment,
                        absoluteAmount,
                        availableBefore,
                        heldBefore,
                        debtBefore,
                        adminUserId,
                        $"{directionText}: {normalizedReason}"));

                await _context.SaveChangesAsync(cancellationToken);
                await CommitIfOwnedAsync(databaseTransaction, cancellationToken);

                return new FinanceOperationResult
                {
                    Success = true,
                    Message = "تم تنفيذ التعديل الإداري وتسجيله في كشف الحركات.",
                    EntityId = account.PlaceCommissionAccountId,
                    AvailableBalance = account.AvailableBalance,
                    HeldBalance = account.HeldBalance,
                    DebtAmount = account.DebtAmount
                };
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);

                _logger.LogWarning(
                    exception,
                    "تعارض متزامن أثناء تعديل حساب عمولات المنشأة {PlaceId}.",
                    placeId);

                return new FinanceOperationResult
                {
                    Success = false,
                    ConcurrencyConflict = true,
                    Message = "تغير الرصيد أثناء تنفيذ التعديل. أعد تحميل البيانات ثم حاول مرة أخرى."
                };
            }
            catch (Exception exception)
            {
                await RollbackIfOwnedAsync(databaseTransaction, cancellationToken);

                _logger.LogError(
                    exception,
                    "فشل التعديل الإداري على حساب عمولات المنشأة {PlaceId}.",
                    placeId);

                return FinanceOperationResult.Failed(
                    "تعذر تنفيذ التعديل الإداري حاليًا.");
            }
        }

    }
}
