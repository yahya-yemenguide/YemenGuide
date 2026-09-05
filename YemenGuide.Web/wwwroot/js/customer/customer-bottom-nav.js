/**
 * =============================================================
 * CUSTOMER BOTTOM NAVIGATION
 * التبويب النشط، لوحة المزيد، الشارات، وتحسين تجربة الهاتف
 * =============================================================
 */

(function () {
    'use strict';

    if (window.__customerBottomNavigationLoaded) {
        return;
    }

    window.__customerBottomNavigationLoaded = true;

    const ITEM_SELECTOR =
        '.customer-bottom-nav__item[data-bottom-nav-item]';

    const BADGE_SELECTOR =
        '[data-bottom-nav-badge]';

    /**
     * إعادة خاصية Inline Style إلى قيمتها السابقة.
     * @param {HTMLElement} element
     * @param {string} property
     * @param {{ value: string, priority: string }} snapshot
     */
    function restoreInlineProperty(element, property, snapshot) {
        if (snapshot.value) {
            element.style.setProperty(
                property,
                snapshot.value,
                snapshot.priority
            );
        } else {
            element.style.removeProperty(property);
        }
    }

    /**
     * حفظ خاصية Inline Style قبل فتح لوحة المزيد.
     * @param {HTMLElement} element
     * @param {string} property
     * @returns {{ value: string, priority: string }}
     */
    function snapshotInlineProperty(element, property) {
        return {
            value: element.style.getPropertyValue(property),
            priority: element.style.getPropertyPriority(property)
        };
    }

    /**
     * تنظيف مسار الرابط.
     * @param {string} path
     * @returns {string}
     */
    function normalizePath(path) {
        const normalized =
            (path || '/')
                .split('?')[0]
                .split('#')[0]
                .replace(/\/{2,}/g, '/')
                .replace(/\/+$/, '')
                .toLowerCase();

        return normalized || '/';
    }

    /**
     * تحديد مفتاح التبويب من المسار الحالي.
     * @param {string} path
     * @returns {'overview'|'bookings'|'messages'|'favorites'|'more'}
     */
    function resolveActiveKey(path) {
        const currentPath = normalizePath(path);

        if (
            currentPath.startsWith('/bookings')
        ) {
            return 'bookings';
        }

        if (
            currentPath.startsWith('/messages') ||
            currentPath.startsWith('/chat')
        ) {
            return 'messages';
        }

        if (
            currentPath.startsWith('/favorites')
        ) {
            return 'favorites';
        }

        if (
            currentPath.startsWith('/profile') ||
            currentPath.startsWith('/reports')
        ) {
            return 'more';
        }

        return 'overview';
    }

    /**
     * تحويل العدد إلى قيمة آمنة.
     * @param {unknown} value
     * @returns {number}
     */
    function normalizeCount(value) {
        const parsed = Number.parseInt(
            String(value ?? '0'),
            10
        );

        if (!Number.isFinite(parsed) || parsed < 0) {
            return 0;
        }

        return parsed;
    }

    /**
     * تهيئة الشريط السفلي.
     */
    function initCustomerBottomNav() {
        const bottomNav =
            document.getElementById('customerBottomNav');

        if (!bottomNav) {
            return;
        }

        const items =
            Array.from(
                bottomNav.querySelectorAll(
                    ITEM_SELECTOR
                )
            );

        const moreButton =
            document.getElementById(
                'customerBottomMoreButton'
            );

        const moreSheet =
            document.getElementById('customerMoreSheet');

        let bodyLockState = null;

        /**
         * تثبيت الصفحة في مكانها أثناء فتح لوحة المزيد.
         * يمنع Bootstrap من إضافة فراغ يمين أو يسار الشاشة.
         */
        const lockDashboardPage = () => {
            if (bodyLockState) {
                return;
            }

            const body = document.body;
            const root = document.documentElement;
            const scrollY =
                window.scrollY ||
                document.documentElement.scrollTop ||
                0;

            const trackedProperties = [
                'position',
                'top',
                'left',
                'right',
                'width',
                'max-width',
                'overflow',
                'padding-left',
                'padding-right'
            ];

            const styles = {};

            trackedProperties.forEach(property => {
                styles[property] =
                    snapshotInlineProperty(body, property);
            });

            bodyLockState = {
                scrollY,
                styles
            };

            root.classList.add('customer-sheet-open');
            body.classList.add('customer-sheet-open');

            body.style.setProperty(
                'position',
                'fixed',
                'important'
            );

            body.style.setProperty(
                'top',
                `-${scrollY}px`,
                'important'
            );

            body.style.setProperty(
                'left',
                '0',
                'important'
            );

            body.style.setProperty(
                'right',
                '0',
                'important'
            );

            body.style.setProperty(
                'width',
                '100%',
                'important'
            );

            body.style.setProperty(
                'max-width',
                '100vw',
                'important'
            );

            body.style.setProperty(
                'overflow',
                'hidden',
                'important'
            );

            body.style.setProperty(
                'padding-left',
                '0',
                'important'
            );

            body.style.setProperty(
                'padding-right',
                '0',
                'important'
            );

            window.dispatchEvent(
                new CustomEvent(
                    'customer:account-sheet-opened'
                )
            );
        };

        /**
         * إعادة الصفحة إلى مكانها الطبيعي بعد إغلاق لوحة المزيد.
         */
        const unlockDashboardPage = () => {
            if (!bodyLockState) {
                document.documentElement.classList.remove(
                    'customer-sheet-open'
                );

                document.body.classList.remove(
                    'customer-sheet-open'
                );

                return;
            }

            const body = document.body;
            const root = document.documentElement;
            const savedState = bodyLockState;

            bodyLockState = null;

            Object.entries(savedState.styles).forEach(
                ([property, snapshot]) => {
                    restoreInlineProperty(
                        body,
                        property,
                        snapshot
                    );
                }
            );

            root.classList.remove('customer-sheet-open');
            body.classList.remove('customer-sheet-open');

            window.requestAnimationFrame(() => {
                window.scrollTo(0, savedState.scrollY);

                window.dispatchEvent(
                    new CustomEvent(
                        'customer:account-sheet-closed'
                    )
                );
            });
        };

        /**
         * يعيد فرض القيم التي قد يضيف Bootstrap فوقها Padding.
         */
        const enforceLockedBodyDimensions = () => {
            if (!bodyLockState) {
                return;
            }

            document.body.style.setProperty(
                'padding-left',
                '0',
                'important'
            );

            document.body.style.setProperty(
                'padding-right',
                '0',
                'important'
            );

            document.body.style.setProperty(
                'left',
                '0',
                'important'
            );

            document.body.style.setProperty(
                'right',
                '0',
                'important'
            );

            document.body.style.setProperty(
                'width',
                '100%',
                'important'
            );
        };

        /**
         * تعيين التبويب النشط.
         * @param {string} key
         */
        const setActiveItem = key => {
            items.forEach(item => {
                const isActive =
                    item.dataset.bottomNavItem === key;

                item.classList.toggle(
                    'is-active',
                    isActive
                );

                if (isActive) {
                    item.setAttribute(
                        'aria-current',
                        'page'
                    );
                } else {
                    item.removeAttribute(
                        'aria-current'
                    );
                }
            });
        };

        /**
         * الوصول إلى شارة معينة.
         * @param {string} type
         * @returns {HTMLElement|null}
         */
        const getBadge = type =>
            bottomNav.querySelector(
                `${BADGE_SELECTOR}[data-bottom-nav-badge="${type}"]`
            );

        /**
         * تحديث شارة الرسائل أو الحجوزات.
         * @param {'messages'|'bookings'} type
         * @param {number|string} count
         */
        const setBadge = (type, count) => {
            const badge = getBadge(type);

            if (!badge) {
                return;
            }

            const safeCount =
                normalizeCount(count);

            if (safeCount <= 0) {
                badge.hidden = true;
                badge.textContent = '0';
                badge.removeAttribute('aria-label');
                return;
            }

            badge.hidden = false;
            badge.textContent =
                safeCount > 99
                    ? '99+'
                    : String(safeCount);

            badge.setAttribute(
                'aria-label',
                type === 'messages'
                    ? `${safeCount} رسالة غير مقروءة`
                    : `${safeCount} حجز يحتاج المتابعة`
            );
        };

        /**
         * قراءة القيم الأولية إن تم تمريرها لاحقاً من الـLayout أو Controller.
         */
        const hydrateInitialBadges = () => {
            const body = document.body;

            const messagesCount =
                body.dataset.unreadMessages ??
                bottomNav.dataset.unreadMessages ??
                getBadge('messages')?.dataset.count ??
                0;

            const bookingsCount =
                body.dataset.pendingBookings ??
                bottomNav.dataset.pendingBookings ??
                getBadge('bookings')?.dataset.count ??
                0;

            setBadge('messages', messagesCount);
            setBadge('bookings', bookingsCount);
        };

        /**
         * مزامنة التبويب النشط مع المسار الحالي.
         */
        const syncActiveItemWithRoute = () => {
            setActiveItem(
                resolveActiveKey(
                    window.location.pathname
                )
            );
        };

        /**
         * تأثير ضغط بسيط بدون تعطيل الانتقال.
         */
        items.forEach(item => {
            item.addEventListener(
                'pointerdown',
                () => {
                    item.classList.add(
                        'is-pressed'
                    );
                },
                { passive: true }
            );

            const clearPressedState = () => {
                item.classList.remove(
                    'is-pressed'
                );
            };

            item.addEventListener(
                'pointerup',
                clearPressedState,
                { passive: true }
            );

            item.addEventListener(
                'pointercancel',
                clearPressedState,
                { passive: true }
            );

            item.addEventListener(
                'pointerleave',
                clearPressedState,
                { passive: true }
            );
        });

        /**
         * تمييز زر المزيد أثناء فتح اللوحة.
         */
        if (moreButton && moreSheet) {
            moreSheet.addEventListener(
                'show.bs.offcanvas',
                () => {
                    lockDashboardPage();

                    moreButton.classList.add(
                        'is-sheet-open'
                    );

                    moreButton.setAttribute(
                        'aria-expanded',
                        'true'
                    );

                    window.requestAnimationFrame(
                        enforceLockedBodyDimensions
                    );
                }
            );

            moreSheet.addEventListener(
                'shown.bs.offcanvas',
                () => {
                    enforceLockedBodyDimensions();
                }
            );

            moreSheet.addEventListener(
                'hidden.bs.offcanvas',
                () => {
                    moreButton.classList.remove(
                        'is-sheet-open'
                    );

                    moreButton.setAttribute(
                        'aria-expanded',
                        'false'
                    );

                    unlockDashboardPage();
                    syncActiveItemWithRoute();
                }
            );

            moreSheet
                .querySelectorAll('a[href]')
                .forEach(link => {
                    link.addEventListener('click', () => {
                        if (
                            typeof window.bootstrap === 'undefined' ||
                            typeof window.bootstrap.Offcanvas ===
                            'undefined'
                        ) {
                            return;
                        }

                        const instance =
                            window.bootstrap.Offcanvas.getInstance(
                                moreSheet
                            );

                        if (instance) {
                            instance.hide();
                        }
                    });
                });
        }

        /**
         * استقبال تحديثات الشارات من الشات أو الحجوزات مستقبلاً.
         *
         * مثال:
         * window.dispatchEvent(new CustomEvent(
         *   'customer:bottom-nav-badge',
         *   { detail: { type: 'messages', count: 4 } }
         * ));
         */
        window.addEventListener(
            'customer:bottom-nav-badge',
            event => {
                const detail = event.detail || {};
                const type = detail.type;

                if (
                    type !== 'messages' &&
                    type !== 'bookings'
                ) {
                    return;
                }

                setBadge(type, detail.count);
            }
        );

        window.addEventListener(
            'pagehide',
            unlockDashboardPage
        );

        window.addEventListener(
            'pageshow',
            () => {
                unlockDashboardPage();
                syncActiveItemWithRoute();
                hydrateInitialBadges();
            }
        );

        /**
         * واجهة عامة بسيطة يمكن استعمالها من ملفات الشات والحجوزات.
         */
        window.CustomerBottomNav = {
            setActive: setActiveItem,
            setBadge,
            refresh() {
                syncActiveItemWithRoute();
                hydrateInitialBadges();
            }
        };

        syncActiveItemWithRoute();
        hydrateInitialBadges();
    }

    document.addEventListener(
        'DOMContentLoaded',
        initCustomerBottomNav,
        { once: true }
    );
})();

