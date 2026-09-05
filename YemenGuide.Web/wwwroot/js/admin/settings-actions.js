/**
 * =======================================================================================
 * 👑 دليلك اليمني - المحرك الذكي الموحد لتبويبات الإعدادات (نظام الصفحة الواحدة VIP)
 * Asset: wwwroot/js/admin/settings-actions.js
 * =======================================================================================
 */


console.info('[YemenGuide Settings] runtime-fix 2026-09-04-2359 loaded');

/* ==========================================================================
 * صلاحيات صفحة الإعدادات - تُرسل من Razor ولا تمنح أي صلاحية بحد ذاتها.
 * الحماية النهائية تبقى في Controller عبر RequireAdminPermission.
 * ========================================================================== */
const ADMIN_SETTINGS_ROOT =
    document.querySelector('[data-admin-settings-root]');

function readAdminSettingsPermission(datasetKey) {
    return ADMIN_SETTINGS_ROOT?.dataset?.[datasetKey] === 'true';
}

const ADMIN_SETTINGS_PERMISSIONS = Object.freeze({
    manageGeneral:
        readAdminSettingsPermission('canManageGeneral'),
    managePaymentNetworks:
        readAdminSettingsPermission('canManagePaymentNetworks'),
    manageHeroSlides:
        readAdminSettingsPermission('canManageHeroSlides'),
    manageFinanceSettings:
        readAdminSettingsPermission('canManageFinanceSettings')
});

function showPermissionDenied() {
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            icon: 'warning',
            title: 'غير مسموح',
            text: 'ليس لديك صلاحية لتنفيذ هذه العملية.',
            confirmButtonColor: '#00A6DA'
        });
    }
}

function applySettingsPermissionState() {
    const root = document.querySelector('[data-admin-settings-root]');
    if (!root) return;

    // أزرار الحفظ حسب صلاحية كل قسم، بدل إخفاء الجميع بسبب ManageGeneral.
    const sectionButtons = Array.from(
        root.querySelectorAll('.btn-save-tab[data-save-section]')
    );

    sectionButtons.forEach(button => {
        const section = button.dataset.saveSection || '';

        const allowed =
            section === 'policies'
                ? (
                    ADMIN_SETTINGS_PERMISSIONS.manageGeneral ||
                    ADMIN_SETTINGS_PERMISSIONS.manageFinanceSettings
                )
                : ADMIN_SETTINGS_PERMISSIONS.manageGeneral;

        button.hidden = !allowed;
        button.disabled = !allowed;
    });

    if (!ADMIN_SETTINGS_PERMISSIONS.manageGeneral) {
        const generalControlIds = [
            'siteName', 'siteDescription', 'logoFile', 'faviconFile',
            'heroTitleMain', 'heroTitleSub', 'seasonTheme', 'isBannerActive', 'bannerText',
            'autoApprovePlaces', 'allowRegistration', 'maxImageSize', 'maxImageCount',
            'officialEmail', 'supportPhone', 'fbLink', 'xLink', 'instaLink', 'waLink',
            'maintenanceMode', 'maintenanceTitle', 'maintenanceMessage',
            'maintenanceTypeFull', 'maintenanceTypeReadOnly', 'maintenanceTypePartial',
            'maintenanceAffectOwners', 'maintenanceShowContact',
            'maintenanceScopeCustomers', 'maintenanceScopeEveryone',
            'maintenanceTimingNow', 'maintenanceTimingScheduled', 'maintenanceNoEnd',
            'maintenanceStartAt', 'maintenanceEndAt',
            'maintenanceBlockBookings', 'maintenanceBlockPayments',
            'maintenanceBlockOwnerChanges', 'maintenanceBlockMessages',
            'maintenanceBlockReviews', 'maintenanceBlockRegistrations'
        ];

        generalControlIds.forEach(id => {
            const control = document.getElementById(id);
            if (control) control.disabled = true;
        });
    }

    if (!ADMIN_SETTINGS_PERMISSIONS.manageFinanceSettings) {
        [
            'commissionPercentage', 'defaultCurrency', 'autoUpdateExchangeRates',
            'sanaaUsdRate', 'adenUsdRate', 'sanaaSarRate', 'adenSarRate'
        ].forEach(id => {
            const control = document.getElementById(id);
            if (control) control.disabled = true;
        });

        const refreshExchangeButton =
            document.getElementById('refreshExchangeRatesNow');

        if (refreshExchangeButton) {
            refreshExchangeButton.disabled = true;
            refreshExchangeButton.setAttribute(
                'aria-disabled',
                'true'
            );
            refreshExchangeButton.title =
                'ليس لديك صلاحية إدارة إعدادات المالية.';
        }
    }

    if (!ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks) {
        const walletForm = document.getElementById('addSupportedWalletForm');
        if (walletForm) walletForm.hidden = true;

        const defaultWalletLogoPanel =
            document.querySelector('[data-default-wallet-logo-panel]');

        if (defaultWalletLogoPanel) {
            defaultWalletLogoPanel.classList.add('is-readonly');
        }

        const defaultWalletLogoUpload =
            document.getElementById('defaultWalletLogoUpload');

        const saveDefaultWalletLogoButton =
            document.getElementById('saveDefaultWalletLogoButton');

        if (defaultWalletLogoUpload) defaultWalletLogoUpload.disabled = true;
        if (saveDefaultWalletLogoButton) saveDefaultWalletLogoButton.disabled = true;
    }

    if (!ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides) {
        const slideForm = document.getElementById('addHeroSlideForm');
        if (slideForm) slideForm.hidden = true;
    }
}


function getSettingsAntiForgeryInput() {
    return ADMIN_SETTINGS_ROOT
        ?.querySelector(
            'input[type="hidden"][name*="RequestVerificationToken"], input[name="__RequestVerificationToken"]'
        ) || null;
}

function getSettingsAntiForgeryToken() {
    return getSettingsAntiForgeryInput()?.value || '';
}

function getSettingsPostHeaders() {
    const token = getSettingsAntiForgeryToken();

    const headers = {
        'X-Requested-With': 'XMLHttpRequest',
        'Accept': 'application/json'
    };

    /*
     * نرسل التوكن في الـHeader أيضًا.
     * والأهم: settingsPostJson يرسله داخل FormData كذلك،
     * حتى يعمل سواء كان المشروع يستخدم HeaderName افتراضيًا
     * أو يعتمد على FormFieldName.
     */
    if (token) {
        headers['RequestVerificationToken'] = token;
    }

    return headers;
}

function appendSettingsAntiForgeryToken(formData) {
    const input = getSettingsAntiForgeryInput();

    if (!input || !input.name || !input.value) {
        throw new Error(
            'تعذر العثور على رمز حماية الصفحة. حدّث الصفحة بـ Ctrl + F5 ثم حاول مرة أخرى.'
        );
    }

    if (!formData.has(input.name)) {
        formData.append(
            input.name,
            input.value
        );
    }

    return formData;
}

async function settingsPostJson(
    url,
    formData = null,
    options = {}
) {
    const body =
        formData instanceof FormData
            ? formData
            : new FormData();

    appendSettingsAntiForgeryToken(body);

    const response =
        await fetch(
            url,
            {
                method: 'POST',
                credentials: 'same-origin',
                cache: options.cache || 'no-store',
                headers: getSettingsPostHeaders(),
                body,
                signal: options.signal
            }
        );

    const responseText =
        await response.text();

    let data = null;

    if (responseText) {
        try {
            data = JSON.parse(responseText);
        } catch {
            data = null;
        }
    }

    if (!response.ok) {
        let message =
            data?.message ||
            data?.Message ||
            '';

        if (!message) {
            if (response.status === 400) {
                message =
                    'رفض الخادم الطلب. تم إصلاح إرسال رمز الحماية؛ إذا استمرت الرسالة نفّذ Ctrl + F5 مرة واحدة.';
            }
            else if (
                response.status === 401 ||
                response.status === 403
            ) {
                message =
                    'انتهت الجلسة أو ليست لديك صلاحية لهذه العملية.';
            }
            else if (response.status === 404) {
                message =
                    'مسار العملية غير موجود على الخادم.';
            }
            else {
                message =
                    `فشل الطلب من الخادم (HTTP ${response.status}).`;
            }
        }

        throw new Error(message);
    }

    if (!data) {
        throw new Error(
            'استجاب الخادم بصيغة غير متوقعة. أعد تحميل الصفحة وحاول مرة أخرى.'
        );
    }

    return data;
}

function showSettingsRequestError(
    title,
    error
) {
    console.error(
        title,
        error
    );

    Swal.fire({
        icon: 'error',
        title,
        text:
            error?.message ||
            'تعذر تنفيذ العملية. حاول مرة أخرى.',
        confirmButtonColor: '#00A6DA'
    });
}

function escapeSettingsHtml(value) {
    const element = document.createElement('div');
    element.textContent = String(value ?? '');
    return element.innerHTML;
}

function normalizeSettingsMediaPath(value, fallback = '') {
    const path = String(value ?? '').trim();

    return path.startsWith('/') && !path.startsWith('//')
        ? path
        : fallback;
}

function normalizeSettingsCssColor(value, fallback) {
    const color = String(value ?? '').trim();

    return /^#[0-9a-f]{3,8}$/i.test(color)
        ? color
        : fallback;
}


/* ==========================================================================
 * 🟢 القسم الأول: المحرك الأساسي (إدارة التبويبات والملاحة)
 * ========================================================================== */

let settingsWalletsLoaded = false;
let settingsSlidesLoaded = false;
let settingsWalletsAbortController = null;
let settingsSlidesAbortController = null;
let settingsSlidesVisibilityObserver = null;

