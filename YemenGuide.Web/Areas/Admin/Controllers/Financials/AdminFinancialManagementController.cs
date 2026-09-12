using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Admin.Controllers.Financials
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [RequireAdminPermission(AdminPermissionKeys.Finance.View)]
    [Route("Admin/Financials/Management")]
    public sealed class AdminFinancialManagementController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminFinancialManagement.cshtml";

        // الإعدادات المالية العامة لا ترتبط بمنشأة/مدينة بعينها،
        // لذلك نستخدم سوق صنعاء كمرجع الصرف الافتراضي لهذه القيم العامة فقط.
        private const string FinancialSettingsRegion =
            ExchangeRateRegionResolver.SanaaMarket;

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly IAdminPermissionService _adminPermissionService;
        private readonly CurrencyDisplayService _currencyDisplayService;
        private readonly CurrencyService _currencyService;
        private readonly ILogger<AdminFinancialManagementController> _logger;

        public AdminFinancialManagementController(
            IAdminFinancialService adminFinancialService,
            IAdminPermissionService adminPermissionService,
            CurrencyDisplayService currencyDisplayService,
            CurrencyService currencyService,
            ILogger<AdminFinancialManagementController> logger)
        {
            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _adminPermissionService = adminPermissionService ??
                throw new ArgumentNullException(nameof(adminPermissionService));

            _currencyDisplayService = currencyDisplayService ??
                throw new ArgumentNullException(nameof(currencyDisplayService));

            _currencyService = currencyService ??
                throw new ArgumentNullException(nameof(currencyService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            [FromQuery] AdminPlatformWalletFilterDto? walletFilter,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Forbid();
            }

            try
            {
                DisableResponseCaching();

                var canManageSettings =
                    await _adminPermissionService.HasPermissionAsync(
                        adminUserId,
                        AdminPermissionKeys.Finance.ManageSettings,
                        cancellationToken);

                var canManagePlatformWallets =
                    await _adminPermissionService.HasPermissionAsync(
                        adminUserId,
                        AdminPermissionKeys.Finance.ManagePlatformWallets,
                        cancellationToken);

                if (!canManageSettings && !canManagePlatformWallets)
                {
                    return Forbid();
                }

                var displayCurrency = CurrencyDisplayService.YemeniRial;

                var model = new AdminFinancialManagementPageDto
                {
                    LoadedAt = DateTime.Now,
                    Currency = displayCurrency
                };

                if (canManageSettings)
                {
                    model.Settings =
                        await _adminFinancialService.GetFinancialSettingsAsync(
                            cancellationToken);

                    // الخدمة المالية تتعمد إبقاء Settings بقيم YER الخام.
                    // هنا فقط نحول نسخة العرض إلى عملة بروفايل الأدمن.
                    ConvertSettingsForDisplay(
                        model.Settings.Settings,
                        displayCurrency);
                }
                else
                {
                    model.Settings = new AdminFinancialSettingsPageDto();
                }

                if (canManagePlatformWallets)
                {
                    // هذه الصفحة يطبق عليها AdminFinancialService التحويل أصلًا.
                    model.PlatformWallets =
                        await _adminFinancialService.GetPlatformWalletsAsync(
                            walletFilter ?? new AdminPlatformWalletFilterDto(),
                            cancellationToken);
                }
                else
                {
                    model.PlatformWallets = new AdminPlatformWalletPageDto();
                }

                ViewData["CanManageSettings"] = canManageSettings;
                ViewData["CanManagePlatformWallets"] =
                    canManagePlatformWallets;

                return PartialView(
                    PartialViewPath,
                    model);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء تحميل نافذة إدارة المالية للأدمن {AdminUserId}.",
                    adminUserId);

                return StatusCode(
                    500,
                    "تعذر تحميل إدارة المالية. أعد المحاولة.");
            }
        }

        [HttpPost("Settings/Save")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManageSettings)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(
            CancellationToken cancellationToken = default)
        {
            var submittedForm =
                await Request.ReadFormAsync(cancellationToken);

            if (!TryReadFinancialSettingsRequest(
                    submittedForm,
                    out var request,
                    out var requestError))
            {
                return BadRequest(new
                {
                    success = false,
                    message = requestError
                });
            }

            // Each tab records a precise automatic reason. The admin does not
            // have to type a reason before every ordinary settings save.
            request.ChangeReason = request.SettingsSection switch
            {
                "commissions" => "تحديث نسب العمولات من لوحة الإدارة",
                "trial" => "تحديث سياسة الفترة المجانية من لوحة الإدارة",
                "policies" => "تحديث سياسات الشحن والرصيد من لوحة الإدارة",
                _ => "تحديث إعدادات المالية من لوحة الإدارة"
            };

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var displayCurrency = CurrencyDisplayService.YemeniRial;

                // القيم وصلت من الحقول بعملة بروفايل الأدمن؛
                // نعيدها إلى YER قبل تمريرها لخدمة الحفظ وقاعدة البيانات.
                ConvertSettingsInputToYer(
                    request,
                    displayCurrency);

                var result =
                    await _adminFinancialService.SaveFinancialSettingsAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                if (!result.Success)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message
                    });
                }

                var updated =
                    await _adminFinancialService.GetFinancialSettingsAsync(
                        cancellationToken);

                // نجعل الاستجابة نفسها متوافقة مع عملة العرض أيضًا،
                // حتى لو استخدمتها الواجهة لاحقًا بدون Reload.
                ConvertSettingsForDisplay(
                    updated.Settings,
                    displayCurrency);

                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    entityId = result.EntityId,
                    currency = displayCurrency,
                    settings = updated.Settings,
                    categoryCommissions = updated.CategoryCommissions,
                    loadedAt = updated.LoadedAt
                });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء حفظ الإعدادات المالية بواسطة الأدمن {AdminUserId}.",
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر حفظ الإعدادات المالية حاليًا."
                });
            }
        }

        [HttpPost("Wallets/Save")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManagePlatformWallets)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveWallet(
            [FromForm] AdminPlatformWalletEditDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات حساب المنصة غير مكتملة."
                });
            }

            var rawAccountNumber = request.AccountNumber?.Trim() ?? string.Empty;
            if (!TryNormalizeAccountNumber(rawAccountNumber, out var normalizedAccountNumber))
            {
                ModelState.AddModelError(
                    nameof(request.AccountNumber),
                    "رقم الحساب يجب أن يحتوي على أرقام فقط بدون حروف أو رموز أو مسافات.");
            }
            else
            {
                request.AccountNumber = normalizedAccountNumber;
            }

            request.BeneficiaryName =
                request.BeneficiaryName?.Trim() ?? string.Empty;
            request.Instructions = string.IsNullOrWhiteSpace(request.Instructions)
                ? null
                : request.Instructions.Trim();

            request.InstructionsEn = string.IsNullOrWhiteSpace(request.InstructionsEn)
                ? null
                : request.InstructionsEn.Trim();

            if (!string.IsNullOrWhiteSpace(request.Instructions) &&
                string.IsNullOrWhiteSpace(request.InstructionsEn))
            {
                ModelState.AddModelError(
                    nameof(request.InstructionsEn),
                    "اكتب تعليمات التحويل بالإنجليزية أيضاً.");
            }

            if (ContainsArabicCharacters(request.InstructionsEn))
            {
                ModelState.AddModelError(
                    nameof(request.InstructionsEn),
                    "تعليمات التحويل الإنجليزية لا يجب أن تحتوي على أحرف عربية.");
            }

            if (request.PlatformWalletId.HasValue &&
                request.PlatformWalletId.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(request.PlatformWalletId),
                    "رقم حساب المنصة غير صحيح.");
            }

            if (request.IsPrimary && !request.IsActive)
            {
                ModelState.AddModelError(
                    nameof(request.IsPrimary),
                    "لا يمكن جعل حساب موقوف هو الحساب الرئيسي.");
            }

            if (request.Instructions?.Length > 2000)
            {
                ModelState.AddModelError(
                    nameof(request.Instructions),
                    "تعليمات التحويل يجب ألا تتجاوز 2000 حرف.");
            }

            if (request.InstructionsEn?.Length > 2000)
            {
                ModelState.AddModelError(
                    nameof(request.InstructionsEn),
                    "تعليمات التحويل الإنجليزية يجب ألا تتجاوز 2000 حرف.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.SavePlatformWalletAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء حفظ حساب المنصة {PlatformWalletId} بواسطة الأدمن {AdminUserId}.",
                    request.PlatformWalletId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر حفظ حساب المنصة. لم يتم تنفيذ التغيير."
                });
            }
        }

        [HttpPost("Wallets/Status")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManagePlatformWallets)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeWalletStatus(
            [FromForm] AdminPlatformWalletStatusRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات تغيير حالة الحساب غير مكتملة."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.ChangePlatformWalletStatusAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId,
                    isActive = request.IsActive
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء تغيير حالة حساب المنصة {PlatformWalletId} بواسطة الأدمن {AdminUserId}.",
                    request.PlatformWalletId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تغيير حالة حساب المنصة. لم يتم تنفيذ التغيير."
                });
            }
        }

        [HttpPost("Wallets/SetPrimary")]
        [RequireAdminPermission(AdminPermissionKeys.Finance.ManagePlatformWallets)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryWallet(
            [FromForm] AdminSetPrimaryPlatformWalletRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات تعيين الحساب الرئيسي غير مكتملة."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            if (!TryGetCurrentAdminUserId(out var adminUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "تعذر تحديد حساب الأدمن الحالي. سجل الدخول من جديد."
                });
            }

            try
            {
                var result =
                    await _adminFinancialService.SetPrimaryPlatformWalletAsync(
                        request,
                        adminUserId,
                        cancellationToken);

                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    entityId = result.EntityId
                };

                return result.Success
                    ? Ok(response)
                    : BadRequest(response);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء تعيين حساب المنصة {PlatformWalletId} كحساب رئيسي بواسطة الأدمن {AdminUserId}.",
                    request.PlatformWalletId,
                    adminUserId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تعيين الحساب الرئيسي. لم يتم تنفيذ التغيير."
                });
            }
        }

        private static bool TryReadFinancialSettingsRequest(
            IFormCollection form,
            out AdminFinancialSettingsUpdateDto request,
            out string error)
        {
            request = new AdminFinancialSettingsUpdateDto
            {
                SettingsSection =
                    form["SettingsSection"].FirstOrDefault()?.Trim().ToLowerInvariant() ??
                    string.Empty,
                DefaultCurrency = CurrencyDisplayService.YemeniRial,
                CategoryCommissions = new List<AdminCategoryCommissionUpdateDto>()
            };
            error = string.Empty;

            switch (request.SettingsSection)
            {
                case "commissions":
                    if (!TryReadFinancialDecimal(
                            form,
                            "CommissionPercentage",
                            "النسبة العامة للعمولة",
                            out var commissionPercentage,
                            out error))
                    {
                        return false;
                    }

                    request.CommissionPercentage = commissionPercentage;

                    for (var index = 0; ; index++)
                    {
                        var idKey = $"CategoryCommissions[{index}].CategoryId";
                        if (!form.ContainsKey(idKey))
                        {
                            break;
                        }

                        if (!TryReadFinancialInteger(
                                form,
                                idKey,
                                "رقم فئة العمولة",
                                out var categoryId,
                                out error))
                        {
                            return false;
                        }

                        var rateKey =
                            $"CategoryCommissions[{index}].CommissionPercentage";
                        var rawRate = form[rateKey].FirstOrDefault();
                        decimal? categoryRate = null;

                        if (!string.IsNullOrWhiteSpace(rawRate))
                        {
                            if (!TryParseFlexibleDecimal(rawRate, out var parsedRate))
                            {
                                error = "نسبة إحدى الفئات غير صحيحة. استخدم رقمًا مثل 5 أو 5.5.";
                                return false;
                            }

                            categoryRate = parsedRate;
                        }

                        request.CategoryCommissions.Add(
                            new AdminCategoryCommissionUpdateDto
                            {
                                CategoryId = categoryId,
                                CommissionPercentage = categoryRate
                            });
                    }

                    return true;

                case "trial":
                    request.OwnerFreeTrialEnabled =
                        ReadFinancialBoolean(form, "OwnerFreeTrialEnabled");

                    if (!TryReadFinancialInteger(
                            form,
                            "OwnerFreeTrialDays",
                            "مدة الفترة المجانية",
                            out var trialDays,
                            out error) ||
                        !TryReadFinancialDecimal(
                            form,
                            "RequiredCommissionGuaranteeBalance",
                            "رصيد الضمان المطلوب بعد الفترة",
                            out var guaranteeBalance,
                            out error))
                    {
                        return false;
                    }

                    request.OwnerFreeTrialDays = trialDays;
                    request.RequiredCommissionGuaranteeBalance = guaranteeBalance;
                    return true;

                case "policies":
                    if (!TryReadFinancialDecimal(
                            form,
                            "MinimumCommissionTopUp",
                            "الحد الأدنى للشحن",
                            out var minimumTopUp,
                            out error) ||
                        !TryReadFinancialDecimal(
                            form,
                            "DefaultLowBalanceThreshold",
                            "حد انخفاض الرصيد",
                            out var lowBalanceThreshold,
                            out error) ||
                        !TryReadFinancialDecimal(
                            form,
                            "DefaultCommissionDebtLimit",
                            "حد الدين",
                            out var debtLimit,
                            out error) ||
                        !TryReadFinancialInteger(
                            form,
                            "CommissionDebtGraceDays",
                            "مهلة سداد الدين",
                            out var debtGraceDays,
                            out error))
                    {
                        return false;
                    }

                    request.MinimumCommissionTopUp = minimumTopUp;
                    request.DefaultLowBalanceThreshold = lowBalanceThreshold;
                    request.DefaultCommissionDebtLimit = debtLimit;
                    request.CommissionDebtGraceDays = debtGraceDays;
                    request.AllowNegativeCommissionBalance =
                        ReadFinancialBoolean(form, "AllowNegativeCommissionBalance");
                    request.SuspendBookingWhenBalanceInsufficient =
                        ReadFinancialBoolean(form, "SuspendBookingWhenBalanceInsufficient");
                    request.HidePlaceWhenCommissionSuspended =
                        ReadFinancialBoolean(form, "HidePlaceWhenCommissionSuspended");
                    request.ReturnCommissionOnCancellation =
                        ReadFinancialBoolean(form, "ReturnCommissionOnCancellation");
                    return true;

                default:
                    error = "قسم الإعدادات المالية المطلوب غير معروف. أعد فتح النافذة وحاول مجددًا.";
                    return false;
            }
        }

        private static bool TryReadFinancialDecimal(
            IFormCollection form,
            string key,
            string fieldName,
            out decimal value,
            out string error)
        {
            var rawValue = form[key].FirstOrDefault();
            if (TryParseFlexibleDecimal(rawValue, out value))
            {
                error = string.Empty;
                return true;
            }

            error = $"قيمة «{fieldName}» غير صحيحة. استخدم رقمًا مثل 20000 أو 20000.50.";
            return false;
        }

        private static bool TryReadFinancialInteger(
            IFormCollection form,
            string key,
            string fieldName,
            out int value,
            out string error)
        {
            var normalized = NormalizeFinancialNumber(form[key].FirstOrDefault());
            if (int.TryParse(
                    normalized,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                error = string.Empty;
                return true;
            }

            error = $"قيمة «{fieldName}» غير صحيحة. استخدم عددًا صحيحًا.";
            return false;
        }

        private static bool TryParseFlexibleDecimal(
            string? rawValue,
            out decimal value)
        {
            var normalized = NormalizeFinancialNumber(rawValue);
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string NormalizeFinancialNumber(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            var input = rawValue.Trim();
            var builder = new StringBuilder(input.Length);

            foreach (var character in input)
            {
                if (character >= '0' && character <= '9')
                {
                    builder.Append(character);
                }
                else if (character >= '٠' && character <= '٩')
                {
                    builder.Append((char)('0' + character - '٠'));
                }
                else if (character >= '۰' && character <= '۹')
                {
                    builder.Append((char)('0' + character - '۰'));
                }
                else if (character == '.' || character == ',' || character == '٫')
                {
                    builder.Append('.');
                }
                else if (character == '-' || character == '+')
                {
                    builder.Append(character);
                }
                else if (character == ' ' || character == '٬')
                {
                    continue;
                }
                else
                {
                    return string.Empty;
                }
            }

            return builder.ToString();
        }

        private static bool ReadFinancialBoolean(
            IFormCollection form,
            string key)
        {
            return form[key].Any(value =>
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                value == "1");
        }

        private void ConvertSettingsForDisplay(
            AdminFinancialSettingsDto? settings,
            string displayCurrency)
        {
            if (settings == null)
            {
                return;
            }

            settings.MinimumCommissionTopUp =
                _currencyService.ConvertAmount(
                    settings.MinimumCommissionTopUp,
                    displayCurrency,
                    FinancialSettingsRegion);

            settings.DefaultLowBalanceThreshold =
                _currencyService.ConvertAmount(
                    settings.DefaultLowBalanceThreshold,
                    displayCurrency,
                    FinancialSettingsRegion);

            settings.DefaultCommissionDebtLimit =
                _currencyService.ConvertAmount(
                    settings.DefaultCommissionDebtLimit,
                    displayCurrency,
                    FinancialSettingsRegion);

            settings.RequiredCommissionGuaranteeBalance =
                _currencyService.ConvertAmount(
                    settings.RequiredCommissionGuaranteeBalance,
                    displayCurrency,
                    FinancialSettingsRegion);
        }

        private void ConvertSettingsInputToYer(
            AdminFinancialSettingsUpdateDto request,
            string displayCurrency)
        {
            request.MinimumCommissionTopUp =
                _currencyService.ConvertToYer(
                    request.MinimumCommissionTopUp,
                    displayCurrency,
                    FinancialSettingsRegion);

            request.DefaultLowBalanceThreshold =
                _currencyService.ConvertToYer(
                    request.DefaultLowBalanceThreshold,
                    displayCurrency,
                    FinancialSettingsRegion);

            request.DefaultCommissionDebtLimit =
                _currencyService.ConvertToYer(
                    request.DefaultCommissionDebtLimit,
                    displayCurrency,
                    FinancialSettingsRegion);

            request.RequiredCommissionGuaranteeBalance =
                _currencyService.ConvertToYer(
                    request.RequiredCommissionGuaranteeBalance,
                    displayCurrency,
                    FinancialSettingsRegion);
        }

        private static bool ContainsArabicCharacters(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (char character in value)
            {
                if ((character >= '؀' && character <= 'ۿ') ||
                    (character >= 'ݐ' && character <= 'ݿ') ||
                    (character >= 'ࢠ' && character <= 'ࣿ'))
                {
                    return true;
                }
            }

            return false;
        }


        private static bool TryNormalizeAccountNumber(
            string? value,
            out string normalized)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var input = value.Trim();
            var builder = new StringBuilder(input.Length);

            foreach (var character in input)
            {
                if (character >= '0' && character <= '9')
                {
                    builder.Append(character);
                    continue;
                }

                if (character >= '٠' && character <= '٩')
                {
                    builder.Append((char)('0' + (character - '٠')));
                    continue;
                }

                if (character >= '۰' && character <= '۹')
                {
                    builder.Append((char)('0' + (character - '۰')));
                    continue;
                }

                return false;
            }

            normalized = builder.ToString();
            return normalized.Length > 0 && normalized.Length <= 120;
        }

        private bool TryGetCurrentAdminUserId(out int adminUserId)
        {
            adminUserId = 0;

            var rawValue =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("UserID") ??
                User.FindFirstValue("UserId") ??
                User.FindFirstValue("user_id") ??
                User.FindFirstValue("sub");

            return int.TryParse(rawValue, out adminUserId) &&
                   adminUserId > 0;
        }

        private string FirstModelError()
        {
            return ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error =>
                    !string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.ErrorMessage
                        : error.Exception?.Message)
                .FirstOrDefault(message =>
                    !string.IsNullOrWhiteSpace(message)) ??
                "البيانات المرسلة غير صحيحة.";
        }

        private void DisableResponseCaching()
        {
            Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }
    }
}
