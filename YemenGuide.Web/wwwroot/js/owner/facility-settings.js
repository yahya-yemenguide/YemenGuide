"use strict";

/**
 * دليلك اليمني - سياسات المنشأة
 * تعتمد الصفحة على المنشأة النشطة المختارة من هيدر المالك.
 */

const facilityPolicyState = {
    activePlaceId: 0,
    noShowGraceMinutes: 60,
    isLoading: false,
    isSaving: false
};

function isVerificationPolicyFlow() {
    return document.getElementById("facilitySettingsPage")
        ?.dataset.verificationFlow === "true";
}

function withVerificationFlow(url) {
    if (!isVerificationPolicyFlow()) return url;

    const separator = url.includes("?") ? "&" : "?";
    return `${url}${separator}verificationFlow=true`;
}

function postVerificationMessage(type, payload = {}) {
    if (!isVerificationPolicyFlow() || window.parent === window) return;

    window.parent.postMessage(
        {
            source: "YemenGuideFacilityPolicies",
            type,
            ...payload
        },
        window.location.origin
    );
}

function notifyVerificationFrameHeight() {
    if (!isVerificationPolicyFlow() || window.parent === window) return;

    // لا نستخدم documentElement.scrollHeight لأنه يتأثر بارتفاع iframe نفسه
    // ويسبب حلقة تكبير ينتج عنها فراغ كبير أسفل السياسات.
    const page = document.getElementById("facilitySettingsPage");
    if (!page) return;

    postVerificationMessage("height", {
        height: Math.ceil(page.offsetHeight + 12)
    });
}

function initializeVerificationPolicyExperience() {
    if (!isVerificationPolicyFlow()) return;

    document.body.classList.add("facility-verification-flow");

    // داخل رحلة التوثيق لا نكرر أزرار الحفظ بعد كل تبويب.
    // يوجد شريط واحد ثابت ومريح خصوصًا على الهاتف.
    document.querySelectorAll(".policy-save-bar").forEach(bar => {
        bar.classList.add("verification-policy-section-save");
    });

    const page = document.getElementById("facilitySettingsPage");
    if (page && !document.getElementById("verificationPolicyActionBar")) {
        const actionBar = document.createElement("div");
        actionBar.id = "verificationPolicyActionBar";
        actionBar.className = "verification-policy-action-bar";
        actionBar.innerHTML = `
            <div class="verification-policy-action-bar__copy">
                <i class="bi bi-shield-check"></i>
                <span>
                    <small>عند الانتهاء من التعديلات</small>
                    <strong>احفظ السياسات ثم أكمل العقد</strong>
                </span>
            </div>
            <button type="button" id="verificationPolicySaveButton">
                <span>حفظ والمتابعة إلى العقد</span>
                <i class="bi bi-arrow-left"></i>
            </button>`;
        page.appendChild(actionBar);

        actionBar
            .querySelector("#verificationPolicySaveButton")
            ?.addEventListener("click", saveCurrentSettings);
    }

    const resizeObserver =
        typeof ResizeObserver !== "undefined"
            ? new ResizeObserver(() => notifyVerificationFrameHeight())
            : null;

    if (resizeObserver) {
        resizeObserver.observe(document.documentElement);
        const pageElement = document.getElementById("facilitySettingsPage");
        if (pageElement) resizeObserver.observe(pageElement);
    }

    const mutationObserver = new MutationObserver(() => {
        window.requestAnimationFrame(notifyVerificationFrameHeight);
    });

    mutationObserver.observe(document.body, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["class", "style", "hidden"]
    });

    window.addEventListener("resize", notifyVerificationFrameHeight, {
        passive: true
    });

    window.setTimeout(notifyVerificationFrameHeight, 120);
    window.setTimeout(notifyVerificationFrameHeight, 700);
}

document.addEventListener("DOMContentLoaded", initializeFacilitySettings);

async function initializeFacilitySettings() {
    const page = document.getElementById("facilitySettingsPage");
    if (!page) return;

    const settingsForm = document.getElementById("settingsForm");
    if (!settingsForm) return;

    initializeFacilityTabs();
    initializeMobileFacilityExperience();
    initializePolicyFieldBindings();
    initializeEmergencyPhone();
    initializeEmailValidation();
    initializeVerificationPolicyExperience();

    facilityPolicyState.activePlaceId = getActiveFacilityPlaceId();

    if (facilityPolicyState.activePlaceId <= 0) {
        showPageError("تعذر تحديد المنشأة الحالية. اختر منشأة من الهيدر ثم أعد المحاولة.");
        return;
    }

    const loadResult = await loadFacilitySettings(
        facilityPolicyState.activePlaceId
    );

    if (loadResult) {
        handleVisitMode(loadResult.isNew === true);
    }
}

function getActiveFacilityPlaceId() {
    const hiddenInput = document.getElementById("activeFacilityPlaceId");
    const page = document.getElementById("facilitySettingsPage");

    const rawValue =
        hiddenInput?.value ||
        page?.dataset.placeId ||
        "0";

    const placeId = Number.parseInt(rawValue, 10);
    return Number.isInteger(placeId) && placeId > 0 ? placeId : 0;
}

// ============================================================================
// التبويبات
// ============================================================================

function initializeFacilityTabs() {
    const tabButtons = Array.from(
        document.querySelectorAll(".custom-tabs-container .tab-btn")
    );
    const tabPanels = Array.from(
        document.querySelectorAll(".tab-pane-content")
    );

    if (tabButtons.length === 0 || tabPanels.length === 0) return;

    const availableTargets = tabButtons
        .map(button => button.dataset.target)
        .filter(Boolean);

    const urlParams = new URLSearchParams(window.location.search);
    const visitMode = urlParams.get("mode");
    const requestedTab = urlParams.get("tab");
    const savedTab = localStorage.getItem("activeFacilityTab");

    let initialTarget = "booking-tab";

    if (
        visitMode !== "new" &&
        requestedTab &&
        availableTargets.includes(requestedTab)
    ) {
        initialTarget = requestedTab;
    } else if (
        visitMode !== "new" &&
        savedTab &&
        availableTargets.includes(savedTab)
    ) {
        initialTarget = savedTab;
    }

    activateFacilityTab(initialTarget, false);

    tabButtons.forEach((button, index) => {
        button.addEventListener("click", function () {
            activateFacilityTab(this.dataset.target, false);
        });

        button.addEventListener("keydown", function (event) {
            if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
                return;
            }

            event.preventDefault();

            let targetIndex = index;

            if (event.key === "Home") {
                targetIndex = 0;
            } else if (event.key === "End") {
                targetIndex = tabButtons.length - 1;
            } else if (event.key === "ArrowLeft") {
                targetIndex = (index + 1) % tabButtons.length;
            } else if (event.key === "ArrowRight") {
                targetIndex =
                    (index - 1 + tabButtons.length) % tabButtons.length;
            }

            const targetButton = tabButtons[targetIndex];
            activateFacilityTab(targetButton.dataset.target, true);
        });
    });
}

function activateFacilityTab(targetClass, focusButton = false) {
    if (!targetClass) return;

    const tabButtons = document.querySelectorAll(
        ".custom-tabs-container .tab-btn"
    );
    const tabPanels = document.querySelectorAll(".tab-pane-content");
    const targetButton = document.querySelector(
        `.custom-tabs-container .tab-btn[data-target="${targetClass}"]`
    );
    const targetPanel = document.querySelector(`.${targetClass}`);

    if (!targetButton || !targetPanel) return;

    tabButtons.forEach(button => {
        const isActive = button === targetButton;
        button.classList.toggle("active", isActive);
        button.setAttribute("aria-selected", isActive ? "true" : "false");
        button.setAttribute("tabindex", isActive ? "0" : "-1");
    });

    tabPanels.forEach(panel => {
        const isActive = panel === targetPanel;
        panel.style.display = isActive ? "block" : "none";
        panel.classList.toggle("fade-in-up", isActive);
        panel.setAttribute("aria-hidden", isActive ? "false" : "true");
    });

    localStorage.setItem("activeFacilityTab", targetClass);

    const shouldCenterTab =
        focusButton || window.matchMedia("(max-width: 767.98px)").matches;

    if (focusButton) {
        targetButton.focus({ preventScroll: true });
    }

    if (shouldCenterTab) {
        targetButton.scrollIntoView({
            behavior: "smooth",
            block: "nearest",
            inline: "center"
        });
    }
}

