using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YemenGuide.Data;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.Globalization; // 👈 مهم جداً لفك مشكلة الخريطة
using System.Text.RegularExpressions;
using YemenGuide.Web.Services.Notifications;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Services.Advertisements;

namespace YemenGuide.Web.Controllers
{
    public class PlacesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly CurrencyDisplayService _currencyDisplayService;
        private readonly ICustomerAdvertisementService _customerAdvertisementService;

        public PlacesController(
            AppDbContext context,
            INotificationService notificationService,
            CurrencyDisplayService currencyDisplayService,
            ICustomerAdvertisementService customerAdvertisementService)
        {
            _context = context;
            _notificationService = notificationService;
            _currencyDisplayService = currencyDisplayService;
            _customerAdvertisementService = customerAdvertisementService;
        }

        /* ================================================================
           سياق عملة العرض للعميل
           - القيم المالية الأصلية تبقى YER.
           - التحويل هنا للعرض فقط حسب North/South.
           - تُقرأ أسعار الصرف مرة واحدة لكل Action لتجنب استعلام لكل مبلغ.
           ================================================================ */

        private sealed class CustomerCurrencyContext
        {
            public string Currency { get; init; } = CurrencyDisplayService.YemeniRial;
            public string Code { get; init; } = "YER";
            public string Symbol { get; init; } = "ر.ي";
            public decimal SanaaUsdRate { get; init; } = 537m;
            public decimal SanaaSarRate { get; init; } = 140m;
            public decimal AdenUsdRate { get; init; } = 1900m;
            public decimal AdenSarRate { get; init; } = 500m;

            public decimal Convert(
                decimal amountInYer,
                string? exchangeRateRegion)
            {
                if (Currency == CurrencyDisplayService.YemeniRial)
                {
                    return Math.Round(
                        amountInYer,
                        0,
                        MidpointRounding.AwayFromZero);
                }

                bool useAdenMarket =
                    string.Equals(
                        exchangeRateRegion,
                        "South",
                        StringComparison.OrdinalIgnoreCase);

                decimal rate =
                    Currency == CurrencyDisplayService.UsDollar
                        ? useAdenMarket
                            ? AdenUsdRate
                            : SanaaUsdRate
                        : useAdenMarket
                            ? AdenSarRate
                            : SanaaSarRate;

                if (rate <= 0m)
                {
                    rate = Currency == CurrencyDisplayService.UsDollar
                        ? useAdenMarket ? 1900m : 537m
                        : useAdenMarket ? 500m : 140m;
                }

                return Math.Round(
                    amountInYer / rate,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            public string Format(
                decimal amountInYer,
                string? exchangeRateRegion)
            {
                decimal converted =
                    Convert(
                        amountInYer,
                        exchangeRateRegion);

                if (Currency == CurrencyDisplayService.UsDollar)
                {
                    return $"${converted:N2}";
                }

                if (Currency == CurrencyDisplayService.SaudiRiyal)
                {
                    return $"{converted:N2} ر.س";
                }

                return $"{converted:N0} ر.ي";
            }
        }

        private async Task<CustomerCurrencyContext>
            GetCustomerCurrencyContextAsync()
        {
            string currency =
                await _currencyDisplayService
                    .GetPreferredCurrencyAsync(
                        HttpContext.RequestAborted);

            var rates =
                await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(setting => setting.Id)
                    .Select(setting => new
                    {
                        setting.SanaaUsdRate,
                        setting.SanaaSarRate,
                        setting.AdenUsdRate,
                        setting.AdenSarRate
                    })
                    .FirstOrDefaultAsync(
                        HttpContext.RequestAborted);

            return new CustomerCurrencyContext
            {
                Currency = currency,
                Code = CurrencyDisplayService.GetCurrencyCode(currency),
                Symbol = CurrencyDisplayService.GetCurrencySymbol(currency),
                SanaaUsdRate = rates?.SanaaUsdRate > 0m
                    ? rates.SanaaUsdRate
                    : 537m,
                SanaaSarRate = rates?.SanaaSarRate > 0m
                    ? rates.SanaaSarRate
                    : 140m,
                AdenUsdRate = rates?.AdenUsdRate > 0m
                    ? rates.AdenUsdRate
                    : 1900m,
                AdenSarRate = rates?.AdenSarRate > 0m
                    ? rates.AdenSarRate
                    : 500m
            };
        }


        /* ================================================================
           أدوات صفحة تفاصيل المنشأة
           ================================================================ */

        private static bool IsActiveState(
            string? value)
        {
            return string.Equals(
                value?.Trim(),
                "Active",
                StringComparison.OrdinalIgnoreCase);
        }

        private sealed class PolicyShiftWindow
        {
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
        }

        private static bool TryParsePolicyClock(
            string? value,
            out TimeSpan clock)
        {
            clock = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return TimeSpan.TryParse(
                       value.Trim(),
                       CultureInfo.InvariantCulture,
                       out clock) ||
                   TimeSpan.TryParse(
                       value.Trim(),
                       CultureInfo.CurrentCulture,
                       out clock);
        }

        private static TimeSpan GetPolicyCheckInTime(
            Place place)
        {
            if (TryParsePolicyClock(
                    place.PlacePolicy?.CheckInTime,
                    out var policyTime))
            {
                return policyTime;
            }

            if (TryParsePolicyClock(
                    place.CheckInTime,
                    out var placeTime))
            {
                return placeTime;
            }

            return TimeSpan.FromHours(14);
        }

        private static TimeSpan GetPolicyCheckOutTime(
            Place place)
        {
            if (TryParsePolicyClock(
                    place.PlacePolicy?.CheckOutTime,
                    out var policyTime))
            {
                return policyTime;
            }

            if (TryParsePolicyClock(
                    place.CheckOutTime,
                    out var placeTime))
            {
                return placeTime;
            }

            return TimeSpan.FromHours(12);
        }

        private static HashSet<string>
            ReadWeeklyClosedDays(
                Place place)
        {
            var csv =
                place.PlacePolicy?
                    .ClosedDaysCsv;

            return string.IsNullOrWhiteSpace(csv)
                ? new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                : csv.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);
        }

        private static bool ContainsWeeklyClosedDate(
            Place place,
            DateTime startDate,
            DateTime endDate)
        {
            var closedDays =
                ReadWeeklyClosedDays(
                    place);

            if (closedDays.Count == 0)
            {
                return false;
            }

            for (
                var day = startDate.Date;
                day < endDate.Date;
                day = day.AddDays(1))
            {
                if (closedDays.Contains(
                        day.DayOfWeek.ToString()))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<PolicyShiftWindow>
            ReadPolicyShiftWindows(
                string? jsonText)
        {
            var result =
                new List<PolicyShiftWindow>();

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return result;
            }

            try
            {
                using var document =
                    JsonDocument.Parse(
                        jsonText);

                if (document.RootElement.ValueKind !=
                    JsonValueKind.Array)
                {
                    return result;
                }

                foreach (
                    var item in
                    document.RootElement
                        .EnumerateArray())
                {
                    if (item.ValueKind !=
                        JsonValueKind.Object)
                    {
                        continue;
                    }

                    string? from = null;
                    string? to = null;

                    foreach (
                        var property in
                        item.EnumerateObject())
                    {
                        if (
                            string.Equals(
                                property.Name,
                                "from",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                property.Name,
                                "start",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                property.Name,
                                "startTime",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            from =
                                property.Value.ValueKind ==
                                JsonValueKind.String
                                    ? property.Value.GetString()
                                    : null;
                        }

                        if (
                            string.Equals(
                                property.Name,
                                "to",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                property.Name,
                                "end",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                property.Name,
                                "endTime",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            to =
                                property.Value.ValueKind ==
                                JsonValueKind.String
                                    ? property.Value.GetString()
                                    : null;
                        }
                    }

                    if (
                        !TryParsePolicyClock(
                            from,
                            out var start) ||
                        !TryParsePolicyClock(
                            to,
                            out var end) ||
                        start == end)
                    {
                        continue;
                    }

                    result.Add(
                        new PolicyShiftWindow
                        {
                            Start = start,
                            End = end
                        });
                }
            }
            catch (JsonException)
            {
            }

            return result;
        }

        private static bool PackageFitsConfiguredShift(
            Place place,
            PlacePackage package)
        {
            if (!string.Equals(
                    place.PlacePolicy?
                        .OperationSystemMode,
                    "Shifts",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (
                !package.StartTime.HasValue ||
                !package.EndTime.HasValue ||
                package.StartTime.Value ==
                    package.EndTime.Value)
            {
                return false;
            }

            var packageStart =
                package.StartTime.Value
                    .TotalMinutes;

            var packageEnd =
                package.EndTime.Value
                    .TotalMinutes;

            if (package.EndTime.Value <=
                package.StartTime.Value)
            {
                packageEnd +=
                    TimeSpan.FromDays(1)
                        .TotalMinutes;
            }

            var configuredShifts =
                ReadPolicyShiftWindows(
                    place.PlacePolicy?
                        .ShiftsJson);

            /*
             * إذا كان وضع التشغيل "Shifts" لكن لم تُحفظ أي فترة تشغيلية،
             * لا نخفي/نمنع كل الباقات. وقت الباقة نفسه يكفي في هذه الحالة.
             */
            if (configuredShifts.Count == 0)
            {
                return true;
            }

            foreach (
                var shift in
                configuredShifts)
            {
                var shiftStart =
                    shift.Start.TotalMinutes;

                var shiftEnd =
                    shift.End.TotalMinutes;

                if (shift.End <=
                    shift.Start)
                {
                    shiftEnd +=
                        TimeSpan.FromDays(1)
                            .TotalMinutes;
                }

                if (
                    packageStart >= shiftStart &&
                    packageEnd <= shiftEnd)
                {
                    return true;
                }
            }

            return false;
        }

        private static (
            DateTime Start,
            DateTime End)
            GetPackageOperationalInterval(
                DateTime date,
                PlacePackage package)
        {
            var start =
                date.Date.Add(
                    package.StartTime ??
                    TimeSpan.Zero);

            var end =
                date.Date.Add(
                    package.EndTime ??
                    package.StartTime ??
                    TimeSpan.Zero);

            if (end <= start)
            {
                end =
                    end.AddDays(1);
            }

            return (
                start,
                end);
        }

        private static bool PackageIntervalsConflict(
            DateTime requestedDate,
            PlacePackage requestedPackage,
            Booking existingBooking,
            int bufferHours)
        {
            if (existingBooking.Package == null)
            {
                return true;
            }

            if (
                !requestedPackage.StartTime.HasValue ||
                !requestedPackage.EndTime.HasValue ||
                !existingBooking.Package.StartTime.HasValue ||
                !existingBooking.Package.EndTime.HasValue)
            {
                return true;
            }

            var requested =
                GetPackageOperationalInterval(
                    requestedDate,
                    requestedPackage);

            var existing =
                GetPackageOperationalInterval(
                    existingBooking.StartDate,
                    existingBooking.Package);

            var buffer =
                TimeSpan.FromHours(
                    Math.Clamp(
                        bufferHours,
                        0,
                        24));

            return
                existing.Start <
                    requested.End.Add(buffer) &&
                existing.End.Add(buffer) >
                    requested.Start;
        }

        private async Task<(bool Available, string Message)>
            CheckPackageDateAvailabilityCoreAsync(
                Place place,
                PlacePackage selectedPackage,
                DateTime requestedDate,
                DateTime referenceTime)
        {
            var requestedDay =
                requestedDate.Date;

            var today =
                referenceTime.Date;

            if (requestedDay < today)
            {
                return (
                    false,
                    "لا يمكن اختيار تاريخ مضى.");
            }

            var maximumHorizonMonths =
                Math.Max(
                    1,
                    place.PlacePolicy?
                        .MaximumHorizonMonths ?? 6);

            if (requestedDay >
                today.AddMonths(
                    maximumHorizonMonths))
            {
                return (
                    false,
                    $"لا يمكن الحجز لأكثر من {maximumHorizonMonths} أشهر مقدمًا.");
            }

            /*
             * وقت الباقة نفسه (StartTime / EndTime) هو نافذة التشغيل
             * الخاصة بهذه الباقة. لا نمنعها بسبب ShiftsJson هنا،
             * لأن ذلك كان يجعل الباقة ظاهرة ثم يمنع تحميل التقويم.
             */
            var minimumNoticeHours =
                Math.Max(
                    0,
                    place.PlacePolicy?
                        .MinimumNoticeHours ?? 0);

            if (minimumNoticeHours > 0)
            {
                var requestedStartAt =
                    requestedDay.Add(
                        selectedPackage.StartTime ??
                        GetPolicyCheckInTime(
                            place));

                if (requestedStartAt <
                    referenceTime.AddHours(
                        minimumNoticeHours))
                {
                    return (
                        false,
                        $"يجب إجراء الحجز قبل الموعد بـ {minimumNoticeHours} ساعة على الأقل.");
                }
            }

            var weeklyClosedDays =
                ReadWeeklyClosedDays(
                    place);

            if (weeklyClosedDays.Contains(
                    requestedDay.DayOfWeek
                        .ToString()))
            {
                return (
                    false,
                    "هذا اليوم مغلق أسبوعيًا حسب سياسة المنشأة.");
            }

            var isClosedByOwner =
                await _context
                    .PlaceCalendarExceptions
                    .AsNoTracking()
                    .AnyAsync(exception =>
                        exception.PlaceId ==
                            place.PlaceId &&

                        exception.ExceptionDate ==
                            requestedDay &&

                        exception.ExceptionType ==
                            "Closed");

            if (isClosedByOwner)
            {
                return (
                    false,
                    "هذا اليوم مغلق من قبل المنشأة.");
            }

            var blockingStates =
                new[]
                {
                    "Pending",
                    "AwaitingPayment",
                    "PaymentReview",
                    "Confirmed"
                };

            var shiftMode =
                string.Equals(
                    place.PlacePolicy?
                        .OperationSystemMode,
                    "Shifts",
                    StringComparison.OrdinalIgnoreCase);

            var searchFrom =
                shiftMode
                    ? requestedDay.AddDays(-1)
                    : requestedDay;

            var searchTo =
                shiftMode
                    ? requestedDay.AddDays(1)
                    : requestedDay;

            var existingBookings =
                await _context.Bookings
                    .AsNoTracking()
                    .Include(booking =>
                        booking.Package)
                    .Where(booking =>
                        booking.PlaceId ==
                            place.PlaceId &&

                        booking.PackageId
                            .HasValue &&

                        blockingStates.Contains(
                            booking.BookingState ??
                            string.Empty) &&

                        (
                            booking.BookingState !=
                                "AwaitingPayment" ||

                            !booking.TimerExpiredAt
                                .HasValue ||

                            booking.TimerExpiredAt >
                                referenceTime
                        ) &&

                        booking.StartDate.Date >=
                            searchFrom &&

                        booking.StartDate.Date <=
                            searchTo)
                    .ToListAsync();

            if (!shiftMode)
            {
                var sameDayBooked =
                    existingBookings.Any(
                        booking =>
                            booking.StartDate.Date ==
                            requestedDay);

                return sameDayBooked
                    ? (
                        false,
                        "هذا اليوم محجوز بالفعل. اختر يومًا آخر متاحًا.")
                    : (
                        true,
                        string.Empty);
            }

            var bufferHours =
                Math.Clamp(
                    place.PlacePolicy?
                        .BufferTimeHours ?? 0,
                    0,
                    24);

            foreach (
                var booking in
                existingBookings)
            {
                if (booking.Package == null)
                {
                    if (booking.StartDate.Date ==
                        requestedDay)
                    {
                        return (
                            false,
                            "هذا الموعد محجوز بالفعل.");
                    }

                    continue;
                }

                if (PackageIntervalsConflict(
                        requestedDay,
                        selectedPackage,
                        booking,
                        bufferHours))
                {
                    return (
                        false,
                        "هذا الموعد يتعارض مع حجز قائم أو مع وقت التجهيز بين الفترات.");
                }
            }

            return (
                true,
                string.Empty);
        }


        private static bool
            RequiresAdjacentRoomBufferBlock(
                Place place)
        {
            var bufferHours =
                Math.Clamp(
                    place.PlacePolicy?
                        .BufferTimeHours ?? 0,
                    0,
                    24);

            if (bufferHours <= 0)
            {
                return false;
            }

            var checkIn =
                GetPolicyCheckInTime(
                    place);

            var checkOut =
                GetPolicyCheckOutTime(
                    place);

            var checkOutAt =
                DateTime.Today.Add(
                    checkOut);

            var nextCheckInAt =
                DateTime.Today.Add(
                    checkIn);

            if (nextCheckInAt <
                checkOutAt)
            {
                nextCheckInAt =
                    nextCheckInAt.AddDays(1);
            }

            var naturalGap =
                Math.Max(
                    0d,
                    (nextCheckInAt -
                     checkOutAt)
                    .TotalHours);

            return bufferHours >
                   naturalGap;
        }

        private static bool IsSystemAccountUnavailable(
            string? accountState)
        {
            return string.Equals(
                       accountState,
                       "Blocked",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       accountState,
                       "Suspended",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       accountState,
                       "Inactive",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       accountState,
                       "Disabled",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       accountState,
                       "Deleted",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseCoordinateValue(
            string value,
            out double result)
        {
            return double.TryParse(
                       value.Trim(),
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out result) ||
                   double.TryParse(
                       value.Trim(),
                       NumberStyles.Float,
                       CultureInfo.CurrentCulture,
                       out result);
        }

        private static bool IsYemenCoordinate(
            double latitude,
            double longitude)
        {
            return latitude >= 11d &&
                   latitude <= 20d &&
                   longitude >= 41d &&
                   longitude <= 56d;
        }

        private static bool TryParseCoordinates(
            string? coordinates,
            out double latitude,
            out double longitude)
        {
            latitude = 0d;
            longitude = 0d;

            if (string.IsNullOrWhiteSpace(
                    coordinates))
            {
                return false;
            }

            var parts =
                coordinates
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries);

            if (parts.Length < 2 ||
                !TryParseCoordinateValue(
                    parts[0],
                    out var first) ||
                !TryParseCoordinateValue(
                    parts[1],
                    out var second))
            {
                return false;
            }

            var directIsValid =
                Math.Abs(first) <= 90d &&
                Math.Abs(second) <= 180d;

            var reversedIsValid =
                Math.Abs(second) <= 90d &&
                Math.Abs(first) <= 180d;

            if (!directIsValid &&
                !reversedIsValid)
            {
                return false;
            }

            if (directIsValid &&
                IsYemenCoordinate(
                    first,
                    second))
            {
                latitude = first;
                longitude = second;
                return true;
            }

            if (reversedIsValid &&
                IsYemenCoordinate(
                    second,
                    first))
            {
                latitude = second;
                longitude = first;
                return true;
            }

            if (directIsValid)
            {
                latitude = first;
                longitude = second;
                return true;
            }

            latitude = second;
            longitude = first;
            return true;
        }

        private static List<string> SplitTextLines(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return value
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ParseLabeledValues(
            IEnumerable<string> lines,
            string label)
        {
            var line =
                lines.FirstOrDefault(item =>
                    item.Contains(
                        label,
                        StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(line))
            {
                return new List<string>();
            }

            var separatorIndex =
                line.IndexOf(':');

            var valuesText =
                separatorIndex >= 0
                    ? line[(separatorIndex + 1)..]
                    : line.Replace(
                        label,
                        string.Empty,
                        StringComparison.OrdinalIgnoreCase);

            return valuesText
                .Split(
                    new[] { '-', '،', ',', '|', ';' },
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static (
            string MarketingDescription,
            string TechnicalDetails,
            List<string> EventTypes,
            List<string> SeatingSetups)
            ParsePlaceDescription(
                string? description)
        {
            var fullDescription =
                description?.Trim() ??
                string.Empty;

            const string marker =
                "--- تفاصيل المنشأة ---";

            var markerIndex =
                fullDescription.IndexOf(
                    marker,
                    StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                return (
                    fullDescription,
                    string.Empty,
                    new List<string>(),
                    new List<string>());
            }

            var marketingDescription =
                fullDescription[..markerIndex]
                    .Trim();

            var rawTechnical =
                fullDescription[
                    (markerIndex + marker.Length)..]
                    .Trim();

            var rawLines =
                SplitTextLines(
                    rawTechnical);

            var eventTypes =
                ParseLabeledValues(
                    rawLines,
                    "المناسبات");

            var seatingSetups =
                ParseLabeledValues(
                    rawLines,
                    "المقاعد");

            var technicalLines =
                rawLines
                    .Where(line =>
                        !line.Contains(
                            "المناسبات:",
                            StringComparison.OrdinalIgnoreCase) &&
                        !line.Contains(
                            "المقاعد:",
                            StringComparison.OrdinalIgnoreCase))
                    .Select(line =>
                        line.Trim(' ', '|', '-'))
                    .Where(line =>
                        !string.IsNullOrWhiteSpace(line) &&
                        !line.Contains(
                            "لا يوجد",
                            StringComparison.OrdinalIgnoreCase) &&
                        !line.EndsWith(
                            ":",
                            StringComparison.Ordinal))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return (
                marketingDescription,
                string.Join(
                    Environment.NewLine,
                    technicalLines),
                eventTypes,
                seatingSetups);
        }

        private static List<string> ReadStringListJson(
            string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                using var document =
                    JsonDocument.Parse(json);

                if (document.RootElement.ValueKind !=
                    JsonValueKind.Array)
                {
                    return new List<string>();
                }

                var values =
                    new List<string>();

                foreach (
                    var element in
                    document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind ==
                        JsonValueKind.String)
                    {
                        var value =
                            element.GetString()?.Trim();

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            values.Add(value);
                        }
                    }
                }

                return values
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        private static List<DepositPolicyItem>
            ReadDepositPolicies(
                string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<DepositPolicyItem>();
            }

            try
            {
                return JsonSerializer.Deserialize<
                           List<DepositPolicyItem>>(
                           json,
                           new JsonSerializerOptions
                           {
                               PropertyNameCaseInsensitive = true
                           }) ??
                       new List<DepositPolicyItem>();
            }
            catch (JsonException)
            {
                return new List<DepositPolicyItem>();
            }
        }

        private static List<string> ReadShiftLabels(
            string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                using var document =
                    JsonDocument.Parse(json);

                if (document.RootElement.ValueKind !=
                    JsonValueKind.Array)
                {
                    return new List<string>();
                }

                var result =
                    new List<string>();

                foreach (
                    var item in
                    document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind ==
                        JsonValueKind.String)
                    {
                        var text =
                            item.GetString()?.Trim();

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            result.Add(text);
                        }

                        continue;
                    }

                    if (item.ValueKind !=
                        JsonValueKind.Object)
                    {
                        continue;
                    }

                    string? name = null;
                    string? start = null;
                    string? end = null;

                    foreach (
                        var propertyName in
                        new[] { "name", "title", "label" })
                    {
                        if (item.TryGetProperty(
                                propertyName,
                                out var property) &&
                            property.ValueKind ==
                                JsonValueKind.String)
                        {
                            name =
                                property.GetString()?.Trim();

                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                break;
                            }
                        }
                    }

                    foreach (
                        var propertyName in
                        new[] { "start", "startTime", "from" })
                    {
                        if (item.TryGetProperty(
                                propertyName,
                                out var property) &&
                            property.ValueKind ==
                                JsonValueKind.String)
                        {
                            start =
                                property.GetString()?.Trim();
                            break;
                        }
                    }

                    foreach (
                        var propertyName in
                        new[] { "end", "endTime", "to" })
                    {
                        if (item.TryGetProperty(
                                propertyName,
                                out var property) &&
                            property.ValueKind ==
                                JsonValueKind.String)
                        {
                            end =
                                property.GetString()?.Trim();
                            break;
                        }
                    }

                    var timeRange =
                        !string.IsNullOrWhiteSpace(start) &&
                        !string.IsNullOrWhiteSpace(end)
                            ? $"{start} - {end}"
                            : start ?? end ?? string.Empty;

                    var label =
                        string.Join(
                            " — ",
                            new[] { name, timeRange }
                                .Where(value =>
                                    !string.IsNullOrWhiteSpace(value)));

                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        result.Add(label);
                    }
                }

                return result
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        private static List<PlacePolicySectionDto>
            BuildPolicySections(
                Place place,
                IReadOnlyCollection<DepositPolicyItem> depositPolicies,
                IReadOnlyCollection<string> securityPolicies,
                IReadOnlyCollection<string> exceptionPolicies,
                IReadOnlyCollection<string> terms)
        {
            var sections =
                new List<PlacePolicySectionDto>();

            var depositItems =
                depositPolicies
                    .Select(policy =>
                    {
                        var value =
                            policy.val?.Trim() ??
                            string.Empty;

                        var type =
                            policy.type?.Trim() ??
                            string.Empty;

                        var description =
                            policy.desc?.Trim() ??
                            string.Empty;

                        var amountText =
                            string.IsNullOrWhiteSpace(value)
                                ? string.Empty
                                : type.Contains('%') ||
                                  type.Contains(
                                      "نسبة",
                                      StringComparison.OrdinalIgnoreCase)
                                    ? $"{value}%"
                                    : $"{value} {type}".Trim();

                        return string.Join(
                            " — ",
                            new[] { amountText, description }
                                .Where(item =>
                                    !string.IsNullOrWhiteSpace(item)));
                    })
                    .Where(item =>
                        !string.IsNullOrWhiteSpace(item))
                    .ToList();

            if (!string.IsNullOrWhiteSpace(
                    place.DepositPolicy))
            {
                depositItems.Add(
                    place.DepositPolicy.Trim());
            }

            depositItems =
                depositItems
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (depositItems.Count > 0)
            {
                sections.Add(
                    new PlacePolicySectionDto
                    {
                        Key = "deposit",
                        Title = "سياسة العربون",
                        IconClass = "bi-wallet2",
                        Items = depositItems
                    });
            }

            var securityItems =
                securityPolicies
                    .Where(item =>
                        !string.IsNullOrWhiteSpace(item))
                    .ToList();

            if (!string.IsNullOrWhiteSpace(
                    place.InsurancePolicy))
            {
                securityItems.Add(
                    place.InsurancePolicy.Trim());
            }

            securityItems =
                securityItems
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (securityItems.Count > 0)
            {
                sections.Add(
                    new PlacePolicySectionDto
                    {
                        Key = "security",
                        Title = "الضمان والاستخدام",
                        IconClass = "bi-shield-check",
                        Items = securityItems
                    });
            }

            var cancellationItems =
                new List<string>();

            if (!string.IsNullOrWhiteSpace(
                    place.CancellationPolicy))
            {
                cancellationItems.Add(
                    place.CancellationPolicy.Trim());
            }

            if (place.PlacePolicy != null)
            {
                if (place.PlacePolicy.FreeCancellationHours > 0)
                {
                    cancellationItems.Add(
                        $"إلغاء مجاني قبل " +
                        $"{place.PlacePolicy.FreeCancellationHours} ساعة.");
                }

                if (place.PlacePolicy.ConfiscateOnNoShow == true)
                {
                    cancellationItems.Add(
                        "قد تتم مصادرة العربون عند عدم الحضور.");
                }

                if (place.PlacePolicy.RefundProcessingDays > 0)
                {
                    cancellationItems.Add(
                        $"مدة معالجة الاسترداد حتى " +
                        $"{place.PlacePolicy.RefundProcessingDays} أيام.");
                }
            }

            cancellationItems =
                cancellationItems
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (cancellationItems.Count > 0)
            {
                sections.Add(
                    new PlacePolicySectionDto
                    {
                        Key = "cancellation",
                        Title = "الإلغاء والاسترداد",
                        IconClass = "bi-arrow-counterclockwise",
                        Items = cancellationItems
                    });
            }

            var exceptionItems =
                exceptionPolicies
                    .Where(item =>
                        !string.IsNullOrWhiteSpace(item))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (exceptionItems.Count > 0)
            {
                sections.Add(
                    new PlacePolicySectionDto
                    {
                        Key = "exceptions",
                        Title = "الاستثناءات",
                        IconClass = "bi-exclamation-circle",
                        Items = exceptionItems
                    });
            }

            var termsItems =
                terms
                    .Where(item =>
                        !string.IsNullOrWhiteSpace(item))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (termsItems.Count > 0)
            {
                sections.Add(
                    new PlacePolicySectionDto
                    {
                        Key = "terms",
                        Title = "الشروط العامة",
                        IconClass = "bi-list-check",
                        Items = termsItems
                    });
            }

            return sections;
        }

        /* ================================================================
           تفاصيل حجز العميل
           ================================================================ */

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> BookingDetails(int id)
        {
            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            if (!int.TryParse(
                userIdText,
                out var customerId))
            {
                return Challenge();
            }

            var booking =
                await _context.Bookings
                    .AsNoTracking()

                    .Include(b =>
                        b.Customer)

                    .Include(b =>
                        b.Place)
                        .ThenInclude(p =>
                            p.Category)

                    .Include(b =>
                        b.Place)
                        .ThenInclude(p =>
                            p.City)

                    .Include(b =>
                        b.Place)
                        .ThenInclude(p =>
                            p.PlacePolicy)

                    .Include(b =>
                        b.Place)
                        .ThenInclude(p =>
                            p.Owner)

                    .Include(b =>
                        b.Room)

                    .Include(b =>
                        b.BookingRoomUnits)
                        .ThenInclude(assignment =>
                            assignment.RoomUnit)

                    .Include(b =>
                        b.Package)

                    .Include(b =>
                        b.FinancialRecord)

                    .Include(b =>
                        b.BookingAdditionalServices)
                        .ThenInclude(bs =>
                            bs.Service)

                    .Include(b =>
                        b.RefundRequests)

                    .AsSplitQuery()

                    .FirstOrDefaultAsync(b =>
                        b.BookingId == id &&
                        b.CustomerId == customerId);

            if (booking == null)
            {
                return NotFound(
                    "لم يتم العثور على الحجز، أو لا تملك صلاحية عرضه."
                );
            }

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            string? exchangeRateRegion =
                booking.Place?
                    .City?
                    .ExchangeRateRegion;

            ViewBag.DisplayCurrency =
                currencyContext.Currency;
            ViewBag.DisplayCurrencyCode =
                currencyContext.Code;
            ViewBag.DisplayCurrencySymbol =
                currencyContext.Symbol;

            ViewBag.TotalPriceDisplay =
                currencyContext.Convert(
                    booking.TotalPrice,
                    exchangeRateRegion);
            ViewBag.TotalPriceText =
                currencyContext.Format(
                    booking.TotalPrice,
                    exchangeRateRegion);

            var originalTotalPrice =
                booking.OriginalTotalPrice > 0m
                    ? booking.OriginalTotalPrice
                    : booking.TotalPrice +
                      Math.Max(
                          0m,
                          booking.DiscountAmount);

            ViewBag.OriginalTotalPriceDisplay =
                currencyContext.Convert(
                    originalTotalPrice,
                    exchangeRateRegion);
            ViewBag.OriginalTotalPriceText =
                currencyContext.Format(
                    originalTotalPrice,
                    exchangeRateRegion);

            ViewBag.DiscountAmountDisplay =
                currencyContext.Convert(
                    Math.Max(
                        0m,
                        booking.DiscountAmount),
                    exchangeRateRegion);
            ViewBag.DiscountAmountText =
                currencyContext.Format(
                    Math.Max(
                        0m,
                        booking.DiscountAmount),
                    exchangeRateRegion);

            decimal itemPrice =
                booking.Room?.Price ??
                booking.Package?.Price ??
                0m;

            ViewBag.ItemPriceDisplay =
                currencyContext.Convert(
                    itemPrice,
                    exchangeRateRegion);
            ViewBag.ItemPriceText =
                currencyContext.Format(
                    itemPrice,
                    exchangeRateRegion);

            if (booking.FinancialRecord != null)
            {
                ViewBag.DepositAmountDisplay =
                    currencyContext.Convert(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.DepositAmount),
                        exchangeRateRegion);
                ViewBag.DepositAmountText =
                    currencyContext.Format(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.DepositAmount),
                        exchangeRateRegion);

                ViewBag.AmountPaidOnlineDisplay =
                    currencyContext.Convert(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.AmountPaidOnline),
                        exchangeRateRegion);
                ViewBag.AmountPaidOnlineText =
                    currencyContext.Format(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.AmountPaidOnline),
                        exchangeRateRegion);

                ViewBag.AmountToPayAtPropertyDisplay =
                    currencyContext.Convert(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.AmountToPayAtProperty),
                        exchangeRateRegion);
                ViewBag.AmountToPayAtPropertyText =
                    currencyContext.Format(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.AmountToPayAtProperty),
                        exchangeRateRegion);

                ViewBag.AmountCollectedAtPropertyDisplay =
                    currencyContext.Convert(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.AmountCollectedAtProperty),
                        exchangeRateRegion);
                ViewBag.AmountCollectedAtPropertyText =
                    currencyContext.Format(
                        Math.Max(
                            0m,
                            booking.FinancialRecord.AmountCollectedAtProperty),
                        exchangeRateRegion);
            }

