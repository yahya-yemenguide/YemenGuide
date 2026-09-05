// =============================================================
//  LanguageController.cs - التحكم في تعدد اللغات
// =============================================================


// =============================================================
//  1. استيراد المكتبات المطلوبة (Using Directives)
// =============================================================

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims; // أضفنا هذا لقراءة هوية المستخدم الحالية
using System.Linq; // للبحث في قاعدة البيانات
using YemenGuide.Data; // للوصول إلى الجداول


// =============================================================
//  2. تعريف مسار التسمية والكلاس (Namespace & Class)
// =============================================================

namespace YemenGuide.Web.Controllers
{
    public class LanguageController : Controller
    {
        // 🔥 إضافة الاتصال بقاعدة البيانات
        private readonly AppDbContext _context;

        public LanguageController(AppDbContext context)
        {
            _context = context;
        }

        // =============================================================
        //  3. دالة تغيير اللغة (SetLanguage) - POST
        // =============================================================

        [HttpPost]
        public IActionResult SetLanguage([FromBody] LanguageRequest model)
        {
            if (model != null && !string.IsNullOrEmpty(model.Culture))
            {
                // =============================================================
                //  3.1. حفظ كوكيز اللغة العامة القياسية للمتصفح الحالي
                // =============================================================

                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(model.Culture)),
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        Path = "/",
                        HttpOnly = true
                    }
                );

                // =============================================================
                //  3.2. ربط اللغة برقم حساب المستخدم الحالي (إذا كان مسجلاً للدخول)
                // =============================================================

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    // إنشاء كوكيز فريدة مشتقة من الرقم التعريفي للمستخدم لكي لا تضيع بعد تسجيل الخروج
                    Response.Cookies.Append(
                        $"UserCulture_{userId}",
                        model.Culture,
                        new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddYears(1),
                            Path = "/",
                            HttpOnly = true
                        }
                    );

                    // 🔥 التعديل الأهم: حفظ اللغة في قاعدة البيانات لكي لا تنحذف عند تسجيل الدخول القادم!
                    int currentUserId = int.Parse(userId);
                    var user = _context.Users.FirstOrDefault(u => u.UserId == currentUserId);
                    if (user != null)
                    {
                        user.PreferredLanguage = model.Culture;
                        _context.SaveChanges();
                    }
                }

                return Json(new { success = true });
            }
            return Json(new { success = false, message = "ثقافة غير صالحة" });
        }
    }


    // =============================================================
    //  4. تعريف الكلاس الداخلي (LanguageRequest)
    // =============================================================

    public class LanguageRequest
    {
        public string Culture { get; set; }
    }
}
