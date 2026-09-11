/**
 * =======================================================================================
 * 👑 دليلك اليمني - المحرك الذكي الموحد لتبويبات الإعدادات (نظام الصفحة الواحدة VIP)
 * Asset: wwwroot/js/admin/settings-actions.js
 * =======================================================================================
 */

function containsArabicCharacters(value) {
    return /[؀-ۿݐ-ݿࢠ-ࣿ]/.test(
        String(value ?? '')
    );
}

function validateEnglishControl(
    id,
    label,
    required = true
) {
    const control =
        document.getElementById(id);

    if (!control) {
        return {
            valid: false,
            message: `تعذر العثور على حقل ${label}.`,
            control: null
        };
    }

    const value =
        String(control.value || '').trim();

    if (required && !value) {
        return {
            valid: false,
            message: `${label} مطلوب.`,
            control
        };
    }

    if (value && containsArabicCharacters(value)) {
        return {
            valid: false,
            message: `${label} يجب أن يحتوي على النص الإنجليزي فقط.`,
            control
        };
    }

    return {
        valid: true,
        message: '',
        control
    };
}

function validateBilingualSettingsSection(section) {
    const checks = [];

    if (section === 'branding') {
        checks.push(
            validateEnglishControl(
                'siteNameEn',
                'اسم المنصة بالإنجليزية',
                true
            ),
            validateEnglishControl(
                'siteDescriptionEn',
                'وصف المنصة بالإنجليزية',
                !!String(
                    document.getElementById('siteDescription')?.value || ''
                ).trim()
            ),
            validateEnglishControl(
                'heroTitleMainEn',
                'العنوان الرئيسي بالإنجليزية',
                true
            ),
            validateEnglishControl(
                'heroTitleSubEn',
                'الكلمة المساعدة بالإنجليزية',
                true
            ),
            validateEnglishControl(
                'bannerTextEn',
                'نص البنر بالإنجليزية',
                document.getElementById('isBannerActive')?.checked === true ||
                !!String(
                    document.getElementById('bannerText')?.value || ''
                ).trim()
            )
        );
    }

    if (section === 'maintenance') {
        const maintenanceEnabled =
            document.getElementById('maintenanceMode')?.checked === true;

        checks.push(
            validateEnglishControl(
                'maintenanceTitleEn',
                'عنوان الصيانة بالإنجليزية',
                maintenanceEnabled
            ),
            validateEnglishControl(
                'maintenanceMessageEn',
                'رسالة الصيانة بالإنجليزية',
                maintenanceEnabled
            )
        );
    }

    return checks.find(check => !check.valid) || {
        valid: true,
        message: '',
        control: null
    };
}



/* ==========================================================================
 * حماية طلبات إعدادات الإدارة
 * ========================================================================== */

function getSettingsAntiForgeryInput() {
    return document.querySelector(
        'input[name="__RequestVerificationToken"], input[type="hidden"][name*="RequestVerificationToken"]'
    );
}

function appendSettingsAntiForgeryToken(formData) {
    const input = getSettingsAntiForgeryInput();

    if (!input || !input.name || !input.value) {
        throw new Error(
            'تعذر العثور على رمز حماية الصفحة. نفّذ Ctrl + F5 ثم حاول مرة أخرى.'
        );
    }

    if (!formData.has(input.name)) {
        formData.append(input.name, input.value);
    }

    return formData;
}

function settingsFetch(url, options = {}) {
    const requestOptions = { ...options };
    const method = String(requestOptions.method || 'GET').toUpperCase();

    requestOptions.credentials = 'same-origin';

    if (method === 'POST') {
        const body =
            requestOptions.body instanceof FormData
                ? requestOptions.body
                : new FormData();

        appendSettingsAntiForgeryToken(body);
        requestOptions.body = body;

        const token = getSettingsAntiForgeryInput()?.value || '';

        requestOptions.headers = {
            ...(requestOptions.headers || {}),
            'X-Requested-With': 'XMLHttpRequest',
            'Accept': 'application/json',
            ...(token ? { 'RequestVerificationToken': token } : {})
        };
    }

    return window.fetch(url, requestOptions);
}

async function settingsPostJson(url, formData) {
    const response = await settingsFetch(
        url,
        {
            method: 'POST',
            body: formData
        }
    );

    const responseText = await response.text();

    let result = null;

    if (responseText) {
        try {
            result = JSON.parse(responseText);
        }
        catch {
            result = null;
        }
    }

    if (!response.ok) {
        let message =
            result?.message ||
            result?.Message ||
            '';

        if (!message) {
            if (response.status === 400) {
                message =
                    'رفض الخادم طلب الحفظ. حدّث الصفحة بـ Ctrl + F5 ثم حاول مرة أخرى.';
            }
            else if (response.status === 401 || response.status === 403) {
                message =
                    'انتهت الجلسة أو ليست لديك صلاحية لتنفيذ هذه العملية.';
            }
            else {
                message =
                    `فشل طلب الحفظ من الخادم (HTTP ${response.status}).`;
            }
        }

        throw new Error(message);
    }

    if (!result) {
        throw new Error(
            'استجاب الخادم بصيغة غير متوقعة.'
        );
    }

    return result;
}

