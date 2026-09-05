// =============================================================
// ProfileController.cs
// التحكم في الملف الشخصي للعميل + إكمال البيانات المطلوبة للحجز
// =============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Core.Entities;
using YemenGuide.Core.Helpers;
using YemenGuide.Data;

namespace YemenGuide.Web.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfileController(
            AppDbContext context,
            IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /* =============================================================
         * أدوات مشتركة
         * ============================================================= */

        private bool TryGetCurrentUserId(
            out int currentUserId)
        {
            currentUserId = 0;

            var userIdClaim =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                       userIdClaim,
                       out currentUserId) &&
                   currentUserId > 0;
        }

        private static bool IsCustomerRole(
            string? role)
        {
            return string.Equals(
                role?.Trim(),
                "Customer",
                StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeLocalReturnUrl(
            string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(
                    returnUrl))
            {
                return string.Empty;
            }

            var trimmed =
                returnUrl.Trim();

            return Url.IsLocalUrl(trimmed)
                ? trimmed
                : string.Empty;
        }

        private static string NormalizeDigits(
            string? value)
        {
            return (value ?? string.Empty)
                .Replace('٠', '0')
                .Replace('١', '1')
                .Replace('٢', '2')
                .Replace('٣', '3')
                .Replace('٤', '4')
                .Replace('٥', '5')
                .Replace('٦', '6')
                .Replace('٧', '7')
                .Replace('٨', '8')
                .Replace('٩', '9')
                .Replace('۰', '0')
                .Replace('۱', '1')
                .Replace('۲', '2')
                .Replace('۳', '3')
                .Replace('۴', '4')
                .Replace('۵', '5')
                .Replace('۶', '6')
                .Replace('۷', '7')
                .Replace('۸', '8')
                .Replace('۹', '9');
        }

        private static string NormalizePreferredCurrency(
            string? value)
        {
            var normalized =
                (value ?? string.Empty).Trim();

            return normalized switch
            {
                "دولار أمريكي" => "دولار أمريكي",
                "ريال سعودي" => "ريال سعودي",
                "ريال يمني" => "ريال يمني",
                "ريال يمني (قديم)" => "ريال يمني",
                "ريال يمني (جديد)" => "ريال يمني",
                _ => "ريال يمني"
            };
        }

        private static string NormalizePhoneNumber(
            string? value)
        {
            var normalized =
                NormalizeDigits(value)
                    .Trim();

            normalized =
                Regex.Replace(
                    normalized,
                    @"[\s\-\(\)\.]",
                    string.Empty);

            if (normalized.StartsWith(
                    "00",
                    StringComparison.Ordinal))
            {
                normalized =
                    "+" +
                    normalized[2..];
            }

            return normalized;
        }

        private static bool IsValidPhoneNumber(
            string? value)
        {
            var normalized =
                NormalizePhoneNumber(
                    value);

            if (!Regex.IsMatch(
                    normalized,
                    @"^\+?[1-9]\d{6,14}$",
                    RegexOptions.CultureInvariant))
            {
                return false;
            }

            var digitsOnly =
                normalized.TrimStart('+');

            return digitsOnly.Any(
                digit =>
                    digit != '0');
        }

        private static bool IsValidEmail(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }

            return Regex.IsMatch(
                value.Trim(),
                @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
                RegexOptions.CultureInvariant);
        }

        private static List<string>
            GetMissingBookingProfileFields(
                User customer)
        {
            var missingFields =
                new List<string>();

            if (string.IsNullOrWhiteSpace(
                    customer.FullName) ||
                customer.FullName.Trim().Length < 2)
            {
                missingFields.Add(
                    "الاسم الكامل");
            }

            if (!IsValidEmail(
                    customer.ContactInfo))
            {
                missingFields.Add(
                    "البريد الإلكتروني");
            }
            else if (!customer.IsEmailVerified)
            {
                missingFields.Add(
                    "توثيق البريد الإلكتروني");
            }

            if (!IsValidPhoneNumber(
                    customer.PhoneNumber))
            {
                missingFields.Add(
                    "رقم الهاتف");
            }

            if (string.IsNullOrWhiteSpace(
                    customer.City))
            {
                missingFields.Add(
                    "المدينة");
            }

            return missingFields;
        }

        private async Task<User?>
            GetCurrentCustomerAsync()
        {
            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return null;
            }

            return await _context.Users
                .FirstOrDefaultAsync(
                    user =>
                        user.UserId ==
                        currentUserId);
        }

        private IActionResult UnauthorizedJson()
        {
            return Unauthorized(
                new
                {
                    success = false,
                    message =
                        "انتهت جلسة تسجيل الدخول. يرجى تسجيل الدخول مرة أخرى."
                });
        }

        /* =============================================================
         * 1. عرض الملف الشخصي
         * ============================================================= */

        [HttpGet("/Profile/CustomerProfile")]
        public async Task<IActionResult>
            CustomerProfile(
                string? returnUrl = null)
        {
            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return Challenge();
            }

            var customerUser =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        user =>
                            user.UserId ==
                            currentUserId);

            if (customerUser == null)
            {
                return NotFound(
                    "حساب العميل غير موجود في النظام.");
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return Forbid();
            }

            var safeReturnUrl =
                NormalizeLocalReturnUrl(
                    returnUrl);

            var missingFields =
                GetMissingBookingProfileFields(
                    customerUser);

            ViewData["BookingReturnUrl"] =
                safeReturnUrl;

            ViewData["BookingProfileIsComplete"] =
                missingFields.Count == 0;

            ViewData["BookingProfileMissingFields"] =
                missingFields;

            var viewModel =
                new CustomerProfileDto
                {
                    UserId =
                        customerUser.UserId,

                    FullName =
                        customerUser.FullName,

                    Username =
                        customerUser.Username,

                    ContactInfo =
                        customerUser.ContactInfo ??
                        string.Empty,

                    IsEmailVerified =
                        customerUser.IsEmailVerified,

                    PhoneNumber =
                        customerUser.PhoneNumber ??
                        string.Empty,

                    Bio =
                        customerUser.Bio ??
                        string.Empty,

                    DateOfBirth =
                        customerUser.DateOfBirth,

                    Gender =
                        customerUser.Gender ??
                        string.Empty,

                    City =
                        customerUser.City ??
                        string.Empty,

                    WalletBalance =
                        customerUser.WalletBalance,

                    FacebookUrl =
                        customerUser.FacebookUrl ??
                        string.Empty,

                    TwitterUrl =
                        customerUser.TwitterUrl ??
                        string.Empty,

                    InstagramUrl =
                        customerUser.InstagramUrl ??
                        string.Empty,

                    ReceiveOffers =
                        customerUser.ReceiveOffers ??
                        true,

                    PreferredCurrency =
                        NormalizePreferredCurrency(
                            customerUser.PreferredCurrency),

                    Role =
                        customerUser.Role,

                    AccountState =
                        customerUser.AccountState,

                    CreatedAt =
                        customerUser.CreatedAt ??
                        DateTime.Now,

                    DarkModeEnabled =
                        customerUser.IsDarkMode,

                    Language =
                        customerUser.PreferredLanguage ??
                        "ar",

                    InitialLetter =
                        !string.IsNullOrWhiteSpace(
                            customerUser.FullName)
                            ? customerUser
                                .FullName
                                .Trim()
                                .Substring(
                                    0,
                                    1)
                                .ToUpper()
                            : "C",

                    ProfileImage =
                        customerUser.ProfileImage ??
                        string.Empty
                };

            return View(
                "~/Views/Panel/Profile/CustomerProfile.cshtml",
                viewModel);
        }

        /* =============================================================
         * 2. إرسال رمز توثيق البريد أو تغييره
         * ============================================================= */

        [HttpPost("/Profile/RequestEmailChange")]
        public async Task<IActionResult>
            RequestEmailChange(
                [FromBody]
                EmailChangeRequestDto model)
        {
            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return UnauthorizedJson();
            }

            var customerUser =
                await _context.Users
                    .FirstOrDefaultAsync(
                        user =>
                            user.UserId ==
                            currentUserId);

            if (customerUser == null)
            {
                return NotFound(
                    new
                    {
                        success = false,
                        message =
                            "حساب العميل غير موجود."
                    });
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            var newEmail =
                model?.NewEmail?
                    .Trim() ??
                string.Empty;

            if (!IsValidEmail(
                    newEmail))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "البريد الإلكتروني المدخل غير صالح."
                    });
            }

            var isCurrentEmail =
                string.Equals(
                    customerUser.ContactInfo?
                        .Trim(),
                    newEmail,
                    StringComparison.OrdinalIgnoreCase);

            if (isCurrentEmail &&
                customerUser.IsEmailVerified)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "هذا البريد موثق في حسابك بالفعل."
                    });
            }

            var isEmailTaken =
                await _context.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user =>
                            user.UserId !=
                                currentUserId &&
                            user.ContactInfo != null &&
                            user.ContactInfo ==
                                newEmail);

            if (isEmailTaken)
            {
                return Conflict(
                    new
                    {
                        success = false,
                        message =
                            "عذرًا، البريد الإلكتروني مستخدم لحساب آخر."
                    });
            }

            var otpCode =
                Random.Shared
                    .Next(
                        100000,
                        1000000)
                    .ToString();

            customerUser.VerificationCode =
                otpCode;

            customerUser.CodeExpiry =
                DateTime.Now
                    .AddMinutes(5);

            /*
             * يحتفظ مسار التحقق الحالي بالبريد في TempData،
             * سواء كان بريدًا جديدًا أو البريد الحالي غير الموثق.
             */
            TempData["PendingEmail"] =
                newEmail;

            await _context.SaveChangesAsync();

            _ = EmailHelper.SendOtpEmailAsync(
                newEmail,
                otpCode);

            var safeReturnUrl =
                NormalizeLocalReturnUrl(
                    model?.ReturnUrl);

            var verifyUrl =
                Url.Action(
                    "VerifyOtp",
                    "Account",
                    new
                    {
                        email =
                            newEmail,

                        type =
                            "update",

                        returnUrl =
                            string.IsNullOrWhiteSpace(
                                safeReturnUrl)
                                ? null
                                : safeReturnUrl
                    }) ??
                $"/Account/VerifyOtp?email={Uri.EscapeDataString(newEmail)}&type=update";

            return Json(
                new
                {
                    success = true,
                    message =
                        "تم إرسال رمز التحقق إلى البريد الإلكتروني.",
                    verifyUrl
                });
        }

        /* =============================================================
         * 3. تحديث البيانات الشخصية
         * ============================================================= */

        [HttpPost("/Profile/UpdatePersonalInfo")]
        public async Task<IActionResult>
            UpdatePersonalInfo(
                [FromBody]
                CustomerPersonalInfoUpdateDto model)
        {
            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return UnauthorizedJson();
            }

            if (model == null)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "بيانات التحديث غير صالحة."
                    });
            }

            var customerUser =
                await _context.Users
                    .FirstOrDefaultAsync(
                        user =>
                            user.UserId ==
                            currentUserId);

            if (customerUser == null)
            {
                return NotFound(
                    new
                    {
                        success = false,
                        message =
                            "حساب العميل غير موجود."
                    });
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            var fullName =
                model.FullName?
                    .Trim() ??
                string.Empty;

            if (fullName.Length < 2)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        field = "FullName",
                        message =
                            "الاسم الكامل مطلوب ويجب ألا يقل عن حرفين."
                    });
            }

            var newUsername =
                model.Username?
                    .Trim() ??
                string.Empty;

            if (!Regex.IsMatch(
                    newUsername,
                    @"^[a-zA-Z0-9_]{3,20}$",
                    RegexOptions.CultureInvariant))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        field = "Username",
                        message =
                            "اسم المستخدم غير صالح. استخدم أحرفًا إنجليزية وأرقامًا وشرطة سفلية فقط."
                    });
            }

            var isUsernameTaken =
                await _context.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user =>
                            user.UserId !=
                                currentUserId &&
                            user.Username ==
                                newUsername);

            if (isUsernameTaken)
            {
                return Conflict(
                    new
                    {
                        success = false,
                        field = "Username",
                        message =
                            "اسم المستخدم محجوز مسبقًا، يرجى اختيار اسم آخر."
                    });
            }

            var newEmail =
                model.ContactInfo?
                    .Trim() ??
                string.Empty;

            if (!IsValidEmail(
                    newEmail))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        field = "ContactInfo",
                        message =
                            "البريد الإلكتروني المدخل غير صالح."
                    });
            }

            if (!string.Equals(
                    customerUser.ContactInfo?
                        .Trim(),
                    newEmail,
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        requiresEmailVerification = true,
                        field = "ContactInfo",
                        message =
                            "تغيير البريد الإلكتروني يتطلب التحقق الأمني أولًا."
                    });
            }

            var normalizedPhone =
                NormalizePhoneNumber(
                    model.PhoneNumber);

            if (!IsValidPhoneNumber(
                    normalizedPhone))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        field = "PhoneNumber",
                        message =
                            "رقم الهاتف غير صالح. أدخله بصيغة دولية صحيحة."
                    });
            }

            var city =
                model.City?
                    .Trim() ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(
                    city))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        field = "City",
                        message =
                            "المدينة مطلوبة قبل إنشاء الحجز."
                    });
            }

            customerUser.FullName =
                fullName;

            customerUser.Username =
                newUsername;

            customerUser.PhoneNumber =
                normalizedPhone;

            customerUser.City =
                city;

            customerUser.Bio =
                model.Bio?
                    .Trim();

            customerUser.DateOfBirth =
                model.DateOfBirth;

            customerUser.Gender =
                model.Gender?
                    .Trim();

            customerUser.FacebookUrl =
                model.FacebookUrl?
                    .Trim();

            customerUser.TwitterUrl =
                model.TwitterUrl?
                    .Trim();

            customerUser.InstagramUrl =
                model.InstagramUrl?
                    .Trim();

            await _context.SaveChangesAsync();

            var missingFields =
                GetMissingBookingProfileFields(
                    customerUser);

            return Json(
                new
                {
                    success = true,
                    message =
                        "تم تحديث البيانات الشخصية بنجاح.",
                    profileComplete =
                        missingFields.Count == 0,
                    missingFields
                });
        }

        /* =============================================================
         * 4. رفع الصورة الشخصية
         * ============================================================= */

        [HttpPost("/Profile/UploadProfileImage")]
        public async Task<IActionResult>
            UploadProfileImage(
                IFormFile profileImage)
        {
            var customerUser =
                await GetCurrentCustomerAsync();

            if (customerUser == null)
            {
                return UnauthorizedJson();
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            if (profileImage == null ||
                profileImage.Length <= 0)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "لم يتم اختيار صورة صالحة للرفع."
                    });
            }

            const long maximumFileSize =
                5 * 1024 * 1024;

            if (profileImage.Length >
                maximumFileSize)
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "حجم الصورة يجب ألا يتجاوز 5 ميجابايت."
                    });
            }

            var extension =
                Path.GetExtension(
                    profileImage.FileName)
                    .ToLowerInvariant();

            var allowedExtensions =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".webp"
                };

            if (!allowedExtensions.Contains(
                    extension))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "صيغة الصورة غير مدعومة. استخدم JPG أو PNG أو WEBP."
                    });
            }

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var uploadFolder =
                Path.Combine(
                    _env.WebRootPath,
                    "images",
                    "profiles");

            Directory.CreateDirectory(
                uploadFolder);

            var filePath =
                Path.Combine(
                    uploadFolder,
                    fileName);

            await using (
                var stream =
                    new FileStream(
                        filePath,
                        FileMode.Create))
            {
                await profileImage
                    .CopyToAsync(
                        stream);
            }

            customerUser.ProfileImage =
                $"/images/profiles/{fileName}";

            await _context.SaveChangesAsync();

            return Json(
                new
                {
                    success = true,
                    imageUrl =
                        customerUser.ProfileImage
                });
        }

        /* =============================================================
         * 5. حذف الصورة الشخصية
         * ============================================================= */

        [HttpPost("/Profile/DeleteImage")]
        public async Task<IActionResult>
            DeleteImage()
        {
            var customerUser =
                await GetCurrentCustomerAsync();

            if (customerUser == null)
            {
                return UnauthorizedJson();
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            customerUser.ProfileImage =
                null;

            await _context.SaveChangesAsync();

            var initialLetter =
                !string.IsNullOrWhiteSpace(
                    customerUser.FullName)
                    ? customerUser
                        .FullName
                        .Trim()
                        .Substring(
                            0,
                            1)
                        .ToUpper()
                    : "C";

            return Json(
                new
                {
                    success = true,
                    initialLetter
                });
        }

        /* =============================================================
         * 6. تغيير كلمة المرور
         * ============================================================= */

        [HttpPost("/Profile/ChangePassword")]
        public async Task<IActionResult>
            ChangePassword(
                [FromBody]
                CustomerPasswordUpdateDto model)
        {
            var customerUser =
                await GetCurrentCustomerAsync();

            if (customerUser == null)
            {
                return UnauthorizedJson();
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            if (model == null ||
                string.IsNullOrWhiteSpace(
                    model.CurrentPassword))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "كلمة المرور الحالية مطلوبة."
                    });
            }

            if (!BCrypt.Net.BCrypt.Verify(
                    model.CurrentPassword,
                    customerUser.PasswordHash))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "كلمة المرور الحالية غير صحيحة."
                    });
            }

            if (string.IsNullOrEmpty(
                    model.NewPassword) ||
                !Regex.IsMatch(
                    model.NewPassword,
                    @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$",
                    RegexOptions.CultureInvariant))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "يجب أن تتكون كلمة المرور الجديدة من 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم."
                    });
            }

            if (BCrypt.Net.BCrypt.Verify(
                    model.NewPassword,
                    customerUser.PasswordHash))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "كلمة المرور الجديدة يجب أن تختلف عن الحالية."
                    });
            }

            customerUser.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    model.NewPassword,
                    workFactor: 11);

            await _context.SaveChangesAsync();

            return Json(
                new
                {
                    success = true,
                    message =
                        "تم تحديث كلمة المرور بنجاح."
                });
        }

        /* =============================================================
         * 7. حفظ التفضيلات
         * ============================================================= */

        [HttpPost("/Profile/SetPreference")]
        public async Task<IActionResult>
            SetPreference(
                [FromBody]
                CustomerPreferenceDto model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(
                    model.Key))
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "بيانات التفضيل غير صالحة."
                    });
            }

            var customerUser =
                await GetCurrentCustomerAsync();

            if (customerUser == null)
            {
                return UnauthorizedJson();
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            string actualValue;

            if (model.Value is
                System.Text.Json.JsonElement jsonElement &&
                jsonElement.ValueKind ==
                    System.Text.Json.JsonValueKind.String)
            {
                actualValue =
                    jsonElement.GetString() ??
                    string.Empty;
            }
            else
            {
                actualValue =
                    model.Value?
                        .ToString() ??
                    string.Empty;
            }

            var isEnabled =
                string.Equals(
                    actualValue,
                    "true",
                    StringComparison.OrdinalIgnoreCase);

            var cookieOptions =
                new CookieOptions
                {
                    Expires =
                        DateTimeOffset.UtcNow
                            .AddYears(1),

                    Path =
                        "/",

                    HttpOnly =
                        false,

                    IsEssential =
                        true
                };

            if (string.Equals(
                    model.Key,
                    "DarkModeEnabled",
                    StringComparison.OrdinalIgnoreCase))
            {
                Response.Cookies.Append(
                    "darkMode",
                    isEnabled
                        ? "enabled"
                        : "disabled",
                    cookieOptions);

                customerUser.IsDarkMode =
                    isEnabled;
            }
            else if (string.Equals(
                         model.Key,
                         "Language",
                         StringComparison.OrdinalIgnoreCase))
            {
                customerUser.PreferredLanguage =
                    actualValue;
            }
            else if (string.Equals(
                         model.Key,
                         "ReceiveOffers",
                         StringComparison.OrdinalIgnoreCase))
            {
                customerUser.ReceiveOffers =
                    isEnabled;
            }
            else if (string.Equals(
                         model.Key,
                         "Currency",
                         StringComparison.OrdinalIgnoreCase))
            {
                customerUser.PreferredCurrency =
                    actualValue;
            }

            /*
             * CompactMode يبقى محفوظًا في الكوكي والـLocalStorage
             * من JavaScript كما كان، ولا يحتاج عمودًا جديدًا.
             */
            await _context.SaveChangesAsync();

            return Json(
                new
                {
                    success = true
                });
        }

        /* =============================================================
         * 8. تعطيل الحساب مؤقتًا
         * ============================================================= */

        [HttpPost("/Profile/SuspendAccount")]
        public async Task<IActionResult>
            SuspendAccount()
        {
            var customerUser =
                await GetCurrentCustomerAsync();

            if (customerUser == null)
            {
                return UnauthorizedJson();
            }

            if (!IsCustomerRole(
                    customerUser.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            customerUser.AccountState =
                "Suspended";

            await _context.SaveChangesAsync();

            return Json(
                new
                {
                    success = true
                });
        }
    }

    /* =============================================================
     * DTO طلب توثيق أو تغيير البريد
     * ============================================================= */

    public class EmailChangeRequestDto
    {
        public string NewEmail { get; set; } =
            string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
