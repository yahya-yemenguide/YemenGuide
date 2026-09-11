using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Owner.Controllers.OwnerFinancials
{
    [Area("Owner")]
    [Authorize]
    [AuthorizeActiveOwner]
    [Route("Owner/Financials/InvoicesReports")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public sealed class OwnerFinancialInvoicesReportsController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Owner/Views/OwnerFinancials/_OwnerFinancialInvoicesReports.cshtml";

        private const string DocumentPrintViewPath =
            "~/Areas/Owner/Views/OwnerFinancials/OwnerFinancialDocumentPrint.cshtml";

        private const string ReportPrintViewPath =
            "~/Areas/Owner/Views/OwnerFinancials/OwnerFinancialReportPrint.cshtml";

        private readonly IOwnerFinancialService _ownerFinancialService;
        private readonly ILogger<OwnerFinancialInvoicesReportsController> _logger;

        public OwnerFinancialInvoicesReportsController(
            IOwnerFinancialService ownerFinancialService,
            ILogger<OwnerFinancialInvoicesReportsController> logger)
        {
            _ownerFinancialService = ownerFinancialService ??
                throw new ArgumentNullException(nameof(ownerFinancialService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("~/Owner/Financials/Reports")]
        public async Task<IActionResult> Index(
            [FromQuery] FinancialReportFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new FinancialReportFilterDto();
            NormalizeFilter(filter);
            filter.ExportFormat = OwnerFinancialExportFormats.View;

            if (!IsAjaxRequest())
            {
                return Redirect(BuildFinancialPageUrl(filter));
            }

            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized("تعذر تحديد حساب المالك الحالي.");
            }

            try
            {
                filter.PlaceId = await ResolveActivePlaceIdAsync(
                    ownerId,
                    cancellationToken);

                var model = await _ownerFinancialService.GetReportsAsync(
                    ownerId,
                    filter,
                    cancellationToken);

                return PartialView(PartialViewPath, model);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تحميل الفواتير والتقارير للمالك {OwnerId} ونوع التقرير {ReportType}.",
                    ownerId,
                    filter.ReportType);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "تعذر تحميل الفواتير والتقارير حاليًا.");
            }
        }

        [HttpGet("Document")]
        public async Task<IActionResult> PrintDocument(
            [FromQuery] string? documentType,
            [FromQuery] int sourceId,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized();
            }

            documentType = NormalizeText(documentType);

            if (string.IsNullOrWhiteSpace(documentType) ||
                sourceId <= 0 ||
                !OwnerFinancialDocumentTypes.All.Any(value =>
                    string.Equals(
                        value,
                        documentType,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest("بيانات المستند المالي غير صحيحة.");
            }

            try
            {
                var activePlaceId = await ResolveActivePlaceIdAsync(
                    ownerId,
                    cancellationToken);

                if (!activePlaceId.HasValue)
                {
                    return NotFound("لا توجد منشأة محددة لعرض المستند.");
                }

                // مهم: نمر عبر OwnerFinancialService حتى تكون الطباعة بنفس
                // عملة البروفايل وبنفس تحويل الشاشة، لا بالقيم الخام YER.
                var document = await _ownerFinancialService.GetFinancialDocumentAsync(
                    ownerId,
                    documentType,
                    sourceId,
                    cancellationToken);

                if (document == null || document.PlaceId != activePlaceId.Value)
                {
                    return NotFound("المستند غير موجود أو لا تملك صلاحية عرضه.");
                }

                ViewData["Title"] = document.Title;
                return View(DocumentPrintViewPath, document);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر فتح المستند {DocumentType} للمصدر {SourceId} والمالك {OwnerId}.",
                    documentType,
                    sourceId,
                    ownerId);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "تعذر فتح المستند المالي حاليًا.");
            }
        }

        [HttpGet("Print")]
        public async Task<IActionResult> PrintReport(
            [FromQuery] FinancialReportFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new FinancialReportFilterDto();
            NormalizeFilter(filter);
            filter.ExportFormat = OwnerFinancialExportFormats.Print;

            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized();
            }

            try
            {
                filter.PlaceId = await ResolveActivePlaceIdAsync(
                    ownerId,
                    cancellationToken);

                // نفس مسار التحويل المستخدم في الشاشة وExcel.
                var report = await _ownerFinancialService.BuildFinancialReportExportAsync(
                    ownerId,
                    filter,
                    cancellationToken);

                ViewData["Title"] = report.Title;
                return View(ReportPrintViewPath, report);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تجهيز التقرير المطبوع للمالك {OwnerId} ونوع التقرير {ReportType}.",
                    ownerId,
                    filter.ReportType);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "تعذر تجهيز التقرير للطباعة حاليًا.");
            }
        }

        [HttpGet("Excel")]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] FinancialReportFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new FinancialReportFilterDto();
            NormalizeFilter(filter);
            filter.ExportFormat = OwnerFinancialExportFormats.Excel;

            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized();
            }

            try
            {
                filter.PlaceId = await ResolveActivePlaceIdAsync(
                    ownerId,
                    cancellationToken);

                var excel = await _ownerFinancialService.BuildFinancialExcelAsync(
                    ownerId,
                    filter,
                    cancellationToken);

                if (excel.Content == null || excel.Content.Length == 0)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "تعذر إنشاء ملف Excel حاليًا.");
                }

                return File(
                    excel.Content,
                    excel.ContentType,
                    excel.FileName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تصدير Excel للمالك {OwnerId} ونوع التقرير {ReportType}.",
                    ownerId,
                    filter.ReportType);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "تعذر تجهيز ملف Excel حاليًا.");
            }
        }

        private void NormalizeFilter(FinancialReportFilterDto filter)
        {
            NormalizeDateRange(filter);

            filter.ReportType =
                NormalizeText(filter.ReportType) ??
                OwnerFinancialReportTypes.Comprehensive;

            filter.DocumentType = NormalizeText(filter.DocumentType);
            filter.DateScope = NormalizeText(filter.DateScope);
            filter.PaymentChannel = NormalizeText(filter.PaymentChannel);
            filter.BookingState = NormalizeText(filter.BookingState);
            filter.PaymentStatus = NormalizeText(filter.PaymentStatus);
            filter.CommissionStatus = NormalizeText(filter.CommissionStatus);
            filter.RefundStatus = NormalizeText(filter.RefundStatus);
            filter.TransactionType = NormalizeText(filter.TransactionType);
            filter.TransactionStatus = NormalizeText(filter.TransactionStatus);
            filter.ReferenceNumber = NormalizeText(filter.ReferenceNumber);
            filter.Search = NormalizeText(filter.Search);
            filter.PageNumber = Math.Max(1, filter.PageNumber);

            if (filter.PageSize != 10 &&
                filter.PageSize != 25 &&
                filter.PageSize != 50)
            {
                filter.PageSize = 25;
            }
        }

        private void NormalizeDateRange(FinancialReportFilterDto filter)
        {
            if (!filter.FromDate.HasValue ||
                !filter.ToDate.HasValue ||
                filter.FromDate.Value.Date <= filter.ToDate.Value.Date)
            {
                return;
            }

            var temporaryDate = filter.FromDate;
            filter.FromDate = filter.ToDate;
            filter.ToDate = temporaryDate;

            ModelState.Remove(nameof(FinancialReportFilterDto.FromDate));
            ModelState.Remove(nameof(FinancialReportFilterDto.ToDate));
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static string BuildFinancialPageUrl(FinancialReportFilterDto filter)
        {
            var values = BuildFilterQueryValues(filter);
            values.Insert(
                0,
                new KeyValuePair<string, string?>("financeTab", "reports"));

            return "/Owner/Financials" +
                QueryString.Create(values).ToUriComponent();
        }

        private static List<KeyValuePair<string, string?>> BuildFilterQueryValues(
            FinancialReportFilterDto filter)
        {
            var values = new List<KeyValuePair<string, string?>>();

            AddInteger(values, "BookingId", filter.BookingId);
            AddInteger(values, "CustomerId", filter.CustomerId);
            AddText(values, "ReportType", filter.ReportType);
            AddText(values, "DocumentType", filter.DocumentType);
            AddText(values, "DateScope", filter.DateScope);
            AddText(values, "PaymentChannel", filter.PaymentChannel);
            AddText(values, "BookingState", filter.BookingState);
            AddText(values, "PaymentStatus", filter.PaymentStatus);
            AddText(values, "CommissionStatus", filter.CommissionStatus);
            AddText(values, "RefundStatus", filter.RefundStatus);
            AddText(values, "TransactionType", filter.TransactionType);
            AddText(values, "TransactionStatus", filter.TransactionStatus);
            AddText(values, "ReferenceNumber", filter.ReferenceNumber);
            AddText(values, "Search", filter.Search);
            AddDate(values, "FromDate", filter.FromDate);
            AddDate(values, "ToDate", filter.ToDate);

            if (filter.PageNumber > 1)
            {
                values.Add(new(
                    "PageNumber",
                    filter.PageNumber.ToString(CultureInfo.InvariantCulture)));
            }

            if (filter.PageSize != 25)
            {
                values.Add(new(
                    "PageSize",
                    filter.PageSize.ToString(CultureInfo.InvariantCulture)));
            }

            return values;
        }

        private static void AddInteger(
            ICollection<KeyValuePair<string, string?>> values,
            string key,
            int? value)
        {
            if (!value.HasValue || value.Value <= 0)
            {
                return;
            }

            values.Add(new(
                key,
                value.Value.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AddDate(
            ICollection<KeyValuePair<string, string?>> values,
            string key,
            DateTime? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            values.Add(new(
                key,
                value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        private static void AddText(
            ICollection<KeyValuePair<string, string?>> values,
            string key,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            values.Add(new(key, value.Trim()));
        }

        private Task<int?> ResolveActivePlaceIdAsync(
            int ownerId,
            CancellationToken cancellationToken)
        {
            return _ownerFinancialService.ResolveActivePlaceIdAsync(
                ownerId,
                null,
                GetCookiePlaceId(ownerId),
                cancellationToken);
        }

        private int? GetCookiePlaceId(int ownerId)
        {
            var cookieName = $"ownerActivePlace_{ownerId}";

            return int.TryParse(Request.Cookies[cookieName], out var placeId) &&
                   placeId > 0
                ? placeId
                : null;
        }

        private bool TryGetCurrentOwnerId(out int ownerId)
        {
            ownerId = 0;

            return int.TryParse(
                       User.FindFirstValue(ClaimTypes.NameIdentifier),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out ownerId) &&
                   ownerId > 0;
        }

        private bool IsAjaxRequest()
        {
            if (!Request.Headers.TryGetValue(
                    "X-Requested-With",
                    out var requestedWith))
            {
                return false;
            }

            return string.Equals(
                requestedWith.ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
 