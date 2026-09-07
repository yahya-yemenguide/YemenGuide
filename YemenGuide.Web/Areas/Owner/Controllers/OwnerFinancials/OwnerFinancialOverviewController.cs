using System;
using System.Collections.Generic;
using System.Globalization;
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
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Owner.Controllers.OwnerFinancials
{
    [Area("Owner")]
    [Authorize]
    [AuthorizeActiveOwner]
    [Route("Owner/Financials/Overview")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public sealed class OwnerFinancialOverviewController
        : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Owner/Views/OwnerFinancials/" +
            "_OwnerFinancialOverview.cshtml";

        private readonly IOwnerFinancialService
            _ownerFinancialService;

        private readonly AppDbContext
            _context;

        private readonly ILogger<
            OwnerFinancialOverviewController
        > _logger;

        public OwnerFinancialOverviewController(
            IOwnerFinancialService ownerFinancialService,
            AppDbContext context,
            ILogger<
                OwnerFinancialOverviewController
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
        public async Task<IActionResult> Index(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            if (
                fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value.Date >
                toDate.Value.Date
            )
            {
                return BadRequest(
                    "تاريخ البداية يجب أن يسبق تاريخ النهاية.");
            }

            if (
                fromDate.HasValue &&
                fromDate.Value.Date >
                DateTime.Today
            )
            {
                return BadRequest(
                    "تاريخ البداية لا يمكن أن يكون في المستقبل.");
            }

            if (
                toDate.HasValue &&
                toDate.Value.Date >
                DateTime.Today
            )
            {
                return BadRequest(
                    "تاريخ النهاية لا يمكن أن يكون في المستقبل.");
            }

            if (!IsAjaxRequest())
            {
                return Redirect(
                    BuildFinancialPageUrl(
                        fromDate,
                        toDate));
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
                        cancellationToken);

                var isAdministrationAccount =
                    await IsCurrentAdminAccountAsync(
                        ownerId,
                        cancellationToken);

                var isAdministrationOwnedPlace =
                    isAdministrationAccount &&
                    activePlaceId.HasValue &&
                    await _context.Places
                        .AsNoTracking()
                        .AnyAsync(place =>
                            place.PlaceId == activePlaceId.Value &&
                            place.OwnerId == ownerId,
                            cancellationToken);

                ViewData["IsAdministrationAccount"] =
                    isAdministrationAccount;

                ViewData["IsAdministrationOwnedPlace"] =
                    isAdministrationOwnedPlace;

                var model =
                    await _ownerFinancialService
                        .GetOverviewAsync(
                            ownerId,
                            activePlaceId,
                            fromDate,
                            toDate,
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
                    "تعذر تحميل النظرة العامة المالية " +
                    "للمالك {OwnerId}.",
                    ownerId);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError,
                    "تعذر تحميل النظرة العامة المالية حاليًا.");
            }
        }

        private static string BuildFinancialPageUrl(
            DateTime? fromDate,
            DateTime? toDate)
        {
            var values =
                new List<
                    KeyValuePair<
                        string,
                        string?>>
                {
                    new(
                        "financeTab",
                        "overview")
                };

            if (fromDate.HasValue)
            {
                values.Add(
                    new(
                        "fromDate",
                        fromDate.Value.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)));
            }

            if (toDate.HasValue)
            {
                values.Add(
                    new(
                        "toDate",
                        toDate.Value.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)));
            }

            return
                "/Owner/Financials" +
                QueryString
                    .Create(values)
                    .ToUriComponent();
        }

        private Task<int?>
            ResolveActivePlaceIdAsync(
                int ownerId,
                CancellationToken cancellationToken)
        {
            return _ownerFinancialService
                .ResolveActivePlaceIdAsync(
                    ownerId,
                    null,
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

        private Task<bool> IsCurrentAdminAccountAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            return _context.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == userId &&
                    user.Role == SystemRoles.Admin,
                    cancellationToken);
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
