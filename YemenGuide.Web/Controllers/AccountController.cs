// =============================================================
//  AccountController.cs - التحكم في حسابات المستخدمين المطور والمحمي تفاعلياً بالكامل (مع دعم OTP والترقية)
// =============================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Hosting;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using YemenGuide.Core.Entities;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Enums;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Data;
using YemenGuide.Core.Helpers;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly INotificationService _notificationService;

        private const int OtpLifetimeMinutes = 2;
        private const int OtpResendCooldownSeconds = 45;
        private const int OtpMaxAttempts = 5;

        private const string PendingReturnUrlCookie =
            "YemenGuide.PendingReturnUrl";

        // =============================================================
        //  3. المُنشئ (Constructor)
        // =============================================================
        public AccountController(
            AppDbContext context,
            IWebHostEnvironment env,
            IMemoryCache cache,
            INotificationService notificationService)
        {
            _context = context;
            _env = env;
            _cache = cache;
            _notificationService = notificationService;
        }

        private string NormalizeLocalReturnUrl(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed =
                value.Trim();

            if (Url.IsLocalUrl(trimmed))
            {
                return trimmed;
            }

            if (Uri.TryCreate(
                    trimmed,
                    UriKind.Absolute,
                    out var absoluteUri) &&
                string.Equals(
                    absoluteUri.Host,
                    Request.Host.Host,
                    StringComparison.OrdinalIgnoreCase) &&
                (
                    !Request.Host.Port.HasValue ||
                    absoluteUri.IsDefaultPort ||
                    absoluteUri.Port ==
                    Request.Host.Port.Value
                ))
            {
                return
                    absoluteUri.PathAndQuery +
                    absoluteUri.Fragment;
            }

            return string.Empty;
        }

        private void RememberReturnUrl(
            string? returnUrl)
        {
            var safeReturnUrl =
                NormalizeLocalReturnUrl(
                    returnUrl);

            if (string.IsNullOrWhiteSpace(
                    safeReturnUrl))
            {
                return;
            }

            Response.Cookies.Append(
                PendingReturnUrlCookie,
                safeReturnUrl,
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    MaxAge = TimeSpan.FromMinutes(30),
                    Path = "/"
                });
        }

        private string PeekReturnUrl(
            string? explicitReturnUrl = null)
        {
            var explicitSafe =
                NormalizeLocalReturnUrl(
                    explicitReturnUrl);

            if (!string.IsNullOrWhiteSpace(
                    explicitSafe))
            {
                RememberReturnUrl(
                    explicitSafe);

                return explicitSafe;
            }

            if (Request.Cookies.TryGetValue(
                    PendingReturnUrlCookie,
                    out var stored))
            {
                return NormalizeLocalReturnUrl(
                    stored);
            }

            return string.Empty;
        }

        private string ConsumeReturnUrl(
            string? explicitReturnUrl = null)
        {
            var safeReturnUrl =
                PeekReturnUrl(
                    explicitReturnUrl);

            if (!string.IsNullOrWhiteSpace(
                    safeReturnUrl))
            {
                Response.Cookies.Delete(
                    PendingReturnUrlCookie,
                    new CookieOptions
                    {
                        Path = "/"
                    });
            }

            return safeReturnUrl;
        }

        private async Task<string>
            ResolvePostAuthenticationUrlAsync(
                User user,
                string? explicitReturnUrl = null,
                bool consume = true)
        {
            var requestedUrl =
                consume
                    ? ConsumeReturnUrl(
                        explicitReturnUrl)
                    : PeekReturnUrl(
                        explicitReturnUrl);

            if (
                string.Equals(
                    user.Role,
                    SystemRoles.Customer,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(
                    requestedUrl)
            )
            {
                return requestedUrl;
            }

            return await GetUserLandingUrlAsync(
                user);
        }

        private void QueueCustomerAccountNotification(
            User user,
            string title,
            string text,
            string notificationType)
        {
            if (!string.Equals(
                    user.Role,
                    SystemRoles.Customer,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _notificationService.Queue(
                new CreateNotificationDto
                {
                    UserId =
                        user.UserId,
                    NotificationTitle =
                        title,
                    NotificationText =
                        text,
                    NotificationType =
                        notificationType,
                    TargetUrl =
                        "/Profile/CustomerProfile",
                    ReferenceType =
                        NotificationReferenceTypes.User,
                    ReferenceId =
                        user.UserId
                });
        }

        // =============================================================
        //  4. عرض صفحة تسجيل الدخول (Login GET)
        // =============================================================
        [HttpGet]
        public IActionResult Login(
            string? returnUrl = null)
        {
            RememberReturnUrl(
                returnUrl);

            ViewData["ReturnUrl"] =
                PeekReturnUrl();

            return View();
        }

        // =============================================================
        //  5. عرض صفحة إنشاء حساب (Register GET)
        // =============================================================
        [HttpGet]
        public IActionResult Register(
            string? returnUrl = null)
        {
            RememberReturnUrl(
                returnUrl);

            ViewData["ReturnUrl"] =
                PeekReturnUrl();

            return View();
        }

        // =============================================================
        //  6. تسجيل الدخول بالبريد أو اسم المستخدم
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string emailOrUsername,
            string password,
            bool rememberMe = false,
            string? returnUrl = null)
        {
            RememberReturnUrl(
                returnUrl);

            string loginKey = (emailOrUsername ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(loginKey) || string.IsNullOrEmpty(password))
            {
                return Json(new
                {
                    success = false,
                    message = "يرجى إدخال البريد/اسم المستخدم وكلمة المرور."
                });
            }

            string normalizedEmail = NormalizeEmail(loginKey);
            string normalizedUsername = NormalizeUsername(loginKey);

            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.ContactInfo.ToLower() == normalizedEmail ||
                    u.Username.ToLower() == normalizedUsername);

            bool passwordValid = false;
            if (user != null && !string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                try
                {
                    passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                catch
                {
                    passwordValid = false;
                }
            }

            if (user == null || !passwordValid)
            {
                return Json(new
                {
                    success = false,
                    message = "البيانات المدخلة (البريد/المستخدم أو كلمة المرور) غير صحيحة."
                });
            }

            var accountAccessError = await GetAccountAccessErrorAsync(user);
            if (accountAccessError != null)
            {
                return Json(new { success = false, message = accountAccessError });
            }

            if (!user.IsEmailVerified)
            {
                var sendResult = await TrySendOtpAsync(user, "verify", isPasswordReset: false);

                return Json(new
                {
                    success = false,
                    isEmailUnverified = true,
                    email = user.ContactInfo,
                    retryAfter = sendResult.retryAfter,
                    message = sendResult.sent
                        ? "حسابك غير مفعل بعد. أرسلنا رمز تحقق جديداً إلى بريدك."
                        : "حسابك غير مفعل بعد. استخدم رمز التحقق المرسل مسبقاً أو أعد الإرسال بعد انتهاء العداد."
                });
            }

            await SignInUserAsync(user, rememberMe);

            if (user.Role == SystemRoles.Admin)
            {
                user.LastLoginAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    url = await GetAdminEntryUrlAsync(user)
                });
            }

            // حساب Google الجديد يجب أن ينشئ كلمة مرور حقيقية قبل المتابعة.
            // في المرحلة الثانية سيظهر هذا كـ Modal فخم بدل الصفحة المؤقتة.
            if (user.MustChangePassword)
            {
                return Json(new
                {
                    success = true,
                    url = "/Account/ForceChangePassword?source=google"
                });
            }

            if (user.AccountState == "ForcePasswordChange")
            {
                return Json(new
                {
                    success = true,
                    url = "/Account/ForceChangePassword"
                });
            }

            return Json(new
            {
                success = true,
                url =
                    await ResolvePostAuthenticationUrlAsync(
                        user)
            });
        }

        // =============================================================
        //  7. إنشاء حساب عادي + OTP آمن
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string firstName,
            string lastName,
            string contactInfo,
            string username,
            string password,
            string role)
        {
            string email = NormalizeEmail(contactInfo);
            string cleanUsername = NormalizeUsername(username);
            string cleanFirstName = (firstName ?? string.Empty).Trim();
            string cleanLastName = (lastName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(cleanFirstName) || string.IsNullOrWhiteSpace(cleanLastName))
                return Json(new { success = false, message = "الاسم الأول واللقب مطلوبان." });

            if (!IsValidEmail(email))
                return Json(new { success = false, message = "يرجى إدخال بريد إلكتروني صحيح." });

            if (!System.Text.RegularExpressions.Regex.IsMatch(cleanUsername, @"^[a-zA-Z0-9_]{3,20}$"))
                return Json(new { success = false, message = "اسم المستخدم يجب أن يكون من 3 إلى 20 حرفاً ويحتوي على أحرف إنجليزية أو أرقام أو _ فقط." });

            var passwordError = GetPasswordPolicyError(password);
            if (passwordError != null)
                return Json(new { success = false, message = passwordError });

            if (await _context.Users.AnyAsync(u => u.ContactInfo.ToLower() == email))
                return Json(new { success = false, message = "عذراً، البريد الإلكتروني هذا مستخدم مسبقاً!" });

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername))
                return Json(new { success = false, message = "عذراً، اسم المستخدم هذا محجوز مسبقاً، يرجى اختيار اسم آخر!" });

            string safeRole = string.Equals(role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase)
                ? SystemRoles.Owner
                : SystemRoles.Customer;

            string accountState = safeRole == SystemRoles.Owner ? "Pending" : "Active";
            string otpCode = GenerateOtpCode();

            var newUser = new User
            {
                FullName = $"{cleanFirstName} {cleanLastName}".Trim(),
                Username = cleanUsername,
                ContactInfo = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11),
                Role = safeRole,
                AccountState = accountState,
                IsDarkMode = false,
                PreferredLanguage = "ar",
                PhoneNumber = null,
                IsEmailVerified = false,
                MustChangePassword = false,
                VerificationCode = otpCode,
                CodeExpiry = DateTime.Now.AddMinutes(OtpLifetimeMinutes)
            };

            _context.Users.Add(newUser);

            try
            {
                await _context.SaveChangesAsync();
                await EmailHelper.SendOtpEmailAsync(email, otpCode);
                MarkOtpSent(email, "verify");
                ResetOtpAttempts(email, "verify");
            }
            catch
            {
                // إذا فشل البريد أثناء إنشاء الحساب نعيد قاعدة البيانات كما كانت حتى لا نترك حساباً عالقاً.
                _context.Users.Remove(newUser);
                try { await _context.SaveChangesAsync(); } catch { }

                return Json(new
                {
                    success = false,
                    message = "تعذر إرسال رمز التحقق حالياً. يرجى المحاولة بعد قليل."
                });
            }

            var pendingReturnUrl =
                PeekReturnUrl();

            string redirectUrl =
                $"/Account/VerifyOtp?email={Uri.EscapeDataString(email)}";

            if (!string.IsNullOrWhiteSpace(
                    pendingReturnUrl))
            {
                redirectUrl +=
                    $"&returnUrl={Uri.EscapeDataString(pendingReturnUrl)}";
            }

            return Json(new
            {
                success = true,
                isOtpRedirect = true,
                url = redirectUrl,
                message = "تم إنشاء الحساب! جاري توجيهك لتأكيد البريد..."
            });
        }

        // =============================================================
        //  7.1 عرض شاشة إدخال رمز التحقق
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> VerifyOtp(
            string email,
            string type = null,
            string? returnUrl = null)
        {
            RememberReturnUrl(
                returnUrl);

            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Login");

            var normalizedEmail = NormalizeEmail(email);
            var purpose = string.Equals(type, "update", StringComparison.OrdinalIgnoreCase)
                ? "update"
                : "verify";

            User? otpUser = null;

            if (purpose == "update")
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out var currentUserId))
                {
                    otpUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UserId == currentUserId);
                }
            }
            else
            {
                otpUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.ContactInfo.ToLower() == normalizedEmail);
            }

            var expiresInSeconds = 0;
            if (otpUser?.CodeExpiry != null)
            {
                expiresInSeconds = Math.Max(
                    0,
                    (int)Math.Ceiling((otpUser.CodeExpiry.Value - DateTime.Now).TotalSeconds));
            }

            ViewBag.Email = normalizedEmail;
            ViewBag.Type = type;
            ViewBag.OtpExpiresInSeconds = expiresInSeconds;
            ViewBag.OtpResendCooldownSeconds = GetOtpRetryAfter(normalizedEmail, purpose);

            return View();
        }

        // =============================================================
        //  7.2 التحقق من OTP + حماية عدد المحاولات
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(
            string email,
            string code,
            string type = null,
            string? returnUrl = null)
        {
            RememberReturnUrl(
                returnUrl);

            email = NormalizeEmail(email);
            code = (code ?? string.Empty).Trim();

            if (!IsValidEmail(email) || !System.Text.RegularExpressions.Regex.IsMatch(code, @"^\d{6}$"))
                return Json(new { success = false, message = "الرمز يجب أن يتكون من 6 أرقام." });

            string purpose = string.Equals(type, "update", StringComparison.OrdinalIgnoreCase)
                ? "update"
                : "verify";

            if (GetOtpAttempts(email, purpose) >= OtpMaxAttempts)
            {
                return Json(new
                {
                    success = false,
                    attemptsExceeded = true,
                    message = "تم تجاوز عدد المحاولات المسموحة. اطلب رمزاً جديداً ثم حاول مرة أخرى."
                });
            }

            if (purpose == "update")
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                    return Json(new { success = false, message = "انتهت الجلسة، يرجى تسجيل الدخول مجدداً." });

                var currentUser = await _context.Users.FindAsync(currentUserId);
                if (currentUser == null)
                    return Json(new { success = false, message = "المستخدم غير موجود." });

                if (currentUser.CodeExpiry == null || currentUser.CodeExpiry < DateTime.Now)
                    return Json(new { success = false, expired = true, message = "انتهت صلاحية الرمز. اطلب رمزاً جديداً." });

                if (!FixedTimeCodeEquals(currentUser.VerificationCode, code))
                {
                    IncrementOtpAttempts(email, purpose);
                    return Json(new { success = false, message = "الرمز الذي أدخلته غير صحيح." });
                }

                currentUser.ContactInfo = email;
                currentUser.IsEmailVerified = true;
                currentUser.VerificationCode = null;
                currentUser.CodeExpiry = null;

                QueueCustomerAccountNotification(
                    currentUser,
                    "تم تحديث البريد الإلكتروني",
                    "تم تحديث البريد الإلكتروني المرتبط بحسابك وتوثيقه بنجاح. إذا لم تقم بهذا التغيير فتواصل مع الدعم فورًا.",
                    NotificationTypes.Account);

                await _context.SaveChangesAsync();
                ClearOtpState(email, purpose);

                var pendingReturnUrl =
                    PeekReturnUrl();

                var profileUrl =
                    string.IsNullOrWhiteSpace(
                        pendingReturnUrl)
                        ? "/Profile/CustomerProfile"
                        : "/Profile/CustomerProfile?returnUrl=" +
                          Uri.EscapeDataString(
                              pendingReturnUrl);

                return Json(new
                {
                    success = true,
                    url = profileUrl,
                    message = "تم توثيق وتحديث بريدك الإلكتروني بنجاح!"
                });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.ContactInfo.ToLower() == email);
            if (user == null)
                return Json(new { success = false, message = "تعذر التحقق من الرمز." });

            if (user.IsEmailVerified)
                return Json(new { success = false, alreadyVerified = true, message = "هذا الحساب مفعل مسبقاً، يمكنك تسجيل الدخول مباشرة." });

            if (user.CodeExpiry == null || user.CodeExpiry < DateTime.Now)
                return Json(new { success = false, expired = true, message = "انتهت صلاحية الرمز. اطلب رمزاً جديداً." });

            if (!FixedTimeCodeEquals(user.VerificationCode, code))
            {
                IncrementOtpAttempts(email, purpose);
                return Json(new { success = false, message = "الرمز الذي أدخلته غير صحيح." });
            }

            user.IsEmailVerified = true;
            user.VerificationCode = null;
            user.CodeExpiry = null;
            await _context.SaveChangesAsync();
            ClearOtpState(email, purpose);

            await SignInUserAsync(user, rememberMe: true);

            return Json(new
            {
                success = true,
                url =
                    await ResolvePostAuthenticationUrlAsync(
                        user),
                message = "تم تفعيل حسابك بنجاح! جاري الدخول..."
            });
        }

        // =============================================================
        //  7.3 إعادة إرسال OTP - ستُربط بالعداد في المرحلة الثانية
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string email, string type = null)
        {
            email = NormalizeEmail(email);
            if (!IsValidEmail(email))
                return Json(new { success = false, message = "البريد الإلكتروني غير صالح." });

            string purpose = string.Equals(type, "reset", StringComparison.OrdinalIgnoreCase)
                ? "reset"
                : string.Equals(type, "update", StringComparison.OrdinalIgnoreCase)
                    ? "update"
                    : "verify";

            int retryAfter = GetOtpRetryAfter(email, purpose);
            if (retryAfter > 0)
            {
                return Json(new
                {
                    success = false,
                    cooldown = true,
                    retryAfter,
                    message = $"يمكنك إعادة الإرسال بعد {retryAfter} ثانية."
                });
            }

            User? user = null;
            if (purpose == "update")
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                    user = await _context.Users.FindAsync(userId);
            }
            else
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.ContactInfo.ToLower() == email);
            }

            // في الاستعادة لا نكشف إن كان البريد موجوداً أم لا.
            if (user == null)
            {
                MarkOtpSent(email, purpose);
                return Json(new
                {
                    success = true,
                    retryAfter = OtpResendCooldownSeconds,
                    expiresIn = OtpLifetimeMinutes * 60,
                    message = "إذا كان البريد مسجلاً لدينا فسيصلك رمز تحقق جديد."
                });
            }

            if (purpose == "verify" && user.IsEmailVerified)
                return Json(new { success = false, alreadyVerified = true, message = "الحساب مفعل مسبقاً." });

            string otpCode = GenerateOtpCode();
            user.VerificationCode = otpCode;
            user.CodeExpiry = DateTime.Now.AddMinutes(OtpLifetimeMinutes);
            await _context.SaveChangesAsync();

            try
            {
                await EmailHelper.SendOtpEmailAsync(email, otpCode, purpose == "reset");
                MarkOtpSent(email, purpose);
                ResetOtpAttempts(email, purpose);
            }
            catch
            {
                return Json(new { success = false, message = "تعذر إرسال الرمز حالياً. حاول مرة أخرى بعد قليل." });
            }

            return Json(new
            {
                success = true,
                retryAfter = OtpResendCooldownSeconds,
                expiresIn = OtpLifetimeMinutes * 60,
                message = "تم إرسال رمز تحقق جديد إلى بريدك."
            });
        }

        // =============================================================
        //  8. Google Login
        //  نحفظ مسار التسجيل (Customer / Owner) داخل AuthenticationProperties
        //  حتى لا يتحول المالك إلى عميل عند العودة من Google.
        // =============================================================
        [HttpGet]
        public IActionResult GoogleLogin(
            string? role = null,
            string? returnUrl = null)
        {
            RememberReturnUrl(
                returnUrl);

            string safeRole = string.Equals(role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase)
                ? SystemRoles.Owner
                : SystemRoles.Customer;

            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleResponse), "Account")
            };

            properties.Items["registration_role"] = safeRole;

            var pendingReturnUrl =
                PeekReturnUrl();

            if (!string.IsNullOrWhiteSpace(
                    pendingReturnUrl))
            {
                properties.Items["return_url"] =
                    pendingReturnUrl;
            }

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // =============================================================
        //  9. Google Response
        //  - لا توجد كلمة مرور افتراضية مشتركة.
        //  - الحساب الجديد يأخذ Hash عشوائياً غير قابل للاستخدام.
        //  - MustChangePassword=true حتى ينشئ المستخدم كلمة خاصة به.
        //  - الحساب الموجود يحتفظ بدوره وحالته كما هي.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            if (
                result.Properties?.Items.TryGetValue(
                    "return_url",
                    out var googleReturnUrl) == true
            )
            {
                RememberReturnUrl(
                    googleReturnUrl);
            }

            if (!result.Succeeded || result.Principal == null)
                return RedirectToAction(nameof(Login));

            string email = NormalizeEmail(result.Principal.FindFirstValue(ClaimTypes.Email));
            string googleName = (result.Principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty).Trim();

            if (!IsValidEmail(email))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ContactInfo.ToLower() == email);

            bool isNewGoogleAccount = false;

            if (user == null)
            {
                string requestedRole = result.Properties?.Items.TryGetValue("registration_role", out var storedRole) == true
                    ? storedRole ?? SystemRoles.Customer
                    : SystemRoles.Customer;

                string safeRole = string.Equals(requestedRole, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase)
                    ? SystemRoles.Owner
                    : SystemRoles.Customer;

                user = new User
                {
                    FullName = string.IsNullOrWhiteSpace(googleName) ? "مستخدم دليلك اليمني" : googleName,
                    Username = await GenerateUniqueGoogleUsernameAsync(email),
                    ContactInfo = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(GenerateUnusableRandomPassword(), workFactor: 11),
                    Role = safeRole,
                    AccountState = safeRole == SystemRoles.Owner ? "Pending" : "Active",
                    IsDarkMode = false,
                    PreferredLanguage = "ar",
                    PhoneNumber = null,
                    IsEmailVerified = true,
                    MustChangePassword = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                isNewGoogleAccount = true;
            }

            var accountAccessError = await GetAccountAccessErrorAsync(user);
            if (accountAccessError != null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["LoginError"] = accountAccessError;
                return RedirectToAction(nameof(Login));
            }

            await SignInUserAsync(user, rememberMe: true);

            if (user.Role == SystemRoles.Admin)
            {
                user.LastLoginAt = DateTime.Now;
                await _context.SaveChangesAsync();
                return Redirect(await GetAdminEntryUrlAsync(user));
            }

            // المرحلة الثالثة: حساب Google الجديد يدخل لمساره الطبيعي مباشرة،
            // والمودل الإجباري يظهر فوق واجهة العميل أو بوابة توثيق المالك.
            // للحسابات الموجودة: العميل والمالك غير النشط يستخدمان نفس المودل،
            // أما المالك النشط ذو حالة تغيير قديمة فيبقى على صفحة التغيير الآمنة.
            if (isNewGoogleAccount)
            {
                return Redirect(
                    await ResolvePostAuthenticationUrlAsync(
                        user));
            }

            if (user.MustChangePassword)
            {
                if (user.Role == SystemRoles.Customer)
                    return Redirect(
                        await ResolvePostAuthenticationUrlAsync(
                            user));

                if (user.Role == SystemRoles.Owner &&
                    !string.Equals(user.AccountState, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction(nameof(VerifyAccount));
                }

                return RedirectToAction(nameof(ForceChangePassword), new { source = "google" });
            }

            return Redirect(
                await ResolvePostAuthenticationUrlAsync(
                    user));
        }

        // =============================================================
        //  10. بوابة توثيق المالك
        // =============================================================
        [HttpGet]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> VerifyAccount()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null || user.Role != SystemRoles.Owner)
                return RedirectToAction(nameof(Login));

            if (string.Equals(user.AccountState, "Active", StringComparison.OrdinalIgnoreCase))
                return Redirect("/Owner/OwnerDashboard/DashboardOwner");

            bool isUploaded =
                !string.IsNullOrWhiteSpace(user.CommercialRegisterDocument) &&
                !string.IsNullOrWhiteSpace(user.IdentityDocument);

            ViewBag.IsUploaded = isUploaded;
            ViewBag.AccountState = user.AccountState;
            ViewBag.RequiresGooglePasswordSetup = user.MustChangePassword;

            // لا نعرض في رحلة المالك إلا الفئات التي تسمح بإضافة منشآت من حساب المالك.
            ViewBag.Categories = await _context.Categories
                .AsNoTracking()
                .Where(category => category.OwnerCanAddPlaces == true)
                .OrderByDescending(category => category.HasBookingSystem)
                .ThenBy(category => category.CategoryName)
                .ToListAsync();

            ViewBag.Cities = await _context.Cities
                .AsNoTracking()
                .OrderBy(city => city.CityName)
                .Select(city => new SelectListItem
                {
                    Value = city.CityId.ToString(),
                    Text = city.CityName
                })
                .ToListAsync();

            // المرحلة الثانية: إذا أنشأ المالك منشأة أثناء التوثيق ثم حدّث الصفحة،
            // نعيد ربط الرحلة بأحدث منشأة Pending بدلاً من إجباره على إنشائها من جديد.
            var verificationPlace = await _context.Places
                .AsNoTracking()
                .Where(place =>
                    place.OwnerId == userId &&
                    place.Status == "Pending")
                .OrderByDescending(place => place.PlaceId)
                .Select(place => new
                {
                    place.PlaceId,
                    place.CategoryId,
                    place.PlaceNameAndDesc,
                    BookingSystemId =
                        place.Category != null
                            ? place.Category.BookingSystemId
                            : 0,
                    HasPolicy = place.PlacePolicy != null
                })
                .FirstOrDefaultAsync();

            ViewBag.VerificationPlaceId = verificationPlace?.PlaceId ?? 0;
            ViewBag.VerificationPlaceCategoryId = verificationPlace?.CategoryId ?? 0;
            ViewBag.VerificationPlaceName = verificationPlace?.PlaceNameAndDesc ?? string.Empty;
            ViewBag.VerificationPlaceBookingSystemId =
                verificationPlace?.BookingSystemId ?? 0;
            ViewBag.VerificationPlaceHasPolicy =
                verificationPlace?.HasPolicy ?? false;
            ViewBag.VerificationOwnerJobTitle =
                user.JobTitle ?? string.Empty;
            ViewBag.VerificationOwnerPhone =
                user.PhoneNumber ?? string.Empty;

            // لقطة سياسة المنصة المالية المعروضة داخل العقد.
            // مصدرها إعدادات الأدمن الحالية، وإذا كان للمنشأة حساب ضمان بسياسة خاصة
            // نعرض القيم الفعلية لذلك الحساب بدل القيم العامة.
            var financialSettings = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .FirstOrDefaultAsync();

            PlaceCommissionAccount? commissionAccount = null;
            if (verificationPlace != null && verificationPlace.PlaceId > 0)
            {
                commissionAccount = await _context.PlaceCommissionAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(account =>
                        account.PlaceId == verificationPlace.PlaceId);
            }

            var currency = string.IsNullOrWhiteSpace(financialSettings?.DefaultCurrency)
                ? "ريال يمني"
                : financialSettings!.DefaultCurrency!.Trim();

            ViewBag.ContractPolicy = new OwnerVerificationContractPolicyDto
            {
                GeneralCommissionPercentage =
                    financialSettings?.CommissionPercentage ?? 10m,
                Currency = currency,
                MinimumCommissionTopUp =
                    Math.Max(0m, financialSettings?.MinimumCommissionTopUp ?? 0m),
                LowBalanceThreshold =
                    Math.Max(0m,
                        commissionAccount?.LowBalanceThreshold ??
                        financialSettings?.DefaultLowBalanceThreshold ??
                        0m),
                AllowNegativeBalance =
                    commissionAccount?.AllowNegativeBalance ??
                    financialSettings?.AllowNegativeCommissionBalance ??
                    false,
                DebtLimit =
                    Math.Max(0m,
                        commissionAccount?.DebtLimit ??
                        financialSettings?.DefaultCommissionDebtLimit ??
                        0m),
                DebtGraceDays =
                    Math.Max(0, financialSettings?.CommissionDebtGraceDays ?? 0),
                SuspendBookingWhenBalanceInsufficient =
                    financialSettings?.SuspendBookingWhenBalanceInsufficient ?? true,
                HidePlaceWhenCommissionSuspended =
                    financialSettings?.HidePlaceWhenCommissionSuspended ?? false,
                UsesPlaceSpecificFinancialPolicy = commissionAccount != null &&
                    (commissionAccount.AllowNegativeBalance.HasValue ||
                     commissionAccount.DebtLimit.HasValue ||
                     commissionAccount.LowBalanceThreshold.HasValue)
            };

            return View();
        }

        // =============================================================
        //  11. إرسال التوثيق - المرحلة الأولى
        //  التسجيل السريع يرسل: الصفة + الهاتف + الفئة + العقد + الوثائق.
        //  المسار الشامل سيستفيد من PlaceId في المرحلة الثانية.
        // =============================================================
        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadVerification([FromForm] OwnerVerificationDto model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, message = "انتهت جلسة الدخول. سجّل الدخول ثم حاول مرة أخرى." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.Role != SystemRoles.Owner)
                return Json(new { success = false, message = "هذا الإجراء مخصص لحسابات الملاك فقط." });

            if (string.Equals(user.AccountState, "Active", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "حسابك مفعل بالفعل." });

            if (!model.IsAgreedToTerms)
                return Json(new { success = false, message = "يجب قراءة اتفاقية الشراكة والموافقة عليها قبل إرسال الطلب." });

            string finalJobTitle;
            string finalAdminPhone;
            Category? selectedCategory = null;
            Place? verificationPlace = null;

            if (model.IsFastTrack)
            {
                // لا نسمح بالرجوع للمسار السريع بعد إنشاء منشأة في التوثيق.
                bool hasPendingVerificationPlace = await _context.Places
                    .AsNoTracking()
                    .AnyAsync(place => place.OwnerId == userId && place.Status == "Pending");

                if (hasPendingVerificationPlace)
                {
                    return Json(new
                    {
                        success = false,
                        message = "لقد بدأت مسار إنشاء المنشأة قبل الدخول. أكمل المسار الشامل بدلاً من التسجيل السريع."
                    });
                }

                finalJobTitle = (model.JobTitle ?? string.Empty).Trim();
                if (string.Equals(finalJobTitle, "أخرى", StringComparison.Ordinal))
                {
                    finalJobTitle = Request.Form["JobTitleOther"].ToString().Trim();
                    if (string.IsNullOrWhiteSpace(finalJobTitle))
                        return Json(new { success = false, message = "اكتب صفتك الوظيفية قبل المتابعة." });
                }

                if (string.IsNullOrWhiteSpace(finalJobTitle))
                    return Json(new { success = false, message = "يرجى تحديد صفتك الوظيفية." });

                finalAdminPhone = (model.AdminPhone ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(finalAdminPhone))
                    return Json(new { success = false, message = "رقم هاتف التواصل مطلوب." });

                if (!model.CategoryId.HasValue || model.CategoryId.Value <= 0)
                    return Json(new { success = false, message = "اختر فئة المنشأة حتى تظهر لك نسبة العمولة الصحيحة في العقد." });

                selectedCategory = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(category =>
                        category.CategoryId == model.CategoryId.Value &&
                        category.OwnerCanAddPlaces == true);

                if (selectedCategory == null)
                    return Json(new { success = false, message = "الفئة المختارة غير متاحة لتسجيل المالك." });
            }
            else
            {
                // المسار الشامل: PlaceId هو المصدر الموثوق، ولا نعتمد CategoryId القادم من الواجهة.
                if (!model.PlaceId.HasValue || model.PlaceId.Value <= 0)
                    return Json(new { success = false, message = "أكمل إنشاء المنشأة أولاً قبل إرسال التوثيق." });

                verificationPlace = await _context.Places
                    .Include(place => place.Category)
                    .Include(place => place.PlacePolicy)
                    .FirstOrDefaultAsync(place =>
                        place.PlaceId == model.PlaceId.Value &&
                        place.OwnerId == userId &&
                        place.Status == "Pending");

                if (verificationPlace == null)
                    return Json(new { success = false, message = "المنشأة المرتبطة برحلة التوثيق غير موجودة أو لا تتبع حسابك." });

                selectedCategory = verificationPlace.Category;
                if (selectedCategory == null || selectedCategory.OwnerCanAddPlaces != true)
                    return Json(new { success = false, message = "فئة المنشأة المرتبطة بالطلب غير صحيحة." });

                // يمنع تجاوز المرحلة الثالثة يدوياً للفئات التي تحتاج سياسات حجز.
                bool requiresFacilityPolicies =
                    selectedCategory.BookingSystemId >= 1 &&
                    selectedCategory.BookingSystemId <= 4;

                if (requiresFacilityPolicies && verificationPlace.PlacePolicy == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "قبل إرسال التوثيق يجب اختيار السياسات الافتراضية أو ضبط سياسات المنشأة يدويًا."
                    });
                }

                finalJobTitle = (user.JobTitle ?? string.Empty).Trim();
                finalAdminPhone = (user.PhoneNumber ?? string.Empty).Trim();

                // استكمال آمن للحسابات التي أنشأت المنشأة قبل إصلاح مزامنة هاتف المالك.
                // عند استئناف رحلة التوثيق بعد تسجيل الخروج لا نعيد إنشاء المنشأة،
                // لذلك نسمح للواجهة بإرسال AdminPhone مرة واحدة لاستكمال Users.PhoneNumber.
                if (string.IsNullOrWhiteSpace(finalAdminPhone))
                {
                    string recoveryPhone = (model.AdminPhone ?? string.Empty).Trim();
                    string normalizedRecoveryPhone = recoveryPhone
                        .Replace(" ", string.Empty)
                        .Replace("-", string.Empty)
                        .Replace("(", string.Empty)
                        .Replace(")", string.Empty);

                    if (!string.IsNullOrWhiteSpace(normalizedRecoveryPhone) &&
                        System.Text.RegularExpressions.Regex.IsMatch(normalizedRecoveryPhone, @"^\+?[0-9]{7,15}$"))
                    {
                        finalAdminPhone = normalizedRecoveryPhone;
                        user.PhoneNumber = normalizedRecoveryPhone;
                        await _context.SaveChangesAsync();
                    }
                }

                if (string.IsNullOrWhiteSpace(finalJobTitle))
                {
                    return Json(new
                    {
                        success = false,
                        message = "الصفة الوظيفية لمسؤول المنشأة غير محفوظة. راجع بيانات مسؤول المنشأة ثم حاول مرة أخرى."
                    });
                }

                if (string.IsNullOrWhiteSpace(finalAdminPhone))
                {
                    return Json(new
                    {
                        success = false,
                        message = "رقم هاتف مسؤول المنشأة غير محفوظ. أدخل رقم التواصل لإكمال التوثيق."
                    });
                }
            }

            decimal effectiveCommission = 0m;

            var systemSettings = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .FirstOrDefaultAsync();

            effectiveCommission = Math.Round(
                selectedCategory?.CommissionPercentage ??
                systemSettings?.CommissionPercentage ??
                10m,
                2);

            // التفويض مطلوب في المسارين إذا لم يكن المسجل هو المالك الفعلي.
            bool isActualOwner =
                string.Equals(finalJobTitle, "المالك الفعلي", StringComparison.Ordinal) ||
                string.Equals(finalJobTitle, "صاحب المنشأة", StringComparison.Ordinal);

            bool authorizationRequired = !isActualOwner;

            if (authorizationRequired &&
                (model.AuthorizationLetter == null || model.AuthorizationLetter.Length <= 0))
            {
                return Json(new
                {
                    success = false,
                    message = "بما أنك لست المالك الفعلي، يجب إرفاق خطاب التفويض أو التوكيل الرسمي."
                });
            }

            if (model.CommercialRegister == null || model.CommercialRegister.Length <= 0)
                return Json(new { success = false, message = "يرجى رفع السجل التجاري أو رخصة المزاولة." });

            if (model.IdentityCardFront == null || model.IdentityCardFront.Length <= 0 ||
                model.IdentityCardBack == null || model.IdentityCardBack.Length <= 0)
            {
                return Json(new { success = false, message = "يرجى رفع الوجهين الأمامي والخلفي للهوية." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string webRootPath = _env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRootPath))
                    webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                string uploadsFolder = Path.Combine(webRootPath, "uploads", "verification");
                Directory.CreateDirectory(uploadsFolder);

                // رفع الوثائق بنفس بنية المشروع الحالية.
                string commercialExt = Path.GetExtension(model.CommercialRegister.FileName) ?? ".jpg";
                string commercialName = $"Owner_{user.UserId}_Commercial{commercialExt}";
                string commercialPath = Path.Combine(uploadsFolder, commercialName);
                await using (var stream = new FileStream(commercialPath, FileMode.Create))
                    await model.CommercialRegister.CopyToAsync(stream);
                user.CommercialRegisterDocument = $"/uploads/verification/{commercialName}";

                string frontExt = Path.GetExtension(model.IdentityCardFront.FileName) ?? ".jpg";
                string frontName = $"Owner_{user.UserId}_IDFront{frontExt}";
                string frontPath = Path.Combine(uploadsFolder, frontName);
                await using (var stream = new FileStream(frontPath, FileMode.Create))
                    await model.IdentityCardFront.CopyToAsync(stream);

                string backExt = Path.GetExtension(model.IdentityCardBack.FileName) ?? ".jpg";
                string backName = $"Owner_{user.UserId}_IDBack{backExt}";
                string backPath = Path.Combine(uploadsFolder, backName);
                await using (var stream = new FileStream(backPath, FileMode.Create))
                    await model.IdentityCardBack.CopyToAsync(stream);

                user.IdentityDocument =
                    $"/uploads/verification/{frontName}|/uploads/verification/{backName}";

                if (model.AuthorizationLetter != null && model.AuthorizationLetter.Length > 0)
                {
                    string authExt = Path.GetExtension(model.AuthorizationLetter.FileName) ?? ".jpg";
                    string authName = $"Owner_{user.UserId}_Auth{authExt}";
                    string authPath = Path.Combine(uploadsFolder, authName);
                    await using (var stream = new FileStream(authPath, FileMode.Create))
                        await model.AuthorizationLetter.CopyToAsync(stream);
                    user.AuthorizationDocument = $"/uploads/verification/{authName}";
                }
                else
                {
                    user.AuthorizationDocument = null;
                }

                string cleanPhone = finalAdminPhone;
                bool isPhoneUsed = await _context.Users
                    .AnyAsync(other =>
                        other.UserId != user.UserId &&
                        other.PhoneNumber == cleanPhone);

                if (isPhoneUsed)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = "رقم الهاتف مستخدم في حساب آخر. استخدم رقمًا مختلفًا."
                    });
                }

                user.PhoneNumber = cleanPhone;
                user.JobTitle = finalJobTitle;
                user.IsAgreedToTerms = true;
                user.AgreedToTermsDate = DateTime.Now;

                // أي إعادة تقديم بعد الرفض تعود للمراجعة.
                bool wasRejected =
                    string.Equals(user.AccountState, "Rejected", StringComparison.OrdinalIgnoreCase);

                if (wasRejected)
                    user.AccountState = "Pending";

                // =============================================================
                // إشعار مراجعة توثيق المالك:
                // يصل فقط لمن يملك إحدى صلاحيتي مراجعة الوثائق/تغيير الحالة،
                // مع إزالة التكرار إذا كان الموظف يملك الصلاحيتين.
                // =============================================================
                await _notificationService.QueueForAdminPermissionsAsync(
                    new[]
                    {
                        AdminPermissionKeys.Users.ViewVerificationDocuments,
                        AdminPermissionKeys.Users.ChangeState
                    },
                    new CreateNotificationDto
                    {
                        NotificationTitle = wasRejected
                            ? "إعادة تقديم توثيق مالك يحتاج مراجعة"
                            : "طلب توثيق مالك جديد يحتاج مراجعة",
                        NotificationText = verificationPlace != null
                            ? $"أرسل {user.FullName} وثائق التوثيق المرتبطة بمنشأة «{verificationPlace.PlaceNameAndDesc}» وتحتاج مراجعة."
                            : $"أرسل {user.FullName} وثائق توثيق حساب المالك وتحتاج مراجعة.",
                        NotificationType = NotificationTypes.AdminAccountActionRequired,
                        TargetUrl = $"/Admin/Users/UserManagement?focusUser={user.UserId}&openVerification=true",
                        ReferenceType = NotificationReferenceTypes.User,
                        ReferenceId = user.UserId
                    },
                    HttpContext.RequestAborted);

                // في المسار الشامل تكون المنشأة قد أُنشئت Pending قبل اكتمال التوثيق.
                // لا نرسل إشعارها في AddFullPlace حتى لا تسبق مراجعة الحساب؛
                // نرسلها هنا، لحظة اكتمال التوثيق، لتصل إلى فريق Places.Review.
                if (verificationPlace != null &&
                    verificationPlace.PlaceId > 0 &&
                    string.Equals(verificationPlace.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    await _notificationService.QueueForAdminPermissionAsync(
                        AdminPermissionKeys.Places.Review,
                        new CreateNotificationDto
                        {
                            NotificationTitle = "منشأة جديدة تحتاج مراجعة",
                            NotificationText =
                                $"أكمل المالك {user.FullName} توثيق منشأة «{verificationPlace.PlaceNameAndDesc}» وأصبحت جاهزة للمراجعة.",
                            NotificationType = NotificationTypes.AdminPlaceActionRequired,
                            TargetUrl =
                                $"/Admin/Places/PlaceManagement?focusPlace={verificationPlace.PlaceId}",
                            ReferenceType = NotificationReferenceTypes.Place,
                            ReferenceId = verificationPlace.PlaceId
                        },
                        HttpContext.RequestAborted);
                }

                // حفظ بيانات التوثيق والإشعارات معًا داخل نفس الـ Transaction.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = "تم إرسال طلب التوثيق بنجاح، وهو الآن قيد مراجعة الإدارة.",
                    categoryName = selectedCategory?.CategoryName,
                    commissionPercentage = effectiveCommission
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                return Json(new
                {
                    success = false,
                    message = "تعذر إرسال طلب التوثيق حاليًا. حاول مرة أخرى، وإذا استمرت المشكلة فتواصل مع الدعم."
                });
            }
        }


        // =============================================================
        //  12. تسجيل الخروج (Logout GET)
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var cookieOptions = new CookieOptions { Path = "/" };
            Response.Cookies.Delete("darkMode", cookieOptions);
            Response.Cookies.Delete(CookieRequestCultureProvider.DefaultCookieName, cookieOptions);

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                Response.Cookies.Delete($"UserCulture_{userIdClaim}", cookieOptions);
            }

            return RedirectToAction("Login", "Account");
        }

        // =============================================================
        //  13. التفعيل النهائي والترقية للعميل (The Magic Button)
        // =============================================================
        [HttpPost]
        public async Task<IActionResult> FinalizeActivation()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId);

            if (user != null && user.AccountState == "Accepted")
            {
                user.AccountState = "Active";
                user.Role = SystemRoles.Owner;
                await _context.SaveChangesAsync();

                string lang = user.PreferredLanguage ?? "ar";
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("IsDarkMode", user.IsDarkMode.ToString()),
                    new Claim("PreferredLanguage", lang),
                    new Claim("AccountState", user.AccountState)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

                TempData["ShowWelcomePopup"] = true;
                TempData["WelcomeName"] = user.FullName;
            }

            return Redirect("/Owner/OwnerDashboard/DashboardOwner");
        }

        // =============================================================
        //  14. طلب استعادة كلمة المرور
        // =============================================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            email = NormalizeEmail(email);
            if (!IsValidEmail(email))
                return Json(new { success = false, message = "يرجى إدخال بريد إلكتروني صحيح." });

            string redirectUrl = $"/Account/ResetPassword?email={Uri.EscapeDataString(email)}";
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ContactInfo.ToLower() == email);

            // نفس شكل الاستجابة دائماً حتى لا نكشف للمهاجم إن كان البريد مسجلاً.
            if (user == null)
            {
                MarkOtpSent(email, "reset");
                await Task.Delay(RandomNumberGenerator.GetInt32(120, 260));

                return Json(new
                {
                    success = true,
                    url = redirectUrl,
                    retryAfter = OtpResendCooldownSeconds,
                    message = "إذا كان البريد مسجلاً لدينا فسيصلك رمز إعادة التعيين."
                });
            }

            int retryAfter = GetOtpRetryAfter(email, "reset");
            if (retryAfter <= 0)
            {
                string otpCode = GenerateOtpCode();
                user.VerificationCode = otpCode;
                user.CodeExpiry = DateTime.Now.AddMinutes(OtpLifetimeMinutes);
                await _context.SaveChangesAsync();

                try
                {
                    await EmailHelper.SendOtpEmailAsync(email, otpCode, true);
                    MarkOtpSent(email, "reset");
                    ResetOtpAttempts(email, "reset");
                }
                catch
                {
                    return Json(new
                    {
                        success = false,
                        message = "تعذر إرسال رمز الاستعادة حالياً. يرجى المحاولة بعد قليل."
                    });
                }
            }

            return Json(new
            {
                success = true,
                url = redirectUrl,
                retryAfter = Math.Max(retryAfter, OtpResendCooldownSeconds),
                message = "إذا كان البريد مسجلاً لدينا فسيصلك رمز إعادة التعيين."
            });
        }

        // =============================================================
        //  15. إعادة تعيين كلمة المرور
        // =============================================================
        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Login));

            ViewBag.Email = NormalizeEmail(email);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            string email,
            string code,
            string newPassword)
        {
            email = NormalizeEmail(email);
            code = (code ?? string.Empty).Trim();

            if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(code) || string.IsNullOrEmpty(newPassword))
                return Json(new { success = false, message = "الرجاء تعبئة جميع الحقول المطلوبة." });

            var passwordError = GetPasswordPolicyError(newPassword);
            if (passwordError != null)
                return Json(new { success = false, message = passwordError });

            if (GetOtpAttempts(email, "reset") >= OtpMaxAttempts)
                return Json(new { success = false, attemptsExceeded = true, message = "تم تجاوز عدد المحاولات. اطلب رمزاً جديداً." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.ContactInfo.ToLower() == email);
            if (user == null)
                return Json(new { success = false, message = "رمز التحقق غير صحيح أو منتهي الصلاحية." });

            if (user.CodeExpiry == null || user.CodeExpiry < DateTime.Now)
                return Json(new { success = false, expired = true, message = "انتهت صلاحية الرمز، يرجى طلب رمز جديد." });

            if (!FixedTimeCodeEquals(user.VerificationCode, code))
            {
                IncrementOtpAttempts(email, "reset");
                return Json(new { success = false, message = "رمز التحقق غير صحيح، يرجى التأكد." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
            user.VerificationCode = null;
            user.CodeExpiry = null;
            user.IsEmailVerified = true;
            user.MustChangePassword = false;

            QueueCustomerAccountNotification(
                user,
                "تمت إعادة تعيين كلمة المرور",
                "تمت إعادة تعيين كلمة مرور حسابك بنجاح. إذا لم تطلب هذا التغيير فتواصل مع الدعم فورًا.",
                NotificationTypes.Security);

            await _context.SaveChangesAsync();
            ClearOtpState(email, "reset");

            return Json(new
            {
                success = true,
                url = "/Account/Login",
                message = "تم تغيير كلمة المرور بنجاح. يمكنك تسجيل الدخول الآن."
            });
        }

        // =============================================================
        //  17. تغيير كلمة المرور الإجبارية
        //  المرحلة الأولى: صفحة موجودة تعمل كجسر آمن.
        //  المرحلة الثانية: نفس المنطق سيُعرض داخل Modal فخم بدون تغيير Backend.
        // =============================================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ForceChangePassword(string? source = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (user.Role == SystemRoles.Admin && user.MustChangePassword)
                return Redirect(await GetAdminEntryUrlAsync(user));

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceChangePassword(
            string newPassword,
            string? confirmPassword = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, message = "انتهت الجلسة، يرجى تسجيل الدخول مجدداً." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "الحساب غير موجود." });

            if (!user.MustChangePassword && user.AccountState != "ForcePasswordChange")
            {
                return Json(new
                {
                    success = false,
                    message = "هذا الحساب لا يحتاج تغيير كلمة مرور إجباري."
                });
            }

            var passwordError = GetPasswordPolicyError(newPassword);
            if (passwordError != null)
                return Json(new { success = false, message = passwordError });

            // الصفحة القديمة في المرحلة الأولى تحتوي حقلاً واحداً فقط.
            // إذا أرسلت الواجهة confirmPassword نتحقق منه، أما المودل في المرحلة الثانية فسيكون التأكيد إلزامياً.
            if (!string.IsNullOrWhiteSpace(confirmPassword) &&
                !string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return Json(new { success = false, message = "تأكيد كلمة المرور غير مطابق." });
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(user.PasswordHash) &&
                    BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
                {
                    return Json(new
                    {
                        success = false,
                        message = "اختر كلمة مرور جديدة مختلفة عن كلمة المرور الحالية."
                    });
                }
            }
            catch { }

            if (user.Role == SystemRoles.Admin)
            {
                var accessError = await GetAdminEmployeeAccessErrorAsync(user);
                if (accessError != null)
                    return Json(new { success = false, message = accessError });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
            user.MustChangePassword = false;

            // نحافظ على السلوك القديم فقط للحسابات القديمة التي تستخدم هذه الحالة.
            if (user.Role == SystemRoles.Owner && user.AccountState == "ForcePasswordChange")
                user.AccountState = "Active";

            if (user.Role == SystemRoles.Admin)
                user.LastLoginAt = DateTime.Now;

            QueueCustomerAccountNotification(
                user,
                "تم تعيين كلمة مرور جديدة",
                "تم تأمين حسابك وتعيين كلمة مرور جديدة بنجاح.",
                NotificationTypes.Security);

            await _context.SaveChangesAsync();

            string redirectUrl = user.Role == SystemRoles.Admin
                ? await GetAdminLandingUrlAsync(user)
                : await ResolvePostAuthenticationUrlAsync(
                    user);

            return Json(new
            {
                success = true,
                url = redirectUrl,
                message = "تم تأمين حسابك وتعيين كلمة المرور الجديدة بنجاح."
            });
        }

        // =============================================================
        //  17.1 Endpoint مخصص لمودل Google في المرحلة الثانية.
        //  موجود من الآن حتى لا نغيّر Backend عند بناء الواجهة.
        // =============================================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetGooglePassword(
            string newPassword,
            string confirmPassword)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, sessionExpired = true, message = "انتهت الجلسة. سجل الدخول مجدداً." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "الحساب غير موجود." });

            if (!user.MustChangePassword)
                return Json(new
                {
                    success = true,
                    alreadyCompleted = true,
                    url =
                        await ResolvePostAuthenticationUrlAsync(
                            user)
                });

            var passwordError = GetPasswordPolicyError(newPassword);
            if (passwordError != null)
                return Json(new { success = false, message = passwordError });

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
                return Json(new { success = false, message = "تأكيد كلمة المرور غير مطابق." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
            user.MustChangePassword = false;

            QueueCustomerAccountNotification(
                user,
                "تم تأمين حساب Google",
                "تم تعيين كلمة مرور لحسابك بنجاح ويمكنك استخدامها مع تسجيل الدخول المعتاد.",
                NotificationTypes.Security);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                url =
                    await ResolvePostAuthenticationUrlAsync(
                        user),
                message = "تم تأمين حسابك بنجاح."
            });
        }

        // =============================================================
        // Helpers مشتركة للمصادقة والأمان
        // =============================================================
        private static string NormalizeEmail(string? value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant();

        private static string NormalizeUsername(string? value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant();

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email);

        private static string? GetPasswordPolicyError(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) ||
                !System.Text.RegularExpressions.Regex.IsMatch(password, @"^(?=.*[A-Za-z])(?=.*\d).{8,}$"))
            {
                return "كلمة المرور يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.";
            }

            return null;
        }

        private static string GenerateOtpCode() =>
            RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        private static string GenerateUnusableRandomPassword() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        private async Task<string> GenerateUniqueGoogleUsernameAsync(string email)
        {
            string rawBase = email.Contains('@') ? email.Split('@')[0] : "user";
            string safeBase = System.Text.RegularExpressions.Regex.Replace(rawBase, @"[^a-zA-Z0-9_]", string.Empty);

            if (string.IsNullOrWhiteSpace(safeBase)) safeBase = "user";
            if (safeBase.Length > 14) safeBase = safeBase[..14];

            for (int i = 0; i < 20; i++)
            {
                string candidate = $"{safeBase}{RandomNumberGenerator.GetInt32(1000, 10000)}".ToLowerInvariant();
                bool exists = await _context.Users.AnyAsync(u => u.Username.ToLower() == candidate);
                if (!exists) return candidate;
            }

            return ($"user_{Guid.NewGuid():N}")[..20];
        }

        private static bool FixedTimeCodeEquals(string? storedCode, string submittedCode)
        {
            if (string.IsNullOrWhiteSpace(storedCode)) return false;

            byte[] left = System.Text.Encoding.UTF8.GetBytes(storedCode);
            byte[] right = System.Text.Encoding.UTF8.GetBytes(submittedCode);

            return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
        }

        private string GetOtpCacheKey(string email, string purpose, string suffix) =>
            $"auth:otp:{purpose}:{NormalizeEmail(email)}:{suffix}";

        private void MarkOtpSent(string email, string purpose)
        {
            _cache.Set(
                GetOtpCacheKey(email, purpose, "sentAt"),
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(OtpLifetimeMinutes));
        }

        private int GetOtpRetryAfter(string email, string purpose)
        {
            if (!_cache.TryGetValue<DateTimeOffset>(GetOtpCacheKey(email, purpose, "sentAt"), out var sentAt))
                return 0;

            int elapsed = (int)Math.Floor((DateTimeOffset.UtcNow - sentAt).TotalSeconds);
            return Math.Max(0, OtpResendCooldownSeconds - elapsed);
        }

        private int GetOtpAttempts(string email, string purpose) =>
            _cache.TryGetValue<int>(GetOtpCacheKey(email, purpose, "attempts"), out var attempts)
                ? attempts
                : 0;

        private void IncrementOtpAttempts(string email, string purpose)
        {
            int attempts = GetOtpAttempts(email, purpose) + 1;
            _cache.Set(
                GetOtpCacheKey(email, purpose, "attempts"),
                attempts,
                TimeSpan.FromMinutes(OtpLifetimeMinutes));
        }

        private void ResetOtpAttempts(string email, string purpose) =>
            _cache.Remove(GetOtpCacheKey(email, purpose, "attempts"));

        private void ClearOtpState(string email, string purpose)
        {
            _cache.Remove(GetOtpCacheKey(email, purpose, "attempts"));
            _cache.Remove(GetOtpCacheKey(email, purpose, "sentAt"));
        }

        private async Task<(bool sent, int retryAfter)> TrySendOtpAsync(
            User user,
            string purpose,
            bool isPasswordReset)
        {
            int retryAfter = GetOtpRetryAfter(user.ContactInfo, purpose);
            if (retryAfter > 0)
                return (false, retryAfter);

            string otpCode = GenerateOtpCode();
            user.VerificationCode = otpCode;
            user.CodeExpiry = DateTime.Now.AddMinutes(OtpLifetimeMinutes);
            await _context.SaveChangesAsync();

            try
            {
                await EmailHelper.SendOtpEmailAsync(user.ContactInfo, otpCode, isPasswordReset);
                MarkOtpSent(user.ContactInfo, purpose);
                ResetOtpAttempts(user.ContactInfo, purpose);
                return (true, OtpResendCooldownSeconds);
            }
            catch
            {
                return (false, 0);
            }
        }

        private async Task<string?> GetAccountAccessErrorAsync(User user)
        {
            if (string.Equals(user.AccountState, "Blocked", StringComparison.OrdinalIgnoreCase))
                return "عذراً، هذا الحساب محظور من قِبل الإدارة ولا يمكنه تسجيل الدخول.";

            if (string.Equals(user.AccountState, "Suspended", StringComparison.OrdinalIgnoreCase))
                return "عذراً، تم تعليق حسابك مؤقتاً من قِبل الإدارة.";

            if (user.Role == SystemRoles.Admin)
                return await GetAdminEmployeeAccessErrorAsync(user);

            return null;
        }

        private async Task SignInUserAsync(User user, bool rememberMe)
        {
            string lang = user.PreferredLanguage ?? "ar";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Path = "/",
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            Response.Cookies.Append(
                "darkMode",
                user.IsDarkMode ? "enabled" : "disabled",
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Path = "/",
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? SystemRoles.Customer),
                new Claim("IsDarkMode", user.IsDarkMode.ToString()),
                new Claim("PreferredLanguage", lang),
                new Claim("AccountState", user.AccountState ?? "Active")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    AllowRefresh = true,
                    ExpiresUtc = rememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30)
                        : DateTimeOffset.UtcNow.AddHours(12)
                });
        }

        private async Task<string> GetUserLandingUrlAsync(User user)
        {
            if (user.Role == SystemRoles.Admin)
                return await GetAdminEntryUrlAsync(user);

            if (user.Role == SystemRoles.Owner)
            {
                return string.Equals(user.AccountState, "Active", StringComparison.OrdinalIgnoreCase)
                    ? "/Owner/OwnerDashboard/DashboardOwner"
                    : "/Account/VerifyAccount";
            }

            return "/Home/Index";
        }

        private async Task<string?> GetAdminEmployeeAccessErrorAsync(
            User user)
        {
            if (
                !string.Equals(
                    user.EmploymentStatus,
                    "Active",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                return user.EmploymentStatus switch
                {
                    "Suspended" =>
                        "تم إيقاف حساب الموظف. راجع مدير النظام.",

                    "OnLeave" =>
                        "حساب الموظف في إجازة حالياً.",

                    "Terminated" =>
                        "انتهت صلاحية هذا الحساب الوظيفي.",

                    _ =>
                        "حساب الموظف غير فعال حالياً."
                };
            }

            if (!user.AdminRoleId.HasValue)
            {
                return "حساب الموظف غير مرتبط بدور وظيفي.";
            }

            var role =
                await _context.AdminRoles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item =>
                            item.AdminRoleId ==
                            user.AdminRoleId.Value);

            if (role == null)
            {
                return "الدور الوظيفي المرتبط بالحساب غير موجود.";
            }

            if (role.IsActive != true)
            {
                return "الدور الوظيفي لهذا الحساب موقوف حالياً.";
            }

            return null;
        }

        // =============================================================
        // بوابة دخول موظف الإدارة بعد المصادقة
        // إذا كانت كلمة المرور ما تزال مؤقتة، نفتح صفحة الملف الشخصي
        // الموجودة فعلاً داخل لوحة الإدارة حتى يتمكن _AdminLayout من
        // عرض مودل تغيير كلمة المرور قبل المرور على أي صلاحية قسم.
        // بعد تغيير كلمة المرور نرجع لاختيار أول صفحة مسموحة حسب الدور.
        // =============================================================
        private async Task<string> GetAdminEntryUrlAsync(
            User user)
        {
            if (user.MustChangePassword)
            {
                return "/Admin/Profile/AdminProfile";
            }

            return await GetAdminLandingUrlAsync(user);
        }

        private async Task<string> GetAdminLandingUrlAsync(
            User user)
        {
            if (!user.AdminRoleId.HasValue)
            {
                return "/Admin/Profile/AdminProfile";
            }

            var role =
                await _context.AdminRoles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item =>
                            item.AdminRoleId ==
                            user.AdminRoleId.Value);

            if (role?.IsSystemRole == true)
            {
                return "/Admin/AdminDashboard/DashboardOverview";
            }

            var permissionKeys =
                await _context.AdminRolePermissions
                    .AsNoTracking()
                    .Where(
                        link =>
                            link.AdminRoleId ==
                                user.AdminRoleId.Value &&
                            link.AdminPermission.IsActive ==
                                true)
                    .Select(
                        link =>
                            link.AdminPermission.PermissionKey)
                    .ToListAsync();

            bool HasPrefix(string prefix) =>
                permissionKeys.Any(
                    key =>
                        key.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase));

            if (
                permissionKeys.Any(
                    key =>
                        string.Equals(
                            key,
                            "Dashboard.View",
                            StringComparison.OrdinalIgnoreCase))
            )
            {
                return "/Admin/AdminDashboard/DashboardOverview";
            }

            if (
                HasPrefix("Employees.") ||
                HasPrefix("Roles.") ||
                HasPrefix("Audit.")
            )
            {
                return "/Admin/Employees/EmployeeManagement";
            }

            if (HasPrefix("Users."))
            {
                return "/Admin/Users/UserManagement";
            }

            if (HasPrefix("Places."))
            {
                return "/Admin/Places/PlaceManagement";
            }

            if (HasPrefix("Cases."))
            {
                return "/Admin/Cases/CasesManagement";
            }

            if (HasPrefix("Finance."))
            {
                return "/Admin/Financials/Manage";
            }

            if (HasPrefix("Advertisements."))
            {
                return "/Admin/Advertisements/AdvertisementManagement";
            }

            if (
                HasPrefix("MasterData.") ||
                HasPrefix("Categories.") ||
                HasPrefix("Cities.")
            )
            {
                return "/Admin/MasterEntities/MasterEntitiesManagement";
            }

            if (HasPrefix("Settings."))
            {
                return "/Admin/Settings/SystemSettings";
            }

            // الملف الشخصي متاح لأي موظف إدارة لتعديل بياناته الأساسية.
            return "/Admin/Profile/AdminProfile";
        }

    }
}
