using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using YemenGuide.Core.DTOs.Owner;
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
        // بدء رحلة إضافة المنشأة من لوحة العميل
        // مهم: العميل يظل Customer حتى تنجح إضافة منشأة فعلية.
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

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.UserId == userId,
                    HttpContext.RequestAborted);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

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

            // المالك الموجود مسبقاً لا يحتاج رحلة عميل جديدة.
            if (string.Equals(
                    user.Role,
                    SystemRoles.Owner,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(
                        user.AccountState,
                        "Active",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(
                        "/Owner/OwnerDashboard/DashboardOwner");
                }

                return RedirectToAction(
                    "VerifyAccount",
                    "Account");
            }

            if (!string.Equals(
                    user.Role,
                    SystemRoles.Customer,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // لا نغيّر Role ولا AccountState هنا.
            // هذه Cookie فقط لفتح شاشة إنشاء المنشأة الخاصة بالعميل.
            Response.Cookies.Append(
                "YemenGuide.OwnerOnboarding",
                "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    MaxAge = TimeSpan.FromMinutes(60),
                    Path = "/"
                });

            return RedirectToAction(nameof(OwnerOnboarding));
        }

        // =============================================================
        // اختيار العميل للمسار السريع.
        // هنا فقط يتحول إلى Owner/Pending لأنه اختار صراحةً
        // التسجيل السريع الذي يبدأ توثيق حساب المالك مباشرة.
        // =============================================================
        [HttpPost]
        [Authorize(Roles = "Customer,Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartOwnerFastTrack()
        {
            var userIdText =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId) || userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(
                    item => item.UserId == userId,
                    HttpContext.RequestAborted);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

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

            if (string.Equals(
                    user.Role,
                    SystemRoles.Owner,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(
                        user.AccountState,
                        "Active",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(
                        "/Owner/OwnerDashboard/DashboardOwner");
                }

                Response.Cookies.Delete(
                    "YemenGuide.OwnerOnboarding",
                    new CookieOptions { Path = "/" });

                return Redirect(
                    "/Account/VerifyAccount?track=fast");
            }

            if (!string.Equals(
                    user.Role,
                    SystemRoles.Customer,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            user.Role = SystemRoles.Owner;
            user.AccountState = "Pending";

            await _context.SaveChangesAsync(
                HttpContext.RequestAborted);

            await RefreshOwnerAuthenticationAsync(user);

            Response.Cookies.Delete(
                "YemenGuide.OwnerOnboarding",
                new CookieOptions { Path = "/" });

            return Redirect(
                "/Account/VerifyAccount?track=fast");
        }

        // =============================================================
        // شاشة اختيار المسار ثم إضافة المنشأة للعميل قبل أن يصبح Owner.
        // تستخدم نفس _AddPlaceWizard + add-place.js بدون نسخ منطق الإضافة.
        // =============================================================
        [HttpGet]
        [Authorize(Roles = "Customer,Owner")]
        public async Task<IActionResult> OwnerOnboarding()
        {
            var userIdText =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId) || userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.UserId == userId,
                    HttpContext.RequestAborted);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.Equals(
                    user.Role,
                    SystemRoles.Owner,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(
                        user.AccountState,
                        "Active",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(
                        "/Owner/OwnerDashboard/DashboardOwner");
                }

                return RedirectToAction(
                    "VerifyAccount",
                    "Account");
            }

            if (!string.Equals(
                    user.Role,
                    SystemRoles.Customer,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (!HasOwnerOnboardingCookie())
            {
                return RedirectToAction(nameof(Overview));
            }

            ViewBag.Categories = await _context.Categories
                .AsNoTracking()
                .Where(category => category.OwnerCanAddPlaces == true)
                .OrderByDescending(category => category.HasBookingSystem)
                .ThenBy(category => category.CategoryName)
                .ToListAsync(HttpContext.RequestAborted);

            ViewBag.SiteName = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .Select(setting => setting.SiteName)
                .FirstOrDefaultAsync(HttpContext.RequestAborted)
                ?? "دليلك اليمني";

            return View(
                "~/Views/Panel/Overview/OwnerOnboarding.cshtml");
        }

        // =============================================================
        // Proxy آمن لتحميل نفس نموذج AddPlace الحالي.
        // استدعاء مباشر للكنترولر الحالي يحافظ على مصدر واحد للمنطق.
        // =============================================================
        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> OwnerOnboardingLoadPlaceForm(
            int categoryId)
        {
            if (!HasOwnerOnboardingCookie())
            {
                return Forbid();
            }

            var placeController =
                CreateOwnerPlaceControllerProxy();

            return await placeController.LoadPlaceForm(categoryId);
        }

        // =============================================================
        // حفظ المنشأة من رحلة العميل.
        // التحويل Customer -> Owner/Pending يحدث فقط بعد نجاح AddFullPlace.
        // =============================================================
        [HttpPost]
        [Authorize(Roles = "Customer")]
        [RequestFormLimits(
            ValueCountLimit = 10000,
            MultipartBodyLengthLimit = 104857600)]
        [RequestSizeLimit(104857600)]
        public async Task<IActionResult> OwnerOnboardingAddFullPlace(
            [FromForm] FullPlaceWizardDto model,
            IFormFile CoverImageUpload,
            List<IFormFile> GalleryImages)
        {
            if (!HasOwnerOnboardingCookie())
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message = "انتهت جلسة إضافة المنشأة. ارجع للوحة العميل واضغط «سجل منشأتك الآن» مرة أخرى."
                    });
            }

            var userIdText =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId) || userId <= 0)
            {
                return Unauthorized(
                    new
                    {
                        success = false,
                        message = "انتهت جلسة الدخول. سجّل الدخول ثم حاول مرة أخرى."
                    });
            }

            var placeController =
                CreateOwnerPlaceControllerProxy();

            // هذا هو AddFullPlace الأصلي نفسه؛ لا نكرر منطق المنشأة.
            var result = await placeController.AddFullPlace(
                model,
                CoverImageUpload,
                GalleryImages);

            if (!TryGetCreatedPlaceId(result, out var placeId))
            {
                // فشل الحفظ: العميل يظل Customer كما هو.
                return result;
            }

            // حماية إضافية: لا نغيّر الدور إلا إذا كانت المنشأة المحفوظة
            // موجودة فعلياً ومربوطة بنفس الحساب الحالي.
            bool savedPlaceBelongsToCustomer =
                await _context.Places
                    .AsNoTracking()
                    .AnyAsync(
                        place =>
                            place.PlaceId == placeId &&
                            place.OwnerId == userId,
                        HttpContext.RequestAborted);

            if (!savedPlaceBelongsToCustomer)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "تم حفظ البيانات لكن تعذر تأكيد ربط المنشأة بحسابك. لا تغلق الصفحة وتواصل مع الإدارة."
                    });
            }

            var customer = await _context.Users
                .FirstOrDefaultAsync(
                    item => item.UserId == userId,
                    HttpContext.RequestAborted);

            if (customer == null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "تم حفظ المنشأة لكن تعذر قراءة الحساب الحالي. سجّل الدخول من جديد."
                    });
            }

            if (string.Equals(
                    customer.Role,
                    SystemRoles.Customer,
                    StringComparison.OrdinalIgnoreCase))
            {
                customer.Role = SystemRoles.Owner;
                customer.AccountState = "Pending";

                await _context.SaveChangesAsync(
                    HttpContext.RequestAborted);
            }

            // بعد نجاح حفظ المنشأة وتغيير قاعدة البيانات فقط، نحدّث Claims.
            await RefreshOwnerAuthenticationAsync(customer);

            Response.Cookies.Delete(
                "YemenGuide.OwnerOnboarding",
                new CookieOptions { Path = "/" });

            return result;
        }

        private bool HasOwnerOnboardingCookie()
        {
            return Request.Cookies.TryGetValue(
                       "YemenGuide.OwnerOnboarding",
                       out var value) &&
                   string.Equals(
                       value,
                       "1",
                       StringComparison.Ordinal);
        }

        private YemenGuide.Web.Areas.Owner.Controllers.PlaceController
            CreateOwnerPlaceControllerProxy()
        {
            var environment = HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>();

            var controller =
                new YemenGuide.Web.Areas.Owner.Controllers.PlaceController(
                    _context,
                    environment)
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = HttpContext,
                        RouteData = RouteData
                    }
                };

            return controller;
        }

        private static bool TryGetCreatedPlaceId(
            IActionResult result,
            out int placeId)
        {
            placeId = 0;

            if (result is not JsonResult jsonResult ||
                jsonResult.Value == null)
            {
                return false;
            }

            var valueType = jsonResult.Value.GetType();

            var successProperty =
                valueType.GetProperty("success");

            var placeIdProperty =
                valueType.GetProperty("placeId");

            bool success =
                successProperty?.GetValue(jsonResult.Value) is bool saved &&
                saved;

            if (!success || placeIdProperty == null)
            {
                return false;
            }

            object? rawPlaceId =
                placeIdProperty.GetValue(jsonResult.Value);

            if (rawPlaceId is int intPlaceId)
            {
                placeId = intPlaceId;
            }
            else if (!int.TryParse(
                         Convert.ToString(rawPlaceId),
                         out placeId))
            {
                placeId = 0;
            }

            return placeId > 0;
        }

        private async Task RefreshOwnerAuthenticationAsync(
            YemenGuide.Core.Entities.User user)
        {
            var authentication =
                await HttpContext.AuthenticateAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

            string language =
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
                    SystemRoles.Owner),

                new Claim(
                    "IsDarkMode",
                    user.IsDarkMode.ToString()),

                new Claim(
                    "PreferredLanguage",
                    language),

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

            var authenticationProperties =
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
                authenticationProperties);
        }
    }
}
