(() => {
    'use strict';

    window.OwnerFinancialTabs ??= {};

    const TAB_KEY = 'wallets';
    const MODULE_KEY = '__ownerFinancialReceivingWalletsModule';
    const ROOT_SELECTOR = '[data-owner-financial-partial="wallets"]';

    const SELECTORS = Object.freeze({
        walletDialog: '[data-ofw-wallet-dialog]',
        walletForm: '[data-ofw-wallet-form]',
        dialogEyebrow: '[data-ofw-dialog-eyebrow]',
        dialogTitle: '[data-ofw-dialog-title]',
        ownerWalletId: '[data-ofw-owner-wallet-id]',
        walletId: '[data-ofw-wallet-id]',
        accountNumber: '[data-ofw-account-number]',
        accountCounter: '[data-ofw-account-counter]',
        beneficiaryName: '[data-ofw-beneficiary-name]',
        isActive: '[data-ofw-is-active]',
        saveButton: '[data-ofw-save-button]',
        saveButtonText: '[data-ofw-save-button-text]',
        formAlert: '[data-ofw-form-alert]',
        statusDialog: '[data-ofw-status-dialog]',
        statusIcon: '[data-ofw-status-icon]',
        statusTitle: '[data-ofw-status-title]',
        statusMessage: '[data-ofw-status-message]',
        statusOwnerWalletId: '[data-ofw-status-owner-wallet-id]',
        statusNextActive: '[data-ofw-status-next-active]',
        confirmStatus: '[data-ofw-confirm-status]',
        walletChoice: '[data-ofw-wallet-choice]'
    });

    const state = {
        root: null,
        api: null,
        events: null,
        observer: null,
        requestController: null,
        isBusy: false,
        isSaving: false,
        isChangingStatus: false,
        lastFocusedElement: null
    };

    const query = (selector, root = state.root) =>
        root?.querySelector?.(selector) ?? null;

    const queryAll = (selector, root = state.root) =>
        Array.from(root?.querySelectorAll?.(selector) ?? []);

    function findRoot(container = document) {
        if (
            container instanceof Element &&
            container.matches(ROOT_SELECTOR)
        ) {
            return container;
        }

        return (
            container?.querySelector?.(ROOT_SELECTOR) ??
            document.querySelector(ROOT_SELECTOR)
        );
    }

    function init(container, api = {}) {
        const root = findRoot(container || document);

        if (!root) {
            destroyCurrentRoot();
            return;
        }

        destroyCurrentRoot();

        state.root = root;
        state.api = api || {};
        state.events = new AbortController();
        state.isBusy = false;
        state.isSaving = false;
        state.isChangingStatus = false;

        bindEvents(state.events.signal);
        updateAccountCounter();
        resetBusyState();
    }

    function destroyCurrentRoot() {
        state.events?.abort();
        state.requestController?.abort();

        state.root = null;
        state.events = null;
        state.requestController = null;
        state.isBusy = false;
        state.isSaving = false;
        state.isChangingStatus = false;
        state.lastFocusedElement = null;
    }

    function destroy() {
        destroyCurrentRoot();
        state.api = null;
    }

    function bindEvents(signal) {
        state.root.addEventListener('click', onRootClick, { signal });
        state.root.addEventListener('input', onRootInput, { signal });
        state.root.addEventListener('change', onRootChange, { signal });
        state.root.addEventListener('keydown', onRootKeyDown, { signal });

        query(SELECTORS.walletForm)?.addEventListener(
            'submit',
            onWalletFormSubmit,
            { signal }
        );

        const walletDialog = query(SELECTORS.walletDialog);
        const statusDialog = query(SELECTORS.statusDialog);

        walletDialog?.addEventListener(
            'click',
            onWalletDialogBackdropClick,
            { signal }
        );

        walletDialog?.addEventListener(
            'cancel',
            onWalletDialogCancel,
            { signal }
        );

        walletDialog?.addEventListener(
            'close',
            restoreDialogFocus,
            { signal }
        );

        statusDialog?.addEventListener(
            'click',
            onStatusDialogBackdropClick,
            { signal }
        );

        statusDialog?.addEventListener(
            'cancel',
            onStatusDialogCancel,
            { signal }
        );

        statusDialog?.addEventListener(
            'close',
            restoreDialogFocus,
            { signal }
        );
    }

    function onRootClick(event) {
        const target =
            event.target instanceof Element
                ? event.target
                : null;

        if (!target) {
            return;
        }

        const addButton = target.closest('[data-ofw-open-add]');

        if (addButton) {
            event.preventDefault();

            if (!addButton.disabled && !state.isBusy) {
                openAddDialog(addButton);
            }

            return;
        }

        const editButton = target.closest('[data-ofw-open-edit]');

        if (editButton) {
            event.preventDefault();

            if (!editButton.disabled && !state.isBusy) {
                openEditDialog(editButton);
            }

            return;
        }

        const walletChoice = target.closest(SELECTORS.walletChoice);

        if (walletChoice) {
            event.preventDefault();

            if (!walletChoice.disabled && !state.isBusy && !state.isSaving) {
                chooseWalletProvider(walletChoice);
            }

            return;
        }

        const closeButton = target.closest('[data-ofw-close-dialog]');

        if (closeButton) {
            event.preventDefault();
            closeWalletDialog();
            return;
        }

        const copyButton = target.closest('[data-ofw-copy]');

        if (copyButton) {
            event.preventDefault();
            copyAccountNumber(copyButton);
            return;
        }

        const statusButton = target.closest('[data-ofw-request-status]');

        if (statusButton) {
            event.preventDefault();

            if (!statusButton.disabled && !state.isBusy) {
                openStatusDialog(statusButton);
            }

            return;
        }

        if (target.closest('[data-ofw-cancel-status]')) {
            event.preventDefault();
            closeStatusDialog();
            return;
        }

        if (target.closest('[data-ofw-confirm-status]')) {
            event.preventDefault();
            confirmStatusChange();
        }
    }

    function onRootInput(event) {
        const target =
            event.target instanceof HTMLInputElement
                ? event.target
                : null;

        if (!target) {
            return;
        }

        if (target.matches(SELECTORS.accountNumber)) {
            target.value = normalizeAccountNumber(target.value);
            updateAccountCounter();
            clearFieldError('AccountNumber');
            clearFormAlert();
            return;
        }

        if (target.matches(SELECTORS.beneficiaryName)) {
            target.value = String(target.value || '')
                .replace(/[\u0000-\u001F\u007F]/g, '')
                .slice(0, 150);

            clearFieldError('BeneficiaryName');
            clearFormAlert();
        }
    }

    function onRootChange(event) {
        const target =
            event.target instanceof Element
                ? event.target
                : null;

        if (!target) {
            return;
        }

        if (target.matches(SELECTORS.walletId)) {
            clearFieldError('WalletId');
            clearFormAlert();

            syncWalletChoiceState(
                cleanInteger(target.value),
                Boolean(cleanInteger(query(SELECTORS.ownerWalletId)?.value))
            );
        }
    }

    function onRootKeyDown(event) {
        if (event.key !== 'Escape') {
            return;
        }

        const walletDialog = query(SELECTORS.walletDialog);
        const statusDialog = query(SELECTORS.statusDialog);

        if (statusDialog?.open) {
            event.preventDefault();
            closeStatusDialog();
            return;
        }

        if (walletDialog?.open) {
            event.preventDefault();
            closeWalletDialog();
        }
    }

    function onWalletDialogBackdropClick(event) {
        const dialog = query(SELECTORS.walletDialog);

        if (dialog && event.target === dialog && !state.isSaving) {
            closeWalletDialog();
        }
    }

    function onWalletDialogCancel(event) {
        event.preventDefault();

        if (!state.isSaving) {
            closeWalletDialog();
        }
    }

    function onStatusDialogBackdropClick(event) {
        const dialog = query(SELECTORS.statusDialog);

        if (dialog && event.target === dialog && !state.isChangingStatus) {
            closeStatusDialog();
        }
    }

    function onStatusDialogCancel(event) {
        event.preventDefault();

        if (!state.isChangingStatus) {
            closeStatusDialog();
        }
    }

    function openAddDialog(button) {
        const walletId = cleanInteger(button.dataset.walletId);
        const walletName = String(button.dataset.walletName || '').trim();
        const form = query(SELECTORS.walletForm);
        const walletSelect = query(SELECTORS.walletId);

        if (!form || !walletSelect) {
            toast('تعذر فتح نموذج إضافة الحساب.', 'error');
            return;
        }

        state.lastFocusedElement = button;

        form.reset();
        clearAllErrors();
        setInputValue(SELECTORS.ownerWalletId, '');
        setInputValue(SELECTORS.accountNumber, '');
        setInputValue(SELECTORS.beneficiaryName, '');
        setCheckboxValue(SELECTORS.isActive, true);

        prepareWalletOptionsForAdd();
        walletSelect.disabled = false;

        if (walletId > 0) {
            walletSelect.value = String(walletId);
        }
        else {
            walletSelect.value = '';
        }

        syncWalletChoiceState(walletId, false);

        setText(SELECTORS.dialogEyebrow, 'إضافة حساب جديد');

        setText(
            SELECTORS.dialogTitle,
            walletName
                ? `ربط حساب ${walletName}`
                : 'ربط حساب استقبال'
        );

        setText(SELECTORS.saveButtonText, 'حفظ الحساب');

        updateAccountCounter();
        showDialog(query(SELECTORS.walletDialog));

        window.requestAnimationFrame(() => {
            if (walletId > 0) {
                query(SELECTORS.accountNumber)?.focus();
            }
            else {
                walletSelect.focus();
            }
        });
    }

    function openEditDialog(button) {
        const ownerWalletId = cleanInteger(button.dataset.ownerWalletId);
        const walletId = cleanInteger(button.dataset.walletId);
        const walletName = String(button.dataset.walletName || '').trim();

        const accountNumber = normalizeAccountNumber(
            button.dataset.accountNumber || ''
        );

        const beneficiaryName = String(
            button.dataset.beneficiaryName || ''
        ).trim();

        const isActive = parseBoolean(button.dataset.isActive);
        const walletSelect = query(SELECTORS.walletId);

        if (ownerWalletId <= 0 || walletId <= 0 || !walletSelect) {
            toast('بيانات حساب الاستقبال غير مكتملة.', 'error');
            return;
        }

        state.lastFocusedElement = button;

        query(SELECTORS.walletForm)?.reset();
        clearAllErrors();
        prepareWalletOptionsForEdit(walletId);

        setInputValue(SELECTORS.ownerWalletId, String(ownerWalletId));

        walletSelect.value = String(walletId);
        walletSelect.disabled = true;

        syncWalletChoiceState(walletId, true);

        setInputValue(SELECTORS.accountNumber, accountNumber);
        setInputValue(SELECTORS.beneficiaryName, beneficiaryName);
        setCheckboxValue(SELECTORS.isActive, isActive);

        setText(SELECTORS.dialogEyebrow, 'تعديل حساب موجود');

        setText(
            SELECTORS.dialogTitle,
            walletName
                ? `تعديل حساب ${walletName}`
                : 'تعديل حساب الاستقبال'
        );

        setText(SELECTORS.saveButtonText, 'حفظ التعديلات');

        updateAccountCounter();
        showDialog(query(SELECTORS.walletDialog));

        window.requestAnimationFrame(() => {
            query(SELECTORS.accountNumber)?.focus();
        });
    }

    function chooseWalletProvider(button) {
        const walletId = cleanInteger(button.dataset.walletId);
        const walletName = String(button.dataset.walletName || '').trim();
        const walletSelect = query(SELECTORS.walletId);

        if (walletId <= 0 || !walletSelect || walletSelect.disabled) {
            return;
        }

        walletSelect.value = String(walletId);

        syncWalletChoiceState(walletId, false);
        clearFieldError('WalletId');
        clearFormAlert();

        setText(
            SELECTORS.dialogTitle,
            walletName
                ? `ربط حساب ${walletName}`
                : 'ربط حساب استقبال'
        );

        window.requestAnimationFrame(() => {
            query(SELECTORS.accountNumber)?.focus({
                preventScroll: true
            });
        });
    }

    function syncWalletChoiceState(selectedWalletId, isEditMode) {
        const selectedId = cleanInteger(selectedWalletId);

        queryAll(SELECTORS.walletChoice).forEach(button => {
            const walletId = cleanInteger(button.dataset.walletId);
            const linkedOwnerWalletId = cleanInteger(
                button.dataset.ownerWalletId
            );

            const isSelected =
                selectedId > 0 &&
                walletId === selectedId;

            button.classList.toggle(
                'is-selected',
                isSelected
            );

            button.setAttribute(
                'aria-pressed',
                isSelected
                    ? 'true'
                    : 'false'
            );

            if (isEditMode) {
                button.disabled = !isSelected;
                return;
            }

            button.disabled =
                linkedOwnerWalletId > 0;
        });
    }

    function prepareWalletOptionsForAdd() {
        const walletSelect = query(SELECTORS.walletId);

        if (!walletSelect) {
            return;
        }

        Array.from(walletSelect.options).forEach(option => {
            if (!option.value) {
                option.disabled = false;
                return;
            }

            option.disabled = Boolean(
                cleanInteger(option.dataset.ownerWalletId)
            );
        });
    }

    function prepareWalletOptionsForEdit(selectedWalletId) {
        const walletSelect = query(SELECTORS.walletId);

        if (!walletSelect) {
            return;
        }

        Array.from(walletSelect.options).forEach(option => {
            if (!option.value) {
                option.disabled = true;
                return;
            }

            option.disabled =
                cleanInteger(option.value) !== selectedWalletId;
        });
    }

    function closeWalletDialog() {
        if (state.isSaving) {
            return;
        }

        closeDialog(query(SELECTORS.walletDialog));
    }

    async function onWalletFormSubmit(event) {
        event.preventDefault();

        if (state.isSaving || state.isBusy) {
            return;
        }

        clearAllErrors();

        const payload = buildWalletPayload();

        if (!validateWalletPayload(payload)) {
            return;
        }

        const saveUrl = state.root?.dataset.saveUrl;

        if (!saveUrl) {
            showFormAlert('رابط حفظ حساب الاستقبال غير مضبوط.');
            return;
        }

        setSaving(true);

        try {
            const result = await postJson(saveUrl, payload);

            if (!result?.success) {
                applyServerErrors(result?.errors);

                throw new Error(
                    result?.message ||
                    'تعذر حفظ حساب الاستقبال.'
                );
            }

            closeDialog(query(SELECTORS.walletDialog));

            toast(
                result.message || 'تم حفظ الحساب بنجاح.',
                'success'
            );

            await refresh();
        }
        catch (error) {
            if (error?.name === 'AbortError') {
                return;
            }

            console.error(
                'Owner Financial Receiving Wallets:',
                error
            );

            const message =
                error?.message ||
                'تعذر حفظ حساب الاستقبال حاليًا.';

            showFormAlert(message);
            toast(message, 'error');
        }
        finally {
            setSaving(false);
        }
    }

    function buildWalletPayload() {
        const ownerWalletId = cleanInteger(
            query(SELECTORS.ownerWalletId)?.value
        );

        return {
            OwnerWalletId:
                ownerWalletId > 0
                    ? ownerWalletId
                    : null,

            WalletId:
                cleanInteger(
                    query(SELECTORS.walletId)?.value
                ),

            AccountNumber:
                normalizeAccountNumber(
                    query(SELECTORS.accountNumber)?.value || ''
                ),

            BeneficiaryName:
                String(
                    query(SELECTORS.beneficiaryName)?.value || ''
                ).trim(),

            IsActive:
                Boolean(
                    query(SELECTORS.isActive)?.checked
                )
        };
    }

    function validateWalletPayload(payload) {
        let isValid = true;
        let firstInvalidElement = null;

        if (payload.WalletId <= 0) {
            setFieldError(
                'WalletId',
                'اختر شبكة الدفع.'
            );

            firstInvalidElement ??=
                query(SELECTORS.walletId);

            isValid = false;
        }

        if (!/^[0-9]{6}$/.test(payload.AccountNumber)) {
            setFieldError(
                'AccountNumber',
                'رقم الحساب التجاري يجب أن يتكون من 6 أرقام إنجليزية.'
            );

            firstInvalidElement ??=
                query(SELECTORS.accountNumber);

            isValid = false;
        }

        if (
            payload.BeneficiaryName.length < 2 ||
            payload.BeneficiaryName.length > 150
        ) {
            setFieldError(
                'BeneficiaryName',
                'اسم المستفيد يجب أن يكون بين حرفين و150 حرفًا.'
            );

            firstInvalidElement ??=
                query(SELECTORS.beneficiaryName);

            isValid = false;
        }

        if (!isValid) {
            showFormAlert(
                'تحقق من الحقول المطلوبة ثم أعد المحاولة.'
            );

            firstInvalidElement?.focus();
        }

        return isValid;
    }

    function setSaving(value) {
        state.isSaving = Boolean(value);

        const form = query(SELECTORS.walletForm);
        const saveButton = query(SELECTORS.saveButton);

        form?.setAttribute(
            'aria-busy',
            state.isSaving
                ? 'true'
                : 'false'
        );

        if (saveButton) {
            saveButton.disabled = state.isSaving;
        }

        const ownerWalletId = cleanInteger(
            query(SELECTORS.ownerWalletId)?.value
        );

        setText(
            SELECTORS.saveButtonText,
            state.isSaving
                ? 'جارٍ الحفظ...'
                : ownerWalletId > 0
                    ? 'حفظ التعديلات'
                    : 'حفظ الحساب'
        );
    }

    function openStatusDialog(button) {
        const ownerWalletId =
            cleanInteger(button.dataset.ownerWalletId);

        const walletName =
            String(
                button.dataset.walletName || 'الحساب'
            ).trim();

        const nextActive =
            parseBoolean(button.dataset.nextActive);

        if (ownerWalletId <= 0) {
            toast(
                'رقم حساب الاستقبال غير صحيح.',
                'error'
            );

            return;
        }

        state.lastFocusedElement = button;

        setInputValue(
            SELECTORS.statusOwnerWalletId,
            String(ownerWalletId)
        );

        setInputValue(
            SELECTORS.statusNextActive,
            nextActive
                ? 'true'
                : 'false'
        );

        const icon = query(SELECTORS.statusIcon);
        const confirmButton = query(SELECTORS.confirmStatus);

        icon?.classList.toggle('is-start', nextActive);

        if (icon) {
            icon.innerHTML = nextActive
                ? '<i class="bi bi-play-circle-fill"></i>'
                : '<i class="bi bi-pause-circle-fill"></i>';
        }

        setText(
            SELECTORS.statusTitle,
            nextActive
                ? 'تفعيل حساب الاستقبال'
                : 'إيقاف حساب الاستقبال'
        );

        setText(
            SELECTORS.statusMessage,
            nextActive
                ? `سيظهر حساب ${walletName} للعملاء بعد التفعيل، طالما أن شبكة الدفع مفعلة من الإدارة.`
                : `سيتوقف حساب ${walletName} عن الظهور للعملاء، وسيبقى محفوظًا لتتمكن من تفعيله لاحقًا.`
        );

        if (confirmButton) {
            confirmButton.textContent =
                nextActive
                    ? 'تأكيد التفعيل'
                    : 'تأكيد الإيقاف';

            confirmButton.dataset.defaultText =
                confirmButton.textContent;
        }

        showDialog(query(SELECTORS.statusDialog));

        window.requestAnimationFrame(() => {
            confirmButton?.focus();
        });
    }

    function closeStatusDialog() {
        if (state.isChangingStatus) {
            return;
        }

        closeDialog(query(SELECTORS.statusDialog));
    }

    async function confirmStatusChange() {
        if (state.isChangingStatus || state.isBusy) {
            return;
        }

        const ownerWalletId = cleanInteger(
            query(SELECTORS.statusOwnerWalletId)?.value
        );

        const isActive = parseBoolean(
            query(SELECTORS.statusNextActive)?.value
        );

        const statusUrl = state.root?.dataset.statusUrl;

        if (ownerWalletId <= 0) {
            toast(
                'رقم حساب الاستقبال غير صحيح.',
                'error'
            );

            return;
        }

        if (!statusUrl) {
            toast(
                'رابط تغيير حالة الحساب غير مضبوط.',
                'error'
            );

            return;
        }

        setChangingStatus(true);

        try {
            const result = await postJson(
                statusUrl,
                {
                    OwnerWalletId: ownerWalletId,
                    IsActive: isActive
                }
            );

            if (!result?.success) {
                throw new Error(
                    result?.message ||
                    'تعذر تغيير حالة الحساب.'
                );
            }

            closeDialog(query(SELECTORS.statusDialog));

            toast(
                result.message ||
                'تم تغيير حالة الحساب.',
                'success'
            );

            await refresh();
        }
        catch (error) {
            if (error?.name === 'AbortError') {
                return;
            }

            console.error(
                'Owner Financial Receiving Wallets:',
                error
            );

            toast(
                error?.message ||
                'تعذر تغيير حالة الحساب حاليًا.',
                'error'
            );
        }
        finally {
            setChangingStatus(false);
        }
    }

    function setChangingStatus(value) {
        state.isChangingStatus = Boolean(value);

        const confirmButton =
            query(SELECTORS.confirmStatus);

        if (!confirmButton) {
            return;
        }

        confirmButton.disabled =
            state.isChangingStatus;

        confirmButton.textContent =
            state.isChangingStatus
                ? 'جارٍ التنفيذ...'
                : confirmButton.dataset.defaultText ||
                'تأكيد';
    }

    async function refresh() {
        const loadUrl = state.root?.dataset.loadUrl;

        if (!loadUrl || state.isBusy) {
            return;
        }

        state.requestController?.abort();

        state.requestController =
            new AbortController();

        setBusy(true);

        try {
            const response = await fetch(
                loadUrl,
                {
                    method: 'GET',
                    credentials: 'same-origin',
                    cache: 'no-store',

                    headers: {
                        'X-Requested-With':
                            'XMLHttpRequest',

                        Accept:
                            'text/html, application/xhtml+xml'
                    },

                    signal:
                        state.requestController.signal
                }
            );

            if (
                response.status === 401 ||
                response.status === 403
            ) {
                throw new Error(
                    'انتهت الجلسة أو لا تملك صلاحية إدارة حسابات الاستقبال.'
                );
            }

            const contentType =
                response.headers.get(
                    'content-type'
                ) || '';

            const responseText =
                await response.text();

            if (
                !response.ok ||
                !contentType.includes('text/html')
            ) {
                throw new Error(
                    safeServerMessage(responseText) ||
                    'تعذر تحديث حسابات الاستقبال.'
                );
            }

            const nextRoot =
                parsePartialRoot(responseText);

            if (!nextRoot) {
                throw new Error(
                    'أعاد الخادم محتوى غير متوقع لتبويب حسابات الاستقبال.'
                );
            }

            const oldRoot = state.root;
            const savedApi = state.api;

            oldRoot.replaceWith(nextRoot);

            init(
                nextRoot,
                savedApi
            );
        }
        catch (error) {
            if (error?.name === 'AbortError') {
                return;
            }

            console.error(
                'Owner Financial Receiving Wallets:',
                error
            );

            toast(
                error?.message ||
                'تعذر تحديث حسابات الاستقبال حاليًا.',
                'error'
            );
        }
        finally {
            setBusy(false);
        }
    }

    async function postJson(url, payload) {
        const token = getAntiForgeryToken();

        if (!token) {
            throw new Error(
                'تعذر العثور على رمز الحماية. حدّث الصفحة ثم أعد المحاولة.'
            );
        }

        const response = await fetch(
            url,
            {
                method: 'POST',
                credentials: 'same-origin',
                cache: 'no-store',

                headers: {
                    'Content-Type':
                        'application/json',

                    Accept:
                        'application/json',

                    'X-Requested-With':
                        'XMLHttpRequest',

                    RequestVerificationToken:
                        token
                },

                body:
                    JSON.stringify(payload)
            }
        );

        const contentType =
            response.headers.get(
                'content-type'
            ) || '';

        let result = null;

        if (
            contentType.includes(
                'application/json'
            )
        ) {
            result = await response.json();
        }
        else {
            const responseText =
                await response.text();

            result = {
                success: false,

                message:
                    safeServerMessage(
                        responseText
                    ) ||
                    'أعاد الخادم استجابة غير متوقعة.'
            };
        }

        if (!response.ok) {
            const error = new Error(
                result?.message ||
                'تعذر تنفيذ العملية.'
            );

            error.result = result;

            applyServerErrors(
                result?.errors
            );

            throw error;
        }

        return result;
    }

    function getAntiForgeryToken() {
        if (
            typeof state.api
                ?.antiForgeryToken ===
            'function'
        ) {
            const apiToken =
                state.api.antiForgeryToken();

            if (apiToken) {
                return apiToken;
            }
        }

        return (
            query(
                'input[name="__RequestVerificationToken"]',
                state.root
            )?.value ||

            document.querySelector(
                'input[name="__RequestVerificationToken"]'
            )?.value ||

            ''
        );
    }

    function applyServerErrors(errors) {
        if (
            !errors ||
            typeof errors !== 'object'
        ) {
            return;
        }

        Object.entries(errors).forEach(
            ([rawKey, messages]) => {
                const key =
                    String(rawKey || '')
                        .split('.')
                        .pop();

                const message =
                    Array.isArray(messages)
                        ? messages.find(Boolean)
                        : String(messages || '');

                if (key && message) {
                    setFieldError(
                        key,
                        message
                    );
                }
            }
        );
    }

    function setFieldError(fieldName, message) {
        const errorElement = query(
            `[data-ofw-error-for="${cssEscape(fieldName)}"]`
        );

        const input =
            getFieldInput(fieldName);

        const field =
            input?.closest(
                '.wallet-vault__field'
            );

        if (errorElement) {
            errorElement.textContent =
                message || '';
        }

        field?.classList.toggle(
            'is-invalid',
            Boolean(message)
        );

        input?.setAttribute(
            'aria-invalid',
            message
                ? 'true'
                : 'false'
        );
    }

    function clearFieldError(fieldName) {
        setFieldError(
            fieldName,
            ''
        );
    }

    function clearAllErrors() {
        queryAll('[data-ofw-error-for]').forEach(
            element => {
                element.textContent = '';
            }
        );

        queryAll(
            '.wallet-vault__field.is-invalid'
        ).forEach(
            element => {
                element.classList.remove(
                    'is-invalid'
                );
            }
        );

        queryAll('[aria-invalid="true"]').forEach(
            element => {
                element.setAttribute(
                    'aria-invalid',
                    'false'
                );
            }
        );

        clearFormAlert();
    }

    function getFieldInput(fieldName) {
        const map = {
            WalletId:
                SELECTORS.walletId,

            AccountNumber:
                SELECTORS.accountNumber,

            BeneficiaryName:
                SELECTORS.beneficiaryName
        };

        return query(map[fieldName]);
    }

    function showFormAlert(message) {
        const alert =
            query(SELECTORS.formAlert);

        if (!alert) {
            return;
        }

        alert.replaceChildren();

        const icon =
            document.createElement('i');

        icon.className =
            'bi bi-exclamation-triangle-fill';

        icon.setAttribute(
            'aria-hidden',
            'true'
        );

        const text =
            document.createElement('span');

        text.textContent =
            message ||
            'تحقق من البيانات المدخلة.';

        alert.append(
            icon,
            text
        );

        alert.hidden = false;
    }

    function clearFormAlert() {
        const alert =
            query(SELECTORS.formAlert);

        if (!alert) {
            return;
        }

        alert.hidden = true;
        alert.replaceChildren();
    }

    async function copyAccountNumber(button) {
        const value =
            String(
                button.dataset.copyValue || ''
            ).trim();

        if (!value) {
            toast(
                'رقم الحساب غير متوفر للنسخ.',
                'warning'
            );

            return;
        }

        try {
            if (
                navigator.clipboard &&
                window.isSecureContext
            ) {
                await navigator.clipboard.writeText(
                    value
                );
            }
            else {
                fallbackCopy(value);
            }

            const icon =
                button.querySelector('i');

            const previousClass =
                icon?.className;

            if (icon) {
                icon.className =
                    'bi bi-check2';
            }

            toast(
                'تم نسخ رقم الحساب.',
                'success'
            );

            window.setTimeout(
                () => {
                    if (icon?.isConnected) {
                        icon.className =
                            previousClass ||
                            'bi bi-copy';
                    }
                },
                1200
            );
        }
        catch (error) {
            console.error(
                'Copy wallet account number:',
                error
            );

            toast(
                'تعذر نسخ رقم الحساب.',
                'error'
            );
        }
    }

    function fallbackCopy(value) {
        const textarea =
            document.createElement(
                'textarea'
            );

        textarea.value = value;

        textarea.setAttribute(
            'readonly',
            ''
        );

        textarea.style.position =
            'fixed';

        textarea.style.opacity =
            '0';

        textarea.style.pointerEvents =
            'none';

        document.body.appendChild(
            textarea
        );

        textarea.select();

        const copied =
            document.execCommand('copy');

        textarea.remove();

        if (!copied) {
            throw new Error(
                'Copy command failed.'
            );
        }
    }

    function setBusy(value) {
        state.isBusy = Boolean(value);

        if (!state.root) {
            return;
        }

        state.root.classList.toggle(
            'is-loading',
            state.isBusy
        );

        state.root.setAttribute(
            'aria-busy',
            state.isBusy
                ? 'true'
                : 'false'
        );

        queryAll('button').forEach(
            button => {
                if (state.isBusy) {
                    if (
                        !button.hasAttribute(
                            'data-ofw-was-disabled'
                        )
                    ) {
                        button.dataset.ofwWasDisabled =
                            button.disabled
                                ? 'true'
                                : 'false';
                    }

                    button.disabled = true;
                }
                else if (
                    button.hasAttribute(
                        'data-ofw-was-disabled'
                    )
                ) {
                    button.disabled =
                        button.dataset.ofwWasDisabled ===
                        'true';

                    delete button.dataset
                        .ofwWasDisabled;
                }
            }
        );

        if (
            typeof state.api
                ?.setLoading ===
            'function'
        ) {
            state.api.setLoading(
                state.isBusy
            );
        }
    }

    function resetBusyState() {
        state.root?.classList.remove(
            'is-loading'
        );

        state.root?.setAttribute(
            'aria-busy',
            'false'
        );
    }

    function parsePartialRoot(html) {
        const template =
            document.createElement(
                'template'
            );

        template.innerHTML =
            String(
                html || ''
            ).trim();

        const root =
            template.content.querySelector(
                ROOT_SELECTOR
            );

        return root
            ? root.cloneNode(true)
            : null;
    }

    function safeServerMessage(responseText) {
        const text =
            String(
                responseText || ''
            ).trim();

        if (
            !text ||
            /<\s*(html|body|title|h1|pre)[\s>]/i
                .test(text)
        ) {
            return '';
        }

        return text.length <= 300
            ? text
            : '';
    }

    function showDialog(dialog) {
        if (!dialog) {
            return;
        }

        if (
            typeof dialog.showModal ===
            'function'
        ) {
            if (!dialog.open) {
                dialog.showModal();
            }
        }
        else {
            dialog.setAttribute(
                'open',
                ''
            );
        }
    }

    function closeDialog(dialog) {
        if (!dialog) {
            return;
        }

        if (
            typeof dialog.close ===
            'function' &&
            dialog.open
        ) {
            dialog.close();
        }
        else {
            dialog.removeAttribute(
                'open'
            );

            restoreDialogFocus();
        }
    }

    function restoreDialogFocus() {
        const element =
            state.lastFocusedElement;

        state.lastFocusedElement = null;

        if (
            element instanceof HTMLElement &&
            element.isConnected
        ) {
            element.focus({
                preventScroll: true
            });
        }
    }

    function updateAccountCounter() {
        const input =
            query(SELECTORS.accountNumber);

        const counter =
            query(SELECTORS.accountCounter);

        if (!counter) {
            return;
        }

        counter.textContent =
            `${String(input?.value || '').length}/6`;
    }

    function normalizeAccountNumber(value) {
        return String(value || '')
            .replace(/[^0-9]/g, '')
            .slice(0, 6);
    }

    function setInputValue(selector, value) {
        const element =
            query(selector);

        if (
            element &&
            'value' in element
        ) {
            element.value = value;
        }
    }

    function setCheckboxValue(selector, value) {
        const element =
            query(selector);

        if (
            element instanceof HTMLInputElement
        ) {
            element.checked =
                Boolean(value);
        }
    }

    function setText(selector, value) {
        const element =
            query(selector);

        if (element) {
            element.textContent =
                value;
        }
    }

    function cleanInteger(value) {
        const number =
            Number.parseInt(
                String(value ?? ''),
                10
            );

        return Number.isFinite(number)
            ? number
            : 0;
    }

    function parseBoolean(value) {
        if (typeof value === 'boolean') {
            return value;
        }

        return (
            String(value || '')
                .toLowerCase() ===
            'true'
        );
    }

    function cssEscape(value) {
        if (window.CSS?.escape) {
            return window.CSS.escape(
                String(value)
            );
        }

        return String(value)
            .replace(
                /[^a-zA-Z0-9_-]/g,
                '\\$&'
            );
    }

    function toast(message, type = 'info') {
        if (!message) {
            return;
        }

        if (
            typeof state.api
                ?.toast ===
            'function'
        ) {
            state.api.toast(
                message,
                type
            );

            return;
        }

        if (
            typeof window
                .OwnerFinancialShell
                ?.toast ===
            'function'
        ) {
            window.OwnerFinancialShell
                .toast(
                    message,
                    type
                );

            return;
        }

        fallbackToast(
            message,
            type
        );
    }

    function fallbackToast(message, type) {
        let host =
            document.getElementById(
                'ownerFinancialReceivingWalletsToastHost'
            );

        if (!host) {
            host =
                document.createElement(
                    'div'
                );

            host.id =
                'ownerFinancialReceivingWalletsToastHost';

            Object.assign(
                host.style,
                {
                    position: 'fixed',
                    top: '18px',
                    right: '18px',
                    zIndex: '100000',
                    display: 'grid',
                    gap: '9px',
                    width:
                        'min(360px, calc(100vw - 36px))',
                    direction: 'rtl'
                }
            );

            document.body.appendChild(
                host
            );
        }

        const palette = {
            success:
                ['#eaf8f3', '#13856c'],

            error:
                ['#fff0f1', '#d4474d'],

            warning:
                ['#fff6e5', '#a96800'],

            info:
                ['#eaf9fd', '#008cb8']
        }[type] || ['#eaf9fd', '#008cb8'];

        const item =
            document.createElement(
                'div'
            );

        item.textContent = message;

        Object.assign(
            item.style,
            {
                padding: '12px 15px',
                color: palette[1],
                border:
                    `1px solid ${palette[1]}33`,
                borderRadius: '13px',
                background: palette[0],
                boxShadow:
                    '0 14px 34px rgba(5,32,60,.14)',
                fontSize: '.76rem',
                fontWeight: '800',
                lineHeight: '1.7'
            }
        );

        host.appendChild(item);

        window.setTimeout(
            () => {
                item.style.opacity =
                    '0';

                item.style.transform =
                    'translateY(-6px)';

                item.style.transition =
                    'opacity .2s ease, transform .2s ease';

                window.setTimeout(
                    () => item.remove(),
                    220
                );
            },
            3200
        );
    }

    function startObserver() {
        if (
            state.observer ||
            !document.body
        ) {
            return;
        }

        state.observer =
            new MutationObserver(
                () => {
                    const root =
                        document.querySelector(
                            ROOT_SELECTOR
                        );

                    if (
                        root &&
                        root !== state.root
                    ) {
                        init(
                            root,
                            state.api || {}
                        );
                    }
                }
            );

        state.observer.observe(
            document.body,
            {
                childList: true,
                subtree: true
            }
        );

        const currentRoot =
            document.querySelector(
                ROOT_SELECTOR
            );

        if (currentRoot) {
            init(
                currentRoot,
                state.api || {}
            );
        }
    }

    const moduleApi =
        Object.freeze({
            init,

            destroy,

            refresh,

            openAdd: walletId => {
                const normalizedWalletId =
                    cleanInteger(walletId);

                const button =
                    normalizedWalletId > 0
                        ? queryAll(
                            '[data-ofw-open-add]'
                        ).find(
                            item =>
                                cleanInteger(
                                    item.dataset.walletId
                                ) ===
                                normalizedWalletId
                        )
                        : query(
                            '[data-ofw-open-add]'
                        );

                if (button) {
                    openAddDialog(button);
                }
            },

            openEdit: ownerWalletId => {
                const normalizedOwnerWalletId =
                    cleanInteger(ownerWalletId);

                const button =
                    queryAll(
                        '[data-ofw-open-edit]'
                    ).find(
                        item =>
                            cleanInteger(
                                item.dataset.ownerWalletId
                            ) ===
                            normalizedOwnerWalletId
                    );

                if (button) {
                    openEditDialog(button);
                }
            }
        });

    if (window[MODULE_KEY]) {
        window.OwnerFinancialTabs[
            TAB_KEY
        ] = window[MODULE_KEY];

        return;
    }

    window[MODULE_KEY] = moduleApi;

    window.OwnerFinancialTabs[
        TAB_KEY
    ] = moduleApi;

    if (
        document.readyState ===
        'loading'
    ) {
        document.addEventListener(
            'DOMContentLoaded',
            startObserver,
            {
                once: true
            }
        );
    }
    else {
        startObserver();
    }
})();