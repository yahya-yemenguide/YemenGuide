(() => {
    "use strict";

    const MAX_FILE_SIZE = 5 * 1024 * 1024;
    const ALLOWED_EXTENSIONS = ["jpg", "jpeg", "png", "webp", "pdf"];
    const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp", "application/pdf"];
    const state = {
        requests: new WeakSet(),
        previewUrl: null,
        placeTimer: null,
        placeAbort: null,
        bookingTimer: null,
        bookingAbort: null,
        bookingBrowserAbort: null
    };

    const qs = (selector, root = document) => root.querySelector(selector);
    const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));
    const text = value => (value ?? "").toString().trim();

    document.addEventListener("DOMContentLoaded", () => {
        const page = qs("[data-customer-reports-page]");
        if (!page) return;

        setupCaseCards(page);
        setupCopyButtons(page);
        setupFlashMessages(page);

        const mode = page.getAttribute("data-customer-reports-page");
        if (mode === "create") setupCreatePage(page);
        if (mode === "details") setupDetailsPage(page);
    });

    /* =====================================================================
       Shared UI
       ===================================================================== */

    function setupCaseCards(root) {
        qsa("[data-report-card]", root).forEach(card => {
            const url = card.getAttribute("data-report-url");
            if (!url) return;

            const open = event => {
                if (event?.target?.closest("a,button,input,select,textarea,label")) return;
                window.location.href = url;
            };

            card.addEventListener("click", open);
            card.addEventListener("keydown", event => {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    open(event);
                }
            });
        });
    }

    function setupCopyButtons(root) {
        qsa("[data-copy-report-number]", root).forEach(button => {
            button.addEventListener("click", async event => {
                event.preventDefault();
                event.stopPropagation();
                const value = button.getAttribute("data-copy-report-number") || "";
                if (!value) return;

                try {
                    await navigator.clipboard.writeText(value);
                    showToast("تم نسخ رقم القضية.", "success");
                } catch {
                    const temp = document.createElement("textarea");
                    temp.value = value;
                    temp.style.position = "fixed";
                    temp.style.opacity = "0";
                    document.body.appendChild(temp);
                    temp.select();
                    document.execCommand("copy");
                    temp.remove();
                    showToast("تم نسخ رقم القضية.", "success");
                }
            });
        });
    }

    function setupFlashMessages(root) {
        qsa("[data-report-flash]", root).forEach(message => {
            window.setTimeout(() => message.classList.add("is-fading"), 5000);
            window.setTimeout(() => message.remove(), 5500);
        });
    }

    function showToast(message, tone = "info") {
        let host = qs(".cr-toast-host");
        if (!host) {
            host = document.createElement("div");
            host.className = "cr-toast-host";
            document.body.appendChild(host);
        }

        const toast = document.createElement("div");
        toast.className = `cr-toast is-${tone}`;
        toast.setAttribute("role", tone === "error" ? "alert" : "status");

        const icon = tone === "success"
            ? "bi-check-circle-fill"
            : tone === "error"
                ? "bi-exclamation-triangle-fill"
                : "bi-info-circle-fill";

        toast.innerHTML = `<i class="bi ${icon}"></i><span></span>`;
        qs("span", toast).textContent = message;
        host.appendChild(toast);

        requestAnimationFrame(() => toast.classList.add("is-visible"));
        window.setTimeout(() => {
            toast.classList.remove("is-visible");
            window.setTimeout(() => toast.remove(), 220);
        }, 3600);
    }

    function confirmDialog({ title, message, confirmText, tone = "primary" }) {
        return new Promise(resolve => {
            const overlay = document.createElement("div");
            overlay.className = "cr-dialog-backdrop";
            overlay.innerHTML = `
                <div class="cr-dialog" role="dialog" aria-modal="true" aria-labelledby="cr-dialog-title">
                    <button type="button" class="cr-dialog__close" aria-label="إغلاق"><i class="bi bi-x-lg"></i></button>
                    <span class="cr-dialog__icon is-${tone}"><i class="bi ${tone === "danger" ? "bi-exclamation-triangle-fill" : "bi-shield-check"}"></i></span>
                    <h3 id="cr-dialog-title"></h3>
                    <p></p>
                    <div class="cr-dialog__actions">
                        <button type="button" class="cr-dialog__cancel">رجوع</button>
                        <button type="button" class="cr-dialog__confirm is-${tone}"></button>
                    </div>
                </div>`;

            qs("h3", overlay).textContent = title;
            qs("p", overlay).textContent = message;
            qs(".cr-dialog__confirm", overlay).textContent = confirmText;
            document.body.appendChild(overlay);
            document.documentElement.classList.add("cr-dialog-open");

            const finish = value => {
                overlay.classList.remove("is-visible");
                document.documentElement.classList.remove("cr-dialog-open");
                window.setTimeout(() => overlay.remove(), 180);
                resolve(value);
            };

            qs(".cr-dialog__confirm", overlay).addEventListener("click", () => finish(true));
            qs(".cr-dialog__cancel", overlay).addEventListener("click", () => finish(false));
            qs(".cr-dialog__close", overlay).addEventListener("click", () => finish(false));
            overlay.addEventListener("click", event => { if (event.target === overlay) finish(false); });
            overlay.addEventListener("keydown", event => { if (event.key === "Escape") finish(false); });

            requestAnimationFrame(() => {
                overlay.classList.add("is-visible");
                qs(".cr-dialog__confirm", overlay)?.focus();
            });
        });
    }

    function setBusy(form, busy) {
        const button = qs("button[type='submit']", form);
        qsa("input,textarea,select,button", form).forEach(field => {
            if (field === button || field.matches("button")) return;
            field.toggleAttribute("aria-busy", busy);
        });

        if (!button) return;
        if (busy) {
            button.dataset.originalHtml = button.innerHTML;
            button.disabled = true;
            button.classList.add("is-loading");
            button.innerHTML = `<span class="cr-button-spinner"></span><span>جارٍ الإرسال...</span>`;
        } else {
            button.disabled = false;
            button.classList.remove("is-loading");
            if (button.dataset.originalHtml) button.innerHTML = button.dataset.originalHtml;
        }
    }

    async function submitAjaxForm(form) {
        if (state.requests.has(form)) return;
        state.requests.add(form);
        setBusy(form, true);

        try {
            const response = await fetch(form.action, {
                method: (form.method || "post").toUpperCase(),
                body: new FormData(form),
                credentials: "same-origin",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Accept": "application/json"
                }
            });

            let payload = null;
            try { payload = await response.json(); } catch { /* ignored */ }

            if (!response.ok || payload?.success !== true) {
                throw new Error(payload?.message || "تعذر تنفيذ العملية الآن. حاول مرة أخرى.");
            }

            showToast(payload?.message || "تمت العملية بنجاح.", "success");
            const redirect = payload?.redirectUrl;
            window.setTimeout(() => {
                if (redirect) window.location.href = redirect;
                else window.location.reload();
            }, 520);
        } catch (error) {
            showToast(error?.message || "حدث خطأ غير متوقع.", "error");
            setBusy(form, false);
            state.requests.delete(form);
        }
    }

    /* =====================================================================
       Create report
       ===================================================================== */

    function setupCreatePage(page) {
        const form = qs("[data-report-create-form]", page);
        if (!form) return;

        const refreshReview = () => {
            updateFinalReview(form);
            updateReferenceAside(form);
        };

        setupTypeSelector(form, page, refreshReview);
        setupCategorySelector(form);
        setupBookingPicker(form, refreshReview);
        setupPlacePicker(form, refreshReview);
        setupCreateUpload(form, refreshReview);
        setupCharacterCounters(form);
        setupFinalReview(form);
        setupProgressObserver(page);
        refreshReview();

        form.addEventListener("submit", async event => {
            event.preventDefault();
            const validation = validateCreateForm(form);
            if (!validation.valid) {
                showToast(validation.message, "error");
                focusField(validation.element);
                return;
            }

            const confirmed = await confirmDialog({
                title: "إرسال البلاغ إلى الإدارة؟",
                message: "سيتم إنشاء قضية برقم مستقل، وكل التحديثات اللاحقة ستبقى داخل نفس القضية.",
                confirmText: "إرسال البلاغ",
                tone: "primary"
            });
            if (!confirmed) return;
            await submitAjaxForm(form);
        });
    }

    function setupTypeSelector(form, page, onChange) {
        const radios = qsa("input[name='ReportType']", form);
        const otherPanel = qs("[data-other-type-panel]", form);
        const categorySelect = qs("[data-category-select]", form);
        const categoryField = categorySelect?.closest(".cr-field");
        const referenceSection = qs("[data-report-step='reference']", form);
        const progressReference = qs("[data-progress-step='reference']", page);
        const progressReferenceLine = qs("[data-progress-line='reference']", page);

        const render = () => {
            const selected = qs("input[name='ReportType']:checked", form)?.value || "";
            if (otherPanel) otherPanel.hidden = selected !== "Other";

            qsa("[data-reference-panel]", form).forEach(panel => panel.hidden = true);

            const needsReference = ["Booking", "Place", "Review", "Promotion", "Advertisement"].includes(selected);
            if (referenceSection) referenceSection.hidden = !needsReference;
            if (progressReference) progressReference.hidden = !needsReference;
            if (progressReferenceLine) progressReferenceLine.hidden = !needsReference;

            if (needsReference) {
                const key = selected === "Booking"
                    ? "Booking"
                    : selected === "Place"
                        ? "Place"
                        : "Context";

                const panel = qs(`[data-reference-panel='${key}']`, form);
                if (panel) panel.hidden = false;
            }

            if (categoryField) categoryField.hidden = selected === "Other";
            if (selected === "Other" && categorySelect instanceof HTMLSelectElement) {
                categorySelect.value = "";
                const otherCategory = qs("[data-other-category-panel]", form);
                if (otherCategory) otherCategory.hidden = true;
            }

            onChange?.();
        };

        radios.forEach(radio => radio.addEventListener("change", render));
        render();
    }

    function setupCategorySelector(form) {
        const select = qs("[data-category-select]", form);
        const panel = qs("[data-other-category-panel]", form);
        if (!(select instanceof HTMLSelectElement) || !panel) return;

        const render = () => panel.hidden = select.value !== "Other";
        select.addEventListener("change", render);
        render();
    }

    function syncBookingSelection(form) {
        if (!(form instanceof HTMLFormElement)) return;

        const selectedId = Number(qs("[data-booking-id]", form)?.value || 0);

        qsa("[data-booking-id-value]", form).forEach(button => {
            const id = Number(button.getAttribute("data-booking-id-value") || 0);
            const isSelected = selectedId > 0 && id === selectedId;

            button.classList.toggle("is-selected", isSelected);
            button.setAttribute("aria-pressed", isSelected ? "true" : "false");
            if (isSelected) {
                button.setAttribute("aria-current", "true");
            } else {
                button.removeAttribute("aria-current");
            }
        });
    }

    function syncPlaceSelection(form) {
        if (!(form instanceof HTMLFormElement)) return;

        const selectedId = Number(qs("[data-place-id]", form)?.value || 0);

        qsa("[data-place-quick], [data-place-result-id]", form).forEach(button => {
            const raw = button.getAttribute("data-place-quick") || button.getAttribute("data-place-result-id") || "0";
            const id = Number(raw);
            const isSelected = selectedId > 0 && id === selectedId;

            button.classList.toggle("is-selected", isSelected);
            button.setAttribute("aria-pressed", isSelected ? "true" : "false");
            if (isSelected) {
                button.setAttribute("aria-current", "true");
            } else {
                button.removeAttribute("aria-current");
            }
        });
    }

    function setupBookingPicker(form, onChange) {
        const hidden = qs("[data-booking-id]", form);
        const picker = qs("[data-booking-picker]", form);
        const selected = qs("[data-booking-selected]", form);
        const selectedPlace = qs("[data-booking-selected-place]", form);
        const selectedNumber = qs("[data-booking-selected-number]", form);
        const selectedDate = qs("[data-booking-selected-date]", form);
        const selectedStatus = qs("[data-booking-selected-status]", form);
        const search = qs("[data-booking-search-input]", form);
        const liveResults = qs("[data-booking-live-results]", form);
        const spinner = qs("[data-booking-search-spinner]", form);
        const quickList = qs("[data-booking-quick-list]", form);

        if (!(hidden instanceof HTMLInputElement)) return;

        const choose = booking => {
            const id = Number(booking?.id);
            if (!Number.isInteger(id) || id <= 0) return;

            hidden.value = String(id);
            if (selectedPlace) selectedPlace.textContent = booking.placeName || "منشأة";
            if (selectedNumber) selectedNumber.textContent = `#${id}`;
            if (selectedDate) selectedDate.textContent = booking.date || "";
            if (selectedStatus) selectedStatus.textContent = booking.subtitle || "";
            if (selected) selected.hidden = false;
            if (picker) picker.hidden = false;

            // نبقي البحث والاقتراحات ظاهرة بعد الاختيار، ونميز المرجع المحدد بصريًا.
            syncBookingSelection(form);
            onChange?.();
        };

        qsa("[data-booking-select]", form).forEach(button => {
            button.addEventListener("click", () => choose({
                id: button.getAttribute("data-booking-id-value"),
                placeName: button.getAttribute("data-booking-place") || "",
                date: button.getAttribute("data-booking-date") || "",
                subtitle: button.getAttribute("data-booking-status") || ""
            }));
        });

        qs("[data-booking-change]", form)?.addEventListener("click", () => {
            if (picker) picker.hidden = false;
            if (quickList && !(search instanceof HTMLInputElement && text(search.value))) {
                quickList.hidden = false;
            }
            picker?.scrollIntoView({ behavior: "smooth", block: "center" });
            window.setTimeout(() => search?.focus(), 260);
        });

        if (hidden.value && selected) {
            selected.hidden = false;
            if (picker) picker.hidden = false;
        }

        syncBookingSelection(form);

        if (search instanceof HTMLInputElement && liveResults) {
            search.addEventListener("input", () => {
                window.clearTimeout(state.bookingTimer);
                const query = text(search.value);

                if (!query) {
                    liveResults.hidden = true;
                    liveResults.innerHTML = "";
                    if (quickList) quickList.hidden = false;
                    return;
                }

                if (quickList) quickList.hidden = true;
                state.bookingTimer = window.setTimeout(async () => {
                    state.bookingAbort?.abort();
                    state.bookingAbort = new AbortController();
                    if (spinner) spinner.hidden = false;

                    try {
                        const data = await fetchBookingPage(query, 5, 0, state.bookingAbort.signal);
                        renderBookingResults(liveResults, data.items || [], choose, {
                            emptyText: "لم نجد حجزًا مطابقًا لبحثك.",
                            compact: true
                        });
                        syncBookingSelection(form);
                    } catch (error) {
                        if (error?.name !== "AbortError") {
                            liveResults.innerHTML = `<div class="cr-place-results__message">تعذر البحث الآن. حاول مرة أخرى.</div>`;
                            liveResults.hidden = false;
                        }
                    } finally {
                        if (spinner) spinner.hidden = true;
                    }
                }, 280);
            });
        }

        setupBookingBrowser(form, choose);
    }

    function setupBookingBrowser(form, choose) {
        const modal = qs("[data-booking-browser]", form);
        const openButton = qs("[data-booking-open-browser]", form);
        const search = qs("[data-booking-browser-search]", form);
        const results = qs("[data-booking-browser-results]", form);
        const spinner = qs("[data-booking-browser-spinner]", form);
        const loadMore = qs("[data-booking-load-more]", form);
        const hint = qs("[data-booking-browser-hint]", form);
        if (!modal || !openButton || !(search instanceof HTMLInputElement) || !results || !loadMore) return;

        let currentQuery = "";
        let nextSkip = 0;
        let timer = null;

        const close = () => {
            modal.hidden = true;
            modal.setAttribute("aria-hidden", "true");
            document.body.classList.remove("cr-dialog-open");
            state.bookingBrowserAbort?.abort();
        };

        const load = async (append = false) => {
            state.bookingBrowserAbort?.abort();
            state.bookingBrowserAbort = new AbortController();
            if (spinner) spinner.hidden = false;
            loadMore.disabled = true;

            try {
                const skip = append ? nextSkip : 0;
                const data = await fetchBookingPage(currentQuery, 10, skip, state.bookingBrowserAbort.signal);
                const items = data.items || [];

                if (!append) results.innerHTML = "";
                renderBookingResults(results, items, booking => {
                    choose(booking);
                    close();
                }, {
                    append: true,
                    emptyText: "لا توجد حجوزات مطابقة.",
                    compact: false
                });

                nextSkip = Number(data.nextSkip) || (skip + items.length);
                loadMore.hidden = !data.hasMore;
                if (hint) hint.textContent = items.length || append ? "اختر الحجز المطلوب" : "لا توجد نتائج";
            } catch (error) {
                if (error?.name !== "AbortError") {
                    if (!append) results.innerHTML = `<div class="cr-booking-browser__empty"><i class="bi bi-wifi-off"></i><span>تعذر تحميل الحجوزات الآن.</span></div>`;
                    showToast("تعذر تحميل الحجوزات الآن.", "error");
                }
            } finally {
                if (spinner) spinner.hidden = true;
                loadMore.disabled = false;
            }
        };

        openButton.addEventListener("click", () => {
            modal.hidden = false;
            modal.setAttribute("aria-hidden", "false");
            document.body.classList.add("cr-dialog-open");
            currentQuery = "";
            nextSkip = 0;
            search.value = "";
            load(false);
            window.setTimeout(() => search.focus(), 60);
        });

        qsa("[data-booking-close]", modal).forEach(button => button.addEventListener("click", close));
        loadMore.addEventListener("click", () => load(true));

        search.addEventListener("input", () => {
            window.clearTimeout(timer);
            currentQuery = text(search.value);
            timer = window.setTimeout(() => {
                nextSkip = 0;
                load(false);
            }, 280);
        });

        document.addEventListener("keydown", event => {
            if (event.key === "Escape" && !modal.hidden) close();
        });
    }

    async function fetchBookingPage(query, take, skip, signal) {
        const response = await fetch(
            `/Reports/SearchBookings?q=${encodeURIComponent(query || "")}&take=${encodeURIComponent(take)}&skip=${encodeURIComponent(skip)}`,
            {
                credentials: "same-origin",
                signal,
                headers: { "Accept": "application/json" }
            }
        );

        const data = await response.json();
        if (!response.ok || !data?.success) throw new Error("تعذر تحميل الحجوزات.");
        return data;
    }

    function renderBookingResults(container, items, choose, options = {}) {
        if (!options.append) container.innerHTML = "";

        if (!items.length && !container.children.length) {
            const empty = document.createElement("div");
            empty.className = options.compact ? "cr-place-results__message" : "cr-booking-browser__empty";
            empty.innerHTML = options.compact
                ? ""
                : `<i class="bi bi-calendar2-x"></i><span></span>`;
            if (options.compact) empty.textContent = options.emptyText || "لا توجد نتائج.";
            else qs("span", empty).textContent = options.emptyText || "لا توجد نتائج.";
            container.appendChild(empty);
            container.hidden = false;
            return;
        }

        items.forEach(item => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = options.compact ? "cr-booking-search-result" : "cr-booking-browser-item";
            button.setAttribute("data-booking-id-value", String(item.id || ""));
            button.setAttribute("aria-pressed", "false");
            button.innerHTML = `
                <span class="cr-booking-result-icon"><i class="bi bi-calendar2-check"></i></span>
                <span class="cr-booking-result-body">
                    <strong></strong>
                    <small><b></b><em></em><em></em></small>
                </span>
                <i class="bi bi-chevron-left"></i>`;

            qs("strong", button).textContent = item.placeName || "منشأة";
            qs("b", button).textContent = `#${item.id}`;
            const meta = qsa("em", button);
            meta[0].textContent = item.date || "";
            meta[1].textContent = item.subtitle || "";
            button.addEventListener("click", () => choose(item));
            container.appendChild(button);
        });

        container.hidden = false;
    }

    function setupPlacePicker(form, onChange) {
        const hidden = qs("[data-place-id]", form);
        const picker = qs("[data-place-picker]", form);
        const search = qs("[data-place-search-input]", form);
        const results = qs("[data-place-search-results]", form);
        const selected = qs("[data-place-selected]", form);
        const selectedName = qs("[data-place-selected-name]", form);
        const spinner = qs("[data-place-search-spinner]", form);
        if (!(hidden instanceof HTMLInputElement)) return;

        const choose = (id, name) => {
            hidden.value = String(id);
            if (selectedName) selectedName.textContent = name || `منشأة #${id}`;
            if (selected) selected.hidden = false;
            if (picker) picker.hidden = false;

            // لا نخفي الاقتراحات بعد الاختيار؛ نظهر علامة واضحة على المنشأة المحددة.
            syncPlaceSelection(form);
            onChange?.();
        };

        qsa("[data-place-quick]", form).forEach(button => {
            button.addEventListener("click", () => {
                const id = Number(button.getAttribute("data-place-quick"));
                if (id > 0) choose(id, button.getAttribute("data-place-name") || button.textContent || "");
            });
        });

        qs("[data-place-clear]", form)?.addEventListener("click", () => {
            if (picker) picker.hidden = false;
            picker?.scrollIntoView({ behavior: "smooth", block: "center" });
            window.setTimeout(() => search?.focus(), 260);
        });

        if (hidden.value && selected) {
            selected.hidden = false;
            if (picker) picker.hidden = false;
        }

        syncPlaceSelection(form);

        if (!(search instanceof HTMLInputElement) || !results) return;

        search.addEventListener("input", () => {
            window.clearTimeout(state.placeTimer);
            const query = text(search.value);
            if (query.length < 2) {
                results.hidden = true;
                results.innerHTML = "";
                return;
            }

            state.placeTimer = window.setTimeout(async () => {
                state.placeAbort?.abort();
                state.placeAbort = new AbortController();
                if (spinner) spinner.hidden = false;

                try {
                    const response = await fetch(`/Reports/SearchPlaces?q=${encodeURIComponent(query)}&take=12`, {
                        credentials: "same-origin",
                        signal: state.placeAbort.signal,
                        headers: { "Accept": "application/json" }
                    });
                    const data = await response.json();
                    if (!response.ok || !data?.success) throw new Error();
                    renderPlaceResults(results, data.items || [], choose);
                    syncPlaceSelection(form);
                } catch (error) {
                    if (error?.name !== "AbortError") {
                        results.innerHTML = `<div class="cr-place-results__message">تعذر البحث الآن. حاول مرة أخرى.</div>`;
                        results.hidden = false;
                    }
                } finally {
                    if (spinner) spinner.hidden = true;
                }
            }, 320);
        });

        document.addEventListener("click", event => {
            if (!event.target.closest("[data-place-search]")) results.hidden = true;
        });
    }

    function renderPlaceResults(container, items, choose) {
        container.innerHTML = "";
        if (!items.length) {
            const empty = document.createElement("div");
            empty.className = "cr-place-results__message";
            empty.textContent = "لم نجد منشأة مطابقة.";
            container.appendChild(empty);
            container.hidden = false;
            return;
        }

        items.forEach(item => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "cr-place-result";
            button.setAttribute("data-place-result-id", String(item.id || ""));
            button.setAttribute("aria-pressed", "false");
            button.innerHTML = `<span><i class="bi bi-building"></i></span><div><strong></strong><small></small></div><i class="bi bi-chevron-left"></i>`;
            qs("strong", button).textContent = item.title || `منشأة #${item.id}`;
            qs("small", button).textContent = item.subtitle || "اضغط للاختيار";
            button.addEventListener("click", () => choose(item.id, item.title));
            container.appendChild(button);
        });
        container.hidden = false;
    }

    function setupCreateUpload(form, onChange) {
        const input = qs("[data-report-evidence-input]", form);
        const zone = qs("[data-report-upload-zone]", form);
        const preview = qs("[data-report-upload-preview]", form);
        if (!(input instanceof HTMLInputElement) || !zone || !preview) return;

        const render = () => {
            renderFilePreview(input.files?.[0] || null, preview, input);
            onChange?.();
        };
        input.addEventListener("change", render);

        ["dragenter", "dragover"].forEach(name => zone.addEventListener(name, event => {
            event.preventDefault();
            zone.classList.add("is-dragging");
        }));
        ["dragleave", "drop"].forEach(name => zone.addEventListener(name, event => {
            event.preventDefault();
            zone.classList.remove("is-dragging");
        }));

        zone.addEventListener("drop", event => {
            const file = event.dataTransfer?.files?.[0];
            if (!file) return;
            const validation = validateFile(file);
            if (!validation.valid) {
                showToast(validation.message, "error");
                return;
            }
            try {
                const transfer = new DataTransfer();
                transfer.items.add(file);
                input.files = transfer.files;
                render();
            } catch {
                showToast("اختر الملف بالضغط على منطقة الرفع.", "info");
            }
        });
    }

    function renderFilePreview(file, preview, input) {
        if (state.previewUrl) {
            URL.revokeObjectURL(state.previewUrl);
            state.previewUrl = null;
        }
        preview.innerHTML = "";
        if (!file) { preview.hidden = true; return; }

        const validation = validateFile(file);
        if (!validation.valid) {
            input.value = "";
            preview.hidden = true;
            showToast(validation.message, "error");
            return;
        }

        const isImage = file.type.startsWith("image/");
        const card = document.createElement("div");
        card.className = "cr-upload-preview__card";

        const thumb = document.createElement("span");
        thumb.className = "cr-upload-preview__thumb";
        if (isImage) {
            state.previewUrl = URL.createObjectURL(file);
            const image = document.createElement("img");
            image.src = state.previewUrl;
            image.alt = "معاينة المرفق";
            thumb.appendChild(image);
        } else {
            thumb.innerHTML = `<i class="bi bi-file-earmark-pdf-fill"></i>`;
        }

        const info = document.createElement("div");
        const name = document.createElement("strong");
        name.textContent = file.name;
        const size = document.createElement("small");
        size.textContent = `${formatBytes(file.size)} · ${isImage ? "صورة" : "PDF"}`;
        info.append(name, size);

        const remove = document.createElement("button");
        remove.type = "button";
        remove.innerHTML = `<i class="bi bi-trash3"></i>`;
        remove.setAttribute("aria-label", "إزالة المرفق");
        remove.addEventListener("click", () => {
            input.value = "";
            renderFilePreview(null, preview, input);
            updateFinalReview(input.form);
        });

        card.append(thumb, info, remove);
        preview.appendChild(card);
        preview.hidden = false;
    }

    function setupCharacterCounters(root) {
        qsa("[data-report-character-count]", root).forEach(counter => {
            const id = counter.getAttribute("data-report-character-count");
            const field = id ? document.getElementById(id) : null;
            if (!(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) return;
            const max = Number(field.maxLength) > 0 ? Number(field.maxLength) : 4000;
            const render = () => counter.textContent = `${field.value.length.toLocaleString("ar")} / ${max.toLocaleString("ar")}`;
            field.addEventListener("input", render);
            render();
        });
    }

    function setupFinalReview(form) {
        qs("[data-review-details]", form)?.addEventListener("click", () => {
            const details = qs("[data-report-step='details']", form);
            details?.scrollIntoView({ behavior: "smooth", block: "start" });
            window.setTimeout(() => qs("input[name='Title']", form)?.focus(), 450);
        });
    }

    function updateFinalReview(form) {
        if (!(form instanceof HTMLFormElement)) return;

        const typeInput = qs("input[name='ReportType']:checked", form);
        const typeLabel = typeInput?.closest("[data-report-type-label]")?.getAttribute("data-report-type-label") || "—";
        const typeTarget = qs("[data-review-type]", form);
        const referenceTarget = qs("[data-review-reference]", form);
        const evidenceTarget = qs("[data-review-evidence]", form);

        if (typeTarget) typeTarget.textContent = typeLabel;

        let reference = "لا يحتاج مرجعًا";
        const type = typeInput?.value || "";

        if (type === "Booking") {
            const id = text(qs("[data-booking-id]", form)?.value);
            const place = text(qs("[data-booking-selected-place]", form)?.textContent);
            reference = id ? `${place || "حجز"} · #${id}` : "لم يتم اختيار الحجز بعد";
        } else if (type === "Place") {
            const id = text(qs("[data-place-id]", form)?.value);
            const place = text(qs("[data-place-selected-name]", form)?.textContent);
            reference = id ? (place || `منشأة #${id}`) : "لم يتم اختيار المنشأة بعد";
        } else if (["Review", "Promotion", "Advertisement"].includes(type)) {
            reference = text(qs("[data-reference-panel='Context'] strong", form)?.textContent) || "عنصر مرتبط تلقائيًا";
        }

        if (referenceTarget) referenceTarget.textContent = reference;

        const file = qs("input[name='evidenceFile']", form)?.files?.[0];
        if (evidenceTarget) evidenceTarget.textContent = file ? file.name : "بدون مرفق";
    }

    function updateReferenceAside(form) {
        if (!(form instanceof HTMLFormElement)) return;

        const type = qs("input[name='ReportType']:checked", form)?.value || "";
        const empty = qs("[data-reference-aside-empty]", form);
        const emptyTitle = qs("[data-reference-aside-empty-title]", form);
        const emptyText = qs("[data-reference-aside-empty-text]", form);
        const bookingSelected = qs("[data-booking-selected]", form);
        const placeSelected = qs("[data-place-selected]", form);
        const contextSummary = qs("[data-reference-context-summary]", form);

        const bookingId = Number(qs("[data-booking-id]", form)?.value || 0);
        const placeId = Number(qs("[data-place-id]", form)?.value || 0);
        const isBookingSelected = type === "Booking" && Number.isInteger(bookingId) && bookingId > 0;
        const isPlaceSelected = type === "Place" && Number.isInteger(placeId) && placeId > 0;
        const isContext = ["Review", "Promotion", "Advertisement"].includes(type);

        if (bookingSelected) bookingSelected.hidden = !isBookingSelected;
        if (placeSelected) placeSelected.hidden = !isPlaceSelected;
        if (contextSummary) contextSummary.hidden = !isContext;

        const hasSelection = isBookingSelected || isPlaceSelected || isContext;
        if (empty) empty.hidden = hasSelection;

        if (!hasSelection) {
            if (type === "Booking") {
                if (emptyTitle) emptyTitle.textContent = "لم تحدد حجزًا بعد";
                if (emptyText) emptyText.textContent = "ابحث برقم الحجز أو اسم المنشأة، أو اختر واحدًا من آخر الحجوزات الظاهرة.";
            } else if (type === "Place") {
                if (emptyTitle) emptyTitle.textContent = "لم تحدد منشأة بعد";
                if (emptyText) emptyText.textContent = "ابحث باسم المنشأة أو استخدم أحد الاقتراحات السريعة، وسيظهر اختيارك هنا.";
            } else {
                if (emptyTitle) emptyTitle.textContent = "لم تحدد مرجعًا بعد";
                if (emptyText) emptyText.textContent = "اختر نوع المشكلة أولًا، وبعدها سنعرض لك المرجع المناسب بطريقة مختصرة وواضحة.";
            }
        }
    }

    function setupProgressObserver(page) {
        const progressItems = qsa(".cr-progress > span", page);
        const sections = qsa("[data-report-step]", page);
        if (!progressItems.length || !sections.length || !("IntersectionObserver" in window)) return;

        const map = { type: "type", reference: "reference", details: "details", evidence: "evidence" };
        const order = ["type", "reference", "details", "evidence"];

        const observer = new IntersectionObserver(entries => {
            const visible = entries
                .filter(entry => entry.isIntersecting && !entry.target.hidden)
                .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
            if (!visible) return;

            const currentKey = map[visible.target.getAttribute("data-report-step")];
            const currentIndex = order.indexOf(currentKey);
            progressItems.forEach(item => {
                const key = item.getAttribute("data-progress-step");
                const index = order.indexOf(key);
                item.classList.toggle("is-active", index >= 0 && index <= currentIndex && !item.hidden);
            });
        }, { rootMargin: "-28% 0px -55%", threshold: [0.05, 0.25, 0.5] });

        sections.forEach(section => observer.observe(section));
    }

    function validateCreateForm(form) {
        const type = qs("input[name='ReportType']:checked", form)?.value;
        if (!type) return invalid("اختر نوع المشكلة.", qs("[data-report-types]", form));

        if (type === "Other") {
            const field = qs("input[name='OtherTypeDescription']", form);
            const value = field?.value.trim() || "";
            if (value.length < 3) return invalid("اكتب نوع المشكلة عند اختيار «أخرى».", field);
            if (value.length > 50) return invalid("وصف نوع المشكلة يجب ألا يتجاوز 50 حرفًا.", field);
        }

        if (type === "Booking") {
            const booking = qs("input[name='BookingId']", form);
            if (!booking || Number(booking.value) <= 0) return invalid("اختر الحجز المرتبط بالمشكلة.", qs("[data-booking-search-input]", form));
        }

        if (type === "Place") {
            const place = qs("input[name='PlaceId']", form);
            if (!place || Number(place.value) <= 0) return invalid("اختر المنشأة المرتبطة بالمشكلة.", qs("[data-place-search-input]", form));
        }

        const contextual = { Review: "ReviewId", Promotion: "PromotionId", Advertisement: "AdvertisementId" };
        if (contextual[type]) {
            const field = qs(`input[name='${contextual[type]}']`, form);
            if (!field || Number(field.value) <= 0) return invalid("افتح العنصر نفسه واضغط «إبلاغ» حتى يتم ربط القضية به.", qs("[data-reference-panel='Context']", form));
        }

        const category = qs("select[name='Category']", form);
        if (type !== "Other" && category?.value === "Other") {
            const other = qs("input[name='OtherCategoryDescription']", form);
            const value = other?.value.trim() || "";
            if (value.length < 3) return invalid("اكتب التصنيف عند اختيار «أخرى».", other);
            if (value.length > 50) return invalid("التصنيف يجب ألا يتجاوز 50 حرفًا.", other);
        }

        const title = qs("input[name='Title']", form);
        const titleValue = title?.value.trim() || "";
        if (titleValue.length < 5) return invalid("اكتب عنوانًا واضحًا لا يقل عن 5 أحرف.", title);
        if (titleValue.length > 200) return invalid("العنوان يجب ألا يتجاوز 200 حرف.", title);

        const reason = qs("textarea[name='ReportReason']", form);
        const reasonValue = reason?.value.trim() || "";
        if (reasonValue.length < 15) return invalid("اكتب تفاصيل المشكلة في 15 حرفًا على الأقل.", reason);
        if (reasonValue.length > 4000) return invalid("تفاصيل المشكلة يجب ألا تتجاوز 4000 حرف.", reason);

        const file = qs("input[name='evidenceFile']", form)?.files?.[0];
        const fileValidation = validateFile(file);
        if (!fileValidation.valid) return invalid(fileValidation.message, qs("input[name='evidenceFile']", form));
        return { valid: true };
    }

    /* =====================================================================
       Details page
       ===================================================================== */

    function setupDetailsPage(page) {
        setupMiniUploads(page);
        setupEvidenceFallback(page);

        qsa("[data-report-operation-form]", page).forEach(form => {
            form.addEventListener("submit", async event => {
                event.preventDefault();
                if (state.requests.has(form) || form.dataset.confirming === "true") return;
                const operation = form.getAttribute("data-report-operation-form") || "";
                const validation = validateDetailsForm(form, operation);
                if (!validation.valid) {
                    showToast(validation.message, "error");
                    focusField(validation.element);
                    return;
                }

                const config = operationConfirmation(operation);
                form.dataset.confirming = "true";
                let confirmed;
                try { confirmed = await confirmDialog(config); }
                finally { delete form.dataset.confirming; }
                if (!confirmed) return;
                await submitAjaxForm(form);
            });
        });
    }

    function setupMiniUploads(root) {
        qsa(".cr-mini-upload input[type='file']", root).forEach(input => {
            const label = input.closest(".cr-mini-upload");
            const span = qs("span", label);
            const original = span?.textContent || "إضافة مرفق";
            input.addEventListener("change", () => {
                const file = input.files?.[0];
                const validation = input.dataset.imageOnly === "true" && file && !/\.(jpe?g|png|webp)$/i.test(file.name)
                    ? { valid: false, message: "المطلوب صورة JPG أو PNG أو WEBP، وليس PDF." }
                    : validateFile(file);
                if (!validation.valid) {
                    input.value = "";
                    label?.classList.remove("has-file");
                    if (span) span.textContent = original;
                    showToast(validation.message, "error");
                    return;
                }
                if (file) {
                    label?.classList.add("has-file");
                    if (span) span.textContent = `${file.name} · ${formatBytes(file.size)}`;
                } else {
                    label?.classList.remove("has-file");
                    if (span) span.textContent = original;
                }
            });
        });
    }

    function setupEvidenceFallback(root) {
        qsa("[data-report-evidence-image]", root).forEach(image => {
            const link = image.closest("[data-report-evidence-link]");
            const fallback = link?.parentElement ? qs("[data-report-evidence-fallback]", link.parentElement) : null;
            const fail = () => {
                if (link) link.hidden = true;
                if (fallback) fallback.hidden = false;
            };
            image.addEventListener("error", fail);
            if (image.complete && image.naturalWidth === 0) fail();
        });
    }

    function validateDetailsForm(form, operation) {
        const fileInput = qs("input[name='evidenceFile']", form);
        const fileValidation = validateFile(fileInput?.files?.[0]);
        if (!fileValidation.valid) return invalid(fileValidation.message, fileInput);

        if (operation === "clarification") {
            const field = qs("textarea[name='Message']", form);
            const value = field?.value.trim() || "";
            const type = form.dataset.responseContentType;
            const file = fileInput?.files?.[0];
            if ((type === "Text" || type === "TextAndImage") && !value) return invalid("اكتب التوضيح الذي طلبته الإدارة.", field);
            if ((type === "Image" || type === "TextAndImage") && !file) return invalid("أرفق صورة الإثبات المطلوبة.", fileInput);
            if ((type === "Image" || type === "TextAndImage") && file && !/\.(jpe?g|png|webp)$/i.test(file.name)) return invalid("المطلوب صورة، وليس PDF.", fileInput);
            if (type === "Text" && file) return invalid("طلبت الإدارة نصًا فقط دون مرفق.", fileInput);
            if (type === "Image" && value) return invalid("طلبت الإدارة صورة فقط دون نص.", field);
            if (!value && !file) return invalid("اكتب توضيحًا أو أرفق دليلًا جديدًا.", field || fileInput);
            if (value.length > 4000) return invalid("التوضيح يجب ألا يتجاوز 4000 حرف.", field);
        }

        if (operation === "resolution-not-confirmed") {
            const field = qs("textarea[name='Note']", form);
            const value = field?.value.trim() || "";
            if (!value) return invalid("اكتب ما الذي ما زال غير محلول قبل الإرسال.", field);
            if (value.length > 4000) return invalid("التوضيح يجب ألا يتجاوز 4000 حرف.", field);
        }

        if (operation === "resolution-confirmed") {
            const field = qs("textarea[name='Note']", form);
            if ((field?.value || "").length > 4000) return invalid("الملاحظة يجب ألا تتجاوز 4000 حرف.", field);
        }

        if (operation === "reopen") {
            const field = qs("textarea[name='Reason']", form);
            const value = field?.value.trim() || "";
            if (value.length < 10) return invalid("سبب إعادة المراجعة يجب أن يكون 10 أحرف على الأقل.", field);
            if (value.length > 4000) return invalid("سبب إعادة المراجعة يجب ألا يتجاوز 4000 حرف.", field);
        }

        if (operation === "withdraw") {
            const field = qs("textarea[name='Reason']", form);
            const value = field?.value.trim() || "";
            if (!value) return invalid("اكتب سبب سحب القضية.", field);
            if (value.length > 500) return invalid("سبب السحب يجب ألا يتجاوز 500 حرف.", field);
        }

        return { valid: true };
    }

    function operationConfirmation(operation) {
        if (operation === "resolution-confirmed") return { title: "تأكيد أن المشكلة حُلّت؟", message: "سيصل تأكيدك للإدارة لتقوم بمراجعة النتيجة والإغلاق الرسمي.", confirmText: "نعم، تم الحل", tone: "primary" };
        if (operation === "resolution-not-confirmed") return { title: "المشكلة ما زالت مستمرة؟", message: "سيعود البلاغ إلى الإدارة للمتابعة مع السبب والدليل الذي أرسلته.", confirmText: "إرسال للإدارة", tone: "primary" };
        if (operation === "reopen") return { title: "طلب إعادة مراجعة القضية؟", message: "ستعود نفس القضية ونفس الرقم إلى المراجعة، ولن يتم إنشاء بلاغ جديد.", confirmText: "طلب إعادة المراجعة", tone: "primary" };
        if (operation === "withdraw") return { title: "سحب القضية؟", message: "سيتم إنهاء متابعة القضية بعد تسجيل سبب السحب.", confirmText: "تأكيد السحب", tone: "danger" };
        return { title: "إرسال التوضيح للإدارة؟", message: "سيتم حفظ التوضيح أو الدليل داخل سجل نفس القضية.", confirmText: "إرسال التوضيح", tone: "primary" };
    }

    /* =====================================================================
       Validation helpers
       ===================================================================== */

    function validateFile(file) {
        if (!file) return { valid: true, message: "" };
        if (file.size === 0) return { valid: false, message: "الملف فارغ. اختر ملفًا آخر." };
        if (file.size > MAX_FILE_SIZE) return { valid: false, message: "حجم المرفق يجب ألا يتجاوز 5 MB." };

        const extension = (file.name.split(".").pop() || "").toLowerCase();
        if (!ALLOWED_EXTENSIONS.includes(extension)) return { valid: false, message: "الصيغة غير مدعومة. استخدم JPG أو PNG أو WEBP أو PDF." };
        if (file.type && !ALLOWED_TYPES.includes(file.type)) return { valid: false, message: "نوع الملف غير مدعوم." };
        return { valid: true, message: "" };
    }

    function formatBytes(bytes) {
        if (!Number.isFinite(bytes) || bytes <= 0) return "0 KB";
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(bytes < 10240 ? 1 : 0)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
    }

    function invalid(message, element) {
        return { valid: false, message, element: element instanceof HTMLElement ? element : null };
    }

    function focusField(element) {
        if (!(element instanceof HTMLElement)) return;
        element.scrollIntoView({ behavior: "smooth", block: "center" });
        window.setTimeout(() => {
            if (typeof element.focus === "function") element.focus({ preventScroll: true });
            element.classList.add("cr-invalid-pulse");
            window.setTimeout(() => element.classList.remove("cr-invalid-pulse"), 900);
        }, 300);
    }
})();