            decimal servicesTotal =
                booking.BookingAdditionalServices
                    .Sum(item =>
                        item.UnitPrice *
                        Math.Max(
                            1,
                            item.Quantity));

            ViewBag.ServicesTotalDisplay =
                currencyContext.Convert(
                    servicesTotal,
                    exchangeRateRegion);
            ViewBag.ServicesTotalText =
                currencyContext.Format(
                    servicesTotal,
                    exchangeRateRegion);

            ViewBag.BookingServiceUnitPriceDisplay =
                booking.BookingAdditionalServices
                    .GroupBy(item => item.ServiceId)
                    .ToDictionary(
                        group => group.Key,
                        group => currencyContext.Convert(
                            group.First().UnitPrice,
                            exchangeRateRegion));

            ViewBag.BookingServiceUnitPriceText =
                booking.BookingAdditionalServices
                    .GroupBy(item => item.ServiceId)
                    .ToDictionary(
                        group => group.Key,
                        group => currencyContext.Format(
                            group.First().UnitPrice,
                            exchangeRateRegion));

            ViewBag.BookingServiceTotalPriceDisplay =
                booking.BookingAdditionalServices
                    .GroupBy(item => item.ServiceId)
                    .ToDictionary(
                        group => group.Key,
                        group => currencyContext.Convert(
                            group.First().UnitPrice *
                            Math.Max(
                                1,
                                group.First().Quantity),
                            exchangeRateRegion));

            ViewBag.BookingServiceTotalPriceText =
                booking.BookingAdditionalServices
                    .GroupBy(item => item.ServiceId)
                    .ToDictionary(
                        group => group.Key,
                        group => currencyContext.Format(
                            group.First().UnitPrice *
                            Math.Max(
                                1,
                                group.First().Quantity),
                            exchangeRateRegion));

            ViewBag.RefundDepositPaidText =
                booking.RefundRequests
                    .GroupBy(item => item.RefundRequestId)
                    .ToDictionary(
                        group => group.Key,
                        group => currencyContext.Format(
                            group.First().DepositPaid,
                            exchangeRateRegion));

            ViewBag.RefundCancellationFeeText =
                booking.RefundRequests
                    .GroupBy(item => item.RefundRequestId)
                    .ToDictionary(
                        group => group.Key,
                        group => currencyContext.Format(
                            group.First().CancellationFee,
                            exchangeRateRegion));

            ViewBag.RefundAmountText =
                booking.RefundRequests
                    .GroupBy(item => item.RefundRequestId)
                    .ToDictionary(
                        group => group.Key,
                        group => currencyContext.Format(
                            group.First().RefundAmount,
                            exchangeRateRegion));

            if (booking.DiscountValueSnapshot.HasValue &&
                string.Equals(
                    booking.DiscountTypeSnapshot,
                    "FixedAmount",
                    StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.PromotionDiscountValueText =
                    currencyContext.Format(
                        booking.DiscountValueSnapshot.Value,
                        exchangeRateRegion);
            }

            return View(booking);
        }

        [HttpGet("/Places/CheckPackageDateAvailability")]
        public async Task<IActionResult> CheckPackageDateAvailability(
            int placeId,
            int packageId,
            DateTime date)
        {
            Response.Headers["Cache-Control"] =
                "no-store, no-cache, must-revalidate";

            if (placeId <= 0 ||
                packageId <= 0 ||
                date == default)
            {
                return BadRequest(new
                {
                    available = false,
                    message =
                        "بيانات المنشأة أو الباقة أو التاريخ غير صحيحة."
                });
            }

            var place =
                await _context.Places
                    .AsNoTracking()
                    .Include(item =>
                        item.PlacePolicy)
                    .FirstOrDefaultAsync(item =>
                        item.PlaceId ==
                            placeId);

            if (place == null ||
                !IsActiveState(
                    place.Status))
            {
                return NotFound(new
                {
                    available = false,
                    message =
                        "المنشأة غير موجودة أو غير متاحة حاليًا."
                });
            }

            var selectedPackage =
                await _context.PlacePackages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(package =>
                        package.PackageId ==
                            packageId &&

                        package.PlaceId ==
                            placeId &&

                        package.IsActive ==
                            true);

            if (selectedPackage == null)
            {
                return NotFound(new
                {
                    available = false,
                    message =
                        "الباقة المختارة لم تعد متاحة."
                });
            }

            var availability =
                await CheckPackageDateAvailabilityCoreAsync(
                    place,
                    selectedPackage,
                    date,
                    DateTime.Now);

            return Json(new
            {
                available =
                    availability.Available,

                message =
                    availability.Message,

                date =
                    date.Date.ToString(
                        "yyyy-MM-dd")
            });
        }


        [HttpGet("/Places/GetFullyBookedDates")]
        public async Task<IActionResult> GetFullyBookedDates(
            int placeId,
            int? packageId = null)
        {
            Response.Headers["Cache-Control"] =
                "no-store, no-cache, must-revalidate";

            if (placeId <= 0)
            {
                return BadRequest(
                    "رقم المنشأة غير صحيح.");
            }

            var today =
                DateTime.Today;

            var now =
                DateTime.Now;

            var place =
                await _context.Places
                    .AsNoTracking()
                    .Include(item =>
                        item.PlacePolicy)
                    .FirstOrDefaultAsync(item =>
                        item.PlaceId ==
                            placeId);

            if (place == null)
            {
                return NotFound(
                    "المنشأة غير موجودة.");
            }

            var maximumHorizonMonths =
                Math.Max(
                    1,
                    place.PlacePolicy?
                        .MaximumHorizonMonths ?? 6);

            var horizonEnd =
                today.AddMonths(
                    maximumHorizonMonths);

            var closedDates =
                (
                    await _context
                        .PlaceCalendarExceptions
                        .AsNoTracking()
                        .Where(exception =>
                            exception.PlaceId ==
                                placeId &&

                            exception.ExceptionType ==
                                "Closed" &&

                            exception.ExceptionDate >=
                                today &&

                            exception.ExceptionDate <=
                                horizonEnd)
                        .Select(exception =>
                            exception.ExceptionDate.Date)
                        .ToListAsync()
                )
                .ToHashSet();

            var weeklyClosedDays =
                ReadWeeklyClosedDays(
                    place);

            if (weeklyClosedDays.Count > 0)
            {
                for (
                    var day = today;
                    day <= horizonEnd;
                    day = day.AddDays(1))
                {
                    if (weeklyClosedDays.Contains(
                            day.DayOfWeek.ToString()))
                    {
                        closedDates.Add(
                            day);
                    }
                }
            }

            var blockingStates =
                new[]
                {
            "Pending",
            "AwaitingPayment",
            "PaymentReview",
            "Confirmed"
                };

            /*
             * القاعات والشاليهات والباقات:
             * في اليوم الكامل يبقى سلوك الحجز اليومي كما هو.
             * في نظام الفترات نحجب فقط الأيام التي تتعارض فيها أوقات
             * الباقة المختارة مع حجز قائم بعد احتساب وقت التجهيز.
             */
            if (packageId.HasValue)
            {
                var selectedPackage =
                    await _context.PlacePackages
                        .AsNoTracking()
                        .FirstOrDefaultAsync(package =>
                            package.PackageId ==
                                packageId.Value &&
                            package.PlaceId ==
                                placeId &&
                            package.IsActive ==
                                true);

                if (selectedPackage == null)
                {
                    return NotFound(
                        "الباقة غير موجودة أو لا تتبع هذه المنشأة.");
                }

                /*
                 * لا نرجع BadRequest بسبب ShiftsJson.
                 * التقويم يجب أن يُحمّل دائمًا للباقة المفعلة، ثم نحدد
                 * الأيام غير المتاحة من الإغلاقات والحجوزات الفعلية.
                 */
                var existingPackageBookings =
                    await _context.Bookings
                        .AsNoTracking()
                        .Include(booking =>
                            booking.Package)
                        .Where(booking =>
                            booking.PlaceId ==
                                placeId &&

                            booking.PackageId
                                .HasValue &&

                            blockingStates.Contains(
                                booking.BookingState ??
                                string.Empty) &&

                            (
                                booking.BookingState !=
                                    "AwaitingPayment" ||

                                !booking.TimerExpiredAt
                                    .HasValue ||

                                booking.TimerExpiredAt >
                                    now
                            ) &&

                            booking.StartDate >=
                                today &&

                            booking.StartDate <=
                                horizonEnd)
                        .ToListAsync();

                var bookedPackageDates =
                    new HashSet<DateTime>(
                        closedDates);

                var shiftMode =
                    string.Equals(
                        place.PlacePolicy?
                            .OperationSystemMode,
                        "Shifts",
                        StringComparison.OrdinalIgnoreCase);

                var bufferHours =
                    Math.Clamp(
                        place.PlacePolicy?
                            .BufferTimeHours ?? 0,
                        0,
                        24);

                foreach (
                    var booking in
                    existingPackageBookings)
                {
                    if (!shiftMode)
                    {
                        bookedPackageDates.Add(
                            booking.StartDate.Date);

                        continue;
                    }

                    foreach (
                        var candidateDate in
                        new[]
                        {
                            booking.StartDate.Date.AddDays(-1),
                            booking.StartDate.Date,
                            booking.StartDate.Date.AddDays(1)
                        })
                    {
                        if (
                            candidateDate < today ||
                            candidateDate > horizonEnd)
                        {
                            continue;
                        }

                        if (PackageIntervalsConflict(
                                candidateDate,
                                selectedPackage,
                                booking,
                                bufferHours))
                        {
                            bookedPackageDates.Add(
                                candidateDate);
                        }
                    }
                }

                return Json(
                    bookedPackageDates
                        .Where(date =>
                            date >= today &&
                            date <= horizonEnd)
                        .Distinct()
                        .OrderBy(date =>
                            date)
                        .Select(date =>
                            date.ToString(
                                "yyyy-MM-dd"))
                        .ToList());
            }

            /*
             * الفنادق:
             * اليوم لا يقفل إلا إذا امتلأت كل الغرف والأجنحة.
             */
            var rooms =
                await _context.Rooms
                    .AsNoTracking()
                    .Where(room =>
                        room.PlaceId ==
                            placeId &&

                        room.IsAvailable ==
                            true &&

                        room.Quantity >
                            0)
                    .Select(room =>
                        new
                        {
                            room.RoomId,
                            room.Quantity
                        })
                    .ToListAsync();

            if (rooms.Count == 0)
            {
                return Json(
                    new List<string>());
            }

            var roomIds =
                rooms
                    .Select(room =>
                        room.RoomId)
                    .ToList();

            var bookings =
                await _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.PlaceId ==
                            placeId &&

                        booking.RoomId
                            .HasValue &&

                        roomIds.Contains(
                            booking.RoomId.Value) &&

                        blockingStates.Contains(
                            booking.BookingState ??
                            string.Empty) &&

                        (
                            booking.BookingState !=
                                "AwaitingPayment" ||

                            !booking.TimerExpiredAt
                                .HasValue ||

                            booking.TimerExpiredAt >
                                now
                        ) &&

                        (
                            booking.EndDate ??
                            booking.StartDate
                                .AddDays(1)
                        ) > today)
                    .ToListAsync();

            var candidateDates =
                new HashSet<DateTime>();

            foreach (var booking in bookings)
            {
                var startDate =
                    booking.StartDate.Date <
                    today
                        ? today
                        : booking.StartDate.Date;

                var endDate =
                    booking.EndDate?.Date ??
                    booking.StartDate.Date
                        .AddDays(1);

                if (endDate <= startDate)
                {
                    endDate =
                        startDate.AddDays(1);
                }

                /*
                 * يوم الخروج لا يُحسب محجوزًا.
                 */
                for (
                    var date = startDate;
                    date < endDate;
                    date = date.AddDays(1))
                {
                    candidateDates.Add(
                        date);
                }
            }

            var fullyBookedDates =
                new List<DateTime>();

            foreach (var date in candidateDates)
            {
                var nextDate =
                    date.AddDays(1);

                var allRoomsAreFull =
                    rooms.All(room =>
                    {
                        var reservedQuantity =
                            bookings
                                .Where(booking =>
                                    booking.RoomId ==
                                        room.RoomId &&

                                    booking.StartDate <
                                        nextDate &&

                                    (
                                        booking.EndDate ??
                                        booking.StartDate
                                            .AddDays(1)
                                    ) > date)
                                .Sum(booking =>
                                    booking.Quantity);

                        return reservedQuantity >=
                               room.Quantity;
                    });

                if (allRoomsAreFull)
                {
                    fullyBookedDates.Add(
                        date);
                }
            }

            fullyBookedDates.AddRange(
                closedDates);

