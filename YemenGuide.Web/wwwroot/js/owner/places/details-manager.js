/**
 * =========================================================================
 * YemenGuide - إدارة المنشآت: الخدمات والباقات والغرف
 * يحتوي الغرف، الخدمات، المرافق، الباقات، الصور التابعة، والسياسات.
 * تم فصله من manage-places.js دون تغيير الوظائف أو المسارات أو البيانات.
 * =========================================================================
 */

document.addEventListener("DOMContentLoaded", function () {


    // =========================================================================
    // 📁 القسم السابع: تبويبة الخدمات والباقات والغرف (Details Manager)
    // =========================================================================
    window.selectIconGrid = function (iconVal, el) {
        const hiddenInput = document.getElementById('edit-icon');
        if (hiddenInput) hiddenInput.value = iconVal;

        document.querySelectorAll('.icon-grid-item').forEach(i => {
            i.classList.remove('is-selected');
            i.classList.remove('bg-primary', 'text-white');
            i.classList.add('bg-white', 'text-dark');
            i.setAttribute('aria-pressed', 'false');
        });

        el.classList.add('is-selected');
        el.classList.remove('bg-white', 'text-dark');
        el.classList.add('bg-primary', 'text-white');
        el.setAttribute('aria-pressed', 'true');

        const preview = document.querySelector('[data-selected-amenity-icon]');
        if (preview) preview.innerHTML = `<i class="bi ${iconVal}"></i>`;
    };

    window.filterAmenityIcons = function (value) {
        const query = String(value || '').trim().toLowerCase();
        let visibleCount = 0;

        document.querySelectorAll('.icon-grid-item').forEach(item => {
            const searchable = `${item.dataset.icon || ''} ${item.dataset.keywords || ''}`.toLowerCase();
            const visible = !query || searchable.includes(query);
            item.hidden = !visible;
            if (visible) visibleCount++;
        });

        const empty = document.querySelector('[data-icon-search-empty]');
        if (empty) empty.hidden = visibleCount > 0;
    };

    function getIconGridHTML(selectedIcon) {
        const icons = [
            'bi-patch-check-fill', 'bi-water', 'bi-wifi', 'bi-snow', 'bi-fan',
            'bi-car-front', 'bi-tv', 'bi-film', 'bi-cup-hot', 'bi-shop',
            'bi-droplet', 'bi-moon-stars', 'bi-door-closed', 'bi-tree', 'bi-bicycle',
            'bi-cup', 'bi-shield-check', 'bi-elevator', 'bi-lightning', 'bi-controller',
            'bi-fire', 'bi-speaker', 'bi-people', 'bi-thermometer-sun', 'bi-thermometer-snow',
            'bi-cloud-haze2', 'bi-projector', 'bi-camera-video', 'bi-boombox', 'bi-display',
            'bi-earbuds', 'bi-egg-fried', 'bi-cake', 'bi-cup-straw', 'bi-basket',
            'bi-cart', 'bi-refrigerator', 'bi-lamp', 'bi-archive', 'bi-safe',
            'bi-flower1', 'bi-flower2', 'bi-cloud-sun', 'bi-stars', 'bi-signpost',
            'bi-globe', 'bi-binoculars', 'bi-building', 'bi-house', 'bi-bank',
            'bi-hospital', 'bi-p-circle', 'bi-fuel-pump', 'bi-ev-station', 'bi-scooter',
            'bi-balloon', 'bi-music-note-beamed', 'bi-mic', 'bi-calendar-event', 'bi-gift',
            'bi-trash', 'bi-recycle', 'bi-telephone', 'bi-bell', 'bi-clock',
            'bi-briefcase', 'bi-heart', 'bi-magic', 'bi-award', 'bi-gem',
            'bi-palette', 'bi-scissors', 'bi-credit-card', 'bi-printer', 'bi-tools',
            'bi-bandaid', 'bi-camera', 'bi-chat-square-text', 'bi-compass', 'bi-dice-5',
            'bi-flag', 'bi-geo-alt', 'bi-headset', 'bi-info-circle', 'bi-key',
            'bi-laptop', 'bi-life-preserver', 'bi-map', 'bi-megaphone', 'bi-mortarboard',
            'bi-pass', 'bi-peace', 'bi-plug', 'bi-puzzle', 'bi-receipt',
            'bi-suit-heart', 'bi-tags', 'bi-ticket', 'bi-trophy', 'bi-umbrella',
            'bi-wrench', 'bi-check-circle', 'bi-box-seam', 'bi-bag', 'bi-wallet2'
        ];

        const iconKeywords = {
            'bi-water': 'مسبح ماء سباحة نافورة',
            'bi-wifi': 'انترنت واي فاي شبكة راوتر',
            'bi-snow': 'تكييف بارد ثلج',
            'bi-fan': 'مروحة تهوية',
            'bi-car-front': 'سيارة موقف مواقف كراج',
            'bi-tv': 'تلفزيون شاشة',
            'bi-film': 'سينما أفلام',
            'bi-cup-hot': 'قهوة شاي مشروبات',
            'bi-droplet': 'ماء دش حمام',
            'bi-moon-stars': 'نوم ليل سرير',
            'bi-tree': 'حديقة أشجار جلسة خارجية',
            'bi-bicycle': 'دراجة رياضة',
            'bi-shield-check': 'أمان حراسة حماية',
            'bi-elevator': 'مصعد',
            'bi-lightning': 'كهرباء مولد طاقة',
            'bi-controller': 'ألعاب ترفيه',
            'bi-fire': 'شواية موقد تدفئة',
            'bi-speaker': 'سماعات صوت موسيقى',
            'bi-people': 'عائلات مجلس اجتماعات',
            'bi-projector': 'بروجكتر عرض شاشة',
            'bi-camera-video': 'كاميرا مراقبة تصوير',
            'bi-refrigerator': 'ثلاجة مطبخ',
            'bi-safe': 'خزنة أمان',
            'bi-flower1': 'حديقة ورد زهور',
            'bi-building': 'مبنى فندق',
            'bi-house': 'منزل سكن',
            'bi-geo-alt': 'موقع خريطة',
            'bi-headset': 'خدمة عملاء اتصال',
            'bi-key': 'مفتاح دخول خاص',
            'bi-umbrella': 'مظلة شمسية',
            'bi-check-circle': 'متوفر ميزة مرفق'
        };

        const currentIcon = selectedIcon || 'bi-patch-check-fill';
        let html = `
            <input type="hidden" id="edit-icon" value="${currentIcon}">
            <div class="details-icon-picker">
                <div class="details-icon-picker__toolbar">
                    <span class="details-icon-picker__preview" data-selected-amenity-icon>
                        <i class="bi ${currentIcon}"></i>
                    </span>
                    <label class="details-icon-picker__search">
                        <i class="bi bi-search"></i>
                        <input type="search" placeholder="ابحثي عن أيقونة..."
                               oninput="window.filterAmenityIcons(this.value)">
                    </label>
                </div>
                <div class="details-icon-picker__grid custom-scrollbar">`;

        icons.forEach(icon => {
            const isSelected = icon === currentIcon;
            const activeClass = isSelected ? 'bg-primary text-white' : 'bg-white text-dark';
            html += `
                <button type="button"
                        class="icon-grid-item ${activeClass} ${isSelected ? 'is-selected' : ''}"
                        data-icon="${icon}"
                        data-keywords="مرفق راحة خدمة ${iconKeywords[icon] || ''} ${icon.replace(/bi-/g, '').replace(/-/g, ' ')}"
                        onclick="window.selectIconGrid('${icon}', this)"
                        id="icon-item-${icon}"
                        title="${icon}"
                        aria-label="اختيار ${icon}"
                        aria-pressed="${isSelected}">
                    <i class="bi ${icon}"></i>
                </button>`;
        });
        html += `
                </div>
                <div class="details-icon-picker__empty" data-icon-search-empty hidden>
                    <i class="bi bi-search"></i>
                    لا توجد أيقونة مطابقة، جرّبي كلمة أخرى.
                </div>
            </div>`;
        return html;
    }

    function buildServiceEditorHtml(service, existingImagesHtml) {
        const item = service || {};
        const isEdit = Boolean(service);
        const name = item.serviceName ?? item.ServiceName ?? '';
        const price = item.price ?? item.Price ?? '';
        const description = item.description ?? item.Description ?? '';

        return `
            <div class="details-editor-shell details-service-editor">
                <div class="details-editor-intro details-editor-intro--service">
                    <span><i class="bi bi-stars"></i></span>
                    <div>
                        <strong>${isEdit ? 'تحديث الخدمة' : 'خدمة إضافية جديدة'}</strong>
                        <small>اكتبي اسمًا واضحًا وسعرًا ووصفًا مختصرًا يساعد العميل على الاختيار.</small>
                    </div>
                </div>

                <div class="details-service-editor__grid">
                <div class="details-editor-field">
                    <label for="edit-name">اسم الخدمة <span>*</span></label>
                    <div class="details-editor-control">
                        <i class="bi bi-stars"></i>
                        <input id="edit-name" name="ServiceName" class="form-control"
                               maxlength="100" value="${escapePackageHtml(name)}"
                               placeholder="مثال: وجبة إفطار عائلية" required>
                    </div>
                </div>

                <div class="details-editor-field">
                    <label for="edit-price">سعر الخدمة <span>*</span></label>
                    <div class="details-service-price-field">
                        <input id="edit-price" name="Price" type="number" min="0" step="0.01"
                               class="form-control" value="${escapePackageHtml(price)}" placeholder="0" required>
                        <span>ريال</span>
                    </div>
                </div>
                </div>

                <div class="details-editor-field">
                    <label for="edit-desc">وصف الخدمة</label>
                    <textarea id="edit-desc" name="Description" class="form-control"
                              rows="2" maxlength="500" placeholder="وضّحي ما تتضمنه الخدمة ومتى يحصل عليها العميل...">${escapePackageHtml(description)}</textarea>
                    <small class="details-editor-field__hint"><i class="bi bi-info-circle"></i> الوصف الجيد يقلل استفسارات العملاء.</small>
                </div>

                ${existingImagesHtml || ''}

                <div class="details-editor-media-section">
                    <div class="details-editor-media-section__title">
                        <i class="bi bi-images"></i>
                        <div><strong>${isEdit ? 'إضافة صور جديدة' : 'صور الخدمة'}</strong><small>يمكن اختيار عدة صور بحد إجمالي 5 ميجابايت.</small></div>
                    </div>
                    ${getArabicUploadFieldHTML({
            id: 'edit-image',
            name: 'ImageFiles',
            title: isEdit ? 'إضافة صور جديدة للخدمة' : 'صور الخدمة',
            description: isEdit ? 'اتركيه فارغًا للإبقاء على الصور الحالية.' : 'اختاري صورًا واضحة تعبّر عن الخدمة.',
            buttonText: isEdit ? 'اختيار صور جديدة' : 'اختيار الصور'
        })}
                </div>
            </div>`;
    }

    function buildAmenityEditorHtml(name, selectedIcon, isEdit) {
        return `
            <div class="details-editor-shell details-amenity-editor">
                <div class="details-editor-intro details-editor-intro--amenity">
                    <span><i class="bi bi-gem"></i></span>
                    <div>
                        <strong>${isEdit ? 'تحديث المرفق' : 'إضافة مرفق جديد'}</strong>
                        <small>اكتبي اسم المرفق وسنقترح أيقونته تلقائيًا، ويمكنك اختيار أيقونة أخرى.</small>
                    </div>
                </div>

                <div class="details-editor-field">
                    <label for="edit-name">اسم المرفق <span>*</span></label>
                    <div class="details-editor-control">
                        <i class="bi bi-pencil-square"></i>
                        <input id="edit-name" class="form-control" maxlength="100"
                               value="${escapePackageHtml(name || '')}"
                               placeholder="مثال: مسبح، واي فاي، موقف سيارات"
                               oninput="window.autoSuggestIcon(this.value)">
                    </div>
                </div>

                <div class="details-editor-field">
                    <label>أيقونة المرفق <span>*</span></label>
                    ${getIconGridHTML(selectedIcon || '')}
                </div>
            </div>`;
    }


    const detailsUploadObjectUrls = new WeakMap();

    function getArabicUploadFieldHTML(options) {
        const config = options || {};
        const inputId = config.id || 'details-images';
        const inputName = config.name ? ` name="${config.name}"` : '';
        const title = config.title || 'صور العنصر';
        const description = config.description || 'يمكن اختيار أكثر من صورة، بحد إجمالي 5 ميجابايت.';
        const buttonText = config.buttonText || 'اختيار الصور';

        return `
            <div class="details-upload-field">
                <input type="file"
                       id="${inputId}"
                       ${inputName}
                       class="details-upload-input"
                       accept="image/*"
                       multiple
                       onchange="window.handleDetailsImageSelection(this, '${title.replace(/'/g, "\\'")}')">

                <label class="details-upload-card" for="${inputId}">
                    <span class="details-upload-card__icon">
                        <i class="bi bi-cloud-arrow-up"></i>
                    </span>
                    <span class="details-upload-card__copy">
                        <strong>${title}</strong>
                        <small>${description}</small>
                    </span>
                    <span class="details-upload-card__button">
                        <i class="bi bi-images"></i>
                        ${buttonText}
                    </span>
                </label>

                <div class="details-upload-summary"
                     id="${inputId}-summary">
                    <i class="bi bi-info-circle"></i>
                    لم يتم اختيار صور بعد
                </div>

                <div class="details-upload-preview"
                     id="${inputId}-preview"></div>
            </div>`;
    }

    window.handleDetailsImageSelection = function (input, label) {
        if (!input) return;

        if (typeof window.checkImageSize === 'function') {
            window.checkImageSize(input);
        }

        const summary = document.getElementById(`${input.id}-summary`);
        const preview = document.getElementById(`${input.id}-preview`);
        const previousUrls = detailsUploadObjectUrls.get(input) || [];

        previousUrls.forEach(url => URL.revokeObjectURL(url));
        detailsUploadObjectUrls.set(input, []);

        if (preview) preview.innerHTML = '';

        const files = Array.from(input.files || []);
        if (!files.length) {
            if (summary) {
                summary.className = 'details-upload-summary';
                summary.innerHTML = '<i class="bi bi-info-circle"></i> لم يتم اختيار صور بعد';
            }
            return;
        }

        const totalBytes = files.reduce((sum, file) => sum + file.size, 0);
        const totalMb = totalBytes / (1024 * 1024);
        const isTooLarge = totalBytes > 5 * 1024 * 1024;

        input.classList.toggle('is-invalid', isTooLarge);

        if (summary) {
            summary.className = `details-upload-summary ${isTooLarge ? 'is-error' : 'is-ready'}`;
            summary.innerHTML = `
                <i class="bi ${isTooLarge ? 'bi-exclamation-circle' : 'bi-check-circle'}"></i>
                ${isTooLarge
                    ? `إجمالي الصور ${totalMb.toFixed(2)} ميجابايت — يتجاوز الحد المسموح`
                    : `تم اختيار ${files.length} صورة — الحجم الإجمالي ${totalMb.toFixed(2)} ميجابايت`}`;
        }

        if (preview) {
            const createdUrls = [];

            files.slice(0, 8).forEach(file => {
                if (!file.type || !file.type.startsWith('image/')) return;

                const objectUrl = URL.createObjectURL(file);
                createdUrls.push(objectUrl);

                const item = document.createElement('span');
                item.className = 'details-upload-preview__item';

                const image = document.createElement('img');
                image.src = objectUrl;
                image.alt = label || 'معاينة الصورة';

                item.appendChild(image);
                preview.appendChild(item);
            });

            detailsUploadObjectUrls.set(input, createdUrls);
        }
    };

    // =========================================================================
    // الباقات الديناميكية: أوقات مخصصة + حالة التفعيل + عرض آمن
    // =========================================================================
    function escapePackageHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function normalizePackageTime(value) {
        if (value === null || value === undefined || value === '') return '';
        const match = String(value).match(/(\d{1,2}):(\d{2})/);
        if (!match) return '';
        return `${String(Number(match[1])).padStart(2, '0')}:${match[2]}`;
    }

    function getLegacyPackageTimes(timeFrame) {
        const value = String(timeFrame || '');
        if (value.includes('صباحي')) return { start: '09:00', end: value.includes('12 ظهراً') ? '12:00' : '13:00' };
        if (value.includes('مسائي') || value.includes('مقيل')) return { start: '14:00', end: '22:00' };
        if (value.includes('ليلي') || value.includes('سمرة')) return { start: '18:00', end: '06:00' };
        if (value.includes('يوم كامل')) return { start: '09:00', end: '00:00' };

        const match = value.match(/(\d{1,2}:\d{2}).*?(\d{1,2}:\d{2})/);
        return match
            ? { start: normalizePackageTime(match[1]), end: normalizePackageTime(match[2]) }
            : { start: '', end: '' };
    }

    function getPackageTimes(pkg) {
        const start = normalizePackageTime(pkg?.startTime ?? pkg?.StartTime);
        const end = normalizePackageTime(pkg?.endTime ?? pkg?.EndTime);
        if (start && end) return { start, end };
        return getLegacyPackageTimes(pkg?.timeFrame ?? pkg?.TimeFrame);
    }

    function formatPackageTime(value) {
        const normalized = normalizePackageTime(value);
        if (!normalized) return '';
        const [hours, minutes] = normalized.split(':').map(Number);
        const hour12 = hours % 12 || 12;
        const suffix = hours < 12 ? 'ص' : 'م';
        return `${hour12}:${String(minutes).padStart(2, '0')} ${suffix}`;
    }

    function getPackagePeriodInfo(pkg) {
        const times = getPackageTimes(pkg);
        if (!times.start || !times.end) {
            return {
                label: pkg?.timeFrame ?? pkg?.TimeFrame ?? 'وقت غير محدد',
                overnight: false,
                ...times
            };
        }

        return {
            label: `${formatPackageTime(times.start)} — ${formatPackageTime(times.end)}`,
            overnight: times.end <= times.start,
            ...times
        };
    }

    const packagePresetCatalog = [
        { key: 'morning', label: 'صباحي', icon: 'bi-sunrise', start: '09:00', end: '13:00' },
        { key: 'evening', label: 'مسائي', icon: 'bi-sunset', start: '14:00', end: '22:00' },
        { key: 'night', label: 'ليلي', icon: 'bi-moon-stars', start: '18:00', end: '06:00' },
        { key: 'full-day', label: 'يوم كامل', icon: 'bi-calendar2-check', start: '09:00', end: '00:00' }
    ];

    function resolvePackagePreset(pkg, period) {
        const frame = String(pkg?.timeFrame ?? pkg?.TimeFrame ?? '').trim();
        const isNamedCustom = frame && frame !== 'غير محدد' && !/^من\s+\d/.test(frame) &&
            !packagePresetCatalog.some(item => frame.includes(item.label));
        if (isNamedCustom) return 'custom';

        const exact = packagePresetCatalog.find(item => item.start === period.start && item.end === period.end);
        if (exact) return exact.key;

        const byName = packagePresetCatalog.find(item => frame.includes(item.label));
        return byName?.key || 'custom';
    }

    function buildPackageEditorHtml(pkg) {
        const item = pkg || {};
        const period = getPackagePeriodInfo(item);
        const isNew = !pkg;
        if (isNew && !period.start && !period.end) {
            period.start = '09:00';
            period.end = '13:00';
        }
        const selectedPreset = isNew ? 'morning' : resolvePackagePreset(item, period);
        const rawFrame = String(item.timeFrame ?? item.TimeFrame ?? '').trim();
        const customPeriodName = selectedPreset === 'custom'
            ? (rawFrame && !/^من\s+\d/.test(rawFrame) && rawFrame !== 'غير محدد' ? rawFrame : 'فترة مخصصة')
            : '';
        const isActive = (item.isActive ?? item.IsActive ?? true) !== false;
        const name = item.packageName ?? item.PackageName ?? '';
        const price = item.price ?? item.Price ?? '';
        const capacity = item.capacity ?? item.Capacity ?? '';

        return `
            <div class="details-package-editor" data-package-editor>
                <div class="details-package-editor__intro">
                    <span><i class="bi bi-stars"></i></span>
                    <div>
                        <strong>بيانات الباقة</strong>
                        <small>يمكنك تحديد أي وقت وسعر وسعة، وليس فقط الفترات الجاهزة.</small>
                    </div>
                </div>

                <div class="details-package-field details-package-field--full">
                    <label for="package-name">اسم الباقة <span>*</span></label>
                    <input id="package-name" class="form-control" maxlength="100"
                           value="${escapePackageHtml(name)}" placeholder="مثال: باقة عائلية خاصة">
                </div>

                <div class="details-package-editor__grid">
                    <div class="details-package-field">
                        <label for="package-price">السعر <span>*</span></label>
                        <div class="details-package-number-field">
                            <input id="package-price" type="number" min="0.01" step="0.01"
                                   class="form-control" value="${escapePackageHtml(price)}" placeholder="0">
                            <span>ريال</span>
                        </div>
                    </div>

                    <div class="details-package-field">
                        <label for="package-capacity">السعة <span>*</span></label>
                        <div class="details-package-number-field">
                            <input id="package-capacity" type="number" min="1" step="1"
                                   class="form-control" value="${escapePackageHtml(capacity)}" placeholder="1">
                            <span>شخص</span>
                        </div>
                    </div>
                </div>

                <div class="details-package-period-section">
                    <div class="details-package-period-section__title">
                        <span><i class="bi bi-clock-history"></i></span>
                        <div><strong>فترة الباقة</strong><small>اختاري فترة جاهزة أو أنشئي فترة أخرى باسم ووقت مخصصين.</small></div>
                    </div>
                    <div class="details-package-presets" aria-label="فترة الباقة">
                        ${packagePresetCatalog.map(preset => `
                            <button type="button" data-package-preset="${preset.key}"
                                    data-start="${preset.start}" data-end="${preset.end}"
                                    class="${selectedPreset === preset.key ? 'is-active' : ''}">
                                <i class="bi ${preset.icon}"></i><span>${preset.label}</span>
                                <small>${formatPackageTime(preset.start)} — ${formatPackageTime(preset.end)}</small>
                            </button>`).join('')}
                        <button type="button" data-package-preset="custom"
                                class="${selectedPreset === 'custom' ? 'is-active' : ''}">
                            <i class="bi bi-sliders"></i><span>أخرى</span><small>وقت واسم مخصصان</small>
                        </button>
                    </div>
                    <div class="details-package-custom-period ${selectedPreset === 'custom' ? 'is-visible' : ''}" data-package-custom-period>
                        <label for="package-period-name">اسم الفترة المخصصة <span>*</span></label>
                        <input id="package-period-name" class="form-control" maxlength="100"
                               value="${escapePackageHtml(customPeriodName)}" placeholder="مثال: فترة ما بعد الظهر">
                    </div>
                </div>

                <div class="details-package-editor__grid details-package-editor__grid--time">
                    <div class="details-package-field">
                        <label for="package-start">وقت البداية <span>*</span></label>
                        <input id="package-start" type="time" class="form-control" value="${period.start}">
                    </div>
                    <div class="details-package-field">
                        <label for="package-end">وقت النهاية <span>*</span></label>
                        <input id="package-end" type="time" class="form-control" value="${period.end}">
                    </div>
                </div>

                <div class="details-package-overnight" data-package-overnight hidden>
                    <i class="bi bi-moon-stars"></i>
                    وقت النهاية في اليوم التالي.
                </div>

                <label class="details-package-active-row">
                    <span>
                        <strong>الباقة متاحة للحجز</strong>
                        <small>يمكن إيقافها مؤقتًا دون حذف بياناتها.</small>
                    </span>
                    <input id="package-active" type="checkbox" ${isActive ? 'checked' : ''}>
                    <i aria-hidden="true"></i>
                </label>
            </div>`;
    }

    function initPackageEditor() {
        const root = Swal.getPopup()?.querySelector('[data-package-editor]');
        if (!root || root.dataset.ready === 'true') return;
        root.dataset.ready = 'true';

        const startInput = root.querySelector('#package-start');
        const endInput = root.querySelector('#package-end');
        const overnight = root.querySelector('[data-package-overnight]');
        const customPeriod = root.querySelector('[data-package-custom-period]');

        const setPreset = key => {
            root.dataset.periodKey = key;
            root.querySelectorAll('[data-package-preset]').forEach(button => {
                button.classList.toggle('is-active', button.dataset.packagePreset === key);
            });
            customPeriod?.classList.toggle('is-visible', key === 'custom');
            if (key === 'custom') {
                setTimeout(() => root.querySelector('#package-period-name')?.focus(), 0);
            }
        };

        const updateOvernight = () => {
            overnight.hidden = !(startInput.value && endInput.value && endInput.value <= startInput.value);
        };

        root.querySelectorAll('[data-package-preset]').forEach(button => {
            button.addEventListener('click', () => {
                const key = button.dataset.packagePreset;
                setPreset(key);
                if (key !== 'custom') {
                    startInput.value = button.dataset.start;
                    endInput.value = button.dataset.end;
                }
                updateOvernight();
            });
        });

        startInput.addEventListener('input', updateOvernight);
        endInput.addEventListener('input', updateOvernight);
        setPreset(root.querySelector('[data-package-preset].is-active')?.dataset.packagePreset || 'morning');
        updateOvernight();
    }

    function collectPackageEditor(placeId, packageId) {
        const root = Swal.getPopup()?.querySelector('[data-package-editor]');
        if (!root) return { error: 'تعذر قراءة نموذج الباقة.' };

        const name = root.querySelector('#package-name').value.trim();
        const price = Number(root.querySelector('#package-price').value);
        const capacity = Number(root.querySelector('#package-capacity').value);
        const start = root.querySelector('#package-start').value;
        const end = root.querySelector('#package-end').value;
        const isActive = root.querySelector('#package-active').checked;
        const periodKey = root.dataset.periodKey || 'morning';
        const preset = packagePresetCatalog.find(item => item.key === periodKey);
        const customPeriodName = root.querySelector('#package-period-name')?.value.trim() || '';

        if (!name) return { error: 'أدخلي اسم الباقة.' };
        if (name.length > 100) return { error: 'اسم الباقة يجب ألا يتجاوز 100 حرف.' };
        if (!Number.isFinite(price) || price <= 0) return { error: 'أدخلي سعرًا أكبر من صفر.' };
        if (!Number.isInteger(capacity) || capacity <= 0) return { error: 'أدخلي سعة صحيحة لا تقل عن شخص واحد.' };
        if (!start || !end) return { error: 'حددي وقت البداية والنهاية.' };
        if (start === end) return { error: 'وقت البداية والنهاية يجب أن يكونا مختلفين.' };
        if (periodKey === 'custom' && !customPeriodName) return { error: 'أدخلي اسم الفترة المخصصة.' };

        const normalizeName = value => String(value || '').trim().toLowerCase().replace(/\s+/g, ' ');
        const duplicate = window.currentPlaceData?.packages?.some(item => {
            const id = item.packageID ?? item.packageId ?? item.PackageID;
            const itemName = item.packageName ?? item.PackageName;
            return Number(id) !== Number(packageId || 0) && normalizeName(itemName) === normalizeName(name);
        });

        if (duplicate) return { error: 'توجد باقة أخرى بالاسم نفسه.' };

        return {
            data: {
                PlaceId: Number(placeId),
                PackageId: Number(packageId || 0),
                PackageName: name,
                Price: price,
                Capacity: capacity,
                StartTime: `${start}:00`,
                EndTime: `${end}:00`,
                TimeFrame: periodKey === 'custom' ? customPeriodName : (preset?.label || 'فترة مخصصة'),
                IsActive: isActive
            }
        };
    }

    function initDetailsManager() {
        const root = document.getElementById('detailsManagerRoot');
        const noPlaceMsg = document.getElementById('noPlaceSelectedMsg');
        const container = document.getElementById('placeDetailsContainer');
        const loadingState = document.getElementById('detailsLoadingState');
        const packagesSection = document.getElementById('packagesSection');
        const roomsSection = document.getElementById('roomsSection');

        if (!root) return;

        const activePlaceId = parseInt(root.dataset.placeId || '0', 10);
        const activeSystemId = parseInt(root.dataset.systemId || '0', 10);

        window.activeDetailsPlaceId = activePlaceId;
        window.activeDetailsSystemId = activeSystemId;
        window.activeDetailsPlaceName = root.dataset.placeName || '';
        window.activeDetailsCategoryName = root.dataset.categoryName || '';

        window.refreshDetailsView = function (placeId) {
            const resolvedPlaceId = parseInt(placeId || window.activeDetailsPlaceId || '0', 10);

            if (!resolvedPlaceId) {
                if (noPlaceMsg) noPlaceMsg.style.display = 'grid';
                if (container) container.style.display = 'none';
                if (loadingState) loadingState.style.display = 'none';
                return;
            }

            const systemId = window.activeDetailsSystemId || 0;

            if (loadingState) loadingState.style.display = 'grid';
            if (container) container.style.display = 'none';
            if (noPlaceMsg) noPlaceMsg.style.display = 'none';

            if (packagesSection) {
                packagesSection.style.display =
                    (systemId === 2 || systemId === 3 || systemId === 4)
                        ? 'block'
                        : 'none';
            }

            if (roomsSection) {
                roomsSection.style.display = systemId === 1 ? 'block' : 'none';
            }

            const policySection = document.getElementById('mandatoryPolicySection');
            if (policySection) {
                policySection.style.display =
                    (systemId >= 1 && systemId <= 4)
                        ? 'block'
                        : 'none';
            }

            fetch(`/Owner/Place/GetPlaceSubItems?placeId=${resolvedPlaceId}`)
                .then(res => res.json())
                .then(data => {
                    if (!data.success) {
                        throw new Error(data.message || 'تعذر جلب بيانات المنشأة.');
                    }

                    window.currentPlaceData = data;
                    renderLists(data.packages, data.services, data.amenities, data.rooms);

                    if (loadingState) loadingState.style.display = 'none';
                    if (container) container.style.display = 'block';

                    const alertMsg = document.getElementById('policyAlertMsg');
                    if (alertMsg && systemId >= 1 && systemId <= 4) {
                        if (data.hasPolicies) {
                            alertMsg.innerHTML = `
                                    <div class="details-policy-card">
                                        <div class="details-policy-card__copy">
                                            <span class="details-policy-card__icon">
                                                <i class="bi bi-shield-check"></i>
                                            </span>
                                            <div>
                                                <h5>سياسات المنشأة مكتملة</h5>
                                                <p>يمكن استقبال الحجوزات وفق السياسات المحفوظة، ويمكن مراجعتها في أي وقت.</p>
                                            </div>
                                        </div>
                                        <div class="details-policy-card__actions">
                                            <button type="button"
                                                    class="details-add-button details-add-button--green"
                                                    onclick="window.openEditPolicyModal('edit')">
                                                <i class="bi bi-pencil-square"></i>
                                                تعديل السياسات
                                            </button>
                                        </div>
                                    </div>`;
                        } else {
                            alertMsg.innerHTML = `
                                    <div class="details-policy-card details-policy-card--warning">
                                        <div class="details-policy-card__copy">
                                            <span class="details-policy-card__icon">
                                                <i class="bi bi-exclamation-triangle"></i>
                                            </span>
                                            <div>
                                                <h5>بقي إعداد سياسات الحجز والإلغاء</h5>
                                                <p>أكمل السياسات حتى تصبح المنشأة جاهزة للإرسال والمراجعة.</p>
                                            </div>
                                        </div>
                                        <div class="details-policy-card__actions">
                                            <button type="button"
                                                    class="details-add-button details-add-button--green"
                                                    onclick="window.applySmartDefaultPolicies(${resolvedPlaceId})">
                                                <i class="bi bi-magic"></i>
                                                تطبيق الافتراضية
                                            </button>
                                            <button type="button"
                                                    class="details-primary-button"
                                                    onclick="window.openEditPolicyModal('new')">
                                                <i class="bi bi-sliders"></i>
                                                إعداد يدوي
                                            </button>
                                        </div>
                                    </div>`;
                        }
                    }

                    // بعد إنشاء منشأة جديدة: وجّه المالك بصرياً إلى بطاقة اختيار السياسات فقط.
                    // لا نفتح أي مودال ولا نطبّق أي خيار تلقائياً.
                    const requestedPolicyFocusId =
                        Number.parseInt(localStorage.getItem('focusNewPlacePolicyId') || '0', 10);

                    if (
                        requestedPolicyFocusId === resolvedPlaceId &&
                        data.hasPolicies === false
                    ) {
                        const policyCard =
                            document.querySelector(
                                '#policyAlertMsg .details-policy-card--warning'
                            );

                        if (policyCard) {
                            localStorage.removeItem('focusNewPlacePolicyId');

                            window.setTimeout(() => {
                                policyCard.classList.add('details-policy-card--new-focus');

                                policyCard.scrollIntoView({
                                    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches
                                        ? 'auto'
                                        : 'smooth',
                                    block: 'center'
                                });

                                const firstChoice =
                                    policyCard.querySelector(
                                        '.details-policy-card__actions button'
                                    );

                                window.setTimeout(() => {
                                    firstChoice?.focus({ preventScroll: true });
                                }, 520);

                                window.setTimeout(() => {
                                    policyCard.classList.remove('details-policy-card--new-focus');
                                }, 4200);
                            }, 180);
                        }
                    }

                    if (typeof window.filterRooms === 'function') {
                        window.filterRooms();
                    }
                })
                .catch(error => {
                    console.error('Details Manager Fetch Error:', error);
                    if (loadingState) loadingState.style.display = 'none';
                    if (container) container.style.display = 'none';
                    if (noPlaceMsg) {
                        noPlaceMsg.classList.remove('d-none');
                        noPlaceMsg.style.display = 'grid';
                        noPlaceMsg.innerHTML = `
                                <span class="details-empty-state__icon">
                                    <i class="bi bi-exclamation-triangle"></i>
                                </span>
                                <h4>تعذر تحميل بيانات المنشأة</h4>
                                <p>${error.message || 'حدث خطأ غير متوقع أثناء تحميل البيانات.'}</p>`;
                    }
                });
        };

        const DEFAULT_VISIBLE_ROOMS = 3;
        let roomsExpanded = false;

        function updateLoadMoreButton(buttonWrap, options) {
            if (!buttonWrap) return;

            const settings = options || {};
            const totalCount = Number(settings.totalCount || 0);
            const initialVisible = Number(settings.initialVisible || 3);
            const expanded = settings.expanded === true;
            const noun = settings.noun || 'عناصر';
            const hasExtraItems = totalCount > initialVisible;
            const button = buttonWrap.querySelector('.details-load-more-button');

            buttonWrap.hidden = !hasExtraItems;
            buttonWrap.style.display = hasExtraItems ? 'flex' : 'none';

            if (!button || !hasExtraItems) return;

            button.dataset.expanded = expanded ? 'true' : 'false';
            button.classList.toggle('is-expanded', expanded);
            button.setAttribute('aria-expanded', expanded ? 'true' : 'false');

            const title = button.querySelector('[data-toggle-title]');
            const meta = button.querySelector('[data-toggle-meta]');
            const icon = button.querySelector('i');
            const remainingCount = Math.max(totalCount - initialVisible, 0);

            if (title) {
                title.textContent = expanded ? 'إخفاء العناصر الزائدة' : 'عرض المزيد';
            }

            if (meta) {
                meta.textContent = expanded
                    ? `العودة إلى أول ${initialVisible.toLocaleString('ar-YE')} ${noun}`
                    : `${remainingCount.toLocaleString('ar-YE')} ${noun} متبقية`;
            }

            if (icon) {
                icon.className = expanded ? 'bi bi-chevron-up' : 'bi bi-chevron-down';
            }
        }

        window.filterRooms = function (resetExpansion = true) {
            const filterElement = document.getElementById('roomTypeFilter');
            const selectedType = filterElement ? filterElement.value : 'all';
            const roomItems = Array.from(document.querySelectorAll('.room-item-card'));

            if (resetExpansion) {
                roomsExpanded = false;
            }

            const matchingItems = roomItems.filter(item =>
                selectedType === 'all' || item.getAttribute('data-room-type') === selectedType
            );

            roomItems.forEach(item => {
                item.classList.add('d-none');
                item.classList.remove('room-item-visible');
            });

            const visibleItems = roomsExpanded
                ? matchingItems
                : matchingItems.slice(0, DEFAULT_VISIBLE_ROOMS);

            visibleItems.forEach(item => {
                item.classList.remove('d-none');
                item.classList.add('room-item-visible');
            });

            updateLoadMoreButton(document.getElementById('loadMoreRoomsBtn'), {
                totalCount: matchingItems.length,
                initialVisible: DEFAULT_VISIBLE_ROOMS,
                expanded: roomsExpanded,
                noun: 'عناصر'
            });
        };

        window.toggleMoreRooms = function () {
            roomsExpanded = !roomsExpanded;
            window.filterRooms(false);

            if (!roomsExpanded) {
                const roomsSection = document.getElementById('roomsSection');
                if (roomsSection) {
                    roomsSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            }
        };

        function renderLists(packages, services, amenities, rooms) {
            const setCounter = function (id, value) {
                const element = document.getElementById(id);
                if (element) element.textContent = Number(value || 0).toLocaleString('ar-YE');
            };

            setCounter('roomsCountBadge', (rooms || []).reduce((sum, room) => sum + Number(room.quantity ?? room.Quantity ?? 0), 0));
            setCounter('packagesCountBadge', packages?.length || 0);
            setCounter('servicesCountBadge', services?.length || 0);
            setCounter('amenitiesCountBadge', amenities?.length || 0);

            const roomList = document.getElementById('roomsList');
            if (roomList) {
                const roomFilter = document.getElementById('roomTypeFilter');
                if (roomFilter && rooms) {
                    const existingValues = new Set(Array.from(roomFilter.options).map(option => option.value));
                    rooms.forEach(room => {
                        const type = room.roomType || room.RoomType;
                        if (type && !existingValues.has(type)) {
                            roomFilter.add(new Option(type, type));
                            existingValues.add(type);
                        }
                    });
                }
                if (!rooms || rooms.length === 0) {
                    roomList.innerHTML = `<div class="details-empty-card"><div><i class="bi bi-door-open"></i><strong>لا توجد أنواع غرف أو أجنحة</strong><small class="d-block mt-1">أضف نوعًا أولًا، وبعدها نظّم الغرف الفعلية من زر إدارة الطوابق والغرف.</small></div></div>`;
                } else {
                    let html = '';
                    rooms.forEach((r, index) => {
                        const rId = r.roomId || r.RoomId || r.roomID || Object.values(r)[0];
                        const type = r.roomType || r.RoomType || 'غرفة مفردة';
                        const displayName = r.roomNumber || r.RoomNumber || '';
                        const hasCustomDisplayName =
                            String(displayName).trim() &&
                            String(displayName).trim().toLocaleLowerCase('ar') !== String(type).trim().toLocaleLowerCase('ar');
                        const price = r.price !== undefined ? r.price.toLocaleString() : '0';
                        const qty = r.quantity || r.Quantity || 0;
                        const units = r.units || r.Units || [];

                        // اعرض رقم/أرقام الطوابق التي يوجد فيها هذا النوع فعليًا،
                        // بدل عرض عدد الطوابق فقط. مثال: غرفة في الطابق 2 => "الطابق 2" وليس "1 طابق".
                        const floorNumbers = [...new Set(
                            units
                                .map(unit => Number(unit.floorNumber ?? unit.FloorNumber ?? 0))
                                .filter(number => Number.isInteger(number))
                        )].sort((a, b) => a - b);

                        const floorsText = floorNumbers.length === 0
                            ? 'بدون طابق'
                            : floorNumbers.length === 1
                                ? (floorNumbers[0] === 0 ? 'الدور الأرضي' : `الطابق ${floorNumbers[0]}`)
                                : `الطوابق ${floorNumbers.map(number => number === 0 ? 'الأرضي' : number).join('، ')}`;

                        const area = r.areaSquareMeters ?? r.AreaSquareMeters;
                        const adults = r.maxAdults ?? r.MaxAdults ?? 1;
                        const children = r.maxChildren ?? r.MaxChildren ?? 0;
                        const beds = r.bedsCount ?? r.BedsCount;
                        const internalRooms = Number(r.internalRoomsCount ?? r.InternalRoomsCount ?? 0);
                        const isSuite = internalRooms > 0 || type.includes('جناح');

                        let hiddenClass = index >= 3 ? 'd-none room-item-hidden' : '';

                        html += `
            <div class="col-md-4 room-item-card ${hiddenClass}" data-room-type="${type}">
                <div class="p-3 border rounded-3 bg-light position-relative">
                    <div class="d-flex justify-content-between align-items-start">
                        <div>
                            <span class="badge bg-dark bg-opacity-10 text-dark mb-2">نوع الوحدة</span>
                            <h6 class="fw-bold text-primary mb-1">${type}</h6>
                            ${hasCustomDisplayName ? `<div class="details-room-card-display-name"><i class="bi bi-tag"></i><span>${displayName}</span></div>` : ''}
                            <p class="text-success fw-black fs-6 mb-2">${price} ر.ي</p>
                            <div class="details-room-card-meta">
                                <small><i class="bi ${isSuite ? 'bi-gem' : 'bi-door-open-fill'}"></i> ${qty} ${isSuite ? 'جناح فعلي' : 'غرفة فعلية'}</small>
                                <small><i class="bi bi-layers"></i> ${floorsText}</small>
                                <small><i class="bi bi-people"></i> ${adults} بالغ / ${children} طفل</small>
                                ${area ? `<small><i class="bi bi-aspect-ratio"></i> ${area} م²</small>` : ''}
                                ${isSuite ? `<small><i class="bi bi-grid-3x3-gap"></i> ${internalRooms} غرف داخلية</small>` : (beds !== null && beds !== undefined ? `<small><i class="bi bi-moon-stars"></i> ${beds} سرير</small>` : '')}
                            </div>
                        </div>
                        <div class="d-flex flex-column gap-2">
                            <button class="btn btn-sm btn-outline-primary p-1 shadow-none" onclick="window.openEditRoomTypeModal(${rId})" title="تعديل بيانات النوع"><i class="bi bi-pencil-square"></i></button>
                            <button class="btn btn-sm btn-outline-danger p-1 shadow-none" onclick="deleteItem('room', ${rId}, ${window.activeDetailsPlaceId})" title="حذف النوع"><i class="bi bi-trash3-fill"></i></button>
                        </div>
                    </div>
                </div>
            </div>`;
                    });
                    if (rooms.length > 3) html += `<div class="col-12 details-load-more-wrap" id="loadMoreRoomsBtn"><button type="button" class="details-load-more-button" onclick="window.toggleMoreRooms()" aria-expanded="false"><span data-toggle-title>عرض المزيد</span><small data-toggle-meta>${Math.max(rooms.length - 3, 0).toLocaleString('ar-YE')} عناصر متبقية</small><i class="bi bi-chevron-down"></i></button></div>`;
                    roomList.innerHTML = html;
                }
            }

            const pkgList = document.getElementById('packagesList');
            if (pkgList) {
                if (!packages || packages.length === 0) {
                    pkgList.innerHTML = `<div class="details-empty-card"><div><i class="bi bi-tags"></i><strong>لا توجد باقات مضافة</strong><small class="d-block mt-1">أنشئ باقة توضح الوقت والسعر والسعة.</small></div></div>`;
                } else {
                    let html = '';
                    packages.forEach((pkg, index) => {
                        const pId = pkg.packageID || pkg.packageId || pkg.PackageID || Object.values(pkg)[0];
                        const name = pkg.packageName || pkg.PackageName || 'بدون اسم';
                        const priceValue = Number(pkg.price ?? pkg.Price ?? 0);
                        const price = priceValue.toLocaleString('ar-YE', { maximumFractionDigits: 2 });
                        const capacity = Number(pkg.capacity ?? pkg.Capacity ?? 0);
                        const isActive = (pkg.isActive ?? pkg.IsActive ?? true) !== false;
                        const period = getPackagePeriodInfo(pkg);

                        let hiddenClass = index >= 3 ? 'd-none package-item-hidden' : '';
                        html += `
            <div class="col-md-4 package-item ${hiddenClass}">
                <article class="details-package-card ${isActive ? '' : 'is-inactive'}">
                    <div class="details-package-card__top">
                        <span class="details-package-card__icon"><i class="bi bi-tag-fill"></i></span>
                        <span class="details-package-card__state ${isActive ? 'is-active' : 'is-paused'}">
                            <i class="bi ${isActive ? 'bi-check-circle-fill' : 'bi-pause-circle-fill'}"></i>
                            ${isActive ? 'مفعلة' : 'متوقفة'}
                        </span>
                    </div>

                    <div class="details-package-card__content">
                        <h6>${escapePackageHtml(name)}</h6>
                        <div class="details-package-card__price">
                            <strong>${price}</strong><span>ريال</span>
                        </div>
                        <div class="details-package-card__meta">
                            <span><i class="bi bi-clock-history"></i>${escapePackageHtml(period.label)}</span>
                            <span><i class="bi bi-people-fill"></i>${capacity.toLocaleString('ar-YE')} شخص</span>
                            ${period.overnight ? '<span class="is-overnight"><i class="bi bi-moon-stars"></i>ينتهي في اليوم التالي</span>' : ''}
                        </div>
                    </div>

                    <footer class="details-package-card__footer">
                        <button type="button" class="details-package-status-toggle ${isActive ? 'is-on' : ''}"
                                onclick="window.togglePackageStatus(${pId}, event)"
                                aria-label="${isActive ? 'إيقاف الباقة' : 'تفعيل الباقة'}"
                                aria-pressed="${isActive}">
                            <i></i><span>${isActive ? 'إيقاف مؤقت' : 'تفعيل الباقة'}</span>
                        </button>
                        <div class="details-package-card__actions">
                            <button type="button" onclick="openEditPackageModal(${pId})" title="تعديل الباقة">
                                <i class="bi bi-pencil-square"></i>
                            </button>
                            <button type="button" class="is-danger" onclick="deleteItem('package', ${pId}, ${window.activeDetailsPlaceId})" title="حذف الباقة">
                                <i class="bi bi-trash3-fill"></i>
                            </button>
                        </div>
                    </footer>
                </article>
            </div>`;
                    });
                    if (packages.length > 3) html += `<div class="col-12 details-load-more-wrap" id="loadMorePackagesBtn"><button type="button" class="details-load-more-button" onclick="window.toggleMoreItems('.package-item', 'loadMorePackagesBtn', 3, 'عناصر')" aria-expanded="false"><span data-toggle-title>عرض المزيد</span><small data-toggle-meta>${Math.max(packages.length - 3, 0).toLocaleString('ar-YE')} عناصر متبقية</small><i class="bi bi-chevron-down"></i></button></div>`;
                    pkgList.innerHTML = html;
                }
            }

            const srvList = document.getElementById('servicesList');
            if (srvList) {
                if (!services || services.length === 0) {
                    srvList.innerHTML = `<div class="details-empty-card"><div><i class="bi bi-stars"></i><strong>لا توجد خدمات إضافية</strong><small class="d-block mt-1">أضف خدمة اختيارية يمكن للعميل طلبها مع الحجز.</small></div></div>`;
                    srvList.className = "row g-3 w-100 m-0 p-0";
                } else {
                    srvList.className = "row g-3 w-100 m-0 p-0";
                    let html = '';
                    services.forEach((s, index) => {
                        const sId = s.serviceID || s.serviceId || s.ServiceID || Object.values(s)[0];
                        const sName = s.serviceName || s.ServiceName || 'بدون اسم';
                        const sPrice = s.price !== undefined ? s.price.toLocaleString() : '0';
                        const sDesc = s.description || s.Description || 'لا يوجد وصف مضاف.';

                        let images = [];
                        const rawImg = s.imageUrl || s.ImageUrl;
                        if (rawImg) {
                            images = rawImg.split(',').filter(i => i.trim() !== '');
                        }
                        const firstImg = images.length > 0 ? images[0] : '';
                        const mediaHtml = firstImg
                            ? `<img src="${escapePackageHtml(firstImg)}" alt="${escapePackageHtml(sName)}" loading="lazy">`
                            : `<span class="details-service-card__placeholder"><i class="bi bi-stars"></i></span>`;

                        html += `
                        <div class="col-md-6 service-item">
                            <article class="details-service-card">
                                <div class="details-service-card__media">
                                    ${mediaHtml}
                                    <span class="details-service-card__type"><i class="bi bi-plus-circle"></i> خدمة إضافية</span>
                                    ${images.length > 1 ? `<span class="details-service-card__images"><i class="bi bi-images"></i>${images.length.toLocaleString('ar-YE')}</span>` : ''}
                                </div>
                                <div class="details-service-card__body" dir="rtl">
                                    <div class="details-service-card__heading">
                                        <h6>${escapePackageHtml(sName)}</h6>
                                        <div class="details-service-card__price"><strong>${sPrice}</strong><span>ريال</span></div>
                                    </div>
                                    <p>${escapePackageHtml(sDesc)}</p>
                                    <footer>
                                        <span><i class="bi bi-check-circle-fill"></i> متاحة مع الحجز</span>
                                        <div class="details-card-actions">
                                            <button type="button" onclick="window.openEditServiceModal(${sId})" title="تعديل الخدمة"><i class="bi bi-pencil-square"></i></button>
                                            <button type="button" class="is-danger" onclick="window.deleteItem('service', ${sId}, ${window.activeDetailsPlaceId})" title="حذف الخدمة"><i class="bi bi-trash3-fill"></i></button>
                                        </div>
                                    </footer>
                                </div>
                            </article>
                        </div>`;
                    });
                    srvList.innerHTML = html;
                }
            }

            const amnList = document.getElementById('amenitiesList');
            if (amnList) {
                if (!amenities || amenities.length === 0) amnList.innerHTML = `<li class="details-empty-card"><div><i class="bi bi-gem"></i><strong>لا توجد مرافق مضافة</strong><small class="d-block mt-1">أضف وسائل الراحة المتوفرة داخل المنشأة.</small></div></li>`;
                else {
                    let html = '';
                    amenities.forEach((a, index) => {
                        const aId = a.amenityID || a.amenityId || a.AmenityID || a.id || Object.values(a)[0];
                        const aName = a.amenityName || a.AmenityName;
                        const aIcon = a.iconClass || a.IconClass || 'bi-patch-check-fill';
                        html += `
                            <li class="amenity-item">
                                <article class="details-amenity-card">
                                    <span class="details-amenity-card__icon"><i class="bi ${escapePackageHtml(aIcon)}"></i></span>
                                    <div class="details-amenity-card__copy">
                                        <strong>${escapePackageHtml(aName)}</strong>
                                        <small><i class="bi bi-check-circle-fill"></i> متوفر في المنشأة</small>
                                    </div>
                                    <div class="details-card-actions">
                                        <button type="button" onclick="openEditAmenityModal(${aId})" title="تعديل المرفق"><i class="bi bi-pencil-square"></i></button>
                                        <button type="button" class="is-danger" onclick="deleteItem('amenity', ${aId}, ${window.activeDetailsPlaceId})" title="حذف المرفق"><i class="bi bi-trash3-fill"></i></button>
                                    </div>
                                </article>
                            </li>`;
                    });
                    amnList.innerHTML = html;
                }
            }
        }

        window.toggleMoreItems = function (itemSelector, btnId, initialVisible, noun) {
            const items = Array.from(document.querySelectorAll(itemSelector));
            const buttonWrap = document.getElementById(btnId);
            const button = buttonWrap ? buttonWrap.querySelector('.details-load-more-button') : null;
            const expanded = button ? button.dataset.expanded === 'true' : false;
            const nextExpanded = !expanded;
            const visibleLimit = Number(initialVisible || 3);

            items.forEach((item, index) => {
                const shouldShow = nextExpanded || index < visibleLimit;
                item.classList.toggle('d-none', !shouldShow);
            });

            updateLoadMoreButton(buttonWrap, {
                totalCount: items.length,
                initialVisible: visibleLimit,
                expanded: nextExpanded,
                noun: noun || 'عناصر'
            });

            if (!nextExpanded && buttonWrap && buttonWrap.parentElement) {
                buttonWrap.parentElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        };

        // اسم قديم للتوافق مع أي استدعاء خارجي سابق.
        window.showMoreItems = function (itemSelector, btnId, initialVisible, noun) {
            window.toggleMoreItems(itemSelector, btnId, initialVisible, noun);
        };

        localStorage.removeItem('autoSelectPlaceId');

        if (window.activeDetailsPlaceId) {
            window.refreshDetailsView(window.activeDetailsPlaceId);
        } else {
            if (noPlaceMsg) noPlaceMsg.style.display = 'grid';
            if (container) container.style.display = 'none';
            if (loadingState) loadingState.style.display = 'none';
        }

        const hotelTypeCatalog = [
            'غرفة مفردة', 'غرفة مزدوجة', 'غرفة توأم', 'غرفة ثلاثية', 'استوديو',
            'جناح عائلي', 'جناح ملكي', 'جناح تنفيذي', 'شاليه خاص',
            'قاعة مؤتمرات/مناسبات', 'طيرمانة/مجلس'
        ];

        const detailsEscape = value => String(value ?? '')
            .replace(/&/g, '&amp;').replace(/</g, '&lt;')
            .replace(/>/g, '&gt;').replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');

        function hotelTypeOptions(selected) {
            const known = hotelTypeCatalog.includes(selected);
            return hotelTypeCatalog.map(type => `<option value="${detailsEscape(type)}" ${type === selected ? 'selected' : ''}>${detailsEscape(type)}</option>`).join('')
                + `<option value="أخرى" ${!known && selected ? 'selected' : ''}>أخرى</option>`;
        }

        function roomUnitRowHtml(prefix, unit) {
            const data = unit || {};
            return `<div class="details-room-unit-row" data-room-unit>
                <input type="hidden" data-unit-field="RoomUnitID" value="${data.roomUnitID || data.RoomUnitID || 0}">
                <input class="form-control" data-unit-field="RoomNumber" maxlength="50" value="${detailsEscape(data.roomNumber || data.RoomNumber || '')}" placeholder="رقم الغرفة">
                <input type="number" min="-100" max="1000" class="form-control" data-unit-field="FloorNumber" value="${data.floorNumber ?? data.FloorNumber ?? 0}" placeholder="رقم الطابق">
                <input class="form-control" data-unit-field="FloorLabel" maxlength="50" value="${detailsEscape(data.floorLabel || data.FloorLabel || '')}" placeholder="اسم الطابق">
                <label class="details-room-unit-available"><input type="checkbox" data-unit-field="IsAvailable" ${(data.isAvailable ?? data.IsAvailable ?? true) ? 'checked' : ''}> متاحة</label>
                <input class="form-control" data-unit-field="Notes" maxlength="250" value="${detailsEscape(data.notes || data.Notes || '')}" placeholder="ملاحظات">
                <button type="button" class="details-room-unit-remove" title="حذف"><i class="bi bi-x-lg"></i></button>
            </div>`;
        }

        function buildHotelRoomEditorHtml(prefix, room) {
            const data = room || {};
            const type = data.roomType || data.RoomType || 'غرفة مفردة';
            const units = data.units || data.Units || [];
            return `
                <div class="details-hotel-room-editor" data-room-editor="${prefix}">
                    <div class="row g-2">
                        <div class="col-md-6"><label>نوع الوحدة <span>*</span></label><select id="${prefix}-type" class="form-select">${hotelTypeOptions(type)}</select></div>
                        <div class="col-md-6"><label>نوع مخصص</label><input id="${prefix}-custom-type" class="form-control" maxlength="50" value="${hotelTypeCatalog.includes(type) ? '' : detailsEscape(type)}" placeholder="يظهر عند اختيار أخرى"></div>
                        <div class="col-md-6"><label>اسم عرض النوع <span>*</span></label><input id="${prefix}-name" class="form-control" maxlength="50" value="${detailsEscape(data.roomNumber || data.RoomNumber || '')}" placeholder="مثال: غرفة مفردة ديلوكس"></div>
                        <div class="col-md-3"><label>السعر <span>*</span></label><input id="${prefix}-price" type="number" min="0.01" step="0.01" class="form-control" value="${data.price ?? data.Price ?? ''}"></div>
                        <div class="col-md-3"><label>المساحة م²</label><input id="${prefix}-area" type="number" min="0.01" step="0.01" class="form-control" value="${data.areaSquareMeters ?? data.AreaSquareMeters ?? ''}"></div>
                        <div class="col-md-3"><label>البالغون <span>*</span></label><input id="${prefix}-adults" type="number" min="1" max="100" class="form-control" value="${data.maxAdults ?? data.MaxAdults ?? 1}"></div>
                        <div class="col-md-3"><label>الأطفال</label><input id="${prefix}-children" type="number" min="0" max="100" class="form-control" value="${data.maxChildren ?? data.MaxChildren ?? 0}"></div>
                        <div class="col-md-3"><label>عدد الأسرّة</label><input id="${prefix}-beds" type="number" min="0" max="50" class="form-control" value="${data.bedsCount ?? data.BedsCount ?? ''}"></div>
                        <div class="col-md-3"><label>نوع السرير</label><input id="${prefix}-bed-type" class="form-control" maxlength="50" value="${detailsEscape(data.bedType || data.BedType || '')}"></div>
                        <div class="col-12"><label>الوصف</label><textarea id="${prefix}-description" class="form-control" maxlength="500" rows="2">${detailsEscape(data.description || data.Description || '')}</textarea></div>
                    </div>

                    <div class="details-room-range">
                        <strong><i class="bi bi-magic"></i> إنشاء نطاق غرف لهذا النوع</strong>
                        <div class="details-room-range__grid">
                            <input id="${prefix}-range-start" class="form-control" inputmode="numeric" placeholder="من 1 أو 101">
                            <input id="${prefix}-range-end" class="form-control" inputmode="numeric" placeholder="إلى 20 أو 110">
                            <input id="${prefix}-range-prefix" class="form-control" maxlength="12" placeholder="بادئة B">
                            <input id="${prefix}-range-floor" type="number" min="-100" max="1000" class="form-control" value="0" placeholder="رقم الطابق">
                            <input id="${prefix}-range-label" class="form-control" maxlength="50" placeholder="اسم الطابق">
                            <button type="button" class="details-add-button" data-generate-room-range><i class="bi bi-plus-circle"></i> إنشاء</button>
                        </div>
                    </div>

                    <div class="details-room-units-toolbar">
                        <div><strong>الغرف الفعلية</strong><small>يمكن تعديل الرقم والطابق لكل غرفة بعد الإنشاء.</small></div>
                        <button type="button" class="details-add-button" data-add-room-unit><i class="bi bi-plus-lg"></i> غرفة يدوية</button>
                    </div>
                    <div class="details-room-units-list" id="${prefix}-units">${units.map(unit => roomUnitRowHtml(prefix, unit)).join('')}</div>
                    ${getArabicUploadFieldHTML({
                id: `${prefix}-images`, name: 'RoomImages', title: room ? 'إضافة صور جديدة للنوع' : 'صور النوع',
                description: 'الحد الإجمالي للصور 5 ميجابايت.', buttonText: 'اختيار الصور'
            })}
                </div>`;
        }

        function initHotelRoomEditor(prefix) {
            const root = document.querySelector(`[data-room-editor="${prefix}"]`);
            if (!root || root.dataset.ready === 'true') return;
            root.dataset.ready = 'true';
            const list = root.querySelector(`#${prefix}-units`);
            const typeSelect = root.querySelector(`#${prefix}-type`);
            const customType = root.querySelector(`#${prefix}-custom-type`);
            const toggleCustom = () => {
                const isOther = typeSelect.value === 'أخرى';
                customType.disabled = !isOther;
                customType.closest('.col-md-6').classList.toggle('details-field-muted', !isOther);
            };
            typeSelect.addEventListener('change', toggleCustom);
            toggleCustom();

            const bindRemove = row => row.querySelector('.details-room-unit-remove')?.addEventListener('click', () => row.remove());
            list.querySelectorAll('[data-room-unit]').forEach(bindRemove);
            root.querySelector('[data-add-room-unit]').addEventListener('click', () => {
                list.insertAdjacentHTML('beforeend', roomUnitRowHtml(prefix, {}));
                bindRemove(list.lastElementChild);
            });
            root.querySelector('[data-generate-room-range]').addEventListener('click', () => {
                const startRaw = root.querySelector(`#${prefix}-range-start`).value.trim();
                const endRaw = root.querySelector(`#${prefix}-range-end`).value.trim();
                const start = Number(startRaw), end = Number(endRaw);
                if (!Number.isInteger(start) || !Number.isInteger(end) || start > end || end - start > 500) {
                    Swal.showValidationMessage('أدخل نطاقًا صحيحًا بحد أقصى 501 غرفة.');
                    return;
                }
                const pad = Math.max(startRaw.length, endRaw.length);
                const prefixText = root.querySelector(`#${prefix}-range-prefix`).value.trim();
                const floor = Number(root.querySelector(`#${prefix}-range-floor`).value || 0);
                const label = root.querySelector(`#${prefix}-range-label`).value.trim();
                for (let n = start; n <= end; n++) {
                    list.insertAdjacentHTML('beforeend', roomUnitRowHtml(prefix, {
                        roomNumber: prefixText + String(n).padStart(pad, '0'), floorNumber: floor, floorLabel: label, isAvailable: true
                    }));
                    bindRemove(list.lastElementChild);
                }
            });
        }

        function collectHotelRoomForm(prefix, placeId, roomId) {
            const root = document.querySelector(`[data-room-editor="${prefix}"]`);
            const selectedType = root.querySelector(`#${prefix}-type`).value;
            const roomType = selectedType === 'أخرى' ? root.querySelector(`#${prefix}-custom-type`).value.trim() : selectedType;
            const name = root.querySelector(`#${prefix}-name`).value.trim();
            const price = Number(root.querySelector(`#${prefix}-price`).value);
            const adults = Number(root.querySelector(`#${prefix}-adults`).value);
            const rows = Array.from(root.querySelectorAll('[data-room-unit]'));
            if (!roomType || !name || price <= 0 || adults < 1 || !rows.length)
                return { error: 'أكمل النوع واسم العرض والسعر والبالغين وأضف غرفة فعلية واحدة على الأقل.' };

            const numbers = new Set();
            for (const row of rows) {
                const number = row.querySelector('[data-unit-field="RoomNumber"]').value.trim();
                const normalized = number.toLocaleLowerCase('ar');
                if (!number) return { error: 'رقم كل غرفة فعلية مطلوب.' };
                if (numbers.has(normalized)) return { error: `رقم الغرفة (${number}) مكرر.` };
                numbers.add(normalized);
            }

            const data = new FormData();
            data.append('PlaceID', placeId);
            if (roomId) data.append('RoomID', roomId);
            data.append('RoomType', roomType);
            data.append('RoomNumber', name);
            data.append('Price', price);
            data.append('AreaSquareMeters', root.querySelector(`#${prefix}-area`).value);
            data.append('MaxAdults', adults);
            data.append('MaxChildren', root.querySelector(`#${prefix}-children`).value || 0);
            data.append('BedsCount', root.querySelector(`#${prefix}-beds`).value);
            data.append('BedType', root.querySelector(`#${prefix}-bed-type`).value.trim());
            data.append('Description', root.querySelector(`#${prefix}-description`).value.trim());
            data.append('IsAvailable', 'true');
            data.append('Quantity', rows.length);
            rows.forEach((row, index) => {
                const get = field => row.querySelector(`[data-unit-field="${field}"]`);
                data.append(`Units[${index}].RoomUnitID`, get('RoomUnitID').value || 0);
                data.append(`Units[${index}].PlaceID`, placeId);
                data.append(`Units[${index}].RoomID`, roomId || 0);
                data.append(`Units[${index}].RoomNumber`, get('RoomNumber').value.trim());
                data.append(`Units[${index}].FloorNumber`, get('FloorNumber').value || 0);
                data.append(`Units[${index}].FloorLabel`, get('FloorLabel').value.trim());
                data.append(`Units[${index}].IsAvailable`, get('IsAvailable').checked ? 'true' : 'false');
                data.append(`Units[${index}].Notes`, get('Notes').value.trim());
            });
            const files = root.querySelector(`#${prefix}-images`)?.files || [];
            for (const file of files) data.append('RoomImages', file);
            return { data };
        }

        // =====================================================================
        // المحرر الموحد الجديد: غرف/أجنحة + طوابق قابلة للطي
        // تُرك المحرر السابق أعلاه دون حذف لتجنب كسر أي استدعاء قديم خارج الصفحة.
        // =====================================================================
        const premiumHotelRoomTypes = ['غرفة مفردة', 'غرفة مزدوجة', 'غرفة توأم', 'غرفة ثلاثية', 'استوديو'];
        const premiumHotelSuiteTypes = ['جناح عائلي', 'جناح ملكي', 'جناح تنفيذي'];

        function premiumRoomFamily(data) {
            const type = String(data?.roomType ?? data?.RoomType ?? '');
            const internalRooms = Number(data?.internalRoomsCount ?? data?.InternalRoomsCount ?? 0);
            return internalRooms > 0 || premiumHotelSuiteTypes.includes(type) || type.includes('جناح') ? 'suite' : 'room';
        }

        function premiumTypeButtonsHtml(family, selectedType) {
            const types = family === 'suite' ? premiumHotelSuiteTypes : premiumHotelRoomTypes;
            const isKnown = types.includes(selectedType);
            return `${types.map((type, index) => `
                <button type="button" class="details-room-type-choice ${(isKnown ? type === selectedType : index === 0) ? 'is-active' : ''}"
                        data-unit-type-button data-family="${family}" data-value="${detailsEscape(type)}">
                    <i class="bi ${family === 'suite' ? 'bi-gem' : 'bi-door-open'}"></i>
                    <span>${detailsEscape(type)}</span><small>${family === 'suite' ? 'جناح' : 'غرفة'}</small>
                </button>`).join('')}
                <button type="button" class="details-room-type-choice ${!isKnown && selectedType ? 'is-active' : ''}"
                        data-unit-type-button data-family="${family}" data-value="أخرى">
                    <i class="bi bi-plus-circle"></i><span>أخرى</span><small>نوع مخصص</small>
                </button>`;
        }

        function premiumUnitRowHtml(unit) {
            const data = unit || {};
            return `<div class="details-floor-unit" data-room-unit>
                <input type="hidden" data-unit-field="RoomUnitID" value="${data.roomUnitID || data.RoomUnitID || 0}">
                <label><span>رقم/رمز الوحدة</span><input class="form-control" data-unit-field="RoomNumber" maxlength="50"
                    value="${detailsEscape(data.roomNumber || data.RoomNumber || '')}" placeholder="مثال: 101"></label>
                <label class="details-floor-unit__available"><input type="checkbox" data-unit-field="IsAvailable"
                    ${(data.isAvailable ?? data.IsAvailable ?? true) ? 'checked' : ''}><i></i><span>متاحة</span></label>
                <label><span>ملاحظات</span><input class="form-control" data-unit-field="Notes" maxlength="250"
                    value="${detailsEscape(data.notes || data.Notes || '')}" placeholder="اختياري"></label>
                <button type="button" class="details-room-unit-remove" data-remove-room-unit title="حذف الوحدة"><i class="bi bi-trash3"></i></button>
            </div>`;
        }

        function groupPremiumUnitsByFloor(units) {
            const groups = new Map();
            (units || []).forEach(unit => {
                const number = Number(unit.floorNumber ?? unit.FloorNumber ?? 0);
                const label = String(unit.floorLabel ?? unit.FloorLabel ?? '').trim();
                const key = `${number}|${label}`;
                if (!groups.has(key)) groups.set(key, { number, label, units: [] });
                groups.get(key).units.push(unit);
            });
            return groups.size ? Array.from(groups.values()) : [{ number: 0, label: 'الدور الأرضي', units: [] }];
        }

        function premiumFloorCardHtml(prefix, key, floor, expanded) {
            const data = floor || { number: 0, label: '', units: [] };
            const units = data.units || [];
            const title = data.label || (Number(data.number) === 0 ? 'الدور الأرضي' : `الطابق ${data.number}`);
            return `<article class="details-floor-card ${expanded ? '' : 'is-collapsed'}" data-floor-card data-floor-key="${key}">
                <header class="details-floor-card__header">
                    <button type="button" class="details-floor-card__toggle" data-floor-toggle aria-expanded="${expanded ? 'true' : 'false'}">
                        <span class="details-floor-card__icon"><i class="bi bi-layers"></i></span>
                        <span class="details-floor-card__copy"><strong data-floor-title>${detailsEscape(title)}</strong>
                            <small><b data-floor-count>${units.length}</b> وحدة فعلية</small></span>
                        <i class="bi bi-chevron-down details-floor-card__chevron"></i>
                    </button>
                    <button type="button" class="details-floor-card__delete" data-delete-floor title="حذف الطابق"><i class="bi bi-trash3"></i></button>
                </header>
                <div class="details-floor-card__body" data-floor-body>
                    <div class="details-floor-identity">
                        <label><span>رقم الطابق <b>*</b></span><input type="number" min="-100" max="1000" class="form-control"
                            data-floor-number value="${data.number ?? 0}" placeholder="0"></label>
                        <label><span>اسم الطابق</span><input class="form-control" maxlength="50" data-floor-label
                            value="${detailsEscape(data.label || '')}" placeholder="مثال: الدور الأرضي"></label>
                    </div>
                    <section class="details-floor-generator">
                        <div class="details-floor-generator__title"><span><i class="bi bi-magic"></i></span><div>
                            <strong>إنشاء نطاق وحدات</strong><small>اكتبي البداية والنهاية وسيتم إنشاء الأرقام داخل هذا الطابق.</small></div></div>
                        <div class="details-floor-generator__grid">
                            <label><span>من</span><input class="form-control" data-range-start inputmode="numeric" placeholder="1 أو 101"></label>
                            <label><span>إلى</span><input class="form-control" data-range-end inputmode="numeric" placeholder="20 أو 110"></label>
                            <label><span>بادئة اختيارية</span><input class="form-control" data-range-prefix maxlength="12" placeholder="B"></label>
                            <button type="button" class="details-add-button" data-generate-floor-range><i class="bi bi-plus-circle"></i> إنشاء</button>
                        </div>
                    </section>
                    <div class="details-floor-units-toolbar"><div><strong>الوحدات الفعلية</strong><small>يمكن تعديل الرقم والحالة لكل وحدة.</small></div>
                        <button type="button" class="details-add-button details-add-button--soft" data-add-floor-unit><i class="bi bi-plus-lg"></i> وحدة يدوية</button></div>
                    <div class="details-floor-units" data-floor-units>${units.map(premiumUnitRowHtml).join('')}</div>
                </div>
            </article>`;
        }

        function buildPremiumHotelRoomEditorHtml(prefix, room) {
            const data = room || {};
            const type = data.roomType || data.RoomType || 'غرفة مفردة';
            const family = premiumRoomFamily(data);
            const knownTypes = family === 'suite' ? premiumHotelSuiteTypes : premiumHotelRoomTypes;
            const floors = groupPremiumUnitsByFloor(data.units || data.Units || []);
            return `<div class="details-hotel-room-editor details-hotel-room-editor--premium" data-premium-room-editor="${prefix}"
                         data-family="${family}" data-selected-type="${knownTypes.includes(type) ? detailsEscape(type) : 'أخرى'}">
                <section class="details-room-editor-section">
                    <div class="details-room-editor-section__heading"><span><i class="bi bi-grid"></i></span><div>
                        <strong>تصنيف ونوع الوحدة</strong><small>اختاري غرفة أو جناحًا، وستظهر الحقول المناسبة تلقائيًا.</small></div></div>
                    <div class="details-unit-family-switch">
                        <button type="button" data-unit-family="room" class="${family === 'room' ? 'is-active' : ''}"><i class="bi bi-door-open"></i><span>غرفة</span><small>سرير ونوع سرير</small></button>
                        <button type="button" data-unit-family="suite" class="${family === 'suite' ? 'is-active' : ''}"><i class="bi bi-gem"></i><span>جناح</span><small>غرف داخلية مستقلة</small></button>
                    </div>
                    <div class="details-room-type-grid" data-type-group="room">${premiumTypeButtonsHtml('room', family === 'room' ? type : '')}</div>
                    <div class="details-room-type-grid" data-type-group="suite">${premiumTypeButtonsHtml('suite', family === 'suite' ? type : '')}</div>
                    <div class="details-custom-room-type" data-custom-type-wrap>
                        <label>اسم النوع المخصص <span>*</span></label><input id="${prefix}-custom-type" class="form-control" maxlength="50"
                            value="${knownTypes.includes(type) ? '' : detailsEscape(type)}" placeholder="مثال: جناح شهر العسل">
                    </div>
                </section>

                <section class="details-room-editor-section">
                    <div class="details-room-editor-section__heading"><span><i class="bi bi-card-checklist"></i></span><div>
                        <strong>بيانات العرض والحجز</strong><small>هذه البيانات تظهر للعميل عند اختيار الوحدة.</small></div></div>
                    <div class="details-room-fields-grid">
                        <label class="details-room-field--wide"><span>اسم عرض النوع <b>*</b></span><input id="${prefix}-name" class="form-control" maxlength="50" value="${detailsEscape(data.roomNumber || data.RoomNumber || '')}" placeholder="مثال: غرفة ديلوكس مطلة"></label>
                        <label><span>السعر <b>*</b></span><input id="${prefix}-price" type="number" min="0.01" step="0.01" class="form-control" value="${data.price ?? data.Price ?? ''}"></label>
                        <label><span>المساحة م²</span><input id="${prefix}-area" type="number" min="0.01" step="0.01" class="form-control" value="${data.areaSquareMeters ?? data.AreaSquareMeters ?? ''}"></label>
                        <label><span>البالغون <b>*</b></span><input id="${prefix}-adults" type="number" min="1" max="100" class="form-control" value="${data.maxAdults ?? data.MaxAdults ?? 1}"></label>
                        <label><span>الأطفال</span><input id="${prefix}-children" type="number" min="0" max="100" class="form-control" value="${data.maxChildren ?? data.MaxChildren ?? 0}"></label>
                        <label data-room-only><span>عدد الأسرّة</span><input id="${prefix}-beds" type="number" min="0" max="50" class="form-control" value="${data.bedsCount ?? data.BedsCount ?? ''}"></label>
                        <label data-room-only><span>نوع السرير</span><input id="${prefix}-bed-type" class="form-control" maxlength="50" value="${detailsEscape(data.bedType || data.BedType || '')}" placeholder="مثال: مزدوج كبير"></label>
                        <label data-suite-only class="details-suite-count-field"><span>عدد الغرف الداخلية للجناح <b>*</b></span><input id="${prefix}-internal-rooms" type="number" min="1" max="100" class="form-control" value="${data.internalRoomsCount ?? data.InternalRoomsCount ?? 1}"></label>
                        <div data-suite-only class="details-suite-note"><i class="bi bi-info-circle"></i><span>كل رقم وحدة في الطوابق أدناه يمثل جناحًا فعليًا، وهذا الحقل يحدد عدد غرفه الداخلية.</span></div>
                        <label class="details-room-field--full"><span>الوصف</span><textarea id="${prefix}-description" class="form-control" maxlength="500" rows="2">${detailsEscape(data.description || data.Description || '')}</textarea></label>
                    </div>
                </section>

                <section class="details-room-editor-section details-floors-section">
                    <div class="details-floor-main-toolbar"><div class="details-room-editor-section__heading"><span><i class="bi bi-building"></i></span><div>
                        <strong>الطوابق والوحدات الفعلية</strong><small>افتحي الطابق المطلوب فقط؛ بقية الطوابق تبقى مطوية لتوفير المساحة.</small></div></div>
                        <button type="button" class="details-add-button" data-add-floor><i class="bi bi-plus-lg"></i> إضافة طابق</button></div>
                    <div class="details-floor-cards" data-floor-cards>${floors.map((floor, index) => premiumFloorCardHtml(prefix, index + 1, floor, index === 0)).join('')}</div>
                </section>
                ${getArabicUploadFieldHTML({ id: `${prefix}-images`, name: 'RoomImages', title: room ? 'إضافة صور جديدة للنوع' : 'صور النوع', description: 'الحد الإجمالي للصور 5 ميجابايت.', buttonText: 'اختيار الصور' })}
            </div>`;
        }

        function initPremiumHotelRoomEditor(prefix) {
            const root = document.querySelector(`[data-premium-room-editor="${prefix}"]`);
            if (!root || root.dataset.ready === 'true') return;
            root.dataset.ready = 'true';
            const cards = root.querySelector('[data-floor-cards]');
            let floorSequence = cards.querySelectorAll('[data-floor-card]').length + 1;

            const refreshFamily = family => {
                root.dataset.family = family;
                root.querySelectorAll('[data-unit-family]').forEach(button => button.classList.toggle('is-active', button.dataset.unitFamily === family));
                root.querySelectorAll('[data-type-group]').forEach(group => group.hidden = group.dataset.typeGroup !== family);
                root.querySelectorAll('[data-room-only]').forEach(item => item.hidden = family !== 'room');
                root.querySelectorAll('[data-suite-only]').forEach(item => item.hidden = family !== 'suite');
                const selected = root.querySelector(`[data-type-group="${family}"] [data-unit-type-button].is-active`);
                root.dataset.selectedType = selected?.dataset.value || (family === 'suite' ? 'جناح عائلي' : 'غرفة مفردة');
                refreshCustomType();
            };

            const refreshCustomType = () => {
                const show = root.dataset.selectedType === 'أخرى';
                root.querySelector('[data-custom-type-wrap]').classList.toggle('is-visible', show);
            };

            root.querySelectorAll('[data-unit-family]').forEach(button => button.addEventListener('click', () => refreshFamily(button.dataset.unitFamily)));
            root.querySelectorAll('[data-unit-type-button]').forEach(button => button.addEventListener('click', () => {
                const family = button.dataset.family;
                root.querySelectorAll(`[data-unit-type-button][data-family="${family}"]`).forEach(item => item.classList.toggle('is-active', item === button));
                root.dataset.selectedType = button.dataset.value;
                refreshCustomType();
            }));

            const bindFloor = card => {
                const body = card.querySelector('[data-floor-body]');
                const count = card.querySelector('[data-floor-count]');
                const floorNumber = card.querySelector('[data-floor-number]');
                const floorLabel = card.querySelector('[data-floor-label]');
                const units = card.querySelector('[data-floor-units]');
                const updateFloor = () => {
                    count.textContent = units.querySelectorAll('[data-room-unit]').length;
                    const label = floorLabel.value.trim();
                    card.querySelector('[data-floor-title]').textContent = label || (Number(floorNumber.value || 0) === 0 ? 'الدور الأرضي' : `الطابق ${floorNumber.value}`);
                };
                const bindUnit = row => row.querySelector('[data-remove-room-unit]')?.addEventListener('click', () => { row.remove(); updateFloor(); });
                units.querySelectorAll('[data-room-unit]').forEach(bindUnit);
                floorNumber.addEventListener('input', updateFloor);
                floorLabel.addEventListener('input', updateFloor);
                card.querySelector('[data-floor-toggle]').addEventListener('click', () => {
                    card.classList.toggle('is-collapsed');
                    const expanded = !card.classList.contains('is-collapsed');
                    card.querySelector('[data-floor-toggle]').setAttribute('aria-expanded', String(expanded));
                });
                card.querySelector('[data-delete-floor]').addEventListener('click', () => card.remove());
                card.querySelector('[data-add-floor-unit]').addEventListener('click', () => {
                    units.insertAdjacentHTML('beforeend', premiumUnitRowHtml({})); bindUnit(units.lastElementChild); updateFloor();
                    units.lastElementChild.querySelector('[data-unit-field="RoomNumber"]')?.focus();
                });
                card.querySelector('[data-generate-floor-range]').addEventListener('click', () => {
                    const startRaw = card.querySelector('[data-range-start]').value.trim();
                    const endRaw = card.querySelector('[data-range-end]').value.trim();
                    const start = Number(startRaw), end = Number(endRaw);
                    if (!Number.isInteger(start) || !Number.isInteger(end) || start > end || end - start > 500) {
                        Swal.showValidationMessage('أدخلي نطاقًا صحيحًا بحد أقصى 501 وحدة داخل الطابق.'); return;
                    }
                    const pad = Math.max(startRaw.length, endRaw.length);
                    const prefixText = card.querySelector('[data-range-prefix]').value.trim();
                    for (let n = start; n <= end; n++) {
                        units.insertAdjacentHTML('beforeend', premiumUnitRowHtml({ roomNumber: prefixText + String(n).padStart(pad, '0'), isAvailable: true }));
                        bindUnit(units.lastElementChild);
                    }
                    card.classList.remove('is-collapsed'); updateFloor();
                });
                updateFloor();
            };

            cards.querySelectorAll('[data-floor-card]').forEach(bindFloor);
            root.querySelector('[data-add-floor]').addEventListener('click', () => {
                cards.insertAdjacentHTML('beforeend', premiumFloorCardHtml(prefix, floorSequence++, { number: 0, label: '', units: [] }, true));
                bindFloor(cards.lastElementChild);
                cards.lastElementChild.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            });
            refreshFamily(root.dataset.family || 'room');
        }

        function collectPremiumHotelRoomForm(prefix, placeId, roomId) {
            const root = document.querySelector(`[data-premium-room-editor="${prefix}"]`);
            if (!root) return { error: 'تعذر قراءة نموذج الوحدة.' };
            const family = root.dataset.family || 'room';
            const selectedType = root.dataset.selectedType;
            const roomType = selectedType === 'أخرى' ? root.querySelector(`#${prefix}-custom-type`).value.trim() : selectedType;
            const name = root.querySelector(`#${prefix}-name`).value.trim();
            const price = Number(root.querySelector(`#${prefix}-price`).value);
            const adults = Number(root.querySelector(`#${prefix}-adults`).value);
            const internalRooms = Number(root.querySelector(`#${prefix}-internal-rooms`)?.value || 0);
            const floorCards = Array.from(root.querySelectorAll('[data-floor-card]'));
            const unitRows = floorCards.flatMap(card => Array.from(card.querySelectorAll('[data-room-unit]')).map(row => ({ card, row })));

            if (!roomType || !name || !Number.isFinite(price) || price <= 0 || !Number.isInteger(adults) || adults < 1)
                return { error: 'أكملي نوع الوحدة واسم العرض والسعر وعدد البالغين.' };
            if (family === 'suite' && (!Number.isInteger(internalRooms) || internalRooms < 1 || internalRooms > 100))
                return { error: 'حددي عدد الغرف الداخلية للجناح من 1 إلى 100.' };
            if (!unitRows.length) return { error: 'أضيفي وحدة فعلية واحدة على الأقل داخل أحد الطوابق.' };

            const numbers = new Set();
            for (const item of unitRows) {
                const number = item.row.querySelector('[data-unit-field="RoomNumber"]').value.trim();
                const normalized = number.toLocaleLowerCase('ar');
                if (!number) return { error: 'رقم/رمز كل وحدة فعلية مطلوب.' };
                if (numbers.has(normalized)) return { error: `رقم الوحدة (${number}) مكرر.` };
                numbers.add(normalized);
            }

            const data = new FormData();
            data.append('PlaceID', placeId);
            if (roomId) data.append('RoomID', roomId);
            data.append('RoomType', roomType);
            data.append('RoomNumber', name);
            data.append('Price', String(price));
            data.append('AreaSquareMeters', root.querySelector(`#${prefix}-area`).value);
            data.append('MaxAdults', String(adults));
            data.append('MaxChildren', root.querySelector(`#${prefix}-children`).value || '0');
            data.append('BedsCount', family === 'room' ? root.querySelector(`#${prefix}-beds`).value : '');
            data.append('BedType', family === 'room' ? root.querySelector(`#${prefix}-bed-type`).value.trim() : '');
            data.append('InternalRoomsCount', family === 'suite' ? String(internalRooms) : '');
            data.append('Description', root.querySelector(`#${prefix}-description`).value.trim());
            data.append('IsAvailable', 'true');
            data.append('Quantity', String(unitRows.length));
            unitRows.forEach((item, index) => {
                const get = field => item.row.querySelector(`[data-unit-field="${field}"]`);
                data.append(`Units[${index}].RoomUnitID`, get('RoomUnitID').value || '0');
                data.append(`Units[${index}].PlaceID`, String(placeId));
                data.append(`Units[${index}].RoomID`, String(roomId || 0));
                data.append(`Units[${index}].RoomNumber`, get('RoomNumber').value.trim());
                data.append(`Units[${index}].FloorNumber`, item.card.querySelector('[data-floor-number]').value || '0');
                data.append(`Units[${index}].FloorLabel`, item.card.querySelector('[data-floor-label]').value.trim());
                data.append(`Units[${index}].IsAvailable`, get('IsAvailable').checked ? 'true' : 'false');
                data.append(`Units[${index}].Notes`, get('Notes').value.trim());
            });
            const files = root.querySelector(`#${prefix}-images`)?.files || [];
            for (const file of files) data.append('RoomImages', file);
            return { data };
        }


        // =====================================================================
        // HOTEL TYPES + FLOOR PLAN V1
        // فصل بيانات النوع عن الطوابق والوحدات الفعلية.
        // =====================================================================

        function getDetailsRooms() {
            return Array.isArray(window.currentPlaceData?.rooms) ? window.currentPlaceData.rooms : [];
        }

        function getDetailsRoomId(room) {
            return Number(room?.roomId ?? room?.RoomId ?? room?.roomID ?? 0);
        }

        function getDetailsRoomLabel(room) {
            const display = String(room?.roomNumber ?? room?.RoomNumber ?? '').trim();
            const type = String(room?.roomType ?? room?.RoomType ?? '').trim();
            return display && type ? `${display} — ${type}` : (display || type || 'نوع بدون اسم');
        }

        function buildRoomTypeEditorHtml(prefix, room) {
            const data = room || {};
            const type = data.roomType || data.RoomType || 'غرفة مفردة';
            const family = premiumRoomFamily(data);
            const knownTypes = family === 'suite' ? premiumHotelSuiteTypes : premiumHotelRoomTypes;
            const currentType = knownTypes.includes(type) ? type : 'أخرى';

            return `<div class="details-hotel-room-editor details-room-type-editor" data-room-type-editor="${prefix}"
                        data-family="${family}"
                        data-selected-type="${detailsEscape(currentType)}"
                        data-original-type="${detailsEscape(type)}"
                        data-original-name="${detailsEscape(data.roomNumber || data.RoomNumber || '')}"
                        data-is-available="${(data.isAvailable ?? data.IsAvailable ?? true) !== false}">
                <section class="details-room-editor-section">
                    <div class="details-room-editor-section__heading">
                        <span><i class="bi bi-grid"></i></span>
                        <div>
                            <strong>تصنيف ونوع الوحدة</strong>
                            <small>هنا تعدّل بيانات النوع فقط، أما الغرف الفعلية والطوابق فلها مدير مستقل.</small>
                        </div>
                    </div>

                    <div class="details-unit-family-switch">
                        <button type="button" data-unit-family="room" class="${family === 'room' ? 'is-active' : ''}">
                            <i class="bi bi-door-open"></i><span>غرفة</span><small>سرير ونوع سرير</small>
                        </button>
                        <button type="button" data-unit-family="suite" class="${family === 'suite' ? 'is-active' : ''}">
                            <i class="bi bi-gem"></i><span>جناح</span><small>غرف داخلية مستقلة</small>
                        </button>
                    </div>

                    <div class="details-room-type-grid" data-type-group="room">
                        ${premiumTypeButtonsHtml('room', family === 'room' ? type : '')}
                    </div>

                    <div class="details-room-type-grid" data-type-group="suite">
                        ${premiumTypeButtonsHtml('suite', family === 'suite' ? type : '')}
                    </div>

                    <div class="details-custom-room-type ${currentType === 'أخرى' ? 'is-visible' : ''}" data-custom-type-wrap>
                        <label>
                            اسم النوع المخصص <span>*</span>
                            <input id="${prefix}-custom-type" class="form-control" maxlength="50"
                                value="${currentType === 'أخرى' ? detailsEscape(type) : ''}"
                                placeholder="مثال: جناح شهر العسل">
                        </label>
                    </div>
                </section>

                <section class="details-room-editor-section">
                    <div class="details-room-editor-section__heading">
                        <span><i class="bi bi-card-checklist"></i></span>
                        <div>
                            <strong>بيانات العرض والحجز</strong>
                            <small>هذه البيانات ستظهر للعميل عند اختيار هذا النوع.</small>
                        </div>
                    </div>

                    <div class="details-room-fields-grid">
                        <label class="details-room-field--wide">
                            <span>اسم عرض النوع <b>*</b></span>
                            <input id="${prefix}-name" class="form-control" maxlength="50"
                                value="${detailsEscape(data.roomNumber || data.RoomNumber || '')}"
                                placeholder="مثال: غرفة ديلوكس مطلة">
                        </label>

                        <label>
                            <span>السعر <b>*</b></span>
                            <input id="${prefix}-price" type="number" min="0.01" step="0.01" class="form-control"
                                value="${data.price ?? data.Price ?? ''}">
                        </label>

                        <label>
                            <span>المساحة م²</span>
                            <input id="${prefix}-area" type="number" min="0.01" step="0.01" class="form-control"
                                value="${data.areaSquareMeters ?? data.AreaSquareMeters ?? ''}">
                        </label>

                        <label>
                            <span>البالغون <b>*</b></span>
                            <input id="${prefix}-adults" type="number" min="1" max="100" class="form-control"
                                value="${data.maxAdults ?? data.MaxAdults ?? 1}">
                        </label>

                        <label>
                            <span>الأطفال</span>
                            <input id="${prefix}-children" type="number" min="0" max="100" class="form-control"
                                value="${data.maxChildren ?? data.MaxChildren ?? 0}">
                        </label>

                        <label data-room-only>
                            <span>عدد الأسرّة</span>
                            <input id="${prefix}-beds" type="number" min="0" max="50" class="form-control"
                                value="${data.bedsCount ?? data.BedsCount ?? ''}">
                        </label>

                        <label data-room-only>
                            <span>نوع السرير</span>
                            <input id="${prefix}-bed-type" class="form-control" maxlength="50"
                                value="${detailsEscape(data.bedType || data.BedType || '')}"
                                placeholder="مثال: مزدوج كبير">
                        </label>

                        <label data-suite-only>
                            <span>عدد الغرف الداخلية للجناح <b>*</b></span>
                            <input id="${prefix}-internal-rooms" type="number" min="1" max="100" class="form-control"
                                value="${data.internalRoomsCount ?? data.InternalRoomsCount ?? 1}">
                        </label>

                        <label class="details-room-field--full">
                            <span>الوصف</span>
                            <textarea id="${prefix}-description" class="form-control" maxlength="500" rows="3">${detailsEscape(data.description || data.Description || '')}</textarea>
                        </label>
                    </div>
                </section>

                ${getArabicUploadFieldHTML({
                id: `${prefix}-images`,
                name: 'RoomImages',
                title: room ? 'إضافة صور جديدة للنوع' : 'صور النوع',
                description: 'الحد الإجمالي للصور 5 ميجابايت.',
                buttonText: 'اختيار الصور'
            })}
            </div>`;
        }

        function initRoomTypeEditor(prefix) {
            const root = document.querySelector(`[data-room-type-editor="${prefix}"]`);
            if (!root || root.dataset.ready === 'true') return;
            root.dataset.ready = 'true';

            const nameInput = root.querySelector(`#${prefix}-name`);
            const customTypeInput = root.querySelector(`#${prefix}-custom-type`);

            const predefinedTypeNames = new Set(
                [...premiumHotelRoomTypes, ...premiumHotelSuiteTypes]
                    .map(value => String(value || '').trim().replace(/\s+/g, ' ').toLocaleLowerCase('ar'))
                    .filter(Boolean)
            );

            const normalizeTypeText = value =>
                String(value || '')
                    .trim()
                    .replace(/\s+/g, ' ')
                    .toLocaleLowerCase('ar');

            // الاسم يبقى مرتبطًا بالنوع ما دام المستخدم لم يكتب اسمًا تسويقيًا مخصصًا.
            // كذلك نعالج البيانات القديمة التي كان فيها الاسم نوعًا قديمًا مختلفًا.
            let displayNameIsAutoManaged = (() => {
                if (!nameInput) return false;

                const currentName = normalizeTypeText(nameInput.value);
                const originalType = normalizeTypeText(root.dataset.originalType);
                const originalName = normalizeTypeText(root.dataset.originalName);

                if (!currentName) return true;
                if (currentName === originalType) return true;
                if (originalName && originalName === originalType) return true;

                // أهم إصلاح: إذا الاسم الحالي واحد من أسماء الأنواع المعروفة
                // مثل "غرفة مفردة"، فهو غالبًا اسم تلقائي قديم وليس اسمًا تسويقيًا.
                if (predefinedTypeNames.has(currentName)) return true;

                return false;
            })();

            const setDisplayNameFromType = nextType => {
                if (!nameInput || !displayNameIsAutoManaged) return;
                const value = String(nextType || '').trim();
                if (!value || value === 'أخرى') return;
                nameInput.value = value;
            };

            const refreshCustom = () => {
                const custom = root.querySelector('[data-custom-type-wrap]');
                if (custom) custom.classList.toggle('is-visible', root.dataset.selectedType === 'أخرى');
            };

            const refreshFamily = (family, preserveSelection = false) => {
                root.dataset.family = family;

                root.querySelectorAll('[data-unit-family]').forEach(button => {
                    button.classList.toggle('is-active', button.dataset.unitFamily === family);
                });

                root.querySelectorAll('[data-type-group]').forEach(group => {
                    group.hidden = group.dataset.typeGroup !== family;
                });

                root.querySelectorAll('[data-room-only]').forEach(item => item.hidden = family !== 'room');
                root.querySelectorAll('[data-suite-only]').forEach(item => item.hidden = family !== 'suite');

                if (!preserveSelection) {
                    const first = root.querySelector(`[data-type-group="${family}"] [data-unit-type-button]`);

                    root.querySelectorAll(`[data-unit-type-button][data-family="${family}"]`).forEach(item => {
                        item.classList.toggle('is-active', item === first);
                    });

                    root.dataset.selectedType =
                        first?.dataset.value ||
                        (family === 'suite' ? 'جناح عائلي' : 'غرفة مفردة');

                    setDisplayNameFromType(root.dataset.selectedType);
                }

                refreshCustom();
            };

            // إذا المستخدم كتب بنفسه في اسم العرض، نوقف المزامنة نهائيًا لهذه الجلسة.
            if (nameInput) {
                nameInput.addEventListener('input', event => {
                    if (event.isTrusted) {
                        displayNameIsAutoManaged = false;
                    }
                });
            }

            root.querySelectorAll('[data-unit-family]').forEach(button => {
                button.addEventListener('click', () => {
                    const nextFamily = button.dataset.unitFamily;
                    if (nextFamily === root.dataset.family) return;

                    refreshFamily(nextFamily, false);
                });
            });

            root.querySelectorAll('[data-unit-type-button]').forEach(button => {
                button.addEventListener('click', () => {
                    const family = button.dataset.family;
                    const nextType = button.dataset.value;

                    root.querySelectorAll(`[data-unit-type-button][data-family="${family}"]`).forEach(item => {
                        item.classList.toggle('is-active', item === button);
                    });

                    root.dataset.family = family;
                    root.dataset.selectedType = nextType;
                    refreshFamily(family, true);

                    if (nextType !== 'أخرى') {
                        setDisplayNameFromType(nextType);
                    }
                });
            });

            if (customTypeInput && nameInput) {
                customTypeInput.addEventListener('input', () => {
                    if (root.dataset.selectedType === 'أخرى' && displayNameIsAutoManaged) {
                        nameInput.value = customTypeInput.value;
                    }
                });
            }

            refreshFamily(root.dataset.family || 'room', true);

            // عند فتح سجل قديم فيه تعارض مثل:
            // RoomType = غرفة ثلاثية، RoomNumber = غرفة مفردة
            // نصلحه بصريًا فورًا ما دام الاسم من الأنواع الجاهزة.
            if (displayNameIsAutoManaged && root.dataset.selectedType !== 'أخرى') {
                setDisplayNameFromType(root.dataset.selectedType);
            }
        }

        function collectRoomTypeForm(prefix, placeId, roomId) {
            const root = document.querySelector(`[data-room-type-editor="${prefix}"]`);
            if (!root) return { error: 'تعذر قراءة نموذج نوع الغرفة.' };

            const family = root.dataset.family || 'room';
            const selectedType = root.dataset.selectedType || '';
            const customType = root.querySelector(`#${prefix}-custom-type`)?.value.trim() || '';
            const roomType = selectedType === 'أخرى' ? customType : selectedType;
            const displayName = root.querySelector(`#${prefix}-name`)?.value.trim() || '';
            const price = Number(root.querySelector(`#${prefix}-price`)?.value || 0);
            const adults = Number(root.querySelector(`#${prefix}-adults`)?.value || 0);
            const children = Number(root.querySelector(`#${prefix}-children`)?.value || 0);
            const internalRooms = Number(root.querySelector(`#${prefix}-internal-rooms`)?.value || 0);

            if (!roomType) return { error: 'حددي نوع الغرفة أو الجناح.' };
            if (!displayName) return { error: 'اكتبي اسم عرض النوع.' };
            if (!Number.isFinite(price) || price <= 0) return { error: 'أدخلي سعرًا صحيحًا أكبر من صفر.' };
            if (!Number.isInteger(adults) || adults < 1) return { error: 'حددي عدد البالغين.' };
            if (!Number.isInteger(children) || children < 0) return { error: 'عدد الأطفال غير صحيح.' };
            if (family === 'suite' && (!Number.isInteger(internalRooms) || internalRooms < 1 || internalRooms > 100)) {
                return { error: 'حددي عدد الغرف الداخلية للجناح من 1 إلى 100.' };
            }

            const duplicate = getDetailsRooms().some(item => {
                const id = getDetailsRoomId(item);
                if (Number(id) === Number(roomId || 0)) return false;
                const otherType = String(item.roomType ?? item.RoomType ?? '').trim().toLowerCase();
                const otherName = String(item.roomNumber ?? item.RoomNumber ?? '').trim().toLowerCase();
                return otherType === roomType.toLowerCase() && otherName === displayName.toLowerCase();
            });

            if (duplicate) return { error: 'يوجد نوع آخر بالاسم والتصنيف نفسيهما.' };

            const data = new FormData();
            data.append('PlaceID', String(placeId));
            if (roomId) data.append('RoomID', String(roomId));
            data.append('RoomType', roomType);
            data.append('RoomNumber', displayName);
            data.append('Price', String(price));
            data.append('AreaSquareMeters', root.querySelector(`#${prefix}-area`)?.value || '');
            data.append('MaxAdults', String(adults));
            data.append('MaxChildren', String(children));
            data.append('BedsCount', family === 'room' ? (root.querySelector(`#${prefix}-beds`)?.value || '') : '');
            data.append('BedType', family === 'room' ? (root.querySelector(`#${prefix}-bed-type`)?.value.trim() || '') : '');
            data.append('InternalRoomsCount', family === 'suite' ? String(internalRooms) : '');
            data.append('Description', root.querySelector(`#${prefix}-description`)?.value.trim() || '');
            data.append('IsAvailable', root.dataset.isAvailable === 'false' ? 'false' : 'true');

            const files = root.querySelector(`#${prefix}-images`)?.files || [];
            for (const file of files) data.append('RoomImages', file);

            return { data };
        }

        function buildExistingRoomImagesHtml(room) {
            const roomId = getDetailsRoomId(room);
            const rawImageUrl = String(room?.imageUrl ?? room?.ImageUrl ?? '');
            const images = rawImageUrl.split(',').map(path => path.trim()).filter(Boolean);
            if (!images.length) return '';

            return `<div class="details-current-room-images">
                <strong><i class="bi bi-images"></i> الصور الحالية</strong>
                <div class="details-existing-images">
                    ${images.map((path, index) => `
                        <div id="room-img-${roomId}-${index}" class="details-current-room-image">
                            <img src="${detailsEscape(path)}" alt="صورة النوع">
                            <button type="button"
                                onclick="window.deleteRoomImageDirect(${roomId}, '${detailsEscape(path)}', ${index})"
                                title="حذف الصورة"><i class="bi bi-x-lg"></i></button>
                        </div>`).join('')}
                </div>
            </div>`;
        }

        window.openAddRoomTypeModal = function () {
            const placeId = Number(window.activeDetailsPlaceId || 0);
            if (!placeId) return;

            Swal.fire({
                title: '<h5 class="fw-bold text-primary m-0">إضافة نوع غرفة أو جناح</h5>',
                html: `<div class="details-modal-body text-start" dir="rtl">${buildRoomTypeEditorHtml('add-room-type', null)}</div>`,
                showCloseButton: true,
                allowOutsideClick: false,
                showCancelButton: true,
                confirmButtonText: 'إضافة النوع',
                cancelButtonText: 'إلغاء',
                customClass: {
                    popup: 'details-luxury-modal details-luxury-modal--wide',
                    confirmButton: 'details-modal-confirm',
                    cancelButton: 'details-modal-cancel',
                    htmlContainer: 'details-modal-html',
                    actions: 'details-modal-actions'
                },
                buttonsStyling: false,
                didOpen: () => initRoomTypeEditor('add-room-type'),
                preConfirm: async () => {
                    const collected = collectRoomTypeForm('add-room-type', placeId, 0);
                    if (collected.error) {
                        Swal.showValidationMessage(collected.error);
                        return false;
                    }

                    try {
                        const response = await fetch('/Owner/Place/AddRoomType', {
                            method: 'POST',
                            body: collected.data
                        });
                        const result = await response.json();
                        if (!result.success) {
                            Swal.showValidationMessage(result.message || 'تعذر إضافة النوع.');
                            return false;
                        }
                        return result;
                    } catch (error) {
                        Swal.showValidationMessage('فشل الاتصال بالخادم.');
                        return false;
                    }
                }
            }).then(result => {
                if (result.isConfirmed && result.value) {
                    Swal.mixin({ toast: true, timer: 1600 }).fire({ icon: 'success', title: result.value.message });
                    refreshDetailsView(placeId);
                }
            });
        };

        window.openEditRoomTypeModal = function (id) {
            const room = getDetailsRooms().find(item => getDetailsRoomId(item) === Number(id));
            if (!room) {
                Swal.fire('تنبيه', 'تعذر العثور على بيانات النوع. حدّث الصفحة وحاول مجددًا.', 'warning');
                return;
            }

            const roomId = getDetailsRoomId(room);
            const unitsCount = (room.units || room.Units || []).length;

            Swal.fire({
                title: '<h5 class="fw-bold text-primary m-0">تعديل بيانات النوع</h5>',
                html: `<div class="details-modal-body text-start" dir="rtl">
                    <div class="details-room-edit-context">
                        <i class="bi bi-info-circle"></i>
                        <span>هذا التعديل يخص بيانات النوع فقط. عدد الوحدات الحالية: <b>${unitsCount.toLocaleString('ar-YE')}</b>. لتعديل الأرقام والطوابق استخدم «إدارة الطوابق والغرف».</span>
                    </div>
                    ${buildRoomTypeEditorHtml('edit-room-type', room)}
                    ${buildExistingRoomImagesHtml(room)}
                </div>`,
                showCloseButton: true,
                allowOutsideClick: false,
                showCancelButton: true,
                confirmButtonText: 'حفظ التعديل',
                cancelButtonText: 'إلغاء',
                customClass: {
                    popup: 'details-luxury-modal details-luxury-modal--wide',
                    confirmButton: 'details-modal-confirm',
                    cancelButton: 'details-modal-cancel',
                    htmlContainer: 'details-modal-html',
                    actions: 'details-modal-actions'
                },
                buttonsStyling: false,
                didOpen: () => initRoomTypeEditor('edit-room-type'),
                preConfirm: async () => {
                    const collected = collectRoomTypeForm('edit-room-type', Number(window.activeDetailsPlaceId), roomId);
                    if (collected.error) {
                        Swal.showValidationMessage(collected.error);
                        return false;
                    }

                    try {
                        const response = await fetch('/Owner/Place/EditRoomType', {
                            method: 'POST',
                            body: collected.data
                        });
                        const result = await response.json();
                        if (!result.success) {
                            Swal.showValidationMessage(result.message || 'تعذر حفظ التعديل.');
                            return false;
                        }
                        return result;
                    } catch (error) {
                        Swal.showValidationMessage('فشل الاتصال بالخادم.');
                        return false;
                    }
                }
            }).then(result => {
                if (result.isConfirmed && result.value) {
                    Swal.mixin({ toast: true, timer: 1600 }).fire({ icon: 'success', title: result.value.message });
                    refreshDetailsView(window.activeDetailsPlaceId);
                }
            });
        };

        window.openEditRoomModal = window.openEditRoomTypeModal;

        function flattenHotelFloorUnits(rooms) {
            const result = [];
            (rooms || []).forEach(room => {
                const roomId = getDetailsRoomId(room);
                (room.units || room.Units || []).forEach(unit => {
                    result.push({
                        roomUnitID: Number(unit.roomUnitID ?? unit.RoomUnitID ?? unit.roomUnitId ?? 0),
                        roomId,
                        roomNumber: unit.roomNumber ?? unit.RoomNumber ?? '',
                        floorNumber: Number(unit.floorNumber ?? unit.FloorNumber ?? 0),
                        floorLabel: unit.floorLabel ?? unit.FloorLabel ?? '',
                        isAvailable: (unit.isAvailable ?? unit.IsAvailable ?? true) !== false,
                        notes: unit.notes ?? unit.Notes ?? ''
                    });
                });
            });
            return result;
        }

        function groupHotelFloorPlanUnits(units) {
            const map = new Map();

            (units || []).forEach(unit => {
                const floorNumber = Number(unit.floorNumber ?? 0);
                const floorLabel = String(unit.floorLabel ?? '').trim();

                // FIX: الطابق يُعرَّف برقم الطابق نفسه، وليس بالرقم + الاسم.
                // هذا يمنع تقسيم نفس الطابق إلى أكثر من بطاقة إذا اختلف FloorLabel
                // بين الوحدات القديمة أو كان بعضه فارغًا.
                const key = floorNumber;

                if (!map.has(key)) {
                    map.set(key, { floorNumber, floorLabel, units: [] });
                }

                const floor = map.get(key);

                // إذا أول وحدة لم يكن لها اسم طابق، نأخذ أول اسم غير فارغ من بقية الوحدات.
                if (!floor.floorLabel && floorLabel) {
                    floor.floorLabel = floorLabel;
                }

                floor.units.push(unit);
            });

            const floors = Array.from(map.values()).sort((a, b) => a.floorNumber - b.floorNumber);
            return floors.length ? floors : [{ floorNumber: 0, floorLabel: 'الدور الأرضي', units: [] }];
        }

        function roomTypeOptionsHtml(rooms, selectedRoomId) {
            return (rooms || []).map(room => {
                const roomId = getDetailsRoomId(room);
                const selected = Number(roomId) === Number(selectedRoomId) ? 'selected' : '';
                return `<option value="${roomId}" ${selected}>${detailsEscape(getDetailsRoomLabel(room))}</option>`;
            }).join('');
        }

        function floorPlanUnitRowHtml(unit, rooms) {
            const data = unit || {};
            const defaultRoomId = Number(data.roomId || getDetailsRoomId(rooms?.[0]));
            return `<div class="details-floor-plan-unit" data-plan-unit>
                <input type="hidden" data-plan-field="RoomUnitId" value="${Number(data.roomUnitID || 0)}">

                <label class="details-floor-plan-unit__type">
                    <span>نوع الغرفة/الجناح</span>
                    <select class="form-select" data-plan-field="RoomId">
                        ${roomTypeOptionsHtml(rooms, defaultRoomId)}
                    </select>
                </label>

                <label>
                    <span>رقم/رمز الوحدة</span>
                    <input class="form-control" maxlength="50" data-plan-field="RoomNumber"
                        value="${detailsEscape(data.roomNumber || '')}" placeholder="مثال: 101">
                </label>

                <label class="details-floor-unit__available">
                    <input type="checkbox" data-plan-field="IsAvailable" ${data.isAvailable !== false ? 'checked' : ''}>
                    <i></i><span>متاحة</span>
                </label>

                <label class="details-floor-plan-unit__notes">
                    <span>ملاحظات</span>
                    <input class="form-control" maxlength="250" data-plan-field="Notes"
                        value="${detailsEscape(data.notes || '')}" placeholder="اختياري">
                </label>

                <button type="button" class="details-room-unit-remove" data-remove-plan-unit title="حذف الوحدة">
                    <i class="bi bi-trash3"></i>
                </button>
            </div>`;
        }

        function floorPlanCardHtml(floor, index, rooms, expanded) {
            const data = floor || { floorNumber: 0, floorLabel: '', units: [] };
            const title = data.floorLabel || (Number(data.floorNumber) === 0 ? 'الدور الأرضي' : `الطابق ${data.floorNumber}`);

            return `<article class="details-floor-card details-floor-plan-card ${expanded ? '' : 'is-collapsed'}" data-plan-floor>
                <header class="details-floor-card__header">
                    <button type="button" class="details-floor-card__toggle" data-plan-floor-toggle aria-expanded="${expanded ? 'true' : 'false'}">
                        <span class="details-floor-card__icon"><i class="bi bi-layers"></i></span>
                        <span class="details-floor-card__copy">
                            <strong data-plan-floor-title>${detailsEscape(title)}</strong>
                            <small><b data-plan-floor-count>${(data.units || []).length}</b> وحدة فعلية</small>
                        </span>
                        <i class="bi bi-chevron-down details-floor-card__chevron"></i>
                    </button>

                    <button type="button" class="details-floor-card__delete" data-remove-plan-floor title="حذف الطابق">
                        <i class="bi bi-trash3"></i>
                    </button>
                </header>

                <div class="details-floor-card__body">
                    <div class="details-floor-identity">
                        <label>
                            <span>رقم الطابق <b>*</b></span>
                            <input type="number" min="-100" max="1000" class="form-control"
                                data-plan-floor-number value="${Number(data.floorNumber || 0)}">
                        </label>

                        <label>
                            <span>اسم الطابق</span>
                            <input class="form-control" maxlength="50" data-plan-floor-label
                                value="${detailsEscape(data.floorLabel || '')}"
                                placeholder="مثال: الدور الأرضي">
                        </label>
                    </div>

                    <section class="details-floor-generator details-floor-generator--plan">
                        <div class="details-floor-generator__title">
                            <span><i class="bi bi-magic"></i></span>
                            <div>
                                <strong>إنشاء نطاق غرف</strong>
                                <small>اختر النوع ثم أنشئ أرقامًا متتالية لهذا الطابق.</small>
                            </div>
                        </div>

                        <div class="details-floor-generator__grid details-floor-generator__grid--plan">
                            <label class="details-floor-generator__type">
                                <span>النوع</span>
                                <select class="form-select" data-plan-range-room>
                                    ${roomTypeOptionsHtml(rooms, getDetailsRoomId(rooms?.[0]))}
                                </select>
                            </label>

                            <label><span>من</span><input class="form-control" data-plan-range-start inputmode="numeric" placeholder="101"></label>
                            <label><span>إلى</span><input class="form-control" data-plan-range-end inputmode="numeric" placeholder="110"></label>
                            <label><span>بادئة</span><input class="form-control" data-plan-range-prefix maxlength="12" placeholder="B"></label>

                            <button type="button" class="details-add-button" data-generate-plan-range>
                                <i class="bi bi-plus-circle"></i> إنشاء
                            </button>
                        </div>
                    </section>

                    <div class="details-floor-units-toolbar">
                        <div>
                            <strong>الغرف الفعلية في الطابق</strong>
                            <small>كل وحدة تحتفظ بمعرفها عند التعديل، لذلك لا يتم إنشاؤها من جديد.</small>
                        </div>
                        <button type="button" class="details-add-button details-add-button--soft" data-add-plan-unit>
                            <i class="bi bi-plus-lg"></i> غرفة يدوية
                        </button>
                    </div>

                    <div class="details-floor-plan-units" data-plan-units>
                        ${(data.units || []).map(unit => floorPlanUnitRowHtml(unit, rooms)).join('')}
                    </div>
                </div>
            </article>`;
        }

        function buildHotelFloorPlanHtml(rooms) {
            const floors = groupHotelFloorPlanUnits(flattenHotelFloorUnits(rooms));

            return `<div class="details-hotel-floor-plan" data-hotel-floor-plan>
                <div class="details-floor-plan-intro">
                    <span><i class="bi bi-building-check"></i></span>
                    <div>
                        <strong>الطوابق والغرف الفعلية</strong>
                        <small>الطابق يتبع الفندق، وكل غرفة فعلية ترتبط بنوعها. هذا يطابق منطق إضافة المنشأة.</small>
                    </div>
                </div>

                <div class="details-floor-plan-toolbar">
                    <div>
                        <b>${rooms.length.toLocaleString('ar-YE')}</b>
                        <span>أنواع متاحة للتوزيع</span>
                    </div>
                    <button type="button" class="details-add-button" data-add-plan-floor>
                        <i class="bi bi-plus-lg"></i> إضافة طابق
                    </button>
                </div>

                <div class="details-floor-cards" data-plan-floor-cards>
                    ${floors.map((floor, index) => floorPlanCardHtml(floor, index, rooms, true)).join('')}
                </div>
            </div>`;
        }

        function initHotelFloorPlanEditor(rooms) {
            const root = document.querySelector('[data-hotel-floor-plan]');
            if (!root || root.dataset.ready === 'true') return;
            root.dataset.ready = 'true';

            const cards = root.querySelector('[data-plan-floor-cards]');

            // FIX: رقم الطابق الجديد يعتمد على أكبر FloorNumber موجود فعليًا،
            // وليس على عدد البطاقات. مثال: الطوابق 0 و 2 و 5 => التالي 6.
            const existingFloorNumbers = Array.from(
                cards.querySelectorAll('[data-plan-floor-number]')
            )
                .map(input => Number(input.value))
                .filter(value => Number.isInteger(value));

            let sequence = existingFloorNumbers.length
                ? Math.max(...existingFloorNumbers, 0) + 1
                : 1;

            const updateFloor = card => {
                const units = card.querySelector('[data-plan-units]');
                const numberInput = card.querySelector('[data-plan-floor-number]');
                const labelInput = card.querySelector('[data-plan-floor-label]');
                const count = units.querySelectorAll('[data-plan-unit]').length;
                const label = labelInput.value.trim();
                const number = Number(numberInput.value || 0);

                card.querySelector('[data-plan-floor-count]').textContent = count.toLocaleString('ar-YE');
                card.querySelector('[data-plan-floor-title]').textContent =
                    label || (number === 0 ? 'الدور الأرضي' : `الطابق ${number}`);
            };

            const bindUnit = (row, floorCard) => {
                row.querySelector('[data-remove-plan-unit]')?.addEventListener('click', () => {
                    row.remove();
                    updateFloor(floorCard);
                });
            };

            const bindFloor = card => {
                const units = card.querySelector('[data-plan-units]');

                units.querySelectorAll('[data-plan-unit]').forEach(row => bindUnit(row, card));

                card.querySelector('[data-plan-floor-toggle]')?.addEventListener('click', () => {
                    card.classList.toggle('is-collapsed');
                    const expanded = !card.classList.contains('is-collapsed');
                    card.querySelector('[data-plan-floor-toggle]').setAttribute('aria-expanded', String(expanded));
                });

                card.querySelector('[data-remove-plan-floor]')?.addEventListener('click', () => card.remove());
                card.querySelector('[data-plan-floor-number]')?.addEventListener('input', () => updateFloor(card));
                card.querySelector('[data-plan-floor-label]')?.addEventListener('input', () => updateFloor(card));

                card.querySelector('[data-add-plan-unit]')?.addEventListener('click', () => {
                    units.insertAdjacentHTML('beforeend', floorPlanUnitRowHtml({}, rooms));
                    bindUnit(units.lastElementChild, card);
                    card.classList.remove('is-collapsed');
                    updateFloor(card);
                    units.lastElementChild.querySelector('[data-plan-field="RoomNumber"]')?.focus();
                });

                card.querySelector('[data-generate-plan-range]')?.addEventListener('click', () => {
                    const startRaw = card.querySelector('[data-plan-range-start]').value.trim();
                    const endRaw = card.querySelector('[data-plan-range-end]').value.trim();
                    const prefix = card.querySelector('[data-plan-range-prefix]').value.trim();
                    const roomId = Number(card.querySelector('[data-plan-range-room]').value || 0);
                    const start = Number(startRaw);
                    const end = Number(endRaw);

                    if (!roomId) {
                        Swal.showValidationMessage('اختر نوع الغرفة أو الجناح.');
                        return;
                    }

                    if (!Number.isInteger(start) || !Number.isInteger(end) || start > end || end - start > 500) {
                        Swal.showValidationMessage('أدخل نطاقًا صحيحًا بحد أقصى 501 غرفة في المرة الواحدة.');
                        return;
                    }

                    const allCurrentNumbers = new Set(
                        Array.from(root.querySelectorAll('[data-plan-field="RoomNumber"]'))
                            .map(input => input.value.trim().toLowerCase())
                            .filter(Boolean)
                    );

                    const pad = Math.max(startRaw.length, endRaw.length);

                    for (let number = start; number <= end; number += 1) {
                        const roomNumber = prefix + String(number).padStart(pad, '0');
                        const normalized = roomNumber.toLowerCase();

                        if (allCurrentNumbers.has(normalized)) continue;
                        allCurrentNumbers.add(normalized);

                        units.insertAdjacentHTML('beforeend', floorPlanUnitRowHtml({
                            roomId,
                            roomNumber,
                            isAvailable: true
                        }, rooms));
                        bindUnit(units.lastElementChild, card);
                    }

                    card.classList.remove('is-collapsed');
                    updateFloor(card);
                });

                updateFloor(card);
            };

            cards.querySelectorAll('[data-plan-floor]').forEach(bindFloor);

            root.querySelector('[data-add-plan-floor]')?.addEventListener('click', () => {
                cards.insertAdjacentHTML('beforeend', floorPlanCardHtml({
                    floorNumber: sequence++,
                    floorLabel: '',
                    units: []
                }, sequence, rooms, true));

                bindFloor(cards.lastElementChild);
                cards.lastElementChild.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            });
        }

        function collectHotelFloorPlan() {
            const root = document.querySelector('[data-hotel-floor-plan]');
            if (!root) return { error: 'تعذر قراءة مخطط الطوابق.' };

            const units = [];
            const numbers = new Set();
            const floorNumbers = new Set();

            for (const floor of root.querySelectorAll('[data-plan-floor]')) {
                const floorNumber = Number(floor.querySelector('[data-plan-floor-number]')?.value || 0);
                const floorLabel = floor.querySelector('[data-plan-floor-label]')?.value.trim() || '';

                if (!Number.isInteger(floorNumber) || floorNumber < -100 || floorNumber > 1000) {
                    return { error: 'رقم أحد الطوابق غير صحيح.' };
                }

                // FIX: لا نسمح بوجود بطاقتي طابق بنفس FloorNumber.
                if (floorNumbers.has(floorNumber)) {
                    return { error: `رقم الطابق (${floorNumber}) مكرر.` };
                }
                floorNumbers.add(floorNumber);

                for (const row of floor.querySelectorAll('[data-plan-unit]')) {
                    const roomUnitId = Number(row.querySelector('[data-plan-field="RoomUnitId"]')?.value || 0);
                    const roomId = Number(row.querySelector('[data-plan-field="RoomId"]')?.value || 0);
                    const roomNumber = row.querySelector('[data-plan-field="RoomNumber"]')?.value.trim() || '';
                    const notes = row.querySelector('[data-plan-field="Notes"]')?.value.trim() || '';
                    const isAvailable = !!row.querySelector('[data-plan-field="IsAvailable"]')?.checked;

                    if (!roomId) return { error: 'اختر نوعًا لكل غرفة فعلية.' };
                    if (!roomNumber) return { error: 'رقم/رمز كل غرفة فعلية مطلوب.' };

                    const normalized = roomNumber.toLowerCase();
                    if (numbers.has(normalized)) return { error: `رقم الوحدة (${roomNumber}) مكرر داخل الفندق.` };
                    numbers.add(normalized);

                    units.push({
                        roomUnitId,
                        roomId,
                        roomNumber,
                        floorNumber,
                        floorLabel,
                        isAvailable,
                        notes
                    });
                }
            }

            return {
                data: {
                    placeId: Number(window.activeDetailsPlaceId || 0),
                    units
                }
            };
        }


        function initFastSmoothFloorPlanScroll() {
            if (!window.matchMedia('(min-width: 769px)').matches) return;

            const popup = Swal.getPopup();
            if (!popup) return;

            const scroller =
                popup.querySelector('.details-modal-html') ||
                popup.querySelector('.swal2-html-container');

            if (!scroller || scroller.dataset.fastFloorScroll === 'true') return;
            scroller.dataset.fastFloorScroll = 'true';

            let target = scroller.scrollTop;
            let frame = 0;

            const clampTarget = value => {
                const max = Math.max(0, scroller.scrollHeight - scroller.clientHeight);
                return Math.max(0, Math.min(max, value));
            };

            const animate = () => {
                if (!scroller.isConnected) {
                    frame = 0;
                    return;
                }

                const diff = target - scroller.scrollTop;

                if (Math.abs(diff) < 0.7) {
                    scroller.scrollTop = target;
                    frame = 0;
                    return;
                }

                // استجابة أسرع من السكرول العادي مع بقاء الحركة ناعمة.
                scroller.scrollTop += diff * 0.34;
                frame = requestAnimationFrame(animate);
            };

            scroller.addEventListener('wheel', event => {
                // لا نتدخل عند Ctrl + Wheel حتى يبقى Zoom المتصفح طبيعيًا.
                if (event.ctrlKey) return;

                const max = Math.max(0, scroller.scrollHeight - scroller.clientHeight);
                if (max <= 0) return;

                event.preventDefault();

                // تسريع عجلة الماوس مع حد يمنع القفز العنيف.
                const acceleratedDelta = Math.max(-420, Math.min(420, event.deltaY * 1.55));
                target = clampTarget(target + acceleratedDelta);

                if (!frame) {
                    frame = requestAnimationFrame(animate);
                }
            }, { passive: false });

            // إذا سحب المستخدم شريط التمرير يدويًا، نزامن الهدف مع مكانه الجديد.
            scroller.addEventListener('scroll', () => {
                if (!frame) target = scroller.scrollTop;
            }, { passive: true });
        }

        window.openManageHotelFloorsModal = function () {
            const rooms = getDetailsRooms();
            if (!rooms.length) {
                Swal.fire({
                    icon: 'info',
                    title: 'أضف نوعًا أولًا',
                    text: 'يجب إضافة نوع غرفة أو جناح قبل إنشاء الطوابق والغرف الفعلية.'
                });
                return;
            }

            Swal.fire({
                title: '<h5 class="fw-bold text-primary m-0">إدارة الطوابق والغرف الفعلية</h5>',
                html: `<div class="details-modal-body text-start" dir="rtl">${buildHotelFloorPlanHtml(rooms)}</div>`,
                showCloseButton: true,
                allowOutsideClick: false,
                showCancelButton: true,
                confirmButtonText: 'حفظ توزيع الغرف',
                cancelButtonText: 'إلغاء',
                customClass: {
                    popup: 'details-luxury-modal details-luxury-modal--wide details-floor-plan-modal',
                    confirmButton: 'details-modal-confirm',
                    cancelButton: 'details-modal-cancel',
                    htmlContainer: 'details-modal-html',
                    actions: 'details-modal-actions'
                },
                buttonsStyling: false,
                didOpen: () => {
                    initHotelFloorPlanEditor(rooms);
                    initFastSmoothFloorPlanScroll();
                },
                preConfirm: async () => {
                    const collected = collectHotelFloorPlan();
                    if (collected.error) {
                        Swal.showValidationMessage(collected.error);
                        return false;
                    }

                    try {
                        const response = await fetch('/Owner/Place/SaveHotelFloorPlan', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(collected.data)
                        });

                        const result = await response.json();
                        if (!result.success) {
                            Swal.showValidationMessage(result.message || 'تعذر حفظ الطوابق.');
                            return false;
                        }

                        return result;
                    } catch (error) {
                        Swal.showValidationMessage('فشل الاتصال بالخادم.');
                        return false;
                    }
                }
            }).then(result => {
                if (result.isConfirmed && result.value) {
                    Swal.mixin({ toast: true, timer: 1700 }).fire({ icon: 'success', title: result.value.message });
                    refreshDetailsView(window.activeDetailsPlaceId);
                }
            });
        };

        window.openAddModal = function (type) {
            const placeId = window.activeDetailsPlaceId;
            if (!placeId) return;

            if (type === 'room') {
                window.openAddRoomTypeModal?.();
                return;
            }

            let title = '', endpoint = '', fields = '';

            if (type === 'room') {
                title = 'إضافة غرفة أو جناح وتنظيم وحداته'; endpoint = '/Owner/Place/AddRoom';
                fields = buildPremiumHotelRoomEditorHtml('add-room', null);

            } else if (type === 'package') {
                title = 'إضافة باقة'; endpoint = '/Owner/Place/AddPlacePackage';
                fields = buildPackageEditorHtml(null);
            } else if (type === 'service') {
                title = 'إضافة خدمة'; endpoint = '/Owner/Place/AddAdditionalService';
                fields = buildServiceEditorHtml(null, '');
            } else if (type === 'amenity') {
                title = 'إضافة مرفق'; endpoint = '/Owner/Place/AddPlaceAmenity';
                fields = buildAmenityEditorHtml('', '', false);
            }

            Swal.fire({
                title: `<h5 class="fw-bold text-primary m-0">${title}</h5>`,
                html: `<div class="details-modal-body text-start" dir="rtl">${fields}</div>`,
                showCloseButton: true, allowOutsideClick: false, showCancelButton: true, confirmButtonText: 'حفظ', cancelButtonText: 'إلغاء',
                confirmButtonColor: '#00A6DA', cancelButtonColor: '#6c757d',
                customClass: {
                    popup: type === 'room' ? 'details-luxury-modal details-luxury-modal--wide' : 'details-luxury-modal details-luxury-modal--compact',
                    confirmButton: 'details-modal-confirm',
                    cancelButton: 'details-modal-cancel',
                    htmlContainer: 'details-modal-html',
                    actions: 'details-modal-actions'
                },
                buttonsStyling: false,
                didOpen: () => {
                    if (type === 'room') initPremiumHotelRoomEditor('add-room');
                    if (type === 'package') initPackageEditor();
                },
                preConfirm: async () => {
                    if (type === 'room' || type === 'service') {
                        const formData = new FormData();
                        formData.append('PlaceId', parseInt(placeId));

                        if (type === 'room') {
                            const collected = collectPremiumHotelRoomForm('add-room', parseInt(placeId), 0);
                            if (collected.error) { Swal.showValidationMessage(collected.error); return false; }
                            try {
                                const response = await fetch(endpoint, { method: 'POST', body: collected.data });
                                const res = await response.json();
                                if (!res.success) { Swal.showValidationMessage(res.message); return false; }
                                return res;
                            } catch (error) { Swal.showValidationMessage('فشل الاتصال بالخادم.'); return false; }
                        }
                        else if (type === 'service') {
                            const popup = Swal.getPopup();
                            const nameVal = popup.querySelector('#edit-name').value;
                            const priceVal = popup.querySelector('#edit-price').value;
                            const descVal = popup.querySelector('#edit-desc').value;

                            if (!nameVal || !priceVal) { Swal.showValidationMessage('يرجى تعبئة الحقول الإجبارية!'); return false; }

                            const normalizedInput = window.normalizeArabic(nameVal);
                            const isDuplicate = window.currentPlaceData?.services?.some(s => window.normalizeArabic(s.serviceName || s.ServiceName) === normalizedInput);
                            if (isDuplicate) { Swal.showValidationMessage('هذه الخدمة موجودة مسبقاً!'); return false; }

                            formData.append('ServiceName', nameVal);
                            formData.append('Price', parseFloat(priceVal));
                            formData.append('Description', descVal);

                            const imageInput = popup.querySelector('#edit-image');
                            if (imageInput && imageInput.files.length > 0) {
                                for (let i = 0; i < imageInput.files.length; i++) {
                                    if (imageInput.files[i].size > 5 * 1024 * 1024) {
                                        Swal.showValidationMessage('عذراً، حجم إحدى الصور وحدها يتجاوز 5 ميجابايت!');
                                        return false;
                                    }
                                    formData.append('ImageFiles', imageInput.files[i]);
                                }
                            }
                        }

                        try {
                            const response = await fetch(endpoint, { method: 'POST', body: formData });
                            const res = await response.json();
                            if (!res.success) { Swal.showValidationMessage(res.message); return false; }
                            return res;
                        } catch (error) { Swal.showValidationMessage('فشل الاتصال بالخادم.'); return false; }
                    }

                    const data = { PlaceID: parseInt(placeId) };

                    if (type === 'package') {
                        const collected = collectPackageEditor(placeId, 0);
                        if (collected.error) {
                            Swal.showValidationMessage(collected.error);
                            return false;
                        }
                        Object.assign(data, collected.data);
                    } else if (type === 'amenity') {
                        data.AmenityName = document.getElementById('edit-name').value;
                        data.IconClass = document.getElementById('edit-icon').value;
                        if (!data.AmenityName) { Swal.showValidationMessage('يرجى إدخال اسم المرفق!'); return false; }
                    }

                    try {
                        const response = await fetch(endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
                        const res = await response.json();
                        if (!res.success) { Swal.showValidationMessage(res.message); return false; }
                        return res;
                    } catch (error) { Swal.showValidationMessage('فشل الاتصال بالخادم.'); return false; }
                }
            }).then(r => {
                if (r.isConfirmed && r.value) { Swal.mixin({ toast: true, timer: 1500 }).fire({ icon: 'success', title: r.value.message }); refreshDetailsView(placeId); }
            });
        };










        window.deleteRoomImageDirect = function (roomId, imagePath, idx) {
            window.elegantConfirm('هل أنت متأكد من مسح هذه الصورة نهائياً؟', function () {
                const imgBox = document.getElementById(`room-img-${roomId}-${idx}`);
                if (imgBox) imgBox.style.opacity = '0.5';

                const url = `/Owner/Place/DeleteRoomImage?roomId=${roomId}&imagePath=${encodeURIComponent(imagePath)}`;

                fetch(url, { method: 'POST' })
                    .then(r => r.json()).then(data => {
                        if (data.success) {
                            if (imgBox) imgBox.remove();

                            if (window.currentPlaceData && window.currentPlaceData.rooms) {
                                const rm = window.currentPlaceData.rooms.find(r => r.roomId == roomId || r.RoomId == roomId);
                                if (rm && rm.imageUrl) {
                                    let imgs = rm.imageUrl.split(',');
                                    rm.imageUrl = imgs.filter(i => i !== imagePath).join(',');
                                }
                            }

                            // 🌟 التحديث الديناميكي للرسالة في نفس النافذة دون إغلاقها
                            const validationMessage = Swal.getValidationMessage();
                            if (validationMessage && validationMessage.style.display !== 'none') {
                                let currentExistingMb = data.currentSizeMb || 0;
                                let newFilesSize = 0;
                                const newImageInput = document.getElementById('edit-room-images');
                                if (newImageInput && newImageInput.files) {
                                    for (let i = 0; i < newImageInput.files.length; i++) {
                                        newFilesSize += newImageInput.files[i].size;
                                    }
                                }
                                let newMb = (newFilesSize / (1024 * 1024)).toFixed(2);
                                let maxMb = 5.0;
                                let availableMb = (maxMb - currentExistingMb).toFixed(2);
                                if (availableMb < 0) availableMb = 0;
                                let grandTotalMb = parseFloat(currentExistingMb) + parseFloat(newMb);

                                if (grandTotalMb > maxMb) {
                                    Swal.showValidationMessage(`عذراً! مساحة الصور القديمة لديك (${currentExistingMb} ميجا). والمتبقي لك هو (${availableMb} ميجا) فقط، بينما الصور الجديدة حجمها (${newMb} ميجا). يرجى حذف صور قديمة من علامة ❌ لتفريغ مساحة.`);
                                    const popup = Swal.getPopup();
                                    if (popup) popup.scrollTo({ top: popup.scrollHeight, behavior: 'smooth' });
                                } else {
                                    Swal.resetValidationMessage();
                                    window.showSafeToast('المساحة أصبحت كافية! يمكنك الآن الضغط على (حفظ التعديل).', 'success');
                                }
                            } else {
                                window.showSafeToast('تم مسح الصورة بنجاح.', 'success');
                            }
                        } else {
                            if (imgBox) imgBox.style.opacity = '1';
                            window.showSafeToast(data.message, 'error');
                        }
                    }).catch(err => {
                        if (imgBox) imgBox.style.opacity = '1';
                        window.showSafeToast('حدث خطأ في الاتصال بالخادم.', 'error');
                    });
            });
        };








        window.openEditServiceModal = function (id) {
            const service = window.currentPlaceData?.services?.find(s => (s.serviceID || s.serviceId || s.ServiceID || Object.values(s)[0]) == id);

            const sName = service ? (service.serviceName || service.ServiceName) : '';
            const sPrice = service ? (service.price || service.Price) : 0;
            const sDesc = service ? (service.description || service.Description || '') : '';

            let images = [];
            const rawImageUrl = service ? (service.imageUrl || service.ImageUrl) : null;
            if (rawImageUrl) {
                images = rawImageUrl.split(',').filter(i => i.trim() !== '');
            }

            let existingImagesHtml = '';
            if (images.length > 0) {
                existingImagesHtml = `
                <div class="details-current-media">
                    <div class="details-current-media__title">
                        <div><i class="bi bi-images"></i><span><strong>الصور الحالية</strong><small>احذفي الصورة غير المطلوبة من زر ×.</small></span></div>
                        <b>${images.length.toLocaleString('ar-YE')}</b>
                    </div>
                    <div class="details-existing-images" id="service-images-container-${id}">`;

                images.forEach((img, idx) => {
                    existingImagesHtml += `
                    <div class="details-current-media__item" id="srv-img-${id}-${idx}">
                        <img src="${escapePackageHtml(img)}" alt="صورة الخدمة">
                        <button type="button" aria-label="حذف الصورة"
                                onclick="window.deleteServiceImageDirect(${id}, '${String(img).replace(/'/g, "\\'")}', ${idx})"
                                onkeydown="if(event.key === 'Enter') { event.stopPropagation(); }">
                            <i class="bi bi-x-lg"></i>
                        </button>
                    </div>`;
                });
                existingImagesHtml += `</div></div>`;
            }

            Swal.fire({
                title: `<h5 class="fw-bold text-primary m-0">تعديل خدمة</h5>`,
                showCloseButton: true, allowOutsideClick: false, confirmButtonColor: '#00A6DA', cancelButtonColor: '#6c757d',
                html: `<div class="details-modal-body text-start" dir="rtl">${buildServiceEditorHtml(service, existingImagesHtml)}</div>`,

                showCancelButton: true, confirmButtonText: 'حفظ التعديلات', cancelButtonText: 'إلغاء',
                customClass: {
                    popup: 'details-luxury-modal details-luxury-modal--compact',
                    confirmButton: 'details-modal-confirm',
                    cancelButton: 'details-modal-cancel',
                    htmlContainer: 'details-modal-html',
                    actions: 'details-modal-actions'
                },
                buttonsStyling: false,

                preConfirm: async () => {
                    const name = document.getElementById('edit-name').value;
                    const price = document.getElementById('edit-price').value;
                    const desc = document.getElementById('edit-desc').value;

                    if (!name || !price) { Swal.showValidationMessage('يرجى تعبئة الحقول الإجبارية!'); return false; }

                    const normalizedInput = window.normalizeArabic(name);
                    const isDuplicate = window.currentPlaceData?.services?.some(s => {
                        if (id && (s.serviceID || s.serviceId || s.ServiceID || Object.values(s)[0]) == id) return false;
                        return window.normalizeArabic(s.serviceName || s.ServiceName) === normalizedInput;
                    });
                    if (isDuplicate) { Swal.showValidationMessage('هذه الخدمة موجودة مسبقاً في قائمتك!'); return false; }

                    const formData = new FormData();
                    formData.append('ServiceId', id);
                    formData.append('ServiceName', name);
                    formData.append('Price', parseFloat(price) || 0);
                    formData.append('Description', desc);

                    const imageInput = document.getElementById('edit-image');
                    if (imageInput && imageInput.files.length > 0) {



                        for (let i = 0; i < imageInput.files.length; i++) {
                            if (imageInput.files[i].size > 5 * 1024 * 1024) {
                                Swal.showValidationMessage('عذراً، حجم إحدى الصور يتجاوز 5 ميجابايت!');
                                return false;
                            }
                            formData.append('ImageFiles', imageInput.files[i]);
                        }
                    }

                    try {
                        const response = await fetch('/Owner/Place/EditAdditionalService', { method: 'POST', body: formData });
                        const res = await response.json();
                        if (!res.success) {
                            Swal.showValidationMessage(res.message);
                            return false;
                        }
                        return res;
                    } catch (error) {
                        Swal.showValidationMessage('فشل الاتصال بالخادم.');
                        return false;
                    }
                }
            }).then(r => {
                if (r.isConfirmed && r.value) {
                    Swal.mixin({ toast: true, timer: 1500 }).fire({ icon: 'success', title: r.value.message });
                    refreshDetailsView(window.activeDetailsPlaceId);
                }
            });
        };



        // 🌟 1. دالة الإشعار الأنيق المستقل (لا تغلق الفورم أبداً)
        window.showSafeToast = function (msg, type = 'success') {
            const toast = document.createElement('div');
            const bgColor = type === 'error' ? '#dc3545' : (type === 'info' ? '#0dcaf0' : '#198754');
            toast.style.cssText = `position:fixed; top:20px; right:20px; background:${bgColor}; color:#fff; padding:12px 25px; border-radius:8px; z-index:9999999; font-weight:bold; box-shadow:0 4px 15px rgba(0,0,0,0.2); transition: opacity 0.3s; direction: rtl;`;
            toast.innerHTML = msg;
            document.body.appendChild(toast);
            // إخفاء الإشعار بعد ثانيتين ونصف
            setTimeout(() => { toast.style.opacity = '0'; setTimeout(() => toast.remove(), 300); }, 2500);
        };

        // 🌟 دالة الحذف المحدثة (آمنة تماماً ولا تغلق الفورم)
        window.deleteServiceImageDirect = function (serviceId, imagePath, idx) {
            window.elegantConfirm('هل أنتِ متأكدة من مسح هذه الصورة لتفريغ مساحة؟', function () {
                const formData = new FormData();
                formData.append('serviceId', serviceId);
                formData.append('imagePath', imagePath);

                const imgBox = document.getElementById(`srv-img-${serviceId}-${idx}`);
                if (imgBox) imgBox.style.opacity = '0.5';

                fetch('/Owner/Place/DeleteServiceImage', { method: 'POST', body: formData })
                    .then(r => r.json()).then(result => {
                        if (result.success) {

                            // 1. إخفاء الصورة المحذوفة من الشاشة
                            if (imgBox) imgBox.remove();

                            // 2. تحديث البيانات في الخلفية
                            if (window.currentPlaceData && window.currentPlaceData.services) {
                                const srv = window.currentPlaceData.services.find(s => s.serviceID == serviceId || s.ServiceId == serviceId);
                                if (srv) {
                                    let imgs = srv.imageUrl.split(',');
                                    srv.imageUrl = imgs.filter(i => i !== imagePath).join(',');
                                }
                            }


                            // 🌟 3. السحر هنا: إخفاء الخطأ الأحمر بهدوء وترك القرار للمستخدم
                            const validationMessage = Swal.getValidationMessage();
                            if (validationMessage && validationMessage.textContent !== '') {
                                Swal.resetValidationMessage(); // إخفاء الرسالة الحمراء فوراً
                                window.showSafeToast('تم مسح الصورة بنجاح!    .', 'success');
                            } else {
                                window.showSafeToast('تم مسح الصورة بنجاح.', 'success');
                            }

                        } else {
                            if (imgBox) imgBox.style.opacity = '1';
                            window.showSafeToast(result.message, 'error');
                        }
                    }).catch(err => {
                        if (imgBox) imgBox.style.opacity = '1';
                        window.showSafeToast('حدث خطأ في الاتصال بالخادم.', 'error');
                    });
            });
        };




        window.openEditPackageModal = function (id) {
            const pkg = window.currentPlaceData?.packages?.find(
                p => (p.packageID || p.packageId || p.PackageID || Object.values(p)[0]) == id
            );

            if (!pkg) {
                Swal.fire('تنبيه', 'تعذر العثور على بيانات الباقة. حدّثي الصفحة وحاولي مجددًا.', 'warning');
                return;
            }

            Swal.fire({
                title: '<h5><i class="bi bi-pencil-square"></i> تعديل باقة</h5>',
                showCloseButton: true,
                allowOutsideClick: false,
                html: `<div class="details-modal-body text-start" dir="rtl">${buildPackageEditorHtml(pkg)}</div>`,
                showCancelButton: true,
                confirmButtonText: 'حفظ التعديل',
                cancelButtonText: 'إلغاء',
                customClass: {
                    popup: 'details-luxury-modal details-luxury-modal--compact',
                    confirmButton: 'details-modal-confirm',
                    cancelButton: 'details-modal-cancel',
                    htmlContainer: 'details-modal-html',
                    actions: 'details-modal-actions'
                },
                buttonsStyling: false,
                didOpen: () => initPackageEditor(),
                preConfirm: () => {
                    const collected = collectPackageEditor(window.activeDetailsPlaceId, id);
                    if (collected.error) {
                        Swal.showValidationMessage(collected.error);
                        return false;
                    }
                    return collected.data;
                }
            }).then(result => {
                if (!result.isConfirmed || !result.value) return;

                fetch('/Owner/Place/EditPlacePackage', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(result.value)
                })
                    .then(res => res.json())
                    .then(res => {
                        if (res.success) {
                            Swal.mixin({ toast: true, timer: 1500 }).fire({
                                icon: 'success',
                                title: res.message
                            });
                            refreshDetailsView(window.activeDetailsPlaceId);
                        } else {
                            Swal.fire('خطأ', res.message, 'error');
                        }
                    });
            });
        };

        window.togglePackageStatus = function (id, event) {
            event?.preventDefault();
            event?.stopPropagation();

            const button = event?.currentTarget;
            if (button) button.disabled = true;

            fetch(`/Owner/Place/TogglePlacePackage?id=${encodeURIComponent(id)}`, { method: 'POST' })
                .then(async response => {
                    const result = await response.json();
                    if (!response.ok || !result.success) {
                        throw new Error(result.message || 'تعذر تحديث حالة الباقة.');
                    }
                    window.showSafeToast(result.message, 'success');
                    return refreshDetailsView(window.activeDetailsPlaceId);
                })
                .catch(error => {
                    if (button) button.disabled = false;
                    window.showSafeToast(error.message || 'حدث خطأ أثناء تحديث الباقة.', 'error');
                });
        };

        window.openEditAmenityModal = function (id) {
            const amenity = window.currentPlaceData?.amenities?.find(a => (a.amenityID || a.amenityId || a.AmenityID || a.id || Object.values(a)[0]) == id);
            const currentName = amenity ? (amenity.amenityName || amenity.AmenityName) : '';
            const currentIcon = amenity ? (amenity.iconClass || amenity.IconClass) : 'bi-patch-check-fill';

            Swal.fire({
                title: `<h5 class="fw-bold text-primary m-0">تعديل مرفق</h5>`, showCloseButton: true, allowOutsideClick: false, confirmButtonColor: '#00A6DA', cancelButtonColor: '#6c757d',
                customClass: {
                    popup: 'details-luxury-modal details-luxury-modal--compact',
                    confirmButton: 'details-modal-confirm',
                    cancelButton: 'details-modal-cancel',
                    htmlContainer: 'details-modal-html',
                    actions: 'details-modal-actions'
                },
                buttonsStyling: false,
                html: `<div class="details-modal-body text-start" dir="rtl">${buildAmenityEditorHtml(currentName, currentIcon, true)}</div>`,
                showCancelButton: true, confirmButtonText: 'حفظ', cancelButtonText: 'إلغاء',
                preConfirm: () => {
                    const name = document.getElementById('edit-name').value;
                    if (!name) { Swal.showValidationMessage('يرجى إدخال اسم المرفق!'); return false; }

                    const normalizedInput = window.normalizeArabic(name);
                    const isDuplicate = window.currentPlaceData?.amenities?.some(a => {
                        if (id && (a.amenityID || a.amenityId || a.AmenityID || a.id || Object.values(a)[0]) == id) return false;
                        return window.normalizeArabic(a.amenityName || a.AmenityName) === normalizedInput;
                    });
                    if (isDuplicate) { Swal.showValidationMessage('هذا المرفق موجود مسبقاً في قائمتك!'); return false; }

                    return { AmenityID: id, AmenityId: id, AmenityName: name, IconClass: document.getElementById('edit-icon').value }
                }
            }).then(r => { if (r.isConfirmed) { fetch('/Owner/Place/EditPlaceAmenity', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(r.value) }).then(res => res.json()).then(res => { if (res.success) { Swal.mixin({ toast: true, timer: 1500 }).fire({ icon: 'success', title: res.message }); refreshDetailsView(window.activeDetailsPlaceId); } else Swal.fire('خطأ', res.message, 'error'); }); } });
        };

        // 🌟 دالة حذف الوحدات والخدمات (بتأثير التلاشي الناعم)
        window.deleteItem = function (type, id, placeId) {
            window.elegantConfirm('هل أنت متأكد من الحذف؟ لن يمكن التراجع.', function () {
                let endpoint = type === 'package' ? `/Owner/Place/DeletePlacePackage?id=${id}` : type === 'service' ? `/Owner/Place/DeleteAdditionalService?id=${id}` : type === 'room' ? `/Owner/Place/DeleteRoom?id=${id}` : `/Owner/Place/DeletePlaceAmenity?id=${id}`;

                // 1. تحديد كرت الخدمة/الغرفة لعمل تأثير التلاشي فوراً
                const btn = document.querySelector(`button[onclick*="deleteItem('${type}', ${id}"]`) || document.querySelector(`button[onclick*="deleteItem('${type}',${id}"]`);
                let card = null;
                if (btn) {
                    card = btn.closest('.service-item') || btn.closest('.amenity-item') || btn.closest('.package-item') || btn.closest('.room-item') || btn.closest('.col-md-4') || btn.closest('.col-md-6') || btn.closest('li');
                    if (card) {
                        card.style.transition = "all 0.4s ease";
                        card.style.transform = "scale(0.8)";
                        card.style.opacity = "0";
                    }
                }

                fetch(endpoint, { method: 'POST' }).then(res => res.json()).then(res => {
                    if (res.success) {
                        window.showSafeToast('تم الحذف بنجاح.', 'success');
                        if (card) {
                            setTimeout(() => card.remove(), 400);
                            // تحديث البيانات في الخلفية بصمت تام بدون أي رمشة
                            fetch(`/Owner/Place/GetPlaceSubItems?placeId=${placeId}`).then(r => r.json()).then(d => { window.currentPlaceData = d; });
                        } else {
                            refreshDetailsView(placeId); // احتياطي
                        }
                    } else {
                        if (card) { card.style.transform = "scale(1)"; card.style.opacity = "1"; }
                        window.showSafeToast(res.message, 'error');
                    }
                });
            });
        };

    }

    // =========================================================================
    // 📁 القسم الثامن: دوال إضافية خارجية (Tags, AutoSuggest, Images)
    // =========================================================================

    window.addDynamicTag = function (btn) {
        if (!btn) return;
        const container = btn.closest('.dynamic-field-container') || btn.closest('.input-group').parentElement;
        const inputGroup = btn.closest('.input-group');
        const input = inputGroup.querySelector('input');
        const val = input.value.trim();

        if (val === '') {
            input.focus();
            return;
        }

        let tagsContainer = container.querySelector('.tags-container');
        if (!tagsContainer) {
            tagsContainer = document.createElement('div');
            tagsContainer.className = 'tags-container mb-2 d-flex flex-wrap gap-2';
            container.insertBefore(tagsContainer, inputGroup);
        }

        const tag = document.createElement('span');
        tag.className = 'badge bg-primary text-white p-2 d-flex align-items-center shadow-sm';
        tag.style.fontSize = '14px';
        tag.style.animation = "fadeIn 0.3s";
        tag.innerHTML = `
${val}
<i class="bi bi-x-circle-fill ms-2 text-light" style="cursor: pointer; font-size: 15px;" onclick="this.parentElement.remove()" title="حذف"></i>
<input type="hidden" name="${input.name}" value="${val}">
`;

        tagsContainer.appendChild(tag);
        input.value = '';
        input.focus();
    };

    window.autoSuggestIcon = function (text) {
        if (!text) return;
        let icon = 'bi-patch-check-fill';
        const iconMap = {
            'مسبح': 'bi-water', 'ماء': 'bi-water', 'سباحة': 'bi-water', 'نافورة': 'bi-water',
            'انترنت': 'bi-wifi', 'واي فاي': 'bi-wifi', 'شبكة': 'bi-wifi', 'راوتر': 'bi-wifi',
            'مكيف': 'bi-snow', 'تكييف': 'bi-snow', 'تبريد': 'bi-snow', 'بارد': 'bi-thermometer-snow',
            'مروحة': 'bi-fan', 'تهوية': 'bi-fan',
            'موقف': 'bi-car-front', 'سيار': 'bi-p-circle', 'جراج': 'bi-p-circle',
            'شاش': 'bi-tv', 'تلفزيون': 'bi-tv', 'عرض': 'bi-display', 'بروجكتر': 'bi-projector',
            'سينما': 'bi-film', 'فيلم': 'bi-film',
            'مطبخ': 'bi-cup-hot', 'طبخ': 'bi-cup-hot', 'طعام': 'bi-shop', 'بوفيه': 'bi-shop', 'مطعم': 'bi-shop',
            'حمام': 'bi-droplet', 'دورة': 'bi-droplet', 'غسيل': 'bi-droplet',
            'سرير': 'bi-moon-stars', 'نوم': 'bi-moon-stars', 'غرف': 'bi-door-closed', 'باب': 'bi-door-closed',
            'حديق': 'bi-tree', 'شجر': 'bi-tree', 'عشب': 'bi-tree', 'ورد': 'bi-flower1', 'زهور': 'bi-flower2',
            'رياض': 'bi-bicycle', 'جيم': 'bi-bicycle', 'لياقة': 'bi-bicycle', 'دراجة': 'bi-bicycle',
            'قهو': 'bi-cup', 'كافيه': 'bi-cup', 'عصير': 'bi-cup-straw', 'مشروب': 'bi-cup-straw',
            'حارس': 'bi-shield-check', 'امن': 'bi-shield-check', 'حماية': 'bi-shield-check',
            'مصعد': 'bi-elevator', 'درج': 'bi-elevator',
            'كهرباء': 'bi-lightning', 'مولد': 'bi-lightning',
            'العاب': 'bi-controller', 'بلايستيشن': 'bi-controller', 'لعب': 'bi-puzzle', 'نرد': 'bi-dice-5',
            'نار': 'bi-fire', 'شواء': 'bi-fire', 'باربكيو': 'bi-fire', 'تدفئة': 'bi-thermometer-sun',
            'سماع': 'bi-speaker', 'صوت': 'bi-speaker', 'دي جي': 'bi-boombox', 'موسيقى': 'bi-music-note-beamed', 'مايك': 'bi-mic',
            'عائل': 'bi-people', 'جلسات': 'bi-people', 'اشخاص': 'bi-people',
            'فطور': 'bi-egg-fried', 'افطار': 'bi-egg-fried',
            'كيك': 'bi-cake', 'حفلة': 'bi-balloon', 'ميلاد': 'bi-cake', 'بالون': 'bi-balloon',
            'تسوق': 'bi-cart', 'سوبرماركت': 'bi-cart', 'بقالة': 'bi-basket',
            'ثلاجة': 'bi-refrigerator', 'تجميد': 'bi-refrigerator',
            'خزنة': 'bi-safe', 'امانات': 'bi-safe', 'دولاب': 'bi-archive', 'خزانة': 'bi-archive',
            'مصباح': 'bi-lamp', 'اضاءة': 'bi-lamp', 'لمبة': 'bi-lamp',
            'بنك': 'bi-bank', 'صراف': 'bi-credit-card', 'دفع': 'bi-credit-card', 'محفظة': 'bi-wallet2',
            'عيادة': 'bi-hospital', 'طبي': 'bi-bandaid', 'اسعاف': 'bi-bandaid',
            'شحن': 'bi-ev-station', 'محطة': 'bi-fuel-pump',
            'سكوتر': 'bi-scooter',
            'هدية': 'bi-gift', 'هدايا': 'bi-gift',
            'قمامة': 'bi-trash', 'نظافة': 'bi-trash', 'تدوير': 'bi-recycle',
            'تلفون': 'bi-telephone', 'هاتف': 'bi-telephone', 'استقبال': 'bi-bell', 'خدمة': 'bi-bell',
            'ساعة': 'bi-clock', 'وقت': 'bi-clock',
            'اعمال': 'bi-briefcase', 'اجتماع': 'bi-briefcase', 'مؤتمر': 'bi-briefcase',
            'عرسان': 'bi-suit-heart', 'رومانسي': 'bi-suit-heart', 'حب': 'bi-heart',
            'مميز': 'bi-stars', 'vip': 'bi-award', 'فخامة': 'bi-gem', 'ذهبي': 'bi-award',
            'ديكور': 'bi-palette', 'رسم': 'bi-palette',
            'حلاقة': 'bi-scissors', 'صالون': 'bi-scissors',
            'طباعة': 'bi-printer', 'طابعة': 'bi-printer',
            'صيانة': 'bi-tools', 'تصليح': 'bi-wrench', 'ادوات': 'bi-tools',
            'تصوير': 'bi-camera', 'كاميرا': 'bi-camera', 'فيديو': 'bi-camera-video',
            'رحلة': 'bi-compass', 'سفاري': 'bi-compass', 'خريطة': 'bi-map', 'موقع': 'bi-geo-alt',
            'مفتاح': 'bi-key', 'دخول': 'bi-door-closed',
            'لابتوب': 'bi-laptop', 'كمبيوتر': 'bi-laptop',
            'تذاكر': 'bi-ticket', 'تذكرة': 'bi-ticket', 'تخفيض': 'bi-tags', 'عروض': 'bi-tags',
            'ملعب': 'bi-trophy', 'بطولة': 'bi-trophy',
            'مظلة': 'bi-umbrella', 'شمسية': 'bi-umbrella'
        };

        for (const key in iconMap) { if (text.includes(key)) { icon = iconMap[key]; break; } }
        const targetElement = document.getElementById('icon-item-' + icon);
        if (targetElement) { window.selectIconGrid(icon, targetElement); targetElement.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); }
    };

    // تصدير دوال التهيئة للملف المركزي بعد الفصل
    window.initDetailsManager = initDetailsManager;

});



