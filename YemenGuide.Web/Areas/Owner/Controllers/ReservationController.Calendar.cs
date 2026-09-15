using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using YemenGuide.Web.Services;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    public partial class ReservationController
    {
        private static readonly string[] CalendarBlockingAssignmentStates =
        {
            "Held", "Confirmed"
        };

        private static readonly HashSet<string> ManualBookingChannels =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Phone", "WalkIn", "WhatsApp", "Other"
            };

        private static readonly HashSet<string> ManualPaymentModes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Unpaid", "Collected"
            };

        private static readonly HashSet<string> CalendarHistoricalBookingStates =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Completed", "Cancelled", "Rejected", "NoShow", "Expired"
            };


        // =====================================================================
        // أدوات العملة الخاصة بالتقويم والحجز المباشر
        //
        // ملاحظة مهمة:
        // هذا الملف Partial، لكن ReservationController.cs الحالي لا يحتوي
        // على هذه الأدوات. لذلك نضعها هنا حتى يكون Calendar.cs مكتفيًا بذاته
        // بدون تغيير Constructor أو أي ملف آخر.
        // =====================================================================

        private sealed class OwnerReservationCurrencyContext
        {
            public string Currency { get; init; } =
                CurrencyDisplayService.YemeniRial;

            public string CurrencyCode { get; init; } =
                "YER";

            public string CurrencySymbol { get; init; } =
                "ر.ي";

            public string ExchangeRateRegion { get; init; } =
                ExchangeRateRegionResolver.SanaaMarket;
        }


        private CurrencyDisplayService GetCalendarCurrencyDisplayService()
        {
            var service =
                HttpContext.RequestServices.GetService(
                    typeof(CurrencyDisplayService))
                as CurrencyDisplayService;

            if (service == null)
            {
                throw new InvalidOperationException(
                    "CurrencyDisplayService غير مسجلة في Dependency Injection.");
            }

            return service;
        }


        private CurrencyService GetCalendarCurrencyService()
        {
            var service =
                HttpContext.RequestServices.GetService(
                    typeof(CurrencyService))
                as CurrencyService;

            if (service == null)
            {
                throw new InvalidOperationException(
                    "CurrencyService غير مسجلة في Dependency Injection.");
            }

            return service;
        }


        private async Task<OwnerReservationCurrencyContext>
            GetOwnerCurrencyContextAsync(int placeId)
        {
            var currencyDisplayService =
                GetCalendarCurrencyDisplayService();

            var currency =
                await currencyDisplayService
                    .GetPreferredCurrencyAsync(
                        HttpContext.RequestAborted);

            currency =
                CurrencyDisplayService.NormalizeCurrency(
                    currency);

            if (string.IsNullOrWhiteSpace(currency))
            {
                currency =
                    CurrencyDisplayService.YemeniRial;
            }

            string? region = null;

            if (placeId > 0)
            {
                region =
                    await _context.Places
                        .AsNoTracking()
                        .Where(place =>
                            place.PlaceId == placeId)
                        .Select(place =>
                            place.City != null
                                ? place.City.ExchangeRateRegion
                                : null)
                        .FirstOrDefaultAsync(
                            HttpContext.RequestAborted);
            }

            if (string.IsNullOrWhiteSpace(region))
            {
                region =
                    ExchangeRateRegionResolver.SanaaMarket;
            }

            return new OwnerReservationCurrencyContext
            {
                Currency = currency,

                CurrencyCode =
                    CurrencyDisplayService.GetCurrencyCode(
                        currency),

                CurrencySymbol =
                    CurrencyDisplayService.GetCurrencySymbol(
                        currency),

                ExchangeRateRegion =
                    region.Trim()
            };
        }


        private decimal ToDisplayCurrency(
            decimal amountInYer,
            OwnerReservationCurrencyContext currency)
        {
            return GetCalendarCurrencyService()
                .ConvertAmount(
                    amountInYer,
                    currency.Currency,
                    currency.ExchangeRateRegion);
        }


        private decimal ToYer(
            decimal amountInDisplayCurrency,
            OwnerReservationCurrencyContext currency)
        {
            return GetCalendarCurrencyService()
                .ConvertToYer(
                    amountInDisplayCurrency,
                    currency.Currency,
                    currency.ExchangeRateRegion);
        }


        private string DisplayMoney(
            decimal amountInYer,
            OwnerReservationCurrencyContext currency)
        {
            var converted =
                ToDisplayCurrency(
                    amountInYer,
                    currency);

            var number =
                CurrencyDisplayService.FormatNumber(
                    converted,
                    currency.Currency);

            return currency.CurrencyCode == "USD"
                ? $"${number}"
                : $"{number} {currency.CurrencySymbol}";
        }


        private static bool IsMonetaryDiscountType(
            string? discountType)
        {
            return
                string.Equals(
                    discountType,
                    "FixedAmount",
                    StringComparison.OrdinalIgnoreCase) ||

                string.Equals(
                    discountType,
                    "FixedDiscount",
                    StringComparison.OrdinalIgnoreCase) ||

                string.Equals(
                    discountType,
                    "SpecialPrice",
                    StringComparison.OrdinalIgnoreCase) ||

                string.Equals(
                    discountType,
                    "ServiceSpecialPrice",
                    StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ManualPromotionResolution
        {
            public int? PromotionId { get; set; }
            public string PromotionTitle { get; set; } = string.Empty;
            public string PromotionCode { get; set; } = string.Empty;
            public string OfferMode { get; set; } = "Discount";
            public string RewardType { get; set; } = string.Empty;
            public int? RequiredQuantity { get; set; }
            public int? RewardQuantity { get; set; }
            public bool IsRewardRepeatable { get; set; }
            public int AppliedFreeQuantity { get; set; }
            public int RepeatCount { get; set; }
            public int? RewardServiceId { get; set; }
            public string RewardServiceName { get; set; } = string.Empty;
            public decimal? SpecialPrice { get; set; }
            public string OfferSummary { get; set; } = string.Empty;
            public string DiscountType { get; set; } = string.Empty;
            public decimal DiscountValue { get; set; }
            public decimal? MaximumDiscountAmount { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal BenefitAmount { get; set; }
            public bool AddsComplimentaryService { get; set; }
            public bool IsApplied { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private sealed class ManualBookingQuoteInternal
        {
            public OwnerManualBookingQuoteDto PublicQuote { get; set; } = new();
            public Place Place { get; set; } = null!;
            public Room? Room { get; set; }
            public PlacePackage? Package { get; set; }
            public User? Customer { get; set; }
            public List<PlaceAdditionalService> Services { get; set; } = new();
            public Dictionary<int, int> ServiceQuantities { get; set; } = new();
            public HashSet<int> ComplimentaryServiceIds { get; set; } = new();
            public List<RoomUnit> CandidateRoomUnits { get; set; } = new();
            public ManualPromotionResolution Promotion { get; set; } = new();
            public DateTime StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public int Quantity { get; set; }
            public string ResolvedPaymentMethod { get; set; } = string.Empty;
        }

        private static string MaskManualWalletAccount(string? accountNumber)
        {
            var value = accountNumber?.Trim() ?? string.Empty;
            if (value.Length <= 4) return value;
            return $"{new string('•', Math.Min(6, value.Length - 4))}{value[^4..]}";
        }

        private async Task<List<OwnerManualWalletOptionDto>> GetOwnerManualWalletsAsync(int ownerId)
        {
            return await _context.OwnerWallets
                .AsNoTracking()
                .Where(item =>
                    item.OwnerId == ownerId &&
                    item.IsActive == true &&
                    item.Wallet.IsActive == true)
                .OrderByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Wallet.WalletName)
                .Select(item => new OwnerManualWalletOptionDto
                {
                    WalletId = item.WalletId,
                    WalletName = item.Wallet.WalletName ?? "محفظة إلكترونية",
                    AccountNumberMasked = MaskManualWalletAccount(item.AccountNumber)
                })
                .ToListAsync(HttpContext.RequestAborted);
        }

        private async Task<(bool Success, string StoredValue, string Message)>
            ResolveManualPaymentMethodAsync(int ownerId, string? paymentMethod, int? walletId)
        {
            var method = paymentMethod?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(method) || method.Length > 30)
                return (false, string.Empty, "حدد طريقة دفع صحيحة.");

            if (!IsState(method, "Wallet"))
                return (true, method, string.Empty);

            if (!walletId.HasValue || walletId.Value <= 0)
                return (false, string.Empty, "حدد نوع المحفظة التي استلمت الدفعة.");

            var wallet = await _context.OwnerWallets
                .AsNoTracking()
                .Where(item =>
                    item.OwnerId == ownerId &&
                    item.WalletId == walletId.Value &&
                    item.IsActive == true &&
                    item.Wallet.IsActive == true)
                .Select(item => new
                {
                    item.WalletId,
                    item.Wallet.WalletName
                })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (wallet == null)
                return (false, string.Empty, "المحفظة المختارة غير مفعلة ضمن حسابات استقبال المالك.");

            var storedValue = $"محفظة: {wallet.WalletName?.Trim() ?? "إلكترونية"}";
            if (storedValue.Length > 30)
                storedValue = storedValue[..30];

            return (true, storedValue, string.Empty);
        }

        private void ApplyManualQuoteDisplayCurrency(
            OwnerManualBookingQuoteDto quote,
            OwnerReservationCurrencyContext currency)
        {
            quote.BaseAmount = ToDisplayCurrency(quote.BaseAmount, currency);
            quote.ServicesTotal = ToDisplayCurrency(quote.ServicesTotal, currency);
            quote.CleaningFee = ToDisplayCurrency(quote.CleaningFee, currency);
            quote.TaxAmount = ToDisplayCurrency(quote.TaxAmount, currency);
            quote.OriginalTotalPrice = ToDisplayCurrency(quote.OriginalTotalPrice, currency);
            quote.DiscountAmount = ToDisplayCurrency(quote.DiscountAmount, currency);
            quote.TotalPrice = ToDisplayCurrency(quote.TotalPrice, currency);
            quote.DepositAmount = ToDisplayCurrency(quote.DepositAmount, currency);
            quote.RemainingAmount = ToDisplayCurrency(quote.RemainingAmount, currency);
            quote.PlatformCommission = ToDisplayCurrency(quote.PlatformCommission, currency);

            if (quote.DiscountValue.HasValue && IsMonetaryDiscountType(quote.DiscountType))
            {
                quote.DiscountValue = ToDisplayCurrency(quote.DiscountValue.Value, currency);
                quote.PromotionMessage = string.IsNullOrWhiteSpace(quote.PromotionTitle)
                    ? "تم تطبيق العرض المالي على الحجز."
                    : $"تم تطبيق العرض: {quote.PromotionTitle}.";
            }
        }

        private void ApplyCalendarDayDisplayCurrency(
            OwnerCalendarDayDetailsDto dto,
            OwnerReservationCurrencyContext currency)
        {
            foreach (var booking in dto.Bookings)
            {
                booking.TotalPrice = ToDisplayCurrency(booking.TotalPrice, currency);
                booking.DepositAmount = ToDisplayCurrency(booking.DepositAmount, currency);
                booking.PaidAmount = ToDisplayCurrency(booking.PaidAmount, currency);
                booking.RemainingAmount = ToDisplayCurrency(booking.RemainingAmount, currency);
            }
        }

        // =====================================================================
        // سياق التقويم — المنشأة تأتي من هيدر المالك فقط
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> GetCalendarContext(DateTime? start = null, DateTime? end = null)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();

            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            if (!placeId.HasValue)
                return Json(new { success = false, message = "لا توجد منشأة نشطة مرتبطة بحسابك." });

            var currency = await GetOwnerCurrencyContextAsync(placeId.Value);
            await ExpireOverdueAwaitingPaymentsAsync(ownerId, placeId.Value);

            var place = await _context.Places
                .AsNoTracking()
                .Include(item => item.Category)
                .Include(item => item.PlacePolicy)
                .FirstOrDefaultAsync(item => item.PlaceId == placeId.Value && item.OwnerId == ownerId);

            if (place == null)
                return Json(new { success = false, message = "تعذر تحميل المنشأة المختارة من الهيدر." });

            var rangeStart = (start ?? DateTime.Today).Date;
            var rangeEnd = (end ?? rangeStart.AddMonths(1)).Date;
            if (rangeEnd <= rangeStart) rangeEnd = rangeStart.AddDays(1);
            if ((rangeEnd - rangeStart).TotalDays > 370) rangeEnd = rangeStart.AddDays(370);

            var rangeBookings = await _context.Bookings.AsNoTracking()
                .Where(item =>
                    item.PlaceId == place.PlaceId &&
                    item.StartDate < rangeEnd &&
                    (item.RoomId.HasValue
                        ? (item.EndDate ?? item.StartDate.AddDays(1)) > rangeStart
                        : item.StartDate >= rangeStart) &&
                    item.BookingState != "Cancelled" &&
                    item.BookingState != "Rejected" &&
                    item.BookingState != "Expired")
                .Select(item => new { item.BookingState, item.BookingSource })
                .ToListAsync();

            var totalUnits = await _context.RoomUnits.AsNoTracking().CountAsync(item =>
                item.PlaceId == place.PlaceId && item.IsAvailable == true);
            var occupiedToday = await GetOccupiedRoomUnitCountForDateAsync(place.PlaceId, DateTime.Today);
            var closedDays = await _context.PlaceCalendarExceptions.AsNoTracking().CountAsync(item =>
                item.PlaceId == place.PlaceId &&
                item.ExceptionType == "Closed" &&
                item.ExceptionDate >= rangeStart &&
                item.ExceptionDate < rangeEnd);

            var availableToday = Math.Max(0, totalUnits - occupiedToday);
            var occupancy = totalUnits <= 0 ? 0m : Math.Round(occupiedToday * 100m / totalUnits, 1);
            var supportsRooms = await _context.Rooms.AsNoTracking().AnyAsync(item =>
                item.PlaceId == place.PlaceId && item.IsAvailable == true);
            var supportsPackages = await _context.PlacePackages.AsNoTracking().AnyAsync(item =>
                item.PlaceId == place.PlaceId && item.IsActive == true);
            var manualTemplate = ResolveManualBookingTemplate(place, supportsRooms, supportsPackages, totalUnits);

            var context = new OwnerCalendarContextDto
            {
                PlaceId = place.PlaceId,
                PlaceName = place.PlaceNameAndDesc ?? "المنشأة الحالية",
                CategoryName = place.Category?.CategoryName ?? "منشأة",
                BookingSystemId = place.Category?.BookingSystemId ?? 0,
                SupportsRooms = supportsRooms,
                SupportsPackages = supportsPackages,
                ManualBookingTemplate = manualTemplate.Key,
                ManualBookingTemplateLabel = manualTemplate.Label,
                TotalActiveRoomUnits = totalUnits,
                OccupiedRoomUnitsToday = occupiedToday,
                AvailableRoomUnitsToday = availableToday,
                OccupancyPercentToday = occupancy,
                TotalBookingsInRange = rangeBookings.Count,
                ConfirmedBookingsInRange = rangeBookings.Count(item => IsState(item.BookingState, "Confirmed")),
                AwaitingPaymentBookingsInRange = rangeBookings.Count(item =>
                    IsState(item.BookingState, "AwaitingPayment") || IsState(item.BookingState, "PaymentReview")),
                ManualBookingsInRange = rangeBookings.Count(item => IsState(item.BookingSource, "OwnerManual")),
                OnlineBookingsInRange = rangeBookings.Count(item => !IsState(item.BookingSource, "OwnerManual")),
                ClosedDaysInRange = closedDays,
                BookingApprovalMode = place.PlacePolicy?.BookingApprovalMode ?? "Manual",
                MinimumNoticeHours = Math.Max(0, place.PlacePolicy?.MinimumNoticeHours ?? 0),
                BufferTimeHours = Math.Max(0, place.PlacePolicy?.BufferTimeHours ?? 0),
                MaximumHorizonMonths = Math.Max(0, place.PlacePolicy?.MaximumHorizonMonths ?? 0),
                ClosedDaysCsv = place.PlacePolicy?.ClosedDaysCsv ?? string.Empty,
                CheckInTime = place.PlacePolicy?.CheckInTime ?? place.CheckInTime ?? string.Empty,
                CheckOutTime = place.PlacePolicy?.CheckOutTime ?? place.CheckOutTime ?? string.Empty
            };

            return Json(new
            {
                success = true,
                data = context,
                currency = currency.Currency,
                currencyCode = currency.CurrencyCode,
                currencySymbol = currency.CurrencySymbol
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendarEventsV2(DateTime start, DateTime end)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            if (!placeId.HasValue) return Json(new { success = true, events = Array.Empty<object>() });

            var currency = await GetOwnerCurrencyContextAsync(placeId.Value);
            await ExpireOverdueAwaitingPaymentsAsync(ownerId, placeId.Value);
            start = start.Date;
            end = end.Date;
            if (end <= start) end = start.AddMonths(1);
            if ((end - start).TotalDays > 370) end = start.AddDays(370);

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(item => item.Customer)
                .Include(item => item.Room)
                .Include(item => item.Package)
                .Include(item => item.BookingRoomUnits)
                .Where(item =>
                    item.PlaceId == placeId.Value &&
                    item.StartDate < end &&
                    (item.RoomId.HasValue
                        ? (item.EndDate ?? item.StartDate.AddDays(1)) > start
                        : item.StartDate >= start))
                .OrderBy(item => item.StartDate)
                .ThenBy(item => item.BookingId)
                .ToListAsync();

            var exceptions = await _context.PlaceCalendarExceptions.AsNoTracking()
                .Where(item => item.PlaceId == placeId.Value &&
                               item.ExceptionType == "Closed" &&
                               item.ExceptionDate >= start && item.ExceptionDate < end)
                .ToListAsync();

            var events = new List<OwnerCalendarEventDto>();
            foreach (var booking in bookings)
            {
                var state = booking.BookingState?.Trim() ?? "Pending";
                var isManual = IsState(booking.BookingSource, "OwnerManual");
                var customerName = FirstNonEmpty("ضيف", booking.Customer?.FullName, booking.Customer?.Username, booking.GuestName);
                var resourceName = booking.Room != null
                    ? BuildRoomName(booking.Room)
                    : booking.Package != null ? BuildPackageName(booking.Package) : "حجز";
                var css = GetCalendarStateCss(state, isManual);
                var titlePrefix = isManual ? "مباشر" : "أونلاين";

                events.Add(new OwnerCalendarEventDto
                {
                    Id = $"B_{booking.BookingId}",
                    Title = $"{titlePrefix} · #{booking.BookingId} · {customerName}",
                    Start = booking.StartDate.Date,
                    End = booking.RoomId.HasValue
                        ? (booking.EndDate?.Date ?? booking.StartDate.Date.AddDays(1))
                        : booking.StartDate.Date.AddDays(1),
                    AllDay = true,
                    EventType = "booking",
                    State = state,
                    Source = isManual ? "OwnerManual" : "Online",
                    BookingId = booking.BookingId,
                    CustomerName = customerName,
                    ResourceName = resourceName,
                    Reason = BuildCalendarBookingStateReason(booking),
                    TimerExpiredAt = booking.TimerExpiredAt,
                    IsHistorical = IsCalendarHistoricalState(state),
                    ClassNames = isManual ? new List<string> { css, "yg-event-manual" } : new List<string> { css }
                });
            }

            var activeUnitIds = await _context.RoomUnits.AsNoTracking()
                .Where(item => item.PlaceId == placeId.Value && item.IsAvailable == true)
                .Select(item => item.RoomUnitId)
                .ToListAsync();

            if (activeUnitIds.Count > 0)
            {
                var activeUnitSet = activeUnitIds.ToHashSet();
                var referenceTime = DateTime.Now;
                for (var day = start; day < end; day = day.AddDays(1))
                {
                    var nextDay = day.AddDays(1);
                    var blockingForDay = bookings.Where(item =>
                        item.RoomId.HasValue &&
                        BlockingBookingStates.Contains(item.BookingState ?? string.Empty) &&
                        (item.BookingState != "AwaitingPayment" || !item.TimerExpiredAt.HasValue || item.TimerExpiredAt.Value > referenceTime) &&
                        item.StartDate < nextDay &&
                        (item.EndDate ?? item.StartDate.AddDays(1)) > day).ToList();

                    var assigned = blockingForDay.SelectMany(item => item.BookingRoomUnits)
                        .Where(item => activeUnitSet.Contains(item.RoomUnitId) && CalendarBlockingAssignmentStates.Contains(item.AssignmentStatus))
                        .Select(item => item.RoomUnitId).Distinct().Count();
                    var legacy = blockingForDay.Where(item => item.BookingRoomUnits.Count == 0).Sum(item => Math.Max(1, item.Quantity));
                    var occupied = Math.Min(activeUnitIds.Count, assigned + Math.Max(0, legacy));
                    if (occupied <= 0) continue;

                    events.Add(new OwnerCalendarEventDto
                    {
                        Id = $"O_{day:yyyyMMdd}",
                        Title = $"إشغال {occupied}/{activeUnitIds.Count}",
                        Start = day,
                        End = day.AddDays(1),
                        AllDay = true,
                        EventType = "occupancy",
                        State = occupied >= activeUnitIds.Count ? "Full" : "Available",
                        OccupiedUnits = occupied,
                        TotalUnits = activeUnitIds.Count,
                        ClassNames = new List<string>
                        {
                            occupied >= activeUnitIds.Count ? "yg-event-occupancy-full" : "yg-event-occupancy"
                        }
                    });
                }
            }

            foreach (var exception in exceptions)
            {
                events.Add(new OwnerCalendarEventDto
                {
                    Id = $"E_{exception.ExceptionId}",
                    Title = string.IsNullOrWhiteSpace(exception.Reason) ? "مغلق من المنشأة" : $"مغلق · {exception.Reason.Trim()}",
                    Start = exception.ExceptionDate.Date,
                    End = exception.ExceptionDate.Date.AddDays(1),
                    AllDay = true,
                    EventType = "closed",
                    State = "Closed",
                    ExceptionId = exception.ExceptionId,
                    Reason = exception.Reason?.Trim() ?? string.Empty,
                    ClassNames = new List<string> { "yg-event-closed" }
                });
            }

            var place = await _context.Places.AsNoTracking().Include(item => item.PlacePolicy)
                .FirstOrDefaultAsync(item => item.PlaceId == placeId.Value && item.OwnerId == ownerId);
            if (place != null)
            {
                var weeklyClosed = ReadWeeklyClosedDays(place);
                if (weeklyClosed.Count > 0)
                {
                    for (var day = start; day < end; day = day.AddDays(1))
                    {
                        if (!weeklyClosed.Contains(day.DayOfWeek.ToString())) continue;
                        events.Add(new OwnerCalendarEventDto
                        {
                            Id = $"P_{day:yyyyMMdd}",
                            Title = "إغلاق أسبوعي",
                            Start = day,
                            End = day.AddDays(1),
                            AllDay = true,
                            EventType = "policyClosed",
                            State = "PolicyClosed",
                            ClassNames = new List<string> { "yg-event-policy-closed" }
                        });
                    }
                }
            }

            return Json(new
            {
                success = true,
                events,
                currency = currency.Currency,
                currencyCode = currency.CurrencyCode,
                currencySymbol = currency.CurrencySymbol
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendarDayDetails(DateTime date)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            if (!placeId.HasValue) return Json(new { success = false, message = "لا توجد منشأة حالية." });

            var currency = await GetOwnerCurrencyContextAsync(placeId.Value);
            await ExpireOverdueAwaitingPaymentsAsync(ownerId, placeId.Value);
            date = date.Date;

            var place = await _context.Places.AsNoTracking().Include(item => item.PlacePolicy)
                .FirstOrDefaultAsync(item => item.PlaceId == placeId.Value && item.OwnerId == ownerId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة." });

            var exception = await _context.PlaceCalendarExceptions.AsNoTracking().FirstOrDefaultAsync(item =>
                item.PlaceId == place.PlaceId && item.ExceptionDate == date && item.ExceptionType == "Closed");

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(item => item.Customer)
                .Include(item => item.Room)
                .Include(item => item.Package)
                .Include(item => item.FinancialRecord)
                .Include(item => item.BookingRoomUnits).ThenInclude(item => item.RoomUnit)
                .Where(item =>
                    item.PlaceId == place.PlaceId &&
                    item.StartDate.Date <= date &&
                    (item.RoomId.HasValue
                        ? (item.EndDate ?? item.StartDate.AddDays(1)).Date > date
                        : item.StartDate.Date == date))
                .OrderBy(item => item.StartDate)
                .ThenBy(item => item.BookingId)
                .ToListAsync();

            var totalUnits = await _context.RoomUnits.AsNoTracking().CountAsync(item =>
                item.PlaceId == place.PlaceId && item.IsAvailable == true);
            var occupiedUnits = await GetOccupiedRoomUnitCountForDateAsync(place.PlaceId, date);
            var availableUnits = Math.Max(0, totalUnits - occupiedUnits);

            var dto = new OwnerCalendarDayDetailsDto
            {
                Date = date,
                IsWeeklyClosed = ReadWeeklyClosedDays(place).Contains(date.DayOfWeek.ToString()),
                IsExceptionClosed = exception != null,
                ClosedReason = exception?.Reason?.Trim() ?? string.Empty,
                TotalActiveRoomUnits = totalUnits,
                OccupiedRoomUnits = occupiedUnits,
                AvailableRoomUnits = availableUnits,
                OccupancyPercent = totalUnits <= 0 ? 0m : Math.Round(occupiedUnits * 100m / totalUnits, 1),
                BookingCount = bookings.Count,
                ConfirmedCount = bookings.Count(item => IsState(item.BookingState, "Confirmed")),
                AwaitingPaymentCount = bookings.Count(item => IsState(item.BookingState, "AwaitingPayment")),
                PaymentReviewCount = bookings.Count(item => IsState(item.BookingState, "PaymentReview")),
                PendingCount = bookings.Count(item => IsState(item.BookingState, "Pending")),
                CompletedCount = bookings.Count(item => IsState(item.BookingState, "Completed")),
                NoShowCount = bookings.Count(item => IsState(item.BookingState, "NoShow")),
                CancelledCount = bookings.Count(item => IsState(item.BookingState, "Cancelled")),
                RejectedCount = bookings.Count(item => IsState(item.BookingState, "Rejected")),
                ExpiredCount = bookings.Count(item => IsState(item.BookingState, "Expired")),
                HistoricalCount = bookings.Count(item => IsCalendarHistoricalState(item.BookingState)),
                ManualCount = bookings.Count(item => IsState(item.BookingSource, "OwnerManual")),
                OnlineCount = bookings.Count(item => !IsState(item.BookingSource, "OwnerManual")),
                Bookings = bookings.Select(item =>
                {
                    var paid = (item.FinancialRecord?.AmountPaidOnline ?? 0m) +
                               (item.FinancialRecord?.AmountCollectedAtProperty ?? 0m);
                    var remaining = Math.Max(0m, item.FinancialRecord?.AmountToPayAtProperty ?? (item.TotalPrice - paid));
                    return new OwnerCalendarBookingItemDto
                    {
                        BookingId = item.BookingId,
                        CustomerId = item.CustomerId,
                        CustomerName = FirstNonEmpty("ضيف", item.Customer?.FullName, item.Customer?.Username, item.GuestName),
                        CustomerPhone = FirstNonEmpty("بدون هاتف", item.Customer?.PhoneNumber, item.Customer?.ContactInfo, item.GuestPhone),
                        BookingSource = item.BookingSource?.Trim() ?? "Online",
                        ManualBookingChannel = item.ManualBookingChannel?.Trim() ?? string.Empty,
                        BookingState = item.BookingState?.Trim() ?? string.Empty,
                        PaymentStatus = item.PaymentStatus?.Trim() ?? "Unpaid",
                        PaymentMethod = item.PaymentMethod?.Trim() ?? string.Empty,
                        RefundStatus = item.RefundStatus?.Trim() ?? string.Empty,
                        StatusReason = BuildCalendarBookingStateReason(item),
                        ClosedAt = item.ClosedAt,
                        Quantity = Math.Max(1, item.Quantity),
                        AdultsCount = Math.Max(0, item.AdultsCount),
                        ChildrenCount = Math.Max(0, item.ChildrenCount),
                        RoomId = item.RoomId,
                        RoomName = item.Room == null ? string.Empty : BuildRoomName(item.Room),
                        PackageId = item.PackageId,
                        PackageName = item.Package == null ? string.Empty : BuildPackageName(item.Package),
                        StartDate = item.StartDate,
                        EndDate = item.EndDate,
                        TotalPrice = item.TotalPrice,
                        DepositAmount = item.FinancialRecord?.DepositAmount ?? 0m,
                        PaidAmount = paid,
                        RemainingAmount = remaining,
                        OwnerInternalNotes = item.OwnerInternalNotes?.Trim() ?? string.Empty,
                        TimerExpiredAt = item.TimerExpiredAt,
                        IsTimerExpired = IsState(item.BookingState, "AwaitingPayment") &&
                                         item.TimerExpiredAt.HasValue &&
                                         item.TimerExpiredAt.Value <= DateTime.Now,
                        IsHistorical = IsCalendarHistoricalState(item.BookingState),
                        AssignedUnitsText = string.Join("، ", item.BookingRoomUnits
                            .Where(assignment =>
                                IsState(assignment.AssignmentStatus, "Held") ||
                                IsState(assignment.AssignmentStatus, "Confirmed") ||
                                IsState(assignment.AssignmentStatus, "Completed"))
                            .OrderBy(assignment => assignment.RoomUnit.FloorNumber)
                            .ThenBy(assignment => assignment.RoomUnit.RoomNumber)
                            .Select(assignment => string.IsNullOrWhiteSpace(assignment.RoomUnit.FloorLabel)
                                ? assignment.RoomUnit.RoomNumber
                                : $"{assignment.RoomUnit.RoomNumber} · {assignment.RoomUnit.FloorLabel}")),
                        CanRecordManualPayment =
                            IsState(item.BookingSource, "OwnerManual") &&
                            remaining > 0m &&
                            (string.IsNullOrWhiteSpace(item.RefundStatus) || IsState(item.RefundStatus, "NotApplicable")) &&
                            (
                                (IsState(item.BookingState, "AwaitingPayment") &&
                                 (!item.TimerExpiredAt.HasValue || item.TimerExpiredAt.Value > DateTime.Now)) ||
                                IsState(item.BookingState, "Confirmed") ||
                                IsState(item.BookingState, "Completed")
                            )
                    };
                }).ToList()
            };

            ApplyCalendarDayDisplayCurrency(dto, currency);
            return Json(new
            {
                success = true,
                data = dto,
                currency = currency.Currency,
                currencyCode = currency.CurrencyCode,
                currencySymbol = currency.CurrencySymbol
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchManualBookingCustomer(string phone)
        {
            if (!TryGetCurrentOwnerId(out _)) return UnauthorizedOwnerJson();
            var normalized = NormalizePhoneForSearch(phone);
            if (normalized.Length < 7)
                return Json(new { success = true, data = new OwnerManualBookingCustomerResultDto { Found = false } });

            var phoneSuffix = normalized.Length > 9 ? normalized.Substring(normalized.Length - 9) : normalized;
            var customer = await _context.Users.AsNoTracking()
                .Where(item =>
                    item.Role == "Customer" && item.AccountState == "Active" &&
                    item.PhoneNumber != null &&
                    item.PhoneNumber.Replace(" ", string.Empty).Replace("-", string.Empty)
                        .Replace("(", string.Empty).Replace(")", string.Empty)
                        .Replace(".", string.Empty).Replace("+", string.Empty).EndsWith(phoneSuffix))
                .OrderByDescending(item => item.LastLoginAt)
                .ThenByDescending(item => item.UserId)
                .Select(item => new
                {
                    item.UserId,
                    item.FullName,
                    item.Username,
                    item.PhoneNumber,
                    item.ContactInfo,
                    item.ProfileImage
                }).FirstOrDefaultAsync();

            var result = customer == null
                ? new OwnerManualBookingCustomerResultDto { Found = false }
                : new OwnerManualBookingCustomerResultDto
                {
                    Found = true,
                    CustomerId = customer.UserId,
                    FullName = FirstNonEmpty("عميل", customer.FullName, customer.Username),
                    PhoneNumber = customer.PhoneNumber?.Trim() ?? string.Empty,
                    ContactInfo = customer.ContactInfo?.Trim() ?? string.Empty,
                    ProfileImage = customer.ProfileImage?.Trim() ?? string.Empty
                };
            return Json(new { success = true, data = result });
        }

        [HttpGet]
        public async Task<IActionResult> GetManualBookingOptions(DateTime startDate, DateTime? endDate = null)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            if (!placeId.HasValue) return Json(new { success = false, message = "لا توجد منشأة حالية." });

            var currency = await GetOwnerCurrencyContextAsync(placeId.Value);
            var place = await LoadManualBookingPlaceAsync(placeId.Value, ownerId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة." });

            startDate = startDate.Date;
            var roomEnd = endDate?.Date ?? startDate.AddDays(1);
            if (roomEnd <= startDate) roomEnd = startDate.AddDays(1);

            var weeklyClosed = ManualContainsWeeklyClosedDate(place, startDate, roomEnd, true);
            var exceptionClosed = await _context.PlaceCalendarExceptions.AsNoTracking().AnyAsync(item =>
                item.PlaceId == place.PlaceId && item.ExceptionType == "Closed" &&
                item.ExceptionDate >= startDate && item.ExceptionDate < roomEnd);

            var rooms = await _context.Rooms.AsNoTracking()
                .Where(item => item.PlaceId == place.PlaceId && item.IsAvailable == true)
                .OrderBy(item => item.RoomType).ThenBy(item => item.RoomId).ToListAsync();

            var roomOptions = new List<OwnerManualRoomOptionDto>();
            foreach (var room in rooms)
            {
                var availability = await GetRoomAvailabilityAsync(place, room, startDate, roomEnd, DateTime.Now, true);
                roomOptions.Add(new OwnerManualRoomOptionDto
                {
                    RoomId = room.RoomId,
                    Name = BuildRoomName(room),
                    Description = room.Description?.Trim() ?? string.Empty,
                    Price = ToDisplayCurrency(room.Price, currency),
                    ActiveUnits = availability.ActiveUnits,
                    AvailableUnits = availability.AvailableUnits,
                    MaxAdults = Math.Max(0, room.MaxAdults),
                    MaxChildren = Math.Max(0, room.MaxChildren),
                    BedsCount = room.BedsCount,
                    BedType = room.BedType?.Trim() ?? string.Empty,
                    AreaSquareMeters = room.AreaSquareMeters,
                    AvailableRoomUnits = availability.UnitOptions.Where(unit => unit.IsSelectable).ToList(),
                    RoomUnits = availability.UnitOptions.ToList()
                });
            }

            var packages = await _context.PlacePackages.AsNoTracking()
                .Where(item => item.PlaceId == place.PlaceId && item.IsActive == true)
                .OrderBy(item => item.StartTime).ThenBy(item => item.PackageName).ToListAsync();

            var packageOptions = new List<OwnerManualPackageOptionDto>();
            foreach (var package in packages)
            {
                var packageClosed = ManualContainsWeeklyClosedDate(place, startDate, startDate, false) ||
                    await _context.PlaceCalendarExceptions.AsNoTracking().AnyAsync(item =>
                        item.PlaceId == place.PlaceId && item.ExceptionType == "Closed" && item.ExceptionDate == startDate);
                var shiftValid = ManualPackageFitsConfiguredShift(place, package);
                var conflict = !packageClosed && shiftValid &&
                    await ManualHasPackageBookingConflictAsync(place, package, startDate, DateTime.Now);
                var available = !packageClosed && shiftValid && !conflict;

                packageOptions.Add(new OwnerManualPackageOptionDto
                {
                    PackageId = package.PackageId,
                    Name = package.PackageName?.Trim() ?? "باقة",
                    TimeFrame = package.TimeFrame?.Trim() ?? string.Empty,
                    StartTime = package.StartTime,
                    EndTime = package.EndTime,
                    Price = ToDisplayCurrency(package.Price, currency),
                    Capacity = package.Capacity,
                    IsAvailable = available,
                    AvailabilityMessage = packageClosed ? "اليوم مغلق"
                        : !shiftValid ? "خارج الفترات التشغيلية"
                        : conflict ? "الفترة محجوزة أو ضمن وقت التجهيز" : "متاحة"
                });
            }

            var rawServices = await _context.PlaceAdditionalServices.AsNoTracking()
                .Where(item => item.PlaceId == place.PlaceId && item.IsActive == true)
                .OrderBy(item => item.ServiceName)
                .Select(item => new OwnerManualServiceOptionDto
                {
                    ServiceId = item.ServiceId,
                    ServiceName = item.ServiceName ?? "خدمة إضافية",
                    Description = item.Description ?? string.Empty,
                    Price = item.Price
                }).ToListAsync();

            foreach (var service in rawServices)
                service.Price = ToDisplayCurrency(service.Price, currency);

            var wallets = await GetOwnerManualWalletsAsync(ownerId);

            return Json(new
            {
                success = true,
                data = new OwnerManualBookingOptionsDto
                {
                    StartDate = startDate,
                    EndDate = roomEnd,
                    IsClosedByPolicy = weeklyClosed,
                    IsClosedByException = exceptionClosed,
                    ClosureMessage = weeklyClosed
                        ? "الفترة تتضمن يوم إغلاق أسبوعي حسب سياسة المنشأة."
                        : exceptionClosed ? "الفترة تتضمن يومًا مغلقًا من التقويم." : string.Empty,
                    Rooms = roomOptions,
                    Packages = packageOptions,
                    Services = rawServices,
                    Wallets = wallets
                },
                currency = currency.Currency,
                currencyCode = currency.CurrencyCode,
                currencySymbol = currency.CurrencySymbol
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetManualPaymentWallets()
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();

            var wallets = await GetOwnerManualWalletsAsync(ownerId);
            return Json(new
            {
                success = true,
                data = wallets
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewManualBooking([FromBody] OwnerManualBookingRequestDto request)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            var currency = await GetOwnerCurrencyContextAsync(placeId ?? 0);

            if (request != null && request.PaidAmount > 0m)
                request.PaidAmount = ToYer(request.PaidAmount, currency);

            var result = await BuildManualBookingQuoteAsync(request, ownerId, false);
            if (!result.PublicQuote.Success)
                return BadRequest(new { success = false, message = result.PublicQuote.Message });

            ApplyManualQuoteDisplayCurrency(result.PublicQuote, currency);
            return Json(new
            {
                success = true,
                data = result.PublicQuote,
                currency = currency.Currency,
                currencyCode = currency.CurrencyCode,
                currencySymbol = currency.CurrencySymbol
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateManualBooking([FromBody] OwnerManualBookingRequestDto request)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            var activePlaceId = await GetActiveOwnerPlaceIdAsync(ownerId);
            var currency = await GetOwnerCurrencyContextAsync(activePlaceId ?? 0);

            if (request != null && request.PaidAmount > 0m)
                request.PaidAmount = ToYer(request.PaidAmount, currency);

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var quote = await BuildManualBookingQuoteAsync(request, ownerId, true);
                if (!quote.PublicQuote.Success)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = quote.PublicQuote.Message });
                }

                var now = DateTime.Now;
                var isFreeBooking = quote.PublicQuote.TotalPrice <= 0m;
                var isCollectedMode = IsState(request.PaymentMode, "Collected");
                var paidAmount = isFreeBooking ? 0m
                    : isCollectedMode ? quote.PublicQuote.InitialPaymentAmount : 0m;
                var isCollected = !isFreeBooking && isCollectedMode && paidAmount > 0m;

                if (paidAmount > quote.PublicQuote.TotalPrice)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = "المبلغ المستلم لا يمكن أن يتجاوز إجمالي الحجز." });
                }
                if (isCollected && quote.PublicQuote.DepositAmount > 0m && paidAmount < quote.PublicQuote.DepositAmount)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new
                    {
                        success = false,
                        message = $"المبلغ المستلم أقل من العربون المطلوب ({DisplayMoney(quote.PublicQuote.DepositAmount, currency)})."
                    });
                }

                var requiresDeposit = !isFreeBooking && quote.PublicQuote.DepositAmount > 0m;
                var bookingState = requiresDeposit && !isCollected ? "AwaitingPayment" : "Confirmed";
                var paymentTimeout = Math.Max(1, quote.Place.PlacePolicy?.PaymentTimeout ?? 12);
                var timerExpiredAt = bookingState == "AwaitingPayment" ? now.AddHours(paymentTimeout) : (DateTime?)null;
                var paymentStatus = isFreeBooking ? "Paid"
                    : paidAmount <= 0m ? "Unpaid"
                    : paidAmount >= quote.PublicQuote.TotalPrice ? "Paid" : "PartiallyPaid";

                var customerId = quote.Customer?.UserId;
                var guestName = customerId.HasValue ? null : request.GuestName?.Trim();
                var guestPhone = customerId.HasValue ? null : request.GuestPhone?.Trim();
                var guestEmail = customerId.HasValue ? null : request.GuestEmail?.Trim();

                var booking = new Booking
                {
                    CustomerId = customerId,
                    PlaceId = quote.Place.PlaceId,
                    RoomId = quote.Room?.RoomId,
                    PackageId = quote.Package?.PackageId,
                    StartDate = quote.StartDate,
                    EndDate = quote.EndDate,
                    Quantity = quote.Quantity,
                    AdultsCount = Math.Max(0, request.AdultsCount),
                    ChildrenCount = Math.Max(0, request.ChildrenCount),
                    BookingType = string.IsNullOrWhiteSpace(request.BookingType) ? null : request.BookingType.Trim(),
                    SeatingSetup = string.IsNullOrWhiteSpace(request.SeatingSetup) ? null : request.SeatingSetup.Trim(),
                    TotalPrice = quote.PublicQuote.TotalPrice,
                    OriginalTotalPrice = quote.PublicQuote.OriginalTotalPrice,
                    DiscountAmount = quote.PublicQuote.DiscountAmount,
                    PromotionId = quote.Promotion.IsApplied ? quote.Promotion.PromotionId : null,
                    PromotionTitleSnapshot = quote.Promotion.IsApplied ? quote.Promotion.PromotionTitle : null,
                    PromotionCodeSnapshot = quote.Promotion.IsApplied && !string.IsNullOrWhiteSpace(quote.Promotion.PromotionCode)
                        ? quote.Promotion.PromotionCode : null,
                    DiscountTypeSnapshot = quote.Promotion.IsApplied ? quote.Promotion.DiscountType : null,
                    DiscountValueSnapshot = quote.Promotion.IsApplied ? quote.Promotion.DiscountValue : null,
                    BookingState = bookingState,
                    PaymentStatus = paymentStatus,
                    PaymentMethod = isFreeBooking ? "Promotion" : isCollected ? quote.ResolvedPaymentMethod : null,
                    RefundStatus = "NotApplicable",
                    TimerExpiredAt = timerExpiredAt,
                    LastStatusChangedAt = now,
                    AcceptedAt = now,
                    PaymentApprovedAt = isFreeBooking || isCollected ? now : null,
                    CreatedAt = now,
                    BookingSource = "OwnerManual",
                    ManualBookingChannel = NormalizeManualChannel(request.ManualBookingChannel),
                    CreatedByUserId = ownerId,
                    GuestName = guestName,
                    GuestPhone = guestPhone,
                    GuestEmail = guestEmail,
                    OwnerInternalNotes = BuildManualOwnerNotes(request)
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                if (quote.Room != null)
                {
                    foreach (var roomUnit in quote.CandidateRoomUnits.Take(quote.Quantity))
                    {
                        _context.BookingRoomUnits.Add(new BookingRoomUnit
                        {
                            BookingId = booking.BookingId,
                            RoomUnitId = roomUnit.RoomUnitId,
                            AssignmentStatus = bookingState == "Confirmed" ? "Confirmed" : "Held",
                            AssignedAt = now,
                            ReleasedAt = null
                        });
                    }
                }

                foreach (var service in quote.Services)
                {
                    if (!quote.ServiceQuantities.TryGetValue(service.ServiceId, out var serviceQuantity) || serviceQuantity <= 0)
                        continue;

                    _context.BookingAdditionalServices.Add(new BookingAdditionalService
                    {
                        BookingId = booking.BookingId,
                        ServiceId = service.ServiceId,
                        Quantity = serviceQuantity,
                        UnitPrice = quote.ComplimentaryServiceIds.Contains(service.ServiceId) ? 0m : service.Price
                    });
                }

                var remaining = Math.Round(Math.Max(0m, quote.PublicQuote.TotalPrice - paidAmount), 2);
                _context.FinancialRecords.Add(new FinancialRecord
                {
                    BookingId = booking.BookingId,
                    DepositAmount = quote.PublicQuote.DepositAmount,
                    PlatformCommission = 0m,
                    CommissionRateSnapshot = 0m,
                    CommissionBaseAmount = 0m,
                    CommissionStatus = "NotApplicable",
                    TransferReferenceNumber = isCollected && !string.IsNullOrWhiteSpace(request.PaymentReferenceNumber)
                        ? request.PaymentReferenceNumber.Trim() : null,
                    AmountPaidOnline = 0m,
                    AmountToPayAtProperty = remaining,
                    AmountCollectedAtProperty = paidAmount,
                    UpdatedAt = now
                });

                if (bookingState == "AwaitingPayment")
                {
                    AddStructuredNotification(
                        customerId,
                        "حجز مباشر جديد - مطلوب الدفع",
                        $"أنشأت المنشأة لك حجزًا مباشرًا رقم {booking.BookingId}. العربون المطلوب {quote.PublicQuote.DepositAmount:N0} ريال.",
                        NotificationTypes.PaymentRequired,
                        $"/BookingFlow/Payment/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }
                else
                {
                    AddStructuredNotification(
                        customerId,
                        "تم تأكيد الحجز",
                        isFreeBooking
                            ? $"تم إنشاء وتأكيد الحجز المباشر المجاني رقم {booking.BookingId} بواسطة المنشأة."
                            : $"تم إنشاء وتأكيد الحجز المباشر رقم {booking.BookingId} بواسطة المنشأة.",
                        NotificationTypes.BookingConfirmed,
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                }

                await _context.SaveChangesAsync();
                if (bookingState == "Confirmed" && quote.PublicQuote.PlatformCommission > 0m)
                {
                    var commissionResult = await _commissionFinanceService.HoldBookingCommissionAsync(
                        booking.BookingId, ownerId, HttpContext.RequestAborted);
                    if (!commissionResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = commissionResult.Message });
                    }
                }

                await transaction.CommitAsync();
                return Json(new
                {
                    success = true,
                    bookingId = booking.BookingId,
                    bookingState,
                    paymentStatus,
                    assignedRoomUnits = quote.Room == null
                        ? Array.Empty<object>()
                        : quote.CandidateRoomUnits.Take(quote.Quantity).Select(item => new
                        {
                            item.RoomUnitId,
                            item.RoomNumber,
                            item.FloorNumber,
                            item.FloorLabel
                        }).ToArray(),
                    totalPrice = ToDisplayCurrency(quote.PublicQuote.TotalPrice, currency),
                    depositAmount = ToDisplayCurrency(quote.PublicQuote.DepositAmount, currency),
                    paidAmount = ToDisplayCurrency(paidAmount, currency),
                    remainingAmount = ToDisplayCurrency(remaining, currency),
                    currency = currency.Currency,
                    currencyCode = currency.CurrencyCode,
                    currencySymbol = currency.CurrencySymbol,
                    message = isFreeBooking
                        ? "تم إنشاء الحجز المجاني وتثبيت التوفر بنجاح."
                        : bookingState == "Confirmed"
                            ? "تم إنشاء الحجز المباشر وتثبيت التوفر بنجاح."
                            : "تم إنشاء الحجز المباشر وحجز التوفر مؤقتًا بانتظار العربون."
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return ConcurrencyError();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "تعذر إنشاء الحجز اليدوي. لم يتم حفظ أي بيانات." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordManualBookingPayment([FromBody] OwnerManualPaymentInputDto input)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            if (input.BookingId <= 0 || input.PaidAmount <= 0m ||
                string.IsNullOrWhiteSpace(input.PaymentMethod) || input.PaymentMethod.Trim().Length > 30 ||
                (!string.IsNullOrWhiteSpace(input.PaymentReferenceNumber) && input.PaymentReferenceNumber.Trim().Length > 100))
            {
                return BadRequest(new { success = false, message = "بيانات الدفعة غير صحيحة." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var booking = await _context.Bookings
                    .Include(item => item.Place)
                    .Include(item => item.FinancialRecord)
                    .Include(item => item.BookingRoomUnits)
                    .FirstOrDefaultAsync(item =>
                        item.BookingId == input.BookingId &&
                        item.Place.OwnerId == ownerId &&
                        item.BookingSource == "OwnerManual");

                if (booking == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { success = false, message = "الحجز اليدوي غير موجود أو لا يتبع منشأتك." });
                }

                var currency = await GetOwnerCurrencyContextAsync(booking.PlaceId);
                var inputPaidAmountYer = Math.Round(Math.Max(0m, ToYer(input.PaidAmount, currency)), 2);
                var paymentResolution = await ResolveManualPaymentMethodAsync(
                    ownerId,
                    input.PaymentMethod,
                    input.WalletId);
                if (!paymentResolution.Success)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = paymentResolution.Message });
                }
                var isInitialPayment = IsState(booking.BookingState, "AwaitingPayment");
                var canCollectRemaining =
                    IsState(booking.BookingState, "Confirmed") ||
                    IsState(booking.BookingState, "Completed");

                if (!isInitialPayment && !canCollectRemaining)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = "حالة الحجز الحالية لا تسمح بتسجيل دفعة." });
                }

                if (!string.IsNullOrWhiteSpace(booking.RefundStatus) &&
                    !IsState(booking.RefundStatus, "NotApplicable"))
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = "لا يمكن تسجيل دفعة أثناء وجود عملية استرداد على الحجز." });
                }

                var now = DateTime.Now;
                if (isInitialPayment && booking.TimerExpiredAt.HasValue && booking.TimerExpiredAt.Value <= now)
                {
                    booking.BookingState = "Expired";
                    booking.PaymentStatus = "Expired";
                    booking.RefundStatus = "NotApplicable";
                    booking.RejectionReason = null;
                    ReleaseRoomAssignments(booking, now);
                    CloseBooking(booking, now, null, "System");
                    AddStructuredNotification(
                        booking.CustomerId,
                        "انتهت مهلة دفع الحجز",
                        $"انتهت مهلة دفع العربون للحجز رقم {booking.BookingId}، وتم تحرير التوفر تلقائيًا.",
                        NotificationTypes.BookingExpired,
                        $"/Places/BookingDetails/{booking.BookingId}",
                        NotificationReferenceTypes.Booking,
                        booking.BookingId);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return BadRequest(new
                    {
                        success = false,
                        expired = true,
                        bookingState = "Expired",
                        message = "انتهت مهلة دفع العربون لهذا الحجز وتم تحرير التوفر. لا يمكن تسجيل الدفعة من هذه العملية."
                    });
                }

                var financial = booking.FinancialRecord;
                if (financial == null)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = "السجل المالي للحجز غير موجود." });
                }

                var paidBefore = Math.Round(
                    Math.Max(0m, financial.AmountPaidOnline) +
                    Math.Max(0m, financial.AmountCollectedAtProperty),
                    2);
                var outstandingAmount = Math.Round(Math.Max(0m, booking.TotalPrice - paidBefore), 2);
                if (outstandingAmount <= 0m)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = "تم سداد كامل مبلغ هذا الحجز مسبقًا." });
                }

                var paidAmount = inputPaidAmountYer;
                if (isInitialPayment && paidAmount < financial.DepositAmount)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new
                    {
                        success = false,
                        message = $"المبلغ أقل من العربون المطلوب ({DisplayMoney(financial.DepositAmount, currency)})."
                    });
                }
                if (paidAmount > outstandingAmount)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new
                    {
                        success = false,
                        message = $"المبلغ يتجاوز المتبقي على الحجز ({DisplayMoney(outstandingAmount, currency)})."
                    });
                }

                var remainingAmount = Math.Round(Math.Max(0m, outstandingAmount - paidAmount), 2);
                if (isInitialPayment)
                {
                    booking.BookingState = "Confirmed";
                    booking.TimerExpiredAt = null;

                    foreach (var assignment in booking.BookingRoomUnits.Where(item => IsState(item.AssignmentStatus, "Held")))
                    {
                        assignment.AssignmentStatus = "Confirmed";
                        assignment.ReleasedAt = null;
                    }
                }

                booking.PaymentStatus = remainingAmount <= 0m ? "Paid" : "PartiallyPaid";
                booking.PaymentMethod = paymentResolution.StoredValue;
                booking.PaymentApprovedAt ??= now;
                booking.LastStatusChangedAt = now;

                financial.AmountCollectedAtProperty = Math.Round(
                    Math.Max(0m, financial.AmountCollectedAtProperty) + paidAmount,
                    2);
                financial.AmountToPayAtProperty = remainingAmount;
                if (!string.IsNullOrWhiteSpace(input.PaymentReferenceNumber))
                {
                    financial.TransferReferenceNumber = input.PaymentReferenceNumber.Trim();
                }
                financial.CollectedAt = now;
                financial.UpdatedAt = now;

                AddStructuredNotification(
                    booking.CustomerId,
                    remainingAmount <= 0m ? "تم سداد كامل مبلغ الحجز" : "تم تسجيل دفعة على الحجز",
                    remainingAmount <= 0m
                        ? $"تم تسجيل الدفعة الأخيرة للحجز رقم {booking.BookingId} وأصبح المبلغ مسددًا بالكامل."
                        : $"تم تسجيل دفعة جديدة للحجز رقم {booking.BookingId}. المتبقي {DisplayMoney(remainingAmount, currency)}.",
                    NotificationTypes.PaymentApproved,
                    $"/Places/BookingDetails/{booking.BookingId}",
                    NotificationReferenceTypes.Booking,
                    booking.BookingId);

                await _context.SaveChangesAsync();
                if (isInitialPayment && financial.PlatformCommission > 0m)
                {
                    var commissionResult = await _commissionFinanceService.HoldBookingCommissionAsync(
                        booking.BookingId, ownerId, HttpContext.RequestAborted);
                    if (!commissionResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = commissionResult.Message });
                    }
                }

                await transaction.CommitAsync();
                return Json(new
                {
                    success = true,
                    bookingId = booking.BookingId,
                    bookingState = booking.BookingState,
                    paymentStatus = booking.PaymentStatus,
                    paidAmount = ToDisplayCurrency(paidAmount, currency),
                    totalPaidAmount = ToDisplayCurrency(paidBefore + paidAmount, currency),
                    remainingAmount = ToDisplayCurrency(remainingAmount, currency),
                    currency = currency.Currency,
                    currencyCode = currency.CurrencyCode,
                    currencySymbol = currency.CurrencySymbol,
                    message = remainingAmount <= 0m
                        ? "تم تحصيل المبلغ المتبقي وسداد الحجز بالكامل."
                        : isInitialPayment
                            ? "تم تسجيل العربون وتأكيد الحجز بنجاح."
                            : "تم تسجيل الدفعة وتحديث المبلغ المتبقي بنجاح."
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "تعذر تسجيل الدفعة." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseCalendarRange([FromBody] OwnerCalendarRangeActionDto input)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            if (!placeId.HasValue) return BadRequest(new { success = false, message = "لا توجد منشأة حالية." });

            var start = input.StartDate.Date;
            var end = input.EndDate.Date;
            if (end < start) (start, end) = (end, start);
            if (start < DateTime.Today) return BadRequest(new { success = false, message = "لا يمكن إغلاق أيام سابقة." });
            if ((end - start).Days > 366) return BadRequest(new { success = false, message = "الحد الأقصى للإغلاق دفعة واحدة سنة كاملة." });

            var reason = string.IsNullOrWhiteSpace(input.Reason) ? "إغلاق من قبل المالك" : input.Reason.Trim();
            if (reason.Length > 255) return BadRequest(new { success = false, message = "سبب الإغلاق أطول من الحد المسموح." });

            await ExpireOverdueAwaitingPaymentsAsync(ownerId, placeId.Value);
            var referenceTime = DateTime.Now;
            var blockingBookings = await _context.Bookings.AsNoTracking()
                .Where(item =>
                    item.PlaceId == placeId.Value &&
                    BlockingBookingStates.Contains(item.BookingState ?? string.Empty) &&
                    (item.BookingState != "AwaitingPayment" || !item.TimerExpiredAt.HasValue || item.TimerExpiredAt > referenceTime) &&
                    item.StartDate.Date <= end &&
                    (item.EndDate.HasValue ? item.EndDate.Value.Date > start : item.StartDate.Date >= start))
                .OrderBy(item => item.StartDate)
                .Select(item => new { item.BookingId, item.BookingState, item.StartDate, item.EndDate })
                .Take(8).ToListAsync();

            if (blockingBookings.Count > 0)
            {
                var first = blockingBookings[0];
                var firstDate = first.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var extra = blockingBookings.Count > 1 ? $" ويوجد {blockingBookings.Count - 1} حجز نشط آخر ضمن الفترة." : string.Empty;
                return BadRequest(new
                {
                    success = false,
                    code = "ACTIVE_BOOKING_EXISTS",
                    message = start == end
                        ? $"لا يمكن إغلاق يوم {start:yyyy-MM-dd} لوجود حجز نشط عليه (#{first.BookingId}). ألغِ/عالِج الحجز أولاً ثم أغلق اليوم."
                        : $"لا يمكن إغلاق هذه الفترة لأنها تتضمن حجوزات نشطة. أول تعارض: الحجز #{first.BookingId} بتاريخ {firstDate}.{extra}",
                    bookingId = first.BookingId
                });
            }

            var existingExceptions = await _context.PlaceCalendarExceptions
                .Where(item => item.PlaceId == placeId.Value && item.ExceptionDate >= start && item.ExceptionDate <= end)
                .ToListAsync();
            var existingByDate = existingExceptions.GroupBy(item => item.ExceptionDate.Date)
                .ToDictionary(group => group.Key, group => group.First());
            var createdAt = DateTime.Now;

            for (var day = start; day <= end; day = day.AddDays(1))
            {
                if (!existingByDate.TryGetValue(day, out var existing))
                {
                    _context.PlaceCalendarExceptions.Add(new PlaceCalendarException
                    {
                        PlaceId = placeId.Value,
                        ExceptionDate = day,
                        ExceptionType = "Closed",
                        Reason = reason,
                        CustomPrice = null,
                        CreatedAt = createdAt
                    });
                    continue;
                }
                existing.ExceptionType = "Closed";
                existing.Reason = reason;
                existing.CustomPrice = null;
            }

            await _context.SaveChangesAsync();
            return Json(new
            {
                success = true,
                message = start == end
                    ? "تم إغلاق اليوم بنجاح بعد التأكد من عدم وجود حجز نشط عليه."
                    : $"تم إغلاق الفترة من {start:yyyy-MM-dd} إلى {end:yyyy-MM-dd} بعد التأكد من خلوها من الحجوزات النشطة."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReopenCalendarRange([FromBody] OwnerCalendarRangeActionDto input)
        {
            if (!TryGetCurrentOwnerId(out var ownerId)) return UnauthorizedOwnerJson();
            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            if (!placeId.HasValue) return BadRequest(new { success = false, message = "لا توجد منشأة حالية." });

            var start = input.StartDate.Date;
            var end = input.EndDate.Date;
            if (end < start) (start, end) = (end, start);
            if ((end - start).Days > 366) return BadRequest(new { success = false, message = "الحد الأقصى لإعادة الفتح دفعة واحدة سنة كاملة." });

            var exceptions = await _context.PlaceCalendarExceptions
                .Where(item => item.PlaceId == placeId.Value &&
                               item.ExceptionType == "Closed" &&
                               item.ExceptionDate >= start && item.ExceptionDate <= end)
                .ToListAsync();
            if (exceptions.Count > 0)
            {
                _context.PlaceCalendarExceptions.RemoveRange(exceptions);
                await _context.SaveChangesAsync();
            }

            return Json(new
            {
                success = true,
                removedCount = exceptions.Count,
                message = exceptions.Count == 0
                    ? "لا يوجد إغلاق استثنائي في الفترة المحددة."
                    : "تمت إعادة فتح الفترة للحجوزات الجديدة."
            });
        }

        private async Task<ManualBookingQuoteInternal> BuildManualBookingQuoteAsync(
            OwnerManualBookingRequestDto request,
            int ownerId,
            bool forCommit)
        {
            var failed = new ManualBookingQuoteInternal();
            if (request == null) { failed.PublicQuote.Message = "بيانات الحجز غير موجودة."; return failed; }
            if (request.StartDate == default) { failed.PublicQuote.Message = "حدد تاريخ بداية الحجز."; return failed; }
            if (request.RoomId.HasValue == request.PackageId.HasValue) { failed.PublicQuote.Message = "اختر غرفة أو باقة واحدة فقط."; return failed; }
            if (request.Quantity < 1 || request.Quantity > 100 || request.AdultsCount < 0 || request.AdultsCount > 1000 || request.ChildrenCount < 0 || request.ChildrenCount > 1000)
            { failed.PublicQuote.Message = "الكمية أو أعداد الأشخاص خارج الحدود المسموحة."; return failed; }
            if ((request.SelectedRoomUnitIds?.Count ?? 0) > 100 ||
                (request.SelectedRoomUnitIds?.Any(id => id <= 0) == true) ||
                (request.SelectedRoomUnitIds?.Distinct().Count() ?? 0) != (request.SelectedRoomUnitIds?.Count ?? 0))
            { failed.PublicQuote.Message = "اختيار أرقام الغرف الفعلية غير صحيح."; return failed; }
            if ((request.AdditionalServices?.Count ?? 0) > 100 ||
                (request.AdditionalServices?.Any(item => item.ServiceId <= 0 || item.Quantity < 1 || item.Quantity > 100) == true))
            { failed.PublicQuote.Message = "بيانات الخدمات الإضافية غير صحيحة."; return failed; }
            if (!string.IsNullOrWhiteSpace(request.BookingType) && request.BookingType.Trim().Length > 50)
            { failed.PublicQuote.Message = "نوع الحجز أطول من الحد المسموح."; return failed; }
            if (!string.IsNullOrWhiteSpace(request.SeatingSetup) && request.SeatingSetup.Trim().Length > 100)
            { failed.PublicQuote.Message = "وصف إعداد الجلسة أطول من الحد المسموح."; return failed; }
            if (!ManualBookingChannels.Contains(request.ManualBookingChannel ?? string.Empty))
            { failed.PublicQuote.Message = "قناة الحجز اليدوي غير صحيحة."; return failed; }

            if (IsState(request.ManualBookingChannel, "Other"))
            {
                var otherChannel = request.ManualBookingChannelOther?.Trim() ?? string.Empty;
                if (otherChannel.Length < 2) { failed.PublicQuote.Message = "اكتب قناة الحجز الأخرى."; return failed; }
                if (otherChannel.Length > 100) { failed.PublicQuote.Message = "وصف قناة الحجز الأخرى أطول من الحد المسموح."; return failed; }
            }

            if (!ManualPaymentModes.Contains(request.PaymentMode ?? string.Empty))
            { failed.PublicQuote.Message = "حالة الدفع المختارة غير صحيحة."; return failed; }

            if (IsState(request.PaymentMode, "Collected"))
            {
                if (string.IsNullOrWhiteSpace(request.PaymentMethod) || request.PaymentMethod.Trim().Length > 30)
                { failed.PublicQuote.Message = "حدد طريقة دفع صحيحة."; return failed; }
                if (!string.IsNullOrWhiteSpace(request.PaymentReferenceNumber) && request.PaymentReferenceNumber.Trim().Length > 100)
                { failed.PublicQuote.Message = "الرقم المرجعي أطول من الحد المسموح."; return failed; }
            }

            var placeId = await GetActiveOwnerPlaceIdAsync(ownerId);
            if (!placeId.HasValue) { failed.PublicQuote.Message = "لا توجد منشأة مختارة من الهيدر."; return failed; }
            var place = await LoadManualBookingPlaceAsync(placeId.Value, ownerId);
            if (place == null) { failed.PublicQuote.Message = "المنشأة المختارة غير متاحة."; return failed; }
            var displayCurrency = await GetOwnerCurrencyContextAsync(place.PlaceId);

            var paymentResolution = (Success: true, StoredValue: string.Empty, Message: string.Empty);
            if (IsState(request.PaymentMode, "Collected"))
            {
                paymentResolution = await ResolveManualPaymentMethodAsync(
                    ownerId,
                    request.PaymentMethod,
                    request.WalletId);
                if (!paymentResolution.Success)
                {
                    failed.PublicQuote.Message = paymentResolution.Message;
                    return failed;
                }
            }

            var startDate = request.StartDate.Date;
            var endDate = request.EndDate?.Date;
            var now = DateTime.Now;
            if (startDate < DateTime.Today) { failed.PublicQuote.Message = "لا يمكن إنشاء حجز في تاريخ سابق."; return failed; }

            var horizonMonths = Math.Max(0, place.PlacePolicy?.MaximumHorizonMonths ?? 0);
            if (horizonMonths > 0)
            {
                var lastDate = endDate ?? startDate;
                if (lastDate > DateTime.Today.AddMonths(horizonMonths))
                { failed.PublicQuote.Message = $"سياسة المنشأة تسمح بالحجز حتى {horizonMonths} أشهر مقدمًا فقط."; return failed; }
            }

            User? customer = null;
            if (request.CustomerId.HasValue && request.CustomerId.Value > 0)
            {
                customer = await _context.Users.FirstOrDefaultAsync(item =>
                    item.UserId == request.CustomerId.Value && item.Role == "Customer" && item.AccountState == "Active");
                if (customer == null) { failed.PublicQuote.Message = "حساب العميل المرتبط بالحجز غير موجود أو غير نشط."; return failed; }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.GuestName) || string.IsNullOrWhiteSpace(request.GuestPhone))
                { failed.PublicQuote.Message = "اكتب اسم الضيف ورقم هاتفه، أو اربط الحجز بحساب عميل."; return failed; }

                var guestName = request.GuestName.Trim();
                var guestPhone = request.GuestPhone.Trim();
                var guestEmail = request.GuestEmail?.Trim();
                if (guestName.Length < 2 || guestName.Length > 150 || guestPhone.Length < 5 || guestPhone.Length > 50)
                { failed.PublicQuote.Message = "بيانات الضيف غير مكتملة أو أطول من الحد المسموح."; return failed; }
                if (!string.IsNullOrWhiteSpace(guestEmail) &&
                    (guestEmail.Length > 150 || !guestEmail.Contains('@') || guestEmail.StartsWith('@') || guestEmail.EndsWith('@')))
                { failed.PublicQuote.Message = "البريد الإلكتروني للضيف غير صحيح."; return failed; }
            }

            var effectiveOwnerNotes = BuildManualOwnerNotes(request);
            if (!string.IsNullOrWhiteSpace(effectiveOwnerNotes) && effectiveOwnerNotes.Length > 1000)
            { failed.PublicQuote.Message = "الملاحظات الداخلية مع تفاصيل الحجز الأخرى أطول من الحد المسموح."; return failed; }
            if (!string.IsNullOrWhiteSpace(request.PromotionCode) && request.PromotionCode.Trim().Length > 50)
            { failed.PublicQuote.Message = "كود العرض أطول من الحد المسموح."; return failed; }

            var noticeError = ManualValidateMinimumNotice(place, startDate, null, now);
            if (!string.IsNullOrWhiteSpace(noticeError) && request.RoomId.HasValue)
            { failed.PublicQuote.Message = noticeError; return failed; }

            Room? room = null;
            PlacePackage? package = null;
            var quantity = Math.Max(1, request.Quantity);
            decimal baseAmount;
            var candidateUnits = new List<RoomUnit>();
            var nights = 1;

            if (request.RoomId.HasValue)
            {
                room = await _context.Rooms.FirstOrDefaultAsync(item =>
                    item.RoomId == request.RoomId.Value && item.PlaceId == place.PlaceId && item.IsAvailable == true);
                if (room == null) { failed.PublicQuote.Message = "نوع الغرفة المختار غير موجود أو غير متاح."; return failed; }
                if (!endDate.HasValue || endDate.Value <= startDate)
                { failed.PublicQuote.Message = "تاريخ الخروج يجب أن يكون بعد تاريخ الدخول."; return failed; }
                if (ManualContainsWeeklyClosedDate(place, startDate, endDate, true))
                { failed.PublicQuote.Message = "الفترة تتضمن يوم إغلاق أسبوعي حسب سياسة المنشأة."; return failed; }

                var containsClosed = await _context.PlaceCalendarExceptions.AsNoTracking().AnyAsync(item =>
                    item.PlaceId == place.PlaceId && item.ExceptionType == "Closed" &&
                    item.ExceptionDate >= startDate && item.ExceptionDate < endDate.Value);
                if (containsClosed) { failed.PublicQuote.Message = "الفترة تتضمن يومًا مغلقًا من التقويم."; return failed; }

                var availability = await GetRoomAvailabilityAsync(place, room, startDate, endDate.Value, now, true);
                if (quantity > availability.AvailableUnits)
                {
                    failed.PublicQuote.Message = availability.AvailableUnits <= 0
                        ? "لا توجد وحدات متاحة من هذا النوع خلال الفترة."
                        : $"المتاح من هذا النوع {availability.AvailableUnits} فقط.";
                    return failed;
                }

                var guests = Math.Max(0, request.AdultsCount) + Math.Max(0, request.ChildrenCount);
                if (guests <= 0) { failed.PublicQuote.Message = "حدد شخصًا واحدًا على الأقل."; return failed; }
                var roomCapacity = Math.Max(0, room.MaxAdults) + Math.Max(0, room.MaxChildren);
                var maximumAdults = Math.Max(0, room.MaxAdults) * quantity;
                var maximumChildren = Math.Max(0, room.MaxChildren) * quantity;
                if (maximumAdults > 0 && request.AdultsCount > maximumAdults)
                { failed.PublicQuote.Message = $"عدد البالغين أكبر من الحد المحدد للغرف ({maximumAdults})."; return failed; }
                if (maximumChildren >= 0 && request.ChildrenCount > maximumChildren)
                { failed.PublicQuote.Message = $"عدد الأطفال أكبر من الحد المحدد للغرف ({maximumChildren})."; return failed; }
                if (roomCapacity > 0 && guests > roomCapacity * quantity)
                { failed.PublicQuote.Message = $"عدد الأشخاص أكبر من سعة الغرف المطلوبة ({roomCapacity * quantity})."; return failed; }

                nights = Math.Max(1, (endDate.Value - startDate).Days);
                baseAmount = Math.Round(room.Price * nights * quantity, 2);
                var requestedUnitIds = (request.SelectedRoomUnitIds ?? new List<int>()).Distinct().ToList();
                if (requestedUnitIds.Count > 0)
                {
                    if (requestedUnitIds.Count != quantity)
                    { failed.PublicQuote.Message = "عدد الغرف الفعلية المختارة يجب أن يساوي الكمية المطلوبة."; return failed; }
                    var candidatesById = availability.Candidates.ToDictionary(item => item.RoomUnitId);
                    if (requestedUnitIds.Any(id => !candidatesById.ContainsKey(id)))
                    { failed.PublicQuote.Message = "إحدى الغرف الفعلية المختارة لم تعد متاحة خلال الفترة."; return failed; }
                    candidateUnits = requestedUnitIds.Select(id => candidatesById[id]).ToList();
                }
                else candidateUnits = availability.Candidates.Take(quantity).ToList();

                if (forCommit && candidateUnits.Count != quantity)
                { failed.PublicQuote.Message = "تغير توفر الغرف قبل الحفظ. أعد التحقق من التوفر."; return failed; }
            }
            else
            {
                quantity = 1;
                endDate = startDate;
                package = await _context.PlacePackages.FirstOrDefaultAsync(item =>
                    item.PackageId == request.PackageId!.Value && item.PlaceId == place.PlaceId && item.IsActive == true);
                if (package == null) { failed.PublicQuote.Message = "الباقة المختارة غير موجودة أو غير متاحة."; return failed; }
                if (ManualContainsWeeklyClosedDate(place, startDate, startDate, false))
                { failed.PublicQuote.Message = "هذا اليوم مغلق أسبوعيًا حسب سياسة المنشأة."; return failed; }
                if (!ManualPackageFitsConfiguredShift(place, package))
                { failed.PublicQuote.Message = "وقت الباقة خارج الفترات التشغيلية المفعلة."; return failed; }
                noticeError = ManualValidateMinimumNotice(place, startDate, package, now);
                if (!string.IsNullOrWhiteSpace(noticeError)) { failed.PublicQuote.Message = noticeError; return failed; }

                var closed = await _context.PlaceCalendarExceptions.AsNoTracking().AnyAsync(item =>
                    item.PlaceId == place.PlaceId && item.ExceptionType == "Closed" && item.ExceptionDate == startDate);
                if (closed) { failed.PublicQuote.Message = "هذا اليوم مغلق من التقويم."; return failed; }
                if (await ManualHasPackageBookingConflictAsync(place, package, startDate, now))
                { failed.PublicQuote.Message = "الفترة المختارة محجوزة أو تتعارض مع وقت التجهيز."; return failed; }

                var guests = Math.Max(0, request.AdultsCount) + Math.Max(0, request.ChildrenCount);
                if (guests > 0 && package.Capacity > 0 && guests > package.Capacity)
                { failed.PublicQuote.Message = $"عدد الأشخاص أكبر من سعة الباقة ({package.Capacity})."; return failed; }
                baseAmount = Math.Round(package.Price, 2);
            }

            var serviceQuantities = (request.AdditionalServices ?? new())
                .Where(item => item.ServiceId > 0 && item.Quantity > 0)
                .GroupBy(item => item.ServiceId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
            var serviceIds = serviceQuantities.Keys.ToList();
            var services = serviceIds.Count == 0 ? new List<PlaceAdditionalService>()
                : await _context.PlaceAdditionalServices.Where(item =>
                    serviceIds.Contains(item.ServiceId) && item.PlaceId == place.PlaceId && item.IsActive == true).ToListAsync();
            if (services.Count != serviceIds.Count) { failed.PublicQuote.Message = "إحدى الخدمات المختارة غير متاحة."; return failed; }

            var servicesTotal = Math.Round(services.Sum(item => item.Price * serviceQuantities[item.ServiceId]), 2);
            var cleaningFee = Math.Round(Math.Max(0m, place.PlacePolicy?.CleaningFee ?? 0m), 2);
            var taxPercentage = Math.Clamp(place.PlacePolicy?.TaxPercentage ?? 0m, 0m, 100m);
            var originalBeforeTax = Math.Round(baseAmount + servicesTotal + cleaningFee, 2);
            var originalTax = Math.Round(originalBeforeTax * taxPercentage / 100m, 2);
            var originalTotal = Math.Round(originalBeforeTax + originalTax, 2);
            var serviceTotals = services.ToDictionary(
                item => item.ServiceId,
                item => Math.Round(item.Price * serviceQuantities[item.ServiceId], 2));

            var promotion = await ResolveManualPromotionAsync(
                place.PlaceId,
                customer?.UserId,
                room?.RoomId,
                package?.PackageId,
                startDate,
                endDate,
                nights,
                quantity,
                Math.Max(0, request.AdultsCount) + Math.Max(0, request.ChildrenCount),
                baseAmount,
                servicesTotal,
                serviceTotals,
                serviceQuantities,
                request.PromotionId,
                request.PromotionCode);

            if ((request.PromotionId.HasValue || !string.IsNullOrWhiteSpace(request.PromotionCode)) && !promotion.IsApplied)
            {
                failed.PublicQuote.Message = string.IsNullOrWhiteSpace(promotion.Message)
                    ? "العرض لا ينطبق على هذا الحجز." : promotion.Message;
                return failed;
            }

            var complimentaryServiceIds = new HashSet<int>();
            if (promotion.IsApplied && promotion.AddsComplimentaryService &&
                promotion.RewardServiceId.HasValue && promotion.RewardServiceId.Value > 0)
            {
                var rewardService = await _context.PlaceAdditionalServices.FirstOrDefaultAsync(item =>
                    item.ServiceId == promotion.RewardServiceId.Value && item.PlaceId == place.PlaceId && item.IsActive == true);
                if (rewardService == null) { failed.PublicQuote.Message = "الخدمة المجانية لم تعد متاحة في هذه المنشأة."; return failed; }
                if (!serviceQuantities.ContainsKey(rewardService.ServiceId))
                {
                    serviceQuantities[rewardService.ServiceId] = Math.Max(1, promotion.RewardQuantity ?? 1);
                    services.Add(rewardService);
                    complimentaryServiceIds.Add(rewardService.ServiceId);
                }
            }

            var discount = promotion.IsApplied
                ? Math.Round(Math.Min(Math.Max(0m, promotion.DiscountAmount), Math.Max(0m, baseAmount + servicesTotal)), 2)
                : 0m;
            var discountedBeforeTax = Math.Round(Math.Max(0m, originalBeforeTax - discount), 2);
            var taxAmount = Math.Round(discountedBeforeTax * taxPercentage / 100m, 2);
            var totalPrice = Math.Round(Math.Max(0m, discountedBeforeTax + taxAmount), 2);
            var depositAmount = ManualCalculateDepositAmount(place, totalPrice);

            var collected = totalPrice > 0m && IsState(request.PaymentMode, "Collected");
            var initialPaymentAmount = collected
                ? Math.Round(depositAmount > 0m ? depositAmount : totalPrice, 2)
                : 0m;
            var normalizedPaidAmount = initialPaymentAmount;

            // الحجوزات التي ينشئها المالك يدويًا لا تُحتسب عليها عمولة منصة.
            const decimal commissionPercentage = 0m;
            const decimal platformCommission = 0m;
            var proposedState = depositAmount > 0m && !collected ? "AwaitingPayment" : "Confirmed";
            var paymentTimeout = Math.Max(1, place.PlacePolicy?.PaymentTimeout ?? 12);

            var publicQuote = new OwnerManualBookingQuoteDto
            {
                Success = true,
                Message = totalPrice <= 0m
                    ? "تم التحقق من الحجز والعرض المجاني والتوفر والسياسات."
                    : "تم التحقق من السعر والتوفر والسياسات.",
                CustomerId = customer?.UserId,
                CustomerDisplayName = customer == null
                    ? request.GuestName?.Trim() ?? "ضيف"
                    : FirstNonEmpty("عميل", customer.FullName, customer.Username),
                Quantity = quantity,
                Nights = nights,
                BaseAmount = baseAmount,
                ServicesTotal = servicesTotal,
                CleaningFee = cleaningFee,
                TaxPercentage = taxPercentage,
                TaxAmount = taxAmount,
                OriginalTotalPrice = originalTotal,
                DiscountAmount = discount,
                TotalPrice = totalPrice,
                DepositAmount = depositAmount,
                InitialPaymentAmount = initialPaymentAmount,
                RemainingAmount = Math.Round(Math.Max(0m, totalPrice - normalizedPaidAmount), 2),
                CommissionPercentage = commissionPercentage,
                PlatformCommission = platformCommission,
                PromotionId = promotion.IsApplied ? promotion.PromotionId : null,
                PromotionTitle = promotion.IsApplied ? promotion.PromotionTitle : string.Empty,
                PromotionCode = promotion.IsApplied ? promotion.PromotionCode : string.Empty,
                DiscountType = promotion.IsApplied ? promotion.DiscountType : string.Empty,
                DiscountValue = promotion.IsApplied ? promotion.DiscountValue : null,
                PromotionMessage = promotion.Message,
                CanConfirmImmediately = proposedState == "Confirmed",
                ProposedBookingState = proposedState,
                TimerExpiredAt = proposedState == "AwaitingPayment" ? now.AddHours(paymentTimeout) : null,
                AvailableUnits = candidateUnits.Select(item => new OwnerManualAssignedUnitPreviewDto
                {
                    RoomUnitId = item.RoomUnitId,
                    RoomNumber = item.RoomNumber?.Trim() ?? string.Empty,
                    FloorNumber = item.FloorNumber,
                    FloorLabel = item.FloorLabel?.Trim() ?? string.Empty
                }).ToList()
            };

            return new ManualBookingQuoteInternal
            {
                PublicQuote = publicQuote,
                Place = place,
                Room = room,
                Package = package,
                Customer = customer,
                Services = services,
                ServiceQuantities = serviceQuantities,
                ComplimentaryServiceIds = complimentaryServiceIds,
                CandidateRoomUnits = candidateUnits,
                Promotion = promotion,
                StartDate = startDate,
                EndDate = endDate,
                Quantity = quantity,
                ResolvedPaymentMethod = paymentResolution.StoredValue
            };
        }

        private async Task<Place?> LoadManualBookingPlaceAsync(int placeId, int ownerId)
        {
            return await _context.Places
                .Include(item => item.Category)
                .Include(item => item.PlacePolicy)
                .FirstOrDefaultAsync(item =>
                    item.PlaceId == placeId &&
                    item.OwnerId == ownerId &&
                    item.Status != "WaitOwner" &&
                    item.Status != "RejectedByOwner");
        }

        private sealed class RoomAvailabilityResult
        {
            public int ActiveUnits { get; set; }
            public int AvailableUnits { get; set; }
            public List<RoomUnit> Candidates { get; set; } = new();
            public List<OwnerManualRoomUnitOptionDto> UnitOptions { get; set; } = new();
        }

        private async Task<RoomAvailabilityResult> GetRoomAvailabilityAsync(
            Place place,
            Room room,
            DateTime startDate,
            DateTime endDate,
            DateTime referenceTime,
            bool includeCandidates = false)
        {
            var allUnits = await _context.RoomUnits.AsNoTracking()
                .Where(item => item.PlaceId == place.PlaceId && item.RoomId == room.RoomId)
                .OrderBy(item => item.FloorNumber)
                .ThenBy(item => item.RoomNumber)
                .ThenBy(item => item.RoomUnitId)
                .ToListAsync();

            if (allUnits.Count == 0) return new RoomAvailabilityResult();

            var activeUnits = allUnits.Where(item => item.IsAvailable == true).ToList();
            if (activeUnits.Count == 0)
            {
                return new RoomAvailabilityResult
                {
                    UnitOptions = allUnits.Select(unit => BuildManualRoomUnitOption(
                        unit, false, "Closed", "مغلقة / غير متاحة", null, null)).ToList()
                };
            }

            var unitIds = activeUnits.Select(item => item.RoomUnitId).ToList();
            var blockAdjacent = ManualRequiresAdjacentRoomBufferBlock(place);

            var assignmentRows = await _context.BookingRoomUnits.AsNoTracking()
                .Where(item =>
                    unitIds.Contains(item.RoomUnitId) &&
                    CalendarBlockingAssignmentStates.Contains(item.AssignmentStatus) &&
                    BlockingBookingStates.Contains(item.Booking.BookingState ?? string.Empty) &&
                    (item.Booking.BookingState != "AwaitingPayment" ||
                     !item.Booking.TimerExpiredAt.HasValue ||
                     item.Booking.TimerExpiredAt > referenceTime) &&
                    (blockAdjacent ? item.Booking.StartDate <= endDate : item.Booking.StartDate < endDate) &&
                    (blockAdjacent
                        ? (item.Booking.EndDate ?? item.Booking.StartDate.AddDays(1)) >= startDate
                        : (item.Booking.EndDate ?? item.Booking.StartDate.AddDays(1)) > startDate))
                .Select(item => new
                {
                    item.RoomUnitId,
                    item.BookingId,
                    item.Booking.BookingState,
                    item.Booking.TimerExpiredAt
                }).ToListAsync();

            var assignmentByUnit = assignmentRows
                .GroupBy(item => item.RoomUnitId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(item => ManualRoomUnitStatePriority(item.BookingState))
                        .ThenBy(item => item.BookingId).First());

            var legacyReservedQuantity = await _context.Bookings.AsNoTracking()
                .Where(item =>
                    item.RoomId == room.RoomId &&
                    !item.BookingRoomUnits.Any() &&
                    BlockingBookingStates.Contains(item.BookingState ?? string.Empty) &&
                    (item.BookingState != "AwaitingPayment" || !item.TimerExpiredAt.HasValue || item.TimerExpiredAt > referenceTime) &&
                    (blockAdjacent ? item.StartDate <= endDate : item.StartDate < endDate) &&
                    (blockAdjacent
                        ? (item.EndDate ?? item.StartDate.AddDays(1)) >= startDate
                        : (item.EndDate ?? item.StartDate.AddDays(1)) > startDate))
                .SumAsync(item => (int?)item.Quantity) ?? 0;

            var unassignedOperationalUnits = activeUnits
                .Where(item => !assignmentByUnit.ContainsKey(item.RoomUnitId)).ToList();
            var legacyBlockedIds = unassignedOperationalUnits
                .Take(Math.Max(0, legacyReservedQuantity))
                .Select(item => item.RoomUnitId).ToHashSet();
            var candidates = unassignedOperationalUnits
                .Where(item => !legacyBlockedIds.Contains(item.RoomUnitId)).ToList();

            var unitOptions = new List<OwnerManualRoomUnitOptionDto>();
            foreach (var unit in allUnits)
            {
                if (unit.IsAvailable != true)
                {
                    unitOptions.Add(BuildManualRoomUnitOption(
                        unit,
                        false,
                        "Closed",
                        string.IsNullOrWhiteSpace(unit.Notes) ? "مغلقة / غير متاحة" : "مغلقة / صيانة",
                        null,
                        null));
                    continue;
                }

                if (assignmentByUnit.TryGetValue(unit.RoomUnitId, out var assignment))
                {
                    var state = NormalizeManualRoomUnitAvailabilityState(assignment.BookingState);
                    unitOptions.Add(BuildManualRoomUnitOption(
                        unit, false, state.Code, state.Label, assignment.BookingId, assignment.TimerExpiredAt));
                    continue;
                }

                if (legacyBlockedIds.Contains(unit.RoomUnitId))
                {
                    unitOptions.Add(BuildManualRoomUnitOption(
                        unit, false, "LegacyReserved", "محجوزة بحجز سابق", null, null));
                    continue;
                }

                unitOptions.Add(BuildManualRoomUnitOption(
                    unit, true, "Available", "متاحة", null, null));
            }

            return new RoomAvailabilityResult
            {
                ActiveUnits = activeUnits.Count,
                AvailableUnits = candidates.Count,
                Candidates = includeCandidates ? candidates : new List<RoomUnit>(),
                UnitOptions = unitOptions
            };
        }

        private static OwnerManualRoomUnitOptionDto BuildManualRoomUnitOption(
            RoomUnit unit,
            bool isSelectable,
            string state,
            string label,
            int? bookingId,
            DateTime? timerExpiredAt)
        {
            return new OwnerManualRoomUnitOptionDto
            {
                RoomUnitId = unit.RoomUnitId,
                RoomNumber = unit.RoomNumber?.Trim() ?? string.Empty,
                FloorNumber = unit.FloorNumber,
                FloorLabel = unit.FloorLabel?.Trim() ?? string.Empty,
                IsSelectable = isSelectable,
                AvailabilityState = state,
                AvailabilityLabel = label,
                BlockingBookingId = bookingId,
                TimerExpiredAt = timerExpiredAt,
                Notes = unit.Notes?.Trim() ?? string.Empty
            };
        }

        private static int ManualRoomUnitStatePriority(string? state)
        {
            return state?.Trim() switch
            {
                "Confirmed" => 0,
                "PaymentReview" => 1,
                "AwaitingPayment" => 2,
                "Pending" => 3,
                _ => 9
            };
        }

        private static (string Code, string Label) NormalizeManualRoomUnitAvailabilityState(string? bookingState)
        {
            return bookingState?.Trim() switch
            {
                "Confirmed" => ("Occupied", "مشغولة"),
                "PaymentReview" => ("PaymentReview", "مراجعة دفع"),
                "AwaitingPayment" => ("Held", "محجوزة مؤقتًا"),
                "Pending" => ("Pending", "طلب قائم"),
                _ => ("Occupied", "غير متاحة")
            };
        }

        private async Task<int> GetOccupiedRoomUnitCountForDateAsync(int placeId, DateTime date)
        {
            date = date.Date;
            var nextDate = date.AddDays(1);
            var now = DateTime.Now;

            var unitIds = await _context.RoomUnits.AsNoTracking()
                .Where(item => item.PlaceId == placeId && item.IsAvailable == true)
                .Select(item => item.RoomUnitId).ToListAsync();
            if (unitIds.Count == 0) return 0;

            var assigned = await _context.BookingRoomUnits.AsNoTracking()
                .Where(item =>
                    unitIds.Contains(item.RoomUnitId) &&
                    CalendarBlockingAssignmentStates.Contains(item.AssignmentStatus) &&
                    BlockingBookingStates.Contains(item.Booking.BookingState ?? string.Empty) &&
                    (item.Booking.BookingState != "AwaitingPayment" || !item.Booking.TimerExpiredAt.HasValue || item.Booking.TimerExpiredAt > now) &&
                    item.Booking.StartDate < nextDate &&
                    (item.Booking.EndDate ?? item.Booking.StartDate.AddDays(1)) > date)
                .Select(item => item.RoomUnitId).Distinct().CountAsync();

            var legacy = await _context.Bookings.AsNoTracking()
                .Where(item =>
                    item.PlaceId == placeId && item.RoomId.HasValue && !item.BookingRoomUnits.Any() &&
                    BlockingBookingStates.Contains(item.BookingState ?? string.Empty) &&
                    (item.BookingState != "AwaitingPayment" || !item.TimerExpiredAt.HasValue || item.TimerExpiredAt > now) &&
                    item.StartDate < nextDate && (item.EndDate ?? item.StartDate.AddDays(1)) > date)
                .SumAsync(item => (int?)item.Quantity) ?? 0;

            return Math.Min(unitIds.Count, assigned + Math.Max(0, legacy));
        }

        private async Task<ManualPromotionResolution> ResolveManualPromotionAsync(
            int placeId,
            int? customerId,
            int? roomId,
            int? packageId,
            DateTime startDate,
            DateTime? endDate,
            int bookingDays,
            int bookingQuantity,
            int totalGuests,
            decimal baseAmount,
            decimal servicesTotal,
            IReadOnlyDictionary<int, decimal> serviceTotals,
            IReadOnlyDictionary<int, int> serviceQuantities,
            int? requestedPromotionId,
            string? requestedPromotionCode)
        {
            var result = new ManualPromotionResolution();
            var explicitPromotion = requestedPromotionId.HasValue || !string.IsNullOrWhiteSpace(requestedPromotionCode);
            var normalizedCode = requestedPromotionCode?.Trim() ?? string.Empty;
            var today = DateTime.Today;

            var promotions = await _context.Promotions.AsNoTracking()
                .Where(item =>
                    item.PlaceId == placeId &&
                    item.IsActive == true &&
                    (item.Status == "Active" || item.Status == "Scheduled") &&
                    item.StartDate <= today && item.EndDate >= today)
                .OrderBy(item => item.EndDate)
                .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .ThenByDescending(item => item.PromotionId)
                .ToListAsync();

            if (requestedPromotionId.HasValue && requestedPromotionId.Value > 0)
                promotions = promotions.Where(item => item.PromotionId == requestedPromotionId.Value).ToList();
            else if (!string.IsNullOrWhiteSpace(normalizedCode))
                promotions = promotions.Where(item =>
                    !string.IsNullOrWhiteSpace(item.PromoCode) &&
                    string.Equals(item.PromoCode.Trim(), normalizedCode, StringComparison.OrdinalIgnoreCase)).ToList();
            else
                promotions = promotions.Where(item => item.IsAutomatic == true).ToList();

            if (promotions.Count == 0)
            {
                if (explicitPromotion) result.Message = "العرض غير فعال أو لا يتبع المنشأة الحالية.";
                return result;
            }

            var ids = promotions.Select(item => item.PromotionId).ToArray();
            var usageCounts = await _context.Bookings.AsNoTracking()
                .Where(item =>
                    item.PromotionId.HasValue && ids.Contains(item.PromotionId.Value) &&
                    item.BookingState != "Cancelled" && item.BookingState != "Rejected" && item.BookingState != "Expired")
                .GroupBy(item => item.PromotionId!.Value)
                .Select(group => new { Id = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Id, item => item.Count);

            var customerUsage = customerId.HasValue && customerId.Value > 0
                ? await _context.Bookings.AsNoTracking()
                    .Where(item =>
                        item.CustomerId == customerId.Value && item.PromotionId.HasValue && ids.Contains(item.PromotionId.Value) &&
                        item.BookingState != "Cancelled" && item.BookingState != "Rejected" && item.BookingState != "Expired")
                    .GroupBy(item => item.PromotionId!.Value)
                    .Select(group => new { Id = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(item => item.Id, item => item.Count)
                : new Dictionary<int, int>();

            var candidates = new List<ManualPromotionResolution>();
            var minimumAmount = Math.Round(Math.Max(0m, baseAmount + servicesTotal), 2);
            string firstFailure = string.Empty;

            foreach (var promotion in promotions)
            {
                var evaluation = new ManualPromotionResolution
                {
                    PromotionTitle = promotion.PromotionTitle?.Trim() ?? "عرض خاص",
                    OfferMode = string.IsNullOrWhiteSpace(promotion.OfferMode) ? "Discount" : promotion.OfferMode.Trim(),
                    RewardType = promotion.RewardType?.Trim() ?? string.Empty,
                    RequiredQuantity = promotion.RequiredQuantity,
                    RewardQuantity = promotion.RewardQuantity,
                    IsRewardRepeatable = promotion.IsRewardRepeatable,
                    RewardServiceId = promotion.RewardServiceId,
                    SpecialPrice = promotion.SpecialPrice,
                    DiscountType = promotion.DiscountType?.Trim() ?? string.Empty,
                    DiscountValue = promotion.DiscountValue,
                    MaximumDiscountAmount = promotion.MaximumDiscountAmount
                };

                decimal eligible;
                string targetType;

                if (promotion.RoomId.HasValue)
                {
                    if (roomId != promotion.RoomId) { firstFailure = FirstNonEmpty(firstFailure, "العرض مخصص لغرفة أخرى."); continue; }
                    targetType = "Room";
                    eligible = baseAmount;
                }
                else if (promotion.PackageId.HasValue)
                {
                    if (packageId != promotion.PackageId) { firstFailure = FirstNonEmpty(firstFailure, "العرض مخصص لباقة أخرى."); continue; }
                    targetType = "Package";
                    eligible = baseAmount;
                }
                else if (promotion.ServiceId.HasValue)
                {
                    if (!serviceTotals.TryGetValue(promotion.ServiceId.Value, out eligible) || eligible <= 0m)
                    { firstFailure = FirstNonEmpty(firstFailure, "العرض مخصص لخدمة غير مختارة."); continue; }
                    targetType = "Service";
                }
                else
                {
                    targetType = "Place";
                    eligible = baseAmount + servicesTotal;
                }

                eligible = Math.Round(Math.Max(0m, eligible), 2);
                if (eligible <= 0m) { firstFailure = FirstNonEmpty(firstFailure, "لا توجد قيمة مؤهلة لتطبيق هذا العرض."); continue; }
                if (promotion.MinimumBookingAmount.HasValue && promotion.MinimumBookingAmount.Value > 0m && minimumAmount < promotion.MinimumBookingAmount.Value)
                { firstFailure = FirstNonEmpty(firstFailure, "قيمة الحجز أقل من الحد الأدنى للعرض."); continue; }
                if (promotion.MinimumNights.HasValue && promotion.MinimumNights.Value > 0 && bookingDays < promotion.MinimumNights.Value)
                { firstFailure = FirstNonEmpty(firstFailure, "عدد الليالي أقل من شرط العرض."); continue; }
                if (promotion.MinimumPeople.HasValue && promotion.MinimumPeople.Value > 0 && totalGuests < promotion.MinimumPeople.Value)
                { firstFailure = FirstNonEmpty(firstFailure, "عدد الأشخاص أقل من شرط العرض."); continue; }

                var lastStayDate = roomId.HasValue && endDate.HasValue && endDate.Value.Date > startDate.Date
                    ? endDate.Value.Date.AddDays(-1) : startDate.Date;
                if (promotion.StayStartDate.HasValue && startDate.Date < promotion.StayStartDate.Value.Date)
                { firstFailure = FirstNonEmpty(firstFailure, "تاريخ الحجز يسبق فترة العرض."); continue; }
                if (promotion.StayEndDate.HasValue && lastStayDate > promotion.StayEndDate.Value.Date)
                { firstFailure = FirstNonEmpty(firstFailure, "فترة الحجز تتجاوز فترة العرض."); continue; }

                if (!string.IsNullOrWhiteSpace(promotion.ApplicableDaysCsv))
                {
                    var allowed = promotion.ApplicableDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var allAllowed = true;
                    var exclusiveEnd = roomId.HasValue && endDate.HasValue ? endDate.Value.Date : startDate.Date.AddDays(1);
                    for (var day = startDate.Date; day < exclusiveEnd; day = day.AddDays(1))
                    {
                        if (!allowed.Contains(day.DayOfWeek.ToString())) { allAllowed = false; break; }
                    }
                    if (!allAllowed) { firstFailure = FirstNonEmpty(firstFailure, "العرض لا يشمل جميع أيام الحجز."); continue; }
                }

                usageCounts.TryGetValue(promotion.PromotionId, out var totalUsage);
                if (promotion.TotalUsageLimit.HasValue && promotion.TotalUsageLimit.Value > 0 && totalUsage >= promotion.TotalUsageLimit.Value)
                { firstFailure = FirstNonEmpty(firstFailure, "انتهى الحد المتاح للعرض."); continue; }

                if (promotion.PerCustomerUsageLimit.HasValue && promotion.PerCustomerUsageLimit.Value > 0)
                {
                    if (!customerId.HasValue || customerId.Value <= 0)
                    { firstFailure = FirstNonEmpty(firstFailure, "هذا العرض له حد استخدام لكل عميل؛ اربط الحجز بحساب العميل لتطبيقه."); continue; }
                    customerUsage.TryGetValue(promotion.PromotionId, out var usedByCustomer);
                    if (usedByCustomer >= promotion.PerCustomerUsageLimit.Value)
                    { firstFailure = FirstNonEmpty(firstFailure, "العميل وصل إلى حد استخدام هذا العرض."); continue; }
                }

                var requiresCode = promotion.IsAutomatic == false;
                var storedCode = promotion.PromoCode?.Trim() ?? string.Empty;
                if (requiresCode)
                {
                    if (string.IsNullOrWhiteSpace(storedCode)) { firstFailure = FirstNonEmpty(firstFailure, "العرض يحتاج كودًا لكن لم يتم إعداد كود صالح له."); continue; }
                    if (!string.Equals(storedCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
                    { firstFailure = FirstNonEmpty(firstFailure, "أدخل كود العرض الصحيح."); continue; }
                    evaluation.PromotionCode = storedCode;
                }

                var normalizedOfferMode = string.Equals(evaluation.OfferMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase)
                    ? "ConditionalReward" : "Discount";
                var normalizedRewardType = evaluation.RewardType;
                decimal rawDiscount = 0m;
                decimal benefitAmount = 0m;
                var canApply = true;

                if (normalizedOfferMode == "Discount")
                {
                    var discountType = string.Equals(promotion.DiscountType, "FixedAmount", StringComparison.OrdinalIgnoreCase)
                        ? "FixedAmount" : "Percentage";
                    evaluation.DiscountType = discountType;
                    evaluation.OfferSummary = discountType == "Percentage"
                        ? $"خصم {promotion.DiscountValue:0.##}%" : "خصم بمبلغ ثابت";
                    rawDiscount = discountType == "Percentage"
                        ? eligible * promotion.DiscountValue / 100m : promotion.DiscountValue;
                    if (promotion.MaximumDiscountAmount.HasValue && promotion.MaximumDiscountAmount.Value > 0m)
                        rawDiscount = Math.Min(rawDiscount, promotion.MaximumDiscountAmount.Value);
                    benefitAmount = rawDiscount;
                }
                else if (string.Equals(normalizedRewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "Percentage";
                    evaluation.OfferSummary = $"خصم {promotion.DiscountValue:0.##}% بعد تحقق شروط العرض";
                    rawDiscount = eligible * promotion.DiscountValue / 100m;
                    if (promotion.MaximumDiscountAmount.HasValue && promotion.MaximumDiscountAmount.Value > 0m)
                        rawDiscount = Math.Min(rawDiscount, promotion.MaximumDiscountAmount.Value);
                    benefitAmount = rawDiscount;
                }
                else if (string.Equals(normalizedRewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "FixedAmount";
                    evaluation.OfferSummary = "خصم بمبلغ ثابت بعد تحقق شروط العرض";
                    rawDiscount = promotion.DiscountValue;
                    if (promotion.MaximumDiscountAmount.HasValue && promotion.MaximumDiscountAmount.Value > 0m)
                        rawDiscount = Math.Min(rawDiscount, promotion.MaximumDiscountAmount.Value);
                    benefitAmount = rawDiscount;
                }
                else if (string.Equals(normalizedRewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "SpecialPrice";
                    if (!promotion.SpecialPrice.HasValue || promotion.SpecialPrice.Value < 0m)
                    {
                        evaluation.Message = "السعر الخاص في هذا العرض غير صالح.";
                        canApply = false;
                    }
                    else
                    {
                        decimal specialTotal;
                        if (roomId.HasValue && (promotion.RoomId.HasValue || targetType == "Place"))
                            specialTotal = promotion.SpecialPrice.Value * Math.Max(1, bookingDays) * Math.Max(1, bookingQuantity);
                        else if (packageId.HasValue && (promotion.PackageId.HasValue || targetType == "Place"))
                            specialTotal = promotion.SpecialPrice.Value * Math.Max(1, bookingQuantity);
                        else if (targetType == "Service" && promotion.ServiceId.HasValue)
                        {
                            var selectedQuantity = serviceQuantities.TryGetValue(promotion.ServiceId.Value, out var serviceQuantity)
                                ? Math.Max(1, serviceQuantity) : 1;
                            specialTotal = promotion.SpecialPrice.Value * selectedQuantity;
                        }
                        else specialTotal = promotion.SpecialPrice.Value;

                        rawDiscount = Math.Max(0m, eligible - specialTotal);
                        benefitAmount = rawDiscount;
                        evaluation.OfferSummary = "سعر خاص";
                    }
                }
                else if (string.Equals(normalizedRewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "FreeQuantity";
                    int totalUnits;
                    decimal amountPerUnit;
                    string unitLabel;
                    var isNightBasedReward = roomId.HasValue;
                    var requiredQuantity = isNightBasedReward && promotion.MinimumNights.HasValue && promotion.MinimumNights.Value > 0
                        ? promotion.MinimumNights.Value
                        : promotion.RequiredQuantity ?? promotion.MinimumNights ?? 1;
                    requiredQuantity = Math.Max(1, requiredQuantity);
                    var rewardQuantity = Math.Max(1, promotion.RewardQuantity ?? 1);

                    if (isNightBasedReward)
                    {
                        totalUnits = Math.Max(1, bookingDays);
                        amountPerUnit = baseAmount / Math.Max(1, bookingDays);
                        unitLabel = "ليلة";
                    }
                    else if (targetType == "Service" && promotion.ServiceId.HasValue)
                    {
                        var serviceId = promotion.ServiceId.Value;
                        totalUnits = serviceQuantities.TryGetValue(serviceId, out var serviceQuantity) ? Math.Max(0, serviceQuantity) : 0;
                        amountPerUnit = totalUnits > 0 && serviceTotals.TryGetValue(serviceId, out var serviceTotal)
                            ? serviceTotal / totalUnits : 0m;
                        unitLabel = "وحدة";
                    }
                    else
                    {
                        totalUnits = Math.Max(1, bookingQuantity);
                        amountPerUnit = baseAmount / Math.Max(1, bookingQuantity);
                        unitLabel = "وحدة";
                    }

                    var completedRequirements = requiredQuantity > 0 ? totalUnits / requiredQuantity : 0;
                    var repeatCount = promotion.IsRewardRepeatable ? completedRequirements : completedRequirements > 0 ? 1 : 0;
                    var calculatedFreeUnits = repeatCount * rewardQuantity;
                    var freeUnits = Math.Min(Math.Max(0, totalUnits), Math.Max(0, calculatedFreeUnits));

                    if (freeUnits <= 0 || amountPerUnit <= 0m)
                    {
                        evaluation.Message = $"للاستفادة من العرض اختر {requiredQuantity} {unitLabel} على الأقل؛ عند تحقق الشرط تحصل على {rewardQuantity} مجانًا.";
                        canApply = false;
                    }
                    else
                    {
                        rawDiscount = Math.Min(eligible, amountPerUnit * freeUnits);
                        benefitAmount = rawDiscount;
                        evaluation.RequiredQuantity = requiredQuantity;
                        evaluation.RewardQuantity = rewardQuantity;
                        evaluation.IsRewardRepeatable = promotion.IsRewardRepeatable;
                        evaluation.AppliedFreeQuantity = freeUnits;
                        evaluation.RepeatCount = repeatCount;
                        evaluation.OfferSummary = unitLabel == "ليلة"
                            ? promotion.IsRewardRepeatable
                                ? $"كل {requiredQuantity} ليلة تمنح {rewardQuantity} ليلة مجانًا — تم تطبيقها {repeatCount} مرة"
                                : $"احجز {requiredQuantity} ليلة واحصل على {rewardQuantity} ليلة مجانًا — مرة واحدة للحجز"
                            : promotion.IsRewardRepeatable
                                ? $"كل {requiredQuantity} وحدة تمنح {rewardQuantity} مجانًا — تم تطبيقها {repeatCount} مرة"
                                : $"احجز {requiredQuantity} واحصل على {rewardQuantity} مجانًا — مرة واحدة للحجز";
                    }
                }
                else if (string.Equals(normalizedRewardType, "FreeService", StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "FreeService";
                    var rewardServiceId = promotion.RewardServiceId;
                    if (!rewardServiceId.HasValue || rewardServiceId.Value <= 0)
                    { evaluation.Message = "الخدمة المجانية في هذا العرض غير محددة."; canApply = false; }
                    else
                    {
                        var rewardService = await _context.PlaceAdditionalServices.AsNoTracking().FirstOrDefaultAsync(service =>
                            service.ServiceId == rewardServiceId.Value && service.PlaceId == placeId && service.IsActive == true);
                        if (rewardService == null)
                        { evaluation.Message = "الخدمة المجانية لم تعد متاحة في هذه المنشأة."; canApply = false; }
                        else
                        {
                            var rewardQuantity = Math.Max(1, promotion.RewardQuantity ?? 1);
                            evaluation.RewardServiceId = rewardService.ServiceId;
                            evaluation.RewardServiceName = rewardService.ServiceName?.Trim() ?? "خدمة إضافية";
                            evaluation.RewardQuantity = rewardQuantity;
                            evaluation.AddsComplimentaryService = !serviceTotals.ContainsKey(rewardService.ServiceId);
                            var rewardValue = Math.Max(0m, rewardService.Price) * rewardQuantity;
                            if (serviceTotals.TryGetValue(rewardService.ServiceId, out var selectedServiceTotal) && selectedServiceTotal > 0m)
                                rawDiscount = Math.Min(selectedServiceTotal, rewardValue);
                            benefitAmount = rewardValue;
                            evaluation.OfferSummary = rewardQuantity > 1
                                ? $"{rewardQuantity} × {evaluation.RewardServiceName} مجانًا"
                                : $"{evaluation.RewardServiceName} مجانًا";
                        }
                    }
                }
                else if (string.Equals(normalizedRewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "ServiceSpecialPrice";
                    var rewardServiceId = promotion.RewardServiceId ?? promotion.ServiceId;
                    if (!rewardServiceId.HasValue || !promotion.SpecialPrice.HasValue || promotion.SpecialPrice.Value < 0m)
                    { evaluation.Message = "بيانات سعر الخدمة الخاص غير مكتملة."; canApply = false; }
                    else if (!serviceTotals.TryGetValue(rewardServiceId.Value, out var selectedServiceTotal) || selectedServiceTotal <= 0m)
                    { evaluation.Message = "أضف الخدمة المشمولة بالعرض أولًا للاستفادة من سعرها الخاص."; canApply = false; }
                    else
                    {
                        var rewardService = await _context.PlaceAdditionalServices.AsNoTracking().FirstOrDefaultAsync(service =>
                            service.ServiceId == rewardServiceId.Value && service.PlaceId == placeId && service.IsActive == true);
                        if (rewardService == null)
                        { evaluation.Message = "الخدمة ذات السعر الخاص لم تعد متاحة."; canApply = false; }
                        else
                        {
                            var selectedQuantity = serviceQuantities.TryGetValue(rewardServiceId.Value, out var serviceQuantity)
                                ? Math.Max(1, serviceQuantity) : 1;
                            var specialTotal = promotion.SpecialPrice.Value * selectedQuantity;
                            rawDiscount = Math.Max(0m, selectedServiceTotal - specialTotal);
                            benefitAmount = rawDiscount;
                            evaluation.RewardServiceId = rewardService.ServiceId;
                            evaluation.RewardServiceName = rewardService.ServiceName?.Trim() ?? "خدمة إضافية";
                            evaluation.OfferSummary = $"{evaluation.RewardServiceName} بسعر خاص";
                        }
                    }
                }
                else
                {
                    evaluation.Message = "نوع المكافأة في هذا العرض غير مدعوم.";
                    canApply = false;
                }

                if (!canApply) { firstFailure = FirstNonEmpty(firstFailure, evaluation.Message); continue; }

                decimal discountCap = eligible;
                if (string.Equals(normalizedRewardType, "FreeService", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedRewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase))
                {
                    var rewardServiceIdForCap = evaluation.RewardServiceId ?? promotion.RewardServiceId ?? promotion.ServiceId;
                    if (rewardServiceIdForCap.HasValue &&
                        serviceTotals.TryGetValue(rewardServiceIdForCap.Value, out var rewardServiceSelectedTotal) &&
                        rewardServiceSelectedTotal > 0m)
                        discountCap = rewardServiceSelectedTotal;
                    else if (string.Equals(normalizedRewardType, "FreeService", StringComparison.OrdinalIgnoreCase) &&
                             evaluation.AddsComplimentaryService)
                        discountCap = 0m;
                }

                rawDiscount = Math.Min(Math.Max(0m, discountCap), Math.Max(0m, rawDiscount));
                evaluation.DiscountAmount = Math.Round(rawDiscount, 2);
                evaluation.BenefitAmount = Math.Round(Math.Max(benefitAmount, evaluation.DiscountAmount), 2);
                var isNonMonetaryReward = string.Equals(normalizedRewardType, "FreeService", StringComparison.OrdinalIgnoreCase) &&
                                          evaluation.AddsComplimentaryService;

                if (evaluation.DiscountAmount <= 0m && !isNonMonetaryReward)
                {
                    evaluation.Message =
                        string.Equals(normalizedRewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalizedRewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase)
                            ? "السعر الخاص لا يقل عن السعر الحالي، لذلك لا يوجد توفير قابل للتطبيق."
                            : "قيمة التوفير المحسوبة لهذا العرض تساوي صفرًا.";
                    firstFailure = FirstNonEmpty(firstFailure, evaluation.Message);
                    continue;
                }

                evaluation.PromotionId = promotion.PromotionId;
                evaluation.IsApplied = true;
                evaluation.Message = string.IsNullOrWhiteSpace(evaluation.OfferSummary)
                    ? "تم تطبيق أفضل عرض واحد صالح بدون تكديس."
                    : $"تم تطبيق العرض: {evaluation.OfferSummary}.";
                candidates.Add(evaluation);
            }

            var selected = candidates
                .OrderByDescending(item => item.BenefitAmount)
                .ThenByDescending(item => item.DiscountAmount)
                .ThenByDescending(item => item.DiscountValue)
                .FirstOrDefault();
            if (selected != null) return selected;

            result.Message = firstFailure;
            return result;
        }

        private static decimal ManualCalculateDepositAmount(Place place, decimal totalPrice)
        {
            if (totalPrice <= 0m) return 0m;
            var json = place.PlacePolicy?.DepositPoliciesJson;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var policy in document.RootElement.EnumerateArray())
                        {
                            if (!policy.TryGetProperty("val", out var valueElement)) continue;
                            var raw = valueElement.ValueKind == JsonValueKind.String
                                ? valueElement.GetString() : valueElement.GetRawText();
                            raw = NormalizeManualDigits(raw).Trim().Replace("٫", ".").Replace(",", ".");
                            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0m)
                                continue;

                            var type = policy.TryGetProperty("type", out var typeElement)
                                ? typeElement.GetString()?.Trim() ?? string.Empty : string.Empty;
                            var percentage = type.Contains("%") ||
                                             type.Contains("نسبة", StringComparison.OrdinalIgnoreCase) ||
                                             type.Contains("percent", StringComparison.OrdinalIgnoreCase);
                            var amount = percentage ? totalPrice * value / 100m : value;
                            return Math.Round(Math.Min(totalPrice, Math.Max(0m, amount)), 2);
                        }
                    }
                }
                catch (JsonException) { }
            }

            if (place.IsDepositRequired && place.DepositAmount > 0m)
                return Math.Round(Math.Min(place.DepositAmount, totalPrice), 2);
            return 0m;
        }

        private static bool ManualContainsWeeklyClosedDate(
            Place place,
            DateTime startDate,
            DateTime? endDate,
            bool isRoomBooking)
        {
            var closedDays = ReadWeeklyClosedDays(place);
            if (closedDays.Count == 0) return false;
            var first = startDate.Date;
            var exclusiveEnd = isRoomBooking && endDate.HasValue && endDate.Value.Date > first
                ? endDate.Value.Date : first.AddDays(1);
            for (var day = first; day < exclusiveEnd; day = day.AddDays(1))
                if (closedDays.Contains(day.DayOfWeek.ToString())) return true;
            return false;
        }

        private static HashSet<string> ReadWeeklyClosedDays(Place place)
        {
            var csv = place.PlacePolicy?.ClosedDaysCsv?.Trim();
            if (string.IsNullOrWhiteSpace(csv)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static bool ManualTryParseClock(string? value, out TimeSpan clock)
        {
            clock = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            return TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out clock) ||
                   TimeSpan.TryParse(value.Trim(), CultureInfo.CurrentCulture, out clock);
        }

        private static TimeSpan ManualGetCheckIn(Place place)
        {
            if (ManualTryParseClock(place.PlacePolicy?.CheckInTime, out var policy)) return policy;
            if (ManualTryParseClock(place.CheckInTime, out var legacy)) return legacy;
            return TimeSpan.FromHours(14);
        }

        private static TimeSpan ManualGetCheckOut(Place place)
        {
            if (ManualTryParseClock(place.PlacePolicy?.CheckOutTime, out var policy)) return policy;
            if (ManualTryParseClock(place.CheckOutTime, out var legacy)) return legacy;
            return TimeSpan.FromHours(12);
        }

        private sealed class ManualShiftWindow
        {
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
        }

        private static List<ManualShiftWindow> ManualReadShiftWindows(string? json)
        {
            var result = new List<ManualShiftWindow>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array) return result;
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    string? from = null;
                    string? to = null;
                    foreach (var property in item.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.String) continue;
                        if (property.Name.Equals("from", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Equals("start", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Equals("startTime", StringComparison.OrdinalIgnoreCase))
                            from = property.Value.GetString();
                        if (property.Name.Equals("to", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Equals("end", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Equals("endTime", StringComparison.OrdinalIgnoreCase))
                            to = property.Value.GetString();
                    }
                    if (ManualTryParseClock(from, out var start) && ManualTryParseClock(to, out var end) && start != end)
                        result.Add(new ManualShiftWindow { Start = start, End = end });
                }
            }
            catch (JsonException) { }
            return result;
        }

        private static bool ManualPackageFitsConfiguredShift(Place place, PlacePackage package)
        {
            if (!IsState(place.PlacePolicy?.OperationSystemMode, "Shifts")) return true;
            if (!package.StartTime.HasValue || !package.EndTime.HasValue || package.StartTime.Value == package.EndTime.Value)
                return false;

            var packageStart = package.StartTime.Value.TotalMinutes;
            var packageEnd = package.EndTime.Value.TotalMinutes;
            if (package.EndTime.Value <= package.StartTime.Value) packageEnd += TimeSpan.FromDays(1).TotalMinutes;

            foreach (var shift in ManualReadShiftWindows(place.PlacePolicy?.ShiftsJson))
            {
                var shiftStart = shift.Start.TotalMinutes;
                var shiftEnd = shift.End.TotalMinutes;
                if (shift.End <= shift.Start) shiftEnd += TimeSpan.FromDays(1).TotalMinutes;
                if (packageStart >= shiftStart && packageEnd <= shiftEnd) return true;
            }
            return false;
        }

        private static (DateTime Start, DateTime End) ManualPackageInterval(DateTime date, PlacePackage package)
        {
            var start = date.Date.Add(package.StartTime ?? TimeSpan.Zero);
            var end = date.Date.Add(package.EndTime ?? package.StartTime ?? TimeSpan.Zero);
            if (end <= start) end = end.AddDays(1);
            return (start, end);
        }

        private async Task<bool> ManualHasPackageBookingConflictAsync(
            Place place,
            PlacePackage requestedPackage,
            DateTime requestedDate,
            DateTime referenceTime)
        {
            var bookings = await _context.Bookings.AsNoTracking()
                .Include(item => item.Package)
                .Where(item =>
                    item.PlaceId == place.PlaceId && item.PackageId.HasValue &&
                    BlockingBookingStates.Contains(item.BookingState ?? string.Empty) &&
                    (item.BookingState != "AwaitingPayment" || !item.TimerExpiredAt.HasValue || item.TimerExpiredAt > referenceTime) &&
                    item.StartDate.Date >= requestedDate.Date.AddDays(-1) &&
                    item.StartDate.Date <= requestedDate.Date.AddDays(1))
                .ToListAsync();

            if (bookings.Count == 0) return false;
            if (!IsState(place.PlacePolicy?.OperationSystemMode, "Shifts"))
                return bookings.Any(item => item.StartDate.Date == requestedDate.Date);

            var buffer = TimeSpan.FromHours(Math.Clamp(place.PlacePolicy?.BufferTimeHours ?? 0, 0, 24));
            var requested = ManualPackageInterval(requestedDate, requestedPackage);
            foreach (var booking in bookings)
            {
                if (booking.Package == null) return true;
                var existing = ManualPackageInterval(booking.StartDate, booking.Package);
                if (existing.Start < requested.End.Add(buffer) && existing.End.Add(buffer) > requested.Start) return true;
            }
            return false;
        }

        private static bool ManualRequiresAdjacentRoomBufferBlock(Place place)
        {
            var bufferHours = Math.Clamp(place.PlacePolicy?.BufferTimeHours ?? 0, 0, 24);
            if (bufferHours <= 0) return false;
            var checkIn = ManualGetCheckIn(place);
            var checkOut = ManualGetCheckOut(place);
            var checkOutAt = DateTime.Today.Add(checkOut);
            var nextCheckInAt = DateTime.Today.Add(checkIn);
            if (nextCheckInAt < checkOutAt) nextCheckInAt = nextCheckInAt.AddDays(1);
            var naturalGap = Math.Max(0d, (nextCheckInAt - checkOutAt).TotalHours);
            return bufferHours > naturalGap;
        }

        private static string? ManualValidateMinimumNotice(
            Place place,
            DateTime startDate,
            PlacePackage? package,
            DateTime now)
        {
            var hours = Math.Max(0, place.PlacePolicy?.MinimumNoticeHours ?? 0);
            if (hours <= 0) return null;
            var startAt = package?.StartTime.HasValue == true
                ? startDate.Date.Add(package.StartTime.Value)
                : startDate.Date.Add(ManualGetCheckIn(place));
            return startAt < now.AddHours(hours)
                ? $"يجب إنشاء الحجز قبل الموعد بـ {hours} ساعة على الأقل حسب سياسة المنشأة."
                : null;
        }

        private static string BuildCalendarBookingStateReason(Booking booking)
        {
            var state = booking.BookingState?.Trim() ?? string.Empty;
            if (IsState(state, "Cancelled")) return FirstNonEmpty(string.Empty, booking.OwnerCancellationReason, booking.CancellationReason);
            if (IsState(state, "Rejected")) return booking.RejectionReason?.Trim() ?? string.Empty;
            if (IsState(state, "Expired")) return "انتهت مهلة دفع العربون.";
            if (IsState(state, "NoShow")) return "تم تسجيل عدم حضور العميل.";
            if (IsState(state, "Completed")) return "تم إكمال الحجز وإغلاقه بنجاح.";
            return string.Empty;
        }

        private static string GetCalendarStateCss(string state, bool isManual)
        {
            if (IsState(state, "Confirmed")) return "yg-event-confirmed";
            if (IsState(state, "AwaitingPayment")) return "yg-event-awaiting";
            if (IsState(state, "PaymentReview")) return "yg-event-review";
            if (IsState(state, "Pending")) return "yg-event-pending";
            if (IsState(state, "Completed")) return "yg-event-completed";
            if (IsState(state, "NoShow")) return "yg-event-noshow";
            if (IsState(state, "Cancelled")) return "yg-event-cancelled";
            if (IsState(state, "Rejected")) return "yg-event-rejected";
            if (IsState(state, "Expired")) return "yg-event-expired";
            return "yg-event-neutral";
        }

        private static bool IsCalendarHistoricalState(string? state)
        {
            if (string.IsNullOrWhiteSpace(state)) return false;
            return CalendarHistoricalBookingStates.Contains(state.Trim());
        }

        private static (string Key, string Label) ResolveManualBookingTemplate(
            Place place,
            bool supportsRooms,
            bool supportsPackages,
            int totalActiveUnits)
        {
            var bookingSystemId = place.Category?.BookingSystemId ?? 0;
            if (bookingSystemId == 1)
            {
                if (supportsRooms && totalActiveUnits <= 1) return ("SingleUnit", "وحدة إقامة مستقلة");
                return ("Lodging", "فندق / غرف وأجنحة");
            }
            if (bookingSystemId == 2) return ("Chalet", "شاليه / مزرعة وباقات");
            if (bookingSystemId == 3 || bookingSystemId == 4) return ("Venue", "قاعة / صالة وباقات");

            var category = place.Category?.CategoryName?.Trim() ?? string.Empty;
            var normalized = category.ToLowerInvariant();
            var venueLike = normalized.Contains("قاعة") || normalized.Contains("صالة") || normalized.Contains("ملعب") || normalized.Contains("نادي");
            var chaletLike = normalized.Contains("شاليه") || normalized.Contains("مزرعة") || normalized.Contains("استراحة") || normalized.Contains("فيلا");
            if (venueLike && supportsPackages) return ("Venue", "قاعة / صالة وباقات");
            if (chaletLike && supportsPackages) return ("Chalet", "شاليه / مزرعة وباقات");
            if (supportsRooms && supportsPackages) return ("Hybrid", "إقامة وباقات");
            if (supportsRooms) return totalActiveUnits <= 1
                ? ("SingleUnit", "وحدة إقامة مستقلة")
                : ("Lodging", "فندق / غرف وأجنحة");
            if (supportsPackages) return ("Venue", "باقات وفترات");
            return ("Generic", "حجز عام");
        }

        private static string? BuildManualOwnerNotes(OwnerManualBookingRequestDto request)
        {
            var lines = new List<string>();
            if (IsState(request.ManualBookingChannel, "Other") && !string.IsNullOrWhiteSpace(request.ManualBookingChannelOther))
                lines.Add($"قناة الحجز الأخرى: {request.ManualBookingChannelOther.Trim()}");
            if (!string.IsNullOrWhiteSpace(request.OwnerInternalNotes)) lines.Add(request.OwnerInternalNotes.Trim());
            return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
        }

        private static string NormalizeManualChannel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Phone";
            var normalized = value.Trim();
            return ManualBookingChannels.Contains(normalized) ? normalized : "Other";
        }

        private static string NormalizePhoneForSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var normalized = NormalizeManualDigits(value);
            return new string(normalized.Where(character => char.IsDigit(character) || character == '+').ToArray());
        }

        private static string NormalizeManualDigits(string? value)
        {
            return (value ?? string.Empty)
                .Replace('٠', '0').Replace('١', '1').Replace('٢', '2')
                .Replace('٣', '3').Replace('٤', '4').Replace('٥', '5')
                .Replace('٦', '6').Replace('٧', '7').Replace('٨', '8')
                .Replace('٩', '9').Replace('۰', '0').Replace('۱', '1')
                .Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
                .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7')
                .Replace('۸', '8').Replace('۹', '9');
        }
    }
}
