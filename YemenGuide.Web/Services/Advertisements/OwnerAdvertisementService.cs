using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Advertisements
{
    public sealed class OwnerAdvertisementService : IOwnerAdvertisementService
    {
        private const string BaseCurrency = "ريال يمني";
        private const int DefaultPageSize = 12;
        private const int MaximumPageSize = 100;
        private const string RefundPendingMarker = "[REFUND_PENDING]";

        private readonly AppDbContext _context;
        private readonly CurrencyDisplayService _currencyDisplayService;
        private readonly IAdvertisementPricingService _pricing;
        private readonly ILogger<OwnerAdvertisementService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IWebHostEnvironment _environment;

        public OwnerAdvertisementService(
            AppDbContext context,
            CurrencyDisplayService currencyDisplayService,
            IAdvertisementPricingService pricing,
            ILogger<OwnerAdvertisementService> logger,
            INotificationService notificationService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _currencyDisplayService = currencyDisplayService;
            _pricing = pricing;
            _logger = logger;
            _notificationService = notificationService;
            _environment = environment;
        }

        public async Task<OwnerAdvertisementManagementPageDto> GetManagementPageAsync(
            int ownerId,
            int? requestedPlaceId,
            string? requestedTab,
            CancellationToken cancellationToken = default)
        {
            EnsureOwner(ownerId);
            var place = await ResolveOwnedPlaceAsync(ownerId, requestedPlaceId, cancellationToken);
            var displayCurrency = await _currencyDisplayService.GetPreferredCurrencyAsync();

            var model = new OwnerAdvertisementManagementPageDto
            {
                DefaultTab = OwnerAdvertisementTabs.Normalize(requestedTab),
                HasPlace = place != null,
                PlaceId = place?.PlaceId,
                PlaceName = place?.PlaceName ?? string.Empty,
                PlaceImage = place?.PlaceImage,
                PlaceStatus = place?.Status ?? string.Empty,
                DisplayCurrency = displayCurrency,
                DisplayCurrencyCode = CurrencyDisplayService.GetCurrencyCode(displayCurrency),
                DisplayCurrencySymbol = CurrencyDisplayService.GetCurrencySymbol(displayCurrency)
            };

            if (place == null)
            {
                return model;
            }

            await RefreshLifecycleAsync(ownerId, place.PlaceId, cancellationToken);

            var counters = await _context.Advertisements
                .AsNoTracking()
                .Where(item => item.OwnerId == ownerId && item.PlaceId == place.PlaceId)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Count(),
                    Draft = group.Count(item => item.Status == OwnerAdvertisementStatuses.Draft),
                    AwaitingPayment = group.Count(item => item.Status == OwnerAdvertisementStatuses.AwaitingPayment),
                    Pending = group.Count(item => item.Status == OwnerAdvertisementStatuses.Pending),
                    Active = group.Count(item => item.Status == OwnerAdvertisementStatuses.Active),
                    Scheduled = group.Count(item => item.Status == OwnerAdvertisementStatuses.Scheduled),
                    Paused = group.Count(item => item.Status == OwnerAdvertisementStatuses.Paused),
                    Expired = group.Count(item => item.Status == OwnerAdvertisementStatuses.Expired),
                    Cancelled = group.Count(item => item.Status == OwnerAdvertisementStatuses.Cancelled),
                    Rejected = group.Count(item => item.Status == OwnerAdvertisementStatuses.Rejected)
                })
                .FirstOrDefaultAsync(cancellationToken);

            model.DraftCount = counters?.Draft ?? 0;
            model.AwaitingPaymentCount = counters?.AwaitingPayment ?? 0;
            model.PendingCount = counters?.Pending ?? 0;
            model.ActiveCount = counters?.Active ?? 0;
            model.ScheduledCount = counters?.Scheduled ?? 0;
            model.PausedCount = counters?.Paused ?? 0;
            model.ExpiredCount = counters?.Expired ?? 0;
            model.CancelledCount = counters?.Cancelled ?? 0;
            model.RejectedCount = counters?.Rejected ?? 0;

            // الفئات في واجهة المالك يجب أن تكون حصرية ويجب أن يساوي مجموعها "الكل".
            // لذلك أي إعلان ليس عاملًا/متوقفًا/منتهيًا/ملغيًا يذهب إلى "تحتاج إجراء"
            // (Draft / AwaitingPayment / Pending / Rejected وأي حالة قديمة غير مصنفة).
            var totalCount = counters?.Total ?? 0;
            model.NeedsActionCount = Math.Max(0, totalCount -
                model.RunningCount -
                model.ScheduledCount -
                model.PausedCount -
                model.ExpiredCount -
                model.CancelledCount);

            // Single-view payload: load everything the one Razor view needs.
            model.Overview = await GetOverviewAsync(
                ownerId,
                place.PlaceId,
                cancellationToken);

            model.Advertisements = await GetAdvertisementsAsync(
                new OwnerAdvertisementFilterDto
                {
                    Group = "All",
                    SortBy = "Newest",
                    Page = 1,
                    PageSize = DefaultPageSize
                },
                ownerId,
                place.PlaceId,
                cancellationToken);

            model.Wizard = await GetWizardAsync(
                null,
                ownerId,
                place.PlaceId,
                cancellationToken);

            return model;
        }

        public async Task<OwnerAdvertisementOverviewDto> GetOverviewAsync(
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            EnsureOwner(ownerId);
            var place = await ResolveOwnedPlaceAsync(ownerId, requestedPlaceId, cancellationToken);
            if (place == null)
            {
                return new OwnerAdvertisementOverviewDto();
            }

            await RefreshLifecycleAsync(ownerId, place.PlaceId, cancellationToken);
            var now = DateTime.Now;
            var endingSoonAt = now.AddDays(3);
            var query = _context.Advertisements
                .AsNoTracking()
                .Where(item => item.OwnerId == ownerId && item.PlaceId == place.PlaceId);

            var aggregate = await query
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Count(),
                    Active = group.Count(item => item.Status == OwnerAdvertisementStatuses.Active),
                    Scheduled = group.Count(item => item.Status == OwnerAdvertisementStatuses.Scheduled),
                    Pending = group.Count(item => item.Status == OwnerAdvertisementStatuses.Pending),
                    Draft = group.Count(item => item.Status == OwnerAdvertisementStatuses.Draft),
                    AwaitingPayment = group.Count(item => item.Status == OwnerAdvertisementStatuses.AwaitingPayment),
                    PaymentRejected = group.Count(item => item.PaymentStatus == OwnerAdvertisementPaymentStatuses.Rejected),
                    Paused = group.Count(item => item.Status == OwnerAdvertisementStatuses.Paused),
                    EndingSoon = group.Count(item => item.Status == OwnerAdvertisementStatuses.Active && item.EndAt.HasValue && item.EndAt >= now && item.EndAt <= endingSoonAt),
                    BudgetRisk = group.Count(item => item.Status == OwnerAdvertisementStatuses.Active && item.TotalBudget.HasValue && item.TotalBudget > 0m && item.SpentAmount >= item.TotalBudget * .9m),
                    Rejected = group.Count(item => item.Status == OwnerAdvertisementStatuses.Rejected),
                    Impressions = group.Sum(item => item.ImpressionsCount),
                    UniqueImpressions = group.Sum(item => item.UniqueImpressionsCount),
                    Clicks = group.Sum(item => item.ClicksCount),
                    BookingStarts = group.Sum(item => item.BookingStartsCount),
                    Conversions = group.Sum(item => item.ConversionsCount),
                    Spent = group.Sum(item => item.SpentAmount),
                    // لا نعرض ميزانية المسودات أو الطلبات غير المدفوعة كرصيد متاح.
                    Budget = group.Sum(item => item.PaymentStatus == OwnerAdvertisementPaymentStatuses.Paid &&
                        item.Status != OwnerAdvertisementStatuses.Cancelled &&
                        item.Status != OwnerAdvertisementStatuses.Rejected
                            ? (item.BillingModelSnapshot == OwnerAdvertisementBillingModels.Flat
                                ? item.PriceSnapshot
                                : item.TotalBudget ?? 0m)
                            : 0m)
                })
                .FirstOrDefaultAsync(cancellationToken);

            // مؤشرات الأداء في "نظرة عامة" تُحسب من سجل أحداث الإعلان الفعلي،
            // وليس من أسماء أو أرقام تجميعية قديمة في جدول Advertisements.
            var advertisementIds = await query
                .Select(item => item.AdvertisementId)
                .ToListAsync(cancellationToken);
            var actualMetrics = await GetActualInteractionMetricsAsync(
                advertisementIds,
                cancellationToken);

            var recentRows = await BuildOwnedAdvertisementQuery(ownerId, place.PlaceId)
                .OrderByDescending(item =>
                    item.Status == OwnerAdvertisementStatuses.Draft ||
                    item.Status == OwnerAdvertisementStatuses.AwaitingPayment ||
                    item.Status == OwnerAdvertisementStatuses.Rejected ||
                    item.PaymentStatus == OwnerAdvertisementPaymentStatuses.Rejected)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenByDescending(item => item.AdvertisementId)
                .Take(6)
                .ToListAsync(cancellationToken);

            var recent = new List<OwnerAdvertisementListItemDto>();
            foreach (var row in recentRows)
            {
                recent.Add(await ToListItemAsync(row, place.ExchangeRateRegion, cancellationToken));
            }

            var recommended = await GetPlanCardsAsync(
                ownerId,
                place.PlaceId,
                place.ExchangeRateRegion,
                onlyAvailable: true,
                take: 4,
                cancellationToken);

            var impressions = actualMetrics.TotalViews;
            var clicks = actualMetrics.TotalClicks;
            var bookingStarts = actualMetrics.BookingStarts;
            var conversions = actualMetrics.CompletedBookings;
            var spent = aggregate?.Spent ?? 0m;
            var remaining = Math.Max(0m, (aggregate?.Budget ?? 0m) - spent);

            return new OwnerAdvertisementOverviewDto
            {
                HasPlace = true,
                PlaceId = place.PlaceId,
                PlaceName = place.PlaceName,
                PlaceImage = place.PlaceImage,
                TotalAdvertisements = aggregate?.Total ?? 0,
                ActiveAdvertisements = aggregate?.Active ?? 0,
                ScheduledAdvertisements = aggregate?.Scheduled ?? 0,
                PendingAdvertisements = aggregate?.Pending ?? 0,
                DraftAdvertisements = aggregate?.Draft ?? 0,
                AwaitingPaymentAdvertisements = aggregate?.AwaitingPayment ?? 0,
                PaymentRejectedAdvertisements = aggregate?.PaymentRejected ?? 0,
                PausedAdvertisements = aggregate?.Paused ?? 0,
                EndingSoonAdvertisements = aggregate?.EndingSoon ?? 0,
                BudgetRiskAdvertisements = aggregate?.BudgetRisk ?? 0,
                RejectedAdvertisements = aggregate?.Rejected ?? 0,
                ImpressionsCount = impressions,
                UniqueImpressionsCount = actualMetrics.UniqueViews,
                ClicksCount = clicks,
                BookingStartsCount = bookingStarts,
                ConversionsCount = conversions,
                ClickThroughRate = Percentage(clicks, impressions),
                ConversionRate = Percentage(conversions, clicks),
                SpentAmount = spent,
                RemainingBudget = remaining,
                SpentAmountText = await FormatMoneyAsync(spent, place.ExchangeRateRegion),
                RemainingBudgetText = await FormatMoneyAsync(remaining, place.ExchangeRateRegion),
                RecentAdvertisements = recent,
                RecommendedPlans = recommended
            };
        }

        public async Task<OwnerAdvertisementsTabDto> GetAdvertisementsAsync(
            OwnerAdvertisementFilterDto filter,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            EnsureOwner(ownerId);
            filter ??= new OwnerAdvertisementFilterDto();
            NormalizePaging(filter);
            var place = await ResolveOwnedPlaceAsync(ownerId, requestedPlaceId, cancellationToken);

            var result = new OwnerAdvertisementsTabDto
            {
                HasPlace = place != null,
                Filter = filter,
                PaymentStatusOptions = PaymentStatusOptions(),
                Page = filter.Page,
                PageSize = filter.PageSize
            };

            if (place == null)
            {
                return result;
            }

            await RefreshLifecycleAsync(ownerId, place.PlaceId, cancellationToken);
            var baseQuery = BuildOwnedAdvertisementQuery(ownerId, place.PlaceId);
            result.AllCount = await baseQuery.CountAsync(cancellationToken);
            result.RunningCount = await baseQuery.CountAsync(
                item => item.Status == OwnerAdvertisementStatuses.Active,
                cancellationToken);
            result.ScheduledCount = await baseQuery.CountAsync(
                item => item.Status == OwnerAdvertisementStatuses.Scheduled,
                cancellationToken);
            result.PausedCount = await baseQuery.CountAsync(
                item => item.Status == OwnerAdvertisementStatuses.Paused,
                cancellationToken);
            result.ExpiredCount = await baseQuery.CountAsync(
                item => item.Status == OwnerAdvertisementStatuses.Expired,
                cancellationToken);
            result.CancelledCount = await baseQuery.CountAsync(
                item => item.Status == OwnerAdvertisementStatuses.Cancelled,
                cancellationToken);

            // فئة حصرية متبقية: تضمن أن الأرقام دائمًا تجمع إلى "الكل".
            result.NeedsActionCount = Math.Max(0,
                result.AllCount -
                result.RunningCount -
                result.ScheduledCount -
                result.PausedCount -
                result.ExpiredCount -
                result.CancelledCount);

            var query = ApplyFilters(baseQuery, filter);
            var totalItems = await query.CountAsync(cancellationToken);
            var totalPages = CalculateTotalPages(totalItems, filter.PageSize);
            filter.Page = ClampPage(filter.Page, totalPages);

            var orderedQuery = filter.SortBy switch
            {
                "Oldest" => query.OrderBy(item => item.CreatedAt).ThenBy(item => item.AdvertisementId),
                "HighestBudget" => query.OrderByDescending(item => item.TotalBudget ?? item.PriceSnapshot).ThenByDescending(item => item.AdvertisementId),
                "MostViewed" => query.OrderByDescending(item => item.ImpressionsCount).ThenByDescending(item => item.AdvertisementId),
                "NeedsAction" => query.OrderByDescending(item =>
                        item.Status != OwnerAdvertisementStatuses.Active &&
                        item.Status != OwnerAdvertisementStatuses.Scheduled &&
                        item.Status != OwnerAdvertisementStatuses.Paused &&
                        item.Status != OwnerAdvertisementStatuses.Expired &&
                        item.Status != OwnerAdvertisementStatuses.Cancelled)
                    .ThenByDescending(item => item.UpdatedAt),
                _ => query.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.AdvertisementId)
            };

            var rows = await orderedQuery
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                result.Items.Add(await ToListItemAsync(row, place.ExchangeRateRegion, cancellationToken));
            }

            result.TotalItems = totalItems;
            result.Page = filter.Page;
            result.TotalPages = totalPages;
            return result;
        }

        public async Task<OwnerAdvertisementWizardDto> GetWizardAsync(
            int? advertisementId,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            EnsureOwner(ownerId);
            var place = await ResolveOwnedPlaceAsync(ownerId, requestedPlaceId, cancellationToken);
            var displayCurrency = await _currencyDisplayService.GetPreferredCurrencyAsync();
            var model = new OwnerAdvertisementWizardDto
            {
                HasPlace = place != null,
                PlaceId = place?.PlaceId,
                PlaceName = place?.PlaceName ?? string.Empty,
                PlaceImage = place?.PlaceImage,
                DisplayCurrency = displayCurrency,
                DisplayCurrencyCode = CurrencyDisplayService.GetCurrencyCode(displayCurrency),
                DisplayCurrencySymbol = CurrencyDisplayService.GetCurrencySymbol(displayCurrency)
            };

            if (place == null)
            {
                return model;
            }

            model.Plans = await GetPlanCardsAsync(
                ownerId,
                place.PlaceId,
                place.ExchangeRateRegion,
                onlyAvailable: false,
                take: null,
                cancellationToken);
            model.Cities = await _context.Cities
                .AsNoTracking()
                .OrderBy(item => item.CityName)
                .Select(item => new OwnerAdvertisementOptionDto
                {
                    Value = item.CityId.ToString(),
                    Label = item.CityName
                })
                .ToListAsync(cancellationToken);

            model.Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(item => item.CategoryName)
                .Select(item => new OwnerAdvertisementOptionDto
                {
                    Value = item.CategoryId.ToString(),
                    Label = item.CategoryName
                })
                .ToListAsync(cancellationToken);

            var today = DateTime.Today;
            model.Promotions = await _context.Promotions
                .AsNoTracking()
                .Where(item =>
                    item.OwnerId == ownerId &&
                    item.PlaceId == place.PlaceId &&
                    item.IsActive == true &&
                    item.StartDate <= today &&
                    item.EndDate >= today)
                .OrderBy(item => item.PromotionTitle)
                .Select(item => new OwnerAdvertisementOptionDto
                {
                    Value = item.PromotionId.ToString(),
                    Label = item.PromotionTitle
                })
                .ToListAsync(cancellationToken);

            model.PlatformWallets = await GetPlatformWalletsAsync(cancellationToken);

            if (!advertisementId.HasValue)
            {
                var firstPlan = model.Plans.FirstOrDefault(item => item.IsAvailable);
                model.Advertisement = new OwnerAdvertisementSaveDto
                {
                    AdvertisementPlanId = firstPlan?.AdvertisementPlanId ?? 0,
                    DurationDays = 1,
                    StartImmediately = true,
                    ButtonText = "عرض المنشأة",
                    TargetingMode = OwnerAdvertisementTargetingModes.All,
                    DeviceTarget = OwnerAdvertisementDeviceTargets.All,
                    TotalBudget = firstPlan?.BillingModel == OwnerAdvertisementBillingModels.Flat
                        ? null
                        : firstPlan?.MinimumTotalBudget,
                    DailyBudget = firstPlan?.BillingModel == OwnerAdvertisementBillingModels.Flat
                        ? null
                        : firstPlan?.DefaultDailyBudget
                };
                return model;
            }

            var advertisement = await _context.Advertisements
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AdvertisementId == advertisementId.Value &&
                    item.OwnerId == ownerId &&
                    item.PlaceId == place.PlaceId,
                    cancellationToken);

            if (advertisement == null)
            {
                return model;
            }

            var targets = await _context.AdvertisementTargets
                .AsNoTracking()
                .Where(item => item.AdvertisementId == advertisement.AdvertisementId)
                .ToListAsync(cancellationToken);

            model.Media = await GetMediaAsync(advertisement.AdvertisementId, cancellationToken);
            model.Advertisement = new OwnerAdvertisementSaveDto
            {
                AdvertisementId = advertisement.AdvertisementId,
                AdvertisementPlanId = advertisement.AdvertisementPlanId,
                AdvertisementTitle = advertisement.AdvertisementTitle,
                ShortDescription = advertisement.ShortDescription,
                ButtonText = advertisement.ButtonText,
                PromotionId = advertisement.PromotionId,
                StartImmediately = advertisement.StartImmediately == true,
                RequestedStartAt = advertisement.RequestedStartAt,
                DurationDays = CampaignDurationDays(
                    advertisement.StartAt,
                    advertisement.EndAt,
                    advertisement.DurationDaysSnapshot),
                TotalBudget = advertisement.TotalBudget,
                DailyBudget = advertisement.DailyBudget,
                TargetingMode = advertisement.TargetingMode,
                CityIds = targets.Where(item => item.CityId.HasValue).Select(item => item.CityId!.Value).ToList(),
                CategoryIds = targets.Where(item => item.CategoryId.HasValue).Select(item => item.CategoryId!.Value).ToList(),
                DeviceTarget = advertisement.DeviceTarget,
                RowVersion = ToRowVersion(advertisement.RowVersion)
            };

            return model;
        }

        public async Task<OwnerAdvertisementPriceQuoteDto> GetPriceQuoteAsync(
            OwnerAdvertisementPriceQuoteRequestDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            EnsureOwner(ownerId);

            var place = await ResolveOwnedPlaceAsync(
                ownerId,
                requestedPlaceId,
                cancellationToken);

            if (place == null)
            {
                return PriceQuoteFailure("اختر منشأة صحيحة من الهيدر أولًا.");
            }

            if (request.DurationDays < 1)
            {
                return PriceQuoteFailure("مدة الإعلان لا يمكن أن تقل عن يوم واحد.");
            }

            if (request.DurationDays > 365)
            {
                return PriceQuoteFailure("لا يمكن أن تتجاوز مدة الإعلان سنة واحدة (365 يومًا).");
            }

            var plan = await _context.AdvertisementPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AdvertisementPlanId == request.AdvertisementPlanId &&
                    item.IsActive == true &&
                    item.PlacementType == OwnerAdvertisementPlacementTypes.SearchTop &&
                    item.BillingModel == OwnerAdvertisementBillingModels.Flat,
                    cancellationToken);

            if (plan == null)
            {
                return PriceQuoteFailure("الباقة غير موجودة أو غير متاحة حاليًا.");
            }

            var quote = _pricing.Calculate(new AdvertisementPriceQuoteRequestDto
            {
                BillingModel = OwnerAdvertisementBillingModels.Flat,
                UnitPrice = plan.Price,
                DurationDays = request.DurationDays
            });

            return new OwnerAdvertisementPriceQuoteDto
            {
                Success = quote.Success,
                Message = quote.Message,
                AdvertisementPlanId = plan.AdvertisementPlanId,
                BillingModel = OwnerAdvertisementBillingModels.Flat,
                BillingModelLabel = "سعر يومي",
                FinalAmount = quote.FinalAmount,
                DailyAmount = quote.DailyAmount,
                EstimatedClicks = null,
                EstimatedImpressions = null,
                FinalAmountText = quote.Success
                    ? await FormatMoneyAsync(quote.FinalAmount, place.ExchangeRateRegion)
                    : string.Empty
            };
        }

        public async Task<OwnerAdvertisementDetailsDto?> GetDetailsAsync(
            int advertisementId,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            EnsureOwner(ownerId);
            var place = await ResolveOwnedPlaceAsync(ownerId, requestedPlaceId, cancellationToken);
            if (place == null)
            {
                return null;
            }

            // ثبّت الموعد الذي اختاره المالك قبل قراءة التفاصيل.
            // موافقة الإدارة (ReviewedAt) لا يجب أن تستبدل RequestedStartAt ببداية وقت الموافقة.
            await RefreshLifecycleAsync(ownerId, place.PlaceId, cancellationToken);

            var row = await BuildOwnedAdvertisementQuery(ownerId, place.PlaceId)
                .FirstOrDefaultAsync(item => item.AdvertisementId == advertisementId, cancellationToken);
            if (row == null)
            {
                return null;
            }

            var media = await GetMediaAsync(advertisementId, cancellationToken);
            var targets = await (
                from target in _context.AdvertisementTargets.AsNoTracking()
                join city in _context.Cities.AsNoTracking()
                    on target.CityId equals (int?)city.CityId into cities
                from city in cities.DefaultIfEmpty()
                join category in _context.Categories.AsNoTracking()
                    on target.CategoryId equals (int?)category.CategoryId into categories
                from category in categories.DefaultIfEmpty()
                where target.AdvertisementId == advertisementId
                orderby city.CityName, category.CategoryName
                select new OwnerAdvertisementTargetDto
                {
                    AdvertisementTargetId = target.AdvertisementTargetId,
                    CityId = target.CityId,
                    CityName = city != null ? city.CityName : null,
                    CategoryId = target.CategoryId,
                    CategoryName = category != null ? category.CategoryName : null,
                    TargetType = target.CityId.HasValue ? "City" : "Category",
                    TargetLabel = target.CityId.HasValue
                        ? city!.CityName
                        : category!.CategoryName
                })
                .ToListAsync(cancellationToken);

            string? promotionTitle = null;
            if (row.PromotionId.HasValue)
            {
                promotionTitle = await _context.Promotions
                    .AsNoTracking()
                    .Where(item => item.PromotionId == row.PromotionId.Value)
                    .Select(item => item.PromotionTitle)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            WalletRow? wallet = null;
            if (row.PlatformWalletId.HasValue)
            {
                wallet = await (
                    from platformWallet in _context.PlatformWallets.AsNoTracking()
                    join supportedWallet in _context.SupportedWallets.AsNoTracking()
                        on platformWallet.WalletId equals supportedWallet.WalletId
                    where platformWallet.PlatformWalletId == row.PlatformWalletId.Value
                    select new WalletRow
                    {
                        WalletName = supportedWallet.WalletName,
                        AccountNumber = platformWallet.AccountNumber,
                        BeneficiaryName = platformWallet.BeneficiaryName
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            }

            string? refundWalletName = null;
            if (row.RefundWalletId.HasValue)
            {
                refundWalletName = await _context.SupportedWallets
                    .AsNoTracking()
                    .Where(item => item.WalletId == row.RefundWalletId.Value)
                    .Select(item => item.WalletName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            string? refundedByName = null;
            if (row.RefundedByUserId.HasValue)
            {
                refundedByName = await _context.Users
                    .AsNoTracking()
                    .Where(item => item.UserId == row.RefundedByUserId.Value)
                    .Select(item => item.FullName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var refundWallets = await GetOwnerRefundWalletsAsync(ownerId, cancellationToken);

            var expectedPayment = ExpectedPayment(row.BillingModel, row.PriceSnapshot, row.TotalBudget);
            var timeAmounts = CalculateTimeBasedAmounts(row);
            var actualMetrics = await GetActualInteractionMetricsAsync(row.AdvertisementId, cancellationToken);
            var capabilities = Capabilities(row.Status, row.PaymentStatus, row.StartAt, row.EndAt, row.RefundRequestedAt, row.RefundedAt);

            return new OwnerAdvertisementDetailsDto
            {
                AdvertisementId = row.AdvertisementId,
                PlaceId = row.PlaceId,
                AdvertisementPlanId = row.AdvertisementPlanId,
                PromotionId = row.PromotionId,
                PlaceName = row.PlaceName,
                PlanName = row.PlanName,
                PromotionTitle = promotionTitle,
                AdvertisementTitle = row.AdvertisementTitle,
                ShortDescription = row.ShortDescription,
                ButtonText = row.ButtonText,
                DurationLabel = CampaignDurationLabel(row.StartAt, row.EndAt, row.DurationDays),
                DurationDays = CampaignDurationDays(row.StartAt, row.EndAt, row.DurationDays),
                BillingModel = row.BillingModel,
                BillingModelLabel = BillingModelLabel(row.BillingModel),
                PriceSnapshot = row.PriceSnapshot,
                CostPerClick = row.CostPerClick,
                CostPerThousandImpressions = row.CostPerThousand,
                TotalBudget = row.TotalBudget,
                DailyBudget = row.DailyBudget,
                SpentAmount = timeAmounts.Spent,
                RemainingBudget = timeAmounts.Remaining,
                ExpectedPaymentAmount = expectedPayment,
                PriceText = await FormatMoneyAsync(row.PriceSnapshot, place.ExchangeRateRegion),
                SpentAmountText = await FormatMoneyAsync(timeAmounts.Spent, place.ExchangeRateRegion),
                RemainingBudgetText = await FormatMoneyAsync(timeAmounts.Remaining, place.ExchangeRateRegion),
                ExpectedPaymentText = await FormatMoneyAsync(expectedPayment, place.ExchangeRateRegion),
                Status = row.Status,
                StatusLabel = StatusLabel(row.Status),
                PaymentStatus = row.PaymentStatus,
                PaymentStatusLabel = PaymentStatusLabel(row.PaymentStatus),
                RejectionReason = row.RejectionReason,
                PauseReason = row.PauseReason,
                CancellationReason = CleanCancellationReason(row.CancellationReason),
                StartImmediately = row.StartImmediately,
                RequestedStartAt = row.RequestedStartAt,
                StartAt = row.StartAt,
                EndAt = row.EndAt,
                TargetingMode = row.TargetingMode,
                TargetingModeLabel = TargetingModeLabel(row.TargetingMode),
                DeviceTarget = row.DeviceTarget,
                DeviceTargetLabel = DeviceTargetLabel(row.DeviceTarget),
                // الإحصائيات المعروضة للمالك مبنية على أحداث فعلية لا على أرقام تقديرية.
                ImpressionsCount = actualMetrics.TotalViews,
                UniqueImpressionsCount = actualMetrics.UniqueViews,
                ClicksCount = actualMetrics.TotalClicks,
                BookingStartsCount = actualMetrics.BookingStarts,
                ConversionsCount = actualMetrics.CompletedBookings,
                ClickThroughRate = Percentage(actualMetrics.TotalClicks, actualMetrics.TotalViews),
                ConversionRate = Percentage(actualMetrics.CompletedBookings, actualMetrics.TotalClicks),
                PlatformWalletId = row.PlatformWalletId,
                WalletName = wallet?.WalletName,
                WalletAccountNumber = wallet?.AccountNumber,
                WalletBeneficiaryName = wallet?.BeneficiaryName,
                TransferredAmount = row.TransferredAmount,
                TransferredAmountText = row.TransferredAmount.HasValue
                    ? await FormatMoneyAsync(row.TransferredAmount.Value, place.ExchangeRateRegion)
                    : null,
                ReferenceNumber = row.ReferenceNumber,
                TransferDate = row.TransferDate,
                ReceiptImage = row.ReceiptImage,

                RefundRequestedAt = row.RefundRequestedAt,
                RefundAmount = row.RefundAmount,
                RefundAmountText = row.RefundAmount.HasValue
                    ? await FormatMoneyAsync(row.RefundAmount.Value, place.ExchangeRateRegion)
                    : null,
                RefundWalletId = row.RefundWalletId,
                RefundWalletName = refundWalletName,
                RefundAccountNumber = row.RefundAccountNumber,
                RefundBeneficiaryName = row.RefundBeneficiaryName,
                RefundReferenceNumber = row.RefundReferenceNumber,
                RefundProofImage = row.RefundProofImage,
                RefundedAt = row.RefundedAt,
                RefundedByName = refundedByName,
                RefundNote = row.RefundNote,
                RefundStatusLabel = RefundStatusLabel(row.Status, row.PaymentStatus, row.RefundRequestedAt, row.RefundedAt),
                RefundWallets = refundWallets,

                SubmittedAt = row.SubmittedAt,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt,
                RowVersion = row.RowVersion,
                CanEdit = capabilities.CanEdit,
                CanSubmit = capabilities.CanSubmit,
                CanPay = capabilities.CanPay,
                CanPause = capabilities.CanPause,
                CanResume = capabilities.CanResume,
                CanCancel = capabilities.CanCancel,
                CanRenew = capabilities.CanRenew,
                CanRestore = capabilities.CanRestore,
                Media = media,
                Targets = targets
            };
        }

        public async Task<OwnerAdvertisementOperationResultDto> SaveDraftAsync(
            OwnerAdvertisementSaveDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            EnsureOwner(ownerId);
            var place = await ResolveOwnedPlaceAsync(ownerId, requestedPlaceId, cancellationToken);
            if (place == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail("اختر منشأة صحيحة من الهيدر أولًا.");
            }

            // تعديل المحتوى فقط له مسار مستقل: لا نعيد تسعير الإعلان، ولا نغير الباقة/المدة/الاستهداف،
            // ولا نمس بيانات الدفع المعتمدة. نسمح فقط بعنوان الإعلان ونص الزر، بينما الوسيط
            // (الصورة/الفيديو) يُستبدل بعد هذه الخطوة من نفس معالج الوسائط الحالي
            if (request.AdvertisementId.HasValue && IsContentOnlyEditMode(request.EditMode))
            {
                return await SaveContentOnlyEditAsync(
                    request,
                    ownerId,
                    place.PlaceId,
                    cancellationToken);
            }

            var validation = await ValidateSaveRequestAsync(request, ownerId, place, cancellationToken);
            if (validation != null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(validation);
            }

            var plan = await _context.AdvertisementPlans
                .AsNoTracking()
                .FirstAsync(item => item.AdvertisementPlanId == request.AdvertisementPlanId, cancellationToken);
            var priceQuote = CalculatePlanQuote(plan, request);
            if (!priceQuote.Success)
            {
                return OwnerAdvertisementOperationResultDto.Fail(priceQuote.Message);
            }

            Advertisement entity;
            var isNew = !request.AdvertisementId.HasValue;
            var paymentTermsChanged = false;

            if (isNew)
            {
                entity = new Advertisement
                {
                    OwnerId = ownerId,
                    PlaceId = place.PlaceId,
                    CreatedAt = DateTime.Now,
                    Status = OwnerAdvertisementStatuses.Draft,
                    PaymentStatus = OwnerAdvertisementPaymentStatuses.Unpaid,
                    Currency = BaseCurrency,
                    SpentAmount = 0m,
                    ImpressionsCount = 0,
                    UniqueImpressionsCount = 0,
                    ClicksCount = 0,
                    BookingStartsCount = 0,
                    ConversionsCount = 0,
                    RotationWeight = 100,
                    RenewalCount = 0
                };
                _context.Advertisements.Add(entity);
            }
            else
            {
                entity = await _context.Advertisements
                    .FirstOrDefaultAsync(item =>
                        item.AdvertisementId == request.AdvertisementId!.Value &&
                        item.OwnerId == ownerId &&
                        item.PlaceId == place.PlaceId,
                        cancellationToken) ?? new Advertisement();

                if (entity.AdvertisementId == 0)
                {
                    return OwnerAdvertisementOperationResultDto.Fail("الإعلان غير موجود أو لا يخص المنشأة المحددة.");
                }

                if (!CanEdit(entity.Status, entity.StartAt))
                {
                    return OwnerAdvertisementOperationResultDto.Fail("لا يمكن تعديل الإعلان في حالته الحالية.");
                }

                if (!TryApplyRowVersion(entity, request.RowVersion))
                {
                    return OwnerAdvertisementOperationResultDto.Fail("تم تعديل الإعلان من جلسة أخرى. حدّث الصفحة وحاول مجددًا.");
                }

                paymentTermsChanged = entity.AdvertisementPlanId != plan.AdvertisementPlanId ||
                                      CampaignDurationDays(entity.StartAt, entity.EndAt, entity.DurationDaysSnapshot) !=
                                          EffectiveDurationDays(plan.BillingModel, request.DurationDays) ||
                                      entity.TotalBudget != NormalizeBudget(plan.BillingModel, request.TotalBudget) ||
                                      entity.PriceSnapshot != SnapshotPrice(plan.BillingModel, plan.Price, priceQuote.FinalAmount);
            }

            ApplyDraft(entity, request, plan, priceQuote, place.PlaceId);

            // Payment is preserved when the edit does not change pricing terms.
            // Only changing plan/duration/budget/price invalidates the old payment.
            if (!isNew && paymentTermsChanged)
            {
                ClearPayment(entity);
            }

            entity.Status = OwnerAdvertisementStatuses.Draft;
            entity.RejectionReason = null;
            entity.ReviewedAt = null;
            entity.ReviewedByUserId = null;
            entity.UpdatedAt = DateTime.Now;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await ReplaceTargetsAsync(entity.AdvertisementId, request, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OwnerAdvertisementOperationResultDto.Fail("تم تعديل الإعلان من جلسة أخرى. حدّث الصفحة وحاول مجددًا.");
            }
            catch (DbUpdateException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(exception, "Failed to save owner advertisement draft for owner {OwnerId}", ownerId);
                return OwnerAdvertisementOperationResultDto.Fail(DatabaseSaveMessage(exception));
            }

            return AdvertisementSuccess("تم حفظ مسودة الإعلان.", entity);
        }

        private async Task<OwnerAdvertisementOperationResultDto> SaveContentOnlyEditAsync(
            OwnerAdvertisementSaveDto request,
            int ownerId,
            int placeId,
            CancellationToken cancellationToken)
        {
            var advertisement = await _context.Advertisements
                .FirstOrDefaultAsync(item =>
                    item.AdvertisementId == request.AdvertisementId!.Value &&
                    item.OwnerId == ownerId &&
                    item.PlaceId == placeId,
                    cancellationToken);

            if (advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "الإعلان غير موجود أو لا يخص المنشأة المحددة.");
            }

            if (!CanEdit(advertisement.Status, advertisement.StartAt))
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "لا يمكن تعديل الإعلان في حالته الحالية.");
            }

            if (advertisement.PaymentStatus != OwnerAdvertisementPaymentStatuses.Paid)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "تعديل المحتوى بدون دفع جديد متاح فقط للإعلان الذي سبق اعتماد دفعه.");
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            var title = NormalizeOptional(request.AdvertisementTitle);
            if (title == null || title.Length < 3 || title.Length > 150)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "عنوان الإعلان مطلوب ويجب أن يكون بين 3 و150 حرفًا.");
            }

            var buttonText = NormalizeOptional(request.ButtonText);
            if (buttonText == null || buttonText.Length < 2 || buttonText.Length > 50)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "نص زر الإعلان يجب أن يكون بين حرفين و50 حرفًا.");
            }

            // مهم: لا نستخدم ApplyDraft هنا حتى لا يستطيع أي طلب يدوي تغيير الباقة أو المدة
            // أو السعر أو الاستهداف أو العرض المرتبط أثناء وضع ContentOnly.
            advertisement.AdvertisementTitle = title;
            advertisement.ButtonText = buttonText;

            // نخرج النسخة الحالية من العرض قبل استبدال الصورة/الفيديو، ثم نعيد إرسالها للإدارة.
            // ReviewedAt/ReviewedByUserId يبقيان كما هما عمدًا، حتى يستطيع مسار الإدارة تمييز
            // أن الطلب الحالي تعديل لمحتوى إعلان سبق اعتماده، وليس إعلانًا جديدًا.
            advertisement.Status = OwnerAdvertisementStatuses.Draft;
            advertisement.RejectionReason = null;
            advertisement.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyFailure();
            }

            return AdvertisementSuccess(
                "تم حفظ تعديل المحتوى. سيتم إرساله للمراجعة دون طلب دفع جديد.",
                advertisement);
        }

        public async Task<OwnerAdvertisementOperationResultDto> SubmitAsync(
            OwnerAdvertisementSubmitDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId, ownerId, requestedPlaceId, cancellationToken);
            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            var isAdminPlace = string.Equals(
                loaded.Place?.OwnerRole,
                "Admin",
                StringComparison.OrdinalIgnoreCase);

            if (!CanSubmit(advertisement.Status))
            {
                return OwnerAdvertisementOperationResultDto.Fail("لا يمكن إرسال الإعلان في حالته الحالية.");
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            var readiness = await ValidateReadyForSubmissionAsync(advertisement, cancellationToken);
            if (readiness != null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(readiness);
            }

            var expectedPayment = ExpectedPayment(advertisement);
            var paymentWasAlreadyApproved =
                advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Paid;
            var isApprovedContentEdit =
                paymentWasAlreadyApproved && advertisement.ReviewedAt.HasValue;
            var paymentComplete =
                isAdminPlace ||
                paymentWasAlreadyApproved ||
                expectedPayment <= 0m ||
                HasCompletePayment(advertisement, expectedPayment);

            advertisement.SubmittedAt = DateTime.Now;
            advertisement.UpdatedAt = DateTime.Now;
            advertisement.RejectionReason = null;

            if (isAdminPlace)
            {
                // منشآت الإدارة لا تحتاج دفع أو مراجعة أو موافقة.
                advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.Paid;

                if (advertisement.StartImmediately == true)
                {
                    advertisement.Status = OwnerAdvertisementStatuses.Active;
                    advertisement.StartAt = DateTime.Now;
                    advertisement.EndAt = DateTime.Now.AddDays(
                        Math.Max(1, advertisement.DurationDaysSnapshot));
                }
                else
                {
                    advertisement.Status = OwnerAdvertisementStatuses.Scheduled;
                    advertisement.StartAt = advertisement.RequestedStartAt;
                    advertisement.EndAt = advertisement.RequestedStartAt.HasValue
                        ? advertisement.RequestedStartAt.Value.AddDays(
                            Math.Max(1, advertisement.DurationDaysSnapshot))
                        : DateTime.Now.AddDays(Math.Max(1, advertisement.DurationDaysSnapshot));
                }
            }
            else if (paymentComplete)
            {
                advertisement.Status = OwnerAdvertisementStatuses.Pending;

                // إذا كان هذا تعديل محتوى لإعلان سبق دفعه واعتماده فلا نعيد الدفع للمراجعة.
                // أما الإعلان الجديد/إعادة التسعير فيبقى إثبات الدفع بحاجة مراجعة الإدارة.
                if (!paymentWasAlreadyApproved)
                {
                    advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.PendingReview;
                }
            }
            else
            {
                advertisement.Status = OwnerAdvertisementStatuses.AwaitingPayment;
                if (advertisement.PaymentStatus != OwnerAdvertisementPaymentStatuses.Rejected)
                {
                    advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.Unpaid;
                }
            }

            try
            {
                if (paymentComplete && !isAdminPlace)
                {
                    await _notificationService.QueueForAdminPermissionAsync(
                        AdminPermissionKeys.Advertisements.Review,
                        new CreateNotificationDto
                        {
                            NotificationTitle = isApprovedContentEdit
                                ? "تعديلات محتوى إعلان تحتاج مراجعة"
                                : "إعلان جديد يحتاج مراجعة",
                            NotificationText = isApprovedContentEdit
                                ? $"عدّل مالك {loaded.Place!.PlaceName} محتوى الإعلان «{advertisement.AdvertisementTitle}». الدفع معتمد مسبقًا وتحتاج التعديلات موافقة الإدارة."
                                : $"أرسل مالك {loaded.Place!.PlaceName} الإعلان «{advertisement.AdvertisementTitle}» للمراجعة.",
                            NotificationType = NotificationTypes.AdminAdvertisementActionRequired,
                            TargetUrl =
                                $"/Admin/Advertisements/AdvertisementManagement?tab=ads&group=NeedsAction&focusAdvertisement={advertisement.AdvertisementId}",
                            ReferenceType = NotificationReferenceTypes.Advertisement,
                            ReferenceId = advertisement.AdvertisementId
                        },
                        cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyFailure();
            }

            var paymentText = await FormatMoneyAsync(expectedPayment, loaded.Place!.ExchangeRateRegion);
            var submitMessage = isAdminPlace
                ? (advertisement.Status == OwnerAdvertisementStatuses.Scheduled
                    ? "تم حفظ الإعلان وسيُعرض قريبًا."
                    : "تم حفظ الإعلان وعرضه.")
                : paymentComplete
                    ? (isApprovedContentEdit
                        ? "تم إرسال تعديلات المحتوى للإدارة دون طلب دفع جديد."
                        : "تم إرسال الإعلان للمراجعة.")
                    : "تم حفظ الإعلان وهو الآن بانتظار رفع إثبات الدفع.";

            return OwnerAdvertisementOperationResultDto.Ok(
                submitMessage,
                advertisement.AdvertisementId,
                advertisement.Status,
                advertisement.PaymentStatus,
                ToRowVersion(advertisement.RowVersion),
                expectedPayment,
                paymentText);
        }

        public async Task<OwnerAdvertisementOperationResultDto> SavePaymentProofAsync(
            OwnerAdvertisementPaymentSaveDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId, ownerId, requestedPlaceId, cancellationToken);
            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            var isAdminPlace = string.Equals(
                loaded.Place?.OwnerRole,
                "Admin",
                StringComparison.OrdinalIgnoreCase);

            if (isAdminPlace)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "إعلانات منشآت الإدارة لا تحتاج إلى دفع أو رفع إثبات دفع.");
            }

            if (advertisement.Status != OwnerAdvertisementStatuses.AwaitingPayment &&
                advertisement.Status != OwnerAdvertisementStatuses.Draft &&
                advertisement.Status != OwnerAdvertisementStatuses.Rejected)
            {
                return OwnerAdvertisementOperationResultDto.Fail("لا يمكن تعديل الدفع في حالة الإعلان الحالية.");
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            var readiness = await ValidateReadyForSubmissionAsync(advertisement, cancellationToken);
            if (readiness != null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(readiness);
            }

            var expectedPayment = ExpectedPayment(advertisement);
            if (expectedPayment <= 0m)
            {
                return OwnerAdvertisementOperationResultDto.Fail("هذا الإعلان لا يحتاج إلى حوالة مالية.");
            }

            if (request.TransferredAmount < expectedPayment)
            {
                return OwnerAdvertisementOperationResultDto.Fail("المبلغ المحول أقل من المبلغ المطلوب.");
            }

            if (!request.TransferDate.HasValue || request.TransferDate.Value.Date > DateTime.Today)
            {
                return OwnerAdvertisementOperationResultDto.Fail("تاريخ الحوالة غير صحيح.");
            }

            var walletExists = await _context.PlatformWallets
                .AsNoTracking()
                .AnyAsync(item =>
                    item.PlatformWalletId == request.PlatformWalletId &&
                    item.IsActive == true,
                    cancellationToken);
            if (!walletExists)
            {
                return OwnerAdvertisementOperationResultDto.Fail("محفظة المنصة غير موجودة أو غير مفعلة.");
            }

            var reference = NormalizeOptional(request.ReferenceNumber);
            if (reference != null)
            {
                var duplicateReference = await _context.Advertisements
                    .AsNoTracking()
                    .AnyAsync(item =>
                        item.AdvertisementId != advertisement.AdvertisementId &&
                        item.PlatformWalletId == request.PlatformWalletId &&
                        item.ReferenceNumber == reference,
                        cancellationToken);
                if (duplicateReference)
                {
                    return OwnerAdvertisementOperationResultDto.Fail("رقم الحوالة مستخدم في إعلان آخر.");
                }
            }

            var submittedReceiptPath = NormalizeOptional(request.ReceiptImagePath);
            if (submittedReceiptPath != null &&
                !IsOwnedUploadPath(submittedReceiptPath, ownerId, advertisement.AdvertisementId, "receipts"))
            {
                return OwnerAdvertisementOperationResultDto.Fail("مسار إثبات الدفع غير صالح.");
            }

            var receiptPath = submittedReceiptPath ?? advertisement.ReceiptImage;
            if (string.IsNullOrWhiteSpace(receiptPath))
            {
                return OwnerAdvertisementOperationResultDto.Fail("صورة إثبات الدفع مطلوبة.");
            }

            var oldReceipt = advertisement.ReceiptImage;
            advertisement.PlatformWalletId = request.PlatformWalletId;
            advertisement.TransferredAmount = request.TransferredAmount;
            advertisement.ReferenceNumber = reference;
            advertisement.TransferDate = request.TransferDate.Value;
            advertisement.ReceiptImage = receiptPath;
            advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.PendingReview;
            advertisement.Status = OwnerAdvertisementStatuses.Pending;
            advertisement.SubmittedAt = DateTime.Now;
            advertisement.RejectionReason = null;
            advertisement.ReviewedAt = null;
            advertisement.ReviewedByUserId = null;
            advertisement.UpdatedAt = DateTime.Now;

            try
            {
                await _notificationService.QueueForAdminPermissionAsync(
                    AdminPermissionKeys.Advertisements.Review,
                    new CreateNotificationDto
                    {
                        NotificationTitle = "إثبات دفع إعلان يحتاج مراجعة",
                        NotificationText =
                            $"رفع مالك {loaded.Place!.PlaceName} إثبات دفع للإعلان «{advertisement.AdvertisementTitle}» ويحتاج اعتماد الإدارة.",
                        NotificationType = NotificationTypes.AdminAdvertisementActionRequired,
                        TargetUrl =
                            $"/Admin/Advertisements/AdvertisementManagement?tab=ads&group=NeedsAction&focusAdvertisement={advertisement.AdvertisementId}",
                        ReferenceType = NotificationReferenceTypes.Advertisement,
                        ReferenceId = advertisement.AdvertisementId
                    },
                    cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyFailure();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(exception, "Duplicate or invalid advertisement payment data");
                return OwnerAdvertisementOperationResultDto.Fail("تعذر حفظ إثبات الدفع. تحقق من بيانات التحويل وحاول مجددًا.");
            }

            return OwnerAdvertisementOperationResultDto.Ok(
                "تم رفع إثبات الدفع وإرسال الإعلان للمراجعة.",
                advertisement.AdvertisementId,
                advertisement.Status,
                advertisement.PaymentStatus,
                ToRowVersion(advertisement.RowVersion),
                expectedPayment,
                await FormatMoneyAsync(expectedPayment, loaded.Place!.ExchangeRateRegion),
                oldReceipt != receiptPath ? oldReceipt : null);
        }

        public async Task<OwnerAdvertisementOperationResultDto> AddMediaAsync(
            OwnerAdvertisementMediaSaveDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId, ownerId, requestedPlaceId, cancellationToken);
            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            if (!CanEdit(advertisement.Status, advertisement.StartAt))
            {
                return OwnerAdvertisementOperationResultDto.Fail("لا يمكن تعديل وسائط الإعلان في حالته الحالية.");
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            var mediaType = NormalizeMediaType(request.MediaType);
            if (mediaType == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail("نوع ملف الإعلان غير مدعوم.");
            }

            var filePath = request.FilePath.Trim();
            if (!IsOwnedUploadPath(filePath, ownerId, advertisement.AdvertisementId, "media"))
            {
                return OwnerAdvertisementOperationResultDto.Fail("مسار ملف الإعلان غير صالح.");
            }

            var plan = await _context.AdvertisementPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.AdvertisementPlanId == advertisement.AdvertisementPlanId, cancellationToken);
            if (plan == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail("باقة الإعلان غير موجودة.");
            }

            var currentCount = await _context.AdvertisementMedia
                .AsNoTracking()
                .CountAsync(item =>
                    item.AdvertisementId == advertisement.AdvertisementId &&
                    item.IsActive == true,
                    cancellationToken);

            if (currentCount >= 1)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "يسمح الإعلان بوسيط واحد فقط: صورة أو فيديو.");
            }

            if (mediaType == OwnerAdvertisementMediaTypes.Video && !plan.AllowVideo)
            {
                return OwnerAdvertisementOperationResultDto.Fail("الباقة المختارة لا تسمح بالفيديو.");
            }

            var isPrimary = true;
            var displayOrder = 0;
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                if (isPrimary)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE dbo.AdvertisementMedia SET IsPrimary = 0 WHERE AdvertisementID = {advertisement.AdvertisementId}",
                        cancellationToken);
                }

                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO dbo.AdvertisementMedia
                        (AdvertisementID, MediaType, FilePath, ThumbnailPath, AltText, DisplayOrder, IsPrimary, IsActive, CreatedAt)
                    VALUES
                        ({advertisement.AdvertisementId}, {mediaType}, {filePath}, {NormalizeOptional(request.ThumbnailPath)},
                         {NormalizeOptional(request.AltText)}, {displayOrder}, {isPrimary}, {true}, {DateTime.Now})",
                    cancellationToken);

                if (isPrimary && mediaType == OwnerAdvertisementMediaTypes.Image)
                {
                    advertisement.ImagePath = filePath;
                }

                advertisement.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ConcurrencyFailure();
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(exception, "Failed to add media to advertisement {AdvertisementId}", advertisement.AdvertisementId);
                return OwnerAdvertisementOperationResultDto.Fail("تعذر حفظ الوسائط. حاول مرة أخرى.");
            }

            return OwnerAdvertisementOperationResultDto.Ok(
                "تمت إضافة الوسائط.",
                advertisement.AdvertisementId,
                advertisement.Status,
                advertisement.PaymentStatus,
                ToRowVersion(advertisement.RowVersion));
        }

        public async Task<OwnerAdvertisementOperationResultDto> DeleteMediaAsync(
            OwnerAdvertisementMediaDeleteDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId, ownerId, requestedPlaceId, cancellationToken);
            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            if (!CanEdit(advertisement.Status, advertisement.StartAt))
            {
                return OwnerAdvertisementOperationResultDto.Fail("لا يمكن حذف الوسائط في حالة الإعلان الحالية.");
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            var media = await _context.AdvertisementMedia
                .AsNoTracking()
                .Where(item =>
                    item.AdvertisementMediaId == request.AdvertisementMediaId &&
                    item.AdvertisementId == advertisement.AdvertisementId)
                .Select(item => new
                {
                    item.AdvertisementMediaId,
                    item.FilePath,
                    item.ThumbnailPath,
                    item.IsPrimary
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (media == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail("ملف الوسائط غير موجود.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM dbo.AdvertisementMedia WHERE AdvertisementMediaID = {media.AdvertisementMediaId} AND AdvertisementID = {advertisement.AdvertisementId}",
                    cancellationToken);

                if (media.IsPrimary)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE dbo.AdvertisementMedia
                        SET IsPrimary = 1
                        WHERE AdvertisementMediaID =
                        (
                            SELECT TOP (1) AdvertisementMediaID
                            FROM dbo.AdvertisementMedia
                            WHERE AdvertisementID = {advertisement.AdvertisementId} AND IsActive = 1
                            ORDER BY DisplayOrder, AdvertisementMediaID
                        )",
                        cancellationToken);
                }

                var newPrimary = await _context.AdvertisementMedia
                    .AsNoTracking()
                    .Where(item => item.AdvertisementId == advertisement.AdvertisementId && item.IsPrimary)
                    .Select(item => new { item.FilePath, item.MediaType })
                    .FirstOrDefaultAsync(cancellationToken);
                advertisement.ImagePath = newPrimary?.MediaType == OwnerAdvertisementMediaTypes.Image
                    ? newPrimary.FilePath
                    : null;
                advertisement.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ConcurrencyFailure();
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(exception, "Failed to delete advertisement media {MediaId}", request.AdvertisementMediaId);
                return OwnerAdvertisementOperationResultDto.Fail("تعذر حذف الوسائط.");
            }

            return OwnerAdvertisementOperationResultDto.Ok(
                "تم حذف الوسائط.",
                advertisement.AdvertisementId,
                advertisement.Status,
                advertisement.PaymentStatus,
                ToRowVersion(advertisement.RowVersion),
                affectedFilePath: media.FilePath);
        }

        public Task<OwnerAdvertisementOperationResultDto> PauseAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            return ChangeStateAsync(request, ownerId, requestedPlaceId, StateOperation.Pause, cancellationToken);
        }

        public Task<OwnerAdvertisementOperationResultDto> ResumeAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            return ChangeStateAsync(request, ownerId, requestedPlaceId, StateOperation.Resume, cancellationToken);
        }

        public async Task<OwnerAdvertisementOperationResultDto> CancelAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            // المسودة أو الإعلان الذي ما زال بانتظار الدفع ولم يدخل المراجعة
            // يُحذف نهائياً بدل الاحتفاظ به كسجل ملغي.
            var deleteResult = await TryDeleteUnstartedUnpaidAdvertisementAsync(
                request,
                ownerId,
                requestedPlaceId,
                cancellationToken);

            if (deleteResult != null)
            {
                return deleteResult;
            }

            // بمجرد دخول الإعلان المراجعة/الدفع المعتمد/الجدولة أو العرض،
            // نحافظ عليه كسجل تاريخي ونستخدم الإلغاء العادي بدلاً من الحذف.
            return await ChangeStateAsync(
                request,
                ownerId,
                requestedPlaceId,
                StateOperation.Cancel,
                cancellationToken);
        }

        private async Task<OwnerAdvertisementOperationResultDto?> TryDeleteUnstartedUnpaidAdvertisementAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId,
                ownerId,
                requestedPlaceId,
                cancellationToken);

            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            var deleteCandidate =
                (advertisement.Status == OwnerAdvertisementStatuses.Draft ||
                 advertisement.Status == OwnerAdvertisementStatuses.AwaitingPayment) &&
                advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Unpaid;

            if (!deleteCandidate)
            {
                return null;
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            // حماية السجل التاريخي: إذا دخل الإعلان فعلياً في مراجعة/تشغيل سابق،
            // أو أصبح له أثر إعلاني/حجز/بلاغ، لا نسمح بالحذف النهائي حتى لو عاد
            // لاحقاً إلى حالة تشبه المسودة (مثلاً بعد تجديد أو تعديل).
            var hasNotificationHistory = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(item =>
                    item.ReferenceType == NotificationReferenceTypes.Advertisement &&
                    item.ReferenceId == advertisement.AdvertisementId,
                    cancellationToken);

            var hasAdvertisementEvents = await _context.AdvertisementEvents
                .AsNoTracking()
                .AnyAsync(item =>
                    item.AdvertisementId == advertisement.AdvertisementId,
                    cancellationToken);

            var hasBookings = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(item =>
                    item.AdvertisementId == advertisement.AdvertisementId,
                    cancellationToken);

            var hasReports = await _context.Reports
                .AsNoTracking()
                .AnyAsync(item =>
                    item.AdvertisementId == advertisement.AdvertisementId,
                    cancellationToken);

            var hasHistoricalLifecycle =
                hasNotificationHistory ||
                hasAdvertisementEvents ||
                hasBookings ||
                hasReports ||
                advertisement.RenewalCount > 0 ||
                advertisement.ReviewedAt.HasValue ||
                advertisement.RefundRequestedAt.HasValue ||
                advertisement.RefundedAt.HasValue ||
                advertisement.TransferredAmount.HasValue ||
                !string.IsNullOrWhiteSpace(advertisement.ReceiptImage);

            if (hasHistoricalLifecycle)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    "لا يمكن حذف هذا الإعلان نهائياً لأنه دخل دورة المراجعة أو أصبح له سجل تاريخي. استخدم الإلغاء بدلاً من الحذف.");
            }

            var media = await _context.AdvertisementMedia
                .Where(item => item.AdvertisementId == advertisement.AdvertisementId)
                .ToListAsync(cancellationToken);

            var targets = await _context.AdvertisementTargets
                .Where(item => item.AdvertisementId == advertisement.AdvertisementId)
                .ToListAsync(cancellationToken);

            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                if (media.Count > 0)
                {
                    _context.AdvertisementMedia.RemoveRange(media);
                }

                if (targets.Count > 0)
                {
                    _context.AdvertisementTargets.RemoveRange(targets);
                }

                _context.Advertisements.Remove(advertisement);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ConcurrencyFailure();
            }
            catch (DbUpdateException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    exception,
                    "Failed to permanently delete owner advertisement {AdvertisementId}",
                    advertisement.AdvertisementId);

                return OwnerAdvertisementOperationResultDto.Fail(
                    "تعذر حذف الإعلان لأنه مرتبط ببيانات أخرى. يمكنك إلغاء الإعلان بدلاً من حذفه.");
            }

            TryDeleteAdvertisementUploadDirectory(ownerId, advertisement.AdvertisementId);

            return OwnerAdvertisementOperationResultDto.Ok(
                "تم حذف الإعلان نهائياً.",
                advertisement.AdvertisementId,
                "Deleted",
                advertisement.PaymentStatus);
        }

        private void TryDeleteAdvertisementUploadDirectory(int ownerId, int advertisementId)
        {
            try
            {
                var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                    ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                    : _environment.WebRootPath;

                var directory = Path.Combine(
                    webRoot,
                    "uploads",
                    "advertisements",
                    ownerId.ToString(CultureInfo.InvariantCulture),
                    advertisementId.ToString(CultureInfo.InvariantCulture));

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception exception)
            {
                // فشل تنظيف الملف لا يعيد السجل المحذوف إلى قاعدة البيانات.
                // نسجل الخطأ حتى يمكن تنظيف الملفات اليتيمة لاحقاً دون تعطيل المستخدم.
                _logger.LogWarning(
                    exception,
                    "Advertisement {AdvertisementId} was deleted but its upload directory could not be removed.",
                    advertisementId);
            }
        }

        public async Task<OwnerAdvertisementOperationResultDto> RestoreAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId, ownerId, requestedPlaceId, cancellationToken);
            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            if (advertisement.Status != OwnerAdvertisementStatuses.Cancelled)
            {
                return OwnerAdvertisementOperationResultDto.Fail("يمكن استعادة الإعلانات الملغية فقط.");
            }

            if (advertisement.RefundRequestedAt.HasValue ||
                advertisement.RefundedAt.HasValue ||
                advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Refunded)
            {
                return OwnerAdvertisementOperationResultDto.Fail(
                    advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Refunded
                        ? "تم استرداد مبلغ هذا الإعلان، لذلك لا يمكن استعادته. أنشئ إعلانًا جديدًا إذا رغبت في حملة أخرى."
                        : "هذا الإعلان دخل في دورة الاسترداد ولا يمكن استعادته أثناء انتظار إعادة المبلغ.");
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            var now = DateTime.Now;
            var cancelledAt = advertisement.UpdatedAt;

            // الإعلان الملغي قبل الدفع يمكن استعادته للعودة إلى خطوة الدفع.
            if (advertisement.PaymentStatus != OwnerAdvertisementPaymentStatuses.Paid)
            {
                advertisement.Status = OwnerAdvertisementStatuses.AwaitingPayment;
                advertisement.CancellationReason = null;
                advertisement.PausedAt = null;
                advertisement.PauseReason = null;
                advertisement.UpdatedAt = now;

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return ConcurrencyFailure();
                }

                return AdvertisementSuccess(
                    "تمت استعادة الإعلان. أكمل الدفع لإرساله إلى الإدارة.",
                    advertisement);
            }

            // إذا أُلغي الإعلان قبل بدايته المجدولة، نحافظ على كامل مدته.
            var wasScheduledBeforeCancellation =
                advertisement.StartAt.HasValue &&
                advertisement.StartAt.Value > cancelledAt;

            if (wasScheduledBeforeCancellation)
            {
                var fullDuration = advertisement.EndAt.HasValue
                    ? advertisement.EndAt.Value - advertisement.StartAt!.Value
                    : TimeSpan.FromDays(Math.Max(1, advertisement.DurationDaysSnapshot));

                if (fullDuration <= TimeSpan.Zero)
                {
                    fullDuration = TimeSpan.FromDays(Math.Max(1, advertisement.DurationDaysSnapshot));
                }

                if (advertisement.StartAt!.Value > now)
                {
                    // الموعد الأصلي ما زال مستقبليًا: نرجع الإعلان للجدولة كما هو.
                    advertisement.Status = OwnerAdvertisementStatuses.Scheduled;
                    advertisement.EndAt = advertisement.StartAt.Value.Add(fullDuration);
                }
                else
                {
                    // الموعد الأصلي مر أثناء فترة الإلغاء: يبدأ الآن بكامل المدة المدفوعة.
                    advertisement.StartAt = now;
                    advertisement.EndAt = now.Add(fullDuration);
                    advertisement.Status = OwnerAdvertisementStatuses.Active;
                }
            }
            else
            {
                // إعلان كان قد بدأ: نعيد فقط المدة التي كانت متبقية لحظة الإلغاء.
                var remaining = advertisement.EndAt.HasValue
                    ? advertisement.EndAt.Value - cancelledAt
                    : TimeSpan.FromDays(Math.Max(1, advertisement.DurationDaysSnapshot));

                if (remaining <= TimeSpan.Zero)
                {
                    return OwnerAdvertisementOperationResultDto.Fail(
                        "لم تعد هناك مدة متبقية للاستعادة. استخدم التجديد بعد انتهاء الإعلان.");
                }

                advertisement.EndAt = now.Add(remaining);
                advertisement.Status = OwnerAdvertisementStatuses.Active;
            }

            advertisement.CancellationReason = null;
            advertisement.PausedAt = null;
            advertisement.PauseReason = null;
            advertisement.RefundRequestedAt = null;
            advertisement.RefundAmount = null;
            advertisement.RefundWalletId = null;
            advertisement.RefundAccountNumber = null;
            advertisement.RefundBeneficiaryName = null;
            advertisement.RefundReferenceNumber = null;
            advertisement.RefundProofImage = null;
            advertisement.RefundedAt = null;
            advertisement.RefundedByUserId = null;
            advertisement.RefundNote = null;
            advertisement.UpdatedAt = now;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyFailure();
            }

            return AdvertisementSuccess(
                advertisement.Status == OwnerAdvertisementStatuses.Scheduled
                    ? "تمت استعادة الإعلان وإعادته إلى الجدولة."
                    : "تمت استعادة الإعلان وسيكمل المدة المتبقية.",
                advertisement);
        }

        public async Task<OwnerAdvertisementOperationResultDto> RenewAsync(
            OwnerAdvertisementRenewDto request,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId, ownerId, requestedPlaceId, cancellationToken);
            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            if (advertisement.Status != OwnerAdvertisementStatuses.Expired)
            {
                return OwnerAdvertisementOperationResultDto.Fail("يمكن تجديد الإعلانات المنتهية فقط.");
            }

            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            if (!request.StartImmediately &&
                (!request.RequestedStartAt.HasValue || request.RequestedStartAt.Value <= DateTime.Now))
            {
                return OwnerAdvertisementOperationResultDto.Fail("اختر موعد بداية مستقبليًا.");
            }

            var plan = await _context.AdvertisementPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AdvertisementPlanId == advertisement.AdvertisementPlanId &&
                    item.IsActive == true,
                    cancellationToken);
            if (plan == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail("خيار التسعير لم يعد متاحًا للتجديد.");
            }

            var renewalDurationDays = CampaignDurationDays(
                advertisement.StartAt,
                advertisement.EndAt,
                advertisement.DurationDaysSnapshot);
            var renewalQuote = CalculatePlanQuote(plan, new OwnerAdvertisementSaveDto
            {
                AdvertisementPlanId = plan.AdvertisementPlanId,
                DurationDays = renewalDurationDays,
                TotalBudget = advertisement.TotalBudget,
                DailyBudget = advertisement.DailyBudget
            });
            if (!renewalQuote.Success)
            {
                return OwnerAdvertisementOperationResultDto.Fail(renewalQuote.Message);
            }

            ArchiveRenewalStatistics(advertisement);
            ApplyPlanSnapshot(
                advertisement,
                plan,
                renewalDurationDays,
                renewalQuote.FinalAmount);
            advertisement.MaxClicks = PositiveOrNull(renewalQuote.EstimatedClicks);
            advertisement.MaxImpressions = PositiveOrNull(renewalQuote.EstimatedImpressions);
            advertisement.StartImmediately = request.StartImmediately;
            advertisement.RequestedStartAt = request.StartImmediately ? null : request.RequestedStartAt;
            SetPlannedCampaignRange(advertisement, renewalDurationDays, request.StartImmediately, request.RequestedStartAt);
            var isAdminPlace = string.Equals(
                loaded.Place?.OwnerRole,
                "Admin",
                StringComparison.OrdinalIgnoreCase);

            advertisement.Status = OwnerAdvertisementStatuses.AwaitingPayment;
            advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.Unpaid;
            advertisement.SpentAmount = 0m;
            advertisement.ImpressionsCount = 0;
            advertisement.UniqueImpressionsCount = 0;
            advertisement.ClicksCount = 0;
            advertisement.BookingStartsCount = 0;
            advertisement.ConversionsCount = 0;
            advertisement.LastImpressionAt = null;
            advertisement.LastClickAt = null;
            advertisement.LastServedAt = null;
            advertisement.RenewalCount += 1;
            advertisement.PauseReason = null;
            advertisement.PausedAt = null;
            advertisement.CancellationReason = null;
            advertisement.RejectionReason = null;
            advertisement.SubmittedAt = null;
            ClearPayment(advertisement);
            advertisement.UpdatedAt = DateTime.Now;

            var expectedPayment = ExpectedPayment(advertisement);
            if (isAdminPlace)
            {
                // تجديد إعلان منشأة الإدارة لا يمر بالدفع أو المراجعة الإدارية.
                advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.Paid;
                advertisement.SubmittedAt = DateTime.Now;

                if (advertisement.StartImmediately == true)
                {
                    advertisement.Status = OwnerAdvertisementStatuses.Active;
                    advertisement.StartAt = DateTime.Now;
                    advertisement.EndAt = DateTime.Now.AddDays(
                        Math.Max(1, advertisement.DurationDaysSnapshot));
                }
                else
                {
                    advertisement.Status = OwnerAdvertisementStatuses.Scheduled;
                    advertisement.StartAt = advertisement.RequestedStartAt;
                    advertisement.EndAt = advertisement.RequestedStartAt.HasValue
                        ? advertisement.RequestedStartAt.Value.AddDays(
                            Math.Max(1, advertisement.DurationDaysSnapshot))
                        : DateTime.Now.AddDays(Math.Max(1, advertisement.DurationDaysSnapshot));
                }
            }
            else if (expectedPayment <= 0m)
            {
                advertisement.Status = OwnerAdvertisementStatuses.Pending;
                advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.PendingReview;
                advertisement.SubmittedAt = DateTime.Now;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyFailure();
            }

            var renewalMessage = isAdminPlace
                ? (advertisement.Status == OwnerAdvertisementStatuses.Scheduled
                    ? "تم تجديد الإعلان وجدولته في الموعد المحدد دون دفع أو مراجعة."
                    : "تم تجديد الإعلان وبدأ عرضه مباشرة دون دفع أو مراجعة.")
                : expectedPayment > 0m
                    ? "تم تجهيز التجديد. ارفع إثبات الدفع لإرساله للمراجعة."
                    : "تم إرسال تجديد الإعلان للمراجعة.";

            return OwnerAdvertisementOperationResultDto.Ok(
                renewalMessage,
                advertisement.AdvertisementId,
                advertisement.Status,
                advertisement.PaymentStatus,
                ToRowVersion(advertisement.RowVersion),
                expectedPayment,
                await FormatMoneyAsync(expectedPayment, loaded.Place!.ExchangeRateRegion));
        }

        private async Task<OwnerAdvertisementOperationResultDto> ChangeStateAsync(
            OwnerAdvertisementStateChangeDto request,
            int ownerId,
            int? requestedPlaceId,
            StateOperation operation,
            CancellationToken cancellationToken)
        {
            var loaded = await LoadOwnedAdvertisementAsync(
                request.AdvertisementId, ownerId, requestedPlaceId, cancellationToken);
            if (loaded.Advertisement == null)
            {
                return OwnerAdvertisementOperationResultDto.Fail(loaded.Error!);
            }

            var advertisement = loaded.Advertisement;
            if (!TryApplyRowVersion(advertisement, request.RowVersion))
            {
                return ConcurrencyFailure();
            }

            var now = DateTime.Now;
            var isAdminPlace = string.Equals(
                loaded.Place?.OwnerRole,
                "Admin",
                StringComparison.OrdinalIgnoreCase);
            string message;
            switch (operation)
            {
                case StateOperation.Pause:
                    if (advertisement.Status != OwnerAdvertisementStatuses.Active)
                    {
                        return OwnerAdvertisementOperationResultDto.Fail("يمكن إيقاف الإعلان النشط فقط.");
                    }

                    advertisement.Status = OwnerAdvertisementStatuses.Paused;
                    advertisement.PausedAt = now;
                    advertisement.PauseReason = NormalizeOptional(request.Reason) ?? "أوقفه المالك مؤقتًا.";
                    message = "تم إيقاف الإعلان مؤقتًا.";
                    break;

                case StateOperation.Resume:
                    if (advertisement.Status != OwnerAdvertisementStatuses.Paused)
                    {
                        return OwnerAdvertisementOperationResultDto.Fail("يمكن استئناف الإعلان المتوقف فقط.");
                    }

                    if (advertisement.PaymentStatus != OwnerAdvertisementPaymentStatuses.Paid)
                    {
                        return OwnerAdvertisementOperationResultDto.Fail("لا يمكن استئناف إعلان غير مدفوع.");
                    }

                    if (advertisement.EndAt.HasValue && advertisement.EndAt.Value <= now)
                    {
                        advertisement.Status = OwnerAdvertisementStatuses.Expired;
                        advertisement.UpdatedAt = now;
                        await _context.SaveChangesAsync(cancellationToken);
                        return OwnerAdvertisementOperationResultDto.Fail("انتهت مدة الإعلان ولا يمكن استئنافه. يمكنك تجديده.");
                    }

                    if (advertisement.TotalBudget.HasValue && advertisement.SpentAmount >= advertisement.TotalBudget.Value)
                    {
                        advertisement.Status = OwnerAdvertisementStatuses.Expired;
                        advertisement.UpdatedAt = now;
                        await _context.SaveChangesAsync(cancellationToken);
                        return OwnerAdvertisementOperationResultDto.Fail("استهلك الإعلان ميزانيته ولا يمكن استئنافه.");
                    }

                    advertisement.Status = advertisement.StartAt.HasValue && advertisement.StartAt.Value > now
                        ? OwnerAdvertisementStatuses.Scheduled
                        : OwnerAdvertisementStatuses.Active;
                    advertisement.PausedAt = null;
                    advertisement.PauseReason = null;
                    message = advertisement.Status == OwnerAdvertisementStatuses.Active
                        ? "تم استئناف الإعلان."
                        : "تمت إعادة الإعلان إلى الجدولة.";
                    break;

                default:
                    if (advertisement.Status == OwnerAdvertisementStatuses.Cancelled ||
                        advertisement.Status == OwnerAdvertisementStatuses.Expired)
                    {
                        return OwnerAdvertisementOperationResultDto.Fail("الإعلان منتهي أو ملغي بالفعل.");
                    }

                    // سياسة الإلغاء: بعد بدء العرض لا يسمح للمالك بالإلغاء أو طلب الاسترداد.
                    // Active يعني أن الحملة بدأت فعليًا، وPaused يعني أنها بدأت ثم أوقفها المالك مؤقتًا.
                    if (advertisement.Status == OwnerAdvertisementStatuses.Active ||
                        advertisement.Status == OwnerAdvertisementStatuses.Paused ||
                        (advertisement.Status == OwnerAdvertisementStatuses.Scheduled &&
                         advertisement.StartAt.HasValue && advertisement.StartAt.Value <= now))
                    {
                        return OwnerAdvertisementOperationResultDto.Fail(
                            "بدأ عرض هذا الإعلان ولا يمكن إلغاؤه أو استرداد المبلغ بعد بدء الحملة.");
                    }

                    var reason = NormalizeOptional(request.Reason);
                    if (reason == null || reason.Length < 5)
                    {
                        return OwnerAdvertisementOperationResultDto.Fail("اكتب سبب الإلغاء بوضوح، خمسة أحرف على الأقل.");
                    }

                    // الاسترداد متاح فقط للإعلان ذي الموعد المحدد ما دام العرض لم يبدأ.
                    // الإعلان الذي يبدأ مباشرة بعد موافقة الإدارة يصبح غير قابل للاسترداد بعد تأكيد الدفع.
                    var needsRefundDestination =
                        !isAdminPlace &&
                        advertisement.StartImmediately != true &&
                        (advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Paid ||
                         advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.PendingReview);

                    OwnerAdvertisementRefundWalletDto? refundDestination = null;
                    if (needsRefundDestination)
                    {
                        if (!request.RefundOwnerWalletId.HasValue || request.RefundOwnerWalletId.Value <= 0)
                        {
                            return OwnerAdvertisementOperationResultDto.Fail(
                                "اختر المحفظة التي تريد استلام مبلغ الاسترداد عليها قبل تأكيد الإلغاء.");
                        }

                        refundDestination = await (
                            from ownerWallet in _context.OwnerWallets.AsNoTracking()
                            join wallet in _context.SupportedWallets.AsNoTracking()
                                on ownerWallet.WalletId equals wallet.WalletId
                            where ownerWallet.OwnerWalletId == request.RefundOwnerWalletId.Value &&
                                  ownerWallet.OwnerId == ownerId &&
                                  ownerWallet.IsActive == true &&
                                  wallet.IsActive == true
                            select new OwnerAdvertisementRefundWalletDto
                            {
                                OwnerWalletId = ownerWallet.OwnerWalletId,
                                WalletId = ownerWallet.WalletId,
                                WalletName = wallet.WalletName,
                                LogoPath = wallet.LogoPath,
                                AccountNumber = ownerWallet.AccountNumber,
                                BeneficiaryName = ownerWallet.BeneficiaryName
                            })
                            .FirstOrDefaultAsync(cancellationToken);

                        if (refundDestination == null)
                        {
                            return OwnerAdvertisementOperationResultDto.Fail(
                                "محفظة الاسترداد المختارة غير موجودة أو غير مفعلة. اختر محفظة أخرى.");
                        }
                    }

                    advertisement.Status = OwnerAdvertisementStatuses.Cancelled;
                    advertisement.PausedAt = null;

                    if (needsRefundDestination && refundDestination != null)
                    {
                        // نحفظ وجهة الاسترداد كـ Snapshot عند طلب الإلغاء حتى لا تتغير
                        // العملية إذا عدّل المالك بيانات محفظته لاحقًا.
                        advertisement.RefundRequestedAt = now;
                        advertisement.RefundWalletId = refundDestination.WalletId;
                        advertisement.RefundAccountNumber = refundDestination.AccountNumber.Trim();
                        advertisement.RefundBeneficiaryName = refundDestination.BeneficiaryName.Trim();
                        advertisement.RefundAmount = null;
                        advertisement.RefundReferenceNumber = null;
                        advertisement.RefundProofImage = null;
                        advertisement.RefundedAt = null;
                        advertisement.RefundedByUserId = null;
                        advertisement.RefundNote = null;
                    }
                    else
                    {
                        advertisement.RefundRequestedAt = null;
                        advertisement.RefundWalletId = null;
                        advertisement.RefundAccountNumber = null;
                        advertisement.RefundBeneficiaryName = null;
                        advertisement.RefundAmount = null;
                        advertisement.RefundReferenceNumber = null;
                        advertisement.RefundProofImage = null;
                        advertisement.RefundedAt = null;
                        advertisement.RefundedByUserId = null;
                        advertisement.RefundNote = null;
                    }

                    if (isAdminPlace)
                    {
                        advertisement.CancellationReason = reason;
                        message = "تم إلغاء الإعلان. إعلانات منشآت الإدارة لا ترتبط بدفع أو استرداد مالي.";
                    }
                    else if (needsRefundDestination && advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Paid)
                    {
                        advertisement.CancellationReason = RefundPendingMarker + reason;
                        message = $"تم إلغاء الإعلان قبل بدء العرض. سيتم استرداد المبلغ إلى {refundDestination!.WalletName} ({refundDestination.AccountNumber}) بعد تنفيذ الإدارة للعملية.";
                    }
                    else if (needsRefundDestination && advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.PendingReview)
                    {
                        advertisement.CancellationReason = reason;
                        message = $"تم إلغاء الإعلان قبل بدء العرض. ستراجع الإدارة إثبات الدفع، وإذا تم اعتماده فسيتم الاسترداد إلى {refundDestination!.WalletName} ({refundDestination.AccountNumber}).";
                    }
                    else if (advertisement.StartImmediately == true &&
                             (advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Paid ||
                              advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.PendingReview))
                    {
                        advertisement.CancellationReason = reason;
                        message = "تم إلغاء الإعلان. لأنك اخترت البدء مباشرة بعد موافقة الإدارة، فالمبلغ غير قابل للاسترداد بعد تأكيد الدفع.";
                    }
                    else
                    {
                        advertisement.CancellationReason = reason;
                        message = "تم إلغاء الإعلان قبل بدء العرض. لا يوجد مبلغ مدفوع يحتاج إلى استرداد.";
                    }

                    // إذا اختار المالك وجهة استرداد، فهذه عملية تحتاج إجراء من الإدارة.
                    // نضيف الإشعار إلى نفس DbContext ليُحفظ مع الإلغاء والاسترداد بشكل ذري.
                    if (needsRefundDestination && refundDestination != null)
                    {
                        await _notificationService.QueueForAdminPermissionAsync(
                            AdminPermissionKeys.Advertisements.Review,
                            new CreateNotificationDto
                            {
                                NotificationTitle = "طلب استرداد مبلغ إعلان",
                                NotificationText =
                                    advertisement.PaymentStatus == OwnerAdvertisementPaymentStatuses.Paid
                                        ? $"ألغى مالك {loaded.Place!.PlaceName} الإعلان «{advertisement.AdvertisementTitle}» قبل بدء العرض وطلب استرداد المبلغ إلى {refundDestination.WalletName} ({refundDestination.AccountNumber})."
                                        : $"ألغى مالك {loaded.Place!.PlaceName} الإعلان «{advertisement.AdvertisementTitle}» قبل بدء العرض وحدد وجهة الاسترداد. راجع إثبات الدفع ثم نفّذ الاسترداد عند اعتماده.",
                                NotificationType = NotificationTypes.AdminAdvertisementActionRequired,
                                TargetUrl =
                                    $"/Admin/Advertisements/AdvertisementManagement?tab=ads&group=Closed&focusAdvertisement={advertisement.AdvertisementId}",
                                ReferenceType = NotificationReferenceTypes.Advertisement,
                                ReferenceId = advertisement.AdvertisementId
                            },
                            cancellationToken);
                    }
                    break;
            }

            advertisement.UpdatedAt = now;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrencyFailure();
            }

            return AdvertisementSuccess(message, advertisement);
        }

        private async Task<string?> ValidateSaveRequestAsync(
            OwnerAdvertisementSaveDto request,
            int ownerId,
            OwnedPlaceRow place,
            CancellationToken cancellationToken)
        {
            if (place.IsAvailable == false ||
                !(string.Equals(place.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(place.Status, "Approved", StringComparison.OrdinalIgnoreCase)))
            {
                return "المنشأة غير نشطة أو ما زالت قيد المراجعة.";
            }

            if (string.IsNullOrWhiteSpace(request.AdvertisementTitle) ||
                request.AdvertisementTitle.Trim().Length < 3 ||
                request.AdvertisementTitle.Trim().Length > 150)
            {
                return "عنوان الإعلان مطلوب ويكون بين 3 و150 حرفًا.";
            }

            if (string.IsNullOrWhiteSpace(request.ButtonText) || request.ButtonText.Trim().Length > 50)
            {
                return "نص زر الإعلان مطلوب ولا يتجاوز 50 حرفًا.";
            }

            var plan = await _context.AdvertisementPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AdvertisementPlanId == request.AdvertisementPlanId &&
                    item.IsActive == true,
                    cancellationToken);
            if (plan == null)
            {
                return "خيار التسعير غير موجود أو غير متاح حاليًا.";
            }

            if (plan.PlacementType != OwnerAdvertisementPlacementTypes.SearchTop)
            {
                return "الباقة غير مخصصة لمكان ظهور الإعلانات الحالي.";
            }

            if (!string.Equals(
                    plan.BillingModel,
                    OwnerAdvertisementBillingModels.Flat,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "هذه الباقة لا تتوافق مع نظام السعر اليومي الحالي.";
            }

            // في النظام الجديد لا يختار المالك CPC/CPM أو ميزانيات أو استهداف جمهور.
            request.TotalBudget = null;
            request.DailyBudget = null;
            request.TargetingMode = OwnerAdvertisementTargetingModes.All;
            request.CityIds = new List<int>();
            request.CategoryIds = new List<int>();

            if (request.DurationDays < 1)
            {
                return "مدة الإعلان لا يمكن أن تقل عن يوم واحد.";
            }

            if (request.DurationDays > 365)
            {
                return "لا يمكن أن تتجاوز مدة الإعلان سنة واحدة (365 يومًا).";
            }

            var occupiedGlobally = await _context.Advertisements
                .AsNoTracking()
                .CountAsync(item =>
                    item.AdvertisementId != request.AdvertisementId.GetValueOrDefault() &&
                    item.AdvertisementPlanId == plan.AdvertisementPlanId &&
                    (item.Status == OwnerAdvertisementStatuses.Active ||
                     item.Status == OwnerAdvertisementStatuses.Scheduled),
                    cancellationToken);
            if (occupiedGlobally >= plan.MaxActiveAdvertisements)
            {
                return "لا توجد مساحة متاحة حاليًا لخيار التسعير هذا. اختر خيارًا آخر أو حاول لاحقًا.";
            }

            if (!request.StartImmediately &&
                (!request.RequestedStartAt.HasValue || request.RequestedStartAt.Value <= DateTime.Now))
            {
                return "اختر موعد بداية مستقبليًا أو فعّل البدء فورًا.";
            }


            if (!IsDeviceTarget(request.DeviceTarget))
            {
                return "نوع الجهاز المستهدف غير صحيح.";
            }

            if (request.PromotionId.HasValue)
            {
                var promotionExists = await _context.Promotions
                    .AsNoTracking()
                    .AnyAsync(item =>
                        item.PromotionId == request.PromotionId.Value &&
                        item.OwnerId == ownerId &&
                        item.PlaceId == place.PlaceId &&
                        item.IsActive == true,
                        cancellationToken);
                if (!promotionExists)
                {
                    return "العرض المرتبط غير موجود أو غير نشط.";
                }
            }

            var billingModel = NormalizeBillingModel(plan.BillingModel);
            if (billingModel == null)
            {
                return "طريقة المحاسبة غير صحيحة.";
            }

            request.TotalBudget = null;
            request.DailyBudget = null;

            var quote = CalculatePlanQuote(plan, request);
            if (!quote.Success)
            {
                return quote.Message;
            }


            return null;
        }

        private async Task<string?> ValidateReadyForSubmissionAsync(
            Advertisement advertisement,
            CancellationToken cancellationToken)
        {
            var plan = await _context.AdvertisementPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AdvertisementPlanId == advertisement.AdvertisementPlanId &&
                    item.IsActive == true,
                    cancellationToken);
            if (plan == null)
            {
                return "خيار التسعير لم يعد متاحًا. اختر خيارًا آخر.";
            }

            var media = await _context.AdvertisementMedia
                .AsNoTracking()
                .Where(item => item.AdvertisementId == advertisement.AdvertisementId && item.IsActive == true)
                .Select(item => item.MediaType)
                .ToListAsync(cancellationToken);
            if (media.Count == 0 && string.IsNullOrWhiteSpace(advertisement.ImagePath))
            {
                return "اختر صورة الإعلان أو فيديو بديلًا عنها قبل المتابعة إلى الدفع.";
            }

            if (media.Count > 1)
            {
                return "لا يمكن الجمع بين صورة وفيديو في الإعلان نفسه.";
            }

            if (!plan.AllowVideo && media.Any(item => item == OwnerAdvertisementMediaTypes.Video))
            {
                return "الباقة المختارة لا تسمح بالفيديو.";
            }

            var activeCount = await _context.Advertisements
                .AsNoTracking()
                .CountAsync(item =>
                    item.AdvertisementId != advertisement.AdvertisementId &&
                    item.AdvertisementPlanId == advertisement.AdvertisementPlanId &&
                    (item.Status == OwnerAdvertisementStatuses.Active ||
                     item.Status == OwnerAdvertisementStatuses.Scheduled),
                    cancellationToken);
            if (activeCount >= plan.MaxActiveAdvertisements)
            {
                return "اكتملت المساحات المتاحة في خيار التسعير هذا حاليًا.";
            }

            if (advertisement.StartImmediately != true && !advertisement.RequestedStartAt.HasValue)
            {
                return "موعد بداية الإعلان غير محدد.";
            }

            var campaignDurationDays = CampaignDurationDays(
                advertisement.StartAt,
                advertisement.EndAt,
                advertisement.DurationDaysSnapshot);
            if (campaignDurationDays < 1 || campaignDurationDays > 365 ||
                !advertisement.StartAt.HasValue || !advertisement.EndAt.HasValue ||
                advertisement.EndAt.Value <= advertisement.StartAt.Value)
            {
                return "مدة الحملة غير مكتملة. افتح الإعلان وحدد مدة بين يوم و365 يومًا ثم احفظه.";
            }

            if (advertisement.BillingModelSnapshot != OwnerAdvertisementBillingModels.Flat &&
                (!advertisement.TotalBudget.HasValue || advertisement.TotalBudget.Value <= 0m))
            {
                return "ميزانية الإعلان غير مكتملة.";
            }

            if (advertisement.BillingModelSnapshot == OwnerAdvertisementBillingModels.Cpc &&
                (!advertisement.CostPerClickSnapshot.HasValue || advertisement.CostPerClickSnapshot.Value <= 0m))
            {
                return "تكلفة النقرة في خيار التسعير غير صحيحة.";
            }

            if (advertisement.BillingModelSnapshot == OwnerAdvertisementBillingModels.Cpm &&
                (!advertisement.CostPerThousandSnapshot.HasValue || advertisement.CostPerThousandSnapshot.Value <= 0m))
            {
                return "تكلفة ألف ظهور في خيار التسعير غير صحيحة.";
            }

            if (advertisement.BillingModelSnapshot != OwnerAdvertisementBillingModels.Flat &&
                (!advertisement.DailyBudget.HasValue || advertisement.DailyBudget.Value <= 0m ||
                 advertisement.DailyBudget.Value > advertisement.TotalBudget!.Value))
            {
                return "الميزانية اليومية غير مكتملة أو تتجاوز الميزانية الكلية.";
            }

            return null;
        }

        private static void ApplyDraft(
            Advertisement entity,
            OwnerAdvertisementSaveDto request,
            AdvertisementPlan plan,
            AdvertisementPriceQuoteDto priceQuote,
            int placeId)
        {
            entity.PlaceId = placeId;
            entity.AdvertisementPlanId = plan.AdvertisementPlanId;
            entity.AdvertisementTitle = request.AdvertisementTitle.Trim();
            entity.ShortDescription = NormalizeOptional(request.ShortDescription);
            entity.ButtonText = request.ButtonText.Trim();
            entity.PromotionId = request.PromotionId;
            ApplyPlanSnapshot(entity, plan, request.DurationDays, priceQuote.FinalAmount);
            entity.TotalBudget = NormalizeBudget(plan.BillingModel, request.TotalBudget);
            entity.DailyBudget = entity.TotalBudget.HasValue ? request.DailyBudget : null;
            entity.MaxImpressions = PositiveOrNull(priceQuote.EstimatedImpressions);
            entity.MaxClicks = PositiveOrNull(priceQuote.EstimatedClicks);
            // قيم تشغيلية داخلية وليست اختيارات للمالك.
            entity.FrequencyCapPerUserPerDay = 3;
            entity.TargetingMode = request.TargetingMode;
            entity.DeviceTarget = request.DeviceTarget;
            entity.DestinationType = OwnerAdvertisementDestinationTypes.Place;
            entity.DestinationPath = null;
            entity.TargetLatitude = null;
            entity.TargetLongitude = null;
            entity.TargetRadiusKm = null;
            entity.StartImmediately = request.StartImmediately;
            entity.RequestedStartAt = request.StartImmediately ? null : request.RequestedStartAt;
            SetPlannedCampaignRange(
                entity,
                EffectiveDurationDays(plan.BillingModel, request.DurationDays),
                request.StartImmediately,
                request.RequestedStartAt);
            entity.AutoRenew = false;
            entity.Currency = BaseCurrency;
        }

        private static void ApplyPlanSnapshot(
            Advertisement entity,
            AdvertisementPlan plan,
            int requestedDurationDays,
            decimal calculatedAmount)
        {
            var billingModel = NormalizeBillingModel(plan.BillingModel) ?? OwnerAdvertisementBillingModels.Flat;
            entity.PlacementTypeSnapshot = OwnerAdvertisementPlacementTypes.SearchTop;
            entity.DurationTypeSnapshot = "Daily";
            entity.DurationDaysSnapshot = Math.Clamp(requestedDurationDays, 1, 365);
            // PriceSnapshot في Flat = إجمالي الحملة وقت الشراء.
            entity.PriceSnapshot = SnapshotPrice(billingModel, plan.Price, calculatedAmount);
            entity.BillingModelSnapshot = OwnerAdvertisementBillingModels.Flat;
            entity.CostPerClickSnapshot = null;
            entity.CostPerThousandSnapshot = null;
            entity.PrioritySnapshot = plan.DisplayPriority;
            entity.Currency = BaseCurrency;
        }

        private AdvertisementPriceQuoteDto CalculatePlanQuote(
            AdvertisementPlan plan,
            OwnerAdvertisementSaveDto request)
        {
            var billingModel = NormalizeBillingModel(plan.BillingModel);
            if (billingModel == null)
            {
                return new AdvertisementPriceQuoteDto
                {
                    Success = false,
                    Message = "طريقة المحاسبة غير صحيحة."
                };
            }

            var unitPrice = billingModel switch
            {
                OwnerAdvertisementBillingModels.Cpc => plan.CostPerClick ?? 0m,
                OwnerAdvertisementBillingModels.Cpm => plan.CostPerThousandImpressions ?? 0m,
                _ => plan.Price
            };

            return _pricing.Calculate(new AdvertisementPriceQuoteRequestDto
            {
                BillingModel = billingModel,
                UnitPrice = unitPrice,
                DurationDays = EffectiveDurationDays(billingModel, request.DurationDays),
                TotalBudget = request.TotalBudget,
                DailyBudget = request.DailyBudget,
                MinimumTotalBudget = plan.MinimumTotalBudget
            });
        }

        private async Task ReplaceTargetsAsync(
            int advertisementId,
            OwnerAdvertisementSaveDto request,
            CancellationToken cancellationToken)
        {
            var current = await _context.AdvertisementTargets
                .Where(item => item.AdvertisementId == advertisementId)
                .ToListAsync(cancellationToken);
            if (current.Count > 0)
            {
                _context.AdvertisementTargets.RemoveRange(current);
            }

            if (request.TargetingMode != OwnerAdvertisementTargetingModes.Custom)
            {
                return;
            }

            var now = DateTime.Now;
            foreach (var cityId in request.CityIds.Distinct())
            {
                _context.AdvertisementTargets.Add(new AdvertisementTarget
                {
                    AdvertisementId = advertisementId,
                    CityId = cityId,
                    CreatedAt = now
                });
            }

            foreach (var categoryId in request.CategoryIds.Distinct())
            {
                _context.AdvertisementTargets.Add(new AdvertisementTarget
                {
                    AdvertisementId = advertisementId,
                    CategoryId = categoryId,
                    CreatedAt = now
                });
            }
        }

        private async Task<LoadedAdvertisement> LoadOwnedAdvertisementAsync(
            int advertisementId,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken)
        {
            EnsureOwner(ownerId);
            var place = await ResolveOwnedPlaceAsync(ownerId, requestedPlaceId, cancellationToken);
            if (place == null)
            {
                return new LoadedAdvertisement { Error = "اختر منشأة صحيحة من الهيدر أولًا." };
            }

            var advertisement = await _context.Advertisements
                .FirstOrDefaultAsync(item =>
                    item.AdvertisementId == advertisementId &&
                    item.OwnerId == ownerId &&
                    item.PlaceId == place.PlaceId,
                    cancellationToken);
            return advertisement == null
                ? new LoadedAdvertisement { Place = place, Error = "الإعلان غير موجود أو لا يخص المنشأة المحددة." }
                : new LoadedAdvertisement { Place = place, Advertisement = advertisement };
        }

        private async Task<OwnedPlaceRow?> ResolveOwnedPlaceAsync(
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken)
        {
            var query = _context.Places
                .AsNoTracking()
                .Where(item => item.OwnerId == ownerId)
                .Select(item => new OwnedPlaceRow
                {
                    PlaceId = item.PlaceId,
                    PlaceName = item.PlaceNameAndDesc,
                    PlaceImage = item.LogoImage ?? item.CoverImage,
                    Coordinates = item.Coordinates,
                    Status = item.Status,
                    IsAvailable = item.IsAvailable,
                    ExchangeRateRegion = item.City != null ? item.City.ExchangeRateRegion : null,
                    OwnerRole = _context.Users
                        .Where(user => user.UserId == item.OwnerId)
                        .Select(user => user.Role)
                        .FirstOrDefault()
                });

            if (requestedPlaceId.HasValue && requestedPlaceId.Value > 0)
            {
                var requested = await query.FirstOrDefaultAsync(
                    item => item.PlaceId == requestedPlaceId.Value,
                    cancellationToken);
                if (requested != null)
                {
                    return requested;
                }
            }

            return await query
                .OrderByDescending(item => item.Status == "Approved" || item.Status == "Active")
                .ThenBy(item => item.PlaceName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private IQueryable<AdvertisementRow> BuildOwnedAdvertisementQuery(int ownerId, int placeId)
        {
            return from advertisement in _context.Advertisements.AsNoTracking()
                   join plan in _context.AdvertisementPlans.AsNoTracking()
                       on advertisement.AdvertisementPlanId equals plan.AdvertisementPlanId
                   join place in _context.Places.AsNoTracking()
                       on advertisement.PlaceId equals place.PlaceId
                   where advertisement.OwnerId == ownerId && advertisement.PlaceId == placeId
                   select new AdvertisementRow
                   {
                       AdvertisementId = advertisement.AdvertisementId,
                       PlaceId = advertisement.PlaceId,
                       AdvertisementPlanId = advertisement.AdvertisementPlanId,
                       PromotionId = advertisement.PromotionId,
                       PlaceName = place.PlaceNameAndDesc,
                       PlanName = plan.PlanName,
                       DisplayWeight = advertisement.PrioritySnapshot > 0
                           ? advertisement.PrioritySnapshot
                           : plan.DisplayPriority,
                       AdvertisementTitle = advertisement.AdvertisementTitle,
                       ShortDescription = advertisement.ShortDescription,
                       ImagePath = advertisement.ImagePath,
                       ButtonText = advertisement.ButtonText,
                       DurationType = advertisement.DurationTypeSnapshot,
                       DurationDays = advertisement.DurationDaysSnapshot,
                       BillingModel = advertisement.BillingModelSnapshot,
                       PriceSnapshot = advertisement.PriceSnapshot,
                       CostPerClick = advertisement.CostPerClickSnapshot,
                       CostPerThousand = advertisement.CostPerThousandSnapshot,
                       TotalBudget = advertisement.TotalBudget,
                       DailyBudget = advertisement.DailyBudget,
                       SpentAmount = advertisement.SpentAmount,
                       Status = advertisement.Status,
                       PaymentStatus = advertisement.PaymentStatus,
                       RejectionReason = advertisement.RejectionReason,
                       PauseReason = advertisement.PauseReason,
                       CancellationReason = advertisement.CancellationReason,
                       StartImmediately = advertisement.StartImmediately == true,
                       RequestedStartAt = advertisement.RequestedStartAt,
                       StartAt = advertisement.StartAt,
                       EndAt = advertisement.EndAt,
                       TargetingMode = advertisement.TargetingMode,
                       DeviceTarget = advertisement.DeviceTarget,
                       ImpressionsCount = advertisement.ImpressionsCount,
                       UniqueImpressionsCount = advertisement.UniqueImpressionsCount,
                       ClicksCount = advertisement.ClicksCount,
                       BookingStartsCount = advertisement.BookingStartsCount,
                       ConversionsCount = advertisement.ConversionsCount,
                       PlatformWalletId = advertisement.PlatformWalletId,
                       TransferredAmount = advertisement.TransferredAmount,
                       ReferenceNumber = advertisement.ReferenceNumber,
                       TransferDate = advertisement.TransferDate,
                       ReceiptImage = advertisement.ReceiptImage,
                       RefundRequestedAt = advertisement.RefundRequestedAt,
                       RefundAmount = advertisement.RefundAmount,
                       RefundWalletId = advertisement.RefundWalletId,
                       RefundAccountNumber = advertisement.RefundAccountNumber,
                       RefundBeneficiaryName = advertisement.RefundBeneficiaryName,
                       RefundReferenceNumber = advertisement.RefundReferenceNumber,
                       RefundProofImage = advertisement.RefundProofImage,
                       RefundedAt = advertisement.RefundedAt,
                       RefundedByUserId = advertisement.RefundedByUserId,
                       RefundNote = advertisement.RefundNote,
                       SubmittedAt = advertisement.SubmittedAt,
                       CreatedAt = advertisement.CreatedAt,
                       UpdatedAt = advertisement.UpdatedAt,
                       RowVersionBytes = advertisement.RowVersion
                   };
        }

        private static IQueryable<AdvertisementRow> ApplyFilters(
            IQueryable<AdvertisementRow> query,
            OwnerAdvertisementFilterDto filter)
        {
            var search = NormalizeOptional(filter.Search);
            if (search != null)
            {
                query = query.Where(item =>
                    item.AdvertisementTitle.Contains(search) ||
                    item.PlanName.Contains(search));
            }

            query = (filter.Group ?? "All").Trim().ToLowerInvariant() switch
            {
                "needsaction" => query.Where(item =>
                    item.Status != OwnerAdvertisementStatuses.Active &&
                    item.Status != OwnerAdvertisementStatuses.Scheduled &&
                    item.Status != OwnerAdvertisementStatuses.Paused &&
                    item.Status != OwnerAdvertisementStatuses.Expired &&
                    item.Status != OwnerAdvertisementStatuses.Cancelled),
                "running" => query.Where(item =>
                    item.Status == OwnerAdvertisementStatuses.Active),
                "scheduled" => query.Where(item =>
                    item.Status == OwnerAdvertisementStatuses.Scheduled),
                "paused" => query.Where(item => item.Status == OwnerAdvertisementStatuses.Paused),
                "expired" => query.Where(item => item.Status == OwnerAdvertisementStatuses.Expired),
                "cancelled" => query.Where(item => item.Status == OwnerAdvertisementStatuses.Cancelled),
                _ => query
            };

            if (IsPaymentStatus(filter.PaymentStatus))
            {
                query = query.Where(item => item.PaymentStatus == filter.PaymentStatus);
            }

            return query;
        }

        private async Task<OwnerAdvertisementListItemDto> ToListItemAsync(
            AdvertisementRow row,
            string? exchangeRateRegion,
            CancellationToken cancellationToken)
        {
            var primary = await _context.AdvertisementMedia
                .AsNoTracking()
                .Where(item => item.AdvertisementId == row.AdvertisementId && item.IsActive == true)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.DisplayOrder)
                .Select(item => new { item.FilePath, item.MediaType })
                .FirstOrDefaultAsync(cancellationToken);

            var expected = ExpectedPayment(row.BillingModel, row.PriceSnapshot, row.TotalBudget);
            var timeAmounts = CalculateTimeBasedAmounts(row);
            var actualMetrics = await GetActualInteractionMetricsAsync(row.AdvertisementId, cancellationToken);
            var capabilities = Capabilities(row.Status, row.PaymentStatus, row.StartAt, row.EndAt, row.RefundRequestedAt, row.RefundedAt);
            return new OwnerAdvertisementListItemDto
            {
                AdvertisementId = row.AdvertisementId,
                AdvertisementPlanId = row.AdvertisementPlanId,
                AdvertisementTitle = row.AdvertisementTitle,
                ShortDescription = row.ShortDescription,
                PrimaryMediaPath = primary?.FilePath ?? row.ImagePath,
                PrimaryMediaType = primary?.MediaType ?? (!string.IsNullOrWhiteSpace(row.ImagePath) ? OwnerAdvertisementMediaTypes.Image : null),
                PlanName = row.PlanName,
                DisplayWeight = row.DisplayWeight,
                BillingModel = row.BillingModel,
                BillingModelLabel = BillingModelLabel(row.BillingModel),
                Status = row.Status,
                StatusLabel = StatusLabel(row.Status),
                PaymentStatus = row.PaymentStatus,
                PaymentStatusLabel = PaymentStatusLabel(row.PaymentStatus),
                PriceSnapshot = row.PriceSnapshot,
                TotalBudget = row.TotalBudget,
                SpentAmount = timeAmounts.Spent,
                RemainingBudget = timeAmounts.Remaining,
                PriceText = await FormatMoneyAsync(expected, exchangeRateRegion),
                SpentAmountText = await FormatMoneyAsync(timeAmounts.Spent, exchangeRateRegion),
                RemainingBudgetText = await FormatMoneyAsync(timeAmounts.Remaining, exchangeRateRegion),
                ImpressionsCount = actualMetrics.TotalViews,
                UniqueImpressionsCount = actualMetrics.UniqueViews,
                ClicksCount = actualMetrics.TotalClicks,
                BookingStartsCount = actualMetrics.BookingStarts,
                ConversionsCount = actualMetrics.CompletedBookings,
                ClickThroughRate = Percentage(actualMetrics.TotalClicks, actualMetrics.TotalViews),
                SubmittedAt = row.SubmittedAt,
                StartAt = row.StartAt,
                EndAt = row.EndAt,
                CreatedAt = row.CreatedAt,
                RejectionReason = row.RejectionReason,
                RowVersion = ToRowVersion(row.RowVersionBytes),
                CanEdit = capabilities.CanEdit,
                CanSubmit = capabilities.CanSubmit,
                CanPay = capabilities.CanPay,
                CanPause = capabilities.CanPause,
                CanResume = capabilities.CanResume,
                CanCancel = capabilities.CanCancel,
                CanRenew = capabilities.CanRenew,
                CanRestore = capabilities.CanRestore,
                CardMode = CardMode(row.Status, row.PaymentStatus),
                PrimaryAction = PrimaryAction(row.Status, row.PaymentStatus, capabilities),
                PrimaryActionLabel = PrimaryActionLabel(row.Status, row.PaymentStatus, capabilities),
                AttentionText = AttentionText(
                    row.Status,
                    row.PaymentStatus,
                    row.RejectionReason,
                    row.EndAt,
                    row.TotalBudget,
                    row.SpentAmount,
                    row.RefundRequestedAt,
                    row.RefundedAt),
                RefundRequestedAt = row.RefundRequestedAt,
                RefundedAt = row.RefundedAt,
                RefundStatusLabel = RefundStatusLabel(row.Status, row.PaymentStatus, row.RefundRequestedAt, row.RefundedAt)
            };
        }

        private async Task<List<OwnerAdvertisementPlanCardDto>> GetPlanCardsAsync(
            int ownerId,
            int placeId,
            string? exchangeRateRegion,
            bool onlyAvailable,
            int? take,
            CancellationToken cancellationToken)
        {
            var plans = await _context.AdvertisementPlans
                .AsNoTracking()
                .Where(item =>
                    item.IsActive == true &&
                    item.PlacementType == OwnerAdvertisementPlacementTypes.SearchTop &&
                    item.BillingModel == OwnerAdvertisementBillingModels.Flat)
                .OrderBy(item => item.Price)
                .ThenBy(item => item.PlanName)
                .ToListAsync(cancellationToken);

            var activeCounts = await _context.Advertisements
                .AsNoTracking()
                .Where(item =>
                    (item.Status == OwnerAdvertisementStatuses.Active ||
                     item.Status == OwnerAdvertisementStatuses.Scheduled))
                .GroupBy(item => item.AdvertisementPlanId)
                .Select(group => new { PlanId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.PlanId, item => item.Count, cancellationToken);

            var cards = new List<OwnerAdvertisementPlanCardDto>();
            foreach (var plan in plans)
            {
                var activeCount = activeCounts.TryGetValue(plan.AdvertisementPlanId, out var count) ? count : 0;
                var available = activeCount < plan.MaxActiveAdvertisements;
                if (onlyAvailable && !available)
                {
                    continue;
                }

                var billingModel = NormalizeBillingModel(plan.BillingModel) ?? OwnerAdvertisementBillingModels.Flat;
                decimal? unitCost = billingModel switch
                {
                    OwnerAdvertisementBillingModels.Cpc => plan.CostPerClick,
                    OwnerAdvertisementBillingModels.Cpm => plan.CostPerThousandImpressions,
                    _ => null
                };
                var minimumCharge = MinimumBillableCharge(billingModel, unitCost);
                decimal? effectiveDailyBudget = billingModel == OwnerAdvertisementBillingModels.Flat
                    ? null
                    : Math.Max(plan.DefaultDailyBudget ?? minimumCharge, minimumCharge);
                var effectiveMinimumBudget = billingModel == OwnerAdvertisementBillingModels.Flat
                    ? (decimal?)null
                    : Math.Max(plan.MinimumTotalBudget ?? minimumCharge, effectiveDailyBudget ?? minimumCharge);
                var primaryAmount = billingModel == OwnerAdvertisementBillingModels.Flat
                    ? plan.Price
                    : effectiveMinimumBudget ?? 0m;

                cards.Add(new OwnerAdvertisementPlanCardDto
                {
                    AdvertisementPlanId = plan.AdvertisementPlanId,
                    PlanName = plan.PlanName,
                    BillingModel = billingModel,
                    BillingModelLabel = BillingModelLabel(billingModel),
                    Price = plan.Price,
                    DisplayWeight = plan.DisplayPriority,
                    CostPerClick = plan.CostPerClick,
                    CostPerThousandImpressions = plan.CostPerThousandImpressions,
                    MinimumTotalBudget = effectiveMinimumBudget,
                    DefaultDailyBudget = effectiveDailyBudget,
                    PriceText = await FormatMoneyAsync(primaryAmount, exchangeRateRegion),
                    UnitCostText = unitCost.HasValue ? await FormatMoneyAsync(unitCost.Value, exchangeRateRegion) : null,
                    MinimumBudgetText = effectiveMinimumBudget.HasValue
                        ? await FormatMoneyAsync(effectiveMinimumBudget.Value, exchangeRateRegion)
                        : null,
                    Description = plan.Description,
                    MaxActiveAdvertisements = plan.MaxActiveAdvertisements,
                    MaxMediaItems = plan.MaxMediaItems,
                    AllowVideo = plan.AllowVideo,
                    IsAvailable = available,
                    ActiveAdvertisementsCount = activeCount,
                    AvailabilityMessage = available
                        ? "متاحة الآن"
                        : "وصل خيار التسعير إلى الحد الأقصى للإعلانات النشطة"
                });
            }

            return take.HasValue ? cards.Take(take.Value).ToList() : cards;
        }

        private async Task<List<OwnerAdvertisementWalletDto>> GetPlatformWalletsAsync(
            CancellationToken cancellationToken)
        {
            return await (
                from platformWallet in _context.PlatformWallets.AsNoTracking()
                join wallet in _context.SupportedWallets.AsNoTracking()
                    on platformWallet.WalletId equals wallet.WalletId
                where platformWallet.IsActive == true && wallet.IsActive == true
                orderby platformWallet.IsPrimary descending, wallet.WalletName
                select new OwnerAdvertisementWalletDto
                {
                    PlatformWalletId = platformWallet.PlatformWalletId,
                    WalletName = wallet.WalletName,
                    LogoPath = wallet.LogoPath,
                    AccountNumber = platformWallet.AccountNumber,
                    BeneficiaryName = platformWallet.BeneficiaryName,
                    Instructions = platformWallet.Instructions,
                    IsPrimary = platformWallet.IsPrimary == true
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<OwnerAdvertisementRefundWalletDto>> GetOwnerRefundWalletsAsync(
            int ownerId,
            CancellationToken cancellationToken)
        {
            return await (
                from ownerWallet in _context.OwnerWallets.AsNoTracking()
                join wallet in _context.SupportedWallets.AsNoTracking()
                    on ownerWallet.WalletId equals wallet.WalletId
                where ownerWallet.OwnerId == ownerId &&
                      ownerWallet.IsActive == true &&
                      wallet.IsActive == true
                orderby wallet.WalletName, ownerWallet.AccountNumber
                select new OwnerAdvertisementRefundWalletDto
                {
                    OwnerWalletId = ownerWallet.OwnerWalletId,
                    WalletId = ownerWallet.WalletId,
                    WalletName = wallet.WalletName,
                    LogoPath = wallet.LogoPath,
                    AccountNumber = ownerWallet.AccountNumber,
                    BeneficiaryName = ownerWallet.BeneficiaryName
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<OwnerAdvertisementMediaDto>> GetMediaAsync(
            int advertisementId,
            CancellationToken cancellationToken)
        {
            return await _context.AdvertisementMedia
                .AsNoTracking()
                .Where(item => item.AdvertisementId == advertisementId)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.AdvertisementMediaId)
                .Select(item => new OwnerAdvertisementMediaDto
                {
                    AdvertisementMediaId = item.AdvertisementMediaId,
                    MediaType = item.MediaType,
                    FilePath = item.FilePath,
                    ThumbnailPath = item.ThumbnailPath,
                    AltText = item.AltText,
                    DisplayOrder = item.DisplayOrder,
                    IsPrimary = item.IsPrimary,
                    IsActive = item.IsActive == true
                })
                .ToListAsync(cancellationToken);
        }

        private async Task RefreshLifecycleAsync(
            int ownerId,
            int placeId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var rows = await _context.Advertisements
                .Where(item =>
                    item.OwnerId == ownerId &&
                    item.PlaceId == placeId &&
                    (item.Status == OwnerAdvertisementStatuses.Scheduled ||
                     item.Status == OwnerAdvertisementStatuses.Active))
                .ToListAsync(cancellationToken);

            var changed = false;
            foreach (var item in rows)
            {
                // إذا اختار المالك بداية مستقبلية، تبقى RequestedStartAt هي المرجع الحقيقي.
                // قد تقوم عملية اعتماد الإدارة القديمة بتعيين StartAt = وقت الاعتماد؛ نصحح ذلك هنا
                // ونُبقي الإعلان Scheduled إلى أن يصل الموعد الذي اختاره المالك.
                if (item.StartImmediately != true &&
                    item.RequestedStartAt.HasValue &&
                    item.RequestedStartAt.Value > now &&
                    item.PaymentStatus == OwnerAdvertisementPaymentStatuses.Paid)
                {
                    var requestedStart = item.RequestedStartAt.Value;
                    var requestedEnd = requestedStart.AddDays(Math.Clamp(item.DurationDaysSnapshot, 1, 365));

                    if (item.StartAt != requestedStart ||
                        item.EndAt != requestedEnd ||
                        item.Status != OwnerAdvertisementStatuses.Scheduled)
                    {
                        item.StartAt = requestedStart;
                        item.EndAt = requestedEnd;
                        item.Status = OwnerAdvertisementStatuses.Scheduled;
                        item.UpdatedAt = now;
                        changed = true;
                    }
                }

                if (item.Status == OwnerAdvertisementStatuses.Scheduled &&
                    item.StartAt.HasValue && item.StartAt.Value <= now)
                {
                    item.Status = OwnerAdvertisementStatuses.Active;
                    item.UpdatedAt = now;
                    changed = true;
                }

                var expiredByDate = item.EndAt.HasValue && item.EndAt.Value <= now;
                var expiredByBudget = item.TotalBudget.HasValue && item.SpentAmount >= item.TotalBudget.Value;
                if (item.Status == OwnerAdvertisementStatuses.Active && (expiredByDate || expiredByBudget))
                {
                    item.Status = OwnerAdvertisementStatuses.Expired;
                    item.UpdatedAt = now;
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private bool TryApplyRowVersion(Advertisement advertisement, string? rowVersion)
        {
            if (string.IsNullOrWhiteSpace(rowVersion))
            {
                return false;
            }

            try
            {
                var bytes = Convert.FromBase64String(rowVersion);
                if (bytes.Length == 0)
                {
                    return false;
                }

                _context.Entry(advertisement)
                    .Property(item => item.RowVersion)
                    .OriginalValue = bytes;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void ClearPayment(Advertisement advertisement)
        {
            advertisement.PlatformWalletId = null;
            advertisement.TransferredAmount = null;
            advertisement.ReferenceNumber = null;
            advertisement.TransferDate = null;
            advertisement.ReceiptImage = null;
            advertisement.PaymentStatus = OwnerAdvertisementPaymentStatuses.Unpaid;
        }

        private static bool HasCompletePayment(Advertisement advertisement, decimal expectedPayment)
        {
            return advertisement.PlatformWalletId.HasValue &&
                   advertisement.TransferredAmount.HasValue &&
                   advertisement.TransferredAmount.Value >= expectedPayment &&
                   advertisement.TransferDate.HasValue &&
                   !string.IsNullOrWhiteSpace(advertisement.ReceiptImage);
        }

        private static decimal ExpectedPayment(Advertisement advertisement)
        {
            return ExpectedPayment(
                advertisement.BillingModelSnapshot,
                advertisement.PriceSnapshot,
                advertisement.TotalBudget);
        }

        private static decimal ExpectedPayment(string? billingModel, decimal price, decimal? totalBudget)
        {
            return NormalizeBillingModel(billingModel) == OwnerAdvertisementBillingModels.Flat
                ? Math.Max(0m, price)
                : Math.Max(0m, totalBudget ?? 0m);
        }

        private static OwnerAdvertisementPriceQuoteDto PriceQuoteFailure(string message)
        {
            return new OwnerAdvertisementPriceQuoteDto
            {
                Success = false,
                Message = message
            };
        }

        private async Task<string> FormatMoneyAsync(decimal amount, string? exchangeRateRegion)
        {
            return await _currencyDisplayService.FormatByRegionAsync(amount, exchangeRateRegion);
        }

        private static decimal? NormalizeBudget(string? billingModel, decimal? totalBudget)
        {
            return NormalizeBillingModel(billingModel) == OwnerAdvertisementBillingModels.Flat
                ? null
                : totalBudget;
        }

        private static int EffectiveDurationDays(string? billingModel, int requestedDurationDays)
        {
            return Math.Clamp(requestedDurationDays, 1, 365);
        }

        private static int CampaignDurationDays(DateTime? startAt, DateTime? endAt, int snapshotFallback)
        {
            if (startAt.HasValue && endAt.HasValue && endAt.Value > startAt.Value)
            {
                var days = (int)Math.Ceiling((endAt.Value - startAt.Value).TotalDays - 0.000001d);
                return Math.Clamp(days, 1, 365);
            }

            return Math.Clamp(snapshotFallback, 1, 365);
        }

        private static string CampaignDurationLabel(DateTime? startAt, DateTime? endAt, int snapshotFallback)
        {
            var days = CampaignDurationDays(startAt, endAt, snapshotFallback);
            return days == 1 ? "يوم واحد" : days == 2 ? "يومان" : $"{days} أيام";
        }

        private static void SetPlannedCampaignRange(
            Advertisement advertisement,
            int durationDays,
            bool startImmediately,
            DateTime? requestedStartAt)
        {
            var safeDuration = Math.Clamp(durationDays, 1, 365);
            var plannedStart = startImmediately
                ? DateTime.Now
                : requestedStartAt ?? DateTime.Now;
            advertisement.StartAt = plannedStart;
            advertisement.EndAt = plannedStart.AddDays(safeDuration);
        }

        private static decimal MinimumBillableCharge(string? billingModel, decimal? unitCost)
        {
            var cost = Math.Max(0m, unitCost ?? 0m);
            return NormalizeBillingModel(billingModel) switch
            {
                OwnerAdvertisementBillingModels.Cpc => Math.Max(0.01m, cost),
                OwnerAdvertisementBillingModels.Cpm => Math.Max(
                    0.01m,
                    decimal.Round(cost / 1000m, 2, MidpointRounding.AwayFromZero)),
                _ => 0m
            };
        }

        private static long? PositiveOrNull(long? value)
        {
            return value.HasValue && value.Value > 0 ? value : null;
        }

        private static string DatabaseSaveMessage(DbUpdateException exception)
        {
            var details = string.Join(" ",
                exception.Message,
                exception.InnerException?.Message,
                exception.InnerException?.InnerException?.Message);

            if (details.Contains("CK_Advertisements_CompleteBudget", StringComparison.OrdinalIgnoreCase))
                return "تعذر حفظ الميزانية لأن نتيجتها أقل من نقرة أو ظهور واحد. ارفع الميزانية أو اختر باقة أخرى.";

            if (details.Contains("CK_Advertisements_Duration", StringComparison.OrdinalIgnoreCase))
                return "تعذر حفظ مدة الحملة. حدد مدة بين يوم و365 يومًا ثم حاول مجددًا.";

            if (details.Contains("CK_Advertisements_DateRange", StringComparison.OrdinalIgnoreCase))
                return "تاريخ نهاية الإعلان يجب أن يكون بعد تاريخ البداية.";

            if (details.Contains("FK_Advertisements_Plans", StringComparison.OrdinalIgnoreCase))
                return "خيار التسعير لم يعد متاحًا. اختر خيارًا آخر.";

            return "تعذر حفظ المسودة بسبب بيانات غير متوافقة. راجع المدة والميزانية ثم حاول مرة أخرى.";
        }

        private static decimal SnapshotPrice(string? billingModel, decimal planPrice, decimal calculatedAmount)
        {
            return NormalizeBillingModel(billingModel) == OwnerAdvertisementBillingModels.Flat
                ? calculatedAmount
                : planPrice;
        }


        private static string? NormalizeBillingModel(string? value)
        {
            return value?.Trim().ToUpperInvariant() switch
            {
                "FLAT" => OwnerAdvertisementBillingModels.Flat,
                "CPC" => OwnerAdvertisementBillingModels.Cpc,
                "CPM" => OwnerAdvertisementBillingModels.Cpm,
                _ => null
            };
        }

        private static string? NormalizeMediaType(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "image" => OwnerAdvertisementMediaTypes.Image,
                "video" => OwnerAdvertisementMediaTypes.Video,
                _ => null
            };
        }

        private static bool IsOwnedUploadPath(
            string path,
            int ownerId,
            int advertisementId,
            string section)
        {
            var expectedPrefix = $"/uploads/advertisements/{ownerId}/{advertisementId}/{section}/";
            return path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) &&
                   !path.Contains("..", StringComparison.Ordinal);
        }

        private static bool IsDeviceTarget(string? value)
        {
            return value == OwnerAdvertisementDeviceTargets.All ||
                   value == OwnerAdvertisementDeviceTargets.Desktop ||
                   value == OwnerAdvertisementDeviceTargets.Mobile;
        }


        private static bool IsPaymentStatus(string? value)
        {
            return value == OwnerAdvertisementPaymentStatuses.Unpaid ||
                   value == OwnerAdvertisementPaymentStatuses.PendingReview ||
                   value == OwnerAdvertisementPaymentStatuses.Paid ||
                   value == OwnerAdvertisementPaymentStatuses.Rejected ||
                   value == OwnerAdvertisementPaymentStatuses.Refunded;
        }

        private static bool CanEdit(string status, DateTime? startAt = null)
        {
            // بعد بدء الحملة لا نسمح بتعديل المحتوى أو التسعير.
            // الإعلان المجدول يمكن تعديله فقط قبل وصول موعد بدايته،
            // أما Pending فيبقى ثابتاً أثناء مراجعة الإدارة.
            if (status == OwnerAdvertisementStatuses.Active ||
                status == OwnerAdvertisementStatuses.Paused ||
                status == OwnerAdvertisementStatuses.Pending ||
                status == OwnerAdvertisementStatuses.Cancelled ||
                status == OwnerAdvertisementStatuses.Expired)
            {
                return false;
            }

            if (status == OwnerAdvertisementStatuses.Scheduled)
            {
                return !startAt.HasValue || startAt.Value > DateTime.Now;
            }

            return true;
        }

        private static bool IsContentOnlyEditMode(string? editMode)
        {
            return string.Equals(
                editMode?.Trim(),
                "ContentOnly",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanSubmit(string status)
        {
            // SaveDraft moves an edited advertisement back to Draft.
            // Submit is intentionally restricted to editable/review states.
            return status == OwnerAdvertisementStatuses.Draft ||
                   status == OwnerAdvertisementStatuses.AwaitingPayment ||
                   status == OwnerAdvertisementStatuses.Rejected;
        }

        private static AdvertisementCapabilities Capabilities(
            string status,
            string paymentStatus,
            DateTime? startAt,
            DateTime? endAt,
            DateTime? refundRequestedAt,
            DateTime? refundedAt)
        {
            _ = endAt; // يبقى الوسيط للحفاظ على التوافق مع الاستدعاءات الحالية.
            var now = DateTime.Now;
            var campaignAlreadyStarted =
                status == OwnerAdvertisementStatuses.Active ||
                status == OwnerAdvertisementStatuses.Paused ||
                (status == OwnerAdvertisementStatuses.Scheduled && startAt.HasValue && startAt.Value <= now);

            var refundFlowStarted =
                refundRequestedAt.HasValue ||
                refundedAt.HasValue ||
                paymentStatus == OwnerAdvertisementPaymentStatuses.Refunded;

            return new AdvertisementCapabilities
            {
                CanEdit = CanEdit(status, startAt),
                CanSubmit = CanSubmit(status),
                CanPay = paymentStatus != OwnerAdvertisementPaymentStatuses.Paid &&
                         (status == OwnerAdvertisementStatuses.AwaitingPayment ||
                          paymentStatus == OwnerAdvertisementPaymentStatuses.Rejected),
                CanPause = status == OwnerAdvertisementStatuses.Active,
                CanResume = status == OwnerAdvertisementStatuses.Paused,
                // لا نسمح باستعادة إعلان بدأ مسار استرداده؛ هذا يمنع إعادة تشغيل حملة
                // بينما الإدارة قد تكون بصدد إعادة المبلغ.
                CanRestore = status == OwnerAdvertisementStatuses.Cancelled && !refundFlowStarted,
                CanCancel = !campaignAlreadyStarted &&
                            status != OwnerAdvertisementStatuses.Cancelled &&
                            status != OwnerAdvertisementStatuses.Expired,
                CanRenew = status == OwnerAdvertisementStatuses.Expired
            };
        }

        private static string CardMode(string status, string paymentStatus)
        {
            if (paymentStatus == OwnerAdvertisementPaymentStatuses.Rejected) return "payment-rejected";
            return status switch
            {
                OwnerAdvertisementStatuses.Draft => "draft",
                OwnerAdvertisementStatuses.AwaitingPayment => "payment",
                OwnerAdvertisementStatuses.Pending => "review",
                OwnerAdvertisementStatuses.Active => "active",
                OwnerAdvertisementStatuses.Scheduled => "scheduled",
                OwnerAdvertisementStatuses.Paused => "paused",
                OwnerAdvertisementStatuses.Expired => "expired",
                OwnerAdvertisementStatuses.Rejected => "changes",
                OwnerAdvertisementStatuses.Cancelled => "cancelled",
                _ => "waiting"
            };
        }

        private static string PrimaryAction(
            string status,
            string paymentStatus,
            AdvertisementCapabilities capabilities)
        {
            if (paymentStatus == OwnerAdvertisementPaymentStatuses.Rejected || capabilities.CanPay) return "payment";
            if (capabilities.CanEdit) return "edit";
            if (capabilities.CanResume) return "resume";
            if (capabilities.CanRestore) return "restore";
            if (capabilities.CanRenew) return "renew";
            return "details";
        }

        private static string PrimaryActionLabel(
            string status,
            string paymentStatus,
            AdvertisementCapabilities capabilities)
        {
            if (paymentStatus == OwnerAdvertisementPaymentStatuses.Rejected) return "رفع إثبات جديد";
            if (capabilities.CanPay) return "رفع إثبات الدفع";
            if (status == OwnerAdvertisementStatuses.Draft) return "متابعة الإنشاء";
            if (status == OwnerAdvertisementStatuses.Rejected && capabilities.CanEdit) return "تنفيذ التعديلات";
            if (capabilities.CanResume) return "استئناف الحملة";
            if (capabilities.CanRestore) return "استعادة الإعلان";
            if (capabilities.CanRenew) return "تجديد الإعلان";
            return "عرض التفاصيل";
        }

        private static string? RefundStatusLabel(
            string status,
            string paymentStatus,
            DateTime? refundRequestedAt,
            DateTime? refundedAt)
        {
            if (paymentStatus == OwnerAdvertisementPaymentStatuses.Refunded || refundedAt.HasValue)
                return "تم الاسترداد";
            if (status == OwnerAdvertisementStatuses.Cancelled && refundRequestedAt.HasValue)
                return "بانتظار الاسترداد";
            return null;
        }

        private static string? AttentionText(
            string status,
            string paymentStatus,
            string? rejectionReason,
            DateTime? endAt,
            decimal? totalBudget,
            decimal spentAmount,
            DateTime? refundRequestedAt,
            DateTime? refundedAt)
        {
            if (paymentStatus == OwnerAdvertisementPaymentStatuses.Refunded || refundedAt.HasValue)
                return "تم استرداد مبلغ الإعلان. افتح التفاصيل لمراجعة مرجع وإثبات التحويل.";
            if (status == OwnerAdvertisementStatuses.Cancelled && refundRequestedAt.HasValue)
                return "الإعلان ملغي ومبلغ الاسترداد بانتظار تنفيذ الإدارة.";
            if (paymentStatus == OwnerAdvertisementPaymentStatuses.Rejected)
                return NormalizeOptional(rejectionReason) ?? "إثبات الدفع مرفوض ويحتاج إلى إعادة الرفع.";
            if (status == OwnerAdvertisementStatuses.Rejected)
                return NormalizeOptional(rejectionReason) ?? "طلبت الإدارة تعديل بيانات الإعلان.";
            if (status == OwnerAdvertisementStatuses.AwaitingPayment)
                return "أكمل بيانات التحويل وارفع إثبات الدفع.";
            if (status == OwnerAdvertisementStatuses.Draft)
                return "المسودة لم تُرسل للمراجعة بعد.";
            if (status == OwnerAdvertisementStatuses.Paused)
                return "الحملة متوقفة مؤقتًا.";
            if (status == OwnerAdvertisementStatuses.Active && endAt.HasValue && endAt.Value <= DateTime.Now.AddDays(3))
                return "تنتهي الحملة خلال أقل من ثلاثة أيام.";
            if (status == OwnerAdvertisementStatuses.Active && totalBudget.HasValue && totalBudget.Value > 0m && spentAmount >= totalBudget.Value * .9m)
                return "استهلكت الحملة 90% أو أكثر من ميزانيتها.";
            return null;
        }

        private static void ArchiveRenewalStatistics(Advertisement advertisement)
        {
            List<RenewalArchiveRow> history;
            try
            {
                history = string.IsNullOrWhiteSpace(advertisement.ArchivedDailyStatisticsJson)
                    ? new List<RenewalArchiveRow>()
                    : JsonSerializer.Deserialize<List<RenewalArchiveRow>>(
                        advertisement.ArchivedDailyStatisticsJson) ?? new List<RenewalArchiveRow>();
            }
            catch (JsonException)
            {
                history = new List<RenewalArchiveRow>();
            }

            history.Add(new RenewalArchiveRow
            {
                ArchivedAt = DateTime.Now,
                RenewalNumber = advertisement.RenewalCount,
                StartAt = advertisement.StartAt,
                EndAt = advertisement.EndAt,
                Impressions = advertisement.ImpressionsCount,
                UniqueImpressions = advertisement.UniqueImpressionsCount,
                Clicks = advertisement.ClicksCount,
                BookingStarts = advertisement.BookingStartsCount,
                Conversions = advertisement.ConversionsCount,
                SpentAmount = advertisement.SpentAmount
            });
            advertisement.ArchivedDailyStatisticsJson = JsonSerializer.Serialize(history);
        }

        private static OwnerAdvertisementOperationResultDto AdvertisementSuccess(
            string message,
            Advertisement advertisement)
        {
            return OwnerAdvertisementOperationResultDto.Ok(
                message,
                advertisement.AdvertisementId,
                advertisement.Status,
                advertisement.PaymentStatus,
                ToRowVersion(advertisement.RowVersion));
        }

        private static OwnerAdvertisementOperationResultDto ConcurrencyFailure()
        {
            return OwnerAdvertisementOperationResultDto.Fail(
                "تم تعديل البيانات من جلسة أخرى. حدّث الصفحة وحاول مجددًا.");
        }

        private static void EnsureOwner(int ownerId)
        {
            if (ownerId <= 0)
            {
                throw new UnauthorizedAccessException("تعذر تحديد حساب المالك.");
            }
        }

        private static void NormalizePaging(OwnerAdvertisementFilterDto filter)
        {
            filter.Page = Math.Max(1, filter.Page);
            filter.PageSize = Math.Clamp(
                filter.PageSize <= 0 ? DefaultPageSize : filter.PageSize,
                1,
                MaximumPageSize);
        }

        private static int CalculateTotalPages(int totalItems, int pageSize)
        {
            return totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        }

        private static int ClampPage(int page, int totalPages)
        {
            return Math.Clamp(page, 1, Math.Max(1, totalPages));
        }

        private static decimal Percentage(long numerator, long denominator)
        {
            return denominator <= 0 || numerator <= 0
                ? 0m
                : Math.Round((decimal)numerator * 100m / denominator, 2);
        }

        private Task<ActualInteractionMetrics> GetActualInteractionMetricsAsync(
            int advertisementId,
            CancellationToken cancellationToken)
        {
            return GetActualInteractionMetricsAsync(
                new[] { advertisementId },
                cancellationToken);
        }

        private async Task<ActualInteractionMetrics> GetActualInteractionMetricsAsync(
            IReadOnlyCollection<int> advertisementIds,
            CancellationToken cancellationToken)
        {
            if (advertisementIds == null || advertisementIds.Count == 0)
            {
                return new ActualInteractionMetrics();
            }

            var events = await _context.AdvertisementEvents
                .AsNoTracking()
                .Where(evt =>
                    advertisementIds.Contains(evt.AdvertisementId) &&
                    (evt.EventType == "Impression" ||
                     evt.EventType == "Click" ||
                     evt.EventType == "BookingStarted" ||
                     evt.EventType == "BookingCompleted"))
                .Select(evt => new
                {
                    evt.EventType,
                    evt.UserId,
                    evt.VisitorHash,
                    evt.SessionKey,
                    evt.BookingId
                })
                .ToListAsync(cancellationToken);

            string IdentityKey(int? userId, string? visitorHash, string? sessionKey)
            {
                if (userId.HasValue && userId.Value > 0)
                    return $"U:{userId.Value}";

                if (!string.IsNullOrWhiteSpace(visitorHash))
                    return $"V:{visitorHash}";

                if (!string.IsNullOrWhiteSpace(sessionKey))
                    return $"S:{sessionKey}";

                return string.Empty;
            }

            var totalViews = events.LongCount(evt => evt.EventType == "Impression");
            var totalClicks = events.LongCount(evt => evt.EventType == "Click");
            var bookingStarts = events.LongCount(evt => evt.EventType == "BookingStarted");

            var uniqueViews = events
                .Where(evt => evt.EventType == "Impression")
                .Select(evt => IdentityKey(evt.UserId, evt.VisitorHash, evt.SessionKey))
                .Where(key => key.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .LongCount();

            var uniqueClickers = events
                .Where(evt => evt.EventType == "Click")
                .Select(evt => IdentityKey(evt.UserId, evt.VisitorHash, evt.SessionKey))
                .Where(key => key.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .LongCount();

            var completedBookings = events
                .Where(evt => evt.EventType == "BookingCompleted")
                .Select(evt => evt.BookingId)
                .Where(bookingId => bookingId.HasValue && bookingId.Value > 0)
                .Select(bookingId => bookingId!.Value)
                .Distinct()
                .LongCount();

            return new ActualInteractionMetrics
            {
                TotalViews = totalViews,
                UniqueViews = uniqueViews,
                TotalClicks = totalClicks,
                UniqueClickers = uniqueClickers,
                BookingStarts = bookingStarts,
                CompletedBookings = completedBookings
            };
        }

        private static (decimal Spent, decimal Remaining) CalculateTimeBasedAmounts(AdvertisementRow row)
        {
            // النظام الجديد Flat: PriceSnapshot = إجمالي قيمة الحملة (سعر اليوم × عدد الأيام).
            if (NormalizeBillingModel(row.BillingModel) != OwnerAdvertisementBillingModels.Flat)
            {
                var legacyRemaining = RemainingBudget(row.TotalBudget, row.SpentAmount);
                return (Math.Max(0m, row.SpentAmount), legacyRemaining);
            }

            var totalAmount = Math.Max(0m, row.PriceSnapshot);
            if (totalAmount <= 0m)
            {
                return (0m, 0m);
            }

            // قبل الدفع والاعتماد والبدء الفعلي: لم يُستهلك أي يوم.
            if (row.PaymentStatus != OwnerAdvertisementPaymentStatuses.Paid ||
                !row.StartAt.HasValue ||
                (row.Status != OwnerAdvertisementStatuses.Active &&
                 row.Status != OwnerAdvertisementStatuses.Paused &&
                 row.Status != OwnerAdvertisementStatuses.Expired &&
                 row.Status != OwnerAdvertisementStatuses.Cancelled))
            {
                return (0m, totalAmount);
            }

            var durationDays = CampaignDurationDays(row.StartAt, row.EndAt, row.DurationDays);
            var dailyPrice = durationDays > 0
                ? totalAmount / durationDays
                : totalAmount;

            int elapsedDays;
            var now = DateTime.Now;

            if (row.Status == OwnerAdvertisementStatuses.Expired ||
                (row.EndAt.HasValue && now >= row.EndAt.Value))
            {
                elapsedDays = durationDays;
            }
            else
            {
                // عند الإيقاف أو الإلغاء نجمّد الحساب عند آخر تحديث للحملة،
                // حتى لا يستمر الخصم أثناء توقف الإعلان.
                var effectiveNow =
                    row.Status == OwnerAdvertisementStatuses.Paused ||
                    row.Status == OwnerAdvertisementStatuses.Cancelled
                        ? row.UpdatedAt
                        : now;

                elapsedDays = effectiveNow <= row.StartAt.Value
                    ? 0
                    : (int)Math.Floor((effectiveNow - row.StartAt.Value).TotalDays);

                elapsedDays = Math.Clamp(elapsedDays, 0, durationDays);
            }

            var spent = Math.Round(
                Math.Min(totalAmount, dailyPrice * elapsedDays),
                2,
                MidpointRounding.AwayFromZero);

            var remaining = Math.Round(
                Math.Max(0m, totalAmount - spent),
                2,
                MidpointRounding.AwayFromZero);

            return (spent, remaining);
        }

        private static decimal RemainingBudget(decimal? totalBudget, decimal spentAmount)
        {
            return totalBudget.HasValue ? Math.Max(0m, totalBudget.Value - spentAmount) : 0m;
        }

        private static string? CleanCancellationReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return null;
            return reason.StartsWith(RefundPendingMarker, StringComparison.Ordinal)
                ? reason[RefundPendingMarker.Length..].Trim()
                : reason.Trim();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string ToRowVersion(byte[]? rowVersion)
        {
            return rowVersion == null || rowVersion.Length == 0
                ? string.Empty
                : Convert.ToBase64String(rowVersion);
        }


        private static List<OwnerAdvertisementOptionDto> StatusOptions()
        {
            return OwnerAdvertisementStatuses.All
                .Select(value => Option(value, StatusLabel(value)))
                .ToList();
        }

        private static List<OwnerAdvertisementOptionDto> PaymentStatusOptions()
        {
            return new[]
                {
                    OwnerAdvertisementPaymentStatuses.Unpaid,
                    OwnerAdvertisementPaymentStatuses.PendingReview,
                    OwnerAdvertisementPaymentStatuses.Paid,
                    OwnerAdvertisementPaymentStatuses.Rejected,
                    OwnerAdvertisementPaymentStatuses.Refunded
                }
                .Select(value => Option(value, PaymentStatusLabel(value)))
                .ToList();
        }

        private static OwnerAdvertisementOptionDto Option(
            string value,
            string label,
            string? description = null,
            string? icon = null)
        {
            return new OwnerAdvertisementOptionDto
            {
                Value = value,
                Label = label,
                Description = description,
                Icon = icon
            };
        }


        private static string DurationLabel(string? value)
        {
            return value switch
            {
                "Daily" => "يوم واحد",
                "Weekly" => "7 أيام",
                "Monthly" => "30 يومًا",
                _ => value ?? "غير محددة"
            };
        }

        private static string BillingModelLabel(string? value)
        {
            return NormalizeBillingModel(value) switch
            {
                OwnerAdvertisementBillingModels.Flat => "مدة محددة",
                OwnerAdvertisementBillingModels.Cpc => "الدفع حسب النقرة",
                OwnerAdvertisementBillingModels.Cpm => "الدفع لكل ألف ظهور",
                _ => value ?? "غير محدد"
            };
        }

        private static string StatusLabel(string? value)
        {
            return value switch
            {
                OwnerAdvertisementStatuses.Draft => "مسودة",
                OwnerAdvertisementStatuses.AwaitingPayment => "بانتظار الدفع",
                OwnerAdvertisementStatuses.Pending => "قيد المراجعة",
                OwnerAdvertisementStatuses.Scheduled => "مجدول",
                OwnerAdvertisementStatuses.Active => "نشط",
                OwnerAdvertisementStatuses.Paused => "موقوف مؤقتًا",
                OwnerAdvertisementStatuses.Expired => "منتهي",
                OwnerAdvertisementStatuses.Rejected => "مرفوض",
                OwnerAdvertisementStatuses.Cancelled => "ملغي",
                _ => value ?? "غير محدد"
            };
        }

        private static string PaymentStatusLabel(string? value)
        {
            return value switch
            {
                OwnerAdvertisementPaymentStatuses.Unpaid => "غير مدفوع",
                OwnerAdvertisementPaymentStatuses.PendingReview => "قيد مراجعة الدفع",
                OwnerAdvertisementPaymentStatuses.Paid => "مدفوع",
                OwnerAdvertisementPaymentStatuses.Rejected => "إثبات الدفع مرفوض",
                OwnerAdvertisementPaymentStatuses.Refunded => "مسترد",
                _ => value ?? "غير محدد"
            };
        }

        private static string TargetingModeLabel(string? value)
        {
            return value == OwnerAdvertisementTargetingModes.Custom ? "استهداف مخصص" : "جميع الزوار";
        }

        private static string DeviceTargetLabel(string? value)
        {
            return value switch
            {
                OwnerAdvertisementDeviceTargets.Desktop => "الكمبيوتر",
                OwnerAdvertisementDeviceTargets.Mobile => "الهاتف",
                _ => "كل الأجهزة"
            };
        }


        private sealed class OwnedPlaceRow
        {
            public int PlaceId { get; init; }
            public string PlaceName { get; init; } = string.Empty;
            public string? PlaceImage { get; init; }
            public string? Coordinates { get; init; }
            public string? Status { get; init; }
            public bool? IsAvailable { get; init; }
            public string? ExchangeRateRegion { get; init; }
            public string? OwnerRole { get; init; }
        }

        private sealed class AdvertisementRow
        {
            public int AdvertisementId { get; init; }
            public int PlaceId { get; init; }
            public int AdvertisementPlanId { get; init; }
            public int? PromotionId { get; init; }
            public string PlaceName { get; init; } = string.Empty;
            public string PlanName { get; init; } = string.Empty;
            public int DisplayWeight { get; init; }
            public string AdvertisementTitle { get; init; } = string.Empty;
            public string? ShortDescription { get; init; }
            public string? ImagePath { get; init; }
            public string ButtonText { get; init; } = string.Empty;
            public string DurationType { get; init; } = string.Empty;
            public int DurationDays { get; init; }
            public string BillingModel { get; init; } = string.Empty;
            public decimal PriceSnapshot { get; init; }
            public decimal? CostPerClick { get; init; }
            public decimal? CostPerThousand { get; init; }
            public decimal? TotalBudget { get; init; }
            public decimal? DailyBudget { get; init; }
            public decimal SpentAmount { get; init; }
            public string Status { get; init; } = string.Empty;
            public string PaymentStatus { get; init; } = string.Empty;
            public string? RejectionReason { get; init; }
            public string? PauseReason { get; init; }
            public string? CancellationReason { get; init; }
            public bool StartImmediately { get; init; }
            public DateTime? RequestedStartAt { get; init; }
            public DateTime? StartAt { get; init; }
            public DateTime? EndAt { get; init; }
            public string TargetingMode { get; init; } = string.Empty;
            public string DeviceTarget { get; init; } = string.Empty;
            public long ImpressionsCount { get; init; }
            public long UniqueImpressionsCount { get; init; }
            public long ClicksCount { get; init; }
            public long BookingStartsCount { get; init; }
            public long ConversionsCount { get; init; }
            public int? PlatformWalletId { get; init; }
            public decimal? TransferredAmount { get; init; }
            public string? ReferenceNumber { get; init; }
            public DateTime? TransferDate { get; init; }
            public string? ReceiptImage { get; init; }
            public DateTime? RefundRequestedAt { get; init; }
            public decimal? RefundAmount { get; init; }
            public int? RefundWalletId { get; init; }
            public string? RefundAccountNumber { get; init; }
            public string? RefundBeneficiaryName { get; init; }
            public string? RefundReferenceNumber { get; init; }
            public string? RefundProofImage { get; init; }
            public DateTime? RefundedAt { get; init; }
            public int? RefundedByUserId { get; init; }
            public string? RefundNote { get; init; }
            public DateTime? SubmittedAt { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime UpdatedAt { get; init; }
            public byte[] RowVersionBytes { get; init; } = Array.Empty<byte>();
            public string RowVersion => ToRowVersion(RowVersionBytes);
        }

        private sealed class ActualInteractionMetrics
        {
            public long TotalViews { get; init; }
            public long UniqueViews { get; init; }
            public long TotalClicks { get; init; }
            public long UniqueClickers { get; init; }
            public long BookingStarts { get; init; }
            public long CompletedBookings { get; init; }
        }

        private sealed class WalletRow
        {
            public string WalletName { get; init; } = string.Empty;
            public string AccountNumber { get; init; } = string.Empty;
            public string BeneficiaryName { get; init; } = string.Empty;
        }

        private sealed class LoadedAdvertisement
        {
            public OwnedPlaceRow? Place { get; init; }
            public Advertisement? Advertisement { get; init; }
            public string? Error { get; init; }
        }

        private sealed class AdvertisementCapabilities
        {
            public bool CanEdit { get; init; }
            public bool CanSubmit { get; init; }
            public bool CanPay { get; init; }
            public bool CanPause { get; init; }
            public bool CanResume { get; init; }
            public bool CanCancel { get; init; }
            public bool CanRenew { get; init; }
            public bool CanRestore { get; init; }
        }

        private sealed class RenewalArchiveRow
        {
            public DateTime ArchivedAt { get; set; }
            public int RenewalNumber { get; set; }
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
            public long Impressions { get; set; }
            public long UniqueImpressions { get; set; }
            public long Clicks { get; set; }
            public long BookingStarts { get; set; }
            public long Conversions { get; set; }
            public decimal SpentAmount { get; set; }
        }

        private enum StateOperation
        {
            Pause,
            Resume,
            Cancel
        }
    }
}