function activateSettingsTab(targetClass, remember = true) {
    const root =
        document.querySelector('[data-admin-settings-root]');

    if (!root) return;

    const tabButtons =
        root.querySelectorAll('.custom-tabs-container .tab-btn');

    const tabContents =
        root.querySelectorAll('.settings-tab-panel');

    const targetButton =
        root.querySelector(
            `.custom-tabs-container .tab-btn[data-target="${targetClass}"]`
        );

    const targetContent =
        root.querySelector(`.${targetClass}.settings-tab-panel`);

    if (!targetButton || !targetContent) return;

    tabButtons.forEach(button => {
        const isCurrent = button === targetButton;
        button.classList.toggle('active', isCurrent);
        button.setAttribute(
            'aria-selected',
            isCurrent ? 'true' : 'false'
        );
        button.setAttribute(
            'tabindex',
            isCurrent ? '0' : '-1'
        );
    });

    tabContents.forEach(content => {
        const isCurrent = content === targetContent;
        content.classList.toggle(
            'is-active',
            isCurrent
        );
        content.setAttribute(
            'aria-hidden',
            isCurrent ? 'false' : 'true'
        );
    });

    if (remember) {
        localStorage.setItem(
            'activeSettingsTab',
            targetClass
        );
    }

    if (targetClass === 'wallets-tab') {
        loadSupportedWalletsDynamicList();
    } else if (targetClass === 'branding-tab') {
        scheduleHeroSlidesLoad();
    }
}

function scheduleHeroSlidesLoad() {
    if (settingsSlidesLoaded || settingsSlidesVisibilityObserver) return;

    const target =
        document.getElementById('heroSlidesTableBody');

    if (!target) return;

    if (!('IntersectionObserver' in window)) {
        loadHeroSlidesList();
        return;
    }

    settingsSlidesVisibilityObserver =
        new IntersectionObserver(
            entries => {
                if (entries.some(entry => entry.isIntersecting)) {
                    settingsSlidesVisibilityObserver?.disconnect();
                    settingsSlidesVisibilityObserver = null;
                    loadHeroSlidesList();
                }
            },
            { rootMargin: '220px 0px' }
        );

    settingsSlidesVisibilityObserver.observe(target);
}


function initializeSettingsTabKeyboardNavigation(root, tabButtons) {
    const buttons = Array.from(tabButtons);
    if (buttons.length === 0) return;

    const reduceMotion =
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const moveTo = (index, shouldFocus = true) => {
        const normalized =
            (index + buttons.length) % buttons.length;

        const nextButton = buttons[normalized];
        const targetClass =
            nextButton.getAttribute('data-target');

        if (!targetClass) return;

        activateSettingsTab(targetClass, true);

        if (shouldFocus) {
            nextButton.focus({ preventScroll: true });
        }

        nextButton.scrollIntoView({
            behavior: reduceMotion ? 'auto' : 'smooth',
            block: 'nearest',
            inline: 'nearest'
        });
    };

    const resolveNextIndex = (eventKey, index) => {
        const isRtl =
            getComputedStyle(root).direction === 'rtl';

        if (eventKey === 'ArrowLeft') {
            return isRtl ? index + 1 : index - 1;
        }

        if (eventKey === 'ArrowRight') {
            return isRtl ? index - 1 : index + 1;
        }

        if (eventKey === 'Home') {
            return 0;
        }

        if (eventKey === 'End') {
            return buttons.length - 1;
        }

        return null;
    };

    // التنقل القياسي عندما يكون التركيز على أحد أزرار التبويب.
    buttons.forEach((button, index) => {
        button.addEventListener('keydown', event => {
            const nextIndex =
                resolveNextIndex(event.key, index);

            if (nextIndex === null) return;

            event.preventDefault();
            moveTo(nextIndex, true);
        });
    });

    /*
     * تنقل إضافي مريح:
     * الأسهم تعمل داخل صفحة الإعدادات حتى لو لم يكن التركيز على زر التبويب،
     * لكن لا نتدخل أثناء الكتابة في input/textarea/select/contenteditable.
     */
    root.addEventListener('keydown', event => {
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
            return;
        }

        const target = event.target;

        if (
            target instanceof HTMLElement &&
            (
                target.matches('input, textarea, select, button, [contenteditable="true"]') ||
                target.closest('[role="dialog"], .swal2-container')
            )
        ) {
            return;
        }

        const activeIndex =
            Math.max(
                0,
                buttons.findIndex(
                    button => button.getAttribute('aria-selected') === 'true'
                )
            );

        const nextIndex =
            resolveNextIndex(event.key, activeIndex);

        if (nextIndex === null) return;

        event.preventDefault();
        moveTo(nextIndex, true);
    });
}


function formatSettingsFileSize(sizeInBytes) {
    if (!Number.isFinite(sizeInBytes) || sizeInBytes <= 0) {
        return '';
    }

    const sizeInMb =
        sizeInBytes / (1024 * 1024);

    return new Intl.NumberFormat(
        'ar-YE',
        {
            maximumFractionDigits: 1,
            minimumFractionDigits: 0
        }
    ).format(sizeInMb) + ' م.ب';
}


function updateSettingsFilePicker(input) {
    if (!input) return;

    const targetId =
        input.dataset.fileNameTarget;

    const target =
        targetId
            ? document.getElementById(targetId)
            : null;

    const previewId =
        input.dataset.filePreviewTarget;

    const preview =
        previewId
            ? document.getElementById(previewId)
            : null;

    const file =
        input.files?.[0];

    const dropZone =
        input.closest('[data-file-dropzone]') ||
        document.querySelector(
            `[data-file-dropzone][for="${input.id}"]`
        );

    if (!file) {
        if (target) {
            target.textContent =
                input.dataset.emptyLabel ||
                'اضغط لاختيار ملف';
        }

        if (preview?.dataset.previewObjectUrl) {
            URL.revokeObjectURL(
                preview.dataset.previewObjectUrl
            );
            delete preview.dataset.previewObjectUrl;
        }

        if (
            preview &&
            preview.dataset.initialMarkup !== undefined
        ) {
            preview.innerHTML =
                preview.dataset.initialMarkup;
        }

        dropZone?.classList.remove('has-file');
        return;
    }

    const sizeLabel =
        formatSettingsFileSize(file.size);

    if (target) {
        target.textContent =
            `تم اختيار: ${file.name}` +
            (sizeLabel ? ` • ${sizeLabel}` : '');
    }

    if (
        preview &&
        file.type &&
        file.type.startsWith('image/')
    ) {
        if (preview.dataset.previewObjectUrl) {
            URL.revokeObjectURL(
                preview.dataset.previewObjectUrl
            );
        }

        const objectUrl =
            URL.createObjectURL(file);

        preview.dataset.previewObjectUrl = objectUrl;
        preview.innerHTML = '';

        const image = document.createElement('img');
        image.src = objectUrl;
        image.alt = 'معاينة الصورة المختارة';
        image.className = 'settings-file-picker__thumb';
        preview.appendChild(image);
    }

    dropZone?.classList.add('has-file');
}


function resetSettingsFilePicker(input) {
    if (!input) return;

    input.value = '';
    updateSettingsFilePicker(input);
}


function initializeSettingsFilePickers(root) {
    if (!root || root.dataset.filePickersReady === 'true') {
        return;
    }

    root.dataset.filePickersReady = 'true';

    root.addEventListener('change', event => {
        const input =
            event.target.closest?.('[data-file-picker-input]');

        if (!input) return;

        updateSettingsFilePicker(input);
    });

    root.addEventListener('keydown', event => {
        const dropZone =
            event.target.closest?.('[data-file-dropzone]');

        if (!dropZone) return;

        if (event.key !== 'Enter' && event.key !== ' ') {
            return;
        }

        event.preventDefault();

        const input =
            dropZone.querySelector('[data-file-picker-input]') ||
            document.getElementById(
                dropZone.getAttribute('for') || ''
            );

        input?.click();
    });

    root.addEventListener('dragover', event => {
        const dropZone =
            event.target.closest?.('[data-file-dropzone]');

        if (!dropZone) return;

        event.preventDefault();
        dropZone.classList.add('is-dragover');
    });

    root.addEventListener('dragleave', event => {
        const dropZone =
            event.target.closest?.('[data-file-dropzone]');

        if (!dropZone) return;

        if (
            event.relatedTarget &&
            dropZone.contains(event.relatedTarget)
        ) {
            return;
        }

        dropZone.classList.remove('is-dragover');
    });

    root.addEventListener('drop', event => {
        const dropZone =
            event.target.closest?.('[data-file-dropzone]');

        if (!dropZone) return;

        event.preventDefault();
        dropZone.classList.remove('is-dragover');

        const input =
            dropZone.querySelector('[data-file-picker-input]') ||
            document.getElementById(
                dropZone.getAttribute('for') || ''
            );

        const file =
            event.dataTransfer?.files?.[0];

        if (!input || !file) return;

        try {
            const transfer = new DataTransfer();
            transfer.items.add(file);
            input.files = transfer.files;

            input.dispatchEvent(
                new Event('change', { bubbles: true })
            );
        }
        catch {
            input.click();
        }
    });

    root
        .querySelectorAll('[data-file-picker-input]')
        .forEach(input => {
            const previewId =
                input.dataset.filePreviewTarget;

            const preview =
                previewId
                    ? document.getElementById(previewId)
                    : null;

            if (
                preview &&
                preview.dataset.initialMarkup === undefined
            ) {
                preview.dataset.initialMarkup =
                    preview.innerHTML;
            }

            updateSettingsFilePicker(input);
        });
}


document.addEventListener('DOMContentLoaded', function () {
    applySettingsPermissionState();

    const root =
        document.querySelector('[data-admin-settings-root]');

    if (!root) return;

    const tabButtons =
        root.querySelectorAll('.custom-tabs-container .tab-btn');

    const savedTab =
        localStorage.getItem('activeSettingsTab');

    const savedButton =
        savedTab
            ? root.querySelector(
                `.custom-tabs-container .tab-btn[data-target="${savedTab}"]`
            )
            : null;

    const initialTab =
        savedButton
            ? savedTab
            : 'branding-tab';

    activateSettingsTab(initialTab, false);
    initializeSettingsTabKeyboardNavigation(root, tabButtons);
    initializeSettingsFilePickers(root);

    tabButtons.forEach(button => {
        button.addEventListener('click', function () {
            const targetClass =
                this.getAttribute('data-target');

            if (targetClass) {
                activateSettingsTab(
                    targetClass,
                    true
                );
            }
        });
    });

    document
        .querySelectorAll('.btn-save-tab')
        .forEach(button => {
            button.addEventListener(
                'click',
                saveAllSettings
            );
        });

    const heroForm =
        document.getElementById('addHeroSlideForm');

    if (heroForm) {
        heroForm.addEventListener(
            'submit',
            addHeroSlideDynamically
        );
    }

    const walletForm =
        document.getElementById('addSupportedWalletForm');

    if (walletForm) {
        walletForm.addEventListener(
            'submit',
            createPlatformWallet
        );
    }

    initializeExchangeRateControls();
    initializeMaintenanceControls();
    initializeDefaultWalletLogoControls();
});


