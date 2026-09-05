/**
 * ============================================================================
 * Dalilak Al-Yamani - Master Entities Management
 * الفئات + المدن | Premium / Responsive Edition
 * ============================================================================
 */

document.addEventListener('DOMContentLoaded', () => {
    'use strict';

    const qs = (selector, root = document) => root.querySelector(selector);
    const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));
    const DEFAULT_COLOR = '#00A6DA';
    const DEFAULT_CATEGORY_ICON = 'bi-building';

    const smartIcons = [
        { class: 'bi-shop', tags: ['مطعم', 'مطاعم', 'بوفيه', 'وجبات', 'restaurant', 'food'] },
        { class: 'bi-cup-hot', tags: ['مقهى', 'كافيه', 'كوفي', 'قهوة', 'شاي', 'cafe', 'coffee'] },
        { class: 'bi-egg-fried', tags: ['مندي', 'مطبخ', 'شعبي', 'مشويات', 'مأكولات', 'kitchen'] },
        { class: 'bi-cake2', tags: ['مخبز', 'أفران', 'حلويات', 'كيك', 'معجنات', 'bakery', 'sweets'] },
        { class: 'bi-building', tags: ['فندق', 'فنادق', 'عمارة', 'شقق', 'سكن', 'hotel'] },
        { class: 'bi-buildings', tags: ['مجمع', 'مجمعات', 'أبراج', 'مدينة', 'عقارات'] },
        { class: 'bi-house', tags: ['بيت', 'منزل', 'عقار', 'house', 'home'] },
        { class: 'bi-house-heart', tags: ['شاليه', 'شاليهات', 'استراحة', 'chalet', 'resthouse'] },
        { class: 'bi-water', tags: ['منتجع', 'مسبح', 'مسابح', 'بحر', 'resort', 'pool'] },
        { class: 'bi-tree', tags: ['منتزه', 'حديقة', 'طبيعة', 'park', 'nature'] },
        { class: 'bi-umbrella', tags: ['شاطئ', 'سياحة', 'ترفيه', 'beach', 'tourism'] },
        { class: 'bi-moon-stars', tags: ['مسجد', 'مساجد', 'جامع', 'مصلى', 'mosque'] },
        { class: 'bi-bank', tags: ['معلم', 'أثري', 'معالم', 'متحف', 'تاريخ', 'قلعة', 'حصن', 'تراث', 'museum'] },
        { class: 'bi-map', tags: ['موقع', 'سياحي', 'خريطة', 'وجهة', 'map'] },
        { class: 'bi-shield-check', tags: ['قلعة', 'حصن', 'أمني', 'حماية', 'castle'] },
        { class: 'bi-mortarboard', tags: ['مدرسة', 'جامعة', 'معهد', 'تعليم', 'school', 'education'] },
        { class: 'bi-book', tags: ['مكتبة', 'كتب', 'دراسة', 'library', 'book'] },
        { class: 'bi-hospital', tags: ['مستشفى', 'مستوصف', 'عيادة', 'صحة', 'hospital', 'clinic'] },
        { class: 'bi-capsule', tags: ['صيدلية', 'أدوية', 'pharmacy', 'medicine'] },
        { class: 'bi-bandaid', tags: ['مختبر', 'تحاليل', 'طب', 'lab', 'medical'] },
        { class: 'bi-scissors', tags: ['حلاق', 'صالون', 'تجميل', 'barber', 'salon'] },
        { class: 'bi-eyeglasses', tags: ['نظارات', 'بصريات', 'عيون', 'optics'] },
        { class: 'bi-shop-window', tags: ['مول', 'مركز تجاري', 'معرض', 'سوق', 'mall', 'market'] },
        { class: 'bi-cart', tags: ['سوبر ماركت', 'بقالة', 'تسوق', 'متجر', 'shopping', 'store'] },
        { class: 'bi-bag', tags: ['ملابس', 'بوتيك', 'أزياء', 'clothes'] },
        { class: 'bi-gem', tags: ['ذهب', 'مجوهرات', 'فضة', 'jewelry'] },
        { class: 'bi-flower1', tags: ['عطور', 'بخور', 'هدايا', 'perfume', 'gifts'] },
        { class: 'bi-basket', tags: ['خضار', 'فواكه', 'أسواق شعبية'] },
        { class: 'bi-car-front', tags: ['سيارات', 'معرض سيارات', 'مواصلات', 'تأجير', 'car'] },
        { class: 'bi-truck', tags: ['نقل', 'شحن', 'بضائع', 'توصيل', 'truck'] },
        { class: 'bi-wrench', tags: ['صيانة', 'ورشة', 'إصلاح', 'ميكانيكي', 'repair'] },
        { class: 'bi-fuel-pump', tags: ['محطة', 'بترول', 'وقود', 'غاز', 'fuel'] },
        { class: 'bi-airplane', tags: ['مطار', 'طيران', 'سفريات', 'airport', 'travel'] },
        { class: 'bi-pc-display', tags: ['كمبيوتر', 'إلكترونيات', 'تقنية', 'جوالات', 'tech'] },
        { class: 'bi-music-note-beamed', tags: ['قاعة', 'قاعات', 'أفراح', 'زفاف', 'مناسبات', 'hall', 'wedding'] },
        { class: 'bi-calendar-event', tags: ['مناسبات', 'حفلات', 'حجز', 'موعد', 'event'] },
        { class: 'bi-camera', tags: ['استديو', 'تصوير', 'كاميرا', 'studio', 'photo'] },
        { class: 'bi-currency-exchange', tags: ['صرافة', 'حوالات', 'بنك', 'أموال', 'money'] },
        { class: 'bi-briefcase', tags: ['شركة', 'مؤسسة', 'محامي', 'مكتب', 'company', 'office'] },
        { class: 'bi-bicycle', tags: ['رياضة', 'نادي', 'جيم', 'لياقة', 'sports', 'gym'] },
        { class: 'bi-trophy', tags: ['ملعب', 'ملاعب', 'بطولة', 'stadium'] },
        { class: 'bi-hexagon', tags: ['عسل', 'مناحل', 'honey'] },
        { class: 'bi-leaf', tags: ['زراعة', 'نباتات', 'مشاتل', 'agriculture'] },
        { class: 'bi-bricks', tags: ['بناء', 'مقاولات', 'مواد بناء', 'construction'] },
        { class: 'bi-tools', tags: ['سباكة', 'كهرباء', 'نجارة', 'أدوات', 'tools'] }
    ];

    const extraIcons = [
        'bi-activity', 'bi-alarm', 'bi-app', 'bi-archive', 'bi-award', 'bi-balloon', 'bi-bell',
        'bi-bookmark-star', 'bi-box', 'bi-brush', 'bi-bucket', 'bi-calculator', 'bi-calendar',
        'bi-chat', 'bi-clock', 'bi-cloud', 'bi-compass', 'bi-credit-card', 'bi-cup', 'bi-display',
        'bi-droplet', 'bi-earbuds', 'bi-envelope', 'bi-eye', 'bi-file-earmark-text', 'bi-flag',
        'bi-folder', 'bi-gift', 'bi-globe2', 'bi-grid', 'bi-hammer', 'bi-headset', 'bi-heart',
        'bi-house-door', 'bi-image', 'bi-key', 'bi-laptop', 'bi-lightbulb', 'bi-lock', 'bi-megaphone',
        'bi-palette', 'bi-people', 'bi-person-badge', 'bi-phone', 'bi-pin-map', 'bi-printer',
        'bi-puzzle', 'bi-receipt', 'bi-router', 'bi-send', 'bi-shield', 'bi-star', 'bi-sun',
        'bi-tag', 'bi-telephone', 'bi-ticket', 'bi-tv', 'bi-wallet2', 'bi-wifi'
    ];

    const iconsData = [...smartIcons];
    extraIcons.forEach(iconClass => {
        if (!iconsData.some(item => item.class === iconClass)) {
            iconsData.push({ class: iconClass, tags: [] });
        }
    });
    window.iconsData = iconsData;

    let selectedCategoryColor = DEFAULT_COLOR;
    let selectedCategoryIcon = DEFAULT_CATEGORY_ICON;
    let selectedCityColor = DEFAULT_COLOR;

    initializeTabs();
    initializeSearch();
    initializeCategoryForm();
    initializeCityForm();
    initializeEntityActions();

    function getOffcanvas(element) {
        if (!element || !window.bootstrap?.Offcanvas) return null;
        return bootstrap.Offcanvas.getInstance(element) || new bootstrap.Offcanvas(element);
    }

    function initializeTabs() {
        const buttons = qsa('.tab-btn');
        const panes = qsa('.tab-pane-content');
        const saved = localStorage.getItem('activeCategoryTab');
        const validTarget = panes.some(p => p.classList.contains(saved)) ? saved : 'categories-tab-content';

        activateTab(validTarget);

        buttons.forEach(button => {
            button.addEventListener('click', () => {
                const target = button.dataset.target;
                if (!target) return;
                localStorage.setItem('activeCategoryTab', target);
                activateTab(target);
            });
        });

        function activateTab(target) {
            panes.forEach(pane => {
                const active = pane.classList.contains(target);
                pane.style.display = active ? 'block' : 'none';
                pane.classList.toggle('is-active', active);
            });

            buttons.forEach(button => {
                const active = button.dataset.target === target;
                button.classList.toggle('active', active);
                button.setAttribute('aria-selected', active ? 'true' : 'false');
            });
        }
    }

    function initializeSearch() {
        bindCardSearch({
            inputId: 'categorySearchInput',
            cardSelector: '[data-category-card]',
            countSelector: '[data-category-visible-count]',
            noResultsSelector: '[data-category-no-results]'
        });

        bindCardSearch({
            inputId: 'citySearchInput',
            cardSelector: '[data-city-card]',
            countSelector: '[data-city-visible-count]',
            noResultsSelector: '[data-city-no-results]'
        });

        qsa('[data-clear-search]').forEach(button => {
            button.addEventListener('click', () => {
                const input = document.getElementById(button.dataset.clearSearch);
                if (!input) return;
                input.value = '';
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.focus();
            });
        });
    }

    function bindCardSearch({ inputId, cardSelector, countSelector, noResultsSelector }) {
        const input = document.getElementById(inputId);
        if (!input) return;

        const clearButton = qs(`[data-clear-search="${inputId}"]`);
        const count = qs(countSelector);
        const noResults = qs(noResultsSelector);

        const apply = () => {
            const term = normalizeSearch(input.value);
            const cards = qsa(cardSelector);
            let visible = 0;

            cards.forEach(card => {
                const text = normalizeSearch(card.dataset.searchText || card.textContent || '');
                const show = !term || text.includes(term);
                card.classList.toggle('is-filtered-out', !show);
                if (show) visible++;
            });

            if (count) count.textContent = visible.toString();
            if (noResults) noResults.classList.toggle('d-none', visible > 0 || cards.length === 0);
            if (clearButton) clearButton.hidden = input.value.length === 0;
        };

        input.addEventListener('input', apply);
        apply();
    }

    function normalizeSearch(value) {
        return String(value || '')
            .trim()
            .toLowerCase()
            .replace(/[أإآ]/g, 'ا')
            .replace(/ة/g, 'ه');
    }

    function initializeCategoryForm() {
        const form = document.getElementById('addCategoryForm');
        if (!form) return;

        const nameInput = qs('.category-name-input', form);
        const colorPicker = qs('.category-color-picker', form);
        const iconSearch = qs('.category-icon-search', form);
        const bookingSelect = document.getElementById('bookingSystemId');
        const imageInput = document.getElementById('categoryImageInput');

        colorPicker?.addEventListener('input', () => {
            selectedCategoryColor = colorPicker.value || DEFAULT_COLOR;
            updateCategoryColorUI();
        });

        nameInput?.addEventListener('input', () => {
            updateCategoryPreviewName();
            renderCategoryIcons(nameInput.value, selectedCategoryColor, true);
        });

        iconSearch?.addEventListener('input', () => {
            renderCategoryIcons(iconSearch.value, selectedCategoryColor, true);
        });

        bookingSelect?.addEventListener('change', updateBookingPreview);

        qsa('input[name="OwnerCanAddPlacesChoice"]', form).forEach(radio => {
            radio.addEventListener('change', updateAccessChoiceUI);
        });

        imageInput?.addEventListener('change', () => {
            const file = imageInput.files?.[0];
            if (!file) {
                resetImagePreview();
                return;
            }

            if (!file.type.startsWith('image/')) {
                imageInput.value = '';
                showWarning('الملف غير صالح', 'اختر ملف صورة صالحًا للفئة.');
                resetImagePreview();
                return;
            }

            const reader = new FileReader();
            reader.onload = event => showImagePreview(event.target?.result || '');
            reader.readAsDataURL(file);
        });

        form.addEventListener('submit', submitCategoryForm);
        renderCategoryIcons('', selectedCategoryColor, false);
        updateCategoryColorUI();
        updateBookingPreview();
        updateAccessChoiceUI();
    }

    function initializeCityForm() {
        const form = document.getElementById('addCityForm');
        if (!form) return;

        const nameInput = qs('.city-name-input', form);
        const colorPicker = qs('.city-color-picker', form);

        nameInput?.addEventListener('input', updateCityPreviewName);
        colorPicker?.addEventListener('input', () => {
            selectedCityColor = colorPicker.value || DEFAULT_COLOR;
            updateCityColorUI();
        });

        form.addEventListener('submit', submitCityForm);
        updateCityColorUI();
    }

    window.openAddCategory = function openAddCategory() {
        resetCategoryFormForCreate();
        getOffcanvas(document.getElementById('addCategoryOffcanvas'))?.show();
        setTimeout(() => qs('.category-name-input')?.focus(), 350);
    };

    window.openAddCity = function openAddCity() {
        resetCityFormForCreate();
        getOffcanvas(document.getElementById('addCityOffcanvas'))?.show();
        setTimeout(() => qs('.city-name-input')?.focus(), 350);
    };

    function resetCategoryFormForCreate() {
        const form = document.getElementById('addCategoryForm');
        if (!form) return;

        form.reset();
        document.getElementById('categoryId').value = '0';
        document.getElementById('addCategoryLabel').textContent = 'إضافة فئة جديدة';
        qs('.btn-save-entity-text', form).textContent = 'حفظ الفئة';

        selectedCategoryColor = DEFAULT_COLOR;
        selectedCategoryIcon = DEFAULT_CATEGORY_ICON;

        const colorPicker = qs('.category-color-picker', form);
        if (colorPicker) colorPicker.value = DEFAULT_COLOR;

        const booking = document.getElementById('bookingSystemId');
        if (booking) booking.value = '0';

        const openRadio = qs('input[name="OwnerCanAddPlacesChoice"][value="true"]', form);
        if (openRadio) openRadio.checked = true;

        const iconSearch = qs('.category-icon-search', form);
        if (iconSearch) iconSearch.value = '';

        resetImagePreview();
        updateCategoryPreviewName();
        updateCategoryColorUI();
        updateBookingPreview();
        updateAccessChoiceUI();
        renderCategoryIcons('', selectedCategoryColor, false);
    }

    function resetCityFormForCreate() {
        const form = document.getElementById('addCityForm');
        if (!form) return;

        form.reset();
        document.getElementById('cityId').value = '0';
        document.getElementById('addCityLabel').textContent = 'إضافة مدينة جديدة';
        qs('.btn-save-entity-text', form).textContent = 'حفظ المدينة';

        selectedCityColor = DEFAULT_COLOR;
        const picker = qs('.city-color-picker', form);
        if (picker) picker.value = DEFAULT_COLOR;

        updateCityPreviewName();
        updateCityColorUI();
    }

    function updateCategoryPreviewName() {
        const input = qs('.category-name-input');
        const preview = document.getElementById('previewCategoryName');
        if (preview) preview.textContent = input?.value.trim() || 'فئة جديدة';
    }

    function updateCityPreviewName() {
        const input = qs('.city-name-input');
        const preview = document.getElementById('previewCityName');
        if (preview) preview.textContent = input?.value.trim() || 'مدينة جديدة';
    }

    function updateCategoryColorUI() {
        const hex = qs('.category-color-hex');
        const previewIcon = document.getElementById('previewCategoryIcon');
        if (hex) hex.textContent = selectedCategoryColor;
        if (previewIcon) previewIcon.style.color = selectedCategoryColor;
    }

    function updateCityColorUI() {
        const hex = qs('.city-color-hex');
        const previewIcon = document.getElementById('previewCityIcon');
        if (hex) hex.textContent = selectedCityColor;
        if (previewIcon) previewIcon.style.color = selectedCityColor;
    }

    function updateBookingPreview() {
        const select = document.getElementById('bookingSystemId');
        const preview = document.getElementById('previewCategoryBooking');
        const value = Number.parseInt(select?.value || '0', 10);
        const labels = {
            0: 'بدون حجز',
            1: 'غرف وأيام',
            2: 'باقات وفترات',
            3: 'قاعات ومناسبات'
        };

        if (preview) preview.textContent = labels[value] || labels[0];
        qsa('[data-booking-help]').forEach(item => {
            item.classList.toggle('is-active', Number.parseInt(item.dataset.bookingHelp || '-1', 10) === value);
        });
    }

    function updateAccessChoiceUI() {
        const checked = qs('input[name="OwnerCanAddPlacesChoice"]:checked');
        const ownerCanAdd = checked?.value !== 'false';

        qsa('[data-access-card]').forEach(card => {
            card.classList.toggle('is-selected', card.dataset.accessCard === String(ownerCanAdd));
        });

        const preview = document.getElementById('previewCategoryAccess');
        if (preview) {
            preview.className = `preview-access-badge ${ownerCanAdd ? 'preview-access-badge--open' : 'preview-access-badge--locked'}`;
            preview.innerHTML = ownerCanAdd
                ? '<i class="bi bi-people-fill"></i> المالك والإدارة'
                : '<i class="bi bi-lock-fill"></i> إدارة فقط';
        }
    }

    function renderCategoryIcons(searchTerm = '', currentColor = DEFAULT_COLOR, autoSelectBest = false) {
        const grid = qs('.category-icon-grid');
        if (!grid) return;

        const term = normalizeSearch(searchTerm);
        let bestMatch = null;

        if (term && autoSelectBest) {
            bestMatch = iconsData.find(item => {
                return normalizeSearch(item.class).includes(term) ||
                    item.tags.some(tag => normalizeSearch(tag).includes(term) || term.includes(normalizeSearch(tag)));
            });

            if (bestMatch) selectedCategoryIcon = bestMatch.class;
        }

        const filtered = term
            ? iconsData.filter(item =>
                normalizeSearch(item.class).includes(term) ||
                item.tags.some(tag => normalizeSearch(tag).includes(term) || term.includes(normalizeSearch(tag))))
            : iconsData;

        const list = filtered.length > 0 ? filtered : iconsData;
        grid.innerHTML = '';

        list.forEach(iconObj => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = `icon-item ${iconObj.class === selectedCategoryIcon ? 'active' : ''}`;
            button.setAttribute('role', 'option');
            button.setAttribute('aria-selected', iconObj.class === selectedCategoryIcon ? 'true' : 'false');
            button.title = iconObj.class.replace('bi-', '');
            button.innerHTML = `<i class="bi ${iconObj.class}"></i>`;

            button.addEventListener('click', () => {
                selectedCategoryIcon = iconObj.class;
                qsa('.icon-item', grid).forEach(item => {
                    const active = item === button;
                    item.classList.toggle('active', active);
                    item.setAttribute('aria-selected', active ? 'true' : 'false');
                });
                updatePreviewCategoryIcon(currentColor);
            });

            grid.appendChild(button);
        });

        updatePreviewCategoryIcon(currentColor);
    }

    function updatePreviewCategoryIcon(color = selectedCategoryColor) {
        const preview = document.getElementById('previewCategoryIcon');
        if (!preview) return;
        preview.className = `bi ${selectedCategoryIcon}`;
        preview.style.color = color;
    }

    function showImagePreview(source) {
        const wrapper = document.getElementById('categoryImagePreview');
        const image = document.getElementById('categoryImagePreviewImg');
        if (!wrapper || !image || !source) return;
        image.src = source;
        wrapper.hidden = false;
    }

    function resetImagePreview() {
        const wrapper = document.getElementById('categoryImagePreview');
        const image = document.getElementById('categoryImagePreviewImg');
        if (wrapper) wrapper.hidden = true;
        if (image) image.removeAttribute('src');
    }

    async function submitCategoryForm(event) {
        event.preventDefault();

        const form = event.currentTarget;
        const submitButton = qs('button[type="submit"]', form);
        const categoryId = Number.parseInt(document.getElementById('categoryId')?.value || '0', 10);
        const categoryName = qs('.category-name-input', form)?.value.trim() || '';
        const bookingSystemId = Number.parseInt(document.getElementById('bookingSystemId')?.value || '0', 10);
        const imageInput = document.getElementById('categoryImageInput');
        const ownerCanAdd = qs('input[name="OwnerCanAddPlacesChoice"]:checked', form)?.value !== 'false';

        if (!categoryName) {
            showWarning('اسم الفئة مطلوب', 'اكتب اسم الفئة قبل الحفظ.');
            qs('.category-name-input', form)?.focus();
            return;
        }

        if (categoryId === 0 && (!imageInput || !imageInput.files?.length)) {
            showWarning('صورة الفئة مطلوبة', 'اختر صورة خلفية للفئة الجديدة قبل الحفظ.');
            return;
        }

        const formData = new FormData();
        formData.append('CategoryId', String(categoryId));
        formData.append('CategoryName', categoryName);
        formData.append('ThemeColor', selectedCategoryColor || DEFAULT_COLOR);
        formData.append('IconClass', selectedCategoryIcon || DEFAULT_CATEGORY_ICON);
        formData.append('BookingSystemId', String(bookingSystemId));
        formData.append('HasBookingSystem', String(bookingSystemId > 0));
        formData.append('OwnerCanAddPlaces', String(ownerCanAdd));

        if (imageInput?.files?.[0]) formData.append('imageFile', imageInput.files[0]);

        const url = categoryId === 0
            ? '/Admin/MasterEntities/AddCategory'
            : '/Admin/MasterEntities/EditCategory';

        setButtonLoading(submitButton, true, categoryId === 0 ? 'جاري إضافة الفئة...' : 'جاري حفظ التعديلات...');

        try {
            const response = await fetch(url, { method: 'POST', body: formData });
            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(data.message || 'تعذر حفظ الفئة.');
            }

            await showSuccess('تم الحفظ', data.message || 'تم حفظ الفئة بنجاح.');
            window.location.reload();
        } catch (error) {
            showError('تعذر حفظ الفئة', error.message || 'حدث خطأ أثناء الاتصال بالخادم.');
            setButtonLoading(submitButton, false);
        }
    }

    async function submitCityForm(event) {
        event.preventDefault();

        const form = event.currentTarget;
        const submitButton = qs('button[type="submit"]', form);
        const cityId = Number.parseInt(document.getElementById('cityId')?.value || '0', 10);
        const cityName = qs('.city-name-input', form)?.value.trim() || '';

        if (!cityName) {
            showWarning('اسم المدينة مطلوب', 'اكتب اسم المدينة قبل الحفظ.');
            qs('.city-name-input', form)?.focus();
            return;
        }

        const url = cityId === 0
            ? '/Admin/MasterEntities/AddCity'
            : '/Admin/MasterEntities/EditCity';

        setButtonLoading(submitButton, true, cityId === 0 ? 'جاري إضافة المدينة...' : 'جاري حفظ التعديلات...');

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    CityId: cityId,
                    CityName: cityName,
                    ThemeColor: selectedCityColor || DEFAULT_COLOR
                })
            });

            const data = await response.json();
            if (!response.ok || !data.success) {
                throw new Error(data.message || 'تعذر حفظ المدينة.');
            }

            await showSuccess('تم الحفظ', data.message || 'تم حفظ المدينة بنجاح.');
            window.location.reload();
        } catch (error) {
            showError('تعذر حفظ المدينة', error.message || 'حدث خطأ أثناء الاتصال بالخادم.');
            setButtonLoading(submitButton, false);
        }
    }

    function initializeEntityActions() {
        document.addEventListener('click', async event => {
            const editCategory = event.target.closest('.edit-category');
            if (editCategory) {
                event.preventDefault();
                await loadCategoryForEdit(editCategory.dataset.id);
                return;
            }

            const editCity = event.target.closest('.edit-city');
            if (editCity) {
                event.preventDefault();
                await loadCityForEdit(editCity.dataset.id);
                return;
            }

            const deleteCategory = event.target.closest('.delete-category');
            if (deleteCategory) {
                event.preventDefault();
                await deleteEntity({
                    title: 'حذف الفئة؟',
                    text: 'سيتم حذف الفئة نهائيًا إذا لم تكن مرتبطة بمنشآت.',
                    url: `/Admin/MasterEntities/DeleteCategory/${deleteCategory.dataset.id}`
                });
                return;
            }

            const deleteCity = event.target.closest('.delete-city');
            if (deleteCity) {
                event.preventDefault();
                await deleteEntity({
                    title: 'حذف المدينة؟',
                    text: 'سيتم حذف المدينة نهائيًا إذا لم تكن مرتبطة بمنشآت أو مستخدمين.',
                    url: `/Admin/MasterEntities/DeleteCity/${deleteCity.dataset.id}`
                });
            }
        });
    }

    async function loadCategoryForEdit(id) {
        if (!id) return;

        try {
            const response = await fetch(`/Admin/MasterEntities/GetCategory/${id}`);
            const result = await response.json();
            if (!result.success) throw new Error(result.message || 'تعذر جلب بيانات الفئة.');

            const data = result.data;
            const form = document.getElementById('addCategoryForm');
            if (!form) return;

            document.getElementById('categoryId').value = String(data.categoryId);
            qs('.category-name-input', form).value = data.categoryName || '';
            document.getElementById('addCategoryLabel').textContent = 'تعديل الفئة';
            qs('.btn-save-entity-text', form).textContent = 'حفظ التعديلات';

            selectedCategoryColor = data.themeColor || DEFAULT_COLOR;
            selectedCategoryIcon = data.iconClass || DEFAULT_CATEGORY_ICON;

            const picker = qs('.category-color-picker', form);
            if (picker) picker.value = selectedCategoryColor;

            const booking = document.getElementById('bookingSystemId');
            if (booking) booking.value = String(data.bookingSystemId ?? 0);

            const ownerCanAdd = data.ownerCanAddPlaces !== false;
            const radio = qs(`input[name="OwnerCanAddPlacesChoice"][value="${ownerCanAdd ? 'true' : 'false'}"]`, form);
            if (radio) radio.checked = true;

            const iconSearch = qs('.category-icon-search', form);
            if (iconSearch) iconSearch.value = '';

            if (data.imagePath) showImagePreview(data.imagePath);
            else resetImagePreview();

            updateCategoryPreviewName();
            updateCategoryColorUI();
            updateBookingPreview();
            updateAccessChoiceUI();
            renderCategoryIcons('', selectedCategoryColor, false);

            getOffcanvas(document.getElementById('addCategoryOffcanvas'))?.show();
        } catch (error) {
            showError('تعذر فتح الفئة', error.message || 'حدث خطأ أثناء تحميل بيانات الفئة.');
        }
    }

    async function loadCityForEdit(id) {
        if (!id) return;

        try {
            const response = await fetch(`/Admin/MasterEntities/GetCity/${id}`);
            const result = await response.json();
            if (!result.success) throw new Error(result.message || 'تعذر جلب بيانات المدينة.');

            const data = result.data;
            const form = document.getElementById('addCityForm');
            if (!form) return;

            document.getElementById('cityId').value = String(data.cityId);
            qs('.city-name-input', form).value = data.cityName || '';
            document.getElementById('addCityLabel').textContent = 'تعديل المدينة';
            qs('.btn-save-entity-text', form).textContent = 'حفظ التعديلات';

            selectedCityColor = data.themeColor || DEFAULT_COLOR;
            const picker = qs('.city-color-picker', form);
            if (picker) picker.value = selectedCityColor;

            updateCityPreviewName();
            updateCityColorUI();
            getOffcanvas(document.getElementById('addCityOffcanvas'))?.show();
        } catch (error) {
            showError('تعذر فتح المدينة', error.message || 'حدث خطأ أثناء تحميل بيانات المدينة.');
        }
    }

    async function deleteEntity({ title, text, url }) {
        const confirmed = await Swal.fire({
            title,
            text,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'نعم، احذف',
            cancelButtonText: 'إلغاء',
            reverseButtons: true,
            focusCancel: true,
            confirmButtonColor: '#FF5A5F',
            cancelButtonColor: '#64748B'
        });

        if (!confirmed.isConfirmed) return;

        try {
            const response = await fetch(url, { method: 'POST' });
            const data = await response.json();
            if (!response.ok || !data.success) throw new Error(data.message || 'تعذر تنفيذ الحذف.');

            await showSuccess('تم الحذف', data.message || 'تم الحذف بنجاح.');
            window.location.reload();
        } catch (error) {
            showError('تعذر الحذف', error.message || 'حدث خطأ أثناء تنفيذ العملية.');
        }
    }

    function setButtonLoading(button, loading, text = '') {
        if (!button) return;

        if (loading) {
            if (!button.dataset.originalHtml) button.dataset.originalHtml = button.innerHTML;
            button.disabled = true;
            button.classList.add('is-loading');
            button.innerHTML = `<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>${text || 'جاري الحفظ...'}</span>`;
            return;
        }

        button.disabled = false;
        button.classList.remove('is-loading');
        if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
            delete button.dataset.originalHtml;
        }
    }

    function showSuccess(title, text) {
        return Swal.fire({
            icon: 'success',
            title,
            text,
            confirmButtonText: 'تم',
            confirmButtonColor: '#00A6DA'
        });
    }

    function showWarning(title, text) {
        return Swal.fire({
            icon: 'warning',
            title,
            text,
            confirmButtonText: 'حسنًا',
            confirmButtonColor: '#00A6DA'
        });
    }

    function showError(title, text) {
        return Swal.fire({
            icon: 'error',
            title,
            text,
            confirmButtonText: 'حسنًا',
            confirmButtonColor: '#FF5A5F'
        });
    }
});
