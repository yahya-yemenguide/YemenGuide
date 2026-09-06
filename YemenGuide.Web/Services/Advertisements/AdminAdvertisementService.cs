using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Advertisements
{
    public sealed class AdminAdvertisementService : IAdminAdvertisementService
    {
        private const string UnifiedPlacement = "SearchTop";
        private const string RefundPendingMarker = "[REFUND_PENDING]";
        private readonly AppDbContext _context;
        private readonly IAdminPermissionService _permissions;
        private readonly IAdminAuditService _audit;
        private readonly IAdvertisementPricingService _pricing;
        private readonly ILogger<AdminAdvertisementService> _logger;
        private readonly INotificationService _notificationService;

        public AdminAdvertisementService(
            AppDbContext context,
            IAdminPermissionService permissions,
            IAdminAuditService audit,
            IAdvertisementPricingService pricing,
            ILogger<AdminAdvertisementService> logger,
            INotificationService notificationService)
        {
            _context = context;
            _permissions = permissions;
            _audit = audit;
            _pricing = pricing;
            _logger = logger;
            _notificationService = notificationService;
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

            // لقطة اليوم في النظرة العامة تستخدم المستخدمين/الزوار المختلفين،
            // حتى تتطابق الأرقام مع تعريف المشاهدات والنقرات في التقارير.
            var todayInteractionRows = await todayEvents
                .Where(x => x.EventType == "Impression" || x.EventType == "Click")
                .Select(x => new
                {
                    x.AdvertisementEventId,
                    x.AdvertisementId,
                    x.EventType,
                    x.UserId,
                    x.VisitorHash,
                    x.SessionKey
                })
                .ToListAsync(cancellationToken);

            var model = new AdminAdvertisementOverviewDto
            {
                AwaitingPaymentCount = await ads.CountAsync(x => x.Status == "AwaitingPayment", cancellationToken),
                PendingPaymentReviewCount = await ads.CountAsync(x => x.PaymentStatus == "PendingReview", cancellationToken),
                PendingReviewCount = await ads.CountAsync(x => x.Status == "Pending" && x.PaymentStatus != "PendingReview", cancellationToken),
                RefundPendingCount = await ads.CountAsync(x =>
                    x.Status == "Cancelled" &&
                    x.PaymentStatus == "Paid" &&
                    x.CancellationReason != null &&
                    x.CancellationReason.StartsWith(RefundPendingMarker), cancellationToken),
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
                TodayImpressions = todayInteractionRows
                    .Where(x => x.EventType == "Impression")
                    .Select(x => ReportInteractionKey(
                        x.AdvertisementId,
                        x.AdvertisementEventId,
                        x.UserId,
                        x.VisitorHash,
                        x.SessionKey))
                    .Distinct()
                    .LongCount(),
                TodayClicks = todayInteractionRows
                    .Where(x => x.EventType == "Click")
                    .Select(x => ReportInteractionKey(
                        x.AdvertisementId,
                        x.AdvertisementEventId,
                        x.UserId,
                        x.VisitorHash,
                        x.SessionKey))
                    .Distinct()
                    .LongCount(),
                TodayBookingStarts = await todayEvents.LongCountAsync(x => x.EventType == "BookingStarted", cancellationToken),
                TodayConversions = await todayEvents.LongCountAsync(x => x.EventType == "BookingCompleted", cancellationToken),
                GeneratedAt = now
            };

            var attention = await AdsWithRelations()
                .Where(x =>
                    x.PaymentStatus == "PendingReview" ||
                    x.Status == "Pending" ||
                    (x.Status == "Cancelled" &&
                     x.PaymentStatus == "Paid" &&
                     x.CancellationReason != null &&
                     x.CancellationReason.StartsWith(RefundPendingMarker)) ||
                    (x.Status == "Active" && x.EndAt.HasValue && x.EndAt >= now && x.EndAt <= endingSoonAt))
                .OrderByDescending(x => x.Status == "Cancelled" &&
                                        x.PaymentStatus == "Paid" &&
                                        x.CancellationReason != null &&
                                        x.CancellationReason.StartsWith(RefundPendingMarker))
                .ThenByDescending(x => x.PaymentStatus == "PendingReview")
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
                    x.PaymentStatus == "PendingReview" ||
                    x.Status == "Pending" ||
                    (x.Status == "Cancelled" &&
                     x.PaymentStatus == "Paid" &&
                     x.CancellationReason != null &&
                     x.CancellationReason.StartsWith(RefundPendingMarker)), cancellationToken),
                RunningCount = await baseQuery.CountAsync(x =>
                    x.Status == "Active" || x.Status == "Scheduled", cancellationToken),
                // الأقسام العلوية تكون غير متداخلة قدر الإمكان:
                // الإعلان الملغي الذي ما زال يحتاج مراجعة دفع أو تنفيذ استرداد يبقى في «تحتاج إجراء»
                // ولا يُحسب «مغلقًا» إلا بعد انتهاء الإجراء المالي المطلوب.
                ClosedCount = await baseQuery.CountAsync(x =>
                    x.Status == "Expired" ||
                    x.Status == "Rejected" ||
                    (x.Status == "Cancelled" &&
                     x.PaymentStatus != "PendingReview" &&
                     !(x.PaymentStatus == "Paid" &&
                       x.CancellationReason != null &&
                       x.CancellationReason.StartsWith(RefundPendingMarker))), cancellationToken),
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
                query = query.Where(x =>
                    x.PaymentStatus == "PendingReview" ||
                    x.Status == "Pending" ||
                    (x.Status == "Cancelled" &&
                     x.PaymentStatus == "Paid" &&
                     x.CancellationReason != null &&
                     x.CancellationReason.StartsWith(RefundPendingMarker)));
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
                query = query.Where(x =>
                    x.Status == "Expired" ||
                    x.Status == "Rejected" ||
                    (x.Status == "Cancelled" &&
                     x.PaymentStatus != "PendingReview" &&
                     !(x.PaymentStatus == "Paid" &&
                       x.CancellationReason != null &&
                       x.CancellationReason.StartsWith(RefundPendingMarker))));
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
                "NeedsAction" => query.OrderByDescending(x =>
                        x.Status == "Cancelled" &&
                        x.PaymentStatus == "Paid" &&
                        x.CancellationReason != null &&
                        x.CancellationReason.StartsWith(RefundPendingMarker))
                    .ThenByDescending(x => x.PaymentStatus == "PendingReview")
                    .ThenByDescending(x => x.Status == "Pending")
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

            var refundDestinations = await (
                from ownerWallet in _context.OwnerWallets.AsNoTracking()
                join wallet in _context.SupportedWallets.AsNoTracking()
                    on ownerWallet.WalletId equals wallet.WalletId
                where ownerWallet.OwnerId == ad.OwnerId &&
                      ownerWallet.IsActive == true &&
                      wallet.IsActive == true
                orderby wallet.WalletName, ownerWallet.AccountNumber
                select new AdminAdvertisementRefundDestinationDto
                {
                    OwnerWalletId = ownerWallet.OwnerWalletId,
                    WalletId = ownerWallet.WalletId,
                    WalletName = wallet.WalletName,
                    AccountNumber = ownerWallet.AccountNumber,
                    BeneficiaryName = ownerWallet.BeneficiaryName
                }).ToListAsync(cancellationToken);

            var refundWallets = await _context.SupportedWallets
                .AsNoTracking()
                .Where(x => x.IsActive == true)
                .OrderBy(x => x.WalletName)
                .Select(x => new AdminAdvertisementRefundWalletDto
                {
                    WalletId = x.WalletId,
                    WalletName = x.WalletName
                })
                .ToListAsync(cancellationToken);

            string? refundedByName = null;
            if (ad.RefundedByUserId.HasValue)
            {
                refundedByName = await _context.Users
                    .AsNoTracking()
                    .Where(x => x.UserId == ad.RefundedByUserId.Value)
                    .Select(x => x.FullName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            string? refundWalletName = null;
            if (ad.RefundWalletId.HasValue)
            {
                refundWalletName = await _context.SupportedWallets
                    .AsNoTracking()
                    .Where(x => x.WalletId == ad.RefundWalletId.Value)
                    .Select(x => x.WalletName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

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
                StatusLabel = AdminStatusLabel(ad.Status, ad.PaymentStatus),
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

                RefundRequestedAt = ad.RefundRequestedAt,
                RefundAmount = ad.RefundAmount,
                RefundWalletId = ad.RefundWalletId,
                RefundWalletName = refundWalletName,
                RefundAccountNumber = ad.RefundAccountNumber,
                RefundBeneficiaryName = ad.RefundBeneficiaryName,
                RefundReferenceNumber = ad.RefundReferenceNumber,
                RefundProofImage = ad.RefundProofImage,
                RefundedAt = ad.RefundedAt,
                RefundedByName = refundedByName,
                RefundNote = ad.RefundNote,
                RefundDestinations = refundDestinations,
                RefundWallets = refundWallets,

                RejectionReason = ad.RejectionReason,
                PauseReason = ad.PauseReason,
                CancellationReason = CleanCancellationReason(ad.CancellationReason),

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
                    cancellationToken),
                CanRefund = IsRefundPending(ad) && await _permissions.HasPermissionAsync(
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
            var reportAds = allAds.Where(x =>
                x.PlacementTypeSnapshot == UnifiedPlacement &&
                x.BillingModelSnapshot == "Flat");
            var scopedAds = ApplyReportScope(reportAds, filter);

            // ندخل الإعلان في نطاق التقرير إذا:
            // 1) أُنشئ خلال الفترة، أو
            // 2) كانت حملته الفعلية متقاطعة مع الفترة المحددة.
            // بهذا لا تبقى المسودات القديمة ذات EndAt = NULL ظاهرة في كل التقارير للأبد.
            var periodAds = scopedAds.Where(x =>
                (x.CreatedAt >= start && x.CreatedAt < end) ||
                (x.StartAt.HasValue &&
                 x.EndAt.HasValue &&
                 x.StartAt.Value < end &&
                 x.EndAt.Value >= start));

            var periodAdIds = periodAds.Select(x => x.AdvertisementId);

            // نقرأ أحداث الفترة مرة واحدة فقط ثم نبني منها كل مؤشرات التقرير.
            // المشاهدات والنقرات هنا فريدة حسب هوية المستخدم/الزائر داخل كل إعلان.
            var eventRows = await _context.AdvertisementEvents
                .AsNoTracking()
                .Where(x =>
                    x.EventAt >= start &&
                    x.EventAt < end &&
                    periodAdIds.Contains(x.AdvertisementId))
                .Select(x => new
                {
                    x.AdvertisementEventId,
                    x.AdvertisementId,
                    x.EventType,
                    x.EventAt,
                    x.UserId,
                    x.VisitorHash,
                    x.SessionKey,
                    x.BookingId
                })
                .ToListAsync(cancellationToken);

            var advertisementRows = await periodAds
                .Select(x => new
                {
                    x.AdvertisementId,
                    x.PlaceId,
                    x.AdvertisementPlanId,
                    Title = x.AdvertisementTitle,
                    PlaceName = x.Place.PlaceNameAndDesc,
                    PlanName = x.AdvertisementPlan.PlanName,
                    PlacementType = x.PlacementTypeSnapshot,
                    BillingModel = x.BillingModelSnapshot,
                    x.Status,
                    x.PaymentStatus,
                    x.CreatedAt,
                    x.DurationDaysSnapshot,
                    x.PrioritySnapshot,
                    x.PriceSnapshot
                })
                .ToListAsync(cancellationToken);

            var paidRows = advertisementRows
                .Where(x => string.Equals(x.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var model = new AdminAdvertisementReportsDto
            {
                Filter = filter,
                From = start,
                To = end.AddTicks(-1),
                GeneratedAt = DateTime.Now,
                TotalAdvertisements = advertisementRows.Count,

                // النظام الحالي سعر يومي ثابت للباقة، والمبلغ النهائي محفوظ Snapshot
                // بعد ضرب سعر اليوم × عدد الأيام التي اختارها المالك.
                PaidRevenue = paidRows.Sum(x => x.PriceSnapshot),
                PaidAdvertisements = paidRows.Count,
                RefundedAdvertisements = advertisementRows.Count(x =>
                    string.Equals(x.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase)),
                PendingPayments = advertisementRows.Count(x =>
                    string.Equals(x.PaymentStatus, "PendingReview", StringComparison.OrdinalIgnoreCase)),

                // CPC/CPM والميزانيات لم تعد جزءًا من النظام الجديد.
                SpentAmount = 0m,
                RemainingBudget = 0m,

                Impressions = eventRows
                    .Where(x => x.EventType == "Impression")
                    .Select(x => ReportInteractionKey(
                        x.AdvertisementId,
                        x.AdvertisementEventId,
                        x.UserId,
                        x.VisitorHash,
                        x.SessionKey))
                    .Distinct()
                    .LongCount(),

                Clicks = eventRows
                    .Where(x => x.EventType == "Click")
                    .Select(x => ReportInteractionKey(
                        x.AdvertisementId,
                        x.AdvertisementEventId,
                        x.UserId,
                        x.VisitorHash,
                        x.SessionKey))
                    .Distinct()
                    .LongCount(),

                BookingStarts = eventRows
                    .Where(x => x.EventType == "BookingStarted")
                    .Select(x => ReportBookingKey(
                        x.AdvertisementId,
                        x.AdvertisementEventId,
                        x.BookingId))
                    .Distinct()
                    .LongCount(),

                Conversions = eventRows
                    .Where(x => x.EventType == "BookingCompleted")
                    .Select(x => ReportBookingKey(
                        x.AdvertisementId,
                        x.AdvertisementEventId,
                        x.BookingId))
                    .Distinct()
                    .LongCount()
            };

            model.PlaceOptions = await reportAds
                .Select(x => new AdminAdvertisementReportOptionDto
                {
                    Id = x.PlaceId,
                    Label = x.Place.PlaceNameAndDesc,
                    SecondaryLabel = x.Owner.FullName
                })
                .Distinct()
                .OrderBy(x => x.Label)
                .ToListAsync(cancellationToken);

            model.PlanOptions = await _context.AdvertisementPlans
                .AsNoTracking()
                .Where(x =>
                    x.PlacementType == UnifiedPlacement &&
                    x.BillingModel == "Flat" &&
                    x.PlanName != "الإعلان الموحد - مدة محددة")
                .OrderByDescending(x => x.IsActive == true)
                .ThenByDescending(x => x.DisplayPriority)
                .ThenBy(x => x.PlanName)
                .Select(x => new AdminAdvertisementReportOptionDto
                {
                    Id = x.AdvertisementPlanId,
                    Label = x.PlanName,
                    SecondaryLabel = x.IsActive == true ? "مفعلة" : "موقفة"
                })
                .ToListAsync(cancellationToken);

            var advertisementOptions = reportAds;
            if (filter.PlaceId.HasValue)
                advertisementOptions = advertisementOptions.Where(x => x.PlaceId == filter.PlaceId.Value);
            if (filter.AdvertisementPlanId.HasValue)
                advertisementOptions = advertisementOptions.Where(x => x.AdvertisementPlanId == filter.AdvertisementPlanId.Value);

            model.AdvertisementOptions = await advertisementOptions
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new AdminAdvertisementReportOptionDto
                {
                    Id = x.AdvertisementId,
                    ParentId = x.PlaceId,
                    Label = x.AdvertisementTitle,
                    SecondaryLabel = x.AdvertisementPlan.PlanName
                })
                .Take(500)
                .ToListAsync(cancellationToken);

            model.ScopeLabel = BuildReportScopeLabel(
                filter,
                model.PlaceOptions,
                model.AdvertisementOptions,
                model.PlanOptions);

            model.AppliedFilters = BuildAppliedFilters(
                filter,
                start,
                end,
                model.PlanOptions);

            model.DailyMetrics = eventRows
                .GroupBy(x => x.EventAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new AdminAdvertisementDailyMetricDto
                {
                    Date = g.Key,
                    Impressions = g
                        .Where(x => x.EventType == "Impression")
                        .Select(x => ReportInteractionKey(
                            x.AdvertisementId,
                            x.AdvertisementEventId,
                            x.UserId,
                            x.VisitorHash,
                            x.SessionKey))
                        .Distinct()
                        .LongCount(),
                    Clicks = g
                        .Where(x => x.EventType == "Click")
                        .Select(x => ReportInteractionKey(
                            x.AdvertisementId,
                            x.AdvertisementEventId,
                            x.UserId,
                            x.VisitorHash,
                            x.SessionKey))
                        .Distinct()
                        .LongCount(),
                    BookingStarts = g
                        .Where(x => x.EventType == "BookingStarted")
                        .Select(x => ReportBookingKey(
                            x.AdvertisementId,
                            x.AdvertisementEventId,
                            x.BookingId))
                        .Distinct()
                        .LongCount(),
                    Conversions = g
                        .Where(x => x.EventType == "BookingCompleted")
                        .Select(x => ReportBookingKey(
                            x.AdvertisementId,
                            x.AdvertisementEventId,
                            x.BookingId))
                        .Distinct()
                        .LongCount(),
                    SpentAmount = 0m
                })
                .ToList();

            // يبقى هذا الملخص للتوافق مع الـDTO، لكن النظام الجديد لديه مكان ظهور واحد فقط.
            model.PlacementMetrics = advertisementRows
                .GroupBy(x => x.PlacementType)
                .Select(g =>
                {
                    var ids = g.Select(x => x.AdvertisementId).ToHashSet();
                    var relatedEvents = eventRows.Where(x => ids.Contains(x.AdvertisementId)).ToList();

                    return new AdminPlacementMetricDto
                    {
                        PlacementType = g.Key,
                        PlacementLabel = "صفحة البحث - أعلى الفئات",
                        Advertisements = g.Count(),
                        Impressions = relatedEvents
                            .Where(x => x.EventType == "Impression")
                            .Select(x => ReportInteractionKey(
                                x.AdvertisementId,
                                x.AdvertisementEventId,
                                x.UserId,
                                x.VisitorHash,
                                x.SessionKey))
                            .Distinct()
                            .LongCount(),
                        Clicks = relatedEvents
                            .Where(x => x.EventType == "Click")
                            .Select(x => ReportInteractionKey(
                                x.AdvertisementId,
                                x.AdvertisementEventId,
                                x.UserId,
                                x.VisitorHash,
                                x.SessionKey))
                            .Distinct()
                            .LongCount(),
                        Conversions = relatedEvents
                            .Where(x => x.EventType == "BookingCompleted")
                            .Select(x => ReportBookingKey(
                                x.AdvertisementId,
                                x.AdvertisementEventId,
                                x.BookingId))
                            .Distinct()
                            .LongCount(),
                        SpentAmount = 0m
                    };
                })
                .OrderByDescending(x => x.Impressions)
                .ToList();

            // للتوافق التاريخي فقط. جميع الإعلانات الجديدة سعر يومي Flat.
            model.BillingMetrics = advertisementRows
                .GroupBy(x => x.BillingModel)
                .Select(g => new AdminBillingMetricDto
                {
                    BillingModel = g.Key,
                    BillingLabel = AdvertisementLookups.BillingLabel(g.Key),
                    Advertisements = g.Count(),
                    PaidAmount = g
                        .Where(x => string.Equals(x.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.PriceSnapshot),
                    SpentAmount = 0m
                })
                .OrderByDescending(x => x.PaidAmount)
                .ToList();

            model.PaymentMetrics = advertisementRows
                .GroupBy(x => x.PaymentStatus)
                .Select(g => new AdminPaymentMetricDto
                {
                    PaymentStatus = g.Key,
                    PaymentStatusLabel = AdvertisementLookups.PaymentLabel(g.Key),
                    Advertisements = g.Count(),
                    Amount = g.Sum(x => x.PriceSnapshot)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var eventByAdvertisement = eventRows
                .GroupBy(x => x.AdvertisementId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Impressions = g
                            .Where(x => x.EventType == "Impression")
                            .Select(x => ReportInteractionKey(
                                x.AdvertisementId,
                                x.AdvertisementEventId,
                                x.UserId,
                                x.VisitorHash,
                                x.SessionKey))
                            .Distinct()
                            .LongCount(),
                        Clicks = g
                            .Where(x => x.EventType == "Click")
                            .Select(x => ReportInteractionKey(
                                x.AdvertisementId,
                                x.AdvertisementEventId,
                                x.UserId,
                                x.VisitorHash,
                                x.SessionKey))
                            .Distinct()
                            .LongCount(),
                        BookingStarts = g
                            .Where(x => x.EventType == "BookingStarted")
                            .Select(x => ReportBookingKey(
                                x.AdvertisementId,
                                x.AdvertisementEventId,
                                x.BookingId))
                            .Distinct()
                            .LongCount(),
                        Conversions = g
                            .Where(x => x.EventType == "BookingCompleted")
                            .Select(x => ReportBookingKey(
                                x.AdvertisementId,
                                x.AdvertisementEventId,
                                x.BookingId))
                            .Distinct()
                            .LongCount()
                    });

            model.TopAdvertisements = advertisementRows.Select(x =>
            {
                eventByAdvertisement.TryGetValue(x.AdvertisementId, out var metric);

                var safeDuration = Math.Clamp(x.DurationDaysSnapshot, 1, 365);
                var dailyPrice = decimal.Round(
                    x.PriceSnapshot / safeDuration,
                    2,
                    MidpointRounding.AwayFromZero);

                return new AdminAdvertisementReportItemDto
                {
                    AdvertisementId = x.AdvertisementId,
                    PlaceId = x.PlaceId,
                    AdvertisementPlanId = x.AdvertisementPlanId,
                    Title = x.Title,
                    PlaceName = x.PlaceName,
                    PlanName = x.PlanName,
                    DisplayWeight = x.PrioritySnapshot,
                    DurationDays = safeDuration,
                    DailyPrice = dailyPrice,
                    TotalAmount = x.PriceSnapshot,
                    PlacementLabel = "صفحة البحث - أعلى الفئات",
                    BillingLabel = "سعر يومي",
                    StatusLabel = AdminStatusLabel(x.Status, x.PaymentStatus),
                    PaymentStatusLabel = AdvertisementLookups.PaymentLabel(x.PaymentStatus),
                    CreatedAt = x.CreatedAt,
                    PaidAmount = string.Equals(x.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
                        ? x.PriceSnapshot
                        : 0m,
                    SpentAmount = 0m,
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
                if (ad.Status == "Cancelled")
                {
                    // أُلغي الإعلان قبل بدء العرض بينما كان إثبات الدفع قيد المراجعة.
                    // بعد اعتماد الحوالة يصبح المبلغ بانتظار تنفيذ الاسترداد من الإدارة.
                    ad.CancellationReason = EnsureRefundPendingMarker(ad.CancellationReason);
                    ad.RefundRequestedAt ??= DateTime.Now;
                }
                else if (ad.Status == "AwaitingPayment")
                {
                    ad.Status = "Pending";
                }
                ad.RejectionReason = null;
            }
            else if (string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.Reason)) return AdminAdvertisementOperationResult.Fail("اكتب سبب رفض إثبات الدفع.");
                ad.PaymentStatus = "Rejected";
                if (ad.Status != "Cancelled")
                {
                    ad.Status = "AwaitingPayment";
                }
                else
                {
                    // ألغى المالك الإعلان بينما كان إثبات الدفع قيد المراجعة، ثم رُفض الإثبات.
                    // لا يوجد مبلغ مؤكد لاسترداده؛ نغلق طلب الاسترداد الشرطي ونحتفظ بالإعلان ملغياً.
                    ad.RefundRequestedAt = null;
                    ad.RefundAmount = null;
                    ad.RefundWalletId = null;
                    ad.RefundAccountNumber = null;
                    ad.RefundBeneficiaryName = null;
                    ad.RefundReferenceNumber = null;
                    ad.RefundProofImage = null;
                    ad.RefundedAt = null;
                    ad.RefundedByUserId = null;
                    ad.RefundNote = null;
                }
                ad.RejectionReason = Clean(request.Reason, 500);
            }
            else return AdminAdvertisementOperationResult.Fail("قرار مراجعة الدفع غير صحيح.");

            ad.ReviewedAt = DateTime.Now;
            ad.ReviewedByUserId = adminUserId;
            ad.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(adminUserId, "Advertisements.PaymentReviewed", "Advertisement", ad.AdvertisementId.ToString(),
                "مراجعة إثبات دفع إعلان.", null, new { decision, ad.PaymentStatus, request.Reason }, cancellationToken: cancellationToken);
            var paymentMessage = ad.PaymentStatus == "Paid"
                ? (ad.Status == "Cancelled"
                    ? "تم اعتماد الدفع. الإعلان ملغى قبل بدء العرض والمبلغ الآن بانتظار تنفيذ الاسترداد."
                    : "تم اعتماد الدفع ونقل الإعلان للمراجعة.")
                : (ad.Status == "Cancelled"
                    ? "تم رفض إثبات الدفع مع إبقاء الإعلان ملغى."
                    : "تم رفض إثبات الدفع وإبلاغ المالك بالسبب.");
            return AdminAdvertisementOperationResult.Ok(paymentMessage);
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

        public async Task<AdminAdvertisementOperationResult> ProcessRefundAsync(
            AdminAdvertisementRefundDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var ad = await _context.Advertisements
                .Include(x => x.Owner)
                .FirstOrDefaultAsync(x => x.AdvertisementId == request.AdvertisementId, cancellationToken);

            if (ad == null)
                return AdminAdvertisementOperationResult.Fail("الإعلان غير موجود.");

            if (!IsRefundPending(ad))
                return AdminAdvertisementOperationResult.Fail("هذا الإعلان لا يملك مبلغ استرداد معلقاً.");

            if (ad.RefundedAt.HasValue || string.Equals(ad.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase))
                return AdminAdvertisementOperationResult.Fail("تم تنفيذ استرداد هذا الإعلان مسبقاً.");

            // إذا اختار المالك وجهة الاسترداد عند الإلغاء فهي الوجهة المعتمدة للعملية.
            // الإدارة توثق التحويل ولا تغيّر حساب الاستلام الذي حدده المالك.
            var hasOwnerSelectedDestination =
                ad.RefundRequestedAt.HasValue &&
                ad.RefundWalletId.HasValue &&
                !string.IsNullOrWhiteSpace(ad.RefundAccountNumber) &&
                !string.IsNullOrWhiteSpace(ad.RefundBeneficiaryName);

            if (hasOwnerSelectedDestination &&
                (request.RefundWalletId != ad.RefundWalletId!.Value ||
                 !string.Equals(
                     Clean(request.RefundAccountNumber, 100),
                     Clean(ad.RefundAccountNumber, 100),
                     StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(
                     Clean(request.RefundBeneficiaryName, 150),
                     Clean(ad.RefundBeneficiaryName, 150),
                     StringComparison.OrdinalIgnoreCase)))
            {
                return AdminAdvertisementOperationResult.Fail(
                    "وجهة الاسترداد حددها المالك عند الإلغاء ولا يمكن تغييرها. أعد فتح الإعلان واستخدم بيانات الاسترداد المحفوظة.");
            }

            var wallet = await _context.SupportedWallets
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.WalletId == request.RefundWalletId &&
                    (hasOwnerSelectedDestination || x.IsActive == true),
                    cancellationToken);

            if (wallet == null)
                return AdminAdvertisementOperationResult.Fail(
                    hasOwnerSelectedDestination
                        ? "وسيلة الاسترداد التي اختارها المالك لم تعد موجودة."
                        : "وسيلة الاسترداد غير موجودة أو غير مفعلة.");

            var accountNumber = Clean(request.RefundAccountNumber, 100);
            var beneficiaryName = Clean(request.RefundBeneficiaryName, 150);
            var referenceNumber = Clean(request.RefundReferenceNumber, 100);
            var proofImage = Clean(request.RefundProofImage, 255);
            var note = Clean(request.RefundNote, 500);

            if (string.IsNullOrWhiteSpace(accountNumber))
                return AdminAdvertisementOperationResult.Fail("رقم الحساب أو المحفظة التي تم رد المبلغ إليها مطلوب.");

            if (string.IsNullOrWhiteSpace(beneficiaryName))
                return AdminAdvertisementOperationResult.Fail("اسم المستفيد مطلوب.");

            if (string.IsNullOrWhiteSpace(referenceNumber) || referenceNumber.Length < 3)
                return AdminAdvertisementOperationResult.Fail("رقم مرجع عملية الاسترداد مطلوب.");

            if (string.IsNullOrWhiteSpace(proofImage) ||
                !proofImage.StartsWith("/uploads/advertisements/refunds/", StringComparison.OrdinalIgnoreCase))
            {
                return AdminAdvertisementOperationResult.Fail("إثبات تحويل مبلغ الاسترداد مطلوب.");
            }

            if (request.OwnerWalletId.HasValue && request.OwnerWalletId.Value > 0)
            {
                var ownerDestination = await _context.OwnerWallets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.OwnerWalletId == request.OwnerWalletId.Value &&
                        x.OwnerId == ad.OwnerId &&
                        x.IsActive == true,
                        cancellationToken);

                if (ownerDestination == null)
                    return AdminAdvertisementOperationResult.Fail("محفظة المالك المختارة لم تعد متاحة.");

                if (ownerDestination.WalletId != request.RefundWalletId ||
                    !string.Equals(ownerDestination.AccountNumber?.Trim(), accountNumber, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ownerDestination.BeneficiaryName?.Trim(), beneficiaryName, StringComparison.OrdinalIgnoreCase))
                {
                    return AdminAdvertisementOperationResult.Fail("بيانات محفظة المالك تغيرت. أعد فتح الإعلان واختر المحفظة من جديد.");
                }
            }

            var duplicateReference = await _context.Advertisements
                .AsNoTracking()
                .AnyAsync(x =>
                    x.AdvertisementId != ad.AdvertisementId &&
                    x.RefundReferenceNumber != null &&
                    x.RefundReferenceNumber == referenceNumber,
                    cancellationToken);

            if (duplicateReference)
                return AdminAdvertisementOperationResult.Fail("رقم مرجع الاسترداد مستخدم في استرداد إعلان آخر.");

            if (!ad.TransferredAmount.HasValue || ad.TransferredAmount.Value <= 0m)
                return AdminAdvertisementOperationResult.Fail("لا يوجد مبلغ محول فعلي صالح للاسترداد لهذا الإعلان.");

            var refundAmount = ad.TransferredAmount.Value;

            var now = DateTime.Now;
            var cleanReason = CleanCancellationReason(ad.CancellationReason);

            ad.RefundRequestedAt ??= ad.UpdatedAt;
            ad.RefundAmount = refundAmount;
            ad.RefundWalletId = request.RefundWalletId;
            ad.RefundAccountNumber = accountNumber;
            ad.RefundBeneficiaryName = beneficiaryName;
            ad.RefundReferenceNumber = referenceNumber;
            ad.RefundProofImage = proofImage;
            ad.RefundedAt = now;
            ad.RefundedByUserId = adminUserId;
            ad.RefundNote = note;
            ad.PaymentStatus = "Refunded";
            ad.CancellationReason = cleanReason;
            ad.UpdatedAt = now;

            _notificationService.Queue(new CreateNotificationDto
            {
                UserId = ad.OwnerId,
                NotificationTitle = "تم استرداد مبلغ إعلانك",
                NotificationText =
                    $"تم استرداد مبلغ إعلان «{ad.AdvertisementTitle}» بقيمة {refundAmount:N2} {ad.Currency}. " +
                    $"تم التحويل عبر {wallet.WalletName} إلى الحساب {accountNumber}، ورقم المرجع {referenceNumber}.",
                NotificationType = NotificationTypes.RefundTransferred,
                TargetUrl =
                    $"/Owner/Advertisements/AdvertisementManagement?tab=advertisements&advertisementId={ad.AdvertisementId}",
                ReferenceType = NotificationReferenceTypes.Advertisement,
                ReferenceId = ad.AdvertisementId
            });

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception,
                    "Failed to process advertisement refund for advertisement {AdvertisementId}",
                    ad.AdvertisementId);
                return AdminAdvertisementOperationResult.Fail("تعذر حفظ عملية الاسترداد. لم يتم اعتمادها، حاول مرة أخرى.");
            }

            try
            {
                await _audit.LogAsync(
                    adminUserId,
                    "Advertisements.RefundCompleted",
                    "Advertisement",
                    ad.AdvertisementId.ToString(),
                    "تنفيذ وتوثيق استرداد مبلغ إعلان.",
                    null,
                    new
                    {
                        ad.RefundAmount,
                        RefundWallet = wallet.WalletName,
                        ad.RefundAccountNumber,
                        ad.RefundBeneficiaryName,
                        ad.RefundReferenceNumber,
                        ad.RefundProofImage,
                        ad.RefundedAt,
                        ad.RefundedByUserId
                    },
                    cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Refund was completed but audit logging failed for advertisement {AdvertisementId}",
                    ad.AdvertisementId);
            }

            return AdminAdvertisementOperationResult.Ok(
                $"تم توثيق استرداد {refundAmount:N2} {ad.Currency} وإشعار المالك بنجاح.",
                ad.AdvertisementId);
        }

        public async Task<AdminAdvertisementOperationResult> ChangeAdvertisementStatusAsync(
            AdminAdvertisementControlDto request,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var ad = await _context.Advertisements.FirstOrDefaultAsync(x => x.AdvertisementId == request.AdvertisementId, cancellationToken);
            if (ad == null) return AdminAdvertisementOperationResult.Fail("الإعلان غير موجود.");
            var oldStatus = ad.Status;
            var oldPaymentStatus = ad.PaymentStatus;
            var action = request.Action?.Trim();
            var now = DateTime.Now;
            var resultMessage = "تم تحديث حالة الإعلان بنجاح.";

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
            else if (string.Equals(action, "Refund", StringComparison.OrdinalIgnoreCase))
            {
                return AdminAdvertisementOperationResult.Fail(
                    "تنفيذ الاسترداد يحتاج توثيق وسيلة التحويل ورقم المرجع ورفع إثبات. استخدم نموذج تنفيذ الاسترداد.");
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
                "تغيير حالة إعلان.",
                new { status = oldStatus, paymentStatus = oldPaymentStatus },
                new { ad.Status, ad.PaymentStatus, request.Reason }, cancellationToken: cancellationToken);
            return AdminAdvertisementOperationResult.Ok(resultMessage);
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
            string? attention = IsRefundPending(ad)
                ? "استرداد مبلغ إعلان بانتظار التنفيذ"
                : ad.PaymentStatus == "PendingReview"
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
                StatusLabel = AdminStatusLabel(ad.Status, ad.PaymentStatus),
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
            if (filter.AdvertisementPlanId.HasValue)
                query = query.Where(x => x.AdvertisementPlanId == filter.AdvertisementPlanId.Value);
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
            filter.AdvertisementPlanId = filter.AdvertisementPlanId.GetValueOrDefault() > 0
                ? filter.AdvertisementPlanId
                : null;
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
            IReadOnlyCollection<AdminAdvertisementReportOptionDto> advertisements,
            IReadOnlyCollection<AdminAdvertisementReportOptionDto> plans)
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

            if (filter.AdvertisementPlanId.HasValue)
            {
                var plan = plans.FirstOrDefault(x => x.Id == filter.AdvertisementPlanId.Value);
                return plan == null ? "تقرير باقة محددة" : $"تقرير الباقة: {plan.Label}";
            }

            return "التقرير العام للمنصة";
        }

        private static List<string> BuildAppliedFilters(
            AdminAdvertisementReportFilterDto filter,
            DateTime start,
            DateTime end,
            IReadOnlyCollection<AdminAdvertisementReportOptionDto> plans)
        {
            var values = new List<string>
            {
                $"الفترة: {start:dd/MM/yyyy} - {end.AddDays(-1):dd/MM/yyyy}"
            };

            if (filter.AdvertisementPlanId.HasValue)
            {
                var plan = plans.FirstOrDefault(x => x.Id == filter.AdvertisementPlanId.Value);
                values.Add(plan == null
                    ? $"الباقة: #{filter.AdvertisementPlanId.Value}"
                    : $"الباقة: {plan.Label}");
            }

            if (!IsAll(filter.Status))
                values.Add($"الحالة: {AdvertisementLookups.StatusLabel(filter.Status)}");

            if (!IsAll(filter.PaymentStatus))
                values.Add($"الدفع: {AdvertisementLookups.PaymentLabel(filter.PaymentStatus)}");

            if (!string.IsNullOrWhiteSpace(filter.Search))
                values.Add($"البحث: {filter.Search}");

            return values;
        }

        private static string ReportInteractionKey(
            int advertisementId,
            long advertisementEventId,
            int? userId,
            string? visitorHash,
            string? sessionKey)
        {
            if (userId.HasValue && userId.Value > 0)
                return $"{advertisementId}|U:{userId.Value}";

            if (!string.IsNullOrWhiteSpace(visitorHash))
                return $"{advertisementId}|V:{visitorHash}";

            if (!string.IsNullOrWhiteSpace(sessionKey))
                return $"{advertisementId}|S:{sessionKey}";

            // fallback آمن للبيانات القديمة التي لا تحتوي هوية زائر.
            return $"{advertisementId}|E:{advertisementEventId}";
        }

        private static string ReportBookingKey(
            int advertisementId,
            long advertisementEventId,
            int? bookingId)
        {
            return bookingId.HasValue && bookingId.Value > 0
                ? $"{advertisementId}|B:{bookingId.Value}"
                : $"{advertisementId}|E:{advertisementEventId}";
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

        private static string AdminStatusLabel(string? status, string? paymentStatus)
        {
            if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                return "بانتظار اعتماد الإعلان";
            }

            if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(paymentStatus, "PendingReview", StringComparison.OrdinalIgnoreCase))
            {
                return "بانتظار مراجعة الدفع";
            }

            return AdvertisementLookups.StatusLabel(status ?? string.Empty);
        }

        private static bool IsRefundPending(Advertisement ad) =>
            ad.Status == "Cancelled" &&
            ad.PaymentStatus == "Paid" &&
            !string.IsNullOrWhiteSpace(ad.CancellationReason) &&
            ad.CancellationReason.StartsWith(RefundPendingMarker, StringComparison.Ordinal);

        private static string EnsureRefundPendingMarker(string? reason)
        {
            var cleanReason = CleanCancellationReason(reason);
            return RefundPendingMarker + (string.IsNullOrWhiteSpace(cleanReason) ? "إلغاء المالك قبل بدء العرض." : cleanReason);
        }

        private static string? CleanCancellationReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return null;
            return reason.StartsWith(RefundPendingMarker, StringComparison.Ordinal)
                ? reason[RefundPendingMarker.Length..].Trim()
                : reason.Trim();
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
