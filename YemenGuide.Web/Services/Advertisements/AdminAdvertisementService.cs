using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Employees;

namespace YemenGuide.Web.Services.Advertisements
{
    public sealed class AdminAdvertisementService : IAdminAdvertisementService
    {
        private const string UnifiedPlacement = "SearchTop";
        private readonly AppDbContext _context;
        private readonly IAdminPermissionService _permissions;
        private readonly IAdminAuditService _audit;
        private readonly IAdvertisementPricingService _pricing;
        private readonly ILogger<AdminAdvertisementService> _logger;

        public AdminAdvertisementService(
            AppDbContext context,
            IAdminPermissionService permissions,
            IAdminAuditService audit,
            IAdvertisementPricingService pricing,
            ILogger<AdminAdvertisementService> logger)
        {
            _context = context;
            _permissions = permissions;
            _audit = audit;
            _pricing = pricing;
            _logger = logger;
        }

        public async Task<AdminAdvertisementManagementPageDto> GetManagementPageAsync(
            int adminUserId,
            string? tab,
            CancellationToken cancellationToken = default)
        {
            var model = new AdminAdvertisementManagementPageDto
            {
                CanView = await _permissions.HasPermissionAsync(adminUserId, AdminPermissionKeys.Advertisements.View, cancellationToken),
                CanReview = await _permissions.HasPermissionAsync(adminUserId, AdminPermissionKeys.Advertisements.Review, cancellationToken),
                CanManagePlans = await _permissions.HasPermissionAsync(adminUserId, AdminPermissionKeys.Advertisements.ManagePlans, cancellationToken),
                CanControl = await _permissions.HasPermissionAsync(adminUserId, AdminPermissionKeys.Advertisements.Control, cancellationToken),
                CanViewReports = await _permissions.HasPermissionAsync(adminUserId, AdminPermissionKeys.Advertisements.Reports, cancellationToken)
            };

            model.DefaultTab = NormalizeTab(tab, model);
            model.Overview = await GetOverviewAsync(cancellationToken);
            model.Advertisements = await GetAdvertisementsAsync(
                new AdminAdvertisementFilterDto { Page = 1, PageSize = 12 },
                adminUserId,
                cancellationToken);
            model.Plans = model.CanManagePlans
                ? await GetPlansAsync(adminUserId, cancellationToken)
                : new AdminPlacementPlansTabDto();
            model.Reports = model.CanViewReports
                ? await GetReportsAsync(new AdminAdvertisementReportFilterDto(), cancellationToken)
                : new AdminAdvertisementReportsDto();
            return model;
        }

        public async Task<AdminAdvertisementOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
        {
            var ads = _context.Advertisements.AsNoTracking();
            var now = DateTime.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var endingSoonAt = now.AddDays(3);
            var todayEvents = _context.AdvertisementEvents.AsNoTracking()
                .Where(x => x.EventAt >= today && x.EventAt < tomorrow);

            var model = new AdminAdvertisementOverviewDto
            {
                AwaitingPaymentCount = await ads.CountAsync(x => x.Status == "AwaitingPayment", cancellationToken),
                PendingPaymentReviewCount = await ads.CountAsync(x => x.PaymentStatus == "PendingReview", cancellationToken),
                PendingReviewCount = await ads.CountAsync(x => x.Status == "Pending" && x.PaymentStatus != "PendingReview", cancellationToken),
                ScheduledCount = await ads.CountAsync(x => x.Status == "Scheduled", cancellationToken),
                ActiveCount = await ads.CountAsync(x => x.Status == "Active", cancellationToken),
                PausedCount = await ads.CountAsync(x => x.Status == "Paused", cancellationToken),
                ExpiredCount = await ads.CountAsync(x => x.Status == "Expired", cancellationToken),
                EndingSoonCount = await ads.CountAsync(x => x.Status == "Active" && x.EndAt.HasValue &&
                    x.EndAt.Value >= now && x.EndAt.Value <= endingSoonAt, cancellationToken),

                // النظام الجديد لا يعتمد على ميزانية CPC/CPM.
                BudgetRiskCount = 0,
                ActivePlacementCount = await _context.AdvertisementPlans.AsNoTracking()
                    .CountAsync(x => x.IsActive == true &&
                                     x.PlacementType == UnifiedPlacement &&
                                     x.BillingModel == "Flat" &&
                                     x.PlanName != "الإعلان الموحد - مدة محددة", cancellationToken),

                PaidRevenue = await ads.Where(x => x.PaymentStatus == "Paid")
                    .SumAsync(x => (decimal?)(x.TransferredAmount ?? x.PriceSnapshot), cancellationToken) ?? 0m,

                SpentAmount = 0m,
                Impressions = await ads.SumAsync(x => (long?)x.ImpressionsCount, cancellationToken) ?? 0,
                Clicks = await ads.SumAsync(x => (long?)x.ClicksCount, cancellationToken) ?? 0,
                Conversions = await ads.SumAsync(x => (long?)x.ConversionsCount, cancellationToken) ?? 0,
                TodaySpentAmount = 0m,
                TodayImpressions = await todayEvents.LongCountAsync(x => x.EventType == "Impression", cancellationToken),
                TodayClicks = await todayEvents.LongCountAsync(x => x.EventType == "Click", cancellationToken),
                TodayBookingStarts = await todayEvents.LongCountAsync(x => x.EventType == "BookingStarted", cancellationToken),
                TodayConversions = await todayEvents.LongCountAsync(x => x.EventType == "BookingCompleted", cancellationToken),
                GeneratedAt = now
            };

            var attention = await AdsWithRelations()
                .Where(x =>
                    x.PaymentStatus == "PendingReview" ||
                    x.Status == "Pending" ||
                    (x.Status == "Active" && x.EndAt.HasValue && x.EndAt >= now && x.EndAt <= endingSoonAt))
                .OrderByDescending(x => x.PaymentStatus == "PendingReview")
                .ThenByDescending(x => x.Status == "Pending")
                .ThenBy(x => x.SubmittedAt ?? x.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            model.NeedsAttention = attention.Select(MapCard).ToList();
            return model;
        }

        public async Task<AdminAdvertisementsTabDto> GetAdvertisementsAsync(
            AdminAdvertisementFilterDto filter,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminAdvertisementFilterDto();
            filter.Page = Math.Max(1, filter.Page);
            filter.PageSize = Math.Clamp(filter.PageSize, 6, 48);

            var baseQuery = _context.Advertisements.AsNoTracking();
            var model = new AdminAdvertisementsTabDto
            {
                Filter = filter,
                AllCount = await baseQuery.CountAsync(cancellationToken),
                AwaitingPaymentCount = await baseQuery.CountAsync(x => x.Status == "AwaitingPayment", cancellationToken),
                PendingCount = await baseQuery.CountAsync(x => x.Status == "Pending", cancellationToken),
                ScheduledCount = await baseQuery.CountAsync(x => x.Status == "Scheduled", cancellationToken),
                ActiveCount = await baseQuery.CountAsync(x => x.Status == "Active", cancellationToken),
                PausedCount = await baseQuery.CountAsync(x => x.Status == "Paused", cancellationToken),
                ExpiredCount = await baseQuery.CountAsync(x => x.Status == "Expired", cancellationToken),
                RejectedCount = await baseQuery.CountAsync(x => x.Status == "Rejected", cancellationToken),
                NeedsActionCount = await baseQuery.CountAsync(x =>
                    x.PaymentStatus == "PendingReview" || x.Status == "Pending", cancellationToken),
                RunningCount = await baseQuery.CountAsync(x =>
                    x.Status == "Active" || x.Status == "Scheduled", cancellationToken),
                ClosedCount = await baseQuery.CountAsync(x =>
                    x.Status == "Expired" || x.Status == "Rejected" || x.Status == "Cancelled", cancellationToken),
                CanReview = await _permissions.HasPermissionAsync(adminUserId, AdminPermissionKeys.Advertisements.Review, cancellationToken),
                CanControl = await _permissions.HasPermissionAsync(adminUserId, AdminPermissionKeys.Advertisements.Control, cancellationToken)
            };

            IQueryable<Advertisement> query = AdsWithRelations();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(x => x.AdvertisementTitle.Contains(search) ||
                                         x.Place.PlaceNameAndDesc.Contains(search) ||
                                         x.Owner.FullName.Contains(search) ||
                                         (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
            }

            if (!IsAll(filter.ExactStatus))
            {
                query = query.Where(x => x.Status == filter.ExactStatus);
            }
            else if (string.Equals(filter.Status, "NeedsAction", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.PaymentStatus == "PendingReview" || x.Status == "Pending");
            }
            else if (string.Equals(filter.Status, "Running", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == "Active" || x.Status == "Scheduled");
            }
            else if (string.Equals(filter.Status, "Paused", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == "Paused");
            }
            else if (string.Equals(filter.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == "Expired" || x.Status == "Rejected" || x.Status == "Cancelled");
            }
            else if (!IsAll(filter.Status))
            {
                // دعم الروابط القديمة التي ترسل الحالة الدقيقة في Status.
                query = query.Where(x => x.Status == filter.Status);
            }
            if (!IsAll(filter.BillingModel)) query = query.Where(x => x.BillingModelSnapshot == filter.BillingModel);
            if (!IsAll(filter.PaymentStatus)) query = query.Where(x => x.PaymentStatus == filter.PaymentStatus);

            model.TotalCount = await query.CountAsync(cancellationToken);
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalCount / (double)filter.PageSize));
            if (filter.Page > model.TotalPages) filter.Page = model.TotalPages;

            var orderedQuery = filter.SortBy switch
            {
                "Oldest" => query.OrderBy(x => x.SubmittedAt ?? x.CreatedAt),
                "HighestBudget" => query.OrderByDescending(x => x.TotalBudget ?? x.PriceSnapshot),
                "MostViewed" => query.OrderByDescending(x => x.ImpressionsCount),
                "NeedsAction" => query.OrderByDescending(x => x.PaymentStatus == "PendingReview" || x.Status == "Pending")
                    .ThenByDescending(x => x.SubmittedAt ?? x.CreatedAt),
                _ => query.OrderByDescending(x => x.SubmittedAt ?? x.CreatedAt)
            };

            var items = await orderedQuery
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            model.Items = items.Select(MapCard).ToList();
            return model;
        }

