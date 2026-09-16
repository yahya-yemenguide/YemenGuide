using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Data;
using YemenGuide.Web.Services;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Services.Payments;

namespace YemenGuide.Web.Services.OwnerDashboard
{
    /// <summary>
    /// لوحة المالك الحية.
    /// لا توجد هنا بيانات تجريبية أو قيم أعمال ثابتة؛ كل المؤشرات تُقرأ من قاعدة البيانات
    /// وتُنسب إلى تاريخ الحدث الحقيقي قدر الإمكان.
    /// </summary>
    public sealed class OwnerDashboardService : IOwnerDashboardService
    {
        private readonly AppDbContext _context;
        private readonly CurrencyDisplayService _currencyDisplayService;
        private readonly CurrencyService _currencyService;

        public OwnerDashboardService(
            AppDbContext context,
            CurrencyDisplayService currencyDisplayService,
            CurrencyService currencyService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currencyDisplayService = currencyDisplayService ??
                throw new ArgumentNullException(nameof(currencyDisplayService));
            _currencyService = currencyService ??
                throw new ArgumentNullException(nameof(currencyService));
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

            var displayCurrency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(cancellationToken);

            var model = new OwnerDashboardDto
            {
                OwnerId = ownerId,
                OwnerName = string.IsNullOrWhiteSpace(owner.FullName)
                    ? "المالك"
                    : owner.FullName.Trim(),
                Currency = displayCurrency,
                GeneratedAt = DateTime.Now
            };

            var settings = await _context.SystemSettings
                .AsNoTracking()
                .OrderByDescending(item => item.Id)
                .Select(item => new
                {
                    item.RequiredCommissionGuaranteeBalance,
                    item.DefaultLowBalanceThreshold
                })
                .FirstOrDefaultAsync(cancellationToken);

            var places = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .Select(place => new PlaceCandidate
                {
                    PlaceId = place.PlaceId,
                    PlaceName = place.PlaceNameAndDesc,
                    Status = place.Status,
                    IsAvailable = place.IsAvailable == true,
                    CategoryName = place.Category != null
                        ? place.Category.CategoryName
                        : null,
                    BookingSystemId = place.Category != null
                        ? place.Category.BookingSystemId
                        : 0,
                    ExchangeRateRegion = place.City != null
                        ? place.City.ExchangeRateRegion
                        : null,
                    FreeTrialStartedAt = place.FreeTrialStartedAt,
                    FreeTrialEndsAt = place.FreeTrialEndsAt
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

            var exchangeRateRegion = string.IsNullOrWhiteSpace(selectedPlace.ExchangeRateRegion)
                ? ExchangeRateRegionResolver.SanaaMarket
                : selectedPlace.ExchangeRateRegion!.Trim();

            decimal DisplayAmount(decimal amountInYer) =>
                _currencyService.ConvertAmount(
                    amountInYer,
                    displayCurrency,
                    exchangeRateRegion);

            string DisplayMoney(decimal amountInYer) =>
                $"{CurrencyDisplayService.FormatNumber(DisplayAmount(amountInYer), displayCurrency)} " +
                CurrencyDisplayService.GetCurrencySymbol(displayCurrency);

            var placeId = selectedPlace.PlaceId;
            var isBookablePlace = selectedPlace.BookingSystemId > 0;
            var now = DateTime.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);
            var previousMonthStart = monthStart.AddMonths(-1);
            var sixMonthsStart = monthStart.AddMonths(-5);
            var trendStart = today.AddDays(-29);
            var calendarStart = today.AddDays(-7);
            var calendarEnd = today.AddDays(36);

            var bookingQuery = _context.Bookings
                .AsNoTracking()
                .Where(booking => booking.PlaceId == placeId);

            // =========================================================
            // الحجوزات
            // =========================================================
            model.Kpis.NewBookingsToday = await bookingQuery.CountAsync(
                booking => booking.CreatedAt.HasValue &&
                           booking.CreatedAt.Value >= today &&
                           booking.CreatedAt.Value < tomorrow,
                cancellationToken);

            model.Kpis.TodayBookings = await bookingQuery.CountAsync(
                booking => booking.StartDate >= today &&
                           booking.StartDate < tomorrow &&
                           booking.BookingState != "Cancelled" &&
                           booking.BookingState != "Rejected" &&
                           booking.BookingState != "Expired",
                cancellationToken);

            var monthlyBookings = await bookingQuery
                .Where(booking => booking.CreatedAt.HasValue &&
                                  booking.CreatedAt.Value >= monthStart &&
                                  booking.CreatedAt.Value < nextMonthStart)
                .Select(booking => new
                {
                    booking.BookingState,
                    booking.TotalPrice
                })
                .ToListAsync(cancellationToken);

            var monthlyClosed = monthlyBookings.Count(item =>
                IsState(item.BookingState, "Cancelled") ||
                IsState(item.BookingState, "Rejected") ||
                IsState(item.BookingState, "Expired"));

            model.BookingSummary = new OwnerDashboardBookingSummaryDto
            {
                TotalThisMonth = monthlyBookings.Count,
                Pending = monthlyBookings.Count(item =>
                    IsState(item.BookingState, "Pending") ||
                    IsState(item.BookingState, "AwaitingPayment") ||
                    IsState(item.BookingState, "PaymentReview") ||
                    IsState(item.BookingState, "UnderReview")),
                Confirmed = monthlyBookings.Count(item => IsState(item.BookingState, "Confirmed")),
                Completed = monthlyBookings.Count(item => IsState(item.BookingState, "Completed")),
                Cancelled = monthlyClosed,
                NoShow = monthlyBookings.Count(item => IsState(item.BookingState, "NoShow")),
                CancellationRate = monthlyBookings.Count == 0
                    ? 0m
                    : Math.Round(monthlyClosed * 100m / monthlyBookings.Count, 1)
            };

            // =========================================================
            // المالية
            // - الدفعة الأولى: Booking.PaymentApprovedAt
            // - الدفعات الإضافية: PaymentTransactionsJson + ReviewedAt
            // - التحصيل في المنشأة: FinancialRecord.CollectedAt
            // - الاسترداد: RefundRequest.ProcessedAt
            // - العمولة: CommissionTransactions (Charge - Release)
            // =========================================================
            var refundRows = await _context.RefundRequests
                .AsNoTracking()
                .Where(refund =>
                    refund.PlaceId == placeId &&
                    refund.Status == "Completed" &&
                    refund.ProcessedAt.HasValue &&
                    refund.ProcessedAt.Value >= sixMonthsStart &&
                    refund.ProcessedAt.Value < nextMonthStart)
                .Select(refund => new RefundEventRow
                {
                    BookingId = refund.BookingId,
                    DepositPaid = refund.DepositPaid,
                    ProcessedAt = refund.ProcessedAt,
                    RefundAmount = refund.RefundAmount
                })
                .ToListAsync(cancellationToken);

            var financialRows = await _context.FinancialRecords
                .AsNoTracking()
                .Where(record =>
                    record.Booking.PlaceId == placeId &&
                    ((record.Booking.PaymentApprovedAt.HasValue &&
                      record.Booking.PaymentApprovedAt.Value >= sixMonthsStart &&
                      record.Booking.PaymentApprovedAt.Value < nextMonthStart) ||
                     (record.CollectedAt.HasValue &&
                      record.CollectedAt.Value >= sixMonthsStart &&
                      record.CollectedAt.Value < nextMonthStart) ||
                     (record.UpdatedAt >= sixMonthsStart &&
                      record.UpdatedAt < nextMonthStart)))
                .Select(record => new FinancialEventRow
                {
                    BookingId = record.BookingId,
                    PaymentApprovedAt = record.Booking.PaymentApprovedAt,
                    AmountPaidOnline = record.AmountPaidOnline,
                    PaymentTransactionsJson = record.PaymentTransactionsJson,
                    CollectedAt = record.CollectedAt,
                    AmountCollectedAtProperty = record.AmountCollectedAtProperty,
                    UpdatedAt = record.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            var onlinePaymentEvents = BuildOnlinePaymentEvents(
                financialRows,
                refundRows,
                sixMonthsStart,
                nextMonthStart);

            var commissionRows = await _context.CommissionTransactions
                .AsNoTracking()
                .Where(item =>
                    item.PlaceId == placeId &&
                    item.Status == "Completed" &&
                    item.CreatedAt.HasValue &&
                    item.CreatedAt.Value >= sixMonthsStart &&
                    item.CreatedAt.Value < nextMonthStart &&
                    (item.TransactionType == "CommissionCharge" ||
                     item.TransactionType == "CommissionRelease"))
                .Select(item => new CommissionEventRow
                {
                    OccurredAt = item.CreatedAt,
                    TransactionType = item.TransactionType,
                    Amount = item.Amount
                })
                .ToListAsync(cancellationToken);

            decimal PaidOnline(DateTime start, DateTime end) => onlinePaymentEvents
                .Where(item => item.OccurredAt >= start && item.OccurredAt < end)
                .Sum(item => item.Amount);

            decimal CollectedAtProperty(DateTime start, DateTime end) => financialRows
                .Where(item => InPeriod(item.CollectedAt, start, end))
                .Sum(item => Math.Max(0m, item.AmountCollectedAtProperty));

            decimal CompletedRefunds(DateTime start, DateTime end) => refundRows
                .Where(item => InPeriod(item.ProcessedAt, start, end))
                .Sum(item => Math.Max(0m, item.RefundAmount));

            decimal NetCommission(DateTime start, DateTime end)
            {
                var charged = commissionRows
                    .Where(item =>
                        InPeriod(item.OccurredAt, start, end) &&
                        IsState(item.TransactionType, "CommissionCharge"))
                    .Sum(item => Math.Max(0m, item.Amount));

                var released = commissionRows
                    .Where(item =>
                        InPeriod(item.OccurredAt, start, end) &&
                        IsState(item.TransactionType, "CommissionRelease"))
                    .Sum(item => Math.Max(0m, item.Amount));

                return Math.Round(charged - released, 2);
            }

            decimal NetCashFlow(DateTime start, DateTime end) => Math.Round(
                PaidOnline(start, end) +
                CollectedAtProperty(start, end) -
                CompletedRefunds(start, end) -
                NetCommission(start, end),
                2);

            var currentPaidOnline = PaidOnline(monthStart, nextMonthStart);
            var currentCollectedAtProperty = CollectedAtProperty(monthStart, nextMonthStart);
            var currentRefunds = CompletedRefunds(monthStart, nextMonthStart);
            var currentCommission = NetCommission(monthStart, nextMonthStart);
            var currentNetRevenue = NetCashFlow(monthStart, nextMonthStart);
            var previousNetRevenue = NetCashFlow(previousMonthStart, monthStart);

            var currentScheduledBookingValue = await bookingQuery
                .Where(booking =>
                    booking.StartDate >= monthStart &&
                    booking.StartDate < nextMonthStart &&
                    booking.BookingState != "Cancelled" &&
                    booking.BookingState != "Rejected" &&
                    booking.BookingState != "Expired")
                .SumAsync(booking => (decimal?)booking.TotalPrice, cancellationToken) ?? 0m;

            model.Finance = new OwnerDashboardFinanceDto
            {
                GrossBookingValue = DisplayAmount(currentScheduledBookingValue),
                PaidOnline = DisplayAmount(currentPaidOnline),
                CollectedAtProperty = DisplayAmount(currentCollectedAtProperty),
                RefundedAmount = DisplayAmount(currentRefunds),
                PlatformCommission = DisplayAmount(currentCommission),
                NetCollectedRevenue = DisplayAmount(currentNetRevenue)
            };

            model.Kpis.MonthlyRevenue = DisplayAmount(currentNetRevenue);
            model.Kpis.PreviousMonthRevenue = DisplayAmount(previousNetRevenue);
            model.Kpis.RevenueChangePercent = PercentageChange(currentNetRevenue, previousNetRevenue);

            for (var index = 0; index < 6; index++)
            {
                var month = sixMonthsStart.AddMonths(index);
                var monthEnd = month.AddMonths(1);

                model.Finance.MonthlyLabels.Add(MonthLabel(month));
                model.Finance.MonthlyCollected.Add(DisplayAmount(
                    PaidOnline(month, monthEnd) +
                    CollectedAtProperty(month, monthEnd) -
                    CompletedRefunds(month, monthEnd)));
                model.Finance.MonthlyCommission.Add(DisplayAmount(
                    NetCommission(month, monthEnd)));
            }

            // =========================================================
            // حساب ضمان العمولة
            // =========================================================
            var requiredGuaranteeYer = isBookablePlace
                ? Math.Max(0m, settings?.RequiredCommissionGuaranteeBalance ?? 0m)
                : 0m;

            var account = isBookablePlace
                ? await _context.PlaceCommissionAccounts
                    .AsNoTracking()
                    .Where(item => item.PlaceId == placeId)
                    .Select(item => new
                    {
                        item.AvailableBalance,
                        item.HeldBalance,
                        item.DebtAmount,
                        item.AccountStatus,
                        item.LowBalanceThreshold
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var lowBalanceThresholdYer = isBookablePlace
                ? Math.Max(
                    0m,
                    account?.LowBalanceThreshold ??
                    settings?.DefaultLowBalanceThreshold ??
                    0m)
                : 0m;

            var availableBalanceYer = Math.Max(0m, account?.AvailableBalance ?? 0m);
            var heldBalanceYer = Math.Max(0m, account?.HeldBalance ?? 0m);
            var debtAmountYer = Math.Max(0m, account?.DebtAmount ?? 0m);
            var accountStatus = isBookablePlace
                ? (string.IsNullOrWhiteSpace(account?.AccountStatus)
                    ? "NeedsTopUp"
                    : account!.AccountStatus!.Trim())
                : "NotApplicable";

            var fundingPercent = requiredGuaranteeYer <= 0m
                ? (isBookablePlace ? 100m : 0m)
                : Math.Clamp(
                    Math.Round(availableBalanceYer * 100m / requiredGuaranteeYer, 1),
                    0m,
                    100m);

            model.Guarantee = new OwnerDashboardGuaranteeDto
            {
                AvailableBalance = DisplayAmount(availableBalanceYer),
                HeldBalance = DisplayAmount(heldBalanceYer),
                DebtAmount = DisplayAmount(debtAmountYer),
                RequiredBalance = DisplayAmount(requiredGuaranteeYer),
                LowBalanceThreshold = DisplayAmount(lowBalanceThresholdYer),
                FundingPercent = fundingPercent,
                AccountStatus = accountStatus,
                AccountStatusLabel = isBookablePlace
                    ? GuaranteeStatusLabel(accountStatus)
                    : "غير مطبق"
            };

            model.Kpis.AvailableGuaranteeBalance = model.Guarantee.AvailableBalance;
            model.Kpis.GuaranteeStatus = accountStatus;

            // =========================================================
            // التقييمات والمفضلة
            // =========================================================
            var reviewRows = await _context.Reviews
                .AsNoTracking()
                .Where(review =>
                    review.PlaceId == placeId &&
                    review.ParentReviewId == null &&
                    review.Stars.HasValue)
                .Select(review => new ReviewRow
                {
                    ReviewId = review.ReviewId,
                    Stars = review.Stars,
                    ReviewText = review.ReviewText,
                    CreatedAt = review.CreatedAt,
                    OwnerReply = review.OwnerReply,
                    CustomerName = review.User != null ? review.User.FullName : null
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

            // =========================================================
            // التشغيل الحالي
            // =========================================================
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
                              booking.StartDate <= now &&
                              (booking.EndDate.HasValue
                                  ? booking.EndDate.Value > now
                                  : booking.StartDate >= today)
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
                           (booking.EndDate.HasValue
                               ? booking.EndDate.Value > now
                               : booking.StartDate >= today),
                cancellationToken);

            var arrivalsToday = await bookingQuery.CountAsync(
                booking => booking.BookingState == "Confirmed" &&
                           booking.StartDate >= today &&
                           booking.StartDate < tomorrow,
                cancellationToken);

            var departuresToday = await bookingQuery.CountAsync(
                booking => (booking.BookingState == "Confirmed" ||
                            booking.BookingState == "Completed") &&
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
                    TotalPrice = DisplayAmount(item.TotalPrice),
                    Url = BookingUrl(item.BookingId, item.BookingState)
                }).ToList()
            };

            // =========================================================
            // عناصر تحتاج إجراء
            // =========================================================
            var pendingBookings = await bookingQuery.CountAsync(
                booking => booking.BookingState == "Pending",
                cancellationToken);

            var paymentReviews = await bookingQuery.CountAsync(
                booking => booking.BookingState == "PaymentReview" ||
                           booking.PaymentStatus == "UnderReview" ||
                           (booking.PaymentStatus == "Pending" &&
                            booking.PaymentNotificationImage != null &&
                            booking.PaymentNotificationImage != ""),
                cancellationToken);

            var pendingAdditionalPayments = CountPendingAdditionalPayments(financialRows);
            paymentReviews += pendingAdditionalPayments;

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

            var trialActive =
                selectedPlace.FreeTrialStartedAt.HasValue &&
                selectedPlace.FreeTrialEndsAt.HasValue &&
                selectedPlace.FreeTrialStartedAt.Value <= now &&
                selectedPlace.FreeTrialEndsAt.Value > now;

            BuildAttentionItems(
                model,
                placeId,
                pendingBookings,
                paymentReviews,
                awaitingReviewReply,
                unreadMessages,
                pendingRefunds,
                isBookablePlace,
                trialActive,
                accountStatus,
                model.Guarantee.AvailableBalance,
                model.Guarantee.RequiredBalance,
                displayCurrency);

            // =========================================================
            // اتجاه الحجوزات: تاريخ الحدث نفسه
            // =========================================================
            var createdTrendRows = await bookingQuery
                .Where(booking => booking.CreatedAt.HasValue &&
                                  booking.CreatedAt.Value >= trendStart &&
                                  booking.CreatedAt.Value < tomorrow)
                .GroupBy(booking => booking.CreatedAt!.Value.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var completedTrendRows = await bookingQuery
                .Where(booking => booking.CompletedAt.HasValue &&
                                  booking.CompletedAt.Value >= trendStart &&
                                  booking.CompletedAt.Value < tomorrow &&
                                  booking.BookingState == "Completed")
                .GroupBy(booking => booking.CompletedAt!.Value.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var closedTrendRows = await bookingQuery
                .Where(booking => booking.ClosedAt.HasValue &&
                                  booking.ClosedAt.Value >= trendStart &&
                                  booking.ClosedAt.Value < tomorrow &&
                                  (booking.BookingState == "Cancelled" ||
                                   booking.BookingState == "Rejected" ||
                                   booking.BookingState == "Expired"))
                .GroupBy(booking => booking.ClosedAt!.Value.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var createdLookup = createdTrendRows.ToDictionary(item => item.Date.Date, item => item.Count);
            var completedLookup = completedTrendRows.ToDictionary(item => item.Date.Date, item => item.Count);
            var closedLookup = closedTrendRows.ToDictionary(item => item.Date.Date, item => item.Count);

            for (var day = trendStart; day <= today; day = day.AddDays(1))
            {
                model.BookingTrend.Labels.Add(day.ToString("dd/MM"));
                model.BookingTrend.Bookings.Add(createdLookup.TryGetValue(day.Date, out var created) ? created : 0);
                model.BookingTrend.Completed.Add(completedLookup.TryGetValue(day.Date, out var completed) ? completed : 0);
                model.BookingTrend.Cancelled.Add(closedLookup.TryGetValue(day.Date, out var closed) ? closed : 0);
            }

            // =========================================================
            // التسويق
            // =========================================================
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
                          eventItem.EventAt < nextMonthStart
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
                SpentAmount = DisplayAmount(marketingSpent),
                ConversionRate = impressions <= 0
                    ? 0m
                    : Math.Round(conversions * 100m / impressions, 2),
                Favorites = favoritesCount
            };

            // =========================================================
            // التقويم والنشاط الحديث
            // =========================================================
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
                DisplayMoney,
                cancellationToken);

            return model;
        }

        private async Task<List<OwnerDashboardActivityDto>> BuildRecentActivitiesAsync(
            int ownerId,
            int placeId,
            Func<decimal, string> displayMoney,
            CancellationToken cancellationToken)
        {
            var activities = new List<OwnerDashboardActivityDto>();

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Where(item => item.PlaceId == placeId)
                .OrderByDescending(item => item.LastStatusChangedAt ?? item.CreatedAt)
                .ThenByDescending(item => item.BookingId)
                .Take(5)
                .Select(item => new
                {
                    item.BookingId,
                    item.BookingState,
                    item.TotalPrice,
                    item.CreatedAt,
                    item.LastStatusChangedAt,
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
                    Description = $"{FirstNonEmpty("عميل", item.CustomerName, item.GuestName)} · {displayMoney(item.TotalPrice)}",
                    Icon = "bi-calendar2-check-fill",
                    Tone = IsClosedWithoutRevenue(item.BookingState) ? "danger" : "primary",
                    OccurredAt = item.LastStatusChangedAt ?? item.CreatedAt ?? item.StartDate,
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
                    CustomerName = item.User != null ? item.User.FullName : null
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
                    item.SendTime,
                    item.IsRead,
                    SenderName = item.Sender != null ? item.Sender.FullName : null
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
                    Description = $"{displayMoney(item.Amount)} · {TransactionStatusLabel(item.Status)}",
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

        private static List<MoneyEventRow> BuildOnlinePaymentEvents(
            IReadOnlyCollection<FinancialEventRow> financialRows,
            IReadOnlyCollection<RefundEventRow> refundRows,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            var result = new List<MoneyEventRow>();

            // عند إلغاء حجز قد يتم تصفير AmountPaidOnline، بينما RefundRequest.DepositPaid
            // يحتفظ بقيمة الدفع التاريخية. نستخدم الأعلى منهما لإعادة بناء الحركة الأصلية دون اختلاق رقم.
            var historicalPaidByBooking = refundRows
                .GroupBy(item => item.BookingId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Max(item => Math.Max(0m, item.DepositPaid)));

            foreach (var row in financialRows)
            {
                var ledgerReadable = BookingPaymentLedger.TryRead(
                    row.PaymentTransactionsJson,
                    out var transactions);

                var approvedAdditionalTotal = 0m;

                if (ledgerReadable)
                {
                    foreach (var payment in transactions.Where(BookingPaymentLedger.IsApproved))
                    {
                        var amount = Math.Max(0m, payment.Amount);
                        if (amount <= 0m)
                        {
                            continue;
                        }

                        approvedAdditionalTotal += amount;

                        var occurredAt = payment.ReviewedAt ?? payment.SubmittedAt;
                        if (occurredAt >= rangeStart && occurredAt < rangeEnd)
                        {
                            result.Add(new MoneyEventRow
                            {
                                OccurredAt = occurredAt,
                                Amount = amount
                            });
                        }
                    }
                }

                var historicalPaid = Math.Max(0m, row.AmountPaidOnline);
                if (historicalPaidByBooking.TryGetValue(row.BookingId, out var refundDepositPaid))
                {
                    historicalPaid = Math.Max(historicalPaid, refundDepositPaid);
                }

                // PaymentTransactionsJson يسجل الدفعات الإضافية؛ الباقي هو الدفعة الأولى.
                var initialPayment = ledgerReadable
                    ? Math.Max(0m, historicalPaid - approvedAdditionalTotal)
                    : historicalPaid;

                if (initialPayment > 0m &&
                    row.PaymentApprovedAt.HasValue &&
                    row.PaymentApprovedAt.Value >= rangeStart &&
                    row.PaymentApprovedAt.Value < rangeEnd)
                {
                    result.Add(new MoneyEventRow
                    {
                        OccurredAt = row.PaymentApprovedAt.Value,
                        Amount = initialPayment
                    });
                }
            }

            return result;
        }

        private static int CountPendingAdditionalPayments(
            IEnumerable<FinancialEventRow> financialRows)
        {
            var count = 0;

            foreach (var row in financialRows)
            {
                if (!BookingPaymentLedger.TryRead(
                        row.PaymentTransactionsJson,
                        out var transactions))
                {
                    continue;
                }

                count += transactions.Count(BookingPaymentLedger.IsPending);
            }

            return count;
        }

        private static void BuildAttentionItems(
            OwnerDashboardDto model,
            int placeId,
            int pendingBookings,
            int paymentReviews,
            int awaitingReviewReply,
            int unreadMessages,
            int pendingRefunds,
            bool isBookablePlace,
            bool trialActive,
            string accountStatus,
            decimal availableBalance,
            decimal requiredGuarantee,
            string displayCurrency)
        {
            if (pendingBookings > 0)
            {
                model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
                {
                    Key = "pending-bookings",
                    Title = "حجوزات تنتظر قرارك",
                    Description = "هذه طلبات فعلية بحالة انتظار في قاعدة البيانات.",
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
                    Title = "إثباتات دفع تحتاج مراجعة",
                    Description = "تشمل إثبات الدفع الأول وأي دفعات إضافية ما زالت قيد المراجعة.",
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
                    Title = "تقييمات بلا رد",
                    Description = "تقييمات منشورة لم يسجل لها رد مالك بعد.",
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
                    Description = "رسائل وصلت لحساب المالك ولم تُقرأ بعد.",
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
                    Description = "طلبات مسجلة بحالة Pending أو Processing.",
                    Icon = "bi-arrow-counterclockwise",
                    Tone = "danger",
                    Count = pendingRefunds,
                    Url = $"/Owner/Financials?financeTab=refunds&placeId={placeId}",
                    ActionLabel = "متابعة"
                });
            }

            if (!isBookablePlace || trialActive)
            {
                return;
            }

            var financialRisk = IsState(accountStatus, "LowBalance") ||
                                IsState(accountStatus, "InDebt") ||
                                IsState(accountStatus, "Suspended") ||
                                (IsState(accountStatus, "NeedsTopUp") && requiredGuarantee > availableBalance);

            if (!financialRisk)
            {
                return;
            }

            var symbol = CurrencyDisplayService.GetCurrencySymbol(displayCurrency);
            var availableText = CurrencyDisplayService.FormatNumber(availableBalance, displayCurrency);
            var requiredText = CurrencyDisplayService.FormatNumber(requiredGuarantee, displayCurrency);

            model.AttentionItems.Add(new OwnerDashboardAttentionItemDto
            {
                Key = "commission-balance",
                Title = GuaranteeStatusLabel(accountStatus),
                Description = requiredGuarantee > 0m
                    ? $"المتاح {availableText} {symbol} من رصيد مطلوب {requiredText} {symbol}."
                    : "راجع رصيد ضمان العمولات لمعرفة حالة الحساب المسجلة.",
                Icon = "bi-shield-exclamation",
                Tone = IsState(accountStatus, "Suspended") || IsState(accountStatus, "InDebt")
                    ? "danger"
                    : "warning",
                Count = 1,
                Url = $"/Owner/Financials?financeTab=balance&placeId={placeId}",
                ActionLabel = "فتح الرصيد"
            });
        }

        private static bool InPeriod(DateTime? value, DateTime start, DateTime end) =>
            value.HasValue && value.Value >= start && value.Value < end;

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
            if (previous <= 0m)
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
            public string? ExchangeRateRegion { get; set; }
            public DateTime? FreeTrialStartedAt { get; set; }
            public DateTime? FreeTrialEndsAt { get; set; }
        }

        private sealed class FinancialEventRow
        {
            public int BookingId { get; set; }
            public DateTime? PaymentApprovedAt { get; set; }
            public decimal AmountPaidOnline { get; set; }
            public string? PaymentTransactionsJson { get; set; }
            public DateTime? CollectedAt { get; set; }
            public decimal AmountCollectedAtProperty { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        private sealed class RefundEventRow
        {
            public int BookingId { get; set; }
            public decimal DepositPaid { get; set; }
            public DateTime? ProcessedAt { get; set; }
            public decimal RefundAmount { get; set; }
        }

        private sealed class CommissionEventRow
        {
            public DateTime? OccurredAt { get; set; }
            public string? TransactionType { get; set; }
            public decimal Amount { get; set; }
        }

        private sealed class MoneyEventRow
        {
            public DateTime OccurredAt { get; set; }
            public decimal Amount { get; set; }
        }

        private sealed class ReviewRow
        {
            public int ReviewId { get; set; }
            public int? Stars { get; set; }
            public string? ReviewText { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string? OwnerReply { get; set; }
            public string? CustomerName { get; set; }
        }
    }
}
