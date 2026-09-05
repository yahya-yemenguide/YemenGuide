/* ============================================================================
   Owner Reviews & Feedback
   ملف مستقل لتبويبة تقييمات المالك والهيكل الموحد للتبويبات.
   لا يحتوي على أي منطق خاص بإرسال رسائل الدردشة.
   ============================================================================ */

(() => {
    "use strict";

    const root = document.querySelector("[data-owner-communications]");

    if (!root) {
        return;
    }

    const reviewsHost = root.querySelector("[data-owner-reviews-host]");
    const reviewCountBadge = root.querySelector("[data-owner-reviews-tab-count]");
    const tabButtons = Array.from(root.querySelectorAll("[data-owner-comm-tab]"));
    const tabPanes = Array.from(root.querySelectorAll("[data-owner-comm-pane]"));

    const FILTER_DEFAULTS = Object.freeze({
        Search: "",
        Rating: "",
        Sentiment: "all",
        ReplyState: "all",
        SortBy: "newest",
        DateFrom: "",
        DateTo: "",
        Page: "1",
        PageSize: "10"
    });

    const state = {
        activeTab: "messages",
        reviewsLoaded: false,
        reviewsLoading: false,
        filtersExpanded: false,
        filters: { ...FILTER_DEFAULTS },
        requestController: null,
        loadPromise: null,
        composer: {
            mode: "reply",
            reviewId: 0,
            userName: ""
        }
    };

    function initialize() {
        bindRootEvents();
        initializeTabs();

        // التحميل المبكر يحدث عدد التقييمات وفق المنشأة المحددة في الهيدر،
        // بينما تبقى الدردشة ظاهرة دون انتظار.
        void loadReviews({
            silent: state.activeTab !== "reviews",
            preserveScroll: false
        }).then(() => focusReviewFromNotification());
    }

    function readReviewNotificationDeepLink() {
        try {
            const url = new URL(window.location.href);
            return {
                reviewId: toPositiveInteger(url.searchParams.get("reviewId")),
                replyId: toPositiveInteger(url.searchParams.get("replyId"))
            };
        } catch {
            return { reviewId: 0, replyId: 0 };
        }
    }

    function focusReviewFromNotification() {
        const link = readReviewNotificationDeepLink();
        const targetId = link.replyId || link.reviewId;

        if (!targetId || state.activeTab !== "reviews") {
            return;
        }

        const target = document.getElementById(`owner-review-${targetId}`) ||
            getReviewsPanel()?.querySelector(`[data-review-id="${targetId}"]`);

        if (!target) {
            console.warn("Review deep link target is not visible on the current review page:", targetId);
            return;
        }

        // الردود تكون داخل حاويات مطوية افتراضيًا؛ افتح كل الآباء قبل التمرير.
        let parent = target.parentElement;
        while (parent && parent !== reviewsHost) {
            if (parent.matches?.(".orv-replies[hidden], [data-orv-replies][hidden]")) {
                parent.hidden = false;
                const ownerId = parent.id?.replace("owner-review-replies-", "");
                if (ownerId) {
                    const toggle = getReviewsPanel()?.querySelector(`[data-orv-toggle-replies][data-review-id="${ownerId}"]`);
                    toggle?.classList.add("is-open");
                    toggle?.setAttribute("aria-expanded", "true");
                }
            }
            parent = parent.parentElement;
        }

        target.classList.add("is-notification-focus");
        target.scrollIntoView({
            behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth",
            block: "center"
        });

        window.setTimeout(() => target.classList.remove("is-notification-focus"), 4200);

        try {
            const url = new URL(window.location.href);
            url.searchParams.delete("reviewId");
            url.searchParams.delete("replyId");
            window.history.replaceState(window.history.state, "", `${url.pathname}${url.search}${url.hash}`);
        } catch {
            // لا شيء.
        }
    }

    function bindRootEvents() {
        root.addEventListener("click", handleRootClick);
        root.addEventListener("submit", handleRootSubmit);
        root.addEventListener("change", handleRootChange);
        root.addEventListener("input", handleRootInput);
        root.addEventListener("keydown", handleRootKeydown);

        document.addEventListener("keydown", event => {
            if (event.key !== "Escape") {
                return;
            }

            if (isEmojiPanelOpen()) {
                closeEmojiPanel();
                return;
            }

            if (state.composer.reviewId > 0) {
                resetComposer();
            }
        });

        document.addEventListener("click", event => {
            if (!event.target.closest("[data-orv-toggle-emoji], [data-orv-emoji-panel]")) {
                closeEmojiPanel();
            }
        });

        // إن كان كود اختيار المنشأة يطلق حدثًا مخصصًا فلن نحتاج إعادة الصفحة.
        document.addEventListener("owner:active-place-changed", () => {
            state.reviewsLoaded = false;
            state.filters = { ...FILTER_DEFAULTS };
            void loadReviews({ silent: false, preserveScroll: false });
        });
    }

    function initializeTabs() {
        const url = new URL(window.location.href);
        const requestedTab = url.searchParams.get("communicationTab");
        const savedTab = safeStorageGet("ownerCommunicationTab");

        const initialTab = isValidTab(requestedTab)
            ? requestedTab
            : isValidTab(savedTab)
                ? savedTab
                : "messages";

        activateTab(initialTab, {
            updateAddress: false,
            save: false
        });
    }

    function isValidTab(value) {
        return value === "messages" || value === "reviews";
    }

    function activateTab(tabName, options = {}) {
        if (!isValidTab(tabName)) {
            tabName = "messages";
        }

        state.activeTab = tabName;

        tabButtons.forEach(button => {
            const isActive = button.dataset.ownerCommTab === tabName;
            button.classList.toggle("is-active", isActive);
            button.setAttribute("aria-selected", isActive ? "true" : "false");
            button.tabIndex = isActive ? 0 : -1;

            if (isActive && window.innerWidth <= 767) {
                window.setTimeout(() => {
                    button.scrollIntoView({
                        behavior: "smooth",
                        block: "nearest",
                        inline: "center"
                    });
                }, 30);
            }
        });

        tabPanes.forEach(pane => {
            const isActive = pane.dataset.ownerCommPane === tabName;
            pane.hidden = !isActive;
            pane.classList.toggle("is-active", isActive);
        });

        if (options.save !== false) {
            safeStorageSet("ownerCommunicationTab", tabName);
        }

        if (options.updateAddress !== false) {
            updateTabInAddress(tabName);
        }

        if (tabName === "reviews" && !state.reviewsLoaded) {
            void loadReviews({ silent: false, preserveScroll: false });
        }
    }

    function updateTabInAddress(tabName) {
        try {
            const url = new URL(window.location.href);

            if (tabName === "messages") {
                url.searchParams.delete("communicationTab");
            } else {
                url.searchParams.set("communicationTab", tabName);
            }

            window.history.replaceState({}, "", url.toString());
        } catch (error) {
            console.warn("تعذر تحديث رابط التبويب.", error);
        }
    }

    async function loadReviews(options = {}) {
        if (!reviewsHost) {
            return;
        }

        if (state.reviewsLoading && state.loadPromise) {
            return state.loadPromise;
        }

        const panelUrl = root.dataset.reviewsPanelUrl;

        if (!panelUrl) {
            renderLoadError("رابط تحميل التقييمات غير موجود.");
            return;
        }

        const previousScroll = options.preserveScroll
            ? getReviewScrollPosition()
            : 0;

        state.requestController?.abort();
        state.requestController = new AbortController();
        state.reviewsLoading = true;

        if (!options.silent) {
            reviewsHost.classList.add("is-loading");
        }

        state.loadPromise = (async () => {
            try {
                const url = buildReviewsUrl(panelUrl);
                const response = await fetch(url, {
                    method: "GET",
                    credentials: "same-origin",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest",
                        "Accept": "text/html"
                    },
                    signal: state.requestController.signal
                });

                if (response.status === 401 || response.status === 403) {
                    throw new Error("انتهت الجلسة أو لا تملك صلاحية عرض التقييمات.");
                }

                if (!response.ok) {
                    throw new Error("تعذر تحميل التقييمات الآن.");
                }

                const html = await response.text();
                reviewsHost.innerHTML = html;

                state.reviewsLoaded = true;
                resetComposerState();
                syncStateFromReturnedPanel();
                syncReviewCountBadge();
                syncFilterVisibility();
                initializeEmojiPicker();

                if (previousScroll > 0) {
                    window.requestAnimationFrame(() => {
                        restoreReviewScrollPosition(previousScroll);
                    });
                }
            } catch (error) {
                if (error?.name === "AbortError") {
                    return;
                }

                console.error(error);
                state.reviewsLoaded = false;
                renderLoadError(error?.message || "تعذر تحميل التقييمات الآن.");
            } finally {
                state.reviewsLoading = false;
                state.loadPromise = null;
                reviewsHost.classList.remove("is-loading");
            }
        })();

        return state.loadPromise;
    }

    function buildReviewsUrl(panelUrl) {
        const url = new URL(panelUrl, window.location.origin);

        Object.entries(state.filters).forEach(([key, value]) => {
            if (value !== null && value !== undefined && String(value).trim() !== "") {
                url.searchParams.set(key, String(value));
            }
        });

        return url.toString();
    }

    function syncStateFromReturnedPanel() {
        const form = getFilterForm();

        if (!form) {
            return;
        }

        state.filters = readFiltersFromForm(form);
    }

    function syncReviewCountBadge() {
        const panel = getReviewsPanel();
        const total = Number.parseInt(panel?.dataset.totalReviews || "0", 10) || 0;

        if (reviewCountBadge) {
            reviewCountBadge.textContent = String(total);
            reviewCountBadge.classList.toggle("has-items", total > 0);
        }
    }

    function renderLoadError(message) {
        if (!reviewsHost) {
            return;
        }

        reviewsHost.innerHTML = `
            <div class="orv-load-error" role="alert">
                <span class="orv-load-error__icon">
                    <i class="bi bi-wifi-off"></i>
                </span>
                <div>
                    <strong>تعذر تجهيز التقييمات</strong>
                    <p>${escapeHtml(message)}</p>
                </div>
                <button type="button" class="orv-button orv-button--primary" data-orv-retry>
                    <i class="bi bi-arrow-clockwise"></i>
                    إعادة المحاولة
                </button>
            </div>`;
    }

    async function handleRootClick(event) {
        const tabButton = event.target.closest("[data-owner-comm-tab]");

        if (tabButton) {
            activateTab(tabButton.dataset.ownerCommTab);
            return;
        }

        const retryButton = event.target.closest("[data-orv-retry]");
        if (retryButton) {
            await loadReviews({ silent: false, preserveScroll: false });
            return;
        }

        const filterToggle = event.target.closest("[data-orv-filter-toggle]");
        if (filterToggle) {
            state.filtersExpanded = !state.filtersExpanded;
            syncFilterVisibility();
            return;
        }

        const resetButton = event.target.closest("[data-orv-reset-filters]");
        if (resetButton) {
            state.filters = { ...FILTER_DEFAULTS };
            await loadReviews({ silent: false, preserveScroll: false });
            return;
        }

        const sentimentButton = event.target.closest("[data-orv-sentiment]");
        if (sentimentButton) {
            state.filters.Sentiment = sentimentButton.dataset.orvSentiment || "all";

            // فلاتر الانطباع والنجوم بدائل سريعة لبعضها، مثل واجهة العميل.
            state.filters.Rating = "";

            state.filters.Page = "1";
            await loadReviews({ silent: false, preserveScroll: false });
            return;
        }

        const ratingButton = event.target.closest("[data-orv-rating]");
        if (ratingButton) {
            state.filters.Rating = ratingButton.dataset.orvRating || "";
            state.filters.Sentiment = "all";
            state.filters.Page = "1";
            await loadReviews({ silent: false, preserveScroll: false });
            return;
        }

        const replyStateShortcut = event.target.closest("[data-orv-reply-state]");
        if (replyStateShortcut) {
            state.filters.ReplyState = replyStateShortcut.dataset.orvReplyState || "all";
            state.filters.Page = "1";
            await loadReviews({ silent: false, preserveScroll: false });
            return;
        }

        const pageButton = event.target.closest("[data-orv-page]");
        if (pageButton && !pageButton.disabled) {
            const requestedPage = Number.parseInt(pageButton.dataset.orvPage || "1", 10) || 1;
            state.filters.Page = String(Math.max(1, requestedPage));
            await loadReviews({ silent: false, preserveScroll: false });
            scrollReviewsIntoView();
            return;
        }

        const repliesToggle = event.target.closest("[data-orv-toggle-replies]");
        if (repliesToggle) {
            toggleReplies(repliesToggle);
            return;
        }

        const likeButton = event.target.closest("[data-orv-like]");
        if (likeButton) {
            await toggleLike(likeButton);
            return;
        }

        const replyButton = event.target.closest("[data-orv-reply]");
        if (replyButton) {
            activateComposer({
                mode: "reply",
                reviewId: toPositiveInteger(replyButton.dataset.reviewId),
                userName: replyButton.dataset.userName || "العميل",
                text: ""
            });
            return;
        }

        const editButton = event.target.closest("[data-orv-edit]");
        if (editButton) {
            activateComposer({
                mode: "edit",
                reviewId: toPositiveInteger(editButton.dataset.reviewId),
                userName: editButton.dataset.userName || "العميل",
                text: editButton.dataset.reviewText || ""
            });
            return;
        }

        const deleteButton = event.target.closest("[data-orv-delete]");
        if (deleteButton) {
            await deleteReviewTree(deleteButton);
            return;
        }

        const cancelComposerButton = event.target.closest("[data-orv-cancel-composer]");
        if (cancelComposerButton) {
            resetComposer();
            return;
        }

        const emojiToggle = event.target.closest("[data-orv-toggle-emoji]");
        if (emojiToggle) {
            toggleEmojiPanel(emojiToggle);
            return;
        }

        const submitComposerButton = event.target.closest("[data-orv-submit-composer]");
        if (submitComposerButton) {
            await submitComposer(submitComposerButton);
        }
    }

    function handleRootSubmit(event) {
        const form = event.target.closest("[data-orv-filter-form]");

        if (!form) {
            return;
        }

        event.preventDefault();
        state.filters = readFiltersFromForm(form);
        state.filters.Page = "1";
        void loadReviews({ silent: false, preserveScroll: false });
    }

    function handleRootChange(event) {
        if (!event.target.matches("[data-orv-auto-submit]")) {
            return;
        }

        const form = event.target.closest("[data-orv-filter-form]");
        if (!form) {
            return;
        }

        state.filters = readFiltersFromForm(form);
        state.filters.Page = "1";
        void loadReviews({ silent: false, preserveScroll: false });
    }

    function handleRootInput(event) {
        if (!event.target.matches("[data-orv-composer-text]")) {
            return;
        }

        updateCharacterCount(event.target);
        resizeComposerTextarea(event.target);
    }

    function handleRootKeydown(event) {
        if (!event.target.matches("[data-orv-composer-text]")) {
            return;
        }

        if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            const submitButton = getComposer()?.querySelector("[data-orv-submit-composer]");
            submitButton?.click();
        }
    }

    function readFiltersFromForm(form) {
        const data = new FormData(form);

        return {
            Search: cleanValue(data.get("Search")),
            Rating: cleanValue(data.get("Rating")),
            Sentiment: cleanValue(data.get("Sentiment")) || "all",
            ReplyState: cleanValue(data.get("ReplyState")) || "all",
            SortBy: cleanValue(data.get("SortBy")) || "newest",
            DateFrom: cleanValue(data.get("DateFrom")),
            DateTo: cleanValue(data.get("DateTo")),
            Page: cleanValue(data.get("Page")) || "1",
            PageSize: cleanValue(data.get("PageSize")) || "10"
        };
    }

    function syncFilterVisibility() {
        const form = getFilterForm();
        const button = getReviewsPanel()?.querySelector("[data-orv-filter-toggle]");

        if (form) {
            form.hidden = !state.filtersExpanded;
        }

        if (button) {
            button.classList.toggle("is-open", state.filtersExpanded);
            button.setAttribute("aria-expanded", state.filtersExpanded ? "true" : "false");

            const label = button.querySelector("span");
            const icon = button.querySelector("i");

            if (label) {
                label.textContent = state.filtersExpanded ? "إخفاء الفلاتر" : "إظهار الفلاتر";
            }

            if (icon) {
                icon.className = state.filtersExpanded
                    ? "bi bi-chevron-up"
                    : "bi bi-chevron-down";
            }
        }
    }

    function toggleReplies(button) {
        const reviewId = button.dataset.reviewId;
        const count = Number.parseInt(button.dataset.repliesCount || "0", 10) || 0;
        const container = document.getElementById(`owner-review-replies-${reviewId}`);

        if (!container) {
            return;
        }

        const willOpen = container.hidden;
        container.hidden = !willOpen;
        button.classList.toggle("is-open", willOpen);
        button.setAttribute("aria-expanded", willOpen ? "true" : "false");

        const icon = button.querySelector("i");
        const label = button.querySelector("[data-orv-replies-label]");

        if (icon) {
            icon.className = willOpen ? "bi bi-chevron-up" : "bi bi-chevron-down";
        }

        if (label) {
            label.textContent = willOpen ? "إخفاء الردود" : `عرض ${count} من الردود`;
        }
    }

    async function toggleLike(button) {
        const panel = getReviewsPanel();
        const reviewId = toPositiveInteger(button.dataset.reviewId);

        if (!panel || reviewId <= 0 || button.disabled) {
            return;
        }

        button.disabled = true;

        try {
            const result = await postForm(panel.dataset.likeUrl, {
                reviewId: String(reviewId)
            });

            if (!result.success) {
                throw new Error(result.message || "تعذر تحديث الإعجاب.");
            }

            const icon = button.querySelector("i");
            const count = button.querySelector("[data-orv-like-count]");

            button.classList.toggle("is-liked", Boolean(result.isLiked));
            button.setAttribute("aria-pressed", result.isLiked ? "true" : "false");

            if (icon) {
                icon.className = result.isLiked ? "bi bi-heart-fill" : "bi bi-heart";
            }

            if (count) {
                count.textContent = String(Number(result.likesCount) || 0);
            }
        } catch (error) {
            await notify("error", "تعذر تحديث الإعجاب", error.message);
        } finally {
            button.disabled = false;
        }
    }

    function activateComposer(options) {
        const panel = getReviewsPanel();
        const composer = getComposer();
        const textarea = composer?.querySelector("[data-orv-composer-text]");

        if (!panel || !composer || !textarea || options.reviewId <= 0) {
            return;
        }

        clearComposerTargetHighlight();

        state.composer.mode = options.mode === "edit" ? "edit" : "reply";
        state.composer.reviewId = options.reviewId;
        state.composer.userName = options.userName || "العميل";

        const context = composer.querySelector("[data-orv-composer-context]");
        const eyebrow = composer.querySelector("[data-orv-context-eyebrow]");
        const targetName = composer.querySelector("[data-orv-target-name]");
        const contextIcon = composer.querySelector("[data-orv-context-icon]");
        const submitLabel = composer.querySelector("[data-orv-submit-label]");

        if (context) {
            context.hidden = false;
            context.classList.toggle("is-edit-mode", state.composer.mode === "edit");
        }

        if (state.composer.mode === "edit") {
            if (eyebrow) eyebrow.textContent = "تعديل رد المنشأة";
            if (targetName) targetName.textContent = "سيتم تحديث الرد الحالي";
            if (contextIcon) contextIcon.className = "bi bi-pencil-square";
            if (submitLabel) submitLabel.textContent = "حفظ";
            textarea.placeholder = "عدّل رد المنشأة ثم اضغط حفظ...";
            textarea.value = options.text || "";
        } else {
            if (eyebrow) eyebrow.textContent = "الرد على";
            if (targetName) targetName.textContent = state.composer.userName;
            if (contextIcon) contextIcon.className = "bi bi-reply-fill";
            if (submitLabel) submitLabel.textContent = "نشر";
            textarea.placeholder = `اكتب رد المنشأة على ${state.composer.userName}...`;
            textarea.value = "";
        }

        const targetNode = panel.querySelector(
            `.orv-node[data-review-id="${state.composer.reviewId}"]`
        );
        targetNode?.classList.add("is-composer-target");

        updateCharacterCount(textarea);
        resizeComposerTextarea(textarea);
        closeEmojiPanel();

        window.setTimeout(() => {
            textarea.focus();
            textarea.setSelectionRange(textarea.value.length, textarea.value.length);
        }, 40);
    }

    function resetComposer() {
        const composer = getComposer();
        const textarea = composer?.querySelector("[data-orv-composer-text]");
        const context = composer?.querySelector("[data-orv-composer-context]");
        const submitLabel = composer?.querySelector("[data-orv-submit-label]");

        clearComposerTargetHighlight();
        resetComposerState();

        if (context) {
            context.hidden = true;
            context.classList.remove("is-edit-mode");
        }

        if (textarea) {
            textarea.value = "";
            textarea.placeholder = "اضغط «رد» أسفل أي تعليق، ثم اكتب رد المنشأة هنا...";
            updateCharacterCount(textarea);
            resizeComposerTextarea(textarea);
        }

        if (submitLabel) {
            submitLabel.textContent = "نشر";
        }

        closeEmojiPanel();
    }

    function resetComposerState() {
        state.composer.mode = "reply";
        state.composer.reviewId = 0;
        state.composer.userName = "";
    }

    function clearComposerTargetHighlight() {
        getReviewsPanel()
            ?.querySelectorAll(".orv-node.is-composer-target")
            .forEach(node => node.classList.remove("is-composer-target"));
    }

    function initializeEmojiPicker() {
        const picker = getComposer()?.querySelector("[data-orv-emoji-picker]");

        if (!picker || picker.dataset.orvBound === "true") {
            return;
        }

        picker.dataset.orvBound = "true";
        picker.addEventListener("emoji-click", event => {
            const emoji = event.detail?.unicode || event.detail?.emoji?.unicode || "";
            insertEmoji(emoji);
        });
    }

    function toggleEmojiPanel(button) {
        const panel = getEmojiPanel();

        if (!panel) {
            return;
        }

        const willOpen = panel.hidden;
        panel.hidden = !willOpen;
        button.setAttribute("aria-expanded", willOpen ? "true" : "false");
        button.classList.toggle("is-active", willOpen);
    }

    function closeEmojiPanel() {
        const panel = getEmojiPanel();
        const button = getComposer()?.querySelector("[data-orv-toggle-emoji]");

        if (panel) {
            panel.hidden = true;
        }

        if (button) {
            button.setAttribute("aria-expanded", "false");
            button.classList.remove("is-active");
        }
    }

    function isEmojiPanelOpen() {
        const panel = getEmojiPanel();
        return Boolean(panel && !panel.hidden);
    }

    function insertEmoji(emoji) {
        const textarea = getComposer()?.querySelector("[data-orv-composer-text]");

        if (!textarea || !emoji) {
            return;
        }

        const start = textarea.selectionStart ?? textarea.value.length;
        const end = textarea.selectionEnd ?? start;
        const nextValue = textarea.value.slice(0, start) + emoji + textarea.value.slice(end);

        if (nextValue.length > 2000) {
            return;
        }

        textarea.value = nextValue;
        textarea.focus();
        textarea.setSelectionRange(start + emoji.length, start + emoji.length);
        updateCharacterCount(textarea);
        resizeComposerTextarea(textarea);
    }

    function updateCharacterCount(textarea) {
        const counter = getComposer()?.querySelector("[data-orv-character-count]");

        if (counter) {
            counter.textContent = String(textarea.value.length);
            counter.parentElement?.classList.toggle("is-near-limit", textarea.value.length >= 1800);
        }
    }

    function resizeComposerTextarea(textarea) {
        textarea.style.height = "auto";
        textarea.style.height = `${Math.min(Math.max(textarea.scrollHeight, 46), 118)}px`;
        textarea.classList.toggle("is-scrollable", textarea.scrollHeight > 118);
    }

    async function submitComposer(button) {
        const panel = getReviewsPanel();
        const textarea = getComposer()?.querySelector("[data-orv-composer-text]");
        const text = textarea?.value.trim() || "";

        if (!panel || !textarea || button.disabled) {
            return;
        }

        if (state.composer.reviewId <= 0) {
            await notify(
                "info",
                "اختر التعليق أولًا",
                "اضغط زر «رد» أسفل التعليق المطلوب، ثم اكتب رد المنشأة."
            );
            return;
        }

        if (!text) {
            await notify("warning", "الرد فارغ", "اكتب ردك أولًا ثم حاول النشر.");
            textarea.focus();
            return;
        }

        if (text.length > 2000) {
            await notify("warning", "الرد طويل", "الحد الأقصى للرد هو 2000 حرف.");
            return;
        }

        button.disabled = true;
        button.classList.add("is-busy");

        try {
            const isEdit = state.composer.mode === "edit";
            const endpoint = isEdit
                ? panel.dataset.editReplyUrl
                : panel.dataset.addReplyUrl;

            const fields = isEdit
                ? {
                    reviewId: String(state.composer.reviewId),
                    newText: text
                }
                : {
                    parentReviewId: String(state.composer.reviewId),
                    replyText: text
                };

            const result = await postForm(endpoint, fields);

            if (!result.success) {
                throw new Error(result.message || "تعذر حفظ الرد.");
            }

            resetComposer();
            await loadReviews({ silent: false, preserveScroll: true });
            await notify("success", "تم بنجاح", result.message || "تم حفظ الرد بنجاح.", true);
        } catch (error) {
            await notify("error", "تعذر الحفظ", error.message);
        } finally {
            button.disabled = false;
            button.classList.remove("is-busy");
        }
    }

    async function deleteReviewTree(button) {
        const panel = getReviewsPanel();
        const reviewId = toPositiveInteger(button.dataset.reviewId);

        if (!panel || reviewId <= 0 || button.disabled) {
            return;
        }

        const isRootReview = button.dataset.rootReview === "true";
        const repliesCount = Number.parseInt(button.dataset.repliesCount || "0", 10) || 0;

        const confirmationTitle = isRootReview
            ? "حذف التقييم والمناقشة بالكامل؟"
            : "حذف الرد وما يتبعه؟";

        const confirmationText = isRootReview
            ? repliesCount > 0
                ? `سيُحذف تقييم العميل ونجومه وجميع الردود التابعة له وعددها ${repliesCount}، مع جميع الإعجابات. لا يمكن التراجع عن العملية.`
                : "سيُحذف تقييم العميل ونجومه وجميع إعجاباته نهائيًا. لا يمكن التراجع عن العملية."
            : repliesCount > 0
                ? `سيُحذف هذا الرد وجميع الردود التابعة له وعددها ${repliesCount}، مع جميع الإعجابات. لا يمكن التراجع عن العملية.`
                : "سيُحذف هذا الرد وجميع إعجاباته نهائيًا. لا يمكن التراجع عن العملية.";

        const confirmed = await confirmAction(
            confirmationTitle,
            confirmationText
        );

        if (!confirmed) {
            return;
        }

        button.disabled = true;

        try {
            const result = await postForm(panel.dataset.deleteReplyUrl, {
                reviewId: String(reviewId)
            });

            if (!result.success) {
                throw new Error(result.message || "تعذر حذف التعليق.");
            }

            if (state.composer.reviewId === reviewId) {
                resetComposer();
            }

            await loadReviews({ silent: false, preserveScroll: true });
            await notify("success", "تم الحذف", result.message || "تم حذف التعليق وما يتبعه بنجاح.", true);
        } catch (error) {
            await notify("error", "تعذر الحذف", error.message);
        } finally {
            button.disabled = false;
        }
    }

    async function postForm(url, fields) {
        if (!url) {
            throw new Error("رابط العملية غير موجود.");
        }

        const token = getAntiForgeryToken();
        const body = new FormData();

        Object.entries(fields).forEach(([key, value]) => {
            body.append(key, value);
        });

        if (token) {
            body.append("__RequestVerificationToken", token);
        }

        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json"
            },
            body
        });

        if (response.status === 401 || response.status === 403) {
            throw new Error("انتهت الجلسة أو لا تملك صلاحية تنفيذ العملية.");
        }

        let result;

        try {
            result = await response.json();
        } catch {
            throw new Error("استجابة الخادم غير صالحة.");
        }

        if (!response.ok && result?.success !== false) {
            throw new Error(result?.message || "تعذر تنفيذ العملية.");
        }

        return result;
    }

    function getAntiForgeryToken() {
        return getReviewsPanel()
            ?.querySelector('[data-orv-token-form] input[name="__RequestVerificationToken"]')
            ?.value || "";
    }

    async function confirmAction(title, text) {
        if (window.Swal?.fire) {
            const result = await window.Swal.fire({
                icon: "warning",
                title,
                text,
                showCancelButton: true,
                confirmButtonText: "نعم، احذف",
                cancelButtonText: "تراجع",
                confirmButtonColor: "#FF5A5F",
                cancelButtonColor: "#8A9AAA",
                reverseButtons: true,
                focusCancel: true
            });

            return result.isConfirmed;
        }

        return window.confirm(`${title}\n${text}`);
    }

    async function notify(icon, title, text, toast = false) {
        if (window.Swal?.fire) {
            return window.Swal.fire({
                icon,
                title,
                text,
                toast,
                position: toast ? "top-end" : "center",
                showConfirmButton: !toast,
                confirmButtonText: "حسنًا",
                confirmButtonColor: "#00A6DA",
                timer: toast ? 2400 : undefined,
                timerProgressBar: toast
            });
        }

        if (!toast) {
            window.alert(text || title);
        }
    }

    function getReviewsPanel() {
        return reviewsHost?.querySelector("[data-owner-reviews-panel]") || null;
    }

    function getFilterForm() {
        return getReviewsPanel()?.querySelector("[data-orv-filter-form]") || null;
    }

    function getComposer() {
        return getReviewsPanel()?.querySelector("[data-orv-composer]") || null;
    }

    function getEmojiPanel() {
        return getComposer()?.querySelector("[data-orv-emoji-panel]") || null;
    }

    function getReviewScrollPosition() {
        return getReviewsPanel()?.querySelector("[data-orv-scroll-area]")?.scrollTop || 0;
    }

    function restoreReviewScrollPosition(position) {
        const scrollArea = getReviewsPanel()?.querySelector("[data-orv-scroll-area]");

        if (scrollArea) {
            scrollArea.scrollTop = position;
        }
    }

    function scrollReviewsIntoView() {
        const scrollArea = getReviewsPanel()?.querySelector("[data-orv-scroll-area]");
        const stream = getReviewsPanel()?.querySelector("[data-orv-review-stream]");

        window.setTimeout(() => {
            if (scrollArea && stream) {
                scrollArea.scrollTo({
                    top: Math.max(0, stream.offsetTop - 18),
                    behavior: "smooth"
                });
            }
        }, 80);
    }

    function cleanValue(value) {
        return value === null || value === undefined ? "" : String(value).trim();
    }

    function toPositiveInteger(value) {
        const parsed = Number.parseInt(value || "0", 10);
        return Number.isInteger(parsed) && parsed > 0 ? parsed : 0;
    }

    function safeStorageGet(key) {
        try {
            return window.localStorage.getItem(key);
        } catch {
            return null;
        }
    }

    function safeStorageSet(key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch {
            // التخزين المحلي اختياري ولا يجب أن يوقف الصفحة.
        }
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
