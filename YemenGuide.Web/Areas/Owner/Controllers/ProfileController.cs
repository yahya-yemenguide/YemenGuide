// =============================================================
//  ProfileController.cs - التحكم في الملف الشخصي للمالك (النسخة النهائية المحمية) 🏢🛡️
// =============================================================

using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.Text.RegularExpressions;
using YemenGuide.Data;
using YemenGuide.Core.DTOs.Owner;
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using YemenGuide.Web.Filters;
using YemenGuide.Core.Helpers;
using YemenGuide.Web.Services;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner")]
    [AuthorizeActiveOwner]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfileController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }


        private string ResolvePreferredCurrency(
            string? userCurrency)
        {
            string normalized =
                CurrencyDisplayService.NormalizeCurrency(
                    userCurrency);

            if (!string.IsNullOrWhiteSpace(
                    normalized))
            {
                return normalized;
            }

            string? systemDefault =
                _context.SystemSettings
                    .OrderBy(setting => setting.Id)
                    .Select(setting => setting.DefaultCurrency)
                    .FirstOrDefault();

            normalized =
                CurrencyDisplayService.NormalizeCurrency(
                    systemDefault);

            return string.IsNullOrWhiteSpace(normalized)
                ? CurrencyDisplayService.YemeniRial
                : normalized;
        }

        // =============================================================
        //  1. وظيفة عرض صفحة الملف الشخصي للمالك (OwnerProfile)
        // =============================================================
        [HttpGet("/Owner/Profile/OwnerProfile")]
        public IActionResult OwnerProfile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 2 : int.Parse(userIdClaim);

            var ownerUser = _context.Users.FirstOrDefault(u => u.UserId == currentUserId);
            if (ownerUser == null) return NotFound("حساب المالك غير موجود في النظام.");

            var viewModel = new OwnerProfileDto
            {
                UserId = ownerUser.UserId,
                FullName = ownerUser.FullName,
                Username = ownerUser.Username,

                ContactInfo = ownerUser.ContactInfo ?? "",
                IsEmailVerified = ownerUser.IsEmailVerified,

                PhoneNumber = ownerUser.PhoneNumber ?? "",

                // ✨ الحقول الإضافية المتطابقة مع العميل
                Bio = ownerUser.Bio ?? "",
                DateOfBirth = ownerUser.DateOfBirth,
                Gender = ownerUser.Gender ?? "",
                City = ownerUser.City ?? "",
                WalletBalance = ownerUser.WalletBalance,

                FacebookUrl = ownerUser.FacebookUrl ?? "",
                TwitterUrl = ownerUser.TwitterUrl ?? "",
                InstagramUrl = ownerUser.InstagramUrl ?? "",

                Role = ownerUser.Role,
                AccountState = ownerUser.AccountState ?? "",
                CreatedAt = ownerUser.CreatedAt ?? DateTime.Now,

                PreferredCurrency = ResolvePreferredCurrency(ownerUser.PreferredCurrency),
                DarkModeEnabled = ownerUser.IsDarkMode,
                Language = ownerUser.PreferredLanguage ?? "ar",

                InitialLetter = !string.IsNullOrWhiteSpace(ownerUser.FullName) ? ownerUser.FullName.Trim().Substring(0, 1).ToUpper() : "O",
                ProfileImage = ownerUser.ProfileImage ?? ""
            };

            return View(viewModel);
        }

        // =============================================================
        //  2. طلب تغيير الإيميل (نظام التوثيق المحمي) ✨
        // =============================================================
        [HttpPost("/Owner/Profile/RequestEmailChange")]
        public async Task<IActionResult> RequestEmailChange([FromBody] OwnerEmailChangeRequestDto model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 2 : int.Parse(userIdClaim);

            var ownerUser = await _context.Users.FindAsync(currentUserId);
            if (ownerUser == null) return Json(new { success = false, message = "حساب المالك غير موجود." });

            string newEmail = model.NewEmail?.Trim();
            if (string.IsNullOrEmpty(newEmail) || !Regex.IsMatch(newEmail, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                return Json(new { success = false, message = "البريد الإلكتروني المدخل غير صالح." });

            if (ownerUser.ContactInfo == newEmail)
                return Json(new { success = false, message = "هذا هو بريدك الإلكتروني الحالي بالفعل." });

            bool isEmailTaken = _context.Users.Any(u => u.ContactInfo == newEmail && u.UserId != currentUserId);
            if (isEmailTaken)
                return Json(new { success = false, message = "عذراً، البريد الإلكتروني هذا مستخدم لحساب آخر!" });

            // توليد الكود وحفظه
            string otpCode = new Random().Next(100000, 999999).ToString();
            ownerUser.VerificationCode = otpCode;
            ownerUser.CodeExpiry = DateTime.Now.AddMinutes(5);

            // حفظ الإيميل الجديد مؤقتاً
            TempData["PendingEmail"] = newEmail;

            await _context.SaveChangesAsync();

            // إرسال الكود
            _ = EmailHelper.SendOtpEmailAsync(newEmail, otpCode);

            return Json(new { success = true });
        }

        // =============================================================
        //  3. وظيفة تحديث البيانات الشخصية الأساسية للمالك (UpdatePersonalInfo)
        // =============================================================
        [HttpPost("/Owner/Profile/UpdatePersonalInfo")]
        public async Task<IActionResult> UpdatePersonalInfo([FromBody] OwnerPersonalInfoUpdateDto model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 2 : int.Parse(userIdClaim);

            var ownerUser = await _context.Users.FindAsync(currentUserId);
            if (ownerUser == null)
                return Json(new { success = false, message = "حساب المالك غير موجود." });

            if (ModelState.IsValid)
            {
                // 🛡️ 1. حماية اسم المستخدم
                string newUsername = model.Username?.Trim();
                if (string.IsNullOrEmpty(newUsername))
                    return Json(new { success = false, message = "اسم المستخدم مطلوب." });

                if (!Regex.IsMatch(newUsername, @"^[a-zA-Z0-9_]{3,20}$"))
                    return Json(new { success = false, message = "اسم المستخدم غير صالح. يرجى استخدام أحرف إنجليزية وأرقام فقط." });

                if (ownerUser.Username != newUsername)
                {
                    bool isUsernameTaken = _context.Users.Any(u => u.Username == newUsername && u.UserId != currentUserId);
                    if (isUsernameTaken)
                        return Json(new { success = false, message = "عذراً، اسم المستخدم هذا محجوز مسبقاً، يرجى اختيار اسم آخر!" });

                    ownerUser.Username = newUsername;
                }

                // 🛡️ 2. حماية البريد الإلكتروني (لا يتم التحديث المباشر من الواجهة)
                string newEmail = model.ContactInfo?.Trim();
                if (string.IsNullOrEmpty(newEmail))
                    return Json(new { success = false, message = "البريد الإلكتروني مطلوب." });

                if (ownerUser.ContactInfo != newEmail)
                {
                    return Json(new { success = false, message = "تغيير البريد الإلكتروني يتطلب إجراء التحقق الأمني أولاً." });
                }

                // 📱 3. الهاتف الدولي المدمج
                string newPhone = model.PhoneNumber?.Trim();
                if (!string.IsNullOrEmpty(newPhone))
                {
                    ownerUser.PhoneNumber = newPhone;
                }
                else
                {
                    return Json(new { success = false, message = "حقل رقم الهاتف مطلوب ولا يمكن تركه فارغاً." });
                }

                // 👤 4. تحديث باقي البيانات
                ownerUser.FullName = model.FullName;
                ownerUser.Bio = model.Bio;
                ownerUser.DateOfBirth = model.DateOfBirth;
                ownerUser.Gender = model.Gender;
                ownerUser.City = model.City;
                ownerUser.FacebookUrl = model.FacebookUrl;
                ownerUser.TwitterUrl = model.TwitterUrl;
                ownerUser.InstagramUrl = model.InstagramUrl;

                _context.Users.Update(ownerUser);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم تحديث البيانات الشخصية بنجاح!" });
            }

            return Json(new { success = false, message = "البيانات المدخلة غير صالحة." });
        }

        // =============================================================
        //  4. وظيفة رفع الصورة الشخصية (UploadProfileImage)
        // =============================================================
        [HttpPost("/Owner/Profile/UploadProfileImage")]
        public async Task<IActionResult> UploadProfileImage(IFormFile profileImage)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 2 : int.Parse(userIdClaim);

            var ownerUser = await _context.Users.FindAsync(currentUserId);
            if (ownerUser == null)
                return Json(new { success = false, message = "حساب المالك غير موجود." });

            if (profileImage != null && profileImage.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(profileImage.FileName);
                var uploadFolder = Path.Combine(_env.WebRootPath, "images", "profiles");
                var filePath = Path.Combine(uploadFolder, fileName);

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }

                ownerUser.ProfileImage = "/images/profiles/" + fileName;

                _context.Users.Update(ownerUser);
                await _context.SaveChangesAsync();

                return Json(new { success = true, imageUrl = ownerUser.ProfileImage });
            }

            return Json(new { success = false, message = "لم يتم اختيار صورة صالحة للرفع." });
        }

        // =============================================================
        //  5. وظيفة حذف الصورة الشخصية (DeleteImage)
        // =============================================================
        [HttpPost("/Owner/Profile/DeleteImage")]
        public async Task<IActionResult> DeleteImage()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 2 : int.Parse(userIdClaim);

            var ownerUser = await _context.Users.FindAsync(currentUserId);
            if (ownerUser == null)
                return Json(new { success = false, message = "حساب المالك غير موجود." });

            ownerUser.ProfileImage = null;

            _context.Users.Update(ownerUser);
            await _context.SaveChangesAsync();

            string initialLetter = !string.IsNullOrWhiteSpace(ownerUser.FullName) ? ownerUser.FullName.Trim().Substring(0, 1).ToUpper() : "O";

            return Json(new { success = true, initialLetter = initialLetter });
        }

        // =============================================================
        //  6. وظيفة حماية وتغيير كلمة المرور (ChangePassword)
        // =============================================================
        [HttpPost("/Owner/Profile/ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] OwnerPasswordUpdateDto model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 2 : int.Parse(userIdClaim);

            var ownerUser = await _context.Users.FindAsync(currentUserId);
            if (ownerUser == null)
                return Json(new { success = false, message = "حساب المالك غير موجود." });

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, ownerUser.PasswordHash))
            {
                return Json(new { success = false, message = "كلمة المرور الحالية التي أدخلتها غير صحيحة." });
            }

            if (string.IsNullOrEmpty(model.NewPassword) || !Regex.IsMatch(model.NewPassword, @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$"))
            {
                return Json(new { success = false, message = "يجب أن تتكون كلمة المرور الجديدة من 8 أحرف على الأقل، وتحتوي على حرف إنجليزي ورقم." });
            }

            if (BCrypt.Net.BCrypt.Verify(model.NewPassword, ownerUser.PasswordHash))
            {
                return Json(new { success = false, message = "كلمة المرور الجديدة يجب أن تكون مختلفة عن كلمة المرور الحالية." });
            }

            ownerUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, workFactor: 11);

            _context.Users.Update(ownerUser);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم تحديث وحماية كلمة المرور بنجاح." });
        }

        // =============================================================
        //  7. وظيفة إدارة التفضيلات (SetPreference)
        // =============================================================
        [HttpPost("/Owner/Profile/SetPreference")]
        public IActionResult SetPreference([FromBody] OwnerPreferenceDto model)
        {
            if (model != null && !string.IsNullOrEmpty(model.Key))
            {
                string actualValue = "";
                if (model.Value is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    actualValue = jsonElement.GetString();
                }
                else
                {
                    actualValue = model.Value?.ToString();
                }

                string valueString = actualValue?.ToLower();
                bool isEnabled = valueString == "true";

                var cookieOptions = new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), Path = "/", HttpOnly = false };
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 0 : int.Parse(userIdClaim);

                if (model.Key == "DarkModeEnabled")
                {
                    Response.Cookies.Append("darkMode", isEnabled ? "enabled" : "disabled", cookieOptions);
                }
                else if (model.Key == "CompactMode")
                {
                    Response.Cookies.Append($"compactMode_{currentUserId}", isEnabled ? "enabled" : "disabled", cookieOptions);
                }

                if (currentUserId > 0)
                {
                    var ownerUser = _context.Users.FirstOrDefault(u => u.UserId == currentUserId);
                    if (ownerUser != null)
                    {
                        if (model.Key == "DarkModeEnabled")
                        {
                            ownerUser.IsDarkMode = isEnabled;
                        }
                        else if (model.Key == "Language")
                        {
                            ownerUser.PreferredLanguage = actualValue;
                        }
                        else if (string.Equals(
                                     model.Key,
                                     "Currency",
                                     StringComparison.OrdinalIgnoreCase))
                        {
                            string normalizedCurrency =
                                CurrencyDisplayService.NormalizeCurrency(
                                    actualValue);

                            if (string.IsNullOrWhiteSpace(
                                    normalizedCurrency))
                            {
                                return Json(new
                                {
                                    success = false,
                                    message =
                                        "العملة المحددة غير مدعومة. اختر الريال اليمني أو الريال السعودي أو الدولار الأمريكي."
                                });
                            }

                            ownerUser.PreferredCurrency =
                                normalizedCurrency;

                            Response.Cookies.Append(
                                "preferredCurrency",
                                normalizedCurrency,
                                cookieOptions);
                        }

                        _context.SaveChanges();
                    }
                }

                return Json(new { success = true });
            }
            return Json(new { success = false, message = "بيانات التفضيل غير صالحة" });
        }

        // =============================================================
        //  8. منطقة الخطر: تعطيل الحساب (SuspendAccount)
        // =============================================================
        [HttpPost("/Owner/Profile/SuspendAccount")]
        public IActionResult SuspendAccount()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 2 : int.Parse(userIdClaim);

            var ownerUser = _context.Users.FirstOrDefault(u => u.UserId == currentUserId);
            if (ownerUser != null)
            {
                ownerUser.AccountState = "Suspended";
                _context.SaveChanges();
            }
            return Json(new { success = true });
        }
    }

    // =============================================================
    // DTO للتعامل مع طلب تغيير الإيميل 
    // =============================================================
    public class OwnerEmailChangeRequestDto
    {
        public string NewEmail { get; set; }
    }
}
