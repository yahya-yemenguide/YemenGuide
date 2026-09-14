using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    [Route("Owner/Financials/BookingAccounts")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public sealed class OwnerFinancialBookingAccountsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICommissionFinanceService _financeService;
        private readonly ILogger<OwnerFinancialBookingAccountsController> _logger;

        public OwnerFinancialBookingAccountsController(
            ICommissionFinanceService financeService,
            AppDbContext context,
            ILogger<OwnerFinancialBookingAccountsController> logger)
        {
            _financeService =
                financeService ??
                throw new ArgumentNullException(nameof(financeService));

            _context =
                context ??
                throw new ArgumentNullException(nameof(context));

            _logger =
                logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(
            [FromQuery] BookingFinancialFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Unauthorized(
                    "تعذر تحديد حساب المالك الحالي.");
            }

            filter ??=
                new BookingFinancialFilterDto();

            try
            {
                filter.PlaceId =
                    await ResolveActivePlaceIdAsync(
                        ownerId,
                        filter.PlaceId,
                        cancellationToken);

                var model =
                    await _financeService.GetBookingAccountsAsync(
                        ownerId,
                        filter,
                        cancellationToken);

                return PartialView(
                    "~/Areas/Owner/Views/OwnerFinancials/_OwnerFinancialBookingAccounts.cshtml",
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
                    "تعذر تحميل حسابات الحجوزات المالية للمالك {OwnerId}.",
                    ownerId);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "تعذر تحميل حسابات الحجوزات حاليًا.");
            }
        }

        private async Task<int?> ResolveActivePlaceIdAsync(
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken)
        {
            var cookieName =
                $"ownerActivePlace_{ownerId}";

            _ = int.TryParse(
                Request.Cookies[cookieName],
                out var cookiePlaceId);

            var ownedPlaceIds =
                await _context.Places
                    .AsNoTracking()
                    .Where(place =>
                        place.OwnerId == ownerId &&
                        place.Category != null &&
                        place.Category.BookingSystemId >= 1 &&
                        place.Category.BookingSystemId <= 4)
                    .OrderByDescending(place =>
                        place.Status == "Approved" ||
                        place.Status == "Active")
                    .ThenBy(place =>
                        place.PlaceNameAndDesc)
                    .Select(place =>
                        place.PlaceId)
                    .ToListAsync(cancellationToken);

            if (ownedPlaceIds.Count == 0)
            {
                return null;
            }

            return
                requestedPlaceId.HasValue &&
                requestedPlaceId.Value > 0 &&
                ownedPlaceIds.Contains(requestedPlaceId.Value)
                    ? requestedPlaceId.Value
                    : cookiePlaceId > 0 && ownedPlaceIds.Contains(cookiePlaceId)
                        ? cookiePlaceId
                        : ownedPlaceIds[0];
        }

        private bool TryGetCurrentOwnerId(
            out int ownerId)
        {
            ownerId = 0;

            var ownerIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return
                int.TryParse(
                    ownerIdText,
                    out ownerId) &&
                ownerId > 0;
        }
    }
}
