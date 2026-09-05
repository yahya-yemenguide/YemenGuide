/**
 * دليلك اليمني - صفحة حجوزات العميل
 * الوظائف: البحث، التبويبات، العدادات، حالات الفراغ، ومؤقت دفع العربون.
 * لا يحتوي هذا الملف على أي تنفيذ لإلغاء الحجز.
 * المسار: wwwroot/js/customer/customer-bookings.js
 */
(() => {
    "use strict";

    const state = {
        activeStatus: "all",
        searchQuery: "",
        searchTimer: null,
        countdownTimer: null
    };

    const $ = (selector, scope = document) =>
        scope.querySelector(selector);

    const $$ = (selector, scope = document) =>
        Array.from(scope.querySelectorAll(selector));

    const bookingRows = () =>
        $$(".booking-card-item");

    const filterPills = () =>
        $$(".filter-pill[data-status]");

    /* =====================================================================
       1. أدوات عامة
       ===================================================================== */

    function normalizeSearch(value) {
        return String(value ?? "")
            .normalize("NFKD")
            .replace(
                /[\u064B-\u065F\u0670]/g,
                ""
            )
            .replace(/[إأآٱ]/g, "ا")
            .replace(/ى/g, "ي")
            .replace(/ة/g, "ه")
            .toLowerCase()
            .replace(/\s+/g, " ")
            .trim();
    }

    function getRowStatus(row) {
        return String(
            row?.dataset?.bookingStatus ||
            "other"
        )
            .trim()
            .toLowerCase();
    }

    function getActivePill() {
        return filterPills().find(
            pill =>
                pill.classList.contains(
                    "active"
                )
        ) || null;
    }

    function getPillLabel(pill) {
        return (
            pill
                ?.querySelector(
                    ":scope > span"
                )
                ?.textContent
                ?.trim() ||
            pill
                ?.textContent
                ?.trim() ||
            "الكل"
        );
    }

    function getInitialStatus() {
        const selected =
            getActivePill()
                ?.dataset
                ?.status;

        return (
            selected &&
            filterPills().some(
                pill =>
                    pill.dataset.status ===
                    selected
            )
        )
            ? selected
            : "all";
    }

    /* =====================================================================
       2. التبويبات والفلاتر
       ===================================================================== */

    function activateFilter(
        requestedStatus,
        shouldScroll = true
    ) {
        const pills =
            filterPills();

        const target =
            pills.find(
                pill =>
                    pill.dataset.status ===
                    requestedStatus
            ) ||
            pills.find(
                pill =>
                    pill.dataset.status ===
                    "all"
            );

        if (!target) {
            state.activeStatus =
                "all";

            return;
        }

        state.activeStatus =
            target.dataset.status ||
            "all";

        pills.forEach(
            pill => {
                const isActive =
                    pill === target;

                pill.classList.toggle(
                    "active",
                    isActive
                );

                pill.setAttribute(
                    "aria-selected",
                    isActive
                        ? "true"
                        : "false"
                );

                pill.setAttribute(
                    "tabindex",
                    isActive
                        ? "0"
                        : "-1"
                );
            }
        );

        if (shouldScroll) {
            target.scrollIntoView({
                behavior: "smooth",
                block: "nearest",
                inline: "center"
            });
        }
    }

    function rowMatchesCurrentFilter(
        row
    ) {
        const matchesStatus =
            state.activeStatus ===
            "all" ||
            getRowStatus(row) ===
            state.activeStatus;

        if (!matchesStatus) {
            return false;
        }

        if (!state.searchQuery) {
            return true;
        }

        const searchableText =
            normalizeSearch(
                row.dataset
                    .searchText ||
                row.textContent ||
                ""
            );

        return searchableText.includes(
            state.searchQuery
        );
    }

    function updateFilterCounts() {
        const rows =
            bookingRows();

        filterPills().forEach(
            pill => {
                const countElement =
                    $(
                        ".filter-pill-count",
                        pill
                    );

                if (!countElement) {
                    return;
                }

                const status =
                    pill.dataset.status ||
                    "all";

                const count =
                    status === "all"
                        ? rows.length
                        : rows.filter(
                            row =>
                                getRowStatus(
                                    row
                                ) ===
                                status
                        ).length;

                countElement.textContent =
                    String(count);
            }
        );
    }

    function updateResultBar(
        visibleCount
    ) {
        const visibleElement =
            $(
                "#visibleBookingsCount"
            );

        const resultText =
            $(
                "#bookingResultsText"
            );

        const rawSearch =
            $(
                "#bookingSearchInput"
            )
                ?.value
                ?.trim() || "";

        const filterLabel =
            getPillLabel(
                getActivePill()
            );

        if (visibleElement) {
            visibleElement.textContent =
                String(visibleCount);
        }

        if (!resultText) {
            return;
        }

        if (
            state.activeStatus ===
            "all" &&
            !rawSearch
        ) {
            resultText.textContent =
                "عرض جميع الحجوزات";
        } else if (
            state.activeStatus !==
            "all" &&
            !rawSearch
        ) {
            resultText.textContent =
                `عرض حجوزات: ${filterLabel}`;
        } else if (
            state.activeStatus ===
            "all"
        ) {
            resultText.textContent =
                `نتائج البحث عن “${rawSearch}”`;
        } else {
            resultText.textContent =
                `نتائج “${rawSearch}” ضمن ${filterLabel}`;
        }
    }

    function updateEmptyState(
        visibleCount
    ) {
        const emptyState =
            $(
                "#emptyBookingsState"
            );

        const message =
            $(
                "#emptyBookingsMessage"
            );

        if (!emptyState) {
            return;
        }

        const shouldShow =
            bookingRows().length > 0 &&
            visibleCount === 0;

        emptyState.classList.toggle(
            "d-none",
            !shouldShow
        );

        if (
            !shouldShow ||
            !message
        ) {
            return;
        }

        const rawSearch =
            $(
                "#bookingSearchInput"
            )
                ?.value
                ?.trim() || "";

        const filterLabel =
            getPillLabel(
                getActivePill()
            );

        if (
            rawSearch &&
            state.activeStatus !==
            "all"
        ) {
            message.textContent =
                `لا توجد حجوزات تطابق “${rawSearch}” ضمن حالة ${filterLabel}.`;
        } else if (rawSearch) {
            message.textContent =
                `لا توجد حجوزات تطابق “${rawSearch}”.`;
        } else if (
            state.activeStatus !==
            "all"
        ) {
            message.textContent =
                `لا توجد حجوزات ضمن حالة ${filterLabel}.`;
        } else {
            message.textContent =
                "لا توجد حجوزات مطابقة للبحث أو الفلتر المحدد.";
        }
    }

    function applyFilters() {
        let visibleCount = 0;

        bookingRows().forEach(
            row => {
                const visible =
                    rowMatchesCurrentFilter(
                        row
                    );

                row.hidden =
                    !visible;

                row.setAttribute(
                    "aria-hidden",
                    visible
                        ? "false"
                        : "true"
                );

                if (visible) {
                    visibleCount += 1;
                }
            }
        );

        const clearButton =
            $(
                "#clearBookingSearch"
            );

        const searchInput =
            $(
                "#bookingSearchInput"
            );

        if (clearButton) {
            clearButton.hidden =
                !searchInput
                    ?.value
                    ?.trim();
        }

        updateResultBar(
            visibleCount
        );

        updateEmptyState(
            visibleCount
        );
    }

    function clearSearch() {
        const searchInput =
            $(
                "#bookingSearchInput"
            );

        if (!searchInput) {
            return;
        }

        searchInput.value = "";
        state.searchQuery = "";

        applyFilters();
        searchInput.focus();
    }

    function resetFilters() {
        const searchInput =
            $(
                "#bookingSearchInput"
            );

        if (searchInput) {
            searchInput.value = "";
        }

        state.searchQuery = "";

        activateFilter(
            "all"
        );

        applyFilters();

        $(
            "#bookingsContainer"
        )?.scrollIntoView({
            behavior: "smooth",
            block: "start"
        });
    }

    function initializeFiltering() {
        filterPills().forEach(
            pill => {
                pill.addEventListener(
                    "click",
                    () => {
                        activateFilter(
                            pill.dataset.status ||
                            "all"
                        );

                        applyFilters();
                    }
                );

                pill.addEventListener(
                    "keydown",
                    event => {
                        if (
                            event.key !==
                            "ArrowLeft" &&
                            event.key !==
                            "ArrowRight"
                        ) {
                            return;
                        }

                        event.preventDefault();

                        const pills =
                            filterPills();

                        const currentIndex =
                            pills.indexOf(
                                pill
                            );

                        if (
                            currentIndex < 0 ||
                            pills.length === 0
                        ) {
                            return;
                        }

                        const direction =
                            event.key ===
                                "ArrowLeft"
                                ? 1
                                : -1;

                        const nextIndex =
                            (
                                currentIndex +
                                direction +
                                pills.length
                            ) %
                            pills.length;

                        const nextPill =
                            pills[nextIndex];

                        nextPill.focus();
                        nextPill.click();
                    }
                );
            }
        );

        const searchInput =
            $(
                "#bookingSearchInput"
            );

        searchInput
            ?.addEventListener(
                "input",
                () => {
                    window.clearTimeout(
                        state.searchTimer
                    );

                    state.searchTimer =
                        window.setTimeout(
                            () => {
                                state.searchQuery =
                                    normalizeSearch(
                                        searchInput
                                            .value
                                    );

                                applyFilters();
                            },
                            120
                        );

                    const clearButton =
                        $(
                            "#clearBookingSearch"
                        );

                    if (clearButton) {
                        clearButton.hidden =
                            !searchInput
                                .value
                                .trim();
                    }
                }
            );

        searchInput
            ?.addEventListener(
                "keydown",
                event => {
                    if (
                        event.key ===
                        "Escape"
                    ) {
                        event.preventDefault();
                        clearSearch();
                    }
                }
            );

        $(
            "#clearBookingSearch"
        )?.addEventListener(
            "click",
            clearSearch
        );

        $(
            "#resetBookingFilters"
        )?.addEventListener(
            "click",
            resetFilters
        );
    }

    /* =====================================================================
       3. مؤقت دفع العربون
       ===================================================================== */

    function formatRemainingTime(
        milliseconds
    ) {
        const totalSeconds =
            Math.max(
                0,
                Math.floor(
                    milliseconds /
                    1000
                )
            );

        const days =
            Math.floor(
                totalSeconds /
                86400
            );

        const hours =
            Math.floor(
                (
                    totalSeconds %
                    86400
                ) /
                3600
            );

        const minutes =
            Math.floor(
                (
                    totalSeconds %
                    3600
                ) /
                60
            );

        const seconds =
            totalSeconds %
            60;

        const pad = value =>
            String(value)
                .padStart(
                    2,
                    "0"
                );

        return days > 0
            ? `${days} يوم ${pad(hours)}:${pad(minutes)}:${pad(seconds)}`
            : `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    }

    function markBookingAsExpired(
        timerElement
    ) {
        if (
            !timerElement ||
            timerElement.dataset
                .expired ===
            "true"
        ) {
            return;
        }

        timerElement.dataset.expired =
            "true";

        const row =
            timerElement.closest(
                ".booking-card-item"
            );

        if (!row) {
            timerElement.remove();
            return;
        }

        row.dataset.bookingStatus =
            "expired";

        row.dataset.bookingState =
            "Expired";

        row.dataset.searchText =
            `${row.dataset
                .searchText || ""
            } انتهت مهلة الدفع expired`;

        row.classList.remove(
            "booking-status-pending",
            "booking-status-payment",
            "booking-status-review",
            "booking-status-confirmed"
        );

        row.classList.add(
            "booking-status-expired"
        );

        const stateBadge =
            $(
                ".customer-state-badge",
                row
            );

        if (stateBadge) {
            stateBadge.classList.remove(
                "payment",
                "warning",
                "review",
                "success",
                "completed",
                "muted",
                "default"
            );

            stateBadge.classList.add(
                "danger"
            );

            stateBadge.innerHTML = `
                <i class="bi bi-hourglass-bottom"></i>
                <span>انتهت مهلة الدفع</span>
            `;
        }

        const description =
            $(
                ".customer-state-description",
                row
            );

        if (description) {
            description.textContent =
                "انتهت المهلة المحددة لدفع العربون.";

            description.title =
                description.textContent;
        }

        $(
            ".customer-booking-button.primary",
            row
        )?.remove();

        timerElement.remove();

        updateFilterCounts();
        applyFilters();
    }

    function updateCountdowns() {
        const timers =
            $$(
                ".customer-payment-timer[data-countdown]"
            );

        if (timers.length === 0) {
            stopCountdowns();
            return;
        }

        timers.forEach(
            timerElement => {
                if (
                    timerElement.dataset
                        .expired ===
                    "true"
                ) {
                    return;
                }

                const expiryDate =
                    new Date(
                        timerElement
                            .dataset
                            .countdown ||
                        ""
                    );

                const countdownText =
                    $(
                        ".booking-payment-countdown",
                        timerElement
                    );

                if (
                    Number.isNaN(
                        expiryDate.getTime()
                    ) ||
                    !countdownText
                ) {
                    timerElement.remove();
                    return;
                }

                const remainingTime =
                    expiryDate.getTime() -
                    Date.now();

                if (
                    remainingTime <= 0
                ) {
                    markBookingAsExpired(
                        timerElement
                    );

                    return;
                }

                countdownText.textContent =
                    formatRemainingTime(
                        remainingTime
                    );

                timerElement.classList.toggle(
                    "is-urgent",
                    remainingTime <=
                    10 * 60 * 1000
                );
            }
        );
    }

    function startCountdowns() {
        stopCountdowns();
        updateCountdowns();

        if (
            !$(
                ".customer-payment-timer[data-countdown]"
            )
        ) {
            return;
        }

        state.countdownTimer =
            window.setInterval(
                () => {
                    if (
                        document
                            .visibilityState ===
                        "visible"
                    ) {
                        updateCountdowns();
                    }
                },
                1000
            );
    }

    function stopCountdowns() {
        if (
            !state.countdownTimer
        ) {
            return;
        }

        window.clearInterval(
            state.countdownTimer
        );

        state.countdownTimer =
            null;
    }

    /* =====================================================================
       4. التهيئة
       ===================================================================== */

    function initialize() {
        state.activeStatus =
            getInitialStatus();

        initializeFiltering();

        document.addEventListener(
            "visibilitychange",
            () => {
                if (
                    document
                        .visibilityState ===
                    "visible"
                ) {
                    updateCountdowns();
                }
            }
        );

        window.addEventListener(
            "pageshow",
            updateCountdowns
        );

        window.addEventListener(
            "beforeunload",
            stopCountdowns
        );

        activateFilter(
            state.activeStatus,
            false
        );

        updateFilterCounts();
        applyFilters();
        startCountdowns();
    }

    if (
        document.readyState ===
        "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            initialize,
            {
                once: true
            }
        );
    } else {
        initialize();
    }
})();

/* Luxury 3D ticket motion — desktop pointer only */
(() => {
    "use strict";

    const media = window.matchMedia?.("(hover:hover) and (pointer:fine)");
    if (!media?.matches) return;
    if (window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches) return;

    const bind = row => {
        if (!row || row.dataset.luxuryMotionBound === "true") return;
        row.dataset.luxuryMotionBound = "true";

        row.addEventListener("pointermove", event => {
            const rect = row.getBoundingClientRect();
            const px = (event.clientX - rect.left) / Math.max(1, rect.width);
            const py = (event.clientY - rect.top) / Math.max(1, rect.height);

            const x = px * 100;
            const y = py * 100;
            const ry = (px - 0.5) * 2.4;
            const rx = (0.5 - py) * 1.5;

            row.style.setProperty("--cb-mx", `${x}%`);
            row.style.setProperty("--cb-my", `${y}%`);
            row.style.setProperty("--cb-rx", `${rx.toFixed(2)}deg`);
            row.style.setProperty("--cb-ry", `${ry.toFixed(2)}deg`);
        }, { passive: true });

        row.addEventListener("pointerleave", () => {
            row.style.setProperty("--cb-rx", "0deg");
            row.style.setProperty("--cb-ry", "0deg");
            row.style.setProperty("--cb-mx", "78%");
            row.style.setProperty("--cb-my", "15%");
        }, { passive: true });
    };

    const reveal = () => {
        document.querySelectorAll(".customer-booking-row").forEach((row, index) => {
            bind(row);
            row.animate(
                [
                    { opacity: 0, transform: "translateY(16px) scale(.988)" },
                    { opacity: 1, transform: "translateY(0) scale(1)" }
                ],
                {
                    duration: 430,
                    delay: Math.min(index * 55, 330),
                    easing: "cubic-bezier(.22,1,.36,1)",
                    fill: "both"
                }
            );
        });
    };

    document.readyState === "loading"
        ? document.addEventListener("DOMContentLoaded", reveal, { once: true })
        : reveal();
})();

/* ============================================================================
   CUSTOMER REFUND EXPERIENCE — VIP V2
   Compact UI + intl-tel-input + reliable save flow
   ============================================================================ */
(() => {
    "use strict";

    if (window.__YGCustomerRefundV2Bound) return;
    window.__YGCustomerRefundV2Bound = true;

    const walletCache = new Map();
    const selector = "[data-customer-refund-context]";
    let lastFocus = null;

    const qs = (s, root = document) => root.querySelector(s);
    const clean = value => String(value ?? "").trim();
    const asInt = value => {
        const n = Number.parseInt(clean(value), 10);
        return Number.isFinite(n) ? n : 0;
    };
    const reduceMotion = () =>
        Boolean(window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches);

    function token() {
        return qs('input[name="__RequestVerificationToken"]')?.value || "";
    }

    function notify(message, type = "success") {
        qs(".refund-v2-toast")?.remove();

        const toast = document.createElement("div");
        toast.className = `refund-v2-toast is-${type}`;
        toast.innerHTML = `
            <span class="refund-v2-toast__icon">
                <i class="bi ${type === "success"
                ? "bi-check2"
                : type === "warning"
                    ? "bi-exclamation-lg"
                    : "bi-x-lg"
            }"></i>
            </span>
            <span>${message}</span>
        `;

        document.body.appendChild(toast);
        requestAnimationFrame(() => toast.classList.add("is-visible"));

        window.setTimeout(() => {
            toast.classList.remove("is-visible");
            window.setTimeout(() => toast.remove(), 200);
        }, 3000);
    }

    async function requestJson(url, options = {}) {
        const response = await fetch(url, {
            credentials: "same-origin",
            cache: "no-store",
            ...options,
            headers: {
                Accept: "application/json",
                "X-Requested-With": "XMLHttpRequest",
                ...(options.headers || {})
            }
        });

        const contentType = response.headers.get("content-type") || "";
        let data;

        if (contentType.includes("application/json")) {
            data = await response.json();
        } else {
            data = {
                success: response.ok,
                message: clean(await response.text())
            };
        }

        if (!response.ok || data?.success === false) {
            throw new Error(data?.message || "تعذر تنفيذ العملية.");
        }

        return data;
    }

    async function getWallets(url) {
        if (walletCache.has(url)) return walletCache.get(url);

        const data = await requestJson(url);
        const source = Array.isArray(data?.wallets)
            ? data.wallets
            : Array.isArray(data?.Wallets)
                ? data.Wallets
                : [];

        const wallets = source
            .map(item => ({
                id: asInt(item?.walletId ?? item?.WalletId),
                name: clean(item?.walletName ?? item?.WalletName) || "محفظة",
                logo: clean(item?.logoPath ?? item?.LogoPath)
            }))
            .filter(item => item.id > 0);

        walletCache.set(url, wallets);
        return wallets;
    }

    function lockPage(locked) {
        document.documentElement.classList.toggle("refund-v2-open", locked);
    }

    function createModal({ title, eyebrow, description, icon = "bi-wallet2", mode = "" }) {
        qs(".refund-v2-overlay")?.remove();

        lastFocus = document.activeElement instanceof HTMLElement
            ? document.activeElement
            : null;

        const overlay = document.createElement("div");
        overlay.className = "refund-v2-overlay";

        const modal = document.createElement("section");
        modal.className = `refund-v2-modal ${mode ? `is-${mode}` : ""}`;
        modal.setAttribute("role", "dialog");
        modal.setAttribute("aria-modal", "true");

        modal.innerHTML = `
            <header class="refund-v2-modal__header">
                <div class="refund-v2-modal__heading">
                    <span class="refund-v2-modal__heading-icon">
                        <i class="bi ${icon}"></i>
                    </span>
                    <div>
                        <small>${eyebrow}</small>
                        <h2>${title}</h2>
                        <p>${description}</p>
                    </div>
                </div>
                <button type="button"
                        class="refund-v2-modal__close"
                        aria-label="إغلاق">
                    <i class="bi bi-x-lg"></i>
                </button>
            </header>
            <div class="refund-v2-modal__body"></div>
            <footer class="refund-v2-modal__footer"></footer>
        `;

        overlay.appendChild(modal);
        document.body.appendChild(overlay);

        const close = () => {
            overlay.classList.remove("is-open");
            lockPage(false);
            window.setTimeout(() => {
                overlay.remove();
                lastFocus?.focus?.({ preventScroll: true });
                lastFocus = null;
            }, reduceMotion() ? 0 : 170);
        };

        qs(".refund-v2-modal__close", modal).addEventListener("click", close);
        overlay.addEventListener("mousedown", event => {
            if (event.target === overlay) close();
        });

        const escape = event => {
            if (event.key !== "Escape" || !document.body.contains(overlay)) return;
            close();
            document.removeEventListener("keydown", escape);
        };
        document.addEventListener("keydown", escape);

        lockPage(true);
        requestAnimationFrame(() => overlay.classList.add("is-open"));

        return {
            overlay,
            modal,
            body: qs(".refund-v2-modal__body", modal),
            footer: qs(".refund-v2-modal__footer", modal),
            close
        };
    }

    function amountBanner(amountText) {
        const el = document.createElement("div");
        el.className = "refund-v2-amount";
        el.innerHTML = `
            <span><i class="bi bi-cash-coin"></i></span>
            <div>
                <small>المبلغ المستحق لك</small>
                <strong>${amountText || "—"}</strong>
            </div>
            <em>المبلغ محسوب من النظام ولا يمكن تغييره</em>
        `;
        return el;
    }

    function setBusy(button, busy, text = "جاري الحفظ...") {
        if (!button) return;

        if (busy) {
            button.dataset.refundOriginalHtml ||= button.innerHTML;
            button.disabled = true;
            button.innerHTML = `<span class="refund-v2-spinner"></span>${text}`;
        } else {
            button.disabled = false;
            if (button.dataset.refundOriginalHtml) {
                button.innerHTML = button.dataset.refundOriginalHtml;
            }
        }
    }

    function setError(box, message = "") {
        box.textContent = message;
        box.hidden = !message;
    }

    function phoneIsAcceptable(input, iti) {
        const raw = clean(input.value);

        if (!raw) {
            return {
                ok: false,
                message: "أدخل رقم الحساب أو الهاتف."
            };
        }

        if (iti) {
            try {
                if (!iti.isValidNumber()) {
                    return {
                        ok: false,
                        message: "رقم الهاتف غير صحيح. اختر الدولة ثم اكتب الرقم بدون تكرار رمز الدولة."
                    };
                }

                return {
                    ok: true,
                    value: clean(iti.getNumber())
                };
            } catch {
                // fallback below
            }
        }

        if (!/^\+?[0-9\s()-]{6,30}$/.test(raw)) {
            return {
                ok: false,
                message: "رقم الحساب غير صالح."
            };
        }

        return {
            ok: true,
            value: raw
        };
    }

    function initInternationalPhone(input, currentValue = "") {
        if (!input || typeof window.intlTelInput !== "function") {
            return null;
        }

        try {
            const iti = window.intlTelInput(input, {
                initialCountry: "ye",
                preferredCountries: ["ye", "sa", "ae", "eg", "qa"],
                separateDialCode: true,
                formatOnDisplay: true,
                autoPlaceholder: "polite",
                dropdownContainer: document.body,
                utilsScript: "https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js"
            });

            if (clean(currentValue).startsWith("+")) {
                iti.setNumber(clean(currentValue));
            }

            input.addEventListener("input", () => {
                input.value = input.value.replace(/[^0-9\s()-]/g, "");
            });

            return iti;
        } catch (error) {
            console.warn("تعذر تهيئة مكتبة الهاتف للاسترداد.", error);
            return null;
        }
    }

    async function openDestination(context) {
        const requestId = asInt(context.dataset.refundRequestId);
        const walletsUrl = clean(context.dataset.refundWalletsUrl);
        const saveUrl = clean(context.dataset.refundSetUrl);
        const amountText = clean(context.dataset.refundAmountText);

        if (requestId <= 0 || !walletsUrl || !saveUrl) {
            notify("بيانات طلب الاسترداد غير مكتملة.", "error");
            return;
        }

        const modal = createModal({
            eyebrow: "وجهة الاسترداد",
            title: "أين تريد استلام المبلغ؟",
            description: "اختر المحفظة ثم أدخل بيانات الحساب المسجلة باسم المستفيد.",
            icon: "bi-wallet2",
            mode: "destination"
        });

        modal.body.appendChild(amountBanner(amountText));

        const loading = document.createElement("div");
        loading.className = "refund-v2-loading";
        loading.innerHTML = `<span class="refund-v2-spinner"></span><strong>جاري تحميل المحافظ...</strong>`;
        modal.body.appendChild(loading);

        let wallets;
        try {
            wallets = await getWallets(walletsUrl);
        } catch (error) {
            loading.remove();
            const box = document.createElement("div");
            box.className = "refund-v2-empty";
            box.innerHTML = `
                <i class="bi bi-exclamation-circle"></i>
                <strong>تعذر تحميل المحافظ</strong>
                <span>${error?.message || "حاول مرة أخرى."}</span>
            `;
            modal.body.appendChild(box);
            return;
        }

        loading.remove();

        if (!wallets.length) {
            const box = document.createElement("div");
            box.className = "refund-v2-empty";
            box.innerHTML = `
                <i class="bi bi-wallet-x"></i>
                <strong>لا توجد محافظ استرداد نشطة</strong>
                <span>جرّب لاحقًا أو تواصل مع الدعم.</span>
            `;
            modal.body.appendChild(box);
            return;
        }

        const currentWalletId = asInt(context.dataset.refundWalletId);
        const currentAccount = clean(context.dataset.refundAccount);
        const currentBeneficiary = clean(context.dataset.refundBeneficiary);

        const form = document.createElement("form");
        form.className = "refund-v2-form";
        form.noValidate = true;

        const walletsWrap = document.createElement("section");
        walletsWrap.className = "refund-v2-section";
        walletsWrap.innerHTML = `
            <div class="refund-v2-section__title">
                <span>1</span>
                <div>
                    <strong>اختر المحفظة</strong>
                    <small>المحافظ المدعومة حاليًا في النظام</small>
                </div>
            </div>
            <div class="refund-v2-wallets"></div>
        `;

        const walletsGrid = qs(".refund-v2-wallets", walletsWrap);

        wallets.forEach(wallet => {
            const label = document.createElement("label");
            label.className = "refund-v2-wallet";
            label.innerHTML = `
                <input type="radio"
                       name="refundWallet"
                       value="${wallet.id}"
                       ${wallet.id === currentWalletId ? "checked" : ""}>
                <span class="refund-v2-wallet__card">
                    <span class="refund-v2-wallet__logo">
                        ${wallet.logo
                    ? `<img src="${wallet.logo}" alt="" loading="lazy">`
                    : `<b>${wallet.name.charAt(0) || "م"}</b>`
                }
                    </span>
                    <span class="refund-v2-wallet__name">
                        <strong>${wallet.name}</strong>
                        <small>متاحة للاسترداد</small>
                    </span>
                    <span class="refund-v2-wallet__check">
                        <i class="bi bi-check-lg"></i>
                    </span>
                </span>
            `;
            walletsGrid.appendChild(label);
        });

        const fields = document.createElement("section");
        fields.className = "refund-v2-section";
        fields.innerHTML = `
            <div class="refund-v2-section__title">
                <span>2</span>
                <div>
                    <strong>بيانات الحساب</strong>
                    <small>نفس طريقة إدخال الهاتف المستخدمة في الملف الشخصي</small>
                </div>
            </div>

            <div class="refund-v2-fields">
                <label class="refund-v2-field is-phone">
                    <span>رقم الحساب / الهاتف</span>
                    <div class="refund-v2-phone" dir="ltr">
                        <input type="tel"
                               name="accountNumber"
                               autocomplete="tel"
                               inputmode="tel"
                               value="${currentAccount.replace(/"/g, "&quot;")}">
                    </div>
                    <small class="refund-v2-field__hint">
                        اختر الدولة ثم اكتب الرقم بدون رمز الدولة.
                    </small>
                </label>

                <label class="refund-v2-field">
                    <span>اسم المستفيد</span>
                    <div class="refund-v2-input">
                        <i class="bi bi-person"></i>
                        <input type="text"
                               name="beneficiaryName"
                               maxlength="150"
                               autocomplete="name"
                               value="${currentBeneficiary.replace(/"/g, "&quot;")}"
                               placeholder="الاسم كما هو مسجل في المحفظة">
                    </div>
                </label>
            </div>

            <div class="refund-v2-security">
                <i class="bi bi-shield-lock"></i>
                <span>يمكنك تعديل بيانات الاستلام حتى قبل تسجيل المنشأة للتحويل.</span>
            </div>
        `;

        const error = document.createElement("div");
        error.className = "refund-v2-error";
        error.hidden = true;

        form.append(walletsWrap, fields, error);
        modal.body.appendChild(form);

        const phoneInput = qs('input[name="accountNumber"]', form);
        const beneficiaryInput = qs('input[name="beneficiaryName"]', form);
        const iti = initInternationalPhone(phoneInput, currentAccount);

        const cancel = document.createElement("button");
        cancel.type = "button";
        cancel.className = "refund-v2-btn is-light";
        cancel.textContent = "إلغاء";

        const save = document.createElement("button");
        save.type = "button";
        save.className = "refund-v2-btn is-primary";
        save.innerHTML = `<i class="bi bi-check2-circle"></i> حفظ حساب الاسترداد`;

        modal.footer.append(cancel, save);
        cancel.addEventListener("click", modal.close);

        save.addEventListener("click", async () => {
            setError(error);

            const walletId = asInt(
                qs('input[name="refundWallet"]:checked', form)?.value
            );

            if (walletId <= 0) {
                setError(error, "اختر المحفظة التي تريد استلام المبلغ عليها.");
                walletsWrap.scrollIntoView({
                    behavior: reduceMotion() ? "auto" : "smooth",
                    block: "nearest"
                });
                return;
            }

            const phone = phoneIsAcceptable(phoneInput, iti);
            if (!phone.ok) {
                setError(error, phone.message);
                phoneInput.focus();
                return;
            }

            const beneficiaryName = clean(beneficiaryInput.value);
            if (beneficiaryName.length < 2) {
                setError(error, "أدخل اسم المستفيد كما هو مسجل في المحفظة.");
                beneficiaryInput.focus();
                return;
            }

            try {
                setBusy(save, true, "جاري الحفظ...");

                const antiForgery = token();
                const result = await requestJson(saveUrl, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json; charset=UTF-8",
                        ...(antiForgery
                            ? { RequestVerificationToken: antiForgery }
                            : {})
                    },
                    body: JSON.stringify({
                        RefundRequestId: requestId,
                        RefundWalletId: walletId,
                        AccountNumber: phone.value,
                        BeneficiaryName: beneficiaryName
                    })
                });

                context.dataset.refundWalletId = String(result?.walletId ?? walletId);
                context.dataset.refundAccount = clean(result?.accountNumber ?? phone.value);
                context.dataset.refundBeneficiary = clean(result?.beneficiaryName ?? beneficiaryName);

                notify(
                    result?.message || "تم حفظ حساب استلام الاسترداد.",
                    "success"
                );

                modal.close();

                window.setTimeout(() => {
                    window.location.reload();
                }, 220);
            } catch (errorRequest) {
                setError(
                    error,
                    errorRequest?.message || "تعذر حفظ حساب الاسترداد."
                );
            } finally {
                setBusy(save, false);
            }
        });
    }

    async function openConfirm(context) {
        const requestId = asInt(context.dataset.refundRequestId);
        const confirmUrl = clean(context.dataset.refundConfirmUrl);
        const amountText = clean(context.dataset.refundAmountText);

        if (requestId <= 0 || !confirmUrl) {
            notify("تعذر قراءة بيانات طلب الاسترداد.", "error");
            return;
        }

        const modal = createModal({
            eyebrow: "تأكيد الاستلام",
            title: "هل وصل المبلغ إلى حسابك؟",
            description: "لا تؤكد إلا بعد التحقق من وصول المبلغ فعليًا.",
            icon: "bi-shield-check",
            mode: "confirm"
        });

        modal.body.appendChild(amountBanner(amountText));

        const warning = document.createElement("div");
        warning.className = "refund-v2-confirm";
        warning.innerHTML = `
            <span><i class="bi bi-exclamation-triangle"></i></span>
            <div>
                <strong>تأكد من رصيدك أو رسالة التحويل أولًا</strong>
                <p>بعد التأكيد سيغلق طلب الاسترداد كعملية مكتملة.</p>
            </div>
        `;
        modal.body.appendChild(warning);

        const cancel = document.createElement("button");
        cancel.type = "button";
        cancel.className = "refund-v2-btn is-light";
        cancel.textContent = "ليس بعد";

        const confirm = document.createElement("button");
        confirm.type = "button";
        confirm.className = "refund-v2-btn is-success";
        confirm.innerHTML = `<i class="bi bi-patch-check"></i> نعم، استلمت المبلغ`;

        modal.footer.append(cancel, confirm);
        cancel.addEventListener("click", modal.close);

        confirm.addEventListener("click", async () => {
            try {
                setBusy(confirm, true, "جاري التأكيد...");

                const antiForgery = token();
                const result = await requestJson(confirmUrl, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json; charset=UTF-8",
                        ...(antiForgery
                            ? { RequestVerificationToken: antiForgery }
                            : {})
                    },
                    body: JSON.stringify({
                        RefundRequestId: requestId
                    })
                });

                notify(
                    result?.message || "تم تأكيد استلام مبلغ الاسترداد.",
                    "success"
                );

                modal.close();

                window.setTimeout(() => {
                    window.location.reload();
                }, 220);
            } catch (error) {
                notify(
                    error?.message || "تعذر تأكيد الاستلام.",
                    "error"
                );
            } finally {
                setBusy(confirm, false);
            }
        });
    }

    document.addEventListener("click", event => {
        const button = event.target.closest?.("[data-refund-action]");
        if (!button) return;

        const context = button.closest(selector);
        if (!context) return;

        const action = clean(button.dataset.refundAction);
        if (action !== "destination" && action !== "confirm") return;

        event.preventDefault();
        event.stopPropagation();

        if (action === "destination") {
            void openDestination(context);
        } else {
            void openConfirm(context);
        }
    });
})();