/* ==========================================================================
 * 💾 القسم الثاني: الإرسال والحفظ (حفظ الإعدادات العامة والمواسم والعملات)
 * ========================================================================== */
async function saveAllSettings(event) {
    const button =
        event?.currentTarget instanceof HTMLElement
            ? event.currentTarget
            : null;

    const section =
        button?.dataset.saveSection ||
        document.querySelector(
            '.settings-tab-panel.is-active .btn-save-tab[data-save-section]'
        )?.dataset.saveSection ||
        '';

    const canSave =
        section === 'policies'
            ? (
                ADMIN_SETTINGS_PERMISSIONS.manageGeneral ||
                ADMIN_SETTINGS_PERMISSIONS.manageFinanceSettings
            )
            : ADMIN_SETTINGS_PERMISSIONS.manageGeneral;

    if (!section || !canSave) {
        showPermissionDenied();
        return;
    }

    if (
        section === 'policies' &&
        ADMIN_SETTINGS_PERMISSIONS.manageFinanceSettings &&
        !isAutomaticExchangeModeEnabled()
    ) {
        const validation =
            validateManualExchangeRates();

        if (!validation.valid) {
            Swal.fire({
                icon: 'warning',
                title: 'راجع أسعار الصرف',
                text: validation.message,
                confirmButtonColor: '#00A6DA'
            });

            validation.control?.focus();
            return;
        }
    }

    if (
        section === 'policies' &&
        ADMIN_SETTINGS_PERMISSIONS.manageGeneral
    ) {
        const imageLimitsValidation =
            validateImageLimitsSettings();

        if (!imageLimitsValidation.valid) {
            Swal.fire({
                icon: 'warning',
                title: 'راجع حدود الصور',
                text: imageLimitsValidation.message,
                confirmButtonColor: '#00A6DA'
            });

            imageLimitsValidation.control?.focus();
            return;
        }
    }

    if (section === 'maintenance') {
        const maintenanceValidation =
            validateMaintenanceSettings();

        if (!maintenanceValidation.valid) {
            Swal.fire({
                icon: 'warning',
                title: 'راجع إعدادات الصيانة',
                text: maintenanceValidation.message,
                confirmButtonColor: '#00A6DA'
            });

            maintenanceValidation.control?.focus();
            return;
        }

        if (
            document.getElementById('maintenanceMode')?.checked === true &&
            getSelectedMaintenanceType() === 'Full'
        ) {
            const confirmation = await Swal.fire({
                icon: 'warning',
                title: 'تفعيل الإغلاق الكامل؟',
                text: 'بعد الحفظ ستظهر شاشة الصيانة للفئات التي اخترتها ولن تستطيع استخدام المنصة بصورة طبيعية حتى توقف الصيانة.',
                showCancelButton: true,
                confirmButtonText: 'نعم، حفظ وتفعيل',
                cancelButtonText: 'رجوع',
                confirmButtonColor: '#ff5a5f',
                cancelButtonColor: '#64748b',
                reverseButtons: true,
                focusCancel: true
            });

            if (!confirmation.isConfirmed) {
                return;
            }
        }
    }

    const originalHtml =
        button?.innerHTML || '';

    if (button) {
        button.disabled = true;
        button.classList.add('is-loading');
        button.innerHTML =
            '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>جاري الحفظ...</span>';
    }

    const formData = new FormData();
    formData.append('Section', section);

    if (section === 'branding') {
        formData.append(
            'SiteName',
            document.getElementById('siteName')?.value || ''
        );

        formData.append(
            'SiteDescription',
            document.getElementById('siteDescription')?.value || ''
        );

        const logoInput =
            document.getElementById('logoFile');

        if (logoInput?.files?.length > 0) {
            formData.append(
                'LogoUpload',
                logoInput.files[0]
            );
        }

        const faviconInput =
            document.getElementById('faviconFile');

        if (faviconInput?.files?.length > 0) {
            formData.append(
                'FaviconUpload',
                faviconInput.files[0]
            );
        }

        const heroTitleMain =
            document.getElementById('heroTitleMain');

        if (heroTitleMain) {
            formData.append(
                'HeroTitleMain',
                heroTitleMain.value
            );
        }

        const heroTitleSub =
            document.getElementById('heroTitleSub');

        if (heroTitleSub) {
            formData.append(
                'HeroTitleSub',
                heroTitleSub.value
            );
        }

        const seasonTheme =
            document.getElementById('seasonTheme');

        if (seasonTheme) {
            formData.append(
                'SeasonTheme',
                seasonTheme.value
            );
        }

        const isBannerActive =
            document.getElementById('isBannerActive');

        if (isBannerActive) {
            formData.append(
                'IsBannerActive',
                isBannerActive.checked
            );
        }

        const bannerText =
            document.getElementById('bannerText');

        if (bannerText) {
            formData.append(
                'BannerText',
                bannerText.value
            );
        }
    }

    if (section === 'policies') {
        if (ADMIN_SETTINGS_PERMISSIONS.manageGeneral) {
            formData.append(
                'AutoApprovePlaces',
                document.getElementById('autoApprovePlaces')?.checked === true
            );

            formData.append(
                'AllowNewRegistrations',
                document.getElementById('allowRegistration')?.checked === true
            );

            formData.append(
                'MaxImageSize',
                document.getElementById('maxImageSize')?.value || 5
            );

            formData.append(
                'MaxImageCount',
                document.getElementById('maxImageCount')?.value || 10
            );
        }

        if (ADMIN_SETTINGS_PERMISSIONS.manageFinanceSettings) {
            formData.append(
                'CommissionPercentage',
                document.getElementById('commissionPercentage')?.value || 0
            );

            formData.append(
                'DefaultCurrency',
                document.getElementById('defaultCurrency')?.value || 'ريال يمني'
            );

            formData.append(
                'AutoUpdateExchangeRates',
                document.getElementById('autoUpdateExchangeRates')?.checked === true
            );

            formData.append(
                'sanaaUsdRate',
                document.getElementById('sanaaUsdRate')?.value || 0
            );

            formData.append(
                'adenUsdRate',
                document.getElementById('adenUsdRate')?.value || 0
            );

            formData.append(
                'sanaaSarRate',
                document.getElementById('sanaaSarRate')?.value || 0
            );

            formData.append(
                'adenSarRate',
                document.getElementById('adenSarRate')?.value || 0
            );
        }
    }

    if (section === 'contact') {
        formData.append(
            'ContactEmail',
            document.getElementById('officialEmail')?.value || ''
        );

        formData.append(
            'SupportPhone',
            document.getElementById('supportPhone')?.value || ''
        );

        formData.append(
            'FacebookLink',
            document.getElementById('fbLink')?.value || ''
        );

        formData.append(
            'TwitterLink',
            document.getElementById('xLink')?.value || ''
        );

        formData.append(
            'InstagramLink',
            document.getElementById('instaLink')?.value || ''
        );

        formData.append(
            'WhatsAppNumber',
            document.getElementById('waLink')?.value || ''
        );
    }

    if (section === 'maintenance') {
        syncMaintenanceScopeToHidden();

        const maintenanceSchedule =
            getMaintenanceSchedulePayload();

        formData.append(
            'MaintenanceMode',
            document.getElementById('maintenanceMode')?.checked === true
        );

        formData.append(
            'MaintenanceTitle',
            document.getElementById('maintenanceTitle')?.value || ''
        );

        formData.append(
            'MaintenanceMessage',
            document.getElementById('maintenanceMessage')?.value || ''
        );

        formData.append(
            'MaintenanceType',
            getSelectedMaintenanceType()
        );

        formData.append(
            'MaintenanceAffectOwners',
            document.getElementById('maintenanceAffectOwners')?.checked === true
        );

        formData.append(
            'MaintenanceStartAt',
            maintenanceSchedule.start
        );

        formData.append(
            'MaintenanceEndAt',
            maintenanceSchedule.end
        );

        formData.append(
            'MaintenanceShowContact',
            document.getElementById('maintenanceShowContact')?.checked === true
        );

        formData.append(
            'MaintenanceBlockBookings',
            document.getElementById('maintenanceBlockBookings')?.checked === true
        );

        formData.append(
            'MaintenanceBlockPayments',
            document.getElementById('maintenanceBlockPayments')?.checked === true
        );

        formData.append(
            'MaintenanceBlockOwnerChanges',
            document.getElementById('maintenanceBlockOwnerChanges')?.checked === true
        );

        formData.append(
            'MaintenanceBlockMessages',
            document.getElementById('maintenanceBlockMessages')?.checked === true
        );

        formData.append(
            'MaintenanceBlockReviews',
            document.getElementById('maintenanceBlockReviews')?.checked === true
        );

        formData.append(
            'MaintenanceBlockRegistrations',
            document.getElementById('maintenanceBlockRegistrations')?.checked === true
        );
    }

    try {
        const result =
            await settingsPostJson(
                '/Admin/Settings/SaveSettings',
                formData
            );

        if (!result.success) {
            throw new Error(
                result.message ||
                'تعذر حفظ الإعدادات.'
            );
        }

        if (result.exchangeRates) {
            applyExchangeRatePayload(
                result.exchangeRates
            );
        }

        if (result.maintenance) {
            applyMaintenancePayload(
                result.maintenance
            );
        }

        if (
            result.defaultWalletLogoPath &&
            ADMIN_SETTINGS_ROOT
        ) {
            ADMIN_SETTINGS_ROOT.dataset.defaultWalletLogo =
                result.defaultWalletLogoPath;
        }

        Swal.fire({
            icon: 'success',
            title: 'تم الحفظ',
            text: result.message || 'تم حفظ التعديلات بنجاح.',
            confirmButtonColor: '#00A6DA',
            timer: 2200,
            timerProgressBar: true
        });
    }
    catch (error) {
        console.error(
            'Settings save error:',
            error
        );

        Swal.fire({
            icon: 'error',
            title: 'تعذر الحفظ',
            text:
                error.message ||
                'تعذر تنفيذ عملية الحفظ.',
            confirmButtonColor: '#00A6DA'
        });
    }
    finally {
        if (button) {
            button.disabled = false;
            button.classList.remove('is-loading');
            button.innerHTML = originalHtml;
        }
    }
}



