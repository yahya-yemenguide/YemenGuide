using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Core.Entities;
using YemenGuide.Data;

namespace YemenGuide.Web.Services.Advertisements
{
    /// <summary>
    /// محرك عرض إعلانات العميل.
    /// المرحلة الأولى: الرئيسية (HomeSlider / HomeBanner / FeaturedPlaces).
    /// المرحلة الثانية: الاستكشاف (SearchTop / CategoryTop / CityPage).
    /// المرحلة الثالثة: التفاصيل والخريطة ونسبة بدء/إنشاء الحجز.
    /// مع التحقق من الحالة والدفع والمدة والميزانية والجهاز والاستهداف والتكرار.
    /// </summary>
    public sealed class CustomerAdvertisementService : ICustomerAdvertisementService
    {
        private const string VisitorCookieName = "yg_ad_visitor";
        private const string SessionCookieName = "yg_ad_session";

        private const string StatusActive = "Active";
        private const string PaymentPaid = "Paid";
        private const string BillingFlat = "Flat";
        private const string BillingCpc = "CPC";
        private const string BillingCpm = "CPM";
        private const string TargetingAll = "All";
        private const string TargetingCustom = "Custom";
        private const string DeviceAll = "All";
        private const string DeviceDesktop = "Desktop";
        private const string DeviceMobile = "Mobile";
        private const string DestinationPromotion = "Promotion";
        private const string DestinationBooking = "Booking";

        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CustomerAdvertisementService> _logger;

        public CustomerAdvertisementService(
            AppDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<CustomerAdvertisementService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<CustomerHomeAdvertisementsDto> GetHomeAdvertisementsAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new CustomerHomeAdvertisementsDto();
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return result;
            }

            var now = DateTime.Now;
            var today = now.Date;
            var identity = GetOrCreateIdentity(httpContext);
            var deviceType = DetectDeviceType(httpContext.Request.Headers["User-Agent"].ToString());
            var targetDevice = deviceType == "Tablet" ? DeviceMobile : deviceType;

            string[] homePlacements =
            {
                CustomerAdvertisementPlacements.HomeSlider,
                CustomerAdvertisementPlacements.HomeBanner,
                CustomerAdvertisementPlacements.FeaturedPlaces
            };

            // لا نستخدم AdvertisementMedium navigation هنا عمداً؛
            // قاعدة البيانات تسمح بعدة وسائط بينما الـEF الحالي ما زال يملك navigation مفرداً.
            // لذلك نقرأ الوسائط مباشرة من DbSet لتبقى المرحلة آمنة بدون تعديل الـDB/DbContext.
            var candidates = await _context.Advertisements
                .AsNoTracking()
                .Where(ad =>
                    ad.Status == StatusActive &&
                    ad.PaymentStatus == PaymentPaid &&
                    ad.StartAt.HasValue &&
                    ad.StartAt.Value <= now &&
                    ad.EndAt.HasValue &&
                    ad.EndAt.Value > now &&
                    homePlacements.Contains(ad.PlacementTypeSnapshot) &&
                    ad.Place.Status == "Active" &&
                    (ad.DeviceTarget == DeviceAll ||
                     ad.DeviceTarget == targetDevice) &&
                    (!ad.MaxImpressions.HasValue ||
                     ad.ImpressionsCount < ad.MaxImpressions.Value) &&
                    (!ad.MaxClicks.HasValue ||
                     ad.ClicksCount < ad.MaxClicks.Value) &&
                    (ad.BillingModelSnapshot == BillingFlat ||
                     !ad.TotalBudget.HasValue ||
                     ad.SpentAmount < ad.TotalBudget.Value))
                .OrderByDescending(ad => ad.PrioritySnapshot)
                .ThenBy(ad => ad.LastServedAt ?? DateTime.MinValue)
                .ThenByDescending(ad => ad.RotationWeight)
                .ThenBy(ad => ad.AdvertisementId)
                .Take(120)
                .Select(ad => new CandidateRow
                {
                    AdvertisementId = ad.AdvertisementId,
                    PlaceId = ad.PlaceId,
                    PromotionId = ad.PromotionId,
                    PlacementType = ad.PlacementTypeSnapshot,
                    AdvertisementTitle = ad.AdvertisementTitle,
                    ShortDescription = ad.ShortDescription,
                    ButtonText = ad.ButtonText,
                    ImagePath = ad.ImagePath,
                    BillingModel = ad.BillingModelSnapshot,
                    CostPerClick = ad.CostPerClickSnapshot,
                    CostPerThousand = ad.CostPerThousandSnapshot,
                    TotalBudget = ad.TotalBudget,
                    DailyBudget = ad.DailyBudget,
                    SpentAmount = ad.SpentAmount,
                    FrequencyCap = ad.FrequencyCapPerUserPerDay,
                    TargetingMode = ad.TargetingMode,
                    DeviceTarget = ad.DeviceTarget,
                    DestinationType = ad.DestinationType,
                    DestinationPath = ad.DestinationPath,
                    Priority = ad.PrioritySnapshot,
                    RotationWeight = ad.RotationWeight,
                    LastServedAt = ad.LastServedAt,
                    PlaceName = ad.Place.PlaceNameAndDesc,
                    PlaceCityId = ad.Place.CityId,
                    PlaceCategoryId = ad.Place.CategoryId,
                    CityName = ad.Place.City != null
                        ? ad.Place.City.CityName
                        : string.Empty,
                    CategoryName = ad.Place.Category != null
                        ? ad.Place.Category.CategoryName
                        : string.Empty,
                    PlaceCoverImage = ad.Place.CoverImage,
                    PlaceLogoImage = ad.Place.LogoImage,
                    PromotionImage = ad.Promotion != null
                        ? ad.Promotion.ImagePath
                        : null
                })
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                return result;
            }

            var candidateIds = candidates
                .Select(item => item.AdvertisementId)
                .Distinct()
                .ToArray();

            var targetRows = await _context.AdvertisementTargets
                .AsNoTracking()
                .Where(target => candidateIds.Contains(target.AdvertisementId))
                .Select(target => new TargetRow
                {
                    AdvertisementId = target.AdvertisementId,
                    CityId = target.CityId,
                    CategoryId = target.CategoryId
                })
                .ToListAsync(cancellationToken);

            var targetsByAdvertisement = targetRows
                .GroupBy(target => target.AdvertisementId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            var dailySpendByAdvertisement = await _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    candidateIds.Contains(evt.AdvertisementId) &&
                    evt.EventAt >= today &&
                    evt.EventAt < today.AddDays(1))
                .GroupBy(evt => evt.AdvertisementId)
                .Select(group => new
                {
                    AdvertisementId = group.Key,
                    Amount = group.Sum(evt => evt.CostAmount)
                })
                .ToDictionaryAsync(
                    item => item.AdvertisementId,
                    item => item.Amount,
                    cancellationToken);