        public async Task<AdminAdvertisementDetailsDto?> GetAdvertisementDetailsAsync(
            int advertisementId,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var ad = await AdsWithRelations()
                .Include(x => x.ReviewedByUser)
                .FirstOrDefaultAsync(x => x.AdvertisementId == advertisementId, cancellationToken);

            if (ad == null) return null;

            var durationDays = Math.Max(1, ad.DurationDaysSnapshot);
            var totalAmount = ad.PriceSnapshot;
            var dailyPrice = durationDays > 0
                ? decimal.Round(totalAmount / durationDays, 2, MidpointRounding.AwayFromZero)
                : totalAmount;

            return new AdminAdvertisementDetailsDto
            {
                AdvertisementId = ad.AdvertisementId,
                Title = ad.AdvertisementTitle,
                ShortDescription = ad.ShortDescription,
                ImagePath = PrimaryImage(ad),
                ButtonText = ad.ButtonText,
                OwnerName = ad.Owner.FullName,
                OwnerPhone = ad.Owner.PhoneNumber,
                PlaceName = ad.Place.PlaceNameAndDesc,
                PromotionName = ad.Promotion?.PromotionTitle,

                PlanName = ad.AdvertisementPlan?.PlanName ?? "باقة إعلانية",
                DisplayWeight = ad.PrioritySnapshot,
                DurationDays = durationDays,
                DailyPrice = dailyPrice,
                TotalAmount = totalAmount,

                PlacementType = ad.PlacementTypeSnapshot,
                PlacementLabel = "صفحة البحث - أعلى الفئات",
                BillingModel = "Flat",
                BillingLabel = "سعر يومي",

                Status = ad.Status,
                StatusLabel = AdvertisementLookups.StatusLabel(ad.Status),
                PaymentStatus = ad.PaymentStatus,
                PaymentStatusLabel = AdvertisementLookups.PaymentLabel(ad.PaymentStatus),

                // Price يبقى إجمالي قيمة الإعلان للتوافق مع الأجزاء القديمة.
                Price = totalAmount,
                CostPerClick = null,
                CostPerThousand = null,
                TotalBudget = null,
                DailyBudget = null,
                SpentAmount = 0m,
                Currency = ad.Currency,

                StartImmediately = ad.StartImmediately == true,
                RequestedStartAt = ad.RequestedStartAt,
                StartAt = ad.StartAt,
                EndAt = ad.EndAt,

                // الاستهداف الجديد موحّد للجميع، والجهاز هو الاختيار الوحيد.
                TargetingMode = "All",
                DeviceTarget = ad.DeviceTarget,
                FrequencyCap = ad.FrequencyCapPerUserPerDay,
                DestinationType = "Place",
                DestinationPath = null,

                PlatformWalletId = ad.PlatformWalletId,
                TransferredAmount = ad.TransferredAmount,
                ReferenceNumber = ad.ReferenceNumber,
                TransferDate = ad.TransferDate,
                ReceiptImage = ad.ReceiptImage,

                RejectionReason = ad.RejectionReason,
                PauseReason = ad.PauseReason,

                Impressions = ad.ImpressionsCount,
                UniqueImpressions = ad.UniqueImpressionsCount,
                Clicks = ad.ClicksCount,
                BookingStarts = ad.BookingStartsCount,
                Conversions = ad.ConversionsCount,

                CreatedAt = ad.CreatedAt,
                SubmittedAt = ad.SubmittedAt,
                ReviewedAt = ad.ReviewedAt,
                ReviewedByName = ad.ReviewedByUser?.FullName,

                CanReview = await _permissions.HasPermissionAsync(
                    adminUserId,
                    AdminPermissionKeys.Advertisements.Review,
                    cancellationToken),
                CanControl = await _permissions.HasPermissionAsync(
                    adminUserId,
                    AdminPermissionKeys.Advertisements.Control,
                    cancellationToken)
            };
        }