function getCurrentSettingsSection() {
    const activeTarget =
        document.querySelector('.tab-btn.active')?.getAttribute('data-target') || '';

    const map = {
        'branding-tab': 'branding',
        'policies-tab': 'policies',
        'contact-tab': 'contact',
        'maintenance-tab': 'maintenance'
    };

    return map[activeTarget] || '';
}


/* ==========================================================================
 * 🟢 القسم الأول: المحرك الأساسي (إدارة التبويبات والملاحة)
 * ========================================================================== */
document.addEventListener("DOMContentLoaded", function () {

    const tabButtons = document.querySelectorAll('.tab-btn');
    const tabContents = document.querySelectorAll('.tab-pane-content');

    // قراءة التبويبة المحفوظة في ذاكرة المتصفح إن وجدت
    const savedTab = localStorage.getItem('activeSettingsTab');

    if (savedTab) {
        // إزالة التفعيل الافتراضي من كل الأزرار والمحتويات
        tabButtons.forEach(btn => btn.classList.remove('active'));
        tabContents.forEach(content => content.style.display = 'none');

        // تفعيل التبويبة المحفوظة
        const targetBtn = document.querySelector(`[data-target="${savedTab}"]`);
        if (targetBtn) targetBtn.classList.add('active');

        const targetContent = document.querySelector('.' + savedTab);
        if (targetContent) targetContent.style.display = 'block';

        // تحميل البيانات الديناميكية بناءً على التبويبة المفتوحة
        if (savedTab === 'wallets-tab') {
            loadSupportedWalletsDynamicList();
        } else if (savedTab === 'branding-tab') {
            loadHeroSlidesList();
        }
    } else {
        // التحميل الافتراضي للتبويبة الأولى
        loadHeroSlidesList();
    }

    // حركة التبويبات عند النقر
    tabButtons.forEach(button => {
        button.addEventListener('click', function () {
            tabButtons.forEach(btn => btn.classList.remove('active'));
            tabContents.forEach(content => content.style.display = 'none');

            this.classList.add('active');
            const targetClass = this.getAttribute('data-target');
            document.querySelector('.' + targetClass).style.display = 'block';

            localStorage.setItem('activeSettingsTab', targetClass);

            if (targetClass === 'wallets-tab') {
                loadSupportedWalletsDynamicList();
            } else if (targetClass === 'branding-tab') {
                loadHeroSlidesList();
            }
        });
    });

});


/* ==========================================================================
 * 💾 القسم الثاني: الإرسال والحفظ (حفظ الإعدادات العامة والمواسم والعملات)
 * ========================================================================== */
