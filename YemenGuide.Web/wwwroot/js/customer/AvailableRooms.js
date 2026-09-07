/**
 * ============================================================================
 * YEMENGUIDE — AVAILABLE ROOMS
 * صفحة الغرف المتاحة
 * متوافق مع AvailableRoomsViewModel والواجهة الفخمة المصغرة
 * ============================================================================
 */

(() => {
    "use strict";

    const state = {
        currentRoomId: 0,
        currentRoomName: "",
        currentRoomStayPrice: 0,
        currentRoomOriginalStayPrice: 0,
        currentPromotionId: 0,
        currentPromotionTitle: "",
        currentPromotionDiscount: "",
        currentPromotionOfferMode: "Discount",
        currentPromotionRewardType: "",
        currentPromotionRequiredQuantity: 0,
        currentPromotionRewardQuantity: 0,
        currentPromotionIsRewardRepeatable: false,
        currentPromotionAppliedFreeQuantity: 0,
        currentPromotionSpecialPrice: 0,
        currentPromotionSummary: "",
        currentPromotionActive: false,
        currentPromotionMinimumPeople: 0,
        currentPromotionCodeRequired: false,
        currentPromotionDiscountType: "",
        currentPromotionDiscountValue: 0,
        currentPromotionMaximumDiscount: 0,
        currentPromotionMinimumBooking: 0,
        currentPromotionMinimumBookingText: "",
        currentCountInputId: "",
        currentUnitCapacity: 0,
        selectedRoomCount: 1,
        maximumGuests: 0,
        lastTriggerButton: null,

        galleryImages: [],
        galleryIndex: 0,
        galleryTitle: "صور الغرفة",
        galleryTouchStartX: 0,
        galleryTouchStartY: 0,
        galleryPointerStartX: 0,
        galleryPointerStartY: 0,
        galleryPointerActive: false,
        galleryPointerId: null,

        unitCounts: new Map(),
        lastValidAdults: 0,
        lastValidChildren: 0
    };

    const currencyRoot =
        document.querySelector(".available-rooms-page");

    const displayCurrencyCode =
        currencyRoot?.dataset?.currencyCode || "YER";

    /* ======================================================================
       01. أدوات عامة
       ====================================================================== */

    const getElement = id =>
        document.getElementById(id);

    const getElements = selector =>
        Array.from(document.querySelectorAll(selector));

    function normalizeDigits(value) {
        const arabicDigits = "٠١٢٣٤٥٦٧٨٩";
        const persianDigits = "۰۱۲۳۴۵۶۷۸۹";

        return String(value ?? "")
            .replace(/[٠-٩]/g, digit =>
                String(arabicDigits.indexOf(digit)))
            .replace(/[۰-۹]/g, digit =>
                String(persianDigits.indexOf(digit)));
    }

    function toNumber(value, fallback = 0) {
        let text = normalizeDigits(value)
            .trim()
            .replace(/\s/g, "")
            .replace(/٬/g, ",")
            .replace(/٫/g, ".")
            .replace(/[^0-9,.-]/g, "");

        if (!text) {
            return fallback;
        }

        const lastComma = text.lastIndexOf(",");
        const lastDot = text.lastIndexOf(".");

        if (lastComma >= 0 && lastDot >= 0) {
            if (lastComma > lastDot) {
                text = text
                    .replace(/\./g, "")
                    .replace(",", ".");
            } else {
                text = text.replace(/,/g, "");
            }
        } else if (lastComma >= 0) {
            const parts = text.split(",");

            if (
                parts.length === 2 &&
                parts[1].length > 0 &&
                parts[1].length <= 2
            ) {
                text = `${parts[0]}.${parts[1]}`;
            } else {
                text = text.replace(/,/g, "");
            }
        }

        const parsed = Number.parseFloat(text);

        return Number.isFinite(parsed)
            ? parsed
            : fallback;
    }

    function toInteger(value, fallback = 0) {
        const parsed = Number.parseInt(
            normalizeDigits(value),
            10
        );

        return Number.isFinite(parsed)
            ? parsed
            : fallback;
    }

    function clamp(value, minimum, maximum) {
        return Math.min(
            Math.max(value, minimum),
            maximum
        );
    }

    function calculateEquivalentGuests(
        adults,
        children
    ) {
        const safeAdults = Math.max(
            0,
            toInteger(adults, 0)
        );

        const safeChildren = Math.max(
            0,
            toInteger(children, 0)
        );

        /*
         * قاعدة الفندق:
         * - أول أربعة أطفال فقط يمكنهم الاستفادة من الاستبدال.
         * - كل طفلين من أول أربعة يستهلكان مكان بالغ واحد.
         * - أي مجموعة غير مكتملة (طفل واحد أو ثلاثة) تحجز المقعد التالي كاملًا.
         * - من الطفل الخامس فصاعدًا يُحتسب كل طفل كشخص كامل.
         */
        const convertibleChildren =
            Math.min(
                safeChildren,
                4
            );

        const convertedSlots =
            Math.ceil(
                convertibleChildren /
                2
            );

        const extraChildren =
            Math.max(
                0,
                safeChildren - 4
            );

        return safeAdults +
            convertedSlots +
            extraChildren;
    }

    function hasRequiredAdult(adults) {
        return toInteger(adults, 0) >= 1;
    }

    function formatMoney(value) {
        const normalized =
            Math.max(0, toNumber(value, 0));

        return normalized.toLocaleString(
            "en-US",
            {
                minimumFractionDigits:
                    displayCurrencyCode === "YER" ? 0 : 2,
                maximumFractionDigits:
                    displayCurrencyCode === "YER" ? 0 : 2
            }
        );
    }

    function getStayNights() {
        const start = getHiddenValue("startDateInput");
        const end = getHiddenValue("endDateInput");

        if (!start || !end) return 1;

        const startDate = new Date(`${start}T00:00:00`);
        const endDate = new Date(`${end}T00:00:00`);
        const diff = Math.round((endDate - startDate) / 86400000);
        return Math.max(1, Number.isFinite(diff) ? diff : 1);
    }

    function calculatePromotionTotal(baseTotal, rules, selectedCount = 1) {
        const original = Math.max(0, toNumber(baseTotal, 0));
        const promotionId = Math.max(0, toInteger(rules?.promotionId, 0));
        const minimumBooking = Math.max(0, toNumber(rules?.minimumBooking, 0));

        if (
            promotionId <= 0 ||
            original <= 0 ||
            (minimumBooking > 0 && original < minimumBooking)
        ) {
            return {
                original,
                discount: 0,
                final: original,
                applies: false,
                minimumPending: minimumBooking > 0 && original < minimumBooking
            };
        }

        const offerMode = String(rules?.offerMode || "Discount").toLowerCase();
        const rewardType = String(rules?.rewardType || "").toLowerCase();
        const type = String(rules?.type || "").toLowerCase();
        const value = Math.max(0, toNumber(rules?.value, 0));
        const maximumDiscount = Math.max(0, toNumber(rules?.maximumDiscount, 0));
        const requiredQuantity = Math.max(1, toInteger(rules?.requiredQuantity, 1));
        const rewardQuantity = Math.max(1, toInteger(rules?.rewardQuantity, 1));
        const specialPrice = Math.max(0, toNumber(rules?.specialPrice, 0));

        let discount = 0;

        if (offerMode !== "conditionalreward") {
            discount = type === "percentage"
                ? original * value / 100
                : value;

            if (maximumDiscount > 0) {
                discount = Math.min(discount, maximumDiscount);
            }
        } else if (rewardType === "percentagediscount") {
            discount = original * value / 100;
            if (maximumDiscount > 0) {
                discount = Math.min(discount, maximumDiscount);
            }
        } else if (rewardType === "fixeddiscount") {
            discount = value;
            if (maximumDiscount > 0) {
                discount = Math.min(discount, maximumDiscount);
            }
        } else if (rewardType === "specialprice" && specialPrice > 0) {
            const nights = getStayNights();
            const quantity = Math.max(1, toInteger(selectedCount, 1));
            const specialTotal = specialPrice * nights * quantity;
            discount = Math.max(0, original - specialTotal);
        } else if (rewardType === "freequantity") {
            const nights = getStayNights();
            const isRepeatable = rules?.isRewardRepeatable === true ||
                String(rules?.isRewardRepeatable || "").toLowerCase() === "true";
            const completedRequirements = Math.floor(nights / requiredQuantity);
            const repeatCount = isRepeatable
                ? completedRequirements
                : completedRequirements > 0 ? 1 : 0;
            const freeNights = Math.min(
                nights,
                Math.max(0, repeatCount * rewardQuantity)
            );

            if (freeNights > 0 && nights > 0) {
                const amountPerNight = original / nights;
                discount = amountPerNight * freeNights;
            }
        } else {
            // FreeService / ServiceSpecialPrice لا يغيران سعر الغرفة هنا.
            // Checkout هو المرجع النهائي للحساب وتثبيت المكافأة.
            discount = 0;
        }

        discount = Math.min(original, Math.max(0, discount));

        return {
            original,
            discount,
            final: Math.max(0, original - discount),
            applies: discount > 0 || rewardType === "freeservice",
            minimumPending: false
        };
    }

    function getRoomPromotionRules(card) {
        if (!card) {
            return null;
        }

        return {
            promotionId: card.dataset.promotionId || "0",
            offerMode: card.dataset.promotionOfferMode || "Discount",
            rewardType: card.dataset.promotionRewardType || "",
            requiredQuantity: card.dataset.promotionRequiredQuantity || "0",
            rewardQuantity: card.dataset.promotionRewardQuantity || "0",
            isRewardRepeatable: card.dataset.promotionRepeatable === "true",
            specialPrice: card.dataset.promotionSpecialPrice || "0",
            summary: card.dataset.promotionSummary || "",
            type: card.dataset.promotionType || "",
            value: card.dataset.promotionValue || "0",
            maximumDiscount: card.dataset.promotionMax || "0",
            minimumBooking: card.dataset.promotionMinBooking || "0"
        };
    }

    function getHiddenValue(id, fallback = "") {
        const element = getElement(id);

        return element
            ? String(element.value ?? "").trim()
            : fallback;
    }


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

    function notify(options) {
        if (
            typeof window.Swal !== "undefined" &&
            typeof window.Swal.fire === "function"
        ) {
            return window.Swal.fire(options);
        }

        return fallbackBookingDialog(options);
    }

    async function ensureBookingProfileReady() {
        try {
            const returnUrl =
                window.location.pathname +
                window.location.search +
                window.location.hash;

            const response = await fetch(
                `/BookingFlow/BookingReadiness?returnUrl=${encodeURIComponent(returnUrl)}`,
                {
                    method: "GET",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    credentials: "same-origin"
                }
            );

            let data = {};
            try {
                data = await response.json();
            } catch {
                data = {};
            }

            if (response.ok && data?.ready === true) {
                return true;
            }

            const missing =
                Array.isArray(data?.missingFields)
                    ? data.missingFields.filter(Boolean)
                    : [];

            const result = await notify({
                icon: "warning",
                title: data?.requiresLogin
                    ? "سجل الدخول لإكمال الحجز"
                    : "أكمل بيانات حسابك أولًا",
                text: missing.length
                    ? `البيانات المطلوبة: ${missing.join("، ")}.`
                    : (data?.message || "يجب إكمال بيانات الحساب قبل الحجز."),
                showCancelButton: true,
                confirmButtonText: data?.requiresLogin
                    ? "تسجيل الدخول"
                    : "الذهاب إلى الملف الشخصي",
                cancelButtonText: "إلغاء"
            });

            if (result?.isConfirmed) {
                if (data?.requiresLogin) {
                    window.location.assign(
                        `/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`
                    );
                } else {
                    window.location.assign(
                        data?.redirectUrl || "/Profile?completeBookingProfile=true"
                    );
                }
            }

            return false;
        } catch (error) {
            console.error("Booking readiness error:", error);

            await notify({
                icon: "error",
                title: "تعذر التحقق من الحساب",
                text: "تعذر التحقق من جاهزية بيانات حسابك. أعد المحاولة بعد قليل.",
                confirmButtonText: "حسنًا"
            });

            return false;
        }
    }

    function initializeBookingTypeOther() {
        const select = getElement("bookingType");
        const wrap = getElement("bookingTypeOtherWrap");
        const input = getElement("bookingTypeOtherInput");

        if (!select || !wrap || !input) return;

        const sync = focus => {
            const show = select.value === "other";
            wrap.classList.toggle("d-none", !show);
            input.required = show;

            if (show && focus) {
                setTimeout(() => input.focus(), 70);
            } else if (!show) {
                input.classList.remove("is-invalid");
            }
        };

        select.addEventListener("change", () => sync(true));
        input.addEventListener("input", () => input.classList.remove("is-invalid"));
        sync(false);
    }

    function resolveBookingTypeValue() {
        const select = getElement("bookingType");
        const selected = String(select?.value || "family").trim();

        if (selected !== "other") {
            return { valid: true, value: selected };
        }

        const input = getElement("bookingTypeOtherInput");
        const value = String(input?.value || "")
            .replace(/\s+/g, " ")
            .trim();

        if (value.length < 2) {
            input?.classList.add("is-invalid");
            input?.focus();

            return {
                valid: false,
                value: "",
                message: "اكتب نوع الحجز عند اختيار «أخرى»."
            };
        }

        return { valid: true, value };
    }

    function showError(title, text) {
        return notify({
            icon: "error",
            title,
            text,
            confirmButtonText: "حسنًا",
            confirmButtonColor: "#12b8d4"
        });
    }

    function showWarning(title, text) {
        return notify({
            icon: "warning",
            title,
            text,
            confirmButtonText: "حسنًا",
            confirmButtonColor: "#12b8d4"
        });
    }

    function getBootstrapModal(element) {
        if (
            !element ||
            typeof window.bootstrap === "undefined" ||
            !window.bootstrap.Modal
        ) {
            return null;
        }

        return window.bootstrap.Modal.getOrCreateInstance(
            element,
            {
                backdrop: true,
                keyboard: true,
                focus: true
            }
        );
    }

    function getBootstrapCollapse(element) {
        if (
            !element ||
            typeof window.bootstrap === "undefined" ||
            !window.bootstrap.Collapse
        ) {
            return null;
        }

        return window.bootstrap.Collapse.getOrCreateInstance(
            element,
            {
                toggle: false
            }
        );
    }

    function storeUiFlag(key, value) {
        try {
            sessionStorage.setItem(key, String(value));
        } catch {
            // هذا التخزين لحالة الواجهة فقط، وتجاهل فشله آمن.
        }
    }

    function cleanOrphanModalState() {
        window.setTimeout(() => {
            const hasOpenModal =
                Boolean(document.querySelector(".modal.show"));

            if (hasOpenModal) {
                return;
            }

            document
                .querySelectorAll(".modal-backdrop")
                .forEach(backdrop => backdrop.remove());

            document.body.classList.remove("modal-open");
            document.body.style.removeProperty("overflow");
            document.body.style.removeProperty("padding-right");
        }, 180);
    }

    /* ======================================================================
       02. التهيئة الرئيسية
       ====================================================================== */

    document.addEventListener("DOMContentLoaded", () => {
        initializeBackToDatesButtons();
        initializeCollapsePanels();
        initializeUnitInputs();
        initializeGuestModal();
        initializeRoomGallery();
        initializeLuxuryRoomExperience();
        initializeBookingTypeOther();
        initializePromotionCodeCopy();
        initializeModalCleanup();
        initializeKeyboardShortcuts();
    });

    /* ======================================================================
       03. العودة إلى صفحة التفاصيل لتعديل التواريخ
       ====================================================================== */

    function initializeBackToDatesButtons() {
        getElements('[id="btnBackToDates"]').forEach(button => {
            if (button.dataset.initialized === "true") {
                return;
            }

            button.dataset.initialized = "true";

            button.addEventListener("click", event => {
                event.preventDefault();

                /*
                 * هذه ليست بيانات حجز.
                 * هي مجرد إشارة لصفحة التفاصيل لإعادة فتح نافذة التاريخ.
                 */
                storeUiFlag("openDateModal", "true");

                if (window.history.length > 1) {
                    window.history.back();
                    return;
                }

                const placeId = toInteger(
                    getHiddenValue("placeIdInput", "0"),
                    0
                );

                const promotionId = toInteger(
                    getHiddenValue("selectedPromotionIdInput", "0"),
                    0
                );

                const promotionQuery = promotionId > 0
                    ? `?promotionId=${encodeURIComponent(promotionId)}`
                    : "";

                window.location.href = placeId > 0
                    ? `/Places/Details/${encodeURIComponent(placeId)}${promotionQuery}`
                    : "/Places";
            });
        });
    }

    /* ======================================================================
       04. فتح وإغلاق تفاصيل الغرفة
       ====================================================================== */

    function initializeCollapsePanels() {
        getElements(".room-details-collapse").forEach(collapseElement => {
            if (collapseElement.dataset.initialized === "true") {
                return;
            }

            collapseElement.dataset.initialized = "true";

            collapseElement.addEventListener("show.bs.collapse", () => {
                closeOtherRoomDetails(collapseElement);
                updateCollapseButton(collapseElement.id, true);
            });

            collapseElement.addEventListener("shown.bs.collapse", () => {
                const card = collapseElement.closest("[data-room-card]");

                window.setTimeout(() => {
                    card?.scrollIntoView({
                        behavior: "smooth",
                        block: "nearest"
                    });
                }, 80);
            });

            collapseElement.addEventListener("hide.bs.collapse", () => {
                updateCollapseButton(collapseElement.id, false);
            });
        });
    }

    function closeOtherRoomDetails(currentCollapse) {
        getElements(".room-details-collapse.show").forEach(otherCollapse => {
            if (otherCollapse === currentCollapse) {
                return;
            }

            const instance = getBootstrapCollapse(otherCollapse);

            if (instance) {
                instance.hide();
            } else {
                otherCollapse.classList.remove("show");
                updateCollapseButton(otherCollapse.id, false);
            }
        });
    }

    function updateCollapseButton(collapseId, expanded) {
        const button = document.querySelector(
            `[data-bs-target="#${collapseId}"]`
        );

        if (!button) {
            return;
        }

        button.setAttribute(
            "aria-expanded",
            expanded ? "true" : "false"
        );

        const label = button.querySelector("span");
        const icon = button.querySelector("i");

        if (label) {
            label.textContent = expanded
                ? "إخفاء التفاصيل"
                : "عرض التفاصيل والحجز";
        }

        if (icon) {
            icon.classList.toggle("bi-chevron-up", expanded);
            icon.classList.toggle("bi-chevron-down", !expanded);
        }
    }

    /* ======================================================================
       05. عدد الوحدات والإجمالي
       ====================================================================== */

    function initializeUnitInputs() {
        getElements(".room-quantity-input").forEach(input => {
            const value = Math.max(
                1,
                toInteger(input.value, 1)
            );

            input.value = String(value);
            state.unitCounts.set(input.id, value);
        });
    }

    window.updateUnitCount = function updateUnitCount(
        inputId,
        change,
        stayPricePerUnit,
        totalId,
        maxQuantity
    ) {
        const input = getElement(inputId);

        if (!input) {
            return;
        }

        const maximum = Math.max(
            1,
            toInteger(maxQuantity, 1)
        );

        const currentValue = Math.max(
            1,
            toInteger(input.value, 1)
        );

        input.value = String(
            clamp(
                currentValue + toInteger(change, 0),
                1,
                maximum
            )
        );

        window.syncUnitCount(
            inputId,
            stayPricePerUnit,
            totalId,
            maximum
        );
    };

    window.syncUnitCount = function syncUnitCount(
        inputId,
        stayPricePerUnit,
        totalId,
        maxQuantity
    ) {
        const input = getElement(inputId);
        const totalElement = getElement(totalId);

        if (!input || !totalElement) {
            return;
        }

        if (input.value === "") {
            return;
        }

        const maximum = Math.max(
            1,
            toInteger(maxQuantity, 1)
        );

        let value = toInteger(input.value, 1);

        if (value < 1) {
            value = 1;
        }

        if (value > maximum) {
            value = maximum;

            showWarning(
                "تجاوز العدد المتاح",
                `أقصى عدد متاح من هذه الوحدة هو ${maximum}.`
            );
        }

        input.value = String(value);
        state.unitCounts.set(inputId, value);

        const unitStayPrice = Math.max(
            0,
            toNumber(stayPricePerUnit, 0)
        );

        const baseTotal = unitStayPrice * value;
        const roomCard = input.closest("[data-room-card]");
        const pricing = calculatePromotionTotal(
            baseTotal,
            getRoomPromotionRules(roomCard)
        );

        totalElement.textContent = formatMoney(
            pricing.final
        );

        roomCard?.classList.toggle(
            "promotion-minimum-met",
            pricing.applies
        );

        if (inputId === state.currentCountInputId) {
            state.selectedRoomCount = value;
            recalculateMaximumGuests();
            updateSelectedRoomPreview();
            resetGuestCounters();
        }
    };

    /* ======================================================================
       06. اختيار الغرفة ونافذة الأشخاص
       ====================================================================== */

    function initializeGuestModal() {
        const modalElement = getElement("guestDetailsModal");
        const roomButtons = getElements(".open-guest-modal-btn");

        roomButtons.forEach(button => {
            if (button.dataset.initialized === "true") {
                return;
            }

            button.dataset.initialized = "true";

            button.addEventListener("click", () => {
                state.lastTriggerButton = button;
                selectRoomCard(
                    button.getAttribute("data-room-id")
                );
            });
        });

        if (!modalElement) {
            return;
        }

        modalElement.addEventListener("show.bs.modal", event => {
            const button =
                event.relatedTarget ||
                state.lastTriggerButton;

            prepareSelectedRoom(button);
        });

        modalElement.addEventListener("shown.bs.modal", () => {
            getElement("bookingType")?.focus({
                preventScroll: true
            });
        });

        modalElement.addEventListener("hidden.bs.modal", () => {
            cleanOrphanModalState();
        });
    }

    function prepareSelectedRoom(button) {
        if (!button) {
            showError(
                "تعذر اختيار الغرفة",
                "لم نتمكن من قراءة بيانات الوحدة المختارة."
            );

            return;
        }

        const roomId = Math.max(
            0,
            toInteger(
                button.getAttribute("data-room-id"),
                0
            )
        );

        if (roomId <= 0) {
            showError(
                "بيانات غير صحيحة",
                "رقم الوحدة المختارة غير صحيح."
            );

            return;
        }

        state.lastTriggerButton = button;
        state.currentRoomId = roomId;
        state.currentRoomName =
            button.getAttribute("data-room-name") ||
            "وحدة مختارة";

        /*
         * هذا السعر هو سعر وحدة واحدة لكامل الفترة.
         * لا يُرسل إلى الخادم؛ يستخدم للعرض داخل الصفحة فقط.
         */
        state.currentRoomStayPrice = Math.max(
            0,
            toNumber(
                button.getAttribute("data-room-price"),
                0
            )
        );

        state.currentRoomOriginalStayPrice = Math.max(
            state.currentRoomStayPrice,
            toNumber(
                button.getAttribute("data-room-original-price"),
                state.currentRoomStayPrice
            )
        );

        state.currentPromotionId = Math.max(
            0,
            toInteger(
                button.getAttribute("data-promotion-id"),
                0
            )
        );

        state.currentPromotionTitle =
            button.getAttribute("data-promotion-title") ||
            "عرض خاص";

        state.currentPromotionDiscount =
            button.getAttribute("data-promotion-discount") ||
            "";

        state.currentPromotionOfferMode =
            button.getAttribute("data-promotion-offer-mode") ||
            "Discount";

        state.currentPromotionRewardType =
            button.getAttribute("data-promotion-reward-type") ||
            "";

        state.currentPromotionRequiredQuantity = Math.max(
            0,
            toInteger(button.getAttribute("data-promotion-required-quantity"), 0)
        );

        state.currentPromotionRewardQuantity = Math.max(
            0,
            toInteger(button.getAttribute("data-promotion-reward-quantity"), 0)
        );

        state.currentPromotionIsRewardRepeatable =
            button.getAttribute("data-promotion-repeatable") === "true";

        state.currentPromotionAppliedFreeQuantity = Math.max(
            0,
            toInteger(button.getAttribute("data-promotion-applied-free"), 0)
        );

        state.currentPromotionSpecialPrice = Math.max(
            0,
            toNumber(button.getAttribute("data-promotion-special-price"), 0)
        );

        state.currentPromotionSummary =
            button.getAttribute("data-promotion-summary") ||
            state.currentPromotionDiscount ||
            "";

        state.currentPromotionActive =
            button.getAttribute("data-promotion-active") === "true";

        state.currentPromotionMinimumPeople = Math.max(
            0,
            toInteger(
                button.getAttribute("data-promotion-min-people"),
                0
            )
        );

        state.currentPromotionCodeRequired =
            button.getAttribute("data-promotion-code-required") === "true";

        state.currentPromotionDiscountType =
            button.getAttribute("data-promotion-type") || "";

        state.currentPromotionDiscountValue = Math.max(
            0,
            toNumber(button.getAttribute("data-promotion-value"), 0)
        );

        state.currentPromotionMaximumDiscount = Math.max(
            0,
            toNumber(button.getAttribute("data-promotion-max"), 0)
        );

        state.currentPromotionMinimumBooking = Math.max(
            0,
            toNumber(button.getAttribute("data-promotion-min-booking"), 0)
        );

        state.currentPromotionMinimumBookingText =
            button.getAttribute("data-promotion-min-booking-text") ||
            "";

        state.currentCountInputId = `count_${roomId}`;

        const countInput = getElement(
            state.currentCountInputId
        );

        state.selectedRoomCount = Math.max(
            1,
            toInteger(
                countInput?.value,
                1
            )
        );

        /*
         * السعة تأتي من DTO فقط.
         * عندما تكون 0 فهذا يعني أن المالك لم يسجلها،
         * لذلك لا نخترع حدًا وهميًا في JavaScript.
         */
        state.currentUnitCapacity = Math.max(
            0,
            toInteger(
                button.getAttribute("data-capacity"),
                0
            )
        );

        recalculateMaximumGuests();
        selectRoomCard(roomId);
        updateSelectedRoomPreview();
        resetGuestCounters();
    }

    function selectRoomCard(roomId) {
        getElements("[data-room-card]").forEach(card => {
            const selected =
                card.getAttribute("data-room-id") ===
                String(roomId);

            card.classList.toggle("is-selected", selected);
            card.setAttribute(
                "aria-selected",
                selected ? "true" : "false"
            );
        });
    }

    function recalculateMaximumGuests() {
        if (state.currentUnitCapacity <= 0) {
            state.maximumGuests = 0;
            return;
        }

        state.maximumGuests =
            state.currentUnitCapacity *
            Math.max(1, state.selectedRoomCount);
    }

    function updateSelectedRoomPreview() {
        const nameElement = getElement(
            "guestSelectedRoomName"
        );

        const priceElement = getElement(
            "guestSelectedRoomPrice"
        );

        if (nameElement) {
            nameElement.textContent =
                state.currentRoomName ||
                "وحدة مختارة";
        }

        if (priceElement) {
            const baseTotal =
                state.currentRoomStayPrice *
                Math.max(1, state.selectedRoomCount);

            const pricing = calculatePromotionTotal(
                baseTotal,
                {
                    promotionId: state.currentPromotionId,
                    offerMode: state.currentPromotionOfferMode,
                    rewardType: state.currentPromotionRewardType,
                    requiredQuantity: state.currentPromotionRequiredQuantity,
                    rewardQuantity: state.currentPromotionRewardQuantity,
                    isRewardRepeatable: state.currentPromotionIsRewardRepeatable,
                    specialPrice: state.currentPromotionSpecialPrice,
                    type: state.currentPromotionDiscountType,
                    value: state.currentPromotionDiscountValue,
                    maximumDiscount: state.currentPromotionMaximumDiscount,
                    minimumBooking: state.currentPromotionMinimumBooking
                },
                state.selectedRoomCount
            );

            priceElement.textContent = formatMoney(
                pricing.final
            );
        }

        updateGuestPromotionPreview();
    }

    function updateGuestPromotionPreview() {
        const preview = getElement("guestPromotionPreview");
        const title = getElement("guestPromotionTitle");
        const note = getElement("guestPromotionNote");
        const discount = getElement("guestPromotionDiscount");

        if (!preview) {
            return;
        }

        const show =
            state.currentPromotionId > 0 &&
            state.currentPromotionActive;

        preview.classList.toggle("d-none", !show);

        if (!show) {
            return;
        }

        if (title) {
            title.textContent =
                state.currentPromotionTitle ||
                "عرض خاص";
        }

        if (discount) {
            discount.textContent =
                state.currentPromotionSummary ||
                state.currentPromotionDiscount ||
                "عرض";
        }

        if (note) {
            const notes = [];
            const selectedBaseTotal =
                state.currentRoomStayPrice *
                Math.max(1, state.selectedRoomCount);
            const pricing = calculatePromotionTotal(
                selectedBaseTotal,
                {
                    promotionId: state.currentPromotionId,
                    offerMode: state.currentPromotionOfferMode,
                    rewardType: state.currentPromotionRewardType,
                    requiredQuantity: state.currentPromotionRequiredQuantity,
                    rewardQuantity: state.currentPromotionRewardQuantity,
                    isRewardRepeatable: state.currentPromotionIsRewardRepeatable,
                    specialPrice: state.currentPromotionSpecialPrice,
                    type: state.currentPromotionDiscountType,
                    value: state.currentPromotionDiscountValue,
                    maximumDiscount: state.currentPromotionMaximumDiscount,
                    minimumBooking: state.currentPromotionMinimumBooking
                },
                state.selectedRoomCount
            );

            if (pricing.minimumPending) {
                notes.push(
                    `الحد الأدنى للحجز ${state.currentPromotionMinimumBookingText ||
                    formatMoney(state.currentPromotionMinimumBooking)
                    }`
                );
            }

            const rewardType = String(state.currentPromotionRewardType || "").toLowerCase();
            if (rewardType === "freequantity") {
                const required = Math.max(1, state.currentPromotionRequiredQuantity || 1);
                const reward = Math.max(1, state.currentPromotionRewardQuantity || 1);
                const nights = getStayNights();
                if (nights < required) {
                    notes.push(`للاستفادة: احجز ${required} ليالٍ على الأقل لتحصل على ${reward} مجانًا`);
                } else if (state.currentPromotionIsRewardRepeatable) {
                    notes.push("المكافأة تتكرر كلما اكتملت مجموعة جديدة");
                } else {
                    notes.push("المكافأة تطبق مرة واحدة فقط على الحجز");
                }
            } else if (rewardType === "freeservice") {
                notes.push("المكافأة خدمة مجانية وتثبت نهائيًا في المراجعة");
            } else if (rewardType === "servicespecialprice") {
                notes.push("السعر الخاص يخص خدمة إضافية مختارة");
            }

            if (state.currentPromotionMinimumPeople > 0) {
                notes.push(
                    `يحتاج ${state.currentPromotionMinimumPeople} أشخاص على الأقل`
                );
            }

            if (state.currentPromotionCodeRequired) {
                notes.push("يتطلب كود العرض");
            }

            note.textContent = notes.length
                ? `${notes.join(" · ")} · يعاد التحقق قبل التأكيد.`
                : "يُعاد التحقق من العرض والسعر النهائي قبل التأكيد.";
        }
    }

    function resetGuestCounters() {
        state.lastValidAdults = 0;
        state.lastValidChildren = 0;

        const adultsInput = getElement("adultsCount");
        const childrenInput = getElement("childrenCount");

        if (adultsInput) {
            adultsInput.value = "0";
        }

        if (childrenInput) {
            childrenInput.value = "0";
        }

        hideCapacityWarning();
    }

    /* ======================================================================
       07. عدادات الأشخاص والتحقق من السعة
       ====================================================================== */

    window.smartUpdateModalCount = function smartUpdateModalCount(
        inputId,
        change
    ) {
        const input = getElement(inputId);

        if (!input) {
            return;
        }

        const currentValue = Math.max(
            0,
            toInteger(input.value, 0)
        );

        input.value = String(
            Math.max(
                0,
                currentValue + toInteger(change, 0)
            )
        );

        window.syncModalBookingNumbers(
            inputId === "adultsCount"
                ? "adults"
                : "children"
        );
    };

    window.syncModalBookingNumbers = function syncModalBookingNumbers(
        changedField
    ) {
        const adultsInput = getElement("adultsCount");
        const childrenInput = getElement("childrenCount");

        if (!adultsInput || !childrenInput) {
            return;
        }

        if (
            adultsInput.value === "" ||
            childrenInput.value === ""
        ) {
            return;
        }

        let adults = Math.max(
            0,
            toInteger(adultsInput.value, 0)
        );

        let children = Math.max(
            0,
            toInteger(childrenInput.value, 0)
        );

        const maximum = Math.max(
            0,
            state.maximumGuests
        );

        const equivalentGuests =
            calculateEquivalentGuests(
                adults,
                children
            );

        if (
            maximum > 0 &&
            equivalentGuests > maximum
        ) {
            showWarning(
                "تجاوز سعة الوحدة",
                `الحد الأقصى للوحدات المختارة يعادل ${maximum} مقاعد. أول 4 أطفال فقط يُحتسب كل طفلين منهم مكان بالغ واحد، ومن الطفل الخامس يُحتسب كل طفل كشخص كامل.`
            );

            if (changedField === "adults") {
                adults = state.lastValidAdults;
            } else if (changedField === "children") {
                children = state.lastValidChildren;
            } else {
                adults = state.lastValidAdults;
                children = state.lastValidChildren;
            }
        } else {
            state.lastValidAdults = adults;
            state.lastValidChildren = children;
        }

        adultsInput.value = String(adults);
        childrenInput.value = String(children);

        updateCapacityMessage(
            calculateEquivalentGuests(
                adults,
                children
            )
        );

        // شرط MinimumPeople الخاص بالعروض يعتمد العدد الفعلي للأشخاص.
        updatePromotionPeopleState(
            adults + children
        );
    };

    function updatePromotionPeopleState(totalPeople) {
        const preview = getElement("guestPromotionPreview");
        const note = getElement("guestPromotionNote");

        if (
            !preview ||
            preview.classList.contains("d-none") ||
            !note ||
            state.currentPromotionMinimumPeople <= 0
        ) {
            return;
        }

        const meetsMinimum =
            totalPeople >= state.currentPromotionMinimumPeople;

        preview.classList.toggle(
            "is-condition-pending",
            !meetsMinimum
        );

        note.textContent = meetsMinimum
            ? "تحقق شرط عدد الأشخاص مبدئيًا · يعاد التحقق قبل التأكيد."
            : `أضف ${state.currentPromotionMinimumPeople} أشخاص على الأقل للاستفادة من العرض.`;
    }

    function updateCapacityMessage(totalGuests) {
        const warningElement = getElement("capacityWarning");
        const warningText = getElement("warningText");

        if (!warningElement || !warningText) {
            return;
        }

        warningElement.classList.remove(
            "alert-info",
            "text-info",
            "border-info",
            "alert-danger",
            "text-danger",
            "border-danger"
        );

        if (state.maximumGuests <= 0 || totalGuests <= 0) {
            hideCapacityWarning();
            return;
        }

        if (totalGuests < state.maximumGuests) {
            hideCapacityWarning();
            return;
        }

        warningElement.classList.remove("d-none");

        if (totalGuests === state.maximumGuests) {
            warningText.innerHTML =
                `وصلت إلى السعة القصوى للوحدات المختارة ` +
                `<strong>(${state.maximumGuests} أشخاص)</strong>.`;

            warningElement.classList.add(
                "alert-info",
                "text-info",
                "border-info"
            );

            return;
        }

        warningText.textContent =
            `لا يمكن تجاوز ${state.maximumGuests} أشخاص.`;

        warningElement.classList.add(
            "alert-danger",
            "text-danger",
            "border-danger"
        );
    }

    function hideCapacityWarning() {
        const warningElement = getElement("capacityWarning");
        const warningText = getElement("warningText");

        if (warningElement) {
            warningElement.classList.add("d-none");
            warningElement.classList.remove(
                "alert-info",
                "text-info",
                "border-info",
                "alert-danger",
                "text-danger",
                "border-danger"
            );
        }

        if (warningText) {
            warningText.textContent = "";
        }
    }

    /* ======================================================================
       08. معرض صور الغرفة
       ====================================================================== */

    function initializeRoomGallery() {
        const modalElement = getElement("roomGalleryModal");

        getElements(".open-room-gallery-btn").forEach(button => {
            if (button.dataset.initialized === "true") {
                return;
            }

            button.dataset.initialized = "true";

            button.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();

                openRoomGallery(button);
            });
        });

        getElement("roomGalleryPrev")?.addEventListener(
            "click",
            () => showGalleryImage(state.galleryIndex - 1)
        );

        getElement("roomGalleryNext")?.addEventListener(
            "click",
            () => showGalleryImage(state.galleryIndex + 1)
        );

        const stage = document.querySelector(".room-gallery-stage");

        stage?.addEventListener(
            "touchstart",
            event => {
                const touch = event.changedTouches[0];

                state.galleryTouchStartX = touch.clientX;
                state.galleryTouchStartY = touch.clientY;
            },
            { passive: true }
        );

        stage?.addEventListener(
            "touchend",
            event => {
                if (state.galleryImages.length < 2) {
                    return;
                }

                const touch = event.changedTouches[0];
                const deltaX =
                    touch.clientX -
                    state.galleryTouchStartX;

                const deltaY =
                    touch.clientY -
                    state.galleryTouchStartY;

                if (
                    Math.abs(deltaX) < 45 ||
                    Math.abs(deltaX) <= Math.abs(deltaY)
                ) {
                    return;
                }

                showGalleryImage(
                    state.galleryIndex +
                    (deltaX < 0 ? 1 : -1)
                );
            },
            { passive: true }
        );

        /*
         * سحب بالماوس/القلم عبر Pointer Events.
         * لا نمنع التمرير العمودي على الهاتف؛ touch swipe يظل بالمسار الموجود.
         */
        stage?.addEventListener("pointerdown", event => {
            if (event.pointerType === "touch") {
                return;
            }

            state.galleryPointerActive = true;
            state.galleryPointerId = event.pointerId;
            state.galleryPointerStartX = event.clientX;
            state.galleryPointerStartY = event.clientY;
            stage.classList.add("is-dragging");

            try {
                stage.setPointerCapture(event.pointerId);
            } catch {
                // بعض المتصفحات لا تسمح بالالتقاط في حالات محددة.
            }
        });

        stage?.addEventListener("pointerup", event => {
            if (
                !state.galleryPointerActive ||
                state.galleryPointerId !== event.pointerId
            ) {
                return;
            }

            const deltaX =
                event.clientX - state.galleryPointerStartX;
            const deltaY =
                event.clientY - state.galleryPointerStartY;

            state.galleryPointerActive = false;
            state.galleryPointerId = null;
            stage.classList.remove("is-dragging");

            if (
                state.galleryImages.length > 1 &&
                Math.abs(deltaX) >= 55 &&
                Math.abs(deltaX) > Math.abs(deltaY)
            ) {
                showGalleryImage(
                    state.galleryIndex +
                    (deltaX < 0 ? 1 : -1)
                );
            }
        });

        stage?.addEventListener("pointercancel", () => {
            state.galleryPointerActive = false;
            state.galleryPointerId = null;
            stage.classList.remove("is-dragging");
        });

        if (modalElement) {
            modalElement.addEventListener("hidden.bs.modal", () => {
                resetGalleryState();
                cleanOrphanModalState();
            });
        }
    }

    function openRoomGallery(sourceElement) {
        const modalElement = getElement("roomGalleryModal");

        if (!modalElement) {
            showError(
                "تعذر فتح الصور",
                "نافذة معرض الصور غير موجودة في الصفحة."
            );
            return;
        }

        const images = parseGalleryImages(
            sourceElement.dataset.roomImages,
            sourceElement.getAttribute("href")
        );

        if (!images.length) {
            showWarning(
                "لا توجد صور",
                "لم يضف مالك المنشأة صورًا لهذه الوحدة."
            );
            return;
        }

        state.galleryImages = images;
        state.galleryIndex = 0;
        state.galleryTitle =
            sourceElement.dataset.roomTitle ||
            "صور الغرفة";

        const titleElement = getElement("roomGalleryTitle");

        if (titleElement) {
            titleElement.textContent = state.galleryTitle;
        }

        buildGalleryThumbnails();
        showGalleryImage(0, false);

        const modal = getBootstrapModal(modalElement);

        if (!modal) {
            showError(
                "تعذر فتح الصور",
                "تأكد من تحميل bootstrap.bundle.min.js داخل Layout."
            );
            return;
        }

        modal.show();
    }

    function parseGalleryImages(rawValue, fallbackImage) {
        const images = [];

        const addImage = value => {
            const image = String(value ?? "").trim();

            if (!image || images.includes(image)) {
                return;
            }

            images.push(image);
        };

        if (rawValue) {
            try {
                const parsed = JSON.parse(rawValue);

                if (Array.isArray(parsed)) {
                    parsed.forEach(addImage);
                }
            } catch {
                String(rawValue)
                    .split(/[,;|\r\n]+/)
                    .forEach(addImage);
            }
        }

        addImage(fallbackImage);

        return images;
    }

    function buildGalleryThumbnails() {
        const container = getElement("roomGalleryThumbnails");

        if (!container) {
            return;
        }

        container.replaceChildren();

        state.galleryImages.forEach((src, index) => {
            const button = document.createElement("button");
            const image = document.createElement("img");

            button.type = "button";
            button.className = "room-gallery-thumbnail";
            button.setAttribute(
                "aria-label",
                `عرض الصورة ${index + 1}`
            );

            image.src = src;
            image.alt = `${state.galleryTitle} - صورة ${index + 1}`;
            image.loading = index === 0 ? "eager" : "lazy";
            image.onerror = () => {
                image.onerror = null;
                image.src = "/images/bg1.jpg";
            };

            button.appendChild(image);
            button.addEventListener("click", () => {
                showGalleryImage(index);
            });

            container.appendChild(button);
        });
    }

    function showGalleryImage(index, animate = true) {
        if (!state.galleryImages.length) {
            return;
        }

        const total = state.galleryImages.length;
        const safeIndex = ((index % total) + total) % total;
        const mainImage = getElement("roomGalleryMainImage");
        const counter = getElement("roomGalleryCounter");
        const previous = getElement("roomGalleryPrev");
        const next = getElement("roomGalleryNext");

        state.galleryIndex = safeIndex;

        if (mainImage) {
            if (animate) {
                mainImage.style.opacity = "0.25";
                mainImage.style.transform = "scale(0.992)";
            }

            const applyImage = () => {
                mainImage.src = state.galleryImages[safeIndex];
                mainImage.alt =
                    `${state.galleryTitle} - صورة ${safeIndex + 1}`;

                mainImage.onload = () => {
                    mainImage.style.opacity = "1";
                    mainImage.style.transform = "none";
                };

                mainImage.onerror = () => {
                    mainImage.onerror = null;
                    mainImage.src = "/images/bg1.jpg";
                    mainImage.style.opacity = "1";
                    mainImage.style.transform = "none";
                };
            };

            if (animate) {
                window.setTimeout(applyImage, 90);
            } else {
                applyImage();
            }
        }

        if (counter) {
            counter.textContent = `${safeIndex + 1} / ${total}`;
        }

        const showNavigation = total > 1;

        [previous, next].forEach(button => {
            if (!button) {
                return;
            }

            button.hidden = !showNavigation;
            button.disabled = !showNavigation;
        });

        getElements(".room-gallery-thumbnail").forEach(
            (thumbnail, thumbnailIndex) => {
                const active = thumbnailIndex === safeIndex;

                thumbnail.classList.toggle("active", active);
                thumbnail.setAttribute(
                    "aria-current",
                    active ? "true" : "false"
                );

                if (active) {
                    window.setTimeout(() => {
                        thumbnail.scrollIntoView({
                            behavior: "smooth",
                            inline: "center",
                            block: "nearest"
                        });
                    }, 40);
                }
            }
        );

        preloadGalleryNeighbor(safeIndex + 1);
        preloadGalleryNeighbor(safeIndex - 1);
    }

    function preloadGalleryNeighbor(index) {
        if (state.galleryImages.length < 2) {
            return;
        }

        const total = state.galleryImages.length;
        const safeIndex = ((index % total) + total) % total;
        const image = new Image();

        image.src = state.galleryImages[safeIndex];
    }

    function resetGalleryState() {
        state.galleryImages = [];
        state.galleryIndex = 0;
        state.galleryTitle = "صور الغرفة";

        const thumbnailContainer = getElement(
            "roomGalleryThumbnails"
        );

        thumbnailContainer?.replaceChildren();
    }

    /* ======================================================================
       09. الانتقال إلى Checkout
       ====================================================================== */

    window.proceedToCheckout = async function proceedToCheckout() {
        if (!(await ensureBookingProfileReady())) {
            return;
        }

        const placeId = toInteger(
            getHiddenValue("placeIdInput", "0"),
            0
        );

        const startDate = getHiddenValue("startDateInput");
        const endDate = getHiddenValue("endDateInput");

        const adults = Math.max(
            0,
            toInteger(
                getElement("adultsCount")?.value,
                0
            )
        );

        const children = Math.max(
            0,
            toInteger(
                getElement("childrenCount")?.value,
                0
            )
        );

        if (placeId <= 0) {
            showError(
                "تعذر تحديد المنشأة",
                "رقم المنشأة غير صحيح. أعد تحميل الصفحة."
            );
            return;
        }

        if (state.currentRoomId <= 0) {
            showWarning(
                "لم يتم اختيار وحدة",
                "اختر الوحدة أولًا ثم أكمل بيانات الحجز."
            );
            return;
        }

        if (!startDate || !endDate) {
            showError(
                "التواريخ غير مكتملة",
                "تعذر قراءة تاريخ الدخول أو الخروج."
            );
            return;
        }

        if (!hasRequiredAdult(adults)) {
            showWarning(
                "يجب إضافة شخص بالغ",
                "لا يمكن إكمال حجز الفندق بأطفال فقط. يجب وجود شخص بالغ واحد على الأقل."
            );
            return;
        }

        const equivalentGuests =
            calculateEquivalentGuests(
                adults,
                children
            );

        if (
            state.maximumGuests > 0 &&
            equivalentGuests > state.maximumGuests
        ) {
            showWarning(
                "تجاوز سعة الوحدة",
                `الحد الأقصى للوحدات المختارة يعادل ${state.maximumGuests} مقاعد. أول 4 أطفال فقط يُحتسب كل طفلين منهم مكان بالغ واحد، ومن الطفل الخامس يُحتسب كل طفل كشخص كامل.`
            );
            return;
        }

        const countInput = getElement(
            state.currentCountInputId
        );

        const quantity = Math.max(
            1,
            toInteger(
                countInput?.value,
                state.selectedRoomCount
            )
        );

        const bookingTypeResult = resolveBookingTypeValue();

        if (!bookingTypeResult.valid) {
            await notify({
                icon: "warning",
                title: "أكمل نوع الحجز",
                text: bookingTypeResult.message || "حدد نوع الحجز قبل المتابعة.",
                confirmButtonText: "حسنًا"
            });
            return;
        }

        const parameters = new URLSearchParams();

        parameters.set("placeId", String(placeId));
        parameters.set("roomId", String(state.currentRoomId));
        parameters.set("startDate", startDate);
        parameters.set("endDate", endDate);
        parameters.set("quantity", String(quantity));
        parameters.set("adultsCount", String(adults));
        parameters.set("childrenCount", String(children));
        parameters.set(
            "bookingType",
            bookingTypeResult.value
        );

        getElements(".selected-service-id").forEach(input => {
            const serviceId = toInteger(input.value, 0);

            if (serviceId > 0) {
                parameters.append(
                    "serviceIds",
                    String(serviceId)
                );
            }
        });

        const selectedPromotionId = Math.max(
            0,
            toInteger(
                getHiddenValue("selectedPromotionIdInput", "0"),
                0
            )
        );

        if (selectedPromotionId > 0) {
            parameters.set(
                "promotionId",
                String(selectedPromotionId)
            );
        }

        /*
         * لا نرسل السعر أو اسم الغرفة أو الوصف.
         * Checkout يعيد قراءتها من قاعدة البيانات بواسطة RoomId.
         */
        window.location.href =
            `/BookingFlow/Checkout?${parameters.toString()}`;
    };

    /* ======================================================================
       10. التجربة البصرية الفخمة بدون تعطيل النقر
       ====================================================================== */

    function initializeLuxuryRoomExperience() {
        const reduceMotion = window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        ).matches;

        const revealItems = getElements(".yg-reveal-3d");

        if (reduceMotion || !("IntersectionObserver" in window)) {
            revealItems.forEach(item => item.classList.add("is-visible"));
        } else {
            const observer = new IntersectionObserver(entries => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) {
                        return;
                    }

                    entry.target.classList.add("is-visible");
                    observer.unobserve(entry.target);
                });
            }, {
                threshold: 0.12,
                rootMargin: "0px 0px -6% 0px"
            });

            revealItems.forEach(item => observer.observe(item));
        }

        if (
            reduceMotion ||
            window.matchMedia("(hover: none), (pointer: coarse)").matches
        ) {
            return;
        }

        getElements(".luxury-room-card").forEach(card => {
            card.addEventListener("pointermove", event => {
                const rect = card.getBoundingClientRect();
                const x = ((event.clientX - rect.left) / rect.width) * 100;
                const y = ((event.clientY - rect.top) / rect.height) * 100;

                card.style.setProperty("--room-pointer-x", `${x}%`);
                card.style.setProperty("--room-pointer-y", `${y}%`);
            }, { passive: true });

            card.addEventListener("pointerleave", () => {
                card.style.removeProperty("--room-pointer-x");
                card.style.removeProperty("--room-pointer-y");
            }, { passive: true });
        });
    }

    function initializePromotionCodeCopy() {
        getElements("[data-copy-code]").forEach(element => {
            element.addEventListener("click", async () => {
                const code = element.getAttribute("data-copy-code") || "";

                if (!code) {
                    return;
                }

                try {
                    await navigator.clipboard.writeText(code);
                    element.classList.add("is-copied");

                    window.setTimeout(() => {
                        element.classList.remove("is-copied");
                    }, 1200);
                } catch {
                    // النسخ ليس شرطًا لاستمرار رحلة الحجز.
                }
            });
        });
    }

    /* ======================================================================
       11. تنظيف المودالات واختصارات لوحة المفاتيح
       ====================================================================== */

    function initializeModalCleanup() {
        getElements(".modal").forEach(modalElement => {
            modalElement.addEventListener(
                "hidden.bs.modal",
                cleanOrphanModalState
            );
        });
    }

    function initializeKeyboardShortcuts() {
        document.addEventListener("keydown", event => {
            const galleryModal = getElement("roomGalleryModal");
            const galleryOpen =
                galleryModal?.classList.contains("show") === true;

            if (!galleryOpen) {
                return;
            }

            if (event.key === "ArrowLeft") {
                event.preventDefault();
                showGalleryImage(state.galleryIndex + 1);
            } else if (event.key === "ArrowRight") {
                event.preventDefault();
                showGalleryImage(state.galleryIndex - 1);
            }
        });
    }
})();