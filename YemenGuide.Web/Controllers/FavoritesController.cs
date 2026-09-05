using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly CurrencyDisplayService _currencyDisplayService;

        public FavoritesController(
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

        // =====================================================================
        // ❤️ صفحة المفضلة — البيانات الحقيقية + التقييم + العروض الفعالة
        // =====================================================================
        [HttpGet("/Favorites/MyFavorites")]
        public async Task<IActionResult> MyFavorites()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            /* =================================================================
               1. الأماكن المحفوظة لهذا المستخدم فقط
               ================================================================= */
            var favoriteRows = await _context.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                .OrderByDescending(favorite => favorite.CreatedAt)
                .Select(favorite => new
                {
                    FavoriteCreatedAt = favorite.CreatedAt,
                    PlaceId = favorite.Place.PlaceId,
                    Name = favorite.Place.PlaceNameAndDesc,
                    CategoryName = favorite.Place.Category != null
                        ? favorite.Place.Category.CategoryName
                        : "عام",
                    CityName = favorite.Place.City != null
                        ? favorite.Place.City.CityName
                        : "غير محدد",
                    ExchangeRateRegion = favorite.Place.City != null
                        ? favorite.Place.City.ExchangeRateRegion
                        : null,
                    Address = favorite.Place.AddressDetail,
                    PlacePrice = favorite.Place.Price,
                    BookingSystemId = favorite.Place.Category != null
                        ? favorite.Place.Category.BookingSystemId
                        : 0,
                    MainImage = favorite.Place.CoverImage ??
                        "/images/places/default-main.jpg"
                })
                .ToListAsync();

            var placeIds = favoriteRows
                .Select(item => item.PlaceId)
                .Distinct()
                .ToArray();

            if (placeIds.Length == 0)
            {
                return View(
                    "~/Views/Panel/Favorites/MyFavorites.cshtml",
                    new List<PlaceSearchResultDto>());
            }

            /* =================================================================
               2. التقييمات الحقيقية — الردود لا تدخل في المتوسط
               ================================================================= */
            var reviewStatsRows = await _context.Reviews
                .AsNoTracking()
                .Where(review =>
                    placeIds.Contains(review.PlaceId) &&
                    review.ParentReviewId == null &&
                    review.Stars.HasValue)
                .GroupBy(review => review.PlaceId)
                .Select(group => new
                {
                    PlaceId = group.Key,
                    Rating = group.Average(review =>
                        (double)review.Stars!.Value),
                    Count = group.Count()
                })
                .ToListAsync();

            var reviewStatsByPlace = reviewStatsRows.ToDictionary(
                item => item.PlaceId,
                item => (
                    Rating: Math.Round(item.Rating, 1),
                    Count: item.Count));

            /* =================================================================
               3. معرض الصور
               ================================================================= */
            var galleryRows = await _context.PlaceImages
                .AsNoTracking()
                .Where(image =>
                    placeIds.Contains(image.PlaceId) &&
                    !string.IsNullOrWhiteSpace(image.ImagePath))
                .Select(image => new
                {
                    image.PlaceId,
                    image.ImagePath
                })
                .ToListAsync();

            var galleryByPlace = galleryRows
                .GroupBy(image => image.PlaceId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(image => image.ImagePath)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList());

            /* =================================================================
               4. هل يوجد خيار حجز حقيقي الآن؟
               الفندق = غرفة متاحة بكمية فعلية
               الأنظمة المعتمدة على الباقات = باقة فعالة
               ================================================================= */
            var placesWithAvailableRooms = (
                await _context.Rooms
                    .AsNoTracking()
                    .Where(room =>
                        placeIds.Contains(room.PlaceId) &&
                        room.IsAvailable == true &&
                        room.Quantity > 0)
                    .Select(room => room.PlaceId)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            var placesWithActivePackages = (
                await _context.PlacePackages
                    .AsNoTracking()
                    .Where(package =>
                        placeIds.Contains(package.PlaceId) &&
                        package.IsActive == true)
                    .Select(package => package.PlaceId)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            /* =================================================================
               5. العروض الفعالة — نفس منطق بطاقات البحث
               ================================================================= */
            var today = DateTime.Today;

            var promotionCandidates = await _context.Promotions
                .AsNoTracking()
                .Where(promotion =>
                    placeIds.Contains(promotion.PlaceId) &&
                    promotion.IsActive == true &&
                    (promotion.Status == "Active" ||
                     promotion.Status == "Scheduled") &&
                    promotion.StartDate.Date <= today &&
                    promotion.EndDate.Date >= today &&
                    promotion.Place.Status == "Active")
                .OrderBy(promotion => promotion.EndDate)
                .ThenByDescending(promotion =>
                    promotion.DiscountType == "Percentage"
                        ? promotion.DiscountValue
                        : 0m)
                .ThenByDescending(promotion =>
                    promotion.UpdatedAt ?? promotion.CreatedAt)
                .ThenByDescending(promotion => promotion.PromotionId)
                .Select(promotion => new
                {
                    promotion.PromotionId,
                    promotion.PlaceId,
                    promotion.PromotionTitle,
                    promotion.OfferMode,
                    promotion.RewardType,
                    promotion.RequiredQuantity,
                    promotion.RewardQuantity,
                    promotion.IsRewardRepeatable,
                    promotion.RewardServiceId,
                    promotion.SpecialPrice,
                    promotion.MinimumNights,
                    promotion.DiscountType,
                    promotion.DiscountValue,
                    promotion.EndDate,
                    promotion.TotalUsageLimit,
                    promotion.RoomId,
                    promotion.PackageId,
                    promotion.ServiceId
                })
                .ToListAsync();

            var promotionIds = promotionCandidates
                .Select(promotion => promotion.PromotionId)
                .ToArray();

            var promotionUsageCounts = new Dictionary<int, int>();

            if (promotionIds.Length > 0)
            {
                try
                {
                    promotionUsageCounts = await _context.Bookings
                        .AsNoTracking()
                        .Where(booking =>
                            booking.PromotionId.HasValue &&
                            promotionIds.Contains(booking.PromotionId.Value) &&
                            booking.BookingState != "Cancelled" &&
                            booking.BookingState != "Rejected")
                        .GroupBy(booking => booking.PromotionId!.Value)
                        .ToDictionaryAsync(
                            group => group.Key,
                            group => group.Count());
                }
                catch (Exception ex)
                {
                    // لا نوقف صفحة المفضلة لو كانت إحصائية استخدام العرض غير متاحة مؤقتاً.
                    Console.WriteLine(
                        $"Favorites promotions usage warning: {ex.Message}");
                }
            }

            var availablePromotions = promotionCandidates
                .Where(promotion =>
                {
                    int usedCount = promotionUsageCounts.TryGetValue(
                        promotion.PromotionId,
                        out int storedUsedCount)
                            ? storedUsedCount
                            : 0;

                    return !promotion.TotalUsageLimit.HasValue ||
                           usedCount < promotion.TotalUsageLimit.Value;
                })
                .ToList();

            var promotionByPlace = availablePromotions
                .GroupBy(promotion => promotion.PlaceId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First());

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            /* =================================================================
               6. بناء DTO النهائي
               ================================================================= */
            var favoritePlaces = new List<PlaceSearchResultDto>();

            foreach (var item in favoriteRows)
            {
                var reviewStats = reviewStatsByPlace.TryGetValue(
                    item.PlaceId,
                    out var storedReviewStats)
                        ? storedReviewStats
                        : (Rating: 0d, Count: 0);

                bool hasBooking =
                    item.BookingSystemId == 1
                        ? placesWithAvailableRooms.Contains(item.PlaceId)
                        : item.BookingSystemId >= 2 &&
                          item.BookingSystemId <= 4
                            ? placesWithActivePackages.Contains(item.PlaceId)
                            : false;

                var dto = new PlaceSearchResultDto
                {
                    PlaceId = item.PlaceId,
                    Name = item.Name,
                    CategoryName = item.CategoryName,
                    CityName = item.CityName,
                    Address = item.Address,
                    Price = item.PlacePrice,
                    DisplayPrice = item.PlacePrice > 0m
                        ? currencyContext.Convert(
                            item.PlacePrice,
                            item.ExchangeRateRegion)
                        : 0m,
                    DisplayPriceText = item.PlacePrice > 0m
                        ? currencyContext.Format(
                            item.PlacePrice,
                            item.ExchangeRateRegion)
                        : string.Empty,
                    DisplayCurrency = currencyContext.Currency,
                    DisplayCurrencyCode = currencyContext.Code,
                    DisplayCurrencySymbol = currencyContext.Symbol,
                    ExchangeRateRegion = item.ExchangeRateRegion ?? string.Empty,
                    HasSearchPrice = item.PlacePrice > 0,
                    HasBooking = hasBooking,
                    MainImage = item.MainImage,
                    Rating = reviewStats.Rating,
                    ReviewsCount = reviewStats.Count,
                    IsFavorited = true,
                    GalleryImages = galleryByPlace.TryGetValue(
                        item.PlaceId,
                        out var gallery)
                            ? gallery
                            : new List<string>()
                };

                if (promotionByPlace.TryGetValue(
                    item.PlaceId,
                    out var promotion))
                {
                    dto.HasActivePromotion = true;
                    dto.ActivePromotionId = promotion.PromotionId;
                    dto.ActivePromotionTitle = promotion.PromotionTitle;
                    dto.PromotionEndDate = promotion.EndDate;

                    dto.PromotionTargetLabel =
                        promotion.RoomId.HasValue
                            ? "على غرفة"
                            : promotion.PackageId.HasValue
                                ? "على باقة"
                                : promotion.ServiceId.HasValue
                                    ? "على خدمة"
                                    : "على المنشأة";

                    string discountText =
                        string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                            ? $"{promotion.DiscountValue:0.##}%"
                            : promotion.DiscountValue > 0m
                                ? currencyContext.Format(promotion.DiscountValue, item.ExchangeRateRegion)
                                : string.Empty;

                    string rewardServiceName = string.Empty;
                    if (promotion.RewardServiceId.HasValue && promotion.RewardServiceId.Value > 0)
                    {
                        rewardServiceName = await _context.PlaceAdditionalServices
                            .AsNoTracking()
                            .Where(service =>
                                service.ServiceId == promotion.RewardServiceId.Value &&
                                service.PlaceId == item.PlaceId)
                            .Select(service => service.ServiceName)
                            .FirstOrDefaultAsync() ?? string.Empty;
                    }

                    string specialPriceText = promotion.SpecialPrice.HasValue
                        ? currencyContext.Format(promotion.SpecialPrice.Value, item.ExchangeRateRegion)
                        : string.Empty;

                    string offerText = BuildPromotionOfferText(
                        string.IsNullOrWhiteSpace(promotion.OfferMode) ? "Discount" : promotion.OfferMode,
                        promotion.RewardType ?? string.Empty,
                        promotion.DiscountType,
                        promotion.DiscountValue,
                        discountText,
                        promotion.RequiredQuantity,
                        promotion.RewardQuantity,
                        promotion.IsRewardRepeatable,
                        rewardServiceName,
                        specialPriceText,
                        promotion.MinimumNights);

                    dto.PromotionOfferText = offerText;
                    dto.PromotionDiscountText = offerText;
                }

                favoritePlaces.Add(dto);
            }

            return View(
                "~/Views/Panel/Favorites/MyFavorites.cshtml",
                favoritePlaces);
        }
        private static string BuildPromotionOfferText(
            string offerMode,
            string rewardType,
            string? discountType,
            decimal discountValue,
            string displayDiscountText,
            int? requiredQuantity,
            int? rewardQuantity,
            bool isRewardRepeatable,
            string rewardServiceName,
            string specialPriceText,
            int? minimumNights)
        {
            if (!string.Equals(offerMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                    ? $"خصم {discountValue:0.##}%"
                    : string.IsNullOrWhiteSpace(displayDiscountText) ? "عرض خاص" : $"خصم {displayDiscountText}";
            }

            if (string.Equals(rewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
            {
                int required = minimumNights.GetValueOrDefault() > 0
                    ? minimumNights!.Value
                    : Math.Max(1, requiredQuantity.GetValueOrDefault(1));
                int reward = Math.Max(1, rewardQuantity.GetValueOrDefault(1));
                string text = minimumNights.GetValueOrDefault() > 0
                    ? $"احجز {required} ليلة واحصل على {reward} ليلة مجانًا"
                    : $"احجز {required} واحصل على {reward} مجانًا";
                return isRewardRepeatable ? text + " — يتكرر" : text + " — مرة واحدة";
            }
            if (string.Equals(rewardType, "FreeService", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(rewardServiceName) ? "خدمة إضافية مجانًا" : $"{rewardServiceName} مجانًا";
            if (string.Equals(rewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(specialPriceText) ? "سعر خاص" : $"سعر خاص {specialPriceText}";
            if (string.Equals(rewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase))
            {
                string service = string.IsNullOrWhiteSpace(rewardServiceName) ? "الخدمة" : rewardServiceName;
                return string.IsNullOrWhiteSpace(specialPriceText) ? $"سعر خاص لـ {service}" : $"{service} بسعر {specialPriceText}";
            }
            if (string.Equals(rewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase))
                return $"خصم {discountValue:0.##}% عند تحقق الشروط";
            if (string.Equals(rewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(displayDiscountText) ? "خصم خاص" : $"خصم {displayDiscountText}";
            return "عرض خاص";
        }

    }
}