async function saveAllSettings() {
    const section = getCurrentSettingsSection();

    if (!section) {
        Swal.fire({
            icon: 'error',
            title: 'تعذر تحديد القسم',
            text: 'أعد تحميل الصفحة ثم حاول مرة أخرى.',
            confirmButtonColor: '#00A6DA'
        });
        return;
    }

    const bilingualValidation =
        validateBilingualSettingsSection(section);

    if (!bilingualValidation.valid) {
        Swal.fire({
            icon: 'warning',
            title: 'راجع الحقول الإنجليزية',
            text: bilingualValidation.message,
            confirmButtonColor: '#00A6DA'
        });

        bilingualValidation.control?.focus();
        return;
    }

    Swal.fire({
        title: 'جاري الحفظ...',
        allowOutsideClick: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });

    const formData = new FormData();
    formData.append('Section', section);

    if (section === 'branding') {
        formData.append('SiteName', document.getElementById('siteName')?.value || '');
        formData.append('SiteNameEn', document.getElementById('siteNameEn')?.value || '');

        formData.append('SiteDescription', document.getElementById('siteDescription')?.value || '');
        formData.append('SiteDescriptionEn', document.getElementById('siteDescriptionEn')?.value || '');

        const logoInput = document.getElementById('logoFile');
        if (logoInput?.files?.length > 0) {
            formData.append('LogoUpload', logoInput.files[0]);
        }

        const faviconInput = document.getElementById('faviconFile');
        if (faviconInput?.files?.length > 0) {
            formData.append('FaviconUpload', faviconInput.files[0]);
        }

        formData.append('HeroTitleMain', document.getElementById('heroTitleMain')?.value || '');
        formData.append('HeroTitleMainEn', document.getElementById('heroTitleMainEn')?.value || '');

        formData.append('HeroTitleSub', document.getElementById('heroTitleSub')?.value || '');
        formData.append('HeroTitleSubEn', document.getElementById('heroTitleSubEn')?.value || '');

        formData.append('SeasonTheme', document.getElementById('seasonTheme')?.value || 'Normal');
        formData.append('IsBannerActive', document.getElementById('isBannerActive')?.checked === true);

        formData.append('BannerText', document.getElementById('bannerText')?.value || '');
        formData.append('BannerTextEn', document.getElementById('bannerTextEn')?.value || '');
    }

    if (section === 'policies') {
        formData.append('AutoApprovePlaces', document.getElementById('autoApprovePlaces')?.checked === true);
        formData.append('AllowNewRegistrations', document.getElementById('allowRegistration')?.checked === true);
        formData.append('MaxImageSize', document.getElementById('maxImageSize')?.value || 5);
        formData.append('MaxImageCount', document.getElementById('maxImageCount')?.value || 10);

        formData.append('CommissionPercentage', document.getElementById('commissionPercentage')?.value || 0);
        formData.append('DefaultCurrency', document.getElementById('defaultCurrency')?.value || 'ريال يمني');
        formData.append('AutoUpdateExchangeRates', document.getElementById('autoUpdateExchangeRates')?.checked === true);

        formData.append('sanaaUsdRate', document.getElementById('sanaaUsdRate')?.value || 0);
        formData.append('adenUsdRate', document.getElementById('adenUsdRate')?.value || 0);
        formData.append('sanaaSarRate', document.getElementById('sanaaSarRate')?.value || 0);
        formData.append('adenSarRate', document.getElementById('adenSarRate')?.value || 0);
    }

    if (section === 'contact') {
        formData.append('ContactEmail', document.getElementById('officialEmail')?.value || '');
        formData.append('SupportPhone', document.getElementById('supportPhone')?.value || '');
        formData.append('FacebookLink', document.getElementById('fbLink')?.value || '');
        formData.append('TwitterLink', document.getElementById('xLink')?.value || '');
        formData.append('InstagramLink', document.getElementById('instaLink')?.value || '');
        formData.append('WhatsAppNumber', document.getElementById('waLink')?.value || '');
    }

    if (section === 'maintenance') {
        formData.append('MaintenanceMode', document.getElementById('maintenanceMode')?.checked === true);
        formData.append('MaintenanceTitle', document.getElementById('maintenanceTitle')?.value || '');
        formData.append('MaintenanceTitleEn', document.getElementById('maintenanceTitleEn')?.value || '');
        formData.append('MaintenanceMessage', document.getElementById('maintenanceMessage')?.value || '');
        formData.append('MaintenanceMessageEn', document.getElementById('maintenanceMessageEn')?.value || '');

        const selectedMaintenanceType =
            document.querySelector('input[name="maintenanceType"]:checked')?.value;

        formData.append('MaintenanceType', selectedMaintenanceType || 'Full');
        formData.append('MaintenanceAffectOwners', document.getElementById('maintenanceAffectOwners')?.checked === true);
        formData.append('MaintenanceShowContact', document.getElementById('maintenanceShowContact')?.checked === true);
        formData.append('MaintenanceStartAt', document.getElementById('maintenanceStartAt')?.value || '');
        formData.append('MaintenanceEndAt', document.getElementById('maintenanceEndAt')?.value || '');

        [
            'MaintenanceBlockBookings',
            'MaintenanceBlockPayments',
            'MaintenanceBlockOwnerChanges',
            'MaintenanceBlockMessages',
            'MaintenanceBlockReviews',
            'MaintenanceBlockRegistrations'
        ].forEach(name => {
            const id = name.charAt(0).toLowerCase() + name.slice(1);

            formData.append(
                name,
                document.getElementById(id)?.checked === true
            );
        });
    }

    try {
        const result = await settingsPostJson(
            '/Admin/Settings/SaveSettings',
            formData
        );

        if (!result.success) {
            throw new Error(
                result.message ||
                'تعذر حفظ الإعدادات.'
            );
        }

        await Swal.fire({
            icon: 'success',
            title: 'تم الحفظ',
            text: result.message || 'تم حفظ التعديلات بنجاح.',
            confirmButtonColor: '#00A6DA'
        });

        window.location.reload();
    }
    catch (error) {
        console.error('Settings save error:', error);

        Swal.fire({
            icon: 'error',
            title: 'تعذر الحفظ',
            text:
                error?.message ||
                'تعذر تنفيذ عملية الحفظ.',
            confirmButtonColor: '#00A6DA'
        });
    }
}


/* ==========================================================================
 * 💳 القسم الثالث: تبويبة المحافظ وشبكات الدفع
 * ========================================================================== */

