using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Reports;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [RequireAdminPermission(AdminPermissionKeys.Cases.View)]
    [Route("Admin/Reports")]
    public sealed class ReportsController : Controller
    {
        private const string ReportsCenterView =
            "~/Areas/Admin/Views/Reports/ReportsCenter.cshtml";

        private const string ReportDetailsView =
            "~/Areas/Admin/Views/Reports/ReportDetails.cshtml";

        private readonly IAdminReportsService _reportsService;

        public ReportsController(IAdminReportsService reportsService)
        {
            _reportsService = reportsService ??
                throw new ArgumentNullException(nameof(reportsService));
        }

        /* =====================================================================
           الصفحات والبيانات
           ===================================================================== */

        [HttpGet("")]
        [HttpGet("Center")]
        public async Task<IActionResult> ReportsCenter(
            [FromQuery] string? tab,
            [FromQuery] AdminReportFilterDto filter,
            CancellationToken cancellationToken)
        {
            var model = await _reportsService.GetReportsCenterAsync(
                tab,
                filter,
                cancellationToken);

            return View(ReportsCenterView, model);
        }

        [HttpGet("Summary")]
        public async Task<IActionResult> ReportsSummary(
            CancellationToken cancellationToken)
        {
            var summary = await _reportsService.GetSummaryAsync(cancellationToken);
            return Json(new { success = true, summary });
        }

        [HttpGet("List")]
        public async Task<IActionResult> ReportsList(
            [FromQuery] string? tab,
            [FromQuery] AdminReportFilterDto filter,
            CancellationToken cancellationToken)
        {
            var model = await _reportsService.GetReportsListAsync(
                tab,
                filter,
                cancellationToken);

            return Json(new { success = true, data = model });
        }

        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> ReportDetails(
            int id,
            CancellationToken cancellationToken)
        {
            if (id <= 0) return NotFound();

            var model = await _reportsService.GetReportDetailsAsync(id, cancellationToken);
            if (model == null) return NotFound("القضية غير موجودة.");

            return View(ReportDetailsView, model);
        }

        /* =====================================================================
           المراجعة والتواصل
           لا يوجد أي Action لإسناد الموظفين داخل البلاغات.
           ===================================================================== */

        [HttpPost("StartReview")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Manage)]
        public Task<IActionResult> StartReview(
            [FromForm] AdminReportSimpleActionDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.StartReviewAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("RequestClarification")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Manage)]
        public Task<IActionResult> RequestClarification(
            [FromForm] AdminReportReplyRequestDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.RequestClarificationAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("RequestOwnerResponse")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Manage)]
        public Task<IActionResult> RequestOwnerResponse(
            [FromForm] AdminReportReplyRequestDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.RequestOwnerResponseAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("RequestResolutionConfirmation")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Resolve)]
        public Task<IActionResult> RequestResolutionConfirmation(
            [FromForm] AdminReportResolutionConfirmationRequestDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.RequestResolutionConfirmationAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("ChangePriority")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Manage)]
        public Task<IActionResult> ChangePriority(
            [FromForm] AdminReportPriorityDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.ChangePriorityAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("Escalate")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Escalate)]
        public Task<IActionResult> Escalate(
            [FromForm] AdminReportEscalationDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.EscalateAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("AddInternalNote")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Manage)]
        public Task<IActionResult> AddInternalNote(
            [FromForm] AdminReportInternalNoteRequestDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.AddInternalNoteAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        /* =====================================================================
           القرارات
           ===================================================================== */

        [HttpPost("Resolve")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Resolve)]
        public Task<IActionResult> Resolve(
            [FromForm] AdminReportResolutionDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.ResolveAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("Reject")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Resolve)]
        public Task<IActionResult> Reject(
            [FromForm] AdminReportRejectionDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.RejectAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        // بلاغ مالك ضد عميل: لا يطلب من العميل الرد.
        // الإدارة فقط تحدد هل المخالفة مثبتة أم غير مثبتة.
        [HttpPost("ConfirmCustomerViolation")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Resolve)]
        public Task<IActionResult> ConfirmCustomerViolation(
            [FromForm] AdminCustomerViolationDecisionDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.ConfirmCustomerViolationAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("DismissCustomerViolation")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Resolve)]
        public Task<IActionResult> DismissCustomerViolation(
            [FromForm] AdminCustomerViolationDecisionDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.DismissCustomerViolationAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("Close")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Resolve)]
        public Task<IActionResult> Close(
            [FromForm] AdminReportCloseDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.CloseAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        [HttpPost("Reopen")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Cases.Reopen)]
        public Task<IActionResult> Reopen(
            [FromForm] AdminReportReopenDto input,
            CancellationToken cancellationToken) =>
            ExecuteOperationAsync(
                input,
                (adminId, context) => _reportsService.ReopenAsync(
                    input,
                    adminId,
                    context,
                    cancellationToken));

        /* =====================================================================
           أدوات موحدة
           ===================================================================== */

        private async Task<IActionResult> ExecuteOperationAsync<TInput>(
            TInput input,
            Func<int, AdminReportRequestContextDto, Task<AdminReportOperationResultDto>> operation)
            where TInput : class
        {
            if (!TryGetCurrentAdminId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "انتهت جلسة تسجيل الدخول."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "راجع بيانات الإجراء ثم حاول مرة أخرى.",
                    errors = GetModelStateErrors()
                });
            }

            var result = await operation(adminUserId, BuildRequestContext());
            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    errorCode = result.ErrorCode
                });
            }

            return Json(new
            {
                success = true,
                message = result.Message,
                reportId = result.ReportId,
                redirectUrl = result.ReportId.HasValue
                    ? Url.Action(
                        nameof(ReportDetails),
                        "Reports",
                        new { area = "Admin", id = result.ReportId.Value })
                    : null
            });
        }

        private bool TryGetCurrentAdminId(out int adminUserId)
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out adminUserId) && adminUserId > 0;
        }

        private AdminReportRequestContextDto BuildRequestContext() => new()
        {
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString()
        };

        private Dictionary<string, string[]> GetModelStateErrors()
        {
            return ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error =>
                            !string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? error.ErrorMessage
                                : "القيمة المدخلة غير صحيحة.")
                        .Distinct()
                        .ToArray());
        }
    }
}