/**
 * =============================================================
 * CUSTOMER MESSAGE INDICATOR
 * العداد الحقيقي للمحادثات غير المقروءة في جميع صفحات العميل
 * =============================================================
 */
(function () {
    'use strict';

    if (window.__customerMessageIndicatorLoaded) {
        return;
    }

    window.__customerMessageIndicatorLoaded = true;

    const INDICATOR_SELECTOR =
        '[data-customer-message-indicator="true"]';

    const MESSAGE_BADGE_SELECTOR =
        '[data-customer-message-badge="true"]';

    const STORAGE_KEY =
        'yemenguide_customer_unread_conversations';

    const STORAGE_EVENT_KEY =
        'yemenguide_customer_unread_conversations_event';

    const SIGNALR_FALLBACK_URL =
        '/lib/signalr/signalr.min.js';

    const HUB_FALLBACK_URL =
        '/chatHub';

    const POLL_INTERVAL_MS =
        60000;

    const state = {
        count: 0,
        initialized: false,
        refreshTimer: null,
        pollTimer: null,
        connection: null,
        connectionStarted: false,
        reconnectTimer: null,
        broadcastChannel: null,
        lastRefreshAt: 0,
        requestController: null
    };

    function normalizeCount(value) {
        const parsed =
            Number.parseInt(
                String(value ?? '0'),
                10
            );

        if (
            !Number.isFinite(parsed) ||
            parsed < 0
        ) {
            return 0;
        }

        return parsed;
    }

    function getIndicatorElements() {
        return Array.from(
            document.querySelectorAll(
                INDICATOR_SELECTOR
            )
        );
    }

    function getMessageBadges() {
        return Array.from(
            document.querySelectorAll(
                MESSAGE_BADGE_SELECTOR
            )
        );
    }

    function readDataValue(name) {
        for (
            const element of
            getIndicatorElements()
        ) {
            const value =
                element.dataset?.[name];

            if (
                typeof value === 'string' &&
                value.trim()
            ) {
                return value.trim();
            }
        }

        return '';
    }

    function getUnreadUrl() {
        return (
            readDataValue(
                'unreadMessagesUrl'
            ) ||
            '/Messages/GetUnreadConversationCount'
        );
    }

    function getHubUrl() {
        return (
            readDataValue(
                'chatHubUrl'
            ) ||
            HUB_FALLBACK_URL
        );
    }

    function getSignalRScriptUrl() {
        return (
            readDataValue(
                'signalrScriptUrl'
            ) ||
            SIGNALR_FALLBACK_URL
        );
    }

    function formatBadgeCount(count) {
        return count > 99
            ? '99+'
            : String(count);
    }

    function animateBadge(badge) {
        if (
            !badge ||
            window.matchMedia(
                '(prefers-reduced-motion: reduce)'
            ).matches
        ) {
            return;
        }

        badge.classList.remove(
            'is-count-updated'
        );

        void badge.offsetWidth;

        badge.classList.add(
            'is-count-updated'
        );

        window.setTimeout(
            () => {
                badge.classList.remove(
                    'is-count-updated'
                );
            },
            560
        );
    }

    function updateMessageLinks(count) {
        document
            .querySelectorAll(
                '#customerHeaderMessagesLink, ' +
                '.customer-bottom-nav__item[data-bottom-nav-item="messages"]'
            )
            .forEach(link => {
                const label =
                    count > 0
                        ? `فتح الدردشة، ${count} محادثة غير مقروءة`
                        : 'فتح الدردشة، لا توجد محادثات غير مقروءة';

                link.setAttribute(
                    'aria-label',
                    label
                );

                link.setAttribute(
                    'title',
                    label
                );

                link.querySelector(
                    '.customer-bottom-nav__icon'
                )?.classList.toggle(
                    'has-unread-messages',
                    count > 0
                );
            });
    }

    function persistCount(count) {
        try {
            localStorage.setItem(
                STORAGE_KEY,
                String(count)
            );

            localStorage.setItem(
                STORAGE_EVENT_KEY,
                JSON.stringify({
                    count,
                    at: Date.now()
                })
            );
        } catch {
            // المتصفح قد يمنع التخزين في بعض الأوضاع الخاصة.
        }
    }

    function broadcastCount(count) {
        try {
            state.broadcastChannel?.postMessage({
                type: 'unread-conversations',
                count
            });
        } catch {
            // لا نوقف العداد عند فشل المزامنة بين النوافذ.
        }
    }

    function setMessageCount(
        value,
        options = {}
    ) {
        const nextCount =
            normalizeCount(value);

        const previousCount =
            state.count;

        state.count =
            nextCount;

        getMessageBadges().forEach(
            badge => {
                badge.dataset.count =
                    String(nextCount);

                badge.textContent =
                    formatBadgeCount(
                        nextCount
                    );

                badge.hidden =
                    nextCount <= 0;

                if (nextCount > 0) {
                    badge.setAttribute(
                        'aria-label',
                        `${nextCount} محادثة غير مقروءة`
                    );
                } else {
                    badge.removeAttribute(
                        'aria-label'
                    );
                }

                if (
                    options.animate === true &&
                    nextCount > previousCount &&
                    nextCount > 0
                ) {
                    animateBadge(badge);
                }
            }
        );

        updateMessageLinks(
            nextCount
        );

        if (
            options.persist !== false
        ) {
            persistCount(
                nextCount
            );
        }

        if (
            options.broadcast !== false
        ) {
            broadcastCount(
                nextCount
            );
        }

        window.dispatchEvent(
            new CustomEvent(
                'customer:unread-conversations-changed',
                {
                    detail: {
                        count: nextCount
                    }
                }
            )
        );
    }

    function readPersistedCount() {
        try {
            return normalizeCount(
                localStorage.getItem(
                    STORAGE_KEY
                )
            );
        } catch {
            return 0;
        }
    }

    async function parseResponse(response) {
        const contentType =
            response.headers.get(
                'content-type'
            ) || '';

        if (
            response.redirected &&
            /login|account/i.test(
                response.url
            )
        ) {
            return {
                success: false,
                requiresLogin: true,
                message:
                    'انتهت جلسة العميل.'
            };
        }

        if (
            contentType.includes(
                'application/json'
            )
        ) {
            return response.json();
        }

        return {
            success: false,
            message:
                'استجابة غير متوقعة من الخادم.'
        };
    }

    async function refreshUnreadCount(
        options = {}
    ) {
        const now =
            Date.now();

        if (
            options.force !== true &&
            now - state.lastRefreshAt < 1200
        ) {
            return state.count;
        }

        state.requestController?.abort();

        const requestController =
            new AbortController();

        state.requestController =
            requestController;

        try {
            const response =
                await fetch(
                    getUnreadUrl(),
                    {
                        method: 'GET',
                        credentials: 'same-origin',
                        cache: 'no-store',
                        headers: {
                            Accept:
                                'application/json',
                            'X-Requested-With':
                                'XMLHttpRequest'
                        },
                        signal:
                            requestController.signal
                    }
                );

            const result =
                await parseResponse(
                    response
                );

            if (
                !response.ok ||
                result?.success !== true
            ) {
                return state.count;
            }

            state.lastRefreshAt =
                Date.now();

            setMessageCount(
                result.count,
                {
                    animate:
                        options.animate === true,
                    persist: true,
                    broadcast: true
                }
            );

            return state.count;
        } catch (error) {
            if (
                error?.name !==
                'AbortError'
            ) {
                console.warn(
                    'Unread conversations refresh failed:',
                    error
                );
            }

            return state.count;
        } finally {
            if (
                state.requestController ===
                requestController
            ) {
                state.requestController =
                    null;
            }
        }
    }

    function scheduleRefresh(
        delay = 180,
        animate = false
    ) {
        window.clearTimeout(
            state.refreshTimer
        );

        state.refreshTimer =
            window.setTimeout(
                () => {
                    void refreshUnreadCount({
                        force: true,
                        animate
                    });
                },
                Math.max(
                    0,
                    delay
                )
            );
    }

    function waitForExistingScript(
        script
    ) {
        return new Promise(
            resolve => {
                if (
                    window.signalR
                ) {
                    resolve(true);
                    return;
                }

                const complete = success => {
                    script.removeEventListener(
                        'load',
                        onLoad
                    );

                    script.removeEventListener(
                        'error',
                        onError
                    );

                    resolve(success);
                };

                const onLoad = () =>
                    complete(
                        Boolean(
                            window.signalR
                        )
                    );

                const onError = () =>
                    complete(false);

                script.addEventListener(
                    'load',
                    onLoad,
                    { once: true }
                );

                script.addEventListener(
                    'error',
                    onError,
                    { once: true }
                );

                window.setTimeout(
                    () => {
                        complete(
                            Boolean(
                                window.signalR
                            )
                        );
                    },
                    8000
                );
            }
        );
    }

    async function ensureSignalR() {
        if (
            window.signalR
        ) {
            return true;
        }

        const existingScript =
            document.querySelector(
                'script[src*="/lib/signalr/signalr.min.js"], ' +
                'script[data-customer-signalr-loader]'
            );

        if (existingScript) {
            return waitForExistingScript(
                existingScript
            );
        }

        const script =
            document.createElement(
                'script'
            );

        script.src =
            getSignalRScriptUrl();

        script.async =
            true;

        script.dataset
            .customerSignalrLoader =
            'true';

        const loaded =
            waitForExistingScript(
                script
            );

        document.head.appendChild(
            script
        );

        return loaded;
    }

    async function startSignalR() {
        if (
            state.connection ||
            document.visibilityState ===
            'hidden'
        ) {
            return;
        }

        const available =
            await ensureSignalR();

        if (
            !available ||
            !window.signalR
        ) {
            return;
        }

        const connection =
            new window.signalR
                .HubConnectionBuilder()
                .withUrl(
                    getHubUrl()
                )
                .withAutomaticReconnect([
                    0,
                    2000,
                    5000,
                    10000,
                    20000
                ])
                .configureLogging(
                    window.signalR
                        .LogLevel
                        .Warning
                )
                .build();

        state.connection =
            connection;

        connection.on(
            'ReceiveMessage',
            () => {
                scheduleRefresh(
                    160,
                    true
                );
            }
        );

        connection.on(
            'MessageDeleted',
            () => {
                scheduleRefresh(
                    180,
                    false
                );
            }
        );

        connection.on(
            'UnreadConversationChanged',
            data => {
                if (
                    data &&
                    Number.isFinite(
                        Number(
                            data.count
                        )
                    )
                ) {
                    setMessageCount(
                        data.count,
                        {
                            animate: false,
                            persist: true,
                            broadcast: true
                        }
                    );

                    return;
                }

                scheduleRefresh(
                    120,
                    false
                );
            }
        );

        connection.onreconnecting(
            () => {
                state.connectionStarted =
                    false;
            }
        );

        connection.onreconnected(
            () => {
                state.connectionStarted =
                    true;

                scheduleRefresh(
                    120,
                    false
                );
            }
        );

        connection.onclose(
            () => {
                state.connectionStarted =
                    false;

                state.connection =
                    null;

                window.clearTimeout(
                    state.reconnectTimer
                );

                state.reconnectTimer =
                    window.setTimeout(
                        () => {
                            void startSignalR();
                        },
                        5000
                    );
            }
        );

        try {
            await connection.start();

            state.connectionStarted =
                true;

            scheduleRefresh(
                80,
                false
            );
        } catch (error) {
            console.warn(
                'Customer message indicator SignalR failed:',
                error
            );

            state.connection =
                null;

            window.clearTimeout(
                state.reconnectTimer
            );

            state.reconnectTimer =
                window.setTimeout(
                    () => {
                        void startSignalR();
                    },
                    5000
                );
        }
    }

    function initializeCrossTabSync() {
        if (
            typeof window.BroadcastChannel ===
            'function'
        ) {
            state.broadcastChannel =
                new BroadcastChannel(
                    'yemenguide-customer-messages'
                );

            state.broadcastChannel
                .addEventListener(
                    'message',
                    event => {
                        if (
                            event.data?.type !==
                            'unread-conversations'
                        ) {
                            return;
                        }

                        setMessageCount(
                            event.data.count,
                            {
                                animate: false,
                                persist: true,
                                broadcast: false
                            }
                        );
                    }
                );
        }

        window.addEventListener(
            'storage',
            event => {
                if (
                    event.key !==
                    STORAGE_KEY &&
                    event.key !==
                    STORAGE_EVENT_KEY
                ) {
                    return;
                }

                const nextCount =
                    event.key ===
                        STORAGE_KEY
                        ? event.newValue
                        : (() => {
                            try {
                                return JSON.parse(
                                    event.newValue ||
                                    '{}'
                                ).count;
                            } catch {
                                return 0;
                            }
                        })();

                setMessageCount(
                    nextCount,
                    {
                        animate: false,
                        persist: false,
                        broadcast: false
                    }
                );
            }
        );
    }

    function bridgeCustomerBottomNav() {
        const navigation =
            window.CustomerBottomNav;

        if (
            !navigation ||
            navigation
                .__messageIndicatorWrapped
        ) {
            return;
        }

        const originalSetBadge =
            typeof navigation.setBadge ===
                'function'
                ? navigation.setBadge
                    .bind(navigation)
                : null;

        navigation.setBadge = (
            type,
            count
        ) => {
            originalSetBadge?.(
                type,
                count
            );

            if (
                type ===
                'messages'
            ) {
                setMessageCount(
                    count,
                    {
                        animate: false,
                        persist: true,
                        broadcast: true
                    }
                );
            }
        };

        const originalRefresh =
            typeof navigation.refresh ===
                'function'
                ? navigation.refresh
                    .bind(navigation)
                : null;

        navigation.refresh = () => {
            originalRefresh?.();

            void refreshUnreadCount({
                force: true,
                animate: false
            });
        };

        navigation
            .__messageIndicatorWrapped =
            true;
    }

    function startPolling() {
        window.clearInterval(
            state.pollTimer
        );

        state.pollTimer =
            window.setInterval(
                () => {
                    if (
                        document.visibilityState ===
                        'visible'
                    ) {
                        void refreshUnreadCount({
                            force: true,
                            animate: false
                        });
                    }
                },
                POLL_INTERVAL_MS
            );
    }

    function initialize() {
        if (
            state.initialized ||
            getIndicatorElements()
                .length === 0
        ) {
            return;
        }

        state.initialized =
            true;

        setMessageCount(
            readPersistedCount(),
            {
                animate: false,
                persist: false,
                broadcast: false
            }
        );

        initializeCrossTabSync();
        bridgeCustomerBottomNav();
        startPolling();

        void refreshUnreadCount({
            force: true,
            animate: false
        });

        void startSignalR();

        window.addEventListener(
            'customer:bottom-nav-badge',
            event => {
                if (
                    event.detail?.type !==
                    'messages'
                ) {
                    return;
                }

                setMessageCount(
                    event.detail.count,
                    {
                        animate: false,
                        persist: true,
                        broadcast: true
                    }
                );
            }
        );

        window.addEventListener(
            'customer:message-count-refresh',
            () => {
                scheduleRefresh(
                    0,
                    false
                );
            }
        );

        window.addEventListener(
            'focus',
            () => {
                scheduleRefresh(
                    0,
                    false
                );
            }
        );

        window.addEventListener(
            'pageshow',
            () => {
                bridgeCustomerBottomNav();

                scheduleRefresh(
                    0,
                    false
                );

                void startSignalR();
            }
        );

        document.addEventListener(
            'visibilitychange',
            () => {
                if (
                    document.visibilityState ===
                    'visible'
                ) {
                    bridgeCustomerBottomNav();

                    scheduleRefresh(
                        0,
                        false
                    );

                    void startSignalR();
                }
            }
        );

        window.CustomerMessageIndicator = {
            getCount() {
                return state.count;
            },

            setCount(count) {
                setMessageCount(
                    count,
                    {
                        animate: false,
                        persist: true,
                        broadcast: true
                    }
                );
            },

            refresh() {
                return refreshUnreadCount({
                    force: true,
                    animate: false
                });
            }
        };
    }

    if (
        document.readyState ===
        'loading'
    ) {
        document.addEventListener(
            'DOMContentLoaded',
            initialize,
            { once: true }
        );
    } else {
        initialize();
    }
})();