// جلب قائمة الشبكات وعرضها
function loadSupportedWalletsDynamicList() {
    const tableBody = document.getElementById('supportedWalletsTableBody');
    if (!tableBody) return;

    settingsFetch('/Admin/Settings/GetSupportedWallets')
        .then(response => response.json())
        .then(data => {
            tableBody.innerHTML = '';
            if (data.length === 0) {
                tableBody.innerHTML = `<tr><td colspan="5" class="text-center py-4 text-muted">لا توجد شبكات دفع مضافة حالياً.</td></tr>`;
                return;
            }

            data.forEach((wallet, index) => {
                const logo = wallet.logoPath ? wallet.logoPath : '/images/wallets/default-wallet.png';
                const checked = wallet.isActive ? 'checked' : '';

                const rowMarkup = `
                    <tr>
                        <td class="text-center text-muted fw-bold">${index + 1}</td>
                        <td>
                            <img src="${logo}" class="wallet-logo-img" onerror="this.src='/images/wallets/default-wallet.png'" />
                        </td>
                        <td class="fw-bold text-dark" style="font-size: 1.05rem;">${wallet.walletName}</td>
                        <td class="text-center">
                            <div class="form-check form-switch d-inline-block m-0">
                                <input class="form-check-input" type="checkbox" role="switch" ${checked} onchange="toggleWalletStateDynamically(${wallet.walletId}, this.checked)" style="cursor:pointer;">
                            </div>
                        </td>
                        <td class="text-center">
                            <button type="button" class="btn-delete-wallet" onclick="deletePlatformWalletDynamically(${wallet.walletId}, '${wallet.walletName}')" title="حذف الشبكة">
                                <i class="bi bi-trash"></i>
                            </button>
                        </td>
                    </tr>
                `;
                tableBody.insertAdjacentHTML('beforeend', rowMarkup);
            });
        })
        .catch(error => {
            console.error('Fetch Wallets Error:', error);
            tableBody.innerHTML = `<tr><td colspan="5" class="text-center py-4 text-danger">حدث خطأ أثناء جلب البيانات.</td></tr>`;
        });
}

// إضافة شبكة دفع جديدة
function createPlatformWallet(event) {
    event.preventDefault();
    const nameInput = document.getElementById('newWalletName');
    const nameEnInput = document.getElementById('newWalletNameEn');
    const logoInput = document.getElementById('newWalletLogo');

    const arabicName = String(nameInput?.value || '').trim();
    const englishName = String(nameEnInput?.value || '').trim();

    if (!arabicName) {
        showSettingsRequestError(
            'اسم الشبكة مطلوب',
            new Error('اكتب اسم البنك أو المحفظة بالعربية.')
        );
        nameInput?.focus();
        return;
    }

    if (!englishName) {
        showSettingsRequestError(
            'الاسم الإنجليزي مطلوب',
            new Error('اكتب اسم البنك أو المحفظة بالإنجليزية.')
        );
        nameEnInput?.focus();
        return;
    }

    if (containsArabicCharacters(englishName)) {
        showSettingsRequestError(
            'راجع الاسم الإنجليزي',
            new Error('اسم البنك أو المحفظة بالإنجليزية لا يجب أن يحتوي على أحرف عربية.')
        );
        nameEnInput?.focus();
        return;
    }

    Swal.fire({ title: 'جاري الإضافة...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

    const walletForm = new FormData();
    walletForm.append("WalletName", arabicName);
    walletForm.append("WalletNameEn", englishName);
    if (logoInput.files.length > 0) walletForm.append("LogoUpload", logoInput.files[0]);

    settingsFetch('/Admin/Settings/AddSupportedWallet', { method: 'POST', body: walletForm })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                Swal.fire({ icon: 'success', title: 'تمت الإضافة!', text: result.message, confirmButtonColor: '#00A6DA', timer: 2000 });
                document.getElementById('addSupportedWalletForm').reset();
                loadSupportedWalletsDynamicList();
            } else {
                Swal.fire('خطأ', result.message, 'error');
            }
        })
        .catch(error => {
            console.error('Add Wallet Error:', error);
            Swal.fire('خطأ', 'فشل إرسال البيانات للسيرفر.', 'error');
        });
}

