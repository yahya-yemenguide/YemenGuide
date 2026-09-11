// =============================================================
// LanguageController.cs
// المحرك المركزي لتغيير لغة الموقع
// =============================================================

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using YemenGuide.Data;
using YemenGuide.Web.Localization;

namespace YemenGuide.Web.Controllers
{
    public class LanguageController : Controller
    {
        private readonly AppDbContext _context;

        public LanguageController(
            AppDbContext context)
        {
            _context = context;
        }

        // =============================================================
        // تغيير اللغة
        //
        // الزائر:
        //   نحفظ Cookie الخاصة بالموقع.
        //
        // المستخدم المسجل:
        //   نحفظ Cookie + PreferredLanguage في قاعدة البيانات
        //   ونحدّث Claim اللغة في جلسة الدخول الحالية.
        // =============================================================

        [HttpPost]
        public async Task<IActionResult> SetLanguage(
            [FromBody] LanguageRequest? model)
        {
            if (
                !CustomerLocalization.TryNormalize(
                    model?.Culture,
                    out var culture
                )
            )
            {
                return BadRequest(
                    new
                    {
                        success = false,
                        message =
                            "Unsupported language."
                    });
            }

            /*
             * Cookie اختيار الزائر.
             * تبقى موجودة عند تسجيل الخروج لأنها مستقلة
             * عن Cookie ASP.NET الافتراضية.
             */
            Response.Cookies.Append(
                CustomerLocalization
                    .CultureCookieName,
                culture,
                CreateCultureCookieOptions()
            );

            /*
             * نكتب كذلك Cookie ASP.NET القياسية للتوافق
             * مع أي كود قديم ما زال يعتمد عليها.
             */
            Response.Cookies.Append(
                CookieRequestCultureProvider
                    .DefaultCookieName,
                CookieRequestCultureProvider
                    .MakeCookieValue(
                        new RequestCulture(
                            culture
                        )
                    ),
                CreateCultureCookieOptions()
            );

            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            if (
                User.Identity?.IsAuthenticated ==
                    true &&
                int.TryParse(
                    userIdText,
                    out var userId
                ) &&
                userId > 0
            )
            {
                var user =
                    await _context.Users
                        .FirstOrDefaultAsync(
                            item =>
                                item.UserId ==
                                userId
                        );

                if (user != null)
                {
                    user.PreferredLanguage =
                        culture;

                    await _context
                        .SaveChangesAsync();

                    await RefreshLanguageClaimAsync(
                        culture);
                }
            }

            return Json(
                new
                {
                    success = true,
                    culture,
                    direction =
                        culture ==
                        CustomerLocalization
                            .ArabicCulture
                            ? "rtl"
                            : "ltr"
                });
        }

        private CookieOptions
            CreateCultureCookieOptions()
        {
            return new CookieOptions
            {
                Expires =
                    DateTimeOffset.UtcNow
                        .AddYears(1),

                MaxAge =
                    TimeSpan.FromDays(365),

                Path = "/",

                HttpOnly = true,

                IsEssential = true,

                SameSite =
                    SameSiteMode.Lax,

                Secure =
                    Request.IsHttps
            };
        }

        /// <summary>
        /// عندما يغيّر مستخدم مسجل اللغة، نحدّث Claim اللغة
        /// في Cookie تسجيل الدخول فورًا حتى يقرأها
        /// RequestLocalization في الطلب التالي بدون الحاجة
        /// إلى تسجيل خروج ودخول.
        /// </summary>
        private async Task
            RefreshLanguageClaimAsync(
                string culture)
        {
            var authResult =
                await HttpContext
                    .AuthenticateAsync(
                        CookieAuthenticationDefaults
                            .AuthenticationScheme
                    );

            if (
                !authResult.Succeeded ||
                authResult.Principal == null
            )
            {
                return;
            }

            var claims =
                authResult.Principal.Claims
                    .Where(
                        claim =>
                            !string.Equals(
                                claim.Type,
                                "PreferredLanguage",
                                StringComparison.Ordinal
                            )
                    )
                    .ToList();

            claims.Add(
                new Claim(
                    "PreferredLanguage",
                    culture
                )
            );

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme,
                    ClaimTypes.Name,
                    ClaimTypes.Role
                );

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authResult.Properties
            );
        }
    }

    public class LanguageRequest
    {
        public string? Culture
        {
            get;
            set;
        }
    }
}
