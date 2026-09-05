using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Reports;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner")]
    [AuthorizeActiveOwner]
    public sealed class ReportsController : Controller
    {
        private const string OwnerReportsView =
            "~/Areas/Owner/Views/Reports/OwnerReports.cshtml";

        private const string OwnerReportDetailsView =
            "~/Areas/Owner/Views/Reports/OwnerReportDetails.cshtml";

        private const string CreateOwnerReportView =
            "~/Areas/Owner/Views/Reports/CreateOwnerReport.cshtml";

        private readonly IOwnerReportsService _reportsService;

        public ReportsController(IOwnerReportsService reportsService)
        {
            _reportsService = reportsService ??
                throw new ArgumentNullException(nameof(reportsService));
        }

        [HttpGet("/Owner/Reports/OwnerReports")]
        public async Task<IActionResult> OwnerReports(
            [FromQuery] OwnerReportsFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetOwnerId(out var ownerId))
                return Challenge();

            SetNoStoreHeaders();

            var model = await _reportsService.GetReportsPageAsync(
                ownerId,
                filter ?? new OwnerReportsFilterDto(),
                cancellationToken);

            return View(OwnerReportsView, model);
        }

        [HttpGet("/Owner/Reports/OwnerReportDetails/{id:int}")]
        public async Task<IActionResult> OwnerReportDetails(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetOwnerId(out var ownerId))
                return Challenge();

            if (id <= 0)
                return NotFound();

            SetNoStoreHeaders();

            var model = await _reportsService.GetDetailsAsync(
                ownerId,
                id,
                cancellationToken);

            if (model == null)
                return NotFound();

            return View(OwnerReportDetailsView, model);
        }

        [HttpGet("/Owner/Reports/CreateOwnerReport")]
        public async Task<IActionResult> CreateOwnerReport(
            [FromQuery] string? referenceType = null,
            [FromQuery] int? referenceId = null,
            [FromQuery] string? reportType = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetOwnerId(out var ownerId))
                return Challenge();

            SetNoStoreHeaders();

            var form = new OwnerCreateReportRequestDto
            {
                ReferenceType = string.IsNullOrWhiteSpace(referenceType)
                    ? OwnerReportValues.ReferenceBooking
                    : referenceType.Trim(),
                ReferenceId = PositiveOrZero(referenceId),
                ReportType = string.IsNullOrWhiteSpace(reportType)
                    ? YemenGuide.Core.Constants.ReportConstants.Types.Booking
                    : reportType.Trim()
            };

            var model = await _reportsService.GetCreatePageAsync(
                ownerId,
                form,
                cancellationToken);

            return View(CreateOwnerReportView, model);
        }

        [HttpPost("/Owner/Reports/CreateOwnerReport")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> CreateOwnerReport(
            [FromForm] OwnerCreateReportRequestDto request,
            [FromForm] IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetOwnerId(out var ownerId))
                return Challenge();

            SetNoStoreHeaders();

            if (!ModelState.IsValid)
            {
                if (ExpectsJson())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = FirstModelStateError("راجع بيانات البلاغ ثم حاول مرة أخرى."),
                        errorCode = "validation"
                    });
                }

                var invalidModel = await _reportsService.GetCreatePageAsync(
                    ownerId,
                    request,
                    cancellationToken);

                return View(CreateOwnerReportView, invalidModel);
            }

            var result = await _reportsService.CreateAsync(
                ownerId,
                request,
                evidenceFile,
                cancellationToken);

            if (!result.Success)
            {
                if (ExpectsJson())
                    return BadRequest(ToJsonResult(result));

                ModelState.AddModelError(string.Empty, result.Message);
                var failedModel = await _reportsService.GetCreatePageAsync(
                    ownerId,
                    request,
                    cancellationToken);

                return View(CreateOwnerReportView, failedModel);
            }

            var detailsUrl = $"/Owner/Reports/OwnerReportDetails/{result.ReportId}";

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

            TempData["OwnerReportSuccess"] = result.Message;
            return RedirectToAction(
                nameof(OwnerReportDetails),
                new { id = result.ReportId });
        }

        [HttpPost("/Owner/Reports/OwnerReportDetails/{id:int}/Response")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> SubmitResponse(
            int id,
            [FromForm] OwnerReportResponseRequestDto request,
            [FromForm] IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetOwnerId(out var ownerId))
                return Challenge();

            if (id <= 0)
                return NotFound();

            if (!ModelState.IsValid)
                return OperationValidationFailure(
                    id,
                    "اكتب ردًا واضحًا ثم حاول مرة أخرى.");

            var result = await _reportsService.SubmitResponseAsync(
                ownerId,
                id,
                request,
                evidenceFile,
                cancellationToken);

            return FinishDetailsOperation(id, result);
        }

        [HttpGet("/Owner/Reports/SearchBookings")]
        public async Task<IActionResult> SearchBookings(
            [FromQuery] string? q = null,
            [FromQuery] int take = 10,
            [FromQuery] int skip = 0,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetOwnerId(out var ownerId))
                return Challenge();

            SetNoStoreHeaders();

            var normalizedTake = Math.Clamp(take, 1, 10);
            var normalizedSkip = Math.Max(0, skip);

            var rows = await _reportsService.SearchBookingsAsync(
                ownerId,
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
                    item.Title,
                    item.Subtitle,
                    item.PlaceName,
                    item.CustomerName,
                    warningCount = item.CustomerWarningCount,
                    date = item.Date?.ToString("yyyy/MM/dd")
                })
            });
        }

        [HttpGet("/Owner/Reports/SearchReviews")]
        public async Task<IActionResult> SearchReviews(
            [FromQuery] string? q = null,
            [FromQuery] int take = 10,
            [FromQuery] int skip = 0,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetOwnerId(out var ownerId))
                return Challenge();

            SetNoStoreHeaders();

            var normalizedTake = Math.Clamp(take, 1, 10);
            var normalizedSkip = Math.Max(0, skip);

            var rows = await _reportsService.SearchReviewsAsync(
                ownerId,
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
                    item.Title,
                    item.Subtitle,
                    item.PlaceName,
                    item.CustomerName,
                    warningCount = item.CustomerWarningCount,
                    date = item.Date?.ToString("yyyy/MM/dd")
                })
            });
        }

        private bool TryGetOwnerId(out int ownerId)
        {
            var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idText, out ownerId) && ownerId > 0;
        }

        private IActionResult FinishDetailsOperation(
            int reportId,
            OwnerReportOperationResultDto result)
        {
            if (ExpectsJson())
            {
                var payload = ToJsonResult(result);
                return result.Success ? Json(payload) : BadRequest(payload);
            }

            if (result.Success)
                TempData["OwnerReportSuccess"] = result.Message;
            else
                TempData["OwnerReportError"] = result.Message;

            return RedirectToAction(
                nameof(OwnerReportDetails),
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

            TempData["OwnerReportError"] = message;
            return RedirectToAction(
                nameof(OwnerReportDetails),
                new { id = reportId });
        }

        private static object ToJsonResult(OwnerReportOperationResultDto result)
        {
            return new
            {
                success = result.Success,
                result.Message,
                result.ErrorCode,
                result.ReportId,
                result.ReportNumber,
                redirectUrl = result.Success && result.ReportId.HasValue
                    ? $"/Owner/Reports/OwnerReportDetails/{result.ReportId.Value}"
                    : null
            };
        }

        private string FirstModelStateError(string fallback)
        {
            return ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? fallback;
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
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
        }

        private static int PositiveOrZero(int? value)
        {
            return value.HasValue && value.Value > 0 ? value.Value : 0;
        }
    }
}