// ============================================================================
// تجربة الهاتف
// ============================================================================

function initializeMobileFacilityExperience() {
    const page = document.getElementById("facilitySettingsPage");
    if (!page || page.dataset.mobileExperienceReady === "true") return;

    page.dataset.mobileExperienceReady = "true";

    let resizeTimer = 0;

    const updateMobileExperience = () => {
        window.clearTimeout(resizeTimer);

        resizeTimer = window.setTimeout(() => {
            const isMobile = window.matchMedia("(max-width: 767.98px)").matches;
            const viewportHeight = window.visualViewport?.height || window.innerHeight;

            page.classList.toggle("facility-mobile-view", isMobile);
            document.documentElement.style.setProperty(
                "--facility-viewport-height",
                `${Math.round(viewportHeight)}px`
            );

            if (!isMobile) return;

            const activeTab = document.querySelector(
                ".custom-tabs-container .tab-btn.active"
            );

            activeTab?.scrollIntoView({
                behavior: "smooth",
                block: "nearest",
                inline: "center"
            });
        }, 90);
    };

    updateMobileExperience();

    window.addEventListener("resize", updateMobileExperience, { passive: true });
    window.addEventListener("orientationchange", updateMobileExperience, {
        passive: true
    });
    window.visualViewport?.addEventListener("resize", updateMobileExperience, {
        passive: true
    });
}

// ============================================================================
// ربط الحقول والأزرار
// ============================================================================

function initializePolicyFieldBindings() {
    document.querySelectorAll(".btn-royal-save").forEach(button => {
        button.addEventListener("click", saveCurrentSettings);
    });

    const nonRefundableSwitch = document.getElementById("nonRefundableRate");
    const nonRefundableInput = document.getElementById(
        "nonRefundableDiscount"
    );

    if (nonRefundableSwitch && nonRefundableInput) {
        nonRefundableSwitch.addEventListener("change", function () {
            nonRefundableInput.disabled = !this.checked;

            if (this.checked) {
                nonRefundableInput.focus();
            } else {
                nonRefundableInput.value = "10";
            }
        });
    }

    togglePenaltyInput();
    toggleOperationMode();
    toggleChatSettings();
}

function handleVisitMode(isNewPolicy) {
    const urlParams = new URLSearchParams(window.location.search);
    const visitMode = urlParams.get("mode");
    const firstTabButton = document.querySelector(
        '.tab-btn[data-target="booking-tab"]'
    );

    if (isVerificationPolicyFlow()) {
        activateFacilityTab("booking-tab", false);
        setSaveButtonsMode("required");
        window.setTimeout(notifyVerificationFrameHeight, 100);
        return;
    }

    if (visitMode === "new" || isNewPolicy) {
        activateFacilityTab("booking-tab", false);
        setSaveButtonsMode("required");

        Swal.fire({
            icon: "warning",
            title: "استكمال السياسات الأساسية",
            text: "اضبط الحجز والإلغاء والتشغيل والتواصل، ثم احفظ السياسات.",
            confirmButtonColor: "#00A6DA",
            confirmButtonText: "البدء الآن",
            allowOutsideClick: false
        }).then(result => {
            if (!result.isConfirmed) return;

            firstTabButton?.focus();

            const addDepositButton = document.querySelector(
                "#depositPoliciesContainer + button"
            );

            if (addDepositButton) {
                addDepositButton.classList.add("policy-required-pulse");
                addDepositButton.scrollIntoView({
                    behavior: "smooth",
                    block: "center"
                });

                addDepositButton.addEventListener(
                    "click",
                    () => addDepositButton.classList.remove("policy-required-pulse"),
                    { once: true }
                );
            }
        });

        return;
    }

    setSaveButtonsMode("normal");

    if (visitMode === "edit") {
        Swal.mixin({
            toast: true,
            position: "top-end",
            timer: 3000,
            showConfirmButton: false
        }).fire({
            icon: "info",
            title: "وضع التعديل",
            text: "تم تحميل السياسات المحفوظة للمنشأة الحالية."
        });
    }
}

function setSaveButtonsMode(mode) {
    document.querySelectorAll(".btn-royal-save").forEach(button => {
        const label = button.querySelector("span");
        const icon = button.querySelector("i");

        button.classList.toggle("is-required", mode === "required");

        if (label) {
            if (!label.dataset.originalText) {
                label.dataset.originalText = label.textContent.trim();
            }

            label.textContent = mode === "required"
                ? "تأكيد وحفظ السياسات"
                : label.dataset.originalText;
        }

        if (icon) {
            if (!icon.dataset.originalClass) {
                icon.dataset.originalClass = icon.className;
            }

            icon.className = mode === "required"
                ? "bi bi-shield-check"
                : icon.dataset.originalClass;
        }
    });
}

// ============================================================================
// تحميل بيانات المنشأة
// ============================================================================

async function loadFacilitySettings(placeId) {
    if (facilityPolicyState.isLoading) return null;

    facilityPolicyState.isLoading = true;
    setPageBusy(true, "جاري تحميل السياسات...");

    try {
        const response = await fetch(
            withVerificationFlow(
                `/Owner/Settings/GetFacilitySettings?placeId=${encodeURIComponent(placeId)}`
            ),
            {
                method: "GET",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            }
        );

        const result = await readJsonResponse(response);

        if (!response.ok || !result.success) {
            throw new Error(
                result.message || "تعذر تحميل سياسات المنشأة الحالية."
            );
        }

        if (result.isNew) {
            prepareNewFacilityPolicyForm();
        } else {
            applyFacilityPolicyToForm(result.data || {});
        }

        return result;
    } catch (error) {
        console.error("Failed to load facility settings:", error);
        showPageError(error.message || "فشل تحميل الإعدادات.");
        return null;
    } finally {
        facilityPolicyState.isLoading = false;
        setPageBusy(false);
    }
}

function prepareNewFacilityPolicyForm() {
    const form = document.getElementById("settingsForm");
    form?.reset();

    document
        .querySelectorAll('input[name="operationSystemMode"]')
        .forEach(radio => {
            radio.checked = false;
        });

    document.getElementById("fullDaySettingsCard")?.classList.add("d-none");
    document.getElementById("shiftsSettingsCard")?.classList.add("d-none");

    const penaltyType = document.getElementById("penaltyType");
    if (penaltyType) {
        let emptyOption = penaltyType.querySelector('option[value=""]');

        if (!emptyOption) {
            emptyOption = document.createElement("option");
            emptyOption.value = "";
            emptyOption.textContent = "-- اختر نوع الغرامة --";
            emptyOption.disabled = true;
            penaltyType.insertBefore(emptyOption, penaltyType.firstChild);
        }

        penaltyType.value = "";
    }

    [
        "depositPoliciesContainer",
        "securityPoliciesContainer",
        "exceptionPoliciesContainer",
        "shiftsContainer",
        "quickRepliesContainer"
    ].forEach(containerId => {
        const container = document.getElementById(containerId);
        if (container) container.innerHTML = "";
    });

    facilityPolicyState.noShowGraceMinutes = 60;
    togglePenaltyInput();
    toggleChatSettings();
}

