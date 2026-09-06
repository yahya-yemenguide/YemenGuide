(() => {
    "use strict";

    const app = document.querySelector("[data-owner-ads]");
    if (!app) return;

    const $ = (selector, root = document) => root.querySelector(selector);
    const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));
    const token = $('input[name="__RequestVerificationToken"]', app)?.value || "";
    const state = {
        currentTab: "overview",
        currentGroup: "All",
        wizardStep: 1,
        wizardData: null,
        selectedImageFile: null,
        selectedVideoFile: null,
        existingMedia: null,
        currentDetails: null
    };

    bind();
    initializeOverviewBookingChart();

    function initializeOverviewBookingChart() {
        ensureOverviewBookingChart();
        renderOverviewBookingChart();
        void refreshOverviewBookingChartFromServer();
    }

    function ensureOverviewBookingChart() {
        const overviewGrid = $(".oad-overview-grid", app);
        if (!overviewGrid) return null;

        let card = $(".oad-performance-card", overviewGrid);
        if (card) return card;

        card = document.createElement("article");
        card.className = "oad-card oad-performance-card";
        card.innerHTML = `
            <header class="oad-card__head">
                <div>
                    <span class="oad-kicker">نتائج الحجز</span>
                    <h3>الحجوزات الناتجة عن الإعلان</h3>
                </div>
                <i class="bi bi-bar-chart-line-fill"></i>
            </header>

            <div class="oad-performance-chart" data-booking-performance-chart>
                <div class="oad-performance-row"
                     data-booking-performance-row
                     data-metric="starts"
                     data-value="0">
                    <div class="oad-performance-row__head">
                        <span>بدء الحجز من الإعلان</span>
                        <strong data-booking-performance-value="starts">0</strong>
                    </div>
                    <div class="oad-performance-track">
                        <span data-booking-performance-bar></span>
                    </div>
                </div>

                <div class="oad-performance-row"
                     data-booking-performance-row
                     data-metric="conversions"
                     data-value="0">
                    <div class="oad-performance-row__head">
                        <span>الحجوزات المكتملة من الإعلان</span>
                        <strong data-booking-performance-value="conversions">0</strong>
                    </div>
                    <div class="oad-performance-track">
                        <span data-booking-performance-bar></span>
                    </div>
                </div>
            </div>`;

        overviewGrid.appendChild(card);
        return card;
    }

    async function refreshOverviewBookingChartFromServer() {
        if (!app.dataset.overviewUrl) return;

        try {
            const response = await fetch(app.dataset.overviewUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                cache: "no-store"
            });

            if (!response.ok) return;

            const data = await response.json();

            ensureOverviewBookingChart();
            renderOverviewBookingChart({
                starts: data.bookingStartsCount,
                conversions: data.conversionsCount
            });
        } catch (_) {
            // يبقى الرسم ظاهراً بالقيم الموجودة أو بصفر عند تعذر التحديث.
        }
    }

    function bind() {
        // SweetAlert2 appends its popup under <body>, not inside [data-owner-ads].
        // Listen on document so wizard/details/payment controls actually work.
        document.addEventListener("click", onClick);
        document.addEventListener("pointerdown", onPointerDown);
        document.addEventListener("submit", onSubmit);
        document.addEventListener("change", onChange);
        document.addEventListener("input", onInput);
    }

    function onPointerDown(event) {
        const durationInput = event.target.closest('input[name="DurationDays"]');
        if (!durationInput || !durationInput.closest("[data-wizard-form]")) return;

        // الإعلان الجديد يظهر افتراضيًا بـ 1.
        // أول ضغطة فعلية على الحقل فقط تمسح هذا الرقم حتى يكتب المالك العدد الجديد مباشرة.
        // في التعديل، أو بعد أول ضغطة، لا نمسح أي قيمة كتبها المستخدم.
        if (durationInput.dataset.clearDefaultOnFirstClick !== "true") return;

        durationInput.dataset.clearDefaultOnFirstClick = "false";

        if (String(durationInput.value).trim() === "1") {
            durationInput.value = "";
            durationInput.dispatchEvent(new Event("input", { bubbles: true }));
        }
    }

    function onClick(event) {

        const tab = event.target.closest("[data-main-tab]");
        if (tab) {
            switchTab(tab.dataset.mainTab);
            return;
        }

        const group = event.target.closest("[data-group]");
        if (group) {
            state.currentGroup = group.dataset.group || "All";
            $$("[data-group]", app).forEach(x => x.classList.toggle("active", x.dataset.group === state.currentGroup));

            const form = $("[data-filter-form]", app);
            if (form) {
                if (form.elements.Group) form.elements.Group.value = state.currentGroup;
                if (form.elements.Page) form.elements.Page.value = "1";
                loadAds(form);
            }
            return;
        }

        const futureTrigger = event.target.closest("[data-future-datetime-trigger]");
        if (futureTrigger) {
            openFutureDateTimePicker(futureTrigger.closest(".oad-swal-builder"));
            return;
        }

        const futureNav = event.target.closest("[data-future-nav]");
        if (futureNav) {
            const panel = futureNav.closest("[data-future-datetime-picker]");
            if (panel) shiftFuturePickerMonth(panel, Number(futureNav.dataset.futureNav || 0));
            return;
        }

        const futureDay = event.target.closest("[data-future-day]");
        if (futureDay && !futureDay.disabled) {
            const panel = futureDay.closest("[data-future-datetime-picker]");
            if (panel) {
                panel.dataset.selectedDate = futureDay.dataset.futureDay || "";
                clearFuturePickerError(panel);
                renderFutureDateTimePicker(panel);
            }
            return;
        }

        if (event.target.closest("[data-future-picker-cancel]")) {
            const panel = event.target.closest("[data-future-datetime-picker]");
            if (panel) panel.hidden = true;
            return;
        }

        if (event.target.closest("[data-future-picker-apply]")) {
            const panel = event.target.closest("[data-future-datetime-picker]");
            if (panel) applyFutureDateTimePicker(panel);
            return;
        }

        const page = event.target.closest("[data-page]");
        if (page && !page.disabled) {
            const form = $("[data-filter-form]", app);
            form.elements.Page.value = page.dataset.page;
            loadAds(form);
            return;
        }

        const create = event.target.closest("[data-open-create]");
        if (create) {
            openWizard();
            return;
        }

        const details = event.target.closest("[data-details-id]");
        if (details) {
            openDetails(Number(details.dataset.detailsId));
            return;
        }

        const planCard = event.target.closest("[data-plan]");
        if (planCard && planCard.closest(".oad-swal-builder")) {
            const radio = planCard.querySelector('[name="AdvertisementPlanId"]');
            if (radio && !radio.disabled) {
                radio.checked = true;
                syncPlanUI(planCard.closest(".oad-swal-builder"));
                quotePrice(planCard.closest(".oad-swal-builder"));
            }
            return;
        }

        const step = event.target.closest("[data-step-go]");
        if (step) {
            goWizardStep(Number(step.dataset.stepGo));
            return;
        }

        if (event.target.closest("[data-prev]")) {
            goWizardStep(Math.max(1, state.wizardStep - 1));
            return;
        }

        if (event.target.closest("[data-next]")) {
            if (validateStep(state.wizardStep)) goWizardStep(Math.min(4, state.wizardStep + 1));
            return;
        }

        if (event.target.closest("[data-save-submit]")) {
            saveAndSubmitWizard();
            return;
        }

        const planToggle = event.target.closest("[data-plan-toggle]");
        if (planToggle) {
            toggleMorePlans(planToggle);
            return;
        }

        const clearImage = event.target.closest("[data-clear-image]");
        if (clearImage) {
            clearSelectedMedia("image", clearImage.closest(".oad-swal-builder"));
            return;
        }

        const clearVideo = event.target.closest("[data-clear-video]");
        if (clearVideo) {
            clearSelectedMedia("video", clearVideo.closest(".oad-swal-builder"));
            return;
        }

        const paymentReview = event.target.closest("[data-payment-review]");
        if (paymentReview) {
            event.preventDefault();
            submitPayment(paymentReview.closest("[data-payment-form]"));
            return;
        }

        if (event.target.closest("[data-wizard-confirm-back]")) {
            hideWizardConfirmation();
            return;
        }

        const wizardConfirmPay = event.target.closest("[data-wizard-confirm-pay]");
        if (wizardConfirmPay) {
            confirmAndContinueToPayment(wizardConfirmPay);
            return;
        }

        if (event.target.closest("[data-payment-confirm-back]")) {
            hidePaymentConfirmation(event.target.closest("[data-payment-form]"));
            return;
        }

        const paymentConfirmSend = event.target.closest("[data-payment-confirm-send]");
        if (paymentConfirmSend) {
            sendPayment(event.target.closest("[data-payment-form]"));
            return;
        }

        const action = event.target.closest("[data-owner-action]");
        if (action) {
            handleOwnerAction(action);
            return;
        }
    }

    function onSubmit(event) {
        const filter = event.target.closest("[data-filter-form]");
        if (filter) {
            event.preventDefault();
            if (filter.elements.Group) filter.elements.Group.value = state.currentGroup || "All";
            if (filter.elements.Page) filter.elements.Page.value = "1";
            loadAds(filter);
            return;
        }

        const payment = event.target.closest("[data-payment-form]");
        if (payment) {
            event.preventDefault();
            submitPayment(payment);
        }
    }

    function onChange(event) {
        if (event.target.matches('[name="AdvertisementPlanId"]')) {
            const root = event.target.closest(".oad-swal-builder");
            clearStep1Error(root);
            syncPlanUI(root);
            quotePrice(root);
        }
        if (event.target.matches('[name="StartImmediately"]')) {
            const root = event.target.closest(".oad-swal-builder");
            clearStep1Error(root);
            syncStartUI(root);

            // بمجرد اختيار «تاريخ مستقبلي» نفتح تقويمنا الإنجليزي مباشرة.
            if (event.target.value === "false") {
                openFutureDateTimePicker(root);
            }
        }
        if (event.target.matches("[data-image-file]")) {
            const root = event.target.closest(".oad-swal-builder");
            const file = event.target.files?.[0] || null;

            if (!file) {
                state.selectedImageFile = null;
                updateMediaChoiceUI(root);
                return;
            }

            if (!file.type.startsWith("image/") || file.size > 10 * 1024 * 1024) {
                event.target.value = "";
                state.selectedImageFile = null;
                showWizardError(root, "اختر صورة JPG أو PNG أو WebP بحجم لا يتجاوز 10MB.", event.target);
                updateMediaChoiceUI(root);
                return;
            }

            if (state.selectedVideoFile) {
                event.target.value = "";
                showWizardError(
                    root,
                    "لديك فيديو محدد بالفعل. اضغط «إلغاء تحديد الفيديو» أولًا ثم اختر الصورة.",
                    event.target
                );
                updateMediaChoiceUI(root);
                return;
            }

            state.selectedImageFile = file;

            clearWizardError(root);
            renderPreviewFile(file, root);
            updateMediaChoiceUI(root);
        }

        if (event.target.matches("[data-video-file]")) {
            const root = event.target.closest(".oad-swal-builder");
            const file = event.target.files?.[0] || null;

            if (!file) {
                state.selectedVideoFile = null;
                updateMediaChoiceUI(root);
                return;
            }

            if (!file.type.startsWith("video/") || file.size > 25 * 1024 * 1024) {
                event.target.value = "";
                state.selectedVideoFile = null;
                showWizardError(root, "اختر فيديو MP4 أو WebM بحجم لا يتجاوز 25MB.", event.target);
                updateMediaChoiceUI(root);
                return;
            }

            if (state.selectedImageFile) {
                event.target.value = "";
                showWizardError(
                    root,
                    "لديك صورة محددة بالفعل. اضغط «إلغاء تحديد الصورة» أولًا ثم اختر الفيديو.",
                    event.target
                );
                updateMediaChoiceUI(root);
                return;
            }

            state.selectedVideoFile = file;

            clearWizardError(root);
            renderPreviewFile(file, root);
            updateMediaChoiceUI(root);
        }
        if (event.target.matches("[data-receipt-file]")) {
            const file = event.target.files?.[0];
            const shell = event.target.closest(".oad-swal-payclean");
            const label = shell?.querySelector("[data-receipt-name]");
            if (label) {
                label.textContent = file ? file.name : "اضغط لاختيار JPG أو PNG أو WebP — حتى 8MB";
            }
            clearPaymentError(event.target.closest("[data-payment-form]"));
        }
        if (event.target.matches("#oadRefundOwnerWallet")) {
            syncRefundWalletDetails(event.target);
        }

        if (event.target.matches("[data-future-hour], [data-future-minute]")) {
            const panel = event.target.closest("[data-future-datetime-picker]");
            if (panel) {
                if (event.target.matches("[data-future-hour]")) panel.dataset.hour = event.target.value;
                if (event.target.matches("[data-future-minute]")) panel.dataset.minute = event.target.value;
                clearFuturePickerError(panel);
            }
        }

        if (event.target.closest("[data-wizard-form]") &&
            event.target.matches('[name="DurationDays"]')) {
            quotePrice(event.target.closest(".oad-swal-builder"));
        }
    }

    function onInput(event) {
        const wizardForm = event.target.closest("[data-wizard-form]");
        const paymentForm = event.target.closest("[data-payment-form]");

        if (paymentForm) {
            clearPaymentError(paymentForm);

            if (event.target.matches('[name="ReferenceNumber"]')) {
                event.target.value = toEnglishDigits(event.target.value)
                    .replace(/\D+/g, "");
            }

            if (event.target.matches('[name="TransferredAmount"]')) {
                const raw = toEnglishDigits(event.target.value)
                    .replace(/[^\d.]/g, "");
                const firstDot = raw.indexOf(".");
                event.target.value = firstDot === -1
                    ? raw
                    : raw.slice(0, firstDot + 1) + raw.slice(firstDot + 1).replace(/\./g, "");
            }
            return;
        }

        if (!wizardForm) return;

        const root = event.target.closest(".oad-swal-builder");
        clearWizardError(root);

        if (event.target.matches('[name="AdvertisementTitle"],[name="ShortDescription"],[name="ButtonText"]')) {
            updatePreviewText(root);
        }

        if (event.target.matches('[name="DurationDays"]')) {
            event.target.value = String(event.target.value || "").replace(/[^\d]/g, "");
            const valid = validateDuration(root, false);
            updateNextState(root);
            updateStep1Summary(root);
            if (valid) quotePrice(root);
        }
    }

    function switchTab(name) {
        state.currentTab = name === "ads" ? "ads" : "overview";
        $$("[data-main-tab]", app).forEach(btn => btn.classList.toggle("active", btn.dataset.mainTab === state.currentTab));
        $$("[data-panel]", app).forEach(panel => {
            const active = panel.dataset.panel === state.currentTab;
            panel.hidden = !active;
            panel.classList.toggle("active", active);
        });
    }

    async function loadAds(form) {
        // IMPORTANT: serialize BEFORE disabling controls.
        // Disabled inputs/selects are excluded from FormData.
        const formData = new FormData(form);
        formData.set("Group", state.currentGroup || form.elements.Group?.value || "All");
        formData.set("Page", form.elements.Page?.value || "1");

        const params = new URLSearchParams();
        for (const [key, value] of formData.entries()) {
            if (value !== null && String(value).trim() !== "") {
                params.append(key, String(value));
            }
        }

        try {
            busy(form, true);
            const response = await fetch(`${app.dataset.adsUrl}?${params.toString()}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                cache: "no-store"
            });
            if (!response.ok) throw new Error("تعذر تحميل الإعلانات.");

            const data = await response.json();
            renderAds(data);
        } catch (error) {
            toast(error.message || "تعذر تحميل الإعلانات.", "error");
        } finally {
            busy(form, false);
        }
    }

    function renderAds(data) {
        const grid = $("[data-ad-grid]", app);
        if (!grid) return;

        grid.innerHTML = (data.items || []).map(item => {
            const media = item.primaryMediaPath
                ? `<img src="${attr(item.primaryMediaPath)}" alt="${attr(item.advertisementTitle)}" loading="lazy">`
                : `<span><i class="bi bi-image"></i></span>`;
            const alert = item.attentionText
                ? `<div class="oad-card-alert"><i class="bi bi-exclamation-circle"></i><span>${esc(item.attentionText)}</span></div>`
                : "";
            return `
                <article class="oad-ad-card" data-ad-card="${item.advertisementId}">
                    <div class="oad-ad-card__media">
                        ${media}
                        <em class="is-${slug(item.status)}">${esc(item.statusLabel)}</em>
                    </div>
                    <div class="oad-ad-card__content">
                        <div class="oad-ad-card__title">
                            <div><small>#${item.advertisementId}</small><h3>${esc(item.advertisementTitle)}</h3></div>
                            <div class="oad-ad-card__badges">
                                <span class="oad-payment is-${slug(item.paymentStatus)}">${esc(item.paymentStatusLabel)}</span>
                                ${item.refundStatusLabel ? `<span class="oad-refund-badge is-${item.refundedAt ? "done" : "pending"}">${esc(item.refundStatusLabel)}</span>` : ""}
                            </div>
                        </div>
                        ${alert}
                        <div class="oad-ad-metrics">
                            <div class="is-strength">
                                <small>أولوية العرض</small>
                                <strong>${num(item.displayWeight)}%</strong>
                            </div>
                            <div><small>نقرات</small><strong>${num(item.clicksCount)}</strong></div>
                            <div><small>حجوزات</small><strong>${num(item.conversionsCount)}</strong></div>
                            <div class="is-unique-views">
                                <small>عدد المشاهدات</small>
                                <strong>${num(item.impressionsCount)}</strong>
                            </div>
                        </div>
                        <div class="oad-ad-card__foot">
                            <span><small>المصروف</small><strong>${esc(item.spentAmountText || "—")}</strong></span>
                            <span><small>المتبقي</small><strong>${esc(item.remainingBudgetText || "—")}</strong></span>
                            <button type="button" class="oad-btn oad-btn--soft" data-details-id="${item.advertisementId}">التفاصيل <i class="bi bi-arrow-left"></i></button>
                        </div>
                    </div>
                </article>`;
        }).join("") || `<div class="oad-empty"><i class="bi bi-megaphone"></i><h3>لا توجد إعلانات هنا</h3><p>غيّر الفلتر أو أنشئ إعلانًا جديدًا.</p></div>`;

        $("[data-results-count]", app).textContent = data.totalItems ?? 0;
        $$("[data-group]", app).forEach(btn => {
            const value = btn.dataset.group;
            btn.classList.toggle("active", value === state.currentGroup);
            const counts = {
                All: data.allCount,
                Running: data.runningCount,
                Scheduled: data.scheduledCount,
                NeedsAction: data.needsActionCount,
                Paused: data.pausedCount,
                Expired: data.expiredCount,
                Cancelled: data.cancelledCount
            };
            const b = $("b", btn);
            if (b) b.textContent = counts[value] ?? 0;
        });

        const pagination = $("[data-pagination]", app);
        if (pagination) {
            pagination.hidden = (data.totalPages || 1) <= 1;
            $("[data-current-page]", pagination).textContent = data.page || 1;
            $("[data-total-pages]", pagination).textContent = data.totalPages || 1;
            const buttons = $$("[data-page]", pagination);
            if (buttons[0]) {
                buttons[0].dataset.page = Math.max(1, (data.page || 1) - 1);
                buttons[0].disabled = (data.page || 1) <= 1;
            }
            if (buttons[1]) {
                buttons[1].dataset.page = Math.min(data.totalPages || 1, (data.page || 1) + 1);
                buttons[1].disabled = (data.page || 1) >= (data.totalPages || 1);
            }
        }
    }

    async function openWizard(id = null) {
        try {
            const url = new URL(app.dataset.wizardUrl, window.location.origin);
            if (id) url.searchParams.set("id", id);
            const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) throw new Error("تعذر فتح الإعلان.");
            state.wizardData = await response.json();
            state.selectedImageFile = null;
            state.selectedVideoFile = null;
            state.existingMedia = null;
            state.wizardStep = 1;
            state.currentWizardIsEdit = Boolean(id);

            const html = document.getElementById("oad-wizard-template").innerHTML;
            await Swal.fire({
                html,
                showConfirmButton: false,
                showCloseButton: true,
                width: "min(1120px, calc(100vw - 20px))",
                customClass: { popup: "oad-swal-shell oad-swal-builder" },
                didOpen: popup => {
                    hydrateWizard(popup, state.wizardData);
                    syncPlanUI(popup);
                    syncStartUI(popup);
                    updatePreviewText(popup);
                    updateMediaChoiceUI(popup);
                    updateNextState(popup);
                    quotePrice(popup);
                }
            });
        } catch (error) {
            toast(error.message || "تعذر فتح نافذة الإعلان.", "error");
        }
    }

    function hydrateWizard(root, data) {
        const form = $("[data-wizard-form]", root);
        const ad = data.advertisement || {};
        $("[data-wizard-title]", root).textContent = data.isEdit ? "تعديل الإعلان" : "إنشاء إعلان";
        setValue(form, "AdvertisementId", ad.advertisementId || "");
        setValue(form, "RowVersion", ad.rowVersion || "");
        setValue(form, "AdvertisementTitle", ad.advertisementTitle || "");
        setValue(form, "ShortDescription", ad.shortDescription || "");
        setValue(form, "ButtonText", ad.buttonText || "عرض المنشأة");
        setValue(form, "PromotionId", ad.promotionId || "");
        setValue(form, "DurationDays", ad.durationDays ?? 1);
        const durationInput = form.elements.DurationDays;
        if (durationInput) {
            durationInput.dataset.clearDefaultOnFirstClick = data.isEdit ? "false" : "true";
        }
        setValue(form, "TotalBudget", ad.totalBudget ?? "");
        setValue(form, "DailyBudget", ad.dailyBudget ?? "");
        setValue(form, "DeviceTarget", ad.deviceTarget || "All");

        const planId = String(ad.advertisementPlanId || "");
        $$('[name="AdvertisementPlanId"]', form).forEach(input => {
            input.checked = Boolean(planId) && input.value === planId;
        });

        $$('[name="StartImmediately"]', form).forEach(input => input.checked = input.value === String(ad.startImmediately !== false));
        if (ad.requestedStartAt) setValue(form, "RequestedStartAt", String(ad.requestedStartAt).slice(0, 16));

        setValue(form, "TargetingMode", "All");

        state.existingMedia = (data.media || [])[0] || null;
        if (state.existingMedia) {
            renderExistingMedia(root, state.existingMedia);
        }
        updateMediaChoiceUI(root);
    }

    function goWizardStep(step) {
        const root = $(".oad-swal-builder");
        if (!root) return;

        const target = Math.max(1, Math.min(4, step));
        if (target > state.wizardStep) {
            for (let current = state.wizardStep; current < target; current++) {
                if (!validateStep(current, root)) return;
            }
        }

        state.wizardStep = target;

        $$("[data-step]", root).forEach(section => {
            const active = Number(section.dataset.step) === state.wizardStep;
            section.hidden = !active;
            section.classList.toggle("active", active);
        });
        $$("[data-step-go]", root).forEach(btn => btn.classList.toggle("active", Number(btn.dataset.stepGo) === state.wizardStep));
        $("[data-prev]", root).hidden = state.wizardStep === 1;
        $("[data-next]", root).hidden = state.wizardStep === 4;
        $("[data-save-submit]", root).hidden = state.wizardStep !== 4;
        if (state.wizardStep === 1) updateNextState(root);
        if (state.wizardStep === 4) updateReview(root);
    }

    function validateStep(step, root = $(".oad-swal-builder")) {
        const form = $("[data-wizard-form]", root);
        clearWizardError(root);

        const invalid = (message, field = null) => {
            showWizardError(root, message, field);
            return false;
        };

        if (step === 1) {
            const plan = form.querySelector('[name="AdvertisementPlanId"]:checked');
            if (!plan) return invalid("اختر باقة إعلانية أولًا.");

            if (!validateDuration(root, true)) return false;

            const futureMode =
                form.querySelector('[name="StartImmediately"]:checked')?.value === "false";

            if (futureMode) {
                const field = form.elements.RequestedStartAt;
                const trigger = $("[data-future-datetime-trigger]", root);
                if (!field?.value) {
                    return invalid("حدد تاريخ ووقت البداية المطلوب.", trigger);
                }

                const requested = new Date(field.value);
                if (Number.isNaN(requested.getTime()) || requested <= new Date()) {
                    return invalid("تاريخ البداية يجب أن يكون في المستقبل.", trigger);
                }
            }

            return true;
        }

        if (step === 2) {
            const title = (form.elements.AdvertisementTitle.value || "").trim();
            if (title.length < 3) {
                return invalid("عنوان الإعلان مطلوب ويجب أن يكون 3 أحرف على الأقل.", form.elements.AdvertisementTitle);
            }
            if (title.length > 150) {
                return invalid("عنوان الإعلان لا يتجاوز 150 حرفًا.", form.elements.AdvertisementTitle);
            }

            const buttonText = (form.elements.ButtonText.value || "").trim();
            if (buttonText.length < 2) {
                return invalid("اكتب نصًا واضحًا لزر الإعلان.", form.elements.ButtonText);
            }
            if (buttonText.length > 50) {
                return invalid("نص زر الإعلان لا يتجاوز 50 حرفًا.", form.elements.ButtonText);
            }

            const hasNewMedia = Boolean(state.selectedImageFile || state.selectedVideoFile);
            const hasExistingMedia = Boolean(state.existingMedia?.filePath);
            if (!hasNewMedia && !hasExistingMedia) {
                return invalid(
                    "يجب اختيار وسيط للإعلان قبل المتابعة. اختر صورة أو فيديو واحدًا فقط، ولا يمكن ترك الإعلان بدون صورة أو فيديو.",
                    $("[data-image-file]", root)
                );
            }

            if (state.selectedImageFile && state.selectedVideoFile) {
                return invalid("لا يمكن اختيار صورة وفيديو في نفس الإعلان.");
            }

            return true;
        }

        if (step === 3) {
            const device = form.elements.DeviceTarget?.value || "All";
            if (!["All", "Mobile", "Desktop"].includes(device)) {
                return invalid("اختر نوع الجهاز.", form.elements.DeviceTarget);
            }
            return true;
        }

        return true;
    }

    function showWizardError(root, message, field = null) {
        if (!root) return;

        const box = $("[data-wizard-error]", root);
        const textEl = $("[data-wizard-error-text]", root);

        if (box) {
            box.hidden = false;
            box.classList.add("is-visible");
        }
        if (textEl) textEl.textContent = message;

        $$("[data-wizard-form] .is-invalid", root)
            .forEach(x => x.classList.remove("is-invalid"));

        if (field) {
            field.classList?.add("is-invalid");
            try { field.focus({ preventScroll: true }); } catch { field.focus?.(); }
            field.scrollIntoView?.({ behavior: "smooth", block: "center" });
        } else {
            box?.scrollIntoView?.({ behavior: "smooth", block: "nearest" });
        }
    }

    function clearWizardError(root = $(".oad-swal-builder")) {
        if (!root) return;

        const box = $("[data-wizard-error]", root);
        if (box) {
            box.hidden = true;
            box.classList.remove("is-visible");
        }

        $$("[data-wizard-form] .is-invalid", root)
            .forEach(x => x.classList.remove("is-invalid"));
    }

    function validateDuration(root = $(".oad-swal-builder"), focusOnError = false) {
        if (!root) return false;

        const form = $("[data-wizard-form]", root);
        const input = form?.elements?.DurationDays;
        const error = $("[data-duration-error]", root);
        if (!input) return false;

        const raw = String(input.value || "").trim();
        const days = Number(raw);
        let message = "";

        if (!raw || !Number.isInteger(days) || days < 1) {
            message = "أدخل عدد أيام صحيحًا يبدأ من يوم واحد.";
        } else if (days > 365) {
            message = "لا يمكن أن تتجاوز مدة الإعلان سنة واحدة (365 يومًا).";
        }

        input.classList.toggle("is-invalid", Boolean(message));
        input.setAttribute("aria-invalid", message ? "true" : "false");

        if (error) {
            error.hidden = !message;
            error.textContent = message;
        }

        if (message && focusOnError) {
            showWizardError(root, message, input);
        }

        return !message;
    }

    function toggleMorePlans(button) {
        const root = button.closest(".oad-swal-builder");
        if (!root) return;

        const nextExpanded = button.dataset.expanded !== "true";

        $$("[data-plan-grid] .oad-cb-plan--extra", root)
            .forEach(card => card.classList.toggle("is-revealed", nextExpanded));

        button.dataset.expanded = String(nextExpanded);

        const label = $("span", button);
        const icon = $("i", button);
        const hiddenCount = Number(button.dataset.hiddenCount || 0);

        if (label) {
            label.textContent = nextExpanded
                ? "عرض أقل"
                : `عرض المزيد (${hiddenCount})`;
        }

        if (icon) {
            icon.className = nextExpanded
                ? "bi bi-chevron-up"
                : "bi bi-chevron-down";
        }
    }

    function updateMediaChoiceUI(root = $(".oad-swal-builder")) {
        if (!root) return;

        const imageInput = $("[data-image-file]", root);
        const videoInput = $("[data-video-file]", root);
        const imageOption = imageInput?.closest(".oad-media-option");
        const videoOption = videoInput?.closest(".oad-media-option");
        const imageName = $("[data-image-name]", root);
        const videoName = $("[data-video-name]", root);
        const clearImage = $("[data-clear-image]", root);
        const clearVideo = $("[data-clear-video]", root);

        const hasImage = Boolean(state.selectedImageFile);
        const hasVideo = Boolean(state.selectedVideoFile);

        imageOption?.classList.toggle("selected", hasImage);
        videoOption?.classList.toggle("selected", hasVideo);

        // لا يسمح باختيار النوع الآخر حتى يلغي المالك اختياره الحالي بنفسه.
        if (imageInput) imageInput.disabled = hasVideo;
        if (videoInput) videoInput.disabled = hasImage;

        imageOption?.classList.toggle("is-disabled", hasVideo);
        videoOption?.classList.toggle("is-disabled", hasImage);

        if (clearImage) clearImage.hidden = !hasImage;
        if (clearVideo) clearVideo.hidden = !hasVideo;

        if (imageName) {
            if (hasImage) {
                imageName.textContent = state.selectedImageFile.name;
            } else if (hasVideo) {
                imageName.textContent = "ألغِ تحديد الفيديو أولًا لاختيار صورة.";
            } else if (
                state.existingMedia &&
                String(state.existingMedia.mediaType).toLowerCase() === "image"
            ) {
                imageName.textContent = "الصورة الحالية محفوظة.";
            } else {
                imageName.textContent = "JPG / PNG / WebP حتى 10MB";
            }
        }

        if (videoName) {
            if (hasVideo) {
                videoName.textContent = state.selectedVideoFile.name;
            } else if (hasImage) {
                videoName.textContent = "ألغِ تحديد الصورة أولًا لاختيار فيديو.";
            } else if (
                state.existingMedia &&
                String(state.existingMedia.mediaType).toLowerCase() === "video"
            ) {
                videoName.textContent = "الفيديو الحالي محفوظ.";
            } else {
                videoName.textContent = "MP4 / WebM حتى 25MB";
            }
        }
    }

    function clearSelectedMedia(kind, root = $(".oad-swal-builder")) {
        if (!root) return;

        if (kind === "image") {
            state.selectedImageFile = null;
            const input = $("[data-image-file]", root);
            if (input) input.value = "";
        } else {
            state.selectedVideoFile = null;
            const input = $("[data-video-file]", root);
            if (input) input.value = "";
        }

        clearWizardError(root);
        updateMediaChoiceUI(root);

        const remaining = state.selectedImageFile || state.selectedVideoFile;
        if (remaining) {
            renderPreviewFile(remaining, root);
            return;
        }

        if (state.existingMedia?.filePath) {
            renderExistingMedia(root, state.existingMedia);
            return;
        }

        const emptyMarkup = '<i class="bi bi-image"></i>';
        const preview = $("[data-preview-media]", root);
        const review = $("[data-review-media]", root);
        if (preview) preview.innerHTML = emptyMarkup;
        if (review) review.innerHTML = emptyMarkup;
    }

    function syncPlanUI(root = $(".oad-swal-builder")) {
        if (!root) return;

        const form = $("[data-wizard-form]", root);
        const selectedInput = form.querySelector('[name="AdvertisementPlanId"]:checked');
        const selected = selectedInput?.closest("[data-plan]");
        const flatPanel = $("[data-flat-panel]", root);

        $$("[data-plan]", root).forEach(card => {
            card.classList.toggle("selected", card === selected);
        });

        if (!selected) {
            if (flatPanel) flatPanel.hidden = true;
            text(root, "[data-quote-plan]", "اختر باقة");
            text(root, "[data-quote-weight]", "—");
            text(root, "[data-quote-daily]", "—");
            text(root, "[data-quote-total]", "—");
            text(root, "[data-quote-note]", "اختر الباقة أولًا لإظهار السعر والإجمالي.");
            updateNextState(root);
            return;
        }

        if (flatPanel) flatPanel.hidden = false;

        const planName = selected.dataset.planName ||
            selected.querySelector("[data-plan-label]")?.textContent?.trim() ||
            "الباقة";

        const priceText = selected.dataset.priceText || "—";
        const weight = selected.dataset.weight || "—";

        text(root, "[data-flat-unit]", priceText);
        text(root, "[data-flat-weight]", weight);
        text(root, "[data-quote-plan]", planName);
        text(root, "[data-quote-weight]", weight);
        text(root, "[data-quote-daily]", priceText);

        if (form.elements.DurationDays) {
            form.elements.DurationDays.required = true;
        }

        updateStep1Summary(root);
        updateNextState(root);
    }

    function updateStep1Summary(root = $(".oad-swal-builder")) {
        if (!root) return;
        const form = $("[data-wizard-form]", root);
        const days = Number(form?.elements?.DurationDays?.value || 0);

        text(
            root,
            "[data-quote-days]",
            days > 0 ? `${days} ${days === 1 ? "يوم" : "أيام"}` : "—"
        );
    }

    function updateNextState(root = $(".oad-swal-builder")) {
        if (!root) return;

        const next = $("[data-next]", root);
        if (!next || state.wizardStep !== 1) return;

        const form = $("[data-wizard-form]", root);
        const selected = form.querySelector('[name="AdvertisementPlanId"]:checked');
        const durationValid = validateDuration(root, false);

        next.disabled = !selected || !durationValid;
        next.classList.toggle("is-ready", Boolean(selected) && durationValid);
    }

    function syncStartUI(root = $(".oad-swal-builder")) {
        if (!root) return;

        const immediate = $('[name="StartImmediately"]:checked', root)?.value !== "false";
        const box = $("[data-start-date]", root);
        const form = $("[data-wizard-form]", root);
        const field = form?.elements?.RequestedStartAt;
        const panel = $("[data-future-datetime-picker]", root);

        if (box) {
            box.classList.toggle("is-visible", !immediate);
            if (immediate) {
                box.hidden = true;
                box.setAttribute("hidden", "");
            } else {
                box.hidden = false;
                box.removeAttribute("hidden");
            }
        }

        if (field) {
            field.disabled = immediate;
            if (immediate) {
                if (panel) panel.hidden = true;
            } else {
                field.disabled = false;
                syncFutureDateTimeDisplay(root);
            }
        }
    }

    const FUTURE_MONTHS = [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];
    const FUTURE_WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    function openFutureDateTimePicker(root = $(".oad-swal-builder")) {
        if (!root) return;
        const form = $("[data-wizard-form]", root);
        const field = form?.elements?.RequestedStartAt;
        const panel = $("[data-future-datetime-picker]", root);
        if (!field || !panel) return;

        const now = new Date();
        const parsed = parseLocalDateTime(field.value);
        const base = parsed && parsed > now ? parsed : defaultFutureDateTime(now);

        panel.dataset.year = String(base.getFullYear());
        panel.dataset.month = String(base.getMonth());
        panel.dataset.selectedDate = ymd(base.getFullYear(), base.getMonth() + 1, base.getDate());
        panel.dataset.hour = String(base.getHours()).padStart(2, "0");
        panel.dataset.minute = String(base.getMinutes()).padStart(2, "0");
        panel.hidden = false;
        clearFuturePickerError(panel);
        renderFutureDateTimePicker(panel);
        panel.scrollIntoView?.({ behavior: "smooth", block: "nearest" });
    }

    function renderFutureDateTimePicker(panel) {
        if (!panel) return;
        const year = Number(panel.dataset.year);
        const month = Number(panel.dataset.month);
        const selected = panel.dataset.selectedDate || "";
        const hour = panel.dataset.hour || "00";
        const minute = panel.dataset.minute || "00";
        const today = new Date();
        const todayKey = ymd(today.getFullYear(), today.getMonth() + 1, today.getDate());
        const firstDay = new Date(year, month, 1).getDay();
        const daysInMonth = new Date(year, month + 1, 0).getDate();
        const currentMonthKey = today.getFullYear() * 12 + today.getMonth();
        const shownMonthKey = year * 12 + month;

        const dayCells = [];
        for (let blank = 0; blank < firstDay; blank++) {
            dayCells.push('<span class="oad-future-picker__blank"></span>');
        }
        for (let day = 1; day <= daysInMonth; day++) {
            const key = ymd(year, month + 1, day);
            const isPast = key < todayKey;
            const isSelected = key === selected;
            const isToday = key === todayKey;
            dayCells.push(`
                <button type="button"
                        class="oad-future-picker__day${isSelected ? " is-selected" : ""}${isToday ? " is-today" : ""}"
                        data-future-day="${key}"
                        ${isPast ? "disabled" : ""}>${day}</button>`);
        }

        const hourOptions = Array.from({ length: 24 }, (_, i) => {
            const value = String(i).padStart(2, "0");
            return `<option value="${value}"${value === hour ? " selected" : ""}>${value}</option>`;
        }).join("");
        const minuteOptions = Array.from({ length: 60 }, (_, i) => {
            const value = String(i).padStart(2, "0");
            return `<option value="${value}"${value === minute ? " selected" : ""}>${value}</option>`;
        }).join("");

        panel.innerHTML = `
            <div class="oad-future-picker__head">
                <button type="button" data-future-nav="-1" aria-label="Previous month" ${shownMonthKey <= currentMonthKey ? "disabled" : ""}><i class="bi bi-chevron-left"></i></button>
                <strong>${FUTURE_MONTHS[month]} ${year}</strong>
                <button type="button" data-future-nav="1" aria-label="Next month"><i class="bi bi-chevron-right"></i></button>
            </div>
            <div class="oad-future-picker__week">${FUTURE_WEEKDAYS.map(x => `<span>${x}</span>`).join("")}</div>
            <div class="oad-future-picker__days">${dayCells.join("")}</div>
            <div class="oad-future-picker__time">
                <label><span>Hour</span><select data-future-hour>${hourOptions}</select></label>
                <span class="oad-future-picker__colon">:</span>
                <label><span>Minute</span><select data-future-minute>${minuteOptions}</select></label>
            </div>
            <div class="oad-future-picker__error" data-future-picker-error hidden></div>
            <div class="oad-future-picker__actions" dir="rtl">
                <button type="button" data-future-picker-cancel>إلغاء</button>
                <button type="button" class="is-primary" data-future-picker-apply>موافق</button>
            </div>`;
    }

    function shiftFuturePickerMonth(panel, delta) {
        if (!panel || !delta) return;
        const current = new Date(Number(panel.dataset.year), Number(panel.dataset.month) + delta, 1);
        const now = new Date();
        const minimum = new Date(now.getFullYear(), now.getMonth(), 1);
        if (current < minimum) return;
        panel.dataset.year = String(current.getFullYear());
        panel.dataset.month = String(current.getMonth());
        renderFutureDateTimePicker(panel);
    }

    function applyFutureDateTimePicker(panel) {
        if (!panel) return;
        const root = panel.closest(".oad-swal-builder");
        const form = $("[data-wizard-form]", root);
        const field = form?.elements?.RequestedStartAt;
        const selected = panel.dataset.selectedDate || "";
        const hour = panel.querySelector("[data-future-hour]")?.value || panel.dataset.hour || "00";
        const minute = panel.querySelector("[data-future-minute]")?.value || panel.dataset.minute || "00";
        if (!field || !selected) {
            showFuturePickerError(panel, "Select a date first.");
            return;
        }

        const value = `${selected}T${hour}:${minute}`;
        const requested = new Date(value);
        if (Number.isNaN(requested.getTime()) || requested <= new Date()) {
            showFuturePickerError(panel, "Please select a future date and time.");
            return;
        }

        field.value = value;
        field.dispatchEvent(new Event("input", { bubbles: true }));
        panel.hidden = true;
        syncFutureDateTimeDisplay(root);
        clearWizardError(root);
        updateNextState(root);
    }

    function syncFutureDateTimeDisplay(root = $(".oad-swal-builder")) {
        if (!root) return;
        const form = $("[data-wizard-form]", root);
        const value = form?.elements?.RequestedStartAt?.value || "";
        const label = $("[data-future-datetime-text]", root);
        if (!label) return;
        const parsed = parseLocalDateTime(value);
        label.textContent = parsed
            ? parsed.toLocaleString("en-US", {
                month: "short", day: "2-digit", year: "numeric",
                hour: "2-digit", minute: "2-digit", hour12: true
            })
            : "Select date, hour & minute";
    }

    function parseLocalDateTime(value) {
        if (!value) return null;
        const parsed = new Date(value);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    function defaultFutureDateTime(now = new Date()) {
        const result = new Date(now.getTime() + 5 * 60000);
        result.setSeconds(0, 0);
        return result;
    }

    function ymd(year, month, day) {
        return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
    }

    function showFuturePickerError(panel, message) {
        const box = $("[data-future-picker-error]", panel);
        if (!box) return;
        box.textContent = message;
        box.hidden = false;
    }

    function clearFuturePickerError(panel) {
        const box = $("[data-future-picker-error]", panel);
        if (!box) return;
        box.textContent = "";
        box.hidden = true;
    }


    async function quotePrice(root = $(".oad-swal-builder")) {
        if (!root) return;

        const form = $("[data-wizard-form]", root);
        const planInput = form.querySelector('[name="AdvertisementPlanId"]:checked');

        if (!planInput) {
            text(root, "[data-quote-total]", "—");
            text(root, "[data-flat-total]", "—");
            text(root, "[data-quote-note]", "اختر الباقة أولًا لإظهار السعر والإجمالي.");
            return;
        }

        if (!validateDuration(root, false)) {
            text(root, "[data-quote-total]", "—");
            text(root, "[data-flat-total]", "—");
            text(root, "[data-quote-note]", "صحح عدد الأيام أولًا.");
            return;
        }

        const data = new FormData();
        data.append("AdvertisementPlanId", planInput.value);
        data.append("DurationDays", form.elements.DurationDays.value);

        try {
            const response = await postForm(app.dataset.quoteUrl, data);
            const result = await readJson(response);

            if (!response.ok || !result.success) {
                text(root, "[data-quote-total]", "—");
                text(root, "[data-flat-total]", "—");
                text(root, "[data-quote-note]", result.message || "راجع بيانات الباقة.");
                return;
            }

            text(root, "[data-quote-total]", result.finalAmountText || "—");
            text(root, "[data-flat-total]", result.finalAmountText || "—");
            text(
                root,
                "[data-quote-note]",
                `الإجمالي = سعر اليوم × ${form.elements.DurationDays.value} ${Number(form.elements.DurationDays.value) === 1 ? "يوم" : "أيام"}`
            );
            updateStep1Summary(root);
        } catch {
            text(root, "[data-quote-note]", "تعذر حساب السعر الآن.");
        }
    }

    function updatePreviewText(root = $(".oad-swal-builder")) {
        if (!root) return;
        const form = $("[data-wizard-form]", root);
        const title = (form.elements.AdvertisementTitle.value || "").trim() || "عنوان إعلانك";
        const desc = (form.elements.ShortDescription.value || "").trim() || "سيظهر الوصف هنا.";
        const button = (form.elements.ButtonText.value || "").trim() || "عرض المنشأة";
        $$("[data-preview-title],[data-review-title]", root).forEach(x => x.textContent = title);
        $$("[data-preview-desc],[data-review-desc]", root).forEach(x => x.textContent = desc);
        $$("[data-preview-button],[data-review-button]", root).forEach(x => x.textContent = button);
    }

    function renderPreviewFile(file, root = $(".oad-swal-builder")) {
        if (!root || !file) return;
        const url = URL.createObjectURL(file);
        const video = file.type.startsWith("video/");
        const markup = video ? `<video src="${url}" controls muted></video>` : `<img src="${url}" alt="معاينة">`;
        $("[data-preview-media]", root).innerHTML = markup;
        $("[data-review-media]", root).innerHTML = markup;
    }

    function renderExistingMedia(root, media) {
        if (!media?.filePath) return;
        const markup = String(media.mediaType).toLowerCase() === "video"
            ? `<video src="${attr(media.filePath)}" controls muted></video>`
            : `<img src="${attr(media.filePath)}" alt="معاينة">`;
        $("[data-preview-media]", root).innerHTML = markup;
        $("[data-review-media]", root).innerHTML = markup;
    }

    function updateReview(root = $(".oad-swal-builder")) {
        if (!root) return;

        const form = $("[data-wizard-form]", root);
        const selected = form.querySelector('[name="AdvertisementPlanId"]:checked')?.closest("[data-plan]");

        const planName =
            selected?.dataset.planName ||
            selected?.querySelector("[data-plan-label]")?.textContent?.trim() ||
            "—";

        const weight = selected?.dataset.weight || "—";
        const weightText = weight === "—" ? weight : `${weight}%`;
        const daily = selected?.dataset.priceText || "—";
        const days = Number(form.elements.DurationDays?.value || 0);

        text(root, "[data-review-plan]", planName);
        text(root, "[data-review-weight]", weightText);
        text(root, "[data-review-daily]", daily);
        text(root, "[data-review-days]", days > 0 ? `${days} ${days === 1 ? "يوم" : "أيام"}` : "—");
        text(root, "[data-review-total]", $("[data-quote-total]", root)?.textContent || "—");

        const immediate =
            form.querySelector('[name="StartImmediately"]:checked')?.value !== "false";
        const startValue = form.elements.RequestedStartAt?.value;

        if (immediate) {
            text(root, "[data-review-start]", "بعد اعتماد الإدارة");
            text(root, "[data-review-end]", "تُحسب من وقت البدء الفعلي");
        } else {
            const start = startValue ? new Date(startValue) : null;
            const startText =
                start && !Number.isNaN(start.getTime())
                    ? start.toLocaleString("ar-YE")
                    : "—";

            text(root, "[data-review-start]", startText);

            if (start && !Number.isNaN(start.getTime()) && days > 0) {
                const end = new Date(start.getTime());
                end.setDate(end.getDate() + days);
                text(root, "[data-review-end]", end.toLocaleString("ar-YE"));
            } else {
                text(root, "[data-review-end]", "—");
            }
        }

        const device = form.elements.DeviceTarget?.value || "All";
        const deviceLabel = {
            All: "كل الأجهزة",
            Mobile: "الهاتف",
            Desktop: "الكمبيوتر"
        }[device] || "كل الأجهزة";

        text(root, "[data-review-device]", deviceLabel);

        const buttonText = (form.elements.ButtonText?.value || "").trim() || "عرض المنشأة";
        text(root, "[data-review-button-text]", buttonText);

        const promotion = form.elements.PromotionId;
        text(
            root,
            "[data-review-promotion]",
            promotion?.selectedOptions?.[0]?.textContent?.trim() || "بدون عرض"
        );

        updatePreviewText(root);
    }

    async function saveAndSubmitWizard() {
        const root = $(".oad-swal-builder");
        if (!root) return;

        for (let step = 1; step <= 3; step++) {
            if (!validateStep(step, root)) {
                goWizardStep(step);
                return;
            }
        }

        updateReview(root);
        clearWizardError(root);

        const panel = $("[data-wizard-confirm]", root);
        if (panel) {
            panel.hidden = false;
            panel.classList.add("is-visible");
        }
    }

    function hideWizardConfirmation(root = $(".oad-swal-builder")) {
        if (!root) return;
        const panel = $("[data-wizard-confirm]", root);
        if (panel) {
            panel.classList.remove("is-visible");
            panel.hidden = true;
        }
    }

    async function confirmAndContinueToPayment(button) {
        const root = $(".oad-swal-builder");
        if (!root) return;

        const form = $("[data-wizard-form]", root);
        if (!form) return;

        for (let step = 1; step <= 3; step++) {
            if (!validateStep(step, root)) {
                hideWizardConfirmation(root);
                goWizardStep(step);
                return;
            }
        }

        busy(button, true);

        try {
            const payload = collectWizardForm(form);
            const saveResponse = await postForm(app.dataset.saveUrl, payload);
            const saveResult = await readJson(saveResponse);

            if (!saveResponse.ok || !saveResult.success) {
                throw new Error(saveResult.message || "تعذر حفظ الإعلان.");
            }

            let advertisementId = saveResult.advertisementId;
            let rowVersion = saveResult.rowVersion;

            const mediaFile = state.selectedImageFile || state.selectedVideoFile;

            // عند تعديل إعلان موجود: اختيار وسيط جديد يستبدل الوسيط القديم أولًا.
            if (mediaFile && state.existingMedia?.advertisementMediaId) {
                const remove = new FormData();
                remove.append("AdvertisementId", advertisementId);
                remove.append("AdvertisementMediaId", state.existingMedia.advertisementMediaId);
                remove.append("RowVersion", rowVersion);

                const deleteResponse = await postForm(app.dataset.deleteMediaUrl, remove);
                const deleteResult = await readJson(deleteResponse);

                if (!deleteResponse.ok || !deleteResult.success) {
                    throw new Error(deleteResult.message || "تعذر استبدال وسيط الإعلان الحالي.");
                }

                rowVersion = deleteResult.rowVersion || rowVersion;
                state.existingMedia = null;
            }

            if (mediaFile) {
                const media = new FormData();
                media.append("advertisementId", advertisementId);
                media.append("rowVersion", rowVersion);
                media.append("file", mediaFile);
                media.append("isPrimary", "true");
                media.append("altText", form.elements.AdvertisementTitle.value);

                const uploadResponse = await postForm(app.dataset.uploadUrl, media);
                const uploadResult = await readJson(uploadResponse);

                if (!uploadResponse.ok || !uploadResult.success) {
                    throw new Error(uploadResult.message || "تعذر رفع وسيط الإعلان.");
                }

                rowVersion = uploadResult.rowVersion || rowVersion;
            }

            const submit = new FormData();
            submit.append("AdvertisementId", advertisementId);
            submit.append("RowVersion", rowVersion);

            const submitResponse = await postForm(app.dataset.submitUrl, submit);
            const submitResult = await readJson(submitResponse);

            if (!submitResponse.ok || !submitResult.success) {
                throw new Error(submitResult.message || "تعذر تجهيز الإعلان للدفع.");
            }

            if (submitResult.status === "AwaitingPayment") {
                await openPayment(advertisementId, root);
                return;
            }

            Swal.close();
            toast(submitResult.message || "تم إرسال الإعلان للمراجعة.", "success");
            await refreshAfterChange();
        } catch (error) {
            hideWizardConfirmation(root);
            showWizardError(root, error.message || "تعذر إكمال العملية.");
        } finally {
            busy(button, false);
        }
    }

    function collectWizardForm(form) {
        const fd = new FormData();

        const scalarNames = [
            "AdvertisementId",
            "AdvertisementPlanId",
            "AdvertisementTitle",
            "ShortDescription",
            "ButtonText",
            "PromotionId",
            "StartImmediately",
            "RequestedStartAt",
            "DurationDays",
            "TargetingMode",
            "DeviceTarget",
            "RowVersion"
        ];

        scalarNames.forEach(name => {
            const field = form.elements.namedItem(name);
            if (!field) return;

            const value = field instanceof RadioNodeList
                ? field.value
                : field.value;

            if (value !== "") {
                fd.append(name, value);
            }
        });

        return fd;
    }

    async function openDetails(id) {
        try {
            const response = await fetch(`${app.dataset.detailsUrl}?id=${encodeURIComponent(id)}`, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) throw new Error("تعذر تحميل التفاصيل.");
            const data = await response.json();
            state.currentDetails = data;
            const html = document.getElementById("oad-details-template").innerHTML;
            await Swal.fire({
                html,
                showConfirmButton: false,
                showCloseButton: true,
                width: "min(980px, calc(100vw - 20px))",
                customClass: { popup: "oad-swal-shell oad-swal-details" },
                didOpen: popup => hydrateDetails(popup, data)
            });
        } catch (error) {
            toast(error.message || "تعذر تحميل التفاصيل.", "error");
        }
    }

    function hydrateDetails(root, d) {
        text(root, "[data-d-title]", d.advertisementTitle);
        text(root, "[data-d-place]", d.placeName);
        text(root, "[data-d-billing]", d.billingModelLabel);
        text(root, "[data-d-description]", d.shortDescription || "لا يوجد وصف.");
        text(root, "[data-d-impressions]", num(d.impressionsCount));
        text(root, "[data-d-clicks]", num(d.clicksCount));
        text(root, "[data-d-starts]", num(d.bookingStartsCount));
        text(root, "[data-d-conversions]", num(d.conversionsCount));
        text(root, "[data-d-expected]", d.expectedPaymentText || "—");
        text(root, "[data-d-spent]", d.spentAmountText || "—");
        text(root, "[data-d-remaining]", d.remainingBudgetText || "—");
        text(root, "[data-d-duration]", d.billingModel === "Flat" ? d.durationLabel : "حتى انتهاء الميزانية");
        text(root, "[data-d-targeting]", d.targetingModeLabel || "الجميع");
        text(root, "[data-d-device]", d.deviceTargetLabel || "كل الأجهزة");
        // إذا اختار المالك موعدًا مستقبليًا، هذا هو موعد البداية الحقيقي الذي يجب عرضه.
        // وقت موافقة الإدارة منفصل ولا يُستخدم كبداية للحملة.
        const displayedStart = d.startImmediately
            ? d.startAt
            : (d.requestedStartAt || d.startAt);
        text(root, "[data-d-start-date]", formatDateOnly(displayedStart));
        text(root, "[data-d-start-time]", formatTimeOnly(displayedStart));
        text(root, "[data-d-end-date]", formatDateOnly(d.endAt));
        text(root, "[data-d-end-time]", formatTimeOnly(d.endAt));

        const status = $("[data-d-status]", root);
        status.textContent = d.statusLabel || "—";
        status.className = `is-${slug(d.status)}`;
        const payment = $("[data-d-payment]", root);
        payment.textContent = d.paymentStatusLabel || "—";
        payment.className = `is-${slug(d.paymentStatus)}`;

        const media = (d.media || [])[0];
        $("[data-d-media]", root).innerHTML = media?.filePath
            ? (String(media.mediaType).toLowerCase() === "video"
                ? `<video src="${attr(media.filePath)}" controls></video>`
                : `<img src="${attr(media.filePath)}" alt="${attr(d.advertisementTitle)}">`)
            : `<i class="bi bi-image"></i>`;

        const noticeText = d.rejectionReason || d.pauseReason || d.cancellationReason || "";
        const notice = $("[data-d-notice]", root);
        notice.hidden = !noticeText;
        notice.innerHTML = noticeText ? `<i class="bi bi-exclamation-circle-fill"></i><span>${esc(noticeText)}</span>` : "";

        const refund = $("[data-d-refund]", root);
        const hasRefund = Boolean(d.refundRequestedAt || d.refundedAt || String(d.paymentStatus || "") === "Refunded");
        if (refund) {
            refund.hidden = !hasRefund;
            if (hasRefund) {
                text(refund, "[data-d-refund-status]", d.refundStatusLabel || (d.refundedAt ? "تم الاسترداد" : "بانتظار الاسترداد"));
                text(refund, "[data-d-refund-amount]", d.refundAmountText || d.transferredAmountText || "يحدد عند التنفيذ");
                text(refund, "[data-d-refund-wallet]", d.refundWalletName || "—");
                text(refund, "[data-d-refund-account]", d.refundAccountNumber || "—");
                text(refund, "[data-d-refund-beneficiary]", d.refundBeneficiaryName || "—");
                text(refund, "[data-d-refund-reference]", d.refundReferenceNumber || "بانتظار التنفيذ");
                text(refund, "[data-d-refund-requested-date]", formatDateOnly(d.refundRequestedAt));
                text(refund, "[data-d-refund-requested-time]", formatTimeOnly(d.refundRequestedAt));
                text(refund, "[data-d-refund-at-date]", formatDateOnly(d.refundedAt));
                text(refund, "[data-d-refund-at-time]", formatTimeOnly(d.refundedAt));
                text(refund, "[data-d-refund-note]", d.refundNote || "—");

                const proof = $("[data-d-refund-proof]", refund);
                if (proof) {
                    proof.innerHTML = d.refundProofImage
                        ? `<a href="${attr(d.refundProofImage)}" target="_blank" rel="noopener"><i class="bi bi-image"></i> عرض إثبات الاسترداد</a>`
                        : `<span>بانتظار رفع إثبات الاسترداد من الإدارة.</span>`;
                }
            }
        }

        const actions = $("[data-d-actions]", root);
        const list = [];
        if (d.canEdit) list.push(actionButton("edit", "bi-pencil-square", "تعديل", "soft", d));
        if (d.canPay) list.push(actionButton("pay", "bi-receipt", "رفع إثبات الدفع", "primary", d));
        if (d.canSubmit) list.push(actionButton("submit", "bi-send-check", "إرسال للمراجعة", "primary", d));
        if (d.canPause) list.push(actionButton("pause", "bi-pause-circle", "إيقاف", "soft", d));
        if (d.canResume) list.push(actionButton("resume", "bi-play-circle", "استئناف", "primary", d));
        if (d.canRestore) list.push(actionButton("restore", "bi-arrow-counterclockwise", "استعادة الإعلان", "primary", d));
        if (d.canRenew) list.push(actionButton("renew", "bi-arrow-repeat", "تجديد", "primary", d));
        if (d.canCancel) list.push(actionButton("cancel", "bi-x-circle", "إلغاء", "danger", d));
        actions.innerHTML = list.join("");
    }

    function actionButton(action, icon, label, mode, d) {
        return `<button type="button" class="oad-btn oad-btn--${mode}" data-owner-action="${action}" data-ad-id="${d.advertisementId}" data-row-version="${attr(d.rowVersion)}" data-payment-status="${attr(d.paymentStatus || "")}"><i class="bi ${icon}"></i>${label}</button>`;
    }

    async function handleOwnerAction(button) {
        const action = button.dataset.ownerAction;
        const id = Number(button.dataset.adId);
        const rowVersion = button.dataset.rowVersion || "";

        if (action === "edit") {
            Swal.close();
            setTimeout(() => openWizard(id), 180);
            return;
        }
        if (action === "pay") {
            Swal.close();
            setTimeout(() => openPayment(id), 180);
            return;
        }

        if (action === "restore") {
            const result = await confirmBox(
                "استعادة الإعلان",
                "سيعود الإعلان الملغي للعمل ويكمل المدة المتبقية مع الاحتفاظ بإحصائياته السابقة.",
                "استعادة"
            );
            if (!result) return;
            return postState(app.dataset.restoreUrl, { AdvertisementId: id, RowVersion: rowVersion });
        }

        if (action === "renew") {
            const result = await confirmBox("تجديد الإعلان", "سيتم تجديد الإعلان والبدء بعد الاعتماد.", "تجديد");
            if (!result) return;
            return postState(app.dataset.renewUrl, { AdvertisementId: id, RowVersion: rowVersion, StartImmediately: true });
        }

        if (action === "submit") {
            const result = await confirmBox("إرسال الإعلان", "سيتم إرسال الإعلان للمراجعة.", "إرسال");
            if (!result) return;
            return postState(app.dataset.submitUrl, { AdvertisementId: id, RowVersion: rowVersion });
        }

        if (action === "resume") {
            const result = await confirmBox(
                "استئناف الإعلان",
                "سيعود الإعلان المتوقف مؤقتًا للعمل.",
                "استئناف"
            );
            if (!result) return;
            return postState(app.dataset.resumeUrl, { AdvertisementId: id, RowVersion: rowVersion });
        }

        if (action === "pause") {
            const result = await Swal.fire({
                title: "إيقاف الإعلان مؤقتًا",
                text: "يمكنك استئنافه لاحقًا.",
                input: "textarea",
                inputPlaceholder: "سبب الإيقاف...",
                showConfirmButton: true,
                showCancelButton: true,
                confirmButtonText: "إيقاف",
                cancelButtonText: "تراجع",
                reverseButtons: true,
                customClass: { popup: "oad-confirm-popup", confirmButton: "oad-confirm-primary", cancelButton: "oad-confirm-cancel" },
                buttonsStyling: false
            });
            if (!result.isConfirmed) return;
            return postState(app.dataset.pauseUrl, {
                AdvertisementId: id,
                RowVersion: rowVersion,
                Reason: String(result.value || "").trim()
            });
        }

        if (action === "cancel") {
            const paymentStatus = String(button.dataset.paymentStatus || "");
            let details = state.currentDetails?.advertisementId === id ? state.currentDetails : null;

            if (!details) {
                try {
                    const response = await fetch(`${app.dataset.detailsUrl}?id=${encodeURIComponent(id)}`, {
                        headers: { "X-Requested-With": "XMLHttpRequest" },
                        cache: "no-store"
                    });
                    if (!response.ok) throw new Error("تعذر تحميل تفاصيل الإعلان.");
                    details = await response.json();
                } catch (error) {
                    toast(error.message || "تعذر تحميل تفاصيل الإعلان.", "error");
                    return;
                }
            }

            const hasPaidOrPendingPayment = paymentStatus === "Paid" || paymentStatus === "PendingReview";
            const scheduledStart = details?.startImmediately === false;
            const needsRefundWallet = hasPaidOrPendingPayment && scheduledStart;
            const wallets = needsRefundWallet ? (details?.refundWallets || []) : [];
            if (needsRefundWallet && wallets.length === 0) {
                await Swal.fire({
                    icon: "warning",
                    title: "أضف محفظة للاسترداد أولًا",
                    text: "لا توجد لديك محفظة استلام مفعلة. أضف محفظة إلى حساباتك المالية ثم أعد محاولة إلغاء الإعلان.",
                    confirmButtonText: "حسنًا",
                    customClass: { popup: "oad-confirm-popup", confirmButton: "oad-confirm-primary" },
                    buttonsStyling: false
                });
                return;
            }

            const walletOptions = wallets.map(wallet => `
                <option
                    value="${Number(wallet.ownerWalletId) || 0}"
                    data-wallet-name="${attr(wallet.walletName || "محفظة")}"
                    data-account-number="${attr(wallet.accountNumber || "")}"
                    data-beneficiary-name="${attr(wallet.beneficiaryName || "")}">
                    ${esc(wallet.walletName || "محفظة")}
                </option>`).join("");

            const explanation = needsRefundWallet
                ? (paymentStatus === "Paid"
                    ? "الإعلان له موعد عرض محدد ولم يبدأ بعد. اختر محفظة الاسترداد، وسيتم حفظ بياناتها كاملة مع طلب الإلغاء."
                    : "الإعلان له موعد عرض محدد ولم يبدأ بعد. إذا اعتمدت الإدارة إثبات الدفع فسيتم رد المبلغ إلى محفظة الاسترداد التي تختارها.")
                : (hasPaidOrPendingPayment && !scheduledStart
                    ? "هذا الإعلان مضبوط ليبدأ مباشرة بعد موافقة الإدارة. بعد تأكيد الدفع يكون المبلغ غير قابل للاسترداد؛ يمكنك إلغاء الإعلان ولكن دون استرداد المبلغ."
                    : "اكتب سبب الإلغاء. لا يوجد مبلغ مدفوع يحتاج إلى استرداد.");

            const result = await Swal.fire({
                title: "إلغاء الإعلان",
                html: `
                    <div class="oad-cancel-refund" dir="rtl">
                        <p class="oad-cancel-refund__intro">${esc(explanation)}</p>
                        ${needsRefundWallet ? `
                            <label class="oad-cancel-refund__field">
                                <span><i class="bi bi-wallet2"></i> محفظة استلام الاسترداد</span>
                                <select id="oadRefundOwnerWallet">
                                    <option value="">اختر المحفظة</option>
                                    ${walletOptions}
                                </select>
                            </label>
                            <div class="oad-refund-wallet-details" data-refund-wallet-details hidden>
                                <div>
                                    <span>المحفظة</span>
                                    <strong data-refund-wallet-name>—</strong>
                                </div>
                                <div>
                                    <span>رقم الحساب</span>
                                    <strong data-refund-wallet-account dir="ltr">—</strong>
                                </div>
                                <div>
                                    <span>اسم المستفيد</span>
                                    <strong data-refund-wallet-beneficiary>—</strong>
                                </div>
                            </div>
                            <div class="oad-cancel-refund__policy">
                                <i class="bi bi-shield-check"></i>
                                <span>سيحفظ النظام بيانات المحفظة المختارة مع طلب الاسترداد، وتظهر للإدارة عند تنفيذ التحويل.</span>
                            </div>` : ""}
                        <label class="oad-cancel-refund__field">
                            <span><i class="bi bi-chat-square-text"></i> سبب الإلغاء</span>
                            <textarea id="oadCancelReason" maxlength="500" placeholder="اكتب سبب الإلغاء بوضوح..."></textarea>
                        </label>
                    </div>`,
                showConfirmButton: true,
                showCancelButton: true,
                confirmButtonText: "تأكيد الإلغاء",
                cancelButtonText: "تراجع",
                reverseButtons: true,
                customClass: { popup: "oad-confirm-popup oad-confirm-popup--refund", confirmButton: "oad-confirm-danger", cancelButton: "oad-confirm-cancel" },
                buttonsStyling: false,
                preConfirm: () => {
                    const popup = Swal.getPopup();
                    const reason = String(popup?.querySelector("#oadCancelReason")?.value || "").trim();
                    const walletId = Number(popup?.querySelector("#oadRefundOwnerWallet")?.value || 0);
                    if (reason.length < 5) {
                        Swal.showValidationMessage("اكتب سبب الإلغاء بوضوح، خمسة أحرف على الأقل.");
                        return false;
                    }
                    if (needsRefundWallet && !walletId) {
                        Swal.showValidationMessage("اختر المحفظة التي تريد استلام مبلغ الاسترداد عليها.");
                        return false;
                    }
                    return { reason, walletId };
                }
            });

            if (!result.isConfirmed || !result.value) return;
            return postState(app.dataset.cancelUrl, {
                AdvertisementId: id,
                RowVersion: details?.rowVersion || rowVersion,
                Reason: result.value.reason,
                RefundOwnerWalletId: result.value.walletId || ""
            });
        }
    }

    function syncRefundWalletDetails(select) {
        if (!select) return;
        const popup = select.closest(".oad-confirm-popup") || Swal.getPopup();
        const panel = popup?.querySelector("[data-refund-wallet-details]");
        if (!panel) return;

        const option = select.options?.[select.selectedIndex];
        const hasValue = Boolean(option && option.value);
        panel.hidden = !hasValue;
        if (!hasValue) return;

        text(panel, "[data-refund-wallet-name]", option.dataset.walletName || "—");
        text(panel, "[data-refund-wallet-account]", option.dataset.accountNumber || "—");
        text(panel, "[data-refund-wallet-beneficiary]", option.dataset.beneficiaryName || "—");
    }

    async function postState(url, values) {
        try {
            const fd = new FormData();
            Object.entries(values).forEach(([k, v]) => fd.append(k, v ?? ""));
            const response = await postForm(url, fd);
            const result = await readJson(response);
            if (!response.ok || !result.success) throw new Error(result.message || "تعذر تنفيذ العملية.");
            Swal.close();
            toast(result.message || "تمت العملية.", "success");
            await refreshAfterChange();
        } catch (error) {
            toast(error.message || "تعذر تنفيذ العملية.", "error");
        }
    }

    async function openPayment(id, sourceWizardRoot = null) {
        try {
            const response = await fetch(`${app.dataset.paymentUrl}?id=${encodeURIComponent(id)}`, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) throw new Error("تعذر تحميل بيانات الدفع.");
            const data = await response.json();
            const html = document.getElementById("oad-payment-template").innerHTML;
            await Swal.fire({
                html,
                showConfirmButton: false,
                showCloseButton: true,
                width: "min(760px, calc(100vw - 20px))",
                customClass: { popup: "oad-swal-shell oad-swal-payclean" },
                didOpen: popup => hydratePayment(popup, data)
            });
        } catch (error) {
            if (sourceWizardRoot) {
                hideWizardConfirmation(sourceWizardRoot);
                showWizardError(sourceWizardRoot, error.message || "تعذر فتح الدفع.");
            } else {
                toast(error.message || "تعذر فتح الدفع.", "error");
            }
        }
    }

    function hydratePayment(root, data) {
        const d = data.details;
        const form = $("[data-payment-form]", root);
        form.elements.AdvertisementId.value = d.advertisementId;
        form.elements.RowVersion.value = d.rowVersion;
        form.elements.TransferredAmount.value = toEnglishDigits(d.expectedPaymentAmount || "");
        form.elements.TransferDate.value = new Date().toISOString().slice(0, 10);
        text(root, "[data-pay-title]", d.advertisementTitle);
        text(root, "[data-pay-amount]", d.expectedPaymentText || "—");
        text(root, "[data-pay-billing]", d.billingModelLabel || "—");

        const wallets = $("[data-wallets]", root);
        wallets.innerHTML = (data.wallets || []).map((w, index) => `
            <label class="oad-pm-wallet">
                <input type="radio" name="PlatformWalletId" value="${w.platformWalletId}" ${index === 0 || w.isPrimary ? "checked" : ""}>
                <span class="oad-pm-wallet__logo">
                    ${w.logoPath ? `<img src="${attr(w.logoPath)}" alt="">` : `<i class="bi bi-wallet2"></i>`}
                </span>
                <span class="oad-pm-wallet__body">
                    <strong>${esc(w.walletName)}</strong>
                    <small>${esc(w.accountNumber || "")}</small>
                    <em>${esc(w.beneficiaryName || "")}</em>
                </span>
                <span class="oad-pm-wallet__check"><i class="bi bi-check-lg"></i></span>
            </label>`).join("");

        text(root, "[data-pay-footer-amount]", d.expectedPaymentText || "—");

        const immediateStart = d.startImmediately === true;
        const policyText = immediateStart
            ? "تراجع الإدارة إثبات الدفع، وإذا وافقت على الإعلان يبدأ العرض مباشرة. بعد تأكيد وإرسال الدفع يكون المبلغ غير قابل للاسترداد."
            : "تراجع الإدارة إثبات الدفع، ويبدأ الإعلان في التاريخ والوقت اللذين حددتهما. يمكنك استرداد المبلغ قبل بدء عرض الإعلان؛ بعد بدء العرض لا يمكن الاسترداد.";
        const confirmText = immediateStart
            ? "بعد التأكيد سيتم إرسال إثبات الدفع والإعلان إلى الإدارة للمراجعة. لأن البداية ستكون مباشرة بعد موافقة الإدارة، لن تتمكن من استرداد المبلغ بعد تأكيد الدفع."
            : "بعد التأكيد سيتم إرسال إثبات الدفع والإعلان إلى الإدارة للمراجعة. يمكنك استرداد المبلغ ما دام عرض الإعلان لم يبدأ بعد؛ بعد بدء العرض لا يمكن الاسترداد.";

        text(root, "[data-pay-policy-text]", policyText);
        text(root, "[data-payment-confirm-text]", confirmText);
    }

    async function submitPayment(form) {
        clearPaymentError(form);

        const selected = form.querySelector('[name="PlatformWalletId"]:checked');
        if (!selected) {
            return showPaymentError(form, "اختر محفظة الدفع.");
        }

        const amount = Number(form.elements.TransferredAmount.value || 0);
        if (!Number.isFinite(amount) || amount <= 0) {
            return showPaymentError(form, "أدخل مبلغًا محولًا صحيحًا.", form.elements.TransferredAmount);
        }

        const reference = (form.elements.ReferenceNumber.value || "").trim();
        if (reference.length < 3) {
            return showPaymentError(form, "اكتب رقم الحوالة بصورة صحيحة.", form.elements.ReferenceNumber);
        }

        if (!String(form.elements.TransferDate.value || "").trim()) {
            return showPaymentError(form, "حدد تاريخ الحوالة.", form.elements.TransferDate);
        }

        if (!form.elements.receiptImage.files?.[0]) {
            return showPaymentError(form, "ارفع صورة إثبات الدفع.", form.elements.receiptImage);
        }

        const panel = $("[data-payment-confirm]", form);
        if (panel) {
            panel.hidden = false;
            panel.classList.add("is-visible");
        }
    }

    function showPaymentError(form, message, field = null) {
        if (!form) return;

        const box = $("[data-payment-error]", form);
        const textEl = $("[data-payment-error-text]", form);

        if (box) {
            box.hidden = false;
            box.classList.add("is-visible");
        }

        if (textEl) textEl.textContent = message;

        $$(".is-invalid", form).forEach(x => x.classList.remove("is-invalid"));

        if (field) {
            field.classList?.add("is-invalid");
            try { field.focus({ preventScroll: true }); } catch { field.focus?.(); }
            field.scrollIntoView?.({ behavior: "smooth", block: "center" });
        } else {
            box?.scrollIntoView?.({ behavior: "smooth", block: "nearest" });
        }
    }

    function clearPaymentError(form) {
        if (!form) return;

        const box = $("[data-payment-error]", form);
        if (box) {
            box.hidden = true;
            box.classList.remove("is-visible");
        }

        $$(".is-invalid", form).forEach(x => x.classList.remove("is-invalid"));
    }

    function hidePaymentConfirmation(form) {
        if (!form) return;

        const panel = $("[data-payment-confirm]", form);
        if (panel) {
            panel.classList.remove("is-visible");
            panel.hidden = true;
        }
    }

    async function sendPayment(form) {
        if (!form) return;

        const button = $("[data-payment-confirm-send]", form);
        const fd = new FormData(form);

        try {
            busy(button, true);

            const response = await postForm(app.dataset.paymentSaveUrl, fd);
            const result = await readJson(response);

            if (!response.ok || !result.success) {
                throw new Error(result.message || "تعذر حفظ إثبات الدفع.");
            }

            Swal.close();
            toast(
                result.message || "تم إرسال الإعلان وإثبات الدفع للإدارة للمراجعة.",
                "success"
            );

            await refreshAfterChange();
        } catch (error) {
            hidePaymentConfirmation(form);
            showPaymentError(form, error.message || "تعذر حفظ إثبات الدفع.");
        } finally {
            busy(button, false);
        }
    }

    async function refreshAfterChange() {
        const form = $("[data-filter-form]", app);
        if (form) await loadAds(form);
        try {
            const response = await fetch(app.dataset.overviewUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                cache: "no-store"
            });
            if (response.ok) {
                const d = await response.json();
                text(app, "[data-overview-active]", d.activeAdvertisements);
                text(app, "[data-overview-action]", (d.draftAdvertisements || 0) + (d.awaitingPaymentAdvertisements || 0) + (d.pendingAdvertisements || 0) + (d.rejectedAdvertisements || 0));
                text(app, "[data-overview-impressions]", num(d.impressionsCount));
                text(app, "[data-overview-clicks]", num(d.clicksCount));
                ensureOverviewBookingChart();
                renderOverviewBookingChart({
                    starts: d.bookingStartsCount,
                    conversions: d.conversionsCount
                });
            }
        } catch { }
    }

    function renderOverviewBookingChart(values = null) {
        ensureOverviewBookingChart();

        const chart = $("[data-booking-performance-chart]", app);
        if (!chart) return;

        const rows = $$("[data-booking-performance-row]", chart);
        if (!rows.length) return;

        if (values) {
            const normalized = {
                starts: Math.max(0, Number(values.starts || 0)),
                conversions: Math.max(0, Number(values.conversions || 0))
            };

            rows.forEach(row => {
                const metric = row.dataset.metric;
                if (!metric || !(metric in normalized)) return;

                const value = normalized[metric];
                row.dataset.value = String(value);
                text(
                    row,
                    `[data-booking-performance-value="${metric}"]`,
                    num(value)
                );
            });
        }

        const maxValue = Math.max(
            1,
            ...rows.map(row => Math.max(0, Number(row.dataset.value || 0)))
        );

        rows.forEach(row => {
            const value = Math.max(0, Number(row.dataset.value || 0));
            const bar = $("[data-booking-performance-bar]", row);
            if (!bar) return;

            const width = value <= 0
                ? 0
                : Math.max(6, Math.min(100, value * 100 / maxValue));

            bar.style.width = `${width}%`;
            row.classList.toggle("is-zero", value <= 0);
        });
    }

    async function confirmBox(title, textValue, confirmText) {
        const result = await Swal.fire({
            title,
            text: textValue,
            icon: "question",
            showConfirmButton: true,
            showCancelButton: true,
            confirmButtonText: confirmText,
            cancelButtonText: "تراجع",
            reverseButtons: true,
            customClass: { popup: "oad-confirm-popup", confirmButton: "oad-confirm-primary", cancelButton: "oad-confirm-cancel" },
            buttonsStyling: false
        });
        return result.isConfirmed;
    }

    function toEnglishDigits(value) {
        return String(value ?? "")
            .replace(/[٠-٩]/g, digit => "٠١٢٣٤٥٦٧٨٩".indexOf(digit))
            .replace(/[۰-۹]/g, digit => "۰۱۲۳۴۵۶۷۸۹".indexOf(digit));
    }

    async function postForm(url, formData) {
        return fetch(url, {
            method: "POST",
            headers: { "RequestVerificationToken": token, "X-Requested-With": "XMLHttpRequest" },
            body: formData
        });
    }

    async function readJson(response) {
        try { return await response.json(); } catch { return { success: false, message: "استجابة غير صالحة من الخادم." }; }
    }

    function setValue(form, name, value) {
        const field = form.elements.namedItem(name);
        if (field && !(field instanceof RadioNodeList)) field.value = value ?? "";
    }

    function busy(target, value) {
        if (!target) return;
        target.classList?.toggle("is-busy", value);
        $$("button,input,select,textarea", target).forEach(x => x.disabled = value);
        if (target.tagName === "BUTTON") target.disabled = value;
    }

    function toast(message, icon = "info") {
        Swal.fire({
            toast: true,
            position: "top-start",
            icon,
            title: message,
            showConfirmButton: false,
            timer: icon === "error" ? 5000 : 3000,
            timerProgressBar: true,
            customClass: { popup: "oad-toast" }
        });
    }

    function text(root, selector, value) {
        const el = $(selector, root);
        if (el) el.textContent = value ?? "—";
    }

    function num(value) {
        const n = Number(value || 0);
        return Number.isFinite(n) ? new Intl.NumberFormat("ar-YE").format(n) : "0";
    }

    function formatDate(value) {
        if (!value) return "—";
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? "—" : d.toLocaleString("ar-YE");
    }

    function formatDateOnly(value) {
        if (!value) return "—";
        const d = new Date(value);
        return Number.isNaN(d.getTime())
            ? "—"
            : d.toLocaleDateString("ar-YE", { year: "numeric", month: "2-digit", day: "2-digit" });
    }

    function formatTimeOnly(value) {
        if (!value) return "—";
        const d = new Date(value);
        return Number.isNaN(d.getTime())
            ? "—"
            : d.toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" });
    }

    function esc(value) {
        return String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    }

    function attr(value) {
        return esc(value).replace(/`/g, "&#96;");
    }

    function slug(value) {
        return String(value || "").replace(/[^a-z0-9_-]/gi, "").toLowerCase();
    }
})();