async function editSupportedWalletNames(
    walletId,
    currentArabicName,
    currentEnglishName
) {
    if (!ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks) {
        showPermissionDenied();
        return;
    }

    const result = await Swal.fire({
        title: 'تعديل اسم شبكة الدفع',
        html: `
            <div class="text-start">
                <label class="form-label fw-bold d-block text-end">الاسم بالعربية</label>
                <input id="walletEditNameAr"
                       class="swal2-input m-0 mb-3"
                       dir="rtl"
                       value="${escapeSettingsHtml(currentArabicName)}"
                       maxlength="150">

                <label class="form-label fw-bold d-block text-start">Name — English</label>
                <input id="walletEditNameEn"
                       class="swal2-input m-0"
                       dir="ltr"
                       value="${escapeSettingsHtml(currentEnglishName)}"
                       maxlength="150">
            </div>
        `,
        showCancelButton: true,
        confirmButtonText: 'حفظ',
        cancelButtonText: 'إلغاء',
        confirmButtonColor: '#00A6DA',
        focusConfirm: false,
        preConfirm: () => {
            const arabicName =
                String(document.getElementById('walletEditNameAr')?.value || '').trim();

            const englishName =
                String(document.getElementById('walletEditNameEn')?.value || '').trim();

            if (!arabicName) {
                Swal.showValidationMessage('اكتب الاسم بالعربية.');
                return false;
            }

            if (!englishName) {
                Swal.showValidationMessage('اكتب الاسم بالإنجليزية.');
                return false;
            }

            if (containsArabicCharacters(englishName)) {
                Swal.showValidationMessage('الاسم الإنجليزي لا يجب أن يحتوي على أحرف عربية.');
                return false;
            }

            return {
                arabicName,
                englishName
            };
        }
    });

    if (!result.isConfirmed || !result.value) {
        return;
    }

    const formData = new FormData();
    formData.append('id', String(walletId));
    formData.append('WalletName', result.value.arabicName);
    formData.append('WalletNameEn', result.value.englishName);

    try {
        const response =
            await settingsPostJson(
                '/Admin/Settings/EditSupportedWalletNames',
                formData
            );

        if (!response.success) {
            throw new Error(
                response.message ||
                'تعذر تحديث أسماء شبكة الدفع.'
            );
        }

        Swal.fire({
            icon: 'success',
            title: 'تم التحديث',
            text: response.message,
            timer: 1600,
            showConfirmButton: false
        });

        settingsWalletsLoaded = false;
        loadSupportedWalletsDynamicList(true);
    }
    catch (error) {
        showSettingsRequestError(
            'تعذر تحديث شبكة الدفع',
            error
        );
    }
}


// تفعيل/تعطيل الشبكة
function toggleWalletStateDynamically(walletId, isChecked) {
    const formData = new FormData();
    formData.append("id", walletId);
    formData.append("isActive", isChecked);

    settingsFetch('/Admin/Settings/ToggleWalletStatus', { method: 'POST', body: formData })
        .then(response => response.json())
        .then(result => {
            if (!result.success) {
                Swal.fire('خطأ', result.message, 'error');
                loadSupportedWalletsDynamicList();
            }
        })
        .catch(error => {
            console.error('Toggle Status Error:', error);
            Swal.fire('خطأ', 'حدث خطأ في الاتصال.', 'error');
            loadSupportedWalletsDynamicList();
        });
}

// حذف الشبكة نهائياً
function deletePlatformWalletDynamically(walletId, walletName) {
    Swal.fire({
        title: `حذف [ ${walletName} ]؟`,
        text: "هل أنت متأكد من حذف هذه الشبكة؟ سيتم إزالتها من النظام بالكامل.",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#dc3545',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'نعم، احذف',
        cancelButtonText: 'إلغاء'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({ title: 'جاري الحذف...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

            const deleteForm = new FormData();
            deleteForm.append("id", walletId);

            settingsFetch('/Admin/Settings/DeleteSupportedWallet', { method: 'POST', body: deleteForm })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'تم الحذف!', text: data.message, confirmButtonColor: '#00A6DA', timer: 1500 });
                        loadSupportedWalletsDynamicList();
                    } else {
                        Swal.fire('خطأ', data.message, 'error');
                    }
                })
                .catch(error => {
                    console.error('Delete Wallet Error:', error);
                    Swal.fire('خطأ', 'فشل إتمام عملية الحذف.', 'error');
                });
        }
    });
}


/* ==========================================================================
 * 🎬 القسم الرابع: تبويبة الهوية والسلايدر (Hero Slider Studio VIP)
 * ========================================================================== */

// جلب قائمة السلايدرات وعرضها

const heroSlidesAdminCache = new Map();

function escapeHeroAdminHtml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

function getHeroSlideFromAdminCache(slideId) {
    return heroSlidesAdminCache.get(Number(slideId)) || null;
}