function applyFacilityPolicyToForm(policy) {
    setCheckedRadio("bookingApprovalMode", policy.bookingApprovalMode);

    setInputValue("paymentTimeout", policy.paymentTimeout);
    setCheckboxValue("isSarSupported", policy.isSarSupported);
    setCheckboxValue("isUsdSupported", policy.isUsdSupported);
    setInputValue("cleaningFee", policy.cleaningFee ?? 0);
    setInputValue("taxPercentage", policy.taxPercentage ?? 0);
    setCheckboxValue(
        "nonRefundableRate",
        policy.isNonRefundableEnabled
    );
    setInputValue(
        "nonRefundableDiscount",
        policy.nonRefundableDiscount ?? 10
    );

    const nonRefundableDiscount = document.getElementById(
        "nonRefundableDiscount"
    );
    if (nonRefundableDiscount) {
        nonRefundableDiscount.disabled = !policy.isNonRefundableEnabled;
    }

    rebuildCompositeList(
        "depositPoliciesContainer",
        policy.depositPoliciesJson,
        "%"
    );
    rebuildSimpleList(
        "securityPoliciesContainer",
        policy.securityPoliciesJson,
        "shield"
    );

    setInputValue("freeCancellationHours", policy.freeCancellationHours);
    setCheckboxValue("immediateGracePeriod", policy.immediateGracePeriod);
    setInputValue("penaltyType", policy.penaltyType);
    setInputValue("penaltyValue", policy.penaltyValue ?? 0);
    setCheckboxValue("confiscateOnNoShow", policy.confiscateOnNoShow);
    setCheckedRadio("refundTransferFee", policy.refundTransferFee);
    setInputValue("refundProcessingDays", policy.refundProcessingDays);

    rebuildSimpleList(
        "exceptionPoliciesContainer",
        policy.exceptionPoliciesJson,
        "info"
    );

    facilityPolicyState.noShowGraceMinutes =
        Number.parseInt(policy.noShowGraceMinutes, 10) || 60;

    setCheckedRadio("operationSystemMode", policy.operationSystemMode);
    setInputValue("checkInTime", policy.checkInTime || "14:00");
    setInputValue("checkOutTime", policy.checkOutTime || "12:00");
    setInputValue("bufferTimeHours", policy.bufferTimeHours ?? 2);
    setInputValue("minimumNoticeHours", policy.minimumNoticeHours);
    setInputValue("maximumHorizonMonths", policy.maximumHorizonMonths);

    rebuildShiftsList(policy.shiftsJson);

    const closedDays = String(policy.closedDaysCsv || "")
        .split(",")
        .map(value => value.trim())
        .filter(Boolean);

    document.querySelectorAll('input[name="closedDays"]').forEach(checkbox => {
        checkbox.checked = closedDays.includes(checkbox.value);
    });

    setCheckboxValue("isChatEnabled", policy.isChatEnabled);
    setInputValue("welcomeMessage", policy.welcomeMessage || "");
    setCheckboxValue("offlineReplyToggle", policy.offlineReplyToggle);
    setInputValue("offlineMessage", policy.offlineMessage || "");
    rebuildQuickRepliesList(policy.quickRepliesJson);

    const phoneInput = document.getElementById("emergencyContact");
    const emergencyContact = policy.emergencyContact || "";

    if (window.emergencyPhoneIti && emergencyContact) {
        window.emergencyPhoneIti.setNumber(emergencyContact);
    } else if (phoneInput) {
        phoneInput.value = emergencyContact;
    }

    setInputValue("contactEmail", policy.contactEmail || "");
    setCheckboxValue("isSoundAlertEnabled", policy.isSoundAlertEnabled);
    setCheckboxValue("isPushNoticeEnabled", policy.isPushNoticeEnabled);

    setCheckboxValue("forFamiliesOnly", policy.forFamiliesOnly);
    setCheckboxValue("allowSmoking", policy.allowSmoking);
    setCheckboxValue("allowParties", policy.allowParties);
    setCheckboxValue("allowQatInside", policy.allowQatInside);

    togglePenaltyInput();
    toggleOperationMode();
    toggleChatSettings();
    validateEmergencyPhoneInput();
}

function setInputValue(elementId, value) {
    const element = document.getElementById(elementId);
    if (element) element.value = value ?? "";
}

function setCheckboxValue(elementId, value) {
    const element = document.getElementById(elementId);
    if (element) element.checked = value === true;
}

function setCheckedRadio(name, value) {
    if (!value) return;

    const radio = document.querySelector(
        `input[name="${name}"][value="${cssEscape(String(value))}"]`
    );

    if (radio) radio.checked = true;
}

// ============================================================================
// الإضافة والتعديل والحذف للقوائم الديناميكية
// ============================================================================

function toggleEditMode(button) {
    const item = button?.closest(".luxury-list-item");
    if (!item) return;

    const fields = Array.from(item.querySelectorAll("input, select"));
    if (fields.length === 0) return;

    const isEditing = fields.some(field =>
        field.tagName === "SELECT"
            ? !field.disabled
            : !field.readOnly
    );

    if (isEditing) {
        fields.forEach(field => {
            if (field.tagName === "SELECT") {
                field.disabled = true;
            } else {
                field.readOnly = true;
            }
            field.classList.remove("editing-mode");
        });

        button.innerHTML = '<i class="bi bi-pencil-square"></i>';
        button.classList.remove("text-success");
        button.classList.add("text-primary");
        button.title = "تعديل";
        button.setAttribute("aria-label", "تعديل");
    } else {
        fields.forEach(field => {
            if (field.tagName === "SELECT") {
                field.disabled = false;
            } else {
                field.readOnly = false;
            }
            field.classList.add("editing-mode");
        });

        fields[0].focus();
        button.innerHTML = '<i class="bi bi-check-lg"></i>';
        button.classList.remove("text-primary");
        button.classList.add("text-success");
        button.title = "حفظ التعديل";
        button.setAttribute("aria-label", "حفظ التعديل");
    }
}

function addCompositePolicy() {
    const container = document.getElementById("depositPoliciesContainer");
    if (!container) return;

    const item = document.createElement("div");
    item.className =
        "luxury-list-item policy-builder-item fade-in-up";
    item.innerHTML = `
        <span class="item-icon policy-builder-item__icon" aria-hidden="true">
            <i class="bi bi-check2"></i>
        </span>
        <div class="input-group input-group-sm policy-builder-item__value">
            <input type="number"
                   class="form-control text-center fw-bold policy-val editing-mode"
                   placeholder="0"
                   min="0"
                   aria-label="قيمة العربون">
            <select class="form-select fw-bold policy-type editing-mode"
                    aria-label="نوع قيمة العربون">
                <option value="%" selected>%</option>
                <option value="fixed">ريال</option>
            </select>
        </div>
        <input type="text"
               class="form-control policy-desc editing-mode"
               placeholder="اكتب تفاصيل الشرط هنا..."
               aria-label="وصف سياسة العربون">
        ${buildItemActionsHtml("سياسة العربون")}
    `;

    container.appendChild(item);
    item.querySelector(".policy-val")?.focus();
}

function addSimplePolicy() {
    const container = document.getElementById("securityPoliciesContainer");
    if (!container) return;

    const item = createSimpleListItem(
        "",
        "shield",
        true,
        "اكتب تفاصيل الضمان هنا..."
    );

    container.appendChild(item);
    item.querySelector(".policy-desc")?.focus();
}

function addExceptionPolicy() {
    const container = document.getElementById("exceptionPoliciesContainer");
    if (!container) return;

    const item = createSimpleListItem(
        "",
        "info",
        true,
        "اكتب الاستثناء أو الشرط هنا..."
    );

    container.appendChild(item);
    item.querySelector(".policy-desc")?.focus();
}

function addShiftPolicy() {
    const container = document.getElementById("shiftsContainer");
    if (!container) return;

    const item = createShiftListItem(
        { name: "", from: "08:00", to: "14:00" },
        true
    );

    container.appendChild(item);
    item.querySelector(".policy-desc")?.focus();
}

function addQuickReply() {
    const container = document.getElementById("quickRepliesContainer");
    if (!container) return;

    const item = createQuickReplyListItem(
        { title: "", reply: "" },
        true
    );

    container.appendChild(item);
    item.querySelector("input")?.focus();
}

function createSimpleListItem(
    value,
    iconType = "info",
    editing = false,
    placeholder = "اكتب التفاصيل هنا..."
) {
    const item = document.createElement("div");
    item.className =
        "luxury-list-item policy-builder-item fade-in-up";

    const iconClass =
        iconType === "shield"
            ? "bi-shield-check"
            : "bi-info-circle-fill";

    item.innerHTML = `
        <span class="item-icon policy-builder-item__icon" aria-hidden="true">
            <i class="bi ${iconClass}"></i>
        </span>
        <input type="text"
               class="form-control policy-desc ${editing ? "editing-mode" : ""}"
               value="${escapeAttribute(value)}"
               placeholder="${escapeAttribute(placeholder)}"
               aria-label="وصف السياسة"
               ${editing ? "" : "readonly"}>
        ${buildItemActionsHtml("السياسة", editing)}
    `;

    return item;
}

