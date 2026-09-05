(function (window, document) {
    "use strict";

    const endpoints = Object.freeze({
        add: "/Owner/Place/AddPromotion",
        edit: "/Owner/Place/EditPromotion",
        toggle: "/Owner/Place/TogglePromotionStatus",
        remove: "/Owner/Place/DeletePromotion"
    });

    function initPromotionsManager() {
        const root = document.getElementById("promotionManager");
        if (!root || root.dataset.initialized === "true") return;
        root.dataset.initialized = "true";

        const template = root.querySelector("#promotionFormTemplate");
        const openButton = root.querySelector("#openPromotionModal");
        const searchInput = root.querySelector("#promotionSearch");
        const filterButtons = Array.from(root.querySelectorAll("[data-promo-filter]"));
        const cards = Array.from(root.querySelectorAll(".yg-promo-card"));
        const emptyState = root.querySelector("#promotionFilteredEmpty");
        const token = root.querySelector('#promotionTokenForm input[name="__RequestVerificationToken"]')?.value || "";
        const promotions = readJson(root.querySelector("#promotionManagerData"));
        const promotionMap = new Map(promotions.map(item => [Number(item.promotionId), item]));

        let activeFilter = chooseInitialFilter(filterButtons);

        filterButtons.forEach(button => {
            button.classList.toggle("is-active", button.dataset.promoFilter === activeFilter);
            button.addEventListener("click", function () {
                activeFilter = this.dataset.promoFilter || "active";
                filterButtons.forEach(item => item.classList.toggle("is-active", item === this));
                filterCards();
            });
        });

        searchInput?.addEventListener("input", filterCards);

        openButton?.addEventListener("click", function () {
            openPromotionDialog(null);
        });

        root.querySelectorAll("[data-open-promotion-from-empty]").forEach(button => {
            button.addEventListener("click", function () {
                openPromotionDialog(null);
            });
        });

        root.querySelectorAll(".yg-promo-edit-button").forEach(button => {
            button.addEventListener("click", function () {
                const item = promotionMap.get(Number(this.dataset.promotionId));
                if (item) openPromotionDialog(item);
            });
        });

        root.querySelectorAll(".yg-promo-status-button").forEach(button => {
            button.addEventListener("click", function () {
                togglePromotion(this);
            });
        });

        root.querySelectorAll(".yg-promo-delete-button").forEach(button => {
            button.addEventListener("click", function () {
                deletePromotion(this);
            });
        });

        filterCards();
        openPromotionFromNotification();

        function openPromotionFromNotification() {
            try {
                const url = new URL(window.location.href);
                const promotionId = Number.parseInt(url.searchParams.get("promotionId") || "0", 10) || 0;
                if (promotionId <= 0) return;

                const item = promotionMap.get(promotionId);
                if (!item) return;

                window.setTimeout(() => openPromotionDialog(item), 90);
                url.searchParams.delete("promotionId");
                url.searchParams.delete("placeTab");
                window.history.replaceState(window.history.state, "", `${url.pathname}${url.search}${url.hash}`);
            } catch {
                // لا شيء.
            }
        }

        async function openPromotionDialog(item) {
            if (!template) return;

            if (!window.Swal) {
                window.alert("تعذر فتح نافذة العرض لأن مكتبة SweetAlert2 غير محملة.");
                return;
            }

            const holder = document.createElement("div");
            holder.appendChild(template.content.cloneNode(true));

            await window.Swal.fire({
                html: holder.innerHTML,
                showConfirmButton: false,
                showCancelButton: false,
                showCloseButton: false,
                buttonsStyling: false,
                width: "min(980px, calc(100vw - 20px))",
                padding: 0,
                background: "#ffffff",
                allowOutsideClick: false,
                allowEscapeKey: false,
                returnFocus: false,
                heightAuto: false,
                customClass: {
                    popup: "yg-promo-swal-popup",
                    htmlContainer: "yg-promo-swal-html"
                },
                didOpen: popup => initializePromotionForm(popup, item)
            });
        }

        function initializePromotionForm(popup, item) {
            const form = popup.querySelector("#promotionForm");
            if (!form) return;

            const ui = {
                title: form.querySelector("#promotionDialogTitle"),
                alert: form.querySelector("#promotionFormAlert"),
                previous: form.querySelector("#promotionPreviousStep"),
                next: form.querySelector("#promotionNextStep"),
                publish: form.querySelector("#publishPromotion"),
                draft: form.querySelector("#savePromotionDraft"),
                close: form.querySelector("[data-close-promotion-dialog]"),
                steps: Array.from(form.querySelectorAll("[data-promo-step]")),
                stepButtons: Array.from(form.querySelectorAll("[data-step-jump]")),
                body: form.querySelector(".yg-promo-wizard__body"),
                footer: form.querySelector(".yg-promo-wizard__footer"),
                discountBuilder: form.querySelector("#promotionDiscountBuilder"),
                rewardBuilder: form.querySelector("#promotionRewardBuilder"),
                summaryText: form.querySelector("#promotionLiveSummaryText"),
                freeQuantityCondition: form.querySelector("#promotionFreeQuantityCondition"),
                rewardRepeatableField: form.querySelector("#promotionRewardRepeatableField"),
                rewardRepeatable: form.querySelector("#promotionRewardRepeatable")
            };

            let currentStep = 1;
            let busy = false;

            const isMobilePromotionSheet = window.matchMedia("(max-width: 768px)").matches;
            if (isMobilePromotionSheet && ui.footer) {
                popup.classList.add("yg-promo-swal-popup--external-footer");
                popup.appendChild(ui.footer);

                if (ui.publish) {
                    ui.publish.setAttribute("form", "promotionForm");
                }
            }

            resetForm();
            if (item) fillForm(item);
            if (ui.title) ui.title.textContent = item ? "تعديل العرض" : "إضافة عرض جديد";

            ui.close?.addEventListener("click", function () {
                if (!busy) window.Swal.close();
            });

            ui.previous?.addEventListener("click", function () {
                showStep(currentStep - 1);
            });

            ui.next?.addEventListener("click", function () {
                if (validateStep(currentStep)) showStep(currentStep + 1);
            });

            ui.stepButtons.forEach(button => {
                button.addEventListener("click", function () {
                    const target = Number(this.dataset.stepJump || 1);
                    if (target <= currentStep || validateThrough(target - 1)) {
                        showStep(target);
                    }
                });
            });

            form.querySelectorAll('input[name="TargetType"]').forEach(input => {
                input.addEventListener("change", function () {
                    syncTargetFields();
                    updateLiveSummary();
                });
            });

            form.querySelectorAll('input[name="OfferMode"]').forEach(input => {
                input.addEventListener("change", function () {
                    syncOfferMode();
                    updateLiveSummary();
                });
            });

            form.querySelectorAll('input[name="RewardType"]').forEach(input => {
                input.addEventListener("change", function () {
                    syncRewardFields();
                    updateLiveSummary();
                });
            });

            form.querySelectorAll('input[name="DiscountType"]').forEach(input => {
                input.addEventListener("change", function () {
                    syncDiscountFields();
                    updateLiveSummary();
                });
            });

            form.querySelectorAll('input[name="IsAutomatic"]').forEach(input => {
                input.addEventListener("change", syncApplyMode);
            });

            [
                "promotionDiscountValue",
                "promotionRewardDiscountValue",
                "promotionRewardQuantity",
                "promotionRewardRepeatable",
                "promotionRequiredQuantity",
                "promotionMinimumNights",
                "promotionMinimumPeople",
                "promotionMinimumBooking",
                "promotionSpecialPrice",
                "promotionRewardServiceId",
                "promotionRoomId",
                "promotionPackageId",
                "promotionServiceId"
            ].forEach(id => {
                const control = form.querySelector(`#${id}`);
                control?.addEventListener("input", updateLiveSummary);
                control?.addEventListener("change", updateLiveSummary);
            });

            ui.draft?.addEventListener("click", function () {
                submitPromotion("Draft");
            });

            form.addEventListener("submit", function (event) {
                event.preventDefault();
                submitPromotion("Publish");
            });

            window.setTimeout(() => form.querySelector("#promotionTitle")?.focus(), 100);

            function resetForm() {
                form.reset();
                setValue("promotionId", "0");
                setValue("promotionSaveMode", "Draft");
                setValue("promotionRemoveImage", "false");

                checkRadio("TargetType", "Place");
                checkRadio("OfferMode", "Discount");
                checkRadio("DiscountType", "Percentage");
                checkRadio("IsAutomatic", "true");
                if (ui.rewardRepeatable) ui.rewardRepeatable.checked = false;

                form.querySelectorAll(".yg-promo-day-options input").forEach(input => {
                    input.checked = false;
                });

                setDefaultDates();
                hideAlert();
                syncTargetFields();
                syncOfferMode();
                syncDiscountFields();
                syncApplyMode();
                syncRewardFields();
                showStep(1);
            }

            function fillForm(data) {
                setValue("promotionId", data.promotionId);
                setValue("promotionTitle", data.promotionTitle);
                setValue("promotionShortDescription", data.shortDescription);
                setValue("promotionDescription", data.description);

                setValue("promotionDiscountValue", data.discountValue);
                setValue("promotionRewardDiscountValue", data.discountValue);
                setValue("promotionMaximumDiscount", data.maximumDiscountAmount);
                setValue("promotionMinimumBooking", data.minimumBookingAmount);
                setValue("promotionRequiredQuantity", data.requiredQuantity);
                setValue("promotionRewardQuantity", data.rewardQuantity);
                if (ui.rewardRepeatable) ui.rewardRepeatable.checked = data.isRewardRepeatable === true;
                setValue("promotionRewardServiceId", data.rewardServiceId);
                setValue("promotionSpecialPrice", data.specialPrice);

                setValue("promotionStartDate", toDateInput(data.startDate));
                setValue("promotionEndDate", toDateInput(data.endDate));
                setValue("promotionStayStartDate", toDateInput(data.stayStartDate));
                setValue("promotionStayEndDate", toDateInput(data.stayEndDate));
                setValue("promotionMinimumNights", data.minimumNights);
                setValue("promotionMinimumPeople", data.minimumPeople);
                setValue("promotionTotalUsage", data.totalUsageLimit);
                setValue("promotionPerCustomerUsage", data.perCustomerUsageLimit);
                setValue("promotionRoomId", data.roomId);
                setValue("promotionPackageId", data.packageId);
                setValue("promotionServiceId", data.serviceId);
                setValue("promotionCode", data.promoCode);

                const stayDetails = form.querySelector("#promotionStayRangeDetails");
                if (stayDetails && (data.stayStartDate || data.stayEndDate)) {
                    stayDetails.open = true;
                }

                checkRadio("TargetType", data.targetType || "Place");
                checkRadio("OfferMode", data.offerMode || "Discount");
                checkRadio("DiscountType", data.discountType || "Percentage");
                if (data.rewardType) checkRadio("RewardType", data.rewardType);
                checkRadio("IsAutomatic", String(data.isAutomatic !== false));

                const selectedDays = String(data.applicableDaysCsv || "")
                    .split(",")
                    .map(day => day.trim())
                    .filter(Boolean);

                form.querySelectorAll(".yg-promo-day-options input").forEach(input => {
                    input.checked = selectedDays.includes(input.value);
                });

                syncTargetFields();
                syncOfferMode();
                syncDiscountFields();
                syncRewardFields();
                syncApplyMode();
                updateLiveSummary();
            }

            function showStep(step, preserveAlert) {
                currentStep = Math.max(1, Math.min(3, step));

                ui.steps.forEach(section => {
                    section.classList.toggle(
                        "is-active",
                        Number(section.dataset.promoStep) === currentStep
                    );
                });

                ui.stepButtons.forEach(button => {
                    const number = Number(button.dataset.stepJump || 1);
                    button.classList.toggle("is-active", number === currentStep);
                    button.classList.toggle("is-complete", number < currentStep);
                });

                setControlVisible(ui.previous, currentStep > 1);
                setControlVisible(ui.next, currentStep < 3);
                setControlVisible(ui.publish, currentStep === 3);
                setControlVisible(ui.draft, currentStep === 3);

                if (!preserveAlert) hideAlert();
                ui.body?.scrollTo({ top: 0, behavior: "smooth" });
            }

            function setControlVisible(element, visible) {
                if (!element) return;
                element.hidden = !visible;
                element.style.display = visible ? "inline-flex" : "none";
                element.setAttribute("aria-hidden", visible ? "false" : "true");
            }

            function validateThrough(lastStep) {
                for (let step = 1; step <= lastStep; step += 1) {
                    if (!validateStep(step)) {
                        showStep(step, true);
                        return false;
                    }
                }
                return true;
            }

            function validateStep(step) {
                hideAlert();

                if (step === 1) {
                    const title = form.querySelector("#promotionTitle");
                    if (!title || title.value.trim().length < 3) {
                        return invalidate(title, "اكتب اسمًا واضحًا للعرض لا يقل عن ثلاثة أحرف.");
                    }
                }

                if (step === 2) {
                    const target = fieldValue("TargetType") || "Place";
                    const targetSelect =
                        target === "Room" ? form.querySelector("#promotionRoomId") :
                            target === "Package" ? form.querySelector("#promotionPackageId") :
                                target === "Service" ? form.querySelector("#promotionServiceId") :
                                    null;

                    if (targetSelect && !targetSelect.value) {
                        return invalidate(targetSelect, "اختر العنصر الذي سيطبق عليه العرض.");
                    }

                    const offerMode = fieldValue("OfferMode") || "Discount";

                    if (offerMode === "Discount") {
                        const discount = form.querySelector("#promotionDiscountValue");
                        const discountValue = Number(discount?.value || 0);

                        if (!discountValue || discountValue <= 0) {
                            return invalidate(discount, "أدخل قيمة خصم أكبر من صفر.");
                        }

                        if (fieldValue("DiscountType") === "Percentage" && discountValue > 100) {
                            return invalidate(discount, "نسبة الخصم لا يمكن أن تتجاوز 100%.");
                        }
                    } else {
                        const rewardType = fieldValue("RewardType");
                        if (!rewardType) {
                            return invalidate(
                                form.querySelector('input[name="RewardType"]'),
                                "اختر المكافأة التي سيحصل عليها العميل."
                            );
                        }

                        if (rewardType === "PercentageDiscount" || rewardType === "FixedDiscount") {
                            const value = form.querySelector("#promotionRewardDiscountValue");
                            const amount = Number(value?.value || 0);
                            if (!amount || amount <= 0) {
                                return invalidate(value, "أدخل قيمة المكافأة.");
                            }
                            if (rewardType === "PercentageDiscount" && amount > 100) {
                                return invalidate(value, "نسبة الخصم لا يمكن أن تتجاوز 100%.");
                            }
                        }

                        if (rewardType === "FreeQuantity") {
                            const quantity = form.querySelector("#promotionRewardQuantity");
                            if (Number(quantity?.value || 0) <= 0) {
                                return invalidate(quantity, "حدد الكمية المجانية.");
                            }

                            const hasCondition =
                                Number(form.querySelector("#promotionRequiredQuantity")?.value || 0) > 0 ||
                                Number(form.querySelector("#promotionMinimumNights")?.value || 0) > 0 ||
                                Number(form.querySelector("#promotionMinimumPeople")?.value || 0) > 0 ||
                                Number(form.querySelector("#promotionMinimumBooking")?.value || 0) > 0;

                            if (!hasCondition) {
                                return invalidate(
                                    form.querySelector("#promotionRequiredQuantity") ||
                                    form.querySelector("#promotionMinimumNights"),
                                    "حدد شرط الحصول على المجاني: عدد الليالي المطلوبة أو الكمية المطلوبة على الأقل."
                                );
                            }
                        }

                        if (rewardType === "FreeService" || rewardType === "ServiceSpecialPrice") {
                            const service = form.querySelector("#promotionRewardServiceId");
                            if (!service?.value) {
                                return invalidate(service, "اختر خدمة المكافأة.");
                            }
                        }

                        if (rewardType === "SpecialPrice" || rewardType === "ServiceSpecialPrice") {
                            const price = form.querySelector("#promotionSpecialPrice");
                            if (Number(price?.value || 0) <= 0) {
                                return invalidate(price, "أدخل السعر الخاص.");
                            }
                        }
                    }

                    if (fieldValue("IsAutomatic") === "false") {
                        const code = form.querySelector("#promotionCode");
                        if (!code || !code.value.trim()) {
                            return invalidate(code, "اكتب كود العرض أو اختر التطبيق التلقائي.");
                        }
                    }
                }

                if (step === 3) {
                    const start = form.querySelector("#promotionStartDate");
                    const end = form.querySelector("#promotionEndDate");

                    if (!start?.value) return invalidate(start, "حدد تاريخ بداية العرض.");
                    if (!end?.value) return invalidate(end, "حدد تاريخ نهاية العرض.");
                    if (end.value < start.value) {
                        return invalidate(end, "تاريخ النهاية يجب أن يكون بعد البداية أو مساويًا لها.");
                    }

                    const stayStart = form.querySelector("#promotionStayStartDate");
                    const stayEnd = form.querySelector("#promotionStayEndDate");

                    if ((stayStart?.value && !stayEnd?.value) || (!stayStart?.value && stayEnd?.value)) {
                        return invalidate(
                            stayStart?.value ? stayEnd : stayStart,
                            "حدد بداية ونهاية فترة الحجز المشمولة معًا، أو اتركهما فارغتين."
                        );
                    }

                    if (stayStart?.value && stayEnd?.value && stayEnd.value < stayStart.value) {
                        return invalidate(stayEnd, "نهاية فترة الحجز المشمولة يجب أن تكون بعد بدايتها.");
                    }

                    const total = numberOrNull("promotionTotalUsage");
                    const perCustomer = numberOrNull("promotionPerCustomerUsage");

                    if (total && perCustomer && perCustomer > total) {
                        return invalidate(
                            form.querySelector("#promotionPerCustomerUsage"),
                            "حد استخدام العميل لا يمكن أن يتجاوز الحد الإجمالي للعرض."
                        );
                    }
                }

                return true;
            }

            function invalidate(field, message) {
                if (!field) {
                    showAlert(message);
                    return false;
                }

                field.classList.add("is-invalid");

                const clearInvalid = function () {
                    field.classList.remove("is-invalid");
                    field.removeEventListener("input", clearInvalid);
                    field.removeEventListener("change", clearInvalid);
                };

                field.addEventListener("input", clearInvalid);
                field.addEventListener("change", clearInvalid);

                showAlert(message);
                field.focus?.();
                field.scrollIntoView?.({ block: "center", behavior: "smooth" });
                return false;
            }

            async function submitPromotion(saveMode) {
                if (busy) return;

                const requiredStep = saveMode === "Publish" ? 3 : currentStep;
                if (!validateThrough(requiredStep)) return;

                const id = Number(form.querySelector("#promotionId")?.value || 0);
                setValue("promotionSaveMode", saveMode);

                const days = Array.from(form.querySelectorAll(".yg-promo-day-options input:checked"))
                    .map(input => input.value)
                    .join(",");
                setValue("promotionApplicableDays", days);

                const offerMode = fieldValue("OfferMode") || "Discount";
                if (offerMode === "ConditionalReward") {
                    const rewardType = fieldValue("RewardType");
                    if (rewardType === "PercentageDiscount" || rewardType === "FixedDiscount") {
                        setValue(
                            "promotionDiscountValue",
                            form.querySelector("#promotionRewardDiscountValue")?.value || ""
                        );
                    } else {
                        setValue("promotionDiscountValue", "");
                    }
                }

                const data = new FormData(form);

                [
                    "DiscountValue",
                    "MaximumDiscountAmount",
                    "MinimumBookingAmount",
                    "MinimumNights",
                    "MinimumPeople",
                    "TotalUsageLimit",
                    "PerCustomerUsageLimit",
                    "RequiredQuantity",
                    "RewardQuantity",
                    "RewardServiceId",
                    "SpecialPrice",
                    "RoomId",
                    "PackageId",
                    "ServiceId",
                    "StayStartDate",
                    "StayEndDate"
                ].forEach(name => {
                    if (data.get(name) === "") data.delete(name);
                });

                setBusy(
                    true,
                    saveMode === "Publish" ? "جاري نشر العرض..." : "جاري حفظ المسودة..."
                );

                try {
                    const response = await fetch(id > 0 ? endpoints.edit : endpoints.add, {
                        method: "POST",
                        body: data,
                        headers: {
                            "X-Requested-With": "XMLHttpRequest",
                            "RequestVerificationToken": token
                        }
                    });

                    const result = await readResponse(response);
                    if (!response.ok || !result.success) {
                        const message = result?.message || `تعذر حفظ العرض (HTTP ${response.status}).`;
                        throw new Error(message);
                    }

                    window.Swal.close();
                    await notify("success", "تم الحفظ", result.message || "تم حفظ العرض بنجاح.");
                    reloadPromotions();
                } catch (error) {
                    showAlert(error.message || "حدث خطأ غير متوقع أثناء الحفظ.");
                } finally {
                    setBusy(false);
                }
            }

            function syncTargetFields() {
                const selected = fieldValue("TargetType") || "Place";

                form.querySelectorAll("[data-target-select]").forEach(group => {
                    group.hidden = group.dataset.targetSelect !== selected;
                });

                updateLiveSummary();
            }

            function syncOfferMode() {
                const conditional = fieldValue("OfferMode") === "ConditionalReward";

                if (ui.discountBuilder) ui.discountBuilder.hidden = conditional;
                if (ui.rewardBuilder) ui.rewardBuilder.hidden = !conditional;

                if (ui.freeQuantityCondition && !conditional) {
                    ui.freeQuantityCondition.hidden = true;
                }

                if (!conditional) {
                    form.querySelectorAll('input[name="RewardType"]').forEach(input => {
                        input.checked = false;
                    });
                    setValue("promotionRewardQuantity", "");
                    setValue("promotionRewardServiceId", "");
                    setValue("promotionSpecialPrice", "");
                    setValue("promotionRequiredQuantity", "");
                    setValue("promotionMinimumNights", "");
                    setValue("promotionMinimumPeople", "");
                    setValue("promotionMinimumBooking", "");
                    if (ui.rewardRepeatable) ui.rewardRepeatable.checked = false;
                }

                syncRewardFields();
                updateLiveSummary();
            }

            function syncDiscountFields() {
                const percentage = fieldValue("DiscountType") !== "FixedAmount";
                const valueLabel = form.querySelector("#promotionDiscountValueLabel");
                const valueInput = form.querySelector("#promotionDiscountValue");
                const maximumField = form.querySelector("#maximumDiscountField");
                const maximumInput = form.querySelector("#promotionMaximumDiscount");

                if (valueLabel) {
                    valueLabel.innerHTML = percentage
                        ? 'قيمة الخصم (%) <em>*</em>'
                        : 'قيمة الخصم (ريال) <em>*</em>';
                }

                if (valueInput) {
                    valueInput.placeholder = percentage ? "مثال: 20" : "مثال: 5000";
                    valueInput.max = percentage ? "100" : "";
                }

                if (maximumField) maximumField.hidden = !percentage;
                if (!percentage && maximumInput) maximumInput.value = "";

                updateLiveSummary();
            }

            function syncRewardFields() {
                const rewardType = fieldValue("RewardType");

                form.querySelectorAll("[data-reward-field]").forEach(field => {
                    field.hidden = true;
                });

                const show = key => {
                    const field = form.querySelector(`[data-reward-field="${key}"]`);
                    if (field) field.hidden = false;
                };

                if (rewardType === "PercentageDiscount" || rewardType === "FixedDiscount") {
                    show("discount");
                    const label = form.querySelector("#promotionRewardDiscountLabel");
                    const input = form.querySelector("#promotionRewardDiscountValue");

                    if (label) {
                        label.innerHTML = rewardType === "PercentageDiscount"
                            ? 'نسبة الخصم (%) <em>*</em>'
                            : 'قيمة الخصم (ريال) <em>*</em>';
                    }

                    if (input) {
                        input.max = rewardType === "PercentageDiscount" ? "100" : "";
                        input.placeholder = rewardType === "PercentageDiscount" ? "مثال: 15" : "مثال: 5000";
                    }
                }

                const isFreeQuantity = rewardType === "FreeQuantity";
                if (isFreeQuantity) {
                    show("rewardQuantity");
                    show("rewardRepeatable");
                }

                if (ui.freeQuantityCondition) {
                    ui.freeQuantityCondition.hidden = !isFreeQuantity;
                }

                if (!isFreeQuantity) {
                    setValue("promotionRequiredQuantity", "");
                    setValue("promotionMinimumNights", "");
                    setValue("promotionMinimumPeople", "");
                    setValue("promotionMinimumBooking", "");
                    if (ui.rewardRepeatable) ui.rewardRepeatable.checked = false;
                }

                if (rewardType === "FreeService") show("rewardService");
                if (rewardType === "SpecialPrice") show("specialPrice");

                if (rewardType === "ServiceSpecialPrice") {
                    show("rewardService");
                    show("specialPrice");
                }

                updateLiveSummary();
            }

            function syncApplyMode() {
                const automatic = fieldValue("IsAutomatic") !== "false";
                const codeField = form.querySelector("#promotionCodeField");
                const codeInput = form.querySelector("#promotionCode");

                if (codeField) codeField.hidden = automatic;
                if (automatic && codeInput) codeInput.value = "";
            }

            function updateLiveSummary() {
                if (!ui.summaryText) return;

                if (fieldValue("OfferMode") !== "ConditionalReward") {
                    ui.summaryText.textContent = "الخصم المباشر سيظهر للعميل تلقائيًا حسب الهدف والشروط المحددة.";
                    return;
                }

                const rewardType = fieldValue("RewardType");
                if (!rewardType) {
                    ui.summaryText.textContent = "اختر المكافأة وسيظهر وصف العرض هنا.";
                    return;
                }

                const conditionParts = [];
                const nights = numberOrNull("promotionMinimumNights");
                const requiredQuantity = numberOrNull("promotionRequiredQuantity");
                const people = numberOrNull("promotionMinimumPeople");
                const minimumBooking = numberOrNull("promotionMinimumBooking");

                if (nights) conditionParts.push(`احجز ${nights} ليلة`);
                if (requiredQuantity) conditionParts.push(`اختر كمية ${requiredQuantity}`);
                if (people) conditionParts.push(`لـ ${people} أشخاص أو أكثر`);
                if (minimumBooking) conditionParts.push(`بقيمة ${formatNumber(minimumBooking)} ريال أو أكثر`);

                let rewardText = "مكافأة خاصة";

                if (rewardType === "PercentageDiscount") {
                    const value = Number(form.querySelector("#promotionRewardDiscountValue")?.value || 0);
                    rewardText = value > 0 ? `خصم ${value}%` : "خصم بنسبة تحددها";
                } else if (rewardType === "FixedDiscount") {
                    const value = Number(form.querySelector("#promotionRewardDiscountValue")?.value || 0);
                    rewardText = value > 0 ? `خصم ${formatNumber(value)} ريال` : "خصم بمبلغ تحدده";
                } else if (rewardType === "FreeQuantity") {
                    const quantity = Number(form.querySelector("#promotionRewardQuantity")?.value || 0);
                    const repeatText = ui.rewardRepeatable?.checked ? " — ويتكرر العرض" : "";
                    rewardText = quantity > 0 ? `${quantity} مجانًا${repeatText}` : `كمية مجانية${repeatText}`;
                } else if (rewardType === "FreeService") {
                    rewardText = `${selectedText("promotionRewardServiceId", "خدمة إضافية")} مجانًا`;
                } else if (rewardType === "SpecialPrice") {
                    const price = Number(form.querySelector("#promotionSpecialPrice")?.value || 0);
                    rewardText = price > 0 ? `سعر خاص ${formatNumber(price)} ريال` : "سعر خاص";
                } else if (rewardType === "ServiceSpecialPrice") {
                    const price = Number(form.querySelector("#promotionSpecialPrice")?.value || 0);
                    const service = selectedText("promotionRewardServiceId", "الخدمة");
                    rewardText = price > 0
                        ? `${service} بسعر ${formatNumber(price)} ريال`
                        : `${service} بسعر خاص`;
                }

                ui.summaryText.textContent = conditionParts.length
                    ? `${conditionParts.join("، ")} واحصل على ${rewardText}`
                    : rewardText;
            }

            function selectedText(id, fallback) {
                const select = form.querySelector(`#${id}`);
                const option = select?.selectedOptions?.[0];
                if (!option || !option.value) return fallback;
                return String(option.textContent || fallback).split("—")[0].trim();
            }

            function formatNumber(value) {
                try {
                    return new Intl.NumberFormat("ar-YE", { maximumFractionDigits: 2 }).format(value);
                } catch {
                    return String(value);
                }
            }

            function setBusy(state, label) {
                busy = state;

                form.querySelectorAll("button, input, textarea, select").forEach(control => {
                    if (control === ui.close) {
                        control.disabled = state;
                        return;
                    }
                    if (control.dataset?.stepJump) {
                        control.disabled = state;
                        return;
                    }
                });

                if (ui.publish) {
                    if (!ui.publish.dataset.originalLabel) {
                        ui.publish.dataset.originalLabel = ui.publish.innerHTML;
                    }
                    ui.publish.innerHTML = state
                        ? `<span class="yg-promo-spinner"></span><span>${label || "جاري الحفظ..."}</span>`
                        : ui.publish.dataset.originalLabel;
                    ui.publish.disabled = state;
                }

                if (ui.draft) {
                    if (!ui.draft.dataset.originalLabel) {
                        ui.draft.dataset.originalLabel = ui.draft.innerHTML;
                    }
                    ui.draft.innerHTML = state && label?.includes("المسودة")
                        ? `<span class="yg-promo-spinner"></span><span>${label}</span>`
                        : ui.draft.dataset.originalLabel;
                    ui.draft.disabled = state;
                }

                if (ui.previous) ui.previous.disabled = state;
                if (ui.next) ui.next.disabled = state;
            }

            function setDefaultDates() {
                const today = new Date();
                const end = new Date(today);
                end.setDate(end.getDate() + 7);

                setValue("promotionStartDate", localDate(today));
                setValue("promotionEndDate", localDate(end));
            }

            function setValue(id, value) {
                const field = form.querySelector(`#${id}`);
                if (field) field.value = value ?? "";
            }

            function checkRadio(name, value) {
                const input = form.querySelector(`input[type="radio"][name="${name}"][value="${cssEscape(String(value))}"]`);
                if (input) input.checked = true;
            }

            function fieldValue(name) {
                const checked = form.querySelector(`input[type="radio"][name="${name}"]:checked`);
                if (checked) return checked.value;

                const hidden = form.querySelector(`input[type="hidden"][name="${name}"]`);
                if (hidden) return hidden.value;

                return "";
            }

            function numberOrNull(id) {
                const value = form.querySelector(`#${id}`)?.value;
                return value ? Number(value) : null;
            }

            function showAlert(message) {
                if (!ui.alert) return;
                ui.alert.textContent = message;
                ui.alert.hidden = false;
                ui.alert.scrollIntoView({ block: "nearest", behavior: "smooth" });
            }

            function hideAlert() {
                if (!ui.alert) return;
                ui.alert.hidden = true;
                ui.alert.textContent = "";
                form.querySelectorAll(".is-invalid").forEach(field => field.classList.remove("is-invalid"));
            }
        }

        function filterCards() {
            const query = normalizeSearch(searchInput?.value || "");
            let visible = 0;

            cards.forEach(card => {
                const sameGroup = card.dataset.promoGroup === activeFilter;
                const searchable = normalizeSearch(card.dataset.promoSearch || "");
                const matchesQuery = !query || searchable.includes(query);
                const show = sameGroup && matchesQuery;

                card.hidden = !show;
                if (show) visible += 1;
            });

            if (emptyState) emptyState.hidden = visible !== 0;
        }

        async function togglePromotion(button) {
            const isActive = button.dataset.nextActive === "true";

            const confirmed = await confirmAction({
                title: isActive ? "تفعيل العرض؟" : "إيقاف العرض مؤقتًا؟",
                text: isActive
                    ? "سيعود العرض للعمل وفق الفترة والشروط المحددة له."
                    : "يمكنك تفعيله لاحقًا دون فقد بياناته أو سجل استخدامه.",
                confirmText: isActive ? "تفعيل العرض" : "إيقاف مؤقت",
                icon: isActive ? "question" : "warning",
                danger: !isActive
            });

            if (!confirmed) return;

            button.disabled = true;

            try {
                const result = await postJson(endpoints.toggle, {
                    promotionId: Number(button.dataset.promotionId),
                    isActive: isActive
                });

                if (!result.success) throw new Error(result.message || "تعذر تغيير حالة العرض.");

                await notify("success", "تم", result.message || "تم تحديث حالة العرض.");
                reloadPromotions();
            } catch (error) {
                await notify("error", "لم يتم التحديث", error.message || "حدث خطأ أثناء تحديث العرض.");
                button.disabled = false;
            }
        }

        async function deletePromotion(button) {
            const hasBookings = button.dataset.hasBookings === "true";

            const confirmed = await confirmAction({
                title: hasBookings ? "إلغاء العرض؟" : "حذف العرض؟",
                text: hasBookings
                    ? "لوجود حجوزات مرتبطة بالعرض، سيُلغى العرض ويُحتفظ بسجله للحفاظ على سلامة الحجوزات السابقة."
                    : "سيُحذف العرض نهائيًا، ولا يمكن التراجع عن هذه العملية.",
                confirmText: hasBookings ? "إلغاء العرض" : "حذف نهائي",
                icon: "warning",
                danger: true
            });

            if (!confirmed) return;

            button.disabled = true;

            try {
                const result = await postJson(endpoints.remove, {
                    promotionId: Number(button.dataset.promotionId)
                });

                if (!result.success) throw new Error(result.message || "تعذر تنفيذ العملية.");

                await notify("success", "تم", result.message || "تم تنفيذ العملية بنجاح.");
                reloadPromotions();
            } catch (error) {
                await notify("error", "لم تتم العملية", error.message || "حدث خطأ أثناء تنفيذ العملية.");
                button.disabled = false;
            }
        }

        async function postJson(url, body) {
            const response = await fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token,
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify(body)
            });

            const result = await readResponse(response);
            if (!response.ok) {
                throw new Error(result.message || `فشل الطلب برمز ${response.status}.`);
            }

            return result;
        }

        function reloadPromotions() {
            if (typeof window.activatePlaceTab === "function") {
                window.activatePlaceTab("promotions-manager-tab", false);
            } else {
                window.location.reload();
            }
        }
    }

    function chooseInitialFilter(buttons) {
        if (!buttons.length) return "active";

        const preferredOrder = ["active", "scheduled", "draft", "archived"];
        for (const key of preferredOrder) {
            const button = buttons.find(item => item.dataset.promoFilter === key);
            if (button && Number(button.dataset.count || 0) > 0) return key;
        }

        return buttons[0]?.dataset.promoFilter || "active";
    }

    function readJson(element) {
        try {
            return JSON.parse(element?.textContent || "[]");
        } catch {
            return [];
        }
    }

    async function readResponse(response) {
        const text = await response.text();

        try {
            return JSON.parse(text);
        } catch {
            return {
                success: false,
                message: response.ok
                    ? "استجابة الخادم غير مفهومة."
                    : `تعذر تنفيذ الطلب (${response.status}). حاول مرة أخرى، وإذا استمر الخطأ راجع سجل الخادم.`
            };
        }
    }

    function toDateInput(value) {
        return value ? String(value).slice(0, 10) : "";
    }

    function localDate(date) {
        const offset = date.getTimezoneOffset();
        return new Date(date.getTime() - offset * 60000).toISOString().slice(0, 10);
    }

    function normalizeSearch(value) {
        return String(value || "")
            .toLowerCase()
            .replace(/[أإآ]/g, "ا")
            .replace(/ة/g, "ه")
            .replace(/ى/g, "ي")
            .replace(/\s+/g, " ")
            .trim();
    }

    function cssEscape(value) {
        if (window.CSS && typeof window.CSS.escape === "function") {
            return window.CSS.escape(value);
        }
        return value.replace(/(["\\])/g, "\\$1");
    }

    async function confirmAction(options) {
        const title = options?.title || "تأكيد العملية";
        const text = options?.text || "هل تريد المتابعة؟";
        const confirmText = options?.confirmText || "متابعة";
        const icon = options?.icon || "question";
        const danger = options?.danger === true;

        if (window.Swal) {
            const result = await window.Swal.fire({
                title: title,
                text: text,
                icon: icon,
                showCancelButton: true,
                reverseButtons: true,
                focusCancel: !danger,
                confirmButtonText: confirmText,
                cancelButtonText: "تراجع",
                buttonsStyling: false,
                customClass: {
                    popup: "yg-promo-confirm-popup",
                    confirmButton: danger ? "yg-promo-confirm-danger" : "yg-promo-confirm-primary",
                    cancelButton: "yg-promo-confirm-cancel"
                }
            });

            return result.isConfirmed;
        }

        return window.confirm(`${title}\n${text}`);
    }

    async function notify(icon, title, text) {
        if (window.Swal) {
            return window.Swal.fire({
                icon: icon,
                title: title,
                text: text,
                confirmButtonText: "حسنًا",
                buttonsStyling: false,
                customClass: {
                    popup: "yg-promo-confirm-popup",
                    confirmButton: "yg-promo-confirm-primary"
                }
            });
        }

        window.alert(text || title);
    }

    window.initPromotionsManager = initPromotionsManager;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initPromotionsManager);
    } else {
        initPromotionsManager();
    }
})(window, document);
