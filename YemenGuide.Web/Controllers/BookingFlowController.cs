using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Finance;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Services.Notifications;
using YemenGuide.Web.Services.Advertisements;

namespace YemenGuide.Web.Controllers
{
    [Authorize]
    public class BookingFlowController : Controller
    {
        private const long MaximumPaymentProofSize =
            5 * 1024 * 1024;

        private static readonly string[] AllowedPaymentProofExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private static readonly string[] BlockingBookingStates =
        {
            "Pending",
            "AwaitingPayment",
            "PaymentReview",
            "Confirmed"
        };

        private static readonly string[] BlockingRoomAssignmentStates =
        {
            "Held",
            "Confirmed"
        };

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICommissionFinanceService
            _commissionFinanceService;
        private readonly INotificationService
            _notificationService;
        private readonly CurrencyDisplayService
            _currencyDisplayService;
        private readonly ICustomerAdvertisementService
            _customerAdvertisementService;

        public BookingFlowController(
            AppDbContext context,
            IWebHostEnvironment environment,
            ICommissionFinanceService commissionFinanceService,
            INotificationService notificationService,
            CurrencyDisplayService currencyDisplayService,
            ICustomerAdvertisementService customerAdvertisementService)
        {
            _context = context;
            _environment = environment;
            _commissionFinanceService = commissionFinanceService;
            _notificationService = notificationService;
            _currencyDisplayService = currencyDisplayService;
            _customerAdvertisementService = customerAdvertisementService;
        }

        private sealed class CustomerCurrencyContext
        {
            public string Currency { get; init; } = CurrencyDisplayService.YemeniRial;
            public string Code { get; init; } = "YER";
            public string Symbol { get; init; } = "ر.ي";
            public decimal SanaaUsdRate { get; init; } = 537m;
            public decimal SanaaSarRate { get; init; } = 140m;
            public decimal AdenUsdRate { get; init; } = 1900m;
            public decimal AdenSarRate { get; init; } = 500m;

