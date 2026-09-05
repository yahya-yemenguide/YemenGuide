/**
 * =============================================================
 * Login & Register Script - تفاعل الواجهات، إظهار كلمة المرور، والتحقق اللحظي والأمني
 * =============================================================
 */

document.addEventListener('DOMContentLoaded', () => {

    // عناصر تبديل الشاشات
    const container = document.querySelector('.container');
    const signUpLink = document.getElementById('signUpLink');
    const signInLink = document.getElementById('signInLink');

    // النماذج
    const loginForm = document.getElementById('ajaxLoginForm');
    const registerForm = document.getElementById('ajaxRegisterForm');

    // =============================================================
    // Google: حفظ مسار التسجيل بدون المساس بحركة Login/Register الحالية
    // =============================================================
    const registerGoogleButton = registerForm?.querySelector('.google-btn');
    const roleInputs = registerForm?.querySelectorAll('input[name="role"]') || [];

    const refreshRegisterGoogleUrl = () => {
        if (!registerGoogleButton) return;

        const selectedRole = registerForm?.querySelector('input[name="role"]:checked')?.value;
        const safeRole = selectedRole === 'Owner' ? 'Owner' : 'Customer';
        registerGoogleButton.href = `/Account/GoogleLogin?role=${encodeURIComponent(safeRole)}`;
    };

    roleInputs.forEach(input => input.addEventListener('change', refreshRegisterGoogleUrl));
    refreshRegisterGoogleUrl();

    // =============================================================
    // PREMIUM MOTION: طبقات 3D وإضاءة تفاعلية بدون تغيير حركة التبديل الأصلية
    // =============================================================
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)').matches;
    let interactionFrame = 0;
    let switchTimer = 0;

    const setPremiumPointer = (clientX, clientY) => {
        if (!container) return;

        const rect = container.getBoundingClientRect();
        if (!rect.width || !rect.height) return;

        const x = Math.max(0, Math.min(100, ((clientX - rect.left) / rect.width) * 100));
        const y = Math.max(0, Math.min(100, ((clientY - rect.top) / rect.height) * 100));

        container.style.setProperty('--premium-pointer-x', `${x.toFixed(1)}%`);
        container.style.setProperty('--premium-pointer-y', `${y.toFixed(1)}%`);
    };

    const resetPremiumPointer = () => {
        if (!container) return;
        container.style.setProperty('--premium-pointer-x', '50%');
        container.style.setProperty('--premium-pointer-y', '42%');
        container.classList.remove('is-interacting');
    };

    const playCinematicSwitch = () => {
        if (!container || reducedMotion) return;

        window.clearTimeout(switchTimer);
        container.classList.remove('is-switching');

        requestAnimationFrame(() => {
            container.classList.add('is-switching');
            switchTimer = window.setTimeout(() => {
                container.classList.remove('is-switching');
            }, 1950);
        });
    };

    if (container && !reducedMotion) {
        if (finePointer) {
            container.addEventListener('pointermove', (event) => {
                if (interactionFrame) return;
                interactionFrame = window.requestAnimationFrame(() => {
                    container.classList.add('is-interacting');
                    setPremiumPointer(event.clientX, event.clientY);
                    interactionFrame = 0;
                });
            }, { passive: true });

            container.addEventListener('pointerleave', resetPremiumPointer, { passive: true });
        }

        // الهاتف يأخذ نفس الإضاءة التفاعلية عبر اللمس، وليس نسخة ثابتة ناقصة.
        container.addEventListener('touchstart', (event) => {
            const touch = event.touches?.[0];
            if (!touch) return;
            container.classList.add('is-interacting');
            setPremiumPointer(touch.clientX, touch.clientY);
        }, { passive: true });

        container.addEventListener('touchmove', (event) => {
            const touch = event.touches?.[0];
            if (!touch || interactionFrame) return;
            interactionFrame = window.requestAnimationFrame(() => {
                setPremiumPointer(touch.clientX, touch.clientY);
                interactionFrame = 0;
            });
        }, { passive: true });

        container.addEventListener('touchend', () => {
            window.setTimeout(resetPremiumPointer, 240);
        }, { passive: true });
    }

    document.querySelectorAll('.btn').forEach(button => {
        button.addEventListener('touchstart', () => button.classList.add('is-touching'), { passive: true });
        button.addEventListener('touchend', () => {
            window.setTimeout(() => button.classList.remove('is-touching'), 620);
        }, { passive: true });
    });

    // عناصر إدخال إنشاء الحساب ونصوص الأخطاء
    const regEmail = document.getElementById('regEmail');
    const emailError = document.getElementById('emailError');
    const regUsername = document.getElementById('regUsername');
    const usernameError = document.getElementById('usernameError');
    const regPassword = document.getElementById('regPassword');

    // =============================================================
    // ✨ تجهيز عناصر التحقق المتقدم لكلمة المرور (Dynamic Injection)
    // =============================================================
    const passwordError = document.createElement('div');
    passwordError.className = 'error-text text-danger mt-1 fw-bold';
    passwordError.style.display = 'none';
    passwordError.style.fontSize = '0.78rem';
    passwordError.style.textAlign = 'right';
    passwordError.style.transition = 'all 0.3s ease';

    const passwordStrengthContainer = document.createElement('div');
    passwordStrengthContainer.className = 'mt-2 mb-1 d-flex align-items-center justify-content-between';
    passwordStrengthContainer.style.fontSize = '0.75rem';
    passwordStrengthContainer.style.display = 'none';

    const strengthText = document.createElement('span');
    strengthText.style.fontWeight = 'bold';
    strengthText.style.transition = 'color 0.3s ease';
    strengthText.innerHTML = 'قوة كلمة المرور: ';

    const strengthBarContainer = document.createElement('div');
    strengthBarContainer.style.width = '55%';
    strengthBarContainer.style.height = '4px';
    strengthBarContainer.style.backgroundColor = '#e9ecef';
    strengthBarContainer.style.borderRadius = '2px';
    strengthBarContainer.style.overflow = 'hidden';

    const strengthBar = document.createElement('div');
    strengthBar.style.height = '100%';
    strengthBar.style.width = '0%';
    strengthBar.style.transition = 'all 0.4s cubic-bezier(0.4, 0, 0.2, 1)';

    strengthBarContainer.appendChild(strengthBar);
    passwordStrengthContainer.appendChild(strengthText);
    passwordStrengthContainer.appendChild(strengthBarContainer);

    // 🛡️ الحل الهندسي لمنع التداخل: إدراج العناصر خارج الـ input-box تماماً
    if (regPassword) {
        const parentInputBox = regPassword.closest('.input-box');
        if (parentInputBox) {
            // إدراج المؤشر ورسالة الخطأ مباشرة بعد صندوق الإدخال بالكامل لمنع كركبة العين والأيقونات
            parentInputBox.parentNode.insertBefore(passwordStrengthContainer, parentInputBox.nextSibling);
            passwordStrengthContainer.parentNode.insertBefore(passwordError, passwordStrengthContainer.nextSibling);
        }
    }

    // عناصر رسائل الخطأ لتسجيل الدخول
    const loginErrorMessage = document.getElementById('loginErrorMessage');
    const loginErrorText = document.getElementById('errorText');

    // =============================================================
    //  1. تفعيل حركات التنقل والانتقال الأنيق بين اللوحات
    // =============================================================
    if (signUpLink && container) {
        signUpLink.addEventListener('click', (e) => {
            e.preventDefault();
            playCinematicSwitch();
            container.classList.add('active');
            if (loginErrorMessage) loginErrorMessage.style.display = 'none';
        });
    }

    if (signInLink && container) {
        signInLink.addEventListener('click', (e) => {
            e.preventDefault();
            playCinematicSwitch();
            container.classList.remove('active');
        });
    }

    // =============================================================
    //  2. 👁️ ميزة إظهار وإخفاء كلمة المرور (أيقونة العين)
    // =============================================================
    const togglePasswordIcons = document.querySelectorAll('.toggle-password');
    togglePasswordIcons.forEach(icon => {
        icon.addEventListener('click', function () {
            const input = this.parentElement.querySelector('input');
            if (input.type === 'password') {
                input.type = 'text';
                this.classList.remove('bi-eye-slash-fill');
                this.classList.add('bi-eye-fill');
                this.style.color = '#00A6DA';
            } else {
                input.type = 'password';
                this.classList.remove('bi-eye-fill');
                this.classList.add('bi-eye-slash-fill');
                this.style.color = '#999';
            }
        });
    });

    // =============================================================
    //  3. الاستماع الذكي والتحقق اللحظي
    // =============================================================
    if (regEmail) {
        regEmail.addEventListener('input', () => {
            emailError.style.opacity = '0';
            setTimeout(() => emailError.style.display = 'none', 300);
        });
    }

    if (regUsername) {
        regUsername.addEventListener('input', () => {
            usernameError.style.opacity = '0';
            setTimeout(() => usernameError.style.display = 'none', 300);
        });
    }

    // نظام تقييم قوة كلمة المرور الفخم والمستقل عن الحقل
    if (regPassword) {
        regPassword.addEventListener('input', () => {
            passwordError.style.display = 'none';
            const val = regPassword.value;

            if (val.length === 0) {
                passwordStrengthContainer.style.display = 'none';
                strengthBar.style.width = '0%';
                return;
            }

            passwordStrengthContainer.style.display = 'flex';
            let strength = 0;

            if (val.length >= 8) strength += 1;
            if (/[a-zA-Z]/.test(val)) strength += 1;
            if (/\d/.test(val)) strength += 1;
            if (/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]+/.test(val)) strength += 1;

            if (val.length < 6 || strength <= 1) {
                strengthBar.style.width = '33%';
                strengthBar.style.backgroundColor = '#dc3545';
                strengthText.innerHTML = '🔴 ضعيفة جداً';
                strengthText.style.color = '#dc3545';
            } else if (strength === 2 || (strength === 3 && val.length < 8)) {
                strengthBar.style.width = '66%';
                strengthBar.style.backgroundColor = '#ffc107';
                strengthText.innerHTML = '🟡 متوسطة (أضف رموزاً)';
                strengthText.style.color = '#d39e00';
            } else {
                strengthBar.style.width = '100%';
                strengthBar.style.backgroundColor = '#198754';
                strengthText.innerHTML = '🟢 قوية وممتازة!';
                strengthText.style.color = '#198754';
            }
        });
    }

    if (loginForm) {
        const loginInputs = loginForm.querySelectorAll('input');
        loginInputs.forEach(input => {
            input.addEventListener('input', () => {
                if (loginErrorMessage) loginErrorMessage.style.display = 'none';
            });
        });
    }

    // =============================================================
    //  4. معالجة تسجيل الدخول (مع التوجيه الصامت والمباشر للـ OTP)
    // =============================================================
    if (loginForm) {
        loginForm.addEventListener('submit', (e) => {
            e.preventDefault();

            let submitBtn = loginForm.querySelector('button[type="submit"]');
            let originalText = submitBtn.innerHTML;

            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> جاري التحقق...';
            submitBtn.disabled = true;
            if (loginErrorMessage) loginErrorMessage.style.display = 'none';

            const formData = new FormData(loginForm);

            fetch('/Account/Login', {
                method: 'POST',
                body: formData
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        window.location.href = data.url;
                    } else {
                        // 🌟 التوجيه الصامت والمباشر لشاشة التحقق دون أي رسائل منبثقة وسطية
                        if (data.isEmailUnverified) {
                            window.location.href = `/Account/VerifyOtp?email=${data.email}`;
                            return;
                        }

                        if (loginErrorMessage && loginErrorText) {
                            loginErrorText.innerText = data.message;
                            loginErrorMessage.style.display = 'block';
                        } else {
                            Swal.fire({ icon: 'error', title: 'عفواً!', text: data.message, confirmButtonColor: '#05203C' });
                        }
                        submitBtn.innerHTML = originalText;
                        submitBtn.disabled = false;
                    }
                })
                .catch(error => {
                    Swal.fire({ icon: 'error', title: 'خطأ في الاتصال!', text: 'تعذر الوصول للسيرفر.', confirmButtonColor: '#05203C' });
                    submitBtn.innerHTML = originalText;
                    submitBtn.disabled = false;
                });
        });
    }

    // =============================================================
    //  5. معالجة إنشاء الحساب المطور (Client Validations & AJAX)
    // =============================================================
    if (registerForm) {
        registerForm.addEventListener('submit', (e) => {
            e.preventDefault();

            // تصفير وعمل Reset كامل للأخطاء قبل الفحص الجديد
            emailError.style.display = 'none';
            emailError.style.opacity = '0';
            usernameError.style.display = 'none';
            usernameError.style.opacity = '0';
            passwordError.style.display = 'none';

            let isValid = true;

            // 🛡️ 1. فحص صيغة اسم المستخدم وعرض التعليمات والمثال بدقة
            const usernameRegex = /^[a-zA-Z0-9_]{3,20}$/;
            if (!usernameRegex.test(regUsername.value?.trim())) {
                usernameError.innerHTML = `<i class="bi bi-info-circle-fill me-1"></i> يرجى استخدام أحرف إنجليزية وأرقام فقط، بدون مسافات (مثال: Ali_2026).`;
                usernameError.style.display = 'block';
                usernameError.style.height = 'auto';
                usernameError.style.opacity = '1';
                const usernameBox = regUsername.closest('.input-box');
                usernameBox?.classList.remove('is-invalid-premium');
                void usernameBox?.offsetWidth;
                usernameBox?.classList.add('is-invalid-premium');
                window.setTimeout(() => usernameBox?.classList.remove('is-invalid-premium'), 420);
                regUsername.focus();
                isValid = false;
            }

            // 🛡️ 2. الفحص الصارم لقوة كلمة المرور
            const passwordRegex = /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;
            if (!passwordRegex.test(regPassword.value)) {
                passwordError.innerHTML = `<i class="bi bi-shield-exclamation me-1"></i> لضمان أمانك، يجب أن تتكون كلمة المرور من 8 أحرف على الأقل، وتحتوي على حرف إنجليزي واحد ورقم واحد.`;
                passwordError.style.display = 'block';
                const passwordBox = regPassword.closest('.input-box');
                passwordBox?.classList.remove('is-invalid-premium');
                void passwordBox?.offsetWidth;
                passwordBox?.classList.add('is-invalid-premium');
                window.setTimeout(() => passwordBox?.classList.remove('is-invalid-premium'), 420);
                if (isValid) regPassword.focus();
                isValid = false;
            }

            if (!isValid) {
                return;
            }

            let submitBtn = registerForm.querySelector('button[type="submit"]');
            let originalText = submitBtn.innerHTML;

            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> جاري تهيئة الحساب...';
            submitBtn.disabled = true;

            const formData = new FormData(registerForm);

            fetch('/Account/Register', {
                method: 'POST',
                body: formData
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        // الانتقال المباشر والسلس إلى رابط التحقق OTP دون إظهار أي نوافذ منبثقة مخربة لتدفق العملية
                        window.location.href = data.url;
                    } else {
                        // إظهار وتوجيه الأخطاء القادمة من السيرفر (مثل التحقق من التكرار اللحظي)
                        if (data.message.includes("البريد الإلكتروني")) {
                            emailError.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-1"></i> ${data.message}`;
                            emailError.style.display = 'block';
                            emailError.style.height = 'auto';
                            emailError.style.opacity = '1';
                            regEmail.focus();
                        }
                        else if (data.message.includes("اسم المستخدم")) {
                            usernameError.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-1"></i> ${data.message}`;
                            usernameError.style.display = 'block';
                            usernameError.style.height = 'auto';
                            usernameError.style.opacity = '1';
                            regUsername.focus();
                        }
                        else {
                            Swal.fire({ icon: 'warning', title: 'تنبيه من السيرفر!', text: data.message, confirmButtonColor: '#00A6DA' });
                        }
                        submitBtn.innerHTML = originalText;
                        submitBtn.disabled = false;
                    }
                })
                .catch(error => {
                    Swal.fire({ icon: 'error', title: 'خطأ فني!', text: 'تعذر معالجة طلب التسجيل حالياً، يرجى إعادة المحاولة.', confirmButtonColor: '#05203C' });
                    submitBtn.innerHTML = originalText;
                    submitBtn.disabled = false;
                });
        });
    }
});
