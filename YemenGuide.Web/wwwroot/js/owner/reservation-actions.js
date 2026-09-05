/**
 * =============================================================================
 * دليلك اليمني — إدارة حجوزات المالك
 * File: wwwroot/js/owner/reservation-actions.js
 * =============================================================================
 */
(() => {
    "use strict";

    const TAB_ENDPOINTS = Object.freeze({
        "pending-requests-tab": "/Owner/Reservation/PendingRequests",
        "active-reservations-tab": "/Owner/Reservation/ActiveReservations",
        "reservation-history-tab": "/Owner/Reservation/ReservationHistory",
        "calendar-manager-tab": "/Owner/Reservation/CalendarManager"
    });

    const ENDPOINTS = Object.freeze({
        accept: "/Owner/Reservation/AcceptBooking",
        rejectPending: "/Owner/Reservation/RejectBooking",
        approvePayment: "/Owner/Reservation/ApprovePayment",
        rejectPayment: "/Owner/Reservation/RejectPayment",
        rejectPermanently:
            "/Owner/Reservation/RejectBookingPermanently",
        complete: "/Owner/Reservation/CompleteBooking",
        noShow: "/Owner/Reservation/NoShowBooking",
        ownerCancel: "/Owner/Reservation/CancelBookingByOwner"
    });

    const DEFAULT_TAB =
        "pending-requests-tab";

    const BOOKING_STATES = Object.freeze({
        Pending: {
            label: "يحتاج موافقة",
            icon: "bi-hourglass-split",
            className: "badge-glass-warning"
        },

        AwaitingPayment: {
            label: "بانتظار دفع العميل",
            icon: "bi-wallet2",
            className: "badge-glass-info"
        },

        PaymentReview: {
            label: "مراجعة الحوالة",
            icon: "bi-file-earmark-check",
            className: "badge-glass-info"
        },

        Confirmed: {
            label: "حجز مؤكد",
            icon: "bi-calendar-check-fill",
            className: "badge-glass-success"
        },

        Completed: {
            label: "حجز مكتمل",
            icon: "bi-check2-circle",
            className: "badge-glass-success"
        },

        Rejected: {
            label: "طلب مرفوض",
            icon: "bi-x-octagon",
            className: "badge-glass-danger"
        },

        Cancelled: {
            label: "حجز ملغي",
            icon: "bi-slash-circle",
            className: "badge-glass-warning"
        },

        NoShow: {
            label: "عدم حضور",
            icon: "bi-person-x",
            className: "badge-glass-danger"
        },

        Expired: {
            label: "انتهت المهلة",
            icon: "bi-hourglass-bottom",
            className: "badge-glass-warning"
        }
    });

    const PAYMENT_STATES = Object.freeze({
        Unpaid: {
            label: "غير مدفوع",
            icon: "bi-wallet2",
            className: "badge-glass-warning"
        },

        UnderReview: {
            label: "الإشعار قيد المراجعة",
            icon: "bi-file-earmark-check",
            className: "badge-glass-info"
        },

        PartiallyPaid: {
            label: "مدفوع جزئيًا",
            icon: "bi-pie-chart-fill",
            className: "badge-glass-info"
        },

        Paid: {
            label: "مدفوع بالكامل",
            icon: "bi-check2-circle",
            className: "badge-glass-success"
        },

        FullyPaid: {
            label: "مدفوع بالكامل",
            icon: "bi-check2-circle",
            className: "badge-glass-success"
        },

        Expired: {
            label: "انتهت مهلة الدفع",
            icon: "bi-hourglass-bottom",
            className: "badge-glass-danger"
        },

        Refunded: {
            label: "تم الاسترداد",
            icon: "bi-arrow-counterclockwise",
            className: "badge-glass-info"
        },

        RefundPending: {
            label: "الاسترداد قيد المعالجة",
            icon: "bi-arrow-counterclockwise",
            className: "badge-glass-info"
        },

        Cancelled: {
            label: "تم إلغاء الدفع",
            icon: "bi-x-circle",
            className: "badge-glass-warning"
        },

        Rejected: {
            label: "إشعار الدفع مرفوض",
            icon: "bi-file-earmark-x",
            className: "badge-glass-danger"
        }
    });

    const state = {
        activeTab: DEFAULT_TAB,
        tabRequestController: null,
        actionRunning: false,
        currentBooking: null,
        initialized: false
    };

    const byId = id =>
        document.getElementById(id);

    const all = (
        selector,
        root = document
    ) =>
        Array.from(
            root.querySelectorAll(selector)
        );

    /* =====================================================================
       أدوات عامة
       ===================================================================== */

    function hasSweetAlert() {
        return Boolean(
            window.Swal &&
            typeof window.Swal.fire ===
            "function"
        );
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

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
                        arabicDigits.indexOf(
                            digit
                        )
                    )
            )
            .replace(
                /[۰-۹]/g,
                digit =>
                    String(
                        persianDigits.indexOf(
                            digit
                        )
                    )
            );
    }

    function parseInteger(
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

    function parsePositiveId(value) {
        const parsed =
            parseInteger(
                value,
                0
            );

        return parsed > 0
            ? parsed
            : 0;
    }

    function parseNumber(
        value,
        fallback = 0
    ) {
        let text =
            normalizeDigits(value)
                .trim()
                .replace(/\s/g, "")
                .replace(/٬/g, ",")
                .replace(/٫/g, ".")
                .replace(
                    /[^0-9,.-]/g,
                    ""
                );

        if (!text) {
            return fallback;
        }

        const lastComma =
            text.lastIndexOf(",");

        const lastDot =
            text.lastIndexOf(".");

        if (
            lastComma >= 0 &&
            lastDot >= 0
        ) {
            text =
                lastComma > lastDot
                    ? text
                        .replace(/\./g, "")
                        .replace(",", ".")
                    : text
                        .replace(/,/g, "");
        } else if (lastComma >= 0) {
            const parts =
                text.split(",");

            text =
                parts.length === 2 &&
                    parts[1].length <= 2
                    ? `${parts[0]}.${parts[1]}`
                    : text.replace(/,/g, "");
        }

        const parsed =
            Number.parseFloat(text);

        return Number.isFinite(parsed)
            ? parsed
            : fallback;
    }

    function formatMoney(value) {
        return Math.round(
            Math.max(
                0,
                parseNumber(
                    value,
                    0
                )
            )
        ).toLocaleString("en-US");
    }

    function readAttribute(
        element,
        name,
        fallback = ""
    ) {
        const value =
            element?.getAttribute(name);

        if (
            value === null ||
            value === undefined ||
            value === "null" ||
            value === "undefined"
        ) {
            return fallback;
        }

        return String(value)
            .trim() ||
            fallback;
    }

    function readBooleanAttribute(
        element,
        name,
        fallback = false
    ) {
        const value =
            readAttribute(
                element,
                name,
                fallback
                    ? "true"
                    : "false"
            )
                .toLowerCase();

        return (
            value === "true" ||
            value === "1"
        );
    }

    function setText(
        id,
        value,
        fallback = "--"
    ) {
        const element =
            byId(id);

        if (!element) {
            return;
        }

        const cleaned =
            value !== null &&
                value !== undefined
                ? String(value)
                    .trim()
                : "";

        element.textContent =
            cleaned ||
            fallback;
    }

    function setVisible(
        elementOrId,
        visible,
        displayValue = "block"
    ) {
        const element =
            typeof elementOrId ===
                "string"
                ? byId(elementOrId)
                : elementOrId;

        if (!element) {
            return;
        }

        element.style.display =
            visible
                ? displayValue
                : "none";
    }

    function formatDateTime(
        value,
        fallback = "--"
    ) {
        if (!value) {
            return fallback;
        }

        const date =
            new Date(value);

        if (
            Number.isNaN(
                date.getTime()
            )
        ) {
            return String(value);
        }

        return new Intl.DateTimeFormat(
            "ar-YE",
            {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit"
            }
        ).format(date);
    }

    function formatPaymentTimer(value) {
        if (!value) {
            return "غير مطلوبة";
        }

        const expiresAt =
            new Date(value);

        if (
            Number.isNaN(
                expiresAt.getTime()
            )
        ) {
            return "غير محددة";
        }

        const remaining =
            expiresAt.getTime() -
            Date.now();

        if (remaining <= 0) {
            return "انتهت مهلة الدفع";
        }

        const totalMinutes =
            Math.floor(
                remaining /
                60000
            );

        const days =
            Math.floor(
                totalMinutes /
                1440
            );

        const hours =
            Math.floor(
                (
                    totalMinutes %
                    1440
                ) / 60
            );

        const minutes =
            totalMinutes %
            60;

        if (days > 0) {
            return hours > 0
                ? `متبقي ${days} يوم و${hours} ساعة`
                : `متبقي ${days} يوم`;
        }

        if (hours > 0) {
            return minutes > 0
                ? `متبقي ${hours} ساعة و${minutes} دقيقة`
                : `متبقي ${hours} ساعة`;
        }

        return `متبقي ${Math.max(
            1,
            minutes
        )} دقيقة`;
    }

    function normalizeImagePath(path) {
        if (
            !path ||
            path === "null" ||
            path === "undefined"
        ) {
            return null;
        }

        let value =
            String(path)
                .trim();

        if (
            !value ||
            /^javascript:/i.test(value) ||
            value.includes("..")
        ) {
            return null;
        }

        if (
            /^https?:\/\//i.test(
                value
            ) ||
            /^data:/i.test(value)
        ) {
            return value;
        }

        if (
            value.startsWith(
                "wwwroot/"
            )
        ) {
            value =
                value.slice(
                    "wwwroot/".length
                );
        }

        if (
            value.startsWith("~/")
        ) {
            value =
                value.slice(2);
        }

        return `/${value.replace(
            /^\/+/,
            ""
        )}`;
    }

    /* =====================================================================
       الرسائل والاتصال بالسيرفر
       ===================================================================== */

    function showMessage(options) {
        if (hasSweetAlert()) {
            return window.Swal.fire(
                options
            );
        }

        const message =
            options.text ||
            options.title ||
            "حدث تنبيه.";

        window.alert(message);

        return Promise.resolve({
            isConfirmed: true,
            value: true
        });
    }

    function showLoading(
        title,
        text
    ) {
        if (!hasSweetAlert()) {
            return;
        }

        window.Swal.fire({
            title,
            text,

            allowOutsideClick:
                false,

            allowEscapeKey:
                false,

            showConfirmButton:
                false,

            returnFocus:
                false,

            didOpen:
                () =>
                    window.Swal
                        .showLoading()
        });
    }

    function closeSweetAlert() {
        if (
            hasSweetAlert() &&
            typeof window.Swal.close ===
            "function"
        ) {
            window.Swal.close();
        }
    }

    function getAntiForgeryToken() {
        return document.querySelector(
            '#ownerReservationAntiForgeryForm input[name="__RequestVerificationToken"], input[name="__RequestVerificationToken"]'
        )?.value || "";
    }

    async function parseJsonResponse(
        response
    ) {
        if (
            response.redirected &&
            /login|account/i.test(
                response.url
            )
        ) {
            throw new Error(
                "انتهت جلسة الدخول. أعد تسجيل الدخول ثم حاول مرة أخرى."
            );
        }

        const text =
            await response.text();

        let data = {};

        try {
            data =
                text
                    ? JSON.parse(text)
                    : {};
        } catch (error) {
            console.error(
                "Invalid JSON response:",
                text,
                error
            );

            if (
                response.status ===
                401 ||
                response.status ===
                403
            ) {
                throw new Error(
                    "انتهت جلسة الدخول أو لا توجد صلاحية لتنفيذ الإجراء."
                );
            }

            throw new Error(
                "استجاب السيرفر بصيغة غير متوقعة."
            );
        }

        if (!response.ok) {
            throw new Error(
                data?.message ||
                "تعذر تنفيذ العملية على السيرفر."
            );
        }

        return data || {};
    }

    async function postForm(
        endpoint,
        values = {}
    ) {
        const formData =
            new FormData();

        Object.entries(
            values
        ).forEach(
            ([key, value]) => {
                if (
                    value !== undefined &&
                    value !== null
                ) {
                    formData.append(
                        key,
                        String(value)
                    );
                }
            }
        );

        const token =
            getAntiForgeryToken();

        if (token) {
            formData.append(
                "__RequestVerificationToken",
                token
            );
        }

        const response =
            await fetch(
                endpoint,
                {
                    method:
                        "POST",

                    body:
                        formData,

                    credentials:
                        "same-origin",

                    headers: {
                        Accept:
                            "application/json",

                        "X-Requested-With":
                            "XMLHttpRequest"
                    }
                }
            );

        return parseJsonResponse(
            response
        );
    }

    /* =====================================================================
       مودال Bootstrap
       ===================================================================== */

    function getReservationModalElement() {
        return byId(
            "premiumCenterModal"
        );
    }

    function cleanupReservationModalState() {
        if (
            document.querySelector(
                ".modal.show"
            )
        ) {
            return;
        }

        all(
            ".modal-backdrop"
        ).forEach(
            backdrop =>
                backdrop.remove()
        );

        document.body
            .classList
            .remove(
                "modal-open"
            );

        document.body.style
            .removeProperty(
                "overflow"
            );

        document.body.style
            .removeProperty(
                "padding-right"
            );

        document.documentElement
            .style
            .removeProperty(
                "overflow"
            );
    }

    function closeReservationModal() {
        return new Promise(
            resolve => {
                const modalElement =
                    getReservationModalElement();

                if (!modalElement) {
                    cleanupReservationModalState();
                    resolve();
                    return;
                }

                const isShown =
                    modalElement
                        .classList
                        .contains(
                            "show"
                        ) ||
                    modalElement
                        .getAttribute(
                            "aria-hidden"
                        ) ===
                    "false";

                if (
                    !isShown ||
                    typeof window.bootstrap ===
                    "undefined" ||
                    !window.bootstrap.Modal
                ) {
                    cleanupReservationModalState();
                    resolve();
                    return;
                }

                let completed =
                    false;

                const finish =
                    () => {
                        if (completed) {
                            return;
                        }

                        completed =
                            true;

                        modalElement
                            .removeEventListener(
                                "hidden.bs.modal",
                                finish
                            );

                        window.setTimeout(
                            () => {
                                cleanupReservationModalState();
                                resolve();
                            },
                            50
                        );
                    };

                modalElement
                    .addEventListener(
                        "hidden.bs.modal",
                        finish,
                        {
                            once: true
                        }
                    );

                window.bootstrap.Modal
                    .getOrCreateInstance(
                        modalElement
                    )
                    .hide();

                window.setTimeout(
                    finish,
                    900
                );
            }
        );
    }

    /* =====================================================================
       تجربة العروض ثلاثية الأبعاد
       ===================================================================== */

    function initializePromotionExperience(
        root = document
    ) {
        if (
            window.matchMedia?.(
                "(prefers-reduced-motion: reduce)"
            ).matches ||
            window.matchMedia?.(
                "(pointer: coarse)"
            ).matches
        ) {
            return;
        }

        const candidates = [];

        if (
            root instanceof Element &&
            root.matches(
                "[data-promo-tilt]"
            )
        ) {
            candidates.push(root);
        }

        all(
            "[data-promo-tilt]",
            root
        ).forEach(element => {
            if (
                !candidates.includes(
                    element
                )
            ) {
                candidates.push(
                    element
                );
            }
        });

        candidates.forEach(card => {
            if (
                card.dataset
                    .promoTiltBound ===
                "true"
            ) {
                return;
            }

            card.dataset
                .promoTiltBound =
                "true";

            let frame = 0;

            const reset = () => {
                card.style
                    .setProperty(
                        "--promo-tilt-x",
                        "0deg"
                    );

                card.style
                    .setProperty(
                        "--promo-tilt-y",
                        "0deg"
                    );

                card.style
                    .setProperty(
                        "--promo-light-x",
                        "50%"
                    );

                card.style
                    .setProperty(
                        "--promo-light-y",
                        "50%"
                    );
            };

            card.addEventListener(
                "pointermove",
                event => {
                    const rect =
                        card
                            .getBoundingClientRect();

                    if (
                        rect.width <= 0 ||
                        rect.height <= 0
                    ) {
                        return;
                    }

                    const x =
                        (
                            event.clientX -
                            rect.left
                        ) / rect.width;

                    const y =
                        (
                            event.clientY -
                            rect.top
                        ) / rect.height;

                    const rotateY =
                        (
                            x - 0.5
                        ) * 7;

                    const rotateX =
                        (
                            0.5 - y
                        ) * 6;

                    if (frame) {
                        window
                            .cancelAnimationFrame(
                                frame
                            );
                    }

                    frame =
                        window
                            .requestAnimationFrame(
                                () => {
                                    card.style
                                        .setProperty(
                                            "--promo-tilt-x",
                                            `${rotateX.toFixed(
                                                2
                                            )}deg`
                                        );

                                    card.style
                                        .setProperty(
                                            "--promo-tilt-y",
                                            `${rotateY.toFixed(
                                                2
                                            )}deg`
                                        );

                                    card.style
                                        .setProperty(
                                            "--promo-light-x",
                                            `${(
                                                x *
                                                100
                                            ).toFixed(
                                                1
                                            )}%`
                                        );

                                    card.style
                                        .setProperty(
                                            "--promo-light-y",
                                            `${(
                                                y *
                                                100
                                            ).toFixed(
                                                1
                                            )}%`
                                        );
                                }
                            );
                }
            );

            card.addEventListener(
                "pointerleave",
                reset
            );

            card.addEventListener(
                "blur",
                reset,
                true
            );
        });
    }

    /* =====================================================================
       التبويبات وتحميل المحتوى
       ===================================================================== */

    function getValidTab(tabName) {
        return Object.prototype
            .hasOwnProperty
            .call(
                TAB_ENDPOINTS,
                tabName
            )
            ? tabName
            : DEFAULT_TAB;
    }

    function getTabElement(tabName) {
        return document.querySelector(
            `.${tabName}`
        );
    }

    function setActiveTabVisual(tabName) {
        all(
            ".custom-tabs-container .tab-btn"
        ).forEach(
            button => {
                const active =
                    button.dataset.target ===
                    tabName;

                button.classList
                    .toggle(
                        "active",
                        active
                    );

                button.setAttribute(
                    "aria-selected",
                    active
                        ? "true"
                        : "false"
                );

                button.tabIndex =
                    active
                        ? 0
                        : -1;
            }
        );

        all(
            ".tab-pane-content"
        ).forEach(
            panel => {
                const active =
                    panel.classList
                        .contains(
                            tabName
                        );

                panel.style.display =
                    active
                        ? "block"
                        : "none";

                panel.classList
                    .toggle(
                        "show-smooth",
                        active
                    );

                panel.setAttribute(
                    "aria-hidden",
                    active
                        ? "false"
                        : "true"
                );
            }
        );
    }

    function getTabLoadingMarkup() {
        return `
            <div class="reservation-initial-loader">

                <div class="reservation-loader-ring">
                    <span></span>
                </div>

                <h6>
                    جاري تحديث بيانات الحجوزات
                </h6>

                <p>
                    يرجى الانتظار لحين مزامنة السجلات.
                </p>

            </div>
        `;
    }

    function getTabErrorMarkup(
        message,
        tabName
    ) {
        return `
            <div class="premium-empty-state compact-empty-state">

                <div class="empty-state-icon">
                    <i class="bi bi-wifi-off"></i>
                </div>

                <h5 class="fw-black text-danger mb-2">
                    تعذر تحميل البيانات
                </h5>

                <p class="text-muted mb-3">
                    ${escapeHtml(message)}
                </p>

                <button type="button"
                        class="reservation-refresh-btn"
                        data-retry-tab="${escapeHtml(
            tabName
        )}">

                    <i class="bi bi-arrow-clockwise"></i>

                    <span>
                        إعادة المحاولة
                    </span>

                </button>

            </div>
        `;
    }

    async function loadReservationTab(
        requestedTab,
        options = {}
    ) {
        const tabName =
            getValidTab(
                requestedTab
            );

        const targetPanel =
            getTabElement(
                tabName
            );

        if (!targetPanel) {
            console.error(
                "Reservation tab panel not found:",
                tabName
            );

            return;
        }

        state.activeTab =
            tabName;

        localStorage.setItem(
            "activeReservationTab",
            tabName
        );

        setActiveTabVisual(
            tabName
        );

        if (!options.silent) {
            targetPanel.innerHTML =
                getTabLoadingMarkup();
        }

        state.tabRequestController
            ?.abort();

        const controller =
            new AbortController();

        state.tabRequestController =
            controller;

        try {
            const response =
                await fetch(
                    TAB_ENDPOINTS[
                    tabName
                    ],
                    {
                        method:
                            "GET",

                        credentials:
                            "same-origin",

                        cache:
                            "no-store",

                        headers: {
                            Accept:
                                "text/html",

                            "X-Requested-With":
                                "XMLHttpRequest"
                        },

                        signal:
                            controller.signal
                    }
                );

            if (!response.ok) {
                if (
                    response.status ===
                    401 ||
                    response.status ===
                    403
                ) {
                    throw new Error(
                        "انتهت الجلسة أو لا توجد صلاحية لعرض هذه البيانات."
                    );
                }

                throw new Error(
                    `تعذر تحميل التبويب. رمز الاستجابة: ${response.status}`
                );
            }

            const html =
                await response.text();

            targetPanel.innerHTML =
                html;

            window.requestAnimationFrame(
                () => {
                    targetPanel
                        .classList
                        .add(
                            "show-smooth"
                        );
                }
            );

            initializePromotionExperience(
                targetPanel
            );

            if (
                tabName ===
                "reservation-history-tab"
            ) {
                initializeHistoryFilters();
            }

            if (
                tabName ===
                "calendar-manager-tab" &&
                typeof window
                    .initPremiumCalendar ===
                "function"
            ) {
                window
                    .initPremiumCalendar();
            }
        } catch (error) {
            if (
                error.name ===
                "AbortError"
            ) {
                return;
            }

            console.error(
                "Reservation tab loading error:",
                error
            );

            targetPanel.innerHTML =
                getTabErrorMarkup(
                    error.message ||
                    "تعذر تحديث البيانات.",
                    tabName
                );
        } finally {
            if (
                state.tabRequestController ===
                controller
            ) {
                state.tabRequestController =
                    null;
            }
        }
    }

    window.activateReservationTab =
        loadReservationTab;

    function initializeTabNavigation() {
        const tabContainer =
            document.querySelector(
                ".custom-tabs-container"
            );

        if (
            !tabContainer ||
            tabContainer.dataset
                .initialized ===
            "true"
        ) {
            return;
        }

        tabContainer.dataset
            .initialized =
            "true";

        tabContainer.addEventListener(
            "click",
            event => {
                const button =
                    event.target.closest(
                        ".tab-btn[data-target]"
                    );

                if (!button) {
                    return;
                }

                event.preventDefault();

                loadReservationTab(
                    button.dataset.target
                );
            }
        );

        tabContainer.addEventListener(
            "keydown",
            event => {
                if (
                    ![
                        "ArrowRight",
                        "ArrowLeft",
                        "Home",
                        "End"
                    ].includes(
                        event.key
                    )
                ) {
                    return;
                }

                const buttons =
                    all(
                        ".tab-btn[data-target]",
                        tabContainer
                    );

                if (
                    buttons.length ===
                    0
                ) {
                    return;
                }

                const currentIndex =
                    Math.max(
                        0,
                        buttons.indexOf(
                            document.activeElement
                        )
                    );

                let nextIndex =
                    currentIndex;

                if (
                    event.key ===
                    "Home"
                ) {
                    nextIndex =
                        0;
                } else if (
                    event.key ===
                    "End"
                ) {
                    nextIndex =
                        buttons.length -
                        1;
                } else if (
                    event.key ===
                    "ArrowRight"
                ) {
                    nextIndex =
                        (
                            currentIndex -
                            1 +
                            buttons.length
                        ) %
                        buttons.length;
                } else if (
                    event.key ===
                    "ArrowLeft"
                ) {
                    nextIndex =
                        (
                            currentIndex +
                            1
                        ) %
                        buttons.length;
                }

                event.preventDefault();

                buttons[
                    nextIndex
                ].focus();

                loadReservationTab(
                    buttons[
                        nextIndex
                    ].dataset.target
                );
            }
        );
    }

    function initializeRefreshButton() {
        const refreshButton =
            document.querySelector(
                "[data-reservation-refresh='true']"
            );

        if (
            !refreshButton ||
            refreshButton.dataset
                .initialized ===
            "true"
        ) {
            return;
        }

        refreshButton.dataset
            .initialized =
            "true";

        refreshButton.addEventListener(
            "click",
            () => {
                loadReservationTab(
                    state.activeTab ||
                    DEFAULT_TAB
                );
            }
        );
    }

    function initializeRetryDelegation() {
        document.addEventListener(
            "click",
            event => {
                const retryButton =
                    event.target.closest(
                        "[data-retry-tab]"
                    );

                if (!retryButton) {
                    return;
                }

                loadReservationTab(
                    retryButton.dataset
                        .retryTab
                );
            }
        );
    }

    /* =====================================================================
       فلترة سجل الحجوزات
       ===================================================================== */

    function updateHistoryEmptyState(
        visibleRows
    ) {
        const historyPanel =
            getTabElement(
                "reservation-history-tab"
            );

        if (!historyPanel) {
            return;
        }

        const tableShell =
            historyPanel.querySelector(
                ".compact-reservation-table-shell"
            );

        let emptyState =
            byId(
                "historyFilteredEmptyState"
            );

        if (visibleRows > 0) {
            emptyState?.remove();
            return;
        }

        if (
            !tableShell ||
            emptyState
        ) {
            return;
        }

        emptyState =
            document.createElement(
                "div"
            );

        emptyState.id =
            "historyFilteredEmptyState";

        emptyState.className =
            "premium-empty-state compact-empty-state";

        emptyState.innerHTML = `
            <div class="empty-state-icon">
                <i class="bi bi-search"></i>
            </div>

            <h5 class="fw-black mb-2">
                لا توجد نتائج مطابقة
            </h5>

            <p class="text-muted mb-0">
                غيّر كلمة البحث أو اختر حالة أخرى.
            </p>
        `;

        tableShell.insertAdjacentElement(
            "afterend",
            emptyState
        );
    }

    function initializeHistoryFilters() {
        const searchInput =
            byId(
                "historySearch"
            );

        const hiddenStatusInput =
            byId(
                "hiddenStatusFilter"
            );

        const filterLabel =
            byId(
                "filterLabel"
            );

        const filterOptions =
            all(
                ".filter-option"
            );

        if (
            !searchInput ||
            searchInput.dataset
                .initialized ===
            "true"
        ) {
            return;
        }

        searchInput.dataset
            .initialized =
            "true";

        const applyFilters =
            () => {
                const searchValue =
                    searchInput.value
                        .toLowerCase()
                        .trim();

                const selectedStatus =
                    hiddenStatusInput
                        ?.value ||
                    "";

                const rows =
                    all(
                        "tr.history-card"
                    );

                let visibleRows =
                    0;

                rows.forEach(
                    row => {
                        const rowStatus =
                            row.dataset
                                .status ||
                            "";

                        const rowText =
                            row.textContent
                                .toLowerCase();

                        const matchesSearch =
                            !searchValue ||
                            rowText.includes(
                                searchValue
                            );

                        const matchesStatus =
                            !selectedStatus ||
                            rowStatus ===
                            selectedStatus;

                        const visible =
                            matchesSearch &&
                            matchesStatus;

                        row.style
                            .setProperty(
                                "display",
                                visible
                                    ? "table-row"
                                    : "none",
                                "important"
                            );

                        if (visible) {
                            visibleRows +=
                                1;
                        }
                    }
                );

                updateHistoryEmptyState(
                    visibleRows
                );
            };

        searchInput.addEventListener(
            "input",
            applyFilters
        );

        searchInput.addEventListener(
            "keydown",
            event => {
                if (
                    event.key !==
                    "Escape"
                ) {
                    return;
                }

                searchInput.value =
                    "";

                applyFilters();
            }
        );

        filterOptions.forEach(
            option => {
                option.addEventListener(
                    "click",
                    event => {
                        event.preventDefault();

                        filterOptions
                            .forEach(
                                item => {
                                    item.classList
                                        .remove(
                                            "active",
                                            "fw-bold"
                                        );
                                }
                            );

                        option.classList
                            .add(
                                "active",
                                "fw-bold"
                            );

                        if (
                            hiddenStatusInput
                        ) {
                            hiddenStatusInput
                                .value =
                                option.dataset
                                    .value ||
                                "";
                        }

                        if (filterLabel) {
                            filterLabel
                                .textContent =
                                option.textContent
                                    .trim();
                        }

                        applyFilters();
                    }
                );
            }
        );

        applyFilters();
    }

    /* =====================================================================
       قراءة بيانات الحجز
       ===================================================================== */

    function readBookingDetails(
        button
    ) {
        return {
            bookingId:
                readAttribute(
                    button,
                    "data-id"
                ),

            customerId:
                readAttribute(
                    button,
                    "data-customer-id"
                ),

            placeId:
                readAttribute(
                    button,
                    "data-place-id"
                ),

            customerName:
                readAttribute(
                    button,
                    "data-customer",
                    "عميل"
                ),

            customerPhone:
                readAttribute(
                    button,
                    "data-phone",
                    "بدون هاتف"
                ),

            placeName:
                readAttribute(
                    button,
                    "data-place",
                    "منشأة غير معروفة"
                ),

            unitName:
                readAttribute(
                    button,
                    "data-unit",
                    "وحدة حجز"
                ),

            unitType:
                readAttribute(
                    button,
                    "data-unit-type",
                    "الغرفة أو الباقة"
                ),

            hasAssignedRooms:
                readBooleanAttribute(
                    button,
                    "data-has-assigned-rooms",
                    false
                ),

            assignedRooms:
                readAttribute(
                    button,
                    "data-assigned-rooms",
                    "لم تُسند أرقام غرف فعلية"
                ),

            startDate:
                readAttribute(
                    button,
                    "data-start",
                    "--"
                ),

            endDate:
                readAttribute(
                    button,
                    "data-end",
                    "نفس اليوم"
                ),

            startIso:
                readAttribute(
                    button,
                    "data-start-iso"
                ),

            endIso:
                readAttribute(
                    button,
                    "data-end-iso"
                ),

            operationalStart:
                readAttribute(
                    button,
                    "data-operational-start"
                ),

            operationalEnd:
                readAttribute(
                    button,
                    "data-operational-end"
                ),

            noShowAvailableAt:
                readAttribute(
                    button,
                    "data-no-show-available-at"
                ),

            noShowGraceMinutes:
                parseInteger(
                    readAttribute(
                        button,
                        "data-no-show-grace-minutes",
                        "0"
                    ),
                    0
                ),

            totalAmount:
                readAttribute(
                    button,
                    "data-total",
                    "0 ريال يمني"
                ),

            totalAmountRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-total-raw",
                        "0"
                    ),
                    0
                ),

            hasPromotion:
                readBooleanAttribute(
                    button,
                    "data-has-promotion",
                    false
                ),

            promotionId:
                parsePositiveId(
                    readAttribute(
                        button,
                        "data-promotion-id",
                        "0"
                    )
                ),

            promotionTitle:
                readAttribute(
                    button,
                    "data-promotion-title",
                    "عرض خاص"
                ),

            promotionCode:
                readAttribute(
                    button,
                    "data-promotion-code"
                ),

            discountType:
                readAttribute(
                    button,
                    "data-discount-type"
                ),

            discountValue:
                readAttribute(
                    button,
                    "data-discount-value"
                ),

            discountLabel:
                readAttribute(
                    button,
                    "data-discount-label",
                    "عرض مطبّق"
                ),

            originalTotal:
                readAttribute(
                    button,
                    "data-original-total",
                    "0 ريال يمني"
                ),

            originalTotalRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-original-total-raw",
                        "0"
                    ),
                    0
                ),

            discountAmount:
                readAttribute(
                    button,
                    "data-discount-amount",
                    "0 ريال يمني"
                ),

            discountAmountRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-discount-amount-raw",
                        "0"
                    ),
                    0
                ),

            finalSavings:
                readAttribute(
                    button,
                    "data-final-savings",
                    "0 ريال يمني"
                ),

            bookingState:
                readAttribute(
                    button,
                    "data-status"
                ),

            paymentStatus:
                readAttribute(
                    button,
                    "data-payment-status"
                ),

            paymentLabel:
                readAttribute(
                    button,
                    "data-payment-label"
                ),

            paymentMethod:
                readAttribute(
                    button,
                    "data-method",
                    "لم تُحدد"
                ),

            transferReference:
                readAttribute(
                    button,
                    "data-ref",
                    "غير متوفر"
                ),

            onlinePaymentType:
                readAttribute(
                    button,
                    "data-online-payment-type",
                    "Deposit"
                ),

            onlinePaymentTypeLabel:
                readAttribute(
                    button,
                    "data-online-payment-type-label",
                    "دفع العربون"
                ),

            expectedOnlineAmount:
                readAttribute(
                    button,
                    "data-expected-online",
                    "0 ريال يمني"
                ),

            expectedOnlineAmountRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-expected-online-raw",
                        "0"
                    ),
                    0
                ),

            expectedRemaining:
                readAttribute(
                    button,
                    "data-expected-remaining",
                    "0 ريال يمني"
                ),

            expectedRemainingRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-expected-remaining-raw",
                        "0"
                    ),
                    0
                ),

            depositAmount:
                readAttribute(
                    button,
                    "data-deposit",
                    "0 ريال يمني"
                ),

            depositAmountRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-deposit-raw",
                        "0"
                    ),
                    0
                ),

            onlineAmount:
                readAttribute(
                    button,
                    "data-online",
                    "0 ريال يمني"
                ),

            onlineAmountRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-online-raw",
                        "0"
                    ),
                    parseNumber(
                        readAttribute(
                            button,
                            "data-online",
                            "0"
                        ),
                        0
                    )
                ),

            propertyAmount:
                readAttribute(
                    button,
                    "data-property",
                    "0 ريال يمني"
                ),

            propertyAmountRaw:
                parseNumber(
                    readAttribute(
                        button,
                        "data-property-raw",
                        "0"
                    ),
                    parseNumber(
                        readAttribute(
                            button,
                            "data-property",
                            "0"
                        ),
                        0
                    )
                ),

            collectedAmount:
                readAttribute(
                    button,
                    "data-collected",
                    "0 ريال يمني"
                ),

            totalCollectedAmount:
                readAttribute(
                    button,
                    "data-total-collected",
                    "0 ريال يمني"
                ),

            hasProof:
                readBooleanAttribute(
                    button,
                    "data-has-proof",
                    false
                ),

            paymentConfirmed:
                readBooleanAttribute(
                    button,
                    "data-payment-confirmed",
                    false
                ),

            commissionStatus:
                readAttribute(
                    button,
                    "data-commission-status"
                ),

            paymentImage:
                readAttribute(
                    button,
                    "data-image"
                ),

            timer:
                readAttribute(
                    button,
                    "data-timer"
                ),

            rejectionReason:
                readAttribute(
                    button,
                    "data-reason"
                ),

            reasonLabel:
                readAttribute(
                    button,
                    "data-reason-label",
                    "سبب الإغلاق"
                ),

            paymentProofRejection:
                readAttribute(
                    button,
                    "data-payment-proof-rejection"
                ),

            paymentProofRejectedAt:
                readAttribute(
                    button,
                    "data-payment-proof-rejected-at"
                ),

            paymentProofReviewCount:
                parseInteger(
                    readAttribute(
                        button,
                        "data-payment-proof-review-count",
                        "0"
                    ),
                    0
                ),

            services:
                readAttribute(
                    button,
                    "data-services",
                    "لا توجد خدمات إضافية"
                ),

            quantity:
                readAttribute(
                    button,
                    "data-quantity",
                    "1"
                ),

            adults:
                readAttribute(
                    button,
                    "data-adults",
                    "1"
                ),

            children:
                readAttribute(
                    button,
                    "data-children",
                    "0"
                ),

            bookingType:
                readAttribute(
                    button,
                    "data-booking-type",
                    "حجز عادي"
                ),

            seatingSetup:
                readAttribute(
                    button,
                    "data-seating",
                    "غير محدد"
                ),

            createdAt:
                readAttribute(
                    button,
                    "data-created",
                    "غير محدد"
                ),

            acceptedAt:
                readAttribute(
                    button,
                    "data-accepted-at"
                ),

            paymentApprovedAt:
                readAttribute(
                    button,
                    "data-payment-approved-at"
                ),

            completedAt:
                readAttribute(
                    button,
                    "data-completed-at"
                ),

            noShowAt:
                readAttribute(
                    button,
                    "data-no-show-at"
                ),

            closedAt:
                readAttribute(
                    button,
                    "data-closed-at"
                ),

            closedBy:
                readAttribute(
                    button,
                    "data-closed-by"
                ),

            closedByLabel:
                readAttribute(
                    button,
                    "data-closed-by-label"
                ),

            lastStatusAt:
                readAttribute(
                    button,
                    "data-last-status-at"
                ),

            cancellationReason:
                readAttribute(
                    button,
                    "data-cancellation-reason"
                ),

            ownerCancellationReason:
                readAttribute(
                    button,
                    "data-owner-cancellation-reason"
                ),

            canAccept:
                readBooleanAttribute(
                    button,
                    "data-can-accept",
                    true
                ),

            canApprovePayment:
                readBooleanAttribute(
                    button,
                    "data-can-approve-payment",
                    true
                ),

            canRejectPermanently:
                readBooleanAttribute(
                    button,
                    "data-can-reject-permanently",
                    true
                ),

            canComplete:
                readBooleanAttribute(
                    button,
                    "data-can-complete",
                    false
                ),

            canNoShow:
                readBooleanAttribute(
                    button,
                    "data-can-no-show",
                    false
                ),

            canOwnerCancel:
                readBooleanAttribute(
                    button,
                    "data-can-owner-cancel",
                    false
                ),

            actionNote:
                readAttribute(
                    button,
                    "data-action-note"
                ),

            refundStatus:
                readAttribute(
                    button,
                    "data-refund-status"
                ),

            refundRequestStatus:
                readAttribute(
                    button,
                    "data-refund-request-status"
                ),

            refundDeposit:
                readAttribute(
                    button,
                    "data-refund-deposit",
                    "0"
                ),

            refundFee:
                readAttribute(
                    button,
                    "data-refund-fee",
                    "0"
                ),

            refundAmount:
                readAttribute(
                    button,
                    "data-refund-amount",
                    "0"
                ),

            refundOwnerShare:
                readAttribute(
                    button,
                    "data-refund-owner-share",
                    "0"
                ),

            refundReason:
                readAttribute(
                    button,
                    "data-refund-reason"
                ),

            refundRequestedAt:
                readAttribute(
                    button,
                    "data-refund-requested-at"
                ),

            refundProcessedAt:
                readAttribute(
                    button,
                    "data-refund-processed-at"
                ),

            refundNotes:
                readAttribute(
                    button,
                    "data-refund-notes"
                )
        };
    }

    /* =====================================================================
       عرض بيانات المودال
       ===================================================================== */

    function getBookingStateMeta(
        bookingState
    ) {
        return BOOKING_STATES[
            String(
                bookingState ||
                ""
            ).trim()
        ] || {
            label:
                "حالة غير معروفة",

            icon:
                "bi-question-circle",

            className:
                "badge-glass-info"
        };
    }

    function isFullOnlinePayment(
        details
    ) {
        const type = String(
            details?.onlinePaymentType ||
            ""
        ).trim().toLowerCase();

        if (type === "full") {
            return true;
        }

        const label = String(
            details?.onlinePaymentTypeLabel ||
            ""
        ).trim();

        if (label.includes("كامل")) {
            return true;
        }

        const total = Number(
            details?.totalRaw ||
            0
        );

        const expected = Number(
            details?.expectedOnlineAmountRaw ||
            details?.onlineAmountRaw ||
            0
        );

        return total > 0 &&
            expected >= total;
    }

    function getOnlinePaymentTypeLabel(
        details
    ) {
        if (
            details?.onlinePaymentTypeLabel
        ) {
            return details
                .onlinePaymentTypeLabel;
        }

        return isFullOnlinePayment(details)
            ? "دفع كامل الحجز"
            : "دفع العربون";
    }

    function getPaymentStateMeta(
        details
    ) {
        const configured =
            PAYMENT_STATES[
            String(
                details.paymentStatus ||
                ""
            ).trim()
            ];

        if (configured) {
            return {
                ...configured,

                label:
                    details.paymentLabel ||
                    configured.label
            };
        }

        if (
            details.paymentConfirmed
        ) {
            return {
                label:
                    details.paymentLabel ||
                    "الدفع معتمد",

                icon:
                    "bi-check2-circle",

                className:
                    "badge-glass-success"
            };
        }

        return {
            label:
                details.paymentLabel ||
                "غير مدفوع",

            icon:
                "bi-wallet2",

            className:
                "badge-glass-warning"
        };
    }

    function updateModalBadge(
        id,
        meta
    ) {
        const element =
            byId(id);

        if (!element) {
            return;
        }

        element.className =
            `badge rounded-pill ${meta.className}`;

        element.innerHTML = `
            <i class="bi ${escapeHtml(
            meta.icon
        )}"></i>

            <span>
                ${escapeHtml(
            meta.label
        )}
            </span>
        `;
    }

    function getCommissionLabel(value) {
        const status =
            String(
                value ||
                ""
            )
                .trim()
                .toLowerCase();

        if (
            [
                "paid",
                "completed",
                "released"
            ].includes(
                status
            )
        ) {
            return "تمت تسوية العمولة";
        }

        if (
            [
                "pending",
                "unpaid",
                "held",
                "calculated"
            ].includes(
                status
            )
        ) {
            return "العمولة قيد التسوية";
        }

        if (
            [
                "cancelled",
                "notapplicable",
                "not_applicable"
            ].includes(
                status
            )
        ) {
            return "لا توجد عمولة مستحقة";
        }

        return value ||
            "غير محددة";
    }

    function setFinancialRow(
        rowId,
        value,
        className = ""
    ) {
        const row =
            byId(rowId);

        const strong =
            row?.querySelector(
                "strong"
            );

        if (!strong) {
            return;
        }

        strong.textContent =
            value ||
            "--";

        if (className) {
            strong.className =
                className;
        }
    }

    function updateFinancialSummary(
        details
    ) {
        setText(
            "modalAmountOnline",
            details.onlineAmount,
            "0 ريال يمني"
        );

        setText(
            "modalAmountProperty",
            details.propertyAmount,
            "0 ريال يمني"
        );

        setText(
            "modalTotalPrice",
            details.totalAmount,
            "0 ريال يمني"
        );

        setFinancialRow(
            "modalDepositRow",
            details.depositAmount,
            "text-info"
        );

        setFinancialRow(
            "modalOnlinePaymentTypeRow",
            getOnlinePaymentTypeLabel(
                details
            ),
            isFullOnlinePayment(details)
                ? "text-success"
                : "text-info"
        );

        setFinancialRow(
            "modalExpectedOnlineRow",
            details.expectedOnlineAmount,
            "text-success"
        );

        setFinancialRow(
            "modalExpectedRemainingRow",
            details.expectedRemaining,
            details.expectedRemainingRaw > 0
                ? "text-warning"
                : "text-success"
        );

        setFinancialRow(
            "modalPaymentConfirmedRow",
            details.paymentConfirmed
                ? "تم اعتماد الدفع"
                : "الدفع غير معتمد",

            details.paymentConfirmed
                ? "text-success"
                : "text-warning"
        );

        const isPaymentReview =
            details.bookingState ===
            "PaymentReview";

        setVisible(
            "modalPaymentReviewNotice",
            isPaymentReview,
            "flex"
        );

        if (isPaymentReview) {
            setText(
                "modalPaymentReviewExpected",
                details.expectedOnlineAmount,
                "غير محدد"
            );
        }

        setFinancialRow(
            "modalTotalCollectedRow",
            details.totalCollectedAmount,
            "text-success"
        );

        setFinancialRow(
            "modalCommissionStatusRow",
            getCommissionLabel(
                details.commissionStatus
            ),
            "text-muted"
        );
    }

    function updatePromotionSection(
        details
    ) {
        const section =
            byId(
                "modalPromotionSection"
            );

        if (!section) {
            return;
        }

        const hasPromotion =
            Boolean(
                details.hasPromotion &&
                details.discountAmountRaw >
                0
            );

        setVisible(
            section,
            hasPromotion,
            "block"
        );

        if (!hasPromotion) {
            return;
        }

        setText(
            "modalPromotionTitle",
            details.promotionTitle,
            "عرض خاص"
        );

        setText(
            "modalPromotionDiscountLabel",
            details.discountLabel,
            "عرض مطبّق"
        );

        setText(
            "modalPromotionOriginalPrice",
            details.originalTotal,
            "0 ريال يمني"
        );

        setText(
            "modalPromotionFinalPrice",
            details.totalAmount,
            "0 ريال يمني"
        );

        setText(
            "modalPromotionDiscountAmount",
            details.discountAmount,
            "0 ريال يمني"
        );

        setText(
            "modalPromotionSavings",
            details.finalSavings,
            "0 ريال يمني"
        );

        const hasCode =
            Boolean(
                String(
                    details.promotionCode ||
                    ""
                ).trim()
            );

        setVisible(
            "modalPromotionCodeRow",
            hasCode,
            "inline-flex"
        );

        if (hasCode) {
            setText(
                "modalPromotionCode",
                details.promotionCode,
                "--"
            );
        }

        initializePromotionExperience(
            section
        );
    }

    function configureReceiptButton(
        imagePath
    ) {
        const button =
            byId(
                "quickViewReceiptBtn"
            );

        if (!button) {
            return;
        }

        const cleanPath =
            normalizeImagePath(
                imagePath
            );

        if (!cleanPath) {
            button.style.display =
                "none";

            button.onclick =
                null;

            return;
        }

        button.style.display =
            "inline-flex";

        button.onclick =
            async () => {
                if (!hasSweetAlert()) {
                    window.open(
                        cleanPath,
                        "_blank",
                        "noopener,noreferrer"
                    );

                    return;
                }

                await window.Swal.fire({
                    title:
                        "معاينة إشعار الدفع",

                    html: `
                        <div class="reservation-receipt-preview">

                            <img id="reservationReceiptPreview"
                                 alt="صورة إشعار دفع الحجز"
                                 src="${escapeHtml(
                        cleanPath
                    )}" />

                        </div>
                    `,

                    width:
                        680,

                    showCloseButton:
                        true,

                    confirmButtonText:
                        "إغلاق",

                    confirmButtonColor:
                        "#05203C",

                    returnFocus:
                        false,

                    didOpen:
                        popup => {
                            const image =
                                popup.querySelector(
                                    "#reservationReceiptPreview"
                                );

                            if (!image) {
                                return;
                            }

                            image.onerror =
                                () => {
                                    const wrapper =
                                        image.closest(
                                            ".reservation-receipt-preview"
                                        );

                                    if (wrapper) {
                                        wrapper.innerHTML = `
                                            <div class="premium-empty-state compact-empty-state">

                                                <div class="empty-state-icon">
                                                    <i class="bi bi-image"></i>
                                                </div>

                                                <h5 class="text-danger">
                                                    تعذر تحميل صورة إشعار الدفع
                                                </h5>

                                            </div>
                                        `;
                                    }
                                };
                        }
                });
            };
    }

    function updateOperationalSection(
        details
    ) {
        const hasOperationalData =
            Boolean(
                details.operationalStart ||
                details.operationalEnd ||
                details.noShowAvailableAt ||
                details.actionNote
            );

        setVisible(
            "modalOperationalPanel",
            hasOperationalData
        );

        setText(
            "modalActionAvailabilityText",
            details.actionNote,
            "سيتم تحديد توفر الإجراءات حسب موعد الحجز."
        );

        setText(
            "modalOperationalStartAt",
            formatDateTime(
                details.operationalStart
            ),
            "--"
        );

        setText(
            "modalOperationalEndAt",
            formatDateTime(
                details.operationalEnd
            ),
            "--"
        );

        setText(
            "modalNoShowAvailableAt",
            formatDateTime(
                details.noShowAvailableAt
            ),
            "--"
        );

        setText(
            "modalNoShowGraceMinutes",
            details.noShowGraceMinutes > 0
                ? `${details.noShowGraceMinutes} دقيقة`
                : "غير محددة",
            "غير محددة"
        );
    }

    function updatePaymentProofRejection(
        details
    ) {
        const visible =
            Boolean(
                details
                    .paymentProofRejection
            );

        setVisible(
            "modalPaymentProofRejectionRow",
            visible
        );

        if (!visible) {
            return;
        }

        setText(
            "modalPaymentProofRejectionReason",
            details
                .paymentProofRejection
        );

        setText(
            "modalPaymentProofRejectedAt",
            formatDateTime(
                details
                    .paymentProofRejectedAt
            )
        );

        setText(
            "modalPaymentProofReviewCount",
            String(
                details
                    .paymentProofReviewCount ||
                0
            ),
            "0"
        );
    }

    function getClosedByLabel(value) {
        switch (
        String(
            value ||
            ""
        )
            .trim()
            .toLowerCase()
        ) {
            case "owner":
                return "المالك";

            case "customer":
                return "العميل";

            case "admin":
                return "الإدارة";

            case "system":
                return "النظام تلقائيًا";

            default:
                return "غير محدد";
        }
    }

    function getClosureReason(
        details
    ) {
        if (
            details
                .ownerCancellationReason
        ) {
            return {
                label:
                    "سبب إلغاء المالك",

                value:
                    details
                        .ownerCancellationReason
            };
        }

        if (
            details
                .cancellationReason
        ) {
            return {
                label:
                    "سبب إلغاء العميل",

                value:
                    details
                        .cancellationReason
            };
        }

        if (
            details.rejectionReason
        ) {
            return {
                label:
                    details.reasonLabel ||
                    "سبب الإغلاق",

                value:
                    details
                        .rejectionReason
            };
        }

        return null;
    }

    function updateClosureSection(
        details
    ) {
        const closedStates = [
            "Completed",
            "Rejected",
            "Cancelled",
            "NoShow",
            "Expired"
        ];

        const isClosed =
            closedStates.includes(
                details.bookingState
            ) ||
            Boolean(
                details.closedAt ||
                details.closedBy ||
                details.closedByLabel
            );

        setVisible(
            "modalClosureSection",
            isClosed
        );

        if (!isClosed) {
            return;
        }

        const stateMeta =
            getBookingStateMeta(
                details.bookingState
            );

        const closureReason =
            getClosureReason(
                details
            );

        setText(
            "modalClosureTitle",
            stateMeta.label,
            "تم إغلاق الحجز"
        );

        setText(
            "modalClosedBy",
            details.closedByLabel ||
            getClosedByLabel(
                details.closedBy
            ),
            "غير محدد"
        );

        setText(
            "modalClosedAt",
            formatDateTime(
                details.closedAt
            ),
            "--"
        );

        setText(
            "modalLastStatusChangedAt",
            formatDateTime(
                details.lastStatusAt
            ),
            "--"
        );

        setVisible(
            "modalClosureReasonRow",
            Boolean(
                closureReason
            )
        );

        if (closureReason) {
            setText(
                "modalClosureReasonLabel",
                closureReason.label,
                "سبب الإغلاق"
            );

            setText(
                "modalClosureReason",
                closureReason.value
            );
        }
    }

    function getRefundStatusLabel(
        status
    ) {
        switch (
        String(
            status ||
            ""
        )
            .trim()
            .toLowerCase()
        ) {
            case "pending":
                return "قيد المراجعة";

            case "processing":
                return "قيد المعالجة";

            case "completed":
                return "تم الاسترداد";

            case "rejected":
                return "تم رفض الاسترداد";

            case "notapplicable":
            case "not_applicable":
                return "لا يوجد استرداد";

            default:
                return status ||
                    "غير محددة";
        }
    }

    function updateRefundSection(
        details
    ) {
        const refundStatus =
            details.refundRequestStatus ||
            details.refundStatus;

        const hasRefund =
            Boolean(
                details.refundRequestStatus ||
                parseNumber(
                    details.refundDeposit,
                    0
                ) > 0 ||
                parseNumber(
                    details.refundFee,
                    0
                ) > 0 ||
                parseNumber(
                    details.refundAmount,
                    0
                ) > 0 ||
                details.refundReason ||
                details.refundRequestedAt
            );

        setVisible(
            "modalRefundSection",
            hasRefund
        );

        if (!hasRefund) {
            return;
        }

        setText(
            "modalRefundStatus",
            getRefundStatusLabel(
                refundStatus
            )
        );

        setText(
            "modalRefundDeposit",
            `${formatMoney(
                details.refundDeposit
            )} ريال`
        );

        setText(
            "modalRefundFee",
            `${formatMoney(
                details.refundFee
            )} ريال`
        );

        setText(
            "modalRefundAmount",
            `${formatMoney(
                details.refundAmount
            )} ريال`
        );

        setText(
            "modalRefundOwnerShare",
            `${formatMoney(
                details.refundOwnerShare
            )} ريال`
        );

        setText(
            "modalRefundRequestedAt",
            formatDateTime(
                details.refundRequestedAt
            ),
            "--"
        );

        setText(
            "modalRefundProcessedAt",
            formatDateTime(
                details.refundProcessedAt
            ),
            "لم تتم المعالجة بعد"
        );

        setVisible(
            "modalRefundReasonRow",
            Boolean(
                details.refundReason
            )
        );

        if (
            details.refundReason
        ) {
            setText(
                "modalRefundReason",
                details.refundReason
            );
        }

        setVisible(
            "modalRefundNotesRow",
            Boolean(
                details.refundNotes
            )
        );

        if (
            details.refundNotes
        ) {
            setText(
                "modalRefundNotes",
                details.refundNotes
            );
        }
    }

    function buildActionButton({
        className,
        icon,
        text,
        handler,
        disabled = false,
        title = ""
    }) {
        return `
            <button type="button"
                    class="btn ${escapeHtml(
            className
        )}"
                    ${disabled
                ? 'disabled aria-disabled="true"'
                : ""
            }
                    ${title
                ? `title="${escapeHtml(
                    title
                )}"`
                : ""
            }
                    onclick="${escapeHtml(
                handler
            )}">

                <i class="bi ${escapeHtml(
                icon
            )}"></i>

                <span>
                    ${escapeHtml(text)}
                </span>

            </button>
        `;
    }

    function buildModalActionButtons(
        details
    ) {
        const bookingId =
            parsePositiveId(
                details.bookingId
            );

        if (!bookingId) {
            return "";
        }

        if (
            details.bookingState ===
            "Pending"
        ) {
            return [
                buildActionButton({
                    className:
                        "btn-success",

                    icon:
                        "bi-check2-circle",

                    text:
                        "قبول الطلب",

                    handler:
                        `window.executeAcceptBooking(${bookingId})`,

                    disabled:
                        !details.canAccept,

                    title:
                        details.canAccept
                            ? ""
                            : details.actionNote
                }),

                buildActionButton({
                    className:
                        "btn-outline-danger",

                    icon:
                        "bi-x-circle",

                    text:
                        "رفض الطلب",

                    handler:
                        `window.executeModalReject(${bookingId})`
                })
            ].join("");
        }

        if (
            details.bookingState ===
            "AwaitingPayment"
        ) {
            return [
                `
                    <div class="reservation-awaiting-message">

                        <i class="bi bi-stopwatch"></i>

                        <span>
                            الحجز بانتظار دفع العميل.
                        </span>

                    </div>
                `,

                buildActionButton({
                    className:
                        "btn-outline-danger reservation-permanent-reject-btn",

                    icon:
                        "bi-ban",

                    text:
                        "رفض الحجز نهائيًا",

                    handler:
                        `window.executePermanentReject(${bookingId})`,

                    disabled:
                        !details
                            .canRejectPermanently,

                    title:
                        details
                            .canRejectPermanently
                            ? ""
                            : details.actionNote
                })
            ].join("");
        }

        if (
            details.bookingState ===
            "PaymentReview"
        ) {
            return [
                buildActionButton({
                    className:
                        "btn-success",

                    icon:
                        "bi-shield-check",

                    text:
                        isFullOnlinePayment(details)
                            ? "اعتماد كامل قيمة الحجز"
                            : "اعتماد العربون",

                    handler:
                        `window.executeApprovePayment(${bookingId})`,

                    disabled:
                        !details
                            .canApprovePayment,

                    title:
                        details
                            .canApprovePayment
                            ? ""
                            : details.actionNote
                }),

                buildActionButton({
                    className:
                        "btn-outline-warning",

                    icon:
                        "bi-file-earmark-x",

                    text:
                        "رفض إشعار الدفع",

                    handler:
                        `window.executePaymentReject(${bookingId})`
                }),

                buildActionButton({
                    className:
                        "btn-outline-danger reservation-permanent-reject-btn",

                    icon:
                        "bi-ban",

                    text:
                        "رفض الحجز نهائيًا",

                    handler:
                        `window.executePermanentReject(${bookingId})`,

                    disabled:
                        !details
                            .canRejectPermanently,

                    title:
                        details
                            .canRejectPermanently
                            ? ""
                            : details.actionNote
                })
            ].join("");
        }

        if (
            details.bookingState ===
            "Confirmed"
        ) {
            return [
                buildActionButton({
                    className:
                        "btn-success",

                    icon:
                        "bi-cash-stack",

                    text:
                        "تحصيل وإنهاء الحجز",

                    handler:
                        `window.executeCompleteBooking(${bookingId})`,

                    disabled:
                        !details.canComplete,

                    title:
                        details.canComplete
                            ? ""
                            : details.actionNote
                }),

                buildActionButton({
                    className:
                        "btn-warning text-dark",

                    icon:
                        "bi-person-x",

                    text:
                        "تسجيل عدم حضور",

                    handler:
                        `window.executeNoShowBooking(${bookingId})`,

                    disabled:
                        !details.canNoShow,

                    title:
                        details.canNoShow
                            ? ""
                            : details.actionNote
                }),

                buildActionButton({
                    className:
                        "btn-outline-danger reservation-owner-cancel-btn",

                    icon:
                        "bi-calendar-x",

                    text:
                        "إلغاء الحجز من جهة المالك",

                    handler:
                        `window.executeOwnerCancellation(${bookingId})`,

                    disabled:
                        !details.canOwnerCancel,

                    title:
                        details.canOwnerCancel
                            ? ""
                            : details.actionNote
                })
            ].join("");
        }

        return "";
    }

    function updateActionNotice(
        details
    ) {
        const notice =
            byId(
                "modalActionNotice"
            );

        if (!notice) {
            return;
        }

        const note =
            String(
                details.actionNote ||
                ""
            ).trim();

        if (!note) {
            notice.innerHTML =
                "";

            notice.style.display =
                "none";

            return;
        }

        notice.innerHTML = `
            <i class="bi bi-info-circle"></i>

            <span>
                ${escapeHtml(note)}
            </span>
        `;

        notice.style.display =
            "flex";
    }

    window.openPremiumCenterModal =
        function (
            button
        ) {
            if (!button) {
                return;
            }

            const details =
                readBookingDetails(
                    button
                );

            state.currentBooking =
                details;

            setText(
                "modalBookingTitle",
                `تفاصيل الحجز رقم #${details.bookingId}`,
                "تفاصيل الحجز"
            );

            updateModalBadge(
                "modalBookingState",
                getBookingStateMeta(
                    details.bookingState
                )
            );

            updateModalBadge(
                "modalPaymentState",
                getPaymentStateMeta(
                    details
                )
            );

            setText(
                "modalCustomerName",
                details.customerName,
                "عميل"
            );

            setText(
                "modalCustomerPhone",
                details.customerPhone,
                "بدون هاتف"
            );

            setText(
                "modalPlaceName",
                details.placeName,
                "منشأة غير معروفة"
            );

            setText(
                "modalUnitType",
                details.unitType,
                "الغرفة أو الباقة"
            );

            setText(
                "modalUnitName",
                details.unitName,
                "وحدة حجز"
            );

            setText(
                "modalAssignedRooms",
                details.assignedRooms,
                "لم تُسند أرقام غرف فعلية"
            );

            setVisible(
                "modalAssignedRoomsCard",
                details.hasAssignedRooms
            );

            setText(
                "modalStartDate",
                details.startDate
            );

            setText(
                "modalEndDate",
                details.endDate,
                "نفس اليوم"
            );

            setText(
                "modalQuantity",
                details.quantity,
                "1"
            );

            setText(
                "modalAdults",
                details.adults,
                "1"
            );

            setText(
                "modalChildren",
                details.children,
                "0"
            );

            setText(
                "modalBookingType",
                details.bookingType,
                "حجز عادي"
            );

            setText(
                "modalSeatingSetup",
                details.seatingSetup,
                "غير محدد"
            );

            setText(
                "modalServices",
                details.services,
                "لا توجد خدمات إضافية"
            );

            setText(
                "modalCreatedAt",
                details.createdAt,
                "غير محدد"
            );

            setText(
                "modalPaymentTimer",
                details.bookingState ===
                    "AwaitingPayment"
                    ? formatPaymentTimer(
                        details.timer
                    )
                    : "غير مطلوبة"
            );

            setText(
                "modalPaymentMethod",
                details.paymentMethod,
                "لم تُحدد"
            );

            setText(
                "modalTransferRef",
                details.transferReference,
                "غير متوفر"
            );

            updatePromotionSection(
                details
            );

            updateFinancialSummary(
                details
            );

            configureReceiptButton(
                details.paymentImage
            );

            updateOperationalSection(
                details
            );

            updatePaymentProofRejection(
                details
            );

            updateClosureSection(
                details
            );

            updateRefundSection(
                details
            );

            const showGeneralReason =
                Boolean(
                    details.rejectionReason
                ) &&
                ![
                    "Cancelled",
                    "Completed",
                    "NoShow",
                    "Expired"
                ].includes(
                    details.bookingState
                );

            setVisible(
                "rejectionReasonRow",
                showGeneralReason
            );

            if (showGeneralReason) {
                setText(
                    "modalReasonLabel",
                    details.reasonLabel ||
                    "سبب الرفض"
                );

                setText(
                    "modalRejectionReason",
                    details.rejectionReason
                );
            }

            const actionButtons =
                byId(
                    "modalActionButtons"
                );

            if (actionButtons) {
                actionButtons.innerHTML =
                    buildModalActionButtons(
                        details
                    );
            }

            updateActionNotice(
                details
            );

            const modalElement =
                getReservationModalElement();

            if (!modalElement) {
                showMessage({
                    icon:
                        "error",

                    title:
                        "خطأ في الواجهة",

                    text:
                        "تعذر العثور على نافذة تفاصيل الحجز."
                });

                return;
            }

            if (
                typeof window.bootstrap ===
                "undefined" ||
                !window.bootstrap.Modal
            ) {
                showMessage({
                    icon:
                        "error",

                    title:
                        "تعذر فتح التفاصيل",

                    text:
                        "مكتبة Bootstrap غير محملة في الصفحة."
                });

                return;
            }

            if (
                modalElement.parentNode !==
                document.body
            ) {
                document.body.appendChild(
                    modalElement
                );
            }

            window.bootstrap.Modal
                .getOrCreateInstance(
                    modalElement
                )
                .show();
        };

    /* =====================================================================
       نوافذ الأسباب والإجراءات المشتركة
       ===================================================================== */

    async function requestReason(
        options
    ) {
        if (!hasSweetAlert()) {
            const value =
                window.prompt(
                    `${options.title}\n\n${options.text || ""}\n\n${options.placeholder || ""}`
                );

            if (value === null) {
                return null;
            }

            const reason =
                value.trim();

            if (
                reason.length < 5 ||
                reason.length > 500
            ) {
                window.alert(
                    "السبب يجب أن يكون بين 5 و500 حرف."
                );

                return null;
            }

            return reason;
        }

        const result =
            await window.Swal.fire({
                icon:
                    options.icon ||
                    "warning",

                title:
                    options.title,

                html:
                    options.html ||
                    `<p>${escapeHtml(
                        options.text ||
                        ""
                    )}</p>`,

                input:
                    "textarea",

                inputLabel:
                    options.inputLabel ||
                    "السبب",

                inputPlaceholder:
                    options.placeholder ||
                    "اكتب السبب بوضوح...",

                inputAttributes: {
                    maxlength:
                        "500",

                    rows:
                        "5",

                    dir:
                        "rtl",

                    autocomplete:
                        "off",

                    "aria-label":
                        options.ariaLabel ||
                        "سبب الإجراء"
                },

                showCancelButton:
                    true,

                confirmButtonText:
                    options.confirmText ||
                    "تأكيد",

                cancelButtonText:
                    "تراجع",

                confirmButtonColor:
                    options.confirmColor ||
                    "#EF4444",

                cancelButtonColor:
                    "#64748B",

                reverseButtons:
                    true,

                allowOutsideClick:
                    false,

                allowEscapeKey:
                    true,

                returnFocus:
                    false,

                didOpen:
                    popup => {
                        window.setTimeout(
                            () => {
                                popup
                                    .querySelector(
                                        ".swal2-textarea"
                                    )
                                    ?.focus();
                            },
                            50
                        );
                    },

                inputValidator:
                    value => {
                        const reason =
                            String(
                                value ||
                                ""
                            ).trim();

                        if (
                            reason.length <
                            5
                        ) {
                            return "اكتب سببًا واضحًا لا يقل عن 5 أحرف.";
                        }

                        if (
                            reason.length >
                            500
                        ) {
                            return "السبب لا يمكن أن يتجاوز 500 حرف.";
                        }

                        return undefined;
                    }
            });

        return result.isConfirmed
            ? String(
                result.value ||
                ""
            ).trim()
            : null;
    }

    async function executeSimpleAction(
        config
    ) {
        if (state.actionRunning) {
            return;
        }

        const bookingId =
            parsePositiveId(
                config.bookingId
            );

        if (!bookingId) {
            await showMessage({
                icon:
                    "error",

                title:
                    "رقم الحجز غير صحيح",

                text:
                    "تعذر تحديد الحجز المطلوب."
            });

            return;
        }

        await closeReservationModal();

        const confirmation =
            hasSweetAlert()
                ? await window.Swal.fire({
                    icon:
                        config.icon ||
                        "question",

                    title:
                        config.title,

                    text:
                        config.html
                            ? undefined
                            : config.text,

                    html:
                        config.html ||
                        undefined,

                    preConfirm:
                        config.preConfirm ||
                        undefined,

                    showCancelButton:
                        true,

                    confirmButtonText:
                        config.confirmText,

                    cancelButtonText:
                        "تراجع",

                    confirmButtonColor:
                        config.confirmColor ||
                        "#10B981",

                    cancelButtonColor:
                        "#64748B",

                    reverseButtons:
                        true,

                    focusCancel:
                        true,

                    returnFocus:
                        false
                })
                : {
                    isConfirmed:
                        window.confirm(
                            `${config.title}\n\n${config.text}`
                        )
                };

        if (
            !confirmation
                .isConfirmed
        ) {
            return;
        }

        state.actionRunning =
            true;

        showLoading(
            config.loadingTitle,
            config.loadingText
        );

        try {
            const result =
                await postForm(
                    config.endpoint,
                    {
                        id:
                            bookingId,

                        ...(
                            config.values ||
                            {}
                        )
                    }
                );

            if (
                result.success !==
                true
            ) {
                throw new Error(
                    result.message ||
                    "تعذر تنفيذ العملية."
                );
            }

            closeSweetAlert();

            await showMessage({
                icon:
                    "success",

                title:
                    config.successTitle,

                text:
                    result.message ||
                    "تم تحديث الحجز بنجاح.",

                confirmButtonText:
                    "حسنًا",

                confirmButtonColor:
                    "#00A6DA",

                returnFocus:
                    false
            });

            window.location.reload();
        } catch (error) {
            closeSweetAlert();

            console.error(
                `${config.endpoint} error:`,
                error
            );

            await showMessage({
                icon:
                    "error",

                title:
                    "تعذر تنفيذ العملية",

                text:
                    error.message ||
                    "حدث خطأ أثناء الاتصال بالسيرفر.",

                confirmButtonText:
                    "حسنًا",

                confirmButtonColor:
                    "#EF4444",

                returnFocus:
                    false
            });
        } finally {
            state.actionRunning =
                false;
        }
    }

    async function executeReasonAction(
        config
    ) {
        if (state.actionRunning) {
            return;
        }

        const bookingId =
            parsePositiveId(
                config.bookingId
            );

        if (!bookingId) {
            await showMessage({
                icon:
                    "error",

                title:
                    "رقم الحجز غير صحيح"
            });

            return;
        }

        await closeReservationModal();

        const reason =
            await requestReason(
                config.dialog
            );

        if (!reason) {
            return;
        }

        state.actionRunning =
            true;

        showLoading(
            config.loadingTitle,
            config.loadingText
        );

        try {
            const result =
                await postForm(
                    config.endpoint,
                    {
                        id:
                            bookingId,

                        reason
                    }
                );

            if (
                result.success !==
                true
            ) {
                throw new Error(
                    result.message ||
                    config.fallbackError
                );
            }

            closeSweetAlert();

            await showMessage({
                icon:
                    "success",

                title:
                    config.successTitle,

                text:
                    result.message ||
                    "تم تحديث الحجز بنجاح.",

                confirmButtonText:
                    "حسنًا",

                confirmButtonColor:
                    "#00A6DA",

                returnFocus:
                    false
            });

            window.location.reload();
        } catch (error) {
            closeSweetAlert();

            console.error(
                `${config.endpoint} error:`,
                error
            );

            await showMessage({
                icon:
                    "error",

                title:
                    config.fallbackError,

                text:
                    error.message ||
                    config.fallbackError,

                confirmButtonText:
                    "حسنًا",

                confirmButtonColor:
                    "#EF4444",

                returnFocus:
                    false
            });
        } finally {
            state.actionRunning =
                false;
        }
    }

    /* =====================================================================
       قبول ورفض الطلبات ومراجعة الدفع
       ===================================================================== */

    window.executeAcceptBooking =
        bookingId =>
            executeSimpleAction({
                bookingId,

                endpoint:
                    ENDPOINTS.accept,

                icon:
                    "question",

                title:
                    "قبول طلب الحجز؟",

                text:
                    "سيتم فحص التوفر مرة أخرى، ثم تأكيد الحجز أو تحويله إلى انتظار الدفع.",

                confirmText:
                    "نعم، قبول الطلب",

                confirmColor:
                    "#10B981",

                loadingTitle:
                    "جاري قبول الحجز",

                loadingText:
                    "يتم فحص التوفر وتحديث حالة الطلب.",

                successTitle:
                    "تم قبول الطلب"
            });

    window.executeModalReject =
        bookingId =>
            executeReasonAction({
                bookingId,

                endpoint:
                    ENDPOINTS
                        .rejectPending,

                dialog: {
                    title:
                        "رفض طلب الحجز",

                    text:
                        "اكتب سببًا واضحًا وسيظهر للعميل داخل تفاصيل الحجز.",

                    placeholder:
                        "مثال: لا توجد وحدات متاحة خلال التاريخ المطلوب.",

                    ariaLabel:
                        "سبب رفض طلب الحجز",

                    confirmText:
                        "رفض الطلب وإبلاغ العميل"
                },

                loadingTitle:
                    "جاري رفض الطلب",

                loadingText:
                    "يتم تحديث السجل وإبلاغ العميل بالسبب.",

                successTitle:
                    "تم رفض الطلب",

                fallbackError:
                    "تعذر رفض طلب الحجز."
            });

    window.executeApprovePayment =
        bookingId => {
            const details =
                state.currentBooking ||
                {};

            const fullPayment =
                isFullOnlinePayment(
                    details
                );

            const paymentLabel =
                fullPayment
                    ? "كامل قيمة الحجز"
                    : "العربون";

            const expectedAmount =
                details.expectedOnlineAmount ||
                details.depositAmount ||
                "غير محدد";

            return executeSimpleAction({
                bookingId,

                endpoint:
                    ENDPOINTS
                        .approvePayment,

                icon:
                    "warning",

                title:
                    fullPayment
                        ? "اعتماد كامل قيمة الحجز؟"
                        : "اعتماد العربون؟",

                text:
                    `تأكد أن المبلغ الظاهر في الحوالة يطابق ${expectedAmount} قبل الاعتماد.`,

                html: `
                    <div class="reservation-payment-approval-dialog" dir="rtl">

                        <div class="reservation-payment-approval-summary">

                            <span>
                                <small>نوع الدفع</small>
                                <strong>
                                    ${escapeHtml(
                    getOnlinePaymentTypeLabel(
                        details
                    )
                )}
                                </strong>
                            </span>

                            <span>
                                <small>المبلغ المطلوب</small>
                                <strong>
                                    ${escapeHtml(
                    expectedAmount
                )}
                                </strong>
                            </span>

                            <span>
                                <small>المتبقي بعد الاعتماد</small>
                                <strong>
                                    ${escapeHtml(
                    details.expectedRemaining ||
                    "0 ريال يمني"
                )}
                                </strong>
                            </span>

                        </div>

                        <label class="reservation-payment-approval-check">

                            <input type="checkbox"
                                   id="reservationPaymentAmountVerified" />

                            <span>
                                تحققت من صورة الحوالة، والمبلغ الظاهر فيها
                                يطابق المبلغ المطلوب أعلاه.
                            </span>

                        </label>

                        <div class="reservation-payment-approval-warning">
                            <i class="bi bi-exclamation-triangle"></i>
                            <span>
                                إذا كان المبلغ أقل أو أكثر من المطلوب، ارفض
                                الإثبات ولا تعتمد الحوالة.
                            </span>
                        </div>

                    </div>
                `,

                preConfirm:
                    () => {
                        const checkbox =
                            document.getElementById(
                                "reservationPaymentAmountVerified"
                            );

                        if (!checkbox?.checked) {
                            window.Swal
                                ?.showValidationMessage(
                                    "أكد مطابقة مبلغ الحوالة أولًا."
                                );

                            return false;
                        }

                        return true;
                    },

                confirmText:
                    fullPayment
                        ? "اعتماد كامل المبلغ"
                        : "اعتماد العربون",

                confirmColor:
                    "#10B981",

                loadingTitle:
                    "جاري اعتماد الدفع",

                loadingText:
                    `يتم اعتماد ${paymentLabel} وتحديث السجل المالي والحجز.`,

                successTitle:
                    fullPayment
                        ? "تم اعتماد كامل قيمة الحجز"
                        : "تم اعتماد العربون"
            });
        };

    window.executePaymentReject =
        bookingId =>
            executeReasonAction({
                bookingId,

                endpoint:
                    ENDPOINTS
                        .rejectPayment,

                dialog: {
                    title:
                        "رفض إشعار الدفع",

                    text:
                        "هذا الإجراء يرفض الإثبات فقط ويعيد الحجز إلى انتظار الدفع حتى يستطيع العميل رفع إثبات جديد.",

                    placeholder:
                        "مثال: الصورة غير واضحة أو الرقم المرجعي غير مطابق.",

                    ariaLabel:
                        "سبب رفض إشعار الدفع",

                    confirmText:
                        "رفض الإشعار وإعادة الدفع",

                    confirmColor:
                        "#F59E0B"
                },

                loadingTitle:
                    "جاري رفض إشعار الدفع",

                loadingText:
                    "يتم إعادة الحجز إلى انتظار الدفع ومنح العميل مهلة جديدة.",

                successTitle:
                    "تم رفض الإشعار",

                fallbackError:
                    "تعذر رفض إشعار الدفع."
            });

    window.executePermanentReject =
        bookingId => {
            const details =
                state.currentBooking ||
                {};

            const warning =
                details.bookingState ===
                    "PaymentReview"
                    ? "سيُرفض الحجز كاملًا، وليس إثبات الدفع فقط. لن يستطيع العميل رفع إثبات جديد لهذا الحجز."
                    : "سيُغلق الحجز نهائيًا وينتقل إلى سجل الحجوزات المرفوضة.";

            return executeReasonAction({
                bookingId,

                endpoint:
                    ENDPOINTS
                        .rejectPermanently,

                dialog: {
                    title:
                        "رفض الحجز نهائيًا",

                    html: `
                        <div dir="rtl">

                            <p>
                                ${escapeHtml(
                        warning
                    )}
                            </p>

                            <div class="reservation-danger-dialog-note">

                                <i class="bi bi-exclamation-triangle"></i>

                                <span>
                                    استخدم «رفض إشعار الدفع» فقط عندما تريد السماح للعميل برفع إثبات جديد.
                                </span>

                            </div>

                        </div>
                    `,

                    placeholder:
                        "اكتب سبب رفض الحجز كاملًا...",

                    ariaLabel:
                        "سبب رفض الحجز نهائيًا",

                    confirmText:
                        "رفض الحجز نهائيًا",

                    confirmColor:
                        "#B91C1C"
                },

                loadingTitle:
                    "جاري رفض الحجز",

                loadingText:
                    "يتم إغلاق الحجز نهائيًا وإبلاغ العميل.",

                successTitle:
                    "تم رفض الحجز نهائيًا",

                fallbackError:
                    "تعذر رفض الحجز نهائيًا."
            });
        };

    /* =====================================================================
       إنهاء الحجز والتحصيل
       ===================================================================== */

    async function requestCollectionAmount(
        details
    ) {
        const remaining =
            Math.max(
                0,
                details
                    .propertyAmountRaw ||
                0
            );

        if (!hasSweetAlert()) {
            const value =
                window.prompt(
                    `المبلغ المتبقي: ${formatMoney(
                        remaining
                    )} ريال\nأدخل المبلغ المحصل فعليًا:`,
                    String(remaining)
                );

            if (value === null) {
                return null;
            }

            const amount =
                parseNumber(
                    value,
                    -1
                );

            if (
                amount < 0 ||
                amount > remaining
            ) {
                window.alert(
                    "المبلغ المحصل يجب أن يكون بين صفر والمبلغ المتبقي."
                );

                return null;
            }

            return amount;
        }

        const result =
            await window.Swal.fire({
                icon:
                    "question",

                title:
                    "تحصيل وإنهاء الحجز",

                html: `
                    <div dir="rtl">

                        <p>
                            أدخل المبلغ الذي تم تحصيله فعليًا عند المنشأة.
                        </p>

                        <div class="reservation-collection-summary">

                            <span>
                                المتبقي المسجل
                            </span>

                            <strong>
                                ${formatMoney(
                    remaining
                )}
                                ريال يمني
                            </strong>

                        </div>

                    </div>
                `,

                input:
                    "number",

                inputLabel:
                    "المبلغ المحصل فعليًا",

                inputValue:
                    remaining,

                inputAttributes: {
                    min:
                        "0",

                    max:
                        String(
                            remaining
                        ),

                    step:
                        "0.01",

                    inputmode:
                        "decimal",

                    dir:
                        "ltr"
                },

                showCancelButton:
                    true,

                confirmButtonText:
                    "متابعة الإنهاء",

                cancelButtonText:
                    "تراجع",

                confirmButtonColor:
                    "#10B981",

                cancelButtonColor:
                    "#64748B",

                reverseButtons:
                    true,

                allowOutsideClick:
                    false,

                returnFocus:
                    false,

                inputValidator:
                    value => {
                        const amount =
                            parseNumber(
                                value,
                                -1
                            );

                        if (amount < 0) {
                            return "أدخل مبلغًا صحيحًا لا يقل عن صفر.";
                        }

                        if (
                            amount >
                            remaining
                        ) {
                            return `المبلغ لا يمكن أن يتجاوز ${formatMoney(
                                remaining
                            )} ريال.`;
                        }

                        return undefined;
                    }
            });

        return result.isConfirmed
            ? parseNumber(
                result.value,
                0
            )
            : null;
    }

    async function confirmPartialCollection(
        remainingAfterCollection
    ) {
        const message =
            `سيبقى مبلغ ${formatMoney(
                remainingAfterCollection
            )} ريال غير محصل بعد إغلاق الحجز.`;

        if (!hasSweetAlert()) {
            return window.confirm(
                `${message}\nهل تريد الإنهاء رغم وجود مبلغ متبقٍ؟`
            );
        }

        const result =
            await window.Swal.fire({
                icon:
                    "warning",

                title:
                    "يوجد مبلغ غير محصل",

                html: `
                    <div dir="rtl">

                        <p>
                            ${escapeHtml(
                    message
                )}
                        </p>

                        <p class="text-danger fw-bold mb-0">
                            سيُسجل الحجز كمكتمل، لكن حالته المالية ستبقى مدفوعة جزئيًا.
                        </p>

                    </div>
                `,

                showCancelButton:
                    true,

                confirmButtonText:
                    "إنهاء مع رصيد متبقٍ",

                cancelButtonText:
                    "العودة وتعديل المبلغ",

                confirmButtonColor:
                    "#F59E0B",

                cancelButtonColor:
                    "#64748B",

                reverseButtons:
                    true,

                returnFocus:
                    false
            });

        return result.isConfirmed;
    }

    window.executeCompleteBooking =
        async bookingId => {
            if (state.actionRunning) {
                return;
            }

            const id =
                parsePositiveId(
                    bookingId
                );

            const details =
                state.currentBooking ||
                {};

            if (!id) {
                await showMessage({
                    icon:
                        "error",

                    title:
                        "رقم الحجز غير صحيح"
                });

                return;
            }

            if (
                details.canComplete ===
                false
            ) {
                await closeReservationModal();

                await showMessage({
                    icon:
                        "warning",

                    title:
                        "لا يمكن إنهاء الحجز الآن",

                    text:
                        details.actionNote ||
                        "لم ينته موعد الحجز بعد.",

                    confirmButtonColor:
                        "#00A6DA"
                });

                return;
            }

            await closeReservationModal();

            const collectedAmount =
                await requestCollectionAmount(
                    details
                );

            if (
                collectedAmount ===
                null
            ) {
                return;
            }

            state.actionRunning =
                true;

            showLoading(
                "جاري إنهاء الحجز",
                "يتم تسجيل المبلغ المحصل وإغلاق الحجز."
            );

            try {
                let result =
                    await postForm(
                        ENDPOINTS.complete,
                        {
                            id,

                            collectedAmount,

                            confirmPartialCollection:
                                false
                        }
                    );

                if (
                    result
                        .requiresPartialConfirmation ===
                    true
                ) {
                    closeSweetAlert();

                    const confirmed =
                        await confirmPartialCollection(
                            result
                                .remainingAfterCollection
                        );

                    if (!confirmed) {
                        return;
                    }

                    showLoading(
                        "جاري إنهاء الحجز",
                        "يتم إغلاق الحجز مع تسجيل الرصيد المتبقي."
                    );

                    result =
                        await postForm(
                            ENDPOINTS.complete,
                            {
                                id,

                                collectedAmount,

                                confirmPartialCollection:
                                    true
                            }
                        );
                }

                if (
                    result.success !==
                    true
                ) {
                    throw new Error(
                        result.message ||
                        "تعذر إنهاء الحجز."
                    );
                }

                closeSweetAlert();

                await showMessage({
                    icon:
                        "success",

                    title:
                        "تم إنهاء الحجز",

                    html: `
                        <div dir="rtl">

                            <p>
                                ${escapeHtml(
                        result.message ||
                        "تم إنهاء الحجز بنجاح."
                    )}
                            </p>

                            <div class="reservation-collection-summary">

                                <span>
                                    المبلغ المحصل
                                </span>

                                <strong>
                                    ${formatMoney(
                        result
                            .amountCollectedAtProperty ??
                        collectedAmount
                    )}
                                    ريال
                                </strong>

                            </div>

                            <div class="reservation-collection-summary">

                                <span>
                                    المتبقي بعد الإنهاء
                                </span>

                                <strong>
                                    ${formatMoney(
                        result
                            .remainingAfterCollection
                    )}
                                    ريال
                                </strong>

                            </div>

                        </div>
                    `,

                    confirmButtonText:
                        "حسنًا",

                    confirmButtonColor:
                        "#00A6DA",

                    returnFocus:
                        false
                });

                window.location.reload();
            } catch (error) {
                closeSweetAlert();

                console.error(
                    "Complete booking error:",
                    error
                );

                await showMessage({
                    icon:
                        "error",

                    title:
                        "تعذر إنهاء الحجز",

                    text:
                        error.message ||
                        "حدث خطأ أثناء إنهاء الحجز.",

                    confirmButtonColor:
                        "#EF4444",

                    returnFocus:
                        false
                });
            } finally {
                state.actionRunning =
                    false;
            }
        };

    /* =====================================================================
       عدم الحضور وإلغاء المالك
       ===================================================================== */

    window.executeNoShowBooking =
        async bookingId => {
            const details =
                state.currentBooking ||
                {};

            if (
                details.canNoShow ===
                false
            ) {
                await closeReservationModal();

                await showMessage({
                    icon:
                        "warning",

                    title:
                        "لا يمكن تسجيل عدم حضور الآن",

                    text:
                        details.actionNote ||
                        "لم تنته مهلة السماح بعد.",

                    confirmButtonColor:
                        "#00A6DA"
                });

                return;
            }

            return executeSimpleAction({
                bookingId,

                endpoint:
                    ENDPOINTS.noShow,

                values: {
                    confirmed:
                        true
                },

                icon:
                    "warning",

                title:
                    "تسجيل عدم حضور العميل؟",

                text:
                    isFullOnlinePayment(details)
                        ? "أكد أن العميل لم يحضر بعد انتهاء مهلة السماح. إذا كانت السياسة تصادر العربون فسيتم الاحتفاظ بالعربون فقط، وأي مبلغ مدفوع فوقه سيُنشأ له استرداد تلقائي."
                        : "أكد أن العميل لم يحضر بعد انتهاء مهلة السماح. ستُطبق سياسة المنشأة على العربون.",

                confirmText:
                    "تأكيد عدم الحضور",

                confirmColor:
                    "#F59E0B",

                loadingTitle:
                    "جاري تسجيل عدم الحضور",

                loadingText:
                    "يتم إغلاق الحجز وتسوية حالته المالية.",

                successTitle:
                    "تم تسجيل عدم الحضور"
            });
        };

    window.executeOwnerCancellation =
        bookingId => {
            const details =
                state.currentBooking ||
                {};

            const paidOnline =
                Math.max(
                    0,
                    details
                        .onlineAmountRaw ||
                    0
                );

            return executeReasonAction({
                bookingId,

                endpoint:
                    ENDPOINTS
                        .ownerCancel,

                dialog: {
                    icon:
                        "warning",

                    title:
                        "إلغاء الحجز من جهة المالك",

                    html: `
                        <div dir="rtl">

                            <p>
                                سيُلغى الحجز من جهة المنشأة، ولن تُطبق على العميل أي غرامة إلغاء.
                            </p>

                            <div class="reservation-refund-preview">

                                <span>
                                    المبلغ المتوقع استرداده للعميل
                                </span>

                                <strong>
                                    ${formatMoney(
                        paidOnline
                    )}
                                    ريال يمني
                                </strong>

                            </div>

                            <div class="reservation-danger-dialog-note">

                                <i class="bi bi-exclamation-triangle"></i>

                                <span>
                                    هذا الإجراء نهائي وسيظهر للعميل أن المنشأة هي من ألغت الحجز.
                                </span>

                            </div>

                        </div>
                    `,

                    inputLabel:
                        "سبب إلغاء المنشأة",

                    placeholder:
                        "مثال: ظرف طارئ أو تعذر توفير الوحدة المحجوزة.",

                    ariaLabel:
                        "سبب إلغاء الحجز من جهة المالك",

                    confirmText:
                        "إلغاء الحجز وإنشاء الاسترداد",

                    confirmColor:
                        "#B91C1C"
                },

                loadingTitle:
                    "جاري إلغاء الحجز",

                loadingText:
                    "يتم إغلاق الحجز وإنشاء طلب الاسترداد عند الحاجة.",

                successTitle:
                    "تم إلغاء الحجز",

                fallbackError:
                    "تعذر إلغاء الحجز من جهة المالك."
            });
        };

    /* =====================================================================
       المحادثة
       ===================================================================== */

    window.openDirectChat =
        customerId => {
            const id =
                parsePositiveId(
                    customerId
                );

            if (!id) {
                showMessage({
                    icon:
                        "error",

                    title:
                        "تعذر فتح المحادثة",

                    text:
                        "رقم العميل غير صحيح."
                });

                return;
            }

            window.location.href =
                `/Owner/Chat/ChatInterface?communicationTab=messages&withUserId=${encodeURIComponent(
                    id
                )}`;
        };

    /* =====================================================================
       Deep Link من إشعارات المالك
       - bookingId يفتح الحجز نفسه.
       - reservationTab يحدد التبويب المفضّل إن توفر.
       - إذا كان الرابط قديمًا ولا يحمل التبويب الصحيح، نبحث في التبويبات
         الثلاثة تلقائيًا حتى نجد الحجز بدل إبقاء المستخدم في "الحجوزات الجديدة".
       ===================================================================== */

    function readReservationDeepLink() {
        const params =
            new URLSearchParams(
                window.location.search || ""
            );

        const bookingId =
            parsePositiveId(
                params.get("bookingId")
            );

        const requestedTab =
            params.get("reservationTab");

        return {
            bookingId,
            requestedTab:
                requestedTab &&
                Object.prototype.hasOwnProperty.call(
                    TAB_ENDPOINTS,
                    requestedTab
                )
                    ? requestedTab
                    : null
        };
    }

    function clearReservationDeepLink() {
        const url =
            new URL(
                window.location.href
            );

        url.searchParams.delete(
            "bookingId"
        );
        url.searchParams.delete(
            "reservationTab"
        );

        window.history.replaceState(
            window.history.state,
            "",
            `${url.pathname}${url.search}${url.hash}`
        );
    }

    async function openBookingFromDeepLink(
        bookingId,
        preferredTab = null
    ) {
        const id =
            parsePositiveId(
                bookingId
            );

        if (!id) {
            return false;
        }

        const searchableTabs = [
            "pending-requests-tab",
            "active-reservations-tab",
            "reservation-history-tab"
        ];

        const tabs =
            preferredTab &&
            searchableTabs.includes(
                preferredTab
            )
                ? [
                    preferredTab,
                    ...searchableTabs.filter(
                        tab => tab !== preferredTab
                    )
                ]
                : searchableTabs;

        for (const tab of tabs) {
            await loadReservationTab(
                tab
            );

            const panel =
                getTabElement(
                    tab
                );

            const button =
                panel?.querySelector(
                    `.compact-details-button[data-id="${id}"]`
                );

            if (!button) {
                continue;
            }

            button.scrollIntoView({
                behavior:
                    window.matchMedia(
                        "(prefers-reduced-motion: reduce)"
                    ).matches
                        ? "auto"
                        : "smooth",
                block: "center"
            });

            if (
                typeof window
                    .openPremiumCenterModal ===
                "function"
            ) {
                window.setTimeout(
                    () => window
                        .openPremiumCenterModal(
                            button
                        ),
                    180
                );
            }

            clearReservationDeepLink();
            return true;
        }

        return false;
    }

    /* =====================================================================
       تشغيل الصفحة
       ===================================================================== */

    function initializeModalCleanup() {
        const modalElement =
            getReservationModalElement();

        if (
            !modalElement ||
            modalElement.dataset
                .cleanupInitialized ===
            "true"
        ) {
            return;
        }

        modalElement.dataset
            .cleanupInitialized =
            "true";

        modalElement.addEventListener(
            "hidden.bs.modal",
            cleanupReservationModalState
        );
    }

    function initializePage() {
        if (state.initialized) {
            return;
        }

        state.initialized =
            true;

        initializeTabNavigation();
        initializeRefreshButton();
        initializeRetryDelegation();
        initializeModalCleanup();

        const deepLink =
            readReservationDeepLink();

        if (deepLink.bookingId) {
            openBookingFromDeepLink(
                deepLink.bookingId,
                deepLink.requestedTab
            ).then(found => {
                if (!found) {
                    console.warn(
                        "Booking deep link was not found in the owner reservation tabs:",
                        deepLink.bookingId
                    );
                }
            });

            return;
        }

        const savedTab =
            getValidTab(
                localStorage.getItem(
                    "activeReservationTab"
                ) ||
                DEFAULT_TAB
            );

        loadReservationTab(
            savedTab
        );
    }

    if (
        document.readyState ===
        "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            initializePage,
            {
                once: true
            }
        );
    } else {
        initializePage();
    }
})();