function loadHeroSlidesList() {
    const tableBody = document.getElementById('heroSlidesTableBody');
    if (!tableBody) return;

    settingsFetch('/Admin/Settings/GetHeroSlides')
        .then(response => response.json())
        .then(data => {
            tableBody.innerHTML = '';
            heroSlidesAdminCache.clear();

            if (!Array.isArray(data) || data.length === 0) {
                tableBody.innerHTML =
                    `<tr>
                        <td colspan="7" class="text-center py-5 text-muted">
                            لا توجد شرائح عرض مضافة حالياً.
                        </td>
                    </tr>`;
                return;
            }

            data.forEach((slide) => {
                heroSlidesAdminCache.set(
                    Number(slide.id),
                    {
                        ...slide,
                        typingText: String(slide.typingText || '').trim(),
                        typingTextEn: String(slide.typingTextEn || '').trim()
                    }
                );

                const img =
                    slide.imagePath
                        ? slide.imagePath
                        : '/images/bg1.jpg';

                const checked =
                    slide.isActive
                        ? 'checked'
                        : '';

                const isVideo =
                    img.toLowerCase().endsWith('.mp4');

                const mediaTypeHtml =
                    isVideo
                        ? `<span class="media-type-badge video">
                               <i class="bi bi-camera-video-fill"></i>
                               فيديو 4K
                           </span>`
                        : `<span class="media-type-badge image">
                               <i class="bi bi-image-fill"></i>
                               صورة HD
                           </span>`;

                const safeImagePath =
                    escapeHeroAdminHtml(img);

                const previewHtml =
                    isVideo
                        ? `<video src="${safeImagePath}"
                                  class="hero-slide-video"
                                  autoplay
                                  loop
                                  muted></video>`
                        : `<img src="${safeImagePath}"
                                class="hero-slide-img"
                                onerror="this.src='/images/bg1.jpg'" />`;

                const mainColor =
                    slide.mainTextColor || '#ffffff';

                const accentColor =
                    slide.accentColor || '#ff5a5f';

                const arabicText =
                    String(slide.typingText || '').trim();

                const englishText =
                    String(slide.typingTextEn || '').trim();

                const arabicTextHtml =
                    escapeHeroAdminHtml(
                        arabicText || '—'
                    );

                const englishTextHtml =
                    englishText
                        ? escapeHeroAdminHtml(englishText)
                        : '<span class="text-danger">لم تتم إضافة الترجمة الإنجليزية</span>';

                const rowMarkup = `
                    <tr>
                        <td>
                            <div class="order-arrows-container">
                                <button type="button"
                                        class="btn-sort-arrow mb-1"
                                        onclick="moveSlideOrder(${slide.id}, 'up')"
                                        title="تحريك للأعلى">
                                    <i class="bi bi-caret-up-fill"></i>
                                </button>

                                <button type="button"
                                        class="btn-sort-arrow mt-1"
                                        onclick="moveSlideOrder(${slide.id}, 'down')"
                                        title="تحريك للأسفل">
                                    <i class="bi bi-caret-down-fill"></i>
                                </button>
                            </div>
                        </td>

                        <td>
                            ${previewHtml}
                        </td>

                        <td>
                            ${mediaTypeHtml}
                        </td>

                        <td class="text-center">
                            <div class="d-flex flex-column gap-2 align-items-stretch px-1">
                                <div class="rounded-3 border bg-light px-3 py-2"
                                     dir="rtl"
                                     style="text-align:right;">
                                    <div class="small text-muted fw-bold mb-1">AR</div>
                                    <div class="fw-bold text-dark">${arabicTextHtml}</div>
                                </div>

                                <div class="rounded-3 border bg-white px-3 py-2"
                                     dir="ltr"
                                     style="text-align:left;">
                                    <div class="small text-muted fw-bold mb-1">EN</div>
                                    <div class="fw-semibold text-dark">${englishTextHtml}</div>
                                </div>
                            </div>
                        </td>

                        <td>
                            <div class="color-harmony-container">
                                <div class="color-dot"
                                     style="background-color: ${mainColor}; border-color: #e9ecef;"
                                     title="لون العنوان الأساسي: ${mainColor}">
                                </div>

                                <div class="color-dot"
                                     style="background-color: ${accentColor}; border-color: #e9ecef;"
                                     title="لون الكلمة التفاعلية: ${accentColor}">
                                </div>
                            </div>
                        </td>

                        <td class="text-center">
                            <div class="form-check form-switch d-inline-block m-0">
                                <input class="form-check-input"
                                       type="checkbox"
                                       role="switch"
                                       ${checked}
                                       onchange="toggleHeroSlideStatus(${slide.id}, this.checked)"
                                       style="cursor:pointer;">
                            </div>
                        </td>

                        <td class="text-center">
                            <div class="action-buttons-container">
                                <button type="button"
                                        class="btn-edit-slide"
                                        onclick="editHeroSlideText(${slide.id})"
                                        title="تعديل النص العربي والإنجليزي">
                                    <i class="bi bi-pencil-square"></i>
                                </button>

                                <button type="button"
                                        class="btn-delete-wallet"
                                        onclick="deleteHeroSlideDynamically(${slide.id})"
                                        title="حذف السلايد">
                                    <i class="bi bi-trash"></i>
                                </button>
                            </div>
                        </td>
                    </tr>
                `;

                tableBody.insertAdjacentHTML(
                    'beforeend',
                    rowMarkup
                );
            });
        })
        .catch(error => {
            console.error(
                'Fetch Slides Error:',
                error
            );

            tableBody.innerHTML =
                `<tr>
                    <td colspan="7"
                        class="text-center py-4 text-danger">
                        حدث خطأ أثناء جلب السلايدر.
                    </td>
                </tr>`;
        });
}

