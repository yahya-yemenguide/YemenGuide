/**
 * =============================================================
 * CUSTOMER SIDEBAR LOGIC
 * العنوان الديناميكي، الطي على الكمبيوتر، وحفظ حالة العرض
 * =============================================================
 */

(function () {
    'use strict';

    const DESKTOP_QUERY = '(min-width: 1200px)';
    const COMPACT_STORAGE_KEY = 'customerCompactMode';
    const COMPACT_COOKIE_KEY = 'compactMode_customer';
    const PREFERENCE_ENDPOINT = '/Profile/SetPreference';
    const INACTIVITY_TIMEOUT = 7000;

    /**
     * حفظ Cookie.
     * @param {string} name
     * @param {string} value
     */
    function setCookie(name, value) {
        const securePart =
            window.location.protocol === 'https:' ? '; Secure' : '';

        document.cookie =
            `${encodeURIComponent(name)}=${encodeURIComponent(value)}` +
            `; path=/; max-age=31536000; SameSite=Lax${securePart}`;
    }

    /**
     * إرسال حالة الطي إلى السيرفر.
     * @param {boolean} isCollapsed
     * @returns {Promise<void>}
     */
    async function saveCompactPreference(isCollapsed) {
        try {
            const response = await fetch(PREFERENCE_ENDPOINT, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    Key: 'CompactMode',
                    Value: isCollapsed
                })
            });

            if (!response.ok) {
                console.warn(
                    `تعذر حفظ وضع القائمة. رمز الاستجابة: ${response.status}`
                );
            }
        } catch (error) {
            console.warn(
                'تعذر حفظ وضع القائمة مؤقتاً.',
                error
            );
        }
    }

    /**
     * استخراج نص الرابط لاستخدامه كـTooltip في وضع التابلت.
     * @param {HTMLElement} sidebar
     */
    function addSidebarTooltips(sidebar) {
        sidebar
            .querySelectorAll(
                '.custom-sidebar-nav .nav-link, .logout-wrapper .nav-link'
            )
            .forEach(link => {
                const textElement =
                    link.querySelector('.nav-text');

                const text =
                    textElement?.textContent?.trim();

                if (text && !link.hasAttribute('title')) {
                    link.setAttribute('title', text);
                }
            });
    }

    /**
     * تحديث عنوان التبويب من الرابط النشط.
     */
    function updateDynamicTitle() {
        const dynamicTitle =
            document.getElementById('dynamic-tab-title');

        const activeLink =
            document.querySelector(
                '.custom-sidebar-nav .nav-link.active'
            );

        if (!dynamicTitle || !activeLink) {
            return;
        }

        const iconElement =
            activeLink.querySelector('i');

        const textElement =
            activeLink.querySelector('.nav-text');

        if (!iconElement || !textElement) {
            return;
        }

        const titleIcon =
            document.createElement('i');

        titleIcon.className = iconElement.className;
        titleIcon.classList.remove('me-3', 'ms-3');
        titleIcon.classList.add('me-2', 'text-primary');

        const titleText =
            document.createElement('span');

        titleText.className = 'nav-text';
        titleText.textContent =
            textElement.textContent?.trim() || 'لوحة التحكم';

        dynamicTitle.replaceChildren(
            titleIcon,
            titleText
        );
    }

    /**
     * تهيئة القائمة.
     */
    function initCustomerSidebar() {
        const sidebar =
            document.getElementById('customerSidebar');

        const toggleButton =
            document.getElementById('customerSidebarToggle');

        if (!sidebar) {
            return;
        }

        updateDynamicTitle();
        addSidebarTooltips(sidebar);

        if (!toggleButton) {
            return;
        }

        const desktopMedia =
            window.matchMedia(DESKTOP_QUERY);

        let inactivityTimer = null;

        /**
         * هل الواجهة الآن كمبيوتر كامل؟
         * @returns {boolean}
         */
        const isDesktop = () => desktopMedia.matches;

        /**
         * قراءة الحالة المحفوظة.
         * @returns {boolean}
         */
        const getSavedCollapsedState = () =>
            localStorage.getItem(COMPACT_STORAGE_KEY) ===
            'enabled';

        /**
         * تحديث حالة زر الطي.
         * @param {boolean} isCollapsed
         */
        const updateToggleAccessibility = isCollapsed => {
            toggleButton.setAttribute(
                'aria-expanded',
                isCollapsed ? 'false' : 'true'
            );

            toggleButton.setAttribute(
                'aria-label',
                isCollapsed
                    ? 'توسيع القائمة الجانبية'
                    : 'طي القائمة الجانبية'
            );

            toggleButton.title =
                isCollapsed
                    ? 'توسيع القائمة'
                    : 'طي القائمة';
        };

        /**
         * تطبيق الحالة بصرياً.
         * @param {boolean} isCollapsed
         */
        const applyCollapsedState = isCollapsed => {
            sidebar.classList.toggle(
                'sidebar-collapsed',
                isDesktop() && isCollapsed
            );

            updateToggleAccessibility(
                isDesktop() && isCollapsed
            );
        };

        /**
         * حفظ الحالة محلياً وفي السيرفر.
         * @param {boolean} isCollapsed
         */
        const persistCollapsedState = isCollapsed => {
            localStorage.setItem(
                COMPACT_STORAGE_KEY,
                isCollapsed ? 'enabled' : 'disabled'
            );

            setCookie(
                COMPACT_COOKIE_KEY,
                isCollapsed ? 'enabled' : 'disabled'
            );

            void saveCompactPreference(isCollapsed);
        };

        /**
         * إيقاف مؤقت الخمول.
         */
        const clearInactivityTimer = () => {
            if (inactivityTimer !== null) {
                window.clearTimeout(inactivityTimer);
                inactivityTimer = null;
            }
        };

        /**
         * طي القائمة بعد الخمول.
         */
        const collapseOnInactivity = () => {
            inactivityTimer = null;

            if (
                !isDesktop() ||
                sidebar.classList.contains('sidebar-collapsed')
            ) {
                return;
            }

            applyCollapsedState(true);
            persistCollapsedState(true);
        };

        /**
         * إعادة تشغيل مؤقت الخمول.
         */
        const resetInactivityTimer = () => {
            clearInactivityTimer();

            if (
                !isDesktop() ||
                sidebar.classList.contains('sidebar-collapsed')
            ) {
                return;
            }

            inactivityTimer =
                window.setTimeout(
                    collapseOnInactivity,
                    INACTIVITY_TIMEOUT
                );
        };

        /**
         * مزامنة الواجهة عند الانتقال بين الكمبيوتر والتابلت.
         */
        const handleViewportChange = () => {
            clearInactivityTimer();

            if (isDesktop()) {
                applyCollapsedState(
                    getSavedCollapsedState()
                );

                resetInactivityTimer();
            } else {
                // التابلت له شريط أيقونات ثابت من CSS،
                // لذلك لا نحمل عليه حالة طي الكمبيوتر.
                sidebar.classList.remove(
                    'sidebar-collapsed'
                );

                updateToggleAccessibility(false);
            }
        };

        toggleButton.addEventListener('click', event => {
            event.preventDefault();

            if (!isDesktop()) {
                return;
            }

            const nextCollapsedState =
                !sidebar.classList.contains(
                    'sidebar-collapsed'
                );

            applyCollapsedState(nextCollapsedState);
            persistCollapsedState(nextCollapsedState);

            if (nextCollapsedState) {
                clearInactivityTimer();
            } else {
                resetInactivityTimer();
            }
        });

        const userActivityEvents = [
            'pointermove',
            'pointerdown',
            'keydown',
            'touchstart',
            'scroll'
        ];

        userActivityEvents.forEach(eventName => {
            document.addEventListener(
                eventName,
                resetInactivityTimer,
                { passive: true }
            );
        });

        sidebar.addEventListener(
            'mouseenter',
            clearInactivityTimer
        );

        sidebar.addEventListener(
            'mouseleave',
            resetInactivityTimer
        );

        if (
            typeof desktopMedia.addEventListener ===
            'function'
        ) {
            desktopMedia.addEventListener(
                'change',
                handleViewportChange
            );
        } else {
            // دعم المتصفحات القديمة.
            desktopMedia.addListener(
                handleViewportChange
            );
        }

        document.addEventListener(
            'visibilitychange',
            () => {
                if (document.hidden) {
                    clearInactivityTimer();
                } else {
                    resetInactivityTimer();
                }
            }
        );

        handleViewportChange();
    }

    document.addEventListener(
        'DOMContentLoaded',
        initCustomerSidebar,
        { once: true }
    );
})();
