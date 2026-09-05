using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Data;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAdminPermissionService _permissionService;
        private readonly CurrencyService _currencyService;

        public AdminDashboardController(
            AppDbContext context,
            IAdminPermissionService permissionService,
            CurrencyService currencyService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _currencyService = currencyService ?? throw new ArgumentNullException(nameof(currencyService));
        }

        [RequireAdminPermission(AdminPermissionKeys.Dashboard.View)]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(DashboardOverview));
        }

        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Dashboard.View)]
        public async Task<IActionResult> DashboardOverview()
        {
            var now = DateTime.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var trendStart = today.AddDays(-6);

            bool isArabic =
                string.Equals(
                    CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                    "ar",
                    StringComparison.OrdinalIgnoreCase);

            string L(string ar, string en) =>
                isArabic ? ar : en;

            var viewModel = new DashboardDto
            {
                GeneratedAt = now,
                AdminName = L("مدير النظام", "System Admin")
            };

            /*
             * اسم المدير + عملة العرض من نفس الحساب.
             * هذه هي العملة التي يختارها المستخدم من البروفايل.
             */
            string? userPreferredCurrency = null;

            var currentUserIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (int.TryParse(
                    currentUserIdText,
                    out var currentUserId) &&
                currentUserId > 0)
            {
                var currentAdmin =
                    await _context.Users
                        .AsNoTracking()
                        .Where(u =>
                            u.UserId == currentUserId)
                        .Select(u => new
                        {
                            u.FullName,
                            u.PreferredCurrency
                        })
                        .FirstOrDefaultAsync();

                if (currentAdmin != null)
                {
                    viewModel.AdminName =
                        string.IsNullOrWhiteSpace(
                            currentAdmin.FullName)
                            ? L(
                                "مدير النظام",
                                "System Admin")
                            : currentAdmin.FullName;

                    userPreferredCurrency =
                        currentAdmin
                            .PreferredCurrency;
                }
            }

            /*
             * Dashboard يعرض إجماليات مالية للمنصة وليست سعر منشأة محددة.
             * السلوك القديم كان يستخدم CurrencyService بدون CityId،
             * أي سوق صنعاء كمرجع احتياطي.
             *
             * نحافظ على نفس المرجع، لكن نأخذ العملة أولًا من البروفايل.
             * ونقرأ أسعار الصرف مرة واحدة فقط لتفادي Queries متكررة.
             */
            var currencySettings =
                await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(s => s.Id)
                    .Select(s => new
                    {
                        s.DefaultCurrency,
                        s.SanaaUsdRate,
                        s.SanaaSarRate
                    })
                    .FirstOrDefaultAsync();

            string preferredCurrency =
                CurrencyDisplayService
                    .NormalizeCurrency(
                        userPreferredCurrency);

            if (string.IsNullOrWhiteSpace(
                    preferredCurrency))
            {
                preferredCurrency =
                    CurrencyDisplayService
                        .NormalizeCurrency(
                            currencySettings
                                ?.DefaultCurrency);
            }

            if (string.IsNullOrWhiteSpace(
                    preferredCurrency))
            {
                preferredCurrency =
                    CurrencyDisplayService
                        .YemeniRial;
            }

            viewModel.DisplayCurrency =
                preferredCurrency;

            viewModel.CurrencyCode =
                CurrencyDisplayService
                    .GetCurrencyCode(
                        preferredCurrency);

            viewModel.CurrencySymbol =
                CurrencyDisplayService
                    .GetCurrencySymbol(
                        preferredCurrency);

            decimal configuredRate =
                preferredCurrency switch
                {
                    CurrencyDisplayService
                        .UsDollar =>
                            currencySettings
                                ?.SanaaUsdRate
                            ?? 0m,

                    CurrencyDisplayService
                        .SaudiRiyal =>
                            currencySettings
                                ?.SanaaSarRate
                            ?? 0m,

                    _ => 1m
                };

            decimal ConvertDashboardAmount(
                decimal amountInYer)
            {
                if (preferredCurrency ==
                    CurrencyDisplayService
                        .YemeniRial)
                {
                    return Math.Round(
                        amountInYer,
                        0,
                        MidpointRounding
                            .AwayFromZero);
                }

                if (configuredRate > 0m)
                {
                    return Math.Round(
                        amountInYer /
                        configuredRate,
                        2,
                        MidpointRounding
                            .AwayFromZero);
                }

                /*
                 * fallback نادر فقط لو سعر الإعدادات غير صالح.
                 * CurrencyService تستخدم حماية الأسعار الموجودة عندها.
                 */
                return _currencyService
                    .ConvertAmount(
                        amountInYer,
                        preferredCurrency,
                        ExchangeRateRegionResolver.SanaaMarket);
            }

            string FormatDashboardAmount(
                decimal convertedAmount)
            {
                string number =
                    CurrencyDisplayService
                        .FormatNumber(
                            convertedAmount,
                            preferredCurrency);

                return preferredCurrency ==
                       CurrencyDisplayService
                           .UsDollar
                    ? $"${number}"
                    : $"{number} {viewModel.CurrencySymbol}";
            }

            viewModel.TotalCategories =
                await _context.Categories
                    .CountAsync();

            viewModel.TotalPlaces =
                await _context.Places
                    .CountAsync();

            viewModel.PendingPlaces =
                await _context.Places
                    .CountAsync(p =>
                        p.Status == "Pending");

            viewModel.ActivePlaces =
                await _context.Places
                    .CountAsync(p =>
                        p.IsAvailable == true &&
                        (
                            p.Status == "Approved" ||
                            p.Status == "Active"
                        ));

            viewModel.TotalUsers =
                await _context.Users
                    .CountAsync();

            viewModel.TotalBookings =
                await _context.Bookings
                    .CountAsync();

            viewModel.TodayBookingsCreated =
                await _context.Bookings
                    .CountAsync(b =>
                        b.CreatedAt >= today &&
                        b.CreatedAt < tomorrow);

            viewModel.TodayBookingsStarting =
                await _context.Bookings
                    .CountAsync(b =>
                        b.StartDate >= today &&
                        b.StartDate < tomorrow);

            viewModel.ThisMonthBookings =
                await _context.Bookings
                    .CountAsync(b =>
                        b.CreatedAt >= monthStart &&
                        b.CreatedAt < nextMonth);

            viewModel.TotalRevenue =
                await _context
                    .FinancialRecords
                    .Where(f =>
                        f.CommissionStatus ==
                        "Charged")
                    .SumAsync(f =>
                        (decimal?)
                        f.PlatformCommission)
                ?? 0m;

            viewModel.PendingRevenue =
                await _context
                    .FinancialRecords
                    .Where(f =>
                        f.CommissionStatus ==
                            "Calculated" ||
                        f.CommissionStatus ==
                            "Held" ||
                        f.CommissionStatus ==
                            "Debt")
                    .SumAsync(f =>
                        (decimal?)
                        f.PlatformCommission)
                ?? 0m;

            viewModel.TodayRevenue =
                await _context
                    .FinancialRecords
                    .Where(f =>
                        f.CommissionStatus ==
                            "Charged" &&
                        f.CommissionChargedAt
                            .HasValue &&
                        f.CommissionChargedAt
                            .Value >= today &&
                        f.CommissionChargedAt
                            .Value < tomorrow)
                    .SumAsync(f =>
                        (decimal?)
                        f.PlatformCommission)
                ?? 0m;

            viewModel.CategoryDistribution =
                await _context.Categories
                    .AsNoTracking()
                    .Select(c =>
                        new CategoryDistributionDto
                        {
                            CategoryName =
                                c.CategoryName,

                            PlaceCount =
                                c.Places.Count()
                        })
                    .Where(x =>
                        x.PlaceCount > 0)
                    .OrderByDescending(x =>
                        x.PlaceCount)
                    .ToListAsync();

            viewModel.PlaceStatusDistribution =
                await _context.Places
                    .AsNoTracking()
                    .GroupBy(p =>
                        p.Status ??
                        "Unknown")
                    .Select(g =>
                        new StatusDistributionDto
                        {
                            Status =
                                g.Key,

                            Count =
                                g.Count()
                        })
                    .OrderByDescending(x =>
                        x.Count)
                    .ToListAsync();

            viewModel.FinanceDistribution =
                await _context
                    .FinancialRecords
                    .AsNoTracking()
                    .GroupBy(f =>
                        f.CommissionStatus)
                    .Select(g =>
                        new FinanceDistributionDto
                        {
                            Status =
                                g.Key,

                            Count =
                                g.Count(),

                            Amount =
                                g.Sum(x =>
                                    x.PlatformCommission)
                        })
                    .OrderByDescending(x =>
                        x.Amount)
                    .ToListAsync();

            var createdBookings =
                await _context.Bookings
                    .AsNoTracking()
                    .Where(b =>
                        b.CreatedAt.HasValue &&
                        b.CreatedAt.Value >=
                            trendStart &&
                        b.CreatedAt.Value <
                            tomorrow)
                    .GroupBy(b =>
                        b.CreatedAt
                            .Value.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

            var startingBookings =
                await _context.Bookings
                    .AsNoTracking()
                    .Where(b =>
                        b.StartDate >=
                            trendStart &&
                        b.StartDate <
                            tomorrow)
                    .GroupBy(b =>
                        b.StartDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

            viewModel.BookingTrend =
                Enumerable
                    .Range(0, 7)
                    .Select(offset =>
                        trendStart.AddDays(
                            offset))
                    .Select(date =>
                        new DashboardTrendPointDto
                        {
                            Label =
                                date.ToString(
                                    "ddd",
                                    CultureInfo
                                        .CurrentUICulture),

                            Count =
                                createdBookings
                                    .FirstOrDefault(
                                        x =>
                                            x.Date ==
                                            date)
                                    ?.Count
                                ?? 0,

                            Amount =
                                startingBookings
                                    .FirstOrDefault(
                                        x =>
                                            x.Date ==
                                            date)
                                    ?.Count
                                ?? 0
                        })
                    .ToList();

            var revenueRaw =
                await _context
                    .FinancialRecords
                    .AsNoTracking()
                    .Where(f =>
                        f.CommissionStatus ==
                            "Charged" &&
                        f.CommissionChargedAt
                            .HasValue &&
                        f.CommissionChargedAt
                            .Value >=
                            trendStart &&
                        f.CommissionChargedAt
                            .Value <
                            tomorrow)
                    .GroupBy(f =>
                        f.CommissionChargedAt!
                            .Value.Date)
                    .Select(g => new
                    {
                        Date =
                            g.Key,

                        Amount =
                            g.Sum(x =>
                                x.PlatformCommission)
                    })
                    .ToListAsync();

            viewModel.RevenueTrend =
                Enumerable
                    .Range(0, 7)
                    .Select(offset =>
                        trendStart.AddDays(
                            offset))
                    .Select(date =>
                        new DashboardTrendPointDto
                        {
                            Label =
                                date.ToString(
                                    "ddd",
                                    CultureInfo
                                        .CurrentUICulture),

                            Amount =
                                revenueRaw
                                    .FirstOrDefault(
                                        x =>
                                            x.Date ==
                                            date)
                                    ?.Amount
                                ?? 0m
                        })
                    .ToList();

            viewModel.RecentPlaces =
                await _context.Places
                    .AsNoTracking()
                    .Include(p =>
                        p.Category)
                    .Include(p =>
                        p.Owner)
                    .OrderByDescending(p =>
                        p.PlaceId)
                    .Take(6)
                    .Select(p =>
                        new RecentPlaceDto
                        {
                            Id =
                                p.PlaceId,

                            PlaceName =
                                p.PlaceNameAndDesc,

                            CategoryName =
                                p.Category != null
                                    ? p.Category
                                        .CategoryName
                                    : (
                                        isArabic
                                            ? "غير محدد"
                                            : "Unspecified"
                                    ),

                            OwnerName =
                                p.Owner != null
                                    ? p.Owner
                                        .FullName
                                    : (
                                        isArabic
                                            ? "بدون مالك"
                                            : "No owner"
                                    ),

                            Status =
                                p.Status
                        })
                    .ToListAsync();

            /*
             * تحويل كل القيم المالية قبل إرسالها للـView.
             * كذلك الرسوم البيانية تستقبل قيم العملة المختارة.
             */
            viewModel.TotalRevenue =
                ConvertDashboardAmount(
                    viewModel.TotalRevenue);

            viewModel.PendingRevenue =
                ConvertDashboardAmount(
                    viewModel.PendingRevenue);

            viewModel.TodayRevenue =
                ConvertDashboardAmount(
                    viewModel.TodayRevenue);

            foreach (var point in
                     viewModel.RevenueTrend)
            {
                point.Amount =
                    ConvertDashboardAmount(
                        point.Amount);
            }

            foreach (var item in
                     viewModel.FinanceDistribution)
            {
                item.Amount =
                    ConvertDashboardAmount(
                        item.Amount);
            }

            viewModel.TotalRevenueText =
                FormatDashboardAmount(
                    viewModel.TotalRevenue);

            viewModel.PendingRevenueText =
                FormatDashboardAmount(
                    viewModel.PendingRevenue);

            viewModel.TodayRevenueText =
                FormatDashboardAmount(
                    viewModel.TodayRevenue);

            BuildAttentionItems(
                viewModel,
                isArabic);

            return View(viewModel);
        }


        private static void BuildAttentionItems(
            DashboardDto model,
            bool isArabic)
        {
            string L(string ar, string en) =>
                isArabic ? ar : en;

            string FormatAmount(decimal amount)
            {
                string number =
                    CurrencyDisplayService
                        .FormatNumber(
                            amount,
                            model.DisplayCurrency);

                return model.DisplayCurrency ==
                       CurrencyDisplayService
                           .UsDollar
                    ? $"${number}"
                    : $"{number} {model.CurrencySymbol}";
            }

            if (model.PendingPlaces > 0)
            {
                model.AttentionItems.Add(
                    new DashboardAttentionItemDto
                    {
                        Key =
                            "pending-places",

                        Title =
                            L(
                                "منشآت تنتظر المراجعة",
                                "Places awaiting review"),

                        Description =
                            L(
                                "طلبات تحتاج قرار الإدارة قبل ظهورها على المنصة.",
                                "Requests that need an admin decision before appearing on the platform."),

                        IconClass =
                            "bi bi-buildings-fill",

                        Tone =
                            "warning",

                        ValueText =
                            model.PendingPlaces
                                .ToString("N0"),

                        Url =
                            "/Admin/Places/PlaceManagement"
                    });
            }

            if (model.PendingRevenue > 0m)
            {
                model.AttentionItems.Add(
                    new DashboardAttentionItemDto
                    {
                        Key =
                            "pending-finance",

                        Title =
                            L(
                                "عمولات داخل دورة التحصيل",
                                "Commissions in collection cycle"),

                        Description =
                            L(
                                "مبالغ محسوبة أو محجوزة أو مديونية تحتاج متابعة مالية.",
                                "Calculated, held, or debt amounts that need financial follow-up."),

                        IconClass =
                            "bi bi-wallet2",

                        Tone =
                            "danger",

                        ValueText =
                            FormatAmount(
                                model.PendingRevenue),

                        Url =
                            "/Admin/Financials/Manage?financeTab=commissions"
                    });
            }

            var debt =
                model.FinanceDistribution
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Status,
                            "Debt",
                            StringComparison
                                .OrdinalIgnoreCase));

            if (debt != null &&
                debt.Count > 0)
            {
                model.AttentionItems.Add(
                    new DashboardAttentionItemDto
                    {
                        Key =
                            "debt",

                        Title =
                            L(
                                "سجلات مالية بحالة مديونية",
                                "Financial records in debt"),

                        Description =
                            L(
                                "حالات تستحق مراجعة قبل تراكم رصيد الالتزامات.",
                                "Cases that should be reviewed before liabilities accumulate."),

                        IconClass =
                            "bi bi-exclamation-octagon-fill",

                        Tone =
                            "purple",

                        ValueText =
                            debt.Count
                                .ToString("N0"),

                        Url =
                            "/Admin/Financials/Manage?financeTab=commissions&CommissionStatus=Debt&HasDebt=true"
                    });
            }
        }


        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Settings.View)]
        public IActionResult SystemSettings()
        {
            return View();
        }

        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Dashboard.View)]
        public async Task<IActionResult> GlobalSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new { success = false, results = Array.Empty<object>() });
            }

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var currentAdminUserId) || currentAdminUserId <= 0)
            {
                return Forbid();
            }

            var permissionKeys = await _permissionService.GetPermissionKeysAsync(
                currentAdminUserId,
                HttpContext.RequestAborted);

            bool HasPermission(string key) => permissionKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
            string cleanQuery = query.Trim().ToLower();
            var allResults = new List<object>();

            if (HasPermission(AdminPermissionKeys.MasterData.View))
            {
                var categories = await _context.Categories
                    .Where(c => c.CategoryName.ToLower().Contains(cleanQuery))
                    .Select(c => new
                    {
                        id = c.CategoryId,
                        title = c.CategoryName,
                        type = "فئة",
                        icon = "bi-tag-fill text-primary",
                        url = "/Admin/MasterEntities/MasterEntitiesManagement"
                    })
                    .Take(3)
                    .ToListAsync();

                allResults.AddRange(categories.Cast<object>());
            }

            if (HasPermission(AdminPermissionKeys.Users.View))
            {
                var users = await _context.Users
                    .Where(u =>
                        u.FullName.ToLower().Contains(cleanQuery) ||
                        u.Username.ToLower().Contains(cleanQuery))
                    .Select(u => new
                    {
                        id = u.UserId,
                        title = u.FullName,
                        type = "مستخدم",
                        icon = "bi-person-fill text-success",
                        url = "/Admin/Users/UserManagement"
                    })
                    .Take(3)
                    .ToListAsync();

                allResults.AddRange(users.Cast<object>());
            }

            if (HasPermission(AdminPermissionKeys.Places.View))
            {
                var places = await _context.Places
                    .Where(p => p.PlaceNameAndDesc.ToLower().Contains(cleanQuery))
                    .Select(p => new
                    {
                        id = p.PlaceId,
                        title = p.PlaceNameAndDesc,
                        type = "مكان",
                        icon = "bi-building-fill text-warning",
                        url = "/Admin/Places/PlaceManagement"
                    })
                    .Take(4)
                    .ToListAsync();

                allResults.AddRange(places.Cast<object>());
            }

            return Json(new { success = true, results = allResults });
        }
    }
}
