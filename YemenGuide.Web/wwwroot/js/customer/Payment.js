/**
 * ============================================================================
 * YEMENGUIDE — PAYMENT
 * إدارة اختيار نوع الدفع، مؤقت الحجز، المحفظة، رفع الحوالة وإرسالها للمراجعة
 * ============================================================================
 */

(() => {
    "use strict";

    const MAX_FILE_SIZE =
        5 * 1024 * 1024;

    const ALLOWED_FILE_TYPES =
        new Set([
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/webp"
        ]);

    const ALLOWED_EXTENSIONS =
        new Set([
            "jpg",
            "jpeg",
            "png",
            "webp"
        ]);

    const EXPIRE_PAYMENT_ENDPOINT =
        "/BookingFlow/ExpirePayment";

    const PAYMENT_TYPE_DEPOSIT =
        "Deposit";

    const PAYMENT_TYPE_FULL =
        "Full";

    const state = {
        initialized: false,
        submitting: false,
        expired: false,
        expiryRequestSent: false,
        timerInterval: null,
        previewUrl: "",
        selectedWalletId: 0,
        selectedWalletName: "",
        selectedPaymentType: PAYMENT_TYPE_DEPOSIT
    };

    /* ======================================================================
       01. أدوات عامة
       ====================================================================== */

    const getElement = id =>
        document.getElementById(id);

    const getElements = selector =>
        Array.from(
            document.querySelectorAll(selector)
        );

    function toInteger(
        value,
        fallback = 0
    ) {
        const parsed =
            Number.parseInt(
                String(value ?? ""),
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
        const parsed =
            Number.parseFloat(
                String(value ?? "")
                    .replace(/,/g, "")
            );

        return Number.isFinite(parsed)
            ? parsed
            : fallback;
    }

    function formatMoney(value) {
        return Math.round(
            toNumber(value, 0)
        ).toLocaleString("en-US");
    }

    function formatTwoDigits(value) {
        return String(
            Math.max(
                0,
                toInteger(value, 0)
            )
        ).padStart(2, "0");
    }

    function createPaymentLuxuryDialog(options = {}) {
        return new Promise(resolve => {
            const oldOverlay = document.querySelector(".payment-lux-dialog-overlay");
            oldOverlay?.remove();

            const overlay = document.createElement("div");
            overlay.className = "payment-lux-dialog-overlay";

            const iconMap = {
                success: "bi-check2-circle",
                error: "bi-x-octagon",
                warning: "bi-exclamation-triangle",
                info: "bi-info-circle",
                question: "bi-question-circle"
            };

            const icon = iconMap[String(options.icon || "info")] || iconMap.info;
            const title = String(options.title || "تنبيه");
            const message = String(options.text || "");
            const showCancel = Boolean(options.showCancelButton);

            overlay.innerHTML = `
                <section class="payment-lux-dialog" role="dialog" aria-modal="true">
                    <span class="payment-lux-dialog-icon"><i class="bi ${icon}"></i></span>
                    <h3></h3>
                    <p></p>
                    <div class="payment-lux-dialog-actions">
                        ${showCancel ? '<button type="button" class="payment-lux-dialog-cancel"></button>' : ""}
                        <button type="button" class="payment-lux-dialog-confirm"></button>
                    </div>
                </section>`;

            overlay.querySelector("h3").textContent = title;
            overlay.querySelector("p").textContent = message;
            const confirm = overlay.querySelector(".payment-lux-dialog-confirm");
            const cancel = overlay.querySelector(".payment-lux-dialog-cancel");
            confirm.textContent = String(options.confirmButtonText || "حسنًا");
            if (cancel) cancel.textContent = String(options.cancelButtonText || "إلغاء");

            const done = isConfirmed => {
                overlay.remove();
                resolve({ isConfirmed, isDismissed: !isConfirmed });
            };

            confirm.addEventListener("click", () => done(true), { once: true });
            cancel?.addEventListener("click", () => done(false), { once: true });
            overlay.addEventListener("click", event => {
                if (event.target === overlay && options.allowOutsideClick !== false) done(false);
            });
            document.body.appendChild(overlay);
            confirm.focus();
        });
    }

    function showMessage(options = {}) {
        if (
            typeof window.Swal !== "undefined" &&
            typeof window.Swal.fire === "function"
        ) {
            const customClass = options.customClass || {};
            return window.Swal.fire({
                ...options,
                buttonsStyling: false,
                customClass: {
                    ...customClass,
                    popup: [customClass.popup, "payment-lux-swal"].filter(Boolean).join(" "),
                    confirmButton: [customClass.confirmButton, "payment-lux-swal-confirm"].filter(Boolean).join(" "),
                    cancelButton: [customClass.cancelButton, "payment-lux-swal-cancel"].filter(Boolean).join(" ")
                }
            });
        }

        return createPaymentLuxuryDialog(options);
    }

    function getAntiForgeryToken() {
        return document.querySelector(
            '#paymentProofForm input[name="__RequestVerificationToken"]'
        )?.value || "";
    }

    function scrollToElement(element) {
        if (!element) {
            return;
        }

        element.scrollIntoView({
            behavior: "smooth",
            block: "center"
        });
    }

    function clearValidationErrors() {
        const paymentTypeCard =
            document.querySelector(
                ".payment-type-card"
            );

        const walletsCard =
            document.querySelector(
                ".payment-wallets-card"
            );

        const proofCard =
            document.querySelector(
                ".payment-proof-card"
            );

        const uploadZone =
            getElement(
                "paymentUploadZone"
            );

        const proofError =
            getElement(
                "paymentProofError"
            );

        paymentTypeCard?.classList.remove(
            "has-validation-error"
        );

        walletsCard?.classList.remove(
            "has-validation-error"
        );

        proofCard?.classList.remove(
            "has-validation-error"
        );

        uploadZone?.classList.remove(
            "has-error"
        );

        proofError?.classList.add(
            "d-none"
        );
    }

    function initializeLuxuryPaymentMotion() {
        if (!window.matchMedia?.("(hover:hover) and (pointer:fine)").matches) return;

        document.querySelectorAll(
            ".payment-promotion-card, .payment-card, .payment-summary-card, .payment-instructions-card, .payment-support-card"
        ).forEach(card => {
            if (card.dataset.luxuryMotionBound === "true") return;
            card.dataset.luxuryMotionBound = "true";
            card.addEventListener("pointermove", event => {
                const rect = card.getBoundingClientRect();
                const x = ((event.clientX - rect.left) / Math.max(1, rect.width)) * 100;
                const y = ((event.clientY - rect.top) / Math.max(1, rect.height)) * 100;
                card.style.setProperty("--pay-mx", `${x}%`);
                card.style.setProperty("--pay-my", `${y}%`);
            }, { passive: true });
        });
    }

    /* ======================================================================
       02. تهيئة الصفحة
       ====================================================================== */

    function initializePaymentPage() {
        if (state.initialized) {
            return;
        }

        const page =
            getElement("paymentPage");

        if (!page) {
            return;
        }

        state.initialized = true;

        initializePaymentTypeSelection();
        initializeWalletSelection();
        initializeCopyButtons();
        initializeUploadZone();
        initializePaymentForm();
        initializeCountdown();
        applyCurrentBookingState();
        initializeLuxuryPaymentMotion();
    }

    if (
        document.readyState ===
        "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            initializePaymentPage,
            {
                once: true
            }
        );
    } else {
        initializePaymentPage();
    }

    /* ======================================================================
       03. اختيار طريقة الدفع
       ====================================================================== */

    function normalizePaymentType(value) {
        const normalized =
            String(value || "")
                .trim()
                .toLowerCase();

        if (
            normalized ===
            PAYMENT_TYPE_FULL
                .toLowerCase()
        ) {
            return PAYMENT_TYPE_FULL;
        }

        return PAYMENT_TYPE_DEPOSIT;
    }

    function getPaymentPresentation(type) {
        const page =
            getElement(
                "paymentPage"
            );

        const normalizedType =
            normalizePaymentType(type);

        const isFull =
            normalizedType ===
            PAYMENT_TYPE_FULL;

        return {
            type: normalizedType,
            isFull,
            label:
                isFull
                    ? "كامل قيمة الحجز"
                    : "العربون",
            actionLabel:
                isFull
                    ? "دفع كامل الحجز"
                    : "دفع العربون",
            amountText:
                String(
                    isFull
                        ? page?.dataset
                            .totalPriceText
                        : page?.dataset
                            .depositAmountText
                        || ""
                ).trim(),
            remainingText:
                String(
                    isFull
                        ? page?.dataset
                            .zeroAmountText
                        : page?.dataset
                            .remainingAmountText
                        || ""
                ).trim()
        };
    }

    function initializePaymentTypeSelection() {
        const page =
            getElement(
                "paymentPage"
            );

        const radios =
            getElements(
                'input[name="paymentTypeChoice"]'
            );

        if (
            !page ||
            !radios.length
        ) {
            return;
        }

        const initialType =
            normalizePaymentType(
                page.dataset
                    .initialPaymentType
            );

        const initialRadio =
            radios.find(
                radio =>
                    normalizePaymentType(
                        radio.value
                    ) ===
                    initialType
            ) ||
            radios[0];

        initialRadio.checked =
            true;

        radios.forEach(radio => {
            radio.addEventListener(
                "change",
                () => {
                    if (!radio.checked) {
                        return;
                    }

                    applyPaymentType(
                        radio.value,
                        {
                            resetConfirmation:
                                true
                        }
                    );
                }
            );
        });

        applyPaymentType(
            initialRadio.value,
            {
                resetConfirmation:
                    false
            }
        );
    }

    function setElementText(
        id,
        value
    ) {
        const element =
            getElement(id);

        if (element) {
            element.textContent =
                String(value ?? "");
        }
    }

    function applyPaymentType(
        type,
        options = {}
    ) {
        const presentation =
            getPaymentPresentation(type);

        state.selectedPaymentType =
            presentation.type;

        const hiddenInput =
            getElement(
                "onlinePaymentTypeInput"
            );

        if (hiddenInput) {
            hiddenInput.value =
                presentation.type;
        }

        const page =
            getElement(
                "paymentPage"
            );

        if (page) {
            page.dataset
                .selectedPaymentType =
                presentation.type;
        }

        setElementText(
            "paymentSelectedTypeTitle",
            presentation.isFull
                ? "كامل قيمة الحجز"
                : "قيمة العربون"
        );

        setElementText(
            "paymentSelectedTypeBadge",
            presentation.isFull
                ? "دفع كامل"
                : "مطلوب للتأكيد"
        );

        setElementText(
            "paymentSelectedAmount",
            presentation.amountText
        );

        setElementText(
            "paymentSelectedAmountHint",
            presentation.isFull
                ? "بعد اعتماد الحوالة لن يتبقى عليك أي مبلغ للدفع لدى المنشأة."
                : "بعد اعتماد العربون يتبقى عليك المبلغ المتبقي، ويتم تحصيله لدى المنشأة."
        );

        setElementText(
            "paymentSecurityTitle",
            "راجع قيمة الحوالة قبل الإرسال"
        );

        setElementText(
            "paymentSecurityText",
            `يجب أن يظهر في إشعار الحوالة مبلغ ${presentation.amountText} بالضبط.`
        );

        setElementText(
            "paymentSummaryPayNowLabel",
            presentation.isFull
                ? "المبلغ المطلوب الآن"
                : "العربون المطلوب الآن"
        );

        setElementText(
            "paymentSummaryPayNowAmount",
            presentation.amountText
        );

        setElementText(
            "paymentSummaryRemainingAmount",
            presentation.remainingText
        );

        setElementText(
            "paymentSummaryStateText",
            presentation.isFull
                ? "بانتظار إثبات دفع كامل الحجز"
                : "بانتظار إكمال الدفع"
        );

        setElementText(
            "paymentInstructionAmountText",
            presentation.isFull
                ? `حوّل كامل قيمة الحجز (${presentation.amountText}) دون زيادة أو نقص.`
                : `حوّل مبلغ العربون (${presentation.amountText}) دون زيادة أو نقص.`
        );

        setElementText(
            "paymentConfirmationTitle",
            presentation.isFull
                ? "أؤكد أنني حوّلت كامل قيمة الحجز"
                : "أؤكد أنني حوّلت مبلغ العربون كاملًا"
        );

        setElementText(
            "paymentConfirmationAmount",
            presentation.amountText
        );

        setElementText(
            "paymentConfirmationAfterText",
            presentation.isFull
                ? "كاملًا إلى الحساب الذي اخترته، وأن صورة الحوالة صحيحة."
                : "كعربون إلى الحساب الذي اخترته، وأن صورة الحوالة صحيحة."
        );

        const paymentTypeCard =
            document.querySelector(
                ".payment-type-card"
            );

        paymentTypeCard?.classList.remove(
            "has-validation-error"
        );

        if (
            options
                .resetConfirmation
        ) {
            const confirmation =
                getElement(
                    "paymentConfirmationInput"
                );

            if (confirmation) {
                confirmation.checked =
                    false;
            }
        }
    }

    /* ======================================================================
       03. اختيار المحفظة
       ====================================================================== */

    function initializeWalletSelection() {
        const walletRadios =
            getElements(
                'input[name="OwnerWalletId"]'
            );

        const paymentMethodInput =
            getElement(
                "selectedPaymentMethodInput"
            );

        walletRadios.forEach(radio => {
            radio.addEventListener(
                "change",
                () => {
                    if (!radio.checked) {
                        return;
                    }

                    state.selectedWalletId =
                        toInteger(
                            radio.value,
                            0
                        );

                    state.selectedWalletName =
                        String(
                            radio.dataset
                                .walletName ||
                            ""
                        ).trim();

                    if (
                        paymentMethodInput
                    ) {
                        paymentMethodInput.value =
                            state.selectedWalletName;
                    }

                    document
                        .querySelector(
                            ".payment-wallets-card"
                        )
                        ?.classList
                        .remove(
                            "has-validation-error"
                        );
                }
            );
        });

        const checkedWallet =
            walletRadios.find(
                radio =>
                    radio.checked
            );

        if (checkedWallet) {
            checkedWallet.dispatchEvent(
                new Event(
                    "change",
                    {
                        bubbles: true
                    }
                )
            );
        }
    }

    /* ======================================================================
       04. نسخ رقم الحساب
       ====================================================================== */

    function initializeCopyButtons() {
        const buttons =
            getElements(
                ".payment-copy-button"
            );

        buttons.forEach(button => {
            button.addEventListener(
                "click",
                async event => {
                    event.preventDefault();

                    const value =
                        String(
                            button.dataset
                                .copyValue ||
                            ""
                        ).trim();

                    if (!value) {
                        await showMessage({
                            icon: "warning",
                            title:
                                "تعذر النسخ",
                            text:
                                "رقم الحساب غير متوفر.",
                            confirmButtonColor:
                                "#00A6DA",
                            confirmButtonText:
                                "حسنًا"
                        });

                        return;
                    }

                    const copied =
                        await copyText(
                            value
                        );

                    if (!copied) {
                        await showMessage({
                            icon: "error",
                            title:
                                "تعذر النسخ",
                            text:
                                "انسخي رقم الحساب يدويًا.",
                            confirmButtonColor:
                                "#00A6DA",
                            confirmButtonText:
                                "حسنًا"
                        });

                        return;
                    }

                    markButtonAsCopied(
                        button
                    );
                }
            );
        });
    }

    async function copyText(value) {
        try {
            if (
                navigator.clipboard &&
                window.isSecureContext
            ) {
                await navigator
                    .clipboard
                    .writeText(value);

                return true;
            }

            const temporaryInput =
                document.createElement(
                    "textarea"
                );

            temporaryInput.value =
                value;

            temporaryInput.setAttribute(
                "readonly",
                ""
            );

            temporaryInput.style.position =
                "fixed";

            temporaryInput.style.opacity =
                "0";

            document.body.appendChild(
                temporaryInput
            );

            temporaryInput.select();

            const copied =
                document.execCommand(
                    "copy"
                );

            temporaryInput.remove();

            return copied;
        } catch (error) {
            console.error(
                "تعذر نسخ رقم الحساب:",
                error
            );

            return false;
        }
    }

    function markButtonAsCopied(button) {
        const originalHtml =
            button.innerHTML;

        button.classList.add(
            "is-copied"
        );

        button.innerHTML = `
            <i class="bi bi-check2-circle"></i>
            <span>تم النسخ</span>
        `;

        window.setTimeout(
            () => {
                button.classList.remove(
                    "is-copied"
                );

                button.innerHTML =
                    originalHtml;
            },
            1800
        );
    }

    /* ======================================================================
       05. رفع ومعاينة صورة الحوالة
       ====================================================================== */

    function initializeUploadZone() {
        const uploadZone =
            getElement(
                "paymentUploadZone"
            );

        const fileInput =
            getElement(
                "paymentProofInput"
            );

        const removeButton =
            getElement(
                "removePaymentProofButton"
            );

        if (
            !uploadZone ||
            !fileInput
        ) {
            return;
        }

        fileInput.addEventListener(
            "change",
            () => {
                const file =
                    fileInput.files?.[0];

                if (!file) {
                    clearPaymentProof();
                    return;
                }

                handleSelectedFile(
                    file
                );
            }
        );

        uploadZone.addEventListener(
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

                fileInput.click();
            }
        );

        [
            "dragenter",
            "dragover"
        ].forEach(eventName => {
            uploadZone.addEventListener(
                eventName,
                event => {
                    event.preventDefault();
                    event.stopPropagation();

                    if (
                        state.expired ||
                        state.submitting
                    ) {
                        return;
                    }

                    uploadZone.classList.add(
                        "is-dragging"
                    );
                }
            );
        });

        [
            "dragleave",
            "drop"
        ].forEach(eventName => {
            uploadZone.addEventListener(
                eventName,
                event => {
                    event.preventDefault();
                    event.stopPropagation();

                    uploadZone.classList.remove(
                        "is-dragging"
                    );
                }
            );
        });

        uploadZone.addEventListener(
            "drop",
            event => {
                if (
                    state.expired ||
                    state.submitting
                ) {
                    return;
                }

                const file =
                    event.dataTransfer
                        ?.files?.[0];

                if (!file) {
                    return;
                }

                try {
                    const transfer =
                        new DataTransfer();

                    transfer.items.add(
                        file
                    );

                    fileInput.files =
                        transfer.files;
                } catch {
                    // بعض المتصفحات لا تسمح
                    // بتعديل FileList يدويًا.
                }

                handleSelectedFile(
                    file
                );
            }
        );

        removeButton?.addEventListener(
            "click",
            event => {
                event.preventDefault();
                event.stopPropagation();

                clearPaymentProof();
            }
        );
    }

    function handleSelectedFile(file) {
        const validation =
            validatePaymentFile(
                file
            );

        if (!validation.valid) {
            clearPaymentProof();

            showPaymentProofError(
                validation.message
            );

            return;
        }

        hidePaymentProofError();
        showPaymentProofPreview(
            file
        );
    }

    function validatePaymentFile(file) {
        if (!file) {
            return {
                valid: false,
                message:
                    "يرجى اختيار صورة إشعار الحوالة."
            };
        }

        const extension =
            String(
                file.name || ""
            )
                .split(".")
                .pop()
                ?.toLowerCase() ||
            "";

        const validType =
            ALLOWED_FILE_TYPES.has(
                String(
                    file.type || ""
                ).toLowerCase()
            ) ||
            ALLOWED_EXTENSIONS.has(
                extension
            );

        if (!validType) {
            return {
                valid: false,
                message:
                    "صيغة الصورة غير مدعومة. استخدمي JPG أو PNG أو WEBP."
            };
        }

        if (file.size <= 0) {
            return {
                valid: false,
                message:
                    "الملف المختار فارغ أو غير صالح."
            };
        }

        if (
            file.size >
            MAX_FILE_SIZE
        ) {
            return {
                valid: false,
                message:
                    "حجم الصورة أكبر من 5 ميجابايت."
            };
        }

        return {
            valid: true,
            message: ""
        };
    }

    function showPaymentProofPreview(file) {
        const placeholder =
            getElement(
                "paymentUploadPlaceholder"
            );

        const preview =
            getElement(
                "paymentProofPreview"
            );

        const image =
            getElement(
                "paymentProofPreviewImage"
            );

        const fileName =
            getElement(
                "paymentProofFileName"
            );

        const fileSize =
            getElement(
                "paymentProofFileSize"
            );

        const fileInput =
            getElement(
                "paymentProofInput"
            );

        revokePreviewUrl();

        state.previewUrl =
            URL.createObjectURL(
                file
            );

        if (image) {
            image.src =
                state.previewUrl;
        }

        if (fileName) {
            fileName.textContent =
                file.name ||
                "إشعار الحوالة";
        }

        if (fileSize) {
            fileSize.textContent =
                formatFileSize(
                    file.size
                );
        }

        placeholder?.classList.add(
            "d-none"
        );

        preview?.classList.remove(
            "d-none"
        );

        if (fileInput) {
            fileInput.style.pointerEvents =
                "none";
        }

        getElement(
            "paymentUploadZone"
        )?.classList.remove(
            "has-error"
        );

        document
            .querySelector(
                ".payment-proof-card"
            )
            ?.classList
            .remove(
                "has-validation-error"
            );
    }

    function clearPaymentProof() {
        const fileInput =
            getElement(
                "paymentProofInput"
            );

        const placeholder =
            getElement(
                "paymentUploadPlaceholder"
            );

        const preview =
            getElement(
                "paymentProofPreview"
            );

        const image =
            getElement(
                "paymentProofPreviewImage"
            );

        if (fileInput) {
            fileInput.value = "";
            fileInput.style.pointerEvents =
                "auto";
        }

        if (image) {
            image.src = "";
        }

        preview?.classList.add(
            "d-none"
        );

        placeholder?.classList.remove(
            "d-none"
        );

        revokePreviewUrl();
    }

    function revokePreviewUrl() {
        if (!state.previewUrl) {
            return;
        }

        URL.revokeObjectURL(
            state.previewUrl
        );

        state.previewUrl = "";
    }

    function formatFileSize(bytes) {
        const numericBytes =
            Math.max(
                0,
                toNumber(bytes, 0)
            );

        if (
            numericBytes <
            1024
        ) {
            return `${numericBytes} بايت`;
        }

        if (
            numericBytes <
            1024 * 1024
        ) {
            return `${(
                numericBytes /
                1024
            ).toFixed(1)
                } كيلوبايت`;
        }

        return `${(
            numericBytes /
            (
                1024 *
                1024
            )
        ).toFixed(2)
            } ميجابايت`;
    }

    function showPaymentProofError(message) {
        const errorElement =
            getElement(
                "paymentProofError"
            );

        const uploadZone =
            getElement(
                "paymentUploadZone"
            );

        const proofCard =
            document.querySelector(
                ".payment-proof-card"
            );

        if (errorElement) {
            const textElement =
                errorElement.querySelector(
                    "span"
                );

            if (textElement) {
                textElement.textContent =
                    message;
            }

            errorElement.classList.remove(
                "d-none"
            );
        }

        uploadZone?.classList.add(
            "has-error"
        );

        proofCard?.classList.add(
            "has-validation-error"
        );
    }

    function hidePaymentProofError() {
        getElement(
            "paymentProofError"
        )?.classList.add(
            "d-none"
        );

        getElement(
            "paymentUploadZone"
        )?.classList.remove(
            "has-error"
        );
    }

    /* ======================================================================
       06. مؤقت انتهاء الدفع
       ====================================================================== */

    function initializeCountdown() {
        const page =
            getElement(
                "paymentPage"
            );

        if (!page) {
            return;
        }

        const expireAtText =
            String(
                page.dataset
                    .expireAt ||
                ""
            ).trim();

        if (!expireAtText) {
            updateCountdownDisplay(
                0,
                0,
                0
            );

            return;
        }

        const expireAt =
            new Date(
                expireAtText
            );

        if (
            Number.isNaN(
                expireAt.getTime()
            )
        ) {
            console.warn(
                "وقت انتهاء الدفع غير صالح:",
                expireAtText
            );

            return;
        }

        const updateTimer =
            () => {
                const remaining =
                    expireAt.getTime() -
                    Date.now();

                if (remaining <= 0) {
                    updateCountdownDisplay(
                        0,
                        0,
                        0
                    );

                    expirePaymentPage();
                    return;
                }

                const totalSeconds =
                    Math.floor(
                        remaining /
                        1000
                    );

                const hours =
                    Math.floor(
                        totalSeconds /
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

                updateCountdownDisplay(
                    hours,
                    minutes,
                    seconds
                );
            };

        updateTimer();

        state.timerInterval =
            window.setInterval(
                updateTimer,
                1000
            );
    }

    function updateCountdownDisplay(
        hours,
        minutes,
        seconds
    ) {
        const hoursElement =
            getElement(
                "countdownHours"
            );

        const minutesElement =
            getElement(
                "countdownMinutes"
            );

        const secondsElement =
            getElement(
                "countdownSeconds"
            );

        if (hoursElement) {
            hoursElement.textContent =
                formatTwoDigits(
                    hours
                );
        }

        if (minutesElement) {
            minutesElement.textContent =
                formatTwoDigits(
                    minutes
                );
        }

        if (secondsElement) {
            secondsElement.textContent =
                formatTwoDigits(
                    seconds
                );
        }
    }

    function expirePaymentPage() {
        if (state.expired) {
            return;
        }

        state.expired = true;

        if (
            state.timerInterval
        ) {
            window.clearInterval(
                state.timerInterval
            );

            state.timerInterval =
                null;
        }

        disablePaymentForm();

        getElement(
            "paymentExpiredOverlay"
        )?.classList.remove(
            "d-none"
        );

        document.body.style.overflow =
            "hidden";

        notifyServerAboutExpiration();
    }

    async function notifyServerAboutExpiration() {
        if (state.expiryRequestSent) {
            return;
        }

        const bookingId =
            getCurrentBookingId();

        if (bookingId <= 0) {
            return;
        }

        state.expiryRequestSent =
            true;

        const token =
            getAntiForgeryToken();

        const headers = {
            "X-Requested-With":
                "XMLHttpRequest"
        };

        if (token) {
            headers.RequestVerificationToken =
                token;
        }

        const formData =
            new FormData();

        formData.append(
            "bookingId",
            String(bookingId)
        );

        try {
            const response =
                await fetch(
                    EXPIRE_PAYMENT_ENDPOINT,
                    {
                        method:
                            "POST",

                        credentials:
                            "same-origin",

                        cache:
                            "no-store",

                        headers,

                        body:
                            formData
                    }
                );

            if (!response.ok) {
                console.warn(
                    "تعذر تحديث انتهاء مهلة الدفع في الخادم.",
                    response.status
                );
            }
        } catch (error) {
            /*
             * تبقى الصفحة منتهية محليًا. سيعيد الخادم تنفيذ التحرير
             * عند فتح صفحة الدفع أو محاولة إرسال الإثبات لاحقًا.
             */
            console.warn(
                "تعذر الاتصال بالخادم لتحرير الغرف المنتهية.",
                error
            );
        }
    }

    function disablePaymentForm() {
        const form =
            getElement(
                "paymentProofForm"
            );

        if (!form) {
            return;
        }

        form
            .querySelectorAll(
                "input, button"
            )
            .forEach(element => {
                element.disabled = true;
            });

        getElements(
            'input[name="paymentTypeChoice"]'
        ).forEach(element => {
            element.disabled = true;
        });
    }

    /* ======================================================================
       07. حالة الحجز الحالية
       ====================================================================== */

    function applyCurrentBookingState() {
        const page =
            getElement(
                "paymentPage"
            );

        if (!page) {
            return;
        }

        const bookingState =
            String(
                page.dataset
                    .bookingState ||
                ""
            )
                .trim()
                .toLowerCase();

        const paymentStatus =
            String(
                page.dataset
                    .paymentStatus ||
                ""
            )
                .trim()
                .toLowerCase();

        const paymentIsUnderReview =
            bookingState ===
            "paymentreview" ||
            paymentStatus ===
            "underreview";

        const paymentIsCompleted =
            bookingState ===
            "confirmed" ||
            paymentStatus ===
            "paid";

        if (
            paymentIsUnderReview
        ) {
            disablePaymentForm();

            showMessage({
                icon: "info",
                title:
                    "الحوالة قيد المراجعة",
                text:
                    "تم إرسال إشعار الحوالة مسبقًا، وهو الآن بانتظار مراجعة مالك المنشأة.",
                confirmButtonColor:
                    "#00A6DA",
                confirmButtonText:
                    "عرض تفاصيل الحجز",
                allowOutsideClick:
                    false
            }).then(() => {
                redirectToBookingDetails();
            });

            return;
        }

        if (paymentIsCompleted) {
            disablePaymentForm();

            showMessage({
                icon: "success",
                title:
                    "تم اعتماد الدفع",
                text:
                    "تم اعتماد الدفع وتأكيد الحجز.",
                confirmButtonColor:
                    "#00A6DA",
                confirmButtonText:
                    "عرض تفاصيل الحجز",
                allowOutsideClick:
                    false
            }).then(() => {
                redirectToBookingDetails();
            });
        }
    }

    /* ======================================================================
       08. التحقق من النموذج
       ====================================================================== */

    function validatePaymentForm() {
        clearValidationErrors();

        if (state.expired) {
            return {
                valid: false,
                message:
                    "انتهت مهلة الدفع لهذا الحجز."
            };
        }

        const typeRadios =
            getElements(
                'input[name="paymentTypeChoice"]'
            );

        const checkedType =
            typeRadios.find(
                radio =>
                    radio.checked
            );

        if (!checkedType) {
            const paymentTypeCard =
                document.querySelector(
                    ".payment-type-card"
                );

            paymentTypeCard?.classList.add(
                "has-validation-error"
            );

            scrollToElement(
                paymentTypeCard
            );

            return {
                valid: false,
                message:
                    "اختر دفع العربون أو كامل قيمة الحجز."
            };
        }

        applyPaymentType(
            checkedType.value,
            {
                resetConfirmation:
                    false
            }
        );

        const walletRadios =
            getElements(
                'input[name="OwnerWalletId"]'
            );

        const checkedWallet =
            walletRadios.find(
                radio =>
                    radio.checked
            );

        if (!walletRadios.length) {
            return {
                valid: false,
                message:
                    "لا توجد محفظة استقبال متاحة لهذه المنشأة."
            };
        }

        if (!checkedWallet) {
            const walletsCard =
                document.querySelector(
                    ".payment-wallets-card"
                );

            walletsCard?.classList.add(
                "has-validation-error"
            );

            scrollToElement(
                walletsCard
            );

            return {
                valid: false,
                message:
                    "اختر محفظة الاستقبال أولًا."
            };
        }

        const fileInput =
            getElement(
                "paymentProofInput"
            );

        const file =
            fileInput?.files?.[0];

        const fileValidation =
            validatePaymentFile(
                file
            );

        if (
            !fileValidation.valid
        ) {
            showPaymentProofError(
                fileValidation.message
            );

            scrollToElement(
                getElement(
                    "paymentUploadZone"
                )
            );

            return fileValidation;
        }

        const confirmation =
            getElement(
                "paymentConfirmationInput"
            );

        if (!confirmation?.checked) {
            const proofCard =
                document.querySelector(
                    ".payment-proof-card"
                );

            proofCard?.classList.add(
                "has-validation-error"
            );

            scrollToElement(
                confirmation
                    ?.closest(
                        ".payment-confirmation-box"
                    ) ||
                proofCard
            );

            return {
                valid: false,
                message:
                    "يجب تأكيد صحة بيانات الحوالة قبل الإرسال."
            };
        }

        return {
            valid: true,
            message: ""
        };
    }

    /* ======================================================================
       09. إرسال الحوالة إلى الخادم
       ====================================================================== */

    function initializePaymentForm() {
        const form =
            getElement(
                "paymentProofForm"
            );

        if (!form) {
            return;
        }

        form.addEventListener(
            "submit",
            async event => {
                event.preventDefault();

                if (
                    state.submitting
                ) {
                    return;
                }

                const validation =
                    validatePaymentForm();

                if (
                    !validation.valid
                ) {
                    await showMessage({
                        icon: "warning",
                        title:
                            "تعذر إرسال الحوالة",
                        text:
                            validation.message,
                        confirmButtonColor:
                            "#00A6DA",
                        confirmButtonText:
                            "حسنًا"
                    });

                    return;
                }

                const presentation =
                    getPaymentPresentation(
                        state.selectedPaymentType
                    );

                const confirmation =
                    await showMessage({
                        icon: "question",
                        title:
                            "تأكيد إرسال إثبات الدفع",
                        text:
                            `أنت ترسل إثبات ${presentation.actionLabel} بمبلغ ${presentation.amountText}. تأكد أن المبلغ الظاهر في الحوالة مطابق تمامًا قبل المتابعة.`,
                        showCancelButton:
                            true,
                        confirmButtonText:
                            "نعم، إرسال للمراجعة",
                        cancelButtonText:
                            "مراجعة الحوالة",
                        allowOutsideClick:
                            false
                    });

                if (
                    !confirmation
                        ?.isConfirmed
                ) {
                    return;
                }

                await submitPaymentProof(
                    form
                );
            }
        );
    }

    async function submitPaymentProof(form) {
        const button =
            getElement(
                "submitPaymentProofButton"
            );

        const selectedWallet =
            document.querySelector(
                'input[name="OwnerWalletId"]:checked'
            );

        const paymentMethodInput =
            getElement(
                "selectedPaymentMethodInput"
            );

        const selectedWalletName =
            String(
                selectedWallet
                    ?.dataset
                    .walletName ||
                ""
            ).trim();

        if (
            paymentMethodInput
        ) {
            paymentMethodInput.value =
                selectedWalletName;
        }

        state.submitting = true;

        setSubmitButtonLoading(
            true
        );

        try {
            const formData =
                new FormData(
                    form
                );

            formData.set(
                "OwnerWalletId",
                selectedWallet?.value || ""
            );

            formData.set(
                "OnlinePaymentType",
                state.selectedPaymentType
            );

            const token =
                getAntiForgeryToken();

            const headers = {
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
                    form.action,
                    {
                        method: "POST",
                        body: formData,
                        headers,
                        credentials:
                            "same-origin"
                    }
                );

            const data =
                await readServerResponse(
                    response
                );

            if (!data.success) {
                throw new Error(
                    data.message ||
                    "فشل إرسال إشعار الحوالة."
                );
            }

            const bookingId =
                toInteger(
                    data.bookingId,
                    getCurrentBookingId()
                );

            await showMessage({
                icon: "success",
                title:
                    "تم إرسال إثبات الدفع",
                text:
                    data.message ||
                    "وصل إثبات الدفع وأصبح الآن قيد المراجعة.",
                confirmButtonColor:
                    "#00A6DA",
                confirmButtonText:
                    "عرض تفاصيل الحجز",
                allowOutsideClick:
                    false,
                allowEscapeKey:
                    false
            });

            const redirectUrl =
                String(
                    data.redirectUrl ||
                    ""
                ).trim();

            if (redirectUrl) {
                window.location.href =
                    redirectUrl;

                return;
            }

            window.location.href =
                `/Places/BookingDetails/${bookingId}`;
        } catch (error) {
            console.error(
                "فشل إرسال الحوالة:",
                error
            );

            await showMessage({
                icon: "error",
                title:
                    "تعذر إرسال الحوالة",
                text:
                    error.message ||
                    "حدث خطأ أثناء الاتصال بالخادم.",
                confirmButtonColor:
                    "#00A6DA",
                confirmButtonText:
                    "حسنًا"
            });
        } finally {
            state.submitting = false;

            if (!state.expired) {
                setSubmitButtonLoading(
                    false
                );
            }
        }
    }

    async function readServerResponse(response) {
        if (
            response.redirected &&
            /login|account/i.test(
                response.url
            )
        ) {
            return {
                success: false,
                message:
                    "انتهت جلسة تسجيل الدخول. سجلي الدخول مرة أخرى."
            };
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
            try {
                data =
                    await response.json();
            } catch {
                data = null;
            }
        } else {
            const text =
                await response.text();

            try {
                data =
                    JSON.parse(text);
            } catch {
                data = {
                    success: false,
                    message:
                        response.ok
                            ? "أعاد الخادم استجابة غير متوقعة."
                            : "فشل إرسال الحوالة إلى الخادم."
                };
            }
        }

        if (!response.ok) {
            return {
                ...(data || {}),
                success: false,
                message:
                    data?.message ||
                    `تعذر إرسال الحوالة (${response.status}).`
            };
        }

        return data || {
            success: false,
            message:
                "لم تصل نتيجة واضحة من الخادم."
        };
    }

    /* ======================================================================
       10. حالة زر الإرسال
       ====================================================================== */

    function setSubmitButtonLoading(
        loading
    ) {
        const button =
            getElement(
                "submitPaymentProofButton"
            );

        if (!button) {
            return;
        }

        const title =
            button.querySelector(
                ".payment-submit-copy strong"
            );

        const subtitle =
            button.querySelector(
                ".payment-submit-copy small"
            );

        if (loading) {
            button.dataset
                .originalTitle =
                title?.textContent ||
                "";

            button.dataset
                .originalSubtitle =
                subtitle?.textContent ||
                "";

            if (title) {
                title.textContent =
                    "جارٍ إرسال الحوالة...";
            }

            if (subtitle) {
                subtitle.textContent =
                    "يرجى عدم إغلاق الصفحة";
            }

            button.classList.add(
                "is-loading"
            );

            button.disabled =
                true;

            button.setAttribute(
                "aria-busy",
                "true"
            );
        } else {
            if (title) {
                title.textContent =
                    button.dataset
                        .originalTitle ||
                    "إرسال الحوالة للمراجعة";
            }

            if (subtitle) {
                subtitle.textContent =
                    button.dataset
                        .originalSubtitle ||
                    "سيتم إشعار مالك المنشأة فورًا";
            }

            delete button.dataset
                .originalTitle;

            delete button.dataset
                .originalSubtitle;

            button.classList.remove(
                "is-loading"
            );

            button.disabled =
                false;

            button.setAttribute(
                "aria-busy",
                "false"
            );
        }
    }

    /* ======================================================================
       11. التحويل لتفاصيل الحجز
       ====================================================================== */

    function getCurrentBookingId() {
        return toInteger(
            getElement(
                "paymentPage"
            )?.dataset
                .bookingId,
            0
        );
    }

    function redirectToBookingDetails() {
        const bookingId =
            getCurrentBookingId();

        if (bookingId < 1) {
            return;
        }

        window.location.href =
            `/Places/BookingDetails/${bookingId}`;
    }

    /* ======================================================================
       12. تنظيف الموارد
       ====================================================================== */

    window.addEventListener(
        "beforeunload",
        () => {
            if (
                state.timerInterval
            ) {
                window.clearInterval(
                    state.timerInterval
                );
            }

            revokePreviewUrl();
        }
    );
})();
