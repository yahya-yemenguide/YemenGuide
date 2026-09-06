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
        init(document);
        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})(window, document);
