using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Admin
{
    /* ========================================================================
       ثوابت التقارير والمستندات
       ======================================================================== */

    public static class AdminFinancialReportTypes
    {
        public const string BookingCommissions = "BookingCommissions";
        public const string TopUps = "TopUps";
        public const string CommissionAccounts = "CommissionAccounts";
        public const string Debts = "Debts";
        public const string Transactions = "Transactions";
        public const string LowBalanceAccounts = "LowBalanceAccounts";
        public const string SuspendedAccounts = "SuspendedAccounts";
        public const string Comprehensive = "Comprehensive";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                BookingCommissions,
                TopUps,
                CommissionAccounts,
                Debts,
                Transactions,
                LowBalanceAccounts,
                SuspendedAccounts,
                Comprehensive
            };

        /// <summary>
        /// الأنواع التي تظهر للمستخدم في الواجهة الجديدة.
        /// تقريري انخفاض الرصيد والحسابات الموقوفة يبقيان مدعومين داخليًا
        /// ولكن يتم الوصول إليهما كفلاتر من تقرير حسابات الضمان.
        /// </summary>
        public static readonly IReadOnlyList<string> UiTypes =
            new[]
            {
                Comprehensive,
                BookingCommissions,
                TopUps,
                CommissionAccounts,
                Debts,
                Transactions
            };
    }

    public static class AdminFinancialDocumentTypes
    {
        public const string TopUpReceipt = "TopUpReceipt";
        public const string BookingCommissionSettlement = "BookingCommissionSettlement";
        public const string TransactionDocument = "TransactionDocument";
        public const string CommissionAccountStatement = "CommissionAccountStatement";

        public static readonly IReadOnlyList<string> All =
            new[]
            {
                TopUpReceipt,
                BookingCommissionSettlement,
                TransactionDocument,
                CommissionAccountStatement
            };
    }

    public static class AdminFinancialExportFormats
    {
        public const string View = "View";
        public const string Print = "Print";
        public const string Pdf = "Pdf";
        public const string Excel = "Excel";
    }

    public static class AdminFinancialReportDataTypes
    {
        public const string Text = "Text";
        public const string Number = "Number";
        public const string Money = "Money";
        public const string Percentage = "Percentage";
        public const string Date = "Date";
        public const string DateTime = "DateTime";
        public const string Status = "Status";
        public const string Boolean = "Boolean";
    }

    public static class AdminFinancialReportAlignments
    {
        public const string Right = "Right";
        public const string Center = "Center";
        public const string Left = "Left";
    }

    /* ========================================================================
       فلتر ومحرك التقارير
       ======================================================================== */

    public class AdminFinancialReportFilterDto
    {
        [Required]
        [StringLength(50)]
        public string ReportType { get; set; } =
            AdminFinancialReportTypes.Comprehensive;

        [StringLength(50)]
        public string DateScope { get; set; } =
            AdminFinancialDateScopes.BookingCreatedAt;

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int? OwnerId { get; set; }

        public int? PlaceId { get; set; }

        [Range(1, int.MaxValue)]
        public int? BookingId { get; set; }

        [Range(1, int.MaxValue)]
        public int? CommissionTransactionId { get; set; }

        public int? PlatformWalletId { get; set; }

        [StringLength(30)]
        public string? CommissionStatus { get; set; }

        [StringLength(30)]
        public string? AccountStatus { get; set; }

        [StringLength(30)]
        public string? TransactionType { get; set; }

        [StringLength(20)]
        public string? TransactionStatus { get; set; }

        [StringLength(20)]
        public string? TopUpStatus { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        public bool? HasDebt { get; set; }

        public bool? IsLowBalance { get; set; }

        public bool? IsSuspended { get; set; }

        [StringLength(120)]
        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(10, 50)]
        public int PageSize { get; set; } = 25;

        [StringLength(20)]
        public string ExportFormat { get; set; } =
            AdminFinancialExportFormats.View;
    }

    public class AdminFinancialReportFilterCapabilitiesDto
    {
        public bool ShowOwner { get; set; }

        public bool ShowPlace { get; set; }

        public bool ShowDateScope { get; set; }

        public bool ShowBookingId { get; set; }

        public bool ShowTransactionId { get; set; }

        public bool ShowPlatformWallet { get; set; }

        public bool ShowCommissionStatus { get; set; }

        public bool ShowAccountStatus { get; set; }

        public bool ShowTransactionType { get; set; }

        public bool ShowTransactionStatus { get; set; }

        public bool ShowTopUpStatus { get; set; }

        public bool ShowReferenceNumber { get; set; }

        public bool ShowHasDebt { get; set; }

        public bool ShowLowBalance { get; set; }

        public bool ShowSuspended { get; set; }

        public bool ShowSearch { get; set; } = true;
    }

    public class AdminFinancialReportOptionDto
    {
        public string Value { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? IconClass { get; set; }

        public bool IsSelected { get; set; }
    }

    public class AdminFinancialReportPageDto
    {
        public string PlatformName { get; set; } =
            "دليلك اليمني";

        public string? PlatformLogoPath { get; set; }

        public string Currency { get; set; } =
            "ريال يمني";

        public string ReportType { get; set; } =
            AdminFinancialReportTypes.Comprehensive;

        public string Title { get; set; } =
            "التقرير المالي";

        public string Subtitle { get; set; } =
            string.Empty;

        public string DateScopeLabel { get; set; } =
            string.Empty;

        public string PeriodLabel { get; set; } =
            "جميع الفترات";

        public string FilterDescription { get; set; } =
            "بدون فلاتر إضافية";

        public DateTime GeneratedAt { get; set; } =
            DateTime.Now;

        public string? GeneratedByName { get; set; }

        public AdminFinancialReportFilterDto Filter { get; set; } =
            new();

        public AdminFinancialReportFilterCapabilitiesDto Capabilities { get; set; } =
            new();

        public List<AdminFinancialReportOptionDto> ReportTypeOptions { get; set; } =
            new();

        public List<AdminFinancialOwnerOptionDto> Owners { get; set; } =
            new();

        public List<AdminFinancialPlaceOptionDto> Places { get; set; } =
            new();

        public List<AdminPlatformWalletOptionDto> PlatformWallets { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> DateScopeOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> CommissionStatusOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> AccountStatusOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> TransactionTypeOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> TransactionStatusOptions { get; set; } =
            new();

        public List<AdminFinancialSelectOptionDto> TopUpStatusOptions { get; set; } =
            new();

        public List<AdminFinancialReportMetricDto> Metrics { get; set; } =
            new();

        public List<AdminFinancialReportInsightDto> Insights { get; set; } =
            new();

        public List<AdminFinancialReportBreakdownItemDto> PlaceBreakdown { get; set; } =
            new();

        public List<AdminFinancialReportBreakdownItemDto> StatusBreakdown { get; set; } =
            new();

        public List<AdminFinancialReportSectionDto> Sections { get; set; } =
            new();

        public List<AdminFinancialDocumentListItemDto> Documents { get; set; } =
            new();

        public AdminFinancialPaginationDto Pagination { get; set; } =
            new();

        public int TotalAvailableRows { get; set; }

        public bool HasActiveFilters { get; set; }

        public bool HasRows =>
            TotalAvailableRows > 0 ||
            Sections.Exists(section => section.Rows.Count > 0);
    }

    public class AdminFinancialReportMetricDto
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public decimal? NumericValue { get; set; }

        public string? Hint { get; set; }

        public string? IconClass { get; set; }

        public string Tone { get; set; } = "Default";
    }

    public class AdminFinancialReportInsightDto
    {
        public string Title { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Tone { get; set; } = "Default";

        public string? IconClass { get; set; }
    }

    public class AdminFinancialReportBreakdownItemDto
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }

        public decimal Amount { get; set; }

        public decimal Percentage { get; set; }
    }

    public class AdminFinancialReportSectionDto
    {
        public string Key { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public List<AdminFinancialReportColumnDto> Columns { get; set; } =
            new();

        public List<AdminFinancialReportRowDto> Rows { get; set; } =
            new();

        public List<AdminFinancialReportMetricDto> Metrics { get; set; } =
            new();

        public string? EmptyMessage { get; set; }

        public bool IsPrimary { get; set; }
    }

    public class AdminFinancialReportColumnDto
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string DataType { get; set; } =
            AdminFinancialReportDataTypes.Text;

        public string Alignment { get; set; } =
            AdminFinancialReportAlignments.Right;

        public double Width { get; set; } = 18d;

        public bool IsVisible { get; set; } = true;

        public bool IsSortable { get; set; }

        public bool IsPrimary { get; set; }
    }

    public class AdminFinancialReportRowDto
    {
        public string RowKey { get; set; } = string.Empty;

        public List<string> Cells { get; set; } =
            new();

        public List<AdminFinancialReportCellDto> TypedCells { get; set; } =
            new();

        public string? Tone { get; set; }

        public string? DocumentType { get; set; }

        public int? DocumentSourceId { get; set; }

        public Dictionary<string, string> Metadata { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public class AdminFinancialReportCellDto
    {
        public string Text { get; set; } = string.Empty;

        public string DataType { get; set; } =
            AdminFinancialReportDataTypes.Text;

        public decimal? DecimalValue { get; set; }

        public long? IntegerValue { get; set; }

        public DateTime? DateValue { get; set; }

        public bool? BooleanValue { get; set; }

        public string? Tone { get; set; }

        public string? LinkUrl { get; set; }

        public string? Tooltip { get; set; }
    }

    /* ========================================================================
       صفوف التقارير المتخصصة
       ======================================================================== */

    public class AdminBookingCommissionReportRowDto
    {
        public int BookingId { get; set; }

        public int RecordId { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public decimal BookingValue { get; set; }

        public decimal CommissionRate { get; set; }

        public decimal CommissionBaseAmount { get; set; }

        public decimal CommissionAmount { get; set; }

        public decimal HeldAmount { get; set; }

        public decimal ChargedAmount { get; set; }

        public decimal ReleasedAmount { get; set; }

        public decimal DebtAmount { get; set; }

        public string BookingState { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public string CommissionStatus { get; set; } = string.Empty;

        public DateTime? BookingCreatedAt { get; set; }

        public DateTime? CommissionHeldAt { get; set; }

        public DateTime? CommissionSettledAt { get; set; }
    }

    public class AdminTopUpReportRowDto
    {
        public int CommissionTransactionId { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public int? PlatformWalletId { get; set; }

        public string PlatformWalletName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal DebtPaymentAmount { get; set; }

        public decimal CreditedAmount { get; set; }

        public string? ReferenceNumber { get; set; }

        public DateTime? TransferDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedByName { get; set; }

        public string? RejectionReason { get; set; }
    }

    public class AdminCommissionAccountReportRowDto
    {
        public int PlaceCommissionAccountId { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public bool AllowNegativeBalance { get; set; }

        public decimal DebtLimit { get; set; }

        public decimal LowBalanceThreshold { get; set; }

        public string AccountStatus { get; set; } = string.Empty;

        public DateTime? LastTopUpAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminDebtReportRowDto
    {
        public int PlaceCommissionAccountId { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public decimal DebtAmount { get; set; }

        public decimal DebtLimit { get; set; }

        public decimal RemainingDebtCapacity { get; set; }

        public int GraceDays { get; set; }

        public DateTime? DebtStartedAt { get; set; }

        public DateTime? LastDebtPaymentAt { get; set; }

        public decimal LastDebtPaymentAmount { get; set; }

        public string AccountStatus { get; set; } = string.Empty;
    }

    public class AdminFinancialTransactionReportRowDto
    {
        public int CommissionTransactionId { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public int? BookingId { get; set; }

        public int? PlatformWalletId { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal AvailableBalanceBefore { get; set; }

        public decimal AvailableBalanceAfter { get; set; }

        public decimal HeldBalanceBefore { get; set; }

        public decimal HeldBalanceAfter { get; set; }

        public decimal DebtBefore { get; set; }

        public decimal DebtAfter { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? ReferenceNumber { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? CreatedByName { get; set; }
    }

    public class AdminLowBalanceAccountReportRowDto
    {
        public int PlaceCommissionAccountId { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public decimal AvailableBalance { get; set; }

        public decimal LowBalanceThreshold { get; set; }

        public decimal ShortageAmount { get; set; }

        public decimal DebtAmount { get; set; }

        public string AccountStatus { get; set; } = string.Empty;

        public DateTime? LastTopUpAt { get; set; }
    }

    public class AdminSuspendedAccountReportRowDto
    {
        public int PlaceCommissionAccountId { get; set; }

        public int? OwnerId { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public int PlaceId { get; set; }

        public string PlaceName { get; set; } = string.Empty;

        public decimal AvailableBalance { get; set; }

        public decimal HeldBalance { get; set; }

        public decimal DebtAmount { get; set; }

        public decimal DebtLimit { get; set; }

        public string AccountStatus { get; set; } = string.Empty;

        public string SuspensionReason { get; set; } = string.Empty;

        public DateTime? SuspendedAt { get; set; }
    }

    /* ========================================================================
       المستندات المالية
       ======================================================================== */

    public class AdminFinancialDocumentListItemDto
    {
        public string DocumentType { get; set; } = string.Empty;

        public int SourceId { get; set; }

        public string DocumentNumber { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Subtitle { get; set; }

        public string? OwnerName { get; set; }

        public string? PlaceName { get; set; }

        public decimal? Amount { get; set; }

        public string? Status { get; set; }

        public DateTime DocumentDate { get; set; }

        public string? PreviewUrl { get; set; }

        public string? PrintUrl { get; set; }
    }

    public class AdminFinancialDocumentDetailsDto
    {
        public string PlatformName { get; set; } =
            "دليلك اليمني";

        public string? PlatformLogoPath { get; set; }

        public string? ContactEmail { get; set; }

        public string? SupportPhone { get; set; }

        public string Currency { get; set; } =
            "ريال يمني";

        public string DocumentType { get; set; } = string.Empty;

        public int SourceId { get; set; }

        public string DocumentNumber { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Subtitle { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime DocumentDate { get; set; }

        public DateTime GeneratedAt { get; set; } =
            DateTime.Now;

        public string? GeneratedByName { get; set; }

        public decimal? TotalAmount { get; set; }

        public List<AdminFinancialDocumentSectionDto> Sections { get; set; } =
            new();

        public List<AdminFinancialDocumentAmountDto> Amounts { get; set; } =
            new();

        public string? Notes { get; set; }

        public string? FooterText { get; set; }
    }

    public class AdminFinancialDocumentSectionDto
    {
        public string Title { get; set; } = string.Empty;

        public List<AdminFinancialDocumentFieldDto> Fields { get; set; } =
            new();
    }

    public class AdminFinancialDocumentFieldDto
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string? Hint { get; set; }

        public bool IsEmphasized { get; set; }
    }

    public class AdminFinancialDocumentAmountDto
    {
        public string Label { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Tone { get; set; } = "Default";

        public bool IsTotal { get; set; }
    }

    /* ========================================================================
       التصدير والطباعة وExcel
       ======================================================================== */

    public class AdminFinancialReportExportDto
    {
        public string PlatformName { get; set; } =
            "دليلك اليمني";

        public string? PlatformLogoPath { get; set; }

        public string? ContactEmail { get; set; }

        public string? SupportPhone { get; set; }

        public string Currency { get; set; } =
            "ريال يمني";

        public string ReportType { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string DateScopeLabel { get; set; } = string.Empty;

        public string PeriodLabel { get; set; } = string.Empty;

        public string FilterDescription { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; } =
            DateTime.Now;

        public string? GeneratedByName { get; set; }

        public int TotalAvailableRows { get; set; }

        public List<AdminFinancialReportMetricDto> Metrics { get; set; } =
            new();

        public List<AdminFinancialReportInsightDto> Insights { get; set; } =
            new();

        public List<AdminFinancialReportBreakdownItemDto> PlaceBreakdown { get; set; } =
            new();

        public List<AdminFinancialReportBreakdownItemDto> StatusBreakdown { get; set; } =
            new();

        public List<AdminFinancialReportExportSectionDto> Sections { get; set; } =
            new();

        public string SuggestedFileName { get; set; } =
            "admin-financial-report";
    }

    public class AdminFinancialReportExportSectionDto
    {
        public string Key { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public List<AdminFinancialReportExportColumnDto> Columns { get; set; } =
            new();

        public List<AdminFinancialReportExportRowDto> Rows { get; set; } =
            new();

        public List<AdminFinancialReportMetricDto> Metrics { get; set; } =
            new();
    }

    public class AdminFinancialReportExportColumnDto
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string DataType { get; set; } =
            AdminFinancialReportDataTypes.Text;

        public string Alignment { get; set; } =
            AdminFinancialReportAlignments.Right;

        public double Width { get; set; } = 18d;
    }

    public class AdminFinancialReportExportRowDto
    {
        public List<string> Cells { get; set; } =
            new();

        public List<AdminFinancialReportExportCellDto> TypedCells { get; set; } =
            new();

        public string? Tone { get; set; }
    }

    public class AdminFinancialReportExportCellDto
    {
        public string Text { get; set; } = string.Empty;

        public string DataType { get; set; } =
            AdminFinancialReportDataTypes.Text;

        public decimal? DecimalValue { get; set; }

        public long? IntegerValue { get; set; }

        public DateTime? DateValue { get; set; }

        public bool? BooleanValue { get; set; }
    }

    public class AdminFinancialExcelFileDto
    {
        public byte[] Content { get; set; } =
            Array.Empty<byte>();

        public string ContentType { get; set; } =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public string FileName { get; set; } =
            "admin-financial-report.xlsx";
    }

    public class AdminFinancialReportPrintDto
    {
        public AdminFinancialReportExportDto Report { get; set; } =
            new();

        public bool AutoPrint { get; set; } = true;
    }

    public class AdminFinancialDocumentPrintDto
    {
        public AdminFinancialDocumentDetailsDto Document { get; set; } =
            new();

        public bool AutoPrint { get; set; } = true;
    }
}
