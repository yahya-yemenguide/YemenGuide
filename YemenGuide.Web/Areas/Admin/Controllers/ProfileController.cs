// =============================================================
//  ProfileController.cs - التحكم في الملف الشخصي للمدير (النسخة النهائية المحمية) 🏢🛡️
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
using YemenGuide.Core.DTOs.Admin; // ✨ توجيه لكائنات الأدمن
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
// using YemenGuide.Web.Filters; // (قم بتفعيلها إذا كان لديك فلاتر مخصصة للأدمن)
using YemenGuide.Core.Helpers;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")] // ✨ صلاحيات الإدارة
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfileController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // =============================================================
        //  1. وظيفة عرض صفحة الملف الشخصي للمدير (AdminProfile)
        // =============================================================
        [HttpGet("/Admin/Profile/AdminProfile")]
        public async Task<IActionResult> AdminProfile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out int currentUserId) ||
                currentUserId <= 0)
            {
                return Unauthorized();
            }

            var adminUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == currentUserId);

            if (adminUser == null)
            {
                return NotFound("حساب المدير غير موجود في النظام.");
            }

            string preferredCurrency =
                NormalizePreferredCurrency(adminUser.PreferredCurrency);

            if (string.IsNullOrWhiteSpace(preferredCurrency))
            {
                string? defaultCurrency = await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(s => s.Id)
                    .Select(s => s.DefaultCurrency)
                    .FirstOrDefaultAsync();

                preferredCurrency =
                    NormalizePreferredCurrency(defaultCurrency);
            }

            if (string.IsNullOrWhiteSpace(preferredCurrency))
            {
                preferredCurrency = "ريال يمني";
            }

            var viewModel = new AdminProfileDto
            {
                UserId = adminUser.UserId,
                FullName = adminUser.FullName ?? string.Empty,
                Username = adminUser.Username ?? string.Empty,

                ContactInfo = adminUser.ContactInfo ?? "",
                IsEmailVerified = adminUser.IsEmailVerified,
                PhoneNumber = adminUser.PhoneNumber ?? "",

                Bio = adminUser.Bio ?? "",
                DateOfBirth = adminUser.DateOfBirth,
                Gender = adminUser.Gender ?? "",
                City = adminUser.City ?? "",

                FacebookUrl = adminUser.FacebookUrl ?? "",
                TwitterUrl = adminUser.TwitterUrl ?? "",
                InstagramUrl = adminUser.InstagramUrl ?? "",

                Role = adminUser.Role ?? "Admin",
                AccountState = adminUser.AccountState ?? "",
                CreatedAt = adminUser.CreatedAt ?? DateTime.Now,

                DarkModeEnabled = adminUser.IsDarkMode,
                Language = adminUser.PreferredLanguage ?? "ar",
                PreferredCurrency = preferredCurrency,

                InitialLetter =
                    !string.IsNullOrWhiteSpace(adminUser.FullName)
                        ? adminUser.FullName.Trim().Substring(0, 1).ToUpper()
                        : "A",

                ProfileImage = adminUser.ProfileImage ?? ""
            };

            return View(viewModel);
        }

        // =============================================================
        //  2. طلب تغيير الإيميل (نظام التوثيق المحمي) ✨
        // =============================================================
        [HttpPost("/Admin/Profile/RequestEmailChange")]
        public async Task<IActionResult> RequestEmailChange([FromBody] AdminEmailChangeRequestDto model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 1 : int.Parse(userIdClaim);

            var adminUser = await _context.Users.FindAsync(currentUserId);
            if (adminUser == null) return Json(new { success = false, message = "حساب المدير غير موجود." });

            string newEmail = model.NewEmail?.Trim();
            if (string.IsNullOrEmpty(newEmail) || !Regex.IsMatch(newEmail, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                return Json(new { success = false, message = "البريد الإلكتروني المدخل غير صالح." });

            if (adminUser.ContactInfo == newEmail)
                return Json(new { success = false, message = "هذا هو بريدك الإلكتروني الحالي بالفعل." });

            bool isEmailTaken = _context.Users.Any(u => u.ContactInfo == newEmail && u.UserId != currentUserId);
            if (isEmailTaken)
                return Json(new { success = false, message = "عذراً، البريد الإلكتروني هذا مستخدم لحساب آخر!" });

            // توليد الكود وحفظه
            string otpCode = new Random().Next(100000, 999999).ToString();
            adminUser.VerificationCode = otpCode;
            adminUser.CodeExpiry = DateTime.Now.AddMinutes(5);

            // حفظ الإيميل الجديد مؤقتاً
            TempData["PendingEmail"] = newEmail;

            await _context.SaveChangesAsync();

            // إرسال الكود
            _ = EmailHelper.SendOtpEmailAsync(newEmail, otpCode);

            return Json(new { success = true });
        }

        // =============================================================
        //  3. وظيفة تحديث البيانات الشخصية الأساسية للمدير (UpdatePersonalInfo)
        // =============================================================
        [HttpPost("/Admin/Profile/UpdatePersonalInfo")]
        public async Task<IActionResult> UpdatePersonalInfo([FromBody] AdminPersonalInfoUpdateDto model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 1 : int.Parse(userIdClaim);

            var adminUser = await _context.Users.FindAsync(currentUserId);
            if (adminUser == null)
                return Json(new { success = false, message = "حساب المدير غير موجود." });

            if (ModelState.IsValid)
            {
                // 🛡️ 1. حماية اسم المستخدم
                string newUsername = model.Username?.Trim();
                if (string.IsNullOrEmpty(newUsername))
                    return Json(new { success = false, message = "اسم المستخدم مطلوب." });

                if (!Regex.IsMatch(newUsername, @"^[a-zA-Z0-9_]{3,20}$"))
                    return Json(new { success = false, message = "اسم المستخدم غير صالح. يرجى استخدام أحرف إنجليزية وأرقام فقط." });

                if (adminUser.Username != newUsername)
                {
                    bool isUsernameTaken = _context.Users.Any(u => u.Username == newUsername && u.UserId != currentUserId);
                    if (isUsernameTaken)
                        return Json(new { success = false, message = "عذراً، اسم المستخدم هذا محجوز مسبقاً، يرجى اختيار اسم آخر!" });

                    adminUser.Username = newUsername;
                }

                // 🛡️ 2. حماية البريد الإلكتروني (لا يتم التحديث المباشر من الواجهة)
                string newEmail = model.ContactInfo?.Trim();
                if (string.IsNullOrEmpty(newEmail))
                    return Json(new { success = false, message = "البريد الإلكتروني مطلوب." });

                if (adminUser.ContactInfo != newEmail)
                {
                    return Json(new { success = false, message = "تغيير البريد الإلكتروني يتطلب إجراء التحقق الأمني أولاً." });
                }

                // 📱 3. الهاتف الدولي المدمج
                string newPhone = model.PhoneNumber?.Trim();
                if (!string.IsNullOrEmpty(newPhone))
                {
                    adminUser.PhoneNumber = newPhone;
                }
                else
                {
                    return Json(new { success = false, message = "حقل رقم الهاتف مطلوب ولا يمكن تركه فارغاً." });
                }

                // 👤 4. تحديث باقي البيانات
                adminUser.FullName = model.FullName;
                adminUser.Bio = model.Bio;
                adminUser.DateOfBirth = model.DateOfBirth;
                adminUser.Gender = model.Gender;
                adminUser.City = model.City;
                adminUser.FacebookUrl = model.FacebookUrl;
                adminUser.TwitterUrl = model.TwitterUrl;
                adminUser.InstagramUrl = model.InstagramUrl;

                _context.Users.Update(adminUser);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم تحديث البيانات الشخصية بنجاح!" });
            }

            return Json(new { success = false, message = "البيانات المدخلة غير صالحة." });
        }

        // =============================================================
        //  4. وظيفة رفع الصورة الشخصية (UploadProfileImage)
        // =============================================================
        [HttpPost("/Admin/Profile/UploadProfileImage")]
        public async Task<IActionResult> UploadProfileImage(IFormFile profileImage)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 1 : int.Parse(userIdClaim);

            var adminUser = await _context.Users.FindAsync(currentUserId);
            if (adminUser == null)
                return Json(new { success = false, message = "حساب المدير غير موجود." });

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

                adminUser.ProfileImage = "/images/profiles/" + fileName;

                _context.Users.Update(adminUser);
                await _context.SaveChangesAsync();

                return Json(new { success = true, imageUrl = adminUser.ProfileImage });
            }

            return Json(new { success = false, message = "لم يتم اختيار صورة صالحة للرفع." });
        }

        // =============================================================
        //  5. وظيفة حذف الصورة الشخصية (DeleteImage)
        // =============================================================
        [HttpPost("/Admin/Profile/DeleteImage")]
        public async Task<IActionResult> DeleteImage()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 1 : int.Parse(userIdClaim);

            var adminUser = await _context.Users.FindAsync(currentUserId);
            if (adminUser == null)
                return Json(new { success = false, message = "حساب المدير غير موجود." });

            adminUser.ProfileImage = null;

            _context.Users.Update(adminUser);
            await _context.SaveChangesAsync();

            string initialLetter = !string.IsNullOrWhiteSpace(adminUser.FullName) ? adminUser.FullName.Trim().Substring(0, 1).ToUpper() : "A";

            return Json(new { success = true, initialLetter = initialLetter });
        }

        // =============================================================
        //  6. وظيفة حماية وتغيير كلمة المرور (ChangePassword)
        // =============================================================
        [HttpPost("/Admin/Profile/ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] AdminPasswordUpdateDto model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 1 : int.Parse(userIdClaim);

            var adminUser = await _context.Users.FindAsync(currentUserId);
            if (adminUser == null)
                return Json(new { success = false, message = "حساب المدير غير موجود." });

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, adminUser.PasswordHash))
            {
                return Json(new { success = false, message = "كلمة المرور الحالية التي أدخلتها غير صحيحة." });
            }

            if (string.IsNullOrEmpty(model.NewPassword) || !Regex.IsMatch(model.NewPassword, @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$"))
            {
                return Json(new { success = false, message = "يجب أن تتكون كلمة المرور الجديدة من 8 أحرف على الأقل، وتحتوي على حرف إنجليزي ورقم." });
            }

            if (BCrypt.Net.BCrypt.Verify(model.NewPassword, adminUser.PasswordHash))
            {
                return Json(new { success = false, message = "كلمة المرور الجديدة يجب أن تكون مختلفة عن كلمة المرور الحالية." });
            }

            adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, workFactor: 11);

            _context.Users.Update(adminUser);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم تحديث وحماية كلمة المرور بنجاح." });
        }

        // =============================================================
        //  7. وظيفة إدارة التفضيلات (SetPreference)
        // =============================================================
        [HttpPost("/Admin/Profile/SetPreference")]
        public async Task<IActionResult> SetPreference(
            [FromBody] AdminPreferenceDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Key))
            {
                return Json(new
                {
                    success = false,
                    message = "بيانات التفضيل غير صالحة."
                });
            }

            string actualValue = ReadPreferenceValue(model.Value);
            string key = model.Key.Trim();

            var userIdClaim =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out int currentUserId) ||
                currentUserId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "تعذر تحديد حساب المدير الحالي."
                });
            }

            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            };

            string valueString =
                actualValue?.Trim().ToLowerInvariant() ?? string.Empty;

            bool isEnabled =
                valueString == "true" ||
                valueString == "enabled";

            if (key == "CompactMode")
            {
                Response.Cookies.Append(
                    $"compactMode_{currentUserId}",
                    isEnabled ? "enabled" : "disabled",
                    cookieOptions);

                return Json(new { success = true });
            }

            var adminUser =
                await _context.Users.FindAsync(currentUserId);

            if (adminUser == null)
            {
                return Json(new
                {
                    success = false,
                    message = "حساب المدير غير موجود."
                });
            }

            if (key == "DarkModeEnabled")
            {
                Response.Cookies.Append(
                    "darkMode",
                    isEnabled ? "enabled" : "disabled",
                    cookieOptions);

                adminUser.IsDarkMode = isEnabled;
            }
            else if (key == "Language")
            {
                adminUser.PreferredLanguage =
                    string.IsNullOrWhiteSpace(actualValue)
                        ? "ar"
                        : actualValue.Trim();
            }
            else if (key == "PreferredCurrency")
            {
                string normalizedCurrency =
                    NormalizePreferredCurrency(actualValue);

                if (string.IsNullOrWhiteSpace(normalizedCurrency))
                {
                    return Json(new
                    {
                        success = false,
                        message = "العملة المختارة غير مدعومة."
                    });
                }

                adminUser.PreferredCurrency =
                    normalizedCurrency;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    preferredCurrency = normalizedCurrency,
                    message = "تم تحديث عملة عرض الأسعار."
                });
            }
            else
            {
                return Json(new
                {
                    success = false,
                    message = "هذا التفضيل غير مدعوم في النظام الحالي."
                });
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }


        private static string ReadPreferenceValue(object? value)
        {
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String =>
                        jsonElement.GetString() ?? string.Empty,

                    System.Text.Json.JsonValueKind.True =>
                        "true",

                    System.Text.Json.JsonValueKind.False =>
                        "false",

                    _ => jsonElement.ToString()
                };
            }

            return value?.ToString() ?? string.Empty;
        }


        private static string NormalizePreferredCurrency(
            string? currency)
        {
            string value =
                (currency ?? string.Empty).Trim();

            return value switch
            {
                "ريال يمني" => "ريال يمني",
                "ريال يمني (قديم)" => "ريال يمني",
                "ريال يمني (جديد)" => "ريال يمني",
                "YER" => "ريال يمني",

                "ريال سعودي" => "ريال سعودي",
                "SAR" => "ريال سعودي",

                "دولار أمريكي" => "دولار أمريكي",
                "دولار امريكي" => "دولار أمريكي",
                "USD" => "دولار أمريكي",

                _ => string.Empty
            };
        }


        // =============================================================
        //  8. منطقة الخطر: تعطيل الحساب (SuspendAccount)
        // =============================================================
        [HttpPost("/Admin/Profile/SuspendAccount")]
        public IActionResult SuspendAccount()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = string.IsNullOrEmpty(userIdClaim) ? 1 : int.Parse(userIdClaim);

            var adminUser = _context.Users.FirstOrDefault(u => u.UserId == currentUserId);
            if (adminUser != null)
            {
                adminUser.AccountState = "Suspended";
                _context.SaveChanges();
            }
            return Json(new { success = true });
        }
    }
}
