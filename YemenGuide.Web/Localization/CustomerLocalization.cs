using System;

namespace YemenGuide.Web.Localization
{
    /// <summary>
    /// المصدر المركزي لأسماء اللغات المدعومة وCookie اختيار اللغة.
    /// لا يحتوي أي نصوص ترجمة؛ دوره فقط ضبط المحرك.
    /// </summary>
    public static class CustomerLocalization
    {
        public const string ArabicCulture = "ar";
        public const string EnglishCulture = "en";
        public const string DefaultCulture = ArabicCulture;

        /// <summary>
        /// Cookie مستقلة عن Cookie ASP.NET الافتراضية حتى يبقى
        /// اختيار الزائر محفوظًا حتى بعد تسجيل الخروج.
        /// </summary>
        public const string CultureCookieName =
            "YemenGuide.Culture";

        public static bool TryNormalize(
            string? value,
            out string culture)
        {
            culture = DefaultCulture;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized =
                value
                    .Trim()
                    .Replace('_', '-')
                    .Split(
                        '-',
                        StringSplitOptions
                            .RemoveEmptyEntries
                    )[0]
                    .ToLowerInvariant();

            if (
                string.Equals(
                    normalized,
                    ArabicCulture,
                    StringComparison.Ordinal
                )
            )
            {
                culture = ArabicCulture;
                return true;
            }

            if (
                string.Equals(
                    normalized,
                    EnglishCulture,
                    StringComparison.Ordinal
                )
            )
            {
                culture = EnglishCulture;
                return true;
            }

            return false;
        }

        public static string NormalizeOrDefault(
            string? value)
        {
            return TryNormalize(
                value,
                out var culture)
                ? culture
                : DefaultCulture;
        }
    }
}
