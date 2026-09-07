/**
 * ============================================================================
 * دليلك اليمني — إدارة منشآت الأدمن
 * الوظائف المحفوظة:
 * - عرض التفاصيل
 * - الموافقة والرفض والتعليق والحذف والتنشيط
 * - إعادة تعيين المالك
 * - معرض صور الغرف والخدمات
 * - البحث داخل التبويبات
 * - بوابة اختيار الفئة والمالك
 * ============================================================================
 */
(() => {
    "use strict";

    const COLORS = Object.freeze({
        primary: "#00A6DA",
        primaryDark: "#008AB5",
        coral: "#FF5A5F",
        coralDark: "#E5484D",
        success: "#15966A",
        warning: "#D99218",
        muted: "#718395"
    });

    const managementRoot =
        document.querySelector(
            "[data-admin-places-page]"
        );

    const isArabic =
        String(
            managementRoot?.dataset.language ||
            document.documentElement.lang ||
            "ar"
        )
            .toLowerCase()
            .startsWith("ar");

    const locale =
        managementRoot?.dataset.locale ||
        (isArabic ? "ar-YE" : "en-US");

    const T = (ar, en) =>
        isArabic ? ar : en;

    const STATUS_META = Object.freeze({
        Active: {
            label: T("نشطة ومعتمدة", "Active & approved"),
            className: "is-active",
            icon: "bi-check2-circle"
        },
        Pending: {
            label: T("قيد المراجعة", "Pending review"),
            className: "is-pending",
            icon: "bi-clock-history"
        },
        WaitOwner: {
            label: T("بانتظار المالك", "Waiting for owner"),
            className: "is-waiting",
            icon: "bi-hourglass-split"
        },
        Rejected: {
            label: T("مرفوضة", "Rejected"),
            className: "is-rejected",
            icon: "bi-x-circle"
        },
        Suspended: {
            label: T("معلّقة", "Suspended"),
            className: "is-suspended",
            icon: "bi-pause-circle"
        }
    });

    let placeDetailsModal = null;

    let activeCurrency = {
        name: "ريال يمني",
        code: "YER",
        symbol: "ر.ي",
        divisor: 1
    };

    function byId(id) {
        return document.getElementById(id);
    }

    function all(selector, scope = document) {
        return Array.from(scope.querySelectorAll(selector));
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function escapeAttribute(value) {
        return escapeHtml(value).replace(/`/g, "&#096;");
    }

    function normalizeSearch(value) {
        return String(value ?? "")
            .toLowerCase()
            .replace(/[أإآ]/g, "ا")
            .replace(/ة/g, "ه")
            .replace(/ى/g, "ي")
            .replace(/\s+/g, " ")
            .trim();
    }

    function safeArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function displayValue(value, fallback = T("غير محدد", "Unspecified")) {
        const text = String(value ?? "").trim();
        return text || fallback;
    }

    function toNumber(value) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function formatNumber(value, decimals = 0) {
        return new Intl.NumberFormat(
            locale,
            {
                minimumFractionDigits:
                    decimals,
                maximumFractionDigits:
                    decimals
            }
        ).format(
            toNumber(value)
        );
    }

    function formatMoney(
        value,
        readyText = ""
    ) {
        if (
            readyText &&
            String(readyText).trim()
        ) {
            return String(
                readyText
            ).trim();
        }

        const numeric =
            toNumber(value);

        const decimals =
            activeCurrency.code === "YER"
                ? 0
                : 2;

        const number =
            formatNumber(
                numeric,
                decimals
            );

        return activeCurrency.code === "USD"
            ? `$${number}`
            : `${number} ${activeCurrency.symbol}`;
    }

    function convertYerForPolicy(value) {
        const raw =
            toNumber(value);

        const divisor =
            toNumber(
                activeCurrency.divisor
            ) || 1;

        if (
            activeCurrency.code === "YER"
        ) {
            return Math.round(raw);
        }

        return Math.round(
            (raw / divisor) *
            100
        ) / 100;
    }

    function setText(id, value, fallback = T("غير محدد", "Unspecified")) {
        const element = byId(id);
        if (element) element.textContent = displayValue(value, fallback);
    }

    function setHtml(id, html) {
        const element = byId(id);
        if (element) element.innerHTML = html;
    }

    function showLoading(title = T("جاري التحميل...", "Loading...")) {
        if (!window.Swal) return;
        Swal.fire({
            title,
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false,
            customClass: { popup: "admin-places-swal admin-places-swal--loading" },
            didOpen: () => Swal.showLoading()
        });
    }

    function showError(message) {
        if (window.Swal) {
            return Swal.fire({
                icon: "error",
                title: T("تعذر إكمال العملية", "Unable to complete the action"),
                text: message || T("حدث خطأ غير متوقع.", "An unexpected error occurred."),
                confirmButtonText: T("موافق", "OK"),
                confirmButtonColor: COLORS.primary,
                customClass: { container: "admin-places-swal-layer", popup: "admin-places-swal" }
            });
        }
        window.alert(message || T("حدث خطأ غير متوقع.", "An unexpected error occurred."));
        return Promise.resolve();
    }

    function showSuccess(message) {
        if (!window.Swal) return Promise.resolve();
        return Swal.fire({
            icon: "success",
            title: T("تم بنجاح", "Completed successfully"),
            text: message,
            confirmButtonText: T("موافق", "OK"),
            confirmButtonColor: COLORS.primary,
            customClass: { popup: "admin-places-swal" }
        });
    }

    async function fetchJson(url, options = {}) {
        const response = await fetch(url, options);
        let data = null;
        try {
            data = await response.json();
        } catch (_) {
            data = null;
        }

        if (!response.ok) {
            throw new Error(data?.message || T("حدث خطأ أثناء الاتصال بالخادم.", "A server connection error occurred."));
        }

        return data;
    }

    function getStatusMeta(status) {
        return STATUS_META[String(status ?? "").trim()] || {
            label: displayValue(status, "غير محددة"),
            className: "is-neutral",
            icon: "bi-info-circle"
        };
    }

    function splitDescription(description) {
        const raw = String(description ?? "").trim();
        if (!raw || raw === "لا يوجد") {
            return { bio: "لا يوجد وصف مضاف لهذه المنشأة.", details: [] };
        }

        const marker = "--- تفاصيل المنشأة ---";
        const parts = raw.split(marker);
        const bio = parts[0].trim() || "لا توجد نبذة عامة.";
        const details = parts.length > 1
            ? parts.slice(1).join(marker).split(/\r?\n/).map(line => line.trim()).filter(Boolean)
            : [];

        return { bio, details };
    }

    function renderDescription(description) {
        const parsed = splitDescription(description);
        let html = `<p class="admin-detail-description__bio">${escapeHtml(parsed.bio).replace(/\r?\n/g, "<br>")}</p>`;

        if (parsed.details.length) {
            html += `<div class="admin-detail-specs">`;
            parsed.details.forEach(line => {
                const separator = line.indexOf(":");
                if (separator > -1) {
                    const key = line.slice(0, separator).trim();
                    const value = line.slice(separator + 1).trim();
                    html += `<div><strong>${escapeHtml(key)}</strong><span>${escapeHtml(value || "—")}</span></div>`;
                } else {
                    html += `<div class="admin-detail-specs__full"><span>${escapeHtml(line)}</span></div>`;
                }
            });
            html += `</div>`;
        }

        return html;
    }

    function renderAmenities(amenities) {
        const list = safeArray(amenities);
        if (!list.length) {
            return `<div class="admin-detail-empty-inline"><i class="bi bi-stars"></i><span>${T("لا توجد مرافق مضافة.", "No amenities added.")}</span></div>`;
        }

        return list.map(item => `
            <span class="admin-detail-chip">
                <i class="bi ${escapeAttribute(item.icon || "bi-patch-check")}"></i>
                ${escapeHtml(displayValue(item.name, "مرفق"))}
            </span>
        `).join("");
    }

    function renderGallery(images) {
        const list = safeArray(images).filter(Boolean);
        if (!list.length) {
            return `<div class="admin-detail-empty-inline"><i class="bi bi-image"></i><span>${T("لم يتم رفع صور إضافية.", "No additional photos uploaded.")}</span></div>`;
        }

        return list.map((image, index) => {
            const url = String(image);
            return `
                <button type="button" class="admin-detail-gallery__item" data-admin-image="${escapeAttribute(url)}" aria-label="${T("تكبير الصورة", "Enlarge photo")} ${index + 1}">
                    <img src="${escapeAttribute(url)}" onerror="this.src='/images/placeholder.jpg'" alt="${T("صورة المنشأة", "Place photo")} ${index + 1}">
                    <span><i class="bi bi-arrows-fullscreen"></i></span>
                </button>
            `;
        }).join("");
    }

    function renderUnits(packages, rooms) {
        const packageList = safeArray(packages);
        const roomList = safeArray(rooms);
        const cards = [];

        const formatClockValue = value => {
            if (value === null || value === undefined || value === "") return "";

            const text = String(value).trim();
            const match = text.match(/(\d{1,2}):(\d{2})/);
            if (!match) return text;

            const hours = Number(match[1]);
            const minutes = match[2];
            if (!Number.isFinite(hours)) return text;

            const suffix = hours >= 12 ? "م" : "ص";
            const displayHours = hours % 12 || 12;
            return `${displayHours}:${minutes} ${suffix}`;
        };

        packageList.forEach(item => {
            const startTime = formatClockValue(item.startTime);
            const endTime = formatClockValue(item.endTime);
            const hasRange = Boolean(startTime && endTime);
            const isActive = item.isActive !== false;

            cards.push(`
                <article class="admin-detail-card admin-detail-card--package ${isActive ? "is-active" : "is-inactive"}">
                    <span class="admin-detail-card__icon"><i class="bi bi-box-seam"></i></span>
                    <div class="admin-detail-card__content">
                        <div class="admin-detail-card__title-row">
                            <h4>${escapeHtml(displayValue(item.name, T("باقة", "Package")))}</h4>
                            <span class="admin-detail-state ${isActive ? "is-active" : "is-inactive"}">
                                <i class="bi ${isActive ? "bi-check-circle-fill" : "bi-pause-circle-fill"}"></i>
                                ${isActive ? T("مفعّلة", "Active") : T("متوقفة", "Inactive")}
                            </span>
                        </div>
                        <strong>${formatMoney(item.price, item.priceText)}</strong>
                        <div class="admin-detail-card__meta">
                            <span><i class="bi bi-people"></i>${formatNumber(item.capacity)} ${T("أشخاص", "people")}</span>
                            <span><i class="bi bi-calendar2-range"></i>${escapeHtml(displayValue(item.time, T("الفترة غير محددة", "Period not specified")))}</span>
                            ${hasRange ? `<span class="admin-detail-time-range"><i class="bi bi-clock"></i>${escapeHtml(startTime)} <b>—</b> ${escapeHtml(endTime)}</span>` : ""}
                        </div>
                    </div>
                </article>
            `);
        });

        roomList.forEach(item => {
            const imageUrl = item.imageUrl || item.ImageUrl || "";
            const roomUnits = Array.isArray(item.units) ? item.units : [];
            const isSuite = item.isSuite === true || Number(item.internalRoomsCount || 0) > 0 || String(item.roomType || "").includes("جناح");
            const internalRoomsCount = Math.max(Number(item.internalRoomsCount || 0), 0);
            const floorCount = new Set(roomUnits.map(unit => unit.floorNumber ?? 0)).size;
            const unitNumbers = roomUnits.slice(0, 8).map(unit => {
                const floor = displayValue(unit.floorLabel, `الطابق ${unit.floorNumber ?? 0}`);
                return `<span class="admin-room-unit-chip"><b>${escapeHtml(unit.roomNumber)}</b><small>${escapeHtml(floor)}</small></span>`;
            }).join("");
            const remainingUnits = Math.max(roomUnits.length - 8, 0);
            const galleryButton = imageUrl
                ? `<button type="button" class="admin-detail-card__gallery" data-room-title="${escapeAttribute(displayValue(item.name, T("الوحدة", "Unit")))}" data-room-images="${escapeAttribute(imageUrl)}"><i class="bi bi-images"></i>${T("الصور", "Photos")}</button>`
                : `<span class="admin-detail-card__no-image"><i class="bi bi-image"></i>${T("بدون صور", "No photos")}</span>`;

            cards.push(`
                <article class="admin-detail-card ${isSuite ? "admin-detail-card--suite" : "admin-detail-card--room"}">
                    <span class="admin-detail-card__icon ${isSuite ? "admin-detail-card__icon--suite" : "admin-detail-card__icon--coral"}"><i class="bi ${isSuite ? "bi-buildings" : "bi-door-open"}"></i></span>
                    <div class="admin-detail-card__content">
                        <div class="admin-detail-card__title-row">
                            <h4>${escapeHtml(displayValue(item.name, T("وحدة", "Unit")))}</h4>
                            <div class="admin-detail-card__title-actions">
                                <span class="admin-unit-kind ${isSuite ? "is-suite" : "is-room"}"><i class="bi ${isSuite ? "bi-gem" : "bi-door-open"}"></i>${isSuite ? T("جناح", "Suite") : T("غرفة", "Room")}</span>
                                ${galleryButton}
                            </div>
                        </div>
                        <strong>${formatMoney(item.price, item.priceText)}</strong>
                        <div class="admin-detail-card__meta">
                            <span><i class="bi ${isSuite ? "bi-buildings" : "bi-door-open"}"></i>${formatNumber(item.qty)} ${isSuite ? T("جناح فعلي", "actual suites") : T("غرفة فعلية", "actual rooms")}</span>
                            <span><i class="bi bi-layers"></i>${formatNumber(floorCount)} ${T("طابق", "floors")}</span>
                            <span><i class="bi bi-people"></i>${formatNumber(item.maxAdults || 1)} ${T("بالغ", "adults")} / ${formatNumber(item.maxChildren || 0)} ${T("طفل", "children")}</span>
                            ${isSuite && internalRoomsCount > 0 ? `<span class="admin-suite-internal-count"><i class="bi bi-grid-1x2"></i>${formatNumber(internalRoomsCount)} ${T("غرف داخلية في الجناح", "internal rooms in suite")}</span>` : ""}
                            ${!isSuite && item.bedsCount ? `<span><i class="bi bi-moon-stars"></i>${formatNumber(item.bedsCount)} سرير${item.bedType ? ` · ${escapeHtml(item.bedType)}` : ""}</span>` : ""}
                            ${item.areaSquareMeters ? `<span><i class="bi bi-aspect-ratio"></i>${formatNumber(item.areaSquareMeters)} م²</span>` : ""}
                        </div>
                        ${item.desc ? `<p>${escapeHtml(item.desc)}</p>` : ""}
                        ${unitNumbers ? `<div class="admin-room-units-preview">${unitNumbers}${remainingUnits ? `<span class="admin-room-unit-chip admin-room-unit-chip--more">+${formatNumber(remainingUnits)}</span>` : ""}</div>` : ""}
                    </div>
                </article>
            `);
        });

        return cards.length
            ? cards.join("")
            : `<div class="admin-detail-empty-inline"><i class="bi bi-box"></i><span>لا توجد غرف أو أجنحة أو باقات مضافة.</span></div>`;
    }

    function renderServices(services) {
        const list = safeArray(services);
        if (!list.length) {
            return `<div class="admin-detail-empty-inline"><i class="bi bi-bag"></i><span>${T("لا توجد خدمات إضافية.", "No additional services.")}</span></div>`;
        }

        return list.map((service, index) => {
            const images = String(service.image || "").split(",").map(item => item.trim()).filter(Boolean);
            const firstImage = images[0] || "/images/default-service.jpg";
            const encodedImages = encodeURIComponent(JSON.stringify(images));
            const hiddenClass = index >= 3 ? " is-hidden admin-service-hidden" : "";

            return `
                <article class="admin-detail-service${hiddenClass}">
                    <button type="button" class="admin-detail-service__image" data-service-images="${escapeAttribute(encodedImages)}" data-service-name="${escapeAttribute(displayValue(service.name, T("الخدمة", "service")))}" ${images.length ? "" : "disabled"}>
                        <img src="${escapeAttribute(firstImage)}" onerror="this.src='/images/default-service.jpg'" alt="صورة ${escapeAttribute(displayValue(service.name, T("الخدمة", "service")))}">
                        ${images.length > 1 ? `<span>+${images.length - 1}</span>` : ""}
                    </button>
                    <div class="admin-detail-service__copy">
                        <div><h4>${escapeHtml(displayValue(service.name, T("خدمة إضافية", "Additional service")))}</h4><strong>${formatMoney(service.price, service.priceText)}</strong></div>
                        <p>${escapeHtml(displayValue(service.description, T("لا يوجد وصف مضاف لهذه الخدمة.", "No description added for this service.")))}</p>
                    </div>
                </article>
            `;
        }).join("") + (list.length > 3
            ? `<button type="button" class="admin-detail-show-more" data-show-more-services><i class="bi bi-chevron-down"></i>${T("عرض بقية الخدمات", "Show more services")}</button>`
            : "");
    }

    function policyPenaltyText(policy) {
        const type = policy?.penaltyType;
        const value = toNumber(policy?.penaltyValue);
        if (type === "LoseDeposit") {
            return T(
                "مصادرة العربون بالكامل",
                "Forfeit the full deposit"
            );
        }

        if (type === "FirstNight") {
            return T(
                "خصم قيمة أول ليلة",
                "Charge the first night"
            );
        }

        if (type === "DeductPercentage") {
            return isArabic
                ? `خصم نسبة ${formatNumber(value)}%`
                : `Deduct ${formatNumber(value)}%`;
        }

        if (type === "DeductFixed") {
            const ready =
                policy?.penaltyValueText;

            return isArabic
                ? `خصم مبلغ ${formatMoney(value, ready)}`
                : `Deduct ${formatMoney(value, ready)}`;
        }

        return T(
            "بدون غرامة",
            "No penalty"
        );
    }

    function renderJsonList(raw, type) {
        try {
            const list = JSON.parse(raw || "[]");
            if (!Array.isArray(list) || !list.length) return "";

            if (type === "deposit") {
                return list.map(item => {
                    const isPercent =
                        item.type === "%";

                    const display =
                        isPercent
                            ? `${escapeHtml(item.val ?? "0")}%`
                            : formatMoney(
                                convertYerForPolicy(
                                    item.val
                                )
                            );

                    return `<li><i class="bi bi-check2-circle"></i><span><strong>${display}</strong>${escapeHtml(item.desc || "")}</span></li>`;
                }).join("");
            }

            return list.map(item => `<li><i class="bi bi-shield-check"></i><span>${escapeHtml(typeof item === "string" ? item : JSON.stringify(item))}</span></li>`).join("");
        } catch (_) {
            return "";
        }
    }

    function renderOperationPolicy(policy) {
        const operationMode = String(policy?.operationMode || "");
        if (operationMode === "FullDay" || operationMode === "Daily") {
            return `
                <li><span>نظام التشغيل</span><strong>يوم كامل</strong></li>
                <li><span>وقت الدخول</span><strong>${escapeHtml(displayValue(policy?.checkIn))}</strong></li>
                <li><span>وقت الخروج</span><strong>${escapeHtml(displayValue(policy?.checkOut))}</strong></li>
            `;
        }

        let shifts = [];
        try {
            shifts = JSON.parse(policy?.shiftsJson || "[]");
        } catch (_) {
            shifts = [];
        }

        const shiftRows = Array.isArray(shifts) && shifts.length
            ? shifts.map(shift => `<li><span>${escapeHtml(displayValue(shift.name, "فترة"))}</span><strong dir="ltr">${escapeHtml(displayValue(shift.from, "--"))} - ${escapeHtml(displayValue(shift.to, "--"))}</strong></li>`).join("")
            : `<li><span>الفترات</span><strong>لم يتم تحديدها</strong></li>`;

        return `<li><span>نظام التشغيل</span><strong>نظام فترات</strong></li>${shiftRows}`;
    }

    function renderPolicy(policy) {
        if (!policy) {
            return `
                <div class="admin-detail-policy-empty">
                    <span><i class="bi bi-exclamation-triangle"></i></span>
                    <h3>السياسات غير مكتملة</h3>
                    <p>لم يقم المالك بإدخال سياسات الحجز والإلغاء لهذه المنشأة حتى الآن.</p>
                </div>
            `;
        }

        const depositItems = renderJsonList(policy.depositJson, "deposit");
        const securityItems = renderJsonList(policy.securityJson, "security");

        return `
            <div class="admin-detail-policy-grid">
                <article class="admin-detail-policy-card">
                    <header><span><i class="bi bi-clock-history"></i></span><div><h3>النظام التشغيلي</h3><p>مواعيد وآلية تشغيل المنشأة</p></div></header>
                    <ul class="admin-detail-policy-list">${renderOperationPolicy(policy)}</ul>
                </article>
                <article class="admin-detail-policy-card admin-detail-policy-card--coral">
                    <header><span><i class="bi bi-x-circle"></i></span><div><h3>سياسة الإلغاء</h3><p>الفترة المجانية والغرامة</p></div></header>
                    <ul class="admin-detail-policy-list">
                        <li><span>إلغاء مجاني قبل</span><strong>${formatNumber(policy.freeCancelHours)} ساعة</strong></li>
                        <li><span>نوع الغرامة</span><strong>${escapeHtml(policyPenaltyText(policy))}</strong></li>
                    </ul>
                </article>
                <article class="admin-detail-policy-card admin-detail-policy-card--wide">
                    <header><span><i class="bi bi-cash-coin"></i></span><div><h3>العربون والدفع</h3><p>دفعات الحجز والمهلة المتاحة</p></div></header>
                    <ul class="admin-detail-policy-points">
                        ${depositItems || `<li><i class="bi bi-info-circle"></i><span>لا توجد سياسة عربون محددة.</span></li>`}
                        ${toNumber(policy.paymentTimeout) > 0 ? `<li><i class="bi bi-alarm"></i><span>مهلة السداد: <strong>${formatNumber(policy.paymentTimeout)} ساعة</strong></span></li>` : ""}
                    </ul>
                </article>
                <article class="admin-detail-policy-card admin-detail-policy-card--wide">
                    <header><span><i class="bi bi-shield-lock"></i></span><div><h3>شروط وضمانات السلامة</h3><p>الضمانات المطلوبة من العميل</p></div></header>
                    <ul class="admin-detail-policy-points">${securityItems || `<li><i class="bi bi-info-circle"></i><span>لا توجد ضمانات محددة.</span></li>`}</ul>
                </article>
            </div>
        `;
    }

    function resetDetailsTabs() {
        const basicTab = document.querySelector('#placeDetailsTabs [data-bs-target="#tab-basic"]');
        if (basicTab) basicTab.click();
    }

    async function fetchAndShowDetails(placeId, triggerButton = null) {
        const originalHtml = triggerButton?.innerHTML;
        if (triggerButton) {
            triggerButton.disabled = true;
            triggerButton.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>تحميل</span>';
        }

        try {
            const data = await fetchJson(`/Admin/Places/GetPlaceDetails/${encodeURIComponent(placeId)}`);

            activeCurrency = {
                name:
                    data.currency?.name ||
                    activeCurrency.name,

                code:
                    data.currency?.code ||
                    activeCurrency.code,

                symbol:
                    data.currency?.symbol ||
                    activeCurrency.symbol,

                divisor:
                    toNumber(
                        data.currency?.divisor
                    ) || 1
            };

            const statusMeta =
                getStatusMeta(
                    data.status
                );

            setText("modal-place-name", data.name, T("منشأة بدون اسم", "Unnamed place"));
            setText("modal-place-category", data.category);
            setText("modal-place-city", data.city);
            setText("modal-place-owner", data.owner);
            setText("modal-place-phone", data.phone, T("غير متوفر", "Unavailable"));
            setText("modal-place-address", data.address, T("لم يتم إدخال عنوان تفصيلي", "No detailed address entered"));
            setText("modal-place-availability", data.isAvailable ? T("متاحة للعملاء", "Available to customers") : T("غير متاحة حاليًا", "Currently unavailable"));

            const cover = byId("modal-cover-image");
            if (cover) cover.src = data.coverImage || "/images/placeholder.jpg";

            const status = byId("modal-place-status");
            if (status) {
                status.className = `admin-detail-status ${statusMeta.className}`;
                status.innerHTML = `<i class="bi ${statusMeta.icon}"></i>${escapeHtml(statusMeta.label)}`;
            }

            const price = byId("modal-place-price");
            if (price) {
                const priceText =
                    String(
                        data.price || ""
                    ).trim();

                const isZero =
                    toNumber(
                        data.priceValue
                    ) <= 0;

                price.hidden =
                    isZero;

                price.textContent =
                    isZero
                        ? ""
                        : priceText;
            }

            setHtml("modal-place-desc", renderDescription(data.description));
            setHtml("modal-amenities-list", renderAmenities(data.amenities));
            setHtml("modal-gallery-list", renderGallery(data.images));
            setHtml("modal-packages-list", renderUnits(data.packages, data.rooms));
            setHtml("modal-services-list", renderServices(data.services));
            setHtml("modal-policies-content", renderPolicy(data.policy));

            const mapLink = byId("modal-place-coords");
            if (mapLink) {
                const coordinates = String(data.coordinates || "").trim();
                if (coordinates) {
                    mapLink.href = coordinates.startsWith("http")
                        ? coordinates
                        : `https://www.google.com/maps?q=${encodeURIComponent(coordinates)}`;
                    mapLink.hidden = false;
                } else {
                    mapLink.removeAttribute("href");
                    mapLink.hidden = true;
                }
            }

            const reasonBox = byId("modal-reason-box");
            const reasonText = String(data.rejectionReason || "").trim();
            if (reasonBox) {
                reasonBox.hidden = !reasonText;
                if (reasonText) {
                    const prefix =
                        data.status === "Pending"
                            ? T(
                                "سبب الرفض السابق بعد إعادة الإرسال: ",
                                "Previous rejection reason after resubmission: "
                            )
                            : T(
                                "سبب الرفض أو التعليق: ",
                                "Rejection or suspension reason: "
                            );
                    setText("modal-place-reason", `${prefix}${reasonText}`);
                }
            }

            const packagesTab = byId("packages-tab-li");
            const policiesTab = byId("policies-tab-li");
            const hasBooking = toNumber(data.bookingSystemId) > 0;
            if (packagesTab) packagesTab.hidden = !hasBooking;
            if (policiesTab) policiesTab.hidden = !hasBooking;

            resetDetailsTabs();
            if (!placeDetailsModal) {
                const modalElement = byId("placeDetailsModal");
                if (modalElement && window.bootstrap?.Modal) {
                    placeDetailsModal = bootstrap.Modal.getOrCreateInstance(modalElement);
                }
            }
            placeDetailsModal?.show();
        } catch (error) {
            console.error(error);
            await showError(error.message || "حدثت مشكلة أثناء جلب بيانات المنشأة.");
        } finally {
            if (triggerButton) {
                triggerButton.disabled = false;
                triggerButton.innerHTML = originalHtml;
            }
        }
    }

    function performPlaceAction(url, placeId, options) {
        const config = Object.assign({
            title: "تأكيد الإجراء",
            text: "هل تريد تنفيذ هذا الإجراء؟",
            confirmText: "نعم، نفّذ",
            confirmColor: COLORS.primary,
            successFallback: "تم تنفيذ الإجراء بنجاح."
        }, options || {});

        return Swal.fire({
            title: config.title,
            text: config.text,
            icon: "warning",
            showCancelButton: true,
            reverseButtons: true,
            confirmButtonColor: config.confirmColor,
            cancelButtonColor: COLORS.muted,
            confirmButtonText: config.confirmText,
            cancelButtonText: T("إلغاء", "Cancel"),
            customClass: { popup: "admin-places-swal", actions: "admin-places-swal__actions" }
        }).then(async result => {
            if (!result.isConfirmed) return;
            showLoading(T("جاري تنفيذ الإجراء...", "Processing..."));
            try {
                const data = await fetchJson(url, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(Number(placeId))
                });
                await showSuccess(data?.message || config.successFallback);
                window.location.reload();
            } catch (error) {
                await showError(error.message);
            }
        });
    }

    function performActionWithReason(url, placeId, options) {
        const config = Object.assign({
            title: T("توضيح سبب الإجراء", "Action reason"),
            label: T("اكتب السبب الذي سيظهر للمالك", "Enter the reason that will be shown to the owner"),
            placeholder: T("اكتب السبب بوضوح...", "Enter a clear reason..."),
            confirmText: T("تأكيد وإرسال", "Confirm & send"),
            confirmColor: COLORS.coral
        }, options || {});

        return Swal.fire({
            title: config.title,
            input: "textarea",
            inputLabel: config.label,
            inputPlaceholder: config.placeholder,
            inputAttributes: { maxlength: "1000", "aria-label": config.label },
            showCancelButton: true,
            reverseButtons: true,
            confirmButtonColor: config.confirmColor,
            cancelButtonColor: COLORS.muted,
            confirmButtonText: config.confirmText,
            cancelButtonText: T("إلغاء", "Cancel"),
            customClass: { popup: "admin-places-swal admin-places-swal--reason", input: "admin-places-swal__textarea" },
            inputValidator: value => {
                if (!String(value || "").trim()) return T("يجب كتابة السبب قبل المتابعة.", "A reason is required.");
                if (String(value).trim().length < 5) return T("يرجى كتابة سبب أوضح.", "Please enter a clearer reason.");
                return undefined;
            }
        }).then(async result => {
            if (!result.isConfirmed) return;
            showLoading(T("جاري حفظ القرار...", "Saving decision..."));
            try {
                const data = await fetchJson(url, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ Id: Number(placeId), Reason: String(result.value).trim() })
                });
                await showSuccess(data?.message || T("تم حفظ القرار بنجاح.", "Decision saved successfully."));
                window.location.reload();
            } catch (error) {
                await showError(error.message);
            }
        });
    }

    async function openReassignOwner(placeId) {
        showLoading("جاري جلب قائمة الملاك...");
        try {
            const owners = await fetchJson("/Admin/Places/GetOwnersList");
            const options = {};
            safeArray(owners).forEach(owner => {
                options[owner.id] = owner.name;
            });

            const result = await Swal.fire({
                title: "إعادة تعيين المالك",
                text: "اختر المالك الذي ستُرسل إليه المنشأة من جديد.",
                input: "select",
                inputOptions: options,
                inputPlaceholder: "اختر المالك",
                showCancelButton: true,
                reverseButtons: true,
                confirmButtonColor: COLORS.primary,
                cancelButtonColor: COLORS.muted,
                confirmButtonText: "تأكيد وإرسال",
                cancelButtonText: T("إلغاء", "Cancel"),
                customClass: { popup: "admin-places-swal" },
                inputValidator: value => !value ? "يجب اختيار مالك من القائمة." : undefined
            });

            if (!result.isConfirmed) return;

            const formData = new FormData();
            formData.append("placeId", String(placeId));
            formData.append("newOwnerId", String(result.value));
            showLoading("جاري إعادة التعيين...");
            const data = await fetchJson("/Admin/Places/ReassignPlaceOwner", { method: "POST", body: formData });
            await showSuccess(data?.message || "تم إعادة تعيين المنشأة بنجاح.");
            window.location.reload();
        } catch (error) {
            await showError(error.message);
        }
    }

    function initializePlaceSearch() {
        const input = byId("adminPlacesSearch");
        const clearButton = document.querySelector("[data-clear-admin-search]");
        if (!input) return;

        const applySearch = () => {
            const query = normalizeSearch(input.value);
            if (clearButton) clearButton.hidden = !query;

            all(".tab-pane-content").forEach(pane => {
                const rows = all("[data-place-row]", pane);
                let visibleCount = 0;
                rows.forEach(row => {
                    const haystack = normalizeSearch(row.dataset.search || row.textContent);
                    const visible = !query || haystack.includes(query);
                    row.hidden = !visible;
                    if (visible) visibleCount += 1;
                });

                const searchEmpty = pane.querySelector("[data-search-empty]");
                if (searchEmpty) searchEmpty.hidden = !query || !rows.length || visibleCount > 0;
            });
        };

        input.addEventListener("input", applySearch);
        clearButton?.addEventListener("click", () => {
            input.value = "";
            applySearch();
            input.focus();
        });
    }

    function initializeDetailsInteractions() {
        const modal = byId("placeDetailsModal");
        if (!modal) return;

        modal.addEventListener("click", event => {
            const imageButton = event.target.closest("[data-admin-image]");
            if (imageButton) {
                window.openAdminImage(imageButton.dataset.adminImage);
                return;
            }

            const roomGallery = event.target.closest("[data-room-images]");
            if (roomGallery) {
                window.openRoomGallery(roomGallery.dataset.roomTitle, roomGallery.dataset.roomImages);
                return;
            }

            const serviceGallery = event.target.closest("[data-service-images]");
            if (serviceGallery && !serviceGallery.disabled) {
                window.openServiceGallery(serviceGallery.dataset.serviceImages, serviceGallery.dataset.serviceName);
                return;
            }

            const showMore = event.target.closest("[data-show-more-services]");
            if (showMore) {
                all(".admin-service-hidden", modal).forEach(item => item.classList.remove("is-hidden", "admin-service-hidden"));
                showMore.remove();
            }
        });
    }

    function initializeManagementActions() {
        const root = document.querySelector("[data-admin-places-page]");
        if (!root) return;

        const modalElement = byId("placeDetailsModal");
        if (modalElement && window.bootstrap?.Modal) {
            placeDetailsModal = bootstrap.Modal.getOrCreateInstance(modalElement);
        }

        // IMPORTANT: action-menu items may be portalled to <body> so they no longer
        // bubble through the page root. Delegate from document and scope the event
        // to this page/action-menu only. This keeps Approve/Reject/etc. working
        // on desktop and mobile without changing any backend endpoint.
        document.addEventListener("click", event => {
            const button = event.target.closest("button[data-id]");
            if (!button) return;

            const belongsToPlacesPage =
                root.contains(button) ||
                Boolean(button.closest("[data-place-action-menu]"));

            if (!belongsToPlacesPage) return;

            const id = button.dataset.id;

            if (button.classList.contains("view-btn")) {
                fetchAndShowDetails(id, button);
            } else if (button.classList.contains("approve-btn")) {
                performPlaceAction("/Admin/Places/ApprovePlace", id, {
                    title: T("اعتماد المنشأة", "Approve place"),
                    text: T("سيتم تفعيل المنشأة وإتاحتها وفق إعداداتها الحالية.", "The place will be activated and made available using its current settings."),
                    confirmText: T("اعتماد", "Approve"),
                    confirmColor: COLORS.success
                });
            } else if (button.classList.contains("delete-btn") || button.classList.contains("delete-rejected-btn")) {
                performPlaceAction("/Admin/Places/DeletePlace", id, {
                    title: T("حذف المنشأة نهائيًا", "Delete place permanently"),
                    text: T("لن تتمكن من استرجاع المنشأة بعد الحذف.", "The place cannot be restored after deletion."),
                    confirmText: T("حذف نهائي", "Delete permanently"),
                    confirmColor: COLORS.coral
                });
            } else if (button.classList.contains("activate-btn")) {
                performPlaceAction("/Admin/Places/ActivatePlace", id, {
                    title: T("إعادة تنشيط المنشأة", "Reactivate place"),
                    text: T("ستعود المنشأة إلى الحالة النشطة.", "The place will return to active status."),
                    confirmText: T("إعادة التنشيط", "Reactivate"),
                    confirmColor: COLORS.success
                });
            } else if (button.classList.contains("reject-btn")) {
                performActionWithReason("/Admin/Places/RejectPlace", id, {
                    title: T("رفض طلب المنشأة", "Reject place request"),
                    label: T("سبب الرفض الذي سيظهر للمالك", "Reason that will be shown to the owner"),
                    placeholder: T("مثال: الصور غير واضحة أو البيانات ناقصة...", "Example: photos are unclear or information is incomplete..."),
                    confirmColor: COLORS.coral
                });
            } else if (button.classList.contains("suspend-btn")) {
                performActionWithReason("/Admin/Places/SuspendPlace", id, {
                    title: T("تعليق المنشأة", "Suspend place"),
                    label: "سبب التعليق الذي سيظهر للمالك",
                    placeholder: "اكتب سبب التعليق بوضوح...",
                    confirmColor: COLORS.warning
                });
            } else if (button.classList.contains("reassign-btn")) {
                openReassignOwner(id);
            }
        });

        initializePlaceSearch();
        initializeDetailsInteractions();
    }

    const createAccountState = {
        usernameAvailable: null,
        emailAvailable: null,
        checking: false,
        debounceTimer: null
    };

    function setCreateFieldMessage(selector, text, state = "neutral") {
        const element = document.querySelector(selector);
        if (!element) return;

        element.textContent = text || "";
        element.classList.toggle("is-valid", state === "valid");
        element.classList.toggle("is-invalid", state === "invalid");
        element.classList.toggle("is-checking", state === "checking");
    }

    function setCreateInputStatus(selector, state = "neutral") {
        const element = document.querySelector(selector);
        if (!element) return;

        const icons = {
            valid: '<i class="bi bi-check-circle-fill"></i>',
            invalid: '<i class="bi bi-x-circle-fill"></i>',
            checking: '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>'
        };

        element.className = `admin-place-input__status is-${state}`;
        element.innerHTML = icons[state] || "";
    }

    function isLocallyValidUsername(value) {
        return /^[a-zA-Z0-9_.-]+$/.test(String(value || "").trim());
    }

    function isLocallyValidEmail(value) {
        const text = String(value || "").trim();
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(text);
    }

    function validateOwnerPasswords() {
        const password = byId("NewOwnerPassword");
        const confirm = byId("ConfirmOwnerPassword");
        const message = document.querySelector("[data-password-match-message]");
        const strength = document.querySelector("[data-password-strength]");

        if (!password || !confirm || password.disabled || confirm.disabled) {
            confirm?.setCustomValidity("");
            if (message) {
                message.textContent = "";
                message.classList.remove("is-valid", "is-invalid");
            }
            if (strength) strength.dataset.level = "0";
            return true;
        }

        const passwordValue = String(password.value || "");
        const confirmValue = String(confirm.value || "");
        const hasValue = Boolean(passwordValue || confirmValue);
        const matches = passwordValue === confirmValue;
        const lengthValid = passwordValue.length >= 6;

        confirm.setCustomValidity(hasValue && !matches ? "كلمتا المرور غير متطابقتين." : "");
        password.setCustomValidity(passwordValue && !lengthValid ? "كلمة المرور يجب ألا تقل عن 6 أحرف." : "");

        if (message) {
            message.textContent = !hasValue
                ? ""
                : !lengthValid
                    ? "كلمة المرور يجب ألا تقل عن 6 أحرف."
                    : matches
                        ? "كلمتا المرور متطابقتان."
                        : "كلمتا المرور غير متطابقتين.";
            message.classList.toggle("is-valid", hasValue && lengthValid && matches);
            message.classList.toggle("is-invalid", hasValue && (!lengthValid || !matches));
        }

        if (strength) {
            let level = 0;
            if (passwordValue.length >= 6) level++;
            if (/[A-Za-z]/.test(passwordValue) && /\d/.test(passwordValue)) level++;
            if (/[^A-Za-z0-9]/.test(passwordValue) || passwordValue.length >= 10) level++;
            strength.dataset.level = String(level);
            const label = strength.querySelector("small");
            if (label) {
                label.textContent = level === 0
                    ? "قوة كلمة المرور"
                    : level === 1
                        ? "كلمة مرور مقبولة"
                        : level === 2
                            ? "كلمة مرور جيدة"
                            : "كلمة مرور قوية";
            }
        }

        return !hasValue || (lengthValid && matches);
    }

    async function checkOwnerAccountAvailability(force = false) {
        const root = document.querySelector("[data-admin-place-create]");
        const usernameInput = byId("NewOwnerUsername");
        const emailInput = byId("NewEmail");

        if (!root || !usernameInput || !emailInput || usernameInput.disabled || emailInput.disabled) {
            return true;
        }

        const username = String(usernameInput.value || "").trim();
        const email = String(emailInput.value || "").trim();

        usernameInput.setCustomValidity("");
        emailInput.setCustomValidity("");

        if (username && !isLocallyValidUsername(username)) {
            createAccountState.usernameAvailable = false;
            usernameInput.setCustomValidity("اسم المستخدم يجب أن يكون باللغة الإنجليزية وبدون مسافات.");
            setCreateFieldMessage("[data-username-message]", "اسم المستخدم يجب أن يكون بالإنجليزية وبدون مسافات.", "invalid");
            setCreateInputStatus("[data-username-status]", "invalid");
        } else if (!username) {
            createAccountState.usernameAvailable = null;
            setCreateFieldMessage("[data-username-message]", "إنجليزي فقط، ويمكن استخدام النقطة والشرطة والشرطة السفلية.", "neutral");
            setCreateInputStatus("[data-username-status]", "neutral");
        }

        if (email && !isLocallyValidEmail(email)) {
            createAccountState.emailAvailable = false;
            emailInput.setCustomValidity("يرجى إدخال بريد إلكتروني صحيح.");
            setCreateFieldMessage("[data-email-message]", "صيغة البريد الإلكتروني غير صحيحة.", "invalid");
            setCreateInputStatus("[data-email-status]", "invalid");
        } else if (!email) {
            createAccountState.emailAvailable = null;
            setCreateFieldMessage("[data-email-message]", "سيستخدم الحساب هذا البريد للتحقق واستعادة كلمة المرور.", "neutral");
            setCreateInputStatus("[data-email-status]", "neutral");
        }

        if ((username && !isLocallyValidUsername(username)) || (email && !isLocallyValidEmail(email))) {
            return false;
        }

        if (!username && !email) return true;

        const url = String(root.dataset.accountCheckUrl || "").trim();
        if (!url) return true;

        createAccountState.checking = true;

        if (username) {
            setCreateFieldMessage("[data-username-message]", "جاري التحقق من اسم المستخدم...", "checking");
            setCreateInputStatus("[data-username-status]", "checking");
        }
        if (email) {
            setCreateFieldMessage("[data-email-message]", "جاري التحقق من البريد...", "checking");
            setCreateInputStatus("[data-email-status]", "checking");
        }

        try {
            const parameters = new URLSearchParams();
            if (username) parameters.set("username", username);
            if (email) parameters.set("email", email);

            const response = await fetchJson(`${url}?${parameters.toString()}`);

            if (username) {
                createAccountState.usernameAvailable = response.usernameAvailable !== false;
                usernameInput.setCustomValidity(createAccountState.usernameAvailable ? "" : "اسم المستخدم مستخدم بالفعل.");
                setCreateFieldMessage(
                    "[data-username-message]",
                    createAccountState.usernameAvailable ? "اسم المستخدم متاح." : "اسم المستخدم مستخدم بالفعل.",
                    createAccountState.usernameAvailable ? "valid" : "invalid"
                );
                setCreateInputStatus("[data-username-status]", createAccountState.usernameAvailable ? "valid" : "invalid");
            }

            if (email) {
                createAccountState.emailAvailable = response.emailAvailable !== false;
                emailInput.setCustomValidity(createAccountState.emailAvailable ? "" : "البريد الإلكتروني مستخدم بالفعل.");
                setCreateFieldMessage(
                    "[data-email-message]",
                    createAccountState.emailAvailable ? "البريد الإلكتروني متاح." : "البريد الإلكتروني مستخدم بالفعل.",
                    createAccountState.emailAvailable ? "valid" : "invalid"
                );
                setCreateInputStatus("[data-email-status]", createAccountState.emailAvailable ? "valid" : "invalid");
            }

            return (createAccountState.usernameAvailable !== false && createAccountState.emailAvailable !== false);
        } catch (_) {
            // لا نمنع المستخدم بسبب تعذر الفحص اللحظي؛ السيرفر يعيد التحقق عند الحفظ.
            if (username) {
                setCreateFieldMessage("[data-username-message]", "سيتم التحقق النهائي عند المتابعة.", "neutral");
                setCreateInputStatus("[data-username-status]", "neutral");
            }
            if (email) {
                setCreateFieldMessage("[data-email-message]", "سيتم التحقق النهائي عند المتابعة.", "neutral");
                setCreateInputStatus("[data-email-status]", "neutral");
            }
            return true;
        } finally {
            createAccountState.checking = false;
        }
    }

    function scheduleAccountAvailabilityCheck() {
        window.clearTimeout(createAccountState.debounceTimer);
        createAccountState.debounceTimer = window.setTimeout(() => {
            checkOwnerAccountAvailability(false);
        }, 420);
    }

    function getSelectedCategoryName() {
        const select = byId("categorySelect");
        const option = select?.selectedOptions?.[0];
        return String(option?.dataset?.categoryName || option?.textContent || "").trim();
    }

    function getSelectedOwnerMode() {
        return document.querySelector('[name="OwnerMode"]:checked')?.value || "new";
    }

    function getSelectedExistingOwnerCard() {
        const selectedId = String(byId("ExistingOwnerId")?.value || "");
        return all("[data-owner-option]").find(card => String(card.dataset.ownerId || "") === selectedId) || null;
    }

    function updateCreateSummary() {
        const categoryTarget = document.querySelector("[data-summary-category]");
        const ownerTarget = document.querySelector("[data-summary-owner]");
        const categoryName = getSelectedCategoryName();
        const mode = getSelectedOwnerMode();

        if (categoryTarget) {
            categoryTarget.textContent = categoryName && categoryName !== "اختر الفئة" ? categoryName : "لم تُحدد بعد";
        }

        if (ownerTarget) {
            if (mode === "existing") {
                const card = getSelectedExistingOwnerCard();
                ownerTarget.textContent = card?.dataset.ownerName || "اختر مالكًا موجودًا";
            } else if (mode === "selfadmin") {
                ownerTarget.textContent = "حسابي الإداري الحالي";
            } else if (mode === "admin") {
                const root = document.querySelector("[data-admin-place-create]");
                ownerTarget.textContent = String(root?.dataset.platformOwnerReady || "false") === "true"
                    ? "حساب تشغيل منشآت الإدارة"
                    : "حساب الإدارة يحتاج إعدادًا";
            } else {
                const firstName = String(byId("NewFirstName")?.value || "").trim();
                const lastName = String(byId("NewLastName")?.value || "").trim();
                ownerTarget.textContent = `${firstName} ${lastName}`.trim() || "مالك جديد";
            }
        }

        updateCreateProgress();
    }

    function updateCreateProgress() {
        const categorySelected = Boolean(byId("categorySelect")?.value);
        const mode = getSelectedOwnerMode();
        const platformReady = String(document.querySelector("[data-admin-place-create]")?.dataset.platformOwnerReady || "false") === "true";

        let ownerReady = false;
        if (categorySelected) {
            if (mode === "existing") {
                ownerReady = Boolean(byId("ExistingOwnerId")?.value);
            } else if (mode === "selfadmin") {
                ownerReady = true;
            } else if (mode === "admin") {
                ownerReady = platformReady;
            } else {
                ownerReady = ["NewFirstName", "NewLastName", "NewOwnerUsername", "NewEmail", "NewOwnerPassword", "ConfirmOwnerPassword"]
                    .every(id => Boolean(String(byId(id)?.value || "").trim()));
            }
        }

        const step1 = document.querySelector('[data-create-step="1"]');
        const step2 = document.querySelector('[data-create-step="2"]');
        const step3 = document.querySelector('[data-create-step="3"]');

        [step1, step2, step3].forEach(step => step?.classList.remove("is-active", "is-complete"));

        if (!categorySelected) {
            step1?.classList.add("is-active");
            return;
        }

        step1?.classList.add("is-complete");

        if (!ownerReady) {
            step2?.classList.add("is-active");
            return;
        }

        step2?.classList.add("is-complete");
        step3?.classList.add("is-active");
    }

    function updateOwnerMode(options = {}) {
        const root = document.querySelector("[data-admin-place-create]");
        const selectedMode = getSelectedOwnerMode();
        const platformOwnerReady = String(root?.dataset.platformOwnerReady || "false").toLowerCase() === "true";

        const isNew = selectedMode === "new";
        const isExisting = selectedMode === "existing";
        const isSelfAdmin = selectedMode === "selfadmin";
        const isLegacyAdmin = selectedMode === "admin";
        const isAdmin = isSelfAdmin || isLegacyAdmin;

        const newPanel = byId("newOwnerInputs");
        const existingPanel = byId("existingOwnerInputs");
        const adminPanel = byId("adminOwnerInputs");
        const existingSelect = byId("ExistingOwnerId");
        const platformToggle = byId("SetAsPlatformOwner");

        all("[data-owner-mode-card]").forEach(card => {
            const radio = card.querySelector('input[type="radio"]');
            card.classList.toggle("is-active", Boolean(radio?.checked));
        });

        if (newPanel) newPanel.hidden = !isNew;
        if (existingPanel) existingPanel.hidden = !isExisting;
        if (adminPanel) adminPanel.hidden = !isAdmin;

        [
            "NewFirstName",
            "NewLastName",
            "NewEmail",
            "NewOwnerUsername",
            "NewOwnerPassword",
            "ConfirmOwnerPassword"
        ].forEach(id => {
            const field = byId(id);
            if (!field) return;
            field.disabled = !isNew;
            field.required = isNew;
            if (!isNew) field.setCustomValidity("");
        });

        if (platformToggle) {
            platformToggle.disabled = !isNew;
            if (!isNew) platformToggle.checked = false;
        }

        if (existingSelect) {
            existingSelect.disabled = !isExisting;
            existingSelect.required = isExisting;
        }

        if (isLegacyAdmin && !platformOwnerReady && !options.silent) {
            adminPanel?.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }

        validateOwnerPasswords();
        updateCreateSummary();
    }

    function updateCategorySelection() {
        const select = byId("categorySelect");
        const ownerSection = byId("ownerSection");
        const error = document.querySelector("[data-category-error]");
        if (!select) return;

        const selectedId = String(select.value || "");

        all("[data-category-card]").forEach(card => {
            const active = String(card.dataset.categoryId) === selectedId;
            card.classList.toggle("is-active", active);
            card.setAttribute("aria-pressed", active ? "true" : "false");
        });

        if (error) error.hidden = Boolean(selectedId);

        if (ownerSection) {
            ownerSection.hidden = !selectedId;
            if (selectedId) ownerSection.classList.add("is-revealed");
        }

        if (selectedId) updateOwnerMode({ silent: true });
        updateCreateSummary();
    }

    function selectExistingOwner(ownerId, options = {}) {
        const select = byId("ExistingOwnerId");
        if (!select) return;

        const id = String(ownerId || "");
        select.value = id;

        all("[data-owner-option]").forEach(card => {
            const active = String(card.dataset.ownerId || "") === id;
            card.classList.toggle("is-selected", active);
            card.setAttribute("aria-pressed", active ? "true" : "false");
        });

        const selectedCard = getSelectedExistingOwnerCard();
        const summary = document.querySelector("[data-selected-owner-summary]");
        const name = document.querySelector("[data-selected-owner-name]");
        const meta = document.querySelector("[data-selected-owner-meta]");

        if (summary) summary.hidden = !selectedCard;
        if (name) name.textContent = selectedCard?.dataset.ownerName || "—";
        if (meta) {
            const username = selectedCard?.dataset.ownerUsername || "";
            const email = selectedCard?.dataset.ownerEmail || "";
            meta.textContent = [username ? `@${username}` : "", email].filter(Boolean).join(" · ") || "حساب مالك نشط";
        }

        if (selectedCard && options.scroll !== false) {
            selectedCard.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }

        updateCreateSummary();
    }

    function filterOwnerOptions(query) {
        const normalized = normalizeSearch(query);
        const cards = all("[data-owner-option]");
        let visibleCount = 0;

        cards.forEach(card => {
            const haystack = normalizeSearch([
                card.dataset.ownerName,
                card.dataset.ownerUsername,
                card.dataset.ownerEmail,
                card.dataset.ownerPhone
            ].filter(Boolean).join(" "));

            const visible = !normalized || haystack.includes(normalized);
            card.hidden = !visible;
            if (visible) visibleCount++;
        });

        const empty = document.querySelector("[data-owner-search-empty]");
        if (empty) empty.hidden = visibleCount > 0;

        const clear = document.querySelector("[data-clear-owner-search]");
        if (clear) clear.hidden = !String(query || "").trim();
    }

    function beginCreateSubmit(form) {
        form.dataset.submitting = "true";
        const submitButton = form.querySelector('[type="submit"]');
        if (submitButton) {
            submitButton.disabled = true;
            submitButton.innerHTML = `
                <span>
                    <small>لحظات فقط</small>
                    <strong>جاري تجهيز النموذج...</strong>
                </span>
                <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
            `;
        }
    }

    function initializeCreatePage() {
        const root = document.querySelector("[data-admin-place-create]");
        const form = document.querySelector("[data-admin-place-create-form]");
        if (!root || !form) return;

        const categorySelect = byId("categorySelect");

        all("[data-category-card]", root).forEach(card => {
            card.addEventListener("click", () => {
                if (!categorySelect) return;
                categorySelect.value = card.dataset.categoryId || "";
                categorySelect.dispatchEvent(new Event("change", { bubbles: true }));
            });
        });

        categorySelect?.addEventListener("change", updateCategorySelection);

        all('[name="OwnerMode"]', root).forEach(radio => {
            radio.addEventListener("change", () => updateOwnerMode());
        });

        all("[data-owner-option]", root).forEach(card => {
            card.setAttribute("aria-pressed", "false");
            card.addEventListener("click", () => selectExistingOwner(card.dataset.ownerId));
        });

        byId("ExistingOwnerId")?.addEventListener("change", event => {
            selectExistingOwner(event.target.value, { scroll: false });
        });

        const ownerSearch = byId("ownerSearchInput");
        ownerSearch?.addEventListener("input", event => filterOwnerOptions(event.target.value));

        document.querySelector("[data-clear-owner-search]")?.addEventListener("click", () => {
            if (!ownerSearch) return;
            ownerSearch.value = "";
            filterOwnerOptions("");
            ownerSearch.focus();
        });

        document.querySelector("[data-clear-owner-selection]")?.addEventListener("click", () => {
            selectExistingOwner("", { scroll: false });
            ownerSearch?.focus();
        });

        document.querySelector("[data-setup-platform-owner]")?.addEventListener("click", () => {
            const newOwnerRadio = byId("newOwner");
            const platformToggle = byId("SetAsPlatformOwner");
            if (newOwnerRadio) {
                newOwnerRadio.checked = true;
                updateOwnerMode({ silent: true });
            }
            if (platformToggle) platformToggle.checked = true;
            byId("newOwnerInputs")?.scrollIntoView({ behavior: "smooth", block: "start" });
            window.setTimeout(() => byId("NewFirstName")?.focus(), 350);
        });

        byId("NewOwnerUsername")?.addEventListener("input", () => {
            createAccountState.usernameAvailable = null;
            scheduleAccountAvailabilityCheck();
            updateCreateSummary();
        });

        byId("NewEmail")?.addEventListener("input", () => {
            createAccountState.emailAvailable = null;
            scheduleAccountAvailabilityCheck();
        });

        ["NewFirstName", "NewLastName"].forEach(id => {
            byId(id)?.addEventListener("input", updateCreateSummary);
        });

        byId("NewOwnerPassword")?.addEventListener("input", () => {
            validateOwnerPasswords();
            updateCreateProgress();
        });
        byId("ConfirmOwnerPassword")?.addEventListener("input", () => {
            validateOwnerPasswords();
            updateCreateProgress();
        });

        all("[data-password-toggle]", root).forEach(button => {
            button.addEventListener("click", () => {
                const input = byId(button.dataset.passwordToggle);
                if (!input) return;
                const show = input.type === "password";
                input.type = show ? "text" : "password";
                button.innerHTML = `<i class="bi ${show ? "bi-eye-slash" : "bi-eye"}"></i>`;
            });
        });

        form.addEventListener("submit", async event => {
            event.preventDefault();
            if (form.dataset.submitting === "true") return;

            if (!categorySelect?.value) {
                const error = document.querySelector("[data-category-error]");
                if (error) error.hidden = false;
                root.querySelector(".admin-place-create-card--category")?.scrollIntoView({ behavior: "smooth", block: "start" });
                return;
            }

            updateOwnerMode({ silent: true });
            const selectedMode = getSelectedOwnerMode();
            const platformOwnerReady = String(root.dataset.platformOwnerReady || "false") === "true";

            if (selectedMode === "admin" && !platformOwnerReady) {
                if (window.Swal) {
                    await Swal.fire({
                        icon: "info",
                        title: "جهّز حساب التشغيل أولًا",
                        text: "حساب منشآت الإدارة يُنشأ مرة واحدة فقط كحساب Owner، ثم يصبح الربط به مباشرًا.",
                        confirmButtonText: "إنشاء الحساب الآن",
                        confirmButtonColor: COLORS.primary,
                        customClass: { popup: "admin-places-swal" }
                    });
                }
                document.querySelector("[data-setup-platform-owner]")?.click();
                return;
            }

            if (selectedMode === "existing" && !byId("ExistingOwnerId")?.value) {
                if (window.Swal) {
                    await Swal.fire({
                        icon: "warning",
                        title: "اختر مالكًا",
                        text: "حدد حساب مالك نشط من البطاقات قبل المتابعة.",
                        confirmButtonText: T("موافق", "OK"),
                        confirmButtonColor: COLORS.primary,
                        customClass: { popup: "admin-places-swal" }
                    });
                }
                byId("existingOwnerInputs")?.scrollIntoView({ behavior: "smooth", block: "start" });
                return;
            }

            if (selectedMode === "new") {
                if (!validateOwnerPasswords()) {
                    byId("ConfirmOwnerPassword")?.focus();
                    form.reportValidity();
                    return;
                }

                const accountAvailable = await checkOwnerAccountAvailability(true);
                if (!accountAvailable) {
                    form.reportValidity();
                    return;
                }
            }

            if (!form.checkValidity()) {
                form.reportValidity();
                return;
            }

            beginCreateSubmit(form);
            form.submit();
        });

        updateCategorySelection();
        updateOwnerMode({ silent: true });
        filterOwnerOptions("");
        updateCreateSummary();
    }

    window.toggleOwnerSection = updateCategorySelection;
    window.toggleOwnerInputs = updateOwnerMode;

    window.isStrictlyValidPhone = window.isStrictlyValidPhone || function (itiInstance, inputElement) {
        if (!itiInstance || !inputElement) return false;
        const raw = String(inputElement.value || "").replace(/\D/g, "");
        const country = itiInstance.getSelectedCountryData?.();
        if (country?.iso2 === "ye") return /^(77|78|70|73|71)[0-9]{7}$/.test(raw);
        return typeof itiInstance.isValidNumber === "function" ? itiInstance.isValidNumber() : raw.length >= 8;
    };

    let adminMediaOverlay = null;

    function ensureAdminMediaOverlay() {
        if (adminMediaOverlay && document.body.contains(adminMediaOverlay)) return adminMediaOverlay;

        const host = byId("placeDetailsModal") || document.body;
        adminMediaOverlay = document.createElement("div");
        adminMediaOverlay.className = "admin-detail-media-overlay";
        adminMediaOverlay.hidden = true;
        adminMediaOverlay.setAttribute("role", "dialog");
        adminMediaOverlay.setAttribute("aria-modal", "true");
        adminMediaOverlay.setAttribute("aria-label", T("معرض الصور", "Photo gallery"));
        adminMediaOverlay.innerHTML = `
            <section class="admin-detail-media-overlay__panel" role="document">
                <header class="admin-detail-media-overlay__header">
                    <h3 data-admin-media-title>${T("معرض الصور", "Photo gallery")}</h3>
                    <button type="button" class="admin-detail-media-overlay__close" data-admin-media-close aria-label="${T("إغلاق معرض الصور", "Close photo gallery")}">
                        <i class="bi bi-x-lg"></i>
                    </button>
                </header>
                <div class="admin-detail-media-overlay__content" data-admin-media-content></div>
            </section>`;

        host.appendChild(adminMediaOverlay);

        adminMediaOverlay.addEventListener("click", event => {
            if (event.target === adminMediaOverlay || event.target.closest("[data-admin-media-close]")) {
                closeAdminMediaOverlay();
                return;
            }

            const imageButton = event.target.closest("[data-admin-media-preview]");
            if (imageButton) {
                openAdminImageOverlay(imageButton.dataset.adminMediaPreview, imageButton.dataset.adminMediaAlt || T("صورة المنشأة", "Place photo"));
            }
        });

        document.addEventListener("keydown", event => {
            if (event.key === "Escape" && adminMediaOverlay && !adminMediaOverlay.hidden) {
                event.preventDefault();
                event.stopPropagation();
                closeAdminMediaOverlay();
            }
        }, true);

        return adminMediaOverlay;
    }

    function closeAdminMediaOverlay() {
        if (!adminMediaOverlay || adminMediaOverlay.hidden) return;
        adminMediaOverlay.classList.remove("is-open");
        window.setTimeout(() => {
            if (!adminMediaOverlay) return;
            adminMediaOverlay.hidden = true;
            const content = adminMediaOverlay.querySelector("[data-admin-media-content]");
            if (content) content.innerHTML = "";
        }, 180);
    }

    function openAdminMediaGallery(images, title) {
        const cleanImages = safeArray(images).map(item => String(item || "").trim()).filter(Boolean);
        if (!cleanImages.length) {
            showError(T("لا توجد صور متاحة للعرض.", "No photos are available."));
            return;
        }

        const overlay = ensureAdminMediaOverlay();
        const titleElement = overlay.querySelector("[data-admin-media-title]");
        const content = overlay.querySelector("[data-admin-media-content]");

        if (titleElement) titleElement.textContent = displayValue(title, T("معرض الصور", "Photo gallery"));
        if (content) {
            content.innerHTML = `<div class="admin-detail-media-grid">${cleanImages.map((image, index) => `
                <button type="button"
                        class="admin-detail-media-grid__item"
                        data-admin-media-preview="${escapeAttribute(image)}"
                        data-admin-media-alt="${T("صورة", "Photo")} ${index + 1}">
                    <img src="${escapeAttribute(image)}"
                         onerror="this.src='/images/placeholder.jpg'"
                         alt="${T("صورة", "Photo")} ${index + 1}">
                </button>`).join("")}</div>`;
        }

        overlay.hidden = false;
        requestAnimationFrame(() => overlay.classList.add("is-open"));
        overlay.querySelector("[data-admin-media-close]")?.focus();
    }

    function openAdminImageOverlay(url, title) {
        if (!url) return;
        const overlay = ensureAdminMediaOverlay();
        const titleElement = overlay.querySelector("[data-admin-media-title]");
        const content = overlay.querySelector("[data-admin-media-content]");

        if (titleElement) titleElement.textContent = displayValue(title, T("معاينة الصورة", "Photo preview"));
        if (content) {
            content.innerHTML = `<div class="admin-detail-media-single">
                <img src="${escapeAttribute(url)}"
                     onerror="this.src='/images/placeholder.jpg'"
                     alt="${escapeAttribute(displayValue(title, T("صورة المنشأة", "Place photo")))}">
            </div>`;
        }

        overlay.hidden = false;
        requestAnimationFrame(() => overlay.classList.add("is-open"));
        overlay.querySelector("[data-admin-media-close]")?.focus();
    }

    window.openAdminImage = function (url) {
        openAdminImageOverlay(url, T("معاينة الصورة", "Photo preview"));
    };

    window.showMoreAdminItems = function (hiddenClassSelector, buttonId) {
        const hiddenItems = all(hiddenClassSelector);
        hiddenItems.slice(0, 3).forEach(item => {
            item.classList.remove("d-none", hiddenClassSelector.replace(".", ""), "is-hidden");
        });
        if (!document.querySelector(hiddenClassSelector)) byId(buttonId)?.remove();
    };

    window.openServiceGallery = function (encodedImages, serviceName) {
        let images = [];
        try {
            images = JSON.parse(decodeURIComponent(encodedImages || ""));
        } catch (_) {
            images = [];
        }

        if (!images.length) {
            showError(T("لا توجد صور لهذه الخدمة.", "No photos are available for this service."));
            return;
        }

        openAdminMediaGallery(images, isArabic ? `صور ${displayValue(serviceName, "الخدمة")}` : `${displayValue(serviceName, "service")} photos`);
    };

    window.openRoomGallery = function (title, imageUrlsString) {
        const images = String(imageUrlsString || "")
            .split(",")
            .map(item => item.trim())
            .filter(Boolean);

        if (!images.length) {
            showError(T("لا توجد صور لهذه الوحدة.", "No photos are available for this unit."));
            return;
        }

        openAdminMediaGallery(images, displayValue(title, T("صور الوحدة", "Unit photos")));
    };

    async function initializeNotificationDeepLink() {
        const root =
            document.querySelector("[data-admin-places-page]");

        if (!root) {
            return;
        }

        const url = new URL(window.location.href);
        const placeId =
            Number.parseInt(
                url.searchParams.get("focusPlace") || "0",
                10
            );

        if (!Number.isInteger(placeId) || placeId <= 0) {
            return;
        }

        const viewButton =
            root.querySelector(
                `button.view-btn[data-id="${placeId}"]`
            );

        if (viewButton) {
            const pane =
                viewButton.closest(".tab-pane-content");

            const tabId =
                pane?.id || "";

            const tabButton =
                tabId
                    ? root.querySelector(
                        `.tab-btn[data-target="${tabId}"]`
                    )
                    : null;

            if (tabButton) {
                if (typeof window.switchTab === "function") {
                    window.switchTab(tabId, tabButton);
                } else {
                    tabButton.click();
                }
            }

            const focusHost =
                viewButton.closest("tr, article, .admin-place-card") ||
                viewButton;

            focusHost.scrollIntoView({
                behavior: "smooth",
                block: "center",
                inline: "nearest"
            });

            const previousOutline = focusHost.style.outline;
            const previousOffset = focusHost.style.outlineOffset;

            focusHost.style.outline =
                "2px solid rgba(0, 166, 218, .58)";
            focusHost.style.outlineOffset = "4px";

            window.setTimeout(() => {
                focusHost.style.outline = previousOutline;
                focusHost.style.outlineOffset = previousOffset;
            }, 4200);
        }

        try {
            await fetchAndShowDetails(placeId, viewButton);
        } finally {
            url.searchParams.delete("focusPlace");

            window.history.replaceState(
                window.history.state,
                "",
                url.pathname + url.search + url.hash
            );
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        initializeManagementActions();
        initializeCreatePage();
        void initializeNotificationDeepLink();
    });
})();
/* ========================================================================== 
   محرر المنشأة الموحد مدمج هنا لتجنب إنشاء أي ملف أو مسار جديد
   ========================================================================== */
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
        // أغلق قائمة إجراءات الصف أولاً حتى لا يبقى Backdrop فوق SweetAlert.
        window.closeAdminPlaceActionMenu?.();
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
                    container: 'owner-place-edit-workspace-layer',
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
                    document.body.classList.add('owner-place-modal-context', 'owner-place-edit-modal');
                    document.body.classList.remove('admin-place-action-sheet-open');
                    document.querySelector('[data-place-action-backdrop]')?.classList.remove('is-open');
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
                willClose: () => {
                    document.body.classList.remove('owner-place-edit-modal');
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

                                // 2. 🌟 الحل هنا: استخدام إشعار سويت أليرت المدمج والآمن (يعمل دائماً)
                                Swal.mixin({ toast: true, position: 'top-end', showConfirmButton: false, timer: 2500 })
                                    .fire({ icon: 'success', title: 'تم تحديث بيانات المنشأة بنجاح!' });

                                if (editorConfig.reloadAfterSave) {
                                    window.setTimeout(() => window.location.reload(), 650);
                                    return;
                                }

                                // التحديث الصامت للكروت
                                const targetClass = localStorage.getItem('activePlaceTab') || 'place-list-tab';
                                const targetBtn = document.querySelector(`.custom-tabs-container .tab-btn[data-target="${targetClass}"]`);
                                const endpointUrl = targetBtn?.getAttribute('data-url');
                                const targetContent = document.querySelector('.' + targetClass);

                                if (endpointUrl && targetContent) {
                                    fetch(endpointUrl)
                                        .then(response => response.text())
                                        .then(htmlMarkup => {
                                            // 🌟 التحديث المخملي (تخفيت الشاشة ثم إعادة رسمها بنعومة)
                                            targetContent.style.transition = 'opacity 0.3s ease';
                                            targetContent.style.opacity = '0'; // إخفاء ناعم

                                            setTimeout(() => {
                                                targetContent.innerHTML = htmlMarkup;
                                                targetContent.style.opacity = '1'; // إظهار ناعم
                                            }, 300);
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

    // واجهة عربية لاختيار الصور بدل النص الافتراضي الإنجليزي للمتصفح.
    window.updateAdminPlaceImagePicker = function (input) {
        if (!input) return;
        const popup = input.closest('.swal2-popup') || document;
        const text = popup.querySelector('[data-admin-image-picker-text]');
        if (!text) return;

        const files = Array.from(input.files || []);
        if (!files.length) {
            text.textContent = 'لم يتم اختيار صور بعد';
            return;
        }

        const totalBytes = files.reduce((sum, file) => sum + (file.size || 0), 0);
        const totalMb = (totalBytes / (1024 * 1024)).toFixed(totalBytes >= 1024 * 1024 ? 2 : 1);
        text.textContent = `${files.length} ${files.length === 1 ? 'صورة محددة' : 'صور محددة'} • ${totalMb} ميجا`;
    };

    // 📸 إدارة الصور الذكية للمنشأة (مع توسيط الصور وتأكيد الحفظ)
    window.openManagePlaceImagesModal = function (placeId) {
        // أغلق قائمة إجراءات الصف أولاً حتى تبقى نافذة الصور قابلة للنقر دائماً.
        window.closeAdminPlaceActionMenu?.();
        Swal.fire({
            title: 'جاري تحميل الصور...',
            allowOutsideClick: false,
            customClass: { container: 'admin-places-swal-layer', popup: 'admin-places-swal' },
            didOpen: () => Swal.showLoading()
        });

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
                    <button class="btn btn-danger btn-sm position-absolute top-0 end-0 m-2 rounded-circle shadow" style="width: 25px; height: 25px; padding: 0;" onclick="deletePlaceImageDirect(${placeId}, 0, true)">
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
                <button class="btn btn-danger btn-sm position-absolute top-0 end-0 m-1 rounded-circle shadow" style="width: 20px; height: 20px; padding: 0;" onclick="deletePlaceImageDirect(${placeId}, ${img.id}, false)">
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
            <div class="admin-place-image-picker">
                <input type="file" id="new-place-images" class="admin-place-image-picker__input" multiple accept="image/*" onchange="window.updateAdminPlaceImagePicker(this)">
                <label for="new-place-images" class="admin-place-image-picker__label">
                    <span class="admin-place-image-picker__icon"><i class="bi bi-images"></i></span>
                    <span class="admin-place-image-picker__copy">
                        <strong>اختر صور المنشأة</strong>
                        <small data-admin-image-picker-text>لم يتم اختيار صور بعد</small>
                    </span>
                    <span class="admin-place-image-picker__button">استعراض</span>
                </label>
            </div>
            <small class="text-muted mt-2 d-block fw-bold text-danger">الحد الأقصى للصور الإضافية معاً هو 10 ميجا (الغلاف له مساحة مستقلة).</small>
        </div>
    </div>`;

                Swal.fire({
                    title: `<h5 class="fw-bold text-dark m-0">معرض صور المنشأة</h5>`,
                    html: html,
                    showCloseButton: true,
                    allowOutsideClick: false,
                    showCancelButton: true,
                    buttonsStyling: false,
                    customClass: {
                        container: 'owner-place-images-workspace-layer',
                        popup: 'owner-place-images-workspace',
                        title: 'owner-place-images-workspace__title',
                        htmlContainer: 'owner-place-images-workspace__body',
                        actions: 'owner-place-images-workspace__actions',
                        confirmButton: 'owner-place-images-workspace__save',
                        cancelButton: 'owner-place-images-workspace__cancel'
                    },
                    confirmButtonText: '<i class="bi bi-cloud-arrow-up"></i><span>حفظ ورفع الصور</span>',
                    cancelButtonText: 'إغلاق',
                    didOpen: () => {
                        document.body.classList.add('owner-place-modal-context', 'owner-place-images-modal');
                        document.body.classList.remove('admin-place-action-sheet-open');
                        document.querySelector('[data-place-action-backdrop]')?.classList.remove('is-open');
                    },
                    willClose: () => {
                        document.body.classList.remove('owner-place-images-modal');
                    },
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
                    // 🌟 الحل لمشكلة عدم وضوح الحفظ: إظهار رسالة صريحة قبل إعادة تحميل النافذة
                    if (res.isConfirmed && res.value?.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'تم الحفظ والرفع بنجاح!',
                            text: res.value.message,
                            confirmButtonColor: '#00A6DA',
                            confirmButtonText: 'حسناً'
                        }).then(() => {
                            // بعد أن يضغط المستخدم "حسناً"، نقوم بتحديث الصور أمامه
                            if (res.value.newCoverPath) {
                                const cardImg = document.getElementById(`card-img-${placeId}`);
                                if (cardImg) cardImg.src = res.value.newCoverPath;
                            }
                            openManagePlaceImagesModal(placeId);
                        });
                    }
                });
            });
    };


    // 🌟 دالة حذف صور المنشأة (تحديث الأرقام بنعومة واحترافية)
    window.deletePlaceImageDirect = function (placeId, imageId, isCover) {
        window.elegantConfirm('هل أنتِ متأكدة من مسح هذه الصورة لتفريغ مساحة؟', function () {

            let imgBox;
            if (isCover === true || isCover === 'true') {
                imgBox = document.getElementById(`cover-container-${placeId}`);
            } else {
                imgBox = document.getElementById(`gallery-img-${imageId}`);
            }
            if (imgBox) imgBox.style.opacity = '0.5';

            fetch(`/Owner/Place/DeletePlaceImage?placeId=${placeId}&imageId=${imageId}&isCover=${isCover}`, { method: 'POST' })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {

                        // إخفاء الصورة من الشاشة بصمت
                        if (isCover === true || isCover === 'true') {
                            if (imgBox) {
                                imgBox.innerHTML = `
                                <div class="border border-dashed p-3 text-center rounded animate-fade-in">
                                    <p class="small text-muted mb-2">لا توجد صورة غلاف</p>
                                    <label class="btn btn-sm btn-outline-primary fw-bold">
                                        <i class="bi bi-camera-fill me-1"></i> إضافة غلاف
                                        <input type="file" class="d-none" accept="image/*" onchange="uploadSingleCover(${placeId}, this)">
                                    </label>
                                </div>`;
                                imgBox.style.opacity = '1';
                            }
                            const cardImg = document.getElementById(`card-img-${placeId}`);
                            if (cardImg) cardImg.src = '/images/default-place.jpg';
                        } else {
                            if (imgBox) imgBox.remove();
                        }

                        // 🌟 السحر هنا: حساب وتحديث الأرقام فوراً بناءً على الرد من السيرفر!
                        const validationMessage = Swal.getValidationMessage();
                        if (validationMessage && validationMessage.style.display !== 'none') {

                            // 1. استلام المساحة الحالية للصور القديمة من السيرفر (بعد الحذف)
                            let currentExistingMb = data.currentSizeMb || 0;

                            // 2. حساب مساحة الملفات الجديدة التي اختارها المستخدم (بدون تضييعها)
                            let newFilesSize = 0;
                            const newImageInput = document.getElementById('new-place-images');
                            if (newImageInput && newImageInput.files) {
                                for (let i = 0; i < newImageInput.files.length; i++) {
                                    newFilesSize += newImageInput.files[i].size;
                                }
                            }
                            let newMb = (newFilesSize / (1024 * 1024)).toFixed(2);

                            // 3. مقارنة الإجمالي مع الحد الأقصى (10 ميجا)
                            let maxMb = 10.0;
                            let availableMb = (maxMb - currentExistingMb).toFixed(2);
                            if (availableMb < 0) availableMb = 0;

                            let grandTotalMb = parseFloat(currentExistingMb) + parseFloat(newMb);

                            if (grandTotalMb > maxMb) {
                                // إذا كانت المساحة لا تزال غير كافية -> نحدث الأرقام في الرسالة الحمراء بنعومة
                                Swal.showValidationMessage(`عذراً! مساحة الصور الإضافية الحالية (${currentExistingMb} ميجا). المتبقي لك (${availableMb} ميجا) فقط، بينما الصور الجديدة حجمها (${newMb} ميجا). يرجى حذف صور إضافية قديمة لتفريغ مساحة.`);
                                const popup = Swal.getPopup();
                                if (popup) popup.scrollTo({ top: popup.scrollHeight, behavior: 'smooth' });
                            } else {
                                // إذا أصبحت المساحة كافية! -> نخفي الرسالة الحمراء ونسمح بالحفظ
                                Swal.resetValidationMessage();
                                window.showSafeToast('المساحة أصبحت كافية! يمكنك الآن الضغط على (حفظ ورفع الصور).', 'success');
                            }

                        } else {
                            window.showSafeToast('تم مسح الصورة بنجاح.', 'success');
                        }

                    } else {
                        if (imgBox) imgBox.style.opacity = '1';
                        window.showSafeToast(data.message || 'حدث خطأ أثناء الحذف', 'error');
                    }
                }).catch(err => {
                    if (imgBox) imgBox.style.opacity = '1';
                    window.showSafeToast('حدث خطأ في الاتصال بالخادم.', 'error');
                });
        });
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
        const editorMode =
            editorHost.dataset.placeEditorMode ||
            'owner';

        configure({
            mode: editorMode
        });

        document.body.classList.toggle(
            'admin-place-editor-context',
            editorMode === 'admin'
        );
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

/* ========================================================================== 
   ADMIN PLACES — ACTION MENU V2
   واجهة فقط: لا تغيّر أي endpoint أو action قائم.
   ========================================================================== */
(() => {
    "use strict";

    const MOBILE_QUERY = window.matchMedia("(max-width: 767.98px)");
    let activeHost = null;
    const menuByHost = new WeakMap();
    const menuHome = new WeakMap();

    function getBackdrop() {
        let backdrop = document.querySelector("[data-place-action-backdrop]");
        if (backdrop) return backdrop;

        backdrop = document.createElement("div");
        backdrop.className = "admin-place-action-backdrop";
        backdrop.setAttribute("data-place-action-backdrop", "");
        backdrop.setAttribute("aria-hidden", "true");
        document.body.appendChild(backdrop);
        return backdrop;
    }

    function getParts(host) {
        if (!host) return {};
        const toggle = host.querySelector("[data-place-action-toggle]");
        let menu = menuByHost.get(host) || host.querySelector("[data-place-action-menu]");
        if (menu) menuByHost.set(host, menu);
        return { toggle, menu };
    }

    function portalMenu(host, menu) {
        if (!host || !menu || menu.parentNode === document.body) return;
        if (!menuHome.has(menu)) {
            menuHome.set(menu, {
                parent: menu.parentNode,
                nextSibling: menu.nextSibling
            });
        }
        menuByHost.set(host, menu);
        document.body.appendChild(menu);
    }

    function restoreMenu(menu) {
        if (!menu) return;
        const home = menuHome.get(menu);
        if (!home?.parent) return;

        if (home.nextSibling && home.nextSibling.parentNode === home.parent) {
            home.parent.insertBefore(menu, home.nextSibling);
        } else {
            home.parent.appendChild(menu);
        }
        menuHome.delete(menu);
    }

    function positionDesktopMenu(host) {
        if (!host || MOBILE_QUERY.matches) return;
        const { toggle, menu } = getParts(host);
        if (!toggle || !menu || menu.hidden) return;

        menu.style.top = "0px";
        menu.style.left = "0px";
        menu.style.right = "auto";
        menu.style.bottom = "auto";

        const toggleRect = toggle.getBoundingClientRect();
        const menuRect = menu.getBoundingClientRect();
        const gap = 8;
        const edge = 12;

        let top = toggleRect.bottom + gap;
        if (top + menuRect.height > window.innerHeight - edge) {
            top = Math.max(edge, toggleRect.top - menuRect.height - gap);
        }

        let left = toggleRect.right - menuRect.width;
        left = Math.max(edge, Math.min(left, window.innerWidth - menuRect.width - edge));

        menu.style.top = `${Math.round(top)}px`;
        menu.style.left = `${Math.round(left)}px`;
    }

    function closeMenu(host = activeHost) {
        if (!host) return;
        const { toggle, menu } = getParts(host);

        if (toggle) toggle.setAttribute("aria-expanded", "false");
        if (menu) {
            menu.classList.remove("is-open");
            menu.hidden = true;
            menu.style.removeProperty("top");
            menu.style.removeProperty("left");
            menu.style.removeProperty("right");
            menu.style.removeProperty("bottom");
            restoreMenu(menu);
        }

        const backdrop = document.querySelector("[data-place-action-backdrop]");
        backdrop?.classList.remove("is-open");
        backdrop?.setAttribute("aria-hidden", "true");
        document.body.classList.remove("admin-place-action-sheet-open");

        if (activeHost === host) activeHost = null;
    }

    // API صغير تستخدمه المودلات قبل الفتح حتى لا يبقى أي Overlay من قائمة الإجراءات.
    window.closeAdminPlaceActionMenu = () => closeMenu();

    function openMenu(host) {
        if (!host) return;
        if (activeHost && activeHost !== host) closeMenu(activeHost);

        const { toggle, menu } = getParts(host);
        if (!toggle || !menu) return;

        activeHost = host;
        toggle.setAttribute("aria-expanded", "true");
        portalMenu(host, menu);
        menu.hidden = false;
        menu.classList.add("is-open");

        if (MOBILE_QUERY.matches) {
            const backdrop = getBackdrop();
            backdrop.classList.add("is-open");
            backdrop.setAttribute("aria-hidden", "false");
            document.body.classList.add("admin-place-action-sheet-open");
            window.setTimeout(() => {
                menu.querySelector("[data-place-action-close]")?.focus({ preventScroll: true });
            }, 40);
        } else {
            requestAnimationFrame(() => positionDesktopMenu(host));
        }
    }

    function toggleMenu(host) {
        const expanded = host?.querySelector("[data-place-action-toggle]")?.getAttribute("aria-expanded") === "true";
        if (expanded) closeMenu(host);
        else openMenu(host);
    }

    document.addEventListener("click", event => {
        const toggle = event.target.closest("[data-place-action-toggle]");
        if (toggle) {
            event.preventDefault();
            event.stopPropagation();
            toggleMenu(toggle.closest(".admin-place-action-menu-host"));
            return;
        }

        if (event.target.closest("[data-place-action-close]")) {
            event.preventDefault();
            closeMenu();
            return;
        }

        if (event.target.closest("[data-place-action-backdrop]")) {
            closeMenu();
            return;
        }

        const action = event.target.closest("[data-place-action-menu] .action-btn");
        if (action) {
            // أغلق الـsheet/backdrop فورًا قبل فتح SweetAlert أو تنفيذ الإجراء.
            // مع delegation على document يبقى الحدث صالحًا حتى بعد إعادة القائمة
            // إلى مكانها الأصلي داخل الصف.
            closeMenu();
            return;
        }

        if (activeHost && !event.target.closest(".admin-place-action-menu-host") && !event.target.closest("[data-place-action-menu]")) {
            closeMenu();
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && activeHost) {
            event.preventDefault();
            const toggle = activeHost.querySelector("[data-place-action-toggle]");
            closeMenu(activeHost);
            toggle?.focus({ preventScroll: true });
        }
    });

    window.addEventListener("resize", () => {
        if (!activeHost) return;
        if (MOBILE_QUERY.matches) {
            const { menu } = getParts(activeHost);
            if (menu) {
                menu.style.removeProperty("top");
                menu.style.removeProperty("left");
                menu.style.removeProperty("right");
                menu.style.removeProperty("bottom");
            }
            const backdrop = getBackdrop();
            backdrop.classList.add("is-open");
            document.body.classList.add("admin-place-action-sheet-open");
        } else {
            document.querySelector("[data-place-action-backdrop]")?.classList.remove("is-open");
            document.body.classList.remove("admin-place-action-sheet-open");
            requestAnimationFrame(() => positionDesktopMenu(activeHost));
        }
    }, { passive: true });

    window.addEventListener("scroll", () => {
        if (activeHost && !MOBILE_QUERY.matches) closeMenu(activeHost);
    }, { passive: true, capture: true });
})();
