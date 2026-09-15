using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;
using YemenGuide.Core.Entities;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services;
using YemenGuide.Web.Services.Finance;
using YemenGuide.Web.Services.Notifications;
using YemenGuide.Core.DTOs.Notifications;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Globalization;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PlacesController : Controller
    {
        private const decimal FallbackSanaaUsdRate = 537m;
        private const decimal FallbackSanaaSarRate = 140m;
        private const decimal FallbackAdenUsdRate = 1900m;
        private const decimal FallbackAdenSarRate = 500m;

        private static bool IsArabic =>
            string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "ar",
                StringComparison.OrdinalIgnoreCase);

        private static string L(string ar, string en) =>
            IsArabic ? ar : en;

        private readonly AppDbContext _db;
        private readonly CurrencyService _cur;
        private readonly ICommissionFinanceService _commissionFinanceService;

        public PlacesController(
            AppDbContext db,
            CurrencyService cur,
            ICommissionFinanceService commissionFinanceService)
        {
            _db = db;
            _cur = cur;
            _commissionFinanceService = commissionFinanceService;
        }

        public class PlaceActionDto { public int Id { get; set; } public string? Reason { get; set; } }

        [RequireAdminPermission(AdminPermissionKeys.Places.View)]
        public async Task<IActionResult> PlaceManagement()
        {
            var cancellationToken = HttpContext.RequestAborted;

            var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentAdminId = int.TryParse(adminIdClaim, out var parsedAdminId)
                ? parsedAdminId
                : 0;

            // نبقي حساب التشغيل القديم للمنشآت السابقة فقط حتى لا تختفي بيانات موجودة،
            // أما الإضافات الجديدة من خيار «حسابي الإداري» فتُربط بحساب الأدمن نفسه.
            int? platformOwnerId = await _db.SystemSettings
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => s.PlatformOwnerUserId)
                .FirstOrDefaultAsync(cancellationToken);

            // تصحيح تلقائي للبيانات القديمة: منشأة الأدمن الحجزية لا تُعد نشطة
            // قبل وجود السياسات. هذا يعالج المنشآت التي أُنشئت قبل هذا التعديل
            // وكانت محفوظة Active مع IsAvailable=false.
            if (currentAdminId > 0)
            {
                var incompleteAdminPlaces = await _db.Places
                    .Where(p =>
                        p.OwnerId == currentAdminId &&
                        p.Status == "Active" &&
                        p.Category != null &&
                        p.Category.BookingSystemId >= 1 &&
                        p.Category.BookingSystemId <= 4 &&
                        !_db.PlacePolicies.Any(policy => policy.PlaceId == p.PlaceId))
                    .ToListAsync(cancellationToken);

                if (incompleteAdminPlaces.Count > 0)
                {
                    foreach (var place in incompleteAdminPlaces)
                    {
                        place.Status = "Pending";
                        place.IsAvailable = false;
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                }

                // إذا كانت السياسات قد حُفظت بالفعل لمنشأة يملكها حساب Admin
                // فلا يوجد اعتماد إداري ثانٍ: تصبح نشطة ومتاحة للعميل مباشرة.
                // هذا يصلح أيضًا أي منشأة بقيت Pending من تشغيل سابق قبل تركيب الإصلاح.
                var readyAdminPlaces = await _db.Places
                    .Where(p =>
                        p.OwnerId == currentAdminId &&
                        p.Status == "Pending" &&
                        p.Category != null &&
                        p.Category.BookingSystemId >= 1 &&
                        p.Category.BookingSystemId <= 4 &&
                        _db.PlacePolicies.Any(policy => policy.PlaceId == p.PlaceId))
                    .ToListAsync(cancellationToken);

                if (readyAdminPlaces.Count > 0)
                {
                    foreach (var place in readyAdminPlaces)
                    {
                        place.Status = "Active";
                        place.IsAvailable = true;
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            /*
             * PlacePolicy محمّل أصلًا مع المنشأة، لذلك لا نعمل Query منفصلة
             * لجلب أرقام المنشآت التي لديها سياسات. هذا يقلل تحميل الصفحة.
             */
            var places = await _db.Places
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.City)
                .Include(p => p.Owner)
                .Include(p => p.PlacePolicy)
                .ToListAsync(cancellationToken);

            return View(new PlacesDto
            {
                PendingPlaces = places.Where(p => p.Status == "Pending" &&
                    (p.Category == null ||
                     p.Category.BookingSystemId < 1 ||
                     p.Category.BookingSystemId > 4 ||
                     p.PlacePolicy != null)
                ).ToList(),

                ActivePlaces = places.Where(p => p.Status == "Active").ToList(),
                RejectedPlaces = places.Where(p => p.Status == "Rejected").ToList(),
                SuspendedPlaces = places.Where(p => p.Status == "Suspended").ToList(),

                // تظهر للأدمن منشآته المرتبطة بحسابه مباشرة، مع إبقاء منشآت
                // حساب التشغيل القديم ظاهرة للتوافق مع البيانات السابقة.
                AdminPlaces = places
                    .Where(p =>
                        (currentAdminId > 0 && p.OwnerId == currentAdminId) ||
                        (platformOwnerId.HasValue && p.OwnerId == platformOwnerId.Value))
                    .ToList(),

                WaitingOwnerApprovalPlaces = places.Where(p => p.Status == "WaitOwner").ToList()
            });
        }


        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Places.Edit)]
        public async Task<IActionResult> ManageOwnPlace(int id)
        {
            var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminIdClaim, out int currentAdminId) || currentAdminId <= 0)
                return Forbid();

            bool ownsPlace = await _db.Places
                .AsNoTracking()
                .AnyAsync(place =>
                    place.PlaceId == id &&
                    place.OwnerId == currentAdminId);

            if (!ownsPlace)
                return NotFound();

            string cookieName = $"ownerActivePlace_{currentAdminId}";
            Response.Cookies.Append(
                cookieName,
                id.ToString(CultureInfo.InvariantCulture),
                new Microsoft.AspNetCore.Http.CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Path = "/"
                });

            return RedirectToAction(
                "ManagePlaces",
                "Place",
                new { area = "Owner" });
        }

        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Places.Create)]
        public async Task<IActionResult> Create()
        {
            var settings = await _db.SystemSettings
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            User? platformOwner = null;
            if (settings?.PlatformOwnerUserId is int platformOwnerId && platformOwnerId > 0)
            {
                platformOwner = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.UserId == platformOwnerId &&
                        u.Role == SystemRoles.Owner &&
                        u.AccountState == "Active");
            }

            ViewBag.Categories = await _db.Categories
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Cities = await _db.Cities
                .AsNoTracking()
                .ToListAsync();

            int? excludedPlatformOwnerId = platformOwner?.UserId;
            ViewBag.Owners = await _db.Users
                .AsNoTracking()
                .Where(u =>
                    u.Role == SystemRoles.Owner &&
                    u.AccountState == "Active" &&
                    (!excludedPlatformOwnerId.HasValue || u.UserId != excludedPlatformOwnerId.Value))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            User? currentAdmin = null;

            if (int.TryParse(adminIdClaim, out int adminId) && adminId > 0)
            {
                currentAdmin = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.UserId == adminId &&
                        u.Role == SystemRoles.Admin);
            }

            if (currentAdmin == null)
                return Forbid();

            ViewBag.CurrentAdminName = currentAdmin.FullName ?? currentAdmin.Username ?? "حساب الإدارة الحالي";
            ViewBag.CurrentAdminUsername = currentAdmin.Username ?? "";
            ViewBag.CurrentAdminEmail = currentAdmin.ContactInfo ?? "";
            ViewBag.CurrentAdminPhone = currentAdmin.PhoneNumber ?? "";
            ViewBag.CurrentAdminJobTitle = currentAdmin.JobTitle ?? "إدارة النظام";

            // هذه القيم تخص المسار القديم فقط حتى يبقى خيار إنشاء حساب تشغيل
            // داخل «مالك جديد» كما كان، بدون أن يعتمد عليه خيار حسابي الإداري.
            ViewBag.PlatformOwnerExists = platformOwner != null;
            ViewBag.PlatformOwnerConfigured = settings?.PlatformOwnerUserId.HasValue == true;
            ViewBag.PlatformOwnerName = platformOwner?.FullName ?? "";
            ViewBag.PlatformOwnerUsername = platformOwner?.Username ?? "";
            ViewBag.PlatformOwnerEmail = platformOwner?.ContactInfo ?? "";

            return View();
        }

        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Places.Create)]
        public async Task<IActionResult> CheckOwnerAccountAvailability(string? username, string? email)
        {
            string normalizedUsername = (username ?? string.Empty).Trim();
            string normalizedEmail = (email ?? string.Empty).Trim();

            bool usernameAvailable = true;
            bool emailAvailable = true;

            if (!string.IsNullOrWhiteSpace(normalizedUsername))
            {
                usernameAvailable = !await _db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Username == normalizedUsername);
            }

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                emailAvailable = !await _db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.ContactInfo == normalizedEmail);
            }

            return Json(new
            {
                success = true,
                usernameAvailable,
                emailAvailable
            });
        }

        [RequireAdminPermission(AdminPermissionKeys.Places.View)]
        public async Task<IActionResult> GetPlaceDetails(int id)
        {
            try
            {
                var cancellationToken =
                    HttpContext.RequestAborted;

                /*
                 * AsSplitQuery مهم هنا لأن التفاصيل تحمل عدة Collections
                 * (صور + مرافق + باقات + خدمات + غرف + وحدات).
                 * يمنع Cartesian Explosion ويجعل فتح التفاصيل أخف.
                 */
                var p = await _db.Places
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(x => x.Category)
                    .Include(x => x.City)
                    .Include(x => x.Owner)
                    .Include(x => x.PlaceImages)
                    .Include(x => x.PlaceAmenities)
                    .Include(x => x.PlacePackages)
                    .Include(x => x.PlaceAdditionalServices)
                    .Include(x => x.Rooms)
                        .ThenInclude(r => r.RoomUnits)
                    .Include(x => x.PlacePolicy)
                    .FirstOrDefaultAsync(
                        x => x.PlaceId == id,
                        cancellationToken);

                if (p == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = L(
                            "المنشأة غير موجودة.",
                            "The place was not found.")
                    });
                }

                /*
                 * عملة العرض تأتي من بروفايل المدير أولًا.
                 * لو لا توجد قيمة صالحة نرجع لعملة النظام ثم الريال اليمني.
                 * أسعار الصرف تُقرأ مرة واحدة فقط لهذا المودل.
                 */
                string? userCurrency = null;

                string? userIdText =
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                if (int.TryParse(
                        userIdText,
                        out int currentUserId) &&
                    currentUserId > 0)
                {
                    userCurrency =
                        await _db.Users
                            .AsNoTracking()
                            .Where(u =>
                                u.UserId ==
                                currentUserId)
                            .Select(u =>
                                u.PreferredCurrency)
                            .FirstOrDefaultAsync(
                                cancellationToken);
                }

                var currencySettings =
                    await _db.SystemSettings
                        .AsNoTracking()
                        .OrderBy(s => s.Id)
                        .Select(s => new
                        {
                            s.DefaultCurrency,
                            s.SanaaUsdRate,
                            s.SanaaSarRate,
                            s.AdenUsdRate,
                            s.AdenSarRate
                        })
                        .FirstOrDefaultAsync(
                            cancellationToken);

                string displayCurrency =
                    CurrencyDisplayService
                        .NormalizeCurrency(
                            userCurrency);

                if (string.IsNullOrWhiteSpace(
                        displayCurrency))
                {
                    displayCurrency =
                        CurrencyDisplayService
                            .NormalizeCurrency(
                                currencySettings
                                    ?.DefaultCurrency);
                }

                if (string.IsNullOrWhiteSpace(
                        displayCurrency))
                {
                    displayCurrency =
                        CurrencyDisplayService
                            .YemeniRial;
                }

                bool useAdenMarket =
                    string.Equals(
                        p.City?.ExchangeRateRegion,
                        ExchangeRateRegionResolver
                            .AdenMarket,
                        StringComparison
                            .OrdinalIgnoreCase);

                decimal usdRate =
                    useAdenMarket
                        ? currencySettings
                              ?.AdenUsdRate
                          ?? FallbackAdenUsdRate
                        : currencySettings
                              ?.SanaaUsdRate
                          ?? FallbackSanaaUsdRate;

                decimal sarRate =
                    useAdenMarket
                        ? currencySettings
                              ?.AdenSarRate
                          ?? FallbackAdenSarRate
                        : currencySettings
                              ?.SanaaSarRate
                          ?? FallbackSanaaSarRate;

                if (usdRate <= 0m)
                {
                    usdRate =
                        useAdenMarket
                            ? FallbackAdenUsdRate
                            : FallbackSanaaUsdRate;
                }

                if (sarRate <= 0m)
                {
                    sarRate =
                        useAdenMarket
                            ? FallbackAdenSarRate
                            : FallbackSanaaSarRate;
                }

                decimal divisor =
                    displayCurrency switch
                    {
                        CurrencyDisplayService
                            .UsDollar =>
                                usdRate,

                        CurrencyDisplayService
                            .SaudiRiyal =>
                                sarRate,

                        _ => 1m
                    };

                decimal ConvertYer(object? value)
                {
                    decimal amount =
                        value == null
                            ? 0m
                            : Convert.ToDecimal(value);

                    if (displayCurrency ==
                        CurrencyDisplayService
                            .YemeniRial)
                    {
                        return Math.Round(
                            amount,
                            0,
                            MidpointRounding
                                .AwayFromZero);
                    }

                    if (divisor <= 0m)
                    {
                        return amount;
                    }

                    return Math.Round(
                        amount / divisor,
                        2,
                        MidpointRounding
                            .AwayFromZero);
                }

                string FormatMoney(object? value) =>
                    _cur.FormatAmount(
                        ConvertYer(value),
                        displayCurrency);

                bool fixedPenalty =
                    string.Equals(
                        p.PlacePolicy?.PenaltyType,
                        "DeductFixed",
                        StringComparison.OrdinalIgnoreCase);

                decimal penaltyValue =
                    fixedPenalty
                        ? ConvertYer(
                            p.PlacePolicy
                                ?.PenaltyValue)
                        : Convert.ToDecimal(
                            p.PlacePolicy
                                ?.PenaltyValue ?? 0m);

                string? penaltyValueText =
                    fixedPenalty
                        ? FormatMoney(
                            p.PlacePolicy
                                ?.PenaltyValue)
                        : null;

                return Json(new
                {
                    placeId = p.PlaceId,
                    name = p.PlaceNameAndDesc,
                    description =
                        p.Description ??
                        L("لا يوجد", "None"),
                    address =
                        p.AddressDetail ?? "",
                    isAvailable =
                        p.IsAvailable == true,

                    phone =
                        !string.IsNullOrEmpty(
                            p.PhoneNumber)
                            ? p.PhoneNumber
                            : (
                                !string.IsNullOrEmpty(
                                    p.Owner
                                        ?.PhoneNumber)
                                    ? p.Owner
                                        .PhoneNumber
                                    : (
                                        p.Owner
                                            ?.ContactInfo
                                        ?? L(
                                            "غير متوفر",
                                            "Unavailable")
                                    )
                            ),

                    category =
                        p.Category?.CategoryName ??
                        L(
                            "غير محدد",
                            "Unspecified"),

                    bookingSystemId =
                        p.Category
                            ?.BookingSystemId
                        ?? 0,

                    city =
                        p.City?.CityName ??
                        L(
                            "غير محدد",
                            "Unspecified"),

                    priceValue =
                        ConvertYer(
                            p.Price),

                    price =
                        FormatMoney(
                            p.Price),

                    currency = new
                    {
                        name =
                            displayCurrency,

                        code =
                            CurrencyDisplayService
                                .GetCurrencyCode(
                                    displayCurrency),

                        symbol =
                            CurrencyDisplayService
                                .GetCurrencySymbol(
                                    displayCurrency),

                        divisor
                    },

                    status = p.Status,

                    owner =
                        p.Owner?.FullName ??
                        L(
                            "لا يوجد مالك",
                            "No owner"),

                    coordinates =
                        p.Coordinates,

                    rejectionReason =
                        p.RejectionReason ??
                        "",

                    coverImage =
                        p.CoverImage ??
                        "/images/placeholder.jpg",

                    images =
                        p.PlaceImages
                            .Select(i =>
                                i.ImagePath)
                            .ToList(),

                    amenities =
                        p.PlaceAmenities
                            .Select(a => new
                            {
                                name =
                                    a.AmenityName,

                                icon =
                                    a.IconClass ??
                                    "bi-patch-check-fill"
                            })
                            .ToList(),

                    packages =
                        p.PlacePackages
                            .Select(k => new
                            {
                                name =
                                    k.PackageName,

                                price =
                                    ConvertYer(
                                        k.Price),

                                priceText =
                                    FormatMoney(
                                        k.Price),

                                capacity =
                                    k.Capacity,

                                time =
                                    k.TimeFrame,

                                startTime =
                                    k.StartTime,

                                endTime =
                                    k.EndTime,

                                isActive =
                                    k.IsActive ?? true
                            })
                            .ToList(),

                    services =
                        p.PlaceAdditionalServices
                            .Select(s => new
                            {
                                name =
                                    s.ServiceName,

                                price =
                                    ConvertYer(
                                        s.Price),

                                priceText =
                                    FormatMoney(
                                        s.Price),

                                description =
                                    s.Description,

                                image =
                                    s.ImageUrl
                            })
                            .ToList(),

                    rooms =
                        p.Rooms
                            .Select(r => new
                            {
                                roomId =
                                    r.RoomId,

                                name =
                                    r.RoomType +
                                    " (" +
                                    r.RoomNumber +
                                    ")",

                                roomType =
                                    r.RoomType,

                                displayName =
                                    r.RoomNumber,

                                price =
                                    ConvertYer(
                                        r.Price),

                                priceText =
                                    FormatMoney(
                                        r.Price),

                                qty =
                                    r.RoomUnits.Any()
                                        ? r.RoomUnits.Count
                                        : r.Quantity,

                                areaSquareMeters =
                                    r.AreaSquareMeters,

                                maxAdults =
                                    r.MaxAdults,

                                maxChildren =
                                    r.MaxChildren,

                                bedsCount =
                                    r.BedsCount,

                                bedType =
                                    r.BedType,

                                internalRoomsCount =
                                    r.InternalRoomsCount,

                                isSuite =
                                    (
                                        r.InternalRoomsCount
                                        ?? 0
                                    ) > 0 ||
                                    (
                                        !string.IsNullOrWhiteSpace(
                                            r.RoomType) &&
                                        r.RoomType.Contains(
                                            "جناح")
                                    ),

                                desc =
                                    r.Description,

                                imageUrl =
                                    r.ImageUrl,

                                units =
                                    r.RoomUnits
                                        .OrderBy(unit =>
                                            unit.FloorNumber)
                                        .ThenBy(unit =>
                                            unit.RoomNumber)
                                        .Select(unit =>
                                            new
                                            {
                                                roomUnitId =
                                                    unit.RoomUnitId,

                                                roomNumber =
                                                    unit.RoomNumber,

                                                floorNumber =
                                                    unit.FloorNumber,

                                                floorLabel =
                                                    unit.FloorLabel,

                                                isAvailable =
                                                    unit.IsAvailable,

                                                notes =
                                                    unit.Notes
                                            })
                                        .ToList()
                            })
                            .ToList(),

                    policy =
                        p.PlacePolicy != null
                            ? new
                            {
                                operationMode =
                                    p.PlacePolicy
                                        .OperationSystemMode,

                                checkIn =
                                    p.PlacePolicy
                                        .CheckInTime,

                                checkOut =
                                    p.PlacePolicy
                                        .CheckOutTime,

                                freeCancelHours =
                                    p.PlacePolicy
                                        .FreeCancellationHours,

                                penaltyType =
                                    p.PlacePolicy
                                        .PenaltyType,

                                penaltyValue,

                                penaltyValueText,

                                depositJson =
                                    p.PlacePolicy
                                        .DepositPoliciesJson,

                                securityJson =
                                    p.PlacePolicy
                                        .SecurityPoliciesJson,

                                paymentTimeout =
                                    p.PlacePolicy
                                        .PaymentTimeout,

                                shiftsJson =
                                    p.PlacePolicy
                                        .ShiftsJson
                            }
                            : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message = L(
                            "تعذر تحميل تفاصيل المنشأة.",
                            "Unable to load place details."),
                        detail = ex.Message
                    });
            }
        }



        // دالة مساعدة لتحديث حالة الأماكن
        private async Task<IActionResult> UpdateStatus(int id, string status, string? reason, string msg)
        {
            var p = await _db.Places.FindAsync(id);
            if (p == null) return Json(new { success = false, message = L("لم يتم العثور على المنشأة.", "The place was not found.") });

            var previousStatus = p.Status?.Trim() ?? string.Empty;

            if (status == "Deleted")
            {
                _db.Places.Remove(p);
            }
            else
            {
                if (status == "Active")
                {
                    var ownerState = await _db.Users
                        .AsNoTracking()
                        .Where(user => user.UserId == p.OwnerId)
                        .Select(user => new
                        {
                            user.Role,
                            user.AccountState
                        })
                        .FirstOrDefaultAsync();

                    bool isAdminOwned =
                        ownerState != null &&
                        string.Equals(
                            ownerState.Role,
                            SystemRoles.Admin,
                            StringComparison.OrdinalIgnoreCase);

                    bool isApprovedOwner =
                        ownerState != null &&
                        string.Equals(
                            ownerState.Role,
                            SystemRoles.Owner,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            ownerState.AccountState,
                            "Active",
                            StringComparison.OrdinalIgnoreCase);

                    if (!isAdminOwned && !isApprovedOwner)
                    {
                        p.Status = previousStatus;
                        p.IsAvailable = false;

                        return Json(new
                        {
                            success = false,
                            message = L(
                                "لا يمكن اعتماد المنشأة قبل اكتمال تفعيل حساب المالك. اعتماد المالك واعتماد المنشأة خطوتان منفصلتان.",
                                "The place cannot be approved before the owner's account is fully active. Owner approval and place approval are separate steps.")
                        });
                    }
                }

                p.Status = status;
                if (reason != null) p.RejectionReason = reason;

                if (status == "Active")
                {
                    int bookingSystemId = await _db.Categories
                        .Where(c => c.CategoryId == p.CategoryId)
                        .Select(c => c.BookingSystemId)
                        .FirstOrDefaultAsync();

                    bool isBookable = bookingSystemId >= 1 && bookingSystemId <= 4;
                    bool hasPolicies = !isBookable || await _db.PlacePolicies
                        .AnyAsync(policy => policy.PlaceId == p.PlaceId);

                    if (!hasPolicies)
                    {
                        p.Status = previousStatus;
                        p.IsAvailable = false;

                        return Json(new
                        {
                            success = false,
                            message = L(
                                "لا يمكن تفعيل المنشأة قبل إكمال سياسات الحجز.",
                                "The place cannot be activated before booking policies are completed.")
                        });
                    }

                    p.IsAvailable = true;
                    p.RejectionReason = null;
                }
                else if (
                    status == "Rejected" ||
                    status == "Suspended" ||
                    status == "Pending" ||
                    status == "WaitOwner" ||
                    status == "UnderReview")
                {
                    p.IsAvailable = false;
                }
            }

            await _db.SaveChangesAsync();

            /*
             * تبدأ تجربة المالك فقط عند الاعتماد الأول لمنشأة أصبحت جاهزة
             * فعليًا للحجز. إعادة تنشيط منشأة معلقة لا تعيد تشغيل التجربة.
             */
            bool firstOperationalApproval =
                status == "Active" &&
                p.IsAvailable == true &&
                (string.Equals(previousStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(previousStatus, "WaitOwner", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(previousStatus, "UnderReview", StringComparison.OrdinalIgnoreCase));

            if (firstOperationalApproval)
            {
                var trialResult =
                    await _commissionFinanceService
                        .TryStartOwnerFreeTrialForPlaceAsync(p.PlaceId);

                if (!trialResult.Success)
                {
                    // اعتماد المنشأة لا يُلغى بسبب خطأ جانبي في تشغيل التجربة؛
                    // بوابة الحجز المركزية ستحاول تهيئة التجربة مرة أخرى بأمان.
                    msg = string.Concat(
                        msg,
                        " تم اعتماد المنشأة، وسيعاد التحقق من الفترة التجريبية تلقائيًا عند أول عملية حجز.");
                }
            }

            /*
             * إشعار المالك جزء من قرار الحالة نفسه. لا نرسل إشعارًا لمنشآت
             * حساب الأدمن، ولا ندّعي إرسال السبب دون إنشاء إشعار فعلي.
             */
            if (status != "Deleted" && p.OwnerId.HasValue && p.OwnerId.Value > 0)
            {
                var recipient = await _db.Users
                    .AsNoTracking()
                    .Where(user => user.UserId == p.OwnerId.Value)
                    .Select(user => new { user.Role })
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

                if (recipient != null &&
                    string.Equals(recipient.Role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase))
                {
                    string notificationTitle;
                    string notificationText;
                    string notificationType;

                    if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        notificationTitle = "تم رفض المنشأة";
                        notificationText =
                            $"تم رفض منشأة «{p.PlaceNameAndDesc}». السبب: {p.RejectionReason ?? "لم يُذكر السبب"}.";
                        notificationType = NotificationTypes.AdminPlaceActionRequired;
                    }
                    else if (string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase))
                    {
                        notificationTitle = "تم تعليق المنشأة";
                        notificationText =
                            $"تم تعليق منشأة «{p.PlaceNameAndDesc}». السبب: {p.RejectionReason ?? "لم يُذكر السبب"}.";
                        notificationType = NotificationTypes.General;
                    }
                    else if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        notificationTitle =
                            string.Equals(previousStatus, "Rejected", StringComparison.OrdinalIgnoreCase)
                                ? "تمت إعادة قبول المنشأة"
                                : "تم اعتماد المنشأة";
                        notificationText =
                            $"أصبحت منشأة «{p.PlaceNameAndDesc}» نشطة ومتاحة وفق إعداداتها الحالية.";
                        notificationType = NotificationTypes.Account;
                    }
                    else
                    {
                        notificationTitle = string.Empty;
                        notificationText = string.Empty;
                        notificationType = NotificationTypes.General;
                    }

                    if (!string.IsNullOrWhiteSpace(notificationTitle))
                    {
                        var notificationService = HttpContext.RequestServices
                            .GetRequiredService<INotificationService>();

                        notificationService.Queue(new CreateNotificationDto
                        {
                            UserId = p.OwnerId.Value,
                            NotificationTitle = notificationTitle,
                            NotificationText = notificationText,
                            NotificationType = notificationType,
                            TargetUrl =
                                $"/Owner/Place/ManagePlaces?placeTab=place-list-tab&focusPlace={p.PlaceId}",
                            ReferenceType = NotificationReferenceTypes.Place,
                            ReferenceId = p.PlaceId
                        });

                        await _db.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }
            }

            return Json(new { success = true, message = msg });
        }

        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.Review)]
        public Task<IActionResult> ApprovePlace([FromBody] int id) =>
            UpdateStatus(id, "Active", null, L("تم اعتماد المنشأة بنجاح.", "The place was approved successfully."));
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.Review)]
        public async Task<IActionResult> RejectPlace([FromBody] PlaceActionDto m)
        {
            var reason = m?.Reason?.Trim();
            if (m == null || m.Id <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
            {
                return Json(new
                {
                    success = false,
                    message = L(
                        "يجب كتابة سبب رفض واضح لا يقل عن 5 أحرف.",
                        "A clear rejection reason of at least 5 characters is required.")
                });
            }

            return await UpdateStatus(
                m.Id,
                "Rejected",
                reason,
                L(
                    "تم رفض المنشأة وإرسال السبب للمالك.",
                    "The place was rejected and the reason was sent to the owner."));
        }

        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.Review)]
        public async Task<IActionResult> RestoreRejectedPlace([FromBody] int id)
        {
            if (id <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = L("رقم المنشأة غير صحيح.", "The place id is invalid.")
                });
            }

            var currentStatus = await _db.Places
                .AsNoTracking()
                .Where(place => place.PlaceId == id)
                .Select(place => place.Status)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (currentStatus == null)
            {
                return Json(new
                {
                    success = false,
                    message = L("لم يتم العثور على المنشأة.", "The place was not found.")
                });
            }

            if (!string.Equals(currentStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = false,
                    message = L(
                        "إعادة القبول متاحة للمنشآت المرفوضة فقط.",
                        "Only rejected places can be restored.")
                });
            }

            return await UpdateStatus(
                id,
                "Active",
                null,
                L(
                    "تمت إعادة قبول المنشأة وتفعيلها وإشعار المالك.",
                    "The place was restored, activated, and its owner was notified."));
        }

        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.ChangeStatus)]
        public async Task<IActionResult> SuspendPlace([FromBody] PlaceActionDto m)
        {
            var reason = m?.Reason?.Trim();
            if (m == null || m.Id <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
            {
                return Json(new
                {
                    success = false,
                    message = L(
                        "يجب كتابة سبب تعليق واضح لا يقل عن 5 أحرف.",
                        "A clear suspension reason of at least 5 characters is required.")
                });
            }

            return await UpdateStatus(
                m.Id,
                "Suspended",
                reason,
                L("تم تعليق المنشأة وإشعار المالك.", "The place was suspended and its owner was notified."));
        }
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.Delete)]
        public Task<IActionResult> DeletePlace([FromBody] int id) =>
            UpdateStatus(id, "Deleted", null, L("تم حذف المنشأة نهائيًا.", "The place was permanently deleted."));
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.ChangeStatus)]
        public Task<IActionResult> ActivatePlace([FromBody] int id) =>
            UpdateStatus(id, "Active", null, L("تمت إعادة تنشيط المنشأة بنجاح.", "The place was reactivated successfully."));


        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.Create)]
        public async Task<IActionResult> StartSetup(
            int CategoryId,
            string OwnerMode,
            string? NewFirstName,
            string? NewLastName,
            string? NewOwnerUsername,
            string? NewEmail,
            string? NewOwnerPassword,
            string? ConfirmOwnerPassword,
            bool SetAsPlatformOwner,
            int? ExistingOwnerId)
        {
            var category = await _db.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryId == CategoryId);

            if (category == null)
            {
                TempData["ErrorMessage"] = "الفئة المحددة غير موجودة.";
                return RedirectToAction(nameof(Create));
            }

            string normalizedOwnerMode = (OwnerMode ?? string.Empty).Trim().ToLowerInvariant();
            int? targetOwnerId = null;

            switch (normalizedOwnerMode)
            {
                case "admin":
                    {
                        // المطلوب هنا هو نفس حساب الأدمن المسجل حاليًا، وليس حساب Owner وسيط.
                        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!int.TryParse(adminIdClaim, out int adminId) || adminId <= 0)
                        {
                            TempData["ErrorMessage"] = "تعذر تحديد حساب الإدارة الحالي.";
                            return RedirectToAction(nameof(Create));
                        }

                        var currentAdmin = await _db.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u =>
                                u.UserId == adminId &&
                                u.Role == SystemRoles.Admin);

                        if (currentAdmin == null)
                        {
                            TempData["ErrorMessage"] = "حساب الإدارة الحالي غير موجود أو لم يعد يملك دور Admin.";
                            return RedirectToAction(nameof(Create));
                        }

                        targetOwnerId = currentAdmin.UserId;
                        TempData["OwnerMode"] = "admin";
                        break;
                    }

                case "existing":
                    {
                        if (!ExistingOwnerId.HasValue || ExistingOwnerId.Value <= 0)
                        {
                            TempData["ErrorMessage"] = "يرجى اختيار مالك موجود من القائمة.";
                            return RedirectToAction(nameof(Create));
                        }

                        var existingOwner = await _db.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u =>
                                u.UserId == ExistingOwnerId.Value &&
                                u.Role == SystemRoles.Owner &&
                                u.AccountState == "Active");

                        if (existingOwner == null)
                        {
                            TempData["ErrorMessage"] = "المالك المحدد غير موجود أو غير نشط.";
                            return RedirectToAction(nameof(Create));
                        }

                        targetOwnerId = existingOwner.UserId;
                        TempData["OwnerMode"] = "existing";
                        break;
                    }

                case "new":
                    {
                        string firstName = (NewFirstName ?? string.Empty).Trim();
                        string lastName = (NewLastName ?? string.Empty).Trim();
                        string username = (NewOwnerUsername ?? string.Empty).Trim();
                        string email = (NewEmail ?? string.Empty).Trim();
                        string password = NewOwnerPassword ?? string.Empty;
                        string confirmPassword = ConfirmOwnerPassword ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(firstName) ||
                            string.IsNullOrWhiteSpace(lastName) ||
                            string.IsNullOrWhiteSpace(username) ||
                            string.IsNullOrWhiteSpace(email) ||
                            string.IsNullOrWhiteSpace(password) ||
                            string.IsNullOrWhiteSpace(confirmPassword))
                        {
                            TempData["ErrorMessage"] = "يرجى استكمال جميع بيانات حساب المالك الجديد.";
                            return RedirectToAction(nameof(Create));
                        }

                        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                        {
                            TempData["ErrorMessage"] = "عذراً، يرجى إدخال بريد إلكتروني صحيح.";
                            return RedirectToAction(nameof(Create));
                        }

                        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_.-]+$"))
                        {
                            TempData["ErrorMessage"] = "اسم المستخدم يجب أن يكون باللغة الإنجليزية وبدون مسافات.";
                            return RedirectToAction(nameof(Create));
                        }

                        if (password.Length < 6)
                        {
                            TempData["ErrorMessage"] = "كلمة المرور يجب ألا تقل عن 6 أحرف.";
                            return RedirectToAction(nameof(Create));
                        }

                        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
                        {
                            TempData["ErrorMessage"] = "كلمة المرور وتأكيد كلمة المرور غير متطابقين.";
                            return RedirectToAction(nameof(Create));
                        }

                        bool duplicateOwner = await _db.Users.AnyAsync(u =>
                            u.Username == username ||
                            u.ContactInfo == email);

                        if (duplicateOwner)
                        {
                            TempData["ErrorMessage"] = "اسم المستخدم أو البريد الإلكتروني مستخدم بالفعل.";
                            return RedirectToAction(nameof(Create));
                        }

                        if (SetAsPlatformOwner)
                        {
                            var settings = await _db.SystemSettings
                                .OrderBy(s => s.Id)
                                .FirstOrDefaultAsync();

                            if (settings == null)
                            {
                                TempData["ErrorMessage"] =
                                    "تعذر العثور على سجل إعدادات النظام. يجب وجود سجل واحد في جدول SystemSettings.";
                                return RedirectToAction(nameof(Create));
                            }

                            User? currentPlatformOwner = null;

                            if (settings.PlatformOwnerUserId.HasValue)
                            {
                                currentPlatformOwner = await _db.Users
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(u =>
                                        u.UserId == settings.PlatformOwnerUserId.Value &&
                                        u.Role == SystemRoles.Owner &&
                                        u.AccountState == "Active");
                            }

                            if (currentPlatformOwner != null)
                            {
                                TempData["ErrorMessage"] =
                                    $"حساب تشغيل منشآت الإدارة مجهز بالفعل باسم: {currentPlatformOwner.FullName}.";
                                return RedirectToAction(nameof(Create));
                            }

                            await using var transaction = await _db.Database.BeginTransactionAsync();

                            try
                            {
                                var platformOwner = new User
                                {
                                    FullName = $"{firstName} {lastName}".Trim(),
                                    Username = username,
                                    ContactInfo = email,
                                    PasswordHash = HashPassword(password),
                                    Role = SystemRoles.Owner,
                                    CreatedAt = DateTime.Now,
                                    PreferredLanguage = "ar",
                                    AccountState = "Active",
                                    IsDarkMode = false,
                                    IsOnline = false,
                                    WarningCount = 0,
                                    IsEmailVerified = false
                                };

                                _db.Users.Add(platformOwner);
                                await _db.SaveChangesAsync();

                                settings.PlatformOwnerUserId = platformOwner.UserId;
                                await _db.SaveChangesAsync();

                                await transaction.CommitAsync();

                                targetOwnerId = platformOwner.UserId;
                                TempData["OwnerMode"] = "admin";
                            }
                            catch
                            {
                                await transaction.RollbackAsync();
                                TempData["ErrorMessage"] =
                                    "تعذر إنشاء حساب تشغيل منشآت الإدارة. لم يتم حفظ أي بيانات، حاول مرة أخرى.";
                                return RedirectToAction(nameof(Create));
                            }
                        }
                        else
                        {
                            // المالك العادي يُنشأ عند الحفظ النهائي داخل AddFullPlace
                            // حتى يبقى إنشاء الحساب والمنشأة في معاملة واحدة.
                            TempData["OwnerMode"] = "new";
                            TempData["NewFirstName"] = firstName;
                            TempData["NewLastName"] = lastName;
                            TempData["NewOwnerUsername"] = username;
                            TempData["NewEmail"] = email;
                            TempData["NewOwnerPassword"] = password;
                        }

                        break;
                    }

                default:
                    TempData["ErrorMessage"] = "يرجى تحديد طريقة صحيحة لربط مالك المنشأة.";
                    return RedirectToAction(nameof(Create));
            }

            bool isBookable = category.BookingSystemId >= 1 && category.BookingSystemId <= 4;
            bool isAdministrationOperation =
                string.Equals(normalizedOwnerMode, "admin", StringComparison.OrdinalIgnoreCase) ||
                SetAsPlatformOwner;

            string targetStatus = isAdministrationOperation
                ? (isBookable ? "Pending" : "Active")
                : "WaitOwner";

            string targetAvailability =
                isAdministrationOperation && !isBookable
                    ? "true"
                    : "false";

            return RedirectToAction("FillSurvey", new
            {
                categoryId = CategoryId,
                ownerId = targetOwnerId,
                initialStatus = targetStatus,
                initialAvailability = targetAvailability
            });
        }


        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Places.Create)]
        public async Task<IActionResult> FillSurvey(int categoryId, int? ownerId, string initialStatus, string initialAvailability)
        {
            var category = await _db.Categories.FindAsync(categoryId);
            if (category == null) return NotFound();

            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.BookingSystemId = category.BookingSystemId;
            ViewBag.CategoryName = category.CategoryName;
            ViewBag.SelectedOwnerId = ownerId;
            ViewBag.InitialStatus = initialStatus ?? "Pending";
            ViewBag.InitialAvailability = initialAvailability ?? "false";

            var fillAdminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isCurrentAdminOwner =
                ownerId.HasValue &&
                int.TryParse(fillAdminIdClaim, out int fillAdminId) &&
                fillAdminId > 0 &&
                ownerId.Value == fillAdminId;

            ViewBag.IsCurrentAdminOwner = isCurrentAdminOwner;

            if (ownerId.HasValue && ownerId.Value > 0)
            {
                var selectedAccount = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == ownerId.Value);

                if (selectedAccount != null)
                {
                    ViewBag.CurrentOwnerName = selectedAccount.FullName;
                    ViewBag.CurrentOwnerPhone = selectedAccount.PhoneNumber;
                }
            }
            else
            {
                var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(adminIdClaim) && int.TryParse(adminIdClaim, out int adminId))
                {
                    var adminUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == adminId);
                    if (adminUser != null)
                    {
                        ViewBag.CurrentOwnerName = adminUser.FullName;
                        ViewBag.CurrentOwnerPhone = adminUser.PhoneNumber;
                    }
                }
            }

            ViewBag.OwnerMode = TempData["OwnerMode"]?.ToString();
            ViewBag.NewFirstName = TempData["NewFirstName"]?.ToString();
            ViewBag.NewLastName = TempData["NewLastName"]?.ToString();
            ViewBag.NewOwnerUsername = TempData["NewOwnerUsername"]?.ToString();
            ViewBag.NewEmail = TempData["NewEmail"]?.ToString();
            ViewBag.NewOwnerPassword = TempData["NewOwnerPassword"]?.ToString();

            ViewBag.Categories = _db.Categories.ToList();
            ViewBag.Cities = _db.Cities.ToList();

            return View();
        }


        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            return BCrypt.Net.BCrypt.HashPassword(password, 11);
        }

        // 1. دالة لجلب قائمة الملاك ليتم عرضهم في النافذة المنبثقة
        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Places.ReassignOwner)]
        public async Task<IActionResult> GetOwnersList()
        {
            var owners = await _db.Users
                .Where(u => u.Role == "Owner")
                .Select(u => new { id = u.UserId, name = u.FullName })
                .ToListAsync();

            return Json(owners);
        }

        // 2. دالة لاستقبال طلب إعادة التعيين
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Places.ReassignOwner)]
        public async Task<IActionResult> ReassignPlaceOwner([FromForm] int placeId, [FromForm] int newOwnerId)
        {
            var place = await _db.Places.FindAsync(placeId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة." });

            var ownerExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == newOwnerId &&
                    user.Role == "Owner");

            if (!ownerExists)
            {
                return Json(new { success = false, message = "حساب المالك المحدد غير موجود." });
            }

            place.OwnerId = newOwnerId;
            place.Status = "WaitOwner";
            place.IsAvailable = false;
            place.RejectionReason = null;

            var notificationService = HttpContext.RequestServices
                .GetRequiredService<INotificationService>();

            notificationService.Queue(new CreateNotificationDto
            {
                UserId = newOwnerId,
                NotificationTitle = "منشأة تنتظر موافقتك",
                NotificationText =
                    $"أعادت الإدارة إسناد منشأة «{place.PlaceNameAndDesc}» إلى حسابك. راجع البيانات ثم وافق أو ارفض.",
                NotificationType = NotificationTypes.General,
                TargetUrl =
                    $"/Owner/Place/ManagePlaces?placeTab=place-list-tab&focusPlace={place.PlaceId}",
                ReferenceType = NotificationReferenceTypes.Place,
                ReferenceId = place.PlaceId
            });

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "تم إعادة تعيين المنشأة وإرسال إشعار للمالك بنجاح!" });
        }




        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Places.Edit)]
        public async Task<IActionResult> Edit(int id)
        {
            // 1. البحث عن المنشأة المطلوبة مع جلب بيانات الفئة والمدينة والمالك
            var place = await _db.Places
                .Include(p => p.Category)
                .Include(p => p.City)
                .Include(p => p.Owner)
                .FirstOrDefaultAsync(p => p.PlaceId == id);

            // 2. إذا لم يجد المنشأة (تم حذفها مثلاً)
            if (place == null)
            {
                return NotFound();
            }

            // 3. تمرير القوائم المنسدلة (الفئات والمدن) للواجهة لكي يتمكن من تعديلها
            ViewBag.Categories = _db.Categories.ToList();
            ViewBag.Cities = _db.Cities.ToList();

            // تمرير نظام الحجز لمعرفة هل هو مطعم أم فندق
            ViewBag.BookingSystemId = place.Category?.BookingSystemId ?? 0;

            // 4. إرسال كائن المنشأة للفيو
            return View(place);
        }



    }
}
