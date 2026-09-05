using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Admin
{
    public class DashboardDto
    {
        public string AdminName { get; set; } = "مدير النظام";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public int TotalCategories { get; set; }
        public int TotalPlaces { get; set; }
        public int ActivePlaces { get; set; }
        public int PendingPlaces { get; set; }
        public int TotalUsers { get; set; }
        public int TotalBookings { get; set; }
        public int TodayBookingsCreated { get; set; }
        public int TodayBookingsStarting { get; set; }
        public int ThisMonthBookings { get; set; }

        // القيم المالية في هذا DTO تكون جاهزة بعملة العرض المختارة للمستخدم.
        public decimal TotalRevenue { get; set; }
        public decimal PendingRevenue { get; set; }
        public decimal TodayRevenue { get; set; }

        public string DisplayCurrency { get; set; } = "ريال يمني";
        public string CurrencyCode { get; set; } = "YER";
        public string CurrencySymbol { get; set; } = "ر.ي";

        public string TotalRevenueText { get; set; } = "0 ر.ي";
        public string PendingRevenueText { get; set; } = "0 ر.ي";
        public string TodayRevenueText { get; set; } = "0 ر.ي";

        public List<CategoryDistributionDto> CategoryDistribution { get; set; } = new();
        public List<StatusDistributionDto> PlaceStatusDistribution { get; set; } = new();
        public List<FinanceDistributionDto> FinanceDistribution { get; set; } = new();
        public List<DashboardTrendPointDto> BookingTrend { get; set; } = new();
        public List<DashboardTrendPointDto> RevenueTrend { get; set; } = new();
        public List<RecentPlaceDto> RecentPlaces { get; set; } = new();
        public List<DashboardAttentionItemDto> AttentionItems { get; set; } = new();
    }

    public class CategoryDistributionDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int PlaceCount { get; set; }
    }

    public class StatusDistributionDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class FinanceDistributionDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public class DashboardTrendPointDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public class DashboardAttentionItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi bi-info-circle";
        public string Tone { get; set; } = "info";
        public string ValueText { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class RecentPlaceDto
    {
        public int Id { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