// إضافة سلايد جديد مع فحص الحجم المسبق (Max 20MB)
function addHeroSlideDynamically(event) {
    event.preventDefault();

    const textInput = document.getElementById('newSlideText');
    const textEnInput = document.getElementById('newSlideTextEn');
    const imageInput = document.getElementById('newSlideImage');
    const mainColorInput = document.getElementById('newSlideMainTextColor');
    const accentColorInput = document.getElementById('newSlideAccentColor');

    if (!textInput.value.trim()) {
        Swal.fire({
            icon: 'warning',
            title: 'النص العربي مطلوب',
            text: 'اكتب النص التفاعلي بالعربية.',
            confirmButtonColor: '#00A6DA'
        });
        textInput.focus();
        return;
    }

    const englishText =
        String(textEnInput?.value || '').trim();

    if (!englishText) {
        Swal.fire({
            icon: 'warning',
            title: 'النص الإنجليزي مطلوب',
            text: 'اكتب النص التفاعلي بالإنجليزية.',
            confirmButtonColor: '#00A6DA'
        });
        textEnInput?.focus();
        return;
    }

    if (containsArabicCharacters(englishText)) {
        Swal.fire({
            icon: 'warning',
            title: 'راجع النص الإنجليزي',
            text: 'النص الإنجليزي لا يجب أن يحتوي على أحرف عربية.',
            confirmButtonColor: '#00A6DA'
        });
        textEnInput?.focus();
        return;
    }

    // ✨ الحماية والفحص: التأكد من حجم الملف قبل الرفع (20 ميجابايت)
    if (imageInput.files.length > 0) {
        const file = imageInput.files[0];
        const maxSizeInBytes = 20 * 1024 * 1024; // 20 MB

        if (file.size > maxSizeInBytes) {
            Swal.fire({
                icon: 'warning',
                title: 'حجم الملف كبير جداً!',
                text: 'عذراً، يجب ألا يتجاوز حجم الصورة أو الفيديو 20 ميجابايت لضمان سرعة المنصة.',
                confirmButtonColor: '#00A6DA'
            });
            // إعادة ضبط حقل الملف لكي يستطيع المستخدم اختيار ملف آخر
            imageInput.value = '';
            document.getElementById('fileNameDisplay').innerText = 'اختر ملفاً...';
            return; // إيقاف الدالة هنا وعدم إرسال الطلب للسيرفر
        }
    }

    Swal.fire({ title: 'جاري الرفع والمعالجة...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

    const slideForm = new FormData();
    slideForm.append("TypingText", textInput.value.trim());
    slideForm.append("TypingTextEn", englishText);

    if (mainColorInput) slideForm.append("MainTextColor", mainColorInput.value);
    if (accentColorInput) slideForm.append("AccentColor", accentColorInput.value);
    if (imageInput.files.length > 0) slideForm.append("ImageUpload", imageInput.files[0]);

    settingsFetch('/Admin/Settings/AddHeroSlide', { method: 'POST', body: slideForm })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                Swal.fire({ icon: 'success', title: 'تمت الإضافة الفخمة!', text: result.message, confirmButtonColor: '#00A6DA', timer: 2000 });
                document.getElementById('addHeroSlideForm').reset();
                document.getElementById('fileNameDisplay').innerText = 'اختر ملفاً...';
                loadHeroSlidesList();
            } else {
                Swal.fire('خطأ', result.message, 'error');
            }
        })
        .catch(error => {
            console.error('Add Slide Error:', error);
            Swal.fire('خطأ', 'فشل إرسال البيانات للسيرفر.', 'error');
        });
}

// تفعيل/تعطيل السلايد
function toggleHeroSlideStatus(slideId, isChecked) {
    const formData = new FormData();
    formData.append("id", slideId);
    formData.append("isActive", isChecked);

    settingsFetch('/Admin/Settings/ToggleHeroSlideStatus', { method: 'POST', body: formData })
        .then(response => response.json())
        .then(result => {
            if (!result.success) {
                Swal.fire('خطأ', result.message, 'error');
                loadHeroSlidesList();
            }
        })
        .catch(error => {
            console.error('Toggle Slide Status Error:', error);
            Swal.fire('خطأ', 'حدث خطأ في الاتصال.', 'error');
            loadHeroSlidesList();
        });
}

// تحريك السلايد للأعلى أو الأسفل
function moveSlideOrder(slideId, direction) {
    const formData = new FormData();
    formData.append("id", slideId);
    formData.append("direction", direction);

    settingsFetch('/Admin/Settings/MoveHeroSlideOrder', { method: 'POST', body: formData })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                loadHeroSlidesList();
            } else {
                console.log(result.message);
            }
        })
        .catch(error => {
            console.error('Move Slide Error:', error);
            Swal.fire('خطأ', 'حدث خطأ أثناء تعديل الترتيب.', 'error');
        });
}

