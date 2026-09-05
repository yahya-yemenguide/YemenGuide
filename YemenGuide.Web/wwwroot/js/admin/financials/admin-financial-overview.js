(function (window, document) {
    "use strict";

    function finance() {
        return window.AdminFinance;
    }

    function getRoot() {
        return document.querySelector("[data-overview-root]");
    }

    function buildFormUrl(form) {
        const params = new URLSearchParams(new FormData(form));
        params.delete("PageNumber");

        for (const [key, value] of Array.from(params.entries())) {
            if (value == null || String(value).trim() === "") {
                params.delete(key);
            }
        }

        return `${form.action}${params.toString() ? `?${params.toString()}` : ""}`;
    }

    async function submitFilter(form) {
        const api = finance();
        if (!api) {
            return;
        }

        await api.loadTab("overview", buildFormUrl(form), {
            historyMode: "replace"
        });
    }

    async function openTargetTab(button) {
        const api = finance();
        if (!api) {
            return;
        }

        const tab = button.dataset.overviewOpenTab;
        if (!tab) {
            return;
        }

        const tabButton = document.querySelector(
            `.af-tab[data-finance-tab="${CSS.escape(tab)}"]`
        );

        if (!tabButton) {
            api.showToast("لا تملك صلاحية فتح هذا القسم.", "warning");
            return;
        }

        let url = tabButton.dataset.financeTabUrl;
        const query = button.dataset.overviewQuery;

        if (query) {
            const queryParams = new URLSearchParams(query);
            url = api.buildUrl(url, queryParams);
        }

        await api.loadTab(tab, url, {
            historyMode: "push"
        });
    }

    async function openTransaction(transactionId) {
        const api = finance();
        if (!api || !transactionId) {
            return;
        }

        const tabButton = document.querySelector(
            '.af-tab[data-finance-tab="transactions"]'
        );

        if (!tabButton) {
            api.showToast("لا تملك صلاحية فتح كشف الحركات.", "warning");
            return;
        }

        const loaded = await api.loadTab(
            "transactions",
            tabButton.dataset.financeTabUrl,
            { historyMode: "push" }
        );

        if (loaded &&
            window.AdminFinanceTransactions &&
            typeof window.AdminFinanceTransactions.openDetails === "function") {
            window.AdminFinanceTransactions.openDetails(transactionId);
        }
    }

    function onSubmit(event) {
        const form = event.target.closest("[data-overview-filter-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        submitFilter(form);
    }

    function onClick(event) {
        const root = getRoot();
        if (!root) {
            return;
        }

        const reset = event.target.closest("[data-overview-reset]");
        if (reset && root.contains(reset)) {
            event.preventDefault();

            const form = reset.closest("form");
            if (form) {
                form.reset();
                finance().loadTab("overview", form.action, {
                    historyMode: "replace"
                });
            }
            return;
        }

        const targetTab = event.target.closest("[data-overview-open-tab]");
        if (targetTab && root.contains(targetTab)) {
            event.preventDefault();
            openTargetTab(targetTab);
            return;
        }

        const transactionButton = event.target.closest("[data-overview-transaction-id]");
        if (transactionButton && root.contains(transactionButton)) {
            event.preventDefault();
            openTransaction(transactionButton.dataset.overviewTransactionId);
        }
    }

    document.addEventListener("submit", onSubmit);
    document.addEventListener("click", onClick);
})(window, document);