function createShiftListItem(shift, editing = false) {
    const item = document.createElement("div");
    item.className =
        "luxury-list-item policy-builder-item policy-builder-item--shift fade-in-up";
    item.innerHTML = `
        <span class="item-icon policy-builder-item__icon" aria-hidden="true">
            <i class="bi bi-sun-fill"></i>
        </span>
        <input type="text"
               class="form-control fw-bold policy-desc text-center ${editing ? "editing-mode" : ""}"
               value="${escapeAttribute(shift.name || "")}"
               placeholder="اسم الفترة..."
               aria-label="اسم الفترة"
               ${editing ? "" : "readonly"}>
        <div class="input-group input-group-sm policy-shift-time">
            <span class="input-group-text">من</span>
            <input type="time"
                   class="form-control text-center fw-bold policy-val ${editing ? "editing-mode" : ""}"
                   value="${escapeAttribute(shift.from || "08:00")}"
                   aria-label="بداية الفترة"
                   ${editing ? "" : "readonly"}>
            <span class="input-group-text">إلى</span>
            <input type="time"
                   class="form-control text-center fw-bold policy-val ${editing ? "editing-mode" : ""}"
                   value="${escapeAttribute(shift.to || "14:00")}"
                   aria-label="نهاية الفترة"
                   ${editing ? "" : "readonly"}>
        </div>
        ${buildItemActionsHtml("الفترة", editing)}
    `;

    return item;
}

function createQuickReplyListItem(reply, editing = false) {
    const item = document.createElement("div");
    item.className =
        "luxury-list-item policy-builder-item policy-builder-item--reply fade-in-up";
    item.innerHTML = `
        <span class="item-icon policy-builder-item__icon" aria-hidden="true">
            <i class="bi bi-chat-right-text"></i>
        </span>
        <div class="input-group input-group-sm flex-grow-1 policy-quick-reply-fields">
            <span class="input-group-text">العنوان</span>
            <input type="text"
                   class="form-control fw-bold text-primary ${editing ? "editing-mode" : ""}"
                   value="${escapeAttribute(reply.title || "")}"
                   placeholder="مثال: الموقع الجغرافي"
                   aria-label="عنوان الرد الجاهز"
                   ${editing ? "" : "readonly"}>
            <span class="input-group-text">الرد</span>
            <input type="text"
                   class="form-control fw-bold policy-desc ${editing ? "editing-mode" : ""}"
                   value="${escapeAttribute(reply.reply || "")}"
                   placeholder="اكتب النص الذي سيتم إرساله..."
                   aria-label="نص الرد الجاهز"
                   ${editing ? "" : "readonly"}>
        </div>
        ${buildItemActionsHtml("الرد", editing)}
    `;

    return item;
}

function buildItemActionsHtml(itemName, editing = false) {
    const editClass = editing ? "text-success" : "text-primary";
    const editIcon = editing ? "bi-check-lg" : "bi-pencil-square";
    const editLabel = editing ? "حفظ التعديل" : `تعديل ${itemName}`;

    return `
        <div class="actions-wrapper policy-builder-item__actions">
            <button type="button"
                    class="btn-action ${editClass} btn-edit"
                    onclick="toggleEditMode(this)"
                    aria-label="${escapeAttribute(editLabel)}"
                    title="${escapeAttribute(editLabel)}">
                <i class="bi ${editIcon}"></i>
            </button>
            <button type="button"
                    class="btn-action text-danger btn-delete"
                    onclick="this.closest('.luxury-list-item').remove()"
                    aria-label="حذف ${escapeAttribute(itemName)}"
                    title="حذف ${escapeAttribute(itemName)}">
                <i class="bi bi-trash3"></i>
            </button>
        </div>
    `;
}

// ============================================================================
// الحقول الشرطية
// ============================================================================

function togglePenaltyInput() {
    const penaltyType = document.getElementById("penaltyType");
    const penaltyValue = document.getElementById("penaltyValue");
    const penaltyUnit = document.getElementById("penaltyUnit");

    if (!penaltyType || !penaltyValue || !penaltyUnit) return;

    if (penaltyType.value === "DeductPercentage") {
        penaltyValue.disabled = false;
        penaltyUnit.textContent = "%";
        penaltyValue.max = "100";
    } else if (penaltyType.value === "DeductFixed") {
        penaltyValue.disabled = false;
        penaltyUnit.textContent = "ريال";
        penaltyValue.removeAttribute("max");
    } else {
        penaltyValue.disabled = true;
        penaltyValue.value = "0";
        penaltyUnit.textContent = "--";
    }
}

function toggleOperationMode() {
    const fullDayCard = document.getElementById("fullDaySettingsCard");
    const shiftsCard = document.getElementById("shiftsSettingsCard");
    const selectedMode = document.querySelector(
        'input[name="operationSystemMode"]:checked'
    );

    if (!fullDayCard || !shiftsCard) return;

    if (!selectedMode) {
        fullDayCard.classList.add("d-none");
        shiftsCard.classList.add("d-none");
        return;
    }

    const isFullDay = selectedMode.value === "FullDay";
    fullDayCard.classList.toggle("d-none", !isFullDay);
    shiftsCard.classList.toggle("d-none", isFullDay);
}

function toggleChatSettings() {
    const chatToggle = document.getElementById("isChatEnabled");
    const container = document.getElementById("chatSettingsContainer");
    const welcomeMessage = document.getElementById("welcomeMessage");
    const offlineToggle = document.getElementById("offlineReplyToggle");

    if (!chatToggle || !container) return;

    const enabled = chatToggle.checked;
    container.classList.toggle("is-disabled", !enabled);
    container.style.opacity = enabled ? "1" : "0.55";
    container.style.pointerEvents = enabled ? "auto" : "none";

    if (welcomeMessage) welcomeMessage.disabled = !enabled;
    if (offlineToggle) offlineToggle.disabled = !enabled;

    toggleOfflineMessage();
}

function toggleOfflineMessage() {
    const chatToggle = document.getElementById("isChatEnabled");
    const offlineToggle = document.getElementById("offlineReplyToggle");
    const offlineMessage = document.getElementById("offlineMessage");

    if (!offlineToggle || !offlineMessage) return;

    const enabled =
        (chatToggle?.checked ?? true) &&
        offlineToggle.checked;

    offlineMessage.disabled = !enabled;
    offlineMessage.classList.toggle("bg-light", !enabled);
    offlineMessage.classList.toggle("text-muted", !enabled);
}

// ============================================================================
// الحفظ والتحقق
// ============================================================================

async function saveCurrentSettings() {
    if (facilityPolicyState.isSaving) return;

    const placeId = getActiveFacilityPlaceId();

    if (placeId <= 0) {
        Swal.fire({
            icon: "warning",
            title: "تعذر تحديد المنشأة",
            text: "اختر منشأة من الهيدر ثم أعد المحاولة.",
            confirmButtonColor: "#00A6DA"
        });
        return;
    }

    clearValidationHighlights();

    const errors = validateFacilitySettingsForm();

    if (errors.length > 0) {
        const firstError = errors[0];

        await Swal.fire({
            icon: "error",
            title: "بيانات مطلوبة",
            text: firstError.message,
            confirmButtonColor: "#FF5A5F",
            confirmButtonText: "حسنًا",
            allowOutsideClick: false
        });

        activateFacilityTab(firstError.tab, false);

        window.setTimeout(() => {
            highlightValidationTarget(firstError.target);
        }, 180);

        return;
    }

    const phoneInput = document.getElementById("emergencyContact");
    const finalEmergencyPhone = window.emergencyPhoneIti
        ? window.emergencyPhoneIti.getNumber()
        : phoneInput?.value.trim() || "";

    const payload = buildFacilitySettingsPayload(
        placeId,
        finalEmergencyPhone
    );

    facilityPolicyState.isSaving = true;
    setSaveButtonsBusy(true);
    setFacilityVisualSaveState("saving");

    try {
        const response = await fetch(withVerificationFlow("/Owner/Settings/SaveFacilitySettings"), {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            credentials: "same-origin",
            body: JSON.stringify(payload)
        });

        const result = await readJsonResponse(response);

        if (!response.ok || !result.success) {
            throw new Error(result.message || "تعذر حفظ السياسات.");
        }

        setFacilityVisualSaveState("saved");
        await handleSuccessfulSave(placeId);
    } catch (error) {
        console.error("Failed to save facility settings:", error);
        setFacilityVisualSaveState("error");
        Swal.fire({
            icon: "error",
            title: "تعذر الحفظ",
            text: error.message || "فشل الاتصال بالخادم.",
            confirmButtonColor: "#FF5A5F"
        });
    } finally {
        facilityPolicyState.isSaving = false;
        setSaveButtonsBusy(false);
    }
}