// النافذة الذكية لتعديل النص التفاعلي مباشرة
function editHeroSlideText(slideId) {
    const slide =
        getHeroSlideFromAdminCache(slideId);

    if (!slide) {
        Swal.fire({
            icon: 'error',
            title: 'تعذر فتح السلايد',
            text: 'لم يتم العثور على بيانات السلايد. أعد تحميل القائمة وحاول مرة أخرى.',
            confirmButtonColor: '#00A6DA'
        });
        return;
    }

    const safeArabicText =
        escapeHeroAdminHtml(
            slide.typingText || ''
        );

    const safeEnglishText =
        escapeHeroAdminHtml(
            slide.typingTextEn || ''
        );

    Swal.fire({
        title: 'تعديل النص التفاعلي',
        width: 650,
        html: `
            <div class="text-start mt-2">
                <label for="heroEditTextAr"
                       class="form-label fw-bold d-block text-end"
                       dir="rtl">
                    النص التفاعلي — عربي
                </label>

                <input id="heroEditTextAr"
                       type="text"
                       class="swal2-input m-0 mb-3 w-100"
                       value="${safeArabicText}"
                       dir="rtl"
                       maxlength="250"
                       style="box-sizing:border-box; text-align:right;">

                <label for="heroEditTextEn"
                       class="form-label fw-bold d-block text-start"
                       dir="ltr">
                    Interactive text — English
                </label>

                <input id="heroEditTextEn"
                       type="text"
                       class="swal2-input m-0 w-100"
                       value="${safeEnglishText}"
                       dir="ltr"
                       maxlength="250"
                       style="box-sizing:border-box; text-align:left;">
            </div>
        `,
        showCancelButton: true,
        confirmButtonText: 'حفظ العربي والإنجليزي',
        cancelButtonText: 'إلغاء',
        confirmButtonColor: '#00A6DA',
        focusConfirm: false,

        preConfirm: () => {
            const arabicInput =
                document.getElementById(
                    'heroEditTextAr'
                );

            const englishInput =
                document.getElementById(
                    'heroEditTextEn'
                );

            const arabicText =
                String(
                    arabicInput?.value || ''
                ).trim();

            const englishText =
                String(
                    englishInput?.value || ''
                ).trim();

            if (!arabicText) {
                Swal.showValidationMessage(
                    'النص العربي مطلوب.'
                );

                arabicInput?.focus();
                return false;
            }

            if (!englishText) {
                Swal.showValidationMessage(
                    'النص الإنجليزي مطلوب.'
                );

                englishInput?.focus();
                return false;
            }

            if (
                containsArabicCharacters(
                    englishText
                )
            ) {
                Swal.showValidationMessage(
                    'النص الإنجليزي لا يجب أن يحتوي على أحرف عربية.'
                );

                englishInput?.focus();
                return false;
            }

            return {
                arabicText,
                englishText
            };
        }
    }).then((result) => {
        if (!result.isConfirmed) {
            return;
        }

        const formData =
            new FormData();

        formData.append(
            'id',
            slideId
        );

        formData.append(
            'newText',
            result.value.arabicText
        );

        formData.append(
            'newTextEn',
            result.value.englishText
        );

        Swal.fire({
            title: 'جاري حفظ النصين...',
            allowOutsideClick: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        settingsFetch(
            '/Admin/Settings/EditHeroSlideText',
            {
                method: 'POST',
                body: formData
            }
        )
            .then(response => response.json())
            .then(res => {
                if (!res.success) {
                    throw new Error(
                        res.message ||
                        'تعذر تحديث السلايد.'
                    );
                }

                return Swal.fire({
                    icon: 'success',
                    title: 'تم التحديث بنجاح',
                    text:
                        res.message ||
                        'تم حفظ النص العربي والإنجليزي.',
                    confirmButtonColor: '#00A6DA',
                    timer: 1700
                });
            })
            .then(() => {
                loadHeroSlidesList();
            })
            .catch(error => {
                console.error(
                    'Edit Hero Slide Error:',
                    error
                );

                Swal.fire({
                    icon: 'error',
                    title: 'تعذر الحفظ',
                    text:
                        error?.message ||
                        'حدث خطأ أثناء حفظ النصين.',
                    confirmButtonColor: '#00A6DA'
                });
            });
    });
}

// حذف السلايد نهائياً
function deleteHeroSlideDynamically(slideId) {
    Swal.fire({
        title: `تأكيد الحذف؟`,
        text: "لن تتمكن من التراجع عن هذا الإجراء، وسيتم إزالة الصورة أو الفيديو من المنصة بشكل نهائي.",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#dc3545',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'نعم، احذف',
        cancelButtonText: 'إلغاء'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({ title: 'جاري الحذف...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

            const deleteForm = new FormData();
            deleteForm.append("id", slideId);

            settingsFetch('/Admin/Settings/DeleteHeroSlide', { method: 'POST', body: deleteForm })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'تم الحذف!', text: data.message, confirmButtonColor: '#00A6DA', timer: 1500 });
                        loadHeroSlidesList();
                    } else {
                        Swal.fire('خطأ', data.message, 'error');
                    }
                })
                .catch(error => {
                    console.error('Delete Slide Error:', error);
                    Swal.fire('خطأ', 'فشل إتمام عملية الحذف.', 'error');
                });
        }
    });
}
