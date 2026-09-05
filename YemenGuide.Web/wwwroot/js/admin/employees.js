/**
 * ==========================================================================
 * YemenGuide — Admin Employees Management
 * واجهة الموظفين + الأدوار والصلاحيات + سجل العمليات
 * ==========================================================================
 */
(() => {
    'use strict';

    const app = document.querySelector('[data-employees-app]');
    if (!app) return;

    const isArabic =
        (app.dataset.language || document.documentElement.lang || 'ar')
            .toLowerCase()
            .startsWith('ar');

    const L = (ar, en) => isArabic ? ar : en;

    const translateServerMessage = message => {
        if (isArabic || !message || typeof message !== 'string') return message;

        const map = new Map([
            ['الدور الوظيفي غير متاح. اختر دورًا نشطًا وغير محمي.', 'The selected role is unavailable. Choose an active, unprotected role.'],
            [L('كلمة المرور المؤقتة يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.', 'The temporary password must be at least 8 characters and contain an English letter and a number.'), 'The temporary password must be at least 8 characters and contain an English letter and a number.'],
            ['تم إنشاء الموظف وإرسال بيانات الدخول المؤقتة إلى بريده الإلكتروني.', 'The employee was created and the temporary credentials were emailed.'],
            ['تم إنشاء الموظف، لكن تعذر إرسال بيانات الدخول إلى البريد. يمكنك نسخها من النافذة الحالية.', 'The employee was created, but the email could not be sent. Copy the credentials from this dialog.'],
            ['تعذر إنشاء الموظف. لم يتم حفظ أي بيانات.', 'The employee could not be created. No data was saved.'],
            ['رقم الموظف غير صحيح.', 'The employee ID is invalid.'],
            ['الموظف غير موجود.', 'The employee was not found.'],
            ['حساب المدير الرئيسي محمي ولا يتم تعديله من إدارة الموظفين.', 'The primary system admin account is protected and cannot be edited here.'],
            ['الدور المحدد غير متاح.', 'The selected role is unavailable.'],
            ['تم تحديث بيانات الموظف ودوره بنجاح.', 'The employee data and role were updated successfully.'],
            ['تعذر حفظ تعديلات الموظف.', 'The employee changes could not be saved.'],
            ['لا يمكنك إيقاف حسابك الإداري من هذه الصفحة.', 'You cannot suspend your own admin account from this page.'],
            ['الحالة المطلوبة غير مدعومة هنا.', 'The requested status is not supported here.'],
            ['لا يمكن إيقاف حساب المدير الرئيسي النظامي.', 'The primary system admin account cannot be suspended.'],
            ['تم تفعيل الموظف بنجاح.', 'The employee was activated successfully.'],
            ['تم إيقاف الموظف بنجاح.', 'The employee was suspended successfully.'],
            ['تعذر تغيير حالة الموظف.', 'The employee status could not be changed.'],
            ['اسم الدور مطلوب.', 'Role name is required.'],
            ['اختر صلاحية واحدة على الأقل للدور.', 'Select at least one permission for the role.'],
            ['يوجد دور آخر بنفس الاسم.', 'Another role already uses the same name.'],
            ['تحتوي الصلاحيات المختارة على صلاحية غير موجودة أو غير نشطة.', 'The selection contains a permission that does not exist or is inactive.'],
            ['الدور غير موجود.', 'The role was not found.'],
            ['دور المدير الرئيسي محمي ولا يمكن تعديله.', 'The primary system admin role is protected and cannot be edited.'],
            ['تم حفظ الدور والصلاحيات بنجاح.', 'The role and permissions were saved successfully.'],
            ['تعذر حفظ الدور والصلاحيات.', 'The role and permissions could not be saved.'],
            ['دور المدير الرئيسي محمي ولا يمكن إيقافه.', 'The primary system admin role is protected and cannot be disabled.'],
            ['تم تفعيل الدور بنجاح.', 'The role was enabled successfully.'],
            ['تم إيقاف الدور. الموظفون المرتبطون به لن تكون صلاحياته فعالة حتى إعادة تفعيله.', 'The role was disabled. Linked employees will not receive its permissions until it is enabled again.'],
            ['تعذر تغيير حالة الدور.', 'The role status could not be changed.'],
            ['الاسم الأول والأخير مطلوبان.', 'First and last name are required.'],
            ['اسم المستخدم يجب أن يكون باللغة الإنجليزية وبدون مسافات، ويمكن استخدام . _ -.', 'Username must use English characters without spaces; . _ - are allowed.'],
            ['صيغة البريد الإلكتروني غير صحيحة.', 'The email format is invalid.'],
            ['اختر دورًا وظيفيًا للموظف.', 'Select a role for the employee.'],
            ['تأكيد كلمة المرور غير مطابق.', 'Password confirmation does not match.'],
            ['اسم المستخدم مستخدم مسبقًا.', 'The username is already in use.'],
            ['البريد الإلكتروني مستخدم مسبقًا.', 'The email is already in use.'],
            ['رقم الهاتف مستخدم مسبقًا.', 'The phone number is already in use.']
        ]);

        return map.get(message) || message;
    };

    const content = app.querySelector('[data-tab-content]');
    const drawerLayer = app.querySelector('[data-drawer-layer]');
    const drawerContent = app.querySelector('[data-drawer-content]');
    const confirmLayer = app.querySelector('[data-confirm-layer]');
    const secretLayer = app.querySelector('[data-secret-layer]');
    const toastStack = app.querySelector('[data-toast-stack]');
    const antiforgeryToken = app.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const urls = {
        employees: app.dataset.employeesUrl || '',
        roles: app.dataset.rolesUrl || '',
        audit: app.dataset.auditUrl || '',
        employeeForm: app.dataset.employeeFormUrl || '',
        roleForm: app.dataset.roleFormUrl || '',
        saveEmployee: app.dataset.saveEmployeeUrl || '',
        saveRole: app.dataset.saveRoleUrl || '',
        employeeStatus: app.dataset.statusEmployeeUrl || '',
        roleStatus: app.dataset.roleStatusUrl || '',
        availability: app.dataset.availabilityUrl || ''
    };

    const state = {
        activeTab: app.dataset.defaultTab || 'employees',
        tabQueries: {
            employees: new URLSearchParams(),
            roles: new URLSearchParams(),
            audit: new URLSearchParams()
        },
        loadingController: null,
        drawerController: null,
        confirmResolve: null,
        availabilityTimers: new Map(),
        employeePhone: null
    };

    function normalizeTab(tab) {
        const button = app.querySelector(`[data-employee-tab="${CSS.escape(tab || '')}"]`);
        if (button) return tab;
        return app.querySelector('[data-employee-tab]')?.dataset.employeeTab || 'employees';
    }

    function queryString(params) {
        const text = params?.toString?.() || '';
        return text ? `?${text}` : '';
    }

    function setLoading(message = L('جاري تجهيز البيانات...', 'Preparing data...')) {
        if (!content) return;
        content.innerHTML = `
            <div class="employees-loading-state">
                <span class="employees-loading-state__spinner"></span>
                <strong>${escapeHtml(message)}</strong>
                <small>${L('لحظة واحدة فقط', 'Just a moment')}</small>
            </div>`;
    }

    function setContentError(message) {
        if (!content) return;
        content.innerHTML = `
            <div class="employees-empty-state">
                <span><i class="bi bi-exclamation-triangle"></i></span>
                <h3>${L('تعذر تحميل البيانات', 'Unable to load data')}</h3>
                <p>${escapeHtml(message || L('أعد المحاولة بعد قليل.', 'Try again shortly.'))}</p>
                <button type="button" class="employees-btn employees-btn--primary" data-retry-tab>
                    <i class="bi bi-arrow-clockwise"></i> ${L('إعادة المحاولة', 'Retry')}
                </button>
            </div>`;
    }

    function setActiveTab(tab, updateHistory = true) {
        state.activeTab = normalizeTab(tab);

        app.querySelectorAll('[data-employee-tab]').forEach(button => {
            const active = button.dataset.employeeTab === state.activeTab;
            button.classList.toggle('active', active);
            button.setAttribute('aria-selected', active ? 'true' : 'false');
            button.tabIndex = active ? 0 : -1;
        });

        if (updateHistory) {
            const pageUrl = new URL(window.location.href);
            pageUrl.searchParams.set('tab', state.activeTab);
            history.replaceState({ employeesTab: state.activeTab }, '', pageUrl);
        }
    }

    async function fetchHtml(url, params = null, signal = null) {
        const response = await fetch(`${url}${queryString(params)}`, {
            method: 'GET',
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'text/html'
            },
            credentials: 'same-origin',
            signal
        });

        if (response.status === 403) throw new Error(L('ليس لديك صلاحية لعرض هذا القسم.', 'You do not have permission to view this section.'));
        if (response.status === 404) throw new Error(L('العنصر المطلوب غير موجود.', 'The requested item was not found.'));
        if (!response.ok) throw new Error(L('تعذر تحميل البيانات من الخادم.', 'Unable to load data from the server.'));
        return await response.text();
    }

    async function loadTab(tab, params = null, options = {}) {
        tab = normalizeTab(tab);
        const endpoint = urls[tab];
        if (!endpoint) return;

        if (params instanceof URLSearchParams) {
            state.tabQueries[tab] = new URLSearchParams(params);
        }

        const query = state.tabQueries[tab] || new URLSearchParams();
        setActiveTab(tab, options.updateHistory !== false);

        state.loadingController?.abort();
        state.loadingController = new AbortController();

        const loadingTimer = window.setTimeout(() => setLoading(), 110);

        try {
            const html = await fetchHtml(endpoint, query, state.loadingController.signal);
            window.clearTimeout(loadingTimer);
            if (tab !== state.activeTab) return;
            content.innerHTML = html;
        } catch (error) {
            window.clearTimeout(loadingTimer);
            if (error?.name === 'AbortError') return;
            setContentError(translateServerMessage(error?.message));
        }
    }

    function serializeForm(form) {
        const params = new URLSearchParams();
        new FormData(form).forEach((value, key) => {
            if (typeof value === 'string' && value.trim() !== '') {
                params.append(key, value.trim());
            }
        });
        return params;
    }

    function withToken(formData) {
        if (antiforgeryToken && !formData.has('__RequestVerificationToken')) {
            formData.append('__RequestVerificationToken', antiforgeryToken);
        }
        return formData;
    }

    async function postForm(url, formData) {
        const response = await fetch(url, {
            method: 'POST',
            body: withToken(formData),
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin'
        });

        let result;
        try {
            result = await response.json();
        } catch {
            result = { success: false, message: L('استجابة الخادم غير صالحة.', 'The server returned an invalid response.') };
        }

        if (result?.message) result.message = translateServerMessage(result.message);

        if (response.status === 403) {
            throw new Error(translateServerMessage(result.message) || L('ليس لديك صلاحية تنفيذ هذه العملية.', 'You do not have permission to perform this action.'));
        }

        if (!response.ok || !result.success) {
            throw new Error(translateServerMessage(result.message) || L('تعذر تنفيذ العملية.', 'Unable to perform the action.'));
        }

        return result;
    }

    async function openDrawer(url, params = null) {
        if (!drawerLayer || !drawerContent) return;

        drawerLayer.hidden = false;
        document.body.style.overflow = 'hidden';
        drawerContent.innerHTML = `
            <div class="employees-loading-state">
                <span class="employees-loading-state__spinner"></span>
                <strong>${L('جاري فتح النموذج...', 'Opening form...')}</strong>
                <small>${L('لحظة واحدة فقط', 'Just a moment')}</small>
            </div>`;

        state.drawerController?.abort();
        state.drawerController = new AbortController();

        try {
            drawerContent.innerHTML = await fetchHtml(url, params, state.drawerController.signal);
            initializeDrawerContent();
            drawerContent.querySelector('input:not([type="hidden"]):not(:disabled), button:not([disabled])')?.focus({ preventScroll: true });
        } catch (error) {
            if (error?.name === 'AbortError') return;
            drawerContent.innerHTML = `
                <div class="employees-empty-state">
                    <span><i class="bi bi-exclamation-triangle"></i></span>
                    <h3>${L('تعذر فتح النموذج', 'Unable to open form')}</h3>
                    <p>${escapeHtml(error?.message || L('أعد المحاولة.', 'Try again.'))}</p>
                    <button type="button" class="employees-btn employees-btn--soft" data-close-drawer>${L('إغلاق', 'Close')}</button>
                </div>`;
        }
    }

    function closeDrawer() {
        state.drawerController?.abort();

        try {
            state.employeePhone?.destroy?.();
        } catch (_) {
            // لا نوقف إغلاق النافذة إذا كانت مكتبة الهاتف قد أزيلت مسبقًا.
        }
        state.employeePhone = null;

        if (drawerLayer) drawerLayer.hidden = true;
        if (drawerContent) drawerContent.innerHTML = '';
        document.body.style.overflow = '';
        state.availabilityTimers.forEach(timer => clearTimeout(timer));
        state.availabilityTimers.clear();
    }

    function initializeDrawerContent() {
        const employeeForm = drawerContent?.querySelector('[data-employee-form]');
        if (employeeForm) initializeEmployeeForm(employeeForm);

        const roleForm = drawerContent?.querySelector('[data-role-form]');
        if (roleForm) initializeRoleForm(roleForm);
    }

    function initializeEmployeeForm(form) {
        form.dataset.currentStep = '1';
        cacheEmployeeFieldHints(form);
        renderEmployeeStep(form, 1);
        initializeRolePicker(form);
        initializeAccountAvailability(form);
        void initializeEmployeePhone(form);
        initializeEmployeeLiveValidation(form);
        initializeTemporaryPasswordMode(form);
        initializeEmployeePasswordStrength(form);
    }

    function cacheEmployeeFieldHints(form) {
        form.querySelectorAll('[data-field-error]').forEach(element => {
            if (!element.dataset.defaultMessage) {
                element.dataset.defaultMessage = element.textContent?.trim() || '';
            }
        });
    }

    function restoreFieldHint(input) {
        const field = input?.closest('.employee-field');
        if (!field) return;
        const message = field.querySelector(`[data-field-error="${CSS.escape(input.name)}"]`)
            || field.querySelector('[data-field-error]');
        if (message) {
            message.textContent = message.dataset.defaultMessage || '';
        }
    }

    function setFieldVisualState(input, stateName) {
        const field = input?.closest('.employee-field');
        if (!field) return;

        field.classList.remove('is-invalid', 'is-valid');
        if (stateName === 'error') field.classList.add('is-invalid');
        if (stateName === 'success') field.classList.add('is-valid');

        const stateIcon = field.querySelector(`[data-field-state="${CSS.escape(input.name)}"]`);
        if (stateIcon) {
            stateIcon.className = 'employee-control__state';
            stateIcon.innerHTML = '';
            if (stateName === 'success') {
                stateIcon.classList.add('is-ok');
                stateIcon.innerHTML = '<i class="bi bi-check-lg"></i>';
            } else if (stateName === 'error') {
                stateIcon.classList.add('is-bad');
                stateIcon.innerHTML = '<i class="bi bi-x-lg"></i>';
            }
        }
    }

    function validateNameValue(value) {
        const text = String(value || '').trim();
        if (text.length < 2 || text.length > 60) return false;
        return /^[\p{L}\p{M}][\p{L}\p{M}\s'’-]*$/u.test(text);
    }

    function validateUsernameValue(value) {
        return /^[a-zA-Z0-9_.-]{3,50}$/.test(String(value || '').trim());
    }

    function validateEmailValue(value) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value || '').trim());
    }

    function initializeEmployeeLiveValidation(form) {
        const firstName = form.querySelector('input[name="FirstName"]');
        const lastName = form.querySelector('input[name="LastName"]');
        const username = form.querySelector('input[name="Username"]');
        const email = form.querySelector('input[name="ContactInfo"]');
        const password = form.querySelector('input[name="TemporaryPassword"]');
        const confirmPassword = form.querySelector('input[name="ConfirmTemporaryPassword"]');

        const bind = (input, validator, errorMessage) => {
            if (!input || input.disabled) return;

            const validate = () => {
                const value = input.value.trim();
                restoreFieldHint(input);

                if (!value) {
                    setFieldVisualState(input, null);
                    return;
                }

                const ok = validator(value);
                setFieldVisualState(input, ok ? 'success' : 'error');
                if (!ok) {
                    const field = input.closest('.employee-field');
                    const message = field?.querySelector(`[data-field-error="${CSS.escape(input.name)}"]`)
                        || field?.querySelector('[data-field-error]');
                    if (message) message.textContent = errorMessage;
                }
            };

            input.addEventListener('input', validate);
            input.addEventListener('blur', validate);
        };

        bind(firstName, validateNameValue, L('اكتب اسمًا صحيحًا من حرفين على الأقل وبدون أرقام.', 'Enter a valid name with at least two letters and no numbers.'));
        bind(lastName, validateNameValue, L('اكتب اسمًا صحيحًا من حرفين على الأقل وبدون أرقام.', 'Enter a valid name with at least two letters and no numbers.'));
        bind(username, validateUsernameValue, L('اسم المستخدم إنجليزي فقط وبدون مسافات؛ يسمح بـ . _ -', 'Username must be in English with no spaces; . _ - are allowed.'));
        bind(email, validateEmailValue, L('أدخل بريدًا إلكترونيًا صحيحًا مثل name@example.com.', 'Enter a valid email such as name@example.com.'));

        if (password && confirmPassword) {
            const validateMatch = () => {
                restoreFieldHint(confirmPassword);
                const hasAnyValue = Boolean(password.value || confirmPassword.value);
                if (!hasAnyValue) {
                    setFieldVisualState(confirmPassword, null);
                    return;
                }

                const matches = password.value === confirmPassword.value && /^(?=.*[A-Za-z])(?=.*\\d).{8,}$/.test(password.value);
                setFieldVisualState(confirmPassword, matches ? 'success' : 'error');
                if (!matches) {
                    const message = confirmPassword.closest('.employee-field')?.querySelector('[data-field-error="ConfirmTemporaryPassword"]');
                    if (message) message.textContent = L('تأكيد كلمة المرور يجب أن يطابق كلمة المرور المؤقتة.', 'Password confirmation must match the temporary password.');
                }
            };

            password.addEventListener('input', validateMatch);
            confirmPassword.addEventListener('input', validateMatch);
            confirmPassword.addEventListener('blur', validateMatch);
        }
    }

    let phoneLibraryPromise = null;

    function loadEmployeesStylesheetOnce(href, id) {
        if (document.getElementById(id)) return;
        const link = document.createElement('link');
        link.id = id;
        link.rel = 'stylesheet';
        link.href = href;
        document.head.appendChild(link);
    }

    function loadEmployeesScriptOnce(src, id) {
        const existing = document.getElementById(id);
        if (existing) {
            return new Promise((resolve, reject) => {
                if (typeof window.intlTelInput === 'function') {
                    resolve();
                    return;
                }
                existing.addEventListener('load', resolve, { once: true });
                existing.addEventListener('error', reject, { once: true });
            });
        }
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.id = id;
            script.src = src;
            script.async = true;
            script.addEventListener('load', resolve, { once: true });
            script.addEventListener('error', reject, { once: true });
            document.head.appendChild(script);
        });
    }

    function ensureEmployeePhoneLibrary() {
        if (typeof window.intlTelInput === 'function') return Promise.resolve();
        if (phoneLibraryPromise) return phoneLibraryPromise;

        loadEmployeesStylesheetOnce(
            'https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/css/intlTelInput.css',
            'employeesIntlTelInputCss'
        );

        phoneLibraryPromise = loadEmployeesScriptOnce(
            'https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/intlTelInput.min.js',
            'employeesIntlTelInputJs'
        ).catch(error => {
            phoneLibraryPromise = null;
            throw error;
        });

        return phoneLibraryPromise;
    }

    async function initializeEmployeePhone(form) {
        const input = form.querySelector('input[name="PhoneNumber"]');
        if (!input || input.disabled) return;

        const validate = () => {
            const field = input.closest('.employee-field');
            const message = field?.querySelector('[data-field-error="PhoneNumber"]');
            if (!field) return true;
            restoreFieldHint(input);
            if (!input.value.trim()) {
                setFieldVisualState(input, null);
                return false;
            }
            const valid = state.employeePhone?.isValidNumber
                ? state.employeePhone.isValidNumber()
                : /^\+?[0-9\s()-]{8,20}$/.test(input.value.trim());
            setFieldVisualState(input, valid ? 'success' : 'error');
            if (!valid && message) {
                message.textContent = L(
                    'رقم الهاتف غير صالح. اختر الدولة ثم اكتب الرقم بدون تكرار رمز الدولة.',
                    'Invalid phone number. Select the country and enter the number without repeating the country code.'
                );
            }
            return valid;
        };

        input.addEventListener('input', validate);
        input.addEventListener('blur', validate);

        try {
            await ensureEmployeePhoneLibrary();
            if (typeof window.intlTelInput !== 'function') return;

            state.employeePhone = window.intlTelInput(input, {
                initialCountry: 'ye',
                preferredCountries: ['ye', 'sa', 'ae', 'eg', 'qa'],
                separateDialCode: true,
                formatOnDisplay: true,
                autoPlaceholder: 'polite',
                dropdownContainer: document.body,
                utilsScript: 'https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js'
            });

            if (input.value.trim().startsWith('+')) {
                state.employeePhone.setNumber(input.value.trim());
            }
            validate();
        } catch (error) {
            console.warn(L('تعذر تهيئة حقل الهاتف الدولي.', 'International phone input could not be initialized.'), error);
            state.employeePhone = null;
        }
    }

    function normalizeEmployeePhoneForSubmit(form) {
        const input = form.querySelector('input[name="PhoneNumber"]');
        if (!input || input.disabled) return true;

        if (typeof form._validateEmployeePhone === 'function' && !form._validateEmployeePhone()) {
            return false;
        }

        if (state.employeePhone?.getNumber) {
            const fullNumber = state.employeePhone.getNumber();
            if (fullNumber) input.value = fullNumber;
        }

        return /^\+[1-9]\d{7,14}$/.test(input.value.trim());
    }


    function generateSecureTemporaryPassword(length = 12) {
        const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
        const lower = 'abcdefghijkmnopqrstuvwxyz';
        const digits = '23456789';
        const symbols = '!@#$%*-_';
        const all = upper + lower + digits + symbols;
        const pick = chars => {
            const array = new Uint32Array(1);
            crypto.getRandomValues(array);
            return chars[array[0] % chars.length];
        };

        const chars = [pick(upper), pick(lower), pick(digits), pick(symbols)];
        while (chars.length < Math.max(8, length)) chars.push(pick(all));

        for (let i = chars.length - 1; i > 0; i--) {
            const array = new Uint32Array(1);
            crypto.getRandomValues(array);
            const j = array[0] % (i + 1);
            [chars[i], chars[j]] = [chars[j], chars[i]];
        }

        return chars.join('');
    }

    function syncTemporaryPasswordMode(form) {
        const mode = form.querySelector('input[name="PasswordMode"]:checked')?.value || 'auto';
        const serverPassword = form.querySelector('input[name="TemporaryPassword"]');
        const serverConfirm = form.querySelector('input[name="ConfirmTemporaryPassword"]');
        const manual = form.querySelector('[data-manual-password]');
        const manualConfirm = form.querySelector('[data-manual-password-confirm]');

        if (!serverPassword || serverPassword.disabled) return true;

        if (mode === 'manual') {
            serverPassword.readOnly = false;
            serverPassword.type = 'hidden';
            serverPassword.value = manual?.value || '';
            if (serverConfirm) serverConfirm.value = manualConfirm?.value || '';
            return /^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(serverPassword.value) &&
                serverPassword.value === (serverConfirm?.value || '');
        }

        serverPassword.readOnly = true;
        serverPassword.type = 'text';
        if (!serverPassword.value || !/^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(serverPassword.value)) {
            serverPassword.value = generateSecureTemporaryPassword();
        }
        if (serverConfirm) serverConfirm.value = serverPassword.value;
        return true;
    }

    function initializeTemporaryPasswordMode(form) {
        const modes = [...form.querySelectorAll('input[name="PasswordMode"]')];
        if (!modes.length) return;

        const autoPanel = form.querySelector('[data-generated-password-panel]');
        const manualPanel = form.querySelector('[data-manual-password-panel]');
        const serverPassword = form.querySelector('input[name="TemporaryPassword"]');
        const serverConfirm = form.querySelector('input[name="ConfirmTemporaryPassword"]');
        const manual = form.querySelector('[data-manual-password]');
        const manualConfirm = form.querySelector('[data-manual-password-confirm]');

        const render = () => {
            const mode = form.querySelector('input[name="PasswordMode"]:checked')?.value || 'auto';

            form.querySelectorAll('[data-password-mode-card]').forEach(card => {
                card.classList.toggle('is-selected', card.dataset.passwordModeCard === mode);
            });

            if (autoPanel) autoPanel.hidden = mode !== 'auto';
            if (manualPanel) manualPanel.hidden = mode !== 'manual';

            if (mode === 'auto') {
                if (serverPassword && !serverPassword.value) {
                    serverPassword.value = generateSecureTemporaryPassword();
                }
                if (serverConfirm && serverPassword) serverConfirm.value = serverPassword.value;
            } else {
                if (serverPassword) serverPassword.value = manual?.value || '';
                if (serverConfirm) serverConfirm.value = manualConfirm?.value || '';
            }
        };

        modes.forEach(input => input.addEventListener('change', render));

        manual?.addEventListener('input', () => {
            if (serverPassword) serverPassword.value = manual.value;
        });
        manualConfirm?.addEventListener('input', () => {
            if (serverConfirm) serverConfirm.value = manualConfirm.value;
        });

        form.querySelector('[data-regenerate-password]')?.addEventListener('click', () => {
            if (!serverPassword) return;
            serverPassword.value = generateSecureTemporaryPassword();
            if (serverConfirm) serverConfirm.value = serverPassword.value;
            setFieldVisualState(serverPassword, 'success');
        });

        form.querySelector('[data-copy-generated-password]')?.addEventListener('click', async () => {
            const value = serverPassword?.value || '';
            if (!value) return;
            try {
                await navigator.clipboard.writeText(value);
                showToast(L('تم نسخ كلمة المرور المؤقتة.', 'Temporary password copied.'));
            } catch {
                serverPassword?.select?.();
                showToast(L('تم تحديد كلمة المرور؛ انسخها يدويًا.', 'The password is selected; copy it manually.'));
            }
        });

        render();
    }

    function initializeEmployeePasswordStrength(form) {
        const input = form.querySelector('input[name="TemporaryPassword"]');
        const meter = form.querySelector('[data-employee-password-strength]');
        const copy = form.querySelector('[data-password-strength-copy]');
        if (!input || !meter) return;

        const update = () => {
            const value = input.value;
            let strength = 0;

            if (value.length >= 8) strength++;
            if (/[A-Za-z]/.test(value) && /\d/.test(value)) strength++;
            if (value.length >= 10 && /[^A-Za-z0-9]/.test(value)) strength++;

            if (!value) strength = 0;
            meter.dataset.strength = String(strength);

            if (copy) {
                copy.textContent = !value
                    ? L('قوة كلمة المرور', 'Password strength')
                    : strength === 1
                        ? L('ضعيفة', 'Weak')
                        : strength === 2
                            ? L('جيدة', 'Good')
                            : L('قوية', 'Strong');
            }

            if (value) {
                restoreFieldHint(input);
                const ok = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(value);
                setFieldVisualState(input, ok ? 'success' : 'error');
                if (!ok) {
                    const message = input.closest('.employee-field')?.querySelector('[data-field-error="TemporaryPassword"]');
                    if (message) message.textContent = L('كلمة المرور المؤقتة يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.', 'The temporary password must be at least 8 characters and contain an English letter and a number.');
                }
            } else {
                setFieldVisualState(input, null);
                restoreFieldHint(input);
            }
        };

        input.addEventListener('input', update);
        update();
    }

    function renderEmployeeStep(form, step) {
        step = Math.max(1, Math.min(3, Number(step) || 1));
        form.dataset.currentStep = String(step);

        form.querySelectorAll('[data-form-page]').forEach(page => {
            page.classList.toggle('is-active', Number(page.dataset.formPage) === step);
        });

        form.querySelectorAll('[data-form-step-button]').forEach(button => {
            const number = Number(button.dataset.formStepButton);
            button.classList.toggle('is-active', number === step);
            button.classList.toggle('is-complete', number < step);
        });

        const prev = form.querySelector('[data-form-prev]');
        const next = form.querySelector('[data-form-next]');
        const submit = form.querySelector('[data-form-submit]');
        const copy = form.querySelector('[data-step-copy]');

        if (prev) prev.hidden = step === 1;
        if (next) next.hidden = step === 3;
        if (submit) submit.hidden = step !== 3;
        if (copy) copy.textContent = isArabic ? `الخطوة ${step} من 3` : `Step ${step} of 3`;

        form.querySelector('.employee-form-body')?.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function clearFieldErrors(form, page = null) {
        const scope = page || form;
        scope.querySelectorAll('.employee-field.is-invalid').forEach(field => field.classList.remove('is-invalid'));
        scope.querySelectorAll('[data-field-error]').forEach(element => {
            if (element.closest('[data-form-page]') === page || !page) {
                element.textContent = element.dataset.defaultMessage || element.textContent || '';
            }
        });
    }

    function setFieldError(input, message) {
        const field = input?.closest('.employee-field');
        if (!field) return;
        field.classList.remove('is-valid');
        field.classList.add('is-invalid');
        const error = field.querySelector(`[data-field-error="${CSS.escape(input.name)}"]`) || field.querySelector('[data-field-error]') || field.querySelector('small');
        if (error) error.textContent = message;
    }

    function validateEmployeeStep(form, step) {
        const page = form.querySelector(`[data-form-page="${step}"]`);
        if (!page) return true;
        clearFieldErrors(form, page);

        let valid = true;
        const enabledInputs = page.querySelectorAll('input:not(:disabled), select:not(:disabled), textarea:not(:disabled)');

        enabledInputs.forEach(input => {
            if (!input.checkValidity()) {
                valid = false;
                setFieldError(input, validationMessage(input));
            }
        });

        if (step === 1) {
            const firstName = form.querySelector('input[name="FirstName"]');
            const lastName = form.querySelector('input[name="LastName"]');
            const username = form.querySelector('input[name="Username"]');
            const email = form.querySelector('input[name="ContactInfo"]');
            const phone = form.querySelector('input[name="PhoneNumber"]');

            if (firstName && !firstName.disabled && !validateNameValue(firstName.value)) {
                valid = false;
                setFieldError(firstName, L('اكتب اسمًا صحيحًا من حرفين على الأقل وبدون أرقام.', 'Enter a valid name with at least two letters and no numbers.'));
            }

            if (lastName && !lastName.disabled && !validateNameValue(lastName.value)) {
                valid = false;
                setFieldError(lastName, L('اكتب اسمًا صحيحًا من حرفين على الأقل وبدون أرقام.', 'Enter a valid name with at least two letters and no numbers.'));
            }

            if (username && !username.disabled && !validateUsernameValue(username.value)) {
                valid = false;
                setFieldError(username, 'اسم المستخدم إنجليزي فقط وبدون مسافات؛ يسمح بالأرقام و . _ -');
            }

            if (email && !email.disabled && !validateEmailValue(email.value)) {
                valid = false;
                setFieldError(email, L('أدخل بريدًا إلكترونيًا صحيحًا.', 'Enter a valid email address.'));
            }

            if (phone && !phone.disabled) {
                const phoneValid = typeof form._validateEmployeePhone === 'function'
                    ? form._validateEmployeePhone()
                    : /^\+?[0-9\s()-]{8,20}$/.test(phone.value.trim());

                if (!phoneValid) {
                    valid = false;
                    setFieldError(phone, L('رقم الهاتف غير صحيح. اختر الدولة ثم أدخل الرقم.', 'Invalid phone number. Select the country and enter the number.'));
                }
            }

            const badAvailability = page.querySelector('[data-account-status].is-bad');
            if (badAvailability) {
                valid = false;
                const targetName = badAvailability.dataset.accountStatus === 'email' ? 'ContactInfo' : 'Username';
                setFieldError(form.querySelector(`[name="${targetName}"]`), L('هذه القيمة مستخدمة في حساب آخر.', 'This value is already used by another account.'));
            }
        }

        if (step === 2) {
            const serverPassword = form.querySelector('input[name="TemporaryPassword"]');
            const serverConfirm = form.querySelector('input[name="ConfirmTemporaryPassword"]');
            const mode = form.querySelector('input[name="PasswordMode"]:checked')?.value;

            if (serverPassword && !serverPassword.disabled) {
                syncTemporaryPasswordMode(form);

                const passwordValue = serverPassword.value || '';
                const confirmValue = serverConfirm?.value || '';
                const policyOk = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(passwordValue);

                if (!policyOk) {
                    valid = false;
                    if (mode === 'manual') {
                        const manual = form.querySelector('[data-manual-password]');
                        manual?.closest('.employee-field')?.classList.add('is-invalid');
                    } else {
                        setFieldError(serverPassword, 'كلمة المرور يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.');
                    }
                    showToast(L('راجع كلمة المرور المؤقتة قبل المتابعة.', 'Review the temporary password before continuing.'), true);
                }

                if (passwordValue !== confirmValue) {
                    valid = false;
                    const manualConfirm = form.querySelector('[data-manual-password-confirm]');
                    manualConfirm?.closest('.employee-field')?.classList.add('is-invalid');
                    showToast(L('تأكيد كلمة المرور غير مطابق.', 'Password confirmation does not match.'), true);
                }
            }
        }

        if (step === 3 && !form.querySelector('input[name="AdminRoleId"]:checked')) {
            valid = false;
            showToast(L('اختر دورًا وظيفيًا للموظف قبل الحفظ.', 'Select an employee role before saving.'), true);
        }

        if (!valid) {
            page.querySelector('.is-invalid input, .is-invalid textarea, .is-invalid select')?.focus();
        }
        return valid;
    }

    function validationMessage(input) {
        const value = input.value?.trim?.() || '';
        if (input.validity.valueMissing) return L('هذا الحقل مطلوب.', 'This field is required.');
        if (input.validity.typeMismatch) return L('صيغة البيانات غير صحيحة.', 'The value format is invalid.');
        if (input.validity.patternMismatch) return L('استخدم حروفًا إنجليزية وأرقامًا فقط بدون مسافات.', 'Use English letters and numbers only, without spaces.');
        if (input.validity.tooShort) return isArabic ? `الحد الأدنى ${input.minLength} أحرف.` : `Minimum ${input.minLength} characters.`;
        if (input.validity.tooLong) return isArabic ? `الحد الأقصى ${input.maxLength} حرفًا.` : `Maximum ${input.maxLength} characters.`;
        if (!value) return L('راجع هذا الحقل.', 'Review this field.');
        return L('القيمة المدخلة غير صحيحة.', 'The entered value is invalid.');
    }

    function initializeRolePicker(form) {
        const choices = [...form.querySelectorAll('[data-role-choice]')];
        const checked = form.querySelector('input[name="AdminRoleId"]:checked');
        if (checked) selectRoleChoice(checked.closest('[data-role-choice]'), form);

        choices.forEach(choice => {
            choice.addEventListener('click', event => {
                event.preventDefault();

                const radio = choice.querySelector('input[type="radio"]');
                if (!radio || radio.disabled) return;

                const wasSelected = radio.checked;
                form.querySelectorAll('input[name="AdminRoleId"]').forEach(input => {
                    if (!input.disabled) input.checked = false;
                });

                if (wasSelected) {
                    clearRoleChoice(form);
                    return;
                }

                radio.checked = true;
                selectRoleChoice(choice, form);
            });
        });
    }

    function clearRoleChoice(form) {
        form.querySelectorAll('[data-role-choice]').forEach(item => item.classList.remove('is-selected'));
        const preview = form.querySelector('[data-role-preview]');
        if (preview) {
            preview.innerHTML = `
                <div class="employee-role-preview__empty">
                    <i class="bi bi-hand-index-thumb"></i>
                    <span>${L('لم يتم اختيار دور. اختر دورًا لعرض صلاحياته.', 'No role selected. Select a role to preview its permissions.')}</span>
                </div>`;
        }
    }

    function selectRoleChoice(choice, form) {
        if (!choice) return;
        form.querySelectorAll('[data-role-choice]').forEach(item => item.classList.toggle('is-selected', item === choice));
        const preview = form.querySelector('[data-role-preview]');
        const template = choice.querySelector('[data-role-preview-template]');
        if (preview && template) {
            preview.innerHTML = template.innerHTML.trim() || `
                <div class="employee-role-preview__empty"><i class="bi bi-info-circle"></i><span>${L('هذا الدور لا يحتوي على صلاحيات.', 'This role has no permissions.')}</span></div>`;
        }
    }

    function initializeAccountAvailability(form) {
        const username = form.querySelector('input[name="Username"]');
        const email = form.querySelector('input[name="ContactInfo"]');
        const userId = form.querySelector('input[name="UserId"]')?.value || '';

        const setup = (input, type) => {
            if (!input || input.disabled) return;
            input.addEventListener('input', () => {
                const status = form.querySelector(`[data-account-status="${type}"]`);
                if (status) {
                    status.className = '';
                    status.innerHTML = '';
                }
                const oldTimer = state.availabilityTimers.get(type);
                if (oldTimer) clearTimeout(oldTimer);

                if (input.value.trim().length < (type === 'username' ? 3 : 5)) return;
                const timer = setTimeout(() => checkAvailability(form, type, userId), 450);
                state.availabilityTimers.set(type, timer);
            });
            input.addEventListener('blur', () => {
                if (input.value.trim()) checkAvailability(form, type, userId);
            });
        };

        setup(username, 'username');
        setup(email, 'email');
    }

    async function checkAvailability(form, type, userId) {
        const username = form.querySelector('input[name="Username"]')?.value.trim() || '';
        const email = form.querySelector('input[name="ContactInfo"]')?.value.trim() || '';
        const status = form.querySelector(`[data-account-status="${type}"]`);
        if (!status || !urls.availability) return;

        if (type === 'username' && !/^[a-zA-Z0-9_.-]{3,50}$/.test(username)) return;
        if (type === 'email' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return;

        status.className = 'is-checking';
        status.innerHTML = '<i class="bi bi-three-dots"></i>';

        const params = new URLSearchParams();
        if (username) params.set('username', username);
        if (email) params.set('email', email);
        if (userId) params.set('excludeUserId', userId);

        try {
            const response = await fetch(`${urls.availability}?${params}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });
            if (!response.ok) return;
            const result = await response.json();
            const available = type === 'username' ? result.usernameAvailable : result.emailAvailable;
            status.className = available ? 'is-ok' : 'is-bad';
            status.innerHTML = `<i class="bi ${available ? 'bi-check-lg' : 'bi-x-lg'}"></i>`;

            const input = form.querySelector(type === 'username'
                ? 'input[name="Username"]'
                : 'input[name="ContactInfo"]');

            if (input) {
                setFieldVisualState(input, available ? 'success' : 'error');
                if (!available) {
                    const message = input.closest('.employee-field')?.querySelector(`[data-field-error="${CSS.escape(input.name)}"]`);
                    if (message) message.textContent = type === 'username'
                        ? L('اسم المستخدم مستخدم مسبقًا، اختر اسمًا آخر.', 'Username is already in use; choose another.')
                        : L('البريد الإلكتروني مستخدم في حساب آخر.', 'Email is already used by another account.');
                } else {
                    restoreFieldHint(input);
                }
            }
        } catch {
            status.className = '';
            status.innerHTML = '';
        }
    }

    function initializeRoleForm(form) {
        updatePermissionCounts(form);

        form.querySelectorAll('[data-permission-group]').forEach(group => {
            const selectAll = group.querySelector('[data-group-select-all]');

            if (selectAll && !selectAll.disabled) {
                selectAll.addEventListener('change', () => {
                    group
                        .querySelectorAll('input[name="PermissionIds"]:not(:disabled)')
                        .forEach(input => {
                            input.checked = selectAll.checked;
                            input
                                .closest('[data-permission-item]')
                                ?.classList
                                .toggle('is-selected', input.checked);
                        });

                    updatePermissionCounts(form);
                });
            }
        });

        form.querySelectorAll('input[name="PermissionIds"]').forEach(input => {
            input.addEventListener('change', () => {
                input
                    .closest('[data-permission-item]')
                    ?.classList
                    .toggle('is-selected', input.checked);

                updatePermissionCounts(form);
            });
        });

        const search = form.querySelector('[data-permission-search]');
        const clearSearch = form.querySelector('[data-clear-permission-search]');

        if (search) {
            search.addEventListener('input', event => {
                const value = event.target.value || '';
                filterPermissions(form, value);

                if (clearSearch) {
                    clearSearch.hidden = !String(value).trim();
                }
            });
        }

        clearSearch?.addEventListener('click', () => {
            if (!search) return;

            search.value = '';
            clearSearch.hidden = true;
            filterPermissions(form, '');
            search.focus();
        });

        form.querySelector('[data-permissions-select-all]')?.addEventListener('click', () => {
            setAllRolePermissions(form, true);
        });

        form.querySelector('[data-permissions-clear-all]')?.addEventListener('click', () => {
            setAllRolePermissions(form, false);
        });
    }

    function togglePermissionGroup(button) {
        const id = button.dataset.togglePermissionGroup;
        if (!id) return;

        const scope = button.closest('[data-role-form]') || drawerContent;
        const body = scope?.querySelector(`#${CSS.escape(id)}`);
        if (!body) return;

        const open = !body.classList.contains('is-open');
        body.classList.toggle('is-open', open);
        button.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    function setAllRolePermissions(form, checked) {
        form
            .querySelectorAll('input[name="PermissionIds"]:not(:disabled)')
            .forEach(input => {
                input.checked = checked;
                input
                    .closest('[data-permission-item]')
                    ?.classList
                    .toggle('is-selected', checked);
            });

        updatePermissionCounts(form);
    }

    function updatePermissionCounts(form) {
        let total = 0;

        form.querySelectorAll('[data-permission-group]').forEach(group => {
            const inputs = [
                ...group.querySelectorAll(
                    'input[type="checkbox"][name="PermissionIds"]'
                )
            ];

            const selected = inputs.filter(input => input.checked).length;
            total += selected;

            const counter = group.querySelector('[data-group-selected-count]');
            if (counter) counter.textContent = String(selected);

            group.classList.toggle('has-selection', selected > 0);

            const groupTitleMeta = group.querySelector('.role-v6-group__title small');
            if (groupTitleMeta) {
                const oldMarker = groupTitleMeta.querySelector('[data-dynamic-selected-label]');
                oldMarker?.remove();

                if (selected > 0) {
                    const marker = document.createElement('em');
                    marker.dataset.dynamicSelectedLabel = '';
                    marker.textContent = isArabic ? ` • ${selected} محددة` : ` • ${selected} selected`;
                    groupTitleMeta.appendChild(marker);
                }
            }

            const selectAll = group.querySelector('[data-group-select-all]');
            if (selectAll && !selectAll.disabled) {
                const selectable = inputs.filter(input => !input.disabled);
                const selectableSelected = selectable.filter(input => input.checked).length;

                selectAll.checked =
                    selectable.length > 0 &&
                    selectableSelected === selectable.length;

                selectAll.indeterminate =
                    selectableSelected > 0 &&
                    selectableSelected < selectable.length;
            }
        });

        const totalCounter = form.querySelector('[data-selected-permissions-count]');
        if (totalCounter) totalCounter.textContent = String(total);
    }

    function filterPermissions(form, rawTerm) {
        const term = String(rawTerm || '').trim().toLowerCase();
        let visibleGroups = 0;

        form.querySelectorAll('[data-permission-group]').forEach(group => {
            let visibleItems = 0;

            group.querySelectorAll('[data-permission-item]').forEach(item => {
                const text = String(
                    item.dataset.permissionSearchText ||
                    item.textContent ||
                    ''
                ).toLowerCase();

                const show = !term || text.includes(term);
                item.classList.toggle('is-filter-hidden', !show);
                if (show) visibleItems++;
            });

            const groupName = String(group.dataset.groupName || '').toLowerCase();
            const groupNameMatches = Boolean(term && groupName.includes(term));

            if (groupNameMatches) {
                group
                    .querySelectorAll('[data-permission-item]')
                    .forEach(item => item.classList.remove('is-filter-hidden'));

                visibleItems = group.querySelectorAll('[data-permission-item]').length;
            }

            const hideGroup = visibleItems === 0;
            group.classList.toggle('is-hidden', hideGroup);

            if (!hideGroup) {
                visibleGroups++;
            }

            if (term && !hideGroup) {
                group
                    .querySelector('[data-permission-group-body]')
                    ?.classList
                    .add('is-open');

                group
                    .querySelector('[data-toggle-permission-group]')
                    ?.setAttribute('aria-expanded', 'true');
            }
        });

        const noResults = form.querySelector('[data-permission-no-results]');
        if (noResults) noResults.hidden = visibleGroups > 0;
    }

    async function submitEmployeeForm(form) {
        syncTemporaryPasswordMode(form);

        for (let step = 1; step <= 3; step++) {
            if (!validateEmployeeStep(form, step)) {
                renderEmployeeStep(form, step);
                return;
            }
        }

        if (!normalizeEmployeePhoneForSubmit(form)) {
            const phone = form.querySelector('input[name="PhoneNumber"]');
            if (phone) setFieldError(phone, L('رقم الهاتف غير صحيح. اختر الدولة ثم أدخل رقمًا صالحًا.', 'Invalid phone number. Select a country and enter a valid number.'));
            renderEmployeeStep(form, 1);
            phone?.focus();
            return;
        }

        const submit = form.querySelector('[data-form-submit]');
        setButtonBusy(submit, true, L('جاري الحفظ...', 'Saving...'));

        try {
            const result = await postForm(urls.saveEmployee, new FormData(form));
            closeDrawer();
            showToast(translateServerMessage(result.message) || L('تم حفظ الموظف بنجاح.', 'Employee saved successfully.'));
            await loadTab('employees', state.tabQueries.employees, { updateHistory: false });
            refreshTopCounts('employees');
            if (result.temporaryPassword) showSecret(result);
        } catch (error) {
            showToast(error.message, true);
        } finally {
            setButtonBusy(submit, false);
        }
    }

    async function submitRoleForm(form) {
        const roleName = form.querySelector('[name="RoleName"]');
        if (roleName && !roleName.checkValidity()) {
            setFieldError(roleName, validationMessage(roleName));
            roleName.focus();
            return;
        }

        const selected = form.querySelectorAll('input[name="PermissionIds"]:checked').length;
        if (selected === 0) {
            showToast(L('اختر صلاحية واحدة على الأقل للدور.', 'Select at least one permission for the role.'), true);
            return;
        }

        const submit = form.querySelector('[data-role-submit]');
        setButtonBusy(submit, true, L('جاري الحفظ...', 'Saving...'));

        try {
            const result = await postForm(urls.saveRole, new FormData(form));
            closeDrawer();
            showToast(translateServerMessage(result.message) || L('تم حفظ الدور والصلاحيات.', 'Role and permissions saved.'));
            await loadTab('roles', state.tabQueries.roles, { updateHistory: false });
            refreshTopCounts('roles');
        } catch (error) {
            showToast(error.message, true);
        } finally {
            setButtonBusy(submit, false);
        }
    }

    function setButtonBusy(button, busy, busyText = '') {
        if (!button) return;
        if (busy) {
            button.dataset.originalHtml = button.innerHTML;
            button.disabled = true;
            button.innerHTML = `<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> ${escapeHtml(busyText)}`;
        } else {
            button.disabled = false;
            if (button.dataset.originalHtml) {
                button.innerHTML = button.dataset.originalHtml;
                delete button.dataset.originalHtml;
            }
        }
    }

    function confirmAction(options = {}) {
        if (!confirmLayer) return Promise.resolve(false);
        if (state.confirmResolve) state.confirmResolve(false);

        const title = confirmLayer.querySelector('[data-confirm-title]');
        const message = confirmLayer.querySelector('[data-confirm-message]');
        const icon = confirmLayer.querySelector('[data-confirm-icon]');
        const accept = confirmLayer.querySelector('[data-confirm-accept]');

        if (title) title.textContent = options.title || L('تأكيد العملية', 'Confirm action');
        if (message) message.textContent = options.message || L('هل تريد المتابعة؟', 'Do you want to continue?');
        if (icon) icon.innerHTML = `<i class="bi ${options.icon || 'bi-exclamation-lg'}"></i>`;
        if (accept) {
            accept.textContent = options.acceptText || L('تأكيد', 'Confirm');
            accept.classList.toggle('employees-btn--danger', options.danger !== false);
            accept.classList.toggle('employees-btn--primary', options.danger === false);
        }

        confirmLayer.hidden = false;
        document.body.style.overflow = 'hidden';
        accept?.focus();

        return new Promise(resolve => {
            state.confirmResolve = result => {
                confirmLayer.hidden = true;
                if (drawerLayer?.hidden !== false) document.body.style.overflow = '';
                state.confirmResolve = null;
                resolve(result);
            };
        });
    }

    function finishConfirm(value) {
        if (state.confirmResolve) state.confirmResolve(Boolean(value));
    }

    async function changeEmployeeStatus(button) {
        const userId = button.dataset.changeEmployeeStatus;
        const nextStatus = button.dataset.nextStatus;
        const name = button.dataset.employeeName || L('الموظف', 'employee');
        const isSuspend = nextStatus === 'Suspended';

        const approved = await confirmAction({
            title: isSuspend ? L('إيقاف حساب الموظف', 'Suspend employee account') : L('إعادة تفعيل الموظف', 'Reactivate employee'),
            message: isSuspend
                ? `سيتم منع ${name} من استخدام صلاحيات الإدارة حتى إعادة التفعيل.`
                : `سيتم إعادة تفعيل حساب ${name} وصلاحيات دوره.`,
            acceptText: isSuspend ? L('إيقاف الحساب', 'Suspend account') : L('تفعيل الحساب', 'Activate account'),
            icon: isSuspend ? 'bi-pause-circle' : 'bi-play-circle',
            danger: isSuspend
        });
        if (!approved) return;

        const data = new FormData();
        data.append('UserId', userId);
        data.append('EmploymentStatus', nextStatus);

        try {
            const result = await postForm(urls.employeeStatus, data);
            showToast(result.message);
            await loadTab('employees', state.tabQueries.employees, { updateHistory: false });
        } catch (error) {
            showToast(error.message, true);
        }
    }

    async function changeRoleStatus(button) {
        const roleId = button.dataset.roleStatus;
        const roleName = button.dataset.roleName || L('الدور', 'role');
        const nextActive = button.dataset.nextActive === 'true';

        const approved = await confirmAction({
            title: nextActive ? L('تفعيل الدور', 'Enable role') : L('إيقاف الدور', 'Disable role'),
            message: nextActive
                ? `ستعود صلاحيات دور «${roleName}» للعمل للموظفين المرتبطين به.`
                : `ستصبح صلاحيات دور «${roleName}» غير فعالة حتى تعيد تفعيله.`,
            acceptText: nextActive ? L('تفعيل الدور', 'Enable role') : L('إيقاف الدور', 'Disable role'),
            icon: nextActive ? 'bi-play-circle' : 'bi-pause-circle',
            danger: !nextActive
        });
        if (!approved) return;

        const data = new FormData();
        data.append('AdminRoleId', roleId);
        data.append('IsActive', nextActive ? 'true' : 'false');

        try {
            const result = await postForm(urls.roleStatus, data);
            showToast(result.message);
            await loadTab('roles', state.tabQueries.roles, { updateHistory: false });
        } catch (error) {
            showToast(error.message, true);
        }
    }

    function showSecret(result) {
        if (!secretLayer) return;

        const value = typeof result === 'string'
            ? result
            : result?.temporaryPassword || '';

        const username = typeof result === 'object'
            ? result?.username || ''
            : '';

        const email = typeof result === 'object'
            ? result?.email || ''
            : '';

        const emailSent = typeof result === 'object'
            ? result?.emailSent === true
            : null;

        const code = secretLayer.querySelector('[data-secret-value]');
        if (code) code.textContent = value;

        const usernameNode = secretLayer.querySelector('[data-secret-username]');
        if (usernameNode) usernameNode.textContent = username || '—';

        const emailNode = secretLayer.querySelector('[data-secret-email]');
        if (emailNode) emailNode.textContent = email || '—';

        const status = secretLayer.querySelector('[data-secret-email-status]');
        if (status) {
            status.classList.remove('is-success', 'is-warning');
            status.classList.add(emailSent ? 'is-success' : 'is-warning');
            status.innerHTML = emailSent
                ? `<i class="bi bi-envelope-check-fill"></i><span><strong>${L('تم إرسال بيانات الدخول إلى البريد', 'Credentials were emailed')}</strong><small>${L('احتفظ بهذه النافذة كنسخة احتياطية فقط.', 'Keep this dialog only as a backup.')}</small></span>`
                : `<i class="bi bi-exclamation-triangle-fill"></i><span><strong>${L('تعذر إرسال البريد', 'Email could not be sent')}</strong><small>${L('الحساب تم إنشاؤه؛ انسخ كلمة المرور وأرسلها للموظف بطريقة آمنة.', 'The account was created; copy the password and share it securely with the employee.')}</small></span>`;
        }

        secretLayer.hidden = false;
        document.body.style.overflow = 'hidden';
        secretLayer.querySelector('[data-copy-secret]')?.focus();
    }

    function closeSecret() {
        if (!secretLayer) return;
        secretLayer.hidden = true;
        if (drawerLayer?.hidden !== false && confirmLayer?.hidden !== false) document.body.style.overflow = '';
    }

    async function copySecret() {
        const value = secretLayer?.querySelector('[data-secret-value]')?.textContent || '';
        if (!value) return;
        try {
            await navigator.clipboard.writeText(value);
            showToast(L('تم نسخ كلمة المرور المؤقتة.', 'Temporary password copied.'));
        } catch {
            const range = document.createRange();
            const element = secretLayer.querySelector('[data-secret-value]');
            if (element) {
                range.selectNodeContents(element);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                showToast(L('تم تحديد كلمة المرور؛ انسخها يدويًا.', 'The password is selected; copy it manually.'));
            }
        }
    }

    function showToast(message, isError = false) {
        if (!toastStack) return;
        const toast = document.createElement('div');
        toast.className = `employees-toast${isError ? ' is-error' : ''}`;
        toast.innerHTML = `
            <i class="bi ${isError ? 'bi-exclamation-circle-fill' : 'bi-check-circle-fill'}"></i>
            <span><strong>${isError ? L('تعذر التنفيذ', 'Action failed') : L('تم بنجاح', 'Completed successfully')}</strong><small>${escapeHtml(translateServerMessage(message) || '')}</small></span>`;
        toastStack.appendChild(toast);
        window.setTimeout(() => toast.remove(), 4200);
    }

    function refreshTopCounts(tab) {
        const badge = app.querySelector(`[data-tab-count="${CSS.escape(tab)}"]`);
        if (!badge) return;
        if (tab === 'employees') {
            const visible = content?.querySelectorAll('[data-employee-id]').length;
            const totalText = content?.querySelector('.employees-mini-stat:first-child strong')?.textContent;
            if (totalText && /^\d+$/.test(totalText.trim())) badge.textContent = totalText.trim();
            else if (typeof visible === 'number') badge.textContent = String(visible);
        }
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    app.addEventListener('click', async event => {
        const target = event.target.closest('button, [data-role-choice]');
        if (!target) return;

        if (target.matches('[data-employee-tab]')) {
            await loadTab(target.dataset.employeeTab);
            return;
        }

        if (target.matches('[data-retry-tab]')) {
            await loadTab(state.activeTab, state.tabQueries[state.activeTab], { updateHistory: false });
            return;
        }

        if (target.matches('[data-open-employee-form]')) {
            await openDrawer(urls.employeeForm);
            return;
        }

        if (target.matches('[data-edit-employee]')) {
            await openDrawer(urls.employeeForm, new URLSearchParams({ id: target.dataset.editEmployee }));
            return;
        }

        if (target.matches('[data-open-role-form]')) {
            await openDrawer(urls.roleForm);
            return;
        }

        if (target.matches('[data-edit-role]')) {
            await openDrawer(urls.roleForm, new URLSearchParams({ id: target.dataset.editRole }));
            return;
        }

        if (target.matches('[data-close-drawer]')) {
            closeDrawer();
            return;
        }

        if (target.matches('[data-form-next]')) {
            const form = target.closest('[data-employee-form]');
            const current = Number(form?.dataset.currentStep || 1);
            if (form && validateEmployeeStep(form, current)) renderEmployeeStep(form, current + 1);
            return;
        }

        if (target.matches('[data-form-prev]')) {
            const form = target.closest('[data-employee-form]');
            if (form) renderEmployeeStep(form, Number(form.dataset.currentStep || 1) - 1);
            return;
        }

        if (target.matches('[data-form-step-button]')) {
            const form = target.closest('[data-employee-form]');
            if (!form) return;
            const requested = Number(target.dataset.formStepButton);
            const current = Number(form.dataset.currentStep || 1);
            if (requested <= current || validateEmployeeStep(form, current)) renderEmployeeStep(form, requested);
            return;
        }

        if (target.matches('[data-toggle-password]')) {
            const form = target.closest('form');
            const fieldName = target.dataset.togglePassword;
            const input = fieldName
                ? form?.querySelector(`[name="${CSS.escape(fieldName)}"]`)
                : target.closest('.employee-control')?.querySelector('input');

            if (!input) return;

            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            target.querySelector('i').className = `bi ${show ? 'bi-eye-slash' : 'bi-eye'}`;
            return;
        }

        if (target.matches('[data-toggle-permission-group]')) {
            togglePermissionGroup(target);
            return;
        }

        if (target.matches('[data-change-employee-status]')) {
            await changeEmployeeStatus(target);
            return;
        }

        if (target.matches('[data-role-status]')) {
            await changeRoleStatus(target);
            return;
        }

        if (target.matches('[data-employees-page]')) {
            if (target.disabled) return;
            const form = content.querySelector('[data-employees-filter]');
            const params = form ? serializeForm(form) : new URLSearchParams(state.tabQueries.employees);
            params.set('PageNumber', target.dataset.employeesPage);
            await loadTab('employees', params, { updateHistory: false });
            return;
        }

        if (target.matches('[data-audit-page]')) {
            if (target.disabled) return;
            const form = content.querySelector('[data-audit-filter]');
            const params = form ? serializeForm(form) : new URLSearchParams(state.tabQueries.audit);
            params.set('PageNumber', target.dataset.auditPage);
            await loadTab('audit', params, { updateHistory: false });
            return;
        }

        if (target.matches('[data-clear-search]')) {
            const form = target.closest('form');
            const search = form?.querySelector('input[type="search"]');
            if (search) search.value = '';
            form?.requestSubmit();
            return;
        }

        if (target.matches('[data-confirm-cancel]')) {
            finishConfirm(false);
            return;
        }

        if (target.matches('[data-confirm-accept]')) {
            finishConfirm(true);
            return;
        }

        if (target.matches('[data-close-secret]')) {
            closeSecret();
            return;
        }

        if (target.matches('[data-copy-secret]')) {
            await copySecret();
        }
    });

    app.addEventListener('submit', async event => {
        const form = event.target;

        if (form.matches('[data-employees-filter]')) {
            event.preventDefault();
            const params = serializeForm(form);
            params.set('PageNumber', '1');
            await loadTab('employees', params, { updateHistory: false });
            return;
        }

        if (form.matches('[data-roles-search]')) {
            event.preventDefault();
            const params = serializeForm(form);
            await loadTab('roles', params, { updateHistory: false });
            return;
        }

        if (form.matches('[data-audit-filter]')) {
            event.preventDefault();
            const params = serializeForm(form);
            params.set('PageNumber', '1');
            await loadTab('audit', params, { updateHistory: false });
            return;
        }

        if (form.matches('[data-employee-form]')) {
            event.preventDefault();
            await submitEmployeeForm(form);
            return;
        }

        if (form.matches('[data-role-form]')) {
            event.preventDefault();
            await submitRoleForm(form);
        }
    });

    document.addEventListener('keydown', event => {
        const tabButton = event.target?.closest?.('[data-employee-tab]');

        if (tabButton && ['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
            const tabs = [...app.querySelectorAll('[data-employee-tab]')]
                .filter(button => !button.disabled && button.offsetParent !== null);
            if (!tabs.length) return;

            const currentIndex = Math.max(0, tabs.indexOf(tabButton));
            let nextIndex = currentIndex;

            if (event.key === 'Home') nextIndex = 0;
            else if (event.key === 'End') nextIndex = tabs.length - 1;
            else {
                const rtl = document.documentElement.dir?.toLowerCase() === 'rtl';
                const direction = rtl ? -1 : 1;
                const delta = event.key === 'ArrowRight' ? direction : -direction;
                nextIndex = (currentIndex + delta + tabs.length) % tabs.length;
            }

            event.preventDefault();
            const nextTab = tabs[nextIndex];
            nextTab.focus({ preventScroll: true });
            loadTab(nextTab.dataset.employeeTab);
            return;
        }

        if (event.key !== 'Escape') return;
        if (confirmLayer?.hidden === false) finishConfirm(false);
        else if (secretLayer?.hidden === false) closeSecret();
        else if (drawerLayer?.hidden === false) closeDrawer();
    });

    const pageTab = new URL(window.location.href).searchParams.get('tab');
    state.activeTab = normalizeTab(pageTab || state.activeTab);
    loadTab(state.activeTab, null, { updateHistory: false });
})();
