using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Web.Services.Advertisements;
using YemenGuide.Web.Services.Employees;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public sealed class AdvertisementsController : Controller
    {
        private readonly IAdminAdvertisementService _service;
        private readonly IAdminPermissionService _permissions;

        public AdvertisementsController(
            IAdminAdvertisementService service,
            IAdminPermissionService permissions)
        {
            _service = service;
            _permissions = permissions;
        }

        [HttpGet]
        public async Task<IActionResult> AdvertisementManagement(
            string? tab = null,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.View, cancellationToken))
                return Forbid();

            var model = await _service.GetManagementPageAsync(userId, tab, cancellationToken);
            return View(model);
        }

        // هذه الـActions لم تعد صفحات أو Partial Views.
        // هي endpoints بيانات فقط لتحديث نفس شاشة الإدارة الواحدة بدون تنقل.
        [HttpGet]
        public async Task<IActionResult> OverviewTab(CancellationToken cancellationToken = default)
        {
            if (!await CanAsync(CurrentUserId(), AdminPermissionKeys.Advertisements.View, cancellationToken))
                return Forbid();

            return Json(await _service.GetOverviewAsync(cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> AdvertisementsTab(
            [FromQuery] AdminAdvertisementFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.View, cancellationToken))
                return Forbid();

            return Json(await _service.GetAdvertisementsAsync(filter, userId, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> PlansTab(CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.ManagePlans, cancellationToken))
                return Forbid();

            return Json(await _service.GetPlansAsync(userId, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> ReportsTab(
            [FromQuery] AdminAdvertisementReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            if (!await CanAsync(CurrentUserId(), AdminPermissionKeys.Advertisements.Reports, cancellationToken))
                return Forbid();

            return Json(await _service.GetReportsAsync(filter, cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> AdvertisementDetails(
            int id,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.View, cancellationToken))
                return Forbid();

            var model = await _service.GetAdvertisementDetailsAsync(id, userId, cancellationToken);
            return model == null ? NotFound() : Json(model);
        }

        [HttpGet]
        public async Task<IActionResult> PlanForm(
            int? id = null,
            CancellationToken cancellationToken = default)
        {
            if (!await CanAsync(CurrentUserId(), AdminPermissionKeys.Advertisements.ManagePlans, cancellationToken))
                return Forbid();

            var model = await _service.GetPlanFormAsync(id, cancellationToken);
            return model == null ? NotFound() : Json(model);
        }

        [HttpGet]
        public async Task<IActionResult> AdvertisementReportPrint(
            [FromQuery] AdminAdvertisementReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            if (!await CanAsync(CurrentUserId(), AdminPermissionKeys.Advertisements.Reports, cancellationToken))
                return Forbid();

            var model = await _service.GetReportsAsync(filter, cancellationToken);
            return View("AdvertisementReportPrint", model);
        }
        [HttpGet]
        public async Task<IActionResult> AdvertisementReportPdf(
            [FromQuery] AdminAdvertisementReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            if (!await CanAsync(CurrentUserId(), AdminPermissionKeys.Advertisements.Reports, cancellationToken))
                return Forbid();

            var model = await _service.GetReportsAsync(filter, cancellationToken);
            ViewData["AutoPrint"] = true;
            ViewData["PdfMode"] = true;
            return View("AdvertisementReportPrint", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePlan(
            AdminAdvertisementPlanUpsertDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.ManagePlans, cancellationToken))
                return ForbiddenJson();

            if (!ModelState.IsValid)
                return BadRequestJson(FirstModelError());

            return Operation(await _service.SavePlanAsync(request, userId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePlanStatus(
            AdminAdvertisementPlanStatusDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.ManagePlans, cancellationToken))
                return ForbiddenJson();

            return Operation(await _service.ChangePlanStatusAsync(request, userId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlan(
            int advertisementPlanId,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.ManagePlans, cancellationToken))
                return ForbiddenJson();

            return Operation(await _service.DeletePlanAsync(advertisementPlanId, userId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewPayment(
            AdminAdvertisementPaymentReviewDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.Review, cancellationToken))
                return ForbiddenJson();

            if (!ModelState.IsValid)
                return BadRequestJson(FirstModelError());

            return Operation(await _service.ReviewPaymentAsync(request, userId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewAdvertisement(
            AdminAdvertisementReviewDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.Review, cancellationToken))
                return ForbiddenJson();

            if (!ModelState.IsValid)
                return BadRequestJson(FirstModelError());

            return Operation(await _service.ReviewAdvertisementAsync(request, userId, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeAdvertisementStatus(
            AdminAdvertisementControlDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.Control, cancellationToken))
                return ForbiddenJson();

            if (!ModelState.IsValid)
                return BadRequestJson(FirstModelError());

            return Operation(await _service.ChangeAdvertisementStatusAsync(request, userId, cancellationToken));
        }

        private IActionResult Operation(AdminAdvertisementOperationResult result) =>
            StatusCode(result.Success ? 200 : 400, new
            {
                success = result.Success,
                message = result.Message,
                entityId = result.EntityId
            });

        private IActionResult ForbiddenJson() =>
            StatusCode(403, new
            {
                success = false,
                message = "ليس لديك صلاحية تنفيذ هذه العملية."
            });

        private IActionResult BadRequestJson(string message) =>
            BadRequest(new
            {
                success = false,
                message
            });

        private async Task<bool> CanAsync(
            int userId,
            string permission,
            CancellationToken cancellationToken) =>
            userId > 0 &&
            await _permissions.HasPermissionAsync(
                userId,
                permission,
                cancellationToken);

        private int CurrentUserId() =>
            int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId)
                ? userId
                : 0;

        private string FirstModelError() =>
            ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? "راجع البيانات المدخلة وحاول مرة أخرى.";
    }
}
