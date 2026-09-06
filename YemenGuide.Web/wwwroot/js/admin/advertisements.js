(function () {
    "use strict";

    const app = document.getElementById("adminAdvertisementsApp");
    if (!app) return;

    const token = app.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    const urls = {
        overview: app.dataset.overviewUrl || "",
        ads: app.dataset.adsUrl || "",
        plans: app.dataset.plansUrl || "",
        reports: app.dataset.reportsUrl || "",
        details: app.dataset.detailsUrl || "",
        planForm: app.dataset.planFormUrl || "",
        savePlan: app.dataset.savePlanUrl || "",
        planStatus: app.dataset.planStatusUrl || "",
        deletePlan: app.dataset.deletePlanUrl || "",
        reviewPayment: app.dataset.reviewPaymentUrl || "",
        reviewAd: app.dataset.reviewAdUrl || "",
        controlAd: app.dataset.controlAdUrl || "",
        refundAd: app.dataset.refundAdUrl || "",
        print: app.dataset.printUrl || "",
        pdf: app.dataset.pdfUrl || ""
    };

    let activeTab = normalizeTab(new URL(window.location.href).searchParams.get("tab")) ||
        normalizeTab(app.dataset.defaultTab) ||
        "overview";
    let searchTimer = 0;
    let currentReportQuery = "";
    let performanceChart = null;

    init();

    function init() {
        activateTab(activeTab, false);
        document.addEventListener("click", onClick);
        document.addEventListener("submit", onSubmit);
        document.addEventListener("input", onInput);
        document.addEventListener("change", onChange);
        window.addEventListener("popstate", () => {
            const tab = normalizeTab(new URL(window.location.href).searchParams.get("tab")) || "overview";
            activateTab(tab, false);
        });
        buildPaginationFromInitial();
        syncReportAdOptions();
    }

    function normalizeTab(value) {
        value = String(value || "").toLowerCase();
        return ["overview", "ads", "plans", "reports"].includes(value) ? value : "";
    }

    function activateTab(tab, push = true) {
        tab = normalizeTab(tab) || "overview";
        const target = app.querySelector(`[data-admx-section="${cssEscape(tab)}"]`);
        if (!target) return;

        activeTab = tab;
        app.querySelectorAll("[data-admx-section]").forEach(section => {
            const active = section === target;
            section.hidden = !active;
            section.classList.toggle("is-active", active);
        });

        app.querySelectorAll("[data-admx-tab]").forEach(button => {
            const active = button.dataset.admxTab === tab;
            button.classList.toggle("is-active", active);
            button.setAttribute("aria-selected", active ? "true" : "false");
        });

        // أزرار الطباعة والحفظ تظهر في الشريط العلوي فقط عند فتح تبويب التقارير.
        const reportToolbar = app.querySelector("[data-report-toolbar]");
        if (reportToolbar) {
            reportToolbar.hidden = tab !== "reports";
        }

        if (push) {
            const url = new URL(window.location.href);
            url.searchParams.set("tab", tab);
            history.pushState({ tab }, "", url);
        }

        if (!prefersReducedMotion()) {
            target.animate(
                [{ opacity: .35, transform: "translateY(8px)" }, { opacity: 1, transform: "translateY(0)" }],
                { duration: 260, easing: "cubic-bezier(.2,.78,.22,1)" }
            );
        }

        if (tab === "reports") {
            window.setTimeout(() => loadReports(), 30);
        }
    }

    function onClick(event) {
        const tab = event.target.closest("[data-admx-tab]");
        if (tab && app.contains(tab)) {
            event.preventDefault();
            activateTab(tab.dataset.admxTab, true);
            return;
        }

        const groupTab = event.target.closest("[data-ad-group]");
        if (groupTab) {
            event.preventDefault();
            selectAdGroup(groupTab.dataset.adGroup || "All");
            return;
        }

        const details = event.target.closest("[data-admx-details]");
        if (details) {
            event.preventDefault();
            openAdvertisementDetails(toInt(details.dataset.admxDetails));
            return;
        }

        const page = event.target.closest("[data-ads-page]");
        if (page) {
            event.preventDefault();
            loadAds(toInt(page.dataset.adsPage));
            return;
        }

        const filterToggle = event.target.closest("[data-admx-filter-toggle]");
        if (filterToggle) {
            event.preventDefault();
            toggleAdsMobileFilter(filterToggle);
            return;
        }

        const resetAds = event.target.closest("[data-admx-reset-ads]");
        if (resetAds) {
            event.preventDefault();
            resetAdsFilter();
            return;
        }

        const planShowMore = event.target.closest("[data-plan-show-more]");
        if (planShowMore) {
            event.preventDefault();
            togglePlanCards(planShowMore);
            return;
        }

        const planOpen = event.target.closest(".admx-plan-open");
        if (planOpen) {
            event.preventDefault();
            openPlanModal(toInt(planOpen.dataset.planId));
            return;
        }

        const planDelete = event.target.closest("[data-plan-delete]");
        if (planDelete) {
            event.preventDefault();
            deletePlan(
                toInt(planDelete.dataset.planDelete),
                String(planDelete.dataset.planName || "الباقة"),
                toInt(planDelete.dataset.planAdCount)
            );
            return;
        }

        const planToggle = event.target.closest("[data-plan-toggle]");
        if (planToggle) {
            event.preventDefault();
            togglePlan(
                toInt(planToggle.dataset.planToggle),
                String(planToggle.dataset.nextActive).toLowerCase() === "true"
            );
            return;
        }

        if (event.target.closest("[data-admx-refresh-overview]")) {
            event.preventDefault();
            refreshOverview(true);
            return;
        }

        if (event.target.closest("[data-report-reset]")) {
            event.preventDefault();
            resetReports();
            return;
        }

        if (event.target.closest("[data-report-print]")) {
            event.preventDefault();
            openReport("print");
            return;
        }

        if (event.target.closest("[data-report-pdf]")) {
            event.preventDefault();
            openReport("pdf");
        }
    }

    function onSubmit(event) {
        if (event.target.matches("#admxAdsFilter")) {
            event.preventDefault();
            loadAds(1);
            return;
        }

        if (event.target.matches("#admxReportsFilter")) {
            event.preventDefault();
            loadReports();
        }
    }

    function onInput(event) {
        if (event.target.matches('#admxAdsFilter input[name="Search"]')) {
            window.clearTimeout(searchTimer);
            searchTimer = window.setTimeout(() => loadAds(1), 520);
        }
    }

    function onChange(event) {
        if (event.target.matches("#admxReportsFilter [data-report-place]")) {
            syncReportAdOptions();
        }
    }

    async function requestJson(url, options = {}) {
        const response = await fetch(url, {
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json",
                ...(options.headers || {})
            },
            ...options
        });

        let payload = null;
        const text = await response.text();
        if (text) {
            try { payload = JSON.parse(text); }
            catch { payload = null; }
        }

        if (!response.ok) {
            throw new Error(payload?.message || "تعذر تنفيذ الطلب.");
        }

        return payload;
    }

    function formQuery(form, names) {
        const params = new URLSearchParams();
        names.forEach(name => {
            const element = form.elements.namedItem(name);
            if (!element) return;
            const value = String(element.value || "").trim();
            if (value) params.set(name, value);
        });
        return params;
    }

    async function loadAds(page) {
        const form = document.getElementById("admxAdsFilter");
        const grid = document.getElementById("admxAdsGrid");
        if (!form || !grid || !urls.ads) return;

        form.elements.Page.value = String(Math.max(1, page || 1));
        const params = formQuery(form, [
            "Search", "Status", "ExactStatus", "PaymentStatus", "BillingModel", "SortBy", "Page", "PageSize"
        ]);
        params.set("PlacementType", "All");

        setBusy(grid, true, "جاري تحديث الإعلانات...");
        try {
            const data = await requestJson(`${urls.ads}?${params.toString()}`);
            renderAds(data);
        } catch (error) {
            notify("error", "تعذر تحميل الإعلانات", error.message);
        } finally {
            setBusy(grid, false);
        }
    }

    function renderAds(data) {
        const grid = document.getElementById("admxAdsGrid");
        const count = app.querySelector("[data-ads-count]");
        if (!grid || !data) return;

        if (count) count.textContent = number(data.totalCount);

        updateSubtabCounts(data);

        const items = Array.isArray(data.items) ? data.items : [];
        if (!items.length) {
            grid.innerHTML = `
                <div class="admx-empty-state admx-empty-state--wide">
                    <i class="bi bi-search-heart"></i>
                    <strong>لا توجد إعلانات مطابقة</strong>
                    <small>جرّب تبويبًا آخر أو غيّر البحث والفلاتر.</small>
                </div>`;
        } else {
            grid.innerHTML = items.map(adRowMarkup).join("");
        }

        renderPagination(data.filter?.page || 1, data.totalPages || 1);
    }

    function adRowMarkup(item) {
        const image = esc(item.imagePath || "/images/default-category.jpg");
        const attention = item.needsAttention
            ? `<span class="admx-attention-dot" title="يحتاج إجراء"></span>`
            : "";
        const isRefundedCancellation = item.status === "Cancelled" && item.paymentStatus === "Refunded";
        const statusLabel = isRefundedCancellation ? "ملغي" : item.statusLabel;
        const paymentStatusLabel = isRefundedCancellation ? "تم الاسترداد" : item.paymentStatusLabel;

        return `
            <article class="admx-ad-row">
                <button type="button" class="admx-ad-row__media" data-admx-details="${toInt(item.advertisementId)}">
                    <img src="${image}" alt="" onerror="this.src='/images/default-category.jpg'">
                    ${attention}
                </button>

                <div class="admx-ad-row__main">
                    <div class="admx-ad-row__title">
                        <h3>${esc(item.title)}</h3>
                        <span class="admx-status-pill" data-status="${esc(item.status)}">${esc(statusLabel)}</span>
                        <span class="admx-payment-pill">${esc(paymentStatusLabel)}</span>
                    </div>
                    <p>${esc(item.placeName)} <span>•</span> ${esc(item.ownerName || "")}</p>
                </div>

                <div class="admx-ad-row__metrics">
                    <span><small>الباقة</small><strong>${esc(item.planName || "—")}</strong></span>
                    <span><small>وزن الظهور</small><strong>${number(item.displayWeight)}</strong></span>
                    <span><small>المشاهدات</small><strong>${number(item.impressions)}</strong></span>
                    <span><small>النقرات</small><strong>${number(item.clicks)}</strong></span>
                </div>

                <button type="button" class="admx-row-open" data-admx-details="${toInt(item.advertisementId)}" title="عرض التفاصيل">
                    <i class="bi bi-chevron-left"></i>
                </button>
            </article>`;
    }

    function selectAdGroup(group) {
        const form = document.getElementById("admxAdsFilter");
        if (!form) return;

        const normalized = ["All", "Running", "NeedsAction", "Paused", "Closed"].includes(group)
            ? group
            : "All";

        setField(form, "Status", normalized);
        setField(form, "ExactStatus", "All");
        setField(form, "Page", "1");

        app.querySelectorAll("[data-ad-group]").forEach(button => {
            button.classList.toggle("is-active", button.dataset.adGroup === normalized);
        });

        loadAds(1);
    }

    function updateSubtabCounts(data) {
        const counts = {
            All: data.allCount ?? data.totalCount ?? 0,
            Running: data.runningCount ?? 0,
            NeedsAction: data.needsActionCount ?? 0,
            Paused: data.pausedCount ?? 0,
            Closed: data.closedCount ?? 0
        };

        app.querySelectorAll("[data-ad-group]").forEach(button => {
            const badge = button.querySelector("b");
            if (badge) badge.textContent = number(counts[button.dataset.adGroup] || 0);
        });
    }

    function buildPaginationFromInitial() {
        const form = document.getElementById("admxAdsFilter");
        if (!form) return;
        renderPagination(toInt(form.elements.Page?.value) || 1, 1);
    }

    function renderPagination(current, total) {
        const root = document.getElementById("admxAdsPagination");
        if (!root) return;
        current = Math.max(1, current);
        total = Math.max(1, total);
        if (total <= 1) {
            root.innerHTML = "";
            return;
        }

        const pages = [];
        const start = Math.max(1, current - 2);
        const end = Math.min(total, current + 2);
        if (current > 1) pages.push(`<button data-ads-page="${current - 1}"><i class="bi bi-chevron-right"></i></button>`);
        if (start > 1) pages.push(`<button data-ads-page="1">1</button><span>…</span>`);
        for (let i = start; i <= end; i++) {
            pages.push(`<button data-ads-page="${i}" class="${i === current ? "is-active" : ""}">${i}</button>`);
        }
        if (end < total) pages.push(`<span>…</span><button data-ads-page="${total}">${total}</button>`);
        if (current < total) pages.push(`<button data-ads-page="${current + 1}"><i class="bi bi-chevron-left"></i></button>`);
        root.innerHTML = pages.join("");
    }

    function toggleAdsMobileFilter(button) {
        const panel = document.getElementById("admxAdsAdvancedFilter");
        if (!panel) return;

        const open = panel.classList.toggle("is-open");
        button.classList.toggle("is-active", open);
        button.setAttribute("aria-expanded", open ? "true" : "false");
    }

    function resetAdsFilter() {
        const form = document.getElementById("admxAdsFilter");
        if (!form) return;
        form.reset();
        setField(form, "Status", "All");
        setField(form, "PaymentStatus", "All");
        setField(form, "BillingModel", "All");
        setField(form, "SortBy", "Newest");
        setField(form, "Status", "All");
        setField(form, "ExactStatus", "All");
        setField(form, "Page", "1");
        app.querySelectorAll("[data-ad-group]").forEach(button => {
            button.classList.toggle("is-active", button.dataset.adGroup === "All");
        });
        loadAds(1);
    }

    async function openAdvertisementDetails(id) {
        if (!id || !urls.details) return;
        showLoading("جاري فتح تفاصيل الإعلان...");
        try {
            const item = await requestJson(`${urls.details}?id=${encodeURIComponent(id)}`);
            showAdvertisementModal(item);
        } catch (error) {
            closeLoading();
            notify("error", "تعذر فتح الإعلان", error.message);
        }
    }

    function showAdvertisementModal(item) {
        if (!item) return;

        const media = esc(item.imagePath || "/images/default-category.jpg");
        const canReview = Boolean(item.canReview);
        const canControl = Boolean(item.canControl);
        const device = deviceLabel(item.deviceTarget);
        const appearanceTime = item.startImmediately
            ? "بعد موافقة الإدارة"
            : (item.requestedStartAt
                ? `من ${dateTime(item.requestedStartAt)}`
                : "تاريخ مستقبلي محدد");
        const isRefundedCancellation = item.status === "Cancelled" && item.paymentStatus === "Refunded";
        const statusLabel = isRefundedCancellation ? "ملغي" : item.statusLabel;
        const paymentStatusLabel = isRefundedCancellation ? "تم الاسترداد" : item.paymentStatusLabel;
        const actions = [];

        if (canReview && item.paymentStatus === "PendingReview") {
            actions.push(`<button type="button" class="admx-swal-action is-success" data-ad-action="payment-approve"><i class="bi bi-check2-circle"></i> اعتماد الدفع</button>`);
            actions.push(`<button type="button" class="admx-swal-action is-danger" data-ad-action="payment-reject"><i class="bi bi-x-circle"></i> رفض الدفع</button>`);
        }

        if (canReview && item.status === "Pending" && item.paymentStatus === "Paid") {
            actions.push(`<button type="button" class="admx-swal-action is-success" data-ad-action="content-approve"><i class="bi bi-patch-check"></i> اعتماد الإعلان</button>`);
            actions.push(`<button type="button" class="admx-swal-action is-danger" data-ad-action="content-reject"><i class="bi bi-pencil-square"></i> طلب تعديل</button>`);
        }

        if (item.canRefund) {
            actions.push(`<button type="button" class="admx-swal-action is-success" data-ad-action="refund"><i class="bi bi-cash-coin"></i> تنفيذ الاسترداد</button>`);
        }

        if (canControl && item.status === "Active") {
            actions.push(`<button type="button" class="admx-swal-action is-warning" data-ad-action="pause"><i class="bi bi-pause-circle"></i> إيقاف مؤقت</button>`);
        }

        if (canControl && item.status === "Paused") {
            actions.push(`<button type="button" class="admx-swal-action is-success" data-ad-action="resume"><i class="bi bi-play-circle"></i> استئناف</button>`);
        }

        if (canControl && !["Cancelled", "Expired"].includes(item.status)) {
            actions.push(`<button type="button" class="admx-swal-action is-danger ghost" data-ad-action="cancel"><i class="bi bi-x-octagon"></i> إلغاء الإعلان</button>`);
        }

        const promotion = item.promotionName
            ? `<article><small>العرض المرتبط</small><strong>${esc(item.promotionName)}</strong></article>`
            : "";

        const paymentMeta = item.referenceNumber
            ? `<article><small>رقم الحوالة</small><strong>${esc(item.referenceNumber)}</strong></article>`
            : "";

        const html = `
            <div class="admx-luxury-review" data-current-ad="${toInt(item.advertisementId)}">
                <section class="admx-luxury-review__visual">
                    <img src="${media}" alt="" onerror="this.src='/images/default-category.jpg'">
                    <div class="admx-luxury-review__visual-content">
                        <span class="admx-luxury-review__sponsored"><i class="bi bi-megaphone-fill"></i> إعلان ممول</span>
                        <h3>${esc(item.title)}</h3>
                        <p>${esc(item.shortDescription || item.placeName || "")}</p>
                    </div>
                </section>

                <section class="admx-luxury-review__content">
                    <div class="admx-luxury-review__statuses">
                        <span class="status"><i class="bi bi-activity"></i>${esc(statusLabel)}</span>
                        <span class="payment"><i class="bi bi-credit-card"></i>${esc(paymentStatusLabel)}</span>
                        <span class="billing"><i class="bi bi-box-seam"></i>${esc(item.planName || "باقة إعلانية")}</span>
                    </div>

                    <div class="admx-luxury-review__section-title">
                        <i class="bi bi-info-circle"></i>
                        <span>بيانات الإعلان</span>
                    </div>

                    <div class="admx-luxury-review__info">
                        <article><small>المنشأة</small><strong>${esc(item.placeName || "—")}</strong></article>
                        <article><small>المالك</small><strong>${esc(item.ownerName || "—")}</strong></article>
                        <article><small>الباقة</small><strong>${esc(item.planName || "—")}</strong></article>
                        <article><small>وزن الظهور</small><strong>${number(item.displayWeight)}</strong></article>
                        <article><small>المدة</small><strong>${number(item.durationDays)} يوم</strong></article>
                        <article><small>الجهاز</small><strong>${esc(device)}</strong></article>
                        ${promotion}
                    </div>

                    <div class="admx-luxury-review__section-title">
                        <i class="bi bi-calendar3"></i>
                        <span>المدة والسعر</span>
                    </div>

                    <div class="admx-luxury-review__info">
                        <article><small>سعر اليوم</small><strong>${money(item.dailyPrice)}</strong></article>
                        <article><small>الإجمالي</small><strong>${money(item.totalAmount ?? item.price)}</strong></article>
                        <article><small>وقت الظهور</small><strong>${esc(appearanceTime)}</strong></article>
                        <article><small>بدء العرض الفعلي</small><strong>${dateTime(item.startAt)}</strong></article>
                        <article><small>نهاية العرض</small><strong>${dateTime(item.endAt)}</strong></article>
                    </div>

                    <div class="admx-luxury-review__section-title">
                        <i class="bi bi-graph-up-arrow"></i>
                        <span>الأداء</span>
                    </div>

                    <div class="admx-luxury-review__performance">
                        <article><i class="bi bi-eye"></i><small>المشاهدات</small><strong>${number(item.impressions)}</strong></article>
                        <article><i class="bi bi-cursor"></i><small>النقرات</small><strong>${number(item.clicks)}</strong></article>
                    </div>

                    ${item.receiptImage ? `
                    <a class="admx-luxury-review__receipt" href="${esc(item.receiptImage)}" target="_blank" rel="noopener">
                        <i class="bi bi-receipt"></i>
                        <div><strong>إثبات الدفع</strong><small>${paymentMeta ? esc(item.referenceNumber || "") : "فتح الإثبات"}</small></div>
                        <i class="bi bi-box-arrow-up-left"></i>
                    </a>` : ""}

                    ${item.rejectionReason ? `
                    <div class="admx-luxury-review__note">
                        <i class="bi bi-exclamation-circle"></i>
                        <div><strong>ملاحظة المراجعة</strong><p>${esc(item.rejectionReason)}</p></div>
                    </div>` : ""}

                    ${item.cancellationReason ? `
                    <div class="admx-luxury-review__note">
                        <i class="bi bi-arrow-counterclockwise"></i>
                        <div><strong>${item.canRefund ? "طلب استرداد" : "سبب الإلغاء"}</strong><p>${esc(item.cancellationReason)}</p></div>
                    </div>` : ""}

                    ${item.canRefund ? `
                    <div class="admx-luxury-review__note">
                        <i class="bi bi-cash-coin"></i>
                        <div><strong>المبلغ المطلوب استرداده</strong><p>${money(item.transferredAmount ?? item.totalAmount ?? item.price)} — اضغط «تنفيذ الاسترداد» لتحديد جهة التحويل ورفع الإثبات.</p></div>
                    </div>` : ""}

                    ${(item.refundedAt || item.refundProofImage || item.paymentStatus === "Refunded") ? `
                    <div class="admx-refund-proof">
                        <div class="admx-refund-proof__head">
                            <i class="bi bi-patch-check-fill"></i>
                            <div><strong>تم تنفيذ الاسترداد</strong><small>${dateTime(item.refundedAt)}</small></div>
                        </div>
                        <div class="admx-refund-proof__grid">
                            <article><small>المبلغ المسترد</small><strong>${money(item.refundAmount ?? item.transferredAmount ?? 0)}</strong></article>
                            <article><small>الوسيلة</small><strong>${esc(item.refundWalletName || "—")}</strong></article>
                            <article><small>الحساب المستفيد</small><strong>${esc(item.refundAccountNumber || "—")}</strong></article>
                            <article><small>اسم المستفيد</small><strong>${esc(item.refundBeneficiaryName || "—")}</strong></article>
                            <article><small>رقم المرجع</small><strong>${esc(item.refundReferenceNumber || "—")}</strong></article>
                            <article><small>نفذه</small><strong>${esc(item.refundedByName || "الإدارة")}</strong></article>
                        </div>
                        ${item.refundNote ? `<p class="admx-refund-proof__note">${esc(item.refundNote)}</p>` : ""}
                        ${item.refundProofImage ? `<a class="admx-refund-proof__link" href="${esc(item.refundProofImage)}" target="_blank" rel="noopener"><i class="bi bi-image"></i> فتح إثبات الاسترداد</a>` : ""}
                    </div>` : ""}

                    ${actions.length ? `<footer class="admx-luxury-review__actions">${actions.join("")}</footer>` : ""}
                </section>
            </div>`;

        fire({
            title: "مراجعة الإعلان",
            html,
            width: 1040,
            showConfirmButton: false,
            showCloseButton: true,
            customClass: swalClasses("admx-swal-review"),
            didOpen: popup => {
                popup.querySelectorAll("[data-ad-action]").forEach(button => {
                    button.addEventListener("click", () =>
                        handleAdAction(item, button.dataset.adAction));
                });
            }
        });
    }

    async function openRefundForm(item) {
        if (!urls.refundAd) {
            await notify("error", "تعذر تنفيذ الاسترداد", "مسار تنفيذ الاسترداد غير متاح.");
            showAdvertisementModal(item);
            return;
        }

        const expectedAmount = item.transferredAmount ?? item.totalAmount ?? item.price ?? 0;
        const destinations = Array.isArray(item.refundDestinations) ? item.refundDestinations : [];
        const wallets = Array.isArray(item.refundWallets) ? item.refundWallets : [];
        const ownerSelectedRefundDestination = Boolean(
            toInt(item.refundWalletId) &&
            String(item.refundAccountNumber || "").trim() &&
            String(item.refundBeneficiaryName || "").trim()
        );

        const destinationOptions = destinations.length
            ? destinations.map(destination => `
                <option value="${toInt(destination.ownerWalletId)}"
                        data-wallet-id="${toInt(destination.walletId)}"
                        data-account="${esc(destination.accountNumber || "")}"
                        data-beneficiary="${esc(destination.beneficiaryName || "")}">
                    ${esc(destination.walletName || "محفظة")} — ${esc(destination.accountNumber || "")}
                </option>`).join("")
            : "";

        const walletOptions = wallets.map(wallet => `
            <option value="${toInt(wallet.walletId)}">${esc(wallet.walletName || "")}</option>`).join("");

        const html = `
            <div class="admx-refund-form" dir="rtl">
                <div class="admx-refund-form__amount">
                    <small>المبلغ المطلوب استرداده</small>
                    <strong>${money(expectedAmount)}</strong>
                    <span>المبلغ يحسب تلقائيًا من المبلغ الذي تم اعتماده للدفع.</span>
                </div>

                ${ownerSelectedRefundDestination ? `
                <div class="admx-refund-form__amount">
                    <small>وجهة الاسترداد التي اختارها المالك</small>
                    <strong>${esc(item.refundWalletName || "محفظة الاسترداد")}</strong>
                    <span>${esc(item.refundAccountNumber || "")} — ${esc(item.refundBeneficiaryName || "")}</span>
                </div>` : (destinations.length ? `
                <label class="is-wide">
                    <span>محفظة المالك المحفوظة</span>
                    <select id="admxRefundDestination">
                        <option value="">اختر محفظة المالك</option>
                        ${destinationOptions}
                    </select>
                </label>` : "")}

                <div class="admx-swal-form-grid">
                    ${ownerSelectedRefundDestination ? "" : `
                    <label>
                        <span>وسيلة الاسترداد</span>
                        <select id="admxRefundWallet">
                            <option value="">اختر الوسيلة</option>
                            ${walletOptions}
                        </select>
                    </label>
                    <label>
                        <span>رقم الحساب / المحفظة</span>
                        <input id="admxRefundAccount" type="text" maxlength="100" autocomplete="off">
                    </label>
                    <label>
                        <span>اسم المستفيد</span>
                        <input id="admxRefundBeneficiary" type="text" maxlength="150" autocomplete="off">
                    </label>`}
                    <label>
                        <span>رقم مرجع الاسترداد</span>
                        <input id="admxRefundReference" type="text" maxlength="100" autocomplete="off">
                    </label>
                    <label class="is-wide">
                        <span>صورة إثبات التحويل</span>
                        <input id="admxRefundProof" type="file" accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp">
                        <small class="admx-refund-form__hint">JPG / PNG / WEBP — بحد أقصى 8 MB.</small>
                    </label>
                    <label class="is-wide">
                        <span>ملاحظة للإسترداد <em>اختياري</em></span>
                        <textarea id="admxRefundNote" maxlength="500" placeholder="أي ملاحظة تساعد المالك على التحقق من العملية..."></textarea>
                    </label>
                </div>
            </div>`;

        const result = await fire({
            icon: "info",
            title: "تنفيذ وتوثيق الاسترداد",
            html,
            width: 680,
            showCancelButton: true,
            confirmButtonText: "تنفيذ الاسترداد",
            cancelButtonText: "رجوع",
            reverseButtons: true,
            customClass: swalClasses("admx-swal-confirm admx-swal-refund"),
            didOpen: popup => {
                if (ownerSelectedRefundDestination) return;
                const destination = popup.querySelector("#admxRefundDestination");
                const wallet = popup.querySelector("#admxRefundWallet");
                const account = popup.querySelector("#admxRefundAccount");
                const beneficiary = popup.querySelector("#admxRefundBeneficiary");

                destination?.addEventListener("change", () => {
                    const option = destination.options[destination.selectedIndex];
                    if (!option?.value) return;
                    if (wallet) wallet.value = option.dataset.walletId || "";
                    if (account) account.value = option.dataset.account || "";
                    if (beneficiary) beneficiary.value = option.dataset.beneficiary || "";
                });
            },
            preConfirm: () => {
                const popup = window.Swal?.getPopup();
                if (!popup) return false;

                const destination = popup.querySelector("#admxRefundDestination");
                const walletId = ownerSelectedRefundDestination
                    ? toInt(item.refundWalletId)
                    : toInt(popup.querySelector("#admxRefundWallet")?.value);
                const account = ownerSelectedRefundDestination
                    ? String(item.refundAccountNumber || "").trim()
                    : String(popup.querySelector("#admxRefundAccount")?.value || "").trim();
                const beneficiary = ownerSelectedRefundDestination
                    ? String(item.refundBeneficiaryName || "").trim()
                    : String(popup.querySelector("#admxRefundBeneficiary")?.value || "").trim();
                const reference = String(popup.querySelector("#admxRefundReference")?.value || "").trim();
                const note = String(popup.querySelector("#admxRefundNote")?.value || "").trim();
                const proof = popup.querySelector("#admxRefundProof")?.files?.[0];

                if (!walletId) {
                    window.Swal?.showValidationMessage("اختر وسيلة الاسترداد.");
                    return false;
                }
                if (!account) {
                    window.Swal?.showValidationMessage("اكتب رقم الحساب أو المحفظة التي رُد إليها المبلغ.");
                    return false;
                }
                if (!beneficiary) {
                    window.Swal?.showValidationMessage("اكتب اسم المستفيد.");
                    return false;
                }
                if (reference.length < 3) {
                    window.Swal?.showValidationMessage("اكتب رقم مرجع عملية الاسترداد.");
                    return false;
                }
                if (!proof) {
                    window.Swal?.showValidationMessage("ارفع صورة إثبات تحويل مبلغ الاسترداد.");
                    return false;
                }
                if (proof.size > 8 * 1024 * 1024) {
                    window.Swal?.showValidationMessage("حجم الإثبات يجب ألا يتجاوز 8 ميجابايت.");
                    return false;
                }
                if (!/^image\/(jpeg|png|webp)$/i.test(proof.type || "")) {
                    window.Swal?.showValidationMessage("الإثبات يجب أن يكون صورة JPG أو PNG أو WEBP.");
                    return false;
                }

                return {
                    ownerWalletId: ownerSelectedRefundDestination ? 0 : (toInt(destination?.value) || 0),
                    walletId,
                    account,
                    beneficiary,
                    reference,
                    note,
                    proof
                };
            }
        });

        if (!result?.isConfirmed || !result.value) {
            showAdvertisementModal(item);
            return;
        }

        const value = result.value;
        const body = new FormData();
        body.append("AdvertisementId", String(item.advertisementId));
        body.append("RefundWalletId", String(value.walletId));
        if (value.ownerWalletId) body.append("OwnerWalletId", String(value.ownerWalletId));
        body.append("RefundAccountNumber", value.account);
        body.append("RefundBeneficiaryName", value.beneficiary);
        body.append("RefundReferenceNumber", value.reference);
        if (value.note) body.append("RefundNote", value.note);
        body.append("RefundProof", value.proof, value.proof.name);
        appendToken(body);

        showLoading("جاري توثيق الاسترداد...");
        try {
            const response = await requestJson(urls.refundAd, { method: "POST", body });
            await notify("success", "تم الاسترداد", response?.message || "تم توثيق الاسترداد وإشعار المالك.");
            await loadAds(toInt(document.getElementById("admxAdsFilter")?.elements.Page?.value) || 1);
            await refreshOverview(false);
            await refreshReportsAfterMutation();
        } catch (error) {
            notify("error", "تعذر تنفيذ الاسترداد", error.message);
        }
    }

    async function handleAdAction(item, action) {
        if (action === "refund") {
            await openRefundForm(item);
            return;
        }

        const map = {
            "payment-approve": { url: urls.reviewPayment, field: "Decision", value: "Approve", title: "اعتماد إثبات الدفع؟", reason: false, ok: "اعتماد الدفع" },
            "payment-reject": { url: urls.reviewPayment, field: "Decision", value: "Reject", title: "رفض إثبات الدفع؟", reason: true, ok: "رفض الدفع" },
            "content-approve": { url: urls.reviewAd, field: "Decision", value: "Approve", title: "اعتماد محتوى الإعلان؟", reason: false, ok: "اعتماد الإعلان" },
            "content-reject": { url: urls.reviewAd, field: "Decision", value: "Reject", title: "طلب تعديل الإعلان؟", reason: true, ok: "طلب تعديل" },
            "pause": { url: urls.controlAd, field: "Action", value: "Pause", title: "إيقاف الحملة مؤقتًا؟", reason: true, ok: "إيقاف" },
            "resume": { url: urls.controlAd, field: "Action", value: "Resume", title: "استئناف الحملة؟", reason: false, ok: "استئناف" },
            "cancel": { url: urls.controlAd, field: "Action", value: "Cancel", title: "إلغاء الحملة نهائيًا؟", reason: true, ok: "إلغاء الحملة" }
        };
        const cfg = map[action];
        if (!cfg?.url) return;

        const result = await fire({
            icon: cfg.reason ? "warning" : "question",
            title: cfg.title,
            text: cfg.reason ? "اكتب السبب بوضوح حتى يظهر في سجل المراجعة." : "سيتم تطبيق العملية فورًا بعد التأكيد.",
            input: cfg.reason ? "textarea" : undefined,
            inputPlaceholder: cfg.reason ? "اكتب السبب هنا..." : undefined,
            showCancelButton: true,
            confirmButtonText: cfg.ok,
            cancelButtonText: "رجوع",
            reverseButtons: true,
            customClass: swalClasses("admx-swal-confirm"),
            preConfirm: value => {
                if (cfg.reason && !String(value || "").trim()) {
                    if (window.Swal) Swal.showValidationMessage("السبب مطلوب لهذه العملية.");
                    return false;
                }
                return value;
            }
        });

        if (!result?.isConfirmed) {
            showAdvertisementModal(item);
            return;
        }

        const body = new FormData();
        body.append("AdvertisementId", String(item.advertisementId));
        body.append(cfg.field, cfg.value);
        if (cfg.reason) body.append("Reason", String(result.value || "").trim());
        appendToken(body);

        showLoading("جاري حفظ القرار...");
        try {
            const response = await requestJson(cfg.url, { method: "POST", body });
            await notify("success", "تمت العملية", response?.message || "تم حفظ التغيير بنجاح.");

            // بعد اعتماد محتوى الإعلان يصبح Active أو Scheduled في الخدمة،
            // لذلك ننقله بصريًا مباشرة من «تحتاج إجراء» إلى «تعمل».
            if (action === "content-approve") {
                selectAdGroup("Running");
            } else {
                await loadAds(toInt(document.getElementById("admxAdsFilter")?.elements.Page?.value) || 1);
            }

            await refreshOverview(false);
            await refreshReportsAfterMutation();
        } catch (error) {
            notify("error", "تعذر تنفيذ العملية", error.message);
        }
    }

    function togglePlanCards(button) {
        const section = button.closest('[data-admx-section="plans"]');
        if (!section) return;

        const extraCards = Array.from(
            section.querySelectorAll('[data-extra-plan="true"]')
        );

        if (!extraCards.length) {
            button.hidden = true;
            return;
        }

        const expanded = button.getAttribute("aria-expanded") === "true";
        const nextExpanded = !expanded;

        extraCards.forEach(card => {
            card.style.display = nextExpanded ? "" : "none";
        });

        button.setAttribute("aria-expanded", nextExpanded ? "true" : "false");

        const label = button.querySelector("[data-plan-more-label]");
        const icon = button.querySelector("[data-plan-more-icon]");
        const hiddenCount = Number(button.dataset.hiddenCount || extraCards.length);

        if (label) {
            label.textContent = nextExpanded
                ? "عرض أقل"
                : `عرض المزيد (${hiddenCount})`;
        }

        if (icon) {
            icon.classList.toggle("bi-chevron-down", !nextExpanded);
            icon.classList.toggle("bi-chevron-up", nextExpanded);
        }
    }

    async function openPlanModal(id) {
        if (!urls.planForm) return;

        showLoading("جاري تجهيز الباقة...");
        try {
            const query = id ? `?id=${encodeURIComponent(id)}` : "";
            const model = await requestJson(urls.planForm + query);
            showPlanModal(model);
        } catch (error) {
            notify("error", "تعذر فتح الباقة", error.message);
        }
    }

    function showPlanModal(model) {
        const editing = Boolean(model.advertisementPlanId);
        const title = editing ? "تعديل الباقة" : "إنشاء باقة جديدة";

        const html = `
            <form class="admx-plan-clean" id="admxSwalPlanForm" autocomplete="off">
                <input type="hidden" name="AdvertisementPlanId" value="${model.advertisementPlanId || ""}">

                <header class="admx-plan-clean__head">
                    <span><i class="bi bi-wallet2"></i></span>
                    <div>
                        <small>صفحة البحث - أعلى الفئات</small>
                        <h3>${editing ? "تعديل إعدادات الباقة" : "إضافة باقة إعلانية"}</h3>
                        <p>حدد اسم الباقة وسعر اليوم ووزن الظهور. وزن الظهور يزيد فرصة الاختيار مقارنة بالإعلانات الأخرى ولا يمثل نسبة ظهور مضمونة.</p>
                    </div>
                </header>

                <section class="admx-plan-clean__budget">
                    <label>
                        <span>اسم الباقة</span>
                        <input type="text"
                               name="PlanName"
                               maxlength="100"
                               value="${esc(model.planName || "")}"
                               placeholder="مثال: الباقة المميزة"
                               required>
                    </label>

                    <label>
                        <span>سعر اليوم</span>
                        <div class="admx-plan-money">
                            <input type="text"
                                   inputmode="decimal"
                                   dir="ltr"
                                   lang="en"
                                   autocomplete="off"
                                   data-english-number="decimal"
                                   data-replace-on-type="true"
                                   name="DailyPrice"
                                   value="${numValue(model.dailyPrice)}"
                                   placeholder="0"
                                   required>
                            <b>ر.ي</b>
                        </div>
                    </label>

                    <label>
                        <span>وزن الظهور</span>
                        <input type="text"
                               inputmode="numeric"
                               dir="ltr"
                               lang="en"
                               autocomplete="off"
                               data-english-number="integer"
                               data-replace-on-type="true"
                               name="DisplayWeight"
                               value="${toInt(model.displayWeight) || 50}"
                               required>
                        <small>من 1 إلى 100. كلما ارتفع الوزن زادت فرصة ظهور الإعلان مقارنة بالإعلانات الأخرى.</small>
                    </label>

                    <label class="is-wide">
                        <span>وصف الباقة <small>(اختياري)</small></span>
                        <textarea name="Description"
                                  maxlength="500"
                                  rows="3"
                                  placeholder="وصف قصير يظهر للمالك عند اختيار الباقة.">${esc(model.description || "")}</textarea>
                    </label>
                </section>

                <section class="admx-plan-clean__toggles">
                    <label>
                        <span>
                            <strong>الباقة مفعّلة</strong>
                            <small>عند تفعيلها تظهر للملاك عند إنشاء إعلان جديد.</small>
                        </span>
                        <input type="checkbox" name="IsActive" ${model.isActive !== false ? "checked" : ""}>
                    </label>
                </section>
            </form>`;

        fire({
            title,
            html,
            width: 760,
            showCancelButton: true,
            confirmButtonText: editing ? "حفظ التعديلات" : "إنشاء الباقة",
            cancelButtonText: "إلغاء",
            reverseButtons: true,
            focusConfirm: false,
            customClass: swalClasses("admx-swal-plan-clean"),
            didOpen: popup => {
                popup.querySelectorAll("[data-english-number]").forEach(field => {
                    const normalize = () => {
                        let value = toEnglishDigits(String(field.value || ""));

                        if (field.dataset.englishNumber === "integer") {
                            value = value.replace(/[^0-9]/g, "");
                            if (value) {
                                const n = Math.min(100, Math.max(1, Number(value)));
                                value = Number.isFinite(n) ? String(n) : "";
                            }
                        } else {
                            value = value
                                .replace(/,/g, ".")
                                .replace(/[^0-9.]/g, "");

                            const firstDot = value.indexOf(".");
                            if (firstDot >= 0) {
                                value =
                                    value.slice(0, firstDot + 1) +
                                    value.slice(firstDot + 1).replace(/\./g, "");
                            }
                        }

                        field.value = value;
                    };

                    field.addEventListener("focus", () => {
                        if (field.dataset.replaceOnType === "true" &&
                            field.dataset.didInitialSelect !== "true") {
                            field.dataset.didInitialSelect = "true";
                            window.setTimeout(() => field.select(), 0);
                        }
                    });

                    field.addEventListener("input", normalize);
                    field.addEventListener("paste", () =>
                        window.setTimeout(normalize, 0));
                });
            },
            preConfirm: async () => {
                const form = document.getElementById("admxSwalPlanForm");
                if (!form) return false;

                const name = String(form.elements.PlanName.value || "").trim();
                const dailyPrice = Number(form.elements.DailyPrice.value || 0);
                const displayWeight = Number(form.elements.DisplayWeight.value || 0);

                if (name.length < 2) {
                    Swal.showValidationMessage("اكتب اسمًا واضحًا للباقة.");
                    return false;
                }
                if (!dailyPrice || dailyPrice <= 0) {
                    Swal.showValidationMessage("سعر اليوم يجب أن يكون أكبر من صفر.");
                    return false;
                }
                if (!displayWeight || displayWeight < 1 || displayWeight > 100) {
                    Swal.showValidationMessage("وزن الظهور يجب أن يكون بين 1 و100.");
                    return false;
                }

                const body = new FormData(form);
                body.set("PlanName", name);
                body.set("IsActive", form.elements.IsActive.checked ? "true" : "false");
                appendToken(body);

                try {
                    if (window.Swal) Swal.showLoading();
                    return await requestJson(urls.savePlan, { method: "POST", body });
                } catch (error) {
                    if (window.Swal) Swal.showValidationMessage(error.message);
                    return false;
                }
            }
        }).then(async result => {
            if (!result?.isConfirmed) return;

            await notify(
                "success",
                editing ? "تم حفظ التعديلات" : "تم إنشاء الباقة",
                result.value?.message || "تم حفظ الباقة بنجاح."
            );

            window.location.reload();
        });
    }

    async function deletePlan(id, planName, linkedAdvertisements) {
        if (!id || !urls.deletePlan) return;

        if (linkedAdvertisements > 0) {
            await fire({
                icon: "info",
                title: "لا يمكن حذف هذه الباقة",
                text: "هذه الباقة مستخدمة في إعلانات. يمكنك إيقافها بدلًا من ذلك.",
                confirmButtonText: "حسنًا",
                customClass: swalClasses("admx-swal-confirm")
            });
            return;
        }

        const result = await fire({
            icon: "warning",
            title: "حذف الباقة؟",
            text: `سيتم حذف الباقة «${planName}» نهائيًا. هل تريد المتابعة؟`,
            showCancelButton: true,
            confirmButtonText: "نعم، حذف",
            cancelButtonText: "إلغاء",
            reverseButtons: true,
            focusCancel: true,
            customClass: swalClasses("admx-swal-confirm")
        });

        if (!result?.isConfirmed) return;

        const body = new FormData();
        body.append("advertisementPlanId", String(id));
        appendToken(body);

        showLoading("جاري حذف الباقة...");

        try {
            const response = await requestJson(urls.deletePlan, {
                method: "POST",
                body
            });

            await notify(
                "success",
                "تم الحذف",
                response?.message || "تم حذف الباقة بنجاح."
            );

            window.location.reload();
        } catch (error) {
            await notify("error", "تعذر حذف الباقة", error.message);
        }
    }

    async function togglePlan(id, nextActive) {
        const result = await fire({
            icon: nextActive ? "question" : "warning",
            title: nextActive ? "تفعيل الباقة؟" : "إيقاف الباقة؟",
            text: nextActive
                ? "ستصبح هذه الباقة متاحة للمالك."
                : "لن تتأثر الإعلانات السابقة، لكن لن يمكن إنشاء إعلانات جديدة بهذه الباقة.",
            showCancelButton: true,
            confirmButtonText: nextActive ? "تفعيل" : "إيقاف",
            cancelButtonText: "رجوع",
            reverseButtons: true,
            customClass: swalClasses("admx-swal-confirm")
        });
        if (!result?.isConfirmed) return;

        const body = new FormData();
        body.append("AdvertisementPlanId", String(id));
        body.append("IsActive", nextActive ? "true" : "false");
        appendToken(body);

        showLoading("جاري تحديث الباقة...");
        try {
            const response = await requestJson(urls.planStatus, { method: "POST", body });
            await notify("success", "تم التحديث", response?.message || "تم تحديث حالة الباقة.");
            window.location.reload();
        } catch (error) {
            notify("error", "تعذر التحديث", error.message);
        }
    }

    async function refreshOverview(showSuccessMessage = false) {
        if (!urls.overview) return;
        try {
            const data = await requestJson(urls.overview);
            setText('[data-overview-today-impressions]', number(data.todayImpressions));
            setText('[data-overview-today-clicks]', number(data.todayClicks));
            setText('[data-overview-active]', number(data.activeCount));
            setText('[data-overview-needs-review]', number((data.pendingPaymentReviewCount || 0) + (data.pendingReviewCount || 0) + (data.refundPendingCount || 0)));
            if (showSuccessMessage) {
                notify("success", "تم التحديث", "تم تحديث مؤشرات النظرة العامة.", true);
            }
        } catch (error) {
            if (showSuccessMessage) {
                notify("error", "تعذر التحديث", error.message);
            } else {
                console.warn("تعذر تحديث مؤشرات الإعلانات بعد العملية.", error);
            }
        }
    }

    async function refreshReportsAfterMutation() {
        const form = document.getElementById("admxReportsFilter");
        if (!form || !urls.reports) return;

        try {
            const params = reportParams(form);
            currentReportQuery = params.toString();
            const separator = currentReportQuery ? "&" : "";
            const data = await requestJson(`${urls.reports}?${currentReportQuery}${separator}_ts=${Date.now()}`);
            renderReports(data);
        } catch (error) {
            // لا نفشل عملية الاعتماد/الإلغاء/الاسترداد إذا تعذر فقط تحديث التقرير بالخلفية.
            console.warn("تعذر تحديث تقارير الإعلانات تلقائيًا بعد العملية.", error);
        }
    }

    async function loadReports() {
        const form = document.getElementById("admxReportsFilter");
        if (!form || !urls.reports) return;
        const params = reportParams(form);
        currentReportQuery = params.toString();

        const section = app.querySelector('[data-admx-section="reports"]');
        setBusy(section, true, "جاري إعداد التقرير...");
        try {
            const data = await requestJson(`${urls.reports}?${currentReportQuery}`);
            renderReports(data);
        } catch (error) {
            notify("error", "تعذر إعداد التقرير", error.message);
        } finally {
            setBusy(section, false);
        }
    }

    function reportParams(form) {
        const params = formQuery(form, [
            "From", "To", "AdvertisementPlanId", "PlaceId", "AdvertisementId"
        ]);
        params.set("BillingModel", "All");
        params.set("Status", "All");
        params.set("PaymentStatus", "All");
        params.set("PlacementType", "All");
        params.set("SortBy", "Impressions");
        params.set("SortDirection", "Desc");
        return params;
    }

    function renderReports(data) {
        if (!data) return;

        const root = document.getElementById("admxReportKpis");
        if (root) {
            root.innerHTML = `
                ${reportKpi("bi-megaphone", "إجمالي الإعلانات", data.totalAdvertisements, "ضمن الفترة المحددة")}
                ${reportKpi("bi-eye", "المشاهدات الفريدة", data.impressions, "مستخدمون أو زوار مختلفون")}
                ${reportKpi("bi-cursor", "النقرات الفريدة", data.clicks, "مستخدمون أو زوار مختلفون")}
                ${reportKpi("bi-check2-circle", "الحجوزات المكتملة", data.conversions)}
                ${reportKpi("bi-cash-stack", "إيرادات الإعلانات", data.paidRevenue, "مبالغ دفع معتمدة · ر.ي", true)}`;
        }

        renderPerformanceChart(Array.isArray(data.dailyMetrics) ? data.dailyMetrics : []);

        // ملخص حالات الدفع كان يُرسم من Razor فقط، لذلك كان يبقى قديمًا حتى Refresh كامل للصفحة.
        // الآن يتحدث من نفس استجابة التقرير مع بقية المؤشرات.
        setText('[data-report-paid-count]', number(data.paidAdvertisements ?? 0));
        setText('[data-report-pending-count]', number(data.pendingPayments ?? 0));
        setText('[data-report-refunded-count]', number(data.refundedAdvertisements ?? 0));

        const table = document.querySelector("#admxTopAdsTable tbody");
        const items = Array.isArray(data.topAdvertisements) ? data.topAdvertisements : [];

        if (table) {
            table.innerHTML = items.length
                ? items.slice(0, 30).map(item => `
                    <tr>
                        <td><strong>${esc(item.title)}</strong><small>${esc(item.placeName)}</small></td>
                        <td><strong>${esc(item.planName)}</strong><small>نسبة الظهور ${number(item.displayWeight)}%</small></td>
                        <td><strong>${esc(item.statusLabel)}</strong><small>${esc(item.paymentStatusLabel)}</small></td>
                        <td>${number(item.durationDays)} يوم</td>
                        <td>${number(item.dailyPrice)} ر.ي</td>
                        <td>${number(item.totalAmount)} ر.ي</td>
                        <td>${number(item.impressions)}</td>
                        <td>${number(item.clicks)}</td>
                        <td>${number(item.conversions)}</td>
                    </tr>`).join("")
                : `<tr><td colspan="9"><div class="admx-empty-state"><strong>لا توجد بيانات في الفترة المحددة</strong></div></td></tr>`;
        }
    }

    function renderInitialCharts() {
        const reportSection = app.querySelector('[data-admx-section="reports"]');
        if (!reportSection || typeof window.ApexCharts !== "function") return;

        const rows = Array.from(document.querySelectorAll("#admxTopAdsTable tbody tr"));
        // الرسم الحقيقي يُعاد من endpoint عند فتح التقارير أو تطبيق الفلاتر.
        // نترك مساحة نظيفة في أول تحميل، ثم نطلب التقرير إذا كان التبويب الحالي هو reports.
        if (activeTab === "reports") {
            loadReports();
        }
    }

    function renderPerformanceChart(metrics) {
        const root = document.getElementById("admxPerformanceChart");
        if (!root) return;

        if (performanceChart) {
            performanceChart.destroy();
            performanceChart = null;
        }

        if (typeof window.ApexCharts !== "function") {
            root.innerHTML = `<div class="admx-empty-state"><strong>تعذر تحميل مكتبة الرسم المحلية.</strong></div>`;
            return;
        }

        const hasActivity = metrics.some(x => Number(x.impressions || 0) > 0 || Number(x.clicks || 0) > 0);
        if (!hasActivity) {
            root.innerHTML = `<div class="admx-empty-state"><strong>لا توجد مشاهدات أو نقرات في الفترة المحددة بعد</strong><small>يظهر الرسم تلقائيًا بعد بدء عرض إعلان وتسجيل تفاعل فعلي.</small></div>`;
            return;
        }

        root.innerHTML = "";
        const categories = metrics.map(x => shortDate(x.date));
        const options = {
            chart: {
                type: "area",
                height: 300,
                toolbar: { show: false },
                zoom: { enabled: false },
                fontFamily: "Tajawal, sans-serif"
            },
            series: [
                { name: "المشاهدات الفريدة", data: metrics.map(x => Number(x.impressions || 0)) },
                { name: "النقرات الفريدة", data: metrics.map(x => Number(x.clicks || 0)) }
            ],
            colors: ["#00A6DA", "#FF5A5F"],
            stroke: { curve: "smooth", width: [3, 2] },
            fill: {
                type: "gradient",
                gradient: { shadeIntensity: 1, opacityFrom: .24, opacityTo: .02, stops: [0, 90, 100] }
            },
            dataLabels: { enabled: false },
            xaxis: {
                categories,
                labels: { style: { colors: "#738797", fontSize: "10px" } },
                axisBorder: { show: false },
                axisTicks: { show: false }
            },
            yaxis: {
                labels: { style: { colors: "#738797", fontSize: "10px" } }
            },
            grid: { borderColor: "#e8f1f4", strokeDashArray: 4 },
            legend: {
                position: "top",
                horizontalAlign: "right",
                fontSize: "11px",
                labels: { colors: "#05203C" }
            },
            tooltip: { shared: true, intersect: false }
        };

        performanceChart = new ApexCharts(root, options);
        performanceChart.render();
    }

    function resetReports() {
        const form = document.getElementById("admxReportsFilter");
        if (!form) return;
        form.reset();
        setField(form, "PlaceId", "");
        setField(form, "AdvertisementId", "");
        setField(form, "AdvertisementPlanId", "");
        setField(form, "BillingModel", "All");
        setField(form, "Status", "All");
        syncReportAdOptions();
        loadReports();
    }

    function syncReportAdOptions() {
        const form = document.getElementById("admxReportsFilter");
        if (!form) return;
        const place = form.querySelector("[data-report-place]")?.value || "";
        const ad = form.querySelector("[data-report-ad]");
        if (!ad) return;

        let selectedVisible = false;
        Array.from(ad.options).forEach((option, index) => {
            if (index === 0) return;
            const visible = !place || String(option.dataset.parent || "") === String(place);
            option.hidden = !visible;
            option.disabled = !visible;
            if (visible && option.selected) selectedVisible = true;
        });
        if (!selectedVisible && ad.value) ad.value = "";
    }

    function openReport(kind) {
        const form = document.getElementById("admxReportsFilter");
        if (!form) return;
        const query = reportParams(form).toString();

        if (kind === "pdf") {
            if (!urls.pdf) return;
            window.open(`${urls.pdf}?${query}`, "_blank", "noopener");
            return;
        }

        if (!urls.print) return;
        window.open(`${urls.print}?${query}`, "_blank", "noopener");
    }

    function reportKpi(icon, label, value, note = "", moneyClass = false) {
        return `<article class="${moneyClass ? "is-coral" : ""}">
            <span><i class="bi ${icon}"></i></span>
            <div>
                <small>${esc(label)}</small>
                <strong>${number(value)}</strong>
                ${note ? `<em>${esc(note)}</em>` : ""}
            </div>
        </article>`;
    }

    function info(label, value, icon) {
        return `<div><span><i class="bi ${icon}"></i></span><small>${esc(label)}</small><strong>${esc(value || "—")}</strong></div>`;
    }

    function metric(label, value, icon) {
        return `<div><i class="bi ${icon}"></i><small>${esc(label)}</small><strong>${number(value)}</strong></div>`;
    }

    function toEnglishDigits(value) {
        return String(value || "")
            .replace(/[٠-٩]/g, digit => String("٠١٢٣٤٥٦٧٨٩".indexOf(digit)))
            .replace(/[۰-۹]/g, digit => String("۰۱۲۳۴۵۶۷۸۹".indexOf(digit)));
    }

    function deviceLabel(value) {
        const normalized = String(value || "All").toLowerCase();
        if (normalized === "mobile") return "الجوال";
        if (normalized === "desktop") return "الكمبيوتر";
        return "جميع الأجهزة";
    }

    function billingLabel(value) {
        return value === "CPC" ? "مقابل النقرات" : value === "CPM" ? "مقابل ألف ظهور" : "مدة محددة";
    }

    function billingIcon(value) {
        return value === "CPC" ? "bi-cursor-fill" : value === "CPM" ? "bi-eye-fill" : "bi-calendar-range-fill";
    }

    function setBusy(element, busy, label) {
        if (!element) return;
        element.classList.toggle("is-busy", Boolean(busy));
        if (busy) {
            element.dataset.oldAriaBusy = element.getAttribute("aria-busy") || "";
            element.setAttribute("aria-busy", "true");
            if (!element.querySelector(":scope > .admx-inline-loader")) {
                element.insertAdjacentHTML("afterbegin", `<div class="admx-inline-loader"><span></span><strong>${esc(label || "جاري التحميل...")}</strong></div>`);
            }
        } else {
            element.setAttribute("aria-busy", element.dataset.oldAriaBusy || "false");
            element.querySelector(":scope > .admx-inline-loader")?.remove();
        }
    }

    function setField(form, name, value) {
        const field = form?.elements?.namedItem(name);
        if (field) field.value = value;
    }

    function setText(selector, value) {
        const element = app.querySelector(selector);
        if (element) element.textContent = value;
    }

    function appendToken(body) {
        if (token) body.set("__RequestVerificationToken", token);
    }

    function showLoading(title) {
        fire({
            title: title || "جاري التنفيذ...",
            allowOutsideClick: false,
            showConfirmButton: false,
            customClass: swalClasses("admx-swal-loading"),
            didOpen: () => { if (window.Swal) Swal.showLoading(); }
        });
    }

    function closeLoading() {
        if (window.Swal) Swal.close();
    }

    async function notify(icon, title, text, toast = false) {
        if (!window.Swal) {
            console.error(title, text || "");
            return { isConfirmed: false };
        }
        return Swal.fire({
            icon,
            title,
            text,
            toast,
            position: toast ? "top-end" : "center",
            showConfirmButton: !toast,
            timer: toast ? 2200 : undefined,
            confirmButtonText: "حسنًا",
            customClass: swalClasses(toast ? "admx-swal-toast" : "admx-swal-message")
        });
    }

    function fire(options) {
        if (window.Swal) return Swal.fire(options);
        console.error("SweetAlert2 local library is not loaded.", options);
        return Promise.resolve({ isConfirmed: false, value: "" });
    }

    function swalClasses(extra) {
        const isToast = extra === "admx-swal-toast";
        return {
            container: isToast ? "admx-swal-container admx-swal-container--toast" : "admx-swal-container",
            popup: `admx-swal-popup ${extra || ""}`.trim(),
            title: "admx-swal-title",
            htmlContainer: "admx-swal-html",
            confirmButton: "admx-swal-confirm-btn",
            cancelButton: "admx-swal-cancel-btn",
            closeButton: "admx-swal-close"
        };
    }

    function date(value) {
        if (!value) return "—";
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? "—" : d.toLocaleDateString("ar-YE");
    }

    function dateTime(value) {
        if (!value) return "—";
        const d = new Date(value);
        return Number.isNaN(d.getTime())
            ? "—"
            : d.toLocaleString("ar-YE", {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit"
            });
    }

    function shortDate(value) {
        if (!value) return "";
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? "" : `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}`;
    }

    function money(value) {
        const n = Number(value || 0);
        return `${number(n)} ر.ي`;
    }

    function number(value) {
        const n = Number(value || 0);
        return Number.isFinite(n) ? n.toLocaleString("ar-YE", { maximumFractionDigits: 2 }) : "0";
    }

    function numValue(value) {
        if (value === null || value === undefined) return "";
        const n = Number(value);
        return Number.isFinite(n) ? String(n) : "";
    }

    function toInt(value) {
        return Math.max(0, parseInt(String(value || "0"), 10) || 0);
    }

    function esc(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function cssEscape(value) {
        return window.CSS?.escape ? CSS.escape(String(value || "")) : String(value || "").replace(/["\\]/g, "\\$&");
    }

    function prefersReducedMotion() {
        return window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches === true;
    }
})();