/* ==========================================================================
 * 🛠️ مركز الصيانة — نسخة مبسطة: تشغيل ← نوع ← فئة ← وقت ← رسالة
 * ========================================================================== */

let maintenanceUiFrame = 0;

function scheduleMaintenanceUiSync() {
    cancelAnimationFrame(maintenanceUiFrame);

    maintenanceUiFrame =
        requestAnimationFrame(() => {
            syncMaintenanceUi();
        });
}



function initializeMaintenanceControls() {
    const center =
        document.querySelector('[data-maintenance-center]');

    if (!center) return;

    const directControls = [
        'maintenanceMode',
        'maintenanceTitle',
        'maintenanceMessage',
        'maintenanceShowContact',
        'maintenanceStartAt',
        'maintenanceEndAt',
        'maintenanceNoEnd',
        'maintenanceBlockBookings',
        'maintenanceBlockPayments',
        'maintenanceBlockOwnerChanges',
        'maintenanceBlockMessages',
        'maintenanceBlockReviews',
        'maintenanceBlockRegistrations'
    ];

    directControls.forEach(id => {
        const control = document.getElementById(id);
        if (!control) return;

        const eventName =
            control.matches('input[type="text"], textarea, input[type="datetime-local"]')
                ? 'input'
                : 'change';

        control.addEventListener(eventName, scheduleMaintenanceUiSync);
    });

    document
        .querySelectorAll('input[name="maintenanceType"]')
        .forEach(control => {
            control.addEventListener('change', scheduleMaintenanceUiSync);
        });

    document
        .querySelectorAll('input[name="maintenanceScope"]')
        .forEach(control => {
            control.addEventListener('change', () => {
                syncMaintenanceScopeToHidden();
                scheduleMaintenanceUiSync();
            });
        });

    document
        .querySelectorAll('input[name="maintenanceTiming"]')
        .forEach(control => {
            control.addEventListener('change', scheduleMaintenanceUiSync);
        });

    syncMaintenanceScopeToHidden();
    syncMaintenanceUi();
}

function getSelectedMaintenanceType() {
    return document.querySelector(
        'input[name="maintenanceType"]:checked'
    )?.value || 'Full';
}

function getSelectedMaintenanceScope() {
    return document.querySelector(
        'input[name="maintenanceScope"]:checked'
    )?.value || 'customers';
}

function syncMaintenanceScopeToHidden() {
    const affectOwners =
        document.getElementById('maintenanceAffectOwners');

    if (!affectOwners) return;

    affectOwners.checked =
        getSelectedMaintenanceScope() === 'everyone';
}

function getMaintenanceTimingMode() {
    return document.querySelector(
        'input[name="maintenanceTiming"]:checked'
    )?.value || 'now';
}

function getMaintenanceSchedulePayload() {
    if (getMaintenanceTimingMode() !== 'scheduled') {
        return { start: '', end: '' };
    }

    const start =
        document.getElementById('maintenanceStartAt')?.value || '';

    const noEnd =
        document.getElementById('maintenanceNoEnd')?.checked === true;

    const end = noEnd
        ? ''
        : document.getElementById('maintenanceEndAt')?.value || '';

    return { start, end };
}

function syncMaintenanceUi() {
    const enabled =
        document.getElementById('maintenanceMode')?.checked === true;

    const type =
        getSelectedMaintenanceType();

    syncMaintenanceScopeToHidden();

    const center =
        document.querySelector('[data-maintenance-center]');

    const statusBoard =
        document.querySelector('[data-maintenance-status-board]');

    const setupPanel =
        document.getElementById('maintenanceSetupPanel');

    const normalNote =
        document.getElementById('maintenanceNormalNote');

    const partialPanel =
        document.getElementById('maintenancePartialPanel');

    const schedulePanel =
        document.getElementById('maintenanceSchedulePanel');

    const endField =
        document.getElementById('maintenanceEndField');

    const noEnd =
        document.getElementById('maintenanceNoEnd')?.checked === true;

    const statusTitle =
        document.getElementById('maintenanceStatusTitle');

    const statusDescription =
        document.getElementById('maintenanceStatusDescription');

    const powerLabel =
        document.getElementById('maintenancePowerLabel');

    const heroStatus =
        document.getElementById('maintenanceHeroStatus');

    const saveButtonText =
        document.getElementById('maintenanceSaveButtonText');

    if (center) {
        center.dataset.enabled = enabled ? 'true' : 'false';
        center.dataset.type = type;
    }

    if (statusBoard) {
        statusBoard.dataset.enabled = enabled ? 'true' : 'false';
    }

    if (setupPanel) {
        setupPanel.hidden = !enabled;
    }

    if (normalNote) {
        normalNote.hidden = enabled;
    }

    document
        .querySelectorAll('[data-maintenance-type-card]')
        .forEach(card => {
            card.classList.toggle(
                'is-selected',
                card.dataset.maintenanceTypeCard === type
            );
        });

    const selectedScope = getSelectedMaintenanceScope();

    document
        .querySelectorAll('[data-maintenance-scope-card]')
        .forEach(card => {
            card.classList.toggle(
                'is-selected',
                card.dataset.maintenanceScopeCard === selectedScope
            );
        });

    const timingMode = getMaintenanceTimingMode();

    document
        .querySelectorAll('[data-maintenance-timing-card]')
        .forEach(card => {
            card.classList.toggle(
                'is-selected',
                card.dataset.maintenanceTimingCard === timingMode
            );
        });

    if (partialPanel) {
        partialPanel.hidden = !(enabled && type === 'Partial');
    }

    if (schedulePanel) {
        schedulePanel.hidden = !(enabled && timingMode === 'scheduled');
    }

    if (endField) {
        endField.classList.toggle('is-disabled', noEnd);
    }

    const endInput =
        document.getElementById('maintenanceEndAt');

    if (endInput) {
        endInput.disabled = noEnd || timingMode !== 'scheduled';
    }

    const startInput =
        document.getElementById('maintenanceStartAt');

    if (startInput) {
        startInput.disabled = timingMode !== 'scheduled';
    }

    if (statusTitle) {
        statusTitle.textContent = enabled
            ? 'وضع الصيانة مفعّل'
            : 'المنصة تعمل بشكل طبيعي';
    }

    if (statusDescription) {
        statusDescription.textContent = enabled
            ? getMaintenanceTypeDescription(type)
            : 'لا توجد قيود صيانة مفعلة الآن، وجميع الخدمات تعمل بصورة طبيعية.';
    }

    if (powerLabel) {
        powerLabel.textContent = enabled
            ? 'إيقاف الصيانة'
            : 'تشغيل الصيانة';
    }

    if (heroStatus) {
        heroStatus.textContent = enabled
            ? getMaintenanceTypeShortLabel(type)
            : 'تعمل بصورة طبيعية';
    }

    if (saveButtonText) {
        saveButtonText.textContent = enabled
            ? 'حفظ وتطبيق إعدادات الصيانة'
            : 'حفظ وإيقاف وضع الصيانة';
    }

    syncMaintenanceOperationCards();
    syncMaintenanceMessageCounter();
    syncMaintenanceScheduleSummary();
    syncMaintenanceImpactPreview();
}

function getMaintenanceTypeShortLabel(type) {
    switch (type) {
        case 'ReadOnly':
            return 'تصفح فقط';
        case 'Partial':
            return 'خدمات محددة';
        default:
            return 'إغلاق كامل';
    }
}

function getMaintenanceTypeDescription(type) {
    switch (type) {
        case 'ReadOnly':
            return 'المستخدمون يستطيعون التصفح والمشاهدة، لكن العمليات التي تغيّر البيانات تتوقف.';
        case 'Partial':
            return 'المنصة تستمر بالعمل، وسيتم إيقاف الخدمات التي تختارها فقط.';
        default:
            return 'سيتم إغلاق المنصة أمام الفئات التي اخترتها وإظهار شاشة الصيانة لهم.';
    }
}

function syncMaintenanceOperationCards() {
    document
        .querySelectorAll('.maintenance-service-card input[type="checkbox"]')
        .forEach(input => {
            const card = input.closest('.maintenance-service-card');
            const state = card?.querySelector('.maintenance-service-card__state');

            if (!card) return;

            card.classList.toggle('is-blocked', input.checked);

            if (state) {
                state.textContent = input.checked
                    ? 'سيتم إيقافه'
                    : 'يعمل';
            }
        });
}

function syncMaintenanceMessageCounter() {
    const message =
        document.getElementById('maintenanceMessage');

    const counter =
        document.getElementById('maintenanceMessageCounter');

    if (!message || !counter) return;

    counter.textContent =
        `${message.value.length} / 500`;
}

function syncMaintenanceScheduleSummary() {
    const summary =
        document.querySelector('#maintenanceScheduleSummary span');

    if (!summary) return;

    const timingMode =
        getMaintenanceTimingMode();

    if (timingMode !== 'scheduled') {
        summary.textContent =
            'سيبدأ وضع الصيانة فورًا بعد الحفظ ويستمر حتى توقفه يدويًا.';
        return;
    }

    const schedule =
        getMaintenanceSchedulePayload();

    if (!schedule.start) {
        summary.textContent =
            'حدد موعد البداية أولًا.';
        return;
    }

    if (!schedule.end) {
        summary.textContent =
            `تبدأ الصيانة في ${formatMaintenanceLocalDate(schedule.start)} وتستمر حتى توقفها يدويًا.`;
        return;
    }

    summary.textContent =
        `تبدأ الصيانة في ${formatMaintenanceLocalDate(schedule.start)} وتنتهي تلقائيًا في ${formatMaintenanceLocalDate(schedule.end)}.`;
}

function formatMaintenanceLocalDate(value) {
    if (!value) return '—';

    const parsed = new Date(value);

    if (Number.isNaN(parsed.getTime())) {
        return value.replace('T', ' ');
    }

    return new Intl.DateTimeFormat(
        'ar-YE',
        {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            hour12: true
        }
    ).format(parsed);
}

