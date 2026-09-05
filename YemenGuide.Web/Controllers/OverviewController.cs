using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Controllers
{
    [Authorize]
    public class OverviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly CurrencyDisplayService _currencyDisplayService;

        public OverviewController(
            AppDbContext context,
            CurrencyDisplayService currencyDisplayService)
        {
            _context = context;
            _currencyDisplayService = currencyDisplayService;
        }

        private sealed class CustomerCurrencyContext
        {
            public string Currency { get; init; } = CurrencyDisplayService.YemeniRial;
            public string Code { get; init; } = "YER";
            public string Symbol { get; init; } = "ر.ي";
            public decimal SanaaUsdRate { get; init; } = 537m;
            public decimal SanaaSarRate { get; init; } = 140m;
            public decimal AdenUsdRate { get; init; } = 1900m;
            public decimal AdenSarRate { get; init; } = 500m;

            public decimal Convert(decimal amountInYer, string? exchangeRateRegion)
            {
                if (Currency == CurrencyDisplayService.YemeniRial)
                {
                    return Math.Round(amountInYer, 0, MidpointRounding.AwayFromZero);
                }

                bool useAdenMarket = string.Equals(
                    exchangeRateRegion,
                    "South",
                    StringComparison.OrdinalIgnoreCase);

                decimal rate = Currency == CurrencyDisplayService.UsDollar
                    ? useAdenMarket ? AdenUsdRate : SanaaUsdRate
                    : useAdenMarket ? AdenSarRate : SanaaSarRate;

                if (rate <= 0m)
                {
                    rate = Currency == CurrencyDisplayService.UsDollar
                        ? useAdenMarket ? 1900m : 537m
                        : useAdenMarket ? 500m : 140m;
                }

                return Math.Round(amountInYer / rate, 2, MidpointRounding.AwayFromZero);
            }

            public string Format(decimal amountInYer, string? exchangeRateRegion)
            {
                decimal converted = Convert(amountInYer, exchangeRateRegion);
                if (Currency == CurrencyDisplayService.UsDollar) return $"${converted:N2}";
                if (Currency == CurrencyDisplayService.SaudiRiyal) return $"{converted:N2} ر.س";
                return $"{converted:N0} ر.ي";
            }
        }

        private async Task<CustomerCurrencyContext> GetCustomerCurrencyContextAsync()
        {
            string currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(HttpContext.RequestAborted);

            var rates = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .Select(setting => new
                {
                    setting.SanaaUsdRate,
                    setting.SanaaSarRate,
                    setting.AdenUsdRate,
                    setting.AdenSarRate
                })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            return new CustomerCurrencyContext
            {
                Currency = currency,
                Code = CurrencyDisplayService.GetCurrencyCode(currency),
                Symbol = CurrencyDisplayService.GetCurrencySymbol(currency),
                SanaaUsdRate = rates?.SanaaUsdRate > 0m ? rates.SanaaUsdRate : 537m,
                SanaaSarRate = rates?.SanaaSarRate > 0m ? rates.SanaaSarRate : 140m,
                AdenUsdRate = rates?.AdenUsdRate > 0m ? rates.AdenUsdRate : 1900m,
                AdenSarRate = rates?.AdenSarRate > 0m ? rates.AdenSarRate : 500m
            };
        }

        [HttpGet]
        public async Task<IActionResult> Overview()
        {
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId) || userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserId == userId);

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var today = DateTime.Today;

            var bookingsCount = await _context.Bookings
                .AsNoTracking()
                .CountAsync(booking => booking.CustomerId == userId);

            var reviewsCount = await _context.Reviews
                .AsNoTracking()
                .CountAsync(review => review.UserId == userId);

            var unreadMessagesCount = await _context.Chats
                .AsNoTracking()
                .CountAsync(chat =>
                    chat.ReceiverId == userId &&
                    !chat.IsRead);

            var favoritesCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(favorite => favorite.UserId == userId);

            var requiresActionCount = await _context.Bookings
                .AsNoTracking()
                .CountAsync(booking =>
                    booking.CustomerId == userId &&
                    booking.BookingState == "AwaitingPayment" &&
                    (!booking.TimerExpiredAt.HasValue ||
                     booking.TimerExpiredAt.Value > DateTime.Now));

            var nextBooking = await _context.Bookings
                .AsNoTracking()
                .Include(booking => booking.Place)
                    .ThenInclude(place => place.City)
                .Include(booking => booking.Room)
                .Include(booking => booking.Package)
                .Where(booking =>
                    booking.CustomerId == userId &&
                    booking.StartDate >= today &&
                    (
                        booking.BookingState == "Pending" ||
                        booking.BookingState == "AwaitingPayment" ||
                        booking.BookingState == "PaymentReview" ||
                        booking.BookingState == "Confirmed"
                    ))
                .OrderBy(booking => booking.StartDate)
                .ThenBy(booking => booking.BookingId)
                .FirstOrDefaultAsync();

            var featuredPromotions = await _context.Promotions
                .AsNoTracking()
                .Include(promotion => promotion.Place)
                    .ThenInclude(place => place.City)
                .Where(promotion =>
                    promotion.IsActive == true &&
                    (promotion.Status == "Active" ||
                     promotion.Status == "Scheduled") &&
                    promotion.StartDate <= today &&
                    promotion.EndDate >= today &&
                    promotion.IsAutomatic != false &&
                    promotion.Place.Status == "Active")
                .OrderByDescending(promotion =>
                    promotion.DiscountType == "Percentage"
                        ? promotion.DiscountValue
                        : 0m)
                .ThenBy(promotion => promotion.EndDate)
                .ThenByDescending(promotion => promotion.PromotionId)
                .Take(3)
                .ToListAsync();

            var isProfileComplete =
                !string.IsNullOrWhiteSpace(currentUser.ProfileImage) &&
                !string.IsNullOrWhiteSpace(currentUser.PhoneNumber);

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            var featuredPromotionDiscountTexts =
                featuredPromotions.ToDictionary(
                    promotion => promotion.PromotionId,
                    promotion =>
                        string.Equals(
                            promotion.DiscountType,
                            "Percentage",
                            StringComparison.OrdinalIgnoreCase)
                            ? $"{promotion.DiscountValue:0.##}%"
                            : currencyContext.Format(
                                promotion.DiscountValue,
                                promotion.Place?
                                    .City?
                                    .ExchangeRateRegion));

            ViewBag.DisplayCurrency = currencyContext.Currency;
            ViewBag.DisplayCurrencyCode = currencyContext.Code;
            ViewBag.DisplayCurrencySymbol = currencyContext.Symbol;
            ViewBag.FeaturedPromotionDiscountTexts =
                featuredPromotionDiscountTexts;

            ViewBag.CurrentUser = currentUser;
            ViewBag.BookingsCount = bookingsCount;
            ViewBag.ReviewsCount = reviewsCount;
            ViewBag.UnreadMessagesCount = unreadMessagesCount;
            ViewBag.FavoritesCount = favoritesCount;
            ViewBag.RequiresActionCount = requiresActionCount;
            ViewBag.NextBooking = nextBooking;
            ViewBag.FeaturedPromotions = featuredPromotions;
            ViewBag.IsProfileComplete = isProfileComplete;

            return View("~/Views/Panel/Overview/Overview.cshtml");
        }
    }
}
