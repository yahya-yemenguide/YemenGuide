/**
 * ============================================================================
 * YEMENGUIDE — CHECKOUT
 * إنشاء الحجز من بيانات الـ Model فقط. الاستثناء الوحيد هو معرف إعلان اختياري
 * محفوظ مؤقتًا لنسبة الحجز، ويعيد الخادم التحقق منه بالكامل قبل اعتماده.
 * ============================================================================
 */

(() => {
    "use strict";

    const CREATE_ENDPOINT =
        "/BookingFlow/Create";

    const BOOKING_DETAILS_ROUTE =
        "/Places/BookingDetails";

    const PROFILE_FALLBACK_ROUTE =
        "/Profile/CustomerProfile";

    function getCurrentLocalReturnUrl() {
        return (
            window.location.pathname +
            window.location.search +
            window.location.hash
        );
    }

    function buildLoginReturnUrl() {
        const loginUrl =
            new URL(
                "/Account/Login",
                window.location.origin
            );

        loginUrl.searchParams.set(
            "returnUrl",
            getCurrentLocalReturnUrl()
        );

        return (
            loginUrl.pathname +
            loginUrl.search
        );
    }

    const AD_ATTRIBUTION_ID_KEY =
        "ygAdvertisementAttributionId";

    const AD_ATTRIBUTION_PLACE_KEY =
        "ygAdvertisementAttributionPlaceId";

    const state = {
        initialized: false,
        submitting: false,
        completed: false,
        originalButtonHtml: ""
    };

    const getElement = id =>
        document.getElementById(id);

    const getElements = selector =>
        Array.from(
            document.querySelectorAll(selector)
        );

    /* ======================================================================
       01. أدوات قراءة القيم
       ====================================================================== */

    function normalizeDigits(value) {
        const arabicDigits =
            "٠١٢٣٤٥٦٧٨٩";

        const persianDigits =
            "۰۱۲۳۴۵۶۷۸۹";

        return String(value ?? "")
            .replace(
                /[٠-٩]/g,
                digit =>
                    String(
                        arabicDigits.indexOf(digit)
                    )
            )
            .replace(
                /[۰-۹]/g,
                digit =>
                    String(
                        persianDigits.indexOf(digit)
                    )
            );
    }

    function readValue(
        id,
        fallback = ""
    ) {
        const element =
            getElement(id);

        return element
            ? String(
                element.value ?? ""
            ).trim()
            : fallback;
    }

    function toInteger(
        value,
        fallback = 0
    ) {
        const parsed =
            Number.parseInt(
                normalizeDigits(value),
                10
            );

        return Number.isFinite(parsed)
            ? parsed
            : fallback;
    }

    function readInteger(
        id,
        fallback = 0
    ) {
        return toInteger(
            readValue(id),
            fallback
        );
    }

    function readNullableInteger(id) {
        const value =
            readValue(id);

        if (!value) {
            return null;
        }

        const parsed =
            toInteger(
                value,
                0
            );

        return parsed > 0
            ? parsed
            : null;
    }

    function isValidDateOnly(value) {
        const match =
            String(value ?? "")
                .match(
                    /^(\d{4})-(\d{2})-(\d{2})$/
                );

        if (!match) {
            return false;
        }

        const year =
            Number(match[1]);

        const month =
            Number(match[2]);

        const day =
            Number(match[3]);

        const date =
            new Date(
                Date.UTC(
                    year,
                    month - 1,
                    day
                )
            );

        return date.getUTCFullYear() === year &&
            date.getUTCMonth() === month - 1 &&
            date.getUTCDate() === day;
    }

    function getAntiForgeryToken() {
        return document.querySelector(
            'input[name="__RequestVerificationToken"]'
        )?.value || "";
    }

    function readAdditionalServices() {
        const groupedServices =
            new Map();

        getElements(
            ".selected-service-id"
        ).forEach(input => {
            const serviceId =
                toInteger(
                    input.value,
                    0
                );

            const quantity =
                Math.max(
                    1,
                    toInteger(
                        input.dataset.quantity,
                        1
                    )
                );

            if (serviceId <= 0) {
                return;
            }

            groupedServices.set(
                serviceId,
                (
                    groupedServices.get(
                        serviceId
                    ) || 0
                ) + quantity
            );
        });

        return Array.from(
            groupedServices.entries()
        ).map(
            (
                [
                    serviceId,
                    quantity
                ]
            ) => ({
                ServiceId:
                    serviceId,

                Quantity:
                    quantity
            })
        );
    }


    function isBookingProfileComplete() {
        return readValue(
            "bookingProfileCompleteInput",
            "true"
        ).toLowerCase() ===
            "true";
    }

    function readMissingProfileFields() {
        return getElements(
            ".booking-profile-missing-field"
        )
            .map(input =>
                String(
                    input.value ||
                    ""
                ).trim()
            )
            .filter(value =>
                value.length > 0
            );
    }

    function getSafeProfileUrl(
        requestedUrl
    ) {
        const fallbackUrl =
            readValue(
                "bookingProfileUrlInput",
                PROFILE_FALLBACK_ROUTE
            ) ||
            PROFILE_FALLBACK_ROUTE;

        const candidate =
            String(
                requestedUrl ||
                fallbackUrl
            ).trim();

        try {
            const url =
                new URL(
                    candidate,
                    window.location.origin
                );

            if (
                url.origin !==
                window.location.origin
            ) {
                return fallbackUrl;
            }

            return (
                url.pathname +
                url.search +
                url.hash
            );
        } catch {
            return fallbackUrl;
        }
    }

    /* ======================================================================
       02. الرسائل
       ====================================================================== */


    function fallbackBookingDialog(options = {}) {
        return new Promise(resolve => {
            let style = document.getElementById("ygBookingFallbackStyle");
            if (!style) {
                style = document.createElement("style");
                style.id = "ygBookingFallbackStyle";
                style.textContent = ".yg-booking-fallback{position:fixed;inset:0;z-index:2147483000;background:rgba(7,28,44,.46);backdrop-filter:blur(7px);display:grid;place-items:center;padding:18px}.yg-booking-fallback>div{width:min(450px,100%);background:#fff;border-radius:22px;padding:24px;box-shadow:0 28px 80px rgba(9,42,67,.22);direction:rtl}.yg-booking-fallback h3{margin:0 0 8px;color:#17384d;font-size:1.08rem;font-weight:900}.yg-booking-fallback p{margin:0;color:#657c8d;line-height:1.8;white-space:pre-line}.yg-booking-fallback section{display:flex;gap:8px;margin-top:20px}.yg-booking-fallback button{border:0;border-radius:12px;padding:10px 16px;font:inherit;font-weight:850;cursor:pointer}.yg-booking-fallback .ok{background:#00a6da;color:#fff}.yg-booking-fallback .cancel{background:#eef4f7;color:#486173}";
                document.head.appendChild(style);
            }

            const overlay = document.createElement("div");
            overlay.className = "yg-booking-fallback";
            overlay.innerHTML = `<div role="dialog" aria-modal="true"><h3></h3><p></p><section><button class="ok" type="button"></button>${options.showCancelButton ? '<button class="cancel" type="button"></button>' : ''}</section></div>`;
            overlay.querySelector("h3").textContent = options.title || "تنبيه";
            overlay.querySelector("p").textContent = options.text || "";
            const ok = overlay.querySelector(".ok");
            const cancel = overlay.querySelector(".cancel");
            ok.textContent = options.confirmButtonText || "حسنًا";
            if (cancel) cancel.textContent = options.cancelButtonText || "إلغاء";
            const close = value => { overlay.remove(); resolve(value); };
            ok.addEventListener("click", () => close({ isConfirmed: true }));
            cancel?.addEventListener("click", () => close({ isConfirmed: false, isDismissed: true }));
            document.body.appendChild(overlay);
            ok.focus();
        });
    }

    function showMessage(options) {
        if (
            typeof window.Swal !==
            "undefined" &&
            typeof window.Swal.fire ===
            "function"
        ) {
            return window.Swal.fire(
                options
            );
        }

        return fallbackBookingDialog(options);
    }

    function showWarning(
        title,
        text
    ) {
        return showMessage({
            icon: "warning",
            title,
            text,
            confirmButtonText:
                "حسنًا",
            confirmButtonColor:
                "#12b8d4"
        });
    }

    function showError(
        title,
        text
    ) {
        return showMessage({
            icon: "error",
            title,
            text,
            confirmButtonText:
                "حسنًا",
            confirmButtonColor:
                "#12b8d4"
        });
    }


    async function showProfileCompletionPrompt(
        data = {}
    ) {
        const responseFields =
            Array.isArray(
                data.missingFields
            )
                ? data.missingFields
                    .map(field =>
                        String(
                            field ||
                            ""
                        ).trim()
                    )
                    .filter(field =>
                        field.length > 0
                    )
                : [];

        const missingFields =
            responseFields.length > 0
                ? responseFields
                : readMissingProfileFields();

        const details =
            missingFields.length > 0
                ? `البيانات المطلوبة: ${missingFields.join("، ")}.`
                : "أكمل بيانات حسابك المهمة ثم عد لإتمام الحجز.";

        const message =
            data.message
                ? `${data.message} ${details}`
                : `يجب إكمال بيانات حسابك قبل إنشاء الحجز. ${details}`;

        const profileUrl =
            getSafeProfileUrl(
                data.redirectUrl
            );

        if (
            typeof window.Swal !==
            "undefined" &&
            typeof window.Swal.fire ===
            "function"
        ) {
            const result =
                await window.Swal.fire({
                    icon:
                        "warning",

                    title:
                        "أكمل بيانات حسابك",

                    text:
                        message,

                    showCancelButton:
                        true,

                    confirmButtonText:
                        "إكمال البيانات الآن",

                    cancelButtonText:
                        "العودة للمراجعة",

                    confirmButtonColor:
                        "#12b8d4",

                    cancelButtonColor:
                        "#748493",

                    reverseButtons:
                        true
                });

            if (
                result.isConfirmed ===
                true
            ) {
                window.location.assign(
                    profileUrl
                );
            }

            return;
        }

        const fallbackResult =
            await showMessage({
                icon: "warning",
                title: "أكمل بيانات حسابك",
                text: message,
                showCancelButton: true,
                confirmButtonText: "إكمال البيانات الآن",
                cancelButtonText: "العودة للمراجعة"
            });

        if (fallbackResult?.isConfirmed) {
            window.location.assign(profileUrl);
        }
    }

    async function confirmSubmission() {
        const approvalMode =
            readValue(
                "bookingApprovalModeInput",
                "Instant"
            ).toLowerCase();

        const requiresOwnerApproval =
            approvalMode ===
            "manual";

        if (
            typeof window.Swal ===
            "undefined" ||
            typeof window.Swal.fire !==
            "function"
        ) {
            const fallbackResult =
                await showMessage({
                    icon: "question",
                    title: requiresOwnerApproval
                        ? "إرسال طلب الحجز؟"
                        : "تأكيد الحجز؟",
                    text: requiresOwnerApproval
                        ? "سيصل الطلب إلى مالك المنشأة للمراجعة والموافقة."
                        : "سيعيد النظام التحقق من السعر والتوفر قبل إنشاء الحجز.",
                    showCancelButton: true,
                    confirmButtonText: requiresOwnerApproval
                        ? "إرسال الطلب"
                        : "تأكيد الحجز",
                    cancelButtonText: "مراجعة البيانات"
                });

            return fallbackResult?.isConfirmed === true;
        }

        const result =
            await window.Swal.fire({
                icon:
                    "question",

                title:
                    requiresOwnerApproval
                        ? "إرسال طلب الحجز؟"
                        : "تأكيد الحجز؟",

                text:
                    requiresOwnerApproval
                        ? "سيصل الطلب إلى مالك المنشأة للمراجعة والموافقة."
                        : "سيعيد النظام التحقق من السعر والتوفر قبل إنشاء الحجز.",

                showCancelButton:
                    true,

                confirmButtonText:
                    requiresOwnerApproval
                        ? "إرسال الطلب"
                        : "تأكيد الحجز",

                cancelButtonText:
                    "مراجعة البيانات",

                confirmButtonColor:
                    "#12b8d4",

                cancelButtonColor:
                    "#748493",

                reverseButtons:
                    true,

                focusCancel:
                    true
            });

        return result.isConfirmed ===
            true;
    }

    function readAdvertisementAttribution(placeId) {
        try {
            const advertisementId =
                toInteger(
                    sessionStorage.getItem(
                        AD_ATTRIBUTION_ID_KEY
                    ),
                    0
                );

            const storedPlaceId =
                toInteger(
                    sessionStorage.getItem(
                        AD_ATTRIBUTION_PLACE_KEY
                    ),
                    0
                );

            return (
                advertisementId > 0 &&
                storedPlaceId === placeId
            )
                ? advertisementId
                : null;
        } catch {
            return null;
        }
    }

    function clearAdvertisementAttribution() {
        try {
            sessionStorage.removeItem(
                AD_ATTRIBUTION_ID_KEY
            );
            sessionStorage.removeItem(
                AD_ATTRIBUTION_PLACE_KEY
            );
        } catch {
            // لا يؤثر فشل التخزين على الحجز نفسه.
        }
    }

    /* ======================================================================
       03. بناء طلب الحجز
       ====================================================================== */

    function buildBookingRequest() {
        const placeId =
            readInteger(
                "placeIdInput",
                0
            );

        const roomId =
            readNullableInteger(
                "roomIdInput"
            );

        const packageId =
            readNullableInteger(
                "packageIdInput"
            );

        const startDate =
            readValue(
                "startDateInput"
            );

        const endDateValue =
            readValue(
                "endDateInput"
            );

        const quantity =
            Math.max(
                1,
                readInteger(
                    "quantityInput",
                    1
                )
            );

        const adultsCount =
            Math.max(
                0,
                readInteger(
                    "adultsCountInput",
                    0
                )
            );

        const childrenCount =
            Math.max(
                0,
                readInteger(
                    "childrenCountInput",
                    0
                )
            );

        if (placeId <= 0) {
            return {
                valid:
                    false,

                message:
                    "تعذر تحديد المنشأة الخاصة بهذا الحجز."
            };
        }

        if (
            (
                roomId === null &&
                packageId === null
            ) ||
            (
                roomId !== null &&
                packageId !== null
            )
        ) {
            return {
                valid:
                    false,

                message:
                    "بيانات الغرفة أو الباقة غير صحيحة. ارجع إلى الخطوة السابقة وأعد الاختيار."
            };
        }

        if (
            !isValidDateOnly(
                startDate
            )
        ) {
            return {
                valid:
                    false,

                message:
                    "تاريخ الحجز غير موجود أو غير صحيح."
            };
        }

        if (roomId !== null) {
            if (
                !isValidDateOnly(
                    endDateValue
                ) ||
                endDateValue <=
                startDate
            ) {
                return {
                    valid:
                        false,

                    message:
                        "تاريخ الخروج يجب أن يكون بعد تاريخ الدخول."
                };
            }

            if (
                adultsCount +
                childrenCount <
                1
            ) {
                return {
                    valid:
                        false,

                    message:
                        "يجب تحديد شخص واحد على الأقل لحجز الغرفة."
                };
            }
        }

        return {
            valid:
                true,

            payload: {
                PlaceId:
                    placeId,

                RoomId:
                    roomId,

                PackageId:
                    packageId,

                StartDate:
                    startDate,

                EndDate:
                    endDateValue ||
                    null,

                Quantity:
                    quantity,

                AdultsCount:
                    adultsCount,

                ChildrenCount:
                    childrenCount,

                BookingType:
                    readValue(
                        "bookingTypeInput"
                    ) || null,

                SeatingSetup:
                    readValue(
                        "seatingSetupInput"
                    ) || null,

                PromotionId:
                    readNullableInteger(
                        "promotionIdInput"
                    ),

                PromotionCode:
                    readValue(
                        "promotionCodeInput"
                    ) || null,

                AdvertisementId:
                    readAdvertisementAttribution(
                        placeId
                    ),

                AdditionalServices:
                    readAdditionalServices()
            }
        };
    }

    /* ======================================================================
       04. حالة زر التأكيد
       ====================================================================== */

    function setSubmitLoading(
        loading
    ) {
        const button =
            getElement(
                "btnFinalSubmit"
            );

        if (!button) {
            return;
        }

        if (
            !state
                .originalButtonHtml
        ) {
            state.originalButtonHtml =
                button.innerHTML;
        }

        button.disabled =
            loading;

        button.setAttribute(
            "aria-busy",
            loading
                ? "true"
                : "false"
        );

        button.classList.toggle(
            "is-loading",
            loading
        );

        if (loading) {
            button.innerHTML = `
                <span
                    class="spinner-border spinner-border-sm"
                    aria-hidden="true">
                </span>

                <span class="checkout-submit-button-copy">
                    <strong>
                        جارٍ التحقق وإنشاء الحجز...
                    </strong>

                    <small>
                        لا تغلق الصفحة
                    </small>
                </span>
            `;
        } else if (
            state
                .originalButtonHtml
        ) {
            button.innerHTML =
                state
                    .originalButtonHtml;
        }
    }

    /* ======================================================================
       05. الاتصال بالخادم
       ====================================================================== */

    async function parseServerResponse(
        response
    ) {
        const contentType =
            response.headers.get(
                "content-type"
            ) || "";

        if (
            response.redirected &&
            /login|account/i.test(
                response.url
            )
        ) {
            return {
                success:
                    false,

                requiresLogin:
                    true,

                loginUrl:
                    buildLoginReturnUrl(),

                message:
                    "انتهت جلسة تسجيل الدخول. سجل الدخول ثم أعد المحاولة."
            };
        }

        let data = null;

        if (
            contentType.includes(
                "application/json"
            )
        ) {
            try {
                data =
                    await response.json();
            } catch {
                data =
                    null;
            }
        } else {
            const text =
                await response.text();

            try {
                data =
                    JSON.parse(text);
            } catch {
                data =
                    null;
            }
        }

        if (
            response.status === 401
        ) {
            return {
                ...(data &&
                    typeof data === "object"
                    ? data
                    : {}),

                success:
                    false,

                requiresLogin:
                    true,

                loginUrl:
                    buildLoginReturnUrl(),

                message:
                    data?.message ||
                    "يجب تسجيل الدخول بحساب عميل لإكمال هذه العملية."
            };
        }

        if (
            !data ||
            typeof data !==
            "object"
        ) {
            return {
                success:
                    false,

                message:
                    response.ok
                        ? "استجاب الخادم، لكن تعذر قراءة النتيجة."
                        : "تعذر إنشاء الحجز. أعد المحاولة بعد قليل."
            };
        }

        if (!response.ok) {
            return {
                ...data,

                success:
                    false,

                message:
                    data.message ||
                    "تعذر إنشاء الحجز. تحقق من البيانات وحاول مرة أخرى."
            };
        }

        return data;
    }

    async function sendBookingRequest(
        payload
    ) {
        const token =
            getAntiForgeryToken();

        const headers = {
            "Content-Type":
                "application/json",

            "X-Requested-With":
                "XMLHttpRequest"
        };

        if (token) {
            headers
                .RequestVerificationToken =
                token;
        }

        const response =
            await fetch(
                CREATE_ENDPOINT,
                {
                    method:
                        "POST",

                    credentials:
                        "same-origin",

                    cache:
                        "no-store",

                    headers,

                    body:
                        JSON.stringify(
                            payload
                        )
                }
            );

        return parseServerResponse(
            response
        );
    }

    /* ======================================================================
       06. النجاح والانتقال
       ====================================================================== */

    function getSafeRedirectUrl(
        data
    ) {
        const fallbackUrl =
            data?.bookingId
                ? `${BOOKING_DETAILS_ROUTE}?id=${encodeURIComponent(
                    data.bookingId
                )}`
                : "/Places";

        const requestedUrl =
            String(
                data?.redirectUrl ||
                fallbackUrl
            ).trim();

        try {
            const url =
                new URL(
                    requestedUrl,
                    window.location.origin
                );

            if (
                url.origin !==
                window.location.origin
            ) {
                return fallbackUrl;
            }

            return (
                url.pathname +
                url.search +
                url.hash
            );
        } catch {
            return fallbackUrl;
        }
    }

    async function showSuccessAndRedirect(
        data
    ) {
        const requiresPayment =
            data.requiresPayment ===
            true;

        const manualApproval =
            String(
                data.approvalMode ||
                ""
            ).toLowerCase() ===
            "manual";

        const title =
            manualApproval
                ? "تم إرسال طلب الحجز"
                : requiresPayment
                    ? "تم إنشاء الحجز"
                    : "تم تأكيد الحجز";

        const confirmText =
            requiresPayment
                ? "الانتقال إلى الدفع"
                : "عرض تفاصيل الحجز";

        const redirectUrl =
            getSafeRedirectUrl(
                data
            );

        if (
            typeof window.Swal !==
            "undefined" &&
            typeof window.Swal.fire ===
            "function"
        ) {
            await window.Swal.fire({
                icon:
                    "success",

                title,

                text:
                    data.message ||
                    "تم إنشاء الحجز بنجاح.",

                confirmButtonText:
                    confirmText,

                confirmButtonColor:
                    "#12b8d4",

                allowOutsideClick:
                    false,

                allowEscapeKey:
                    false
            });
        } else {
            await showMessage({
                icon: "success",
                title: "تم إنشاء الحجز",
                text: data.message || "تم إنشاء الحجز بنجاح.",
                confirmButtonText: confirmText
            });
        }

        clearAdvertisementAttribution();

        window.location.assign(
            redirectUrl
        );
    }

    /* ======================================================================
       07. تنفيذ التأكيد
       ====================================================================== */

    async function submitBooking() {
        if (
            state.submitting ||
            state.completed
        ) {
            return;
        }

        if (!isBookingProfileComplete()) {
            await showProfileCompletionPrompt();
            return;
        }

        const request =
            buildBookingRequest();

        if (!request.valid) {
            await showWarning(
                "راجع بيانات الحجز",
                request.message
            );

            return;
        }

        const confirmed =
            await confirmSubmission();

        if (!confirmed) {
            return;
        }

        state.submitting =
            true;

        setSubmitLoading(
            true
        );

        try {
            const result =
                await sendBookingRequest(
                    request.payload
                );

            if (
                result.requiresLogin
            ) {
                await showWarning(
                    "تسجيل الدخول مطلوب",
                    result.message
                );

                window.location.assign(
                    buildLoginReturnUrl()
                );

                return;
            }

            if (
                result.requiresProfileCompletion ===
                true
            ) {
                await showProfileCompletionPrompt(
                    result
                );

                return;
            }

            if (
                result.accountUnavailable ===
                true
            ) {
                await showError(
                    "الحساب غير متاح للحجز",
                    result.message ||
                    "تعذر إنشاء الحجز باستخدام هذا الحساب."
                );

                return;
            }

            if (
                result.promotionChanged ===
                true
            ) {
                await showWarning(
                    "تغيّرت حالة العرض",
                    result.message ||
                    "أعيد التحقق من العرض وتغيّرت حالته. سنحدّث السعر قبل المتابعة."
                );

                window.location.reload();
                return;
            }

            if (
                result.success !==
                true
            ) {
                await showError(
                    "تعذر إنشاء الحجز",
                    result.message ||
                    "حدث خطأ أثناء إنشاء الحجز."
                );

                return;
            }

            state.completed =
                true;

            await showSuccessAndRedirect(
                result
            );
        } catch (error) {
            console.error(
                "Checkout submit error:",
                error
            );

            await showError(
                "تعذر الاتصال بالخادم",
                "تحقق من الاتصال ثم أعد المحاولة. لم يتم إنشاء حجز جديد."
            );
        } finally {
            state.submitting =
                false;

            if (!state.completed) {
                setSubmitLoading(
                    false
                );
            }
        }
    }

    /* ======================================================================
       08. تنسيق السياسات
       ====================================================================== */
    /* ======================================================================
   معرض صور الغرفة أو الباقة
   ====================================================================== */

    function initializeImageGallery() {
        const gallery =
            getElement(
                "checkoutGallery"
            );

        if (
            !gallery ||
            gallery.dataset.initialized ===
            "true"
        ) {
            return;
        }

        const mainImage =
            getElement(
                "checkoutGalleryMainImage"
            );

        const counter =
            getElement(
                "checkoutGalleryCounter"
            );

        const stage =
            getElement(
                "checkoutGalleryStage"
            );

        const thumbnailButtons =
            getElements(
                "#checkoutGallery [data-gallery-thumbnail]"
            );

        const images =
            thumbnailButtons
                .map(button => ({
                    source:
                        String(
                            button.dataset
                                .gallerySrc ||
                            ""
                        ).trim(),

                    button
                }))
                .filter(item =>
                    item.source.length > 0
                );

        if (
            !mainImage ||
            images.length === 0
        ) {
            return;
        }

        gallery.dataset.initialized =
            "true";

        gallery.classList.toggle(
            "is-single",
            images.length === 1
        );

        let currentIndex = 0;
        let lastTrigger = null;
        let touchStartX = null;

        function normalizeIndex(index) {
            if (images.length === 0) {
                return 0;
            }

            return (
                index %
                images.length +
                images.length
            ) % images.length;
        }

        function renderImage(index) {
            currentIndex =
                normalizeIndex(index);

            const selected =
                images[currentIndex];

            mainImage.src =
                selected.source;

            mainImage.alt =
                `الصورة ${currentIndex + 1} من ${images.length}`;

            if (counter) {
                counter.textContent =
                    `${currentIndex + 1} / ${images.length}`;
            }

            images.forEach(
                (item, itemIndex) => {
                    const active =
                        itemIndex ===
                        currentIndex;

                    item.button.classList
                        .toggle(
                            "is-active",
                            active
                        );

                    item.button.setAttribute(
                        "aria-current",
                        active
                            ? "true"
                            : "false"
                    );
                }
            );

            selected.button.scrollIntoView({
                behavior:
                    "smooth",

                block:
                    "nearest",

                inline:
                    "center"
            });
        }

        function openGallery(
            index,
            trigger
        ) {
            lastTrigger =
                trigger instanceof
                    HTMLElement
                    ? trigger
                    : null;

            renderImage(index);

            gallery.classList.add(
                "is-open"
            );

            gallery.setAttribute(
                "aria-hidden",
                "false"
            );

            document.body.classList.add(
                "checkout-gallery-open"
            );

            window.setTimeout(
                () => {
                    gallery
                        .querySelector(
                            "[data-gallery-close]"
                        )
                        ?.focus();
                },
                30
            );
        }

        function closeGallery() {
            gallery.classList.remove(
                "is-open"
            );

            gallery.setAttribute(
                "aria-hidden",
                "true"
            );

            document.body.classList.remove(
                "checkout-gallery-open"
            );

            lastTrigger?.focus();
        }

        function showPrevious() {
            renderImage(
                currentIndex - 1
            );
        }

        function showNext() {
            renderImage(
                currentIndex + 1
            );
        }

        getElements(
            ".checkout-gallery-trigger"
        ).forEach(trigger => {
            trigger.addEventListener(
                "click",
                event => {
                    event.preventDefault();

                    openGallery(
                        Number.parseInt(
                            trigger.dataset
                                .galleryIndex ||
                            "0",
                            10
                        ) || 0,
                        trigger
                    );
                }
            );

            if (
                trigger.tagName !==
                "BUTTON"
            ) {
                trigger.addEventListener(
                    "keydown",
                    event => {
                        if (
                            event.key !==
                            "Enter" &&
                            event.key !==
                            " "
                        ) {
                            return;
                        }

                        event.preventDefault();

                        openGallery(
                            Number.parseInt(
                                trigger.dataset
                                    .galleryIndex ||
                                "0",
                                10
                            ) || 0,
                            trigger
                        );
                    }
                );
            }
        });

        gallery
            .querySelectorAll(
                "[data-gallery-close]"
            )
            .forEach(button => {
                button.addEventListener(
                    "click",
                    closeGallery
                );
            });

        gallery
            .querySelector(
                "[data-gallery-previous]"
            )
            ?.addEventListener(
                "click",
                showPrevious
            );

        gallery
            .querySelector(
                "[data-gallery-next]"
            )
            ?.addEventListener(
                "click",
                showNext
            );

        thumbnailButtons.forEach(
            button => {
                button.addEventListener(
                    "click",
                    () => {
                        renderImage(
                            Number.parseInt(
                                button.dataset
                                    .galleryIndex ||
                                "0",
                                10
                            ) || 0
                        );
                    }
                );
            }
        );

        document.addEventListener(
            "keydown",
            event => {
                if (
                    !gallery.classList
                        .contains(
                            "is-open"
                        )
                ) {
                    return;
                }

                if (
                    event.key ===
                    "Escape"
                ) {
                    event.preventDefault();
                    closeGallery();
                    return;
                }

                if (
                    event.key ===
                    "ArrowRight"
                ) {
                    event.preventDefault();
                    showPrevious();
                    return;
                }

                if (
                    event.key ===
                    "ArrowLeft"
                ) {
                    event.preventDefault();
                    showNext();
                }
            }
        );

        stage?.addEventListener(
            "touchstart",
            event => {
                touchStartX =
                    event.changedTouches[0]
                        ?.clientX ??
                    null;
            },
            {
                passive: true
            }
        );

        stage?.addEventListener(
            "touchend",
            event => {
                if (
                    touchStartX ===
                    null
                ) {
                    return;
                }

                const touchEndX =
                    event.changedTouches[0]
                        ?.clientX ??
                    touchStartX;

                const difference =
                    touchEndX -
                    touchStartX;

                touchStartX =
                    null;

                if (
                    Math.abs(
                        difference
                    ) < 45
                ) {
                    return;
                }

                if (
                    difference > 0
                ) {
                    showPrevious();
                } else {
                    showNext();
                }
            },
            {
                passive: true
            }
        );

        let pointerStartX = null;
        let activePointerId = null;

        stage?.addEventListener(
            "pointerdown",
            event => {
                if (
                    event.pointerType !== "mouse" &&
                    event.pointerType !== "pen"
                ) {
                    return;
                }

                if (
                    event.target instanceof Element &&
                    event.target.closest("button")
                ) {
                    return;
                }

                pointerStartX = event.clientX;
                activePointerId = event.pointerId;
                stage.classList.add("is-dragging");

                try {
                    stage.setPointerCapture(event.pointerId);
                } catch {
                    // لا نكسر المعرض إذا لم يدعم المتصفح Pointer Capture.
                }
            }
        );

        stage?.addEventListener(
            "pointerup",
            event => {
                if (
                    activePointerId === null ||
                    event.pointerId !== activePointerId ||
                    pointerStartX === null
                ) {
                    return;
                }

                const difference =
                    event.clientX - pointerStartX;

                pointerStartX = null;
                activePointerId = null;
                stage.classList.remove("is-dragging");

                if (Math.abs(difference) < 55) {
                    return;
                }

                if (difference > 0) {
                    showPrevious();
                } else {
                    showNext();
                }
            }
        );

        stage?.addEventListener(
            "pointercancel",
            () => {
                pointerStartX = null;
                activePointerId = null;
                stage.classList.remove("is-dragging");
            }
        );

        mainImage.addEventListener(
            "error",
            () => {
                mainImage.src =
                    "/images/bg1.jpg";
            }
        );

        renderImage(0);
    }
    /* ======================================================================
       09. العروض — تطبيق الكود وإزالة العرض
       ====================================================================== */

    function initializePromotionControls() {
        const applyButton =
            getElement("btnApplyPromotionCode");

        const removeButton =
            getElement("btnRemovePromotion");

        const codeInput =
            getElement("promotionCodeEntry");

        const applyPromotionCode = async () => {
            const code =
                String(codeInput?.value || "").trim();

            if (!code) {
                await showWarning(
                    "أدخل كود العرض",
                    "اكتب الكود أولًا ثم اضغط تطبيق."
                );

                codeInput?.focus();
                return;
            }

            const promotionId =
                toInteger(
                    applyButton?.dataset?.promotionId ||
                    readValue("requestedPromotionIdInput"),
                    0
                );

            const url =
                new URL(window.location.href);

            if (promotionId > 0) {
                url.searchParams.set(
                    "promotionId",
                    String(promotionId)
                );
            }

            url.searchParams.set(
                "promotionCode",
                code
            );

            applyButton?.classList.add("is-loading");
            if (applyButton) {
                applyButton.disabled = true;
            }

            window.location.assign(
                url.pathname +
                url.search +
                url.hash
            );
        };

        applyButton?.addEventListener(
            "click",
            applyPromotionCode
        );

        codeInput?.addEventListener(
            "keydown",
            event => {
                if (event.key !== "Enter") {
                    return;
                }

                event.preventDefault();
                applyPromotionCode();
            }
        );

        removeButton?.addEventListener(
            "click",
            () => {
                const url =
                    new URL(window.location.href);

                url.searchParams.delete("promotionId");
                url.searchParams.delete("promotionCode");

                window.location.assign(
                    url.pathname +
                    url.search +
                    url.hash
                );
            }
        );
    }

    /* ======================================================================
       10. الحركة الفخمة الهادئة
       ====================================================================== */

    function initializeLuxuryCheckoutExperience() {
        const reduceMotion =
            window.matchMedia(
                "(prefers-reduced-motion: reduce)"
            ).matches;

        const revealItems =
            getElements(".yg-checkout-reveal");

        if (reduceMotion) {
            revealItems.forEach(item =>
                item.classList.add("is-revealed")
            );
            return;
        }

        if ("IntersectionObserver" in window) {
            const observer =
                new IntersectionObserver(
                    entries => {
                        entries.forEach(entry => {
                            if (!entry.isIntersecting) {
                                return;
                            }

                            entry.target.classList.add(
                                "is-revealed"
                            );

                            observer.unobserve(
                                entry.target
                            );
                        });
                    },
                    {
                        threshold: 0.12,
                        rootMargin: "0px 0px -24px 0px"
                    }
                );

            revealItems.forEach(item =>
                observer.observe(item)
            );
        } else {
            revealItems.forEach(item =>
                item.classList.add("is-revealed")
            );
        }

        if (!window.matchMedia("(hover: hover) and (pointer: fine)").matches) {
            return;
        }

        getElements(
            ".checkout-card, .checkout-summary-card, .checkout-promotion-card"
        ).forEach(card => {
            card.addEventListener(
                "pointermove",
                event => {
                    const rect =
                        card.getBoundingClientRect();

                    const x =
                        ((event.clientX - rect.left) /
                            Math.max(1, rect.width)) * 100;

                    const y =
                        ((event.clientY - rect.top) /
                            Math.max(1, rect.height)) * 100;

                    card.style.setProperty(
                        "--checkout-pointer-x",
                        `${x}%`
                    );

                    card.style.setProperty(
                        "--checkout-pointer-y",
                        `${y}%`
                    );
                },
                { passive: true }
            );
        });
    }


    function initializePoliciesAccordion() {
        const policies =
            getElements(
                ".checkout-policy-item"
            );

        policies.forEach(policy => {
            policy.addEventListener(
                "toggle",
                () => {
                    if (!policy.open) {
                        return;
                    }

                    policies.forEach(
                        otherPolicy => {
                            if (
                                otherPolicy !==
                                policy
                            ) {
                                otherPolicy.open =
                                    false;
                            }
                        }
                    );
                }
            );
        });
    }

    /* ======================================================================
       09. التهيئة
       ====================================================================== */

    function initializeCheckout() {
        if (state.initialized) {
            return;
        }

        state.initialized =
            true;

        const submitButton =
            getElement(
                "btnFinalSubmit"
            );

        if (!submitButton) {
            console.error(
                "زر تأكيد الحجز غير موجود: #btnFinalSubmit"
            );

            return;
        }

        state.originalButtonHtml =
            submitButton.innerHTML;

        submitButton.addEventListener(
            "click",
            submitBooking
        );

        initializeImageGallery();
        initializePromotionControls();
        initializePoliciesAccordion();
        initializeLuxuryCheckoutExperience();
    }

    if (
        document.readyState ===
        "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            initializeCheckout,
            {
                once: true
            }
        );
    } else {
        initializeCheckout();
    }
})();