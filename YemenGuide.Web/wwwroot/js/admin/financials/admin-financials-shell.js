(function (window, document) {
    "use strict";

    const Finance = window.AdminFinance = window.AdminFinance || {};

    const state = {
        root: null,
        host: null,
        activeTab: null,
        activeUrl: null,
        tabAbortController: null,
        shellAbortController: null,
        managementAbortController: null,
        initialized: false
    };

    function qs(selector, scope) {
        return (scope || document).querySelector(selector);
    }

    function qsa(selector, scope) {
        return Array.from((scope || document).querySelectorAll(selector));
    }

    function escapeHtml(value) {
        const text = value == null ? "" : String(value);
        return text
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function pick(obj, ...keys) {
        if (!obj || typeof obj !== "object") {
            return undefined;
        }

        for (const key of keys) {
            if (Object.prototype.hasOwnProperty.call(obj, key)) {
                return obj[key];
            }
        }

        const map = Object.keys(obj).reduce((acc, key) => {
            acc[key.toLowerCase()] = key;
            return acc;
        }, {});

        for (const key of keys) {
            const actual = map[String(key).toLowerCase()];
            if (actual !== undefined) {
                return obj[actual];
            }
        }

        return undefined;
    }

    function number(value, fallback) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : (fallback == null ? 0 : fallback);
    }

    function bool(value) {
        if (typeof value === "boolean") {
            return value;
        }

        if (typeof value === "number") {
            return value !== 0;
        }

        if (typeof value === "string") {
            return ["true", "1", "yes", "on"].includes(value.trim().toLowerCase());
        }

        return false;
    }

    function asArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function formatNumber(value, maxFractionDigits) {
        const parsed = number(value, 0);

        try {
            return new Intl.NumberFormat("ar-YE", {
                maximumFractionDigits: maxFractionDigits == null ? 2 : maxFractionDigits
            }).format(parsed);
        } catch (_) {
            return parsed.toLocaleString();
        }
    }

    function formatMoney(value, currency) {
        const suffix = (currency || getCurrency() || "ريال يمني").trim();
        return `${formatNumber(value, 2)} ${suffix}`;
    }

    function formatDate(value, includeTime) {
        if (!value) {
            return "—";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return String(value);
        }

        try {
            return new Intl.DateTimeFormat("ar-YE", includeTime ? {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit"
            } : {
                year: "numeric",
                month: "2-digit",
                day: "2-digit"
            }).format(date);
        } catch (_) {
            return includeTime ? date.toLocaleString() : date.toLocaleDateString();
        }
    }

    function getCurrency() {
        const root = state.root || qs("#adminFinancialsRoot");
        const currencyStrong = root ? qs(".af-meta-pill strong", root) : null;
        return currencyStrong ? currencyStrong.textContent.trim() : "ريال يمني";
    }

    function buildUrl(baseUrl, params) {
        const url = new URL(baseUrl, window.location.origin);

        if (params instanceof URLSearchParams) {
            params.forEach((value, key) => {
                if (value !== "" && value != null) {
                    url.searchParams.set(key, value);
                } else {
                    url.searchParams.delete(key);
                }
            });
        } else if (params && typeof params === "object") {
            Object.entries(params).forEach(([key, value]) => {
                if (value !== "" && value != null) {
                    url.searchParams.set(key, String(value));
                } else {
                    url.searchParams.delete(key);
                }
            });
        }

        return url.pathname + url.search;
    }

    async function readError(response) {
        const contentType = response.headers.get("content-type") || "";

        if (contentType.includes("application/json")) {
            try {
                const payload = await response.json();
                return pick(payload, "message", "Message") || `حدث خطأ (${response.status}).`;
            } catch (_) {
                return `حدث خطأ (${response.status}).`;
            }
        }

        try {
            const text = (await response.text()).trim();
            return text || `حدث خطأ (${response.status}).`;
        } catch (_) {
            return `حدث خطأ (${response.status}).`;
        }
    }

    async function fetchHtml(url, options) {
        const response = await fetch(url, {
            method: "GET",
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "text/html"
            },
            ...(options || {})
        });

        if (!response.ok) {
            const error = new Error(await readError(response));
            error.status = response.status;
            throw error;
        }

        return response.text();
    }

    async function fetchJson(url, options) {
        const response = await fetch(url, {
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json",
                ...((options && options.headers) || {})
            },
            ...(options || {})
        });

        if (!response.ok) {
            const error = new Error(await readError(response));
            error.status = response.status;
            throw error;
        }

        const text = await response.text();
        if (!text) {
            return {};
        }

        try {
            return JSON.parse(text);
        } catch (_) {
            throw new Error("استجابة الخادم غير صالحة.");
        }
    }

    async function postForm(form, submitter) {
        if (!form) {
            throw new Error("النموذج غير موجود.");
        }

        const formData = new FormData(form);

        if (submitter && submitter.name) {
            formData.append(submitter.name, submitter.value || "");
        }

        const response = await fetch(form.action, {
            method: (form.method || "POST").toUpperCase(),
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json"
            },
            body: formData
        });

        if (!response.ok) {
            const error = new Error(await readError(response));
            error.status = response.status;
            throw error;
        }

        const text = await response.text();
        return text ? JSON.parse(text) : {};
    }

    function setButtonBusy(button, isBusy, busyText) {
        if (!button) {
            return;
        }

        if (isBusy) {
            if (!button.dataset.originalHtml) {
                button.dataset.originalHtml = button.innerHTML;
            }

            button.disabled = true;
            button.classList.add("is-loading");
            button.innerHTML =
                `<span class="af-global-loader__spinner" style="width:18px;height:18px;border-width:2px;"></span>` +
                `<span>${escapeHtml(busyText || "جارٍ التنفيذ...")}</span>`;
        } else {
            button.disabled = false;
            button.classList.remove("is-loading");

            if (button.dataset.originalHtml) {
                button.innerHTML = button.dataset.originalHtml;
                delete button.dataset.originalHtml;
            }
        }
    }

    function showGlobalLoader(show) {
        const loader = state.root ? qs("[data-finance-global-loader]", state.root) : null;
        if (!loader) {
            return;
        }

        loader.hidden = !show;
    }

    function showToast(message, type, title, duration) {
        const stack = state.root ? qs("#adminFinanceToastStack", state.root) : null;
        if (!stack) {
            return;
        }

        const tone = ["success", "danger", "warning", "info"].includes(type)
            ? type
            : "info";

        const icon = {
            success: "bi-check2-circle",
            danger: "bi-x-octagon",
            warning: "bi-exclamation-triangle",
            info: "bi-info-circle"
        }[tone];

        const toast = document.createElement("div");
        toast.className = `af-toast af-toast--${tone}`;
        toast.innerHTML = `
            <span class="af-toast__icon"><i class="bi ${icon}"></i></span>
            <div class="af-toast__body">
                <strong>${escapeHtml(title || (
                    tone === "success" ? "تم بنجاح" :
                    tone === "danger" ? "تعذر التنفيذ" :
                    tone === "warning" ? "تنبيه" : "معلومة"
                ))}</strong>
                <p>${escapeHtml(message || "")}</p>
            </div>
            <button type="button" class="af-toast__close" aria-label="إغلاق">
                <i class="bi bi-x-lg"></i>
            </button>
        `;

        const remove = () => {
            if (toast.parentNode) {
                toast.remove();
            }
        };

        qs(".af-toast__close", toast).addEventListener("click", remove);
        stack.appendChild(toast);
        window.setTimeout(remove, duration || 5000);
    }

    function updateActiveTab(tab) {
        state.activeTab = tab;
        if (state.root) {
            state.root.dataset.selectedTab = tab;
        }

        qsa("[data-finance-tab]", state.root).forEach(button => {
            const active = button.dataset.financeTab === tab &&
                button.classList.contains("af-tab");

            if (button.classList.contains("af-tab")) {
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-selected", active ? "true" : "false");
            }
        });
    }

    function updateBrowserUrl(tab, mode) {
        if (!tab || mode === "none") {
            return;
        }

        const url = new URL(window.location.href);
        url.searchParams.set("financeTab", tab);

        if (mode === "push") {
            window.history.pushState({ financeTab: tab }, "", url);
        } else {
            window.history.replaceState({ financeTab: tab }, "", url);
        }
    }

    function positiveQueryInt(name) {
        const url = new URL(window.location.href);
        const value = Number.parseInt(
            url.searchParams.get(name) || "0",
            10
        );

        return Number.isInteger(value) && value > 0
            ? value
            : 0;
    }

    function consumeDeepLinkParameter(name) {
        const url = new URL(window.location.href);

        if (!url.searchParams.has(name)) {
            return;
        }

        url.searchParams.delete(name);

        window.history.replaceState(
            window.history.state,
            "",
            url.pathname + url.search + url.hash
        );
    }

    async function openDeepLinkTarget(tab) {
        if (tab === "topups") {
            const id = positiveQueryInt("focusTopUp");

            if (id > 0 &&
                window.AdminFinanceTopUps &&
                typeof window.AdminFinanceTopUps.openDetails === "function") {
                consumeDeepLinkParameter("focusTopUp");
                await window.AdminFinanceTopUps.openDetails(id);
            }

            return;
        }

        if (tab === "transactions") {
            const id =
                positiveQueryInt("focusTransaction");

            if (id > 0 &&
                window.AdminFinanceTransactions &&
                typeof window.AdminFinanceTransactions.openDetails === "function") {
                consumeDeepLinkParameter("focusTransaction");
                await window.AdminFinanceTransactions.openDetails(id);
            }
        }
    }

    function renderLoadError(error, tab, url) {
        if (!state.host) {
            return;
        }

        const forbidden = error && (error.status === 401 || error.status === 403);

        state.host.innerHTML = `
            <div style="min-height:320px;display:grid;place-items:center;text-align:center;padding:36px;">
                <div>
                    <div style="width:58px;height:58px;display:grid;place-items:center;margin:0 auto 14px;border-radius:18px;background:#FFF0F2;color:#D84D5A;font-size:24px;">
                        <i class="bi ${forbidden ? "bi-shield-lock" : "bi-exclamation-triangle"}"></i>
                    </div>
                    <strong style="display:block;color:#162333;font-size:16px;font-weight:900;">
                        ${forbidden ? "لا تملك صلاحية فتح هذا القسم" : "تعذر تحميل البيانات"}
                    </strong>
                    <p style="max-width:440px;margin:7px auto 16px;color:#708094;font-size:13px;">
                        ${escapeHtml(error && error.message ? error.message : "أعد المحاولة بعد لحظات.")}
                    </p>
                    ${forbidden ? "" : `
                        <button type="button"
                                class="af-btn af-btn--primary"
                                data-finance-retry
                                data-tab="${escapeHtml(tab || "")}"
                                data-url="${escapeHtml(url || "")}">
                            <i class="bi bi-arrow-clockwise"></i>
                            إعادة المحاولة
                        </button>
                    `}
                </div>
            </div>
        `;
    }

    async function loadTab(tab, url, options) {
        const opts = {
            historyMode: "push",
            showLoader: true,
            ...options
        };

        if (!state.root || !state.host) {
            return false;
        }

        const fallbackButton = qsa(".af-tab[data-finance-tab]", state.root)
            .find(button => button.dataset.financeTab === tab);

        const targetUrl = url || (fallbackButton && fallbackButton.dataset.financeTabUrl);
        if (!targetUrl) {
            showToast("تعذر تحديد رابط التبويب.", "danger");
            return false;
        }

        if (state.tabAbortController) {
            state.tabAbortController.abort();
        }

        const controller = new AbortController();
        state.tabAbortController = controller;

        updateActiveTab(tab);
        state.activeUrl = targetUrl;
        updateBrowserUrl(tab, opts.historyMode);

        if (opts.showLoader) {
            state.host.setAttribute("aria-busy", "true");
            state.host.innerHTML = `
                <div class="af-loading-state">
                    <div class="af-loading-state__mark"><span></span><span></span><span></span></div>
                    <strong>جارٍ تجهيز البيانات المالية</strong>
                    <p>لحظات بسيطة...</p>
                </div>
            `;
        }

        try {
            const html = await fetchHtml(targetUrl, {
                signal: controller.signal
            });

            if (controller.signal.aborted) {
                return false;
            }

            state.host.innerHTML = html;
            state.host.setAttribute("aria-busy", "false");
            state.activeUrl = targetUrl;

            document.dispatchEvent(new CustomEvent("adminfinance:tabloaded", {
                detail: {
                    tab,
                    url: targetUrl,
                    host: state.host
                }
            }));

            await openDeepLinkTarget(tab);

            return true;
        } catch (error) {
            if (error && error.name === "AbortError") {
                return false;
            }

            renderLoadError(error, tab, targetUrl);
            showToast(
                error && error.message ? error.message : "تعذر تحميل التبويب.",
                "danger"
            );
            return false;
        } finally {
            if (state.tabAbortController === controller) {
                state.tabAbortController = null;
            }
        }
    }

    async function reloadCurrentTab(urlOverride) {
        const tab = state.activeTab || (state.root && state.root.dataset.selectedTab);
        if (!tab) {
            return false;
        }

        return loadTab(tab, urlOverride || state.activeUrl, {
            historyMode: "replace"
        });
    }

    async function refreshShell() {
        if (!state.root) {
            return null;
        }

        const shellUrl = state.root.dataset.shellUrl;
        if (!shellUrl) {
            return null;
        }

        if (state.shellAbortController) {
            state.shellAbortController.abort();
        }

        const controller = new AbortController();
        state.shellAbortController = controller;

        try {
            const url = buildUrl(shellUrl, {
                financeTab: state.activeTab || state.root.dataset.selectedTab || "overview"
            });

            const payload = await fetchJson(url, {
                signal: controller.signal
            });

            const shell = pick(payload, "shell", "Shell") || {};
            const pending = number(pick(shell, "pendingTopUpsCount", "PendingTopUpsCount"));
            const low = number(pick(shell, "lowBalanceAccountsCount", "LowBalanceAccountsCount"));
            const debt = number(pick(shell, "inDebtAccountsCount", "InDebtAccountsCount"));
            const suspended = number(pick(shell, "suspendedAccountsCount", "SuspendedAccountsCount"));

            qsa('[data-shell-counter="pending-topups"]', state.root)
                .forEach(el => { el.textContent = String(pending); });
            qsa('[data-shell-counter="pending-topups-tab"]', state.root)
                .forEach(el => { el.textContent = String(pending); });
            qsa('[data-shell-counter="low-balance"]', state.root)
                .forEach(el => { el.textContent = String(low); });
            qsa('[data-shell-counter="in-debt"]', state.root)
                .forEach(el => { el.textContent = String(debt); });
            qsa('[data-shell-counter="suspended"]', state.root)
                .forEach(el => { el.textContent = String(suspended); });

            return payload;
        } catch (error) {
            if (error && error.name === "AbortError") {
                return null;
            }

            console.warn("Finance shell refresh failed:", error);
            return null;
        } finally {
            if (state.shellAbortController === controller) {
                state.shellAbortController = null;
            }
        }
    }


    function portalToBody(element) {
        if (!element || element.__afPortalPlaceholder) {
            return element;
        }

        const parent = element.parentNode;
        if (!parent) {
            return element;
        }

        const placeholder = document.createComment("admin-finance-portal");
        parent.insertBefore(placeholder, element);

        element.__afPortalPlaceholder = placeholder;
        element.classList.add("af-finance-portaled");
        document.body.appendChild(element);

        return element;
    }

    function restoreFromBody(element) {
        if (!element) {
            return;
        }

        const placeholder = element.__afPortalPlaceholder;

        if (placeholder && placeholder.parentNode) {
            placeholder.parentNode.replaceChild(element, placeholder);
        }

        element.classList.remove("af-finance-portaled");
        delete element.__afPortalPlaceholder;
    }

    function lockOverlayPage() {
        document.documentElement.classList.add("af-finance-overlay-lock");
        document.body.classList.add("af-finance-overlay-open");
        document.documentElement.style.overflow = "hidden";
        document.body.style.overflow = "hidden";
    }

    function unlockOverlayPageIfSafe() {
        const opened =
            document.querySelector(".af-management-drawer.is-open") ||
            document.querySelector(".acg-modal.is-open") ||
            document.querySelector(".atr-modal.is-open") ||
            document.querySelector(".aft-modal.is-open") ||
            document.querySelector(".afm-modal.is-open");

        if (opened) {
            return;
        }

        document.documentElement.classList.remove("af-finance-overlay-lock");
        document.body.classList.remove("af-finance-overlay-open");
        document.documentElement.style.overflow = "";
        document.body.style.overflow = "";
    }

    function openOverlay(element) {
        if (!element) {
            return;
        }

        portalToBody(element);

        element.hidden = false;
        element.setAttribute("aria-hidden", "false");
        element.style.setProperty("position", "fixed", "important");
        element.style.setProperty("inset", "0", "important");
        element.style.setProperty("width", "100vw", "important");
        element.style.setProperty("height", "100vh", "important");
        element.style.setProperty("z-index", "2000000005", "important");

        void element.offsetWidth;
        element.classList.add("is-open");

        lockOverlayPage();
    }

    function closeOverlay(element) {
        if (!element) {
            return;
        }

        element.classList.remove("is-open");
        element.setAttribute("aria-hidden", "true");

        window.setTimeout(function () {
            restoreFromBody(element);
            unlockOverlayPageIfSafe();
        }, 0);
    }

    function openManagementDrawerFrame() {
        const drawer =
            qs("[data-finance-management-drawer]", state.root) ||
            qs("[data-finance-management-drawer]", document);

        const backdrop =
            qs("[data-finance-management-backdrop]", state.root) ||
            qs("[data-finance-management-backdrop]", document);

        if (backdrop) {
            portalToBody(backdrop);
            backdrop.hidden = false;
            backdrop.classList.add("is-open");
            backdrop.style.setProperty("position", "fixed", "important");
            backdrop.style.setProperty("inset", "0", "important");
            backdrop.style.setProperty("width", "100vw", "important");
            backdrop.style.setProperty("height", "100vh", "important");
            backdrop.style.setProperty("z-index", "2000000000", "important");
        }

        if (drawer) {
            portalToBody(drawer);

            drawer.hidden = false;
            drawer.classList.remove("is-open");
            drawer.setAttribute("aria-hidden", "false");

            // Force the drawer to the LEFT side, opposite the RTL sidebar.
            drawer.style.setProperty("position", "fixed", "important");
            drawer.style.setProperty("top", "12px", "important");
            drawer.style.setProperty("bottom", "12px", "important");
            drawer.style.setProperty("left", "12px", "important");
            drawer.style.setProperty("right", "auto", "important");
            drawer.style.setProperty("transform", "none", "important");
            drawer.style.setProperty("z-index", "2000000001", "important");

            void drawer.offsetWidth;
            drawer.classList.add("is-open");
        }

        lockOverlayPage();
    }

    function closeManagementDrawer() {
        const drawer = qs("[data-finance-management-drawer]", document);
        const backdrop = qs("[data-finance-management-backdrop]", document);

        if (drawer) {
            drawer.classList.remove("is-open");
            drawer.setAttribute("aria-hidden", "true");
            drawer.hidden = true;

            drawer.style.removeProperty("left");
            drawer.style.removeProperty("right");
            drawer.style.removeProperty("top");
            drawer.style.removeProperty("bottom");
            drawer.style.removeProperty("transform");
            drawer.style.removeProperty("z-index");
            drawer.style.removeProperty("position");

            restoreFromBody(drawer);
        }

        if (backdrop) {
            backdrop.classList.remove("is-open");
            backdrop.hidden = true;

            backdrop.style.removeProperty("position");
            backdrop.style.removeProperty("inset");
            backdrop.style.removeProperty("width");
            backdrop.style.removeProperty("height");
            backdrop.style.removeProperty("z-index");

            restoreFromBody(backdrop);
        }

        unlockOverlayPageIfSafe();
    }

    async function openManagement() {
        if (!state.root) {
            return;
        }

        const url = state.root.dataset.managementUrl;
        const host = qs("[data-finance-management-host]", state.root);

        if (!url || !host) {
            return;
        }

        if (state.managementAbortController) {
            state.managementAbortController.abort();
        }

        const controller = new AbortController();
        state.managementAbortController = controller;

        openManagementDrawerFrame();
        host.innerHTML = `
            <div class="af-loading-state" style="height:100%;">
                <div class="af-loading-state__mark"><span></span><span></span><span></span></div>
                <strong>جارٍ تجهيز إدارة المالية</strong>
                <p>تحميل الإعدادات وحسابات المنصة...</p>
            </div>
        `;

        try {
            const html = await fetchHtml(url, {
                signal: controller.signal
            });

            host.innerHTML = html;

            document.dispatchEvent(new CustomEvent("adminfinance:managementloaded", {
                detail: { host }
            }));
        } catch (error) {
            if (error && error.name === "AbortError") {
                return;
            }

            host.innerHTML = `
                <div class="af-loading-state">
                    <strong>تعذر تحميل إدارة المالية</strong>
                    <p>${escapeHtml(error && error.message ? error.message : "أعد المحاولة.")}</p>
                </div>
            `;

            showToast(
                error && error.message ? error.message : "تعذر تحميل إدارة المالية.",
                "danger"
            );
        } finally {
            if (state.managementAbortController === controller) {
                state.managementAbortController = null;
            }
        }
    }

    async function reloadManagement() {
        const host = state.root ? qs("[data-finance-management-host]", state.root) : null;
        const url = state.root ? state.root.dataset.managementUrl : null;

        if (!host || !url) {
            return false;
        }

        try {
            const html = await fetchHtml(url);
            host.innerHTML = html;

            document.dispatchEvent(new CustomEvent("adminfinance:managementloaded", {
                detail: { host }
            }));

            return true;
        } catch (error) {
            showToast(
                error && error.message ? error.message : "تعذر تحديث إدارة المالية.",
                "danger"
            );
            return false;
        }
    }

    function onRootClick(event) {
        const tabButton = event.target.closest("[data-finance-tab]");
        if (tabButton && state.root.contains(tabButton)) {
            const tab = tabButton.dataset.financeTab;
            const url = tabButton.dataset.financeTabUrl;

            if (tab && url) {
                event.preventDefault();
                loadTab(tab, url, {
                    historyMode: tabButton.classList.contains("af-tab") ? "push" : "replace"
                });
            }
            return;
        }

        const refreshButton = event.target.closest("[data-finance-refresh]");
        if (refreshButton && state.root.contains(refreshButton)) {
            event.preventDefault();

            setButtonBusy(refreshButton, true, "تحديث...");
            Promise.all([
                reloadCurrentTab(),
                refreshShell()
            ]).finally(() => {
                setButtonBusy(refreshButton, false);
            });
            return;
        }

        const managementOpen = event.target.closest("[data-finance-management-open]");
        if (managementOpen && state.root.contains(managementOpen)) {
            event.preventDefault();
            openManagement();
            return;
        }

        const managementClose = event.target.closest("[data-finance-management-close]");
        if (managementClose) {
            event.preventDefault();
            closeManagementDrawer();
            return;
        }

        const backdrop = event.target.closest("[data-finance-management-backdrop]");
        if (backdrop) {
            closeManagementDrawer();
            return;
        }

        const retry = event.target.closest("[data-finance-retry]");
        if (retry && state.root.contains(retry)) {
            event.preventDefault();
            loadTab(retry.dataset.tab, retry.dataset.url, {
                historyMode: "replace"
            });
        }
    }

    function onPopState() {
        if (!state.root) {
            return;
        }

        const url = new URL(window.location.href);
        const tab = url.searchParams.get("financeTab") || "overview";
        const button = qsa(".af-tab[data-finance-tab]", state.root)
            .find(item => item.dataset.financeTab === tab);

        if (button) {
            loadTab(tab, button.dataset.financeTabUrl, {
                historyMode: "none"
            });
        }
    }

    function onKeyDown(event) {
        if (event.key !== "Escape" || !state.root) {
            return;
        }

        const drawer = qs("[data-finance-management-drawer]", document);
        if (drawer && drawer.classList.contains("is-open")) {
            closeManagementDrawer();
        }
    }

    function init() {
        if (state.initialized) {
            return;
        }

        state.root = qs("#adminFinancialsRoot");
        if (!state.root) {
            return;
        }

        state.host = qs("#adminFinancialTabHost", state.root);
        if (!state.host) {
            return;
        }

        state.initialized = true;

        const initialUrl =
            new URL(window.location.href);

        if (positiveQueryInt("focusTopUp") > 0) {
            state.activeTab = "topups";
        } else if (positiveQueryInt("focusTransaction") > 0) {
            state.activeTab = "transactions";
        } else {
            state.activeTab =
                initialUrl.searchParams.get("financeTab") ||
                state.root.dataset.selectedTab ||
                "overview";
        }

        // Always start with the financial management drawer fully closed.
        const initialDrawer = qs("[data-finance-management-drawer]", state.root);
        const initialBackdrop = qs("[data-finance-management-backdrop]", state.root);

        if (initialDrawer) {
            initialDrawer.classList.remove("is-open");
            initialDrawer.setAttribute("aria-hidden", "true");
            initialDrawer.hidden = true;
        }

        if (initialBackdrop) {
            initialBackdrop.hidden = true;
        }

        document.documentElement.style.overflow = "";

        document.addEventListener("click", onRootClick);
        window.addEventListener("popstate", onPopState);
        document.addEventListener("keydown", onKeyDown);

        const activeButton =
            qs(`.af-tab[data-finance-tab="${CSS.escape(state.activeTab)}"]`, state.root) ||
            qs(".af-tab[data-finance-tab]", state.root);

        if (activeButton) {
            state.activeTab = activeButton.dataset.financeTab;
            state.activeUrl = activeButton.dataset.financeTabUrl;

            loadTab(state.activeTab, state.activeUrl, {
                historyMode: "replace"
            });
        }
    }

    Finance.qs = qs;
    Finance.qsa = qsa;
    Finance.escapeHtml = escapeHtml;
    Finance.pick = pick;
    Finance.number = number;
    Finance.bool = bool;
    Finance.asArray = asArray;
    Finance.formatNumber = formatNumber;
    Finance.formatMoney = formatMoney;
    Finance.formatDate = formatDate;
    Finance.getCurrency = getCurrency;
    Finance.buildUrl = buildUrl;
    Finance.fetchHtml = fetchHtml;
    Finance.fetchJson = fetchJson;
    Finance.postForm = postForm;
    Finance.setButtonBusy = setButtonBusy;
    Finance.showGlobalLoader = showGlobalLoader;
    Finance.showToast = showToast;
    Finance.portalToBody = portalToBody;
    Finance.restoreFromBody = restoreFromBody;
    Finance.openOverlay = openOverlay;
    Finance.closeOverlay = closeOverlay;
    Finance.lockOverlayPage = lockOverlayPage;
    Finance.unlockOverlayPageIfSafe = unlockOverlayPageIfSafe;
    Finance.loadTab = loadTab;
    Finance.reloadCurrentTab = reloadCurrentTab;
    Finance.refreshShell = refreshShell;
    Finance.openManagement = openManagement;
    Finance.closeManagement = closeManagementDrawer;
    Finance.reloadManagement = reloadManagement;
    Finance.state = state;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})(window, document);
