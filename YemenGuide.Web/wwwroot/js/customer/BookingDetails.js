/**
 * ============================================================================
 * دليلك اليمني — تفاصيل الحجز
 * تنفيذ إلغاء الحجز من صفحة التفاصيل فقط.
 *
 * المسار:
 * wwwroot/js/customer/BookingDetails.js
 *
 * يعتمد على العناصر:
 * #bookingCancellationPanel
 * #btnCancelOrder
 * #bookingCancellationAntiForgeryForm
 * ============================================================================
 */

(() => {
    "use strict";

    const state = {
        initialized: false,
        cancelling: false
    };

    const byId = id =>
        document.getElementById(id);

    /* =========================================================================
       1. أدوات عامة
       ========================================================================= */

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

    function toNumber(
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
            if (lastComma > lastDot) {
                text =
                    text
                        .replace(/\./g, "")
                        .replace(",", ".");
            } else {
                text =
                    text.replace(/,/g, "");
            }
        } else if (lastComma >= 0) {
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
            Math.max(
                0,
                toNumber(value)
            )
        ).toLocaleString(
            "en-US"
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

    function readProperty(
        source,
        camelCaseName,
        pascalCaseName,
        fallback = null
    ) {
        if (!source) {
            return fallback;
        }

        if (
            Object.prototype
                .hasOwnProperty.call(
                    source,
                    camelCaseName
                )
        ) {
            return source[
                camelCaseName
            ];
        }

        if (
            pascalCaseName &&
            Object.prototype
                .hasOwnProperty.call(
                    source,
                    pascalCaseName
                )
        ) {
            return source[
                pascalCaseName
            ];
        }

        return fallback;
    }

    function hasSweetAlert() {
        return Boolean(
            window.Swal &&
            typeof window.Swal.fire ===
            "function"
        );
    }

    function getCancellationPanel() {
        return byId(
            "bookingCancellationPanel"
        );
    }

    function getCancelButton() {
        return byId(
            "btnCancelOrder"
        );
    }

    function getAntiForgeryToken() {
        return byId(
            "bookingCancellationAntiForgeryForm"
        )
            ?.querySelector(
                'input[name="__RequestVerificationToken"]'
            )
            ?.value || "";
    }

    function getBookingId(button) {
        return toInteger(
            button?.dataset.bookingId ||
            getCancellationPanel()
                ?.dataset.bookingId ||
            "0"
        );
    }

    function getQuoteUrl(button) {
        return String(
            button?.dataset.quoteUrl ||
            getCancellationPanel()
                ?.dataset.quoteUrl ||
            ""
        ).trim();
    }

    function getCancelUrl(button) {
        return String(
            button?.dataset.cancelUrl ||
            getCancellationPanel()
                ?.dataset.cancelUrl ||
            ""
        ).trim();
    }

    function getPlaceName(button) {
        return String(
            button?.dataset.placeName ||
            "المنشأة"
        ).trim() || "المنشأة";
    }

    /* =========================================================================
       2. نظام الرسائل الفخم — مستقل ولا يعتمد على SweetAlert
       ========================================================================= */

    function ensureLuxuryDialog() {
        let overlay = byId("bdLuxuryDialogOverlay");
        if (overlay) return overlay;

        overlay = document.createElement("div");
        overlay.id = "bdLuxuryDialogOverlay";
        overlay.className = "bd-lux-dialog-overlay";
        overlay.hidden = true;
        overlay.setAttribute("aria-hidden", "true");
        overlay.innerHTML = `
            <div class="bd-lux-dialog" role="dialog" aria-modal="true" aria-labelledby="bdLuxuryDialogTitle">
                <button type="button" class="bd-lux-dialog-close" data-bd-dialog-close aria-label="إغلاق">
                    <i class="bi bi-x-lg"></i>
                </button>
                <div class="bd-lux-dialog-aura" aria-hidden="true"></div>
                <div class="bd-lux-dialog-icon" data-bd-dialog-icon>
                    <i class="bi bi-stars"></i>
                </div>
                <div class="bd-lux-dialog-heading">
                    <small data-bd-dialog-kicker>دليلك اليمني</small>
                    <h2 id="bdLuxuryDialogTitle" data-bd-dialog-title>تنبيه</h2>
                </div>
                <div class="bd-lux-dialog-body" data-bd-dialog-body></div>
                <div class="bd-lux-dialog-input-wrap" data-bd-dialog-input-wrap hidden>
                    <label for="bdLuxuryDialogInput" data-bd-dialog-input-label>اكتب التفاصيل</label>
                    <textarea id="bdLuxuryDialogInput" maxlength="500" rows="4" data-bd-dialog-input></textarea>
                    <div class="bd-lux-dialog-input-meta">
                        <span data-bd-dialog-validation></span>
                        <small><b data-bd-dialog-count>0</b> / 500</small>
                    </div>
                </div>
                <div class="bd-lux-dialog-actions">
                    <button type="button" class="bd-lux-dialog-btn is-secondary" data-bd-dialog-cancel>تراجع</button>
                    <button type="button" class="bd-lux-dialog-btn is-primary" data-bd-dialog-confirm>تأكيد</button>
                </div>
            </div>`;

        document.body.appendChild(overlay);
        return overlay;
    }

    function getDialogIcon(icon) {
        const map = {
            success: "bi-check2-circle",
            error: "bi-x-octagon-fill",
            warning: "bi-exclamation-triangle-fill",
            question: "bi-question-circle-fill",
            info: "bi-info-circle-fill"
        };
        return map[icon] || "bi-stars";
    }

    function showLuxuryDialog(options = {}) {
        const overlay = ensureLuxuryDialog();
        const dialog = overlay.querySelector(".bd-lux-dialog");
        const title = overlay.querySelector("[data-bd-dialog-title]");
        const kicker = overlay.querySelector("[data-bd-dialog-kicker]");
        const body = overlay.querySelector("[data-bd-dialog-body]");
        const iconWrap = overlay.querySelector("[data-bd-dialog-icon]");
        const confirm = overlay.querySelector("[data-bd-dialog-confirm]");
        const cancel = overlay.querySelector("[data-bd-dialog-cancel]");
        const close = overlay.querySelector("[data-bd-dialog-close]");
        const inputWrap = overlay.querySelector("[data-bd-dialog-input-wrap]");
        const input = overlay.querySelector("[data-bd-dialog-input]");
        const inputLabel = overlay.querySelector("[data-bd-dialog-input-label]");
        const validation = overlay.querySelector("[data-bd-dialog-validation]");
        const count = overlay.querySelector("[data-bd-dialog-count]");
        const icon = options.icon || "info";
        const wantsInput = options.input === "textarea";
        const showCancel = Boolean(options.showCancelButton);
        const allowOutsideClick = options.allowOutsideClick !== false;
        const allowEscapeKey = options.allowEscapeKey !== false;
        const minLength = Number(options.minLength || 0);
        const maxLength = Number(options.maxLength || 500);

        title.textContent = options.title || "تنبيه";
        kicker.textContent = options.kicker || "دليلك اليمني";
        body.innerHTML = options.html || (options.text ? `<p>${escapeHtml(options.text)}</p>` : "");
        iconWrap.className = `bd-lux-dialog-icon is-${icon}`;
        iconWrap.innerHTML = `<i class="bi ${getDialogIcon(icon)}"></i>`;
        confirm.textContent = options.confirmButtonText || "حسنًا";
        cancel.textContent = options.cancelButtonText || "تراجع";
        cancel.hidden = !showCancel;
        close.hidden = options.showCloseButton === false;
        inputWrap.hidden = !wantsInput;
        validation.textContent = "";
        dialog.className = `bd-lux-dialog is-${icon}`;

        if (wantsInput) {
            input.value = options.inputValue || "";
            input.placeholder = options.inputPlaceholder || "اكتب هنا...";
            input.maxLength = maxLength;
            inputLabel.textContent = options.inputLabel || "اكتب التفاصيل";
            count.textContent = String(input.value.length);
        }

        overlay.hidden = false;
        overlay.setAttribute("aria-hidden", "false");
        document.body.classList.add("bd-lux-dialog-open");
        requestAnimationFrame(() => overlay.classList.add("is-open"));

        return new Promise(resolve => {
            let settled = false;

            const cleanup = (result) => {
                if (settled) return;
                settled = true;
                overlay.classList.remove("is-open");
                document.body.classList.remove("bd-lux-dialog-open");
                document.removeEventListener("keydown", onKeyDown);
                confirm.removeEventListener("click", onConfirm);
                cancel.removeEventListener("click", onCancel);
                close.removeEventListener("click", onCancel);
                overlay.removeEventListener("click", onOverlayClick);
                input.removeEventListener("input", onInput);
                window.setTimeout(() => {
                    overlay.hidden = true;
                    overlay.setAttribute("aria-hidden", "true");
                    resolve(result);
                }, 180);
            };

            const onInput = () => {
                count.textContent = String(input.value.length);
                validation.textContent = "";
                inputWrap.classList.remove("has-error");
            };

            const onConfirm = () => {
                const value = wantsInput ? String(input.value || "").trim() : null;
                if (wantsInput && value.length < minLength) {
                    validation.textContent = `اكتب سببًا واضحًا لا يقل عن ${minLength} أحرف.`;
                    inputWrap.classList.add("has-error");
                    input.focus();
                    return;
                }
                if (wantsInput && value.length > maxLength) {
                    validation.textContent = `النص لا يمكن أن يتجاوز ${maxLength} حرفًا.`;
                    inputWrap.classList.add("has-error");
                    input.focus();
                    return;
                }
                cleanup({ isConfirmed: true, value });
            };

            const onCancel = () => cleanup({ isConfirmed: false, value: null });
            const onOverlayClick = (event) => {
                if (allowOutsideClick && event.target === overlay) onCancel();
            };
            const onKeyDown = (event) => {
                if (event.key === "Escape" && allowEscapeKey) onCancel();
            };

            confirm.addEventListener("click", onConfirm);
            cancel.addEventListener("click", onCancel);
            close.addEventListener("click", onCancel);
            overlay.addEventListener("click", onOverlayClick);
            document.addEventListener("keydown", onKeyDown);
            input.addEventListener("input", onInput);

            window.setTimeout(() => {
                if (wantsInput) input.focus();
                else confirm.focus();
            }, 90);
        });
    }

    window.YGBookingLuxuryDialog = showLuxuryDialog;

    function showLuxuryToast(message, icon = "success") {
        let toast = document.querySelector(".bd-lux-toast");
        if (!toast) {
            toast = document.createElement("div");
            toast.className = "bd-lux-toast";
            toast.innerHTML = `<span data-bd-toast-icon></span><strong data-bd-toast-text></strong>`;
            document.body.appendChild(toast);
        }

        const iconHost = toast.querySelector("[data-bd-toast-icon]");
        const textHost = toast.querySelector("[data-bd-toast-text]");
        iconHost.innerHTML = `<i class="bi ${getDialogIcon(icon)}"></i>`;
        toast.className = `bd-lux-toast is-${icon}`;
        textHost.textContent = String(message || "تمت العملية بنجاح");
        requestAnimationFrame(() => toast.classList.add("is-visible"));
        window.clearTimeout(showLuxuryToast._timer);
        showLuxuryToast._timer = window.setTimeout(() => toast.classList.remove("is-visible"), 2100);
    }

    window.YGBookingLuxuryToast = showLuxuryToast;

    async function showMessage(options) {
        return showLuxuryDialog(options);
    }

    async function showError(title, message) {
        return showLuxuryDialog({
            icon: "error",
            title,
            html: `<div class="bd-lux-message-card"><p>${escapeHtml(message || "حدث خطأ غير متوقع.")}</p></div>`,
            confirmButtonText: "حسنًا"
        });
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

        const contentType =
            response.headers.get(
                "content-type"
            ) || "";

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
                    text
                        ? JSON.parse(text)
                        : {};
            } catch {
                data = {
                    success: false,
                    message:
                        "استجاب الخادم بصيغة غير متوقعة."
                };
            }
        }

        if (!response.ok) {
            throw new Error(
                readProperty(
                    data,
                    "message",
                    "Message",
                    "تعذر تنفيذ العملية على الخادم."
                )
            );
        }

        return data || {};
    }

    /* =========================================================================
       3. حالة زر الإلغاء
       ========================================================================= */

    function setCancelButtonBusy(
        button,
        busy,
        title =
            "جارٍ التحقق...",
        subtitle =
            "يرجى الانتظار لحظات"
    ) {
        if (!button) {
            return;
        }

        if (busy) {
            if (
                !button.dataset
                    .originalHtml
            ) {
                button.dataset
                    .originalHtml =
                    button.innerHTML;
            }

            button.disabled = true;

            button.setAttribute(
                "aria-busy",
                "true"
            );

            button.classList.add(
                "is-loading"
            );

            const icon =
                button.querySelector(
                    ".bd2-cancel-button-icon i"
                );

            const titleElement =
                button.querySelector(
                    ".bd2-cancel-button-copy strong"
                );

            const subtitleElement =
                button.querySelector(
                    ".bd2-cancel-button-copy small"
                );

            const arrow =
                button.querySelector(
                    ".bd2-cancel-button-arrow"
                );

            if (icon) {
                icon.className =
                    "spinner-border spinner-border-sm";
            }

            if (titleElement) {
                titleElement.textContent =
                    title;
            }

            if (subtitleElement) {
                subtitleElement.textContent =
                    subtitle;
            }

            arrow?.classList.add(
                "d-none"
            );

            return;
        }

        button.disabled = false;

        button.setAttribute(
            "aria-busy",
            "false"
        );

        button.classList.remove(
            "is-loading"
        );

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

    /* =========================================================================
       4. جلب ملخص سياسة الإلغاء
       ========================================================================= */

    async function fetchCancellationQuote(
        url
    ) {
        const response =
            await fetch(
                url,
                {
                    method:
                        "GET",

                    credentials:
                        "same-origin",

                    cache:
                        "no-store",

                    headers: {
                        Accept:
                            "application/json",

                        "X-Requested-With":
                            "XMLHttpRequest"
                    }
                }
            );

        const result =
            await parseJsonResponse(
                response
            );

        const success =
            Boolean(
                readProperty(
                    result,
                    "success",
                    "Success",
                    false
                )
            );

        const quote =
            readProperty(
                result,
                "data",
                "Data",
                null
            );

        const canCancel =
            Boolean(
                readProperty(
                    quote,
                    "canCancel",
                    "CanCancel",
                    false
                )
            );

        if (
            !success ||
            !quote ||
            !canCancel
        ) {
            throw new Error(
                readProperty(
                    result,
                    "message",
                    "Message",
                    readProperty(
                        quote,
                        "message",
                        "Message",
                        "لا يمكن إلغاء هذا الحجز حاليًا."
                    )
                )
            );
        }

        return quote;
    }

    /* =========================================================================
       5. بناء واجهة ملخص الإلغاء
       ========================================================================= */

    function buildMoneyRow(
        label,
        value,
        cssClass = "",
        displayText = ""
    ) {
        const safeMoneyText =
            String(displayText || "").trim() ||
            `${formatMoney(value)} ر.ي`;

        return `
            <div class="bd-cancel-summary-row">

                <span>
                    ${escapeHtml(label)}
                </span>

                <strong class="${escapeHtml(cssClass)}">
                    ${escapeHtml(safeMoneyText)}
                </strong>

            </div>
        `;
    }

    function buildCancellationQuoteHtml(
        quote
    ) {
        let policyLabel = String(
            readProperty(
                quote,
                "policyLabel",
                "PolicyLabel",
                "سياسة الإلغاء المحفوظة لدى المنشأة"
            ) || ""
        ).trim();

        let customerRefundMessage = String(
            readProperty(
                quote,
                "customerRefundMessage",
                "CustomerRefundMessage",
                ""
            ) || ""
        ).trim();

        const depositPaid = Math.max(
            0,
            toNumber(readProperty(quote, "depositPaid", "DepositPaid", 0))
        );

        const cancellationFee = Math.max(
            0,
            toNumber(readProperty(quote, "cancellationFee", "CancellationFee", 0))
        );

        const refundAmount = Math.max(
            0,
            toNumber(readProperty(quote, "refundAmount", "RefundAmount", 0))
        );


        const depositPaidText = String(
            readProperty(quote, "depositPaidText", "DepositPaidText", "") || ""
        ).trim();

        const cancellationFeeText = String(
            readProperty(quote, "cancellationFeeText", "CancellationFeeText", "") || ""
        ).trim();

        const refundAmountText = String(
            readProperty(quote, "refundAmountText", "RefundAmountText", "") || ""
        ).trim();


        const penaltyValueText = String(
            readProperty(quote, "penaltyValueText", "PenaltyValueText", "") || ""
        ).trim();

        if (penaltyValueText && /ريال|ر\.ي/.test(policyLabel)) {
            policyLabel = `تُطبق غرامة إلغاء بقيمة ${penaltyValueText}`;
        }

        if (
            refundAmountText &&
            /ريال|ر\.ي/.test(customerRefundMessage)
        ) {
            customerRefundMessage =
                `المبلغ المتوقع استرداده ${refundAmountText} وفق سياسة المنشأة.`;
        }

        const refundProcessingDays = Math.max(
            0,
            toInteger(readProperty(quote, "refundProcessingDays", "RefundProcessingDays", 0))
        );

        const refundTransferFeeLabel = String(
            readProperty(
                quote,
                "refundTransferFeeLabel",
                "RefundTransferFeeLabel",
                ""
            ) || ""
        ).trim();

        const requiresRefundRequest = Boolean(
            readProperty(quote, "requiresRefundRequest", "RequiresRefundRequest", false)
        );

        const isPaymentUnderReview = Boolean(
            readProperty(quote, "isPaymentUnderReview", "IsPaymentUnderReview", false)
        );

        const isFreeCancellation = Boolean(
            readProperty(quote, "isFreeCancellation", "IsFreeCancellation", false)
        );

        const rows = [];

        if (depositPaid > 0) {
            rows.push(buildMoneyRow("المبلغ المدفوع إلكترونيًا", depositPaid, "", depositPaidText));
        }

        if (cancellationFee > 0) {
            rows.push(buildMoneyRow("غرامة الإلغاء", cancellationFee, "text-danger", cancellationFeeText));
        }

        if (requiresRefundRequest || refundAmount > 0) {
            rows.push(buildMoneyRow("المبلغ المتوقع استرداده", refundAmount, "text-success", refundAmountText));
        }

        const noPaidMoney = depositPaid <= 0 && cancellationFee <= 0 && refundAmount <= 0;

        return `
            <div class="bd-cancel-dialog" dir="rtl">
                <div class="bd-cancel-policy-badge ${isFreeCancellation ? "is-free" : "is-penalty"}">
                    <i class="bi ${isFreeCancellation ? "bi-shield-check" : "bi-exclamation-triangle"}"></i>
                    <span>${escapeHtml(policyLabel)}</span>
                </div>

                ${rows.length
                ? `<div class="bd-cancel-summary-box">${rows.join("")}</div>`
                : ""
            }

                ${noPaidMoney
                ? `
                        <div class="bd-cancel-empty-money">
                            <i class="bi bi-wallet2"></i>
                            <span>لا يوجد مبلغ مدفوع يحتاج إلى استرداد.</span>
                        </div>
                    `
                : ""
            }

                ${isPaymentUnderReview
                ? `
                        <div class="bd-cancel-review-note">
                            <i class="bi bi-file-earmark-check"></i>
                            <span>إثبات الدفع ما زال قيد المراجعة وسيتم احتساب الاسترداد بعد التحقق.</span>
                        </div>
                    `
                : ""
            }

                ${(requiresRefundRequest && (refundProcessingDays > 0 || refundTransferFeeLabel))
                ? `
                        <div class="bd-cancel-refund-meta">
                            ${refundProcessingDays > 0
                    ? `<span><i class="bi bi-clock-history"></i> المعالجة: ${refundProcessingDays} ${refundProcessingDays === 1 ? "يوم عمل" : "أيام عمل"}</span>`
                    : ""
                }
                            ${refundTransferFeeLabel
                    ? `<span><i class="bi bi-credit-card"></i> ${escapeHtml(refundTransferFeeLabel)}</span>`
                    : ""
                }
                        </div>
                    `
                : ""
            }

                ${(!noPaidMoney && customerRefundMessage)
                ? `<p class="bd-cancel-customer-message">${escapeHtml(customerRefundMessage)}</p>`
                : ""
            }
            </div>
        `;
    }

    /* =========================================================================
       6. طلب سبب الإلغاء
       ========================================================================= */

    async function requestCancellationReason(
        placeName,
        quote
    ) {
        const result = await showLuxuryDialog({
            icon: "warning",
            kicker: "مراجعة سياسة الإلغاء",
            title: "هل تريد إلغاء الحجز؟",
            html: `
                <p class="bd-cancel-place-line">
                    سيتم إلغاء حجز <strong>${escapeHtml(placeName)}</strong> بعد تأكيدك النهائي.
                </p>
                ${buildCancellationQuoteHtml(quote)}
            `,
            input: "textarea",
            inputLabel: "سبب الإلغاء",
            inputPlaceholder: "اكتب سبب الإلغاء بوضوح...",
            minLength: 5,
            maxLength: 500,
            showCancelButton: true,
            confirmButtonText: "تأكيد الإلغاء",
            cancelButtonText: "الاحتفاظ بالحجز",
            allowOutsideClick: false,
            allowEscapeKey: true
        });

        if (!result.isConfirmed) return null;
        return String(result.value || "").trim();
    }

    /* =========================================================================
       7. إرسال طلب الإلغاء
       ========================================================================= */

    async function submitCancellation(
        url,
        bookingId,
        reason
    ) {
        const token =
            getAntiForgeryToken();

        if (!token) {
            throw new Error(
                "تعذر العثور على رمز الحماية. حدّث الصفحة ثم حاول مرة أخرى."
            );
        }

        const formData =
            new FormData();

        formData.append(
            "BookingId",
            String(bookingId)
        );

        formData.append(
            "Reason",
            reason
        );

        formData.append(
            "__RequestVerificationToken",
            token
        );

        const response =
            await fetch(
                url,
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

        const result =
            await parseJsonResponse(
                response
            );

        const success =
            Boolean(
                readProperty(
                    result,
                    "success",
                    "Success",
                    false
                )
            );

        if (!success) {
            throw new Error(
                readProperty(
                    result,
                    "message",
                    "Message",
                    "تعذر إلغاء الحجز."
                )
            );
        }

        return result;
    }

    /* =========================================================================
       8. رسالة نجاح الإلغاء
       ========================================================================= */

    function buildCancellationSuccessHtml(
        result
    ) {
        const cancellationFee =
            Math.max(
                0,
                toNumber(
                    readProperty(
                        result,
                        "cancellationFee",
                        "CancellationFee",
                        0
                    )
                )
            );

        const refundAmount =
            Math.max(
                0,
                toNumber(
                    readProperty(
                        result,
                        "refundAmount",
                        "RefundAmount",
                        0
                    )
                )
            );


        const cancellationFeeText = String(
            readProperty(
                result,
                "cancellationFeeText",
                "CancellationFeeText",
                ""
            ) || ""
        ).trim();

        const refundAmountText = String(
            readProperty(
                result,
                "refundAmountText",
                "RefundAmountText",
                ""
            ) || ""
        ).trim();

        const refundStatus =
            String(
                readProperty(
                    result,
                    "refundStatus",
                    "RefundStatus",
                    ""
                ) || ""
            ).trim();

        let refundMessage =
            String(
                readProperty(
                    result,
                    "customerRefundMessage",
                    "CustomerRefundMessage",
                    ""
                ) || ""
            ).trim();


        if (
            refundAmountText &&
            /ريال|ر\.ي/.test(refundMessage)
        ) {
            refundMessage =
                `المبلغ المتوقع استرداده ${refundAmountText} وفق سياسة المنشأة.`;
        }

        const rows = [];

        if (cancellationFee > 0) {
            rows.push(
                buildMoneyRow(
                    "غرامة الإلغاء",
                    cancellationFee,
                    "text-danger",
                    cancellationFeeText
                )
            );
        }

        if (refundAmount > 0) {
            rows.push(
                buildMoneyRow(
                    "مبلغ الاسترداد",
                    refundAmount,
                    "text-success",
                    refundAmountText
                )
            );
        }

        let refundStatusLabel =
            "";

        if (
            refundStatus &&
            refundStatus.toLowerCase() !==
            "notapplicable"
        ) {
            refundStatusLabel =
                refundStatus.toLowerCase() ===
                    "pending"
                    ? "قيد المعالجة"
                    : "تمت المعالجة";
        }

        return `
            <div class="bd-cancel-success-content"
                 dir="rtl">

                ${rows.length > 0
                ? `
                            <div class="bd-cancel-summary-box">
                                ${rows.join("")}
                            </div>
                        `
                : ""
            }

                ${refundStatusLabel
                ? `
                            <div class="bd-cancel-success-refund">

                                <i class="bi bi-arrow-counterclockwise"></i>

                                <span>

                                    حالة الاسترداد:

                                    <strong>
                                        ${escapeHtml(refundStatusLabel)}
                                    </strong>

                                </span>

                            </div>
                        `
                : ""
            }

                ${refundMessage
                ? `
                            <p>
                                ${escapeHtml(refundMessage)}
                            </p>
                        `
                : ""
            }

            </div>
        `;
    }

    async function showCancellationSuccess(
        result
    ) {
        const message = String(
            readProperty(result, "message", "Message", "تم إلغاء الحجز بنجاح.") ||
            "تم إلغاء الحجز بنجاح."
        ).trim();

        await showLuxuryDialog({
            icon: "success",
            kicker: "تم تحديث الحجز",
            title: "تم إلغاء الحجز",
            html: `
                <div class="bd-lux-message-card is-success">
                    <p>${escapeHtml(message)}</p>
                </div>
                ${buildCancellationSuccessHtml(result)}
            `,
            confirmButtonText: "الذهاب إلى حجوزاتي",
            allowOutsideClick: false,
            allowEscapeKey: false,
            showCloseButton: false
        });
    }

    /* =========================================================================
       9. تنفيذ رحلة الإلغاء
       ========================================================================= */

    async function handleCancellation(
        button
    ) {
        if (
            !button ||
            state.cancelling
        ) {
            return;
        }

        const bookingId =
            getBookingId(button);

        const quoteUrl =
            getQuoteUrl(button);

        const cancelUrl =
            getCancelUrl(button);

        const placeName =
            getPlaceName(button);

        if (
            bookingId <= 0 ||
            !quoteUrl ||
            !cancelUrl
        ) {
            await showError(
                "بيانات غير مكتملة",
                "تعذر قراءة بيانات الإلغاء. حدّث الصفحة ثم حاول مرة أخرى."
            );

            return;
        }

        state.cancelling =
            true;

        setCancelButtonBusy(
            button,
            true,
            "جارٍ قراءة السياسة...",
            "يتم حساب الغرامة والاسترداد"
        );

        try {
            const quote =
                await fetchCancellationQuote(
                    quoteUrl
                );

            setCancelButtonBusy(
                button,
                false
            );

            const reason =
                await requestCancellationReason(
                    placeName,
                    quote
                );

            if (!reason) {
                return;
            }

            setCancelButtonBusy(
                button,
                true,
                "جارٍ إلغاء الحجز...",
                "يتم تحديث الحجز وطلب الاسترداد"
            );


            const result =
                await submitCancellation(
                    cancelUrl,
                    bookingId,
                    reason
                );


            await showCancellationSuccess(
                result
            );

            window.location.assign(
                "/Bookings/MyBookings"
            );
        } catch (error) {

            await showError(
                "تعذر إلغاء الحجز",
                error?.message ||
                "حدث خطأ أثناء إلغاء الحجز."
            );
        } finally {
            state.cancelling =
                false;

            if (
                document.body.contains(
                    button
                )
            ) {
                setCancelButtonBusy(
                    button,
                    false
                );
            }
        }
    }

    /* =========================================================================
       10. معالجة الصور المفقودة
       ========================================================================= */

    function initializeImageFallbacks() {
        document
            .querySelectorAll("img")
            .forEach(image => {
                if (
                    image.dataset
                        .bookingFallbackInitialized ===
                    "true"
                ) {
                    return;
                }

                image.dataset
                    .bookingFallbackInitialized =
                    "true";

                image.addEventListener(
                    "error",
                    () => {
                        const fallback =
                            "/images/bg1.jpg";

                        if (
                            !image.src.endsWith(
                                fallback
                            )
                        ) {
                            image.src =
                                fallback;
                        }
                    },
                    {
                        once: true
                    }
                );
            });
    }

    /* =========================================================================
       11. التجربة البصرية الفخمة للصفحة والرسائل
       ========================================================================= */

    function initializeLuxuryExperience() {


        const revealTargets =
            document.querySelectorAll(
                [
                    ".bd2-place-card",
                    ".bd3-booking-hero",
                    ".bd2-chat-card",
                    ".bd2-status-card",
                    ".bd3-timeline-card",
                    ".bd2-next-step",
                    ".bd2-lifecycle-alert",
                    ".bd2-closure-card",
                    ".bd2-refund-card",
                    ".bd2-card",
                    ".bd3-promotion-card",
                    ".bd2-finance",
                    ".bd2-cancellation-panel",
                    ".bd2-actions"
                ].join(",")
            );

        if (
            window.matchMedia(
                "(prefers-reduced-motion: reduce)"
            ).matches
        ) {
            revealTargets.forEach(element =>
                element.classList.add(
                    "is-visible"
                )
            );
        } else if (
            "IntersectionObserver" in window
        ) {
            const observer =
                new IntersectionObserver(
                    entries => {
                        entries.forEach(entry => {
                            if (!entry.isIntersecting) {
                                return;
                            }

                            entry.target.classList.add(
                                "is-visible"
                            );

                            observer.unobserve(
                                entry.target
                            );
                        });
                    },
                    {
                        threshold: 0.08,
                        rootMargin:
                            "0px 0px -35px 0px"
                    }
                );

            revealTargets.forEach((element, index) => {
                element.classList.add(
                    "bd3-reveal"
                );

                element.style.transitionDelay =
                    `${Math.min(index * 28, 180)}ms`;

                observer.observe(element);
            });
        } else {
            revealTargets.forEach(element =>
                element.classList.add(
                    "is-visible"
                )
            );
        }

        const canTrackPointer =
            window.matchMedia(
                "(hover: hover) and (pointer: fine)"
            ).matches;

        if (canTrackPointer) {
            document.querySelectorAll(
                ".bd2-place-card, .bd2-status-card, .bd2-next-step, .bd2-card, .bd2-finance, .bd2-cancellation-panel, .bd3-timeline-card, .bd3-promotion-card"
            ).forEach(card => {
                card.addEventListener(
                    "pointermove",
                    event => {
                        const rect =
                            card.getBoundingClientRect();

                        const x =
                            ((event.clientX - rect.left) /
                                Math.max(1, rect.width)) *
                            100;

                        const y =
                            ((event.clientY - rect.top) /
                                Math.max(1, rect.height)) *
                            100;

                        card.style.setProperty(
                            "--bd3-x",
                            `${x}%`
                        );

                        card.style.setProperty(
                            "--bd3-y",
                            `${y}%`
                        );
                    },
                    { passive: true }
                );
            });
        }

        document.querySelectorAll(
            "[data-copy-promotion-code]"
        ).forEach(button => {
            button.addEventListener(
                "click",
                async event => {
                    event.preventDefault();
                    event.stopPropagation();

                    const value =
                        String(
                            button.dataset
                                .copyPromotionCode ||
                            ""
                        ).trim();

                    if (!value) {
                        return;
                    }

                    try {
                        await navigator.clipboard
                            .writeText(value);

                        showLuxuryToast(`تم نسخ كود العرض: ${value}`, "success");
                    } catch {
                        // لا نعطل الصفحة إذا منع المتصفح Clipboard API.
                    }
                }
            );
        });
    }

    /* =========================================================================
       11. تهيئة زر الإلغاء
       ========================================================================= */

    function initializeCancellationButton() {
        const button =
            getCancelButton();

        if (
            !button ||
            button.dataset.initialized ===
            "true"
        ) {
            return;
        }

        button.dataset.initialized =
            "true";

        button.addEventListener(
            "click",
            event => {
                event.preventDefault();

                handleCancellation(
                    button
                );
            }
        );
    }

    /* =========================================================================
       12. تشغيل الصفحة
       ========================================================================= */

    function initialize() {
        if (state.initialized) {
            return;
        }

        state.initialized =
            true;

        initializeImageFallbacks();
        initializeLuxuryExperience();
        initializeCancellationButton();
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

