/**
 * =============================================================
 * Owner Profile Interactions - النسخة الفخمة والنهائية 🏢💎
 * (التحقق اللحظي، التقويم الذكي، مكتبة الهاتف العالمية، وتفضيلات المالك)
 * =============================================================
 */

document.addEventListener('DOMContentLoaded', function () {

    // =============================================================
    // 1. إدارة الصورة الشخصية (الطفو، المودال، والرفع)
    // =============================================================
    const avatarModalElement = document.getElementById('avatarManageModal');
    const avatarModal = avatarModalElement ? new bootstrap.Modal(avatarModalElement) : null;
    const mainAvatarPreview = document.getElementById('avatarPreview');
    const modalAvatarPreview = document.getElementById('modalAvatarPreview');
    const fileInput = document.getElementById('profileImageUpload');
    const cameraBtn = document.getElementById('uploadCameraBtn');
    const modalUploadBtn = document.getElementById('modalUploadBtn');
    const modalDeleteBtn = document.getElementById('modalDeleteBtn');

    // إيقاف الطفو بنعومة عند وضع الماوس لسهولة النقر
    if (mainAvatarPreview) {
        mainAvatarPreview.addEventListener('mouseenter', function () {
            this.style.animationPlayState = 'paused';
        });

        mainAvatarPreview.addEventListener('mouseleave', function () {
            this.style.animationPlayState = 'running';
        });

        mainAvatarPreview.addEventListener('click', function (e) {
            if (e.target.closest('#uploadCameraBtn')) return;

            if (avatarModal) {
                avatarModal.show();
            }
        });
    }

    [cameraBtn, modalUploadBtn].forEach(btn => {
        if (btn && fileInput) {
            btn.addEventListener('click', () => fileInput.click());
        }
    });

    if (fileInput) {
        fileInput.addEventListener('change', function (e) {
            const file = e.target.files[0];

            if (file) {
                const reader = new FileReader();

                reader.onload = function (ev) {
                    const imgUrl = ev.target.result;

                    [mainAvatarPreview, modalAvatarPreview].forEach(el => {
                        if (el) {
                            el.style.backgroundImage = `url(${imgUrl})`;
                            el.style.background = `url(${imgUrl}) center/cover no-repeat`;

                            const initialSpan = el.querySelector(
                                '.avatar-initial, #modalAvatarInitial'
                            );

                            if (initialSpan) {
                                initialSpan.style.display = 'none';
                            }
                        }
                    });

                    if (modalDeleteBtn) {
                        modalDeleteBtn.style.display = 'block';
                    }

                    uploadProfileImage(file);
                };

                reader.readAsDataURL(file);
            }
        });
    }

    function uploadProfileImage(file) {
        const formData = new FormData();
        formData.append('profileImage', file);

        fetch('/Owner/Profile/UploadProfileImage', {
            method: 'POST',
            body: formData
        })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    Swal.fire({
                        icon: 'success',
                        title: 'رائع!',
                        text: 'تم تحديث صورتك الشخصية بنجاح',
                        timer: 1800,
                        showConfirmButton: false
                    });

                    if (avatarModal) {
                        avatarModal.hide();
                    }
                } else {
                    Swal.fire('عذراً', data.message, 'error');
                }
            })
            .catch(() => {
                Swal.fire(
                    'خطأ في الاتصال',
                    'لم نتمكن من رفع الصورة للسيرفر',
                    'error'
                );
            });
    }

    if (modalDeleteBtn) {
        modalDeleteBtn.addEventListener('click', function () {
            Swal.fire({
                title: 'هل أنت متأكد؟',
                text: 'سيتم حذف صورتك الشخصية واستعادة الحرف الافتراضي!',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#dc3545',
                cancelButtonColor: '#6c757d',
                confirmButtonText: 'نعم، احذفها',
                cancelButtonText: 'إلغاء'
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch('/Owner/Profile/DeleteImage', {
                        method: 'POST'
                    })
                        .then(res => res.json())
                        .then(data => {
                            if (data.success) {
                                [mainAvatarPreview, modalAvatarPreview].forEach(el => {
                                    if (el) {
                                        el.style.backgroundImage = 'none';
                                        el.style.background =
                                            'linear-gradient(135deg, #05203C 0%, #00A6DA 100%)';
                                    }
                                });

                                if (mainAvatarPreview) {
                                    mainAvatarPreview.innerHTML = `
                                        <span class="avatar-initial text-white fw-bold display-4"
                                              id="mainAvatarInitial">
                                            ${data.initialLetter}
                                        </span>

                                        <button class="camera-icon btn btn-white btn-sm rounded-circle position-absolute bottom-0 end-0 shadow-sm border d-flex align-items-center justify-content-center"
                                                id="uploadCameraBtn"
                                                type="button"
                                                style="width: 38px; height: 38px; background: white; z-index: 5;">

                                            <i class="bi bi-camera-fill text-primary fs-5"></i>
                                        </button>`;
                                }

                                if (modalAvatarPreview) {
                                    modalAvatarPreview.innerHTML = `
                                        <div class="d-flex align-items-center justify-content-center h-100"
                                             id="modalInitialContainer">

                                            <span class="text-white fw-bold display-3"
                                                  id="modalAvatarInitial">
                                                ${data.initialLetter}
                                            </span>
                                        </div>`;
                                }

                                modalDeleteBtn.style.display = 'none';

                                if (avatarModal) {
                                    avatarModal.hide();
                                }

                                Swal.fire({
                                    icon: 'success',
                                    title: 'تم الحذف!',
                                    text: 'تمت إزالة الصورة بنجاح.',
                                    timer: 1800,
                                    showConfirmButton: false
                                });
                            }
                        });
                }
            });
        });
    }


    // =============================================================
    // 2. تحديث البيانات الشخصية
    // مكتبة الهاتف والتحقق اللحظي
    // =============================================================

    document.querySelectorAll('.edit-icon').forEach(btn => {
        btn.addEventListener('click', function () {
            const targetId = this.getAttribute('data-target');
            const input = document.getElementById(targetId);

            if (input) {
                input.focus();

                const val = input.value;
                input.value = '';
                input.value = val;
            }
        });
    });


    // =============================================================
    // تهيئة مكتبة الأرقام الدولية للمالك
    // =============================================================

    const phoneInput = document.getElementById('phoneNumber');
    let iti;

    if (phoneInput) {
        iti = window.intlTelInput(phoneInput, {
            initialCountry: 'ye',
            preferredCountries: ['ye', 'sa', 'ae', 'eg', 'qa'],
            separateDialCode: true,
            formatOnDisplay: true,
            autoPlaceholder: 'polite',
            utilsScript:
                'https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js'
        });

        // التحقق اللحظي لرقم الهاتف
        phoneInput.addEventListener('input', function () {
            this.value = this.value.replace(/[^0-9\s+]/g, '');

            const phoneError =
                document.getElementById('profilePhoneError');

            const wrapper =
                this.closest('.phone-wrapper-ltr');

            if (phoneError) {
                phoneError.style.display = 'none';
            }

            if (this.value.trim() === '') {
                if (wrapper) {
                    wrapper.classList.remove(
                        'glow-error',
                        'glow-success'
                    );
                }

                return;
            }

            if (iti.isValidNumber()) {
                if (wrapper) {
                    wrapper.classList.remove('glow-error');
                    wrapper.classList.add('glow-success');
                }
            } else {
                if (wrapper) {
                    wrapper.classList.remove('glow-success');
                    wrapper.classList.add('glow-error');
                }
            }
        });
    }


    // =============================================================
    // عناصر التحقق للبريد واسم المستخدم
    // =============================================================

    const usernameInput =
        document.getElementById('username');

    const emailInput =
        document.getElementById('contactInfo');

    const userError =
        document.getElementById('profileUsernameError');

    const emailError =
        document.getElementById('profileEmailError');

    const phoneError =
        document.getElementById('profilePhoneError');

    const emailWarningDiv =
        document.getElementById('emailVerificationWarning');

    const inlineVerifyEmailBtn =
        document.getElementById('inlineVerifyEmailBtn');

    const emailVerifiedBadge =
        document.getElementById('emailVerifiedBadge');

    const originalEmail =
        emailInput ? emailInput.value.trim() : '';

    const wasOriginallyVerified =
        emailVerifiedBadge &&
        !emailVerifiedBadge.classList.contains('d-none');


    // =============================================================
    // التحقق اللحظي لاسم المستخدم
    // =============================================================

    if (usernameInput) {
        usernameInput.addEventListener('input', function () {
            if (userError) {
                userError.style.display = 'none';
            }

            const wrapper =
                this.closest('.input-group');

            const val =
                this.value.trim();

            if (val === '') {
                if (wrapper) {
                    wrapper.classList.remove(
                        'glow-error',
                        'glow-success'
                    );
                }

                return;
            }

            const usernameRegex =
                /^[a-zA-Z0-9_]{3,20}$/;

            if (usernameRegex.test(val)) {
                if (wrapper) {
                    wrapper.classList.remove('glow-error');
                    wrapper.classList.add('glow-success');
                }
            } else {
                if (wrapper) {
                    wrapper.classList.remove('glow-success');
                    wrapper.classList.add('glow-error');
                }
            }
        });
    }


    // =============================================================
    // التحقق اللحظي للبريد الإلكتروني
    // =============================================================

    if (emailInput) {
        emailInput.addEventListener('input', function () {
            if (emailError) {
                emailError.style.display = 'none';
            }

            const wrapper =
                this.closest('.input-group');

            const currentEmail =
                this.value.trim();

            if (currentEmail !== originalEmail) {
                if (emailVerifiedBadge) {
                    emailVerifiedBadge.classList.add('d-none');
                }

                if (emailWarningDiv) {
                    emailWarningDiv.classList.remove('d-none');
                }
            } else {
                if (emailWarningDiv) {
                    emailWarningDiv.classList.add('d-none');
                }

                if (
                    wasOriginallyVerified &&
                    emailVerifiedBadge
                ) {
                    emailVerifiedBadge.classList.remove('d-none');
                }
            }

            if (currentEmail === '') {
                if (wrapper) {
                    wrapper.classList.remove(
                        'glow-error',
                        'glow-success'
                    );
                }

                return;
            }

            const emailRegex =
                /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

            if (emailRegex.test(currentEmail)) {
                if (wrapper) {
                    wrapper.classList.remove('glow-error');
                    wrapper.classList.add('glow-success');
                }
            } else {
                if (wrapper) {
                    wrapper.classList.remove('glow-success');
                    wrapper.classList.add('glow-error');
                }
            }
        });
    }


    // =============================================================
    // 3. فتح التقويم من أي مكان في الحقل
    // =============================================================

    const dateInput =
        document.getElementById('dateOfBirth');

    if (dateInput) {
        const dateWrapper =
            dateInput.closest('.input-group');

        if (dateWrapper) {
            dateWrapper.addEventListener('click', function (e) {
                if (e.target !== dateInput) {
                    try {
                        dateInput.showPicker();
                    } catch (err) {
                        dateInput.focus();
                    }
                }
            });
        }

        dateInput.addEventListener('click', function () {
            try {
                this.showPicker();
            } catch (err) {
                // المتصفح لا يدعم showPicker
            }
        });
    }


    // =============================================================
    // 4. طلب توثيق البريد الإلكتروني للمالك
    // =============================================================

    if (inlineVerifyEmailBtn) {
        inlineVerifyEmailBtn.addEventListener('click', function () {
            const newEmail =
                emailInput.value.trim();

            const emailRegex =
                /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

            if (!emailRegex.test(newEmail)) {
                if (emailError) {
                    emailError.innerHTML = `
                        <i class="bi bi-info-circle-fill me-1"></i>
                        يرجى إدخال بريد إلكتروني صحيح أولاً.`;

                    emailError.style.display = 'block';
                }

                emailInput.focus();
                return;
            }

            const btnOriginalHtml =
                this.innerHTML;

            this.disabled = true;

            this.innerHTML = `
                <span class="spinner-border spinner-border-sm me-1"></span>
                جاري...`;

            fetch('/Owner/Profile/RequestEmailChange', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    NewEmail: newEmail
                })
            })
                .then(res => res.json())
                .then(data => {
                    this.disabled = false;
                    this.innerHTML = btnOriginalHtml;

                    if (data.success) {
                        Swal.fire({
                            icon: 'info',
                            title: 'خطوة أخيرة',
                            text: 'تم إرسال رمز التحقق. سيتم توجيهك الآن لإدخاله...',
                            timer: 2500,
                            showConfirmButton: false
                        }).then(() => {
                            window.location.href =
                                `/Account/VerifyOtp?email=${encodeURIComponent(newEmail)}&type=update`;
                        });
                    } else {
                        Swal.fire(
                            'عذراً',
                            data.message,
                            'error'
                        );
                    }
                })
                .catch(() => {
                    this.disabled = false;
                    this.innerHTML = btnOriginalHtml;

                    Swal.fire(
                        'خطأ',
                        'فشل الاتصال بالخادم.',
                        'error'
                    );
                });
        });
    }


    // =============================================================
    // 5. حفظ البيانات الشخصية
    // =============================================================

    const personalForm =
        document.getElementById('personalInfoForm');

    if (personalForm) {
        personalForm.addEventListener('submit', function (e) {
            e.preventDefault();

            if (userError) {
                userError.style.display = 'none';
            }

            if (emailError) {
                emailError.style.display = 'none';
            }

            if (phoneError) {
                phoneError.style.display = 'none';
            }

            let hasError = false;

            const usernameVal =
                usernameInput.value.trim();

            const usernameRegex =
                /^[a-zA-Z0-9_]{3,20}$/;

            if (!usernameRegex.test(usernameVal)) {
                if (userError) {
                    userError.innerHTML = `
                        <i class="bi bi-info-circle-fill me-1"></i>
                        يرجى استخدام أحرف إنجليزية وأرقام فقط.`;

                    userError.style.display = 'block';
                }

                const usernameWrapper =
                    usernameInput.closest('.input-group');

                if (usernameWrapper) {
                    usernameWrapper.classList.add('glow-error');
                }

                hasError = true;
            }

            const emailVal =
                emailInput.value.trim();

            const emailRegex =
                /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

            if (!emailRegex.test(emailVal)) {
                if (emailError) {
                    emailError.innerHTML = `
                        <i class="bi bi-info-circle-fill me-1"></i>
                        يرجى إدخال بريد إلكتروني صحيح.`;

                    emailError.style.display = 'block';
                }

                const emailWrapper =
                    emailInput.closest('.input-group');

                if (emailWrapper) {
                    emailWrapper.classList.add('glow-error');
                }

                hasError = true;
            }

            if (iti && !iti.isValidNumber()) {
                if (phoneError) {
                    phoneError.innerHTML = `
                        <i class="bi bi-info-circle-fill me-1"></i>
                        يرجى إدخال رقم هاتف صحيح يطابق الدولة المختارة.`;

                    phoneError.style.display = 'block';
                }

                const phoneWrapper =
                    phoneInput.closest('.phone-wrapper-ltr');

                if (phoneWrapper) {
                    phoneWrapper.classList.add('glow-error');
                }

                hasError = true;
            }

            if (hasError) {
                return;
            }

            if (emailVal !== originalEmail) {
                if (emailWarningDiv) {
                    emailWarningDiv.classList.remove('d-none');

                    emailWarningDiv.scrollIntoView({
                        behavior: 'smooth',
                        block: 'center'
                    });

                    if (inlineVerifyEmailBtn) {
                        inlineVerifyEmailBtn.classList.add(
                            'btn-warning'
                        );

                        inlineVerifyEmailBtn.classList.remove(
                            'btn-danger'
                        );

                        setTimeout(() => {
                            inlineVerifyEmailBtn.classList.add(
                                'btn-danger'
                            );

                            inlineVerifyEmailBtn.classList.remove(
                                'btn-warning'
                            );
                        }, 500);
                    }
                }

                return;
            }

            showLoadingSpinner(
                personalForm,
                'جاري الحفظ...'
            );

            const fullInternationalPhone =
                iti
                    ? iti.getNumber()
                    : phoneInput.value.trim();

            const formData = {
                FullName:
                    document.getElementById('fullName').value,

                Username:
                    usernameVal,

                ContactInfo:
                    emailVal,

                PhoneNumber:
                    fullInternationalPhone,

                City:
                    document.getElementById('city').value,

                Gender:
                    document.getElementById('gender').value,

                DateOfBirth:
                    document.getElementById('dateOfBirth').value || null,

                Bio:
                    document.getElementById('bio').value,

                FacebookUrl:
                    document.getElementById('facebookUrl').value,

                TwitterUrl:
                    document.getElementById('twitterUrl').value,

                InstagramUrl:
                    document.getElementById('instagramUrl').value
            };

            fetch('/Owner/Profile/UpdatePersonalInfo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(formData)
            })
                .then(res => res.json())
                .then(data => {
                    hideLoadingSpinner(
                        personalForm,
                        '<i class="bi bi-floppy-fill me-2"></i> حفظ التحديثات'
                    );

                    if (data.success) {
                        const profileName =
                            document.querySelector('.profile-name');

                        if (profileName) {
                            profileName.textContent =
                                formData.FullName;
                        }

                        document
                            .querySelectorAll('.glow-success')
                            .forEach(el => {
                                el.classList.remove('glow-success');
                            });

                        Swal.fire({
                            icon: 'success',
                            title: 'تم الحفظ',
                            text: 'تم تحديث بياناتك الشخصية بنجاح',
                            timer: 2000,
                            showConfirmButton: false
                        });
                    } else {
                        if (
                            data.message &&
                            data.message.includes('اسم المستخدم') &&
                            userError
                        ) {
                            userError.innerHTML = `
                                <i class="bi bi-exclamation-triangle-fill me-1"></i>
                                ${data.message}`;

                            userError.style.display = 'block';

                            const usernameWrapper =
                                usernameInput.closest('.input-group');

                            if (usernameWrapper) {
                                usernameWrapper.classList.add(
                                    'glow-error'
                                );
                            }

                            usernameInput.focus();
                        } else {
                            Swal.fire(
                                'خطأ',
                                data.message,
                                'error'
                            );
                        }
                    }
                })
                .catch(() => {
                    hideLoadingSpinner(
                        personalForm,
                        '<i class="bi bi-floppy-fill me-2"></i> حفظ التحديثات'
                    );

                    Swal.fire(
                        'خطأ في الاتصال',
                        'فشل إرسال التحديثات إلى الخادم',
                        'error'
                    );
                });
        });
    }


    // =============================================================
    // 6. الأمان وكلمات المرور
    // =============================================================

    document.querySelectorAll('.toggle-password').forEach(btn => {
        btn.addEventListener('click', function () {
            const input =
                document.getElementById(this.dataset.target);

            if (!input) {
                return;
            }

            if (input.type === 'password') {
                input.type = 'text';

                this.innerHTML =
                    '<i class="bi bi-eye text-primary"></i>';
            } else {
                input.type = 'password';

                this.innerHTML =
                    '<i class="bi bi-eye-slash"></i>';
            }
        });
    });

    const newPassword =
        document.getElementById('newPassword');

    const pwdError =
        document.getElementById('profilePasswordError');

    const strengthBar =
        document.querySelector('.strength-bar');

    const strengthText =
        document.querySelector('.strength-text');

    if (
        newPassword &&
        strengthBar &&
        strengthText
    ) {
        newPassword.addEventListener('input', function () {
            if (pwdError) {
                pwdError.style.display = 'none';
            }

            const val =
                this.value;

            let strength = 0;

            if (val.length >= 8) {
                strength++;
            }

            if (/[a-zA-Z]/.test(val)) {
                strength++;
            }

            if (/\d/.test(val)) {
                strength++;
            }

            if (
                /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]+/.test(val)
            ) {
                strength++;
            }

            const percent =
                val.length === 0
                    ? 0
                    : (strength / 4) * 100;

            strengthBar.style.width =
                percent + '%';

            if (val.length === 0) {
                strengthBar.style.backgroundColor =
                    'transparent';

                strengthText.innerText = '';
            } else if (strength <= 1) {
                strengthBar.style.backgroundColor =
                    '#dc3545';

                strengthText.innerText =
                    'كلمة مرور ضعيفة';

                strengthText.className =
                    'strength-text d-block mt-1 fw-bold text-danger';
            } else if (strength === 2) {
                strengthBar.style.backgroundColor =
                    '#ffc107';

                strengthText.innerText =
                    'كلمة مرور متوسطة';

                strengthText.className =
                    'strength-text d-block mt-1 fw-bold text-warning';
            } else {
                strengthBar.style.backgroundColor =
                    '#198754';

                strengthText.innerText =
                    'كلمة مرور قوية وممتازة';

                strengthText.className =
                    'strength-text d-block mt-1 fw-bold text-success';
            }
        });
    }

    const passwordForm =
        document.getElementById('passwordForm');

    if (passwordForm) {
        passwordForm.addEventListener('submit', function (e) {
            e.preventDefault();

            if (pwdError) {
                pwdError.style.display = 'none';
            }

            const newPwd =
                newPassword.value;

            const passwordRegex =
                /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;

            if (!passwordRegex.test(newPwd)) {
                if (pwdError) {
                    pwdError.innerHTML = `
                        <i class="bi bi-shield-exclamation me-1"></i>
                        يجب أن تتكون من 8 أحرف على الأقل،
                        وتحتوي على حرف إنجليزي ورقم.`;

                    pwdError.style.display = 'block';
                }

                newPassword.focus();
                return;
            }

            const current =
                document.getElementById(
                    'currentPassword'
                ).value;

            const confirm =
                document.getElementById(
                    'confirmPassword'
                ).value;

            if (newPwd !== confirm) {
                return Swal.fire(
                    'خطأ',
                    'كلمتا المرور الجديدتان غير متطابقتين!',
                    'warning'
                );
            }

            showLoadingSpinner(
                passwordForm,
                'جاري التحديث'
            );

            fetch('/Owner/Profile/ChangePassword', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    CurrentPassword: current,
                    NewPassword: newPwd
                })
            })
                .then(res => res.json())
                .then(data => {
                    hideLoadingSpinner(
                        passwordForm,
                        '<i class="bi bi-shield-check me-2"></i> تحديث الأمان'
                    );

                    if (data.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'مؤمن',
                            text: 'تم تغيير كلمة المرور بنجاح',
                            timer: 2000
                        });

                        passwordForm.reset();

                        if (strengthBar) {
                            strengthBar.style.width = '0%';
                        }

                        if (strengthText) {
                            strengthText.innerText = '';
                        }
                    } else {
                        Swal.fire(
                            'خطأ',
                            data.message,
                            'error'
                        );
                    }
                })
                .catch(() => {
                    hideLoadingSpinner(
                        passwordForm,
                        '<i class="bi bi-shield-check me-2"></i> تحديث الأمان'
                    );

                    Swal.fire(
                        'خطأ',
                        'فشل الاتصال بالخادم.',
                        'error'
                    );
                });
        });
    }


    // =============================================================
    // 7. تفضيلات النظام والواجهة
    // =============================================================

    const profileDarkModeSwitch =
        document.getElementById('profileDarkModeSwitch');

    if (profileDarkModeSwitch) {
        profileDarkModeSwitch.checked =
            localStorage.getItem('darkMode') === 'enabled';

        profileDarkModeSwitch.addEventListener(
            'change',
            function () {
                fetch('/Owner/Profile/SetPreference', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        Key: 'DarkModeEnabled',
                        Value: this.checked
                    })
                });

                document.body.classList.toggle(
                    'dark-mode',
                    this.checked
                );

                localStorage.setItem(
                    'darkMode',
                    this.checked
                        ? 'enabled'
                        : 'disabled'
                );

                const icon =
                    document.getElementById(
                        'profileDarkModeIcon'
                    );

                if (icon) {
                    icon.className =
                        this.checked
                            ? 'bi bi-sun-fill text-warning'
                            : 'bi bi-moon-stars-fill text-primary';
                }
            }
        );
    }

    const compactModeToggle =
        document.getElementById('compactModeToggle');

    const sidebar =
        document.getElementById('dashboardSidebar');

    if (compactModeToggle) {
        compactModeToggle.checked =
            localStorage.getItem('ownerCompactMode') ===
            'enabled';

        compactModeToggle.addEventListener(
            'change',
            function () {
                const isChecked =
                    this.checked;

                localStorage.setItem(
                    'ownerCompactMode',
                    isChecked
                        ? 'enabled'
                        : 'disabled'
                );

                document.cookie =
                    `compactMode_owner=${isChecked ? 'enabled' : 'disabled'}; path=/; max-age=31536000`;

                if (sidebar) {
                    sidebar.classList.toggle(
                        'sidebar-collapsed',
                        isChecked
                    );
                }

                fetch('/Owner/Profile/SetPreference', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        Key: 'CompactMode',
                        Value: isChecked
                    })
                }).catch(e => {
                    console.warn(
                        'فشل حفظ التفضيل:',
                        e
                    );
                });
            }
        );
    }

    // =============================================================
    // 7.1 عملة عرض الأسعار — نفس تجربة الأدمن
    // =============================================================

    const currencyInputs =
        Array.from(
            document.querySelectorAll(
                'input[name="preferredCurrency"]'
            )
        );

    const currencyStatus =
        document.getElementById(
            'preferredCurrencyStatus'
        );

    let lastSavedCurrency =
        currencyInputs
            .find(input => input.checked)
            ?.value ||
        'ريال يمني';

    let currencySaveSequence = 0;
    let currencySaveController = null;


    function setCurrencyStatusText(
        currency,
        state = 'saved'
    ) {
        if (!currencyStatus) return;

        currencyStatus.classList.remove(
            'is-saving',
            'is-saved',
            'is-error'
        );

        if (state === 'saving') {
            currencyStatus.classList.add('is-saving');
            currencyStatus.innerHTML =
                `<i class="bi bi-arrow-repeat"></i>
                 <span>جاري تثبيت <strong>${currency}</strong>...</span>`;
            return;
        }

        if (state === 'error') {
            currencyStatus.classList.add('is-error');
            currencyStatus.innerHTML =
                '<i class="bi bi-exclamation-circle"></i><span>تعذر حفظ العملة</span>';
            return;
        }

        currencyStatus.classList.add('is-saved');
        currencyStatus.innerHTML =
            `<i class="bi bi-check2-circle"></i>
             <span>العملة الحالية: <strong>${currency}</strong></span>`;
    }


    function saveOwnerCurrencyFast(selectedCurrency) {
        currencySaveController?.abort();

        const controller =
            new AbortController();

        currencySaveController =
            controller;

        return fetch(
            '/Owner/Profile/SetPreference',
            {
                method: 'POST',
                headers: {
                    'Content-Type':
                        'application/json',
                    'X-Requested-With':
                        'XMLHttpRequest'
                },
                body: JSON.stringify({
                    Key: 'Currency',
                    Value: selectedCurrency
                }),
                signal:
                    controller.signal
            }
        )
            .then(async response => {
                const data =
                    await response
                        .json()
                        .catch(() => ({}));

                if (
                    !response.ok ||
                    data.success === false
                ) {
                    throw new Error(
                        data.message ||
                        'تعذر حفظ العملة المفضلة.'
                    );
                }

                return data;
            })
            .finally(() => {
                if (
                    currencySaveController ===
                    controller
                ) {
                    currencySaveController = null;
                }
            });
    }


    currencyInputs.forEach(input => {
        input.addEventListener(
            'change',
            function () {
                if (!this.checked) return;

                const selectedCurrency =
                    this.value;

                const previousCurrency =
                    lastSavedCurrency;

                const currentSequence =
                    ++currencySaveSequence;

                setCurrencyStatusText(
                    selectedCurrency,
                    'saving'
                );

                window.dispatchEvent(
                    new CustomEvent(
                        'yemenguide:currency-changing',
                        {
                            detail: {
                                currency:
                                    selectedCurrency
                            }
                        }
                    )
                );

                void saveOwnerCurrencyFast(
                    selectedCurrency
                )
                    .then(() => {
                        if (
                            currentSequence !==
                            currencySaveSequence
                        ) {
                            return;
                        }

                        lastSavedCurrency =
                            selectedCurrency;

                        setCurrencyStatusText(
                            lastSavedCurrency,
                            'saved'
                        );

                        window.dispatchEvent(
                            new CustomEvent(
                                'yemenguide:currency-changed',
                                {
                                    detail: {
                                        currency:
                                            lastSavedCurrency
                                    }
                                }
                            )
                        );
                    })
                    .catch(error => {
                        if (
                            error.name === 'AbortError' ||
                            currentSequence !==
                            currencySaveSequence
                        ) {
                            return;
                        }

                        const previousInput =
                            currencyInputs.find(
                                item =>
                                    item.value ===
                                    previousCurrency
                            );

                        if (previousInput) {
                            previousInput.checked = true;
                        }

                        setCurrencyStatusText(
                            previousCurrency,
                            'error'
                        );

                        Swal.fire({
                            toast: true,
                            position: 'top-end',
                            icon: 'error',
                            title: 'تعذر حفظ العملة',
                            text:
                                error.message ||
                                'حاول مرة أخرى.',
                            showConfirmButton: false,
                            timer: 2200
                        });
                    });
            }
        );
    });


    const languageSelect =
        document.getElementById('languageSelect');

    if (languageSelect) {
        languageSelect.addEventListener(
            'change',
            function () {
                fetch('/Language/SetLanguage', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        Culture: this.value
                    })
                })
                    .then(res => res.json())
                    .then(data => {
                        if (data.success) {
                            window.location.reload();
                        }
                    })
                    .catch(() => {
                        Swal.fire(
                            'خطأ',
                            'فشل تغيير لغة النظام.',
                            'error'
                        );
                    });
            }
        );
    }

    document.querySelectorAll('.alert-toggle').forEach(toggle => {
        toggle.addEventListener('change', function () {
            const key =
                this.getAttribute('data-key');

            fetch('/Owner/Profile/SetPreference', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    Key: key,
                    Value: this.checked
                })
            }).catch(e => {
                console.warn(
                    'فشل حفظ التفضيل:',
                    e
                );
            });
        });
    });


    // =============================================================
    // 8. تعطيل الحساب
    // =============================================================

    const suspendBtn =
        document.getElementById('suspendAccountBtn');

    if (suspendBtn) {
        suspendBtn.addEventListener('click', () => {
            Swal.fire({
                title: 'إجراء خطير!',
                text: 'هل أنت متأكد من رغبتك بتعطيل حسابك التجاري؟',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#dc3545',
                confirmButtonText: 'نعم، قم بالتعطيل',
                cancelButtonText: 'تراجع'
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch('/Owner/Profile/SuspendAccount', {
                        method: 'POST'
                    })
                        .then(() => {
                            Swal.fire(
                                'تم',
                                'تم تعطيل الحساب بنجاح.',
                                'success'
                            );

                            setTimeout(() => {
                                window.location.href =
                                    '/Account/Logout';
                            }, 2000);
                        })
                        .catch(() => {
                            Swal.fire(
                                'خطأ',
                                'فشل تعطيل الحساب.',
                                'error'
                            );
                        });
                }
            });
        });
    }


    // =============================================================
    // تبويبات الملف الشخصي — نفس سلوك الأدمن
    // =============================================================

    const profileTabRoot =
        document.getElementById('profileTab');

    const profileTabButtons =
        profileTabRoot
            ? Array.from(
                profileTabRoot.querySelectorAll(
                    '.profile-tab-btn'
                )
            )
            : [];


    function showProfileTab(
        button,
        focus = false
    ) {
        if (!button) return;

        if (window.bootstrap?.Tab) {
            bootstrap.Tab
                .getOrCreateInstance(button)
                .show();
        } else {
            button.click();
        }

        profileTabButtons.forEach(item => {
            const isCurrent =
                item === button;

            item.setAttribute(
                'tabindex',
                isCurrent ? '0' : '-1'
            );
        });

        if (focus) {
            button.focus({
                preventScroll: true
            });
        }

        button.scrollIntoView({
            behavior: 'auto',
            block: 'nearest',
            inline: 'nearest'
        });
    }


    profileTabButtons.forEach(
        (button, index) => {

            button.addEventListener(
                'keydown',
                function (event) {
                    const isRtl =
                        getComputedStyle(
                            profileTabRoot
                        ).direction === 'rtl';

                    let nextIndex = null;

                    if (event.key === 'ArrowLeft') {
                        nextIndex =
                            isRtl
                                ? index + 1
                                : index - 1;
                    }
                    else if (event.key === 'ArrowRight') {
                        nextIndex =
                            isRtl
                                ? index - 1
                                : index + 1;
                    }
                    else if (event.key === 'Home') {
                        nextIndex = 0;
                    }
                    else if (event.key === 'End') {
                        nextIndex =
                            profileTabButtons.length - 1;
                    }

                    if (nextIndex === null) {
                        return;
                    }

                    event.preventDefault();

                    const normalizedIndex =
                        (
                            nextIndex +
                            profileTabButtons.length
                        ) %
                        profileTabButtons.length;

                    showProfileTab(
                        profileTabButtons[
                        normalizedIndex
                        ],
                        true
                    );
                }
            );

            button.addEventListener(
                'shown.bs.tab',
                function () {
                    profileTabButtons.forEach(
                        item => {
                            item.setAttribute(
                                'tabindex',
                                item === button
                                    ? '0'
                                    : '-1'
                            );
                        }
                    );

                    const target =
                        button.getAttribute(
                            'data-bs-target'
                        );

                    if (
                        target &&
                        window.history?.replaceState
                    ) {
                        window.history.replaceState(
                            null,
                            '',
                            target
                        );
                    }
                }
            );
        }
    );


    function activateTabFromHash() {
        const requestedTarget =
            window.location.hash;

        if (!requestedTarget) {
            return;
        }

        const tabButton =
            profileTabButtons.find(
                button =>
                    button.getAttribute(
                        'data-bs-target'
                    ) === requestedTarget
            );

        if (!tabButton) {
            return;
        }

        showProfileTab(
            tabButton,
            false
        );
    }


    activateTabFromHash();

    window.addEventListener(
        'hashchange',
        activateTabFromHash
    );


    // =============================================================
    // دوال التحميل العامة
    // =============================================================

    function showLoadingSpinner(form, loadingText) {
        const btn =
            form.querySelector(
                'button[type="submit"]'
            );

        if (btn) {
            btn.disabled = true;

            btn.innerHTML = `
                <span class="spinner-border spinner-border-sm me-2"></span>
                ${loadingText}`;
        }
    }

    function hideLoadingSpinner(form, originalHtml) {
        const btn =
            form.querySelector(
                'button[type="submit"]'
            );

        if (btn) {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }
});