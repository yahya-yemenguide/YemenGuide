using System;
using System.Collections.Generic;
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
        private static readonly HashSet<string> AllowedPaymentStatuses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "FullyPaid", "PartiallyPaid", "Unpaid", "UnderReview", "Refunded"
            };

        private static readonly HashSet<string> AllowedCommissionStatuses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Calculated", "Held", "Charged", "Released", "Debt"
            };

        private static readonly HashSet<string> AllowedBookingStates =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Pending", "AwaitingPayment", "PaymentReview", "Confirmed",
                "Completed", "Cancelled", "NoShow"
            };

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

            NormalizeFilter(filter);

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

        private void NormalizeFilter(
            BookingFinancialFilterDto filter)
        {
            if (
                filter.FromDate.HasValue &&
                filter.ToDate.HasValue &&
                filter.FromDate.Value.Date > filter.ToDate.Value.Date)
            {
                (filter.FromDate, filter.ToDate) =
                    (filter.ToDate, filter.FromDate);

                ModelState.Remove(nameof(BookingFinancialFilterDto.FromDate));
                ModelState.Remove(nameof(BookingFinancialFilterDto.ToDate));
            }

            var today = DateTime.Today;

            if (filter.FromDate.HasValue && filter.FromDate.Value.Date > today)
            {
                filter.FromDate = today;
                ModelState.Remove(nameof(BookingFinancialFilterDto.FromDate));
            }

            if (filter.ToDate.HasValue && filter.ToDate.Value.Date > today)
            {
                filter.ToDate = today;
                ModelState.Remove(nameof(BookingFinancialFilterDto.ToDate));
            }

            filter.Search = NormalizeText(filter.Search, 100);
            filter.PaymentStatus =
                AllowedValueOrNull(filter.PaymentStatus, AllowedPaymentStatuses);
            filter.CommissionStatus =
                AllowedValueOrNull(filter.CommissionStatus, AllowedCommissionStatuses);
            filter.BookingState =
                AllowedValueOrNull(filter.BookingState, AllowedBookingStates);

            filter.PageNumber = Math.Max(1, filter.PageNumber);

            if (
                filter.PageSize != 10 &&
                filter.PageSize != 20 &&
                filter.PageSize != 30 &&
                filter.PageSize != 50)
            {
                filter.PageSize = 20;
            }

            ModelState.Remove(nameof(BookingFinancialFilterDto.Search));
            ModelState.Remove(nameof(BookingFinancialFilterDto.PaymentStatus));
            ModelState.Remove(nameof(BookingFinancialFilterDto.CommissionStatus));
            ModelState.Remove(nameof(BookingFinancialFilterDto.BookingState));
            ModelState.Remove(nameof(BookingFinancialFilterDto.PageNumber));
            ModelState.Remove(nameof(BookingFinancialFilterDto.PageSize));
        }

        private static string? AllowedValueOrNull(
            string? value,
            HashSet<string> allowed)
        {
            var normalized = NormalizeText(value, 40);

            if (normalized == null)
            {
                return null;
            }

            return allowed.FirstOrDefault(item =>
                string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static string? NormalizeText(
            string? value,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();

            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength);
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
