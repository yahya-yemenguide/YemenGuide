using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Data;

namespace YemenGuide.Web.Services.OwnerDashboard
{
    /// <summary>
    /// يبني مركز قيادة المالك من الجداول الموجودة فعليًا في قاعدة البيانات.
    /// لا ينشئ أي بيانات تجريبية ولا يكتب في قاعدة البيانات.
    /// </summary>
    public sealed class OwnerDashboardService : IOwnerDashboardService
    {
        private readonly AppDbContext _context;

        public OwnerDashboardService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<OwnerDashboardDto> GetDashboardAsync(
            int ownerId,
            int? requestedPlaceId = null,
            CancellationToken cancellationToken = default)
        {
            if (ownerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId));
            }

            var owner = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == ownerId)
                .Select(user => new
                {
                    user.UserId,
                    user.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (owner == null)
            {
                throw new InvalidOperationException("تعذر العثور على حساب المالك الحالي.");
            }

            var model = new OwnerDashboardDto
            {
                OwnerId = ownerId,
                OwnerName = string.IsNullOrWhiteSpace(owner.FullName)
                    ? "المالك"
                    : owner.FullName.Trim(),
                GeneratedAt = DateTime.Now
            };

            var settings = await _context.SystemSettings
                .AsNoTracking()
                .Select(item => new
                {
                    item.DefaultCurrency,
                    item.RequiredCommissionGuaranteeBalance,
                    item.DefaultLowBalanceThreshold
                })
                .FirstOrDefaultAsync(cancellationToken);

            model.Currency = string.IsNullOrWhiteSpace(settings?.DefaultCurrency)
                ? "ريال يمني"
                : settings!.DefaultCurrency!.Trim();

            var places = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .Select(place => new PlaceCandidate
                {
                    PlaceId = place.PlaceId,
                    PlaceName = place.PlaceNameAndDesc,
                    Status = place.Status,
                    IsAvailable = place.IsAvailable == true,
                    CategoryName = place.Category.CategoryName,
                    BookingSystemId = place.Category.BookingSystemId
                })
                .OrderByDescending(place =>
                    (place.Status == "Approved" || place.Status == "Active") &&
                    place.IsAvailable)
                .ThenBy(place => place.PlaceName)
                .ToListAsync(cancellationToken);

            var selectedPlace = places.FirstOrDefault(place =>
                                    requestedPlaceId.HasValue &&
                                    place.PlaceId == requestedPlaceId.Value)
                                ?? places.FirstOrDefault(IsOperationalPlace)
                                ?? places.FirstOrDefault();

            if (selectedPlace == null)
            {
                model.HasPlace = false;
                return model;
            }

            model.HasPlace = true;
            model.PlaceId = selectedPlace.PlaceId;
            model.PlaceName = string.IsNullOrWhiteSpace(selectedPlace.PlaceName)
                ? "منشأتك"
                : selectedPlace.PlaceName.Trim();
            model.CategoryName = selectedPlace.CategoryName?.Trim() ?? string.Empty;
            model.BookingSystemId = selectedPlace.BookingSystemId;
            model.PlaceStatus = selectedPlace.Status?.Trim() ?? string.Empty;
            model.PlaceStatusLabel = PlaceStatusLabel(selectedPlace.Status, selectedPlace.IsAvailable);
            model.IsPlaceAvailable = selectedPlace.IsAvailable;

            var placeId = selectedPlace.PlaceId;
            var now = DateTime.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var previousMonthStart = monthStart.AddMonths(-1);
            var sixMonthsStart = monthStart.AddMonths(-5);
            var trendStart = today.AddDays(-29);
            var calendarStart = today.AddDays(-7);
            var calendarEnd = today.AddDays(36);

            var bookingQuery = _context.Bookings
                .AsNoTracking()
                .Where(booking => booking.PlaceId == placeId);

            var newBookingsToday = await bookingQuery.CountAsync(
                booking => booking.CreatedAt.HasValue &&
                           booking.CreatedAt.Value >= today &&
                           booking.CreatedAt.Value < tomorrow,
                cancellationToken);

            var todayBookings = await bookingQuery.CountAsync(
                booking => booking.StartDate >= today &&
                           booking.StartDate < tomorrow &&
                           booking.BookingState != "Cancelled" &&
                           booking.BookingState != "Rejected" &&
                           booking.BookingState != "Expired",
                cancellationToken);

            var monthlyBookings = await bookingQuery
                .Where(booking => booking.CreatedAt.HasValue &&
                                  booking.CreatedAt.Value >= monthStart &&
                                  booking.CreatedAt.Value < monthStart.AddMonths(1))
                .Select(booking => new
                {
                    booking.BookingState,
                    booking.TotalPrice
                })
                .ToListAsync(cancellationToken);

            var totalThisMonth = monthlyBookings.Count;
            var pendingThisMonth = monthlyBookings.Count(item =>
                IsState(item.BookingState, "Pending") ||
                IsState(item.BookingState, "AwaitingPayment") ||
                IsState(item.BookingState, "PaymentReview") ||
                IsState(item.BookingState, "UnderReview"));
            var confirmedThisMonth = monthlyBookings.Count(item => IsState(item.BookingState, "Confirmed"));
            var completedThisMonth = monthlyBookings.Count(item => IsState(item.BookingState, "Completed"));
            var noShowThisMonth = monthlyBookings.Count(item => IsState(item.BookingState, "NoShow"));
            var cancelledThisMonth = monthlyBookings.Count(item =>
                IsState(item.BookingState, "Cancelled") ||
                IsState(item.BookingState, "Rejected") ||
                IsState(item.BookingState, "Expired"));

            model.BookingSummary = new OwnerDashboardBookingSummaryDto
            {
                TotalThisMonth = totalThisMonth,
                Pending = pendingThisMonth,
                Confirmed = confirmedThisMonth,
                Completed = completedThisMonth,
                Cancelled = cancelledThisMonth,
                NoShow = noShowThisMonth,
                CancellationRate = totalThisMonth <= 0
                    ? 0m
                    : Math.Round(cancelledThisMonth * 100m / totalThisMonth, 1)
            };

            var currentFinance = await (
                    from record in _context.FinancialRecords.AsNoTracking()
                    join booking in _context.Bookings.AsNoTracking()
                        on record.BookingId equals booking.BookingId
                    where booking.PlaceId == placeId &&
                          booking.CreatedAt.HasValue &&
                          booking.CreatedAt.Value >= monthStart &&
                          booking.CreatedAt.Value < monthStart.AddMonths(1)
                    select new
                    {
                        record.AmountPaidOnline,
                        record.AmountCollectedAtProperty,
                        record.PlatformCommission
                    })
                .ToListAsync(cancellationToken);

            var previousFinance = await (
                    from record in _context.FinancialRecords.AsNoTracking()
                    join booking in _context.Bookings.AsNoTracking()
                        on record.BookingId equals booking.BookingId
                    where booking.PlaceId == placeId &&
                          booking.CreatedAt.HasValue &&
                          booking.CreatedAt.Value >= previousMonthStart &&
                          booking.CreatedAt.Value < monthStart
                    select record.AmountPaidOnline + record.AmountCollectedAtProperty)
                .SumAsync(cancellationToken);

            var monthlyRevenue = currentFinance.Sum(item =>
                item.AmountPaidOnline + item.AmountCollectedAtProperty);

            var grossBookingValue = monthlyBookings
                .Where(item => !IsClosedWithoutRevenue(item.BookingState))
                .Sum(item => item.TotalPrice);

            var refundedAmount = await _context.RefundRequests
                .AsNoTracking()
                .Where(item => item.PlaceId == placeId &&
                               item.Status == "Completed" &&
                               item.ProcessedAt.HasValue &&
                               item.ProcessedAt.Value >= monthStart &&
                               item.ProcessedAt.Value < monthStart.AddMonths(1))
                .SumAsync(item => (decimal?)item.RefundAmount, cancellationToken) ?? 0m;

            var paidOnline = currentFinance.Sum(item => item.AmountPaidOnline);
            var collectedAtProperty = currentFinance.Sum(item => item.AmountCollectedAtProperty);
            var platformCommission = currentFinance.Sum(item => item.PlatformCommission);

            model.Finance = new OwnerDashboardFinanceDto
            {
                GrossBookingValue = grossBookingValue,
                PaidOnline = paidOnline,
                CollectedAtProperty = collectedAtProperty,
                RefundedAmount = refundedAmount,
                PlatformCommission = platformCommission,
                NetCollectedRevenue = Math.Max(
                    0m,
                    paidOnline + collectedAtProperty - refundedAmount - platformCommission)
            };

            model.Kpis.NewBookingsToday = newBookingsToday;
            model.Kpis.TodayBookings = todayBookings;
            model.Kpis.MonthlyRevenue = monthlyRevenue;
            model.Kpis.PreviousMonthRevenue = previousFinance;
            model.Kpis.RevenueChangePercent = PercentageChange(monthlyRevenue, previousFinance);

            var commissionAccount = await _context.PlaceCommissionAccounts
                .AsNoTracking()
                .Where(account => account.PlaceId == placeId)
                .Select(account => new
                {
                    account.AvailableBalance,
                    account.HeldBalance,
                    account.DebtAmount,
                    account.LowBalanceThreshold,
                    account.AccountStatus
                })
                .FirstOrDefaultAsync(cancellationToken);

            var requiredGuarantee = Math.Max(
                0m,
                settings?.RequiredCommissionGuaranteeBalance ?? 0m);

            var availableBalance = commissionAccount?.AvailableBalance ?? 0m;
            var lowBalanceThreshold = commissionAccount?.LowBalanceThreshold
                                      ?? settings?.DefaultLowBalanceThreshold
                                      ?? 0m;
            var accountStatus = string.IsNullOrWhiteSpace(commissionAccount?.AccountStatus)
                ? (availableBalance >= requiredGuarantee && requiredGuarantee > 0m
                    ? "Active"
                    : "NeedsTopUp")
                : commissionAccount!.AccountStatus!.Trim();

            var fundingPercent = requiredGuarantee <= 0m
                ? 100m
                : Math.Clamp(
                    Math.Round(availableBalance * 100m / requiredGuarantee, 1),
                    0m,
                    100m);

            model.Guarantee = new OwnerDashboardGuaranteeDto
            {
                AvailableBalance = availableBalance,
                HeldBalance = commissionAccount?.HeldBalance ?? 0m,
                DebtAmount = commissionAccount?.DebtAmount ?? 0m,
                RequiredBalance = requiredGuarantee,
                LowBalanceThreshold = Math.Max(0m, lowBalanceThreshold),
                FundingPercent = fundingPercent,
                AccountStatus = accountStatus,
                AccountStatusLabel = GuaranteeStatusLabel(accountStatus)
            };

            model.Kpis.AvailableGuaranteeBalance = availableBalance;
            model.Kpis.GuaranteeStatus = accountStatus;

            var reviewRows = await _context.Reviews
                .AsNoTracking()
                .Where(review => review.PlaceId == placeId &&
                                 review.ParentReviewId == null &&
                                 review.Stars.HasValue)
                .Select(review => new
                {
                    review.ReviewId,
                    review.Stars,
                    review.ReviewText,
                    review.CreatedAt,
                    review.OwnerReply,
                    CustomerName = review.User.FullName
                })
                .ToListAsync(cancellationToken);

            var reviewsCount = reviewRows.Count;
            var averageRating = reviewsCount == 0
                ? 0d
                : Math.Round(reviewRows.Average(item => item.Stars!.Value), 1);
            var awaitingReviewReply = reviewRows.Count(item => string.IsNullOrWhiteSpace(item.OwnerReply));

            var starCounts = new List<int> { 0, 0, 0, 0, 0 };
            foreach (var row in reviewRows)
            {
                if (row.Stars.HasValue && row.Stars.Value >= 1 && row.Stars.Value <= 5)
                {
                    starCounts[row.Stars.Value - 1]++;
                }
            }

            model.Reviews = new OwnerDashboardReviewsDto
            {
                AverageRating = averageRating,
                TotalReviews = reviewsCount,
                AwaitingReply = awaitingReviewReply,
                StarCounts = starCounts,
                LatestReviews = reviewRows
                    .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
                    .ThenByDescending(item => item.ReviewId)
                    .Take(3)
                    .Select(item => new OwnerDashboardReviewItemDto
                    {
                        ReviewId = item.ReviewId,
                        CustomerName = string.IsNullOrWhiteSpace(item.CustomerName)
                            ? "عميل"
                            : item.CustomerName.Trim(),
                        Stars = item.Stars ?? 0,
                        ReviewText = item.ReviewText?.Trim() ?? string.Empty,
                        CreatedAt = item.CreatedAt,
                        HasOwnerReply = !string.IsNullOrWhiteSpace(item.OwnerReply),
                        Url = $"/Owner/Chat/ChatInterface?communicationTab=reviews&reviewId={item.ReviewId}"
                    })
                    .ToList()
            };

            model.Kpis.AverageRating = averageRating;
            model.Kpis.ReviewsCount = reviewsCount;

            var favoritesCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(item => item.PlaceId == placeId, cancellationToken);
            model.Kpis.FavoritesCount = favoritesCount;

            var roomUnitCount = await _context.RoomUnits
                .AsNoTracking()
                .CountAsync(item => item.PlaceId == placeId && item.IsAvailable == true, cancellationToken);

            var occupiedRoomUnits = 0;
            if (roomUnitCount > 0)
            {
                occupiedRoomUnits = await (
                        from assignment in _context.BookingRoomUnits.AsNoTracking()
                        join booking in _context.Bookings.AsNoTracking()
                            on assignment.BookingId equals booking.BookingId
                        join roomUnit in _context.RoomUnits.AsNoTracking()
                            on assignment.RoomUnitId equals roomUnit.RoomUnitId
                        where roomUnit.PlaceId == placeId &&
                              roomUnit.IsAvailable == true &&
                              (assignment.AssignmentStatus == "Held" ||
                               assignment.AssignmentStatus == "Confirmed") &&
                              booking.BookingState == "Confirmed" &&
                              booking.StartDate < tomorrow &&
                              (!booking.EndDate.HasValue || booking.EndDate.Value >= today)
                        select roomUnit.RoomUnitId)
                    .Distinct()
                    .CountAsync(cancellationToken);
            }

            var occupancyPercent = roomUnitCount <= 0
                ? 0m
                : Math.Clamp(
                    Math.Round(occupiedRoomUnits * 100m / roomUnitCount, 1),
                    0m,
                    100m);

            model.Kpis.HasRoomUnits = roomUnitCount > 0;
            model.Kpis.OccupancyPercent = occupancyPercent;

            var activeNow = await bookingQuery.CountAsync(
                booking => booking.BookingState == "Confirmed" &&
                           booking.StartDate <= now &&
                           (!booking.EndDate.HasValue || booking.EndDate.Value >= now),
                cancellationToken);

            var arrivalsToday = await bookingQuery.CountAsync(
                booking => booking.BookingState == "Confirmed" &&
                           booking.StartDate >= today &&
                           booking.StartDate < tomorrow,
                cancellationToken);

            var departuresToday = await bookingQuery.CountAsync(
                booking => booking.BookingState == "Confirmed" &&
                           booking.EndDate.HasValue &&
                           booking.EndDate.Value >= today &&
                           booking.EndDate.Value < tomorrow,
                cancellationToken);

            var todayBookingRows = await bookingQuery
                .Where(booking =>
                    ((booking.StartDate >= today && booking.StartDate < tomorrow) ||
                     (booking.EndDate.HasValue && booking.EndDate.Value >= today && booking.EndDate.Value < tomorrow)) &&
                    booking.BookingState != "Cancelled" &&
                    booking.BookingState != "Rejected" &&
                    booking.BookingState != "Expired")
                .OrderBy(booking => booking.StartDate)
                .ThenBy(booking => booking.BookingId)
                .Take(8)
                .Select(booking => new
                {
                    booking.BookingId,
                    booking.StartDate,
                    booking.EndDate,
                    booking.BookingState,
                    booking.BookingSource,
                    booking.GuestName,
                    booking.TotalPrice,
                    CustomerName = booking.Customer != null ? booking.Customer.FullName : null,
                    RoomName = booking.Room != null ? booking.Room.RoomNumber : null,
                    PackageName = booking.Package != null ? booking.Package.PackageName : null,
                    booking.BookingType
                })
                .ToListAsync(cancellationToken);

            model.TodayOperation = new OwnerDashboardTodayOperationDto
            {
                ArrivalsToday = arrivalsToday,
                DeparturesToday = departuresToday,
                ActiveNow = activeNow,
                TotalRoomUnits = roomUnitCount,
                OccupiedRoomUnits = occupiedRoomUnits,
                AvailableRoomUnits = Math.Max(0, roomUnitCount - occupiedRoomUnits),
                HasRoomUnits = roomUnitCount > 0,
                Bookings = todayBookingRows.Select(item => new OwnerDashboardTodayBookingDto
                {
                    BookingId = item.BookingId,
                    CustomerName = FirstNonEmpty("عميل", item.CustomerName, item.GuestName),
                    ResourceName = FirstNonEmpty("الحجز", item.RoomName, item.PackageName, item.BookingType),
                    StartDate = item.StartDate,
                    EndDate = item.EndDate,
                    BookingState = item.BookingState?.Trim() ?? string.Empty,
                    BookingStateLabel = BookingStateLabel(item.BookingState),
                    BookingSource = item.BookingSource?.Trim() ?? string.Empty,
                    TotalPrice = item.TotalPrice,
                    Url = BookingUrl(item.BookingId, item.BookingState)
                }).ToList()
            };

            var pendingBookings = await bookingQuery.CountAsync(
                booking => booking.BookingState == "Pending",
                cancellationToken);

            var paymentReviews = await bookingQuery.CountAsync(
                booking => booking.BookingState == "PaymentReview" ||
                           booking.PaymentStatus == "Pending",
                cancellationToken);

            var unreadMessages = await _context.Chats
                .AsNoTracking()
                .CountAsync(message =>
                    message.ReceiverId == ownerId &&
                    message.PlaceId == placeId &&
                    !message.IsRead &&
                    !message.IsReportMessage &&
                    !message.IsDeletedForReceiver,
                    cancellationToken);

            var pendingRefunds = await _context.RefundRequests
                .AsNoTracking()
                .CountAsync(item =>
                    item.PlaceId == placeId &&
                    (item.Status == "Pending" || item.Status == "Processing"),
                    cancellationToken);

            BuildAttentionItems(
                model,
                placeId,
                pendingBookings,
                paymentReviews,
                awaitingReviewReply,
                unreadMessages,
                pendingRefunds,
                accountStatus,
                availableBalance,
                requiredGuarantee);

            var trendRows = await bookingQuery
                .Where(booking => booking.CreatedAt.HasValue &&
                                  booking.CreatedAt.Value >= trendStart &&
                                  booking.CreatedAt.Value < tomorrow)
                .GroupBy(booking => booking.CreatedAt!.Value.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Total = group.Count(),
                    Completed = group.Count(item => item.BookingState == "Completed"),
                    Cancelled = group.Count(item =>
                        item.BookingState == "Cancelled" ||
                        item.BookingState == "Rejected" ||
                        item.BookingState == "Expired")
                })
                .ToListAsync(cancellationToken);

            var trendLookup = trendRows.ToDictionary(item => item.Date.Date);
            for (var day = trendStart; day <= today; day = day.AddDays(1))
            {
                model.BookingTrend.Labels.Add(day.ToString("dd/MM"));
                if (trendLookup.TryGetValue(day.Date, out var row))
                {
                    model.BookingTrend.Bookings.Add(row.Total);
                    model.BookingTrend.Completed.Add(row.Completed);
                    model.BookingTrend.Cancelled.Add(row.Cancelled);
                }
                else
                {
                    model.BookingTrend.Bookings.Add(0);
                    model.BookingTrend.Completed.Add(0);
                    model.BookingTrend.Cancelled.Add(0);
                }
            }

            var financeTrendRows = await (
                    from record in _context.FinancialRecords.AsNoTracking()
                    join booking in _context.Bookings.AsNoTracking()
                        on record.BookingId equals booking.BookingId
                    where booking.PlaceId == placeId &&
                          booking.CreatedAt.HasValue &&
                          booking.CreatedAt.Value >= sixMonthsStart
                    group record by new
                    {
                        Year = booking.CreatedAt!.Value.Year,
                        Month = booking.CreatedAt!.Value.Month
                    }
                    into groupByMonth
                    select new
                    {
                        groupByMonth.Key.Year,
                        groupByMonth.Key.Month,
                        Collected = groupByMonth.Sum(item =>
                            item.AmountPaidOnline + item.AmountCollectedAtProperty),
                        Commission = groupByMonth.Sum(item => item.PlatformCommission)
                    })
                .ToListAsync(cancellationToken);

            for (var index = 0; index < 6; index++)
            {
                var month = sixMonthsStart.AddMonths(index);
                var row = financeTrendRows.FirstOrDefault(item =>
                    item.Year == month.Year && item.Month == month.Month);

                model.Finance.MonthlyLabels.Add(MonthLabel(month));
                model.Finance.MonthlyCollected.Add(row?.Collected ?? 0m);
                model.Finance.MonthlyCommission.Add(row?.Commission ?? 0m);
            }

            var activePromotions = await _context.Promotions
                .AsNoTracking()
                .CountAsync(item => item.PlaceId == placeId &&
                                    item.IsActive == true &&
                                    item.Status == "Active" &&
                                    item.StartDate <= today &&
                                    item.EndDate >= today,
                    cancellationToken);

            var activeAdvertisements = await _context.Advertisements
                .AsNoTracking()
                .CountAsync(item => item.PlaceId == placeId && item.Status == "Active",
                    cancellationToken);

            var marketingEvents = await (
                    from eventItem in _context.AdvertisementEvents.AsNoTracking()
                    join advertisement in _context.Advertisements.AsNoTracking()
                        on eventItem.AdvertisementId equals advertisement.AdvertisementId
                    where advertisement.PlaceId == placeId &&
                          eventItem.EventAt >= monthStart &&
                          eventItem.EventAt < monthStart.AddMonths(1)
                    group eventItem by eventItem.EventType
                    into grouped
                    select new
                    {
                        EventType = grouped.Key,
                        Count = grouped.LongCount(),
                        Cost = grouped.Sum(item => item.CostAmount)
                    })
                .ToListAsync(cancellationToken);

            long EventCount(string type) => marketingEvents
                .Where(item => IsState(item.EventType, type))
                .Sum(item => item.Count);

            var impressions = EventCount("Impression");
            var clicks = EventCount("Click");
            var bookingStarts = EventCount("BookingStarted");
            var conversions = EventCount("BookingCompleted");
            var marketingSpent = marketingEvents.Sum(item => item.Cost);

            model.Marketing = new OwnerDashboardMarketingDto
            {
                ActivePromotions = activePromotions,
                ActiveAdvertisements = activeAdvertisements,
                Impressions = impressions,
                Clicks = clicks,
                BookingStarts = bookingStarts,
                Conversions = conversions,
                SpentAmount = marketingSpent,
                ConversionRate = impressions <= 0
                    ? 0m
                    : Math.Round(conversions * 100m / impressions, 2),
                Favorites = favoritesCount
            };

            var calendarRows = await bookingQuery
                .Where(booking =>
                    booking.StartDate < calendarEnd &&
                    (!booking.EndDate.HasValue || booking.EndDate.Value >= calendarStart) &&
                    booking.BookingState != "Cancelled" &&
                    booking.BookingState != "Rejected" &&
                    booking.BookingState != "Expired")
                .OrderBy(booking => booking.StartDate)
                .Select(booking => new
                {
                    booking.BookingId,
                    booking.StartDate,
                    booking.EndDate,
                    booking.BookingState,
                    booking.BookingSource,
                    booking.GuestName,
                    CustomerName = booking.Customer != null ? booking.Customer.FullName : null
                })
                .ToListAsync(cancellationToken);

            model.CalendarEvents = calendarRows
                .Select(item => new OwnerDashboardCalendarEventDto
                {
                    BookingId = item.BookingId,
                    Title = $"#{item.BookingId} · {FirstNonEmpty("عميل", item.CustomerName, item.GuestName)}",
                    Start = item.StartDate,
                    End = item.EndDate,
                    State = item.BookingState?.Trim() ?? string.Empty,
                    Source = item.BookingSource?.Trim() ?? string.Empty,
                    Url = BookingUrl(item.BookingId, item.BookingState)
                })
                .ToList();

            model.RecentActivities = await BuildRecentActivitiesAsync(
                ownerId,
                placeId,
                cancellationToken);

            return model;
        }

        private async Task<List<OwnerDashboardActivityDto>> BuildRecentActivitiesAsync(
            int ownerId,
            int placeId,
            CancellationToken cancellationToken)
        {
            var activities = new List<OwnerDashboardActivityDto>();

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Where(item => item.PlaceId == placeId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.BookingId)
                .Take(5)
                .Select(item => new
                {
                    item.BookingId,
                    item.BookingState,
                    item.TotalPrice,
                    item.CreatedAt,
                    item.StartDate,
                    item.GuestName,
                    CustomerName = item.Customer != null ? item.Customer.FullName : null
                })
                .ToListAsync(cancellationToken);

            foreach (var item in bookings)
            {
                activities.Add(new OwnerDashboardActivityDto
                {
                    Type = "Booking",
                    Title = $"حجز #{item.BookingId} · {BookingStateLabel(item.BookingState)}",
                    Description = $"{FirstNonEmpty("عميل", item.CustomerName, item.GuestName)} · {item.TotalPrice:N0}",
                    Icon = "bi-calendar2-check-fill",
                    Tone = IsClosedWithoutRevenue(item.BookingState) ? "danger" : "primary",
                    OccurredAt = item.CreatedAt ?? item.StartDate,
                    Url = BookingUrl(item.BookingId, item.BookingState)
                });
            }

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(item => item.PlaceId == placeId &&
                               item.ParentReviewId == null &&
                               item.Stars.HasValue)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.ReviewId)
                .Take(4)
                .Select(item => new
                {
                    item.ReviewId,
                    item.Stars,
                    item.CreatedAt,
                    CustomerName = item.User.FullName
                })
                .ToListAsync(cancellationToken);

            foreach (var item in reviews)
            {
                activities.Add(new OwnerDashboardActivityDto
                {
                    Type = "Review",
                    Title = "تقييم جديد للمنشأة",
                    Description = $"{FirstNonEmpty("عميل", item.CustomerName)} · {item.Stars ?? 0}/5",
                    Icon = "bi-star-fill",
                    Tone = "warning",
                    OccurredAt = item.CreatedAt ?? DateTime.MinValue,
                    Url = $"/Owner/Chat/ChatInterface?communicationTab=reviews&reviewId={item.ReviewId}"
                });
            }

            var messages = await _context.Chats
                .AsNoTracking()
                .Where(item => item.ReceiverId == ownerId &&
                               item.PlaceId == placeId &&
                               !item.IsReportMessage &&
                               !item.IsDeletedForReceiver)
                .OrderByDescending(item => item.SendTime)
                .ThenByDescending(item => item.MessageId)
                .Take(4)
                .Select(item => new
                {
                    item.MessageId,
                    item.SendTime,
                    item.IsRead,
                    SenderName = item.Sender.FullName
                })
                .ToListAsync(cancellationToken);

            foreach (var item in messages)
            {
                activities.Add(new OwnerDashboardActivityDto
                {
                    Type = "Message",
                    Title = item.IsRead ? "رسالة عميل" : "رسالة جديدة غير مقروءة",
                    Description = FirstNonEmpty("عميل", item.SenderName),
                    Icon = "bi-chat-dots-fill",
                    Tone = item.IsRead ? "info" : "primary",
                    OccurredAt = item.SendTime ?? DateTime.MinValue,
                    Url = "/Owner/Chat/ChatInterface?communicationTab=messages"
                });
            }

            var transactions = await _context.CommissionTransactions
                .AsNoTracking()
                .Where(item => item.PlaceId == placeId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.CommissionTransactionId)
                .Take(4)
                .Select(item => new
                {
                    item.TransactionType,
                    item.Amount,
                    item.Status,
                    item.CreatedAt
                })
                .ToListAsync(cancellationToken);

            foreach (var item in transactions)
            {
                activities.Add(new OwnerDashboardActivityDto
                {
                    Type = "Finance",
                    Title = CommissionTransactionLabel(item.TransactionType),
                    Description = $"{item.Amount:N0} · {TransactionStatusLabel(item.Status)}",
                    Icon = "bi-wallet2",
                    Tone = IsState(item.Status, "Rejected") || IsState(item.Status, "Failed")
                        ? "danger"
                        : "success",
                    OccurredAt = item.CreatedAt ?? DateTime.MinValue,
                    Url = $"/Owner/Financials?financeTab=transactions&placeId={placeId}"
                });
            }

            return activities
                .Where(item => item.OccurredAt > DateTime.MinValue)
                .OrderByDescending(item => item.OccurredAt)
                .Take(9)
                .ToList();
        }

        private static void BuildAttentionItems(
            OwnerDashboardDto model,
            int placeId,
            int pendingBookings,
            int paymentReviews,
            int awaitingReviewReply,
            int unreadMessages,
            int pendingRefunds,
            string accountStatus,
            decimal availableBalance,
            decimal requiredGuarantee)
        {
            if (pendingBookings > 0)
            {
                model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
                {
                    Key = "pending-bookings",
                    Title = "حجوزات تنتظر قرارك",
                    Description = "راجع الطلبات الجديدة قبل انتهاء مهلة العميل.",
                    Icon = "bi-calendar2-event-fill",
                    Tone = "warning",
                    Count = pendingBookings,
                    Url = "/Owner/Reservation/ManageReservations?reservationTab=pending-requests-tab",
                    ActionLabel = "مراجعة"
                });
            }

            if (paymentReviews > 0)
            {
                model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
                {
                    Key = "payment-review",
                    Title = "إثباتات دفع قيد المراجعة",
                    Description = "توجد دفعات تحتاج التحقق قبل تأكيد الحجز.",
                    Icon = "bi-receipt-cutoff",
                    Tone = "primary",
                    Count = paymentReviews,
                    Url = "/Owner/Reservation/ManageReservations?reservationTab=pending-requests-tab",
                    ActionLabel = "فتح"
                });
            }

            if (awaitingReviewReply > 0)
            {
                model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
                {
                    Key = "reviews",
                    Title = "تقييمات تنتظر ردك",
                    Description = "الرد السريع يساعد على رفع ثقة العملاء بالمنشأة.",
                    Icon = "bi-star-fill",
                    Tone = "warning",
                    Count = awaitingReviewReply,
                    Url = "/Owner/Chat/ChatInterface?communicationTab=reviews",
                    ActionLabel = "رد الآن"
                });
            }

            if (unreadMessages > 0)
            {
                model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
                {
                    Key = "messages",
                    Title = "رسائل غير مقروءة",
                    Description = "لديك محادثات جديدة من العملاء.",
                    Icon = "bi-chat-dots-fill",
                    Tone = "info",
                    Count = unreadMessages,
                    Url = "/Owner/Chat/ChatInterface?communicationTab=messages",
                    ActionLabel = "فتح"
                });
            }

            if (pendingRefunds > 0)
            {
                model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
                {
                    Key = "refunds",
                    Title = "طلبات استرداد مفتوحة",
                    Description = "تابع الطلبات المالية التي ما زالت قيد المعالجة.",
                    Icon = "bi-arrow-counterclockwise",
                    Tone = "danger",
                    Count = pendingRefunds,
                    Url = $"/Owner/Financials?financeTab=refunds&placeId={placeId}",
                    ActionLabel = "متابعة"
                });
            }

            var financialRisk = IsState(accountStatus, "LowBalance") ||
                                IsState(accountStatus, "InDebt") ||
                                IsState(accountStatus, "Suspended") ||
                                (IsState(accountStatus, "NeedsTopUp") && requiredGuarantee > availableBalance);

            if (financialRisk)
            {
                model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
                {
                    Key = "commission-balance",
                    Title = GuaranteeStatusLabel(accountStatus),
                    Description = requiredGuarantee > 0m
                        ? $"المتاح {availableBalance:N0} من رصيد مطلوب {requiredGuarantee:N0}."
                        : "راجع مركز رصيد ضمان العمولات لمعرفة حالة الحساب.",
                    Icon = "bi-shield-exclamation",
                    Tone = IsState(accountStatus, "Suspended") || IsState(accountStatus, "InDebt")
                        ? "danger"
                        : "warning",
                    Count = 1,
                    Url = $"/Owner/Financials?financeTab=balance&placeId={placeId}",
                    ActionLabel = "فتح الرصيد"
                });
            }
        }

        private static bool IsOperationalPlace(PlaceCandidate place) =>
            place.IsAvailable &&
            (IsState(place.Status, "Approved") || IsState(place.Status, "Active"));

        private static bool IsState(string? value, string expected) =>
            string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        private static bool IsClosedWithoutRevenue(string? state) =>
            IsState(state, "Cancelled") ||
            IsState(state, "Rejected") ||
            IsState(state, "Expired");

        private static decimal PercentageChange(decimal current, decimal previous)
        {
            if (previous == 0m)
            {
                return current > 0m ? 100m : 0m;
            }

            return Math.Round((current - previous) * 100m / previous, 1);
        }

        private static string MonthLabel(DateTime value)
        {
            try
            {
                return value.ToString("MMM", CultureInfo.GetCultureInfo("ar"));
            }
            catch (CultureNotFoundException)
            {
                return value.ToString("MM/yyyy");
            }
        }

        private static string PlaceStatusLabel(string? status, bool isAvailable)
        {
            if (!isAvailable)
            {
                return "غير متاحة للحجز";
            }

            return status?.Trim() switch
            {
                "Active" => "نشطة",
                "Approved" => "معتمدة",
                "Pending" => "قيد المراجعة",
                "Rejected" => "مرفوضة",
                "Suspended" => "موقوفة",
                _ => string.IsNullOrWhiteSpace(status) ? "غير محددة" : status.Trim()
            };
        }

        private static string BookingStateLabel(string? state) => state?.Trim() switch
        {
            "Pending" => "بانتظار القرار",
            "AwaitingPayment" => "بانتظار الدفع",
            "PaymentReview" => "الدفع قيد المراجعة",
            "UnderReview" => "قيد المراجعة",
            "Confirmed" => "مؤكد",
            "Completed" => "مكتمل",
            "Cancelled" => "ملغي",
            "Rejected" => "مرفوض",
            "Expired" => "منتهي المهلة",
            "NoShow" => "عدم حضور",
            _ => string.IsNullOrWhiteSpace(state) ? "غير محدد" : state.Trim()
        };

        private static string GuaranteeStatusLabel(string? status) => status?.Trim() switch
        {
            "Active" => "الرصيد آمن",
            "LowBalance" => "الرصيد منخفض",
            "InDebt" => "يوجد دين عمولة",
            "Suspended" => "استقبال الحجوزات موقوف ماليًا",
            "NeedsTopUp" => "الرصيد يحتاج شحن",
            _ => "حالة الرصيد"
        };

        private static string CommissionTransactionLabel(string? type) => type?.Trim() switch
        {
            "TopUp" => "شحن رصيد الضمان",
            "CommissionHold" => "حجز عمولة",
            "CommissionCharge" => "خصم عمولة",
            "CommissionRelease" => "تحرير عمولة",
            "Debt" => "تسجيل دين عمولة",
            "DebtPayment" => "سداد دين عمولة",
            "Adjustment" => "تسوية مالية",
            _ => "حركة مالية"
        };

        private static string TransactionStatusLabel(string? status) => status?.Trim() switch
        {
            "Pending" => "قيد المراجعة",
            "Completed" => "مكتملة",
            "Rejected" => "مرفوضة",
            "Reversed" => "معكوسة",
            "Failed" => "فشلت",
            "Cancelled" => "ملغاة",
            _ => string.IsNullOrWhiteSpace(status) ? "غير محددة" : status.Trim()
        };

        private static string BookingUrl(int bookingId, string? bookingState)
        {
            var tab = bookingState?.Trim() switch
            {
                "Confirmed" => "active-reservations-tab",
                "Completed" => "reservation-history-tab",
                "Cancelled" => "reservation-history-tab",
                "Rejected" => "reservation-history-tab",
                "NoShow" => "reservation-history-tab",
                "Expired" => "reservation-history-tab",
                _ => "pending-requests-tab"
            };

            return $"/Owner/Reservation/ManageReservations?reservationTab={tab}&bookingId={bookingId}";
        }

        private static string FirstNonEmpty(string fallback, params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return fallback;
        }

        private sealed class PlaceCandidate
        {
            public int PlaceId { get; set; }
            public string? PlaceName { get; set; }
            public string? Status { get; set; }
            public bool IsAvailable { get; set; }
            public string? CategoryName { get; set; }
            public int BookingSystemId { get; set; }
        }
    }
}
