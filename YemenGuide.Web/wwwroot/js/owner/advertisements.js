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

            // عند اختيار «تاريخ مستقبلي» ننتظر إطار رسم واحد حتى يصبح الحقل ظاهرًا فعليًا،
            // ثم نعطيه التركيز ونفتح منتقي التاريخ إذا كان المتصفح يدعمه.
            if (event.target.value === "false") {
                const field = $('[name="RequestedStartAt"]', root);
                if (field) {
                    requestAnimationFrame(() => {
                        field.disabled = false;
                        field.removeAttribute("disabled");
                        field.readOnly = false;
                        field.removeAttribute("readonly");
                        field.style.setProperty("pointer-events", "auto", "important");
                        field.style.opacity = "1";
                        field.style.cursor = "pointer";
                        field.focus({ preventScroll: true });

                        if (typeof field.showPicker === "function") {
                            try { field.showPicker(); } catch { /* يبقى الحقل مفعّلًا ويمكن اختيار التاريخ يدويًا. */ }
                        }
                    });
                }
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
                headers: { "X-Requested-With": "XMLHttpRequest" }
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
                            <span class="oad-payment is-${slug(item.paymentStatus)}">${esc(item.paymentStatusLabel)}</span>
                        </div>
                        ${alert}
                        <div class="oad-ad-metrics">
                            <div class="is-strength">
                                <small>نسبة الظهور</small>
                                <strong>${num(item.displayWeight)}%</strong>
                            </div>
                            <div><small>نقرات</small><strong>${num(item.clicksCount)}</strong></div>
                            <div><small>حجوزات</small><strong>${num(item.conversionsCount)}</strong></div>
                            <div class="is-unique-views">
                                <small>عدد المشاهدات</small>
                                <strong>${num(item.uniqueImpressionsCount)}</strong>
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
                if (!field?.value) {
                    return invalid("حدد تاريخ ووقت البداية المطلوب.", field);
                }

                const requested = new Date(field.value);
                if (Number.isNaN(requested.getTime()) || requested <= new Date()) {
                    return invalid("تاريخ البداية يجب أن يكون في المستقبل.", field);
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

    function ensureFutureDateField(root = $(".oad-swal-builder")) {
        if (!root) return null;

        const form = $("[data-wizard-form]", root);
        if (!form) return null;

        const field = form.querySelector('input[name="RequestedStartAt"]');
        const box = field?.closest("[data-start-date]") || $("[data-start-date]", root);

        if (!field || !box) return null;

        return { box, field };
    }

    function syncStartUI(root = $(".oad-swal-builder")) {
        if (!root) return;

        const form = $("[data-wizard-form]", root);
        if (!form) return;

        const selected = form.querySelector('[name="StartImmediately"]:checked');
        const future = selected?.value === "false";

        const ensured = ensureFutureDateField(root);
        const box = ensured?.box;
        const field = ensured?.field;

        if (box) {
            box.classList.toggle("is-visible", future);
            box.setAttribute("aria-hidden", future ? "false" : "true");
        }

        if (!field) return;

        field.required = future;
        field.disabled = !future;
        field.readOnly = false;

        if (!future) {
            field.removeAttribute("min");
            return;
        }

        field.removeAttribute("disabled");
        field.removeAttribute("readonly");

        const now = new Date();
        now.setSeconds(0, 0);
        const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
            .toISOString()
            .slice(0, 16);

        field.min = local;
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
        text(root, "[data-d-start]", formatDate(d.startAt || d.requestedStartAt));
        text(root, "[data-d-end]", formatDate(d.endAt));

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
        return `<button type="button" class="oad-btn oad-btn--${mode}" data-owner-action="${action}" data-ad-id="${d.advertisementId}" data-row-version="${attr(d.rowVersion)}"><i class="bi ${icon}"></i>${label}</button>`;
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

        if (action === "pause" || action === "cancel") {
            const title = action === "pause" ? "إيقاف الإعلان مؤقتًا" : "إلغاء الإعلان";
            const result = await Swal.fire({
                title,
                text: action === "pause" ? "يمكنك استئنافه لاحقًا." : "اكتب سبب الإلغاء.",
                input: "textarea",
                inputPlaceholder: "السبب...",
                showConfirmButton: true,
                showCancelButton: true,
                confirmButtonText: action === "pause" ? "إيقاف" : "تأكيد الإلغاء",
                cancelButtonText: "تراجع",
                reverseButtons: true,
                customClass: { popup: "oad-confirm-popup", confirmButton: action === "cancel" ? "oad-confirm-danger" : "oad-confirm-primary", cancelButton: "oad-confirm-cancel" },
                buttonsStyling: false,
                inputValidator: value => action === "cancel" && !String(value || "").trim() ? "اكتب السبب." : undefined
            });
            if (!result.isConfirmed) return;
            return postState(action === "pause" ? app.dataset.pauseUrl : app.dataset.cancelUrl, {
                AdvertisementId: id, RowVersion: rowVersion, Reason: String(result.value || "").trim()
            });
        }
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
            const response = await fetch(app.dataset.overviewUrl, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (response.ok) {
                const d = await response.json();
                text(app, "[data-overview-active]", d.activeAdvertisements);
                text(app, "[data-overview-action]", (d.draftAdvertisements || 0) + (d.awaitingPaymentAdvertisements || 0) + (d.rejectedAdvertisements || 0) + (d.paymentRejectedAdvertisements || 0));
                text(app, "[data-overview-impressions]", num(d.impressionsCount));
                text(app, "[data-overview-clicks]", num(d.clicksCount));
            }
        } catch { }
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