            public decimal Convert(decimal amountInYer, string? exchangeRateRegion)
            {
                if (Currency == CurrencyDisplayService.YemeniRial)
                {
                    return Math.Round(amountInYer, 0, MidpointRounding.AwayFromZero);
                }

                bool useAdenMarket = string.Equals(
                    exchangeRateRegion,
                    "South",
                    StringComparison.OrdinalIgnoreCase);

                decimal rate = Currency == CurrencyDisplayService.UsDollar
                    ? useAdenMarket ? AdenUsdRate : SanaaUsdRate
                    : useAdenMarket ? AdenSarRate : SanaaSarRate;

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

            public string Format(decimal amountInYer, string? exchangeRateRegion)
            {
                decimal converted = Convert(amountInYer, exchangeRateRegion);

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

        private async Task<CustomerCurrencyContext> GetCustomerCurrencyContextAsync()
        {
            string currency = await _currencyDisplayService
                .GetPreferredCurrencyAsync(HttpContext.RequestAborted);

            var rates = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .Select(setting => new
                {
                    setting.SanaaUsdRate,
                    setting.SanaaSarRate,
                    setting.AdenUsdRate,
                    setting.AdenSarRate
                })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            return new CustomerCurrencyContext
            {
                Currency = currency,
                Code = CurrencyDisplayService.GetCurrencyCode(currency),
                Symbol = CurrencyDisplayService.GetCurrencySymbol(currency),
                SanaaUsdRate = rates?.SanaaUsdRate > 0m ? rates.SanaaUsdRate : 537m,
                SanaaSarRate = rates?.SanaaSarRate > 0m ? rates.SanaaSarRate : 140m,
                AdenUsdRate = rates?.AdenUsdRate > 0m ? rates.AdenUsdRate : 1900m,
                AdenSarRate = rates?.AdenSarRate > 0m ? rates.AdenSarRate : 500m
            };
        }

        /* ================================================================
           أدوات عامة
           ================================================================ */

        private static int CalculateHotelEquivalentGuestCapacity(
            int adults,
            int children)
        {
            var safeAdults =
                Math.Max(0, adults);

            var safeChildren =
                Math.Max(0, children);

            /*
             * الفنادق:
             * أول 4 أطفال فقط يستفيدون من قاعدة طفلين مقابل مكان بالغ.
             * الطفل الخامس وما بعده يُحتسب كل واحد منهم كشخص كامل.
             * ونستخدم Ceiling حتى لا ينتج نصف مقعد عند وجود عدد فردي من الأطفال.
             */
            var convertibleChildren =
                Math.Min(
                    safeChildren,
                    4);

            var convertedSlots =
                (int)Math.Ceiling(
                    convertibleChildren /
                    2m);

            var extraChildren =
                Math.Max(
                    0,
                    safeChildren - 4);

            return safeAdults +
                   convertedSlots +
                   extraChildren;
        }

        private static (bool Valid, string Message) ValidateRoomGuestCapacity(
            int adults,
            int children,
            int maximumCapacity,
            string itemLabel)
        {
            if (adults < 1)
            {
                return (
                    false,
                    $"لا يمكن إكمال حجز {itemLabel} بأطفال فقط. يجب وجود شخص بالغ واحد على الأقل.");
            }

            if (children < 0)
            {
                return (
                    false,
                    "عدد الأطفال غير صحيح.");
            }

            if (
                maximumCapacity > 0 &&
                CalculateHotelEquivalentGuestCapacity(
                    adults,
                    children) > maximumCapacity
            )
            {
                return (
                    false,
                    $"عدد الأشخاص أكبر من سعة {itemLabel} ({maximumCapacity}). أول 4 أطفال فقط يُحتسب كل طفلين منهم مكان بالغ واحد، ومن الطفل الخامس يُحتسب كل طفل كشخص كامل.");
            }

            return (
                true,
                string.Empty);
        }

        private static int GetPackageExtraChildrenAllowance(
            int maximumCapacity)
        {
            var capacity =
                Math.Max(
                    0,
                    maximumCapacity);

            if (capacity <= 0)
            {
                return 0;
            }

            if (capacity < 10)
            {
                return 2;
            }

            if (capacity == 10)
            {
                return 3;
            }

            if (capacity <= 50)
            {
                return 5;
            }

            return Math.Min(
                10,
                (int)Math.Floor(
                    capacity *
                    0.10m));
        }

        private static (bool Valid, string Message) ValidatePackageGuestCapacity(
            int adults,
            int children,
            int maximumCapacity,
            string itemLabel)
        {
            if (adults < 1)
            {
                return (
                    false,
                    $"لا يمكن إكمال حجز {itemLabel} بأطفال فقط. يجب وجود شخص بالغ واحد على الأقل.");
            }

            if (children < 0)
            {
                return (
                    false,
                    "عدد الأطفال غير صحيح.");
            }

            var capacity =
                Math.Max(
                    0,
                    maximumCapacity);

            if (capacity <= 0)
            {
                return (
                    true,
                    string.Empty);
            }

            /*
             * الشاليهات/الباقات ذات عداد الأشخاص:
             * البالغون لا يتجاوزون السعة الأساسية نهائيًا.
             * الأطفال يُحسبون كأشخاص عاديين داخل السعة، ثم نسمح لهم فقط
             * بتجاوزها حسب الشريحة المتفق عليها.
             */
            if (adults > capacity)
            {
                return (
                    false,
                    $"عدد البالغين لا يمكن أن يتجاوز سعة {itemLabel} الأساسية ({capacity}).");
            }

            var extraChildrenAllowance =
                GetPackageExtraChildrenAllowance(
                    capacity);

            var absoluteMaximum =
                capacity +
                extraChildrenAllowance;

            if (
                adults +
                children >
                absoluteMaximum
            )
            {
                return (
                    false,
                    $"السعة الأساسية لـ {itemLabel} هي {capacity} أشخاص، ويسمح بتجاوزها بـ {extraChildrenAllowance} أطفال فقط كحد أقصى.");
            }

            return (
                true,
                string.Empty);
        }

        private bool TryGetCustomerId(
            out int customerId)
        {
            var customerIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                customerIdText,
                out customerId);
        }

        private static bool IsState(
            string? value,
            string expected)
        {
            return string.Equals(
                value?.Trim(),
                expected,
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

        private static bool ContainsWeeklyClosedDate(
            Place place,
            DateTime startDate,
            DateTime? endDate,
            bool isRoomBooking)
        {
            var csv =
                place.PlacePolicy?
                    .ClosedDaysCsv?
                    .Trim();

            if (string.IsNullOrWhiteSpace(csv))
            {
                return false;
            }

            var closedDays =
                csv.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

            if (closedDays.Count == 0)
            {
                return false;
            }

            var firstDate =
                startDate.Date;

            var exclusiveEnd =
                isRoomBooking &&
                endDate.HasValue &&
                endDate.Value.Date >
                    firstDate
                    ? endDate.Value.Date
                    : firstDate.AddDays(1);

            for (
                var day = firstDate;
                day < exclusiveEnd;
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
                // سياسة قديمة أو JSON غير صالح: لا نفترض وجود فترة صالحة.
            }

            return result;
        }

        private static bool PackageFitsConfiguredShift(
            Place place,
            PlacePackage package)
        {
            if (!IsState(
                    place.PlacePolicy?
                        .OperationSystemMode,
                    "Shifts"))
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
                package.StartTime.Value;

            var packageEnd =
                package.EndTime.Value;

            var packageEndMinutes =
                packageEnd.TotalMinutes;

            if (packageEnd <= packageStart)
            {
                packageEndMinutes +=
                    TimeSpan.FromDays(1)
                        .TotalMinutes;
            }

            foreach (
                var shift in
                ReadPolicyShiftWindows(
                    place.PlacePolicy?
                        .ShiftsJson))
            {
                var shiftStartMinutes =
                    shift.Start.TotalMinutes;

                var shiftEndMinutes =
                    shift.End.TotalMinutes;

                if (shift.End <= shift.Start)
                {
                    shiftEndMinutes +=
                        TimeSpan.FromDays(1)
                            .TotalMinutes;
                }

                if (
                    packageStart.TotalMinutes >=
                        shiftStartMinutes &&
                    packageEndMinutes <=
                        shiftEndMinutes)
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
            var startTime =
                package.StartTime ??
                TimeSpan.Zero;

            var endTime =
                package.EndTime ??
                startTime;

            var start =
                date.Date.Add(
                    startTime);

            var end =
                date.Date.Add(
                    endTime);

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
            DateTime existingDate,
            PlacePackage existingPackage,
            int bufferHours)
        {
            if (
                !requestedPackage.StartTime.HasValue ||
                !requestedPackage.EndTime.HasValue ||
                !existingPackage.StartTime.HasValue ||
                !existingPackage.EndTime.HasValue)
            {
                return true;
            }

            var requested =
                GetPackageOperationalInterval(
                    requestedDate,
                    requestedPackage);

            var existing =
                GetPackageOperationalInterval(
                    existingDate,
                    existingPackage);

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

        private async Task<bool>
            HasPackageBookingConflictAsync(
                Place place,
                PlacePackage selectedPackage,
                DateTime requestedDate,
                DateTime referenceTime)
        {
            /*
             * قاعدة التوفر الصحيحة للقاعات والشاليهات والباقات:
             *
             * 1) اليوم الكامل / أي وضع غير Shifts:
             *    الحجز يمنع اليوم نفسه فقط.
             *    حجز يوم 20 لا يمنع 21 أو 22 أو 26.
             *
             * 2) نظام Shifts:
             *    نحتاج فحص اليوم السابق واليوم التالي أيضًا فقط لأن
             *    الباقة قد تعبر منتصف الليل أو لأن BufferTimeHours
             *    قد يصنع تعارضًا زمنيًا حقيقيًا بين يومين متجاورين.
             */
            var shiftMode =
                IsState(
                    place.PlacePolicy?
                        .OperationSystemMode,
                    "Shifts");

            var requestedDay =
                requestedDate.Date;

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

                        BlockingBookingStates.Contains(
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

            if (existingBookings.Count == 0)
            {
                return false;
            }

            if (!shiftMode)
            {
                /*
                 * الاستعلام في هذا الوضع محصور أصلًا في requestedDay،
                 * لذلك وجود أي حجز حاجز يعني أن اليوم نفسه مشغول.
                 */
                return existingBookings.Any(booking =>
                    booking.StartDate.Date ==
                    requestedDay);
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
                    /*
                     * سجل قديم بدون بيانات باقة كافية:
                     * نحجبه فقط إذا كان في اليوم نفسه بدل منع أيام بعيدة.
                     */
                    if (booking.StartDate.Date ==
                        requestedDay)
                    {
                        return true;
                    }

                    continue;
                }

                if (PackageIntervalsConflict(
                        requestedDate,
                        selectedPackage,
                        booking.StartDate,
                        booking.Package,
                        bufferHours))
                {
                    return true;
                }
            }

            return false;
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

            var naturalGapHours =
                Math.Max(
                    0d,
                    (nextCheckInAt -
                     checkOutAt)
                    .TotalHours);

            return bufferHours >
                   naturalGapHours;
        }

        private static DateTime
            GetRequestedOperationalStartAt(
                Place place,
                DateTime startDate,
                PlacePackage? package)
        {
            if (package?.StartTime.HasValue ==
                true)
            {
                return startDate.Date.Add(
                    package.StartTime.Value);
            }

            return startDate.Date.Add(
                GetPolicyCheckInTime(
                    place));
        }

        private static string?
            ValidateMinimumNotice(
                Place place,
                DateTime startDate,
                PlacePackage? package,
                DateTime now)
        {
            var minimumNoticeHours =
                Math.Max(
                    0,
                    place.PlacePolicy?
                        .MinimumNoticeHours ?? 0);

            if (minimumNoticeHours <= 0)
            {
                return null;
            }

            var requestedStartAt =
                GetRequestedOperationalStartAt(
                    place,
                    startDate,
                    package);

            if (
                requestedStartAt <
                now.AddHours(
                    minimumNoticeHours))
            {
                return
                    $"يجب إجراء الحجز قبل الموعد بـ {minimumNoticeHours} ساعة على الأقل.";
            }

            return null;
        }

        private static void ReleaseHeldRoomUnits(
            Booking booking,
            DateTime releasedAt)
        {
            foreach (
                var assignment in
                booking.BookingRoomUnits.Where(assignment =>
                    IsState(
                        assignment.AssignmentStatus,
                        "Held")))
            {
                assignment.AssignmentStatus =
                    "Released";

                assignment.ReleasedAt =
                    releasedAt;
            }
        }


        /* ================================================================
           التحقق من اكتمال بيانات العميل اللازمة للحجز
           ================================================================ */

        private static bool IsValidBookingEmail(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Regex.IsMatch(
                value.Trim(),
                @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
                RegexOptions.CultureInvariant);
        }

        private static bool IsValidBookingPhoneNumber(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized =
                NormalizeDigits(value)
                    .Trim();

            normalized =
                Regex.Replace(
                    normalized,
                    @"[\s\-\(\)\.]",
                    string.Empty);

            if (normalized.StartsWith(
                    "00",
                    StringComparison.Ordinal))
            {
                normalized =
                    "+" +
                    normalized[2..];
            }

            if (!Regex.IsMatch(
                    normalized,
                    @"^\+?[1-9]\d{6,14}$",
                    RegexOptions.CultureInvariant))
            {
                return false;
            }

            var digitsOnly =
                normalized.TrimStart('+');

            return digitsOnly.Any(digit =>
                digit != '0');
        }

        private static List<string>
            GetMissingBookingProfileFields(
                User customer)
        {
            var missingFields =
                new List<string>();

            if (string.IsNullOrWhiteSpace(
                    customer.FullName) ||
                customer.FullName.Trim().Length < 2)
            {
                missingFields.Add(
                    "الاسم الكامل");
            }

            if (!IsValidBookingEmail(
                    customer.ContactInfo))
            {
                missingFields.Add(
                    "البريد الإلكتروني");
            }
            else if (!customer.IsEmailVerified)
            {
                missingFields.Add(
                    "توثيق البريد الإلكتروني");
            }

            if (!IsValidBookingPhoneNumber(
                    customer.PhoneNumber))
            {
                missingFields.Add(
                    "رقم الهاتف");
            }

            if (string.IsNullOrWhiteSpace(
                    customer.City))
            {
                missingFields.Add(
                    "المدينة");
            }

            return missingFields;
        }

        private string NormalizeBookingReturnUrl(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "/";
            }

            var trimmed =
                value.Trim();

            if (Uri.TryCreate(
                    trimmed,
                    UriKind.Absolute,
                    out var absoluteUri))
            {
                if (!string.Equals(
                        absoluteUri.Authority,
                        Request.Host.Value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "/";
                }

                return
                    absoluteUri.PathAndQuery +
                    absoluteUri.Fragment;
            }

            if (trimmed.StartsWith(
                    "/",
                    StringComparison.Ordinal) &&
                !trimmed.StartsWith(
                    "//",
                    StringComparison.Ordinal) &&
                !trimmed.StartsWith(
                    "/\\",
                    StringComparison.Ordinal))
            {
                return trimmed;
            }

            return "/";
        }

        private string BuildProfileCompletionUrl(
            string? returnUrl)
        {
            var localReturnUrl =
                NormalizeBookingReturnUrl(
                    returnUrl);

            return Url.Action(
                       "CustomerProfile",
                       "Profile",
                       new
                       {
                           returnUrl =
                               localReturnUrl
                       }) ??
                   "/Profile/CustomerProfile";
        }

        private static string NormalizeDigits(
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

        private static decimal CalculateDepositAmount(
            Place place,
            decimal totalPrice)
        {
            if (totalPrice <= 0)
            {
                return 0;
            }

            var policiesJson =
                place.PlacePolicy?
                    .DepositPoliciesJson;

            if (!string.IsNullOrWhiteSpace(
                policiesJson))
            {
                try
                {
                    using var document =
                        JsonDocument.Parse(
                            policiesJson);

                    if (document.RootElement.ValueKind ==
                        JsonValueKind.Array)
                    {
                        foreach (
                            var policy in
                            document.RootElement
                                .EnumerateArray())
                        {
                            if (!policy.TryGetProperty(
                                "val",
                                out var valueElement))
                            {
                                continue;
                            }

                            string? valueText;

                            if (valueElement.ValueKind ==
                                JsonValueKind.Number)
                            {
                                valueText =
                                    valueElement.GetRawText();
                            }
                            else if (
                                valueElement.ValueKind ==
                                JsonValueKind.String)
                            {
                                valueText =
                                    valueElement.GetString();
                            }
                            else
                            {
                                continue;
                            }

                            valueText =
                                NormalizeDigits(valueText)
                                    .Trim()
                                    .Replace("٫", ".")
                                    .Replace(",", ".");

                            if (!decimal.TryParse(
                                valueText,
                                NumberStyles.Number,
                                CultureInfo.InvariantCulture,
                                out var policyValue) ||
                                policyValue <= 0)
                            {
                                continue;
                            }

                            var policyType =
                                policy.TryGetProperty(
                                    "type",
                                    out var typeElement)
                                    ? typeElement
                                        .GetString()?
                                        .Trim() ?? string.Empty
                                    : string.Empty;

                            var isPercentage =
                                policyType.Contains("%") ||
                                policyType.Contains(
                                    "نسبة",
                                    StringComparison.OrdinalIgnoreCase) ||
                                policyType.Contains(
                                    "percent",
                                    StringComparison.OrdinalIgnoreCase);

                            var depositAmount =
                                isPercentage
                                    ? totalPrice *
                                      policyValue /
                                      100m
                                    : policyValue;

                            return Math.Round(
                                Math.Min(
                                    Math.Max(
                                        0,
                                        depositAmount),
                                    totalPrice),
                                2);
                        }
                    }
                }
                catch (JsonException)
                {
                    // عند وجود JSON غير صالح نرجع للحقول القديمة.
                }
            }

            if (place.IsDepositRequired &&
                place.DepositAmount > 0)
            {
                return Math.Round(
                    Math.Min(
                        place.DepositAmount,
                        totalPrice),
                    2);
            }

            return 0;
        }

        private static bool HasDepositPolicy(
            Place place)
        {
            return CalculateDepositAmount(
                place,
                100m) > 0;
        }


        private static string NormalizeImagePath(
        string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
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
                normalized =
                    "/" +
                    normalized.Substring(2);
            }
            else if (!normalized.StartsWith("/"))
            {
                normalized =
                    "/" +
                    normalized;
            }

            return normalized;
        }

        private static List<string> ReadImagePaths(
            string? value)
        {
            var images =
                new List<string>();

            if (string.IsNullOrWhiteSpace(value))
            {
                return images;
            }

            var trimmedValue =
                value.Trim();

            /*
             * يدعم JSON:
             * ["/images/a.jpg", "/images/b.jpg"]
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
                    // نرجع للتقسيم النصي في الأسفل.
                }
            }

            /*
             * يدعم صورة واحدة أو عدة صور مفصولة
             * بفاصلة أو فاصلة منقوطة أو سطر.
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

        private static string FirstImagePath(
            string? value)
        {
            return ReadImagePaths(value)
                .FirstOrDefault() ??
                string.Empty;
        }

        private static int? ExtractRoomCapacity(
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
                    System.Text.RegularExpressions
                        .RegexOptions
                        .IgnoreCase);

            if (!match.Success ||
                !int.TryParse(
                    match.Groups[1].Value,
                    out var capacity) ||
                capacity <= 0)
            {
                return null;
            }

            return capacity;
        }

        private static List<string> ReadPolicyTexts(
            string? json)
        {
            var values =
                new List<string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return values;
            }

            try
            {
                using var document =
                    JsonDocument.Parse(json);

                if (document.RootElement.ValueKind !=
                    JsonValueKind.Array)
                {
                    return values;
                }

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
                            values.Add(text);
                        }

                        continue;
                    }

                    if (item.ValueKind !=
                        JsonValueKind.Object)
                    {
                        continue;
                    }

                    string? description = null;

                    foreach (
                        var propertyName in
                        new[]
                        {
                            "desc",
                            "description",
                            "text",
                            "title",
                            "name"
                        })
                    {
                        if (item.TryGetProperty(
                                propertyName,
                                out var property) &&
                            property.ValueKind ==
                                JsonValueKind.String)
                        {
                            description =
                                property.GetString()?.Trim();

                            if (!string.IsNullOrWhiteSpace(
                                    description))
                            {
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(
                            description))
                    {
                        values.Add(description);
                        continue;
                    }

                    if (item.TryGetProperty(
                            "val",
                            out var valueElement))
                    {
                        var valueText =
                            valueElement.ValueKind ==
                                JsonValueKind.String
                                ? valueElement.GetString()
                                : valueElement.GetRawText();

                        var typeText =
                            item.TryGetProperty(
                                "type",
                                out var typeElement)
                                ? typeElement.GetString()
                                : string.Empty;

                        var combined =
                            $"{valueText} {typeText}".Trim();

                        if (!string.IsNullOrWhiteSpace(
                                combined))
                        {
                            values.Add(combined);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // نتجاهل JSON غير الصالح، وتُستخدم الحقول النصية البديلة.
            }

            return values
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<CheckoutPolicyItemDto>
            BuildCheckoutPolicies(
                Place place,
                decimal depositAmount,
                string depositAmountText)
        {
            var policies =
                new List<CheckoutPolicyItemDto>();

            var depositTexts =
                ReadPolicyTexts(
                    place.PlacePolicy?
                        .DepositPoliciesJson);

            if (!string.IsNullOrWhiteSpace(
                    place.DepositPolicy))
            {
                depositTexts.Add(
                    place.DepositPolicy.Trim());
            }

            depositTexts =
                depositTexts
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (depositAmount > 0 ||
                depositTexts.Count > 0)
            {
                var depositDescription =
                    depositTexts.Count > 0
                        ? string.Join(
                            " • ",
                            depositTexts)
                        : $"العربون المطلوب هو " +
                          $"{depositAmountText}.";

                policies.Add(
                    new CheckoutPolicyItemDto
                    {
                        Type = "deposit",
                        Title = "سياسة العربون",
                        Description =
                            depositDescription
                    });
            }

            var securityTexts =
                ReadPolicyTexts(
                    place.PlacePolicy?
                        .SecurityPoliciesJson);

            if (!string.IsNullOrWhiteSpace(
                    place.InsurancePolicy))
            {
                securityTexts.Add(
                    place.InsurancePolicy.Trim());
            }

            securityTexts =
                securityTexts
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (securityTexts.Count > 0)
            {
                policies.Add(
                    new CheckoutPolicyItemDto
                    {
                        Type = "security",
                        Title = "سياسة الضمان والاستخدام",
                        Description =
                            string.Join(
                                " • ",
                                securityTexts)
                    });
            }

            var cancellationParts =
                new List<string>();

            if (!string.IsNullOrWhiteSpace(
                    place.CancellationPolicy))
            {
                cancellationParts.Add(
                    place.CancellationPolicy.Trim());
            }

            var freeCancellationHours =
                place.PlacePolicy?
                    .FreeCancellationHours ?? 0;

            if (freeCancellationHours > 0)
            {
                cancellationParts.Add(
                    $"الإلغاء المجاني متاح قبل " +
                    $"{freeCancellationHours} ساعة.");
            }

            if (place.PlacePolicy?
                    .ConfiscateOnNoShow == true)
            {
                cancellationParts.Add(
                    "قد تتم مصادرة العربون عند عدم الحضور.");
            }

            cancellationParts =
                cancellationParts
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (cancellationParts.Count > 0)
            {
                policies.Add(
                    new CheckoutPolicyItemDto
                    {
                        Type = "cancellation",
                        Title = "سياسة الإلغاء",
                        Description =
                            string.Join(
                                " • ",
                                cancellationParts)
                    });
            }

            if (!string.IsNullOrWhiteSpace(
                    place.Terms))
            {
                policies.Add(
                    new CheckoutPolicyItemDto
                    {
                        Type = "terms",
                        Title = "الشروط المهمة",
                        Description =
                            place.Terms.Trim()
                    });
            }

            return policies;
        }

        private string GetWebRootPath()
        {
            if (!string.IsNullOrWhiteSpace(
                _environment.WebRootPath))
            {
                return _environment.WebRootPath;
            }

            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot");
        }

        /* ================================================================
           العروض داخل Checkout
           ----------------------------------------------------------------
           - لا نثق بأي سعر أو خصم قادم من المتصفح.
           - العرض الواحد فقط هو الذي يُحفظ في Booking.PromotionID؛ لذلك
             نختار أفضل عرض تلقائي واحد أو العرض الذي اختاره العميل.
           - Create يعيد تنفيذ هذه الدالة داخل Serializable Transaction.
           ================================================================ */

        private sealed class CheckoutPromotionResolution
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

            // حقول الخصم القديمة تبقى للتوافق مع بقية الحجز والـSnapshot.
            public string DiscountType { get; set; } = string.Empty;
            public decimal DiscountValue { get; set; }
            public decimal? MaximumDiscountAmount { get; set; }

            public string TargetType { get; set; } = string.Empty;
            public string TargetLabel { get; set; } = string.Empty;
            public decimal EligibleAmount { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal BenefitAmount { get; set; }

            public bool IsApplied { get; set; }
            public bool RequiresCode { get; set; }
            public bool IsAutomatic { get; set; }
            public string Message { get; set; } = string.Empty;

            public bool AddsComplimentaryService { get; set; }
        }

        private async Task<CheckoutPromotionResolution>
            ResolveCheckoutPromotionAsync(
                int placeId,
                int customerId,
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
                string? requestedPromotionCode,
                bool allowAutomaticSelection = true)
        {
            var result =
                new CheckoutPromotionResolution();

            var today =
                DateTime.Today;

            var normalizedCode =
                requestedPromotionCode?
                    .Trim() ??
                string.Empty;

            var promotions =
                await _context.Promotions
                    .AsNoTracking()
                    .Where(promotion =>
                        promotion.PlaceId == placeId &&
                        promotion.IsActive == true &&
                        (
                            promotion.Status == "Active" ||
                            promotion.Status == "Scheduled"
                        ) &&
                        promotion.StartDate <= today &&
                        promotion.EndDate >= today)
                    .OrderBy(promotion =>
                        promotion.EndDate)
                    .ThenByDescending(promotion =>
                        promotion.UpdatedAt ??
                        promotion.CreatedAt)
                    .ThenByDescending(promotion =>
                        promotion.PromotionId)
                    .ToListAsync();

            if (requestedPromotionId.HasValue &&
                requestedPromotionId.Value > 0)
            {
                promotions =
                    promotions
                        .Where(promotion =>
                            promotion.PromotionId ==
                                requestedPromotionId.Value)
                        .ToList();

                if (promotions.Count == 0)
                {
                    result.Message =
                        "العرض الذي اخترته لم يعد فعالًا أو لا يتبع هذه المنشأة.";

                    return result;
                }
            }
            else if (!string.IsNullOrWhiteSpace(
                         normalizedCode))
            {
                promotions =
                    promotions
                        .Where(promotion =>
                            !string.IsNullOrWhiteSpace(
                                promotion.PromoCode) &&
                            string.Equals(
                                promotion.PromoCode.Trim(),
                                normalizedCode,
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (promotions.Count == 0)
                {
                    result.Message =
                        "كود العرض غير صحيح أو لم يعد متاحًا لهذه المنشأة.";

                    return result;
                }
            }
            else if (allowAutomaticSelection)
            {
                promotions =
                    promotions
                        .Where(promotion =>
                            promotion.IsAutomatic == true)
                        .ToList();
            }
            else
            {
                promotions.Clear();
            }

            if (promotions.Count == 0)
            {
                return result;
            }

            var promotionIds =
                promotions
                    .Select(promotion =>
                        promotion.PromotionId)
                    .ToArray();

            var usageCounts =
                await _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.PromotionId.HasValue &&
                        promotionIds.Contains(
                            booking.PromotionId.Value) &&
                        booking.BookingState != "Cancelled" &&
                        booking.BookingState != "Rejected" &&
                        booking.BookingState != "Expired")
                    .GroupBy(booking =>
                        booking.PromotionId!.Value)
                    .Select(group =>
                        new
                        {
                            PromotionId = group.Key,
                            Count = group.Count()
                        })
                    .ToDictionaryAsync(
                        item =>
                            item.PromotionId,
                        item =>
                            item.Count);

            var customerUsageCounts =
                customerId <= 0
                    ? new Dictionary<int, int>()
                    : await _context.Bookings
                        .AsNoTracking()
                        .Where(booking =>
                            booking.CustomerId == customerId &&
                            booking.PromotionId.HasValue &&
                            promotionIds.Contains(
                                booking.PromotionId.Value) &&
                            booking.BookingState != "Cancelled" &&
                            booking.BookingState != "Rejected" &&
                            booking.BookingState != "Expired")
                        .GroupBy(booking =>
                            booking.PromotionId!.Value)
                        .Select(group =>
                            new
                            {
                                PromotionId = group.Key,
                                Count = group.Count()
                            })
                        .ToDictionaryAsync(
                            item =>
                                item.PromotionId,
                            item =>
                                item.Count);

            var bookingAmountForMinimum =
                Math.Round(
                    Math.Max(
                        0m,
                        baseAmount +
                        servicesTotal),
                    2);

            var evaluatedPromotions =
                new List<CheckoutPromotionResolution>();

            string firstFailureMessage =
                string.Empty;

            foreach (var promotion in promotions)
            {
                var evaluation =
                    new CheckoutPromotionResolution
                    {
                        PromotionTitle =
                            promotion.PromotionTitle?
                                .Trim() ??
                            "عرض خاص",

                        OfferMode =
                            string.IsNullOrWhiteSpace(promotion.OfferMode)
                                ? "Discount"
                                : promotion.OfferMode.Trim(),

                        RewardType =
                            promotion.RewardType?.Trim() ?? string.Empty,

                        RequiredQuantity = promotion.RequiredQuantity,
                        RewardQuantity = promotion.RewardQuantity,
                        IsRewardRepeatable = promotion.IsRewardRepeatable,
                        RewardServiceId = promotion.RewardServiceId,
                        SpecialPrice = promotion.SpecialPrice,

                        DiscountType =
                            promotion.DiscountType?.Trim() ?? string.Empty,

                        DiscountValue = promotion.DiscountValue,
                        MaximumDiscountAmount = promotion.MaximumDiscountAmount,
                        IsAutomatic = promotion.IsAutomatic != false
                    };

                decimal eligibleAmount;

                if (promotion.RoomId.HasValue)
                {
                    evaluation.TargetType =
                        "Room";

                    evaluation.TargetLabel =
                        "الغرفة المختارة";

                    if (!roomId.HasValue ||
                        roomId.Value !=
                            promotion.RoomId.Value)
                    {
                        evaluation.Message =
                            "العرض مخصص لغرفة أخرى.";

                        firstFailureMessage =
                            firstFailureMessage.Length == 0
                                ? evaluation.Message
                                : firstFailureMessage;

                        continue;
                    }

                    eligibleAmount =
                        baseAmount;
                }
                else if (promotion.PackageId.HasValue)
                {
                    evaluation.TargetType =
                        "Package";

                    evaluation.TargetLabel =
                        "الباقة المختارة";

                    if (!packageId.HasValue ||
                        packageId.Value !=
                            promotion.PackageId.Value)
                    {
                        evaluation.Message =
                            "العرض مخصص لباقة أخرى.";

                        firstFailureMessage =
                            firstFailureMessage.Length == 0
                                ? evaluation.Message
                                : firstFailureMessage;

                        continue;
                    }

                    eligibleAmount =
                        baseAmount;
                }
                else if (promotion.ServiceId.HasValue)
                {
                    evaluation.TargetType =
                        "Service";

                    evaluation.TargetLabel =
                        "الخدمة الإضافية";

                    if (!serviceTotals.TryGetValue(
                            promotion.ServiceId.Value,
                            out eligibleAmount) ||
                        eligibleAmount <= 0)
                    {
                        evaluation.Message =
                            "العرض مخصص لخدمة إضافية غير موجودة في هذا الحجز.";

                        firstFailureMessage =
                            firstFailureMessage.Length == 0
                                ? evaluation.Message
                                : firstFailureMessage;

                        continue;
                    }
                }
                else
                {
                    evaluation.TargetType =
                        "Place";

                    evaluation.TargetLabel =
                        "الحجز في المنشأة";

                    /*
                     * عرض المنشأة يطبق على قيمة الحجز والخدمات المختارة،
                     * وليس على رسوم التنظيف أو الضريبة.
                     */
                    eligibleAmount =
                        baseAmount +
                        servicesTotal;
                }

                eligibleAmount =
                    Math.Round(
                        Math.Max(
                            0m,
                            eligibleAmount),
                        2);

                evaluation.EligibleAmount =
                    eligibleAmount;

                if (eligibleAmount <= 0)
                {
                    evaluation.Message =
                        "لا توجد قيمة مؤهلة لتطبيق هذا العرض.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                if (promotion.MinimumBookingAmount.HasValue &&
                    promotion.MinimumBookingAmount.Value > 0 &&
                    bookingAmountForMinimum <
                        promotion.MinimumBookingAmount.Value)
                {
                    evaluation.Message =
                        $"يحتاج العرض إلى حد أدنى للحجز " +
                        $"{promotion.MinimumBookingAmount.Value:N0}.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                if (promotion.MinimumNights.HasValue &&
                    promotion.MinimumNights.Value > 0 &&
                    bookingDays <
                        promotion.MinimumNights.Value)
                {
                    evaluation.Message =
                        $"يحتاج العرض إلى {promotion.MinimumNights.Value} ليلة على الأقل.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                if (promotion.MinimumPeople.HasValue &&
                    promotion.MinimumPeople.Value > 0 &&
                    totalGuests <
                        promotion.MinimumPeople.Value)
                {
                    evaluation.Message =
                        $"يحتاج العرض إلى {promotion.MinimumPeople.Value} أشخاص على الأقل.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                var lastStayDate =
                    roomId.HasValue &&
                    endDate.HasValue &&
                    endDate.Value.Date >
                        startDate.Date
                        ? endDate.Value.Date
                            .AddDays(-1)
                        : startDate.Date;

                if (promotion.StayStartDate.HasValue &&
                    startDate.Date <
                        promotion.StayStartDate.Value.Date)
                {
                    evaluation.Message =
                        "تاريخ الحجز يسبق فترة الاستفادة من العرض.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                if (promotion.StayEndDate.HasValue &&
                    lastStayDate >
                        promotion.StayEndDate.Value.Date)
                {
                    evaluation.Message =
                        "فترة الحجز تتجاوز آخر يوم مسموح به للعرض.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(
                        promotion.ApplicableDaysCsv))
                {
                    var allowedDays =
                        promotion.ApplicableDaysCsv
                            .Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries)
                            .ToHashSet(
                                StringComparer.OrdinalIgnoreCase);

                    if (allowedDays.Count > 0)
                    {
                        var allDaysAllowed =
                            true;

                        if (roomId.HasValue &&
                            endDate.HasValue &&
                            endDate.Value.Date >
                                startDate.Date)
                        {
                            for (
                                var day = startDate.Date;
                                day < endDate.Value.Date;
                                day = day.AddDays(1))
                            {
                                if (!allowedDays.Contains(
                                        day.DayOfWeek.ToString()))
                                {
                                    allDaysAllowed =
                                        false;

                                    break;
                                }
                            }
                        }
                        else
                        {
                            allDaysAllowed =
                                allowedDays.Contains(
                                    startDate.DayOfWeek.ToString());
                        }

                        if (!allDaysAllowed)
                        {
                            evaluation.Message =
                                "العرض لا يشمل كل أيام الحجز المختارة.";

                            firstFailureMessage =
                                firstFailureMessage.Length == 0
                                    ? evaluation.Message
                                    : firstFailureMessage;

                            continue;
                        }
                    }
                }

                usageCounts.TryGetValue(
                    promotion.PromotionId,
                    out var totalUsage);

                if (promotion.TotalUsageLimit.HasValue &&
                    promotion.TotalUsageLimit.Value > 0 &&
                    totalUsage >=
                        promotion.TotalUsageLimit.Value)
                {
                    evaluation.Message =
                        "اكتمل الحد المتاح لاستخدام هذا العرض.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                customerUsageCounts.TryGetValue(
                    promotion.PromotionId,
                    out var customerUsage);

                if (customerId > 0 &&
                    promotion.PerCustomerUsageLimit.HasValue &&
                    promotion.PerCustomerUsageLimit.Value > 0 &&
                    customerUsage >=
                        promotion.PerCustomerUsageLimit.Value)
                {
                    evaluation.Message =
                        "وصلت إلى الحد المسموح لك لاستخدام هذا العرض.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;

                    continue;
                }

                evaluation.RequiresCode =
                    promotion.IsAutomatic == false;

                if (evaluation.RequiresCode)
                {
                    var storedCode =
                        promotion.PromoCode?
                            .Trim() ??
                        string.Empty;

                    if (string.IsNullOrWhiteSpace(
                            storedCode))
                    {
                        evaluation.Message =
                            "العرض يحتاج كودًا لكن لم يتم إعداد كود صالح له.";

                        firstFailureMessage =
                            firstFailureMessage.Length == 0
                                ? evaluation.Message
                                : firstFailureMessage;

                        continue;
                    }

                    if (!string.Equals(
                            storedCode,
                            normalizedCode,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        evaluation.Message =
                            "أدخل كود العرض الصحيح لتفعيل العرض.";

                        /*
                         * في العرض المطلوب بالرقم نعيد معلوماته للواجهة حتى
                         * تظهر بطاقة إدخال الكود، لكن لا نعتبره مطبقًا.
                         */
                        if (requestedPromotionId.HasValue &&
                            requestedPromotionId.Value ==
                                promotion.PromotionId)
                        {
                            result.PromotionTitle =
                                evaluation.PromotionTitle;

                            result.OfferMode = evaluation.OfferMode;
                            result.RewardType = evaluation.RewardType;
                            result.RequiredQuantity = evaluation.RequiredQuantity;
                            result.RewardQuantity = evaluation.RewardQuantity;
                            result.IsRewardRepeatable = evaluation.IsRewardRepeatable;
                            result.AppliedFreeQuantity = evaluation.AppliedFreeQuantity;
                            result.RepeatCount = evaluation.RepeatCount;
                            result.RewardServiceId = evaluation.RewardServiceId;
                            result.SpecialPrice = evaluation.SpecialPrice;
                            result.OfferSummary = evaluation.OfferSummary;

                            result.DiscountType =
                                evaluation.DiscountType;

                            result.DiscountValue =
                                evaluation.DiscountValue;

                            result.MaximumDiscountAmount =
                                evaluation.MaximumDiscountAmount;

                            result.TargetType =
                                evaluation.TargetType;

                            result.TargetLabel =
                                evaluation.TargetLabel;

                            result.EligibleAmount =
                                evaluation.EligibleAmount;

                            result.RequiresCode =
                                true;

                            result.IsAutomatic =
                                false;

                            result.Message =
                                evaluation.Message;
                        }

                        firstFailureMessage =
                            firstFailureMessage.Length == 0
                                ? evaluation.Message
                                : firstFailureMessage;

                        continue;
                    }

                    evaluation.PromotionCode =
                        storedCode;
                }

                var normalizedOfferMode =
                    string.Equals(
                        evaluation.OfferMode,
                        "ConditionalReward",
                        StringComparison.OrdinalIgnoreCase)
                        ? "ConditionalReward"
                        : "Discount";

                var normalizedRewardType =
                    evaluation.RewardType;

                decimal rawDiscount = 0m;
                decimal benefitAmount = 0m;
                bool canApply = true;

                if (normalizedOfferMode == "Discount")
                {
                    var discountType =
                        string.Equals(
                            promotion.DiscountType,
                            "FixedAmount",
                            StringComparison.OrdinalIgnoreCase)
                            ? "FixedAmount"
                            : "Percentage";

                    evaluation.DiscountType = discountType;
                    evaluation.OfferSummary =
                        discountType == "Percentage"
                            ? $"خصم {promotion.DiscountValue:0.##}%"
                            : $"خصم بمبلغ {promotion.DiscountValue:N0}";

                    rawDiscount =
                        discountType == "Percentage"
                            ? eligibleAmount * promotion.DiscountValue / 100m
                            : promotion.DiscountValue;

                    if (promotion.MaximumDiscountAmount.HasValue &&
                        promotion.MaximumDiscountAmount.Value > 0)
                    {
                        rawDiscount = Math.Min(
                            rawDiscount,
                            promotion.MaximumDiscountAmount.Value);
                    }

                    benefitAmount = rawDiscount;
                }
                else if (string.Equals(
                             normalizedRewardType,
                             "PercentageDiscount",
                             StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "Percentage";
                    evaluation.OfferSummary =
                        $"خصم {promotion.DiscountValue:0.##}% بعد تحقق شروط العرض";

                    rawDiscount =
                        eligibleAmount * promotion.DiscountValue / 100m;

                    if (promotion.MaximumDiscountAmount.HasValue &&
                        promotion.MaximumDiscountAmount.Value > 0)
                    {
                        rawDiscount = Math.Min(
                            rawDiscount,
                            promotion.MaximumDiscountAmount.Value);
                    }

                    benefitAmount = rawDiscount;
                }
                else if (string.Equals(
                             normalizedRewardType,
                             "FixedDiscount",
                             StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "FixedAmount";
                    evaluation.OfferSummary =
                        $"خصم بمبلغ {promotion.DiscountValue:N0} بعد تحقق شروط العرض";

                    rawDiscount = promotion.DiscountValue;

                    if (promotion.MaximumDiscountAmount.HasValue &&
                        promotion.MaximumDiscountAmount.Value > 0)
                    {
                        rawDiscount = Math.Min(
                            rawDiscount,
                            promotion.MaximumDiscountAmount.Value);
                    }

                    benefitAmount = rawDiscount;
                }
                else if (string.Equals(
                             normalizedRewardType,
                             "SpecialPrice",
                             StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "SpecialPrice";

                    if (!promotion.SpecialPrice.HasValue ||
                        promotion.SpecialPrice.Value <= 0)
                    {
                        evaluation.Message =
                            "السعر الخاص في هذا العرض غير صالح.";
                        canApply = false;
                    }
                    else
                    {
                        decimal specialTotal;

                        if (roomId.HasValue &&
                            (promotion.RoomId.HasValue ||
                             evaluation.TargetType == "Place"))
                        {
                            specialTotal =
                                promotion.SpecialPrice.Value *
                                Math.Max(1, bookingDays) *
                                Math.Max(1, bookingQuantity);
                        }
                        else if (packageId.HasValue &&
                                 (promotion.PackageId.HasValue ||
                                  evaluation.TargetType == "Place"))
                        {
                            specialTotal =
                                promotion.SpecialPrice.Value *
                                Math.Max(1, bookingQuantity);
                        }
                        else if (evaluation.TargetType == "Service")
                        {
                            var serviceId = promotion.ServiceId!.Value;
                            var serviceQuantity =
                                serviceQuantities.TryGetValue(serviceId, out var quantity)
                                    ? Math.Max(1, quantity)
                                    : 1;

                            specialTotal =
                                promotion.SpecialPrice.Value * serviceQuantity;
                        }
                        else
                        {
                            specialTotal = promotion.SpecialPrice.Value;
                        }

                        rawDiscount = Math.Max(0m, eligibleAmount - specialTotal);
                        benefitAmount = rawDiscount;
                        evaluation.OfferSummary =
                            $"سعر خاص {promotion.SpecialPrice.Value:N0}";
                    }
                }
                else if (string.Equals(
                             normalizedRewardType,
                             "FreeQuantity",
                             StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "FreeQuantity";

                    /*
                     * FreeQuantity — قاعدة موحدة وآمنة:
                     * - الفندق/الغرفة: MinimumNights هو الشرط الأساسي إن وُجد.
                     * - الخدمة: RequiredQuantity هو عدد وحدات الخدمة المطلوبة.
                     * - الباقة/المنشأة غير الفندقية: RequiredQuantity هو عدد الوحدات.
                     * - عند التكرار: كل اكتمال للشرط يعطي المكافأة من جديد.
                     *   مثال 4 ليالٍ + 1 مجانية، حجز 8 ليالٍ => ليلتان مجانًا.
                     */
                    int totalUnits;
                    decimal amountPerUnit;
                    string unitLabel;

                    bool isNightBasedReward = roomId.HasValue;

                    var requiredQuantity = isNightBasedReward &&
                                           promotion.MinimumNights.HasValue &&
                                           promotion.MinimumNights.Value > 0
                        ? promotion.MinimumNights.Value
                        : promotion.RequiredQuantity ??
                          promotion.MinimumNights ??
                          1;

                    requiredQuantity = Math.Max(1, requiredQuantity);

                    var rewardQuantity =
                        Math.Max(1, promotion.RewardQuantity ?? 1);

                    if (isNightBasedReward)
                    {
                        totalUnits = Math.Max(1, bookingDays);
                        amountPerUnit =
                            baseAmount / Math.Max(1, bookingDays);
                        unitLabel = "ليلة";
                    }
                    else if (evaluation.TargetType == "Service" &&
                             promotion.ServiceId.HasValue)
                    {
                        var serviceId = promotion.ServiceId.Value;
                        totalUnits =
                            serviceQuantities.TryGetValue(serviceId, out var quantity)
                                ? Math.Max(0, quantity)
                                : 0;

                        amountPerUnit =
                            totalUnits > 0 &&
                            serviceTotals.TryGetValue(serviceId, out var serviceTotal)
                                ? serviceTotal / totalUnits
                                : 0m;

                        unitLabel = "وحدة";
                    }
                    else
                    {
                        totalUnits = Math.Max(1, bookingQuantity);
                        amountPerUnit =
                            baseAmount / Math.Max(1, bookingQuantity);
                        unitLabel = "وحدة";
                    }

                    var completedRequirements =
                        requiredQuantity > 0
                            ? totalUnits / requiredQuantity
                            : 0;

                    var repeatCount =
                        promotion.IsRewardRepeatable
                            ? completedRequirements
                            : completedRequirements > 0 ? 1 : 0;

                    var calculatedFreeUnits =
                        repeatCount * rewardQuantity;

                    // لا نسمح للمكافأة أن تتجاوز الوحدات الموجودة فعليًا في الحجز.
                    var freeUnits = Math.Min(
                        Math.Max(0, totalUnits),
                        Math.Max(0, calculatedFreeUnits));

                    if (freeUnits <= 0 || amountPerUnit <= 0)
                    {
                        evaluation.Message =
                            $"للاستفادة من العرض اختر {requiredQuantity} {unitLabel} على الأقل؛ عند تحقق الشرط تحصل على {rewardQuantity} مجانًا.";
                        canApply = false;
                    }
                    else
                    {
                        rawDiscount = Math.Min(
                            eligibleAmount,
                            amountPerUnit * freeUnits);
                        benefitAmount = rawDiscount;
                        evaluation.RequiredQuantity = requiredQuantity;
                        evaluation.RewardQuantity = rewardQuantity;
                        evaluation.IsRewardRepeatable = promotion.IsRewardRepeatable;
                        evaluation.AppliedFreeQuantity = freeUnits;
                        evaluation.RepeatCount = repeatCount;
                        evaluation.OfferSummary =
                            unitLabel == "ليلة"
                                ? promotion.IsRewardRepeatable
                                    ? $"كل {requiredQuantity} ليلة تمنح {rewardQuantity} ليلة مجانًا — تم تطبيقها {repeatCount} مرة"
                                    : $"احجز {requiredQuantity} ليلة واحصل على {rewardQuantity} ليلة مجانًا — مرة واحدة للحجز"
                                : promotion.IsRewardRepeatable
                                    ? $"كل {requiredQuantity} وحدة تمنح {rewardQuantity} مجانًا — تم تطبيقها {repeatCount} مرة"
                                    : $"احجز {requiredQuantity} واحصل على {rewardQuantity} مجانًا — مرة واحدة للحجز";
                    }
                }
                else if (string.Equals(
                             normalizedRewardType,
                             "FreeService",
                             StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "FreeService";

                    var rewardServiceId = promotion.RewardServiceId;
                    if (!rewardServiceId.HasValue || rewardServiceId.Value <= 0)
                    {
                        evaluation.Message =
                            "الخدمة المجانية في هذا العرض غير محددة.";
                        canApply = false;
                    }
                    else
                    {
                        var rewardService =
                            await _context.PlaceAdditionalServices
                                .AsNoTracking()
                                .FirstOrDefaultAsync(service =>
                                    service.ServiceId == rewardServiceId.Value &&
                                    service.PlaceId == placeId &&
                                    service.IsActive == true);

                        if (rewardService == null)
                        {
                            evaluation.Message =
                                "الخدمة المجانية لم تعد متاحة في هذه المنشأة.";
                            canApply = false;
                        }
                        else
                        {
                            var rewardQuantity =
                                Math.Max(1, promotion.RewardQuantity ?? 1);

                            evaluation.RewardServiceId = rewardService.ServiceId;
                            evaluation.RewardServiceName =
                                rewardService.ServiceName?.Trim() ?? "خدمة إضافية";
                            evaluation.RewardQuantity = rewardQuantity;
                            evaluation.AddsComplimentaryService =
                                !serviceTotals.ContainsKey(rewardService.ServiceId);

                            var rewardValue =
                                Math.Max(0m, rewardService.Price) * rewardQuantity;

                            if (serviceTotals.TryGetValue(
                                    rewardService.ServiceId,
                                    out var selectedServiceTotal) &&
                                selectedServiceTotal > 0)
                            {
                                rawDiscount =
                                    Math.Min(selectedServiceTotal, rewardValue);
                            }

                            benefitAmount = rewardValue;
                            evaluation.OfferSummary =
                                rewardQuantity > 1
                                    ? $"{rewardQuantity} × {evaluation.RewardServiceName} مجانًا"
                                    : $"{evaluation.RewardServiceName} مجانًا";
                        }
                    }
                }
                else if (string.Equals(
                             normalizedRewardType,
                             "ServiceSpecialPrice",
                             StringComparison.OrdinalIgnoreCase))
                {
                    evaluation.DiscountType = "ServiceSpecialPrice";

                    var rewardServiceId =
                        promotion.RewardServiceId ?? promotion.ServiceId;

                    if (!rewardServiceId.HasValue ||
                        !promotion.SpecialPrice.HasValue ||
                        promotion.SpecialPrice.Value < 0)
                    {
                        evaluation.Message =
                            "بيانات سعر الخدمة الخاص غير مكتملة.";
                        canApply = false;
                    }
                    else if (!serviceTotals.TryGetValue(
                                 rewardServiceId.Value,
                                 out var selectedServiceTotal) ||
                             selectedServiceTotal <= 0)
                    {
                        evaluation.Message =
                            "أضف الخدمة المشمولة بالعرض أولًا للاستفادة من سعرها الخاص.";
                        canApply = false;
                    }
                    else
                    {
                        var rewardService =
                            await _context.PlaceAdditionalServices
                                .AsNoTracking()
                                .FirstOrDefaultAsync(service =>
                                    service.ServiceId == rewardServiceId.Value &&
                                    service.PlaceId == placeId &&
                                    service.IsActive == true);

                        if (rewardService == null)
                        {
                            evaluation.Message =
                                "الخدمة ذات السعر الخاص لم تعد متاحة.";
                            canApply = false;
                        }
                        else
                        {
                            var selectedQuantity =
                                serviceQuantities.TryGetValue(
                                    rewardServiceId.Value,
                                    out var quantity)
                                    ? Math.Max(1, quantity)
                                    : 1;

                            var specialTotal =
                                promotion.SpecialPrice.Value * selectedQuantity;

                            rawDiscount =
                                Math.Max(0m, selectedServiceTotal - specialTotal);
                            benefitAmount = rawDiscount;
                            evaluation.RewardServiceId = rewardService.ServiceId;
                            evaluation.RewardServiceName =
                                rewardService.ServiceName?.Trim() ?? "خدمة إضافية";
                            evaluation.OfferSummary =
                                $"{evaluation.RewardServiceName} بسعر خاص {promotion.SpecialPrice.Value:N0}";
                        }
                    }
                }
                else
                {
                    evaluation.Message =
                        "نوع المكافأة في هذا العرض غير مدعوم.";
                    canApply = false;
                }

                if (!canApply)
                {
                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;
                    continue;
                }

                /*
                 * حد الخصم النهائي يعتمد على نوع المنفعة:
                 * - الخصومات والسعر الخاص/الكمية المجانية: لا تتجاوز الجزء المستهدف من الحجز.
                 * - خدمة مجانية / خدمة بسعر خاص: المنفعة تخص الخدمة المكافأة نفسها،
                 *   لذلك لا يجوز قصها بسعر الغرفة أو الباقة المستهدفة.
                 *
                 * مثال: باقة 500 + خدمة 1000 مجانًا. إذا كان العرض مستهدفًا للباقة،
                 * كان eligibleAmount = 500. قص خصم الخدمة إلى 500 هو الخطأ الذي كان
                 * يجعل الإجمالي 1000 بدل 500. هنا نستخدم قيمة الخدمة كحد مستقل.
                 */
                decimal discountCap = eligibleAmount;

                if (string.Equals(
                        normalizedRewardType,
                        "FreeService",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        normalizedRewardType,
                        "ServiceSpecialPrice",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var rewardServiceIdForCap =
                        evaluation.RewardServiceId ??
                        promotion.RewardServiceId ??
                        promotion.ServiceId;

                    if (rewardServiceIdForCap.HasValue &&
                        serviceTotals.TryGetValue(
                            rewardServiceIdForCap.Value,
                            out var rewardServiceSelectedTotal) &&
                        rewardServiceSelectedTotal > 0m)
                    {
                        discountCap = rewardServiceSelectedTotal;
                    }
                    else if (string.Equals(
                                 normalizedRewardType,
                                 "FreeService",
                                 StringComparison.OrdinalIgnoreCase) &&
                             evaluation.AddsComplimentaryService)
                    {
                        // الخدمة ستُضاف كسطر مجاني بسعر صفر؛ لا يوجد خصم نقدي من أصل الحجز.
                        discountCap = 0m;
                    }
                }

                rawDiscount =
                    Math.Min(
                        Math.Max(0m, discountCap),
                        Math.Max(0m, rawDiscount));

                evaluation.DiscountAmount =
                    Math.Round(rawDiscount, 2);

                evaluation.BenefitAmount =
                    Math.Round(
                        Math.Max(benefitAmount, evaluation.DiscountAmount),
                        2);

                var isNonMonetaryReward =
                    string.Equals(
                        normalizedRewardType,
                        "FreeService",
                        StringComparison.OrdinalIgnoreCase) &&
                    evaluation.AddsComplimentaryService;

                if (evaluation.DiscountAmount <= 0 &&
                    !isNonMonetaryReward)
                {
                    evaluation.Message =
                        string.Equals(
                            normalizedRewardType,
                            "SpecialPrice",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            normalizedRewardType,
                            "ServiceSpecialPrice",
                            StringComparison.OrdinalIgnoreCase)
                            ? "السعر الخاص لا يقل عن السعر الحالي، لذلك لا يوجد توفير قابل للتطبيق."
                            : "قيمة التوفير المحسوبة لهذا العرض تساوي صفرًا.";

                    firstFailureMessage =
                        firstFailureMessage.Length == 0
                            ? evaluation.Message
                            : firstFailureMessage;
                    continue;
                }

                evaluation.PromotionId = promotion.PromotionId;
                evaluation.IsApplied = true;
                evaluation.Message =
                    string.IsNullOrWhiteSpace(evaluation.OfferSummary)
                        ? "تم تطبيق العرض بعد التحقق من جميع شروطه."
                        : $"تم تطبيق العرض: {evaluation.OfferSummary}.";

                evaluatedPromotions.Add(evaluation);
            }

            var selected =
                evaluatedPromotions
                    .OrderByDescending(evaluation =>
                        evaluation.BenefitAmount)
                    .ThenByDescending(evaluation =>
                        evaluation.DiscountAmount)
                    .ThenByDescending(evaluation =>
                        evaluation.DiscountValue)
                    .FirstOrDefault();

            if (selected != null)
            {
                return selected;
            }

            if (string.IsNullOrWhiteSpace(
                    result.Message) &&
                (
                    requestedPromotionId.HasValue ||
                    !string.IsNullOrWhiteSpace(
                        normalizedCode)
                ))
            {
                result.Message =
                    string.IsNullOrWhiteSpace(
                        firstFailureMessage)
                        ? "العرض لا ينطبق على هذا الحجز حاليًا."
                        : firstFailureMessage;
            }

            return result;
        }



        /* ================================================================
           جاهزية العميل لبدء الحجز
           ================================================================ */
        [HttpGet]
        public async Task<IActionResult> BookingReadiness(
            [FromQuery] string? returnUrl = null)
        {
            if (!TryGetCustomerId(out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    requiresLogin = true,
                    message = "يجب تسجيل الدخول بحساب عميل لإكمال الحجز."
                });
            }

            var customer =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user =>
                        user.UserId == customerId);

            if (customer == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    requiresLogin = true,
                    message = "تعذر العثور على حساب العميل."
                });
            }

            if (!IsState(customer.Role, "Customer"))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message = "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            if (!IsState(customer.AccountState, "Active"))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        accountUnavailable = true,
                        message = "حسابك غير نشط حاليًا، لذلك لا يمكن بدء حجز جديد."
                    });
            }

            var missingFields =
                GetMissingBookingProfileFields(customer);

            var safeReturnUrl =
                string.IsNullOrWhiteSpace(returnUrl)
                    ? Request.Headers.Referer.ToString()
                    : returnUrl;

            var profileUrl =
                BuildProfileCompletionUrl(safeReturnUrl);

            return Json(new
            {
                success = true,
                ready = missingFields.Count == 0,
                requiresProfileCompletion = missingFields.Count > 0,
                missingFields,
                redirectUrl = profileUrl,
                message = missingFields.Count == 0
                    ? "بيانات الحساب جاهزة للحجز."
                    : "يجب إكمال بيانات حسابك قبل بدء الحجز."
            });
        }


        private IActionResult RedirectPackageCheckoutError(
            int placeId,
            int? packageId,
            string message)
        {
            /*
             * Checkout GET هو انتقال متصفح عادي من صفحة تفاصيل المنشأة.
             * إرجاع BadRequest(string) هنا يعرض صفحة بيضاء بالنص فقط.
             * لذلك أخطاء الباقة التي قد تتغير لحظيًا تعود لصفحة المنشأة
             * برسالة قصيرة، وDetails.js يعرضها داخل نافذة أنيقة.
             */
            return RedirectToAction(
                "Details",
                "Places",
                new
                {
                    id = placeId,
                    bookingError = message,
                    bookingPackageId = packageId
                });
        }


        /* ================================================================
           عرض صفحة مراجعة الحجز
           جميع الأسماء والصور والأسعار والسياسات من قاعدة البيانات.
           القيم التي اختارها العميل تصل في Query String، وليس sessionStorage.
           ================================================================ */

        [HttpGet]
        public async Task<IActionResult> Checkout(
      [FromQuery]
    CheckoutSelectionRequestDto request)
        {
            if (request.PlaceId <= 0)
            {
                return BadRequest(
                    "رقم المنشأة غير صحيح.");
            }

            /*
             * يجب اختيار غرفة أو باقة.
             * لم نعد ندعم المسار القديم الذي يرسل PlaceId فقط.
             */
            if (!request.RoomId.HasValue &&
                !request.PackageId.HasValue)
            {
                return BadRequest(
                    "يجب اختيار غرفة أو باقة لإكمال الحجز.");
            }

            if (request.RoomId.HasValue &&
                request.PackageId.HasValue)
            {
                return BadRequest(
                    "لا يمكن اختيار غرفة وباقة في نفس الحجز.");
            }

            if (request.StartDate ==
                default)
            {
                return BadRequest(
                    "تاريخ الحجز غير صحيح.");
            }

            request.Quantity =
                Math.Max(
                    1,
                    request.Quantity);

            request.AdultsCount =
                Math.Max(
                    0,
                    request.AdultsCount);

            request.ChildrenCount =
                Math.Max(
                    0,
                    request.ChildrenCount);

            request.ServiceIds ??=
                new List<int>();

            var startDate =
                request.StartDate.Date;

            DateTime? endDate =
                request.EndDate?.Date;

            if (startDate <
                DateTime.Today)
            {
                return BadRequest(
                    "لا يمكن اختيار تاريخ حجز سابق.");
            }

            if (!TryGetCustomerId(
                    out var customerId))
            {
                return Challenge();
            }

            var customer =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user =>
                        user.UserId ==
                            customerId);

            if (customer == null)
            {
                return Challenge();
            }

            if (!IsState(
                    customer.Role,
                    "Customer"))
            {
                return Forbid();
            }

            if (!IsState(
                    customer.AccountState,
                    "Active"))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "حسابك غير نشط حاليًا، لذلك لا يمكن إنشاء حجز جديد.");
            }

            var missingProfileFields =
                GetMissingBookingProfileFields(
                    customer);

            var profileCompletionUrl =
                BuildProfileCompletionUrl(
                    $"{Request.Path}{Request.QueryString}");

            ViewData["BookingProfileIsComplete"] =
                missingProfileFields.Count == 0;

            ViewData["BookingProfileMissingFields"] =
                missingProfileFields;

            ViewData["BookingProfileUrl"] =
                profileCompletionUrl;

            var place =
                await _context.Places
                    .AsNoTracking()
                    .Include(place =>
                        place.Category)
                    .Include(place =>
                        place.City)
                    .Include(place =>
                        place.PlacePolicy)
                    .Include(place =>
                        place.PlaceImages)
                    .FirstOrDefaultAsync(place =>
                        place.PlaceId ==
                        request.PlaceId);

            if (place == null)
            {
                return NotFound(
                    "المنشأة غير موجودة.");
            }

            if (!IsState(
          place.Status,
          "Active"))
            {
                TempData["ErrorMessage"] =
                    "هذه المنشأة غير مفعلة حاليًا.";

                return RedirectToAction(
                    "Details",
                    "Places",
                    new
                    {
                        id = request.PlaceId
                    });
            }

            var categoryName =
                place.Category?
                    .CategoryName?
                    .Trim() ??
                "منشأة";

            var bookingSystemId =
                place.Category?
                    .BookingSystemId ?? 0;

            var isHallBooking =
                bookingSystemId == 3 ||
                bookingSystemId == 4 ||
                categoryName.Contains(
                    "قاعة",
                    StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains(
                    "قاعات",
                    StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains(
                    "صالة",
                    StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains(
                    "صالات",
                    StringComparison.OrdinalIgnoreCase);

            /*
             * التحقق من أقصى مدة مستقبلية يسمح بها المالك.
             */
            var maximumHorizonMonths =
                place.PlacePolicy?
                    .MaximumHorizonMonths ?? 0;

            if (maximumHorizonMonths > 0)
            {
                var maximumAllowedDate =
                    DateTime.Today.AddMonths(
                        maximumHorizonMonths);

                var requestedLastDate =
                    endDate ??
                    startDate;

                if (requestedLastDate >
                    maximumAllowedDate)
                {
                    return BadRequest(
                        $"لا يمكن الحجز لأكثر من " +
                        $"{maximumHorizonMonths} أشهر مقدمًا.");
                }
            }

            Room? room = null;

            PlacePackage? package = null;

            decimal baseAmount;

            string itemName;

            string itemDescription;

            string itemSecondaryText;

            int? itemCapacity = null;

            decimal? areaSquareMeters = null;

            int maxAdults = 0;

            int maxChildren = 0;

            int? bedsCount = null;

            string bedType =
                string.Empty;

            int floorsCount = 0;

            string roomAssignmentNotice =
                string.Empty;

            int availableQuantity = 1;

            var itemImages =
                new List<string>();

            var now =
                DateTime.Now;

            /* ================================================================
               حجز غرفة أو جناح
               ================================================================ */

            if (request.RoomId.HasValue)
            {
                room =
                    await _context.Rooms
                        .AsNoTracking()
                        .FirstOrDefaultAsync(room =>
                            room.RoomId ==
                                request.RoomId.Value &&

                            room.PlaceId ==
                                request.PlaceId &&

                            room.IsAvailable ==
                                true &&

                            room.RoomUnits.Any(unit =>
                                unit.IsAvailable == true));

                if (room == null)
                {
                    return NotFound(
                        "الغرفة المختارة غير موجودة أو غير متاحة.");
                }

                if (!endDate.HasValue ||
                    endDate.Value <=
                    startDate)
                {
                    return BadRequest(
                        "تاريخ الخروج يجب أن يكون بعد تاريخ الدخول.");
                }

                if (ContainsWeeklyClosedDate(
                        place,
                        startDate,
                        endDate,
                        isRoomBooking: true))
                {
                    return BadRequest(
                        "الفترة المختارة تتضمن يوم إغلاق أسبوعي حسب سياسة المنشأة.");
                }

                var roomNoticeError =
                    ValidateMinimumNotice(
                        place,
                        startDate,
                        package: null,
                        now);

                if (!string.IsNullOrWhiteSpace(
                        roomNoticeError))
                {
                    return BadRequest(
                        roomNoticeError);
                }

                var blockAdjacentRoomBookings =
                    RequiresAdjacentRoomBufferBlock(
                        place);

                /*
                 * فحص الأيام المغلقة من تقويم المالك.
                 * يوم الخروج لا يعد ليلة محجوزة.
                 */
                var hasClosedDate =
                    await _context
                        .PlaceCalendarExceptions
                        .AsNoTracking()
                        .AnyAsync(exception =>
                            exception.PlaceId ==
                                request.PlaceId &&

                            exception.ExceptionDate >=
                                startDate &&

                            exception.ExceptionDate <
                                endDate.Value &&

                            exception.ExceptionType ==
                                "Closed");

                if (hasClosedDate)
                {
                    return BadRequest(
                        "الفترة المختارة تحتوي على يوم مغلق من قبل المنشأة.");
                }

                /*
                 * التوفر الحقيقي يأتي من RoomUnits الفعالة.
                 * نحسب الحجوزات الجديدة من BookingRoomUnits، ثم نضيف
                 * الحجوزات القديمة التي لم تُربط بوحدات فعلية بعد.
                 */
                var activeRoomUnits =
                    await _context.RoomUnits
                        .AsNoTracking()
                        .Where(unit =>
                            unit.PlaceId ==
                                request.PlaceId &&

                            unit.RoomId ==
                                room.RoomId &&

                            unit.IsAvailable == true)
                        .Select(unit =>
                            new
                            {
                                unit.RoomUnitId,
                                unit.FloorNumber
                            })
                        .ToListAsync();

                var activeUnitIds =
                    activeRoomUnits
                        .Select(unit =>
                            unit.RoomUnitId)
                        .ToList();

                var assignedQuantity =
                    activeUnitIds.Count == 0
                        ? 0
                        : await _context.BookingRoomUnits
                            .AsNoTracking()
                            .Where(assignment =>
                                activeUnitIds.Contains(
                                    assignment.RoomUnitId) &&

                                BlockingRoomAssignmentStates.Contains(
                                    assignment.AssignmentStatus) &&

                                BlockingBookingStates.Contains(
                                    assignment.Booking.BookingState ??
                                        string.Empty) &&

                                (
                                    assignment.Booking.BookingState !=
                                        "AwaitingPayment" ||

                                    !assignment.Booking.TimerExpiredAt
                                        .HasValue ||

                                    assignment.Booking.TimerExpiredAt >
                                        now
                                ) &&

                                (
                                    blockAdjacentRoomBookings
                                        ? assignment.Booking.StartDate <=
                                            endDate.Value
                                        : assignment.Booking.StartDate <
                                            endDate.Value
                                ) &&

                                (
                                    blockAdjacentRoomBookings
                                        ? (
                                            assignment.Booking.EndDate ??
                                            assignment.Booking.StartDate
                                                .AddDays(1)
                                          ) >= startDate
                                        : (
                                            assignment.Booking.EndDate ??
                                            assignment.Booking.StartDate
                                                .AddDays(1)
                                          ) > startDate
                                ))
                            .Select(assignment =>
                                assignment.RoomUnitId)
                            .Distinct()
                            .CountAsync();

                var legacyReservedQuantity =
                    await _context.Bookings
                        .AsNoTracking()
                        .Where(booking =>
                            booking.RoomId ==
                                room.RoomId &&

                            !booking.BookingRoomUnits.Any() &&

                            BlockingBookingStates.Contains(
                                booking.BookingState ??
                                    string.Empty) &&

                            (
                                booking.BookingState !=
                                    "AwaitingPayment" ||

                                !booking.TimerExpiredAt.HasValue ||

                                booking.TimerExpiredAt >
                                    now
                            ) &&

                            (
                                blockAdjacentRoomBookings
                                    ? booking.StartDate <=
                                        endDate.Value
                                    : booking.StartDate <
                                        endDate.Value
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
                        .SumAsync(booking =>
                            (int?)booking.Quantity) ??
                    0;

                availableQuantity =
                    Math.Max(
                        0,
                        activeRoomUnits.Count -
                        assignedQuantity -
                        Math.Max(
                            0,
                            legacyReservedQuantity));

                if (availableQuantity <= 0)
                {
                    return BadRequest(
                        "هذه الغرفة لم تعد متاحة في الفترة المختارة.");
                }

                if (request.Quantity >
                    availableQuantity)
                {
                    return BadRequest(
                        $"المتاح حاليًا من هذه الغرفة هو " +
                        $"{availableQuantity} فقط.");
                }

                var bookingDays =
                    Math.Max(
                        1,
                        (endDate.Value -
                         startDate).Days);

                baseAmount =
                    room.Price *
                    bookingDays *
                    request.Quantity;

                itemName =
                    room.RoomType?.Trim() ??
                    "غرفة";

                itemDescription =
                    room.Description?.Trim() ??
                    string.Empty;

                itemSecondaryText =
                    "سيُحدد رقم الغرفة والطابق تلقائيًا بعد إنشاء الحجز.";

                itemCapacity =
                    room.MaxAdults +
                        room.MaxChildren > 0
                        ? room.MaxAdults +
                          room.MaxChildren
                        : ExtractRoomCapacity(
                            room.Description);

                areaSquareMeters =
                    room.AreaSquareMeters;

                maxAdults =
                    Math.Max(
                        0,
                        room.MaxAdults);

                maxChildren =
                    Math.Max(
                        0,
                        room.MaxChildren);

                bedsCount =
                    room.BedsCount;

                bedType =
                    room.BedType?.Trim() ??
                    string.Empty;

                floorsCount =
                    activeRoomUnits
                        .Select(unit =>
                            unit.FloorNumber)
                        .Distinct()
                        .Count();

                roomAssignmentNotice =
                    $"سيختار النظام {request.Quantity} " +
                    (request.Quantity == 1
                        ? "غرفة فعلية متاحة"
                        : "غرف فعلية متاحة") +
                    " من هذا النوع، ويظهر الرقم والطابق بعد إنشاء الحجز.";

                var effectiveRoomCapacity =
                    itemCapacity.HasValue
                        ? itemCapacity.Value *
                          request.Quantity
                        : 0;

                var roomGuestValidation =
                    ValidateRoomGuestCapacity(
                        request.AdultsCount,
                        request.ChildrenCount,
                        effectiveRoomCapacity,
                        request.Quantity > 1
                            ? "الغرف المختارة"
                            : "الغرفة");

                if (!roomGuestValidation.Valid)
                {
                    return BadRequest(
                        roomGuestValidation.Message);
                }

                itemImages =
                    ReadImagePaths(
                        room.ImageUrl);
            }

            /* ================================================================
               حجز باقة أو قاعة أو شاليه
               ================================================================ */

            else
            {
                package =
                    await _context
                        .PlacePackages
                        .AsNoTracking()
                        .FirstOrDefaultAsync(package =>
                            package.PackageId ==
                                request.PackageId!.Value &&

                            package.PlaceId ==
                                request.PlaceId &&

                            package.IsActive ==
                                true);

                if (package == null)
                {
                    return RedirectPackageCheckoutError(
                        request.PlaceId,
                        request.PackageId,
                        "الباقة المختارة لم تعد متاحة. اختر باقة أخرى.");
                }

                if (ContainsWeeklyClosedDate(
                        place,
                        startDate,
                        endDate,
                        isRoomBooking: false))
                {
                    return RedirectPackageCheckoutError(
                        request.PlaceId,
                        request.PackageId,
                        "هذا اليوم مغلق أسبوعيًا حسب سياسة المنشأة.");
                }

                if (!PackageFitsConfiguredShift(
                        place,
                        package))
                {
                    return RedirectPackageCheckoutError(
                        request.PlaceId,
                        request.PackageId,
                        "وقت هذه الباقة لا يقع ضمن إحدى الفترات التشغيلية المفعلة في سياسة المنشأة.");
                }

                var packageNoticeError =
                    ValidateMinimumNotice(
                        place,
                        startDate,
                        package,
                        now);

                if (!string.IsNullOrWhiteSpace(
                        packageNoticeError))
                {
                    return RedirectPackageCheckoutError(
                        request.PlaceId,
                        request.PackageId,
                        packageNoticeError);
                }

                /*
                 * الباقة تمثل حجزًا واحدًا للمكان.
                 */
                request.Quantity = 1;

                endDate =
                    startDate;

                var hasClosedDate =
                    await _context
                        .PlaceCalendarExceptions
                        .AsNoTracking()
                        .AnyAsync(exception =>
                            exception.PlaceId ==
                                request.PlaceId &&

                            exception.ExceptionDate ==
                                startDate &&

                            exception.ExceptionType ==
                                "Closed");

                if (hasClosedDate)
                {
                    return RedirectPackageCheckoutError(
                        request.PlaceId,
                        request.PackageId,
                        "هذا اليوم مغلق من قبل المنشأة.");
                }

                /*
                 * في نظام اليوم الكامل يبقى اليوم محجوزًا بالكامل.
                 * في نظام الفترات نسمح بأكثر من باقة في اليوم نفسه إذا
                 * لم تتداخل أوقاتها بعد احتساب وقت التجهيز.
                 */
                var placeAlreadyBooked =
                    await HasPackageBookingConflictAsync(
                        place,
                        package,
                        startDate,
                        now);

                if (placeAlreadyBooked)
                {
                    return RedirectPackageCheckoutError(
                        request.PlaceId,
                        request.PackageId,
                        "هذا الموعد لم يعد متاحًا. اختر يومًا آخر متاحًا.");
                }

                /*
                 * القاعات لا تستخدم عدادات بالغين/أطفال أصلًا.
                 * نحدد ذلك من نظام الحجز/الفئة نفسها، وليس من قيمة
                 * SeatingSetup القادمة من الواجهة.
                 */
                if (!isHallBooking)
                {
                    var packageGuestValidation =
                        ValidatePackageGuestCapacity(
                            request.AdultsCount,
                            request.ChildrenCount,
                            Math.Max(
                                0,
                                package.Capacity),
                            "الباقة");

                    if (!packageGuestValidation.Valid)
                    {
                        return RedirectPackageCheckoutError(
                            request.PlaceId,
                            request.PackageId,
                            packageGuestValidation.Message);
                    }
                }

                baseAmount =
                    package.Price;

                itemName =
                    package.PackageName?.Trim() ??
                    "باقة حجز";

                itemDescription =
                    package.TimeFrame?.Trim() ??
                    string.Empty;

                itemSecondaryText =
                    package.TimeFrame?.Trim() ??
                    "باقة حجز";

                itemCapacity =
                    package.Capacity;

                availableQuantity = 1;
            }

            /* ================================================================
               الخدمات الإضافية
               ================================================================ */

            var requestedServiceIds =
                request.ServiceIds
                    .Where(serviceId =>
                        serviceId > 0)
                    .Distinct()
                    .ToList();

            var services =
                requestedServiceIds.Count == 0
                    ? new List<
                        PlaceAdditionalService>()
                    : await _context
                        .PlaceAdditionalServices
                        .AsNoTracking()
                        .Where(service =>
                            requestedServiceIds.Contains(
                                service.ServiceId) &&

                            service.PlaceId ==
                                request.PlaceId &&

                            service.IsActive ==
                                true)
                        .OrderBy(service =>
                            service.ServiceName)
                        .ToListAsync();

            if (services.Count !=
                requestedServiceIds.Count)
            {
                return BadRequest(
                    "إحدى الخدمات الإضافية غير موجودة أو غير متاحة.");
            }

            var serviceModels =
                services
                    .Select(service =>
                        new CheckoutAdditionalServiceDto
                        {
                            ServiceId =
                                service.ServiceId,

                            ServiceName =
                                service.ServiceName?
                                    .Trim() ??
                                "خدمة إضافية",

                            Description =
                                service.Description?
                                    .Trim() ??
                                string.Empty,

                            ImageUrl =
                                FirstImagePath(
                                    service.ImageUrl),

                            Quantity = 1,

                            UnitPrice =
                                service.Price
                        })
                    .ToList();

            var servicesTotal =
                serviceModels.Sum(service =>
                    service.TotalPrice);

            /* ================================================================
               الأسعار + العرض + الضريبة
               ---------------------------------------------------------------
               الخصم يطبّق على الجزء المؤهل فقط، ثم يعاد حساب الضريبة على
               المجموع الجديد. رسوم التنظيف لا تدخل ضمن قيمة الخصم.
               ================================================================ */

            var cleaningFee =
                Math.Max(
                    0m,
                    place.PlacePolicy?
                        .CleaningFee ?? 0m);

            var taxPercentage =
                Math.Max(
                    0m,
                    Math.Min(
                        place.PlacePolicy?
                            .TaxPercentage ?? 0m,
                        100m));

            baseAmount =
                Math.Round(
                    baseAmount,
                    2);

            servicesTotal =
                Math.Round(
                    servicesTotal,
                    2);

            cleaningFee =
                Math.Round(
                    cleaningFee,
                    2);

            var serviceTotalsById =
                serviceModels
                    .GroupBy(service =>
                        service.ServiceId)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            Math.Round(
                                group.Sum(service =>
                                    service.TotalPrice),
                                2));

            var bookingDaysForPromotion =
                room != null &&
                endDate.HasValue
                    ? Math.Max(
                        1,
                        (endDate.Value.Date -
                         startDate.Date).Days)
                    : 1;

            var promotionResolution =
                await ResolveCheckoutPromotionAsync(
                    place.PlaceId,
                    customerId,
                    room?.RoomId,
                    package?.PackageId,
                    startDate,
                    endDate,
                    bookingDaysForPromotion,
                    request.Quantity,
                    request.AdultsCount +
                    request.ChildrenCount,
                    baseAmount,
                    servicesTotal,
                    serviceTotalsById,
                    serviceModels.ToDictionary(service => service.ServiceId, service => Math.Max(1, service.Quantity)),
                    request.PromotionId,
                    request.PromotionCode,
                    allowAutomaticSelection: true);

            if (promotionResolution.IsApplied &&
                promotionResolution.AddsComplimentaryService &&
                promotionResolution.RewardServiceId.HasValue &&
                serviceModels.All(service =>
                    service.ServiceId != promotionResolution.RewardServiceId.Value))
            {
                serviceModels.Add(
                    new CheckoutAdditionalServiceDto
                    {
                        ServiceId = promotionResolution.RewardServiceId.Value,
                        ServiceName = string.IsNullOrWhiteSpace(promotionResolution.RewardServiceName)
                            ? "خدمة مجانية"
                            : promotionResolution.RewardServiceName,
                        Description = "مكافأة مجانية ضمن العرض المطبق",
                        Quantity = Math.Max(1, promotionResolution.RewardQuantity ?? 1),
                        UnitPrice = 0m,
                        UnitPriceDisplay = 0m,
                        UnitPriceText = string.Empty,
                        TotalPriceDisplay = 0m,
                        TotalPriceText = string.Empty
                    });
            }

            var originalBeforeTax =
                Math.Round(
                    baseAmount +
                    servicesTotal +
                    cleaningFee,
                    2);

            var originalTaxAmount =
                Math.Round(
                    originalBeforeTax *
                    taxPercentage /
                    100m,
                    2);

            var originalTotalPrice =
                Math.Round(
                    originalBeforeTax +
                    originalTaxAmount,
                    2);

            var discountAmount =
                promotionResolution.IsApplied
                    ? Math.Round(
                        Math.Min(
                            promotionResolution.DiscountAmount,
                            Math.Max(
                                0m,
                                baseAmount +
                                servicesTotal)),
                        2)
                    : 0m;

            var discountedBeforeTax =
                Math.Round(
                    Math.Max(
                        0m,
                        originalBeforeTax -
                        discountAmount),
                    2);

            var taxAmount =
                Math.Round(
                    discountedBeforeTax *
                    taxPercentage /
                    100m,
                    2);

            var finalTotal =
                Math.Round(
                    discountedBeforeTax +
                    taxAmount,
                    2);

            if (finalTotal <= 0)
            {
                return BadRequest(
                    "تعذر حساب السعر النهائي للحجز.");
            }

            var depositAmount =
                CalculateDepositAmount(
                    place,
                    finalTotal);


            var paymentTimeout =
                place.PlacePolicy?
                    .PaymentTimeout ?? 12;

            if (paymentTimeout <= 0)
            {
                paymentTimeout = 12;
            }

            /*
             * رحلة الحجز أصبحت موحدة:
             * - مع عربون: ينتقل العميل للدفع، ثم يراجع المالك الحجز.
             * - بدون عربون: ينتقل الحجز مباشرة إلى مراجعة المالك.
             *
             * القيمة التالية للتوافق مع Checkout.js الحالي فقط،
             * وليست سياسة يختارها المالك.
             */
            var approvalMode =
                depositAmount > 0
                    ? "Instant"
                    : "Manual";

            /* ================================================================
               صور المنشأة
               ================================================================ */

            var placeImages =
                new List<string>();

            placeImages.AddRange(
                ReadImagePaths(
                    place.CoverImage));

            placeImages.AddRange(
                ReadImagePaths(
                    place.LogoImage));

            placeImages.AddRange(
                place.PlaceImages
                    .Select(image =>
                        NormalizeImagePath(
                            image.ImagePath))
                    .Where(path =>
                        !string.IsNullOrWhiteSpace(
                            path)));

            placeImages.AddRange(
                ReadImagePaths(
                    place.Category?
                        .ImagePath));

            placeImages =
                placeImages
                    .Where(path =>
                        !string.IsNullOrWhiteSpace(
                            path))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (placeImages.Count == 0)
            {
                placeImages.Add(
                    "/images/bg1.jpg");
            }

            var placeImageUrl =
                placeImages[0];

            if (itemImages.Count == 0)
            {
                itemImages.AddRange(
                    placeImages);
            }

            itemImages =
                itemImages
                    .Where(path =>
                        !string.IsNullOrWhiteSpace(
                            path))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var itemImageUrl =
                itemImages.FirstOrDefault() ??
                placeImageUrl;

            /* ================================================================
               العملة
               ================================================================ */

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            string? exchangeRateRegion =
                place.City?
                    .ExchangeRateRegion;

            var currencyLabel =
                currencyContext.Currency;

            foreach (var serviceModel in serviceModels)
            {
                serviceModel.UnitPriceDisplay =
                    currencyContext.Convert(
                        serviceModel.UnitPrice,
                        exchangeRateRegion);

                serviceModel.UnitPriceText =
                    currencyContext.Format(
                        serviceModel.UnitPrice,
                        exchangeRateRegion);

                serviceModel.TotalPriceDisplay =
                    currencyContext.Convert(
                        serviceModel.TotalPrice,
                        exchangeRateRegion);

                serviceModel.TotalPriceText =
                    currencyContext.Format(
                        serviceModel.TotalPrice,
                        exchangeRateRegion);
            }

            string promotionOfferSummaryForDisplay =
                promotionResolution.OfferSummary;

            if (promotionResolution.IsApplied)
            {
                var offerMode = promotionResolution.OfferMode ?? "Discount";
                var rewardType = promotionResolution.RewardType ?? string.Empty;

                if (!string.Equals(offerMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase))
                {
                    promotionOfferSummaryForDisplay =
                        string.Equals(promotionResolution.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                            ? $"خصم {promotionResolution.DiscountValue:0.##}%"
                            : $"خصم {currencyContext.Format(promotionResolution.DiscountValue, exchangeRateRegion)}";
                }
                else if (string.Equals(rewardType, "PercentageDiscount", StringComparison.OrdinalIgnoreCase))
                {
                    promotionOfferSummaryForDisplay =
                        $"خصم {promotionResolution.DiscountValue:0.##}% بعد تحقق الشروط";
                }
                else if (string.Equals(rewardType, "FixedDiscount", StringComparison.OrdinalIgnoreCase))
                {
                    promotionOfferSummaryForDisplay =
                        $"خصم {currencyContext.Format(promotionResolution.DiscountValue, exchangeRateRegion)} بعد تحقق الشروط";
                }
                else if (string.Equals(rewardType, "SpecialPrice", StringComparison.OrdinalIgnoreCase) &&
                         promotionResolution.SpecialPrice.HasValue)
                {
                    promotionOfferSummaryForDisplay =
                        $"سعر خاص {currencyContext.Format(promotionResolution.SpecialPrice.Value, exchangeRateRegion)}";
                }
                else if (string.Equals(rewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase) &&
                         promotionResolution.SpecialPrice.HasValue)
                {
                    promotionOfferSummaryForDisplay =
                        !string.IsNullOrWhiteSpace(promotionResolution.RewardServiceName)
                            ? $"{promotionResolution.RewardServiceName} بسعر خاص {currencyContext.Format(promotionResolution.SpecialPrice.Value, exchangeRateRegion)}"
                            : $"خدمة بسعر خاص {currencyContext.Format(promotionResolution.SpecialPrice.Value, exchangeRateRegion)}";
                }
                else if (string.Equals(rewardType, "FreeService", StringComparison.OrdinalIgnoreCase))
                {
                    var rewardQuantity = Math.Max(1, promotionResolution.RewardQuantity ?? 1);
                    promotionOfferSummaryForDisplay =
                        !string.IsNullOrWhiteSpace(promotionResolution.RewardServiceName)
                            ? (rewardQuantity > 1
                                ? $"{rewardQuantity} × {promotionResolution.RewardServiceName} مجانًا"
                                : $"{promotionResolution.RewardServiceName} مجانًا")
                            : "خدمة إضافية مجانية";
                }
            }

            /* ================================================================
               نموذج الصفحة
               ================================================================ */

            var model =
                new CheckoutViewModel
                {
                    PlaceId =
                        place.PlaceId,

                    PlaceName =
                        place.PlaceNameAndDesc,

                    PlaceDescription =
                        place.Description?.Trim() ??
                        string.Empty,

                    CategoryName =
                        categoryName,

                    CityName =
                        place.City?
                            .CityName?.Trim() ??
                        string.Empty,

                    BookingSystemId =
                        bookingSystemId,

                    PlaceImageUrl =
                        placeImageUrl,

                    AddressDetail =
                        place.AddressDetail?.Trim() ??
                        string.Empty,

                    PhoneNumber =
                        place.PhoneNumber?.Trim() ??
                        string.Empty,

                    AllowsCashOnArrival =
                        place.AllowCashOnArrival ==
                            true ||
                        place.IsCashOnArrival ==
                            true,

                    RoomId =
                        room?.RoomId,

                    PackageId =
                        package?.PackageId,

                    IsRoomBooking =
                        room != null,

                    IsPackageBooking =
                        package != null,

                    IsHallBooking =
                        isHallBooking,

                    ItemName =
                        itemName,

                    ItemDescription =
                        itemDescription,

                    ItemImageUrl =
                        itemImageUrl,

                    ItemImages =
                        itemImages,

                    ItemSecondaryText =
                        itemSecondaryText,

                    ItemUnitPrice =
                        room?.Price ??
                        package?.Price ??
                        0m,

                    ItemUnitPriceDisplay =
                        currencyContext.Convert(
                            room?.Price ??
                            package?.Price ??
                            0m,
                            exchangeRateRegion),

                    ItemUnitPriceText =
                        currencyContext.Format(
                            room?.Price ??
                            package?.Price ??
                            0m,
                            exchangeRateRegion),

                    ItemCapacity =
                        itemCapacity,

                    AreaSquareMeters =
                        areaSquareMeters,

                    MaxAdults =
                        maxAdults,

                    MaxChildren =
                        maxChildren,

                    BedsCount =
                        bedsCount,

                    BedType =
                        bedType,

                    FloorsCount =
                        floorsCount,

                    RoomAssignmentNotice =
                        roomAssignmentNotice,

                    AvailableQuantity =
                        availableQuantity,

                    StartDate =
                        startDate,

                    EndDate =
                        endDate,

                    Quantity =
                        request.Quantity,

                    AdultsCount =
                        request.AdultsCount,

                    ChildrenCount =
                        request.ChildrenCount,

                    BookingType =
                        request.BookingType?
                            .Trim() ??
                        string.Empty,

                    SeatingSetup =
                        request.SeatingSetup?
                            .Trim() ??
                        string.Empty,

                    BookingApprovalMode =
                        approvalMode,

                    PaymentTimeoutHours =
                        paymentTimeout,

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

                    RequiresDeposit =
                        depositAmount > 0,

                    DepositAmount =
                        depositAmount,

                    DepositAmountDisplay =
                        currencyContext.Convert(
                            depositAmount,
                            exchangeRateRegion),

                    DepositAmountText =
                        currencyContext.Format(
                            depositAmount,
                            exchangeRateRegion),

                    DepositCurrency =
                        currencyContext.Currency,

                    Policies =
                        BuildCheckoutPolicies(
                            place,
                            depositAmount,
                            currencyContext.Format(
                                depositAmount,
                                exchangeRateRegion)),

                    AdditionalServices =
                        serviceModels,

                    RequestedPromotionId =
                        request.PromotionId,

                    PromotionId =
                        promotionResolution.IsApplied
                            ? promotionResolution.PromotionId
                            : null,

                    PromotionTitle =
                        promotionResolution.PromotionTitle,

                    PromotionCode =
                        promotionResolution.IsApplied
                            ? promotionResolution.PromotionCode
                            : request.PromotionCode?.Trim() ?? string.Empty,

                    PromotionOfferMode = promotionResolution.OfferMode,
                    PromotionRewardType = promotionResolution.RewardType,
                    PromotionRequiredQuantity = promotionResolution.RequiredQuantity,
                    PromotionRewardQuantity = promotionResolution.RewardQuantity,
                    PromotionIsRewardRepeatable = promotionResolution.IsRewardRepeatable,
                    AppliedFreeQuantity = promotionResolution.AppliedFreeQuantity,
                    PromotionRepeatCount = promotionResolution.RepeatCount,
                    PromotionRewardServiceId = promotionResolution.RewardServiceId,
                    PromotionRewardServiceName = promotionResolution.RewardServiceName,
                    PromotionSpecialPrice = promotionResolution.SpecialPrice,
                    PromotionSpecialPriceDisplay = promotionResolution.SpecialPrice.HasValue
                        ? currencyContext.Convert(promotionResolution.SpecialPrice.Value, exchangeRateRegion)
                        : null,
                    PromotionSpecialPriceText = promotionResolution.SpecialPrice.HasValue
                        ? currencyContext.Format(promotionResolution.SpecialPrice.Value, exchangeRateRegion)
                        : string.Empty,
                    PromotionOfferSummary = promotionOfferSummaryForDisplay,

                    PromotionDiscountType =
                        promotionResolution.DiscountType,

                    PromotionDiscountValue =
                        promotionResolution.DiscountValue,

                    PromotionDiscountValueDisplay =
                        string.Equals(
                            promotionResolution.DiscountType,
                            "Percentage",
                            StringComparison.OrdinalIgnoreCase)
                            ? 0m
                            : currencyContext.Convert(
                                promotionResolution.DiscountValue,
                                exchangeRateRegion),

                    PromotionDiscountValueText =
                        string.Equals(
                            promotionResolution.DiscountType,
                            "Percentage",
                            StringComparison.OrdinalIgnoreCase)
                            ? string.Empty
                            : currencyContext.Format(
                                promotionResolution.DiscountValue,
                                exchangeRateRegion),

                    PromotionMaximumDiscountAmount =
                        promotionResolution.MaximumDiscountAmount,

                    PromotionMaximumDiscountAmountDisplay =
                        promotionResolution.MaximumDiscountAmount.HasValue
                            ? currencyContext.Convert(
                                promotionResolution.MaximumDiscountAmount.Value,
                                exchangeRateRegion)
                            : null,

                    PromotionMaximumDiscountAmountText =
                        promotionResolution.MaximumDiscountAmount.HasValue
                            ? currencyContext.Format(
                                promotionResolution.MaximumDiscountAmount.Value,
                                exchangeRateRegion)
                            : string.Empty,

                    PromotionTargetType =
                        promotionResolution.TargetType,

                    PromotionTargetLabel =
                        promotionResolution.TargetLabel,

                    PromotionEligibleAmount =
                        promotionResolution.EligibleAmount,

                    PromotionEligibleAmountDisplay =
                        currencyContext.Convert(
                            promotionResolution.EligibleAmount,
                            exchangeRateRegion),

                    PromotionEligibleAmountText =
                        currencyContext.Format(
                            promotionResolution.EligibleAmount,
                            exchangeRateRegion),

                    DiscountAmount =
                        discountAmount,

                    DiscountAmountDisplay =
                        currencyContext.Convert(
                            discountAmount,
                            exchangeRateRegion),

                    DiscountAmountText =
                        currencyContext.Format(
                            discountAmount,
                            exchangeRateRegion),

                    PromotionRequiresCode =
                        promotionResolution.RequiresCode,

                    PromotionIsAutomatic =
                        promotionResolution.IsAutomatic,

                    PromotionMessage =
                        promotionResolution.Message,

                    BaseAmount =
                        baseAmount,

                    BaseAmountDisplay =
                        currencyContext.Convert(
                            baseAmount,
                            exchangeRateRegion),

                    BaseAmountText =
                        currencyContext.Format(
                            baseAmount,
                            exchangeRateRegion),

                    ServicesTotal =
                        servicesTotal,

                    ServicesTotalDisplay =
                        currencyContext.Convert(
                            servicesTotal,
                            exchangeRateRegion),

                    ServicesTotalText =
                        currencyContext.Format(
                            servicesTotal,
                            exchangeRateRegion),

                    CleaningFee =
                        cleaningFee,

                    CleaningFeeDisplay =
                        currencyContext.Convert(
                            cleaningFee,
                            exchangeRateRegion),

                    CleaningFeeText =
                        currencyContext.Format(
                            cleaningFee,
                            exchangeRateRegion),

                    TaxPercentage =
                        taxPercentage,

                    TaxAmount =
                        taxAmount,

                    TaxAmountDisplay =
                        currencyContext.Convert(
                            taxAmount,
                            exchangeRateRegion),

                    TaxAmountText =
                        currencyContext.Format(
                            taxAmount,
                            exchangeRateRegion),

                    OriginalTaxAmount =
                        originalTaxAmount,

                    OriginalTaxAmountDisplay =
                        currencyContext.Convert(
                            originalTaxAmount,
                            exchangeRateRegion),

                    OriginalTaxAmountText =
                        currencyContext.Format(
                            originalTaxAmount,
                            exchangeRateRegion),

                    OriginalTotalPrice =
                        originalTotalPrice,

                    OriginalTotalPriceDisplay =
                        currencyContext.Convert(
                            originalTotalPrice,
                            exchangeRateRegion),

                    OriginalTotalPriceText =
                        currencyContext.Format(
                            originalTotalPrice,
                            exchangeRateRegion),

                    DiscountedBeforeTaxAmount =
                        discountedBeforeTax,

                    DiscountedBeforeTaxAmountDisplay =
                        currencyContext.Convert(
                            discountedBeforeTax,
                            exchangeRateRegion),

                    DiscountedBeforeTaxAmountText =
                        currencyContext.Format(
                            discountedBeforeTax,
                            exchangeRateRegion),

                    FinalTotal =
                        finalTotal,

                    FinalTotalDisplay =
                        currencyContext.Convert(
                            finalTotal,
                            exchangeRateRegion),

                    FinalTotalText =
                        currencyContext.Format(
                            finalTotal,
                            exchangeRateRegion),

                    BeforeTaxAmountDisplay =
                        currencyContext.Convert(
                            originalBeforeTax,
                            exchangeRateRegion),

                    BeforeTaxAmountText =
                        currencyContext.Format(
                            originalBeforeTax,
                            exchangeRateRegion),

                    RemainingAfterDepositDisplay =
                        currencyContext.Convert(
                            Math.Max(0m, finalTotal - depositAmount),
                            exchangeRateRegion),

                    RemainingAfterDepositText =
                        currencyContext.Format(
                            Math.Max(0m, finalTotal - depositAmount),
                            exchangeRateRegion),

                    TotalSavingsDisplay =
                        currencyContext.Convert(
                            Math.Max(0m, originalTotalPrice - finalTotal),
                            exchangeRateRegion),

                    TotalSavingsText =
                        currencyContext.Format(
                            Math.Max(0m, originalTotalPrice - finalTotal),
                            exchangeRateRegion),

                    CurrencyLabel =
                        currencyLabel,

                    CurrencyCode =
                        currencyContext.Code,

                    CurrencySymbol =
                        currencyContext.Symbol
                };

            return View(model);
        }

        /* ================================================================
           إنشاء الحجز
           ================================================================ */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromBody]
            CreateBookingRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "بيانات الحجز غير مكتملة أو غير صحيحة."
                });
            }

            if (!TryGetCustomerId(
                out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    requiresLogin = true,
                    loginUrl =
                        "/Account/Login",
                    message =
                        "يجب تسجيل الدخول بحساب عميل لإكمال الحجز."
                });
            }

            var customer =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user =>
                        user.UserId ==
                            customerId);

            if (customer == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message =
                        "حساب العميل غير موجود."
                });
            }

