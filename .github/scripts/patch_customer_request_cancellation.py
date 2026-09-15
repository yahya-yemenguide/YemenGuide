from pathlib import Path


def replace_once(path, old, new, label):
    p = Path(path)
    text = p.read_text(encoding="utf-8-sig")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected 1 match, found {count}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8-sig")


js = "YemenGuide.Web/wwwroot/js/owner/reservation-actions.js"
customer = "YemenGuide.Web/Controllers/BookingsController.cs"

replace_once(
    js,
    '''        noShow: "/Owner/Reservation/NoShowBooking",\n        ownerCancel: "/Owner/Reservation/CancelBookingByOwner"''',
    '''        noShow: "/Owner/Reservation/NoShowBooking",\n        ownerCancel: "/Owner/Reservation/CancelBookingByOwner",\n        customerCancelQuote: "/Owner/Reservation/CustomerCancellationQuote",\n        customerCancel: "/Owner/Reservation/CancelByCustomerRequest"''',
    "endpoints")

replace_once(
    js,
    '''                buildActionButton({\n                    className:\n                        "btn-outline-danger reservation-owner-cancel-btn",\n\n                    icon:\n                        "bi-calendar-x",\n\n                    text:\n                        "إلغاء الحجز من جهة المالك",\n\n                    handler:\n                        `window.executeOwnerCancellation(${bookingId})`,\n\n                    disabled:\n                        !details.canOwnerCancel,\n\n                    title:\n                        details.canOwnerCancel\n                            ? ""\n                            : details.actionNote\n                })\n            ].join("");''',
    '''                buildActionButton({\n                    className:\n                        "btn-outline-danger reservation-owner-cancel-btn",\n\n                    icon:\n                        "bi-calendar-x",\n\n                    text:\n                        "إلغاء الحجز من جهة المالك",\n\n                    handler:\n                        `window.executeOwnerCancellation(${bookingId})`,\n\n                    disabled:\n                        !details.canOwnerCancel,\n\n                    title:\n                        details.canOwnerCancel\n                            ? ""\n                            : details.actionNote\n                }),\n\n                buildActionButton({\n                    className:\n                        "btn-outline-warning reservation-customer-cancel-btn",\n\n                    icon:\n                        "bi-person-x",\n\n                    text:\n                        "إلغاء بطلب العميل",\n\n                    handler:\n                        `window.executeCustomerRequestedCancellation(${bookingId})`\n                })\n            ].join("");''',
    "customer cancel button")

