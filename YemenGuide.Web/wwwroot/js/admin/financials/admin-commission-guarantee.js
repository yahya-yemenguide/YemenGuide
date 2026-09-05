(function (window, document) {
    "use strict";

    const moduleState = {
        currentDetails: null,
        currentAccountId: null,
        detailsAbortController: null,
        bookingAbortController: null
    };

    function api() {
        return window.AdminFinance;
    }

    function root() {
        return document.querySelector("[data-commission-guarantee-root]");
    }

    function modal(selector) {
        return document.querySelector(selector);
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

    function text(selector, value, scope) {
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

    function accountTone(status) {
        const value = String(status || "").toLowerCase();

        if (value.includes("active")) return "success";
        if (value.includes("low")) return "warning";
        if (value.includes("debt")) return "danger";
        if (value.includes("suspend")) return "muted";
        if (value.includes("topup") || value.includes("top-up")) return "info";

        return "neutral";
    }

    function accountStatusLabel(status) {
        const value = String(status || "").toLowerCase();

        if (value === "active") return "نشط";
        if (value.includes("low")) return "رصيد منخفض";
        if (value.includes("debt")) return "عليه دين";
        if (value.includes("suspend")) return "موقوف";
        if (value.includes("topup") || value.includes("top-up")) return "يحتاج شحن";

        return status || "غير محدد";
    }

    function commissionStatusLabel(status) {
        const value = String(status || "").toLowerCase();

        if (value.includes("charged")) return "مخصومة";
        if (value.includes("held")) return "محجوزة";
        if (value.includes("released")) return "محررة";
        if (value.includes("debt")) return "دين";
        if (value.includes("calculated")) return "محتسبة";

        return status || "—";
    }

    function movementRows(items, currency) {
        const Finance = api();
        const rows = Finance.asArray(items);

        if (!rows.length) {
            return `
                <div class="acg-empty" style="min-height:130px;">
                    <span class="acg-empty__icon"><i class="bi bi-arrow-left-right"></i></span>
                    <h4>لا توجد حركات بعد</h4>
                    <p>سيظهر السجل المالي عند تنفيذ أول حركة.</p>
                </div>
            `;
        }

        return rows.map(item => {
            const id = Finance.pick(item, "commissionTransactionId", "CommissionTransactionId");
            const typeLabel = Finance.pick(item, "transactionTypeLabel", "TransactionTypeLabel") || "حركة مالية";
            const amount = Finance.pick(item, "amount", "Amount");
            const statusLabel = Finance.pick(item, "statusLabel", "StatusLabel") || Finance.pick(item, "status", "Status") || "—";
            const reference = Finance.pick(item, "referenceNumber", "ReferenceNumber");
            const createdAt = Finance.pick(item, "createdAt", "CreatedAt");

            return `
                <div class="acg-movement-row" data-movement-id="${Finance.escapeHtml(id)}">
                    <div>
                        <strong>${Finance.escapeHtml(typeLabel)}</strong>
                        <small>${Finance.escapeHtml(reference || `#${id}`)}</small>
                    </div>
                    <strong>${Finance.escapeHtml(Finance.formatMoney(amount, currency))}</strong>
                    <small>${Finance.escapeHtml(statusLabel)}</small>
                    <small>${Finance.escapeHtml(Finance.formatDate(createdAt, false))}</small>
                    <span><i class="bi bi-arrow-up-left"></i></span>
                </div>
            `;
        }).join("");
    }

    function commissionRows(items, currency) {
        const Finance = api();
        const rows = Finance.asArray(items);

        if (!rows.length) {
            return `
                <div class="acg-empty" style="min-height:130px;">
                    <span class="acg-empty__icon"><i class="bi bi-receipt"></i></span>
                    <h4>لا توجد عمولات حجوزات</h4>
                    <p>ستظهر أحدث العمولات المرتبطة بهذه المنشأة هنا.</p>
                </div>
            `;
        }

        return rows.map(item => {
            const bookingId = Finance.pick(item, "bookingId", "BookingId");
            const customer = Finance.pick(item, "customerName", "CustomerName") || "عميل";
            const amount = Finance.pick(item, "platformCommission", "PlatformCommission");
            const status = Finance.pick(item, "commissionStatus", "CommissionStatus");
            const updatedAt = Finance.pick(item, "updatedAt", "UpdatedAt");
            const debt = Finance.number(Finance.pick(item, "debtAmount", "DebtAmount"));

            return `
                <button type="button"
                        class="acg-commission-row"
                        data-acg-booking-id="${Finance.escapeHtml(bookingId)}"
                        style="width:100%;border:0;background:transparent;text-align:right;cursor:pointer;">
                    <div>
                        <strong>حجز #${Finance.escapeHtml(bookingId)}</strong>
                        <small>${Finance.escapeHtml(customer)}</small>
                    </div>
                    <strong>${Finance.escapeHtml(Finance.formatMoney(amount, currency))}</strong>
                    <small>${Finance.escapeHtml(commissionStatusLabel(status))}</small>
                    <small>${debt > 0 ? `دين: ${Finance.escapeHtml(Finance.formatMoney(debt, currency))}` : "بدون دين"}</small>
                    <small>${Finance.escapeHtml(Finance.formatDate(updatedAt, false))}</small>
                </button>
            `;
        }).join("");
    }

    function populateDetails(payload) {
        const Finance = api();
        const details = Finance.pick(payload, "data", "Data") || {};
        const account = Finance.pick(details, "account", "Account") || {};
        const recentCommissions = Finance.pick(details, "recentCommissions", "RecentCommissions");
        const currency = Finance.pick(details, "currency", "Currency") || Finance.getCurrency();

        moduleState.currentDetails = details;
        moduleState.currentAccountId = Finance.pick(
            account,
            "placeCommissionAccountId",
            "PlaceCommissionAccountId"
        );

        text("[data-acg-place-name]", Finance.pick(account, "placeName", "PlaceName"));
        text("[data-acg-owner-name]", Finance.pick(account, "ownerName", "OwnerName"));
        text("[data-acg-account-id-label]", `حساب #${moduleState.currentAccountId || "—"}`);
        text("[data-acg-available]", Finance.formatMoney(
            Finance.pick(account, "availableBalance", "AvailableBalance"),
            currency
        ));
        text("[data-acg-held]", Finance.formatMoney(
            Finance.pick(account, "heldBalance", "HeldBalance"),
            currency
        ));
        text("[data-acg-debt]", Finance.formatMoney(
            Finance.pick(account, "debtAmount", "DebtAmount"),
            currency
        ));

        const statusElement = document.querySelector("[data-acg-account-status]");
        const status = Finance.pick(account, "accountStatus", "AccountStatus");
        if (statusElement) {
            statusElement.className = `acg-status acg-status--${accountTone(status)}`;
            statusElement.innerHTML = `
                <span class="acg-status__dot"></span>
                ${Finance.escapeHtml(accountStatusLabel(status))}
            `;
        }

        text("[data-acg-total-topups]", Finance.formatMoney(
            Finance.pick(account, "totalTopUps", "TotalTopUps"),
            currency
        ));
        text("[data-acg-total-held]", Finance.formatMoney(
            Finance.pick(account, "totalCommissionHeld", "TotalCommissionHeld"),
            currency
        ));
        text("[data-acg-total-charged]", Finance.formatMoney(
            Finance.pick(account, "totalCommissionCharged", "TotalCommissionCharged"),
            currency
        ));
        text("[data-acg-total-released]", Finance.formatMoney(
            Finance.pick(account, "totalCommissionReleased", "TotalCommissionReleased"),
            currency
        ));
        text("[data-acg-total-debt-registered]", Finance.formatMoney(
            Finance.pick(account, "totalDebtRegistered", "TotalDebtRegistered"),
            currency
        ));
        text("[data-acg-total-debt-paid]", Finance.formatMoney(
            Finance.pick(account, "totalDebtPaid", "TotalDebtPaid"),
            currency
        ));

        text(
            "[data-acg-related-bookings]",
            `${Finance.number(Finance.pick(account, "relatedBookingsCount", "RelatedBookingsCount"))} حجز`
        );
        text(
            "[data-acg-transactions-count]",
            `${Finance.number(Finance.pick(account, "transactionsCount", "TransactionsCount"))} حركة`
        );

        const commissionsHost = document.querySelector("[data-acg-recent-commissions]");
        if (commissionsHost) {
            commissionsHost.hidden = false;
            commissionsHost.innerHTML = commissionRows(recentCommissions, currency);

            const existingBookingDetail = document.querySelector("[data-acg-booking-detail]");
            if (existingBookingDetail) {
                existingBookingDetail.remove();
            }
        }

        const movementHost = document.querySelector("[data-acg-recent-transactions]");
        if (movementHost) {
            movementHost.innerHTML = movementRows(
                Finance.pick(account, "recentTransactions", "RecentTransactions"),
                currency
            );
        }

        setHidden("[data-acg-details-loading]", true);
        setHidden("[data-acg-details-content]", false);
    }

    async function openDetails(accountId) {
        const Finance = api();
        const currentRoot = root();

        if (!Finance || !currentRoot || !accountId) {
            return;
        }

        if (moduleState.detailsAbortController) {
            moduleState.detailsAbortController.abort();
        }

        const controller = new AbortController();
        moduleState.detailsAbortController = controller;

        const detailsModal = modal("[data-acg-details-modal]");
        openModal(detailsModal);

        setHidden("[data-acg-details-loading]", false);
        setHidden("[data-acg-details-content]", true);

        const detailsUrl = currentRoot.dataset.detailsUrl;
        const url = Finance.buildUrl(detailsUrl, {
            placeCommissionAccountId: accountId
        });

        try {
            const payload = await Finance.fetchJson(url, {
                signal: controller.signal
            });

            populateDetails(payload);
        } catch (error) {
            if (error && error.name === "AbortError") {
                return;
            }

            closeModal(detailsModal);
            Finance.showToast(
                error && error.message ? error.message : "تعذر تحميل تفاصيل حساب الضمان.",
                "danger"
            );
        } finally {
            if (moduleState.detailsAbortController === controller) {
                moduleState.detailsAbortController = null;
            }
        }
    }

    function currentAccount() {
        const Finance = api();
        return Finance
            ? Finance.pick(moduleState.currentDetails, "account", "Account") || {}
            : {};
    }

    function fillPolicyForm() {
        const Finance = api();
        const form = document.querySelector("[data-acg-policy-form]");
        const account = currentAccount();

        if (!Finance || !form || !moduleState.currentAccountId) {
            return false;
        }

        const placeId = Finance.pick(account, "placeId", "PlaceId");
        const negativeOverride = Finance.pick(
            account,
            "allowNegativeBalanceOverride",
            "AllowNegativeBalanceOverride"
        );
        const debtOverride = Finance.pick(account, "debtLimitOverride", "DebtLimitOverride");
        const lowOverride = Finance.pick(
            account,
            "lowBalanceThresholdOverride",
            "LowBalanceThresholdOverride"
        );

        form.querySelector("[data-acg-policy-account-id]").value = moduleState.currentAccountId;
        form.querySelector("[data-acg-policy-place-id]").value = placeId || "";

        const useGlobalNegative = form.querySelector("[data-acg-use-global-negative]");
        const negativeValue = form.querySelector("[data-acg-negative-value]");
        const useGlobalDebt = form.querySelector("[data-acg-use-global-debt]");
        const debtValue = form.querySelector("[data-acg-debt-limit]");
        const useGlobalLow = form.querySelector("[data-acg-use-global-low]");
        const lowValue = form.querySelector("[data-acg-low-limit]");

        useGlobalNegative.checked = negativeOverride == null;
        negativeValue.disabled = useGlobalNegative.checked;
        negativeValue.value = negativeOverride == null ? "" : String(Finance.bool(negativeOverride));

        useGlobalDebt.checked = debtOverride == null;
        debtValue.disabled = useGlobalDebt.checked;
        debtValue.value = debtOverride == null ? "" : String(debtOverride);

        useGlobalLow.checked = lowOverride == null;
        lowValue.disabled = useGlobalLow.checked;
        lowValue.value = lowOverride == null ? "" : String(lowOverride);

        const reason = form.querySelector('textarea[name="Reason"]');
        if (reason) {
            reason.value = "";
        }

        return true;
    }

    function fillStatusForm() {
        const Finance = api();
        const form = document.querySelector("[data-acg-status-form]");
        const account = currentAccount();

        if (!Finance || !form || !moduleState.currentAccountId) {
            return false;
        }

        form.querySelector("[data-acg-status-account-id]").value = moduleState.currentAccountId;
        form.querySelector("[data-acg-status-place-id]").value =
            Finance.pick(account, "placeId", "PlaceId") || "";

        const currentStatus = String(
            Finance.pick(account, "accountStatus", "AccountStatus") || ""
        ).toLowerCase();

        const radios = Array.from(form.querySelectorAll('input[name="TargetStatus"]'));
        radios.forEach(radio => {
            const value = String(radio.value || "").toLowerCase();
            radio.checked =
                (currentStatus.includes("suspend") && value.includes("suspend")) ||
                (!currentStatus.includes("suspend") && value.includes("active"));
        });

        const reason = form.querySelector('textarea[name="Reason"]');
        if (reason) {
            reason.value = "";
        }

        return true;
    }

    function syncPolicySwitch(input) {
        const form = input.closest("form");
        if (!form) {
            return;
        }

        if (input.matches("[data-acg-use-global-negative]")) {
            const target = form.querySelector("[data-acg-negative-value]");
            if (target) {
                target.disabled = input.checked;
                if (input.checked) target.value = "";
            }
        }

        if (input.matches("[data-acg-use-global-debt]")) {
            const target = form.querySelector("[data-acg-debt-limit]");
            if (target) {
                target.disabled = input.checked;
                if (input.checked) target.value = "";
            }
        }

        if (input.matches("[data-acg-use-global-low]")) {
            const target = form.querySelector("[data-acg-low-limit]");
            if (target) {
                target.disabled = input.checked;
                if (input.checked) target.value = "";
            }
        }
    }

    function buildListUrl(form, mutate) {
        const params = new URLSearchParams(new FormData(form));

        [
            "AccountStatus",
            "HasDebt",
            "IsLowBalance",
            "IsBookingSuspended"
        ].forEach(key => params.delete(key));

        if (typeof mutate === "function") {
            mutate(params);
        }

        for (const [key, value] of Array.from(params.entries())) {
            if (value == null || String(value).trim() === "") {
                params.delete(key);
            }
        }

        return `${form.action}${params.toString() ? `?${params.toString()}` : ""}`;
    }

    async function submitMutationForm(form, submitter, successMessage) {
        const Finance = api();
        if (!Finance || !form) {
            return;
        }

        if (!form.reportValidity()) {
            return;
        }

        const button = submitter || form.querySelector('[type="submit"]');
        Finance.setButtonBusy(button, true, "حفظ...");

        try {
            const payload = await Finance.postForm(form, submitter);
            const message =
                Finance.pick(payload, "message", "Message") ||
                successMessage ||
                "تم حفظ التغيير.";

            Finance.showToast(message, "success");

            closeModal(form.closest(".acg-modal"));

            const accountId = moduleState.currentAccountId;
            const reloaded = await Finance.reloadCurrentTab();

            await Finance.refreshShell();

            if (reloaded && accountId) {
                await openDetails(accountId);
            }
        } catch (error) {
            Finance.showToast(
                error && error.message ? error.message : "تعذر حفظ التغيير.",
                "danger"
            );
        } finally {
            Finance.setButtonBusy(button, false);
        }
    }

    async function showBookingDetails(bookingId) {
        const Finance = api();
        const currentRoot = root();
        const host = currentRoot?.querySelector("[data-acg-recent-commissions]");

        if (!Finance || !currentRoot || !host || !bookingId) {
            return;
        }

        if (moduleState.bookingAbortController) {
            moduleState.bookingAbortController.abort();
        }

        const controller = new AbortController();
        moduleState.bookingAbortController = controller;

        const urlBase = currentRoot.dataset.bookingUrl;
        const cleanBase = urlBase.endsWith("/") ? urlBase.slice(0, -1) : urlBase;
        const url = `${cleanBase}/${encodeURIComponent(bookingId)}`;

        try {
            host.innerHTML = `
                <div class="acg-modal-loading" style="min-height:120px;">
                    <span class="acg-spinner"></span>
                    <strong>جارٍ تحميل عمولة الحجز...</strong>
                </div>
            `;

            const payload = await Finance.fetchJson(url, {
                signal: controller.signal
            });
            const details = Finance.pick(payload, "data", "Data") || {};
            const currency = Finance.pick(
                moduleState.currentDetails,
                "currency",
                "Currency"
            ) || Finance.getCurrency();

            const timeline = Finance.asArray(
                Finance.pick(details, "timeline", "Timeline")
            );

            const timelineHtml = timeline.length
                ? timeline.map(item => `
                    <div class="acg-movement-row">
                        <div>
                            <strong>${Finance.escapeHtml(Finance.pick(item, "label", "Label") || "مرحلة")}</strong>
                            <small>${Finance.escapeHtml(Finance.pick(item, "description", "Description") || "")}</small>
                        </div>
                        <strong>${Finance.escapeHtml(Finance.formatMoney(Finance.pick(item, "amount", "Amount"), currency))}</strong>
                        <small>${Finance.bool(Finance.pick(item, "isCompleted", "IsCompleted")) ? "مكتملة" : "قيد المتابعة"}</small>
                        <small>${Finance.escapeHtml(Finance.formatDate(Finance.pick(item, "occurredAt", "OccurredAt"), false))}</small>
                        <span><i class="bi bi-check2-circle"></i></span>
                    </div>
                `).join("")
                : `<div class="acg-empty" style="min-height:100px;"><p>لا يوجد خط زمني متاح.</p></div>`;

            host.innerHTML = `
                <div class="acg-detail-section" data-acg-booking-detail>
                    <div class="acg-detail-section__head">
                        <div>
                            <span>تفاصيل عمولة الحجز</span>
                            <h4>حجز #${Finance.escapeHtml(Finance.pick(details, "bookingId", "BookingId") || bookingId)}</h4>
                        </div>
                        <button type="button" class="acg-btn acg-btn--ghost" data-acg-booking-back>
                            <i class="bi bi-arrow-right"></i>
                            رجوع
                        </button>
                    </div>

                    <div class="acg-commission-metrics">
                        <div>
                            <small>إجمالي الحجز</small>
                            <strong>${Finance.escapeHtml(Finance.formatMoney(Finance.pick(details, "bookingTotalPrice", "BookingTotalPrice"), currency))}</strong>
                        </div>
                        <div>
                            <small>عمولة المنصة</small>
                            <strong>${Finance.escapeHtml(Finance.formatMoney(Finance.pick(details, "platformCommission", "PlatformCommission"), currency))}</strong>
                        </div>
                        <div>
                            <small>المحجوز</small>
                            <strong>${Finance.escapeHtml(Finance.formatMoney(Finance.pick(details, "heldAmount", "HeldAmount"), currency))}</strong>
                        </div>
                        <div>
                            <small>المخصوم</small>
                            <strong>${Finance.escapeHtml(Finance.formatMoney(Finance.pick(details, "chargedAmount", "ChargedAmount"), currency))}</strong>
                        </div>
                        <div>
                            <small>المحرر</small>
                            <strong>${Finance.escapeHtml(Finance.formatMoney(Finance.pick(details, "releasedAmount", "ReleasedAmount"), currency))}</strong>
                        </div>
                        <div>
                            <small>الدين</small>
                            <strong>${Finance.escapeHtml(Finance.formatMoney(Finance.pick(details, "debtAmount", "DebtAmount"), currency))}</strong>
                        </div>
                    </div>

                    <div class="acg-movement-list">${timelineHtml}</div>
                </div>
            `;
        } catch (error) {
            if (error && error.name === "AbortError") {
                return;
            }

            Finance.showToast(
                error && error.message ? error.message : "تعذر تحميل عمولة الحجز.",
                "danger"
            );

            populateDetails({ data: moduleState.currentDetails });
        } finally {
            if (moduleState.bookingAbortController === controller) {
                moduleState.bookingAbortController = null;
            }
        }
    }

    function restoreCommissionList() {
        if (!moduleState.currentDetails) {
            return;
        }

        populateDetails({ data: moduleState.currentDetails });
    }

    function belongsToCommissionUi(target, currentRoot) {
        return !!(
            target &&
            currentRoot &&
            (currentRoot.contains(target) || target.closest(".acg-modal"))
        );
    }

    async function onSubmit(event) {
        const currentRoot = root();
        if (!currentRoot) {
            return;
        }

        const filterForm = event.target.closest("[data-acg-filter-form]");
        if (filterForm && belongsToCommissionUi(filterForm, currentRoot)) {
            event.preventDefault();
            const url = buildListUrl(filterForm, params => {
                params.set("PageNumber", "1");
            });

            await api().loadTab("commission-guarantee", url, {
                historyMode: "replace"
            });
            return;
        }

        const policyForm = event.target.closest("[data-acg-policy-form]");
        if (policyForm && belongsToCommissionUi(policyForm, currentRoot)) {
            event.preventDefault();
            await submitMutationForm(
                policyForm,
                event.submitter,
                "تم تحديث سياسة حساب الضمان."
            );
            return;
        }

        const statusForm = event.target.closest("[data-acg-status-form]");
        if (statusForm && belongsToCommissionUi(statusForm, currentRoot)) {
            event.preventDefault();
            await submitMutationForm(
                statusForm,
                event.submitter,
                "تم تحديث حالة حساب الضمان."
            );
        }
    }

    async function onClick(event) {
        const currentRoot = root();
        const Finance = api();

        if (!currentRoot || !Finance) {
            return;
        }

        const detailsButton = event.target.closest("[data-acg-details]");
        if (detailsButton && belongsToCommissionUi(detailsButton, currentRoot)) {
            event.preventDefault();
            openDetails(detailsButton.dataset.accountId);
            return;
        }

        const close = event.target.closest("[data-acg-close]");
        if (close && belongsToCommissionUi(close, currentRoot)) {
            event.preventDefault();
            closeModal(close.closest(".acg-modal"));
            return;
        }

        const quickFilter = event.target.closest("[data-acg-quick-filter]");
        if (quickFilter && belongsToCommissionUi(quickFilter, currentRoot)) {
            event.preventDefault();

            const form = currentRoot.querySelector("[data-acg-filter-form]");
            if (!form) {
                return;
            }

            const type = quickFilter.dataset.acgQuickFilter;
            const value = quickFilter.dataset.value;

            const url = buildListUrl(form, params => {
                params.set("PageNumber", "1");

                if (type === "status" && value) {
                    params.set("AccountStatus", value);
                } else if (type === "low") {
                    params.set("IsLowBalance", "true");
                } else if (type === "debt") {
                    params.set("HasDebt", "true");
                } else if (type === "suspended") {
                    params.set("IsBookingSuspended", "true");
                }
            });

            await Finance.loadTab("commission-guarantee", url, {
                historyMode: "replace"
            });
            return;
        }

        const pageButton = event.target.closest("[data-acg-page]");
        if (pageButton && belongsToCommissionUi(pageButton, currentRoot) && !pageButton.disabled) {
            event.preventDefault();

            const page = Number(pageButton.dataset.acgPage);
            const form = currentRoot.querySelector("[data-acg-filter-form]");
            if (!form || !Number.isFinite(page) || page < 1) {
                return;
            }

            const params = new URLSearchParams(new FormData(form));
            params.set("PageNumber", String(page));

            await Finance.loadTab(
                "commission-guarantee",
                `${form.action}?${params.toString()}`,
                { historyMode: "replace" }
            );
            return;
        }

        const openPolicy = event.target.closest("[data-acg-open-policy]");
        if (openPolicy && belongsToCommissionUi(openPolicy, currentRoot)) {
            event.preventDefault();

            if (fillPolicyForm()) {
                openModal(modal("[data-acg-policy-modal]"));
            }
            return;
        }

        const policyClose = event.target.closest("[data-acg-policy-close]");
        if (policyClose && belongsToCommissionUi(policyClose, currentRoot)) {
            event.preventDefault();
            closeModal(policyClose.closest(".acg-modal"));
            return;
        }

        const openStatus = event.target.closest("[data-acg-open-status]");
        if (openStatus && belongsToCommissionUi(openStatus, currentRoot)) {
            event.preventDefault();

            if (fillStatusForm()) {
                openModal(modal("[data-acg-status-modal]"));
            }
            return;
        }

        const statusClose = event.target.closest("[data-acg-status-close]");
        if (statusClose && belongsToCommissionUi(statusClose, currentRoot)) {
            event.preventDefault();
            closeModal(statusClose.closest(".acg-modal"));
            return;
        }

        const bookingButton = event.target.closest("[data-acg-booking-id]");
        if (bookingButton && belongsToCommissionUi(bookingButton, currentRoot)) {
            event.preventDefault();
            showBookingDetails(bookingButton.dataset.acgBookingId);
            return;
        }

        const bookingBack = event.target.closest("[data-acg-booking-back]");
        if (bookingBack && belongsToCommissionUi(bookingBack, currentRoot)) {
            event.preventDefault();
            restoreCommissionList();
        }
    }

    function onChange(event) {
        const currentRoot = root();
        if (!currentRoot || !belongsToCommissionUi(event.target, currentRoot)) {
            return;
        }

        if (event.target.matches(
            "[data-acg-use-global-negative], [data-acg-use-global-debt], [data-acg-use-global-low]"
        )) {
            syncPolicySwitch(event.target);
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

        const opened = Array.from(document.querySelectorAll(".acg-modal.is-open"));
        if (opened.length) {
            closeModal(opened[opened.length - 1]);
        }
    }

    document.addEventListener("submit", onSubmit);
    document.addEventListener("click", onClick);
    document.addEventListener("change", onChange);
    document.addEventListener("keydown", onKeyDown);

    window.AdminFinanceCommissionGuarantee = {
        openDetails
    };
})(window, document);