// =========================================================================
// 🚀 دالة الانتقال الذكي لصفحة السياسات (محدثة لتدعم وضع التعديل)
// =========================================================================
window.openEditPolicyModal = function (mode = 'new') {
    const placeId = window.activeDetailsPlaceId || null;

    if (!placeId) {
        Swal.fire('تنبيه', 'يرجى اختيار المنشأة أولاً!', 'warning');
        return;
    }

    window.location.href = `/Owner/Settings/FacilitySettings?autoPlaceId=${placeId}&mode=${mode}`;
};

// =========================================================================
// 🪄 دالة تطبيق السياسات الافتراضية (نظام التسهيل الذكي)
// =========================================================================
window.applySmartDefaultPolicies = function (placeId) {
    Swal.fire({
        title: '<h4 class="fw-bold text-success mb-0"><i class="bi bi-magic me-2"></i> تطبيق السياسات الافتراضية؟</h4>',
        html: `
            <div class="text-start mt-3" dir="rtl">
                <p class="text-muted fw-bold">سيتم اعتماد السياسات التالية لمنشأتك فوراً:</p>
                <ul class="mb-0 ps-3 text-secondary small fw-bold" style="line-height: 1.8;">
                    <li>عربون مبدئي <b>20%</b> لتأكيد الحجز.</li>
                    <li>مهلة السداد: <b>12 ساعة</b> لتسديد العربون قبل الإلغاء.</li>
                    <li>نظام <b>اليوم الكامل</b> (استلام 2 ظهراً / تسليم 12 ظهراً).</li>
                    <li>إلغاء مجاني قبل <b>48 ساعة</b> من موعد الدخول.</li>
                    <li>غرامة الإلغاء المتأخر: <b>مصادرة العربون</b> بالكامل.</li>
                </ul>
            </div>
        `,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#198754',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'نعم، طبقها', // تم اختصار النص ليكون مناسباً للحالتين
        cancelButtonText: 'إلغاء الأمر',
        customClass: {
            popup: 'details-luxury-modal',
            confirmButton: 'details-modal-confirm',
            cancelButton: 'details-modal-cancel',
            htmlContainer: 'details-modal-html',
            actions: 'details-modal-actions'
        },
        buttonsStyling: false
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({ title: 'جاري التطبيق...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

            // 🌟 تم تصحيح المسار ليوجه إلى PlaceController الذي عدلناه
            fetch('/Owner/Place/ApplyDefaultPolicies', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(parseInt(placeId))
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'تم بنجاح!',

                            // 🌟 التعديل المطلوب: تم استبدال النص الثابت بـ data.message 
                            text: data.message,

                            confirmButtonColor: '#00A6DA'
                        }).then(() => {
                            // تحديث الصفحة أو الكروت بعد النجاح
                            if (typeof window.refreshDetailsView === 'function') {
                                window.refreshDetailsView(placeId);
                            } else {
                                location.reload();
                            }
                        });
                    } else {
                        Swal.fire('خطأ', data.message, 'error');
                    }
                })
                .catch(err => {
                    Swal.fire('خطأ', 'تعذر الاتصال بالخادم.', 'error');
                });
        }
    });
};


