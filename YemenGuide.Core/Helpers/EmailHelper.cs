using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace YemenGuide.Core.Helpers
{
    public static class EmailHelper
    {
        private const string DefaultFromEmail = "Dalilakalyemeni@gmail.com";

        // يتم تهيئة هذه القيم مرة واحدة من Program.cs.
        // الأولوية: appsettings / Environment Variables عبر IConfiguration،
        // مع دعم أسماء Environment Variables القديمة أيضاً حتى لا ينكسر النشر الحالي.
        private static string _configuredFromEmail = DefaultFromEmail;
        private static string? _configuredAppPassword;
        private static string _configuredBaseUrl = "https://localhost:7268";

        public static void Configure(
            string? fromEmail,
            string? appPassword,
            string? baseUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(fromEmail))
                _configuredFromEmail = fromEmail.Trim();

            if (!string.IsNullOrWhiteSpace(appPassword))
                _configuredAppPassword = NormalizeAppPassword(appPassword);

            if (!string.IsNullOrWhiteSpace(baseUrl))
                _configuredBaseUrl = baseUrl.Trim().TrimEnd('/');
        }

        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ResolveAppPassword());

        private static string FromEmail
        {
            get
            {
                var legacyEnvironmentEmail =
                    Environment.GetEnvironmentVariable("YEMENGUIDE_SMTP_EMAIL");

                return !string.IsNullOrWhiteSpace(legacyEnvironmentEmail)
                    ? legacyEnvironmentEmail.Trim()
                    : _configuredFromEmail;
            }
        }

        private static string AppPassword =>
            ResolveAppPassword()
            ?? throw new InvalidOperationException(
                "SMTP_NOT_CONFIGURED: EmailSettings:AppPassword is missing.");

        private static string? ResolveAppPassword()
        {
            // دعم الإعداد القديم لو كان موجوداً على الاستضافة.
            var legacyEnvironmentPassword =
                Environment.GetEnvironmentVariable("YEMENGUIDE_SMTP_APP_PASSWORD");

            if (!string.IsNullOrWhiteSpace(legacyEnvironmentPassword))
                return NormalizeAppPassword(legacyEnvironmentPassword);

            return string.IsNullOrWhiteSpace(_configuredAppPassword)
                ? null
                : _configuredAppPassword;
        }

        private static string NormalizeAppPassword(string value)
        {
            // Google يعرض App Password أحياناً بمسافات؛ SMTP يحتاج القيمة بدون مسافات.
            return value.Trim().Replace(" ", string.Empty);
        }

        public static async Task SendOtpEmailAsync(
            string toEmail,
            string otpCode,
            bool isPasswordReset = false)
        {
            string subject = isPasswordReset
                ? "إعادة تعيين كلمة المرور - منصة دليلك اليمني 🇾🇪"
                : "رمز التحقق - منصة دليلك اليمني 🇾🇪";

            string title = isPasswordReset
                ? "طلب تغيير كلمة المرور"
                : "أهلاً بك في مجتمع دليلك اليمني!";

            string messageText = isPasswordReset
                ? "لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بحسابك. لإتمام العملية، يرجى إدخال رمز التحقق التالي:"
                : "لضمان أمان حسابك، يرجى إدخال رمز التحقق التالي:";

            using var message = new MailMessage(FromEmail, toEmail)
            {
                Subject = subject,
                Body = $@"
                    <div style='text-align:right;direction:rtl;font-family:Tahoma,Arial,sans-serif;padding:20px;border:1px solid #eee;border-radius:10px;max-width:500px;margin:auto;'>
                        <h2 style='color:#05203C;'>{title}</h2>
                        <p style='font-size:16px;color:#555;'>{messageText}</p>
                        <div style='background-color:#f8f9fa;padding:15px;text-align:center;border-radius:8px;margin:20px 0;'>
                            <strong style='font-size:28px;letter-spacing:5px;color:#00A6DA;'>{WebUtility.HtmlEncode(otpCode)}</strong>
                        </div>
                        <p style='font-size:14px;color:#999;'>هذا الرمز صالح لمدة دقيقتين فقط. إذا لم تطلب هذا الرمز، يمكنك تجاهل هذه الرسالة بأمان.</p>
                    </div>",
                IsBodyHtml = true
            };

            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        public static async Task SendEmployeeCredentialsEmailAsync(
            string toEmail,
            string fullName,
            string username,
            string temporaryPassword,
            string roleName,
            bool isPasswordReset)
        {
            var environmentBaseUrl =
                Environment.GetEnvironmentVariable("YEMENGUIDE_BASE_URL");

            var baseUrl = !string.IsNullOrWhiteSpace(environmentBaseUrl)
                ? environmentBaseUrl.Trim().TrimEnd('/')
                : _configuredBaseUrl;

            var loginUrl = baseUrl + "/Account/Login";

            var safeName = WebUtility.HtmlEncode(fullName ?? string.Empty);
            var safeUsername = WebUtility.HtmlEncode(username ?? string.Empty);
            var safePassword = WebUtility.HtmlEncode(temporaryPassword ?? string.Empty);
            var safeRole = WebUtility.HtmlEncode(roleName ?? "موظف إدارة");
            var safeLoginUrl = WebUtility.HtmlEncode(loginUrl);

            string subject = isPasswordReset
                ? "بيانات الدخول المؤقتة الجديدة - دليلك اليمني"
                : "تم إنشاء حسابك الإداري - دليلك اليمني";

            string intro = isPasswordReset
                ? "تم إنشاء كلمة مرور مؤقتة جديدة لحسابك الإداري."
                : "تم إنشاء حساب موظف إدارة خاص بك في منصة دليلك اليمني.";

            using var message = new MailMessage(FromEmail, toEmail)
            {
                Subject = subject,
                IsBodyHtml = true,
                Body = $@"
<!doctype html>
<html lang='ar' dir='rtl'>
<body style='margin:0;padding:0;background:#f3f8fb;font-family:Tahoma,Arial,sans-serif;color:#05203C;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background:#f3f8fb;padding:28px 12px;'>
        <tr><td align='center'>
            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='max-width:600px;background:#ffffff;border:1px solid #e4edf3;border-radius:24px;overflow:hidden;'>
                <tr><td style='padding:30px;text-align:right;border-bottom:1px solid #e4edf3;'>
                    <div style='color:#00A6DA;font-size:12px;font-weight:bold;'>حساب موظف إدارة</div>
                    <h1 style='margin:5px 0;color:#05203C;'>مرحباً {safeName}</h1>
                    <p style='color:#6f8293;'>{intro}</p>
                </td></tr>
                <tr><td style='padding:24px 30px;'>
                    <p><strong>اسم المستخدم:</strong> <span dir='ltr'>{safeUsername}</span></p>
                    <p><strong>كلمة المرور المؤقتة:</strong> <span dir='ltr'>{safePassword}</span></p>
                    <p><strong>الدور:</strong> {safeRole}</p>
                    <p style='color:#8f641a;'>عند أول تسجيل دخول سيُطلب منك إنشاء كلمة مرور جديدة خاصة بك.</p>
                    <a href='{safeLoginUrl}' style='display:block;padding:15px;background:#00A6DA;color:#fff;text-decoration:none;text-align:center;border-radius:14px;font-weight:bold;'>تسجيل الدخول إلى دليلك اليمني</a>
                </td></tr>
            </table>
        </td></tr>
    </table>
</body>
</html>"
            };

            using var client = CreateClient();
            await client.SendMailAsync(message);
        }

        private static SmtpClient CreateClient()
        {
            return new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(FromEmail, AppPassword),
                EnableSsl = true
            };
        }
    }
}
