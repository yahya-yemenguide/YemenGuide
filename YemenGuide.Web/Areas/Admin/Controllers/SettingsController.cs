using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using YemenGuide.Data;
using YemenGuide.Core.Entities;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IAdminPermissionService _permissionService;
        private readonly ExchangeRateService _exchangeRateService;

        public SettingsController(
            AppDbContext context,
            IWebHostEnvironment env,
            IAdminPermissionService permissionService,
            ExchangeRateService exchangeRateService)
        {
            _context = context;
            _env = env;
            _permissionService = permissionService;
            _exchangeRateService = exchangeRateService;
        }

        // ==========================================
        // 1. العرض الرئيسي لصفحة الإعدادات
        // ==========================================
        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Settings.View)]
        public async Task<IActionResult> SystemSettings()
        {
            var settings =
                await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted)
                ?? new SystemSetting();

            return View(settings);
        }

        // ==========================================
        // 2. حفظ إعدادات التبويب الحالي فقط
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(
            [FromForm] SystemSetting formData,
            [FromForm] string? Section,
            IFormFile? LogoUpload,
            IFormFile? FaviconUpload)
        {
            try
            {
                var cancellationToken =
                    HttpContext.RequestAborted;

                string section =
                    (Section ?? string.Empty)
                        .Trim()
                        .ToLowerInvariant();

                if (section != "branding" &&
                    section != "policies" &&
                    section != "contact" &&
                    section != "maintenance")
                {
                    return Json(new
                    {
                        success = false,
                        message = "قسم الإعدادات المطلوب حفظه غير معروف."
                    });
                }

                var userIdValue =
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                var currentAdminUserId =
                    int.TryParse(
                        userIdValue,
                        out var parsedAdminUserId)
                        ? parsedAdminUserId
                        : 0;

                var canManageGeneral =
                    await _permissionService.HasPermissionAsync(
                        currentAdminUserId,
                        AdminPermissionKeys.Settings.ManageGeneral,
                        cancellationToken);

                var canManageFinanceSettings =
                    await _permissionService.HasPermissionAsync(
                        currentAdminUserId,
                        AdminPermissionKeys.Finance.ManageSettings,
                        cancellationToken);

                bool sectionAllowed =
                    section == "policies"
                        ? canManageGeneral ||
                          canManageFinanceSettings
                        : canManageGeneral;

                if (!sectionAllowed)
                {
                    return Json(new
                    {
                        success = false,
                        message = "ليس لديك صلاحية لحفظ هذا القسم."
                    });
                }

                var settings =
                    await _context.SystemSettings
                        .OrderBy(s => s.Id)
                        .FirstOrDefaultAsync(
                            cancellationToken);

                bool isNewRecord =
                    settings == null;

                settings ??=
                    new SystemSetting();

                if (string.IsNullOrWhiteSpace(
                        settings.DefaultWalletLogoPath))
                {
                    settings.DefaultWalletLogoPath =
                        "/images/wallets/default-wallet.png";
                }

                if (string.IsNullOrWhiteSpace(
                        settings.ExchangeRateLastStatus))
                {
                    settings.ExchangeRateLastStatus =
                        "Never";
                }

                // ==============================================================
                // 1) الهوية والمظهر
                // ==============================================================
                if (section == "branding")
                {
                    if (LogoUpload != null &&
                        LogoUpload.Length > 0)
                    {
                        if (!TryValidateUploadedFile(
                                LogoUpload,
                                10 * 1024L * 1024L,
                                new[] { ".jpg", ".jpeg", ".png", ".webp" },
                                new[]
                                {
                                    "image/jpeg",
                                    "image/png",
                                    "image/webp"
                                },
                                out string logoValidationError))
                        {
                            return Json(new
                            {
                                success = false,
                                message = logoValidationError
                            });
                        }
                    }

                    if (FaviconUpload != null &&
                        FaviconUpload.Length > 0)
                    {
                        if (!TryValidateUploadedFile(
                                FaviconUpload,
                                2 * 1024L * 1024L,
                                new[] { ".ico", ".png" },
                                new[]
                                {
                                    "image/x-icon",
                                    "image/vnd.microsoft.icon",
                                    "image/png",
                                    "application/octet-stream"
                                },
                                out string faviconValidationError))
                        {
                            return Json(new
                            {
                                success = false,
                                message = faviconValidationError
                            });
                        }
                    }

                    string imagesDirectory =
                        Path.Combine(
                            _env.WebRootPath,
                            "images");

                    Directory.CreateDirectory(
                        imagesDirectory);

                    if (LogoUpload != null &&
                        LogoUpload.Length > 0)
                    {
                        string extension =
                            Path.GetExtension(
                                    LogoUpload.FileName)
                                .ToLowerInvariant();

                        string logoFileName =
                            $"site-logo-{Guid.NewGuid():N}{extension}";

                        string logoPath =
                            Path.Combine(
                                imagesDirectory,
                                logoFileName);

                        await using var stream =
                            new FileStream(
                                logoPath,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None);

                        await LogoUpload.CopyToAsync(
                            stream,
                            cancellationToken);

                        settings.LogoPath =
                            "/images/" + logoFileName;
                    }

                    if (FaviconUpload != null &&
                        FaviconUpload.Length > 0)
                    {
                        string extension =
                            Path.GetExtension(
                                    FaviconUpload.FileName)
                                .ToLowerInvariant();

                        string faviconFileName =
                            $"site-favicon-{Guid.NewGuid():N}{extension}";

                        string faviconPath =
                            Path.Combine(
                                imagesDirectory,
                                faviconFileName);

                        await using var stream =
                            new FileStream(
                                faviconPath,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None);

                        await FaviconUpload.CopyToAsync(
                            stream,
                            cancellationToken);

                        settings.FaviconPath =
                            "/images/" + faviconFileName;
                    }

                    settings.SiteName =
                        string.IsNullOrWhiteSpace(
                            formData.SiteName)
                            ? "دليلك اليمني"
                            : formData.SiteName.Trim();

                    settings.SiteDescription =
                        formData.SiteDescription?.Trim();

                    settings.HeroTitleMain =
                        string.IsNullOrWhiteSpace(
                            formData.HeroTitleMain)
                            ? "دليلك اليمني بين يديك.."
                            : formData.HeroTitleMain.Trim();

                    settings.HeroTitleSub =
                        string.IsNullOrWhiteSpace(
                            formData.HeroTitleSub)
                            ? "اكتشف"
                            : formData.HeroTitleSub.Trim();

                    settings.SeasonTheme =
                        string.IsNullOrWhiteSpace(
                            formData.SeasonTheme)
                            ? "Normal"
                            : formData.SeasonTheme.Trim();

                    settings.IsBannerActive =
                        formData.IsBannerActive;

                    settings.BannerText =
                        formData.BannerText?.Trim();
                }

                // ==============================================================
                // 2) التشغيل والمالية
                // ==============================================================
                if (section == "policies")
                {
                    if (canManageGeneral)
                    {
                        if (formData.MaxImageSize < 1 ||
                            formData.MaxImageSize > 100)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "الحد الأقصى لحجم الصورة يجب أن يكون بين 1 و100 ميجابايت."
                            });
                        }

                        if (formData.MaxImageCount < 1 ||
                            formData.MaxImageCount > 100)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "الحد الأقصى لعدد الصور يجب أن يكون بين 1 و100 صورة."
                            });
                        }

                        settings.AutoApprovePlaces =
                            formData.AutoApprovePlaces;

                        settings.AllowNewRegistrations =
                            formData.AllowNewRegistrations;

                        settings.MaxImageSize =
                            formData.MaxImageSize;

                        settings.MaxImageCount =
                            formData.MaxImageCount;
                    }

                    if (canManageFinanceSettings)
                    {
                        decimal? parsedCommission =
                            null;

                        if (decimal.TryParse(
                                Request.Form["CommissionPercentage"],
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out decimal commission))
                        {
                            if (commission < 0m ||
                                commission > 100m)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = "نسبة عمولة المنصة يجب أن تكون بين 0 و100."
                                });
                            }

                            parsedCommission =
                                commission;
                        }

                        bool requestedAutoExchangeMode =
                            formData.AutoUpdateExchangeRates == true;

                        string requestedDefaultCurrency =
                            NormalizeDefaultCurrency(
                                formData.DefaultCurrency);

                        decimal? sanaaUsd = null;
                        decimal? sanaaSar = null;
                        decimal? adenUsd = null;
                        decimal? adenSar = null;

                        bool mustValidateRates =
                            !requestedAutoExchangeMode ||
                            isNewRecord;

                        if (mustValidateRates)
                        {
                            if (!TryReadPositiveDecimal(
                                    "sanaaUsdRate",
                                    out decimal parsedSanaaUsd) ||
                                !TryReadPositiveDecimal(
                                    "sanaaSarRate",
                                    out decimal parsedSanaaSar) ||
                                !TryReadPositiveDecimal(
                                    "adenUsdRate",
                                    out decimal parsedAdenUsd) ||
                                !TryReadPositiveDecimal(
                                    "adenSarRate",
                                    out decimal parsedAdenSar))
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = requestedAutoExchangeMode
                                        ? "لا يمكن إنشاء إعدادات الصرف لأول مرة بقيم صفرية. أدخل أسعاراً سليمة أولاً ثم فعّل التحديث التلقائي."
                                        : "في الوضع اليدوي يجب إدخال أسعار صنعاء وعدن الأربعة بقيم أكبر من صفر."
                                });
                            }

                            var manualRates =
                                new YemenExchangeRateResult
                                {
                                    SanaaUsdRate =
                                        parsedSanaaUsd,
                                    SanaaSarRate =
                                        parsedSanaaSar,
                                    AdenUsdRate =
                                        parsedAdenUsd,
                                    AdenSarRate =
                                        parsedAdenSar,
                                    SourceName =
                                        "Manual",
                                    FetchedAtUtc =
                                        DateTime.UtcNow,
                                    RateType =
                                        "Sell"
                                };

                            if (!manualRates.IsValid(
                                    out string validationError))
                            {
                                return Json(new
                                {
                                    success = false,
                                    message =
                                        "أسعار الصرف المدخلة غير صالحة: " +
                                        validationError
                                });
                            }

                            sanaaUsd =
                                parsedSanaaUsd;

                            sanaaSar =
                                parsedSanaaSar;

                            adenUsd =
                                parsedAdenUsd;

                            adenSar =
                                parsedAdenSar;
                        }

                        bool previousAutoMode =
                            settings.AutoUpdateExchangeRates ==
                            true;

                        settings.DefaultCurrency =
                            requestedDefaultCurrency;

                        settings.AutoUpdateExchangeRates =
                            requestedAutoExchangeMode;

                        if (parsedCommission.HasValue)
                        {
                            settings.CommissionPercentage =
                                parsedCommission.Value;
                        }

                        if (!requestedAutoExchangeMode ||
                            isNewRecord)
                        {
                            bool manualRatesChanged =
                                settings.SanaaUsdRate !=
                                    sanaaUsd!.Value ||
                                settings.SanaaSarRate !=
                                    sanaaSar!.Value ||
                                settings.AdenUsdRate !=
                                    adenUsd!.Value ||
                                settings.AdenSarRate !=
                                    adenSar!.Value ||
                                previousAutoMode;

                            settings.SanaaUsdRate =
                                sanaaUsd.Value;

                            settings.SanaaSarRate =
                                sanaaSar.Value;

                            settings.AdenUsdRate =
                                adenUsd.Value;

                            settings.AdenSarRate =
                                adenSar.Value;

                            if (manualRatesChanged)
                            {
                                DateTime manualUpdatedAt =
                                    DateTime.UtcNow;

                                settings.ExchangeRateLastAttemptAt =
                                    manualUpdatedAt;

                                settings.ExchangeRateLastUpdatedAt =
                                    manualUpdatedAt;

                                settings.ExchangeRateLastSource =
                                    "Manual";

                                settings.ExchangeRateLastStatus =
                                    "Success";

                                settings.ExchangeRateLastError =
                                    null;
                            }
                        }
                    }
                }

                // ==============================================================
                // 3) التواصل
                // ==============================================================
                if (section == "contact")
                {
                    settings.ContactEmail =
                        formData.ContactEmail?.Trim();

                    settings.SupportPhone =
                        formData.SupportPhone?.Trim();

                    settings.FacebookLink =
                        formData.FacebookLink?.Trim();

                    settings.TwitterLink =
                        formData.TwitterLink?.Trim();

                    settings.InstagramLink =
                        formData.InstagramLink?.Trim();

                    settings.WhatsAppNumber =
                        formData.WhatsAppNumber?.Trim();
                }

                // ==============================================================
                // 4) الصيانة والطوارئ
                // ==============================================================
                if (section == "maintenance")
                {
                    string maintenanceType =
                        NormalizeMaintenanceType(
                            formData.MaintenanceType);

                    DateTime? maintenanceStartUtc =
                        ConvertYemenLocalToUtc(
                            formData.MaintenanceStartAt);

                    DateTime? maintenanceEndUtc =
                        ConvertYemenLocalToUtc(
                            formData.MaintenanceEndAt);

                    if (maintenanceStartUtc.HasValue &&
                        maintenanceEndUtc.HasValue &&
                        maintenanceEndUtc.Value <=
                            maintenanceStartUtc.Value)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "موعد انتهاء الصيانة يجب أن يكون بعد موعد البداية."
                        });
                    }

                    if (formData.MaintenanceMode &&
                        maintenanceType == "Partial" &&
                        !formData.MaintenanceBlockBookings &&
                        !formData.MaintenanceBlockPayments &&
                        !formData.MaintenanceBlockOwnerChanges &&
                        !formData.MaintenanceBlockMessages &&
                        !formData.MaintenanceBlockReviews &&
                        !formData.MaintenanceBlockRegistrations)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "في الصيانة الجزئية اختر عملية واحدة على الأقل لإيقافها."
                        });
                    }

                    settings.MaintenanceMode =
                        formData.MaintenanceMode;

                    settings.MaintenanceTitle =
                        string.IsNullOrWhiteSpace(
                            formData.MaintenanceTitle)
                            ? "نعود إليكم قريبًا"
                            : formData.MaintenanceTitle.Trim();

                    settings.MaintenanceMessage =
                        string.IsNullOrWhiteSpace(
                            formData.MaintenanceMessage)
                            ? "المنصة تحت الصيانة حاليًا، نعمل على تحسين الخدمة وسنعود قريبًا."
                            : formData.MaintenanceMessage.Trim();

                    settings.MaintenanceType =
                        maintenanceType;

                    settings.MaintenanceAffectOwners =
                        formData.MaintenanceAffectOwners;

                    settings.MaintenanceStartAt =
                        maintenanceStartUtc;

                    settings.MaintenanceEndAt =
                        maintenanceEndUtc;

                    settings.MaintenanceShowContact =
                        formData.MaintenanceShowContact;

                    settings.MaintenanceBlockBookings =
                        formData.MaintenanceBlockBookings;

                    settings.MaintenanceBlockPayments =
                        formData.MaintenanceBlockPayments;

                    settings.MaintenanceBlockOwnerChanges =
                        formData.MaintenanceBlockOwnerChanges;

                    settings.MaintenanceBlockMessages =
                        formData.MaintenanceBlockMessages;

                    settings.MaintenanceBlockReviews =
                        formData.MaintenanceBlockReviews;

                    settings.MaintenanceBlockRegistrations =
                        formData.MaintenanceBlockRegistrations;
                }

                if (isNewRecord)
                {
                    _context.SystemSettings.Add(
                        settings);
                }

                await _context.SaveChangesAsync(
                    cancellationToken);

                string message =
                    section switch
                    {
                        "branding" =>
                            "تم حفظ الهوية والمظهر بنجاح.",
                        "policies" =>
                            "تم حفظ إعدادات التشغيل والمالية المسموح لك بإدارتها.",
                        "contact" =>
                            "تم حفظ إعدادات التواصل بنجاح.",
                        "maintenance" =>
                            "تم حفظ إعدادات الصيانة والطوارئ بنجاح.",
                        _ =>
                            "تم حفظ الإعدادات بنجاح."
                    };

                return Json(new
                {
                    success = true,
                    message,
                    exchangeRates =
                        section == "policies"
                            ? BuildExchangeRatePayload(
                                settings)
                            : null,
                    maintenance =
                        section == "maintenance"
                            ? BuildMaintenancePayload(
                                settings)
                            : null,
                    defaultWalletLogoPath =
                        GetDefaultWalletLogoPath(
                            settings)
                });
            }
            catch (OperationCanceledException)
                when (HttpContext.RequestAborted
                    .IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "حدث خطأ غير متوقع أثناء الحفظ: " +
                        GetUsefulErrorMessage(ex)
                });
            }
        }


        // ==========================================================================

        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Settings.View)]
        public async Task<IActionResult> GetExchangeRateStatus()
        {
            var settings =
                await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(
                        HttpContext.RequestAborted);

            if (settings == null)
            {
                return Json(new
                {
                    success = false,
                    message = "لا يوجد سجل لإعدادات النظام."
                });
            }

            return Json(new
            {
                success = true,
                exchangeRates = BuildExchangeRatePayload(settings)
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManageSettings)]
        public async Task<IActionResult> RefreshExchangeRatesNow()
        {
            var cancellationToken =
                HttpContext.RequestAborted;

            var settings =
                await _context.SystemSettings
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
            {
                return Json(new
                {
                    success = false,
                    message = "لا يوجد سجل لإعدادات النظام."
                });
            }

            if (settings.AutoUpdateExchangeRates != true)
            {
                return Json(new
                {
                    success = false,
                    code = "AUTO_MODE_REQUIRED",
                    message = "فعّل التحديث التلقائي واحفظ الإعدادات أولاً، ثم استخدم زر تحديث الأسعار الآن.",
                    exchangeRates = BuildExchangeRatePayload(settings)
                });
            }

            settings.ExchangeRateLastAttemptAt =
                DateTime.UtcNow;

            settings.ExchangeRateLastSource =
                _exchangeRateService.SourceName;

            await _context.SaveChangesAsync(
                cancellationToken);

            try
            {
                YemenExchangeRateResult latestRates =
                    await _exchangeRateService
                        .GetLatestExchangeRatesAsync(
                            cancellationToken);

                settings.SanaaUsdRate =
                    latestRates.SanaaUsdRate;

                settings.SanaaSarRate =
                    latestRates.SanaaSarRate;

                settings.AdenUsdRate =
                    latestRates.AdenUsdRate;

                settings.AdenSarRate =
                    latestRates.AdenSarRate;

                settings.ExchangeRateLastUpdatedAt =
                    DateTime.UtcNow;

                settings.ExchangeRateLastSource =
                    string.IsNullOrWhiteSpace(
                        latestRates.SourceName)
                        ? _exchangeRateService.SourceName
                        : latestRates.SourceName.Trim();

                settings.ExchangeRateLastStatus =
                    "Success";

                settings.ExchangeRateLastError =
                    null;

                await _context.SaveChangesAsync(
                    cancellationToken);

                return Json(new
                {
                    success = true,
                    message = "تم تحديث أسعار صنعاء وعدن من المصدر المعتمد بنجاح.",
                    exchangeRates = BuildExchangeRatePayload(settings)
                });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                settings.ExchangeRateLastStatus =
                    "Failed";

                settings.ExchangeRateLastSource =
                    _exchangeRateService.SourceName;

                settings.ExchangeRateLastError =
                    Truncate(
                        GetUsefulErrorMessage(ex),
                        500);

                try
                {
                    await _context.SaveChangesAsync(
                        cancellationToken);
                }
                catch
                {
                    // لا نستبدل الخطأ الأصلي بخطأ حفظ بيانات الحالة.
                }

                return Json(new
                {
                    success = false,
                    message = "تعذر تحديث أسعار الصرف. تم الاحتفاظ بآخر أسعار سليمة دون تغيير.",
                    exchangeRates = BuildExchangeRatePayload(settings)
                });
            }
        }


        // ==========================================================================
        // 💎 3. أكشنات التبويبة الخامسة الجديدة (إدارة المحافظ البنكية المعتمدة)
        // ==========================================================================

        // أ. تحديث الشعار الافتراضي لشبكات الدفع
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManagePaymentNetworks)]
        [RequestSizeLimit(104857600)]
        public async Task<IActionResult> UpdateDefaultWalletLogo(
            IFormFile? DefaultWalletLogoUpload)
        {
            try
            {
                if (DefaultWalletLogoUpload == null ||
                    DefaultWalletLogoUpload.Length <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "اختر صورة للشعار الافتراضي أولاً."
                    });
                }

                var settings = await _context.SystemSettings
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

                if (settings == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "لا يوجد سجل لإعدادات النظام."
                    });
                }

                long maxSizeInBytes =
                    Math.Clamp(
                        settings.MaxImageSize,
                        1,
                        100) *
                    1024L * 1024L;

                if (!TryValidateUploadedFile(
                        DefaultWalletLogoUpload,
                        maxSizeInBytes,
                        new[] { ".jpg", ".jpeg", ".png", ".webp" },
                        new[]
                        {
                            "image/jpeg",
                            "image/png",
                            "image/webp"
                        },
                        out string validationError))
                {
                    return Json(new
                    {
                        success = false,
                        message = validationError
                    });
                }

                var extension =
                    Path.GetExtension(
                            DefaultWalletLogoUpload.FileName)
                        .ToLowerInvariant();

                string directoryPath = Path.Combine(
                    _env.WebRootPath,
                    "images",
                    "wallets",
                    "default");

                Directory.CreateDirectory(directoryPath);

                string fileName =
                    $"wallet-default-{Guid.NewGuid():N}{extension}";

                string fullPath = Path.Combine(
                    directoryPath,
                    fileName);

                await using (var stream = new FileStream(
                    fullPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await DefaultWalletLogoUpload.CopyToAsync(
                        stream,
                        HttpContext.RequestAborted);
                }

                string newPath =
                    "/images/wallets/default/" + fileName;

                string oldPath =
                    GetDefaultWalletLogoPath(settings);

                settings.DefaultWalletLogoPath = newPath;

                await _context.SaveChangesAsync(
                    HttpContext.RequestAborted);

                // السجلات القديمة التي كانت تشير للصورة الثابتة تعتبر بدون شعار خاص.
                var legacyWallets = await _context.SupportedWallets
                    .Where(w =>
                        w.LogoPath == "/images/wallets/default-wallet.png" ||
                        w.LogoPath == oldPath)
                    .ToListAsync(HttpContext.RequestAborted);

                foreach (var wallet in legacyWallets)
                {
                    wallet.LogoPath = null;
                }

                if (legacyWallets.Count > 0)
                {
                    await _context.SaveChangesAsync(
                        HttpContext.RequestAborted);
                }

                return Json(new
                {
                    success = true,
                    message = "تم تحديث الشعار الافتراضي لشبكات الدفع بنجاح.",
                    logoPath = newPath
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "تعذر تحديث الشعار الافتراضي: " +
                              GetUsefulErrorMessage(ex)
                });
            }
        }

        // ب. جلب قائمة شبكات الدفع المعتمدة (أجاكس - GET)
        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Settings.View)]
        public async Task<IActionResult> GetSupportedWallets()
        {
            var wallets = await _context.SupportedWallets
                .OrderBy(w => w.WalletId)
                .ToListAsync();
            return Json(wallets);
        }

        // b. إضافة محفظة/شبكة دفع جديدة مع رفع اللوجو (أجاكس - POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManagePaymentNetworks)]
        public async Task<IActionResult> AddSupportedWallet([FromForm] SupportedWallet walletData, IFormFile? LogoUpload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(walletData.WalletName))
                {
                    return Json(new { success = false, message = "يجب كتابة اسم المحفظة أو البنك أولاً." });
                }

                // منع التكرار البرمجي لاسم المحفظة
                bool isExist = await _context.SupportedWallets.AnyAsync(w => w.WalletName == walletData.WalletName.Trim());
                if (isExist)
                {
                    return Json(new { success = false, message = "عذراً، هذه المحفظة أو الشبكة مسجلة مسبقاً في النظام." });
                }

                // رفع وحفظ الشعار المالي للشبكة
                if (LogoUpload != null &&
                    LogoUpload.Length > 0)
                {
                    var systemSettings =
                        await _context.SystemSettings
                            .AsNoTracking()
                            .OrderBy(s => s.Id)
                            .FirstOrDefaultAsync(
                                HttpContext.RequestAborted);

                    long maxSizeInBytes =
                        Math.Clamp(
                            systemSettings?.MaxImageSize ?? 5,
                            1,
                            100) *
                        1024L * 1024L;

                    if (!TryValidateUploadedFile(
                            LogoUpload,
                            maxSizeInBytes,
                            new[] { ".jpg", ".jpeg", ".png", ".webp" },
                            new[]
                            {
                                "image/jpeg",
                                "image/png",
                                "image/webp"
                            },
                            out string logoValidationError))
                    {
                        return Json(new
                        {
                            success = false,
                            message = logoValidationError
                        });
                    }

                    string extension =
                        Path.GetExtension(
                                LogoUpload.FileName)
                            .ToLowerInvariant();

                    string fileName =
                        $"wallet-{Guid.NewGuid():N}{extension}";

                    string directoryPath =
                        Path.Combine(
                            _env.WebRootPath,
                            "images",
                            "wallets");

                    Directory.CreateDirectory(
                        directoryPath);

                    string fullPath =
                        Path.Combine(
                            directoryPath,
                            fileName);

                    await using var stream =
                        new FileStream(
                            fullPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None);

                    await LogoUpload.CopyToAsync(
                        stream,
                        HttpContext.RequestAborted);

                    walletData.LogoPath =
                        "/images/wallets/" +
                        fileName;
                }
                else
                {
                    // NULL يعني: استخدم الشعار الافتراضي الديناميكي من SystemSettings.
                    walletData.LogoPath = null;
                }

                walletData.WalletName = walletData.WalletName.Trim();
                walletData.IsActive = true;
                walletData.CreatedAt = DateTime.Now;

                _context.SupportedWallets.Add(walletData);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"تم اعتماد وإدراج شبكة [{walletData.WalletName}] بنجاح!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "فشل إضافة الشبكة: " + ex.Message });
            }
        }

        // ج. تحويل وتحديث الحالة التشغيلية للمحفظة لحظياً (أجاكس - POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManagePaymentNetworks)]
        public async Task<IActionResult> ToggleWalletStatus([FromForm] int id, [FromForm] bool isActive)
        {
            try
            {
                var wallet = await _context.SupportedWallets.FindAsync(id);
                if (wallet == null)
                {
                    return Json(new { success = false, message = "خطأ: لم يتم العثور على قنوات هذه الشبكة." });
                }

                wallet.IsActive = isActive;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث عطل أثناء تحديث الحالة: " + ex.Message });
            }
        }

        // د. شطب وحذف شبكة دفع معتمدة نهائياً من السيرفر (أجاكس - POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManagePaymentNetworks)]
        public async Task<IActionResult> DeleteSupportedWallet([FromForm] int id)
        {
            try
            {
                var wallet = await _context.SupportedWallets.FindAsync(id);
                if (wallet == null)
                {
                    return Json(new { success = false, message = "خطأ: المحفظة المستهدفة غير موجودة مسبقاً." });
                }

                // نحذف فقط شعار المحفظة الخاص بها، ولا نحذف الشعار الافتراضي المشترك.
                var settings = await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync();

                string defaultWalletLogoPath =
                    GetDefaultWalletLogoPath(settings);

                if (!string.IsNullOrWhiteSpace(wallet.LogoPath) &&
                    !string.Equals(
                        wallet.LogoPath,
                        defaultWalletLogoPath,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        wallet.LogoPath,
                        "/images/wallets/default-wallet.png",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string fullPath = Path.Combine(
                        _env.WebRootPath,
                        wallet.LogoPath.TrimStart('/'));

                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                _context.SupportedWallets.Remove(wallet);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم إقصاء وتصفية سجلات الشبكة من المنظومة بنجاح." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "تعذر إتمام الحذف: " + ex.Message });
            }
        }

        // ==========================================================================
        // ✨ 4. أكشنات استوديو السلايدر التفاعلي (Hero Slider Studio) المحدثة
        // ==========================================================================

        // أ. جلب جميع السلايدرات مرتبة حسب (DisplayOrder) وإرسالها بـ JSON
        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Settings.View)]
        public async Task<IActionResult> GetHeroSlides()
        {
            var slides = await _context.HeroSlides
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new {
                    id = s.HeroSlideId,
                    imagePath = s.ImagePath,
                    typingText = s.TypingText,
                    mainTextColor = s.MainTextColor,
                    accentColor = s.AccentColor,
                    isActive = s.IsActive
                })
                .ToListAsync();

            return Json(slides);
        }

        // ب. إضافة سلايدر جديد وحفظ صورته في مجلد uploads/hero
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageHeroSlides)]
        [RequestSizeLimit(31457280)] // ✨ السماح للطلب بالوصول إلى 30 ميجابايت كحد أقصى لمنع السيرفر من إسقاط الملف مبكراً
        public async Task<IActionResult> AddHeroSlide([FromForm] string TypingText, [FromForm] string MainTextColor, [FromForm] string AccentColor, IFormFile ImageUpload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TypingText) || ImageUpload == null)
                    return Json(new { success = false, message = "النص والصورة/الفيديو مطلوبان." });

                long maxSizeInBytes =
                    20 * 1024L * 1024L;

                if (!TryValidateUploadedFile(
                        ImageUpload,
                        maxSizeInBytes,
                        new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4" },
                        new[]
                        {
                            "image/jpeg",
                            "image/png",
                            "image/gif",
                            "image/webp",
                            "video/mp4"
                        },
                        out string mediaValidationError))
                {
                    return Json(new
                    {
                        success = false,
                        message = mediaValidationError
                    });
                }

                var extension =
                    Path.GetExtension(
                            ImageUpload.FileName)
                        .ToLowerInvariant();

                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "hero");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + extension;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageUpload.CopyToAsync(fileStream);
                }

                int maxOrder = await _context.HeroSlides.MaxAsync(s => (int?)s.DisplayOrder) ?? 0;

                var newSlide = new HeroSlide
                {
                    TypingText = TypingText.Trim(),
                    ImagePath = "/uploads/hero/" + uniqueFileName,
                    MainTextColor = string.IsNullOrWhiteSpace(MainTextColor) ? "#ffffff" : MainTextColor.Trim(),
                    AccentColor = string.IsNullOrWhiteSpace(AccentColor) ? "#ff5a5f" : AccentColor.Trim(),
                    DisplayOrder = maxOrder + 1,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.HeroSlides.Add(newSlide);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تمت إضافة السلايد بنجاح!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ أثناء المعالجة: " + ex.Message });
            }
        }

        // ج. تفعيل/إيقاف السلايدر (تحديث الحالة IsActive)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageHeroSlides)]
        public async Task<IActionResult> ToggleHeroSlideStatus([FromForm] int id, [FromForm] bool isActive)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide == null) return Json(new { success = false, message = "السلايد غير موجود." });

            slide.IsActive = isActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // د. تعديل النص التفاعلي (Smart Edit) 
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageHeroSlides)]
        public async Task<IActionResult> EditHeroSlideText([FromForm] int id, [FromForm] string newText)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide == null) return Json(new { success = false, message = "السلايد غير موجود." });

            if (string.IsNullOrWhiteSpace(newText)) return Json(new { success = false, message = "النص لا يمكن أن يكون فارغاً." });

            slide.TypingText = newText.Trim();
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // هـ. ترتيب السلايدرات للأعلى أو الأسفل (الأسهم)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageHeroSlides)]
        public async Task<IActionResult> MoveHeroSlideOrder([FromForm] int id, [FromForm] string direction)
        {
            var currentSlide = await _context.HeroSlides.FindAsync(id);
            if (currentSlide == null) return Json(new { success = false, message = "السلايد غير موجود." });

            HeroSlide targetSlide = null;

            if (direction == "up")
            {
                targetSlide = await _context.HeroSlides
                    .Where(s => s.DisplayOrder < currentSlide.DisplayOrder)
                    .OrderByDescending(s => s.DisplayOrder)
                    .FirstOrDefaultAsync();
            }
            else if (direction == "down")
            {
                targetSlide = await _context.HeroSlides
                    .Where(s => s.DisplayOrder > currentSlide.DisplayOrder)
                    .OrderBy(s => s.DisplayOrder)
                    .FirstOrDefaultAsync();
            }

            if (targetSlide != null)
            {
                int tempOrder = currentSlide.DisplayOrder;
                currentSlide.DisplayOrder = targetSlide.DisplayOrder;
                targetSlide.DisplayOrder = tempOrder;

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "لا يمكن تحريك السلايد أكثر في هذا الاتجاه." });
        }

        // و. شطب وحذف السلايدر نهائياً من قاعدة البيانات والسيرفر
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageHeroSlides)]
        public async Task<IActionResult> DeleteHeroSlide([FromForm] int id)
        {
            var slide = await _context.HeroSlides.FindAsync(id);
            if (slide == null) return Json(new { success = false, message = "السلايد غير موجود." });

            if (!string.IsNullOrEmpty(slide.ImagePath) && !slide.ImagePath.Contains("default"))
            {
                string fullPath = Path.Combine(_env.WebRootPath, slide.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }

            _context.HeroSlides.Remove(slide);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم حذف السلايد نهائياً." });
        }
        // ==========================================================================
        // Helpers
        // ==========================================================================

        private IActionResult AjaxError(
            string message,
            int statusCode = StatusCodes.Status400BadRequest)
        {
            Response.StatusCode = statusCode;

            return Json(new
            {
                success = false,
                message
            });
        }


        private static bool TryValidateUploadedFile(
            IFormFile file,
            long maxSizeInBytes,
            string[] allowedExtensions,
            string[] allowedContentTypes,
            out string error)
        {
            error = string.Empty;

            if (file == null ||
                file.Length <= 0)
            {
                error = "الملف المرفوع فارغ.";
                return false;
            }

            if (file.Length > maxSizeInBytes)
            {
                error =
                    $"حجم الملف يتجاوز الحد المسموح ({Math.Max(1, maxSizeInBytes / (1024L * 1024L))} ميجابايت).";
                return false;
            }

            string extension =
                Path.GetExtension(
                        file.FileName)
                    .ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension) ||
                !allowedExtensions.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase))
            {
                error = "صيغة الملف غير مدعومة.";
                return false;
            }

            string contentType =
                (file.ContentType ??
                 string.Empty)
                    .Trim();

            if (string.IsNullOrWhiteSpace(contentType) ||
                !allowedContentTypes.Contains(
                    contentType,
                    StringComparer.OrdinalIgnoreCase))
            {
                error = "نوع الملف المرفوع لا يطابق الصيغ المسموحة.";
                return false;
            }

            return true;
        }


        private static string NormalizeMaintenanceType(
            string? maintenanceType)
        {
            return maintenanceType switch
            {
                "ReadOnly" => "ReadOnly",
                "Partial" => "Partial",
                _ => "Full"
            };
        }

        private static DateTime? ConvertYemenLocalToUtc(
            DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            // datetime-local لا يرسل منطقة زمنية. نعتمده كتوقيت اليمن UTC+3.
            DateTime localYemen = DateTime.SpecifyKind(
                value.Value,
                DateTimeKind.Unspecified);

            return DateTime.SpecifyKind(
                localYemen.AddHours(-3),
                DateTimeKind.Utc);
        }

        private static string GetDefaultWalletLogoPath(
            SystemSetting? settings)
        {
            return string.IsNullOrWhiteSpace(
                    settings?.DefaultWalletLogoPath)
                ? "/images/wallets/default-wallet.png"
                : settings.DefaultWalletLogoPath.Trim();
        }

        private static object BuildMaintenancePayload(
            SystemSetting settings)
        {
            return new
            {
                enabled = settings.MaintenanceMode,
                type = NormalizeMaintenanceType(settings.MaintenanceType),
                title = settings.MaintenanceTitle,
                message = settings.MaintenanceMessage,
                affectOwners = settings.MaintenanceAffectOwners,
                startAt = NormalizeUtcDateForJson(settings.MaintenanceStartAt),
                endAt = NormalizeUtcDateForJson(settings.MaintenanceEndAt),
                showContact = settings.MaintenanceShowContact,
                blockBookings = settings.MaintenanceBlockBookings,
                blockPayments = settings.MaintenanceBlockPayments,
                blockOwnerChanges = settings.MaintenanceBlockOwnerChanges,
                blockMessages = settings.MaintenanceBlockMessages,
                blockReviews = settings.MaintenanceBlockReviews,
                blockRegistrations = settings.MaintenanceBlockRegistrations
            };
        }

        private bool TryReadPositiveDecimal(
            string formKey,
            out decimal value)
        {
            value = 0m;

            string raw =
                Request.Form[formKey].ToString();

            return decimal.TryParse(
                       raw,
                       System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out value) &&
                   value > 0m;
        }

        private static string NormalizeDefaultCurrency(
            string? currency)
        {
            string normalized =
                (currency ?? string.Empty).Trim();

            return normalized switch
            {
                "دولار أمريكي" => "دولار أمريكي",
                "ريال سعودي" => "ريال سعودي",
                "ريال يمني" => "ريال يمني",
                _ => "ريال يمني"
            };
        }

        private static object BuildExchangeRatePayload(
            SystemSetting settings)
        {
            string status =
                string.IsNullOrWhiteSpace(
                    settings.ExchangeRateLastStatus)
                    ? "Never"
                    : settings.ExchangeRateLastStatus.Trim();

            string statusLabel =
                status == "Success"
                    ? "محدّث بنجاح"
                    : status == "Failed"
                        ? "تعذر آخر تحديث"
                        : "لم يتم التحديث بعد";

            return new
            {
                autoUpdate =
                    settings.AutoUpdateExchangeRates == true,

                sanaaUsdRate =
                    settings.SanaaUsdRate,

                sanaaSarRate =
                    settings.SanaaSarRate,

                adenUsdRate =
                    settings.AdenUsdRate,

                adenSarRate =
                    settings.AdenSarRate,

                status,

                statusLabel,

                source =
                    string.IsNullOrWhiteSpace(
                        settings.ExchangeRateLastSource)
                        ? string.Empty
                        : settings.ExchangeRateLastSource.Trim(),

                sourceLabel =
                    GetExchangeSourceLabel(
                        settings.ExchangeRateLastSource),

                lastAttemptAt =
                    NormalizeUtcDateForJson(
                        settings.ExchangeRateLastAttemptAt),

                lastUpdatedAt =
                    NormalizeUtcDateForJson(
                        settings.ExchangeRateLastUpdatedAt),

                lastAttemptText =
                    settings.ExchangeRateLastAttemptAt.HasValue
                        ? FormatYemenExchangeDate(
                            settings.ExchangeRateLastAttemptAt.Value)
                        : "لا توجد محاولة تحديث حتى الآن",

                lastUpdatedText =
                    settings.ExchangeRateLastUpdatedAt.HasValue
                        ? FormatYemenExchangeDate(
                            settings.ExchangeRateLastUpdatedAt.Value)
                        : "لا يوجد تحديث ناجح حتى الآن",

                lastError =
                    settings.ExchangeRateLastError ??
                    string.Empty
            };
        }

        private static string GetExchangeSourceLabel(
            string? source)
        {
            string normalized =
                (source ?? string.Empty).Trim();

            return normalized switch
            {
                "YemenSul" => "يمن سول (YemenSul)",
                "Manual" => "إدخال يدوي",
                "" => "لم يُحدَّد بعد",
                _ => normalized
            };
        }

        private static DateTime? NormalizeUtcDateForJson(
            DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            DateTime utcValue =
                value.Value.Kind switch
                {
                    DateTimeKind.Utc =>
                        value.Value,

                    DateTimeKind.Local =>
                        value.Value.ToUniversalTime(),

                    _ =>
                        DateTime.SpecifyKind(
                            value.Value,
                            DateTimeKind.Utc)
                };

            return utcValue;
        }

        private static string FormatYemenExchangeDate(
            DateTime value)
        {
            DateTime utcValue =
                value.Kind switch
                {
                    DateTimeKind.Utc =>
                        value,

                    DateTimeKind.Local =>
                        value.ToUniversalTime(),

                    _ =>
                        DateTime.SpecifyKind(
                            value,
                            DateTimeKind.Utc)
                };

            DateTime yemenTime =
                utcValue.AddHours(3);

            return yemenTime.ToString(
                "dd/MM/yyyy - hh:mm tt",
                new System.Globalization.CultureInfo("ar-YE"));
        }


        private static string GetUsefulErrorMessage(
            Exception ex)
        {
            Exception current = ex;

            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return string.IsNullOrWhiteSpace(current.Message)
                ? "حدث خطأ غير معروف."
                : current.Message.Trim();
        }

        private static string Truncate(
            string value,
            int maxLength)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(
                0,
                maxLength);
        }

    }
}
