using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Admin;

namespace YemenGuide.Web.Services.Finance
{
    /// <summary>
    /// خدمة موحدة للإدارة المالية الجديدة ذات الخمسة تبويبات.
    ///
    /// هذه الخدمة مسؤولة عن الاستعلامات والتجميع والفلاتر وسياسات الإدارة،
    /// بينما تبقى دورة العمولة الفعلية وتغيير الأرصدة داخل
    /// ICommissionFinanceService حتى لا يتكرر المنطق المحاسبي.
    /// </summary>
    public interface IAdminFinancialService
    {
        /* ================================================================
           الغلاف والنظرة العامة
           ================================================================ */

        Task<AdminFinancialShellDto> GetShellAsync(
            string? selectedTab = null,
            CancellationToken cancellationToken = default);

        Task<AdminFinancialOverviewPageDto> GetOverviewAsync(
            AdminFinancialOverviewFilterDto filter,
            CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الثاني: العمولات والضمان
           ================================================================ */

        Task<AdminCommissionGuaranteePageDto> GetCommissionGuaranteeAsync(
            AdminCommissionGuaranteeFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminCommissionGuaranteeDetailsDto?> GetCommissionGuaranteeDetailsAsync(
            int placeCommissionAccountId,
            CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الثالث: طلبات شحن رصيد الضمان
           ================================================================ */

        Task<AdminCommissionTopUpPageDto> GetTopUpsAsync(
            AdminCommissionTopUpFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminCommissionTopUpDetailsDto?> GetTopUpDetailsAsync(
            int commissionTransactionId,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> ApproveTopUpAsync(
            AdminApproveTopUpRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> RejectTopUpAsync(
            AdminRejectTopUpRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        /* ================================================================
           مكونات داخلية للتبويب الثاني: حسابات ضمان العمولات
           ================================================================ */

        Task<AdminCommissionAccountPageDto> GetCommissionAccountsAsync(
            AdminCommissionAccountFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminCommissionAccountDetailsDto?> GetCommissionAccountDetailsAsync(
            int placeCommissionAccountId,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> UpdateCommissionAccountPolicyAsync(
            AdminCommissionAccountPolicyUpdateDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> ChangeCommissionAccountStatusAsync(
            AdminCommissionAccountStatusUpdateDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        /* ================================================================
           مكونات داخلية للتبويب الثاني: عمولات الحجوزات
           ================================================================ */

        Task<AdminBookingCommissionPageDto> GetBookingCommissionsAsync(
            AdminBookingCommissionFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminBookingCommissionDetailsDto?> GetBookingCommissionDetailsAsync(
            int bookingId,
            CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الرابع: كشف الحركات المالية
           ================================================================ */

        Task<AdminFinancialTransactionPageDto> GetTransactionsAsync(
            AdminFinancialTransactionFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<AdminFinancialTransactionDetailsDto?> GetTransactionDetailsAsync(
            int commissionTransactionId,
            CancellationToken cancellationToken = default);

        /* ================================================================
           إدارة المالية (Drawer خارج التبويبات)
           ================================================================ */

        Task<AdminFinancialManagementPageDto> GetFinancialManagementAsync(
            AdminPlatformWalletFilterDto? walletFilter = null,
            CancellationToken cancellationToken = default);

        /* ================================================================
           مكوّن إدارة المالية: حسابات المنصة
           ================================================================ */

        Task<AdminPlatformWalletPageDto> GetPlatformWalletsAsync(
            AdminPlatformWalletFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> SavePlatformWalletAsync(
            AdminPlatformWalletEditDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> ChangePlatformWalletStatusAsync(
            AdminPlatformWalletStatusRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> SetPrimaryPlatformWalletAsync(
            AdminSetPrimaryPlatformWalletRequestDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        /* ================================================================
           مكوّن إدارة المالية: الإعدادات والسياسات
           ================================================================ */

        Task<AdminFinancialSettingsPageDto> GetFinancialSettingsAsync(
            CancellationToken cancellationToken = default);

        Task<FinanceOperationResult> SaveFinancialSettingsAsync(
            AdminFinancialSettingsUpdateDto request,
            int adminUserId,
            CancellationToken cancellationToken = default);

        /* ================================================================
           التبويب الخامس: التقارير والمستندات
           ================================================================ */

        Task<AdminFinancialReportPageDto> GetFinancialReportAsync(
            AdminFinancialReportFilterDto filter,
            bool includeAllRows = false,
            string? generatedByName = null,
            CancellationToken cancellationToken = default);

        Task<AdminFinancialReportExportDto> BuildFinancialReportExportAsync(
            AdminFinancialReportFilterDto filter,
            string? generatedByName = null,
            CancellationToken cancellationToken = default);

        Task<AdminFinancialDocumentDetailsDto?> GetFinancialDocumentAsync(
            string documentType,
            int sourceId,
            string? generatedByName = null,
            CancellationToken cancellationToken = default);
    }
}
