/**
 * =============================================================
 * CUSTOMER LAYOUT SCRIPTS
 * الهيدر، السكرول، الوضع الليلي، الإشعارات، والوظائف العامة
 * =============================================================
 */

(function () {
    'use strict';

    const CUSTOMER_THEME_COOKIE = 'darkMode';
    const CUSTOMER_THEME_ENDPOINT = '/Profile/SetPreference';

    /**
     * قراءة قيمة Cookie بالاسم.
     * @param {string} name
     * @returns {string|null}
     */
    function getCookie(name) {
        const cookieName = `${encodeURIComponent(name)}=`;
        const cookies = document.cookie ? document.cookie.split(';') : [];

        for (const rawCookie of cookies) {
            const cookie = rawCookie.trim();

            if (cookie.startsWith(cookieName)) {
                return decodeURIComponent(cookie.substring(cookieName.length));
            }
        }

        return null;
    }

    /**
     * حفظ Cookie على مستوى الموقع.
     * @param {string} name
     * @param {string} value
     * @param {number} maxAge
     */
    function setCookie(name, value, maxAge) {
        const securePart = window.location.protocol === 'https:' ? '; Secure' : '';

        document.cookie =
            `${encodeURIComponent(name)}=${encodeURIComponent(value)}` +
            `; path=/; max-age=${maxAge}; SameSite=Lax${securePart}`;
    }

    /**
     * إرسال التفضيل إلى السيرفر بدون تعطيل الواجهة عند فشل الشبكة.
     * @param {string} key
     * @param {boolean|string|number} value
     * @returns {Promise<void>}
     */
    async function savePreference(key, value) {
        try {
            const response = await fetch(CUSTOMER_THEME_ENDPOINT, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    Key: key,
                    Value: value
                })
            });

            if (!response.ok) {
                console.warn(
                    `تعذر حفظ التفضيل "${key}". رمز الاستجابة: ${response.status}`
                );
            }
        } catch (error) {
            console.warn(`تعذر حفظ التفضيل "${key}" مؤقتاً.`, error);
        }
    }

    /**
     * مدير الوضع الليلي الموحد لكل واجهات العميل.
     */
    const customerTheme = {
        switchSelectors: [
            '#profileDarkModeSwitch',
            '#customerBottomDarkModeSwitch'
        ],

        /**
         * جلب جميع مفاتيح الوضع الليلي الموجودة حالياً.
         * @returns {HTMLInputElement[]}
         */
        getSwitches() {
            return this.switchSelectors
                .map(selector => document.querySelector(selector))
                .filter(element => element instanceof HTMLInputElement);
        },

        /**
         * مزامنة حالة جميع المفاتيح.
         * @param {boolean} enabled
         */
        syncSwitches(enabled) {
            this.getSwitches().forEach(themeSwitch => {
                themeSwitch.checked = enabled;
                themeSwitch.setAttribute(
                    'aria-checked',
                    enabled ? 'true' : 'false'
                );
            });
        },

        /**
         * تطبيق الوضع الليلي وحفظه اختيارياً.
         * @param {boolean} enabled
         * @param {{ persist?: boolean }} options
         */
        setDarkMode(enabled, options = {}) {
            const shouldPersist = options.persist !== false;

            document.body.classList.toggle('dark-mode', enabled);
            document.documentElement.style.colorScheme =
                enabled ? 'dark' : 'light';

            this.syncSwitches(enabled);

            if (shouldPersist) {
                setCookie(
                    CUSTOMER_THEME_COOKIE,
                    enabled ? 'enabled' : 'disabled',
                    31536000
                );

                void savePreference('DarkModeEnabled', enabled);
            }

            window.dispatchEvent(
                new CustomEvent('customer:theme-changed', {
                    detail: { enabled }
                })
            );
        },

        /**
         * تهيئة الوضع من الـCookie وربط المفاتيح.
         */
        init() {
            // إزالة المفتاح القديم المشترك حتى لا يتعارض مع نظام العميل الحالي.
            localStorage.removeItem('darkMode');

            const isDark =
                getCookie(CUSTOMER_THEME_COOKIE) === 'enabled';

            this.setDarkMode(isDark, { persist: false });

            this.getSwitches().forEach(themeSwitch => {
                if (themeSwitch.dataset.customerThemeBound === 'true') {
                    return;
                }

                themeSwitch.dataset.customerThemeBound = 'true';

                themeSwitch.addEventListener('change', event => {
                    const target = event.currentTarget;

                    if (target instanceof HTMLInputElement) {
                        this.setDarkMode(target.checked);
                    }
                });
            });
        }
    };

    // متاح لملف الشريط السفلي وأي صفحة عميل تحتاج تغيير المظهر.
    window.CustomerTheme = customerTheme;

    /**
     * تهيئة الهيدر وتأثير التمرير.
     */
    function initNavbar() {
        const navbar = document.getElementById('mainNavbar');

        if (!navbar) {
            return;
        }

        const path = window.location.pathname
            .replace(/\/+$/, '')
            .toLowerCase();

        const isHomePage =
            path === '' ||
            path === '/' ||
            path === '/home' ||
            path === '/home/index';

        const isCustomerDashboard =
            navbar.dataset.customerDashboard === 'true' ||
            document.body.dataset.customerDashboard === 'true';

        if (!isHomePage || isCustomerDashboard) {
            navbar.classList.add('navbar-solid');
        }

        let scrollFrameRequested = false;

        const updateNavbarState = () => {
            navbar.classList.toggle(
                'scrolled',
                window.scrollY > 50
            );

            scrollFrameRequested = false;
        };

        const requestNavbarUpdate = () => {
            if (scrollFrameRequested) {
                return;
            }

            scrollFrameRequested = true;
            window.requestAnimationFrame(updateNavbarState);
        };

        window.addEventListener('scroll', requestNavbarUpdate, {
            passive: true
        });

        updateNavbarState();
    }


    /**
     * تهيئة هيدر YemenGuide المميز:
     * - الضوء المتحرك خلف رابط التنقل النشط.
     * - تفاعل الإضاءة مع موضع المؤشر على الكمبيوتر.
     * - إعادة تموضع الـglider بعد تغيير المقاس/فتح القائمة.
     * لا يتدخل في روابط Bootstrap أو منطق الإشعارات.
     */
    function initSignatureHeader() {
        const navbar =
            document.getElementById('mainNavbar');

        if (!navbar ||
            !navbar.classList.contains(
                'customer-signature-header'
            )) {
            return;
        }

        const navShell =
            document.getElementById(
                'customerSignatureNavShell'
            );

        const nav =
            document.getElementById(
                'customerSignatureNav'
            );

        const glider =
            document.getElementById(
                'customerNavActiveGlider'
            );

        const links =
            nav
                ? Array.from(
                    nav.querySelectorAll(
                        '[data-signature-nav]'
                    )
                )
                : [];

        const reduceMotion =
            window.matchMedia(
                '(prefers-reduced-motion: reduce)'
            ).matches;

        const hasFinePointer =
            window.matchMedia(
                '(pointer: fine)'
            ).matches;

        let currentLink =
            links.find(link =>
                link.classList.contains(
                    'is-current'
                )
            ) || null;

        const setGlider = (
            target,
            options = {}
        ) => {
            if (
                !navShell ||
                !glider ||
                !target ||
                window.innerWidth < 1200
            ) {
                glider?.classList.remove(
                    'is-visible'
                );
                return;
            }

            const shellRect =
                navShell.getBoundingClientRect();

            const targetRect =
                target.getBoundingClientRect();

            const x =
                targetRect.left -
                shellRect.left;

            if (
                options.immediate === true ||
                reduceMotion
            ) {
                const previousTransition =
                    glider.style.transition;

                glider.style.transition =
                    'none';

                glider.style.setProperty(
                    '--yg-glider-x',
                    `${Math.round(x)}px`
                );

                glider.style.setProperty(
                    '--yg-glider-w',
                    `${Math.round(targetRect.width)}px`
                );

                glider.classList.add(
                    'is-visible'
                );

                requestAnimationFrame(() => {
                    glider.style.transition =
                        previousTransition;
                });

                return;
            }

            glider.style.setProperty(
                '--yg-glider-x',
                `${Math.round(x)}px`
            );

            glider.style.setProperty(
                '--yg-glider-w',
                `${Math.round(targetRect.width)}px`
            );

            glider.classList.add(
                'is-visible'
            );
        };

        const returnToCurrent = () => {
            if (currentLink) {
                setGlider(currentLink);
            } else {
                glider?.classList.remove(
                    'is-visible'
                );
            }
        };

        links.forEach(link => {
            link.addEventListener(
                'mouseenter',
                () => {
                    setGlider(link);
                }
            );

            link.addEventListener(
                'focus',
                () => {
                    setGlider(link);
                }
            );

            link.addEventListener(
                'click',
                () => {
                    navbar.classList.add(
                        'is-signature-committing'
                    );

                    window.setTimeout(
                        () => {
                            navbar.classList.remove(
                                'is-signature-committing'
                            );
                        },
                        620
                    );
                }
            );
        });

        navShell?.addEventListener(
            'mouseleave',
            returnToCurrent
        );

        navShell?.addEventListener(
            'focusout',
            event => {
                const nextTarget =
                    event.relatedTarget;

                if (
                    !(nextTarget instanceof Element) ||
                    !navShell.contains(
                        nextTarget
                    )
                ) {
                    returnToCurrent();
                }
            }
        );
        // أداء: أوقفنا تتبع المؤشر داخل الهيدر؛ hover/focus ما زال يعمل.

        const refreshGlider = (
            immediate = false
        ) => {
            if (currentLink) {
                setGlider(
                    currentLink,
                    { immediate }
                );
            }
        };

        let resizeFrame = 0;

        window.addEventListener(
            'resize',
            () => {
                if (resizeFrame) {
                    return;
                }

                resizeFrame =
                    window.requestAnimationFrame(
                        () => {
                            refreshGlider(true);
                            resizeFrame = 0;
                        }
                    );
            },
            {
                passive: true
            }
        );

        const offcanvas =
            document.getElementById(
                'offcanvasNavbar'
            );

        offcanvas?.addEventListener(
            'hidden.bs.offcanvas',
            () => {
                refreshGlider(true);
            }
        );

        const exploreDropdown =
            nav?.querySelector(
                '.nav-item.dropdown'
            );

        exploreDropdown?.addEventListener(
            'shown.bs.dropdown',
            () => {
                const trigger =
                    exploreDropdown.querySelector(
                        '[data-signature-nav]'
                    );

                if (trigger) {
                    setGlider(trigger);
                }
            }
        );

        exploreDropdown?.addEventListener(
            'hidden.bs.dropdown',
            returnToCurrent
        );

        requestAnimationFrame(
            () => {
                refreshGlider(true);
            }
        );

        window.setTimeout(
            () => {
                refreshGlider(true);
            },
            250
        );
    }


    /**
     * حركة القائمة الجانبية المميزة.
     * تعتمد على أحداث Bootstrap فقط ولا تغيّر وظيفة أي رابط.
     */
    function initSignatureDrawer() {
        const drawer =
            document.getElementById(
                'offcanvasNavbar'
            );

        if (
            !drawer ||
            !drawer.classList.contains(
                'customer-signature-drawer'
            )
        ) {
            return;
        }

        const closeButton =
            drawer.querySelector(
                '[data-signature-drawer-close]'
            );

        const getBootstrapInstance = () => {
            const Offcanvas =
                window.bootstrap?.Offcanvas;

            if (!Offcanvas) {
                return null;
            }

            return (
                Offcanvas.getInstance(drawer) ||
                Offcanvas.getOrCreateInstance(drawer)
            );
        };

        const hideDrawer = event => {
            event?.preventDefault();
            event?.stopPropagation();

            const instance =
                getBootstrapInstance();

            if (instance) {
                instance.hide();
                return;
            }

            /*
             * Fallback إذا Bootstrap JS لم يكن متاحاً لأي سبب:
             * نعيد زر الإغلاق الأصلي كسلوك آمن.
             */
            drawer.classList.remove(
                'show'
            );

            drawer.style.visibility =
                'hidden';

            document.body.classList.remove(
                'offcanvas-open'
            );

            document
                .querySelectorAll(
                    '.offcanvas-backdrop'
                )
                .forEach(backdrop =>
                    backdrop.remove()
                );
        };

        closeButton?.addEventListener(
            'click',
            hideDrawer
        );

        closeButton?.addEventListener(
            'pointerup',
            event => {
                if (
                    event.pointerType ===
                    'touch'
                ) {
                    hideDrawer(event);
                }
            }
        );

        const replayEntrance = () => {
            drawer.classList.remove(
                'is-signature-opening'
            );

            requestAnimationFrame(
                () => {
                    drawer.classList.add(
                        'is-signature-opening'
                    );
                }
            );
        };

        drawer.addEventListener(
            'show.bs.offcanvas',
            replayEntrance
        );

        drawer.addEventListener(
            'shown.bs.offcanvas',
            () => {
                window.setTimeout(
                    () => {
                        drawer.classList.remove(
                            'is-signature-opening'
                        );
                    },
                    760
                );
            }
        );

        drawer.addEventListener(
            'hidden.bs.offcanvas',
            () => {
                drawer.classList.remove(
                    'is-signature-opening'
                );

                /*
                 * نتأكد أن أي ستايل fallback سابق لا يبقى.
                 */
                drawer.style.removeProperty(
                    'visibility'
                );
            }
        );
    }

    /**
     * تأثير زر إضافة المنشأة، ويعمل فقط على الأجهزة التي تملك مؤشراً دقيقاً.
     */
    function initMagneticButton() {
        // معطل للأداء: الزر ووظيفته باقية بدون تتبع mousemove.
    }

    /**
     * تهيئة كبسولة إكمال الملف الشخصي.
     */
    function initProfileHint() {
        const profileHint =
            document.getElementById('headerProfileHint');

        const profileWrapper =
            document.getElementById('headerProfileWrapper');

        if (!profileHint || !profileWrapper) {
            return;
        }

        let hideTimer = null;
        let isHidden = false;

        const hideHint = () => {
            if (isHidden) {
                return;
            }

            isHidden = true;
            profileHint.style.opacity = '0';
            profileHint.style.pointerEvents = 'none';
            profileHint.style.animation = 'none';

            if (hideTimer) {
                window.clearTimeout(hideTimer);
            }

            window.setTimeout(() => {
                profileHint.hidden = true;
            }, 420);
        };

        profileWrapper.addEventListener('click', hideHint);

        profileHint.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                profileHint.click();
            }
        });

        hideTimer = window.setTimeout(hideHint, 14000);
    }

    /**
     * زر العودة إلى أعلى الصفحة.
     */
    function initBackToTop() {
        const backToTopButton =
            document.getElementById('btn-back-to-top');

        if (!backToTopButton) {
            return;
        }

        let scrollFrameRequested = false;

        const updateButtonState = () => {
            const currentScroll =
                window.scrollY ||
                document.documentElement.scrollTop ||
                document.body.scrollTop ||
                0;

            const isAccountSheetOpen =
                document.body.classList.contains(
                    'customer-sheet-open'
                );

            const shouldShow =
                currentScroll > 320 &&
                !isAccountSheetOpen;

            backToTopButton.classList.toggle(
                'show-btn',
                shouldShow
            );

            backToTopButton.setAttribute(
                'aria-hidden',
                shouldShow ? 'false' : 'true'
            );

            scrollFrameRequested = false;
        };

        const requestButtonUpdate = () => {
            if (scrollFrameRequested) {
                return;
            }

            scrollFrameRequested = true;
            window.requestAnimationFrame(updateButtonState);
        };

        window.addEventListener('scroll', requestButtonUpdate, {
            passive: true
        });

        backToTopButton.addEventListener('click', () => {
            window.scrollTo({
                top: 0,
                behavior: window.matchMedia(
                    '(prefers-reduced-motion: reduce)'
                ).matches
                    ? 'auto'
                    : 'smooth'
            });
        });

        window.addEventListener(
            'customer:account-sheet-opened',
            updateButtonState
        );

        window.addEventListener(
            'customer:account-sheet-closed',
            updateButtonState
        );

        updateButtonState();
    }

    /**
     * قائمة الموقع العامة Offcanvas.
     * لا تُغلق روابط Dropdown تلقائياً حتى تعمل قائمة "استكشف" بشكل صحيح.
     */
    function initPublicOffcanvas() {
        const offcanvasElement =
            document.getElementById('offcanvasNavbar');

        if (!offcanvasElement) {
            return;
        }

        const closeButton =
            offcanvasElement.querySelector('.btn-close');

        if (!closeButton) {
            return;
        }

        closeButton.addEventListener('click', () => {
            if (
                typeof window.bootstrap === 'undefined' ||
                typeof window.bootstrap.Offcanvas === 'undefined'
            ) {
                return;
            }

            const instance =
                window.bootstrap.Offcanvas.getOrCreateInstance(
                    offcanvasElement
                );

            instance.hide();
        });
    }

    /**
     * منع بقاء Dropdown قديم مفتوح بعد الانتقال في سجل المتصفح.
     */
    function resetTransientHeaderState() {
        document
            .querySelectorAll(
                '.custom-navbar .dropdown-menu.show'
            )
            .forEach(menu => {
                menu.classList.remove('show');
            });

        document
            .querySelectorAll(
                '.custom-navbar [aria-expanded="true"]'
            )
            .forEach(toggle => {
                toggle.setAttribute('aria-expanded', 'false');
                toggle.classList.remove('show');
            });
    }


    /**
     * =============================================================
     * مركز الإشعارات المركزي
     * =============================================================
     */

    const customerNotifications = {
        center: null,
        toggle: null,
        panel: null,
        body: null,
        badge: null,
        liveDot: null,
        unreadPill: null,
        unreadText: null,
        markAllButton: null,
        refreshButton: null,
        progress: null,
        footText: null,
        antiForgeryToken: null,
        initialized: false,
        loading: false,
        loadedOnce: false,
        unreadCount: 0,
        lastKnownUnreadCount: null,
        pollTimer: null,
        activeController: null,

        endpoints: {
            header: '/Notifications/Header',
            unread: '/Notifications/UnreadCount',
            markRead: '/Notifications/MarkAsRead',
            markAll: '/Notifications/MarkAllAsRead'
        },

        typePresentation: {
            bookingcreated: { icon: 'bi-calendar-plus-fill', tone: 'cyan', label: 'حجز' },
            bookingaccepted: { icon: 'bi-calendar-check-fill', tone: 'green', label: 'حجز' },
            bookingconfirmed: { icon: 'bi-patch-check-fill', tone: 'green', label: 'حجز' },
            bookingrejected: { icon: 'bi-x-circle-fill', tone: 'coral', label: 'حجز' },
            bookingcancelled: { icon: 'bi-calendar-x-fill', tone: 'coral', label: 'حجز' },
            bookingexpired: { icon: 'bi-hourglass-split', tone: 'amber', label: 'حجز' },
            bookingcompleted: { icon: 'bi-check2-circle', tone: 'green', label: 'حجز' },
            bookingnoshow: { icon: 'bi-person-x-fill', tone: 'amber', label: 'حجز' },

            paymentrequired: { icon: 'bi-wallet2', tone: 'amber', label: 'دفع' },
            paymentreceived: { icon: 'bi-receipt-cutoff', tone: 'cyan', label: 'دفع' },
            paymentapproved: { icon: 'bi-check-circle-fill', tone: 'green', label: 'دفع' },
            paymentrejected: { icon: 'bi-exclamation-circle-fill', tone: 'coral', label: 'دفع' },
            refundrequested: { icon: 'bi-arrow-counterclockwise', tone: 'amber', label: 'استرداد' },
            refundreadyfortransfer: { icon: 'bi-lightning-charge-fill', tone: 'green', label: 'استرداد' },
            refundtransferred: { icon: 'bi-send-check-fill', tone: 'cyan', label: 'استرداد' },
            refundcompleted: { icon: 'bi-cash-coin', tone: 'green', label: 'استرداد' },

            reviewcreated: { icon: 'bi-star-fill', tone: 'coral', label: 'تقييم' },
            reviewreply: { icon: 'bi-chat-square-quote-fill', tone: 'coral', label: 'تقييم' },

            reportreply: { icon: 'bi-shield-check', tone: 'cyan', label: 'بلاغ' },
            reportcreated: { icon: 'bi-shield-exclamation', tone: 'amber', label: 'بلاغ' },
            reportescalated: { icon: 'bi-arrow-up-circle-fill', tone: 'amber', label: 'بلاغ' },
            reportwithdrawn: { icon: 'bi-arrow-return-left', tone: 'slate', label: 'بلاغ' },
            reportresolved: { icon: 'bi-check2-circle', tone: 'green', label: 'بلاغ' },
            reportrejected: { icon: 'bi-shield-x', tone: 'coral', label: 'بلاغ' },

            message: { icon: 'bi-chat-dots-fill', tone: 'cyan', label: 'رسالة' },
            chatmessage: { icon: 'bi-chat-dots-fill', tone: 'cyan', label: 'رسالة' },

            promotion: { icon: 'bi-percent', tone: 'coral', label: 'عرض' },
            promotionstarted: { icon: 'bi-fire', tone: 'coral', label: 'عرض' },

            account: { icon: 'bi-person-check-fill', tone: 'cyan', label: 'الحساب' },
            security: { icon: 'bi-shield-lock-fill', tone: 'amber', label: 'أمان' },

            general: { icon: 'bi-bell-fill', tone: 'slate', label: 'إشعار' }
        },

        init() {
            this.center = document.getElementById('customerNotificationCenter');

            if (!this.center || this.initialized) {
                return;
            }

            this.initialized = true;

            this.toggle = document.getElementById('customerNotificationToggle');
            this.panel = document.getElementById('customerNotificationPanel');
            this.body = document.getElementById('customerNotificationBody');
            this.badge = document.getElementById('customerNotificationBadge');
            this.liveDot = document.getElementById('customerNotificationLiveDot');
            this.unreadPill = document.getElementById('customerNotificationUnreadPill');
            this.unreadText = document.getElementById('customerNotificationUnreadText');
            this.markAllButton = document.getElementById('customerNotificationMarkAll');
            this.refreshButton = document.getElementById('customerNotificationRefresh');
            this.progress = document.getElementById('customerNotificationProgress');
            this.footText = document.getElementById('customerNotificationFootText');

            this.endpoints.header =
                this.center.dataset.headerEndpoint || this.endpoints.header;

            this.endpoints.unread =
                this.center.dataset.unreadEndpoint || this.endpoints.unread;

            this.endpoints.markRead =
                this.center.dataset.markReadEndpoint || this.endpoints.markRead;

            this.endpoints.markAll =
                this.center.dataset.markAllEndpoint || this.endpoints.markAll;

            const tokenInput =
                this.center.querySelector('input[name="__RequestVerificationToken"]');

            this.antiForgeryToken =
                tokenInput instanceof HTMLInputElement
                    ? tokenInput.value
                    : null;

            this.bindEvents();
            this.showSkeleton();

            const isNotificationCenterPage =
                Boolean(document.getElementById('notificationCenterPage'));

            /*
             * داخل مركز الإشعارات لا نرسل طلب Header إضافياً فور فتح الصفحة.
             * القائمة المنسدلة ستجلب آخر 7 فقط عندما يفتح المستخدم الجرس.
             * هذا يمنع طلباً شبكياً غير ضروري أثناء التنقل بين التبويبات.
             */
            if (!isNotificationCenterPage) {
                void this.load({ animateNew: true });
            }

            this.startPolling();
        },

        bindEvents() {
            if (this.toggle) {
                this.toggle.addEventListener('shown.bs.dropdown', () => {
                    void this.load({ animateNew: false, force: true });
                });

                // بعض إصدارات Bootstrap تطلق الحدث على العنصر الأب.
                this.center.addEventListener('shown.bs.dropdown', () => {
                    void this.load({ animateNew: false, force: true });
                });
            }

            if (this.refreshButton) {
                this.refreshButton.addEventListener('click', event => {
                    event.preventDefault();
                    event.stopPropagation();
                    void this.load({ animateNew: false, force: true });
                });
            }

            if (this.markAllButton) {
                this.markAllButton.addEventListener('click', event => {
                    event.preventDefault();
                    event.stopPropagation();
                    void this.markAllAsRead();
                });
            }

            if (this.body) {
                this.body.addEventListener('click', event => {
                    const retryButton =
                        event.target.closest('[data-notification-retry]');

                    if (retryButton) {
                        event.preventDefault();
                        void this.load({ animateNew: false, force: true });
                        return;
                    }

                    const item =
                        event.target.closest('[data-notification-id]');

                    if (!(item instanceof HTMLAnchorElement)) {
                        return;
                    }

                    // نسمح للرابط المركزي أن يتولى القراءة والتحويل.
                    // الجرس يعرض غير المقروء فقط، لذلك نخفي البطاقة فور الضغط
                    // بينما تبقى محفوظة في مركز "جميع الإشعارات".
                    if (item.classList.contains('is-unread')) {
                        this.setUnreadCount(Math.max(0, this.unreadCount - 1), {
                            celebrate: false
                        });

                        window.setTimeout(() => {
                            item.remove();

                            if (
                                this.body &&
                                !this.body.querySelector('[data-notification-id]')
                            ) {
                                this.body.innerHTML = this.emptyStateTemplate();
                                this.setFootMessage('كل شيء هادئ الآن');
                            }
                        }, 0);
                    }
                });
            }

            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible') {
                    void this.refreshUnreadCount();
                    this.startPolling();
                } else {
                    this.stopPolling();
                }
            });

            window.addEventListener('pagehide', () => {
                this.stopPolling();
                this.activeController?.abort();
            });
        },

        startPolling() {
            this.stopPolling();

            if (document.visibilityState !== 'visible') {
                return;
            }

            this.pollTimer = window.setInterval(() => {
                void this.refreshUnreadCount();
            }, 60000);
        },

        stopPolling() {
            if (this.pollTimer) {
                window.clearInterval(this.pollTimer);
                this.pollTimer = null;
            }
        },

        async refreshUnreadCount() {
            try {
                const response = await fetch(this.endpoints.unread, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    cache: 'no-store'
                });

                if (!response.ok) {
                    return;
                }

                const data = await response.json();
                const count =
                    this.toNonNegativeInteger(
                        data.unreadCount ?? data.UnreadCount
                    );

                const shouldCelebrate =
                    this.lastKnownUnreadCount !== null &&
                    count > this.lastKnownUnreadCount;

                this.setUnreadCount(count, {
                    celebrate: shouldCelebrate
                });

                this.lastKnownUnreadCount = count;
            } catch {
                // polling صامت حتى لا يزعج المستخدم عند ضعف الشبكة.
            }
        },

        async load(options = {}) {
            if (this.loading && !options.force) {
                return;
            }

            if (this.activeController) {
                this.activeController.abort();
            }

            this.activeController = new AbortController();
            this.loading = true;
            this.setLoadingState(true);

            if (!this.loadedOnce) {
                this.showSkeleton();
            }

            try {
                const response = await fetch(`${this.endpoints.header}?take=7`, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    cache: 'no-store',
                    signal: this.activeController.signal
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();
                const rawItems = Array.isArray(data.items)
                    ? data.items
                    : (Array.isArray(data.Items) ? data.Items : []);

                // جرس الهيدر مخصص لغير المقروء فقط. هذا فلتر حماية إضافي
                // حتى لو أعاد Endpoint قديم عنصرًا مقروءًا بالخطأ.
                const items = rawItems.filter(rawItem =>
                    !this.normalizeItem(rawItem).isRead
                );

                const count =
                    this.toNonNegativeInteger(
                        data.unreadCount ?? data.UnreadCount
                    );

                const shouldCelebrate =
                    options.animateNew === true &&
                    (
                        !this.loadedOnce
                            ? count > 0
                            : (
                                this.lastKnownUnreadCount !== null &&
                                count > this.lastKnownUnreadCount
                            )
                    );

                this.setUnreadCount(count, {
                    celebrate: shouldCelebrate
                });

                this.lastKnownUnreadCount = count;
                this.renderItems(items);
                this.loadedOnce = true;
                this.setFootMessage(
                    items.length > 0
                        ? `${items.length} إشعارات غير مقروءة • تحديث تلقائي`
                        : 'كل شيء هادئ الآن'
                );
            } catch (error) {
                if (error?.name === 'AbortError') {
                    return;
                }

                console.warn('تعذر تحميل الإشعارات.', error);
                this.renderError();
                this.setFootMessage('تعذر التحديث مؤقتاً');
            } finally {
                this.loading = false;
                this.setLoadingState(false);
            }
        },

        async markAllAsRead() {
            if (this.unreadCount <= 0 || this.loading) {
                return;
            }

            this.markAllButton?.setAttribute('disabled', 'disabled');
            this.setLoadingState(true);

            try {
                const response = await fetch(this.endpoints.markAll, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: this.buildPostHeaders(),
                    body: ''
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();

                this.setUnreadCount(
                    this.toNonNegativeInteger(
                        data.unreadCount ?? data.UnreadCount ?? 0
                    ),
                    { celebrate: false }
                );

                this.lastKnownUnreadCount = this.unreadCount;

                // لأن جرس الهيدر مخصص لغير المقروء فقط، بعد "قراءة الكل"
                // لا نترك بطاقات مقروءة داخله. تبقى في مركز جميع الإشعارات.
                if (this.body) {
                    this.body.innerHTML = this.emptyStateTemplate();
                    this.body.setAttribute('aria-busy', 'false');
                }

                this.setFootMessage('لا توجد إشعارات غير مقروءة');
                this.flashMarkAllSuccess();
            } catch (error) {
                console.warn('تعذر تعليم الإشعارات كمقروءة.', error);
                this.setFootMessage('تعذر تنفيذ قراءة الكل');
            } finally {
                this.markAllButton?.removeAttribute('disabled');
                this.setLoadingState(false);
            }
        },

        renderItems(items) {
            if (!this.body) {
                return;
            }

            if (!items.length) {
                this.body.innerHTML = this.emptyStateTemplate();
                this.body.setAttribute('aria-busy', 'false');
                return;
            }

            const list = document.createElement('div');
            list.className = 'customer-notification-list';

            items.forEach((rawItem, index) => {
                const item = this.normalizeItem(rawItem);
                const presentation =
                    this.getPresentation(item.notificationType);

                const anchor = document.createElement('a');
                anchor.className =
                    `customer-notification-item notification-item-enter${item.isRead ? '' : ' is-unread'}`;

                anchor.dataset.notificationId =
                    String(item.notificationId);

                anchor.dataset.tone =
                    presentation.tone;

                anchor.style.setProperty(
                    '--notification-delay',
                    `${Math.min(index * 56, 280)}ms`
                );

                anchor.href =
                    item.openUrl ||
                    `/Notifications/Open/${encodeURIComponent(item.notificationId)}`;

                anchor.setAttribute(
                    'aria-label',
                    `${item.notificationTitle}. ${item.notificationText}`
                );

                const safeTitle =
                    this.escapeHtml(item.notificationTitle || 'إشعار');

                const safeText =
                    this.escapeHtml(item.notificationText || '');

                const safeTypeLabel =
                    this.escapeHtml(presentation.label);

                const safeTime =
                    this.escapeHtml(
                        this.formatRelativeTime(item.createdAt)
                    );

                anchor.innerHTML = `
                    <span class="customer-notification-item-icon"
                          aria-hidden="true">
                        <i class="bi ${presentation.icon}"></i>
                    </span>

                    <span class="customer-notification-item-main">
                        <span class="customer-notification-item-title-row">
                            <span class="customer-notification-item-title">
                                ${safeTitle}
                            </span>

                            ${item.isRead
                        ? ''
                        : '<span class="customer-notification-unread-dot" aria-hidden="true"></span>'
                    }
                        </span>

                        <span class="customer-notification-item-text">
                            ${safeText}
                        </span>

                        <span class="customer-notification-item-meta">
                            <span>
                                <i class="bi bi-clock"></i>
                                ${safeTime}
                            </span>

                            <span aria-hidden="true">•</span>

                            <span>${safeTypeLabel}</span>
                        </span>
                    </span>

                    <span class="customer-notification-item-arrow"
                          aria-hidden="true">
                        <i class="bi bi-chevron-left"></i>
                    </span>
                `;

                list.appendChild(anchor);
            });

            this.body.replaceChildren(list);
            this.body.setAttribute('aria-busy', 'false');
        },

        renderError() {
            if (!this.body) {
                return;
            }

            this.body.innerHTML = `
                <div class="customer-notification-state">
                    <div class="customer-notification-state-card">
                        <div class="customer-notification-state-icon">
                            <i class="bi bi-cloud-slash"></i>
                        </div>

                        <h6 class="customer-notification-state-title">
                            تعذر تحميل الإشعارات
                        </h6>

                        <p class="customer-notification-state-text">
                            تحقق من الاتصال ثم أعد المحاولة.
                            لن يؤثر هذا على باقي الصفحة.
                        </p>

                        <button type="button"
                                class="customer-notification-retry"
                                data-notification-retry>
                            <i class="bi bi-arrow-clockwise"></i>
                            إعادة المحاولة
                        </button>
                    </div>
                </div>
            `;

            this.body.setAttribute('aria-busy', 'false');
        },

        showSkeleton() {
            if (!this.body) {
                return;
            }

            this.body.setAttribute('aria-busy', 'true');

            this.body.innerHTML = `
                <div class="customer-notification-skeletons"
                     aria-hidden="true">
                    ${Array.from({ length: 4 }).map(() => `
                        <div class="customer-notification-skeleton">
                            <span class="skeleton-icon"></span>
                            <span class="skeleton-lines">
                                <span></span>
                                <span></span>
                                <span></span>
                            </span>
                        </div>
                    `).join('')}
                </div>
            `;
        },

        emptyStateTemplate() {
            return `
                <div class="customer-notification-state">
                    <div class="customer-notification-state-card">
                        <div class="customer-notification-state-icon">
                            <i class="bi bi-bell-slash"></i>
                        </div>

                        <h6 class="customer-notification-state-title">
                            كل شيء هادئ
                        </h6>

                        <p class="customer-notification-state-text">
                            لا توجد إشعارات في حسابك حالياً.
                            عندما يحدث شيء مهم سيظهر هنا مباشرة.
                        </p>
                    </div>
                </div>
            `;
        },

        setUnreadCount(count, options = {}) {
            const normalized =
                this.toNonNegativeInteger(count);

            this.unreadCount = normalized;

            const displayValue =
                normalized > 99 ? '99+' : String(normalized);

            if (this.badge) {
                this.badge.textContent = displayValue;
                this.badge.hidden = normalized === 0;
            }

            if (this.liveDot) {
                this.liveDot.hidden = normalized === 0;
            }

            if (this.unreadPill) {
                this.unreadPill.hidden = normalized === 0;
            }

            if (this.unreadText) {
                this.unreadText.textContent = displayValue;
            }

            if (this.markAllButton) {
                this.markAllButton.hidden = normalized === 0;
            }

            if (this.toggle) {
                this.toggle.classList.toggle(
                    'has-unread',
                    normalized > 0
                );

                const label =
                    normalized > 0
                        ? `فتح مركز الإشعارات، لديك ${normalized} غير مقروءة`
                        : 'فتح مركز الإشعارات';

                this.toggle.setAttribute('aria-label', label);
            }

            if (options.celebrate && normalized > 0) {
                this.celebrateBell();
            }
        },

        celebrateBell() {
            if (!this.toggle) {
                return;
            }

            this.toggle.classList.remove('is-celebrating');

            // إجبار المتصفح على إعادة تشغيل الحركة.
            void this.toggle.offsetWidth;

            this.toggle.classList.add('is-celebrating');

            window.setTimeout(() => {
                this.toggle?.classList.remove('is-celebrating');
            }, 1200);
        },

        flashMarkAllSuccess() {
            if (!this.markAllButton) {
                return;
            }

            const originalHtml = this.markAllButton.innerHTML;

            this.markAllButton.innerHTML =
                '<i class="bi bi-check2-circle"></i><span>تمت القراءة</span>';

            window.setTimeout(() => {
                if (this.markAllButton) {
                    this.markAllButton.innerHTML = originalHtml;
                }
            }, 1400);
        },

        setLoadingState(isLoading) {
            this.progress?.classList.toggle(
                'is-active',
                Boolean(isLoading)
            );

            this.refreshButton?.classList.toggle(
                'is-spinning',
                Boolean(isLoading)
            );
        },

        setFootMessage(message) {
            if (this.footText) {
                this.footText.textContent = message;
            }
        },

        normalizeNotificationTitle(title, type, text = '') {
            const rawTitle = String(title ?? '').trim();

            if (rawTitle && rawTitle !== 'إشعار') {
                return rawTitle;
            }

            const key = String(type || 'General')
                .replace(/[^a-z0-9]/gi, '')
                .toLowerCase();

            const normalizedText = String(text ?? '').trim();

            // دعم عرض الإشعارات القديمة التي كانت محفوظة بعنوان/نوع عام فقط.
            if (key === 'general' && normalizedText) {
                if (normalizedText.includes('انتهت مهلة دفع')) return 'انتهت مهلة الدفع';
                if (normalizedText.includes('وافق المالك') && normalizedText.includes('العربون')) return 'تم قبول الحجز';
                if (normalizedText.includes('تمت الموافقة على الحجز')) return 'تم تأكيد الحجز';
                if (normalizedText.includes('تم اعتماد إشعار دفع')) return 'تم اعتماد الدفع';
                if (normalizedText.includes('تم رفض إشعار الدفع')) return 'تم رفض إثبات الدفع';
                if (normalizedText.includes('تم رفض طلب الحجز') || normalizedText.includes('تم رفض الحجز')) return 'تم رفض الحجز';
                if (normalizedText.includes('تم إنهاء الحجز')) return 'اكتمل الحجز';
                if (normalizedText.includes('عدم حضور')) return 'تم تسجيل عدم حضور';
                if (normalizedText.includes('ألغت المنشأة الحجز')) return 'تم إلغاء الحجز';
            }

            const titles = {
                bookingcreated: 'حجز جديد',
                bookingaccepted: 'تم قبول الحجز',
                bookingconfirmed: 'تم تأكيد الحجز',
                bookingrejected: 'تم رفض الحجز',
                bookingcancelled: 'تم إلغاء الحجز',
                bookingexpired: 'انتهت مهلة الحجز',
                bookingcompleted: 'اكتمل الحجز',
                bookingnoshow: 'تم تسجيل عدم حضور',
                paymentrequired: 'مطلوب إكمال الدفع',
                paymentreceived: 'تم استلام إثبات الدفع',
                paymentapproved: 'تم اعتماد الدفع',
                paymentrejected: 'تعذر اعتماد الدفع',
                refundrequested: 'تم إنشاء طلب استرداد',
                refundreadyfortransfer: 'الاسترداد جاهز للتحويل',
                refundtransferred: 'تم تحويل الاسترداد',
                refundcompleted: 'اكتمل الاسترداد',
                reviewcreated: 'تقييم جديد',
                reviewreply: 'رد جديد على التقييم',
                message: 'رسالة جديدة',
                chatmessage: 'رسالة جديدة',
                reportcreated: 'بلاغ جديد',
                reportreply: 'تحديث على البلاغ',
                reportescalated: 'تم تصعيد البلاغ',
                reportwithdrawn: 'تم سحب البلاغ',
                reportresolved: 'تم حل البلاغ',
                reportrejected: 'تم رفض البلاغ',
                promotion: 'عرض جديد',
                advertisement: 'تحديث الإعلان',
                account: 'تحديث الحساب',
                security: 'تنبيه أمني'
            };

            return titles[key] || 'تحديث جديد';
        },

        normalizeItem(item) {
            const notificationType =
                String(
                    item.notificationType ??
                    item.NotificationType ??
                    'General'
                );

            return {
                notificationId:
                    Number(item.notificationId ?? item.NotificationId ?? 0),

                notificationTitle:
                    this.normalizeNotificationTitle(
                        item.notificationTitle ??
                        item.NotificationTitle,
                        notificationType,
                        item.notificationText ??
                        item.NotificationText
                    ),

                notificationText:
                    String(
                        item.notificationText ??
                        item.NotificationText ??
                        ''
                    ),

                notificationType,


                isRead:
                    Boolean(
                        item.isRead ??
                        item.IsRead ??
                        (
                            String(
                                item.notificationState ??
                                item.NotificationState ??
                                ''
                            ).toLowerCase() === 'read'
                        )
                    ),

                createdAt:
                    item.createdAt ??
                    item.CreatedAt ??
                    null,

                openUrl:
                    item.openUrl ??
                    item.OpenUrl ??
                    null
            };
        },

        getPresentation(type) {
            const key =
                String(type || 'General')
                    .replace(/[^a-z0-9]/gi, '')
                    .toLowerCase();

            return this.typePresentation[key] ||
                this.typePresentation.general;
        },

        formatRelativeTime(value) {
            if (!value) {
                return 'الآن';
            }

            const date = new Date(value);

            if (Number.isNaN(date.getTime())) {
                return 'الآن';
            }

            const now = new Date();
            const seconds =
                Math.max(
                    0,
                    Math.floor((now.getTime() - date.getTime()) / 1000)
                );

            if (seconds < 45) {
                return 'الآن';
            }

            const minutes = Math.floor(seconds / 60);

            if (minutes < 60) {
                return minutes === 1
                    ? 'منذ دقيقة'
                    : `منذ ${this.toArabicNumber(minutes)} دقائق`;
            }

            const hours = Math.floor(minutes / 60);

            if (hours < 24) {
                return hours === 1
                    ? 'منذ ساعة'
                    : `منذ ${this.toArabicNumber(hours)} ساعات`;
            }

            const days = Math.floor(hours / 24);

            if (days === 1) {
                return 'أمس';
            }

            if (days < 7) {
                return `منذ ${this.toArabicNumber(days)} أيام`;
            }

            try {
                return new Intl.DateTimeFormat('ar-YE', {
                    day: 'numeric',
                    month: 'short'
                }).format(date);
            } catch {
                return date.toLocaleDateString();
            }
        },

        toArabicNumber(value) {
            try {
                return new Intl.NumberFormat('ar-YE', {
                    useGrouping: false
                }).format(value);
            } catch {
                return String(value);
            }
        },

        toNonNegativeInteger(value) {
            const number = Number(value);

            if (!Number.isFinite(number)) {
                return 0;
            }

            return Math.max(0, Math.floor(number));
        },

        buildPostHeaders() {
            const headers = {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            };

            if (this.antiForgeryToken) {
                headers.RequestVerificationToken =
                    this.antiForgeryToken;
            }

            return headers;
        },

        escapeHtml(value) {
            const element = document.createElement('div');
            element.textContent = String(value ?? '');
            return element.innerHTML;
        }
    };

    window.CustomerNotifications = customerNotifications;


    /**
     * =============================================================
     * صفحة مركز جميع الإشعارات
     * =============================================================
     */
    function initNotificationCenterPage() {
        const page =
            document.getElementById('notificationCenterPage');

        if (!page) {
            return;
        }

        const endpoint =
            page.dataset.centerDataEndpoint ||
            '/Notifications/CenterData';

        const markAllEndpoint =
            page.dataset.markAllEndpoint ||
            '/Notifications/MarkAllAsRead';

        const pageSize =
            Math.max(
                1,
                Number(page.dataset.pageSize || 12)
            );

        const tabs = Array.from(
            page.querySelectorAll('[data-notification-tab]')
        );

        const list =
            document.getElementById('notificationCenterList');

        const panel =
            page.querySelector('.notification-center-panel');

        const sectionTitle =
            document.getElementById(
                'notificationCenterSectionTitle'
            );

        const resultCount =
            document.getElementById(
                'notificationCenterResultCount'
            );

        const pagination =
            document.getElementById(
                'notificationCenterPagination'
            );

        const markAllButton =
            document.getElementById(
                'notificationCenterMarkAll'
            );

        const token =
            page.querySelector(
                'input[name="__RequestVerificationToken"]'
            )?.value || '';

        let currentFilter =
            String(
                page.dataset.currentFilter ||
                new URLSearchParams(window.location.search)
                    .get('filter') ||
                'all'
            )
                .trim()
                .toLowerCase();

        let currentPage =
            Math.max(
                1,
                Number(
                    page.dataset.currentPage ||
                    new URLSearchParams(window.location.search)
                        .get('page') ||
                    1
                )
            );

        let requestController = null;
        let requestSequence = 0;

        const titleByFilter = {
            all: 'جميع الإشعارات',
            unread: 'غير المقروءة',
            booking: 'الحجوزات',
            payment: 'الدفع والاسترداد',
            review: 'التقييمات',
            message: 'الرسائل',
            report: 'البلاغات والشكاوى'
        };

        const normalizeItem = rawItem => {
            const notificationType =
                String(
                    rawItem.notificationType ??
                    rawItem.NotificationType ??
                    'General'
                );

            return {
                notificationId:
                    Number(
                        rawItem.notificationId ??
                        rawItem.NotificationId ??
                        0
                    ),

                notificationTitle:
                    customerNotifications.normalizeNotificationTitle(
                        rawItem.notificationTitle ??
                        rawItem.NotificationTitle,
                        notificationType,
                        rawItem.notificationText ??
                        rawItem.NotificationText
                    ),

                notificationText:
                    String(
                        rawItem.notificationText ??
                        rawItem.NotificationText ??
                        ''
                    ),

                notificationType,

                isRead:
                    Boolean(
                        rawItem.isRead ??
                        rawItem.IsRead ??
                        false
                    ),

                createdAt:
                    rawItem.createdAt ??
                    rawItem.CreatedAt ??
                    null,

                openUrl:
                    String(
                        rawItem.openUrl ??
                        rawItem.OpenUrl ??
                        (
                            '/Notifications/Open/' +
                            String(
                                rawItem.notificationId ??
                                rawItem.NotificationId ??
                                ''
                            )
                        )
                    )
            };
        };

        const getNumber = (data, camel, pascal) =>
            customerNotifications.toNonNegativeInteger(
                data?.[camel] ??
                data?.[pascal] ??
                0
            );

        const setText = (id, value) => {
            const element =
                document.getElementById(id);

            if (element) {
                element.textContent =
                    String(value);
            }
        };

        const setActiveTab = filter => {
            tabs.forEach(tab => {
                const isActive =
                    String(tab.dataset.filter || '')
                        .toLowerCase() === filter;

                tab.classList.toggle(
                    'active',
                    isActive
                );

                tab.setAttribute(
                    'aria-current',
                    isActive ? 'page' : 'false'
                );
            });
        };

        const updateCounts = data => {
            const total =
                getNumber(
                    data,
                    'totalCount',
                    'TotalCount'
                );

            const unread =
                getNumber(
                    data,
                    'unreadCount',
                    'UnreadCount'
                );

            setText(
                'notificationCenterAllCount',
                total
            );

            setText(
                'notificationCenterUnreadHero',
                unread
            );

            setText(
                'notificationCenterUnreadTab',
                unread
            );

            setText(
                'notificationCenterBookingCount',
                getNumber(
                    data,
                    'bookingCount',
                    'BookingCount'
                )
            );

            setText(
                'notificationCenterPaymentCount',
                getNumber(
                    data,
                    'paymentCount',
                    'PaymentCount'
                )
            );

            setText(
                'notificationCenterReviewCount',
                getNumber(
                    data,
                    'reviewCount',
                    'ReviewCount'
                )
            );

            setText(
                'notificationCenterMessageCount',
                getNumber(
                    data,
                    'messageCount',
                    'MessageCount'
                )
            );

            setText(
                'notificationCenterReportCount',
                getNumber(
                    data,
                    'reportCount',
                    'ReportCount'
                )
            );

            if (markAllButton) {
                markAllButton.dataset.unreadCount =
                    String(unread);

                markAllButton.disabled =
                    unread <= 0;

                markAllButton.classList.toggle(
                    'is-disabled',
                    unread <= 0
                );
            }

            customerNotifications.setUnreadCount(
                unread,
                { celebrate: false }
            );

            customerNotifications.lastKnownUnreadCount =
                unread;
        };

        const renderEmpty = () => {
            const html = `
                <div class="notification-center-empty">
                    <span class="notification-center-empty__icon">
                        <i class="bi bi-bell-slash"></i>
                    </span>
                    <h3>لا توجد إشعارات هنا حاليًا</h3>
                    <p>اختر قسمًا آخر أو ارجع إلى جميع الإشعارات.</p>
                </div>
            `;

            const existingList =
                document.getElementById(
                    'notificationCenterList'
                );

            if (existingList) {
                existingList.outerHTML = `
                    <div id="notificationCenterList">
                        ${html}
                    </div>
                `;
            }
        };

        const renderItems = items => {
            let currentList =
                document.getElementById(
                    'notificationCenterList'
                );

            if (!currentList && panel) {
                currentList =
                    document.createElement('div');

                currentList.id =
                    'notificationCenterList';

                currentList.className =
                    'notification-center-list';

                currentList.setAttribute(
                    'aria-live',
                    'polite'
                );

                const paginationNode =
                    document.getElementById(
                        'notificationCenterPagination'
                    );

                if (paginationNode) {
                    panel.insertBefore(
                        currentList,
                        paginationNode
                    );
                } else {
                    panel.appendChild(
                        currentList
                    );
                }
            }

            if (!currentList) {
                return;
            }

            if (!items.length) {
                currentList.className =
                    'notification-center-list';

                currentList.innerHTML = `
                    <div class="notification-center-empty notification-center-empty--inside-list"
                         style="grid-column:1/-1">
                        <span class="notification-center-empty__icon">
                            <i class="bi bi-bell-slash"></i>
                        </span>
                        <h3>لا توجد إشعارات هنا حاليًا</h3>
                        <p>اختر قسمًا آخر أو ارجع إلى جميع الإشعارات.</p>
                    </div>
                `;
                return;
            }

            currentList.className =
                'notification-center-list';

            currentList.innerHTML =
                items
                    .map((rawItem, index) => {
                        const item =
                            normalizeItem(rawItem);

                        const presentation =
                            customerNotifications
                                .getPresentation(
                                    item.notificationType
                                );

                        const safeTitle =
                            customerNotifications
                                .escapeHtml(
                                    item.notificationTitle
                                );

                        const safeText =
                            customerNotifications
                                .escapeHtml(
                                    item.notificationText
                                );

                        const safeTime =
                            customerNotifications
                                .escapeHtml(
                                    customerNotifications
                                        .formatRelativeTime(
                                            item.createdAt
                                        )
                                );

                        const safeLabel =
                            customerNotifications
                                .escapeHtml(
                                    presentation.label
                                );

                        const safeUrl =
                            customerNotifications
                                .escapeHtml(
                                    item.openUrl
                                );

                        return `
                            <a href="${safeUrl}"
                               class="notification-center-item${item.isRead
                                ? ''
                                : ' is-unread'
                            }"
                               data-notification-center-item
                               data-notification-id="${item.notificationId}"
                               data-tone="${presentation.tone}"
                               style="--notification-center-delay:${Math.min(index * 36, 250)
                            }ms">

                                <span class="notification-center-item__icon"
                                      aria-hidden="true">
                                    <i class="bi ${presentation.icon}"></i>
                                </span>

                                <span class="notification-center-item__content">
                                    <span class="notification-center-item__top">
                                        <span class="notification-center-item__title-wrap">
                                            <strong>${safeTitle}</strong>
                                            ${item.isRead
                                ? ''
                                : '<span class="notification-center-item__unread-dot" aria-label="غير مقروء"></span>'
                            }
                                        </span>

                                        <span class="notification-center-item__type">
                                            ${safeLabel}
                                        </span>
                                    </span>

                                    <span class="notification-center-item__text">
                                        ${safeText}
                                    </span>

                                    <span class="notification-center-item__meta">
                                        <span>
                                            <i class="bi bi-clock"></i>
                                            ${safeTime}
                                        </span>
                                    </span>
                                </span>

                                <span class="notification-center-item__arrow"
                                      aria-hidden="true">
                                    <i class="bi bi-chevron-left"></i>
                                </span>
                            </a>
                        `;
                    })
                    .join('');
        };

        const renderPagination = data => {
            let nav =
                document.getElementById(
                    'notificationCenterPagination'
                );

            const totalPages =
                getNumber(
                    data,
                    'totalPages',
                    'TotalPages'
                );

            const serverPage =
                Math.max(
                    1,
                    getNumber(
                        data,
                        'currentPage',
                        'CurrentPage'
                    ) || 1
                );

            currentPage =
                serverPage;

            if (totalPages <= 1) {
                nav?.remove();
                return;
            }

            if (!nav) {
                nav =
                    document.createElement('nav');

                nav.id =
                    'notificationCenterPagination';

                nav.className =
                    'notification-center-pagination';

                nav.setAttribute(
                    'aria-label',
                    'صفحات الإشعارات'
                );

                panel?.appendChild(nav);
            }

            const pageParts = [];

            for (
                let pageNumber = 1;
                pageNumber <= totalPages;
                pageNumber++
            ) {
                if (
                    pageNumber === 1 ||
                    pageNumber === totalPages ||
                    Math.abs(
                        pageNumber - serverPage
                    ) <= 1
                ) {
                    pageParts.push(`
                        <button type="button"
                                class="notification-center-pagination__page ${pageNumber === serverPage
                            ? 'active'
                            : ''
                        }"
                                data-notification-page="${pageNumber}">
                            ${pageNumber}
                        </button>
                    `);
                } else if (
                    pageNumber ===
                    serverPage - 2 ||
                    pageNumber ===
                    serverPage + 2
                ) {
                    pageParts.push(
                        '<span>…</span>'
                    );
                }
            }

            nav.innerHTML = `
                <button type="button"
                        class="notification-center-pagination__arrow${serverPage <= 1
                    ? ' is-disabled'
                    : ''
                }"
                        data-notification-page="${Math.max(
                    1,
                    serverPage - 1
                )
                }"
                        ${serverPage <= 1
                    ? 'disabled'
                    : ''
                }>
                    <i class="bi bi-chevron-right"></i>
                </button>

                <div class="notification-center-pagination__pages">
                    ${pageParts.join('')}
                </div>

                <button type="button"
                        class="notification-center-pagination__arrow${serverPage >= totalPages
                    ? ' is-disabled'
                    : ''
                }"
                        data-notification-page="${Math.min(
                    totalPages,
                    serverPage + 1
                )
                }"
                        ${serverPage >= totalPages
                    ? 'disabled'
                    : ''
                }>
                    <i class="bi bi-chevron-left"></i>
                </button>
            `;
        };

        const updateUrl = (
            filter,
            pageNumber,
            mode = 'push'
        ) => {
            const url =
                new URL(
                    window.location.href
                );

            if (filter === 'all') {
                url.searchParams.delete(
                    'filter'
                );
            } else {
                url.searchParams.set(
                    'filter',
                    filter
                );
            }

            if (pageNumber <= 1) {
                url.searchParams.delete(
                    'page'
                );
            } else {
                url.searchParams.set(
                    'page',
                    String(pageNumber)
                );
            }

            url.searchParams.delete(
                'focus'
            );

            if (mode === 'replace') {
                window.history.replaceState(
                    {
                        notificationFilter:
                            filter,
                        notificationPage:
                            pageNumber
                    },
                    '',
                    url
                );
            } else if (mode === 'push') {
                window.history.pushState(
                    {
                        notificationFilter:
                            filter,
                        notificationPage:
                            pageNumber
                    },
                    '',
                    url
                );
            }
        };

        const loadCenter = async (
            filter,
            pageNumber = 1,
            options = {}
        ) => {
            filter =
                String(filter || 'all')
                    .trim()
                    .toLowerCase();

            pageNumber =
                Math.max(
                    1,
                    Number(pageNumber || 1)
                );

            /*
             * أهم نقطة:
             * التبويب يتغير بصرياً فور الضغطة،
             * قبل انتظار السيرفر.
             */
            setActiveTab(filter);

            currentFilter =
                filter;

            currentPage =
                pageNumber;

            if (sectionTitle) {
                sectionTitle.textContent =
                    titleByFilter[filter] ||
                    titleByFilter.all;
            }

            if (resultCount) {
                resultCount.innerHTML =
                    '<i class="bi bi-arrow-repeat"></i> جاري التحديث';
            }

            if (options.history !== false) {
                updateUrl(
                    filter,
                    pageNumber,
                    options.replaceHistory
                        ? 'replace'
                        : 'push'
                );
            }

            if (requestController) {
                requestController.abort();
            }

            requestController =
                new AbortController();

            const sequence =
                ++requestSequence;

            const url =
                new URL(
                    endpoint,
                    window.location.origin
                );

            url.searchParams.set(
                'filter',
                filter
            );

            url.searchParams.set(
                'page',
                String(pageNumber)
            );

            url.searchParams.set(
                'pageSize',
                String(pageSize)
            );

            page.setAttribute(
                'aria-busy',
                'true'
            );

            page.classList.add(
                'is-center-loading'
            );

            try {
                const response =
                    await fetch(
                        url.toString(),
                        {
                            method: 'GET',
                            credentials:
                                'same-origin',
                            headers: {
                                'Accept':
                                    'application/json',
                                'X-Requested-With':
                                    'XMLHttpRequest'
                            },
                            cache:
                                'no-store',
                            signal:
                                requestController.signal
                        }
                    );

                if (!response.ok) {
                    throw new Error(
                        `HTTP ${response.status}`
                    );
                }

                const data =
                    await response.json();

                /*
                 * إذا ضغط المستخدم بسرعة على أكثر من تبويب،
                 * نتجاهل الرد القديم ولا نعيد الشاشة للخلف.
                 */
                if (
                    sequence !==
                    requestSequence
                ) {
                    return;
                }

                const serverFilter =
                    String(
                        data.filter ??
                        data.Filter ??
                        filter
                    )
                        .trim()
                        .toLowerCase();

                currentFilter =
                    serverFilter;

                setActiveTab(
                    serverFilter
                );

                updateCounts(
                    data
                );

                const filteredCount =
                    getNumber(
                        data,
                        'filteredCount',
                        'FilteredCount'
                    );

                if (resultCount) {
                    resultCount.innerHTML = `
                        <i class="bi bi-collection"></i>
                        ${filteredCount} نتيجة
                    `;
                }

                const items =
                    Array.isArray(data.items)
                        ? data.items
                        : (
                            Array.isArray(
                                data.Items
                            )
                                ? data.Items
                                : []
                        );

                renderItems(
                    items
                );

                renderPagination(
                    data
                );

                page.dataset.currentFilter =
                    currentFilter;

                page.dataset.currentPage =
                    String(currentPage);
            } catch (error) {
                if (
                    error?.name ===
                    'AbortError'
                ) {
                    return;
                }

                console.warn(
                    'تعذر تحديث تبويب الإشعارات.',
                    error
                );

                /*
                 * Fallback آمن:
                 * لو فشل AJAX فقط، نستخدم المسار التقليدي مرة واحدة.
                 */
                const fallback =
                    new URL(
                        '/Notifications/NotificationCenter',
                        window.location.origin
                    );

                if (
                    filter !==
                    'all'
                ) {
                    fallback.searchParams.set(
                        'filter',
                        filter
                    );
                }

                if (
                    pageNumber >
                    1
                ) {
                    fallback.searchParams.set(
                        'page',
                        String(pageNumber)
                    );
                }

                fallback.searchParams.set(
                    'pageSize',
                    String(pageSize)
                );

                window.location.href =
                    fallback.pathname +
                    fallback.search;
            } finally {
                if (
                    sequence ===
                    requestSequence
                ) {
                    page.setAttribute(
                        'aria-busy',
                        'false'
                    );

                    page.classList.remove(
                        'is-center-loading'
                    );
                }
            }
        };

        /*
         * Click على التبويبات:
         * لا Reload، لا location.assign، لا Layout جديد.
         */
        tabs.forEach(tab => {
            tab.addEventListener(
                'click',
                event => {
                    if (
                        event.button !== 0 ||
                        event.ctrlKey ||
                        event.metaKey ||
                        event.shiftKey ||
                        event.altKey
                    ) {
                        return;
                    }

                    event.preventDefault();

                    const filter =
                        String(
                            tab.dataset.filter ||
                            'all'
                        );

                    if (
                        filter ===
                        currentFilter &&
                        currentPage === 1
                    ) {
                        return;
                    }

                    void loadCenter(
                        filter,
                        1
                    );
                }
            );
        });

        /*
         * Pagination بالـAJAX أيضاً.
         */
        page.addEventListener(
            'click',
            event => {
                const button =
                    event.target.closest(
                        '[data-notification-page]'
                    );

                if (!button) {
                    return;
                }

                event.preventDefault();

                if (
                    button.disabled ||
                    button.classList.contains(
                        'is-disabled'
                    )
                ) {
                    return;
                }

                const targetPage =
                    Math.max(
                        1,
                        Number(
                            button.dataset
                                .notificationPage ||
                            1
                        )
                    );

                void loadCenter(
                    currentFilter,
                    targetPage
                );
            }
        );

        /*
         * الأسهم تعمل من أي مكان داخل الصفحة،
         * ولا تحتاج Focus على التبويب.
         */
        document.addEventListener(
            'keydown',
            event => {
                if (
                    event.defaultPrevented ||
                    event.ctrlKey ||
                    event.altKey ||
                    event.metaKey
                ) {
                    return;
                }

                const target =
                    event.target;

                if (
                    target instanceof Element &&
                    target.closest(
                        'input, textarea, select, [contenteditable="true"]'
                    )
                ) {
                    return;
                }

                const order =
                    [
                        'all',
                        'unread',
                        'booking',
                        'payment',
                        'review',
                        'message',
                        'report'
                    ];

                let index =
                    order.indexOf(
                        currentFilter
                    );

                if (index < 0) {
                    index = 0;
                }

                let nextIndex =
                    null;

                switch (event.key) {
                    case 'ArrowLeft':
                    case 'ArrowDown':
                        nextIndex =
                            index + 1;
                        break;

                    case 'ArrowRight':
                    case 'ArrowUp':
                        nextIndex =
                            index - 1;
                        break;

                    case 'Home':
                        nextIndex = 0;
                        break;

                    case 'End':
                        nextIndex =
                            order.length - 1;
                        break;

                    default:
                        return;
                }

                event.preventDefault();

                nextIndex =
                    (
                        (
                            nextIndex %
                            order.length
                        ) +
                        order.length
                    ) %
                    order.length;

                void loadCenter(
                    order[nextIndex],
                    1
                );
            },
            true
        );

        /*
         * Back / Forward في المتصفح بدون Reload.
         */
        window.addEventListener(
            'popstate',
            () => {
                const params =
                    new URLSearchParams(
                        window.location.search
                    );

                void loadCenter(
                    params.get('filter') ||
                    'all',
                    Number(
                        params.get('page') ||
                        1
                    ),
                    {
                        history: false
                    }
                );
            }
        );

        /*
         * قراءة الكل.
         */
        if (
            markAllButton &&
            Number(
                markAllButton.dataset
                    .unreadCount ||
                0
            ) <= 0
        ) {
            markAllButton.disabled =
                true;
        }

        markAllButton?.addEventListener(
            'click',
            async () => {
                if (
                    markAllButton.disabled
                ) {
                    return;
                }

                markAllButton.disabled =
                    true;

                markAllButton.classList.add(
                    'is-loading'
                );

                try {
                    const headers = {
                        'Accept':
                            'application/json',
                        'X-Requested-With':
                            'XMLHttpRequest'
                    };

                    if (token) {
                        headers.RequestVerificationToken =
                            token;
                    }

                    const response =
                        await fetch(
                            markAllEndpoint,
                            {
                                method:
                                    'POST',
                                credentials:
                                    'same-origin',
                                headers,
                                body: ''
                            }
                        );

                    if (!response.ok) {
                        throw new Error(
                            `HTTP ${response.status}`
                        );
                    }

                    /*
                     * بدلاً من Reload كامل:
                     * نعيد نفس التبويب عبر AJAX.
                     */
                    await loadCenter(
                        currentFilter,
                        1,
                        {
                            replaceHistory:
                                true
                        }
                    );
                } catch (error) {
                    console.warn(
                        'تعذر تعليم إشعارات المركز كمقروءة.',
                        error
                    );

                    markAllButton.disabled =
                        false;
                } finally {
                    markAllButton.classList.remove(
                        'is-loading'
                    );
                }
            }
        );

        /*
         * إذا كان هناك focus قديم، نترك الرندر الأول من السيرفر
         * ثم نعمل Scroll فقط، بدون طلب إضافي.
         */
        const focusId =
            Number(
                page.dataset
                    .focusNotification ||
                0
            );

        if (
            Number.isFinite(
                focusId
            ) &&
            focusId > 0
        ) {
            const focusedItem =
                page.querySelector(
                    `[data-notification-id="${String(focusId)}"]`
                );

            focusedItem
                ?.scrollIntoView({
                    behavior:
                        window.matchMedia(
                            '(prefers-reduced-motion: reduce)'
                        ).matches
                            ? 'auto'
                            : 'smooth',
                    block:
                        'center'
                });
        }

        /*
         * نحفظ الحالة الحالية فقط، بدون أي Network Request إضافي.
         */
        updateUrl(
            currentFilter,
            currentPage,
            'replace'
        );
    }

    /**
     * نقطة البداية.
     */
    function initCustomerLayout() {
        initNavbar();
        initSignatureHeader();
        initSignatureDrawer();
        initMagneticButton();
        customerTheme.init();
        initProfileHint();
        initBackToTop();
        initPublicOffcanvas();
        customerNotifications.init();
        initNotificationCenterPage();

        document.documentElement.classList.add(
            'customer-ui-ready'
        );
    }

    document.addEventListener(
        'DOMContentLoaded',
        initCustomerLayout,
        { once: true }
    );

    window.addEventListener('pageshow', event => {
        if (event.persisted) {
            resetTransientHeaderState();
            customerTheme.init();
        }
    });
})();