function syncMaintenanceImpactPreview() {
    const enabled =
        document.getElementById('maintenanceMode')?.checked === true;

    const type =
        getSelectedMaintenanceType();

    const scope =
        getSelectedMaintenanceScope();

    const title =
        document.getElementById('maintenancePreviewTitle');

    const text =
        document.getElementById('maintenancePreviewText');

    const badge =
        document.getElementById('maintenancePreviewBadge');

    const preview =
        document.querySelector('[data-maintenance-preview]');

    if (!title || !text || !badge || !preview) return;

    preview.dataset.mode = enabled ? type : 'Normal';

    if (!enabled) {
        title.textContent = 'سيتم حفظ المنصة بوضعها الطبيعي';
        text.textContent = 'لن يتم منع العملاء أو الزوار أو المالكين بسبب الصيانة.';
        badge.textContent = 'طبيعي';
        return;
    }

    const audienceText = scope === 'everyone'
        ? 'العملاء والزوار والمالكون'
        : 'العملاء والزوار فقط';

    const timingText =
        getMaintenanceTimingMode() === 'scheduled'
            ? buildMaintenanceTimingPreviewText()
            : 'وتبدأ بعد الحفظ مباشرة';

    if (type === 'Full') {
        title.textContent = 'سيتم إغلاق المنصة بالكامل';
        text.textContent = `ستظهر شاشة الصيانة لـ ${audienceText}، ${timingText}.`;
        badge.textContent = 'إغلاق كامل';
        return;
    }

    if (type === 'ReadOnly') {
        title.textContent = 'ستصبح المنصة للتصفح فقط';
        text.textContent = `سيتمكن ${audienceText} من المشاهدة دون تنفيذ العمليات التي تغيّر البيانات، ${timingText}.`;
        badge.textContent = 'تصفح فقط';
        return;
    }

    const blockedLabels =
        getSelectedPartialOperationLabels();

    title.textContent = blockedLabels.length > 0
        ? `سيتم إيقاف ${blockedLabels.length} خدمات فقط`
        : 'اختر الخدمات التي تريد إيقافها';

    text.textContent = blockedLabels.length > 0
        ? `الخدمات المتوقفة: ${blockedLabels.join('، ')}. سيطبق ذلك على ${audienceText}، ${timingText}.`
        : 'الصيانة الجزئية تحتاج اختيار خدمة واحدة على الأقل.';

    badge.textContent = 'خدمات محددة';
}

function buildMaintenanceTimingPreviewText() {
    const schedule =
        getMaintenanceSchedulePayload();

    if (!schedule.start) {
        return 'لكن موعد البداية لم يُحدد بعد';
    }

    if (!schedule.end) {
        return `وتبدأ في ${formatMaintenanceLocalDate(schedule.start)}`;
    }

    return `وتعمل من ${formatMaintenanceLocalDate(schedule.start)} حتى ${formatMaintenanceLocalDate(schedule.end)}`;
}

function getSelectedPartialOperationLabels() {
    const options = [
        ['maintenanceBlockBookings', 'الحجوزات'],
        ['maintenanceBlockPayments', 'المدفوعات'],
        ['maintenanceBlockOwnerChanges', 'تعديلات المالك'],
        ['maintenanceBlockMessages', 'الرسائل'],
        ['maintenanceBlockReviews', 'التقييمات'],
        ['maintenanceBlockRegistrations', 'التسجيل']
    ];

    return options
        .filter(([id]) => document.getElementById(id)?.checked === true)
        .map(([, label]) => label);
}

function validateMaintenanceSettings() {
    const enabled =
        document.getElementById('maintenanceMode')?.checked === true;

    if (!enabled) {
        return { valid: true };
    }

    const title =
        document.getElementById('maintenanceTitle');

    const message =
        document.getElementById('maintenanceMessage');

    if (!String(title?.value || '').trim()) {
        return {
            valid: false,
            message: 'اكتب عنوانًا واضحًا لشاشة الصيانة.',
            control: title
        };
    }

    if (!String(message?.value || '').trim()) {
        return {
            valid: false,
            message: 'اكتب رسالة الصيانة التي ستظهر للمستخدمين.',
            control: message
        };
    }

    if (getMaintenanceTimingMode() === 'scheduled') {
        const schedule =
            getMaintenanceSchedulePayload();

        const startControl =
            document.getElementById('maintenanceStartAt');

        const endControl =
            document.getElementById('maintenanceEndAt');

        if (!schedule.start) {
            return {
                valid: false,
                message: 'اختر تاريخ ووقت بداية الصيانة المجدولة.',
                control: startControl
            };
        }

        if (schedule.end) {
            const startDate = new Date(schedule.start);
            const endDate = new Date(schedule.end);

            if (endDate <= startDate) {
                return {
                    valid: false,
                    message: 'موعد انتهاء الصيانة يجب أن يكون بعد موعد البداية.',
                    control: endControl
                };
            }
        }
    }

    if (
        getSelectedMaintenanceType() === 'Partial' &&
        getSelectedPartialOperationLabels().length === 0
    ) {
        return {
            valid: false,
            message: 'اختر خدمة واحدة على الأقل لإيقافها في وضع «خدمات محددة».',
            control: document.getElementById('maintenanceBlockBookings')
        };
    }

    return { valid: true };
}

function validateImageLimitsSettings() {
    const sizeControl =
        document.getElementById('maxImageSize');

    const countControl =
        document.getElementById('maxImageCount');

    const size = Number.parseInt(sizeControl?.value || '', 10);
    const count = Number.parseInt(countControl?.value || '', 10);

    if (!Number.isInteger(size) || size < 1 || size > 100) {
        return {
            valid: false,
            message: 'الحد الأقصى لحجم الصورة يجب أن يكون بين 1 و100 ميجابايت.',
            control: sizeControl
        };
    }

    if (!Number.isInteger(count) || count < 1 || count > 100) {
        return {
            valid: false,
            message: 'الحد الأقصى لعدد الصور يجب أن يكون بين 1 و100 صورة.',
            control: countControl
        };
    }

    return { valid: true };
}

function applyMaintenancePayload(payload) {
    if (!payload) return;

    const enabled =
        document.getElementById('maintenanceMode');

    if (enabled && typeof payload.enabled === 'boolean') {
        enabled.checked = payload.enabled;
    }

    const typeControl =
        document.querySelector(
            `input[name="maintenanceType"][value="${payload.type || 'Full'}"]`
        );

    if (typeControl) {
        typeControl.checked = true;
    }

    syncMaintenanceUi();
}


/* ==========================================================================
 * 💱 مركز أسعار الصرف — صنعاء / عدن
 * ========================================================================== */

function initializeExchangeRateControls() {
    const autoToggle =
        document.getElementById(
            'autoUpdateExchangeRates'
        );

    const refreshButton =
        document.getElementById(
            'refreshExchangeRatesNow'
        );

    if (autoToggle) {
        autoToggle.addEventListener(
            'change',
            function () {
                syncExchangeRateModeUi();

                if (
                    this.checked &&
                    ADMIN_SETTINGS_PERMISSIONS.manageFinanceSettings
                ) {
                    showExchangeModeHint(
                        'تم اختيار الوضع التلقائي',
                        'احفظ الإعدادات أولاً، وبعد الحفظ يمكنك تنفيذ تحديث فوري.'
                    );
                }
            }
        );
    }

    if (refreshButton) {
        refreshButton.addEventListener(
            'click',
            refreshExchangeRatesNow
        );
    }

    syncExchangeRateModeUi();
}


function isAutomaticExchangeModeEnabled() {
    return document
        .getElementById(
            'autoUpdateExchangeRates'
        )?.checked === true;
}


function syncExchangeRateModeUi() {
    const autoToggle =
        document.getElementById(
            'autoUpdateExchangeRates'
        );

    const modeCard =
        document.querySelector(
            '[data-exchange-mode-card]'
        );

    const modeBadge =
        document.getElementById(
            'exchangeRateModeBadge'
        );

    const refreshButton =
        document.getElementById(
            'refreshExchangeRatesNow'
        );

    const rateInputs =
        Array.from(
            document.querySelectorAll(
                '[data-exchange-rate-input]'
            )
        );

    if (!autoToggle) return;

    const isAuto =
        autoToggle.checked === true;

    if (modeCard) {
        modeCard.classList.toggle(
            'is-auto',
            isAuto
        );

        modeCard.classList.toggle(
            'is-manual',
            !isAuto
        );
    }

    if (modeBadge) {
        modeBadge.classList.toggle(
            'is-auto',
            isAuto
        );

        modeBadge.classList.toggle(
            'is-manual',
            !isAuto
        );

        const icon =
            modeBadge.querySelector('i');

        const label =
            modeBadge.querySelector('span');

        if (icon) {
            icon.className =
                isAuto
                    ? 'bi bi-lightning-charge-fill'
                    : 'bi bi-pencil-fill';
        }

        if (label) {
            label.textContent =
                isAuto
                    ? 'تلقائي'
                    : 'يدوي';
        }
    }

    rateInputs.forEach(input => {
        const financeAllowed =
            ADMIN_SETTINGS_PERMISSIONS
                .manageFinanceSettings;

        input.readOnly =
            isAuto && financeAllowed;

        input.classList.toggle(
            'is-exchange-readonly',
            isAuto
        );

        if (!financeAllowed) {
            input.disabled = true;
        }

        input.setAttribute(
            'aria-readonly',
            isAuto ? 'true' : 'false'
        );
    });

    if (refreshButton) {
        const financeAllowed =
            ADMIN_SETTINGS_PERMISSIONS
                .manageFinanceSettings;

        refreshButton.disabled =
            !financeAllowed || !isAuto;

        refreshButton.setAttribute(
            'aria-disabled',
            refreshButton.disabled
                ? 'true'
                : 'false'
        );

        refreshButton.title =
            !financeAllowed
                ? 'ليس لديك صلاحية إدارة إعدادات المالية.'
                : !isAuto
                    ? 'فعّل التحديث التلقائي واحفظ الإعدادات أولاً.'
                    : 'جلب أحدث أسعار صنعاء وعدن الآن.';
    }
}


