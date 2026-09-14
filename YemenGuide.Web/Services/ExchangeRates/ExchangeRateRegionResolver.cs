using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace YemenGuide.Web.Services.ExchangeRates
{
    public sealed class ExchangeRateRegionResolver
    {
        public const string SanaaMarket = "North";
        public const string AdenMarket = "South";

        private static readonly HashSet<string> SanaaMarketNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "صنعاء",
                "امانه العاصمه",
                "العاصمه",
                "صنعاء القديمه",
                "صعده",
                "عمران",
                "ذمار",
                "اب",
                "حجه",
                "المحويت",
                "ريمه",
                "الحديده",
                "البيضاء",
                "الجوف",
                "sanaa",
                "sana a",
                "amanat al asimah",
                "saada",
                "amran",
                "dhamar",
                "ibb",
                "hajjah",
                "al mahwit",
                "raymah",
                "al hudaydah",
                "hodeidah",
                "al bayda",
                "al jawf"
            };

        private static readonly HashSet<string> AdenMarketNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "عدن",
                "تعز",
                "مارب",
                "حضرموت",
                "المكلا",
                "مكلا",
                "سيئون",
                "سيون",
                "تريم",
                "الشحر",
                "شبوه",
                "عتق",
                "ابين",
                "زنجبار",
                "لحج",
                "الحوطه",
                "الضالع",
                "المهره",
                "الغيضه",
                "سقطري",
                "سقطرى",
                "حديبو",
                "aden",
                "taiz",
                "marib",
                "ma rib",
                "hadramout",
                "hadramawt",
                "mukalla",
                "seiyun",
                "tarim",
                "ash shihr",
                "shabwah",
                "shabwa",
                "ataq",
                "abyan",
                "zinjibar",
                "lahij",
                "al hawtah",
                "al dhale e",
                "al mahrah",
                "al ghaydah",
                "socotra",
                "hadibu"
            };

        public static bool IsValidRegion(string? region)
        {
            return string.Equals(region, SanaaMarket, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(region, AdenMarket, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeRegion(string? region)
        {
            if (string.Equals(region, SanaaMarket, StringComparison.OrdinalIgnoreCase))
            {
                return SanaaMarket;
            }

            if (string.Equals(region, AdenMarket, StringComparison.OrdinalIgnoreCase))
            {
                return AdenMarket;
            }

            throw new ArgumentException(
                "سوق الصرف يجب أن يكون سوق صنعاء (North) أو سوق عدن (South).",
                nameof(region));
        }

        public string Resolve(string? cityName)
        {
            if (TryResolve(cityName, out string region))
            {
                return region;
            }

            throw new InvalidOperationException(
                $"المدينة '{cityName?.Trim()}' غير مصنفة ضمن سوق صنعاء أو سوق عدن. راجع إدارة المدن وحدد سوق الصرف.");
        }

        public bool TryResolve(string? cityName, out string region)
        {
            string normalized = Normalize(cityName);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                region = string.Empty;
                return false;
            }

            if (AdenMarketNames.Contains(normalized))
            {
                region = AdenMarket;
                return true;
            }

            if (SanaaMarketNames.Contains(normalized))
            {
                region = SanaaMarket;
                return true;
            }

            foreach (string name in AdenMarketNames)
            {
                if (ContainsWholeNormalizedName(normalized, name))
                {
                    region = AdenMarket;
                    return true;
                }
            }

            foreach (string name in SanaaMarketNames)
            {
                if (ContainsWholeNormalizedName(normalized, name))
                {
                    region = SanaaMarket;
                    return true;
                }
            }

            region = string.Empty;
            return false;
        }

        private static bool ContainsWholeNormalizedName(
            string normalizedValue,
            string normalizedName)
        {
            if (normalizedValue == normalizedName)
            {
                return true;
            }

            return normalizedValue.StartsWith(normalizedName + " ", StringComparison.Ordinal) ||
                   normalizedValue.EndsWith(" " + normalizedName, StringComparison.Ordinal) ||
                   normalizedValue.Contains(" " + normalizedName + " ", StringComparison.Ordinal);
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string text = value.Trim().ToLowerInvariant();

            text = text
                .Replace('أ', 'ا')
                .Replace('إ', 'ا')
                .Replace('آ', 'ا')
                .Replace('ٱ', 'ا')
                .Replace('ة', 'ه')
                .Replace('ى', 'ي')
                .Replace("ـ", string.Empty, StringComparison.Ordinal);

            text = Regex.Replace(
                text,
                @"[\u064B-\u065F\u0670]",
                string.Empty,
                RegexOptions.CultureInvariant);

            var builder = new StringBuilder(text.Length);

            foreach (char ch in text)
            {
                builder.Append(
                    char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)
                        ? ch
                        : ' ');
            }

            return Regex.Replace(
                builder.ToString(),
                @"\s+",
                " ",
                RegexOptions.CultureInvariant).Trim();
        }
    }
}