function validateFacilitySettingsForm() {
    const errors = [];
    const operationMode = document.querySelector(
        'input[name="operationSystemMode"]:checked'
    );

    if (!operationMode) {
        errors.push({
            tab: "operational-tab",
            target: 'input[name="operationSystemMode"]',
            message: "حدد نظام التشغيل: يوم كامل أو فترات."
        });
    } else if (operationMode.value === "FullDay") {
        if (!getElementValue("checkInTime")) {
            errors.push({
                tab: "operational-tab",
                target: "#checkInTime",
                message: "حدد وقت الاستلام."
            });
        }

        if (!getElementValue("checkOutTime")) {
            errors.push({
                tab: "operational-tab",
                target: "#checkOutTime",
                message: "حدد وقت التسليم."
            });
        }
    } else if (
        document.querySelectorAll("#shiftsContainer .luxury-list-item").length === 0
    ) {
        errors.push({
            tab: "operational-tab",
            target: "#shiftsContainer + button",
            message: "أضف فترة تشغيلية واحدة على الأقل."
        });
    }

    validateRequiredNumber(errors, {
        id: "minimumNoticeHours",
        tab: "operational-tab",
        minimum: 0,
        message: "حدد الحد الأدنى للحجز المسبق."
    });

    validateRequiredNumber(errors, {
        id: "maximumHorizonMonths",
        tab: "operational-tab",
        minimum: 1,
        message: "حدد أقصى أفق زمني للحجز."
    });

    validateRequiredNumber(errors, {
        id: "freeCancellationHours",
        tab: "cancellation-tab",
        minimum: 0,
        message: "حدد مدة الإلغاء المجاني."
    });

    const penaltyType = getElementValue("penaltyType");

    if (!penaltyType) {
        errors.push({
            tab: "cancellation-tab",
            target: "#penaltyType",
            message: "اختر نوع غرامة الإلغاء المتأخر."
        });
    } else if (
        ["DeductPercentage", "DeductFixed"].includes(penaltyType)
    ) {
        validateRequiredNumber(errors, {
            id: "penaltyValue",
            tab: "cancellation-tab",
            minimum: 0.01,
            message: "أدخل قيمة الغرامة المحددة."
        });
    }

    validateRequiredNumber(errors, {
        id: "refundProcessingDays",
        tab: "cancellation-tab",
        minimum: 1,
        message: "حدد مدة تحويل المبلغ للزبون."
    });

    if (
        document.querySelectorAll("#depositPoliciesContainer .luxury-list-item")
            .length === 0
    ) {
        errors.push({
            tab: "booking-tab",
            target: "#depositPoliciesContainer + button",
            message: "أضف سياسة عربون ودفع واحدة على الأقل."
        });
    }

    if (
        document.querySelectorAll("#securityPoliciesContainer .luxury-list-item")
            .length === 0
    ) {
        errors.push({
            tab: "booking-tab",
            target: "#securityPoliciesContainer + button",
            message: "أضف ضمان سلامة واحدًا على الأقل."
        });
    }

    validateRequiredNumber(errors, {
        id: "paymentTimeout",
        tab: "booking-tab",
        minimum: 1,
        message: "حدد مهلة سداد الحجز."
    });

    const phoneInput = document.getElementById("emergencyContact");

    if (
        !phoneInput ||
        !window.isEmergencyPhoneValid(
            window.emergencyPhoneIti,
            phoneInput
        )
    ) {
        errors.push({
            tab: "communication-tab",
            target: "#emergencyContact",
            message: "أدخل رقم طوارئ صحيحًا ومطابقًا للدولة."
        });
    }

    const contactEmail = getElementValue("contactEmail");

    if (contactEmail && !isAllowedContactEmail(contactEmail)) {
        errors.push({
            tab: "communication-tab",
            target: "#contactEmail",
            message: "أدخل بريدًا صحيحًا من Gmail أو Yahoo أو Hotmail أو Outlook أو iCloud."
        });
    }

    return errors;
}

function validateRequiredNumber(errors, options) {
    const element = document.getElementById(options.id);
    const rawValue = element?.value?.trim() ?? "";
    const value = Number(rawValue);

    if (
        rawValue === "" ||
        !Number.isFinite(value) ||
        value < options.minimum
    ) {
        errors.push({
            tab: options.tab,
            target: `#${options.id}`,
            message: options.message
        });
    }
}

function buildFacilitySettingsPayload(placeId, emergencyPhone) {
    const closedDays = Array.from(
        document.querySelectorAll('input[name="closedDays"]:checked')
    ).map(checkbox => checkbox.value);

    return {
        PlaceId: placeId,
        BookingApprovalMode:
            getCheckedRadioValue("bookingApprovalMode") || "Manual",
        DepositPoliciesJson: serializeCompositeList(
            "depositPoliciesContainer"
        ),
        SecurityPoliciesJson: serializeSimpleList(
            "securityPoliciesContainer"
        ),
        PaymentTimeout: toInteger("paymentTimeout", 12),
        IsSarSupported: getCheckboxValue("isSarSupported"),
        IsUsdSupported: getCheckboxValue("isUsdSupported"),
        CleaningFee: toDecimal("cleaningFee", 0),
        TaxPercentage: toDecimal("taxPercentage", 0),
        IsNonRefundableEnabled: getCheckboxValue("nonRefundableRate"),
        NonRefundableDiscount: toInteger("nonRefundableDiscount", 10),
        FreeCancellationHours: toInteger("freeCancellationHours", 48),
        ImmediateGracePeriod: getCheckboxValue("immediateGracePeriod"),
        PenaltyType: getElementValue("penaltyType") || "LoseDeposit",
        PenaltyValue: toDecimal("penaltyValue", 0),
        ConfiscateOnNoShow: getCheckboxValue("confiscateOnNoShow"),
        NoShowGraceMinutes: facilityPolicyState.noShowGraceMinutes || 60,
        RefundTransferFee:
            getCheckedRadioValue("refundTransferFee") || "CustomerPays",
        RefundProcessingDays: toInteger("refundProcessingDays", 3),
        ExceptionPoliciesJson: serializeSimpleList(
            "exceptionPoliciesContainer"
        ),
        OperationSystemMode:
            getCheckedRadioValue("operationSystemMode") || "FullDay",
        CheckInTime: getElementValue("checkInTime") || null,
        CheckOutTime: getElementValue("checkOutTime") || null,
        ShiftsJson: serializeShiftsList(),
        BufferTimeHours: toInteger("bufferTimeHours", 2),
        MinimumNoticeHours: toInteger("minimumNoticeHours", 24),
        MaximumHorizonMonths: toInteger("maximumHorizonMonths", 6),
        ClosedDaysCsv: closedDays.join(","),
        IsChatEnabled: getCheckboxValue("isChatEnabled"),
        WelcomeMessage: getElementValue("welcomeMessage"),
        OfflineReplyToggle: getCheckboxValue("offlineReplyToggle"),
        OfflineMessage: getElementValue("offlineMessage"),
        QuickRepliesJson: serializeQuickRepliesList(),
        EmergencyContact: emergencyPhone,
        ContactEmail: getElementValue("contactEmail"),
        IsSoundAlertEnabled: getCheckboxValue("isSoundAlertEnabled"),
        IsPushNoticeEnabled: getCheckboxValue("isPushNoticeEnabled"),
        ForFamiliesOnly: getCheckboxValue("forFamiliesOnly"),
        AllowSmoking: getCheckboxValue("allowSmoking"),
        AllowParties: getCheckboxValue("allowParties"),
        AllowQatInside: getCheckboxValue("allowQatInside")
    };
}

async function handleSuccessfulSave(placeId) {
    const urlParams = new URLSearchParams(window.location.search);
    const visitMode = urlParams.get("mode");

    if (isVerificationPolicyFlow()) {
        postVerificationMessage("saved", {
            placeId,
            mode: "manual"
        });
        notifyVerificationFrameHeight();
        return;
    }

    if (visitMode === "new" || visitMode === "edit") {
        const result = await Swal.fire({
            icon: "success",
            title: "تم الحفظ بنجاح",
            text: "هل تريد مراجعة القسم التالي أم العودة لإدارة المنشآت؟",
            showCancelButton: true,
            confirmButtonColor: "#00A6DA",
            cancelButtonColor: "#6c757d",
            confirmButtonText: "القسم التالي",
            cancelButtonText: "العودة للقائمة",
            reverseButtons: true,
            allowOutsideClick: false
        });

        if (result.isConfirmed) {
            const currentButton = document.querySelector(
                ".custom-tabs-container .tab-btn.active"
            );
            const nextButton = currentButton?.nextElementSibling;

            if (nextButton?.classList.contains("tab-btn")) {
                activateFacilityTab(nextButton.dataset.target, true);
                window.scrollTo({ top: 0, behavior: "smooth" });
            } else {
                Swal.mixin({
                    toast: true,
                    position: "top-end",
                    timer: 2800,
                    showConfirmButton: false
                }).fire({
                    icon: "success",
                    title: "تمت مراجعة جميع الأقسام"
                });
            }
        } else {
            localStorage.setItem("activePlaceTab", "details-manager-tab");
            localStorage.setItem("autoSelectPlaceId", String(placeId));
            window.location.href = "/Owner/Place/ManagePlaces";
        }

        return;
    }

    Swal.mixin({
        toast: true,
        position: "top-end",
        timer: 2500,
        showConfirmButton: false
    }).fire({
        icon: "success",
        title: "تم حفظ سياسات المنشأة"
    });
}

