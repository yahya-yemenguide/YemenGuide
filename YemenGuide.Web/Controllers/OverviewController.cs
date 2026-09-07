using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.Constants;
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

        // =============================================================
        // بدء رحلة المالك من لوحة العميل
        // Customer -> Owner Pending -> VerifyAccount
        // =============================================================
        [HttpPost]
        [Authorize(Roles = "Customer,Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartOwnerJourney()
        {
            var userIdText =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId) || userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var authentication =
                await HttpContext.AuthenticateAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

            var user =
                await _context.Users
                    .FirstOrDefaultAsync(
                        item => item.UserId == userId,
                        HttpContext.RequestAborted);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // الحسابات المحظورة أو المعلقة لا يمكنها بدء رحلة مالك.
            if (string.Equals(
                    user.AccountState,
                    "Blocked",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    user.AccountState,
                    "Suspended",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // إذا كان الحساب ما يزال Customer نبدأ رحلة المالك فعلياً.
            // Pending مهمة حتى تظهر رحلة التوثيق ولا يتم تجاوزها إلى لوحة المالك.
            if (string.Equals(
                    user.Role,
                    SystemRoles.Customer,
                    StringComparison.OrdinalIgnoreCase))
            {
                user.Role = SystemRoles.Owner;
                user.AccountState = "Pending";

                await _context.SaveChangesAsync(
                    HttpContext.RequestAborted);
            }
            else if (!string.Equals(
                         user.Role,
                         SystemRoles.Owner,
                         StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // قاعدة البيانات وحدها لا تكفي:
            // Cookie الحالية ما زالت تحمل Role القديم، لذلك نجدد الـClaims فوراً.
            string lang =
                string.IsNullOrWhiteSpace(user.PreferredLanguage)
                    ? "ar"
                    : user.PreferredLanguage;

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    user.Username ?? string.Empty),

                new Claim(
                    ClaimTypes.Role,
                    user.Role ?? SystemRoles.Owner),

                new Claim(
                    "IsDarkMode",
                    user.IsDarkMode.ToString()),

                new Claim(
                    "PreferredLanguage",
                    lang),

                new Claim(
                    "AccountState",
                    user.AccountState ?? "Pending")
            };

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

            bool isPersistent =
                authentication.Properties?.IsPersistent ?? false;

            var authProperties =
                new AuthenticationProperties
                {
                    IsPersistent = isPersistent,
                    AllowRefresh = true,
                    ExpiresUtc =
                        authentication.Properties?.ExpiresUtc ??
                        (
                            isPersistent
                                ? DateTimeOffset.UtcNow.AddDays(30)
                                : DateTimeOffset.UtcNow.AddHours(12)
                        )
                };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authProperties);

            // Owner نشط مسبقاً لا يحتاج إعادة رحلة التوثيق.
            if (string.Equals(
                    user.AccountState,
                    "Active",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(
                    "/Owner/OwnerDashboard/DashboardOwner");
            }

            // Pending / Rejected / Accepted تذهب لمسار المالك الموجود أصلاً.
            return RedirectToAction(
                "VerifyAccount",
                "Account");
        }
    }
}
