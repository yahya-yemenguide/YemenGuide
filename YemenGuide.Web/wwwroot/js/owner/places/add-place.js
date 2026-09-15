/**
 * DYNAMIC-PACKAGES-FINAL: باقات مخصصة متعددة مع التفعيل والطي والحذف وإعادة الفهرسة
 * STICKY-v2: زر إضافة الطابق ثابت أعلى التمرير والتنقل ثابت أسفل النافذة
 * علامة التحقق داخل الملف: wizardMain.appendChild(wizardNavigation)
 * =========================================================================
 * نسخة التصحيح المؤكدة: سويتشات أنواع الغرف + فتح وإغلاق غرف كل طابق
 * ابحثي داخل الملف عن: data-toggle-units
 * =========================================================================
 * =========================================================================
 * YemenGuide - إدارة المنشآت: إضافة منشأة
 * يحتوي معالج الإضافة، الهواتف، التحقق، الخريطة، الصور، الخطوات، والحقول الديناميكية.
 * تم فصله من manage-places.js دون تغيير الوظائف أو المسارات أو البيانات.
 * =========================================================================
 */

(function initializeAddPlaceModule() {

    /* ======================================================================
     * البيانات الجغرافية المشتركة بين المالك والأدمن
     * ====================================================================== */

    // المصدر الوحيد لحدود الجمهورية اليمنية المستخدمة في الخريطة والتحقق.
    window.yemenBounds = Object.freeze({
        north: 19.5000,
        south: 12.0000,
        west: 41.5000,
        east: 55.0000
    });

    // المصدر الوحيد لمراكز المحافظات. أي تعديل هنا يعمل عند المالك والأدمن معًا.
    window.yemenCities = Object.freeze({
        1: Object.freeze({ lat: 15.3694, lng: 44.1910 }),
        2: Object.freeze({ lat: 12.7985, lng: 45.0318 }),
        3: Object.freeze({ lat: 13.5795, lng: 44.0205 }),
        4: Object.freeze({ lat: 14.5422, lng: 49.1242 }),
        5: Object.freeze({ lat: 14.7932, lng: 42.9511 }),
        6: Object.freeze({ lat: 13.9667, lng: 44.1833 }),
        7: Object.freeze({ lat: 15.6594, lng: 43.9358 }),
        8: Object.freeze({ lat: 14.5427, lng: 44.4051 }),
        9: Object.freeze({ lat: 15.4206, lng: 45.3253 }),
        10: Object.freeze({ lat: 16.9402, lng: 43.7639 }),
        11: Object.freeze({ lat: 16.2079, lng: 52.1760 }),
        12: Object.freeze({ lat: 14.5377, lng: 46.8319 }),
        13: Object.freeze({ lat: 13.1287, lng: 45.3807 }),
        14: Object.freeze({ lat: 13.0583, lng: 44.8837 }),
        15: Object.freeze({ lat: 16.1661, lng: 44.7766 }),
        16: Object.freeze({ lat: 13.9852, lng: 45.5736 }),
        17: Object.freeze({ lat: 15.6094, lng: 43.5956 }),
        18: Object.freeze({ lat: 13.6957, lng: 44.7314 }),
        19: Object.freeze({ lat: 15.4701, lng: 43.5448 }),
        20: Object.freeze({ lat: 14.6322, lng: 43.7229 }),
        21: Object.freeze({ lat: 12.6517, lng: 54.0239 }),
        22: Object.freeze({ lat: 15.3500, lng: 44.1500 }),
        default: Object.freeze({ lat: 15.3694, lng: 44.1910 })
    });

    /* ======================================================================
     * حماية حقول الأرقام في فورم إضافة المنشأة عند المالك
     * تمنع السالب والحروف و + و e/E، وتعمل أيضًا مع الحقول الديناميكية.
     * ملاحظة: رقم الطابق مستثنى فقط لأن النظام الحالي يستخدم -1 للبدروم.
     * ====================================================================== */
    function installOwnerNumericInputGuard(form, config) {
        if (!form || form.dataset.ownerNumericGuardReady === 'true') return;

        // الملف نفسه مشترك مع الأدمن؛ المطلوب هنا يطبق على المالك ورحلة التوثيق فقط.
        if (config?.mode === 'admin') return;

        form.dataset.ownerNumericGuardReady = 'true';

        const numericTextPattern =
            /(Price|Amount|Area|AreaSquareMeters|Distance|Capacity|Count|Qty|MaxPersons|MaxAdults|MaxChildren|BedsCount|InternalRoomsCount|Percentage|Percent|Rate)$/i;

        const explicitNumericTextIds = new Set([
            'add-lat-display',
            'add-lng-display'
        ]);

        const isFloorNumber = input =>
            input instanceof HTMLInputElement &&
            input.hasAttribute('data-floor-number');

        const isGuardedNumericInput = input => {
            if (!(input instanceof HTMLInputElement) || !form.contains(input)) return false;

            // الحفاظ على دعم البدروم الموجود أصلًا: 0 للأرضي و-1 للبدروم.
            if (isFloorNumber(input)) return false;

            if (input.type === 'number') return true;
            if (input.type !== 'text') return false;

            if (explicitNumericTextIds.has(input.id)) return true;
            if (input.hasAttribute('data-range-start') || input.hasAttribute('data-range-end')) return true;

            const semanticKey = [
                input.name || '',
                input.getAttribute('data-field') || '',
                input.getAttribute('data-package-field') || '',
                input.id || ''
            ].join(' ');

            return numericTextPattern.test(semanticKey);
        };

        const allowsDecimal = input => {
            if (explicitNumericTextIds.has(input.id)) return true;

            const semanticKey = [
                input.name || '',
                input.getAttribute('data-field') || '',
                input.getAttribute('data-package-field') || ''
            ].join(' ');

            if (/(Price|Amount|Area|AreaSquareMeters|Distance|Percentage|Percent|Rate)$/i.test(semanticKey)) {
                return true;
            }

            const step = String(input.getAttribute('step') || '').trim().toLowerCase();
            if (step === 'any') return true;
            if (step && Number.isFinite(Number(step)) && Number(step) % 1 !== 0) return true;

            return false;
        };

        const normalizeDigits = value => String(value ?? '')
            .replace(/[٠-٩]/g, digit => String('٠١٢٣٤٥٦٧٨٩'.indexOf(digit)))
            .replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)))
            .replace(/[٫,]/g, '.');

        const configureInput = input => {
            if (!isGuardedNumericInput(input)) return;

            if (input.type === 'number') {
                const minText = input.getAttribute('min');
                const minValue = minText === null ? NaN : Number(minText);

                // لا نغير min=1 أو min=0.01؛ فقط نضيف 0 إذا لم يوجد حد أدنى.
                if (minText === null || (!Number.isNaN(minValue) && minValue < 0)) {
                    input.setAttribute('min', '0');
                }
            }

            input.setAttribute('inputmode', allowsDecimal(input) ? 'decimal' : 'numeric');
        };

        const controlKeys = new Set([
            'Backspace', 'Delete', 'Tab', 'Escape', 'Enter',
            'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown',
            'Home', 'End'
        ]);

        const currentValue = input => normalizeDigits(input.value || '');

        const selectionContainsDecimal = input => {
            const start = input.selectionStart;
            const end = input.selectionEnd;
            if (start === null || end === null || start === end) return false;
            return currentValue(input).slice(start, end).includes('.');
        };

        const canInsertDecimal = input =>
            allowsDecimal(input) &&
            (!currentValue(input).includes('.') || selectionContainsDecimal(input));

        form.addEventListener('keydown', event => {
            const input = event.target;
            if (!isGuardedNumericInput(input)) return;

            configureInput(input);

            // السماح باختصارات النسخ واللصق والتحديد.
            if (event.ctrlKey || event.metaKey) return;
            if (controlKeys.has(event.key)) return;
            if (/^[0-9٠-٩۰-۹]$/.test(event.key)) return;

            if ((event.key === '.' || event.key === 'Decimal') && canInsertDecimal(input)) {
                return;
            }

            // يمنع -, +, e, E والحروف وأي رمز غير رقمي قبل دخوله للحقل.
            event.preventDefault();
        }, true);

        // حماية إضافية للهواتف وطرق الإدخال التي قد لا تمر عبر keydown.
        form.addEventListener('beforeinput', event => {
            const input = event.target;
            if (!isGuardedNumericInput(input) || !event.data) return;
            if (event.inputType?.startsWith('delete')) return;

            const incoming = normalizeDigits(event.data);

            if (/^\d+$/.test(incoming)) return;
            if (incoming === '.' && canInsertDecimal(input)) return;

            event.preventDefault();
        }, true);

        // لا نسمح بلصق قيمة سالبة أو نص أو رموز داخل الحقول الرقمية.
        form.addEventListener('paste', event => {
            const input = event.target;
            if (!isGuardedNumericInput(input)) return;

            const pasted = normalizeDigits(event.clipboardData?.getData('text') || '').trim();
            const pattern = allowsDecimal(input)
                ? /^\d+(?:\.\d+)?$/
                : /^\d+$/;

            if (!pattern.test(pasted)) {
                event.preventDefault();
            }
        }, true);

        // حاجز أخير لو سمح المتصفح مؤقتًا بـ "-" داخل input[type=number].
        form.addEventListener('input', event => {
            const input = event.target;
            if (!isGuardedNumericInput(input)) return;

            configureInput(input);

            if (input.type === 'number') {
                if (input.validity.badInput) {
                    input.value = '';
                    return;
                }

                const value = input.value.trim();
                if (!value) return;

                const numberValue = Number(value);
                if (!Number.isFinite(numberValue) || numberValue < 0) {
                    input.value = '';
                }
                return;
            }

            let value = normalizeDigits(input.value);
            value = value.replace(allowsDecimal(input) ? /[^0-9.]/g : /[^0-9]/g, '');

            if (allowsDecimal(input)) {
                const firstDot = value.indexOf('.');
                if (firstDot >= 0) {
                    value = value.slice(0, firstDot + 1) +
                        value.slice(firstDot + 1).replace(/\./g, '');
                }
            }

            if (input.value !== value) input.value = value;
        }, true);

        // تجهيز الحقول الموجودة الآن؛ والحقول التي تُنشأ لاحقًا يحميها التفويض أعلاه تلقائيًا.
        form.querySelectorAll('input').forEach(configureInput);
    }

    /* ======================================================================
     * أدوات الفورم المشتركة بين المالك والأدمن
     * ====================================================================== */

    // فحص أحجام الصور. كانت هذه الدالة موجودة في الملف العام للمالك؛
    // وضعت هنا أيضًا ليصبح add-place.js مكتفيًا بذاته عند استعماله من الأدمن.
    window.checkImageSize = function (input) {
        if (!input) return;

        if (typeof Swal !== 'undefined' && Swal.isVisible()) {
            Swal.resetValidationMessage();
        }

        let label = input.nextElementSibling;
        if (!label || !label.classList.contains('dynamic-size-lbl')) {
            label = document.createElement('span');
            label.className = 'small mt-1 d-block fw-bold dynamic-size-lbl';
            input.parentNode.insertBefore(label, input.nextSibling);
        }

        if (!input.files || input.files.length === 0) {
            label.innerHTML = '<span class="text-muted">لم يتم اختيار صور.</span>';
            input.classList.remove('is-invalid');
            return;
        }

        let totalSize = 0;
        let maximumMegabytes = 5;

        if (input.name === 'GalleryImages' || input.id === 'new-place-images') {
            maximumMegabytes = 10;
        } else if (input.name === 'CoverImageUpload') {
            maximumMegabytes = 3;
        }

        Array.from(input.files).forEach(file => {
            totalSize += file.size;
        });

        const selectedMegabytes = (totalSize / (1024 * 1024)).toFixed(2);
        const maximumBytes = maximumMegabytes * 1024 * 1024;

        if (totalSize > maximumBytes) {
            label.innerHTML = `<span class="text-danger"><i class="bi bi-x-circle-fill"></i> مساحة التحديد: ${selectedMegabytes} ميجا (مرفوض - الأقصى ${maximumMegabytes} ميجا)</span>`;
            input.classList.add('is-invalid');
        } else {
            label.innerHTML = `<span class="text-success"><i class="bi bi-check-circle-fill"></i> مساحة التحديد: ${selectedMegabytes} ميجا (مقبول)</span>`;
            input.classList.remove('is-invalid');
        }
    };

    // إضافة الوسوم الديناميكية المستخدمة في الغرف والمرافق والباقات.
    window.addDynamicTag = function (button) {
        if (!button) return;

        const inputGroup = button.closest('.input-group');
        if (!inputGroup) return;

        const input = inputGroup.querySelector('input');
        if (!input) return;

        const value = input.value.trim();
        if (!value) {
            input.focus();
            return;
        }

        const container = button.closest('.dynamic-field-container') || inputGroup.parentElement;
        if (!container) return;

        let tagsContainer = container.querySelector('.tags-container');
        if (!tagsContainer) {
            tagsContainer = document.createElement('div');
            tagsContainer.className = 'tags-container mb-2 d-flex flex-wrap gap-2';
            container.insertBefore(tagsContainer, inputGroup);
        }

        const tag = document.createElement('span');
        tag.className = 'badge bg-primary text-white p-2 d-flex align-items-center shadow-sm';
        tag.style.fontSize = '14px';
        tag.style.animation = 'fadeIn 0.3s';

        const textNode = document.createTextNode(value + ' ');
        const removeIcon = document.createElement('i');
        removeIcon.className = 'bi bi-x-circle-fill ms-2 text-light';
        removeIcon.style.cursor = 'pointer';
        removeIcon.style.fontSize = '15px';
        removeIcon.title = 'حذف';
        removeIcon.addEventListener('click', () => tag.remove());

        const hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.name = input.name;
        hidden.value = value;

        tag.appendChild(textNode);
        tag.appendChild(removeIcon);
        tag.appendChild(hidden);
        tagsContainer.appendChild(tag);

        input.value = '';
        input.focus();
    };

    window.toggleCustomRole = function (selectObj) {
        const customRoleInput = document.getElementById('customOwnerRoleInput');
        const finalRoleInput = document.getElementById('finalOwnerRole');

        if (!customRoleInput || !finalRoleInput) return;

        if (selectObj.value === 'other') {
            customRoleInput.classList.remove('d-none');
            customRoleInput.required = true;
            customRoleInput.value = '';
            finalRoleInput.value = '';
        } else {
            customRoleInput.classList.add('d-none');
            customRoleInput.required = false;
            finalRoleInput.value = selectObj.value;
        }
    };

    document.addEventListener('input', function (e) {
        if (e.target && e.target.id === 'customOwnerRoleInput') {
            const hiddenFinal = document.getElementById('finalOwnerRole');
            if (hiddenFinal) hiddenFinal.value = e.target.value;
        }
    });

    window.isStrictlyValidPhone = function (itiInstance, inputElement) {
        if (!itiInstance || !inputElement) return false;

        const rawValue = inputElement.value.replace(/[^0-9]/g, '');
        const countryData = itiInstance.getSelectedCountryData();

        if (countryData && countryData.iso2 === 'ye') {
            return /^(77|78|70|73|71)[0-9]{7}$/.test(rawValue);
        }
        if (rawValue.length === 9 && /^(77|78|70|73|71)/.test(rawValue)) {
            return true;
        }
        return itiInstance.isValidNumber();
    };

    window.formatPhoneNumber = function (itiInstance, inputElement) {
        if (typeof intlTelInputUtils !== 'undefined' && window.isStrictlyValidPhone(itiInstance, inputElement)) {
            const currentNumber = itiInstance.getNumber();
            const dialCode = '+' + itiInstance.getSelectedCountryData().dialCode;
            let formattedNumber = intlTelInputUtils.formatNumber(currentNumber, itiInstance.getSelectedCountryData().iso2, intlTelInputUtils.numberFormat.INTERNATIONAL);

            if (formattedNumber.startsWith(dialCode)) {
                formattedNumber = formattedNumber.substring(dialCode.length).trim();
            }

            // 🌟 السحر هنا: مسح جميع المسافات لكي يكون الرقم 9 أرقام متصلة وتنجح عملية التحقق
            inputElement.value = formattedNumber.replace(/\s+/g, '');
        }
    };


    window.validatePhoneInput = function (itiInstance, inputElement, msgId) {
        const msgElement = document.getElementById(msgId);
        const wrapper = inputElement.closest('.premium-input-group-phone');

        if (wrapper) wrapper.classList.remove('glow-error', 'glow-success', 'is-invalid');
        if (msgElement) {
            msgElement.innerHTML = '';
            msgElement.className = 'small mt-1 text-end d-block fw-bold';
        }

        if (inputElement.value.trim()) {
            if (window.isStrictlyValidPhone(itiInstance, inputElement)) {
                if (wrapper) wrapper.classList.add('glow-success');
                if (msgElement) {
                    msgElement.innerHTML = '<i class="bi bi-check-circle-fill"></i> رقم صحيح ومدعوم';
                    msgElement.classList.add('text-success');
                }
            } else {
                if (wrapper) wrapper.classList.add('glow-error', 'is-invalid');
                if (msgElement) {
                    msgElement.innerHTML = '<i class="bi bi-x-circle-fill"></i> صيغة الرقم خاطئة أو وهمية';
                    msgElement.classList.add('text-danger');
                }
            }
        }
    };

    window.toggleSuiteFieldsAdd = function (val) {
        const suiteContainer = document.getElementById('add-suite-rooms-count-container');
        if (suiteContainer) {
            if (val.includes('جناح')) suiteContainer.classList.remove('d-none');
            else suiteContainer.classList.add('d-none');
        }
    };

    window.toggleSuiteFieldsEdit = function (val) {
        const suiteContainer = document.getElementById('edit-suite-rooms-count-container');
        if (suiteContainer) {
            if (val.includes('جناح')) suiteContainer.classList.remove('d-none');
            else suiteContainer.classList.add('d-none');
        }
    };

    // =========================================================================
    // 📁 القسم الثالث: تبويبة إضافة منشأة (Add Place)
    // =========================================================================

    window.ownerPhoneIti = null;
    window.contactPhoneIti = null;

    function initPhoneFieldsForWizard() {
        const ownerPhoneDisplay = document.querySelector("#ownerPhoneDisplay");
        const contactPhoneDisplay = document.querySelector("#contactPhoneDisplay");

        const itiConfig = {
            initialCountry: "ye",
            preferredCountries: ["ye", "sa", "ae", "eg"],
            separateDialCode: true,
            strictMode: true,
            utilsScript: "https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js"
        };

        if (ownerPhoneDisplay && typeof window.intlTelInput !== 'undefined') {
            window.ownerPhoneIti = window.intlTelInput(ownerPhoneDisplay, itiConfig);

            ownerPhoneDisplay.addEventListener('input', function () {
                window.validatePhoneInput(window.ownerPhoneIti, ownerPhoneDisplay, 'ownerPhoneMsg');
            });

            ownerPhoneDisplay.addEventListener('countrychange', function () {
                window.validatePhoneInput(window.ownerPhoneIti, ownerPhoneDisplay, 'ownerPhoneMsg');
                window.formatPhoneNumber(window.ownerPhoneIti, ownerPhoneDisplay);
            });

            ownerPhoneDisplay.addEventListener('blur', function () {
                window.formatPhoneNumber(window.ownerPhoneIti, ownerPhoneDisplay);
            });

            if (ownerPhoneDisplay.value) {
                window.validatePhoneInput(window.ownerPhoneIti, ownerPhoneDisplay, 'ownerPhoneMsg');
                setTimeout(() => window.formatPhoneNumber(window.ownerPhoneIti, ownerPhoneDisplay), 200);
            }
        }

        if (contactPhoneDisplay && typeof window.intlTelInput !== 'undefined') {
            window.contactPhoneIti = window.intlTelInput(contactPhoneDisplay, itiConfig);

            contactPhoneDisplay.addEventListener('input', function () {
                window.validatePhoneInput(window.contactPhoneIti, contactPhoneDisplay, 'contactPhoneMsg');
            });

            contactPhoneDisplay.addEventListener('countrychange', function () {
                window.validatePhoneInput(window.contactPhoneIti, contactPhoneDisplay, 'contactPhoneMsg');
                window.formatPhoneNumber(window.contactPhoneIti, contactPhoneDisplay);
            });

            contactPhoneDisplay.addEventListener('blur', function () {
                window.formatPhoneNumber(window.contactPhoneIti, contactPhoneDisplay);
            });

            if (contactPhoneDisplay.value) {
                window.validatePhoneInput(window.contactPhoneIti, contactPhoneDisplay, 'contactPhoneMsg');
                setTimeout(() => window.formatPhoneNumber(window.contactPhoneIti, contactPhoneDisplay), 200);
            }
        }

        const revalidatePhonePair = () => {
            if (ownerPhoneDisplay?.value.trim() && contactPhoneDisplay?.value.trim()) {
                validateOwnerStepPhones(false);
            }
        };

        [ownerPhoneDisplay, contactPhoneDisplay].filter(Boolean).forEach(input => {
            input.addEventListener('input', revalidatePhonePair);
            input.addEventListener('countrychange', revalidatePhonePair);
            input.addEventListener('blur', revalidatePhonePair);
        });

        window.setTimeout(revalidatePhonePair, 240);
    }

    function normalizePhoneForComparison(itiInstance, inputElement) {
        if (!inputElement) return '';

        let value = inputElement.value || '';
        if (itiInstance) {
            const internationalNumber = itiInstance.getNumber();
            if (internationalNumber) value = internationalNumber;
        }

        let digits = value.replace(/\D/g, '');
        if (digits.startsWith('00')) digits = digits.substring(2);

        if (digits.length === 9 && /^(77|78|70|73|71)/.test(digits)) {
            digits = `967${digits}`;
        }

        return digits;
    }

    function showPhoneFieldError(inputElement, msgId, message) {
        const wrapper = inputElement?.closest('.premium-input-group-phone');
        const msgElement = document.getElementById(msgId);

        if (wrapper) {
            wrapper.classList.remove('glow-success');
            wrapper.classList.add('glow-error', 'is-invalid');
        }

        if (msgElement) {
            msgElement.innerHTML = `<i class="bi bi-x-circle-fill"></i> ${message}`;
            msgElement.className = 'small mt-1 text-end d-block fw-bold text-danger';
        }
    }

    function validateOwnerStepPhones(showEmptyMessages) {
        const ownerInput = document.getElementById('ownerPhoneDisplay');
        const bookingInput = document.getElementById('contactPhoneDisplay');

        const ownerValid = window.isStrictlyValidPhone(window.ownerPhoneIti, ownerInput);
        const bookingValid = window.isStrictlyValidPhone(window.contactPhoneIti, bookingInput);

        window.validatePhoneInput(window.ownerPhoneIti, ownerInput, 'ownerPhoneMsg');
        window.validatePhoneInput(window.contactPhoneIti, bookingInput, 'contactPhoneMsg');

        if (!ownerValid) {
            if (showEmptyMessages && !ownerInput?.value.trim()) {
                showPhoneFieldError(ownerInput, 'ownerPhoneMsg', 'يرجى إدخال رقم هاتف المالك.');
            }
            return {
                isValid: false,
                message: 'رقم هاتف المالك غير صحيح. يرجى مراجعته قبل المتابعة.'
            };
        }

        if (!bookingValid) {
            if (showEmptyMessages && !bookingInput?.value.trim()) {
                showPhoneFieldError(bookingInput, 'contactPhoneMsg', 'يرجى إدخال رقم هاتف الحجوزات.');
            }
            return {
                isValid: false,
                message: 'رقم هاتف الحجوزات غير صحيح. يرجى مراجعته قبل المتابعة.'
            };
        }

        const ownerNumber = normalizePhoneForComparison(window.ownerPhoneIti, ownerInput);
        const bookingNumber = normalizePhoneForComparison(window.contactPhoneIti, bookingInput);

        if (ownerNumber && bookingNumber && ownerNumber === bookingNumber) {
            showPhoneFieldError(
                bookingInput,
                'contactPhoneMsg',
                'رقم الحجوزات يجب أن يكون مختلفًا عن رقم المالك الشخصي.'
            );

            return {
                isValid: false,
                message: 'رقم هاتف الحجوزات يجب أن يكون مختلفًا عن رقم هاتف المالك الشخصي.'
            };
        }

        return { isValid: true, message: '' };
    }



    // =========================================================================
    // 🖼️ واجهة عربية فخمة لاختيار الصور مع إبقاء input الحقيقي كما هو
    // =========================================================================
    function initializeArabicFileUploads(rootElement) {
        const root = rootElement || document;
        const fileInputs = root.querySelectorAll('#fullPlaceSurveyForm input[type="file"]');

        const formatFileSize = function (bytes) {
            if (!Number.isFinite(bytes) || bytes <= 0) return '0 كيلوبايت';
            const mb = bytes / (1024 * 1024);
            if (mb >= 1) return `${mb.toFixed(2)} ميجابايت`;
            return `${Math.max(1, Math.round(bytes / 1024))} كيلوبايت`;
        };

        const getUploadCopy = function (input) {
            const inputName = input.getAttribute('name') || '';
            const isMultiple = input.multiple;

            if (inputName === 'CoverImageUpload') {
                return {
                    title: 'صورة الغلاف الرئيسية',
                    description: 'الصورة التي ستظهر كواجهة أساسية للمنشأة أمام العملاء.',
                    button: 'اختيار صورة الغلاف',
                    empty: 'لم يتم اختيار صورة الغلاف بعد',
                    limit: 'الحد الأقصى 3 ميجابايت'
                };
            }

            if (inputName === 'GalleryImages') {
                return {
                    title: 'معرض صور المنشأة',
                    description: 'اختر مجموعة صور واضحة تعرض تفاصيل المنشأة من الداخل والخارج.',
                    button: 'اختيار صور الألبوم',
                    empty: 'لم يتم اختيار صور للألبوم بعد',
                    limit: 'الحد الإجمالي 10 ميجابايت'
                };
            }

            const nearbyLabel = input.parentElement?.querySelector('label');
            let title = nearbyLabel?.textContent?.replace('*', '').trim() || 'صور الوحدة';
            title = title.replace(/\s+/g, ' ');

            return {
                title: title,
                description: isMultiple
                    ? 'يمكنك اختيار أكثر من صورة واضحة لهذه الوحدة.'
                    : 'اختر صورة واضحة لهذه الوحدة.',
                button: isMultiple ? 'اختيار صور الوحدة' : 'اختيار صورة الوحدة',
                empty: isMultiple ? 'لم يتم اختيار صور بعد' : 'لم يتم اختيار صورة بعد',
                limit: 'الحد الإجمالي 5 ميجابايت'
            };
        };

        const updateUploadState = function (input, wrapper, copy) {
            const files = Array.from(input.files || []);
            const summary = wrapper.querySelector('.wizard-upload__summary');
            const count = wrapper.querySelector('.wizard-upload__count');
            const preview = wrapper.querySelector('.wizard-upload__preview');

            if (!summary || !count || !preview) return;

            preview.querySelectorAll('img[data-object-url]').forEach(img => {
                try { URL.revokeObjectURL(img.dataset.objectUrl); } catch (_) { }
            });
            preview.innerHTML = '';

            if (!files.length) {
                summary.textContent = copy.empty;
                count.textContent = copy.limit;
                wrapper.classList.remove('has-files', 'has-error');
                return;
            }

            const totalSize = files.reduce((sum, file) => sum + file.size, 0);
            const imageWord = files.length === 1 ? 'صورة واحدة' : `${files.length} صور`;
            summary.textContent = `تم اختيار ${imageWord}`;
            count.textContent = `الحجم الإجمالي: ${formatFileSize(totalSize)}`;
            wrapper.classList.add('has-files');
            wrapper.classList.toggle('has-error', input.classList.contains('is-invalid'));

            files.slice(0, 5).forEach(file => {
                if (!file.type || !file.type.startsWith('image/')) return;
                const objectUrl = URL.createObjectURL(file);
                const item = document.createElement('span');
                item.className = 'wizard-upload__preview-item';
                const img = document.createElement('img');
                img.src = objectUrl;
                img.alt = file.name || 'معاينة الصورة';
                img.dataset.objectUrl = objectUrl;
                item.appendChild(img);
                preview.appendChild(item);
            });

            if (files.length > 5) {
                const more = document.createElement('span');
                more.className = 'wizard-upload__preview-more';
                more.textContent = `+${files.length - 5}`;
                preview.appendChild(more);
            }
        };

        fileInputs.forEach((input, index) => {
            if (input.dataset.arabicUploadReady === 'true') return;

            const copy = getUploadCopy(input);
            const originalParent = input.parentElement;
            if (!originalParent) return;

            if (!input.id) input.id = `wizard-file-${Date.now()}-${index}`;
            input.dataset.arabicUploadReady = 'true';
            input.classList.add('wizard-file-input');

            const wrapper = document.createElement('div');
            wrapper.className = 'wizard-upload';
            wrapper.innerHTML = `
                <div class="wizard-upload__heading">
                    <span class="wizard-upload__icon" aria-hidden="true"><i class="bi bi-cloud-arrow-up"></i></span>
                    <span class="wizard-upload__copy">
                        <strong>${copy.title}</strong>
                        <small>${copy.description}</small>
                    </span>
                </div>
                <label class="wizard-upload__dropzone" for="${input.id}">
                    <span class="wizard-upload__dropzone-icon" aria-hidden="true"><i class="bi bi-images"></i></span>
                    <span class="wizard-upload__dropzone-copy">
                        <strong>${copy.button}</strong>
                        <small>اضغط هنا لفتح الصور من جهازك</small>
                    </span>
                    <span class="wizard-upload__button"><i class="bi bi-plus-lg"></i> اختيار</span>
                </label>
                <div class="wizard-upload__status" aria-live="polite">
                    <span class="wizard-upload__summary">${copy.empty}</span>
                    <span class="wizard-upload__count">${copy.limit}</span>
                </div>
                <div class="wizard-upload__preview" aria-label="معاينة الصور المختارة"></div>
            `;

            originalParent.insertBefore(wrapper, input);
            wrapper.appendChild(input);

            input.addEventListener('change', function () {
                if (typeof window.checkImageSize === 'function') {
                    window.checkImageSize(input);
                }
                updateUploadState(input, wrapper, copy);

                // بعض فحوص الحجم العامة تُفرغ الحقل بعد وصول الحدث إلى document.
                setTimeout(() => updateUploadState(input, wrapper, copy), 0);
            });

            updateUploadState(input, wrapper, copy);
        });
    }


    function normalizeWizardConfig(rawConfig) {
        const source = rawConfig || {};
        const newOwner = source.newOwner || {};

        const requestedMode = String(source.mode || 'owner').toLowerCase();
        const mode = requestedMode === 'admin'
            ? 'admin'
            : requestedMode === 'verification'
                ? 'verification'
                : 'owner';

        return {
            mode,
            ownerId: source.ownerId ? String(source.ownerId) : '',
            ownerMode: source.ownerMode ? String(source.ownerMode) : '',
            initialStatus: source.initialStatus ? String(source.initialStatus) : '',
            initialAvailability:
                source.initialAvailability === true || String(source.initialAvailability).toLowerCase() === 'true',
            currentOwnerName: source.currentOwnerName ? String(source.currentOwnerName) : '',
            currentOwnerPhone: source.currentOwnerPhone ? String(source.currentOwnerPhone) : '',
            newOwner: {
                firstName: newOwner.firstName ? String(newOwner.firstName) : '',
                lastName: newOwner.lastName ? String(newOwner.lastName) : '',
                username: newOwner.username ? String(newOwner.username) : '',
                email: newOwner.email ? String(newOwner.email) : '',
                password: newOwner.password ? String(newOwner.password) : ''
            },
            redirectUrl: source.redirectUrl ? String(source.redirectUrl) : '/Admin/Places/PlaceManagement',
            targetTab: source.targetTab ? String(source.targetTab) : '',
            redirectOnClose: source.redirectOnClose !== false
        };
    }

    function applyWizardSubmissionContext(formData, config) {
        if (!formData || !config || config.mode !== 'admin') return;

        if (config.ownerId) {
            formData.set('OwnerId', config.ownerId);
        }

        if (config.initialStatus) {
            formData.set('Status', config.initialStatus);
        }

        formData.set('IsAvailableStr', config.initialAvailability ? 'true' : 'false');

        if (config.ownerMode) {
            formData.set('OwnerMode', config.ownerMode);
        }

        if (config.ownerMode === 'new') {
            formData.set('NewFirstName', config.newOwner.firstName || '');
            formData.set('NewLastName', config.newOwner.lastName || '');
            formData.set('NewOwnerUsername', config.newOwner.username || '');
            formData.set('NewEmail', config.newOwner.email || '');
            formData.set('NewOwnerPassword', config.newOwner.password || '');
        }
    }

    function prefillWizardContext(config) {
        if (!config || config.mode !== 'admin') return;

        const ownerNameInput = document.querySelector('input[name="OwnerName"]');
        const ownerPhoneInput = document.getElementById('ownerPhoneDisplay');

        let ownerName = config.currentOwnerName;
        if (config.ownerMode === 'new') {
            ownerName = `${config.newOwner.firstName} ${config.newOwner.lastName}`.trim();
        }

        if (ownerNameInput && ownerName) {
            ownerNameInput.value = ownerName;
            ownerNameInput.dispatchEvent(new Event('input', { bubbles: true }));
        }

        if (ownerPhoneInput && config.currentOwnerPhone) {
            const originalPhone = config.currentOwnerPhone;
            ownerPhoneInput.value = originalPhone
                .replace(/^\+967/, '')
                .replace(/^00967/, '')
                .replace(/\s+/g, '');

            if (window.ownerPhoneIti) {
                window.ownerPhoneIti.setNumber(originalPhone);
                setTimeout(() => {
                    window.formatPhoneNumber(window.ownerPhoneIti, ownerPhoneInput);
                    window.validatePhoneInput(window.ownerPhoneIti, ownerPhoneInput, 'ownerPhoneMsg');
                }, 80);
            }
        }
    }

    function openWizardHtml(html, rawConfig) {
        const config = normalizeWizardConfig(rawConfig);
        let explicitClose = false;

        return Swal.fire({
            html: html,
            showCloseButton: true,
            showConfirmButton: false,
            allowOutsideClick: false,
            allowEscapeKey: config.mode !== 'admin',
            heightAuto: false,
            width: 'min(1180px, calc(100vw - 24px))',
            customClass: {
                container: 'owner-place-wizard-layer',
                popup: 'owner-place-wizard-modal',
                htmlContainer: 'owner-place-wizard-modal__html',
                closeButton: 'owner-place-wizard-modal__close'
            },
            didOpen: (popup) => {
                popup.setAttribute('dir', 'rtl');

                const closeButton = Swal.getCloseButton();
                if (closeButton && config.mode === 'admin') {
                    closeButton.setAttribute('title', 'إلغاء والعودة إلى إدارة المنشآت');
                    closeButton.addEventListener('click', (event) => {
                        event.preventDefault();
                        event.stopImmediatePropagation();
                        explicitClose = true;
                        window.location.replace(config.redirectUrl);
                    }, { capture: true, once: true });
                }

                setupWizardLogic(config);
                initPhoneFieldsForWizard();
                window.initAddPlaceMap();
                prefillWizardContext(config);

                initializeArabicFileUploads(popup);
                requestAnimationFrame(() => initializeArabicFileUploads(popup));
                setTimeout(() => initializeArabicFileUploads(popup), 120);
            },
            willClose: () => {
                if (
                    config.mode === 'admin' &&
                    config.redirectOnClose &&
                    explicitClose
                ) {
                    window.location.replace(config.redirectUrl);
                }
            }
        });
    }

    const addPlaceFormCache = new Map();

    function getCachedAddPlaceForm(categoryId) {
        const key = String(categoryId || '').trim();
        if (!key) return Promise.reject(new Error('الفئة غير صحيحة.'));

        if (addPlaceFormCache.has(key)) {
            return addPlaceFormCache.get(key);
        }

        const request = fetch(`/Owner/Place/LoadPlaceForm?categoryId=${encodeURIComponent(key)}`, {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(async response => {
                const html = await response.text();
                if (!response.ok) {
                    throw new Error(html || 'فشل تحميل النموذج');
                }
                return html;
            })
            .catch(error => {
                addPlaceFormCache.delete(key);
                throw error;
            });

        addPlaceFormCache.set(key, request);
        return request;
    }

    function warmAddPlaceForms(categoryCards) {
        const ids = Array.from(categoryCards)
            .map(card => card.getAttribute('data-category-id'))
            .filter(Boolean);

        let cursor = 0;

        const loadNext = () => {
            if (cursor >= ids.length) return;

            const categoryId = ids[cursor++];
            getCachedAddPlaceForm(categoryId)
                .catch(() => { })
                .finally(() => window.setTimeout(loadNext, 90));
        };

        if ('requestIdleCallback' in window) {
            window.requestIdleCallback(loadNext, { timeout: 700 });
        } else {
            window.setTimeout(loadNext, 220);
        }
    }

    function initAddPlace(rawConfig) {
        const config = normalizeWizardConfig(rawConfig);
        const categoryCards = document.querySelectorAll('.category-option');
        if (!categoryCards.length) return;

        categoryCards.forEach(card => {
            if (card._ygAddPlaceClickHandler) {
                card.removeEventListener('click', card._ygAddPlaceClickHandler);
            }

            const categoryId = card.getAttribute('data-category-id');

            const warmCurrent = () => {
                if (categoryId) {
                    getCachedAddPlaceForm(categoryId).catch(() => { });
                }
            };

            card.addEventListener('pointerenter', warmCurrent, { once: true, passive: true });
            card.addEventListener('touchstart', warmCurrent, { once: true, passive: true });
            card.addEventListener('focusin', warmCurrent, { once: true });

            const clickHandler = function () {
                categoryCards.forEach(c => {
                    c.classList.remove(
                        'border-primary',
                        'shadow',
                        'bg-primary',
                        'bg-opacity-10',
                        'is-preparing'
                    );
                    c.removeAttribute('aria-busy');
                });

                this.classList.add(
                    'border-primary',
                    'shadow',
                    'bg-primary',
                    'bg-opacity-10',
                    'is-preparing'
                );
                this.setAttribute('aria-busy', 'true');

                const selectedCard = this;
                const selectedCategoryId = this.getAttribute('data-category-id');

                getCachedAddPlaceForm(selectedCategoryId)
                    .then(html => {
                        selectedCard.classList.remove('is-preparing');
                        selectedCard.removeAttribute('aria-busy');
                        openWizardHtml(html, config);
                    })
                    .catch(error => {
                        selectedCard.classList.remove('is-preparing');
                        selectedCard.removeAttribute('aria-busy');

                        Swal.fire({
                            icon: 'error',
                            title: 'تعذر فتح النموذج',
                            text: error?.message || 'فشل تحميل النموذج',
                            confirmButtonText: 'حسنًا',
                            customClass: {
                                container: 'owner-place-wizard-layer'
                            }
                        });
                    });
            };

            card._ygAddPlaceClickHandler = clickHandler;
            card.addEventListener('click', clickHandler);
        });

        warmAddPlaceForms(categoryCards);
    }


    /* ======================================================================
     * محرر الباقات المخصصة الديناميكي
     * كل بطاقة مفعلة تُرسل كعنصر مستقل داخل Packages[index].
     * ====================================================================== */
    function initCustomPackagesEditor(form) {
        const builders = Array.from(form.querySelectorAll('[data-custom-packages-builder]'));
        if (!builders.length || form.dataset.customPackagesReady === 'true') return;

        form.dataset.customPackagesReady = 'true';
        let packageSequence = 0;

        const cardsInBuilder = builder => Array.from(builder.querySelectorAll('[data-custom-package-card]'));
        const enabledCardsInBuilder = builder => cardsInBuilder(builder)
            .filter(card => card.dataset.enabled === 'true');

        function updateBuilderState(builder) {
            const cards = cardsInBuilder(builder);
            const emptyState = builder.querySelector('[data-custom-packages-empty]');
            if (emptyState) emptyState.hidden = cards.length > 0;

            cards.forEach((card, index) => {
                card.dataset.position = String(index + 1);
                const nameInput = card.querySelector('[data-package-field="Name"]');
                const title = card.querySelector('[data-package-title]');
                if (title && (!nameInput || !nameInput.value.trim())) {
                    title.textContent = `باقة مخصصة ${index + 1}`;
                }
            });
        }

        function setPackageEnabled(card, enabled) {
            const body = card.querySelector('[data-package-body]');
            const toggle = card.querySelector('[data-package-toggle]');
            const badge = card.querySelector('[data-package-status]');
            const collapseButton = card.querySelector('[data-collapse-package]');

            card.dataset.enabled = enabled ? 'true' : 'false';
            card.classList.toggle('is-disabled', !enabled);
            if (toggle) toggle.checked = enabled;

            card.querySelectorAll('[data-package-field]').forEach(input => {
                input.disabled = !enabled;
                input.classList.remove('is-invalid');
            });

            if (badge) {
                badge.textContent = enabled ? 'مفعلة' : 'متوقفة';
                badge.classList.toggle('is-off', !enabled);
            }

            if (body) body.hidden = !enabled;
            if (collapseButton) {
                collapseButton.disabled = !enabled;
                collapseButton.setAttribute('aria-expanded', enabled ? 'true' : 'false');
                collapseButton.querySelector('i')?.classList.toggle('bi-chevron-up', enabled);
                collapseButton.querySelector('i')?.classList.toggle('bi-chevron-down', !enabled);
            }
        }

        function addCustomPackage(builder) {
            const list = builder.querySelector('[data-custom-packages-list]');
            if (!list) return;

            packageSequence += 1;
            const card = document.createElement('article');
            card.className = 'custom-package-card';
            card.dataset.customPackageCard = '';
            card.dataset.enabled = 'true';
            card.dataset.packageKey = `custom_package_${Date.now()}_${packageSequence}`;

            card.innerHTML = `
                <div class="custom-package-card__header">
                    <label class="custom-package-switch">
                        <input type="checkbox" data-package-toggle checked>
                        <span class="custom-package-switch__slider" aria-hidden="true"></span>
                    </label>
                    <span class="custom-package-card__number"><i class="bi bi-tag"></i></span>
                    <div class="custom-package-card__heading">
                        <strong data-package-title>باقة مخصصة</strong>
                        <small>وقت وسعر وسعة مستقلة</small>
                    </div>
                    <span class="custom-package-status" data-package-status>مفعلة</span>
                    <button type="button" class="custom-package-icon-button" data-collapse-package aria-label="طي أو فتح الباقة" aria-expanded="true">
                        <i class="bi bi-chevron-up"></i>
                    </button>
                    <button type="button" class="custom-package-icon-button custom-package-icon-button--danger" data-remove-package aria-label="حذف الباقة">
                        <i class="bi bi-trash3"></i>
                    </button>
                </div>
                <div class="custom-package-card__body" data-package-body>
                    <div class="custom-package-fields-grid">
                        <div class="custom-package-field custom-package-field--name">
                            <label>اسم الباقة <span>*</span></label>
                            <input type="text" maxlength="100" class="form-control vip-input" data-package-field="Name" placeholder="${builder.dataset.packageExample || 'مثال: باقة عائلية'}">
                        </div>
                        <div class="custom-package-field">
                            <label>السعر <span>*</span></label>
                            <div class="custom-package-input-with-suffix">
                                <input type="number" min="0.01" step="0.01" class="form-control vip-input" data-package-field="Price" placeholder="مثال: 25000">
                                <span>ريال</span>
                            </div>
                        </div>
                        <div class="custom-package-field">
                            <label>السعة <span>*</span></label>
                            <div class="custom-package-input-with-suffix">
                                <input type="number" min="1" step="1" class="form-control vip-input" data-package-field="Capacity" placeholder="مثال: 20">
                                <span>شخص</span>
                            </div>
                        </div>
                        <div class="custom-package-field">
                            <label>وقت البداية <span>*</span></label>
                            <input type="time" class="form-control vip-input" data-package-field="StartTime">
                        </div>
                        <div class="custom-package-field">
                            <label>وقت النهاية <span>*</span></label>
                            <input type="time" class="form-control vip-input" data-package-field="EndTime">
                        </div>
                    </div>
                    <div class="custom-package-card__hint">
                        <i class="bi bi-info-circle"></i>
                        يمكن أن ينتهي الوقت في اليوم التالي؛ المهم ألا يتطابق وقت البداية مع النهاية.
                    </div>
                </div>`;

            list.appendChild(card);
            updateBuilderState(builder);

            const nameInput = card.querySelector('[data-package-field="Name"]');
            const title = card.querySelector('[data-package-title]');
            const body = card.querySelector('[data-package-body]');
            const collapseButton = card.querySelector('[data-collapse-package]');

            nameInput?.addEventListener('input', () => {
                if (title) title.textContent = nameInput.value.trim() || `باقة مخصصة ${card.dataset.position}`;
                nameInput.classList.remove('is-invalid');
            });

            card.querySelector('[data-package-toggle]')?.addEventListener('change', event => {
                setPackageEnabled(card, event.target.checked);
            });

            collapseButton?.addEventListener('click', () => {
                if (card.dataset.enabled !== 'true' || !body) return;
                const willOpen = body.hidden;
                body.hidden = !willOpen;
                collapseButton.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
                collapseButton.querySelector('i')?.classList.toggle('bi-chevron-up', willOpen);
                collapseButton.querySelector('i')?.classList.toggle('bi-chevron-down', !willOpen);
            });

            card.querySelector('[data-remove-package]')?.addEventListener('click', () => {
                card.remove();
                updateBuilderState(builder);
                reindex();
            });

            card.querySelectorAll('[data-package-field]').forEach(input => {
                input.addEventListener('input', () => input.classList.remove('is-invalid'));
                input.addEventListener('change', () => input.classList.remove('is-invalid'));
            });

            setPackageEnabled(card, true);
            reindex();
            window.setTimeout(() => nameInput?.focus(), 60);
        }

        function reindex() {
            let packageIndex = 0;

            builders.forEach(builder => {
                cardsInBuilder(builder).forEach(card => {
                    const enabled = card.dataset.enabled === 'true';
                    card.querySelectorAll('[data-package-field]').forEach(input => {
                        input.removeAttribute('name');
                        input.disabled = !enabled;
                    });

                    if (!enabled) return;

                    card.querySelectorAll('[data-package-field]').forEach(input => {
                        input.name = `Packages[${packageIndex}].${input.dataset.packageField}`;
                        input.disabled = false;
                    });
                    packageIndex += 1;
                });
            });
        }

        function validate(stepId) {
            const step = document.getElementById(stepId);
            const builder = step?.querySelector('[data-custom-packages-builder]');
            if (!builder) return '';

            const enabledCards = enabledCardsInBuilder(builder);
            const names = new Set();
            const mainCapacityInput = step.querySelector('input[name="Capacity"]');
            const mainCapacity = Number(mainCapacityInput?.value || 0);
            const isHallStep = stepId === 'step_hall_details';

            for (const card of enabledCards) {
                const name = card.querySelector('[data-package-field="Name"]');
                const price = card.querySelector('[data-package-field="Price"]');
                const capacity = card.querySelector('[data-package-field="Capacity"]');
                const startTime = card.querySelector('[data-package-field="StartTime"]');
                const endTime = card.querySelector('[data-package-field="EndTime"]');
                const title = name?.value.trim() || 'الباقة المخصصة';

                const requiredInputs = [name, price, capacity, startTime, endTime];
                const missing = requiredInputs.filter(input => !input || !String(input.value).trim());
                if (missing.length) {
                    missing.forEach(input => input?.classList.add('is-invalid'));
                    card.querySelector('[data-package-body]').hidden = false;
                    return `أكملي جميع بيانات الباقة (${title}).`;
                }

                if (Number(price.value) <= 0) {
                    price.classList.add('is-invalid');
                    return `سعر الباقة (${title}) يجب أن يكون أكبر من صفر.`;
                }

                if (!Number.isInteger(Number(capacity.value)) || Number(capacity.value) < 1) {
                    capacity.classList.add('is-invalid');
                    return `سعة الباقة (${title}) يجب أن تكون عددًا صحيحًا أكبر من صفر.`;
                }

                if (isHallStep && mainCapacity > 0 && Number(capacity.value) > mainCapacity) {
                    capacity.classList.add('is-invalid');
                    return `سعة الباقة (${title}) لا يمكن أن تتجاوز سعة القاعة (${mainCapacity}).`;
                }

                if (startTime.value === endTime.value) {
                    startTime.classList.add('is-invalid');
                    endTime.classList.add('is-invalid');
                    return `وقت بداية الباقة (${title}) يجب أن يختلف عن وقت نهايتها.`;
                }

                const normalizedName = name.value.trim().replace(/\s+/g, ' ').toLocaleLowerCase('ar');
                if (names.has(normalizedName)) {
                    name.classList.add('is-invalid');
                    return `اسم الباقة (${name.value.trim()}) مكرر.`;
                }
                names.add(normalizedName);
            }

            reindex();
            return '';
        }

        function hasEnabledInStep(stepId) {
            const step = document.getElementById(stepId);
            const builder = step?.querySelector('[data-custom-packages-builder]');
            return builder ? enabledCardsInBuilder(builder).length > 0 : false;
        }

        builders.forEach(builder => {
            builder.querySelector('[data-add-custom-package]')?.addEventListener('click', () => addCustomPackage(builder));
            updateBuilderState(builder);
        });

        form.customPackagesEditor = { validate, reindex, hasEnabledInStep };
    }

    /* ======================================================================
     * محرر الفندق المشترك: أنواع الغرف والطوابق والغرف الفعلية
     * يعمل من نفس النموذج عند المالك والإدارة، ولا يعتمد على مسار جديد.
     * ====================================================================== */
    function initHotelStructureEditor(form) {
        const typesList = form.querySelector('#hotelRoomTypesList');
        const suiteTypesList = form.querySelector('#hotelSuiteTypesList');
        const floorsList = form.querySelector('#hotelFloorsList');
        if (!typesList || !suiteTypesList || !floorsList || form.dataset.hotelEditorReady === 'true') return;

        form.dataset.hotelEditorReady = 'true';
        let keySequence = 0;
        const predefinedRoomTypes = [
            'غرفة مفردة', 'غرفة مزدوجة', 'غرفة توأم', 'غرفة ثلاثية', 'استوديو'
        ];
        const predefinedSuiteTypes = [
            'جناح ملكي', 'جناح عائلي', 'جناح تنفيذي'
        ];

        const escapeHtml = value => String(value ?? '')
            .replace(/&/g, '&amp;').replace(/</g, '&lt;')
            .replace(/>/g, '&gt;').replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
        const normalizeName = value => String(value || '').trim().replace(/\s+/g, ' ').toLocaleLowerCase('ar');
        const makeKey = () => `roomtype_${Date.now()}_${++keySequence}`;
        const allTypeCards = () => [typesList, suiteTypesList]
            .flatMap(list => Array.from(list.querySelectorAll('.hotel-room-type-card')));
        const enabledTypeCards = () => allTypeCards().filter(card => card.dataset.enabled === 'true');
        const enabledRoomCards = () => enabledTypeCards().filter(card => card.dataset.isSuite !== 'true');
        const enabledSuiteCards = () => enabledTypeCards().filter(card => card.dataset.isSuite === 'true');
        const getCardDisplayName = card =>
            card.querySelector('[data-field="DisplayName"]')?.value.trim() || card.dataset.typeName;
        const typeOptions = () => enabledTypeCards().map(card => ({
            key: card.dataset.clientKey,
            label: getCardDisplayName(card),
            isSuite: card.dataset.isSuite === 'true'
        }));

        function refreshEmptyStates() {
            const typesEmpty = form.querySelector('#hotelRoomTypesEmpty');
            const suitesEmpty = form.querySelector('#hotelSuiteTypesEmpty');
            const floorsEmpty = form.querySelector('#hotelFloorsEmpty');
            if (typesEmpty) typesEmpty.hidden = enabledRoomCards().length > 0;
            if (suitesEmpty) suitesEmpty.hidden = enabledSuiteCards().length > 0;
            if (floorsEmpty) floorsEmpty.hidden = floorsList.children.length > 0;
        }

        function refreshUnitTypeSelects() {
            const options = typeOptions();
            floorsList.querySelectorAll('[data-unit-type], [data-range-type], [data-manual-type]').forEach(select => {
                const selected = select.value;
                const roomOptions = options.filter(item => !item.isSuite)
                    .map(item => `<option value="${escapeHtml(item.key)}">${escapeHtml(item.label)}</option>`).join('');
                const suiteOptions = options.filter(item => item.isSuite)
                    .map(item => `<option value="${escapeHtml(item.key)}">${escapeHtml(item.label)}</option>`).join('');
                select.innerHTML = '<option value="">اختر النوع</option>' +
                    (roomOptions ? `<optgroup label="الغرف">${roomOptions}</optgroup>` : '') +
                    (suiteOptions ? `<optgroup label="الأجنحة">${suiteOptions}</optgroup>` : '');
                if (options.some(item => item.key === selected)) select.value = selected;
            });
        }

        function addRoomType(typeName, isCustom = false, isSuite = false) {
            const key = makeKey();
            const card = document.createElement('article');
            card.className = 'hotel-room-type-card';
            card.dataset.clientKey = key;
            card.dataset.typeName = typeName;
            card.dataset.enabled = 'false';
            card.dataset.custom = isCustom ? 'true' : 'false';
            card.dataset.isSuite = isSuite ? 'true' : 'false';
            const displayNameField = isSuite
                ? `<div><label>اسم الجناح <span>*</span></label><input class="form-control vip-input" maxlength="50" data-field="DisplayName" value="${escapeHtml(typeName)}" placeholder="مثال: جناح ملكي مطل"></div>`
                : `<input type="hidden" data-field="DisplayName" value="${escapeHtml(typeName)}">`;
            const suiteFields = isSuite
                ? `<div><label>عدد الغرف داخل الجناح <span>*</span></label><input type="number" min="1" max="100" class="form-control vip-input" data-field="InternalRoomsCount" placeholder="مثال: 2"></div>`
                : '';
            const bedFields = isSuite
                ? ''
                : `<div><label>عدد الأسرّة <small>(اختياري)</small></label><input type="number" min="0" max="50" class="form-control vip-input" data-field="BedsCount" placeholder="مثال: 1"></div>
                   <div><label>نوع السرير <small>(اختياري)</small></label><input class="form-control vip-input" maxlength="50" data-field="BedType" placeholder="مفرد، مزدوج، كينج..."></div>`;
            card.innerHTML = `
                <div class="hotel-type-switch-row">
                    <label class="hotel-type-switch">
                        <input type="checkbox" data-type-toggle>
                        <span class="hotel-type-switch__slider" aria-hidden="true"></span>
                        <span class="hotel-type-switch__name">${escapeHtml(typeName)}</span>
                    </label>
                    ${isCustom ? '<button type="button" class="hotel-icon-button hotel-icon-button--danger" data-remove-type title="حذف النوع المخصص"><i class="bi bi-trash3"></i></button>' : ''}
                </div>
                <input type="hidden" data-field="ClientKey" value="${key}">
                <input type="hidden" data-field="RoomType" value="${escapeHtml(typeName)}">
                <input type="hidden" data-field="IsSuite" value="${isSuite ? 'true' : 'false'}">
                ${isSuite ? '' : displayNameField}
                <input type="hidden" data-field="IsAvailable" value="true">
                <div class="hotel-type-fields" data-type-fields hidden>
                    <div class="hotel-type-fields__grid">
                        ${isSuite ? displayNameField : ''}
                        ${suiteFields}
                        <div><label>السعر لليلة <span>*</span></label><input type="number" min="0.01" step="0.01" class="form-control vip-input" data-field="Price" placeholder="مثال: 25000"></div>
                        <div><label>المساحة م² <small>(اختياري)</small></label><input type="number" min="0.01" step="0.01" class="form-control vip-input" data-field="AreaSquareMeters" placeholder="مثال: 35"></div>
                        <div><label>عدد البالغين <span>*</span></label><input type="number" min="1" max="100" class="form-control vip-input" data-field="MaxAdults" placeholder="مثال: 2"></div>
                        <div><label>عدد الأطفال <small>(اختياري)</small></label><input type="number" min="0" max="100" class="form-control vip-input" data-field="MaxChildren" placeholder="مثال: 1"></div>
                        ${bedFields}
                        <div class="hotel-type-fields__wide"><label>${isSuite ? 'وصف ومزايا الجناح' : 'الوصف'} <small>(اختياري)</small></label><textarea class="form-control vip-input" maxlength="500" rows="2" data-field="Description" placeholder="المزايا الخاصة بهذا النوع"></textarea></div>
                        <div class="hotel-type-fields__wide"><label>صور النوع <small>(اختياري)</small></label><input type="file" multiple accept="image/*" class="form-control vip-input image-upload" data-field="Images"></div>
                    </div>
                </div>`;
            (isSuite ? suiteTypesList : typesList).appendChild(card);

            const toggle = card.querySelector('[data-type-toggle]');
            const fields = card.querySelector('[data-type-fields]');
            const setEnabled = enabled => {
                if (!enabled) {
                    const inUse = Array.from(floorsList.querySelectorAll('[data-unit-type]')).some(select => select.value === key);
                    if (inUse) {
                        toggle.checked = true;
                        showWizardInlineError('هذا النوع مرتبط بوحدات فعلية. غيّري نوع الوحدات أو احذفيها أولاً.');
                        return;
                    }
                }
                card.dataset.enabled = enabled ? 'true' : 'false';
                card.classList.toggle('is-enabled', enabled);
                fields.hidden = !enabled;
                card.querySelectorAll('[data-field]').forEach(input => { input.disabled = !enabled; });
                refreshUnitTypeSelects();
                refreshEmptyStates();
            };
            toggle.addEventListener('change', () => setEnabled(toggle.checked));
            if (isSuite) {
                card.querySelector('[data-field="DisplayName"]')?.addEventListener('input', refreshUnitTypeSelects);
            }

            card.querySelector('[data-remove-type]')?.addEventListener('click', () => {
                const inUse = Array.from(floorsList.querySelectorAll('[data-unit-type]'))
                    .some(select => select.value === key);
                if (inUse) {
                    showWizardInlineError('لا يمكن حذف النوع لأنه مرتبط بوحدات فعلية. احذفي الوحدات أو غيّري نوعها أولاً.');
                    return;
                }
                card.remove();
                refreshUnitTypeSelects();
                refreshEmptyStates();
            });
            initializeArabicFileUploads(card);
            setEnabled(isCustom);
            if (isCustom) toggle.checked = true;
        }

        function addUnit(floorCard, roomNumber, typeKey, isAvailable = true, notes = '') {
            const row = document.createElement('div');
            row.className = 'hotel-unit-row';
            row.innerHTML = `
                <input class="form-control vip-input" data-unit-number maxlength="50" value="${escapeHtml(roomNumber)}" placeholder="رقم الوحدة">
                <select class="form-select vip-input" data-unit-type></select>
                <label class="hotel-availability"><input type="checkbox" data-unit-available ${isAvailable ? 'checked' : ''}><input type="hidden" data-unit-unavailable value="false"> متاحة</label>
                <input class="form-control vip-input" data-unit-notes maxlength="250" value="${escapeHtml(notes)}" placeholder="ملاحظات اختيارية">
                <button type="button" class="hotel-icon-button hotel-icon-button--danger" data-remove-unit title="حذف الوحدة"><i class="bi bi-x-lg"></i></button>`;
            floorCard.querySelector('[data-units-list]').appendChild(row);
            refreshUnitTypeSelects();
            row.querySelector('[data-unit-type]').value = typeKey || '';
            updateFloorSummary(floorCard);
            row.querySelector('[data-remove-unit]').addEventListener('click', () => {
                row.remove();
                updateFloorSummary(floorCard);
            });
        }

        function updateFloorSummary(floorCard) {
            const countBadge = floorCard?.querySelector('[data-units-count]');
            if (!countBadge) return;

            const unitsCount = floorCard.querySelectorAll('.hotel-unit-row').length;
            countBadge.textContent = unitsCount;
            countBadge.setAttribute('aria-label', unitsCount === 1 ? 'وحدة واحدة' : `${unitsCount} وحدات`);
        }

        function setUnitsCollapsed(floorCard, collapsed) {
            if (!floorCard) return;

            const toggle = floorCard.querySelector('[data-toggle-units]');
            const panel = floorCard.querySelector('[data-units-panel]');
            const actionText = floorCard.querySelector('.hotel-units-toggle__action span');
            floorCard.classList.toggle('is-units-collapsed', collapsed);
            if (toggle) toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
            if (panel) panel.hidden = collapsed;
            if (actionText) actionText.textContent = collapsed ? 'عرض الوحدات' : 'إخفاء الوحدات';
        }

        function closeOtherFloorUnits(activeFloor) {
            floorsList.querySelectorAll('.hotel-floor-card').forEach(floor => {
                if (floor !== activeFloor) setUnitsCollapsed(floor, true);
            });
        }

        function revealFloorForError(floorCard) {
            closeOtherFloorUnits(floorCard);
            setUnitsCollapsed(floorCard, false);
            requestAnimationFrame(() => floorCard.scrollIntoView({ behavior: 'smooth', block: 'start' }));
        }

        function addFloor() {
            closeOtherFloorUnits(null);
            const card = document.createElement('article');
            card.className = 'hotel-floor-card';
            card.innerHTML = `
                <div class="hotel-card-header">
                    <div><span class="hotel-card-kicker">طابق</span><strong data-floor-title>طابق جديد</strong></div>
                    <button type="button" class="hotel-icon-button hotel-icon-button--danger" data-remove-floor title="حذف الطابق"><i class="bi bi-trash3"></i></button>
                </div>
                <div class="row g-3 mb-3">
                    <div class="col-md-4"><label>رقم الطابق <span>*</span></label><input type="number" min="-100" max="1000" class="form-control vip-input" data-floor-number placeholder="0 للأرضي، -1 للبدروم"></div>
                    <div class="col-md-8"><label>اسم الطابق <small>(اختياري)</small></label><input class="form-control vip-input" maxlength="50" data-floor-label placeholder="مثال: البدروم، الأرضي، الأول"></div>
                </div>
                <div class="hotel-range-box">
                    <strong><i class="bi bi-magic"></i> إنشاء نطاق وحدات</strong>
                    <div class="hotel-range-grid">
                        <input class="form-control vip-input" data-range-start inputmode="numeric" placeholder="من: 1 أو 101">
                        <input class="form-control vip-input" data-range-end inputmode="numeric" placeholder="إلى: 20 أو 110">
                        <input class="form-control vip-input" data-range-prefix maxlength="12" placeholder="بادئة اختيارية B">
                        <select class="form-select vip-input" data-range-type></select>
                        <button type="button" class="hotel-action-button" data-generate-range><i class="bi bi-plus-circle"></i> إنشاء</button>
                    </div>
                </div>
                <div class="hotel-manual-box">
                    <strong>إضافة وحدة برقم/رمز خاص</strong>
                    <div class="hotel-manual-grid">
                        <input class="form-control vip-input" data-manual-number maxlength="50" placeholder="مثال: B01 أو VIP-1">
                        <select class="form-select vip-input" data-manual-type></select>
                        <button type="button" class="hotel-secondary-button" data-add-manual><i class="bi bi-plus-lg"></i> إضافة</button>
                    </div>
                </div>
                <div class="hotel-units-section">
                    <button type="button" class="hotel-units-toggle" data-toggle-units aria-expanded="true">
                        <span class="hotel-units-toggle__title"><i class="bi bi-door-open"></i> الوحدات المضافة <b data-units-count>0</b></span>
                        <span class="hotel-units-toggle__action"><span>إخفاء الوحدات</span><i class="bi bi-chevron-up"></i></span>
                    </button>
                    <div class="hotel-units-panel" data-units-panel>
                        <div class="hotel-units-header"><span>رقم الوحدة</span><span>النوع</span><span>الحالة</span><span>ملاحظات</span><span></span></div>
                        <div class="hotel-units-list" data-units-list></div>
                    </div>
                </div>`;
            floorsList.appendChild(card);
            refreshUnitTypeSelects();
            refreshEmptyStates();

            const numberInput = card.querySelector('[data-floor-number]');
            const labelInput = card.querySelector('[data-floor-label]');
            const updateTitle = () => {
                const customLabel = labelInput.value.trim();
                const floorNumber = numberInput.value.trim();
                card.querySelector('[data-floor-title]').textContent = customLabel || (floorNumber === '' ? 'طابق جديد' : `الطابق ${floorNumber}`);
            };
            numberInput.addEventListener('input', updateTitle);
            labelInput.addEventListener('input', updateTitle);
            card.querySelector('[data-remove-floor]').addEventListener('click', () => { card.remove(); refreshEmptyStates(); });
            card.querySelector('[data-toggle-units]').addEventListener('click', () => {
                const willOpen = card.classList.contains('is-units-collapsed');
                if (willOpen) closeOtherFloorUnits(card);
                setUnitsCollapsed(card, !willOpen);
            });
            setUnitsCollapsed(card, false);
            updateFloorSummary(card);
            requestAnimationFrame(() => card.scrollIntoView({ behavior: 'smooth', block: 'start' }));

            card.querySelector('[data-generate-range]').addEventListener('click', () => {
                const startRaw = card.querySelector('[data-range-start]').value.trim();
                const endRaw = card.querySelector('[data-range-end]').value.trim();
                const start = Number(startRaw), end = Number(endRaw);
                const prefix = card.querySelector('[data-range-prefix]').value.trim();
                const typeKey = card.querySelector('[data-range-type]').value;
                if (!Number.isInteger(start) || !Number.isInteger(end) || start > end || end - start > 500 || !typeKey) {
                    showWizardInlineError('أدخلي نطاقًا صحيحًا (بحد أقصى 501 وحدة) وحددي النوع.');
                    return;
                }
                const pad = (/^0\d/.test(startRaw) || /^0\d/.test(endRaw)) ? Math.max(startRaw.length, endRaw.length) : 0;
                const existing = new Set(Array.from(form.querySelectorAll('[data-unit-number]')).map(input => normalizeName(input.value)));
                for (let n = start; n <= end; n++) {
                    const roomNumber = prefix + (pad ? String(n).padStart(pad, '0') : String(n));
                    if (existing.has(normalizeName(roomNumber))) {
                        showWizardInlineError(`رقم الوحدة (${roomNumber}) موجود مسبقًا.`);
                        return;
                    }
                }
                for (let n = start; n <= end; n++) addUnit(card, prefix + (pad ? String(n).padStart(pad, '0') : String(n)), typeKey);
                updateFloorSummary(card);
            });
            card.querySelector('[data-add-manual]').addEventListener('click', () => {
                const number = card.querySelector('[data-manual-number]').value.trim();
                const typeKey = card.querySelector('[data-manual-type]').value;
                if (!number || !typeKey) {
                    showWizardInlineError('أدخلي رقم/رمز الوحدة وحددي نوعها.');
                    return;
                }
                const duplicate = Array.from(form.querySelectorAll('[data-unit-number]')).some(input => normalizeName(input.value) === normalizeName(number));
                if (duplicate) {
                    showWizardInlineError(`رقم الوحدة (${number}) موجود مسبقًا.`);
                    return;
                }
                addUnit(card, number, typeKey);
                card.querySelector('[data-manual-number]').value = '';
                updateFloorSummary(card);
            });
        }

        function reindex() {
            allTypeCards().forEach(card => {
                card.querySelectorAll('[data-field]').forEach(input => {
                    input.removeAttribute('name');
                    input.disabled = card.dataset.enabled !== 'true';
                });
            });
            enabledTypeCards().forEach((card, typeIndex) => {
                card.querySelectorAll('[data-field]').forEach(input => {
                    input.name = `HotelRoomTypes[${typeIndex}].${input.dataset.field}`;
                    input.disabled = false;
                });
            });
            floorsList.querySelectorAll('.hotel-floor-card').forEach((floor, floorIndex) => {
                floor.querySelector('[data-floor-number]').name = `HotelFloors[${floorIndex}].FloorNumber`;
                floor.querySelector('[data-floor-label]').name = `HotelFloors[${floorIndex}].FloorLabel`;
                floor.querySelectorAll('.hotel-unit-row').forEach((unit, unitIndex) => {
                    unit.querySelector('[data-unit-number]').name = `HotelFloors[${floorIndex}].Units[${unitIndex}].RoomNumber`;
                    unit.querySelector('[data-unit-type]').name = `HotelFloors[${floorIndex}].Units[${unitIndex}].RoomTypeClientKey`;
                    unit.querySelector('[data-unit-notes]').name = `HotelFloors[${floorIndex}].Units[${unitIndex}].Notes`;
                    const checkbox = unit.querySelector('[data-unit-available]');
                    unit.querySelector('[data-unit-unavailable]').name = `HotelFloors[${floorIndex}].Units[${unitIndex}].IsAvailable`;
                    checkbox.name = `HotelFloors[${floorIndex}].Units[${unitIndex}].IsAvailable`;
                    checkbox.value = 'true';
                });
            });
        }

        function validate(stepId) {
            const typeCards = enabledTypeCards();
            const floorCards = Array.from(floorsList.querySelectorAll('.hotel-floor-card'));
            if (stepId === 'step_rooms') {
                const roomCards = enabledRoomCards();
                if (!roomCards.length) return 'يجب تفعيل نوع غرفة واحد على الأقل.';
                for (const card of roomCards) {
                    const price = card.querySelector('[data-field="Price"]');
                    const adults = card.querySelector('[data-field="MaxAdults"]');
                    if (Number(price.value) <= 0 || Number(adults.value) < 1) {
                        [price, adults].forEach(input => input.classList.toggle('is-invalid', !input.value || Number(input.value) <= 0));
                        return `أكمل السعر وعدد البالغين للنوع (${getCardDisplayName(card)}).`;
                    }
                }
            }
            if (stepId === 'step_suites') {
                for (const card of enabledSuiteCards()) {
                    const price = card.querySelector('[data-field="Price"]');
                    const adults = card.querySelector('[data-field="MaxAdults"]');
                    const displayName = card.querySelector('[data-field="DisplayName"]');
                    const internalRooms = card.querySelector('[data-field="InternalRoomsCount"]');

                    const priceInvalid = Number(price.value) <= 0;
                    const adultsInvalid = Number(adults.value) < 1;
                    const nameInvalid = !displayName.value.trim();
                    const roomsInvalid = Number(internalRooms.value) < 1;

                    price.classList.toggle('is-invalid', priceInvalid);
                    adults.classList.toggle('is-invalid', adultsInvalid);
                    displayName.classList.toggle('is-invalid', nameInvalid);
                    internalRooms.classList.toggle('is-invalid', roomsInvalid);

                    if (priceInvalid || adultsInvalid || nameInvalid || roomsInvalid) {
                        return `أكمل اسم الجناح وعدد غرفه الداخلية والسعر والسعة للنوع (${card.dataset.typeName}).`;
                    }
                }
            }
            if (stepId === 'step_floors') {
                if (!floorCards.length) return 'يجب إضافة طابق واحد على الأقل.';
                const numbers = new Set();
                const usedTypes = new Set();
                for (const floor of floorCards) {
                    const floorNumber = floor.querySelector('[data-floor-number]').value.trim();
                    if (floorNumber === '') {
                        revealFloorForError(floor);
                        return 'أدخل رقم كل طابق. استخدم 0 للدور الأرضي و-1 للبدروم.';
                    }
                    if (numbers.has(floorNumber)) {
                        revealFloorForError(floor);
                        return 'لا يمكن تكرار رقم الطابق.';
                    }
                    numbers.add(floorNumber);
                    const units = Array.from(floor.querySelectorAll('.hotel-unit-row'));
                    if (!units.length) {
                        revealFloorForError(floor);
                        return 'كل طابق مضاف يجب أن يحتوي على وحدة واحدة على الأقل.';
                    }
                    for (const unit of units) {
                        const number = unit.querySelector('[data-unit-number]').value.trim();
                        const normalized = number.toLocaleLowerCase('ar');
                        const typeKey = unit.querySelector('[data-unit-type]').value;
                        if (!number || !typeKey) {
                            revealFloorForError(floor);
                            return 'أكمل رقم ونوع كل وحدة فعلية.';
                        }
                        if (numbers.has(`unit:${normalized}`)) {
                            revealFloorForError(floor);
                            return `رقم الوحدة (${number}) مكرر، ويجب أن يكون فريدًا في الفندق.`;
                        }
                        numbers.add(`unit:${normalized}`);
                        usedTypes.add(typeKey);
                    }
                }
                const unused = typeCards.find(card => !usedTypes.has(card.dataset.clientKey));
                if (unused) return `النوع (${getCardDisplayName(unused)}) لا يحتوي على أي وحدة فعلية.`;
            }
            reindex();
            return '';
        }

        predefinedRoomTypes.forEach(typeName => addRoomType(typeName, false, false));
        predefinedSuiteTypes.forEach(typeName => addRoomType(typeName, false, true));

        const addCustomType = (input, isSuite) => {
            const typeName = input?.value.trim().replace(/\s+/g, ' ') || '';
            if (!typeName) {
                showWizardInlineError(isSuite ? 'أدخلي اسم الجناح المخصص أولاً.' : 'أدخلي اسم نوع الغرفة المخصص أولاً.');
                input?.focus();
                return;
            }
            const duplicate = allTypeCards()
                .some(card => normalizeName(card.dataset.typeName) === normalizeName(typeName));
            if (duplicate) {
                showWizardInlineError('هذا النوع موجود بالفعل. فعّليه من القائمة.', 'info');
                return;
            }
            addRoomType(typeName, true, isSuite);
            input.value = '';
        };
        const wireCustomType = (inputSelector, buttonSelector, isSuite) => {
            const input = form.querySelector(inputSelector);
            form.querySelector(buttonSelector)?.addEventListener('click', () => addCustomType(input, isSuite));
            input?.addEventListener('keydown', event => {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    addCustomType(input, isSuite);
                }
            });
        };
        wireCustomType('#customHotelRoomTypeName', '#addCustomHotelRoomTypeBtn', false);
        wireCustomType('#customHotelSuiteTypeName', '#addCustomHotelSuiteTypeBtn', true);
        form.addEventListener('focusin', event => {
            if (event.target.matches('input[type="number"]') && event.target.value === '0') event.target.select();
        });
        form.querySelector('#addHotelFloorBtn')?.addEventListener('click', addFloor);
        form.hotelStructureEditor = { validate, reindex };
        refreshEmptyStates();
    }

    function setupWizardLogic(rawConfig) {
        const config = normalizeWizardConfig(rawConfig);
        const form = document.getElementById('fullPlaceSurveyForm');
        if (!form) return;

        installOwnerNumericInputGuard(form, config);

        bindRealTimeValidation('add-lat-display', 'add-lat', 'lat');
        bindRealTimeValidation('add-lng-display', 'add-lng', 'lng');

        const systemId = parseInt(document.getElementById('selectedSystemId').value) || 0;

        let flowSteps = [];
        if (systemId === 1) flowSteps = ['step_owner', 'step_general', 'step_rooms', 'step_suites', 'step_floors', 'step_facilities'];
        else if (systemId === 2) flowSteps = ['step_owner', 'step_general', 'step_chalet_details', 'step_chalet_facilities'];
        else if (systemId === 3 || systemId === 4) flowSteps = ['step_owner', 'step_general', 'step_hall_details', 'step_hall_facilities'];
        else flowSteps = ['step_owner', 'step_general', 'step_warnings'];

        const stepMeta = {
            step_owner: { title: 'بيانات مسؤول المنشأة', hint: 'أدخل بيانات الشخص المسؤول وأرقام التواصل المعتمدة.', icon: 'bi-person-vcard' },
            step_general: { title: 'هوية المنشأة والموقع', hint: 'أضف الاسم والوصف والصور وحدد الموقع بدقة على الخريطة.', icon: 'bi-geo-alt' },
            step_rooms: { title: 'أنواع الغرف', hint: 'أضف أنواع الغرف وأسعارها وسعاتها وصورها، بما فيها الأنواع المخصصة.', icon: 'bi-door-open' },
            step_suites: { title: 'الأجنحة الفندقية', hint: 'أضف الأجنحة وعدد الغرف الداخلية لكل جناح، أو تجاوز الخطوة إذا لم توجد أجنحة.', icon: 'bi-gem' },
            step_floors: { title: 'الطوابق والوحدات الفعلية', hint: 'أنشئ الطوابق واربط كل رقم غرفة أو جناح بنوعه الصحيح.', icon: 'bi-building' },
            step_facilities: { title: 'المرافق والوحدات الإضافية', hint: 'حدد الخدمات والمرافق والوحدات التابعة للفندق.', icon: 'bi-stars' },
            step_chalet_details: { title: 'تفاصيل المنشأة والباقات', hint: 'أدخل تفاصيل المبنى وحدد باقات الحجز والأسعار.', icon: 'bi-house-heart' },
            step_chalet_facilities: { title: 'الخدمات ووسائل الراحة', hint: 'حدد الترفيه والخدمات والتكييف ومرافق الحوش.', icon: 'bi-grid' },
            step_hall_details: { title: 'تفاصيل القاعة والباقات', hint: 'أدخل السعة والمناسبات وأنواع الجلوس وأسعار الباقات.', icon: 'bi-people' },
            step_hall_facilities: { title: 'تجهيزات القاعة ومرافقها', hint: 'أدخل أعداد التجهيزات والخدمات المساندة المتاحة.', icon: 'bi-speaker' },
            step_warnings: { title: 'الإقرار والإرسال', hint: 'راجع ميثاق الاستخدام وأكّد صحة البيانات قبل الإرسال.', icon: 'bi-shield-check' }
        };

        let currentIndex = 0;
        const totalSteps = flowSteps.length;

        const btnNext = document.getElementById('btnNextStep'), btnPrev = document.getElementById('btnPrevStep'), btnSubmit = document.getElementById('btnSubmitForm'), progressBar = document.getElementById('formProgressBar');

        /*
         * تثبيت شريط التنقل أسفل نافذة المعالج:
         * ننقله خارج منطقة التمرير مع إبقاء زر الإرسال مرتبطًا بنفس الفورم.
         */
        const wizardNavigation = form.querySelector('.wizard-navigation');
        const wizardMain = form.closest('.place-wizard-main');
        const wizardContentScroll = form.closest('.place-wizard-content-scroll');
        if (wizardNavigation && wizardMain && wizardContentScroll && wizardNavigation.parentElement !== wizardMain) {
            if (btnSubmit) btnSubmit.setAttribute('form', form.id);
            wizardMain.appendChild(wizardNavigation);
        }

        if (systemId === 1) initHotelStructureEditor(form);
        initCustomPackagesEditor(form);

        function moveToStepAndShowError(message, stepId) {
            const requestedIndex = stepId ? flowSteps.indexOf(stepId) : -1;
            if (requestedIndex >= 0 && requestedIndex !== currentIndex) {
                currentIndex = requestedIndex;
                updateUI();
            }
            requestAnimationFrame(() => showWizardInlineError(message));
        }

        function setWizardSavingState(isSaving) {
            const popup = form.closest('.owner-place-wizard-modal') || Swal.getPopup();
            if (!popup) return;

            let overlay = popup.querySelector('.wizard-saving-overlay');
            if (!overlay && !isSaving) return;
            if (!overlay) {
                overlay = document.createElement('div');
                overlay.className = 'wizard-saving-overlay';
                overlay.hidden = true;
                overlay.style.cssText = 'position:absolute;inset:0;z-index:30;display:flex;align-items:center;justify-content:center;padding:20px;background:rgba(255,255,255,.82);backdrop-filter:blur(4px);border-radius:inherit;';
                overlay.innerHTML = `
                    <div class="wizard-saving-overlay__card" role="status" aria-live="polite" style="width:min(390px,100%);display:grid;justify-items:center;gap:10px;padding:26px;border:1px solid #dceaf2;border-radius:20px;background:#fff;box-shadow:0 18px 50px rgba(5,32,60,.16);text-align:center;">
                        <span class="wizard-saving-overlay__spinner" aria-hidden="true" style="width:42px;height:42px;border:4px solid #dff4fb;border-top-color:#00A6DA;border-radius:50%;animation:swal2-rotate-loading 1.5s linear infinite;"></span>
                        <strong style="color:#05203C;font-size:1.05rem;">جاري حفظ بيانات المنشأة...</strong>
                        <small style="color:#718096;line-height:1.7;">لا تغلقي الصفحة، ستبقين في النموذج إذا احتاج أي حقل إلى تعديل.</small>
                    </div>`;
                popup.appendChild(overlay);
            }

            overlay.hidden = !isSaving;
            overlay.style.display = isSaving ? 'flex' : 'none';
            popup.classList.toggle('wizard-is-saving', isSaving);
            form.setAttribute('aria-busy', isSaving ? 'true' : 'false');
        }

        form.addEventListener('input', event => {
            if (event.target.matches('input:not([type="file"]), textarea')) {
                event.target.classList.remove('is-invalid');
                hideWizardInlineError();
            }
        });

        form.addEventListener('change', event => {
            if (event.target.matches('select, input[type="radio"]')) {
                event.target.classList.remove('is-invalid');
                hideWizardInlineError();
            }
        });

        function updateUI() {
            document.querySelectorAll('.step-section').forEach(el => el.classList.remove('active'));

            const currentStepId = flowSteps[currentIndex];
            const currentEl = document.getElementById(currentStepId);
            if (currentEl) currentEl.classList.add('active');

            const progressValue = Math.round(((currentIndex + 1) / totalSteps) * 100);
            if (progressBar) {
                progressBar.style.width = progressValue + '%';
                progressBar.setAttribute('aria-valuenow', String(progressValue));
            }

            const currentMeta = stepMeta[currentStepId] || { title: 'إضافة منشأة', hint: 'أكمل البيانات المطلوبة.', icon: 'bi-building-add' };
            const stepLabel = document.getElementById('wizardCurrentStepLabel');
            const stepHint = document.getElementById('wizardCurrentStepHint');
            const stepCounter = document.getElementById('wizardStepCounter');
            const progressPercent = document.getElementById('wizardProgressPercent');
            const stepIcon = document.getElementById('wizardCurrentStepIcon');

            if (stepLabel) stepLabel.textContent = currentMeta.title;
            if (stepHint) stepHint.textContent = currentMeta.hint;
            if (stepCounter) stepCounter.textContent = `الخطوة ${currentIndex + 1} من ${totalSteps}`;
            if (progressPercent) progressPercent.textContent = progressValue + '%';
            if (stepIcon) stepIcon.className = `bi ${currentMeta.icon}`;

            document.querySelectorAll('[data-step-target]').forEach(item => {
                const itemIndex = flowSteps.indexOf(item.getAttribute('data-step-target'));
                item.classList.remove('is-active', 'is-complete', 'is-pending');
                item.removeAttribute('aria-current');

                if (itemIndex < 0) return;
                if (itemIndex < currentIndex) item.classList.add('is-complete');
                else if (itemIndex === currentIndex) {
                    item.classList.add('is-active');
                    item.setAttribute('aria-current', 'step');
                } else item.classList.add('is-pending');
            });

            const activeMobileStep = document.querySelector(`.place-wizard-mobile-step[data-step-target="${currentStepId}"]`);
            if (activeMobileStep && window.innerWidth < 992) {
                activeMobileStep.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
            }

            const scrollArea = document.querySelector('.place-wizard-content-scroll');
            if (scrollArea) scrollArea.scrollTo({ top: 0, behavior: currentIndex === 0 ? 'auto' : 'smooth' });

            // بعض حقول الصور تكون داخل خطوات مخفية عند فتح المودال؛
            // إعادة التهيئة آمنة لأنها تتجاهل الحقول التي تم تجهيزها مسبقًا.
            initializeArabicFileUploads(document.getElementById('fullPlaceSurveyForm'));

            btnPrev.style.display = currentIndex > 0 ? 'block' : 'none';
            if (currentIndex === totalSteps - 1) { btnNext.style.display = 'none'; btnSubmit.style.display = 'block'; }
            else { btnNext.style.display = 'block'; btnSubmit.style.display = 'none'; }
        }

        if (btnNext) btnNext.addEventListener('click', (e) => {
            e.preventDefault();

            const currentSectionId = flowSteps[currentIndex];
            const currentSection = document.getElementById(currentSectionId);
            const errorContainer = document.getElementById('wizardErrorContainer');
            const errorText = document.getElementById('wizardErrorText');

            errorContainer.classList.add('d-none');
            errorText.innerHTML = '';

            // 💡 إزالة الأخطاء القديمة (باستثناء الصور لأن الخطأ فيها ديناميكي من دالة checkImageSize)
            currentSection.querySelectorAll('.is-invalid:not([type="file"])').forEach(el => el.classList.remove('is-invalid'));

            let isValid = true;
            let errorMsg = '';

            // 1. التأكد من أن الصور لم تتجاوز الحجم (إذا كانت حمراء)
            const invalidFiles = currentSection.querySelectorAll('input[type="file"].is-invalid');
            if (invalidFiles.length > 0) {
                isValid = false;
                errorMsg = 'يوجد صور تتجاوز المساحة المسموحة! يرجى تقليل الصور المحددة في الحقول التي تحتها نص أحمر.';
            }

            // 2. فحص بقية الحقول المطلوبة
            currentSection.querySelectorAll('[required]').forEach(input => {
                if (input.type === 'checkbox' && !input.checked) {
                    isValid = false; errorMsg = 'يرجى الموافقة على الشروط أولاً.';
                } else if (input.type !== 'checkbox' && !input.value.trim()) {
                    input.classList.add('is-invalid'); isValid = false; errorMsg = 'يرجى تعبئة جميع الحقول الإجبارية المحددة باللون الأحمر.';
                }
            });

            if (currentSectionId === 'step_owner') {
                // التحقق مما إذا كان الأدمن قد اختار "إنشاء حساب مالك جديد"
                const ownerMode = document.querySelector('input[name="OwnerMode"]:checked')?.value || document.getElementById('OwnerMode')?.value;

                if (ownerMode === 'new') {
                    const emailInput = document.getElementById('NewEmail');
                    const usernameInput = document.getElementById('NewOwnerUsername');
                    const passInput = document.getElementById('NewOwnerPassword');

                    // 1. تقييد الإيميل (يجب أن يكون بصيغة صحيحة)
                    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                    if (emailInput && emailInput.value && !emailRegex.test(emailInput.value)) {
                        emailInput.classList.add('is-invalid');
                        isValid = false;
                        errorMsg = 'صيغة البريد الإلكتروني غير صحيحة.';
                    }

                    // 2. تقييد اسم المستخدم (إنجليزي وأرقام فقط، بدون مسافات)
                    const usernameRegex = /^[a-zA-Z0-9_.-]+$/;
                    if (usernameInput && usernameInput.value && !usernameRegex.test(usernameInput.value)) {
                        usernameInput.classList.add('is-invalid');
                        isValid = false;
                        errorMsg = 'اسم المستخدم يجب أن يكون باللغة الإنجليزية ولا يحتوي على مسافات.';
                    }

                    // 3. تقييد كلمة المرور (6 أحرف على الأقل)
                    if (passInput && passInput.value && passInput.value.length < 6) {
                        passInput.classList.add('is-invalid');
                        isValid = false;
                        errorMsg = 'كلمة المرور يجب أن لا تقل عن 6 أحرف أو أرقام.';
                    }
                }

                const phoneValidation = validateOwnerStepPhones(true);
                if (!phoneValidation.isValid) {
                    isValid = false;
                    errorMsg = phoneValidation.message;
                }
            }


            if (currentSectionId === 'step_general') {
                const latDisplay = document.getElementById('add-lat-display');
                const lngDisplay = document.getElementById('add-lng-display');
                const latHidden = document.getElementById('add-lat');
                const lngHidden = document.getElementById('add-lng');
                const citySelect = document.querySelector('select[name="CityId"]');

                if ((latDisplay && latDisplay.classList.contains('is-invalid')) ||
                    (lngDisplay && lngDisplay.classList.contains('is-invalid'))) {
                    isValid = false;
                    errorMsg = 'عذراً، الإحداثيات المدخلة تقع خارج حدود الجمهورية اليمنية!';
                }
                else if (!latHidden.value || !lngHidden.value || !latDisplay.value || !lngDisplay.value) {
                    isValid = false;
                    errorMsg = 'يجب تحديد موقع المنشأة بدقة (إما بسحب الدبوس أو بإدخال الإحداثيات يدوياً).';
                    if (latDisplay) latDisplay.classList.add('is-invalid');
                    if (lngDisplay) lngDisplay.classList.add('is-invalid');
                }
                else if (citySelect && citySelect.value) {
                    const selectedCityId = citySelect.value;
                    const cityCenter = window.yemenCities[selectedCityId];

                    if (cityCenter) {
                        const tolerance = 0.8;
                        const isLatMatch = Math.abs(parseFloat(latHidden.value) - cityCenter.lat) <= tolerance;
                        const isLngMatch = Math.abs(parseFloat(lngHidden.value) - cityCenter.lng) <= tolerance;

                        if (!isLatMatch || !isLngMatch) {
                            const cityName = citySelect.options[citySelect.selectedIndex].text;
                            isValid = false;

                            errorMsg = `لقد اخترت مدينة (${cityName})، ولكن الخريطة في منطقة أخرى. يرجى تعديلهما ليتطابقا.`;

                            citySelect.classList.add('is-invalid');
                            latDisplay.classList.add('is-invalid');
                            lngDisplay.classList.add('is-invalid');
                        } else {
                            latHidden.value = latDisplay.value;
                            lngHidden.value = lngDisplay.value;
                        }
                    }
                }
            }

            if (currentSectionId === 'step_rooms') {
                const hotelError = form.hotelStructureEditor?.validate('step_rooms');
                if (hotelError) { isValid = false; errorMsg = hotelError; }
            }

            if (currentSectionId === 'step_suites') {
                const hotelError = form.hotelStructureEditor?.validate('step_suites');
                if (hotelError) { isValid = false; errorMsg = hotelError; }
            }

            if (currentSectionId === 'step_floors') {
                const hotelError = form.hotelStructureEditor?.validate('step_floors');
                if (hotelError) { isValid = false; errorMsg = hotelError; }
            }

            if (currentSectionId === 'step_chalet_details' || currentSectionId === 'step_hall_details') {

                if (currentSectionId === 'step_hall_details') {
                    const eventTypes = currentSection.querySelectorAll('input[name="EventTypes"]');
                    if (eventTypes.length > 0 && !Array.from(eventTypes).some(cb => cb.checked)) {
                        isValid = false;
                        errorMsg = 'يرجى اختيار مناسبة واحدة على الأقل من (المناسبات المسموحة والمخصصة).';
                    }

                    const seatingTypes = currentSection.querySelectorAll('input[name="SeatingTypes"]');
                    if (seatingTypes.length > 0 && !Array.from(seatingTypes).some(cb => cb.checked)) {
                        isValid = false;
                        errorMsg = 'يرجى اختيار تجهيز واحد على الأقل من (تجهيزات الجلوس والمقاعد).';
                    }
                }

                const mainCapacityInput = document.querySelector('input[name="Capacity"]');
                const mainCapacity = mainCapacityInput && mainCapacityInput.value ? parseInt(mainCapacityInput.value) : 0;

                const chkMorning = document.getElementById('chkMorning');
                const chkEvening = document.getElementById('chkEvening');
                const chkNight = document.getElementById('chkNight');
                const chkNightHall = document.getElementById('chkNightHall');
                const chkFullDay = document.getElementById('chkFullDay');
                const hasCustomPackages = form.customPackagesEditor?.hasEnabledInStep(currentSectionId) || false;

                const isAnyPackageChecked =
                    (chkMorning && chkMorning.checked) ||
                    (chkEvening && chkEvening.checked) ||
                    (chkNight && chkNight.checked) ||
                    (chkNightHall && chkNightHall.checked) ||
                    (chkFullDay && chkFullDay.checked) ||
                    hasCustomPackages;

                if (!isAnyPackageChecked && isValid) {
                    isValid = false;
                    errorMsg = 'يجب تفعيل وإضافة باقة حجز واحدة على الأقل (صباحية، مسائية، إلخ).';
                } else if (isValid) {
                    const validateReq = (selector) => {
                        currentSection.querySelectorAll(selector).forEach(inp => {
                            if (!inp.value.trim()) {
                                inp.classList.add('is-invalid');
                                isValid = false;
                                errorMsg = 'يرجى تعبئة أسعار وبيانات الباقات المحددة بشكل كامل.';
                            }
                            else if (currentSectionId === 'step_hall_details' && inp.name.includes('Capacity')) {
                                const pkgCapacity = parseInt(inp.value);
                                if (pkgCapacity > mainCapacity) {
                                    inp.classList.add('is-invalid');
                                    isValid = false;
                                    errorMsg = `عذراً، سعة الباقة المحددة (${pkgCapacity} شخص) لا يمكن أن تكون أكبر من السعة الإجمالية للقاعة (${mainCapacity} شخص)!`;
                                }
                            }
                        });
                    };

                    if (chkMorning && chkMorning.checked) validateReq('.req-morning');
                    if (chkEvening && chkEvening.checked) validateReq('.req-evening');
                    if (chkNight && chkNight.checked) validateReq('.req-night');
                    if (chkNightHall && chkNightHall.checked) validateReq('.req-night');
                    if (chkFullDay && chkFullDay.checked) validateReq('.req-fullday');

                    const customPackagesError = form.customPackagesEditor?.validate(currentSectionId);
                    if (customPackagesError) {
                        isValid = false;
                        errorMsg = customPackagesError;
                    }
                }
            }


            if (currentSectionId === 'step_facilities') {
                const validateReq = (selector) => {
                    currentSection.querySelectorAll(selector).forEach(inp => {
                        if (!inp.value) { inp.classList.add('is-invalid'); isValid = false; errorMsg = 'يرجى تعبئة بيانات الوحدات الإضافية بشكل كامل.'; }
                    });
                };
                if (document.getElementById('chkHotelChalet')?.checked) validateReq('.req-hchalet');
                if (document.getElementById('chkHotelHall')?.checked) validateReq('.req-hhall');
                if (document.getElementById('chkTairamana')?.checked) validateReq('.req-tairamana');
            }

            if (!isValid) {
                errorText.innerHTML = errorMsg;
                errorContainer.classList.remove('d-none');
                errorContainer.scrollIntoView({ behavior: 'smooth', block: 'center' });
                return;
            }

            currentIndex++;
            updateUI();
        });

        if (btnPrev) btnPrev.addEventListener('click', () => { currentIndex--; updateUI(); });

        let submissionInProgress = false;
        form.addEventListener('submit', (e) => {
            e.preventDefault();
            if (submissionInProgress) return;

            let isValid = true;
            const currentSection = document.getElementById(flowSteps[currentIndex]);
            currentSection.querySelectorAll('[required]').forEach(input => {
                const invalid = input.type === 'checkbox' ? !input.checked : !input.value.trim();
                if (invalid) {
                    input.classList.add('is-invalid');
                    isValid = false;
                }
            });

            if (!isValid) {
                moveToStepAndShowError('يرجى تعبئة الحقول الإجبارية في هذه الخطوة بشكل صحيح.');
                return;
            }

            const phoneValidation = validateOwnerStepPhones(true);
            if (!phoneValidation.isValid) {
                moveToStepAndShowError(phoneValidation.message, 'step_owner');
                return;
            }

            if (systemId === 1) {
                const hotelValidationSteps = ['step_rooms', 'step_suites', 'step_floors'];
                for (const hotelStep of hotelValidationSteps) {
                    const hotelError = form.hotelStructureEditor?.validate(hotelStep);
                    if (hotelError) {
                        moveToStepAndShowError(hotelError, hotelStep);
                        return;
                    }
                }
                form.hotelStructureEditor?.reindex();
            }

            // إعادة فهرسة الباقات المخصصة المفعلة قبل إنشاء FormData مباشرة.
            form.customPackagesEditor?.reindex();

            const formData = new FormData(form);
            applyWizardSubmissionContext(formData, config);

            // إرسال رقم هاتف مسؤول المنشأة بالاسم الذي يتوقعه الخادم.
            // الحقل المرئي لا يحمل name، لذلك نزامن الرقم مع الحقل المخفي
            // ونضعه صراحة داخل FormData حتى لا يصل OwnerPhone فارغًا.
            const ownerPhoneInput = document.getElementById('ownerPhoneDisplay');
            const ownerPhoneHidden = document.getElementById('ownerPhoneReal');
            if (ownerPhoneInput && ownerPhoneInput.value) {
                let finalOwnerPhone = ownerPhoneInput.value.replace(/\s+/g, '');

                if (typeof window.ownerPhoneIti !== 'undefined' && window.ownerPhoneIti) {
                    const intlOwnerPhone = window.ownerPhoneIti.getNumber();
                    if (intlOwnerPhone) finalOwnerPhone = intlOwnerPhone;
                }

                if (ownerPhoneHidden) ownerPhoneHidden.value = finalOwnerPhone;
                formData.set('OwnerPhone', finalOwnerPhone);
            }

            // إرسال رقم المنشأة الدولي بالاسم الذي يتوقعه الخادم.
            const facilityPhoneInput = document.getElementById('contactPhoneDisplay');
            if (facilityPhoneInput && facilityPhoneInput.value) {
                let finalPhone = facilityPhoneInput.value.replace(/\s+/g, '');

                if (typeof window.contactPhoneIti !== 'undefined' && window.contactPhoneIti) {
                    let intlNumber = window.contactPhoneIti.getNumber();
                    if (intlNumber) finalPhone = intlNumber;
                }

                formData.set('PhoneNumber', finalPhone);
            }
            submissionInProgress = true;
            if (btnSubmit) btnSubmit.disabled = true;
            hideWizardInlineError();
            setWizardSavingState(true);

            const abortController = new AbortController();
            const requestTimeout = window.setTimeout(() => abortController.abort(), 45000);

            fetch('/Owner/Place/AddFullPlace', {
                method: 'POST',
                body: formData,
                signal: abortController.signal,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(async response => {
                    const responseText = await response.text();
                    let result;
                    try {
                        result = responseText ? JSON.parse(responseText) : null;
                    } catch {
                        throw new Error(response.ok
                            ? 'استجاب الخادم برد غير مفهوم. راجعي سجل الخادم.'
                            : `فشل الحفظ برمز ${response.status}. راجعي سجل الخادم.`);
                    }
                    if (!response.ok) throw new Error(result?.message || `فشل الحفظ برمز ${response.status}.`);
                    if (!result) throw new Error('لم يُرجع الخادم نتيجة للحفظ.');
                    return result;
                })
                .then(result => {
                    if (result.success) {
                        setWizardSavingState(false);

                        // داخل رحلة توثيق المالك لا نعامل الحفظ كإضافة منشأة عادية من لوحة المالك.
                        // نعيد نتيجة الحفظ إلى VerifyAccount نفسها، وهي تتولى نقل المستخدم
                        // مباشرة إلى السياسات أو العقد بدون إعادة عرض اختيار المسار.
                        if (config.mode === 'verification') {
                            const placeId = Number.parseInt(result.placeId || '0', 10) || 0;
                            const categoryId = Number.parseInt(
                                formData.get('CategoryId') ||
                                form.querySelector('[name="CategoryId"]')?.value ||
                                '0',
                                10
                            ) || 0;
                            const placeName = String(
                                result.placeName ||
                                formData.get('Name') ||
                                form.querySelector('[name="Name"]')?.value ||
                                'المنشأة الجديدة'
                            ).trim();
                            const ownerPhone = String(
                                formData.get('OwnerPhone') ||
                                document.getElementById('ownerPhoneReal')?.value ||
                                ''
                            ).trim();
                            const jobTitle = String(
                                formData.get('JobTitle') ||
                                formData.get('OwnerJobTitle') ||
                                ''
                            ).trim();

                            const detail = {
                                placeId,
                                categoryId,
                                bookingSystemId: systemId,
                                placeName,
                                ownerPhone,
                                jobTitle
                            };

                            // أغلق مودال AddPlace أولاً، ثم سلّم الرحلة إلى VerifyAccount.
                            Swal.close();
                            window.setTimeout(() => {
                                window.dispatchEvent(new CustomEvent(
                                    'yemenGuide:verificationPlaceCreated',
                                    { detail }
                                ));
                            }, 0);
                            return;
                        }

                        if (config.mode === 'admin') {
                            const currentAdminOwnsPlace = config.ownerMode === 'selfadmin';
                            const legacyPlatformPlace = config.ownerMode === 'admin';

                            const targetTab = (currentAdminOwnsPlace || legacyPlatformPlace)
                                ? 'admin-places-tab'
                                : (config.targetTab ||
                                    (systemId > 0 ? 'waiting-owner-tab' : 'admin-places-tab'));

                            Swal.fire({
                                icon: 'success',
                                title: 'تم إنشاء المنشأة بنجاح',
                                text: currentAdminOwnsPlace
                                    ? 'تم ربط المنشأة مباشرة بحساب الأدمن الحالي وحفظها نشطة، وتبقى الحجوزات متوقفة حتى استكمال السياسات.'
                                    : (legacyPlatformPlace
                                        ? 'تم ربط المنشأة بحساب تشغيل الإدارة وحفظها نشطة، وتبقى الحجوزات متوقفة حتى استكمال السياسات.'
                                        : (systemId > 0
                                            ? 'تم إرسال المنشأة إلى المالك المختار ليراجعها ويكمل السياسات.'
                                            : 'تم إنشاء المنشأة وإضافتها إلى منشآت الإدارة.')),
                                confirmButtonText: 'العودة إلى إدارة المنشآت',
                                confirmButtonColor: '#00A6DA',
                                allowOutsideClick: false,
                                customClass: {
                                    container: 'owner-place-wizard-layer',
                                    popup: 'rounded-4 shadow-lg border-0'
                                }
                            }).then(() => {
                                localStorage.setItem('adminActiveTab', targetTab);
                                window.location.href = config.redirectUrl;
                            });

                            return;
                        }

                        let successHtml = '<div class="alert alert-info border-0 rounded-3 mb-2 small fw-bold"><i class="bi bi-info-circle-fill me-1"></i> لن تظهر المنشأة للعملاء حتى تتم الموافقة عليها من قبل الإدارة.</div>';

                        if (systemId > 0) {
                            successHtml += `
                        <div class="alert alert-primary border-0 bg-primary bg-opacity-10 text-primary small fw-bold mt-3 mb-0 text-start shadow-sm" dir="rtl">
                            <i class="bi bi-arrow-left-circle-fill me-1"></i> سيتم نقلك إلى (الخدمات والباقات) لاستكمال الخدمات الإضافية وسياسات الحجز والإلغاء.
                        </div>`;
                        }

                        Swal.fire({
                            html: successHtml,
                            title: 'تم الإرسال بنجاح',
                            showConfirmButton: true,
                            confirmButtonText: 'متابعة <i class="bi bi-arrow-left ms-1"></i>',
                            buttonsStyling: false,
                            allowOutsideClick: false,
                            customClass: {
                                container: 'owner-place-wizard-layer',
                                popup: 'rounded-4 shadow-lg border-0 p-3',
                                confirmButton: 'btn btn-primary rounded-pill px-5 py-2 fw-bold shadow-sm w-100 mt-2 fs-5'
                            }
                        }).then(() => {
                            document.querySelectorAll('.tab-pane-content').forEach(content => {
                                content.removeAttribute('data-loaded');
                            });

                            // بعد إنشاء المنشأة تصبح هي المنشأة النشطة في الهيدر،
                            // ثم تعاد الصفحة على تبويب الخدمات والباقات لاستكمال الإعداد.
                            localStorage.setItem('activePlaceTab', 'details-manager-tab');
                            localStorage.setItem('autoSelectPlaceId', String(result.placeId));
                            localStorage.setItem('focusNewPlacePolicyId', String(result.placeId));

                            if (typeof window.setOwnerActivePlace === 'function') {
                                window.setOwnerActivePlace(
                                    result.placeId,
                                    'المنشأة الجديدة',
                                    {
                                        reload: true,
                                        delay: 420,
                                        showScreen: true
                                    }
                                );
                                return;
                            }

                            // احتياط عند عدم تحميل ملف الهيدر لأي سبب.
                            window.location.reload();
                        });

                    } else {
                        setWizardSavingState(false);
                        moveToStepAndShowError(
                            result.message || 'تعذر حفظ المنشأة. راجعي البيانات وحاولي مجدداً.',
                            result.step || ''
                        );
                    }
                })
                .catch(error => {
                    const message = error.name === 'AbortError'
                        ? 'استغرق الحفظ وقتاً أطول من المتوقع وتم إيقاف الطلب. تأكدي من الاتصال ثم حاولي مجدداً.'
                        : (error.message || 'تعذر الاتصال بالخادم أثناء الحفظ.');
                    setWizardSavingState(false);
                    moveToStepAndShowError(message);
                })
                .finally(() => {
                    window.clearTimeout(requestTimeout);
                    submissionInProgress = false;
                    if (btnSubmit) btnSubmit.disabled = false;
                    setWizardSavingState(false);
                });

        });

        updateUI();
    }


    window.initAddPlaceMap = function () {
        const mapEl = document.getElementById("addWizardMap");
        const latHidden = document.getElementById('add-lat');
        const lngHidden = document.getElementById('add-lng');
        const latDisplay = document.getElementById('add-lat-display');
        const lngDisplay = document.getElementById('add-lng-display');
        const syncStatus = document.getElementById('mapSyncStatus');
        const addressInput = document.querySelector('textarea[name="AddressDetail"]');
        const citySelect = document.getElementById('CityId') || document.querySelector('select[name="CityId"]');

        if (latHidden) latHidden.value = '';
        if (lngHidden) lngHidden.value = '';
        if (latDisplay) latDisplay.value = '';
        if (lngDisplay) lngDisplay.value = '';

        let map = null;
        let marker = null;
        let isMapLoaded = (typeof google !== 'undefined' && typeof google.maps !== 'undefined');
        let geocoder = null;
        let placesService = null;
        let addressTimer = 0;
        let addressRequestId = 0;
        let lastSuggestedAddresses = [];

        const cleanAddressText = value =>
            String(value || '')
                .replace(/,\s*Yemen$/i, '')
                .replace(/,\s*اليمن$/i, '')
                .replace(/\s+/g, ' ')
                .trim();

        const escapeHtml = value =>
            String(value || '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#039;');

        const cityName = () => {
            if (!citySelect) return '';
            const option = citySelect.options?.[citySelect.selectedIndex];
            return String(option?.textContent || '').trim();
        };

        const ensureSuggestionBox = () => {
            if (!addressInput) return null;

            let box = document.getElementById('ygAddressSuggestions');
            if (box) return box;

            box = document.createElement('div');
            box.id = 'ygAddressSuggestions';
            box.className = 'yg-address-suggestions';
            box.hidden = true;
            box.setAttribute('role', 'listbox');
            box.setAttribute('aria-label', 'اقتراحات الموقع التفصيلي');

            addressInput.insertAdjacentElement('afterend', box);
            return box;
        };

        const renderSuggestions = addresses => {
            const box = ensureSuggestionBox();
            if (!box || !addressInput) return;

            const seen = new Set();
            lastSuggestedAddresses = [];

            addresses.forEach(value => {
                const clean = cleanAddressText(value);
                const key = clean.toLowerCase();
                if (!clean || seen.has(key)) return;
                seen.add(key);
                lastSuggestedAddresses.push(clean);
            });

            lastSuggestedAddresses =
                lastSuggestedAddresses.slice(0, 5);

            if (!lastSuggestedAddresses.length) {
                box.innerHTML = '';
                box.hidden = true;
                return;
            }

            box.innerHTML = lastSuggestedAddresses
                .map((address, index) => `
                    <button type="button"
                            class="yg-address-suggestion"
                            data-address-index="${index}"
                            role="option">
                        <span class="yg-address-suggestion__icon">
                            <i class="bi bi-geo-alt-fill"></i>
                        </span>
                        <span>${escapeHtml(address)}</span>
                        <i class="bi bi-chevron-left"></i>
                    </button>
                `)
                .join('');

            box.hidden = false;

            box.querySelectorAll('[data-address-index]')
                .forEach(button => {
                    button.addEventListener('click', () => {
                        const index =
                            Number.parseInt(
                                button.dataset.addressIndex || '-1',
                                10
                            );

                        if (
                            index >= 0 &&
                            lastSuggestedAddresses[index]
                        ) {
                            addressInput.value =
                                lastSuggestedAddresses[index];

                            addressInput.dataset.autoAddress =
                                lastSuggestedAddresses[index];

                            addressInput.dispatchEvent(
                                new Event(
                                    'input',
                                    { bubbles: true }
                                )
                            );

                            box.hidden = true;
                        }
                    });
                });
        };

        const setAutomaticAddress = value => {
            if (!addressInput) return;

            const clean = cleanAddressText(value);
            if (!clean) return;

            const current = addressInput.value.trim();
            const previousAuto =
                addressInput.dataset.autoAddress || '';

            // إذا كتب المستخدم عنوانه بنفسه لا نمسحه.
            if (!current || current === previousAuto) {
                addressInput.value = clean;
                addressInput.dataset.autoAddress = clean;
                addressInput.dispatchEvent(
                    new Event('input', { bubbles: true })
                );
            }
        };

        const buildNominatimDescription = data => {
            const address = data?.address || {};
            const name =
                data?.name ||
                address.amenity ||
                address.shop ||
                address.tourism ||
                address.building ||
                address.office ||
                address.leisure ||
                '';

            const road =
                address.road ||
                address.pedestrian ||
                address.residential ||
                address.path ||
                address.neighbourhood ||
                '';

            const neighbourhood =
                address.neighbourhood ||
                address.suburb ||
                address.quarter ||
                address.city_district ||
                '';

            const city =
                address.city ||
                address.town ||
                address.village ||
                address.municipality ||
                cityName() ||
                '';

            const suggestions = [];

            if (name) {
                suggestions.push(
                    [
                        `بالقرب من ${name}`,
                        road,
                        neighbourhood,
                        city
                    ]
                        .filter(Boolean)
                        .filter(
                            (item, index, array) =>
                                array.indexOf(item) === index
                        )
                        .join('، ')
                );
            }

            suggestions.push(
                [road, neighbourhood, city]
                    .filter(Boolean)
                    .filter(
                        (item, index, array) =>
                            array.indexOf(item) === index
                    )
                    .join('، ')
            );

            suggestions.push(
                [neighbourhood, city]
                    .filter(Boolean)
                    .filter(
                        (item, index, array) =>
                            array.indexOf(item) === index
                    )
                    .join('، ')
            );

            const display =
                cleanAddressText(data?.display_name || '');

            if (display) {
                suggestions.push(display);
            }

            return suggestions.filter(Boolean);
        };

        const fetchNominatimAddress = async position => {
            const url =
                'https://nominatim.openstreetmap.org/reverse' +
                `?format=jsonv2&lat=${encodeURIComponent(position.lat)}` +
                `&lon=${encodeURIComponent(position.lng)}` +
                '&zoom=18&addressdetails=1&accept-language=ar';

            const controller = new AbortController();
            const timeoutId =
                window.setTimeout(
                    () => controller.abort(),
                    5500
                );

            try {
                const response = await fetch(url, {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/json'
                    },
                    signal: controller.signal,
                    cache: 'no-store'
                });

                if (!response.ok) {
                    return [];
                }

                const data = await response.json();
                return buildNominatimDescription(data);
            } catch (_) {
                return [];
            } finally {
                window.clearTimeout(timeoutId);
            }
        };

        const showAddressLoadingState = visible => {
            if (!addressInput) return;

            const wrapper =
                addressInput.closest(
                    '.form-group, .mb-3, .mb-4, .col-12'
                ) ||
                addressInput.parentElement;

            if (!wrapper) return;

            let status =
                wrapper.querySelector(
                    '.yg-address-auto-status'
                );

            if (!status) {
                status =
                    document.createElement('div');

                status.className =
                    'yg-address-auto-status';

                status.innerHTML = `
                    <span class="yg-address-auto-status__spinner"></span>
                    <span>جاري تحديد وصف الموقع...</span>
                `;

                addressInput.insertAdjacentElement(
                    'afterend',
                    status
                );
            }

            status.hidden = !visible;
        };

        const distanceMeters = (from, to) => {
            const lat1 = Number(from?.lat);
            const lng1 = Number(from?.lng);
            const lat2 =
                typeof to?.lat === 'function'
                    ? Number(to.lat())
                    : Number(to?.lat);
            const lng2 =
                typeof to?.lng === 'function'
                    ? Number(to.lng())
                    : Number(to?.lng);

            if (
                !Number.isFinite(lat1) ||
                !Number.isFinite(lng1) ||
                !Number.isFinite(lat2) ||
                !Number.isFinite(lng2)
            ) {
                return Number.POSITIVE_INFINITY;
            }

            const earthRadius = 6371000;
            const toRad = value => value * Math.PI / 180;

            const dLat = toRad(lat2 - lat1);
            const dLng = toRad(lng2 - lng1);
            const a =
                Math.sin(dLat / 2) ** 2 +
                Math.cos(toRad(lat1)) *
                Math.cos(toRad(lat2)) *
                Math.sin(dLng / 2) ** 2;

            return earthRadius *
                2 *
                Math.atan2(
                    Math.sqrt(a),
                    Math.sqrt(1 - a)
                );
        };

        const buildPlaceDescription = (name, address, distance) => {
            const cleanName = cleanAddressText(name || '');
            const cleanAddress = cleanAddressText(address || '');
            const city = cityName();

            const parts = [];

            if (cleanName) {
                parts.push(`بالقرب من ${cleanName}`);
            }

            if (cleanAddress) {
                parts.push(cleanAddress);
            }

            if (
                city &&
                !parts.some(part =>
                    part.includes(city)
                )
            ) {
                parts.push(city);
            }

            const description = Array.from(
                new Set(parts.filter(Boolean))
            ).join('، ');

            if (!description) return '';

            // إذا كان المكان ملاصقاً تقريباً للنقطة فلا نكتب مسافة.
            if (
                Number.isFinite(distance) &&
                distance >= 80 &&
                distance <= 1000
            ) {
                return `${description} — يبعد نحو ${Math.round(distance)} م`;
            }

            return description;
        };

        const searchModernNearbyPlaces = async position => {
            try {
                if (
                    !google?.maps?.importLibrary
                ) {
                    return [];
                }

                const placesLibrary =
                    await google.maps.importLibrary('places');

                const PlaceClass =
                    placesLibrary?.Place;

                const RankPreference =
                    placesLibrary?.SearchNearbyRankPreference;

                if (
                    !PlaceClass?.searchNearby ||
                    !RankPreference
                ) {
                    return [];
                }

                const response =
                    await withTimeout(
                        PlaceClass.searchNearby({
                            fields: [
                                'displayName',
                                'formattedAddress',
                                'location',
                                'primaryType',
                                'types'
                            ],
                            locationRestriction: {
                                center: position,
                                radius: 1800
                            },
                            rankPreference:
                                RankPreference.DISTANCE,
                            maxResultCount: 8,
                            language: 'ar',
                            region: 'YE'
                        }),
                        850,
                        { places: [] }
                    );

                const rows =
                    Array.isArray(response?.places)
                        ? response.places
                        : [];

                return rows
                    .map(place => {
                        const displayName =
                            typeof place?.displayName === 'string'
                                ? place.displayName
                                : place?.displayName?.text || '';

                        const distance =
                            distanceMeters(
                                position,
                                place?.location
                            );

                        return {
                            name: displayName,
                            address:
                                place?.formattedAddress || '',
                            distance
                        };
                    })
                    .filter(item => item.name)
                    .sort(
                        (a, b) =>
                            a.distance - b.distance
                    )
                    .slice(0, 6);
            } catch (error) {
                console.warn(
                    'Modern Google Places nearby search failed:',
                    error
                );
                return [];
            }
        };

        const searchLegacyNearbyPlaces = position =>
            new Promise(resolve => {
                if (
                    !map ||
                    typeof google === 'undefined' ||
                    !google.maps?.places?.PlacesService
                ) {
                    resolve([]);
                    return;
                }

                placesService =
                    placesService ||
                    new google.maps.places.PlacesService(map);

                let settled = false;

                const done = value => {
                    if (settled) return;
                    settled = true;
                    resolve(value);
                };

                const timeoutId =
                    window.setTimeout(
                        () => done([]),
                        850
                    );

                placesService.nearbySearch(
                    {
                        location: position,
                        rankBy:
                            google.maps.places.RankBy.DISTANCE
                    },
                    (places, status) => {
                        window.clearTimeout(
                            timeoutId
                        );

                        if (
                            status !==
                            google.maps.places.PlacesServiceStatus.OK ||
                            !Array.isArray(places)
                        ) {
                            done([]);
                            return;
                        }

                        const rows = places
                            .filter(place => place?.name)
                            .map(place => ({
                                name:
                                    place.name || '',
                                address:
                                    place.vicinity || '',
                                distance:
                                    distanceMeters(
                                        position,
                                        place.geometry?.location
                                    )
                            }))
                            .sort(
                                (a, b) =>
                                    a.distance -
                                    b.distance
                            )
                            .slice(0, 6);

                        done(rows);
                    }
                );
            });

        const buildNearbyPlaceSuggestions = async position => {
            const modern =
                await searchModernNearbyPlaces(
                    position
                );

            if (modern.length) {
                return modern
                    .map(place =>
                        buildPlaceDescription(
                            place.name,
                            place.address,
                            place.distance
                        )
                    )
                    .filter(Boolean);
            }

            const legacy =
                await searchLegacyNearbyPlaces(
                    position
                );

            return legacy
                .map(place =>
                    buildPlaceDescription(
                        place.name,
                        place.address,
                        place.distance
                    )
                )
                .filter(Boolean);
        };

        const withTimeout = (promise, timeoutMs, fallbackValue) =>
            Promise.race([
                Promise.resolve(promise)
                    .catch(() => fallbackValue),
                new Promise(resolve =>
                    window.setTimeout(
                        () => resolve(fallbackValue),
                        timeoutMs
                    )
                )
            ]);

        const reverseGeocodeFast = position =>
            new Promise(resolve => {
                if (
                    typeof google === 'undefined' ||
                    !google.maps?.Geocoder
                ) {
                    resolve([]);
                    return;
                }

                geocoder =
                    geocoder ||
                    new google.maps.Geocoder();

                let settled = false;
                const done = value => {
                    if (settled) return;
                    settled = true;
                    resolve(value);
                };

                const timer =
                    window.setTimeout(
                        () => done([]),
                        850
                    );

                geocoder.geocode(
                    {
                        location: position,
                        language: 'ar',
                        region: 'YE'
                    },
                    (results, status) => {
                        window.clearTimeout(timer);

                        if (
                            status !== 'OK' ||
                            !Array.isArray(results)
                        ) {
                            done([]);
                            return;
                        }

                        const rows = results
                            .slice()
                            .sort((a, b) => {
                                const score = item => {
                                    const types =
                                        item?.types || [];

                                    if (
                                        types.includes(
                                            'street_address'
                                        )
                                    ) return 0;

                                    if (
                                        types.includes(
                                            'premise'
                                        )
                                    ) return 1;

                                    if (
                                        types.includes(
                                            'route'
                                        )
                                    ) return 2;

                                    if (
                                        types.includes(
                                            'neighborhood'
                                        ) ||
                                        types.includes(
                                            'sublocality'
                                        )
                                    ) return 3;

                                    return 8;
                                };

                                return score(a) - score(b);
                            })
                            .slice(0, 5)
                            .map(item =>
                                cleanAddressText(
                                    item.formatted_address
                                )
                            )
                            .filter(Boolean);

                        done(rows);
                    }
                );
            });

        const updateSmartAddress = (lat, lng, immediate = false) => {
            if (!addressInput || !isMapLoaded) return;

            window.clearTimeout(addressTimer);

            addressTimer = window.setTimeout(async () => {
                const requestId = ++addressRequestId;
                const position = {
                    lat: Number(lat),
                    lng: Number(lng)
                };

                showAddressLoadingState(true);

                // حماية نهائية: لا يبقى "جاري تحديد..." ظاهرًا أكثر من 1.2 ثانية.
                const hardStopTimer =
                    window.setTimeout(() => {
                        if (requestId === addressRequestId) {
                            showAddressLoadingState(false);
                        }
                    }, 1200);

                try {
                    // نبدأ الاثنين معًا بدل انتظار خدمة ثم الأخرى.
                    const geocoderPromise =
                        reverseGeocodeFast(position);

                    const poiPromise =
                        withTimeout(
                            buildNearbyPlaceSuggestions(
                                position
                            ),
                            950,
                            []
                        );

                    // 1) ننتظر Google Geocoder مدة قصيرة فقط.
                    const geocoderAddresses =
                        await geocoderPromise;

                    if (requestId !== addressRequestId) {
                        return;
                    }

                    // نكتب عنوان الشارع فورًا إن وجد، فلا يشعر المالك بأي انتظار.
                    if (geocoderAddresses.length) {
                        setAutomaticAddress(
                            geocoderAddresses[0]
                        );
                        renderSuggestions(
                            geocoderAddresses
                        );
                        showAddressLoadingState(false);
                    }

                    // 2) أقرب POI يأتي بالتوازي، ونرقّي الوصف فقط إذا وصل سريعًا.
                    const placeAddresses =
                        await poiPromise;

                    if (requestId !== addressRequestId) {
                        return;
                    }

                    if (placeAddresses.length) {
                        setAutomaticAddress(
                            placeAddresses[0]
                        );

                        renderSuggestions([
                            ...placeAddresses,
                            ...geocoderAddresses
                        ]);

                        showAddressLoadingState(false);
                        return;
                    }

                    if (geocoderAddresses.length) {
                        showAddressLoadingState(false);
                        return;
                    }

                    // 3) fallback احتياطي سريع فقط إذا Google كله لم يُرجع شيئًا.
                    const fallbackAddresses =
                        await withTimeout(
                            fetchNominatimAddress(
                                position
                            ),
                            850,
                            []
                        );

                    if (requestId !== addressRequestId) {
                        return;
                    }

                    if (fallbackAddresses.length) {
                        setAutomaticAddress(
                            fallbackAddresses[0]
                        );
                        renderSuggestions(
                            fallbackAddresses
                        );
                        showAddressLoadingState(false);
                        return;
                    }

                    // لا نترك الحقل فارغًا ولا نُبقي التحميل مستمرًا.
                    const city = cityName();

                    const emergencyAddress =
                        city
                            ? `${city}، قرب الموقع المحدد على الخريطة`
                            : 'قرب الموقع المحدد على الخريطة';

                    setAutomaticAddress(
                        emergencyAddress
                    );
                    renderSuggestions([
                        emergencyAddress
                    ]);
                    showAddressLoadingState(false);
                } finally {
                    window.clearTimeout(
                        hardStopTimer
                    );
                }
            }, immediate ? 0 : 120);
        };

        if (addressInput) {
            ensureSuggestionBox();

            addressInput.addEventListener('input', () => {
                const previousAuto =
                    addressInput.dataset.autoAddress || '';

                if (
                    addressInput.value.trim() !==
                    previousAuto
                ) {
                    addressInput.dataset.autoAddress = '';
                }
            });

            addressInput.addEventListener('focus', () => {
                const box =
                    document.getElementById(
                        'ygAddressSuggestions'
                    );

                if (
                    box &&
                    lastSuggestedAddresses.length
                ) {
                    box.hidden = false;
                }
            });

            document.addEventListener(
                'pointerdown',
                event => {
                    const box =
                        document.getElementById(
                            'ygAddressSuggestions'
                        );

                    if (
                        box &&
                        event.target !== addressInput &&
                        !box.contains(event.target)
                    ) {
                        box.hidden = true;
                    }
                },
                { passive: true }
            );
        }

        if (!isMapLoaded && mapEl) {
            mapEl.innerHTML = `
            <div class="alert alert-warning p-4 text-center mt-3 fw-bold rounded-4 shadow-sm" style="border: 2px dashed #ffc107;">
                <i class="bi bi-wifi-off fs-1 d-block mb-2 text-warning"></i>
                تعذر تحميل الخريطة.<br>
                <span class="text-muted small">قم بكتابة خط الطول وخط العرض يدوياً.</span>
            </div>`;
        } else if (mapEl) {

            let initialCityId = citySelect ? citySelect.value : 'default';
            let visualCenter = window.yemenCities[initialCityId] || window.yemenCities['default'];

            map = new google.maps.Map(mapEl, {
                zoom: 12,
                center: visualCenter,
                mapTypeControl: false,
                streetViewControl: false,
                fullscreenControl: true,
                restriction: {
                    latLngBounds: window.yemenBounds,
                    strictBounds: true,
                }
            });

            marker = new google.maps.Marker({
                map: map,
                draggable: true,
                animation: google.maps.Animation.DROP
            });

            marker.setPosition(visualCenter);

            marker.addListener("dragend", (event) => {
                const lat = event.latLng.lat();
                const lng = event.latLng.lng();

                if (latDisplay) {
                    latDisplay.value = lat.toFixed(6);
                }
                if (lngDisplay) {
                    lngDisplay.value = lng.toFixed(6);
                }

                handleManualInput(true);
                updateSmartAddress(lat, lng, true);
            });

            map.addListener("click", (event) => {
                marker.setPosition(event.latLng);

                const lat = event.latLng.lat();
                const lng = event.latLng.lng();

                if (latDisplay) {
                    latDisplay.value = lat.toFixed(6);
                }
                if (lngDisplay) {
                    lngDisplay.value = lng.toFixed(6);
                }

                handleManualInput(true);
                updateSmartAddress(lat, lng, true);
            });

            if (citySelect) {
                citySelect.addEventListener(
                    'change',
                    function () {
                        let selectedCityId = this.value;
                        let newCenter =
                            window.yemenCities[
                            selectedCityId
                            ] ||
                            window.yemenCities[
                            'default'
                            ];

                        if (map && marker) {
                            map.setCenter(newCenter);
                            map.setZoom(13);
                            marker.setPosition(newCenter);

                            if (latDisplay) {
                                latDisplay.value =
                                    newCenter.lat
                                        .toFixed(6);
                            }

                            if (lngDisplay) {
                                lngDisplay.value =
                                    newCenter.lng
                                        .toFixed(6);
                            }

                            handleManualInput();
                            updateSmartAddress(
                                newCenter.lat,
                                newCenter.lng
                            );
                        }
                    }
                );
            }

            setTimeout(() => {
                google.maps.event.trigger(
                    map,
                    'resize'
                );
                map.setCenter(visualCenter);

                const initialLat =
                    parseFloat(latDisplay?.value || '');
                const initialLng =
                    parseFloat(lngDisplay?.value || '');

                if (
                    Number.isFinite(initialLat) &&
                    Number.isFinite(initialLng)
                ) {
                    updateSmartAddress(
                        initialLat,
                        initialLng,
                        true
                    );
                }
            }, 500);
        }

        function handleManualInput(fromMapAction = false) {
            const latDisp =
                document.getElementById(
                    'add-lat-display'
                );
            const lngDisp =
                document.getElementById(
                    'add-lng-display'
                );
            const latHid =
                document.getElementById('add-lat');
            const lngHid =
                document.getElementById('add-lng');

            if (!latDisp || !lngDisp) return;

            let cleanLat =
                latDisp.value.replace(
                    /[^0-9.-]/g,
                    ''
                );
            let cleanLng =
                lngDisp.value.replace(
                    /[^0-9.-]/g,
                    ''
                );

            if (latDisp.value !== cleanLat) {
                latDisp.value = cleanLat;
            }

            if (lngDisp.value !== cleanLng) {
                lngDisp.value = cleanLng;
            }

            let newLat = parseFloat(cleanLat);
            let newLng = parseFloat(cleanLng);

            if (
                !isNaN(newLat) &&
                !isNaN(newLng)
            ) {
                const isInsideYemen =
                    newLat >= 12.0 &&
                    newLat <= 19.5 &&
                    newLng >= 41.0 &&
                    newLng <= 55.0;

                if (!isInsideYemen) {
                    latDisp.classList.add(
                        'is-invalid'
                    );
                    lngDisp.classList.add(
                        'is-invalid'
                    );

                    if (latHid) {
                        latHid.value = '';
                    }
                    if (lngHid) {
                        lngHid.value = '';
                    }

                    if (fromMapAction) {
                        showWizardInlineError(
                            'عفواً، الموقع الذي حددته يقع خارج حدود الجمهورية اليمنية.'
                        );
                    }
                } else {
                    latDisp.classList.remove(
                        'is-invalid'
                    );
                    lngDisp.classList.remove(
                        'is-invalid'
                    );

                    if (latHid) {
                        latHid.value =
                            newLat.toFixed(6);
                    }

                    if (lngHid) {
                        lngHid.value =
                            newLng.toFixed(6);
                    }

                    if (syncStatus) {
                        syncStatus.classList.remove(
                            'd-none'
                        );

                        setTimeout(
                            () =>
                                syncStatus.classList.add(
                                    'd-none'
                                ),
                            2500
                        );
                    }

                    if (
                        isMapLoaded &&
                        marker &&
                        map
                    ) {
                        let newPos = {
                            lat: newLat,
                            lng: newLng
                        };

                        marker.setPosition(newPos);
                        map.setCenter(newPos);
                    }

                    if (!fromMapAction) {
                        updateSmartAddress(
                            newLat,
                            newLng
                        );
                    }
                }
            } else {
                if (latHid) latHid.value = '';
                if (lngHid) lngHid.value = '';
            }
        }

        if (latDisplay) {
            latDisplay.addEventListener(
                'input',
                function () {
                    handleManualInput(false);
                }
            );
        }

        if (lngDisplay) {
            lngDisplay.addEventListener(
                'input',
                function () {
                    handleManualInput(false);
                }
            );
        }
    };

    const galleryInput = document.querySelector('input[name="GalleryImages"]');
    if (galleryInput) {
        galleryInput.addEventListener('change', function () {
            let totalSize = 0;
            const maxAllowedSize = 10 * 1024 * 1024;

            for (let i = 0; i < this.files.length; i++) {
                totalSize += this.files[i].size;
            }

            if (totalSize > maxAllowedSize) {
                showWizardInlineError('إجمالي حجم الصور يتجاوز 10 ميجابايت، يرجى اختيار صور أقل أو ضغطها قبل الرفع.');
                this.value = '';
            }
        });
    }


    // ==========================================================================
    // 🛡️ نظام التحقق الجغرافي الفوري والديناميكي لليمن 🇾🇪
    // ==========================================================================

    function isCoordinateInYemen(value, type) {
        const num = parseFloat(value);
        if (isNaN(num)) return false;

        if (type === 'lat') {
            return num >= 12.0 && num <= 19.5;
        } else if (type === 'lng') {
            return num >= 41.5 && num <= 55.0;
        }
        return false;
    }

    function bindRealTimeValidation(displayId, hiddenId, type) {
        const displayInput = document.getElementById(displayId);
        const hiddenInput = document.getElementById(hiddenId);

        if (!displayInput || displayInput.dataset.coordinateValidationReady === 'true') return;
        displayInput.dataset.coordinateValidationReady = 'true';

        displayInput.addEventListener('blur', function () {
            const val = this.value.trim();

            if (val === '') {
                this.classList.remove('is-invalid');
                if (hiddenInput) hiddenInput.value = '';
                return;
            }

            if (!isCoordinateInYemen(val, type)) {
                this.classList.add('is-invalid');
                if (hiddenInput) hiddenInput.value = '';
            } else {
                this.classList.remove('is-invalid');
                if (hiddenInput) hiddenInput.value = parseFloat(val).toFixed(6);
            }
        });

        displayInput.addEventListener('input', function () {
            if (isCoordinateInYemen(this.value.trim(), type)) {
                this.classList.remove('is-invalid');
            }
        });
    }

    // 🌟 فحص صورة الغلاف (الحد الأقصى 3 ميجا مثلاً)
    document.addEventListener('change', function (e) {
        if (e.target && e.target.name === 'CoverImageUpload') {
            const maxCoverSize = 3 * 1024 * 1024;
            if (e.target.files[0] && e.target.files[0].size > maxCoverSize) {
                showWizardInlineError('يرجى اختيار صورة غلاف حجمها أقل من (3 ميجابايت).');
                e.target.value = ''; // تفريغ الحقل فوراً
            }
        }
    });

    // تصدير دوال التهيئة للملف المركزي بعد الفصل
    window.initAddPlace = initAddPlace;

    // المصدر المشترك الوحيد لتشغيل فورم الإضافة عند المالك والأدمن.
    window.YemenGuidePlaceWizard = Object.freeze({
        openHtml: openWizardHtml,
        yemenBounds: window.yemenBounds,
        yemenCities: window.yemenCities,
        initializeForm: function (rawConfig) {
            const config = normalizeWizardConfig(rawConfig);
            setupWizardLogic(config);
            initPhoneFieldsForWizard();
            window.initAddPlaceMap();
            prefillWizardContext(config);
            initializeArabicFileUploads(document.getElementById('fullPlaceSurveyForm'));
        }
    });

})();



function showWizardInlineError(message, type = 'danger') {
    const errorContainer = document.getElementById('wizardErrorContainer');
    const errorText = document.getElementById('wizardErrorText');
    if (errorContainer && errorText) {
        errorText.textContent = message;
        errorContainer.classList.remove('alert-danger', 'alert-warning', 'alert-info');
        errorContainer.classList.add(`alert-${type}`);
        errorContainer.classList.remove('d-none');
        errorContainer.scrollIntoView({ behavior: 'smooth', block: 'center' });
    } else if (typeof Swal !== 'undefined' && Swal.getPopup()) {
        Swal.showValidationMessage(message);
    }
}

function hideWizardInlineError() {
    const errorContainer = document.getElementById('wizardErrorContainer');
    if (errorContainer) {
        errorContainer.classList.add('d-none');
    }
    if (typeof Swal !== 'undefined' && Swal.getPopup()) {
        Swal.resetValidationMessage();
    }
}
ل