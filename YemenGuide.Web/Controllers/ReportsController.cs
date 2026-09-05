using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Web.Services.Reports;

namespace YemenGuide.Web.Controllers
{
    /// <summary>
    /// واجهة العميل للشكاوى والبلاغات.
    ///
    /// هذه الطبقة لا تحتوي منطق القضايا نفسه؛ كل القراءة والكتابة تمر عبر
    /// ICustomerReportsService حتى يبقى الـ Controller خفيفًا وواضحًا.
    ///
    /// قاعدة المسار:
    /// العميل يرسل القضية إلى الإدارة أولًا، ولا يوجد إرسال مباشر للمالك
    /// من أي Action داخل هذا الـ Controller.
    /// </summary>
    [Authorize(Roles = "Customer")]
    public sealed class ReportsController : Controller
    {
        private const string MyReportsView =
            "~/Views/Panel/Reports/MyReports.cshtml";

        private const string CreateReportView =
            "~/Views/Panel/Reports/CreateReport.cshtml";

        private const string ReportDetailsView =
            "~/Views/Panel/Reports/ReportDetails.cshtml";

        private readonly ICustomerReportsService _reportsService;

        public ReportsController(
            ICustomerReportsService reportsService)
        {
            _reportsService = reportsService ??
                throw new ArgumentNullException(nameof(reportsService));
        }

        /* =====================================================================
           1. قائمة قضايا العميل
           ===================================================================== */

        [HttpGet("/Reports/MyReports")]
        public async Task<IActionResult> MyReports(
            [FromQuery] CustomerReportsFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            SetNoStoreHeaders();

            var model = await _reportsService.GetMyReportsAsync(
                customerId,
                filter ?? new CustomerReportsFilterDto(),
                cancellationToken);

            return View(MyReportsView, model);
        }

        /* =====================================================================
           2. إنشاء شكوى / بلاغ
           ===================================================================== */

        [HttpGet("/Reports/CreateReport")]
        public async Task<IActionResult> CreateReport(
            [FromQuery] string? requestKind = null,
            [FromQuery] string? reportType = null,
            [FromQuery] int? bookingId = null,
            [FromQuery] int? placeId = null,
            [FromQuery] int? reviewId = null,
            [FromQuery] int? promotionId = null,
            [FromQuery] int? advertisementId = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            SetNoStoreHeaders();

            var form = new CustomerCreateReportRequestDto
            {
                RequestKind = "Report",
                BookingId = PositiveOrNull(bookingId),
                PlaceId = PositiveOrNull(placeId),
                ReviewId = PositiveOrNull(reviewId),
                PromotionId = PositiveOrNull(promotionId),
                AdvertisementId = PositiveOrNull(advertisementId)
            };

            if (!string.IsNullOrWhiteSpace(reportType))
            {
                form.ReportType = reportType.Trim();
            }
            else if (form.BookingId.HasValue)
            {
                form.ReportType = "Booking";
            }
            else if (form.PlaceId.HasValue)
            {
                form.ReportType = "Place";
            }
            else if (form.ReviewId.HasValue)
            {
                form.ReportType = "Review";
            }
            else if (form.PromotionId.HasValue)
            {
                form.ReportType = "Promotion";
            }
            else if (form.AdvertisementId.HasValue)
            {
                form.ReportType = "Advertisement";
            }

            var model = await _reportsService.GetCreatePageAsync(
                customerId,
                form,
                cancellationToken);

            return View(CreateReportView, model);
        }

        [HttpGet("/Reports/SearchBookings")]
        public async Task<IActionResult> SearchBookings(
            [FromQuery] string? q = null,
            [FromQuery] int take = 10,
            [FromQuery] int skip = 0,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            SetNoStoreHeaders();

            var normalizedTake = Math.Clamp(take, 1, 10);
            var normalizedSkip = Math.Max(0, skip);

            // نطلب عنصرًا إضافيًا فقط لمعرفة هل توجد صفحة تالية.
            var rows = await _reportsService.SearchBookingsAsync(
                customerId,
                q,
                normalizedTake + 1,
                normalizedSkip,
                cancellationToken);

            var hasMore = rows.Count > normalizedTake;
            var items = rows.Take(normalizedTake).ToList();

            return Json(new
            {
                success = true,
                hasMore,
                nextSkip = normalizedSkip + items.Count,
                items = items.Select(item => new
                {
                    id = item.ReferenceId,
                    title = item.Title,
                    placeName = item.PlaceName,
                    subtitle = item.Subtitle,
                    date = item.Date?.ToString("yyyy/MM/dd")
                })
            });
        }