function validateManualExchangeRates() {
    const fields = [
        {
            id: 'sanaaUsdRate',
            label: 'دولار سوق صنعاء'
        },
        {
            id: 'sanaaSarRate',
            label: 'الريال السعودي في سوق صنعاء'
        },
        {
            id: 'adenUsdRate',
            label: 'دولار سوق عدن'
        },
        {
            id: 'adenSarRate',
            label: 'الريال السعودي في سوق عدن'
        }
    ];

    const values = {};

    for (const field of fields) {
        const control =
            document.getElementById(
                field.id
            );

        if (!control) {
            return {
                valid: false,
                message:
                    `تعذر العثور على حقل ${field.label}.`,
                control: null
            };
        }

        const value =
            Number.parseFloat(
                control.value
            );

        if (
            !Number.isFinite(value) ||
            value <= 0
        ) {
            return {
                valid: false,
                message:
                    `أدخل قيمة صحيحة أكبر من صفر في ${field.label}.`,
                control
            };
        }

        values[field.id] = value;
    }

    const sanaaRatio =
        values.sanaaUsdRate /
        values.sanaaSarRate;

    const adenRatio =
        values.adenUsdRate /
        values.adenSarRate;

    if (
        sanaaRatio < 3 ||
        sanaaRatio > 5
    ) {
        return {
            valid: false,
            message:
                'قيم صنعاء غير منطقية؛ راجع سعر الدولار والسعودي قبل الحفظ.',
            control:
                document.getElementById(
                    'sanaaUsdRate'
                )
        };
    }

    if (
        adenRatio < 3 ||
        adenRatio > 5
    ) {
        return {
            valid: false,
            message:
                'قيم عدن غير منطقية؛ راجع سعر الدولار والسعودي قبل الحفظ.',
            control:
                document.getElementById(
                    'adenUsdRate'
                )
        };
    }

    return {
        valid: true,
        message: '',
        control: null
    };
}


async function refreshExchangeRatesNow() {
    if (
        !ADMIN_SETTINGS_PERMISSIONS
            .manageFinanceSettings
    ) {
        showPermissionDenied();
        return;
    }

    if (!isAutomaticExchangeModeEnabled()) {
        Swal.fire({
            icon: 'info',
            title: 'الوضع اليدوي مفعّل',
            text: 'فعّل التحديث التلقائي واحفظ الإعدادات أولاً، ثم استخدم التحديث الفوري.',
            confirmButtonColor: '#00A6DA'
        });

        return;
    }

    const refreshButton =
        document.getElementById(
            'refreshExchangeRatesNow'
        );

    const originalButtonHtml =
        refreshButton?.innerHTML ?? '';

    if (refreshButton) {
        refreshButton.disabled = true;
        refreshButton.classList.add(
            'is-loading'
        );
        refreshButton.innerHTML =
            '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span>جاري التحديث...</span>';
    }

    setExchangeMonitorStatus(
        'Loading',
        'جاري الاتصال بالمصدر'
    );

    try {
        const data =
            await settingsPostJson(
                '/Admin/Settings/RefreshExchangeRatesNow'
            );

        if (data.exchangeRates) {
            applyExchangeRatePayload(
                data.exchangeRates
            );
        }

        if (!data.success) {
            Swal.fire({
                icon: 'error',
                title: 'تعذر تحديث الأسعار',
                text:
                    data.message ||
                    'فشل تحديث أسعار الصرف.',
                confirmButtonColor: '#00A6DA'
            });

            return;
        }

        Swal.fire({
            icon: 'success',
            title: 'تم تحديث أسعار الصرف',
            text:
                data.message ||
                'تم اعتماد أحدث أسعار صنعاء وعدن.',
            confirmButtonColor: '#00A6DA',
            timer: 2400,
            timerProgressBar: true
        });
    }
    catch (error) {
        console.error(
            'Exchange rate refresh error:',
            error
        );

        setExchangeMonitorStatus(
            'Failed',
            'تعذر آخر تحديث'
        );

        Swal.fire({
            icon: 'error',
            title: 'خطأ في الاتصال',
            text: 'تعذر الاتصال بالخادم أثناء تحديث أسعار الصرف.',
            confirmButtonColor: '#00A6DA'
        });
    }
    finally {
        if (refreshButton) {
            refreshButton.classList.remove(
                'is-loading'
            );

            refreshButton.innerHTML =
                originalButtonHtml;

            syncExchangeRateModeUi();
        }
    }
}


function applyExchangeRatePayload(payload) {
    if (!payload) return;

    setExchangeInputValue(
        'sanaaUsdRate',
        payload.sanaaUsdRate
    );

    setExchangeInputValue(
        'sanaaSarRate',
        payload.sanaaSarRate
    );

    setExchangeInputValue(
        'adenUsdRate',
        payload.adenUsdRate
    );

    setExchangeInputValue(
        'adenSarRate',
        payload.adenSarRate
    );

    const source =
        document.getElementById(
            'exchangeRateSourceLabel'
        );

    const lastUpdated =
        document.getElementById(
            'exchangeRateLastUpdated'
        );

    const lastAttempt =
        document.getElementById(
            'exchangeRateLastAttempt'
        );

    const errorBox =
        document.getElementById(
            'exchangeRateErrorBox'
        );

    const errorText =
        document.getElementById(
            'exchangeRateLastError'
        );

    if (source) {
        source.textContent =
            getExchangeSourceLabel(
                payload.source
            );
    }

    if (lastUpdated) {
        lastUpdated.textContent =
            formatExchangeDate(
                payload.lastUpdatedAt,
                'لا يوجد تحديث ناجح حتى الآن'
            );
    }

    if (lastAttempt) {
        lastAttempt.textContent =
            formatExchangeDate(
                payload.lastAttemptAt,
                'لا توجد محاولة تحديث حتى الآن'
            );
    }

    const lastError =
        String(
            payload.lastError || ''
        ).trim();

    if (errorText) {
        errorText.textContent =
            lastError;
    }

    if (errorBox) {
        errorBox.classList.toggle(
            'd-none',
            !lastError
        );
    }

    setExchangeMonitorStatus(
        payload.status || 'Never',
        payload.statusLabel
    );

    const autoToggle =
        document.getElementById(
            'autoUpdateExchangeRates'
        );

    if (
        autoToggle &&
        typeof payload.autoUpdate === 'boolean'
    ) {
        autoToggle.checked =
            payload.autoUpdate;
    }

    syncExchangeRateModeUi();
}


function setExchangeInputValue(
    id,
    rawValue
) {
    const control =
        document.getElementById(id);

    const value =
        Number.parseFloat(rawValue);

    if (
        !control ||
        !Number.isFinite(value)
    ) {
        return;
    }

    control.value =
        value.toFixed(2);
}


function setExchangeMonitorStatus(
    status,
    label
) {
    const normalized =
        String(status || 'Never');

    const monitor =
        document.getElementById(
            'exchangeRateMonitor'
        );

    const heroSignal =
        document.querySelector(
            '.settings-command-signal--exchange'
        );

    const statusLabel =
        document.getElementById(
            'exchangeRateStatusLabel'
        );

    const heroStatus =
        document.getElementById(
            'exchangeRateHeroStatus'
        );

    if (monitor) {
        monitor.dataset.status =
            normalized;
    }

    if (heroSignal) {
        heroSignal.dataset.exchangeSummaryStatus =
            normalized;
    }

    const finalLabel =
        label ||
        getExchangeStatusLabel(
            normalized
        );

    if (statusLabel) {
        statusLabel.textContent =
            finalLabel;
    }

    if (heroStatus) {
        heroStatus.textContent =
            finalLabel;
    }

    updateExchangeStatusIcons(
        normalized
    );
}


function updateExchangeStatusIcons(status) {
    const normalized =
        String(status || 'Never');

    let iconClass =
        'bi-clock-history';

    if (normalized === 'Success') {
        iconClass =
            'bi-check-circle-fill';
    }
    else if (normalized === 'Failed') {
        iconClass =
            'bi-exclamation-triangle-fill';
    }
    else if (normalized === 'Loading') {
        iconClass =
            'bi-arrow-repeat';
    }

    const monitorIcon =
        document.querySelector(
            '#exchangeRateMonitor .exchange-monitor__pulse i'
        );

    const heroIcon =
        document.querySelector(
            '.settings-command-signal--exchange .settings-command-signal__icon i'
        );

    [
        monitorIcon,
        heroIcon
    ].forEach(icon => {
        if (!icon) return;

        icon.className =
            `bi ${iconClass}`;
    });
}


function getExchangeStatusLabel(status) {
    switch (status) {
        case 'Success':
            return 'محدّث بنجاح';

        case 'Failed':
            return 'تعذر آخر تحديث';

        case 'Loading':
            return 'جاري الاتصال بالمصدر';

        default:
            return 'لم يتم التحديث بعد';
    }
}


function getExchangeSourceLabel(rawSource) {
    const source =
        String(rawSource || '').trim();

    switch (source) {
        case 'YemenSul':
            return 'يمن سول (YemenSul)';

        case 'Manual':
            return 'إدخال يدوي';

        case '':
            return 'لم يُحدَّد بعد';

        default:
            return source;
    }
}


function formatExchangeDate(
    rawValue,
    fallback
) {
    if (!rawValue) {
        return fallback;
    }

    let normalizedValue =
        String(rawValue).trim();

    /*
     * SQL datetime2 لا يحتفظ بمعلومة Kind.
     * أوقات أسعار الصرف عندنا تُحفظ بـ DateTime.UtcNow،
     * لذلك إذا وصل التاريخ بدون Z نعتبره UTC صراحةً.
     */
    if (
        normalizedValue &&
        !/[zZ]|[+-]\d{2}:?\d{2}$/.test(
            normalizedValue
        )
    ) {
        normalizedValue += 'Z';
    }

    const date =
        new Date(normalizedValue);

    if (
        Number.isNaN(
            date.getTime()
        )
    ) {
        return fallback;
    }

    const datePart =
        new Intl.DateTimeFormat(
            'ar-YE',
            {
                timeZone: 'Asia/Aden',
                year: 'numeric',
                month: '2-digit',
                day: '2-digit'
            }
        ).format(date);

    const timePart =
        new Intl.DateTimeFormat(
            'ar-YE',
            {
                timeZone: 'Asia/Aden',
                hour: '2-digit',
                minute: '2-digit',
                hour12: true
            }
        ).format(date);

    return `${datePart} - ${timePart}`;
}


