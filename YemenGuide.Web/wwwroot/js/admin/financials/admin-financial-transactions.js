(function (window, document) {
    "use strict";

    const state = {
        abortController: null
    };

    function api() {
        return window.AdminFinance;
    }

    function root() {
        return document.querySelector("[data-transactions-root]");
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

    function setText(selector, value) {
        const element = document.querySelector(selector);
        if (element) {
            element.textContent = value == null || value === "" ? "—" : String(value);
        }
    }

    function setHidden(selector, hidden) {
        const element = document.querySelector(selector);
        if (element) {
            element.hidden = !!hidden;
        }
    }

    function typeIcon(type) {
        const value = String(type || "").toLowerCase();

        if (value.includes("topup")) return "bi-wallet2";
        if (value.includes("hold")) return "bi-lock";
        if (value.includes("charge")) return "bi-cash-coin";
        if (value.includes("release")) return "bi-unlock";
        if (value.includes("debtpayment")) return "bi-check2-circle";
        if (value.includes("debt")) return "bi-exclamation-triangle";
        if (value.includes("adjust")) return "bi-sliders";
        return "bi-arrow-left-right";
    }

    function detailsUrl(id) {
        const Finance = api();
        const currentRoot = root();

        return Finance.buildUrl(currentRoot.dataset.detailsUrl, {
            commissionTransactionId: id
        });
    }

    function populate(payload) {
        const Finance = api();
        const details = Finance.pick(payload, "data", "Data") || {};
        const currency = Finance.getCurrency();

        const id = Finance.pick(details, "commissionTransactionId", "CommissionTransactionId");
        const type = Finance.pick(details, "transactionType", "TransactionType");
        const typeLabel = Finance.pick(details, "transactionTypeLabel", "TransactionTypeLabel");
        const status = Finance.pick(details, "status", "Status");
        const statusLabel = Finance.pick(details, "statusLabel", "StatusLabel") || status || "—";
        const bookingId = Finance.pick(details, "bookingId", "BookingId");
        const bookingState = Finance.pick(details, "bookingState", "BookingState");
        const reference = Finance.pick(details, "referenceNumber", "ReferenceNumber");
        const notes = Finance.pick(details, "notes", "Notes");
        const rejection = Finance.pick(details, "rejectionReason", "RejectionReason");

        setText("[data-transaction-number]", `#${id || "—"}`);
        setText(
            "[data-transaction-subtitle]",
            `${Finance.pick(details, "placeName", "PlaceName") || "منشأة"} • ${Finance.formatDate(Finance.pick(details, "createdAt", "CreatedAt"), true)}`
        );
        setText("[data-transaction-type]", typeLabel || type || "حركة مالية");
        setText(
            "[data-transaction-amount]",
            Finance.formatMoney(Finance.pick(details, "amount", "Amount"), currency)
        );
        setText("[data-transaction-status]", statusLabel);

        const iconHost = document.querySelector("[data-transaction-type-icon]");
        if (iconHost) {
            iconHost.innerHTML = `<i class="bi ${typeIcon(type)}"></i>`;
        }

        setText(
            "[data-transaction-available-before]",
            Finance.formatMoney(Finance.pick(details, "availableBalanceBefore", "AvailableBalanceBefore"), currency)
        );
        setText(
            "[data-transaction-available-after]",
            Finance.formatMoney(Finance.pick(details, "availableBalanceAfter", "AvailableBalanceAfter"), currency)
        );
        setText(
            "[data-transaction-held-before]",
            Finance.formatMoney(Finance.pick(details, "heldBalanceBefore", "HeldBalanceBefore"), currency)
        );
        setText(
            "[data-transaction-held-after]",
            Finance.formatMoney(Finance.pick(details, "heldBalanceAfter", "HeldBalanceAfter"), currency)
        );
        setText(
            "[data-transaction-debt-before]",
            Finance.formatMoney(Finance.pick(details, "debtBefore", "DebtBefore"), currency)
        );
        setText(
            "[data-transaction-debt-after]",
            Finance.formatMoney(Finance.pick(details, "debtAfter", "DebtAfter"), currency)
        );

        setText("[data-transaction-place]", Finance.pick(details, "placeName", "PlaceName"));
        setText("[data-transaction-owner]", Finance.pick(details, "ownerName", "OwnerName"));
        setText(
            "[data-transaction-booking]",
            bookingId ? `#${bookingId}${bookingState ? ` — ${bookingState}` : ""}` : "غير مرتبطة بحجز"
        );
        setText("[data-transaction-reference]", reference || "بدون مرجع");
        setText(
            "[data-transaction-created-at]",
            Finance.formatDate(Finance.pick(details, "createdAt", "CreatedAt"), true)
        );
        setText(
            "[data-transaction-created-by]",
            Finance.pick(details, "createdByName", "CreatedByName") || "النظام"
        );
        setText(
            "[data-transaction-reviewed-by]",
            Finance.pick(details, "reviewedByName", "ReviewedByName") || "—"
        );
        setText(
            "[data-transaction-reviewed-at]",
            Finance.formatDate(Finance.pick(details, "reviewedAt", "ReviewedAt"), true)
        );

        const notesSection = document.querySelector("[data-transaction-notes-section]");
        const notesHost = document.querySelector("[data-transaction-notes]");
        const combined = [notes, rejection].filter(Boolean);

        if (notesSection) {
            notesSection.hidden = !combined.length;
        }

        if (notesHost) {
            notesHost.innerHTML = combined.length
                ? combined.map((item, index) => `
                    <p>
                        ${index === 1 && rejection ? "<strong>سبب الرفض:</strong> " : ""}
                        ${Finance.escapeHtml(item)}
                    </p>
                `).join("")
                : "";
        }

        setHidden("[data-transaction-loading]", true);
        setHidden("[data-transaction-content]", false);
    }

    async function openDetails(id) {
        const Finance = api();
        const currentRoot = root();

        if (!Finance || !currentRoot || !id) {
            return;
        }

        if (state.abortController) {
            state.abortController.abort();
        }

        const controller = new AbortController();
        state.abortController = controller;

        const detailsModal = currentRoot.querySelector("[data-transaction-modal]");
        openModal(detailsModal);
        setHidden("[data-transaction-loading]", false);
        setHidden("[data-transaction-content]", true);

        try {
            const payload = await Finance.fetchJson(detailsUrl(id), {
                signal: controller.signal
            });

            populate(payload);
        } catch (error) {
            if (error && error.name === "AbortError") {
                return;
            }

            closeModal(detailsModal);
            Finance.showToast(
                error && error.message ? error.message : "تعذر تحميل تفاصيل الحركة المالية.",
                "danger"
            );
        } finally {
            if (state.abortController === controller) {
                state.abortController = null;
            }
        }
    }

    function belongsToTransactionsUi(target, currentRoot) {
        return !!(
            target &&
            currentRoot &&
            (currentRoot.contains(target) || target.closest(".aft-modal"))
        );
    }

    async function onSubmit(event) {
        const currentRoot = root();
        if (!currentRoot) {
            return;
        }

        const form = event.target.closest("[data-transactions-filter-form]");
        if (!form || !belongsToTransactionsUi(form, currentRoot)) {
            return;
        }

        event.preventDefault();

        const params = new URLSearchParams(new FormData(form));
        params.set("PageNumber", "1");

        for (const [key, value] of Array.from(params.entries())) {
            if (value == null || String(value).trim() === "") {
                params.delete(key);
            }
        }

        await api().loadTab(
            "transactions",
            `${form.action}${params.toString() ? `?${params.toString()}` : ""}`,
            { historyMode: "replace" }
        );
    }

    async function onClick(event) {
        const currentRoot = root();
        const Finance = api();

        if (!currentRoot || !Finance) {
            return;
        }

        const detailsButton = event.target.closest("[data-transaction-details]");
        if (detailsButton && belongsToTransactionsUi(detailsButton, currentRoot)) {
            event.preventDefault();
            openDetails(detailsButton.dataset.transactionId);
            return;
        }

        const pageButton = event.target.closest("[data-transactions-page]");
        if (pageButton && belongsToTransactionsUi(pageButton, currentRoot) && !pageButton.disabled) {
            event.preventDefault();

            const page = Number(pageButton.dataset.transactionsPage);
            const form = currentRoot.querySelector("[data-transactions-filter-form]");

            if (!form || !Number.isFinite(page) || page < 1) {
                return;
            }

            const params = new URLSearchParams(new FormData(form));
            params.set("PageNumber", String(page));

            await Finance.loadTab(
                "transactions",
                `${form.action}?${params.toString()}`,
                { historyMode: "replace" }
            );
            return;
        }

        const close = event.target.closest("[data-transaction-close]");
        if (close && belongsToTransactionsUi(close, currentRoot)) {
            event.preventDefault();
            closeModal(close.closest(".aft-modal"));
        }
    }

    function onKeyDown(event) {
        if (event.key !== "Escape") {
            return;
        }

        const currentRoot = root();
        const opened = document.querySelector(".aft-modal.is-open");
        if (opened) {
            closeModal(opened);
        }
    }

    document.addEventListener("submit", onSubmit);
    document.addEventListener("click", onClick);
    document.addEventListener("keydown", onKeyDown);

    window.AdminFinanceTransactions = {
        openDetails
    };
})(window, document);
