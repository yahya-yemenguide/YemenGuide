/**
 * =============================================================
 * YEMENGUIDE VERIFY OTP V2
 * 6 cells + paste + resend + backend cooldown support
 * =============================================================
 */

(() => {
    'use strict';

    const ready = callback => {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
        } else {
            callback();
        }
    };

    const formatSeconds = total => {
        const safe = Math.max(0, Math.floor(Number(total) || 0));
        const minutes = String(Math.floor(safe / 60)).padStart(2, '0');
        const seconds = String(safe % 60).padStart(2, '0');
        return `${minutes}:${seconds}`;
    };

    const setMessage = (element, message, kind = 'error') => {
        if (!element) return;
        if (!message) {
            element.hidden = true;
            element.textContent = '';
            element.classList.remove('otp-message--success');
            element.classList.add('otp-message--error');
            return;
        }

        element.hidden = false;
        element.classList.toggle('otp-message--success', kind === 'success');
        element.classList.toggle('otp-message--error', kind !== 'success');
        element.textContent = message;
    };

    const setLoading = (button, text) => {
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

    ready(() => {
        const form = document.getElementById('verifyForm');
        const codeInput = document.getElementById('code');
        const cellsContainer = document.getElementById('otpCells');
        const cells = Array.from(cellsContainer?.querySelectorAll('.otp-cell') || []);
        const submitBtn = document.getElementById('submitBtn');
        const error = document.getElementById('errorMessage');
        const resendBtn = document.getElementById('resendOtpBtn');
        const resendTimer = document.getElementById('resendOtpTimer');
        const expiryTimer = document.getElementById('otpExpiryTimer');
        const validity = document.getElementById('otpValidity');
        const email = document.getElementById('email');
        const type = document.getElementById('type');

        if (!form || !codeInput || !cells.length || !submitBtn) return;

        const syncCode = () => {
            codeInput.value = cells.map(cell => cell.value).join('');
            cells.forEach(cell => cell.classList.toggle('is-filled', Boolean(cell.value)));
            return codeInput.value;
        };

        const clearCellErrors = () => cells.forEach(cell => cell.classList.remove('is-error'));
        const markCellErrors = () => cells.forEach(cell => cell.classList.add('is-error'));

        const distribute = (value, startIndex = 0) => {
            const digits = String(value || '').replace(/\D/g, '').slice(0, cells.length - startIndex);
            digits.split('').forEach((digit, offset) => {
                if (cells[startIndex + offset]) cells[startIndex + offset].value = digit;
            });
            syncCode();
            const next = Math.min(startIndex + digits.length, cells.length - 1);
            cells[next]?.focus();
        };

        cells.forEach((cell, index) => {
            cell.addEventListener('input', event => {
                clearCellErrors();
                setMessage(error, '');

                const digits = event.target.value.replace(/\D/g, '');
                if (digits.length > 1) {
                    event.target.value = '';
                    distribute(digits, index);
                    return;
                }

                event.target.value = digits.slice(-1);
                syncCode();
                if (event.target.value && index < cells.length - 1) cells[index + 1].focus();
            });

            cell.addEventListener('keydown', event => {
                if (event.key === 'Backspace' && !cell.value && index > 0) {
                    cells[index - 1].focus();
                    cells[index - 1].value = '';
                    syncCode();
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
                cells.forEach(item => { item.value = ''; });
                distribute(digits, 0);
            });
        });

        let countdownId = 0;
        let expiryCountdownId = 0;
        let codeExpired = false;
        const initialCooldown = Number(form.dataset.resendCooldown || 45);
        const initialExpirySeconds = Number(form.dataset.expirySeconds || 120);

        const startCountdown = seconds => {
            window.clearInterval(countdownId);
            const parsedSeconds = Number(seconds);
            let remaining = Number.isFinite(parsedSeconds)
                ? Math.max(0, Math.floor(parsedSeconds))
                : Math.max(0, Math.floor(initialCooldown));

            const render = () => {
                if (remaining <= 0) {
                    window.clearInterval(countdownId);
                    resendBtn.disabled = false;
                    resendBtn.textContent = 'إعادة إرسال الرمز';
                    if (resendTimer) resendTimer.textContent = '00:00';
                    return;
                }

                resendBtn.disabled = true;
                const formatted = formatSeconds(remaining);
                resendBtn.innerHTML = `إعادة الإرسال بعد <strong>${formatted}</strong>`;
                if (resendTimer) resendTimer.textContent = formatted;
                remaining -= 1;
            };

            render();
            countdownId = window.setInterval(render, 1000);
        };

        // عداد صلاحية OTP الحقيقي: يعتمد على CodeExpiry القادم من السيرفر،
        // لذلك Refresh لا يعيد الدقيقتين من البداية.
        const startExpiryCountdown = seconds => {
            window.clearInterval(expiryCountdownId);
            let remaining = Math.max(0, Number(seconds) || 0);

            const render = () => {
                const formatted = formatSeconds(remaining);
                if (expiryTimer) expiryTimer.textContent = formatted;

                if (remaining <= 0) {
                    window.clearInterval(expiryCountdownId);
                    codeExpired = true;
                    validity?.classList.add('is-expired');
                    submitBtn.disabled = true;
                    setMessage(error, 'انتهت صلاحية رمز التحقق. اضغط إعادة إرسال الرمز للحصول على رمز جديد.');
                    return;
                }

                codeExpired = false;
                validity?.classList.remove('is-expired');
                submitBtn.disabled = false;
                remaining -= 1;
            };

            render();
            if (!codeExpired) expiryCountdownId = window.setInterval(render, 1000);
        };

        resendBtn?.addEventListener('click', async () => {
            if (resendBtn.disabled) return;
            resendBtn.disabled = true;
            resendBtn.textContent = 'جاري إرسال رمز جديد...';
            setMessage(error, '');
            clearCellErrors();

            const formData = new FormData();
            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) formData.append('__RequestVerificationToken', token);
            formData.append('email', email?.value || '');
            if (type?.value) formData.append('type', type.value);

            try {
                const response = await fetch('/Account/ResendOtp', {
                    method: 'POST',
                    body: formData,
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const data = await response.json();

                if (data.success) {
                    cells.forEach(cell => { cell.value = ''; });
                    syncCode();
                    cells[0]?.focus();
                    setMessage(error, data.message || 'تم إرسال رمز جديد.', 'success');
                    startCountdown(data.retryAfter || initialCooldown);
                    startExpiryCountdown(data.expiresIn || 120);
                    return;
                }

                if (data.alreadyVerified) {
                    window.location.assign('/Account/Login');
                    return;
                }

                setMessage(error, data.message || 'تعذر إعادة إرسال الرمز.');
                startCountdown(data.retryAfter || initialCooldown);
            } catch {
                setMessage(error, 'تعذر الاتصال بالخادم لإعادة إرسال الرمز.');
                startCountdown(10);
            }
        });

        form.addEventListener('submit', async event => {
            event.preventDefault();
            clearCellErrors();
            setMessage(error, '');

            if (codeExpired) {
                setMessage(error, 'انتهت صلاحية رمز التحقق. أعد إرسال رمز جديد أولاً.');
                return;
            }

            const code = syncCode();
            if (!/^\d{6}$/.test(code)) {
                setMessage(error, 'أدخل رمز التحقق المكون من 6 أرقام بالكامل.');
                markCellErrors();
                cells.find(cell => !cell.value)?.focus();
                return;
            }

            const restore = setLoading(submitBtn, 'جاري التحقق...');
            let completed = false;

            try {
                const response = await fetch('/Account/VerifyOtp', {
                    method: 'POST',
                    body: new FormData(form),
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const data = await response.json();

                if (data.success && data.url) {
                    completed = true;
                    submitBtn.disabled = true;
                    submitBtn.innerHTML = '<i class="bi bi-check-circle-fill"></i><span>تم التأكيد بنجاح</span>';
                    window.setTimeout(() => window.location.assign(data.url), 700);
                    return;
                }

                if (data.alreadyVerified) {
                    window.location.assign('/Account/Login');
                    return;
                }

                setMessage(error, data.message || 'رمز التحقق غير صحيح.');
                markCellErrors();
                if (data.expired) {
                    startExpiryCountdown(0);
                }
                if (data.attemptsExceeded) {
                    startCountdown(1);
                }
            } catch {
                setMessage(error, 'تعذر الاتصال بالخادم. تحقق من الإنترنت ثم حاول مرة أخرى.');
            } finally {
                if (!completed) restore();
                if (codeExpired) submitBtn.disabled = true;
            }
        });

        startCountdown(initialCooldown);
        startExpiryCountdown(initialExpirySeconds);
        window.setTimeout(() => cells[0]?.focus({ preventScroll: true }), 260);
    });
})();
