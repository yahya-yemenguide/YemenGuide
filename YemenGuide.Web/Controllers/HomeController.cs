using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; // ✅ ضروري لاستخراج هوية المستخدم لزر المفضلة
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using YemenGuide.Core.Constants;
using YemenGuide.Data;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly CurrencyDisplayService _currencyDisplayService;
        private readonly IMemoryCache _cache;

        // =============================================================
        // معايير الأماكن المميزة التلقائية
        // =============================================================
        // لا نعتمد على IsFeatured اليدوي عند اختيار بطاقات الرئيسية.
        // المنشأة تتأهل فقط من تقييمات عملاء مختلفين لديهم حجز مكتمل
        // في نفس المنشأة، ثم نستخدم متوسطاً موزوناً حتى لا تتصدر منشأة
        // بعدد قليل من التقييمات على حساب منشأة لها سجل موثوق أكبر.
        private const int FeaturedMinimumVerifiedCustomers = 10;
        private const double FeaturedMinimumAverageRating = 4.3d;
        private const double FeaturedBayesianPriorMean = 4.0d;
        private const int FeaturedBayesianPriorWeight = 20;
        private const int HomeFeaturedPlacesLimit = 6;
        private const int HomePromotionsLimit = 6;

        // نتيجة ترتيب الأماكن المميزة موحّدة لكل الزوار، لذلك نحتفظ بها مدة قصيرة
        // بدل إعادة قراءة كل الحجوزات والتقييمات في كل طلب للرئيسية.
        private sealed class FeaturedPlaceStatSnapshot
        {
            public int PlaceId { get; init; }
            public int VerifiedCustomerCount { get; init; }
            public double AverageRating { get; init; }
            public double WeightedScore { get; init; }
        }

        public HomeController(
            ILogger<HomeController> logger,
            AppDbContext context,
            CurrencyDisplayService currencyDisplayService,
            IMemoryCache cache)
        {
            _logger = logger;
            _context = context;
            _currencyDisplayService = currencyDisplayService;
            _cache = cache;
        }

        // =============================================================
        //  الصفحة الرئيسية (Index) - تخدم صفحة Index.cshtml
        // =============================================================
        public async Task<IActionResult> Index()
        {
            // 1. توجيه الأدمن والمالك إلى لوحات التحكم الخاصة بهم
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole(SystemRoles.Admin))
                {
                    return RedirectToAction("DashboardOverview", "AdminDashboard", new { area = "Admin" });
                }
                if (User.IsInRole(SystemRoles.Owner))
                {
                    return RedirectToAction("DashboardOwner", "OwnerDashboard", new { area = "Owner" });
                }
            }

            // 2. ✨ جلب إعدادات النظام وتمريرها للواجهة (شريط التهنئة والعناوين) ✨
            var settings = await _cache.GetOrCreateAsync(
                "yg:customer-shell:settings",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(30);

                    return await _context.SystemSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync();
                });
            if (settings != null)
            {
                ViewBag.HeroTitleMain = settings.HeroTitleMain;
                ViewBag.HeroTitleSub = settings.HeroTitleSub;
                ViewBag.IsBannerActive = settings.IsBannerActive;
                ViewBag.BannerText = settings.BannerText;
                ViewBag.SeasonTheme = settings.SeasonTheme;
                // نفس مصدر الاسم والشعار المستخدم في الهيدر تماماً.
                ViewBag.PlatformSiteName = settings.SiteName;
                ViewBag.PlatformLogoPath = settings.LogoPath;
            }

            // 💱 عملة العرض الحالية:
            // المستخدم المسجل => Users.PreferredCurrency
            // الزائر => SystemSettings.DefaultCurrency
            string displayCurrency =
                await _currencyDisplayService.GetPreferredCurrencyAsync();

            // ✅ استخراج الـ ID للمستخدم الحالي لمعرفة ما إذا كان قد وضع إعجاباً (قلب أحمر)
            int? currentUserId = null;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int uid))
                {
                    currentUserId = uid;
                }
            }

            // 3. 🌟 جلب الأماكن المميزة تلقائياً من تقييمات موثقة 🌟
            // -----------------------------------------------------------------
            // الحساب نفسه كما هو تماماً، لكن نتيجة الترتيب العام تُخزّن 90 ثانية.
            // هذا يلغي 3 استعلامات كبيرة (المنشآت/الحجوزات/التقييمات) من أغلب
            // طلبات الصفحة، ولا يغيّر المفضلة الخاصة بالمستخدم أو الأسعار.
            // -----------------------------------------------------------------
            var featuredStats = await _cache.GetOrCreateAsync(
                "yg:home:featured-ranking:v2",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(90);

                    var activePlaceIds = await _context.Places
                        .AsNoTracking()
                        .Where(p => p.Status == "Active")
                        .Select(p => p.PlaceId)
                        .ToListAsync();

                    if (activePlaceIds.Count == 0)
                    {
                        return new List<FeaturedPlaceStatSnapshot>();
                    }

                    var completedBookingCustomers = (await _context.Bookings
                        .AsNoTracking()
                        .Where(b =>
                            activePlaceIds.Contains(b.PlaceId) &&
                            b.CustomerId.HasValue &&
                            (b.BookingState == "Completed" || b.CompletedAt.HasValue))
                        .Select(b => new
                        {
                            b.PlaceId,
                            CustomerId = b.CustomerId!.Value
                        })
                        .Distinct()
                        .ToListAsync())
                        .Select(item => (item.PlaceId, item.CustomerId))
                        .ToList();

                    var completedBookingCustomerSet =
                        completedBookingCustomers.ToHashSet();

                    var reviewRows = (await _context.Reviews
                        .AsNoTracking()
                        .Where(r =>
                            activePlaceIds.Contains(r.PlaceId) &&
                            r.ParentReviewId == null &&
                            r.Stars.HasValue)
                        .Select(r => new
                        {
                            r.PlaceId,
                            r.UserId,
                            Stars = r.Stars!.Value
                        })
                        .ToListAsync())
                        .Select(item =>
                            (item.PlaceId, item.UserId, item.Stars))
                        .ToList();

                    var verifiedCustomerRatings = reviewRows
                        .Where(review =>
                            completedBookingCustomerSet.Contains(
                                (review.PlaceId, review.UserId)))
                        .GroupBy(review => new
                        {
                            review.PlaceId,
                            review.UserId
                        })
                        .Select(group => new
                        {
                            group.Key.PlaceId,
                            Rating = group.Average(item => (double)item.Stars)
                        })
                        .ToList();

                    return verifiedCustomerRatings
                        .GroupBy(item => item.PlaceId)
                        .Select(group =>
                        {
                            var verifiedCustomerCount = group.Count();
                            var averageRating = group.Average(item => item.Rating);
                            var weightedScore =
                                ((averageRating * verifiedCustomerCount) +
                                 (FeaturedBayesianPriorMean * FeaturedBayesianPriorWeight)) /
                                (verifiedCustomerCount + FeaturedBayesianPriorWeight);

                            return new FeaturedPlaceStatSnapshot
                            {
                                PlaceId = group.Key,
                                VerifiedCustomerCount = verifiedCustomerCount,
                                AverageRating = averageRating,
                                WeightedScore = weightedScore
                            };
                        })
                        .Where(item =>
                            item.VerifiedCustomerCount >= FeaturedMinimumVerifiedCustomers &&
                            item.AverageRating >= FeaturedMinimumAverageRating)
                        .OrderByDescending(item => item.WeightedScore)
                        .ThenByDescending(item => item.AverageRating)
                        .ThenByDescending(item => item.VerifiedCustomerCount)
                        .ThenByDescending(item => item.PlaceId)
                        .Take(HomeFeaturedPlacesLimit)
                        .ToList();
                }) ?? new List<FeaturedPlaceStatSnapshot>();

            var featuredPlaceIds = featuredStats
                .Select(item => item.PlaceId)
                .ToList();

            var featuredPlaceRows = featuredPlaceIds.Count == 0
                ? new List<YemenGuide.Core.Entities.Place>()
                : await _context.Places
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(place => place.Category)
                    .Include(place => place.City)
                    .Include(place => place.PlaceImages)
                    .Where(place => featuredPlaceIds.Contains(place.PlaceId))
                    .ToListAsync();

            var placeById = featuredPlaceRows
                .ToDictionary(place => place.PlaceId);

            var favoritePlaceIds = new HashSet<int>();

            if (currentUserId.HasValue && featuredPlaceIds.Count > 0)
            {
                favoritePlaceIds = (await _context.Favorites
                    .AsNoTracking()
                    .Where(favorite =>
                        favorite.UserId == currentUserId.Value &&
                        featuredPlaceIds.Contains(favorite.PlaceId))
                    .Select(favorite => favorite.PlaceId)
                    .ToListAsync())
                    .ToHashSet();
            }

            var featuredPlaces = new List<FeaturedPlaceDto>();

            foreach (var stats in featuredStats)
            {
                if (!placeById.TryGetValue(stats.PlaceId, out var place))
                {
                    continue;
                }

                featuredPlaces.Add(new FeaturedPlaceDto
                {
                    PlaceId = place.PlaceId,
                    Name = place.PlaceNameAndDesc,
                    CategoryName = place.Category != null
                        ? place.Category.CategoryName
                        : "عام",
                    CityName = place.City != null
                        ? place.City.CityName
                        : string.Empty,
                    Address = place.AddressDetail ?? string.Empty,
                    Price = place.Price,
                    DisplayPriceText =
                        place.Price > 0
                            ? await _currencyDisplayService.FormatByRegionAsync(
                                place.Price,
                                place.City?.ExchangeRateRegion)
                            : null,
                    MainImage = place.CoverImage,
                    HasBooking =
                        place.Category != null &&
                        place.Category.HasBookingSystem,
                    GalleryImages = place.PlaceImages
                        .Select(image => image.ImagePath)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .ToList(),
                    AverageRating = Math.Round(stats.AverageRating, 1),
                    ReviewCount = stats.VerifiedCustomerCount,
                    IsFavorited = favoritePlaceIds.Contains(place.PlaceId)
                });
            }


            // 4. 🎁 جلب العروض الفعالة للصفحة الرئيسية
            // -----------------------------------------------------------------
            // الرئيسية تعرض العروض المنشورة فقط خلال فترة صلاحيتها، مع منشأة
            // نشطة. لا نحسب أهلية العميل التفصيلية هنا (الأيام/الليالي/المبلغ)
            // لأنها تُعاد فحصها لاحقاً داخل رحلة الحجز.
            // -----------------------------------------------------------------
            DateTime promotionsToday = DateTime.Today;

            var promotionCandidates = await _context.Promotions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(promotion => promotion.Place)
                    .ThenInclude(place => place.Category)
                .Include(promotion => promotion.Place)
                    .ThenInclude(place => place.City)
                .Include(promotion => promotion.Place)
                    .ThenInclude(place => place.PlaceImages)
                .Include(promotion => promotion.Room)
                .Include(promotion => promotion.P)
                .Include(promotion => promotion.PlaceAdditionalService)
                .Where(promotion =>
                    promotion.IsActive == true &&
                    (promotion.Status == "Active" ||
                     promotion.Status == "Scheduled") &&
                    promotion.StartDate.Date <= promotionsToday &&
                    promotion.EndDate.Date >= promotionsToday &&
                    promotion.Place.Status == "Active")
                .OrderBy(promotion => promotion.EndDate)
                .ThenByDescending(promotion =>
                    promotion.DiscountType == "Percentage"
                        ? promotion.DiscountValue
                        : 0m)
                .ThenByDescending(promotion =>
                    promotion.UpdatedAt ?? promotion.CreatedAt)
                .ThenByDescending(promotion => promotion.PromotionId)
                .Take(30)
                .ToListAsync();

            var candidatePromotionIds = promotionCandidates
                .Select(promotion => promotion.PromotionId)
                .ToArray();

            var promotionUsageCounts = new Dictionary<int, int>();

            if (candidatePromotionIds.Length > 0)
            {
                try
                {
                    promotionUsageCounts = await _context.Bookings
                        .AsNoTracking()
                        .Where(booking =>
                            booking.PromotionId.HasValue &&
                            candidatePromotionIds.Contains(
                                booking.PromotionId.Value) &&
                            booking.BookingState != "Cancelled" &&
                            booking.BookingState != "Rejected")
                        .GroupBy(booking => booking.PromotionId!.Value)
                        .ToDictionaryAsync(
                            group => group.Key,
                            group => group.Count());
                }
                catch (Exception ex)
                {
                    // لا نسمح لتعذر إحصاء الاستخدامات أن يعطّل الصفحة الرئيسية.
                    // شرط الاستخدام يُفحص نهائياً مرة أخرى داخل الحجز.
                    _logger.LogWarning(
                        ex,
                        "تعذر جلب عدد استخدامات العروض في الصفحة الرئيسية.");
                }
            }

            var homePromotions = new List<HomePromotionDto>();

            foreach (var promotion in promotionCandidates)
            {
                if (homePromotions.Count >= HomePromotionsLimit)
                {
                    break;
                }

                int usedCount = promotionUsageCounts.TryGetValue(
                    promotion.PromotionId,
                    out int storedUsedCount)
                        ? storedUsedCount
                        : 0;

                if (promotion.TotalUsageLimit.HasValue &&
                    usedCount >= promotion.TotalUsageLimit.Value)
                {
                    continue;
                }

                string targetType = "Place";
                string targetLabel = "المنشأة كاملة";
                string? targetName = null;
                decimal? originalTargetPrice = null;

                if (promotion.RoomId.HasValue)
                {
                    targetType = "Room";
                    targetName = promotion.Room?.RoomType;
                    targetLabel = string.IsNullOrWhiteSpace(targetName)
                        ? "غرفة محددة"
                        : "غرفة • " + targetName;
                    originalTargetPrice = promotion.Room?.Price;
                }
                else if (promotion.PackageId.HasValue)
                {
                    targetType = "Package";
                    targetName = promotion.P?.PackageName;
                    targetLabel = string.IsNullOrWhiteSpace(targetName)
                        ? "باقة محددة"
                        : "باقة • " + targetName;
                    originalTargetPrice = promotion.P?.Price;
                }
                else if (promotion.ServiceId.HasValue)
                {
                    targetType = "Service";
                    targetName = promotion.PlaceAdditionalService?.ServiceName;
                    targetLabel = string.IsNullOrWhiteSpace(targetName)
                        ? "خدمة إضافية"
                        : "خدمة • " + targetName;
                    originalTargetPrice = promotion.PlaceAdditionalService?.Price;
                }

                // بيانات المكافأة الجديدة. لا نفترض أن كل عرض هو خصم نقدي.
                string offerMode = string.IsNullOrWhiteSpace(promotion.OfferMode)
                    ? "Discount"
                    : promotion.OfferMode;
                string rewardType = promotion.RewardType?.Trim() ?? string.Empty;

                string rewardServiceName = string.Empty;
                if (promotion.RewardServiceId.HasValue && promotion.RewardServiceId.Value > 0)
                {
                    rewardServiceName = await _context.PlaceAdditionalServices
                        .AsNoTracking()
                        .Where(service =>
                            service.ServiceId == promotion.RewardServiceId.Value &&
                            service.PlaceId == promotion.PlaceId)
                        .Select(service => service.ServiceName)
                        .FirstOrDefaultAsync() ?? string.Empty;
                }

                decimal? discountedTargetPrice =
                    CalculatePromotionTargetPrice(
                        originalTargetPrice,
                        offerMode,
                        rewardType,
                        promotion.DiscountType,
                        promotion.DiscountValue,
                        promotion.MaximumDiscountAmount,
                        promotion.SpecialPrice);

                // =============================================================
                // 💱 تحويل مبالغ العرض للعرض فقط.
                // كل الحسابات أعلاه تبقى بالريال اليمني، والتحويل يحصل بعد اكتمال
                // حساب الخصم حتى لا يتدخل سعر الصرف في منطق العرض نفسه.
                // =============================================================
                string? exchangeRateRegion =
                    promotion.Place.City?.ExchangeRateRegion;

                string? originalTargetPriceText =
                    originalTargetPrice.HasValue
                        ? await _currencyDisplayService.FormatByRegionAsync(
                            originalTargetPrice.Value,
                            exchangeRateRegion)
                        : null;

                string? discountedTargetPriceText =
                    discountedTargetPrice.HasValue
                        ? await _currencyDisplayService.FormatByRegionAsync(
                            discountedTargetPrice.Value,
                            exchangeRateRegion)
                        : null;

                bool percentageDiscount =
                    string.Equals(
                        promotion.DiscountType,
                        "Percentage",
                        StringComparison.OrdinalIgnoreCase);

                string discountValueText =
                    percentageDiscount
                        ? $"{promotion.DiscountValue:0.#}%"
                        : promotion.DiscountValue > 0
                            ? await _currencyDisplayService.FormatByRegionAsync(
                                promotion.DiscountValue,
                                exchangeRateRegion)
                            : string.Empty;

                string specialPriceText = promotion.SpecialPrice.HasValue
                    ? await _currencyDisplayService.FormatByRegionAsync(
                        promotion.SpecialPrice.Value,
                        exchangeRateRegion)
                    : string.Empty;

                string offerSummary = BuildPromotionOfferSummary(
                    offerMode,
                    rewardType,
                    promotion.DiscountType,
                    promotion.DiscountValue,
                    discountValueText,
                    promotion.RequiredQuantity,
                    promotion.RewardQuantity,
                    rewardServiceName,
                    specialPriceText,
                    promotion.MinimumNights);

                (string offerBadgeCaption, string offerBadgeValue) = BuildPromotionBadge(
                    offerMode,
                    rewardType,
                    promotion.DiscountType,
                    promotion.DiscountValue,
                    discountValueText,
                    promotion.RewardQuantity,
                    specialPriceText);

                string? maximumDiscountAmountText =
                    promotion.MaximumDiscountAmount.HasValue &&
                    promotion.MaximumDiscountAmount.Value > 0
                        ? await _currencyDisplayService.FormatByRegionAsync(
                            promotion.MaximumDiscountAmount.Value,
                            exchangeRateRegion)
                        : null;

                string? minimumBookingAmountText =
                    promotion.MinimumBookingAmount.HasValue &&
                    promotion.MinimumBookingAmount.Value > 0
                        ? await _currencyDisplayService.FormatByRegionAsync(
                            promotion.MinimumBookingAmount.Value,
                            exchangeRateRegion)
                        : null;

                string? placeFallbackImage =
                    promotion.Place.CoverImage ??
                    promotion.Place.LogoImage ??
                    promotion.Place.PlaceImages
                        .Select(image => image.ImagePath)
                        .FirstOrDefault(path =>
                            !string.IsNullOrWhiteSpace(path));

                int daysRemaining = Math.Max(
                    0,
                    (promotion.EndDate.Date - promotionsToday).Days);

                homePromotions.Add(new HomePromotionDto
                {
                    PromotionId = promotion.PromotionId,
                    PlaceId = promotion.PlaceId,
                    PromotionTitle = promotion.PromotionTitle,
                    ShortDescription =
                        promotion.ShortDescription ??
                        promotion.Description,
                    ImagePath =
                        !string.IsNullOrWhiteSpace(promotion.ImagePath)
                            ? promotion.ImagePath
                            : placeFallbackImage,
                    PlaceName = promotion.Place.PlaceNameAndDesc,
                    CategoryName =
                        promotion.Place.Category?.CategoryName ??
                        "منشأة",
                    CityName =
                        promotion.Place.City?.CityName ??
                        string.Empty,
                    OfferMode = offerMode,
                    RewardType = rewardType,
                    RequiredQuantity = promotion.RequiredQuantity,
                    RewardQuantity = promotion.RewardQuantity,
                    IsRewardRepeatable = promotion.IsRewardRepeatable,
                    RewardServiceId = promotion.RewardServiceId,
                    RewardServiceName = rewardServiceName,
                    SpecialPrice = promotion.SpecialPrice,
                    OfferSummary = offerSummary,
                    OfferBadgeCaption = offerBadgeCaption,
                    OfferBadgeValue = offerBadgeValue,
                    DiscountType = promotion.DiscountType ?? string.Empty,
                    DiscountValue = promotion.DiscountValue,
                    MaximumDiscountAmount =
                        promotion.MaximumDiscountAmount,
                    MinimumBookingAmount =
                        promotion.MinimumBookingAmount,
                    MinimumNights = promotion.MinimumNights,
                    MinimumPeople = promotion.MinimumPeople,
                    IsAutomatic = promotion.IsAutomatic != false,
                    TargetType = targetType,
                    TargetLabel = targetLabel,
                    TargetName = targetName,
                    EndDate = promotion.EndDate,
                    DaysRemaining = daysRemaining,
                    OriginalTargetPrice = originalTargetPrice,
                    DiscountedTargetPrice = discountedTargetPrice,
                    OriginalTargetPriceText = originalTargetPriceText,
                    DiscountedTargetPriceText = discountedTargetPriceText,
                    DiscountValueText = discountValueText,
                    MaximumDiscountAmountText = maximumDiscountAmountText,
                    MinimumBookingAmountText = minimumBookingAmountText
                });
            }

            // 4.1 🎁 ربط الأماكن المميزة بعرض فعّال واحد فقط
            // -----------------------------------------------------------------
            // لا نعرض تفاصيل العرض داخل بطاقة المكان المميز حتى تبقى البطاقة
            // نظيفة وفخمة. نمرر فقط وجود العرض ومعرّفه لعرض شارة صغيرة.
            // إذا كان للمنشأة أكثر من عرض، نختار الأقرب انتهاءً بعد استبعاد
            // العروض التي استُنفد حد استخدامها الكلي.
            // -----------------------------------------------------------------
            if (featuredPlaces.Count > 0)
            {
                var featuredPlaceIdsForPromotions = featuredPlaces
                    .Select(place => place.PlaceId)
                    .ToArray();

                var featuredPromotionCandidates = await _context.Promotions
                    .AsNoTracking()
                    .Where(promotion =>
                        featuredPlaceIdsForPromotions.Contains(promotion.PlaceId) &&
                        promotion.IsActive == true &&
                        (promotion.Status == "Active" ||
                         promotion.Status == "Scheduled") &&
                        promotion.StartDate.Date <= promotionsToday &&
                        promotion.EndDate.Date >= promotionsToday &&
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
                        promotion.TotalUsageLimit
                    })
                    .ToListAsync();

                var featuredPromotionIds = featuredPromotionCandidates
                    .Select(promotion => promotion.PromotionId)
                    .ToArray();

                var featuredPromotionUsageCounts = new Dictionary<int, int>();

                if (featuredPromotionIds.Length > 0)
                {
                    try
                    {
                        featuredPromotionUsageCounts = await _context.Bookings
                            .AsNoTracking()
                            .Where(booking =>
                                booking.PromotionId.HasValue &&
                                featuredPromotionIds.Contains(booking.PromotionId.Value) &&
                                booking.BookingState != "Cancelled" &&
                                booking.BookingState != "Rejected")
                            .GroupBy(booking => booking.PromotionId!.Value)
                            .ToDictionaryAsync(group => group.Key, group => group.Count());
                    }
                    catch (Exception ex)
                    {
                        // لا نسمح لتعذر عدّ الاستخدامات بتعطيل الصفحة الرئيسية.
                        _logger.LogWarning(
                            ex,
                            "تعذر جلب استخدامات عروض الأماكن المميزة.");
                    }
                }

                var selectedPromotionByPlace = featuredPromotionCandidates
                    .Where(promotion =>
                    {
                        int usedCount = featuredPromotionUsageCounts.TryGetValue(
                            promotion.PromotionId,
                            out int storedUsedCount)
                                ? storedUsedCount
                                : 0;

                        return !promotion.TotalUsageLimit.HasValue ||
                               usedCount < promotion.TotalUsageLimit.Value;
                    })
                    .GroupBy(promotion => promotion.PlaceId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First().PromotionId);

                foreach (var featuredPlace in featuredPlaces)
                {
                    if (selectedPromotionByPlace.TryGetValue(
                        featuredPlace.PlaceId,
                        out int promotionId))
                    {
                        featuredPlace.HasActivePromotion = true;
                        featuredPlace.ActivePromotionId = promotionId;
                    }
                }
            }

            // 4. --- جلب الفئات والمدن مع الأيقونات والألوان ---
            var categories = await _cache.GetOrCreateAsync(
                "yg:home:categories",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(45);

                    return await _context.Categories
                        .AsNoTracking()
                        .Select(c => new CategoryDto
                        {
                            Id = c.CategoryId,
                            Name = c.CategoryName,
                            ImagePath = c.ImagePath,
                            IconClass = c.IconClass,
                            ThemeColor = c.ThemeColor
                        })
                        .ToListAsync();
                }) ?? new List<CategoryDto>();

            var cities = await _cache.GetOrCreateAsync(
                "yg:home:cities",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(45);

                    return await _context.Cities
                        .AsNoTracking()
                        .Select(c => new CityDto
                        {
                            Id = c.CityId,
                            Name = c.CityName
                        })
                        .ToListAsync();
                }) ?? new List<CityDto>();

            // 5. =============================================================
            // ✨ استعلام جلب السلايدرات مع الألوان التفاعلية الجديدة (VIP)
            // =============================================================
            var heroSlides = await _cache.GetOrCreateAsync(
                "yg:home:hero-slides",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(30);

                    return await _context.HeroSlides
                        .AsNoTracking()
                        .Where(s => s.IsActive == true)
                        .OrderBy(s => s.DisplayOrder)
                        .Select(s => new CustomerHeroSlideDto
                        {
                            HeroSlideId = s.HeroSlideId,
                            ImagePath = s.ImagePath,
                            TypingText = s.TypingText,
                            DisplayOrder = s.DisplayOrder,
                            MainTextColor = s.MainTextColor,
                            AccentColor = s.AccentColor
                        })
                        .ToListAsync();
                }) ?? new List<CustomerHeroSlideDto>();

            // 6. =============================================================
            // 📊 جلب الإحصائيات الحقيقية من قاعدة البيانات
            // =============================================================
            var homeStats = await _cache.GetOrCreateAsync(
                "yg:home:stats",
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(30);

                    return new HomeStatsSnapshot
                    {
                        TotalTrustedPlaces =
                            await _context.Places
                                .AsNoTracking()
                                .CountAsync(p => p.Status == "Active"),

                        TotalSuccessfulBookings =
                            await _context.Bookings
                                .AsNoTracking()
                                .CountAsync(b => b.BookingState != "Rejected"),

                        TotalHappyCustomers =
                            await _context.Users
                                .AsNoTracking()
                                .CountAsync(u =>
                                    u.Role == "Customer" ||
                                    u.Role == "User"),

                        TotalCoveredCities =
                            await _context.Cities
                                .AsNoTracking()
                                .CountAsync()
                    };
                }) ?? new HomeStatsSnapshot();

            // 8. =============================================================
            // 🚀 تعبئة الـ ViewModel لإرساله للواجهة
            // =============================================================
            var viewModel = new HomeViewModel
            {
                FeaturedPlaces = featuredPlaces,
                Promotions = homePromotions,
                Categories = categories,
                Cities = cities,
                HeroSlides = heroSlides,

                // تمرير الإحصائيات (مع أرقام افتراضية فخمة في حال كانت الداتا بيز جديدة أو فارغة)
                TotalTrustedPlaces = homeStats.TotalTrustedPlaces,
                TotalSuccessfulBookings = homeStats.TotalSuccessfulBookings,
                TotalHappyCustomers = homeStats.TotalHappyCustomers,
                TotalCoveredCities = homeStats.TotalCoveredCities,

                // تمرير أسعار الصرف من الإعدادات (مع أرقام افتراضية للحماية)
                SanaaUsdRate = settings?.SanaaUsdRate ?? 535m,
                SanaaSarRate = settings?.SanaaSarRate ?? 140m,
                AdenUsdRate = settings?.AdenUsdRate ?? 1900m,
                AdenSarRate = settings?.AdenSarRate ?? 500m,

                // عملة العرض الفعلية للمستخدم الحالي.
                DisplayCurrency = displayCurrency,
                DisplayCurrencyCode =
                    CurrencyDisplayService.GetCurrencyCode(displayCurrency),
                DisplayCurrencySymbol =
                    CurrencyDisplayService.GetCurrencySymbol(displayCurrency)
            };

            return View(viewModel);
        }


        private sealed class HomeStatsSnapshot
        {
            public int TotalTrustedPlaces { get; init; }
            public int TotalSuccessfulBookings { get; init; }
            public int TotalHappyCustomers { get; init; }
            public int TotalCoveredCities { get; init; }
        }

        private static decimal? CalculatePromotionTargetPrice(
            decimal? originalPrice,
            string? offerMode,
            string? rewardType,
            string? discountType,
            decimal discountValue,
            decimal? maximumDiscountAmount,
            decimal? specialPrice)
        {
            if (!originalPrice.HasValue || originalPrice.Value <= 0m)
                return null;

            bool conditional = string.Equals(
                offerMode,
                "ConditionalReward",
                StringComparison.OrdinalIgnoreCase);

            if (conditional &&
                (string.Equals(rewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase)))
            {
                if (!specialPrice.HasValue || specialPrice.Value < 0m)
                    return null;

                return Math.Round(
                    Math.Min(originalPrice.Value, specialPrice.Value),
                    2);
            }

            bool percentage = string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase) ||
                              (conditional && string.Equals(rewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase));
            bool fixedAmount = string.Equals(discountType, "FixedAmount", StringComparison.OrdinalIgnoreCase) ||
                               (conditional && string.Equals(rewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase));

            if ((!percentage && !fixedAmount) || discountValue <= 0m)
                return null;

            decimal discountAmount = percentage
                ? originalPrice.Value * discountValue / 100m
                : discountValue;

            if (percentage && maximumDiscountAmount.HasValue && maximumDiscountAmount.Value > 0m)
                discountAmount = Math.Min(discountAmount, maximumDiscountAmount.Value);

            discountAmount = Math.Min(originalPrice.Value, Math.Max(0m, discountAmount));
            return Math.Round(originalPrice.Value - discountAmount, 2);
        }

        private static string BuildPromotionOfferSummary(
            string offerMode,
            string rewardType,
            string? discountType,
            decimal discountValue,
            string discountValueText,
            int? requiredQuantity,
            int? rewardQuantity,
            string rewardServiceName,
            string specialPriceText,
            int? minimumNights)
        {
            if (!string.Equals(offerMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                    ? $"خصم {discountValue:0.##}%"
                    : $"خصم {discountValueText}".Trim();
            }

            if (string.Equals(rewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
            {
                int required = minimumNights.GetValueOrDefault() > 0
                    ? minimumNights!.Value
                    : Math.Max(1, requiredQuantity.GetValueOrDefault(1));
                int reward = Math.Max(1, rewardQuantity.GetValueOrDefault(1));
                bool nights = minimumNights.GetValueOrDefault() > 0;
                return nights
                    ? $"احجز {required} ليلة واحصل على {reward} ليلة مجانًا"
                    : $"احجز {required} واحصل على {reward} مجانًا";
            }

            if (string.Equals(rewardType, "FreeService", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(rewardServiceName)
                    ? "خدمة إضافية مجانًا"
                    : $"{rewardServiceName} مجانًا";

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
                return $"خصم {discountValueText} عند تحقق الشروط";

            return "عرض خاص";
        }

        private static (string Caption, string Value) BuildPromotionBadge(
            string offerMode,
            string rewardType,
            string? discountType,
            decimal discountValue,
            string discountValueText,
            int? rewardQuantity,
            string specialPriceText)
        {
            if (!string.Equals(offerMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                    ? ("خصم", $"{discountValue:0.##}%")
                    : ("وفر", discountValueText);
            }

            if (string.Equals(rewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
                return ("مجانًا", $"{Math.Max(1, rewardQuantity.GetValueOrDefault(1))}");
            if (string.Equals(rewardType, "FreeService", StringComparison.OrdinalIgnoreCase))
                return ("مكافأة", "خدمة مجانية");
            if (string.Equals(rewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase))
                return ("سعر خاص", specialPriceText);
            if (string.Equals(rewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase))
                return ("خصم", $"{discountValue:0.##}%");
            if (string.Equals(rewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase))
                return ("خصم", discountValueText);

            return ("عرض", "خاص");
        }

        // =============================================================
        //  صفحة تواصل معنا - تخدم صفحة Contact.cshtml
        // =============================================================
        public IActionResult Contact()
        {
            return View("~/Views/Home/Contact.cshtml");
        }

        // =============================================================
        //  معالجة إرسال رسالة التواصل (موجودة في contact.js)
        // =============================================================
        [HttpPost]
        public IActionResult SendMessage([FromBody] ContactMessageDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.SenderName) || string.IsNullOrWhiteSpace(model.MessageBody))
            {
                return Json(new { success = false, message = "البيانات غير صالحة." });
            }

            return Json(new { success = true, message = "تم إرسال رسالتك بنجاح." });
        }

    }
}