marker = '''    /* =====================================================================\n       المحادثة\n       ===================================================================== */'''
insert = r'''    window.executeCustomerRequestedCancellation =
        async bookingId => {
            if (state.actionRunning) {
                return;
            }

            const id =
                parsePositiveId(
                    bookingId
                );

            if (!id) {
                await showMessage({
                    icon: "error",
                    title: "رقم الحجز غير صحيح"
                });
                return;
            }

            const details =
                state.currentBooking ||
                {};

            showLoading(
                "جاري حساب سياسة الإلغاء",
                "يتم حساب الغرامة والاسترداد حسب سياسة المنشأة الحالية."
            );

            try {
                const response = await fetch(
                    `${ENDPOINTS.customerCancelQuote}?id=${encodeURIComponent(id)}`,
                    {
                        method: "GET",
                        credentials: "same-origin",
                        headers: {
                            Accept: "application/json",
                            "X-Requested-With": "XMLHttpRequest"
                        }
                    }
                );

                const result =
                    await parseJsonResponse(
                        response
                    );

                if (!response.ok || result.success !== true || !result.data) {
                    throw new Error(
                        result.message ||
                        "تعذر حساب سياسة الإلغاء لهذا الحجز."
                    );
                }

                closeSweetAlert();

                const quote =
                    result.data;

                const displayMoney = value => {
                    const rawValue = Math.max(
                        0,
                        Number(value || 0)
                    );

                    const rawTotal = Math.max(
                        0,
                        Number(quote.totalPrice || 0)
                    );

                    const displayedTotal = Math.max(
                        0,
                        Number(details.totalAmountRaw || 0)
                    );

                    const factor =
                        rawTotal > 0 && displayedTotal > 0
                            ? displayedTotal / rawTotal
                            : 1;

                    const converted =
                        rawValue * factor;

                    const currency =
                        window.YGOwnerReservationCurrency || {};

                    const code =
                        currency.code || "YER";

                    const symbol =
                        currency.symbol || "ر.ي";

                    return `${new Intl.NumberFormat("en-US", {
                        minimumFractionDigits: code === "YER" ? 0 : 2,
                        maximumFractionDigits: code === "YER" ? 0 : 2
                    }).format(converted)} ${symbol}`;
                };

                return executeReasonAction({
                    bookingId: id,
                    endpoint:
                        ENDPOINTS.customerCancel,
                    dialog: {
                        icon: "warning",
                        title: "إلغاء بطلب العميل",
                        html: `
                            <div dir="rtl">
                                <p>
                                    استخدم هذا الخيار عندما يتواصل العميل مع المنشأة ويطلب إلغاء الحجز. ستُطبق سياسة الإلغاء الخاصة بالمنشأة كما لو ألغى العميل من حسابه.
                                </p>

                                <div class="reservation-refund-preview">
                                    <span>السياسة المطبقة</span>
                                    <strong>${escapeHtml(quote.policyLabel || "سياسة الإلغاء")}</strong>
                                </div>

                                <div class="reservation-refund-preview">
                                    <span>إجمالي ما دفعه العميل</span>
                                    <strong>${escapeHtml(displayMoney(quote.paidAmount))}</strong>
                                </div>

                                <div class="reservation-refund-preview">
                                    <span>غرامة الإلغاء</span>
                                    <strong>${escapeHtml(displayMoney(quote.cancellationFee))}</strong>
                                </div>

                                <div class="reservation-refund-preview">
                                    <span>المبلغ المستحق للاسترداد</span>
                                    <strong>${escapeHtml(displayMoney(quote.refundAmount))}</strong>
                                </div>

                                <div class="reservation-danger-dialog-note">
                                    <i class="bi bi-info-circle"></i>
                                    <span>
                                        ${escapeHtml(quote.refundTransferFeeLabel || "")} — مدة المعالجة ${escapeHtml(String(quote.refundProcessingDays || 1))} أيام عمل.
                                    </span>
                                </div>

                                <div class="reservation-danger-dialog-note">
                                    <i class="bi bi-exclamation-triangle"></i>
                                    <span>
                                        لا تستخدم «إلغاء الحجز من جهة المالك» لهذه الحالة؛ ذلك الخيار يعني أن المنشأة هي التي ألغت ويلغي غرامة العميل.
                                    </span>
                                </div>
                            </div>
                        `,
                        inputLabel: "سبب طلب العميل للإلغاء",
                        placeholder: "مثال: العميل اتصل وطلب إلغاء الحجز بسبب تغيير موعد السفر.",
                        ariaLabel: "سبب إلغاء الحجز بطلب العميل",
                        confirmText: "تأكيد الإلغاء حسب السياسة",
                        confirmColor: "#F59E0B"
                    },
                    loadingTitle: "جاري إلغاء الحجز",
                    loadingText: "يتم تطبيق سياسة الإلغاء وإنشاء طلب الاسترداد بالقيمة المستحقة.",
                    successTitle: "تم الإلغاء بطلب العميل",
                    fallbackError: "تعذر تنفيذ إلغاء العميل."
                });
            } catch (error) {
                closeSweetAlert();

                await showMessage({
                    icon: "error",
                    title: "تعذر حساب الإلغاء",
                    text:
                        error.message ||
                        "حدث خطأ أثناء حساب سياسة الإلغاء.",
                    confirmButtonColor: "#EF4444",
                    returnFocus: false
                });
            }
        };

'''
replace_once(js, marker, insert + marker, "customer cancel action")

replace_once(
    js,
    '''            const paidOnline =\n                Math.max(\n                    0,\n                    details\n                        .onlineAmountRaw ||\n                    0\n                );''',
    '''            const paidOnline =\n                Math.max(\n                    0,\n                    details\n                        .totalCollectedAmountRaw ||\n                    details\n                        .onlineAmountRaw ||\n                    0\n                );''',
    "owner refund preview")

replace_once(
    customer,
    '''            var hoursUntilStart =\n                Math.Max(\n                    0,\n                    (booking.StartDate - now)\n                        .TotalHours);''',
    '''            var cancellationStartAt =\n                booking.Package?.StartTime.HasValue == true\n                    ? booking.StartDate.Date.Add(booking.Package.StartTime.Value)\n                    : booking.StartDate;\n\n            if (!booking.PackageId.HasValue &&\n                booking.StartDate.TimeOfDay == TimeSpan.Zero &&\n                TimeSpan.TryParse(\n                    policy?.CheckInTime,\n                    out var cancellationCheckInTime))\n            {\n                cancellationStartAt =\n                    booking.StartDate.Date.Add(cancellationCheckInTime);\n            }\n\n            var hoursUntilStart =\n                Math.Max(\n                    0,\n                    (cancellationStartAt - now)\n                        .TotalHours);''',
    "customer operational cancellation start")

print("customer-request cancellation patch applied")