        [HttpGet("/Reports/SearchPlaces")]
        public async Task<IActionResult> SearchPlaces(
            [FromQuery] string? q = null,
            [FromQuery] int take = 12,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            SetNoStoreHeaders();

            var items = await _reportsService.SearchPlacesAsync(
                customerId,
                q,
                take,
                cancellationToken);

            return Json(new
            {
                success = true,
                items = items.Select(item => new
                {
                    id = item.ReferenceId,
                    title = item.Title,
                    subtitle = item.Subtitle
                })
            });
        }

        [HttpPost("/Reports/CreateReport")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> CreateReport(
            [FromForm] CustomerCreateReportRequestDto request,
            [FromForm] IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            SetNoStoreHeaders();

            if (!ModelState.IsValid)
            {
                if (ExpectsJson())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = FirstModelStateError(
                            "راجع البيانات المطلوبة ثم حاول مرة أخرى."),
                        errorCode = "validation"
                    });
                }

                var invalidModel = await _reportsService.GetCreatePageAsync(
                    customerId,
                    request,
                    cancellationToken);

                return View(CreateReportView, invalidModel);
            }

            var result = await _reportsService.CreateAsync(
                customerId,
                request,
                evidenceFile,
                cancellationToken);

            if (!result.Success)
            {
                if (ExpectsJson())
                {
                    return BadRequest(ToJsonResult(result));
                }

                ModelState.AddModelError(string.Empty, result.Message);

                var failedModel = await _reportsService.GetCreatePageAsync(
                    customerId,
                    request,
                    cancellationToken);

                return View(CreateReportView, failedModel);
            }

            var detailsUrl = Url.Action(
                nameof(ReportDetails),
                "Reports",
                new { id = result.ReportId }) ??
                $"/Reports/ReportDetails/{result.ReportId}";

            if (ExpectsJson())
            {
                return Json(new
                {
                    success = true,
                    result.Message,
                    result.ReportId,
                    result.ReportNumber,
                    redirectUrl = detailsUrl
                });
            }

            TempData["ReportSuccess"] = result.Message;