function showExchangeModeHint(
    title,
    text
) {
    if (typeof Swal === 'undefined') {
        return;
    }

    Swal.fire({
        icon: 'info',
        title,
        text,
        confirmButtonColor: '#00A6DA',
        timer: 2600,
        timerProgressBar: true
    });
}


/* ==========================================================================
 * 💳 القسم الثالث: تبويبة المحافظ وشبكات الدفع
 * ========================================================================== */

function getDefaultWalletLogoPath() {
    return String(
        ADMIN_SETTINGS_ROOT?.dataset?.defaultWalletLogo ||
        '/images/wallets/default-wallet.png'
    ).trim() || '/images/wallets/default-wallet.png';
}

function initializeDefaultWalletLogoControls() {
    const input =
        document.getElementById('defaultWalletLogoUpload');

    const preview =
        document.getElementById('defaultWalletLogoPreview');

    const fileName =
        document.getElementById('defaultWalletLogoFileName');

    const saveButton =
        document.getElementById('saveDefaultWalletLogoButton');

    if (input) {
        input.addEventListener('change', function () {
            const file = this.files?.[0];

            if (!file) {
                updateSettingsFilePicker(this);
                if (preview) preview.src = getDefaultWalletLogoPath();
                return;
            }

            updateSettingsFilePicker(this);

            if (preview) {
                const objectUrl = URL.createObjectURL(file);
                preview.src = objectUrl;
                preview.onload = () => URL.revokeObjectURL(objectUrl);
            }
        });
    }

    if (saveButton) {
        saveButton.addEventListener(
            'click',
            updateDefaultWalletLogo
        );
    }
}

async function updateDefaultWalletLogo() {
    if (!ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks) {
        showPermissionDenied();
        return;
    }

    const input =
        document.getElementById('defaultWalletLogoUpload');

    const file = input?.files?.[0];

    if (!file) {
        Swal.fire({
            icon: 'info',
            title: 'اختر شعارًا أولاً',
            text: 'حدد صورة جديدة ثم اضغط حفظ الشعار الافتراضي.',
            confirmButtonColor: '#00A6DA'
        });
        return;
    }

    const maxImageSizeMb = Math.max(
        1,
        Number.parseInt(
            document.getElementById('maxImageSize')?.value || '5',
            10
        ) || 5
    );

    if (file.size > maxImageSizeMb * 1024 * 1024) {
        Swal.fire({
            icon: 'warning',
            title: 'حجم الشعار كبير',
            text: `الحد الحالي للصور هو ${maxImageSizeMb} ميجابايت.`,
            confirmButtonColor: '#00A6DA'
        });
        return;
    }

    const formData = new FormData();
    formData.append('DefaultWalletLogoUpload', file);

    const button =
        document.getElementById('saveDefaultWalletLogoButton');

    const originalHtml = button?.innerHTML || '';

    if (button) {
        button.disabled = true;
        button.innerHTML = '<i class="bi bi-arrow-repeat"></i> جاري الحفظ...';
    }

    try {
        const result =
            await settingsPostJson(
                '/Admin/Settings/UpdateDefaultWalletLogo',
                formData
            );

        if (!result.success) {
            throw new Error(result.message || 'تعذر حفظ الشعار الافتراضي.');
        }

        if (ADMIN_SETTINGS_ROOT && result.logoPath) {
            ADMIN_SETTINGS_ROOT.dataset.defaultWalletLogo = result.logoPath;
        }

        const preview =
            document.getElementById('defaultWalletLogoPreview');

        if (preview && result.logoPath) {
            preview.src = result.logoPath;
        }

        resetSettingsFilePicker(input);

        settingsWalletsLoaded = false;
        loadSupportedWalletsDynamicList(true);

        Swal.fire({
            icon: 'success',
            title: 'تم تحديث الشعار',
            text: result.message,
            confirmButtonColor: '#00A6DA',
            timer: 2200,
            timerProgressBar: true
        });
    }
    catch (error) {
        console.error('Default wallet logo error:', error);

        Swal.fire({
            icon: 'error',
            title: 'تعذر حفظ الشعار',
            text: error.message || 'حدث خطأ أثناء رفع الشعار الافتراضي.',
            confirmButtonColor: '#00A6DA'
        });
    }
    finally {
        if (button) {
            button.disabled = false;
            button.innerHTML = originalHtml;
        }
    }
}

// جلب قائمة الشبكات وعرضها
function loadSupportedWalletsDynamicList(force = false) {
    if (settingsWalletsLoaded && !force) return;

    if (settingsWalletsAbortController) {
        settingsWalletsAbortController.abort();
    }

    settingsWalletsAbortController =
        new AbortController();
    const tableBody = document.getElementById('supportedWalletsTableBody');
    if (!tableBody) return;

    fetch(
        '/Admin/Settings/GetSupportedWallets',
        {
            signal:
                settingsWalletsAbortController.signal,
            cache: 'no-store'
        }
    )
        .then(response => response.json())
        .then(data => {
            tableBody.innerHTML = '';
            if (data.length === 0) {
                tableBody.innerHTML = `<tr><td colspan="5" class="text-center py-4 text-muted">لا توجد شبكات دفع مضافة حالياً.</td></tr>`;
                return;
            }

            data.forEach((wallet, index) => {
                const logo =
                    escapeSettingsHtml(
                        normalizeSettingsMediaPath(
                            wallet.logoPath,
                            getDefaultWalletLogoPath()
                        )
                    );

                const walletName =
                    escapeSettingsHtml(wallet.walletName);

                const walletNameEncoded =
                    encodeURIComponent(
                        String(wallet.walletName ?? '')
                    );

                const checked = wallet.isActive ? 'checked' : '';

                const walletStatusControl = ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks
                    ? `<div class="form-check form-switch d-inline-block m-0">
                           <input class="form-check-input" type="checkbox" role="switch" ${checked} onchange="toggleWalletStateDynamically(${Number(wallet.walletId) || 0}, this.checked)" style="cursor:pointer;">
                       </div>`
                    : `<span class="badge ${wallet.isActive ? 'bg-success' : 'bg-secondary'}">${wallet.isActive ? 'نشطة' : 'متوقفة'}</span>`;

                const walletActions = ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks
                    ? `<button type="button" class="btn-delete-wallet" onclick="deletePlatformWalletDynamically(${Number(wallet.walletId) || 0}, decodeURIComponent('${walletNameEncoded}'))" title="حذف الشبكة">
                           <i class="bi bi-trash"></i>
                       </button>`
                    : `<span class="text-muted small">عرض فقط</span>`;

                const rowMarkup = `
                    <tr>
                        <td class="text-center text-muted fw-bold">${index + 1}</td>
                        <td>
                            <img src="${logo}" class="wallet-logo-img" onerror="this.onerror=null;this.src='${getDefaultWalletLogoPath()}'" />
                        </td>
                        <td class="fw-bold text-dark" style="font-size: 1.05rem;">${walletName}</td>
                        <td class="text-center">${walletStatusControl}</td>
                        <td class="text-center">${walletActions}</td>
                    </tr>
                `;
                tableBody.insertAdjacentHTML('beforeend', rowMarkup);
            });

            settingsWalletsLoaded = true;
        })
        .catch(error => {
            if (error.name === 'AbortError') return;
            console.error('Fetch Wallets Error:', error);
            tableBody.innerHTML = `<tr><td colspan="5" class="text-center py-4 text-danger">حدث خطأ أثناء جلب البيانات.</td></tr>`;
        });
}

// إضافة شبكة دفع جديدة
function createPlatformWallet(event) {
    if (!ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks) {
        showPermissionDenied();
        return;
    }
    event.preventDefault();
    const nameInput = document.getElementById('newWalletName');
    const logoInput = document.getElementById('newWalletLogo');

    if (!nameInput.value.trim()) return;

    Swal.fire({ title: 'جاري الإضافة...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

    const walletForm = new FormData();
    walletForm.append("WalletName", nameInput.value.trim());
    if (logoInput.files.length > 0) walletForm.append("LogoUpload", logoInput.files[0]);

    settingsPostJson('/Admin/Settings/AddSupportedWallet', walletForm)
        .then(result => {
            if (result.success) {
                Swal.fire({ icon: 'success', title: 'تمت الإضافة!', text: result.message, confirmButtonColor: '#00A6DA', timer: 2000 });
                document.getElementById('addSupportedWalletForm').reset();
                resetSettingsFilePicker(logoInput);
                loadSupportedWalletsDynamicList(true);
            } else {
                Swal.fire('خطأ', result.message, 'error');
            }
        })
        .catch(error => {
            showSettingsRequestError(
                'تعذر إضافة شبكة الدفع',
                error
            );
        });
}

// تفعيل/تعطيل الشبكة
function toggleWalletStateDynamically(walletId, isChecked) {
    if (!ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks) {
        showPermissionDenied();
        return;
    }
    const formData = new FormData();
    formData.append("id", walletId);
    formData.append("isActive", isChecked);

    settingsPostJson('/Admin/Settings/ToggleWalletStatus', formData)
        .then(result => {
            if (!result.success) {
                Swal.fire('خطأ', result.message, 'error');
                loadSupportedWalletsDynamicList(true);
            }
        })
        .catch(error => {
            showSettingsRequestError(
                'تعذر تحديث حالة الشبكة',
                error
            );
            loadSupportedWalletsDynamicList(true);
        });
}

// حذف الشبكة نهائياً
function deletePlatformWalletDynamically(walletId, walletName) {
    if (!ADMIN_SETTINGS_PERMISSIONS.managePaymentNetworks) {
        showPermissionDenied();
        return;
    }
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

            settingsPostJson('/Admin/Settings/DeleteSupportedWallet', deleteForm)
                .then(data => {
                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'تم الحذف!', text: data.message, confirmButtonColor: '#00A6DA', timer: 1500 });
                        loadSupportedWalletsDynamicList(true);
                    } else {
                        Swal.fire('خطأ', data.message, 'error');
                    }
                })
                .catch(error => {
                    showSettingsRequestError(
                        'تعذر حذف شبكة الدفع',
                        error
                    );
                });
        }
    });
}


