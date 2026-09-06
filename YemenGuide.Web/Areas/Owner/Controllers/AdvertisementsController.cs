using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Web.Services.Advertisements;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner")]
    public sealed class AdvertisementsController : Controller
    {
        private const long MaximumImageBytes = 10L * 1024L * 1024L;
        private const long MaximumVideoBytes = 25L * 1024L * 1024L;
        private const long MaximumReceiptBytes = 8L * 1024L * 1024L;
        private const long MaximumVideoRequestBytes = 27L * 1024L * 1024L;
        private const long MaximumReceiptRequestBytes = 10L * 1024L * 1024L;

        private static readonly HashSet<string> ImageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        private static readonly HashSet<string> VideoExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };

        private readonly IOwnerAdvertisementService _advertisementService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AdvertisementsController> _logger;

        public AdvertisementsController(
            IOwnerAdvertisementService advertisementService,
            IWebHostEnvironment environment,
            ILogger<AdvertisementsController> logger)
        {
            _advertisementService = advertisementService;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> AdvertisementManagement(
            string? tab = null,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0)
            {
                return Challenge();
            }

            var model = await _advertisementService.GetManagementPageAsync(
                ownerId,
                GetRequestedPlaceId(ownerId),
                tab,
                cancellationToken);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OverviewTab(
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return Challenge();

            var model = await _advertisementService.GetOverviewAsync(
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return Json(model);
        }

        [HttpGet]
        public async Task<IActionResult> AdvertisementsTab(
            [FromQuery] OwnerAdvertisementFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return Challenge();

            var model = await _advertisementService.GetAdvertisementsAsync(
                filter,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return Json(model);
        }

        [HttpGet]
        public async Task<IActionResult> Wizard(
            int? id = null,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return Challenge();

            var model = await _advertisementService.GetWizardAsync(
                id,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);

            if (id.HasValue && !model.Advertisement.AdvertisementId.HasValue)
            {
                return NotFound();
            }

            return Json(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PriceQuote(
            OwnerAdvertisementPriceQuoteRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (!ModelState.IsValid) return ValidationJson();

            var result = await _advertisementService.GetPriceQuoteAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return StatusCode(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, new
            {
                success = result.Success,
                message = result.Message,
                advertisementPlanId = result.AdvertisementPlanId,
                billingModel = result.BillingModel,
                billingModelLabel = result.BillingModelLabel,
                finalAmount = result.FinalAmount,
                dailyAmount = result.DailyAmount,
                estimatedClicks = result.EstimatedClicks,
                estimatedImpressions = result.EstimatedImpressions,
                finalAmountText = result.FinalAmountText
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(
            int id,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return Challenge();

            var model = await _advertisementService.GetDetailsAsync(
                id,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return model == null
                ? NotFound()
                : Json(model);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentForm(
            int id,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return Challenge();

            var details = await _advertisementService.GetDetailsAsync(
                id,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            if (details == null)
            {
                return NotFound();
            }

            var wizard = await _advertisementService.GetWizardAsync(
                id,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return Json(new
            {
                details,
                wallets = wizard.PlatformWallets
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDraft(
            OwnerAdvertisementSaveDto request,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (!ModelState.IsValid) return ValidationJson();

            var result = await _advertisementService.SaveDraftAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return OperationResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            OwnerAdvertisementSubmitDto request,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (!ModelState.IsValid) return ValidationJson();

            var result = await _advertisementService.SubmitAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return OperationResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = MaximumReceiptRequestBytes)]
        [RequestSizeLimit(MaximumReceiptRequestBytes)]
        public async Task<IActionResult> SavePaymentProof(
            OwnerAdvertisementPaymentSaveDto request,
            IFormFile? receiptImage,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");

            // لا نثق بأي مسار ملف قادم من المتصفح؛ المسار يُنشأ داخل الخادم فقط.
            request.ReceiptImagePath = null;
            string? newPath = null;
            if (receiptImage != null && receiptImage.Length > 0)
            {
                var saved = await SaveUploadedFileAsync(
                    receiptImage,
                    ownerId,
                    request.AdvertisementId,
                    UploadKind.Receipt,
                    cancellationToken);
                if (!saved.Success)
                {
                    return BadRequest(new { success = false, message = saved.Message });
                }

                newPath = saved.RelativePath;
                request.ReceiptImagePath = newPath;
            }

            if (!ModelState.IsValid)
            {
                DeletePhysicalFile(newPath);
                return ValidationJson();
            }

            var result = await _advertisementService.SavePaymentProofAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            if (!result.Success)
            {
                DeletePhysicalFile(newPath);
            }
            else if (!string.IsNullOrWhiteSpace(result.AffectedFilePath))
            {
                DeletePhysicalFile(result.AffectedFilePath);
            }

            return OperationResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = MaximumVideoRequestBytes)]
        [RequestSizeLimit(MaximumVideoRequestBytes)]
        public async Task<IActionResult> UploadMedia(
            int advertisementId,
            string rowVersion,
            IFormFile? file,
            bool isPrimary = false,
            string? altText = null,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (advertisementId <= 0 || string.IsNullOrWhiteSpace(rowVersion) || file == null)
            {
                return BadRequest(new { success = false, message = "بيانات رفع الوسائط غير مكتملة." });
            }

            var kind = IsVideoExtension(Path.GetExtension(file.FileName))
                ? UploadKind.Video
                : UploadKind.Image;
            var saved = await SaveUploadedFileAsync(
                file,
                ownerId,
                advertisementId,
                kind,
                cancellationToken);
            if (!saved.Success)
            {
                return BadRequest(new { success = false, message = saved.Message });
            }

            var request = new OwnerAdvertisementMediaSaveDto
            {
                AdvertisementId = advertisementId,
                RowVersion = rowVersion,
                FilePath = saved.RelativePath!,
                MediaType = kind == UploadKind.Video
                    ? OwnerAdvertisementMediaTypes.Video
                    : OwnerAdvertisementMediaTypes.Image,
                IsPrimary = isPrimary,
                AltText = altText
            };

            var result = await _advertisementService.AddMediaAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            if (!result.Success)
            {
                DeletePhysicalFile(saved.RelativePath);
            }

            return OperationResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMedia(
            OwnerAdvertisementMediaDeleteDto request,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (!ModelState.IsValid) return ValidationJson();

            var result = await _advertisementService.DeleteMediaAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            if (result.Success)
            {
                DeletePhysicalFile(result.AffectedFilePath);
            }

            return OperationResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Pause(
            OwnerAdvertisementStateChangeDto request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteStateActionAsync(request, StateAction.Pause, cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Resume(
            OwnerAdvertisementStateChangeDto request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteStateActionAsync(request, StateAction.Resume, cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Cancel(
            OwnerAdvertisementStateChangeDto request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteStateActionAsync(request, StateAction.Cancel, cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(
            OwnerAdvertisementStateChangeDto request,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (!ModelState.IsValid) return ValidationJson();

            var result = await _advertisementService.RestoreAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return OperationResponse(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Renew(
            OwnerAdvertisementRenewDto request,
            CancellationToken cancellationToken = default)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (!ModelState.IsValid) return ValidationJson();

            var result = await _advertisementService.RenewAsync(
                request,
                ownerId,
                GetRequestedPlaceId(ownerId),
                cancellationToken);
            return OperationResponse(result);
        }

        private async Task<IActionResult> ExecuteStateActionAsync(
            OwnerAdvertisementStateChangeDto request,
            StateAction action,
            CancellationToken cancellationToken)
        {
            var ownerId = GetCurrentUserId();
            if (ownerId <= 0) return ForbiddenJson("تعذر تحديد حساب المالك.");
            if (!ModelState.IsValid) return ValidationJson();

            var placeId = GetRequestedPlaceId(ownerId);
            var result = action switch
            {
                StateAction.Pause => await _advertisementService.PauseAsync(request, ownerId, placeId, cancellationToken),
                StateAction.Resume => await _advertisementService.ResumeAsync(request, ownerId, placeId, cancellationToken),
                _ => await _advertisementService.CancelAsync(request, ownerId, placeId, cancellationToken)
            };
            return OperationResponse(result);
        }

        private async Task<FileSaveResult> SaveUploadedFileAsync(
            IFormFile file,
            int ownerId,
            int advertisementId,
            UploadKind kind,
            CancellationToken cancellationToken)
        {
            if (file.Length <= 0)
            {
                return FileSaveResult.Fail("الملف فارغ.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var maximumSize = kind switch
            {
                UploadKind.Video => MaximumVideoBytes,
                UploadKind.Receipt => MaximumReceiptBytes,
                _ => MaximumImageBytes
            };
            if (file.Length > maximumSize)
            {
                return FileSaveResult.Fail(kind == UploadKind.Video
                    ? "حجم الفيديو يتجاوز 25 ميجابايت."
                    : kind == UploadKind.Receipt
                        ? "حجم إثبات الدفع يتجاوز 8 ميجابايت."
                        : "حجم الصورة يتجاوز 10 ميجابايت.");
            }

            var allowedExtension = kind == UploadKind.Video
                ? VideoExtensions.Contains(extension)
                : ImageExtensions.Contains(extension);
            if (!allowedExtension || !await HasValidSignatureAsync(file, kind, cancellationToken))
            {
                return FileSaveResult.Fail(kind == UploadKind.Video
                    ? "صيغة الفيديو غير مدعومة. استخدم MP4 أو WebM."
                    : "صيغة الصورة غير مدعومة. استخدم JPG أو PNG أو WebP.");
            }

            var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;
            var section = kind == UploadKind.Receipt ? "receipts" : "media";
            var relativeDirectory = Path.Combine("uploads", "advertisements", ownerId.ToString(), advertisementId.ToString(), section);
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
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to save advertisement upload");
                return FileSaveResult.Fail("تعذر حفظ الملف. حاول مرة أخرى.");
            }

            var relativePath = "/" + Path.Combine(relativeDirectory, fileName).Replace('\\', '/');
            return FileSaveResult.Ok(relativePath);
        }

        private static async Task<bool> HasValidSignatureAsync(
            IFormFile file,
            UploadKind kind,
            CancellationToken cancellationToken)
        {
            var header = new byte[12];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            if (read < 4) return false;

            if (kind == UploadKind.Video)
            {
                var mp4 = read >= 12 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;
                var webm = header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;
                return mp4 || webm;
            }

            var jpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            var png = read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            var webp = read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
            return jpeg || png || webp;
        }

        private void DeletePhysicalFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                !relativePath.StartsWith("/uploads/advertisements/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                    ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                    : _environment.WebRootPath;
                var uploadRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "advertisements"));
                var candidate = Path.GetFullPath(Path.Combine(webRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                if (!candidate.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (System.IO.File.Exists(candidate))
                {
                    System.IO.File.Delete(candidate);
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to delete advertisement file {Path}", relativePath);
            }
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : 0;
        }

        private int? GetRequestedPlaceId(int ownerId)
        {
            var cookieName = $"ownerActivePlace_{ownerId}";
            return int.TryParse(Request.Cookies[cookieName], out var placeId) && placeId > 0
                ? placeId
                : null;
        }

        private IActionResult ValidationJson()
        {
            return BadRequest(new
            {
                success = false,
                message = FirstModelError()
            });
        }

        private IActionResult ForbiddenJson(string message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message });
        }

        private IActionResult OperationResponse(OwnerAdvertisementOperationResultDto result)
        {
            var response = new
            {
                success = result.Success,
                message = result.Message,
                advertisementId = result.AdvertisementId,
                status = result.Status,
                paymentStatus = result.PaymentStatus,
                rowVersion = result.RowVersion,
                expectedPaymentAmount = result.ExpectedPaymentAmount,
                expectedPaymentText = result.ExpectedPaymentText
            };
            return StatusCode(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, response);
        }

        private string FirstModelError()
        {
            return ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? "راجع البيانات المدخلة وحاول مرة أخرى.";
        }

        private static bool IsVideoExtension(string? extension)
        {
            return !string.IsNullOrWhiteSpace(extension) && VideoExtensions.Contains(extension);
        }

        private enum UploadKind
        {
            Image,
            Video,
            Receipt
        }

        private enum StateAction
        {
            Pause,
            Resume,
            Cancel
        }

        private sealed class FileSaveResult
        {
            public bool Success { get; init; }
            public string Message { get; init; } = string.Empty;
            public string? RelativePath { get; init; }

            public static FileSaveResult Ok(string path)
            {
                return new FileSaveResult { Success = true, RelativePath = path };
            }

            public static FileSaveResult Fail(string message)
            {
                return new FileSaveResult { Success = false, Message = message };
            }
        }
    }
}