/* =============================================================================
   CUSTOMER HEADER V5 — AURORA PRIME VISUAL SYNC
   مزامنة اللون البصري مع Hero فقط. لا يتدخل في التنقل أو الإشعارات.
   ============================================================================= */
(function () {
    'use strict';

    if (window.__customerAuroraPrimeLoaded) {
        return;
    }

    window.__customerAuroraPrimeLoaded = true;

    function parseHexColor(value) {
        const raw = String(value || '').trim();

        if (!/^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(raw)) {
            return null;
        }

        let hex = raw.slice(1);

        if (hex.length === 3) {
            hex = hex
                .split('')
                .map(char => char + char)
                .join('');
        }

        const number = Number.parseInt(hex, 16);

        if (!Number.isFinite(number)) {
            return null;
        }

        return {
            hex: `#${hex.toUpperCase()}`,
            r: (number >> 16) & 255,
            g: (number >> 8) & 255,
            b: number & 255
        };
    }

    function initAuroraPrime() {
        const navbar =
            document.querySelector(
                '#mainNavbar.customer-aurora-prime'
            );

        if (!navbar) {
            return;
        }

        const defaultAccent = {
            hex: '#00A6DA',
            r: 0,
            g: 166,
            b: 218
        };

        const applyAccent = rawColor => {
            const color =
                parseHexColor(rawColor) ||
                defaultAccent;

            navbar.style.setProperty(
                '--customer-hero-accent',
                color.hex
            );

            navbar.style.setProperty(
                '--customer-hero-accent-rgb',
                `${color.r}, ${color.g}, ${color.b}`
            );
        };

        const heroSlider =
            document.querySelector(
                '.hero-slider'
            );

        const getActiveHeroAccent = () => {
            const activeSlide =
                heroSlider?.querySelector(
                    '.slide.active'
                );

            return (
                activeSlide?.getAttribute(
                    'data-accentcolor'
                ) ||
                activeSlide?.dataset?.accentcolor ||
                '#00A6DA'
            );
        };

        applyAccent(
            getActiveHeroAccent()
        );

        if (heroSlider) {
            const observer =
                new MutationObserver(
                    mutations => {
                        const activeChanged =
                            mutations.some(
                                mutation =>
                                    mutation.type ===
                                    'attributes' &&
                                    mutation.attributeName ===
                                    'class'
                            );

                        if (activeChanged) {
                            applyAccent(
                                getActiveHeroAccent()
                            );
                        }
                    }
                );

            heroSlider
                .querySelectorAll('.slide')
                .forEach(slide => {
                    observer.observe(
                        slide,
                        {
                            attributes: true,
                            attributeFilter: ['class']
                        }
                    );
                });
        }

        /*
         * على الهاتف لا يوجد hover حقيقي، لذلك عند لمس الهيدر
         * نحرّك نقطة الضوء قليلاً فقط بدون منع النقر أو التمرير.
         */
        const isMobile =
            window.matchMedia(
                '(max-width: 1199.98px)'
            ).matches;

        const reduceMotion =
            window.matchMedia(
                '(prefers-reduced-motion: reduce)'
            ).matches;

        if (isMobile && !reduceMotion) {
            let frame = 0;

            navbar.addEventListener(
                'pointermove',
                event => {
                    if (
                        event.pointerType !== 'touch' ||
                        frame
                    ) {
                        return;
                    }

                    frame =
                        window.requestAnimationFrame(
                            () => {
                                const rect =
                                    navbar.getBoundingClientRect();

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
                                                Math.max(
                                                    rect.width,
                                                    1
                                                )
                                            ) * 100
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
                                                Math.max(
                                                    rect.height,
                                                    1
                                                )
                                            ) * 100
                                        )
                                    );

                                navbar.style.setProperty(
                                    '--signature-pointer-x',
                                    `${x.toFixed(1)}%`
                                );

                                navbar.style.setProperty(
                                    '--signature-pointer-y',
                                    `${y.toFixed(1)}%`
                                );

                                frame = 0;
                            }
                        );
                },
                { passive: true }
            );
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener(
            'DOMContentLoaded',
            initAuroraPrime,
            { once: true }
        );
    } else {
        initAuroraPrime();
    }
})();
