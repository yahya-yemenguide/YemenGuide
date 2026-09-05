using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YemenGuide.Core.DTOs.Owner;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// الواجهة الأساسية لجميع عمليات مالية المالك.
    ///
    /// الكنترولر لا يتعامل مباشرة مع الجداول المالية،
    /// بل يستدعي هذه الخدمة لقراءة البيانات وتنفيذ الحركات.
    /// </summary>
    public interface ICommissionFinanceService
    {
        /* ================================================================
           الصفحة الرئيسية للمالية
           ================================================================ */

        Task<OwnerFinancialPageDto>
            GetFinancialPageAsync(
                int ownerId,
                int? selectedPlaceId = null,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الأول: النظرة العامة
           ================================================================ */

        Task<FinancialOverviewTabDto>
            GetOverviewAsync(
                int ownerId,
                int? placeId = null,
                DateTime? fromDate = null,
                DateTime? toDate = null,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الثاني: الحسابات المالية للحجوزات
           ================================================================ */

        Task<BookingAccountsTabDto>
            GetBookingAccountsAsync(
                int ownerId,
                BookingFinancialFilterDto filter,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الثالث: حساب ضمان العمولة
           ================================================================ */

        Task<CommissionGuaranteeTabDto>
            GetCommissionGuaranteeAsync(
                int ownerId,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الرابع: شحن رصيد الضمان
           ================================================================ */

        Task<TopUpBalanceTabDto>
            GetTopUpBalanceAsync(
                int ownerId,
                int? selectedPlaceId = null,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            SubmitTopUpRequestAsync(
                int ownerId,
                TopUpRequestDto request,
                IFormFile receiptFile,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            CancelPendingTopUpAsync(
                int ownerId,
                int commissionTransactionId,
                CancellationToken cancellationToken = default);

        /* ================================================================
           مراجعة طلبات الشحن بواسطة الإدارة
           ================================================================ */

        Task<FinanceOperationResult>
            ApproveTopUpAsync(
                int commissionTransactionId,
                int adminUserId,
                string? reviewNote = null,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            RejectTopUpAsync(
                int commissionTransactionId,
                int adminUserId,
                string rejectionReason,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الخامس: كشف الحساب والحركات
           ================================================================ */

        Task<TransactionsTabDto>
            GetTransactionsAsync(
                int ownerId,
                CommissionTransactionFilterDto filter,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب السادس: الاستردادات والإلغاءات
           ================================================================ */

        Task<RefundsTabDto>
            GetRefundsAsync(
                int ownerId,
                RefundFilterDto filter,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب السابع: حسابات استقبال دفعات العملاء
           ================================================================ */

        Task<OwnerWalletsTabDto>
            GetOwnerWalletsAsync(
                int ownerId,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            SaveOwnerWalletAsync(
                int ownerId,
                SaveOwnerWalletRequestDto request,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            ChangeOwnerWalletStatusAsync(
                int ownerId,
                int ownerWalletId,
                bool isActive,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الثامن: الفواتير والتقارير
           ================================================================ */

        Task<InvoicesReportsTabDto>
            GetInvoicesReportsAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken = default);

        Task<FinancialDocumentDetailsDto?>
            GetFinancialDocumentAsync(
                int ownerId,
                string documentType,
                int sourceId,
                CancellationToken cancellationToken = default);

        Task<FinancialReportExportDto>
            BuildFinancialReportExportAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken = default);

        Task<FinancialExcelFileDto>
            BuildFinancialExcelAsync(
                int ownerId,
                FinancialReportFilterDto filter,
                CancellationToken cancellationToken = default);

        /* ================================================================
           التجربة المجانية وضمان تفعيل الحجوزات
           ================================================================ */

        /// <summary>
        /// يبدأ تجربة المالك مرة واحدة فقط عندما تصبح أول منشأة قابلة للحجز
        /// معتمدة وجاهزة فعليًا لاستقبال الحجوزات.
        /// </summary>
        Task<FinanceOperationResult>
            TryStartOwnerFreeTrialForPlaceAsync(
                int placeId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// قرار مركزي يُستخدم قبل إنشاء أي حجز جديد.
        /// أثناء التجربة يسمح بالحجز دون اشتراط ضمان، وبعد انتهائها يطبق
        /// RequiredCommissionGuaranteeBalance وحالة حساب العمولة الحالية.
        /// </summary>
        Task<BookingFinancialAccessResult>
            GetBookingFinancialAccessAsync(
                int placeId,
                CancellationToken cancellationToken = default);

        /* ================================================================
           العمليات الداخلية لدورة عمولة الحجز
           ================================================================ */

        Task<FinanceOperationResult>
            EnsurePlaceCommissionAccountAsync(
                int placeId,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            HoldBookingCommissionAsync(
                int bookingId,
                int performedByUserId,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            ChargeBookingCommissionAsync(
                int bookingId,
                int performedByUserId,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            ReleaseBookingCommissionAsync(
                int bookingId,
                int performedByUserId,
                string? reason = null,
                CancellationToken cancellationToken = default);

        Task<FinanceOperationResult>
            RecalculateBookingCommissionAsync(
                int bookingId,
                decimal commissionBaseAmount,
                int performedByUserId,
                string? reason = null,
                CancellationToken cancellationToken = default);

        /* ================================================================
           عمليات الحساب الإدارية والمجدولة
           ================================================================ */

        Task<FinanceOperationResult>
            RefreshCommissionAccountStatusAsync(
                int placeId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// تعديل إداري موثق على حساب العمولة.
        /// amount موجب = إضافة، amount سالب = خصم.
        /// لا يُسمح بأي تعديل دون سبب ومعرّف أدمن صحيح.
        /// </summary>
        Task<FinanceOperationResult>
            ApplyManualAdjustmentAsync(
                int placeId,
                decimal amount,
                int adminUserId,
                string reason,
                CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// قرار السماح المالي بإنشاء حجز جديد لمنشأة.
    /// لا يغير حالة المنشأة المرئية ولا يمنع المالك من دخول لوحة التحكم؛
    /// وظيفته فقط إرجاع قرار واضح لنقطة إنشاء الحجز.
    /// </summary>
    public sealed class BookingFinancialAccessResult
    {
        public bool CanAcceptNewBookings { get; set; }

        public bool IsBookingCategory { get; set; }

        public bool IsFreeTrialActive { get; set; }

        public DateTime? FreeTrialStartedAt { get; set; }

        public DateTime? FreeTrialEndsAt { get; set; }

        public int RemainingTrialDays { get; set; }

        public decimal RequiredGuaranteeBalance { get; set; }

        public decimal AvailableBalance { get; set; }

        public decimal MissingGuaranteeBalance { get; set; }

        public string AccountStatus { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// نتيجة موحدة لجميع عمليات الحفظ والتعديل المالية.
    /// </summary>
    public class FinanceOperationResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } =
            string.Empty;

        public int? EntityId { get; set; }

        public string? ReferenceNumber { get; set; }

        public decimal? AvailableBalance { get; set; }

        public decimal? HeldBalance { get; set; }

        public decimal? DebtAmount { get; set; }

        public bool ConcurrencyConflict { get; set; }

        public static FinanceOperationResult Succeeded(
            string message,
            int? entityId = null,
            string? referenceNumber = null)
        {
            return new FinanceOperationResult
            {
                Success = true,
                Message = message,
                EntityId = entityId,
                ReferenceNumber = referenceNumber
            };
        }

        public static FinanceOperationResult Failed(
            string message)
        {
            return new FinanceOperationResult
            {
                Success = false,
                Message = message
            };
        }
    }
}