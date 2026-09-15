using System;
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
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// طبقة مالية المالك الخفيفة.
    /// لا تغيّر AvailableBalance/HeldBalance/DebtAmount مباشرة.
    /// كل تغيير على حساب العمولة يمر عبر ICommissionFinanceService.
    /// </summary>
    public sealed class OwnerFinancialService : IOwnerFinancialService
    {
        private readonly AppDbContext _context;
        private readonly ICommissionFinanceService _commissionFinanceService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<OwnerFinancialService> _logger;

        public OwnerFinancialService(
            AppDbContext context,
            ICommissionFinanceService commissionFinanceService,
            INotificationService notificationService,
            ILogger<OwnerFinancialService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commissionFinanceService = commissionFinanceService ??
                throw new ArgumentNullException(nameof(commissionFinanceService));
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
                .Where(place =>
                    place.OwnerId == ownerId &&
                    place.Category != null &&
                    place.Category.BookingSystemId >= 1 &&
                    place.Category.BookingSystemId <= 4)
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

        public Task<OwnerFinancialPageDto> GetShellAsync(
            int ownerId,
            int? selectedPlaceId,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.GetFinancialPageAsync(
                ownerId,
                selectedPlaceId,
                cancellationToken);

        public Task<FinancialOverviewTabDto> GetOverviewAsync(
            int ownerId,
            int? placeId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.GetOverviewAsync(
                ownerId,
                placeId,
                fromDate,
                toDate,
                cancellationToken);

        public Task<BookingAccountsTabDto> GetBookingAccountsAsync(
            int ownerId,
            BookingFinancialFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new BookingFinancialFilterDto();

            return _commissionFinanceService.GetBookingAccountsAsync(
                ownerId,
                filter,
                cancellationToken);
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
            // نستدعيه أولًا حتى تكون حالة الحساب/الضمان المعروضة محدثة
            // ومتطابقة مع ما سيطبقه Backend عند محاولة الحجز.
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

            // التبويب الثالث يتبع المنشأة النشطة من الهيدر بالكامل.
            // خدمة الشحن الداخلية تعيد جميع الحسابات لأغراض أخرى، لذلك نقيّد
            // حسابات الشحن هنا بنفس المنشأة المختارة قبل إرسالها للواجهة.
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

            return new OwnerCommissionBalanceBackendDto
            {
                Account = account,
                TopUp = topUp,
                Transactions = transactions,
                BookingAccess = bookingAccess
            };
        }

        public Task<RefundsTabDto> GetRefundsAsync(
            int ownerId,
            RefundFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new RefundFilterDto();

            return _commissionFinanceService.GetRefundsAsync(
                ownerId,
                filter,
                cancellationToken);
        }

        public Task<OwnerWalletsTabDto> GetReceivingWalletsAsync(
            int ownerId,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.GetOwnerWalletsAsync(
                ownerId,
                cancellationToken);

        public Task<InvoicesReportsTabDto> GetReportsAsync(
            int ownerId,
            FinancialReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new FinancialReportFilterDto();

            return _commissionFinanceService.GetInvoicesReportsAsync(
                ownerId,
                filter,
                cancellationToken);
        }

        public Task<FinanceOperationResult> SubmitTopUpAsync(
            int ownerId,
            TopUpRequestDto request,
            IFormFile receiptFile,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.SubmitTopUpRequestAsync(
                ownerId,
                request,
                receiptFile,
                cancellationToken);

        public Task<FinanceOperationResult> CancelPendingTopUpAsync(
            int ownerId,
            int commissionTransactionId,
            CancellationToken cancellationToken = default) =>
            _commissionFinanceService.CancelPendingTopUpAsync(
                ownerId,
                commissionTransactionId,
                cancellationToken);

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
                    .ThenInclude(booking =>
                        booking.FinancialRecord)
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

            var financialRecord =
                refund.Booking?.FinancialRecord;

            var verifiedPaidTotal =
                Math.Round(
                    Math.Max(0m, financialRecord?.AmountPaidOnline ?? 0m) +
                    Math.Max(0m, financialRecord?.AmountCollectedAtProperty ?? 0m),
                    2);

            if (verifiedPaidTotal + 0.01m < refund.RefundAmount)
            {
                return FinanceOperationResult.Failed(
                    "مبلغ الاسترداد لا يمكن أن يتجاوز إجمالي ما تم تحصيله فعليًا من العميل.");
            }

            var customerId =
                refund.CustomerId ??
                refund.Booking?.CustomerId;

            var isManualGuestRefund =
                string.Equals(
                    refund.Booking?.BookingSource?.Trim(),
                    "OwnerManual",
                    StringComparison.OrdinalIgnoreCase) &&
                !customerId.HasValue;

            // الضيف في الحجز اليدوي قد لا يملك حسابًا داخل المنصة؛
            // في هذه الحالة يوثق المالك التحويل بالمرجع والإثبات مباشرة.
            if (!isManualGuestRefund &&
                (!refund.RefundWalletId.HasValue ||
                 refund.RefundWalletId.Value <= 0 ||
                 string.IsNullOrWhiteSpace(refund.RefundAccountNumber) ||
                 string.IsNullOrWhiteSpace(refund.RefundBeneficiaryName)))
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

            /*
             * لا نسمح باستخدام نفس مرجع حوالة الاسترداد لطلبين مختلفين
             * لدى المالك نفسه. الطلب الحالي مستثنى حتى تبقى إعادة المحاولة
             * لنفس العملية Idempotent ولا تُسجّل تحويلًا ثانيًا.
             */
            var referenceAlreadyUsed =
                await _context.RefundRequests
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
            refund.Status = isManualGuestRefund
                ? FinancialRefundStatuses.Completed
                : FinancialRefundStatuses.Processing;
            refund.ProcessedAt = isManualGuestRefund
                ? now
                : refund.ProcessedAt;
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
                refund.Booking.RefundStatus = isManualGuestRefund
                    ? FinancialRefundStatuses.Completed
                    : FinancialRefundStatuses.Processing;

                refund.Booking.PaymentStatus = isManualGuestRefund
                    ? "Refunded"
                    : "RefundPending";
            }

            if (customerId.HasValue)
            {
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
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                if (databaseTransaction != null)
                {
                    await databaseTransaction.CommitAsync(
                        cancellationToken);
                }

                return FinanceOperationResult.Succeeded(
                    isManualGuestRefund
                        ? "تم تسجيل الاسترداد للحجز اليدوي وإكماله بنجاح."
                        : "تم تسجيل تحويل الاسترداد. بانتظار تأكيد العميل للاستلام.",
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
    }
}
