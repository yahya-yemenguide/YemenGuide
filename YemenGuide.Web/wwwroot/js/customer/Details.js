/**
* ============================================================================
* YEMENGUIDE — PLACE DETAILS
* جميع وظائف صفحة تفاصيل المنشأة مع الحفاظ على الواجهة الفخمة
* ============================================================================
*/
(() => {
    "use strict";

    const AD_ATTRIBUTION_ID_KEY = "ygAdvertisementAttributionId";
    const AD_ATTRIBUTION_PLACE_KEY = "ygAdvertisementAttributionPlaceId";

    const SELECTORS = {
        mobileBookingBar: ".sticky-bottom-booking",
        reviewsContainer: "#smartReviewsContainer",
        reviewsTrigger: "#reviewsSectionTrigger",
        reviewsIcon: "#reviewsChevronIcon",
        reviewsText: "#reviewsToggleText",
        reviewInput: "#instaReplyInput",
        postButton: "#instaPostBtn",
        ratingInput: "#userRatingValue",
        parentReviewInput: "#instaParentReviewId",
        editReviewInput: "#instaEditModeId"
    };

    const state = {
        hotelCalendars: [],
        packageCalendar: null,
        packageDate: "",
        packageAvailabilityCheckInFlight: false,
        selectedServiceIds: new Set(),
        promotionAutoServiceIds: new Set(),

        currentPackage: {
            id: 0,
            name: "",
            price: "",
            capacityText: "",
            time: "",
            maxCapacity: 0,
            promotionId: 0,
            promotionTargetAnchor: ""
        },

        selectedSeatingSetup: "مجلس عربي",

        galleryImages: [],
        selectedGalleryIndex: 0,

        reviewsOpen: false,
        reviewsScrollY: 0,
        bodyLockedByReviews: false,
        reviewsPortalParent: null,
        reviewsPortalNextSibling: null,
        reviewsPortaledToBody: false,

        selectedPromotionId: 0
    };

    window.bookingData =
        window.bookingData || {
            adults: 0,
            children: 0
        };

    window.lastValidAdults = 0;
    window.lastValidChildren = 0;
    window.currentPackageMaxCapacity = 0;

    function isCustomerAuthenticated() {
        return (
            getValue(
                "customerAuthenticatedInput",
                "false"
            ).toLowerCase() ===
            "true"
        );
    }

    function buildLoginUrlForReturn(
        returnUrl
    ) {
        const loginUrl =
            new URL(
                "/Account/Login",
                window.location.origin
            );

        loginUrl.searchParams.set(
            "returnUrl",
            returnUrl
        );

        return (
            loginUrl.pathname +
            loginUrl.search
        );
    }

    async function ensureLoginBeforeBooking(
        checkoutUrl
    ) {
        if (isCustomerAuthenticated()) {
            return true;
        }

        await notify({
            icon:
                "info",

            title:
                "تسجيل الدخول مطلوب",

            text:
                "يجب تسجيل الدخول بحساب عميل لإكمال هذه العملية.",

            confirmButtonText:
                "موافق",

            showConfirmButton:
                true,

            showCancelButton:
                false,

            allowOutsideClick:
                false,

            allowEscapeKey:
                false
        });

        window.location.href =
            buildLoginUrlForReturn(
                checkoutUrl
            );

        return false;
    }

    function buildReviewResumeReturnUrl() {
        const url =
            new URL(
                window.location.href
            );

        url.searchParams.set(
            "resumeReview",
            "1"
        );

        url.hash =
            "reviewsSectionTrigger";

        return (
            url.pathname +
            url.search +
            url.hash
        );
    }

    function savePendingReviewDraft(
        placeId,
        text,
        rating,
        parentId,
        editId
    ) {
        storageSet(
            "pendingReviewPlaceId",
            String(placeId || 0)
        );

        storageSet(
            "pendingReviewText",
            text || ""
        );

        storageSet(
            "pendingReviewRating",
            String(rating || 0)
        );

        storageSet(
            "pendingReviewParentId",
            parentId || ""
        );

        storageSet(
            "pendingReviewEditId",
            editId || ""
        );
    }

    function clearPendingReviewDraft() {
        [
            "pendingReviewPlaceId",
            "pendingReviewText",
            "pendingReviewRating",
            "pendingReviewParentId",
            "pendingReviewEditId"
        ].forEach(
            key =>
                storageRemove(
                    key
                )
        );
    }

    async function ensureLoginBeforeReview(
        placeId,
        text,
        rating,
        parentId,
        editId
    ) {
        if (isCustomerAuthenticated()) {
            return true;
        }

        savePendingReviewDraft(
            placeId,
            text,
            rating,
            parentId,
            editId
        );

        await notify({
            icon:
                "info",

            title:
                "تسجيل الدخول مطلوب",

            text:
                "يجب تسجيل الدخول بحساب عميل لإكمال التقييم أو الرد.",

            confirmButtonText:
                "موافق",

            showConfirmButton:
                true,

            showCancelButton:
                false,

            allowOutsideClick:
                false,

            allowEscapeKey:
                false
        });

        window.location.assign(
            buildLoginUrlForReturn(
                buildReviewResumeReturnUrl()
            )
        );

        return false;
    }

    function resumePendingReviewAfterLogin() {
        const url =
            new URL(
                window.location.href
            );

        if (
            url.searchParams.get(
                "resumeReview"
            ) !== "1" ||
            !isCustomerAuthenticated()
        ) {
            return;
        }

        const currentPlaceId =
            getValue(
                "placeIdInput",
                "0"
            );

        const savedPlaceId =
            storageGet(
                "pendingReviewPlaceId"
            );

        if (
            savedPlaceId &&
            currentPlaceId &&
            savedPlaceId !== currentPlaceId
        ) {
            clearPendingReviewDraft();
            return;
        }

        const input =
            qs(
                SELECTORS.reviewInput
            );

        const ratingInput =
            qs(
                SELECTORS.ratingInput
            );

        const parentInput =
            getElement(
                "instaParentReviewId"
            );

        const editInput =
            getElement(
                "instaEditModeId"
            );

        const savedText =
            storageGet(
                "pendingReviewText"
            ) || "";

        const savedRating =
            Number.parseInt(
                storageGet(
                    "pendingReviewRating"
                ) || "0",
                10
            ) || 0;

        if (input) {
            input.value =
                savedText;
        }

        if (parentInput) {
            parentInput.value =
                storageGet(
                    "pendingReviewParentId"
                ) || "";
        }

        if (editInput) {
            editInput.value =
                storageGet(
                    "pendingReviewEditId"
                ) || "";
        }

        if (
            ratingInput &&
            savedRating > 0
        ) {
            ratingInput.value =
                String(savedRating);

            const matchingStar =
                qs(
                    `.star-item[data-value="${savedRating}"]`
                );

            matchingStar?.click();
        }

        setReviewsOpen(
            true
        );

        url.searchParams.delete(
            "resumeReview"
        );

        window.history.replaceState(
            window.history.state,
            "",
            `${url.pathname}${url.search}#reviewsSectionTrigger`
        );

        clearPendingReviewDraft();

        window.setTimeout(
            () => {
                getElement(
                    "reviewsSectionTrigger"
                )?.scrollIntoView({
                    behavior:
                        "smooth",

                    block:
                        "start"
                });

                input?.focus({
                    preventScroll:
                        true
                });
            },
            180
        );
    }

    /* ======================================================================
       01. أدوات عامة
       ====================================================================== */

    const qs = (selector, scope = document) =>
        scope.querySelector(selector);

    const qsa = (selector, scope = document) =>
        Array.from(
            scope.querySelectorAll(selector)
        );

    function getElement(id) {
        return document.getElementById(id);
    }

    function getValue(
        id,
        fallback = ""
    ) {
        const element =
            getElement(id);

        return element
            ? element.value
            : fallback;
    }

    function setText(
        target,
        value
    ) {
        const element =
            typeof target === "string"
                ? qs(target)
                : target;

        if (element) {
            element.textContent =
                String(value ?? "");
        }
    }

    function toNumber(
        value,
        fallback = 0
    ) {
        let text =
            String(value ?? "")
                .trim()
                .replace(/\s/g, "")
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
            if (
                lastComma > lastDot
            ) {
                text = text
                    .replace(/\./g, "")
                    .replace(",", ".");
            } else {
                text =
                    text.replace(/,/g, "");
            }
        } else if (
            lastComma >= 0
        ) {
            const parts =
                text.split(",");

            if (
                parts.length === 2 &&
                parts[1].length > 0 &&
                parts[1].length <= 2
            ) {
                text =
                    `${parts[0]}.${parts[1]}`;
            } else {
                text =
                    text.replace(/,/g, "");
            }
        }

        const parsed =
            Number.parseFloat(text);

        return Number.isFinite(parsed)
            ? parsed
            : fallback;
    }

    function formatMoney(value) {
        return Math.round(
            toNumber(value, 0)
        ).toLocaleString("en-US");
    }

    function parseCoordinate(
        value,
        fallback
    ) {
        const normalized =
            String(value ?? "")
                .trim()
                .replace(/\s/g, "")
                .replace(",", ".")
                .replace(
                    /[^0-9.-]/g,
                    ""
                );

        const parsed =
            Number.parseFloat(normalized);

        return Number.isFinite(parsed)
            ? parsed
            : fallback;
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    /*
     * نستخدم sessionStorage فقط لحالة الواجهة:
     * إعادة فتح نافذة التاريخ أو التقييمات.
     * بيانات الحجز نفسها تمر في الرابط إلى الخادم.
     */
    function storageGet(key) {
        try {
            return sessionStorage
                .getItem(key);
        } catch {
            return null;
        }
    }

    function storageSet(
        key,
        value
    ) {
        try {
            sessionStorage.setItem(
                key,
                String(value)
            );
        } catch (error) {
            console.warn(
                `تعذر حفظ ${key}:`,
                error
            );
        }
    }

    function storageRemove(key) {
        try {
            sessionStorage
                .removeItem(key);
        } catch {
            // تجاهل
        }
    }

    function clearAdvertisementAttribution() {
        storageRemove(AD_ATTRIBUTION_ID_KEY);
        storageRemove(AD_ATTRIBUTION_PLACE_KEY);
    }

    function syncAdvertisementAttribution() {
        const page =
            qs(".place-details-page");

        if (!page) {
            return;
        }

        const pagePlaceId =
            Number.parseInt(
                page.dataset.attributionPlaceId ||
                getValue("placeIdInput", "0"),
                10
            ) || 0;

        const validatedAdvertisementId =
            Number.parseInt(
                page.dataset.attributionAdvertisementId || "0",
                10
            ) || 0;

        if (
            validatedAdvertisementId > 0 &&
            pagePlaceId > 0
        ) {
            storageSet(
                AD_ATTRIBUTION_ID_KEY,
                validatedAdvertisementId
            );
            storageSet(
                AD_ATTRIBUTION_PLACE_KEY,
                pagePlaceId
            );
            return;
        }

        // لا نسمح بإعادة استخدام إعلان منشأة عند الانتقال لمنشأة أخرى.
        const storedPlaceId =
            Number.parseInt(
                storageGet(AD_ATTRIBUTION_PLACE_KEY) || "0",
                10
            ) || 0;

        if (
            pagePlaceId > 0 &&
            storedPlaceId > 0 &&
            storedPlaceId !== pagePlaceId
        ) {
            clearAdvertisementAttribution();
        }
    }

    function getAdvertisementAttribution(placeId) {
        const normalizedPlaceId =
            Number.parseInt(String(placeId || "0"), 10) || 0;

        const advertisementId =
            Number.parseInt(
                storageGet(AD_ATTRIBUTION_ID_KEY) || "0",
                10
            ) || 0;

        const storedPlaceId =
            Number.parseInt(
                storageGet(AD_ATTRIBUTION_PLACE_KEY) || "0",
                10
            ) || 0;

        if (
            normalizedPlaceId <= 0 ||
            advertisementId <= 0 ||
            storedPlaceId !== normalizedPlaceId
        ) {
            return 0;
        }

        return advertisementId;
    }

    function recordAdvertisementBookingStart(placeId) {
        const advertisementId =
            getAdvertisementAttribution(placeId);

        if (advertisementId <= 0) {
            return;
        }

        const api =
            window.CustomerAdvertisements;

        if (
            api &&
            typeof api.recordBookingStarted === "function"
        ) {
            void api.recordBookingStarted(
                advertisementId,
                placeId
            );
        }
    }

    function initializeAdvertisementAttribution() {
        syncAdvertisementAttribution();

        document.addEventListener(
            "click",
            event => {
                const target =
                    event.target.closest(
                        "[data-customer-booking-start]"
                    );

                if (!target) {
                    return;
                }

                const placeId =
                    getRequiredPlaceId();

                if (placeId > 0) {
                    recordAdvertisementBookingStart(
                        placeId
                    );
                }
            }
        );
    }

    function getAntiForgeryToken() {
        const tokenInput =
            qs(
                'input[name="__RequestVerificationToken"]'
            );

        return tokenInput
            ? tokenInput.value
            : "";
    }

    function buildHeaders(
        contentType
    ) {
        const headers = {};

        const token =
            getAntiForgeryToken();

        if (contentType) {
            headers["Content-Type"] =
                contentType;
        }

        if (token) {
            headers.RequestVerificationToken =
                token;
        }

        return headers;
    }

    async function parseResponse(
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
                success: false,
                requiresLogin: true,
                loginUrl:
                    buildLoginUrlForReturn(
                        window.location.pathname +
                        window.location.search +
                        window.location.hash
                    ),
                message:
                    "يجب تسجيل الدخول أولًا لإكمال العملية."
            };
        }

        let data;

        if (
            contentType.includes(
                "application/json"
            )
        ) {
            data =
                await response.json();
        } else {
            const text =
                await response.text();

            try {
                data =
                    JSON.parse(text);
            } catch {
                data = {
                    success:
                        response.ok,

                    message:
                        response.ok
                            ? "تمت العملية بنجاح."
                            : "تعذر تنفيذ العملية."
                };
            }
        }

        if (
            !response.ok &&
            !data?.message
        ) {
            data = {
                ...(data || {}),
                success: false,
                message:
                    "حدث خطأ أثناء الاتصال بالخادم."
            };
        }

        return data;
    }

    function notify(options) {
        if (
            typeof window.Swal !==
            "undefined"
        ) {
            return window.Swal
                .fire(options);
        }

        window.alert(
            options.text ||
            options.title ||
            "حدث تنبيه."
        );

        return Promise.resolve({
            isConfirmed: true
        });
    }

    function showToast(
        message,
        icon = "success"
    ) {
        return notify({
            toast: true,
            position: "top-end",
            icon,
            title: message,
            showConfirmButton: false,
            timer: 2000,
            timerProgressBar: true
        });
    }

    function safeRun(
        name,
        callback
    ) {
        try {
            callback();
        } catch (error) {
            console.error(
                `خطأ في ${name}:`,
                error
            );
        }
    }

    function getBootstrapModal(
        element
    ) {
        if (
            !element ||
            typeof window.bootstrap ===
            "undefined" ||
            !window.bootstrap.Modal
        ) {
            return null;
        }

        return window.bootstrap
            .Modal
            .getOrCreateInstance(
                element
            );
    }

    function showModalById(id) {
        const element =
            getElement(id);

        const modal =
            getBootstrapModal(element);

        if (!modal) {
            console.error(
                `تعذر فتح المودال #${id}`
            );

            notify({
                icon: "error",
                title:
                    "تعذر فتح النافذة",
                text:
                    "تأكد من تحميل bootstrap.bundle.min.js داخل الـ Layout."
            });

            return null;
        }

        modal.show();

        return modal;
    }

    function switchModal(
        fromId,
        toId
    ) {
        const fromElement =
            getElement(fromId);

        const toElement =
            getElement(toId);

        const fromModal =
            getBootstrapModal(
                fromElement
            );

        const toModal =
            getBootstrapModal(
                toElement
            );

        if (!toModal) {
            notify({
                icon: "error",
                title:
                    "تعذر فتح النافذة",
                text:
                    "النافذة المطلوبة غير موجودة أو Bootstrap غير محمل."
            });

            return;
        }

        if (
            !fromElement ||
            !fromElement.classList
                .contains("show") ||
            !fromModal
        ) {
            toModal.show();
            return;
        }

        fromElement.addEventListener(
            "hidden.bs.modal",
            () => {
                window.setTimeout(
                    () => {
                        toModal.show();
                    },
                    80
                );
            },
            {
                once: true
            }
        );

        fromModal.hide();
    }

    function setButtonLoading(
        button,
        loading,
        loadingText = "جارٍ التنفيذ..."
    ) {
        if (!button) {
            return;
        }

        if (loading) {
            if (
                !button.dataset
                    .originalHtml
            ) {
                button.dataset
                    .originalHtml =
                    button.innerHTML;
            }

            button.disabled = true;

            button.innerHTML = `
                <span>
                    ${escapeHtml(loadingText)}
                </span>

                <span class="spinner-border spinner-border-sm"
                      aria-hidden="true">
                </span>
            `;
        } else {
            button.disabled = false;

            if (
                button.dataset
                    .originalHtml
            ) {
                button.innerHTML =
                    button.dataset
                        .originalHtml;

                delete button.dataset
                    .originalHtml;
            }
        }
    }

    function setPostButtonLabel(
        label
    ) {
        const button =
            qs(
                SELECTORS.postButton
            );

        if (!button) {
            return;
        }

        const labelSpan =
            button.querySelector(
                "span"
            );

        if (labelSpan) {
            labelSpan.textContent =
                label;
        } else {
            button.textContent =
                label;
        }
    }

    /* ======================================================================
       02. التهيئة الرئيسية
       ====================================================================== */

    document.addEventListener(
        "DOMContentLoaded",
        () => {
            safeRun(
                "نسبة الحجز إلى الإعلان",
                initializeAdvertisementAttribution
            );

            safeRun(
                "تقويم الفنادق",
                initializeHotelCalendars
            );

            safeRun(
                "الخدمات الإضافية",
                initializeAddons
            );

            safeRun(
                "نجوم التقييم",
                initializeRatingStars
            );

            safeRun(
                "الإيموجي",
                initializeEmojiPicker
            );

            safeRun(
                "السحب الأفقي",
                initializeDragScroll
            );

            safeRun(
                "تنقل عروض المنشأة",
                initializePromotionCarousel
            );

            safeRun(
                "أزرار الباقات",
                initializePackageButtons
            );

            safeRun(
                "عارض الباقات",
                initializePackagesCarousel
            );

            safeRun(
                "بطاقة الحجز الجانبية",
                initializeSmartBookingRail
            );

            safeRun(
                "معرض الصور",
                initializeGallery
            );

            safeRun(
                "تنقل الأقسام",
                initializeSectionNavigation
            );

            safeRun(
                "العروض والخصومات",
                initializePromotionExperience
            );

            safeRun(
                "الحركة الفاخرة",
                initializeLuxuryMotion
            );

            safeRun(
                "لوحة التقييمات",
                initializeReviewsPanel
            );

            safeRun(
                "استكمال التقييم بعد تسجيل الدخول",
                resumePendingReviewAfterLogin
            );

            safeRun(
                "تنظيف المودالات",
                initializeModalCleanup
            );

            safeRun(
                "تجربة المودالات الفخمة",
                initializeLuxuryModalExperience
            );

            safeRun(
                "رسالة تعارض الحجز",
                showBookingErrorFromQuery
            );

            safeRun(
                "نوع الحجز المخصص",
                initializeCustomBookingType
            );

            safeRun(
                "اختصارات لوحة المفاتيح",
                initializeKeyboardShortcuts
            );

            safeRun(
                "زر المشاركة",
                initializeShareButton
            );

            safeRun(
                "استعادة حالة الصفحة",
                restoreRequestedState
            );

            safeRun(
                "فتح التقييم المرتبط بالإشعار",
                initializeNotificationReviewFocus
            );
        }
    );

    function restoreRequestedState() {
        if (
            storageGet(
                "openDateModal"
            ) === "true"
        ) {
            storageRemove(
                "openDateModal"
            );

            window.setTimeout(
                () => {
                    showModalById(
                        "datePickerModal"
                    );
                },
                160
            );
        }

        if (
            storageGet(
                "keepReviewsOpen"
            ) === "true"
        ) {
            storageRemove(
                "keepReviewsOpen"
            );

            window.setTimeout(
                () => {
                    setReviewsOpen(
                        true,
                        {
                            scroll:
                                !isMobileReviewsMode()
                        }
                    );
                },
                300
            );
        }
    }


    /**
     * فتح التقييم/الرد القادم من إشعار.
     *
     * الرابط المتوقع:
     * /Places/Details/{placeId}?reviewId={rootReviewId}&replyId={replyId}
     *
     * - يفتح لوحة التقييمات تلقائيًا.
     * - يعيد فلتر "الكل" حتى لا يختفي التقييم بسبب فلتر سابق.
     * - يفتح جميع طبقات الردود الأب.
     * - ينقل المستخدم إلى نفس الرد.
     * - يضيف علامة واضحة داخل الرد المرتبط بالإشعار.
     */
    function initializeNotificationReviewFocus() {
        const parameters =
            new URLSearchParams(
                window.location.search
            );

        const reviewId =
            Number.parseInt(
                parameters.get(
                    "reviewId"
                ) || "0",
                10
            ) || 0;

        const replyId =
            Number.parseInt(
                parameters.get(
                    "replyId"
                ) || "0",
                10
            ) || 0;

        if (
            reviewId <= 0 &&
            replyId <= 0
        ) {
            return;
        }

        const requestedTargetId =
            replyId > 0
                ? replyId
                : reviewId;

        const requestedTarget =
            getElement(
                `review-block-${requestedTargetId}`
            );

        const rootFallback =
            reviewId > 0
                ? getElement(
                    `review-block-${reviewId}`
                )
                : null;

        const target =
            requestedTarget ||
            rootFallback;

        if (!target) {
            return;
        }

        setReviewsOpen(
            true,
            {
                scroll: false
            }
        );

        /*
         * نتأكد أن أي فلتر للنجوم لا يخفي الجذر الذي يحتوي الرد.
         */
        const allFilterButton =
            qsa(
                ".review-filter-chip"
            ).find(button =>
                button.textContent
                    ?.trim() ===
                "الكل"
            );

        if (
            allFilterButton &&
            typeof window.filterReviews ===
            "function"
        ) {
            window.filterReviews(
                "all",
                allFilterButton
            );
        } else {
            qsa(
                '.review-node-item[data-depth="0"]'
            ).forEach(root => {
                root.style.display =
                    "";
            });
        }

        /*
         * الرد قد يكون داخل عدة مستويات مطوية.
         * نصعد من الرد حتى الجذر ونفتح كل nested-replies في الطريق.
         */
        let currentNode =
            target;

        while (currentNode) {
            const nestedContainer =
                currentNode.closest(
                    ".nested-replies"
                );

            if (!nestedContainer) {
                break;
            }

            nestedContainer.style.display =
                "block";

            nestedContainer.setAttribute(
                "aria-hidden",
                "false"
            );

            const match =
                nestedContainer.id.match(
                    /^nested-replies-(\d+)$/
                );

            if (match) {
                const parentReviewId =
                    match[1];

                const toggleButton =
                    getElement(
                        `toggle-btn-${parentReviewId}`
                    );

                const toggleText =
                    getElement(
                        `toggle-text-${parentReviewId}`
                    );

                toggleButton?.setAttribute(
                    "aria-expanded",
                    "true"
                );

                if (toggleText) {
                    toggleText.textContent =
                        "إخفاء الردود";
                }
            }

            currentNode =
                nestedContainer.closest(
                    ".review-node-item"
                );
        }

        /*
         * العلامة تبقى على الرد، أما النبضة فتتوقف بعد عدة ثوانٍ.
         */
        target.classList.add(
            "review-notification-target",
            "review-notification-pulse"
        );

        const markerHost =
            target.querySelector(
                ".review-node-user-row"
            );

        if (
            markerHost &&
            !target.querySelector(
                ".review-notification-marker"
            )
        ) {
            const marker =
                document.createElement(
                    "span"
                );

            marker.className =
                "review-notification-marker";

            marker.innerHTML =
                '<i class="bi bi-bell-fill" aria-hidden="true"></i>' +
                `<span>${replyId > 0 ? "الرد من الإشعار" : "التقييم من الإشعار"}</span>`;

            markerHost.appendChild(
                marker
            );
        }

        target.setAttribute(
            "tabindex",
            "-1"
        );

        const reduceMotion =
            window.matchMedia(
                "(prefers-reduced-motion: reduce)"
            ).matches;

        window.setTimeout(
            () => {
                /*
                 * في الهاتف اللوحة Fixed وبداخلها Scroll مستقل،
                 * لذلك نحرك Scroll اللوحة نفسها بدقة.
                 */
                const scrollArea =
                    target.closest(
                        ".reviews-scroll-area"
                    );

                if (
                    isMobileReviewsMode() &&
                    scrollArea
                ) {
                    const areaRect =
                        scrollArea
                            .getBoundingClientRect();

                    const targetRect =
                        target
                            .getBoundingClientRect();

                    const nextTop =
                        scrollArea.scrollTop +
                        (
                            targetRect.top -
                            areaRect.top
                        ) -
                        (
                            areaRect.height /
                            2
                        ) +
                        (
                            targetRect.height /
                            2
                        );

                    scrollArea.scrollTo({
                        top:
                            Math.max(
                                0,
                                nextTop
                            ),
                        behavior:
                            reduceMotion
                                ? "auto"
                                : "smooth"
                    });
                } else {
                    target.scrollIntoView({
                        behavior:
                            reduceMotion
                                ? "auto"
                                : "smooth",
                        block:
                            "center",
                        inline:
                            "nearest"
                    });
                }

                window.setTimeout(
                    () => {
                        try {
                            target.focus({
                                preventScroll:
                                    true
                            });
                        } catch {
                            target.focus();
                        }
                    },
                    220
                );
            },
            180
        );

        window.setTimeout(
            () => {
                target.classList.remove(
                    "review-notification-pulse"
                );
            },
            5200
        );
    }

    /* ======================================================================
       03. مشاركة المكان
       ====================================================================== */

    function initializeShareButton() {
        const button =
            getElement(
                "sharePlaceBtn"
            );

        if (
            !button ||
            button.dataset
                .shareInitialized ===
            "true"
        ) {
            return;
        }

        button.dataset
            .shareInitialized =
            "true";

        button.addEventListener(
            "click",
            async () => {
                const title =
                    getValue(
                        "placeNameInput",
                        document.title
                    );

                try {
                    if (navigator.share) {
                        await navigator.share({
                            title,
                            url:
                                window.location.href
                        });

                        return;
                    }

                    if (
                        navigator.clipboard
                            ?.writeText
                    ) {
                        await navigator.clipboard
                            .writeText(
                                window.location.href
                            );

                        showToast(
                            "تم نسخ رابط المكان."
                        );

                        return;
                    }

                    throw new Error(
                        "Clipboard غير متاح"
                    );
                } catch (error) {
                    if (
                        error?.name ===
                        "AbortError"
                    ) {
                        return;
                    }

                    const input =
                        document.createElement(
                            "input"
                        );

                    input.value =
                        window.location.href;

                    input.style.position =
                        "fixed";

                    input.style.opacity =
                        "0";

                    document.body
                        .appendChild(input);

                    input.select();

                    try {
                        document.execCommand(
                            "copy"
                        );

                        showToast(
                            "تم نسخ رابط المكان."
                        );
                    } catch {
                        notify({
                            icon: "error",
                            title:
                                "تعذر نسخ الرابط",
                            text:
                                "انسخ رابط الصفحة من شريط المتصفح."
                        });
                    } finally {
                        input.remove();
                    }
                }
            }
        );
    }

    /* ======================================================================
       04. تقويم الفندق
       ====================================================================== */

    window.validateDates =
        function validateDates() {
            const startInput =
                getElement(
                    "startDate"
                );

            const endInput =
                getElement(
                    "endDate"
                );

            const button =
                getElement(
                    "btnSaveDates"
                );

            const warning =
                getElement(
                    "dateWarning"
                );

            if (
                !startInput ||
                !endInput ||
                !button
            ) {
                return false;
            }

            const start =
                startInput.value;

            const end =
                endInput.value;

            let valid = false;

            if (start && end) {
                const startDate =
                    new Date(
                        `${start}T00:00:00`
                    );

                const endDate =
                    new Date(
                        `${end}T00:00:00`
                    );

                valid =
                    Number.isFinite(
                        startDate.getTime()
                    ) &&
                    Number.isFinite(
                        endDate.getTime()
                    ) &&
                    endDate > startDate;
            }

            button.disabled =
                !valid;

            button.classList.toggle(
                "btn-secondary",
                !valid
            );

            button.classList.toggle(
                "btn-info",
                valid
            );

            if (warning) {
                warning.classList.toggle(
                    "d-none",
                    !start ||
                    !end ||
                    valid
                );
            }

            return valid;
        };

    function initializeHotelCalendars() {
        const inputs =
            qsa(
                ".my-datepicker"
            );

        if (!inputs.length) {
            return;
        }

        if (
            typeof window.flatpickr ===
            "undefined"
        ) {
            console.error(
                "Flatpickr غير محمل."
            );

            return;
        }

        const placeId =
            Number.parseInt(
                getValue(
                    "placeIdInput",
                    "0"
                ),
                10
            ) || 0;

        const createCalendars =
            disabledDates => {
                state.hotelCalendars
                    .forEach(
                        instance => {
                            try {
                                instance.destroy();
                            } catch {
                                // تجاهل
                            }
                        }
                    );

                state.hotelCalendars =
                    [];

                inputs.forEach(
                    input => {
                        const instance =
                            window.flatpickr(
                                input,
                                {
                                    dateFormat:
                                        "Y-m-d",

                                    minDate:
                                        "today",

                                    locale:
                                        "en",

                                    disableMobile:
                                        true,

                                    allowInput:
                                        false,

                                    disable:
                                        Array.isArray(
                                            disabledDates
                                        )
                                            ? disabledDates
                                            : [],

                                    onChange:
                                        () => {
                                            window
                                                .validateDates();
                                        }
                                }
                            );

                        state.hotelCalendars
                            .push(instance);
                    }
                );

                window.validateDates();
            };

        if (!placeId) {
            createCalendars([]);
            return;
        }

        fetch(
            `/Places/GetFullyBookedDates?placeId=${encodeURIComponent(
                placeId
            )}`,
            {
                credentials:
                    "same-origin"
            }
        )
            .then(
                response => {
                    if (!response.ok) {
                        throw new Error(
                            "تعذر تحميل الأيام المحجوزة."
                        );
                    }

                    return response.json();
                }
            )
            .then(
                data => {
                    createCalendars(data);
                }
            )
            .catch(
                error => {
                    console.warn(error);
                    createCalendars([]);
                }
            );
    }

    /* ======================================================================
       05. الخدمات الإضافية
       ====================================================================== */

    function initializeAddons() {
        const checkboxes =
            qsa(
                ".addon-checkbox"
            );

        state.selectedServiceIds
            .clear();

        const update = () => {
            state.selectedServiceIds
                .clear();

            checkboxes.forEach(
                checkbox => {
                    const card =
                        checkbox.closest(
                            ".addon-container"
                        );

                    const icon =
                        card?.querySelector(
                            ".check-icon"
                        );

                    const checked =
                        checkbox.checked;

                    const serviceId =
                        Number.parseInt(
                            checkbox.dataset
                                .serviceId ||
                            "0",
                            10
                        ) || 0;

                    if (
                        checked &&
                        serviceId > 0
                    ) {
                        state.selectedServiceIds
                            .add(serviceId);
                    }

                    if (card) {
                        card.classList.toggle(
                            "border-info",
                            checked
                        );

                        card.classList.toggle(
                            "bg-light",
                            checked
                        );

                        card.classList.toggle(
                            "shadow",
                            checked
                        );

                        card.classList.toggle(
                            "is-selected",
                            checked
                        );

                        card.classList.toggle(
                            "bg-white",
                            !checked
                        );

                        card.setAttribute(
                            "aria-checked",
                            checked
                                ? "true"
                                : "false"
                        );
                    }

                    if (icon) {
                        icon.className =
                            checked
                                ? "bi bi-check-circle-fill text-info check-icon"
                                : "bi bi-circle text-muted check-icon";
                    }
                }
            );
        };

        checkboxes.forEach(
            checkbox => {
                checkbox.addEventListener(
                    "change",
                    update
                );

                const card =
                    checkbox.closest(
                        ".addon-container"
                    );

                if (!card) {
                    return;
                }

                card.setAttribute(
                    "role",
                    "checkbox"
                );

                card.setAttribute(
                    "aria-checked",
                    checkbox.checked
                        ? "true"
                        : "false"
                );

                if (
                    card.tagName !==
                    "LABEL"
                ) {
                    card.addEventListener(
                        "click",
                        event => {
                            if (
                                event.target.closest(
                                    ".service-image-button"
                                )
                            ) {
                                return;
                            }

                            checkbox.checked =
                                !checkbox.checked;

                            checkbox.dispatchEvent(
                                new Event(
                                    "change",
                                    {
                                        bubbles: true
                                    }
                                )
                            );
                        }
                    );
                }
            }
        );

        update();
    }

    /* ======================================================================
       06. نجوم التقييم
       ====================================================================== */

    function initializeRatingStars() {
        const stars =
            qsa(
                ".star-item"
            );

        const ratingInput =
            qs(
                SELECTORS.ratingInput
            );

        if (
            !stars.length ||
            !ratingInput
        ) {
            return;
        }

        const paint =
            value => {
                stars.forEach(
                    star => {
                        const starValue =
                            Number.parseInt(
                                star.dataset
                                    .value ||
                                "0",
                                10
                            ) || 0;

                        const active =
                            starValue <= value;

                        star.classList.toggle(
                            "bi-star-fill",
                            active
                        );

                        star.classList.toggle(
                            "text-warning",
                            active
                        );

                        star.classList.toggle(
                            "bi-star",
                            !active
                        );

                        star.classList.toggle(
                            "text-muted",
                            !active
                        );
                    }
                );
            };

        stars.forEach(
            star => {
                star.setAttribute(
                    "role",
                    "button"
                );

                star.setAttribute(
                    "tabindex",
                    "0"
                );

                const select = () => {
                    const value =
                        Number.parseInt(
                            star.dataset
                                .value ||
                            "0",
                            10
                        ) || 0;

                    ratingInput.value =
                        String(value);

                    paint(value);
                };

                star.addEventListener(
                    "click",
                    select
                );

                star.addEventListener(
                    "keydown",
                    event => {
                        if (
                            event.key ===
                            "Enter" ||
                            event.key ===
                            " "
                        ) {
                            event.preventDefault();
                            select();
                        }
                    }
                );

                if (
                    window.matchMedia(
                        "(hover: hover)"
                    ).matches
                ) {
                    star.addEventListener(
                        "mouseenter",
                        () => {
                            paint(
                                Number.parseInt(
                                    star.dataset
                                        .value ||
                                    "0",
                                    10
                                ) || 0
                            );
                        }
                    );

                    star.addEventListener(
                        "mouseleave",
                        () => {
                            paint(
                                Number.parseInt(
                                    ratingInput.value ||
                                    "0",
                                    10
                                ) || 0
                            );
                        }
                    );
                }
            }
        );

        paint(
            Number.parseInt(
                ratingInput.value ||
                "0",
                10
            ) || 0
        );
    }

    /* ======================================================================
       07. Emoji Picker
       ====================================================================== */

    function initializeEmojiPicker() {
        const picker =
            getElement(
                "customerEmojiPicker"
            );

        const input =
            qs(
                SELECTORS.reviewInput
            );

        if (!picker || !input) {
            return;
        }

        picker.addEventListener(
            "emoji-click",
            event => {
                const emoji =
                    event.detail
                        ?.unicode ||
                    "";

                if (!emoji) {
                    return;
                }

                const start =
                    input.selectionStart ??
                    input.value.length;

                const end =
                    input.selectionEnd ??
                    input.value.length;

                input.value =
                    input.value.slice(
                        0,
                        start
                    ) +
                    emoji +
                    input.value.slice(end);

                const cursor =
                    start +
                    emoji.length;

                input.setSelectionRange(
                    cursor,
                    cursor
                );

                input.dispatchEvent(
                    new Event(
                        "input",
                        {
                            bubbles: true
                        }
                    )
                );

                input.focus();
            }
        );
    }

    /* ======================================================================
       08. السحب الأفقي
       ====================================================================== */

    function initializeDragScroll() {
        [
            "packagesContainer",
            "extendedGalleryScroll",
            "promotionShowcaseTrack"
        ].forEach(
            enableDragScroll
        );
    }

    function initializePackageButtons() {
        const buttons =
            qsa(
                ".package-select-button"
            );

        buttons.forEach(
            button => {
                if (
                    button.dataset
                        .packageButtonInitialized ===
                    "true"
                ) {
                    return;
                }

                button.dataset
                    .packageButtonInitialized =
                    "true";

                button.removeAttribute(
                    "onclick"
                );

                button.addEventListener(
                    "click",
                    event => {
                        event.preventDefault();
                        event.stopPropagation();

                        window
                            .openPackageModalFromButton(
                                button
                            );
                    }
                );
            }
        );
    }

    /* ======================================================================
       08.A عارض الباقات — الأسهم والكيبورد والسحب
       ====================================================================== */

    function initializePackagesCarousel() {
        const track =
            getElement(
                "packagesContainer"
            );

        if (
            !track ||
            track.dataset
                .carouselInitialized ===
            "true"
        ) {
            return;
        }

        const section =
            track.closest(
                ".packages-premium-section"
            );

        const cards =
            qsa(
                ".package-card",
                track
            );

        if (!cards.length) {
            return;
        }

        track.dataset
            .carouselInitialized =
            "true";

        /*
         * أزلنا الأحداث المكتوبة داخل HTML حتى لا ينفذ الضغط مرتين.
         * هذه الدالة تصبح المصدر الوحيد للتنقل بين الباقات.
         */
        track.removeAttribute(
            "onkeydown"
        );

        const navigationButtons =
            qsa(
                ".packages-nav-button",
                section || document
            );

        navigationButtons.forEach(
            button => {
                button.removeAttribute(
                    "onclick"
                );
            }
        );

        const previousButton =
            navigationButtons.find(
                button =>
                    !button.classList
                        .contains(
                            "is-next"
                        )
            ) || null;

        const nextButton =
            navigationButtons.find(
                button =>
                    button.classList
                        .contains(
                            "is-next"
                        )
            ) || null;

        let currentIndex =
            Number.parseInt(
                track.dataset
                    .currentIndex ||
                "0",
                10
            ) || 0;

        let scrollFrame = null;
        let resizeTimer = null;

        function clampIndex(index) {
            return Math.min(
                cards.length - 1,
                Math.max(
                    0,
                    Number.parseInt(
                        String(index),
                        10
                    ) || 0
                )
            );
        }

        function updateNavigationState() {
            currentIndex =
                clampIndex(
                    currentIndex
                );

            track.dataset.currentIndex =
                String(currentIndex);

            cards.forEach(
                (card, index) => {
                    const active =
                        index ===
                        currentIndex;

                    card.classList.toggle(
                        "is-carousel-current",
                        active
                    );

                    card.setAttribute(
                        "aria-current",
                        active
                            ? "true"
                            : "false"
                    );
                }
            );

            if (previousButton) {
                const disabled =
                    currentIndex <= 0;

                previousButton.disabled =
                    disabled;

                previousButton.setAttribute(
                    "aria-disabled",
                    disabled
                        ? "true"
                        : "false"
                );
            }

            if (nextButton) {
                const disabled =
                    currentIndex >=
                    cards.length - 1;

                nextButton.disabled =
                    disabled;

                nextButton.setAttribute(
                    "aria-disabled",
                    disabled
                        ? "true"
                        : "false"
                );
            }
        }

        function getNearestCardIndex() {
            const trackRect =
                track.getBoundingClientRect();

            const trackCenter =
                trackRect.left +
                trackRect.width / 2;

            let nearestIndex = 0;

            let nearestDistance =
                Number.POSITIVE_INFINITY;

            cards.forEach(
                (card, index) => {
                    const rect =
                        card.getBoundingClientRect();

                    const cardCenter =
                        rect.left +
                        rect.width / 2;

                    const distance =
                        Math.abs(
                            cardCenter -
                            trackCenter
                        );

                    if (
                        distance <
                        nearestDistance
                    ) {
                        nearestDistance =
                            distance;

                        nearestIndex =
                            index;
                    }
                }
            );

            return nearestIndex;
        }

        function syncIndexFromScroll() {
            currentIndex =
                getNearestCardIndex();

            updateNavigationState();
        }

        function scrollToPackage(
            requestedIndex,
            options = {}
        ) {
            const index =
                clampIndex(
                    requestedIndex
                );

            const target =
                cards[index];

            if (!target) {
                return;
            }

            currentIndex =
                index;

            updateNavigationState();

            target.scrollIntoView({
                behavior:
                    options.instant
                        ? "auto"
                        : "smooth",

                block:
                    "nearest",

                inline:
                    "center"
            });

            if (options.focus) {
                const focusTarget =
                    target.querySelector(
                        ".package-select-button"
                    ) || target;

                window.setTimeout(
                    () => {
                        focusTarget.focus({
                            preventScroll:
                                true
                        });
                    },
                    options.instant
                        ? 0
                        : 260
                );
            }
        }

        function movePrevious() {
            scrollToPackage(
                currentIndex - 1
            );
        }

        function moveNext() {
            scrollToPackage(
                currentIndex + 1
            );
        }

        previousButton
            ?.addEventListener(
                "click",
                event => {
                    event.preventDefault();
                    movePrevious();
                }
            );

        nextButton
            ?.addEventListener(
                "click",
                event => {
                    event.preventDefault();
                    moveNext();
                }
            );

        track.addEventListener(
            "keydown",
            event => {
                const interactive =
                    event.target.closest(
                        "button, a, input, select, textarea"
                    );

                /*
                 * عندما يكون التركيز على زر اختيار الباقة، نسمح بـ
                 * Enter و Space ولا نتدخل إلا في مفاتيح التنقل.
                 */
                const navigationKeys = [
                    "ArrowLeft",
                    "ArrowRight",
                    "Home",
                    "End",
                    "PageUp",
                    "PageDown"
                ];

                if (
                    !navigationKeys.includes(
                        event.key
                    )
                ) {
                    return;
                }

                if (
                    interactive &&
                    ![
                        "ArrowLeft",
                        "ArrowRight"
                    ].includes(
                        event.key
                    )
                ) {
                    return;
                }

                event.preventDefault();

                switch (event.key) {
                    case "ArrowLeft":
                    case "PageDown":
                        scrollToPackage(
                            currentIndex + 1,
                            {
                                focus:
                                    Boolean(
                                        interactive
                                    )
                            }
                        );
                        break;

                    case "ArrowRight":
                    case "PageUp":
                        scrollToPackage(
                            currentIndex - 1,
                            {
                                focus:
                                    Boolean(
                                        interactive
                                    )
                            }
                        );
                        break;

                    case "Home":
                        scrollToPackage(
                            0,
                            {
                                focus:
                                    Boolean(
                                        interactive
                                    )
                            }
                        );
                        break;

                    case "End":
                        scrollToPackage(
                            cards.length - 1,
                            {
                                focus:
                                    Boolean(
                                        interactive
                                    )
                            }
                        );
                        break;
                }
            }
        );

        /*
         * بعد السحب بالماوس أو الإصبع نحدد البطاقة الأقرب إلى المنتصف،
         * وبذلك تتزامن حالة الأسهم مع موضع المستخدم الحقيقي.
         */
        track.addEventListener(
            "scroll",
            () => {
                if (scrollFrame) {
                    window.cancelAnimationFrame(
                        scrollFrame
                    );
                }

                scrollFrame =
                    window.requestAnimationFrame(
                        () => {
                            scrollFrame = null;
                            syncIndexFromScroll();
                        }
                    );
            },
            {
                passive: true
            }
        );

        cards.forEach(
            (card, index) => {
                card.addEventListener(
                    "focusin",
                    () => {
                        currentIndex =
                            index;

                        updateNavigationState();
                    }
                );
            }
        );

        window.addEventListener(
            "resize",
            () => {
                window.clearTimeout(
                    resizeTimer
                );

                resizeTimer =
                    window.setTimeout(
                        () => {
                            syncIndexFromScroll();
                        },
                        140
                    );
            },
            {
                passive: true
            }
        );

        updateNavigationState();

        window.setTimeout(
            () => {
                syncIndexFromScroll();
            },
            80
        );
    }

    /* ======================================================================
       08.B البطاقة الجانبية — Sticky ذكي بدون قص المحتوى
       ====================================================================== */

    function initializeSmartBookingRail() {
        const stickyWrap =
            qs(
                ".booking-sticky-wrap"
            );

        if (!stickyWrap) {
            return;
        }

        /*
         * نترك CSS مسؤولًا عن تثبيت البطاقة.
         * لا نحولها إلى position: static حتى لا تختفي أثناء النزول.
         */
        stickyWrap.classList.remove(
            "is-sticky-disabled"
        );

        stickyWrap.style.removeProperty(
            "position"
        );

        stickyWrap.style.removeProperty(
            "top"
        );
    }

    function enableDragScroll(
        elementId
    ) {
        const slider =
            getElement(
                elementId
            );

        if (!slider) {
            return;
        }

        let dragging = false;
        let moved = false;
        let startX = 0;
        let startScroll = 0;
        let activePointerId = null;

        const isInteractiveElement =
            target => {
                return Boolean(
                    target.closest(
                        "button, a, input, label, select, textarea, " +
                        "[role='button'], [data-no-drag]"
                    )
                );
            };

        slider.addEventListener(
            "pointerdown",
            event => {
                if (
                    event.pointerType ===
                    "touch"
                ) {
                    return;
                }

                if (
                    isInteractiveElement(
                        event.target
                    )
                ) {
                    dragging = false;
                    moved = false;
                    return;
                }

                dragging = true;
                moved = false;

                activePointerId =
                    event.pointerId;

                startX =
                    event.clientX;

                startScroll =
                    slider.scrollLeft;

                slider.classList.add(
                    "is-dragging"
                );

                slider.style.cursor =
                    "grabbing";

                try {
                    slider.setPointerCapture(
                        event.pointerId
                    );
                } catch {
                    // تجاهل
                }
            }
        );

        slider.addEventListener(
            "pointermove",
            event => {
                if (
                    !dragging ||
                    event.pointerId !==
                    activePointerId
                ) {
                    return;
                }

                const distance =
                    event.clientX -
                    startX;

                if (
                    Math.abs(distance) <
                    10
                ) {
                    return;
                }

                moved = true;

                event.preventDefault();

                slider.scrollLeft =
                    startScroll -
                    distance * 1.7;
            }
        );

        const stopDragging = () => {
            if (!dragging) {
                return;
            }

            dragging = false;

            slider.classList.remove(
                "is-dragging"
            );

            slider.style.cursor =
                "";

            try {
                if (
                    activePointerId !==
                    null &&
                    slider.hasPointerCapture(
                        activePointerId
                    )
                ) {
                    slider.releasePointerCapture(
                        activePointerId
                    );
                }
            } catch {
                // تجاهل
            }

            activePointerId =
                null;

            window.setTimeout(
                () => {
                    moved = false;
                },
                0
            );
        };

        slider.addEventListener(
            "pointerup",
            stopDragging
        );

        slider.addEventListener(
            "pointercancel",
            stopDragging
        );

        slider.addEventListener(
            "lostpointercapture",
            () => {
                dragging = false;
                activePointerId = null;
                slider.classList.remove(
                    "is-dragging"
                );
                slider.style.cursor = "";
            }
        );

        slider.addEventListener(
            "click",
            event => {
                if (
                    isInteractiveElement(
                        event.target
                    )
                ) {
                    return;
                }

                if (moved) {
                    event.preventDefault();
                    event.stopPropagation();
                    moved = false;
                }
            },
            true
        );
    }

    /* ======================================================================
       08-B. تنقل عروض المنشأة — ماوس + لمس + كيبورد + أزرار
       ====================================================================== */

    function initializePromotionCarousel() {
        const track =
            getElement(
                "promotionShowcaseTrack"
            );

        if (!track) {
            return;
        }

        const previousButton =
            getElement(
                "promotionPreviousButton"
            );

        const nextButton =
            getElement(
                "promotionNextButton"
            );

        const currentIndexLabel =
            getElement(
                "promotionCurrentIndex"
            );

        const cards =
            qsa(
                "[data-promotion-card]",
                track
            );

        if (!cards.length) {
            return;
        }

        let currentIndex = 0;
        let scrollTimer = null;

        const clampIndex = value =>
            Math.max(
                0,
                Math.min(
                    cards.length - 1,
                    value
                )
            );

        const syncControls = index => {
            currentIndex =
                clampIndex(index);

            if (currentIndexLabel) {
                currentIndexLabel.textContent =
                    String(currentIndex + 1);
            }

            if (previousButton) {
                previousButton.disabled =
                    currentIndex <= 0;

                previousButton.setAttribute(
                    "aria-disabled",
                    currentIndex <= 0
                        ? "true"
                        : "false"
                );
            }

            if (nextButton) {
                nextButton.disabled =
                    currentIndex >=
                    cards.length - 1;

                nextButton.setAttribute(
                    "aria-disabled",
                    currentIndex >=
                        cards.length - 1
                        ? "true"
                        : "false"
                );
            }

            cards.forEach(
                (card, index) => {
                    card.classList.toggle(
                        "is-carousel-current",
                        index === currentIndex
                    );
                }
            );
        };

        const findClosestCardIndex = () => {
            const trackRect =
                track.getBoundingClientRect();

            const trackCenter =
                trackRect.left +
                trackRect.width / 2;

            let bestIndex = 0;
            let bestDistance =
                Number.POSITIVE_INFINITY;

            cards.forEach(
                (card, index) => {
                    const rect =
                        card.getBoundingClientRect();

                    const center =
                        rect.left +
                        rect.width / 2;

                    const distance =
                        Math.abs(
                            center -
                            trackCenter
                        );

                    if (
                        distance <
                        bestDistance
                    ) {
                        bestDistance =
                            distance;
                        bestIndex =
                            index;
                    }
                }
            );

            return bestIndex;
        };

        const goToIndex = (
            requestedIndex,
            behavior = "smooth"
        ) => {
            const index =
                clampIndex(
                    requestedIndex
                );

            const card =
                cards[index];

            if (!card) {
                return;
            }

            syncControls(index);

            card.scrollIntoView({
                behavior,
                block: "nearest",
                inline: "center"
            });
        };

        previousButton?.addEventListener(
            "click",
            event => {
                event.preventDefault();
                goToIndex(
                    currentIndex - 1
                );
            }
        );

        nextButton?.addEventListener(
            "click",
            event => {
                event.preventDefault();
                goToIndex(
                    currentIndex + 1
                );
            }
        );

        track.addEventListener(
            "keydown",
            event => {
                if (
                    event.target !== track
                ) {
                    return;
                }

                if (
                    event.key ===
                    "ArrowLeft"
                ) {
                    event.preventDefault();
                    goToIndex(
                        currentIndex + 1
                    );
                    return;
                }

                if (
                    event.key ===
                    "ArrowRight"
                ) {
                    event.preventDefault();
                    goToIndex(
                        currentIndex - 1
                    );
                    return;
                }

                if (
                    event.key === "Home"
                ) {
                    event.preventDefault();
                    goToIndex(0);
                    return;
                }

                if (
                    event.key === "End"
                ) {
                    event.preventDefault();
                    goToIndex(
                        cards.length - 1
                    );
                }
            }
        );

        const scheduleSync = () => {
            if (scrollTimer) {
                window.clearTimeout(
                    scrollTimer
                );
            }

            scrollTimer =
                window.setTimeout(
                    () => {
                        syncControls(
                            findClosestCardIndex()
                        );
                    },
                    90
                );
        };

        track.addEventListener(
            "scroll",
            scheduleSync,
            {
                passive: true
            }
        );

        track.addEventListener(
            "pointerup",
            scheduleSync,
            {
                passive: true
            }
        );

        track.addEventListener(
            "touchend",
            scheduleSync,
            {
                passive: true
            }
        );

        window.addEventListener(
            "resize",
            scheduleSync,
            {
                passive: true
            }
        );

        const selectedIndex =
            cards.findIndex(card =>
                card.classList.contains(
                    "is-selected"
                )
            );

        currentIndex =
            selectedIndex >= 0
                ? selectedIndex
                : findClosestCardIndex();

        syncControls(
            currentIndex
        );

        if (selectedIndex > 0) {
            window.setTimeout(
                () => {
                    goToIndex(
                        selectedIndex,
                        "auto"
                    );
                },
                60
            );
        }
    }

    /* ======================================================================
   09. معرض الصور الرئيسي
   ====================================================================== */

    function normalizeGalleryUrl(
        src
    ) {
        if (!src) {
            return "";
        }

        try {
            return new URL(
                src,
                window.location.href
            ).href;
        } catch {
            return String(src);
        }
    }

    function syncActiveGalleryThumbnail(
        index,
        scrollToThumbnail = true
    ) {
        const thumbnails =
            qsa(".extended-thumb");

        if (
            !thumbnails.length ||
            !state.galleryImages.length
        ) {
            return;
        }

        const total =
            state.galleryImages.length;

        const safeIndex =
            (
                (index % total) +
                total
            ) % total;

        const targetSrc =
            state.galleryImages[
            safeIndex
            ];

        let activeThumbnail =
            thumbnails.find(
                image =>
                    normalizeGalleryUrl(
                        image.src
                    ) === targetSrc
            );

        if (!activeThumbnail) {
            activeThumbnail =
                thumbnails[safeIndex] ||
                thumbnails[0];
        }

        thumbnails.forEach(
            image => {
                const active =
                    image ===
                    activeThumbnail;

                image.classList.toggle(
                    "active",
                    active
                );

                const button =
                    image.closest(
                        ".extended-gallery-item"
                    );

                if (button) {
                    button.classList.toggle(
                        "is-active",
                        active
                    );

                    button.setAttribute(
                        "aria-current",
                        active
                            ? "true"
                            : "false"
                    );
                }
            }
        );

        state.selectedGalleryIndex =
            safeIndex;

        const counter =
            qs(
                ".gallery-photo-counter span:last-child"
            );

        if (counter) {
            counter.textContent =
                `${safeIndex + 1} / ${total}`;
        }

        const section =
            getElement(
                "extendedGallerySection"
            );

        const open =
            section &&
            window.getComputedStyle(
                section
            ).display !== "none";

        if (
            open &&
            scrollToThumbnail &&
            activeThumbnail
        ) {
            window.setTimeout(
                () => {
                    activeThumbnail
                        .scrollIntoView({
                            behavior:
                                "smooth",
                            inline:
                                "center",
                            block:
                                "nearest"
                        });
                },
                50
            );
        }
    }

    function initializeGallery() {
        const mainImage =
            getElement(
                "mainDisplayImage"
            );

        if (!mainImage) {
            return;
        }

        const images = [];

        const add = src => {
            const normalized =
                normalizeGalleryUrl(
                    src
                );

            if (
                normalized &&
                !images.includes(
                    normalized
                )
            ) {
                images.push(
                    normalized
                );
            }
        };

        add(mainImage.src);

        qsa(".extended-thumb")
            .forEach(
                image =>
                    add(image.src)
            );

        qsa(".side-hero-img")
            .forEach(
                image =>
                    add(image.src)
            );

        state.galleryImages =
            images;

        const initial =
            images.indexOf(
                normalizeGalleryUrl(
                    mainImage.src
                )
            );

        state.selectedGalleryIndex =
            initial >= 0
                ? initial
                : 0;

        syncActiveGalleryThumbnail(
            state.selectedGalleryIndex,
            false
        );

        const mainButton =
            mainImage.closest(
                ".gallery-main-shot"
            );

        if (!mainButton) {
            return;
        }

        let startX = 0;
        let startY = 0;

        mainButton.addEventListener(
            "touchstart",
            event => {
                const touch =
                    event
                        .changedTouches[0];

                startX =
                    touch.clientX;

                startY =
                    touch.clientY;
            },
            {
                passive: true
            }
        );

        mainButton.addEventListener(
            "touchend",
            event => {
                if (
                    state
                        .galleryImages
                        .length < 2
                ) {
                    return;
                }

                const touch =
                    event
                        .changedTouches[0];

                const deltaX =
                    touch.clientX -
                    startX;

                const deltaY =
                    touch.clientY -
                    startY;

                if (
                    Math.abs(deltaX) <
                    45 ||
                    Math.abs(deltaX) <=
                    Math.abs(deltaY)
                ) {
                    return;
                }

                event.preventDefault();

                showGalleryIndex(
                    state
                        .selectedGalleryIndex +
                    (
                        deltaX < 0
                            ? 1
                            : -1
                    )
                );
            },
            {
                passive: false
            }
        );
    }

    function showGalleryIndex(
        index
    ) {
        if (
            !state.galleryImages
                .length
        ) {
            return;
        }

        const total =
            state.galleryImages.length;

        const safeIndex =
            (
                (index % total) +
                total
            ) % total;

        window.changeMainImage(
            {
                src:
                    state
                        .galleryImages[
                    safeIndex
                    ]
            },
            {
                galleryIndex:
                    safeIndex,

                scrollThumbnail:
                    true
            }
        );
    }

    window.toggleExtendedGallery =
        function toggleExtendedGallery(
            forceOpen
        ) {
            const section =
                getElement(
                    "extendedGallerySection"
                );

            if (!section) {
                return;
            }

            const currentlyOpen =
                window.getComputedStyle(
                    section
                ).display !== "none";

            const shouldOpen =
                typeof forceOpen ===
                    "boolean"
                    ? forceOpen
                    : !currentlyOpen;

            section.style.display =
                shouldOpen
                    ? "block"
                    : "none";

            section.setAttribute(
                "aria-hidden",
                shouldOpen
                    ? "false"
                    : "true"
            );

            if (shouldOpen) {
                syncActiveGalleryThumbnail(
                    state
                        .selectedGalleryIndex,
                    true
                );

                window.setTimeout(
                    () => {
                        section
                            .scrollIntoView({
                                behavior:
                                    "smooth",
                                block:
                                    "center"
                            });

                        getElement(
                            "extendedGalleryScroll"
                        )?.focus({
                            preventScroll:
                                true
                        });
                    },
                    70
                );
            }
        };

    window.changeMainImage =
        function changeMainImage(
            clickedImage,
            options = {}
        ) {
            const mainImage =
                getElement(
                    "mainDisplayImage"
                );

            if (
                !mainImage ||
                !clickedImage ||
                !clickedImage.src
            ) {
                return;
            }

            const targetSrc =
                normalizeGalleryUrl(
                    clickedImage.src
                );

            let index =
                Number.isInteger(
                    options.galleryIndex
                )
                    ? options
                        .galleryIndex
                    : state
                        .galleryImages
                        .indexOf(
                            targetSrc
                        );

            if (index < 0) {
                const matched =
                    qsa(
                        ".extended-thumb"
                    ).find(
                        image =>
                            normalizeGalleryUrl(
                                image.src
                            ) ===
                            targetSrc
                    );

                if (matched) {
                    index =
                        qsa(
                            ".extended-thumb"
                        ).indexOf(
                            matched
                        );
                }
            }

            mainImage.style.opacity =
                "0.35";

            window.setTimeout(
                () => {
                    mainImage.src =
                        targetSrc;

                    mainImage.style.opacity =
                        "1";
                },
                130
            );

            qsa(".side-hero-img")
                .forEach(image => {
                    image.style.opacity =
                        normalizeGalleryUrl(
                            image.src
                        ) === targetSrc
                            ? "0.76"
                            : "1";
                });

            if (index >= 0) {
                syncActiveGalleryThumbnail(
                    index,
                    options
                        .scrollThumbnail !==
                    false
                );
            }
        };

    /* ======================================================================
       10. تنقل أقسام الصفحة
       ====================================================================== */

    function initializeSectionNavigation() {
        const links =
            qsa(
                ".place-section-link"
            );

        if (!links.length) {
            return;
        }

        const sections =
            links
                .map(link => {
                    const href =
                        link.getAttribute(
                            "href"
                        );

                    if (
                        !href?.startsWith(
                            "#"
                        )
                    ) {
                        return null;
                    }

                    return qs(href);
                })
                .filter(Boolean);

        links.forEach(link => {
            link.addEventListener(
                "click",
                event => {
                    const href =
                        link.getAttribute(
                            "href"
                        );

                    const target =
                        href
                            ? qs(href)
                            : null;

                    if (!target) {
                        return;
                    }

                    event.preventDefault();

                    target
                        .scrollIntoView({
                            behavior:
                                "smooth",
                            block:
                                "start"
                        });

                    setActiveSectionLink(
                        link
                    );
                }
            );
        });

        if (
            !(
                "IntersectionObserver" in
                window
            )
        ) {
            return;
        }

        const observer =
            new IntersectionObserver(
                entries => {
                    const visible =
                        entries
                            .filter(
                                entry =>
                                    entry
                                        .isIntersecting
                            )
                            .sort(
                                (a, b) =>
                                    b
                                        .intersectionRatio -
                                    a
                                        .intersectionRatio
                            )[0];

                    if (!visible) {
                        return;
                    }

                    const link =
                        links.find(
                            item =>
                                item
                                    .getAttribute(
                                        "href"
                                    ) ===
                                `#${visible.target.id}`
                        );

                    if (link) {
                        setActiveSectionLink(
                            link
                        );
                    }
                },
                {
                    rootMargin:
                        "-25% 0px -62% 0px",

                    threshold: [
                        0.01,
                        0.15,
                        0.35
                    ]
                }
            );

        sections.forEach(
            section =>
                observer.observe(
                    section
                )
        );
    }

    function setActiveSectionLink(
        activeLink
    ) {
        qsa(".place-section-link")
            .forEach(link => {
                link.classList.toggle(
                    "active",
                    link ===
                    activeLink
                );
            });

        activeLink.scrollIntoView({
            behavior: "smooth",
            inline: "center",
            block: "nearest"
        });
    }


    /* ======================================================================
       10.A العروض — اختيار العرض، التنقل الذكي، و3D هادئ
       ====================================================================== */

    function getSelectedPromotionInput() {
        return getElement(
            "selectedPromotionIdInput"
        );
    }

    function getSelectedPromotionId() {
        const input =
            getSelectedPromotionInput();

        const fromInput =
            Number.parseInt(
                input?.value ||
                "0",
                10
            ) || 0;

        if (fromInput > 0) {
            return fromInput;
        }

        const page =
            qs(
                ".place-details-page"
            );

        return Number.parseInt(
            page?.dataset
                .selectedPromotionId ||
            "0",
            10
        ) || 0;
    }

    function setSelectedPromotionId(
        promotionId
    ) {
        const parsedId =
            Number.parseInt(
                String(
                    promotionId ||
                    "0"
                ),
                10
            ) || 0;

        state.selectedPromotionId =
            parsedId;

        const input =
            getSelectedPromotionInput();

        if (input) {
            input.value =
                String(
                    parsedId
                );
        }

        const page =
            qs(
                ".place-details-page"
            );

        if (page) {
            page.dataset
                .selectedPromotionId =
                String(
                    parsedId
                );
        }

        qsa(
            "[data-promotion-card]"
        ).forEach(card => {
            const cardId =
                Number.parseInt(
                    card.dataset
                        .promotionId ||
                    "0",
                    10
                ) || 0;

            const selected =
                cardId === parsedId;

            card.classList.toggle(
                "is-selected",
                selected
            );

            card.setAttribute(
                "aria-selected",
                selected
                    ? "true"
                    : "false"
            );
        });

        qsa(
            "[data-promotion-id]"
        ).forEach(element => {
            const elementId =
                Number.parseInt(
                    element.dataset
                        .promotionId ||
                    "0",
                    10
                ) || 0;

            element.classList.toggle(
                "promotion-is-selected",
                elementId > 0 &&
                elementId === parsedId
            );
        });
    }

    function appendSelectedPromotion(
        parameters
    ) {
        if (
            !(parameters instanceof URLSearchParams)
        ) {
            return;
        }

        const promotionId =
            state.selectedPromotionId >
                0
                ? state.selectedPromotionId
                : getSelectedPromotionId();

        if (promotionId > 0) {
            parameters.set(
                "promotionId",
                String(
                    promotionId
                )
            );
        }
    }

    function flashPromotionTarget(
        target
    ) {
        if (!target) {
            return;
        }

        target.classList.remove(
            "promotion-target-flash"
        );

        void target.offsetWidth;

        target.classList.add(
            "promotion-target-flash"
        );

        window.setTimeout(
            () => {
                target.classList.remove(
                    "promotion-target-flash"
                );
            },
            1900
        );
    }

    function getPromotionIncludedServiceIds(
        promotionId,
        rawAnchors = ""
    ) {
        const ids = new Set();

        const parsedPromotionId =
            Number.parseInt(
                String(
                    promotionId ||
                    "0"
                ),
                10
            ) || 0;

        const card =
            parsedPromotionId > 0
                ? qs(
                    `[data-promotion-card][data-promotion-id="${parsedPromotionId}"]`
                )
                : null;

        String(
            card?.dataset
                .includedServiceIds ||
            ""
        )
            .split(",")
            .map(value =>
                Number.parseInt(
                    value,
                    10
                ) || 0
            )
            .filter(value =>
                value > 0
            )
            .forEach(value =>
                ids.add(value)
            );

        String(
            rawAnchors ||
            card?.dataset
                .targetAnchor ||
            ""
        )
            .split("|")
            .forEach(anchor => {
                const match =
                    String(anchor || "")
                        .trim()
                        .match(
                            /^#?serviceCard-(\d+)$/i
                        );

                if (!match) {
                    return;
                }

                const serviceId =
                    Number.parseInt(
                        match[1],
                        10
                    ) || 0;

                if (serviceId > 0) {
                    ids.add(serviceId);
                }
            });

        return Array.from(ids);
    }

    function clearPromotionAutoIncludedServices() {
        const autoServiceIds =
            Array.from(
                state.promotionAutoServiceIds
            );

        state.promotionAutoServiceIds
            .clear();

        autoServiceIds.forEach(serviceId => {
            const checkbox =
                qs(
                    `.addon-checkbox[data-service-id="${serviceId}"]`
                );

            if (!checkbox?.checked) {
                return;
            }

            // نستخدم click حقيقي حتى تتحدث state وحسابات الخدمات بنفس
            // الطريقة التي تحدث عند إلغاء العميل للخدمة بيده.
            checkbox.click();

            if (checkbox.checked) {
                checkbox.checked = false;
                checkbox.dispatchEvent(
                    new Event(
                        "input",
                        { bubbles: true }
                    )
                );
                checkbox.dispatchEvent(
                    new Event(
                        "change",
                        { bubbles: true }
                    )
                );
            }
        });
    }

    function applyPromotionIncludedServices(
        promotionId,
        rawAnchors = "",
        options = {}
    ) {
        const parsedPromotionId =
            Number.parseInt(
                String(
                    promotionId ||
                    "0"
                ),
                10
            ) || 0;

        if (parsedPromotionId <= 0) {
            return [];
        }

        if (options.replacePrevious === true) {
            clearPromotionAutoIncludedServices();
        }

        const serviceIds =
            getPromotionIncludedServiceIds(
                parsedPromotionId,
                rawAnchors
            );

        if (!serviceIds.length) {
            return [];
        }

        const selectedNames = [];
        let changed = false;

        serviceIds.forEach(serviceId => {
            const checkbox =
                qs(
                    `.addon-checkbox[data-service-id="${serviceId}"]`
                );

            if (!checkbox) {
                return;
            }

            const serviceName =
                String(
                    checkbox.dataset
                        .name ||
                    "خدمة إضافية"
                ).trim();

            if (serviceName) {
                selectedNames.push(
                    serviceName
                );
            }

            if (!checkbox.checked) {
                /*
                 * مهم: لا نكتفي بتغيير checked برمجيًا.
                 * ننفذ click حقيقي على الـ input حتى تعمل نفس رحلة
                 * العميل اليدوية بالكامل: click + input + change، وبالتالي
                 * تدخل الخدمة في state وفي رابط Checkout وفي أي حسابات مرتبطة.
                 */
                checkbox.click();

                // حماية احتياطية فقط إذا منع كود آخر الـ click الافتراضي.
                if (!checkbox.checked) {
                    checkbox.checked = true;
                    checkbox.dispatchEvent(
                        new Event(
                            "input",
                            { bubbles: true }
                        )
                    );
                    checkbox.dispatchEvent(
                        new Event(
                            "change",
                            { bubbles: true }
                        )
                    );
                }

                if (checkbox.checked) {
                    state.promotionAutoServiceIds
                        .add(serviceId);
                }

                changed = true;
            }
        });

        // بعض الخدمات قد يكون لها عرض مستقل؛ حدث change الخاص بها قد يغيّر
        // promotionId. نعيد تثبيت عرض الباقة المركب بعد اختيار كل الخدمات.
        setSelectedPromotionId(
            parsedPromotionId
        );

        if (
            changed &&
            options.notify !== false &&
            selectedNames.length
        ) {
            showToast(
                `تم اختيار خدمات العرض تلقائيًا: ${selectedNames.join("، ")}`
            );
        }

        return serviceIds;
    }

    function selectPromotion(
        promotionId,
        targetAnchor,
        options = {}
    ) {
        const parsedId =
            Number.parseInt(
                String(
                    promotionId ||
                    "0"
                ),
                10
            ) || 0;

        if (parsedId <= 0) {
            return;
        }

        setSelectedPromotionId(
            parsedId
        );

        const card =
            qs(
                `[data-promotion-card][data-promotion-id="${parsedId}"]`
            );

        card?.scrollIntoView({
            behavior:
                options.cardScroll ===
                    false
                    ? "auto"
                    : "smooth",
            block: "nearest",
            inline: "center"
        });

        const rawAnchors =
            String(
                targetAnchor ||
                card?.dataset
                    .targetAnchor ||
                ""
            )
                .trim();

        // في عروض الباقات لا نضيف الخدمات بمجرد الضغط على بطاقة العرض.
        // الخدمات تدخل للحجز فقط عند اختيار الباقة نفسها.
        if (options.applyServices !== false) {
            applyPromotionIncludedServices(
                parsedId,
                rawAnchors,
                {
                    notify:
                        options.notifyServices !==
                        false
                }
            );
        }

        if (
            options.scrollTarget ===
            false
        ) {
            return;
        }

        const anchors =
            rawAnchors
                .split("|")
                .map(anchor =>
                    anchor
                        .replace(
                            /^#/,
                            ""
                        )
                        .trim()
                )
                .filter(Boolean);

        if (!anchors.length) {
            return;
        }

        const targets =
            anchors
                .map(anchor =>
                    getElement(
                        anchor
                    )
                )
                .filter(Boolean);

        if (!targets.length) {
            return;
        }

        window.setTimeout(
            () => {
                // ننتقل للهدف الرئيسي (الباقة/الغرفة) ونبرز أيضًا كل
                // الخدمات المشمولة بالعرض المركب، بعد اختيارها تلقائيًا للحجز.
                targets[0].scrollIntoView({
                    behavior: "smooth",
                    block: "center"
                });

                targets.forEach(target => {
                    flashPromotionTarget(
                        target
                    );
                });
            },
            90
        );
    }

    window.selectPromotion =
        selectPromotion;

    async function copyPromotionCode(
        button
    ) {
        const code =
            String(
                button?.dataset
                    .copyPromoCode ||
                ""
            ).trim();

        if (!code) {
            return;
        }

        try {
            if (
                navigator.clipboard
                    ?.writeText
            ) {
                await navigator
                    .clipboard
                    .writeText(
                        code
                    );
            } else {
                const input =
                    document
                        .createElement(
                            "input"
                        );

                input.value =
                    code;

                input.style.position =
                    "fixed";

                input.style.opacity =
                    "0";

                document.body
                    .appendChild(
                        input
                    );

                input.select();

                document
                    .execCommand(
                        "copy"
                    );

                input.remove();
            }

            showToast(
                `تم نسخ كود العرض: ${code}`
            );
        } catch {
            showToast(
                "تعذر نسخ الكود تلقائيًا.",
                "error"
            );
        }
    }

    function initializePromotionExperience() {
        const cards =
            qsa(
                "[data-promotion-card]"
            );

        if (!cards.length) {
            return;
        }

        // لا نفعّل أول عرض في الصفحة تلقائيًا.
        // العرض يصبح مختارًا فقط إذا جاء promotionId فعليًا أو اختار العميل
        // باقة/غرفة/عرضًا مرتبطًا به.
        const initialId =
            getSelectedPromotionId() ||
            0;

        if (initialId > 0) {
            setSelectedPromotionId(
                initialId
            );

            const initialCard =
                qs(
                    `[data-promotion-card][data-promotion-id="${initialId}"]`
                );

            const initialTargetType =
                String(
                    initialCard?.dataset
                        .targetType ||
                    ""
                )
                    .trim()
                    .toLowerCase();

            if (initialTargetType !== "package") {
                applyPromotionIncludedServices(
                    initialId,
                    initialCard?.dataset
                        .targetAnchor ||
                    "",
                    {
                        notify: false
                    }
                );
            }
        }

        qsa(
            "[data-promotion-action]"
        ).forEach(button => {
            if (
                button.dataset
                    .promotionActionBound ===
                "true"
            ) {
                return;
            }

            button.dataset
                .promotionActionBound =
                "true";

            button.addEventListener(
                "click",
                event => {
                    event.preventDefault();

                    const promotionId =
                        button.dataset
                            .promotionId ||
                        "0";

                    const targetType =
                        String(
                            button.dataset
                                .targetType ||
                            ""
                        )
                            .trim()
                            .toLowerCase();

                    if (
                        targetType ===
                        "room"
                    ) {
                        selectPromotion(
                            promotionId,
                            "",
                            {
                                scrollTarget:
                                    false
                            }
                        );

                        window.setTimeout(
                            () => {
                                showModalById(
                                    "datePickerModal"
                                );
                            },
                            90
                        );

                        return;
                    }

                    selectPromotion(
                        promotionId,
                        button.dataset
                            .targetAnchor ||
                        "",
                        {
                            scrollTarget:
                                true,
                            applyServices:
                                targetType !==
                                "package"
                        }
                    );
                }
            );
        });

        qsa(
            "[data-copy-promo-code]"
        ).forEach(button => {
            if (
                button.dataset
                    .promotionCodeBound ===
                "true"
            ) {
                return;
            }

            button.dataset
                .promotionCodeBound =
                "true";

            button.addEventListener(
                "click",
                event => {
                    event.preventDefault();
                    event.stopPropagation();

                    void copyPromotionCode(
                        button
                    );
                }
            );
        });

        qsa(
            ".package-select-button[data-promotion-id]"
        ).forEach(button => {
            button.addEventListener(
                "pointerdown",
                () => {
                    const id =
                        Number.parseInt(
                            button.dataset
                                .promotionId ||
                            "0",
                            10
                        ) || 0;

                    setSelectedPromotionId(
                        id
                    );
                },
                {
                    passive: true
                }
            );
        });

        qsa(
            ".service-choice-card[data-promotion-id] .addon-checkbox"
        ).forEach(checkbox => {
            checkbox.addEventListener(
                "change",
                () => {
                    if (!checkbox.checked) {
                        return;
                    }

                    const card =
                        checkbox.closest(
                            ".service-choice-card"
                        );

                    const id =
                        Number.parseInt(
                            card?.dataset
                                .promotionId ||
                            "0",
                            10
                        ) || 0;

                    if (id > 0) {
                        setSelectedPromotionId(
                            id
                        );
                    }
                }
            );
        });

        const hasFinePointer =
            window.matchMedia(
                "(hover: hover) and (pointer: fine)"
            ).matches;

        const reducedMotion =
            window.matchMedia(
                "(prefers-reduced-motion: reduce)"
            ).matches;

        if (
            !hasFinePointer ||
            reducedMotion
        ) {
            return;
        }

        cards.forEach(card => {
            card.addEventListener(
                "pointermove",
                event => {
                    const bounds =
                        card.getBoundingClientRect();

                    const x =
                        (
                            event.clientX -
                            bounds.left
                        ) /
                        bounds.width;

                    const y =
                        (
                            event.clientY -
                            bounds.top
                        ) /
                        bounds.height;

                    const rotateY =
                        (
                            x -
                            0.5
                        ) *
                        3.2;

                    const rotateX =
                        (
                            0.5 -
                            y
                        ) *
                        2.5;

                    card.style
                        .setProperty(
                            "--promo-rx",
                            `${rotateX.toFixed(2)}deg`
                        );

                    card.style
                        .setProperty(
                            "--promo-ry",
                            `${rotateY.toFixed(2)}deg`
                        );

                    card.style
                        .setProperty(
                            "--promo-mx",
                            `${(x * 100).toFixed(1)}%`
                        );

                    card.style
                        .setProperty(
                            "--promo-my",
                            `${(y * 100).toFixed(1)}%`
                        );
                }
            );

            card.addEventListener(
                "pointerleave",
                () => {
                    card.style
                        .setProperty(
                            "--promo-rx",
                            "0deg"
                        );

                    card.style
                        .setProperty(
                            "--promo-ry",
                            "0deg"
                        );

                    card.style
                        .setProperty(
                            "--promo-mx",
                            "50%"
                        );

                    card.style
                        .setProperty(
                            "--promo-my",
                            "50%"
                        );
                }
            );
        });
    }

    /* ======================================================================
       10.B Reveal فخم وهادئ لبقية الصفحة
       ====================================================================== */

    function initializeLuxuryMotion() {
        const targets =
            qsa(
                ".place-intro-card, " +
                ".details-section-card, " +
                ".promotion-showcase-section, " +
                ".premium-booking-card, " +
                ".premium-visit-card"
            );

        if (!targets.length) {
            return;
        }

        const reducedMotion =
            window.matchMedia(
                "(prefers-reduced-motion: reduce)"
            ).matches;

        if (
            reducedMotion ||
            !(
                "IntersectionObserver" in
                window
            )
        ) {
            targets.forEach(target => {
                target.classList.add(
                    "is-revealed"
                );
            });

            return;
        }

        targets.forEach(target => {
            target.classList.add(
                "luxury-reveal"
            );
        });

        const observer =
            new IntersectionObserver(
                entries => {
                    entries.forEach(entry => {
                        if (
                            !entry.isIntersecting
                        ) {
                            return;
                        }

                        entry.target
                            .classList.add(
                                "is-revealed"
                            );

                        observer.unobserve(
                            entry.target
                        );
                    });
                },
                {
                    rootMargin:
                        "0px 0px -8% 0px",
                    threshold: 0.08
                }
            );

        targets.forEach(target =>
            observer.observe(
                target
            )
        );
    }

    /* ======================================================================
       11. المفضلة
       ====================================================================== */

    window.toggleHeart =
        async function toggleHeart(
            event,
            button,
            placeId
        ) {
            event?.preventDefault();
            event?.stopPropagation();

            if (
                !button ||
                button.dataset.busy ===
                "true"
            ) {
                return;
            }

            const icon =
                button.querySelector(
                    "i"
                );

            button.dataset.busy =
                "true";

            button.disabled = true;

            try {
                const response =
                    await fetch(
                        "/Places/ToggleFavorite",
                        {
                            method:
                                "POST",

                            credentials:
                                "same-origin",

                            headers:
                                buildHeaders(
                                    "application/json"
                                ),

                            body:
                                JSON.stringify({
                                    PlaceId:
                                        Number.parseInt(
                                            placeId,
                                            10
                                        ) || 0
                                })
                        }
                    );

                const data =
                    await parseResponse(
                        response
                    );

                if (!data.success) {
                    await notify({
                        icon:
                            "warning",

                        title:
                            "تنبيه",

                        text:
                            data.message ||
                            "تعذر تحديث المفضلة.",

                        confirmButtonText:
                            "حسنًا"
                    });

                    return;
                }

                const active =
                    Boolean(
                        data.isFavorited
                    );

                button.classList.toggle(
                    "is-active",
                    active
                );

                if (icon) {
                    icon.className =
                        active
                            ? "bi bi-heart-fill text-danger"
                            : "bi bi-heart";
                }

                showToast(
                    data.message ||
                    (
                        active
                            ? "تم الحفظ في المفضلة."
                            : "تمت الإزالة من المفضلة."
                    ),
                    active
                        ? "success"
                        : "info"
                );
            } catch (error) {
                console.error(error);

                notify({
                    icon: "error",
                    title:
                        "تعذر الاتصال",
                    text:
                        "لم نتمكن من تحديث المفضلة الآن."
                });
            } finally {
                button.dataset.busy =
                    "false";

                button.disabled =
                    false;
            }
        };

    /* ======================================================================
       12. خريطة Google
       ====================================================================== */

    window.initMap =
        function initMap() {
            const container =
                getElement(
                    "googleMapContainer"
                );

            if (
                !container ||
                typeof window.google ===
                "undefined" ||
                !window.google.maps
            ) {
                return;
            }

            const latitudeText =
                getValue(
                    "placeLat",
                    ""
                );

            const longitudeText =
                getValue(
                    "placeLng",
                    ""
                );

            if (
                !latitudeText ||
                !longitudeText
            ) {
                return;
            }

            const latitude =
                parseCoordinate(
                    latitudeText,
                    Number.NaN
                );

            const longitude =
                parseCoordinate(
                    longitudeText,
                    Number.NaN
                );

            if (
                !Number.isFinite(latitude) ||
                !Number.isFinite(longitude) ||
                latitude < -90 ||
                latitude > 90 ||
                longitude < -180 ||
                longitude > 180
            ) {
                return;
            }

            const location = {
                lat: latitude,
                lng: longitude
            };

            const placeName =
                getValue(
                    "placeNameInput",
                    "موقع المنشأة"
                );

            const systemId =
                Number.parseInt(
                    getValue(
                        "systemIdInput",
                        "0"
                    ),
                    10
                ) || 0;

            const directionsUrl =
                "https://www.google.com/maps/dir/" +
                `?api=1&destination=${latitude},${longitude}`;

            const map =
                new window.google
                    .maps.Map(
                        container,
                        {
                            center:
                                location,

                            zoom: 17,

                            mapTypeId:
                                "hybrid",

                            mapTypeControl:
                                true,

                            zoomControl:
                                true,

                            streetViewControl:
                                true,

                            fullscreenControl:
                                true,

                            mapTypeControlOptions:
                            {
                                style:
                                    window
                                        .google
                                        .maps
                                        .MapTypeControlStyle
                                        .DROPDOWN_MENU,

                                position:
                                    window
                                        .google
                                        .maps
                                        .ControlPosition
                                        .TOP_RIGHT
                            }
                        }
                    );

            const marker =
                new window.google
                    .maps.Marker({
                        position:
                            location,

                        map,

                        title:
                            placeName,

                        animation:
                            window.google
                                .maps
                                .Animation
                                .DROP
                    });

            if (systemId <= 0) {
                marker.addListener(
                    "click",
                    () => {
                        window.open(
                            directionsUrl,
                            "_blank",
                            "noopener"
                        );
                    }
                );

                return;
            }

            const infoWindow =
                new window.google
                    .maps.InfoWindow({
                        content: `
<div
style="
padding:6px;
text-align:right;
direction:rtl;
font-family:Tajawal,sans-serif;
min-width:145px;
">

<strong
style="
display:block;
margin-bottom:8px;
color:#05203c;
text-align:center;
font-size:13px;
">
${escapeHtml(placeName)}
</strong>

<a
href="${directionsUrl}"
target="_blank"
rel="noopener"
style="
display:block;
padding:7px 10px;
border-radius:8px;
background:#12b8d4;
color:#fff;
text-align:center;
text-decoration:none;
font-size:12px;
font-weight:700;
">
فتح الاتجاهات
</a>
</div>
`
                    });

            const openInfo = () => {
                try {
                    infoWindow.open({
                        map,
                        anchor:
                            marker
                    });
                } catch {
                    infoWindow.open(
                        map,
                        marker
                    );
                }
            };

            marker.addListener(
                "click",
                openInfo
            );

            window.setTimeout(
                openInfo,
                800
            );
        };

    /* ======================================================================
       13. الباقات وتقويمها
       ====================================================================== */

    function resetAttendanceCounters() {
        window.bookingData = {
            adults: 0,
            children: 0
        };

        window.lastValidAdults =
            0;

        window.lastValidChildren =
            0;

        const adults =
            getElement(
                "adultsCount"
            );

        const children =
            getElement(
                "childrenCount"
            );

        if (adults) {
            adults.value =
                "0";
        }

        if (children) {
            children.value =
                "0";
        }

        updateCapacityWarning(
            0,
            0,
            0
        );
    }

    function setPackageNextEnabled(
        enabled
    ) {
        const button =
            getElement(
                "btnNextPackage"
            );

        if (!button) {
            return;
        }

        button.disabled =
            !enabled;

        button.classList.toggle(
            "disabled",
            !enabled
        );

        button.classList.toggle(
            "btn-secondary",
            !enabled
        );

        button.classList.toggle(
            "btn-info",
            enabled
        );
    }

    async function initializeOrResetPackageCalendar() {
        const calendarElement =
            getElement(
                "packageInlineCalendar"
            );

        if (!calendarElement) {
            notify({
                icon: "error",
                title:
                    "تعذر فتح التقويم",
                text:
                    "عنصر تقويم الباقة غير موجود في الصفحة."
            });

            return;
        }

        if (
            typeof window.flatpickr ===
            "undefined"
        ) {
            notify({
                icon: "error",
                title:
                    "تعذر فتح التقويم",
                text:
                    "مكتبة Flatpickr غير محملة."
            });

            return;
        }

        const placeId =
            Number.parseInt(
                getValue(
                    "placeIdInput",
                    "0"
                ),
                10
            ) || 0;

        const packageId =
            Number.parseInt(
                String(
                    state.currentPackage.id ||
                    "0"
                ),
                10
            ) || 0;

        if (
            placeId <= 0 ||
            packageId <= 0
        ) {
            notify({
                icon: "error",
                title:
                    "تعذر تحديد الباقة",
                text:
                    "رقم المنشأة أو الباقة غير صحيح."
            });

            return;
        }

        if (state.packageCalendar) {
            try {
                state.packageCalendar
                    .destroy();
            } catch {
                // تجاهل
            }

            state.packageCalendar =
                null;
        }

        state.packageDate =
            "";

        setPackageNextEnabled(
            false
        );

        let disabledDates = [];

        try {
            const parameters =
                new URLSearchParams();

            parameters.set(
                "placeId",
                String(placeId)
            );

            parameters.set(
                "packageId",
                String(packageId)
            );

            const response =
                await fetch(
                    `/Places/GetFullyBookedDates?${parameters.toString()}`,
                    {
                        credentials:
                            "same-origin",

                        cache:
                            "no-store"
                    }
                );

            if (!response.ok) {
                throw new Error(
                    "تعذر تحميل الأيام المحجوزة."
                );
            }

            const data =
                await response.json();

            disabledDates =
                Array.isArray(data)
                    ? data
                    : [];
        } catch (error) {
            console.error(
                "خطأ في تحميل أيام الباقة:",
                error
            );

            notify({
                icon: "error",
                title:
                    "تعذر تحميل المواعيد",
                text:
                    "لم نتمكن من التحقق من الأيام المحجوزة. أعد المحاولة."
            });

            return;
        }

        const bookedDatesSet =
            new Set(
                disabledDates
            );

        state.packageCalendar =
            window.flatpickr(
                calendarElement,
                {
                    inline: true,

                    mode:
                        "single",

                    dateFormat:
                        "Y-m-d",

                    minDate:
                        "today",

                    locale:
                        "en",

                    disableMobile:
                        true,

                    allowInput:
                        false,

                    disable:
                        disabledDates,

                    onDayCreate:
                        (
                            selectedDates,
                            dateString,
                            instance,
                            dayElement
                        ) => {
                            const date =
                                dayElement.dateObj;

                            const year =
                                date.getFullYear();

                            const month =
                                String(
                                    date.getMonth() +
                                    1
                                ).padStart(
                                    2,
                                    "0"
                                );

                            const day =
                                String(
                                    date.getDate()
                                ).padStart(
                                    2,
                                    "0"
                                );

                            const dateKey =
                                `${year}-${month}-${day}`;

                            if (
                                bookedDatesSet.has(
                                    dateKey
                                )
                            ) {
                                dayElement.classList
                                    .add(
                                        "is-booked-date"
                                    );

                                dayElement.title =
                                    "هذا اليوم محجوز";
                            }
                        },

                    onChange:
                        async (
                            selectedDates,
                            dateString,
                            instance
                        ) => {
                            const selected =
                                selectedDates.length >
                                0;

                            state.packageDate =
                                selected
                                    ? dateString
                                    : "";

                            setPackageNextEnabled(
                                false
                            );

                            if (!selected) {
                                return;
                            }

                            const selectedKey =
                                `${placeId}:${packageId}:${dateString}`;

                            if (
                                state.startedPeriodAcknowledgedKey &&
                                state.startedPeriodAcknowledgedKey !== selectedKey
                            ) {
                                state.startedPeriodAcknowledgedKey =
                                    "";
                            }

                            const isDateAvailable =
                                await checkPackageDateBeforeCheckout(
                                    placeId,
                                    packageId,
                                    dateString,
                                    true
                                );

                            if (!isDateAvailable) {
                                state.packageDate =
                                    "";

                                try {
                                    instance.clear(false);
                                } catch {
                                    // تجاهل
                                }

                                setPackageNextEnabled(
                                    false
                                );

                                return;
                            }

                            setPackageNextEnabled(
                                true
                            );
                        }
                }
            );
    }

    window.openPackageModalFromButton =
        function openPackageModalFromButton(
            button
        ) {
            if (!button) {
                return;
            }

            const promotionId =
                Number.parseInt(
                    button.dataset
                        .promotionId ||
                    "0",
                    10
                ) || 0;

            const promotionCard =
                promotionId > 0
                    ? qs(
                        `[data-promotion-card][data-promotion-id="${promotionId}"]`
                    )
                    : null;

            const promotionTargetAnchor =
                String(
                    button.dataset
                        .promotionTargetAnchor ||
                    promotionCard?.dataset
                        .targetAnchor ||
                    ""
                ).trim();

            // اختيار الباقة نفسها هو مصدر الحقيقة.
            // أولًا نلغي فقط الخدمات التي أضافها عرض الباقة السابقة تلقائيًا.
            // الخدمات التي اختارها العميل يدويًا لا توجد في هذه المجموعة ولن تُمسح.
            clearPromotionAutoIncludedServices();

            setSelectedPromotionId(
                promotionId
            );

            if (promotionId > 0) {
                applyPromotionIncludedServices(
                    promotionId,
                    promotionTargetAnchor,
                    {
                        notify: true,
                        replacePrevious: false
                    }
                );
            }

            window.openPackageModal(
                button.dataset.packageId || "0",
                button.dataset
                    .packageName ||
                "",
                button.dataset
                    .packagePrice ||
                "0",
                button.dataset
                    .packageCapacity ||
                "",
                button.dataset
                    .packageTime ||
                "",
                Number.parseInt(
                    button.dataset
                        .packageMax ||
                    "0",
                    10
                ) || 0,
                promotionId,
                promotionTargetAnchor
            );
        };

    window.openPackageModal =
        function openPackageModal(
            packageId,
            name,
            price,
            capacity,
            time,
            maxCapacity,
            promotionId = 0,
            promotionTargetAnchor = ""
        ) {
            state.currentPackage = {
                id:
                    Number.parseInt(
                        packageId,
                        10
                    ) || 0,

                name:
                    String(
                        name || ""
                    ),

                price:
                    String(
                        price || ""
                    ),

                capacityText:
                    String(
                        capacity || ""
                    ),

                time:
                    String(
                        time || ""
                    ),

                maxCapacity:
                    Number.parseInt(
                        maxCapacity,
                        10
                    ) || 0,

                promotionId:
                    Number.parseInt(
                        String(
                            promotionId ||
                            "0"
                        ),
                        10
                    ) || 0,

                promotionTargetAnchor:
                    String(
                        promotionTargetAnchor ||
                        ""
                    ).trim()
            };

            window
                .currentPackageMaxCapacity =
                state
                    .currentPackage
                    .maxCapacity;

            resetAttendanceCounters();

            setText(
                "#modalPkgName",
                state
                    .currentPackage
                    .name ||
                "-"
            );

            setText(
                "#modalPkgPrice",
                state
                    .currentPackage
                    .price ||
                "-"
            );

            setText(
                "#modalPkgCap",
                state
                    .currentPackage
                    .capacityText ||
                "-"
            );

            setText(
                "#modalPkgTime",
                state
                    .currentPackage
                    .time ||
                "-"
            );

            state.packageDate =
                "";

            setPackageNextEnabled(
                false
            );

            const modalElement =
                getElement(
                    "packageDatePickerModal"
                );

            const modal =
                getBootstrapModal(
                    modalElement
                );

            if (
                !modalElement ||
                !modal
            ) {
                notify({
                    icon: "error",
                    title:
                        "تعذر فتح الباقة",
                    text:
                        "نافذة الباقة غير موجودة أو Bootstrap غير محمل."
                });

                return;
            }

            let initialized =
                false;

            const prepareCalendar =
                () => {
                    if (
                        initialized
                    ) {
                        return;
                    }

                    initialized =
                        true;

                    try {
                        initializeOrResetPackageCalendar();
                    } catch (error) {
                        console.error(
                            "خطأ في تقويم الباقة:",
                            error
                        );

                        notify({
                            icon:
                                "error",

                            title:
                                "تعذر تحميل التقويم",

                            text:
                                "تم فتح النافذة لكن تعذر تجهيز التقويم."
                        });
                    }
                };

            if (
                modalElement
                    .classList
                    .contains("show")
            ) {
                prepareCalendar();
            } else {
                modalElement
                    .addEventListener(
                        "shown.bs.modal",
                        prepareCalendar,
                        {
                            once: true
                        }
                    );

                modal.show();

                window.setTimeout(
                    () => {
                        if (
                            modalElement
                                .classList
                                .contains(
                                    "show"
                                )
                        ) {
                            prepareCalendar();
                        }
                    },
                    300
                );
            }
        };

    window.openAttendanceModal =
        function openAttendanceModal() {
            if (!state.packageDate) {
                notify({
                    icon:
                        "warning",

                    title:
                        "اختر يوم الحجز",

                    text:
                        "حدد اليوم المناسب أولًا ثم اضغط التالي."
                });

                return;
            }

            switchModal(
                "packageDatePickerModal",
                "attendanceModal"
            );
        };

    window.openHallModal =
        function openHallModal() {
            if (!state.packageDate) {
                notify({
                    icon:
                        "warning",

                    title:
                        "اختر يوم الحجز",

                    text:
                        "حدد اليوم المناسب أولًا ثم اضغط التالي."
                });

                return;
            }

            switchModal(
                "packageDatePickerModal",
                "hallBookingModal"
            );
        };

    /* ======================================================================
       14. عدادات الأشخاص والسعة
       ====================================================================== */

    window.smartUpdateCount =
        function smartUpdateCount(
            inputId,
            change
        ) {
            const input =
                getElement(inputId);

            if (!input) {
                return;
            }

            const current =
                Number.parseInt(
                    input.value ||
                    "0",
                    10
                ) || 0;

            input.value =
                String(
                    Math.max(
                        0,
                        current +
                        (
                            Number(
                                change
                            ) || 0
                        )
                    )
                );

            window.syncBookingNumbers(
                inputId ===
                    "adultsCount"
                    ? "adults"
                    : "children"
            );
        };

    window.syncBookingNumbers =
        function syncBookingNumbers(
            changedField
        ) {
            const adultsInput =
                getElement(
                    "adultsCount"
                );

            const childrenInput =
                getElement(
                    "childrenCount"
                );

            if (
                !adultsInput ||
                !childrenInput
            ) {
                return;
            }

            if (
                adultsInput.value ===
                "" ||
                childrenInput.value ===
                ""
            ) {
                return;
            }

            let adults =
                Math.max(
                    0,
                    Number.parseInt(
                        adultsInput
                            .value ||
                        "0",
                        10
                    ) || 0
                );

            let children =
                Math.max(
                    0,
                    Number.parseInt(
                        childrenInput
                            .value ||
                        "0",
                        10
                    ) || 0
                );

            let baseCapacity =
                window
                    .currentPackageMaxCapacity ||
                state
                    .currentPackage
                    .maxCapacity ||
                0;

            if (!baseCapacity) {
                const capacityText =
                    getElement(
                        "modalPkgCap"
                    )?.textContent ||
                    "";

                const match =
                    capacityText.match(
                        /\d+/
                    );

                baseCapacity =
                    match
                        ? Number.parseInt(
                            match[0],
                            10
                        )
                        : 0;
            }

            const absoluteMaximum =
                baseCapacity > 0
                    ? baseCapacity + 2
                    : 0;

            if (
                absoluteMaximum > 0 &&
                adults + children >
                absoluteMaximum
            ) {
                notify({
                    icon: "error",
                    title:
                        "تجاوز الحد الأقصى",
                    text:
                        `لا يمكن أن يتجاوز العدد ` +
                        `${absoluteMaximum} أشخاص.`
                });

                if (
                    changedField ===
                    "adults"
                ) {
                    adults =
                        window
                            .lastValidAdults;
                } else {
                    children =
                        window
                            .lastValidChildren;
                }
            } else {
                window.lastValidAdults =
                    adults;

                window.lastValidChildren =
                    children;
            }

            adultsInput.value =
                String(
                    window
                        .lastValidAdults
                );

            childrenInput.value =
                String(
                    window
                        .lastValidChildren
                );

            window.bookingData.adults =
                window
                    .lastValidAdults;

            window.bookingData.children =
                window
                    .lastValidChildren;

            updateCapacityWarning(
                window
                    .lastValidAdults +
                window
                    .lastValidChildren,
                baseCapacity,
                absoluteMaximum
            );
        };

    function updateCapacityWarning(
        total,
        baseCapacity,
        absoluteMaximum
    ) {
        const alert =
            getElement(
                "capacityWarning"
            );

        const text =
            getElement(
                "warningText"
            );

        if (!alert || !text) {
            return;
        }

        alert.classList.remove(
            "alert-info",
            "text-info",
            "border-info",
            "alert-danger",
            "text-danger",
            "border-danger",
            "is-info",
            "is-danger"
        );

        if (
            baseCapacity <= 0 ||
            total < baseCapacity
        ) {
            alert.classList.add(
                "d-none"
            );

            text.textContent =
                "";

            return;
        }

        alert.classList.remove(
            "d-none"
        );

        if (
            total ===
            baseCapacity &&
            baseCapacity <
            absoluteMaximum
        ) {
            text.innerHTML =
                "اكتمل العدد الأساسي للباقة، " +
                "ويمكن إضافة " +
                "<strong>شخصين إضافيين</strong> " +
                "كحد أقصى.";

            alert.classList.add(
                "alert-info",
                "text-info",
                "border-info",
                "is-info"
            );
        } else if (
            total >=
            absoluteMaximum
        ) {
            text.innerHTML =
                `وصلت إلى الحد الأقصى ` +
                `<strong>(${absoluteMaximum} أشخاص)</strong>.`;

            alert.classList.add(
                "alert-danger",
                "text-danger",
                "border-danger",
                "is-danger"
            );
        } else {
            text.innerHTML =
                "تمت إضافة شخص إضافي، " +
                "ويتبقى <strong>شخص واحد</strong> " +
                "للوصول إلى الحد الأقصى.";

            alert.classList.add(
                "alert-info",
                "text-info",
                "border-info",
                "is-info"
            );
        }
    }

    /* ======================================================================
       15. ترتيب القاعة
       ====================================================================== */

    window.selectSeating =
        function selectSeating(
            element,
            type
        ) {
            if (!element) {
                return;
            }

            state
                .selectedSeatingSetup =
                String(
                    type || ""
                ).trim() ||
                "غير محدد";

            qsa(
                ".seating-option"
            ).forEach(option => {
                const active =
                    option === element;

                option.classList.toggle(
                    "active-seating",
                    active
                );

                option.classList.toggle(
                    "border-info",
                    active
                );

                option.classList.toggle(
                    "border-light",
                    !active
                );

                option.setAttribute(
                    "aria-pressed",
                    active
                        ? "true"
                        : "false"
                );

                const icon =
                    option.querySelector(
                        ".radio-icon"
                    );

                if (icon) {
                    icon.classList.toggle(
                        "bi-record-circle",
                        active
                    );

                    icon.classList.toggle(
                        "text-info",
                        active
                    );

                    icon.classList.toggle(
                        "bi-circle",
                        !active
                    );

                    icon.classList.toggle(
                        "text-muted",
                        !active
                    );
                }
            });
        };

    /* ======================================================================
       16. الحجز والانتقال للصفحات التالية
       ====================================================================== */

    function restoreCurrentPackagePromotionSelection() {
        const promotionId =
            Number.parseInt(
                String(
                    state.currentPackage
                        .promotionId ||
                    "0"
                ),
                10
            ) || 0;

        // الباقة التي اختارها العميل هي التي تحدد العرض.
        setSelectedPromotionId(
            promotionId
        );

        if (promotionId > 0) {
            applyPromotionIncludedServices(
                promotionId,
                state.currentPackage
                    .promotionTargetAnchor ||
                "",
                {
                    notify: false,
                    replacePrevious: false
                }
            );
        } else {
            clearPromotionAutoIncludedServices();
        }
    }

    function appendSelectedServiceIds(
        parameters
    ) {
        const selectedServiceIds =
            qsa(
                ".addon-checkbox:checked"
            )
                .map(checkbox =>
                    Number.parseInt(
                        checkbox.dataset
                            .serviceId ||
                        "0",
                        10
                    )
                )
                .filter(serviceId =>
                    Number.isInteger(
                        serviceId
                    ) &&
                    serviceId > 0
                );

        selectedServiceIds
            .forEach(serviceId => {
                parameters.append(
                    "serviceIds",
                    String(serviceId)
                );
            });
    }

    function getRequiredPlaceId() {
        const placeId =
            Number.parseInt(
                getValue(
                    "placeIdInput",
                    "0"
                ),
                10
            ) || 0;

        if (placeId <= 0) {
            notify({
                icon: "error",
                title:
                    "تعذر تحديد المنشأة",
                text:
                    "رقم المنشأة غير صحيح. أعد تحميل الصفحة."
            });

            return 0;
        }

        return placeId;
    }

    window.goToAvailableRooms =
        function goToAvailableRooms() {
            const start =
                getValue(
                    "startDate"
                );

            const end =
                getValue(
                    "endDate"
                );

            const placeId =
                getRequiredPlaceId();

            if (
                placeId <= 0 ||
                !window.validateDates()
            ) {
                if (placeId > 0) {
                    notify({
                        icon: "warning",
                        title:
                            "تحقق من التواريخ",
                        text:
                            "اختر تاريخ دخول وخروج صحيحين أولًا."
                    });
                }

                return;
            }

            const parameters =
                new URLSearchParams();

            parameters.set(
                "placeId",
                String(placeId)
            );

            parameters.set(
                "start",
                start
            );

            parameters.set(
                "end",
                end
            );

            appendSelectedServiceIds(
                parameters
            );

            appendSelectedPromotion(
                parameters
            );

            recordAdvertisementBookingStart(
                placeId
            );

            window.location.href =
                `/Places/AvailableRooms?${parameters.toString()}`;
        };

    function buildAvailabilityAlertOptions(
        message,
        title = "الموعد غير متاح"
    ) {
        return {
            icon: "warning",
            title,
            text:
                message ||
                "هذا الموعد لم يعد متاحًا. اختر يومًا آخر.",
            confirmButtonText:
                "اختيار موعد آخر",
            showConfirmButton: true,
            buttonsStyling: false,
            allowOutsideClick: false,
            allowEscapeKey: true,
            customClass: {
                container:
                    "booking-availability-swal-container",
                popup:
                    "booking-availability-swal",
                icon:
                    "booking-availability-swal__icon",
                title:
                    "booking-availability-swal__title",
                htmlContainer:
                    "booking-availability-swal__message",
                actions:
                    "booking-availability-swal__actions",
                confirmButton:
                    "booking-availability-swal__confirm"
            }
        };
    }

    function reopenPackageDatePicker() {
        const dateModalElement =
            getElement(
                "packageDatePickerModal"
            );

        const dateModal =
            getBootstrapModal(
                dateModalElement
            );

        if (
            !dateModalElement ||
            !dateModal
        ) {
            return;
        }

        const currentModalId =
            [
                "attendanceModal",
                "hallBookingModal"
            ].find(
                id =>
                    getElement(id)
                        ?.classList
                        .contains("show")
            );

        const refreshCalendar =
            () => {
                window.setTimeout(
                    () => {
                        initializeOrResetPackageCalendar()
                            .catch(
                                error =>
                                    console.error(
                                        "تعذر تحديث تقويم الباقة:",
                                        error
                                    )
                            );
                    },
                    80
                );
            };

        dateModalElement
            .addEventListener(
                "shown.bs.modal",
                refreshCalendar,
                {
                    once: true
                }
            );

        if (currentModalId) {
            switchModal(
                currentModalId,
                "packageDatePickerModal"
            );

            return;
        }

        dateModal.show();

        if (
            dateModalElement
                .classList
                .contains("show")
        ) {
            refreshCalendar();
        }
    }

    async function showPackageAvailabilityAlert(
        message,
        returnToCalendar = true
    ) {
        const result =
            await notify(
                buildAvailabilityAlertOptions(
                    message
                )
            );

        if (
            returnToCalendar &&
            (
                !result ||
                result.isConfirmed !== false
            )
        ) {
            reopenPackageDatePicker();
        }

        return result;
    }

    window.goToNextPackageStep =
        async function goToNextPackageStep(
            isHall
        ) {
            const placeId =
                getRequiredPlaceId();

            const packageId =
                Number.parseInt(
                    String(
                        state.currentPackage.id ||
                        "0"
                    ),
                    10
                ) || 0;

            if (
                placeId <= 0 ||
                packageId <= 0 ||
                !state.packageDate
            ) {
                await showPackageAvailabilityAlert(
                    "اختر يوم الحجز أولًا ثم حاول مرة أخرى.",
                    false
                );
                return;
            }

            const isDateAvailable =
                await checkPackageDateBeforeCheckout(
                    placeId,
                    packageId,
                    state.packageDate,
                    true
                );

            if (!isDateAvailable) {
                return;
            }

            if (isHall === true) {
                window.openHallModal();
            } else {
                window.openAttendanceModal();
            }
        };

    async function checkPackageDateBeforeCheckout(
        placeId,
        packageId,
        date,
        showStartedWarning = true
    ) {
        if (
            state
                .packageAvailabilityCheckInFlight
        ) {
            return false;
        }

        state
            .packageAvailabilityCheckInFlight =
            true;

        try {
            const parameters =
                new URLSearchParams();

            parameters.set(
                "placeId",
                String(placeId)
            );

            parameters.set(
                "packageId",
                String(packageId)
            );

            parameters.set(
                "date",
                date
            );

            const response =
                await fetch(
                    `/Places/CheckPackageDateAvailability?${parameters.toString()}`,
                    {
                        method:
                            "GET",

                        credentials:
                            "same-origin",

                        cache:
                            "no-store",

                        headers: {
                            Accept:
                                "application/json"
                        }
                    }
                );

            let data = null;

            try {
                data =
                    await response.json();
            } catch {
                data = null;
            }

            if (
                !response.ok ||
                data?.available !== true
            ) {
                await showPackageAvailabilityAlert(
                    data?.message ||
                    "تعذر تأكيد توفر هذا الموعد. اختر يومًا آخر أو أعد المحاولة."
                );

                return false;
            }

            const startedPeriodKey =
                `${placeId}:${packageId}:${date}`;

            if (
                showStartedWarning &&
                data?.started === true &&
                state.startedPeriodAcknowledgedKey !== startedPeriodKey
            ) {
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
                    state.startedPeriodAcknowledgedKey =
                        "";

                    reopenPackageDatePicker();
                    return false;
                }

                state.startedPeriodAcknowledgedKey =
                    startedPeriodKey;
            }

            return true;
        } catch (error) {
            console.error(
                "خطأ في التحقق النهائي من موعد الباقة:",
                error
            );

            await showPackageAvailabilityAlert(
                "تعذر الاتصال بالخادم للتحقق من الموعد. أعد المحاولة بعد لحظات.",
                false
            );

            return false;
        } finally {
            state
                .packageAvailabilityCheckInFlight =
                false;
        }
    }

    function showBookingErrorFromQuery() {
        const url =
            new URL(
                window.location.href
            );

        const message =
            url.searchParams.get(
                "bookingError"
            );

        if (!message) {
            return;
        }

        url.searchParams.delete(
            "bookingError"
        );

        url.searchParams.delete(
            "bookingPackageId"
        );

        window.history.replaceState(
            window.history.state,
            "",
            `${url.pathname}${url.search}${url.hash}`
        );

        window.setTimeout(
            () => {
                showPackageAvailabilityAlert(
                    message,
                    false
                );
            },
            120
        );
    }


    window.goToFinalCheckout =
        async function goToFinalCheckout() {
            const placeId =
                getRequiredPlaceId();

            const packageId =
                Number.parseInt(
                    String(
                        state.currentPackage.id ||
                        "0"
                    ),
                    10
                ) || 0;

            const adults =
                Number.parseInt(
                    getValue(
                        "adultsCount",
                        "0"
                    ),
                    10
                ) || 0;

            const children =
                Number.parseInt(
                    getValue(
                        "childrenCount",
                        "0"
                    ),
                    10
                ) || 0;

            if (
                placeId <= 0 ||
                packageId <= 0
            ) {
                notify({
                    icon: "error",
                    title:
                        "تعذر تحديد الباقة",
                    text:
                        "بيانات الباقة غير مكتملة. اختر الباقة مرة أخرى."
                });

                return;
            }

            if (!state.packageDate) {
                notify({
                    icon: "warning",
                    title:
                        "اختر تاريخ الحجز",
                    text:
                        "ارجع واختر يوم الباقة أولًا."
                });

                return;
            }

            if (
                adults +
                children < 1
            ) {
                notify({
                    icon: "warning",
                    title:
                        "حدد عدد الأشخاص",
                    text:
                        "يجب اختيار شخص واحد على الأقل للمتابعة."
                });

                return;
            }

            // قبل بناء رابط Checkout نعيد تثبيت عرض الباقة وخدماته.
            // أي حدث سابق لا يستطيع إسقاط promotionId الخاص بالباقة المختارة.
            restoreCurrentPackagePromotionSelection();

            const parameters =
                new URLSearchParams();

            parameters.set(
                "placeId",
                String(placeId)
            );

            parameters.set(
                "packageId",
                String(packageId)
            );

            parameters.set(
                "startDate",
                state.packageDate
            );

            parameters.set(
                "endDate",
                state.packageDate
            );

            parameters.set(
                "quantity",
                "1"
            );

            parameters.set(
                "adultsCount",
                String(adults)
            );

            parameters.set(
                "childrenCount",
                String(children)
            );

            const bookingType =
                resolveBookingTypeValue();

            if (!bookingType.valid) {
                notify({
                    icon: "warning",
                    title: "أكمل نوع الحجز",
                    text:
                        bookingType.message ||
                        "اكتب نوع الحجز قبل المتابعة."
                });

                return;
            }

            parameters.set(
                "bookingType",
                bookingType.value
            );

            appendSelectedServiceIds(
                parameters
            );

            appendSelectedPromotion(
                parameters
            );

            const isDateAvailable =
                await checkPackageDateBeforeCheckout(
                    placeId,
                    packageId,
                    state.packageDate,
                    false
                );

            if (!isDateAvailable) {
                return;
            }

            const checkoutUrl =
                `/BookingFlow/Checkout?${parameters.toString()}`;

            if (
                !await ensureLoginBeforeBooking(
                    checkoutUrl
                )
            ) {
                return;
            }

            recordAdvertisementBookingStart(
                placeId
            );

            window.location.href =
                checkoutUrl;
        };

    window.goToFinalCheckoutForHall =
        async function goToFinalCheckoutForHall() {
            const placeId =
                getRequiredPlaceId();

            const packageId =
                Number.parseInt(
                    String(
                        state.currentPackage.id ||
                        "0"
                    ),
                    10
                ) || 0;

            if (
                placeId <= 0 ||
                packageId <= 0
            ) {
                notify({
                    icon: "error",
                    title:
                        "تعذر تحديد الباقة",
                    text:
                        "بيانات الباقة غير مكتملة. اختر الباقة مرة أخرى."
                });

                return;
            }

            if (!state.packageDate) {
                notify({
                    icon: "warning",
                    title:
                        "اختر تاريخ الحجز",
                    text:
                        "ارجع واختر يوم الباقة أولًا."
                });

                return;
            }

            const activeSeat =
                qs(
                    ".seating-option.active-seating " +
                    ".seating-text-value"
                );

            const seating =
                activeSeat
                    ?.textContent
                    .trim() ||
                state.selectedSeatingSetup ||
                "غير محدد";

            // قبل بناء رابط Checkout نعيد تثبيت عرض الباقة وخدماته.
            // أي حدث سابق لا يستطيع إسقاط promotionId الخاص بالباقة المختارة.
            restoreCurrentPackagePromotionSelection();

            const parameters =
                new URLSearchParams();

            parameters.set(
                "placeId",
                String(placeId)
            );

            parameters.set(
                "packageId",
                String(packageId)
            );

            parameters.set(
                "startDate",
                state.packageDate
            );

            parameters.set(
                "endDate",
                state.packageDate
            );

            parameters.set(
                "quantity",
                "1"
            );

            parameters.set(
                "adultsCount",
                "0"
            );

            parameters.set(
                "childrenCount",
                "0"
            );

            parameters.set(
                "bookingType",
                getValue(
                    "hallEventTypeSelect",
                    "غير محدد"
                )
            );

            parameters.set(
                "seatingSetup",
                seating
            );

            appendSelectedServiceIds(
                parameters
            );

            appendSelectedPromotion(
                parameters
            );

            const isDateAvailable =
                await checkPackageDateBeforeCheckout(
                    placeId,
                    packageId,
                    state.packageDate,
                    false
                );

            if (!isDateAvailable) {
                return;
            }

            const checkoutUrl =
                `/BookingFlow/Checkout?${parameters.toString()}`;

            if (
                !await ensureLoginBeforeBooking(
                    checkoutUrl
                )
            ) {
                return;
            }

            recordAdvertisementBookingStart(
                placeId
            );

            window.location.href =
                checkoutUrl;
        };

    /* ======================================================================
       17. لوحة التقييمات
       ====================================================================== */

    function isMobileReviewsMode() {
        return window.matchMedia(
            "(max-width: 767.98px)"
        ).matches;
    }

    function rememberReviewsHome(container) {
        if (
            !container ||
            state.reviewsPortalParent
        ) {
            return;
        }

        state.reviewsPortalParent =
            container.parentNode;

        state.reviewsPortalNextSibling =
            container.nextSibling;
    }

    function mountReviewsToViewport(container) {
        if (
            !container ||
            !isMobileReviewsMode()
        ) {
            return;
        }

        rememberReviewsHome(
            container
        );

        if (
            container.parentNode ===
            document.body
        ) {
            state.reviewsPortaledToBody =
                true;
            return;
        }

        document.body.appendChild(
            container
        );

        state.reviewsPortaledToBody =
            true;
    }

    function restoreReviewsHome(container) {
        if (
            !container ||
            !state.reviewsPortaledToBody ||
            !state.reviewsPortalParent
        ) {
            return;
        }

        const parent =
            state.reviewsPortalParent;

        const nextSibling =
            state.reviewsPortalNextSibling;

        if (
            nextSibling &&
            nextSibling.parentNode === parent
        ) {
            parent.insertBefore(
                container,
                nextSibling
            );
        } else {
            parent.appendChild(
                container
            );
        }

        state.reviewsPortaledToBody =
            false;
    }

    function initializeReviewsPanel() {
        const container =
            qs(
                SELECTORS
                    .reviewsContainer
            );

        if (!container) {
            return;
        }

        rememberReviewsHome(
            container
        );

        state.reviewsOpen =
            window.getComputedStyle(
                container
            ).display !== "none";

        container.setAttribute(
            "aria-hidden",
            state.reviewsOpen
                ? "false"
                : "true"
        );

        window.addEventListener(
            "resize",
            () => {
                if (
                    !state.reviewsOpen
                ) {
                    return;
                }

                if (
                    isMobileReviewsMode()
                ) {
                    mountReviewsToViewport(
                        container
                    );

                    lockPageForReviews();
                } else {
                    unlockPageForReviews();

                    restoreReviewsHome(
                        container
                    );
                }
            },
            {
                passive: true
            }
        );
    }

    function lockPageForReviews() {
        if (
            state
                .bodyLockedByReviews ||
            !isMobileReviewsMode()
        ) {
            return;
        }

        state.reviewsScrollY =
            window.scrollY;

        state.bodyLockedByReviews =
            true;

        document.body
            .classList.add(
                "reviews-sheet-open"
            );

        document.body
            .style.position =
            "fixed";

        document.body.style.top =
            `-${state.reviewsScrollY}px`;

        document.body.style.left =
            "0";

        document.body.style.right =
            "0";

        document.body.style.width =
            "100%";

        document.body
            .style.overflow =
            "hidden";

        const bookingBar =
            qs(
                SELECTORS
                    .mobileBookingBar
            );

        if (bookingBar) {
            bookingBar.dataset
                .previousDisplay =
                bookingBar
                    .style
                    .display ||
                "";

            bookingBar.style.display =
                "none";
        }
    }

    function unlockPageForReviews() {
        if (
            !state
                .bodyLockedByReviews
        ) {
            return;
        }

        const scrollY =
            state.reviewsScrollY;

        state.bodyLockedByReviews =
            false;

        document.body
            .classList.remove(
                "reviews-sheet-open"
            );

        document.body.style
            .removeProperty(
                "position"
            );

        document.body.style
            .removeProperty("top");

        document.body.style
            .removeProperty("left");

        document.body.style
            .removeProperty("right");

        document.body.style
            .removeProperty("width");

        if (
            !qs(".modal.show")
        ) {
            document.body.style
                .removeProperty(
                    "overflow"
                );
        }

        const bookingBar =
            qs(
                SELECTORS
                    .mobileBookingBar
            );

        if (bookingBar) {
            bookingBar.style.display =
                bookingBar
                    .dataset
                    .previousDisplay ||
                "";

            delete bookingBar
                .dataset
                .previousDisplay;
        }

        window.scrollTo({
            top: scrollY,
            behavior: "auto"
        });
    }

    function setReviewsOpen(
        open,
        options = {}
    ) {
        const container =
            qs(
                SELECTORS
                    .reviewsContainer
            );

        const trigger =
            qs(
                SELECTORS
                    .reviewsTrigger
            );

        const icon =
            qs(
                SELECTORS
                    .reviewsIcon
            );

        const text =
            qs(
                SELECTORS
                    .reviewsText
            );

        const summary =
            qs(
                ".reviews-summary-card"
            );

        if (!container) {
            return;
        }

        state.reviewsOpen =
            Boolean(open);

        if (
            state.reviewsOpen &&
            isMobileReviewsMode()
        ) {
            /*
             * مهم:
             * لوحة التقييمات كانت داخل .place-details-shell الذي يستخدم
             * perspective وداخل .details-section-card الذي يستخدم overflow:clip.
             * هذا يحول position:fixed إلى عنصر محصور داخل البطاقة على الهاتف.
             * ننقل اللوحة مؤقتًا إلى body حتى ترتبط بالـ viewport الحقيقي.
             */
            mountReviewsToViewport(
                container
            );
        } else if (
            state.reviewsOpen
        ) {
            restoreReviewsHome(
                container
            );
        }

        container.style.display =
            state.reviewsOpen
                ? "block"
                : "none";

        container.setAttribute(
            "aria-hidden",
            state.reviewsOpen
                ? "false"
                : "true"
        );

        summary?.setAttribute(
            "aria-expanded",
            state.reviewsOpen
                ? "true"
                : "false"
        );

        if (icon) {
            icon.classList.toggle(
                "bi-chevron-up",
                state.reviewsOpen
            );

            icon.classList.toggle(
                "bi-chevron-down",
                !state.reviewsOpen
            );
        }

        if (text) {
            text.textContent =
                state.reviewsOpen
                    ? "إخفاء التقييمات والتعليقات"
                    : "عرض التقييمات أو إضافة تقييم";
        }

        if (
            state.reviewsOpen &&
            isMobileReviewsMode()
        ) {
            lockPageForReviews();

            window.requestAnimationFrame(
                () => {
                    const scrollArea =
                        qs(
                            ".reviews-scroll-area",
                            container
                        );

                    if (scrollArea) {
                        scrollArea.style
                            .overscrollBehaviorY =
                            "contain";

                        scrollArea.style
                            .webkitOverflowScrolling =
                            "touch";
                    }
                }
            );
        } else {
            unlockPageForReviews();

            if (
                !state.reviewsOpen
            ) {
                restoreReviewsHome(
                    container
                );
            }
        }

        if (
            state.reviewsOpen &&
            options.scroll !==
            false &&
            !isMobileReviewsMode()
        ) {
            window.setTimeout(
                () => {
                    trigger
                        ?.scrollIntoView({
                            behavior:
                                "smooth",
                            block:
                                "start"
                        });
                },
                70
            );
        }
    }

    window.toggleReviewsContainer =
        function toggleReviewsContainer() {
            setReviewsOpen(
                !state.reviewsOpen
            );
        };

    window.toggleRepliesVisibility =
        function toggleRepliesVisibility(
            reviewId,
            count
        ) {
            const replies =
                getElement(
                    `nested-replies-${reviewId}`
                );

            const label =
                getElement(
                    `toggle-text-${reviewId}`
                );

            const toggleButton =
                getElement(
                    `toggle-btn-${reviewId}`
                );

            if (!replies) {
                return;
            }

            const open =
                window.getComputedStyle(
                    replies
                ).display === "none";

            replies.style.display =
                open
                    ? "block"
                    : "none";

            replies.setAttribute(
                "aria-hidden",
                open
                    ? "false"
                    : "true"
            );

            toggleButton?.setAttribute(
                "aria-expanded",
                open
                    ? "true"
                    : "false"
            );

            if (label) {
                label.textContent =
                    open
                        ? "إخفاء الردود"
                        : `عرض ${count} من الردود`;
            }
        };

    window.filterReviews =
        function filterReviews(
            type,
            button
        ) {
            qsa(
                ".review-filter-chip"
            ).forEach(chip => {
                chip.classList.toggle(
                    "active",
                    chip === button
                );
            });

            let visible = 0;

            qsa(
                '.review-node-item[data-depth="0"]'
            ).forEach(review => {
                const stars =
                    Number.parseInt(
                        review.dataset
                            .stars ||
                        "0",
                        10
                    ) || 0;

                let show =
                    type === "all";

                if (
                    type ===
                    "positive"
                ) {
                    show =
                        stars >= 4;
                } else if (
                    type ===
                    "negative"
                ) {
                    show =
                        stars >= 1 &&
                        stars <= 3;
                } else if (
                    /^[1-5]$/.test(
                        String(type)
                    )
                ) {
                    show =
                        stars ===
                        Number.parseInt(
                            type,
                            10
                        );
                }

                review.style.display =
                    show
                        ? ""
                        : "none";

                if (show) {
                    visible += 1;
                }
            });

            const empty =
                getElement(
                    "noReviewsFilterMessage"
                );

            if (empty) {
                empty.classList.toggle(
                    "d-none",
                    visible > 0
                );

                empty.classList.toggle(
                    "d-flex",
                    visible === 0
                );
            }
        };

    function focusReviewComposer() {
        const input =
            qs(
                SELECTORS
                    .reviewInput
            );

        if (!input) {
            return;
        }

        if (
            !state.reviewsOpen
        ) {
            setReviewsOpen(
                true,
                {
                    scroll: false
                }
            );
        }

        window.setTimeout(
            () => {
                input.focus();

                if (
                    !isMobileReviewsMode()
                ) {
                    input
                        .scrollIntoView({
                            behavior:
                                "smooth",
                            block:
                                "center"
                        });
                }
            },
            130
        );
    }

    window.prepareInstaReply =
        function prepareInstaReply(
            reviewId,
            userName
        ) {
            const editInput =
                qs(
                    SELECTORS
                        .editReviewInput
                );

            const parentInput =
                qs(
                    SELECTORS
                        .parentReviewInput
                );

            const input =
                qs(
                    SELECTORS
                        .reviewInput
                );

            if (
                !editInput ||
                !parentInput ||
                !input
            ) {
                return;
            }

            editInput.value =
                "";

            parentInput.value =
                String(reviewId);

            input.value =
                `@${userName} `;

            getElement(
                "interactiveStars"
            )?.classList.add(
                "d-none"
            );

            getElement(
                "cancelReplyBtn"
            )?.classList.remove(
                "d-none"
            );

            const indicator =
                getElement(
                    "replyingToIndicator"
                );

            if (indicator) {
                indicator.innerHTML =
                    "رد على " +
                    `<strong class="text-primary">` +
                    `@${escapeHtml(
                        userName
                    )}` +
                    "</strong>";
            }

            setPostButtonLabel(
                "نشر الرد"
            );

            focusReviewComposer();
        };

    window.editInstaReview =
        function editInstaReview(
            reviewId
        ) {
            const block =
                getElement(
                    `review-block-${reviewId}`
                );

            const reviewText =
                block
                    ?.querySelector(
                        ".review-text-content"
                    )
                    ?.textContent
                    .trim();

            const editInput =
                qs(
                    SELECTORS
                        .editReviewInput
                );

            const parentInput =
                qs(
                    SELECTORS
                        .parentReviewInput
                );

            const input =
                qs(
                    SELECTORS
                        .reviewInput
                );

            if (
                !block ||
                !reviewText ||
                !editInput ||
                !parentInput ||
                !input
            ) {
                return;
            }

            parentInput.value =
                "";

            editInput.value =
                String(reviewId);

            input.value =
                reviewText;

            getElement(
                "interactiveStars"
            )?.classList.add(
                "d-none"
            );

            getElement(
                "cancelReplyBtn"
            )?.classList.remove(
                "d-none"
            );

            const indicator =
                getElement(
                    "replyingToIndicator"
                );

            if (indicator) {
                indicator.innerHTML =
                    '<strong class="text-warning">' +
                    "وضع التعديل" +
                    "</strong>";
            }

            setPostButtonLabel(
                "حفظ التعديل"
            );

            focusReviewComposer();
        };

    window.cancelInstaReply =
        function cancelInstaReply() {
            const editInput =
                qs(
                    SELECTORS
                        .editReviewInput
                );

            const parentInput =
                qs(
                    SELECTORS
                        .parentReviewInput
                );

            const input =
                qs(
                    SELECTORS
                        .reviewInput
                );

            const ratingInput =
                qs(
                    SELECTORS
                        .ratingInput
                );

            if (editInput) {
                editInput.value =
                    "";
            }

            if (parentInput) {
                parentInput.value =
                    "";
            }

            if (input) {
                input.value =
                    "";
            }

            if (ratingInput) {
                ratingInput.value =
                    "0";
            }

            getElement(
                "interactiveStars"
            )?.classList.remove(
                "d-none"
            );

            getElement(
                "cancelReplyBtn"
            )?.classList.add(
                "d-none"
            );

            setText(
                "#replyingToIndicator",
                "ما هو تقييمك للمكان؟"
            );

            setPostButtonLabel(
                "نشر"
            );

            qsa(".star-item")
                .forEach(star => {
                    star.classList.remove(
                        "bi-star-fill",
                        "text-warning"
                    );

                    star.classList.add(
                        "bi-star",
                        "text-muted"
                    );
                });
        };

    /* ======================================================================
       18. إعجاب التقييم
       ====================================================================== */

    window.toggleInstaLike =
        async function toggleInstaLike(
            reviewId
        ) {
            try {
                const response =
                    await fetch(
                        "/Places/ToggleReviewLike",
                        {
                            method:
                                "POST",

                            credentials:
                                "same-origin",

                            headers:
                                buildHeaders(
                                    "application/x-www-form-urlencoded"
                                ),

                            body:
                                new URLSearchParams({
                                    reviewId:
                                        String(
                                            reviewId
                                        )
                                })
                        }
                    );

                const data =
                    await parseResponse(
                        response
                    );

                if (!data.success) {
                    await notify({
                        icon:
                            "info",

                        title:
                            "تنبيه",

                        text:
                            data.message ||
                            "تعذر تحديث الإعجاب."
                    });

                    return;
                }

                const icon =
                    getElement(
                        `heart-icon-${reviewId}`
                    );

                const count =
                    getElement(
                        `likes-count-${reviewId}`
                    );

                if (icon) {
                    icon.className =
                        data.isLiked
                            ? "bi bi-heart-fill text-danger"
                            : "bi bi-heart";
                }

                if (count) {
                    const likes =
                        Number(
                            data.likesCount
                        ) || 0;

                    count.textContent =
                        String(likes);

                    count.style.display =
                        likes > 0
                            ? "block"
                            : "none";
                }
            } catch (error) {
                console.error(error);

                notify({
                    icon: "error",
                    title:
                        "تعذر الاتصال",
                    text:
                        "لم نتمكن من تحديث الإعجاب الآن."
                });
            }
        };

    /* ======================================================================
       19. إضافة وتعديل التقييم والرد
       ====================================================================== */

    window.submitCustomerPost =
        async function submitCustomerPost() {
            const input =
                qs(
                    SELECTORS
                        .reviewInput
                );

            const postButton =
                qs(
                    SELECTORS
                        .postButton
                );

            const text =
                input?.value.trim() ||
                "";

            const placeId =
                Number.parseInt(
                    getValue(
                        "placeIdInput",
                        "0"
                    ),
                    10
                ) || 0;

            const rating =
                Number.parseInt(
                    getValue(
                        "userRatingValue",
                        "0"
                    ),
                    10
                ) || 0;

            const parentId =
                getValue(
                    "instaParentReviewId"
                );

            const editId =
                getValue(
                    "instaEditModeId"
                );

            if (!text) {
                await notify({
                    icon:
                        "warning",

                    title:
                        "اكتب المحتوى",

                    text:
                        "الرجاء كتابة التقييم أو الرد أولًا."
                });

                input?.focus();

                return;
            }

            if (
                !editId &&
                !parentId &&
                rating < 1
            ) {
                await notify({
                    icon:
                        "warning",

                    title:
                        "اختر التقييم",

                    text:
                        "اختر عدد النجوم قبل نشر تجربتك."
                });

                return;
            }

            if (
                !await ensureLoginBeforeReview(
                    placeId,
                    text,
                    rating,
                    parentId,
                    editId
                )
            ) {
                return;
            }

            setButtonLoading(
                postButton,
                true,
                "جارٍ النشر"
            );

            storageSet(
                "keepReviewsOpen",
                "true"
            );

            try {
                let response;

                if (editId) {
                    response =
                        await fetch(
                            "/Places/EditReview",
                            {
                                method:
                                    "POST",

                                credentials:
                                    "same-origin",

                                headers:
                                    buildHeaders(
                                        "application/x-www-form-urlencoded"
                                    ),

                                body:
                                    new URLSearchParams({
                                        reviewId:
                                            editId,

                                        newText:
                                            text
                                    })
                            }
                        );
                } else if (
                    parentId
                ) {
                    response =
                        await fetch(
                            "/Places/AddReply",
                            {
                                method:
                                    "POST",

                                credentials:
                                    "same-origin",

                                headers:
                                    buildHeaders(
                                        "application/x-www-form-urlencoded"
                                    ),

                                body:
                                    new URLSearchParams({
                                        parentReviewId:
                                            parentId,

                                        replyText:
                                            text,

                                        placeId:
                                            String(
                                                placeId
                                            )
                                    })
                            }
                        );
                } else {
                    response =
                        await fetch(
                            "/Places/AddReview",
                            {
                                method:
                                    "POST",

                                credentials:
                                    "same-origin",

                                headers:
                                    buildHeaders(
                                        "application/json"
                                    ),

                                body:
                                    JSON.stringify({
                                        Rating:
                                            rating,

                                        Comment:
                                            text,

                                        PlaceId:
                                            placeId
                                    })
                            }
                        );
                }

                const data =
                    await parseResponse(
                        response
                    );

                if (
                    !data.success &&
                    data.requiresLogin
                ) {
                    savePendingReviewDraft(
                        placeId,
                        text,
                        rating,
                        parentId,
                        editId
                    );

                    await notify({
                        icon:
                            "info",

                        title:
                            "تسجيل الدخول مطلوب",

                        text:
                            data.message ||
                            "يجب تسجيل الدخول لإكمال التقييم.",

                        confirmButtonText:
                            "موافق",

                        showCancelButton:
                            false
                    });

                    window.location.assign(
                        buildLoginUrlForReturn(
                            buildReviewResumeReturnUrl()
                        )
                    );

                    return;
                }

                if (!data.success) {
                    throw new Error(
                        data.message ||
                        "تعذر حفظ المحتوى."
                    );
                }

                window.location.reload();
            } catch (error) {
                console.error(error);

                storageRemove(
                    "keepReviewsOpen"
                );

                setButtonLoading(
                    postButton,
                    false
                );

                await notify({
                    icon: "error",
                    title:
                        "تعذر الحفظ",
                    text:
                        error.message ||
                        "حدث خطأ أثناء حفظ المحتوى."
                });
            }
        };

    /* ======================================================================
       20. حذف التقييم
       ====================================================================== */

    window.deleteInstaReview =
        async function deleteInstaReview(
            reviewId
        ) {
            const result =
                await notify({
                    title:
                        "حذف التقييم؟",

                    text:
                        "سيتم حذف هذا التقييم " +
                        "والردود التابعة له نهائيًا.",

                    icon:
                        "warning",

                    showCancelButton:
                        true,

                    confirmButtonText:
                        "نعم، احذف",

                    cancelButtonText:
                        "تراجع",

                    confirmButtonColor:
                        "#dc3545"
                });

            if (
                !result.isConfirmed
            ) {
                return;
            }

            try {
                const response =
                    await fetch(
                        "/Places/DeleteReview",
                        {
                            method:
                                "POST",

                            credentials:
                                "same-origin",

                            headers:
                                buildHeaders(
                                    "application/x-www-form-urlencoded"
                                ),

                            body:
                                new URLSearchParams({
                                    reviewId:
                                        String(
                                            reviewId
                                        )
                                })
                        }
                    );

                const data =
                    await parseResponse(
                        response
                    );

                if (!data.success) {
                    throw new Error(
                        data.message ||
                        "تعذر حذف التقييم."
                    );
                }

                const block =
                    getElement(
                        `review-block-${reviewId}`
                    );

                if (block) {
                    block.style.transition =
                        "opacity .25s ease, " +
                        "transform .25s ease";

                    block.style.opacity =
                        "0";

                    block.style.transform =
                        "translateY(-8px)";

                    window.setTimeout(
                        () => {
                            block.remove();
                        },
                        260
                    );
                }

                showToast(
                    "تم حذف التقييم."
                );
            } catch (error) {
                console.error(error);

                notify({
                    icon: "error",
                    title:
                        "تعذر الحذف",
                    text:
                        error.message ||
                        "حدث خطأ أثناء الحذف."
                });
            }
        };
    /* ======================================================================
    21. معرض صور الخدمات الإضافية
    ====================================================================== */

    const serviceGalleryState = {
        images: [],
        index: 0
    };

    function getServiceGalleryElements() {
        return {
            modal:
                getElement(
                    "serviceGalleryModal"
                ),

            title:
                getElement(
                    "serviceGalleryTitle"
                ),

            mainImage:
                getElement(
                    "serviceGalleryMainImage"
                ),

            counter:
                getElement(
                    "serviceGalleryCounter"
                ),

            thumbnails:
                getElement(
                    "serviceGalleryThumbnails"
                ),

            previous:
                getElement(
                    "serviceGalleryPrevious"
                ),

            next:
                getElement(
                    "serviceGalleryNext"
                ),

            stage:
                getElement(
                    "serviceGalleryStage"
                )
        };
    }

    function renderServiceGalleryImage(
        requestedIndex
    ) {
        const elements =
            getServiceGalleryElements();

        const total =
            serviceGalleryState
                .images.length;

        if (
            total === 0 ||
            !elements.mainImage
        ) {
            return;
        }

        const safeIndex =
            (
                (
                    requestedIndex %
                    total
                ) +
                total
            ) % total;

        serviceGalleryState.index =
            safeIndex;

        elements.mainImage
            .dataset
            .fallbackApplied =
            "false";

        elements.mainImage.src =
            serviceGalleryState
                .images[
            safeIndex
            ];

        elements.mainImage.alt =
            `${elements.title
                ?.textContent ||
            "الخدمة"
            } - صورة ${safeIndex + 1}`;

        if (elements.counter) {
            elements.counter.textContent =
                `${safeIndex + 1} / ${total}`;
        }

        const thumbnailButtons =
            qsa(
                ".service-gallery-thumbnail",
                elements.thumbnails ||
                document
            );

        thumbnailButtons.forEach(
            (button, index) => {
                const active =
                    index ===
                    safeIndex;

                button.classList.toggle(
                    "is-active",
                    active
                );

                button.setAttribute(
                    "aria-current",
                    active
                        ? "true"
                        : "false"
                );

                if (active) {
                    window.setTimeout(
                        () => {
                            button.scrollIntoView({
                                behavior:
                                    "smooth",

                                block:
                                    "nearest",

                                inline:
                                    "center"
                            });
                        },
                        30
                    );
                }
            }
        );

        const singleImage =
            total <= 1;

        [
            elements.previous,
            elements.next
        ].forEach(button => {
            if (!button) {
                return;
            }

            button.hidden =
                singleImage;

            button.disabled =
                singleImage;
        });
    }

    function moveServiceGallery(
        step
    ) {
        renderServiceGalleryImage(
            serviceGalleryState.index +
            step
        );
    }

    function initializeServiceGalleryControls() {
        const elements =
            getServiceGalleryElements();

        if (
            !elements.modal ||
            elements.modal.dataset
                .galleryInitialized ===
            "true"
        ) {
            return;
        }

        elements.modal.dataset
            .galleryInitialized =
            "true";

        elements.previous
            ?.addEventListener(
                "click",
                () => {
                    moveServiceGallery(
                        -1
                    );
                }
            );

        elements.next
            ?.addEventListener(
                "click",
                () => {
                    moveServiceGallery(
                        1
                    );
                }
            );

        if (elements.mainImage) {
            elements.mainImage
                .addEventListener(
                    "error",
                    () => {
                        if (
                            elements
                                .mainImage
                                .dataset
                                .fallbackApplied ===
                            "true"
                        ) {
                            return;
                        }

                        elements
                            .mainImage
                            .dataset
                            .fallbackApplied =
                            "true";

                        elements.mainImage.src =
                            "/images/bg1.jpg";
                    }
                );
        }

        let touchStartX = null;
        let touchStartY = null;

        elements.stage
            ?.addEventListener(
                "touchstart",
                event => {
                    const touch =
                        event
                            .changedTouches[0];

                    touchStartX =
                        touch?.clientX ??
                        null;

                    touchStartY =
                        touch?.clientY ??
                        null;
                },
                {
                    passive: true
                }
            );

        elements.stage
            ?.addEventListener(
                "touchend",
                event => {
                    if (
                        touchStartX === null ||
                        touchStartY === null ||
                        serviceGalleryState
                            .images.length < 2
                    ) {
                        return;
                    }

                    const touch =
                        event
                            .changedTouches[0];

                    const endX =
                        touch?.clientX ??
                        touchStartX;

                    const endY =
                        touch?.clientY ??
                        touchStartY;

                    const deltaX =
                        endX -
                        touchStartX;

                    const deltaY =
                        endY -
                        touchStartY;

                    touchStartX =
                        null;

                    touchStartY =
                        null;

                    if (
                        Math.abs(deltaX) <
                        45 ||
                        Math.abs(deltaX) <=
                        Math.abs(deltaY)
                    ) {
                        return;
                    }

                    moveServiceGallery(
                        deltaX < 0
                            ? 1
                            : -1
                    );
                },
                {
                    passive: true
                }
            );

        document.addEventListener(
            "keydown",
            event => {
                if (
                    !elements.modal
                        .classList
                        .contains("show") ||
                    serviceGalleryState
                        .images.length < 2
                ) {
                    return;
                }

                if (
                    event.key ===
                    "ArrowRight"
                ) {
                    event.preventDefault();

                    moveServiceGallery(
                        -1
                    );
                } else if (
                    event.key ===
                    "ArrowLeft"
                ) {
                    event.preventDefault();

                    moveServiceGallery(
                        1
                    );
                }
            }
        );

        elements.modal.addEventListener(
            "hidden.bs.modal",
            () => {
                serviceGalleryState.images =
                    [];

                serviceGalleryState.index =
                    0;

                elements.thumbnails
                    ?.replaceChildren();

                if (elements.mainImage) {
                    elements.mainImage
                        .removeAttribute(
                            "src"
                        );
                }

                if (elements.counter) {
                    elements.counter
                        .textContent =
                        "0 / 0";
                }
            }
        );
    }

    window.openServiceGallery =
        function openServiceGallery(
            event,
            sourceElement
        ) {
            event?.preventDefault();
            event?.stopPropagation();

            if (!sourceElement) {
                return;
            }

            let images = [];

            try {
                const parsed =
                    JSON.parse(
                        sourceElement
                            .dataset
                            .images ||
                        "[]"
                    );

                if (
                    Array.isArray(
                        parsed
                    )
                ) {
                    images =
                        parsed
                            .map(image =>
                                String(
                                    image || ""
                                ).trim()
                            )
                            .filter(Boolean)
                            .filter(
                                (
                                    image,
                                    index,
                                    array
                                ) =>
                                    array.indexOf(
                                        image
                                    ) ===
                                    index
                            );
                }
            } catch (error) {
                console.error(
                    "تعذر قراءة صور الخدمة:",
                    error
                );
            }

            if (!images.length) {
                const fallbackImage =
                    sourceElement
                        .querySelector(
                            "img"
                        )
                        ?.getAttribute(
                            "src"
                        );

                if (fallbackImage) {
                    images = [
                        fallbackImage
                    ];
                }
            }

            if (!images.length) {
                notify({
                    icon:
                        "info",

                    title:
                        "لا توجد صور",

                    text:
                        "لم تتم إضافة صور لهذه الخدمة بعد."
                });

                return;
            }

            initializeServiceGalleryControls();

            const elements =
                getServiceGalleryElements();

            if (
                !elements.modal ||
                !elements.mainImage ||
                !elements.thumbnails
            ) {
                notify({
                    icon:
                        "error",

                    title:
                        "تعذر فتح المعرض",

                    text:
                        "عناصر معرض الصور غير موجودة في الصفحة."
                });

                return;
            }

            const title =
                sourceElement
                    .dataset
                    .title ||
                "صور الخدمة";

            if (elements.title) {
                elements.title.textContent =
                    title;
            }

            serviceGalleryState.images =
                images;

            serviceGalleryState.index =
                0;

            elements.thumbnails
                .replaceChildren();

            images.forEach(
                (
                    imageSource,
                    index
                ) => {
                    const button =
                        document
                            .createElement(
                                "button"
                            );

                    button.type =
                        "button";

                    button.className =
                        "service-gallery-thumbnail";

                    button.setAttribute(
                        "aria-label",
                        `عرض الصورة رقم ${index + 1
                        }`
                    );

                    const image =
                        document
                            .createElement(
                                "img"
                            );

                    image.src =
                        imageSource;

                    image.alt =
                        `${title} - صورة مصغرة ${index + 1
                        }`;

                    image.loading =
                        "lazy";

                    image.addEventListener(
                        "error",
                        () => {
                            image.src =
                                "/images/bg1.jpg";
                        },
                        {
                            once: true
                        }
                    );

                    button.appendChild(
                        image
                    );

                    button.addEventListener(
                        "click",
                        () => {
                            renderServiceGalleryImage(
                                index
                            );
                        }
                    );

                    elements.thumbnails
                        .appendChild(
                            button
                        );
                }
            );

            renderServiceGalleryImage(
                0
            );

            showModalById(
                "serviceGalleryModal"
            );
        };
    /* ======================================================================
       22. تنظيف المودالات
       ====================================================================== */

    function initializeModalCleanup() {
        qsa(".modal")
            .forEach(modal => {
                modal.addEventListener(
                    "hidden.bs.modal",
                    () => {
                        window.setTimeout(
                            () => {
                                if (
                                    qsa(
                                        ".modal.show"
                                    ).length
                                ) {
                                    return;
                                }

                                qsa(
                                    ".modal-backdrop"
                                ).forEach(
                                    backdrop =>
                                        backdrop
                                            .remove()
                                );

                                document.body
                                    .classList
                                    .remove(
                                        "modal-open"
                                    );

                                document.body
                                    .style
                                    .removeProperty(
                                        "padding-right"
                                    );

                                if (
                                    !state
                                        .bodyLockedByReviews
                                ) {
                                    document.body
                                        .style
                                        .removeProperty(
                                            "overflow"
                                        );
                                }
                            },
                            220
                        );
                    }
                );
            });
    }

    /* ======================================================================
       22.1 تجربة المودالات الفخمة
       توهج يتبع المؤشر على الكمبيوتر فقط بدون Tilt حتى لا يتأثر النقر
       ====================================================================== */

    function initializeLuxuryModalExperience() {
        const modals =
            qsa(
                ".premium-place-modal, .service-gallery-modal"
            );

        if (!modals.length) {
            return;
        }

        const canUsePointerGlow =
            window.matchMedia(
                "(hover: hover) and (pointer: fine) and (min-width: 768px)"
            ).matches;

        modals.forEach(modal => {
            if (
                modal.dataset
                    .luxuryModalInitialized ===
                "true"
            ) {
                return;
            }

            modal.dataset
                .luxuryModalInitialized =
                "true";

            const content =
                modal.querySelector(
                    ".premium-place-modal-content, .premium-service-gallery-content"
                );

            const resetGlow = () => {
                if (!content) {
                    return;
                }

                content.style.setProperty(
                    "--modal-glow-x",
                    "74%"
                );

                content.style.setProperty(
                    "--modal-glow-y",
                    "16%"
                );
            };

            if (
                canUsePointerGlow &&
                content
            ) {
                let frameId = 0;

                content.addEventListener(
                    "pointermove",
                    event => {
                        if (
                            event.pointerType &&
                            event.pointerType !==
                            "mouse"
                        ) {
                            return;
                        }

                        if (frameId) {
                            window.cancelAnimationFrame(
                                frameId
                            );
                        }

                        frameId =
                            window.requestAnimationFrame(
                                () => {
                                    const rect =
                                        content
                                            .getBoundingClientRect();

                                    if (
                                        rect.width <= 0 ||
                                        rect.height <= 0
                                    ) {
                                        return;
                                    }

                                    const x =
                                        Math.max(
                                            0,
                                            Math.min(
                                                100,
                                                (
                                                    (
                                                        event.clientX -
                                                        rect.left
                                                    ) /
                                                    rect.width
                                                ) *
                                                100
                                            )
                                        );

                                    const y =
                                        Math.max(
                                            0,
                                            Math.min(
                                                100,
                                                (
                                                    (
                                                        event.clientY -
                                                        rect.top
                                                    ) /
                                                    rect.height
                                                ) *
                                                100
                                            )
                                        );

                                    content
                                        .style
                                        .setProperty(
                                            "--modal-glow-x",
                                            `${x.toFixed(1)}%`
                                        );

                                    content
                                        .style
                                        .setProperty(
                                            "--modal-glow-y",
                                            `${y.toFixed(1)}%`
                                        );

                                    frameId = 0;
                                }
                            );
                    },
                    {
                        passive: true
                    }
                );

                content.addEventListener(
                    "pointerleave",
                    resetGlow,
                    {
                        passive: true
                    }
                );
            }

            modal.addEventListener(
                "show.bs.modal",
                () => {
                    resetGlow();

                    modal.classList.add(
                        "luxury-modal-entering"
                    );
                }
            );

            modal.addEventListener(
                "shown.bs.modal",
                () => {
                    modal.classList.remove(
                        "luxury-modal-entering"
                    );

                    modal.classList.add(
                        "luxury-modal-ready"
                    );

                    const firstSelectedDay =
                        modal.querySelector(
                            ".flatpickr-day.selected"
                        );

                    if (firstSelectedDay) {
                        firstSelectedDay
                            .classList.add(
                                "luxury-calendar-selected"
                            );
                    }
                }
            );

            modal.addEventListener(
                "hidden.bs.modal",
                () => {
                    modal.classList.remove(
                        "luxury-modal-entering",
                        "luxury-modal-ready"
                    );

                    resetGlow();
                }
            );
        });
    }

    /* ======================================================================
       22.1 نوع الحجز — خيار أخرى
       ====================================================================== */

    function initializeCustomBookingType() {
        const select =
            getElement(
                "bookingTypeSelect"
            );

        const wrap =
            getElement(
                "bookingTypeOtherWrap"
            );

        const input =
            getElement(
                "bookingTypeOtherInput"
            );

        const counter =
            getElement(
                "bookingTypeOtherCounter"
            );

        if (
            !select ||
            !wrap ||
            !input
        ) {
            return;
        }

        const updateCounter = () => {
            if (!counter) {
                return;
            }

            counter.textContent =
                `${input.value.length} / 50`;
        };

        const sync = (focusInput = false) => {
            const show =
                select.value ===
                "other";

            wrap.hidden =
                !show;

            wrap.setAttribute(
                "aria-hidden",
                show
                    ? "false"
                    : "true"
            );

            wrap.classList.toggle(
                "is-visible",
                show
            );

            input.required =
                show;

            if (show) {
                window.requestAnimationFrame(
                    () => {
                        wrap.classList.add(
                            "is-ready"
                        );
                    }
                );

                if (focusInput) {
                    window.setTimeout(
                        () => {
                            input.focus({
                                preventScroll: true
                            });
                        },
                        120
                    );
                }
            } else {
                wrap.classList.remove(
                    "is-ready"
                );

                input.classList.remove(
                    "is-invalid"
                );
            }

            updateCounter();
        };

        select.addEventListener(
            "change",
            () => sync(true)
        );

        input.addEventListener(
            "input",
            () => {
                input.classList.remove(
                    "is-invalid"
                );

                updateCounter();
            }
        );

        sync(false);
    }

    function resolveBookingTypeValue() {
        const selected =
            getValue(
                "bookingTypeSelect",
                "family"
            );

        if (selected !== "other") {
            return {
                valid: true,
                value: selected
            };
        }

        const input =
            getElement(
                "bookingTypeOtherInput"
            );

        const customValue =
            String(
                input?.value ||
                ""
            )
                .replace(/\s+/g, " ")
                .trim();

        if (customValue.length < 2) {
            if (input) {
                input.classList.add(
                    "is-invalid"
                );

                input.focus({
                    preventScroll: true
                });
            }

            return {
                valid: false,
                value: "",
                message:
                    "اكتب نوع الحجز في خيار «أخرى» قبل المتابعة."
            };
        }

        if (customValue.length > 50) {
            return {
                valid: false,
                value: "",
                message:
                    "نوع الحجز المخصص يجب ألا يتجاوز 50 حرفًا."
            };
        }

        return {
            valid: true,
            value: customValue
        };
    }

    /* ======================================================================
       23. اختصارات لوحة المفاتيح
       ====================================================================== */

    function initializeKeyboardShortcuts() {
        document.addEventListener(
            "keydown",
            event => {
                const activeTag =
                    document
                        .activeElement
                        ?.tagName
                        ?.toLowerCase();

                const typing =
                    activeTag ===
                    "input" ||
                    activeTag ===
                    "textarea" ||
                    activeTag ===
                    "select";

                if (
                    event.key ===
                    "Escape" &&
                    state.reviewsOpen &&
                    !qs(".modal.show")
                ) {
                    event.preventDefault();

                    setReviewsOpen(
                        false
                    );

                    return;
                }

                if (!typing) {
                    const section =
                        getElement(
                            "extendedGallerySection"
                        );

                    const open =
                        section &&
                        window
                            .getComputedStyle(
                                section
                            )
                            .display !==
                        "none";

                    if (
                        open &&
                        (
                            event.key ===
                            "ArrowRight" ||
                            event.key ===
                            "ArrowLeft"
                        )
                    ) {
                        event.preventDefault();

                        showGalleryIndex(
                            state
                                .selectedGalleryIndex +
                            (
                                event.key ===
                                    "ArrowLeft"
                                    ? 1
                                    : -1
                            )
                        );
                    }
                }

                if (
                    typing &&
                    event.ctrlKey &&
                    event.key ===
                    "Enter" &&
                    state.reviewsOpen
                ) {
                    event.preventDefault();

                    window
                        .submitCustomerPost();
                }
            }
        );
    }

})();