function highlightValidationTarget(selector) {
    let target = document.querySelector(selector);
    if (!target) return;

    if (target.type === "radio" || target.type === "checkbox") {
        target =
            target.closest(".policy-choice-grid") ||
            target.closest(".policy-toggle-card") ||
            target.parentElement;
    }

    target.classList.add("error-highlight");
    target.scrollIntoView({ behavior: "smooth", block: "center" });

    const clear = () => target.classList.remove("error-highlight");
    target.addEventListener("input", clear, { once: true });
    target.addEventListener("change", clear, { once: true });
    target.addEventListener("click", clear, { once: true });

    if (typeof target.focus === "function") {
        window.setTimeout(() => target.focus({ preventScroll: true }), 250);
    }
}

function clearValidationHighlights() {
    document.querySelectorAll(".error-highlight").forEach(element => {
        element.classList.remove("error-highlight");
    });
}

// ============================================================================
// تحويل القوائم إلى JSON وإعادتها إلى الصفحة
// ============================================================================

function serializeSimpleList(containerId) {
    return JSON.stringify(
        Array.from(
            document.querySelectorAll(
                `#${containerId} .luxury-list-item .policy-desc`
            )
        )
            .map(input => input.value.trim())
            .filter(Boolean)
    );
}

function rebuildSimpleList(containerId, jsonText, iconType = "info") {
    const container = document.getElementById(containerId);
    if (!container) return;

    container.innerHTML = "";

    parseJsonArray(jsonText).forEach(value => {
        if (typeof value !== "string") return;
        container.appendChild(
            createSimpleListItem(value, iconType, false)
        );
    });
}

function serializeCompositeList(containerId) {
    const policies = [];

    document
        .querySelectorAll(`#${containerId} .luxury-list-item`)
        .forEach(item => {
            const value = item.querySelector(".policy-val")?.value || "";
            const type = item.querySelector(".policy-type")?.value || "%";
            const description =
                item.querySelector(".policy-desc")?.value.trim() || "";

            if (description) {
                policies.push({
                    val: value,
                    type,
                    desc: description
                });
            }
        });

    return JSON.stringify(policies);
}

function rebuildCompositeList(containerId, jsonText) {
    const container = document.getElementById(containerId);
    if (!container) return;

    container.innerHTML = "";

    parseJsonArray(jsonText).forEach(policy => {
        if (!policy || typeof policy !== "object") return;

        const item = document.createElement("div");
        item.className =
            "luxury-list-item policy-builder-item fade-in-up";
        item.innerHTML = `
            <span class="item-icon policy-builder-item__icon" aria-hidden="true">
                <i class="bi bi-check2"></i>
            </span>
            <div class="input-group input-group-sm policy-builder-item__value">
                <input type="number"
                       class="form-control text-center fw-bold policy-val"
                       value="${escapeAttribute(policy.val ?? "")}"
                       aria-label="قيمة العربون"
                       readonly>
                <select class="form-select fw-bold policy-type"
                        aria-label="نوع قيمة العربون"
                        disabled>
                    <option value="%" ${policy.type === "%" ? "selected" : ""}>%</option>
                    <option value="fixed" ${policy.type === "fixed" ? "selected" : ""}>ريال</option>
                </select>
            </div>
            <input type="text"
                   class="form-control policy-desc"
                   value="${escapeAttribute(policy.desc ?? "")}"
                   aria-label="وصف سياسة العربون"
                   readonly>
            ${buildItemActionsHtml("سياسة العربون")}
        `;

        container.appendChild(item);
    });
}

function serializeShiftsList() {
    const shifts = [];

    document
        .querySelectorAll("#shiftsContainer .luxury-list-item")
        .forEach(item => {
            const inputs = item.querySelectorAll("input");
            const name = inputs[0]?.value.trim() || "";
            const from = inputs[1]?.value || "";
            const to = inputs[2]?.value || "";

            if (name) shifts.push({ name, from, to });
        });

    return JSON.stringify(shifts);
}

function rebuildShiftsList(jsonText) {
    const container = document.getElementById("shiftsContainer");
    if (!container) return;

    container.innerHTML = "";

    parseJsonArray(jsonText).forEach(shift => {
        if (!shift || typeof shift !== "object") return;
        container.appendChild(createShiftListItem(shift, false));
    });
}

function serializeQuickRepliesList() {
    const replies = [];

    document
        .querySelectorAll("#quickRepliesContainer .luxury-list-item")
        .forEach(item => {
            const inputs = item.querySelectorAll("input");
            const title = inputs[0]?.value.trim() || "";
            const reply = inputs[1]?.value.trim() || "";

            if (title) replies.push({ title, reply });
        });

    return JSON.stringify(replies);
}

function rebuildQuickRepliesList(jsonText) {
    const container = document.getElementById("quickRepliesContainer");
    if (!container) return;

    container.innerHTML = "";

    parseJsonArray(jsonText).forEach(reply => {
        if (!reply || typeof reply !== "object") return;
        container.appendChild(createQuickReplyListItem(reply, false));
    });
}

// ============================================================================
// الهاتف والبريد
// ============================================================================

window.emergencyPhoneIti = null;

window.isEmergencyPhoneValid = function (itiInstance, inputElement) {
    if (!inputElement) return false;

    const rawValue = inputElement.value.replace(/[^0-9]/g, "");

    if (!rawValue) return false;

    if (itiInstance) {
        const countryData = itiInstance.getSelectedCountryData();

        if (countryData?.iso2 === "ye") {
            return /^(77|78|70|73|71)[0-9]{7}$/.test(rawValue);
        }

        if (
            rawValue.length === 9 &&
            /^(77|78|70|73|71)/.test(rawValue)
        ) {
            return true;
        }

        return itiInstance.isValidNumber();
    }

    return /^(77|78|70|73|71)[0-9]{7}$/.test(rawValue);
};

function initializeEmergencyPhone() {
    const phoneInput = document.getElementById("emergencyContact");
    if (!phoneInput || window.emergencyPhoneIti) return;

    if (typeof window.intlTelInput === "undefined") {
        window.setTimeout(initializeEmergencyPhone, 400);
        return;
    }

    window.emergencyPhoneIti = window.intlTelInput(phoneInput, {
        initialCountry: "ye",
        preferredCountries: ["ye", "sa", "ae", "eg"],
        separateDialCode: true,
        strictMode: true,
        dropdownContainer: document.body,
        utilsScript:
            "https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js"
    });

    phoneInput.addEventListener("input", validateEmergencyPhoneInput);
    phoneInput.addEventListener(
        "countrychange",
        validateEmergencyPhoneInput
    );
}

function validateEmergencyPhoneInput() {
    const phoneInput = document.getElementById("emergencyContact");
    const message = document.getElementById("emergencyPhoneMsg");

    if (!phoneInput) return;

    const wrapper = phoneInput.closest(".premium-input-group-phone");
    wrapper?.classList.remove(
        "glow-error",
        "glow-success",
        "is-invalid"
    );

    if (message) {
        message.innerHTML = "";
        message.className = "small mt-1 text-end d-block fw-bold";
    }

    if (!phoneInput.value.trim()) return;

    const isValid = window.isEmergencyPhoneValid(
        window.emergencyPhoneIti,
        phoneInput
    );

    if (isValid) {
        wrapper?.classList.add("glow-success");
        phoneInput.classList.remove("error-highlight", "is-invalid");

        if (message) {
            message.innerHTML =
                '<i class="bi bi-check-circle-fill"></i> رقم صحيح ومدعوم';
            message.classList.add("text-success");
        }
    } else {
        wrapper?.classList.add("glow-error", "is-invalid");

        if (message) {
            message.innerHTML =
                '<i class="bi bi-x-circle-fill"></i> صيغة الرقم خاطئة أو ناقصة';
            message.classList.add("text-danger");
        }
    }
}