// =========================================================================
// 🛡️ دالة عرض خيارات إعداد السياسات (بديل نافذة Bootstrap)
// =========================================================================
window.showPolicyOptions = function (placeId) {
    Swal.fire({
        title: '<h5 class="fw-black text-dark mb-0"><i class="bi bi-shield-check text-primary me-2"></i> إعداد سياسات المنشأة</h5>',
        html: `
            <div class="text-center mt-2" dir="rtl">
                <i class="bi bi-gear-fill text-muted" style="font-size: 3.5rem;"></i>
                <p class="mt-3 fw-bold text-dark fs-6">كيف تفضل إعداد سياسات الحجز والإلغاء لمنشأتك؟</p>
                <p class="small text-muted mb-4">يجب إعداد السياسات حتى يتم إرسال المنشأة للإدارة للموافقة عليها.</p>

                <div class="d-grid gap-3">
                    <button type="button" class="btn btn-primary w-100 rounded-pill fw-bold shadow-sm py-2 fs-6" 
                            onclick="window.applySmartDefaultPolicies(${placeId})">
                        <i class="bi bi-magic me-1"></i> تطبيق السياسات الافتراضية (سريع)
                    </button>

                    <button type="button" class="btn btn-outline-secondary w-100 rounded-pill fw-bold py-2 fs-6" 
                            onclick="window.location.href = '/Owner/Settings/FacilitySettings?autoPlaceId=${placeId}&mode=new';">
                        <i class="bi bi-sliders me-1"></i> إعداد السياسات يدوياً
                    </button>
                </div>
            </div>
        `,
        showConfirmButton: false,
        showCloseButton: true,
        allowOutsideClick: false,
        customClass: {
            popup: 'details-luxury-modal',
            confirmButton: 'details-modal-confirm',
            cancelButton: 'details-modal-cancel',
            htmlContainer: 'details-modal-html',
            actions: 'details-modal-actions'
        },
        buttonsStyling: false,
        width: '450px'
    });
};

window.submitDefaultPoliciesForm = function (placeId) {
    Swal.fire({ title: 'جاري التطبيق وإرسال المنشأة...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/Owner/Place/ApplyDefaultPolicies';

    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = 'placeId';
    input.value = placeId;

    form.appendChild(input);
    document.body.appendChild(form);
    form.submit();
};