        public async Task<AdminPlacementPlansTabDto> GetPlansAsync(int adminUserId, CancellationToken cancellationToken = default)
        {
            // لا نعرض طرق التسعير القديمة CPC/CPM في النظام الجديد.
            // تبقى بياناتها في القاعدة للتوافق التاريخي فقط.
            var plans = await _context.AdvertisementPlans
                .AsNoTracking()
                .Include(x => x.Advertisements)
                .Where(x =>
                    x.PlacementType == UnifiedPlacement &&
                    x.BillingModel == "Flat" &&
                    x.PlanName != "الإعلان الموحد - مدة محددة")
                .OrderByDescending(x => x.IsActive == true)
                .ThenByDescending(x => x.DisplayPriority)
                .ThenBy(x => x.PlanName)
                .ToListAsync(cancellationToken);

            var model = new AdminPlacementPlansTabDto
            {
                CanManagePlans = await _permissions.HasPermissionAsync(
                    adminUserId,
                    AdminPermissionKeys.Advertisements.ManagePlans,
                    cancellationToken),
                ActiveOptionsCount = plans.Count(x => x.IsActive == true),
                ConfiguredPlacementsCount = plans.Count
            };

            var group = new AdminPlacementGroupDto
            {
                PlacementType = UnifiedPlacement,
                Label = "الباقات والتسعير",
                Description = "مكان الظهور موحّد في صفحة البحث أعلى الفئات. يحدد الأدمن اسم الباقة ووزن الظهور وسعر اليوم فقط.",
                Icon = "bi-stars"
            };

            group.PricingOptions = plans.Select(x => new AdminAdvertisementPlanCardDto
            {
                AdvertisementPlanId = x.AdvertisementPlanId,
                PlanName = x.PlanName,
                DailyPrice = x.Price,
                DisplayWeight = x.DisplayPriority,
                IsActive = x.IsActive == true,
                Description = x.Description,
                AdvertisementCount = x.Advertisements.Count,
                ActiveAdvertisementCount = x.Advertisements.Count(a =>
                    a.Status == "Active" || a.Status == "Scheduled"),

                // توافق مؤقت مع أي جزء قديم لم يتم تحديثه بعد.
                PlacementType = UnifiedPlacement,
                PlacementLabel = "صفحة البحث",
                BillingModel = "Flat",
                BillingLabel = "سعر يومي",
                UnitPrice = x.Price,
                MaxActiveAdvertisements = x.MaxActiveAdvertisements,
                DisplayPriority = x.DisplayPriority,
                MaxMediaItems = 1,
                AllowVideo = true
            }).ToList();

            model.Placements.Add(group);
            return model;
        }

        public async Task<AdminAdvertisementPlanUpsertDto?> GetPlanFormAsync(int? planId, CancellationToken cancellationToken = default)
        {
            if (!planId.HasValue || planId.Value <= 0)
            {
                return new AdminAdvertisementPlanUpsertDto
                {
                    DisplayWeight = 50,
                    IsActive = true,
                    PlacementType = UnifiedPlacement,
                    BillingModel = "Flat",
                    MaxActiveAdvertisements = int.MaxValue,
                    MaxMediaItems = 1,
                    AllowVideo = true
                };
            }

            var plan = await _context.AdvertisementPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.AdvertisementPlanId == planId.Value &&
                    x.PlacementType == UnifiedPlacement,
                    cancellationToken);

            if (plan == null) return null;

            return new AdminAdvertisementPlanUpsertDto
            {
                AdvertisementPlanId = plan.AdvertisementPlanId,
                PlanName = plan.PlanName,
                DailyPrice = plan.Price,
                DisplayWeight = Math.Clamp(plan.DisplayPriority, 1, 100),
                Description = plan.Description,
                IsActive = plan.IsActive == true,

                PlacementType = UnifiedPlacement,
                BillingModel = "Flat",
                UnitPrice = plan.Price,
                MaxActiveAdvertisements = int.MaxValue,
                DisplayPriority = Math.Clamp(plan.DisplayPriority, 1, 100),
                MaxMediaItems = 1,
                AllowVideo = true
            };
        }

        public async Task<AdminAdvertisementReportsDto> GetReportsAsync(
            AdminAdvertisementReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminAdvertisementReportFilterDto();
            NormalizeReportFilter(filter, out var start, out var end);

            var allAds = _context.Advertisements.AsNoTracking();
            var scopedAds = ApplyReportScope(allAds, filter);
            var periodAds = scopedAds.Where(x =>
                x.CreatedAt < end &&
                (!x.EndAt.HasValue || x.EndAt.Value >= start));

            var periodAdIds = periodAds.Select(x => x.AdvertisementId);
            var events = _context.AdvertisementEvents.AsNoTracking()
                .Where(x => x.EventAt >= start && x.EventAt < end && periodAdIds.Contains(x.AdvertisementId));

            var paidAds = periodAds.Where(x =>
                x.PaymentStatus == "Paid" &&
                ((x.TransferDate.HasValue && x.TransferDate.Value >= start && x.TransferDate.Value < end) ||
                 (!x.TransferDate.HasValue && x.SubmittedAt.HasValue && x.SubmittedAt.Value >= start && x.SubmittedAt.Value < end) ||
                 (!x.TransferDate.HasValue && !x.SubmittedAt.HasValue && x.CreatedAt >= start && x.CreatedAt < end)));

            var model = new AdminAdvertisementReportsDto
            {
                Filter = filter,
                From = start,
                To = end.AddTicks(-1),
                GeneratedAt = DateTime.Now,
                TotalAdvertisements = await periodAds.CountAsync(cancellationToken),
                PaidRevenue = await paidAds.SumAsync(
                    x => (decimal?)(x.TransferredAmount ?? x.TotalBudget ?? x.PriceSnapshot), cancellationToken) ?? 0m,
                PaidAdvertisements = await paidAds.CountAsync(cancellationToken),
                RefundedAdvertisements = await periodAds.CountAsync(x => x.PaymentStatus == "Refunded", cancellationToken),
                PendingPayments = await periodAds.CountAsync(x => x.PaymentStatus == "PendingReview", cancellationToken),
                SpentAmount = await events.SumAsync(x => (decimal?)x.CostAmount, cancellationToken) ?? 0m,
                Impressions = await events.LongCountAsync(x => x.EventType == "Impression", cancellationToken),
                Clicks = await events.LongCountAsync(x => x.EventType == "Click", cancellationToken),
                BookingStarts = await events.LongCountAsync(x => x.EventType == "BookingStarted", cancellationToken),
                Conversions = await events.LongCountAsync(x => x.EventType == "BookingCompleted", cancellationToken)
            };

            var totalBudgets = await periodAds.SumAsync(x => (decimal?)x.TotalBudget, cancellationToken) ?? 0m;
            var lifetimeSpend = await periodAds.SumAsync(x => (decimal?)x.SpentAmount, cancellationToken) ?? 0m;
            model.RemainingBudget = Math.Max(0m, totalBudgets - lifetimeSpend);

            model.PlaceOptions = await allAds
                .Select(x => new AdminAdvertisementReportOptionDto
                {
                    Id = x.PlaceId,
                    Label = x.Place.PlaceNameAndDesc,
                    SecondaryLabel = x.Owner.FullName
                })
                .Distinct()
                .OrderBy(x => x.Label)
                .ToListAsync(cancellationToken);

            var advertisementOptions = allAds;
            if (filter.PlaceId.HasValue)
                advertisementOptions = advertisementOptions.Where(x => x.PlaceId == filter.PlaceId.Value);
            model.AdvertisementOptions = await advertisementOptions
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new AdminAdvertisementReportOptionDto
                {
                    Id = x.AdvertisementId,
                    ParentId = x.PlaceId,
                    Label = x.AdvertisementTitle,
                    SecondaryLabel = x.Place.PlaceNameAndDesc
                })
                .Take(500)
                .ToListAsync(cancellationToken);

