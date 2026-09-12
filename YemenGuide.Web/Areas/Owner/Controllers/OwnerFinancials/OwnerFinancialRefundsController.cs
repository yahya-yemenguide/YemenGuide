using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Data;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Owner.Controllers.OwnerFinancials
{
    [Area("Owner")]
    [Authorize]
    [AuthorizeActiveOwner]
    [Route("Owner/Financials/Refunds")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public sealed class OwnerFinancialRefundsController
        : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Owner/Views/OwnerFinancials/" +
            "_OwnerFinancialRefunds.cshtml";

        private const long MaximumRefundProofSize =
            5 * 1024 * 1024;

        private static readonly HashSet<string>
            AllowedRefundProofExtensions =
                new(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".webp"
                };

        private readonly IOwnerFinancialService
            _ownerFinancialService;

        private readonly AppDbContext
            _context;

        private readonly IWebHostEnvironment
            _environment;

        private readonly ILogger<
            OwnerFinancialRefundsController
        > _logger;

        public OwnerFinancialRefundsController(
            IOwnerFinancialService ownerFinancialService,
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<
                OwnerFinancialRefundsController
            > logger)
        {
            _ownerFinancialService =
                ownerFinancialService ??
                throw new ArgumentNullException(
                    nameof(ownerFinancialService));

            _context =
                context ??
                throw new ArgumentNullException(
                    nameof(context));

            _environment =
                environment ??
                throw new ArgumentNullException(
                    nameof(environment));

            _logger =
                logger ??
                throw new ArgumentNullException(
                    nameof(logger));
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(
            [FromQuery] RefundFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??=
                new RefundFilterDto();

            NormalizeFilter(
                filter);

            if (!IsAjaxRequest())
            {
                return Redirect(
                    BuildFinancialPageUrl(
                        filter));
            }

            if (!TryGetCurrentOwnerId(
                    out var ownerId))
            {
                return Unauthorized(
                    "تعذر تحديد حساب المالك الحالي.");
            }

            try
            {
                filter.PlaceId =
                    await ResolveActivePlaceIdAsync(
                        ownerId,
                        filter.PlaceId,
                        cancellationToken);

                var model =
                    await _ownerFinancialService
                        .GetRefundsAsync(
                            ownerId,
                            filter,
                            cancellationToken);

                return PartialView(
                    PartialViewPath,
                    model);
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تحميل طلبات الاسترداد " +
                    "للمالك {OwnerId}.",
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    "تعذر تحميل طلبات الاسترداد حاليًا.");
            }
        }

        [HttpPost("MarkTransferred")]
        [ValidateAntiForgeryToken]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult>
            MarkTransferred(
                [FromForm]
                MarkRefundTransferredRequestDto? request,
                IFormFile? proofFile,
                CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentOwnerId(
                    out var ownerId))
            {
                return Unauthorized(
                    new
                    {
                        success = false,
                        message =
                            "تعذر تحديد حساب المالك الحالي."
                    });
            }

            if (request == null)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "بيانات تحويل الاسترداد غير مكتملة."
                    });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(
                    BuildValidationResponse());
            }

            try
            {
                var saveResult =
                    await SaveRefundProofAsync(
                        ownerId,
                        proofFile,
                        cancellationToken);

                if (!saveResult.Success)
                {
                    return BadRequest(
                        new
                        {
                            success = false,
                            message =
                                saveResult.Message
                        });
                }

                var result =
                    await _ownerFinancialService
                        .MarkRefundTransferredAsync(
                            ownerId,
                            request,
                            saveResult.RelativePath!,
                            cancellationToken);

                if (!result.Success)
                {
                    DeletePhysicalFileIfExists(
                        saveResult.PhysicalPath);

                    return BadRequest(
                        new
                        {
                            success = false,
                            message =
                                result.Message
                        });
                }

                return Ok(
                    new
                    {
                        success = true,
                        message =
                            result.Message,
                        refundRequestId =
                            result.EntityId,
                        referenceNumber =
                            result.ReferenceNumber
                    });
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر تسجيل تحويل الاسترداد " +
                    "{RefundRequestId} للمالك {OwnerId}.",
                    request.RefundRequestId,
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "تعذر تسجيل تحويل الاسترداد حاليًا."
                    });
            }
        }

        [HttpGet("Proof/{id:int}")]
        public async Task<IActionResult> Proof(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized();
            }

            if (id <= 0)
            {
                return NotFound();
            }

            var proofPath =
                await _context.RefundRequests
                    .AsNoTracking()
                    .Where(item =>
                        item.RefundRequestId == id &&
                        item.Place.OwnerId == ownerId)
                    .Select(item =>
                        item.ProofImage)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (string.IsNullOrWhiteSpace(proofPath))
            {
                return NotFound();
            }

            var webRootPath =
                string.IsNullOrWhiteSpace(
                    _environment.WebRootPath)
                    ? Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot")
                    : _environment.WebRootPath;

            var normalizedRelativePath =
                proofPath
                    .Trim()
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar)
                    .Replace(
                        '\\',
                        Path.DirectorySeparatorChar)
                    .TrimStart(
                        Path.DirectorySeparatorChar);

            var fullWebRoot =
                Path.GetFullPath(
                    webRootPath);

            var physicalPath =
                Path.GetFullPath(
                    Path.Combine(
                        fullWebRoot,
                        normalizedRelativePath));

            if (!physicalPath.StartsWith(
                    fullWebRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !System.IO.File.Exists(
                    physicalPath))
            {
                return NotFound();
            }

            var extension =
                Path.GetExtension(
                        physicalPath)
                    .ToLowerInvariant();

            var contentType =
                extension switch
                {
                    ".jpg" or ".jpeg" =>
                        "image/jpeg",
                    ".png" =>
                        "image/png",
                    ".webp" =>
                        "image/webp",
                    _ =>
                        "application/octet-stream"
                };

            Response.Headers[
                "Cache-Control"] =
                "private, no-store, max-age=0";

            return PhysicalFile(
                physicalPath,
                contentType);
        }

        private void NormalizeFilter(
            RefundFilterDto filter)
        {
            if (
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue &&
                filter.FromDate.Value.Date >
                filter.ToDate.Value.Date
            )
            {
                var temporaryDate =
                    filter.FromDate;

                filter.FromDate =
                    filter.ToDate;

                filter.ToDate =
                    temporaryDate;

                ModelState.Remove(
                    nameof(
                        RefundFilterDto
                            .FromDate));

                ModelState.Remove(
                    nameof(
                        RefundFilterDto
                            .ToDate));
            }

            filter.Status =
                NormalizeText(
                    filter.Status);

            filter.Search =
                NormalizeText(
                    filter.Search);

            filter.PageNumber =
                Math.Max(
                    1,
                    filter.PageNumber);

            if (
                filter.PageSize != 10 &&
                filter.PageSize != 25 &&
                filter.PageSize != 50
            )
            {
                filter.PageSize = 25;
            }
        }

        private async Task<RefundProofSaveResult>
            SaveRefundProofAsync(
                int ownerId,
                IFormFile? file,
                CancellationToken cancellationToken)
        {
            if (
                file == null ||
                file.Length <= 0
            )
            {
                return RefundProofSaveResult.Fail(
                    "ارفع صورة إثبات تحويل الاسترداد.");
            }

            if (file.Length >
                MaximumRefundProofSize)
            {
                return RefundProofSaveResult.Fail(
                    "حجم إثبات التحويل يجب ألا يتجاوز 5 ميجابايت.");
            }

            var extension =
                Path.GetExtension(
                        file.FileName)
                    ?.ToLowerInvariant()
                ??
                string.Empty;

            if (!AllowedRefundProofExtensions
                    .Contains(
                        extension))
            {
                return RefundProofSaveResult.Fail(
                    "صيغة الإثبات غير مدعومة. استخدم JPG أو PNG أو WEBP.");
            }

            var webRootPath =
                string.IsNullOrWhiteSpace(
                    _environment.WebRootPath)
                    ? Path.Combine(
                        Directory
                            .GetCurrentDirectory(),
                        "wwwroot")
                    : _environment.WebRootPath;

            var now =
                DateTime.Now;

            var relativeDirectory =
                Path.Combine(
                    "uploads",
                    "finance",
                    "refunds",
                    ownerId.ToString(
                        CultureInfo.InvariantCulture),
                    now.Year.ToString(
                        CultureInfo.InvariantCulture),
                    now.Month.ToString(
                        "00",
                        CultureInfo.InvariantCulture));

            var physicalDirectory =
                Path.Combine(
                    webRootPath,
                    relativeDirectory);

            Directory.CreateDirectory(
                physicalDirectory);

            var fileName =
                $"refund-{Guid.NewGuid():N}{extension}";

            var physicalPath =
                Path.Combine(
                    physicalDirectory,
                    fileName);

            await using (
                var stream =
                    new FileStream(
                        physicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync: true))
            {
                await file.CopyToAsync(
                    stream,
                    cancellationToken);
            }

            var relativePath =
                "/" +
                Path.Combine(
                        relativeDirectory,
                        fileName)
                    .Replace(
                        "\\",
                        "/");

            return RefundProofSaveResult.Ok(
                relativePath,
                physicalPath);
        }

        private static void
            DeletePhysicalFileIfExists(
                string? physicalPath)
        {
            if (string.IsNullOrWhiteSpace(
                    physicalPath))
            {
                return;
            }

            try
            {
                if (System.IO.File.Exists(
                        physicalPath))
                {
                    System.IO.File.Delete(
                        physicalPath);
                }
            }
            catch
            {
                // تنظيف الملف المؤقت لا يجب أن يكسر العملية المالية.
            }
        }

        private static string BuildFinancialPageUrl(
            RefundFilterDto filter)
        {
            var values =
                new List<
                    KeyValuePair<
                        string,
                        string?>>
                {
                    new(
                        "financeTab",
                        "refunds")
                };

            if (
                filter.BookingId.HasValue &&
                filter.BookingId.Value > 0
            )
            {
                values.Add(
                    new(
                        "bookingId",
                        filter.BookingId.Value
                            .ToString(
                                CultureInfo.InvariantCulture)));
            }

            if (
                filter.CustomerId.HasValue &&
                filter.CustomerId.Value > 0
            )
            {
                values.Add(
                    new(
                        "customerId",
                        filter.CustomerId.Value
                            .ToString(
                                CultureInfo.InvariantCulture)));
            }

            AddText(
                values,
                "status",
                filter.Status);

            AddDate(
                values,
                "fromDate",
                filter.FromDate);

            AddDate(
                values,
                "toDate",
                filter.ToDate);

            AddText(
                values,
                "search",
                filter.Search);

            if (filter.PageNumber > 1)
            {
                values.Add(
                    new(
                        "pageNumber",
                        filter.PageNumber.ToString(
                            CultureInfo.InvariantCulture)));
            }

            if (filter.PageSize != 25)
            {
                values.Add(
                    new(
                        "pageSize",
                        filter.PageSize.ToString(
                            CultureInfo.InvariantCulture)));
            }

            return
                "/Owner/Financials" +
                QueryString
                    .Create(values)
                    .ToUriComponent();
        }

        private static void AddDate(
            ICollection<
                KeyValuePair<
                    string,
                    string?>> values,
            string key,
            DateTime? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            values.Add(
                new(
                    key,
                    value.Value.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture)));
        }

        private static void AddText(
            ICollection<
                KeyValuePair<
                    string,
                    string?>> values,
            string key,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return;
            }

            values.Add(
                new(
                    key,
                    value.Trim()));
        }

        private static string? NormalizeText(
            string? value)
        {
            return string.IsNullOrWhiteSpace(
                    value)
                ? null
                : value.Trim();
        }

        private object BuildValidationResponse()
        {
            var errors =
                ModelState
                    .Where(item =>
                        item.Value != null &&
                        item.Value.Errors.Count > 0)
                    .ToDictionary(
                        item =>
                            item.Key,
                        item =>
                            item.Value!
                                .Errors
                                .Select(error =>
                                    string.IsNullOrWhiteSpace(
                                        error.ErrorMessage)
                                        ? "القيمة المدخلة غير صحيحة."
                                        : error.ErrorMessage)
                                .ToArray());

            var firstMessage =
                errors
                    .SelectMany(item =>
                        item.Value)
                    .FirstOrDefault()
                ??
                "تحقق من البيانات المدخلة.";

            return new
            {
                success = false,
                message = firstMessage,
                errors
            };
        }

        private Task<int?>
            ResolveActivePlaceIdAsync(
                int ownerId,
                int? requestedPlaceId,
                CancellationToken cancellationToken)
        {
            return _ownerFinancialService
                .ResolveActivePlaceIdAsync(
                    ownerId,
                    requestedPlaceId,
                    GetCookiePlaceId(ownerId),
                    cancellationToken);
        }

        private int? GetCookiePlaceId(
            int ownerId)
        {
            var cookieName =
                $"ownerActivePlace_{ownerId}";

            return int.TryParse(
                       Request.Cookies[cookieName],
                       out var placeId) &&
                   placeId > 0
                ? placeId
                : null;
        }

        private bool TryGetCurrentOwnerId(
            out int ownerId)
        {
            ownerId = 0;

            return int.TryParse(
                       User.FindFirstValue(
                           ClaimTypes.NameIdentifier),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out ownerId) &&
                   ownerId > 0;
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(
                Request.Headers[
                    "X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }

        private sealed class RefundProofSaveResult
        {
            public bool Success { get; init; }

            public string Message { get; init; } =
                string.Empty;

            public string? RelativePath { get; init; }

            public string? PhysicalPath { get; init; }

            public static RefundProofSaveResult Ok(
                string relativePath,
                string physicalPath)
            {
                return new RefundProofSaveResult
                {
                    Success = true,
                    RelativePath =
                        relativePath,
                    PhysicalPath =
                        physicalPath
                };
            }

            public static RefundProofSaveResult Fail(
                string message)
            {
                return new RefundProofSaveResult
                {
                    Success = false,
                    Message =
                        message
                };
            }
        }
    }
}