            return Json(
                fullyBookedDates
                    .Where(date =>
                        date >= today &&
                        date <= horizonEnd)
                    .Distinct()
                    .OrderBy(date =>
                        date)
                    .Select(date =>
                        date.ToString(
                            "yyyy-MM-dd"))
                    .ToList());
        }

        [HttpGet]
        public async Task<IActionResult> AvailableRooms(
            int placeId,
            string start,
            string end,
            [FromQuery]
            List<int>? serviceIds,
            int? promotionId = null)
        {
            /* ================================================================
               دوال داخلية خاصة بهذه الصفحة
               ================================================================ */

            static string NormalizeDigits(
                string? value)
            {
                return (value ?? string.Empty)
                    .Replace('٠', '0')
                    .Replace('١', '1')
                    .Replace('٢', '2')
                    .Replace('٣', '3')
                    .Replace('٤', '4')
                    .Replace('٥', '5')
                    .Replace('٦', '6')
                    .Replace('٧', '7')
                    .Replace('٨', '8')
                    .Replace('٩', '9')
                    .Replace('۰', '0')
                    .Replace('۱', '1')
                    .Replace('۲', '2')
                    .Replace('۳', '3')
                    .Replace('۴', '4')
                    .Replace('۵', '5')
                    .Replace('۶', '6')
                    .Replace('۷', '7')
                    .Replace('۸', '8')
                    .Replace('۹', '9');
            }

            static int? ExtractRoomCapacity(
                string? description)
            {
                if (string.IsNullOrWhiteSpace(
                        description))
                {
                    return null;
                }

                var normalizedDescription =
                    NormalizeDigits(
                        description);

                var match =
                    System.Text.RegularExpressions.Regex.Match(
                        normalizedDescription,
                        @"(?:السعة|عدد\s*الأشخاص|الأشخاص|يتسع\s*لـ?)" +
                        @"\s*[:：\-]?\s*(\d+)",
                        System.Text.RegularExpressions.RegexOptions
                            .IgnoreCase);

                if (!match.Success)
                {
                    return null;
                }

                if (!int.TryParse(
                        match.Groups[1].Value,
                        out var capacity) ||
                    capacity <= 0)
                {
                    return null;
                }

                return capacity;
            }

            static string NormalizeImagePath(
                string? path)
            {
                if (string.IsNullOrWhiteSpace(
                        path))
                {
                    return string.Empty;
                }

                var normalized =
                    path
                        .Trim()
                        .Trim('"', '\'')
                        .Replace("\\", "/");

                if (string.IsNullOrWhiteSpace(
                        normalized))
                {
                    return string.Empty;
                }

                if (normalized.StartsWith(
                        "http://",
                        StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith(
                        "https://",
                        StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith(
                        "data:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return normalized;
                }

                if (normalized.StartsWith("~/"))
                {
                    return "/" +
                           normalized.Substring(2);
                }

                if (!normalized.StartsWith("/"))
                {
                    normalized =
                        "/" +
                        normalized;
                }

                return normalized;
            }

            static List<string> ParseRoomImages(
                string? imageValue)
            {
                var images =
                    new List<string>();

                if (string.IsNullOrWhiteSpace(
                        imageValue))
                {
                    return images;
                }

                var trimmedValue =
                    imageValue.Trim();

                /*
                 * الحالة الأولى:
                 * ["image1.jpg", "image2.jpg"]
                 */
                if (trimmedValue.StartsWith("[") &&
                    trimmedValue.EndsWith("]"))
                {
                    try
                    {
                        using var document =
                            JsonDocument.Parse(
                                trimmedValue);

                        if (document.RootElement.ValueKind ==
                            JsonValueKind.Array)
                        {
                            foreach (
                                var item in
                                document.RootElement
                                    .EnumerateArray())
                            {
                                if (item.ValueKind !=
                                    JsonValueKind.String)
                                {
                                    continue;
                                }

                                var imagePath =
                                    NormalizeImagePath(
                                        item.GetString());

                                if (!string.IsNullOrWhiteSpace(
                                        imagePath))
                                {
                                    images.Add(
                                        imagePath);
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        /*
                         * لو كان النص يشبه JSON لكنه غير صالح،
                         * نرجع لطريقة التقسيم العادية تحت.
                         */
                    }
                }

                /*
                 * الحالة الثانية:
                 * صورة واحدة أو صور مفصولة بفاصلة أو فاصلة منقوطة أو سطر.
                 */
                if (images.Count == 0)
                {
                    var parts =
                        trimmedValue.Split(
                            new[]
                            {
                        ',',
                        ';',
                        '|',
                        '\r',
                        '\n'
                            },
                            StringSplitOptions
                                .RemoveEmptyEntries);

                    foreach (var part in parts)
                    {
                        var imagePath =
                            NormalizeImagePath(
                                part);

                        if (!string.IsNullOrWhiteSpace(
                                imagePath))
                        {
                            images.Add(
                                imagePath);
                        }
                    }
                }

                return images
                    .Where(image =>
                        !string.IsNullOrWhiteSpace(
                            image))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            /* ================================================================
               التحقق من رقم المنشأة
               ================================================================ */

            if (placeId <= 0)
            {
                return BadRequest(
                    "رقم المنشأة غير صحيح.");
            }

            /* ================================================================
               التحقق من التواريخ
               ================================================================ */

            const string dateFormat =
                "yyyy-MM-dd";

            var startIsValid =
                DateTime.TryParseExact(
                    start,
                    dateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var startDate);

            var endIsValid =
                DateTime.TryParseExact(
                    end,
                    dateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var endDate);

            if (!startIsValid ||
                !endIsValid)
            {
                return BadRequest(
                    "صيغة تاريخ الدخول أو الخروج غير صحيحة.");
            }

            startDate =
                startDate.Date;

            endDate =
                endDate.Date;

            if (startDate < DateTime.Today)
            {
                return BadRequest(
                    "لا يمكن اختيار تاريخ دخول سابق.");
            }

            if (endDate <= startDate)
            {
                return BadRequest(
                    "تاريخ الخروج يجب أن يكون بعد تاريخ الدخول.");
            }

            var bookingDays =
                (endDate - startDate).Days;

            /* ================================================================
               قراءة المنشأة من قاعدة البيانات
               ================================================================ */

            var place =
                await _context.Places
                    .AsNoTracking()
                    .Include(place =>
                        place.Category)
                    .Include(place =>
                        place.City)
                    .Include(place =>
                        place.PlacePolicy)
                    .FirstOrDefaultAsync(place =>
                        place.PlaceId ==
                        placeId);

            if (place == null)
            {
                return NotFound(
                    "المنشأة غير موجودة.");
            }

            if (!IsActiveState(place.Status))
            {
                return NotFound(
                    "المنشأة غير مفعلة حاليًا.");
            }

            /*
             * التحقق من الحد الأقصى للحجز المستقبلي
             * الموجود في PlacePolicies.
             */
            var maximumHorizonMonths =
                place.PlacePolicy?
                    .MaximumHorizonMonths ?? 0;

            if (maximumHorizonMonths > 0)
            {
                var maximumAllowedDate =
                    DateTime.Today
                        .AddMonths(
                            maximumHorizonMonths);

                if (endDate >
                    maximumAllowedDate)
                {
                    return BadRequest(
                        $"لا يمكن الحجز لأكثر من " +
                        $"{maximumHorizonMonths} أشهر مقدمًا.");
                }
            }

            var minimumNoticeHours =
                Math.Max(
                    0,
                    place.PlacePolicy?
                        .MinimumNoticeHours ?? 0);

            if (minimumNoticeHours > 0)
            {
                var requestedCheckInAt =
                    startDate.Date.Add(
                        GetPolicyCheckInTime(
                            place));

                if (
                    requestedCheckInAt <
                    DateTime.Now.AddHours(
                        minimumNoticeHours))
                {
                    return BadRequest(
                        $"يجب إجراء الحجز قبل الموعد بـ {minimumNoticeHours} ساعة على الأقل.");
                }
            }

            if (ContainsWeeklyClosedDate(
                    place,
                    startDate,
                    endDate))
            {
                return BadRequest(
                    "الفترة المختارة تتضمن يوم إغلاق أسبوعي حسب سياسة المنشأة.");
            }

            /* ================================================================
               التحقق من الأيام التي أغلقها المالك
               ================================================================ */

            var containsClosedDate =
                await _context
                    .PlaceCalendarExceptions
                    .AsNoTracking()
                    .AnyAsync(exception =>
                        exception.PlaceId ==
                            placeId &&

                        exception.ExceptionDate >=
                            startDate &&

                        exception.ExceptionDate <
                            endDate &&

                        exception.ExceptionType ==
                            "Closed");

            if (containsClosedDate)
            {
                return BadRequest(
                    "الفترة المختارة تحتوي على يوم مغلق من قبل المنشأة.");
            }

            /* ================================================================
               التحقق من الخدمات المختارة
               ================================================================ */

            var requestedServiceIds =
                serviceIds?
                    .Where(id =>
                        id > 0)
                    .Distinct()
                    .ToList() ??
                new List<int>();

            var selectedServiceIds =
                requestedServiceIds.Count == 0
                    ? new List<int>()
                    : await _context
                        .PlaceAdditionalServices
                        .AsNoTracking()
                        .Where(service =>
                            requestedServiceIds.Contains(
                                service.ServiceId) &&

                            service.PlaceId ==
                                placeId &&

                            service.IsActive ==
                                true)
                        .Select(service =>
                            service.ServiceId)
                        .ToListAsync();

            if (selectedServiceIds.Count !=
                requestedServiceIds.Count)
            {
                return BadRequest(
                    "إحدى الخدمات الإضافية غير موجودة أو غير متاحة.");
            }

            /* ================================================================
               العرض القادم من صفحة التفاصيل
               ---------------------------------------------------------------
               هذه الصفحة تعرض معاينة موثوقة قدر الإمكان، لكن Checkout يظل
               المرجع النهائي لتطبيق الخصم وحفظه ماليًا داخل الحجز.
               ================================================================ */

            var requestedPromotionId =
                promotionId.HasValue && promotionId.Value > 0
                    ? promotionId
                    : null;

            var selectedPromotion =
                requestedPromotionId.HasValue
                    ? await _context.Promotions
                        .AsNoTracking()
                        .Where(promotion =>
                            promotion.PromotionId == requestedPromotionId.Value &&
                            promotion.PlaceId == placeId &&
                            promotion.IsActive == true &&
                            (
                                promotion.Status == "Active" ||
                                promotion.Status == "Scheduled"
                            ))
                        .Select(promotion => new
                        {
                            promotion.PromotionId,
                            promotion.PromotionTitle,
                            promotion.OfferMode,
                            promotion.RewardType,
                            promotion.RequiredQuantity,
                            promotion.RewardQuantity,
                            promotion.IsRewardRepeatable,
                            promotion.RewardServiceId,
                            promotion.SpecialPrice,
                            promotion.DiscountType,
                            promotion.DiscountValue,
                            promotion.MaximumDiscountAmount,
                            promotion.MinimumBookingAmount,
                            promotion.MinimumNights,
                            promotion.MinimumPeople,
                            promotion.IsAutomatic,
                            promotion.PromoCode,
                            promotion.RoomId,
                            promotion.PackageId,
                            promotion.ServiceId,
                            promotion.StartDate,
                            promotion.EndDate,
                            promotion.StayStartDate,
                            promotion.StayEndDate,
                            promotion.ApplicableDaysCsv,
                            promotion.TotalUsageLimit,
                            promotion.PerCustomerUsageLimit
                        })
                        .FirstOrDefaultAsync()
                    : null;

            string selectedRewardServiceName = string.Empty;
            if (selectedPromotion?.RewardServiceId is int rewardServiceId &&
                rewardServiceId > 0)
            {
                selectedRewardServiceName =
                    await _context.PlaceAdditionalServices
                        .AsNoTracking()
                        .Where(service =>
                            service.ServiceId == rewardServiceId &&
                            service.PlaceId == placeId &&
                            service.IsActive == true)
                        .Select(service => service.ServiceName)
                        .FirstOrDefaultAsync() ?? string.Empty;
            }

            var currentCustomerId = 0;
            if (User.Identity?.IsAuthenticated == true)
            {
                int.TryParse(
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out currentCustomerId);
            }

            bool promotionIsEligibleForStay =
                selectedPromotion != null;

            string promotionStateMessage =
                string.Empty;

            if (selectedPromotion != null)
            {
                var today = DateTime.Today;

                if (
                    selectedPromotion.StartDate.Date > today ||
                    selectedPromotion.EndDate.Date < today)
                {
                    promotionIsEligibleForStay = false;
                    promotionStateMessage =
                        "العرض غير فعال في الوقت الحالي.";
                }

                if (
                    promotionIsEligibleForStay &&
                    selectedPromotion.MinimumNights.HasValue &&
                    bookingDays < selectedPromotion.MinimumNights.Value)
                {
                    promotionIsEligibleForStay = false;
                    promotionStateMessage =
                        $"هذا العرض يحتاج إلى {selectedPromotion.MinimumNights.Value} ليلة على الأقل.";
                }

                if (
                    promotionIsEligibleForStay &&
                    string.Equals(selectedPromotion.OfferMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(selectedPromotion.RewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
                {
                    var required = selectedPromotion.MinimumNights.HasValue &&
                                   selectedPromotion.MinimumNights.Value > 0
                        ? selectedPromotion.MinimumNights.Value
                        : Math.Max(1, selectedPromotion.RequiredQuantity ?? 1);
                    var reward = Math.Max(1, selectedPromotion.RewardQuantity ?? 1);

                    if (bookingDays < required)
                    {
                        promotionStateMessage =
                            $"للاستفادة من العرض اختر {required} ليالٍ على الأقل؛ عند تحقق الشرط تحصل على {reward} ليلة مجانًا.";
                    }
                }

                if (
                    promotionIsEligibleForStay &&
                    selectedPromotion.StayStartDate.HasValue &&
                    startDate < selectedPromotion.StayStartDate.Value.Date)
                {
                    promotionIsEligibleForStay = false;
                    promotionStateMessage =
                        "تاريخ الدخول المختار يسبق فترة الاستفادة من العرض.";
                }

                if (
                    promotionIsEligibleForStay &&
                    selectedPromotion.StayEndDate.HasValue &&
                    endDate > selectedPromotion.StayEndDate.Value.Date.AddDays(1))
                {
                    promotionIsEligibleForStay = false;
                    promotionStateMessage =
                        "فترة الإقامة المختارة تتجاوز آخر يوم مسموح به للعرض.";
                }

                if (
                    promotionIsEligibleForStay &&
                    !string.IsNullOrWhiteSpace(
                        selectedPromotion.ApplicableDaysCsv))
                {
                    var allowedDays =
                        selectedPromotion.ApplicableDaysCsv
                            .Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries)
                            .ToHashSet(
                                StringComparer.OrdinalIgnoreCase);

                    if (allowedDays.Count > 0)
                    {
                        var allStayDaysAllowed = true;

                        for (
                            var day = startDate;
                            day < endDate;
                            day = day.AddDays(1))
                        {
                            if (!allowedDays.Contains(
                                    day.DayOfWeek.ToString()))
                            {
                                allStayDaysAllowed = false;
                                break;
                            }
                        }

                        if (!allStayDaysAllowed)
                        {
                            promotionIsEligibleForStay = false;
                            promotionStateMessage =
                                "العرض لا يشمل كل أيام الإقامة التي اخترتها.";
                        }
                    }
                }

                if (promotionIsEligibleForStay)
                {
                    try
                    {
                        var totalUsage =
                            await _context.Bookings
                                .AsNoTracking()
                                .CountAsync(booking =>
                                    booking.PromotionId ==
                                        selectedPromotion.PromotionId &&
                                    booking.BookingState != "Cancelled" &&
                                    booking.BookingState != "Rejected" &&
                                    booking.BookingState != "Expired");

                        if (
                            selectedPromotion.TotalUsageLimit.HasValue &&
                            totalUsage >=
                                selectedPromotion.TotalUsageLimit.Value)
                        {
                            promotionIsEligibleForStay = false;
                            promotionStateMessage =
                                "اكتمل الحد المتاح لاستخدام هذا العرض.";
                        }

                        if (
                            promotionIsEligibleForStay &&
                            currentCustomerId > 0 &&
                            selectedPromotion.PerCustomerUsageLimit.HasValue)
                        {
                            var customerUsage =
                                await _context.Bookings
                                    .AsNoTracking()
                                    .CountAsync(booking =>
                                        booking.CustomerId == currentCustomerId &&
                                        booking.PromotionId ==
                                            selectedPromotion.PromotionId &&
                                        booking.BookingState != "Cancelled" &&
                                        booking.BookingState != "Rejected" &&
                                        booking.BookingState != "Expired");

                            if (customerUsage >=
                                selectedPromotion.PerCustomerUsageLimit.Value)
                            {
                                promotionIsEligibleForStay = false;
                                promotionStateMessage =
                                    "وصلت إلى الحد المسموح لك لاستخدام هذا العرض.";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        /*
                         * لا نكسر الصفحة إذا كانت نسخة قاعدة البيانات ما زالت
                         * قيد المزامنة. Checkout سيعيد التحقق قبل الحفظ.
                         */
                        Console.WriteLine(
                            $"AvailableRooms promotion usage warning: {ex.Message}");
                    }
                }

                if (
                    promotionIsEligibleForStay &&
                    !selectedPromotion.RoomId.HasValue &&
                    (selectedPromotion.PackageId.HasValue ||
                     selectedPromotion.ServiceId.HasValue))
                {
                    /*
                     * عرض الباقة أو الخدمة لا يغير سعر الغرفة في هذه الصفحة،
                     * لكنه يبقى محمولًا إلى Checkout كي يعالج في موضعه الصحيح.
                     */
                    promotionStateMessage =
                        selectedPromotion.PackageId.HasValue
                            ? "العرض المختار مرتبط بباقة، وسيتم التحقق منه في خطوة الحجز المناسبة."
                            : "العرض المختار مرتبط بخدمة إضافية، وسيتم التحقق منه عند مراجعة الحجز.";
                }
            }
            else if (requestedPromotionId.HasValue)
            {
                promotionStateMessage =
                    "العرض المطلوب غير متاح لهذه المنشأة أو لم يعد فعالًا.";
            }

            /* ================================================================
               قراءة الغرف الحقيقية
               ================================================================ */

            var rooms =
                await _context.Rooms
                    .AsNoTracking()
                    .Where(room =>
                        room.PlaceId ==
                            placeId &&

                        room.IsAvailable ==
                            true &&

                        room.RoomUnits.Any(unit =>
                            unit.IsAvailable == true))
                    .OrderBy(room =>
                        room.Price)
                    .ThenBy(room =>
                        room.RoomType)
                    .ThenBy(room =>
                        room.RoomId)
                    .ToListAsync();

            var roomIds =
                rooms
                    .Select(room =>
                        room.RoomId)
                    .ToList();

            var activeRoomUnits =
                roomIds.Count == 0
                    ? new List<RoomUnit>()
                    : await _context.RoomUnits
                        .AsNoTracking()
                        .Where(unit =>
                            unit.PlaceId == placeId &&
                            roomIds.Contains(unit.RoomId) &&
                            unit.IsAvailable == true)
                        .OrderBy(unit =>
                            unit.FloorNumber)
                        .ThenBy(unit =>
                            unit.RoomNumber)
                        .ToListAsync();

            var activeUnitIds =
                activeRoomUnits
                    .Select(unit =>
                        unit.RoomUnitId)
                    .ToList();

            /* ================================================================
               حالات الحجز التي تمنع إعادة حجز نفس الكمية
               ================================================================ */

            var blockingStates =
                new[]
                {
            "Pending",
            "AwaitingPayment",
            "PaymentReview",
            "Confirmed"
                };

            var now =
                DateTime.Now;

            var blockAdjacentRoomBookings =
                RequiresAdjacentRoomBufferBlock(
                    place);

            var blockingAssignmentStates =
                new[]
                {
                    "Held",
                    "Confirmed"
                };

            var assignedUnitCounts =
                activeUnitIds.Count == 0
                    ? new Dictionary<int, int>()
                    : await _context.BookingRoomUnits
                        .AsNoTracking()
                        .Where(link =>
                            activeUnitIds.Contains(
                                link.RoomUnitId) &&

                            blockingAssignmentStates.Contains(
                                link.AssignmentStatus) &&

                            blockingStates.Contains(
                                link.Booking.BookingState ??
                                    string.Empty) &&

                            (
                                link.Booking.BookingState !=
                                    "AwaitingPayment" ||

                                !link.Booking.TimerExpiredAt.HasValue ||

                                link.Booking.TimerExpiredAt > now
                            ) &&

                            (
                                blockAdjacentRoomBookings
                                    ? link.Booking.StartDate <= endDate
                                    : link.Booking.StartDate < endDate
                            ) &&

                            (
                                blockAdjacentRoomBookings
                                    ? (
                                        link.Booking.EndDate ??
                                        link.Booking.StartDate.AddDays(1)
                                      ) >= startDate
                                    : (
                                        link.Booking.EndDate ??
                                        link.Booking.StartDate.AddDays(1)
                                      ) > startDate
                            ))
                        .GroupBy(link =>
                            link.RoomUnit.RoomId)
                        .Select(group =>
                            new
                            {
                                RoomId = group.Key,
                                ReservedQuantity =
                                    group
                                        .Select(link =>
                                            link.RoomUnitId)
                                        .Distinct()
                                        .Count()
                            })
                        .ToDictionaryAsync(
                            item => item.RoomId,
                            item => item.ReservedQuantity);

            /*
             * توافق انتقالي للحجوزات الموجودة قبل إضافة BookingRoomUnits.
             * لا نحسب أي حجز لديه ربط فعلي حتى لا يتكرر احتسابه.
             */
            var legacyReservedCounts =
                roomIds.Count == 0
                    ? new Dictionary<int, int>()
                    : await _context.Bookings
                        .AsNoTracking()
                        .Where(booking =>
                            booking.RoomId.HasValue &&

                            roomIds.Contains(
                                booking.RoomId.Value) &&

                            !booking.BookingRoomUnits.Any() &&

                            blockingStates.Contains(
                                booking.BookingState ??
                                    string.Empty) &&

                            (
                                booking.BookingState !=
                                    "AwaitingPayment" ||

                                !booking.TimerExpiredAt.HasValue ||

                                booking.TimerExpiredAt > now
                            ) &&

                            (
                                blockAdjacentRoomBookings
                                    ? booking.StartDate <= endDate
                                    : booking.StartDate < endDate
                            ) &&

                            (
                                blockAdjacentRoomBookings
                                    ? (
                                        booking.EndDate ??
                                        booking.StartDate.AddDays(1)
                                      ) >= startDate
                                    : (
                                        booking.EndDate ??
                                        booking.StartDate.AddDays(1)
                                      ) > startDate
                            ))
                        .GroupBy(booking =>
                            booking.RoomId!.Value)
                        .Select(group =>
                            new
                            {
                                RoomId = group.Key,
                                ReservedQuantity =
                                    group.Sum(booking =>
                                        booking.Quantity)
                            })
                        .ToDictionaryAsync(
                            item => item.RoomId,
                            item => item.ReservedQuantity);

            var inventoryCounts =
                activeRoomUnits
                    .GroupBy(unit =>
                        unit.RoomId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Count());

            var floorCounts =
                activeRoomUnits
                    .GroupBy(unit =>
                        unit.RoomId)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(unit =>
                                unit.FloorNumber)
                            .Distinct()
                            .Count());

            /* ================================================================
               العملة وصورة المنشأة
               ================================================================ */

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            string? exchangeRateRegion =
                place.City?
                    .ExchangeRateRegion;

            var currencyLabel =
                currencyContext.Currency;

            var placeImageUrl =
                NormalizeImagePath(
                    place.CoverImage);

            if (string.IsNullOrWhiteSpace(
                    placeImageUrl))
            {
                placeImageUrl =
                    NormalizeImagePath(
                        place.LogoImage);
            }

            if (string.IsNullOrWhiteSpace(
                    placeImageUrl))
            {
                placeImageUrl =
                    NormalizeImagePath(
                        place.Category?
                            .ImagePath);
            }

            if (string.IsNullOrWhiteSpace(
                    placeImageUrl))
            {
                placeImageUrl =
                    "/images/bg1.jpg";
            }

            /* ================================================================
               بناء DTO للغرف المتبقية فقط
               ================================================================ */

            var availableRoomModels =
                new List<AvailableRoomItemDto>();

            foreach (var room in rooms)
            {
                inventoryCounts.TryGetValue(
                    room.RoomId,
                    out var totalQuantity);

                assignedUnitCounts.TryGetValue(
                    room.RoomId,
                    out var assignedQuantity);

                legacyReservedCounts.TryGetValue(
                    room.RoomId,
                    out var legacyReservedQuantity);

                floorCounts.TryGetValue(
                    room.RoomId,
                    out var floorsCount);

                totalQuantity =
                    Math.Max(
                        0,
                        totalQuantity);

                var reservedQuantity =
                    Math.Min(
                        totalQuantity,
                        Math.Max(
                            0,
                            assignedQuantity) +
                        Math.Max(
                            0,
                            legacyReservedQuantity));

                var remainingQuantity =
                    Math.Max(
                        0,
                        totalQuantity -
                        reservedQuantity);

                if (remainingQuantity <= 0)
                {
                    continue;
                }

                var roomImages =
                    ParseRoomImages(
                        room.ImageUrl);

                var mainImageUrl =
                    roomImages.FirstOrDefault() ??
                    placeImageUrl;

                var originalStayPrice =
                    Math.Round(
                        room.Price * bookingDays,
                        2);

                int? roomPromotionId = null;
                string roomPromotionTitle = string.Empty;
                string roomPromotionDiscountDisplay = string.Empty;
                string roomPromotionOfferMode = "Discount";
                string roomPromotionRewardType = string.Empty;
                int? roomPromotionRequiredQuantity = null;
                int? roomPromotionRewardQuantity = null;
                bool roomPromotionIsRewardRepeatable = false;
                int roomPromotionAppliedFreeQuantity = 0;
                int? roomPromotionRewardServiceId = null;
                string roomPromotionRewardServiceName = string.Empty;
                decimal? roomPromotionSpecialPrice = null;
                string roomPromotionOfferSummary = string.Empty;
                bool roomPromotionApplicable = false;
                bool roomPromotionSelected = false;
                bool roomPromotionRequiresPeopleConfirmation = false;
                int? roomPromotionMinimumPeople = null;
                bool roomPromotionRequiresCode = false;
                string roomPromotionCode = string.Empty;
                string roomPromotionNote = string.Empty;
                decimal promotionDiscountAmount = 0m;
                decimal discountedStayPrice = originalStayPrice;
                decimal discountedNightlyPrice = room.Price;

                if (selectedPromotion != null && promotionIsEligibleForStay)
                {
                    var targetsRoomPrice =
                        !selectedPromotion.PackageId.HasValue &&
                        !selectedPromotion.ServiceId.HasValue &&
                        (!selectedPromotion.RoomId.HasValue ||
                         selectedPromotion.RoomId.Value == room.RoomId);

                    if (targetsRoomPrice)
                    {
                        roomPromotionId = selectedPromotion.PromotionId;
                        roomPromotionTitle =
                            selectedPromotion.PromotionTitle ?? "عرض خاص";
                        roomPromotionOfferMode =
                            string.IsNullOrWhiteSpace(selectedPromotion.OfferMode)
                                ? "Discount"
                                : selectedPromotion.OfferMode;
                        roomPromotionRewardType =
                            selectedPromotion.RewardType ?? string.Empty;
                        roomPromotionRequiredQuantity =
                            selectedPromotion.RequiredQuantity;
                        roomPromotionRewardQuantity =
                            selectedPromotion.RewardQuantity;
                        roomPromotionIsRewardRepeatable = selectedPromotion.IsRewardRepeatable;
                        roomPromotionRewardServiceId =
                            selectedPromotion.RewardServiceId;
                        roomPromotionRewardServiceName =
                            selectedRewardServiceName;
                        roomPromotionSpecialPrice =
                            selectedPromotion.SpecialPrice;

                        roomPromotionSelected = true;
                        roomPromotionMinimumPeople = selectedPromotion.MinimumPeople;
                        roomPromotionRequiresPeopleConfirmation =
                            selectedPromotion.MinimumPeople.HasValue &&
                            selectedPromotion.MinimumPeople.Value > 0;
                        roomPromotionRequiresCode =
                            selectedPromotion.IsAutomatic == false &&
                            !string.IsNullOrWhiteSpace(selectedPromotion.PromoCode);
                        roomPromotionCode =
                            selectedPromotion.PromoCode?.Trim() ?? string.Empty;

                        var meetsMinimumBookingAmount =
                            !selectedPromotion.MinimumBookingAmount.HasValue ||
                            selectedPromotion.MinimumBookingAmount.Value <= 0 ||
                            originalStayPrice >= selectedPromotion.MinimumBookingAmount.Value;

                        if (meetsMinimumBookingAmount)
                        {
                            var isConditional =
                                string.Equals(
                                    roomPromotionOfferMode,
                                    "ConditionalReward",
                                    StringComparison.OrdinalIgnoreCase);

                            var rewardType = roomPromotionRewardType;
                            decimal rawDiscount = 0m;
                            bool previewSupported = true;

                            if (!isConditional)
                            {
                                var isPercentage =
                                    string.Equals(
                                        selectedPromotion.DiscountType,
                                        "Percentage",
                                        StringComparison.OrdinalIgnoreCase);

                                rawDiscount = isPercentage
                                    ? originalStayPrice * selectedPromotion.DiscountValue / 100m
                                    : selectedPromotion.DiscountValue;

                                if (selectedPromotion.MaximumDiscountAmount.HasValue &&
                                    selectedPromotion.MaximumDiscountAmount.Value > 0)
                                {
                                    rawDiscount = Math.Min(
                                        rawDiscount,
                                        selectedPromotion.MaximumDiscountAmount.Value);
                                }

                                roomPromotionOfferSummary = isPercentage
                                    ? $"خصم {selectedPromotion.DiscountValue:0.##}%"
                                    : $"خصم {currencyContext.Format(selectedPromotion.DiscountValue, exchangeRateRegion)}";
                            }
                            else if (string.Equals(
                                         rewardType,
                                         "PercentageDiscount",
                                         StringComparison.OrdinalIgnoreCase))
                            {
                                rawDiscount =
                                    originalStayPrice * selectedPromotion.DiscountValue / 100m;

                                if (selectedPromotion.MaximumDiscountAmount.HasValue &&
                                    selectedPromotion.MaximumDiscountAmount.Value > 0)
                                {
                                    rawDiscount = Math.Min(
                                        rawDiscount,
                                        selectedPromotion.MaximumDiscountAmount.Value);
                                }

                                roomPromotionOfferSummary =
                                    $"خصم {selectedPromotion.DiscountValue:0.##}% بعد تحقق الشروط";
                            }
                            else if (string.Equals(
                                         rewardType,
                                         "FixedDiscount",
                                         StringComparison.OrdinalIgnoreCase))
                            {
                                rawDiscount = selectedPromotion.DiscountValue;
                                if (selectedPromotion.MaximumDiscountAmount.HasValue &&
                                    selectedPromotion.MaximumDiscountAmount.Value > 0)
                                {
                                    rawDiscount = Math.Min(
                                        rawDiscount,
                                        selectedPromotion.MaximumDiscountAmount.Value);
                                }

                                roomPromotionOfferSummary =
                                    $"خصم {currencyContext.Format(selectedPromotion.DiscountValue, exchangeRateRegion)} بعد تحقق الشروط";
                            }
                            else if (string.Equals(
                                         rewardType,
                                         "SpecialPrice",
                                         StringComparison.OrdinalIgnoreCase) &&
                                     selectedPromotion.SpecialPrice.HasValue &&
                                     selectedPromotion.SpecialPrice.Value > 0)
                            {
                                var specialStayPrice =
                                    selectedPromotion.SpecialPrice.Value * bookingDays;
                                rawDiscount =
                                    Math.Max(0m, originalStayPrice - specialStayPrice);
                                roomPromotionOfferSummary =
                                    $"سعر خاص {currencyContext.Format(selectedPromotion.SpecialPrice.Value, exchangeRateRegion)} لليلة";
                            }
                            else if (string.Equals(
                                         rewardType,
                                         "FreeQuantity",
                                         StringComparison.OrdinalIgnoreCase))
                            {
                                var required = selectedPromotion.MinimumNights.HasValue &&
                                               selectedPromotion.MinimumNights.Value > 0
                                    ? selectedPromotion.MinimumNights.Value
                                    : Math.Max(1, selectedPromotion.RequiredQuantity ?? 1);
                                var reward = Math.Max(
                                    1,
                                    selectedPromotion.RewardQuantity ?? 1);

                                var completedRequirements = bookingDays / required;
                                var repeatCount = selectedPromotion.IsRewardRepeatable
                                    ? completedRequirements
                                    : completedRequirements > 0 ? 1 : 0;
                                var freeNights = Math.Min(
                                    bookingDays,
                                    Math.Max(0, repeatCount * reward));

                                roomPromotionRequiredQuantity = required;
                                roomPromotionRewardQuantity = reward;
                                roomPromotionIsRewardRepeatable = selectedPromotion.IsRewardRepeatable;
                                roomPromotionAppliedFreeQuantity = freeNights;
                                roomPromotionOfferSummary = selectedPromotion.IsRewardRepeatable
                                    ? $"كل {required} ليلة تمنح {reward} ليلة مجانًا — المستحق الآن {freeNights}"
                                    : $"احجز {required} ليلة واحصل على {reward} ليلة مجانًا — مرة واحدة فقط";

                                if (freeNights > 0)
                                {
                                    rawDiscount = Math.Min(
                                        originalStayPrice,
                                        room.Price * freeNights);
                                }
                                else
                                {
                                    previewSupported = false;
                                    roomPromotionNote =
                                        $"اختر إقامة لا تقل عن {required} ليالٍ للاستفادة من العرض.";
                                }
                            }
                            else if (string.Equals(
                                         rewardType,
                                         "FreeService",
                                         StringComparison.OrdinalIgnoreCase))
                            {
                                previewSupported = false;
                                roomPromotionApplicable = true;
                                var rewardQuantity = Math.Max(
                                    1,
                                    selectedPromotion.RewardQuantity ?? 1);
                                roomPromotionRewardQuantity = rewardQuantity;
                                roomPromotionOfferSummary =
                                    !string.IsNullOrWhiteSpace(selectedRewardServiceName)
                                        ? (rewardQuantity > 1
                                            ? $"{rewardQuantity} × {selectedRewardServiceName} مجانًا"
                                            : $"{selectedRewardServiceName} مجانًا")
                                        : "خدمة إضافية مجانية";
                                roomPromotionNote =
                                    "المكافأة خدمة مجانية، لذلك لا نخصم من سعر الغرفة في هذه الصفحة. يتم اعتمادها نهائيًا في المراجعة.";
                            }
                            else if (string.Equals(
                                         rewardType,
                                         "ServiceSpecialPrice",
                                         StringComparison.OrdinalIgnoreCase))
                            {
                                previewSupported = false;
                                roomPromotionApplicable = true;
                                roomPromotionOfferSummary =
                                    !string.IsNullOrWhiteSpace(selectedRewardServiceName) &&
                                    selectedPromotion.SpecialPrice.HasValue
                                        ? $"{selectedRewardServiceName} بسعر خاص {currencyContext.Format(selectedPromotion.SpecialPrice.Value, exchangeRateRegion)}"
                                        : "خدمة بسعر خاص";
                                roomPromotionNote =
                                    "السعر الخاص يخص خدمة إضافية، وسيعاد التحقق من اختيار الخدمة في Checkout.";
                            }
                            else
                            {
                                previewSupported = false;
                                roomPromotionNote =
                                    "سيعاد التحقق من مكافأة العرض في خطوة مراجعة الحجز.";
                            }

                            if (previewSupported)
                            {
                                rawDiscount = Math.Min(
                                    originalStayPrice,
                                    Math.Max(0m, rawDiscount));

                                promotionDiscountAmount = Math.Round(rawDiscount, 2);
                                discountedStayPrice = Math.Round(
                                    Math.Max(0m, originalStayPrice - promotionDiscountAmount),
                                    2);
                                discountedNightlyPrice = bookingDays > 0
                                    ? Math.Round(discountedStayPrice / bookingDays, 2)
                                    : room.Price;
                                roomPromotionApplicable = promotionDiscountAmount > 0;
                            }

                            roomPromotionDiscountDisplay =
                                !string.IsNullOrWhiteSpace(roomPromotionOfferSummary)
                                    ? roomPromotionOfferSummary
                                    : "عرض خاص";

                            if (string.IsNullOrWhiteSpace(roomPromotionNote))
                            {
                                roomPromotionNote =
                                    roomPromotionRequiresPeopleConfirmation
                                        ? $"السعر المعروض تقديري حتى تحديد {selectedPromotion.MinimumPeople!.Value} أشخاص على الأقل."
                                        : roomPromotionRequiresCode
                                            ? "العرض يتطلب إدخال الكود في خطوة التأكيد، والسعر النهائي يعاد حسابه على الخادم."
                                            : "يُعاد التحقق من العرض والسعر النهائي في خطوة التأكيد.";
                            }
                        }
                        else
                        {
                            roomPromotionOfferSummary = "عرض بشروط";
                            roomPromotionDiscountDisplay = roomPromotionOfferSummary;
                            roomPromotionNote =
                                $"يحتاج العرض إلى حد أدنى للحجز " +
                                $"{currencyContext.Format(selectedPromotion.MinimumBookingAmount!.Value, exchangeRateRegion)}. " +
                                "قد يتحقق الشرط عند زيادة عدد الوحدات.";
                        }
                    }
                }

                availableRoomModels.Add(
                    new AvailableRoomItemDto
                    {
                        RoomId =
                            room.RoomId,

                        PlaceId =
                            room.PlaceId,

                        // لا نرسل رقم وحدة فعلية قبل إسنادها للحجز.
                        // أبقينا الخاصية فارغة فقط للتوافق مع الواجهة القديمة.
                        RoomNumber =
                            string.Empty,

                        RoomType =
                            room.RoomType?
                                .Trim() ??
                            string.Empty,

                        Description =
                            room.Description?
                                .Trim() ??
                            string.Empty,

                        NightlyPrice =
                            Math.Round(
                                room.Price,
                                2),

                        NightlyPriceDisplay =
                            currencyContext.Convert(
                                room.Price,
                                exchangeRateRegion),

                        NightlyPriceText =
                            currencyContext.Format(
                                room.Price,
                                exchangeRateRegion),

                        StayPrice =
                            originalStayPrice,

                        StayPriceDisplay =
                            currencyContext.Convert(
                                originalStayPrice,
                                exchangeRateRegion),

                        StayPriceText =
                            currencyContext.Format(
                                originalStayPrice,
                                exchangeRateRegion),

                        PromotionId =
                            roomPromotionId,

                        PromotionTitle =
                            roomPromotionTitle,

                        PromotionOfferMode = roomPromotionOfferMode,
                        PromotionRewardType = roomPromotionRewardType,
                        PromotionRequiredQuantity = roomPromotionRequiredQuantity,
                        PromotionRewardQuantity = roomPromotionRewardQuantity,
                        PromotionIsRewardRepeatable = roomPromotionIsRewardRepeatable,
                        PromotionAppliedFreeQuantity = roomPromotionAppliedFreeQuantity,
                        PromotionRewardServiceId = roomPromotionRewardServiceId,
                        PromotionRewardServiceName = roomPromotionRewardServiceName,
                        PromotionSpecialPrice = roomPromotionSpecialPrice,
                        PromotionSpecialPriceDisplay = roomPromotionSpecialPrice.HasValue
                            ? currencyContext.Convert(roomPromotionSpecialPrice.Value, exchangeRateRegion)
                            : null,
                        PromotionSpecialPriceText = roomPromotionSpecialPrice.HasValue
                            ? currencyContext.Format(roomPromotionSpecialPrice.Value, exchangeRateRegion)
                            : string.Empty,
                        PromotionOfferSummary = roomPromotionOfferSummary,

                        PromotionDiscountDisplay =
                            roomPromotionDiscountDisplay,

                        PromotionDiscountType =
                            roomPromotionId.HasValue
                                ? selectedPromotion?.DiscountType ?? string.Empty
                                : string.Empty,

                        PromotionDiscountValue =
                            roomPromotionId.HasValue
                                ? selectedPromotion?.DiscountValue ?? 0m
                                : 0m,

                        PromotionDiscountValueDisplay =
                            roomPromotionId.HasValue &&
                            selectedPromotion != null &&
                            !string.Equals(
                                selectedPromotion.DiscountType,
                                "Percentage",
                                StringComparison.OrdinalIgnoreCase)
                                ? currencyContext.Convert(
                                    selectedPromotion.DiscountValue,
                                    exchangeRateRegion)
                                : 0m,

                        PromotionDiscountValueText =
                            roomPromotionId.HasValue &&
                            selectedPromotion != null &&
                            !string.Equals(
                                selectedPromotion.DiscountType,
                                "Percentage",
                                StringComparison.OrdinalIgnoreCase)
                                ? currencyContext.Format(
                                    selectedPromotion.DiscountValue,
                                    exchangeRateRegion)
                                : string.Empty,

                        PromotionMaximumDiscountAmount =
                            roomPromotionId.HasValue
                                ? selectedPromotion?.MaximumDiscountAmount
                                : null,

                        PromotionMaximumDiscountAmountDisplay =
                            roomPromotionId.HasValue &&
                            selectedPromotion?.MaximumDiscountAmount is decimal maximumDiscountAmount
                                ? currencyContext.Convert(
                                    maximumDiscountAmount,
                                    exchangeRateRegion)
                                : null,

                        PromotionMaximumDiscountAmountText =
                            roomPromotionId.HasValue &&
                            selectedPromotion?.MaximumDiscountAmount is decimal maximumDiscountTextValue
                                ? currencyContext.Format(
                                    maximumDiscountTextValue,
                                    exchangeRateRegion)
                                : string.Empty,

                        PromotionMinimumBookingAmount =
                            roomPromotionId.HasValue
                                ? selectedPromotion?.MinimumBookingAmount
                                : null,

                        PromotionMinimumBookingAmountDisplay =
                            roomPromotionId.HasValue &&
                            selectedPromotion?.MinimumBookingAmount is decimal minimumBookingAmount
                                ? currencyContext.Convert(
                                    minimumBookingAmount,
                                    exchangeRateRegion)
                                : null,

                        PromotionMinimumBookingAmountText =
                            roomPromotionId.HasValue &&
                            selectedPromotion?.MinimumBookingAmount is decimal minimumBookingTextValue
                                ? currencyContext.Format(
                                    minimumBookingTextValue,
                                    exchangeRateRegion)
                                : string.Empty,

                        IsPromotionSelected =
                            roomPromotionSelected,

                        IsPromotionApplicable =
                            roomPromotionApplicable,

                        PromotionRequiresPeopleConfirmation =
                            roomPromotionRequiresPeopleConfirmation,

                        PromotionMinimumPeople =
                            roomPromotionMinimumPeople,

                        PromotionRequiresCode =
                            roomPromotionRequiresCode,

                        PromotionCode =
                            roomPromotionCode,

                        PromotionNote =
                            roomPromotionNote,

                        OriginalStayPrice =
                            originalStayPrice,

                        OriginalStayPriceDisplay =
                            currencyContext.Convert(
                                originalStayPrice,
                                exchangeRateRegion),

                        OriginalStayPriceText =
                            currencyContext.Format(
                                originalStayPrice,
                                exchangeRateRegion),

                        PromotionDiscountAmount =
                            promotionDiscountAmount,

                        PromotionDiscountAmountDisplay =
                            currencyContext.Convert(
                                promotionDiscountAmount,
                                exchangeRateRegion),

                        PromotionDiscountAmountText =
                            currencyContext.Format(
                                promotionDiscountAmount,
                                exchangeRateRegion),

                        DiscountedStayPrice =
                            discountedStayPrice,

                        DiscountedStayPriceDisplay =
                            currencyContext.Convert(
                                discountedStayPrice,
                                exchangeRateRegion),

                        DiscountedStayPriceText =
                            currencyContext.Format(
                                discountedStayPrice,
                                exchangeRateRegion),

                        DiscountedNightlyPrice =
                            discountedNightlyPrice,

                        DiscountedNightlyPriceDisplay =
                            currencyContext.Convert(
                                discountedNightlyPrice,
                                exchangeRateRegion),

                        DiscountedNightlyPriceText =
                            currencyContext.Format(
                                discountedNightlyPrice,
                                exchangeRateRegion),

                        TotalQuantity =
                            totalQuantity,

                        ReservedQuantity =
                            reservedQuantity,

                        RemainingQuantity =
                            remainingQuantity,

                        Capacity =
                            room.MaxAdults +
                                room.MaxChildren > 0
                                ? room.MaxAdults +
                                  room.MaxChildren
                                : ExtractRoomCapacity(
                                    room.Description),

                        AreaSquareMeters =
                            room.AreaSquareMeters,

                        MaxAdults =
                            Math.Max(
                                0,
                                room.MaxAdults),

                        MaxChildren =
                            Math.Max(
                                0,
                                room.MaxChildren),

                        BedsCount =
                            room.BedsCount,

                        BedType =
                            room.BedType?
                                .Trim() ??
                            string.Empty,

                        FloorsCount =
                            Math.Max(
                                0,
                                floorsCount),

                        Images =
                            roomImages,

                        MainImageUrl =
                            mainImageUrl
                    });
            }

            /* ================================================================
               حالة العرض بعد معرفة الغرف المتاحة
               ================================================================ */

            var promotionTargetType =
                selectedPromotion == null
                    ? string.Empty
                    : selectedPromotion.RoomId.HasValue
                        ? "Room"
                        : selectedPromotion.PackageId.HasValue
                            ? "Package"
                            : selectedPromotion.ServiceId.HasValue
                                ? "Service"
                                : "Place";

            var promotionTargetRoomId =
                selectedPromotion?.RoomId;

            var selectedPromotionRoomUnavailable =
                selectedPromotion != null &&
                promotionIsEligibleForStay &&
                selectedPromotion.RoomId.HasValue &&
                availableRoomModels.All(room =>
                    room.RoomId !=
                        selectedPromotion.RoomId.Value);

            if (selectedPromotionRoomUnavailable)
            {
                promotionStateMessage =
                    "العرض ما زال فعالًا، لكن الغرفة المشمولة به غير متاحة خلال الفترة المختارة. جرّب تعديل التواريخ.";
            }
            else if (
                selectedPromotion != null &&
                promotionIsEligibleForStay &&
                string.IsNullOrWhiteSpace(
                    promotionStateMessage))
            {
                promotionStateMessage =
                    selectedPromotion.RoomId.HasValue
                        ? "وجدنا الغرفة المشمولة بالعرض ضمن النتائج المتاحة."
                        : !selectedPromotion.PackageId.HasValue &&
                          !selectedPromotion.ServiceId.HasValue
                            ? "العرض صالح مبدئيًا لهذه الفترة وسيعاد التحقق منه عند تأكيد الحجز."
                            : promotionStateMessage;
            }

            string selectedPromotionOfferSummary = string.Empty;
            if (selectedPromotion != null)
            {
                var isConditional = string.Equals(
                    selectedPromotion.OfferMode,
                    "ConditionalReward",
                    StringComparison.OrdinalIgnoreCase);

                if (!isConditional)
                {
                    selectedPromotionOfferSummary = string.Equals(
                        selectedPromotion.DiscountType,
                        "Percentage",
                        StringComparison.OrdinalIgnoreCase)
                        ? $"خصم {selectedPromotion.DiscountValue:0.##}%"
                        : $"خصم {currencyContext.Format(selectedPromotion.DiscountValue, exchangeRateRegion)}";
                }
                else if (string.Equals(selectedPromotion.RewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
                {
                    var required = selectedPromotion.MinimumNights.HasValue && selectedPromotion.MinimumNights.Value > 0
                        ? selectedPromotion.MinimumNights.Value
                        : Math.Max(1, selectedPromotion.RequiredQuantity ?? 1);
                    var reward = Math.Max(1, selectedPromotion.RewardQuantity ?? 1);
                    selectedPromotionOfferSummary = selectedPromotion.IsRewardRepeatable
                        ? $"احجز {required} ليلة واحصل على {reward} ليلة مجانًا — العرض يتكرر"
                        : $"احجز {required} ليلة واحصل على {reward} ليلة مجانًا — مرة واحدة فقط";
                }
                else if (string.Equals(selectedPromotion.RewardType, "FreeService", StringComparison.OrdinalIgnoreCase))
                {
                    selectedPromotionOfferSummary = !string.IsNullOrWhiteSpace(selectedRewardServiceName)
                        ? $"{selectedRewardServiceName} مجانًا"
                        : "خدمة إضافية مجانية";
                }
                else if (string.Equals(selectedPromotion.RewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase) && selectedPromotion.SpecialPrice.HasValue)
                {
                    selectedPromotionOfferSummary = $"سعر خاص {currencyContext.Format(selectedPromotion.SpecialPrice.Value, exchangeRateRegion)}";
                }
                else if (string.Equals(selectedPromotion.RewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase) && selectedPromotion.SpecialPrice.HasValue)
                {
                    selectedPromotionOfferSummary = !string.IsNullOrWhiteSpace(selectedRewardServiceName)
                        ? $"{selectedRewardServiceName} بسعر خاص {currencyContext.Format(selectedPromotion.SpecialPrice.Value, exchangeRateRegion)}"
                        : $"خدمة بسعر خاص {currencyContext.Format(selectedPromotion.SpecialPrice.Value, exchangeRateRegion)}";
                }
                else if (string.Equals(selectedPromotion.RewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase))
                {
                    selectedPromotionOfferSummary = $"خصم {selectedPromotion.DiscountValue:0.##}% بعد تحقق الشروط";
                }
                else if (string.Equals(selectedPromotion.RewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase))
                {
                    selectedPromotionOfferSummary = $"خصم {currencyContext.Format(selectedPromotion.DiscountValue, exchangeRateRegion)} بعد تحقق الشروط";
                }
                else
                {
                    selectedPromotionOfferSummary = "عرض خاص";
                }
            }

            var selectedPromotionDiscountDisplay = selectedPromotionOfferSummary;

            var effectivePromotionId =
                selectedPromotion != null &&
                promotionIsEligibleForStay
                    ? selectedPromotion.PromotionId
                    : (int?)null;

            /* ================================================================
               بناء نموذج الصفحة الكامل
               ================================================================ */

            var model =
                new AvailableRoomsViewModel
                {
                    PlaceId =
                        place.PlaceId,

                    PlaceName =
                        place.PlaceNameAndDesc,

                    CategoryName =
                        place.Category?
                            .CategoryName ??
                        string.Empty,

                    CityName =
                        place.City?
                            .CityName ??
                        string.Empty,

                    AddressDetail =
                        place.AddressDetail ??
                        string.Empty,

                    PlaceImageUrl =
                        placeImageUrl,

                    CheckInTime =
                        place.PlacePolicy?
                            .CheckInTime ??
                        place.CheckInTime ??
                        string.Empty,

                    CheckOutTime =
                        place.PlacePolicy?
                            .CheckOutTime ??
                        place.CheckOutTime ??
                        string.Empty,

                    CurrencyLabel =
                        currencyLabel,

                    CurrencyCode =
                        currencyContext.Code,

                    CurrencySymbol =
                        currencyContext.Symbol,

                    StartDate =
                        startDate,

                    EndDate =
                        endDate,

                    SelectedServiceIds =
                        selectedServiceIds,

                    RequestedPromotionId =
                        requestedPromotionId,

                    SelectedPromotionId =
                        effectivePromotionId,

                    SelectedPromotionTitle =
                        selectedPromotion?.PromotionTitle?.Trim() ??
                        string.Empty,

                    PromotionOfferMode = selectedPromotion?.OfferMode ?? "Discount",
                    PromotionRewardType = selectedPromotion?.RewardType ?? string.Empty,
                    PromotionRequiredQuantity = selectedPromotion?.RequiredQuantity,
                    PromotionRewardQuantity = selectedPromotion?.RewardQuantity,
                    PromotionIsRewardRepeatable = selectedPromotion?.IsRewardRepeatable ?? false,
                    PromotionRewardServiceId = selectedPromotion?.RewardServiceId,
                    PromotionRewardServiceName = selectedRewardServiceName,
                    PromotionSpecialPrice = selectedPromotion?.SpecialPrice,
                    PromotionSpecialPriceDisplay = selectedPromotion?.SpecialPrice is decimal selectedSpecialPrice
                        ? currencyContext.Convert(selectedSpecialPrice, exchangeRateRegion)
                        : null,
                    PromotionSpecialPriceText = selectedPromotion?.SpecialPrice is decimal selectedSpecialPriceText
                        ? currencyContext.Format(selectedSpecialPriceText, exchangeRateRegion)
                        : string.Empty,
                    PromotionOfferSummary = selectedPromotionOfferSummary,

                    PromotionTargetType =
                        promotionTargetType,

                    PromotionTargetRoomId =
                        promotionTargetRoomId,

                    PromotionDiscountDisplay =
                        selectedPromotionDiscountDisplay,

                    PromotionStateMessage =
                        promotionStateMessage,

                    IsPromotionEligibleForStay =
                        selectedPromotion != null &&
                        promotionIsEligibleForStay,

                    IsSelectedPromotionRoomUnavailable =
                        selectedPromotionRoomUnavailable,

                    PromotionRequiresPeopleConfirmation =
                        selectedPromotion != null &&
                        selectedPromotion.MinimumPeople.HasValue &&
                        selectedPromotion.MinimumPeople.Value > 0,

                    PromotionMinimumPeople =
                        selectedPromotion?.MinimumPeople,

                    PromotionRequiresCode =
                        selectedPromotion != null &&
                        selectedPromotion.IsAutomatic == false &&
                        !string.IsNullOrWhiteSpace(
                            selectedPromotion.PromoCode),

                    PromotionCode =
                        selectedPromotion?.PromoCode?.Trim() ??
                        string.Empty,

                    Rooms =
                        availableRoomModels
                };

            return View(
                model);
        }

        [HttpGet("/Places/Featured")]
        public async Task<IActionResult> Featured()
        {
            var placesDb = await _context.Places
                .Include(p => p.Category)
                .Include(p => p.City)
                .Include(p => p.PlaceImages) // 👈 الإضافة هنا لجلب الصور للأماكن المميزة
                .Where(p => p.IsFeatured == true && p.Status == "Active")
                .ToListAsync();

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            var featuredPlaces = placesDb.Select(p => new FeaturedPlaceDto
            {
                PlaceId = p.PlaceId,
                Name = p.PlaceNameAndDesc,
                CategoryName = p.Category != null ? p.Category.CategoryName : "عام",
                CityName = p.City != null ? p.City.CityName : "",
                Address = p.AddressDetail ?? "",
                Price = p.Price,
                DisplayPriceText = p.Price > 0m
                    ? currencyContext.Format(
                        p.Price,
                        p.City?.ExchangeRateRegion)
                    : null,
                MainImage = p.CoverImage ?? "/images/places/default-main.jpg",
                HasBooking = p.Category != null && p.Category.HasBookingSystem,
                // 👇 سحب الصور الحقيقية بدلاً من الوهمية
                GalleryImages = p.PlaceImages != null && p.PlaceImages.Any() ? p.PlaceImages.Select(img => img.ImagePath).ToList() : new List<string>()
            }).ToList();

            return View(featuredPlaces);
        }

        // =========================================================================
        // 🌟 دالة التفاصيل الرئيسية
        // =========================================================================
        [HttpGet("/Places/Details/{id}")]
        public async Task<IActionResult> Details(
            int id,
            int? promotionId = null,
            int? advertisementId = null)
        {
            if (id <= 0)
            {
                return BadRequest(
                    "رقم المنشأة غير صحيح.");
            }

            var place =
                await _context.Places
                    .AsNoTracking()
                    .Include(p =>
                        p.Category)
                    .Include(p =>
                        p.City)
                    .Include(p =>
                        p.Owner)
                    .Include(p =>
                        p.Rooms)
                    .Include(p =>
                        p.PlaceAdditionalServices)
                    .Include(p =>
                        p.PlaceAmenities)
                    .Include(p =>
                        p.PlaceSpaces)
                    .Include(p =>
                        p.PlacePackages)
                    .Include(p =>
                        p.PlacePolicy)
                    .Include(p =>
                        p.PlaceImages)
                    .FirstOrDefaultAsync(p =>
                        p.PlaceId == id);

            if (place == null)
            {
                return NotFound(
                    "عذرًا، هذه المنشأة غير موجودة.");
            }

            var currentUserId = 0;
            var isPlaceFavorited = false;

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdString =
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                int.TryParse(
                    userIdString,
                    out currentUserId);

                if (currentUserId > 0)
                {
                    isPlaceFavorited =
                        await _context.Favorites
                            .AsNoTracking()
                            .AnyAsync(favorite =>
                                favorite.PlaceId == id &&
                                favorite.UserId == currentUserId);
                }
            }

            var allReviews =
                await _context.Reviews
                    .AsNoTracking()
                    .Include(review =>
                        review.User)
                    .Where(review =>
                        review.PlaceId == id)
                    .ToListAsync();

            var reviewIds =
                allReviews
                    .Select(review =>
                        review.ReviewId)
                    .ToList();

            var allLikes =
                reviewIds.Count == 0
                    ? new List<ReviewLike>()
                    : await _context.ReviewLikes
                        .AsNoTracking()
                        .Where(like =>
                            reviewIds.Contains(
                                like.ReviewId))
                        .ToListAsync();

            var reviewDictionary =
                allReviews
                    .Select(review =>
                        new ReviewDto
                        {
                            ReviewId =
                                review.ReviewId,

                            UserId =
                                review.UserId,

                            UserName =
                                review.User?.Username ??
                                review.User?.FullName ??
                                "مستخدم",

                            ProfileImage =
                                review.User?.ProfileImage,

                            Stars =
                                review.Stars ?? 0,

                            ReviewText =
                                review.ReviewText ??
                                string.Empty,

                            CreatedAt =
                                review.CreatedAt ??
                                DateTime.Now,

                            IsEdited =
                                review.IsEdited,

                            LikesCount =
                                allLikes.Count(like =>
                                    like.ReviewId ==
                                    review.ReviewId),

                            IsLikedByMe =
                                currentUserId > 0 &&
                                allLikes.Any(like =>
                                    like.ReviewId ==
                                        review.ReviewId &&
                                    like.UserId ==
                                        currentUserId),

                            Replies =
                                new List<ReviewDto>()
                        })
                    .ToDictionary(review =>
                        review.ReviewId);

            var rootReviews =
                new List<ReviewDto>();

            foreach (var review in allReviews)
            {
                if (review.ParentReviewId.HasValue &&
                    reviewDictionary.TryGetValue(
                        review.ParentReviewId.Value,
                        out var parentReview))
                {
                    parentReview.Replies.Add(
                        reviewDictionary[
                            review.ReviewId]);
                }
                else if (!review.ParentReviewId.HasValue)
                {
                    rootReviews.Add(
                        reviewDictionary[
                            review.ReviewId]);
                }
            }

            rootReviews =
                rootReviews
                    .OrderByDescending(review =>
                        review.CreatedAt)
                    .ToList();

            foreach (var rootReview in rootReviews)
            {
                rootReview.Replies =
                    rootReview.Replies
                        .OrderBy(review =>
                            review.CreatedAt)
                        .ToList();
            }

            var ratedReviews =
                allReviews
                    .Where(review =>
                        !review.ParentReviewId.HasValue &&
                        review.Stars.HasValue &&
                        review.Stars.Value >= 1 &&
                        review.Stars.Value <= 5)
                    .ToList();

            var totalReviews =
                ratedReviews.Count;

            var averageRating =
                totalReviews > 0
                    ? ratedReviews.Average(review =>
                        review.Stars!.Value)
                    : 0d;

            var starDistribution =
                Enumerable.Range(1, 5)
                    .ToDictionary(
                        star => star,
                        star => ratedReviews.Count(review =>
                            review.Stars == star));

            var categoryName =
                place.Category?.CategoryName ??
                "عام";

            var bookingSystemId =
                place.Category?.BookingSystemId ??
                0;

            var requiresBooking =
                bookingSystemId is 1 or 2 or 3;

            var isPlaceActive =
                IsActiveState(
                    place.Status);

            var isPlaceAvailable =
                place.IsAvailable == true;

            var currentUserRole =
                User.FindFirstValue(
                    ClaimTypes.Role);

            var isAuthenticated =
                User.Identity?.IsAuthenticated == true;

            var isCustomerOrGuest =
                !isAuthenticated ||
                string.IsNullOrWhiteSpace(currentUserRole) ||
                string.Equals(
                    currentUserRole,
                    "Customer",
                    StringComparison.OrdinalIgnoreCase);

            var hasOwner =
                place.OwnerId.HasValue &&
                place.Owner != null;

            var isCurrentUserPlaceOwner =
                currentUserId > 0 &&
                place.OwnerId.HasValue &&
                place.OwnerId.Value == currentUserId;

            var isChatEnabled =
                place.PlacePolicy?.IsChatEnabled != false;

            var isOwnerAccountAvailable =
                hasOwner &&
                !IsSystemAccountUnavailable(
                    place.Owner!.AccountState);

            /*
             * IsAvailable يخص استقبال حجوزات جديدة، ولا يوقف قناة الاستفسارات.
             * لذلك تبقى الدردشة متاحة ما دامت المنشأة وحساب المالك نشطين
             * والدردشة مفعلة من السياسة.
             */
            var canContactPlace =
                hasOwner &&
                isPlaceActive &&
                isChatEnabled &&
                isOwnerAccountAvailable &&
                !isCurrentUserPlaceOwner &&
                isCustomerOrGuest;

            string chatUnavailableMessage;

            if (!hasOwner)
            {
                chatUnavailableMessage =
                    "لا يوجد مالك مرتبط بهذه المنشأة حاليًا.";
            }
            else if (isCurrentUserPlaceOwner)
            {
                chatUnavailableMessage =
                    "هذه منشأتك، ولا يمكنك بدء محادثة عميل معها.";
            }
            else if (!isCustomerOrGuest)
            {
                chatUnavailableMessage =
                    "التواصل مع المنشأة متاح من حساب العميل فقط.";
            }
            else if (!isPlaceActive)
            {
                chatUnavailableMessage =
                    "المنشأة غير مفعلة حاليًا.";
            }
            else if (!isOwnerAccountAvailable)
            {
                chatUnavailableMessage =
                    "حساب مالك المنشأة غير متاح حاليًا.";
            }
            else if (!isChatEnabled)
            {
                chatUnavailableMessage =
                    "الدردشة متوقفة حاليًا من إعدادات المنشأة.";
            }
            else
            {
                chatUnavailableMessage =
                    string.Empty;
            }

            var ownerName =
                !string.IsNullOrWhiteSpace(
                    place.Owner?.FullName)
                    ? place.Owner.FullName.Trim()
                    : !string.IsNullOrWhiteSpace(
                        place.Owner?.Username)
                        ? place.Owner.Username.Trim()
                        : "مالك المنشأة";

            var activeRooms =
                place.Rooms?
                    .Where(room =>
                        room.IsAvailable == true &&
                        room.Quantity > 0)
                    .OrderBy(room =>
                        room.Price)
                    .ToList() ??
                new List<Room>();

            var activeServices =
                place.PlaceAdditionalServices?
                    .Where(service =>
                        service.IsActive == true)
                    .OrderBy(service =>
                        service.ServiceName)
                    .ToList() ??
                new List<PlaceAdditionalService>();

            var activeAmenities =
                place.PlaceAmenities?
                    .Where(amenity =>
                        amenity.IsActive == true)
                    .OrderBy(amenity =>
                        amenity.AmenityName)
                    .ToList() ??
                new List<PlaceAmenity>();

            var activeSpaces =
                place.PlaceSpaces?
                    .Where(space =>
                        space.IsActive == true)
                    .OrderBy(space =>
                        space.SpaceName)
                    .ToList() ??
                new List<PlaceSpace>();

            /*
             * الباقة المفعلة يجب أن تظهر للعميل دائمًا.
             * لا نخفيها هنا بسبب نظام الفترات؛ التحقق من توافق موعدها
             * مع سياسة التشغيل يتم عند اختيار التاريخ/إكمال الحجز.
             */
            var activePackages =
                place.PlacePackages?
                    .Where(package =>
                        package.IsActive == true)
                    .OrderBy(package =>
                        package.Price)
                    .ToList() ??
                new List<PlacePackage>();

            var hasBookingOptions =
                bookingSystemId == 1
                    ? activeRooms.Count > 0
                    : bookingSystemId is 2 or 3 or 4
                        ? activePackages.Count > 0
                        : true;

            var canBook =
            requiresBooking &&
            isPlaceActive &&
            hasBookingOptions;

            string bookingAvailabilityMessage;

            if (!isPlaceActive)
            {
                bookingAvailabilityMessage =
                    "المنشأة غير مفعلة حاليًا.";
            }

            else if (requiresBooking &&
                     !hasBookingOptions)
            {
                bookingAvailabilityMessage =
                    bookingSystemId == 1
                        ? "لا توجد غرف أو أجنحة متاحة حاليًا."
                        : "لا توجد باقات متاحة حاليًا.";
            }
            else if (bookingSystemId == 1)
            {
                bookingAvailabilityMessage =
                    "اختر تاريخ الإقامة لعرض الغرف والأسعار المتاحة.";
            }
            else if (bookingSystemId is 2 or 3 or 4)
            {
                bookingAvailabilityMessage =
                    "اختر الباقة المناسبة ثم حدد يوم الحجز.";
            }
            else
            {
                bookingAvailabilityMessage =
                    "لا يحتاج هذا المكان إلى حجز مسبق.";
            }

            var hasValidCoordinates =
                TryParseCoordinates(
                    place.Coordinates,
                    out var latitude,
                    out var longitude);

            var descriptionData =
                ParsePlaceDescription(
                    place.Description);

            var depositPolicies =
                ReadDepositPolicies(
                    place.PlacePolicy?
                        .DepositPoliciesJson);

            var securityPolicies =
                ReadStringListJson(
                    place.PlacePolicy?
                        .SecurityPoliciesJson);

            var exceptionPolicies =
                ReadStringListJson(
                    place.PlacePolicy?
                        .ExceptionPoliciesJson);

            var termsList =
                SplitTextLines(
                    place.Terms);

            var policySections =
                BuildPolicySections(
                    place,
                    depositPolicies,
                    securityPolicies,
                    exceptionPolicies,
                    termsList);

            var dynamicRules =
                new List<string>();

            if (place.PlacePolicy != null)
            {
                if (place.PlacePolicy.ForFamiliesOnly == true)
                {
                    dynamicRules.Add(
                        "مخصص للعائلات فقط");
                }

                dynamicRules.Add(
                    place.PlacePolicy.AllowSmoking == true
                        ? "التدخين مسموح"
                        : "التدخين غير مسموح");

                dynamicRules.Add(
                    place.PlacePolicy.AllowParties == true
                        ? "الحفلات مسموحة"
                        : "الحفلات غير مسموحة");

                dynamicRules.Add(
                    place.PlacePolicy.AllowQatInside == true
                        ? "القات مسموح داخل المنشأة"
                        : "القات غير مسموح داخل المنشأة");
            }

            var galleryImages =
                place.PlaceImages?
                    .Select(image =>
                        image.ImagePath?.Trim())
                    .Where(image =>
                        !string.IsNullOrWhiteSpace(image))
                    .Select(image =>
                        image!)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList() ??
                new List<string>();

            var mainImage =
                new[]
                {
                    place.CoverImage,
                    place.LogoImage,
                    galleryImages.FirstOrDefault()
                }
                .FirstOrDefault(image =>
                    !string.IsNullOrWhiteSpace(image))?
                .Trim() ??
                "/images/bg1.jpg";

            galleryImages =
                galleryImages
                    .Where(image =>
                        !string.Equals(
                            image,
                            mainImage,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var lowestRoomPrice =
                activeRooms.Count > 0
                    ? activeRooms.Min(room =>
                        (decimal?)room.Price)
                    : null;

            var lowestPackagePrice =
                activePackages.Count > 0
                    ? activePackages.Min(package =>
                        (decimal?)package.Price)
                    : null;

            /*
             * BasePrice موجود مؤقتًا للتوافق مع Details.cshtml القديم.
             * الواجهة الجديدة ستخفي السعر العام وتعتمد على الغرف أو الباقات.
             */
            var compatibleBasePrice =
                bookingSystemId == 1
                    ? lowestRoomPrice ?? 0m
                    : bookingSystemId is 2 or 3 or 4
                        ? lowestPackagePrice ?? 0m
                        : place.Price;

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            string? exchangeRateRegion =
                place.City?
                    .ExchangeRateRegion;

            /*
             * -----------------------------------------------------------------
             * عروض العميل في صفحة التفاصيل
             * -----------------------------------------------------------------
             * نعرض فقط العروض المنشورة والفعالة زمنيًا، ونراعي حد الاستخدام
             * العام وحد المستخدم المسجل. الشروط التي تعتمد على تاريخ الحجز
             * أو عدد الأشخاص يعاد التحقق منها لاحقًا داخل Checkout.
             */
            var promotionToday =
                DateTime.Today;

            var promotionCandidates =
                await _context.Promotions
                    .AsNoTracking()
                    .Where(promotion =>
                        promotion.PlaceId == id &&
                        promotion.IsActive == true &&
                        (
                            promotion.Status == "Active" ||
                            promotion.Status == "Scheduled"
                        ) &&
                        promotion.StartDate <= promotionToday &&
                        promotion.EndDate >= promotionToday)
                    .OrderBy(promotion =>
                        promotion.EndDate)
                    .ThenByDescending(promotion =>
                        promotion.DiscountType == "Percentage"
                            ? promotion.DiscountValue
                            : 0m)
                    .ThenByDescending(promotion =>
                        promotion.UpdatedAt ?? promotion.CreatedAt)
                    .ThenByDescending(promotion =>
                        promotion.PromotionId)
                    .Select(promotion => new
                    {
                        promotion.PromotionId,
                        promotion.PromotionTitle,
                        promotion.ShortDescription,
                        promotion.Description,
                        promotion.ImagePath,
                        promotion.OfferMode,
                        promotion.RewardType,
                        promotion.RequiredQuantity,
                        promotion.RewardQuantity,
                        promotion.IsRewardRepeatable,
                        promotion.RewardServiceId,
                        promotion.SpecialPrice,
                        promotion.DiscountType,
                        promotion.DiscountValue,
                        promotion.MaximumDiscountAmount,
                        promotion.MinimumBookingAmount,
                        promotion.MinimumNights,
                        promotion.MinimumPeople,
                        promotion.IsAutomatic,
                        promotion.PromoCode,
                        promotion.IsStackable,
                        promotion.RoomId,
                        promotion.PackageId,
                        promotion.ServiceId,
                        promotion.StartDate,
                        promotion.EndDate,
                        promotion.StayStartDate,
                        promotion.StayEndDate,
                        promotion.ApplicableDaysCsv,
                        promotion.TotalUsageLimit,
                        promotion.PerCustomerUsageLimit
                    })
                    .ToListAsync();

            var rewardServiceIds = promotionCandidates
                .Where(item => item.RewardServiceId.HasValue && item.RewardServiceId.Value > 0)
                .Select(item => item.RewardServiceId!.Value)
                .Distinct()
                .ToArray();

            var rewardServiceNames = rewardServiceIds.Length == 0
                ? new Dictionary<int, string>()
                : await _context.PlaceAdditionalServices
                    .AsNoTracking()
                    .Where(service =>
                        service.PlaceId == id &&
                        rewardServiceIds.Contains(service.ServiceId))
                    .ToDictionaryAsync(
                        service => service.ServiceId,
                        service => service.ServiceName ?? "خدمة إضافية");

            var candidatePromotionIds =
                promotionCandidates
                    .Select(item =>
                        item.PromotionId)
                    .ToArray();

            var promotionUsageCounts =
                new Dictionary<int, int>();

            var customerPromotionUsageCounts =
                new Dictionary<int, int>();

            if (candidatePromotionIds.Length > 0)
            {
                try
                {
                    promotionUsageCounts =
                        await _context.Bookings
                            .AsNoTracking()
                            .Where(booking =>
                                booking.PromotionId.HasValue &&
                                candidatePromotionIds.Contains(
                                    booking.PromotionId.Value) &&
                                booking.BookingState != "Cancelled" &&
                                booking.BookingState != "Rejected" &&
                                booking.BookingState != "Expired")
                            .GroupBy(booking =>
                                booking.PromotionId!.Value)
                            .ToDictionaryAsync(
                                group =>
                                    group.Key,
                                group =>
                                    group.Count());

                    if (currentUserId > 0)
                    {
                        customerPromotionUsageCounts =
                            await _context.Bookings
                                .AsNoTracking()
                                .Where(booking =>
                                    booking.CustomerId == currentUserId &&
                                    booking.PromotionId.HasValue &&
                                    candidatePromotionIds.Contains(
                                        booking.PromotionId.Value) &&
                                    booking.BookingState != "Cancelled" &&
                                    booking.BookingState != "Rejected" &&
                                    booking.BookingState != "Expired")
                                .GroupBy(booking =>
                                    booking.PromotionId!.Value)
                                .ToDictionaryAsync(
                                    group =>
                                        group.Key,
                                    group =>
                                        group.Count());
                    }
                }
                catch (Exception ex)
                {
                    /*
                     * لا نعطّل صفحة التفاصيل إذا كانت نسخة قاعدة قديمة
                     * لا تحتوي PromotionID بعد. العرض يظل ظاهرًا، بينما
                     * التحقق النهائي عند الحجز يظل هو المرجع.
                     */
                    Console.WriteLine(
                        $"Details promotions usage warning: {ex.Message}");
                }
            }

            var promotionDaysArabic =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Saturday"] = "السبت",
                    ["Sunday"] = "الأحد",
                    ["Monday"] = "الاثنين",
                    ["Tuesday"] = "الثلاثاء",
                    ["Wednesday"] = "الأربعاء",
                    ["Thursday"] = "الخميس",
                    ["Friday"] = "الجمعة"
                };

            var roomTypeSummaries =
                activeRooms
                    .Select(room =>
                        new PlaceRoomTypeSummaryDto
                        {
                            RoomId =
                                room.RoomId,

                            RoomType =
                                !string.IsNullOrWhiteSpace(
                                    room.RoomType)
                                    ? room.RoomType.Trim()
                                    : "غرفة",

                            Description =
                                room.Description?.Trim() ??
                                string.Empty,

                            NightlyPrice =
                                room.Price,

                            NightlyPriceDisplay =
                                currencyContext.Convert(
                                    room.Price,
                                    exchangeRateRegion),

                            NightlyPriceText =
                                currencyContext.Format(
                                    room.Price,
                                    exchangeRateRegion),

                            AreaSquareMeters =
                                room.AreaSquareMeters,

                            MaxAdults =
                                room.MaxAdults,

                            MaxChildren =
                                room.MaxChildren,

                            BedsCount =
                                room.BedsCount,

                            BedType =
                                room.BedType?.Trim() ??
                                string.Empty,

                            ActiveUnitsCount =
                                Math.Max(
                                    0,
                                    room.Quantity),

                            /*
                             * عدد الطوابق غير مطلوب لتسعير العرض، وكي لا
                             * نضيف Include ثقيلًا لوحدات الغرف هنا نتركه صفرًا.
                             */
                            FloorsCount = 0
                        })
                    .ToList();

            var customerPromotions =
                new List<PlacePromotionDto>();

            foreach (var promotion in promotionCandidates)
            {
                int usedCount =
                    promotionUsageCounts.TryGetValue(
                        promotion.PromotionId,
                        out int storedUsage)
                        ? storedUsage
                        : 0;

                if (
                    promotion.TotalUsageLimit.HasValue &&
                    usedCount >=
                        promotion.TotalUsageLimit.Value)
                {
                    continue;
                }

                int customerUsedCount =
                    customerPromotionUsageCounts.TryGetValue(
                        promotion.PromotionId,
                        out int storedCustomerUsage)
                        ? storedCustomerUsage
                        : 0;

                if (
                    currentUserId > 0 &&
                    promotion.PerCustomerUsageLimit.HasValue &&
                    customerUsedCount >=
                        promotion.PerCustomerUsageLimit.Value)
                {
                    continue;
                }

                string targetType;
                string targetLabel;
                string targetAnchor;
                string targetIconClass;
                decimal? originalPrice;

                if (promotion.RoomId.HasValue)
                {
                    targetType = "Room";

                    var targetRoom =
                        activeRooms.FirstOrDefault(room =>
                            room.RoomId ==
                                promotion.RoomId.Value);

                    if (targetRoom == null)
                    {
                        continue;
                    }

                    targetLabel =
                        !string.IsNullOrWhiteSpace(
                            targetRoom.RoomType)
                            ? targetRoom.RoomType.Trim()
                            : "غرفة أو جناح";

                    targetAnchor =
                        $"roomTypeCard-{targetRoom.RoomId}";

                    targetIconClass =
                        "bi-door-open-fill";

                    originalPrice =
                        targetRoom.Price;
                }
                else if (promotion.PackageId.HasValue)
                {
                    targetType = "Package";

                    var targetPackage =
                        activePackages.FirstOrDefault(package =>
                            package.PackageId ==
                                promotion.PackageId.Value);

                    if (targetPackage == null)
                    {
                        continue;
                    }

                    targetLabel =
                        targetPackage.PackageName ??
                        "باقة";

                    targetAnchor =
                        $"packageCard-{targetPackage.PackageId}";

                    targetIconClass =
                        "bi-ticket-perforated-fill";

                    originalPrice =
                        targetPackage.Price;
                }
                else if (promotion.ServiceId.HasValue)
                {
                    targetType = "Service";

                    var targetService =
                        activeServices.FirstOrDefault(service =>
                            service.ServiceId ==
                                promotion.ServiceId.Value);

                    if (targetService == null)
                    {
                        continue;
                    }

                    targetLabel =
                        targetService.ServiceName ??
                        "خدمة إضافية";

                    targetAnchor =
                        $"serviceCard-{targetService.ServiceId}";

                    targetIconClass =
                        "bi-plus-circle-fill";

                    originalPrice =
                        targetService.Price;
                }
                else
                {
                    targetType = "Place";
                    targetLabel = "المنشأة كاملة";

                    targetAnchor =
                        bookingSystemId == 1 &&
                        roomTypeSummaries.Count > 0
                            ? "roomTypesSection"
                            : bookingSystemId is 2 or 3 or 4 &&
                              activePackages.Count > 0
                                ? "packagesSection"
                                : "placeOverviewSection";

                    targetIconClass =
                        "bi-building-fill";

                    originalPrice =
                        compatibleBasePrice > 0
                            ? compatibleBasePrice
                            : null;
                }

                string offerMode = string.IsNullOrWhiteSpace(promotion.OfferMode)
                    ? "Discount"
                    : promotion.OfferMode;
                string rewardType = promotion.RewardType?.Trim() ?? string.Empty;
                string rewardServiceName = promotion.RewardServiceId.HasValue &&
                                           rewardServiceNames.TryGetValue(promotion.RewardServiceId.Value, out var storedRewardServiceName)
                    ? storedRewardServiceName
                    : string.Empty;

                decimal? estimatedDiscount = null;
                decimal? estimatedPrice = null;

                if (originalPrice.HasValue && originalPrice.Value > 0)
                {
                    bool conditional = string.Equals(offerMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase);

                    if (conditional &&
                        (string.Equals(rewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(rewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase)) &&
                        promotion.SpecialPrice.HasValue && promotion.SpecialPrice.Value >= 0m)
                    {
                        estimatedPrice = Math.Round(
                            Math.Min(originalPrice.Value, promotion.SpecialPrice.Value),
                            2);
                        estimatedDiscount = Math.Round(
                            Math.Max(0m, originalPrice.Value - estimatedPrice.Value),
                            2);
                    }
                    else
                    {
                        bool percentage =
                            string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase) ||
                            (conditional && string.Equals(rewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase));
                        bool fixedAmount =
                            string.Equals(promotion.DiscountType, "FixedAmount", StringComparison.OrdinalIgnoreCase) ||
                            (conditional && string.Equals(rewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase));

                        if ((percentage || fixedAmount) && promotion.DiscountValue > 0m)
                        {
                            decimal rawDiscount = percentage
                                ? originalPrice.Value * promotion.DiscountValue / 100m
                                : promotion.DiscountValue;

                            if (percentage && promotion.MaximumDiscountAmount.HasValue &&
                                promotion.MaximumDiscountAmount.Value > 0)
                            {
                                rawDiscount = Math.Min(rawDiscount, promotion.MaximumDiscountAmount.Value);
                            }

                            rawDiscount = Math.Min(originalPrice.Value, Math.Max(0m, rawDiscount));
                            estimatedDiscount = Math.Round(rawDiscount, 2);
                            estimatedPrice = Math.Round(Math.Max(0m, originalPrice.Value - rawDiscount), 2);
                        }
                    }
                }

                string displayDiscountText =
                    string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                        ? $"{promotion.DiscountValue:0.##}%"
                        : promotion.DiscountValue > 0m
                            ? currencyContext.Format(promotion.DiscountValue, exchangeRateRegion)
                            : string.Empty;
                string specialPriceText = promotion.SpecialPrice.HasValue
                    ? currencyContext.Format(promotion.SpecialPrice.Value, exchangeRateRegion)
                    : string.Empty;
                string offerSummary = BuildCustomerPromotionSummary(
                    offerMode, rewardType, promotion.DiscountType, promotion.DiscountValue,
                    displayDiscountText, promotion.RequiredQuantity, promotion.RewardQuantity,
                    rewardServiceName, specialPriceText, promotion.MinimumNights, promotion.IsRewardRepeatable);

                var conditions =
                    new List<string>();

                if (
                    promotion.MinimumBookingAmount.HasValue &&
                    promotion.MinimumBookingAmount.Value > 0)
                {
                    conditions.Add(
                        $"حد أدنى للحجز " +
                        currencyContext.Format(
                            promotion.MinimumBookingAmount.Value,
                            exchangeRateRegion));
                }

                if (
                    promotion.MinimumNights.HasValue &&
                    promotion.MinimumNights.Value > 0)
                {
                    conditions.Add(
                        $"حد أدنى " +
                        $"{promotion.MinimumNights.Value} ليلة");
                }

                if (
                    promotion.MinimumPeople.HasValue &&
                    promotion.MinimumPeople.Value > 0)
                {
                    conditions.Add(
                        $"من " +
                        $"{promotion.MinimumPeople.Value} أشخاص");
                }

                if (string.Equals(rewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
                {
                    int required = promotion.MinimumNights.GetValueOrDefault() > 0
                        ? promotion.MinimumNights!.Value
                        : promotion.RequiredQuantity.GetValueOrDefault();
                    if (required > 0)
                        conditions.Add($"الشرط: {required} {(promotion.MinimumNights.GetValueOrDefault() > 0 ? "ليلة" : "وحدة")}");
                }

                if (
                    promotion.StayStartDate.HasValue ||
                    promotion.StayEndDate.HasValue)
                {
                    string stayRange =
                        promotion.StayStartDate.HasValue &&
                        promotion.StayEndDate.HasValue
                            ? $"{promotion.StayStartDate.Value:dd/MM} - " +
                              $"{promotion.StayEndDate.Value:dd/MM}"
                            : promotion.StayStartDate.HasValue
                                ? $"ابتداءً من " +
                                  $"{promotion.StayStartDate.Value:dd/MM}"
                                : $"حتى " +
                                  $"{promotion.StayEndDate!.Value:dd/MM}";

                    conditions.Add(
                        $"فترة الاستفادة {stayRange}");
                }

                string applicableDaysLabel =
                    string.Empty;

                if (!string.IsNullOrWhiteSpace(
                        promotion.ApplicableDaysCsv))
                {
                    var dayLabels =
                        promotion.ApplicableDaysCsv
                            .Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries)
                            .Select(day =>
                                promotionDaysArabic.TryGetValue(
                                    day,
                                    out string? arabicDay)
                                    ? arabicDay
                                    : day)
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .ToList();

                    applicableDaysLabel =
                        string.Join(
                            "، ",
                            dayLabels);

                    if (dayLabels.Count > 0)
                    {
                        conditions.Add(
                            $"الأيام: {applicableDaysLabel}");
                    }
                }

                if (
                    promotion.MaximumDiscountAmount.HasValue &&
                    promotion.MaximumDiscountAmount.Value > 0 &&
                    string.Equals(
                        promotion.DiscountType,
                        "Percentage",
                        StringComparison.OrdinalIgnoreCase))
                {
                    conditions.Add(
                        $"أقصى خصم " +
                        currencyContext.Format(
                            promotion.MaximumDiscountAmount.Value,
                            exchangeRateRegion));
                }

                bool isSelected =
                    promotionId.HasValue &&
                    promotionId.Value ==
                        promotion.PromotionId;

                customerPromotions.Add(
                    new PlacePromotionDto
                    {
                        PromotionId =
                            promotion.PromotionId,

                        PromotionTitle =
                            promotion.PromotionTitle ??
                            "عرض خاص",

                        ShortDescription =
                            promotion.ShortDescription?.Trim() ??
                            string.Empty,

                        Description =
                            promotion.Description?.Trim() ??
                            string.Empty,

                        ImagePath =
                            promotion.ImagePath?.Trim() ??
                            string.Empty,

                        OfferMode = offerMode,
                        RewardType = rewardType,
                        RequiredQuantity = promotion.RequiredQuantity,
                        RewardQuantity = promotion.RewardQuantity,
                        IsRewardRepeatable = promotion.IsRewardRepeatable,
                        RewardServiceId = promotion.RewardServiceId,
                        RewardServiceName = rewardServiceName,
                        SpecialPrice = promotion.SpecialPrice,
                        OfferSummary = offerSummary,

                        DiscountType =
                            promotion.DiscountType ??
                            string.Empty,

                        DiscountValue =
                            promotion.DiscountValue,

                        MaximumDiscountAmount =
                            promotion.MaximumDiscountAmount,

                        MinimumBookingAmount =
                            promotion.MinimumBookingAmount,

                        MinimumNights =
                            promotion.MinimumNights,

                        MinimumPeople =
                            promotion.MinimumPeople,

                        IsAutomatic =
                            promotion.IsAutomatic != false,

                        PromoCode =
                            promotion.PromoCode?.Trim() ??
                            string.Empty,

                        IsStackable =
                            promotion.IsStackable,

                        RoomId =
                            promotion.RoomId,

                        PackageId =
                            promotion.PackageId,

                        ServiceId =
                            promotion.ServiceId,

                        TargetType =
                            targetType,

                        TargetLabel =
                            targetLabel,

                        TargetAnchor =
                            targetAnchor,

                        TargetIconClass =
                            targetIconClass,

                        StartDate =
                            promotion.StartDate,

                        EndDate =
                            promotion.EndDate,

                        StayStartDate =
                            promotion.StayStartDate,

                        StayEndDate =
                            promotion.StayEndDate,

                        ApplicableDaysCsv =
                            promotion.ApplicableDaysCsv ??
                            string.Empty,

                        ApplicableDaysLabel =
                            applicableDaysLabel,

                        UsedCount =
                            usedCount,

                        TotalUsageLimit =
                            promotion.TotalUsageLimit,

                        PerCustomerUsageLimit =
                            promotion.PerCustomerUsageLimit,

                        IsSelected =
                            isSelected,

                        OriginalPrice =
                            originalPrice,

                        EstimatedDiscountAmount =
                            estimatedDiscount,

                        EstimatedPriceAfterDiscount =
                            estimatedPrice,

                        DisplayDiscountValue =
                            !string.Equals(
                                promotion.DiscountType,
                                "Percentage",
                                StringComparison.OrdinalIgnoreCase) &&
                            promotion.DiscountValue > 0m
                                ? currencyContext.Convert(
                                    promotion.DiscountValue,
                                    exchangeRateRegion)
                                : null,

                        DisplayDiscountValueText =
                            !string.Equals(
                                promotion.DiscountType,
                                "Percentage",
                                StringComparison.OrdinalIgnoreCase) &&
                            promotion.DiscountValue > 0m
                                ? currencyContext.Format(
                                    promotion.DiscountValue,
                                    exchangeRateRegion)
                                : string.Empty,

                        DisplayMaximumDiscountAmount =
                            promotion.MaximumDiscountAmount.HasValue
                                ? currencyContext.Convert(
                                    promotion.MaximumDiscountAmount.Value,
                                    exchangeRateRegion)
                                : null,

                        DisplayMaximumDiscountAmountText =
                            promotion.MaximumDiscountAmount.HasValue
                                ? currencyContext.Format(
                                    promotion.MaximumDiscountAmount.Value,
                                    exchangeRateRegion)
                                : string.Empty,

                        DisplayMinimumBookingAmount =
                            promotion.MinimumBookingAmount.HasValue
                                ? currencyContext.Convert(
                                    promotion.MinimumBookingAmount.Value,
                                    exchangeRateRegion)
                                : null,

                        DisplayMinimumBookingAmountText =
                            promotion.MinimumBookingAmount.HasValue
                                ? currencyContext.Format(
                                    promotion.MinimumBookingAmount.Value,
                                    exchangeRateRegion)
                                : string.Empty,

                        DisplayOriginalPrice =
                            originalPrice.HasValue
                                ? currencyContext.Convert(
                                    originalPrice.Value,
                                    exchangeRateRegion)
                                : null,

                        DisplayOriginalPriceText =
                            originalPrice.HasValue
                                ? currencyContext.Format(
                                    originalPrice.Value,
                                    exchangeRateRegion)
                                : string.Empty,

                        DisplayEstimatedDiscountAmount =
                            estimatedDiscount.HasValue
                                ? currencyContext.Convert(
                                    estimatedDiscount.Value,
                                    exchangeRateRegion)
                                : null,

                        DisplayEstimatedDiscountAmountText =
                            estimatedDiscount.HasValue
                                ? currencyContext.Format(
                                    estimatedDiscount.Value,
                                    exchangeRateRegion)
                                : string.Empty,

                        DisplayEstimatedPriceAfterDiscount =
                            estimatedPrice.HasValue
                                ? currencyContext.Convert(
                                    estimatedPrice.Value,
                                    exchangeRateRegion)
                                : null,

                        DisplayEstimatedPriceAfterDiscountText =
                            estimatedPrice.HasValue
                                ? currencyContext.Format(
                                    estimatedPrice.Value,
                                    exchangeRateRegion)
                                : string.Empty,

                        Conditions =
                            conditions
                    });
            }

            customerPromotions =
                customerPromotions
                    .OrderByDescending(item =>
                        item.IsSelected)
                    .ThenBy(item =>
                        item.EndDate)
                    .ThenByDescending(item =>
                        item.DiscountValue)
                    .ToList();

            if (
                customerPromotions.Count > 0 &&
                customerPromotions.All(item =>
                    !item.IsSelected))
            {
                customerPromotions[0]
                    .IsSelected = true;
            }

            int? effectiveSelectedPromotionId =
                customerPromotions
                    .FirstOrDefault(item =>
                        item.IsSelected)?
                    .PromotionId;

            var checkInTime =
                place.PlacePolicy?.CheckInTime ??
                place.CheckInTime ??
                string.Empty;

            var checkOutTime =
                place.PlacePolicy?.CheckOutTime ??
                place.CheckOutTime ??
                string.Empty;

            var openingHours =
                !string.IsNullOrWhiteSpace(checkInTime) ||
                !string.IsNullOrWhiteSpace(checkOutTime)
                    ? $"{checkInTime} - {checkOutTime}".Trim(' ', '-')
                    : string.Empty;

            var maximumPackageCapacity =
                activePackages.Count > 0
                    ? activePackages.Max(package =>
                        package.Capacity)
                    : 0;

            // =================================================================
            // حفظ مصدر الحجز فقط.
            // تم إلغاء الإعلان المرئي داخل صفحة تفاصيل المنشأة، لكن نبقي
            // Attribution حتى يُنسب الحجز للإعلان الذي دخل منه العميل.
            // =================================================================
            int? attributionAdvertisementId = null;

            try
            {
                attributionAdvertisementId =
                    await _customerAdvertisementService
                        .ResolveAttributionAdvertisementIdAsync(
                            advertisementId,
                            place.PlaceId,
                            HttpContext.RequestAborted);
            }
            catch
            {
                // فشل تتبع الإعلان لا يجب أن يمنع فتح صفحة المنشأة.
                attributionAdvertisementId = null;
            }

            var viewModel =
                new PlaceDetailsViewModel
                {
                    PlaceId =
                        place.PlaceId,

                    Name =
                        place.PlaceNameAndDesc,

                    Description =
                        place.Description,

                    MarketingDescription =
                        descriptionData
                            .MarketingDescription,

                    TechnicalDetails =
                        descriptionData
                            .TechnicalDetails,

                    CityName =
                        place.City?.CityName ??
                        string.Empty,

                    AddressDetails =
                        place.AddressDetail ??
                        string.Empty,

                    MainImage =
                        mainImage,

                    CategoryName =
                        categoryName,

                    CategoryId =
                        place.CategoryId,

                    FloorsData =
                        place.FloorsData,

                    AttributionAdvertisementId =
                        attributionAdvertisementId,

                    OwnerId =
                        place.OwnerId ?? 0,

                    OwnerName =
                        ownerName,

                    IsChatEnabled =
                        isChatEnabled,

                    CanContactPlace =
                        canContactPlace,

                    ChatUnavailableMessage =
                        chatUnavailableMessage,

                    IsFavorited =
                        isPlaceFavorited,

                    IsPlaceActive =
                        isPlaceActive,

                    IsPlaceAvailable =
                        isPlaceAvailable,

                    RequiresBooking =
                        requiresBooking,

                    CanBook =
                        canBook,

                    HasBookingOptions =
                        hasBookingOptions,

                    BookingAvailabilityMessage =
                        bookingAvailabilityMessage,

                    DynamicRules =
                        dynamicRules,

                    BasePrice =
                        compatibleBasePrice,

                    DisplayCurrency =
                        currencyContext.Currency,

                    DisplayCurrencyCode =
                        currencyContext.Code,

                    DisplayCurrencySymbol =
                        currencyContext.Symbol,

                    BasePriceDisplay =
                        currencyContext.Convert(
                            compatibleBasePrice,
                            exchangeRateRegion),

                    BasePriceText =
                        currencyContext.Format(
                            compatibleBasePrice,
                            exchangeRateRegion),

                    LowestRoomPrice =
                        lowestRoomPrice,

                    LowestRoomPriceDisplay =
                        lowestRoomPrice.HasValue
                            ? currencyContext.Convert(
                                lowestRoomPrice.Value,
                                exchangeRateRegion)
                            : null,

                    LowestRoomPriceText =
                        lowestRoomPrice.HasValue
                            ? currencyContext.Format(
                                lowestRoomPrice.Value,
                                exchangeRateRegion)
                            : string.Empty,

                    AvailableRoomTypesCount =
                        activeRooms.Count,

                    AvailableRoomUnitsCount =
                        activeRooms.Sum(room =>
                            room.Quantity),

                    RoomTypes =
                        roomTypeSummaries,

                    Promotions =
                        customerPromotions,

                    SelectedPromotionId =
                        effectiveSelectedPromotionId,

                    ActivePackagesCount =
                        activePackages.Count,

                    PriceUnit =
                        bookingSystemId == 1
                            ? "ليلة"
                            : bookingSystemId is 2 or 3 or 4
                                ? "باقة"
                                : "زيارة",

                    OpeningHours =
                        openingHours,

                    StatusMessage =
                        bookingAvailabilityMessage,

                    PhoneNumber =
                        place.PhoneNumber ??
                        place.Owner?.PhoneNumber ??
                        string.Empty,

                    CheckInTime =
                        checkInTime,

                    CheckOutTime =
                        checkOutTime,

                    MaxGuestCapacity =
                        maximumPackageCapacity,

                    HasValidCoordinates =
                        hasValidCoordinates,

                    Latitude =
                        hasValidCoordinates
                            ? latitude
                            : 0d,

                    Longitude =
                        hasValidCoordinates
                            ? longitude
                            : 0d,

                    GpsCoordinates =
                        hasValidCoordinates
                            ? $"{latitude.ToString(CultureInfo.InvariantCulture)}," +
                              $"{longitude.ToString(CultureInfo.InvariantCulture)}"
                            : null,

                    AverageRating =
                        Math.Round(
                            averageRating,
                            1),

                    TotalReviews =
                        totalReviews,

                    StarDistribution =
                        starDistribution,

                    GalleryImages =
                        galleryImages,

                    Reviews =
                        rootReviews,

                    AvailableTimeSlots =
                        ReadShiftLabels(
                            place.PlacePolicy?
                                .ShiftsJson),

                    EventTypes =
                        descriptionData
                            .EventTypes,

                    SeatingSetups =
                        descriptionData
                            .SeatingSetups,

                    AdditionalServices =
                        activeServices,

                    Amenities =
                        activeAmenities,

                    Spaces =
                        activeSpaces,

                    Packages =
                        activePackages,

                    AdditionalServiceDisplayPrices =
                        activeServices.ToDictionary(
                            service => service.ServiceId,
                            service => currencyContext.Convert(
                                service.Price,
                                exchangeRateRegion)),

                    AdditionalServiceDisplayPriceTexts =
                        activeServices.ToDictionary(
                            service => service.ServiceId,
                            service => currencyContext.Format(
                                service.Price,
                                exchangeRateRegion)),

                    PackageDisplayPrices =
                        activePackages.ToDictionary(
                            package => package.PackageId,
                            package => currencyContext.Convert(
                                package.Price,
                                exchangeRateRegion)),

                    PackageDisplayPriceTexts =
                        activePackages.ToDictionary(
                            package => package.PackageId,
                            package => currencyContext.Format(
                                package.Price,
                                exchangeRateRegion)),

                    DepositPolicy =
                        place.DepositPolicy ??
                        string.Empty,

                    InsurancePolicy =
                        place.InsurancePolicy ??
                        string.Empty,

                    TermsList =
                        termsList,

                    PlacePolicy =
                        place.PlacePolicy,

                    ParsedDepositPolicies =
                        depositPolicies,

                    ParsedSecurityPolicies =
                        securityPolicies,

                    ParsedExceptionPolicies =
                        exceptionPolicies,

                    PolicySections =
                        policySections,

                    BookingSystemId =
                        bookingSystemId
                };

            return View(
                viewModel);
        }

        // =========================================================================
        // 🌟 دوال التقييمات والتفاعلات (النسخة المحدثة) 🌟
        // =========================================================================

        [HttpPost("/Places/AddReview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(
            [FromBody] ReviewViewModel model)
        {
            if (model == null ||
                model.PlaceId <= 0 ||
                model.Rating < 1 ||
                model.Rating > 5 ||
                string.IsNullOrWhiteSpace(model.Comment))
            {
                return Json(new
                {
                    success = false,
                    message = "أدخل تقييمًا من 1 إلى 5 نجوم واكتب تجربتك قبل النشر."
                });
            }

            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new
                {
                    success = false,
                    message = "يرجى تسجيل الدخول أولاً لتتمكن من إضافة تقييمك."
                });
            }

            try
            {
                var userIdString =
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                if (!int.TryParse(
                        userIdString,
                        out int userId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "حدث خطأ في التعرف على حسابك."
                    });
                }

                var placeInfo =
                    await _context.Places
                        .AsNoTracking()
                        .Where(place =>
                            place.PlaceId == model.PlaceId)
                        .Select(place => new
                        {
                            place.PlaceId,
                            place.OwnerId,
                            place.PlaceNameAndDesc
                        })
                        .FirstOrDefaultAsync();

                if (placeInfo == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "المنشأة غير موجودة أو لم تعد متاحة."
                    });
                }

                var newReview =
                    new Review
                    {
                        PlaceId = placeInfo.PlaceId,
                        UserId = userId,
                        Stars = model.Rating,
                        ReviewText = model.Comment.Trim(),
                        CreatedAt = DateTime.Now,
                        ParentReviewId = null,
                        IsEdited = false
                    };

                await _context.Reviews
                    .AddAsync(newReview);

                await _context
                    .SaveChangesAsync();

                if (placeInfo.OwnerId is int ownerId &&
                    ownerId > 0 &&
                    ownerId != userId)
                {
                    try
                    {
                        await _notificationService.CreateAsync(
                            new CreateNotificationDto
                            {
                                UserId = ownerId,
                                NotificationTitle =
                                    "تقييم جديد لمنشأتك",
                                NotificationText =
                                    $"وصل تقييم جديد على {placeInfo.PlaceNameAndDesc ?? "منشأتك"}.",
                                NotificationType =
                                    NotificationTypes.ReviewCreated,
                                TargetUrl =
                                    "/Owner/Chat/ChatInterface",
                                ReferenceType =
                                    NotificationReferenceTypes.Review,
                                ReferenceId =
                                    newReview.ReviewId
                            },
                            HttpContext.RequestAborted);
                    }
                    catch (Exception notificationException)
                    {
                        Console.WriteLine(
                            $"Review notification error: {notificationException.Message}");
                    }
                }

                return Json(new
                {
                    success = true,
                    reviewId = newReview.ReviewId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error saving review: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = "حدث خطأ في السيرفر أثناء حفظ التقييم."
                });
            }
        }

        [HttpPost("/Places/AddReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReply(
            [FromForm] int parentReviewId,
            [FromForm] string replyText,
            [FromForm] int placeId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new
                {
                    success = false,
                    message = "يرجى تسجيل الدخول أولاً للرد."
                });
            }

            if (parentReviewId <= 0 ||
                string.IsNullOrWhiteSpace(replyText))
            {
                return Json(new
                {
                    success = false,
                    message = "لا يمكن إرسال رد فارغ أو بدون تعليق أصلي."
                });
            }

            var userIdString =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!int.TryParse(
                    userIdString,
                    out int userId))
            {
                return Json(new
                {
                    success = false,
                    message = "خطأ في بيانات المستخدم."
                });
            }

            /*
             * لا نثق بـ placeId القادم من المتصفح لتحديد منشأة الرد.
             * المنشأة الصحيحة تُؤخذ دائمًا من التعليق الأب حتى لا يمكن
             * إنشاء رد مرتبط بتعليق من منشأة ومنشأة مختلفة في نفس الوقت.
             */
            var parentReview =
                await _context.Reviews
                    .Include(review =>
                        review.Place)
                    .FirstOrDefaultAsync(review =>
                        review.ReviewId ==
                        parentReviewId);

            if (parentReview == null)
            {
                return Json(new
                {
                    success = false,
                    message = "التعليق الأصلي غير موجود."
                });
            }

            try
            {
                var resolvedPlaceId =
                    parentReview.PlaceId;

                var newReply =
                    new Review
                    {
                        UserId = userId,
                        PlaceId = resolvedPlaceId,
                        Stars = null,
                        ReviewText = replyText.Trim(),
                        CreatedAt = DateTime.Now,
                        ParentReviewId = parentReviewId,
                        IsEdited = false
                    };

                _context.Reviews
                    .Add(newReply);

                await _context
                    .SaveChangesAsync();

                if (parentReview.UserId != userId)
                {
                    try
                    {
                        var recipientRole =
                            await _context.Users
                                .AsNoTracking()
                                .Where(user =>
                                    user.UserId ==
                                    parentReview.UserId)
                                .Select(user =>
                                    user.Role)
                                .FirstOrDefaultAsync();

                        var isOwnerRecipient =
                            string.Equals(
                                recipientRole,
                                "Owner",
                                StringComparison.OrdinalIgnoreCase);

                        /*
                         * للعميل نترك TargetUrl فارغًا عمدًا:
                         * NotificationService سيبني الرابط من ReferenceId
                         * ويصعد تلقائيًا حتى جذر شجرة الردود، لذلك يعمل
                         * الانتقال الصحيح حتى مع Reply داخل Reply داخل Reply.
                         *
                         * رابط المالك يبقى كما هو الآن حتى مرحلة إشعارات المالك.
                         */
                        var targetUrl =
                            isOwnerRecipient
                                ? "/Owner/Chat/ChatInterface"
                                : null;

                        await _notificationService.CreateAsync(
                            new CreateNotificationDto
                            {
                                UserId =
                                    parentReview.UserId,
                                NotificationTitle =
                                    isOwnerRecipient
                                        ? "رد جديد على تعليقك"
                                        : "رد جديد على تقييمك",
                                NotificationText =
                                    $"تمت إضافة رد جديد على تعليقك في {parentReview.Place?.PlaceNameAndDesc ?? "المنشأة"}.",
                                NotificationType =
                                    NotificationTypes.ReviewReply,
                                TargetUrl =
                                    targetUrl,
                                ReferenceType =
                                    NotificationReferenceTypes.Review,
                                ReferenceId =
                                    newReply.ReviewId
                            },
                            HttpContext.RequestAborted);
                    }
                    catch (Exception notificationException)
                    {
                        Console.WriteLine(
                            $"Review reply notification error: {notificationException.Message}");
                    }
                }

                return Json(new
                {
                    success = true,
                    message = "تم نشر الرد بنجاح.",
                    replyId = newReply.ReviewId,
                    placeId = resolvedPlaceId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error saving reply: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = "حدث خطأ في السيرفر أثناء حفظ الرد."
                });
            }
        }

        [HttpPost("/Places/EditReview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReview([FromForm] int reviewId, [FromForm] string newText)
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً." });

            if (string.IsNullOrWhiteSpace(newText))
                return Json(new { success = false, message = "لا يمكن أن يكون التقييم فارغاً." });

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Json(new { success = false, message = "خطأ في بيانات المستخدم." });

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId);
            if (review == null)
                return Json(new { success = false, message = "التعليق غير موجود." });

            if (review.UserId != userId)
                return Json(new { success = false, message = "غير مصرح لك بتعديل هذا التعليق." });

            var timePassed = DateTime.Now - (review.CreatedAt ?? DateTime.Now);
            if (timePassed.TotalMinutes > 10)
                return Json(new { success = false, message = "انتهى الوقت المسموح لتعديل التعليق (10 دقائق)." });

            review.ReviewText = newText.Trim();
            review.IsEdited = true;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم تعديل التعليق بنجاح." });
        }

        [HttpPost("/Places/DeleteReview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview([FromForm] int reviewId)
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً." });

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Json(new { success = false, message = "خطأ في بيانات المستخدم." });

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId);
            if (review == null)
                return Json(new { success = false, message = "التعليق غير موجود." });

            if (review.UserId != userId)
                return Json(new { success = false, message = "غير مصرح لك بحذف هذا التعليق." });

            // دالة محلية للبحث عن جميع الردود التابعة (الشجرة كاملة)
            var allReviews = await _context.Reviews.ToListAsync();
            var reviewsToDelete = new List<Review>();

            void FindChildrenToDelete(int parentId)
            {
                var children = allReviews.Where(r => r.ParentReviewId == parentId).ToList();
                foreach (var child in children)
                {
                    reviewsToDelete.Add(child);
                    FindChildrenToDelete(child.ReviewId);
                }
            }

            reviewsToDelete.Add(review);
            FindChildrenToDelete(reviewId);

            var idsToDelete = reviewsToDelete.Select(r => r.ReviewId).ToList();
            var likesToDelete = await _context.ReviewLikes.Where(l => idsToDelete.Contains(l.ReviewId)).ToListAsync();

            if (likesToDelete.Any()) _context.ReviewLikes.RemoveRange(likesToDelete);
            if (reviewsToDelete.Any()) _context.Reviews.RemoveRange(reviewsToDelete);

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم حذف التعليق بنجاح." });
        }

        [HttpPost("/Places/ToggleReviewLike")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReviewLike([FromForm] int reviewId)
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً للإعجاب بالتعليق." });

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Json(new { success = false, message = "خطأ في بيانات المستخدم." });

            var reviewExists = await _context.Reviews.AnyAsync(r => r.ReviewId == reviewId);
            if (!reviewExists)
                return Json(new { success = false, message = "التقييم غير موجود." });

            var existingLike = await _context.ReviewLikes
                .FirstOrDefaultAsync(l => l.ReviewId == reviewId && l.UserId == userId);

            bool isLiked;
            if (existingLike != null)
            {
                _context.ReviewLikes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                _context.ReviewLikes.Add(new ReviewLike
                {
                    UserId = userId,
                    ReviewId = reviewId,
                    CreatedAt = DateTime.Now
                });
                isLiked = true;
            }
            await _context.SaveChangesAsync();

            int newLikeCount = await _context.ReviewLikes.CountAsync(l => l.ReviewId == reviewId);
            return Json(new { success = true, isLiked = isLiked, likesCount = newLikeCount });
        }

        [HttpPost("/Places/ToggleFavorite")]
        public async Task<IActionResult> ToggleFavorite([FromBody] FavoriteRequestDto request)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً لإضافة المكان للمفضلة." });
            }

            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdString, out int userId))
                    return Json(new { success = false, message = "خطأ في بيانات المستخدم." });

                var existingFavorite = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.PlaceId == request.PlaceId);

                if (existingFavorite != null)
                {
                    _context.Favorites.Remove(existingFavorite);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, isFavorited = false, message = "تم إزالة المكان من المفضلة." });
                }
                else
                {
                    var newFav = new Favorite
                    {
                        UserId = userId,
                        PlaceId = request.PlaceId,
                        CreatedAt = DateTime.Now
                    };
                    await _context.Favorites.AddAsync(newFav);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, isFavorited = true, message = "تم إضافة المكان إلى مفضلتك بنجاح! ❤️" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(new { success = false, message = "حدث خطأ في السيرفر أثناء تعديل المفضلة." });
            }
        }

        [HttpGet("/Places/Search")]
        public async Task<IActionResult> Search(
            string? q,
            int? categoryId,
            int? cityId,
            List<string>? sortOptions,
            bool onlyWithOffers = false,
            bool onlyBookable = false,
            int page = 1)
        {
            /* ================================================================
               محرك البحث الحقيقي لصفحة الاستكشاف
               ----------------------------------------------------------------
               - البحث النصي يعمل بالكلمات على: الاسم، الوصف، الفئة، المدينة، العنوان.
               - الفلاتر تتراكم معاً ولا يلغي أحدها الآخر.
               - الأقل/الأعلى سعراً متعاكسان حتى لو تم العبث بالرابط يدوياً.
               - السعر المستخدم في الفرز هو سعر حجز حقيقي قدر الإمكان.
               - "الموصى بها" يعتمد نفس معيار الأماكن المميزة الموثق بالحجوزات.
               - كل الفرز يتم قبل Pagination.
               ================================================================ */

            static string NormalizeArabicSearch(string? value)
            {
                return (value ?? string.Empty)
                    .Trim()
                    .Replace("أ", "ا")
                    .Replace("إ", "ا")
                    .Replace("آ", "ا")
                    .Replace("ة", "ه")
                    .Replace("ى", "ي")
                    .Replace("ؤ", "و")
                    .Replace("ئ", "ي");
            }

            const int recommendedMinimumVerifiedCustomers = 10;
            const double recommendedMinimumAverageRating = 4.3d;
            const double recommendedBayesianPriorMean = 4.0d;
            const int recommendedBayesianPriorWeight = 20;

            int currentUserId = 0;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdString =
                    User.FindFirstValue(ClaimTypes.NameIdentifier);

                int.TryParse(userIdString, out currentUserId);
            }

            page = Math.Max(1, page);

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            var allowedSortOptions = new HashSet<string>(
                new[]
                {
                    "recommended",
                    "price_asc",
                    "price_desc",
                    "top_rated",
                    "newest"
                },
                StringComparer.OrdinalIgnoreCase);

            var sortList = (sortOptions ?? new List<string>())
                .Where(option =>
                    !string.IsNullOrWhiteSpace(option) &&
                    allowedSortOptions.Contains(option))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // حماية Server-side: لا نسمح للأقل والأعلى سعراً أن يجتمعا حتى لو
            // أُرسل URL يدوياً. الواجهة تمنعهما أيضاً في JavaScript.
            if (sortList.Contains("price_asc", StringComparer.OrdinalIgnoreCase) &&
                sortList.Contains("price_desc", StringComparer.OrdinalIgnoreCase))
            {
                sortList.RemoveAll(option =>
                    option.Equals("price_desc", StringComparison.OrdinalIgnoreCase));
            }

            /* ================================================================
               1. العروض الفعالة العامة
               ================================================================ */
            var today = DateTime.Today;

            var promotionCandidates =
                await _context.Promotions
                    .AsNoTracking()
                    .Where(promotion =>
                        promotion.IsActive == true &&
                        (promotion.Status == "Active" ||
                         promotion.Status == "Scheduled") &&
                        promotion.StartDate.Date <= today &&
                        promotion.EndDate.Date >= today &&
                        promotion.Place.Status == "Active")
                    .OrderBy(promotion => promotion.EndDate)
                    .ThenByDescending(promotion =>
                        promotion.DiscountType == "Percentage"
                            ? promotion.DiscountValue
                            : 0m)
                    .ThenByDescending(promotion =>
                        promotion.UpdatedAt ?? promotion.CreatedAt)
                    .ThenByDescending(promotion => promotion.PromotionId)
                    .Select(promotion => new
                    {
                        promotion.PromotionId,
                        promotion.PlaceId,
                        promotion.PromotionTitle,
                        promotion.OfferMode,
                        promotion.RewardType,
                        promotion.RequiredQuantity,
                        promotion.RewardQuantity,
                        promotion.IsRewardRepeatable,
                        promotion.RewardServiceId,
                        promotion.SpecialPrice,
                        promotion.MinimumNights,
                        promotion.DiscountType,
                        promotion.DiscountValue,
                        promotion.EndDate,
                        promotion.TotalUsageLimit,
                        promotion.RoomId,
                        promotion.PackageId,
                        promotion.ServiceId
                    })
                    .ToListAsync();

            var promotionIds = promotionCandidates
                .Select(promotion => promotion.PromotionId)
                .ToArray();

            var promotionUsageCounts = new Dictionary<int, int>();

            if (promotionIds.Length > 0)
            {
                try
                {
                    promotionUsageCounts =
                        await _context.Bookings
                            .AsNoTracking()
                            .Where(booking =>
                                booking.PromotionId.HasValue &&
                                promotionIds.Contains(booking.PromotionId.Value) &&
                                booking.BookingState != "Cancelled" &&
                                booking.BookingState != "Rejected" &&
                                booking.BookingState != "Expired")
                            .GroupBy(booking => booking.PromotionId!.Value)
                            .ToDictionaryAsync(
                                group => group.Key,
                                group => group.Count());
                }
                catch (Exception ex)
                {
                    // لا نعطّل البحث لو كانت إحصاءات الاستخدام غير متاحة مؤقتاً.
                    Console.WriteLine(
                        $"Search promotions usage warning: {ex.Message}");
                }
            }

            var availablePromotionCandidates =
                promotionCandidates
                    .Where(promotion =>
                    {
                        int usedCount = promotionUsageCounts.TryGetValue(
                            promotion.PromotionId,
                            out int storedUsedCount)
                                ? storedUsedCount
                                : 0;

                        return !promotion.TotalUsageLimit.HasValue ||
                               usedCount < promotion.TotalUsageLimit.Value;
                    })
                    .ToList();

            var placesWithOffers = availablePromotionCandidates
                .Select(promotion => promotion.PlaceId)
                .Distinct()
                .ToArray();

            /* ================================================================
               2. الاستعلام الأساسي + البحث النصي الذكي
               ================================================================ */
            var query =
                _context.Places
                    .AsNoTracking()
                    .Where(place => place.Status == "Active")
                    .AsQueryable();

            var normalizedSearch = NormalizeArabicSearch(q);

            var searchTokens = normalizedSearch
                .Split(
                    new[]
                    {
                        ' ', '\t', '\r', '\n', '،', ',', '-', '_', '/', '\\'
                    },
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // كل كلمة يجب أن توجد في أحد الحقول، وليس بالضرورة في نفس الحقل.
            // مثال: "فندق صنعاء" => "فندق" قد يطابق الفئة و"صنعاء" المدينة.
            foreach (var token in searchTokens)
            {
                string currentToken = token;

                query = query.Where(place =>
                    place.PlaceNameAndDesc
                        .Replace("أ", "ا")
                        .Replace("إ", "ا")
                        .Replace("آ", "ا")
                        .Replace("ة", "ه")
                        .Replace("ى", "ي")
                        .Replace("ؤ", "و")
                        .Replace("ئ", "ي")
                        .Contains(currentToken) ||

                    (place.Description != null &&
                     place.Description
                        .Replace("أ", "ا")
                        .Replace("إ", "ا")
                        .Replace("آ", "ا")
                        .Replace("ة", "ه")
                        .Replace("ى", "ي")
                        .Replace("ؤ", "و")
                        .Replace("ئ", "ي")
                        .Contains(currentToken)) ||

                    (place.AddressDetail != null &&
                     place.AddressDetail
                        .Replace("أ", "ا")
                        .Replace("إ", "ا")
                        .Replace("آ", "ا")
                        .Replace("ة", "ه")
                        .Replace("ى", "ي")
                        .Replace("ؤ", "و")
                        .Replace("ئ", "ي")
                        .Contains(currentToken)) ||

                    (place.Category != null &&
                     place.Category.CategoryName
                        .Replace("أ", "ا")
                        .Replace("إ", "ا")
                        .Replace("آ", "ا")
                        .Replace("ة", "ه")
                        .Replace("ى", "ي")
                        .Replace("ؤ", "و")
                        .Replace("ئ", "ي")
                        .Contains(currentToken)) ||

                    (place.City != null &&
                     place.City.CityName
                        .Replace("أ", "ا")
                        .Replace("إ", "ا")
                        .Replace("آ", "ا")
                        .Replace("ة", "ه")
                        .Replace("ى", "ي")
                        .Replace("ؤ", "و")
                        .Replace("ئ", "ي")
                        .Contains(currentToken)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(place =>
                    place.CategoryId == categoryId.Value);
            }

            if (cityId.HasValue)
            {
                query = query.Where(place =>
                    place.CityId == cityId.Value);
            }

            if (onlyWithOffers)
            {
                query = placesWithOffers.Length == 0
                    ? query.Where(place => false)
                    : query.Where(place =>
                        placesWithOffers.Contains(place.PlaceId));
            }

            // "قابلة للحجز" هنا حقيقي: ليس مجرد أن الفئة تدعم الحجز، بل يوجد
            // خيار حجز فعّال حالياً (غرفة متاحة أو باقة فعالة).
            if (onlyBookable)
            {
                query = query.Where(place =>
                    place.Category != null &&
                    (
                        (place.Category.BookingSystemId == 1 &&
                         _context.Rooms.Any(room =>
                             room.PlaceId == place.PlaceId &&
                             room.IsAvailable == true &&
                             room.Quantity > 0)) ||

                        (place.Category.BookingSystemId >= 2 &&
                         place.Category.BookingSystemId <= 4 &&
                         _context.PlacePackages.Any(package =>
                             package.PlaceId == place.PlaceId &&
                             package.IsActive == true))
                    ));
            }

            /* ================================================================
               3. تحميل المرشحين الأساسيين فقط
               ================================================================ */
            var basePlaces =
                await query
                    .Select(place => new
                    {
                        place.PlaceId,
                        Name = place.PlaceNameAndDesc,
                        CategoryName =
                            place.Category != null
                                ? place.Category.CategoryName
                                : "عام",
                        CityName =
                            place.City != null
                                ? place.City.CityName
                                : "غير محدد",
                        ExchangeRateRegion =
                            place.City != null
                                ? place.City.ExchangeRateRegion
                                : null,
                        Address = place.AddressDetail,
                        PlacePrice = place.Price,
                        BookingSystemId =
                            place.Category != null
                                ? place.Category.BookingSystemId
                                : 0,
                        MainImage =
                            place.CoverImage ??
                            "/images/places/default-main.jpg"
                    })
                    .ToListAsync();

            var basePlaceIds = basePlaces
                .Select(place => place.PlaceId)
                .ToArray();

            /* ================================================================
               4. التقييم الحقيقي قبل Pagination
               ================================================================ */
            var reviewStatsByPlace = new Dictionary<int, (double Rating, int Count)>();

            if (basePlaceIds.Length > 0)
            {
                var reviewStatsRows =
                    await _context.Reviews
                        .AsNoTracking()
                        .Where(review =>
                            basePlaceIds.Contains(review.PlaceId) &&
                            review.ParentReviewId == null &&
                            review.Stars.HasValue)
                        .GroupBy(review => review.PlaceId)
                        .Select(group => new
                        {
                            PlaceId = group.Key,
                            Rating = group.Average(review =>
                                (double)review.Stars!.Value),
                            Count = group.Count()
                        })
                        .ToListAsync();

                reviewStatsByPlace = reviewStatsRows
                    .ToDictionary(
                        row => row.PlaceId,
                        row =>
                            (Math.Round(row.Rating, 1), row.Count));
            }

            /* ================================================================
               5. السعر الحقيقي للفرز
               ================================================================ */
            var roomMinPriceByPlace = new Dictionary<int, decimal>();
            var packageMinPriceByPlace = new Dictionary<int, decimal>();

            if (basePlaceIds.Length > 0)
            {
                roomMinPriceByPlace =
                    (await _context.Rooms
                        .AsNoTracking()
                        .Where(room =>
                            basePlaceIds.Contains(room.PlaceId) &&
                            room.IsAvailable == true &&
                            room.Quantity > 0)
                        .GroupBy(room => room.PlaceId)
                        .Select(group => new
                        {
                            PlaceId = group.Key,
                            Price = group.Min(room => room.Price)
                        })
                        .ToListAsync())
                    .ToDictionary(
                        row => row.PlaceId,
                        row => row.Price);

                packageMinPriceByPlace =
                    (await _context.PlacePackages
                        .AsNoTracking()
                        .Where(package =>
                            basePlaceIds.Contains(package.PlaceId) &&
                            package.IsActive == true)
                        .GroupBy(package => package.PlaceId)
                        .Select(group => new
                        {
                            PlaceId = group.Key,
                            Price = group.Min(package => package.Price)
                        })
                        .ToListAsync())
                    .ToDictionary(
                        row => row.PlaceId,
                        row => row.Price);
            }

            /* ================================================================
               6. "الموصى بها" = نفس معيار الأماكن المميزة الموثق
               ================================================================ */
            var recommendedScoreByPlace = new Dictionary<int, double>();

            if (sortList.Contains("recommended", StringComparer.OrdinalIgnoreCase) &&
                basePlaceIds.Length > 0)
            {
                var completedBookingCustomers =
                    (await _context.Bookings
                        .AsNoTracking()
                        .Where(booking =>
                            basePlaceIds.Contains(booking.PlaceId) &&
                            (booking.BookingState == "Completed" ||
                             booking.CompletedAt.HasValue))
                        .Select(booking => new
                        {
                            booking.PlaceId,
                            booking.CustomerId
                        })
                        .Distinct()
                        .ToListAsync())
                    .Select(item =>
                        (item.PlaceId, item.CustomerId))
                    .ToHashSet();

                var verifiedReviewRows =
                    (await _context.Reviews
                        .AsNoTracking()
                        .Where(review =>
                            basePlaceIds.Contains(review.PlaceId) &&
                            review.ParentReviewId == null &&
                            review.Stars.HasValue)
                        .Select(review => new
                        {
                            review.PlaceId,
                            review.UserId,
                            Stars = review.Stars!.Value
                        })
                        .ToListAsync())
                    .Where(review =>
                        completedBookingCustomers.Contains(
                            (review.PlaceId, review.UserId)))
                    .GroupBy(review => new
                    {
                        review.PlaceId,
                        review.UserId
                    })
                    .Select(group => new
                    {
                        group.Key.PlaceId,
                        Rating = group.Average(item =>
                            (double)item.Stars)
                    })
                    .ToList();

                recommendedScoreByPlace = verifiedReviewRows
                    .GroupBy(item => item.PlaceId)
                    .Select(group =>
                    {
                        int verifiedCustomerCount = group.Count();
                        double averageRating = group.Average(item => item.Rating);
                        double weightedScore =
                            ((averageRating * verifiedCustomerCount) +
                             (recommendedBayesianPriorMean *
                              recommendedBayesianPriorWeight)) /
                            (verifiedCustomerCount +
                             recommendedBayesianPriorWeight);

                        return new
                        {
                            PlaceId = group.Key,
                            VerifiedCustomerCount = verifiedCustomerCount,
                            AverageRating = averageRating,
                            WeightedScore = weightedScore
                        };
                    })
                    .Where(item =>
                        item.VerifiedCustomerCount >=
                            recommendedMinimumVerifiedCustomers &&
                        item.AverageRating >=
                            recommendedMinimumAverageRating)
                    .ToDictionary(
                        item => item.PlaceId,
                        item => item.WeightedScore);
            }

            /* ================================================================
               7. بناء النتائج قبل الفرز
               ================================================================ */
            var candidates = new List<PlaceSearchResultDto>();

            foreach (var place in basePlaces)
            {
                // عند اختيار "الموصى بها" تصبح فلترة حقيقية، وليس مجرد IsFeatured.
                if (sortList.Contains("recommended", StringComparer.OrdinalIgnoreCase) &&
                    !recommendedScoreByPlace.ContainsKey(place.PlaceId))
                {
                    continue;
                }

                decimal? realSearchPrice = null;
                bool hasBooking = false;

                if (place.BookingSystemId == 1)
                {
                    if (roomMinPriceByPlace.TryGetValue(
                            place.PlaceId,
                            out decimal roomPrice))
                    {
                        realSearchPrice = roomPrice;
                        hasBooking = true;
                    }
                }
                else if (place.BookingSystemId >= 2 &&
                         place.BookingSystemId <= 4)
                {
                    if (packageMinPriceByPlace.TryGetValue(
                            place.PlaceId,
                            out decimal packagePrice))
                    {
                        realSearchPrice = packagePrice;
                        hasBooking = true;
                    }
                }
                else if (place.PlacePrice > 0)
                {
                    realSearchPrice = place.PlacePrice;
                }

                var reviewStats = reviewStatsByPlace.TryGetValue(
                    place.PlaceId,
                    out var storedReviewStats)
                        ? storedReviewStats
                        : (0d, 0);

                candidates.Add(new PlaceSearchResultDto
                {
                    PlaceId = place.PlaceId,
                    Name = place.Name,
                    CategoryName = place.CategoryName,
                    CityName = place.CityName,
                    Address = place.Address,
                    Price = realSearchPrice ?? 0m,
                    DisplayPrice = realSearchPrice.HasValue
                        ? currencyContext.Convert(
                            realSearchPrice.Value,
                            place.ExchangeRateRegion)
                        : 0m,
                    DisplayPriceText = realSearchPrice.HasValue
                        ? currencyContext.Format(
                            realSearchPrice.Value,
                            place.ExchangeRateRegion)
                        : string.Empty,
                    DisplayCurrency = currencyContext.Currency,
                    DisplayCurrencyCode = currencyContext.Code,
                    DisplayCurrencySymbol = currencyContext.Symbol,
                    ExchangeRateRegion = place.ExchangeRateRegion ?? string.Empty,
                    HasSearchPrice = realSearchPrice.HasValue,
                    HasBooking = hasBooking,
                    MainImage = place.MainImage,
                    Rating = reviewStats.Item1,
                    ReviewsCount = reviewStats.Item2
                });
            }

            /* ================================================================
               8. ترتيب تراكمي صحيح قبل Pagination
               ================================================================
               يمكن اختيار أكثر من معيار:
               - السعر إن اختير يكون المعيار الأول (صعود/نزول فقط، لا يجتمعان).
               - الأعلى تقييماً يكون المعيار التالي.
               - الأحدث يكون معياراً تالياً.
               - الموصى بها فلتر موثق؛ وإذا اختير وحده نرتب بدرجة التوصية.
               ================================================================ */
            IOrderedEnumerable<PlaceSearchResultDto>? orderedResults = null;

            bool priceAscending = sortList.Contains(
                "price_asc",
                StringComparer.OrdinalIgnoreCase);

            bool priceDescending = sortList.Contains(
                "price_desc",
                StringComparer.OrdinalIgnoreCase);

            bool topRated = sortList.Contains(
                "top_rated",
                StringComparer.OrdinalIgnoreCase);

            bool newest = sortList.Contains(
                "newest",
                StringComparer.OrdinalIgnoreCase);

            bool recommended = sortList.Contains(
                "recommended",
                StringComparer.OrdinalIgnoreCase);

            if (priceAscending)
            {
                orderedResults = candidates
                    .OrderBy(result => result.HasSearchPrice ? 0 : 1)
                    .ThenBy(result =>
                        result.HasSearchPrice
                            ? result.DisplayPrice
                            : decimal.MaxValue);
            }
            else if (priceDescending)
            {
                orderedResults = candidates
                    .OrderBy(result => result.HasSearchPrice ? 0 : 1)
                    .ThenByDescending(result =>
                        result.HasSearchPrice
                            ? result.DisplayPrice
                            : decimal.MinValue);
            }

            if (topRated)
            {
                orderedResults = orderedResults == null
                    ? candidates
                        .OrderByDescending(result => result.Rating)
                        .ThenByDescending(result => result.ReviewsCount)
                    : orderedResults
                        .ThenByDescending(result => result.Rating)
                        .ThenByDescending(result => result.ReviewsCount);
            }

            if (newest)
            {
                orderedResults = orderedResults == null
                    ? candidates.OrderByDescending(result => result.PlaceId)
                    : orderedResults.ThenByDescending(result => result.PlaceId);
            }

            // إذا اختيرت "الموصى بها" وحدها، نرتبها بدرجة Bayesian الموثقة.
            if (orderedResults == null && recommended)
            {
                orderedResults = candidates
                    .OrderByDescending(result =>
                        recommendedScoreByPlace.TryGetValue(
                            result.PlaceId,
                            out double score)
                                ? score
                                : 0d)
                    .ThenByDescending(result => result.Rating)
                    .ThenByDescending(result => result.ReviewsCount);
            }

            orderedResults ??=
                candidates.OrderByDescending(result => result.PlaceId);

            int totalItems = candidates.Count;
            const int pageSize = 12;

            int totalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var results = orderedResults
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            /* ================================================================
               9. الصور والمفضلة للصفحة الحالية فقط
               ================================================================ */
            var pagePlaceIds = results
                .Select(result => result.PlaceId)
                .ToArray();

            if (pagePlaceIds.Length > 0)
            {
                var galleryRows =
                    await _context.PlaceImages
                        .AsNoTracking()
                        .Where(image =>
                            pagePlaceIds.Contains(image.PlaceId) &&
                            !string.IsNullOrWhiteSpace(image.ImagePath))
                        .Select(image => new
                        {
                            image.PlaceId,
                            image.ImagePath
                        })
                        .ToListAsync();

                var galleryByPlace = galleryRows
                    .GroupBy(image => image.PlaceId)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(image => image.ImagePath)
                            .Where(path => !string.IsNullOrWhiteSpace(path))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList());

                var favoritePlaceIds = new HashSet<int>();

                if (currentUserId > 0)
                {
                    favoritePlaceIds =
                        (await _context.Favorites
                            .AsNoTracking()
                            .Where(favorite =>
                                favorite.UserId == currentUserId &&
                                pagePlaceIds.Contains(favorite.PlaceId))
                            .Select(favorite => favorite.PlaceId)
                            .ToListAsync())
                        .ToHashSet();
                }

                foreach (var result in results)
                {
                    result.IsFavorited = favoritePlaceIds.Contains(
                        result.PlaceId);

                    if (galleryByPlace.TryGetValue(
                            result.PlaceId,
                            out var gallery))
                    {
                        result.GalleryImages = gallery;
                    }
                }
            }

            /* ================================================================
               10. أفضل عرض صالح لكل بطاقة
               ================================================================ */
            if (results.Count > 0 &&
                availablePromotionCandidates.Count > 0)
            {
                var resultPlaceIdSet = results
                    .Select(result => result.PlaceId)
                    .ToHashSet();

                var promotionByPlace = availablePromotionCandidates
                    .Where(promotion =>
                        resultPlaceIdSet.Contains(promotion.PlaceId))
                    .GroupBy(promotion => promotion.PlaceId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First());

                foreach (var result in results)
                {
                    if (!promotionByPlace.TryGetValue(
                            result.PlaceId,
                            out var promotion))
                    {
                        continue;
                    }

                    result.HasActivePromotion = true;
                    result.ActivePromotionId = promotion.PromotionId;
                    result.ActivePromotionTitle = promotion.PromotionTitle;
                    result.PromotionEndDate = promotion.EndDate;

                    result.PromotionTargetLabel =
                        promotion.RoomId.HasValue
                            ? "على غرفة"
                            : promotion.PackageId.HasValue
                                ? "على باقة"
                                : promotion.ServiceId.HasValue
                                    ? "على خدمة"
                                    : "على المنشأة";

                    string searchDiscountText =
                        string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                            ? $"{promotion.DiscountValue:0.##}%"
                            : promotion.DiscountValue > 0m
                                ? currencyContext.Format(promotion.DiscountValue, result.ExchangeRateRegion)
                                : string.Empty;

                    string searchRewardServiceName = string.Empty;
                    if (promotion.RewardServiceId.HasValue && promotion.RewardServiceId.Value > 0)
                    {
                        searchRewardServiceName = await _context.PlaceAdditionalServices
                            .AsNoTracking()
                            .Where(service =>
                                service.ServiceId == promotion.RewardServiceId.Value &&
                                service.PlaceId == result.PlaceId)
                            .Select(service => service.ServiceName)
                            .FirstOrDefaultAsync() ?? string.Empty;
                    }

                    string searchSpecialPriceText = promotion.SpecialPrice.HasValue
                        ? currencyContext.Format(promotion.SpecialPrice.Value, result.ExchangeRateRegion)
                        : string.Empty;

                    string promotionOfferText = BuildCustomerPromotionSummary(
                        string.IsNullOrWhiteSpace(promotion.OfferMode) ? "Discount" : promotion.OfferMode,
                        promotion.RewardType ?? string.Empty,
                        promotion.DiscountType,
                        promotion.DiscountValue,
                        searchDiscountText,
                        promotion.RequiredQuantity,
                        promotion.RewardQuantity,
                        searchRewardServiceName,
                        searchSpecialPriceText,
                        promotion.MinimumNights,
                        promotion.IsRewardRepeatable);

                    result.PromotionOfferText = promotionOfferText;
                    result.PromotionDiscountText = promotionOfferText; // توافق مع الواجهة القديمة.
                }
            }

            /* ================================================================
               11. القوائم + ViewModel
               ================================================================ */
            var categoriesList =
                await _context.Categories
                    .AsNoTracking()
                    .Select(category => new CategoryDto
                    {
                        Id = category.CategoryId,
                        Name = category.CategoryName,
                        IconClass = category.IconClass
                    })
                    .ToListAsync();

            var citiesList =
                await _context.Cities
                    .AsNoTracking()
                    .Select(city => new CityDto
                    {
                        Id = city.CityId,
                        Name = city.CityName
                    })
                    .ToListAsync();

            // ================================================================
            // 12. إعلان الاستكشاف — Slot واحد فقط أعلى النتائج
            // ----------------------------------------------------------------
            // يظهر في الصفحة الأولى كمساحة ممولة مستقلة تحل محل Hero البحث.
            // لا نشترط أن تكون منشأة الإعلان ضمن النتائج العضوية الحالية؛
            // الفلاتر الحالية تمر للخدمة فقط لاستخدامها في الاستهداف.
            // ================================================================
            IReadOnlyList<CustomerAdvertisementDto> discoveryAdvertisements =
                Array.Empty<CustomerAdvertisementDto>();

            if (page == 1)
            {
                try
                {
                    discoveryAdvertisements =
                        await _customerAdvertisementService
                            .GetDiscoveryAdvertisementsAsync(
                                new CustomerAdvertisementDiscoveryRequestDto
                                {
                                    SearchQuery = q,
                                    CategoryId = categoryId,
                                    CityId = cityId,
                                    EligiblePlaceIds = candidates
                                        .Select(result => result.PlaceId)
                                        .Distinct()
                                        .ToList()
                                },
                                HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    // أي خلل في الإعلان لا يسمح له بإيقاف صفحة البحث.
                    Console.WriteLine(
                        $"Search advertisements warning: {ex.Message}");

                    discoveryAdvertisements =
                        Array.Empty<CustomerAdvertisementDto>();
                }
            }

            var viewModel = new PlaceSearchViewModel
            {
                SearchQuery = q,
                SelectedCategoryId = categoryId,
                SelectedCityId = cityId,
                SortOptions = sortList,
                OnlyWithOffers = onlyWithOffers,
                OnlyBookable = onlyBookable,
                Results = results,

                // صفحة Search الحالية تعرض القائمة داخل السلايدر.
                Advertisements = discoveryAdvertisements.ToList(),

                // توافق مع أي جزء قديم ما زال يتوقع إعلانًا واحدًا فقط.
                Advertisement = discoveryAdvertisements.FirstOrDefault(),

                Categories = categoriesList,
                Cities = citiesList,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize
            };

            return View(viewModel);
        }


        [HttpGet("/Places/GetNearbyPlaces", Name = "GetNearbyPlaces")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetNearbyPlaces(
            double lat,
            double lng,
            int radiusKm = 15,
            int? categoryId = null,
            string sort = "distance",
            bool onlyWithOffers = false,
            bool onlyBookable = false)
        {
            if (!IsValidNearbyCoordinate(lat, lng))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "إحداثيات موقع المستخدم غير صالحة."
                });
            }

            radiusKm = Math.Clamp(radiusKm, 1, 100);
            string[] allowedSorts = { "distance", "rating", "price_asc", "price_desc" };
            sort = allowedSorts.Contains(sort, StringComparer.OrdinalIgnoreCase)
                ? sort.ToLowerInvariant()
                : "distance";

            try
            {
                var query = _context.Places
                    .AsNoTracking()
                    .Include(place => place.Category)
                    .Include(place => place.City)
                    .Include(place => place.PlaceImages)
                    .Where(place =>
                        place.Status == "Active" &&
                        place.Coordinates != null &&
                        place.Coordinates != "");

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(place => place.CategoryId == categoryId.Value);
                }

                // نفس معنى "متاحة للحجز" في صفحة البحث:
                // خيار حجز فعّال حاليًا، وليس مجرد فئة تدعم الحجز.
                if (onlyBookable)
                {
                    query = query.Where(place =>
                        place.Category != null &&
                        (
                            (place.Category.BookingSystemId == 1 &&
                             _context.Rooms.Any(room =>
                                 room.PlaceId == place.PlaceId &&
                                 room.IsAvailable == true &&
                                 room.Quantity > 0)) ||

                            (place.Category.BookingSystemId >= 2 &&
                             place.Category.BookingSystemId <= 4 &&
                             _context.PlacePackages.Any(package =>
                                 package.PlaceId == place.PlaceId &&
                                 package.IsActive == true))
                        ));
                }

                DateTime nearbyToday = DateTime.Today;

                var nearbyPromotionCandidates =
                    await _context.Promotions
                        .AsNoTracking()
                        .Where(promotion =>
                            promotion.IsActive == true &&
                            (promotion.Status == "Active" ||
                             promotion.Status == "Scheduled") &&
                            promotion.StartDate.Date <= nearbyToday &&
                            promotion.EndDate.Date >= nearbyToday &&
                            promotion.Place.Status == "Active")
                        .Select(promotion => new
                        {
                            promotion.PromotionId,
                            promotion.PlaceId,
                            promotion.TotalUsageLimit
                        })
                        .ToListAsync();

                int[] nearbyPromotionIds =
                    nearbyPromotionCandidates
                        .Select(item => item.PromotionId)
                        .ToArray();

                var nearbyPromotionUsage =
                    new Dictionary<int, int>();

                if (nearbyPromotionIds.Length > 0)
                {
                    try
                    {
                        nearbyPromotionUsage =
                            await _context.Bookings
                                .AsNoTracking()
                                .Where(booking =>
                                    booking.PromotionId.HasValue &&
                                    nearbyPromotionIds.Contains(
                                        booking.PromotionId.Value) &&
                                    booking.BookingState != "Cancelled" &&
                                    booking.BookingState != "Rejected" &&
                                    booking.BookingState != "Expired")
                                .GroupBy(
                                    booking => booking.PromotionId!.Value)
                                .ToDictionaryAsync(
                                    group => group.Key,
                                    group => group.Count());
                    }
                    catch
                    {
                        nearbyPromotionUsage =
                            new Dictionary<int, int>();
                    }
                }

                int[] nearbyPlacesWithOffers =
                    nearbyPromotionCandidates
                        .Where(promotion =>
                        {
                            int usedCount =
                                nearbyPromotionUsage.TryGetValue(
                                    promotion.PromotionId,
                                    out int storedUsedCount)
                                        ? storedUsedCount
                                        : 0;

                            return
                                !promotion.TotalUsageLimit.HasValue ||
                                usedCount <
                                promotion.TotalUsageLimit.Value;
                        })
                        .Select(promotion => promotion.PlaceId)
                        .Distinct()
                        .ToArray();

                if (onlyWithOffers)
                {
                    query =
                        nearbyPlacesWithOffers.Length == 0
                            ? query.Where(place => false)
                            : query.Where(place =>
                                nearbyPlacesWithOffers.Contains(
                                    place.PlaceId));
                }

                var activePlaces = await query.ToListAsync();
                var activePlaceIds = activePlaces.Select(place => place.PlaceId).ToArray();
                var ratingByPlace = new Dictionary<int, double>();

                if (activePlaceIds.Length > 0)
                {
                    ratingByPlace = await _context.Reviews
                        .AsNoTracking()
                        .Where(review =>
                            activePlaceIds.Contains(review.PlaceId) &&
                            review.ParentReviewId == null &&
                            review.Stars.HasValue &&
                            review.Stars.Value >= 1 &&
                            review.Stars.Value <= 5)
                        .GroupBy(review => review.PlaceId)
                        .Select(group => new
                        {
                            PlaceId = group.Key,
                            Rating = group.Average(review => (double)review.Stars!.Value)
                        })
                        .ToDictionaryAsync(item => item.PlaceId, item => item.Rating);
                }

                var roomMinPriceByPlace = new Dictionary<int, decimal>();
                var packageMinPriceByPlace = new Dictionary<int, decimal>();

                if (activePlaceIds.Length > 0)
                {
                    roomMinPriceByPlace = (await _context.Rooms
                            .AsNoTracking()
                            .Where(room =>
                                activePlaceIds.Contains(room.PlaceId) &&
                                room.IsAvailable == true &&
                                room.Quantity > 0)
                            .GroupBy(room => room.PlaceId)
                            .Select(group => new
                            {
                                PlaceId = group.Key,
                                Price = group.Min(room => room.Price)
                            })
                            .ToListAsync())
                        .ToDictionary(item => item.PlaceId, item => item.Price);

                    packageMinPriceByPlace = (await _context.PlacePackages
                            .AsNoTracking()
                            .Where(package =>
                                activePlaceIds.Contains(package.PlaceId) &&
                                package.IsActive == true)
                            .GroupBy(package => package.PlaceId)
                            .Select(group => new
                            {
                                PlaceId = group.Key,
                                Price = group.Min(package => package.Price)
                            })
                            .ToListAsync())
                        .ToDictionary(item => item.PlaceId, item => item.Price);
                }

                // نقرأ عملة المستخدم مرة واحدة فقط لهذا الطلب.
                // CurrencyDisplayService نفسها تحفظها داخل الـ scope الحالي.
                string displayCurrency =
                    await _currencyDisplayService.GetPreferredCurrencyAsync();

                var nearbyPlaces = new List<NearbyPlaceResult>();

                foreach (var place in activePlaces)
                {
                    if (!TryParseNearbyCoordinates(
                            place.Coordinates,
                            out double placeLat,
                            out double placeLng))
                    {
                        continue;
                    }

                    double distanceKm = CalculateNearbyDistanceKm(
                        lat,
                        lng,
                        placeLat,
                        placeLng);

                    if (distanceKm > radiusKm)
                    {
                        continue;
                    }

                    string? image =
                        place.CoverImage ??
                        place.LogoImage ??
                        place.PlaceImages
                            .Select(item => item.ImagePath)
                            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

                    decimal? displayPrice = null;
                    int bookingSystemId = place.Category?.BookingSystemId ?? 0;

                    if (bookingSystemId == 1 &&
                        roomMinPriceByPlace.TryGetValue(place.PlaceId, out decimal roomPrice))
                    {
                        displayPrice = roomPrice;
                    }
                    else if (bookingSystemId >= 2 && bookingSystemId <= 4 &&
                             packageMinPriceByPlace.TryGetValue(place.PlaceId, out decimal packagePrice))
                    {
                        displayPrice = packagePrice;
                    }
                    else if (place.Price > 0)
                    {
                        displayPrice = place.Price;
                    }

                    decimal? convertedDisplayPrice = null;
                    string? displayPriceText = null;

                    if (displayPrice.HasValue && displayPrice.Value > 0)
                    {
                        string? exchangeRateRegion =
                            place.City?.ExchangeRateRegion;

                        convertedDisplayPrice =
                            await _currencyDisplayService
                                .ConvertAmountByRegionAsync(
                                    displayPrice.Value,
                                    exchangeRateRegion);

                        displayPriceText =
                            await _currencyDisplayService
                                .FormatByRegionAsync(
                                    displayPrice.Value,
                                    exchangeRateRegion);
                    }

                    nearbyPlaces.Add(new NearbyPlaceResult
                    {
                        Id = place.PlaceId,
                        Name = place.PlaceNameAndDesc ?? "وجهة قريبة",
                        Latitude = placeLat,
                        Longitude = placeLng,
                        Image = image,
                        Category = place.Category?.CategoryName ?? "وجهة",
                        CategoryId = place.CategoryId,
                        City = place.City?.CityName ?? string.Empty,
                        Address = place.AddressDetail ?? string.Empty,
                        Rating = ratingByPlace.TryGetValue(place.PlaceId, out double rating)
                            ? Math.Round(rating, 1)
                            : 0,

                        // Price يبقى السعر الأصلي بالريال اليمني للتوافق.
                        Price = displayPrice,

                        // القيمة الرقمية المحولة تستخدم للترتيب حسب عملة المستخدم.
                        ConvertedPrice = convertedDisplayPrice,

                        // النص الجاهز هو الذي تعرضه الواجهة.
                        PriceText = displayPriceText,

                        DistanceKm = distanceKm,

                        HasActiveOffer =
                            nearbyPlacesWithOffers.Contains(
                                place.PlaceId)
                    });
                }

                IEnumerable<NearbyPlaceResult> ordered = sort switch
                {
                    "rating" => nearbyPlaces
                        .OrderByDescending(place => place.Rating)
                        .ThenBy(place => place.DistanceKm),
                    "price_asc" => nearbyPlaces
                        .OrderBy(place => place.ConvertedPrice.HasValue ? 0 : 1)
                        .ThenBy(place => place.ConvertedPrice)
                        .ThenBy(place => place.DistanceKm),
                    "price_desc" => nearbyPlaces
                        .OrderBy(place => place.ConvertedPrice.HasValue ? 0 : 1)
                        .ThenByDescending(place => place.ConvertedPrice)
                        .ThenBy(place => place.DistanceKm),
                    _ => nearbyPlaces.OrderBy(place => place.DistanceKm)
                };

                var visibleNearbyPlaces = ordered
                    .Take(50)
                    .ToList();

                // إعلان MapPin لا ينشئ نتيجة وهمية في الخريطة؛ بل يعلّم منشأة
                // موجودة أصلًا داخل النطاق الحالي كممولة. الحد الأعلى: اثنان.
                IReadOnlyList<CustomerAdvertisementDto> mapAdvertisements =
                    Array.Empty<CustomerAdvertisementDto>();

                try
                {
                    mapAdvertisements =
                        await _customerAdvertisementService
                            .GetMapAdvertisementsAsync(
                                new CustomerAdvertisementMapRequestDto
                                {
                                    UserLatitude = lat,
                                    UserLongitude = lng,
                                    CategoryId = categoryId,
                                    MaxAdvertisements = 2,
                                    EligiblePlaceIds = visibleNearbyPlaces
                                        .Select(place => place.Id)
                                        .ToList()
                                },
                                HttpContext.RequestAborted);
                }
                catch
                {
                    // الخريطة الأساسية لا تعتمد على توفر طبقة الإعلانات.
                    mapAdvertisements = Array.Empty<CustomerAdvertisementDto>();
                }

                var mapAdsByPlace = mapAdvertisements
                    .GroupBy(ad => ad.PlaceId)
                    .ToDictionary(group => group.Key, group => group.First());

                return Json(visibleNearbyPlaces
                    .Select(place =>
                    {
                        mapAdsByPlace.TryGetValue(
                            place.Id,
                            out var sponsoredAdvertisement);

                        return new
                        {
                            id = place.Id,
                            name = place.Name,
                            lat = place.Latitude,
                            lng = place.Longitude,
                            image = place.Image,
                            category = place.Category,
                            categoryId = place.CategoryId,
                            city = place.City,
                            address = place.Address,
                            rating = place.Rating,

                            // للتوافق مع أي كود قديم: السعر الخام بالريال اليمني.
                            price = place.Price,
                            priceYer = place.Price,

                            // السعر بعد التحويل حسب عملة المستخدم وسوق المدينة.
                            displayPrice = place.ConvertedPrice,
                            priceText = place.PriceText,
                            currency = displayCurrency,
                            currencyCode =
                                CurrencyDisplayService.GetCurrencyCode(
                                    displayCurrency),

                            hasPrice = place.Price.HasValue,
                            distanceKm = Math.Round(place.DistanceKm, 2),
                            hasActiveOffer = place.HasActiveOffer,

                            // بيانات الإعلان لا تظهر إلا للـPin الذي اختاره المحرك.
                            isSponsored = sponsoredAdvertisement != null,
                            advertisementId = sponsoredAdvertisement?.AdvertisementId,
                            advertisementPlacement = sponsoredAdvertisement?.PlacementType,
                            advertisementTitle = sponsoredAdvertisement?.AdvertisementTitle,
                            advertisementDescription = sponsoredAdvertisement?.ShortDescription,
                            advertisementButtonText = sponsoredAdvertisement?.ButtonText,
                            advertisementHref = sponsoredAdvertisement?.FallbackUrl,
                            advertisementMedia = sponsoredAdvertisement?.MediaPath
                        };
                    })
                    .ToList());
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر جلب الأماكن القريبة من قاعدة البيانات."
                });
            }
        }

        private sealed class NearbyPlaceResult
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string? Image { get; set; }
            public string Category { get; set; } = string.Empty;
            public int CategoryId { get; set; }
            public string City { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public double Rating { get; set; }

            // السعر الخام كما هو مخزن في النظام بالريال اليمني.
            public decimal? Price { get; set; }

            // السعر المحول رقميًا حسب عملة المستخدم.
            public decimal? ConvertedPrice { get; set; }

            // النص النهائي الجاهز للعرض: $... / ... ر.س / ... ر.ي
            public string? PriceText { get; set; }

            public double DistanceKm { get; set; }

            public bool HasActiveOffer { get; set; }
        }

        private static bool TryParseNearbyCoordinates(
            string? rawCoordinates,
            out double latitude,
            out double longitude)
        {
            latitude = 0;
            longitude = 0;

            if (string.IsNullOrWhiteSpace(rawCoordinates))
            {
                return false;
            }

            string value = rawCoordinates.Trim().Replace('،', ',');
            try
            {
                value = Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                // نستخدم النص الأصلي إن كان رابطاً غير مكتمل.
            }

            Match pointMatch = Regex.Match(
                value,
                @"POINT\s*\(\s*(?<lng>[-+]?\d+(?:\.\d+)?)\s+(?<lat>[-+]?\d+(?:\.\d+)?)\s*\)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (pointMatch.Success &&
                TryParseNearbyNumber(pointMatch.Groups["lat"].Value, out double pointLat) &&
                TryParseNearbyNumber(pointMatch.Groups["lng"].Value, out double pointLng) &&
                IsValidNearbyCoordinate(pointLat, pointLng))
            {
                latitude = pointLat;
                longitude = pointLng;
                return true;
            }

            Match googleUrlMatch = Regex.Match(
                value,
                @"@(?<lat>[-+]?\d+(?:\.\d+)?),(?<lng>[-+]?\d+(?:\.\d+)?)",
                RegexOptions.CultureInvariant);

            if (googleUrlMatch.Success &&
                TryParseNearbyNumber(googleUrlMatch.Groups["lat"].Value, out double urlLat) &&
                TryParseNearbyNumber(googleUrlMatch.Groups["lng"].Value, out double urlLng) &&
                IsValidNearbyCoordinate(urlLat, urlLng))
            {
                latitude = urlLat;
                longitude = urlLng;
                return true;
            }

            Match pairMatch = Regex.Match(
                value,
                @"(?<lat>[-+]?\d{1,2}(?:\.\d+)?)\s*,\s*(?<lng>[-+]?\d{1,3}(?:\.\d+)?)",
                RegexOptions.CultureInvariant);

            if (pairMatch.Success &&
                TryParseNearbyNumber(pairMatch.Groups["lat"].Value, out double pairLat) &&
                TryParseNearbyNumber(pairMatch.Groups["lng"].Value, out double pairLng) &&
                IsValidNearbyCoordinate(pairLat, pairLng))
            {
                latitude = pairLat;
                longitude = pairLng;
                return true;
            }

            return false;
        }

        private static bool TryParseNearbyNumber(string value, out double result)
        {
            return double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static bool IsValidNearbyCoordinate(double latitude, double longitude)
        {
            return !double.IsNaN(latitude) &&
                   !double.IsInfinity(latitude) &&
                   !double.IsNaN(longitude) &&
                   !double.IsInfinity(longitude) &&
                   latitude >= -90 && latitude <= 90 &&
                   longitude >= -180 && longitude <= 180;
        }

        private static double CalculateNearbyDistanceKm(
            double firstLatitude,
            double firstLongitude,
            double secondLatitude,
            double secondLongitude)
        {
            const double earthRadiusKm = 6371.0088;
            double latitudeDelta = NearbyToRadians(secondLatitude - firstLatitude);
            double longitudeDelta = NearbyToRadians(secondLongitude - firstLongitude);
            double firstLatitudeRadians = NearbyToRadians(firstLatitude);
            double secondLatitudeRadians = NearbyToRadians(secondLatitude);

            double haversine =
                Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) +
                Math.Cos(firstLatitudeRadians) * Math.Cos(secondLatitudeRadians) *
                Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);

            return earthRadiusKm * 2 * Math.Atan2(
                Math.Sqrt(haversine),
                Math.Sqrt(1 - haversine));
        }

        private static double NearbyToRadians(double value)
        {
            return value * Math.PI / 180d;
        }
        private static string BuildCustomerPromotionSummary(
            string offerMode,
            string rewardType,
            string? discountType,
            decimal discountValue,
            string displayDiscountText,
            int? requiredQuantity,
            int? rewardQuantity,
            string rewardServiceName,
            string specialPriceText,
            int? minimumNights,
            bool isRewardRepeatable)
        {
            if (!string.Equals(offerMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(discountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                    ? $"خصم {discountValue:0.##}%"
                    : string.IsNullOrWhiteSpace(displayDiscountText) ? "عرض خاص" : $"خصم {displayDiscountText}";
            }

            if (string.Equals(rewardType, "FreeQuantity", StringComparison.OrdinalIgnoreCase))
            {
                int required = minimumNights.GetValueOrDefault() > 0
                    ? minimumNights!.Value
                    : Math.Max(1, requiredQuantity.GetValueOrDefault(1));
                int reward = Math.Max(1, rewardQuantity.GetValueOrDefault(1));
                bool nights = minimumNights.GetValueOrDefault() > 0;
                return nights
                    ? isRewardRepeatable
                        ? $"احجز {required} ليلة واحصل على {reward} ليلة مجانًا — العرض يتكرر"
                        : $"احجز {required} ليلة واحصل على {reward} ليلة مجانًا — مرة واحدة فقط"
                    : isRewardRepeatable
                        ? $"احجز {required} واحصل على {reward} مجانًا — العرض يتكرر"
                        : $"احجز {required} واحصل على {reward} مجانًا — مرة واحدة فقط";
            }

            if (string.Equals(rewardType, "FreeService", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(rewardServiceName) ? "خدمة إضافية مجانًا" : $"{rewardServiceName} مجانًا";

            if (string.Equals(rewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(specialPriceText) ? "سعر خاص" : $"سعر خاص {specialPriceText}";

            if (string.Equals(rewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase))
            {
                string service = string.IsNullOrWhiteSpace(rewardServiceName) ? "الخدمة" : rewardServiceName;
                return string.IsNullOrWhiteSpace(specialPriceText) ? $"سعر خاص لـ {service}" : $"{service} بسعر {specialPriceText}";
            }

            if (string.Equals(rewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase))
                return $"خصم {discountValue:0.##}% عند تحقق الشروط";

            if (string.Equals(rewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(displayDiscountText) ? "خصم خاص عند تحقق الشروط" : $"خصم {displayDiscountText} عند تحقق الشروط";

            return "عرض خاص";
        }

    }
}
