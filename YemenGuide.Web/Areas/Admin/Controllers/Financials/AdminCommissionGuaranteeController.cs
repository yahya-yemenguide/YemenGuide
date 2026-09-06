using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Data;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Admin.Controllers.Financials
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [RequireAdminPermission(AdminPermissionKeys.Finance.View)]
    [Route("Admin/Financials/CommissionGuarantee")]
    public sealed class AdminCommissionGuaranteeController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminCommissionGuarantee.cshtml";

        private readonly AppDbContext _context;
        private readonly IAdminFinancialService _adminFinancialService;
        private readonly IAdminPermissionService _adminPermissionService;
        private readonly ILogger<AdminCommissionGuaranteeController> _logger;

        public AdminCommissionGuaranteeController(
            AppDbContext context,
            IAdminFinancialService adminFinancialService,
            IAdminPermissionService adminPermissionService,
            ILogger<AdminCommissionGuaranteeController> logger)
        {
            _context = context ??
                throw new ArgumentNullException(nameof(context));

            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _adminPermissionService = adminPermissionService ??
                throw new ArgumentNullException(nameof(adminPermissionService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            [FromQuery] AdminCommissionGuaranteeFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminCommissionGuaranteeFilterDto();

            try
            {
                DisableResponseCaching();

                var model =
                    await _adminFinancialService.GetCommissionGuaranteeAsync(
                        filter,
                        cancellationToken);

                ViewData["CanManageCommissionAccounts"] =
                    await CurrentAdminHasPermissionAsync(
                        AdminPermissionKeys.Finance.ManageCommissionAccounts,
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
                    "حدث خطأ أثناء تحميل تبويب العمولات والضمان.");

                return StatusCode(
                    500,
                    "تعذر تحميل العمولات والضمان. أعد المحاولة.");
            }
        }

        [HttpGet("Details/{placeCommissionAccountId:int}")]
        [HttpGet("Details")]
        public async Task<IActionResult> Details(
            int placeCommissionAccountId,
            CancellationToken cancellationToken = default)
        {
            if (placeCommissionAccountId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم حساب الضمان غير صحيح."
                });
            }

            try
            {
                DisableResponseCaching();

                var details =
                    await _adminFinancialService
                        .GetCommissionGuaranteeDetailsAsync(
                            placeCommissionAccountId,
                            cancellationToken);

                if (details == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "لم يتم العثور على حساب الضمان المطلوب."
                    });
                }

                var canManage =
                    await CurrentAdminHasPermissionAsync(
                        AdminPermissionKeys.Finance.ManageCommissionAccounts,
                        cancellationToken);

                return Json(new
                {
                    success = true,
                    canManageCommissionAccounts = canManage,
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
                    "حدث خطأ أثناء تحميل تفاصيل حساب الضمان {AccountId}.",
                    placeCommissionAccountId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تحميل تفاصيل حساب الضمان."
                });
            }
        }

        [HttpGet("Booking/{bookingId:int}")]
        public async Task<IActionResult> BookingDetails(
            int bookingId,
            CancellationToken cancellationToken = default)
        {
            if (bookingId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم الحجز غير صحيح."
                });
            }

            try
            {
                DisableResponseCaching();

                var details =
                    await _adminFinancialService
                        .GetBookingCommissionDetailsAsync(
                            bookingId,
                            cancellationToken);

                if (details == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "لم يتم العثور على السجل المالي لهذا الحجز."
                    });
                }

                /*
                 * المرحلة الثانية - ربط العروض والخصومات بمالية الأدمن:
                 *
                 * لا نعيد حساب العرض هنا ولا نثق بعرض حيّ قد يكون المالك
                 * عدّله لاحقًا. نقرأ Snapshot الحجز نفسه فقط؛ لأنه يمثل
                 * السعر والعرض اللذين تم تثبيتهما وقت إنشاء الحجز.
                 *
                 * خدمة المالية قامت مسبقًا بتحويل القيم المالية داخل details
                 * إلى عملة العرض المفضلة للأدمن. لذلك نحسب معامل عرض من
                 * TotalPrice المحفوظ ونستخدمه فقط لعرض السعر الأصلي والتوفير.
                 * لا يتم تغيير أي قيمة محاسبية أو عمولة من هذا الكنترولر.
                 */
                var bookingPricing =
                    await _context.Bookings
                        .AsNoTracking()
                        .Where(booking =>
                            booking.BookingId == bookingId)
                        .Select(booking => new
                        {
                            booking.CreatedAt,
                            booking.TotalPrice,
                            booking.OriginalTotalPrice,
                            booking.DiscountAmount,
                            booking.PromotionId,
                            booking.PromotionTitleSnapshot,
                            booking.PromotionCodeSnapshot
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                if (bookingPricing != null)
                {
                    var rawFinalTotal =
                        Math.Max(
                            0m,
                            bookingPricing.TotalPrice);

                    var rawOriginalTotal =
                        bookingPricing.OriginalTotalPrice > 0m
                            ? bookingPricing.OriginalTotalPrice
                            : rawFinalTotal +
                              Math.Max(
                                  0m,
                                  bookingPricing.DiscountAmount);

                    var rawFinalSavings =
                        Math.Max(
                            0m,
                            rawOriginalTotal -
                            rawFinalTotal);

                    var displayScale =
                        rawFinalTotal > 0m
                            ? details.BookingTotalPrice /
                              rawFinalTotal
                            : 1m;

                    var displayOriginalTotal =
                        Math.Round(
                            rawOriginalTotal *
                            displayScale,
                            2);

                    var displayFinalSavings =
                        Math.Round(
                            rawFinalSavings *
                            displayScale,
                            2);

                    var hasPromotion =
                        (bookingPricing.PromotionId.HasValue &&
                         bookingPricing.PromotionId.Value > 0) ||
                        !string.IsNullOrWhiteSpace(
                            bookingPricing.PromotionTitleSnapshot) ||
                        !string.IsNullOrWhiteSpace(
                            bookingPricing.PromotionCodeSnapshot) ||
                        rawFinalSavings > 0m;

                    if (hasPromotion)
                    {
                        var promotionTitle =
                            !string.IsNullOrWhiteSpace(
                                bookingPricing.PromotionTitleSnapshot)
                                ? bookingPricing
                                    .PromotionTitleSnapshot!
                                    .Trim()
                                : "عرض خاص";

                        var promotionCodeText =
                            !string.IsNullOrWhiteSpace(
                                bookingPricing.PromotionCodeSnapshot)
                                ? $" | كود العرض: {bookingPricing.PromotionCodeSnapshot!.Trim()}"
                                : string.Empty;

                        var savingsText =
                            displayFinalSavings > 0m
                                ? $" | التوفير الفعلي: {displayFinalSavings:N2}"
                                : string.Empty;

                        details.Timeline.Insert(
                            0,
                            new AdminBookingCommissionTimelineItemDto
                            {
                                Status = "Promotion",
                                Label =
                                    $"العرض المطبق: {promotionTitle}",
                                OccurredAt =
                                    bookingPricing.CreatedAt,
                                Amount =
                                    displayFinalSavings,
                                Description =
                                    $"السعر قبل العرض: {displayOriginalTotal:N2} ← " +
                                    $"السعر النهائي: {details.BookingTotalPrice:N2}" +
                                    savingsText +
                                    promotionCodeText,
                                IsCompleted = true,
                                IsCurrent = false
                            });

                        /*
                         * توضيح مصدر أساس العمولة فقط.
                         * لا نعدل CommissionBaseAmount ولا PlatformCommission؛
                         * فهما محفوظان أصلًا من الحساب المالي المركزي.
                         */
                        var calculatedStep =
                            details.Timeline.FirstOrDefault(item =>
                                string.Equals(
                                    item.Status,
                                    AdminBookingCommissionStatuses.Calculated,
                                    StringComparison.OrdinalIgnoreCase));

                        if (calculatedStep != null)
                        {
                            var existingDescription =
                                string.IsNullOrWhiteSpace(
                                    calculatedStep.Description)
                                    ? string.Empty
                                    : calculatedStep.Description.Trim() + " ";

                            calculatedStep.Description =
                                existingDescription +
                                $"أساس احتساب العمولة بعد العرض: " +
                                $"{details.CommissionBaseAmount:N2}.";
                        }
                    }
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
                    "حدث خطأ أثناء تحميل تفاصيل عمولة الحجز {BookingId}.",
                    bookingId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تحميل تفاصيل عمولة الحجز."
                });
            }
        }

        [HttpPost("Policy")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManageCommissionAccounts)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePolicy(
            [FromForm] AdminCommissionAccountPolicyUpdateDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات سياسة حساب الضمان غير مكتملة."
                });
            }

            NormalizePolicyRequest(request);
            ValidatePolicyRequest(request);

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
                    await _adminFinancialService
                        .UpdateCommissionAccountPolicyAsync(
                            request,
                            adminUserId,
                            cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    availableBalance = result.AvailableBalance,
                    heldBalance = result.HeldBalance,
                    debtAmount = result.DebtAmount
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
                    "حدث خطأ أثناء تعديل سياسة حساب الضمان {AccountId} بواسطة الأدمن {AdminUserId}.",
                    request.PlaceCommissionAccountId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر حفظ سياسة حساب الضمان. لم يتم تنفيذ أي تغيير مالي."
                });
            }
        }

        [HttpPost("Status")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManageCommissionAccounts)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(
            [FromForm] AdminCommissionAccountStatusUpdateDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات تغيير حالة الحساب غير مكتملة."
                });
            }

            request.TargetStatus = request.TargetStatus?.Trim() ?? string.Empty;
            request.Reason = request.Reason?.Trim() ?? string.Empty;

            if (!IsAllowedManualAccountStatus(request.TargetStatus))
            {
                ModelState.AddModelError(
                    nameof(request.TargetStatus),
                    "الحالة المطلوبة غير مسموحة.");
            }

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
                    await _adminFinancialService
                        .ChangeCommissionAccountStatusAsync(
                            request,
                            adminUserId,
                            cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    availableBalance = result.AvailableBalance,
                    heldBalance = result.HeldBalance,
                    debtAmount = result.DebtAmount,
                    targetStatus = request.TargetStatus
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
                    "حدث خطأ أثناء تغيير حالة حساب الضمان {AccountId} بواسطة الأدمن {AdminUserId}.",
                    request.PlaceCommissionAccountId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تغيير حالة حساب الضمان. لم يتم تنفيذ أي تغيير مالي."
                });
            }
        }

        private static bool IsAllowedManualAccountStatus(string status)
        {
            return string.Equals(
                       status,
                       AdminCommissionAccountStatuses.Active,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       status,
                       AdminCommissionAccountStatuses.Suspended,
                       StringComparison.OrdinalIgnoreCase);
        }

        private void NormalizePolicyRequest(
            AdminCommissionAccountPolicyUpdateDto request)
        {
            request.Reason = request.Reason?.Trim() ?? string.Empty;

            if (request.UseGlobalAllowNegativeBalance)
            {
                request.AllowNegativeBalance = null;
            }

            if (request.UseGlobalDebtLimit)
            {
                request.DebtLimit = null;
            }

            if (request.UseGlobalLowBalanceThreshold)
            {
                request.LowBalanceThreshold = null;
            }
        }

        private void ValidatePolicyRequest(
            AdminCommissionAccountPolicyUpdateDto request)
        {
            if (!request.UseGlobalAllowNegativeBalance &&
                !request.AllowNegativeBalance.HasValue)
            {
                ModelState.AddModelError(
                    nameof(request.AllowNegativeBalance),
                    "حدد هل يسمح الحساب بالدين أم لا.");
            }

            if (!request.UseGlobalDebtLimit &&
                (!request.DebtLimit.HasValue || request.DebtLimit.Value < 0m))
            {
                ModelState.AddModelError(
                    nameof(request.DebtLimit),
                    "حد الدين الخاص غير صحيح.");
            }

            if (!request.UseGlobalLowBalanceThreshold &&
                (!request.LowBalanceThreshold.HasValue ||
                 request.LowBalanceThreshold.Value < 0m))
            {
                ModelState.AddModelError(
                    nameof(request.LowBalanceThreshold),
                    "حد انخفاض الرصيد الخاص غير صحيح.");
            }
        }

        private async Task<bool> CurrentAdminHasPermissionAsync(
            string permissionKey,
            CancellationToken cancellationToken)
        {
            return TryGetCurrentAdminUserId(out var adminUserId) &&
                   await _adminPermissionService.HasPermissionAsync(
                       adminUserId,
                       permissionKey,
                       cancellationToken);
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
