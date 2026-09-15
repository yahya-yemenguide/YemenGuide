/* ========================================================================== 
   YemenGuide Owner Calendar V6 — Mobile First Luxury Calendar + Stable Dialog Portal
   FullCalendar local library + direct/manual booking studio
   ========================================================================== */
(function () {
    "use strict";

    const ENDPOINTS = Object.freeze({
        context: "/Owner/Reservation/GetCalendarContext",
        events: "/Owner/Reservation/GetCalendarEventsV2",
        day: "/Owner/Reservation/GetCalendarDayDetails",
        customer: "/Owner/Reservation/SearchManualBookingCustomer",
        options: "/Owner/Reservation/GetManualBookingOptions",
        preview: "/Owner/Reservation/PreviewManualBooking",
        create: "/Owner/Reservation/CreateManualBooking",
        payment: "/Owner/Reservation/RecordManualBookingPayment",
        closeRange: "/Owner/Reservation/CloseCalendarRange",
        reopenRange: "/Owner/Reservation/ReopenCalendarRange"
    });

    const stateLabels = Object.freeze({
        Pending: "طلب جديد",
        AwaitingPayment: "بانتظار الدفع",
        PaymentReview: "مراجعة الدفع",
        Confirmed: "مؤكد",
        Completed: "مكتمل",
        Cancelled: "ملغي",
        Rejected: "مرفوض",
        NoShow: "عدم حضور",
        Expired: "منتهي"
    });

    const paymentLabels = Object.freeze({
        Unpaid: "غير مدفوع",
        PartiallyPaid: "مدفوع جزئيًا",
        Paid: "مدفوع",
        UnderReview: "قيد المراجعة",
        Expired: "انتهت المهلة"
    });


    const refundLabels = Object.freeze({
        NotApplicable: "لا يوجد استرداد",
        Pending: "استرداد قيد المعالجة",
        RefundPending: "استرداد قيد المعالجة",
        Completed: "تم الاسترداد",
        Rejected: "رفض الاسترداد"
    });


    const historicalBookingStates = new Set([
        "Completed",
        "Cancelled",
        "Rejected",
        "NoShow",
        "Expired"
    ]);

    let activeInstance = null;

    function initPremiumCalendar() {
        const root = document.getElementById("ygCalendarManager");
        const calendarEl = document.getElementById("premiumCalendar");

        if (!root || !calendarEl || typeof window.FullCalendar === "undefined") {
            return;
        }

        if (activeInstance && typeof activeInstance.dispose === "function") {
            activeInstance.dispose();
        }

        activeInstance = createCalendarInstance(root, calendarEl);
        activeInstance.init();
    }

    function createCalendarInstance(root, calendarEl) {
        const listeners = [];
        const abortController = new AbortController();
        let calendar = null;
        let context = null;
        let selectedDate = formatLocalDate(new Date());
        let manualOptions = null;
        let manualResourceKind = "room";
        let linkedCustomer = null;
        let quoteTimer = 0;
        let customerTimer = 0;
        let rangeMode = "close";
        let disposed = false;
        let phoneSearchIti = null;
        let guestPhoneIti = null;
        let manualStep = 1;
        let activeRoomFloorKey = "";
        let selectedRoomUnitIds = new Set();
        let portal = null;
        let lastLoadedEvents = [];
        let mobileAgendaRequestId = 0;
        let monthPickerYear = new Date().getFullYear();
        let mobileViewMode = "month";
        let calendarScope = "active";
        let countdownInterval = 0;
        let paymentRequestInFlight = false;
        let latestManualQuote = null;
        let lockedScrollY = null;
        let previousBodyPaddingRight = "";
        let previousBodyPosition = "";
        let previousBodyTop = "";
        let previousBodyWidth = "";
        let previousBodyOverflow = "";

        const $ = (selector) =>
            root.querySelector(selector) ||
            portal?.querySelector(selector) ||
            null;

        const $$ = (selector, container = null) => {
            if (container) return Array.from(container.querySelectorAll(selector));
            const insideRoot = Array.from(root.querySelectorAll(selector));
            const insidePortal = portal ? Array.from(portal.querySelectorAll(selector)) : [];
            return [...insideRoot, ...insidePortal];
        };

        const elements = {
            loading: $("#ygCalendarLoading"),
            placeName: $("#ygActivePlaceName"),
            placeCategory: $("#ygActivePlaceCategory"),
            heroDay: $("#ygHeroTodayDay"),
            sync: $("#btnSyncCalendar"),
            today: $("#btnCalendarToday"),
            openManual: $("#btnOpenManualBooking"),
            closeRange: $("#btnCloseCalendarRange"),
            reopenRange: $("#btnReopenCalendarRange"),
            calPrev: $("#ygCalendarPrev"),
            calNext: $("#ygCalendarNext"),
            calTodayInline: $("#ygCalendarTodayInline"),
            calPeriodEyebrow: $("#ygCalendarPeriodEyebrow"),
            calPeriodTitle: $("#ygCalendarPeriodTitle"),
            calPeriodSubtitle: $("#ygCalendarPeriodSubtitle"),
            mobileCalendar: $("#ygMobileCalendar"),
            mobilePrev: $("#ygMobilePrev"),
            mobileNext: $("#ygMobileNext"),
            mobileToday: $("#ygMobileToday"),
            mobileMonthPickerBtn: $("#ygMobileMonthPickerBtn"),
            mobileMonthLabel: $("#ygMobileMonthLabel"),
            mobileModeLabel: $("#ygMobileModeLabel"),
            mobileMonthGrid: $("#ygMobileMonthGrid"),
            mobileAgenda: $("#ygMobileAgenda"),

            dayOverlay: $("#ygDayOverlay"),
            daySheet: $("#ygDaySheet"),
            dayTitle: $("#ygDaySheetTitle"),
            daySubtitle: $("#ygDaySheetSubtitle"),
            dayState: $("#ygDayStateBanner"),
            dayBookingsCount: $("#ygDayBookingsCount"),
            dayOccupied: $("#ygDayOccupied"),
            dayAvailable: $("#ygDayAvailable"),
            dayOccupancy: $("#ygDayOccupancy"),
            dayListCount: $("#ygDayListCount"),
            dayBookingsList: $("#ygDayBookingsList"),
            dayCreate: $("#ygDayCreateManual"),
            dayClose: $("#ygDayClose"),
            dayReopen: $("#ygDayReopen"),

            manualOverlay: $("#ygManualOverlay"),
            manualModal: $("#ygManualBookingModal"),
            manualFacilityName: $("#ygManualFacilityName"),
            manualFacilityTemplate: $("#ygManualFacilityTemplate"),
            manualFormAlert: $("#ygManualFormAlert"),
            manualFormAlertText: $("#ygManualFormAlertText"),
            bookingTemplateTitle: $("#ygBookingTemplateTitle"),
            bookingTemplateDescription: $("#ygBookingTemplateDescription"),
            manualTemplateBanner: $("#ygManualTemplateBanner"),
            manualTemplateIcon: $("#ygManualTemplateIcon"),
            manualTemplateName: $("#ygManualTemplateName"),
            manualTemplateCategory: $("#ygManualTemplateCategory"),
            startDateLabel: $("#ygStartDateLabel"),
            endDateLabel: $("#ygEndDateLabel"),
            phoneSearch: $("#ygManualPhoneSearch"),
            phoneSearchFeedback: $("#ygPhoneSearchFeedback"),
            customerSearchLoader: $("#ygCustomerSearchLoader"),
            customerId: $("#ygManualCustomerId"),
            linkedCustomer: $("#ygLinkedCustomer"),
            linkedCustomerAvatar: $("#ygLinkedCustomerAvatar"),
            linkedCustomerName: $("#ygLinkedCustomerName"),
            linkedCustomerPhone: $("#ygLinkedCustomerPhone"),
            unlinkCustomer: $("#ygUnlinkCustomer"),
            guestPanel: $("#ygGuestPanel"),
            guestName: $("#ygGuestName"),
            guestPhone: $("#ygGuestPhone"),
            guestPhoneFeedback: $("#ygGuestPhoneFeedback"),
            guestEmail: $("#ygGuestEmail"),
            manualChannelOtherField: $("#ygManualChannelOtherField"),
            manualChannelOther: $("#ygManualChannelOther"),
            startDate: $("#ygManualStartDate"),
            endDate: $("#ygManualEndDate"),
            endDateField: $("#ygEndDateField"),
            roomPanel: $("#ygRoomResourcePanel"),
            packagePanel: $("#ygPackageResourcePanel"),
            roomId: $("#ygManualRoomId"),
            packageId: $("#ygManualPackageId"),
            quantity: $("#ygManualQuantity"),
            adults: $("#ygManualAdults"),
            children: $("#ygManualChildren"),
            roomHint: $("#ygRoomAvailabilityHint"),
            roomUnitPicker: $("#ygRoomUnitPicker"),
            roomUnitChoices: $("#ygRoomUnitChoices"),
            roomUnitCounter: $("#ygRoomUnitCounter"),
            roomFloorTabs: $("#ygRoomFloorTabs"),
            packageCards: $("#ygPackageCards"),
            packageHint: $("#ygPackageAvailabilityHint"),
            optionsStatus: $("#ygOptionsStatus"),
            services: $("#ygManualServices"),
            promoCode: $("#ygManualPromoCode"),
            seating: $("#ygManualSeating"),
            seatingOtherField: $("#ygSeatingOtherField"),
            seatingOther: $("#ygManualSeatingOther"),
            venueSeatingField: $("#ygVenueSeatingField"),
            roomSelectTrigger: $("#ygRoomSelectTrigger"),
            roomSelectText: $("#ygRoomSelectText"),
            roomSelectMenu: $("#ygRoomSelectMenu"),
            seatingSelectTrigger: $("#ygSeatingSelectTrigger"),
            seatingSelectText: $("#ygSeatingSelectText"),
            seatingSelectMenu: $("#ygSeatingSelectMenu"),
            notes: $("#ygManualNotes"),
            collectedFields: $("#ygCollectedPaymentFields"),
            paidAmount: $("#ygManualPaidAmount"),
            paymentMethod: $("#ygManualPaymentMethod"),
            paymentMethodOtherField: $("#ygPaymentMethodOtherField"),
            paymentMethodOther: $("#ygManualPaymentMethodOther"),
            paymentSelectTrigger: $("#ygPaymentSelectTrigger"),
            paymentSelectText: $("#ygPaymentSelectText"),
            paymentSelectMenu: $("#ygPaymentSelectMenu"),
            ownerWalletField: $("#ygOwnerWalletField"),
            ownerWalletId: $("#ygManualOwnerWalletId"),
            ownerWalletSelectTrigger: $("#ygOwnerWalletSelectTrigger"),
            ownerWalletSelectText: $("#ygOwnerWalletSelectText"),
            ownerWalletSelectMenu: $("#ygOwnerWalletSelectMenu"),
            paymentReference: $("#ygManualPaymentReference"),
            preview: $("#ygPreviewManualBooking"),
            create: $("#ygCreateManualBooking"),
            searchCustomer: $("#ygSearchCustomerBtn"),
            stepBack: $("#ygManualStepBack"),
            stepNext: $("#ygManualStepNext"),
            mobileStepCounter: $("#ygMobileStepCounter"),
            mobileStepTitle: $("#ygMobileStepTitle"),
            mobileStepProgress: $("#ygMobileStepProgress"),

            quotePlaceholder: $("#ygQuotePlaceholder"),
            quoteContent: $("#ygQuoteContent"),
            quoteCustomer: $("#ygQuoteCustomer"),
            quoteBase: $("#ygQuoteBase"),
            quoteServices: $("#ygQuoteServices"),
            quoteCleaning: $("#ygQuoteCleaning"),
            quoteDiscountRow: $("#ygQuoteDiscountRow"),
            quoteDiscount: $("#ygQuoteDiscount"),
            quoteTaxPercent: $("#ygQuoteTaxPercent"),
            quoteTax: $("#ygQuoteTax"),
            quoteTotal: $("#ygQuoteTotal"),
            quoteDeposit: $("#ygQuoteDeposit"),
            quoteRemaining: $("#ygQuoteRemaining"),
            quoteState: $("#ygQuoteState"),
            quoteUnits: $("#ygQuoteUnits"),
            quotePromo: $("#ygQuotePromo"),

            rangeOverlay: $("#ygRangeOverlay"),
            rangeModal: $("#ygRangeModal"),
            rangeIcon: $("#ygRangeIcon"),
            rangeKicker: $("#ygRangeKicker"),
            rangeTitle: $("#ygRangeTitle"),
            rangeDescription: $("#ygRangeDescription"),
            rangeReasonField: $("#ygRangeReasonField"),
            rangeStart: $("#ygRangeStart"),
            rangeEnd: $("#ygRangeEnd"),
            rangeReason: $("#ygRangeReason"),
            rangeNotice: $("#ygRangeNotice"),
            rangeSubmit: $("#ygRangeSubmit"),

            monthPickerOverlay: $("#ygMonthPickerOverlay"),
            monthPickerModal: $("#ygMonthPickerModal"),
            monthPickerPrevYear: $("#ygMonthPickerPrevYear"),
            monthPickerNextYear: $("#ygMonthPickerNextYear"),
            monthPickerYear: $("#ygMonthPickerYear"),
            monthPickerMonths: $("#ygMonthPickerMonths"),

            toasts: $("#ygCalendarToasts")
        };

        function init() {
            root.dataset.initialized = "true";
            elements.heroDay.textContent = String(new Date().getDate());
            mountDialogPortal();
            bindUi();
            syncScopeButtons();
            initializeConstrainedInputs();
            initializePhoneInputs();
            createFullCalendar();
            countdownInterval = window.setInterval(updatePaymentCountdowns, 1000);
            refreshContextForCurrentRange().catch(handleError);
        }

        function dispose() {
            disposed = true;
            window.clearTimeout(quoteTimer);
            window.clearTimeout(customerTimer);
            if (countdownInterval) window.clearInterval(countdownInterval);
            countdownInterval = 0;
            abortController.abort();
            listeners.forEach(({ target, type, handler, options }) => {
                try { target.removeEventListener(type, handler, options); } catch (_) { }
            });
            listeners.length = 0;
            if (calendar) {
                try { calendar.destroy(); } catch (_) { }
                calendar = null;
            }
            try { phoneSearchIti?.destroy(); } catch (_) { }
            try { guestPhoneIti?.destroy(); } catch (_) { }
            phoneSearchIti = null;
            guestPhoneIti = null;
            closeAllPanels(true);
            restoreDialogPortal();
            unlockPageScroll(true);
        }

        function on(target, type, handler, options) {
            if (!target) return;
            target.addEventListener(type, handler, options);
            listeners.push({ target, type, handler, options });
        }

        function bindUi() {
            on(calendarEl, "click", event => {
                const monthTitle = event.target.closest(".fc-multimonth-title");
                if (!monthTitle || !calendar || calendar.view?.type !== "multiMonthYear") return;

                const month = monthTitle.closest(".fc-multimonth-month");
                const dayCell = month?.querySelector('.fc-daygrid-day[data-date]:not(.fc-day-other)') ||
                    month?.querySelector('.fc-daygrid-day[data-date]');
                const targetDate = dayCell?.getAttribute("data-date");
                if (!targetDate) return;

                event.preventDefault();
                calendar.gotoDate(targetDate);
                calendar.changeView("dayGridMonth");
            });

            on(calendarEl, "keydown", event => {
                if (event.key !== "Enter" && event.key !== " ") return;
                const monthTitle = event.target.closest(".fc-multimonth-title");
                if (!monthTitle || !calendar || calendar.view?.type !== "multiMonthYear") return;
                event.preventDefault();
                monthTitle.click();
            });

            on(elements.sync, "click", async () => {
                setSyncing(true);
                try {
                    await refreshAll();
                    toast("success", "تم التحديث", "تم جلب التوفر والحجوزات من قاعدة البيانات.");
                } catch (error) {
                    handleError(error);
                } finally {
                    setSyncing(false);
                }
            });

            on(elements.today, "click", () => {
                calendar?.today();
                selectedDate = formatLocalDate(new Date());
                updateCalendarToolbar();
                renderMobileCalendar();
                if (window.matchMedia("(max-width: 767.98px)").matches) {
                    loadMobileAgenda(selectedDate).catch(handleError);
                } else {
                    openDayDetails(selectedDate);
                }
            });

            on(elements.calPrev, "click", () => {
                calendar?.prev();
                updateCalendarToolbar();
            });
            on(elements.calNext, "click", () => {
                calendar?.next();
                updateCalendarToolbar();
            });
            on(elements.calTodayInline, "click", () => {
                calendar?.today();
                selectedDate = formatLocalDate(new Date());
                updateCalendarToolbar();
                renderMobileCalendar();
                loadMobileAgenda(selectedDate).catch(handleError);
            });

            on(elements.mobilePrev, "click", () => navigateMobileCalendar(-1));
            on(elements.mobileNext, "click", () => navigateMobileCalendar(1));
            on(elements.mobileToday, "click", () => goMobileToday());

            $$('[data-mobile-calendar-view]').forEach(button => {
                on(button, "click", () => {
                    const mode = button.dataset.mobileCalendarView;
                    if (!["week", "month", "year"].includes(mode) || mode === mobileViewMode) return;
                    mobileViewMode = mode;
                    syncMobileViewButtons();
                    renderMobileCalendar();
                    if (mobileViewMode !== "year") {
                        loadMobileAgenda(selectedDate).catch(handleError);
                    }
                });
            });

            on(elements.mobileMonthPickerBtn, "click", () => {
                if (mobileViewMode === "year") return;
                openMonthPicker();
            });
            on(elements.monthPickerOverlay, "click", closeMonthPicker);
            $$('[data-yg-close="month-picker"]').forEach(button => on(button, "click", closeMonthPicker));
            on(elements.monthPickerPrevYear, "click", () => {
                monthPickerYear -= 1;
                renderMonthPicker();
            });
            on(elements.monthPickerNextYear, "click", () => {
                monthPickerYear += 1;
                renderMonthPicker();
            });
            on(elements.monthPickerMonths, "click", event => {
                const button = event.target.closest("[data-month-index]");
                if (!button || !calendar) return;
                const month = Number(button.dataset.monthIndex || 0);
                const target = new Date(monthPickerYear, month, 1);
                calendar.gotoDate(target);
                if (calendar.view?.type !== "dayGridMonth") calendar.changeView("dayGridMonth");
                mobileViewMode = "month";
                syncMobileViewButtons();
                selectedDate = formatLocalDate(target);
                closeMonthPicker();
                updateCalendarToolbar();
                renderMobileCalendar();
                loadMobileAgenda(selectedDate).catch(handleError);
            });

            on(elements.mobileMonthGrid, "click", event => {
                const monthButton = event.target.closest("[data-mobile-month-index]");
                if (monthButton && calendar) {
                    const monthIndex = Number(monthButton.dataset.mobileMonthIndex || 0);
                    const current = calendar.getDate();
                    const target = new Date(current.getFullYear(), monthIndex, 1);
                    calendar.gotoDate(target);
                    mobileViewMode = "month";
                    syncMobileViewButtons();
                    selectedDate = formatLocalDate(target);
                    renderMobileCalendar();
                    loadMobileAgenda(selectedDate).catch(handleError);
                    return;
                }

                const day = event.target.closest("[data-mobile-date]");
                if (!day) return;
                const date = day.dataset.mobileDate;
                if (!date) return;
                if (isPastDate(date)) {
                    showPastDateWarning();
                    return;
                }
                selectedDate = date;
                if (calendar) calendar.gotoDate(parseLocalDate(date));
                renderMobileCalendar();
                loadMobileAgenda(date).catch(handleError);
            });

            on(elements.mobileAgenda, "click", event => {
                const action = event.target.closest("[data-mobile-agenda-action]");
                if (!action) return;
                const type = action.dataset.mobileAgendaAction;
                if (type === "book") openManualStudio(selectedDate);
                else if (type === "details") openDayDetails(selectedDate);
                else if (type === "close") openRangeDialog("close", selectedDate, selectedDate);
                else if (type === "reopen") openRangeDialog("reopen", selectedDate, selectedDate);
                else if (type === "open-booking") {
                    const bookingId = Number(action.dataset.bookingId || 0);
                    if (bookingId > 0) window.location.href = `/Owner/Reservation/ManageReservations?bookingId=${bookingId}`;
                }
            });

            $$('[data-calendar-view]').forEach(button => {
                on(button, "click", () => {
                    const viewName = button.dataset.calendarView;
                    if (!viewName || !calendar) return;
                    calendar.changeView(viewName);
                    updateCalendarToolbar();
                });
            });

            $$('[data-calendar-scope]').forEach(button => {
                on(button, "click", async () => {
                    const scope = button.dataset.calendarScope || "active";
                    if (!["active", "history", "all"].includes(scope) || scope === calendarScope) return;
                    calendarScope = scope;
                    syncScopeButtons();
                    calendar?.refetchEvents();
                    renderMobileCalendar();
                    await loadMobileAgenda(selectedDate).catch(() => { });
                    if (elements.daySheet?.classList.contains("is-open")) {
                        await loadDayDetails(selectedDate);
                    }
                });
            });

            on(elements.openManual, "click", () => openManualStudio(selectedDate));
            on(elements.closeRange, "click", () => openRangeDialog("close", selectedDate, selectedDate));
            on(elements.reopenRange, "click", () => openRangeDialog("reopen", selectedDate, selectedDate));

            on(elements.dayOverlay, "click", () => closeDaySheet());
            on(elements.manualOverlay, "click", () => closeManualStudio());
            on(elements.rangeOverlay, "click", () => closeRangeDialog());

            $$('[data-yg-close="day"]').forEach(button => on(button, "click", closeDaySheet));
            $$('[data-yg-close="manual"]').forEach(button => on(button, "click", closeManualStudio));
            $$('[data-yg-close="range"]').forEach(button => on(button, "click", closeRangeDialog));

            on(elements.dayCreate, "click", () => {
                closeDaySheet(true);
                openManualStudio(selectedDate);
            });
            on(elements.dayClose, "click", () => {
                closeDaySheet(true);
                openRangeDialog("close", selectedDate, selectedDate);
            });
            on(elements.dayReopen, "click", () => {
                closeDaySheet(true);
                openRangeDialog("reopen", selectedDate, selectedDate);
            });

            $$("[data-yg-step-target]").forEach(button => {
                on(button, "click", async () => {
                    const targetStep = Number(button.dataset.ygStepTarget || 1);
                    if (targetStep < manualStep) {
                        setManualStep(targetStep);
                        return;
                    }
                    if (targetStep === manualStep + 1) {
                        await nextManualStep();
                    }
                });
            });
            on(elements.stepBack, "click", () => setManualStep(Math.max(1, manualStep - 1)));
            on(elements.stepNext, "click", nextManualStep);

            $$('[data-counter-target]').forEach(button => {
                on(button, "click", () => {
                    const target = $("#" + button.dataset.counterTarget);
                    if (!target) return;
                    const delta = Number(button.dataset.counterDelta || 0);
                    const min = Number(target.min || 0);
                    const max = Number(target.max || 999999);
                    const current = Number(target.value || 0);
                    target.value = String(Math.min(max, Math.max(min, current + delta)));
                    target.dispatchEvent(new Event("input", { bubbles: true }));
                });
            });

            $$('input[name="ygManualChannel"]').forEach(input => {
                on(input, "change", () => {
                    syncOtherFields();
                    clearFieldError("manualChannel");
                    scheduleQuotePreview();
                });
            });

            on(elements.phoneSearch, "input", () => {
                sanitizePhoneInput(elements.phoneSearch);
                const phone = readPhoneValue(elements.phoneSearch, phoneSearchIti);
                const phoneIsValid = validatePhoneField(elements.phoneSearch, phoneSearchIti, elements.phoneSearchFeedback, false);
                if (!linkedCustomer && phone) {
                    setPhoneValue(elements.guestPhone, guestPhoneIti, phone);
                }
                window.clearTimeout(customerTimer);
                if (phoneIsValid) {
                    customerTimer = window.setTimeout(() => searchCustomer(phone), 520);
                } else if (!phone) {
                    unlinkCustomer(false);
                }
            });
            on(elements.phoneSearch, "countrychange", () => {
                validatePhoneField(elements.phoneSearch, phoneSearchIti, elements.phoneSearchFeedback, false);
            });
            on(elements.searchCustomer, "click", () => searchCustomer(readPhoneValue(elements.phoneSearch, phoneSearchIti)));
            on(elements.unlinkCustomer, "click", () => unlinkCustomer(true));

            $$('[data-resource-kind]').forEach(button => {
                on(button, "click", () => setResourceKind(button.dataset.resourceKind || "room"));
            });

            on(elements.startDate, "change", () => {
                clearFieldError("startDate");
                clearFieldError("endDate");
                handleManualDateChange();
            });
            on(elements.endDate, "change", () => {
                clearFieldError("startDate");
                clearFieldError("endDate");
                handleManualDateChange();
            });
            on(elements.roomId, "change", () => {
                clearFieldError("roomId");
                updateSmartSelectValue(elements.roomId, elements.roomSelectText);
                updateRoomHint();
                normalizeRoomQuantity();
                syncGuestCapacity(true);
                activeRoomFloorKey = "";
                selectedRoomUnitIds.clear();
                renderRoomUnitChoices();
                scheduleQuotePreview();
            });
            on(elements.packageId, "change", () => {
                clearFieldError("packageId");
                updatePackageHint();
                renderPackageCards();
                syncGuestCapacity(true);
                scheduleQuotePreview();
            });
            on(elements.roomFloorTabs, "click", event => {
                const button = event.target.closest("[data-floor-key]");
                if (!button) return;
                activeRoomFloorKey = button.dataset.floorKey || "";
                renderRoomUnitChoices(false);
            });
            on(elements.packageCards, "click", event => {
                const card = event.target.closest("[data-package-id]");
                if (!card || card.classList.contains("is-disabled")) return;
                elements.packageId.value = card.dataset.packageId || "";
                elements.packageId.dispatchEvent(new Event("change", { bubbles: true }));
            });
            on(elements.quantity, "input", () => {
                clearFieldError("quantity");
                normalizeRoomQuantity();
                syncGuestCapacity(true);
                normalizeRoomUnitSelection();
                renderRoomUnitChoices(false);
                scheduleQuotePreview();
            });
            on(elements.roomUnitChoices, "change", event => {
                const changed = event.target.closest('input[type="checkbox"]');
                if (!changed) return;

                const id = Number(changed.value || 0);
                if (id > 0) {
                    if (changed.checked) selectedRoomUnitIds.add(id);
                    else selectedRoomUnitIds.delete(id);
                }

                normalizeRoomUnitSelection();
                updateRoomUnitVisuals();
                clearFieldError("roomUnits");
                scheduleQuotePreview();
            });
            on(elements.adults, "input", () => { syncGuestCapacity(false, "adults"); scheduleQuotePreview(); });
            on(elements.children, "input", () => { syncGuestCapacity(false, "children"); scheduleQuotePreview(); });
            on(elements.promoCode, "input", () => { clearFieldError("promoCode"); scheduleQuotePreview(); });
            on(elements.seating, "change", () => {
                updateSmartSelectValue(elements.seating, elements.seatingSelectText);
                syncOtherFields();
                clearFieldError("seating");
                scheduleQuotePreview();
            });
            on(elements.seatingOther, "input", () => { clearFieldError("seatingOther"); scheduleQuotePreview(); });
            on(elements.guestName, "input", () => { clearFieldError("guestName"); scheduleQuotePreview(); });
            on(elements.guestEmail, "input", () => { clearFieldError("guestEmail"); scheduleQuotePreview(); });
            on(elements.manualChannelOther, "input", () => { clearFieldError("manualChannelOther"); scheduleQuotePreview(); });
            on(elements.guestPhone, "input", () => {
                sanitizePhoneInput(elements.guestPhone);
                validatePhoneField(elements.guestPhone, guestPhoneIti, elements.guestPhoneFeedback, false);
                scheduleQuotePreview();
            });
            on(elements.guestPhone, "countrychange", () => {
                validatePhoneField(elements.guestPhone, guestPhoneIti, elements.guestPhoneFeedback, false);
                scheduleQuotePreview();
            });

            $$('input[name="ygPaymentMode"]').forEach(input => {
                on(input, "change", () => {
                    syncPaymentFields();
                    scheduleQuotePreview();
                });
            });
            on(elements.paidAmount, "input", () => { clearFieldError("paidAmount"); clampDecimalInput(elements.paidAmount, 0, 999999999); scheduleQuotePreview(); });
            on(elements.paymentMethod, "change", () => {
                updateSmartSelectValue(elements.paymentMethod, elements.paymentSelectText);
                // إعادة مزامنة لوحة الدفع كاملة حتى يظهر اختيار نوع المحفظة فورًا
                // عند اختيار Wallet من الـ smart select، وليس فقط في تحديثات الحقول الأخرى.
                syncPaymentFields();
                clearFieldError("paymentMethod");
                clearFieldError("ownerWallet");
                scheduleQuotePreview();
            });
            on(elements.ownerWalletId, "change", () => {
                updateSmartSelectValue(elements.ownerWalletId, elements.ownerWalletSelectText);
                clearFieldError("ownerWallet");
                scheduleQuotePreview();
            });
            on(elements.ownerWalletId, "change", () => {
                updateSmartSelectValue(elements.ownerWalletId, elements.ownerWalletSelectText);
                clearFieldError("ownerWallet");
                scheduleQuotePreview();
            });
            on(elements.paymentMethodOther, "input", () => { clearFieldError("paymentMethodOther"); scheduleQuotePreview(); });
            on(elements.paymentReference, "input", () => { clearFieldError("paymentReference"); scheduleQuotePreview(); });

            initializeSmartSelect(elements.roomId, elements.roomSelectTrigger, elements.roomSelectMenu, elements.roomSelectText);
            initializeSmartSelect(elements.seating, elements.seatingSelectTrigger, elements.seatingSelectMenu, elements.seatingSelectText);
            initializeSmartSelect(elements.paymentMethod, elements.paymentSelectTrigger, elements.paymentSelectMenu, elements.paymentSelectText);
            initializeSmartSelect(elements.ownerWalletId, elements.ownerWalletSelectTrigger, elements.ownerWalletSelectMenu, elements.ownerWalletSelectText);

            on(document, "click", event => {
                if (!event.target.closest(".yg-v8-smart-select")) closeAllSmartSelects();
            });

            on(elements.services, "change", event => {
                const card = event.target.closest(".yg-service-card");
                if (card) {
                    const checkbox = card.querySelector('input[type="checkbox"]');
                    card.classList.toggle("is-selected", Boolean(checkbox?.checked));
                }
                scheduleQuotePreview();
            });
            on(elements.services, "input", scheduleQuotePreview);

            on(elements.preview, "click", () => previewManualBooking(true));
            on(elements.create, "click", createManualBooking);
            on(elements.rangeSubmit, "click", submitRangeAction);

            on(elements.dayBookingsList, "click", event => {
                const paymentButton = event.target.closest("[data-record-payment]");
                if (paymentButton) {
                    recordManualPayment(
                        Number(paymentButton.dataset.recordPayment || 0),
                        Number(paymentButton.dataset.paymentRemaining || 0),
                        paymentButton.dataset.paymentMode || "initial"
                    );
                    return;
                }

                const openButton = event.target.closest("[data-open-booking]");
                if (openButton) {
                    const bookingId = Number(openButton.dataset.openBooking || 0);
                    if (bookingId > 0) {
                        window.location.href = `/Owner/Reservation/ManageReservations?bookingId=${bookingId}`;
                    }
                }
            });

            on(document, "keydown", event => {
                if (event.key !== "Escape") return;
                if (elements.monthPickerModal?.classList.contains("is-open")) closeMonthPicker();
                else if (elements.rangeModal?.classList.contains("is-open")) closeRangeDialog();
                else if (elements.manualModal?.classList.contains("is-open")) closeManualStudio();
                else if (elements.daySheet?.classList.contains("is-open")) closeDaySheet();
            });

            // عندما يغيّر هيدر المالك المنشأة، كل endpoint يقرأ المنشأة من الهيدر/الكوكي.
            on(document, "ownerActivePlaceChanged", () => refreshAfterFacilityChange());
            on(document, "facilityChanged", () => refreshAfterFacilityChange());
            on(document, "change", event => {
                const target = event.target;
                if (!(target instanceof Element)) return;
                if (target.matches("#ownerActivePlaceSelector, #globalPlaceSelector, [data-owner-place-selector], [data-owner-facility-selector]")) {
                    window.setTimeout(refreshAfterFacilityChange, 120);
                }
            });
        }

        function initializeConstrainedInputs() {
            const today = formatLocalDate(new Date());
            [elements.startDate, elements.endDate, elements.rangeStart, elements.rangeEnd]
                .filter(Boolean)
                .forEach(input => { input.min = today; });

            [elements.quantity, elements.adults, elements.children].filter(Boolean).forEach(input => {
                on(input, "keydown", event => {
                    if (["e", "E", "+", "-", ".", ","].includes(event.key)) event.preventDefault();
                });
            });

            on(elements.guestName, "input", () => {
                elements.guestName.value = elements.guestName.value.replace(/\s{2,}/g, " ").replace(/^\s+/, "");
            });
            on(elements.guestEmail, "blur", () => {
                elements.guestEmail.value = elements.guestEmail.value.trim().toLowerCase();
                if (elements.guestEmail.value) {
                    elements.guestEmail.classList.toggle("is-invalid", !elements.guestEmail.checkValidity());
                } else {
                    elements.guestEmail.classList.remove("is-invalid");
                }
            });
            on(elements.paymentReference, "input", () => {
                elements.paymentReference.value = elements.paymentReference.value.replace(/[^0-9A-Za-z\-_/\s]/g, "").slice(0, 100);
            });
        }

        function initializePhoneInputs() {
            if (!elements.phoneSearch || !elements.guestPhone) return;

            if (typeof window.intlTelInput === "function") {
                if (!phoneSearchIti) {
                    phoneSearchIti = window.intlTelInput(elements.phoneSearch, {
                        initialCountry: "ye",
                        preferredCountries: ["ye", "sa", "ae", "eg"],
                        separateDialCode: true,
                        strictMode: true,
                        dropdownContainer: document.body,
                        utilsScript: "https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js"
                    });
                }
                if (!guestPhoneIti) {
                    guestPhoneIti = window.intlTelInput(elements.guestPhone, {
                        initialCountry: "ye",
                        preferredCountries: ["ye", "sa", "ae", "eg"],
                        separateDialCode: true,
                        strictMode: true,
                        dropdownContainer: document.body,
                        utilsScript: "https://cdn.jsdelivr.net/npm/intl-tel-input@23.0.4/build/js/utils.js"
                    });
                }
            }

            sanitizePhoneInput(elements.phoneSearch);
            sanitizePhoneInput(elements.guestPhone);
        }

        function sanitizePhoneInput(input) {
            if (!input) return;
            const raw = String(input.value || "");
            const cleaned = raw.replace(/[^0-9\s()+-]/g, "").replace(/\s{2,}/g, " ").slice(0, 18);
            if (cleaned !== raw) input.value = cleaned;
        }

        function digitsOnly(value) {
            return String(value || "").replace(/\D/g, "");
        }

        function readPhoneValue(input, iti) {
            if (!input) return "";
            const raw = String(input.value || "").trim();
            if (!raw) return "";
            try {
                const number = iti?.getNumber?.();
                if (number && digitsOnly(number).length >= 7) return number;
            } catch (_) { }
            return raw;
        }

        function setPhoneValue(input, iti, value) {
            if (!input) return;
            const safe = String(value || "").trim();
            try {
                if (iti?.setNumber) iti.setNumber(safe);
                else input.value = safe;
            } catch (_) {
                input.value = safe;
            }
            sanitizePhoneInput(input);
        }

        function validatePhoneField(input, iti, feedback, required) {
            if (!input) return !required;
            const raw = String(input.value || "").trim();
            const digits = digitsOnly(readPhoneValue(input, iti));
            input.classList.remove("is-valid", "is-invalid");
            if (feedback) {
                feedback.textContent = "";
                feedback.classList.remove("is-valid", "is-invalid");
            }

            if (!raw) {
                if (required) {
                    input.classList.add("is-invalid");
                    if (feedback) {
                        feedback.textContent = "رقم الهاتف مطلوب.";
                        feedback.classList.add("is-invalid");
                    }
                    return false;
                }
                return true;
            }

            if (!required && digits.length < 7) {
                if (feedback) feedback.textContent = "أكمل رقم الهاتف…";
                return false;
            }

            let valid = digits.length >= 7 && digits.length <= 15;
            try {
                const country = iti?.getSelectedCountryData?.();
                const localDigits = digitsOnly(input.value);
                if (country?.iso2 === "ye") {
                    valid = /^(77|78|70|73|71)[0-9]{7}$/.test(localDigits) || /^967(77|78|70|73|71)[0-9]{7}$/.test(digits);
                } else if (iti?.isValidNumber) {
                    valid = iti.isValidNumber();
                }
            } catch (_) { }

            input.classList.add(valid ? "is-valid" : "is-invalid");
            if (feedback) {
                feedback.textContent = valid ? "رقم صالح" : "تحقق من رقم الهاتف.";
                feedback.classList.add(valid ? "is-valid" : "is-invalid");
            }
            return valid;
        }

        function clampIntegerInput(input, min, max) {
            if (!input || input.value === "") return;
            const value = Math.trunc(Number(input.value));
            input.value = String(Math.min(max, Math.max(min, Number.isFinite(value) ? value : min)));
        }

        function clampDecimalInput(input, min, max) {
            if (!input || input.value === "") return;
            const value = Number(input.value);
            if (!Number.isFinite(value)) { input.value = String(min); return; }
            input.value = String(Math.min(max, Math.max(min, value)));
        }

        function createFullCalendar() {
            const compact = window.matchMedia("(max-width: 680px)").matches;

            calendar = new FullCalendar.Calendar(calendarEl, {
                locale: "ar",
                direction: "rtl",
                initialView: "dayGridMonth",
                firstDay: 6,
                height: "auto",
                navLinks: false,
                selectable: true,
                selectMirror: true,
                unselectAuto: true,
                fixedWeekCount: false,
                nowIndicator: true,
                headerToolbar: false,
                footerToolbar: false,
                dayMaxEvents: compact ? 3 : 2,
                moreLinkText: count => `+${count}`,
                moreLinkClick: "popover",
                dayHeaderContent: arg => renderDayHeader(arg),
                dayCellContent: arg => renderDayCell(arg),
                eventOrder: "start,-duration,title",
                eventDisplay: "block",
                views: {
                    dayGridWeek: {
                        buttonText: "أسبوع",
                        dayMaxEvents: compact ? 3 : 6
                    },
                    dayGridMonth: {
                        buttonText: "شهر"
                    },
                    multiMonthYear: {
                        type: "multiMonth",
                        duration: { years: 1 },
                        buttonText: "سنة",
                        multiMonthMaxColumns: 3,
                        multiMonthMinWidth: 180,
                        dayMaxEvents: 3
                    }
                },
                datesSet: async info => {
                    if (disposed) return;
                    selectedDate = formatLocalDate(calendar.getDate());
                    updateCalendarToolbar();
                    scheduleCalendarEnhancements();
                    renderMobileCalendar();
                    await loadContext(info.start, info.end).catch(handleError);
                },
                viewDidMount: () => {
                    updateCalendarToolbar();
                    scheduleCalendarEnhancements();
                },
                events: async (info, successCallback, failureCallback) => {
                    try {
                        setCalendarLoading(true);
                        const query = new URLSearchParams({
                            start: formatLocalDate(info.start),
                            end: formatLocalDate(info.end)
                        });
                        const result = await getJson(`${ENDPOINTS.events}?${query}`);
                        if (!result.success) throw new Error(result.message || "تعذر تحميل أحداث التقويم.");
                        lastLoadedEvents = Array.isArray(result.events) ? result.events : [];
                        successCallback(filterCalendarEvents(lastLoadedEvents));
                        renderMobileCalendar();
                        loadMobileAgenda(selectedDate).catch(() => { });
                    } catch (error) {
                        failureCallback(error);
                        handleError(error);
                    } finally {
                        setCalendarLoading(false);
                    }
                },
                dateClick: info => {
                    if (isPastDate(info.dateStr)) {
                        showPastDateWarning();
                        return;
                    }
                    selectedDate = info.dateStr;
                    openDayDetails(info.dateStr);
                },
                select: info => {
                    const start = info.startStr;
                    const endInclusive = formatLocalDate(addDays(info.end, -1));
                    if (isPastDate(start) || isPastDate(endInclusive)) {
                        calendar.unselect();
                        showPastDateWarning();
                        return;
                    }
                    selectedDate = start;
                    if (start === endInclusive) openDayDetails(start);
                    else openRangeDialog("close", start, endInclusive);
                    calendar.unselect();
                },
                eventClick: info => {
                    const date = info.event.start ? formatLocalDate(info.event.start) : selectedDate;
                    selectedDate = date;
                    openDayDetails(date);
                },
                eventContent: arg => renderEventContent(arg),
                eventDidMount: info => {
                    const props = info.event.extendedProps || {};
                    const compactNow = window.matchMedia("(max-width: 680px)").matches;
                    if (props.eventType === "occupancy" && (compactNow || info.view?.type === "multiMonthYear")) {
                        info.el.style.display = "none";
                        return;
                    }
                    const title = [
                        info.event.title,
                        props.resourceName,
                        props.state ? stateLabel(props.state) : "",
                        props.source === "OwnerManual" ? "حجز مباشر" : props.source === "Online" ? "أونلاين" : "",
                        props.reason || ""
                    ].filter(Boolean).join(" · ");
                    info.el.setAttribute("title", title);
                }
            });

            calendar.render();
            updateCalendarToolbar();
        }

        function updateCalendarToolbar() {
            if (!calendar) return;
            const viewType = calendar.view?.type || "dayGridMonth";
            const current = calendar.getDate();
            const ar = new Intl.DateTimeFormat("ar-YE", { month: "long", year: "numeric" });
            const yearOnly = new Intl.DateTimeFormat("ar-YE", { year: "numeric" });
            const short = new Intl.DateTimeFormat("ar-YE", { day: "numeric", month: "short" });

            let eyebrow = "عرض الشهر";
            let title = ar.format(current);
            let subtitle = "اضغط على أي يوم لعرض التوفر والحجوزات";

            if (viewType === "dayGridWeek") {
                eyebrow = "عرض الأسبوع";
                const start = calendar.view.activeStart;
                const end = addDays(calendar.view.activeEnd, -1);
                title = `${short.format(start)} — ${short.format(end)}`;
                subtitle = "متابعة سريعة لحجوزات الأسبوع الحالي";
            } else if (viewType === "multiMonthYear") {
                eyebrow = "نظرة سنوية";
                title = yearOnly.format(current);
                subtitle = "اضغط عنوان أي شهر للانتقال إليه مباشرة";
            }

            if (elements.calPeriodEyebrow) elements.calPeriodEyebrow.textContent = eyebrow;
            if (elements.calPeriodTitle) elements.calPeriodTitle.textContent = title;
            if (elements.calPeriodSubtitle) elements.calPeriodSubtitle.textContent = subtitle;
            if (elements.mobileMonthLabel) {
                elements.mobileMonthLabel.textContent = new Intl.DateTimeFormat("ar-YE", {
                    month: "long",
                    year: "numeric"
                }).format(current);
            }

            $$('[data-calendar-view]').forEach(button => {
                const active = button.dataset.calendarView === viewType;
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-pressed", active ? "true" : "false");
            });
        }

        function renderEventContent(arg) {
            const props = arg.event.extendedProps || {};
            const type = props.eventType || "booking";

            const stateKey = type === "closed" || type === "policyClosed"
                ? "closed"
                : calendarStateKey(props.state);

            if (arg.view.type === "multiMonthYear") {
                if (type === "occupancy") return { html: "" };
                return {
                    html: `<span class="yg-year-dot yg-year-dot--${stateKey}" aria-label="${escapeHtml(stateLabel(props.state || ""))}"></span>`
                };
            }

            if (type === "occupancy") {
                const occupied = Number(props.occupiedUnits || 0);
                const total = Number(props.totalUnits || 0);
                if (total <= 0) return { html: "" };

                const percent = Math.max(0, Math.min(100, Math.round((occupied / total) * 100)));
                return {
                    html: `<div class="yg-occupancy-mini" title="الإشغال ${occupied} من ${total}">
                        <span><i style="width:${percent}%"></i></span>
                        <small>${occupied}/${total}</small>
                    </div>`
                };
            }

            if (type === "closed" || type === "policyClosed") {
                const policy = type === "policyClosed";
                return {
                    html: `<div class="yg-closure-token ${policy ? "is-policy" : "is-exception"}">
                        <span class="yg-closure-token__icon"><i class="bi ${policy ? "bi-calendar-week-fill" : "bi-lock-fill"}"></i></span>
                        <span class="yg-closure-token__copy">
                            <b>${policy ? "إغلاق أسبوعي" : "مغلق من المنشأة"}</b>
                            <small>${escapeHtml(props.reason || (policy ? "حسب سياسة التشغيل" : "لا تقبل حجوزات جديدة"))}</small>
                        </span>
                    </div>`
                };
            }

            const icon = stateIconClass(props.state, type);

            const customer = type === "closed" || type === "policyClosed"
                ? (arg.event.title || "غير متاح")
                : (props.customerName || arg.event.title || "حجز");

            const sourceLabel = props.source === "OwnerManual" ? "مباشر" : "أونلاين";
            const sourceIcon = props.source === "OwnerManual" ? "bi-telephone-inbound-fill" : "bi-globe2";
            const subLabel = stateKey === "closed"
                ? (type === "policyClosed" ? "إغلاق أسبوعي" : "إغلاق استثنائي")
                : `${stateLabel(props.state || "")} · ${sourceLabel}`;

            const initial = initialLetter(customer);

            return {
                html: `<div class="yg-booking-token yg-booking-token--${stateKey} ${props.isHistorical ? "is-historical" : ""}">
                    <span class="yg-booking-token__avatar">${escapeHtml(initial)}</span>
                    <span class="yg-booking-token__copy">
                        <b>${escapeHtml(customer)}</b>
                        <small><i class="bi ${sourceIcon}"></i> ${escapeHtml(subLabel)}</small>
                    </span>
                    <span class="yg-booking-token__state"><i class="bi ${icon}"></i></span>
                </div>`
            };
        }

        function renderDayCell(arg) {
            const day = arg.date.getDate();
            const date = formatLocalDate(arg.date);
            const today = formatLocalDate(new Date());
            const selected = date === selectedDate;
            const isToday = date === today;

            return {
                html: `<span class="yg-date-orb ${isToday ? "is-today" : ""} ${selected ? "is-selected" : ""}">
                    <b>${day}</b>
                </span>`
            };
        }

        function renderMobileCalendar() {
            if (!elements.mobileMonthGrid || !calendar) return;

            syncMobileViewButtons();

            if (mobileViewMode === "week") {
                renderMobileWeekView();
                return;
            }

            if (mobileViewMode === "year") {
                renderMobileYearView();
                return;
            }

            renderMobileMonthView();
        }

        function syncMobileViewButtons() {
            $$('[data-mobile-calendar-view]').forEach(button => {
                button.classList.toggle("is-active", button.dataset.mobileCalendarView === mobileViewMode);
            });
            elements.mobileCalendar?.setAttribute("data-mobile-view", mobileViewMode);
            updateMobileNavLabels();
        }

        function updateMobileNavLabels() {
            const labels = mobileViewMode === "week"
                ? { prev: "الأسبوع السابق", next: "الأسبوع التالي" }
                : mobileViewMode === "year"
                    ? { prev: "السنة السابقة", next: "السنة التالية" }
                    : { prev: "الشهر السابق", next: "الشهر التالي" };

            elements.mobilePrev?.setAttribute("aria-label", labels.prev);
            elements.mobileNext?.setAttribute("aria-label", labels.next);
        }

        function goMobileToday() {
            if (!calendar) return;

            const today = new Date();
            selectedDate = formatLocalDate(today);
            calendar.gotoDate(today);
            updateCalendarToolbar();
            renderMobileCalendar();

            if (mobileViewMode !== "year") {
                loadMobileAgenda(selectedDate).catch(handleError);
            }

            if (elements.mobileToday) {
                elements.mobileToday.classList.remove("is-jump");
                void elements.mobileToday.offsetWidth;
                elements.mobileToday.classList.add("is-jump");
                window.setTimeout(() => elements.mobileToday?.classList.remove("is-jump"), 420);
            }
        }

        function navigateMobileCalendar(direction) {
            if (!calendar) return;
            const current = calendar.getDate();
            let target;

            if (mobileViewMode === "week") {
                target = addDays(current, direction * 7);
            } else if (mobileViewMode === "year") {
                target = new Date(current.getFullYear() + direction, current.getMonth(), 1);
            } else {
                target = new Date(current.getFullYear(), current.getMonth() + direction, 1);
            }

            calendar.gotoDate(target);
            updateCalendarToolbar();
            renderMobileCalendar();
        }

        function renderMobileMonthView() {
            const current = calendar.getDate();
            const year = current.getFullYear();
            const month = current.getMonth();

            if (elements.mobileModeLabel) elements.mobileModeLabel.textContent = "عرض الشهر";
            if (elements.mobileMonthLabel) {
                elements.mobileMonthLabel.textContent = new Intl.DateTimeFormat("ar-YE", {
                    month: "long",
                    year: "numeric"
                }).format(current);
            }

            const first = new Date(year, month, 1);
            const last = new Date(year, month + 1, 0);
            const offset = (first.getDay() + 1) % 7;
            const cells = offset + last.getDate();
            const totalCells = cells <= 35 ? 35 : 42;
            const gridStart = new Date(year, month, 1 - offset);
            const eventMap = buildMobileEventMap(filterCalendarEvents(lastLoadedEvents));
            const today = formatLocalDate(new Date());

            const html = [];
            for (let i = 0; i < totalCells; i += 1) {
                const date = addDays(gridStart, i);
                const dateKey = formatLocalDate(date);
                const inMonth = date.getMonth() === month;
                const items = eventMap.get(dateKey) || [];
                const states = Array.from(new Set(items.map(item => mobileStateKey(item)).filter(Boolean))).slice(0, 3);
                const count = items.filter(item => (item.extendedProps || {}).eventType !== "occupancy").length;

                html.push(`<button type="button"
                    class="yg-mobile-day ${inMonth ? "" : "is-outside"} ${dateKey === today ? "is-today" : ""} ${isPastDate(dateKey) ? "is-past" : ""} ${dateKey === selectedDate ? "is-selected" : ""}"
                    data-mobile-date="${dateKey}"
                    aria-label="${escapeHtml(formatDateArabic(dateKey))}">
                    <span class="yg-mobile-day__number">${date.getDate()}</span>
                    <span class="yg-mobile-day__signals">
                        ${states.map(state => `<i class="is-${state}"></i>`).join("")}
                    </span>
                    ${count > 3 ? `<small>+${count - 3}</small>` : ""}
                </button>`);
            }

            elements.mobileMonthGrid.innerHTML = html.join("");
        }

        function renderMobileWeekView() {
            const base = parseLocalDate(selectedDate || formatLocalDate(calendar.getDate()));
            const offset = (base.getDay() + 1) % 7;
            const start = addDays(base, -offset);
            const end = addDays(start, 6);
            const eventMap = buildMobileEventMap(filterCalendarEvents(lastLoadedEvents));
            const today = formatLocalDate(new Date());

            if (elements.mobileModeLabel) elements.mobileModeLabel.textContent = "عرض الأسبوع";
            if (elements.mobileMonthLabel) {
                const startText = new Intl.DateTimeFormat("ar-YE", { day: "numeric", month: "short" }).format(start);
                const endText = new Intl.DateTimeFormat("ar-YE", { day: "numeric", month: "short" }).format(end);
                elements.mobileMonthLabel.textContent = `${startText} — ${endText}`;
            }

            const weekday = ["السبت", "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة"];
            const html = [];
            for (let i = 0; i < 7; i += 1) {
                const date = addDays(start, i);
                const dateKey = formatLocalDate(date);
                const items = eventMap.get(dateKey) || [];
                const states = Array.from(new Set(items.map(item => mobileStateKey(item)).filter(Boolean))).slice(0, 3);
                const count = items.filter(item => (item.extendedProps || {}).eventType !== "occupancy").length;

                html.push(`<button type="button" class="yg-mobile-week-day ${dateKey === today ? "is-today" : ""} ${isPastDate(dateKey) ? "is-past" : ""} ${dateKey === selectedDate ? "is-selected" : ""}" data-mobile-date="${dateKey}">
                    <small>${weekday[i]}</small>
                    <strong>${date.getDate()}</strong>
                    <span>${new Intl.DateTimeFormat("ar-YE", { month: "short" }).format(date)}</span>
                    <i class="yg-mobile-week-day__signals">${states.map(state => `<b class="is-${state}"></b>`).join("")}</i>
                    ${count > 0 ? `<em>${count}</em>` : ""}
                </button>`);
            }
            elements.mobileMonthGrid.innerHTML = html.join("");
        }

        function renderMobileYearView() {
            const current = calendar.getDate();
            const year = current.getFullYear();
            const events = filterCalendarEvents(lastLoadedEvents);
            const monthNames = Array.from({ length: 12 }, (_, index) =>
                new Intl.DateTimeFormat("ar-YE", { month: "long" }).format(new Date(year, index, 1))
            );

            if (elements.mobileModeLabel) elements.mobileModeLabel.textContent = "عرض السنة";
            if (elements.mobileMonthLabel) elements.mobileMonthLabel.textContent = String(year);

            const html = monthNames.map((name, monthIndex) => {
                const monthEvents = events.filter(item => {
                    const raw = item.start || item.Start;
                    if (!raw) return false;
                    const date = new Date(raw);
                    return date.getFullYear() === year && date.getMonth() === monthIndex;
                });
                const bookingCount = monthEvents.filter(item => {
                    const props = item.extendedProps || item.ExtendedProps || item || {};
                    const type = props.eventType || props.EventType || "booking";
                    return type === "booking";
                }).length;
                const states = Array.from(new Set(monthEvents.map(item => mobileStateKey(item)).filter(Boolean))).slice(0, 4);
                const active = current.getMonth() === monthIndex;

                return `<button type="button" class="yg-mobile-year-month ${active ? "is-current" : ""}" data-mobile-month-index="${monthIndex}">
                    <span><i class="bi bi-calendar3"></i>${escapeHtml(name)}</span>
                    <strong>${bookingCount}</strong>
                    <small>${bookingCount === 1 ? "حجز" : "حجوزات"}</small>
                    <i class="yg-mobile-year-month__signals">${states.map(state => `<b class="is-${state}"></b>`).join("")}</i>
                </button>`;
            });

            elements.mobileMonthGrid.innerHTML = html.join("");
        }

        function buildMobileEventMap(events) {
            const map = new Map();

            (events || []).forEach(item => {
                const startRaw = item.start || item.Start;
                if (!startRaw) return;

                const start = new Date(startRaw);
                const endRaw = item.end || item.End;
                const end = endRaw ? new Date(endRaw) : addDays(start, 1);

                let cursor = new Date(start.getFullYear(), start.getMonth(), start.getDate());
                const safeEnd = new Date(end.getFullYear(), end.getMonth(), end.getDate());
                let guard = 0;

                while (cursor < safeEnd && guard < 370) {
                    const key = formatLocalDate(cursor);
                    if (!map.has(key)) map.set(key, []);
                    map.get(key).push(item);
                    cursor = addDays(cursor, 1);
                    guard += 1;
                }

                if (guard === 0) {
                    const key = formatLocalDate(start);
                    if (!map.has(key)) map.set(key, []);
                    map.get(key).push(item);
                }
            });

            return map;
        }

        function mobileStateKey(item) {
            const props = item.extendedProps || item.ExtendedProps || item || {};
            const type = props.eventType || props.EventType || "booking";
            const source = props.source || props.Source;
            const state = props.state || props.State;

            if (type === "occupancy") return "";
            if (type === "closed" || type === "policyClosed") return "closed";
            return calendarStateKey(state);
        }

        async function loadMobileAgenda(date) {
            if (!elements.mobileAgenda || !date) return;
            const requestId = ++mobileAgendaRequestId;

            elements.mobileAgenda.innerHTML = `<div class="yg-mobile-agenda__loading">
                <span class="yg-inline-loader"></span>
                <strong>نرتب يومك…</strong>
            </div>`;

            const result = await getJson(`${ENDPOINTS.day}?date=${encodeURIComponent(date)}`);
            if (requestId !== mobileAgendaRequestId) return;
            if (!result.success || !result.data) throw new Error(result.message || "تعذر تحميل تفاصيل اليوم.");

            renderMobileAgenda(result.data);
        }

        function renderMobileAgenda(data) {
            if (!elements.mobileAgenda) return;

            const bookings = filterBookingItems(Array.isArray(data.bookings) ? data.bookings : []);
            const closed = Boolean(data.isWeeklyClosed || data.isExceptionClosed);
            const blockingBookingsCount =
                Number(data.confirmedCount || 0) +
                Number(data.awaitingPaymentCount || 0) +
                Number(data.paymentReviewCount || 0) +
                Number(data.pendingCount || 0);
            const canCloseDay = !closed && blockingBookingsCount === 0;
            const visible = bookings.slice(0, 3);

            elements.mobileAgenda.innerHTML = `
                <div class="yg-mobile-agenda__head">
                    <div>
                        <small>${escapeHtml(formatDateArabic(selectedDate))}</small>
                        <strong>${closed ? "اليوم غير متاح" : bookings.length ? `${bookings.length} حجز` : "اليوم متاح"}</strong>
                    </div>
                    <span class="yg-mobile-availability ${closed ? "is-closed" : "is-open"}">
                        <i class="bi ${closed ? "bi-lock-fill" : "bi-sparkles"}"></i>
                        ${closed ? "مغلق" : "جاهز للحجز"}
                    </span>
                </div>

                <div class="yg-mobile-agenda__stats">
                    <span><small>المشغول</small><b>${Number(data.occupiedRoomUnits || 0)}</b></span>
                    <span><small>المتاح</small><b>${Number(data.availableRoomUnits || 0)}</b></span>
                    <span><small>الإشغال</small><b>${formatNumber(data.occupancyPercent || 0)}%</b></span>
                </div>

                ${visible.length ? `<div class="yg-mobile-agenda__bookings">
                    ${visible.map(renderMobileAgendaBooking).join("")}
                </div>` : `<div class="yg-mobile-agenda__nothing">
                    <i class="bi bi-calendar2-heart"></i>
                    <span>لا توجد حجوزات في هذا اليوم.</span>
                </div>`}

                <div class="yg-mobile-agenda__actions">
                    <button type="button" class="is-primary" data-mobile-agenda-action="book">
                        <i class="bi bi-plus-lg"></i><span>حجز مباشر</span>
                    </button>
                    <button type="button" data-mobile-agenda-action="details">
                        <i class="bi bi-list-ul"></i><span>التفاصيل</span>
                    </button>
                    <button type="button"
                        data-mobile-agenda-action="${closed ? "reopen" : "close"}"
                        ${!closed && !canCloseDay ? "disabled" : ""}
                        title="${!closed && !canCloseDay ? `يوجد ${blockingBookingsCount} حجز نشط — لا يمكن إغلاق اليوم` : (closed ? "إعادة فتح اليوم" : "إغلاق اليوم")}">
                        <i class="bi ${closed ? "bi-unlock-fill" : (!canCloseDay ? "bi-shield-lock-fill" : "bi-lock-fill")}"></i>
                    </button>
                </div>`;
        }

        function renderMobileAgendaBooking(item) {
            const name = item.customerName || item.guestName || "ضيف";
            const source = item.bookingSource === "OwnerManual" ? "مباشر" : "أونلاين";
            const state = stateLabel(item.bookingState || "");
            return `<button type="button" class="yg-mobile-booking-row"
                data-mobile-agenda-action="open-booking"
                data-booking-id="${Number(item.bookingId || 0)}">
                <span class="yg-mobile-booking-row__avatar">${escapeHtml(initialLetter(name))}</span>
                <span>
                    <b>${escapeHtml(name)}</b>
                    <small>${escapeHtml(state)} · ${escapeHtml(source)}</small>
                </span>
                <i class="bi bi-chevron-left"></i>
            </button>`;
        }

        function openMonthPicker() {
            if (!calendar) return;
            monthPickerYear = calendar.getDate().getFullYear();
            renderMonthPicker();
            openPanel(elements.monthPickerOverlay, elements.monthPickerModal);
        }

        function closeMonthPicker() {
            closePanel(elements.monthPickerOverlay, elements.monthPickerModal);
        }

        function renderMonthPicker() {
            if (!elements.monthPickerMonths) return;
            if (elements.monthPickerYear) elements.monthPickerYear.textContent = String(monthPickerYear);

            const current = calendar?.getDate() || new Date();
            const months = [
                "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
            ];

            elements.monthPickerMonths.innerHTML = months.map((name, index) => {
                const active = current.getFullYear() === monthPickerYear && current.getMonth() === index;
                return `<button type="button" class="${active ? "is-active" : ""}" data-month-index="${index}">
                    <small>${String(index + 1).padStart(2, "0")}</small>
                    <strong>${name}</strong>
                </button>`;
            }).join("");
        }

        function renderDayHeader(arg) {
            const full = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
            const compact = ["أحد", "اثن", "ثلا", "أرب", "خمس", "جمع", "سبت"];
            const useCompact = window.matchMedia("(max-width: 680px)").matches || arg?.view?.type === "multiMonthYear";
            const label = (useCompact ? compact : full)[arg.date.getDay()] || "";
            return { html: `<span class="yg-weekday-label">${label}</span>` };
        }

        function scheduleCalendarEnhancements() {
            window.requestAnimationFrame(() => {
                if (disposed) return;
                bindYearMonthNavigation();
            });
        }

        function bindYearMonthNavigation() {
            if (!calendar || calendar.view?.type !== "multiMonthYear") return;

            Array.from(calendarEl.querySelectorAll(".fc-multimonth-month")).forEach(month => {
                const title = month.querySelector(".fc-multimonth-title");
                if (!title) return;
                title.setAttribute("role", "button");
                title.setAttribute("tabindex", "0");
                title.setAttribute("title", "فتح هذا الشهر");
            });
        }

        async function refreshAll() {
            if (!calendar) return;
            const view = calendar.view;
            await Promise.all([
                loadContext(view.activeStart, view.activeEnd),
                new Promise(resolve => {
                    calendar.refetchEvents();
                    window.setTimeout(resolve, 120);
                })
            ]);
            if (elements.daySheet.classList.contains("is-open")) {
                await loadDayDetails(selectedDate);
            }
        }

        async function refreshContextForCurrentRange() {
            if (!calendar) return;
            await loadContext(calendar.view.activeStart, calendar.view.activeEnd);
        }

        async function loadContext(start, end) {
            const query = new URLSearchParams({
                start: formatLocalDate(start),
                end: formatLocalDate(end)
            });
            const result = await getJson(`${ENDPOINTS.context}?${query}`);
            if (!result.success || !result.data) {
                throw new Error(result.message || "تعذر تحميل سياق التقويم.");
            }
            context = result.data;
            renderContext(context);
        }

        function renderContext(data) {
            elements.placeName.textContent = data.placeName || "المنشأة الحالية";
            elements.placeCategory.textContent = data.categoryName || "منشأة";
            if (elements.manualFacilityName) elements.manualFacilityName.textContent = data.placeName || "المنشأة الحالية";
            if (elements.manualFacilityTemplate) elements.manualFacilityTemplate.textContent = data.manualBookingTemplateLabel || data.categoryName || "قالب الحجز";
            syncManualTemplateUi();

            setText("#ygKpiTotal", data.totalBookingsInRange ?? 0);
            setText("#ygKpiConfirmed", data.confirmedBookingsInRange ?? 0);
            setText("#ygKpiAwaiting", data.awaitingPaymentBookingsInRange ?? 0);
            setText("#ygKpiManual", data.manualBookingsInRange ?? 0);
            setText("#ygKpiOnline", data.onlineBookingsInRange ?? 0);
            setText("#ygKpiOccupied", data.occupiedRoomUnitsToday ?? 0);
            setText("#ygKpiAvailable", data.availableRoomUnitsToday ?? 0);
            setText("#ygKpiOccupancy", `${formatNumber(data.occupancyPercentToday ?? 0)}%`);
            setText("#ygKpiClosed", data.closedDaysInRange ?? 0);

            setText("#ygPolicyCheckIn", normalizeClock(data.checkInTime) || "--");
            setText("#ygPolicyCheckOut", normalizeClock(data.checkOutTime) || "--");
            setText("#ygPolicyNotice", `${data.minimumNoticeHours ?? 0} س`);
            setText("#ygPolicyBuffer", `${data.bufferTimeHours ?? 0} س`);
            setText("#ygPolicyHorizon", (data.maximumHorizonMonths ?? 0) > 0
                ? `${data.maximumHorizonMonths} شهر`
                : "غير محدد");

            const roomTab = $('[data-resource-kind="room"]');
            const packageTab = $('[data-resource-kind="package"]');
            if (roomTab) roomTab.hidden = data.supportsRooms === false;
            if (packageTab) packageTab.hidden = data.supportsPackages === false;

            if (data.supportsRooms === false && data.supportsPackages !== false) {
                setResourceKind("package");
            } else if (data.supportsRooms !== false) {
                setResourceKind(manualResourceKind === "package" && data.supportsPackages !== false ? "package" : "room");
            }
        }

        async function openDayDetails(date) {
            selectedDate = date;
            closePanel(elements.manualOverlay, elements.manualModal, true);
            closePanel(elements.rangeOverlay, elements.rangeModal, true);
            elements.dayTitle.textContent = formatDateArabic(date);
            elements.daySubtitle.textContent = "نقرأ الحجوزات والتوفر الفعلي…";
            elements.dayBookingsList.innerHTML = daySkeleton();
            openPanel(elements.dayOverlay, elements.daySheet);
            await loadDayDetails(date);
        }

        async function loadDayDetails(date) {
            try {
                const result = await getJson(`${ENDPOINTS.day}?date=${encodeURIComponent(date)}`);
                if (!result.success || !result.data) throw new Error(result.message || "تعذر تحميل اليوم.");
                renderDayDetails(result.data);
            } catch (error) {
                elements.dayBookingsList.innerHTML = emptyState("bi-exclamation-triangle", "تعذر تحميل اليوم", error.message || "حدث خطأ.");
                handleError(error, false);
            }
        }

        function renderDayDetails(data) {
            selectedDate = formatLocalDate(new Date(data.date));
            elements.dayTitle.textContent = formatDateArabic(selectedDate);
            const visibleBookings = filterBookingItems(Array.isArray(data.bookings) ? data.bookings : []);
            elements.dayBookingsCount.textContent = String(visibleBookings.length);
            elements.dayOccupied.textContent = String(data.occupiedRoomUnits ?? 0);
            elements.dayAvailable.textContent = String(data.availableRoomUnits ?? 0);
            elements.dayOccupancy.textContent = `${formatNumber(data.occupancyPercent ?? 0)}%`;
            elements.dayListCount.textContent = String(visibleBookings.length);

            const stateMessages = [];
            if (data.isWeeklyClosed) stateMessages.push("هذا اليوم مغلق أسبوعيًا حسب سياسة المنشأة.");
            if (data.isExceptionClosed) stateMessages.push(data.closedReason ? `إغلاق استثنائي: ${data.closedReason}` : "اليوم مغلق استثنائيًا من التقويم.");

            if (stateMessages.length) {
                elements.dayState.hidden = false;
                elements.dayState.textContent = stateMessages.join(" ");
            } else {
                elements.dayState.hidden = true;
                elements.dayState.textContent = "";
            }

            elements.dayReopen.style.display = data.isExceptionClosed ? "flex" : "none";
            elements.dayClose.style.display = data.isExceptionClosed ? "none" : "flex";

            const blockingBookingsCount =
                Number(data.confirmedCount || 0) +
                Number(data.awaitingPaymentCount || 0) +
                Number(data.paymentReviewCount || 0) +
                Number(data.pendingCount || 0);

            elements.dayClose.disabled = blockingBookingsCount > 0;
            elements.dayClose.title = blockingBookingsCount > 0
                ? `لا يمكن إغلاق اليوم لوجود ${blockingBookingsCount} حجز نشط.`
                : "إغلاق اليوم ومنع حجوزات جديدة";

            const isPast = compareDateStrings(selectedDate, formatLocalDate(new Date())) < 0;
            elements.dayCreate.disabled = isPast || data.isWeeklyClosed || data.isExceptionClosed;
            elements.dayCreate.title = isPast
                ? "لا يمكن إنشاء حجز بتاريخ سابق"
                : (data.isWeeklyClosed || data.isExceptionClosed)
                    ? "اليوم مغلق"
                    : "إضافة حجز مباشر";

            elements.dayBookingsList.innerHTML = visibleBookings.length
                ? visibleBookings.map(renderBookingCard).join("")
                : emptyState(
                    calendarScope === "history" ? "bi-clock-history" : "bi-calendar2-heart",
                    calendarScope === "history" ? "لا توجد حالات تاريخية في هذا اليوم" : "لا توجد حجوزات ضمن الفلتر الحالي",
                    calendarScope === "history" ? "الحالات الملغاة والمنتهية والمكتملة ستظهر هنا." : "غيّر الفلتر إلى الكل أو التاريخية إذا أردت مراجعة بقية الحالات."
                );
            elements.daySubtitle.textContent = buildDaySubtitle(data, visibleBookings);
            updatePaymentCountdowns();
        }

        function renderBookingCard(item) {
            const manual = item.bookingSource === "OwnerManual";
            const resource = item.roomName || item.packageName || "حجز";
            const unitText = item.assignedUnitsText ? ` · ${item.assignedUnitsText}` : "";
            const stateClass = stateBadgeClass(item.bookingState);
            const sourceBadge = manual
                ? '<span class="yg-mini-badge yg-mini-badge--manual"><i class="bi bi-telephone-inbound-fill"></i> مباشر</span>'
                : '<span class="yg-mini-badge"><i class="bi bi-globe2"></i> أونلاين</span>';

            const isInitialManualPayment = item.bookingState === "AwaitingPayment";
            const paymentAction = item.canRecordManualPayment
                ? `<button type="button"
                           class="yg-record-payment-btn"
                           data-record-payment="${item.bookingId}"
                           data-payment-remaining="${Number(item.remainingAmount || 0)}"
                           data-payment-mode="${isInitialManualPayment ? "initial" : "remaining"}">
                       <i class="bi bi-cash-coin"></i>
                       ${isInitialManualPayment ? "تسجيل العربون" : "تحصيل المتبقي"}
                   </button>`
                : "";

            const countdown = item.bookingState === "AwaitingPayment" && item.timerExpiredAt
                ? `<div class="yg-payment-countdown" data-countdown-until="${escapeHtml(item.timerExpiredAt)}">
                    <i class="bi bi-hourglass-split"></i>
                    <span>جاري حساب المهلة…</span>
                   </div>`
                : "";

            const note = item.ownerInternalNotes
                ? `<div class="yg-booking-resource"><i class="bi bi-sticky-fill"></i><span><strong>ملاحظة:</strong> ${escapeHtml(item.ownerInternalNotes)}</span></div>`
                : "";

            const statusReason = item.statusReason
                ? `<div class="yg-booking-state-note"><i class="bi bi-info-circle-fill"></i><span>${escapeHtml(item.statusReason)}</span></div>`
                : "";

            const refundBadge = item.refundStatus && item.refundStatus !== "NotApplicable"
                ? `<span class="yg-mini-badge yg-mini-badge--refund"><i class="bi bi-arrow-counterclockwise"></i>${escapeHtml(refundLabel(item.refundStatus))}</span>`
                : "";

            return `
                <article class="yg-booking-card yg-booking-card--${calendarStateKey(item.bookingState)} ${manual ? "yg-booking-card--manual" : ""} ${item.isHistorical ? "is-historical" : ""}">
                    <div class="yg-booking-card-top">
                        <div class="yg-booking-person">
                            <span class="yg-booking-avatar">${escapeHtml(initialLetter(item.customerName))}</span>
                            <div>
                                <strong>#${item.bookingId} · ${escapeHtml(item.customerName || "ضيف")}</strong>
                                <span>${escapeHtml(item.customerPhone || "بدون هاتف")}</span>
                            </div>
                        </div>
                        <div class="yg-booking-badges">
                            ${sourceBadge}
                            <span class="yg-mini-badge ${stateClass}"><i class="bi ${stateIconClass(item.bookingState)}"></i>${escapeHtml(stateLabel(item.bookingState))}</span>
                            <span class="yg-mini-badge"><i class="bi bi-wallet2"></i>${escapeHtml(paymentLabel(item.paymentStatus))}</span>
                            ${refundBadge}
                        </div>
                    </div>
                    <div class="yg-booking-resource">
                        <i class="bi ${item.roomId ? "bi-door-open-fill" : "bi-stars"}"></i>
                        <span><strong>${escapeHtml(resource)}</strong>${escapeHtml(unitText)}<br>${item.quantity || 1} × حجز · ${(item.adultsCount || 0) + (item.childrenCount || 0)} أشخاص</span>
                    </div>
                    ${note}
                    ${statusReason}
                    ${countdown}
                    <div class="yg-booking-finance">
                        <div><small>الإجمالي</small><strong>${money(item.totalPrice)}</strong></div>
                        <div><small>المدفوع</small><strong>${money(item.paidAmount)}</strong></div>
                        <div><small>المتبقي</small><strong>${money(item.remainingAmount)}</strong></div>
                    </div>
                    <div class="yg-booking-card-actions">
                        ${paymentAction}
                        <button type="button" data-open-booking="${item.bookingId}"><i class="bi bi-box-arrow-up-left"></i> فتح الحجز</button>
                    </div>
                </article>`;
        }

        function buildDaySubtitle(data, visibleBookings = []) {
            const scopeLabel = calendarScope === "history" ? "الحالات التاريخية" : calendarScope === "all" ? "كل الحالات" : "الحجوزات النشطة";
            const manual = visibleBookings.filter(item => item.bookingSource === "OwnerManual").length;
            const online = Math.max(0, visibleBookings.length - manual);
            const parts = [`${scopeLabel}: ${visibleBookings.length}`];
            if (manual > 0) parts.push(`${manual} مباشر`);
            if (online > 0) parts.push(`${online} أونلاين`);
            return parts.join(" · ");
        }

        function closeDaySheet(immediate = false) {
            closePanel(elements.dayOverlay, elements.daySheet, immediate);
        }

        function syncScopeButtons() {
            $$('[data-calendar-scope]').forEach(button => {
                const active = button.dataset.calendarScope === calendarScope;
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-pressed", active ? "true" : "false");
            });
        }

        function isHistoricalBookingState(state) {
            return historicalBookingStates.has(String(state || "").trim());
        }

        function filterBookingItems(items) {
            if (calendarScope === "all") return [...(items || [])];
            return (items || []).filter(item => {
                const historical = item.isHistorical === true || isHistoricalBookingState(item.bookingState);
                return calendarScope === "history" ? historical : !historical;
            });
        }

        function filterCalendarEvents(events) {
            return (events || []).filter(item => {
                const props = item.extendedProps || item.ExtendedProps || item;
                const type = props.eventType || props.EventType || item.eventType || item.EventType || "booking";
                if (type !== "booking") return true;
                if (calendarScope === "all") return true;
                const state = props.state || props.State || item.state || item.State || "";
                const historical = props.isHistorical === true || props.IsHistorical === true || isHistoricalBookingState(state);
                return calendarScope === "history" ? historical : !historical;
            });
        }

        function updatePaymentCountdowns() {
            const nodes = $$('[data-countdown-until]');
            if (!nodes.length) return;
            const now = Date.now();

            nodes.forEach(node => {
                const target = new Date(node.dataset.countdownUntil || "").getTime();
                const text = node.querySelector("span");
                if (!Number.isFinite(target) || !text) return;

                const remaining = target - now;
                if (remaining <= 0) {
                    node.classList.add("is-expired");
                    text.textContent = "انتهت مهلة العربون — جاري تحرير التوفر";
                    const card = node.closest(".yg-booking-card");
                    card?.querySelector("[data-record-payment]")?.setAttribute("disabled", "disabled");

                    if (node.dataset.expiryHandled !== "1") {
                        node.dataset.expiryHandled = "1";
                        window.setTimeout(() => {
                            refreshAll().catch(() => { });
                        }, 500);
                    }
                    return;
                }

                node.classList.remove("is-expired");
                const totalSeconds = Math.floor(remaining / 1000);
                const hours = Math.floor(totalSeconds / 3600);
                const minutes = Math.floor((totalSeconds % 3600) / 60);
                const seconds = totalSeconds % 60;
                text.textContent = `متبقي للعربون ${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
            });
        }

        function isPastDate(date) {
            if (!date) return false;
            return compareDateStrings(date, formatLocalDate(new Date())) < 0;
        }

        function showPastDateWarning() {
            const title = "يوم منتهي";
            const message = "لا يمكنك إنشاء حجز جديد في تاريخ سابق. يمكنك فقط مراجعة الحجوزات السابقة من سجل الحجوزات.";

            if (window.Swal && typeof window.Swal.fire === "function") {
                window.Swal.fire({
                    icon: "info",
                    title,
                    text: message,
                    confirmButtonText: "حسنًا",
                    confirmButtonColor: "#00A6DA",
                    customClass: { popup: "yg-swal-popup" }
                });
                return;
            }

            toast("error", title, message);
        }

        function clearManualAlert() {
            if (!elements.manualFormAlert) return;
            elements.manualFormAlert.hidden = true;
            elements.manualFormAlert.classList.remove("is-error", "is-warning", "is-success");
            if (elements.manualFormAlertText) elements.manualFormAlertText.textContent = "";
        }

        function showManualAlert(message, type = "error") {
            if (!elements.manualFormAlert || !message) return;
            elements.manualFormAlert.hidden = false;
            elements.manualFormAlert.classList.remove("is-error", "is-warning", "is-success");
            elements.manualFormAlert.classList.add(`is-${type}`);
            if (elements.manualFormAlertText) elements.manualFormAlertText.textContent = message;
        }

        function fieldNode(key) {
            return elements.manualModal?.querySelector(`[data-yg-field="${key}"]`) || null;
        }

        function errorNode(key) {
            return elements.manualModal?.querySelector(`[data-yg-error="${key}"]`) || null;
        }

        function setFieldError(key, message) {
            const field = fieldNode(key);
            const error = errorNode(key);
            field?.classList.add("is-invalid");
            if (error) {
                error.textContent = message || "";
                error.hidden = !message;
            }
        }

        function clearFieldError(key) {
            const field = fieldNode(key);
            const error = errorNode(key);
            field?.classList.remove("is-invalid");
            if (error) {
                error.textContent = "";
                error.hidden = true;
            }
        }

        function clearAllFieldErrors() {
            elements.manualModal?.querySelectorAll("[data-yg-field].is-invalid")
                .forEach(node => node.classList.remove("is-invalid"));
            elements.manualModal?.querySelectorAll("[data-yg-error]")
                .forEach(node => {
                    node.textContent = "";
                    node.hidden = true;
                });
            clearManualAlert();
        }

        function firstInvalidField() {
            return elements.manualModal?.querySelector("[data-yg-field].is-invalid") || null;
        }

        function focusFirstInvalidField() {
            const field = firstInvalidField();
            if (!field) return;
            const focusable = field.querySelector("input:not([type=hidden]),button,select,textarea");
            focusable?.focus({ preventScroll: true });
            field.scrollIntoView({ block: "nearest", behavior: "smooth" });
        }

        function currentManualTemplate() {
            return String(context?.manualBookingTemplate || "Hybrid");
        }

        function syncManualTemplateUi() {
            const template = currentManualTemplate();
            const category = context?.categoryName || "منشأة";
            const placeName = context?.placeName || "المنشأة الحالية";
            const supportsRooms = context?.supportsRooms === true;
            const supportsPackages = context?.supportsPackages === true;

            if (elements.manualModal) elements.manualModal.dataset.facilityTemplate = template;
            if (elements.manualFacilityName) elements.manualFacilityName.textContent = placeName;
            if (elements.manualFacilityTemplate) elements.manualFacilityTemplate.textContent = context?.manualBookingTemplateLabel || category;
            if (elements.manualTemplateName) elements.manualTemplateName.textContent = context?.manualBookingTemplateLabel || "قالب الحجز";
            if (elements.manualTemplateCategory) elements.manualTemplateCategory.textContent = category;

            const venueMode = ["Venue", "VenueHybrid"].includes(template);
            const chaletMode = template === "Chalet";
            const singleUnitMode = template === "SingleUnit";
            const lodgingMode = ["Lodging", "SingleUnit"].includes(template);
            const packageTemplate = venueMode || chaletMode;
            const trueHybridMode = template === "Hybrid" && supportsRooms && supportsPackages;

            if (elements.bookingTemplateTitle) {
                elements.bookingTemplateTitle.textContent = venueMode
                    ? "المناسبة والفترة"
                    : chaletMode
                        ? "الموعد والباقة"
                        : singleUnitMode
                            ? "الموعد والوحدة"
                            : lodgingMode
                                ? "الموعد والغرفة"
                                : "الحجز والتوفر";
            }
            if (elements.bookingTemplateDescription) {
                elements.bookingTemplateDescription.textContent = venueMode
                    ? "اختر تاريخ المناسبة والباقات أو الفترات المتاحة لهذه القاعة فقط."
                    : chaletMode
                        ? "اختر تاريخ الحجز والباقة التابعة للشاليه أو المزرعة؛ لن تظهر أقسام لا تخص هذه المنشأة."
                        : singleUnitMode
                            ? "حدد فترة الإقامة، وسيعرض النظام الوحدة المتاحة دون أقسام إضافية."
                            : lodgingMode
                                ? "حدد الفترة ونوع الغرفة، ثم اختر الدور والوحدات الفعلية المتاحة."
                                : "يعرض النظام خيارات الحجز الفعلية المرتبطة بهذه المنشأة فقط.";
            }
            if (elements.startDateLabel) {
                elements.startDateLabel.textContent = venueMode
                    ? "تاريخ المناسبة"
                    : chaletMode
                        ? "تاريخ الحجز"
                        : "تاريخ الدخول";
            }
            if (elements.endDateLabel) elements.endDateLabel.textContent = "تاريخ الخروج";
            if (elements.manualTemplateIcon) {
                elements.manualTemplateIcon.innerHTML = venueMode
                    ? '<i class="bi bi-stars"></i>'
                    : chaletMode
                        ? '<i class="bi bi-house-heart"></i>'
                        : singleUnitMode
                            ? '<i class="bi bi-house-heart"></i>'
                            : '<i class="bi bi-building"></i>';
            }

            const resourceTabs = $("#ygResourceTabs");
            if (resourceTabs) resourceTabs.hidden = !trueHybridMode;

            // لا نعرض أقسامًا لا تخص نوع المنشأة.
            if (packageTemplate && supportsPackages) {
                setResourceKind("package");
            } else if (lodgingMode && supportsRooms) {
                setResourceKind("room");
            } else if (!supportsRooms && supportsPackages) {
                setResourceKind("package");
            } else if (supportsRooms && !supportsPackages) {
                setResourceKind("room");
            } else if (trueHybridMode) {
                setResourceKind(manualResourceKind);
            } else {
                if (elements.roomPanel) elements.roomPanel.hidden = true;
                if (elements.packagePanel) elements.packagePanel.hidden = true;
                if (elements.endDateField) elements.endDateField.hidden = true;
                if (elements.optionsStatus) {
                    elements.optionsStatus.hidden = false;
                    elements.optionsStatus.classList.add("is-danger");
                    elements.optionsStatus.textContent = "لا توجد خيارات حجز مفعلة لهذه المنشأة.";
                }
            }

            if (elements.venueSeatingField) {
                elements.venueSeatingField.hidden = !(venueMode && manualResourceKind === "package");
            }
        }

        function syncOtherFields() {
            const channelOther = getChannel() === "Other";
            if (elements.manualChannelOtherField) elements.manualChannelOtherField.hidden = !channelOther;
            if (!channelOther && elements.manualChannelOther) {
                elements.manualChannelOther.value = "";
                clearFieldError("manualChannelOther");
            }

            const paymentOther = elements.paymentMethod?.value === "Other";
            if (elements.paymentMethodOtherField) elements.paymentMethodOtherField.hidden = !paymentOther;
            if (!paymentOther && elements.paymentMethodOther) {
                elements.paymentMethodOther.value = "";
                clearFieldError("paymentMethodOther");
            }

            const walletPayment = getPaymentMode() === "Collected" && elements.paymentMethod?.value === "Wallet";
            if (elements.ownerWalletField) elements.ownerWalletField.hidden = !walletPayment;
            if (!walletPayment && elements.ownerWalletId) {
                elements.ownerWalletId.value = "";
                updateSmartSelectValue(elements.ownerWalletId, elements.ownerWalletSelectText);
                clearFieldError("ownerWallet");
            }

            const seatingOther = elements.seating?.value === "Other";
            if (elements.seatingOtherField) elements.seatingOtherField.hidden = !seatingOther;
            if (!seatingOther && elements.seatingOther) {
                elements.seatingOther.value = "";
                clearFieldError("seatingOther");
            }
        }

        function initializeSmartSelect(select, trigger, menu, textNode) {
            if (!select || !trigger || !menu) return;

            const refresh = () => refreshSmartSelect(select, trigger, menu, textNode);
            refresh();

            on(trigger, "click", event => {
                event.preventDefault();
                const willOpen = menu.hidden;
                closeAllSmartSelects(menu);
                menu.hidden = !willOpen;
                trigger.classList.toggle("is-open", willOpen);
            });

            on(menu, "click", event => {
                const item = event.target.closest("[data-select-value]");
                if (!item || item.classList.contains("is-disabled")) return;
                select.value = item.dataset.selectValue || "";
                menu.hidden = true;
                trigger.classList.remove("is-open");
                updateSmartSelectValue(select, textNode);
                select.dispatchEvent(new Event("change", { bubbles: true }));
            });

            select._ygSmartRefresh = refresh;
        }

        function refreshSmartSelect(select, trigger, menu, textNode) {
            if (!select || !trigger || !menu) return;
            const options = Array.from(select.options || []);
            menu.innerHTML = options.map(option => `
                <button type="button"
                        class="yg-v8-select-option ${option.disabled ? "is-disabled" : ""} ${option.selected ? "is-selected" : ""}"
                        data-select-value="${escapeHtml(option.value)}"
                        ${option.disabled ? "disabled" : ""}>
                    <span>${escapeHtml(option.textContent || option.label || "")}</span>
                    <i class="bi ${option.selected ? "bi-check2-circle" : "bi-circle"}"></i>
                </button>`).join("");
            updateSmartSelectValue(select, textNode);
        }

        function updateSmartSelectValue(select, textNode) {
            if (!select || !textNode) return;
            const option = select.options?.[select.selectedIndex];
            textNode.textContent = option?.textContent?.trim() || "اختر";
            const menu = select._ygSmartRefresh ? select.closest?.(".yg-v8-smart-select")?.querySelector(".yg-v8-select-menu") : null;
            if (menu && !menu.hidden) select._ygSmartRefresh();
        }

        function closeAllSmartSelects(exceptMenu = null) {
            $$(".yg-v8-select-menu").forEach(menu => {
                if (menu === exceptMenu) return;
                menu.hidden = true;
                menu.closest(".yg-v8-smart-select")?.querySelector(".yg-v8-select-trigger")?.classList.remove("is-open");
            });
        }

        function refreshDynamicSmartSelect(select) {
            if (select && typeof select._ygSmartRefresh === "function") select._ygSmartRefresh();
        }

        async function openManualStudio(date) {
            if (!date) date = formatLocalDate(new Date());
            if (isPastDate(date)) {
                showPastDateWarning();
                return;
            }

            selectedDate = date;
            closePanel(elements.dayOverlay, elements.daySheet, true);
            closePanel(elements.rangeOverlay, elements.rangeModal, true);
            resetManualForm(date);
            syncManualTemplateUi();
            setManualStep(1);
            openPanel(elements.manualOverlay, elements.manualModal);
            initializePhoneInputs();
            await loadManualOptions();
        }

        function closeManualStudio() {
            window.clearTimeout(quoteTimer);
            closePanel(elements.manualOverlay, elements.manualModal);
        }

        function resetManualForm(date) {
            linkedCustomer = null;
            manualOptions = null;
            setPhoneValue(elements.phoneSearch, phoneSearchIti, "");
            elements.customerId.value = "";
            elements.guestName.value = "";
            setPhoneValue(elements.guestPhone, guestPhoneIti, "");
            elements.guestEmail.value = "";
            if (elements.manualChannelOther) elements.manualChannelOther.value = "";
            if (elements.seatingOther) elements.seatingOther.value = "";
            if (elements.paymentMethodOther) elements.paymentMethodOther.value = "";
            if (elements.ownerWalletId) elements.ownerWalletId.value = "";
            latestManualQuote = null;
            elements.startDate.value = date;
            elements.endDate.value = formatLocalDate(addDays(parseLocalDate(date), 1));
            elements.quantity.value = "1";
            elements.adults.value = "1";
            elements.children.value = "0";
            elements.roomId.innerHTML = '<option value="">اختر نوع الغرفة</option>';
            if (elements.roomUnitPicker) elements.roomUnitPicker.hidden = true;
            if (elements.roomUnitChoices) elements.roomUnitChoices.innerHTML = "";
            if (elements.roomFloorTabs) elements.roomFloorTabs.innerHTML = "";
            activeRoomFloorKey = "";
            selectedRoomUnitIds.clear();
            if (elements.roomUnitCounter) elements.roomUnitCounter.textContent = "0 / 0";
            elements.packageId.innerHTML = '<option value="">اختر الباقة</option>';
            if (elements.packageCards) elements.packageCards.innerHTML = "";
            elements.services.innerHTML = '<div class="yg-services-empty">جاري تحميل الخدمات…</div>';
            elements.promoCode.value = "";
            elements.seating.value = "";
            elements.notes.value = "";
            elements.paidAmount.value = "0";
            elements.paymentMethod.value = "Cash";
            refreshDynamicSmartSelect(elements.roomId);
            refreshDynamicSmartSelect(elements.seating);
            refreshDynamicSmartSelect(elements.paymentMethod);
            refreshDynamicSmartSelect(elements.ownerWalletId);
            elements.paymentReference.value = "";
            const unpaid = $('input[name="ygPaymentMode"][value="Unpaid"]');
            if (unpaid) unpaid.checked = true;
            const phone = $('input[name="ygManualChannel"][value="Phone"]');
            if (phone) phone.checked = true;
            elements.linkedCustomer.hidden = true;
            elements.guestPanel.hidden = false;
            elements.optionsStatus.hidden = true;
            elements.roomHint.className = "yg-availability-inline";
            elements.roomHint.textContent = "جاري تحميل التوفر…";
            elements.packageHint.className = "yg-availability-inline";
            elements.packageHint.textContent = "جاري تحميل التوفر…";
            clearQuote();
            clearAllFieldErrors();
            syncPaymentFields();
            syncOtherFields();
            syncManualTemplateUi();

            if (context?.supportsRooms === false && context?.supportsPackages !== false) setResourceKind("package");
            else if (["Venue", "VenueHybrid", "Chalet"].includes(currentManualTemplate()) && context?.supportsPackages) setResourceKind("package");
            else setResourceKind("room");
            setManualStep(1, false);
        }

        function setResourceKind(kind) {
            const requested = kind === "package" ? "package" : "room";
            if (requested === "room" && context?.supportsRooms === false && context?.supportsPackages !== false) {
                manualResourceKind = "package";
            } else if (requested === "package" && context?.supportsPackages === false && context?.supportsRooms !== false) {
                manualResourceKind = "room";
            } else {
                manualResourceKind = requested;
            }

            $$('[data-resource-kind]').forEach(button => {
                button.classList.toggle("is-active", button.dataset.resourceKind === manualResourceKind);
            });
            elements.roomPanel.hidden = manualResourceKind !== "room";
            elements.packagePanel.hidden = manualResourceKind !== "package";
            elements.endDateField.hidden = manualResourceKind !== "room";
            if (elements.venueSeatingField) {
                const venueTemplate = ["Venue", "VenueHybrid"].includes(currentManualTemplate());
                elements.venueSeatingField.hidden = manualResourceKind !== "package" || !venueTemplate;
            }

            if (manualResourceKind === "package") {
                elements.quantity.value = "1";
            }
            if (manualOptions) syncGuestCapacity(true);
            clearFieldError("roomId");
            clearFieldError("packageId");
            clearFieldError("roomUnits");
            scheduleQuotePreview();
        }

        async function handleManualDateChange() {
            if (!elements.startDate.value) return;
            if (elements.endDate.value && compareDateStrings(elements.endDate.value, elements.startDate.value) <= 0) {
                elements.endDate.value = formatLocalDate(addDays(parseLocalDate(elements.startDate.value), 1));
            }
            clearQuote();
            await loadManualOptions();
        }

        async function loadManualOptions() {
            const startDate = elements.startDate.value;
            if (!startDate) return;

            const endDate = elements.endDate.value || formatLocalDate(addDays(parseLocalDate(startDate), 1));
            elements.roomId.disabled = true;
            elements.packageId.disabled = true;
            elements.optionsStatus.hidden = true;

            try {
                const query = new URLSearchParams({ startDate, endDate });
                const result = await getJson(`${ENDPOINTS.options}?${query}`);
                if (!result.success || !result.data) throw new Error(result.message || "تعذر تحميل التوفر.");
                manualOptions = result.data;
                renderManualOptions(manualOptions);
            } catch (error) {
                manualOptions = null;
                elements.optionsStatus.hidden = false;
                elements.optionsStatus.textContent = error.message || "تعذر تحميل خيارات الحجز.";
                handleError(error, false);
            } finally {
                elements.roomId.disabled = false;
                elements.packageId.disabled = false;
            }
        }

        function renderManualOptions(data) {
            const previousRoom = elements.roomId.value;
            const previousPackage = elements.packageId.value;
            const template = currentManualTemplate();

            const rooms = Array.isArray(data.rooms) ? data.rooms : [];
            elements.roomId.innerHTML = '<option value="">اختر نوع الغرفة</option>' + rooms.map(room => {
                const disabled = (room.availableUnits ?? 0) <= 0;
                const meta = `${room.availableUnits ?? 0} متاح من ${room.activeUnits ?? 0} · ${money(room.price)} / ليلة`;
                return `<option value="${room.roomId}" ${disabled ? "disabled" : ""}>${escapeHtml(room.name || "غرفة")} — ${escapeHtml(meta)}</option>`;
            }).join("");

            if (rooms.some(room => String(room.roomId) === previousRoom && (room.availableUnits ?? 0) > 0)) {
                elements.roomId.value = previousRoom;
            } else if (template === "SingleUnit") {
                const firstAvailableRoom = rooms.find(room => (room.availableUnits ?? 0) > 0);
                if (firstAvailableRoom) elements.roomId.value = String(firstAvailableRoom.roomId);
                elements.quantity.value = "1";
            }
            refreshDynamicSmartSelect(elements.roomId);

            const roomTypeBlock = $("#ygRoomTypeSelectorBlock");
            if (roomTypeBlock) {
                roomTypeBlock.classList.toggle("is-single-unit-template", template === "SingleUnit");
            }

            const packages = Array.isArray(data.packages) ? data.packages : [];
            elements.packageId.innerHTML = '<option value="">اختر الباقة</option>' + packages.map(item => {
                const timing = item.timeFrame || [timeSpanText(item.startTime), timeSpanText(item.endTime)].filter(Boolean).join(" - ");
                const meta = `${timing ? `${timing} · ` : ""}${money(item.price)} · ${item.availabilityMessage || ""}`;
                return `<option value="${item.packageId}" ${item.isAvailable ? "" : "disabled"}>${escapeHtml(item.name || "باقة")} — ${escapeHtml(meta)}</option>`;
            }).join("");
            if (packages.some(item => String(item.packageId) === previousPackage && item.isAvailable)) {
                elements.packageId.value = previousPackage;
            } else if (["Venue", "VenueHybrid", "Chalet"].includes(template)) {
                const firstAvailablePackage = packages.find(item => item.isAvailable);
                if (firstAvailablePackage && packages.length === 1) elements.packageId.value = String(firstAvailablePackage.packageId);
            }

            renderPackageCards();
            renderServices(Array.isArray(data.services) ? data.services : []);

            syncOwnerWalletChoices();

            if (data.isClosedByPolicy || data.isClosedByException) {
                elements.optionsStatus.hidden = false;
                elements.optionsStatus.classList.add("is-danger");
                elements.optionsStatus.textContent = data.closureMessage || "الفترة مغلقة حسب سياسة المنشأة.";
            } else {
                elements.optionsStatus.hidden = true;
                elements.optionsStatus.classList.remove("is-danger");
            }

            updateRoomHint();
            updatePackageHint();
            activeRoomFloorKey = "";
            renderRoomUnitChoices();
            syncManualTemplateUi();
            syncGuestCapacity(true);
            syncOtherFields();
            scheduleQuotePreview();
        }

        function renderPackageCards() {
            if (!elements.packageCards) return;
            const packages = Array.isArray(manualOptions?.packages) ? manualOptions.packages : [];
            const selectedId = Number(elements.packageId.value || 0);

            if (!packages.length) {
                elements.packageCards.innerHTML = '<div class="yg-v8-empty-choice"><i class="bi bi-calendar-x"></i><span>لا توجد باقات أو فترات مفعلة لهذه المنشأة.</span></div>';
                return;
            }

            elements.packageCards.innerHTML = packages.map(item => {
                const timing = item.timeFrame || [timeSpanText(item.startTime), timeSpanText(item.endTime)].filter(Boolean).join(" - ") || "فترة حسب الإعداد";
                const selected = selectedId === Number(item.packageId);
                return `
                    <button type="button"
                            class="yg-v8-package-card ${item.isAvailable ? "" : "is-disabled"} ${selected ? "is-selected" : ""}"
                            data-package-id="${item.packageId}"
                            ${item.isAvailable ? "" : "disabled"}>
                        <span class="yg-v8-package-icon"><i class="bi bi-stars"></i></span>
                        <span class="yg-v8-package-copy">
                            <strong>${escapeHtml(item.name || "باقة")}</strong>
                            <small>${escapeHtml(timing)}</small>
                            <em class="${item.isAvailable ? "is-good" : "is-bad"}">${escapeHtml(item.availabilityMessage || (item.isAvailable ? "متاحة" : "غير متاحة"))}</em>
                        </span>
                        <span class="yg-v8-package-price">${money(item.price)}</span>
                        <span class="yg-v8-package-check"><i class="bi bi-check2"></i></span>
                    </button>`;
            }).join("");
        }

        function renderServices(services) {
            if (!services.length) {
                elements.services.innerHTML = '<div class="yg-v8-empty-choice"><i class="bi bi-stars"></i><span>لا توجد خدمات إضافية مفعلة لهذه المنشأة.</span></div>';
                return;
            }

            elements.services.innerHTML = services.map(service => `
                <article class="yg-service-card yg-v8-service-card" data-service-id="${service.serviceId}">
                    <label class="yg-service-select yg-v8-service-select">
                        <input type="checkbox" value="${service.serviceId}" />
                        <span class="yg-v8-service-check"><i class="bi bi-check2"></i></span>
                        <span class="yg-v8-service-icon"><i class="bi bi-stars"></i></span>
                        <span class="yg-v8-service-copy">
                            <strong>${escapeHtml(service.serviceName || "خدمة إضافية")}</strong>
                            <small>${escapeHtml(service.description || "خدمة اختيارية")}</small>
                        </span>
                        <span class="yg-service-price">${money(service.price)}</span>
                    </label>
                    <div class="yg-service-qty yg-v8-service-qty"><span>الكمية</span><input type="number" min="1" max="100" value="1" /></div>
                </article>`).join("");
        }

        function updateRoomHint() {
            const roomId = Number(elements.roomId.value || 0);
            const room = manualOptions?.rooms?.find(item => item.roomId === roomId);
            if (!room) {
                elements.roomHint.className = "yg-availability-inline";
                elements.roomHint.textContent = "اختر نوع الغرفة لمشاهدة السعة والتوفر.";
                return;
            }
            elements.roomHint.className = `yg-availability-inline ${(room.availableUnits ?? 0) > 0 ? "is-good" : "is-bad"}`;
            const capacity = (room.maxAdults ?? 0) + (room.maxChildren ?? 0);
            const details = [
                `${room.availableUnits ?? 0} متاح من ${room.activeUnits ?? 0}`,
                capacity > 0 ? `سعة الوحدة ${capacity}` : "",
                room.bedsCount ? `${room.bedsCount} سرير` : "",
                room.areaSquareMeters ? `${formatNumber(room.areaSquareMeters)} م²` : ""
            ].filter(Boolean).join(" · ");
            elements.roomHint.textContent = details;
        }

        function normalizeRoomQuantity() {
            const roomId = Number(elements.roomId.value || 0);
            const room = manualOptions?.rooms?.find(item => item.roomId === roomId);
            if (!room) {
                elements.quantity.max = "1";
                return;
            }
            const available = Math.max(0, Number(room.availableUnits || 0));
            const safeMax = Math.max(1, available);
            const current = Math.max(1, Number(elements.quantity.value || 1));
            elements.quantity.max = String(safeMax);
            if (current > safeMax) elements.quantity.value = String(safeMax);
        }

        function syncGuestCapacity(resetToPolicyMaximum = false, changedKind = "") {
            if (!elements.adults || !elements.children) return;

            let maxAdults = 1000;
            let maxChildren = 1000;
            let maxTotal = 0;
            let hasPolicyLimit = false;

            if (manualResourceKind === "room") {
                const roomId = Number(elements.roomId.value || 0);
                const room = manualOptions?.rooms?.find(item => Number(item.roomId) === roomId);
                if (room) {
                    const quantity = Math.max(1, Number(elements.quantity.value || 1));
                    maxAdults = Math.max(1, Number(room.maxAdults || 0) * quantity);
                    maxChildren = Math.max(0, Number(room.maxChildren || 0) * quantity);
                    maxTotal = maxAdults + maxChildren;
                    hasPolicyLimit = true;
                }
            } else {
                const packageId = Number(elements.packageId.value || 0);
                const item = manualOptions?.packages?.find(pkg => Number(pkg.packageId) === packageId);
                const capacity = Math.max(0, Number(item?.capacity || 0));
                if (item && capacity > 0) {
                    maxAdults = capacity;
                    maxChildren = capacity;
                    maxTotal = capacity;
                    hasPolicyLimit = true;
                }
            }

            elements.adults.max = String(maxAdults);
            elements.children.max = String(maxChildren);

            if (resetToPolicyMaximum) {
                if (hasPolicyLimit && manualResourceKind === "room") {
                    elements.adults.value = String(maxAdults);
                    elements.children.value = String(maxChildren);
                } else if (hasPolicyLimit) {
                    elements.adults.value = String(maxTotal);
                    elements.children.value = "0";
                } else {
                    elements.adults.value = "1";
                    elements.children.value = "0";
                }
            } else {
                clampIntegerInput(elements.adults, 0, maxAdults);
                clampIntegerInput(elements.children, 0, maxChildren);

                if (hasPolicyLimit && manualResourceKind === "package") {
                    let adults = Math.max(0, Number(elements.adults.value || 0));
                    let children = Math.max(0, Number(elements.children.value || 0));
                    if (adults + children > maxTotal) {
                        if (changedKind === "children") {
                            children = Math.max(0, maxTotal - adults);
                            elements.children.value = String(children);
                        } else {
                            adults = Math.max(0, maxTotal - children);
                            elements.adults.value = String(adults);
                        }
                    }
                }
            }

            const adults = Math.max(0, Number(elements.adults.value || 0));
            const children = Math.max(0, Number(elements.children.value || 0));
            const totalReached = hasPolicyLimit && manualResourceKind === "package" && adults + children >= maxTotal;

            $$('[data-counter-target="ygManualAdults"]').forEach(button => {
                const delta = Number(button.dataset.counterDelta || 0);
                button.disabled = delta > 0 && (adults >= maxAdults || totalReached);
            });
            $$('[data-counter-target="ygManualChildren"]').forEach(button => {
                const delta = Number(button.dataset.counterDelta || 0);
                button.disabled = delta > 0 && (children >= maxChildren || totalReached);
            });
        }

        function renderRoomUnitChoices(resetFloor = true) {
            if (!elements.roomUnitPicker || !elements.roomUnitChoices) return;

            const roomId = Number(elements.roomId.value || 0);
            const room = manualOptions?.rooms?.find(item => item.roomId === roomId);
            const units = Array.isArray(room?.roomUnits) && room.roomUnits.length
                ? room.roomUnits
                : (Array.isArray(room?.availableRoomUnits) ? room.availableRoomUnits.map(unit => ({
                    ...unit,
                    isSelectable: true,
                    availabilityState: "Available",
                    availabilityLabel: "متاحة"
                })) : []);

            if (manualResourceKind !== "room" || !room || units.length === 0) {
                elements.roomUnitPicker.hidden = true;
                elements.roomUnitPicker.classList.remove("is-single-floor");
                elements.roomUnitChoices.innerHTML = "";
                if (elements.roomFloorTabs) elements.roomFloorTabs.innerHTML = "";
                if (elements.roomUnitCounter) elements.roomUnitCounter.textContent = "0 / 0";
                selectedRoomUnitIds.clear();
                return;
            }

            const availableIds = new Set(units.filter(unit => unit.isSelectable !== false).map(unit => Number(unit.roomUnitId)));
            Array.from(selectedRoomUnitIds).forEach(id => {
                if (!availableIds.has(Number(id))) selectedRoomUnitIds.delete(id);
            });

            const floorGroups = new Map();
            units.forEach(unit => {
                const floorNumber = Number(unit.floorNumber ?? 0);
                const floorLabel = String(unit.floorLabel || "").trim();
                const label = floorLabel || (floorNumber === 0 ? "الدور الأرضي" : `الدور ${floorNumber}`);
                const key = `${floorNumber}|${floorLabel || label}`;
                if (!floorGroups.has(key)) floorGroups.set(key, { key, label, floorNumber, units: [] });
                floorGroups.get(key).units.push(unit);
            });

            const floors = Array.from(floorGroups.values())
                .sort((a, b) => a.floorNumber - b.floorNumber || a.label.localeCompare(b.label, "ar"));
            if (!floors.length) {
                elements.roomUnitPicker.hidden = true;
                return;
            }

            if (resetFloor || !floors.some(floor => floor.key === activeRoomFloorKey)) {
                activeRoomFloorKey = floors.find(floor => floor.units.some(unit => unit.isSelectable !== false))?.key || floors[0].key;
            }

            elements.roomUnitPicker.hidden = false;
            elements.roomUnitPicker.classList.toggle("is-single-floor", floors.length === 1);

            if (elements.roomFloorTabs) {
                elements.roomFloorTabs.innerHTML = floors.map(floor => {
                    const available = floor.units.filter(unit => unit.isSelectable !== false).length;
                    const selectedOnFloor = floor.units.filter(unit => selectedRoomUnitIds.has(Number(unit.roomUnitId))).length;
                    const unavailable = Math.max(0, floor.units.length - available);
                    return `
                        <button type="button"
                                class="yg-v8-floor-tab ${floor.key === activeRoomFloorKey ? "is-active" : ""}"
                                data-floor-key="${escapeHtml(floor.key)}">
                            <span><i class="bi bi-layers"></i><b>${escapeHtml(floor.label)}</b></span>
                            <small>${available} متاحة${unavailable ? ` · ${unavailable} غير متاحة` : ""}</small>
                            ${selectedOnFloor ? `<em class="yg-mb9-floor-selected">${selectedOnFloor}</em>` : ""}
                        </button>`;
                }).join("");
            }

            const activeFloor = floors.find(floor => floor.key === activeRoomFloorKey) || floors[0];
            elements.roomUnitChoices.innerHTML = activeFloor.units.map(unit => {
                const selectable = unit.isSelectable !== false;
                const state = String(unit.availabilityState || (selectable ? "Available" : "Occupied"));
                const label = unit.availabilityLabel || (selectable ? "متاحة" : "غير متاحة");
                const id = Number(unit.roomUnitId || 0);
                const checked = selectable && selectedRoomUnitIds.has(id);
                const stateClass = roomUnitStateClass(state);
                const floor = unit.floorLabel || activeFloor.label;
                const extra = unit.blockingBookingId
                    ? `حجز #${unit.blockingBookingId}`
                    : (unit.notes || "");
                const actionText = selectable ? (checked ? "مختارة" : "اضغط للاختيار") : "غير قابلة للاختيار";

                return `
                    <label class="yg-unit-choice yg-v8-unit-card ${stateClass} ${checked ? "is-selected" : ""} ${selectable ? "" : "is-disabled"}"
                           data-room-unit-id="${id}"
                           ${selectable ? "" : 'aria-disabled="true"'}>
                        ${selectable ? `<input type="checkbox" value="${id}" ${checked ? "checked" : ""} />` : ""}
                        <span class="yg-v8-unit-card__top">
                            <span class="yg-v8-unit-card__number">${escapeHtml(unit.roomNumber || `#${id}`)}</span>
                            <span class="yg-v8-unit-card__state"><i class="bi ${roomUnitStateIcon(state)}"></i>${escapeHtml(label)}</span>
                        </span>
                        <span class="yg-v8-unit-card__meta"><i class="bi bi-layers"></i>${escapeHtml(floor || "دور غير محدد")}</span>
                        ${extra ? `<span class="yg-v8-unit-card__extra"><i class="bi bi-info-circle"></i>${escapeHtml(extra)}</span>` : ''}
                        ${selectable
                        ? `<span class="yg-v8-unit-card__check"><i class="bi ${checked ? "bi-check2" : "bi-hand-index-thumb"}"></i>${actionText}</span>`
                        : `<span class="yg-v8-unit-card__lock"><i class="bi bi-lock"></i>${actionText}</span>`}
                    </label>`;
            }).join("");

            normalizeRoomUnitSelection();
            updateRoomUnitVisuals();
        }

        function roomUnitStateClass(state) {
            switch (state) {
                case "Available": return "is-available";
                case "Held": return "is-held";
                case "PaymentReview": return "is-review";
                case "Pending": return "is-pending";
                case "Closed": return "is-closed";
                case "LegacyReserved": return "is-occupied";
                case "Occupied": return "is-occupied";
                default: return "is-occupied";
            }
        }

        function roomUnitStateIcon(state) {
            switch (state) {
                case "Available": return "bi-check2-circle";
                case "Held": return "bi-hourglass-split";
                case "PaymentReview": return "bi-credit-card-2-front";
                case "Pending": return "bi-clock-history";
                case "Closed": return "bi-tools";
                default: return "bi-door-closed";
            }
        }

        function normalizeRoomUnitSelection() {
            const wanted = Math.max(1, Number(elements.quantity.value || 1));
            if (selectedRoomUnitIds.size > wanted) {
                Array.from(selectedRoomUnitIds).slice(wanted).forEach(id => selectedRoomUnitIds.delete(id));
            }
            updateRoomUnitVisuals();
        }

        function updateRoomUnitVisuals() {
            if (!elements.roomUnitChoices) return;
            $$('input[type="checkbox"]', elements.roomUnitChoices).forEach(input => {
                const id = Number(input.value || 0);
                input.checked = selectedRoomUnitIds.has(id);
                input.closest('.yg-unit-choice')?.classList.toggle('is-selected', input.checked);
            });

            const wanted = Math.max(1, Number(elements.quantity.value || 1));
            if (elements.roomUnitCounter) {
                elements.roomUnitCounter.textContent = `${selectedRoomUnitIds.size} / ${wanted}`;
                elements.roomUnitCounter.classList.toggle('is-complete', selectedRoomUnitIds.size === wanted);
            }

            if (elements.roomFloorTabs) {
                elements.roomFloorTabs.querySelectorAll("[data-floor-key]").forEach(button => {
                    const floorKey = button.dataset.floorKey || "";
                    const roomId = Number(elements.roomId.value || 0);
                    const room = manualOptions?.rooms?.find(item => item.roomId === roomId);
                    const units = Array.isArray(room?.roomUnits) ? room.roomUnits : [];
                    const selectedOnFloor = units.filter(unit => {
                        const floorNumber = Number(unit.floorNumber ?? 0);
                        const floorLabel = String(unit.floorLabel || "").trim();
                        const label = floorLabel || (floorNumber === 0 ? "الدور الأرضي" : `الدور ${floorNumber}`);
                        const key = `${floorNumber}|${floorLabel || label}`;
                        return key === floorKey && selectedRoomUnitIds.has(Number(unit.roomUnitId));
                    }).length;
                    button.classList.toggle("has-selection", selectedOnFloor > 0);
                });
            }
        }

        function collectSelectedRoomUnitIds() {
            if (manualResourceKind !== "room" || elements.roomUnitPicker?.hidden) return [];
            return Array.from(selectedRoomUnitIds).map(Number).filter(id => id > 0);
        }

        function updatePackageHint() {
            const packageId = Number(elements.packageId.value || 0);
            const item = manualOptions?.packages?.find(pkg => pkg.packageId === packageId);
            if (!item) {
                elements.packageHint.className = "yg-availability-inline";
                elements.packageHint.textContent = "اختر الباقة لمعرفة الفترة والسعة.";
                return;
            }
            elements.packageHint.className = `yg-availability-inline ${item.isAvailable ? "is-good" : "is-bad"}`;
            const timing = item.timeFrame || [timeSpanText(item.startTime), timeSpanText(item.endTime)].filter(Boolean).join(" - ");
            elements.packageHint.textContent = [item.availabilityMessage, timing, item.capacity ? `السعة ${item.capacity}` : ""].filter(Boolean).join(" · ");
        }

        async function searchCustomer(rawPhone) {
            const phone = String(rawPhone || readPhoneValue(elements.phoneSearch, phoneSearchIti) || "").trim();
            clearFieldError("phoneSearch");
            clearManualAlert();

            if (!validatePhoneField(elements.phoneSearch, phoneSearchIti, elements.phoneSearchFeedback, true) || digitsOnly(phone).length < 7) {
                setFieldError("phoneSearch", "اكتب رقم هاتف صحيحًا قبل البحث.");
                focusFirstInvalidField();
                return;
            }

            elements.customerSearchLoader.hidden = false;
            try {
                const result = await getJson(`${ENDPOINTS.customer}?phone=${encodeURIComponent(phone)}`);
                if (!result.success || !result.data) throw new Error(result.message || "تعذر البحث عن العميل.");
                if (result.data.found && result.data.customerId) {
                    linkCustomer(result.data);
                    if (elements.phoneSearchFeedback) {
                        elements.phoneSearchFeedback.textContent = "تم العثور على حساب العميل وربطه بالحجز.";
                        elements.phoneSearchFeedback.className = "yg-field-feedback is-valid";
                    }
                } else {
                    unlinkCustomer(false);
                    setPhoneValue(elements.guestPhone, guestPhoneIti, phone);
                    validatePhoneField(elements.guestPhone, guestPhoneIti, elements.guestPhoneFeedback, false);
                    if (elements.phoneSearchFeedback) {
                        elements.phoneSearchFeedback.textContent = "لا يوجد حساب بهذا الرقم؛ أكمل بيانات الضيف أدناه.";
                        elements.phoneSearchFeedback.className = "yg-field-feedback is-info";
                    }
                }
            } catch (error) {
                showManualAlert(error?.message || "تعذر البحث عن العميل. تحقق من الاتصال وحاول مرة أخرى.");
            } finally {
                elements.customerSearchLoader.hidden = true;
            }
        }

        function linkCustomer(customer) {
            linkedCustomer = customer;
            elements.customerId.value = String(customer.customerId || "");
            elements.linkedCustomer.hidden = false;
            elements.guestPanel.hidden = true;
            elements.linkedCustomerName.textContent = customer.fullName || "عميل";
            elements.linkedCustomerPhone.textContent = customer.phoneNumber || "بدون هاتف";
            elements.linkedCustomerAvatar.innerHTML = `<span>${escapeHtml(initialLetter(customer.fullName))}</span>`;
            elements.linkedCustomerAvatar.style.backgroundImage = "";
            if (customer.profileImage) {
                elements.linkedCustomerAvatar.style.backgroundImage = `url("${safeCssUrl(customer.profileImage)}")`;
                elements.linkedCustomerAvatar.innerHTML = "";
            }
            scheduleQuotePreview();
        }

        function unlinkCustomer(clearPhone) {
            linkedCustomer = null;
            elements.customerId.value = "";
            elements.linkedCustomer.hidden = true;
            elements.guestPanel.hidden = false;
            elements.linkedCustomerAvatar.style.backgroundImage = "";
            if (clearPhone) {
                setPhoneValue(elements.phoneSearch, phoneSearchIti, "");
                setPhoneValue(elements.guestPhone, guestPhoneIti, "");
            } else if (elements.phoneSearch.value) {
                setPhoneValue(elements.guestPhone, guestPhoneIti, readPhoneValue(elements.phoneSearch, phoneSearchIti));
            }
            scheduleQuotePreview();
        }

        function syncPaymentFields() {
            const collected = getPaymentMode() === "Collected";
            elements.collectedFields.hidden = !collected;
            if (!collected) {
                elements.paidAmount.value = "0";
                elements.paidAmount.readOnly = false;
                elements.paymentReference.value = "";
                if (elements.paymentMethodOther) elements.paymentMethodOther.value = "";
                clearFieldError("paidAmount");
                clearFieldError("paymentMethod");
                clearFieldError("paymentMethodOther");
                clearFieldError("ownerWallet");
            } else {
                syncCollectedDepositFromQuote();
                syncOwnerWalletChoices();
            }
            syncOtherFields();
        }

        function syncOwnerWalletChoices() {
            if (!elements.ownerWalletId) return;

            const wallets = Array.isArray(manualOptions?.wallets)
                ? manualOptions.wallets
                : (Array.isArray(context?.wallets) ? context.wallets : []);

            const currentValue = elements.ownerWalletId.value;
            const currentStillExists = wallets.some(wallet =>
                String(wallet.ownerWalletId) === String(currentValue));

            elements.ownerWalletId.innerHTML =
                '<option value="">اختر المحفظة</option>' +
                wallets.map(wallet => {
                    const label = wallet.displayText ||
                        [wallet.walletName, wallet.accountNumber].filter(Boolean).join(" · ");
                    return `<option value="${wallet.ownerWalletId}">${escapeHtml(label || "محفظة")}</option>`;
                }).join("");

            if (currentStillExists) {
                elements.ownerWalletId.value = currentValue;
            } else if (wallets.length === 1) {
                // إذا كان لدى المالك حساب استقبال واحد فقط فاختياره تلقائيًا أوضح وأسرع.
                elements.ownerWalletId.value = String(wallets[0].ownerWalletId);
            } else {
                elements.ownerWalletId.value = "";
            }

            refreshDynamicSmartSelect(elements.ownerWalletId);
            updateSmartSelectValue(elements.ownerWalletId, elements.ownerWalletSelectText);
        }

        function syncCollectedDepositFromQuote(data = latestManualQuote) {
            if (!elements.paidAmount || getPaymentMode() !== "Collected") return;
            const deposit = Math.max(0, Number(data?.depositAmount || 0));
            if (deposit > 0) {
                elements.paidAmount.value = String(deposit);
                elements.paidAmount.readOnly = true;
                elements.paidAmount.title = "قيمة العربون محسوبة تلقائيًا من سياسة المنشأة.";
            } else {
                elements.paidAmount.readOnly = false;
                elements.paidAmount.removeAttribute("title");
            }
        }

        function getPaymentMode() {
            return $('input[name="ygPaymentMode"]:checked')?.value || "Unpaid";
        }

        function getChannel() {
            return $('input[name="ygManualChannel"]:checked')?.value || "Phone";
        }

        function collectSelectedServices() {
            return $$(".yg-service-card").map(card => {
                const checkbox = card.querySelector('input[type="checkbox"]');
                const quantity = card.querySelector('input[type="number"]');
                if (!checkbox?.checked) return null;
                return {
                    serviceId: Number(card.dataset.serviceId || checkbox.value || 0),
                    quantity: Math.max(1, Number(quantity?.value || 1))
                };
            }).filter(item => item && item.serviceId > 0);
        }

        function buildManualRequest() {
            const customerId = Number(elements.customerId.value || 0) || null;
            const paymentMode = getPaymentMode();
            const startDate = elements.startDate.value;
            const endDate = manualResourceKind === "room" ? elements.endDate.value : startDate;
            const channel = getChannel();
            const seatingValue = elements.seating?.value || "";
            const effectiveSeating = seatingValue === "Other"
                ? (elements.seatingOther?.value || "").trim()
                : seatingValue.trim();
            const selectedPaymentMethod = elements.paymentMethod?.value || "Cash";
            const effectivePaymentMethod = selectedPaymentMethod === "Other"
                ? (elements.paymentMethodOther?.value || "").trim()
                : selectedPaymentMethod;
            const ownerWalletId = selectedPaymentMethod === "Wallet"
                ? Number(elements.ownerWalletId?.value || 0) || null
                : null;

            return {
                customerId,
                guestName: customerId ? null : elements.guestName.value.trim(),
                guestPhone: customerId ? null : readPhoneValue(elements.guestPhone, guestPhoneIti),
                guestEmail: customerId ? null : elements.guestEmail.value.trim() || null,
                manualBookingChannel: channel,
                manualBookingChannelOther: channel === "Other" ? (elements.manualChannelOther?.value || "").trim() || null : null,
                roomId: manualResourceKind === "room" ? Number(elements.roomId.value || 0) || null : null,
                packageId: manualResourceKind === "package" ? Number(elements.packageId.value || 0) || null : null,
                startDate,
                endDate: endDate || null,
                quantity: manualResourceKind === "room" ? Math.max(1, Number(elements.quantity.value || 1)) : 1,
                selectedRoomUnitIds: manualResourceKind === "room" ? collectSelectedRoomUnitIds() : [],
                adultsCount: Math.max(0, Number(elements.adults.value || 0)),
                childrenCount: Math.max(0, Number(elements.children.value || 0)),
                bookingType: manualResourceKind === "room" ? "Room" : "Package",
                seatingSetup: manualResourceKind === "package" ? effectiveSeating || null : null,
                ownerInternalNotes: elements.notes.value.trim() || null,
                additionalServices: collectSelectedServices(),
                promotionId: null,
                promotionCode: elements.promoCode.value.trim() || null,
                paymentMode,
                paidAmount: paymentMode === "Collected" ? Math.max(0, Number(elements.paidAmount.value || 0)) : 0,
                paymentMethod: paymentMode === "Collected" ? effectivePaymentMethod || null : null,
                ownerWalletId: paymentMode === "Collected" ? ownerWalletId : null,
                paymentReferenceNumber: paymentMode === "Collected" ? elements.paymentReference.value.trim() || null : null
            };
        }

        function validateManualRequest(request, showMessage) {
            clearAllFieldErrors();
            let firstInvalidStep = 0;
            let firstMessage = "";

            for (const step of [1, 2, 3]) {
                const message = validateManualStepFields(step, request, false);
                if (message && !firstInvalidStep) {
                    firstInvalidStep = step;
                    firstMessage = message;
                }
            }

            if (firstInvalidStep && showMessage) {
                setManualStep(firstInvalidStep);
                window.setTimeout(focusFirstInvalidField, 60);
            }

            return firstMessage;
        }

        function validateManualStepFields(step, request, clearExisting = true) {
            if (clearExisting) {
                clearManualAlert();
                const stepNode = elements.manualModal?.querySelector(`[data-yg-step="${step}"]`);
                stepNode?.querySelectorAll("[data-yg-field]").forEach(node => node.classList.remove("is-invalid"));
                stepNode?.querySelectorAll("[data-yg-error]").forEach(node => {
                    node.textContent = "";
                    node.hidden = true;
                });
            }

            let firstMessage = "";
            const invalid = (key, message) => {
                if (!firstMessage) firstMessage = message;
                setFieldError(key, message);
            };

            if (step === 1) {
                if (!request.customerId) {
                    if (!request.guestName) invalid("guestName", "اكتب اسم الضيف.");
                    if (!request.guestPhone) {
                        invalid("guestPhone", "اكتب رقم هاتف الضيف.");
                    } else if (!validatePhoneField(elements.guestPhone, guestPhoneIti, elements.guestPhoneFeedback, true)) {
                        invalid("guestPhone", "رقم هاتف الضيف غير صحيح.");
                    }
                    if (elements.guestEmail?.value && !elements.guestEmail.checkValidity()) {
                        invalid("guestEmail", "تحقق من صيغة البريد الإلكتروني.");
                    }
                }

                if (request.manualBookingChannel === "Other" && !request.manualBookingChannelOther) {
                    invalid("manualChannelOther", "اكتب قناة الحجز الأخرى.");
                }
            }

            if (step === 2) {
                if (!request.startDate) {
                    invalid("startDate", "حدد تاريخ بداية الحجز.");
                } else if (isPastDate(request.startDate)) {
                    invalid("startDate", "لا يمكن إنشاء حجز في تاريخ سابق.");
                }

                const guests = Number(request.adultsCount || 0) + Number(request.childrenCount || 0);
                if (guests <= 0) invalid("guests", "حدد شخصًا واحدًا على الأقل.");

                if (manualResourceKind === "room") {
                    if (!request.roomId) {
                        invalid("roomId", "اختر نوع الغرفة أو الجناح.");
                    } else {
                        const room = manualOptions?.rooms?.find(item => Number(item.roomId) === Number(request.roomId));
                        const available = Math.max(0, Number(room?.availableUnits || 0));
                        if (request.quantity < 1) invalid("quantity", "الكمية يجب أن تكون وحدة واحدة على الأقل.");
                        else if (available > 0 && request.quantity > available) invalid("quantity", `المتاح من هذا النوع ${available} فقط.`);

                        const roomUnits = Array.isArray(room?.roomUnits) ? room.roomUnits : [];
                        if (roomUnits.length > 0 && request.selectedRoomUnitIds.length !== request.quantity) {
                            invalid("roomUnits", `اختر ${request.quantity} وحدة فعلية بالضبط. المختار الآن ${request.selectedRoomUnitIds.length}.`);
                        }
                    }

                    if (!request.endDate) {
                        invalid("endDate", "حدد تاريخ الخروج.");
                    } else if (request.startDate && compareDateStrings(request.endDate, request.startDate) <= 0) {
                        invalid("endDate", "تاريخ الخروج يجب أن يكون بعد تاريخ الدخول.");
                    }
                } else {
                    if (!request.packageId) {
                        invalid("packageId", "اختر الباقة أو الفترة المناسبة.");
                    } else {
                        const pkg = manualOptions?.packages?.find(item => Number(item.packageId) === Number(request.packageId));
                        if (pkg && pkg.isAvailable === false) invalid("packageId", pkg.availabilityMessage || "هذه الباقة غير متاحة.");
                    }

                    if (elements.seating?.value === "Other" && !elements.seatingOther?.value?.trim()) {
                        invalid("seatingOther", "اكتب إعداد الجلسة الآخر.");
                    }
                }
            }

            if (step === 3 && request.paymentMode === "Collected") {
                if (request.paidAmount < 0) invalid("paidAmount", "قيمة العربون غير صحيحة.");
                if (!request.paymentMethod) invalid("paymentMethod", "حدد طريقة الدفع.");
                if (elements.paymentMethod?.value === "Wallet" && !request.ownerWalletId) {
                    invalid("ownerWallet", "حدد نوع المحفظة التي استلمت عليها العربون.");
                }
                if (elements.paymentMethod?.value === "Other" && !elements.paymentMethodOther?.value?.trim()) {
                    invalid("paymentMethodOther", "اكتب طريقة الدفع الأخرى.");
                }
            }

            return firstMessage;
        }

        function applyManualServerError(message) {
            const text = String(message || "تعذر إكمال العملية.").trim();
            let step = manualStep;
            let key = "";

            if (text.includes("قناة الحجز الأخرى")) { step = 1; key = "manualChannelOther"; }
            else if (text.includes("اسم الضيف")) { step = 1; key = "guestName"; }
            else if (text.includes("هاتف") || text.includes("رقم هاتف")) { step = 1; key = "guestPhone"; }
            else if (text.includes("نوع الغرفة")) { step = 2; key = "roomId"; }
            else if (text.includes("تاريخ الخروج")) { step = 2; key = "endDate"; }
            else if (text.includes("تاريخ بداية") || text.includes("تاريخ سابق")) { step = 2; key = "startDate"; }
            else if (text.includes("الغرف الفعلية") || text.includes("الوحدات") || text.includes("لم تعد متاحة")) { step = 2; key = "roomUnits"; }
            else if (text.includes("الباقة") || text.includes("الفترة المختارة")) { step = 2; key = "packageId"; }
            else if (text.includes("عدد الأشخاص") || text.includes("سعة")) { step = 2; key = "guests"; }
            else if (text.includes("المبلغ المستلم") || text.includes("العربون المستلم") || text.includes("أقل من العربون") || text.includes("يتجاوز إجمالي")) { step = 3; key = "paidAmount"; }
            else if (text.includes("المحفظة")) { step = 3; key = "ownerWallet"; }
            else if (text.includes("طريقة دفع")) { step = 3; key = elements.paymentMethod?.value === "Other" ? "paymentMethodOther" : "paymentMethod"; }

            if (key) {
                setManualStep(step);
                setFieldError(key, text);
                window.setTimeout(focusFirstInvalidField, 60);
            } else {
                showManualAlert(text, "error");
            }
        }

        function scheduleQuotePreview() {
            window.clearTimeout(quoteTimer);
            clearQuote(false);
            if (manualStep !== 4) return;
            quoteTimer = window.setTimeout(() => previewManualBooking(false), 520);
        }

        async function previewManualBooking(showErrors) {
            const request = buildManualRequest();
            const validation = validateManualRequest(request, showErrors);
            if (validation) return null;

            setButtonLoading(elements.preview, true, "نفحص…");
            try {
                const result = await postJson(ENDPOINTS.preview, request);
                if (!result.success || !result.data) throw new Error(result.message || "تعذر معاينة الحجز.");
                renderQuote(result.data);
                return result.data;
            } catch (error) {
                clearQuote(false);
                if (showErrors) applyManualServerError(error?.message || "تعذر معاينة الحجز.");
                return null;
            } finally {
                setButtonLoading(elements.preview, false, "فحص السعر والتوفر");
            }
        }

        function renderQuote(data) {
            latestManualQuote = data;
            syncCollectedDepositFromQuote(data);
            elements.quotePlaceholder.hidden = true;
            elements.quoteContent.hidden = false;
            elements.quoteCustomer.textContent = data.customerDisplayName || "ضيف";
            elements.quoteBase.textContent = money(data.baseAmount);
            elements.quoteServices.textContent = money(data.servicesTotal);
            elements.quoteCleaning.textContent = money(data.cleaningFee);
            elements.quoteTaxPercent.textContent = `${formatNumber(data.taxPercentage ?? 0)}%`;
            elements.quoteTax.textContent = money(data.taxAmount);
            elements.quoteTotal.textContent = money(data.totalPrice);
            elements.quoteDeposit.textContent = money(data.depositAmount);
            elements.quoteRemaining.textContent = money(data.remainingAmount);

            const discount = Number(data.discountAmount || 0);
            elements.quoteDiscountRow.hidden = discount <= 0;
            elements.quoteDiscount.textContent = `-${money(discount)}`;

            const awaiting = data.proposedBookingState === "AwaitingPayment";
            elements.quoteState.classList.toggle("is-awaiting", awaiting);
            elements.quoteState.innerHTML = awaiting
                ? `<i class="bi bi-hourglass-split"></i> سيُحجز التوفر مؤقتًا بانتظار العربون ${money(data.depositAmount)}.`
                : '<i class="bi bi-patch-check-fill"></i> الحجز جاهز للتأكيد عند التثبيت.';

            const units = Array.isArray(data.availableUnits) ? data.availableUnits : [];
            if (units.length) {
                elements.quoteUnits.hidden = false;
                elements.quoteUnits.innerHTML = `<strong>الوحدات المرشحة:</strong> ${units.map(unit => escapeHtml([unit.roomNumber, unit.floorLabel].filter(Boolean).join(" · "))).join("، ")}`;
            } else {
                elements.quoteUnits.hidden = true;
                elements.quoteUnits.innerHTML = "";
            }

            if (data.promotionId || data.promotionMessage) {
                elements.quotePromo.hidden = false;
                elements.quotePromo.innerHTML = `<i class="bi bi-stars"></i> ${escapeHtml(data.promotionTitle || data.promotionMessage || "عرض مطبق")}${discount > 0 ? ` · توفير ${money(discount)}` : ""}`;
            } else {
                elements.quotePromo.hidden = true;
                elements.quotePromo.innerHTML = "";
            }
        }

        function clearQuote(full = true) {
            elements.quotePlaceholder.hidden = false;
            elements.quoteContent.hidden = true;
            if (full) {
                latestManualQuote = null;
                elements.quoteDiscountRow.hidden = true;
                elements.quoteUnits.hidden = true;
                elements.quotePromo.hidden = true;
            }
        }

        async function createManualBooking() {
            let request = buildManualRequest();
            const validation = validateManualRequest(request, true);
            if (validation) return;

            setButtonLoading(elements.create, true, "جاري التثبيت…");
            try {
                const preview = await previewManualBooking(true);
                if (!preview) return;

                syncCollectedDepositFromQuote(preview);
                request = buildManualRequest();

                const confirmed = await confirmCreate(request, preview);
                if (!confirmed) return;

                const result = await postJson(ENDPOINTS.create, request);
                if (!result.success) throw new Error(result.message || "تعذر إنشاء الحجز المباشر.");

                const date = request.startDate;
                closeManualStudio();
                await refreshAll();
                toast("success", "تم إنشاء الحجز", result.message || `تم إنشاء الحجز #${result.bookingId}.`);
                await openDayDetails(date);
            } catch (error) {
                applyManualServerError(error?.message || "تعذر إنشاء الحجز المباشر.");
            } finally {
                setButtonLoading(elements.create, false, "تأكيد الحجز");
            }
        }

        async function confirmCreate(request, preview) {
            // خطوة المراجعة هي التأكيد المرئي النهائي؛ لا نضيف نافذة منبثقة ثانية
            // حتى تبقى تجربة المودل هادئة وواضحة. الحفظ نفسه يعيد فحص التوفر
            // داخل Transaction في السيرفر قبل التثبيت.
            return Boolean(request && preview);
        }

        async function recordManualPayment(bookingId, remainingAmount, paymentMode) {
            if (!bookingId || paymentRequestInFlight) return;

            const remaining = Math.max(0, Number(remainingAmount || 0));
            const isRemainingPayment = paymentMode === "remaining";
            const ownerWallets = Array.isArray(context?.wallets) ? context.wallets : [];
            let amount = "";
            let method = "Cash";
            let ownerWalletId = null;
            let reference = "";

            if (window.Swal) {
                const response = await Swal.fire({
                    title: isRemainingPayment
                        ? `تحصيل المتبقي للحجز #${bookingId}`
                        : `تسجيل عربون الحجز #${bookingId}`,
                    html: `
                        <div style="text-align:right;display:grid;gap:10px">
                            ${remaining > 0 ? `
                                <div style="padding:10px 12px;border-radius:12px;background:#eff9ff;color:#17324d;font-weight:800">
                                    المتبقي الحالي: ${formatNumber(remaining)}
                                </div>` : ""}
                            <label style="font-size:.75rem;font-weight:800;color:#42566A">المبلغ المستلم
                                <input id="ygSwalPaid"
                                       type="number"
                                       inputmode="decimal"
                                       min="0.01"
                                       ${remaining > 0 ? `max="${remaining}"` : ""}
                                       step="0.01"
                                       value="${isRemainingPayment && remaining > 0 ? remaining : ""}"
                                       class="swal2-input"
                                       style="margin:6px 0 0;width:100%" />
                            </label>
                            <label style="font-size:.75rem;font-weight:800;color:#42566A">طريقة الدفع
                                <select id="ygSwalMethod" class="swal2-select" style="margin:6px 0 0;width:100%">
                                    <option value="Cash">نقدًا</option><option value="Wallet">محفظة إلكترونية</option><option value="BankTransfer">حوالة / تحويل</option><option value="Other">أخرى</option>
                                </select>
                            </label>
                            <label id="ygSwalWalletWrap" style="font-size:.75rem;font-weight:800;color:#42566A;display:none">نوع المحفظة
                                <select id="ygSwalWallet" class="swal2-select" style="margin:6px 0 0;width:100%">
                                    <option value="">اختر المحفظة</option>
                                    ${ownerWallets.map(wallet => `<option value="${wallet.ownerWalletId}">${escapeHtml(wallet.displayText || [wallet.walletName, wallet.accountNumber].filter(Boolean).join(" · "))}</option>`).join("")}
                                </select>
                            </label>
                            <label style="font-size:.75rem;font-weight:800;color:#42566A">الرقم المرجعي (إن وجد)
                                <input id="ygSwalRef" type="text" maxlength="100" class="swal2-input" style="margin:6px 0 0;width:100%" />
                            </label>
                        </div>`,
                    showCancelButton: true,
                    confirmButtonText: isRemainingPayment ? "تسجيل التحصيل" : "تسجيل وتأكيد",
                    cancelButtonText: "إلغاء",
                    confirmButtonColor: "#10B981",
                    didOpen: () => {
                        const methodSelect = document.getElementById("ygSwalMethod");
                        const walletWrap = document.getElementById("ygSwalWalletWrap");
                        const syncWallet = () => {
                            if (walletWrap) walletWrap.style.display = methodSelect?.value === "Wallet" ? "block" : "none";
                        };
                        methodSelect?.addEventListener("change", syncWallet);
                        syncWallet();
                    },
                    preConfirm: () => {
                        const paid = Number(document.getElementById("ygSwalPaid")?.value || 0);
                        if (!Number.isFinite(paid) || paid <= 0) {
                            Swal.showValidationMessage("أدخل مبلغًا صحيحًا أكبر من صفر.");
                            return false;
                        }
                        if (remaining > 0 && paid > remaining) {
                            Swal.showValidationMessage(`المبلغ لا يمكن أن يتجاوز المتبقي (${formatNumber(remaining)}).`);
                            return false;
                        }
                        const selectedMethod = document.getElementById("ygSwalMethod")?.value || "Cash";
                        const selectedWalletId = Number(document.getElementById("ygSwalWallet")?.value || 0) || null;
                        if (selectedMethod === "Wallet" && !selectedWalletId) {
                            Swal.showValidationMessage("حدد نوع المحفظة المستخدمة في التحصيل.");
                            return false;
                        }
                        return {
                            amount: paid,
                            method: selectedMethod,
                            ownerWalletId: selectedWalletId,
                            reference: document.getElementById("ygSwalRef")?.value?.trim() || ""
                        };
                    }
                });
                if (!response.isConfirmed || !response.value) return;
                amount = response.value.amount;
                method = response.value.method;
                ownerWalletId = response.value.ownerWalletId || null;
                reference = response.value.reference;
            } else {
                amount = Number(window.prompt(
                    isRemainingPayment ? "المبلغ المحصل من المتبقي:" : "مبلغ العربون المستلم:",
                    isRemainingPayment && remaining > 0 ? String(remaining) : ""
                ) || 0);
                if (!Number.isFinite(amount) || amount <= 0) return;
                if (remaining > 0 && amount > remaining) {
                    window.alert("المبلغ لا يمكن أن يتجاوز المتبقي.");
                    return;
                }
            }

            paymentRequestInFlight = true;
            try {
                const result = await postJson(ENDPOINTS.payment, {
                    bookingId,
                    paidAmount: Number(amount),
                    paymentMethod: method,
                    ownerWalletId,
                    paymentReferenceNumber: reference || null
                });
                if (!result.success) throw new Error(result.message || "تعذر تسجيل الدفعة.");
                toast(
                    "success",
                    Number(result.remainingAmount || 0) <= 0 ? "اكتمل السداد" : "تم تسجيل الدفعة",
                    result.message || "تم تحديث مالية الحجز."
                );
                await refreshAll();
            } catch (error) {
                handleError(error);
            } finally {
                paymentRequestInFlight = false;
            }
        }

        function openRangeDialog(mode, start, end) {
            closePanel(elements.dayOverlay, elements.daySheet, true);
            closePanel(elements.manualOverlay, elements.manualModal, true);
            rangeMode = mode === "reopen" ? "reopen" : "close";
            elements.rangeStart.value = start || selectedDate;
            elements.rangeEnd.value = end || start || selectedDate;
            elements.rangeReason.value = "";
            const reopen = rangeMode === "reopen";
            elements.rangeModal.classList.toggle("is-reopen", reopen);
            elements.rangeKicker.textContent = reopen ? "RESTORE AVAILABILITY" : "BLOCK AVAILABILITY";
            elements.rangeTitle.textContent = reopen ? "إعادة فتح فترة" : "إغلاق فترة";
            elements.rangeDescription.textContent = reopen
                ? "سيُزال الإغلاق الاستثنائي، بينما الإغلاق الأسبوعي يبقى حسب السياسة."
                : "يمكن إغلاق الأيام الخالية فقط؛ أي يوم أو فترة تحتوي حجزًا نشطًا سيرفضها النظام لحماية الحجز.";
            elements.rangeReasonField.hidden = reopen;
            elements.rangeNotice.innerHTML = reopen
                ? '<i class="bi bi-info-circle-fill"></i><span>إعادة الفتح تزيل فقط إغلاقات التقويم الاستثنائية ولا تلغي أيام الإغلاق الأسبوعية.</span>'
                : '<i class="bi bi-shield-lock-fill"></i><span>لا يمكن إغلاق يوم عليه حجز نشط. افحص الفترة ثم عالج الحجز أولًا إذا ظهر تعارض.</span>';
            elements.rangeSubmit.querySelector("span").textContent = reopen ? "إعادة فتح" : "تأكيد الإغلاق";
            elements.rangeSubmit.querySelector("i").className = reopen ? "bi bi-unlock-fill" : "bi bi-lock-fill";
            openPanel(elements.rangeOverlay, elements.rangeModal);
        }

        function closeRangeDialog() {
            closePanel(elements.rangeOverlay, elements.rangeModal);
        }

        async function submitRangeAction() {
            let startDate = elements.rangeStart.value;
            let endDate = elements.rangeEnd.value;
            if (!startDate || !endDate) {
                toast("error", "تاريخ ناقص", "حدد بداية ونهاية الفترة.");
                return;
            }
            if (compareDateStrings(endDate, startDate) < 0) [startDate, endDate] = [endDate, startDate];

            setButtonLoading(elements.rangeSubmit, true, "جاري الحفظ…");
            try {
                const result = await postJson(
                    rangeMode === "reopen" ? ENDPOINTS.reopenRange : ENDPOINTS.closeRange,
                    {
                        startDate,
                        endDate,
                        reason: rangeMode === "close" ? elements.rangeReason.value.trim() || null : null
                    });
                if (!result.success) throw new Error(result.message || "تعذر تحديث الفترة.");
                closeRangeDialog();
                toast("success", rangeMode === "reopen" ? "تمت إعادة الفتح" : "تم الإغلاق", result.message || "تم تحديث التوفر.");
                await refreshAll();
                if (startDate === endDate) await openDayDetails(startDate);
            } catch (error) {
                handleError(error);
            } finally {
                setButtonLoading(elements.rangeSubmit, false, rangeMode === "reopen" ? "إعادة فتح" : "تأكيد الإغلاق");
            }
        }

        function closeAllPanels(immediate) {
            closePanel(elements.dayOverlay, elements.daySheet, immediate);
            closePanel(elements.manualOverlay, elements.manualModal, immediate);
            closePanel(elements.rangeOverlay, elements.rangeModal, immediate);
            closePanel(elements.monthPickerOverlay, elements.monthPickerModal, immediate);
        }

        function mountDialogPortal() {
            if (portal || !document.body) return;

            portal = document.createElement("div");
            portal.className = "yg-calendar-portal";
            portal.dataset.calendarVersion = "v6-mobile-first-luxury";
            portal.setAttribute("dir", "rtl");

            [
                elements.dayOverlay,
                elements.daySheet,
                elements.manualOverlay,
                elements.manualModal,
                elements.rangeOverlay,
                elements.rangeModal,
                elements.monthPickerOverlay,
                elements.monthPickerModal,
                elements.toasts
            ].filter(Boolean).forEach(node => portal.appendChild(node));

            document.body.appendChild(portal);
        }

        function restoreDialogPortal() {
            if (!portal) return;

            [
                elements.dayOverlay,
                elements.daySheet,
                elements.manualOverlay,
                elements.manualModal,
                elements.rangeOverlay,
                elements.rangeModal,
                elements.monthPickerOverlay,
                elements.monthPickerModal,
                elements.toasts
            ].filter(Boolean).forEach(node => root.appendChild(node));

            portal.remove();
            portal = null;
        }

        function lockPageScroll() {
            if (lockedScrollY !== null) return;

            lockedScrollY = window.scrollY || window.pageYOffset || 0;
            previousBodyPaddingRight = document.body.style.paddingRight || "";
            previousBodyPosition = document.body.style.position || "";
            previousBodyTop = document.body.style.top || "";
            previousBodyWidth = document.body.style.width || "";
            previousBodyOverflow = document.body.style.overflow || "";

            const scrollbarWidth = Math.max(0, window.innerWidth - document.documentElement.clientWidth);
            if (scrollbarWidth > 0) document.body.style.paddingRight = `${scrollbarWidth}px`;

            document.body.style.position = "fixed";
            document.body.style.top = `-${lockedScrollY}px`;
            document.body.style.width = "100%";
            document.body.style.overflow = "hidden";
            document.body.classList.add("yg-calendar-modal-open");
        }

        function unlockPageScroll(force = false) {
            if (lockedScrollY === null && !force) return;

            const restoreY = lockedScrollY ?? 0;
            document.body.classList.remove("yg-calendar-modal-open");
            document.body.style.paddingRight = previousBodyPaddingRight;
            document.body.style.position = previousBodyPosition;
            document.body.style.top = previousBodyTop;
            document.body.style.width = previousBodyWidth;
            document.body.style.overflow = previousBodyOverflow;

            lockedScrollY = null;
            previousBodyPaddingRight = "";
            previousBodyPosition = "";
            previousBodyTop = "";
            previousBodyWidth = "";
            previousBodyOverflow = "";

            window.requestAnimationFrame(() => window.scrollTo(0, restoreY));
        }

        function hasOpenPanels() {
            return Boolean(
                elements.daySheet?.classList.contains("is-open") ||
                elements.manualModal?.classList.contains("is-open") ||
                elements.rangeModal?.classList.contains("is-open") ||
                elements.monthPickerModal?.classList.contains("is-open")
            );
        }

        function openPanel(overlay, panel) {
            if (!overlay || !panel) return;

            lockPageScroll();
            overlay.hidden = false;
            panel.setAttribute("aria-hidden", "false");

            requestAnimationFrame(() => {
                overlay.classList.add("is-open");
                panel.classList.add("is-open");
            });
        }

        function closePanel(overlay, panel, immediate = false) {
            if (!overlay || !panel) return;

            overlay.classList.remove("is-open");
            panel.classList.remove("is-open");
            panel.setAttribute("aria-hidden", "true");

            const done = () => {
                overlay.hidden = true;
                if (!hasOpenPanels()) unlockPageScroll();
            };

            if (immediate) done();
            else window.setTimeout(done, 260);
        }

        function setManualStep(step, scrollToTop = true) {
            const safeStep = Math.min(4, Math.max(1, Number(step || 1)));
            manualStep = safeStep;

            if (elements.manualModal) elements.manualModal.dataset.currentStep = String(safeStep);

            $$('[data-yg-step]').forEach(section => {
                section.classList.toggle('is-active-step', Number(section.dataset.ygStep || 0) === safeStep);
            });

            $$('[data-yg-step-target]').forEach(button => {
                const buttonStep = Number(button.dataset.ygStepTarget || 0);
                const active = buttonStep === safeStep;
                button.classList.toggle('is-active', active);
                button.classList.toggle('is-complete', buttonStep < safeStep);
                if (active) button.setAttribute('aria-current', 'step');
                else button.removeAttribute('aria-current');
            });

            // شريط التنقل يبقى ثابتًا دائمًا. زر السابق لا يختفي في الخطوة الأولى بل يصبح معطلاً.
            if (elements.stepBack) {
                elements.stepBack.hidden = false;
                elements.stepBack.disabled = safeStep === 1;
                elements.stepBack.setAttribute('aria-disabled', safeStep === 1 ? 'true' : 'false');
            }
            if (elements.stepNext) elements.stepNext.hidden = safeStep === 4;
            if (elements.create) elements.create.hidden = safeStep !== 4;

            const stepTitles = ["بيانات العميل", "الحجز والتوفر", "الإضافات والدفع", "المراجعة والتثبيت"];
            if (elements.mobileStepCounter) elements.mobileStepCounter.textContent = `الخطوة ${safeStep} من 4`;
            if (elements.mobileStepTitle) elements.mobileStepTitle.textContent = stepTitles[safeStep - 1] || "";
            if (elements.mobileStepProgress) elements.mobileStepProgress.style.width = `${safeStep * 25}%`;

            if (scrollToTop) {
                const activePanel = elements.manualModal?.querySelector(`[data-yg-step="${safeStep}"]`);
                if (activePanel) activePanel.scrollTop = 0;
                const review = elements.manualModal?.querySelector('.yg-mb9-review-card');
                if (safeStep === 4 && review) review.scrollTop = 0;
            }
        }

        function validateManualStep(step) {
            const request = buildManualRequest();
            const message = validateManualStepFields(step, request, true);
            if (message) {
                window.setTimeout(focusFirstInvalidField, 40);
                return false;
            }
            return true;
        }

        async function nextManualStep() {
            if (manualStep >= 4) return;
            if (!validateManualStep(manualStep)) return;

            // قبل فتح خطوة الدفع نحتاج السعر والعربون الآن، لا في خطوة المراجعة فقط.
            // بهذه المعاينة المبكرة يصبح latestManualQuote جاهزًا، وعند اختيار
            // "تم استلام مبلغ" يُملأ العربون مباشرة من سياسة المنشأة.
            if (manualStep === 2) {
                const pricingPreview = await previewManualBooking(true);
                if (!pricingPreview) return;
            }

            if (manualStep === 3) {
                const preview = await previewManualBooking(true);
                if (!preview) return;
            }

            setManualStep(manualStep + 1);

            if (manualStep === 3) {
                syncPaymentFields();
            }
        }

        async function getJson(url) {
            const response = await fetch(url, {
                method: "GET",
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                signal: abortController.signal
            });
            return readResponse(response);
        }

        async function postJson(url, body) {
            const token = getAntiForgeryToken();
            const response = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    ...(token ? { "RequestVerificationToken": token } : {})
                },
                body: JSON.stringify(body),
                signal: abortController.signal
            });
            return readResponse(response);
        }

        async function readResponse(response) {
            const text = await response.text();
            let result;
            try {
                result = text ? JSON.parse(text) : {};
            } catch (_) {
                const clean = text.replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();
                result = { success: false, message: clean || `فشل الطلب برمز ${response.status}.` };
            }
            if (!response.ok) {
                throw new Error(result.message || `فشل الطلب برمز ${response.status}.`);
            }
            return result;
        }

        function getAntiForgeryToken() {
            return document.querySelector('#ownerReservationAntiForgeryForm input[name="__RequestVerificationToken"]')?.value ||
                document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
        }

        function setCalendarLoading(value) {
            if (elements.loading) elements.loading.hidden = !value;
        }

        function setSyncing(value) {
            elements.sync?.classList.toggle("is-spinning", value);
            if (elements.sync) elements.sync.disabled = value;
        }

        function setButtonLoading(button, loading, text) {
            if (!button) return;
            button.classList.toggle("is-loading", loading);
            button.disabled = loading;
            const span = button.querySelector("span:last-child") || button.querySelector("span");
            if (span && text) span.textContent = text;
        }

        function toast(type, title, message) {
            if (!elements.toasts) return;
            const icon = type === "success" ? "bi-check2-circle" : type === "error" ? "bi-exclamation-octagon" : "bi-info-circle";
            const item = document.createElement("div");
            item.className = `yg-calendar-toast yg-calendar-toast--${type || "info"}`;
            item.innerHTML = `<i class="bi ${icon}"></i><div><strong>${escapeHtml(title || "تنبيه")}</strong><span>${escapeHtml(message || "")}</span></div><button type="button"><i class="bi bi-x"></i></button>`;
            elements.toasts.appendChild(item);
            const close = () => {
                item.style.opacity = "0";
                item.style.transform = "translateY(-8px)";
                window.setTimeout(() => item.remove(), 220);
            };
            item.querySelector("button")?.addEventListener("click", close, { once: true });
            window.setTimeout(close, type === "error" ? 6000 : 4200);
        }

        function handleError(error, showToast = true) {
            if (error?.name === "AbortError") return;
            console.error("YemenGuide Calendar:", error);
            if (showToast) toast("error", "لم تتم العملية", error?.message || "حدث خطأ غير متوقع.");
        }

        function setText(selector, value) {
            const el = $(selector);
            if (el) el.textContent = String(value ?? "");
        }

        return { init, dispose };
    }

    function formatLocalDate(value) {
        const date = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(date.getTime())) return "";
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${year}-${month}-${day}`;
    }

    function parseLocalDate(value) {
        const [year, month, day] = String(value || "").split("-").map(Number);
        return new Date(year || 1970, Math.max(0, (month || 1) - 1), day || 1, 12, 0, 0, 0);
    }

    function addDays(value, days) {
        const date = value instanceof Date ? new Date(value) : parseLocalDate(value);
        date.setDate(date.getDate() + days);
        return date;
    }

    function compareDateStrings(a, b) {
        return String(a || "").localeCompare(String(b || ""));
    }

    function formatDateArabic(value) {
        const date = parseLocalDate(value);
        return new Intl.DateTimeFormat("ar-YE", {
            weekday: "long",
            day: "numeric",
            month: "long",
            year: "numeric"
        }).format(date);
    }

    function formatNumber(value) {
        const number = Number(value || 0);
        return new Intl.NumberFormat("ar-YE", { maximumFractionDigits: 2 }).format(number);
    }

    function money(value) {
        return `${formatNumber(value)} ريال`;
    }

    function normalizeClock(value) {
        const text = String(value || "").trim();
        const match = text.match(/(\d{1,2}):(\d{2})/);
        return match ? `${match[1].padStart(2, "0")}:${match[2]}` : text;
    }

    function timeSpanText(value) {
        if (!value) return "";
        if (typeof value === "string") return normalizeClock(value);
        if (typeof value === "object" && value.hours !== undefined) {
            return `${String(value.hours).padStart(2, "0")}:${String(value.minutes || 0).padStart(2, "0")}`;
        }
        return "";
    }

    function stateLabel(value) {
        return stateLabels[value] || value || "غير محدد";
    }

    function paymentLabel(value) {
        return paymentLabels[value] || value || "غير محدد";
    }

    function refundLabel(value) {
        return refundLabels[value] || value || "استرداد";
    }

    function stateBadgeClass(value) {
        return `yg-mini-badge--${calendarStateKey(value)}`;
    }

    function calendarStateKey(value) {
        if (value === "Confirmed") return "confirmed";
        if (value === "AwaitingPayment") return "awaiting";
        if (value === "PaymentReview") return "review";
        if (value === "Pending") return "pending";
        if (value === "Completed") return "completed";
        if (value === "NoShow") return "noshow";
        if (value === "Cancelled") return "cancelled";
        if (value === "Rejected") return "rejected";
        if (value === "Expired") return "expired";
        return "neutral";
    }

    function stateIconClass(value, eventType = "booking") {
        if (eventType === "closed" || eventType === "policyClosed") return "bi-lock-fill";
        if (value === "Confirmed") return "bi-patch-check-fill";
        if (value === "AwaitingPayment") return "bi-hourglass-split";
        if (value === "PaymentReview") return "bi-receipt-cutoff";
        if (value === "Completed") return "bi-check2-all";
        if (value === "NoShow") return "bi-person-x-fill";
        if (value === "Cancelled") return "bi-x-circle-fill";
        if (value === "Rejected") return "bi-slash-circle-fill";
        if (value === "Expired") return "bi-clock-history";
        return "bi-calendar2-event";
    }

    function initialLetter(value) {
        const text = String(value || "ع").trim();
        return text ? text.charAt(0).toUpperCase() : "ع";
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function safeCssUrl(value) {
        return String(value || "").replace(/["'()\\]/g, "");
    }

    function emptyState(icon, title, text) {
        return `<div class="yg-empty-state"><i class="bi ${icon}"></i><strong>${escapeHtml(title)}</strong><span>${escapeHtml(text)}</span></div>`;
    }

    function daySkeleton() {
        return `<div class="yg-empty-state"><span class="yg-loader-orbit"><i></i><i></i><i></i></span><strong>نحمّل تفاصيل اليوم…</strong></div>`;
    }

    window.initPremiumCalendar = initPremiumCalendar;

    document.addEventListener("DOMContentLoaded", function () {
        if (document.getElementById("ygCalendarManager")) {
            initPremiumCalendar();
        }
    }, { once: true });
})();
