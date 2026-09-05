/**
 * =============================================================
 * Admin Profile Interactions
 * التحقق اللحظي، مكتبة الهاتف، الصورة، الأمان وتفضيلات المدير
 * =============================================================
 */

document.addEventListener('DOMContentLoaded', function () {

    // =============================================================
    // أدوات مساعدة عامة
    // =============================================================

    function getFieldWrapper(element) {
        return element
            ? element.closest('.input-group, .premium-input-group')
            : null;
    }

    function setValidationState(wrapper, state) {
        if (!wrapper) return;

        wrapper.classList.remove('glow-error', 'glow-success');

        if (state === 'error') {
            wrapper.classList.add('glow-error');
        } else if (state === 'success') {
            wrapper.classList.add('glow-success');
        }
    }

    function setCookie(name, value, maxAge = 31536000) {
        document.cookie =
            `${name}=${value}; path=/; max-age=${maxAge}; SameSite=Lax`;
    }

    function showLoadingSpinner(form, loadingText) {
        const btn =
            form?.querySelector('button[type="submit"]');

        if (btn) {
            btn.disabled = true;

            btn.innerHTML = `
                <span class="spinner-border spinner-border-sm me-2"></span>
                ${loadingText}`;
        }
    }

    function hideLoadingSpinner(form, originalHtml) {
        const btn =
            form?.querySelector('button[type="submit"]');

        if (btn) {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }


    // =============================================================
    // 1. إدارة الصورة الشخصية
    // =============================================================

    const avatarModalElement =
        document.getElementById('avatarManageModal');

    const avatarModal =
        avatarModalElement && window.bootstrap?.Modal
            ? new bootstrap.Modal(avatarModalElement)
            : null;

    const mainAvatarPreview =
        document.getElementById('avatarPreview');

    const modalAvatarPreview =
        document.getElementById('modalAvatarPreview');

    const fileInput =
        document.getElementById('profileImageUpload');

    const cameraBtn =
        document.getElementById('uploadCameraBtn');

    const modalUploadBtn =
        document.getElementById('modalUploadBtn');

    const modalDeleteBtn =
        document.getElementById('modalDeleteBtn');
    if (mainAvatarPreview) {
        mainAvatarPreview.addEventListener(
            'click',
            function () {
                avatarModal?.show();
            }
        );
    }


    [cameraBtn, modalUploadBtn].forEach(btn => {
        if (btn && fileInput) {
            btn.addEventListener(
                'click',
                function (event) {
                    event.preventDefault();
                    event.stopPropagation();

                    fileInput.click();
                }
            );
        }
    });


    if (fileInput) {
        fileInput.addEventListener(
            'change',
            function (event) {
                const file =
                    event.target.files?.[0];

                if (!file) return;
                const imageUrl =
                    URL.createObjectURL(file);

                [
                    mainAvatarPreview,
                    modalAvatarPreview
                ].forEach(element => {
                    if (!element) return;

                    element.style.backgroundImage =
                        `url(${imageUrl})`;

                    element.style.background =
                        `url(${imageUrl}) center/cover no-repeat`;

                    const initialElement =
                        element.querySelector(
                            '.avatar-initial, #modalAvatarInitial, #modalInitialContainer'
                        );

                    if (initialElement) {
                        initialElement.style.display =
                            'none';
                    }
                });

                if (modalDeleteBtn) {
                    modalDeleteBtn.style.display =
                        'block';
                }

                window.setTimeout(
                    () => URL.revokeObjectURL(imageUrl),
                    2000
                );

                uploadProfileImage(file);
            }
        );
    }


    function uploadProfileImage(file) {
        const formData =
            new FormData();

        formData.append(
            'profileImage',
            file
        );

        fetch(
            '/Admin/Profile/UploadProfileImage',
            {
                method: 'POST',
                body: formData
            }
        )
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

                    avatarModal?.hide();
                } else {
                    Swal.fire(
                        'عذراً',
                        data.message ||
                        'تعذر تحديث الصورة الشخصية.',
                        'error'
                    );
                }
            })

            .catch(() => {
                Swal.fire(
                    'خطأ في الاتصال',
                    'لم نتمكن من رفع الصورة للسيرفر',
                    'error'
                );
            })

            .finally(() => {
                if (fileInput) {
                    fileInput.value = '';
                }
            });
    }


    if (modalDeleteBtn) {
        modalDeleteBtn.addEventListener(
            'click',
            function () {
                Swal.fire({
                    title: 'هل أنت متأكد؟',
                    text: 'سيتم حذف صورتك الشخصية واستعادة الحرف الافتراضي!',
                    icon: 'warning',

                    showCancelButton: true,

                    confirmButtonColor:
                        '#dc3545',

                    cancelButtonColor:
                        '#6c757d',

                    confirmButtonText:
                        'نعم، احذفها',

                    cancelButtonText:
                        'إلغاء'

                }).then(result => {

                    if (!result.isConfirmed) {
                        return;
                    }

                    fetch(
                        '/Admin/Profile/DeleteImage',
                        {
                            method: 'POST'
                        }
                    )
                        .then(response =>
                            response.json()
                        )

                        .then(data => {

                            if (!data.success) {
                                Swal.fire(
                                    'عذراً',
                                    data.message ||
                                    'تعذر حذف الصورة الشخصية.',
                                    'error'
                                );

                                return;
                            }

                            [
                                mainAvatarPreview,
                                modalAvatarPreview
                            ].forEach(element => {

                                if (!element) return;

                                element.style.backgroundImage =
                                    'none';

                                element.style.background =
                                    'linear-gradient(135deg, #05203C 0%, #00A6DA 100%)';
                            });


                            /*
                             * لا ننشئ زر كاميرا جديدًا هنا.
                             * زر uploadCameraBtn الأصلي ما زال موجودًا.
                             */

                            if (mainAvatarPreview) {
                                mainAvatarPreview.innerHTML = `
                                    <span class="avatar-initial text-white fw-bold display-4"
                                          id="mainAvatarInitial">
                                        ${data.initialLetter || 'A'}
                                    </span>`;
                            }


                            if (modalAvatarPreview) {
                                modalAvatarPreview.innerHTML = `
                                    <div class="d-flex align-items-center justify-content-center h-100"
                                         id="modalInitialContainer">

                                        <span class="text-white fw-bold display-3"
                                              id="modalAvatarInitial">

                                            ${data.initialLetter || 'A'}
                                        </span>
                                    </div>`;
                            }


                            modalDeleteBtn.style.display =
                                'none';

                            avatarModal?.hide();


                            Swal.fire({
                                icon: 'success',
                                title: 'تم الحذف!',
                                text: 'تمت إزالة الصورة بنجاح.',
                                timer: 1800,
                                showConfirmButton: false
                            });
                        })

                        .catch(() => {
                            Swal.fire(
                                'خطأ في الاتصال',
                                'تعذر حذف الصورة الشخصية من الخادم.',
                                'error'
                            );
                        });
                });
            }
        );
    }


    // =============================================================
    // 2. أزرار تعديل الحقول
    // =============================================================

    document
        .querySelectorAll('.edit-icon')
        .forEach(btn => {

            btn.addEventListener(
                'click',
                function () {

                    const targetId =
                        this.getAttribute(
                            'data-target'
                        );

                    const input =
                        targetId
                            ? document.getElementById(
                                targetId
                            )
                            : null;

                    if (!input) return;

                    input.focus();


                    if (
                        typeof input.setSelectionRange ===
                        'function'
                    ) {
                        const length =
                            input.value.length;

                        try {
                            input.setSelectionRange(
                                length,
                                length
                            );
                        } catch (_) {
                            /*
                             * بعض أنواع الحقول لا تدعم
                             * setSelectionRange.
                             */
                        }
                    }
                }
            );
        });
    // =============================================================
    // 3. مكتبة الأرقام الدولية — تحميل كسول لتحسين سرعة الصفحة
    // =============================================================

    const phoneInput =
        document.getElementById('phoneNumber');

    const phoneError =
        document.getElementById(
            'profilePhoneError'
        );

    let iti = null;
    let phoneLibraryPromise = null;
    let phoneInitialized = false;


    function loadProfileStylesheetOnce(href, id) {
        if (document.getElementById(id)) {
            return;
        }

        const link =
            document.createElement('link');

        link.id = id;
        link.rel = 'stylesheet';
        link.href = href;

        document.head.appendChild(link);
    }


    function loadProfileScriptOnce(src, id) {
        const existing =
            document.getElementById(id);

        if (existing) {
            return new Promise((resolve, reject) => {
                if (
                    typeof window.intlTelInput ===
                    'function'
                ) {
                    resolve();
                    return;
                }

                existing.addEventListener(
                    'load',
                    resolve,
                    { once: true }
                );

                existing.addEventListener(
                    'error',
                    reject,
                    { once: true }
                );
            });
        }

        return new Promise((resolve, reject) => {
            const script =
                document.createElement('script');

            script.id = id;
            script.src = src;
            script.async = true;

            script.addEventListener(
                'load',
                resolve,
                { once: true }
            );

            script.addEventListener(
                'error',
                reject,
                { once: true }
            );

            document.head.appendChild(script);
        });
    }


    function ensurePhoneLibrary() {
        if (
            typeof window.intlTelInput ===
            'function'
        ) {
            return Promise.resolve();
        }

        if (phoneLibraryPromise) {
            return phoneLibraryPromise;
        }

        loadProfileStylesheetOnce(
            'https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/css/intlTelInput.css',
            'profileIntlTelInputCss'
        );

        phoneLibraryPromise =
            loadProfileScriptOnce(
                'https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/intlTelInput.min.js',
                'profileIntlTelInputJs'
            )
                .catch(error => {
                    phoneLibraryPromise = null;
                    throw error;
                });

        return phoneLibraryPromise;
    }


    function validatePhoneField() {
        if (!phoneInput) return;

        const wrapper =
            phoneInput.closest(
                '.phone-wrapper-ltr'
            );

        if (phoneError) {
            phoneError.style.display =
                'none';
        }

        if (
            phoneInput.value.trim() ===
            ''
        ) {
            setValidationState(
                wrapper,
                null
            );

            return;
        }

        const isValid =
            iti
                ? iti.isValidNumber()
                : /^[0-9\s+]{7,20}$/.test(
                    phoneInput.value.trim()
                );

        setValidationState(
            wrapper,
            isValid
                ? 'success'
                : 'error'
        );
    }


    async function initializePhoneLibrary() {
        if (
            !phoneInput ||
            phoneInitialized
        ) {
            return;
        }

        try {
            await ensurePhoneLibrary();

            if (
                typeof window.intlTelInput !==
                'function'
            ) {
                return;
            }

            iti =
                window.intlTelInput(
                    phoneInput,
                    {
                        initialCountry: 'ye',

                        preferredCountries: [
                            'ye',
                            'sa',
                            'ae',
                            'eg',
                            'qa'
                        ],

                        separateDialCode: true,

                        formatOnDisplay: true,

                        autoPlaceholder:
                            'polite',

                        dropdownContainer:
                            document.body,

                        utilsScript:
                            'https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js'
                    }
                );

            phoneInitialized = true;

            phoneInput.addEventListener(
                'countrychange',
                validatePhoneField
            );

            validatePhoneField();
        } catch (error) {
            console.warn(
                'تعذر تحميل مكتبة الهاتف. سيستمر الحقل بالتحقق الأساسي.',
                error
            );
        }
    }


    if (phoneInput) {
        phoneInput.addEventListener(
            'input',
            function () {
                this.value =
                    this.value.replace(
                        /[^0-9\s+]/g,
                        ''
                    );

                validatePhoneField();
            }
        );

        ['focus', 'pointerdown'].forEach(
            eventName => {
                phoneInput.addEventListener(
                    eventName,
                    initializePhoneLibrary,
                    {
                        once: true,
                        passive:
                            eventName ===
                            'pointerdown'
                    }
                );
            }
        );

        if ('requestIdleCallback' in window) {
            window.requestIdleCallback(
                () => initializePhoneLibrary(),
                { timeout: 1800 }
            );
        } else {
            window.setTimeout(
                () => initializePhoneLibrary(),
                900
            );
        }
    }


    // =============================================================
    // 4. التحقق اللحظي لاسم المستخدم والبريد
    // =============================================================

    const usernameInput =
        document.getElementById('username');

    const emailInput =
        document.getElementById(
            'contactInfo'
        );

    const userError =
        document.getElementById(
            'profileUsernameError'
        );

    const emailError =
        document.getElementById(
            'profileEmailError'
        );

    const emailWarningDiv =
        document.getElementById(
            'emailVerificationWarning'
        );

    const inlineVerifyEmailBtn =
        document.getElementById(
            'inlineVerifyEmailBtn'
        );

    const emailVerifiedBadge =
        document.getElementById(
            'emailVerifiedBadge'
        );

    const originalEmail =
        emailInput
            ? emailInput.value.trim()
            : '';

    const wasOriginallyVerified =
        Boolean(
            emailVerifiedBadge &&
            !emailVerifiedBadge
                .classList
                .contains('d-none')
        );


    if (usernameInput) {
        usernameInput.addEventListener(
            'input',
            function () {

                if (userError) {
                    userError.style.display =
                        'none';
                }

                const wrapper =
                    getFieldWrapper(this);

                const value =
                    this.value.trim();


                if (value === '') {
                    setValidationState(
                        wrapper,
                        null
                    );

                    return;
                }


                const usernameRegex =
                    /^[a-zA-Z0-9_]{3,20}$/;


                setValidationState(
                    wrapper,

                    usernameRegex.test(value)
                        ? 'success'
                        : 'error'
                );
            }
        );
    }


    if (emailInput) {
        emailInput.addEventListener(
            'input',
            function () {

                if (emailError) {
                    emailError.style.display =
                        'none';
                }

                const wrapper =
                    getFieldWrapper(this);

                const currentEmail =
                    this.value.trim();


                if (
                    currentEmail !==
                    originalEmail
                ) {
                    emailVerifiedBadge
                        ?.classList
                        .add('d-none');

                    emailWarningDiv
                        ?.classList
                        .remove('d-none');

                } else {
                    emailWarningDiv
                        ?.classList
                        .add('d-none');


                    if (
                        wasOriginallyVerified
                    ) {
                        emailVerifiedBadge
                            ?.classList
                            .remove('d-none');
                    }
                }


                if (
                    currentEmail === ''
                ) {
                    setValidationState(
                        wrapper,
                        null
                    );

                    return;
                }


                const emailRegex =
                    /^[^\s@]+@[^\s@]+\.[^\s@]+$/;


                setValidationState(
                    wrapper,

                    emailRegex.test(
                        currentEmail
                    )
                        ? 'success'
                        : 'error'
                );
            }
        );
    }


    // =============================================================
    // 5. فتح التقويم من الحقل كاملًا
    // =============================================================

    const dateInput =
        document.getElementById(
            'dateOfBirth'
        );


    if (dateInput) {
        const dateWrapper =
            getFieldWrapper(
                dateInput
            );


        if (dateWrapper) {
            dateWrapper.addEventListener(
                'click',
                function (event) {

                    if (
                        event.target ===
                        dateInput
                    ) {
                        return;
                    }


                    try {
                        dateInput.showPicker();
                    } catch (_) {
                        dateInput.focus();
                    }
                }
            );
        }


        dateInput.addEventListener(
            'click',
            function () {
                try {
                    this.showPicker();
                } catch (_) {
                    /*
                     * المتصفح لا يدعم
                     * showPicker.
                     */
                }
            }
        );
    }


    // =============================================================
    // 6. طلب توثيق البريد الإلكتروني
    // =============================================================

    if (
        inlineVerifyEmailBtn &&
        emailInput
    ) {
        inlineVerifyEmailBtn
            .addEventListener(
                'click',
                function () {

                    const newEmail =
                        emailInput
                            .value
                            .trim();

                    const emailRegex =
                        /^[^\s@]+@[^\s@]+\.[^\s@]+$/;


                    if (
                        !emailRegex.test(
                            newEmail
                        )
                    ) {
                        if (emailError) {
                            emailError.innerHTML = `
                                <i class="bi bi-info-circle-fill me-1"></i>
                                يرجى إدخال بريد إلكتروني صحيح أولاً.`;

                            emailError.style.display =
                                'block';
                        }

                        setValidationState(
                            getFieldWrapper(
                                emailInput
                            ),
                            'error'
                        );

                        emailInput.focus();

                        return;
                    }


                    const originalButtonHtml =
                        this.innerHTML;

                    this.disabled = true;

                    this.innerHTML = `
                        <span class="spinner-border spinner-border-sm me-1"></span>
                        جاري...`;


                    fetch(
                        '/Admin/Profile/RequestEmailChange',
                        {
                            method:
                                'POST',

                            headers: {
                                'Content-Type':
                                    'application/json'
                            },

                            body:
                                JSON.stringify({
                                    NewEmail:
                                        newEmail
                                })
                        }
                    )
                        .then(response =>
                            response.json()
                        )

                        .then(data => {
                            this.disabled =
                                false;

                            this.innerHTML =
                                originalButtonHtml;


                            if (data.success) {
                                Swal.fire({
                                    icon:
                                        'info',

                                    title:
                                        'خطوة أخيرة',

                                    text:
                                        'تم إرسال رمز التحقق. سيتم توجيهك الآن لإدخاله...',

                                    timer:
                                        2500,

                                    showConfirmButton:
                                        false

                                }).then(() => {

                                    window.location.href =
                                        `/Account/VerifyOtp?email=${encodeURIComponent(newEmail)}&type=update`;
                                });

                            } else {
                                Swal.fire(
                                    'عذراً',

                                    data.message ||
                                    'تعذر إرسال رمز التحقق.',

                                    'error'
                                );
                            }
                        })

                        .catch(() => {
                            this.disabled =
                                false;

                            this.innerHTML =
                                originalButtonHtml;


                            Swal.fire(
                                'خطأ',
                                'فشل الاتصال بالخادم.',
                                'error'
                            );
                        });
                }
            );
    }


    // =============================================================
    // 7. حفظ البيانات الشخصية
    // =============================================================

    const personalForm =
        document.getElementById(
            'personalInfoForm'
        );

    const cancelEditBtn =
        document.getElementById(
            'cancelEditBtn'
        );


    if (cancelEditBtn) {
        cancelEditBtn.addEventListener(
            'click',
            function () {

                window.setTimeout(
                    () => {

                        document
                            .querySelectorAll(
                                '.glow-error, .glow-success'
                            )
                            .forEach(element => {

                                element
                                    .classList
                                    .remove(
                                        'glow-error',
                                        'glow-success'
                                    );
                            });


                        [
                            userError,
                            emailError,
                            phoneError
                        ].forEach(
                            errorElement => {

                                if (
                                    errorElement
                                ) {
                                    errorElement
                                        .style
                                        .display =
                                        'none';
                                }
                            }
                        );


                        emailWarningDiv
                            ?.classList
                            .add('d-none');


                        if (
                            wasOriginallyVerified
                        ) {
                            emailVerifiedBadge
                                ?.classList
                                .remove(
                                    'd-none'
                                );
                        }

                    },
                    0
                );
            }
        );
    }


    if (personalForm) {
        personalForm.addEventListener(
            'submit',
            function (event) {

                event.preventDefault();


                if (userError) {
                    userError.style.display =
                        'none';
                }

                if (emailError) {
                    emailError.style.display =
                        'none';
                }

                if (phoneError) {
                    phoneError.style.display =
                        'none';
                }


                let hasError =
                    false;


                const usernameValue =
                    usernameInput
                        ?.value
                        .trim() || '';

                const usernameRegex =
                    /^[a-zA-Z0-9_]{3,20}$/;


                if (
                    !usernameRegex.test(
                        usernameValue
                    )
                ) {
                    if (userError) {
                        userError.innerHTML = `
                            <i class="bi bi-info-circle-fill me-1"></i>
                            يرجى استخدام أحرف إنجليزية وأرقام فقط.`;

                        userError.style.display =
                            'block';
                    }

                    setValidationState(
                        getFieldWrapper(
                            usernameInput
                        ),
                        'error'
                    );

                    hasError =
                        true;
                }


                const emailValue =
                    emailInput
                        ?.value
                        .trim() || '';

                const emailRegex =
                    /^[^\s@]+@[^\s@]+\.[^\s@]+$/;


                if (
                    !emailRegex.test(
                        emailValue
                    )
                ) {
                    if (emailError) {
                        emailError.innerHTML = `
                            <i class="bi bi-info-circle-fill me-1"></i>
                            يرجى إدخال بريد إلكتروني صحيح.`;

                        emailError.style.display =
                            'block';
                    }

                    setValidationState(
                        getFieldWrapper(
                            emailInput
                        ),
                        'error'
                    );

                    hasError =
                        true;
                }


                const phoneWrapper =
                    phoneInput
                        ?.closest(
                            '.phone-wrapper-ltr'
                        );


                const phoneIsValid =
                    iti
                        ? iti.isValidNumber()
                        : Boolean(
                            phoneInput
                                ?.value
                                .trim()
                        );


                if (!phoneIsValid) {
                    if (phoneError) {
                        phoneError.innerHTML = `
                            <i class="bi bi-info-circle-fill me-1"></i>
                            يرجى إدخال رقم هاتف صحيح يطابق الدولة المختارة.`;

                        phoneError.style.display =
                            'block';
                    }

                    setValidationState(
                        phoneWrapper,
                        'error'
                    );

                    hasError =
                        true;
                }


                if (hasError) {
                    return;
                }


                if (
                    emailValue !==
                    originalEmail
                ) {
                    emailWarningDiv
                        ?.classList
                        .remove('d-none');


                    emailWarningDiv
                        ?.scrollIntoView({
                            behavior:
                                'smooth',

                            block:
                                'center'
                        });


                    if (
                        inlineVerifyEmailBtn
                    ) {
                        inlineVerifyEmailBtn
                            .classList
                            .add(
                                'btn-warning'
                            );

                        inlineVerifyEmailBtn
                            .classList
                            .remove(
                                'btn-danger'
                            );


                        window.setTimeout(
                            () => {
                                inlineVerifyEmailBtn
                                    .classList
                                    .add(
                                        'btn-danger'
                                    );

                                inlineVerifyEmailBtn
                                    .classList
                                    .remove(
                                        'btn-warning'
                                    );
                            },
                            500
                        );
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
                        : phoneInput
                            ?.value
                            .trim() || '';


                const formData = {
                    FullName:
                        document
                            .getElementById(
                                'fullName'
                            )
                            ?.value || '',

                    Username:
                        usernameValue,

                    ContactInfo:
                        emailValue,

                    PhoneNumber:
                        fullInternationalPhone,

                    City:
                        document
                            .getElementById(
                                'city'
                            )
                            ?.value || '',

                    Gender:
                        document
                            .getElementById(
                                'gender'
                            )
                            ?.value || '',

                    DateOfBirth:
                        document
                            .getElementById(
                                'dateOfBirth'
                            )
                            ?.value || null,

                    Bio:
                        document
                            .getElementById(
                                'bio'
                            )
                            ?.value || '',

                    FacebookUrl:
                        document
                            .getElementById(
                                'facebookUrl'
                            )
                            ?.value || '',

                    TwitterUrl:
                        document
                            .getElementById(
                                'twitterUrl'
                            )
                            ?.value || '',

                    InstagramUrl:
                        document
                            .getElementById(
                                'instagramUrl'
                            )
                            ?.value || ''
                };


                fetch(
                    '/Admin/Profile/UpdatePersonalInfo',
                    {
                        method:
                            'POST',

                        headers: {
                            'Content-Type':
                                'application/json'
                        },

                        body:
                            JSON.stringify(
                                formData
                            )
                    }
                )
                    .then(response =>
                        response.json()
                    )

                    .then(data => {

                        hideLoadingSpinner(
                            personalForm,
                            '<i class="bi bi-floppy-fill me-2"></i> حفظ التحديثات'
                        );


                        if (data.success) {
                            const profileName =
                                document.querySelector(
                                    '.profile-name'
                                );


                            if (profileName) {
                                profileName.textContent =
                                    formData.FullName;
                            }


                            document
                                .querySelectorAll(
                                    '.glow-success'
                                )
                                .forEach(
                                    element => {

                                        element
                                            .classList
                                            .remove(
                                                'glow-success'
                                            );
                                    }
                                );


                            Swal.fire({
                                icon:
                                    'success',

                                title:
                                    'تم الحفظ',

                                text:
                                    'تم تحديث بياناتك الشخصية بنجاح',

                                timer:
                                    2000,

                                showConfirmButton:
                                    false
                            });

                        } else if (
                            data.message &&

                            data.message.includes(
                                'اسم المستخدم'
                            ) &&

                            userError
                        ) {
                            userError.innerHTML = `
                                <i class="bi bi-exclamation-triangle-fill me-1"></i>
                                ${data.message}`;

                            userError.style.display =
                                'block';


                            setValidationState(
                                getFieldWrapper(
                                    usernameInput
                                ),
                                'error'
                            );


                            usernameInput
                                ?.focus();

                        } else {
                            Swal.fire(
                                'خطأ',

                                data.message ||
                                'تعذر حفظ البيانات الشخصية.',

                                'error'
                            );
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
            }
        );
    }


    // =============================================================
    // 8. الأمان وكلمات المرور
    // =============================================================

    document
        .querySelectorAll(
            '.toggle-password'
        )
        .forEach(btn => {

            btn.addEventListener(
                'click',
                function () {

                    const input =
                        document.getElementById(
                            this.dataset.target
                        );

                    if (!input) return;


                    if (
                        input.type ===
                        'password'
                    ) {
                        input.type =
                            'text';

                        this.innerHTML =
                            '<i class="bi bi-eye text-primary"></i>';

                    } else {
                        input.type =
                            'password';

                        this.innerHTML =
                            '<i class="bi bi-eye-slash"></i>';
                    }
                }
            );
        });


    const newPassword =
        document.getElementById(
            'newPassword'
        );

    const pwdError =
        document.getElementById(
            'profilePasswordError'
        );

    const strengthBar =
        document.querySelector(
            '.strength-bar'
        );

    const strengthText =
        document.querySelector(
            '.strength-text'
        );


    if (
        newPassword &&
        strengthBar &&
        strengthText
    ) {
        newPassword.addEventListener(
            'input',
            function () {

                if (pwdError) {
                    pwdError.style.display =
                        'none';
                }


                const value =
                    this.value;

                let strength =
                    0;


                if (
                    value.length >= 8
                ) {
                    strength++;
                }

                if (
                    /[a-zA-Z]/.test(
                        value
                    )
                ) {
                    strength++;
                }

                if (
                    /\d/.test(
                        value
                    )
                ) {
                    strength++;
                }

                if (
                    /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]+/
                        .test(value)
                ) {
                    strength++;
                }


                const percent =
                    value.length === 0
                        ? 0
                        : (
                            strength / 4
                        ) * 100;


                strengthBar.style.width =
                    `${percent}%`;


                if (
                    value.length === 0
                ) {
                    strengthBar
                        .style
                        .backgroundColor =
                        'transparent';

                    strengthText.innerText =
                        '';

                } else if (
                    strength <= 1
                ) {
                    strengthBar
                        .style
                        .backgroundColor =
                        '#dc3545';

                    strengthText.innerText =
                        'كلمة مرور ضعيفة';

                    strengthText.className =
                        'strength-text d-block mt-1 fw-bold text-danger';

                } else if (
                    strength === 2
                ) {
                    strengthBar
                        .style
                        .backgroundColor =
                        '#ffc107';

                    strengthText.innerText =
                        'كلمة مرور متوسطة';

                    strengthText.className =
                        'strength-text d-block mt-1 fw-bold text-warning';

                } else {
                    strengthBar
                        .style
                        .backgroundColor =
                        '#198754';

                    strengthText.innerText =
                        'كلمة مرور قوية وممتازة';

                    strengthText.className =
                        'strength-text d-block mt-1 fw-bold text-success';
                }
            }
        );
    }


    const passwordForm =
        document.getElementById(
            'passwordForm'
        );


    if (
        passwordForm &&
        newPassword
    ) {
        passwordForm.addEventListener(
            'submit',
            function (event) {

                event.preventDefault();


                if (pwdError) {
                    pwdError.style.display =
                        'none';
                }


                const newPasswordValue =
                    newPassword.value;

                const passwordRegex =
                    /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;


                if (
                    !passwordRegex.test(
                        newPasswordValue
                    )
                ) {
                    if (pwdError) {
                        pwdError.innerHTML = `
                            <i class="bi bi-shield-exclamation me-1"></i>
                            يجب أن تتكون من 8 أحرف على الأقل،
                            وتحتوي على حرف إنجليزي ورقم.`;

                        pwdError.style.display =
                            'block';
                    }

                    newPassword.focus();

                    return;
                }


                const currentPassword =
                    document
                        .getElementById(
                            'currentPassword'
                        )
                        ?.value || '';

                const confirmPassword =
                    document
                        .getElementById(
                            'confirmPassword'
                        )
                        ?.value || '';


                if (
                    newPasswordValue !==
                    confirmPassword
                ) {
                    Swal.fire(
                        'خطأ',
                        'كلمتا المرور الجديدتان غير متطابقتين!',
                        'warning'
                    );

                    return;
                }


                showLoadingSpinner(
                    passwordForm,
                    'جاري التحديث'
                );


                fetch(
                    '/Admin/Profile/ChangePassword',
                    {
                        method:
                            'POST',

                        headers: {
                            'Content-Type':
                                'application/json'
                        },

                        body:
                            JSON.stringify({
                                CurrentPassword:
                                    currentPassword,

                                NewPassword:
                                    newPasswordValue
                            })
                    }
                )
                    .then(response =>
                        response.json()
                    )

                    .then(data => {

                        hideLoadingSpinner(
                            passwordForm,
                            '<i class="bi bi-shield-check me-2"></i> تحديث بروتوكول الأمان'
                        );


                        if (data.success) {
                            Swal.fire({
                                icon:
                                    'success',

                                title:
                                    'مؤمن',

                                text:
                                    'تم تغيير كلمة المرور بنجاح',

                                timer:
                                    2000
                            });


                            passwordForm.reset();


                            if (strengthBar) {
                                strengthBar.style.width =
                                    '0%';

                                strengthBar
                                    .style
                                    .backgroundColor =
                                    'transparent';
                            }


                            if (strengthText) {
                                strengthText.innerText =
                                    '';
                            }

                        } else {
                            Swal.fire(
                                'خطأ',

                                data.message ||
                                'تعذر تغيير كلمة المرور.',

                                'error'
                            );
                        }
                    })

                    .catch(() => {

                        hideLoadingSpinner(
                            passwordForm,
                            '<i class="bi bi-shield-check me-2"></i> تحديث بروتوكول الأمان'
                        );


                        Swal.fire(
                            'خطأ',
                            'فشل الاتصال بالخادم.',
                            'error'
                        );
                    });
            }
        );
    }


    // =============================================================
    // 9. تفضيلات النظام والواجهة
    // =============================================================

    const preferenceSaveControllers =
        new Map();


    function saveProfilePreferenceFast(
        key,
        value
    ) {
        const previousController =
            preferenceSaveControllers.get(
                key
            );

        previousController?.abort();

        const controller =
            new AbortController();

        preferenceSaveControllers.set(
            key,
            controller
        );

        return fetch(
            '/Admin/Profile/SetPreference',
            {
                method: 'POST',

                headers: {
                    'Content-Type':
                        'application/json',

                    'X-Requested-With':
                        'XMLHttpRequest'
                },

                body:
                    JSON.stringify({
                        Key: key,
                        Value: value
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
                        'تعذر حفظ التفضيل.'
                    );
                }

                return data;
            })
            .finally(() => {
                if (
                    preferenceSaveControllers
                        .get(key) === controller
                ) {
                    preferenceSaveControllers
                        .delete(key);
                }
            });
    }


    const profileDarkModeSwitch =
        document.getElementById(
            'profileDarkModeSwitch'
        );

    const profileDarkModeIcon =
        document.getElementById(
            'profileDarkModeIcon'
        );


    if (profileDarkModeSwitch) {
        const currentDarkMode =
            document.body
                .classList
                .contains('dark-mode');


        profileDarkModeSwitch.checked =
            currentDarkMode;


        if (profileDarkModeIcon) {
            profileDarkModeIcon.className =
                currentDarkMode
                    ? 'bi bi-sun-fill text-warning'
                    : 'bi bi-moon-stars-fill text-primary';
        }


        profileDarkModeSwitch
            .addEventListener(
                'change',
                function () {

                    const isEnabled =
                        this.checked;


                    document.body
                        .classList
                        .toggle(
                            'dark-mode',
                            isEnabled
                        );


                    setCookie(
                        'darkMode',

                        isEnabled
                            ? 'enabled'
                            : 'disabled'
                    );


                    if (
                        profileDarkModeIcon
                    ) {
                        profileDarkModeIcon.className =
                            isEnabled
                                ? 'bi bi-sun-fill text-warning'
                                : 'bi bi-moon-stars-fill text-primary';
                    }
                    void saveProfilePreferenceFast(
                        'DarkModeEnabled',
                        isEnabled
                    ).catch(error => {
                        if (
                            error.name !==
                            'AbortError'
                        ) {
                            console.warn(
                                'فشل حفظ تفضيل الوضع الليلي:',
                                error
                            );
                        }
                    });
                }
            );
    }


    const compactModeToggle =
        document.getElementById(
            'compactModeToggle'
        );

    const sidebar =
        document.getElementById(
            'dashboardSidebar'
        );


    if (compactModeToggle) {
        const currentCompactMode =
            Boolean(
                sidebar
                    ?.classList
                    .contains(
                        'sidebar-collapsed'
                    )
            );


        compactModeToggle.checked =
            currentCompactMode;


        compactModeToggle.addEventListener(
            'change',
            function () {

                const isEnabled =
                    this.checked;

                const currentUserId =
                    String(
                        window.currentUserId ||
                        '0'
                    );


                /*
                 * نبقي LocalStorage للتوافق مع
                 * أي كود قديم، لكن الكوكي الخاص
                 * بالمستخدم هو المصدر الأساسي.
                 */

                localStorage.setItem(
                    'adminCompactMode',

                    isEnabled
                        ? 'enabled'
                        : 'disabled'
                );


                setCookie(
                    `compactMode_${currentUserId}`,

                    isEnabled
                        ? 'enabled'
                        : 'disabled'
                );


                sidebar
                    ?.classList
                    .toggle(
                        'sidebar-collapsed',
                        isEnabled
                    );

                // الكوكي المحلي يطبق الحالة فورًا؛ لا نرسل طلبًا زائدًا للسيرفر.
            }
        );
    }


    const languageSelect =
        document.getElementById(
            'languageSelect'
        );


    if (languageSelect) {
        languageSelect.addEventListener(
            'change',
            function () {

                const selectedLanguage =
                    this.value;

                this.disabled =
                    true;


                fetch(
                    '/Language/SetLanguage',
                    {
                        method:
                            'POST',

                        headers: {
                            'Content-Type':
                                'application/json'
                        },

                        body:
                            JSON.stringify({
                                Culture:
                                    selectedLanguage
                            })
                    }
                )
                    .then(response =>
                        response.json()
                    )

                    .then(data => {

                        if (data.success) {
                            window.location.reload();

                        } else {
                            this.disabled =
                                false;


                            Swal.fire(
                                'خطأ',

                                data.message ||
                                'تعذر تغيير لغة النظام.',

                                'error'
                            );
                        }
                    })

                    .catch(() => {

                        this.disabled =
                            false;


                        Swal.fire(
                            'خطأ',
                            'فشل تغيير لغة النظام.',
                            'error'
                        );
                    });
            }
        );
    }



    // =============================================================
    // 9.1 عملة عرض الأسعار — اختيار فوري وحفظ خلفي
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
            currencyStatus
                .classList
                .add('is-saving');

            currencyStatus.innerHTML =
                `<i class="bi bi-arrow-repeat"></i>
                 <span>جاري تثبيت <strong>${currency}</strong>...</span>`;

            return;
        }

        if (state === 'error') {
            currencyStatus
                .classList
                .add('is-error');

            currencyStatus.innerHTML =
                '<i class="bi bi-exclamation-circle"></i><span>تعذر حفظ العملة</span>';

            return;
        }

        currencyStatus
            .classList
            .add('is-saved');

        currencyStatus.innerHTML =
            `<i class="bi bi-check2-circle"></i>
             <span>العملة الحالية: <strong>${currency}</strong></span>`;
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

                // التغيير بصريًا فوري، بدون تعطيل أي خيار.
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

                void saveProfilePreferenceFast(
                    'PreferredCurrency',
                    selectedCurrency
                )
                    .then(data => {
                        if (
                            currentSequence !==
                            currencySaveSequence
                        ) {
                            return;
                        }

                        lastSavedCurrency =
                            data.preferredCurrency ||
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
                            error.name ===
                            'AbortError' ||
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
                            previousInput.checked =
                                true;
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



    // =============================================================
    // 10. تعطيل الحساب
    // =============================================================

    const suspendBtn =
        document.getElementById(
            'suspendAccountBtn'
        );


    if (suspendBtn) {
        suspendBtn.addEventListener(
            'click',
            function () {

                Swal.fire({
                    title:
                        'إجراء خطير!',

                    text:
                        'هل أنت متأكد من رغبتك بتعطيل حسابك كمدير عام؟',

                    icon:
                        'warning',

                    showCancelButton:
                        true,

                    confirmButtonColor:
                        '#dc3545',

                    confirmButtonText:
                        'نعم، قم بالتعطيل',

                    cancelButtonText:
                        'تراجع'

                }).then(result => {

                    if (
                        !result.isConfirmed
                    ) {
                        return;
                    }


                    suspendBtn.disabled =
                        true;


                    fetch(
                        '/Admin/Profile/SuspendAccount',
                        {
                            method:
                                'POST'
                        }
                    )
                        .then(response =>
                            response
                                .json()
                                .catch(
                                    () => ({
                                        success:
                                            response.ok
                                    })
                                )
                        )

                        .then(data => {

                            if (
                                data.success ===
                                false
                            ) {
                                suspendBtn.disabled =
                                    false;


                                Swal.fire(
                                    'خطأ',

                                    data.message ||
                                    'تعذر تعطيل الحساب.',

                                    'error'
                                );

                                return;
                            }


                            Swal.fire(
                                'تم',
                                'تم تعطيل الحساب بنجاح.',
                                'success'
                            );


                            window.setTimeout(
                                () => {
                                    window.location.href =
                                        '/Account/Logout';
                                },
                                2000
                            );
                        })

                        .catch(() => {

                            suspendBtn.disabled =
                                false;


                            Swal.fire(
                                'خطأ',
                                'فشل تعطيل الحساب.',
                                'error'
                            );
                        });
                });
            }
        );
    }


    // =============================================================
    // 10.1 تبويبات الملف الشخصي — ماوس + أسهم + Home / End
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


    function showProfileTab(button, focus = false) {
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

        const reduceMotion =
            window.matchMedia(
                '(prefers-reduced-motion: reduce)'
            ).matches;

        button.scrollIntoView({
            behavior: 'auto',
            block: 'nearest',
            inline: 'nearest'
        });
    }


    profileTabButtons.forEach((button, index) => {

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
                        const isCurrent =
                            item === button;

                        item.setAttribute(
                            'tabindex',
                            isCurrent
                                ? '0'
                                : '-1'
                        );
                    }
                );

                const target =
                    button.getAttribute(
                        'data-bs-target'
                    );

                if (target) {
                    localStorage.setItem(
                        'adminProfileActiveTab',
                        target
                    );

                    if (
                        window.history
                            ?.replaceState
                    ) {
                        window.history.replaceState(
                            null,
                            '',
                            target
                        );
                    }
                }
            }
        );
    });


    // =============================================================
    // 11. فتح التبويبات بواسطة الرابط
    // =============================================================

    function activateTabFromHash() {
        const requestedTarget =
            window.location.hash ||
            localStorage.getItem(
                'adminProfileActiveTab'
            );

        if (!requestedTarget) {
            return;
        }

        const normalizedTarget =
            requestedTarget.startsWith('#')
                ? requestedTarget
                : `#${requestedTarget}`;

        const tabButton =
            profileTabButtons.find(
                button =>
                    button.getAttribute(
                        'data-bs-target'
                    ) === normalizedTarget
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
});