/* ==========================================================================
 * 🎬 القسم الرابع: تبويبة الهوية والسلايدر (Hero Slider Studio VIP)
 * ========================================================================== */

// جلب قائمة السلايدرات وعرضها
function loadHeroSlidesList(force = false) {
    if (settingsSlidesLoaded && !force) return;

    if (settingsSlidesAbortController) {
        settingsSlidesAbortController.abort();
    }

    settingsSlidesAbortController =
        new AbortController();
    const tableBody = document.getElementById('heroSlidesTableBody');
    if (!tableBody) return;

    fetch(
        '/Admin/Settings/GetHeroSlides',
        {
            signal:
                settingsSlidesAbortController.signal,
            cache: 'no-store'
        }
    )
        .then(response => response.json())
        .then(data => {
            tableBody.innerHTML = '';
            if (data.length === 0) {
                tableBody.innerHTML = `<tr><td colspan="7" class="text-center py-5 text-muted">لا توجد شرائح عرض مضافة حالياً. كن أول من يضيف لمسة فنية!</td></tr>`;
                return;
            }

            data.forEach((slide) => {
                const img =
                    escapeSettingsHtml(
                        normalizeSettingsMediaPath(
                            slide.imagePath,
                            '/images/bg1.jpg'
                        )
                    );

                const slideId =
                    Number(slide.id) || 0;

                const typingText =
                    escapeSettingsHtml(slide.typingText);

                const typingTextEncoded =
                    encodeURIComponent(
                        String(slide.typingText ?? '')
                    );

                const checked = slide.isActive ? 'checked' : '';

                const isVideo = img.toLowerCase().endsWith('.mp4');
                const mediaTypeHtml = isVideo
                    ? `<span class="media-type-badge video"><i class="bi bi-camera-video-fill"></i> فيديو 4K</span>`
                    : `<span class="media-type-badge image"><i class="bi bi-image-fill"></i> صورة HD</span>`;

                const previewHtml = isVideo
                    ? `<video src="${img}" class="hero-slide-video" muted playsinline preload="metadata"></video>`
                    : `<img src="${img}" class="hero-slide-img" loading="lazy" decoding="async" onerror="this.src='/images/bg1.jpg'" />`;

                const mainColor =
                    normalizeSettingsCssColor(
                        slide.mainTextColor,
                        '#ffffff'
                    );

                const accentColor =
                    normalizeSettingsCssColor(
                        slide.accentColor,
                        '#ff5a5f'
                    );

                const rowMarkup = `
                    <tr>
                        <td>
                            ${ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides
                        ? `<div class="order-arrows-container">
                                       <button type="button" class="btn-sort-arrow mb-1" onclick="moveSlideOrder(${slideId}, 'up')" title="تحريك للأعلى">
                                           <i class="bi bi-caret-up-fill"></i>
                                       </button>
                                       <button type="button" class="btn-sort-arrow mt-1" onclick="moveSlideOrder(${slideId}, 'down')" title="تحريك للأسفل">
                                           <i class="bi bi-caret-down-fill"></i>
                                       </button>
                                   </div>`
                        : `<span class="text-muted small">—</span>`}
                        </td>
                        
                        <td>
                            ${previewHtml}
                        </td>

                        <td>
                            ${mediaTypeHtml}
                        </td>

                        <td class="text-center">
                            <span class="typing-text-badge">${typingText}</span>
                        </td>

                        <td>
                            <div class="color-harmony-container">
                                <div class="color-dot" style="background-color: ${mainColor}; border-color: #e9ecef;" title="لون العنوان الأساسي: ${mainColor}"></div>
                                <div class="color-dot" style="background-color: ${accentColor}; border-color: #e9ecef;" title="لون الكلمة التفاعلية: ${accentColor}"></div>
                            </div>
                        </td>

                        <td class="text-center">
                            ${ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides
                        ? `<div class="form-check form-switch d-inline-block m-0">
                                       <input class="form-check-input" type="checkbox" role="switch" ${checked} onchange="toggleHeroSlideStatus(${slideId}, this.checked)" style="cursor:pointer;">
                                   </div>`
                        : `<span class="badge ${slide.isActive ? 'bg-success' : 'bg-secondary'}">${slide.isActive ? 'نشط' : 'متوقف'}</span>`}
                        </td>

                        <td class="text-center">
                            ${ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides
                        ? `<div class="action-buttons-container">
                                       <button type="button" class="btn-edit-slide" onclick="editHeroSlideText(${slideId}, decodeURIComponent('${typingTextEncoded}'))" title="تعديل النص">
                                           <i class="bi bi-pencil-square"></i>
                                       </button>
                                       <button type="button" class="btn-delete-wallet" onclick="deleteHeroSlideDynamically(${slideId})" title="حذف السلايد">
                                           <i class="bi bi-trash"></i>
                                       </button>
                                   </div>`
                        : `<span class="text-muted small">عرض فقط</span>`}
                        </td>
                    </tr>
                `;
                tableBody.insertAdjacentHTML('beforeend', rowMarkup);
            });

            settingsSlidesLoaded = true;
        })
        .catch(error => {
            if (error.name === 'AbortError') return;
            console.error('Fetch Slides Error:', error);
            tableBody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-danger">حدث خطأ أثناء جلب السلايدر.</td></tr>`;
        });
}

// إضافة سلايد جديد مع فحص الحجم المسبق (Max 20MB)
function addHeroSlideDynamically(event) {
    if (!ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides) {
        showPermissionDenied();
        return;
    }
    event.preventDefault();

    const textInput = document.getElementById('newSlideText');
    const imageInput = document.getElementById('newSlideImage');
    const mainColorInput = document.getElementById('newSlideMainTextColor');
    const accentColorInput = document.getElementById('newSlideAccentColor');

    if (!textInput.value.trim()) return;

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
            resetSettingsFilePicker(imageInput);
            return; // إيقاف الدالة هنا وعدم إرسال الطلب للسيرفر
        }
    }

    Swal.fire({ title: 'جاري الرفع والمعالجة...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

    const slideForm = new FormData();
    slideForm.append("TypingText", textInput.value.trim());

    if (mainColorInput) slideForm.append("MainTextColor", mainColorInput.value);
    if (accentColorInput) slideForm.append("AccentColor", accentColorInput.value);
    if (imageInput.files.length > 0) slideForm.append("ImageUpload", imageInput.files[0]);

    settingsPostJson('/Admin/Settings/AddHeroSlide', slideForm)
        .then(result => {
            if (result.success) {
                Swal.fire({ icon: 'success', title: 'تمت الإضافة الفخمة!', text: result.message, confirmButtonColor: '#00A6DA', timer: 2000 });
                document.getElementById('addHeroSlideForm').reset();
                resetSettingsFilePicker(imageInput);
                loadHeroSlidesList(true);
            } else {
                Swal.fire('خطأ', result.message, 'error');
            }
        })
        .catch(error => {
            showSettingsRequestError(
                'تعذر إضافة السلايد',
                error
            );
        });
}

// تفعيل/تعطيل السلايد
function toggleHeroSlideStatus(slideId, isChecked) {
    if (!ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides) {
        showPermissionDenied();
        return;
    }
    const formData = new FormData();
    formData.append("id", slideId);
    formData.append("isActive", isChecked);

    settingsPostJson('/Admin/Settings/ToggleHeroSlideStatus', formData)
        .then(result => {
            if (!result.success) {
                Swal.fire('خطأ', result.message, 'error');
                loadHeroSlidesList(true);
            }
        })
        .catch(error => {
            showSettingsRequestError(
                'تعذر تحديث حالة السلايد',
                error
            );
            loadHeroSlidesList(true);
        });
}

// تحريك السلايد للأعلى أو الأسفل
function moveSlideOrder(slideId, direction) {
    if (!ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides) {
        showPermissionDenied();
        return;
    }
    const formData = new FormData();
    formData.append("id", slideId);
    formData.append("direction", direction);

    settingsPostJson('/Admin/Settings/MoveHeroSlideOrder', formData)
        .then(result => {
            if (result.success) {
                loadHeroSlidesList(true);
            } else {
                console.log(result.message);
            }
        })
        .catch(error => {
            showSettingsRequestError(
                'تعذر تعديل ترتيب السلايد',
                error
            );
        });
}

// النافذة الذكية لتعديل النص التفاعلي مباشرة
function editHeroSlideText(slideId, currentText) {
    if (!ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides) {
        showPermissionDenied();
        return;
    }
    Swal.fire({
        title: 'تعديل النص التفاعلي',
        input: 'text',
        inputValue: currentText,
        showCancelButton: true,
        confirmButtonText: 'حفظ التعديل',
        cancelButtonText: 'إلغاء',
        confirmButtonColor: '#00A6DA',
        inputValidator: (value) => {
            if (!value || value.trim() === '') {
                return 'يجب كتابة نص جديد!';
            }
        }
    }).then((result) => {
        if (result.isConfirmed) {
            const formData = new FormData();
            formData.append("id", slideId);
            formData.append("newText", result.value);

            settingsPostJson('/Admin/Settings/EditHeroSlideText', formData)
                .then(res => {
                    if (res.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'تم التحديث بنجاح',
                            timer: 1200,
                            showConfirmButton: false
                        });
                        loadHeroSlidesList(true);
                    } else {
                        Swal.fire('خطأ', res.message, 'error');
                    }
                })
                .catch(error => {
                    showSettingsRequestError(
                        'تعذر تعديل السلايد',
                        error
                    );
                });
        }
    });
}

// حذف السلايد نهائياً
function deleteHeroSlideDynamically(slideId) {
    if (!ADMIN_SETTINGS_PERMISSIONS.manageHeroSlides) {
        showPermissionDenied();
        return;
    }
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

            settingsPostJson('/Admin/Settings/DeleteHeroSlide', deleteForm)
                .then(data => {
                    if (data.success) {
                        Swal.fire({ icon: 'success', title: 'تم الحذف!', text: data.message, confirmButtonColor: '#00A6DA', timer: 1500 });
                        loadHeroSlidesList(true);
                    } else {
                        Swal.fire('خطأ', data.message, 'error');
                    }
                })
                .catch(error => {
                    showSettingsRequestError(
                        'تعذر حذف السلايد',
                        error
                    );
                });
        }
    });
}
