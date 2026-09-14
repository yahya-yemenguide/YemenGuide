using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using YemenGuide.Data;

namespace YemenGuide.Web.Controllers
{
    [Authorize]
    [Route("Profile/DisplayPreferences")]
    public class DisplayPreferencesController : Controller
    {
        private readonly AppDbContext _context;

        public DisplayPreferencesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPreferences()
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "انتهت جلسة تسجيل الدخول. يرجى تسجيل الدخول مرة أخرى."
                });
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == currentUserId);

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الحساب غير موجود."
                });
            }

            if (!IsSupportedRole(user.Role))
            {
                return Forbid();
            }

            var alertPreferences =
                await ReadAlertPreferencesAsync(currentUserId);

            bool isOwner =
                string.Equals(
                    user.Role,
                    "Owner",
                    StringComparison.OrdinalIgnoreCase);

            bool compactMode =
                IsEnabledCookie(
                    Request.Cookies[
                        isOwner
                            ? "compactMode_owner"
                            : "compactMode_customer"]);

            if (isOwner && !compactMode)
            {
                compactMode =
                    IsEnabledCookie(
                        Request.Cookies[$"compactMode_{currentUserId}"]);
            }

            return Json(new
            {
                success = true,
                requiresDatabaseUpdate = !alertPreferences.Ready,
                darkModeEnabled = user.IsDarkMode,
                compactMode,
                language = string.IsNullOrWhiteSpace(user.PreferredLanguage)
                    ? "ar"
                    : user.PreferredLanguage,
                preferredCurrency = NormalizeCurrency(user.PreferredCurrency),
                receiveOffers = user.ReceiveOffers ?? true,
                newReservationAlert = alertPreferences.NewReservationAlert,
                newMessageAlert = alertPreferences.NewMessageAlert,
                newReviewAlert = alertPreferences.NewReviewAlert,
                soundAlert = alertPreferences.SoundAlert
            });
        }

        [HttpPost]
        public async Task<IActionResult> SavePreferences(
            [FromBody] ProfileDisplayPreferencesUpdateDto model)
        {
            if (model == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "بيانات الإعدادات غير صالحة."
                });
            }

            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "انتهت جلسة تسجيل الدخول. يرجى تسجيل الدخول مرة أخرى."
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(item => item.UserId == currentUserId);

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الحساب غير موجود."
                });
            }

            if (!IsSupportedRole(user.Role))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message = "هذه الإعدادات متاحة لحساب العميل أو المالك فقط."
                    });
            }

            var currentAlertPreferences =
                await ReadAlertPreferencesAsync(currentUserId);

            if (!currentAlertPreferences.Ready)
            {
                return StatusCode(
                    StatusCodes.Status409Conflict,
                    new
                    {
                        success = false,
                        requiresDatabaseUpdate = true,
                        message = "قاعدة البيانات تحتاج تحديث إعدادات العرض أولاً. شغّل ملف AddProfileDisplayPreferences.sql مرة واحدة ثم أعد المحاولة."
                    });
            }

            string language =
                string.Equals(
                    model.Language,
                    "en",
                    StringComparison.OrdinalIgnoreCase)
                    ? "en"
                    : "ar";

            string preferredCurrency =
                NormalizeCurrency(model.PreferredCurrency);

            bool isOwner =
                string.Equals(
                    user.Role,
                    "Owner",
                    StringComparison.OrdinalIgnoreCase);

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            user.IsDarkMode = model.DarkModeEnabled;
            user.PreferredLanguage = language;
            user.PreferredCurrency = preferredCurrency;

            if (!isOwner && model.ReceiveOffers.HasValue)
            {
                user.ReceiveOffers = model.ReceiveOffers.Value;
            }

            await _context.SaveChangesAsync();

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.Users
                SET NewReservationAlert = {model.NewReservationAlert},
                    NewMessageAlert = {model.NewMessageAlert},
                    NewReviewAlert = {model.NewReviewAlert},
                    SoundAlert = {model.SoundAlert}
                WHERE UserID = {currentUserId};");

            await transaction.CommitAsync();

            WritePreferenceCookies(
                currentUserId,
                isOwner,
                model,
                language,
                preferredCurrency);

            return Json(new
            {
                success = true,
                message = "تم حفظ إعدادات العرض بنجاح."
            });
        }

        private bool TryGetCurrentUserId(out int currentUserId)
        {
            currentUserId = 0;

            var claim =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(claim, out currentUserId) &&
                   currentUserId > 0;
        }

        private static bool IsSupportedRole(string? role)
        {
            return string.Equals(
                       role,
                       "Owner",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       role,
                       "Customer",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEnabledCookie(string? value)
        {
            return string.Equals(
                       value,
                       "enabled",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       value,
                       "true",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeCurrency(string? value)
        {
            var normalized =
                (value ?? string.Empty).Trim();

            return normalized switch
            {
                "ريال سعودي" => "ريال سعودي",
                "دولار أمريكي" => "دولار أمريكي",
                "ريال يمني" => "ريال يمني",
                "ريال يمني (قديم)" => "ريال يمني",
                "ريال يمني (جديد)" => "ريال يمني",
                _ => "ريال يمني"
            };
        }

        private void WritePreferenceCookies(
            int currentUserId,
            bool isOwner,
            ProfileDisplayPreferencesUpdateDto model,
            string language,
            string preferredCurrency)
        {
            var options = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/",
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Append(
                "darkMode",
                model.DarkModeEnabled ? "enabled" : "disabled",
                options);

            Response.Cookies.Append(
                "preferredCurrency",
                preferredCurrency,
                options);

            string compactCookieName =
                isOwner
                    ? "compactMode_owner"
                    : "compactMode_customer";

            Response.Cookies.Append(
                compactCookieName,
                model.CompactMode ? "enabled" : "disabled",
                options);

            if (isOwner)
            {
                Response.Cookies.Append(
                    $"compactMode_{currentUserId}",
                    model.CompactMode ? "enabled" : "disabled",
                    options);
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(
                        new CultureInfo(language))),
                options);
        }

        private async Task<AlertPreferencesReadResult>
            ReadAlertPreferencesAsync(int currentUserId)
        {
            var connection =
                _context.Database.GetDbConnection();

            bool shouldClose =
                connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var checkCommand =
                    connection.CreateCommand();

                checkCommand.CommandText = @"
                    SELECT COUNT(*)
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'dbo.Users')
                      AND name IN
                      (
                          N'NewReservationAlert',
                          N'NewMessageAlert',
                          N'NewReviewAlert',
                          N'SoundAlert'
                      );";

                int existingColumnCount =
                    Convert.ToInt32(
                        await checkCommand.ExecuteScalarAsync());

                if (existingColumnCount != 4)
                {
                    return AlertPreferencesReadResult.NotReady();
                }

                await using var command =
                    connection.CreateCommand();

                command.CommandText = @"
                    SELECT
                        NewReservationAlert,
                        NewMessageAlert,
                        NewReviewAlert,
                        SoundAlert
                    FROM dbo.Users
                    WHERE UserID = @UserId;";

                var parameter =
                    command.CreateParameter();

                parameter.ParameterName = "@UserId";
                parameter.Value = currentUserId;
                command.Parameters.Add(parameter);

                await using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return AlertPreferencesReadResult.NotReady();
                }

                return new AlertPreferencesReadResult
                {
                    Ready = true,
                    NewReservationAlert = reader.GetBoolean(0),
                    NewMessageAlert = reader.GetBoolean(1),
                    NewReviewAlert = reader.GetBoolean(2),
                    SoundAlert = reader.GetBoolean(3)
                };
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private sealed class AlertPreferencesReadResult
        {
            public bool Ready { get; set; }
            public bool NewReservationAlert { get; set; }
            public bool NewMessageAlert { get; set; }
            public bool NewReviewAlert { get; set; }
            public bool SoundAlert { get; set; }

            public static AlertPreferencesReadResult NotReady()
            {
                return new AlertPreferencesReadResult
                {
                    Ready = false,
                    NewReservationAlert = false,
                    NewMessageAlert = false,
                    NewReviewAlert = false,
                    SoundAlert = false
                };
            }
        }
    }

    public class ProfileDisplayPreferencesUpdateDto
    {
        public bool DarkModeEnabled { get; set; }
        public bool CompactMode { get; set; }
        public string Language { get; set; } = "ar";
        public string PreferredCurrency { get; set; } = "ريال يمني";
        public bool NewReservationAlert { get; set; }
        public bool NewMessageAlert { get; set; }
        public bool NewReviewAlert { get; set; }
        public bool SoundAlert { get; set; }
        public bool? ReceiveOffers { get; set; }
    }
}
