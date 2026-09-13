(() => {
    "use strict";

    const qs = (selector, root = document) => root.querySelector(selector);
    const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));
    const page = qs("[data-owner-reports-page]");
    if (!page) return;

    const toastHost = qs("[data-toast-host]");
    const toast = (message, isError = false) => {
        if (!toastHost || !message) return;
        const item = document.createElement("div");
        item.className = `or-toast${isError ? " is-error" : ""}`;
        item.textContent = message;
        toastHost.appendChild(item);
        window.setTimeout(() => item.remove(), 4200);
    };

    const safeJson = async response => {
        try { return await response.json(); }
        catch { return { success: false, message: "تعذر قراءة استجابة الخادم." }; }
    };

    const formatBytes = bytes => {
        if (!Number.isFinite(bytes)) return "";
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    };

    const validateFile = (file, imageOnly = false) => {
        if (!file) return null;
        const allowed = imageOnly ? ["jpg", "jpeg", "png", "webp"] : ["jpg", "jpeg", "png", "webp", "pdf"];
        const ext = (file.name.split(".").pop() || "").toLowerCase();
        if (!allowed.includes(ext)) return imageOnly ? "المطلوب صورة JPG أو PNG أو WEBP، وليس PDF." : "صيغة الملف غير مدعومة.";
        if (file.size === 0) return "الملف فارغ. اختر ملفًا آخر.";
        if (file.size > 5 * 1024 * 1024) return "حجم الملف يجب ألا يتجاوز 5MB.";
        return null;
    };

    const setUploadPreview = input => {
        const zone = input.closest("[data-upload-zone]");
        const preview = zone?.querySelector("[data-upload-preview]");
        if (!preview) return;
        const file = input.files?.[0];
        if (!file) { preview.hidden = true; preview.innerHTML = ""; return; }
        const error = validateFile(file, input.dataset.imageOnly === "true");
        if (error) { input.value = ""; toast(error, true); preview.hidden = true; return; }
        const ext = (file.name.split(".").pop() || "").toLowerCase();
        preview.hidden = false;
        preview.innerHTML = `<div class="or-upload-preview__item"><i class="bi ${ext === "pdf" ? "bi-file-earmark-pdf-fill" : "bi-image-fill"}"></i><div><strong></strong><small>${formatBytes(file.size)}</small></div><button type="button" aria-label="حذف الملف"><i class="bi bi-x-lg"></i></button></div>`;
        preview.querySelector("strong").textContent = file.name;
        preview.querySelector("button")?.addEventListener("click", () => { input.value = ""; preview.hidden = true; preview.innerHTML = ""; });
    };

    qsa("[data-evidence-input]").forEach(input => {
        input.addEventListener("change", () => setUploadPreview(input));
        const zone = input.closest("[data-upload-zone]");
        if (!zone) return;
        ["dragenter", "dragover"].forEach(type => zone.addEventListener(type, event => { event.preventDefault(); zone.classList.add("is-dragging"); }));
        ["dragleave", "drop"].forEach(type => zone.addEventListener(type, event => { event.preventDefault(); zone.classList.remove("is-dragging"); }));
        zone.addEventListener("drop", event => {
            const file = event.dataTransfer?.files?.[0];
            if (!file) return;
            const dt = new DataTransfer(); dt.items.add(file); input.files = dt.files; setUploadPreview(input);
        });
    });

    let responseOpener = null;
    const modal = qs("[data-response-modal]");
    const openModal = () => { if (!modal) return; modal.classList.add("is-open"); modal.setAttribute("aria-hidden", "false"); document.body.style.overflow = "hidden"; window.setTimeout(() => qs("textarea, input[type=file], button[type=submit]", modal)?.focus(), 60); };
    const closeModal = () => { if (!modal) return; modal.classList.remove("is-open"); modal.setAttribute("aria-hidden", "true"); document.body.style.removeProperty("overflow"); responseOpener?.focus(); };
    qsa("[data-open-response]").forEach(button => button.addEventListener("click", () => { responseOpener = button; openModal(); }));
    qsa("[data-close-response]").forEach(button => button.addEventListener("click", closeModal));
    document.addEventListener("keydown", event => { if (event.key === "Escape") { closeModal(); closeImageViewer(); } });

    const responseText = qs("#ownerResponse");
    const responseCount = qs("[data-char-count]");
    const updateResponseCount = () => { if (responseText && responseCount) responseCount.textContent = String(responseText.value.length); };
    responseText?.addEventListener("input", updateResponseCount); updateResponseCount();

    const imageViewer = qs("[data-image-viewer]");
    const closeImageViewer = () => { if (!imageViewer) return; imageViewer.classList.remove("is-open"); imageViewer.setAttribute("aria-hidden", "true"); const img = qs("img", imageViewer); if (img) img.src = ""; document.body.style.removeProperty("overflow"); };
    qsa("[data-image-preview]").forEach(button => button.addEventListener("click", () => { if (!imageViewer) return; const img = qs("img", imageViewer); if (img) img.src = button.dataset.imagePreview || ""; imageViewer.classList.add("is-open"); imageViewer.setAttribute("aria-hidden", "false"); document.body.style.overflow = "hidden"; }));
    qs("[data-close-image]")?.addEventListener("click", closeImageViewer);
    imageViewer?.addEventListener("click", event => { if (event.target === imageViewer) closeImageViewer(); });

    const submitAjaxForm = async form => {
        if (form.dataset.submitting === "true") return;
        form.dataset.submitting = "true";
        let succeeded = false;
        const button = qs("button[type='submit']", form);
        const oldHtml = button?.innerHTML;
        if (button) { button.disabled = true; button.innerHTML = `<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>جاري الحفظ...</span>`; }
        try {
            const response = await fetch(form.action, { method: "POST", body: new FormData(form), headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" }, credentials: "same-origin" });
            const data = await safeJson(response);
            if (!response.ok || data.success !== true) { toast(data.message || "تعذر تنفيذ العملية.", true); return; }
            succeeded = true;
            toast(data.message || "تم الحفظ بنجاح.");
            if (data.redirectUrl) window.setTimeout(() => { window.location.href = data.redirectUrl; }, 500);
            else window.setTimeout(() => window.location.reload(), 500);
        } catch { toast("تعذر الاتصال بالخادم. حاول مرة أخرى.", true); }
        finally { if (!succeeded) { delete form.dataset.submitting; if (button) { button.disabled = false; button.innerHTML = oldHtml; } } }
    };

    qsa("[data-owner-report-form]").forEach(form => form.addEventListener("submit", event => {
        event.preventDefault();
        if (form.dataset.submitting === "true") return;
        const type = form.dataset.responseContentType;
        const message = qs("textarea[name='Message']", form)?.value.trim() || "";
        const file = qs("input[name='evidenceFile']", form)?.files?.[0];
        if (form.dataset.ownerReportForm === "response") {
            if (type === "Text" && !message) { toast("اكتب التوضيح المطلوب.", true); return; }
            if (type === "Image" && !file) { toast("أرفق صورة الإثبات المطلوبة.", true); return; }
            if (type === "Text" && file) { toast("المطلوب نص فقط دون مرفق.", true); return; }
            if (type === "Image" && message) { toast("المطلوب صورة فقط دون نص.", true); return; }
            if ((type === "Image" || type === "TextAndImage") && file && !/\.(jpe?g|png|webp)$/i.test(file.name)) { toast("مرفق الرد يجب أن يكون صورة JPG أو PNG أو WEBP.", true); return; }
            if (!message && !file) { toast("اكتب توضيحًا أو أرفق صورة إثبات.", true); return; }
        }
        if (!form.reportValidity()) return;
        if (form.dataset.ownerReportForm === "edit" && qs("input[name='RemoveEvidence']", form)?.checked && file) {
            toast("اختر إزالة المرفق أو استبداله، وليس كليهما.", true); return;
        }
        const fileInput = qs("[data-evidence-input]", form);
        const fileError = validateFile(fileInput?.files?.[0], fileInput?.dataset.imageOnly === "true");
        if (fileError) { toast(fileError, true); return; }
        if (form.dataset.ownerReportForm === "create") {
            const refId = qs("[data-reference-id]", form)?.value;
            if (!refId || Number(refId) <= 0) { toast("اختر الحجز أو التقييم المرتبط بالبلاغ أولًا.", true); qs("[data-create-step='1']")?.scrollIntoView({ behavior: "smooth", block: "center" }); return; }
        }
        submitAjaxForm(form);
    }));

    qsa("[data-owner-report-form='edit']").forEach(form => {
        const sync = () => {
            [["[data-edit-type]", "[data-edit-other-type]"], ["[data-edit-category]", "[data-edit-other-category]"]].forEach(([select, box]) => {
                const show = qs(select, form)?.value === "Other";
                const wrapper = qs(box, form);
                if (!wrapper) return;
                wrapper.hidden = !show;
                const input = qs("input", wrapper);
                if (input) { input.required = show; input.disabled = !show; }
            });
        };
        qsa("[data-edit-type], [data-edit-category]", form).forEach(select => select.addEventListener("change", sync));
        sync();
    });
    document.addEventListener("keydown", event => {
        if (event.key !== "Tab" || !modal?.classList.contains("is-open")) return;
        const focusable = qsa("button:not([disabled]), textarea:not([disabled]), input:not([type=hidden]):not([disabled]), select", modal)
            .filter(element => element.getClientRects().length > 0);
        const first = focusable[0], last = focusable[focusable.length - 1];
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    });

    const refTypeInput = qs("[data-reference-type]");
    const refIdInput = qs("[data-reference-id]");
    const resultsRoot = qs("[data-reference-results]");
    const searchInput = qs("[data-reference-search]");
    const loadMore = qs("[data-load-more]");
    let searchTimer = 0;
    let currentSkip = 0;
    let currentQuery = "";

    const kind = () => refTypeInput?.value === "Review" ? "Review" : "Booking";
    const selectReference = button => {
        if (!button || !refIdInput || !refTypeInput) return;
        qsa("[data-reference-option]", resultsRoot || document).forEach(item => item.classList.remove("is-selected"));
        button.classList.add("is-selected");
        refIdInput.value = button.dataset.id || "0";
        refTypeInput.value = button.dataset.kind || kind();
    };
    const bindReferenceButtons = root => qsa("[data-reference-option]", root).forEach(button => button.addEventListener("click", () => selectReference(button)));
    if (resultsRoot) bindReferenceButtons(resultsRoot);

    qsa("[data-ref-kind]").forEach(button => button.addEventListener("click", () => {
        qsa("[data-ref-kind]").forEach(item => item.classList.toggle("is-active", item === button));
        const nextKind = button.dataset.refKind === "Review" ? "Review" : "Booking";
        if (refTypeInput) refTypeInput.value = nextKind;
        if (refIdInput) refIdInput.value = "0";
        qsa("[data-static-list]", resultsRoot || document).forEach(list => list.classList.toggle("d-none", list.dataset.staticList !== nextKind));
        if (searchInput) searchInput.value = "";
        currentQuery = ""; currentSkip = 0; if (loadMore) loadMore.hidden = true;
    }));

    const renderSearchResults = (items, append) => {
        if (!resultsRoot) return;
        let dynamic = qs("[data-dynamic-list]", resultsRoot);
        if (!dynamic) { dynamic = document.createElement("div"); dynamic.dataset.dynamicList = "true"; resultsRoot.appendChild(dynamic); }
        qsa("[data-static-list]", resultsRoot).forEach(list => list.classList.add("d-none"));
        if (!append) dynamic.innerHTML = "";
        if (!append && (!items || items.length === 0)) { dynamic.innerHTML = `<div class="or-mini-empty">لا توجد نتائج مطابقة.</div>`; return; }
        items.forEach(item => {
            const button = document.createElement("button");
            button.type = "button"; button.className = "or-reference-option"; button.dataset.referenceOption = "true"; button.dataset.id = item.id; button.dataset.kind = kind();
            button.innerHTML = `<span class="or-reference-option__icon ${kind() === "Review" ? "is-review" : ""}"><i class="bi ${kind() === "Review" ? "bi-star-fill" : "bi-calendar2-check-fill"}"></i></span><div><strong></strong><small></small><span><i class="bi bi-person-fill"></i><b></b></span></div><em></em><i class="bi bi-check-circle-fill or-selected-check"></i>`;
            button.querySelector("strong").textContent = item.title || ""; button.querySelector("small").textContent = item.placeName || item.subtitle || ""; button.querySelector("b").textContent = item.customerName || "عميل"; button.querySelector("em").textContent = item.date || "";
            button.addEventListener("click", () => selectReference(button)); dynamic.appendChild(button);
        });
    };

    const searchReferences = async (append = false) => {
        if (!resultsRoot || !searchInput) return;
        const endpoint = kind() === "Review" ? resultsRoot.dataset.reviewUrl : resultsRoot.dataset.bookingUrl;
        if (!endpoint) return;
        const query = searchInput.value.trim();
        if (!append) { currentSkip = 0; currentQuery = query; }
        const url = new URL(endpoint, window.location.origin); url.searchParams.set("q", currentQuery); url.searchParams.set("take", "10"); url.searchParams.set("skip", String(currentSkip));
        try {
            const response = await fetch(url, { headers: { "Accept": "application/json" }, credentials: "same-origin" });
            const data = await safeJson(response); if (!response.ok || !data.success) return;
            renderSearchResults(data.items || [], append); currentSkip = data.nextSkip || 0; if (loadMore) loadMore.hidden = !data.hasMore;
        } catch { toast("تعذر البحث الآن.", true); }
    };

    searchInput?.addEventListener("input", () => { window.clearTimeout(searchTimer); searchTimer = window.setTimeout(() => { if (searchInput.value.trim().length === 0) { qsa("[data-dynamic-list]", resultsRoot || document).forEach(x => x.remove()); qsa("[data-static-list]", resultsRoot || document).forEach(list => list.classList.toggle("d-none", list.dataset.staticList !== kind())); if (loadMore) loadMore.hidden = true; return; } searchReferences(false); }, 280); });
    loadMore?.addEventListener("click", () => searchReferences(true));

    const detailsText = qs("[data-details-text]");
    const detailsCount = qs("[data-details-count]");
    const updateDetailsCount = () => { if (detailsText && detailsCount) detailsCount.textContent = String(detailsText.value.length); };
    detailsText?.addEventListener("input", updateDetailsCount); updateDetailsCount();

    const syncOtherFields = () => {
        const typeOther = qs("input[name='ReportType']:checked")?.value === "Other";
        const categoryOther = qs("select[name='Category']")?.value === "Other";
        const typeBox = qs("[data-other-type]"); const catBox = qs("[data-other-category]");
        if (typeBox) typeBox.hidden = !typeOther; if (catBox) catBox.hidden = !categoryOther;
    };
    qsa("input[name='ReportType']").forEach(input => input.addEventListener("change", syncOtherFields));
    qs("select[name='Category']")?.addEventListener("change", syncOtherFields); syncOtherFields();
})();
