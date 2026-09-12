(() => {
    'use strict';

    const TAB_KEYS = [
        'overview',
        'bookings',
        'balance',
        'refunds',
        'wallets',
        'reports'
    ];

    const LEGACY_TAB_ALIASES = {
        guarantee: 'balance',
        topup: 'balance',
        transactions: 'balance'
    };

    const App = {
        root: null,
        tabsHost: null,
        content: null,
        refreshButton: null,
        placeSelector: null,
        selectedPlaceCaption: null,
        titleElement: null,
        descriptionElement: null,
        iconElement: null,
        updatedElement: null,
        toastHost: null,

        tabs: [],
        activeTab: 'overview',
        activeModule: null,
        requestController: null,
        requestNumber: 0,
        isLoading: false,
        tabQueries: new Map(),
        handlers: {},

        init() {
            this.root = document.getElementById('ownerFinancialsShell');

            if (!this.root) {
                return;
            }

            this.tabsHost = document.getElementById('ownerFinancialTabs');
            this.content = document.getElementById('ownerFinancialTabContent');
            this.refreshButton = document.getElementById('ownerFinancialRefreshButton');
            this.placeSelector = document.getElementById('ownerFinancialPlaceSelector');
            this.selectedPlaceCaption = document.getElementById('ownerFinancialSelectedPlaceName');
            this.titleElement = document.getElementById('ownerFinancialActiveTabTitle');
            this.descriptionElement = document.getElementById('ownerFinancialActiveTabDescription');
            this.iconElement = document.getElementById('ownerFinancialActiveTabIcon');
            this.updatedElement = document.getElementById('ownerFinancialLastUpdatedAt');
            this.toastHost = document.getElementById('ownerFinancialToastHost');

            this.tabs = [
                ...this.root.querySelectorAll('[data-financial-tab]')
            ];

            if (!this.tabsHost || !this.content || this.tabs.length === 0) {
                console.error('Owner Financials: عناصر الصفحة الأساسية غير مكتملة.');
                return;
            }

            window.OwnerFinancialTabs ??= {};

            this.ensureRuntimeStyles();
            this.repairPartialRootClasses(this.content);
            this.prepareTabs();
            this.captureInitialQuery();
            this.bindEvents();
            this.syncPlaceCaption();

            const currentUrl = new URL(window.location.href);

            const initialTab =
                this.normalizeTab(currentUrl.searchParams.get('financeTab')) ||
                this.normalizeTab(this.root.dataset.defaultTab) ||
                'overview';

            this.openTab(initialTab, {
                history: 'replace',
                focus: false,
                query: this.getInitialQuery()
            });
        },

        destroy() {
            this.destroyActiveModule();

            if (this.requestController) {
                this.requestController.abort();
                this.requestController = null;
            }

            if (this.handlers.click) {
                this.tabsHost?.removeEventListener('click', this.handlers.click);
            }

            if (this.handlers.rootClick) {
                this.root?.removeEventListener('click', this.handlers.rootClick);
            }

            if (this.handlers.keydown) {
                this.tabsHost?.removeEventListener('keydown', this.handlers.keydown);
            }

            if (this.handlers.refresh) {
                this.refreshButton?.removeEventListener('click', this.handlers.refresh);
            }

            if (this.handlers.popstate) {
                window.removeEventListener('popstate', this.handlers.popstate);
            }

            if (this.handlers.placeChange) {
                this.placeSelector?.removeEventListener('change', this.handlers.placeChange);
            }

            if (this.handlers.customPlaceChange) {
                [
                    'owner:active-place-changed',
                    'owner:place-changed',
                    'ownerActivePlaceChanged'
                ].forEach(eventName => {
                    window.removeEventListener(eventName, this.handlers.customPlaceChange);
                });
            }

            this.root = null;
            this.tabsHost = null;
            this.content = null;
            this.tabs = [];
        },

        prepareTabs() {
            this.tabs.forEach(button => {
                const key = this.normalizeTab(button.dataset.financialTab);

                if (!key) {
                    button.disabled = true;
                    return;
                }

                button.dataset.financialTab = key;
            });
        },

        bindEvents() {
            this.handlers.click = event => {
                const button = event.target.closest('[data-financial-tab]');

                if (!button || !this.tabsHost?.contains(button)) {
                    return;
                }

                event.preventDefault();

                const key = this.normalizeTab(button.dataset.financialTab);

                if (!key) {
                    return;
                }

                this.openTab(key, {
                    history: 'push',
                    focus: true
                });
            };

            this.handlers.keydown = event => {
                if (
                    event.key !== 'ArrowLeft' &&
                    event.key !== 'ArrowRight' &&
                    event.key !== 'Home' &&
                    event.key !== 'End'
                ) {
                    return;
                }

                const current = event.target.closest('[data-financial-tab]');

                if (!current) {
                    return;
                }

                event.preventDefault();

                const enabledTabs = this.tabs.filter(button => !button.disabled);
                const currentIndex = enabledTabs.indexOf(current);

                let nextIndex = currentIndex;

                if (event.key === 'Home') {
                    nextIndex = 0;
                } else if (event.key === 'End') {
                    nextIndex = enabledTabs.length - 1;
                } else if (event.key === 'ArrowLeft') {
                    nextIndex = (currentIndex + 1) % enabledTabs.length;
                } else if (event.key === 'ArrowRight') {
                    nextIndex = (currentIndex - 1 + enabledTabs.length) % enabledTabs.length;
                }

                const next = enabledTabs[nextIndex];

                if (!next) {
                    return;
                }

                next.focus({ preventScroll: true });
                this.scrollActiveTabIntoView(next);

                this.openTab(next.dataset.financialTab, {
                    history: 'push',
                    focus: false,
                    keepScroll: true
                });
            };

            this.handlers.refresh = event => {
                event.preventDefault();

                if (this.isLoading) {
                    return;
                }

                this.reloadCurrent({
                    history: 'replace',
                    keepScroll: true
                });
            };

            this.handlers.popstate = () => {
                const url = new URL(window.location.href);

                const key =
                    this.normalizeTab(url.searchParams.get('financeTab')) ||
                    'overview';

                const query = this.extractTabQuery(url.searchParams);

                this.tabQueries.set(key, query.toString());

                this.openTab(key, {
                    history: 'none',
                    focus: false,
                    query
                });
            };

            this.handlers.placeChange = () => {
                const option = this.placeSelector?.selectedOptions?.[0];

                this.setSelectedPlace(
                    this.placeSelector?.value || '',
                    option?.dataset.placeName ||
                    option?.textContent?.trim() ||
                    '',
                    true
                );
            };

            this.handlers.customPlaceChange = event => {
                const detail = event?.detail || {};

                const placeId =
                    detail.placeId ??
                    detail.id ??
                    detail.value ??
                    '';

                const placeName =
                    detail.placeName ??
                    detail.name ??
                    detail.text ??
                    '';

                if (!placeId) {
                    return;
                }

                this.setSelectedPlace(placeId, placeName, true);
            };

            this.handlers.rootClick = event => {
                const navButton = event.target.closest('[data-financial-nav]');

                if (!navButton || !this.root?.contains(navButton)) {
                    return;
                }

                const targetTab = this.normalizeTab(navButton.dataset.financialNav);

                if (!targetTab) {
                    return;
                }

                event.preventDefault();
                this.openTab(targetTab, {
                    history: 'push',
                    focus: true
                });
            };

            this.root.addEventListener('click', this.handlers.rootClick);

            this.tabsHost.addEventListener('click', this.handlers.click);
            this.tabsHost.addEventListener('keydown', this.handlers.keydown);
            this.refreshButton?.addEventListener('click', this.handlers.refresh);
            window.addEventListener('popstate', this.handlers.popstate);
            this.placeSelector?.addEventListener('change', this.handlers.placeChange);

            [
                'owner:active-place-changed',
                'owner:place-changed',
                'ownerActivePlaceChanged'
            ].forEach(eventName => {
                window.addEventListener(eventName, this.handlers.customPlaceChange);
            });
        },

        captureInitialQuery() {
            const url = new URL(window.location.href);

            const tab =
                this.normalizeTab(url.searchParams.get('financeTab')) ||
                this.normalizeTab(this.root.dataset.defaultTab) ||
                'overview';

            const query = this.extractTabQuery(url.searchParams);

            this.tabQueries.set(tab, query.toString());
        },

        getInitialQuery() {
            const url = new URL(window.location.href);
            return this.extractTabQuery(url.searchParams);
        },

        normalizeTab(value) {
            if (!value) {
                return '';
            }

            let key = String(value).trim().toLowerCase();

            if (LEGACY_TAB_ALIASES[key]) {
                key = LEGACY_TAB_ALIASES[key];
            }

            return TAB_KEYS.includes(key) ? key : '';
        },

        getTabButton(key) {
            return this.tabs.find(button =>
                this.normalizeTab(button.dataset.financialTab) === key
            ) || null;
        },

        getStoredQuery(key) {
            return new URLSearchParams(this.tabQueries.get(key) || '');
        },

        setStoredQuery(key, query) {
            const params = this.toSearchParams(query);
            this.tabQueries.set(key, params.toString());
            return params;
        },

        async openTab(tabKey, options = {}) {
            const key = this.normalizeTab(tabKey);

            if (!key) {
                return;
            }

            const button = this.getTabButton(key);
            const endpoint = button?.dataset.endpoint;

            if (!button || !endpoint) {
                this.showToast('تعذر فتح هذا القسم.', 'error');
                return;
            }

            const query =
                options.query !== undefined
                    ? this.setStoredQuery(key, options.query)
                    : this.getStoredQuery(key);

            const requestId = ++this.requestNumber;

            if (this.requestController) {
                this.requestController.abort();
            }

            const controller = new AbortController();
            this.requestController = controller;

            this.activeTab = key;
            this.setActiveTabUI(key, button, options.focus === true);
            this.setLoading(true);

            try {
                const url = this.buildUrl(endpoint, query);

                const response = await fetch(url, {
                    method: 'GET',
                    credentials: 'same-origin',
                    cache: 'no-store',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Accept': 'text/html'
                    },
                    signal: controller.signal
                });

                if (requestId !== this.requestNumber) {
                    return;
                }

                if (response.status === 401 || response.status === 403) {
                    window.location.reload();
                    return;
                }

                if (!response.ok) {
                    const message = await this.readErrorMessage(
                        response,
                        'تعذر تحميل هذا القسم حاليًا.'
                    );

                    throw new Error(message);
                }

                const html = await response.text();

                if (requestId !== this.requestNumber) {
                    return;
                }

                this.destroyActiveModule();

                this.content.innerHTML = html;
                this.content.setAttribute('aria-busy', 'false');
                this.repairPartialRootClasses(this.content);

                this.initActiveModule(key);
                this.applyVipEffects(this.content);
                this.updateLastUpdated();

                if (options.history !== 'none') {
                    this.writeHistory(
                        key,
                        query,
                        options.history === 'replace' ? 'replace' : 'push'
                    );
                }

                if (!options.keepScroll) {
                    this.scrollActiveTabIntoView(button);
                }
            } catch (error) {
                if (error?.name === 'AbortError') {
                    return;
                }

                console.error('Owner Financials:', error);

                this.renderLoadError(
                    error?.message ||
                    'تعذر تحميل القسم المالي.'
                );

                this.showToast(
                    error?.message ||
                    'تعذر تحميل القسم المالي.',
                    'error'
                );
            } finally {
                if (requestId === this.requestNumber) {
                    this.setLoading(false);
                }
            }
        },

        reloadCurrent(options = {}) {
            const query =
                options.query !== undefined
                    ? this.setStoredQuery(this.activeTab, options.query)
                    : this.getStoredQuery(this.activeTab);

            return this.openTab(this.activeTab, {
                history: options.history || 'replace',
                focus: false,
                query,
                keepScroll: options.keepScroll !== false
            });
        },

        setActiveTabUI(key, activeButton, shouldFocus) {
            this.tabs.forEach(button => {
                const isActive =
                    this.normalizeTab(button.dataset.financialTab) === key;

                button.classList.toggle('is-active', isActive);
                button.setAttribute('aria-selected', isActive ? 'true' : 'false');
                button.tabIndex = isActive ? 0 : -1;
            });

            const title =
                activeButton.dataset.title ||
                activeButton.textContent.trim();

            const description =
                activeButton.dataset.description ||
                '';

            const icon =
                activeButton.dataset.icon ||
                '';

            if (this.titleElement) {
                this.titleElement.textContent = title;
            }

            if (this.descriptionElement) {
                this.descriptionElement.textContent = description;
            }

            if (this.iconElement) {
                this.iconElement.className = `bi ${icon}`;
            }

            this.content?.setAttribute('aria-labelledby', activeButton.id);

            if (shouldFocus) {
                activeButton.focus({ preventScroll: true });
            }
        },

        setLoading(value) {
            this.isLoading = value;

            this.content?.setAttribute('aria-busy', value ? 'true' : 'false');
            this.refreshButton?.classList.toggle('is-loading', value);

            if (this.refreshButton) {
                this.refreshButton.disabled = value;
            }

            if (!value) {
                return;
            }

            this.content.innerHTML = `
                <div class="owner-financial-runtime-loading" role="status">
                    <span aria-hidden="true"></span>
                    <div>
                        <strong>جاري تحميل البيانات</strong>
                        <small>لحظات فقط...</small>
                    </div>
                </div>
            `;
        },

        renderLoadError(message) {
            if (!this.content) {
                return;
            }

            this.content.innerHTML = '';

            const box = document.createElement('div');
            box.className = 'owner-financial-runtime-error';

            const icon = document.createElement('i');
            icon.className = 'bi bi-exclamation-circle';

            const title = document.createElement('strong');
            title.textContent = 'تعذر تحميل القسم';

            const text = document.createElement('span');
            text.textContent = message;

            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = 'إعادة المحاولة';

            button.addEventListener(
                'click',
                () => this.reloadCurrent({ history: 'replace' }),
                { once: true }
            );

            box.append(icon, title, text, button);
            this.content.appendChild(box);
        },

        repairPartialRootClasses(scope) {
            if (!scope) {
                return;
            }

            const classMap = {
                overview: ['owner-financial-overview'],
                bookings: ['owner-booking-accounts', 'owner-booking-dossier'],
                balance: ['owner-balance-center'],
                refunds: ['refund-signature'],
                wallets: ['wallet-vault'],
                reports: ['ofir-editorial']
            };

            scope.querySelectorAll('[data-owner-financial-partial]').forEach(element => {
                const key = String(element.dataset.ownerFinancialPartial || '')
                    .trim()
                    .toLowerCase();

                const requiredClasses = classMap[key];

                if (!requiredClasses) {
                    return;
                }

                requiredClasses.forEach(className => {
                    element.classList.add(className);
                });
            });
        },

        initActiveModule(key) {
            const module = window.OwnerFinancialTabs?.[key];

            if (!module || typeof module.init !== 'function') {
                this.activeModule = null;
                return;
            }

            try {
                module.init(
                    this.content,
                    this.createModuleApi(key)
                );

                this.activeModule = module;
            } catch (error) {
                console.error(`Owner Financials ${key}:`, error);

                this.activeModule = null;

                this.showToast(
                    'تم تحميل القسم، لكن تعذر تشغيل بعض التفاعلات.',
                    'warning'
                );
            }
        },

        destroyActiveModule() {
            if (
                this.activeModule &&
                typeof this.activeModule.destroy === 'function'
            ) {
                try {
                    this.activeModule.destroy();
                } catch (error) {
                    console.warn(
                        'Owner Financials: فشل تنظيف الوحدة السابقة.',
                        error
                    );
                }
            }

            this.activeModule = null;
        },

        createModuleApi(key) {
            return {
                tabKey: key,

                reload: options => {
                    const normalized = options || {};

                    return this.reloadCurrent({
                        query:
                            normalized.query !== undefined
                                ? normalized.query
                                : this.getStoredQuery(key),
                        history: normalized.history || 'replace',
                        keepScroll: normalized.keepScroll !== false
                    });
                },

                openTab: (targetKey, options = {}) => {
                    const openOptions = {
                        history: options.history || 'push',
                        focus: options.focus !== false
                    };

                    if (options.query !== undefined) {
                        openOptions.query = options.query;
                    }

                    return this.openTab(targetKey, openOptions);
                },

                getQuery: () => this.getStoredQuery(key),

                setQuery: query => this.setStoredQuery(key, query),

                serializeForm: (form, overrides = {}) =>
                    this.serializeForm(form, overrides),

                showToast: (message, type = 'info') =>
                    this.showToast(message, type),

                getAntiForgeryToken: () =>
                    this.getAntiForgeryToken(),

                fetchJson: (url, options = {}) =>
                    this.fetchJson(url, options),

                setButtonBusy: (button, busy, busyText) =>
                    this.setButtonBusy(button, busy, busyText),

                confirm: (message, title) =>
                    this.confirmAction(message, title),

                selectedPlaceId:
                    this.root?.dataset.selectedPlaceId || '',

                selectedPlaceName:
                    this.root?.dataset.selectedPlaceName || '',

                currency:
                    this.root?.dataset.currency || 'ريال يمني'
            };
        },

        serializeForm(form, overrides = {}) {
            const params = new URLSearchParams();

            if (form) {
                const data = new FormData(form);

                for (const [name, rawValue] of data.entries()) {
                    if (!name || rawValue instanceof File) {
                        continue;
                    }

                    const value = String(rawValue).trim();

                    if (
                        value === '' ||
                        name === '__RequestVerificationToken'
                    ) {
                        continue;
                    }

                    params.set(name, value);
                }
            }

            Object.entries(overrides || {}).forEach(([name, rawValue]) => {
                if (
                    rawValue === undefined ||
                    rawValue === null ||
                    rawValue === ''
                ) {
                    params.delete(name);
                    return;
                }

                params.set(name, String(rawValue));
            });

            return params;
        },

        extractTabQuery(searchParams) {
            const params = new URLSearchParams(searchParams);

            params.delete('financeTab');
            params.delete('placeId');

            return params;
        },

        toSearchParams(value) {
            if (value instanceof URLSearchParams) {
                return new URLSearchParams(value.toString());
            }

            if (value instanceof FormData) {
                const params = new URLSearchParams();

                for (const [name, rawValue] of value.entries()) {
                    if (rawValue instanceof File || rawValue === null) {
                        continue;
                    }

                    const text = String(rawValue).trim();

                    if (text) {
                        params.set(name, text);
                    }
                }

                return params;
            }

            if (typeof value === 'string') {
                return new URLSearchParams(value.replace(/^\?/, ''));
            }

            const params = new URLSearchParams();

            if (value && typeof value === 'object') {
                Object.entries(value).forEach(([name, rawValue]) => {
                    if (
                        rawValue === undefined ||
                        rawValue === null ||
                        rawValue === ''
                    ) {
                        return;
                    }

                    params.set(name, String(rawValue));
                });
            }

            return params;
        },

        buildUrl(endpoint, query) {
            const url = new URL(endpoint, window.location.origin);
            const params = this.toSearchParams(query);

            // اختيار المنشأة في الهيدر هو مصدر الحقيقة لطلبات المالية.
            // لا نعتمد على الكوكي وحده لأنه قد يبقى على المنشأة السابقة.
            params.delete('PlaceId');
            params.delete('placeId');

            const selectedPlaceId =
                String(this.root?.dataset.selectedPlaceId || '').trim();

            if (selectedPlaceId) {
                params.set('placeId', selectedPlaceId);
            }

            params.forEach((value, name) => {
                url.searchParams.set(name, value);
            });

            return url.toString();
        },

        writeHistory(key, query, mode) {
            const manageUrl =
                this.root?.dataset.manageUrl ||
                '/Owner/Financials';

            const url = new URL(manageUrl, window.location.origin);

            url.searchParams.set('financeTab', key);

            const selectedPlaceId =
                String(this.root?.dataset.selectedPlaceId || '').trim();

            if (selectedPlaceId) {
                url.searchParams.set('placeId', selectedPlaceId);
            }

            const params = this.toSearchParams(query);

            params.forEach((value, name) => {
                if (name !== 'financeTab' && name !== 'placeId') {
                    url.searchParams.set(name, value);
                }
            });

            const state = {
                ownerFinancialTab: key,
                query: params.toString()
            };

            const relativeUrl =
                url.pathname +
                url.search;

            if (mode === 'replace') {
                history.replaceState(state, '', relativeUrl);
            } else {
                history.pushState(state, '', relativeUrl);
            }
        },

        scrollActiveTabIntoView(button) {
            if (!button) {
                return;
            }

            try {
                button.scrollIntoView({
                    behavior: 'smooth',
                    block: 'nearest',
                    inline: 'center'
                });
            } catch {
                button.scrollIntoView(false);
            }
        },

        syncPlaceCaption() {
            if (!this.root) {
                return;
            }

            const placeName =
                this.root.dataset.selectedPlaceName ||
                this.placeSelector?.selectedOptions?.[0]?.dataset.placeName ||
                this.placeSelector?.selectedOptions?.[0]?.textContent?.trim() ||
                'لا توجد منشأة';

            this.root.dataset.selectedPlaceName = placeName;

            if (this.selectedPlaceCaption) {
                this.selectedPlaceCaption.textContent = placeName;
            }
        },

        setSelectedPlace(placeId, placeName, reload = false) {
            if (!this.root) {
                return;
            }

            const normalizedId = String(placeId || '').trim();
            const normalizedName = String(placeName || '').trim();

            const changed =
                this.root.dataset.selectedPlaceId !== normalizedId;

            this.root.dataset.selectedPlaceId = normalizedId;

            if (normalizedName) {
                this.root.dataset.selectedPlaceName = normalizedName;
            }

            if (
                this.placeSelector &&
                this.placeSelector.value !== normalizedId
            ) {
                this.placeSelector.value = normalizedId;
            }

            this.syncPlaceCaption();

            if (changed && reload) {
                this.tabQueries.clear();

                this.reloadCurrent({
                    history: 'replace',
                    query: new URLSearchParams()
                });
            }
        },

        getAntiForgeryToken() {
            return (
                document.querySelector(
                    '#ownerFinancialAntiForgeryForm input[name="__RequestVerificationToken"]'
                )?.value ||
                this.content?.querySelector(
                    'input[name="__RequestVerificationToken"]'
                )?.value ||
                ''
            );
        },

        async fetchJson(url, options = {}) {
            const headers = new Headers(options.headers || {});

            headers.set('X-Requested-With', 'XMLHttpRequest');
            headers.set('Accept', 'application/json, text/plain, */*');

            const token =
                options.antiForgeryToken ||
                this.getAntiForgeryToken();

            if (token) {
                headers.set('RequestVerificationToken', token);
            }

            const response = await fetch(url, {
                ...options,
                headers,
                credentials: 'same-origin',
                cache: 'no-store'
            });

            let data = null;

            const contentType =
                response.headers.get('content-type') ||
                '';

            try {
                if (contentType.includes('application/json')) {
                    data = await response.json();
                } else {
                    const text = await response.text();

                    if (text) {
                        try {
                            data = JSON.parse(text);
                        } catch {
                            data = { message: text };
                        }
                    }
                }
            } catch {
                data = null;
            }

            if (!response.ok) {
                const message =
                    data?.message ||
                    data?.title ||
                    `فشلت العملية (${response.status}).`;

                const error = new Error(message);
                error.status = response.status;
                error.data = data;

                throw error;
            }

            return data || { success: true };
        },

        async readErrorMessage(response, fallback) {
            try {
                const contentType =
                    response.headers.get('content-type') ||
                    '';

                if (contentType.includes('application/json')) {
                    const data = await response.json();

                    return (
                        data?.message ||
                        data?.title ||
                        fallback
                    );
                }

                const text = await response.text();

                return text?.trim()
                    ? text.trim()
                    : fallback;
            } catch {
                return fallback;
            }
        },

        setButtonBusy(button, busy, busyText = 'جاري التنفيذ...') {
            if (!button) {
                return;
            }

            if (busy) {
                if (!button.dataset.ownerOriginalHtml) {
                    button.dataset.ownerOriginalHtml = button.innerHTML;
                }

                button.disabled = true;
                button.setAttribute('aria-busy', 'true');

                button.innerHTML =
                    `<span class="owner-financial-runtime-button-spinner" aria-hidden="true"></span>${this.escapeHtml(busyText)}`;
            } else {
                button.disabled = false;
                button.removeAttribute('aria-busy');

                if (button.dataset.ownerOriginalHtml) {
                    button.innerHTML = button.dataset.ownerOriginalHtml;
                    delete button.dataset.ownerOriginalHtml;
                }
            }
        },

        confirmAction(message, title = 'تأكيد العملية') {
            if (window.Swal && typeof window.Swal.fire === 'function') {
                return window.Swal.fire({
                    title,
                    text: message,
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'تأكيد',
                    cancelButtonText: 'رجوع',
                    reverseButtons: true,
                    focusCancel: true,
                    allowOutsideClick: true,
                    customClass: {
                        popup: 'owner-financial-swal-popup',
                        confirmButton: 'owner-financial-swal-confirm',
                        cancelButton: 'owner-financial-swal-cancel'
                    }
                }).then(result => result.isConfirmed === true);
            }

            return Promise.resolve(
                window.confirm(`${title}\n\n${message}`)
            );
        },

        applyVipEffects(scope) {
            if (!scope) {
                return;
            }

            const reduceMotion =
                window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches === true;

            if (!reduceMotion && typeof Element.prototype.animate === 'function') {
                const selectors = [
                    '[data-vip-tilt]',
                    'article',
                    '.owner-financial-refunds__summary-card',
                    '.owner-financial-invoices-reports__metric-card',
                    '.owner-financial-receiving-wallets__account-card'
                ].join(',');

                [...scope.querySelectorAll(selectors)]
                    .slice(0, 28)
                    .forEach((element, index) => {
                        if (element.dataset.ownerFinancialRevealed === '1') {
                            return;
                        }

                        element.dataset.ownerFinancialRevealed = '1';
                        element.animate(
                            [
                                { opacity: 0, transform: 'translateY(10px) scale(.992)' },
                                { opacity: 1, transform: 'translateY(0) scale(1)' }
                            ],
                            {
                                duration: 380,
                                delay: Math.min(index * 24, 260),
                                easing: 'cubic-bezier(.2,.8,.2,1)',
                                fill: 'both'
                            }
                        );
                    });
            }

            if (reduceMotion) {
                return;
            }

            scope.querySelectorAll('[data-vip-tilt]').forEach(card => {
                if (card.dataset.ownerFinancialTiltReady === '1') {
                    return;
                }

                card.dataset.ownerFinancialTiltReady = '1';

                const reset = () => {
                    card.style.transform = '';
                    card.style.removeProperty('--vip-x');
                    card.style.removeProperty('--vip-y');
                };

                card.addEventListener('pointermove', event => {
                    if (event.pointerType === 'touch') {
                        return;
                    }

                    const rect = card.getBoundingClientRect();
                    const px = (event.clientX - rect.left) / Math.max(1, rect.width);
                    const py = (event.clientY - rect.top) / Math.max(1, rect.height);
                    const rotateY = (px - .5) * 4.2;
                    const rotateX = (.5 - py) * 3.4;

                    card.style.setProperty('--vip-x', `${Math.round(px * 100)}%`);
                    card.style.setProperty('--vip-y', `${Math.round(py * 100)}%`);
                    card.style.transform =
                        `perspective(950px) rotateX(${rotateX.toFixed(2)}deg) rotateY(${rotateY.toFixed(2)}deg) translateY(-2px)`;
                }, { passive: true });

                card.addEventListener('pointerleave', reset, { passive: true });
                card.addEventListener('pointercancel', reset, { passive: true });
            });
        },

        updateLastUpdated() {
            if (!this.updatedElement) {
                return;
            }

            try {
                this.updatedElement.textContent =
                    new Intl.DateTimeFormat(
                        'ar-YE',
                        {
                            hour: '2-digit',
                            minute: '2-digit'
                        }
                    ).format(new Date());
            } catch {
                this.updatedElement.textContent = 'الآن';
            }
        },

        showToast(message, type = 'info') {
            if (!message) {
                return;
            }

            const host = this.toastHost || document.body;

            const normalizedType =
                ['success', 'error', 'warning', 'info'].includes(type)
                    ? type
                    : 'info';

            const toast = document.createElement('div');
            toast.className =
                `owner-financial-runtime-toast is-${normalizedType}`;

            toast.setAttribute(
                'role',
                normalizedType === 'error'
                    ? 'alert'
                    : 'status'
            );

            const icon = document.createElement('i');

            icon.className =
                normalizedType === 'success'
                    ? 'bi bi-check2-circle'
                    : normalizedType === 'error'
                        ? 'bi bi-exclamation-circle'
                        : normalizedType === 'warning'
                            ? 'bi bi-exclamation-triangle'
                            : 'bi bi-info-circle';

            const text = document.createElement('span');
            text.textContent = String(message);

            toast.append(icon, text);
            host.appendChild(toast);

            requestAnimationFrame(() => {
                toast.classList.add('is-visible');
            });

            window.setTimeout(
                () => {
                    toast.classList.remove('is-visible');

                    window.setTimeout(
                        () => toast.remove(),
                        220
                    );
                },
                normalizedType === 'error'
                    ? 5200
                    : 3600
            );
        },

        ensureRuntimeStyles() {
            if (document.getElementById('ownerFinancialRuntimeStyles')) {
                return;
            }

            const style = document.createElement('style');
            style.id = 'ownerFinancialRuntimeStyles';

            style.textContent = `
                .owner-financial-runtime-loading,
                .owner-financial-runtime-error {
                    display:flex;
                    min-height:280px;
                    align-items:center;
                    justify-content:center;
                    gap:12px;
                    padding:24px;
                    border:1px dashed rgba(0,166,218,.20);
                    border-radius:18px;
                    background:linear-gradient(135deg,#f6fcfe,#fff);
                    color:#6f8295;
                    text-align:right;
                }
                .owner-financial-runtime-loading > span,
                .owner-financial-runtime-button-spinner {
                    display:inline-block;
                    width:34px;
                    height:34px;
                    flex:0 0 34px;
                    border:3px solid #e4f8fe;
                    border-top-color:#00a6da;
                    border-radius:50%;
                    animation:ownerFinancialRuntimeSpin .75s linear infinite;
                }
                .owner-financial-runtime-loading strong {
                    display:block;
                    color:#05203c;
                    font-size:.82rem;
                }
                .owner-financial-runtime-loading small {
                    display:block;
                    margin-top:3px;
                    font-size:.6rem;
                }
                .owner-financial-runtime-error {
                    flex-direction:column;
                    text-align:center;
                }
                .owner-financial-runtime-error > i {
                    color:#d24e55;
                    font-size:1.55rem;
                }
                .owner-financial-runtime-error strong {
                    color:#05203c;
                    font-size:.82rem;
                }
                .owner-financial-runtime-error span {
                    max-width:520px;
                    font-size:.62rem;
                    line-height:1.7;
                }
                .owner-financial-runtime-error button {
                    min-height:36px;
                    padding:0 12px;
                    border:0;
                    border-radius:10px;
                    color:#fff;
                    background:#00a6da;
                    font-size:.62rem;
                    font-weight:900;
                    cursor:pointer;
                }
                .owner-financial-runtime-button-spinner {
                    width:15px;
                    height:15px;
                    flex-basis:15px;
                    border-width:2px;
                    border-color:rgba(255,255,255,.42);
                    border-top-color:#fff;
                    vertical-align:middle;
                    margin-inline-end:6px;
                }
                .owner-financial-runtime-toast {
                    display:flex;
                    align-items:center;
                    gap:9px;
                    width:100%;
                    padding:11px 13px;
                    border:1px solid rgba(5,32,60,.08);
                    border-radius:13px;
                    background:#fff;
                    box-shadow:0 15px 36px rgba(5,32,60,.14);
                    color:#18344d;
                    font-size:.66rem;
                    font-weight:800;
                    line-height:1.55;
                    opacity:0;
                    transform:translateY(8px);
                    transition:opacity .2s ease,transform .2s ease;
                    pointer-events:auto;
                }
                .owner-financial-runtime-toast.is-visible {
                    opacity:1;
                    transform:translateY(0);
                }
                .owner-financial-runtime-toast i {
                    flex:0 0 auto;
                    font-size:1rem;
                }
                .owner-financial-runtime-toast.is-success i { color:#16876b; }
                .owner-financial-runtime-toast.is-error i { color:#d24e55; }
                .owner-financial-runtime-toast.is-warning i { color:#b06d09; }
                .owner-financial-runtime-toast.is-info i { color:#00a6da; }
                .owner-financial-swal-popup {
                    border-radius:24px !important;
                    border:1px solid rgba(0,166,218,.10) !important;
                    box-shadow:0 28px 80px rgba(5,32,60,.22) !important;
                    font-family:inherit !important;
                }
                .owner-financial-swal-confirm,
                .owner-financial-swal-cancel {
                    border-radius:12px !important;
                    font-weight:900 !important;
                    box-shadow:none !important;
                }
                .owner-financial-swal-confirm {
                    background:#00A6DA !important;
                }
                @keyframes ownerFinancialRuntimeSpin {
                    to { transform:rotate(360deg); }
                }
                @media (max-width:600px) {
                    .owner-financial-runtime-loading {
                        flex-direction:column;
                        text-align:center;
                    }
                }
                @media (prefers-reduced-motion:reduce) {
                    .owner-financial-runtime-loading > span,
                    .owner-financial-runtime-button-spinner {
                        animation:none !important;
                    }
                    .owner-financial-runtime-toast {
                        transition:none !important;
                    }
                }
            `;

            document.head.appendChild(style);
        },

        escapeHtml(value) {
            return String(value ?? '')
                .replaceAll('&', '&amp;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;')
                .replaceAll('"', '&quot;')
                .replaceAll("'", '&#039;');
        }
    };

    window.OwnerFinancialApp = {
        openTab: (key, options) => App.openTab(key, options),
        reload: options => App.reloadCurrent(options),
        showToast: (message, type) => App.showToast(message, type),
        setSelectedPlace: (placeId, placeName, reload) =>
            App.setSelectedPlace(placeId, placeName, reload),

        get activeTab() {
            return App.activeTab;
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener(
            'DOMContentLoaded',
            () => App.init(),
            { once: true }
        );
    } else {
        App.init();
    }
})();
