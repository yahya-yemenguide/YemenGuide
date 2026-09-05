/**
 * =============================================================
 * YEMENGUIDE PASSWORD RECOVERY V2
 * Forgot password + reset password + OTP resend/countdown
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

    const setMessage = (element, message, kind = 'error') => {
        if (!element) return;

        if (!message) {
            element.hidden = true;
            element.textContent = '';
            element.classList.remove('recovery-message--success');
            element.classList.add('recovery-message--error');
            return;
        }

        element.hidden = false;
        element.classList.toggle('recovery-message--success', kind === 'success');
        element.classList.toggle('recovery-message--error', kind !== 'success');
        element.textContent = message;
    };

    const setLoading = (button, text) => {
        if (!button) return () => {};
        const original = button.innerHTML;
        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
        button.innerHTML = `<span class="spinner-border" aria-hidden="true"></span><span>${text}</span>`;

        return () => {
            button.disabled = false;
            button.removeAttribute('aria-busy');
            button.innerHTML = original;
        };
    };

    const formatSeconds = total => {
        const safe = Math.max(0, Math.floor(Number(total) || 0));
        const minutes = String(Math.floor(safe / 60)).padStart(2, '0');
        const seconds = String(safe % 60).padStart(2, '0');
        return `${minutes}:${seconds}`;
    };

    const createCountdown = ({ button, output, seconds = 45, readyText = 'إعادة إرسال الرمز' }) => {
        let timerId = 0;
        let remaining = seconds;

        const stop = () => {
            if (timerId) window.clearInterval(timerId);
            timerId = 0;
        };

        const render = () => {
            if (!button) return;

            if (remaining <= 0) {
                stop();
                button.disabled = false;
                button.textContent = readyText;
                return;
            }

            button.disabled = true;
            button.innerHTML = `إعادة الإرسال بعد <strong>${formatSeconds(remaining)}</strong>`;
            if (output) output.textContent = formatSeconds(remaining);
            remaining -= 1;
        };

        const restart = value => {
            stop();
            remaining = Math.max(1, Number(value) || seconds);
            render();
            timerId = window.setInterval(render, 1000);
        };

        restart(seconds);
        return { restart, stop };
    };

    const setupPasswordToggles = () => {
        document.querySelectorAll('[data-password-toggle]').forEach(button => {
            button.addEventListener('click', () => {
                const selector = button.dataset.passwordToggle;
                const input = selector ? document.querySelector(selector) : null;
                const icon = button.querySelector('i');
                if (!input) return;

                const reveal = input.type === 'password';
                input.type = reveal ? 'text' : 'password';
                button.setAttribute('aria-label', reveal ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور');

                if (icon) {
                    icon.classList.toggle('bi-eye-fill', reveal);
                    icon.classList.toggle('bi-eye-slash-fill', !reveal);
                }
            });
        });
    };

    const setupOtpCells = (scope, hiddenInput) => {
        const cells = Array.from(scope?.querySelectorAll('.otp-cell') || []);
        if (!cells.length || !hiddenInput) return null;

        const sync = () => {
            hiddenInput.value = cells.map(cell => cell.value).join('');
            cells.forEach(cell => cell.classList.toggle('is-filled', Boolean(cell.value)));
        };

        const distribute = (digits, startIndex = 0) => {
            const clean = String(digits || '').replace(/\D/g, '').slice(0, cells.length - startIndex);
            clean.split('').forEach((digit, offset) => {
                if (cells[startIndex + offset]) cells[startIndex + offset].value = digit;
            });
            sync();
            const nextIndex = Math.min(startIndex + clean.length, cells.length - 1);
            cells[nextIndex]?.focus();
        };

        cells.forEach((cell, index) => {
            cell.addEventListener('input', event => {
                const digits = event.target.value.replace(/\D/g, '');
                if (digits.length > 1) {
                    event.target.value = '';
                    distribute(digits, index);
                    return;
                }

                event.target.value = digits.slice(-1);
                sync();
                if (event.target.value && index < cells.length - 1) cells[index + 1].focus();
            });

            cell.addEventListener('keydown', event => {
                if (event.key === 'Backspace' && !cell.value && index > 0) {
                    cells[index - 1].focus();
                    cells[index - 1].value = '';
                    sync();
                }

                if (event.key === 'ArrowLeft' && index < cells.length - 1) {
                    event.preventDefault();
                    cells[index + 1].focus();
                }

                if (event.key === 'ArrowRight' && index > 0) {
                    event.preventDefault();
                    cells[index - 1].focus();
                }
            });

            cell.addEventListener('paste', event => {
                const text = event.clipboardData?.getData('text') || '';
                const digits = text.replace(/\D/g, '');
                if (!digits) return;
                event.preventDefault();
                distribute(digits, 0);
            });
        });

        return {
            get value() {
                sync();
                return hiddenInput.value;
            },
            clear() {
                cells.forEach(cell => {
                    cell.value = '';
                    cell.classList.remove('is-filled');
                });
                hiddenInput.value = '';
                cells[0]?.focus();
            }
        };
    };

    const setupPasswordMeter = input => {
        const meter = document.getElementById('passwordStrengthContainer');
        const text = document.getElementById('strengthText');
        const bar = document.getElementById('strengthBar');
        if (!input || !meter || !text || !bar) return;

        const update = () => {
            const value = input.value || '';
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
            const hasSymbol = /[^A-Za-z0-9]/.test(value);

            let width = '34%';
            let label = 'ضعيفة';
            let color = '#ff5a5f';

            if (passed === 2) {
                width = '66%';
                label = 'متوسطة';
                color = '#e8a923';
            }

            if (passed === 3) {
                width = hasSymbol ? '100%' : '88%';
                label = hasSymbol ? 'ممتازة' : 'قوية';
                color = '#18a875';
            }

            bar.style.width = width;
            bar.style.background = color;
            text.textContent = label;
            text.style.color = color;
        };

        input.addEventListener('input', update);
        update();
    };

    const setupForgot = () => {
        const form = document.getElementById('forgotForm');
        if (!form) return;

        const email = document.getElementById('email');
        const error = document.getElementById('forgotError');
        const button = document.getElementById('forgotBtn');

        email?.addEventListener('input', () => setMessage(error, ''));

        form.addEventListener('submit', async event => {
            event.preventDefault();
            setMessage(error, '');

            if (!email?.validity.valid) {
                setMessage(error, 'أدخل بريداً إلكترونياً صحيحاً للمتابعة.');
                email?.focus();
                return;
            }

            const restore = setLoading(button, 'جاري إرسال الرمز...');

            try {
                const response = await fetch('/Account/ForgotPassword', {
                    method: 'POST',
                    body: new FormData(form),
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                const data = await response.json();

                if (data.success && data.url) {
                    button.innerHTML = '<i class="bi bi-check-circle-fill"></i><span>تم إرسال الطلب</span>';
                    window.setTimeout(() => window.location.assign(data.url), 650);
                    return;
                }

                setMessage(error, data.message || 'تعذر إرسال رمز الاستعادة حالياً.');
            } catch {
                setMessage(error, 'تعذر الاتصال بالخادم. تحقق من الإنترنت ثم حاول مرة أخرى.');
            } finally {
                restore();
            }
        });
    };

    const setupReset = () => {
        const form = document.getElementById('resetForm');
        if (!form) return;

        const email = document.getElementById('resetEmail');
        const code = document.getElementById('code');
        const otpScope = form.querySelector('[data-otp-cells]');
        const otp = setupOtpCells(otpScope, code);
        const password = document.getElementById('newPassword');
        const confirm = document.getElementById('confirmPassword');
        const error = document.getElementById('resetError');
        const button = document.getElementById('resetBtn');
        const resendButton = document.getElementById('resetResendBtn');
        const resendTimer = document.getElementById('resetResendTimer');
        const initialCooldown = Number(form.dataset.resendCooldown || 45);
        const countdown = createCountdown({
            button: resendButton,
            output: resendTimer,
            seconds: initialCooldown
        });

        setupPasswordMeter(password);

        [password, confirm].forEach(input => input?.addEventListener('input', () => setMessage(error, '')));
        otpScope?.addEventListener('input', () => setMessage(error, ''));

        resendButton?.addEventListener('click', async () => {
            if (resendButton.disabled) return;
            resendButton.disabled = true;
            resendButton.textContent = 'جاري الإرسال...';
            setMessage(error, '');

            const formData = new FormData();
            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) formData.append('__RequestVerificationToken', token);
            formData.append('email', email?.value || '');
            formData.append('type', 'reset');

            try {
                const response = await fetch('/Account/ResendOtp', {
                    method: 'POST',
                    body: formData,
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const data = await response.json();

                if (data.success) {
                    otp?.clear();
                    setMessage(error, data.message || 'تم إرسال رمز جديد.', 'success');
                    countdown.restart(data.retryAfter || initialCooldown);
                    return;
                }

                setMessage(error, data.message || 'تعذر إعادة إرسال الرمز.');
                countdown.restart(data.retryAfter || initialCooldown);
            } catch {
                setMessage(error, 'تعذر الاتصال بالخادم لإعادة إرسال الرمز.');
                countdown.restart(10);
            }
        });

        form.addEventListener('submit', async event => {
            event.preventDefault();
            setMessage(error, '');

            const otpValue = otp?.value || code?.value || '';
            if (!/^\d{6}$/.test(otpValue)) {
                setMessage(error, 'أدخل رمز التحقق المكون من 6 أرقام بالكامل.');
                otpScope?.querySelector('.otp-cell:not(.is-filled)')?.focus();
                return;
            }

            if (!PASSWORD_POLICY.test(password?.value || '')) {
                setMessage(error, 'كلمة المرور يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.');
                password?.focus();
                return;
            }

            if ((password?.value || '') !== (confirm?.value || '')) {
                setMessage(error, 'تأكيد كلمة المرور غير مطابق.');
                confirm?.focus();
                return;
            }

            const restore = setLoading(button, 'جاري حفظ كلمة المرور...');

            try {
                const response = await fetch('/Account/ResetPassword', {
                    method: 'POST',
                    body: new FormData(form),
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const data = await response.json();

                if (data.success && data.url) {
                    button.innerHTML = '<i class="bi bi-check-circle-fill"></i><span>تم الحفظ بنجاح</span>';
                    window.setTimeout(() => window.location.assign(data.url), 800);
                    return;
                }

                setMessage(error, data.message || 'تعذر تغيير كلمة المرور.');
                if (data.expired || data.attemptsExceeded) countdown.restart(1);
            } catch {
                setMessage(error, 'تعذر الاتصال بالخادم. حاول مرة أخرى.');
            } finally {
                restore();
            }
        });
    };

    ready(() => {
        setupPasswordToggles();
        setupForgot();
        setupReset();
    });
})();
