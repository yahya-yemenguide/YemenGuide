(function (window, document) {
    "use strict";

    const editorConfig = {
        mode: "owner",
        reloadAfterSave: false
    };

    function configure(options) {
        Object.assign(editorConfig, options || {});
        if (editorConfig.mode === "admin" && options?.reloadAfterSave === undefined) {
            editorConfig.reloadAfterSave = true;
        }
    }

    window.addDynamicTag = window.addDynamicTag || function (button) {
        if (!button) return;
        const inputGroup = button.closest('.input-group');
        const input = inputGroup?.querySelector('input');
        if (!input || !input.value.trim()) { input?.focus(); return; }
        const container = button.closest('.dynamic-field-container') || inputGroup.parentElement;
        let tagsContainer = container?.querySelector('.tags-container');
        if (!container) return;
        if (!tagsContainer) {
            tagsContainer = document.createElement('div');
            tagsContainer.className = 'tags-container mb-2 d-flex flex-wrap gap-2';
            container.insertBefore(tagsContainer, inputGroup);
        }
        const value = input.value.trim();
        const tag = document.createElement('span');
        tag.className = 'badge bg-primary text-white p-2 d-flex align-items-center shadow-sm';
        tag.innerHTML = '<span></span><i class="bi bi-x-circle-fill ms-2 text-light" role="button" aria-label="حذف"></i><input type="hidden">';
        tag.querySelector('span').textContent = value;
        tag.querySelector('i').addEventListener('click', () => tag.remove());
        tag.querySelector('input').value = value;
        tagsContainer.appendChild(tag);
        input.value = '';
        input.focus();
    };

    window.isStrictlyValidPhone = window.isStrictlyValidPhone || function (itiInstance, inputElement) {
        if (!itiInstance || !inputElement) return false;
        const raw = String(inputElement.value || '').replace(/\D/g, '');
        const country = itiInstance.getSelectedCountryData?.();
        if (country?.iso2 === 'ye') return /^(77|78|70|73|71)[0-9]{7}$/.test(raw);
        return typeof itiInstance.isValidNumber === 'function' ? itiInstance.isValidNumber() : raw.length >= 8;
    };

    window.formatPhoneNumber = window.formatPhoneNumber || function (itiInstance, inputElement) {
        if (!itiInstance || !inputElement || !window.isStrictlyValidPhone(itiInstance, inputElement)) return;
        const selectedCountry = itiInstance.getSelectedCountryData?.();
        const dialCode = selectedCountry?.dialCode ? `+${selectedCountry.dialCode}` : '';
        let formatted = itiInstance.getNumber?.() || inputElement.value;
        if (dialCode && formatted.startsWith(dialCode)) formatted = formatted.substring(dialCode.length);
        inputElement.value = formatted.replace(/\s+/g, '').trim();
    };

    window.validatePhoneInput = window.validatePhoneInput || function (itiInstance, inputElement, messageId) {
        const message = document.getElementById(messageId);
        const wrapper = inputElement?.closest('.premium-input-group-phone');
        const valid = inputElement?.value.trim() && window.isStrictlyValidPhone(itiInstance, inputElement);

        wrapper?.classList.remove('glow-error', 'glow-success', 'is-invalid');
        if (message) message.className = 'small mt-1 text-end d-block fw-bold';

        if (!inputElement?.value.trim()) {
            if (message) message.innerHTML = '';
            return false;
        }

        if (valid) {
            wrapper?.classList.add('glow-success');
            if (message) {
                message.innerHTML = '<i class="bi bi-check-circle-fill"></i> رقم صحيح ومدعوم';
                message.classList.add('text-success');
            }
        } else {
            wrapper?.classList.add('glow-error', 'is-invalid');
            if (message) {
                message.innerHTML = '<i class="bi bi-x-circle-fill"></i> صيغة الرقم خاطئة أو وهمية';
                message.classList.add('text-danger');
            }
        }

        return Boolean(valid);
    };

    function clearOwnerPlaceModalContext() {
        document.body.classList.remove(
            'owner-place-modal-context',
            'owner-place-edit-modal',
            'owner-place-images-modal',
            'owner-place-confirm-modal'
        );
    }

    function ensureOwnerPlaceImageActionUiStyle() {
        if (document.getElementById('ownerPlaceImageActionUiStyle')) return;

        const style = document.createElement('style');
        style.id = 'ownerPlaceImageActionUiStyle';
        style.textContent = `
            .owner-place-delete-confirm-layer {
                position: fixed;
                z-index: 2147483000 !important;
                inset: 0;
                display: grid;
                place-items: center;
                padding: 18px;
                background: rgba(5, 32, 60, .26);
                backdrop-filter: blur(1.5px);
                -webkit-backdrop-filter: blur(1.5px);
                opacity: 0;
                transition: opacity .16s ease;
            }

            .owner-place-delete-confirm-layer.is-visible {
                opacity: 1;
            }

            .owner-place-delete-confirm-modal {
                position: relative;
                z-index: 2147483001 !important;
                width: min(390px, calc(100vw - 32px));
                padding: 22px 20px 18px;
                border: 1px solid rgba(255, 90, 95, .16);
                border-radius: 20px;
                background: #FFFFFF;
                box-shadow: 0 28px 75px rgba(5, 32, 60, .28);
                direction: rtl;
                text-align: center;
                transform: translateY(8px) scale(.985);
                opacity: 0;
                transition: transform .16s ease, opacity .16s ease;
            }

            .owner-place-delete-confirm-modal.is-visible {
                transform: translateY(0) scale(1);
                opacity: 1;
            }

            .owner-place-delete-confirm-modal__close {
                position: absolute;
                inset-block-start: 10px;
                inset-inline-start: 10px;
                width: 32px;
                height: 32px;
                display: grid;
                place-items: center;
                border: 0;
                border-radius: 9px;
                background: #F7FAFC;
                color: #718391;
                cursor: pointer;
            }

            .owner-place-delete-confirm-modal__icon {
                width: 54px;
                height: 54px;
                display: grid;
                place-items: center;
                margin: 2px auto 11px;
                border-radius: 16px;
                color: #FF5A5F;
                background: rgba(255, 90, 95, .10);
                font-size: 1.35rem;
            }

            .owner-place-delete-confirm-modal h5 {
                margin: 0 0 7px;
                color: #05203C;
                font-size: 1rem;
                font-weight: 900;
            }

            .owner-place-delete-confirm-modal p {
                margin: 0 auto 16px;
                max-width: 310px;
                color: #6C8190;
                font-size: .76rem;
                font-weight: 700;
                line-height: 1.7;
            }

            .owner-place-delete-confirm-modal__actions {
                display: grid;
                grid-template-columns: 1fr 1fr;
                gap: 9px;
            }

            .owner-place-delete-confirm-modal__actions button {
                min-height: 40px;
                border-radius: 11px;
                font-family: inherit;
                font-size: .76rem;
                font-weight: 900;
                cursor: pointer;
            }

            .owner-place-delete-confirm-modal__cancel {
                border: 1px solid #DCE7ED;
                color: #637887;
                background: #FFFFFF;
            }

            .owner-place-delete-confirm-modal__approve {
                border: 1px solid #FF5A5F;
                color: #FFFFFF;
                background: #FF5A5F;
            }

            .owner-place-image-action-toast {
                position: fixed;
                z-index: 2147483002 !important;
                inset-block-start: 18px;
                inset-inline-end: 18px;
                width: min(310px, calc(100vw - 28px));
                display: flex;
                align-items: center;
                gap: 9px;
                padding: 10px 12px;
                border: 1px solid rgba(0, 166, 218, .12);
                border-radius: 14px;
                background: #fff;
                box-shadow: 0 14px 36px rgba(5, 32, 60, .16);
                direction: rtl;
                color: #36546A;
                font-size: .73rem;
                font-weight: 800;
                line-height: 1.5;
                opacity: 0;
                transform: translateY(-8px);
                transition: opacity .18s ease, transform .18s ease;
                pointer-events: none;
            }

            .owner-place-image-action-toast.is-visible {
                opacity: 1;
                transform: translateY(0);
            }

            .owner-place-image-action-toast i {
                flex: 0 0 auto;
                font-size: 1rem;
            }

            .owner-place-image-action-toast.is-success i {
                color: #28A66A;
            }

            .owner-place-image-action-toast.is-error i {
                color: #FF5A5F;
            }

            @media (max-width: 575.98px) {
                .owner-place-image-confirm {
                    width: calc(100vw - 20px);
                    padding: 14px;
                    border-radius: 16px;
                }

                .owner-place-image-action-toast {
                    inset-block-start: 10px;
                    inset-inline-end: 10px;
                    width: calc(100vw - 20px);
                }
            }
        `;
        document.head.appendChild(style);
    }

    function showOwnerPlaceImageActionToast(message, type = 'success') {
        ensureOwnerPlaceImageActionUiStyle();

        document.querySelectorAll('.owner-place-image-action-toast')
            .forEach(item => item.remove());

        const toast = document.createElement('div');
        toast.className =
            `owner-place-image-action-toast ${type === 'error' ? 'is-error' : 'is-success'}`;

        toast.innerHTML = `
            <i class="bi ${type === 'error' ? 'bi-x-circle-fill' : 'bi-check-circle-fill'}"></i>
            <span>${String(message || '').replace(/</g, '&lt;').replace(/>/g, '&gt;')}</span>
        `;

        document.body.appendChild(toast);

        requestAnimationFrame(() => {
            toast.classList.add('is-visible');
        });

        window.setTimeout(() => {
            toast.classList.remove('is-visible');
            window.setTimeout(() => toast.remove(), 220);
        }, 1800);
    }

    function confirmOwnerPlaceImageDelete(message, onConfirm) {
        ensureOwnerPlaceImageActionUiStyle();

        document
            .querySelectorAll('.owner-place-delete-confirm-layer')
            .forEach(item => item.remove());

        const layer = document.createElement('div');
        layer.className = 'owner-place-delete-confirm-layer';
        layer.setAttribute('role', 'presentation');

        layer.innerHTML = `
            <div class="owner-place-delete-confirm-modal"
                 role="dialog"
                 aria-modal="true"
                 aria-label="تأكيد حذف الصورة">
                <button type="button"
                        class="owner-place-delete-confirm-modal__close"
                        aria-label="إغلاق">
                    <i class="bi bi-x-lg"></i>
                </button>

                <span class="owner-place-delete-confirm-modal__icon">
                    <i class="bi bi-trash3-fill"></i>
                </span>

                <h5>حذف الصورة؟</h5>

                <p>${String(message || '')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')}</p>

                <div class="owner-place-delete-confirm-modal__actions">
                    <button type="button"
                            class="owner-place-delete-confirm-modal__cancel">
                        إلغاء
                    </button>

                    <button type="button"
                            class="owner-place-delete-confirm-modal__approve">
                        نعم، حذف
                    </button>
                </div>
            </div>
        `;

        document.body.appendChild(layer);

        const modal =
            layer.querySelector('.owner-place-delete-confirm-modal');

        requestAnimationFrame(() => {
            layer.classList.add('is-visible');
            modal?.classList.add('is-visible');
        });

        const close = () => {
            layer.classList.remove('is-visible');
            modal?.classList.remove('is-visible');

            window.setTimeout(() => {
                layer.remove();
            }, 160);
        };

        layer
            .querySelector('.owner-place-delete-confirm-modal__close')
            ?.addEventListener('click', close, { once: true });

        layer
            .querySelector('.owner-place-delete-confirm-modal__cancel')
            ?.addEventListener('click', close, { once: true });

        layer
            .querySelector('.owner-place-delete-confirm-modal__approve')
            ?.addEventListener(
                'click',
                () => {
                    close();

                    if (typeof onConfirm === 'function') {
                        window.setTimeout(onConfirm, 120);
                    }
                },
                { once: true }
            );

        layer.addEventListener('click', event => {
            if (event.target === layer) {
                close();
            }
        });
    }

    ensureOwnerPlaceImageActionUiStyle();

    function ensureOwnerPlaceSuccessToastStyle() {
        if (document.getElementById('ownerPlaceSuccessToastStyle')) return;

        const style = document.createElement('style');
        style.id = 'ownerPlaceSuccessToastStyle';
        style.textContent = `

            .swal2-container.swal2-top-end:has(.owner-place-success-toast) {
                background: transparent !important;
                backdrop-filter: none !important;
                -webkit-backdrop-filter: none !important;
                pointer-events: none !important;
                padding: 14px !important;
            }

            .swal2-container.swal2-top-end:has(.owner-place-success-toast)
            .owner-place-success-toast {
                pointer-events: auto !important;
            }

            .swal2-popup.owner-place-success-toast {
                width: min(320px, calc(100vw - 28px)) !important;
                max-width: min(320px, calc(100vw - 28px)) !important;
                height: auto !important;
                max-height: none !important;
                min-height: 0 !important;
                padding: 10px 12px !important;
                border-radius: 15px !important;
                box-shadow: 0 16px 38px rgba(5, 32, 60, .16) !important;
            }

            .swal2-popup.owner-place-success-toast .swal2-icon {
                width: 28px !important;
                height: 28px !important;
                min-width: 28px !important;
                margin: 0 0 0 9px !important;
                border-width: 2px !important;
            }

            .swal2-popup.owner-place-success-toast .swal2-title {
                margin: 0 !important;
                padding: 0 !important;
                border: 0 !important;
                background: transparent !important;
                font-size: .82rem !important;
                line-height: 1.45 !important;
                font-weight: 900 !important;
            }

            .swal2-popup.owner-place-success-toast .swal2-html-container {
                margin: 2px 0 0 !important;
                padding: 0 !important;
                font-size: .69rem !important;
                line-height: 1.5 !important;
            }

            .swal2-popup.owner-place-success-toast .swal2-timer-progress-bar {
                height: 2px !important;
            }

            @media (max-width: 575.98px) {
                .swal2-popup.owner-place-success-toast {
                    width: calc(100vw - 20px) !important;
                    padding: 9px 10px !important;
                    border-radius: 13px !important;
                }
            }
        `;
        document.head.appendChild(style);
    }

    ensureOwnerPlaceSuccessToastStyle();

    window.showSafeToast = window.showSafeToast || function (message, icon) {
        if (!window.Swal) return;
        Swal.mixin({ toast: true, position: 'top-end', showConfirmButton: false, timer: 2200 })
            .fire({ icon: icon || 'success', title: message });
    };

    window.elegantConfirm = window.elegantConfirm || function (message, onConfirm) {
        Swal.fire({
            title: 'تأكيد الإجراء',
            text: message,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'نعم، متابعة',
            cancelButtonText: 'إلغاء',
            confirmButtonColor: '#FF5A5F',
            cancelButtonColor: '#05203C'
        }).then(result => { if (result.isConfirmed && typeof onConfirm === 'function') onConfirm(); });
    };

    function initializeCompactEditModal() {
        const popup = document.querySelector('.swal2-popup');
        const root = popup?.querySelector('.owner-place-edit-root');
        if (!popup || !root || root.dataset.compactTabsReady === 'true') return;

        root.dataset.compactTabsReady = 'true';

        const mainRow = Array.from(root.children).find(element => element.classList?.contains('row'));
        const mapSection = Array.from(root.children).find(element => element.querySelector?.('#googleMapPicker'));
        const addressSection = Array.from(root.children).find(element => element.querySelector?.('#edit-address'));

        if (!mainRow) return;

        const basicFields = Array.from(mainRow.children).filter(element => {
            return !element.querySelector?.('.dynamic-field-container')
                && !element.querySelector?.('.unit-box')
                && !element.matches?.('[style*="display"]');
        });

        const detailsFields = Array.from(mainRow.children).filter(element => !basicFields.includes(element));

        const tabs = document.createElement('div');
        tabs.className = 'owner-place-edit-tabs';
        tabs.setAttribute('role', 'tablist');
        tabs.innerHTML = `
            <button type="button" class="owner-place-edit-tab is-active" data-edit-panel="basic" role="tab" aria-selected="true">
                <i class="bi bi-card-text"></i> البيانات الأساسية
            </button>
            <button type="button" class="owner-place-edit-tab" data-edit-panel="details" role="tab" aria-selected="false">
                <i class="bi bi-stars"></i> التفاصيل والمرافق
            </button>
            <button type="button" class="owner-place-edit-tab" data-edit-panel="location" role="tab" aria-selected="false">
                <i class="bi bi-geo-alt"></i> الموقع والعنوان
            </button>`;

        const basicPanel = document.createElement('section');
        basicPanel.className = 'owner-place-edit-panel is-active';
        basicPanel.dataset.editPanel = 'basic';
        const basicRow = document.createElement('div');
        basicRow.className = 'owner-place-edit-basic-grid';

        basicFields.forEach(element => {
            if (element.querySelector('#edit-name')) {
                element.classList.add('owner-place-edit-field', 'owner-place-edit-field--name');
            } else if (element.querySelector('#edit-phone-display')) {
                element.classList.add('owner-place-edit-field', 'owner-place-edit-field--phone');
            } else if (element.querySelector('#edit-brief-desc')) {
                element.classList.add('owner-place-edit-field', 'owner-place-edit-field--description');
            } else {
                element.classList.add('owner-place-edit-field');
            }

            basicRow.appendChild(element);
        });

        basicPanel.appendChild(basicRow);

        const detailsPanel = document.createElement('section');
        detailsPanel.className = 'owner-place-edit-panel';
        detailsPanel.dataset.editPanel = 'details';
        if (detailsFields.length) {
            detailsFields.forEach(element => {
                element.classList.remove('mt-3');
                detailsPanel.appendChild(element);
            });
        } else {
            detailsPanel.innerHTML = '<div class="text-center text-muted py-4 small">لا توجد تفاصيل إضافية لهذا النوع من المنشآت.</div>';
        }

        const locationPanel = document.createElement('section');
        locationPanel.className = 'owner-place-edit-panel';
        locationPanel.dataset.editPanel = 'location';
        if (addressSection) locationPanel.appendChild(addressSection);
        if (mapSection) locationPanel.appendChild(mapSection);

        root.innerHTML = '';
        root.appendChild(tabs);
        root.appendChild(basicPanel);
        root.appendChild(detailsPanel);
        root.appendChild(locationPanel);

        const activatePanel = function (panelName) {
            tabs.querySelectorAll('.owner-place-edit-tab').forEach(button => {
                const isActive = button.dataset.editPanel === panelName;
                button.classList.toggle('is-active', isActive);
                button.setAttribute('aria-selected', isActive ? 'true' : 'false');
            });

            root.querySelectorAll('.owner-place-edit-panel').forEach(panel => {
                panel.classList.toggle('is-active', panel.dataset.editPanel === panelName);
            });

            if (panelName === 'location') {
                window.setTimeout(function () {
                    const mapElement = document.getElementById('googleMapPicker');
                    if (mapElement?.__ownerPlaceMap && window.google?.maps) {
                        google.maps.event.trigger(mapElement.__ownerPlaceMap, 'resize');
                        const center = mapElement.__ownerPlaceMap.getCenter();
                        if (center) mapElement.__ownerPlaceMap.setCenter(center);
                    }
                }, 80);
            }

            const htmlContainer = popup.querySelector('.swal2-html-container');
            if (htmlContainer) htmlContainer.scrollTop = 0;
        };

        tabs.addEventListener('click', function (event) {
            const button = event.target.closest('.owner-place-edit-tab');
            if (!button) return;
            activatePanel(button.dataset.editPanel);
        });
    }

    // 🌟 فتح نافذة تعديل المنشأة (النسخة الذكية: قراءة دقيقة جداً، أرقام إنجليزية، وحفظ لا يضيع الخيارات) 🌟
    window.openEditPlaceModal = function (placeId) {
        fetch(`/Owner/Place/GetPlaceForEdit?placeId=${placeId}&_t=${new Date().getTime()}`).then(res => res.json()).then(data => {
            if (!data.success) { Swal.fire('خطأ', 'فشل جلب بيانات المنشأة', 'error'); return; }
            const place = data.place;

            let currentLat = 15.369445; let currentLng = 44.191006;
            let savedCoords = place.coordinates || place.Coordinates;
            if (savedCoords) {
                let cleanCoords = String(savedCoords).replace(/[^\d.,-]/g, '');
                let parts = cleanCoords.split(',');
                if (parts.length === 4) { currentLat = parseFloat(parts[0] + "." + parts[1]); currentLng = parseFloat(parts[2] + "." + parts[3]); }
                else if (parts.length >= 2) { currentLat = parseFloat(parts[0]); currentLng = parseFloat(parts[1]); }
            } else if (place.latitude || place.Latitude) {
                currentLat = parseFloat(place.latitude || place.Latitude); currentLng = parseFloat(place.longitude || place.Longitude);
            }
            if (isNaN(currentLat)) currentLat = 15.369445;
            if (isNaN(currentLng)) currentLng = 44.191006;

            let fullDesc = place.description || '';
            let briefDesc = fullDesc; let detailedDesc = '';
            if (fullDesc.includes('--- تفاصيل المنشأة ---')) {
                let parts = fullDesc.split('--- تفاصيل المنشأة ---');
                briefDesc = parts[0].trim(); detailedDesc = parts[1].trim();
            }

            let dataMap = {};
            let parsedGroups = { events: [], seating: [], ent: [], srv: [], hvac: [], outdoor: [], extra: [] };

            // 🧠 المستخرج الذكي (Regex) لضمان قراءة الخيارات مهما كانت (حتى لو حرف واحد مثل h و f)
            function extractList(descText, keyword) {
                let regex = new RegExp(keyword + "\\s*:\\s*([^\\n]+)");
                let match = descText.match(regex);
                if (match && match[1]) {
                    // split بالشرطة وإزالة الفراغات، وقبول أي كلمة طولها حرف واحد أو أكثر
                    return match[1].split('-').map(i => i.trim()).filter(i => i.length >= 1);
                }
                return [];
            }

            // 🌟 دالة قوية جداً لتحويل أي رقم عربي أو فارسي إلى إنجليزي
            const toEngNum = (str) => {
                if (!str) return '';
                return String(str)
                    .replace(/[٠-٩]/g, d => '٠١٢٣٤٥٦٧٨٩'.indexOf(d))
                    .replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d));
            };

            if (detailedDesc) {
                // استخراج الأرقام وتحويلها فوراً إلى إنجليزي باستخدام الدالة الجديدة
                dataMap['h_area'] = toEngNum((detailedDesc.match(/المساحة:\s*([^\|\n\-]+)/) || [])[1]?.trim() || '');
                dataMap['h_capacity'] = toEngNum((detailedDesc.match(/السعة:\s*(\d+)/) || [])[1] || '');
                dataMap['h_tables'] = toEngNum((detailedDesc.match(/عدد الطاولات:\s*(\d+)/) || [])[1] || '');

                dataMap['c_rooms'] = toEngNum((detailedDesc.match(/غرف\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['c_baths'] = toEngNum((detailedDesc.match(/حمامات\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['c_kitchens'] = toEngNum((detailedDesc.match(/مطابخ\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['c_majlis'] = toEngNum((detailedDesc.match(/مجالس\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['c_halls'] = toEngNum((detailedDesc.match(/صالات استقبال\s*\(\s*(\d+)\s*\)/) || [])[1] || '');

                dataMap['f_bathrooms'] = toEngNum((detailedDesc.match(/حمامات\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['f_acs'] = toEngNum((detailedDesc.match(/مكيفات\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['f_storage'] = toEngNum((detailedDesc.match(/غرف تخزين\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['f_prep'] = toEngNum((detailedDesc.match(/غرف تحضير\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['f_kitchens'] = toEngNum((detailedDesc.match(/مطابخ\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['f_prayer'] = toEngNum((detailedDesc.match(/غرف صلاة\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['f_sound'] = toEngNum((detailedDesc.match(/سماعات\s*\(\s*(\d+)\s*\)/) || [])[1] || '');
                dataMap['f_parking'] = toEngNum((detailedDesc.match(/مواقف\s*\(\s*(\d+)\s*\)/) || [])[1] || '');

                // 🌟 سحب القوائم بذكاء فائق ووضعها في الصناديق المخصصة
                parsedGroups.events = extractList(detailedDesc, 'المناسبات');
                parsedGroups.seating = extractList(detailedDesc, 'المقاعد');
                parsedGroups.ent = extractList(detailedDesc, 'الترفيه والألعاب');
                parsedGroups.srv = extractList(detailedDesc, 'الخدمات المجانية');
                parsedGroups.hvac = extractList(detailedDesc, 'التدفئة والتبريد');
                parsedGroups.outdoor = extractList(detailedDesc, 'مرافق الحوش');

                let extra1 = extractList(detailedDesc, 'مرافق القاعة');
                let extra2 = extractList(detailedDesc, 'الخدمات والمرافق');
                parsedGroups.extra = [...extra1, ...extra2];
                parsedGroups.units = extractList(detailedDesc, 'الوحدات المتاحة');
            }




            let correctPhone = place.phoneNumber || place.PhoneNumber || place.phone || '';

            // 🌟 القراءة الدقيقة لمعرفة هل تدعم المنشأة الحجز أم لا
            let systemId = parseInt(place.systemId !== undefined ? place.systemId : (place.SystemId || 0));
            let hasBooking = place.hasBookingSystem === true || place.HasBookingSystem === true;
            let pName = place.placeNameAndDesc || place.name || '';

            // 🌟 التعديل هنا: نطبق التخمين (فقط) إذا كانت المنشأة تدعم الحجز فعلياً
            if (hasBooking) {
                if (systemId === 0 || systemId === 1) {
                    if (pName.includes('شاليه') || pName.includes('مزرع') || pName.includes('استراح')) systemId = 2;
                    else if (pName.includes('قاع') || pName.includes('صالت') || pName.includes('نادي') || pName.includes('مناسبات')) systemId = 3;
                    else systemId = 1;
                }
            } else {
                systemId = 0; // إجبار النظام على الإخفاء للمنشآت العامة (بدون حجز)
            }

            const showDetails = (systemId > 0);

            let dynamicFacilitiesHtml = ``;

            // =================================================================
            // 🏨 1. الفنادق
            // =================================================================
            if (systemId === 1) {
                dynamicFacilitiesHtml = `
                    <div class="p-4 border rounded-3 bg-light mb-3">
                        <h6 class="fw-bold text-primary mb-4 text-start"><i class="bi bi-stars"></i> الخدمات والمرافق المتوفرة</h6>
                        <div class="row g-3 px-2 mb-4 text-center">
                            <div class="col-4"><div class="form-check custom-checkbox d-inline-block"><input class="form-check-input chk-extra" type="checkbox" value="مسبح كبار" id="f_chk1"><label class="small fw-bold" for="f_chk1">مسبح كبار</label></div></div>
                            <div class="col-4"><div class="form-check custom-checkbox d-inline-block"><input class="form-check-input chk-extra" type="checkbox" value="مسبح أطفال" id="f_chk2"><label class="small fw-bold" for="f_chk2">مسبح أطفال</label></div></div>
                            <div class="col-4"><div class="form-check custom-checkbox d-inline-block"><input class="form-check-input chk-extra" type="checkbox" value="جاكوزي" id="f_chk3"><label class="small fw-bold" for="f_chk3">جاكوزي</label></div></div>
                        </div>
                        <div class="dynamic-field-container text-start">
                            <label class="small fw-bold text-muted mb-2">مرافق أخرى</label>
                            <div class="tags-container mb-2 d-flex flex-wrap gap-2" id="tagsBox_extra"></div>
                            <div class="input-group input-group-sm">
                                <button class="btn btn-primary shadow-none" type="button" onclick="window.addDynamicTag(this.nextElementSibling)"><i class="bi bi-plus-lg"></i></button>
                                <input type="text" class="form-control vip-input bg-white" placeholder="اكتب المرفق هنا واضغط الإضافة..." onkeypress="if(event.key === 'Enter') { event.preventDefault(); window.addDynamicTag(this); }">
                            </div>
                        </div>
                    </div>`;
            }




            // =================================================================
            // 🌴 2. الشاليهات والمزارع
            // =================================================================
            else if (systemId === 2) {
                dynamicFacilitiesHtml = `
                    <div class="row g-2 mb-4 text-center">
                        <div class="col">
                            <label class="small fw-bold text-muted mb-1 text-nowrap" style="font-size: 11px;">الغرف</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="c_rooms" class="form-control vip-input text-center fw-bold p-1" value="${dataMap['c_rooms'] || ''}" onfocus="this.select()">
                        </div>
                        <div class="col">
                            <label class="small fw-bold text-muted mb-1 text-nowrap" style="font-size: 11px;">الحمامات</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="c_baths" class="form-control vip-input text-center fw-bold p-1" value="${dataMap['c_baths'] || ''}" onfocus="this.select()">
                        </div>
                        <div class="col">
                            <label class="small fw-bold text-muted mb-1 text-nowrap" style="font-size: 11px;">المطابخ</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="c_kitchens" class="form-control vip-input text-center fw-bold p-1" value="${dataMap['c_kitchens'] || ''}" onfocus="this.select()">
                        </div>
                        <div class="col">
                            <label class="small fw-bold text-muted mb-1 text-nowrap" style="font-size: 11px;">المجالس</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="c_majlis" class="form-control vip-input text-center fw-bold p-1" value="${dataMap['c_majlis'] || ''}" onfocus="this.select()">
                        </div>
                        <div class="col">
                            <label class="small fw-bold text-muted mb-1 text-nowrap" style="font-size: 11px;">الصالات</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="c_halls" class="form-control vip-input text-center fw-bold p-1" value="${dataMap['c_halls'] || ''}" onfocus="this.select()">
                        </div>
                    </div>
                    <div class="row g-4 text-start">
                        <div class="col-md-6 dynamic-field-container d-flex flex-column">
                            <div class="unit-box flex-grow-1 mt-0">
                                <label class="small fw-bold text-primary mb-3 border-bottom pb-2 d-block"><i class="bi bi-controller me-1"></i> وسائل الترفيه والألعاب:</label>
                                <div class="row g-2 px-2 mb-3">
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="مسبح كبار"><label class="small fw-bold">مسبح كبار</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="ستيم"><label class="small fw-bold">ستيم</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="مسبح اطفال"><label class="small fw-bold">مسبح أطفال</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="جاكوزي"><label class="small fw-bold">جاكوزي</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="ساونا"><label class="small fw-bold">ساونا</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="سينما"><label class="small fw-bold">سينما</label></div></div>
                                    <div class="col-12"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="نظام صوتيات (سماعات)"><label class="small fw-bold">نظام صوتيات (سماعات)</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="شاشات"><label class="small fw-bold">شاشات</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="بلياردو"><label class="small fw-bold">بلياردو</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="شطرنج"><label class="small fw-bold">شطرنج</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="تنس طاولة"><label class="small fw-bold">تنس طاولة</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-ent" type="checkbox" value="لعبة جيم"><label class="small fw-bold">لعبة جيم</label></div></div>
                                </div>
                                <div class="tags-container mb-2 d-flex flex-wrap gap-2" id="tagsBox_ent"></div>
                                <div class="input-group input-group-sm mt-auto mb-1">
                                    <button class="btn btn-primary shadow-none" type="button" onclick="window.addDynamicTag(this.nextElementSibling)"><i class="bi bi-plus-lg"></i></button>
                                    <input type="text" class="form-control vip-input bg-white" placeholder="إضافة ترفيه آخر؟" onkeypress="if(event.key==='Enter'){event.preventDefault(); window.addDynamicTag(this);}">
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6 dynamic-field-container d-flex flex-column">
                            <div class="unit-box flex-grow-1 mt-0">
                                <label class="small fw-bold text-primary mb-3 border-bottom pb-2 d-block"><i class="bi bi-concierge-bell me-1"></i> الخدمات المجانية والمرافق:</label>
                                <div class="row g-2 px-2 mb-3">
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-srv" type="checkbox" value="انترنت مجاني"><label class="small fw-bold">إنترنت مجاني</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-srv" type="checkbox" value="كفتيريا"><label class="small fw-bold">كافتيريا</label></div></div>
                                    <div class="col-12"><div class="form-check custom-checkbox"><input class="form-check-input chk-srv" type="checkbox" value="موقف سيارات داخلي"><label class="small fw-bold">مواقف سيارات داخلي</label></div></div>
                                    <div class="col-12"><div class="form-check custom-checkbox"><input class="form-check-input chk-srv" type="checkbox" value="نظافة كاملة بعد حجز"><label class="small fw-bold">نظافة كاملة</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-srv" type="checkbox" value="بقالة"><label class="small fw-bold">بقالة قريبة</label></div></div>
                                </div>
                                <div class="tags-container mb-2 d-flex flex-wrap gap-2" id="tagsBox_srv"></div>
                                <div class="input-group input-group-sm mt-auto mb-1">
                                    <button class="btn btn-primary shadow-none" type="button" onclick="window.addDynamicTag(this.nextElementSibling)"><i class="bi bi-plus-lg"></i></button>
                                    <input type="text" class="form-control vip-input bg-white" placeholder="إضافة خدمة أخرى؟" onkeypress="if(event.key==='Enter'){event.preventDefault(); window.addDynamicTag(this);}">
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6 dynamic-field-container d-flex flex-column">
                            <div class="unit-box flex-grow-1 mt-0">
                                <label class="small fw-bold text-primary mb-3 border-bottom pb-2 d-block"><i class="bi bi-thermometer-half me-1"></i> التبريد والتدفئة:</label>
                                <div class="row g-2 px-2 mb-3">
                                    <div class="col-12"><div class="form-check custom-checkbox"><input class="form-check-input chk-hvac" type="checkbox" value="مكيف مركزي"><label class="small fw-bold">مكيف مركزي</label></div></div>
                                    <div class="col-12"><div class="form-check custom-checkbox"><input class="form-check-input chk-hvac" type="checkbox" value="مكيف عادي"><label class="small fw-bold">مكيف عادي</label></div></div>
                                    <div class="col-12"><div class="form-check custom-checkbox"><input class="form-check-input chk-hvac" type="checkbox" value="مروحة سقف"><label class="small fw-bold">مروحة سقف</label></div></div>
                                </div>
                                <div class="tags-container mb-2 d-flex flex-wrap gap-2" id="tagsBox_hvac"></div>
                                <div class="input-group input-group-sm mt-auto mb-1">
                                    <button class="btn btn-primary shadow-none" type="button" onclick="window.addDynamicTag(this.nextElementSibling)"><i class="bi bi-plus-lg"></i></button>
                                    <input type="text" class="form-control vip-input bg-white" placeholder="أنظمة أخرى؟" onkeypress="if(event.key==='Enter'){event.preventDefault(); window.addDynamicTag(this);}">
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6 dynamic-field-container d-flex flex-column">
                            <div class="unit-box flex-grow-1 mt-0">
                                <label class="small fw-bold text-primary mb-3 border-bottom pb-2 d-block"><i class="bi bi-tree me-1"></i> مرافق الحوش الخارجي:</label>
                                <div class="row g-2 px-2 mb-3">
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-outdoor" type="checkbox" value="حديقة خارجية"><label class="small fw-bold">حديقة خارجية</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-outdoor" type="checkbox" value="بلكونة"><label class="small fw-bold">بلكونة</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-outdoor" type="checkbox" value="شواية باربكيو"><label class="small fw-bold">شواية باربكيو</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-outdoor" type="checkbox" value="ملعب كرة قدم"><label class="small fw-bold">ملعب قدم</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-outdoor" type="checkbox" value="العاب اطفال"><label class="small fw-bold">ألعاب أطفال</label></div></div>
                                    <div class="col-6"><div class="form-check custom-checkbox"><input class="form-check-input chk-outdoor" type="checkbox" value="ملعب كرة طائرة"><label class="small fw-bold">ملعب طائرة</label></div></div>
                                </div>
                                <div class="tags-container mb-2 d-flex flex-wrap gap-2" id="tagsBox_outdoor"></div>
                                <div class="input-group input-group-sm mt-auto mb-1">
                                    <button class="btn btn-primary shadow-none" type="button" onclick="window.addDynamicTag(this.nextElementSibling)"><i class="bi bi-plus-lg"></i></button>
                                    <input type="text" class="form-control vip-input bg-white" placeholder="إضافة مرفق آخر؟" onkeypress="if(event.key==='Enter'){event.preventDefault(); window.addDynamicTag(this);}">
                                </div>
                            </div>
                        </div>
                    </div>`;
            }




            // =================================================================
            // ✨ 3. القاعات 
            // =================================================================
            else if (systemId === 3 || systemId === 4) {
                dynamicFacilitiesHtml = `
                    <div class="row g-3 mb-4 text-center">
                        <div class="col-md-4">
                            <label class="small fw-bold text-muted mb-1">مساحة القاعة</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="h_area" class="form-control vip-input text-center fw-bold" value="${dataMap['h_area'] || ''}" onfocus="this.select()">
                        </div>
                        <div class="col-md-4">
                            <label class="small fw-bold text-muted mb-1">السعة (الأشخاص)</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="h_capacity" class="form-control vip-input text-center fw-bold" value="${dataMap['h_capacity'] || ''}" onfocus="this.select()">
                        </div>
                        <div class="col-md-4">
                            <label class="small fw-bold text-muted mb-1">سعة الطاولات</label>
                            <input type="text" inputmode="numeric" lang="en" dir="ltr" id="h_tables" class="form-control vip-input text-center fw-bold" value="${dataMap['h_tables'] || ''}" onfocus="this.select()">
                        </div>
                    </div>
                    <div class="row g-4 text-start">
                        <div class="col-md-6 dynamic-field-container d-flex flex-column">
                            <div class="unit-box flex-grow-1 mt-0">
                                <h6 class="fw-bold text-primary small mb-3">المناسبات المسموحة والمخصصة:</h6>
                                <div class="row g-2 small fw-bold text-secondary mb-3">
                                    <div class="col-6"><div class="form-check"><input class="form-check-input chk-events" type="checkbox" value="أعراس نساء"><label class="form-check-label">أعراس نساء</label></div></div>
                                    <div class="col-6"><div class="form-check"><input class="form-check-input chk-events" type="checkbox" value="أعراس رجال"><label class="form-check-label">أعراس رجال</label></div></div>
                                    <div class="col-6"><div class="form-check"><input class="form-check-input chk-events" type="checkbox" value="عزاء"><label class="form-check-label">عزاء</label></div></div>
                                    <div class="col-6"><div class="form-check"><input class="form-check-input chk-events" type="checkbox" value="حفل تخرج"><label class="form-check-label">حفل تخرج</label></div></div>
                                    <div class="col-6"><div class="form-check"><input class="form-check-input chk-events" type="checkbox" value="مناسبات شركات"><label class="form-check-label">مناسبات شركات</label></div></div>
                                    <div class="col-6"><div class="form-check"><input class="form-check-input chk-events" type="checkbox" value="معارض"><label class="form-check-label">معارض</label></div></div>
                                    <div class="col-6"><div class="form-check"><input class="form-check-input chk-events" type="checkbox" value="حفلات خاصة"><label class="form-check-label">حفلات خاصة</label></div></div>
                                </div>
                                <div class="tags-container mb-2 d-flex flex-wrap gap-2" id="tagsBox_events"></div>
                                <div class="input-group input-group-sm mt-auto mb-1">
                                    <button class="btn btn-primary shadow-none" type="button" onclick="window.addDynamicTag(this.nextElementSibling)"><i class="bi bi-plus-lg"></i></button>
                                    <input type="text" class="form-control vip-input bg-white" placeholder="مناسبة أخرى؟" onkeypress="if(event.key==='Enter'){event.preventDefault(); window.addDynamicTag(this);}">
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6 dynamic-field-container d-flex flex-column">
                            <div class="unit-box flex-grow-1 mt-0">
                                <h6 class="fw-bold text-primary small mb-3">تجهيزات الجلوس والمقاعد:</h6>
                                <div class="row g-2 small fw-bold text-secondary mb-3">
                                    <div class="col-12"><div class="form-check"><input class="form-check-input chk-seating" type="checkbox" value="نظام مجالس عربية"><label class="form-check-label">نظام مجالس عربية</label></div></div>
                                    <div class="col-12"><div class="form-check"><input class="form-check-input chk-seating" type="checkbox" value="طاولات ملكية مع كراسي"><label class="form-check-label">طاولات ملكية مع كراسي</label></div></div>
                                    <div class="col-12"><div class="form-check"><input class="form-check-input chk-seating" type="checkbox" value="ترتيب مسرحي (كراسي فقط)"><label class="form-check-label">ترتيب مسرحي (كراسي فقط)</label></div></div>
                                </div>
                                <div class="tags-container mb-2 d-flex flex-wrap gap-2" id="tagsBox_seating"></div>
                                <div class="input-group input-group-sm mt-auto mb-1">
                                    <button class="btn btn-primary shadow-none" type="button" onclick="window.addDynamicTag(this.nextElementSibling)"><i class="bi bi-plus-lg"></i></button>
                                    <input type="text" class="form-control vip-input bg-white" placeholder="تجهيز آخر؟" onkeypress="if(event.key==='Enter'){event.preventDefault(); window.addDynamicTag(this);}">
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="unit-box mt-4 text-center">
                        <h6 class="fw-bold text-primary mb-3 text-center"><i class="bi bi-box-seam me-1"></i> التجهيزات والمرافق المساندة</h6>
                        <div class="row g-3 text-center">
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">دورات المياه</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_bathrooms" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_bathrooms'] || ''}" onfocus="this.select()">
                            </div>
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">أجهزة التكييف</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_acs" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_acs'] || ''}" onfocus="this.select()">
                            </div>
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">غرف التخزين</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_storage" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_storage'] || ''}" onfocus="this.select()">
                            </div>
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">غرف التحضير</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_prep" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_prep'] || ''}" onfocus="this.select()">
                            </div>
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">المطابخ المركزية</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_kitchens" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_kitchens'] || ''}" onfocus="this.select()">
                            </div>
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">غرف الصلاة</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_prayer" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_prayer'] || ''}" onfocus="this.select()">
                            </div>
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">أنظمة الصوت</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_sound" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_sound'] || ''}" onfocus="this.select()">
                            </div>
                            <div class="col-3">
                                <label class="small fw-bold text-muted mb-1" style="font-size: 11px;">مواقف السيارات</label>
                                <input type="text" inputmode="numeric" lang="en" dir="ltr" id="f_parking" class="form-control form-control-sm text-center fw-bold" value="${dataMap['f_parking'] || ''}" onfocus="this.select()">
                            </div>
                        </div>
                    </div>`;
            }


            Swal.fire({
                title: `<div class="owner-place-edit-heading">
                            <span class="owner-place-edit-heading__icon"><i class="bi bi-pencil-square"></i></span>
                            <span><strong>تعديل بيانات المنشأة</strong><small>${place.placeNameAndDesc || 'المنشأة'}</small></span>
                        </div>`,
                width: '880px',
                showCloseButton: true,
                showCancelButton: true,
                allowOutsideClick: false,
                buttonsStyling: false,
                confirmButtonColor: '#00A6DA', cancelButtonColor: '#6c757d',
                customClass: {
                    popup: 'owner-place-edit-workspace',
                    title: 'owner-place-edit-workspace__title',
                    htmlContainer: 'owner-place-edit-workspace__body',
                    actions: 'owner-place-edit-workspace__actions',
                    confirmButton: 'owner-place-edit-workspace__save',
                    cancelButton: 'owner-place-edit-workspace__cancel'
                },
                html: `
    <div class="owner-place-edit-root text-start" dir="rtl">
        <div class="row g-3 mb-3">
            <div class="col-md-7">
                <label class="form-label fw-bold small text-muted">اسم المنشأة التجاري</label>
                <input id="edit-name" class="form-control vip-input-group p-2 fw-bold" value="${place.placeNameAndDesc}">
            </div>
            <div class="col-md-5">
                <label class="form-label fw-bold small text-muted">رقم الحجوزات (المنشأة)</label>
                <div dir="ltr" class="phone-wrapper-ltr premium-input-group-phone w-100">
                    <input id="edit-phone-display" type="tel" class="form-control phone-input-vip w-100" value="${correctPhone}">
                    <input type="hidden" id="edit-phone" value="${correctPhone}">
                </div>
                <span id="editPhoneMsg" class="small mt-1 text-end d-block fw-bold" dir="rtl"></span>
            </div>
            
            <div class="col-12 mb-2">
                <label class="form-label fw-bold small text-muted">النبذة المختصرة (الوصف العام)</label>
                <textarea id="edit-brief-desc" class="form-control vip-input-group p-2" rows="3" placeholder="اكتب نبذة مختصرة وجذابة...">${briefDesc}</textarea>
            </div>
            
            <div class="col-12 mt-3 p-0" style="display: ${showDetails ? 'block' : 'none'} !important;">
                ${dynamicFacilitiesHtml}
            </div>
        </div>
        
        <div class="col-md-12 mb-3 text-start mt-4">
            <div class="vip-map-container bg-white p-3 border rounded-4 shadow-sm">
                <div class="d-flex justify-content-between align-items-center bg-light p-3 rounded-3 mb-3 border">
                    <div class="d-flex align-items-center gap-3">
                        <div class="bg-white rounded-circle shadow-sm d-flex align-items-center justify-content-center flex-shrink-0" style="width: 40px; height: 40px;"><i class="bi bi-geo-alt-fill fs-5 text-primary"></i></div>
                        <div><h6 class="mb-0 fw-bold text-dark">تحديث موقع المنشأة</h6><small class="text-muted">حرك الدبوس أو أدخل الإحداثيات يدوياً</small></div>
                    </div>
                </div>
                <div class="row g-3 mb-3">
                    <div class="col-md-6"><label class="small fw-bold text-muted mb-1">خط الطول (Longitude)</label><input type="text" id="edit-lng-display" lang="en" dir="ltr" class="form-control vip-input text-center fw-bold shadow-sm" value="${parseFloat(currentLng).toFixed(6)}"><input type="hidden" id="edit-lng" value="${currentLng}"></div>
                    <div class="col-md-6"><label class="small fw-bold text-muted mb-1">خط العرض (Latitude)</label><input type="text" id="edit-lat-display" lang="en" dir="ltr" class="form-control vip-input text-center fw-bold shadow-sm" value="${parseFloat(currentLat).toFixed(6)}"><input type="hidden" id="edit-lat" value="${currentLat}"></div>
                </div>
                <div id="googleMapPicker" class="shadow-sm" style="height: 250px; width: 100%; border-radius: 12px; border: 2px solid #eef2f6; overflow: hidden; z-index: 1;"></div>
            </div>
        </div>
        <div class="mb-2">
            <label class="form-label fw-bold small text-muted">العنوان التفصيلي النصي</label>
            <input id="edit-address" class="form-control vip-input-group p-2" value="${place.addressDetail || ''}">
        </div>
    </div>`,
                didOpen: () => {
                    // 🌟 دالة تنظيف النص العربي لضمان تطابق الكلمات بنسبة 100%
                    const cleanText = (txt) => {
                        if (!txt) return '';
                        return txt.replace(/[أإآ]/g, 'ا')
                            .replace(/ة/g, 'ه')
                            .replace(/ى/g, 'ي')
                            .replace(/\s+/g, ' ')
                            .trim();
                    };

                    // 🌟 دالة التأشير الذكية (المحدثة)
                    const checkAndTag = (list, chkClass, boxId) => {
                        const popup = document.querySelector('.swal2-popup');
                        if (!popup) return;
                        const box = popup.querySelector('#' + boxId);

                        list.forEach(item => {
                            let matched = false;
                            let cleanItem = cleanText(item); // تنظيف القيمة القادمة من قاعدة البيانات

                            popup.querySelectorAll('.' + chkClass).forEach(chk => {
                                let cleanChk = cleanText(chk.value); // تنظيف القيمة الموجودة في الشاشة

                                // مقارنة مرنة جداً
                                if (cleanChk === cleanItem || cleanChk.includes(cleanItem) || cleanItem.includes(cleanChk)) {
                                    chk.checked = true;
                                    matched = true;
                                }
                            });

                            // 🌟 إذا لم يجد العنصر في المربعات (يعني أن المالك أضافه يدوياً)، أضفه كـ Tag أزرق
                            if (!matched && box && item.length >= 1) {
                                const tag = document.createElement('span');
                                tag.className = 'badge bg-primary text-white p-2 d-flex align-items-center shadow-sm mt-1';
                                tag.style.fontSize = '14px';
                                tag.innerHTML = `${item} <i class="bi bi-x-circle-fill ms-2 text-light" style="cursor: pointer;" onclick="this.parentElement.remove()"></i><input type="hidden" value="${item}">`;
                                box.appendChild(tag);
                            }
                        });
                    };

                    // استدعاء الدوال بعد تأخير بسيط لضمان تحميل النافذة
                    setTimeout(() => {
                        checkAndTag(parsedGroups.events, 'chk-events', 'tagsBox_events');
                        checkAndTag(parsedGroups.seating, 'chk-seating', 'tagsBox_seating');
                        checkAndTag(parsedGroups.ent, 'chk-ent', 'tagsBox_ent');
                        checkAndTag(parsedGroups.srv, 'chk-srv', 'tagsBox_srv');
                        checkAndTag(parsedGroups.hvac, 'chk-hvac', 'tagsBox_hvac');
                        checkAndTag(parsedGroups.outdoor, 'chk-outdoor', 'tagsBox_outdoor');
                        checkAndTag(parsedGroups.extra, 'chk-extra', 'tagsBox_extra');
                        if (parsedGroups.units) checkAndTag(parsedGroups.units, 'chk-units', 'tagsBox_extra');
                    }, 150);



                    const editPhoneDisplay = document.getElementById('edit-phone-display');
                    window.editPhoneIti = null;
                    if (correctPhone && !correctPhone.startsWith('+') && correctPhone.replace(/[^0-9]/g, '').length === 9) { correctPhone = '+967' + correctPhone.replace(/[^0-9]/g, ''); }
                    if (editPhoneDisplay && typeof window.intlTelInput !== 'undefined') {
                        window.editPhoneIti = window.intlTelInput(editPhoneDisplay, { initialCountry: "ye", preferredCountries: ["ye", "sa", "ae", "eg"], separateDialCode: true, strictMode: true, utilsScript: "https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js" });
                        if (correctPhone) {
                            window.editPhoneIti.setNumber(correctPhone);
                            setTimeout(() => {
                                if (window.validatePhoneInput) window.validatePhoneInput(window.editPhoneIti, editPhoneDisplay, 'editPhoneMsg');
                                if (window.formatPhoneNumber) window.formatPhoneNumber(window.editPhoneIti, editPhoneDisplay);
                            }, 300);
                        }
                        editPhoneDisplay.addEventListener('input', function () { window.validatePhoneInput(window.editPhoneIti, editPhoneDisplay, 'editPhoneMsg'); });
                        editPhoneDisplay.addEventListener('countrychange', function () { window.validatePhoneInput(window.editPhoneIti, editPhoneDisplay, 'editPhoneMsg'); window.formatPhoneNumber(window.editPhoneIti, editPhoneDisplay); });
                        editPhoneDisplay.addEventListener('blur', function () { window.formatPhoneNumber(window.editPhoneIti, editPhoneDisplay); });
                    }

                    const latDisplay = document.getElementById('edit-lat-display'), lngDisplay = document.getElementById('edit-lng-display'), latHidden = document.getElementById('edit-lat'), lngHidden = document.getElementById('edit-lng');
                    function syncManualInputsOffline() { let newLat = parseFloat(latDisplay.value), newLng = parseFloat(lngDisplay.value); if (!isNaN(newLat) && !isNaN(newLng)) { latHidden.value = newLat; lngHidden.value = newLng; } }
                    latDisplay.addEventListener('input', syncManualInputsOffline); lngDisplay.addEventListener('input', syncManualInputsOffline);

                    if (typeof google !== 'undefined' && typeof google.maps !== 'undefined') {
                        const mapCenter = { lat: parseFloat(currentLat), lng: parseFloat(currentLng) };
                        const mapElement = document.getElementById("googleMapPicker");
                        const map = new google.maps.Map(mapElement, { zoom: 15, center: mapCenter, mapTypeControl: false, streetViewControl: false, fullscreenControl: true, restriction: { latLngBounds: { north: 19.5000, south: 12.0000, west: 41.5000, east: 55.0000 }, strictBounds: true } });
                        mapElement.__ownerPlaceMap = map;
                        const marker = new google.maps.Marker({ position: mapCenter, map: map, draggable: true, animation: google.maps.Animation.DROP });
                        marker.addListener("dragend", (event) => { latHidden.value = event.latLng.lat().toFixed(6); lngHidden.value = event.latLng.lng().toFixed(6); latDisplay.value = latHidden.value; lngDisplay.value = lngHidden.value; });
                        map.addListener("click", (event) => { marker.setPosition(event.latLng); latHidden.value = event.latLng.lat().toFixed(6); lngHidden.value = event.latLng.lng().toFixed(6); latDisplay.value = latHidden.value; lngDisplay.value = lngHidden.value; });
                        function moveMapMarker() { let newLat = parseFloat(latDisplay.value), newLng = parseFloat(lngDisplay.value); if (!isNaN(newLat) && !isNaN(newLng)) { let newPos = { lat: newLat, lng: newLng }; marker.setPosition(newPos); map.setCenter(newPos); } }
                        latDisplay.addEventListener('change', moveMapMarker); lngDisplay.addEventListener('change', moveMapMarker);
                        setTimeout(() => { google.maps.event.trigger(map, 'resize'); map.setCenter(mapCenter); }, 400);
                    }

                    window.setTimeout(initializeCompactEditModal, 0);
                },
                confirmButtonText: '<i class="bi bi-check2-circle me-1"></i> حفظ التعديلات', cancelButtonText: 'إلغاء',
                preConfirm: () => {
                    const popup = document.querySelector('.swal2-popup');
                    if (!popup) return false;

                    const editPhoneDisplay = document.getElementById('edit-phone-display');
                    let finalPhone = editPhoneDisplay.value;
                    if (window.editPhoneIti && window.isStrictlyValidPhone(window.editPhoneIti, editPhoneDisplay)) finalPhone = window.editPhoneIti.getNumber();
                    else { Swal.showValidationMessage('عذراً، رقم الهاتف المدخل وهمي أو لا يطابق الدولة المحددة!'); return false; }

                    const finalLat = parseFloat(document.getElementById('edit-lat').value), finalLng = parseFloat(document.getElementById('edit-lng').value);
                    if (isNaN(finalLat) || isNaN(finalLng)) { Swal.showValidationMessage('عذراً، يجب إدخال خط الطول والعرض!'); return false; }

                    let brief = document.getElementById('edit-brief-desc').value.trim();
                    let finalParts = [];

                    // 🌟 الحفظ الذكي: جمع الخيارات وعدم إضاعة المربعات الإضافية (Tags) 🌟
                    if (systemId === 2) {
                        let buildingParts = [];
                        let bIds = ['c_rooms', 'c_baths', 'c_kitchens', 'c_majlis', 'c_halls'];
                        let bKeys = ['غرف', 'حمامات', 'مطابخ', 'مجالس', 'صالات استقبال'];
                        bIds.forEach((id, idx) => { let el = document.getElementById(id); if (el && el.value && parseInt(el.value) > 0) buildingParts.push(`${bKeys[idx]} (${el.value})`); });
                        if (buildingParts.length > 0) finalParts.push(`تفاصيل المبنى: ${buildingParts.join(' | ')}`);

                        let ent = Array.from(popup.querySelectorAll('.chk-ent:checked')).map(c => c.value);
                        popup.querySelectorAll('#tagsBox_ent input[type="hidden"]').forEach(i => { if (i.value) ent.push(i.value); });
                        if (ent.length > 0) finalParts.push(`الترفيه والألعاب: ${ent.join(' - ')}`);

                        let srv = Array.from(popup.querySelectorAll('.chk-srv:checked')).map(c => c.value);
                        popup.querySelectorAll('#tagsBox_srv input[type="hidden"]').forEach(i => { if (i.value) srv.push(i.value); });
                        if (srv.length > 0) finalParts.push(`الخدمات المجانية: ${srv.join(' - ')}`);

                        let hvac = Array.from(popup.querySelectorAll('.chk-hvac:checked')).map(c => c.value);
                        popup.querySelectorAll('#tagsBox_hvac input[type="hidden"]').forEach(i => { if (i.value) hvac.push(i.value); });
                        if (hvac.length > 0) finalParts.push(`التدفئة والتبريد: ${hvac.join(' - ')}`);

                        let outdoor = Array.from(popup.querySelectorAll('.chk-outdoor:checked')).map(c => c.value);
                        popup.querySelectorAll('#tagsBox_outdoor input[type="hidden"]').forEach(i => { if (i.value) outdoor.push(i.value); });
                        if (outdoor.length > 0) finalParts.push(`مرافق الحوش: ${outdoor.join(' - ')}`);

                    } else if (systemId === 3 || systemId === 4) {
                        let hParts = [];
                        let hArea = document.getElementById('h_area'); if (hArea && hArea.value) hParts.push(`المساحة: ${hArea.value}`);
                        let hCap = document.getElementById('h_capacity'); if (hCap && hCap.value) hParts.push(`السعة: ${hCap.value} شخص`);
                        let hTab = document.getElementById('h_tables'); if (hTab && hTab.value) hParts.push(`عدد الطاولات: ${hTab.value}`);
                        if (hParts.length > 0) finalParts.push(hParts.join(' | '));

                        let ev = Array.from(popup.querySelectorAll('.chk-events:checked')).map(c => c.value);
                        popup.querySelectorAll('#tagsBox_events input[type="hidden"]').forEach(i => { if (i.value) ev.push(i.value); });
                        if (ev.length > 0) finalParts.push(`المناسبات: ${ev.join(' - ')}`);

                        let seat = Array.from(popup.querySelectorAll('.chk-seating:checked')).map(c => c.value);
                        popup.querySelectorAll('#tagsBox_seating input[type="hidden"]').forEach(i => { if (i.value) seat.push(i.value); });
                        if (seat.length > 0) finalParts.push(`المقاعد: ${seat.join(' - ')}`);

                        let fParts = [];
                        let fIds = ['f_bathrooms', 'f_acs', 'f_storage', 'f_prep', 'f_kitchens', 'f_prayer', 'f_sound', 'f_parking'];
                        let fKeys = ['حمامات', 'مكيفات', 'غرف تخزين', 'غرف تحضير', 'مطابخ', 'غرف صلاة', 'سماعات', 'مواقف'];
                        fIds.forEach((id, idx) => { let el = document.getElementById(id); if (el && el.value && parseInt(el.value) > 0) fParts.push(`${fKeys[idx]} (${el.value})`); });
                        if (fParts.length > 0) finalParts.push(`مرافق القاعة: ${fParts.join(' - ')}`);

                    } else if (systemId === 1) {
                        // سحب المرافق والخدمات فقط
                        let extra = Array.from(popup.querySelectorAll('.chk-extra:checked')).map(c => c.value);
                        popup.querySelectorAll('#tagsBox_extra input[type="hidden"]').forEach(i => { if (i.value) extra.push(i.value); });
                        if (extra.length > 0) finalParts.push(`الخدمات والمرافق: ${extra.join(' - ')}`);
                    }


                    let detail = finalParts.join('\n');
                    let finalDescription = detail ? (brief + "\n\n--- تفاصيل المنشأة ---\n" + detail) : brief;

                    return { PlaceId: placeId, PlaceNameAndDesc: document.getElementById('edit-name').value, Description: finalDescription, AddressDetail: document.getElementById('edit-address').value, PhoneNumber: finalPhone, Latitude: finalLat, Longitude: finalLng };
                }
            }).then(result => {
                if (result.isConfirmed) {
                    Swal.fire({ title: 'جاري الحفظ...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

                    fetch('/Owner/Place/EditPlace', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(result.value) })
                        .then(res => res.json()).then(res => {
                            if (res.success) {
                                // 1. إغلاق نافذة التحميل فوراً
                                Swal.close();
                                clearOwnerPlaceModalContext();

                                // إشعار نجاح صغير مستقل عن تنسيقات مودال التعديل.
                                Swal.mixin({
                                    toast: true,
                                    position: 'top-end',
                                    showConfirmButton: false,
                                    timer: 1900,
                                    timerProgressBar: true,
                                    customClass: { popup: 'owner-place-success-toast' }
                                }).fire({
                                    icon: 'success',
                                    title: 'تم تحديث بيانات المنشأة'
                                });

                                if (editorConfig.reloadAfterSave) {
                                    window.setTimeout(() => window.location.reload(), 650);
                                    return;
                                }

                                // تحديث صامت ومباشر للكروت بدون تعتيم أو شفافية للصفحة.
                                const targetClass = localStorage.getItem('activePlaceTab') || 'place-list-tab';
                                const targetBtn = document.querySelector(`.custom-tabs-container .tab-btn[data-target="${targetClass}"]`);
                                const endpointUrl = targetBtn?.getAttribute('data-url');
                                const targetContent = document.querySelector('.' + targetClass);

                                if (endpointUrl && targetContent) {
                                    fetch(endpointUrl)
                                        .then(response => response.text())
                                        .then(htmlMarkup => {
                                            targetContent.innerHTML = htmlMarkup;
                                            targetContent.style.removeProperty('opacity');
                                            targetContent.style.removeProperty('transition');

                                            const reinitMap = {
                                                'place-list-tab': window.initPlaceList,
                                                'details-manager-tab': window.initDetailsManager,
                                                'add-place-tab': window.initAddPlace,
                                                'calendar-manager-tab': window.initPremiumCalendar,
                                                'promotions-manager-tab': window.initPromotionsManager
                                            };

                                            const reinit = reinitMap[targetClass];
                                            if (typeof reinit === 'function') {
                                                reinit();
                                            }
                                        });
                                }
                            } // 🌟 1. هذا هو القوس الذي كان مفقوداً!
                            else {
                                Swal.fire('خطأ', res.message, 'error');
                            }
                        }).catch(err => {
                            // تم وضع طباعة للخطأ الحقيقي في الكونسول لتتبع أي مشاكل مستقبلية
                            console.error(err);
                            Swal.fire('خطأ', 'فشل الاتصال بالخادم', 'error');
                        });
                }
            });
        });
    };

    // 📸 إدارة الصور الذكية للمنشأة (مع توسيط الصور وتأكيد الحفظ)
    window.openManagePlaceImagesModal = function (placeId) {
        Swal.fire({ title: 'جاري تحميل الصور...', didOpen: () => Swal.showLoading(), allowOutsideClick: false });

        fetch(`/Owner/Place/GetPlaceImages?placeId=${placeId}`)
            .then(res => res.json())
            .then(data => {
                if (!data.success) { Swal.fire('خطأ', 'فشل جلب بيانات الصور', 'error'); return; }

                let html = `<div class="text-start" dir="rtl">`;
                html += `<h6 class="fw-bold text-primary mb-2"><i class="bi bi-card-image me-1"></i> صورة الغلاف:</h6>`;
                html += `<div id="cover-container-${placeId}" class="mb-3 text-center">`; // توسيط الغلاف

                if (data.coverImage) {
                    html += `<div class="position-relative d-inline-block border p-1 rounded bg-light" style="width: 100%;">
                    <img src="${data.coverImage}" class="rounded w-100 object-fit-cover" style="max-height: 150px;">
                    <button class="btn btn-danger btn-sm position-absolute top-0 end-0 m-2 rounded-circle shadow" style="width: 25px; height: 25px; padding: 0;" onclick="deletePlaceImageDirect(${placeId}, 0, true, this)">
                        <i class="bi bi-x-lg" style="font-size: 12px;"></i>
                    </button>
                 </div>`;
                } else {
                    html += `<div class="border border-dashed p-3 text-center rounded">
                    <p class="small text-muted mb-2">لا توجد صورة غلاف</p>
                    <label class="btn btn-sm btn-outline-primary fw-bold">
                        <i class="bi bi-camera-fill me-1"></i> إضافة غلاف
                        <input type="file" class="d-none" accept="image/*" onchange="uploadSingleCover(${placeId}, this)">
                    </label>
                 </div>`;
                }
                html += `</div>`;

                html += `<h6 class="fw-bold text-primary mb-2"><i class="bi bi-images me-1"></i> صور المنشأة الإضافية:</h6>`;
                if (data.gallery && data.gallery.length > 0) {
                    // 🌟 الحل لمشكلة الفراغ: إضافة justify-content-center لتوسيط الصور
                    html += `<div class="d-flex flex-wrap justify-content-center gap-2 mb-3">`;
                    data.gallery.forEach(img => {
                        html += `
            <div class="position-relative border p-1 rounded bg-light" id="gallery-img-${img.id}"> <img src="${img.path}" class="rounded" style="width: 80px; height: 80px; object-fit: cover;">
                <button class="btn btn-danger btn-sm position-absolute top-0 end-0 m-1 rounded-circle shadow" style="width: 20px; height: 20px; padding: 0;" onclick="deletePlaceImageDirect(${placeId}, ${img.id}, false, this)">
                    <i class="bi bi-x-lg" style="font-size: 10px;"></i>
                </button>
            </div>`;
                    });
                    html += `</div>`;
                } else {
                    html += `<p class="small text-muted mb-3 text-center">لا توجد صور إضافية.</p>`;
                }

                html += `
        <div class="border-top pt-3 mt-2">
            <label class="fw-bold text-dark small mb-2"><i class="bi bi-camera-fill text-primary me-1"></i> التقاط أو رفع صور جديدة</label>
            <input type="file" id="new-place-images" class="form-control border-primary bg-primary bg-opacity-10 fw-bold" multiple accept="image/*">
            <small class="text-muted mt-1 d-block fw-bold text-danger">الحد الأقصى للصور الإضافية معاً هو 10 ميجا (الـغلاف له مساحة مستقلة).</small>
        </div>
    </div>`;

                Swal.fire({
                    title: `<h5 class="fw-bold text-dark m-0">معرض صور المنشأة</h5>`,
                    html: html,
                    showCloseButton: true,
                    allowOutsideClick: false,
                    showCancelButton: true,
                    confirmButtonText: 'حفظ ورفع الصور',
                    cancelButtonText: 'إغلاق',
                    confirmButtonColor: '#00A6DA',
                    preConfirm: () => {
                        const files = document.getElementById('new-place-images').files;
                        if (files.length === 0) return true;

                        let totalNewSize = 0;
                        const maxTotalSize = 10 * 1024 * 1024;
                        for (let i = 0; i < files.length; i++) totalNewSize += files[i].size;

                        if (totalNewSize > maxTotalSize) {
                            let mbSize = (totalNewSize / (1024 * 1024)).toFixed(2);
                            Swal.showValidationMessage(`عذراً! الصور المحددة وحدها حجمها (${mbSize} ميجا) وتتجاوز الحد الأقصى للصور الإضافية (10 ميجا). يرجى تقليلها.`);
                            return false;
                        }

                        const formData = new FormData();
                        formData.append('placeId', placeId);
                        for (let i = 0; i < files.length; i++) { formData.append('newImages', files[i]); }

                        return fetch('/Owner/Place/UploadNewPlaceImages', { method: 'POST', body: formData })
                            .then(r => r.json())
                            .then(res => {
                                if (!res.success) { Swal.showValidationMessage(res.message); return false; }
                                return res;
                            });
                    }
                }).then(res => {
                    if (res.isConfirmed && res.value?.success) {
                        // رسالة نجاح صغيرة وغير حاجبة بدل المودال الكبير.
                        if (res.value.newCoverPath) {
                            const cardImg = document.getElementById(`card-img-${placeId}`);
                            if (cardImg) cardImg.src = res.value.newCoverPath;
                        }

                        clearOwnerPlaceModalContext();

                        Swal.mixin({
                            toast: true,
                            position: 'top-end',
                            showConfirmButton: false,
                            timer: 1900,
                            timerProgressBar: true,
                            customClass: {
                                popup: 'owner-place-success-toast'
                            }
                        }).fire({
                            icon: 'success',
                            title: 'تم الحفظ والرفع',
                            text: res.value.message || 'تم تحديث صور المنشأة بنجاح.'
                        });

                        // نعيد فتح إدارة الصور مباشرة بدون إجبار المستخدم على زر "حسناً".
                        window.setTimeout(() => {
                            openManagePlaceImagesModal(placeId);
                        }, 220);
                    }
                });
            });
    };


    // 🌟 دالة حذف صور المنشأة (تحديث الأرقام بنعومة واحترافية)
    window.deletePlaceImageDirect = function (placeId, imageId, isCover, triggerElement) {
        confirmOwnerPlaceImageDelete(
            'هل تريد حذف هذه الصورة؟ لن يتم إغلاق نافذة إدارة الصور بعد الحذف.',
            function () {
                let imgBox;

                if (isCover === true || isCover === 'true') {
                    imgBox = document.getElementById(`cover-container-${placeId}`);
                } else {
                    imgBox = document.getElementById(`gallery-img-${imageId}`);
                }

                if (imgBox) {
                    imgBox.style.opacity = '0.55';
                    imgBox.style.pointerEvents = 'none';
                }

                fetch(
                    `/Owner/Place/DeletePlaceImage?placeId=${placeId}&imageId=${imageId}&isCover=${isCover}`,
                    { method: 'POST' }
                )
                    .then(res => res.json())
                    .then(data => {
                        if (!data.success) {
                            if (imgBox) {
                                imgBox.style.opacity = '1';
                                imgBox.style.pointerEvents = '';
                            }

                            showOwnerPlaceImageActionToast(
                                data.message || 'تعذر حذف الصورة.',
                                'error'
                            );
                            return;
                        }

                        // تحديث نفس مودال إدارة الصور بدون إغلاقه.
                        if (isCover === true || isCover === 'true') {
                            if (imgBox) {
                                imgBox.innerHTML = `
                                    <div class="border border-dashed p-3 text-center rounded animate-fade-in">
                                        <p class="small text-muted mb-2">لا توجد صورة غلاف</p>
                                        <label class="btn btn-sm btn-outline-primary fw-bold">
                                            <i class="bi bi-camera-fill me-1"></i> إضافة غلاف
                                            <input type="file"
                                                   class="d-none"
                                                   accept="image/*"
                                                   onchange="uploadSingleCover(${placeId}, this)">
                                        </label>
                                    </div>
                                `;
                                imgBox.style.opacity = '1';
                                imgBox.style.pointerEvents = '';
                            }

                            const cardImg =
                                document.getElementById(`card-img-${placeId}`);

                            if (cardImg) {
                                cardImg.src = '/images/default-place.jpg';
                            }
                        } else if (imgBox) {
                            imgBox.remove();
                        }

                        // إذا كان المستخدم قد اختار صوراً جديدة قبل الحذف،
                        // نعيد حساب المساحة دون إغلاق مودال إدارة الصور.
                        const validationMessage = Swal.getValidationMessage();

                        if (
                            validationMessage &&
                            validationMessage.style.display !== 'none'
                        ) {
                            const currentExistingMb =
                                Number(data.currentSizeMb || 0);

                            let newFilesSize = 0;
                            const newImageInput =
                                document.getElementById('new-place-images');

                            if (newImageInput?.files) {
                                for (
                                    let i = 0;
                                    i < newImageInput.files.length;
                                    i++
                                ) {
                                    newFilesSize +=
                                        newImageInput.files[i].size;
                                }
                            }

                            const newMb =
                                Number(
                                    newFilesSize /
                                    (1024 * 1024)
                                );

                            const maxMb = 10;
                            const availableMb =
                                Math.max(
                                    0,
                                    maxMb - currentExistingMb
                                );

                            const grandTotalMb =
                                currentExistingMb + newMb;

                            if (grandTotalMb > maxMb) {
                                Swal.showValidationMessage(
                                    `عذراً! مساحة الصور الإضافية الحالية (${currentExistingMb.toFixed(2)} ميجا). ` +
                                    `المتبقي لك (${availableMb.toFixed(2)} ميجا) فقط، ` +
                                    `بينما الصور الجديدة حجمها (${newMb.toFixed(2)} ميجا). ` +
                                    'يرجى حذف صور إضافية قديمة لتفريغ مساحة.'
                                );

                                const popup = Swal.getPopup();
                                popup?.scrollTo({
                                    top: popup.scrollHeight,
                                    behavior: 'smooth'
                                });
                            } else {
                                Swal.resetValidationMessage();

                                showOwnerPlaceImageActionToast(
                                    'المساحة أصبحت كافية، ويمكنك متابعة رفع الصور.',
                                    'success'
                                );
                            }
                        } else {
                            showOwnerPlaceImageActionToast(
                                'تم حذف الصورة بنجاح.',
                                'success'
                            );
                        }

                        // لا Swal.fire هنا ولا إعادة فتح للمودال:
                        // المودال الحالي يبقى مفتوحاً حتى يغلقه المالك بنفسه.
                    })
                    .catch(() => {
                        if (imgBox) {
                            imgBox.style.opacity = '1';
                            imgBox.style.pointerEvents = '';
                        }

                        showOwnerPlaceImageActionToast(
                            'حدث خطأ في الاتصال بالخادم.',
                            'error'
                        );
                    });
            }
        );
    };



    window.uploadSingleCover = function (placeId, input) {
        if (input.files && input.files[0]) {
            const formData = new FormData();
            formData.append('placeId', placeId);
            formData.append('coverImage', input.files[0]);

            fetch('/Owner/Place/UploadCoverImage', { method: 'POST', body: formData })
                .then(r => r.json())
                .then(res => {
                    if (res.success) {
                        const cardImg = document.getElementById(`card-img-${placeId}`);
                        if (cardImg && res.newCoverPath) cardImg.src = res.newCoverPath;
                        Swal.mixin({ toast: true, timer: 1500, position: 'top-end', showConfirmButton: false }).fire({ icon: 'success', title: 'تم تحديث الغلاف' });
                        openManagePlaceImagesModal(placeId);
                    } else {
                        Swal.fire('خطأ', res.message || 'حدث خطأ', 'error');
                    }
                });
        }
    };

    const originalOpenEdit = window.openEditPlaceModal;
    const originalOpenImages = window.openManagePlaceImagesModal;
    const originalDeleteImage = window.deletePlaceImageDirect;
    const originalUploadCover = window.uploadSingleCover;

    const editorHost = document.querySelector('[data-place-editor-mode]');
    if (editorHost) {
        configure({ mode: editorHost.dataset.placeEditorMode || 'owner' });
    }

    const modalObserver = new MutationObserver(function () {
        if (!document.querySelector('.swal2-container')) {
            document.body.classList.remove(
                'owner-place-modal-context',
                'owner-place-edit-modal',
                'owner-place-images-modal'
            );
        }
    });
    modalObserver.observe(document.body, { childList: true, subtree: true });

    const api = {
        configure,
        openEdit(placeId) {
            document.body.classList.add('owner-place-modal-context', 'owner-place-edit-modal');
            return originalOpenEdit(placeId);
        },
        openImages(placeId) {
            document.body.classList.add('owner-place-modal-context', 'owner-place-images-modal');
            return originalOpenImages(placeId);
        },
        deleteImage: originalDeleteImage,
        uploadCover: originalUploadCover
    };

    function installGlobalAdapters() {
        window.YemenGuidePlaceEditor = api;
        window.openEditPlaceModal = placeId => api.openEdit(placeId);
        window.openManagePlaceImagesModal = placeId => api.openImages(placeId);
        window.deletePlaceImageDirect = (...args) => api.deleteImage(...args);
        window.uploadSingleCover = (...args) => api.uploadCover(...args);
    }

    installGlobalAdapters();
    document.addEventListener('DOMContentLoaded', installGlobalAdapters);
})(window, document);
