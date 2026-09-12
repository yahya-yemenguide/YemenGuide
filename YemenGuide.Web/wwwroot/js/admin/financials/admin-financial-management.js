(function (window, document) {
    "use strict";

    let activeManagementTab = "commissions";
    let pendingTrialDisableConfirmation = null;

    function api() {
        return window.AdminFinance;
    }

    function root() {
        return document.querySelector("[data-financial-management-root]");
    }

    function activateManagementTab(currentRoot, requestedTab, shouldFocus) {
        if (!currentRoot) {
            return;
        }

        const tabs = Array.from(currentRoot.querySelectorAll("[data-management-tab]"));
        const panels = Array.from(currentRoot.querySelectorAll("[data-management-panel]"));
        if (tabs.length === 0 || panels.length === 0) {
            return;
        }

        const selected = tabs.find(tab => tab.dataset.managementTab === requestedTab) || tabs[0];
        activeManagementTab = selected.dataset.managementTab;
        currentRoot.dataset.activeManagementTab = activeManagementTab;

        const settingsContainer = currentRoot.querySelector("[data-management-settings-container]");
        if (settingsContainer) {
            settingsContainer.hidden = activeManagementTab === "wallets";
        }

        tabs.forEach(tab => {
            const isActive = tab === selected;
            tab.classList.toggle("is-active", isActive);
            tab.setAttribute("aria-selected", isActive ? "true" : "false");
            tab.tabIndex = isActive ? 0 : -1;
        });

        panels.forEach(panel => {
            const isActive = panel.dataset.managementPanel === activeManagementTab;
            panel.hidden = !isActive;
            panel.setAttribute("aria-hidden", isActive ? "false" : "true");
        });

        if (shouldFocus) {
            selected.focus();
            currentRoot.querySelector(".afm-body")?.scrollTo({ top: 0, behavior: "smooth" });
        }
    }

    function initializeManagement(currentRoot) {
        syncTrialPolicyState(currentRoot);
        syncDebtPolicyState(currentRoot);
        syncCommissionRates(currentRoot);
        activateManagementTab(currentRoot, activeManagementTab, false);
    }

    function syncDebtPolicyState(scope) {
        const currentRoot = scope || root();
        if (!currentRoot) return;

        const enabled = currentRoot.querySelector("[data-negative-balance-policy]");
        const limitField = currentRoot.querySelector("[data-debt-limit-field]");
        const graceField = currentRoot.querySelector("[data-debt-grace-field]");
        const limit = limitField?.querySelector("input");
        const grace = graceField?.querySelector("input");
        if (!enabled) return;

        [limitField, graceField].forEach(field => {
            field?.classList.toggle("is-disabled", !enabled.checked);
        });

        if (limit) {
            limit.readOnly = !enabled.checked;
            limit.setAttribute("aria-disabled", enabled.checked ? "false" : "true");
        }
        if (grace) {
            grace.readOnly = !enabled.checked;
            grace.setAttribute("aria-disabled", enabled.checked ? "false" : "true");
        }
    }

    function syncCommissionRates(scope) {
        const currentRoot = scope || root();
        if (!currentRoot) return;

        const generalInput = currentRoot.querySelector("[data-global-commission-rate]");
        const generalRate = Number(generalInput?.value || 0);

        currentRoot.querySelectorAll("[data-category-commission-card]").forEach(card => {
            const input = card.querySelector("[data-category-commission-rate]");
            const mode = card.querySelector("[data-category-rate-mode]");
            const effective = card.querySelector("[data-category-effective-rate]");
            if (!input) return;

            const hasSpecialRate = input.value.trim() !== "";
            const appliedRate = hasSpecialRate ? Number(input.value) : generalRate;
            const generalText = Number.isFinite(generalRate)
                ? String(Math.round(generalRate * 100) / 100)
                : "0";
            card.classList.toggle("has-special-rate", hasSpecialRate);
            input.placeholder = generalText;
            if (mode) mode.textContent = hasSpecialRate ? "نسبة خاصة" : "النسبة العامة";
            if (effective) {
                const safeRate = Number.isFinite(appliedRate) ? appliedRate : 0;
                effective.textContent = `المطبق بعد الحفظ: ${safeRate.toLocaleString("ar-YE", { maximumFractionDigits: 2 })}%`;
            }
        });
    }

    function trialDisableModal() {
        return document.querySelector("[data-trial-disable-modal]") || null;
    }

    function resolveTrialDisable(confirmed) {
        const modal = trialDisableModal();
        if (modal) closeModal(modal);

        const resolve = pendingTrialDisableConfirmation;
        pendingTrialDisableConfirmation = null;
        if (resolve) resolve(confirmed === true);
    }

    function confirmTrialDisable() {
        const modal = trialDisableModal();
        if (!modal) {
            api()?.showToast(
                "تعذر فتح نافذة التأكيد. أعد فتح إدارة المالية ثم حاول مجددًا.",
                "danger"
            );
            return Promise.resolve(false);
        }

        if (pendingTrialDisableConfirmation) {
            return Promise.resolve(false);
        }

        const decision = new Promise(resolve => {
            pendingTrialDisableConfirmation = resolve;
        });
        openModal(modal);
        window.setTimeout(() => {
            modal.querySelector("[data-trial-disable-cancel]")?.focus();
        }, 50);
        return decision;
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

    function walletModal() {
        return document.querySelector("[data-platform-wallet-modal]") || null;
    }

    function walletForm() {
        return document.querySelector("[data-platform-wallet-form]") || null;
    }

    function syncTrialPolicyState(scope) {
        const currentRoot = scope || root();
        if (!currentRoot) {
            return;
        }

        const card = currentRoot.querySelector("[data-trial-policy-card]");
        const enabled = currentRoot.querySelector("[data-trial-enabled]");
        const days = currentRoot.querySelector("[data-trial-days]");
        const daysField = currentRoot.querySelector("[data-trial-days-field]");
        const status = currentRoot.querySelector("[data-trial-policy-status]");

        if (!card || !enabled) {
            return;
        }

        const isEnabled = enabled.checked;
        card.classList.toggle("is-enabled", isEnabled);
        card.classList.toggle("is-disabled", !isEnabled);

        if (days) {
            if (isEnabled && Number(days.value) <= 0) {
                days.value = "14";
            }

            days.readOnly = !isEnabled;
            days.setAttribute("aria-disabled", isEnabled ? "false" : "true");
        }

        if (daysField) {
            daysField.classList.toggle("is-readonly", !isEnabled);
        }

        if (status) {
            status.innerHTML = isEnabled
                ? '<i class="bi bi-check-circle-fill"></i><span>التجربة مفعّلة</span>'
                : '<i class="bi bi-pause-circle-fill"></i><span>التجربة متوقفة</span>';
        }
    }

    function setWalletFormValues(card) {
        const form = walletForm();
        const currentRoot = root();

        if (!form || !currentRoot) {
            return;
        }

        const idInput = form.querySelector("[data-platform-wallet-id-input]");
        const type = form.querySelector("[data-platform-wallet-type]");
        const number = form.querySelector("[data-platform-wallet-number]");
        const beneficiary = form.querySelector("[data-platform-wallet-beneficiary]");
        const instructions = form.querySelector("[data-platform-wallet-instructions]");
        const instructionsEn = form.querySelector("[data-platform-wallet-instructions-en]");
        const active = form.querySelector("[data-platform-wallet-active]");
        const primary = form.querySelector("[data-platform-wallet-primary]");
        const title = currentRoot.querySelector("[data-platform-wallet-modal-title]");

        if (!card) {
            form.reset();
            if (idInput) idInput.value = "";
            if (title) title.textContent = "إضافة حساب منصة";
            if (active) active.checked = true;
            if (primary) primary.checked = false;
            return;
        }

        if (idInput) idInput.value = card.dataset.platformWalletId || "";
        if (type) type.value = card.dataset.walletWalletId || "";
        if (number) number.value = card.dataset.walletAccountNumber || "";
        if (beneficiary) beneficiary.value = card.dataset.walletBeneficiary || "";
        if (instructions) instructions.value = card.dataset.walletInstructions || "";
        if (instructionsEn) instructionsEn.value = card.dataset.walletInstructionsEn || "";
        if (active) active.checked = card.dataset.walletActive === "true";
        if (primary) primary.checked = card.dataset.walletPrimary === "true";
        if (title) title.textContent = "تعديل حساب المنصة";
    }

    async function submitAndReload(form, submitter, successTitle) {
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
                "تم حفظ التغيير.";

            Finance.showToast(message, "success", successTitle || "تم الحفظ");

            const parentModal = form.closest(".afm-modal");
            if (parentModal) {
                closeModal(parentModal);
            }

            await Finance.reloadManagement();
            await Finance.refreshShell();
        } catch (error) {
            Finance.showToast(
                error && error.message ? error.message : "تعذر حفظ التغيير.",
                "danger"
            );
        } finally {
            Finance.setButtonBusy(button, false);
        }
    }

    async function submitSettings(form, submitter) {
        const Finance = api();
        if (!Finance || !form) {
            return;
        }

        if (!form.reportValidity()) {
            return;
        }

        const section = form.dataset.settingsSection || "all";
        if (section === "trial") {
            const wasEnabled = form.dataset.trialOriginalEnabled === "true";
            const remainsEnabled = form.querySelector("[data-trial-enabled]")?.checked === true;
            if (wasEnabled && !remainsEnabled) {
                const confirmed = await confirmTrialDisable();
                if (!confirmed) return;
            }
        }

        const button = submitter || form.querySelector('[type="submit"]');
        Finance.setButtonBusy(button, true, "حفظ...");

        try {
            const payload = await Finance.postForm(form, submitter);
            const message =
                Finance.pick(payload, "message", "Message") ||
                "تم حفظ الإعدادات المالية.";

            const successTitle = section === "commissions"
                ? "تم تحديث العمولات"
                : section === "trial"
                    ? "تم تحديث الفترة المجانية"
                    : section === "policies"
                        ? "تم تحديث سياسات الشحن"
                        : "تم تحديث الإعدادات";

            Finance.showToast(message, "success", successTitle);

            await Finance.reloadManagement();
            await Finance.refreshShell();
        } catch (error) {
            Finance.showToast(
                error && error.message ? error.message : "تعذر حفظ الإعدادات المالية.",
                "danger"
            );
        } finally {
            Finance.setButtonBusy(button, false);
        }
    }

    function belongsToManagementUi(target, currentRoot) {
        return !!(
            target &&
            currentRoot &&
            (currentRoot.contains(target) || target.closest(".afm-modal"))
        );
    }

    async function onSubmit(event) {
        const currentRoot = root();
        if (!currentRoot) {
            return;
        }

        const settingsForm = event.target.closest("[data-finance-settings-form]");
        if (settingsForm && belongsToManagementUi(settingsForm, currentRoot)) {
            event.preventDefault();
            await submitSettings(settingsForm, event.submitter);
            return;
        }

        const editWalletForm = event.target.closest("[data-platform-wallet-form]");
        if (editWalletForm && belongsToManagementUi(editWalletForm, currentRoot)) {
            event.preventDefault();
            await submitAndReload(
                editWalletForm,
                event.submitter,
                "تم حفظ حساب المنصة"
            );
            return;
        }

        const statusForm = event.target.closest("[data-platform-wallet-status-form]");
        if (statusForm && belongsToManagementUi(statusForm, currentRoot)) {
            event.preventDefault();
            await submitAndReload(
                statusForm,
                event.submitter,
                "تم تحديث حالة الحساب"
            );
            return;
        }

        const primaryForm = event.target.closest("[data-platform-wallet-primary-form]");
        if (primaryForm && belongsToManagementUi(primaryForm, currentRoot)) {
            event.preventDefault();
            await submitAndReload(
                primaryForm,
                event.submitter,
                "تم تعيين الحساب الرئيسي"
            );
        }
    }

    function onClick(event) {
        const currentRoot = root();
        if (!currentRoot) {
            return;
        }

        const managementTab = event.target.closest("[data-management-tab]");
        if (managementTab && currentRoot.contains(managementTab)) {
            event.preventDefault();
            activateManagementTab(currentRoot, managementTab.dataset.managementTab, true);
            return;
        }

        const trialConfirm = event.target.closest("[data-trial-disable-confirm]");
        if (trialConfirm && belongsToManagementUi(trialConfirm, currentRoot)) {
            event.preventDefault();
            resolveTrialDisable(true);
            return;
        }

        const trialCancel = event.target.closest("[data-trial-disable-cancel]");
        if (trialCancel && belongsToManagementUi(trialCancel, currentRoot)) {
            event.preventDefault();
            resolveTrialDisable(false);
            return;
        }

        const add = event.target.closest("[data-platform-wallet-new]");
        if (add && belongsToManagementUi(add, currentRoot)) {
            event.preventDefault();
            setWalletFormValues(null);
            openModal(walletModal());

            window.setTimeout(() => {
                walletForm()?.querySelector("[data-platform-wallet-type]")?.focus();
            }, 80);
            return;
        }

        const edit = event.target.closest("[data-platform-wallet-edit]");
        if (edit && belongsToManagementUi(edit, currentRoot)) {
            event.preventDefault();

            const card = edit.closest("[data-platform-wallet-id]");
            if (!card) {
                return;
            }

            setWalletFormValues(card);
            openModal(walletModal());

            window.setTimeout(() => {
                walletForm()?.querySelector("[data-platform-wallet-number]")?.focus();
            }, 80);
            return;
        }

        const close = event.target.closest("[data-platform-wallet-modal-close]");
        if (close && belongsToManagementUi(close, currentRoot)) {
            event.preventDefault();
            closeModal(close.closest(".afm-modal"));
        }
    }

    function onChange(event) {
        const currentRoot = root();
        if (!currentRoot || !belongsToManagementUi(event.target, currentRoot)) {
            return;
        }

        if (event.target.matches("[data-trial-enabled]")) {
            syncTrialPolicyState(currentRoot);
            return;
        }

        if (event.target.matches("[data-negative-balance-policy]")) {
            syncDebtPolicyState(currentRoot);
            return;
        }

        if (event.target.matches("[data-global-commission-rate], [data-category-commission-rate]")) {
            syncCommissionRates(currentRoot);
            return;
        }

        if (event.target.matches("[data-platform-wallet-primary]")) {
            const form = event.target.closest("form");
            const active = form?.querySelector("[data-platform-wallet-active]");

            if (event.target.checked && active) {
                active.checked = true;
            }
        }

        if (event.target.matches("[data-platform-wallet-active]")) {
            const form = event.target.closest("form");
            const primary = form?.querySelector("[data-platform-wallet-primary]");

            if (!event.target.checked && primary) {
                primary.checked = false;
            }
        }
    }

    function onKeyDown(event) {
        const currentRoot = root();
        const managementTab = event.target.closest("[data-management-tab]");
        const navigationKeys = ["ArrowRight", "ArrowLeft", "Home", "End"];

        if (currentRoot && managementTab && navigationKeys.includes(event.key)) {
            const tabs = Array.from(currentRoot.querySelectorAll("[data-management-tab]"));
            const currentIndex = tabs.indexOf(managementTab);
            if (currentIndex < 0 || tabs.length === 0) {
                return;
            }

            event.preventDefault();
            let nextIndex;
            if (event.key === "Home") {
                nextIndex = 0;
            } else if (event.key === "End") {
                nextIndex = tabs.length - 1;
            } else {
                const direction = event.key === "ArrowRight" ? 1 : -1;
                nextIndex = (currentIndex + direction + tabs.length) % tabs.length;
            }

            activateManagementTab(currentRoot, tabs[nextIndex].dataset.managementTab, true);
            return;
        }

        if (event.key !== "Escape") {
            return;
        }

        const trialModal = trialDisableModal();
        if (trialModal?.classList.contains("is-open")) {
            event.preventDefault();
            resolveTrialDisable(false);
            return;
        }

        const opened = document.querySelector(".afm-modal.is-open");
        if (opened) {
            closeModal(opened);
        }
    }

    document.addEventListener("adminfinance:managementloaded", function () {
        initializeManagement(root());
    });

    document.addEventListener("DOMContentLoaded", function () {
        initializeManagement(root());
    });

    document.addEventListener("submit", onSubmit);
    document.addEventListener("click", onClick);
    document.addEventListener("change", onChange);
    document.addEventListener("input", onChange);
    document.addEventListener("keydown", onKeyDown);
})(window, document);
