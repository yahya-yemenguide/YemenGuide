(function (window, document) {
    "use strict";

    function financeApi() {
        return window.AdminFinance;
    }

    function reportsRoot() {
        return document.querySelector("[data-reports-root]");
    }

    function filterForm() {
        return reportsRoot()?.querySelector("[data-report-filter-form]") || null;
    }

    function cleanParams(form) {
        const params = new URLSearchParams(new FormData(form));

        for (const [key, value] of Array.from(params.entries())) {
            if (value == null || String(value).trim() === "") {
                params.delete(key);
            }
        }

        return params;
    }

    function reportsPath(form) {
        const action = form?.action || "/Admin/Financials/Reports";
        const url = new URL(action, window.location.origin);
        return url.pathname.replace(/\/Index\/?$/i, "").replace(/\/+$/, "");
    }

    async function loadUrl(url, historyMode) {
        const Finance = financeApi();
        if (!Finance) {
            window.location.assign(url);
            return;
        }

        await Finance.loadTab("reports", url, {
            historyMode: historyMode || "replace"
        });
    }

    async function loadReport(form) {
        if (!form) {
            return;
        }

        const params = cleanParams(form);
        const url = `${form.action}${params.toString() ? `?${params.toString()}` : ""}`;
        await loadUrl(url, "replace");
    }

    function exportUrl() {
        const form = filterForm();
        if (!form) {
            return "";
        }

        const params = cleanParams(form);
        params.set("autoPrint", "true");
        return `${reportsPath(form)}/Print?${params.toString()}`;
    }

    async function onSubmit(event) {
        const currentRoot = reportsRoot();
        const form = event.target.closest("[data-report-filter-form]");

        if (!currentRoot || !form || !currentRoot.contains(form)) {
            return;
        }

        event.preventDefault();
        await loadReport(form);
    }

    async function onClick(event) {
        const currentRoot = reportsRoot();
        if (!currentRoot) {
            return;
        }

        const typeButton = event.target.closest("[data-report-type]");
        if (typeButton && currentRoot.contains(typeButton)) {
            event.preventDefault();
            const type = typeButton.dataset.reportType || "";
            if (!type) {
                return;
            }

            typeButton.classList.add("is-loading");
            const url = `/Admin/Financials/Reports?ReportType=${encodeURIComponent(type)}`;
            await loadUrl(url, "replace");
            return;
        }

        const changeButton = event.target.closest("[data-report-change]");
        if (changeButton && currentRoot.contains(changeButton)) {
            event.preventDefault();
            await loadUrl("/Admin/Financials/Reports", "replace");
            return;
        }

        const clearButton = event.target.closest("[data-report-clear-filters]");
        if (clearButton && currentRoot.contains(clearButton)) {
            event.preventDefault();
            const form = filterForm();
            if (!form) {
                return;
            }

            const reportType = form.querySelector("[data-report-type-input]")?.value || "";
            const url = `/Admin/Financials/Reports?ReportType=${encodeURIComponent(reportType)}`;
            await loadUrl(url, "replace");
            return;
        }

        const printLink = event.target.closest("[data-report-print]");
        if (printLink && currentRoot.contains(printLink)) {
            event.preventDefault();
            const url = exportUrl();
            if (url) {
                window.open(url, "_blank", "noopener,noreferrer");
            }
            return;
        }

        const moreToggle = event.target.closest("[data-report-more-toggle]");
        if (moreToggle && currentRoot.contains(moreToggle)) {
            event.preventDefault();
            const morePanel = currentRoot.querySelector("[data-report-more]");
            if (!morePanel) {
                return;
            }

            const willOpen = morePanel.hasAttribute("hidden");
            morePanel.toggleAttribute("hidden", !willOpen);
            morePanel.classList.toggle("is-open", willOpen);
            moreToggle.classList.toggle("is-open", willOpen);
            moreToggle.setAttribute("aria-expanded", willOpen ? "true" : "false");

            const label = moreToggle.querySelector("[data-report-more-label]");
            if (label) {
                label.textContent = willOpen ? "إخفاء الفلاتر" : "إظهار المزيد";
            }
            return;
        }

        const sectionTab = event.target.closest("[data-report-section-tab]");
        if (sectionTab && currentRoot.contains(sectionTab)) {
            event.preventDefault();
            const selectedIndex = sectionTab.dataset.reportSectionTab;

            currentRoot.querySelectorAll("[data-report-section-tab]").forEach(button => {
                const active = button === sectionTab;
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-selected", active ? "true" : "false");
            });

            currentRoot.querySelectorAll("[data-report-section]").forEach(section => {
                section.toggleAttribute("hidden", section.dataset.reportSection !== selectedIndex);
            });
        }
    }

    document.addEventListener("submit", onSubmit);
    document.addEventListener("click", onClick);
})(window, document);
