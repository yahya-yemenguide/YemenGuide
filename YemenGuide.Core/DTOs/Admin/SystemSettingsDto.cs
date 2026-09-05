using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YemenGuide.Core.DTOs.Admin
{
    public class SystemSettingsDto
    {
        public int Id { get; set; }
        public string SiteName { get; set; } = "";
        public string SiteDescription { get; set; } = "";
        public string LogoPath { get; set; } = "";
        public string FaviconPath { get; set; } = "";

        // ==========================================
        // ✨ إعدادات المواسم والواجهة الترحيبية (VIP)
        // ==========================================
        public string HeroTitleMain { get; set; } = "دليلك اليمني بين يديك..";
        public string HeroTitleSub { get; set; } = "اكتشف";
        public string SeasonTheme { get; set; } = "Normal";
        public bool IsBannerActive { get; set; } = true;
        public string BannerText { get; set; } = "إدارة منصة دليلك اليمني تهنئكم بحلول المناسبة السعيدة، كل عام وأنتم بخير! ✨";

        // ==========================================
        // ⚙️ إعدادات النظام والسياسات
        // ==========================================
        public bool AutoApprovePlaces { get; set; }
        public bool AllowRegistration { get; set; }
        public int MaxImageSize { get; set; }
        public int MaxImageCount { get; set; }

        // ==========================================
        // 📞 الروابط والتواصل
        // ==========================================
        public string OfficialEmail { get; set; } = "";
        public string SupportPhone { get; set; } = "";
        public string FacebookLink { get; set; } = "";
        public string TwitterLink { get; set; } = "";
        public string InstagramLink { get; set; } = "";
        public string WhatsAppLink { get; set; } = "";

        // ==========================================
        // 🛠️ الصيانة والطوارئ
        // ==========================================
        public bool MaintenanceMode { get; set; }
        public string MaintenanceMessage { get; set; } = "";

        // ==========================================
        // 💰 الإعدادات المالية وأسعار الصرف
        // ==========================================
        public decimal CommissionPercentage { get; set; }
        public string DefaultCurrency { get; set; } = "ريال يمني";
        public bool AutoUpdateExchangeRates { get; set; }

        public decimal SanaaUsdRate { get; set; }
        public decimal AdenUsdRate { get; set; }
        public decimal SanaaSarRate { get; set; }
        public decimal AdenSarRate { get; set; }
        public string MaintenanceTitle { get; set; } = "";
        public string MaintenanceType { get; set; } = "Full";
        public bool MaintenanceAffectOwners { get; set; }
        public DateTime? MaintenanceStartAt { get; set; }
        public DateTime? MaintenanceEndAt { get; set; }
        public bool MaintenanceShowContact { get; set; }

        public bool MaintenanceBlockBookings { get; set; }
        public bool MaintenanceBlockPayments { get; set; }
        public bool MaintenanceBlockOwnerChanges { get; set; }
        public bool MaintenanceBlockMessages { get; set; }
        public bool MaintenanceBlockReviews { get; set; }
        public bool MaintenanceBlockRegistrations { get; set; }

        public string DefaultWalletLogoPath { get; set; } = "";

        // ==========================================
        // 🛰️ حالة آخر تحديث لأسعار الصرف
        // ==========================================
        public DateTime? ExchangeRateLastAttemptAt { get; set; }
        public DateTime? ExchangeRateLastUpdatedAt { get; set; }
        public string ExchangeRateLastSource { get; set; } = "";
        public string ExchangeRateLastStatus { get; set; } = "Never";
        public string ExchangeRateLastError { get; set; } = "";
    }
}