            Dictionary<int, int> frequencyByAdvertisement;
            if (identity.UserId.HasValue)
            {
                frequencyByAdvertisement = await _context.AdvertisementEvents
                    .AsNoTracking()
                    .Where(evt =>
                        candidateIds.Contains(evt.AdvertisementId) &&
                        evt.EventType == CustomerAdvertisementEventTypes.Impression &&
                        evt.EventAt >= today &&
                        evt.EventAt < today.AddDays(1) &&
                        evt.UserId == identity.UserId.Value)
                    .GroupBy(evt => evt.AdvertisementId)
                    .Select(group => new
                    {
                        AdvertisementId = group.Key,
                        Count = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.AdvertisementId,
                        item => item.Count,
                        cancellationToken);
            }
            else
            {
                frequencyByAdvertisement = await _context.AdvertisementEvents
                    .AsNoTracking()
                    .Where(evt =>
                        candidateIds.Contains(evt.AdvertisementId) &&
                        evt.EventType == CustomerAdvertisementEventTypes.Impression &&
                        evt.EventAt >= today &&
                        evt.EventAt < today.AddDays(1) &&
                        evt.VisitorHash == identity.VisitorHash)
                    .GroupBy(evt => evt.AdvertisementId)
                    .Select(group => new
                    {
                        AdvertisementId = group.Key,
                        Count = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.AdvertisementId,
                        item => item.Count,
                        cancellationToken);
            }

            var eligible = candidates
                .Where(candidate =>
                    HasBudgetCapacity(
                        candidate,
                        dailySpendByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var dailySpent)
                                ? dailySpent
                                : 0m) &&
                    HasFrequencyCapacity(
                        candidate,
                        frequencyByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var frequency)
                                ? frequency
                                : 0) &&
                    MatchesHomeTargeting(
                        candidate,
                        targetsByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var targets)
                                ? targets
                                : null))
                .ToList();

            if (eligible.Count == 0)
            {
                return result;
            }

            var mediaRows = await _context.AdvertisementMedia
                .AsNoTracking()
                .Where(media =>
                    candidateIds.Contains(media.AdvertisementId) &&
                    media.IsActive == true)
                .OrderByDescending(media => media.IsPrimary)
                .ThenBy(media => media.DisplayOrder)
                .ThenBy(media => media.AdvertisementMediaId)
                .Select(media => new MediaRow
                {
                    AdvertisementId = media.AdvertisementId,
                    MediaType = media.MediaType,
                    FilePath = media.FilePath,
                    ThumbnailPath = media.ThumbnailPath,
                    IsPrimary = media.IsPrimary,
                    DisplayOrder = media.DisplayOrder
                })
                .ToListAsync(cancellationToken);

            var mediaByAdvertisement = mediaRows
                .GroupBy(media => media.AdvertisementId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First());

            CandidateRow? slider = PickForPlacement(
                eligible,
                CustomerAdvertisementPlacements.HomeSlider);

            CandidateRow? banner = PickForPlacement(
                eligible,
                CustomerAdvertisementPlacements.HomeBanner);

            // HomeSlider وHomeBanner يتشاركان Slot رئيسياً واحداً.
            // لا نقارن أولوية باقتين مختلفتين؛ ندوّر بين الـSlots حسب أقدم LastServedAt.
            var primary = PickPrimaryHomeSlot(slider, banner);

            CandidateRow? featured = PickForPlacement(
                eligible,
                CustomerAdvertisementPlacements.FeaturedPlaces,
                primary?.PlaceId);

            if (primary != null)
            {
                result.PrimaryAdvertisement = ToCustomerDto(
                    primary,
                    mediaByAdvertisement.TryGetValue(
                        primary.AdvertisementId,
                        out var primaryMedia)
                            ? primaryMedia
                            : null);
            }

            if (featured != null)
            {
                result.FeaturedAdvertisement = ToCustomerDto(
                    featured,
                    mediaByAdvertisement.TryGetValue(
                        featured.AdvertisementId,
                        out var featuredMedia)
                            ? featuredMedia
                            : null);
            }

            await MarkAsServedAsync(
                new[] { primary, featured }
                    .Where(item => item != null)
                    .Select(item => item!.AdvertisementId)
                    .Distinct()
                    .ToArray(),
                now,
                cancellationToken);

            return result;
        }

        public async Task<IReadOnlyList<CustomerAdvertisementDto>> GetDiscoveryAdvertisementsAsync(
            CustomerAdvertisementDiscoveryRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request == null ||
                request.EligiblePlaceIds == null ||
                request.EligiblePlaceIds.Count == 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var eligiblePlaceIds = request.EligiblePlaceIds
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet();

            if (eligiblePlaceIds.Count == 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            /*
             * النظام الجديد في صفحة Search:
             * لا يوجد أكثر من "مكان ظهور" في واجهة العميل.
             * أي إعلان مدفوع ونشط وكانت منشأته ضمن نتائج البحث الحقيقية
             * يدخل نفس السلايدر العلوي، مهما كانت قيمة PlacementTypeSnapshot
             * القديمة المخزنة من النظام السابق.
             *
             * نُبقي PlacementTypeSnapshot كما هو فقط للتوافق مع السجلات القديمة
             * والإحصائيات وقاعدة البيانات، ولا نستخدمه لاختيار مكان مختلف هنا.
             *
             * الخريطة ليست باقة مستقلة: GetMapAdvertisementsAsync يأخذ من
             * نفس قناة SearchTop ويستخدمها كتمييز إضافي تلقائي فقط.
             */
            var now = DateTime.Now;
            var today = now.Date;
            var identity = GetOrCreateIdentity(httpContext);
            var deviceType = DetectDeviceType(
                httpContext.Request.Headers["User-Agent"].ToString());

            var targetDevice = deviceType == "Tablet"
                ? DeviceMobile
                : deviceType;

            var candidates = await _context.Advertisements
                .AsNoTracking()
                .Where(ad =>
                    ad.Status == StatusActive &&
                    ad.PaymentStatus == PaymentPaid &&
                    ad.StartAt.HasValue &&
                    ad.StartAt.Value <= now &&
                    ad.EndAt.HasValue &&
                    ad.EndAt.Value > now &&
                    ad.Place.Status == "Active" &&
                    eligiblePlaceIds.Contains(ad.PlaceId) &&
                    (ad.DeviceTarget == DeviceAll ||
                     ad.DeviceTarget == targetDevice) &&
                    (!ad.MaxImpressions.HasValue ||
                     ad.ImpressionsCount < ad.MaxImpressions.Value) &&
                    (!ad.MaxClicks.HasValue ||
                     ad.ClicksCount < ad.MaxClicks.Value) &&
                    (ad.BillingModelSnapshot == BillingFlat ||
                     !ad.TotalBudget.HasValue ||
                     ad.SpentAmount < ad.TotalBudget.Value))
                .OrderBy(ad => ad.LastServedAt ?? DateTime.MinValue)
                .ThenByDescending(ad => ad.PrioritySnapshot)
                .ThenByDescending(ad => ad.RotationWeight)
                .ThenBy(ad => ad.AdvertisementId)
                .Take(120)
                .Select(ad => new CandidateRow
                {
                    AdvertisementId = ad.AdvertisementId,
                    PlaceId = ad.PlaceId,
                    PromotionId = ad.PromotionId,
                    PlacementType = ad.PlacementTypeSnapshot,
                    AdvertisementTitle = ad.AdvertisementTitle,
                    ShortDescription = ad.ShortDescription,
                    ButtonText = ad.ButtonText,
                    ImagePath = ad.ImagePath,
                    BillingModel = ad.BillingModelSnapshot,
                    CostPerClick = ad.CostPerClickSnapshot,
                    CostPerThousand = ad.CostPerThousandSnapshot,
                    TotalBudget = ad.TotalBudget,
                    DailyBudget = ad.DailyBudget,
                    SpentAmount = ad.SpentAmount,
                    FrequencyCap = ad.FrequencyCapPerUserPerDay,
                    TargetingMode = ad.TargetingMode,
                    DeviceTarget = ad.DeviceTarget,
                    DestinationType = ad.DestinationType,
                    DestinationPath = ad.DestinationPath,
                    Priority = ad.PrioritySnapshot,
                    RotationWeight = ad.RotationWeight,
                    LastServedAt = ad.LastServedAt,
                    PlaceName = ad.Place.PlaceNameAndDesc,
                    PlaceCityId = ad.Place.CityId,
                    PlaceCategoryId = ad.Place.CategoryId,
                    CityName = ad.Place.City != null
                        ? ad.Place.City.CityName
                        : string.Empty,
                    CategoryName = ad.Place.Category != null
                        ? ad.Place.Category.CategoryName
                        : string.Empty,
                    PlaceCoverImage = ad.Place.CoverImage,
                    PlaceLogoImage = ad.Place.LogoImage,
                    PromotionImage = ad.Promotion != null
                        ? ad.Promotion.ImagePath
                        : null
                })
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var candidateIds = candidates
                .Select(candidate => candidate.AdvertisementId)
                .Distinct()
                .ToArray();

            var targetRows = await _context.AdvertisementTargets
                .AsNoTracking()
                .Where(target =>
                    candidateIds.Contains(target.AdvertisementId))
                .Select(target => new TargetRow
                {
                    AdvertisementId = target.AdvertisementId,
                    CityId = target.CityId,
                    CategoryId = target.CategoryId
                })
                .ToListAsync(cancellationToken);

            var targetsByAdvertisement = targetRows
                .GroupBy(target => target.AdvertisementId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            var dailySpendByAdvertisement = await _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    candidateIds.Contains(evt.AdvertisementId) &&
                    evt.EventAt >= today &&
                    evt.EventAt < today.AddDays(1))
                .GroupBy(evt => evt.AdvertisementId)
                .Select(group => new
                {
                    AdvertisementId = group.Key,
                    Amount = group.Sum(evt => evt.CostAmount)
                })
                .ToDictionaryAsync(
                    item => item.AdvertisementId,
                    item => item.Amount,
                    cancellationToken);

            const int SearchSliderMaximumAdvertisements = 5;

            var eligible = candidates
                .Where(candidate =>
                    HasBudgetCapacity(
                        candidate,
                        dailySpendByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var dailySpent)
                                ? dailySpent
                                : 0m) &&
                    MatchesDiscoveryTargeting(
                        candidate,
                        targetsByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var targets)
                                ? targets
                                : null,
                        request))
                // العدالة أولاً: الأقل خدمة سابقاً ثم الأولوية والوزن.
                .OrderBy(candidate =>
                    candidate.LastServedAt ?? DateTime.MinValue)
                .ThenByDescending(candidate => candidate.Priority)
                .ThenByDescending(candidate =>
                    Math.Max(1, candidate.RotationWeight))
                .ThenBy(candidate => candidate.AdvertisementId)
                // لا نحشو الـHero بعشرات الإعلانات في نفس الزيارة.
                .Take(SearchSliderMaximumAdvertisements)
                .ToList();

            if (eligible.Count == 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var selectedIds = eligible
                .Select(item => item.AdvertisementId)
                .Distinct()
                .ToArray();

            var mediaRows = await _context.AdvertisementMedia
                .AsNoTracking()
                .Where(media =>
                    selectedIds.Contains(media.AdvertisementId) &&
                    media.IsActive == true)
                .OrderByDescending(media => media.IsPrimary)
                .ThenBy(media => media.DisplayOrder)
                .ThenBy(media => media.AdvertisementMediaId)
                .Select(media => new MediaRow
                {
                    AdvertisementId = media.AdvertisementId,
                    MediaType = media.MediaType,
                    FilePath = media.FilePath,
                    ThumbnailPath = media.ThumbnailPath,
                    IsPrimary = media.IsPrimary,
                    DisplayOrder = media.DisplayOrder
                })
                .ToListAsync(cancellationToken);

            var mediaByAdvertisement = mediaRows
                .GroupBy(media => media.AdvertisementId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First());

            /*
             * لا نغيّر LastServedAt هنا عند مجرد تحميل السلايدر.
             * سيُحدّث فعليًا عند تسجيل Impression للشريحة التي شاهدها العميل،
             * وهذا يمنع احتساب الإعلانات المخفية على أنها عُرضت.
             */
            return eligible
                .Select(candidate => ToCustomerDto(
                    candidate,
                    mediaByAdvertisement.TryGetValue(
                        candidate.AdvertisementId,
                        out var media)
                            ? media
                            : null))
                .ToList();
        }

        // توافق مع أي كود قديم ما زال يطلب إعلانًا واحدًا فقط.
        public async Task<CustomerAdvertisementDto?> GetDiscoveryAdvertisementAsync(
            CustomerAdvertisementDiscoveryRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var items = await GetDiscoveryAdvertisementsAsync(
                request,
                cancellationToken);

            return items.FirstOrDefault();
        }

        public async Task<CustomerAdvertisementDto?> GetPlaceDetailsAdvertisementAsync(
            CustomerAdvertisementPlaceDetailsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.CurrentPlaceId <= 0)
            {
                return null;
            }

            var items = await GetPlacementAdvertisementsCoreAsync(
                CustomerAdvertisementPlacements.PlaceDetails,
                eligiblePlaceIds: null,
                excludePlaceId: request.CurrentPlaceId,
                contextCityId: request.CurrentCityId,
                contextCategoryId: request.CurrentCategoryId,
                userLatitude: null,
                userLongitude: null,
                maxAdvertisements: 1,
                cancellationToken: cancellationToken);

            return items.FirstOrDefault();
        }

        public async Task<IReadOnlyList<CustomerAdvertisementDto>> GetMapAdvertisementsAsync(
            CustomerAdvertisementMapRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request == null ||
                request.EligiblePlaceIds == null ||
                request.EligiblePlaceIds.Count == 0 ||
                request.UserLatitude < -90d || request.UserLatitude > 90d ||
                request.UserLongitude < -180d || request.UserLongitude > 180d)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var eligiblePlaceIds = request.EligiblePlaceIds
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet();

            if (eligiblePlaceIds.Count == 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            // الخريطة امتداد مجاني تلقائي لحملة SearchTop وليست Placement مستقلاً.
            return await GetPlacementAdvertisementsCoreAsync(
                CustomerAdvertisementPlacements.SearchTop,
                eligiblePlaceIds,
                excludePlaceId: null,
                contextCityId: 0,
                contextCategoryId: request.CategoryId.GetValueOrDefault(),
                userLatitude: request.UserLatitude,
                userLongitude: request.UserLongitude,
                maxAdvertisements: Math.Clamp(request.MaxAdvertisements, 1, 2),
                cancellationToken: cancellationToken);
        }

        public async Task<int?> ResolveAttributionAdvertisementIdAsync(
            int? advertisementId,
            int placeId,
            CancellationToken cancellationToken = default)
        {
            if (!advertisementId.HasValue || advertisementId.Value <= 0 || placeId <= 0)
            {
                return null;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var ad = await _context.Advertisements
                .AsNoTracking()
                .Where(item =>
                    item.AdvertisementId == advertisementId.Value &&
                    item.PlaceId == placeId &&
                    item.PaymentStatus == PaymentPaid)
                .Select(item => new
                {
                    item.AdvertisementId,
                    item.PlaceId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (ad == null)
            {
                return null;
            }

            var identity = GetOrCreateIdentity(httpContext);
            var cutoff = DateTime.Now.AddDays(-7);

            var events = _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    evt.AdvertisementId == ad.AdvertisementId &&
                    evt.EventAt >= cutoff &&
                    (evt.EventType == CustomerAdvertisementEventTypes.Click ||
                     evt.EventType == CustomerAdvertisementEventTypes.BookingStarted));

            bool hasAttribution;
            if (identity.UserId.HasValue)
            {
                // نسمح باستمرار النسبة إذا ضغط الزائر على الإعلان قبل تسجيل
                // الدخول ثم أكمل الحجز بعده في نفس جلسة المتصفح.
                hasAttribution = await events.AnyAsync(
                    evt =>
                        evt.UserId == identity.UserId.Value ||
                        (evt.VisitorHash == identity.VisitorHash &&
                         evt.SessionKey == identity.SessionKey),
                    cancellationToken);
            }
            else
            {
                hasAttribution = await events.AnyAsync(
                    evt => evt.VisitorHash == identity.VisitorHash &&
                           evt.SessionKey == identity.SessionKey,
                    cancellationToken);
            }

            return hasAttribution
                ? ad.AdvertisementId
                : null;
        }

        public async Task<CustomerAdvertisementTrackResultDto> RecordBookingStartedAsync(
            CustomerAdvertisementBookingStartRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.AdvertisementId <= 0 || request.PlaceId <= 0)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "بيانات بداية الحجز غير صحيحة.");
            }

            var resolvedId = await ResolveAttributionAdvertisementIdAsync(
                request.AdvertisementId,
                request.PlaceId,
                cancellationToken);

            if (!resolvedId.HasValue)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "تعذر التحقق من مصدر الإعلان.");
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "تعذر تحديد جلسة العميل.");
            }

            var identity = GetOrCreateIdentity(httpContext);
            var now = DateTime.Now;
            var cutoff = now.AddDays(-1);

            var duplicateQuery = _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    evt.AdvertisementId == resolvedId.Value &&
                    evt.EventType == CustomerAdvertisementEventTypes.BookingStarted &&
                    evt.EventAt >= cutoff);

            bool alreadyRecorded = identity.UserId.HasValue
                ? await duplicateQuery.AnyAsync(
                    evt =>
                        evt.UserId == identity.UserId.Value ||
                        (evt.VisitorHash == identity.VisitorHash &&
                         evt.SessionKey == identity.SessionKey),
                    cancellationToken)
                : await duplicateQuery.AnyAsync(
                    evt => evt.VisitorHash == identity.VisitorHash &&
                           evt.SessionKey == identity.SessionKey,
                    cancellationToken);

            if (alreadyRecorded)
            {
                return CustomerAdvertisementTrackResultDto.Ok(
                    alreadyRecorded: true);
            }

            var advertisement = await _context.Advertisements
                .FirstOrDefaultAsync(
                    item => item.AdvertisementId == resolvedId.Value &&
                            item.PlaceId == request.PlaceId,
                    cancellationToken);

            if (advertisement == null)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "الإعلان غير موجود.");
            }

            var requestKey = NormalizeRequestKey(request.RequestKey);
            var deviceType = DetectDeviceType(
                httpContext.Request.Headers["User-Agent"].ToString());

            _context.AdvertisementEvents.Add(new AdvertisementEvent
            {
                AdvertisementId = advertisement.AdvertisementId,
                UserId = identity.UserId,
                BookingId = null,
                EventType = CustomerAdvertisementEventTypes.BookingStarted,
                PlacementType = advertisement.PlacementTypeSnapshot,
                SessionKey = identity.SessionKey,
                VisitorHash = identity.VisitorHash,
                RequestKey = requestKey,
                DeviceType = deviceType,
                IpHash = null,
                UserAgentHash = HashValue(
                    httpContext.Request.Headers["User-Agent"].ToString()),
                CostAmount = 0m,
                EventAt = now
            });

            advertisement.BookingStartsCount += 1;
            advertisement.UpdatedAt = now;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Duplicate/failed BookingStarted event for advertisement {AdvertisementId}",
                    advertisement.AdvertisementId);

                return CustomerAdvertisementTrackResultDto.Ok(
                    alreadyRecorded: true);
            }

            return CustomerAdvertisementTrackResultDto.Ok();
        }

        public async Task RecordBookingCompletedAsync(
            int advertisementId,
            int bookingId,
            int placeId,
            CancellationToken cancellationToken = default)
        {
            if (advertisementId <= 0 || bookingId <= 0 || placeId <= 0)
            {
                return;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return;
            }

            var identity = GetOrCreateIdentity(httpContext);

            var booking = await _context.Bookings
                .AsNoTracking()
                .Where(item =>
                    item.BookingId == bookingId &&
                    item.PlaceId == placeId &&
                    item.AdvertisementId == advertisementId)
                .Select(item => new
                {
                    item.BookingId,
                    item.CustomerId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (booking == null ||
                (identity.UserId.HasValue && booking.CustomerId != identity.UserId.Value))
            {
                return;
            }

            bool duplicate = await _context.AdvertisementEvents
                .AsNoTracking()
                .AnyAsync(
                    evt =>
                        evt.AdvertisementId == advertisementId &&
                        evt.BookingId == bookingId &&
                        evt.EventType == CustomerAdvertisementEventTypes.BookingCompleted,
                    cancellationToken);

            if (duplicate)
            {
                return;
            }

            var advertisement = await _context.Advertisements
                .FirstOrDefaultAsync(
                    item => item.AdvertisementId == advertisementId &&
                            item.PlaceId == placeId,
                    cancellationToken);

            if (advertisement == null)
            {
                return;
            }

            var now = DateTime.Now;
            var deviceType = DetectDeviceType(
                httpContext.Request.Headers["User-Agent"].ToString());

            _context.AdvertisementEvents.Add(new AdvertisementEvent
            {
                AdvertisementId = advertisementId,
                UserId = identity.UserId,
                BookingId = bookingId,
                EventType = CustomerAdvertisementEventTypes.BookingCompleted,
                PlacementType = advertisement.PlacementTypeSnapshot,
                SessionKey = identity.SessionKey,
                VisitorHash = identity.VisitorHash,
                RequestKey = $"booking_{bookingId}",
                DeviceType = deviceType,
                IpHash = null,
                UserAgentHash = HashValue(
                    httpContext.Request.Headers["User-Agent"].ToString()),
                CostAmount = 0m,
                EventAt = now
            });

            advertisement.ConversionsCount += 1;
            advertisement.UpdatedAt = now;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Duplicate/failed BookingCompleted event for advertisement {AdvertisementId}, booking {BookingId}",
                    advertisementId,
                    bookingId);
            }
        }

        private async Task<IReadOnlyList<CustomerAdvertisementDto>> GetPlacementAdvertisementsCoreAsync(
            string placement,
            IReadOnlyCollection<int>? eligiblePlaceIds,
            int? excludePlaceId,
            int contextCityId,
            int contextCategoryId,
            double? userLatitude,
            double? userLongitude,
            int maxAdvertisements,
            CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null || maxAdvertisements <= 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var now = DateTime.Now;
            var today = now.Date;
            var identity = GetOrCreateIdentity(httpContext);
            var deviceType = DetectDeviceType(
                httpContext.Request.Headers["User-Agent"].ToString());
            var targetDevice = deviceType == "Tablet"
                ? DeviceMobile
                : deviceType;

            var query = _context.Advertisements
                .AsNoTracking()
                .Where(ad =>
                    ad.Status == StatusActive &&
                    ad.PaymentStatus == PaymentPaid &&
                    ad.StartAt.HasValue &&
                    ad.StartAt.Value <= now &&
                    ad.EndAt.HasValue &&
                    ad.EndAt.Value > now &&
                    ad.PlacementTypeSnapshot == placement &&
                    ad.Place.Status == "Active" &&
                    (ad.DeviceTarget == DeviceAll ||
                     ad.DeviceTarget == targetDevice) &&
                    (!ad.MaxImpressions.HasValue ||
                     ad.ImpressionsCount < ad.MaxImpressions.Value) &&
                    (!ad.MaxClicks.HasValue ||
                     ad.ClicksCount < ad.MaxClicks.Value) &&
                    (ad.BillingModelSnapshot == BillingFlat ||
                     !ad.TotalBudget.HasValue ||
                     ad.SpentAmount < ad.TotalBudget.Value));

            if (eligiblePlaceIds != null && eligiblePlaceIds.Count > 0)
            {
                // نحول القائمة إلى Array قبل إدخالها في استعلام EF حتى تترجم إلى IN(...)
                // بصورة مستقرة مهما كان نوع IReadOnlyCollection المرسل من الواجهة.
                var eligibleIds = eligiblePlaceIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToArray();

                if (eligibleIds.Length == 0)
                {
                    return Array.Empty<CustomerAdvertisementDto>();
                }

                query = query.Where(ad => eligibleIds.Contains(ad.PlaceId));
            }

            if (excludePlaceId.HasValue && excludePlaceId.Value > 0)
            {
                query = query.Where(ad => ad.PlaceId != excludePlaceId.Value);
            }

            var candidates = await query
                .OrderByDescending(ad => ad.PrioritySnapshot)
                .ThenBy(ad => ad.LastServedAt ?? DateTime.MinValue)
                .ThenByDescending(ad => ad.RotationWeight)
                .ThenBy(ad => ad.AdvertisementId)
                .Take(120)
                .Select(ad => new CandidateRow
                {
                    AdvertisementId = ad.AdvertisementId,
                    PlaceId = ad.PlaceId,
                    PromotionId = ad.PromotionId,
                    PlacementType = ad.PlacementTypeSnapshot,
                    AdvertisementTitle = ad.AdvertisementTitle,
                    ShortDescription = ad.ShortDescription,
                    ButtonText = ad.ButtonText,
                    ImagePath = ad.ImagePath,
                    BillingModel = ad.BillingModelSnapshot,
                    CostPerClick = ad.CostPerClickSnapshot,
                    CostPerThousand = ad.CostPerThousandSnapshot,
                    TotalBudget = ad.TotalBudget,
                    DailyBudget = ad.DailyBudget,
                    SpentAmount = ad.SpentAmount,
                    FrequencyCap = ad.FrequencyCapPerUserPerDay,
                    TargetingMode = ad.TargetingMode,
                    DeviceTarget = ad.DeviceTarget,
                    DestinationType = ad.DestinationType,
                    DestinationPath = ad.DestinationPath,
                    Priority = ad.PrioritySnapshot,
                    RotationWeight = ad.RotationWeight,
                    LastServedAt = ad.LastServedAt,
                    TargetLatitude = ad.TargetLatitude,
                    TargetLongitude = ad.TargetLongitude,
                    TargetRadiusKm = ad.TargetRadiusKm,
                    PlaceName = ad.Place.PlaceNameAndDesc,
                    PlaceCityId = ad.Place.CityId,
                    PlaceCategoryId = ad.Place.CategoryId,
                    CityName = ad.Place.City != null
                        ? ad.Place.City.CityName
                        : string.Empty,
                    CategoryName = ad.Place.Category != null
                        ? ad.Place.Category.CategoryName
                        : string.Empty,
                    PlaceCoverImage = ad.Place.CoverImage,
                    PlaceLogoImage = ad.Place.LogoImage,
                    PromotionImage = ad.Promotion != null
                        ? ad.Promotion.ImagePath
                        : null
                })
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var candidateIds = candidates
                .Select(item => item.AdvertisementId)
                .Distinct()
                .ToArray();

            var targetRows = await _context.AdvertisementTargets
                .AsNoTracking()
                .Where(target => candidateIds.Contains(target.AdvertisementId))
                .Select(target => new TargetRow
                {
                    AdvertisementId = target.AdvertisementId,
                    CityId = target.CityId,
                    CategoryId = target.CategoryId
                })
                .ToListAsync(cancellationToken);

            var targetsByAdvertisement = targetRows
                .GroupBy(target => target.AdvertisementId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var dailySpendByAdvertisement = await _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    candidateIds.Contains(evt.AdvertisementId) &&
                    evt.EventAt >= today &&
                    evt.EventAt < today.AddDays(1))
                .GroupBy(evt => evt.AdvertisementId)
                .Select(group => new
                {
                    AdvertisementId = group.Key,
                    Amount = group.Sum(evt => evt.CostAmount)
                })
                .ToDictionaryAsync(
                    item => item.AdvertisementId,
                    item => item.Amount,
                    cancellationToken);

            Dictionary<int, int> frequencyByAdvertisement;
            if (identity.UserId.HasValue)
            {
                frequencyByAdvertisement = await _context.AdvertisementEvents
                    .AsNoTracking()
                    .Where(evt =>
                        candidateIds.Contains(evt.AdvertisementId) &&
                        evt.EventType == CustomerAdvertisementEventTypes.Impression &&
                        evt.EventAt >= today &&
                        evt.EventAt < today.AddDays(1) &&
                        evt.UserId == identity.UserId.Value)
                    .GroupBy(evt => evt.AdvertisementId)
                    .Select(group => new
                    {
                        AdvertisementId = group.Key,
                        Count = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.AdvertisementId,
                        item => item.Count,
                        cancellationToken);
            }
            else
            {
                frequencyByAdvertisement = await _context.AdvertisementEvents
                    .AsNoTracking()
                    .Where(evt =>
                        candidateIds.Contains(evt.AdvertisementId) &&
                        evt.EventType == CustomerAdvertisementEventTypes.Impression &&
                        evt.EventAt >= today &&
                        evt.EventAt < today.AddDays(1) &&
                        evt.VisitorHash == identity.VisitorHash)
                    .GroupBy(evt => evt.AdvertisementId)
                    .Select(group => new
                    {
                        AdvertisementId = group.Key,
                        Count = group.Count()
                    })
                    .ToDictionaryAsync(
                        item => item.AdvertisementId,
                        item => item.Count,
                        cancellationToken);
            }

            var eligible = candidates
                .Where(candidate =>
                    HasBudgetCapacity(
                        candidate,
                        dailySpendByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var dailySpent)
                                ? dailySpent
                                : 0m) &&
                    HasFrequencyCapacity(
                        candidate,
                        frequencyByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var frequency)
                                ? frequency
                                : 0) &&
                    MatchesContextTargeting(
                        candidate,
                        targetsByAdvertisement.TryGetValue(
                            candidate.AdvertisementId,
                            out var targets)
                                ? targets
                                : null,
                        contextCityId,
                        contextCategoryId) &&
                    MatchesGeoTargeting(
                        candidate,
                        userLatitude,
                        userLongitude))
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.LastServedAt ?? DateTime.MinValue)
                .ThenByDescending(candidate => Math.Max(1, candidate.RotationWeight))
                .ThenBy(candidate => candidate.AdvertisementId)
                .ToList();

            if (eligible.Count == 0)
            {
                return Array.Empty<CustomerAdvertisementDto>();
            }

            var selected = eligible
                .GroupBy(candidate => candidate.PlaceId)
                .Select(group => group.First())
                .Take(maxAdvertisements)
                .ToList();

            var selectedIds = selected
                .Select(item => item.AdvertisementId)
                .ToArray();

            var mediaRows = await _context.AdvertisementMedia
                .AsNoTracking()
                .Where(media =>
                    selectedIds.Contains(media.AdvertisementId) &&
                    media.IsActive == true)
                .OrderByDescending(media => media.IsPrimary)
                .ThenBy(media => media.DisplayOrder)
                .ThenBy(media => media.AdvertisementMediaId)
                .Select(media => new MediaRow
                {
                    AdvertisementId = media.AdvertisementId,
                    MediaType = media.MediaType,
                    FilePath = media.FilePath,
                    ThumbnailPath = media.ThumbnailPath,
                    IsPrimary = media.IsPrimary,
                    DisplayOrder = media.DisplayOrder
                })
                .ToListAsync(cancellationToken);

            var mediaByAdvertisement = mediaRows
                .GroupBy(media => media.AdvertisementId)
                .ToDictionary(group => group.Key, group => group.First());

            await MarkAsServedAsync(
                selectedIds,
                now,
                cancellationToken);

            return selected
                .Select(candidate => ToCustomerDto(
                    candidate,
                    mediaByAdvertisement.TryGetValue(
                        candidate.AdvertisementId,
                        out var media)
                            ? media
                            : null))
                .ToList();
        }

        public Task<CustomerAdvertisementTrackResultDto> RecordImpressionAsync(
            CustomerAdvertisementTrackRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return RecordEventAsync(
                request,
                CustomerAdvertisementEventTypes.Impression,
                includeRedirect: false,
                cancellationToken);
        }

        public Task<CustomerAdvertisementTrackResultDto> RecordClickAsync(
            CustomerAdvertisementTrackRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return RecordEventAsync(
                request,
                CustomerAdvertisementEventTypes.Click,
                includeRedirect: true,
                cancellationToken);
        }

        private async Task<CustomerAdvertisementTrackResultDto> RecordEventAsync(
            CustomerAdvertisementTrackRequestDto request,
            string eventType,
            bool includeRedirect,
            CancellationToken cancellationToken)
        {
            if (request == null || request.AdvertisementId <= 0)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "بيانات الإعلان غير صحيحة.");
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "تعذر تحديد طلب المتصفح.");
            }

            var identity = GetOrCreateIdentity(httpContext);
            var deviceType = DetectDeviceType(
                httpContext.Request.Headers["User-Agent"].ToString());

            var advertisement = await _context.Advertisements
                .FirstOrDefaultAsync(
                    ad => ad.AdvertisementId == request.AdvertisementId,
                    cancellationToken);

            if (advertisement == null)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "الإعلان غير موجود.");
            }

            var redirectUrl = BuildDestinationUrl(advertisement);

            if (!string.Equals(
                    advertisement.PlacementTypeSnapshot,
                    request.PlacementType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "مكان ظهور الإعلان غير مطابق.",
                    includeRedirect ? redirectUrl : null);
            }

            // عند النقر نسمح بالتحويل حتى لو انتهت الحملة بين لحظة العرض ولحظة النقر،
            // لكن لا نحتسب تكلفة جديدة لحملة غير قابلة للمحاسبة.
            bool trackable =
                advertisement.Status == StatusActive &&
                advertisement.PaymentStatus == PaymentPaid;

            if (!trackable)
            {
                return CustomerAdvertisementTrackResultDto.Fail(
                    "الحملة لم تعد نشطة.",
                    includeRedirect ? redirectUrl : null);
            }

            var requestKey = NormalizeRequestKey(request.RequestKey);

            // النقرات في إحصائيات المالك = عدد المستخدمين المختلفين الذين نقروا،
            // لذلك نفس المستخدم يستطيع فتح الإعلان مرة أخرى لكن لا يُحتسب كنقرة جديدة.
            if (eventType == CustomerAdvertisementEventTypes.Click &&
                await HasPreviousClickAsync(advertisement.AdvertisementId, identity, cancellationToken))
            {
                return CustomerAdvertisementTrackResultDto.Ok(
                    includeRedirect ? redirectUrl : null,
                    alreadyRecorded: true);
            }

            bool duplicate = await _context.AdvertisementEvents
                .AsNoTracking()
                .AnyAsync(
                    evt =>
                        evt.AdvertisementId == advertisement.AdvertisementId &&
                        evt.EventType == eventType &&
                        evt.RequestKey == requestKey,
                    cancellationToken);

            if (duplicate)
            {
                return CustomerAdvertisementTrackResultDto.Ok(
                    includeRedirect ? redirectUrl : null,
                    alreadyRecorded: true);
            }

            var now = DateTime.Now;
            var eventCost = await CalculateBillableEventCostAsync(
                advertisement,
                eventType,
                now,
                cancellationToken);

            bool uniqueImpression = false;
            if (eventType == CustomerAdvertisementEventTypes.Impression)
            {
                uniqueImpression = !await HasPreviousImpressionAsync(
                    advertisement.AdvertisementId,
                    identity,
                    cancellationToken);
            }

            var advertisementEvent = new AdvertisementEvent
            {
                AdvertisementId = advertisement.AdvertisementId,
                UserId = identity.UserId,
                EventType = eventType,
                PlacementType = advertisement.PlacementTypeSnapshot,
                SessionKey = identity.SessionKey,
                VisitorHash = identity.VisitorHash,
                RequestKey = requestKey,
                DeviceType = deviceType,
                IpHash = null,
                UserAgentHash = HashValue(
                    httpContext.Request.Headers["User-Agent"].ToString()),
                CostAmount = eventCost,
                EventAt = now
            };

            _context.AdvertisementEvents.Add(advertisementEvent);

            if (eventType == CustomerAdvertisementEventTypes.Impression)
            {
                advertisement.ImpressionsCount += 1;
                if (uniqueImpression)
                {
                    advertisement.UniqueImpressionsCount += 1;
                }

                advertisement.LastImpressionAt = now;
                advertisement.LastServedAt = now;
            }
            else if (eventType == CustomerAdvertisementEventTypes.Click)
            {
                advertisement.ClicksCount += 1;
                advertisement.LastClickAt = now;
            }

            if (eventCost > 0m)
            {
                var previousEventCosts = await _context.AdvertisementEvents
                    .AsNoTracking()
                    .Where(evt => evt.AdvertisementId == advertisement.AdvertisementId)
                    .SumAsync(
                        evt => (decimal?)evt.CostAmount,
                        cancellationToken) ?? 0m;

                var calculatedSpent = Math.Round(
                    previousEventCosts + eventCost,
                    2,
                    MidpointRounding.AwayFromZero);

                // لا ننقص أي قيمة قديمة إذا كان المشروع قد احتسب مصروفاً سابقاً.
                advertisement.SpentAmount = Math.Max(
                    advertisement.SpentAmount,
                    calculatedSpent);
            }

            advertisement.UpdatedAt = now;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Concurrent customer advertisement event for advertisement {AdvertisementId}",
                    advertisement.AdvertisementId);

                // لا نمنع العميل من الوصول للوجهة بسبب سباق إحصائي.
                return CustomerAdvertisementTrackResultDto.Ok(
                    includeRedirect ? redirectUrl : null);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to store customer advertisement event {EventType} for {AdvertisementId}",
                    eventType,
                    advertisement.AdvertisementId);

                // غالباً RequestKey مكرر وصل بالتوازي؛ نعامل الطلب كمسجل.
                return CustomerAdvertisementTrackResultDto.Ok(
                    includeRedirect ? redirectUrl : null,
                    alreadyRecorded: true);
            }

            return CustomerAdvertisementTrackResultDto.Ok(
                includeRedirect ? redirectUrl : null);
        }

        private async Task<decimal> CalculateBillableEventCostAsync(
            Advertisement advertisement,
            string eventType,
            DateTime now,
            CancellationToken cancellationToken)
        {
            decimal unitCost = 0m;

            if (eventType == CustomerAdvertisementEventTypes.Click &&
                string.Equals(
                    advertisement.BillingModelSnapshot,
                    BillingCpc,
                    StringComparison.OrdinalIgnoreCase))
            {
                unitCost = Math.Max(
                    0m,
                    advertisement.CostPerClickSnapshot ?? 0m);
            }
            else if (eventType == CustomerAdvertisementEventTypes.Impression &&
                     string.Equals(
                         advertisement.BillingModelSnapshot,
                         BillingCpm,
                         StringComparison.OrdinalIgnoreCase))
            {
                unitCost = Math.Round(
                    Math.Max(
                        0m,
                        advertisement.CostPerThousandSnapshot ?? 0m) / 1000m,
                    4,
                    MidpointRounding.AwayFromZero);
            }

            if (unitCost <= 0m)
            {
                return 0m;
            }

            decimal remainingTotal = decimal.MaxValue;
            if (advertisement.TotalBudget.HasValue)
            {
                remainingTotal = Math.Max(
                    0m,
                    advertisement.TotalBudget.Value - advertisement.SpentAmount);
            }

            decimal remainingDaily = decimal.MaxValue;
            if (advertisement.DailyBudget.HasValue &&
                advertisement.DailyBudget.Value > 0m)
            {
                var dayStart = now.Date;
                var dayEnd = dayStart.AddDays(1);

                var dailySpent = await _context.AdvertisementEvents
                    .AsNoTracking()
                    .Where(evt =>
                        evt.AdvertisementId == advertisement.AdvertisementId &&
                        evt.EventAt >= dayStart &&
                        evt.EventAt < dayEnd)
                    .SumAsync(
                        evt => (decimal?)evt.CostAmount,
                        cancellationToken) ?? 0m;

                remainingDaily = Math.Max(
                    0m,
                    advertisement.DailyBudget.Value - dailySpent);
            }

            if (remainingTotal < unitCost || remainingDaily < unitCost)
            {
                return 0m;
            }

            return unitCost;
        }

        private async Task<bool> HasPreviousImpressionAsync(
            int advertisementId,
            RequestIdentity identity,
            CancellationToken cancellationToken)
        {
            var query = _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    evt.AdvertisementId == advertisementId &&
                    evt.EventType == CustomerAdvertisementEventTypes.Impression);

            if (identity.UserId.HasValue)
            {
                return await query.AnyAsync(
                    evt =>
                        evt.UserId == identity.UserId.Value ||
                        (!string.IsNullOrEmpty(identity.VisitorHash) && evt.VisitorHash == identity.VisitorHash),
                    cancellationToken);
            }

            return await query.AnyAsync(
                evt => evt.VisitorHash == identity.VisitorHash,
                cancellationToken);
        }

        private async Task<bool> HasPreviousClickAsync(
            int advertisementId,
            RequestIdentity identity,
            CancellationToken cancellationToken)
        {
            var query = _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    evt.AdvertisementId == advertisementId &&
                    evt.EventType == CustomerAdvertisementEventTypes.Click);

            if (identity.UserId.HasValue)
            {
                return await query.AnyAsync(
                    evt =>
                        evt.UserId == identity.UserId.Value ||
                        (!string.IsNullOrEmpty(identity.VisitorHash) && evt.VisitorHash == identity.VisitorHash),
                    cancellationToken);
            }

            return await query.AnyAsync(
                evt => evt.VisitorHash == identity.VisitorHash,
                cancellationToken);
        }

        private async Task MarkAsServedAsync(
            IReadOnlyCollection<int> advertisementIds,
            DateTime servedAt,
            CancellationToken cancellationToken)
        {
            if (advertisementIds.Count == 0)
            {
                return;
            }

            try
            {
                var ids = advertisementIds.ToArray();
                var rows = await _context.Advertisements
                    .Where(ad => ids.Contains(ad.AdvertisementId))
                    .ToListAsync(cancellationToken);

                foreach (var row in rows)
                {
                    row.LastServedAt = servedAt;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                // فشل تحديث الدوران لا يجب أن يعطل الرئيسية.
                _logger.LogDebug(
                    exception,
                    "Could not update LastServedAt for selected customer advertisements.");
            }
        }

        private static CandidateRow? PickForPlacement(
            IEnumerable<CandidateRow> candidates,
            string placement,
            int? excludePlaceId = null)
        {
            var query = candidates.Where(candidate =>
                string.Equals(
                    candidate.PlacementType,
                    placement,
                    StringComparison.OrdinalIgnoreCase));

            if (excludePlaceId.HasValue)
            {
                var differentPlace = query
                    .Where(candidate => candidate.PlaceId != excludePlaceId.Value)
                    .ToList();

                // لا نكرر نفس المنشأة في إعلانين داخل نفس تحميل الصفحة.
                if (differentPlace.Count == 0)
                {
                    return null;
                }

                query = differentPlace;
            }

            return query
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.LastServedAt ?? DateTime.MinValue)
                .ThenByDescending(candidate => Math.Max(1, candidate.RotationWeight))
                .ThenBy(candidate => candidate.AdvertisementId)
                .FirstOrDefault();
        }

        private static CandidateRow? PickPrimaryHomeSlot(
            CandidateRow? slider,
            CandidateRow? banner)
        {
            if (slider == null) return banner;
            if (banner == null) return slider;

            var sliderLast = slider.LastServedAt ?? DateTime.MinValue;
            var bannerLast = banner.LastServedAt ?? DateTime.MinValue;

            if (sliderLast < bannerLast) return slider;
            if (bannerLast < sliderLast) return banner;

            // عند التعادل الكامل نبدأ بالسلايدر، وبعد تحديث LastServedAt
            // تصبح فرصة البانر أقدم في الطلب التالي.
            return slider;
        }

        private static bool HasFrequencyCapacity(
            CandidateRow candidate,
            int impressionsToday)
        {
            if (candidate.FrequencyCap <= 0)
            {
                return true;
            }

            return impressionsToday < candidate.FrequencyCap;
        }

        private static bool HasBudgetCapacity(
            CandidateRow candidate,
            decimal dailySpent)
        {
            if (string.Equals(
                    candidate.BillingModel,
                    BillingFlat,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            decimal minimumCharge = MinimumCharge(candidate);
            if (minimumCharge <= 0m)
            {
                return false;
            }

            if (candidate.TotalBudget.HasValue &&
                candidate.TotalBudget.Value - candidate.SpentAmount < minimumCharge)
            {
                return false;
            }

            if (candidate.DailyBudget.HasValue &&
                candidate.DailyBudget.Value > 0m &&
                candidate.DailyBudget.Value - dailySpent < minimumCharge)
            {
                return false;
            }

            return true;
        }

        private static decimal MinimumCharge(CandidateRow candidate)
        {
            if (string.Equals(
                    candidate.BillingModel,
                    BillingCpc,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(0m, candidate.CostPerClick ?? 0m);
            }

            if (string.Equals(
                    candidate.BillingModel,
                    BillingCpm,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Math.Round(
                    Math.Max(0m, candidate.CostPerThousand ?? 0m) / 1000m,
                    4,
                    MidpointRounding.AwayFromZero);
            }

            return 0m;
        }

        private static bool MatchesHomeTargeting(
            CandidateRow candidate,
            IReadOnlyCollection<TargetRow>? targets)
        {
            if (string.IsNullOrWhiteSpace(candidate.TargetingMode) ||
                string.Equals(
                    candidate.TargetingMode,
                    TargetingAll,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(
                    candidate.TargetingMode,
                    TargetingCustom,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (targets == null || targets.Count == 0)
            {
                return false;
            }

            // الصفحة الرئيسية لا تحمل فلتر مدينة/فئة مختاراً.
            // لذلك نطابق الاستهداف المخصص مع سياق المنشأة نفسها، بينما
            // صفحات الاستكشاف تستخدم MatchesDiscoveryTargeting بسياق الفلاتر الحقيقي.
            var cityTargets = targets
                .Where(target => target.CityId.HasValue)
                .Select(target => target.CityId!.Value)
                .Distinct()
                .ToArray();

            var categoryTargets = targets
                .Where(target => target.CategoryId.HasValue)
                .Select(target => target.CategoryId!.Value)
                .Distinct()
                .ToArray();

            bool cityMatch =
                cityTargets.Length == 0 ||
                cityTargets.Contains(candidate.PlaceCityId);

            bool categoryMatch =
                categoryTargets.Length == 0 ||
                categoryTargets.Contains(candidate.PlaceCategoryId);

            return cityMatch && categoryMatch;
        }

        private static bool MatchesDiscoveryTargeting(
            CandidateRow candidate,
            IReadOnlyCollection<TargetRow>? targets,
            CustomerAdvertisementDiscoveryRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(candidate.TargetingMode) ||
                string.Equals(
                    candidate.TargetingMode,
                    TargetingAll,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(
                    candidate.TargetingMode,
                    TargetingCustom,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (targets == null || targets.Count == 0)
            {
                return false;
            }

            var cityTargets = targets
                .Where(target => target.CityId.HasValue)
                .Select(target => target.CityId!.Value)
                .Distinct()
                .ToArray();

            var categoryTargets = targets
                .Where(target => target.CategoryId.HasValue)
                .Select(target => target.CategoryId!.Value)
                .Distinct()
                .ToArray();

            int effectiveCityId = request.CityId.HasValue && request.CityId.Value > 0
                ? request.CityId.Value
                : candidate.PlaceCityId;

            int effectiveCategoryId = request.CategoryId.HasValue && request.CategoryId.Value > 0
                ? request.CategoryId.Value
                : candidate.PlaceCategoryId;

            bool cityMatch =
                cityTargets.Length == 0 ||
                cityTargets.Contains(effectiveCityId);

            bool categoryMatch =
                categoryTargets.Length == 0 ||
                categoryTargets.Contains(effectiveCategoryId);

            return cityMatch && categoryMatch;
        }

        private static bool MatchesContextTargeting(
            CandidateRow candidate,
            IReadOnlyCollection<TargetRow>? targets,
            int contextCityId,
            int contextCategoryId)
        {
            if (string.IsNullOrWhiteSpace(candidate.TargetingMode) ||
                string.Equals(
                    candidate.TargetingMode,
                    TargetingAll,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(
                    candidate.TargetingMode,
                    TargetingCustom,
                    StringComparison.OrdinalIgnoreCase) ||
                targets == null ||
                targets.Count == 0)
            {
                return false;
            }

            var cityTargets = targets
                .Where(target => target.CityId.HasValue)
                .Select(target => target.CityId!.Value)
                .Distinct()
                .ToArray();

            var categoryTargets = targets
                .Where(target => target.CategoryId.HasValue)
                .Select(target => target.CategoryId!.Value)
                .Distinct()
                .ToArray();

            int effectiveCityId = contextCityId > 0
                ? contextCityId
                : candidate.PlaceCityId;

            int effectiveCategoryId = contextCategoryId > 0
                ? contextCategoryId
                : candidate.PlaceCategoryId;

            bool cityMatch =
                cityTargets.Length == 0 ||
                cityTargets.Contains(effectiveCityId);

            bool categoryMatch =
                categoryTargets.Length == 0 ||
                categoryTargets.Contains(effectiveCategoryId);

            return cityMatch && categoryMatch;
        }

        private static bool MatchesGeoTargeting(
            CandidateRow candidate,
            double? userLatitude,
            double? userLongitude)
        {
            if (!candidate.TargetLatitude.HasValue ||
                !candidate.TargetLongitude.HasValue ||
                !candidate.TargetRadiusKm.HasValue ||
                candidate.TargetRadiusKm.Value <= 0m)
            {
                return true;
            }

            if (!userLatitude.HasValue || !userLongitude.HasValue)
            {
                return false;
            }

            double distanceKm = CalculateDistanceKm(
                userLatitude.Value,
                userLongitude.Value,
                (double)candidate.TargetLatitude.Value,
                (double)candidate.TargetLongitude.Value);

            return distanceKm <= (double)candidate.TargetRadiusKm.Value;
        }

        private static double CalculateDistanceKm(
            double lat1,
            double lng1,
            double lat2,
            double lng2)
        {
            const double earthRadiusKm = 6371d;
            double ToRadians(double value) => value * Math.PI / 180d;

            double deltaLat = ToRadians(lat2 - lat1);
            double deltaLng = ToRadians(lng2 - lng1);
            double a =
                Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d) +
                Math.Cos(ToRadians(lat1)) *
                Math.Cos(ToRadians(lat2)) *
                Math.Sin(deltaLng / 2d) * Math.Sin(deltaLng / 2d);

            return earthRadiusKm * 2d * Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(1d - a));
        }

        private static CustomerAdvertisementDto ToCustomerDto(
            CandidateRow candidate,
            MediaRow? media)
        {
            string mediaType = media?.MediaType ?? "Image";
            string mediaPath = FirstNonEmpty(
                media?.FilePath,
                candidate.ImagePath,
                candidate.PromotionImage,
                candidate.PlaceCoverImage,
                candidate.PlaceLogoImage,
                "/images/bg1.jpg");

            return new CustomerAdvertisementDto
            {
                AdvertisementId = candidate.AdvertisementId,
                PlaceId = candidate.PlaceId,
                PromotionId = candidate.PromotionId,
                PlacementType = candidate.PlacementType,
                AdvertisementTitle = candidate.AdvertisementTitle,
                ShortDescription = candidate.ShortDescription,
                ButtonText = string.IsNullOrWhiteSpace(candidate.ButtonText)
                    ? "عرض المنشأة"
                    : candidate.ButtonText,
                PlaceName = candidate.PlaceName,
                CategoryName = candidate.CategoryName,
                CityName = candidate.CityName,
                MediaType = mediaType,
                MediaPath = mediaPath,
                ThumbnailPath = media?.ThumbnailPath,
                DestinationType = candidate.DestinationType,
                FallbackUrl = BuildDestinationUrl(candidate)
            };
        }

        private static string BuildDestinationUrl(Advertisement advertisement)
        {
            var candidate = new CandidateRow
            {
                AdvertisementId = advertisement.AdvertisementId,
                PlaceId = advertisement.PlaceId,
                PromotionId = advertisement.PromotionId,
                DestinationType = advertisement.DestinationType
            };

            return BuildDestinationUrl(candidate);
        }

        private static string BuildDestinationUrl(CandidateRow candidate)
        {
            string adQuery = $"advertisementId={candidate.AdvertisementId}";

            if (string.Equals(
                    candidate.DestinationType,
                    DestinationPromotion,
                    StringComparison.OrdinalIgnoreCase) &&
                candidate.PromotionId.HasValue)
            {
                return $"/Places/Details/{candidate.PlaceId}?promotionId={candidate.PromotionId.Value}&{adQuery}";
            }

            if (string.Equals(
                    candidate.DestinationType,
                    DestinationBooking,
                    StringComparison.OrdinalIgnoreCase))
            {
                // نمر أولًا عبر صفحة التفاصيل؛ منها يُحفظ المصدر الموثق مؤقتًا
                // حتى يختار العميل الغرفة/الباقة ثم يُعاد التحقق منه عند إنشاء الحجز.
                return $"/Places/Details/{candidate.PlaceId}?{adQuery}";
            }

            return $"/Places/Details/{candidate.PlaceId}?{adQuery}";
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                   ?? "/images/bg1.jpg";
        }

        private RequestIdentity GetOrCreateIdentity(HttpContext httpContext)
        {
            int? userId = null;
            var claim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(claim, out var parsedUserId) && parsedUserId > 0)
            {
                userId = parsedUserId;
            }

            var visitorToken = EnsureCookie(
                httpContext,
                VisitorCookieName,
                persistentDays: 180);

            var sessionToken = EnsureCookie(
                httpContext,
                SessionCookieName,
                persistentDays: null);

            return new RequestIdentity
            {
                UserId = userId,
                VisitorHash = HashValue(visitorToken),
                SessionKey = HashValue(sessionToken)
            };
        }

        private static string EnsureCookie(
            HttpContext httpContext,
            string cookieName,
            int? persistentDays)
        {
            if (httpContext.Request.Cookies.TryGetValue(
                    cookieName,
                    out var current) &&
                !string.IsNullOrWhiteSpace(current))
            {
                return current;
            }

            var token = Convert
                .ToHexString(RandomNumberGenerator.GetBytes(24))
                .ToLowerInvariant();

            if (!httpContext.Response.HasStarted)
            {
                var options = new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = httpContext.Request.IsHttps,
                    Path = "/"
                };

                if (persistentDays.HasValue)
                {
                    options.MaxAge = TimeSpan.FromDays(persistentDays.Value);
                }

                httpContext.Response.Cookies.Append(
                    cookieName,
                    token,
                    options);
            }

            return token;
        }

        private static string DetectDeviceType(string userAgent)
        {
            var value = (userAgent ?? string.Empty).ToLowerInvariant();

            if (value.Contains("ipad") ||
                value.Contains("tablet") ||
                (value.Contains("android") && !value.Contains("mobile")))
            {
                return "Tablet";
            }

            if (value.Contains("mobile") ||
                value.Contains("iphone") ||
                value.Contains("ipod") ||
                value.Contains("android"))
            {
                return DeviceMobile;
            }

            return DeviceDesktop;
        }

        private static string HashValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string NormalizeRequestKey(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var trimmed = value.Trim();
                if (trimmed.Length <= 128 &&
                    trimmed.All(character =>
                        char.IsLetterOrDigit(character) ||
                        character == '-' ||
                        character == '_'))
                {
                    return trimmed;
                }
            }

            return Guid.NewGuid().ToString("N");
        }

        private sealed class RequestIdentity
        {
            public int? UserId { get; init; }
            public string VisitorHash { get; init; } = string.Empty;
            public string SessionKey { get; init; } = string.Empty;
        }

        private sealed class CandidateRow
        {
            public int AdvertisementId { get; init; }
            public int PlaceId { get; init; }
            public int? PromotionId { get; init; }
            public string PlacementType { get; init; } = string.Empty;
            public string AdvertisementTitle { get; init; } = string.Empty;
            public string? ShortDescription { get; init; }
            public string ButtonText { get; init; } = string.Empty;
            public string? ImagePath { get; init; }
            public string BillingModel { get; init; } = string.Empty;
            public decimal? CostPerClick { get; init; }
            public decimal? CostPerThousand { get; init; }
            public decimal? TotalBudget { get; init; }
            public decimal? DailyBudget { get; init; }
            public decimal SpentAmount { get; init; }
            public int FrequencyCap { get; init; }
            public string TargetingMode { get; init; } = string.Empty;
            public string DeviceTarget { get; init; } = string.Empty;
            public string DestinationType { get; init; } = string.Empty;
            public string? DestinationPath { get; init; }
            public int Priority { get; init; }
            public int RotationWeight { get; init; }
            public DateTime? LastServedAt { get; init; }
            public decimal? TargetLatitude { get; init; }
            public decimal? TargetLongitude { get; init; }
            public decimal? TargetRadiusKm { get; init; }
            public string PlaceName { get; init; } = string.Empty;
            public int PlaceCityId { get; init; }
            public int PlaceCategoryId { get; init; }
            public string CityName { get; init; } = string.Empty;
            public string CategoryName { get; init; } = string.Empty;
            public string? PlaceCoverImage { get; init; }
            public string? PlaceLogoImage { get; init; }
            public string? PromotionImage { get; init; }
        }

        private sealed class TargetRow
        {
            public int AdvertisementId { get; init; }
            public int? CityId { get; init; }
            public int? CategoryId { get; init; }
        }

        private sealed class MediaRow
        {
            public int AdvertisementId { get; init; }
            public string MediaType { get; init; } = "Image";
            public string FilePath { get; init; } = string.Empty;
            public string? ThumbnailPath { get; init; }
            public bool IsPrimary { get; init; }
            public int DisplayOrder { get; init; }
        }
    }
}
