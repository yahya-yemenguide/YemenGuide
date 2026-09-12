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
using YemenGuide.Data;
using YemenGuide.Core.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Owner.Controllers.OwnerFinancials
{
    [Area("Owner")]
    [Authorize(Roles = "Owner,Admin")]
    [AuthorizeActiveOwner]
    [Route("Owner/Financials/Balance")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public sealed class OwnerFinancialBalanceController
        : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Owner/Views/OwnerFinancials/" +
            "_OwnerFinancialBalance.cshtml";

        private readonly IOwnerFinancialService
            _ownerFinancialService;

        private readonly AppDbContext
            _context;

        private readonly ILogger<
            OwnerFinancialBalanceController
        > _logger;

        public OwnerFinancialBalanceController(
            IOwnerFinancialService ownerFinancialService,
            AppDbContext context,
            ILogger<
                OwnerFinancialBalanceController
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

            _logger =
                logger ??
                throw new ArgumentNullException(
                    nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("~/Owner/Financials/CommissionGuarantee")]
        [HttpGet("~/Owner/Financials/BalanceTopUp")]
        [HttpGet("~/Owner/Financials/Transactions")]
        public async Task<IActionResult> Index(
            [FromQuery] CommissionTransactionFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??=
                new CommissionTransactionFilterDto();

            NormalizeFilter(
                filter);

            if (await IsCurrentAdminAccountAsync(cancellationToken))
            {
                if (!IsAjaxRequest())
                {
                    return Redirect(
                        "/Owner/Financials?financeTab=overview");
                }

                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "المنشآت الإدارية معفاة من ضمان وعمولة المنصة ولا تحتاج إلى قسم الرصيد.");
            }

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
                var activePlaceId =
                    await ResolveActivePlaceIdAsync(
                        ownerId,
                        filter.PlaceId,
                        cancellationToken);

                filter.PlaceId =
                    activePlaceId;

                var model =
                    await _ownerFinancialService
                        .GetCommissionBalanceAsync(
                            ownerId,
                            activePlaceId,
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
                    "تعذر تحميل الرصيد والعمولات " +
                    "للمالك {OwnerId}.",
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    "تعذر تحميل الرصيد والعمولات حاليًا.");
            }
        }

        [HttpPost("TopUp/Submit")]
        [HttpPost("~/Owner/Financials/TopUp/Submit")]
        [ValidateAntiForgeryToken]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitTopUp(
            [FromForm] TopUpRequestDto? request,
            IFormFile? receiptFile,
            CancellationToken cancellationToken = default)
        {
            if (await IsCurrentAdminAccountAsync(cancellationToken))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "المنشآت الإدارية معفاة من ضمان وعمولة المنصة ولا تحتاج إلى شحن رصيد."
                    });
            }

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
                            "بيانات طلب الشحن غير مكتملة."
                    });
            }

            if (receiptFile == null ||
                receiptFile.Length <= 0)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "أرفق سند التحويل."
                    });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(
                    BuildValidationResponse());
            }

            try
            {
                var activePlaceId =
                    await ResolveActivePlaceIdAsync(
                        ownerId,
                        request.PlaceId,
                        cancellationToken);

                if (!activePlaceId.HasValue)
                {
                    return BadRequest(
                        new
                        {
                            success = false,
                            message =
                                "لا توجد منشأة محددة للشحن."
                        });
                }

                // المنشأة المختارة من الهيدر هي المصدر النهائي للحقيقة.
                request.PlaceId =
                    activePlaceId.Value;

                var result =
                    await _ownerFinancialService
                        .SubmitTopUpAsync(
                            ownerId,
                            request,
                            receiptFile,
                            cancellationToken);

                if (!result.Success)
                {
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
                        id =
                            result.EntityId,
                        referenceNumber =
                            result.ReferenceNumber,
                        availableBalance =
                            result.AvailableBalance,
                        heldBalance =
                            result.HeldBalance,
                        debtAmount =
                            result.DebtAmount
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
                    "تعذر إرسال طلب شحن الرصيد " +
                    "للمالك {OwnerId}.",
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "تعذر إرسال طلب الشحن حاليًا."
                    });
            }
        }

        [HttpPost("TopUp/Cancel/{id:int}")]
        [HttpPost("~/Owner/Financials/TopUp/Cancel/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTopUp(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (await IsCurrentAdminAccountAsync(cancellationToken))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "المنشآت الإدارية معفاة من ضمان وعمولة المنصة ولا تحتاج إلى شحن رصيد."
                    });
            }

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

            if (id <= 0)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "رقم طلب الشحن غير صحيح."
                    });
            }

            try
            {
                var result =
                    await _ownerFinancialService
                        .CancelPendingTopUpAsync(
                            ownerId,
                            id,
                            cancellationToken);

                if (!result.Success)
                {
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
                        id =
                            result.EntityId
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
                    "تعذر إلغاء طلب الشحن {TransactionId} " +
                    "للمالك {OwnerId}.",
                    id,
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "تعذر إلغاء طلب الشحن حاليًا."
                    });
            }
        }

        private void NormalizeFilter(
            CommissionTransactionFilterDto filter)
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
                        CommissionTransactionFilterDto
                            .FromDate));

                ModelState.Remove(
                    nameof(
                        CommissionTransactionFilterDto
                            .ToDate));
            }

            filter.TransactionType =
                NormalizeText(
                    filter.TransactionType);

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

        private static string BuildFinancialPageUrl(
            CommissionTransactionFilterDto filter)
        {
            var values =
                new List<
                    KeyValuePair<
                        string,
                        string?>>
                {
                    new(
                        "financeTab",
                        "balance")
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

            AddText(
                values,
                "transactionType",
                filter.TransactionType);

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

        private async Task<bool> IsCurrentAdminAccountAsync(
            CancellationToken cancellationToken)
        {
            if (!TryGetCurrentOwnerId(out var userId))
            {
                return false;
            }

            return await _context.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == userId &&
                    user.Role == SystemRoles.Admin,
                    cancellationToken);
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
    }
}
