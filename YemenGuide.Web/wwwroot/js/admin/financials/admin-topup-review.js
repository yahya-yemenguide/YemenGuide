(function (window, document) {
    "use strict";

    const state = {
        currentId: null,
        currentDetails: null,
        detailsAbortController: null,
        proofUrl: null
    };

    function api() {
        return window.AdminFinance;
    }

    function root() {
        return document.querySelector("[data-topup-root]");
    }

    function openModal(element) {
        const Finance = api();
        if (Finance && typeof Finance.openOverlay === "function") {
            Finance.openOverlay(element);
            return;
        }

        if (!element) return;
        element.classList.add("is-open");
        element.setAttribute("aria-hidden", "false");
    }

    function closeModal(element) {
        const Finance = api();
        if (Finance && typeof Finance.closeOverlay === "function") {
            Finance.closeOverlay(element);
            return;
        }

        if (!element) return;
        element.classList.remove("is-open");
        element.setAttribute("aria-hidden", "true");
    }

    function setText(selector, value, scope) {
        const element = (scope || document)?.querySelector(selector);
        if (element) {
            element.textContent = value == null || value === "" ? "—" : String(value);
        }
    }

    function setHidden(selector, hidden, scope) {
        const element = (scope || document)?.querySelector(selector);
        if (element) {
            element.hidden = !!hidden;
        }
    }

    function detailsUrl(id) {
        const Finance = api();
        const currentRoot = root();
        return Finance.buildUrl(currentRoot.dataset.detailsUrl, {
            commissionTransactionId: id
        });
    }

    function proofUrl(id) {
        const currentRoot = root();
        if (!currentRoot) {
            return "";
        }

        const base = currentRoot.dataset.proofUrl || "";
        return `${base.replace(/\/+$/, "")}/${encodeURIComponent(id)}`;
    }

    function isPending(details) {
        const Finance = api();
        const status = String(
            Finance.pick(details, "status", "Status") || ""
        ).toLowerCase();

        const canApprove = Finance.pick(details, "canApprove", "CanApprove");
        return canApprove !== undefined
            ? Finance.bool(canApprove)
            : status.includes("pending");
    }

    function renderRelatedTransactions(items, currency) {
        const Finance = api();
        const rows = Finance.asArray(items);

        if (!rows.length) {
            return `
                <div class="atr-empty" style="min-height:120px;">
                    <span class="atr-empty__icon"><i class="bi bi-arrow-left-right"></i></span>
                    <h4>لا توجد حركات مرتبطة</h4>
                    <p>ستظهر الحركات الناتجة عن الطلب بعد المعالجة.</p>
                </div>
            `;
        }

        return rows.map(item => {
            const type = Finance.pick(item, "transactionTypeLabel", "TransactionTypeLabel") || "حركة مالية";
            const amount = Finance.pick(item, "amount", "Amount");
            const date = Finance.pick(item, "createdAt", "CreatedAt");
            const status = Finance.pick(item, "statusLabel", "StatusLabel") ||
                Finance.pick(item, "status", "Status") || "—";

            return `
                <div class="atr-related-row">
                    <div>
                        <strong>${Finance.escapeHtml(type)}</strong>
                        <small>${Finance.escapeHtml(status)}</small>
                    </div>
                    <strong>${Finance.escapeHtml(Finance.formatMoney(amount, currency))}</strong>
                    <small>${Finance.escapeHtml(Finance.formatDate(date, false))}</small>
                </div>
            `;
        }).join("");
    }

    function populateDetails(payload) {
        const Finance = api();
        const details = Finance.pick(payload, "data", "Data") || {};
        const currency = Finance.getCurrency();

        state.currentDetails = details;
        state.currentId = Finance.pick(
            details,
            "commissionTransactionId",
            "CommissionTransactionId"
        );

        setText("[data-topup-number]", `#${state.currentId || "—"}`);
        setText("[data-topup-place]", Finance.pick(details, "placeName", "PlaceName"));
        setText("[data-topup-owner]", Finance.pick(details, "ownerName", "OwnerName"));
        setText(
            "[data-topup-amount]",
            Finance.formatMoney(Finance.pick(details, "amount", "Amount"), currency)
        );
        setText(
            "[data-topup-reference]",
            Finance.pick(details, "referenceNumber", "ReferenceNumber") || "بدون مرجع"
        );
        setText(
            "[data-topup-transfer-date]",
            Finance.formatDate(Finance.pick(details, "transferDate", "TransferDate"), false)
        );
        setText(
            "[data-topup-wallet]",
            Finance.pick(details, "platformWalletName", "PlatformWalletName") || "—"
        );
        setText(
            "[data-topup-beneficiary]",
            Finance.pick(details, "platformBeneficiaryName", "PlatformBeneficiaryName") || "—"
        );

        setText(
            "[data-topup-debt-before]",
            Finance.formatMoney(Finance.pick(details, "debtBefore", "DebtBefore"), currency)
        );
        setText(
            "[data-topup-debt-payment]",
            Finance.formatMoney(Finance.pick(details, "debtPaymentAmount", "DebtPaymentAmount"), currency)
        );
        setText(
            "[data-topup-credit]",
            Finance.formatMoney(
                Finance.pick(details, "creditedToAvailableBalance", "CreditedToAvailableBalance"),
                currency
            )
        );
        setText(
            "[data-topup-available-after]",
            Finance.formatMoney(
                Finance.pick(details, "availableBalanceAfter", "AvailableBalanceAfter"),
                currency
            )
        );

        const relatedHost = document.querySelector("[data-topup-related-transactions]");
        if (relatedHost) {
            relatedHost.innerHTML = renderRelatedTransactions(
                Finance.pick(details, "relatedTransactions", "RelatedTransactions"),
                currency
            );
        }

        state.proofUrl = proofUrl(state.currentId);

        const receipt = Finance.pick(details, "receiptImage", "ReceiptImage");
        const image = document.querySelector("[data-topup-proof-image]");
        const empty = document.querySelector("[data-topup-proof-empty]");
        const openProof = document.querySelector("[data-topup-proof-open-new]");

        if (receipt && image) {
            image.hidden = false;
            image.src = state.proofUrl;
            if (empty) empty.hidden = true;
            if (openProof) openProof.disabled = false;
        } else {
            if (image) {
                image.hidden = true;
                image.removeAttribute("src");
            }
            if (empty) empty.hidden = false;
            if (openProof) openProof.disabled = true;
            state.proofUrl = null;
        }

        const decisionArea = document.querySelector("[data-topup-decision-area]");
        if (decisionArea) {
            decisionArea.hidden = !isPending(details);
        }

        const approveId = document.querySelector("[data-topup-approve-id]");
        if (approveId) {
            approveId.value = state.currentId || "";
        }

        const rejectId = document.querySelector("[data-topup-reject-id]");
        if (rejectId) {
            rejectId.value = state.currentId || "";
        }

        setHidden("[data-topup-loading]", true);
        setHidden("[data-topup-content]", false);
    }

    async function openDetails(id) {
        const Finance = api();
        const currentRoot = root();

        if (!Finance || !currentRoot || !id) {
            return;
        }

        if (state.detailsAbortController) {
            state.detailsAbortController.abort();
        }

        const controller = new AbortController();
        state.detailsAbortController = controller;

        const reviewModal = currentRoot.querySelector("[data-topup-modal]");
        openModal(reviewModal);

        setHidden("[data-topup-loading]", false);
        setHidden("[data-topup-content]", true);

        try {
            const payload = await Finance.fetchJson(detailsUrl(id), {
                signal: controller.signal
            });

            populateDetails(payload);
        } catch (error) {
            if (error && error.name === "AbortError") {
                return;
            }

            closeModal(reviewModal);
            Finance.showToast(
                error && error.message ? error.message : "تعذر تحميل تفاصيل طلب الشحن.",
                "danger"
            );
        } finally {
            if (state.detailsAbortController === controller) {
                state.detailsAbortController = null;
            }
        }
    }

    function buildFilterUrl(form) {
        const params = new URLSearchParams(new FormData(form));

        for (const [key, value] of Array.from(params.entries())) {
            if (value == null || String(value).trim() === "") {
                params.delete(key);
            }
        }

        params.set("PageNumber", params.get("PageNumber") || "1");

        return `${form.action}${params.toString() ? `?${params.toString()}` : ""}`;
    }

    async function reloadList() {
        const Finance = api();
        if (!Finance) {
            return;
        }

        await Finance.reloadCurrentTab();
        await Finance.refreshShell();
    }

    async function submitApprove(form, submitter) {
        const Finance = api();
        if (!Finance || !form || !state.currentId) {
            return;
        }

        const button = submitter || form.querySelector('[type="submit"]');
        Finance.setButtonBusy(button, true, "اعتماد...");

        try {
            const payload = await Finance.postForm(form, submitter);
            const message =
                Finance.pick(payload, "message", "Message") ||
                "تم اعتماد طلب الشحن بنجاح.";

            Finance.showToast(message, "success", "تم اعتماد الشحن");

            closeModal(form.closest(".atr-modal"));
            await reloadList();

            const receiptUrl = Finance.pick(payload, "receiptUrl", "ReceiptUrl");
            if (receiptUrl) {
                window.AdminFinanceLastTopUpReceiptUrl = receiptUrl;
            }
        } catch (error) {
            Finance.showToast(
                error && error.message ? error.message : "تعذر اعتماد طلب الشحن.",
                "danger"
            );
        } finally {
            Finance.setButtonBusy(button, false);
        }
    }

    async function submitReject(form, submitter) {
        const Finance = api();
        if (!Finance || !form || !state.currentId) {
            return;
        }

        if (!form.reportValidity()) {
            return;
        }

        const button = submitter || form.querySelector('[type="submit"]');
        Finance.setButtonBusy(button, true, "رفض...");

        try {
            const payload = await Finance.postForm(form, submitter);
            const message =
                Finance.pick(payload, "message", "Message") ||
                "تم رفض طلب الشحن.";

            Finance.showToast(message, "success", "تم تسجيل القرار");

            closeModal(form.closest(".atr-modal"));
            closeModal(document.querySelector("[data-topup-modal]"));

            await reloadList();
        } catch (error) {
            Finance.showToast(
                error && error.message ? error.message : "تعذر رفض طلب الشحن.",
                "danger"
            );
        } finally {
            Finance.setButtonBusy(button, false);
        }
    }

    function belongsToTopUpUi(target, currentRoot) {
        return !!(
            target &&
            currentRoot &&
            (currentRoot.contains(target) || target.closest(".atr-modal"))
        );
    }

    async function onSubmit(event) {
        const currentRoot = root();
        if (!currentRoot) {
            return;
        }

        const filterForm = event.target.closest("[data-topup-filter-form]");
        if (filterForm && belongsToTopUpUi(filterForm, currentRoot)) {
            event.preventDefault();
            const params = new URLSearchParams(new FormData(filterForm));
            params.set("PageNumber", "1");

            for (const [key, value] of Array.from(params.entries())) {
                if (value == null || String(value).trim() === "") {
                    params.delete(key);
                }
            }

            await api().loadTab(
                "topups",
                `${filterForm.action}${params.toString() ? `?${params.toString()}` : ""}`,
                { historyMode: "replace" }
            );
            return;
        }

        const approveForm = event.target.closest("[data-topup-approve-form]");
        if (approveForm && belongsToTopUpUi(approveForm, currentRoot)) {
            event.preventDefault();
            await submitApprove(approveForm, event.submitter);
            return;
        }

        const rejectForm = event.target.closest("[data-topup-reject-form]");
        if (rejectForm && belongsToTopUpUi(rejectForm, currentRoot)) {
            event.preventDefault();
            await submitReject(rejectForm, event.submitter);
        }
    }

    async function onClick(event) {
        const currentRoot = root();
        const Finance = api();

        if (!currentRoot || !Finance) {
            return;
        }

        const statusTab = event.target.closest("[data-topup-status]");
        if (statusTab && belongsToTopUpUi(statusTab, currentRoot)) {
            event.preventDefault();

            const form = currentRoot.querySelector("[data-topup-filter-form]");
            const statusInput = currentRoot.querySelector("[data-topup-status-input]");

            if (!form || !statusInput) {
                return;
            }

            statusInput.value = statusTab.dataset.topupStatus || "";

            const params = new URLSearchParams(new FormData(form));
            params.set("PageNumber", "1");

            for (const [key, value] of Array.from(params.entries())) {
                if (value == null || String(value).trim() === "") {
                    params.delete(key);
                }
            }

            await Finance.loadTab(
                "topups",
                `${form.action}${params.toString() ? `?${params.toString()}` : ""}`,
                { historyMode: "replace" }
            );
            return;
        }

        const review = event.target.closest("[data-topup-review]");
        if (review && belongsToTopUpUi(review, currentRoot)) {
            event.preventDefault();
            openDetails(review.dataset.topupId);
            return;
        }

        const proof = event.target.closest("[data-topup-proof]");
        if (proof && belongsToTopUpUi(proof, currentRoot)) {
            event.preventDefault();
            const url = proofUrl(proof.dataset.topupId);
            if (url) {
                window.open(url, "_blank", "noopener,noreferrer");
            }
            return;
        }

        const pageButton = event.target.closest("[data-topup-page]");
        if (pageButton && belongsToTopUpUi(pageButton, currentRoot) && !pageButton.disabled) {
            event.preventDefault();

            const page = Number(pageButton.dataset.topupPage);
            const form = currentRoot.querySelector("[data-topup-filter-form]");
            if (!form || !Number.isFinite(page) || page < 1) {
                return;
            }

            const params = new URLSearchParams(new FormData(form));
            params.set("PageNumber", String(page));

            await Finance.loadTab(
                "topups",
                `${form.action}?${params.toString()}`,
                { historyMode: "replace" }
            );
            return;
        }

        const close = event.target.closest("[data-topup-close]");
        if (close && belongsToTopUpUi(close, currentRoot)) {
            event.preventDefault();
            closeModal(close.closest(".atr-modal"));
            return;
        }

        const proofOpen = event.target.closest("[data-topup-proof-open-new]");
        if (proofOpen && belongsToTopUpUi(proofOpen, currentRoot)) {
            event.preventDefault();
            if (state.proofUrl) {
                window.open(state.proofUrl, "_blank", "noopener,noreferrer");
            }
            return;
        }

        const rejectOpen = event.target.closest("[data-topup-open-reject]");
        if (rejectOpen && belongsToTopUpUi(rejectOpen, currentRoot)) {
            event.preventDefault();

            const rejectModal = currentRoot.querySelector("[data-topup-reject-modal]");
            const rejectId = currentRoot.querySelector("[data-topup-reject-id]");
            const textarea = currentRoot.querySelector('#topupRejectionReason');

            if (rejectId) rejectId.value = state.currentId || "";
            if (textarea) textarea.value = "";

            openModal(rejectModal);
            window.setTimeout(() => textarea && textarea.focus(), 80);
            return;
        }

        const rejectClose = event.target.closest("[data-topup-reject-close]");
        if (rejectClose && belongsToTopUpUi(rejectClose, currentRoot)) {
            event.preventDefault();
            closeModal(rejectClose.closest(".atr-modal"));
        }
    }

    function onKeyDown(event) {
        if (event.key !== "Escape") {
            return;
        }

        const currentRoot = root();
        if (!currentRoot) {
            return;
        }

        const opened = Array.from(document.querySelectorAll(".atr-modal.is-open"));
        if (opened.length) {
            closeModal(opened[opened.length - 1]);
        }
    }

    document.addEventListener("submit", onSubmit);
    document.addEventListener("click", onClick);
    document.addEventListener("keydown", onKeyDown);

    window.AdminFinanceTopUps = {
        openDetails
    };
})(window, document);