function initializeEmailValidation() {
    const emailInput = document.getElementById("contactEmail");
    if (!emailInput) return;

    emailInput.addEventListener("blur", function () {
        const value = this.value.trim();
        const invalid = value !== "" && !isAllowedContactEmail(value);

        this.classList.toggle("is-invalid", invalid);
        this.classList.toggle("email-valid", !invalid && value !== "");
    });

    emailInput.addEventListener("input", function () {
        this.classList.remove("is-invalid", "email-valid");
    });
}

function isAllowedContactEmail(value) {
    return /^[a-zA-Z0-9._%+-]+@(gmail\.com|yahoo\.com|hotmail\.com|outlook\.com|icloud\.com)$/i.test(
        value
    );
}

// ============================================================================
// نافذة تطبيق السياسات الافتراضية - تستخدم أيضًا من إدارة المنشآت
// ============================================================================

window.showPolicyOptionsAlert = function (placeId) {
    const safePlaceId = Number.parseInt(placeId, 10);
    if (!Number.isInteger(safePlaceId) || safePlaceId <= 0) return;

    Swal.fire({
        title: "خطوة أخيرة ومهمة",
        html: `
            <div class="text-start mt-3" dir="rtl">
                <p class="text-muted fw-bold">
                    قبل استقبال الحجوزات، اختر طريقة إعداد سياسات المنشأة.
                </p>
                <div class="p-3 bg-light border rounded-3 mb-3 small">
                    <strong>السياسات الافتراضية تشمل:</strong>
                    <ul class="mb-0 ps-3 mt-2 text-secondary" style="line-height:1.8;">
                        <li>عربون مبدئي <b>20%</b> لتأكيد الحجز.</li>
                        <li>مهلة السداد: <b>12 ساعة</b> لتسديد العربون قبل الإلغاء.</li>
                        <li>نظام <b>اليوم الكامل</b> (استلام 2 ظهراً / تسليم 12 ظهراً).</li>
                        <li>إلغاء مجاني قبل <b>48 ساعة</b> من موعد الدخول.</li>
                        <li>غرامة الإلغاء المتأخر: <b>مصادرة العربون</b> بالكامل.</li>
                    </ul>
                </div>
            </div>
        `,
        icon: "info",
        showCancelButton: true,
        confirmButtonColor: "#00A6DA",
        cancelButtonColor: "#6c757d",
        confirmButtonText: "تطبيق السياسات الافتراضية",
        cancelButtonText: "ضبطها يدويًا",
        allowOutsideClick: false,
        customClass: { popup: "rounded-4 shadow-lg" }
    }).then(async result => {
        if (result.isConfirmed) {
            Swal.fire({
                title: "جاري تطبيق الإعدادات...",
                allowOutsideClick: false,
                showConfirmButton: false,
                didOpen: () => Swal.showLoading()
            });

            try {
                const response = await fetch(
                    withVerificationFlow("/Owner/Settings/ApplyDefaultPolicies"),
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            "X-Requested-With": "XMLHttpRequest"
                        },
                        credentials: "same-origin",
                        body: JSON.stringify(safePlaceId)
                    }
                );

                const data = await readJsonResponse(response);

                if (!response.ok || !data.success) {
                    throw new Error(
                        data.message || "تعذر تطبيق السياسات الافتراضية."
                    );
                }

                await Swal.fire({
                    icon: "success",
                    title: "تم تطبيق السياسات",
                    text: "يمكنك الآن متابعة إعداد منشأتك.",
                    confirmButtonColor: "#00A6DA"
                });

                window.location.reload();
            } catch (error) {
                Swal.fire({
                    icon: "error",
                    title: "تعذر تنفيذ العملية",
                    text: error.message,
                    confirmButtonColor: "#FF5A5F"
                });
            }

            return;
        }

        if (result.dismiss === Swal.DismissReason.cancel) {
            window.location.href =
                `/Owner/Settings/FacilitySettings?autoPlaceId=${safePlaceId}&mode=new`;
        }
    });
};

// ============================================================================
// أدوات عامة صغيرة
// ============================================================================

function getElementValue(elementId) {
    return document.getElementById(elementId)?.value?.trim() || "";
}

function getCheckboxValue(elementId) {
    return document.getElementById(elementId)?.checked === true;
}

function getCheckedRadioValue(name) {
    return (
        document.querySelector(`input[name="${name}"]:checked`)?.value || ""
    );
}

function toInteger(elementId, fallback = 0) {
    const value = Number.parseInt(getElementValue(elementId), 10);
    return Number.isFinite(value) ? value : fallback;
}

function toDecimal(elementId, fallback = 0) {
    const value = Number.parseFloat(getElementValue(elementId));
    return Number.isFinite(value) ? value : fallback;
}

function parseJsonArray(jsonText) {
    try {
        const parsed = JSON.parse(jsonText || "[]");
        return Array.isArray(parsed) ? parsed : [];
    } catch (error) {
        console.error("Invalid policy JSON:", error);
        return [];
    }
}

function cssEscape(value) {
    if (window.CSS?.escape) return window.CSS.escape(value);
    return value.replace(/["\\]/g, "\\$&");
}

function escapeAttribute(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

async function readJsonResponse(response) {
    const contentType = response.headers.get("content-type") || "";

    if (!contentType.includes("application/json")) {
        throw new Error("انتهت الجلسة أو أعاد الخادم استجابة غير متوقعة.");
    }

    return response.json();
}

function setPageBusy(isBusy, message = "") {
    const page = document.getElementById("facilitySettingsPage");
    if (!page) return;

    page.classList.toggle("is-loading", isBusy);
    page.setAttribute("aria-busy", isBusy ? "true" : "false");

    if (message) page.dataset.loadingMessage = message;
}

function setSaveButtonsBusy(isBusy) {
    document.querySelectorAll(".btn-royal-save, #verificationPolicySaveButton").forEach(button => {
        if (isBusy) {
            button.dataset.originalHtml = button.innerHTML;
            button.innerHTML =
                '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>جاري الحفظ...</span>';
        } else if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
            delete button.dataset.originalHtml;
        }

        button.disabled = isBusy;
    });
}

function showPageError(message) {
    Swal.fire({
        icon: "error",
        title: "تعذر تنفيذ العملية",
        text: message,
        confirmButtonColor: "#FF5A5F"
    });
}
// ============================================================================
// LUXURY 3D MOTION V1 — طبقة مرئية فقط
// لا تغيّر Payload أو المسارات أو قواعد السياسات.
// ============================================================================

document.addEventListener("DOMContentLoaded", initializeLuxuryFacilityExperience);

function initializeLuxuryFacilityExperience() {
    const page = document.getElementById("facilitySettingsPage");
    const form = document.getElementById("settingsForm");

    if (!page || !form || page.dataset.luxuryExperienceReady === "true") {
        return;
    }

    page.dataset.luxuryExperienceReady = "true";

    initializePolicyRevealMotion(page);
    initializePolicyTilt(page);
    initializePolicyTouchFeedback(page);
    initializePolicyDirtyState(page, form);
    initializePolicyTabVisualSync(page);
    initializePolicyPointerGlow(page);
    refreshLuxuryDynamicItems(page);

    // نفعّل حالة الحركة بعد التهيئة حتى لا تومض البطاقات عند أول تحميل.
    window.requestAnimationFrame(() => {
        page.classList.add("facility-motion-ready");
        revealVisiblePolicyElements(page);
    });
}

function setFacilityVisualSaveState(state) {
    const page = document.getElementById("facilitySettingsPage");
    const status = document.getElementById("facilityVisualSaveState");

    if (!page || !status) return;

    const stateMap = {
        saved: {
            className: "is-saved",
            text: "متزامن مع آخر حفظ"
        },
        dirty: {
            className: "is-dirty",
            text: "لديك تعديلات غير محفوظة"
        },
        saving: {
            className: "is-saving",
            text: "جاري حفظ السياسات..."
        },
        error: {
            className: "is-error",
            text: "تعذر الحفظ — التعديلات ما زالت موجودة"
        }
    };

    const safeState = stateMap[state] ? state : "saved";
    const config = stateMap[safeState];
    const text = status.querySelector(".facility-sync-state__text");

    status.classList.remove(
        "is-saved",
        "is-dirty",
        "is-saving",
        "is-error"
    );
    status.classList.add(config.className);

    if (text) {
        text.textContent = config.text;
    }

    page.classList.toggle(
        "has-unsaved-changes",
        safeState === "dirty" || safeState === "error"
    );

    page.classList.toggle(
        "is-visual-saving",
        safeState === "saving"
    );
}

function initializePolicyDirtyState(page, form) {
    const markDirty = event => {
        if (
            facilityPolicyState.isLoading ||
            facilityPolicyState.isSaving
        ) {
            return;
        }

        const target = event.target;

        if (
            target instanceof HTMLElement &&
            target.closest(".btn-royal-save")
        ) {
            return;
        }

        setFacilityVisualSaveState("dirty");
    };

    form.addEventListener("input", markDirty, { passive: true });
    form.addEventListener("change", markDirty, { passive: true });

    form.addEventListener("click", event => {
        const action = event.target.closest(
            ".btn-royal-add, .btn-delete, .btn-edit"
        );

        if (!action || facilityPolicyState.isSaving) return;

        window.setTimeout(() => {
            setFacilityVisualSaveState("dirty");
            refreshLuxuryDynamicItems(page);
        }, 40);
    });

    // القوائم تُبنى ديناميكيًا عند التحميل والإضافة.
    const observer = new MutationObserver(mutations => {
        const hasRelevantMutation = mutations.some(mutation => {
            return (
                mutation.type === "childList" &&
                (
                    mutation.target.closest?.(".policy-builder-list") ||
                    mutation.target.classList?.contains("policy-builder-list")
                )
            );
        });

        if (hasRelevantMutation) {
            refreshLuxuryDynamicItems(page);
        }
    });

    observer.observe(form, {
        childList: true,
        subtree: true
    });
}

function initializePolicyRevealMotion(page) {
    if (
        window.matchMedia("(prefers-reduced-motion: reduce)").matches
    ) {
        page.querySelectorAll(
            "[data-policy-reveal], .policy-card, .royal-card"
        ).forEach(element => {
            element.classList.add("is-policy-visible");
        });
        return;
    }

    const elements = Array.from(
        page.querySelectorAll(
            "[data-policy-reveal], .policy-card, .royal-card"
        )
    );

    elements.forEach((element, index) => {
        element.style.setProperty(
            "--facility-reveal-delay",
            `${Math.min(index * 38, 190)}ms`
        );
    });

    if (!("IntersectionObserver" in window)) {
        elements.forEach(element => {
            element.classList.add("is-policy-visible");
        });
        return;
    }

    const observer = new IntersectionObserver(
        entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;

                entry.target.classList.add("is-policy-visible");
                observer.unobserve(entry.target);
            });
        },
        {
            threshold: 0.08,
            rootMargin: "0px 0px -3% 0px"
        }
    );

    elements.forEach(element => observer.observe(element));

    page.__facilityRevealObserver = observer;
}

