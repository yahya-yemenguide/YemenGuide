(() => {
    'use strict';

    const host = document.querySelector('[data-password-gate]');
    if (!host) return;

    const form = document.getElementById('forceChangePwdForm');
    const newPassword = document.getElementById('newPassword');
    const confirmPassword = document.getElementById('confirmPassword');
    const submitButton = document.getElementById('forcePasswordSubmit');
    const message = document.getElementById('forcePasswordMessage');
    const strengthBars = host.querySelector('[data-strength-bars]');
    const strengthLabel = host.querySelector('[data-strength-label]');

    const isAdminEmployee = host.dataset.adminEmployee === 'true';
    const minimumLength = Number(host.dataset.minLength || (isAdminEmployee ? 8 : 6));

    document.documentElement.classList.add('password-gate-open');
    document.body.classList.add('password-gate-open');

    // لا يوجد زر إغلاق، ونمنع Escape والرجوع أثناء بوابة الأمان.
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
        }
    }, true);

    try {
        history.pushState({ passwordGate: true }, '', window.location.href);
        window.addEventListener('popstate', () => {
            history.pushState({ passwordGate: true }, '', window.location.href);
        });
    } catch (_) {
        // بعض البيئات المقيدة قد تمنع history API؛ الواجهة تبقى إلزامية بدونه.
    }

    function setMessage(text, type = 'error') {
        if (!message) return;

        message.className = `admin-password-gate-message is-${type}`;
        message.innerHTML = `
            <i class="bi ${type === 'success' ? 'bi-check-circle-fill' : 'bi-exclamation-circle-fill'}"></i>
            <span>${escapeHtml(text)}</span>`;
    }

    function clearMessage() {
        if (!message) return;
        message.className = 'admin-password-gate-message';
        message.textContent = '';
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    function rule(name, valid) {
        const element = host.querySelector(`[data-rule="${name}"]`);
        element?.classList.toggle('is-valid', Boolean(valid));
    }

    function passwordState() {
        const value = newPassword?.value ?? '';
        const confirmValue = confirmPassword?.value ?? '';

        const hasLength = value.length >= minimumLength;
        const hasLetter = /[A-Za-z]/.test(value);
        const hasNumber = /\d/.test(value);
        const matches = value.length > 0 && value === confirmValue;

        if (isAdminEmployee) {
            rule('length', hasLength);
            rule('letter', hasLetter);
            rule('number', hasNumber);
            rule('match', matches);
        }

        let level = 0;
        if (hasLength) level += 1;
        if (hasLetter) level += 1;
        if (hasNumber) level += 1;
        if (value.length >= Math.max(minimumLength + 2, 10) && /[^A-Za-z0-9]/.test(value)) {
            level += 1;
        }

        if (strengthBars) {
            strengthBars.dataset.level = String(Math.min(level, 4));
        }

        if (strengthLabel) {
            const labels = ['ابدأ الكتابة', 'ضعيفة', 'مقبولة', 'جيدة', 'قوية'];
            strengthLabel.textContent = labels[Math.min(level, 4)];
        }

        const newShell = host.querySelector('[data-password-shell="new"]');
        const confirmShell = host.querySelector('[data-password-shell="confirm"]');

        const adminPolicyValid = hasLength && hasLetter && hasNumber;
        const ownerPolicyValid = hasLength;
        const policyValid = isAdminEmployee ? adminPolicyValid : ownerPolicyValid;

        newShell?.classList.toggle('is-valid', value.length > 0 && policyValid);
        newShell?.classList.toggle('is-invalid', value.length > 0 && !policyValid);

        confirmShell?.classList.toggle('is-valid', confirmValue.length > 0 && matches);
        confirmShell?.classList.toggle('is-invalid', confirmValue.length > 0 && !matches);

        const allValid = policyValid && matches;
        if (submitButton) {
            submitButton.disabled = !allValid;
        }

        return {
            hasLength,
            hasLetter,
            hasNumber,
            matches,
            allValid
        };
    }

    host.querySelectorAll('[data-password-toggle]').forEach(button => {
        button.addEventListener('click', () => {
            const targetId = button.dataset.passwordToggle;
            const input = targetId ? document.getElementById(targetId) : null;
            if (!input) return;

            const shouldShow = input.type === 'password';
            input.type = shouldShow ? 'text' : 'password';

            const icon = button.querySelector('i');
            if (icon) {
                icon.className = shouldShow ? 'bi bi-eye-slash' : 'bi bi-eye';
            }

            input.focus();
        });
    });

    [newPassword, confirmPassword].forEach(input => {
        input?.addEventListener('input', () => {
            clearMessage();
            passwordState();
        });
    });

    form?.addEventListener('submit', async event => {
        event.preventDefault();
        clearMessage();

        const state = passwordState();
        if (!state.allValid) {
            setMessage(
                isAdminEmployee
                    ? 'راجع شروط كلمة المرور وتأكد أن التأكيد مطابق.'
                    : 'راجع كلمة المرور وتأكد أن التأكيد مطابق.'
            );
            return;
        }

        const originalHtml = submitButton?.innerHTML ?? '';

        if (submitButton) {
            submitButton.disabled = true;
            submitButton.innerHTML = `
                <span class="admin-password-gate-submit-icon">
                    <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
                </span>
                <span class="admin-password-gate-submit-copy">
                    <strong>جاري تأمين الحساب...</strong>
                    <small>لحظات ويتم اعتماد كلمة المرور الجديدة</small>
                </span>`;
        }

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

            if (!response.ok || !result.success) {
                throw new Error(result.message || 'تعذر تغيير كلمة المرور.');
            }

            setMessage(result.message || 'تم تأمين الحساب بنجاح.', 'success');

            if (submitButton) {
                submitButton.innerHTML = `
                    <span class="admin-password-gate-submit-icon">
                        <i class="bi bi-check2-circle"></i>
                    </span>
                    <span class="admin-password-gate-submit-copy">
                        <strong>تم تأمين الحساب</strong>
                        <small>جاري فتح القسم المسموح لك به...</small>
                    </span>`;
            }

            window.setTimeout(() => {
                window.location.replace(result.url || '/');
            }, 650);
        } catch (error) {
            setMessage(error?.message || 'حدث خطأ في الاتصال بالخادم.');

            if (submitButton) {
                submitButton.innerHTML = originalHtml;
                passwordState();
            }
        }
    });

    passwordState();

    window.setTimeout(() => {
        newPassword?.focus();
    }, 180);
})();