            model.ScopeLabel = BuildReportScopeLabel(filter, model.PlaceOptions, model.AdvertisementOptions);
            model.AppliedFilters = BuildAppliedFilters(filter, start, end);

            model.DailyMetrics = await events
                .GroupBy(x => x.EventAt.Date)
                .Select(g => new AdminAdvertisementDailyMetricDto
                {
                    Date = g.Key,
                    Impressions = g.LongCount(x => x.EventType == "Impression"),
                    Clicks = g.LongCount(x => x.EventType == "Click"),
                    BookingStarts = g.LongCount(x => x.EventType == "BookingStarted"),
                    Conversions = g.LongCount(x => x.EventType == "BookingCompleted"),
                    SpentAmount = g.Sum(x => x.CostAmount)
                })
                .OrderBy(x => x.Date)
                .ToListAsync(cancellationToken);

            var placementAds = await periodAds
                .GroupBy(x => x.PlacementTypeSnapshot)
                .Select(g => new { PlacementType = g.Key, Advertisements = g.Count() })
                .ToListAsync(cancellationToken);
            var placementEvents = await events
                .GroupBy(x => x.PlacementType)
                .Select(g => new
                {
                    PlacementType = g.Key,
                    Impressions = g.LongCount(x => x.EventType == "Impression"),
                    Clicks = g.LongCount(x => x.EventType == "Click"),
                    Conversions = g.LongCount(x => x.EventType == "BookingCompleted"),
                    SpentAmount = g.Sum(x => x.CostAmount)
                })
                .ToListAsync(cancellationToken);

            model.PlacementMetrics = placementAds
                .Select(x =>
                {
                    var metric = placementEvents.FirstOrDefault(e => e.PlacementType == x.PlacementType);
                    return new AdminPlacementMetricDto
                    {
                        PlacementType = x.PlacementType,
                        PlacementLabel = "صفحة البحث - أعلى الفئات",
                        Advertisements = x.Advertisements,
                        Impressions = metric?.Impressions ?? 0,
                        Clicks = metric?.Clicks ?? 0,
                        Conversions = metric?.Conversions ?? 0,
                        SpentAmount = metric?.SpentAmount ?? 0m
                    };
                })
                .OrderByDescending(x => x.Impressions)
                .ToList();

            var billingAds = await periodAds
                .GroupBy(x => x.BillingModelSnapshot)
                .Select(g => new { BillingModel = g.Key, Advertisements = g.Count() })
                .ToListAsync(cancellationToken);
            var billingSpend = await events
                .GroupBy(x => x.Advertisement.BillingModelSnapshot)
                .Select(g => new { BillingModel = g.Key, SpentAmount = g.Sum(x => x.CostAmount) })
                .ToListAsync(cancellationToken);
            var billingPaid = await paidAds
                .GroupBy(x => x.BillingModelSnapshot)
                .Select(g => new
                {
                    BillingModel = g.Key,
                    PaidAmount = g.Sum(x => (decimal?)(x.TransferredAmount ?? x.TotalBudget ?? x.PriceSnapshot)) ?? 0m
                })
                .ToListAsync(cancellationToken);

            model.BillingMetrics = billingAds.Select(x => new AdminBillingMetricDto
            {
                BillingModel = x.BillingModel,
                BillingLabel = AdvertisementLookups.BillingLabel(x.BillingModel),
                Advertisements = x.Advertisements,
                PaidAmount = billingPaid.FirstOrDefault(p => p.BillingModel == x.BillingModel)?.PaidAmount ?? 0m,
                SpentAmount = billingSpend.FirstOrDefault(s => s.BillingModel == x.BillingModel)?.SpentAmount ?? 0m
            }).OrderByDescending(x => x.PaidAmount).ToList();

            var paymentRows = await periodAds
                .GroupBy(x => x.PaymentStatus)
                .Select(g => new
                {
                    PaymentStatus = g.Key,
                    Advertisements = g.Count(),
                    Amount = g.Sum(x => (decimal?)(x.TransferredAmount ?? x.TotalBudget ?? x.PriceSnapshot)) ?? 0m
                })
                .ToListAsync(cancellationToken);
            model.PaymentMetrics = paymentRows.Select(x => new AdminPaymentMetricDto
            {
                PaymentStatus = x.PaymentStatus,
                PaymentStatusLabel = AdvertisementLookups.PaymentLabel(x.PaymentStatus),
                Advertisements = x.Advertisements,
                Amount = x.Amount
            }).OrderByDescending(x => x.Amount).ToList();

            var eventByAdvertisement = await events
                .GroupBy(x => x.AdvertisementId)
                .Select(g => new
                {
                    AdvertisementId = g.Key,
                    Impressions = g.LongCount(x => x.EventType == "Impression"),
                    Clicks = g.LongCount(x => x.EventType == "Click"),
                    BookingStarts = g.LongCount(x => x.EventType == "BookingStarted"),
                    Conversions = g.LongCount(x => x.EventType == "BookingCompleted"),
                    SpentAmount = g.Sum(x => x.CostAmount)
                })
                .ToListAsync(cancellationToken);

            var advertisementRows = await periodAds
                .Select(x => new
                {
                    x.AdvertisementId,
                    x.PlaceId,
                    Title = x.AdvertisementTitle,
                    PlaceName = x.Place.PlaceNameAndDesc,
                    PlacementType = x.PlacementTypeSnapshot,
                    BillingModel = x.BillingModelSnapshot,
                    x.Status,
                    x.CreatedAt,
                    PaidAmount = x.PaymentStatus == "Paid" &&
                                 ((x.TransferDate.HasValue && x.TransferDate.Value >= start && x.TransferDate.Value < end) ||
                                  (!x.TransferDate.HasValue && x.SubmittedAt.HasValue && x.SubmittedAt.Value >= start && x.SubmittedAt.Value < end) ||
                                  (!x.TransferDate.HasValue && !x.SubmittedAt.HasValue && x.CreatedAt >= start && x.CreatedAt < end))
                        ? (x.TransferredAmount ?? x.TotalBudget ?? x.PriceSnapshot)
                        : 0m
                })
                .ToListAsync(cancellationToken);

