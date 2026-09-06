using System;
using System.IO;

using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        private const long MaximumRefundProofBytes = 8L * 1024L * 1024L;
        private readonly IAdminAdvertisementService _service;
        private readonly IAdminPermissionService _permissions;
        private readonly IWebHostEnvironment _environment;

        public AdvertisementsController(
            IAdminAdvertisementService service,
            IAdminPermissionService permissions,
            IWebHostEnvironment environment)
        {
            _service = service;
            _permissions = permissions;
            _environment = environment;
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
        [RequestSizeLimit(10L * 1024L * 1024L)]
        public async Task<IActionResult> ProcessRefund(
            [FromForm] AdminAdvertisementRefundDto request,
            IFormFile? refundProof,
            CancellationToken cancellationToken = default)
        {
            var userId = CurrentUserId();
            if (!await CanAsync(userId, AdminPermissionKeys.Advertisements.Control, cancellationToken))
                return ForbiddenJson();

            if (!ModelState.IsValid)
                return BadRequestJson(FirstModelError());

            var proofResult = await SaveRefundProofAsync(
                request.AdvertisementId,
                refundProof,
                cancellationToken);

            if (!proofResult.Success)
                return BadRequestJson(proofResult.Message);

            request.RefundProofImage = proofResult.RelativePath;

            var result = await _service.ProcessRefundAsync(request, userId, cancellationToken);
            if (!result.Success)
            {
                DeleteRefundProof(proofResult.RelativePath);
            }

            return Operation(result);
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

        private async Task<RefundProofSaveResult> SaveRefundProofAsync(
            int advertisementId,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (advertisementId <= 0)
                return RefundProofSaveResult.Fail("الإعلان غير صالح.");

            if (file == null || file.Length <= 0)
                return RefundProofSaveResult.Fail("ارفع صورة إثبات تحويل مبلغ الاسترداد.");

            if (file.Length > MaximumRefundProofBytes)
                return RefundProofSaveResult.Fail("حجم إثبات الاسترداد يجب ألا يتجاوز 8 ميجابايت.");

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                return RefundProofSaveResult.Fail("صيغة إثبات الاسترداد يجب أن تكون JPG أو PNG أو WEBP.");

            if (!await HasValidImageSignatureAsync(file, cancellationToken))
                return RefundProofSaveResult.Fail("ملف إثبات الاسترداد ليس صورة صالحة.");

            var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;

            var relativeDirectory = Path.Combine(
                "uploads",
                "advertisements",
                "refunds",
                advertisementId.ToString());
            var physicalDirectory = Path.Combine(webRoot, relativeDirectory);
            Directory.CreateDirectory(physicalDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(physicalDirectory, fileName);

            try
            {
                await using var stream = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);
                await file.CopyToAsync(stream, cancellationToken);
            }
            catch
            {
                return RefundProofSaveResult.Fail("تعذر حفظ إثبات الاسترداد. حاول مرة أخرى.");
            }

            var relativePath = "/" + Path.Combine(relativeDirectory, fileName).Replace('\\', '/');
            return RefundProofSaveResult.Ok(relativePath);
        }

        private static async Task<bool> HasValidImageSignatureAsync(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var header = new byte[12];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            if (read < 4) return false;

            var jpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            var png = read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            var webp = read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
            return jpeg || png || webp;
        }

        private void DeleteRefundProof(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                !relativePath.StartsWith("/uploads/advertisements/refunds/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                    ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                    : _environment.WebRootPath;
                var uploadRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "advertisements", "refunds"));
                var candidate = Path.GetFullPath(Path.Combine(
                    webRoot,
                    relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

                if (candidate.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    System.IO.File.Exists(candidate))
                {
                    System.IO.File.Delete(candidate);
                }
            }
            catch
            {
                // لا نفشل طلب المستخدم فقط لأن تنظيف ملف غير مستخدم تعذر.
            }
        }

        private sealed class RefundProofSaveResult
        {
            public bool Success { get; private init; }
            public string Message { get; private init; } = string.Empty;
            public string? RelativePath { get; private init; }

            public static RefundProofSaveResult Ok(string relativePath) =>
                new() { Success = true, RelativePath = relativePath };

            public static RefundProofSaveResult Fail(string message) =>
                new() { Success = false, Message = message };
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
