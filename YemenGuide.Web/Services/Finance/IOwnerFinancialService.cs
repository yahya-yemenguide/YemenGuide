using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YemenGuide.Core.DTOs.Owner;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// واجهة طبقة مالية المالك.
    /// هذه الطبقة مسؤولة عن قراءة وتجميع بيانات التبويبات الستة
    /// وعمليات المالك البسيطة، بينما يبقى تغيير الأرصدة الفعلي داخل
    /// ICommissionFinanceService فقط.
    /// </summary>
    public interface IOwnerFinancialService
    {
        Task<int?> ResolveActivePlaceIdAsync(
            int ownerId,
            int? requestedPlaceId,
            int? cookiePlaceId,
            CancellationToken cancellationToken = default);

        Task<OwnerFinancialPageDto> GetShellAsync(
            int ownerId,
            int? selectedPlaceId,
            CancellationToken cancellationToken = default);

        Task<FinancialOverviewTabDto> GetOverviewAsync(
            int ownerId,
            int? placeId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);

        Task<BookingAccountsTabDto> GetBookingAccountsAsync(
            int ownerId,
            BookingFinancialFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<OwnerCommissionBalanceBackendDto> GetCommissionBalanceAsync(
            int ownerId,
            int? placeId,
            CommissionTransactionFilterDto? transactionFilter = null,
            CancellationToken cancellationToken = default);

        Task<RefundsTabDto> GetRefundsAsync(
            int ownerId,
            RefundFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<OwnerWalletsTabDto> GetReceivingWalletsAsync(
            int ownerId,
            CancellationToken cancellationToken = default);

        Task<InvoicesReportsTabDto> GetReportsAsync(
            int ownerId,
            FinancialReportFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> SubmitTopUpAsync(
            int ownerId,
            TopUpRequestDto request,
            IFormFile receiptFile,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> CancelPendingTopUpAsync(
            int ownerId,
            int commissionTransactionId,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> SaveReceivingWalletAsync(
            int ownerId,
            SaveOwnerWalletRequestDto request,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> ChangeReceivingWalletStatusAsync(
            int ownerId,
            int ownerWalletId,
            bool isActive,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> MarkRefundTransferredAsync(
            int ownerId,
            MarkRefundTransferredRequestDto request,
            string proofImagePath,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// نموذج Backend موحد للتبويب الثالث الجديد «رصيد العمولات».
    /// يجمع الحساب والشحن والحركات من دون دمج المسؤوليات داخل محرك المال.
    /// </summary>
    public sealed class OwnerCommissionBalanceBackendDto
    {
        public CommissionGuaranteeTabDto Account { get; set; } = new();

        public TopUpBalanceTabDto TopUp { get; set; } = new();

        public TransactionsTabDto Transactions { get; set; } = new();

        /// <summary>
        /// حالة التجربة المجانية وجاهزية المنشأة لاستقبال حجوزات جديدة.
        /// القرار نفسه صادر من ICommissionFinanceService حتى تبقى الواجهة
        /// متطابقة مع بوابة الحجز الفعلية ولا تنشئ منطقًا ماليًا موازيًا.
        /// </summary>
        public BookingFinancialAccessResult BookingAccess { get; set; } = new();
    }
}