            return RedirectToAction(
                nameof(ReportDetails),
                new { id = result.ReportId });
        }

        /* =====================================================================
           3. تفاصيل القضية
           ===================================================================== */

        [HttpGet("/Reports/ReportDetails/{id:int}")]
        public async Task<IActionResult> ReportDetails(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return NotFound();
            }

            SetNoStoreHeaders();

            var model = await _reportsService.GetDetailsAsync(
                customerId,
                id,
                cancellationToken);

            if (model == null)
            {
                return NotFound();
            }

            return View(ReportDetailsView, model);
        }

        /* =====================================================================
           4. إضافة توضيح أو دليل جديد لنفس القضية
           ===================================================================== */

        [HttpPost("/Reports/ReportDetails/{id:int}/Clarification")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> AddClarification(
            int id,
            [FromForm] CustomerReportClarificationRequestDto request,
            [FromForm] IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return OperationValidationFailure(
                    id,
                    "راجع التوضيح المرسل ثم حاول مرة أخرى.");
            }

            var result = await _reportsService.AddClarificationAsync(
                customerId,
                id,
                request,
                evidenceFile,
                cancellationToken);

            return FinishDetailsOperation(id, result);
        }

        /* =====================================================================
           5. تأكيد العميل: تم الحل / المشكلة ما زالت مستمرة
           ===================================================================== */

        [HttpPost("/Reports/ReportDetails/{id:int}/ConfirmResolution")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> ConfirmResolution(
            int id,
            [FromForm] CustomerReportResolutionConfirmationDto request,
            [FromForm] IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return OperationValidationFailure(
                    id,
                    "تعذر قراءة تأكيدك. حاول مرة أخرى.");
            }

            var result = await _reportsService.ConfirmResolutionAsync(
                customerId,
                id,
                request,
                evidenceFile,
                cancellationToken);

            return FinishDetailsOperation(id, result);
        }

        /* =====================================================================
           6. طلب إعادة المراجعة داخل نفس القضية
           ===================================================================== */

        [HttpPost("/Reports/ReportDetails/{id:int}/RequestReopen")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> RequestReopen(
            int id,
            [FromForm] CustomerReportReopenRequestDto request,
            [FromForm] IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return OperationValidationFailure(
                    id,
                    "اكتب سببًا واضحًا لإعادة المراجعة.");
            }

            var result = await _reportsService.RequestReopenAsync(
                customerId,
                id,
                request,
                evidenceFile,
                cancellationToken);

            return FinishDetailsOperation(id, result);
        }

        /* =====================================================================
           7. سحب القضية بواسطة العميل
           ===================================================================== */

        [HttpPost("/Reports/ReportDetails/{id:int}/Withdraw")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(
            int id,
            [FromForm] CustomerReportWithdrawRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return OperationValidationFailure(
                    id,
                    "راجع سبب السحب ثم حاول مرة أخرى.");
            }

            var result = await _reportsService.WithdrawAsync(
                customerId,
                id,
                request,
                cancellationToken);

            return FinishDetailsOperation(id, result);
        }

        /* =====================================================================
           أدوات داخلية
           ===================================================================== */

        private bool TryGetCustomerId(out int customerId)
        {
            var customerIdText = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(customerIdText, out customerId) &&
                   customerId > 0;
        }

        private IActionResult FinishDetailsOperation(
            int reportId,
            CustomerReportOperationResultDto result)
        {
            if (ExpectsJson())
            {
                var payload = ToJsonResult(result);

                return result.Success
                    ? Json(payload)
                    : BadRequest(payload);
            }

            if (result.Success)
            {
                TempData["ReportSuccess"] = result.Message;
            }
            else
            {
                TempData["ReportError"] = result.Message;
            }

            return RedirectToAction(
                nameof(ReportDetails),
                new { id = reportId });
        }

        private IActionResult OperationValidationFailure(
            int reportId,
            string fallbackMessage)
        {
            var message = FirstModelStateError(fallbackMessage);

            if (ExpectsJson())
            {
                return BadRequest(new
                {
                    success = false,
                    message,
                    errorCode = "validation"
                });
            }

            TempData["ReportError"] = message;

            return RedirectToAction(
                nameof(ReportDetails),
                new { id = reportId });
        }

        private object ToJsonResult(
            CustomerReportOperationResultDto result)
        {
            var redirectUrl = result.ReportId.HasValue
                ? Url.Action(
                    nameof(ReportDetails),
                    "Reports",
                    new { id = result.ReportId.Value })
                : null;

            return new
            {
                success = result.Success,
                result.Message,
                result.ErrorCode,
                result.ReportId,
                result.ReportNumber,
                redirectUrl
            };
        }

        private string FirstModelStateError(
            string fallbackMessage)
        {
            return ModelState.Values
                       .SelectMany(value => value.Errors)
                       .Select(error =>
                           string.IsNullOrWhiteSpace(error.ErrorMessage)
                               ? error.Exception?.Message
                               : error.ErrorMessage)
                       .FirstOrDefault(message =>
                           !string.IsNullOrWhiteSpace(message))
                   ?? fallbackMessage;
        }

        private bool ExpectsJson()
        {
            if (Request.Headers.TryGetValue(
                    "X-Requested-With",
                    out var requestedWith) &&
                requestedWith.Any(value =>
                    string.Equals(
                        value,
                        "XMLHttpRequest",
                        StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var accept = Request.Headers["Accept"].ToString();

            return accept.Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase);
        }

        private void SetNoStoreHeaders()
        {
            Response.Headers["Cache-Control"] =
                "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
        }

        private static int? PositiveOrNull(int? value)
        {
            return value.HasValue && value.Value > 0
                ? value
                : null;
        }
    }
}
