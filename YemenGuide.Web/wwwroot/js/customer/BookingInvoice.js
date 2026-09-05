/**
 * ============================================================================
 * YEMENGUIDE — BOOKING INVOICE
 * فتح وإغلاق فاتورة الحجز، الطباعة، المشاركة، والنسخ الاحتياطي
 * ============================================================================
 */

(() => {
    "use strict";

    const state = {
        initialized: false,
        isOpen: false,
        isPrinting: false,
        lastFocusedElement: null
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

    function showMessage(options) {
        if (typeof window.YGBookingLuxuryDialog === "function") {
            return window.YGBookingLuxuryDialog(options || {});
        }

        if (typeof window.Swal !== "undefined" && typeof window.Swal.fire === "function") {
            return window.Swal.fire(options);
        }

        return Promise.resolve({ isConfirmed: true });
    }

    function getInvoiceOverlay() {
        return getElement(
            "bookingInvoiceOverlay"
        );
    }

    function getInvoiceSheet() {
        return document.querySelector(
            ".booking-invoice-sheet"
        );
    }

    function getInvoiceDocument() {
        return getElement(
            "bookingInvoicePrintable"
        );
    }

    function getBookingId() {
        const overlay =
            getInvoiceOverlay();

        const parsed =
            Number.parseInt(
                String(
                    overlay?.dataset
                        .bookingId ||
                    ""
                ),
                10
            );

        return Number.isFinite(parsed)
            ? parsed
            : 0;
    }

    function getInvoiceNumber() {
        return String(
            getInvoiceOverlay()
                ?.dataset
                .invoiceNumber ||
            ""
        ).trim();
    }

    function getShareTitle() {
        return String(
            getInvoiceOverlay()
                ?.dataset
                .shareTitle ||
            "فاتورة الحجز"
        ).trim();
    }

    function getShareText() {
        return String(
            getInvoiceOverlay()
                ?.dataset
                .shareText ||
            "فاتورة حجز من منصة دليلك اليمني"
        ).trim();
    }

    function isInvoiceOpen() {
        const overlay =
            getInvoiceOverlay();

        return Boolean(
            overlay &&
            !overlay.hidden &&
            overlay.getAttribute(
                "aria-hidden"
            ) !== "true"
        );
    }

    /* ======================================================================
       02. تهيئة الفاتورة
       ====================================================================== */

    function initializeBookingInvoice() {
        if (state.initialized) {
            return;
        }

        const overlay =
            getInvoiceOverlay();

        const openButton =
            getElement(
                "openBookingInvoiceButton"
            );

        if (
            !overlay ||
            !openButton
        ) {
            return;
        }

        state.initialized = true;

        initializeOpenButton();
        initializeCloseButtons();
        initializePrintButton();
        initializeShareButton();
        initializeKeyboardEvents();
        initializeOverlayEvents();
        initializePrintEvents();

        overlay.hidden = true;

        overlay.setAttribute(
            "aria-hidden",
            "true"
        );
    }

    if (
        document.readyState ===
        "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            initializeBookingInvoice,
            {
                once: true
            }
        );
    } else {
        initializeBookingInvoice();
    }

    /* ======================================================================
       03. فتح الفاتورة
       ====================================================================== */

    function initializeOpenButton() {
        const openButton =
            getElement(
                "openBookingInvoiceButton"
            );

        openButton?.addEventListener(
            "click",
            event => {
                event.preventDefault();

                openBookingInvoice();
            }
        );
    }

    function openBookingInvoice() {
        const overlay =
            getInvoiceOverlay();

        if (
            !overlay ||
            state.isOpen
        ) {
            return;
        }

        state.lastFocusedElement =
            document.activeElement instanceof
                HTMLElement
                ? document.activeElement
                : null;

        state.isOpen = true;

        overlay.hidden = false;

        overlay.setAttribute(
            "aria-hidden",
            "false"
        );

        document.body.classList.add(
            "booking-invoice-open"
        );

        window.requestAnimationFrame(
            () => {
                const closeButton =
                    overlay.querySelector(
                        ".booking-invoice-close-button"
                    );

                if (
                    closeButton instanceof
                    HTMLElement
                ) {
                    closeButton.focus();
                }
            }
        );
    }

    /* ======================================================================
       04. إغلاق الفاتورة
       ====================================================================== */

    function initializeCloseButtons() {
        getElements(
            "[data-invoice-close]"
        ).forEach(button => {
            button.addEventListener(
                "click",
                event => {
                    event.preventDefault();

                    closeBookingInvoice();
                }
            );
        });
    }

    function closeBookingInvoice() {
        const overlay =
            getInvoiceOverlay();

        if (
            !overlay ||
            !state.isOpen ||
            state.isPrinting
        ) {
            return;
        }

        state.isOpen = false;

        overlay.hidden = true;

        overlay.setAttribute(
            "aria-hidden",
            "true"
        );

        document.body.classList.remove(
            "booking-invoice-open"
        );

        if (
            state.lastFocusedElement
        ) {
            state.lastFocusedElement
                .focus();
        }

        state.lastFocusedElement =
            null;
    }

    /* ======================================================================
       05. أحداث لوحة المفاتيح
       ====================================================================== */

    function initializeKeyboardEvents() {
        document.addEventListener(
            "keydown",
            event => {
                if (!isInvoiceOpen()) {
                    return;
                }

                if (
                    event.key ===
                    "Escape"
                ) {
                    event.preventDefault();

                    closeBookingInvoice();

                    return;
                }

                if (
                    event.key ===
                    "Tab"
                ) {
                    trapFocusInsideInvoice(
                        event
                    );
                }
            }
        );
    }

    function trapFocusInsideInvoice(event) {
        const sheet =
            getInvoiceSheet();

        if (!sheet) {
            return;
        }

        const focusableElements =
            Array.from(
                sheet.querySelectorAll(
                    [
                        "button:not([disabled])",
                        "a[href]",
                        "input:not([disabled])",
                        "select:not([disabled])",
                        "textarea:not([disabled])",
                        '[tabindex]:not([tabindex="-1"])'
                    ].join(",")
                )
            ).filter(element =>
                element instanceof
                HTMLElement &&
                element.offsetParent !==
                null
            );

        if (!focusableElements.length) {
            return;
        }

        const firstElement =
            focusableElements[0];

        const lastElement =
            focusableElements[
            focusableElements.length -
            1
            ];

        if (
            event.shiftKey &&
            document.activeElement ===
            firstElement
        ) {
            event.preventDefault();

            lastElement.focus();

            return;
        }

        if (
            !event.shiftKey &&
            document.activeElement ===
            lastElement
        ) {
            event.preventDefault();

            firstElement.focus();
        }
    }

    /* ======================================================================
       06. أحداث طبقة الفاتورة
       ====================================================================== */

    function initializeOverlayEvents() {
        const overlay =
            getInvoiceOverlay();

        const sheet =
            getInvoiceSheet();

        overlay?.addEventListener(
            "click",
            event => {
                if (
                    event.target ===
                    overlay
                ) {
                    closeBookingInvoice();
                }
            }
        );

        sheet?.addEventListener(
            "click",
            event => {
                event.stopPropagation();
            }
        );
    }

    /* ======================================================================
       07. طباعة الفاتورة
       ====================================================================== */

    function initializePrintButton() {
        const printButton =
            getElement(
                "printBookingInvoiceButton"
            );

        printButton?.addEventListener(
            "click",
            event => {
                event.preventDefault();

                printBookingInvoice();
            }
        );
    }

    function printBookingInvoice() {
        const invoiceDocument =
            getInvoiceDocument();

        if (!invoiceDocument) {
            showMessage({
                icon: "error",
                title: "تعذر طباعة الفاتورة",
                text: "لم يتم العثور على محتوى الفاتورة.",
                confirmButtonColor: "#00A6DA",
                confirmButtonText: "حسنًا"
            });

            return;
        }

        const printWindow =
            window.open(
                "",
                "_blank",
                "width=1000,height=800"
            );

        if (!printWindow) {
            showMessage({
                icon: "warning",
                title: "تعذر فتح نافذة الطباعة",
                text: "اسمحي بالنوافذ المنبثقة لهذا الموقع ثم حاولي مرة أخرى.",
                confirmButtonColor: "#00A6DA",
                confirmButtonText: "حسنًا"
            });

            return;
        }

        state.isPrinting = true;

        const styles =
            Array.from(
                document.querySelectorAll(
                    'link[rel="stylesheet"], style'
                )
            )
                .map(element =>
                    element.outerHTML
                )
                .join("\n");

        const invoiceTitle =
            getInvoiceNumber() ||
            "فاتورة الحجز";

        printWindow.document.open();

        printWindow.document.write(`
        <!DOCTYPE html>

        <html lang="ar" dir="rtl">

        <head>

            <meta charset="utf-8">

            <meta name="viewport"
                  content="width=device-width, initial-scale=1">

            <base href="${document.baseURI}">

            <title>${invoiceTitle}</title>

            ${styles}

            <style>

                html,
                body {
                    margin: 0 !important;
                    padding: 0 !important;
                    background: #ffffff !important;
                }

                body {
                    direction: rtl;
                }

                #bookingInvoicePrintable {
                    visibility: visible !important;
                    display: block !important;
                    width: 100% !important;
                    margin: 0 !important;
                }

                #bookingInvoicePrintable * {
                    visibility: visible !important;
                }

            </style>

        </head>

        <body>

            ${invoiceDocument.outerHTML}

        </body>

        </html>
    `);

        printWindow.document.close();

        printWindow.addEventListener(
            "load",
            () => {
                window.setTimeout(
                    () => {
                        printWindow.focus();
                        printWindow.print();
                    },
                    500
                );
            },
            {
                once: true
            }
        );

        printWindow.addEventListener(
            "afterprint",
            () => {
                state.isPrinting = false;
                printWindow.close();
            },
            {
                once: true
            }
        );
    }

    function initializePrintEvents() {
        window.addEventListener(
            "beforeprint",
            () => {
                state.isPrinting = true;

                document.body.classList.add(
                    "booking-invoice-printing"
                );

                const overlay =
                    getInvoiceOverlay();

                if (overlay) {
                    overlay.hidden = false;

                    overlay.setAttribute(
                        "aria-hidden",
                        "false"
                    );
                }
            }
        );

        window.addEventListener(
            "afterprint",
            () => {
                state.isPrinting = false;

                document.body.classList.remove(
                    "booking-invoice-printing"
                );

                /*
                 * إذا كانت الفاتورة مفتوحة قبل الطباعة
                 * نظل نعرضها، وإلا نغلقها.
                 */
                if (!state.isOpen) {
                    const overlay =
                        getInvoiceOverlay();

                    if (overlay) {
                        overlay.hidden =
                            true;

                        overlay.setAttribute(
                            "aria-hidden",
                            "true"
                        );
                    }

                    document.body
                        .classList
                        .remove(
                            "booking-invoice-open"
                        );
                }
            }
        );
    }

    /* ======================================================================
       08. مشاركة الفاتورة
       ====================================================================== */

    function initializeShareButton() {
        const shareButton =
            getElement(
                "shareBookingInvoiceButton"
            );

        shareButton?.addEventListener(
            "click",
            async event => {
                event.preventDefault();

                await shareBookingInvoice(
                    shareButton
                );
            }
        );
    }

    async function shareBookingInvoice(
        button
    ) {
        const shareTitle =
            getShareTitle();

        const shareText =
            buildShareMessage();

        const shareUrl =
            window.location.href;

        try {
            if (
                typeof navigator.share ===
                "function"
            ) {
                await navigator.share({
                    title:
                        shareTitle,

                    text:
                        shareText,

                    url:
                        shareUrl
                });

                markShareButtonSuccess(
                    button,
                    "تمت المشاركة"
                );

                return;
            }

            const copied =
                await copyText(
                    `${shareText}\n${shareUrl}`
                );

            if (!copied) {
                throw new Error(
                    "تعذر نسخ بيانات الفاتورة."
                );
            }

            markShareButtonSuccess(
                button,
                "تم النسخ"
            );

            await showMessage({
                icon: "success",
                title:
                    "تم نسخ بيانات الفاتورة",
                text:
                    "يمكنك الآن لصق تفاصيل الفاتورة وإرسالها عبر واتساب أو أي تطبيق آخر.",
                confirmButtonColor:
                    "#00A6DA",
                confirmButtonText:
                    "حسنًا"
            });
        } catch (error) {
            /*
             * إلغاء المستخدم لنافذة المشاركة
             * ليس خطأ يستحق إظهار رسالة.
             */
            if (
                error?.name ===
                "AbortError"
            ) {
                return;
            }

            console.error(
                "تعذر مشاركة الفاتورة:",
                error
            );

            await showMessage({
                icon: "error",
                title:
                    "تعذر مشاركة الفاتورة",
                text:
                    "يمكنك استخدام زر الطباعة وحفظ الفاتورة كملف PDF.",
                confirmButtonColor:
                    "#00A6DA",
                confirmButtonText:
                    "حسنًا"
            });
        }
    }

    function buildShareMessage() {
        const invoiceNumber =
            getInvoiceNumber();

        const bookingId =
            getBookingId();

        const baseText =
            getShareText();

        const parts = [
            baseText
        ];

        if (invoiceNumber) {
            parts.push(
                `رقم الفاتورة: ${invoiceNumber}`
            );
        }

        if (bookingId > 0) {
            parts.push(
                `رقم الحجز: ${String(
                    bookingId
                ).padStart(6, "0")}`
            );
        }

        parts.push(
            "منصة دليلك اليمني"
        );

        return parts.join("\n");
    }

    /* ======================================================================
       09. نسخ النص
       ====================================================================== */

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

            const textArea =
                document.createElement(
                    "textarea"
                );

            textArea.value =
                value;

            textArea.setAttribute(
                "readonly",
                ""
            );

            textArea.style.position =
                "fixed";

            textArea.style.inset =
                "-1000px auto auto -1000px";

            textArea.style.opacity =
                "0";

            document.body.appendChild(
                textArea
            );

            textArea.focus();
            textArea.select();

            const result =
                document.execCommand(
                    "copy"
                );

            textArea.remove();

            return result;
        } catch (error) {
            console.error(
                "تعذر نسخ بيانات الفاتورة:",
                error
            );

            return false;
        }
    }

    /* ======================================================================
       10. تغيير شكل زر المشاركة مؤقتًا
       ====================================================================== */

    function markShareButtonSuccess(
        button,
        text
    ) {
        if (
            !(button instanceof
                HTMLElement)
        ) {
            return;
        }

        const originalHtml =
            button.innerHTML;

        button.innerHTML = `
            <i class="bi bi-check2-circle"></i>
            <span>${escapeHtml(text)}</span>
        `;

        button.classList.add(
            "is-success"
        );

        window.setTimeout(
            () => {
                button.innerHTML =
                    originalHtml;

                button.classList.remove(
                    "is-success"
                );
            },
            1800
        );
    }

    function escapeHtml(value) {
        return String(
            value ?? ""
        )
            .replace(
                /&/g,
                "&amp;"
            )
            .replace(
                /</g,
                "&lt;"
            )
            .replace(
                />/g,
                "&gt;"
            )
            .replace(
                /"/g,
                "&quot;"
            )
            .replace(
                /'/g,
                "&#039;"
            );
    }

    /* ======================================================================
       11. تنظيف الحالة عند مغادرة الصفحة
       ====================================================================== */

    window.addEventListener(
        "beforeunload",
        () => {
            document.body.classList.remove(
                "booking-invoice-open",
                "booking-invoice-printing"
            );
        }
    );
})();

/* Luxury invoice ambient light — visual only */
(() => {
    "use strict";
    if (!window.matchMedia?.("(hover:hover) and (pointer:fine)").matches) return;
    const init = () => {
        const sheet = document.querySelector(".booking-invoice-sheet");
        if (!sheet || sheet.dataset.luxuryMotionBound === "true") return;
        sheet.dataset.luxuryMotionBound = "true";
        sheet.addEventListener("pointermove", event => {
            const rect = sheet.getBoundingClientRect();
            const x = ((event.clientX - rect.left) / Math.max(1, rect.width)) * 100;
            const y = ((event.clientY - rect.top) / Math.max(1, rect.height)) * 100;
            sheet.style.setProperty("--inv-mx", `${x}%`);
            sheet.style.setProperty("--inv-my", `${y}%`);
        }, { passive: true });
    };
    document.readyState === "loading"
        ? document.addEventListener("DOMContentLoaded", init, { once: true })
        : init();
})();