            if (!IsState(
                    customer.Role,
                    "Customer"))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        message =
                            "هذه العملية متاحة لحساب العميل فقط."
                    });
            }

            if (!IsState(
                    customer.AccountState,
                    "Active"))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        accountUnavailable = true,
                        message =
                            "حسابك غير نشط حاليًا، لذلك لا يمكن إنشاء حجز جديد."
                    });
            }

            var missingProfileFields =
                GetMissingBookingProfileFields(
                    customer);

            if (missingProfileFields.Count > 0)
            {
                return BadRequest(new
                {
                    success = false,
                    requiresProfileCompletion = true,
                    message =
                        "يجب إكمال بيانات حسابك المهمة قبل إنشاء الحجز.",
                    missingFields =
                        missingProfileFields,
                    redirectUrl =
                        BuildProfileCompletionUrl(
                            Request.Headers.Referer
                                .ToString())
                });
            }

            if (string.Equals(
                    request.BookingType?.Trim(),
                    "other",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    request.SeatingSetup?.Trim(),
                    "other",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "عند اختيار «أخرى» يجب كتابة الوصف المطلوب قبل المتابعة."
                });
            }

            if (request.PlaceId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "رقم المنشأة غير صحيح."
                });
            }

            if (request.StartDate ==
                default)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "تاريخ الحجز غير صحيح."
                });
            }

            if (request.Quantity < 1)
            {
                request.Quantity = 1;
            }

            if (request.AdultsCount < 0 ||
                request.ChildrenCount < 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "عدد الأشخاص غير صحيح."
                });
            }

            if (request.RoomId.HasValue &&
                request.PackageId.HasValue)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "لا يمكن اختيار غرفة وباقة في نفس الحجز."
                });
            }

            if (!request.RoomId.HasValue &&
                !request.PackageId.HasValue)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "يجب اختيار غرفة أو باقة لإكمال الحجز."
                });
            }

            request.AdditionalServices ??=
                new List<
                    BookingAdditionalServiceRequestDto>();

            var place =
     await _context.Places
         .Include(p =>
             p.PlacePolicy)
         .Include(p =>
             p.Category)
         .Include(p =>
             p.City)
         .FirstOrDefaultAsync(p =>
             p.PlaceId ==
                 request.PlaceId);

            if (place == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "المنشأة غير موجودة."
                });
            }

            var createCategoryName =
                place.Category?
                    .CategoryName?
                    .Trim() ??
                string.Empty;

            var createBookingSystemId =
                place.Category?
                    .BookingSystemId ?? 0;

            var isHallCreateBooking =
                createBookingSystemId == 3 ||
                createBookingSystemId == 4 ||
                createCategoryName.Contains(
                    "قاعة",
                    StringComparison.OrdinalIgnoreCase) ||
                createCategoryName.Contains(
                    "قاعات",
                    StringComparison.OrdinalIgnoreCase) ||
                createCategoryName.Contains(
                    "صالة",
                    StringComparison.OrdinalIgnoreCase) ||
                createCategoryName.Contains(
                    "صالات",
                    StringComparison.OrdinalIgnoreCase);

            if (!IsState(
           place.Status,
           "Active"))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "المنشأة غير مفعلة حاليًا."
                });
            }

            /*
             * BookingApprovalMode لم يعد خيارًا تشغيليًا.
             * تتحدد الرحلة لاحقًا من وجود العربون فقط.
             */
            string approvalMode =
                "Manual";

            var startDate =
                request.StartDate.Date;

            DateTime? endDate =
                request.EndDate?
                    .Date;

            if (startDate <
                DateTime.Today)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "لا يمكن اختيار تاريخ حجز سابق."
                });
            }

            var maximumHorizonMonths =
                place.PlacePolicy?
                    .MaximumHorizonMonths ?? 0;

            if (maximumHorizonMonths > 0 &&
                (endDate ?? startDate) >
                    DateTime.Today.AddMonths(
                        maximumHorizonMonths))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        $"لا يمكن الحجز لأكثر من {maximumHorizonMonths} أشهر مقدمًا."
                });
            }

            decimal basePrice = 0;

            Room? selectedRoom = null;

            PlacePackage?
                selectedPackage = null;

            var blockAdjacentRoomBookings =
                false;

            var now =
                DateTime.Now;

            /* ============================================================
               حجز غرفة أو فندق
               ============================================================ */

            if (request.RoomId.HasValue)
            {
                selectedRoom =
                    await _context.Rooms
                        .FirstOrDefaultAsync(r =>
                            r.RoomId ==
                                request.RoomId.Value &&
                            r.PlaceId ==
                                request.PlaceId);

                if (selectedRoom == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message =
                            "الغرفة المختارة غير موجودة."
                    });
                }

                if (selectedRoom.IsAvailable !=
                    true)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "الغرفة المختارة غير متاحة حاليًا."
                    });
                }

                if (!endDate.HasValue ||
                    endDate.Value <= startDate)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "تاريخ الخروج يجب أن يكون بعد تاريخ الدخول."
                    });
                }

                if (ContainsWeeklyClosedDate(
                        place,
                        startDate,
                        endDate,
                        isRoomBooking: true))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "الفترة المختارة تتضمن يوم إغلاق أسبوعي حسب سياسة المنشأة."
                    });
                }

                var roomNoticeError =
                    ValidateMinimumNotice(
                        place,
                        startDate,
                        package: null,
                        now);

                if (!string.IsNullOrWhiteSpace(
                        roomNoticeError))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            roomNoticeError
                    });
                }

                blockAdjacentRoomBookings =
                    RequiresAdjacentRoomBufferBlock(
                        place);

                var activeRoomUnitsCount =
                    await _context.RoomUnits
                        .AsNoTracking()
                        .CountAsync(unit =>
                            unit.PlaceId ==
                                request.PlaceId &&

                            unit.RoomId ==
                                selectedRoom.RoomId &&

                            unit.IsAvailable == true);

                if (activeRoomUnitsCount <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "لا توجد غرف فعلية مفعلة من هذا النوع."
                    });
                }

                var containsClosedDate =
                    await _context.PlaceCalendarExceptions
                        .AsNoTracking()
                        .AnyAsync(exception =>
                            exception.PlaceId ==
                                request.PlaceId &&

                            exception.ExceptionDate >=
                                startDate &&

                            exception.ExceptionDate <
                                endDate.Value &&

                            exception.ExceptionType ==
                                "Closed");

                if (containsClosedDate)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "الفترة المختارة تحتوي على يوم مغلق من قبل المنشأة."
                    });
                }

                if (request.Quantity >
                    activeRoomUnitsCount)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            $"عدد الغرف المطلوبة أكبر من الوحدات الفعلية المفعلة ({activeRoomUnitsCount})."
                    });
                }

                var roomCapacity =
                    selectedRoom.MaxAdults +
                        selectedRoom.MaxChildren > 0
                        ? selectedRoom.MaxAdults +
                          selectedRoom.MaxChildren
                        : ExtractRoomCapacity(
                            selectedRoom.Description);

                var effectiveRoomCapacity =
                    roomCapacity.HasValue
                        ? roomCapacity.Value *
                          request.Quantity
                        : 0;

                var roomGuestValidation =
                    ValidateRoomGuestCapacity(
                        request.AdultsCount,
                        request.ChildrenCount,
                        effectiveRoomCapacity,
                        request.Quantity > 1
                            ? "الغرف المطلوبة"
                            : "الغرفة");

                if (!roomGuestValidation.Valid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            roomGuestValidation.Message
                    });
                }

                var numberOfDays =
                    (endDate.Value -
                     startDate).Days;

                basePrice =
                    selectedRoom.Price *
                    numberOfDays *
                    request.Quantity;
            }

            /* ============================================================
               حجز باقة أو شاليه أو قاعة
               ============================================================ */

            else if (request.PackageId.HasValue)
            {
                // الباقة أو القاعة حجز واحد مهما كانت القيمة القادمة من المتصفح.
                request.Quantity = 1;

                selectedPackage =
                    await _context
                        .PlacePackages
                        .FirstOrDefaultAsync(p =>
                            p.PackageId ==
                                request.PackageId.Value &&

                            p.PlaceId ==
                                request.PlaceId &&

                            p.IsActive ==
                                true);

                if (selectedPackage == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message =
                            "الباقة المختارة غير موجودة أو غير متاحة."
                    });
                }

                if (ContainsWeeklyClosedDate(
                        place,
                        startDate,
                        endDate,
                        isRoomBooking: false))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "هذا اليوم مغلق أسبوعيًا حسب سياسة المنشأة."
                    });
                }

                if (!PackageFitsConfiguredShift(
                        place,
                        selectedPackage))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "وقت هذه الباقة لا يقع ضمن إحدى الفترات التشغيلية المفعلة في سياسة المنشأة."
                    });
                }

                var packageNoticeError =
                    ValidateMinimumNotice(
                        place,
                        startDate,
                        selectedPackage,
                        now);

                if (!string.IsNullOrWhiteSpace(
                        packageNoticeError))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            packageNoticeError
                    });
                }

                var packageDateIsClosed =
                    await _context.PlaceCalendarExceptions
                        .AsNoTracking()
                        .AnyAsync(exception =>
                            exception.PlaceId ==
                                request.PlaceId &&

                            exception.ExceptionDate ==
                                startDate &&

                            exception.ExceptionType ==
                                "Closed");

                if (packageDateIsClosed)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "هذا اليوم مغلق من قبل المنشأة."
                    });
                }

                /*
                 * في القاعات لا توجد خطوة بالغين/أطفال؛ لذلك لا نطبق
                 * عليها تحقق الحضور نهائيًا.
                 */
                if (!isHallCreateBooking)
                {
                    var packageGuestValidation =
                        ValidatePackageGuestCapacity(
                            request.AdultsCount,
                            request.ChildrenCount,
                            Math.Max(
                                0,
                                selectedPackage.Capacity),
                            "الباقة");

                    if (!packageGuestValidation.Valid)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message =
                                packageGuestValidation.Message
                        });
                    }
                }

                var packageAlreadyBooked =
                    await HasPackageBookingConflictAsync(
                        place,
                        selectedPackage,
                        startDate,
                        now);

                if (packageAlreadyBooked)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "هذا الموعد يتعارض مع حجز قائم أو مع وقت التجهيز بين الحجوزات."
                    });
                }

                basePrice =
                    selectedPackage.Price *
                    request.Quantity;

                endDate ??=
                    startDate;
            }

            /* ============================================================
               الخدمات الإضافية
               ============================================================ */

            decimal servicesTotal = 0;

            var requestedServices =
                request.AdditionalServices
                    .Where(s =>
                        s.ServiceId > 0 &&
                        s.Quantity > 0)
                    .GroupBy(s =>
                        s.ServiceId)
                    .Select(g => new
                    {
                        ServiceId =
                            g.Key,

                        Quantity =
                            g.Sum(x =>
                                x.Quantity)
                    })
                    .ToList();

            var serviceIds =
                requestedServices
                    .Select(s =>
                        s.ServiceId)
                    .ToList();

            var availableServices =
                serviceIds.Count == 0
                    ? new List<
                        PlaceAdditionalService>()
                    : await _context
                        .PlaceAdditionalServices
                        .Where(s =>
                            serviceIds.Contains(
                                s.ServiceId) &&

                            s.PlaceId ==
                                request.PlaceId &&

                            s.IsActive ==
                                true)
                        .ToListAsync();

            if (availableServices.Count !=
                serviceIds.Count)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "إحدى الخدمات الإضافية غير موجودة أو غير متاحة."
                });
            }

            foreach (
                var selectedService in
                requestedServices)
            {
                var service =
                    availableServices
                        .First(s =>
                            s.ServiceId ==
                            selectedService.ServiceId);

                servicesTotal +=
                    service.Price *
                    selectedService.Quantity;
            }

            /* ============================================================
               السعر الأصلي + إعدادات العرض
               ------------------------------------------------------------
               الحساب النهائي للعرض يتم مرة أخرى داخل Serializable
               Transaction قبل إنشاء الحجز مباشرة.
               ============================================================ */

            basePrice =
                Math.Round(
                    basePrice,
                    2);

            servicesTotal =
                Math.Round(
                    servicesTotal,
                    2);

            var cleaningFee =
                Math.Round(
                    Math.Max(
                        0m,
                        place.PlacePolicy?
                            .CleaningFee ?? 0m),
                    2);

            var taxPercentage =
                Math.Max(
                    0m,
                    Math.Min(
                        place.PlacePolicy?
                            .TaxPercentage ?? 0m,
                        100m));

            var originalBeforeTax =
                Math.Round(
                    basePrice +
                    servicesTotal +
                    cleaningFee,
                    2);

            var originalTaxAmount =
                Math.Round(
                    originalBeforeTax *
                    taxPercentage /
                    100m,
                    2);

            var originalTotalPrice =
                Math.Round(
                    originalBeforeTax +
                    originalTaxAmount,
                    2);

            if (originalTotalPrice <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "تعذر حساب السعر الأصلي للحجز."
                });
            }

            var serviceTotalsById =
                requestedServices
                    .ToDictionary(
                        selectedService =>
                            selectedService.ServiceId,
                        selectedService =>
                        {
                            var service =
                                availableServices
                                    .First(item =>
                                        item.ServiceId ==
                                            selectedService.ServiceId);

                            return Math.Round(
                                service.Price *
                                selectedService.Quantity,
                                2);
                        });

            var bookingDaysForPromotion =
                selectedRoom != null &&
                endDate.HasValue
                    ? Math.Max(
                        1,
                        (endDate.Value.Date -
                         startDate.Date).Days)
                    : 1;

            var defaultCommissionPercentage =
                await _context
                    .SystemSettings
                    .AsNoTracking()
                    .OrderByDescending(setting =>
                        setting.Id)
                    .Select(setting =>
                        (decimal?)
                        setting.CommissionPercentage)
                    .FirstOrDefaultAsync() ??
                0m;

            /*
             * نستخدم نسبة الفئة إن كانت موجودة.
             * NULL يعني الرجوع إلى النسبة العامة.
             */
            var commissionPercentage =
                place.Category?
                    .CommissionPercentage ??
                defaultCommissionPercentage;

            commissionPercentage =
                Math.Clamp(
                    commissionPercentage,
                    0m,
                    100m);

            var paymentTimeout =
                place.PlacePolicy?
                    .PaymentTimeout ?? 12;

            if (paymentTimeout <= 0)
            {
                paymentTimeout = 12;
            }

            var promotionResolution =
                new CheckoutPromotionResolution();

            decimal discountAmount = 0m;
            decimal discountedBeforeTax = originalBeforeTax;
            decimal taxAmount = originalTaxAmount;
            decimal totalPrice = originalTotalPrice;
            decimal depositAmount = 0m;
            decimal amountToPayAtProperty = 0m;
            decimal platformCommission = 0m;
            bool requiresDeposit = false;
            string bookingState = string.Empty;
            DateTime? timerExpiredAt = null;

            // ------------------------------------------------------------
            // نسبة الحجز إلى إعلان سابق
            // القيمة القادمة من المتصفح لا تُحفظ مباشرة؛ خدمة الإعلانات
            // تتحقق من المنشأة ومن نقرة موثقة لنفس العميل خلال نافذة النسبة.
            // فشل التحقق لا يمنع الحجز الطبيعي، بل يلغي النسبة فقط.
            // ------------------------------------------------------------
            int? validatedAdvertisementId = null;

            if (request.AdvertisementId.HasValue &&
                request.AdvertisementId.Value > 0)
            {
                try
                {
                    validatedAdvertisementId =
                        await _customerAdvertisementService
                            .ResolveAttributionAdvertisementIdAsync(
                                request.AdvertisementId,
                                place.PlaceId,
                                HttpContext.RequestAborted);
                }
                catch
                {
                    validatedAdvertisementId = null;
                }
            }


            /* ============================================================
               الحفظ داخل Transaction
               ============================================================ */

            var selectedRoomUnits =
                new List<RoomUnit>();

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            try
            {
                var createdAt =
                    DateTime.Now;

                /* ========================================================
                   التحقق النهائي من العرض داخل Transaction
                   ======================================================== */

                promotionResolution =
                    await ResolveCheckoutPromotionAsync(
                        place.PlaceId,
                        customerId,
                        selectedRoom?.RoomId,
                        selectedPackage?.PackageId,
                        startDate,
                        endDate,
                        bookingDaysForPromotion,
                        request.Quantity,
                        request.AdultsCount +
                        request.ChildrenCount,
                        basePrice,
                        servicesTotal,
                        serviceTotalsById,
                        requestedServices.ToDictionary(service => service.ServiceId, service => Math.Max(1, service.Quantity)),
                        request.PromotionId,
                        request.PromotionCode,
                        allowAutomaticSelection: true);

                var promotionWasExplicitlyRequested =
                    request.PromotionId.HasValue &&
                    request.PromotionId.Value > 0;

                var promotionCodeWasSubmitted =
                    !string.IsNullOrWhiteSpace(
                        request.PromotionCode);

                if (
                    (promotionWasExplicitlyRequested ||
                     promotionCodeWasSubmitted) &&
                    !promotionResolution.IsApplied)
                {
                    await transaction.RollbackAsync();

                    return Conflict(new
                    {
                        success = false,
                        promotionChanged = true,
                        message =
                            string.IsNullOrWhiteSpace(
                                promotionResolution.Message)
                                ? "تعذر تطبيق العرض عند التحقق النهائي. راجع السعر ثم أعد المحاولة."
                                : promotionResolution.Message
                    });
                }

                discountAmount =
                    promotionResolution.IsApplied
                        ? Math.Round(
                            Math.Min(
                                promotionResolution.DiscountAmount,
                                Math.Max(
                                    0m,
                                    basePrice +
                                    servicesTotal)),
                            2)
                        : 0m;

                discountedBeforeTax =
                    Math.Round(
                        Math.Max(
                            0m,
                            originalBeforeTax -
                            discountAmount),
                        2);

                taxAmount =
                    Math.Round(
                        discountedBeforeTax *
                        taxPercentage /
                        100m,
                        2);

                totalPrice =
                    Math.Round(
                        discountedBeforeTax +
                        taxAmount,
                        2);

                if (totalPrice <= 0)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "تعذر حساب السعر النهائي للحجز."
                    });
                }

                depositAmount =
                    CalculateDepositAmount(
                        place,
                        totalPrice);

                requiresDeposit =
                    depositAmount > 0;

                // للتوافق مع واجهة Checkout الحالية فقط.
                approvalMode =
                    requiresDeposit
                        ? "Instant"
                        : "Manual";

                amountToPayAtProperty =
                    Math.Round(
                        Math.Max(
                            0m,
                            totalPrice -
                            depositAmount),
                        2);

                platformCommission =
                    Math.Round(
                        totalPrice *
                        commissionPercentage /
                        100m,
                        2);

                /*
                 * الرحلة الموحدة:
                 * مع عربون:
                 * AwaitingPayment -> PaymentReview -> موافقة المالك -> Confirmed
                 *
                 * بدون عربون:
                 * Pending -> موافقة المالك -> Confirmed
                 */
                if (requiresDeposit)
                {
                    bookingState =
                        "AwaitingPayment";

                    timerExpiredAt =
                        createdAt.AddHours(
                            paymentTimeout);
                }
                else
                {
                    bookingState =
                        "Pending";
                }

                if (selectedPackage != null)
                {
                    var packageStillBooked =
                        await HasPackageBookingConflictAsync(
                            place,
                            selectedPackage,
                            startDate,
                            createdAt);

                    if (packageStillBooked)
                    {
                        await transaction
                            .RollbackAsync();

                        return Conflict(new
                        {
                            success = false,
                            availabilityChanged = true,
                            message =
                                "حُجز هذا الموعد قبل إتمام الطلب أو أصبح متعارضًا مع وقت التجهيز. اختاري موعدًا آخر."
                        });
                    }
                }

                /*
                 * إعادة فحص وحدات الغرف واختيار الأرقام داخل نفس المعاملة.
                 * هذا هو الفحص النهائي؛ لا نعتمد على الكمية التي شاهدها
                 * العميل في صفحة Checkout لأن التوفر قد يتغير قبل الضغط.
                 */
                if (selectedRoom != null &&
                    endDate.HasValue)
                {
                    var activeRoomUnits =
                        await _context.RoomUnits
                            .Where(unit =>
                                unit.PlaceId ==
                                    request.PlaceId &&

                                unit.RoomId ==
                                    selectedRoom.RoomId &&

                                unit.IsAvailable == true)
                            .OrderBy(unit =>
                                unit.FloorNumber)
                            .ThenBy(unit =>
                                unit.RoomNumber)
                            .ThenBy(unit =>
                                unit.RoomUnitId)
                            .ToListAsync();

                    var activeUnitIds =
                        activeRoomUnits
                            .Select(unit =>
                                unit.RoomUnitId)
                            .ToList();

                    var occupiedUnitIds =
                        activeUnitIds.Count == 0
                            ? new List<int>()
                            : await _context.BookingRoomUnits
                                .AsNoTracking()
                                .Where(assignment =>
                                    activeUnitIds.Contains(
                                        assignment.RoomUnitId) &&

                                    BlockingRoomAssignmentStates.Contains(
                                        assignment.AssignmentStatus) &&

                                    BlockingBookingStates.Contains(
                                        assignment.Booking.BookingState ??
                                            string.Empty) &&

                                    (
                                        assignment.Booking.BookingState !=
                                            "AwaitingPayment" ||

                                        !assignment.Booking.TimerExpiredAt
                                            .HasValue ||

                                        assignment.Booking.TimerExpiredAt >
                                            createdAt
                                    ) &&

                                    (
                                        blockAdjacentRoomBookings
                                            ? assignment.Booking.StartDate <=
                                                endDate.Value
                                            : assignment.Booking.StartDate <
                                                endDate.Value
                                    ) &&

                                    (
                                        blockAdjacentRoomBookings
                                            ? (
                                                assignment.Booking.EndDate ??
                                                assignment.Booking.StartDate
                                                    .AddDays(1)
                                              ) >= startDate
                                            : (
                                                assignment.Booking.EndDate ??
                                                assignment.Booking.StartDate
                                                    .AddDays(1)
                                              ) > startDate
                                    ))
                                .Select(assignment =>
                                    assignment.RoomUnitId)
                                .Distinct()
                                .ToListAsync();

                    var legacyReservedQuantity =
                        await _context.Bookings
                            .AsNoTracking()
                            .Where(existingBooking =>
                                existingBooking.RoomId ==
                                    selectedRoom.RoomId &&

                                !existingBooking.BookingRoomUnits.Any() &&

                                BlockingBookingStates.Contains(
                                    existingBooking.BookingState ??
                                        string.Empty) &&

                                (
                                    existingBooking.BookingState !=
                                        "AwaitingPayment" ||

                                    !existingBooking.TimerExpiredAt
                                        .HasValue ||

                                    existingBooking.TimerExpiredAt >
                                        createdAt
                                ) &&

                                (
                                    blockAdjacentRoomBookings
                                        ? existingBooking.StartDate <=
                                            endDate.Value
                                        : existingBooking.StartDate <
                                            endDate.Value
                                ) &&

                                (
                                    blockAdjacentRoomBookings
                                        ? (
                                            existingBooking.EndDate ??
                                            existingBooking.StartDate
                                                .AddDays(1)
                                          ) >= startDate
                                        : (
                                            existingBooking.EndDate ??
                                            existingBooking.StartDate
                                                .AddDays(1)
                                          ) > startDate
                                ))
                            .SumAsync(existingBooking =>
                                (int?)existingBooking.Quantity) ??
                        0;

                    var occupiedUnitIdSet =
                        occupiedUnitIds.ToHashSet();

                    selectedRoomUnits =
                        activeRoomUnits
                            .Where(unit =>
                                !occupiedUnitIdSet.Contains(
                                    unit.RoomUnitId))
                            // الحجوزات القديمة تشغل كمية بلا رقم غرفة محدد.
                            .Skip(
                                Math.Max(
                                    0,
                                    legacyReservedQuantity))
                            .Take(
                                request.Quantity)
                            .ToList();

                    if (selectedRoomUnits.Count !=
                        request.Quantity)
                    {
                        await transaction
                            .RollbackAsync();

                        return Conflict(new
                        {
                            success = false,
                            availabilityChanged = true,
                            message =
                                "تغير توفر الغرف قبل إتمام الحجز. ارجعي إلى الغرف المتاحة واختاري العدد المتوفر حاليًا."
                        });
                    }
                }

                var booking =
                    new Booking
                    {
                        CustomerId =
                            customerId,

                        PlaceId =
                            request.PlaceId,

                        RoomId =
                            request.RoomId,

                        PackageId =
                            request.PackageId,

                        StartDate =
                            startDate,

                        EndDate =
                            endDate,

                        Quantity =
                            request.Quantity,

                        AdultsCount =
                            request.AdultsCount,

                        ChildrenCount =
                            request.ChildrenCount,

                        BookingType =
                            string.IsNullOrWhiteSpace(
                                request.BookingType)
                                ? null
                                : request
                                    .BookingType
                                    .Trim(),

                        SeatingSetup =
                            string.IsNullOrWhiteSpace(
                                request.SeatingSetup)
                                ? null
                                : request
                                    .SeatingSetup
                                    .Trim(),

                        TotalPrice =
                            totalPrice,

                        PromotionId =
                            promotionResolution.IsApplied
                                ? promotionResolution.PromotionId
                                : null,

                        OriginalTotalPrice =
                            originalTotalPrice,

                        DiscountAmount =
                            discountAmount,

                        PromotionTitleSnapshot =
                            promotionResolution.IsApplied
                                ? promotionResolution.PromotionTitle
                                : null,

                        PromotionCodeSnapshot =
                            promotionResolution.IsApplied &&
                            !string.IsNullOrWhiteSpace(
                                promotionResolution.PromotionCode)
                                ? promotionResolution.PromotionCode
                                : null,

                        DiscountTypeSnapshot =
                            promotionResolution.IsApplied
                                ? (string.Equals(
                                        promotionResolution.OfferMode,
                                        "ConditionalReward",
                                        StringComparison.OrdinalIgnoreCase) &&
                                   !string.IsNullOrWhiteSpace(promotionResolution.RewardType)
                                    ? promotionResolution.RewardType
                                    : promotionResolution.DiscountType)
                                : null,

                        DiscountValueSnapshot =
                            promotionResolution.IsApplied
                                ? string.Equals(
                                      promotionResolution.RewardType,
                                      "FreeQuantity",
                                      StringComparison.OrdinalIgnoreCase)
                                    ? promotionResolution.AppliedFreeQuantity
                                    : promotionResolution.SpecialPrice ??
                                      (promotionResolution.RewardQuantity.HasValue
                                           ? promotionResolution.RewardQuantity.Value
                                           : promotionResolution.DiscountValue)
                                : null,

                        AdvertisementId =
                            validatedAdvertisementId,

                        BookingState =
                            bookingState,

                        PaymentStatus =
                            "Unpaid",

                        RefundStatus =
                            "NotApplicable",

                        TimerExpiredAt =
                            timerExpiredAt,

                        LastStatusChangedAt =
                            createdAt,

                        AcceptedAt =
                            null,

                        CreatedAt =
                            createdAt
                    };

                _context.Bookings.Add(
                    booking);

                await _context
                    .SaveChangesAsync();

                if (selectedRoomUnits.Count > 0)
                {
                    var assignmentStatus =
                        IsState(
                            bookingState,
                            "Confirmed")
                            ? "Confirmed"
                            : "Held";

                    foreach (
                        var roomUnit in
                        selectedRoomUnits)
                    {
                        _context.BookingRoomUnits.Add(
                            new BookingRoomUnit
                            {
                                BookingId =
                                    booking.BookingId,

                                RoomUnitId =
                                    roomUnit.RoomUnitId,

                                AssignmentStatus =
                                    assignmentStatus,

                                AssignedAt =
                                    createdAt,

                                ReleasedAt =
                                    null
                            });
                    }
                }

                foreach (
                    var selectedService in
                    requestedServices)
                {
                    var service =
                        availableServices
                            .First(s =>
                                s.ServiceId ==
                                selectedService.ServiceId);

                    _context
                        .BookingAdditionalServices
                        .Add(
                            new BookingAdditionalService
                            {
                                BookingId =
                                    booking.BookingId,

                                ServiceId =
                                    service.ServiceId,

                                Quantity =
                                    selectedService.Quantity,

                                UnitPrice =
                                    service.Price
                            });
                }

                if (promotionResolution.IsApplied &&
                    promotionResolution.AddsComplimentaryService &&
                    promotionResolution.RewardServiceId.HasValue &&
                    requestedServices.All(service =>
                        service.ServiceId != promotionResolution.RewardServiceId.Value))
                {
                    var rewardService =
                        await _context.PlaceAdditionalServices
                            .FirstOrDefaultAsync(service =>
                                service.ServiceId == promotionResolution.RewardServiceId.Value &&
                                service.PlaceId == request.PlaceId &&
                                service.IsActive == true);

                    if (rewardService != null)
                    {
                        _context.BookingAdditionalServices.Add(
                            new BookingAdditionalService
                            {
                                BookingId = booking.BookingId,
                                ServiceId = rewardService.ServiceId,
                                Quantity = Math.Max(1, promotionResolution.RewardQuantity ?? 1),
                                UnitPrice = 0m
                            });
                    }
                }

                var financialRecord =
                    new FinancialRecord
                    {
                        BookingId =
                            booking.BookingId,

                        DepositAmount =
                            depositAmount,

                        PlatformCommission =
                            platformCommission,

                        CommissionRateSnapshot =
                            commissionPercentage,

                        CommissionBaseAmount =
                            totalPrice,

                        CommissionStatus =
                            "Calculated",

                        AmountPaidOnline =
                            0m,

                        AmountToPayAtProperty =
                            amountToPayAtProperty,

                        AmountCollectedAtProperty =
                            0m,

                        UpdatedAt =
                            createdAt
                    };

                _context
                    .FinancialRecords
                    .Add(
                        financialRecord);

                var currencyContext =
                    await GetCustomerCurrencyContextAsync();

                string? exchangeRateRegion =
                    place.City?
                        .ExchangeRateRegion;

                decimal depositAmountDisplay =
                    currencyContext.Convert(
                        depositAmount,
                        exchangeRateRegion);

                string depositAmountText =
                    currencyContext.Format(
                        depositAmount,
                        exchangeRateRegion);

                decimal totalPriceDisplay =
                    currencyContext.Convert(
                        totalPrice,
                        exchangeRateRegion);

                string totalPriceText =
                    currencyContext.Format(
                        totalPrice,
                        exchangeRateRegion);

                decimal remainingAmountDisplay =
                    currencyContext.Convert(
                        amountToPayAtProperty,
                        exchangeRateRegion);

                string remainingAmountText =
                    currencyContext.Format(
                        amountToPayAtProperty,
                        exchangeRateRegion);

                string customerNotificationText;
                string customerNotificationTitle;
                string customerNotificationType;
                string customerTargetUrl;

                if (requiresDeposit)
                {
                    customerNotificationTitle =
                        "مطلوب دفع العربون";
                    customerNotificationType =
                        NotificationTypes.PaymentRequired;
                    customerNotificationText =
                        $"تم إنشاء الحجز رقم {booking.BookingId}. يرجى دفع عربون بقيمة {depositAmountText} قبل انتهاء المهلة، ثم يراجع المالك إثبات الدفع ويؤكد الحجز.";
                    customerTargetUrl =
                        $"/BookingFlow/Payment/{booking.BookingId}";
                }
                else
                {
                    customerNotificationTitle =
                        "تم إرسال الحجز للمالك";
                    customerNotificationType =
                        NotificationTypes.BookingCreated;
                    customerNotificationText =
                        $"تم إنشاء الحجز رقم {booking.BookingId} وإرساله إلى مالك المنشأة للمراجعة والتأكيد.";
                    customerTargetUrl =
                        $"/Places/BookingDetails/{booking.BookingId}";
                }

                _notificationService.Queue(
                    new CreateNotificationDto
                    {
                        UserId = customerId,
                        NotificationTitle =
                            customerNotificationTitle,
                        NotificationText =
                            customerNotificationText,
                        NotificationType =
                            customerNotificationType,
                        TargetUrl =
                            customerTargetUrl,
                        ReferenceType =
                            NotificationReferenceTypes.Booking,
                        ReferenceId =
                            booking.BookingId
                    });

                if (place.OwnerId.HasValue)
                {
                    string ownerNotificationText;
                    string ownerNotificationTitle;
                    string ownerNotificationType;

                    if (requiresDeposit)
                    {
                        ownerNotificationTitle =
                            "حجز جديد بانتظار العربون";
                        ownerNotificationType =
                            NotificationTypes.BookingCreated;
                        ownerNotificationText =
                            $"تم إنشاء الحجز رقم {booking.BookingId}، وهو بانتظار دفع العربون من العميل. بعد رفع الإثبات سيظهر لك للمراجعة والتأكيد.";
                    }
                    else
                    {
                        ownerNotificationTitle =
                            "طلب حجز جديد";
                        ownerNotificationType =
                            NotificationTypes.BookingCreated;
                        ownerNotificationText =
                            $"لديك حجز جديد رقم {booking.BookingId} جاهز للمراجعة والموافقة.";
                    }

                    _notificationService.Queue(
                        new CreateNotificationDto
                        {
                            UserId =
                                place.OwnerId.Value,
                            NotificationTitle =
                                ownerNotificationTitle,
                            NotificationText =
                                ownerNotificationText,
                            NotificationType =
                                ownerNotificationType,
                            TargetUrl =
                                $"/Owner/Reservation/ManageReservations?bookingId={booking.BookingId}",
                            ReferenceType =
                                NotificationReferenceTypes.Booking,
                            ReferenceId =
                                booking.BookingId
                        });
                }

                await _context
                    .SaveChangesAsync();

                /*
                 * الرحلة الجديدة لا تنشئ Confirmed مباشرة.
                 * الشرط التالي يبقى للتوافق مع الحالات القديمة فقط.
                 */
                if (IsState(
                        booking.BookingState,
                        "Confirmed"))
                {
                    var commissionResult =
                        await _commissionFinanceService
                            .HoldBookingCommissionAsync(
                                booking.BookingId,
                                customerId,
                                HttpContext.RequestAborted);

                    if (!commissionResult.Success)
                    {
                        await transaction
                            .RollbackAsync();

                        return BadRequest(new
                        {
                            success = false,
                            message =
                                commissionResult.Message
                        });
                    }
                }

                await transaction
                    .CommitAsync();

                // تسجيل Conversion بعد نجاح الحجز فقط. أي خطأ في الإحصاء
                // لا يعيد الحجز ولا يحوّل نجاح العملية إلى فشل للعميل.
                if (booking.AdvertisementId.HasValue)
                {
                    try
                    {
                        await _customerAdvertisementService
                            .RecordBookingCompletedAsync(
                                booking.AdvertisementId.Value,
                                booking.BookingId,
                                booking.PlaceId,
                                HttpContext.RequestAborted);
                    }
                    catch
                    {
                        // التتبع ثانوي ولا يجب أن يمس معاملة الحجز المكتملة.
                    }
                }

                var redirectUrl =
                    requiresDeposit
                        ? Url.Action(
                            nameof(Payment),
                            "BookingFlow",
                            new
                            {
                                id =
                                    booking.BookingId
                            })
                        : Url.Action(
                            "BookingDetails",
                            "Places",
                            new
                            {
                                id =
                                    booking.BookingId
                            });

                return Json(new
                {
                    success = true,

                    bookingId =
                        booking.BookingId,

                    approvalMode,

                    bookingState,

                    requiresPayment =
                        requiresDeposit,

                    depositAmount,

                    depositAmountDisplay,

                    depositAmountText,

                    totalPrice,

                    totalPriceDisplay,

                    totalPriceText,

                    remainingAmount =
                        amountToPayAtProperty,

                    remainingAmountDisplay,

                    remainingAmountText,

                    displayCurrency =
                        currencyContext.Currency,

                    displayCurrencyCode =
                        currencyContext.Code,

                    displayCurrencySymbol =
                        currencyContext.Symbol,

                    paymentTimeout,

                    timerExpiredAt,

                    assignedRoomUnitsCount =
                        selectedRoomUnits.Count,

                    roomAssignmentStatus =
                        selectedRoomUnits.Count == 0
                            ? null
                            : IsState(
                                bookingState,
                                "Confirmed")
                                ? "Confirmed"
                                : "Held",

                    redirectUrl,

                    message =
                        requiresDeposit
                            ? $"تم إنشاء الحجز. قيمة العربون المطلوبة {depositAmountText}، وبعد رفع الإثبات يراجع المالك الحجز ويؤكده."
                            : "تم إنشاء الحجز وإرساله للمالك للمراجعة والتأكيد."
                });
            }
            catch (Exception ex)
            {
                await transaction
                    .RollbackAsync();

                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "حدث خطأ أثناء حفظ الحجز في الخادم.",

                        details =
                            ex.InnerException?
                                .Message ??
                            ex.Message
                    });
            }
        }

        /* ================================================================
           عرض صفحة دفع العربون
           ================================================================ */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExpirePayment(
            int bookingId)
        {
            if (!TryGetCustomerId(
                    out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message =
                        "تعذر التعرف على حساب العميل."
                });
            }

            var booking =
                await _context.Bookings
                    .Include(item =>
                        item.BookingRoomUnits)
                    .FirstOrDefaultAsync(item =>
                        item.BookingId ==
                            bookingId &&

                        item.CustomerId ==
                            customerId);

            if (booking == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "الحجز غير موجود أو لا يتبع حسابك."
                });
            }

            if (!IsState(
                    booking.BookingState,
                    "AwaitingPayment"))
            {
                return Json(new
                {
                    success = true,
                    alreadyProcessed = true,
                    bookingState =
                        booking.BookingState
                });
            }

            var expiredAt =
                DateTime.Now;

            if (!booking.TimerExpiredAt.HasValue ||
                booking.TimerExpiredAt.Value >
                    expiredAt)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "مهلة الدفع لم تنتهِ بعد."
                });
            }

            booking.BookingState =
                "Expired";

            booking.PaymentStatus =
                "Expired";

            booking.RefundStatus =
                "NotApplicable";

            booking.TimerExpiredAt =
                null;

            booking.ClosedByUserId =
                null;

            booking.ClosedByType =
                "System";

            booking.ClosedAt =
                expiredAt;

            booking.LastStatusChangedAt =
                expiredAt;

            ReleaseHeldRoomUnits(
                booking,
                expiredAt);

            await _context
                .SaveChangesAsync();

            return Json(new
            {
                success = true,
                bookingState =
                    booking.BookingState,
                message =
                    "انتهت مهلة الدفع وتم تحرير الغرف المحجوزة مؤقتًا."
            });
        }

        [HttpGet]
        public async Task<IActionResult> Payment(
            int id)
        {
            if (!TryGetCustomerId(
                out var customerId))
            {
                return Unauthorized(
                    "تعذر التعرف على حساب العميل.");
            }

            if (id <= 0)
            {
                return BadRequest(
                    "رقم الحجز غير صحيح.");
            }

            var booking =
                await _context.Bookings
                    .Include(b =>
                        b.FinancialRecord)

                    .Include(b =>
                        b.BookingRoomUnits)
                    .ThenInclude(assignment =>
                        assignment.RoomUnit)

                    .Include(b =>
                        b.Room)

                    .Include(b =>
                        b.Package)

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
                        p.Owner)
                    .ThenInclude(owner =>
                        owner.OwnerWallets)
                    .ThenInclude(ownerWallet =>
                        ownerWallet.Wallet)

                    .FirstOrDefaultAsync(b =>
                        b.BookingId ==
                            id &&
                        b.CustomerId ==
                            customerId);

            if (booking == null)
            {
                return NotFound(
                    "الحجز غير موجود أو لا يتبع حسابك.");
            }

            var financialRecord =
                booking.FinancialRecord;

            if (financialRecord == null ||
                financialRecord.DepositAmount <= 0)
            {
                return RedirectToAction(
                    "BookingDetails",
                    "Places",
                    new
                    {
                        id =
                            booking.BookingId
                    });
            }

            if (IsState(
                    booking.BookingState,
                    "Confirmed") ||
                IsState(
                    booking.PaymentStatus,
                    "Paid"))
            {
                return RedirectToAction(
                    "BookingDetails",
                    "Places",
                    new
                    {
                        id =
                            booking.BookingId
                    });
            }

            if (IsState(
                    booking.BookingState,
                    "Pending"))
            {
                TempData["ErrorMessage"] =
                    "هذا الحجز ما زال بانتظار موافقة المالك.";

                return RedirectToAction(
                    "BookingDetails",
                    "Places",
                    new
                    {
                        id =
                            booking.BookingId
                    });
            }

            if (IsState(
                    booking.BookingState,
                    "AwaitingPayment") &&
                booking.TimerExpiredAt
                    .HasValue &&
                booking.TimerExpiredAt
                    .Value <=
                DateTime.Now)
            {
                var expiredAt =
                    DateTime.Now;

                booking.BookingState =
                    "Expired";

                booking.PaymentStatus =
                    "Expired";

                booking.RefundStatus =
                    "NotApplicable";

                booking.TimerExpiredAt =
                    null;

                booking.ClosedByUserId =
                    null;

                booking.ClosedByType =
                    "System";

                booking.ClosedAt =
                    expiredAt;

                booking.LastStatusChangedAt =
                    expiredAt;

                ReleaseHeldRoomUnits(
                    booking,
                    expiredAt);

                await _context
                    .SaveChangesAsync();

                TempData["ErrorMessage"] =
                    "انتهت مهلة دفع العربون لهذا الحجز.";

                return RedirectToAction(
                    "BookingDetails",
                    "Places",
                    new
                    {
                        id =
                            booking.BookingId
                    });
            }

            if (!IsState(
                    booking.BookingState,
                    "AwaitingPayment") &&
                !IsState(
                    booking.BookingState,
                    "PaymentReview"))
            {
                return RedirectToAction(
                    "BookingDetails",
                    "Places",
                    new
                    {
                        id =
                            booking.BookingId
                    });
            }

            var currencyContext =
                await GetCustomerCurrencyContextAsync();

            string? exchangeRateRegion =
                booking.Place?
                    .City?
                    .ExchangeRateRegion;

            decimal remainingAmount =
                Math.Max(
                    0m,
                    booking.FinancialRecord?
                        .AmountToPayAtProperty ??
                    booking.TotalPrice -
                    (booking.FinancialRecord?
                        .DepositAmount ?? 0m));

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
            ViewBag.DepositAmountDisplay =
                currencyContext.Convert(
                    financialRecord.DepositAmount,
                    exchangeRateRegion);
            ViewBag.DepositAmountText =
                currencyContext.Format(
                    financialRecord.DepositAmount,
                    exchangeRateRegion);
            ViewBag.RemainingAmountDisplay =
                currencyContext.Convert(
                    remainingAmount,
                    exchangeRateRegion);
            ViewBag.RemainingAmountText =
                currencyContext.Format(
                    remainingAmount,
                    exchangeRateRegion);

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

            return View(
                booking);
        }

        /* ================================================================
           رفع إشعار الحوالة وإرساله للمراجعة
           ================================================================ */

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(
            6 * 1024 * 1024)]
        public async Task<IActionResult>
            SubmitPaymentProof(
                int BookingId,
                int OwnerWalletId,
                string? PaymentMethod,
                string? OnlinePaymentType,
                string?
                    TransferReferenceNumber,
                IFormFile? PaymentProof)
        {
            if (!TryGetCustomerId(
                out var customerId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message =
                        "تعذر التعرف على حساب العميل."
                });
            }

            if (BookingId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "رقم الحجز غير صحيح."
                });
            }

            if (OwnerWalletId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "يجب اختيار محفظة الاستقبال."
                });
            }

            var booking =
                await _context.Bookings
                    .Include(b =>
                        b.Place)
                    .Include(b =>
                        b.FinancialRecord)
                    .Include(b =>
                        b.BookingRoomUnits)
                    .ThenInclude(assignment =>
                        assignment.RoomUnit)
                    .FirstOrDefaultAsync(b =>
                        b.BookingId ==
                            BookingId &&
                        b.CustomerId ==
                            customerId);

            if (booking == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "الحجز غير موجود أو لا يتبع حسابك."
                });
            }

            if (!IsState(
                    booking.BookingState,
                    "AwaitingPayment"))
            {
                return BadRequest(new
                {
                    success = false,

                    message =
                        IsState(
                            booking.BookingState,
                            "PaymentReview")
                            ? "تم إرسال إشعار الحوالة مسبقًا، وهو الآن قيد المراجعة."
                            : "هذا الحجز غير متاح حاليًا لإرسال إثبات الدفع."
                });
            }

            if (booking.TimerExpiredAt
                    .HasValue &&
                booking.TimerExpiredAt
                    .Value <=
                DateTime.Now)
            {
                var expiredAt =
                    DateTime.Now;

                booking.BookingState =
                    "Expired";

                booking.PaymentStatus =
                    "Expired";

                booking.RefundStatus =
                    "NotApplicable";

                booking.TimerExpiredAt =
                    null;

                booking.ClosedByUserId =
                    null;

                booking.ClosedByType =
                    "System";

                booking.ClosedAt =
                    expiredAt;

                booking.LastStatusChangedAt =
                    expiredAt;

                ReleaseHeldRoomUnits(
                    booking,
                    expiredAt);

                await _context
                    .SaveChangesAsync();

                return BadRequest(new
                {
                    success = false,
                    message =
                        "انتهت مهلة الدفع لهذا الحجز."
                });
            }

            if (!booking.Place.OwnerId
                .HasValue)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "لا يوجد مالك مرتبط بهذه المنشأة."
                });
            }

            var ownerWallet =
                await _context.OwnerWallets
                    .Include(wallet =>
                        wallet.Wallet)
                    .FirstOrDefaultAsync(wallet =>
                        wallet.OwnerWalletId ==
                            OwnerWalletId &&

                        wallet.OwnerId ==
                            booking.Place
                                .OwnerId.Value &&

                        wallet.IsActive ==
                            true &&

                        wallet.Wallet !=
                            null &&

                        wallet.Wallet.IsActive ==
                            true);

            if (ownerWallet == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "محفظة الاستقبال غير موجودة أو غير متاحة."
                });
            }

            var financialRecord =
                booking.FinancialRecord;

            if (financialRecord == null ||
                financialRecord.DepositAmount <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "لم يتم العثور على بيانات العربون الخاصة بالحجز."
                });
            }

            string normalizedOnlinePaymentType;

            if (IsState(
                    OnlinePaymentType,
                    FinancialOnlinePaymentTypes.Deposit))
            {
                normalizedOnlinePaymentType =
                    FinancialOnlinePaymentTypes.Deposit;
            }
            else if (IsState(
                         OnlinePaymentType,
                         FinancialOnlinePaymentTypes.Full))
            {
                normalizedOnlinePaymentType =
                    FinancialOnlinePaymentTypes.Full;
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "اختر طريقة الدفع: العربون أو كامل قيمة الحجز."
                });
            }

            var expectedOnlinePaymentAmount =
                IsState(
                    normalizedOnlinePaymentType,
                    FinancialOnlinePaymentTypes.Full)
                    ? Math.Max(
                        0m,
                        booking.TotalPrice)
                    : Math.Max(
                        0m,
                        financialRecord.DepositAmount);

            if (expectedOnlinePaymentAmount <= 0m)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "تعذر تحديد المبلغ المطلوب تحويله لهذا الحجز."
                });
            }

            var onlinePaymentTypeLabel =
                IsState(
                    normalizedOnlinePaymentType,
                    FinancialOnlinePaymentTypes.Full)
                    ? "كامل قيمة الحجز"
                    : "العربون";

            if (PaymentProof == null ||
                PaymentProof.Length <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "يجب اختيار صورة إشعار الحوالة."
                });
            }

            if (PaymentProof.Length >
                MaximumPaymentProofSize)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "حجم صورة الحوالة أكبر من 5 ميجابايت."
                });
            }

            var extension =
                Path.GetExtension(
                    PaymentProof.FileName)
                    .ToLowerInvariant();

            if (!AllowedPaymentProofExtensions
                .Contains(extension))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "صيغة الصورة غير مدعومة. استخدمي JPG أو PNG أو WEBP."
                });
            }

            var permittedContentTypes =
                new[]
                {
                    "image/jpeg",
                    "image/png",
                    "image/webp"
                };

            if (!permittedContentTypes
                .Contains(
                    PaymentProof.ContentType,
                    StringComparer
                        .OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "نوع ملف صورة الحوالة غير صالح."
                });
            }

            var relativeFolder =
                Path.Combine(
                    "uploads",
                    "payment-proofs");

            var absoluteFolder =
                Path.Combine(
                    GetWebRootPath(),
                    relativeFolder);

            Directory.CreateDirectory(
                absoluteFolder);

            var safeFileName =
                $"booking-{BookingId}-" +
                $"{Guid.NewGuid():N}" +
                extension;

            var absoluteFilePath =
                Path.Combine(
                    absoluteFolder,
                    safeFileName);

            var relativeFileUrl =
                "/" +
                Path.Combine(
                        relativeFolder,
                        safeFileName)
                    .Replace(
                        "\\",
                        "/");

            try
            {
                await using var stream =
                    new FileStream(
                        absoluteFilePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);

                await PaymentProof
                    .CopyToAsync(stream);
            }
            catch
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message =
                            "تعذر حفظ صورة الحوالة في الخادم."
                    });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var walletName =
                    ownerWallet.Wallet?
                        .WalletName?
                        .Trim();

                if (string.IsNullOrWhiteSpace(
                    walletName))
                {
                    walletName =
                        string.IsNullOrWhiteSpace(
                            PaymentMethod)
                            ? "محفظة إلكترونية"
                            : PaymentMethod.Trim();
                }

                if (walletName.Length > 30)
                {
                    walletName =
                        walletName.Substring(
                            0,
                            30);
                }

                var referenceNumber =
                    TransferReferenceNumber?
                        .Trim();

                if (!string.IsNullOrWhiteSpace(
                        referenceNumber) &&
                    referenceNumber.Length >
                    100)
                {
                    referenceNumber =
                        referenceNumber.Substring(
                            0,
                            100);
                }

                var createdAt =
                    DateTime.Now;

                booking.PaymentMethod =
                    walletName;

                booking.PaymentNotificationImage =
                    relativeFileUrl;

                booking.PaymentStatus =
                    "UnderReview";

                booking.BookingState =
                    "PaymentReview";

                // بعد رفع إثبات جديد نزيل سبب رفض الإثبات السابق،
                // مع الاحتفاظ بعدد مرات الرفض المسجل.
                booking.PaymentProofRejectionReason =
                    null;

                booking.PaymentProofRejectedAt =
                    null;

                booking.RejectionReason =
                    null;

                booking.LastStatusChangedAt =
                    createdAt;

                booking.ClosedByUserId =
                    null;

                booking.ClosedByType =
                    null;

                booking.ClosedAt =
                    null;

                // بعد رفع الحوالة لا نلغي الحجز
                // بسبب مؤقت الدفع.
                booking.TimerExpiredAt =
                    null;

                financialRecord
                    .TransferReferenceNumber =
                    referenceNumber;

                // نحفظ نوع الدفع الذي اختاره العميل فقط.
                // AmountPaidOnline لا يتغير هنا لأن الحوالة لم تُعتمد بعد.
                financialRecord
                    .OnlinePaymentType =
                    normalizedOnlinePaymentType;

                financialRecord
                    .UpdatedAt =
                    createdAt;

                _notificationService.Queue(
                    new CreateNotificationDto
                    {
                        UserId = customerId,
                        NotificationTitle =
                            "إثبات الدفع قيد المراجعة",
                        NotificationText =
                            $"تم استلام إثبات دفع {onlinePaymentTypeLabel} للحجز رقم {booking.BookingId}، وهو الآن قيد المراجعة.",
                        NotificationType =
                            NotificationTypes.PaymentReceived,
                        TargetUrl =
                            $"/Places/BookingDetails/{booking.BookingId}",
                        ReferenceType =
                            NotificationReferenceTypes.Booking,
                        ReferenceId =
                            booking.BookingId
                    });

                if (booking.Place.OwnerId.HasValue)
                {
                    _notificationService.Queue(
                        new CreateNotificationDto
                        {
                            UserId =
                                booking.Place.OwnerId.Value,
                            NotificationTitle =
                                "إثبات دفع جديد",
                            NotificationText =
                                $"رفع العميل إثبات دفع {onlinePaymentTypeLabel} للحجز رقم {booking.BookingId} بقيمة متوقعة {expectedOnlinePaymentAmount:N0} ر.ي. يرجى مراجعة المبلغ في الحوالة قبل الاعتماد.",
                            NotificationType =
                                NotificationTypes.PaymentReceived,
                            TargetUrl =
                                $"/Owner/Reservation/ManageReservations?bookingId={booking.BookingId}",
                            ReferenceType =
                                NotificationReferenceTypes.Booking,
                            ReferenceId =
                                booking.BookingId
                        });
                }

                await _context
                    .SaveChangesAsync();

                await transaction
                    .CommitAsync();

                return Json(new
                {
                    success = true,

                    bookingId =
                        booking.BookingId,

                    bookingState =
                        booking.BookingState,

                    paymentStatus =
                        booking.PaymentStatus,

                    message =
                        $"تم إرسال إثبات دفع {onlinePaymentTypeLabel}، والدفع الآن قيد مراجعة مالك المنشأة.",

                    redirectUrl =
                        Url.Action(
                            "BookingDetails",
                            "Places",
                            new
                            {
                                id =
                                    booking.BookingId
                            })
                });
            }
            catch (Exception ex)
            {
                await transaction
                    .RollbackAsync();

                if (System.IO.File.Exists(
                    absoluteFilePath))
                {
                    try
                    {
                        System.IO.File.Delete(
                            absoluteFilePath);
                    }
                    catch
                    {
                        // تجاهل فشل حذف الملف.
                    }
                }

                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "حدث خطأ أثناء حفظ بيانات الحوالة.",

                        details =
                            ex.InnerException?
                                .Message ??
                            ex.Message
                    });
            }
        }
    }
}
