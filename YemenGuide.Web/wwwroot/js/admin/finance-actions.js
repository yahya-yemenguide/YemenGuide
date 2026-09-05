/* ==========================================================================
 * دليلك اليمني
 * العمليات المالية
 * File: wwwroot/js/admin/finance-actions.js
 *
 * الوظائف:
 * - التبديل الآمن بين تبويبات المالية.
 * - حفظ التبويب النشط بعد تحديث الصفحة.
 * - تمرير التبويب النشط إلى مجال الرؤية على الهاتف.
 * - تأكيد استلام عمولة المنصة.
 * - منع إرسال الطلب المالي أكثر من مرة.
 * - SweetAlert فاخر على الكمبيوتر.
 * - Bottom Sheet سلس من الأسفل على الهاتف.
 * ========================================================================== */

(function () {
    "use strict";


    /* ======================================================================
     * 1. الثوابت
     * ====================================================================== */

    const FINANCE_ACTIVE_TAB_KEY = "activeFinanceTab";

    const FINANCE_CONFIRM_PAYMENT_URL =
        "/Admin/Finance/ConfirmPayment";

    const FINANCE_MOBILE_MEDIA =
        window.matchMedia("(max-width: 767.98px)");


    /* ======================================================================
     * 2. أدوات مساعدة عامة
     * ====================================================================== */

    /**
     * التحقق من أن الصفحة معروضة بحجم هاتف.
     *
     * @returns {boolean}
     */
    function isFinanceMobileViewport() {
        return FINANCE_MOBILE_MEDIA.matches;
    }


    /**
     * التحقق من توفر SweetAlert2.
     *
     * @returns {boolean}
     */
    function hasSweetAlert() {
        return (
            typeof window.Swal !== "undefined" &&
            typeof window.Swal.fire === "function"
        );
    }


    /**
     * دمج أسماء الكلاسات دون إضافة قيم فارغة.
     *
     * @param {...string} classNames
     * @returns {string}
     */
    function joinClassNames(...classNames) {
        return classNames
            .filter(Boolean)
            .join(" ")
            .trim();
    }


    /**
     * قراءة قيمة من localStorage بأمان.
     *
     * @param {string} key
     * @returns {string|null}
     */
    function readFinanceStorage(key) {
        try {
            return window.localStorage.getItem(key);
        } catch (error) {
            console.warn(
                "تعذر قراءة التبويب المالي المحفوظ:",
                error
            );

            return null;
        }
    }


    /**
     * حفظ قيمة داخل localStorage بأمان.
     *
     * @param {string} key
     * @param {string} value
     */
    function writeFinanceStorage(key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch (error) {
            console.warn(
                "تعذر حفظ التبويب المالي:",
                error
            );
        }
    }


    /**
     * الحصول على عناصر صفحة المالية فقط.
     *
     * @returns {HTMLElement|null}
     */
    function getFinancePage() {
        return document.querySelector(".finance-page");
    }


    /**
     * تجهيز معرّف السجل المالي للإرسال.
     *
     * نحافظ عليه كنص كما كان في الملف الأصلي؛ لأن الخادم
     * يستقبل body مباشرة وليس كائن JSON.
     *
     * @param {unknown} value
     * @returns {string}
     */
    function normalizeRecordId(value) {
        return String(value ?? "").trim();
    }


    /* ======================================================================
     * 3. إعداد SweetAlert الموحد
     * ====================================================================== */

    /**
     * فتح نافذة SweetAlert خاصة بصفحة المالية.
     *
     * على الهاتف:
     * - تفتح في أسفل الشاشة.
     * - يأخذ CSS دور Bottom Sheet.
     *
     * على الكمبيوتر:
     * - تبقى في منتصف الشاشة.
     *
     * @param {Object} options
     * @param {string} popupVariant
     * @returns {Promise<any>}
     */
    function fireFinanceAlert(
        options = {},
        popupVariant = ""
    ) {
        if (!hasSweetAlert()) {
            return Promise.resolve({
                isConfirmed: false,
                isDismissed: true
            });
        }

        const suppliedClasses =
            options.customClass || {};

        return window.Swal.fire({
            ...options,

            position:
                isFinanceMobileViewport()
                    ? "bottom"
                    : (options.position || "center"),

            heightAuto: false,

            scrollbarPadding: false,

            returnFocus: true,

            customClass: {
                ...suppliedClasses,

                container: joinClassNames(
                    suppliedClasses.container,
                    "finance-swal-container"
                ),

                popup: joinClassNames(
                    suppliedClasses.popup,
                    "finance-swal-popup",
                    popupVariant
                ),

                title: joinClassNames(
                    suppliedClasses.title,
                    "finance-swal-title"
                ),

                htmlContainer: joinClassNames(
                    suppliedClasses.htmlContainer,
                    "finance-swal-content"
                ),

                actions: joinClassNames(
                    suppliedClasses.actions,
                    "finance-swal-actions"
                ),

                confirmButton: joinClassNames(
                    suppliedClasses.confirmButton,
                    "finance-swal-confirm"
                ),

                cancelButton: joinClassNames(
                    suppliedClasses.cancelButton,
                    "finance-swal-cancel"
                ),

                closeButton: joinClassNames(
                    suppliedClasses.closeButton,
                    "finance-swal-close"
                ),

                loader: joinClassNames(
                    suppliedClasses.loader,
                    "finance-swal-loader"
                )
            }
        });
    }


    /**
     * إغلاق نافذة SweetAlert الحالية.
     */
    function closeFinanceAlert() {
        if (hasSweetAlert()) {
            window.Swal.close();
        }
    }


    /**
     * إظهار نافذة معالجة مالية.
     *
     * @param {string} message
     */
    function showFinanceLoading(message) {
        if (!hasSweetAlert()) {
            return;
        }

        fireFinanceAlert(
            {
                title: message,

                text:
                    "يرجى الانتظار حتى يتم تحديث السجل المالي.",

                allowOutsideClick: false,

                allowEscapeKey: false,

                showConfirmButton: false,

                didOpen: function () {
                    window.Swal.showLoading();
                }
            },
            "finance-loading-popup"
        );
    }


    /**
     * إظهار رسالة خطأ.
     *
     * @param {string} title
     * @param {string} message
     * @returns {Promise<any>}
     */
    function showFinanceError(title, message) {
        if (!hasSweetAlert()) {
            window.alert(`${title}\n${message}`);

            return Promise.resolve();
        }

        return fireFinanceAlert(
            {
                icon: "error",

                title: title,

                text: message,

                confirmButtonColor: "#dc3545",

                confirmButtonText:
                    '<i class="bi bi-check2-circle me-1"></i> حسنًا'
            },
            "finance-error-popup"
        );
    }


    /**
     * إظهار رسالة نجاح العملية.
     *
     * @param {string} message
     * @returns {Promise<any>}
     */
    function showFinanceSuccess(message) {
        if (!hasSweetAlert()) {
            window.alert(
                message ||
                "تم تأكيد السداد بنجاح."
            );

            return Promise.resolve();
        }

        return fireFinanceAlert(
            {
                icon: "success",

                title: "تم تأكيد السداد",

                text:
                    message ||
                    "تم استلام العمولة وتحديث العملية المالية بنجاح.",

                confirmButtonColor: "#198754",

                confirmButtonText:
                    '<i class="bi bi-check-circle-fill me-1"></i> ممتاز',

                allowOutsideClick: false,

                allowEscapeKey: false
            },
            "finance-success-popup"
        );
    }


    /* ======================================================================
     * 4. إدارة التبويبات
     * ====================================================================== */

    /**
     * إظهار زر التبويب النشط داخل مجال الرؤية على الهاتف.
     *
     * @param {HTMLElement|null} button
     * @param {boolean} smooth
     */
    function revealFinanceTab(
        button,
        smooth = true
    ) {
        if (
            !button ||
            !isFinanceMobileViewport()
        ) {
            return;
        }

        window.setTimeout(
            function () {
                button.scrollIntoView({
                    behavior:
                        smooth
                            ? "smooth"
                            : "auto",

                    block: "nearest",

                    inline: "center"
                });
            },
            70
        );
    }


    /**
     * التبديل بين تبويبات المالية.
     *
     * الاسم بقي عامًا حتى يستمر onclick الموجود في HTML بالعمل:
     * switchFinanceTab('due-tab', this)
     *
     * @param {string} tabId
     * @param {HTMLElement|null} clickedBtn
     */
    function switchFinanceTab(
        tabId,
        clickedBtn
    ) {
        const financePage =
            getFinancePage();

        if (!financePage || !tabId) {
            return;
        }

        const tabContents =
            financePage.querySelectorAll(
                ".tab-pane-content"
            );

        const tabButtons =
            financePage.querySelectorAll(
                ".custom-tabs-container .tab-btn"
            );

        const targetTab =
            financePage.querySelector(
                `#${CSS.escape(tabId)}`
            );

        if (!targetTab) {
            console.warn(
                `لم يتم العثور على التبويب المالي: ${tabId}`
            );

            return;
        }

        let activeButton =
            clickedBtn instanceof HTMLElement
                ? clickedBtn
                : null;


        /*
         * إخفاء بقية محتويات التبويبات.
         */

        tabContents.forEach(function (tab) {
            const isActive =
                tab === targetTab;

            tab.style.display =
                isActive
                    ? "block"
                    : "none";

            tab.setAttribute(
                "aria-hidden",
                isActive
                    ? "false"
                    : "true"
            );
        });


        /*
         * تحديث حالة أزرار التبويبات.
         */

        tabButtons.forEach(function (button) {
            const buttonTarget =
                button.getAttribute(
                    "data-target"
                );

            const isActive =
                buttonTarget === tabId;

            button.classList.toggle(
                "active",
                isActive
            );

            button.setAttribute(
                "aria-selected",
                isActive
                    ? "true"
                    : "false"
            );

            button.tabIndex =
                isActive
                    ? 0
                    : -1;

            if (isActive) {
                activeButton = button;
            }
        });


        /*
         * حفظ التبويب لإعادته بعد تحديث الصفحة.
         */

        writeFinanceStorage(
            FINANCE_ACTIVE_TAB_KEY,
            tabId
        );


        /*
         * تحريك التبويب المختار إلى منتصف شريط الهاتف.
         */

        revealFinanceTab(
            activeButton,
            true
        );
    }


    /**
     * تهيئة التبويب المحفوظ عند فتح الصفحة.
     *
     * @param {HTMLElement} financePage
     */
    function initializeFinanceTabs(financePage) {
        const tabButtons =
            Array.from(
                financePage.querySelectorAll(
                    ".custom-tabs-container .tab-btn"
                )
            );

        if (!tabButtons.length) {
            return;
        }

        const savedTab =
            readFinanceStorage(
                FINANCE_ACTIVE_TAB_KEY
            );

        const savedButton =
            savedTab
                ? tabButtons.find(
                    button =>
                        button.getAttribute(
                            "data-target"
                        ) === savedTab
                )
                : null;

        const markupActiveButton =
            tabButtons.find(
                button =>
                    button.classList.contains(
                        "active"
                    )
            );

        const initialButton =
            savedButton ||
            markupActiveButton ||
            tabButtons[0];

        const initialTarget =
            initialButton?.getAttribute(
                "data-target"
            );

        if (initialTarget) {
            switchFinanceTab(
                initialTarget,
                initialButton
            );

            revealFinanceTab(
                initialButton,
                false
            );
        }


        /*
         * دعم التنقل بين التبويبات بلوحة المفاتيح.
         */

        tabButtons.forEach(function (button) {
            button.addEventListener(
                "keydown",
                function (event) {
                    if (
                        event.key !== "ArrowRight" &&
                        event.key !== "ArrowLeft"
                    ) {
                        return;
                    }

                    event.preventDefault();

                    const currentIndex =
                        tabButtons.indexOf(this);

                    const movement =
                        event.key === "ArrowRight"
                            ? -1
                            : 1;

                    let nextIndex =
                        currentIndex + movement;

                    if (nextIndex < 0) {
                        nextIndex =
                            tabButtons.length - 1;
                    }

                    if (
                        nextIndex >=
                        tabButtons.length
                    ) {
                        nextIndex = 0;
                    }

                    const nextButton =
                        tabButtons[nextIndex];

                    const nextTarget =
                        nextButton.getAttribute(
                            "data-target"
                        );

                    if (!nextTarget) {
                        return;
                    }

                    switchFinanceTab(
                        nextTarget,
                        nextButton
                    );

                    nextButton.focus();
                }
            );
        });
    }


    /* ======================================================================
     * 5. تأكيد عملية السداد
     * ====================================================================== */

    /**
     * فتح نافذة تأكيد استلام العمولة.
     *
     * @param {string} recordId
     * @param {HTMLButtonElement|null} sourceButton
     */
    async function confirmPaymentAction(
        recordId,
        sourceButton = null
    ) {
        const normalizedRecordId =
            normalizeRecordId(recordId);

        if (!normalizedRecordId) {
            await showFinanceError(
                "تعذر تنفيذ العملية",
                "رقم السجل المالي غير صالح."
            );

            return;
        }


        let isConfirmed = false;


        /*
         * SweetAlert متوفر.
         */

        if (hasSweetAlert()) {
            const confirmationResult =
                await fireFinanceAlert(
                    {
                        icon: "question",

                        title: "تأكيد استلام العمولة",

                        text:
                            "هل أنت متأكد من استلام عمولة المنصة وتأكيد هذه العملية المالية؟",

                        showCancelButton: true,

                        focusCancel: true,

                        reverseButtons: false,

                        confirmButtonColor:
                            "#198754",

                        cancelButtonColor:
                            "#64748b",

                        confirmButtonText:
                            '<i class="bi bi-check2-circle me-1"></i> نعم، تأكيد السداد',

                        cancelButtonText:
                            '<i class="bi bi-x-circle me-1"></i> إلغاء',

                        allowOutsideClick: true,

                        allowEscapeKey: true
                    },
                    "finance-confirm-popup"
                );

            isConfirmed =
                confirmationResult.isConfirmed;
        }

        /*
         * احتياط في حال لم تُحمّل المكتبة.
         */

        else {
            isConfirmed =
                window.confirm(
                    "هل أنت متأكد من استلام العمولة وتأكيد هذه العملية؟"
                );
        }


        if (!isConfirmed) {
            return;
        }


        await submitConfirmedPayment(
            normalizedRecordId,
            sourceButton
        );
    }


    /* ======================================================================
     * 6. إرسال طلب السداد إلى الخادم
     * ====================================================================== */

    /**
     * إرسال رقم السجل إلى المسار الحالي.
     *
     * مهم:
     * أبقينا body كسجل مباشر مثل الملف الأصلي:
     *
     * body: recordId
     *
     * @param {string} recordId
     * @param {HTMLButtonElement|null} sourceButton
     */
    async function submitConfirmedPayment(
        recordId,
        sourceButton
    ) {
        if (sourceButton) {
            sourceButton.disabled = true;

            sourceButton.setAttribute(
                "aria-busy",
                "true"
            );
        }


        showFinanceLoading(
            "جاري تأكيد العملية المالية..."
        );


        try {
            const response =
                await fetch(
                    FINANCE_CONFIRM_PAYMENT_URL,
                    {
                        method: "POST",

                        headers: {
                            "Content-Type":
                                "application/json"
                        },

                        /*
                         * لا نحول المعرّف إلى كائن ولا نغيّر شكل الطلب.
                         * هذه هي طريقة الإرسال التي كان يستخدمها الملف الأصلي.
                         */
                        body: recordId
                    }
                );


            let data;

            try {
                data =
                    await response.json();
            } catch (jsonError) {
                throw new Error(
                    "أعاد الخادم استجابة غير صالحة."
                );
            }


            if (!response.ok) {
                throw new Error(
                    data?.message ||
                    "رفض الخادم تأكيد العملية المالية."
                );
            }


            if (!data.success) {
                closeFinanceAlert();

                await showFinanceError(
                    "لم يتم تأكيد السداد",
                    data.message ||
                    "تعذر تحديث السجل المالي."
                );

                return;
            }


            closeFinanceAlert();


            await showFinanceSuccess(
                data.message
            );


            /*
             * تحديث الصفحة يعيد تحميل السجلات والتقارير من الخادم،
             * وسيُفتح التبويب المحفوظ تلقائيًا.
             */

            window.location.reload();

        } catch (error) {
            console.error(
                "Finance payment confirmation failed:",
                error
            );

            closeFinanceAlert();

            await showFinanceError(
                "خطأ في الاتصال",
                error?.message ||
                "حدثت مشكلة أثناء الاتصال بالخادم."
            );

        } finally {
            if (sourceButton) {
                sourceButton.disabled = false;

                sourceButton.removeAttribute(
                    "aria-busy"
                );
            }
        }
    }


    /* ======================================================================
     * 7. ربط أزرار تأكيد السداد
     * ====================================================================== */

    /**
     * ربط أحداث أزرار السداد داخل صفحة المالية.
     *
     * @param {HTMLElement} financePage
     */
    function initializeConfirmPaymentButtons(
        financePage
    ) {
        const confirmButtons =
            financePage.querySelectorAll(
                ".confirm-btn"
            );

        confirmButtons.forEach(function (button) {
            button.addEventListener(
                "click",
                function (event) {
                    event.preventDefault();

                    /*
                     * منع تنفيذ طلب ثانٍ أثناء معالجة الطلب الحالي.
                     */

                    if (
                        this.disabled ||
                        this.getAttribute(
                            "aria-busy"
                        ) === "true"
                    ) {
                        return;
                    }

                    const recordId =
                        this.dataset.id;

                    confirmPaymentAction(
                        recordId,
                        this
                    );
                }
            );
        });
    }


    /* ======================================================================
     * 8. بدء تشغيل الصفحة
     * ====================================================================== */

    document.addEventListener(
        "DOMContentLoaded",
        function () {
            const financePage =
                getFinancePage();

            /*
             * منع تشغيل السكربت في صفحات الأدمن الأخرى.
             */

            if (!financePage) {
                return;
            }

            initializeFinanceTabs(
                financePage
            );

            initializeConfirmPaymentButtons(
                financePage
            );
        }
    );


    /* ======================================================================
     * 9. الدوال العامة المرتبطة بالـHTML
     * ====================================================================== */

    /*
     * ضروري لأن أزرار التبويبات في HTML تستخدم:
     * onclick="switchFinanceTab('due-tab', this)"
     */

    window.switchFinanceTab =
        switchFinanceTab;


    /*
     * إبقاء دالة التأكيد متاحة عالميًا للاستخدام مستقبلًا
     * دون التأثير على الربط الحالي بالأزرار.
     */

    window.confirmPaymentAction =
        confirmPaymentAction;

})();