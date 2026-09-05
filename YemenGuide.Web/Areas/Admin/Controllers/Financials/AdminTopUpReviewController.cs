using System;
using System.Linq;
using System.Security.Claims;
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
    [RequireAdminPermission(AdminPermissionKeys.Finance.ReviewTopUps)]
    [Route("Admin/Financials/TopUps")]
    public sealed class AdminTopUpReviewController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminTopUpReview.cshtml";

        private const string DocumentPrintViewPath =
            "~/Areas/Admin/Views/AdminFinancials/AdminFinancialDocumentPrint.cshtml";

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly ILogger<AdminTopUpReviewController> _logger;

        public AdminTopUpReviewController(
            IAdminFinancialService adminFinancialService,
            ILogger<AdminTopUpReviewController> logger)
        {
            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            [FromQuery] AdminCommissionTopUpFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminCommissionTopUpFilterDto();

            try
            {
                DisableResponseCaching();

                var model =
                    await _adminFinancialService.GetTopUpsAsync(
                        filter,
                        cancellationToken);

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
                    "حدث خطأ أثناء تحميل طلبات شحن رصيد الضمان.");

                return StatusCode(
                    500,
                    "تعذر تحميل طلبات الشحن. أعد المحاولة.");
            }
        }

        [HttpGet("Details/{commissionTransactionId:int}")]
        [HttpGet("Details")]
        public async Task<IActionResult> Details(
            int commissionTransactionId,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم طلب الشحن غير صحيح."
                });
            }

            try
            {
                DisableResponseCaching();

                var details =
                    await _adminFinancialService.GetTopUpDetailsAsync(
                        commissionTransactionId,
                        cancellationToken);

                if (details == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "طلب الشحن غير موجود."
                    });
                }

                return Json(new
                {
                    success = true,
                    data = details
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
                    "حدث خطأ أثناء تحميل تفاصيل طلب الشحن {TopUpId}.",
                    commissionTransactionId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تحميل تفاصيل طلب الشحن."
                });
            }
        }

        [HttpGet("Proof/{commissionTransactionId:int}")]
        public async Task<IActionResult> Proof(
            int commissionTransactionId,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return BadRequest("رقم طلب الشحن غير صحيح.");
            }

            try
            {
                DisableResponseCaching();

                var details =
                    await _adminFinancialService.GetTopUpDetailsAsync(
                        commissionTransactionId,
                        cancellationToken);

                if (details == null)
                {
                    return NotFound("طلب الشحن غير موجود.");
                }

                var receiptPath = details.ReceiptImage?.Trim();

                if (string.IsNullOrWhiteSpace(receiptPath))
                {
                    return NotFound("لا يوجد سند تحويل مرفق بهذا الطلب.");
                }

                if (!IsSafeLocalPath(receiptPath))
                {
                    _logger.LogWarning(
                        "تم رفض مسار سند غير محلي لطلب الشحن {TopUpId}: {ReceiptPath}",
                        commissionTransactionId,
                        receiptPath);

                    return BadRequest("مسار سند التحويل غير صالح.");
                }

                return LocalRedirect(receiptPath);
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
                    "حدث خطأ أثناء فتح سند طلب الشحن {TopUpId}.",
                    commissionTransactionId);

                return StatusCode(
                    500,
                    "تعذر فتح سند التحويل.");
            }
        }

        /// <summary>
        /// إيصال اعتماد الشحن متاح لموظف مراجعة الشحنات نفسه، ولا يتطلب
        /// صلاحية التقارير لأن الإيصال جزء مباشر من عملية الاعتماد.
        /// </summary>
        [HttpGet("Receipt/{commissionTransactionId:int}")]
        public async Task<IActionResult> Receipt(
            int commissionTransactionId,
            [FromQuery] bool autoPrint = false,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return BadRequest("رقم طلب الشحن غير صحيح.");
            }

            try
            {
                DisableResponseCaching();

                var document =
                    await _adminFinancialService.GetFinancialDocumentAsync(
                        AdminFinancialDocumentTypes.TopUpReceipt,
                        commissionTransactionId,
                        User.Identity?.Name,
                        cancellationToken);

                if (document == null)
                {
                    return NotFound(
                        "لا يمكن إصدار إيصال قبل اعتماد طلب الشحن.");
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
                    "حدث خطأ أثناء تجهيز إيصال طلب الشحن {TopUpId}.",
                    commissionTransactionId);

                return StatusCode(
                    500,
                    "تعذر تجهيز إيصال طلب الشحن.");
            }
        }

        [HttpPost("Approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            [FromForm] AdminApproveTopUpRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات اعتماد طلب الشحن غير مكتملة."
                });
            }

            request.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote)
                ? null
                : request.ReviewNote.Trim();

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.ApproveTopUpAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var receiptUrl = result.Success
                    ? Url.Action(
                        nameof(Receipt),
                        values: new
                        {
                            commissionTransactionId = request.CommissionTransactionId,
                            autoPrint = false
                        })
                    : null;

                var printUrl = result.Success
                    ? Url.Action(
                        nameof(Receipt),
                        values: new
                        {
                            commissionTransactionId = request.CommissionTransactionId,
                            autoPrint = true
                        })
                    : null;

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    referenceNumber = result.ReferenceNumber,
                    availableBalance = result.AvailableBalance,
                    heldBalance = result.HeldBalance,
                    debtAmount = result.DebtAmount,
                    receiptUrl,
                    printUrl
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
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
                    "حدث خطأ أثناء اعتماد طلب الشحن {TopUpId} بواسطة الأدمن {AdminUserId}.",
                    request.CommissionTransactionId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر اعتماد طلب الشحن. لم يتم تنفيذ أي تغيير مالي."
                });
            }
        }

        [HttpPost("Reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            [FromForm] AdminRejectTopUpRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات رفض طلب الشحن غير مكتملة."
                });
            }

            request.RejectionReason =
                request.RejectionReason?.Trim() ?? string.Empty;

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.RejectTopUpAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    referenceNumber = result.ReferenceNumber
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
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
                    "حدث خطأ أثناء رفض طلب الشحن {TopUpId} بواسطة الأدمن {AdminUserId}.",
                    request.CommissionTransactionId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر رفض طلب الشحن. أعد المحاولة."
                });
            }
        }

        private static bool IsSafeLocalPath(string path)
        {
            return path.StartsWith("/", StringComparison.Ordinal) &&
                   !path.StartsWith("//", StringComparison.Ordinal) &&
                   !Uri.TryCreate(path, UriKind.Absolute, out _);
        }

        private bool TryGetCurrentAdminUserId(out int adminUserId)
        {
            adminUserId = 0;

            var rawValue =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("UserID") ??
                User.FindFirstValue("UserId") ??
                User.FindFirstValue("user_id") ??
                User.FindFirstValue("sub");

            return int.TryParse(rawValue, out adminUserId) &&
                   adminUserId > 0;
        }

        private string FirstModelError()
        {
            return ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error =>
                    !string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.ErrorMessage
                        : error.Exception?.Message)
                .FirstOrDefault(message =>
                    !string.IsNullOrWhiteSpace(message)) ??
                "البيانات المرسلة غير صحيحة.";
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
