/**
 * =============================================================================
 * دليلك اليمني — محرك دردشة العميل
 * File destination: wwwroot/js/customer/customer-messages.js
 *
 * هذا الملف خاص بالدردشة فقط. لا يحتوي على أي منطق للتقييمات أو التبويبات
 * العامة لصفحة التواصل؛ owner-reviews-feedback.js مسؤول عنها.
 * =============================================================================
 */
(() => {
    "use strict";

    const root = document.querySelector("[data-customer-messages]");
    const shell = document.querySelector("[data-customer-chat]");

    if (!root || !shell) {
        return;
    }

    /* =========================================================================
       1. المراجع والحالة
       ========================================================================= */

    const elements = {
        contactsPanel: document.getElementById("chatContactsPanel"),
        conversationPanel: document.getElementById("chatConversationPanel"),
        contactList: document.getElementById("chatContactList"),
        searchInput: document.getElementById("chatSearchInput"),
        clearSearch: document.getElementById("clearChatSearchBtn"),
        noResults: document.getElementById("chatNoFilterResults"),
        filterButtons: Array.from(document.querySelectorAll("[data-chat-filter]")),
        unreadConversationsBadge: document.getElementById("unreadConversationsBadge"),
        totalUnreadBadge: document.getElementById("totalUnreadBadge"),

        mobileBack: document.getElementById("mobileChatBackBtn"),
        activeAvatar: document.getElementById("activeChatAvatar"),
        activeName: document.getElementById("activeChatName"),
        activeStatus: document.getElementById("activeChatStatus"),
        activeLiveDot: document.getElementById("activeChatLiveDot"),
        headerInfo: document.getElementById("headerInfoBtn"),
        headerMenu: document.getElementById("headerMenuBtn"),
        openPlace: document.getElementById("actionOpenPlace"),
        clearChat: document.getElementById("actionClearChatForMe"),

        bookingBar: document.getElementById("activeBookingBar"),
        bookingSummary: document.getElementById("activeBookingSummary"),
        bookingDetails: document.getElementById("activeBookingDetails"),
        bookingNumber: document.getElementById("activeBookingNumber"),
        bookingStatus: document.getElementById("activeBookingStatus"),
        bookingDate: document.getElementById("activeBookingDate"),
        bookingDates: document.getElementById("activeBookingDates"),
        openBooking: document.getElementById("openBookingDetailsBtn"),

        messagesArea: document.getElementById("chatMessagesArea"),
        initialState: document.getElementById("chatInitialState"),
        loadOlder: document.getElementById("loadOlderMessagesBtn"),
        systemNotice: document.getElementById("chatSystemNotice"),
        systemNoticeText: document.getElementById("systemNoticeText"),

        quickRepliesBar: document.getElementById("quickRepliesBar"),
        quickRepliesTrack: document.getElementById("quickRepliesTrack"),

        replyBox: document.getElementById("replyBox"),
        replyToName: document.getElementById("replyToName"),
        replyToText: document.getElementById("replyToText"),
        cancelReply: document.getElementById("cancelReplyBtn"),

        editBox: document.getElementById("editBox"),
        editOriginalText: document.getElementById("editOriginalText"),
        editingMessageId: document.getElementById("editingMessageId"),
        cancelEdit: document.getElementById("cancelEditBtn"),

        attachmentsPreview: document.getElementById("attachmentsPreviewContainer"),
        imageInput: document.getElementById("imageUploadInput"),
        fileInput: document.getElementById("fileUploadInput"),
        emojiToggle: document.getElementById("emojiToggleBtn"),
        attachmentToggle: document.getElementById("attachmentToggleBtn"),
        emojiPicker: document.getElementById("fullEmojiPicker"),
        sheetOverlay: document.getElementById("chatMobileSheetOverlay"),

        input: document.getElementById("chatMsgInput"),
        send: document.getElementById("sendBtn"),
        sendIcon: document.getElementById("sendBtnIcon"),
        sendSpinner: document.querySelector("#sendBtn .chat-send-spinner"),
        characterCounter: document.getElementById("chatCharacterCounter"),
        composerHint: document.getElementById("chatComposerHint"),

        modalContainer: document.getElementById("modalZoomContainer"),
        modalName: document.getElementById("modalZoomName")
    };

    // يحفظ الموضع الأصلي لقائمة إجراءات الرسالة عند نقلها مؤقتًا
    // إلى حاوية المحادثة في الهاتف حتى لا تُقص داخل منطقة الرسائل.
    const mobileMessageMenuOrigins = new WeakMap();

    const limits = Object.freeze({
        messageLength: 4000,
        filesCount: 10,
        fileBytes: 10 * 1024 * 1024,
        totalBytes: 25 * 1024 * 1024,
        initialMessages: 40,
        olderStep: 40
    });

    const allowedExtensions = new Set([
        "jpg", "jpeg", "png", "gif", "webp",
        "mp4", "webm", "mov",
        "pdf", "doc", "docx", "xls", "xlsx",
        "zip", "rar"
    ]);

    const state = {
        activePlaceId: positiveInteger(root.dataset.openPlaceId || shell.dataset.activePlaceId),
        chatEnabled: true,
        activeCustomerId: 0,
        activeCustomerName: "مالك",
        activeCustomerCard: null,
        activeCustomerOnline: false,
        activePresenceText: "",
        activeCustomerCanStart: false,
        activePlaceName: "",
        hasConfirmedBooking: false,
        latestBooking: null,

        currentFilter: "all",
        searchText: "",
        selectedFiles: [],
        objectUrls: new Set(),
        reply: null,
        editing: null,
        isSending: false,
        failedSend: null,

        messages: [],
        renderStartIndex: 0,
        messagesRequest: null,
        openSequence: 0,

        quickReplies: [],
        emergencyContact: "",
        welcomeMessage: "",
        offlineReplyEnabled: false,
        offlineMessage: "",
        isWithinWorkingHours: true,

        connection: null,
        connectionStarted: false,
        receivedSignalMessageIds: new Set(),
        typingSent: false,
        typingStopTimer: null,
        remoteTypingTimer: null,
        noticeTimer: null,
        mobileHistoryPushed: false,
        requestedNotificationMessageId: 0,
        notificationMessageFocusConsumed: false
    };

    /* توافق مع أي كود قديم في الصفحة. */
    window.activeCustomerId = null;
    window.activeCustomerName = "مالك";
    window.activeOwnerId = null;
    window.activeOwnerName = "مالك";
    window.activePlaceId = state.activePlaceId || null;
    window.isActiveCustomerOnline = false;
    window.hasConfirmedBooking = false;
    window.selectedFilesArray = state.selectedFiles;
    window.isBlocked = false;

    initialize();

    /* =========================================================================
       2. التهيئة
       ========================================================================= */

    function initialize() {
        bindContacts();
        bindSearchAndFilters();
        bindComposer();
        bindHeaderAndBooking();
        bindMessageActions();
        bindMobileNavigation();
        bindAttachmentSheets();
        bindMobileViewportSizing();
        renderQuickReplies(state.quickReplies);
        updateCharacterCounter();
        updateSendButton();
        updateUnreadBadges();
        applyContactFilters();
        startSignalR();

        const query = new URLSearchParams(window.location.search);
        state.requestedNotificationMessageId =
            positiveInteger(query.get("messageId"));

        openRequestedConversation();

        window.addEventListener("beforeunload", saveActiveDraft);
        window.addEventListener("resize", handleResponsiveResize, { passive: true });

        document.addEventListener("customer:chat-place-changed", () => {
            saveActiveDraft();
            leaveActiveConversationGroup();
        });

        window.loadChat = openContact;
        window.openProfileModal = openProfileModal;
        window.cancelReply = clearReply;
        window.cancelEdit = clearEdit;
        window.removeFileFromQueue = removeSelectedFile;
    }

    function bindContacts() {
        getContactCards().forEach(card => {
            // المرحلة الثانية أبقت استدعاءات inline للتوافق المؤقت.
            // نحذفها هنا حتى لا تُفتح المحادثة مرتين عند الضغط.
            card.removeAttribute("onclick");
            card.removeAttribute("onkeydown");

            card.addEventListener("click", event => {
                if (event.target.closest(".chat-contact-avatar")) {
                    return;
                }

                openContact(
                    card,
                    positiveInteger(card.dataset.ownerId || card.dataset.customerId || card.dataset.id),
                    card.dataset.name,
                    card.dataset.profile,
                    card.dataset.initial
                );
            });

            card.addEventListener("keydown", event => {
                if (event.key !== "Enter" && event.key !== " ") {
                    return;
                }

                if (card.classList.contains("is-unavailable")) {
                    return;
                }

                event.preventDefault();
                card.click();
            });

            const avatar = card.querySelector(".chat-contact-avatar");
            avatar?.removeAttribute("onclick");
            avatar?.addEventListener("click", event => {
                event.stopPropagation();
                openProfileModal(
                    card.dataset.name,
                    card.dataset.profile,
                    card.dataset.initial
                );
            });
        });
    }

    function bindSearchAndFilters() {
        elements.searchInput?.addEventListener("input", () => {
            state.searchText = normalizeForSearch(elements.searchInput.value);
            elements.clearSearch.hidden = !state.searchText;
            applyContactFilters();
        });

        elements.clearSearch?.addEventListener("click", () => {
            if (!elements.searchInput) {
                return;
            }

            elements.searchInput.value = "";
            state.searchText = "";
            elements.clearSearch.hidden = true;
            applyContactFilters();
            elements.searchInput.focus();
        });

        elements.filterButtons.forEach(button => {
            button.addEventListener("click", () => {
                const requested = button.dataset.chatFilter || "all";
                state.currentFilter = ["all", "unread", "bookings", "conversations"]
                    .includes(requested)
                    ? requested
                    : "all";

                elements.filterButtons.forEach(item => {
                    const active = item === button;
                    item.classList.toggle("active", active);
                    item.setAttribute("aria-pressed", active ? "true" : "false");
                });

                applyContactFilters();
            });
        });
    }

    function bindComposer() {
        elements.input?.addEventListener("input", () => {
            autoResizeInput();
            updateCharacterCounter();
            updateSendButton();
            saveActiveDraft();
            notifyTypingFromInput();
        });

        elements.input?.addEventListener("keydown", event => {
            if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();

                if (!elements.send?.classList.contains("disabled")) {
                    void submitComposer();
                }
            }
        });

        elements.send?.addEventListener("click", () => {
            if (!elements.send.classList.contains("disabled")) {
                void submitComposer();
            }
        });

        elements.cancelReply?.addEventListener("click", clearReply);
        elements.cancelEdit?.addEventListener("click", clearEdit);

        elements.imageInput && bindFileInput(elements.imageInput);
        elements.fileInput && bindFileInput(elements.fileInput);

        elements.emojiPicker?.addEventListener("emoji-click", event => {
            if (!composerIsUsable()) {
                return;
            }

            const emoji = event?.detail?.unicode || "";
            insertAtCursor(elements.input, emoji);
            closeMobileSheets();
        });
    }

    function bindHeaderAndBooking() {
        elements.headerInfo?.addEventListener("click", () => {
            if (!state.activeCustomerId) {
                return;
            }

            if (state.latestBooking?.bookingId) {
                toggleBookingDetails();
                return;
            }

            const message = state.emergencyContact
                ? `لا يوجد حجز حديث لك في هذه المنشأة. وسيلة التواصل المتاحة: ${state.emergencyContact}`
                : "لا توجد بيانات حجز إضافية لك داخل المنشأة المحددة.";

            alertUser("معلومات المحادثة", message, "info");
        });

        elements.bookingSummary?.addEventListener("click", toggleBookingDetails);

        elements.openBooking?.addEventListener("click", () => {
            const bookingId = positiveInteger(
                elements.openBooking?.dataset.bookingId || state.latestBooking?.bookingId
            );

            if (!bookingId) {
                return;
            }

            const url = new URL(requiredUrl(root.dataset.bookingDetailsUrl), window.location.origin);
            url.searchParams.set("id", String(bookingId));
            window.location.href = url.toString();
        });

        elements.clearChat?.addEventListener("click", () => void clearCurrentChat());

        elements.openPlace?.addEventListener("click", event => {
            const isDisabled =
                elements.openPlace.classList.contains("disabled") ||
                elements.openPlace.getAttribute("aria-disabled") === "true" ||
                !positiveInteger(state.activePlaceId);

            if (isDisabled) {
                event.preventDefault();
            }
        });
    }

    function bindMessageActions() {
        // نستخدم مستمعًا واحدًا على document لأن قائمة الرسالة تُنقل مؤقتًا
        // إلى body حتى لا تُقص داخل منطقة الرسائل أو أعمدة الصفحة.
        document.addEventListener("click", event => {
            const actionButton = event.target.closest("[data-message-action]");
            if (actionButton &&
                (shell.contains(actionButton) || actionButton.closest(".message-actions-popover.is-message-menu-portal"))) {
                event.preventDefault();
                event.stopPropagation();
                void executeMessageAction(actionButton);
                return;
            }

            const menuButton = event.target.closest("[data-message-menu]");
            if (menuButton && shell.contains(menuButton)) {
                event.preventDefault();
                event.stopPropagation();
                toggleMessageMenu(menuButton);
                return;
            }

            const media = event.target.closest("[data-chat-media-url]");
            if (media && shell.contains(media)) {
                window.open(media.dataset.chatMediaUrl, "_blank", "noopener,noreferrer");
                return;
            }

            if (!event.target.closest(".message-actions-popover.is-message-menu-portal")) {
                closeAllMessageMenus();
            }
        });

        elements.loadOlder?.addEventListener("click", loadOlderMessages);

        document.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                closeAllMessageMenus();
                closeMobileSheets();
            }
        });

        window.addEventListener("resize", closeAllMessageMenus, { passive: true });
        window.addEventListener("orientationchange", closeAllMessageMenus, { passive: true });
        elements.messagesArea?.addEventListener("scroll", closeAllMessageMenus, { passive: true });
    }

    function bindMobileNavigation() {
        elements.mobileBack?.addEventListener("click", returnToContactsFromMobile);

        window.addEventListener("popstate", () => {
            if (isMobileLayout() && shell.dataset.mobileView === "conversation") {
                showMobileContacts(false);
            }
        });
    }

    function bindAttachmentSheets() {
        const toolButtons = [elements.emojiToggle, elements.attachmentToggle].filter(Boolean);

        toolButtons.forEach(button => {
            // نلغي Bootstrap Dropdown هنا لأن تموضع Popper كان يفتح اللوحة
            // خارج الدردشة أو يحولها إلى Bottom Sheet كبير في الهاتف.
            button.removeAttribute("data-bs-toggle");
            button.removeAttribute("data-bs-auto-close");

            button.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();

                if (!composerIsUsable()) {
                    return;
                }

                toggleToolPanel(button);
            });
        });

        document.querySelectorAll("[data-close-chat-sheet]").forEach(button => {
            button.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();
                closeMobileSheets();
            });
        });

        // الضغط داخل اللوحة لا يغلقها؛ الضغط خارجها يغلقها بهدوء.
        document.addEventListener("click", event => {
            if (!event.target.closest(".chat-tool-dropdown")) {
                closeMobileSheets();
            }
        });

        elements.sheetOverlay?.addEventListener("click", closeMobileSheets);
    }

    function toggleToolPanel(button) {
        const wrapper = button.closest(".chat-tool-dropdown");
        const panel = wrapper?.querySelector(".dropdown-menu");

        if (!panel) {
            return;
        }

        const shouldOpen = !panel.classList.contains("show");
        closeMobileSheets();

        if (!shouldOpen) {
            return;
        }

        panel.classList.add("show");
        panel.removeAttribute("hidden");
        button.setAttribute("aria-expanded", "true");

        // إزالة أي تموضع قديم تركه Bootstrap/Popper من تحميل سابق.
        ["position", "inset", "top", "right", "bottom", "left", "transform"].forEach(property => {
            panel.style.removeProperty(property);
        });
    }

    /* =========================================================================
       3. فتح المحادثة وتحديث الرأس
       ========================================================================= */

    async function openContact(cardOrEvent, ownerId, ownerName, profileImage, initialLetter) {
        const requestedPlaceId = positiveInteger(
            cardOrEvent instanceof Element
                ? cardOrEvent.closest(".chat-contact-card")?.dataset.placeId
                : state.activePlaceId
        );

        const card = cardOrEvent instanceof Element
            ? cardOrEvent.closest(".chat-contact-card") || cardOrEvent
            : findContactCard(ownerId, requestedPlaceId);

        const id = positiveInteger(
            ownerId ||
            card?.dataset.ownerId ||
            card?.dataset.customerId ||
            card?.dataset.id
        );

        const placeId = positiveInteger(card?.dataset.placeId || requestedPlaceId);

        if (!card || !id || !placeId || card.classList.contains("is-unavailable")) {
            return;
        }

        if (!toBoolean(card.dataset.canStart)) {
            alertUser(
                "تعذر فتح المحادثة",
                "هذه المنشأة أو حساب مالكها غير متاح للمراسلة حاليًا.",
                "warning"
            );
            return;
        }

        const sequence = ++state.openSequence;
        const conversationChanged =
            state.activeCustomerId !== id ||
            state.activePlaceId !== placeId;

        if (state.activeCustomerId && conversationChanged) {
            saveActiveDraft();
            clearReply();
            clearEdit();
            clearSelectedFiles();
            await leaveActiveConversationGroup();
        }

        state.activePlaceId = placeId;
        state.chatEnabled = toBoolean(card.dataset.chatEnabled);
        state.activeCustomerId = id;
        state.activeCustomerName = cleanText(ownerName || card.dataset.name) || "مالك";
        state.activeCustomerCard = card;
        state.activeCustomerOnline = toBoolean(card.dataset.isOnline);
        state.activePresenceText = state.activeCustomerOnline ? "متصل الآن" : "جارٍ تحديث الحالة...";
        state.activeCustomerCanStart = toBoolean(card.dataset.canStart);
        state.latestBooking = bookingFromCard(card);
        state.activePlaceName = cleanText(card.dataset.placeName) || "المنشأة";

        window.activeCustomerId = id;
        window.activeCustomerName = state.activeCustomerName;
        window.isActiveCustomerOnline = state.activeCustomerOnline;
        window.activeOwnerId = id;
        window.activeOwnerName = state.activeCustomerName;
        window.activePlaceId = placeId;

        shell.dataset.activePlaceId = String(placeId);
        shell.dataset.chatEnabled = String(state.chatEnabled);

        getContactCards().forEach(item => item.classList.toggle("active", item === card));
        updateActiveHeader(card, profileImage, initialLetter);
        updatePlaceActionLink(card);
        updateBookingBar(state.latestBooking);
        enableHeaderActions(true);
        setComposerEnabled(false, "جارٍ فتح المحادثة...");
        showMessagesLoading();
        markCardRead(card);
        moveCardToTop(card);
        restoreDraft(id);

        if (isMobileLayout()) {
            showMobileConversation(true);
        }

        try {
            const result = await fetchMessages(id);

            if (
                sequence !== state.openSequence ||
                id !== state.activeCustomerId ||
                placeId !== state.activePlaceId
            ) {
                return;
            }

            if (!result.success) {
                throw new Error(result.message || "تعذر تحميل المحادثة.");
            }

            state.activeCustomerOnline = Boolean(result.isOnline);
            state.activePresenceText = cleanText(result.statusText) ||
                (state.activeCustomerOnline ? "متصل الآن" : "غير متصل");
            state.hasConfirmedBooking = Boolean(result.hasBooking);
            state.chatEnabled = result.chatEnabled !== false;
            state.activePlaceId = positiveInteger(result.activePlaceId) || placeId;
            state.activePlaceName = cleanText(result.activePlaceName) || state.activePlaceName;
            state.latestBooking = normalizeBooking(result.latestBooking) || state.latestBooking;
            state.quickReplies = normalizeQuickReplies(
                Array.isArray(result.quickQuestions)
                    ? result.quickQuestions
                    : result.quickRepliesJson
            );
            state.emergencyContact = cleanText(result.emergencyContact) || "";
            state.welcomeMessage = cleanText(result.welcomeMessage) || "";
            state.offlineReplyEnabled = Boolean(result.offlineReplyEnabled);
            state.offlineMessage = cleanText(result.offlineMessage) || "";
            state.isWithinWorkingHours = result.isWithinWorkingHours !== false;
            state.messages = Array.isArray(result.data) ? result.data : [];
            state.renderStartIndex = Math.max(0, state.messages.length - limits.initialMessages);

            window.isActiveCustomerOnline = state.activeCustomerOnline;
            window.hasConfirmedBooking = state.hasConfirmedBooking;
            window.activePlaceId = state.activePlaceId;

            updatePresenceHeader();
            updateBookingBar(state.latestBooking);
            renderQuickReplies(state.quickReplies);
            renderMessages({ scrollToBottom: true });
            focusRequestedNotificationMessage(id, placeId);

            setComposerEnabled(
                state.chatEnabled && state.activeCustomerCanStart,
                state.chatEnabled
                    ? (!state.isWithinWorkingHours && state.offlineReplyEnabled
                        ? "خارج وقت العمل؛ يمكنك الإرسال الآن وسيظهر لك الرد الآلي ثم يراجع المالك رسالتك لاحقًا"
                        : "اضغط Enter للإرسال وShift + Enter لسطر جديد")
                    : "الدردشة متوقفة من إعدادات المنشأة"
            );

            await joinActiveConversationGroup();
        } catch (error) {
            if (error?.name === "AbortError") {
                return;
            }

            console.error("Customer chat load error:", error);
            showMessagesError(error?.message || "تعذر تحميل المحادثة الآن.");
            setComposerEnabled(false, "تعذر فتح المحادثة");
        }
    }

    function updatePlaceActionLink(card) {
        if (!elements.openPlace) {
            return;
        }

        const placeId = positiveInteger(
            card?.dataset.placeId || state.activePlaceId
        );

        let resolvedUrl = cleanText(
            card?.dataset.placeDetailsUrl
        );

        if (!resolvedUrl && placeId) {
            const baseUrl = cleanText(root.dataset.placeDetailsUrl);

            if (baseUrl) {
                const url = new URL(baseUrl, window.location.origin);
                url.searchParams.set("id", String(placeId));
                resolvedUrl = url.toString();
            }
        }

        const enabled = Boolean(placeId && resolvedUrl);

        elements.openPlace.classList.toggle("disabled", !enabled);
        elements.openPlace.setAttribute(
            "aria-disabled",
            enabled ? "false" : "true"
        );

        elements.openPlace.tabIndex = enabled ? 0 : -1;
        elements.openPlace.href = enabled ? resolvedUrl : "#";
    }

    function updateActiveHeader(card, profileImage, initialLetter) {
        const placeName = cleanText(card?.dataset.placeName) || state.activePlaceName || "المنشأة";

        if (elements.activeName) {
            elements.activeName.textContent = placeName;
        }

        const profile = cleanText(profileImage || card?.dataset.profile);
        const initial = cleanText(initialLetter || card?.dataset.initial) ||
            state.activeCustomerName.charAt(0) || "م";

        renderAvatarButton(elements.activeAvatar, state.activeCustomerName, profile, initial);
        updatePresenceHeader();
    }

    function updatePresenceHeader() {
        if (elements.activeStatus) {
            const presence = state.activePresenceText ||
                (state.activeCustomerOnline ? "متصل الآن" : "غير متصل");

            elements.activeStatus.textContent = `${state.activeCustomerName} — ${presence}`;
            elements.activeStatus.classList.toggle("typing-indicator", false);
        }

        if (elements.activeLiveDot) {
            elements.activeLiveDot.hidden = !state.activeCustomerOnline;
        }
    }

    function enableHeaderActions(enabled) {
        [elements.headerInfo, elements.headerMenu].forEach(button => {
            if (!button) {
                return;
            }

            button.classList.toggle("disabled", !enabled);
            button.disabled = !enabled;
            button.setAttribute("aria-disabled", enabled ? "false" : "true");
        });
    }

    function bookingFromCard(card) {
        const bookingId = positiveInteger(card?.dataset.bookingId);
        if (!bookingId) {
            return null;
        }

        return normalizeBooking({
            bookingId,
            bookingState: card.dataset.bookingState,
            startDate: card.dataset.bookingStart,
            endDate: card.dataset.bookingEnd
        });
    }

    function normalizeBooking(value) {
        if (!value) {
            return null;
        }

        const bookingId = positiveInteger(value.bookingId ?? value.BookingId);
        if (!bookingId) {
            return null;
        }

        return {
            bookingId,
            bookingState: cleanText(value.bookingState ?? value.BookingState),
            startDate: cleanText(value.startDate ?? value.StartDate),
            endDate: cleanText(value.endDate ?? value.EndDate)
        };
    }

    function updateBookingBar(booking) {
        if (!elements.bookingBar) {
            return;
        }

        if (!booking?.bookingId) {
            elements.bookingBar.classList.add("d-none");
            elements.bookingBar.dataset.bookingId = "";
            elements.bookingDetails && (elements.bookingDetails.hidden = true);
            elements.bookingSummary?.setAttribute("aria-expanded", "false");
            return;
        }

        elements.bookingBar.classList.remove("d-none");
        elements.bookingBar.dataset.bookingId = String(booking.bookingId);
        elements.bookingNumber.textContent = `حجز #${booking.bookingId}`;
        elements.bookingStatus.textContent = bookingStateLabel(booking.bookingState);
        elements.bookingStatus.dataset.state = normalizeState(booking.bookingState);
        elements.bookingDate.textContent = formatCompactDate(booking.startDate);
        elements.bookingDates.textContent = formatBookingPeriod(booking.startDate, booking.endDate);
        elements.openBooking.dataset.bookingId = String(booking.bookingId);
        elements.bookingDetails.hidden = true;
        elements.bookingSummary.setAttribute("aria-expanded", "false");
    }

    function toggleBookingDetails() {
        if (!elements.bookingDetails || !state.latestBooking?.bookingId) {
            return;
        }

        const willOpen = elements.bookingDetails.hidden;
        elements.bookingDetails.hidden = !willOpen;
        elements.bookingSummary?.setAttribute("aria-expanded", willOpen ? "true" : "false");
        elements.bookingBar?.classList.toggle("is-expanded", willOpen);
    }

    /* =========================================================================
       4. جلب وعرض الرسائل
       ========================================================================= */

    async function fetchMessages(customerId) {
        state.messagesRequest?.abort();
        state.messagesRequest = new AbortController();

        const url = new URL(requiredUrl(root.dataset.getMessagesUrl), window.location.origin);
        url.searchParams.set("ownerId", String(customerId));
        url.searchParams.set("placeId", String(state.activePlaceId));

        return requestJson(url.toString(), {
            method: "GET",
            headers: {
                "Accept": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            signal: state.messagesRequest.signal
        });
    }

    function renderMessages(options = {}) {
        if (!elements.messagesArea) {
            return;
        }

        removeMessagesOnly();

        if (!state.messages.length) {
            showConversationEmptyState();
            updateLoadOlderButton();
            return;
        }

        hideInitialState();
        const fragment = document.createDocumentFragment();

        state.messages
            .slice(state.renderStartIndex)
            .forEach(message => fragment.appendChild(createMessageElement(message)));

        elements.messagesArea.appendChild(fragment);
        updateLoadOlderButton();

        if (options.scrollToBottom !== false) {
            scrollMessagesToBottom();
        }
    }

    function loadOlderMessages() {
        if (state.renderStartIndex <= 0 || !elements.messagesArea) {
            return;
        }

        const oldHeight = elements.messagesArea.scrollHeight;
        const oldTop = elements.messagesArea.scrollTop;
        state.renderStartIndex = Math.max(0, state.renderStartIndex - limits.olderStep);
        renderMessages({ scrollToBottom: false });

        requestAnimationFrame(() => {
            const newHeight = elements.messagesArea.scrollHeight;
            elements.messagesArea.scrollTop = newHeight - oldHeight + oldTop;
        });
    }

    function createMessageElement(rawMessage) {
        const message = normalizeMessage(rawMessage);
        const parsed = parseReplyPrefix(message.text);
        const row = document.createElement("div");

        row.className = `chat-message-row ${message.isSentByMe ? "is-owner" : "is-customer"}`;
        row.id = `message_${message.messageId}`;
        row.dataset.messageId = String(message.messageId);
        row.dataset.sentByMe = String(message.isSentByMe);
        row.dataset.messageText = parsed.body;
        row.dataset.messageSender = message.isSentByMe ? "أنت" : state.activeCustomerName;
        row.dataset.sendTime = message.sendTimeRaw || "";
        row.dataset.isEdited = String(message.isEdited);

        const canEdit = message.isSentByMe && withinMinutes(message.sendTimeRaw, 10);
        const canDeleteEveryone = message.isSentByMe &&
            !state.hasConfirmedBooking &&
            withinHours(message.sendTimeRaw, 24);

        row.innerHTML = `
            <article class="chat-message-bubble">
                ${parsed.quoteText ? `
                    <button type="button" class="message-reply-quote" data-scroll-quote>
                        <strong>${escapeHtml(parsed.quoteSender || "رد")}</strong>
                        <span>${escapeHtml(parsed.quoteText)}</span>
                    </button>
                ` : ""}

                ${parsed.body ? `
                    <div class="chat-message-text">${textToSafeHtml(parsed.body)}</div>
                ` : ""}

                ${buildAttachmentMarkup(message)}

                <footer class="chat-message-meta">
                    <time datetime="${escapeAttribute(message.sendTimeRaw)}">${escapeHtml(message.time || formatClock(message.sendTimeRaw))}</time>
                    ${message.isEdited ? '<span class="message-edited-label">معدلة</span>' : ""}
                    ${message.isSentByMe ? readStateMarkup(message.isRead) : ""}
                </footer>

                <div class="chat-message-actions">
                    <button type="button"
                            class="message-menu-trigger"
                            data-message-menu
                            aria-label="إجراءات الرسالة"
                            aria-expanded="false">
                        <i class="bi bi-chevron-down"></i>
                    </button>

                    <div class="message-actions-popover" hidden>
                        ${messageActionButton("reply", "bi-reply", "رد")}
                        ${messageActionButton("copy", "bi-copy", "نسخ")}
                        ${message.isSentByMe && canEdit ? messageActionButton("edit", "bi-pencil", "تعديل") : ""}
                        ${messageActionButton("delete-me", "bi-trash3", "حذف لدي", "is-danger")}
                        ${message.isSentByMe && canDeleteEveryone
                ? messageActionButton("delete-everyone", "bi-trash3-fill", "حذف للجميع", "is-danger")
                : ""}
                    </div>
                </div>
            </article>
        `;

        return row;
    }

    function normalizeMessage(value) {
        return {
            messageId: value?.messageId ?? value?.MessageId ?? `temp_${Date.now()}`,
            senderId: positiveInteger(value?.senderId ?? value?.SenderId),
            receiverId: positiveInteger(value?.receiverId ?? value?.ReceiverId),
            placeId: positiveInteger(value?.placeId ?? value?.PlaceId) || state.activePlaceId,
            text: cleanTextPreserveLines(value?.text ?? value?.MessageText),
            time: cleanText(value?.time ?? value?.Time),
            isSentByMe: Boolean(value?.isSentByMe ?? value?.IsSentByMe),
            isRead: Boolean(value?.isRead ?? value?.IsRead),
            isEdited: Boolean(value?.isEdited ?? value?.IsEdited),
            attachmentUrl: cleanText(value?.attachmentUrl ?? value?.AttachmentUrl),
            attachmentName: cleanText(value?.attachmentName ?? value?.AttachmentName),
            attachmentSize: cleanText(value?.attachmentSize ?? value?.AttachmentSize),
            isImage: Boolean(value?.isImage ?? value?.IsImage),
            isVideo: Boolean(value?.isVideo ?? value?.IsVideo),
            sendTimeRaw: cleanText(value?.sendTimeRaw ?? value?.SendTimeRaw)
        };
    }

    function parseReplyPrefix(text) {
        const value = cleanTextPreserveLines(text);
        const match = value.match(/^\[REPLY\|([^|]*)\|([^\]]*)\]\s*/);

        if (!match) {
            return {
                quoteSender: "",
                quoteText: "",
                body: value
            };
        }

        return {
            quoteSender: match[1].trim(),
            quoteText: match[2].trim(),
            body: value.slice(match[0].length).trim()
        };
    }

    function buildAttachmentMarkup(message) {
        if (!message.attachmentUrl) {
            return "";
        }

        const url = escapeAttribute(message.attachmentUrl);
        const name = escapeHtml(message.attachmentName || fileNameFromUrl(message.attachmentUrl) || "مرفق");
        const size = escapeHtml(message.attachmentSize || "");

        if (message.isImage) {
            return `
                <button type="button"
                        class="chat-attachment-media is-image"
                        data-chat-media-url="${url}"
                        aria-label="فتح الصورة">
                    <img src="${url}" alt="${name}" loading="lazy" />
                    ${size ? `<span>${size}</span>` : ""}
                </button>
            `;
        }

        if (message.isVideo) {
            return `
                <div class="chat-attachment-media is-video">
                    <video controls preload="metadata">
                        <source src="${url}" />
                    </video>
                    <div class="chat-attachment-caption">
                        <span>${name}</span>
                        ${size ? `<small>${size}</small>` : ""}
                    </div>
                </div>
            `;
        }

        return `
            <a class="chat-file-card"
               href="${url}"
               target="_blank"
               rel="noopener noreferrer"
               download>
                <span class="chat-file-icon"><i class="bi ${fileIcon(message.attachmentName || message.attachmentUrl)}"></i></span>
                <span class="chat-file-copy">
                    <strong>${name}</strong>
                    <small>${escapeHtml(fileExtension(message.attachmentName || message.attachmentUrl).toUpperCase())}${size ? ` • ${size}` : ""}</small>
                </span>
                <i class="bi bi-download chat-file-download"></i>
            </a>
        `;
    }

    function messageActionButton(action, icon, label, extraClass = "") {
        return `
            <button type="button"
                    class="message-action-item ${extraClass}"
                    data-message-action="${escapeAttribute(action)}">
                <i class="bi ${escapeAttribute(icon)}"></i>
                <span>${escapeHtml(label)}</span>
            </button>
        `;
    }

    function readStateMarkup(isRead) {
        return `
            <span class="message-read-state ${isRead ? "is-read" : "is-sent"}"
                  title="${isRead ? "تمت القراءة" : "تم الإرسال"}">
                <i class="bi ${isRead ? "bi-check2-all" : "bi-check2"}"></i>
            </span>
        `;
    }

    function showMessagesLoading() {
        if (!elements.messagesArea) {
            return;
        }

        elements.messagesArea.setAttribute("aria-busy", "true");
        elements.messagesArea.innerHTML = `
            <div class="chat-loading-state">
                <span class="chat-loading-orbit"><i class="bi bi-chat-dots-fill"></i></span>
                <strong>جارٍ تحميل المحادثة</strong>
                <span>نجهّز الرسائل بأمان...</span>
            </div>
        `;
    }

    function showMessagesError(message) {
        if (!elements.messagesArea) {
            return;
        }

        elements.messagesArea.setAttribute("aria-busy", "false");
        elements.messagesArea.innerHTML = `
            <div class="chat-error-state">
                <span><i class="bi bi-wifi-off"></i></span>
                <strong>تعذر تحميل الرسائل</strong>
                <p>${escapeHtml(message)}</p>
                <button type="button" id="retryLoadChatBtn">
                    <i class="bi bi-arrow-clockwise"></i>
                    إعادة المحاولة
                </button>
            </div>
        `;

        document.getElementById("retryLoadChatBtn")?.addEventListener("click", () => {
            if (state.activeCustomerCard) {
                openContact(state.activeCustomerCard, state.activeCustomerId, state.activeCustomerName);
            }
        });
    }

    function showConversationEmptyState() {
        if (!elements.messagesArea) {
            return;
        }

        elements.messagesArea.innerHTML = `
            <div class="chat-empty-state is-conversation-empty">
                <span class="chat-empty-state__art" aria-hidden="true">
                    <i class="bi bi-chat-heart"></i><span></span>
                </span>
                <h3>ابدأ المحادثة مع ${escapeHtml(state.activeCustomerName)}</h3>
                <p>لا توجد رسائل سابقة داخل هذه المنشأة. اكتب أول رسالة من الأسفل.</p>
            </div>
        `;
    }

    function removeMessagesOnly() {
        elements.messagesArea?.querySelectorAll(
            ".chat-message-row, .chat-loading-state, .chat-error-state, .is-conversation-empty"
        ).forEach(item => item.remove());
        elements.messagesArea?.setAttribute("aria-busy", "false");
    }

    function hideInitialState() {
        elements.initialState?.remove();
    }

    function updateLoadOlderButton() {
        if (!elements.loadOlder || !elements.messagesArea) {
            return;
        }

        const show = state.messages.length > 0 && state.renderStartIndex > 0;
        elements.loadOlder.classList.toggle("d-none", !show);

        // حالات التحميل والفراغ تستبدل محتوى منطقة الرسائل، لذلك قد يصبح
        // الزر منفصلًا عن DOM. نعيد تثبيته دائمًا عند الحاجة.
        if (show) {
            elements.messagesArea.prepend(elements.loadOlder);
        }
    }

    function focusRequestedNotificationMessage(ownerId, placeId) {
        const messageId = state.requestedNotificationMessageId;

        if (
            state.notificationMessageFocusConsumed ||
            !messageId ||
            ownerId !== state.activeCustomerId ||
            placeId !== state.activePlaceId
        ) {
            return;
        }

        const targetIndex = state.messages.findIndex(message =>
            positiveInteger(message?.messageId ?? message?.MessageId) === messageId
        );

        if (targetIndex < 0) {
            return;
        }

        if (targetIndex < state.renderStartIndex) {
            state.renderStartIndex = Math.max(0, targetIndex);
            renderMessages({ scrollToBottom: false });
        }

        window.requestAnimationFrame(() => {
            const row = document.getElementById(`message_${messageId}`);

            if (!row) {
                return;
            }

            state.notificationMessageFocusConsumed = true;
            row.setAttribute("tabindex", "-1");
            row.scrollIntoView({
                behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches
                    ? "auto"
                    : "smooth",
                block: "center"
            });

            window.setTimeout(() => {
                row.focus({ preventScroll: true });
            }, 180);
        });
    }

    /* =========================================================================
       5. إجراءات الرسائل
       ========================================================================= */

    async function executeMessageAction(button) {
        const action = button.dataset.messageAction;
        const panel = button.closest(".message-actions-popover");
        const portalMessageId = panel?.dataset.messageId;
        const row = button.closest(".chat-message-row") ||
            (portalMessageId
                ? document.getElementById(`message_${portalMessageId}`)
                : null);
        const messageId = row?.dataset.messageId || portalMessageId;

        if (!row || !messageId) {
            return;
        }

        closeAllMessageMenus();

        if (action === "reply") {
            beginReply(row);
            return;
        }

        if (action === "copy") {
            await copyMessage(row);
            return;
        }

        if (action === "edit") {
            beginEdit(row);
            return;
        }

        if (action === "delete-me") {
            await deleteMessageForMe(messageId, row);
            return;
        }

        if (action === "delete-everyone") {
            await deleteMessageForEveryone(messageId, row);
        }
    }

    function beginReply(row) {
        const messageText = cleanTextPreserveLines(row.dataset.messageText) || "📎 مرفق";
        const sender = cleanText(row.dataset.messageSender) || "رسالة";

        clearEdit();
        state.reply = {
            text: messageText.slice(0, 300),
            sender: sender.slice(0, 80)
        };

        if (elements.replyToName) {
            elements.replyToName.textContent = `رد على ${state.reply.sender}`;
        }
        if (elements.replyToText) {
            elements.replyToText.textContent = state.reply.text;
        }

        elements.replyBox?.classList.remove("d-none");
        elements.input?.focus();
        updateSendButton();
    }

    function clearReply() {
        state.reply = null;
        elements.replyBox?.classList.add("d-none");
        if (elements.replyToName) elements.replyToName.textContent = "";
        if (elements.replyToText) elements.replyToText.textContent = "";
    }

    function beginEdit(row) {
        const messageId = positiveInteger(row.dataset.messageId);
        const text = cleanTextPreserveLines(row.dataset.messageText);

        if (!messageId || !text) {
            alertUser("تعذر التعديل", "هذه الرسالة لا تحتوي على نص قابل للتعديل.", "warning");
            return;
        }

        clearReply();
        clearSelectedFiles();
        state.editing = {
            messageId,
            originalText: text,
            draftBeforeEdit: elements.input?.value || ""
        };

        if (elements.editingMessageId) elements.editingMessageId.value = String(messageId);
        if (elements.editOriginalText) elements.editOriginalText.textContent = text;
        elements.editBox?.classList.remove("d-none");

        if (elements.input) {
            elements.input.value = text;
            autoResizeInput();
            updateCharacterCounter();
            updateSendButton();
            elements.input.focus();
            elements.input.setSelectionRange(text.length, text.length);
        }
    }

    function clearEdit(options = {}) {
        const previousDraft = state.editing?.draftBeforeEdit || "";
        const keepInput = options.keepInput === true;
        const restoreDraft = options.restoreDraft !== false;

        state.editing = null;
        if (elements.editingMessageId) elements.editingMessageId.value = "";
        if (elements.editOriginalText) elements.editOriginalText.textContent = "";
        elements.editBox?.classList.add("d-none");

        if (!keepInput && elements.input) {
            elements.input.value = restoreDraft ? previousDraft : "";
            autoResizeInput();
            updateCharacterCounter();
            updateSendButton();
            saveActiveDraft();
        }
    }

    async function copyMessage(row) {
        const text = cleanTextPreserveLines(row.dataset.messageText) || "مرفق";

        try {
            await navigator.clipboard.writeText(text);
            showSystemNotice("تم نسخ نص الرسالة.", "success", 2200);
        } catch {
            const textarea = document.createElement("textarea");
            textarea.value = text;
            textarea.style.position = "fixed";
            textarea.style.opacity = "0";
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand("copy");
            textarea.remove();
            showSystemNotice("تم نسخ نص الرسالة.", "success", 2200);
        }
    }

    async function deleteMessageForMe(messageId, row) {
        const confirmed = await confirmAction({
            title: "حذف الرسالة من شاشتك؟",
            text: "لن تختفي الرسالة من شاشة الطرف الآخر.",
            confirmText: "حذف لدي",
            confirmColor: "#FF5A5F"
        });

        if (!confirmed) {
            return;
        }

        try {
            const result = await postForm(requiredUrl(root.dataset.deleteForMeUrl), {
                messageId,
                placeId: state.activePlaceId
            });

            if (!result.success) {
                throw new Error(result.message || "تعذر حذف الرسالة.");
            }

            removeMessageRow(row);
            showSystemNotice("تم حذف الرسالة من شاشتك.", "success", 2500);
        } catch (error) {
            alertUser("تعذر الحذف", error.message, "error");
        }
    }

    async function deleteMessageForEveryone(messageId, row) {
        const confirmed = await confirmAction({
            title: "حذف الرسالة للجميع؟",
            text: "ستختفي الرسالة من الطرفين، ولا يمكن التراجع عن هذا الإجراء.",
            confirmText: "حذف للجميع",
            confirmColor: "#FF5A5F"
        });

        if (!confirmed) {
            return;
        }

        try {
            const result = await postForm(requiredUrl(root.dataset.deleteForEveryoneUrl), {
                messageId,
                placeId: state.activePlaceId
            });

            if (!result.success) {
                throw new Error(result.message || "تعذر حذف الرسالة للجميع.");
            }

            removeMessageRow(row);
            showSystemNotice("تم حذف الرسالة للجميع.", "success", 2500);
        } catch (error) {
            alertUser("تعذر الحذف", error.message, "error");
        }
    }

    function removeMessageRow(row) {
        row.classList.add("is-removing");
        window.setTimeout(() => {
            row.remove();
            state.messages = state.messages.filter(
                message => String(message.messageId ?? message.MessageId) !== String(row.dataset.messageId)
            );
            if (!elements.messagesArea?.querySelector(".chat-message-row")) {
                showConversationEmptyState();
            }
        }, 180);
    }

    function toggleMessageMenu(button) {
        const row = button.closest(".chat-message-row");
        const messageId = row?.dataset.messageId || "";
        const localPanel = button.parentElement?.querySelector(".message-actions-popover");
        const portaledPanel = messageId
            ? document.querySelector(
                `.message-actions-popover.is-message-menu-portal[data-message-id="${cssEscape(messageId)}"]`
            )
            : null;
        const panel = localPanel || portaledPanel;

        if (!panel || !row) {
            return;
        }

        const isAlreadyOpen = !panel.hidden && panel.classList.contains("is-visible");
        closeAllMessageMenus();

        if (isAlreadyOpen) {
            return;
        }

        portalMessageMenu(panel, button, messageId);
        panel.hidden = false;
        panel.classList.add("is-visible");
        button.setAttribute("aria-expanded", "true");

        window.requestAnimationFrame(() => positionMessageMenu(panel, button, row));
    }

    function portalMessageMenu(panel, button, messageId) {
        if (panel.classList.contains("is-message-menu-portal")) {
            return;
        }

        const originalParent = panel.parentElement;
        if (!originalParent) {
            return;
        }

        mobileMessageMenuOrigins.set(panel, originalParent);
        panel.dataset.messageId = messageId ||
            button.closest(".chat-message-row")?.dataset.messageId ||
            "";
        panel.classList.add("is-message-menu-portal");
        document.body.appendChild(panel);
    }

    function positionMessageMenu(panel, button, row) {
        if (!panel?.isConnected || !button?.isConnected) {
            return;
        }

        const viewport = window.visualViewport;
        const viewportLeft = viewport?.offsetLeft || 0;
        const viewportTop = viewport?.offsetTop || 0;
        const viewportWidth = viewport?.width || window.innerWidth;
        const viewportHeight = viewport?.height || window.innerHeight;
        const viewportRight = viewportLeft + viewportWidth;
        const viewportBottom = viewportTop + viewportHeight;
        const padding = 8;
        const gap = 6;

        const conversationRect = elements.conversationPanel?.getBoundingClientRect();
        const boundaryLeft = Math.max(
            viewportLeft + padding,
            conversationRect ? conversationRect.left + padding : viewportLeft + padding
        );
        const boundaryRight = Math.min(
            viewportRight - padding,
            conversationRect ? conversationRect.right - padding : viewportRight - padding
        );
        const boundaryTop = Math.max(
            viewportTop + padding,
            conversationRect ? conversationRect.top + padding : viewportTop + padding
        );
        const boundaryBottom = Math.min(
            viewportBottom - padding,
            conversationRect ? conversationRect.bottom - padding : viewportBottom - padding
        );

        const availableWidth = Math.max(150, boundaryRight - boundaryLeft);
        const preferredWidth = Math.min(176, availableWidth);

        panel.style.setProperty("position", "fixed", "important");
        panel.style.setProperty("z-index", "2147483000", "important");
        panel.style.setProperty("width", `${preferredWidth}px`, "important");
        panel.style.setProperty("min-width", `${preferredWidth}px`, "important");
        panel.style.setProperty("max-width", `${availableWidth}px`, "important");
        panel.style.setProperty("height", "auto", "important");
        panel.style.setProperty("min-height", "0", "important");
        panel.style.setProperty("max-height", `${Math.max(120, boundaryBottom - boundaryTop)}px`, "important");
        panel.style.setProperty("right", "auto", "important");
        panel.style.setProperty("bottom", "auto", "important");
        panel.style.setProperty("visibility", "hidden", "important");

        const buttonRect = button.getBoundingClientRect();
        const panelRect = panel.getBoundingClientRect();
        const boundaryCenter = boundaryLeft + ((boundaryRight - boundaryLeft) / 2);
        const buttonCenter = buttonRect.left + (buttonRect.width / 2);

        // القائمة تفتح بجانب السهم نفسه، لا على الجهة المقابلة من المحادثة.
        // إن كان السهم في النصف الأيمن تفتح القائمة إلى يساره، والعكس صحيح.
        let left = buttonCenter >= boundaryCenter
            ? buttonRect.right - panelRect.width
            : buttonRect.left;

        left = Math.max(
            boundaryLeft,
            Math.min(left, boundaryRight - panelRect.width)
        );

        const availableBelow = boundaryBottom - buttonRect.bottom;
        const availableAbove = buttonRect.top - boundaryTop;
        let top = buttonRect.bottom + gap;

        if (panelRect.height + gap > availableBelow && availableAbove >= panelRect.height + gap) {
            top = buttonRect.top - panelRect.height - gap;
        }

        top = Math.max(
            boundaryTop,
            Math.min(top, boundaryBottom - panelRect.height)
        );

        panel.style.setProperty("left", `${Math.round(left)}px`, "important");
        panel.style.setProperty("top", `${Math.round(top)}px`, "important");
        panel.style.removeProperty("visibility");
    }

    function clearMessageMenuPosition(panel) {
        [
            "position", "z-index", "width", "min-width", "max-width",
            "height", "min-height", "max-height", "top", "right",
            "bottom", "left", "visibility"
        ].forEach(property => panel.style.removeProperty(property));
    }

    function restoreMessageMenu(panel) {
        const originalParent = mobileMessageMenuOrigins.get(panel);

        if (originalParent && originalParent.isConnected) {
            originalParent.appendChild(panel);
        }

        clearMessageMenuPosition(panel);
        mobileMessageMenuOrigins.delete(panel);
        panel.classList.remove("is-message-menu-portal");
        panel.removeAttribute("data-message-id");
    }

    function closeAllMessageMenus() {
        document.querySelectorAll(".message-actions-popover").forEach(panel => {
            panel.classList.remove("is-visible");
            panel.hidden = true;

            if (panel.classList.contains("is-message-menu-portal")) {
                restoreMessageMenu(panel);
            } else {
                clearMessageMenuPosition(panel);
            }
        });

        elements.messagesArea?.querySelectorAll("[data-message-menu]").forEach(button => {
            button.setAttribute("aria-expanded", "false");
        });

        syncSharedMobileOverlay();
    }

    /* =========================================================================
       6. الإرسال والتعديل وحالة الفشل
       ========================================================================= */

    async function submitComposer(quickTopic = "") {
        if (!composerIsUsable() || state.isSending) {
            return;
        }

        const text = cleanTextPreserveLines(elements.input?.value).trim();

        if (state.editing) {
            await submitEdit(text);
            return;
        }

        if (!text && !state.selectedFiles.length) {
            return;
        }

        const payload = {
            receiverId: state.activeCustomerId,
            text,
            quickTopic: cleanText(quickTopic).toLowerCase(),
            files: state.selectedFiles.map(item => item.file),
            quote: state.reply ? { ...state.reply } : null
        };

        await submitNewMessage(payload);
    }

    async function submitEdit(text) {
        if (!state.editing?.messageId) {
            return;
        }

        if (!text) {
            alertUser("تنبيه", "لا يمكن حفظ رسالة فارغة.", "warning");
            return;
        }

        setSendingState(true);

        try {
            const result = await postForm(requiredUrl(root.dataset.editMessageUrl), {
                messageId: state.editing.messageId,
                placeId: state.activePlaceId,
                newText: text
            });

            if (!result.success) {
                throw new Error(result.message || "تعذر تعديل الرسالة.");
            }

            clearEdit({ restoreDraft: false });
            elements.input.value = "";
            clearDraft(state.activeCustomerId);
            autoResizeInput();
            updateCharacterCounter();
            showSystemNotice("تم تعديل الرسالة.", "success", 2200);
            await reloadActiveConversation({ preserveScroll: true });
        } catch (error) {
            alertUser("تعذر التعديل", error.message, "error");
        } finally {
            setSendingState(false);
        }
    }

    async function submitNewMessage(payload, retryRow = null) {
        setSendingState(true);
        state.failedSend = null;

        const optimisticRow = retryRow || appendOptimisticMessage(payload);
        optimisticRow.classList.remove("is-failed");
        optimisticRow.classList.add("is-sending");
        updateOptimisticState(optimisticRow, "sending");

        try {
            const formData = new FormData();
            formData.append("receiverId", String(payload.receiverId));
            formData.append("placeId", String(state.activePlaceId));
            formData.append("messageText", payload.text || "");
            formData.append("quickTopic", payload.quickTopic || "");
            formData.append("quotedText", payload.quote?.text || "");
            formData.append("quotedSender", payload.quote?.sender || "");
            appendAntiForgery(formData);
            payload.files.forEach(file => formData.append("attachments", file));

            const result = await requestJson(requiredUrl(root.dataset.sendMessageUrl), {
                method: "POST",
                body: formData,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!result.success) {
                throw new Error(result.message || "تعذر إرسال الرسالة.");
            }

            optimisticRow.remove();
            clearComposerAfterSuccessfulSend(payload);
            updateContactAfterSend(payload);
            await reloadActiveConversation({ scrollToBottom: true });
        } catch (error) {
            console.error("Customer chat send error:", error);
            state.failedSend = payload;
            optimisticRow.classList.remove("is-sending");
            optimisticRow.classList.add("is-failed");
            updateOptimisticState(optimisticRow, "failed", error.message);
            alertUser("فشل الإرسال", error.message || "تحقق من الاتصال ثم أعد المحاولة.", "error");
        } finally {
            setSendingState(false);
        }
    }

    function appendOptimisticMessage(payload) {
        hideInitialState();
        elements.messagesArea?.querySelector(".is-conversation-empty")?.remove();

        const row = document.createElement("div");
        row.className = "chat-message-row is-owner is-sending optimistic-message";
        row.dataset.messageId = `temp_${Date.now()}`;
        row.dataset.sentByMe = "true";
        row.dataset.messageText = payload.text || (payload.files.length ? "📎 مرفق" : "");
        row.dataset.messageSender = "أنت";

        const previews = payload.files.map(file => {
            if (file.type.startsWith("image/")) {
                const objectUrl = URL.createObjectURL(file);
                state.objectUrls.add(objectUrl);
                return `<div class="optimistic-file-preview is-image"><img src="${escapeAttribute(objectUrl)}" alt="${escapeAttribute(file.name)}" /></div>`;
            }
            return `<div class="optimistic-file-preview"><i class="bi ${fileIcon(file.name)}"></i><span>${escapeHtml(file.name)}</span></div>`;
        }).join("");

        row.innerHTML = `
            <article class="chat-message-bubble">
                ${payload.quote ? `
                    <div class="message-reply-quote">
                        <strong>${escapeHtml(payload.quote.sender)}</strong>
                        <span>${escapeHtml(payload.quote.text)}</span>
                    </div>
                ` : ""}
                ${payload.text ? `<div class="chat-message-text">${textToSafeHtml(payload.text)}</div>` : ""}
                ${previews ? `<div class="optimistic-files">${previews}</div>` : ""}
                <footer class="chat-message-meta optimistic-status" data-optimistic-status>
                    <span class="mini-spinner"></span>
                    <span>جارٍ الإرسال...</span>
                </footer>
            </article>
        `;

        elements.messagesArea?.appendChild(row);
        scrollMessagesToBottom();
        return row;
    }

    function updateOptimisticState(row, mode, message = "") {
        const status = row.querySelector("[data-optimistic-status]");
        if (!status) {
            return;
        }

        if (mode === "sending") {
            status.innerHTML = '<span class="mini-spinner"></span><span>جارٍ الإرسال...</span>';
            return;
        }

        status.innerHTML = `
            <i class="bi bi-exclamation-circle-fill"></i>
            <span>${escapeHtml(message || "فشل الإرسال")}</span>
            <button type="button" data-message-action="retry-send">إعادة المحاولة</button>
        `;

        status.querySelector("[data-message-action='retry-send']")?.addEventListener("click", event => {
            event.stopPropagation();
            if (state.failedSend) {
                void submitNewMessage(state.failedSend, row);
            }
        });
    }

    function clearComposerAfterSuccessfulSend(payload) {
        if (elements.input && elements.input.value.trim() === payload.text.trim()) {
            elements.input.value = "";
        }

        clearReply();
        clearEdit({ restoreDraft: false });
        clearSelectedFiles();
        clearDraft(state.activeCustomerId);
        autoResizeInput();
        updateCharacterCounter();
        updateSendButton();
    }

    function updateContactAfterSend(payload) {
        const card = state.activeCustomerCard;
        if (!card) {
            return;
        }

        const preview = card.querySelector(`#contactMsg_${state.activeCustomerId}`) ||
            card.querySelector(".chat-contact-line--message p");
        const time = card.querySelector(`#contactTime_${state.activeCustomerId}`) ||
            card.querySelector(".contact-time-text");

        if (preview) {
            preview.textContent = payload.text || (payload.files.length ? "📎 مرفق" : "رسالة");
        }
        if (time) {
            time.textContent = "الآن";
            time.setAttribute("datetime", new Date().toISOString());
        }

        card.dataset.hasConversation = "true";
        card.dataset.contactSource = "conversation";
        moveCardToTop(card);
    }

    function setSendingState(isSending) {
        state.isSending = isSending;
        shell.classList.toggle("is-sending-message", isSending);
        elements.sendSpinner?.classList.toggle("d-none", !isSending);
        elements.sendIcon?.classList.toggle("d-none", isSending);
        elements.input && (elements.input.readOnly = isSending);
        updateSendButton();
    }

    async function reloadActiveConversation(options = {}) {
        if (!state.activeCustomerId) {
            return;
        }

        const currentScroll = elements.messagesArea?.scrollTop || 0;

        try {
            const result = await fetchMessages(state.activeCustomerId);
            if (!result.success) {
                return;
            }

            state.messages = Array.isArray(result.data) ? result.data : [];
            state.hasConfirmedBooking = Boolean(result.hasBooking);
            state.activeCustomerOnline = Boolean(result.isOnline);
            state.activePresenceText = cleanText(result.statusText) || state.activePresenceText;
            state.latestBooking = normalizeBooking(result.latestBooking) || state.latestBooking;
            state.renderStartIndex = Math.max(0, state.messages.length - limits.initialMessages);

            updatePresenceHeader();
            updateBookingBar(state.latestBooking);
            renderMessages({ scrollToBottom: options.scrollToBottom !== false });

            if (options.preserveScroll) {
                elements.messagesArea.scrollTop = currentScroll;
            }

            markCardRead(state.activeCustomerCard);
        } catch (error) {
            if (error?.name !== "AbortError") {
                console.warn("تعذر تحديث المحادثة بصمت.", error);
            }
        }
    }

    /* =========================================================================
       7. المرفقات والردود السريعة
       ========================================================================= */

    function bindFileInput(input) {
        input.addEventListener("change", () => {
            addSelectedFiles(Array.from(input.files || []));
            input.value = "";
            closeMobileSheets();
        });
    }

    function addSelectedFiles(files) {
        if (!composerIsUsable() || !files.length) {
            return;
        }

        const next = [...state.selectedFiles];
        const errors = [];

        files.forEach(file => {
            if (next.length >= limits.filesCount) {
                errors.push(`الحد الأقصى هو ${limits.filesCount} ملفات.`);
                return;
            }

            const extension = fileExtension(file.name).toLowerCase();
            if (!allowedExtensions.has(extension)) {
                errors.push(`نوع الملف «${file.name}» غير مسموح.`);
                return;
            }

            if (file.size <= 0) {
                errors.push(`الملف «${file.name}» فارغ.`);
                return;
            }

            if (file.size > limits.fileBytes) {
                errors.push(`الملف «${file.name}» يتجاوز 10 ميجابايت.`);
                return;
            }

            const alreadyAdded = next.some(item =>
                item.file.name === file.name &&
                item.file.size === file.size &&
                item.file.lastModified === file.lastModified
            );

            if (!alreadyAdded) {
                next.push({ id: fileToken(file), file, previewUrl: "" });
            }
        });

        const totalBytes = next.reduce((sum, item) => sum + item.file.size, 0);
        if (totalBytes > limits.totalBytes) {
            errors.push("الحجم الإجمالي للمرفقات يجب ألا يتجاوز 25 ميجابايت.");
            return alertUser("تعذر إضافة الملفات", errors[errors.length - 1], "warning");
        }

        state.selectedFiles = next;
        window.selectedFilesArray = state.selectedFiles.map(item => item.file);
        renderSelectedFiles();
        updateSendButton();

        if (errors.length) {
            alertUser("بعض الملفات لم تُضف", errors.join("\n"), "warning");
        }
    }

    function renderSelectedFiles() {
        if (!elements.attachmentsPreview) {
            return;
        }

        revokeUnusedObjectUrls();
        elements.attachmentsPreview.innerHTML = "";
        elements.attachmentsPreview.classList.toggle("d-none", state.selectedFiles.length === 0);

        state.selectedFiles.forEach((item, index) => {
            const file = item.file;
            const card = document.createElement("div");
            card.className = "attachment-preview-card";
            card.dataset.fileToken = item.id;

            let visual;
            if (file.type.startsWith("image/")) {
                item.previewUrl = URL.createObjectURL(file);
                state.objectUrls.add(item.previewUrl);
                visual = `<img src="${escapeAttribute(item.previewUrl)}" alt="${escapeAttribute(file.name)}" />`;
            } else {
                visual = `<span class="attachment-preview-icon"><i class="bi ${fileIcon(file.name)}"></i></span>`;
            }

            card.innerHTML = `
                <div class="attachment-preview-visual">${visual}</div>
                <div class="attachment-preview-copy">
                    <strong title="${escapeAttribute(file.name)}">${escapeHtml(file.name)}</strong>
                    <small>${escapeHtml(fileExtension(file.name).toUpperCase())} • ${escapeHtml(formatBytes(file.size))}</small>
                </div>
                <button type="button" aria-label="إزالة الملف" data-remove-file-index="${index}">
                    <i class="bi bi-x-lg"></i>
                </button>
            `;

            card.querySelector("[data-remove-file-index]")?.addEventListener("click", () => {
                removeSelectedFile(index);
            });

            elements.attachmentsPreview.appendChild(card);
        });
    }

    function removeSelectedFile(index) {
        const parsed = Number(index);
        if (!Number.isInteger(parsed) || parsed < 0 || parsed >= state.selectedFiles.length) {
            return;
        }

        const [removed] = state.selectedFiles.splice(parsed, 1);
        if (removed?.previewUrl) {
            URL.revokeObjectURL(removed.previewUrl);
            state.objectUrls.delete(removed.previewUrl);
        }

        window.selectedFilesArray = state.selectedFiles.map(item => item.file);
        renderSelectedFiles();
        updateSendButton();
    }

    function clearSelectedFiles() {
        state.selectedFiles.forEach(item => {
            if (item.previewUrl) {
                URL.revokeObjectURL(item.previewUrl);
                state.objectUrls.delete(item.previewUrl);
            }
        });
        state.selectedFiles = [];
        window.selectedFilesArray = [];
        if (elements.attachmentsPreview) {
            elements.attachmentsPreview.innerHTML = "";
            elements.attachmentsPreview.classList.add("d-none");
        }
        updateSendButton();
    }

    function revokeUnusedObjectUrls() {
        state.selectedFiles.forEach(item => {
            if (item.previewUrl) {
                URL.revokeObjectURL(item.previewUrl);
                state.objectUrls.delete(item.previewUrl);
                item.previewUrl = "";
            }
        });
    }

    function renderQuickReplies(replies) {
        if (!elements.quickRepliesBar || !elements.quickRepliesTrack) {
            return;
        }

        const values = normalizeQuickReplies(replies);
        elements.quickRepliesTrack.innerHTML = "";
        elements.quickRepliesBar.classList.toggle("d-none", values.length === 0);

        const label = elements.quickRepliesBar.querySelector(".quick-replies-label");
        if (label) {
            label.innerHTML = '<i class="bi bi-lightning-charge-fill"></i> أسئلة سريعة';
        }

        values.forEach(topic => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "quick-reply-chip";
            button.dataset.topicKey = topic.key;
            button.title = topic.question;
            button.innerHTML = `<i class="bi bi-lightning-charge-fill"></i><span>${escapeHtml(topic.title)}</span>`;
            button.addEventListener("click", () => {
                if (!composerIsUsable() || state.isSending) {
                    return;
                }

                const current = elements.input.value.trim();
                if (current) {
                    elements.input.value = `${current}
${topic.question}`;
                    elements.input.dispatchEvent(new Event("input", { bubbles: true }));
                    elements.input.focus();
                    return;
                }

                elements.input.value = topic.question;
                elements.input.dispatchEvent(new Event("input", { bubbles: true }));
                void submitComposer(topic.key);
            });
            elements.quickRepliesTrack.appendChild(button);
        });
    }

    /* =========================================================================
       8. البحث والعدادات وترتيب القائمة
       ========================================================================= */

    function applyContactFilters() {
        let visibleCount = 0;

        getContactCards().forEach(card => {
            const unread = positiveInteger(card.dataset.unread) > 0;
            const hasBooking = toBoolean(card.dataset.hasBooking);
            const hasConversation = toBoolean(card.dataset.hasConversation);
            const haystack = normalizeForSearch([
                card.dataset.name,
                card.dataset.placeName,
                card.dataset.bookingId,
                card.querySelector(".chat-contact-line--message p")?.textContent
            ].join(" "));

            const matchesSearch = !state.searchText || haystack.includes(state.searchText);
            const matchesFilter = state.currentFilter === "all" ||
                (state.currentFilter === "unread" && unread) ||
                (state.currentFilter === "bookings" && hasBooking) ||
                (state.currentFilter === "conversations" && hasConversation);

            const visible = matchesSearch && matchesFilter;
            card.hidden = !visible;
            if (visible) visibleCount += 1;
        });

        if (elements.noResults) {
            elements.noResults.classList.toggle("d-none", visibleCount > 0 || getContactCards().length === 0);
        }
    }

    function markCardRead(card) {
        if (!card) {
            return;
        }

        card.dataset.unread = "0";
        card.classList.remove("unread");
        const badge = card.querySelector(".unread-badge");
        if (badge) {
            badge.textContent = "0";
            badge.style.display = "none";
        }
        updateUnreadBadges();
        applyContactFilters();
    }

    function incrementCardUnread(card) {
        if (!card) {
            return;
        }

        const next = positiveInteger(card.dataset.unread) + 1;
        card.dataset.unread = String(next);
        card.classList.add("unread");
        const badge = card.querySelector(".unread-badge");
        if (badge) {
            badge.textContent = String(next);
            badge.style.display = "inline-grid";
        }
        updateUnreadBadges();
        applyContactFilters();
    }

    function updateUnreadBadges() {
        const cards = getContactCards();
        const unreadMessages = cards.reduce((sum, card) => sum + positiveInteger(card.dataset.unread), 0);
        const unreadConversations = cards.filter(card => positiveInteger(card.dataset.unread) > 0).length;

        if (elements.unreadConversationsBadge) {
            elements.unreadConversationsBadge.textContent = String(unreadConversations);
            elements.unreadConversationsBadge.style.display = unreadConversations > 0 ? "inline-grid" : "none";
        }

        if (elements.totalUnreadBadge) {
            elements.totalUnreadBadge.textContent = String(unreadMessages > 0 ? unreadMessages : cards.length);
            elements.totalUnreadBadge.classList.toggle("has-items", unreadMessages > 0);
            elements.totalUnreadBadge.dataset.unreadConversationsCount = String(unreadConversations);
            elements.totalUnreadBadge.dataset.conversationsCount = String(cards.length);
        }

        if (window.CustomerBottomNav?.setBadge) {
            window.CustomerBottomNav.setBadge("messages", unreadConversations);
        }
    }

    function moveCardToTop(card) {
        if (card && elements.contactList && card.parentElement === elements.contactList) {
            elements.contactList.prepend(card);
        }
    }

    function updateIncomingContact(data) {
        const senderId = positiveInteger(data?.senderId);
        const card = findContactCard(senderId, positiveInteger(data?.placeId));
        if (!card) {
            return;
        }

        const preview = card.querySelector(`#contactMsg_${senderId}`) || card.querySelector(".chat-contact-line--message p");
        const time = card.querySelector(`#contactTime_${senderId}`) || card.querySelector(".contact-time-text");

        if (preview) {
            preview.textContent = cleanText(data.text) || (data.attachmentUrl ? "📎 مرفق" : "رسالة جديدة");
        }
        if (time) {
            time.textContent = "الآن";
            time.setAttribute("datetime", cleanText(data.sendTimeRaw) || new Date().toISOString());
        }

        card.dataset.hasConversation = "true";
        card.dataset.contactSource = "conversation";
        moveCardToTop(card);
    }

    /* =========================================================================
       9. SignalR
       ========================================================================= */

    function startSignalR() {
        if (typeof window.signalR === "undefined") {
            showSystemNotice("التحديث اللحظي غير متاح، ويمكنك الاستمرار باستخدام الدردشة.", "warning", 5000);
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/chatHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        state.connection = connection;

        connection.on("ReceiveMessage", data => void handleReceiveMessage(data));
        connection.on("MessageEdited", data => handleRemoteEdit(data));
        connection.on("MessageDeleted", data => handleRemoteDelete(data));
        connection.on("MessagesRead", data => handleMessagesRead(data));
        connection.on("UserIsTyping", (senderId, isTyping, placeId) => handleRemoteTyping(senderId, isTyping, placeId));
        connection.on("UserStatusChanged", (userId, isOnline, lastSeen) => handleUserStatus(userId, isOnline, lastSeen));

        connection.onreconnecting(() => {
            state.connectionStarted = false;
            showSystemNotice("انقطع الاتصال اللحظي، جارٍ إعادة الاتصال...", "warning", 0);
        });

        connection.onreconnected(async () => {
            state.connectionStarted = true;
            showSystemNotice("عاد الاتصال اللحظي بنجاح.", "success", 2500);
            await joinActiveConversationGroup();
            if (state.activeCustomerId) {
                await reloadActiveConversation({ scrollToBottom: true });
            }
        });

        connection.onclose(() => {
            state.connectionStarted = false;
            showSystemNotice("توقف الاتصال اللحظي. أعد تحميل الصفحة إذا استمر الانقطاع.", "error", 0);
        });

        void connectSignalR();
    }

    async function connectSignalR() {
        if (!state.connection || state.connection.state !== signalR.HubConnectionState.Disconnected) {
            return;
        }

        try {
            await state.connection.start();
            state.connectionStarted = true;
            await joinActiveConversationGroup();
        } catch (error) {
            console.warn("SignalR start failed:", error);
            window.setTimeout(connectSignalR, 5000);
        }
    }

    async function joinActiveConversationGroup() {
        if (!state.connectionStarted || !state.activeCustomerId || !state.activePlaceId) {
            return;
        }

        try {
            const joined = await state.connection.invoke(
                "JoinConversation",
                state.activePlaceId,
                state.activeCustomerId
            );

            if (joined === false) {
                showSystemNotice("تعذر الانضمام إلى قناة المحادثة اللحظية.", "warning", 3500);
            }
        } catch (error) {
            console.warn("JoinConversation failed:", error);
        }
    }

    async function leaveActiveConversationGroup() {
        if (!state.connectionStarted) {
            return;
        }

        try {
            await state.connection.invoke("LeaveConversation");
        } catch {
            // لا نوقف انتقال المستخدم بسبب فشل المغادرة اللحظية.
        }
    }

    async function handleReceiveMessage(data) {
        const incomingMessageId = cleanText(data?.messageId);

        if (incomingMessageId && state.receivedSignalMessageIds.has(incomingMessageId)) {
            return;
        }

        if (incomingMessageId) {
            state.receivedSignalMessageIds.add(incomingMessageId);
            window.setTimeout(() => state.receivedSignalMessageIds.delete(incomingMessageId), 60000);
        }

        const incomingPlaceId = positiveInteger(data?.placeId);
        const senderId = positiveInteger(data?.senderId);
        const targetCard = findContactCard(senderId, incomingPlaceId);

        updateIncomingContact(data);

        const isActiveConversation =
            senderId === state.activeCustomerId &&
            incomingPlaceId === state.activePlaceId;

        if (
            isActiveConversation &&
            (shell.dataset.mobileView !== "contacts" || !isMobileLayout())
        ) {
            await reloadActiveConversation({ scrollToBottom: true });
            return;
        }

        incrementCardUnread(targetCard);
        showSystemNotice(
            `رسالة جديدة من ${targetCard?.dataset.placeName || "إحدى المنشآت"}.`,
            "info",
            3500
        );
    }

    function handleRemoteEdit(data) {
        if (positiveInteger(data?.placeId) !== state.activePlaceId) {
            return;
        }

        const row = document.getElementById(`message_${data?.messageId}`);
        if (!row) {
            return;
        }

        const parsed = parseReplyPrefix(data?.text);
        const body = row.querySelector(".chat-message-text");
        if (body) {
            body.innerHTML = textToSafeHtml(parsed.body);
        }
        row.dataset.messageText = parsed.body;

        const footer = row.querySelector(".chat-message-meta");
        if (footer && !footer.querySelector(".message-edited-label")) {
            footer.insertAdjacentHTML("beforeend", '<span class="message-edited-label">معدلة</span>');
        }
    }

    function handleRemoteDelete(data) {
        if (positiveInteger(data?.placeId) !== state.activePlaceId) {
            return;
        }

        const row = document.getElementById(`message_${data?.messageId}`);
        if (row) {
            removeMessageRow(row);
        }
    }

    function handleMessagesRead(data) {
        if (positiveInteger(data?.placeId) !== state.activePlaceId) {
            return;
        }

        const ids = Array.isArray(data?.messageIds) ? data.messageIds : [];
        ids.forEach(id => {
            const row = document.getElementById(`message_${id}`);
            const stateMark = row?.querySelector(".message-read-state");
            if (stateMark) {
                stateMark.classList.remove("is-sent");
                stateMark.classList.add("is-read");
                stateMark.title = "تمت القراءة";
                stateMark.innerHTML = '<i class="bi bi-check2-all"></i>';
            }
        });
    }

    function handleRemoteTyping(senderId, isTyping, placeId) {
        if (
            positiveInteger(senderId) !== state.activeCustomerId ||
            (positiveInteger(placeId) > 0 && positiveInteger(placeId) !== state.activePlaceId) ||
            !elements.activeStatus
        ) {
            return;
        }

        window.clearTimeout(state.remoteTypingTimer);

        if (isTyping) {
            elements.activeStatus.textContent = `${state.activeCustomerName} يكتب الآن...`;
            elements.activeStatus.classList.add("typing-indicator");
            state.remoteTypingTimer = window.setTimeout(updatePresenceHeader, 2500);
        } else {
            updatePresenceHeader();
        }
    }

    function handleUserStatus(userId, isOnline, lastSeen) {
        const id = positiveInteger(userId);
        const online = Boolean(isOnline);

        findContactCardsByOwner(id).forEach(card => {
            card.dataset.isOnline = String(online);
            const dot = card.querySelector(".contact-presence");
            if (dot) {
                dot.classList.toggle("is-online", online);
                dot.classList.toggle("is-offline", !online);
                dot.title = online ? "متصل الآن" : "غير متصل";
            }
        });

        if (id === state.activeCustomerId) {
            state.activeCustomerOnline = online;
            state.activePresenceText = online ? "متصل الآن" : formatLastSeen(lastSeen);
            updatePresenceHeader();
        }
    }

    function notifyTypingFromInput() {
        if (!state.connectionStarted || !state.activeCustomerId || state.editing) {
            return;
        }

        const hasText = Boolean(elements.input?.value.trim());

        if (hasText && !state.typingSent) {
            state.typingSent = true;
            state.connection.invoke("NotifyTyping", state.activeCustomerId, true).catch(() => { });
        }

        window.clearTimeout(state.typingStopTimer);
        state.typingStopTimer = window.setTimeout(() => {
            if (state.typingSent) {
                state.typingSent = false;
                state.connection.invoke("NotifyTyping", state.activeCustomerId, false).catch(() => { });
            }
        }, 1400);

        if (!hasText && state.typingSent) {
            state.typingSent = false;
            state.connection.invoke("NotifyTyping", state.activeCustomerId, false).catch(() => { });
        }
    }

    /* =========================================================================
       10. الهاتف والمسودات
       ========================================================================= */

    function showMobileConversation(pushHistory) {
        shell.dataset.mobileView = "conversation";
        document.body.classList.add("owner-chat-mobile-conversation-open");
        syncMobileChatViewport();

        if (pushHistory && isMobileLayout() && !state.mobileHistoryPushed) {
            history.pushState({ ownerChatConversation: true }, "", window.location.href);
            state.mobileHistoryPushed = true;
        }

        requestAnimationFrame(() => {
            syncMobileChatViewport();
            elements.input?.focus({ preventScroll: true });
            scrollMessagesToBottom();
        });
    }

    function showMobileContacts(resetHistoryFlag = true) {
        saveActiveDraft();
        closeMobileSheets();
        shell.dataset.mobileView = "contacts";
        document.body.classList.remove("owner-chat-mobile-conversation-open");
        if (resetHistoryFlag) state.mobileHistoryPushed = false;
        syncMobileChatViewport();
        elements.searchInput?.focus({ preventScroll: true });
    }

    function returnToContactsFromMobile() {
        if (!isMobileLayout()) {
            return;
        }

        if (state.mobileHistoryPushed) {
            state.mobileHistoryPushed = false;
            history.back();
        } else {
            showMobileContacts();
        }
    }

    function handleResponsiveResize() {
        closeAllMessageMenus({ keepOverlayState: true });
        closeMobileSheets();
        syncMobileChatViewport();

        if (!isMobileLayout()) {
            shell.dataset.mobileView = "contacts";
            document.body.classList.remove("owner-chat-mobile-conversation-open");
            state.mobileHistoryPushed = false;
        }
    }

    function bindMobileViewportSizing() {
        const refresh = () => syncMobileChatViewport();

        window.visualViewport?.addEventListener("resize", refresh, { passive: true });
        window.visualViewport?.addEventListener("scroll", refresh, { passive: true });

        if (window.ResizeObserver) {
            const observer = new ResizeObserver(() => {
                syncMobileChatViewport();
            });

            const container = shell.querySelector(".chat-container");
            if (container) observer.observe(container);
        }

        syncMobileChatViewport();
    }

    function syncMobileChatViewport() {
        if (!isMobileLayout()) {
            shell.style.removeProperty("--owner-chat-mobile-available-height");
            return;
        }

        const container = shell.querySelector(".chat-container");
        if (!container) {
            return;
        }

        const viewport = window.visualViewport;
        const viewportTop = viewport?.offsetTop || 0;
        const viewportHeight = viewport?.height || window.innerHeight;
        const viewportBottom = viewportTop + viewportHeight;
        const bottomNavigation = document.querySelector(".customer-bottom-nav");
        const bottomNavigationTop = bottomNavigation?.getBoundingClientRect().top;
        const usableBottom = Number.isFinite(bottomNavigationTop)
            ? Math.min(viewportBottom, bottomNavigationTop - 8)
            : viewportBottom;
        const rect = container.getBoundingClientRect();
        const top = Math.max(rect.top, viewportTop + 6);
        const available = Math.max(280, Math.min(viewportHeight - 12, usableBottom - top - 6));

        shell.style.setProperty(
            "--owner-chat-mobile-available-height",
            `${Math.floor(available)}px`
        );
    }

    function isMessagesNearBottom(threshold = 90) {
        if (!elements.messagesArea) {
            return false;
        }

        return elements.messagesArea.scrollHeight -
            elements.messagesArea.scrollTop -
            elements.messagesArea.clientHeight <= threshold;
    }

    function draftKey(customerId) {
        return `customerChatDraft_${state.activePlaceId}_${customerId}`;
    }

    function saveActiveDraft() {
        if (!state.activeCustomerId || !elements.input || state.editing) {
            return;
        }

        const value = elements.input.value;
        try {
            if (value.trim()) {
                localStorage.setItem(draftKey(state.activeCustomerId), value);
            } else {
                localStorage.removeItem(draftKey(state.activeCustomerId));
            }
        } catch {
            // التخزين المحلي اختياري.
        }
    }

    function restoreDraft(customerId) {
        if (!elements.input) {
            return;
        }

        try {
            elements.input.value = localStorage.getItem(draftKey(customerId)) || "";
        } catch {
            elements.input.value = "";
        }

        autoResizeInput();
        updateCharacterCounter();
        updateSendButton();
    }

    function clearDraft(customerId) {
        try {
            localStorage.removeItem(draftKey(customerId));
        } catch {
            // لا شيء.
        }
    }

    /* =========================================================================
       11. مسح المحادثة
       ========================================================================= */

    async function clearCurrentChat() {
        if (!state.activeCustomerId) {
            return;
        }

        const confirmed = await confirmAction({
            title: "مسح المحادثة من شاشتك؟",
            text: "ستختفي رسائل هذه المنشأة من شاشتك فقط، ولن تُحذف من الطرف الآخر.",
            confirmText: "مسح المحادثة",
            confirmColor: "#FF5A5F"
        });

        if (!confirmed) {
            return;
        }

        try {
            const result = await postForm(requiredUrl(root.dataset.clearChatUrl), {
                ownerId: state.activeCustomerId,
                placeId: state.activePlaceId
            });

            if (!result.success) {
                throw new Error(result.message || "تعذر مسح المحادثة.");
            }

            state.messages = [];
            state.renderStartIndex = 0;
            renderMessages();

            const preview = state.activeCustomerCard?.querySelector(".chat-contact-line--message p");
            if (preview) preview.textContent = "ابدأ المحادثة الآن";

            alertUser("تم المسح", result.message || "تم مسح المحادثة من شاشتك.", "success");
        } catch (error) {
            alertUser("تعذر المسح", error.message, "error");
        }
    }

    /* =========================================================================
       12. أدوات الواجهة والطلبات
       ========================================================================= */

    function setComposerEnabled(enabled, hint) {
        const usable = Boolean(enabled && state.activeCustomerId && state.activeCustomerCanStart);

        if (elements.input) {
            elements.input.disabled = !usable;
            elements.input.placeholder = usable
                ? "اكتب رسالتك هنا..."
                : state.chatEnabled
                    ? "اختر منشأة متاحة للمراسلة"
                    : "الدردشة متوقفة لهذه المنشأة";
        }

        [elements.emojiToggle, elements.attachmentToggle].forEach(button => {
            if (!button) return;
            button.disabled = !usable;
            button.setAttribute("aria-disabled", usable ? "false" : "true");
        });

        if (elements.composerHint && hint) {
            elements.composerHint.textContent = hint;
        }

        updateSendButton();
    }

    function composerIsUsable() {
        return Boolean(
            state.activeCustomerId &&
            state.activeCustomerCanStart &&
            state.chatEnabled &&
            elements.input &&
            !elements.input.disabled
        );
    }

    function updateSendButton() {
        if (!elements.send || !elements.input) {
            return;
        }

        const hasText = Boolean(elements.input.value.trim());
        const hasFiles = state.selectedFiles.length > 0;
        const enabled = composerIsUsable() &&
            !state.isSending &&
            (state.editing ? hasText : (hasText || hasFiles));

        elements.send.classList.toggle("disabled", !enabled);
        elements.send.disabled = !enabled;
        elements.send.setAttribute("aria-disabled", enabled ? "false" : "true");
        elements.send.title = state.editing ? "حفظ التعديل" : "إرسال الرسالة";

        if (elements.sendIcon && !state.isSending) {
            elements.sendIcon.className = state.editing
                ? "bi bi-check2-circle"
                : "bi bi-send-fill";
        }
    }

    function updateCharacterCounter() {
        if (!elements.input || !elements.characterCounter) {
            return;
        }

        const length = elements.input.value.length;
        elements.characterCounter.textContent = `${length} / ${limits.messageLength}`;
        elements.characterCounter.classList.toggle("is-near-limit", length >= limits.messageLength * 0.9);
    }

    function autoResizeInput() {
        if (!elements.input) {
            return;
        }

        const keepBottom = isMessagesNearBottom();
        elements.input.style.height = "auto";
        const lineHeight = parseFloat(getComputedStyle(elements.input).lineHeight) || 24;
        const maxHeight = lineHeight * 4 + 24;
        elements.input.style.height = `${Math.min(elements.input.scrollHeight, maxHeight)}px`;
        elements.input.style.overflowY = elements.input.scrollHeight > maxHeight ? "auto" : "hidden";

        syncMobileChatViewport();

        if (keepBottom) {
            requestAnimationFrame(scrollMessagesToBottom);
        }
    }

    function showSystemNotice(message, type = "info", duration = 3500) {
        if (!elements.systemNotice || !elements.systemNoticeText) {
            return;
        }

        window.clearTimeout(state.noticeTimer);
        elements.systemNoticeText.textContent = message;
        elements.systemNotice.className = `chat-system-notice is-${type}`;
        elements.systemNotice.style.display = "flex";

        if (duration > 0) {
            state.noticeTimer = window.setTimeout(() => {
                elements.systemNotice.style.display = "none";
            }, duration);
        }
    }

    async function postForm(url, values) {
        const formData = new FormData();
        Object.entries(values).forEach(([key, value]) => formData.append(key, String(value ?? "")));
        appendAntiForgery(formData);

        return requestJson(url, {
            method: "POST",
            body: formData,
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
    }

    async function requestJson(url, options) {
        const response = await fetch(url, {
            credentials: "same-origin",
            ...options
        });

        const contentType = response.headers.get("content-type") || "";
        const bodyText = await response.text();
        let data;

        if (contentType.includes("application/json") || bodyText.trim().startsWith("{")) {
            try {
                data = JSON.parse(bodyText);
            } catch {
                throw new Error("استجابة الخادم غير صالحة.");
            }
        } else {
            throw new Error(response.status === 401 || response.status === 403
                ? "انتهت الجلسة أو لا تملك الصلاحية."
                : "تعذر الاتصال بالخادم.");
        }

        if (!response.ok) {
            throw new Error(data?.message || "فشل الطلب.");
        }

        return data;
    }

    function appendAntiForgery(formData) {
        const token = document.querySelector("#ownerChatAntiForgeryForm input[name='__RequestVerificationToken']")?.value;
        if (token) {
            formData.append("__RequestVerificationToken", token);
        }
    }

    function requiredUrl(value) {
        const url = cleanText(value);
        if (!url) {
            throw new Error("رابط العملية غير موجود في الصفحة.");
        }
        return url;
    }

    async function confirmAction(config) {
        if (typeof Swal === "undefined") {
            return window.confirm(`${config.title}\n${config.text || ""}`);
        }

        const result = await Swal.fire({
            title: config.title,
            text: config.text || "",
            icon: config.icon || "warning",
            showCancelButton: true,
            confirmButtonText: config.confirmText || "تأكيد",
            cancelButtonText: "إلغاء",
            confirmButtonColor: config.confirmColor || "#00A6DA",
            cancelButtonColor: "#94A3B8",
            reverseButtons: true,
            customClass: { popup: "owner-chat-swal" }
        });

        return result.isConfirmed;
    }

    function alertUser(title, text, icon = "info") {
        if (typeof Swal !== "undefined") {
            return Swal.fire({
                title,
                text,
                icon,
                confirmButtonText: "حسنًا",
                confirmButtonColor: icon === "error" ? "#FF5A5F" : "#00A6DA",
                customClass: { popup: "owner-chat-swal" }
            });
        }

        window.alert(`${title}\n${text}`);
        return Promise.resolve();
    }

    function openProfileModal(name, profileImage, initialLetter) {
        if (!elements.modalContainer || !elements.modalName) {
            return;
        }

        const safeName = cleanText(name) || "مالك";
        const profile = cleanText(profileImage);
        const initial = cleanText(initialLetter) || safeName.charAt(0) || "ع";

        elements.modalName.textContent = safeName;
        elements.modalContainer.innerHTML = profile
            ? `<img src="${escapeAttribute(profile)}" alt="صورة ${escapeAttribute(safeName)}" />`
            : `<span>${escapeHtml(initial)}</span>`;

        const modalElement = document.getElementById("avatarZoomModal");
        if (modalElement && window.bootstrap?.Modal) {
            bootstrap.Modal.getOrCreateInstance(modalElement).show();
        }
    }

    function renderAvatarButton(button, name, profileImage, initialLetter) {
        if (!button) {
            return;
        }

        button.dataset.name = name;
        button.dataset.content = profileImage || initialLetter;
        button.dataset.type = profileImage ? "image" : "text";
        button.setAttribute("aria-disabled", "false");
        button.disabled = false;
        button.innerHTML = profileImage
            ? `<img src="${escapeAttribute(profileImage)}" alt="صورة ${escapeAttribute(name)}" />`
            : `<span>${escapeHtml(initialLetter)}</span>`;

        button.onclick = event => {
            event.preventDefault();
            openProfileModal(name, profileImage, initialLetter);
        };
    }

    function syncMobileOverlay() {
        syncSharedMobileOverlay();
    }

    function syncSharedMobileOverlay() {
        // لوحات الأدوات أصبحت منسدلات صغيرة داخل المحادثة؛ لا نستخدم تعتيمًا
        // ولا Blur ولا قفلًا لتمرير الصفحة.
        if (elements.sheetOverlay) {
            elements.sheetOverlay.hidden = true;
            elements.sheetOverlay.classList.remove("is-visible");
        }

        document.body.classList.remove("owner-chat-sheet-open");
    }

    function closeMobileSheets() {
        [elements.emojiToggle, elements.attachmentToggle].forEach(button => {
            if (!button) {
                return;
            }

            const panel = button.closest(".chat-tool-dropdown")?.querySelector(".dropdown-menu");
            panel?.classList.remove("show");
            panel?.setAttribute("hidden", "");
            button.setAttribute("aria-expanded", "false");
        });

        syncSharedMobileOverlay();
    }

    function scrollMessagesToBottom() {
        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                if (elements.messagesArea) {
                    elements.messagesArea.scrollTop = elements.messagesArea.scrollHeight;
                }
            });
        });
    }

    /* =========================================================================
       13. أدوات القيم والتنسيق
       ========================================================================= */

    function getContactCards() {
        return Array.from(document.querySelectorAll(".chat-contact-card"));
    }

    function findContactCard(ownerId, placeId = 0) {
        const id = positiveInteger(ownerId);
        const resolvedPlaceId = positiveInteger(placeId);

        if (!id) {
            return null;
        }

        if (resolvedPlaceId) {
            const exact = document.querySelector(
                `.chat-contact-card[data-owner-id="${id}"][data-place-id="${resolvedPlaceId}"]`
            );

            if (exact) {
                return exact;
            }
        }

        return document.querySelector(
            `.chat-contact-card[data-owner-id="${id}"], ` +
            `.chat-contact-card[data-customer-id="${id}"], ` +
            `.chat-contact-card[data-id="${id}"]`
        );
    }

    function findContactCardsByOwner(ownerId) {
        const id = positiveInteger(ownerId);
        return id
            ? Array.from(document.querySelectorAll(
                `.chat-contact-card[data-owner-id="${id}"], ` +
                `.chat-contact-card[data-customer-id="${id}"]`
            ))
            : [];
    }

    function openRequestedConversation() {
        const requestedOwnerId = positiveInteger(root.dataset.openOwnerId);
        const requestedPlaceId = positiveInteger(root.dataset.openPlaceId);

        if (!requestedOwnerId && !requestedPlaceId) {
            return;
        }

        const card = requestedOwnerId
            ? findContactCard(requestedOwnerId, requestedPlaceId)
            : getContactCards().find(item => positiveInteger(item.dataset.placeId) === requestedPlaceId);

        if (!card || card.classList.contains("is-unavailable")) {
            return;
        }

        window.setTimeout(() => {
            void openContact(
                card,
                positiveInteger(card.dataset.ownerId || card.dataset.id),
                card.dataset.name,
                card.dataset.profile,
                card.dataset.initial
            );
        }, 0);
    }

    function positiveInteger(value) {
        const parsed = Number.parseInt(String(value ?? ""), 10);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
    }

    function toBoolean(value) {
        return value === true || String(value).toLowerCase() === "true" || String(value) === "1";
    }

    function cleanText(value) {
        if (value === null || value === undefined || value === "null" || value === "undefined") {
            return "";
        }
        return String(value).trim();
    }

    function cleanTextPreserveLines(value) {
        if (value === null || value === undefined) {
            return "";
        }
        return String(value).replace(/\r\n/g, "\n").replace(/\r/g, "\n");
    }

    function normalizeForSearch(value) {
        return cleanText(value)
            .toLocaleLowerCase("ar")
            .normalize("NFKD")
            .replace(/[\u064B-\u065F\u0670]/g, "")
            .replace(/[أإآ]/g, "ا")
            .replace(/ى/g, "ي")
            .replace(/ة/g, "ه");
    }

    function normalizeQuickReplies(value) {
        let source = value;

        if (typeof source === "string") {
            const trimmed = source.trim();
            if (!trimmed) return [];
            try {
                source = JSON.parse(trimmed);
            } catch {
                return [];
            }
        }

        if (!Array.isArray(source)) {
            return [];
        }

        const topics = [];
        const seen = new Set();

        source.forEach(item => {
            if (!item || typeof item !== "object") return;

            const type = cleanText(item.type ?? item.Type).toLowerCase();
            const key = cleanText(
                item.key ?? item.Key ?? item.topicKey ?? item.TopicKey
            ).toLowerCase();

            if (type !== "customer-topic" && !key) return;

            const title = cleanText(item.title ?? item.Title);
            const question = cleanText(
                item.question ?? item.Question ?? item.text ?? item.Text
            );

            if (!key || !title || !question || seen.has(key)) return;

            seen.add(key);
            topics.push({ key, title, question });
        });

        return topics.slice(0, 10);
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

    function textToSafeHtml(value) {
        const escaped = escapeHtml(value);

        // روابط الموقع التي يولدها النظام فقط: نظهرها كرابط واضح قابل للفتح
        // مع إبقاء بقية نصوص المحادثة escaped كما كانت دون السماح بـ HTML خام.
        const withMapLinks = escaped.replace(
            /https:\/\/www\.google\.com\/maps\/search\/\?api=1&amp;query=[^\s<]+/g,
            url => `<a href="${url}" target="_blank" rel="noopener noreferrer" class="chat-location-link">فتح الموقع على الخريطة</a>`
        );

        return withMapLinks.replace(/\n/g, "<br>");
    }

    function insertAtCursor(input, text) {
        if (!input) return;
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? start;
        input.value = input.value.slice(0, start) + text + input.value.slice(end);
        const position = start + text.length;
        input.setSelectionRange(position, position);
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.focus();
    }

    function fileExtension(name) {
        const fileName = cleanText(name).split("?")[0];
        const parts = fileName.split(".");
        return parts.length > 1 ? parts.pop() : "";
    }

    function fileNameFromUrl(url) {
        try {
            const name = decodeURIComponent(cleanText(url).split("/").pop().split("?")[0]);
            const underscore = name.indexOf("_");
            return underscore >= 0 ? name.slice(underscore + 1) : name;
        } catch {
            return "مرفق";
        }
    }

    function fileIcon(name) {
        const extension = fileExtension(name).toLowerCase();
        if (extension === "pdf") return "bi-file-earmark-pdf-fill";
        if (["doc", "docx"].includes(extension)) return "bi-file-earmark-word-fill";
        if (["xls", "xlsx"].includes(extension)) return "bi-file-earmark-excel-fill";
        if (["zip", "rar"].includes(extension)) return "bi-file-earmark-zip-fill";
        if (["mp4", "webm", "mov"].includes(extension)) return "bi-file-earmark-play-fill";
        return "bi-file-earmark-fill";
    }

    function fileToken(file) {
        return `${file.name}_${file.size}_${file.lastModified}_${Math.random().toString(36).slice(2)}`;
    }

    function formatBytes(bytes) {
        const value = Number(bytes) || 0;
        if (value >= 1024 * 1024) return `${(value / (1024 * 1024)).toFixed(2)} MB`;
        return `${Math.max(0.01, value / 1024).toFixed(2)} KB`;
    }

    function normalizeState(value) {
        return cleanText(value).toLowerCase().replace(/\s+/g, "");
    }

    function bookingStateLabel(value) {
        const labels = {
            pending: "بانتظار المراجعة",
            awaitingpayment: "بانتظار الدفع",
            paymentreview: "مراجعة إثبات الدفع",
            confirmed: "مؤكد",
            active: "نشط",
            completed: "مكتمل",
            rejected: "مرفوض",
            cancelled: "ملغي",
            canceled: "ملغي",
            expired: "منتهي",
            noshow: "عدم حضور"
        };
        const normalized = normalizeState(value);
        return labels[normalized] || cleanText(value) || "غير محدد";
    }

    function parseDate(value) {
        const text = cleanText(value);
        if (!text) return null;
        const date = new Date(text);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    function formatCompactDate(value) {
        const date = parseDate(value);
        if (!date) return "";
        return new Intl.DateTimeFormat("ar-YE", {
            day: "numeric",
            month: "short"
        }).format(date);
    }

    function formatBookingPeriod(start, end) {
        const startDate = parseDate(start);
        const endDate = parseDate(end);
        if (!startDate && !endDate) return "غير محدد";

        const formatter = new Intl.DateTimeFormat("ar-YE", {
            year: "numeric",
            month: "short",
            day: "numeric"
        });

        if (startDate && endDate) return `${formatter.format(startDate)} — ${formatter.format(endDate)}`;
        return formatter.format(startDate || endDate);
    }

    function formatClock(value) {
        const date = parseDate(value) || new Date();
        return new Intl.DateTimeFormat("ar-YE", {
            hour: "numeric",
            minute: "2-digit"
        }).format(date);
    }

    function formatLastSeen(value) {
        const date = parseDate(value);
        if (!date) return "غير متصل";
        const difference = Date.now() - date.getTime();
        const minutes = Math.floor(difference / 60000);
        if (minutes < 1) return "آخر ظهور الآن";
        if (minutes < 60) return `آخر ظهور منذ ${minutes} دقيقة`;
        const hours = Math.floor(minutes / 60);
        if (hours < 24) return `آخر ظهور منذ ${hours} ساعة`;
        const days = Math.floor(hours / 24);
        if (days < 7) return `آخر ظهور منذ ${days} يوم`;
        return `آخر ظهور ${new Intl.DateTimeFormat("ar-YE", { dateStyle: "medium" }).format(date)}`;
    }

    function withinMinutes(value, minutes) {
        const date = parseDate(value);
        return !date || (Date.now() - date.getTime()) <= minutes * 60 * 1000;
    }

    function withinHours(value, hours) {
        const date = parseDate(value);
        return !date || (Date.now() - date.getTime()) <= hours * 60 * 60 * 1000;
    }

    function cssEscape(value) {
        const text = String(value ?? "");
        return window.CSS?.escape
            ? window.CSS.escape(text)
            : text.replace(/(["\\])/g, "\\$1");
    }

    function isMobileLayout() {
        return window.matchMedia("(max-width: 767.98px)").matches;
    }
})();
