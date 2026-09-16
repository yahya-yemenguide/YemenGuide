/*
 * Promotion date/time repair (2026-09-16)
 * Restores a usable date + time picker for promotion start/end fields.
 * - Keeps full yyyy-MM-ddTHH:mm values in the hidden form fields.
 * - Prevents choosing a past start time.
 * - Prevents choosing an end time that is not after the start.
 * - Preserves an unchanged legacy start that is already in the past while editing.
 */
(function (window, document) {
    "use strict";

    const FORM_SELECTOR = "#promotionForm";
    const START_ID = "promotionStartDate";
    const END_ID = "promotionEndDate";
    const START_DISPLAY_ID = "promotionStartDateDisplay";
    const END_DISPLAY_ID = "promotionEndDateDisplay";
    const INSTALL_MARKER = "datetimeRepairInstalled";
    const MINUTE_MS = 60 * 1000;

    let pickerOverlay = null;

    function pad(value) {
        return String(value).padStart(2, "0");
    }

    function toLocalDateTimeValue(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) return "";
        return [
            date.getFullYear(),
            "-",
            pad(date.getMonth() + 1),
            "-",
            pad(date.getDate()),
            "T",
            pad(date.getHours()),
            ":",
            pad(date.getMinutes())
        ].join("");
    }

    function normalizeDateTimeValue(value) {
        const text = String(value || "").trim();
        if (!text) return "";

        const match = text.match(/^(\d{4})-(\d{2})-(\d{2})(?:[T\s](\d{2}):(\d{2}))?/);
        if (!match) return "";

        const hour = match[4] || "00";
        const minute = match[5] || "00";
        return `${match[1]}-${match[2]}-${match[3]}T${hour}:${minute}`;
    }

    function parseLocalDateTime(value) {
        const normalized = normalizeDateTimeValue(value);
        if (!normalized) return null;

        const match = normalized.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/);
        if (!match) return null;

        const date = new Date(
            Number(match[1]),
            Number(match[2]) - 1,
            Number(match[3]),
            Number(match[4]),
            Number(match[5]),
            0,
            0
        );

        return Number.isNaN(date.getTime()) ? null : date;
    }

    function ceilToMinute(date, extraMinutes) {
        const result = new Date(date.getTime());
        result.setSeconds(0, 0);
        if (date.getSeconds() > 0 || date.getMilliseconds() > 0) {
            result.setMinutes(result.getMinutes() + 1);
        }
        if (extraMinutes) result.setMinutes(result.getMinutes() + extraMinutes);
        return result;
    }

    function formatForDisplay(value) {
        const date = parseLocalDateTime(value);
        if (!date) return "";

        try {
            return new Intl.DateTimeFormat("en-US", {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit",
                hour12: true
            }).format(date);
        } catch {
            return normalizeDateTimeValue(value).replace("T", " ");
        }
    }

    function readPromotionItems(form) {
        const root = form.closest(".yg-promo-manager") || document.getElementById("promotionManager");
        const dataElement = root?.querySelector("#promotionManagerData") || document.getElementById("promotionManagerData");
        try {
            const value = JSON.parse(dataElement?.textContent || "[]");
            return Array.isArray(value) ? value : [];
        } catch {
            return [];
        }
    }

    function getPromotionItem(form) {
        const id = Number.parseInt(form.querySelector("#promotionId")?.value || "0", 10) || 0;
        if (id <= 0) return null;
        return readPromotionItems(form).find(item => Number(item?.promotionId || 0) === id) || null;
    }

    function setFieldValue(field, value) {
        if (!field) return;
        field.value = normalizeDateTimeValue(value);
        field.dispatchEvent(new Event("input", { bubbles: true }));
        field.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function syncDisplay(form) {
        const start = form.querySelector(`#${START_ID}`);
        const end = form.querySelector(`#${END_ID}`);
        const startDisplay = form.querySelector(`#${START_DISPLAY_ID}`);
        const endDisplay = form.querySelector(`#${END_DISPLAY_ID}`);

        if (startDisplay) startDisplay.value = formatForDisplay(start?.value);
        if (endDisplay) endDisplay.value = formatForDisplay(end?.value);
    }

    function initializeDateValues(form) {
        const start = form.querySelector(`#${START_ID}`);
        const end = form.querySelector(`#${END_ID}`);
        if (!start || !end) return;

        const item = getPromotionItem(form);

        if (item) {
            const originalStart = normalizeDateTimeValue(item.startDate);
            const originalEnd = normalizeDateTimeValue(item.endDate);

            if (originalStart) {
                start.value = originalStart;
                form.dataset.originalPromotionStart = originalStart;
            }
            if (originalEnd) end.value = originalEnd;
        } else {
            const now = ceilToMinute(new Date(), 2);
            const defaultEnd = new Date(now.getTime());
            defaultEnd.setDate(defaultEnd.getDate() + 7);

            // promotions-manager.js currently writes date-only values. Replace those
            // defaults with full local date/time values for a new promotion.
            start.value = toLocalDateTimeValue(now);
            end.value = toLocalDateTimeValue(defaultEnd);
            form.dataset.originalPromotionStart = "";
        }

        syncDisplay(form);
    }

    function showFormAlert(form, message) {
        const alert = form.querySelector("#promotionFormAlert");
        if (!alert) {
            window.alert(message);
            return;
        }

        alert.textContent = message;
        alert.hidden = false;
        alert.scrollIntoView?.({ block: "nearest", behavior: "smooth" });
    }

    function hideFormAlert(form) {
        const alert = form.querySelector("#promotionFormAlert");
        if (!alert) return;
        alert.hidden = true;
        alert.textContent = "";
    }

    function isUnchangedLegacyPastStart(form, value, nowValue) {
        const original = normalizeDateTimeValue(form.dataset.originalPromotionStart || "");
        const current = normalizeDateTimeValue(value);
        return !!original && original === current && current < nowValue;
    }

    function validateDateTimes(form, focusField) {
        const start = normalizeDateTimeValue(form.querySelector(`#${START_ID}`)?.value);
        const end = normalizeDateTimeValue(form.querySelector(`#${END_ID}`)?.value);
        const nowValue = toLocalDateTimeValue(ceilToMinute(new Date(), 0));

        if (!start) {
            showFormAlert(form, "حدد تاريخ ووقت بداية العرض.");
            form.querySelector(`#${START_DISPLAY_ID}`)?.focus?.();
            return false;
        }

        if (!end) {
            showFormAlert(form, "حدد تاريخ ووقت نهاية العرض.");
            form.querySelector(`#${END_DISPLAY_ID}`)?.focus?.();
            return false;
        }

        if (start < nowValue && !isUnchangedLegacyPastStart(form, start, nowValue)) {
            showFormAlert(form, "بداية العرض لا يمكن أن تكون في وقت مضى.");
            form.querySelector(`#${START_DISPLAY_ID}`)?.focus?.();
            return false;
        }

        if (end <= start) {
            showFormAlert(form, "تاريخ ووقت انتهاء العرض يجب أن يكون بعد البداية.");
            form.querySelector(`#${END_DISPLAY_ID}`)?.focus?.();
            return false;
        }

        if (focusField === "end" && end < nowValue) {
            showFormAlert(form, "نهاية العرض لا يمكن أن تكون في وقت مضى.");
            form.querySelector(`#${END_DISPLAY_ID}`)?.focus?.();
            return false;
        }

        return true;
    }

    function closePicker() {
        pickerOverlay?.remove();
        pickerOverlay = null;
    }

    function ensurePickerStyles() {
        if (document.getElementById("ygPromotionDateTimeRepairStyles")) return;

        const style = document.createElement("style");
        style.id = "ygPromotionDateTimeRepairStyles";
        style.textContent = `
            .yg-promo-dt-repair-backdrop {
                position: fixed;
                inset: 0;
                z-index: 2147483600;
                display: grid;
                place-items: center;
                padding: 16px;
                background: rgba(8, 31, 49, .38);
                backdrop-filter: blur(2px);
            }
            .yg-promo-dt-repair {
                width: min(430px, calc(100vw - 28px));
                direction: rtl;
                padding: 18px;
                color: #30495c;
                background: #fff;
                border: 1px solid #dbe8ee;
                border-radius: 18px;
                box-shadow: 0 24px 70px rgba(11, 47, 70, .24);
            }
            .yg-promo-dt-repair h3 {
                margin: 0 0 5px;
                font-size: 1rem;
                font-weight: 900;
            }
            .yg-promo-dt-repair p {
                margin: 0 0 14px;
                color: #7b8f9e;
                font-size: .76rem;
                line-height: 1.7;
            }
            .yg-promo-dt-repair input[type="datetime-local"] {
                width: 100%;
                height: 48px;
                padding: 0 12px;
                direction: ltr;
                text-align: left;
                color: #2d4658;
                background: #fff;
                border: 1px solid #d8e6ed;
                border-radius: 12px;
                outline: none;
                font: 800 .9rem/1 Arial, "Segoe UI", sans-serif;
            }
            .yg-promo-dt-repair input[type="datetime-local"]:focus {
                border-color: #00a6da;
                box-shadow: 0 0 0 3px rgba(0, 166, 218, .09);
            }
            .yg-promo-dt-repair__error {
                min-height: 20px;
                margin-top: 8px;
                color: #c94d4d;
                font-size: .72rem;
                font-weight: 800;
            }
            .yg-promo-dt-repair__actions {
                display: flex;
                justify-content: flex-start;
                gap: 9px;
                margin-top: 12px;
            }
            .yg-promo-dt-repair__actions button {
                min-width: 92px;
                height: 42px;
                border-radius: 11px;
                font: 900 .78rem/1 inherit;
                cursor: pointer;
            }
            .yg-promo-dt-repair__apply {
                color: #fff;
                background: #00a6da;
                border: 1px solid #00a6da;
            }
            .yg-promo-dt-repair__cancel {
                color: #5e7382;
                background: #fff;
                border: 1px solid #dbe6ec;
            }
        `;
        document.head.appendChild(style);
    }

    function openPicker(form, kind) {
        closePicker();
        ensurePickerStyles();
        hideFormAlert(form);

        const isStart = kind === "start";
        const target = form.querySelector(isStart ? `#${START_ID}` : `#${END_ID}`);
        const start = form.querySelector(`#${START_ID}`);
        if (!target) return;

        const now = ceilToMinute(new Date(), 0);
        const nowValue = toLocalDateTimeValue(now);
        const current = normalizeDateTimeValue(target.value);
        let minimum = nowValue;

        if (!isStart) {
            const startValue = normalizeDateTimeValue(start?.value);
            if (startValue && startValue >= minimum) {
                const startDate = parseLocalDateTime(startValue);
                if (startDate) {
                    startDate.setMinutes(startDate.getMinutes() + 1);
                    minimum = toLocalDateTimeValue(startDate);
                }
            }
        }

        const backdrop = document.createElement("div");
        backdrop.className = "yg-promo-dt-repair-backdrop";
        backdrop.innerHTML = `
            <div class="yg-promo-dt-repair" role="dialog" aria-modal="true" aria-label="${isStart ? "بداية العرض" : "نهاية العرض"}">
                <h3>${isStart ? "تحديد بداية العرض" : "تحديد نهاية العرض"}</h3>
                <p>${isStart ? "اختر التاريخ والساعة التي يبدأ فيها العرض. لا يمكن اختيار وقت ماضٍ." : "اختر تاريخ ووقت انتهاء العرض، ويجب أن يكون بعد وقت البداية."}</p>
                <input type="datetime-local" step="60" />
                <div class="yg-promo-dt-repair__error" aria-live="polite"></div>
                <div class="yg-promo-dt-repair__actions">
                    <button type="button" class="yg-promo-dt-repair__apply">تطبيق</button>
                    <button type="button" class="yg-promo-dt-repair__cancel">إلغاء</button>
                </div>
            </div>
        `;

        const input = backdrop.querySelector('input[type="datetime-local"]');
        const error = backdrop.querySelector(".yg-promo-dt-repair__error");
        const apply = backdrop.querySelector(".yg-promo-dt-repair__apply");
        const cancel = backdrop.querySelector(".yg-promo-dt-repair__cancel");

        input.min = minimum;

        // An old promotion can have a past start. We keep it untouched until the
        // owner actively chooses a new value, then the minimum becomes "now".
        if (current && current >= minimum) {
            input.value = current;
        } else if (isStart && current && isUnchangedLegacyPastStart(form, current, nowValue)) {
            input.value = minimum;
        } else if (!isStart && current && current >= minimum) {
            input.value = current;
        } else {
            input.value = minimum;
        }

        function applyValue() {
            const value = normalizeDateTimeValue(input.value);
            if (!value) {
                error.textContent = "حدد التاريخ والوقت أولًا.";
                return;
            }

            const liveNow = toLocalDateTimeValue(ceilToMinute(new Date(), 0));

            if (isStart && value < liveNow) {
                error.textContent = "لا يمكن اختيار بداية عرض في وقت مضى.";
                input.min = liveNow;
                return;
            }

            if (!isStart) {
                const startValue = normalizeDateTimeValue(start?.value);
                if (startValue && value <= startValue) {
                    error.textContent = "نهاية العرض يجب أن تكون بعد البداية.";
                    return;
                }
                if (value < liveNow) {
                    error.textContent = "لا يمكن اختيار نهاية عرض في وقت مضى.";
                    return;
                }
            }

            setFieldValue(target, value);
            syncDisplay(form);
            hideFormAlert(form);
            closePicker();
        }

        apply.addEventListener("click", applyValue);
        cancel.addEventListener("click", closePicker);
        input.addEventListener("keydown", event => {
            if (event.key === "Enter") {
                event.preventDefault();
                applyValue();
            }
        });
        backdrop.addEventListener("click", event => {
            if (event.target === backdrop) closePicker();
        });

        document.body.appendChild(backdrop);
        pickerOverlay = backdrop;
        window.setTimeout(() => input.focus(), 0);
    }

    function installForm(form) {
        if (!(form instanceof HTMLFormElement) || form.dataset[INSTALL_MARKER] === "true") return;
        form.dataset[INSTALL_MARKER] = "true";

        // didOpen/resetForm/fillForm are synchronous. A queued task guarantees
        // that we repair the date-only values after the original manager finishes.
        window.setTimeout(() => initializeDateValues(form), 0);

        const startDisplay = form.querySelector(`#${START_DISPLAY_ID}`);
        const endDisplay = form.querySelector(`#${END_DISPLAY_ID}`);

        startDisplay?.addEventListener("click", () => openPicker(form, "start"));
        endDisplay?.addEventListener("click", () => openPicker(form, "end"));
        startDisplay?.addEventListener("keydown", event => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                openPicker(form, "start");
            }
        });
        endDisplay?.addEventListener("keydown", event => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                openPicker(form, "end");
            }
        });

        // Validate before the original click/submit handlers run.
        form.addEventListener("click", event => {
            const button = event.target?.closest?.(
                "#promotionNextStep, #savePromotionDraft, #publishPromotion"
            );
            if (!button) return;

            // Date rules matter only when the user is at / publishing through step 3.
            const step3 = form.querySelector('[data-promo-step="3"]');
            const isStep3Visible = step3?.classList.contains("is-active");
            if (button.id !== "publishPromotion" && button.id !== "savePromotionDraft" && !isStep3Visible) {
                return;
            }

            if (!validateDateTimes(form)) {
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        }, true);

        form.addEventListener("submit", event => {
            if (!validateDateTimes(form)) {
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        }, true);
    }

    function scan() {
        document.querySelectorAll(FORM_SELECTOR).forEach(installForm);
    }

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && pickerOverlay) {
            event.preventDefault();
            closePicker();
        }
    });

    const observer = new MutationObserver(scan);
    observer.observe(document.documentElement, { childList: true, subtree: true });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", scan);
    } else {
        scan();
    }
})(window, document);
