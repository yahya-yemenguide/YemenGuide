/**
 * =============================================================
 * YEMENGUIDE AUTH SECURITY V2
 * Mandatory password setup for Google / force-change screen
 * =============================================================
 */

(() => {
    'use strict';

    const PASSWORD_POLICY = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/;

    const ready = callback => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    };

    const setMessage = (form, message, kind = 'error') => {
        const box = form.querySelector('[data-form-message]');
        if (!box) return;

        if (!message) {
            box.hidden = true;
            box.textContent = '';
            box.classList.remove('security-message--success');
            box.classList.add('security-message--error');
            return;
        }

        box.hidden = false;
        box.classList.toggle('security-message--success', kind === 'success');
        box.classList.toggle('security-message--error', kind !== 'success');
        box.textContent = message;
    };

    const setupToggle = button => {
        const selector = button.dataset.passwordToggle;
        const input = selector ? document.querySelector(selector) : null;
        const icon = button.querySelector('i');
        if (!input) return;

        button.addEventListener('click', () => {
            const reveal = input.type === 'password';
            input.type = reveal ? 'text' : 'password';
            button.setAttribute('aria-label', reveal ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور');
            if (icon) {
                icon.classList.toggle('bi-eye-fill', reveal);
                icon.classList.toggle('bi-eye-slash-fill', !reveal);
            }
        });
    };

    const setupMeter = form => {
        const password = form.querySelector('input[name="newPassword"]');
        const meter = form.querySelector('[data-password-meter]');
        const text = form.querySelector('[data-strength-text]');
        const bar = form.querySelector('[data-strength-bar]');
        if (!password || !meter || !text || !bar) return;

        const update = () => {
            setMessage(form, '');
            const value = password.value || '';

            if (!value) {
                meter.hidden = true;
                bar.style.width = '0%';
                return;
            }

            meter.hidden = false;
            const rules = {
                length: value.length >= 8,
                letter: /[A-Za-z]/.test(value),
                number: /\d/.test(value)
            };

            meter.querySelectorAll('[data-rule]').forEach(item => {
                const valid = Boolean(rules[item.dataset.rule]);
                item.classList.toggle('is-valid', valid);
                const icon = item.querySelector('i');
                if (icon) icon.className = valid ? 'bi bi-check-circle-fill' : 'bi bi-circle';
            });

            const passed = Object.values(rules).filter(Boolean).length;
            const symbol = /[^A-Za-z0-9]/.test(value);

            let width = '34%';
            let label = 'ضعيفة';
            let color = '#ff5a5f';

            if (passed === 2) {
                width = '66%';
                label = 'متوسطة';
                color = '#e8a923';
            }

            if (passed === 3) {
                width = symbol ? '100%' : '88%';
                label = symbol ? 'ممتازة' : 'قوية';
                color = '#18a875';
            }

            bar.style.width = width;
            bar.style.background = color;
            text.textContent = label;
            text.style.color = color;
        };

        password.addEventListener('input', update);
        form.querySelector('input[name="confirmPassword"]')?.addEventListener('input', () => setMessage(form, ''));
        update();
    };

    const normalizePath = url => {
        try {
            const parsed = new URL(url, window.location.origin);
            return parsed.pathname.replace(/\/+$/, '').toLowerCase() || '/';
        } catch {
            return '';
        }
    };

    const closeModal = modal => {
        if (!modal) return;
        modal.classList.add('is-closing');

        window.setTimeout(() => {
            modal.classList.remove('is-open', 'is-closing');
            modal.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('auth-security-locked');
            window.dispatchEvent(new CustomEvent('auth:password-secured'));
        }, 260);
    };

    const setupForm = form => {
        const password = form.querySelector('input[name="newPassword"]');
        const confirm = form.querySelector('input[name="confirmPassword"]');
        const submit = form.querySelector('[data-submit-button]') || form.querySelector('button[type="submit"]');
        const endpoint = form.dataset.endpoint || form.action;
        const isModalForm = form.dataset.modalForm === 'true';
        const modal = form.closest('[data-auth-security-modal]');

        setupMeter(form);
        form.querySelectorAll('[data-password-toggle]').forEach(setupToggle);

        form.addEventListener('submit', async event => {
            event.preventDefault();
            setMessage(form, '');

            const newPassword = password?.value || '';
            const confirmPassword = confirm?.value || '';

            if (!PASSWORD_POLICY.test(newPassword)) {
                setMessage(form, 'كلمة المرور يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.');
                password?.focus();
                return;
            }

            if (newPassword !== confirmPassword) {
                setMessage(form, 'تأكيد كلمة المرور غير مطابق.');
                confirm?.focus();
                return;
            }

            const original = submit?.innerHTML || '';
            if (submit) {
                submit.disabled = true;
                submit.setAttribute('aria-busy', 'true');
                submit.innerHTML = '<span class="spinner-border" aria-hidden="true"></span><span>جاري تأمين الحساب...</span>';
            }

            let succeeded = false;

            try {
                const response = await fetch(endpoint, {
                    method: 'POST',
                    body: new FormData(form),
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const data = await response.json();

                if (!data.success) {
                    if (data.sessionExpired) {
                        window.location.assign('/Account/Login');
                        return;
                    }

                    setMessage(form, data.message || 'تعذر تأمين الحساب. حاول مرة أخرى.');
                    return;
                }

                succeeded = true;

                if (submit) {
                    submit.innerHTML = '<i class="bi bi-check-circle-fill"></i><span>تم تأمين الحساب</span>';
                }
                setMessage(form, data.message || 'تم تأمين الحساب بنجاح.', 'success');

                window.setTimeout(() => {
                    if (isModalForm && modal) {
                        const targetPath = normalizePath(data.url || '');
                        const currentPath = normalizePath(window.location.href);
                        closeModal(modal);

                        if (targetPath && targetPath !== currentPath) {
                            window.location.assign(data.url);
                        }
                        return;
                    }

                    if (data.url) window.location.assign(data.url);
                }, 650);
            } catch {
                setMessage(form, 'تعذر الاتصال بالخادم. تحقق من الإنترنت ثم حاول مرة أخرى.');
            } finally {
                if (submit && !succeeded) {
                    submit.disabled = false;
                    submit.removeAttribute('aria-busy');
                    submit.innerHTML = original;
                }
            }
        });
    };

    const setupMandatoryModal = modal => {
        if (!modal?.classList.contains('is-open')) return;

        document.body.classList.add('auth-security-locked');
        modal.removeAttribute('aria-hidden');

        const dialog = modal.querySelector('.auth-security-dialog');
        window.setTimeout(() => dialog?.focus({ preventScroll: true }), 120);

        modal.querySelectorAll('input').forEach(input => {
            input.addEventListener('focus', () => {
                window.setTimeout(() => {
                    input.scrollIntoView({ block: 'center', behavior: 'smooth' });
                }, 180);
            });
        });

        // المودل إجباري: لا Escape ولا ضغط خلفية لإغلاقه.
        document.addEventListener('keydown', event => {
            if (!modal.classList.contains('is-open')) return;

            if (event.key === 'Escape') {
                event.preventDefault();
                event.stopPropagation();
                return;
            }

            if (event.key === 'Tab' && dialog) {
                const focusables = Array.from(dialog.querySelectorAll(
                    'button:not([disabled]), input:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
                ));

                if (!focusables.length) return;

                const first = focusables[0];
                const last = focusables[focusables.length - 1];

                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault();
                    last.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault();
                    first.focus();
                }
            }
        }, true);
    };

    ready(() => {
        document.querySelectorAll('[data-auth-password-form]').forEach(setupForm);
        document.querySelectorAll('[data-auth-security-modal]').forEach(setupMandatoryModal);
    });
})();
