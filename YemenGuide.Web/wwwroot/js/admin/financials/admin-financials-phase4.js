(function (window, document) {
    "use strict";

    const AUTO_REASON_AR = "تحديث إعدادات المالية من لوحة الإدارة";
    const AUTO_REASON_EN = "Financial settings updated from the admin dashboard";

    function isEnglish() {
        const root = document.getElementById("adminFinancialsRoot");
        return (root?.dataset.uiLang || document.documentElement.lang || "ar")
            .toLowerCase()
            .startsWith("en");
    }

    function patchPortaledManagementReload() {
        const Finance = window.AdminFinance;
        if (!Finance || Finance.__phase4ManagementReloadPatched) {
            return;
        }

        Finance.reloadManagement = async function () {
            const root = document.getElementById("adminFinancialsRoot");
            // The management drawer is portaled to <body> while it is open,
            // so the host must be resolved from document, not only from root.
            const host = document.querySelector("[data-finance-management-host]");
            const url = root?.dataset.managementUrl;

            if (!host || !url) {
                return false;
            }

            try {
                const response = await fetch(url, {
                    method: "GET",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest",
                        "Accept": "text/html"
                    }
                });

                if (!response.ok) {
                    throw new Error(`تعذر تحديث إدارة المالية (${response.status}).`);
                }

                host.innerHTML = await response.text();

                document.dispatchEvent(new CustomEvent("adminfinance:managementloaded", {
                    detail: { host }
                }));

                return true;
            } catch (error) {
                Finance.showToast?.(
                    error && error.message ? error.message : "تعذر تحديث إدارة المالية.",
                    "danger"
                );
                return false;
            }
        };

        Finance.__phase4ManagementReloadPatched = true;
    }

    function prepareSettingsForm(scope) {
        const container = scope || document;
        const forms = [];

        if (container.matches?.("[data-finance-settings-form]")) {
            forms.push(container);
        }

        container.querySelectorAll?.("[data-finance-settings-form]")
            .forEach(function (form) {
                forms.push(form);
            });

        forms.forEach(function (form) {
            if (form.dataset.phase4ReasonReady === "true") return;
            form.dataset.phase4ReasonReady = "true";

            // Remove the visible reason field completely.
            form.querySelectorAll(".afm-field--reason, #financialSettingsReason")
                .forEach(function (element) {
                    const wrapper = element.closest?.(".afm-field--reason");
                    (wrapper || element).remove();
                });

            // Preserve audit logging without asking the admin to type a reason.
            let hidden = form.querySelector('input[type="hidden"][name="ChangeReason"][data-phase4-auto-reason]');
            if (!hidden) {
                hidden = document.createElement("input");
                hidden.type = "hidden";
                hidden.name = "ChangeReason";
                hidden.dataset.phase4AutoReason = "true";
                form.appendChild(hidden);
            }

            hidden.value = isEnglish() ? AUTO_REASON_EN : AUTO_REASON_AR;

            form.addEventListener("submit", function () {
                hidden.value = isEnglish() ? AUTO_REASON_EN : AUTO_REASON_AR;
            }, true);
        });
    }

    function normalizeDarkPortals() {
        if (!document.body.classList.contains("dark-mode")) return;

        document.querySelectorAll(
            ".af-management-drawer, .afm, .afm-modal, .acg-modal, .atr-modal, .aft-modal"
        ).forEach(function (element) {
            element.dataset.phase4Dark = "true";
        });
    }

    function init(scope) {
        prepareSettingsForm(scope || document);
        normalizeDarkPortals();
    }

    document.addEventListener("adminfinance:managementloaded", function (event) {
        init(event.target instanceof Element ? event.target : document);
    });

    document.addEventListener("adminfinance:tabloaded", function () {
        normalizeDarkPortals();
    });

    const observer = new MutationObserver(function (mutations) {
        for (const mutation of mutations) {
            mutation.addedNodes.forEach(function (node) {
                if (node.nodeType === Node.ELEMENT_NODE) {
                    init(node);
                }
            });
        }
    });

    function start() {
        patchPortaledManagementReload();
        init(document);
        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})(window, document);
