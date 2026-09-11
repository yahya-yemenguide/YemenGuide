using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// طبقة مالية المالك الخفيفة.
    ///
    /// المصدر المحاسبي الوحيد يبقى ICommissionFinanceService، وهذه الطبقة
    /// تضيف فوقه سياق المالك وعملة العرض فقط. جميع القيم المخزنة والحركات
    /// الفعلية داخل قاعدة البيانات تبقى بالريال اليمني YER، ثم تُحوّل هنا
    /// لعملة المالك المختارة من الملف الشخصي بحسب سوق مدينة المنشأة.
    ///
    /// لا تغيّر هذه الطبقة AvailableBalance/HeldBalance/DebtAmount مباشرة.
    /// كل تغيير فعلي على حساب العمولة يمر عبر ICommissionFinanceService.
    /// </summary>
    public sealed class OwnerFinancialService : IOwnerFinancialService
    {
        private readonly AppDbContext _context;
        private readonly ICommissionFinanceService _commissionFinanceService;
        private readonly CurrencyDisplayService _currencyDisplayService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<OwnerFinancialService> _logger;

        public OwnerFinancialService(
            AppDbContext context,
            ICommissionFinanceService commissionFinanceService,
            CurrencyDisplayService currencyDisplayService,
            INotificationService notificationService,
            ILogger<OwnerFinancialService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commissionFinanceService = commissionFinanceService ??
                throw new ArgumentNullException(nameof(commissionFinanceService));
            _currencyDisplayService = currencyDisplayService ??
                throw new ArgumentNullException(nameof(currencyDisplayService));
            _notificationService = notificationService ??
                throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int?> ResolveActivePlaceIdAsync(
            int ownerId,
            int? requestedPlaceId,
            int? cookiePlaceId,
            CancellationToken cancellationToken = default)
        {
            if (ownerId <= 0)
            {
                return null;
            }

            var placeIds = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .OrderByDescending(place =>
                    place.Status == "Approved" ||
                    place.Status == "Active")
                .ThenBy(place => place.PlaceNameAndDesc)
                .Select(place => place.PlaceId)
                .ToListAsync(cancellationToken);

            if (placeIds.Count == 0)
            {
                return null;
            }

            if (requestedPlaceId.HasValue &&
                requestedPlaceId.Value > 0 &&
                placeIds.Contains(requestedPlaceId.Value))
            {
                return requestedPlaceId.Value;
            }

            if (cookiePlaceId.HasValue &&
                cookiePlaceId.Value > 0 &&
                placeIds.Contains(cookiePlaceId.Value))
            {
                return cookiePlaceId.Value;
            }

            return placeIds[0];
        }

        public async Task<OwnerFinancialPageDto> GetShellAsync(
            int ownerId,
            int? selectedPlaceId,
            CancellationToken cancellationToken = default)
        {
            var model = await _commissionFinanceService.GetFinancialPageAsync(
                ownerId,
                selectedPlaceId,
                cancellationToken);

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var regions = await LoadPlaceRegionsAsync(
                ownerId,
                model.Places.Select(item => item.PlaceId),
                cancellationToken);

            model.Currency = currency;

            foreach (var place in model.Places)
            {
                ConvertPlaceOption(
                    place,
                    currency,
                    ResolveRegion(regions, place.PlaceId));
            }

            return model;
        }

        public async Task<FinancialOverviewTabDto> GetOverviewAsync(
            int ownerId,
            int? placeId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var model = await _commissionFinanceService.GetOverviewAsync(
                ownerId,
                placeId,
                fromDate,
                toDate,
                cancellationToken);

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var regionIds = model.Accounts.Select(item => item.PlaceId)
                .Concat(model.RecentTransactions.Select(item => item.PlaceId))
                .Concat(model.SelectedPlaceId.HasValue
                    ? new[] { model.SelectedPlaceId.Value }
                    : Array.Empty<int>());

            var regions = await LoadPlaceRegionsAsync(
                ownerId,
                regionIds,
                cancellationToken);

            var selectedRegion = ResolveRegion(
                regions,
                model.SelectedPlaceId ?? placeId);

            model.Currency = currency;
            ConvertOverviewSummary(model.Summary, currency, selectedRegion);

            foreach (var account in model.Accounts)
            {
                ConvertPlaceAccount(
                    account,
                    currency,
                    ResolveRegion(regions, account.PlaceId));
            }

            foreach (var transaction in model.RecentTransactions)
            {
                ConvertTransaction(
                    transaction,
                    currency,
                    ResolveRegion(regions, transaction.PlaceId));
            }

            return model;
        }

        public async Task<BookingAccountsTabDto> GetBookingAccountsAsync(
            int ownerId,
            BookingFinancialFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new BookingFinancialFilterDto();

            var model = await _commissionFinanceService.GetBookingAccountsAsync(
                ownerId,
                filter,
                cancellationToken);

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var regionIds = model.Bookings.Select(item => item.PlaceId)
                .Concat(model.Places.Select(item => item.PlaceId))
                .Concat(filter.PlaceId.HasValue
                    ? new[] { filter.PlaceId.Value }
                    : Array.Empty<int>());

            var regions = await LoadPlaceRegionsAsync(
                ownerId,
                regionIds,
                cancellationToken);

            var selectedRegion = ResolveRegion(regions, filter.PlaceId);

            model.Currency = currency;
            ConvertBookingSummary(model.Summary, currency, selectedRegion);

            foreach (var place in model.Places)
            {
                ConvertPlaceOption(
                    place,
                    currency,
                    ResolveRegion(regions, place.PlaceId));
            }

            foreach (var booking in model.Bookings)
            {
                ConvertBookingItem(
                    booking,
                    currency,
                    ResolveRegion(regions, booking.PlaceId));
            }

            return model;
        }

        public async Task<OwnerCommissionBalanceBackendDto> GetCommissionBalanceAsync(
            int ownerId,
            int? placeId,
            CommissionTransactionFilterDto? transactionFilter = null,
            CancellationToken cancellationToken = default)
        {
            transactionFilter ??= new CommissionTransactionFilterDto();
            transactionFilter.PlaceId = placeId;

            // نفس قرار السماح المالي المستخدم فعليًا قبل إنشاء الحجز.
            var bookingAccess = placeId.HasValue && placeId.Value > 0
                ? await _commissionFinanceService.GetBookingFinancialAccessAsync(
                    placeId.Value,
                    cancellationToken)
                : new BookingFinancialAccessResult
                {
                    CanAcceptNewBookings = false,
                    Message = "اختر منشأة لعرض حالة التجربة وضمان التفعيل."
                };

            // لا نستخدم Task.WhenAll هنا لأن الخدمات تشترك في نفس DbContext scoped.
            var account = await _commissionFinanceService
                .GetCommissionGuaranteeAsync(ownerId, cancellationToken);

            if (placeId.HasValue)
            {
                account.Accounts = account.Accounts
                    .Where(item => item.PlaceId == placeId.Value)
                    .ToList();
            }

            var topUp = await _commissionFinanceService
                .GetTopUpBalanceAsync(
                    ownerId,
                    placeId,
                    cancellationToken);

            if (placeId.HasValue)
            {
                topUp.Accounts = topUp.Accounts
                    .Where(item => item.PlaceId == placeId.Value)
                    .ToList();
            }

            var transactions = await _commissionFinanceService
                .GetTransactionsAsync(
                    ownerId,
                    transactionFilter,
                    cancellationToken);

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var regionIds = account.Accounts.Select(item => item.PlaceId)
                .Concat(topUp.Accounts.Select(item => item.PlaceId))
                .Concat(topUp.RecentTopUps.Select(item => item.PlaceId))
                .Concat(transactions.Transactions.Select(item => item.PlaceId))
                .Concat(placeId.HasValue
                    ? new[] { placeId.Value }
                    : Array.Empty<int>());

            var regions = await LoadPlaceRegionsAsync(
                ownerId,
                regionIds,
                cancellationToken);

            var selectedRegion = ResolveRegion(regions, placeId);

            account.Currency = currency;
            account.DefaultLowBalanceThreshold = Money(
                account.DefaultLowBalanceThreshold,
                currency,
                selectedRegion);
            account.DefaultDebtLimit = Money(
                account.DefaultDebtLimit,
                currency,
                selectedRegion);

            foreach (var item in account.Accounts)
            {
                ConvertPlaceAccount(
                    item,
                    currency,
                    ResolveRegion(regions, item.PlaceId));
            }

            topUp.Currency = currency;
            topUp.MinimumTopUpAmount = Money(
                topUp.MinimumTopUpAmount,
                currency,
                selectedRegion);

            foreach (var item in topUp.Accounts)
            {
                ConvertPlaceAccount(
                    item,
                    currency,
                    ResolveRegion(regions, item.PlaceId));
            }

            foreach (var item in topUp.RecentTopUps)
            {
                ConvertTransaction(
                    item,
                    currency,
                    ResolveRegion(regions, item.PlaceId));
            }

            transactions.Currency = currency;
            ConvertTransactionSummary(
                transactions.Summary,
                currency,
                selectedRegion);

            foreach (var item in transactions.Transactions)
            {
                ConvertTransaction(
                    item,
                    currency,
                    ResolveRegion(regions, item.PlaceId));
            }

            ConvertBookingAccess(
                bookingAccess,
                currency,
                selectedRegion);

            return new OwnerCommissionBalanceBackendDto
            {
                Account = account,
                TopUp = topUp,
                Transactions = transactions,
                BookingAccess = bookingAccess
            };
        }

        public async Task<RefundsTabDto> GetRefundsAsync(
            int ownerId,
            RefundFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new RefundFilterDto();

            var model = await _commissionFinanceService.GetRefundsAsync(
                ownerId,
                filter,
                cancellationToken);

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var regionIds = model.Refunds.Select(item => item.PlaceId)
                .Concat(model.SelectedPlaceId.HasValue
                    ? new[] { model.SelectedPlaceId.Value }
                    : Array.Empty<int>())
                .Concat(filter.PlaceId.HasValue
                    ? new[] { filter.PlaceId.Value }
                    : Array.Empty<int>());

            var regions = await LoadPlaceRegionsAsync(
                ownerId,
                regionIds,
                cancellationToken);

            var selectedRegion = ResolveRegion(
                regions,
                model.SelectedPlaceId ?? filter.PlaceId);

            model.Currency = currency;
            ConvertRefundSummary(model.Summary, currency, selectedRegion);

            foreach (var refund in model.Refunds)
            {
                ConvertRefundItem(
                    refund,
                    currency,
                    ResolveRegion(regions, refund.PlaceId));
            }

            return model;
        }

        public Task<OwnerWalletsTabDto> GetReceivingWalletsAsync(
            int ownerId,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.GetOwnerWalletsAsync(
                ownerId,
                cancellationToken);

        public async Task<InvoicesReportsTabDto> GetReportsAsync(
            int ownerId,
            FinancialReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new FinancialReportFilterDto();

            var model = await _commissionFinanceService.GetInvoicesReportsAsync(
                ownerId,
                filter,
                cancellationToken);

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var regionIds = model.Documents.Select(item => item.PlaceId)
                .Concat(model.ReportRows.Select(item => item.PlaceId))
                .Concat(model.SelectedPlaceId.HasValue
                    ? new[] { model.SelectedPlaceId.Value }
                    : Array.Empty<int>())
                .Concat(filter.PlaceId.HasValue
                    ? new[] { filter.PlaceId.Value }
                    : Array.Empty<int>());

            var regions = await LoadPlaceRegionsAsync(
                ownerId,
                regionIds,
                cancellationToken);

            var selectedRegion = ResolveRegion(
                regions,
                model.SelectedPlaceId ?? filter.PlaceId);

            model.Currency = currency;
            ConvertReportSummary(model.Summary, currency, selectedRegion);
            ConvertAnalytics(model.Analytics, currency, selectedRegion);
            model.Reconciliation.MaximumCollectionDifference = Money(
                model.Reconciliation.MaximumCollectionDifference,
                currency,
                selectedRegion);

            foreach (var document in model.Documents)
            {
                ConvertDocumentItem(
                    document,
                    currency,
                    ResolveRegion(regions, document.PlaceId));
            }

            foreach (var row in model.ReportRows)
            {
                ConvertReportRow(
                    row,
                    currency,
                    ResolveRegion(regions, row.PlaceId));
            }

            return model;
        }

        /// <summary>
        /// يعيد المستند المالي بنفس عملة العرض المختارة في ملف المالك.
        /// المصدر المحاسبي يبقى YER؛ التحويل هنا للعرض والطباعة فقط.
        /// </summary>
        public async Task<FinancialDocumentDetailsDto?> GetFinancialDocumentAsync(
            int ownerId,
            string documentType,
            int sourceId,
            CancellationToken cancellationToken = default)
        {
            var model = await _commissionFinanceService.GetFinancialDocumentAsync(
                ownerId,
                documentType,
                sourceId,
                cancellationToken);

            if (model == null)
            {
                return null;
            }

            var region = await GetOwnedPlaceRegionAsync(
                ownerId,
                model.PlaceId,
                cancellationToken);

            if (region == null)
            {
                return null;
            }

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            model.Currency = currency;
            ConvertDocumentDetails(model, currency, region);

            return model;
        }

        /// <summary>
        /// يبني نسخة التقرير المخصصة للطباعة/التصدير ثم يحول جميع القيم
        /// المالية فيها إلى عملة المالك قبل أن تصل إلى View أو Excel.
        /// </summary>
        public async Task<FinancialReportExportDto> BuildFinancialReportExportAsync(
            int ownerId,
            FinancialReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new FinancialReportFilterDto();

            var report = await _commissionFinanceService
                .BuildFinancialReportExportAsync(
                    ownerId,
                    filter,
                    cancellationToken);

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var region = filter.PlaceId.HasValue
                ? await GetOwnedPlaceRegionAsync(
                    ownerId,
                    filter.PlaceId.Value,
                    cancellationToken)
                : null;

            // صفحة مالية المالك تعمل دائمًا على منشأة الهيدر. إذا وصل استدعاء
            // قديم بلا PlaceId نستخدم سوق صنعاء كمرجع احتياطي فقط.
            var selectedRegion = region ?? ExchangeRateRegionResolver.SanaaMarket;

            ConvertFinancialReportExport(
                report,
                currency,
                selectedRegion);

            return report;
        }

        /// <summary>
        /// Excel يُبنى من نفس كائن التصدير المحول المستخدم في الطباعة،
        /// لذلك لا توجد معادلات أو عملة مستقلة خاصة بملف Excel.
        /// </summary>
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

        public async Task<FinanceOperationResult> SubmitTopUpAsync(
            int ownerId,
            TopUpRequestDto request,
            IFormFile receiptFile,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return FinanceOperationResult.Failed("بيانات طلب الشحن غير صحيحة.");
            }

            var region = await GetOwnedPlaceRegionAsync(
                ownerId,
                request.PlaceId,
                cancellationToken);

            if (region == null)
            {
                return FinanceOperationResult.Failed(
                    "المنشأة غير موجودة أو لا تتبع حساب المالك الحالي.");
            }

            var currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            // واجهة المالك تعرض وتستقبل القيمة بعملته المفضلة؛ قبل إنشاء
            // الحركة نحولها مرة واحدة إلى YER لأن قاعدة البيانات محاسبيًا YER.
            var requestInYer = new TopUpRequestDto
            {
                PlaceId = request.PlaceId,
                PlatformWalletId = request.PlatformWalletId,
                Amount = _currencyDisplayService.ConvertToYerForCurrency(
                    request.Amount,
                    currency,
                    region),
                ReferenceNumber = request.ReferenceNumber,
                TransferDate = request.TransferDate,
                Notes = request.Notes
            };

            var result = await _commissionFinanceService.SubmitTopUpRequestAsync(
                ownerId,
                requestInYer,
                receiptFile,
                cancellationToken);

            ConvertOperationResult(result, currency, region);
            return result;
        }

        public async Task<FinanceOperationResult> CancelPendingTopUpAsync(
            int ownerId,
            int commissionTransactionId,
            CancellationToken cancellationToken = default)
        {
            var transactionContext = await _context.CommissionTransactions
                .AsNoTracking()
                .Where(item =>
                    item.CommissionTransactionId == commissionTransactionId &&
                    item.Place.OwnerId == ownerId)
                .Select(item => new
                {
                    item.PlaceId,
                    ExchangeRateRegion = item.Place.City.ExchangeRateRegion
                })
                .FirstOrDefaultAsync(cancellationToken);

            var result = await _commissionFinanceService.CancelPendingTopUpAsync(
                ownerId,
                commissionTransactionId,
                cancellationToken);

            if (transactionContext != null)
            {
                var currency = await _currencyDisplayService
                    .GetPreferredCurrencyAsync(cancellationToken);

                ConvertOperationResult(
                    result,
                    currency,
                    NormalizeRegion(transactionContext.ExchangeRateRegion));
            }

            return result;
        }

        public Task<FinanceOperationResult> SaveReceivingWalletAsync(
            int ownerId,
            SaveOwnerWalletRequestDto request,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.SaveOwnerWalletAsync(
                ownerId,
                request,
                cancellationToken);

        public Task<FinanceOperationResult> ChangeReceivingWalletStatusAsync(
            int ownerId,
            int ownerWalletId,
            bool isActive,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.ChangeOwnerWalletStatusAsync(
                ownerId,
                ownerWalletId,
                isActive,
                cancellationToken);

        public async Task<FinanceOperationResult> MarkRefundTransferredAsync(
            int ownerId,
            MarkRefundTransferredRequestDto request,
            string proofImagePath,
            CancellationToken cancellationToken = default)
        {
            if (ownerId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد حساب المالك الحالي.");
            }

            if (request == null || request.RefundRequestId <= 0)
            {
                return FinanceOperationResult.Failed(
                    "طلب الاسترداد غير صحيح.");
            }

            var reference = request.ReferenceNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 100)
            {
                return FinanceOperationResult.Failed(
                    "أدخل رقم حوالة استرداد صحيحًا.");
            }

            if (string.IsNullOrWhiteSpace(proofImagePath))
            {
                return FinanceOperationResult.Failed(
                    "إثبات تحويل الاسترداد مطلوب.");
            }

            /*
             * نستخدم Serializable حتى يكون فحص مرجع الحوالة وحفظه
             * عملية ذرية قدر الإمكان بدون إضافة قيد أو عمود جديد للقاعدة.
             */
            await using var databaseTransaction =
                _context.Database.CurrentTransaction == null
                    ? await _context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable,
                        cancellationToken)
                    : null;

            var refund = await _context.RefundRequests
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking.FinancialRecord)
                .Include(item => item.Place)
                .FirstOrDefaultAsync(item =>
                    item.RefundRequestId == request.RefundRequestId &&
                    item.Place.OwnerId == ownerId,
                    cancellationToken);

            if (refund == null)
            {
                return FinanceOperationResult.Failed(
                    "طلب الاسترداد غير موجود أو لا يتبع منشآتك.");
            }

            if (FinancialConstantsGuard.Is(
                    refund.Status,
                    FinancialRefundStatuses.Completed))
            {
                return FinanceOperationResult.Succeeded(
                    "الاسترداد مكتمل مسبقًا.",
                    refund.RefundRequestId,
                    refund.ReferenceNumber);
            }

            if (FinancialConstantsGuard.Is(
                    refund.Status,
                    FinancialRefundStatuses.Cancelled) ||
                FinancialConstantsGuard.Is(
                    refund.Status,
                    FinancialRefundStatuses.Rejected) ||
                FinancialConstantsGuard.Is(
                    refund.Status,
                    FinancialRefundStatuses.Failed))
            {
                return FinanceOperationResult.Failed(
                    "لا يمكن تسجيل تحويل لهذا الطلب في حالته الحالية.");
            }

            if (!FinancialConstantsGuard.Is(
                    refund.Status,
                    FinancialRefundStatuses.Pending))
            {
                return FinanceOperationResult.Failed(
                    "طلب الاسترداد ليس في حالة تسمح بتسجيل تحويل جديد.");
            }

            if (refund.RefundAmount <= 0m)
            {
                return FinanceOperationResult.Failed(
                    "لا يوجد مبلغ مسترد يحتاج إلى تحويل للعميل.");
            }

            var verifiedPaidOnline = Math.Round(
                Math.Max(
                    0m,
                    refund.Booking?.FinancialRecord?.AmountPaidOnline ?? 0m),
                2);

            if (verifiedPaidOnline + 0.01m < refund.RefundAmount)
            {
                return FinanceOperationResult.Failed(
                    "لا يمكن تنفيذ الاسترداد قبل اعتماد المبلغ الإلكتروني المدفوع فعليًا.");
            }

            if (!refund.RefundWalletId.HasValue ||
                refund.RefundWalletId.Value <= 0 ||
                string.IsNullOrWhiteSpace(refund.RefundAccountNumber) ||
                string.IsNullOrWhiteSpace(refund.RefundBeneficiaryName))
            {
                return FinanceOperationResult.Failed(
                    "العميل لم يحدد حساب استلام الاسترداد بعد.");
            }

            if (refund.TransferredAt.HasValue)
            {
                if (string.Equals(
                        refund.ReferenceNumber?.Trim(),
                        reference,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return FinanceOperationResult.Succeeded(
                        "تم تسجيل هذه الحوالة مسبقًا وهي بانتظار تأكيد العميل.",
                        refund.RefundRequestId,
                        refund.ReferenceNumber);
                }

                return FinanceOperationResult.Failed(
                    "تم تسجيل حوالة استرداد لهذا الطلب مسبقًا. لا يمكن استبدالها بعد الإرسال.");
            }

            var referenceAlreadyUsed = await _context.RefundRequests
                .AsNoTracking()
                .AnyAsync(item =>
                    item.RefundRequestId != refund.RefundRequestId &&
                    item.Place.OwnerId == ownerId &&
                    item.ReferenceNumber != null &&
                    item.ReferenceNumber == reference,
                    cancellationToken);

            if (referenceAlreadyUsed)
            {
                return FinanceOperationResult.Failed(
                    "رقم حوالة الاسترداد مستخدم مسبقًا في طلب استرداد آخر.");
            }

            var now = DateTime.Now;

            refund.ReferenceNumber = reference;
            refund.ProofImage = proofImagePath.Trim();
            refund.TransferredAt = now;
            refund.Status = FinancialRefundStatuses.Processing;
            refund.ProcessedByUserId = ownerId;

            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                var note = request.Notes.Trim();
                refund.Notes = note.Length <= 1000
                    ? note
                    : note.Substring(0, 1000);
            }

            if (refund.Booking != null)
            {
                refund.Booking.RefundStatus = FinancialRefundStatuses.Processing;

                if (!string.Equals(
                        refund.Booking.PaymentStatus,
                        "RefundPending",
                        StringComparison.OrdinalIgnoreCase))
                {
                    refund.Booking.PaymentStatus = "RefundPending";
                }
            }

            var customerId = refund.CustomerId ?? refund.Booking?.CustomerId;

            if (!customerId.HasValue)
            {
                return FinanceOperationResult.Failed(
                    "تعذر تحديد العميل المرتبط بطلب الاسترداد.");
            }

            // قيمة التحويل الفعلية محفوظة بالـ YER. إشعار العميل هنا يوثق
            // المبلغ الأصلي للحوالة؛ تحويل واجهات العميل ليس مسؤولية مالية المالك.
            _notificationService.Queue(
                new CreateNotificationDto
                {
                    UserId = customerId.Value,
                    NotificationTitle = "تم تحويل مبلغ الاسترداد",
                    NotificationText =
                        $"تم تحويل استرداد الحجز رقم {refund.BookingId} " +
                        $"بقيمة {refund.RefundAmount:N0} ريال يمني. " +
                        "راجع الحجز ثم أكد استلام المبلغ.",
                    NotificationType = NotificationTypes.RefundTransferred,
                    TargetUrl = $"/Places/BookingDetails/{refund.BookingId}",
                    ReferenceType = NotificationReferenceTypes.Booking,
                    ReferenceId = refund.BookingId
                });

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                if (databaseTransaction != null)
                {
                    await databaseTransaction.CommitAsync(cancellationToken);
                }

                return FinanceOperationResult.Succeeded(
                    "تم تسجيل تحويل الاسترداد. بانتظار تأكيد العميل للاستلام.",
                    refund.RefundRequestId,
                    refund.ReferenceNumber);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "تعارض أثناء تسجيل تحويل الاسترداد {RefundRequestId} للمالك {OwnerId}.",
                    refund.RefundRequestId,
                    ownerId);

                return new FinanceOperationResult
                {
                    Success = false,
                    ConcurrencyConflict = true,
                    Message = "تغير طلب الاسترداد أثناء التنفيذ. أعد تحميل البيانات ثم حاول مرة أخرى."
                };
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "فشل تسجيل تحويل الاسترداد {RefundRequestId} للمالك {OwnerId}.",
                    refund.RefundRequestId,
                    ownerId);

                return FinanceOperationResult.Failed(
                    "تعذر تسجيل تحويل الاسترداد حاليًا.");
            }
        }

        // =================================================================
        // Currency helpers - كل ما تحت هذا السطر للعرض فقط ولا يكتب DB.
        // =================================================================

        private async Task<Dictionary<int, string>> LoadPlaceRegionsAsync(
            int ownerId,
            IEnumerable<int> placeIds,
            CancellationToken cancellationToken)
        {
            var ids = (placeIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (ownerId <= 0 || ids.Length == 0)
            {
                return new Dictionary<int, string>();
            }

            var rows = await _context.Places
                .AsNoTracking()
                .Where(place =>
                    place.OwnerId == ownerId &&
                    ids.Contains(place.PlaceId))
                .Select(place => new
                {
                    place.PlaceId,
                    ExchangeRateRegion = place.City.ExchangeRateRegion
                })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(
                item => item.PlaceId,
                item => NormalizeRegion(item.ExchangeRateRegion));
        }

        private async Task<string?> GetOwnedPlaceRegionAsync(
            int ownerId,
            int placeId,
            CancellationToken cancellationToken)
        {
            if (ownerId <= 0 || placeId <= 0)
            {
                return null;
            }

            var region = await _context.Places
                .AsNoTracking()
                .Where(place =>
                    place.PlaceId == placeId &&
                    place.OwnerId == ownerId)
                .Select(place => place.City.ExchangeRateRegion)
                .FirstOrDefaultAsync(cancellationToken);

            // FirstOrDefault قد يعيد null إذا كانت المنشأة موجودة لكن المنطقة
            // فارغة. نتحقق من ملكية المنشأة بصورة مستقلة عند الحاجة.
            if (region == null)
            {
                var exists = await _context.Places
                    .AsNoTracking()
                    .AnyAsync(place =>
                        place.PlaceId == placeId &&
                        place.OwnerId == ownerId,
                        cancellationToken);

                if (!exists)
                {
                    return null;
                }
            }

            return NormalizeRegion(region);
        }

        private static string ResolveRegion(
            IReadOnlyDictionary<int, string> regions,
            int? placeId)
        {
            if (placeId.HasValue &&
                regions.TryGetValue(placeId.Value, out var region))
            {
                return region;
            }

            return ExchangeRateRegionResolver.SanaaMarket;
        }

        private static string NormalizeRegion(string? region)
        {
            return string.Equals(
                    region?.Trim(),
                    ExchangeRateRegionResolver.AdenMarket,
                    StringComparison.OrdinalIgnoreCase)
                ? ExchangeRateRegionResolver.AdenMarket
                : ExchangeRateRegionResolver.SanaaMarket;
        }

        private decimal Money(
            decimal amountInYer,
            string currency,
            string region)
        {
            return _currencyDisplayService.ConvertAmountForCurrency(
                amountInYer,
                currency,
                region);
        }

        private decimal? FixedDiscount(
            decimal? value,
            string? discountType,
            string currency,
            string region)
        {
            if (!value.HasValue ||
                !string.Equals(
                    discountType,
                    "FixedAmount",
                    StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return Money(value.Value, currency, region);
        }

        private void ConvertPlaceOption(
            OwnerFinancialPlaceOptionDto item,
            string currency,
            string region)
        {
            item.AvailableBalance = Money(item.AvailableBalance, currency, region);
            item.HeldBalance = Money(item.HeldBalance, currency, region);
            item.DebtAmount = Money(item.DebtAmount, currency, region);
        }

        private void ConvertOverviewSummary(
            FinancialOverviewDto item,
            string currency,
            string region)
        {
            item.TotalBookingValue = Money(item.TotalBookingValue, currency, region);
            item.TotalDepositRequired = Money(item.TotalDepositRequired, currency, region);
            item.TotalPaidOnline = Money(item.TotalPaidOnline, currency, region);
            item.TotalRemainingAtProperty = Money(item.TotalRemainingAtProperty, currency, region);
            item.TotalCollectedAtProperty = Money(item.TotalCollectedAtProperty, currency, region);
            item.TotalCommissionCalculated = Money(item.TotalCommissionCalculated, currency, region);
            item.TotalCommissionHeld = Money(item.TotalCommissionHeld, currency, region);
            item.TotalCommissionCharged = Money(item.TotalCommissionCharged, currency, region);
            item.TotalCommissionReleased = Money(item.TotalCommissionReleased, currency, region);
            item.AvailableGuaranteeBalance = Money(item.AvailableGuaranteeBalance, currency, region);
            item.HeldGuaranteeBalance = Money(item.HeldGuaranteeBalance, currency, region);
            item.TotalDebt = Money(item.TotalDebt, currency, region);
            item.CurrentMonthRevenue = Money(item.CurrentMonthRevenue, currency, region);
            item.PreviousMonthRevenue = Money(item.PreviousMonthRevenue, currency, region);
        }

        private void ConvertBookingSummary(
            BookingAccountsSummaryDto item,
            string currency,
            string region)
        {
            item.TotalOriginalBookingValue = Money(item.TotalOriginalBookingValue, currency, region);
            item.TotalDiscountAmount = Money(item.TotalDiscountAmount, currency, region);
            item.TotalBookingValue = Money(item.TotalBookingValue, currency, region);
            item.TotalDepositRequired = Money(item.TotalDepositRequired, currency, region);
            item.TotalPaidOnline = Money(item.TotalPaidOnline, currency, region);
            item.TotalCollectedAtProperty = Money(item.TotalCollectedAtProperty, currency, region);
            item.TotalRemainingAtProperty = Money(item.TotalRemainingAtProperty, currency, region);
            item.TotalCommission = Money(item.TotalCommission, currency, region);
        }

        private void ConvertBookingItem(
            BookingFinancialItemDto item,
            string currency,
            string region)
        {
            item.TotalPrice = Money(item.TotalPrice, currency, region);
            item.OriginalTotalPrice = Money(item.OriginalTotalPrice, currency, region);
            item.DiscountAmount = Money(item.DiscountAmount, currency, region);
            item.DiscountValueSnapshot = FixedDiscount(
                item.DiscountValueSnapshot,
                item.DiscountTypeSnapshot,
                currency,
                region);
            item.DepositAmount = Money(item.DepositAmount, currency, region);
            item.AmountPaidOnline = Money(item.AmountPaidOnline, currency, region);
            item.AmountToPayAtProperty = Money(item.AmountToPayAtProperty, currency, region);
            item.AmountCollectedAtProperty = Money(item.AmountCollectedAtProperty, currency, region);
            item.CommissionBaseAmount = Money(item.CommissionBaseAmount, currency, region);
            item.PlatformCommission = Money(item.PlatformCommission, currency, region);
        }

        private void ConvertPlaceAccount(
            PlaceCommissionAccountDto item,
            string currency,
            string region)
        {
            item.AvailableBalance = Money(item.AvailableBalance, currency, region);
            item.HeldBalance = Money(item.HeldBalance, currency, region);
            item.DebtAmount = Money(item.DebtAmount, currency, region);
            item.DebtLimit = Money(item.DebtLimit, currency, region);
            item.LowBalanceThreshold = Money(item.LowBalanceThreshold, currency, region);
        }

        private void ConvertTransactionSummary(
            CommissionTransactionsSummaryDto item,
            string currency,
            string region)
        {
            item.CompletedTopUpsAmount = Money(item.CompletedTopUpsAmount, currency, region);
            item.PendingTopUpsAmount = Money(item.PendingTopUpsAmount, currency, region);
            item.CommissionHeldAmount = Money(item.CommissionHeldAmount, currency, region);
            item.CommissionChargedAmount = Money(item.CommissionChargedAmount, currency, region);
            item.CommissionReleasedAmount = Money(item.CommissionReleasedAmount, currency, region);
            item.DebtCreatedAmount = Money(item.DebtCreatedAmount, currency, region);
            item.DebtPaidAmount = Money(item.DebtPaidAmount, currency, region);
            item.AdjustmentsNetAmount = Money(item.AdjustmentsNetAmount, currency, region);
            item.CreditMovementsAmount = Money(item.CreditMovementsAmount, currency, region);
            item.DebitMovementsAmount = Money(item.DebitMovementsAmount, currency, region);
            item.AvailableBalanceNetChange = Money(item.AvailableBalanceNetChange, currency, region);
            item.HeldBalanceNetChange = Money(item.HeldBalanceNetChange, currency, region);
            item.DebtNetChange = Money(item.DebtNetChange, currency, region);
        }

        private void ConvertTransaction(
            CommissionTransactionItemDto item,
            string currency,
            string region)
        {
            item.Amount = Money(item.Amount, currency, region);
            item.AvailableBalanceBefore = Money(item.AvailableBalanceBefore, currency, region);
            item.AvailableBalanceAfter = Money(item.AvailableBalanceAfter, currency, region);
            item.HeldBalanceBefore = Money(item.HeldBalanceBefore, currency, region);
            item.HeldBalanceAfter = Money(item.HeldBalanceAfter, currency, region);
            item.DebtBefore = Money(item.DebtBefore, currency, region);
            item.DebtAfter = Money(item.DebtAfter, currency, region);
        }

        private void ConvertRefundSummary(
            RefundsSummaryDto item,
            string currency,
            string region)
        {
            item.TotalDepositPaid = Money(item.TotalDepositPaid, currency, region);
            item.RequestedRefundAmount = Money(item.RequestedRefundAmount, currency, region);
            item.PendingRefundAmount = Money(item.PendingRefundAmount, currency, region);
            item.VerifiedOpenRefundAmount = Money(item.VerifiedOpenRefundAmount, currency, region);
            item.CompletedRefundAmount = Money(item.CompletedRefundAmount, currency, region);
            item.CompletedCancellationFees = Money(item.CompletedCancellationFees, currency, region);
            item.CompletedOwnerShare = Money(item.CompletedOwnerShare, currency, region);
            item.CompletedPlatformCommission = Money(item.CompletedPlatformCommission, currency, region);
            item.CompletedCommissionReleased = Money(item.CompletedCommissionReleased, currency, region);
            item.CompletedRetainedAmount = Money(item.CompletedRetainedAmount, currency, region);
            item.TotalCommissionReleased = Money(item.TotalCommissionReleased, currency, region);
            item.TotalRetainedAmount = Money(item.TotalRetainedAmount, currency, region);
        }

        private void ConvertRefundItem(
            RefundRequestItemDto item,
            string currency,
            string region)
        {
            item.DepositPaid = Money(item.DepositPaid, currency, region);
            item.BookingOriginalTotalPrice = Money(item.BookingOriginalTotalPrice, currency, region);
            item.BookingDiscountAmount = Money(item.BookingDiscountAmount, currency, region);
            item.BookingTotalPrice = Money(item.BookingTotalPrice, currency, region);
            item.RequiredDepositAmount = Money(item.RequiredDepositAmount, currency, region);
            item.AmountPaidOnline = Money(item.AmountPaidOnline, currency, region);
            item.CommissionBaseAmount = Money(item.CommissionBaseAmount, currency, region);
            item.CancellationFee = Money(item.CancellationFee, currency, region);
            item.RefundAmount = Money(item.RefundAmount, currency, region);
            item.OwnerShare = Money(item.OwnerShare, currency, region);
            item.PlatformCommission = Money(item.PlatformCommission, currency, region);
            item.CommissionReleased = Money(item.CommissionReleased, currency, region);
        }

        private void ConvertReportSummary(
            FinancialReportSummaryDto item,
            string currency,
            string region)
        {
            item.TotalDocumentValue = Money(item.TotalDocumentValue, currency, region);
            item.TotalOriginalBookingValue = Money(item.TotalOriginalBookingValue, currency, region);
            item.TotalDiscountAmount = Money(item.TotalDiscountAmount, currency, region);
            item.TotalBookingValue = Money(item.TotalBookingValue, currency, region);
            item.TotalDepositRequired = Money(item.TotalDepositRequired, currency, region);
            item.TotalPaidOnline = Money(item.TotalPaidOnline, currency, region);
            item.TotalCollectedAtProperty = Money(item.TotalCollectedAtProperty, currency, region);
            item.TotalActualCollected = Money(item.TotalActualCollected, currency, region);
            item.TotalRemainingAtProperty = Money(item.TotalRemainingAtProperty, currency, region);
            item.ActualRevenue = Money(item.ActualRevenue, currency, region);
            item.TotalCommission = Money(item.TotalCommission, currency, region);
            item.TotalCommissionHeld = Money(item.TotalCommissionHeld, currency, region);
            item.TotalCommissionCharged = Money(item.TotalCommissionCharged, currency, region);
            item.TotalCommissionReleased = Money(item.TotalCommissionReleased, currency, region);
            item.TotalCommissionDebt = Money(item.TotalCommissionDebt, currency, region);
            item.TotalCommissionOutstanding = Money(item.TotalCommissionOutstanding, currency, region);
            item.RequestedRefundAmount = Money(item.RequestedRefundAmount, currency, region);
            item.PendingRefundAmount = Money(item.PendingRefundAmount, currency, region);
            item.TotalRefunded = Money(item.TotalRefunded, currency, region);
            item.TotalCancellationFees = Money(item.TotalCancellationFees, currency, region);
            item.TotalOwnerShare = Money(item.TotalOwnerShare, currency, region);
            item.TotalTopUps = Money(item.TotalTopUps, currency, region);
            item.TotalPendingTopUps = Money(item.TotalPendingTopUps, currency, region);
            item.CurrentDebt = Money(item.CurrentDebt, currency, region);
            item.NetRevenue = Money(item.NetRevenue, currency, region);
            item.NetCashFlow = Money(item.NetCashFlow, currency, region);
            item.GuaranteeNetFlow = Money(item.GuaranteeNetFlow, currency, region);
        }

        private void ConvertAnalytics(
            ComprehensiveFinancialAnalyticsDto item,
            string currency,
            string region)
        {
            item.CompletedTopUpsAmount = Money(item.CompletedTopUpsAmount, currency, region);
            item.PendingTopUpsAmount = Money(item.PendingTopUpsAmount, currency, region);
            item.CommissionHeldAmount = Money(item.CommissionHeldAmount, currency, region);
            item.CommissionChargedAmount = Money(item.CommissionChargedAmount, currency, region);
            item.CommissionReleasedAmount = Money(item.CommissionReleasedAmount, currency, region);
            item.CommissionDebtCreatedAmount = Money(item.CommissionDebtCreatedAmount, currency, region);
            item.CurrentAvailableGuaranteeBalance = Money(item.CurrentAvailableGuaranteeBalance, currency, region);
            item.CurrentHeldGuaranteeBalance = Money(item.CurrentHeldGuaranteeBalance, currency, region);
            item.CurrentDebt = Money(item.CurrentDebt, currency, region);
            item.CompletedRefundAmount = Money(item.CompletedRefundAmount, currency, region);
            item.PendingRefundAmount = Money(item.PendingRefundAmount, currency, region);
            item.OperationalNetCashFlow = Money(item.OperationalNetCashFlow, currency, region);
            item.GuaranteeNetFlow = Money(item.GuaranteeNetFlow, currency, region);
        }

        private void ConvertDocumentItem(
            FinancialDocumentItemDto item,
            string currency,
            string region)
        {
            item.PrimaryAmount = Money(item.PrimaryAmount, currency, region);
            item.TotalPrice = Money(item.TotalPrice, currency, region);
            item.OriginalTotalPrice = Money(item.OriginalTotalPrice, currency, region);
            item.DiscountAmount = Money(item.DiscountAmount, currency, region);
            item.DiscountValueSnapshot = FixedDiscount(
                item.DiscountValueSnapshot,
                item.DiscountTypeSnapshot,
                currency,
                region);
            item.DepositAmount = Money(item.DepositAmount, currency, region);
            item.AmountPaidOnline = Money(item.AmountPaidOnline, currency, region);
            item.AmountCollectedAtProperty = Money(item.AmountCollectedAtProperty, currency, region);
            item.RemainingAmount = Money(item.RemainingAmount, currency, region);
            item.PlatformCommission = Money(item.PlatformCommission, currency, region);
            item.RefundedAmount = Money(item.RefundedAmount, currency, region);
            item.CancellationFee = Money(item.CancellationFee, currency, region);
            item.OwnerShare = Money(item.OwnerShare, currency, region);
            item.NetOwnerAmount = Money(item.NetOwnerAmount, currency, region);
        }

        private void ConvertReportRow(
            FinancialReportRowDto item,
            string currency,
            string region)
        {
            item.PrimaryAmount = Money(item.PrimaryAmount, currency, region);
            item.TotalPrice = Money(item.TotalPrice, currency, region);
            item.OriginalTotalPrice = Money(item.OriginalTotalPrice, currency, region);
            item.DiscountAmount = Money(item.DiscountAmount, currency, region);
            item.DiscountValueSnapshot = FixedDiscount(
                item.DiscountValueSnapshot,
                item.DiscountTypeSnapshot,
                currency,
                region);
            item.DepositAmount = Money(item.DepositAmount, currency, region);
            item.AmountPaidOnline = Money(item.AmountPaidOnline, currency, region);
            item.AmountCollectedAtProperty = Money(item.AmountCollectedAtProperty, currency, region);
            item.ActualPaidOnline = Money(item.ActualPaidOnline, currency, region);
            item.ActualCollectedAtProperty = Money(item.ActualCollectedAtProperty, currency, region);
            item.ActualCollectedAmount = Money(item.ActualCollectedAmount, currency, region);
            item.RemainingAmount = Money(item.RemainingAmount, currency, region);
            item.ActualRevenue = Money(item.ActualRevenue, currency, region);
            item.PlatformCommission = Money(item.PlatformCommission, currency, region);
            item.CommissionHeld = Money(item.CommissionHeld, currency, region);
            item.CommissionCharged = Money(item.CommissionCharged, currency, region);
            item.CommissionReleased = Money(item.CommissionReleased, currency, region);
            item.CommissionDebt = Money(item.CommissionDebt, currency, region);
            item.CommissionOutstanding = Money(item.CommissionOutstanding, currency, region);
            item.RequestedRefundAmount = Money(item.RequestedRefundAmount, currency, region);
            item.RefundAmount = Money(item.RefundAmount, currency, region);
            item.CancellationFee = Money(item.CancellationFee, currency, region);
            item.OwnerShare = Money(item.OwnerShare, currency, region);
            item.TopUpAmount = Money(item.TopUpAmount, currency, region);
            item.NetOwnerAmount = Money(item.NetOwnerAmount, currency, region);
            item.AvailableBalanceBefore = Money(item.AvailableBalanceBefore, currency, region);
            item.AvailableBalanceAfter = Money(item.AvailableBalanceAfter, currency, region);
            item.HeldBalanceBefore = Money(item.HeldBalanceBefore, currency, region);
            item.HeldBalanceAfter = Money(item.HeldBalanceAfter, currency, region);
            item.DebtBefore = Money(item.DebtBefore, currency, region);
            item.DebtAfter = Money(item.DebtAfter, currency, region);
        }

        private void ConvertDocumentDetails(
            FinancialDocumentDetailsDto item,
            string currency,
            string region)
        {
            item.OriginalTotalPrice = Money(item.OriginalTotalPrice, currency, region);
            item.DiscountAmount = Money(item.DiscountAmount, currency, region);
            item.FinalTotalPrice = Money(item.FinalTotalPrice, currency, region);
            item.DiscountValueSnapshot = FixedDiscount(
                item.DiscountValueSnapshot,
                item.DiscountTypeSnapshot,
                currency,
                region);
            item.GrandTotal = Money(item.GrandTotal, currency, region);

            foreach (var line in item.Lines ?? new List<FinancialDocumentLineDto>())
            {
                line.UnitPrice = Money(line.UnitPrice, currency, region);
                line.Total = Money(line.Total, currency, region);
            }

            foreach (var amountLine in item.AmountLines ?? new List<FinancialDocumentAmountLineDto>())
            {
                amountLine.Amount = Money(amountLine.Amount, currency, region);
            }
        }

        private void ConvertFinancialReportExport(
            FinancialReportExportDto report,
            string currency,
            string region)
        {
            var previousCurrency = report.Currency;
            report.Currency = currency;

            ConvertReportSummary(report.Summary, currency, region);
            ConvertAnalytics(report.Analytics, currency, region);
            report.Reconciliation.MaximumCollectionDifference = Money(
                report.Reconciliation.MaximumCollectionDifference,
                currency,
                region);

            var convertedRows = new HashSet<FinancialReportExportRowDto>();

            foreach (var section in report.Sections ??
                     new List<FinancialReportExportSectionDto>())
            {
                if (!string.IsNullOrWhiteSpace(previousCurrency) &&
                    !string.Equals(
                        previousCurrency,
                        currency,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(section.Description))
                {
                    section.Description = section.Description.Replace(
                        previousCurrency,
                        currency,
                        StringComparison.OrdinalIgnoreCase);
                }

                ConvertExportRows(
                    section.Columns,
                    section.Rows,
                    currency,
                    region,
                    convertedRows);
            }

            // توافق مع أي تقرير قديم لا يحتوي Sections. في التقارير الجديدة
            // Rows هي نفس مرجع صفوف القسم الرئيسي، وHashSet يمنع التحويل مرتين.
            ConvertExportRows(
                report.Columns,
                report.Rows,
                currency,
                region,
                convertedRows);

            foreach (var item in report.PlaceBreakdown ??
                     new List<FinancialReportBreakdownItemDto>())
            {
                item.Amount = Money(item.Amount, currency, region);
            }

            foreach (var item in report.StatusBreakdown ??
                     new List<FinancialReportBreakdownItemDto>())
            {
                item.Amount = Money(item.Amount, currency, region);
            }

            report.Metrics = BuildConvertedExportMetrics(
                report.Summary,
                report.ReportType,
                currency);

            report.Insights = BuildConvertedExportInsights(
                report.Summary,
                report.ReportType,
                currency,
                report.TotalAvailableRows);
        }

        private void ConvertExportRows(
            IReadOnlyList<FinancialReportExportColumnDto>? columns,
            IEnumerable<FinancialReportExportRowDto>? rows,
            string currency,
            string region,
            ISet<FinancialReportExportRowDto> convertedRows)
        {
            if (columns == null || rows == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                if (row == null || !convertedRows.Add(row))
                {
                    continue;
                }

                var typedCells = row.TypedCells ??
                    new List<FinancialReportExportCellDto>();

                for (var index = 0;
                     index < columns.Count && index < typedCells.Count;
                     index++)
                {
                    var column = columns[index];
                    var cell = typedCells[index];

                    var isMoney = string.Equals(
                            column.DataType,
                            "Money",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            cell.DataType,
                            "Money",
                            StringComparison.OrdinalIgnoreCase);

                    if (!isMoney || !cell.DecimalValue.HasValue)
                    {
                        continue;
                    }

                    var converted = Money(
                        cell.DecimalValue.Value,
                        currency,
                        region);

                    cell.DecimalValue = converted;
                    cell.DataType = "Money";
                    cell.Text = converted.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture);

                    if (row.Cells != null && index < row.Cells.Count)
                    {
                        row.Cells[index] = cell.Text;
                    }
                }
            }
        }

        private static List<FinancialReportMetricDto> BuildConvertedExportMetrics(
            FinancialReportSummaryDto summary,
            string reportType,
            string currency)
        {
            var metrics = new List<FinancialReportMetricDto>
            {
                new()
                {
                    Label = "السجلات المطابقة",
                    Value = summary.RecordsCount.ToString(
                        "N0",
                        CultureInfo.GetCultureInfo("en-US")),
                    Hint = "كل النتائج المطابقة للفلاتر",
                    Tone = "Info"
                }
            };

            void Add(string label, decimal value, string tone, string? hint = null)
            {
                metrics.Add(new FinancialReportMetricDto
                {
                    Label = label,
                    Value = FormatExportMoney(value, currency),
                    Hint = hint,
                    Tone = tone
                });
            }

            if (string.Equals(reportType, OwnerFinancialReportTypes.Documents, StringComparison.OrdinalIgnoreCase))
            {
                Add("قيمة المستندات", summary.TotalDocumentValue, "Primary");
            }
            else if (string.Equals(reportType, OwnerFinancialReportTypes.Bookings, StringComparison.OrdinalIgnoreCase))
            {
                Add("قيمة الحجوزات", summary.TotalBookingValue, "Primary");
                Add("إجمالي المحصّل", summary.TotalPaid, "Info");
                Add("الإيراد الفعلي", summary.ActualRevenue, "Success");
            }
            else if (string.Equals(reportType, OwnerFinancialReportTypes.Payments, StringComparison.OrdinalIgnoreCase))
            {
                Add("مدفوع إلكترونيًا", summary.TotalPaidOnline, "Primary");
                Add("محصّل في المنشأة", summary.TotalCollectedAtProperty, "Success");
                Add("المتبقي", summary.TotalRemainingAtProperty, "Warning");
            }
            else if (string.Equals(reportType, OwnerFinancialReportTypes.Commissions, StringComparison.OrdinalIgnoreCase))
            {
                Add("العمولة المحسوبة", summary.TotalCommission, "Primary");
                Add("المخصومة", summary.TotalCommissionCharged, "Success");
                Add("الدين", summary.TotalCommissionDebt, "Warning");
            }
            else if (string.Equals(reportType, OwnerFinancialReportTypes.Refunds, StringComparison.OrdinalIgnoreCase))
            {
                Add("الاسترداد المعلق", summary.PendingRefundAmount, "Warning");
                Add("المسترد فعليًا", summary.TotalRefunded, "Success");
                Add("حصة المنشأة", summary.TotalOwnerShare, "Primary");
            }
            else if (string.Equals(reportType, OwnerFinancialReportTypes.Transactions, StringComparison.OrdinalIgnoreCase))
            {
                Add("شحن مكتمل", summary.TotalTopUps, "Success");
                Add("شحن معلق", summary.TotalPendingTopUps, "Warning");
                Add("الدين الحالي", summary.CurrentDebt, "Primary");
            }
            else
            {
                Add("إجمالي المحصّل", summary.TotalActualCollected, "Info");
                Add("صافي المالك", summary.NetRevenue, "Success");
                Add("الدين الحالي", summary.CurrentDebt, "Warning");
            }

            return metrics;
        }

        private static List<FinancialReportInsightDto> BuildConvertedExportInsights(
            FinancialReportSummaryDto summary,
            string reportType,
            string currency,
            int totalRows)
        {
            if (totalRows <= 0)
            {
                return new List<FinancialReportInsightDto>
                {
                    new()
                    {
                        Title = "لا توجد نتائج",
                        Value = "0",
                        Description = "لم يتم العثور على سجلات مطابقة للفلاتر الحالية.",
                        Icon = "bi-inbox",
                        Tone = "Neutral"
                    }
                };
            }

            if (string.Equals(reportType, OwnerFinancialReportTypes.Refunds, StringComparison.OrdinalIgnoreCase))
            {
                return new List<FinancialReportInsightDto>
                {
                    new()
                    {
                        Title = "المسترد المكتمل فقط",
                        Value = FormatExportMoney(summary.TotalRefunded, currency),
                        Description = "المبلغ يعكس الاستردادات المنفذة ماليًا وفق دورة الاسترداد.",
                        Icon = "bi-shield-check",
                        Tone = "Success"
                    }
                };
            }

            if (string.Equals(reportType, OwnerFinancialReportTypes.Comprehensive, StringComparison.OrdinalIgnoreCase))
            {
                return new List<FinancialReportInsightDto>
                {
                    new()
                    {
                        Title = "صافي التدفق التشغيلي",
                        Value = FormatExportMoney(summary.NetCashFlow, currency),
                        Description = "التحصيل الفعلي بعد أثر الاستردادات والعمولة المثبتة.",
                        Icon = "bi-cash-coin",
                        Tone = "Success"
                    }
                };
            }

            return new List<FinancialReportInsightDto>();
        }

        private static string FormatExportMoney(
            decimal value,
            string currency)
        {
            var format = decimal.Truncate(value) == value
                ? "N0"
                : "N2";

            return string.Concat(
                value.ToString(
                    format,
                    CultureInfo.GetCultureInfo("en-US")),
                " ",
                currency);
        }

        private void ConvertBookingAccess(
            BookingFinancialAccessResult item,
            string currency,
            string region)
        {
            item.RequiredGuaranteeBalance = Money(item.RequiredGuaranteeBalance, currency, region);
            item.AvailableBalance = Money(item.AvailableBalance, currency, region);
            item.MissingGuaranteeBalance = Money(item.MissingGuaranteeBalance, currency, region);
            item.RequiredCommissionAmount = Money(item.RequiredCommissionAmount, currency, region);
            item.MissingCommissionCoverage = Money(item.MissingCommissionCoverage, currency, region);
        }

        private void ConvertOperationResult(
            FinanceOperationResult item,
            string currency,
            string region)
        {
            if (item.AvailableBalance.HasValue)
            {
                item.AvailableBalance = Money(item.AvailableBalance.Value, currency, region);
            }

            if (item.HeldBalance.HasValue)
            {
                item.HeldBalance = Money(item.HeldBalance.Value, currency, region);
            }

            if (item.DebtAmount.HasValue)
            {
                item.DebtAmount = Money(item.DebtAmount.Value, currency, region);
            }
        }
    }
}
