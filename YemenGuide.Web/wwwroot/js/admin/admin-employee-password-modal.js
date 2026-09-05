(() => {
    'use strict';

    const gate = document.getElementById('employeePasswordGate');
    if (!gate) return;

    const form = document.getElementById('employeePasswordGateForm');
    const newPassword = document.getElementById('employeeNewPassword');
    const confirmPassword = document.getElementById('employeeConfirmPassword');
    const submitButton = document.getElementById('employeePasswordGateSubmit');
    const submitText = submitButton?.querySelector('.employee-password-gate__submit-text');
    const spinner = submitButton?.querySelector('.employee-password-gate__spinner');
    const errorBox = document.getElementById('employeePasswordGateError');

    const rules = {
        length: gate.querySelector('[data-rule="length"]'),
        letter: gate.querySelector('[data-rule="letter"]'),
        number: gate.querySelector('[data-rule="number"]'),
        match: gate.querySelector('[data-rule="match"]')
    };

    document.body.classList.add('employee-password-gate-open');

    // لا يوجد زر إغلاق، كما أن ESC والضغط على الخلفية لا يغلقان البوابة.
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    }, true);

    gate.querySelector('.employee-password-gate__backdrop')
        ?.addEventListener('click', event => {
            event.preventDefault();
            newPassword?.focus();
        });

    gate.querySelectorAll('[data-password-toggle]').forEach(button => {
        button.addEventListener('click', () => {
            const targetId = button.getAttribute('data-password-toggle');
            const input = document.getElementById(targetId);
            const icon = button.querySelector('i');
            if (!input) return;

            const shouldShow = input.type === 'password';
            input.type = shouldShow ? 'text' : 'password';
            if (icon) {
                icon.className = shouldShow ? 'bi bi-eye-slash' : 'bi bi-eye';
            }
        });
    });

    function getState() {
        const value = newPassword?.value ?? '';
        const confirmValue = confirmPassword?.value ?? '';

        return {
            length: value.length >= 8,
            letter: /[A-Za-z]/.test(value),
            number: /\d/.test(value),
            match: value.length > 0 && confirmValue.length > 0 && value === confirmValue
        };
    }

    function renderRules() {
        const state = getState();

        Object.entries(state).forEach(([key, valid]) => {
            rules[key]?.classList.toggle('is-valid', valid);
        });

        const allValid = Object.values(state).every(Boolean);
        if (submitButton) submitButton.disabled = !allValid;

        if (errorBox && !errorBox.hidden) {
            errorBox.hidden = true;
            errorBox.textContent = '';
        }

        return allValid;
    }

    newPassword?.addEventListener('input', renderRules);
    confirmPassword?.addEventListener('input', renderRules);

    function setLoading(loading) {
        if (!submitButton) return;

        submitButton.disabled = loading || !Object.values(getState()).every(Boolean);
        if (spinner) spinner.hidden = !loading;
        if (submitText) {
            submitText.textContent = loading
                ? 'جاري تأمين الحساب...'
                : 'تأمين الحساب والمتابعة';
        }
    }

    function showError(message) {
        if (!errorBox) return;
        errorBox.textContent = message || 'تعذر حفظ كلمة المرور الجديدة.';
        errorBox.hidden = false;
    }

    form?.addEventListener('submit', async event => {
        event.preventDefault();

        if (!renderRules()) {
            showError('راجع شروط كلمة المرور وتأكد من تطابق الكلمتين.');
            return;
        }

        setLoading(true);

        try {
            const response = await fetch('/Account/ForceChangePassword', {
                method: 'POST',
                body: new FormData(form),
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const result = await response.json();

            if (!response.ok || result.success !== true) {
                showError(result.message || 'تعذر حفظ كلمة المرور الجديدة.');
                setLoading(false);
                return;
            }

            if (submitText) submitText.textContent = 'تم تأمين الحساب';
            if (spinner) spinner.hidden = true;
            submitButton?.classList.add('is-success');

            window.setTimeout(() => {
                window.location.href = result.url || window.location.href;
            }, 450);
        } catch {
            showError('تعذر الاتصال بالخادم. أعد المحاولة.');
            setLoading(false);
        }
    });

    window.setTimeout(() => newPassword?.focus(), 120);
})();
