/* ==========================================================================
 * دليلك اليمني
 * إدارة المستخدمين
 * File: wwwroot/js/admin/users-actions.js
 *
 * الوظائف:
 * - إدارة تبويبات المستخدمين وحفظ التبويب النشط.
 * - قبول وتنشيط وتعليق وحظر ورفض الحسابات.
 * - إلغاء الحظر.
 * - الحذف النهائي.
 * - عرض وثائق المالك.
 * - SweetAlert فاخر في الكمبيوتر.
 * - Bottom Sheet سلس في الهاتف.
 * ========================================================================== */

(function () {
    "use strict";


    /* ======================================================================
     * 1. التشغيل بعد اكتمال الصفحة
     * ====================================================================== */

    document.addEventListener("DOMContentLoaded", function () {

        const usersPage =
            document.querySelector(".users-management-page");

        /*
         * منع تشغيل المحرك خارج صفحة إدارة المستخدمين.
         * هذا يحمي التبويبات والأزرار الموجودة في بقية صفحات الأدمن.
         */
        if (!usersPage) {
            return;
        }

        initializeUsersTabs(usersPage);
        initializeUserActionButtons(usersPage);
        initializeVerificationDocuments(usersPage);
        initializeUsersNotificationDeepLink(usersPage);
    });


    function initializeUsersNotificationDeepLink(
        usersPage
    ) {
        if (!usersPage) {
            return;
        }

        const url =
            new URL(window.location.href);

        const userId =
            Number.parseInt(
                url.searchParams.get("focusUser") || "0",
                10
            );

        if (!Number.isInteger(userId) ||
            userId <= 0) {
            return;
        }

        const openVerification =
            String(
                url.searchParams.get("openVerification") || ""
            ).toLowerCase() === "true";

        const userRow =
            usersPage.querySelector(
                `[data-user-id="${userId}"]`
            );

        if (!userRow) {
            url.searchParams.delete("focusUser");
            url.searchParams.delete("openVerification");

            window.history.replaceState(
                window.history.state,
                "",
                url.pathname + url.search + url.hash
            );

            return;
        }

        const pane =
            userRow.closest(
                ".tab-pane-content, .users-tab-pane"
            );

        let targetTab = null;

        if (pane) {
            const tabButtons =
                Array.from(
                    usersPage.querySelectorAll(
                        ".custom-tabs-container .tab-btn"
                    )
                );

            targetTab =
                tabButtons.find(button => {
                    const targetClass =
                        button.getAttribute("data-target");

                    return targetClass &&
                        pane.classList.contains(targetClass);
                }) || null;
        }

        if (targetTab) {
            targetTab.click();
        }

        window.setTimeout(
            function () {
                userRow.scrollIntoView({
                    behavior: "smooth",
                    block: "center",
                    inline: "nearest"
                });

                const previousOutline =
                    userRow.style.outline;

                const previousOffset =
                    userRow.style.outlineOffset;

                userRow.style.outline =
                    "2px solid rgba(0, 166, 218, .58)";

                userRow.style.outlineOffset =
                    "4px";

                window.setTimeout(
                    function () {
                        userRow.style.outline =
                            previousOutline;

                        userRow.style.outlineOffset =
                            previousOffset;
                    },
                    4200
                );

                if (openVerification) {
                    const verificationButton =
                        userRow.querySelector(
                            `.view-docs-btn[data-id="${userId}"]`
                        );

                    verificationButton?.click();
                }

                url.searchParams.delete("focusUser");
                url.searchParams.delete("openVerification");

                window.history.replaceState(
                    window.history.state,
                    "",
                    url.pathname + url.search + url.hash
                );
            },
            90
        );
    }


    /* ======================================================================
     * 2. ثوابت الصفحة
     * ====================================================================== */

    const USERS_ACTIVE_TAB_KEY =
        "activeAdminTab";

    const USERS_MOBILE_MEDIA =
        window.matchMedia("(max-width: 767.98px)");

    function usersIsArabic() {
        const page =
            document.querySelector(
                ".users-management-page"
            );

        const pageLanguage =
            page?.dataset?.language;

        if (pageLanguage) {
            return pageLanguage
                .toLowerCase()
                .startsWith("ar");
        }

        return (
            document.documentElement.lang ||
            "ar"
        )
            .toLowerCase()
            .startsWith("ar");
    }

    function T(ar, en) {
        return usersIsArabic()
            ? ar
            : en;
    }


    /* ======================================================================
     * 3. أدوات مساعدة عامة
     * ====================================================================== */

    /**
     * تحديد ما إذا كانت الصفحة معروضة على هاتف.
     *
     * @returns {boolean}
     */
    function isMobileViewport() {
        return USERS_MOBILE_MEDIA.matches;
    }


    /**
     * دمج أسماء الكلاسات مع تجاهل القيم الفارغة.
     *
     * @param  {...string} classNames
     * @returns {string}
     */
    function joinClassNames(...classNames) {
        return classNames
            .filter(Boolean)
            .join(" ")
            .trim();
    }


    /**
     * قراءة قيمة من localStorage دون تعطيل الصفحة
     * إذا كان المتصفح يمنع التخزين المحلي.
     *
     * @param {string} key
     * @returns {string|null}
     */
    function readLocalStorage(key) {
        try {
            return window.localStorage.getItem(key);
        } catch (error) {
            console.warn(
                "تعذر قراءة التخزين المحلي:",
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
    function writeLocalStorage(key, value) {
        try {
            window.localStorage.setItem(
                key,
                value
            );
        } catch (error) {
            console.warn(
                "تعذر حفظ التبويب النشط:",
                error
            );
        }
    }


    /**
     * تحويل القيمة إلى رقم مستخدم صالح.
     *
     * @param {string|null} value
     * @returns {number|null}
     */
    function parseUserId(value) {

        const parsedValue =
            Number.parseInt(value, 10);

        if (!Number.isInteger(parsedValue) ||
            parsedValue <= 0) {

            return null;
        }

        return parsedValue;
    }


    /**
     * منع حقن HTML داخل محتوى نافذة الوثائق.
     *
     * @param {unknown} value
     * @returns {string}
     */
    function escapeHtml(value) {

        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }


    /**
     * حماية القيم المستخدمة داخل خصائص HTML.
     *
     * @param {unknown} value
     * @returns {string}
     */
    function escapeAttribute(value) {

        return escapeHtml(value)
            .replace(/`/g, "&#096;");
    }


    /**
     * التحقق البسيط من رابط الوثيقة.
     *
     * يسمح بالروابط النسبية والعادية وملفات data،
     * ويمنع روابط JavaScript الضارة.
     *
     * @param {unknown} url
     * @returns {string}
     */
    function normalizeDocumentUrl(url) {

        if (!url) {
            return "";
        }

        const normalizedUrl =
            String(url).trim();

        if (!normalizedUrl) {
            return "";
        }

        if (/^(javascript|vbscript):/i.test(normalizedUrl)) {
            return "";
        }

        return normalizedUrl;
    }


    /**
     * التحقق من وجود SweetAlert2.
     *
     * @returns {boolean}
     */
    function hasSweetAlert() {
        return (
            typeof window.Swal !== "undefined" &&
            typeof window.Swal.fire === "function"
        );
    }


    /* ======================================================================
     * 4. مهيئ SweetAlert الخاص بإدارة المستخدمين
     * ====================================================================== */

    /**
     * فتح SweetAlert بإعدادات موحدة.
     *
     * في الهاتف:
     * - يحدد الموضع في الأسفل.
     * - يتكامل مع CSS ليظهر كـ Bottom Sheet.
     *
     * في الكمبيوتر:
     * - يبقى في منتصف الشاشة.
     *
     * @param {Object} options
     * @param {string} popupVariantClass
     * @returns {Promise<any>}
     */
    function fireUsersAlert(
        options = {},
        popupVariantClass = ""
    ) {

        if (!hasSweetAlert()) {
            return Promise.resolve({
                isConfirmed: false,
                isDismissed: true
            });
        }

        const originalCustomClasses =
            options.customClass || {};

        const alertOptions = {
            ...options,

            position:
                isMobileViewport()
                    ? "bottom"
                    : (options.position || "center"),

            heightAuto: false,
            scrollbarPadding: false,
            returnFocus: true,
            rtl: usersIsArabic(),

            /*
             * لا نسمح لأي إعداد عام في SweetAlert أن يظهر
             * Cancel / No / Deny من تلقاء نفسه.
             * تظهر الأزرار الإضافية فقط عندما نطلبها نحن صراحة.
             */
            showCancelButton:
                options.showCancelButton === true,

            showDenyButton:
                options.showDenyButton === true,

            cancelButtonText:
                options.cancelButtonText || 'إلغاء',

            denyButtonText:
                options.denyButtonText || 'رفض',

            confirmButtonText:
                options.confirmButtonText || 'موافق',

            customClass: {
                ...originalCustomClasses,

                container: joinClassNames(
                    originalCustomClasses.container,
                    "users-swal-container"
                ),

                popup: joinClassNames(
                    originalCustomClasses.popup,
                    "users-swal-popup",
                    popupVariantClass
                ),

                title: joinClassNames(
                    originalCustomClasses.title,
                    "users-swal-title"
                ),

                htmlContainer: joinClassNames(
                    originalCustomClasses.htmlContainer,
                    "users-swal-html"
                ),

                actions: joinClassNames(
                    originalCustomClasses.actions,
                    "users-swal-actions"
                ),

                confirmButton: joinClassNames(
                    originalCustomClasses.confirmButton,
                    "users-swal-confirm"
                ),

                cancelButton: joinClassNames(
                    originalCustomClasses.cancelButton,
                    "users-swal-cancel"
                ),

                closeButton: joinClassNames(
                    originalCustomClasses.closeButton,
                    "users-swal-close"
                ),

                loader: joinClassNames(
                    originalCustomClasses.loader,
                    "users-swal-loader"
                )
            }
        };

        return window.Swal.fire(alertOptions);
    }


    /**
     * إغلاق أي نافذة SweetAlert مفتوحة.
     */
    function closeUsersAlert() {

        if (hasSweetAlert()) {
            window.Swal.close();
        }
    }


    /**
     * إظهار نافذة تحميل فاخرة.
     *
     * @param {string} message
     */
    function showUsersLoading(message) {

        if (!hasSweetAlert()) {
            return;
        }

        fireUsersAlert(
            {
                title:
                    escapeHtml(message),

                allowOutsideClick:
                    false,

                allowEscapeKey:
                    false,

                showConfirmButton:
                    false,

                didOpen: function () {
                    window.Swal.showLoading();
                }
            },
            "users-loading-popup"
        );
    }


    /**
     * إظهار رسالة خطأ.
     *
     * @param {string} title
     * @param {string} message
     */
    function showUsersError(
        title,
        message
    ) {

        if (!hasSweetAlert()) {
            window.alert(
                `${title}\n${message}`
            );

            return Promise.resolve();
        }

        return fireUsersAlert(
            {
                icon:
                    "error",

                title:
                    title,

                text:
                    message,

                confirmButtonText:
                    "موافق",

                confirmButtonColor:
                    "#dc3545"
            },
            "users-error-popup"
        );
    }


    /* ======================================================================
     * 5. نظام التبويبات
     * ====================================================================== */

    /**
     * تهيئة تبويبات إدارة المستخدمين.
     *
     * @param {Element} usersPage
     */
    function initializeUsersTabs(usersPage) {

        const tabButtons =
            Array.from(
                usersPage.querySelectorAll(
                    ".custom-tabs-container .tab-btn"
                )
            );

        const tabContents =
            Array.from(
                usersPage.querySelectorAll(
                    ".tab-pane-content"
                )
            );

        if (!tabButtons.length ||
            !tabContents.length) {

            return;
        }


        /**
         * التحقق من وجود التبويب المطلوب.
         *
         * @param {string|null} targetClass
         * @returns {boolean}
         */
        function tabExists(targetClass) {

            if (!targetClass) {
                return false;
            }

            return tabButtons.some(
                button =>
                    button.getAttribute("data-target") ===
                    targetClass
            );
        }


        /**
         * تمرير زر التبويب إلى منتصف شريط الهاتف.
         *
         * @param {Element|null} activeButton
         * @param {boolean} smooth
         */
        function revealActiveTab(
            activeButton,
            smooth = true
        ) {

            if (!activeButton ||
                !isMobileViewport()) {

                return;
            }

            window.setTimeout(
                function () {

                    activeButton.scrollIntoView({
                        behavior:
                            smooth
                                ? "smooth"
                                : "auto",

                        block:
                            "nearest",

                        inline:
                            "center"
                    });
                },
                60
            );
        }


        /**
         * تنشيط تبويب محدد.
         *
         * @param {string} targetClass
         * @param {boolean} scrollToButton
         */
        function activateTab(
            targetClass,
            scrollToButton = true
        ) {

            if (!tabExists(targetClass)) {
                return;
            }

            let activeButton = null;

            tabContents.forEach(
                content => {

                    const isTarget =
                        content.classList.contains(
                            targetClass
                        );

                    content.style.display =
                        isTarget
                            ? "block"
                            : "none";

                    content.setAttribute(
                        "aria-hidden",
                        isTarget
                            ? "false"
                            : "true"
                    );
                }
            );

            tabButtons.forEach(
                button => {

                    const isActive =
                        button.getAttribute(
                            "data-target"
                        ) === targetClass;

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
                }
            );

            if (scrollToButton) {
                revealActiveTab(
                    activeButton,
                    true
                );
            }
        }


        /*
         * التبويب الافتراضي:
         * 1. التبويب المحفوظ إذا كان موجودًا في الصفحة.
         * 2. التبويب الذي يحمل active داخل HTML.
         * 3. أول تبويب متوفر.
         */

        const savedTab =
            readLocalStorage(
                USERS_ACTIVE_TAB_KEY
            );

        const markupActiveButton =
            tabButtons.find(
                button =>
                    button.classList.contains(
                        "active"
                    )
            );

        const defaultTab =
            markupActiveButton?.getAttribute(
                "data-target"
            ) ||
            tabButtons[0]?.getAttribute(
                "data-target"
            );

        const initialTab =
            tabExists(savedTab)
                ? savedTab
                : defaultTab;

        if (initialTab) {
            activateTab(
                initialTab,
                false
            );

            const initialButton =
                tabButtons.find(
                    button =>
                        button.getAttribute(
                            "data-target"
                        ) === initialTab
                );

            revealActiveTab(
                initialButton,
                false
            );
        }


        /*
         * أحداث النقر.
         */

        tabButtons.forEach(
            button => {

                button.addEventListener(
                    "click",
                    function () {

                        const targetClass =
                            this.getAttribute(
                                "data-target"
                            );

                        if (!targetClass ||
                            !tabExists(targetClass)) {

                            return;
                        }

                        writeLocalStorage(
                            USERS_ACTIVE_TAB_KEY,
                            targetClass
                        );

                        activateTab(
                            targetClass,
                            true
                        );
                    }
                );


                /*
                 * دعم التنقل بلوحة المفاتيح.
                 */

                button.addEventListener(
                    "keydown",
                    function (event) {

                        if (
                            event.key !== "ArrowRight" &&
                            event.key !== "ArrowLeft" &&
                            event.key !== "Home" &&
                            event.key !== "End"
                        ) {
                            return;
                        }

                        event.preventDefault();

                        const currentIndex =
                            tabButtons.indexOf(this);

                        let nextIndex;

                        if (event.key === "Home") {
                            nextIndex = 0;
                        }
                        else if (event.key === "End") {
                            nextIndex = tabButtons.length - 1;
                        }
                        else {
                            const rtl =
                                document.documentElement.dir
                                    .toLowerCase() === "rtl";

                            const direction =
                                event.key === "ArrowRight"
                                    ? (rtl ? -1 : 1)
                                    : (rtl ? 1 : -1);

                            nextIndex =
                                currentIndex + direction;
                        }

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

                        writeLocalStorage(
                            USERS_ACTIVE_TAB_KEY,
                            nextTarget
                        );

                        activateTab(
                            nextTarget,
                            true
                        );

                        nextButton.focus();
                    }
                );
            }
        );
    }


    /* ======================================================================
     * 6. إعداد بيانات كل إجراء
     * ====================================================================== */

    /**
     * إرجاع إعدادات الإجراء المطلوب.
     *
     * @param {HTMLElement} button
     * @returns {Object|null}
     */
    function resolveUserAction(button) {

        const isApprovalContext =
            Boolean(
                button.closest(
                    ".pending-actions-group, .rejected-actions-group"
                )
            );

        const buttonState =
            button.getAttribute(
                "data-state"
            );

        /*
         * الحذف النهائي يتم فحصه أولًا،
         * لأن هذا الزر لا يحتاج data-state.
         */

        if (
            button.classList.contains(
                "permanent-delete-btn"
            )
        ) {
            return {
                url:
                    "/Admin/Users/PermanentDelete",

                state:
                    null,

                title:
                    T("تأكيد الحذف النهائي", "Confirm permanent deletion"),

                message:
                    T("سيتم حذف هذا الحساب وكل وثائقه نهائيًا، ولا يمكن التراجع عن هذا الإجراء.", "This account and all of its documents will be permanently deleted. This action cannot be undone."),

                icon:
                    "error",

                confirmColor:
                    "#dc3545",

                confirmText:
                    "حذف نهائي",

                loadingMessage:
                    T("جاري حذف الحساب نهائيًا...", "Deleting account permanently...")
            };
        }


        if (buttonState === "Activate") {
            return {
                url:
                    "/Admin/Users/UpdateState",

                state:
                    "Activate",

                title:
                    (isApprovalContext ? T("تأكيد الموافقة على الطلب", "Confirm approval") : T("تأكيد تنشيط الحساب", "Confirm activation")),

                message:
                    (isApprovalContext ? T("هل تريد الموافقة على طلب هذا المالك وتفعيل حسابه؟", "Approve this owner request and activate the account?") : T("هل تريد تنشيط هذا الحساب؟", "Activate this account?")),

                icon:
                    "success",

                confirmColor:
                    "#198754",

                confirmText:
                    (isApprovalContext ? "قبول" : "تنشيط"),

                loadingMessage:
                    (isApprovalContext ? T("جاري قبول الطلب...", "Approving request...") : T("جاري تنشيط الحساب...", "Activating account..."))
            };
        }


        if (buttonState === "Unblock") {
            return {
                url:
                    "/Admin/Users/UpdateState",

                state:
                    "Unblock",

                title:
                    T("تأكيد إلغاء الحظر", "Confirm unblock"),

                message:
                    T("هل تريد إلغاء الحظر وإعادة تفعيل هذا الحساب؟", "Unblock and reactivate this account?"),

                icon:
                    "info",

                confirmColor:
                    "#00A6DA",

                confirmText:
                    "إلغاء الحظر",

                loadingMessage:
                    T("جاري إلغاء الحظر...", "Unblocking account...")
            };
        }


        if (buttonState === "Suspended") {
            return {
                url:
                    "/Admin/Users/UpdateState",

                state:
                    "Suspended",

                title:
                    T("تأكيد تعليق الحساب", "Confirm suspension"),

                message:
                    T("هل تريد تعليق هذا الحساب مؤقتًا؟ لن يتم حذف أي بيانات مرتبطة به.", "Suspend this account temporarily? No related data will be deleted."),

                icon:
                    "warning",

                confirmColor:
                    "#f59e0b",

                confirmText:
                    "تعليق الحساب",

                loadingMessage:
                    T("جاري تعليق الحساب...", "Suspending account...")
            };
        }


        if (buttonState === "Blocked") {
            return {
                url:
                    "/Admin/Users/UpdateState",

                state:
                    "Blocked",

                title:
                    T("تأكيد حظر الحساب", "Confirm block"),

                message:
                    T("هل تريد حظر هذا المستخدم ومنعه من تسجيل الدخول؟", "Block this user and prevent sign-in?"),

                icon:
                    "error",

                confirmColor:
                    "#dc3545",

                confirmText:
                    "حظر الحساب",

                loadingMessage:
                    T("جاري حظر الحساب...", "Blocking account...")
            };
        }


        if (buttonState === "Rejected") {
            return {
                url:
                    "/Admin/Users/UpdateState",

                state:
                    "Rejected",

                title:
                    T("تأكيد رفض الطلب", "Confirm rejection"),

                message:
                    T("هل تريد رفض طلب هذا المالك ونقله إلى قائمة المرفوضين؟", "Reject this owner request and move it to the rejected list?"),

                icon:
                    "error",

                confirmColor:
                    "#dc3545",

                confirmText:
                    "رفض",

                loadingMessage:
                    T("جاري رفض الطلب...", "Rejecting request...")
            };
        }


        return null;
    }


    /* ======================================================================
     * 7. تهيئة أزرار الإجراءات
     * ====================================================================== */

    /**
     * ربط أزرار قبول وحظر وتعليق وحذف المستخدمين.
     *
     * @param {Element} usersPage
     */
    function initializeUserActionButtons(
        usersPage
    ) {

        const actionButtons =
            usersPage.querySelectorAll(
                ".action-btn"
            );

        actionButtons.forEach(
            button => {

                button.addEventListener(
                    "click",
                    async function (event) {

                        event.preventDefault();

                        const sourceButton =
                            this;

                        const userId =
                            parseUserId(
                                sourceButton.getAttribute(
                                    "data-id"
                                )
                            );

                        if (!userId) {
                            await showUsersError(
                                T("تعذر تنفيذ الإجراء", "Action could not be completed"),
                                T("معرّف المستخدم غير صالح.", "Invalid user ID.")
                            );

                            return;
                        }

                        const action =
                            resolveUserAction(
                                sourceButton
                            );

                        if (!action) {
                            await showUsersError(
                                T("إجراء غير معروف", "Unknown action"),
                                T("تعذر تحديد العملية المطلوبة لهذا الحساب.", "The requested action could not be determined for this account.")
                            );

                            return;
                        }


                        let isConfirmed = false;


                        /*
                         * SweetAlert متوفر.
                         */

                        if (hasSweetAlert()) {

                            const confirmationResult =
                                await fireUsersAlert(
                                    {
                                        title:
                                            action.title,

                                        text:
                                            action.message,

                                        icon:
                                            action.icon,

                                        showCancelButton:
                                            true,

                                        reverseButtons:
                                            false,

                                        focusCancel:
                                            true,

                                        confirmButtonColor:
                                            action.confirmColor,

                                        cancelButtonColor:
                                            "#64748b",

                                        confirmButtonText:
                                            `<i class="bi bi-check2-circle me-1"></i>${action.confirmText}`,

                                        cancelButtonText:
                                            `<i class="bi bi-arrow-return-left me-1"></i>${"إلغاء"}`,

                                        allowOutsideClick:
                                            true,

                                        allowEscapeKey:
                                            true
                                    },
                                    sourceButton.classList.contains(
                                        "permanent-delete-btn"
                                    )
                                        ? "users-danger-confirm-popup"
                                        : "users-confirm-popup"
                                );

                            isConfirmed =
                                confirmationResult.isConfirmed;
                        }

                        /*
                         * احتياط في حال لم يتم تحميل SweetAlert.
                         */

                        else {
                            isConfirmed =
                                window.confirm(
                                    action.message
                                );
                        }


                        if (!isConfirmed) {
                            return;
                        }


                        await submitUserAction(
                            sourceButton,
                            userId,
                            action
                        );
                    }
                );
            }
        );
    }


    /* ======================================================================
     * 8. تحديث محتوى إدارة المستخدمين عبر AJAX
     * ====================================================================== */

    /**
     * يجلب نسخة حديثة من صفحة المستخدمين ويستبدل محتوى الصفحة فقط
     * بدون إعادة تحميل المتصفح بالكامل.
     *
     * يحافظ على التبويب النشط لأن initializeUsersTabs يقرأه من localStorage.
     *
     * @param {Element|null} currentPage
     * @returns {Promise<Element|null>}
     */
    async function refreshUsersManagementPage(currentPage) {

        if (!currentPage) {
            return null;
        }

        const response =
            await fetch(
                window.location.href,
                {
                    method: "GET",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest",
                        "Accept": "text/html"
                    }
                }
            );

        if (!response.ok) {
            throw new Error(
                T("تم تنفيذ العملية، لكن تعذر تحديث قائمة المستخدمين.", "The action completed, but the user list could not be refreshed.")
            );
        }

        const html =
            await response.text();

        const parsedDocument =
            new DOMParser().parseFromString(
                html,
                "text/html"
            );

        const freshPage =
            parsedDocument.querySelector(
                ".users-management-page"
            );

        if (!freshPage) {
            throw new Error(
                T("تم تنفيذ العملية، لكن تعذر قراءة النسخة المحدثة من الصفحة.", "The action completed, but the refreshed page could not be read.")
            );
        }

        currentPage.replaceWith(
            freshPage
        );

        initializeUsersTabs(
            freshPage
        );

        initializeUserActionButtons(
            freshPage
        );

        initializeVerificationDocuments(
            freshPage
        );

        initializeUsersNotificationDeepLink(
            freshPage
        );

        return freshPage;
    }


    /* ======================================================================
     * 9. إرسال الإجراء إلى الخادم
     * ====================================================================== */

    /**
     * إرسال طلب تغيير حالة المستخدم أو حذفه.
     *
     * يحافظ على نفس جسم الطلب القديم:
     * { Id, State }
     *
     * @param {HTMLButtonElement} sourceButton
     * @param {number} userId
     * @param {Object} action
     */
    async function submitUserAction(
        sourceButton,
        userId,
        action
    ) {

        sourceButton.disabled = true;
        sourceButton.setAttribute(
            "aria-busy",
            "true"
        );

        showUsersLoading(
            action.loadingMessage
        );

        try {

            const response =
                await fetch(
                    action.url,
                    {
                        method:
                            "POST",

                        credentials:
                            "same-origin",

                        headers: {
                            "Content-Type":
                                "application/json",

                            "X-Requested-With":
                                "XMLHttpRequest"
                        },

                        body:
                            JSON.stringify({
                                Id:
                                    userId,

                                State:
                                    action.state
                            })
                    }
                );


            let data;

            try {
                data =
                    await response.json();
            } catch (jsonError) {
                throw new Error(
                    T("استجابة الخادم غير صالحة.", "Invalid server response.")
                );
            }


            if (!response.ok) {
                throw new Error(
                    data?.message ||
                    T("رفض الخادم تنفيذ العملية.", "The server rejected the action.")
                );
            }


            if (!data.success) {

                closeUsersAlert();

                await showUsersError(
                    T("لم تكتمل العملية", "Action not completed"),
                    data.message ||
                    T("تعذر تنفيذ الإجراء المطلوب.", "The requested action could not be completed.")
                );

                return;
            }


            closeUsersAlert();


            if (hasSweetAlert()) {

                await fireUsersAlert(
                    {
                        icon:
                            "success",

                        title:
                            T("تمت العملية بنجاح", "Action completed successfully"),

                        text:
                            data.message ||
                            T("تم تحديث بيانات المستخدم بنجاح.", "User data updated successfully."),

                        timer:
                            1600,

                        timerProgressBar:
                            true,

                        showConfirmButton:
                            false,

                        allowOutsideClick:
                            false,

                        allowEscapeKey:
                            false
                    },
                    "users-success-popup"
                );
            }

            else {
                window.alert(
                    data.message ||
                    T("تمت العملية بنجاح.", "Action completed successfully.")
                );
            }


            await refreshUsersManagementPage(
                sourceButton.closest(".users-management-page")
            );

        } catch (error) {

            console.error(
                "Users action request failed:",
                error
            );

            closeUsersAlert();

            await showUsersError(
                T("خطأ في الاتصال", "Connection error"),
                error?.message ||
                T("تعذر الوصول إلى الخادم، يرجى المحاولة لاحقًا.", "Could not reach the server. Please try again later.")
            );

        } finally {

            sourceButton.disabled = false;

            sourceButton.removeAttribute(
                "aria-busy"
            );
        }
    }


    /* ======================================================================
     * 9. تهيئة أزرار مراجعة الوثائق
     * ====================================================================== */

    /**
     * ربط أزرار مراجعة وثائق المالك.
     *
     * @param {Element} usersPage
     */
    function initializeVerificationDocuments(
        usersPage
    ) {

        const viewDocumentsButtons =
            usersPage.querySelectorAll(
                ".view-docs-btn"
            );

        viewDocumentsButtons.forEach(
            button => {

                button.addEventListener(
                    "click",
                    async function (event) {

                        event.preventDefault();

                        const sourceButton =
                            this;

                        const userId =
                            parseUserId(
                                sourceButton.getAttribute(
                                    "data-id"
                                )
                            );

                        const ownerName =
                            sourceButton.getAttribute(
                                "data-name"
                            ) ||
                            T("مالك المنشأة", "Owner");


                        if (!userId) {
                            await showUsersError(
                                T("تعذر تحميل الوثائق", "Documents could not be loaded"),
                                T("معرّف المستخدم غير صالح.", "Invalid user ID.")
                            );

                            return;
                        }


                        sourceButton.disabled = true;

                        sourceButton.setAttribute(
                            "aria-busy",
                            "true"
                        );


                        showUsersLoading(
                            T("جاري جلب وثائق المالك...", "Loading owner documents...")
                        );


                        try {

                            const response =
                                await fetch(
                                    `/Admin/Users/GetVerificationDocuments?userId=${encodeURIComponent(userId)}`,
                                    {
                                        method:
                                            "GET",

                                        credentials:
                                            "same-origin",

                                        headers: {
                                            "X-Requested-With":
                                                "XMLHttpRequest"
                                        }
                                    }
                                );


                            let data;

                            try {
                                data =
                                    await response.json();
                            } catch (jsonError) {
                                throw new Error(
                                    T("استجابة الخادم غير صالحة.", "Invalid server response.")
                                );
                            }


                            if (!response.ok) {
                                throw new Error(
                                    data?.message ||
                                    T("تعذر جلب وثائق المستخدم.", "User documents could not be loaded.")
                                );
                            }


                            if (!data.success) {

                                closeUsersAlert();

                                await showUsersError(
                                    T("تعذر جلب الوثائق", "Documents could not be loaded"),
                                    data.message ||
                                    T("لم يتم العثور على وثائق هذا المستخدم.", "No documents were found for this user.")
                                );

                                return;
                            }


                            const documentsHtml =
                                buildVerificationDocumentsHtml(
                                    data
                                );


                            closeUsersAlert();


                            if (!hasSweetAlert()) {
                                window.alert(
                                    T("تم جلب الوثائق، لكن مكتبة العرض غير متاحة.", "Documents were loaded, but the viewer library is unavailable.")
                                );

                                return;
                            }


                            await fireUsersAlert(
                                {
                                    title:
                                        `${T("مراجعة وثائق", "Review documents")}: <span class="users-documents-owner-name">${escapeHtml(ownerName)}</span>`,

                                    html:
                                        documentsHtml,

                                    width:
                                        isMobileViewport()
                                            ? "100%"
                                            : "680px",

                                    showCloseButton:
                                        true,

                                    showConfirmButton:
                                        true,

                                    showCancelButton:
                                        false,

                                    showDenyButton:
                                        false,

                                    focusConfirm:
                                        false,

                                    allowOutsideClick:
                                        true,

                                    allowEscapeKey:
                                        true,

                                    confirmButtonText:
                                        `<i class="bi bi-check-circle-fill me-1"></i>${"إغلاق"}`,

                                    confirmButtonColor:
                                        "#05203C",

                                    customClass: {
                                        popup:
                                            "users-documents-popup",

                                        title:
                                            "users-documents-title",

                                        htmlContainer:
                                            "users-documents-html",

                                        confirmButton:
                                            "users-documents-close-button"
                                    },

                                    didOpen:
                                        function (popup) {

                                            const htmlContainer =
                                                popup.querySelector(
                                                    ".swal2-html-container"
                                                );

                                            if (htmlContainer) {
                                                htmlContainer.scrollTop = 0;
                                            }
                                        }
                                },
                                "users-documents-sheet"
                            );

                        } catch (error) {

                            console.error(
                                "Verification documents request failed:",
                                error
                            );

                            closeUsersAlert();

                            await showUsersError(
                                T("خطأ في الاتصال", "Connection error"),
                                error?.message ||
                                T("تعذر الاتصال بالخادم لجلب الوثائق.", "Could not connect to the server to load documents.")
                            );

                        } finally {

                            sourceButton.disabled = false;

                            sourceButton.removeAttribute(
                                "aria-busy"
                            );
                        }
                    }
                );
            }
        );
    }


    /* ======================================================================
     * 10. إنشاء محتوى نافذة الوثائق
     * ====================================================================== */

    /**
     * إنشاء HTML كامل لوثائق المالك.
     *
     * @param {Object} data
     * @returns {string}
     */
    function buildVerificationDocumentsHtml(
        data
    ) {

        const commercialDocument =
            renderVerificationDocument(
                data.commercial,
                T("السجل التجاري", "Commercial register")
            );

        const idFrontDocument =
            renderVerificationDocument(
                data.idFront,
                T("الوجه الأمامي", "Front side")
            );

        const idBackDocument =
            renderVerificationDocument(
                data.idBack,
                T("الوجه الخلفي", "Back side")
            );

        const authorizationDocument =
            data.auth
                ? renderVerificationDocument(
                    data.auth,
                    T("وثيقة التفويض", "Authorization document")
                )
                : "";


        return `
            <div class="users-documents-content">

                <section class="users-document-card users-commercial-document">

                    <h6 class="users-document-card__title">
                        <span class="users-document-card__icon">
                            <i class="bi bi-file-earmark-richtext"
                               aria-hidden="true"></i>
                        </span>
                        <span>${T("السجل التجاري", "Commercial register")}</span>
                    </h6>

                    <div class="text-center users-document-viewer">
                        ${commercialDocument}
                    </div>

                </section>


                <section class="users-document-card users-id-document">

                    <h6 class="users-document-card__title">
                        <span class="users-document-card__icon">
                            <i class="bi bi-person-vcard"
                               aria-hidden="true"></i>
                        </span>
                        <span>${T("البطاقة الشخصية", "Identity document")}</span>
                    </h6>

                    <div class="row g-3 align-items-stretch users-id-documents-grid">

                        <div class="col-6 text-center border-end users-id-document-side">
                            <span class="d-block small text-muted mb-2 fw-bold">
                                ${T("الوجه الأمامي", "Front side")}
                            </span>
                            <div class="users-document-viewer">
                                ${idFrontDocument}
                            </div>
                        </div>

                        <div class="col-6 text-center users-id-document-side">
                            <span class="d-block small text-muted mb-2 fw-bold">
                                ${T("الوجه الخلفي", "Back side")}
                            </span>
                            <div class="users-document-viewer">
                                ${idBackDocument}
                            </div>
                        </div>

                    </div>

                </section>


                ${data.auth
                ? `
                        <section class="users-document-card users-authorization-document">

                            <h6 class="users-document-card__title">
                                <span class="users-document-card__icon">
                                    <i class="bi bi-file-earmark-person"
                                       aria-hidden="true"></i>
                                </span>
                                <span>${T("وثيقة التفويض", "Authorization document")}</span>
                            </h6>

                            <div class="text-center users-document-viewer">
                                ${authorizationDocument}
                            </div>

                        </section>
                    `
                : ""
            }

            </div>
        `;
    }


    /* ======================================================================
     * 11. عرض وثيقة واحدة
     * ====================================================================== */

    /**
     * إنشاء رابط PDF أو صورة وثيقة.
     *
     * @param {unknown} rawUrl
     * @param {string} title
     * @returns {string}
     */
    function renderVerificationDocument(
        rawUrl,
        title
    ) {

        const documentUrl =
            normalizeDocumentUrl(
                rawUrl
            );

        const safeTitle =
            escapeHtml(
                title
            );


        if (!documentUrl) {

            return `
                <div class="users-document-missing d-flex flex-column align-items-center justify-content-center gap-2">

                    <i class="bi bi-file-earmark-x text-danger fs-4"
                       aria-hidden="true"></i>

                    <span class="text-danger small fw-bold">
                        ${T("لم يتم إرفاق", "Not attached")}: ${safeTitle}
                    </span>

                </div>
            `;
        }


        const safeUrl =
            escapeAttribute(
                documentUrl
            );

        const comparableUrl =
            documentUrl
                .split("#")[0]
                .split("?")[0]
                .toLowerCase();

        const isPdf =
            comparableUrl.endsWith(".pdf") ||
            documentUrl
                .toLowerCase()
                .startsWith(
                    "data:application/pdf"
                );


        if (isPdf) {

            return `
                <a href="${safeUrl}"
                   target="_blank"
                   rel="noopener noreferrer"
                   class="btn btn-outline-primary btn-sm w-100 users-document-pdf-link">

                    <i class="bi bi-filetype-pdf"
                       aria-hidden="true"></i>

                    <span>
                        ${T("عرض ملف", "View")}: ${safeTitle} PDF
                    </span>

                    <i class="bi bi-box-arrow-up-left"
                       aria-hidden="true"></i>

                </a>
            `;
        }


        return `
            <a href="${safeUrl}"
               target="_blank"
               rel="noopener noreferrer"
               class="users-document-image-link"
               title="${T("فتح بالحجم الكامل", "Open full size")}: ${safeTitle}">

                <img src="${safeUrl}"
                     alt="${safeTitle}"
                     loading="lazy"
                     decoding="async"
                     class="users-document-image" />

            </a>
        `;
    }

})();