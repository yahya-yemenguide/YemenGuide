(() => {
    'use strict';

    const SHELL_ID = 'ownerFinancialsShell';
    const ERROR_CLASS = 'owner-financial-field-error';
    const digitMap = Object.freeze({
        '٠': '0', '١': '1', '٢': '2', '٣': '3', '٤': '4',
        '٥': '5', '٦': '6', '٧': '7', '٨': '8', '٩': '9',
        '۰': '0', '۱': '1', '۲': '2', '۳': '3', '۴': '4',
        '۵': '5', '۶': '6', '۷': '7', '۸': '8', '۹': '9'
    });

    const toEnglishDigits = value =>
        String(value ?? '').replace(/[٠-٩۰-۹]/g, digit => digitMap[digit] || digit);

    const shell = () => document.getElementById(SHELL_ID);

    const toast = (message, type = 'warning') => {
        const api = window.OwnerFinancials;
        if (api && typeof api.showToast === 'function') {
            api.showToast(message, type);
            return;
        }

        const host = document.getElementById('ownerFinancialToastHost');
        if (!host) return;

        const item = document.createElement('div');
        item.className = `owner-financial-toast is-${type}`;
        item.setAttribute('role', 'status');
        item.textContent = message;
        host.appendChild(item);
        window.setTimeout(() => item.remove(), 4500);
    };

    const clearError = field => {
        field.removeAttribute('aria-invalid');
        const wrapper = field.closest('label, .owner-financial-field, .wallet-vault__input-shell') || field.parentElement;
        wrapper?.querySelector(`.${ERROR_CLASS}[data-for="${field.name}"]`)?.remove();
    };

    const setError = (field, message) => {
        clearError(field);
        field.setAttribute('aria-invalid', 'true');

        const wrapper = field.closest('label, .owner-financial-field, .wallet-vault__input-shell') || field.parentElement;
        if (!wrapper) return;

        const error = document.createElement('span');
        error.className = ERROR_CLASS;
        error.dataset.for = field.name || field.id || 'field';
        error.textContent = message;
        wrapper.appendChild(error);
    };

    const cleanNumericValue = field => {
        const name = (field.name || '').toLowerCase();
        const isDecimal = field.hasAttribute('data-balance-positive-money') ||
            name === 'amount' ||
            field.inputMode === 'decimal' ||
            String(field.step || '').includes('.');

        let value = toEnglishDigits(field.value).trim();
        value = isDecimal
            ? value.replace(/[^0-9.]/g, '')
            : value.replace(/[^0-9]/g, '');

        if (isDecimal) {
            const firstDot = value.indexOf('.');
            if (firstDot >= 0) {
                value = value.slice(0, firstDot + 1) +
                    value.slice(firstDot + 1).replace(/\./g, '').slice(0, 2);
            }
        }

        const maxLength = Number(field.maxLength);
        if (Number.isFinite(maxLength) && maxLength > 0) {
            value = value.slice(0, maxLength);
        }

        if (field.value !== value) field.value = value;
    };

    const isNumericField = field => {
        if (!(field instanceof HTMLInputElement) || field.type === 'hidden') return false;
        const name = (field.name || '').toLowerCase();
        return field.type === 'number' ||
            field.inputMode === 'numeric' ||
            field.inputMode === 'decimal' ||
            field.hasAttribute('data-ofw-account-number') ||
            ['bookingid', 'customerid', 'amount'].includes(name);
    };

    const validateField = field => {
        clearError(field);

        if (isNumericField(field)) cleanNumericValue(field);

        if (!field.checkValidity()) {
            let message = field.validationMessage || 'راجع قيمة هذا الحقل.';
            if (field.validity.valueMissing) message = 'هذا الحقل مطلوب.';
            else if (field.validity.rangeUnderflow) message = 'القيمة أقل من الحد المسموح.';
            else if (field.validity.rangeOverflow) message = 'القيمة أكبر من الحد المسموح.';
            else if (field.validity.tooShort) message = `أدخل ${field.minLength} خانات على الأقل.`;
            else if (field.validity.tooLong) message = `الحد الأعلى ${field.maxLength} خانة.`;
            else if (field.validity.patternMismatch && field.hasAttribute('data-ofw-account-number')) {
                message = 'رقم الحساب يجب أن يكون من 6 إلى 9 أرقام فقط.';
            }

            setError(field, message);
            return false;
        }

        if (field.type === 'number' && field.value !== '') {
            const value = Number(field.value);
            if (!Number.isFinite(value) || value < 0) {
                setError(field, 'أدخل رقمًا صحيحًا بدون قيمة سالبة.');
                return false;
            }
        }

        return true;
    };

    const validateDateRange = form => {
        const from = form.querySelector('[name="FromDate"], [name="fromDate"]');
        const to = form.querySelector('[name="ToDate"], [name="toDate"]');
        if (!from || !to || !from.value || !to.value) return true;

        if (from.value > to.value) {
            setError(from, 'تاريخ البداية يجب أن يسبق تاريخ النهاية.');
            setError(to, 'تاريخ النهاية يجب أن يلي تاريخ البداية.');
            return false;
        }

        return true;
    };

    const validateForm = form => {
        let valid = validateDateRange(form);
        const fields = form.querySelectorAll('input:not([type="hidden"]), select, textarea');
        fields.forEach(field => {
            if (!validateField(field)) valid = false;
        });

        if (!valid) {
            const first = form.querySelector('[aria-invalid="true"]');
            first?.focus({ preventScroll: false });
            first?.scrollIntoView({ behavior: 'smooth', block: 'center' });
            toast('راجع الحقول المحددة قبل المتابعة.', 'warning');
        }

        return valid;
    };

    const setDateLimits = root => {
        const today = new Date();
        const year = today.getFullYear();
        const month = String(today.getMonth() + 1).padStart(2, '0');
        const day = String(today.getDate()).padStart(2, '0');
        const todayValue = `${year}-${month}-${day}`;

        root.querySelectorAll('input[type="date"]').forEach(field => {
            if (!field.max) field.max = todayValue;
        });

        root.querySelectorAll('input[type="datetime-local"][name="TransferDate"]').forEach(field => {
            const allowed = new Date(Date.now() + 10 * 60 * 1000);
            const pad = value => String(value).padStart(2, '0');
            field.max = `${allowed.getFullYear()}-${pad(allowed.getMonth() + 1)}-${pad(allowed.getDate())}T${pad(allowed.getHours())}:${pad(allowed.getMinutes())}`;
        });
    };

    const enhance = root => {
        setDateLimits(root);

        root.querySelectorAll('input[data-ofw-account-number]').forEach(field => {
            field.minLength = 6;
            field.maxLength = 9;
            field.pattern = '[0-9]{6,9}';
            field.inputMode = 'numeric';
        });

        root.querySelectorAll('input[type="number"]').forEach(field => {
            if (!field.min || Number(field.min) < 0) field.min = field.name === 'Amount' ? '0.01' : '1';
            field.inputMode = field.name === 'Amount' ? 'decimal' : 'numeric';
        });
    };

    document.addEventListener('input', event => {
        const root = shell();
        const field = event.target;
        if (!root?.contains(field) || !(field instanceof HTMLInputElement)) return;
        if (isNumericField(field)) cleanNumericValue(field);
        clearError(field);
    });

    document.addEventListener('change', event => {
        const root = shell();
        const field = event.target;
        if (!root?.contains(field) || !(field instanceof HTMLElement)) return;
        if (field.matches('input, select, textarea')) validateField(field);
    });

    document.addEventListener('submit', event => {
        const root = shell();
        const form = event.target;
        if (!root?.contains(form) || !(form instanceof HTMLFormElement)) return;
        if (!validateForm(form)) {
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    }, true);

    const observer = new MutationObserver(records => {
        records.forEach(record => record.addedNodes.forEach(node => {
            if (!(node instanceof HTMLElement)) return;
            enhance(node);
        }));
    });

    const start = () => {
        const root = shell();
        if (!root) return;
        enhance(root);
        observer.observe(root, { childList: true, subtree: true });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
