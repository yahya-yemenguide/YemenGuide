(function (window, document) {
    "use strict";

    const Phase1 = window.AdminFinancePhase1 = window.AdminFinancePhase1 || {
        managementTab: null
    };

    function toArray(list) {
        return Array.prototype.slice.call(list || []);
    }

    function visibleTabs(container, selector) {
        return toArray(container ? container.querySelectorAll(selector) : [])
            .filter(function (item) {
                return !item.disabled && item.offsetParent !== null;
            });
    }

    function syncMainTabAccessibility() {
        const root = document.querySelector("#adminFinancialsRoot");
        if (!root) return;

        const tabs = toArray(root.querySelectorAll(".af-tab[data-finance-tab]"));
        tabs.forEach(function (tab) {
            const active = tab.classList.contains("is-active");
            tab.setAttribute("role", "tab");
            tab.setAttribute("aria-selected", active ? "true" : "false");
            tab.tabIndex = active ? 0 : -1;
        });
    }

    function moveTabFocus(tabs, currentIndex, key) {
        if (!tabs.length) return null;

        if (key === "Home") return tabs[0];
        if (key === "End") return tabs[tabs.length - 1];

        if (key === "ArrowRight") {
            return tabs[(currentIndex + 1) % tabs.length];
        }

        if (key === "ArrowLeft") {
            return tabs[(currentIndex - 1 + tabs.length) % tabs.length];
        }

        return null;
    }

    function onMainTabsKeyDown(event) {
        const tab = event.target.closest(".af-tab[data-finance-tab]");
        if (!tab) return;

        const nav = tab.closest("[data-finance-keyboard-tabs]");
        if (!nav) return;

        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
            return;
        }

        const tabs = visibleTabs(nav, ".af-tab[data-finance-tab]");
        const index = tabs.indexOf(tab);
        const target = moveTabFocus(tabs, index < 0 ? 0 : index, event.key);
        if (!target) return;

        event.preventDefault();
        target.focus({ preventScroll: true });
        target.click();
    }

    function managementRoot() {
        return document.querySelector("[data-financial-management-root]");
    }

    function managementTabs(root) {
        return visibleTabs(root, ".afm-tab[data-afm-tab]");
    }

    function setManagementTab(root, name, focusTab) {
        if (!root || !name) return;

        const tabs = managementTabs(root);
        const exists = tabs.some(function (tab) {
            return tab.dataset.afmTab === name;
        });

        if (!exists && tabs.length) {
            name = tabs[0].dataset.afmTab;
        }

        if (!name) return;

        Phase1.managementTab = name;

        tabs.forEach(function (tab) {
            const active = tab.dataset.afmTab === name;
            tab.classList.toggle("is-active", active);
            tab.setAttribute("aria-selected", active ? "true" : "false");
            tab.tabIndex = active ? 0 : -1;

            if (active && focusTab) {
                tab.focus({ preventScroll: true });
            }
        });

        const form = root.querySelector("[data-finance-settings-form]");
        if (form) {
            form.hidden = name === "wallets";
        }

        toArray(root.querySelectorAll("[data-afm-panel]")).forEach(function (panel) {
            const active = panel.dataset.afmPanel === name;
            panel.hidden = !active;
            panel.classList.toggle("is-active", active);
        });

        const body = root.querySelector(".afm-body");
        if (body) {
            body.scrollTop = 0;
        }
    }

    function initManagementTabs(root) {
        root = root || managementRoot();
        if (!root) return;

        const tabs = managementTabs(root);
        if (!tabs.length) return;

        const remembered = Phase1.managementTab;
        const requested = remembered || root.dataset.afmDefaultTab || tabs[0].dataset.afmTab;
        setManagementTab(root, requested, false);
    }

    function onManagementClick(event) {
        const tab = event.target.closest(".afm-tab[data-afm-tab]");
        if (!tab) return;

        const root = tab.closest("[data-financial-management-root]");
        if (!root) return;

        event.preventDefault();
        setManagementTab(root, tab.dataset.afmTab, false);
    }

    function onManagementKeyDown(event) {
        const tab = event.target.closest(".afm-tab[data-afm-tab]");
        if (!tab) return;

        const root = tab.closest("[data-financial-management-root]");
        if (!root) return;

        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
            return;
        }

        const tabs = managementTabs(root);
        const index = tabs.indexOf(tab);
        const target = moveTabFocus(tabs, index < 0 ? 0 : index, event.key);
        if (!target) return;

        event.preventDefault();
        setManagementTab(root, target.dataset.afmTab, true);
    }

    function keepToastAboveOverlays() {
        const stack = document.querySelector("#adminFinanceToastStack");
        if (stack) {
            stack.style.setProperty("z-index", "2000000030", "important");
        }

        const Finance = window.AdminFinance;
        if (!Finance || Finance.__phase1GlobalToastInstalled) return;

        Finance.__phase1GlobalToastInstalled = true;

        Finance.showToast = function (message, type, title, duration) {
            let globalStack = document.querySelector("#adminFinanceGlobalToastStack");
            if (!globalStack) {
                globalStack = document.createElement("div");
                globalStack.id = "adminFinanceGlobalToastStack";
                globalStack.className = "af-toast-stack af-toast-stack--global";
                globalStack.setAttribute("aria-live", "polite");
                document.body.appendChild(globalStack);
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

            const escape = typeof Finance.escapeHtml === "function"
                ? Finance.escapeHtml
                : function (value) {
                    const div = document.createElement("div");
                    div.textContent = value == null ? "" : String(value);
                    return div.innerHTML;
                };

            const toast = document.createElement("div");
            toast.className = `af-toast af-toast--${tone}`;
            toast.innerHTML = `
                <span class="af-toast__icon"><i class="bi ${icon}"></i></span>
                <div class="af-toast__body">
                    <strong>${escape(title || (
                        tone === "success" ? "تم بنجاح" :
                        tone === "danger" ? "تعذر التنفيذ" :
                        tone === "warning" ? "تنبيه" : "معلومة"
                    ))}</strong>
                    <p>${escape(message || "")}</p>
                </div>
                <button type="button" class="af-toast__close" aria-label="إغلاق">
                    <i class="bi bi-x-lg"></i>
                </button>
            `;

            const remove = function () {
                if (toast.parentNode) toast.remove();
            };

            const close = toast.querySelector(".af-toast__close");
            if (close) close.addEventListener("click", remove);

            globalStack.appendChild(toast);
            window.setTimeout(remove, duration || 5000);
        };
    }

    document.addEventListener("keydown", onMainTabsKeyDown);
    document.addEventListener("click", onManagementClick);
    document.addEventListener("keydown", onManagementKeyDown);

    document.addEventListener("adminfinance:tabloaded", function () {
        syncMainTabAccessibility();
    });

    document.addEventListener("adminfinance:managementloaded", function () {
        initManagementTabs(managementRoot());
        keepToastAboveOverlays();
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            syncMainTabAccessibility();
            initManagementTabs(managementRoot());
            keepToastAboveOverlays();
        }, { once: true });
    } else {
        syncMainTabAccessibility();
        initManagementTabs(managementRoot());
        keepToastAboveOverlays();
    }
})(window, document);