function revealVisiblePolicyElements(page) {
    const activePanel = page.querySelector(
        ".tab-pane-content[aria-hidden='false'], .tab-pane-content[style*='display: block']"
    );

    if (!activePanel) return;

    const elements = activePanel.querySelectorAll(
        "[data-policy-reveal], .policy-card, .royal-card"
    );

    elements.forEach((element, index) => {
        element.style.setProperty(
            "--facility-reveal-delay",
            `${Math.min(index * 34, 180)}ms`
        );

        window.setTimeout(() => {
            element.classList.add("is-policy-visible");
        }, 15);
    });
}

function initializePolicyTilt(page) {
    const finePointer = window.matchMedia(
        "(hover: hover) and (pointer: fine) and (min-width: 992px)"
    );

    if (!finePointer.matches) return;

    const surfaces = page.querySelectorAll(
        "[data-policy-tilt], .policy-card, .royal-card"
    );

    surfaces.forEach(surface => {
        if (surface.dataset.tiltReady === "true") return;

        surface.dataset.tiltReady = "true";

        surface.addEventListener("pointermove", event => {
            const rect = surface.getBoundingClientRect();

            if (rect.width <= 0 || rect.height <= 0) return;

            const px =
                (event.clientX - rect.left) / rect.width;
            const py =
                (event.clientY - rect.top) / rect.height;

            const rotateY = (px - 0.5) * 3.2;
            const rotateX = (0.5 - py) * 2.7;

            surface.style.setProperty(
                "--policy-rx",
                `${rotateX.toFixed(2)}deg`
            );
            surface.style.setProperty(
                "--policy-ry",
                `${rotateY.toFixed(2)}deg`
            );
            surface.style.setProperty(
                "--policy-shine-x",
                `${(px * 100).toFixed(1)}%`
            );
            surface.style.setProperty(
                "--policy-shine-y",
                `${(py * 100).toFixed(1)}%`
            );
        }, { passive: true });

        surface.addEventListener("pointerleave", () => {
            resetPolicyTilt(surface);
        }, { passive: true });
    });
}

function resetPolicyTilt(surface) {
    surface.style.setProperty("--policy-rx", "0deg");
    surface.style.setProperty("--policy-ry", "0deg");
    surface.style.setProperty("--policy-shine-x", "50%");
    surface.style.setProperty("--policy-shine-y", "0%");
}

function initializePolicyPointerGlow(page) {
    if (
        !window.matchMedia("(hover: hover) and (pointer: fine)").matches
    ) {
        return;
    }

    page.addEventListener("pointermove", event => {
        const card = event.target.closest(".policy-card, .royal-card");
        if (!card) return;

        const rect = card.getBoundingClientRect();
        const x = ((event.clientX - rect.left) / rect.width) * 100;
        const y = ((event.clientY - rect.top) / rect.height) * 100;

        card.style.setProperty(
            "--policy-shine-x",
            `${x.toFixed(1)}%`
        );
        card.style.setProperty(
            "--policy-shine-y",
            `${y.toFixed(1)}%`
        );
    }, { passive: true });
}

function initializePolicyTouchFeedback(page) {
    if (
        !window.matchMedia("(hover: none), (pointer: coarse)").matches
    ) {
        return;
    }

    const pressableSelector = [
        ".tab-btn",
        ".btn-royal-save",
        ".btn-royal-add",
        ".btn-action",
        ".policy-choice-card__content",
        ".policy-toggle-card"
    ].join(",");

    page.addEventListener("pointerdown", event => {
        const target = event.target.closest(pressableSelector);
        if (!target) return;

        target.classList.add("is-touch-pressed");
    }, { passive: true });

    const clearPressed = () => {
        page.querySelectorAll(".is-touch-pressed").forEach(element => {
            element.classList.remove("is-touch-pressed");
        });
    };

    page.addEventListener("pointerup", clearPressed, { passive: true });
    page.addEventListener("pointercancel", clearPressed, { passive: true });
    page.addEventListener("pointerleave", clearPressed, { passive: true });
}

function initializePolicyTabVisualSync(page) {
    const buttons = Array.from(
        page.querySelectorAll(".facility-policy-tabs .tab-btn")
    );

    if (buttons.length === 0) return;

    buttons.forEach(button => {
        button.addEventListener("click", () => {
            window.setTimeout(() => {
                revealVisiblePolicyElements(page);
                centerActiveFacilityTab(page);
            }, 30);
        });
    });

    centerActiveFacilityTab(page);
}

function centerActiveFacilityTab(page) {
    if (
        !window.matchMedia("(max-width: 991.98px)").matches
    ) {
        return;
    }

    const active = page.querySelector(
        ".facility-policy-tabs .tab-btn.active"
    );

    active?.scrollIntoView({
        behavior:
            window.matchMedia("(prefers-reduced-motion: reduce)").matches
                ? "auto"
                : "smooth",
        block: "nearest",
        inline: "center"
    });
}

function refreshLuxuryDynamicItems(page) {
    const dynamicItems = page.querySelectorAll(
        ".policy-builder-item, .luxury-list-item"
    );

    dynamicItems.forEach((item, index) => {
        item.style.setProperty(
            "--facility-item-index",
            String(index)
        );
    });

    // أي بطاقة أضيفت بعد التهيئة تدخل مباشرة بصورة ناعمة.
    page.querySelectorAll(
        ".policy-card:not(.is-policy-visible), .royal-card:not(.is-policy-visible)"
    ).forEach(card => {
        card.classList.add("is-policy-visible");
    });
}