            model.TopAdvertisements = advertisementRows.Select(x =>
            {
                var metric = eventByAdvertisement.FirstOrDefault(e => e.AdvertisementId == x.AdvertisementId);
                return new AdminAdvertisementReportItemDto
                {
                    AdvertisementId = x.AdvertisementId,
                    PlaceId = x.PlaceId,
                    Title = x.Title,
                    PlaceName = x.PlaceName,
                    PlacementLabel = "صفحة البحث - أعلى الفئات",
                    BillingLabel = AdvertisementLookups.BillingLabel(x.BillingModel),
                    StatusLabel = AdvertisementLookups.StatusLabel(x.Status),
                    CreatedAt = x.CreatedAt,
                    PaidAmount = x.PaidAmount,
                    SpentAmount = metric?.SpentAmount ?? 0m,
                    Impressions = metric?.Impressions ?? 0,
                    Clicks = metric?.Clicks ?? 0,
                    BookingStarts = metric?.BookingStarts ?? 0,
                    Conversions = metric?.Conversions ?? 0
                };
            }).ToList();
            model.TopAdvertisements = SortReportItems(model.TopAdvertisements, filter).ToList();

            return model;
        }

        public async Task<AdminAdvertisementOperationResult> SavePlanAsync(
            AdminAdvertisementPlanUpsertDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                request ??= new AdminAdvertisementPlanUpsertDto();

                // القيم التالية ثابتة داخليًا ولا يختارها الأدمن.
                request.PlacementType = UnifiedPlacement;
                request.BillingModel = "Flat";
                request.UnitPrice = request.DailyPrice;
                request.DisplayPriority = request.DisplayWeight;
                request.MaxActiveAdvertisements = int.MaxValue;
                request.MaxMediaItems = 1;
                request.AllowVideo = true;
                request.MinimumTotalBudget = null;
                request.DefaultDailyBudget = null;

                var validation = _pricing.ValidatePlan(request);
                if (validation != null)
                    return AdminAdvertisementOperationResult.Fail(validation);

                request.PlanName = request.PlanName.Trim();

                AdvertisementPlan? plan = null;
                object? oldValues = null;

                if (request.AdvertisementPlanId.GetValueOrDefault() > 0)
                {
                    plan = await _context.AdvertisementPlans.FirstOrDefaultAsync(
                        x => x.AdvertisementPlanId == request.AdvertisementPlanId.Value,
                        cancellationToken);

                    if (plan == null)
                        return AdminAdvertisementOperationResult.Fail("الباقة غير موجودة.");

                    oldValues = PlanAudit(plan);
                }

                var duplicateName = await _context.AdvertisementPlans
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.AdvertisementPlanId != request.AdvertisementPlanId.GetValueOrDefault() &&
                        x.PlacementType == UnifiedPlacement &&
                        x.PlanName == request.PlanName,
                        cancellationToken);

                if (duplicateName)
                    return AdminAdvertisementOperationResult.Fail(
                        "يوجد بالفعل باقة بنفس الاسم. اختر اسمًا مختلفًا.");

                if (plan == null)
                {
                    plan = new AdvertisementPlan
                    {
                        CreatedAt = DateTime.Now
                    };
                    _context.AdvertisementPlans.Add(plan);
                }

                ApplyPlan(plan, request);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception)
                {
                    _logger.LogError(
                        exception,
                        "تعذر حفظ باقة الإعلانات في قاعدة البيانات. Inner: {InnerMessage}",
                        exception.InnerException?.Message);

                    return AdminAdvertisementOperationResult.Fail(
                        "تعذر حفظ الباقة في قاعدة البيانات. تحقق من أن اسم الباقة غير مكرر ثم حاول مرة أخرى.");
                }

                // سجل العمليات مهم، لكن فشل السجل لا يجب أن يحول عملية حفظ ناجحة إلى HTTP 500.
                try
                {
                    await _audit.LogAsync(
                        adminUserId,
                        oldValues == null
                            ? "Advertisements.PlanCreated"
                            : "Advertisements.PlanUpdated",
                        "AdvertisementPlan",
                        plan.AdvertisementPlanId.ToString(),
                        oldValues == null
                            ? "إنشاء باقة إعلانية."
                            : "تعديل باقة إعلانية.",
                        oldValues,
                        PlanAudit(plan),
                        cancellationToken: cancellationToken);
                }
                catch (Exception auditException)
                {
                    _logger.LogWarning(
                        auditException,
                        "تم حفظ الباقة رقم {PlanId} لكن تعذر تسجيل العملية في سجل الإدارة.",
                        plan.AdvertisementPlanId);
                }

                return AdminAdvertisementOperationResult.Ok(
                    oldValues == null
                        ? "تم إنشاء الباقة بنجاح."
                        : "تم حفظ تعديلات الباقة بنجاح.",
                    plan.AdvertisementPlanId);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "خطأ غير متوقع أثناء حفظ باقة الإعلانات.");

                return AdminAdvertisementOperationResult.Fail(
                    "حدث خطأ أثناء حفظ الباقة. لم يتم إكمال الطلب.");
            }
        }

        public async Task<AdminAdvertisementOperationResult> ChangePlanStatusAsync(
            AdminAdvertisementPlanStatusDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var plan = await _context.AdvertisementPlans.FirstOrDefaultAsync(
                x => x.AdvertisementPlanId == request.AdvertisementPlanId,
                cancellationToken);

            if (plan == null)
                return AdminAdvertisementOperationResult.Fail("الباقة غير موجودة.");

            var old = plan.IsActive == true;
            plan.IsActive = request.IsActive;
            plan.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(
                adminUserId,
                "Advertisements.PlanStatusChanged",
                "AdvertisementPlan",
                plan.AdvertisementPlanId.ToString(),
                "تغيير حالة باقة إعلانية.",
                new { isActive = old },
                new { isActive = request.IsActive },
                cancellationToken: cancellationToken);

            return AdminAdvertisementOperationResult.Ok(
                request.IsActive
                    ? "تم تفعيل الباقة وأصبحت متاحة للملاك."
                    : "تم إيقاف الباقة. الإعلانات السابقة لن تتأثر.");
        }

        public async Task<AdminAdvertisementOperationResult> DeletePlanAsync(
            int advertisementPlanId,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (advertisementPlanId <= 0)
                return AdminAdvertisementOperationResult.Fail("معرّف الباقة غير صحيح.");

            var plan = await _context.AdvertisementPlans
                .FirstOrDefaultAsync(
                    x => x.AdvertisementPlanId == advertisementPlanId,
                    cancellationToken);

            if (plan == null)
                return AdminAdvertisementOperationResult.Fail("الباقة غير موجودة.");

            var linkedAdvertisements = await _context.Advertisements
                .AsNoTracking()
                .CountAsync(
                    x => x.AdvertisementPlanId == advertisementPlanId,
                    cancellationToken);

            if (linkedAdvertisements > 0)
            {
                return AdminAdvertisementOperationResult.Fail(
                    "لا يمكن حذف هذه الباقة لأنها مستخدمة في إعلانات. يمكنك إيقافها بدلًا من ذلك.");
            }

            var oldValues = PlanAudit(plan);
            var planName = plan.PlanName;

            _context.AdvertisementPlans.Remove(plan);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "تعذر حذف باقة الإعلانات رقم {PlanId}.", advertisementPlanId);
                return AdminAdvertisementOperationResult.Fail(
                    "تعذر حذف الباقة لأنها مرتبطة ببيانات أخرى. يمكنك إيقافها بدلًا من ذلك.");
            }

            try
            {
                await _audit.LogAsync(
                    adminUserId,
                    "Advertisements.PlanDeleted",
                    "AdvertisementPlan",
                    advertisementPlanId.ToString(),
                    $"حذف الباقة الإعلانية «{planName}».",
                    oldValues,
                    null,
                    cancellationToken: cancellationToken);
            }
            catch (Exception auditException)
            {
                _logger.LogWarning(
                    auditException,
                    "تم حذف الباقة رقم {PlanId} لكن تعذر تسجيل العملية في سجل الإدارة.",
                    advertisementPlanId);
            }

            return AdminAdvertisementOperationResult.Ok(
                "تم حذف الباقة بنجاح.",
                advertisementPlanId);
        }

        public async Task<AdminAdvertisementOperationResult> ReviewPaymentAsync(
            AdminAdvertisementPaymentReviewDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var ad = await _context.Advertisements.FirstOrDefaultAsync(x => x.AdvertisementId == request.AdvertisementId, cancellationToken);
            if (ad == null) return AdminAdvertisementOperationResult.Fail("الإعلان غير موجود.");
            if (ad.PaymentStatus != "PendingReview") return AdminAdvertisementOperationResult.Fail("إثبات الدفع ليس قيد المراجعة حالياً.");

            var decision = request.Decision?.Trim();
            if (string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase))
            {
                var expected = ad.BillingModelSnapshot == "Flat" ? ad.PriceSnapshot : ad.TotalBudget ?? ad.PriceSnapshot;
                if (!ad.TransferredAmount.HasValue || ad.TransferredAmount.Value < expected)
                    return AdminAdvertisementOperationResult.Fail("المبلغ المحول أقل من المبلغ المطلوب للإعلان.");
                if (string.IsNullOrWhiteSpace(ad.ReceiptImage) || string.IsNullOrWhiteSpace(ad.ReferenceNumber))
                    return AdminAdvertisementOperationResult.Fail("بيانات إثبات الدفع غير مكتملة.");

                ad.PaymentStatus = "Paid";
                if (ad.Status == "AwaitingPayment") ad.Status = "Pending";
                ad.RejectionReason = null;
            }
            else if (string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.Reason)) return AdminAdvertisementOperationResult.Fail("اكتب سبب رفض إثبات الدفع.");
                ad.PaymentStatus = "Rejected";
                ad.Status = "AwaitingPayment";
                ad.RejectionReason = Clean(request.Reason, 500);
            }
            else return AdminAdvertisementOperationResult.Fail("قرار مراجعة الدفع غير صحيح.");

            ad.ReviewedAt = DateTime.Now;
            ad.ReviewedByUserId = adminUserId;
            ad.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(adminUserId, "Advertisements.PaymentReviewed", "Advertisement", ad.AdvertisementId.ToString(),
                "مراجعة إثبات دفع إعلان.", null, new { decision, ad.PaymentStatus, request.Reason }, cancellationToken: cancellationToken);
            return AdminAdvertisementOperationResult.Ok(ad.PaymentStatus == "Paid" ? "تم اعتماد الدفع ونقل الإعلان للمراجعة." : "تم رفض إثبات الدفع وإبلاغ المالك بالسبب.");
        }

        public async Task<AdminAdvertisementOperationResult> ReviewAdvertisementAsync(
            AdminAdvertisementReviewDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var ad = await _context.Advertisements.FirstOrDefaultAsync(x => x.AdvertisementId == request.AdvertisementId, cancellationToken);
            if (ad == null) return AdminAdvertisementOperationResult.Fail("الإعلان غير موجود.");
            if (ad.Status != "Pending" && ad.Status != "Rejected") return AdminAdvertisementOperationResult.Fail("الإعلان ليس في حالة تسمح بالمراجعة.");

            var decision = request.Decision?.Trim();
            if (string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase))
            {
                if (ad.PaymentStatus != "Paid") return AdminAdvertisementOperationResult.Fail("يجب اعتماد الدفع قبل الموافقة على الإعلان.");
                var now = DateTime.Now;
                var requestedStart = ad.StartImmediately == true ? now : ad.RequestedStartAt ?? now;
                var originalStart = ad.StartAt ?? ad.RequestedStartAt ?? requestedStart;
                var durationDays = ad.EndAt.HasValue && ad.EndAt > originalStart
                    ? (int)Math.Ceiling((ad.EndAt.Value - originalStart).TotalDays - 0.000001d)
                    : Math.Max(1, ad.DurationDaysSnapshot);
                durationDays = Math.Clamp(durationDays, 1, 365);
                var duration = TimeSpan.FromDays(durationDays);
                ad.StartAt = requestedStart < now ? now : requestedStart;
                ad.EndAt = ad.StartAt.Value.Add(duration);
                ad.Status = ad.StartAt.Value > now.AddMinutes(1) ? "Scheduled" : "Active";
                ad.RejectionReason = null;
            }
            else if (string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(decision, "Changes", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.Reason)) return AdminAdvertisementOperationResult.Fail("اكتب سبب الرفض أو التعديلات المطلوبة.");
                ad.Status = "Rejected";
                ad.RejectionReason = string.Equals(decision, "Changes", StringComparison.OrdinalIgnoreCase)
                    ? "مطلوب تعديل: " + Clean(request.Reason, 485)
                    : Clean(request.Reason, 500);
            }
            else return AdminAdvertisementOperationResult.Fail("قرار المراجعة غير صحيح.");

            ad.ReviewedAt = DateTime.Now;
            ad.ReviewedByUserId = adminUserId;
            ad.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(adminUserId, "Advertisements.ContentReviewed", "Advertisement", ad.AdvertisementId.ToString(),
                "مراجعة محتوى إعلان.", null, new { decision, ad.Status, ad.RejectionReason }, cancellationToken: cancellationToken);
            return AdminAdvertisementOperationResult.Ok(ad.Status is "Active" or "Scheduled" ? "تم اعتماد الإعلان بنجاح." : "تمت إعادة الإعلان للمالك مع الملاحظة.");
        }

        public async Task<AdminAdvertisementOperationResult> ChangeAdvertisementStatusAsync(
            AdminAdvertisementControlDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var ad = await _context.Advertisements.FirstOrDefaultAsync(x => x.AdvertisementId == request.AdvertisementId, cancellationToken);
            if (ad == null) return AdminAdvertisementOperationResult.Fail("الإعلان غير موجود.");
            var oldStatus = ad.Status;
            var action = request.Action?.Trim();
            var now = DateTime.Now;

            if (string.Equals(action, "Pause", StringComparison.OrdinalIgnoreCase))
            {
                if (ad.Status is not ("Active" or "Scheduled")) return AdminAdvertisementOperationResult.Fail("لا يمكن إيقاف الإعلان في حالته الحالية.");
                if (string.IsNullOrWhiteSpace(request.Reason)) return AdminAdvertisementOperationResult.Fail("اكتب سبب إيقاف الإعلان.");
                ad.Status = "Paused";
                ad.PausedAt = now;
                ad.PauseReason = Clean(request.Reason, 500);
            }
            else if (string.Equals(action, "Resume", StringComparison.OrdinalIgnoreCase))
            {
                if (ad.Status != "Paused") return AdminAdvertisementOperationResult.Fail("الإعلان غير متوقف حالياً.");
                if (ad.PaymentStatus != "Paid") return AdminAdvertisementOperationResult.Fail("لا يمكن استئناف إعلان غير مدفوع.");
                if (ad.EndAt.HasValue && ad.EndAt <= now) return AdminAdvertisementOperationResult.Fail("انتهت مدة الإعلان ولا يمكن استئنافه.");
                if (ad.TotalBudget.HasValue && ad.SpentAmount >= ad.TotalBudget.Value) return AdminAdvertisementOperationResult.Fail("نفدت ميزانية الإعلان.");
                ad.Status = ad.StartAt.HasValue && ad.StartAt > now ? "Scheduled" : "Active";
                ad.PausedAt = null;
                ad.PauseReason = null;
            }
            else if (string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (ad.Status is "Expired" or "Cancelled") return AdminAdvertisementOperationResult.Fail("الإعلان منتهٍ أو ملغى مسبقاً.");
                if (string.IsNullOrWhiteSpace(request.Reason)) return AdminAdvertisementOperationResult.Fail("اكتب سبب إلغاء الإعلان.");
                ad.Status = "Cancelled";
                ad.CancellationReason = Clean(request.Reason, 500);
            }
            else return AdminAdvertisementOperationResult.Fail("العملية المطلوبة غير صحيحة.");

            ad.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(adminUserId, "Advertisements.StatusChanged", "Advertisement", ad.AdvertisementId.ToString(),
                "تغيير حالة إعلان.", new { status = oldStatus }, new { ad.Status, request.Reason }, cancellationToken: cancellationToken);
            return AdminAdvertisementOperationResult.Ok("تم تحديث حالة الإعلان بنجاح.");
        }

        private IQueryable<Advertisement> AdsWithRelations() =>
            _context.Advertisements.AsNoTracking()
                .Include(x => x.Owner)
                .Include(x => x.Place)
                .Include(x => x.AdvertisementPlan)
                .Include(x => x.Promotion)
                .Include(x => x.AdvertisementMedium)
                .AsSplitQuery();

        private async Task<List<AdminPlacementMetricDto>> BuildPlacementMetricsAsync(
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            var query = _context.Advertisements.AsNoTracking();
            if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value);
            var raw = await query.GroupBy(x => x.PlacementTypeSnapshot)
                .Select(g => new
                {
                    PlacementType = g.Key,
                    Advertisements = g.Count(),
                    Impressions = g.Sum(x => x.ImpressionsCount),
                    Clicks = g.Sum(x => x.ClicksCount),
                    Conversions = g.Sum(x => x.ConversionsCount),
                    SpentAmount = g.Sum(x => x.SpentAmount)
                })
                .OrderByDescending(x => x.Impressions)
                .Take(8)
                .ToListAsync(cancellationToken);
            return raw.Select(x => new AdminPlacementMetricDto
            {
                PlacementType = x.PlacementType,
                PlacementLabel = "صفحة البحث - أعلى الفئات",
                Advertisements = x.Advertisements,
                Impressions = x.Impressions,
                Clicks = x.Clicks,
                Conversions = x.Conversions,
                SpentAmount = x.SpentAmount
            }).ToList();
        }

        private static AdminAdvertisementCardDto MapCard(Advertisement ad)
        {
            string? attention = ad.PaymentStatus == "PendingReview"
                ? "إثبات الدفع بانتظار المراجعة"
                : ad.Status switch
                {
                    "Pending" => "محتوى الإعلان بانتظار المراجعة",
                    "AwaitingPayment" => "بانتظار استكمال أو تصحيح الدفع",
                    "Active" when ad.EndAt.HasValue &&
                                  ad.EndAt >= DateTime.Now &&
                                  ad.EndAt <= DateTime.Now.AddDays(3)
                        => "ينتهي خلال أقل من ثلاثة أيام",
                    _ => null
                };

            return new AdminAdvertisementCardDto
            {
                AdvertisementId = ad.AdvertisementId,
                Title = ad.AdvertisementTitle,
                Description = ad.ShortDescription,
                ImagePath = PrimaryImage(ad),
                OwnerName = ad.Owner.FullName,
                PlaceName = ad.Place.PlaceNameAndDesc,
                PlanName = ad.AdvertisementPlan?.PlanName ?? "باقة إعلانية",
                DisplayWeight = ad.PrioritySnapshot,
                DurationDays = Math.Max(1, ad.DurationDaysSnapshot),
                PlacementType = ad.PlacementTypeSnapshot,
                PlacementLabel = "صفحة البحث - أعلى الفئات",
                BillingModel = "Flat",
                BillingLabel = "سعر يومي",
                Status = ad.Status,
                StatusLabel = AdvertisementLookups.StatusLabel(ad.Status),
                PaymentStatus = ad.PaymentStatus,
                PaymentStatusLabel = AdvertisementLookups.PaymentLabel(ad.PaymentStatus),
                Price = ad.PriceSnapshot,
                Currency = ad.Currency,
                TotalBudget = null,
                SpentAmount = 0m,
                Impressions = ad.ImpressionsCount,
                Clicks = ad.ClicksCount,
                Conversions = ad.ConversionsCount,
                SubmittedAt = ad.SubmittedAt,
                StartAt = ad.StartAt,
                EndAt = ad.EndAt,
                NeedsAttention = attention != null,
                AttentionText = attention
            };
        }

        private static string? PrimaryImage(Advertisement ad) =>
            ad.AdvertisementMedium?.FilePath ??
            ad.ImagePath ??
            ad.Promotion?.ImagePath ??
            ad.Place.CoverImage ??
            ad.Place.LogoImage;

        private static decimal UnitPrice(AdvertisementPlan plan) => plan.Price;

        private static void ApplyPlan(AdvertisementPlan plan, AdminAdvertisementPlanUpsertDto request)
        {
            plan.PlanName = request.PlanName.Trim();

            // ثوابت النظام الجديد.
            plan.PlacementType = UnifiedPlacement;
            plan.DurationType = "Daily";
            plan.DurationDays = 1;
            plan.Currency = "ريال يمني";
            plan.BillingModel = "Flat";

            // Price أصبح سعر اليوم.
            plan.Price = request.DailyPrice;

            // لا يوجد CPC/CPM أو ميزانيات في النظام الجديد.
            plan.CostPerClick = null;
            plan.CostPerThousandImpressions = null;
            plan.MinimumTotalBudget = null;
            plan.DefaultDailyBudget = null;

            // لا نقيّد عدد الإعلانات عبر الباقة.
            plan.MaxActiveAdvertisements = int.MaxValue;

            // DisplayPriority أصبح وزن الظهور.
            plan.DisplayPriority = request.DisplayWeight;

            // كل إعلان له وسيط واحد: صورة أو فيديو، والفيديو متاح لكل الباقات.
            plan.MaxMediaItems = 1;
            plan.AllowVideo = true;

            plan.Description = Clean(request.Description, 500);
            plan.IsActive = request.IsActive;
            plan.UpdatedAt = DateTime.Now;
        }

        private static object PlanAudit(AdvertisementPlan plan) => new
        {
            plan.AdvertisementPlanId,
            plan.PlanName,
            plan.PlacementType,
            DailyPrice = plan.Price,
            DisplayWeight = plan.DisplayPriority,
            plan.Description,
            plan.IsActive
        };

        private static string NormalizeTab(string? tab, AdminAdvertisementManagementPageDto model)
        {
            var normalized = tab?.Trim().ToLowerInvariant();
            if (normalized == "ads" && model.CanView) return "ads";
            if (normalized == "placements" && model.CanManagePlans) return "placements";
            if (normalized == "reports" && model.CanViewReports) return "reports";
            return "overview";
        }

        private static IQueryable<Advertisement> ApplyReportScope(
            IQueryable<Advertisement> query,
            AdminAdvertisementReportFilterDto filter)
        {
            if (filter.PlaceId.HasValue)
                query = query.Where(x => x.PlaceId == filter.PlaceId.Value);
            if (filter.AdvertisementId.HasValue)
                query = query.Where(x => x.AdvertisementId == filter.AdvertisementId.Value);
            // النظام الجديد موحّد بسعر يومي؛ نتجاهل فلتر طرق التسعير القديمة.
            if (!IsAll(filter.Status))
                query = query.Where(x => x.Status == filter.Status);
            if (!IsAll(filter.PaymentStatus))
                query = query.Where(x => x.PaymentStatus == filter.PaymentStatus);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(x =>
                    x.AdvertisementTitle.Contains(search) ||
                    x.Place.PlaceNameAndDesc.Contains(search) ||
                    x.Owner.FullName.Contains(search) ||
                    (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
            }
            return query;
        }

        private static void NormalizeReportFilter(
            AdminAdvertisementReportFilterDto filter,
            out DateTime start,
            out DateTime end)
        {
            end = (filter.To ?? DateTime.Today).Date.AddDays(1);
            start = (filter.From ?? end.AddDays(-30)).Date;
            if (start >= end) start = end.AddDays(-30);
            if ((end - start).TotalDays > 366) start = end.AddDays(-366);

            filter.From = start;
            filter.To = end.AddDays(-1);
            filter.PlaceId = filter.PlaceId.GetValueOrDefault() > 0 ? filter.PlaceId : null;
            filter.AdvertisementId = filter.AdvertisementId.GetValueOrDefault() > 0 ? filter.AdvertisementId : null;
            filter.PlacementType = "All";
            filter.BillingModel = "All";
            filter.Status = NormalizeReportChoice(filter.Status,
                new[] { "Draft", "AwaitingPayment", "Pending", "Scheduled", "Active", "Paused", "Expired", "Rejected", "Cancelled" });
            filter.PaymentStatus = NormalizeReportChoice(filter.PaymentStatus,
                new[] { "Unpaid", "PendingReview", "Paid", "Rejected", "Refunded" });
            filter.Search = Clean(filter.Search, 100);
            filter.SortBy = NormalizeReportChoice(filter.SortBy,
                new[] { "Impressions", "Clicks", "BookingStarts", "Conversions", "Revenue", "SpentAmount", "Newest" });
            if (IsAll(filter.SortBy)) filter.SortBy = "Impressions";
            filter.SortDirection = string.Equals(filter.SortDirection, "Asc", StringComparison.OrdinalIgnoreCase) ? "Asc" : "Desc";
        }

        private static string NormalizeReportChoice(string? value, IEnumerable<string> allowed)
        {
            if (IsAll(value)) return "All";
            return allowed.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) ?? "All";
        }

        private static string BuildReportScopeLabel(
            AdminAdvertisementReportFilterDto filter,
            IReadOnlyCollection<AdminAdvertisementReportOptionDto> places,
            IReadOnlyCollection<AdminAdvertisementReportOptionDto> advertisements)
        {
            if (filter.AdvertisementId.HasValue)
            {
                var advertisement = advertisements.FirstOrDefault(x => x.Id == filter.AdvertisementId.Value);
                return advertisement == null ? "تقرير إعلان محدد" : $"تقرير الإعلان: {advertisement.Label}";
            }
            if (filter.PlaceId.HasValue)
            {
                var place = places.FirstOrDefault(x => x.Id == filter.PlaceId.Value);
                return place == null ? "تقرير منشأة محددة" : $"تقرير المنشأة: {place.Label}";
            }
            return "التقرير العام للمنصة";
        }

        private static List<string> BuildAppliedFilters(
            AdminAdvertisementReportFilterDto filter,
            DateTime start,
            DateTime end)
        {
            var values = new List<string> { $"الفترة: {start:dd/MM/yyyy} - {end.AddDays(-1):dd/MM/yyyy}" };
            if (!IsAll(filter.BillingModel))
                values.Add($"المحاسبة: {AdvertisementLookups.BillingLabel(filter.BillingModel)}");
            if (!IsAll(filter.Status))
                values.Add($"الحالة: {AdvertisementLookups.StatusLabel(filter.Status)}");
            if (!IsAll(filter.PaymentStatus))
                values.Add($"الدفع: {AdvertisementLookups.PaymentLabel(filter.PaymentStatus)}");
            if (!string.IsNullOrWhiteSpace(filter.Search))
                values.Add($"البحث: {filter.Search}");
            return values;
        }

        private static IEnumerable<AdminAdvertisementReportItemDto> SortReportItems(
            IEnumerable<AdminAdvertisementReportItemDto> items,
            AdminAdvertisementReportFilterDto filter)
        {
            var ascending = string.Equals(filter.SortDirection, "Asc", StringComparison.OrdinalIgnoreCase);
            return filter.SortBy switch
            {
                "Clicks" => ascending ? items.OrderBy(x => x.Clicks) : items.OrderByDescending(x => x.Clicks),
                "BookingStarts" => ascending ? items.OrderBy(x => x.BookingStarts) : items.OrderByDescending(x => x.BookingStarts),
                "Conversions" => ascending ? items.OrderBy(x => x.Conversions) : items.OrderByDescending(x => x.Conversions),
                "Revenue" => ascending ? items.OrderBy(x => x.PaidAmount) : items.OrderByDescending(x => x.PaidAmount),
                "SpentAmount" => ascending ? items.OrderBy(x => x.SpentAmount) : items.OrderByDescending(x => x.SpentAmount),
                "Newest" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                _ => ascending ? items.OrderBy(x => x.Impressions) : items.OrderByDescending(x => x.Impressions)
            };
        }

        private static bool IsAll(string? value) => string.IsNullOrWhiteSpace(value) || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase);

        private static string? Clean(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var cleaned = value.Trim();
            return cleaned.Length <= max ? cleaned : cleaned[..max];
        }
    }
}
