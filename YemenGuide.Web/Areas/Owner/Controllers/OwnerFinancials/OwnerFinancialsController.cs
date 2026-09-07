using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Owner.Controllers.OwnerFinancials
{
    [Area("Owner")]
    [Authorize(Roles = "Owner,Admin")]
    [AuthorizeActiveOwner]
    [Route("Owner/Financials")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public sealed class OwnerFinancialsController : Controller
    {
        private static readonly HashSet<string> CanonicalTabs =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "overview",
                "bookings",
                "balance",
                "refunds",
                "wallets",
                "reports"
            };

        private readonly IOwnerFinancialService
            _ownerFinancialService;

        public OwnerFinancialsController(
            IOwnerFinancialService ownerFinancialService)
        {
            _ownerFinancialService =
                ownerFinancialService ??
                throw new ArgumentNullException(
                    nameof(ownerFinancialService));
        }

        [HttpGet("")]
        [HttpGet("Manage")]
        public async Task<IActionResult>
            ManageOwnerFinancials(
                [FromQuery] string? financeTab = null,
                [FromQuery] int? placeId = null,
                CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentOwnerId(
                    out var ownerId))
            {
                return Challenge();
            }

            var activeTab =
                NormalizeTab(
                    financeTab);

            var activePlaceId =
                await ResolveActivePlaceIdAsync(
                    ownerId,
                    placeId,
                    cancellationToken);

            var model =
                await _ownerFinancialService
                    .GetShellAsync(
                        ownerId,
                        activePlaceId,
                        cancellationToken);

            ViewData["Title"] =
                "الإدارة المالية";

            ViewData["ActiveFinancialTab"] =
                activeTab;

            ViewData["CanonicalFinancialTab"] =
                activeTab;

            ViewData["ActivePlaceId"] =
                activePlaceId;

            return View(
                "~/Areas/Owner/Views/OwnerFinancials/" +
                "ManageOwnerFinancials.cshtml",
                model);
        }

        private async Task<int?>
            ResolveActivePlaceIdAsync(
                int ownerId,
                int? requestedPlaceId,
                CancellationToken cancellationToken)
        {
            return await _ownerFinancialService
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

        private static string NormalizeTab(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return "overview";
            }

            var normalized =
                value.Trim()
                    .ToLowerInvariant();

            // توافق مع أي رابط قديم محفوظ لدى المستخدم.
            normalized =
                normalized switch
                {
                    "guarantee" => "balance",
                    "topup" => "balance",
                    "transactions" => "balance",
                    _ => normalized
                };

            return CanonicalTabs.Contains(
                normalized)
                ? normalized
                : "overview";
        }

        private bool TryGetCurrentOwnerId(
            out int ownerId)
        {
            ownerId = 0;

            var ownerIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                       ownerIdText,
                       out ownerId) &&
                   ownerId > 0;
        }
    }
}
