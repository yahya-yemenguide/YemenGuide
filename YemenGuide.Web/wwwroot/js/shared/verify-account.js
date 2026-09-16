﻿/**
 * =============================================================
 * YemenGuide Owner Verification - المرحلة الثالثة: السياسات + العقد + الإثبات
 * =============================================================
 */

document.addEventListener("DOMContentLoaded", function () {

    // --- 1. المتغيرات وعناصر التحكم الأساسية ---
    let currentStep = 0;
    let currentTrack = '';

    const stepper = document.getElementById('mainStepper');
    const progressBar = document.getElementById('wizardProgressBar');
    const isFastTrackInput = document.getElementById('IsFastTrackInput');
    const verificationCategoryInput = document.getElementById('VerificationCategoryId');
    const verificationPlaceInput = document.getElementById('VerificationPlaceId');
    const fullWizardContainer = document.getElementById('fullWizardContainer');
    const fullPlaceCheckpoint = document.getElementById('fullPlaceCheckpoint');
    const savedPlaceName = document.getElementById('verificationSavedPlaceName');
    const policyStep = document.getElementById('stepPolicies');
    const policyReadyPanel = document.getElementById('verificationPolicyReady');
    const policyChoicePanel = document.getElementById('verificationPolicyChoice');
    const policyManualShell = document.getElementById('verificationPolicyManualShell');
    const policyFrame = document.getElementById('verificationPolicyFrame');
    const policyFrameLoading = document.getElementById('verificationPolicyFrameLoading');

    let existingVerificationPlaceId = Number.parseInt(
        document.body.dataset.verificationPlaceId || verificationPlaceInput?.value || '0',
        10
    ) || 0;
    let existingVerificationCategoryId = Number.parseInt(
        document.body.dataset.verificationPlaceCategoryId || verificationCategoryInput?.value || '0',
        10
    ) || 0;
    let existingVerificationPlaceName = document.body.dataset.verificationPlaceName || '';
    let existingVerificationBookingSystemId = Number.parseInt(
        document.body.dataset.verificationPlaceBookingSystemId || '0',
        10
    ) || 0;
    let verificationPoliciesReady =
        document.body.dataset.verificationPlaceHasPolicy === 'true';
    let existingVerificationOwnerPhone =
        (document.body.dataset.verificationOwnerPhone || '').trim();

    const defaultCommission = Number.parseFloat(document.body.dataset.defaultCommission || '0') || 0;
    const categoryMeta = new Map();

    document.querySelectorAll('[data-verification-category]').forEach(item => {
        const id = Number.parseInt(item.dataset.categoryId || '0', 10);
        if (!Number.isInteger(id) || id <= 0) return;

        categoryMeta.set(id, {
            id,
            name: item.dataset.categoryName || 'الفئة المختارة',
            commission: Number.parseFloat(item.dataset.commission || String(defaultCommission)) || 0,
            source: item.dataset.commissionSource || 'النسبة العامة',
            bookingSystemId: Number.parseInt(item.dataset.bookingSystemId || '0', 10) || 0
        });
    });

    function formatCommission(value) {
        const number = Number(value);
        if (!Number.isFinite(number)) return '0';
        return Number.isInteger(number) ? String(number) : number.toFixed(2).replace(/0+$/, '').replace(/\.$/, '');
    }

    function invalidateContractAgreement() {
        const terms = document.getElementById('termsCheckbox');
        if (terms) terms.checked = false;
    }

    function hasSavedVerificationPlace() {
        const placeId = Number.parseInt(verificationPlaceInput?.value || String(existingVerificationPlaceId) || '0', 10);
        return Number.isInteger(placeId) && placeId > 0;
    }


    /**
     * AddPlace في المسار الشامل يجب أن يكون شاشة مستقلة، وليس Card داخل Card.
     * هذه الحالة تخفي Shell/Stepper رحلة التوثيق مؤقتاً وتترك نفس AddPlace يأخذ الشاشة.
     */
    function setStandalonePlaceStage(isActive) {
        const active = isActive === true;
        document.body.classList.toggle('verification-place-stage-active', active);
        document.documentElement.classList.toggle('verification-place-stage-active', active);

        if (active) {
            stepper?.classList.add('d-none');
            window.scrollTo({ top: 0, behavior: 'auto' });
        }
    }

    function requiresFacilityPolicies(bookingSystemId = existingVerificationBookingSystemId) {
        const systemId = Number.parseInt(bookingSystemId || '0', 10);
        return systemId >= 1 && systemId <= 4;
    }

    function setVerificationPoliciesReady(isReady, mode = '') {
        verificationPoliciesReady = isReady === true;
        document.body.dataset.verificationPlaceHasPolicy =
            verificationPoliciesReady ? 'true' : 'false';

        policyReadyPanel?.classList.toggle('d-none', !verificationPoliciesReady);
        policyChoicePanel?.classList.toggle('d-none', verificationPoliciesReady);

        if (verificationPoliciesReady) {
            policyManualShell?.classList.add('d-none');
            if (policyFrame) policyFrame.removeAttribute('src');
        }

        const nextLabel = document.getElementById('fullPlaceNextLabel');
        if (nextLabel) {
            nextLabel.textContent =
                requiresFacilityPolicies()
                    ? (verificationPoliciesReady ? 'مراجعة السياسات' : 'سياسات المنشأة')
                    : 'اتفاقية الشراكة';
        }

        if (mode) {
            document.body.dataset.verificationPolicyMode = mode;
        }
    }

    function getVerificationPolicyUrl() {
        if (!hasSavedVerificationPlace()) return '';

        return `/Owner/Settings/FacilitySettings?autoPlaceId=${encodeURIComponent(existingVerificationPlaceId)}&verificationFlow=true&mode=verification`;
    }

    function openManualPolicyEditor() {
        invalidateContractAgreement();

        if (!hasSavedVerificationPlace()) {
            Swal.fire({
                icon: 'warning',
                title: 'المنشأة غير محفوظة',
                text: 'أكمل إنشاء المنشأة أولاً.',
                confirmButtonColor: '#00A6DA'
            });
            return;
        }

        policyChoicePanel?.classList.add('d-none');
        policyReadyPanel?.classList.add('d-none');
        policyManualShell?.classList.remove('d-none');
        policyFrameLoading?.classList.remove('d-none');
        if (policyFrame) policyFrame.style.height = '520px';

        const url = getVerificationPolicyUrl();
        if (policyFrame && policyFrame.getAttribute('src') !== url) {
            policyFrame.setAttribute('src', url);
        }

        window.setTimeout(() => {
            policyManualShell?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 80);
    }

    function getDefaultPolicyPreviewHtml() {
        // نفس الرسالة الموجودة في إدارة المنشأة / الباقات والخدمات.
        return `
            <div class="text-start mt-3" dir="rtl">
                <p class="text-muted fw-bold">سيتم اعتماد السياسات التالية لمنشأتك فوراً:</p>
                <ul class="mb-0 ps-3 text-secondary small fw-bold" style="line-height: 1.8;">
                    <li>عربون مبدئي <b>20%</b> لتأكيد الحجز.</li>
                    <li>مهلة السداد: <b>12 ساعة</b> لتسديد العربون قبل الإلغاء.</li>
                    <li>نظام <b>اليوم الكامل</b> (استلام 2 ظهراً / تسليم 12 ظهراً).</li>
                    <li>إلغاء مجاني قبل <b>48 ساعة</b> من موعد الدخول.</li>
                    <li>غرامة الإلغاء المتأخر: <b>مصادرة العربون</b> بالكامل.</li>
                </ul>
            </div>`;
    }

    async function applyDefaultVerificationPolicies() {
        if (!hasSavedVerificationPlace()) return;

        const decision = await Swal.fire({
            title: '<h4 class="fw-bold text-success mb-0"><i class="bi bi-magic me-2"></i> تطبيق السياسات الافتراضية؟</h4>',
            html: getDefaultPolicyPreviewHtml(),
            icon: 'question',
            showCancelButton: true,
            showDenyButton: true,
            confirmButtonColor: '#198754',
            denyButtonColor: '#00A6DA',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'نعم، طبقها',
            denyButtonText: 'ضبطها يدوياً',
            cancelButtonText: 'إلغاء الأمر',
            allowOutsideClick: false,
            customClass: { popup: 'verification-default-policy-modal' }
        });

        if (decision.isDenied) {
            openManualPolicyEditor();
            return;
        }
        if (!decision.isConfirmed) return;

        const button = document.getElementById('useDefaultPolicies');
        if (button) {
            button.disabled = true;
            button.classList.add('is-busy');
        }

        Swal.fire({
            title: 'جاري تطبيق الإعدادات...',
            allowOutsideClick: false,
            showConfirmButton: false,
            didOpen: () => Swal.showLoading()
        });

        try {
            const response = await fetch(
                '/Owner/Settings/ApplyDefaultPolicies?verificationFlow=true',
                {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    credentials: 'same-origin',
                    body: JSON.stringify(existingVerificationPlaceId)
                }
            );

            const data = await response.json().catch(() => null);

            if (!response.ok || !data?.success) {
                if (data?.message?.includes('مسبق')) {
                    setVerificationPoliciesReady(true, 'existing');
                    invalidateContractAgreement();
                    Swal.close();
                    await refreshPolicyReviewSummary();
                    window.goToGatewayStep(3);
                    return;
                }
                throw new Error(data?.message || 'تعذر تطبيق السياسات الافتراضية.');
            }

            setVerificationPoliciesReady(true, 'default');
            invalidateContractAgreement();
            await refreshPolicyReviewSummary('default');

            await Swal.fire({
                icon: 'success',
                title: 'تم تطبيق السياسات',
                text: 'تم اعتماد السياسات التي قرأتها. سنعرض لك الآن ملخصها المحفوظ قبل الانتقال إلى العقد.',
                confirmButtonText: 'مراجعة السياسات',
                confirmButtonColor: '#00A6DA',
                allowOutsideClick: false
            });

            window.goToGatewayStep(3);
        } catch (error) {
            Swal.fire({
                icon: 'error',
                title: 'تعذر تطبيق السياسات',
                text: error?.message || 'تعذر تطبيق السياسات الافتراضية.',
                confirmButtonColor: '#FF5A5F'
            });
        } finally {
            if (button) {
                button.disabled = false;
                button.classList.remove('is-busy');
            }
        }
    }

    function safeJsonArray(value) {
        if (Array.isArray(value)) return value;
        if (!value || typeof value !== 'string') return [];
        try {
            const parsed = JSON.parse(value);
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    }

    function formatArabicClock(value) {
        if (!value || !/^\d{2}:\d{2}$/.test(value)) return value || 'غير محدد';
        const [hText, minute] = value.split(':');
        const hour = Number.parseInt(hText, 10);
        const period = hour >= 12 ? 'م' : 'ص';
        return `${hour % 12 || 12}:${minute} ${period}`;
    }

    function getPenaltyText(policy) {
        const type = String(policy?.penaltyType || '');
        const value = Number(policy?.penaltyValue || 0);
        if (type === 'LoseDeposit') return 'مصادرة العربون بالكامل';
        if (type === 'LoseFirstNight' || type === 'FirstNight') return 'مصادرة قيمة الليلة الأولى';
        if (type === 'DeductPercentage') return `خصم ${value}%`;
        if (type === 'DeductFixed') return `خصم ${value} ريال`;
        return 'حسب السياسة المحفوظة';
    }

    function isDefaultPolicySnapshot(policy) {
        const deposits = safeJsonArray(policy?.depositPoliciesJson);
        const firstDeposit = deposits[0] || {};
        return String(firstDeposit.val ?? '') === '20' &&
            String(firstDeposit.type ?? '') === '%' &&
            Number(policy?.paymentTimeout || 0) === 12 &&
            String(policy?.operationSystemMode || '') === 'FullDay' &&
            String(policy?.checkInTime || '') === '14:00' &&
            String(policy?.checkOutTime || '') === '12:00' &&
            Number(policy?.freeCancellationHours || 0) === 48 &&
            String(policy?.penaltyType || '') === 'LoseDeposit';
    }

    function buildPolicyReviewItems(policy) {
        const deposits = safeJsonArray(policy?.depositPoliciesJson);
        const depositText = deposits.length
            ? deposits.map(item => `${item?.val ?? ''}${item?.type ?? ''}${item?.desc ? ` — ${item.desc}` : ''}`).join('، ')
            : 'لا توجد سياسة عربون مسجلة';

        const operationMode = String(policy?.operationSystemMode || '');
        const operationText = operationMode === 'FullDay'
            ? `اليوم الكامل — دخول ${formatArabicClock(policy?.checkInTime)} / خروج ${formatArabicClock(policy?.checkOutTime)}`
            : operationMode === 'Shifts'
                ? 'نظام فترات تشغيل حسب الفترات المحفوظة'
                : (operationMode || 'غير محدد');

        const approvalText = String(policy?.bookingApprovalMode || '').toLowerCase() === 'instant'
            ? 'تأكيد فوري'
            : 'موافقة المنشأة قبل التأكيد';

        const noShowText = policy?.confiscateOnNoShow === true
            ? `مصادرة مفعلة بعد مهلة ${Number(policy?.noShowGraceMinutes || 60)} دقيقة`
            : `بدون مصادرة تلقائية — مهلة ${Number(policy?.noShowGraceMinutes || 60)} دقيقة`;

        const currencies = [];
        if (policy?.isSarSupported === true) currencies.push('SAR');
        if (policy?.isUsdSupported === true) currencies.push('USD');

        return [
            ['تأكيد الحجز', approvalText],
            ['العربون', depositText],
            ['مهلة السداد', `${Number(policy?.paymentTimeout || 0)} ساعة`],
            ['التشغيل', operationText],
            ['الإلغاء المجاني', `قبل ${Number(policy?.freeCancellationHours || 0)} ساعة من موعد الدخول`],
            ['الإلغاء المتأخر', getPenaltyText(policy)],
            ['عدم الحضور', noShowText],
            ['الاسترداد', `المعالجة خلال ${Number(policy?.refundProcessingDays || 0)} أيام`],
            ['الحجز المسبق', `قبل ${Number(policy?.minimumNoticeHours || 0)} ساعة على الأقل`],
            ['أفق الحجز', `${Number(policy?.maximumHorizonMonths || 0)} أشهر`],
            ['العملات', currencies.length ? currencies.join(' + ') : 'حسب وسائل الدفع المتاحة']
        ];
    }

    function renderPolicyReviewSummary(policy, forcedMode = '') {
        const grid = document.getElementById('policyReviewSummaryGrid');
        const mode = document.getElementById('policyReviewMode');
        const placeName = document.getElementById('policyReviewPlaceName');
        if (!grid || !mode || !policy) return;

        const rememberedMode = forcedMode || document.body.dataset.verificationPolicyMode || '';
        const isDefault = rememberedMode === 'default' ||
            (rememberedMode !== 'manual' && isDefaultPolicySnapshot(policy));

        mode.textContent = isDefault ? 'السياسات الافتراضية' : 'سياسات مخصصة يدويًا';
        mode.classList.toggle('is-default', isDefault);
        mode.classList.toggle('is-manual', !isDefault);

        if (placeName) {
            placeName.textContent = existingVerificationPlaceName || 'المنشأة الجديدة';
        }

        grid.innerHTML = buildPolicyReviewItems(policy)
            .map(([label, value], index) => `
                <article class="policy-review-item">
                    <span class="policy-review-item__index">${String(index + 1).padStart(2, '0')}</span>
                    <div>
                        <small>${label}</small>
                        <strong>${value}</strong>
                    </div>
                    <i class="bi bi-check2-circle"></i>
                </article>`)
            .join('');
    }

    async function refreshPolicyReviewSummary(forcedMode = '') {
        if (currentTrack !== 'full' || !hasSavedVerificationPlace()) {
            return null;
        }

        const grid = document.getElementById('policyReviewSummaryGrid');
        const continueButton = document.getElementById('policyReviewContinue');
        if (grid) {
            grid.dataset.ready = 'false';
            grid.innerHTML = `
                <div class="policy-review-loading">
                    <span class="spinner-border spinner-border-sm" role="status"></span>
                    <strong>جاري قراءة السياسة المحفوظة من النظام...</strong>
                </div>`;
        }
        if (continueButton) continueButton.disabled = true;

        try {
            const response = await fetch(
                `/Owner/Settings/GetFacilitySettings?placeId=${encodeURIComponent(existingVerificationPlaceId)}&verificationFlow=true`,
                {
                    method: 'GET',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    credentials: 'same-origin'
                }
            );
            const result = await response.json().catch(() => null);
            if (!response.ok || !result?.success || result?.isNew || !result?.data) {
                throw new Error(result?.message || 'تعذر قراءة السياسة المحفوظة.');
            }

            renderPolicyReviewSummary(result.data, forcedMode);
            if (grid) grid.dataset.ready = 'true';
            if (continueButton) continueButton.disabled = false;
            return result.data;
        } catch (error) {
            console.warn('Unable to load policy summary:', error);
            if (grid) {
                grid.dataset.ready = 'false';
                grid.innerHTML = `
                    <div class="policy-review-load-error">
                        <i class="bi bi-exclamation-triangle-fill"></i>
                        <div><strong>تعذر تحميل ملخص السياسة</strong><span>${error?.message || 'أعد المحاولة أو ارجع لتعديل السياسات.'}</span></div>
                    </div>`;
            }
            if (continueButton) continueButton.disabled = true;
            return null;
        }
    }

    function showFullPlaceCheckpoint(detail) {
        const data = detail || {};
        const placeId = Number.parseInt(data.placeId || verificationPlaceInput?.value || existingVerificationPlaceId || '0', 10) || 0;
        const categoryId = Number.parseInt(data.categoryId || verificationCategoryInput?.value || existingVerificationCategoryId || '0', 10) || 0;
        const placeName = data.placeName || existingVerificationPlaceName || 'المنشأة الجديدة';
        const bookingSystemId = Number.parseInt(
            data.bookingSystemId || existingVerificationBookingSystemId || '0',
            10
        ) || 0;

        if (placeId > 0) {
            existingVerificationPlaceId = placeId;
            if (verificationPlaceInput) verificationPlaceInput.value = String(placeId);
            document.body.dataset.verificationPlaceId = String(placeId);
        }

        if (categoryId > 0) {
            existingVerificationCategoryId = categoryId;
            if (verificationCategoryInput) verificationCategoryInput.value = String(categoryId);
            document.body.dataset.verificationPlaceCategoryId = String(categoryId);
            updateContractCategory(categoryId);
        }

        existingVerificationPlaceName = placeName;
        existingVerificationBookingSystemId = bookingSystemId;
        document.body.dataset.verificationPlaceName = placeName;

        const contractPlaceSummary = document.getElementById('contractPlaceSummary');
        const contractPlaceName = document.getElementById('contractPlaceName');
        const policyReviewPlaceName = document.getElementById('policyReviewPlaceName');
        if (contractPlaceName) contractPlaceName.textContent = placeName;
        if (policyReviewPlaceName) policyReviewPlaceName.textContent = placeName;
        contractPlaceSummary?.classList.toggle('d-none', currentTrack !== 'full');
        document.body.dataset.verificationPlaceBookingSystemId = String(bookingSystemId);
        if (savedPlaceName) savedPlaceName.textContent = placeName;

        setVerificationPoliciesReady(verificationPoliciesReady);

        // انتهى AddPlace: نعيد Shell التوثيق قبل إظهار نقطة الاستكمال.
        setStandalonePlaceStage(false);
        fullWizardContainer?.classList.add('d-none');
        fullPlaceCheckpoint?.classList.remove('d-none');
        document.getElementById('step1_Full')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function updateContractCategory(categoryId) {
        const id = Number.parseInt(categoryId || '0', 10);
        const meta = categoryMeta.get(id);
        const categoryName = document.getElementById('contractCategoryName');
        const commissionValue = document.getElementById('contractCommissionValue');
        const commissionInline = document.getElementById('contractCommissionInline');
        const commissionSource = document.getElementById('contractCommissionSource');

        if (!meta) {
            if (categoryName) categoryName.textContent = 'سيتم تحديدها من اختيارك';
            if (commissionValue) commissionValue.textContent = formatCommission(defaultCommission);
            if (commissionInline) commissionInline.textContent = formatCommission(defaultCommission);
            if (commissionSource) commissionSource.textContent = 'النسبة العامة للمنصة';
            return;
        }

        if (verificationCategoryInput) verificationCategoryInput.value = String(meta.id);
        if (categoryName) categoryName.textContent = meta.name;
        if (commissionValue) commissionValue.textContent = formatCommission(meta.commission);
        if (commissionInline) commissionInline.textContent = formatCommission(meta.commission);
        if (commissionSource) commissionSource.textContent = meta.source === 'خاص بالفئة'
            ? 'نسبة خاصة لهذه الفئة'
            : 'النسبة العامة للمنصة';
    }

    function configureMainStepper() {
        const full = currentTrack === 'full';
        const labels = full
            ? ['المنشأة', 'السياسات', 'مراجعة السياسات', 'العقد', 'التوثيق']
            : ['بيانات المالك', 'اتفاقية الشراكة', 'التوثيق القانوني'];

        document.querySelectorAll('#mainStepper .step-item').forEach((item, index) => {
            const position = index + 1;
            item.classList.toggle('d-none', !full && position > 3);

            const label = item.querySelector('.step-label');
            if (label && labels[index]) {
                label.textContent = labels[index];
            }
        });
    }

    function setCurrentTrack(trackType) {
        currentTrack = trackType || '';
        if (isFastTrackInput) isFastTrackInput.value = currentTrack === 'fast' ? 'true' : 'false';
        document.body.dataset.verificationTrack = trackType || '';
        configureMainStepper();

        const contractPlaceSummary = document.getElementById('contractPlaceSummary');
        contractPlaceSummary?.classList.toggle(
            'd-none',
            currentTrack !== 'full' || !hasSavedVerificationPlace()
        );
    }

    // =============================================================
    // 2. نظام التوجيه واختيار المسار (Gateway Logic)
    // =============================================================

    window.selectTrack = function (trackType) {
        if (trackType === 'fast' && hasSavedVerificationPlace()) {
            Swal.fire({
                icon: 'info',
                title: 'لقد بدأت التأسيس الشامل',
                text: 'تم حفظ منشأة بالفعل ضمن رحلة التوثيق. أكمل المسار الشامل حتى لا تبقى منشأة غير مكتملة.',
                confirmButtonColor: '#00A6DA'
            });
            return;
        }

        setCurrentTrack(trackType);

        // إخفاء شاشة الاختيار بالقوة الجبرية
        const gateway = document.getElementById('step0_Gateway');
        gateway.classList.remove('active-step');
        gateway.classList.add('d-none');
        gateway.style.display = 'none';

        // تصفير وإعداد متطلبات التفويض بناء على المسار
        const authFile = document.getElementById('authorizationFile');
        const authReq = document.getElementById('authRequiredStar');
        const authOpt = document.getElementById('authOptionalSpan');

        if (trackType === 'fast') {
            setStandalonePlaceStage(false);
            document.getElementById('mainStepper').classList.remove('d-none');
            window.goToGatewayStep(1);
            // إعادة ضبط التفويض للمسار السريع (مطلوب مبدئياً حتى يختار الوظيفة)
            document.getElementById('JobTitleSelectFast').value = "";
            authReq?.classList.remove('d-none');
            authOpt?.classList.add('d-none');
            authFile?.setAttribute('required', 'required');
        } else {
            // المسار الشامل: شريط واحد واضح من المنشأة حتى الإثبات.
            document.getElementById('mainStepper').classList.remove('d-none');
            window.goToGatewayStep(1);
            // في المسار الشامل، صفة المالك والهاتف تأتيان من نفس AddPlace.
            // إذا كانت الصفة محفوظة من إنشاء المنشأة نطابق شرط التفويض معها.
            const savedJobTitle = document.body.dataset.verificationOwnerJobTitle || '';
            if (savedJobTitle) {
                handleAuthRequirement(savedJobTitle);
            } else {
                authReq?.classList.add('d-none');
                authOpt?.classList.remove('d-none');
                authFile?.removeAttribute('required');
            }

            if (hasSavedVerificationPlace()) {
                showFullPlaceCheckpoint();
            } else if (typeof window.initAddPlace === 'function') {
                window.initAddPlace({ mode: 'verification' });
            }
        }
    };

    window.resetToGateway = function () {
        if (currentTrack === 'full' && hasSavedVerificationPlace()) {
            Swal.fire({
                icon: 'info',
                title: 'المنشأة محفوظة بالفعل',
                text: 'لا يمكن تحويل هذه الرحلة إلى التسجيل السريع بعد حفظ المنشأة. أكمل التأسيس الشامل.',
                confirmButtonColor: '#00A6DA'
            });
            return;
        }

        // الخروج من شاشة AddPlace المستقلة قبل العودة للبوابة.
        setStandalonePlaceStage(false);

        // إخفاء كل شيء
        document.getElementById('fastTrackForm').style.display = 'none';
        document.getElementById('step1_Full').style.display = 'none';
        if (policyStep) {
            policyStep.style.display = 'none';
            policyStep.classList.add('d-none');
        }
        document.getElementById('stepPolicyReview')?.style.setProperty('display', 'none');
        document.getElementById('stepPolicyReview')?.classList.add('d-none');
        document.getElementById('step2').style.display = 'none';
        document.getElementById('step3').style.display = 'none';
        document.getElementById('mainStepper').classList.add('d-none');

        // إظهار شاشة الاختيار
        const gateway = document.getElementById('step0_Gateway');
        gateway.style.display = 'block';
        gateway.classList.remove('d-none');
        setTimeout(() => gateway.classList.add('active-step'), 50);

        currentStep = 0;
        setCurrentTrack('');
        if (verificationCategoryInput) verificationCategoryInput.value = '';
        if (verificationPlaceInput) verificationPlaceInput.value = '';
        updateContractCategory(0);
        document.querySelectorAll('[data-fast-category]').forEach(card => card.classList.remove('is-selected'));
        const fastCategoryId = document.getElementById('FastCategoryId');
        if (fastCategoryId) fastCategoryId.value = '';
        document.getElementById('fastCategorySelected')?.setAttribute('hidden', 'hidden');
    };

    // =============================================================
    // 3. محرك التنقل وتحديث الـ Stepper
    // =============================================================

    window.goToGatewayStep = function (step) {
        currentStep = step;
        document.body.dataset.verificationStep = String(step);

        const screenIds = ['fastTrackForm', 'step1_Full', 'stepPolicies', 'stepPolicyReview', 'step2', 'step3'];
        screenIds.forEach(id => {
            const element = document.getElementById(id);
            if (!element) return;
            element.style.display = 'none';
            element.classList.add('d-none');
            element.classList.remove('active-step');
        });

        let targetId = '';

        if (currentTrack === 'full') {
            if (step === 1) targetId = 'step1_Full';
            else if (step === 2) targetId = 'stepPolicies';
            else if (step === 3) targetId = 'stepPolicyReview';
            else if (step === 4) targetId = 'step2';
            else if (step === 5) targetId = 'step3';
        } else {
            if (step === 1) targetId = 'fastTrackForm';
            else if (step === 2) targetId = 'step2';
            else if (step === 3) targetId = 'step3';
        }

        const isStandaloneFullPlaceStage =
            currentTrack === 'full' &&
            step === 1 &&
            !hasSavedVerificationPlace();

        setStandalonePlaceStage(isStandaloneFullPlaceStage);

        const targetEl = document.getElementById(targetId);
        if (targetEl) {
            targetEl.classList.remove('d-none');
            targetEl.style.display = 'block';

            window.setTimeout(() => {
                targetEl.classList.add('active-step');
            }, 40);

            window.setTimeout(() => {
                if (isStandaloneFullPlaceStage) {
                    window.scrollTo({ top: 0, behavior: 'auto' });
                } else {
                    document.getElementById('verificationWorkspace')
                        ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            }, 70);
        }

        if (currentTrack && !isStandaloneFullPlaceStage) {
            document.getElementById('mainStepper')?.classList.remove('d-none');
        } else {
            document.getElementById('mainStepper')?.classList.add('d-none');
        }

        updateStepper(step);

        if (currentTrack === 'full' && step === 3) {
            void refreshPolicyReviewSummary();
        }
    };

    function updateStepper(step) {
        const totalSteps = currentTrack === 'full' ? 5 : 3;
        const progress = totalSteps <= 1
            ? 0
            : ((step - 1) / (totalSteps - 1)) * 100;

        if (progressBar) progressBar.style.width = `${Math.max(0, Math.min(100, progress))}%`;

        document.querySelectorAll('#mainStepper .step-item').forEach((el, index) => {
            const stepNum = index + 1;
            const hiddenForFast = currentTrack !== 'full' && stepNum > 3;

            el.classList.toggle('d-none', hiddenForFast);
            if (hiddenForFast) return;

            const iconWrap = el.querySelector('.step-icon');
            const labelWrap = el.querySelector('.step-label');

            el.classList.remove('active', 'completed');

            if (iconWrap) {
                iconWrap.style.background = '';
                iconWrap.style.borderColor = '';
                iconWrap.style.boxShadow = '';
            }

            if (stepNum < step) {
                el.classList.add('completed');
                if (iconWrap) {
                    iconWrap.innerHTML = '<i class="bi bi-check-lg text-white"></i>';
                    iconWrap.style.background = '#198754';
                    iconWrap.style.borderColor = '#198754';
                    iconWrap.style.boxShadow = '0 5px 15px rgba(25, 135, 84, 0.3)';
                }
                if (labelWrap) {
                    labelWrap.className = 'step-label fw-bold mt-2 d-block small text-success';
                }
            } else if (stepNum === step) {
                el.classList.add('active');
                if (iconWrap) {
                    restoreStepIcon(iconWrap, stepNum);
                    iconWrap.style.boxShadow = '0 8px 20px rgba(0, 166, 218, 0.4)';
                }
                if (labelWrap) {
                    labelWrap.className = 'step-label fw-bold mt-2 d-block small text-primary-dark';
                }
            } else {
                if (iconWrap) {
                    restoreStepIcon(iconWrap, stepNum);
                }
                if (labelWrap) {
                    labelWrap.className = 'step-label fw-bold mt-2 d-block small text-muted';
                }
            }
        });
    }

    function restoreStepIcon(iconWrap, stepNum) {
        const fullIcons = {
            1: 'bi-building-fill-add',
            2: 'bi-sliders2',
            3: 'bi-clipboard2-check-fill',
            4: 'bi-file-earmark-ruled-fill',
            5: 'bi-shield-check'
        };
        const fastIcons = {
            1: 'bi-person-vcard-fill',
            2: 'bi-file-earmark-ruled-fill',
            3: 'bi-shield-check'
        };

        const iconClass = (currentTrack === 'full' ? fullIcons : fastIcons)[stepNum] || 'bi-circle';
        iconWrap.innerHTML = `<i class="bi ${iconClass}"></i>`;
    }

    // =============================================================
    // 🌟 4. تهيئة مكتبة الهاتف العالمية والتحقق اللحظي (intl-tel-input)
    // =============================================================
    const phoneInputField = document.getElementById('AdminPhoneDisplay');
    const phoneHiddenField = document.getElementById('AdminPhoneFast');
    const errorMsg = document.getElementById('phone-error-msg');
    const validMsg = document.getElementById('phone-valid-msg');
    let iti;

    if (phoneInputField) {
        iti = window.intlTelInput(phoneInputField, {
            initialCountry: "ye",
            preferredCountries: ["ye", "sa", "ae", "eg", "qa"],
            separateDialCode: true,
            formatOnDisplay: true,
            autoPlaceholder: "polite",
            utilsScript: "https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js"
        });

        const resetPhoneUI = () => {
            phoneInputField.classList.remove('glow-error', 'glow-success');
            errorMsg.innerHTML = "";
            errorMsg.classList.add("d-none");
            validMsg.classList.add("d-none");
        };

        const selectedPhoneCountry = () =>
            String(iti.getSelectedCountryData?.()?.iso2 || '').toLowerCase();

        const normalizePhoneInput = () => {
            let digits = String(phoneInputField.value || '').replace(/\D/g, '');
            const isYemen = selectedPhoneCountry() === 'ye';

            // اليمن: الرقم المحلي 9 أرقام فقط. لا يمكن كتابة أو لصق أكثر من ذلك.
            if (isYemen) {
                digits = digits.slice(0, 9);
                phoneInputField.maxLength = 9;
                phoneInputField.setAttribute('inputmode', 'numeric');
            } else {
                // بقية الدول تبقى حسب تحقق intl-tel-input.
                phoneInputField.maxLength = 15;
                phoneInputField.setAttribute('inputmode', 'tel');
            }

            if (phoneInputField.value !== digits) {
                phoneInputField.value = digits;
            }

            return { digits, isYemen };
        };

        const validatePhoneInput = () => {
            resetPhoneUI();
            const { digits, isYemen } = normalizePhoneInput();

            if (!digits) {
                phoneHiddenField.value = "";
                return;
            }

            if (isYemen && digits.length !== 9) {
                phoneInputField.classList.add('glow-error');
                errorMsg.innerHTML = '<i class="bi bi-exclamation-triangle-fill"></i> رقم الهاتف اليمني يجب أن يتكون من 9 أرقام.';
                errorMsg.classList.remove("d-none");
                phoneHiddenField.value = "";
                return;
            }

            if (iti.isValidNumber()) {
                phoneInputField.classList.add('glow-success');
                validMsg.classList.remove("d-none");
                phoneHiddenField.value = iti.getNumber();
            } else {
                phoneInputField.classList.add('glow-error');
                errorMsg.innerHTML = '<i class="bi bi-exclamation-triangle-fill"></i> يرجى إدخال رقم هاتف صحيح يطابق الدولة.';
                errorMsg.classList.remove("d-none");
                phoneHiddenField.value = "";
            }
        };

        // التحقق عند الكتابة واللصق وتغيير الدولة
        phoneInputField.addEventListener('input', validatePhoneInput);
        phoneInputField.addEventListener('paste', function () {
            window.setTimeout(validatePhoneInput, 0);
        });
        phoneInputField.addEventListener('countrychange', function () {
            window.setTimeout(validatePhoneInput, 0);
        });

        // منع الحروف والرموز من البداية.
        phoneInputField.addEventListener('keypress', function (e) {
            if (!/[0-9]/.test(e.key)) {
                e.preventDefault();
            }
        });

        // طبق حد اليمن فور فتح الصفحة لأن اليمن هي الدولة الافتراضية.
        normalizePhoneInput();
    }

    // =============================================================
    // 5. اختيار الفئة في التسجيل السريع + تحديث العقد ديناميكيًا
    // =============================================================
    const fastCategoryInput = document.getElementById('FastCategoryId');
    const fastCategoryError = document.getElementById('fastCategoryError');
    const fastCategorySelected = document.getElementById('fastCategorySelected');
    const fastCommissionPreview = document.getElementById('fastCommissionPreview');

    document.querySelectorAll('[data-fast-category]').forEach(card => {
        card.addEventListener('click', function () {
            const categoryId = Number.parseInt(this.dataset.categoryId || '0', 10);
            if (!Number.isInteger(categoryId) || categoryId <= 0) return;

            document.querySelectorAll('[data-fast-category]').forEach(item => {
                item.classList.toggle('is-selected', item === this);
                item.setAttribute('aria-pressed', item === this ? 'true' : 'false');
            });

            if (fastCategoryInput) fastCategoryInput.value = String(categoryId);
            if (verificationCategoryInput) verificationCategoryInput.value = String(categoryId);
            if (fastCategoryError) fastCategoryError.textContent = '';

            const meta = categoryMeta.get(categoryId);
            if (fastCommissionPreview && meta) fastCommissionPreview.textContent = formatCommission(meta.commission);
            if (fastCategorySelected) fastCategorySelected.removeAttribute('hidden');

            updateContractCategory(categoryId);
        });
    });

    // =============================================================
    // 6. المسار الشامل - نفس AddPlace المستخدم داخل لوحة المالك
    // =============================================================
    // لا يوجد هنا أي Wizard بديل أو منطق مكرر. ملف add-place.js هو المصدر
    // الوحيد لخطوات المنشأة والتحقق والحفظ. هذه الصفحة فقط تستقبل نتيجة الحفظ.
    if (typeof window.initAddPlace === 'function') {
        window.initAddPlace({ mode: 'verification' });
    }

    window.addEventListener('yemenGuide:verificationPlaceCreated', function (event) {
        const detail = event.detail || {};

        existingVerificationBookingSystemId =
            Number.parseInt(detail.bookingSystemId || '0', 10) || 0;
        verificationPoliciesReady = false;
        document.body.dataset.verificationOwnerJobTitle =
            detail.jobTitle || document.body.dataset.verificationOwnerJobTitle || '';

        if (document.body.dataset.verificationOwnerJobTitle) {
            handleAuthRequirement(document.body.dataset.verificationOwnerJobTitle);
        }

        showFullPlaceCheckpoint(detail);
        setVerificationPoliciesReady(false);

        const nextStep = requiresFacilityPolicies() ? 2 : 4;
        const nextText = requiresFacilityPolicies()
            ? 'الآن اختر طريقة إعداد سياسات المنشأة.'
            : 'هذه الفئة لا تحتاج سياسات حجز، وسننتقل مباشرة إلى العقد.';

        Swal.fire({
            icon: 'success',
            title: 'تم حفظ المنشأة بنجاح',
            html: `
                <div dir="rtl" class="text-center">
                    <p class="mb-2">تم حفظ <strong>${detail.placeName || 'المنشأة الجديدة'}</strong> بنفس خطوات إدارة المنشآت.</p>
                    <p class="text-muted small mb-0">${nextText}</p>
                </div>`,
            confirmButtonText: requiresFacilityPolicies() ? 'إعداد السياسات' : 'عرض العقد',
            confirmButtonColor: '#00A6DA',
            allowOutsideClick: false
        }).then(() => {
            window.goToGatewayStep(nextStep);
        });
    });

    document.getElementById('fullPlaceContinueButton')?.addEventListener('click', function () {
        if (!hasSavedVerificationPlace()) return;

        if (requiresFacilityPolicies()) {
            window.goToGatewayStep(verificationPoliciesReady ? 3 : 2);
            return;
        }

        window.goToGatewayStep(4);
    });

    document.getElementById('policyBackToPlace')?.addEventListener('click', () => {
        window.goToGatewayStep(1);
        showFullPlaceCheckpoint();
    });

    document.getElementById('continueAfterPolicies')?.addEventListener('click', async () => {
        if (verificationPoliciesReady) {
            await refreshPolicyReviewSummary();
            window.goToGatewayStep(3);
        }
    });

    document.getElementById('policyReviewBackToPolicies')?.addEventListener('click', () => {
        window.goToGatewayStep(2);
    });

    document.getElementById('policyReviewContinue')?.addEventListener('click', () => {
        const grid = document.getElementById('policyReviewSummaryGrid');
        if (grid?.dataset.ready !== 'true') {
            Swal.fire({
                icon: 'info',
                title: 'انتظر اكتمال المراجعة',
                text: 'يجب تحميل السياسة المحفوظة من النظام قبل الانتقال إلى العقد.',
                confirmButtonColor: '#00A6DA'
            });
            return;
        }
        window.goToGatewayStep(4);
    });

    document.getElementById('reviewSavedPolicies')?.addEventListener(
        'click',
        openManualPolicyEditor
    );

    document.getElementById('useDefaultPolicies')?.addEventListener(
        'click',
        applyDefaultVerificationPolicies
    );

    document.getElementById('useManualPolicies')?.addEventListener(
        'click',
        openManualPolicyEditor
    );

    document.getElementById('backToPolicyChoice')?.addEventListener('click', () => {
        policyManualShell?.classList.add('d-none');
        policyFrame?.removeAttribute('src');

        if (verificationPoliciesReady) {
            policyReadyPanel?.classList.remove('d-none');
        } else {
            policyChoicePanel?.classList.remove('d-none');
        }
    });

    policyFrame?.addEventListener('load', () => {
        policyFrameLoading?.classList.add('d-none');
        if (!policyFrame.style.height) policyFrame.style.height = '520px';
    });

    window.addEventListener('message', async function (event) {
        if (event.origin !== window.location.origin) return;

        const data = event.data || {};
        if (data.source !== 'YemenGuideFacilityPolicies') return;

        if (data.type === 'height' && policyFrame) {
            const height = Math.max(360, Math.min(8000, Number(data.height) || 520));
            policyFrame.style.height = `${height}px`;
            policyFrameLoading?.classList.add('d-none');
            return;
        }

        if (data.type === 'saved') {
            const savedPlaceId = Number.parseInt(data.placeId || '0', 10);
            if (savedPlaceId !== existingVerificationPlaceId) return;

            setVerificationPoliciesReady(true, data.mode || 'manual');
            invalidateContractAgreement();
            await refreshPolicyReviewSummary('manual');

            Swal.fire({
                icon: 'success',
                title: 'تم حفظ السياسات',
                text: 'تم حفظ السياسة. راجع القيم المحفوظة أولًا، وبعدها تستطيع الانتقال إلى العقد.',
                confirmButtonText: 'مراجعة السياسات',
                confirmButtonColor: '#00A6DA',
                allowOutsideClick: false
            }).then(() => {
                window.goToGatewayStep(3);
            });
        }
    });

    // =============================================================
    // 7. أزرار الانتقال بين الخطوات الأساسية للتوثيق والتحقق من الهاتف
    // =============================================================

    document.querySelector('.btn-next-fast')?.addEventListener('click', function () {
        const fastForm = document.getElementById('fastTrackForm');

        // التأكد من تعبئة الحقول الأساسية
        if (!fastForm.checkValidity()) {
            fastForm.reportValidity();
            return;
        }

        const selectedCategoryId = Number.parseInt(fastCategoryInput?.value || '0', 10);
        if (!Number.isInteger(selectedCategoryId) || selectedCategoryId <= 0) {
            if (fastCategoryError) fastCategoryError.textContent = 'اختر فئة المنشأة قبل عرض العقد.';
            document.getElementById('fastCategoryGrid')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
            return;
        }

        updateContractCategory(selectedCategoryId);

        // التأكد من صحة رقم الهاتف قبل السماح بالانتقال
        if (currentTrack === 'fast' && iti) {
            const selectedCountry = String(iti.getSelectedCountryData?.()?.iso2 || '').toLowerCase();
            const localDigits = String(phoneInputField?.value || '').replace(/\D/g, '');

            if (selectedCountry === 'ye' && localDigits.length !== 9) {
                phoneInputField.classList.add('glow-error');
                Swal.fire({
                    icon: 'warning',
                    title: 'رقم الهاتف اليمني غير مكتمل',
                    text: 'رقم الهاتف اليمني يجب أن يكون 9 أرقام فقط.',
                    confirmButtonColor: '#05203C'
                });
                return;
            }

            if (!iti.isValidNumber()) {
                phoneInputField.classList.add('glow-error');
                Swal.fire({
                    icon: 'warning',
                    title: 'رقم الهاتف غير صالح',
                    text: 'عذراً، يرجى التأكد من إدخال رقم هاتف صحيح يطابق الدولة المختارة قبل المتابعة.',
                    confirmButtonColor: '#05203C'
                });
                return;
            }
            // سحب الرقم الدولي ووضعه في الحقل المخفي للنموذج
            phoneHiddenField.value = iti.getNumber();
        }

        // إذا كان كل شيء سليماً، انتقل للخطوة الثانية (العقد)
        window.goToGatewayStep(2);
    });

    document.querySelector('.btn-prev-to-1')?.addEventListener('click', () => {
        if (currentTrack === 'full') {
            if (requiresFacilityPolicies()) {
                window.goToGatewayStep(3);
            } else {
                window.goToGatewayStep(1);
                showFullPlaceCheckpoint();
            }
        } else {
            window.goToGatewayStep(1);
        }
    });

    document.querySelector('.btn-prev-to-2')?.addEventListener('click', () => {
        window.goToGatewayStep(currentTrack === 'full' ? 4 : 2);
    });

    document.querySelector('.btn-next-to-3')?.addEventListener('click', function () {
        if (!document.getElementById('termsCheckbox').checked) {
            Swal.fire({
                icon: 'warning',
                title: 'تنبيه',
                text: 'يجب الموافقة على اتفاقية الشراكة والوساطة أولاً.',
                confirmButtonColor: '#00A6DA'
            });
            return;
        }

        if (currentTrack === 'full' &&
            requiresFacilityPolicies() &&
            !verificationPoliciesReady) {
            Swal.fire({
                icon: 'warning',
                title: 'السياسات لم تكتمل',
                text: 'اختر السياسات الافتراضية أو احفظ السياسات اليدوية قبل توقيع العقد.',
                confirmButtonColor: '#00A6DA'
            }).then(() => window.goToGatewayStep(2));
            return;
        }

        window.goToGatewayStep(currentTrack === 'full' ? 5 : 3);
    });

    document.getElementById('btnRejectTerms')?.addEventListener('click', function () {
        Swal.fire({
            title: 'هل أنت متأكد؟',
            text: 'رفض الاتفاقية يعني إلغاء عملية التوثيق ولن يتم تفعيل لوحة التحكم الخاصة بك.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#dc3545', cancelButtonColor: '#6c757d',
            confirmButtonText: 'نعم، رفض وخروج', cancelButtonText: 'تراجع'
        }).then((res) => { if (res.isConfirmed) window.location.href = '/Account/Logout'; });
    });

    // =============================================================
    // 8. منطق الوظيفة (أخرى) والتفويض الإجباري (خاص بالمسار السريع)
    // =============================================================
    function handleAuthRequirement(val) {
        const authReq = document.getElementById('authRequiredStar');
        const authOpt = document.getElementById('authOptionalSpan');
        const authFile = document.getElementById('authorizationFile');
        if (!authFile) return;

        const normalizedJobTitle = (val || '').trim();
        const isActualOwner =
            normalizedJobTitle === 'المالك الفعلي' ||
            normalizedJobTitle === 'صاحب المنشأة';

        if (isActualOwner) {
            authReq?.classList.add('d-none');
            authOpt?.classList.remove('d-none');
            authFile.removeAttribute('required');
        } else {
            authReq?.classList.remove('d-none');
            authOpt?.classList.add('d-none');
            authFile.setAttribute('required', 'required');
        }
    }

    const jobSelectFast = document.getElementById('JobTitleSelectFast');
    const jobOtherContainerFast = document.getElementById('JobTitleOtherContainerFast');
    const jobOtherInputFast = document.getElementById('JobTitleOtherFast');

    jobSelectFast?.addEventListener('change', function () {
        if (this.value === 'أخرى') {
            jobOtherContainerFast.classList.remove('d-none');
            jobOtherInputFast.setAttribute('required', 'required');
            jobOtherInputFast.setAttribute('name', 'JobTitleOther');
        } else {
            jobOtherContainerFast.classList.add('d-none');
            jobOtherInputFast.removeAttribute('required');
            jobOtherInputFast.removeAttribute('name');
        }
        handleAuthRequirement(this.value);
    });

    // =============================================================
    // 9. نظام السحب والإفلات الأنيق (Dropzone)
    // =============================================================
    function setupDropzone(dropzoneId, inputId) {
        const dropzone = document.getElementById(dropzoneId);
        const input = document.getElementById(inputId);
        if (!dropzone || !input) return;

        const preview = dropzone.querySelector('.file-name-preview');
        const icon = dropzone.querySelector('.zone-icon');

        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(ev => dropzone.addEventListener(ev, e => { e.preventDefault(); e.stopPropagation(); }));
        ['dragenter', 'dragover'].forEach(ev => dropzone.addEventListener(ev, () => dropzone.classList.add('dragover')));
        ['dragleave', 'drop'].forEach(ev => dropzone.addEventListener(ev, () => dropzone.classList.remove('dragover')));

        dropzone.addEventListener('drop', function (e) {
            if (e.dataTransfer.files.length > 0) { input.files = e.dataTransfer.files; updatePreview(input.files[0].name); }
        });

        input.addEventListener('change', function () {
            if (this.files.length > 0) updatePreview(this.files[0].name);
            else {
                preview.textContent = '';
                dropzone.style.borderColor = ''; dropzone.style.background = '';
                icon.classList.replace('bi-check-circle-fill', 'bi-cloud-arrow-up');
                icon.classList.remove('text-success', 'text-muted');
            }
        });

        function updatePreview(fileName) {
            preview.innerHTML = `<i class="bi bi-file-earmark-check me-1"></i> تم اختيار: ${fileName}`;
            dropzone.style.borderColor = '#198754'; dropzone.style.background = 'rgba(25, 135, 84, 0.05)';
            icon.classList.replace('bi-cloud-arrow-up', 'bi-check-circle-fill');
            icon.classList.add('text-success');
        }
    }
    setupDropzone('dropzoneCommercial', 'commercialFile');
    setupDropzone('dropzoneIdFront', 'idFrontFile');
    setupDropzone('dropzoneIdBack', 'idBackFile');
    setupDropzone('dropzoneAuthorization', 'authorizationFile');

    document.querySelectorAll('[data-fast-category]').forEach(card => card.setAttribute('aria-pressed', 'false'));
    if (existingVerificationCategoryId > 0) {
        updateContractCategory(existingVerificationCategoryId);
    } else {
        updateContractCategory(0);
    }
    if (existingVerificationPlaceId > 0) {
        showFullPlaceCheckpoint();
        setVerificationPoliciesReady(verificationPoliciesReady);
    }

    // =============================================================
    // القادم من زر "سجل منشأتك الآن" في لوحة العميل:
    // افتح المسار الذي اختاره مباشرة ولا تعرض بوابة الاختيار مرة ثانية.
    // تسجيل المالك العادي بدون ?track= يبقى كما هو تماماً.
    // =============================================================
    const requestedTrack =
        new URLSearchParams(window.location.search).get('track');

    if (requestedTrack === 'fast' &&
        !hasSavedVerificationPlace()) {
        window.selectTrack('fast');
    }
    else if (requestedTrack === 'full' &&
        hasSavedVerificationPlace()) {
        window.selectTrack('full');
    }

    // =============================================================
    // 10. الإرسال النهائي للوثائق وتجميع البيانات
    // =============================================================
    document.getElementById('verificationDocsForm').addEventListener('submit', async function (e) {
        e.preventDefault();

        // إذا استأنف المالك المسار الشامل بعد تسجيل الخروج وكانت المنشأة محفوظة
        // لكن هاتف المسؤول لم يُحفظ في النسخة القديمة، نطلب الهاتف فقط بدل إجباره
        // على إعادة إنشاء المنشأة من البداية.
        if (currentTrack === 'full' && !existingVerificationOwnerPhone) {
            const phoneResult = await Swal.fire({
                icon: 'info',
                title: 'أكمل رقم التواصل',
                html: 'منشأتك وسياساتها محفوظة بالفعل، لكن رقم هاتف مسؤول المنشأة يحتاج إلى استكمال مرة واحدة قبل إرسال التوثيق.',
                input: 'tel',
                inputPlaceholder: 'مثال: 77xxxxxxx أو +96777xxxxxxx',
                inputAttributes: {
                    dir: 'ltr',
                    autocomplete: 'tel',
                    inputmode: 'numeric',
                    maxlength: '13'
                },
                showCancelButton: true,
                confirmButtonText: 'حفظ ومتابعة',
                cancelButtonText: 'إلغاء',
                confirmButtonColor: '#00A6DA',
                preConfirm: (value) => {
                    const normalized = String(value || '')
                        .trim()
                        .replace(/[\s\-()]/g, '');

                    const isYemenInternational = /^\+?967/.test(normalized);
                    const yemenLocal = normalized.replace(/^\+?967/, '');

                    if (isYemenInternational && !/^[0-9]{9}$/.test(yemenLocal)) {
                        Swal.showValidationMessage('رقم الهاتف اليمني بعد +967 يجب أن يكون 9 أرقام فقط.');
                        return false;
                    }

                    if (!isYemenInternational && !/^[0-9]{9}$/.test(normalized)) {
                        Swal.showValidationMessage('أدخل رقم الهاتف اليمني من 9 أرقام فقط.');
                        return false;
                    }

                    return isYemenInternational ? `+967${yemenLocal}` : normalized;
                }
            });

            if (!phoneResult.isConfirmed || !phoneResult.value) return;

            existingVerificationOwnerPhone = phoneResult.value;
            document.body.dataset.verificationOwnerPhone = existingVerificationOwnerPhone;
        }

        const commercialFile = document.getElementById('commercialFile').files.length;
        const idFront = document.getElementById('idFrontFile').files.length;
        const idBack = document.getElementById('idBackFile').files.length;
        const authRequired = document.getElementById('authorizationFile').hasAttribute('required');
        const authFile = document.getElementById('authorizationFile').files.length;

        if (commercialFile === 0 || idFront === 0 || idBack === 0 || (authRequired && authFile === 0)) {
            Swal.fire({ icon: 'warning', title: 'بيانات ناقصة', text: 'يرجى التأكد من رفع جميع الوثائق الإجبارية لإتمام التوثيق.' });
            return;
        }

        const submitBtn = document.getElementById('btnSubmitFinal');
        const originalBtnHtml = submitBtn.innerHTML;
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> جاري التوثيق...';
        submitBtn.disabled = true;

        Swal.fire({
            title: 'جاري رفع واعتماد الوثائق...',
            html: 'يرجى الانتظار قليلاً وعدم إغلاق الصفحة.',
            allowOutsideClick: false,
            didOpen: () => { Swal.showLoading(); },
            customClass: { popup: 'rounded-4 border-0 shadow-lg' }
        });

        const finalFormData = new FormData(this);
        finalFormData.set('IsFastTrack', currentTrack === 'fast' ? 'true' : 'false');
        if (verificationCategoryInput?.value) finalFormData.set('CategoryId', verificationCategoryInput.value);
        if (verificationPlaceInput?.value) finalFormData.set('PlaceId', verificationPlaceInput.value);

        if (currentTrack === 'fast') {
            const fastData = new FormData(document.getElementById('fastTrackForm'));
            for (let [key, value] of fastData.entries()) finalFormData.set(key, value);
        } else {
            // في المسار الشامل تبقى بيانات الحساب هي المصدر الأساسي.
            // AdminPhone هنا يستخدم فقط كقيمة استكمال للحسابات القديمة التي
            // حفظت المنشأة قبل إصلاح مزامنة OwnerPhone.
            if (!hasSavedVerificationPlace()) {
                submitBtn.innerHTML = originalBtnHtml;
                submitBtn.disabled = false;
                Swal.close();
                Swal.fire({ icon: 'warning', title: 'المنشأة غير محفوظة', text: 'أكمل إنشاء المنشأة أولاً.' });
                return;
            }

            if (existingVerificationOwnerPhone) {
                finalFormData.set('AdminPhone', existingVerificationOwnerPhone);
            }
        }

        fetch('/Account/UploadVerification', {
            method: 'POST',
            body: finalFormData,
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    Swal.fire({
                        icon: 'success', title: 'تمت العملية بنجاح! 🎉', text: data.message, confirmButtonColor: '#198754',
                        customClass: { popup: 'rounded-4 border-0 shadow-lg' }
                    }).then(() => window.location.reload());
                } else {
                    submitBtn.innerHTML = originalBtnHtml; submitBtn.disabled = false;
                    Swal.fire({ icon: 'error', title: 'عذراً!', text: data.message, confirmButtonColor: '#05203C' });
                }
            })
            .catch(error => {
                submitBtn.innerHTML = originalBtnHtml; submitBtn.disabled = false;
                Swal.fire({ icon: 'error', title: 'خطأ في الاتصال!', text: 'تعذر الوصول للخادم، يرجى المحاولة لاحقاً.', confirmButtonColor: '#05203C' });
            });
    });
});
