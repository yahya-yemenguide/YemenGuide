using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class SystemSetting
    {
        public int Id { get; set; }
        public string SiteName { get; set; } = null!;
        public string? SiteNameEn { get; set; }
        public string? SiteDescription { get; set; }
        public string? SiteDescriptionEn { get; set; }
        public string? LogoPath { get; set; }
        public string? FaviconPath { get; set; }
        public int MaxImageSize { get; set; }
        public int MaxImageCount { get; set; }
        public string DefaultWalletLogoPath { get; set; } = null!;
        public bool AutoApprovePlaces { get; set; }
        public bool? AllowNewRegistrations { get; set; }
        public string? ContactEmail { get; set; }
        public string? SupportPhone { get; set; }
        public string? FacebookLink { get; set; }
        public string? TwitterLink { get; set; }
        public string? InstagramLink { get; set; }
        public string? WhatsAppNumber { get; set; }
        public bool MaintenanceMode { get; set; }
        public string? MaintenanceMessage { get; set; }
        public string? MaintenanceMessageEn { get; set; }
        public string MaintenanceTitle { get; set; } = null!;
        public string? MaintenanceTitleEn { get; set; }
        public string MaintenanceType { get; set; } = null!;
        public bool? MaintenanceAffectOwners { get; set; }
        public DateTime? MaintenanceStartAt { get; set; }
        public DateTime? MaintenanceEndAt { get; set; }
        public bool? MaintenanceShowContact { get; set; }
        public bool MaintenanceBlockBookings { get; set; }
        public bool MaintenanceBlockPayments { get; set; }
        public bool MaintenanceBlockOwnerChanges { get; set; }
        public bool MaintenanceBlockMessages { get; set; }
        public bool MaintenanceBlockReviews { get; set; }
        public bool MaintenanceBlockRegistrations { get; set; }
        public decimal? CommissionPercentage { get; set; }
        public string? DefaultCurrency { get; set; }
        public bool? AutoUpdateExchangeRates { get; set; }
        public DateTime? ExchangeRateLastAttemptAt { get; set; }
        public DateTime? ExchangeRateLastUpdatedAt { get; set; }
        public string? ExchangeRateLastSource { get; set; }
        public string ExchangeRateLastStatus { get; set; } = null!;
        public string? ExchangeRateLastError { get; set; }
        public string? HeroTitleMain { get; set; }
        public string? HeroTitleMainEn { get; set; }
        public string? HeroTitleSub { get; set; }
        public string? HeroTitleSubEn { get; set; }
        public string? SeasonTheme { get; set; }
        public bool? IsBannerActive { get; set; }
        public string? BannerText { get; set; }
        public string? BannerTextEn { get; set; }
        public decimal SanaaUsdRate { get; set; }
        public decimal AdenUsdRate { get; set; }
        public decimal SanaaSarRate { get; set; }
        public decimal AdenSarRate { get; set; }
        public decimal MinimumCommissionTopUp { get; set; }
        public decimal DefaultLowBalanceThreshold { get; set; }
        public bool AllowNegativeCommissionBalance { get; set; }
        public decimal DefaultCommissionDebtLimit { get; set; }
        public int CommissionDebtGraceDays { get; set; }
        public bool? SuspendBookingWhenBalanceInsufficient { get; set; }
        public bool HidePlaceWhenCommissionSuspended { get; set; }
        public bool ReturnCommissionOnCancellation { get; set; } = true;
        public bool OwnerFreeTrialEnabled { get; set; }
        public int OwnerFreeTrialDays { get; set; }
        public decimal RequiredCommissionGuaranteeBalance { get; set; }
        public int? PlatformOwnerUserId { get; set; }

        public virtual User? PlatformOwnerUser { get; set; }
    }
}

