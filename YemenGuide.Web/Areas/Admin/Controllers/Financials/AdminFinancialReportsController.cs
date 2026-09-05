using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Admin.Controllers.Financials
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [RequireAdminPermission(AdminPermissionKeys.Finance.Reports)]
    [Route("Admin/Financials/Reports")]
    public sealed class AdminFinancialReportsController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminFinancialReports.cshtml";

        private const string ReportPrintViewPath =
            "~/Areas/Admin/Views/AdminFinancials/AdminFinancialReportPrint.cshtml";

        private const string DocumentPrintViewPath =
            "~/Areas/Admin/Views/AdminFinancials/AdminFinancialDocumentPrint.cshtml";

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly ILogger<AdminFinancialReportsController> _logger;

        public AdminFinancialReportsController(
            IAdminFinancialService adminFinancialService,
            ILogger<AdminFinancialReportsController> logger)
        {
            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            [FromQuery] AdminFinancialReportFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminFinancialReportFilterDto();

            if (!IsValidReportType(filter.ReportType))
            {
                return BadRequest(
                    "نوع التقرير المالي غير صحيح.");
            }

            try
            {
                DisableResponseCaching();

                var model =
                    await _adminFinancialService.GetFinancialReportAsync(
                        filter,
                        includeAllRows: false,
                        generatedByName: User.Identity?.Name,
                        cancellationToken: cancellationToken);

                return PartialView(
                    PartialViewPath,
                    model);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء تحميل تبويب التقارير المالية.");

                return StatusCode(
                    500,
                    "تعذر تحميل التقارير المالية. أعد المحاولة.");
            }
        }

        [HttpGet("Excel")]
        public async Task<IActionResult> Excel(
            [FromQuery] AdminFinancialReportFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminFinancialReportFilterDto();

            if (!IsValidReportType(filter.ReportType))
            {
                return BadRequest(
                    "نوع التقرير المالي غير صحيح.");
            }

            try
            {
                DisableResponseCaching();

                var file =
                    await _adminFinancialService.BuildFinancialExcelAsync(
                        filter,
                        User.Identity?.Name,
                        cancellationToken);

                if (file.Content == null || file.Content.Length == 0)
                {
                    return NotFound(
                        "لا توجد بيانات قابلة للتصدير.");
                }

                return File(
                    file.Content,
                    file.ContentType,
                    file.FileName);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "فشل تصدير التقرير المالي إلى Excel.");

                return StatusCode(
                    500,
                    "تعذر إنشاء ملف Excel حاليًا.");
            }
        }

        [HttpGet("Print")]
        public async Task<IActionResult> Print(
            [FromQuery] AdminFinancialReportFilterDto? filter,
            [FromQuery] bool autoPrint = true,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminFinancialReportFilterDto();

            if (!IsValidReportType(filter.ReportType))
            {
                return BadRequest(
                    "نوع التقرير المالي غير صحيح.");
            }

            try
            {
                DisableResponseCaching();

                var report =
                    await _adminFinancialService.BuildFinancialReportExportAsync(
                        filter,
                        User.Identity?.Name,
                        cancellationToken);

                return View(
                    ReportPrintViewPath,
                    new AdminFinancialReportPrintDto
                    {
                        Report = report,
                        AutoPrint = autoPrint
                    });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "فشل تجهيز نسخة طباعة التقرير المالي.");

                return StatusCode(
                    500,
                    "تعذر تجهيز التقرير للطباعة.");
            }
        }

        [HttpGet("Document")]
        public async Task<IActionResult> Document(
            [FromQuery] string documentType,
            [FromQuery] int sourceId,
            [FromQuery] bool autoPrint = false,
            CancellationToken cancellationToken = default)
        {
            var normalizedDocumentType =
                documentType?.Trim() ?? string.Empty;

            if (sourceId <= 0 ||
                !AdminFinancialDocumentTypes.All.Contains(
                    normalizedDocumentType,
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "بيانات المستند المالي غير صحيحة.");
            }

            try
            {
                DisableResponseCaching();

                var document =
                    await _adminFinancialService.GetFinancialDocumentAsync(
                        normalizedDocumentType,
                        sourceId,
                        User.Identity?.Name,
                        cancellationToken);

                if (document == null)
                {
                    return NotFound(
                        "لم يتم العثور على المستند المالي المطلوب.");
                }

                return View(
                    DocumentPrintViewPath,
                    new AdminFinancialDocumentPrintDto
                    {
                        Document = document,
                        AutoPrint = autoPrint
                    });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "فشل تجهيز المستند المالي {DocumentType} للمصدر {SourceId}.",
                    normalizedDocumentType,
                    sourceId);

                return StatusCode(
                    500,
                    "تعذر تجهيز المستند المالي.");
            }
        }

        private static bool IsValidReportType(string? reportType)
        {
            return !string.IsNullOrWhiteSpace(reportType) &&
                   AdminFinancialReportTypes.All.Contains(
                       reportType.Trim(),
                       StringComparer.OrdinalIgnoreCase);
        }

        private void DisableResponseCaching()
        {
            Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }
    }
}
