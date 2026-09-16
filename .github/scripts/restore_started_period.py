from pathlib import Path


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8-sig")


def write(path: str, text: str) -> None:
    Path(path).write_text(text, encoding="utf-8-sig")


def replace_once(path: str, old: str, new: str, label: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise SystemExit(
            f"{label}: expected exactly one match in {path}, found {count}"
        )
    write(path, text.replace(old, new, 1))


# -----------------------------------------------------------------------------
# 1) Customer availability API: allow a package that already started but has
#    not ended, and return elapsed/remaining minutes for the warning.
# -----------------------------------------------------------------------------
places_path = "YemenGuide.Web/Controllers/PlacesController.cs"

places_notice_old = '''            /*
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
'''

places_notice_new = '''            var requestedPeriod =
                GetPackageOperationalInterval(
                    requestedDay,
                    selectedPackage);

            /*
             * الفترة التي انتهت لا يمكن حجزها.
             * إذا بدأت وما زالت جارية نسمح للعميل بالمتابعة بعد تنبيه صريح،
             * وشرط الحجز المسبق يبقى مطبقًا فقط قبل بدء الفترة.
             */
            if (referenceTime >=
                requestedPeriod.End)
            {
                return (
                    false,
                    "انتهت هذه الفترة بالفعل. اختر فترة أخرى متاحة.");
            }

            var periodAlreadyStarted =
                referenceTime >=
                requestedPeriod.Start;

            var minimumNoticeHours =
                Math.Max(
                    0,
                    place.PlacePolicy?
                        .MinimumNoticeHours ?? 0);

            if (!periodAlreadyStarted &&
                minimumNoticeHours > 0)
            {
                var requestedStartAt =
                    requestedPeriod.Start;

                if (requestedStartAt <
                    referenceTime.AddHours(
                        minimumNoticeHours))
                {
                    return (
                        false,
                        $"يجب إجراء الحجز قبل الموعد بـ {minimumNoticeHours} ساعة على الأقل.");
                }
            }
'''

replace_once(
    places_path,
    places_notice_old,
    places_notice_new,
    "places minimum notice",
)

places_endpoint_old = '''            var availability =
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
'''

places_endpoint_new = '''            var now =
                DateTime.Now;

            var availability =
                await CheckPackageDateAvailabilityCoreAsync(
                    place,
                    selectedPackage,
                    date,
                    now);

            var operationalPeriod =
                GetPackageOperationalInterval(
                    date,
                    selectedPackage);

            var started =
                availability.Available &&
                now >= operationalPeriod.Start &&
                now < operationalPeriod.End;

            var elapsedMinutes =
                started
                    ? Math.Max(
                        0,
                        (int)Math.Floor(
                            (now - operationalPeriod.Start)
                                .TotalMinutes))
                    : 0;

            var remainingMinutes =
                started
                    ? Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (operationalPeriod.End - now)
                                .TotalMinutes))
                    : 0;

            return Json(new
            {
                available =
                    availability.Available,

                message =
                    availability.Message,

                date =
                    date.Date.ToString(
                        "yyyy-MM-dd"),

                started,
                elapsedMinutes,
                remainingMinutes,

                operationalStartAt =
                    operationalPeriod.Start.ToString("O"),

                operationalEndAt =
                    operationalPeriod.End.ToString("O")
            });
'''

replace_once(
    places_path,
    places_endpoint_old,
    places_endpoint_new,
    "places availability response",
)


# -----------------------------------------------------------------------------
# 2) Customer UI: explicit warning with elapsed + remaining time and consent.
# -----------------------------------------------------------------------------
details_path = "YemenGuide.Web/wwwroot/js/customer/Details.js"

details_old = '''            if (
                !response.ok ||
                data?.available !== true
            ) {
                await showPackageAvailabilityAlert(
                    data?.message ||
                    "تعذر تأكيد توفر هذا الموعد. اختر يومًا آخر أو أعد المحاولة."
                );

                return false;
            }

            return true;
'''

details_new = '''            if (
                !response.ok ||
                data?.available !== true
            ) {
                await showPackageAvailabilityAlert(
                    data?.message ||
                    "تعذر تأكيد توفر هذا الموعد. اختر يومًا آخر أو أعد المحاولة."
                );

                return false;
            }

            if (data?.started === true) {
                const formatDuration =
                    rawMinutes => {
                        const totalMinutes =
                            Math.max(
                                0,
                                Number.parseInt(
                                    String(
                                        rawMinutes ||
                                        "0"
                                    ),
                                    10
                                ) || 0
                            );

                        const hours =
                            Math.floor(
                                totalMinutes / 60
                            );

                        const minutes =
                            totalMinutes % 60;

                        if (
                            hours > 0 &&
                            minutes > 0
                        ) {
                            return `${hours} ساعة و${minutes} دقيقة`;
                        }

                        if (hours > 0) {
                            return `${hours} ساعة`;
                        }

                        return `${Math.max(1, minutes)} دقيقة`;
                    };

                const elapsedText =
                    formatDuration(
                        data.elapsedMinutes
                    );

                const remainingText =
                    formatDuration(
                        data.remainingMinutes
                    );

                const result =
                    await notify({
                        icon:
                            "warning",

                        title:
                            "هذه الفترة بدأت بالفعل",

                        text:
                            `مضى من الفترة ${elapsedText}، والمتبقي ${remainingText}. إذا تابعت فسيظهر لمالك المنشأة أنك أكملت الحجز بعد علمك بأن الفترة قد بدأت.`,

                        showCancelButton:
                            true,

                        confirmButtonText:
                            "أنا على علم وأريد المتابعة",

                        cancelButtonText:
                            "الرجوع",

                        confirmButtonColor:
                            "#12b8d4",

                        cancelButtonColor:
                            "#748493",

                        reverseButtons:
                            true,

                        allowOutsideClick:
                            false
                    });

                if (
                    !result ||
                    result.isConfirmed !== true
                ) {
                    reopenPackageDatePicker();
                    return false;
                }
            }

            return true;
'''

replace_once(
    details_path,
    details_old,
    details_new,
    "customer started-period warning",
)


# -----------------------------------------------------------------------------
# 3) Final server validation: same rule as the availability API. Packages that
#    started but are still running may continue; ended packages are rejected.
#    Room bookings are unchanged.
# -----------------------------------------------------------------------------
flow_path = "YemenGuide.Web/Controllers/BookingFlowController.cs"

flow_old = '''        private static string?
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
'''

flow_new = '''        private static string?
            ValidateMinimumNotice(
                Place place,
                DateTime startDate,
                PlacePackage? package,
                DateTime now)
        {
            /*
             * الاستثناء خاص بالباقات ذات الفترة الزمنية:
             * إذا بدأت الفترة ولم تنتهِ نسمح بإكمال الحجز بعد تنبيه العميل.
             * الغرف تبقى خاضعة لشرط الحجز المسبق كما كانت.
             */
            if (package != null)
            {
                var operationalPeriod =
                    GetPackageOperationalInterval(
                        startDate,
                        package);

                if (now >=
                    operationalPeriod.End)
                {
                    return
                        "انتهت هذه الفترة بالفعل. اختر فترة أخرى متاحة.";
                }

                if (now >=
                    operationalPeriod.Start)
                {
                    return null;
                }
            }

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
'''

replace_once(
    flow_path,
    flow_old,
    flow_new,
    "booking-flow minimum notice",
)


# -----------------------------------------------------------------------------
# 4) Owner UI: show that a customer created the package booking after its
#    operational start, including elapsed and remaining time at creation.
# -----------------------------------------------------------------------------
def patch_owner_view(path: str, active: bool) -> None:
    text = read(path)

    short_date = '''    private static string GetShortDate(DateTime date) => date.ToString("yyyy-MM-dd");
'''
    helper = '''    private static string GetShortDate(DateTime date) => date.ToString("yyyy-MM-dd");

    private static string GetDurationText(TimeSpan duration)
    {
        var totalMinutes = Math.Max(0, (int)Math.Floor(duration.TotalMinutes));
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours > 0 && minutes > 0) return $"{hours} ساعة و{minutes} دقيقة";
        if (hours > 0) return $"{hours} ساعة";
        return $"{Math.Max(1, minutes)} دقيقة";
    }
'''
    if text.count(short_date) != 1:
        raise SystemExit(
            f"owner duration helper: expected one GetShortDate in {path}"
        )
    text = text.replace(short_date, helper, 1)

    promotion_line = '''                        var promotionLabel = CurrencyLabel(booking.PromotionDiscountLabel);
'''
    started_vars = '''                        var promotionLabel = CurrencyLabel(booking.PromotionDiscountLabel);
                        var startedPeriodBooking =
                            booking.HasPackage &&
                            booking.CreatedAt.HasValue &&
                            booking.CreatedAt.Value >= booking.OperationalStartAt &&
                            booking.CreatedAt.Value < booking.OperationalEndAt;
                        var elapsedAtBookingText = startedPeriodBooking
                            ? GetDurationText(booking.CreatedAt!.Value - booking.OperationalStartAt)
                            : string.Empty;
                        var remainingAtBookingText = startedPeriodBooking
                            ? GetDurationText(booking.OperationalEndAt - booking.CreatedAt!.Value)
                            : string.Empty;
'''
    if text.count(promotion_line) != 1:
        raise SystemExit(
            f"owner started vars: expected one promotionLabel in {path}"
        )
    text = text.replace(promotion_line, started_vars, 1)

    if active:
        period_line = '''                                <div class="compact-period-note"><i class="bi bi-clock"></i> @booking.OperationalStartAt.ToString("HH:mm") — @booking.OperationalEndAt.ToString("yyyy-MM-dd HH:mm")</div>
'''
    else:
        period_line = '''                                <div class="compact-period-note"><i class="bi bi-clock"></i> البداية التشغيلية: @booking.OperationalStartAt.ToString("yyyy-MM-dd HH:mm")</div>
'''

    awareness = period_line + '''                                @if (startedPeriodBooking)
                                {
                                    <div class="compact-period-note started-period-awareness-note">
                                        <i class="bi bi-exclamation-triangle-fill text-warning"></i>
                                        <strong>العميل على علم أن الفترة بدأت</strong>
                                        <span>— مضى @elapsedAtBookingText، وكان متبقيًا @remainingAtBookingText وقت إنشاء الحجز.</span>
                                    </div>
                                }
'''
    if text.count(period_line) != 1:
        raise SystemExit(
            f"owner awareness note: expected one period line in {path}"
        )
    text = text.replace(period_line, awareness, 1)

    write(path, text)


patch_owner_view(
    "YemenGuide.Web/Areas/Owner/Views/Reservation/_PendingRequests.cshtml",
    active=False,
)
patch_owner_view(
    "YemenGuide.Web/Areas/Owner/Views/Reservation/_ActiveReservations.cshtml",
    active=True,
)

print("Started-period booking patch applied successfully.")
