(function (window, document) {
    "use strict";

    function api() {
        return window.AdminFinance;
    }

    function root() {
        return document.querySelector("[data-reports-root]");
    }

    function filterForm() {
        return root()?.querySelector("[data-report-filter-form]") || null;
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

    function baseReportsPath(form) {
        const url = new URL(form.action, window.location.origin);
        return url.pathname.replace(/\/Index\/?$/i, "").replace(/\/+$/, "");
    }

    function exportUrl(kind) {
        const form = filterForm();
        if (!form) {
            return "";
        }

        const params = cleanParams(form);
        const base = baseReportsPath(form);

        if (kind === "print") {
            params.set("autoPrint", "true");
            return `${base}/Print?${params.toString()}`;
        }

        return `${base}/Excel?${params.toString()}`;
    }

    async function loadReport(form) {
        const Finance = api();
        if (!Finance || !form) {
            return;
        }

        const params = cleanParams(form);
        const url = `${form.action}${params.toString() ? `?${params.toString()}` : ""}`;

        await Finance.loadTab("reports", url, {
            historyMode: "replace"
        });
    }

    async function onSubmit(event) {
        const currentRoot = root();
        if (!currentRoot) {
            return;
        }

        const form = event.target.closest("[data-report-filter-form]");
        if (!form || !currentRoot.contains(form)) {
            return;
        }

        event.preventDefault();
        await loadReport(form);
    }

    async function onClick(event) {
        const currentRoot = root();
        const Finance = api();

        if (!currentRoot || !Finance) {
            return;
        }

        const typeButton = event.target.closest("[data-report-type]");
        if (typeButton && currentRoot.contains(typeButton)) {
            event.preventDefault();

            const form = filterForm();
            const input = currentRoot.querySelector("[data-report-type-input]");

            if (!form || !input) {
                return;
            }

            input.value = typeButton.dataset.reportType || "";
            await loadReport(form);
            return;
        }

        const clear = event.target.closest("[data-report-clear-filters]");
        if (clear && currentRoot.contains(clear)) {
            event.preventDefault();

            const form = filterForm();
            if (!form) {
                return;
            }

            const currentType = currentRoot.querySelector("[data-report-type-input]")?.value || "";
            form.reset();

            const typeInput = currentRoot.querySelector("[data-report-type-input]");
            if (typeInput) {
                typeInput.value = currentType;
            }

            await loadReport(form);
            return;
        }

        const printLink = event.target.closest('.afr-section-head__actions a[href*="/Print"]');
        if (printLink && currentRoot.contains(printLink)) {
            event.preventDefault();
            const url = exportUrl("print");
            if (url) {
                window.open(url, "_blank", "noopener,noreferrer");
            }
            return;
        }

        const excelLink = event.target.closest('.afr-section-head__actions a[href*="/Excel"]');
        if (excelLink && currentRoot.contains(excelLink)) {
            event.preventDefault();

            const url = exportUrl("excel");
            if (url) {
                window.location.assign(url);
            }
        }
    }

    document.addEventListener("submit", onSubmit);
    document.addEventListener("click", onClick);
})(window, document);
