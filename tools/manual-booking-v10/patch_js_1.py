from pathlib import Path

PATH = Path('YemenGuide.Web/wwwroot/js/owner/calendar-manager.js')
raw = PATH.read_bytes(); bom = raw.startswith(b'\xef\xbb\xbf'); text = raw.decode('utf-8-sig')

def rep(old, new):
    global text
    if new in text: return
    count = text.count(old)
    if count != 1: raise SystemExit(f'js1 expected 1 match, found {count}: {old[:100]!r}')
    text = text.replace(old, new, 1)

rep(
'''        let paymentRequestInFlight = false;
        let lockedScrollY = null;''',
'''        let paymentRequestInFlight = false;
        let latestManualQuote = null;
        let lockedScrollY = null;''')

rep(
'''            paymentSelectTrigger: $("#ygPaymentSelectTrigger"),
            paymentSelectText: $("#ygPaymentSelectText"),
            paymentSelectMenu: $("#ygPaymentSelectMenu"),
            paymentReference: $("#ygManualPaymentReference"),''',
'''            paymentSelectTrigger: $("#ygPaymentSelectTrigger"),
            paymentSelectText: $("#ygPaymentSelectText"),
            paymentSelectMenu: $("#ygPaymentSelectMenu"),
            ownerWalletField: $("#ygOwnerWalletField"),
            ownerWalletId: $("#ygManualOwnerWalletId"),
            ownerWalletSelectTrigger: $("#ygOwnerWalletSelectTrigger"),
            ownerWalletSelectText: $("#ygOwnerWalletSelectText"),
            ownerWalletSelectMenu: $("#ygOwnerWalletSelectMenu"),
            paymentReference: $("#ygManualPaymentReference"),''')

rep(
'''            on(elements.roomId, "change", () => {
                clearFieldError("roomId");
                updateSmartSelectValue(elements.roomId, elements.roomSelectText);
                updateRoomHint();
                normalizeRoomQuantity();
                activeRoomFloorKey = "";''',
'''            on(elements.roomId, "change", () => {
                clearFieldError("roomId");
                updateSmartSelectValue(elements.roomId, elements.roomSelectText);
                updateRoomHint();
                normalizeRoomQuantity();
                syncGuestCapacity(true);
                activeRoomFloorKey = "";''')

rep(
'''            on(elements.packageId, "change", () => {
                clearFieldError("packageId");
                updatePackageHint();
                renderPackageCards();
                scheduleQuotePreview();
            });''',
'''            on(elements.packageId, "change", () => {
                clearFieldError("packageId");
                updatePackageHint();
                renderPackageCards();
                syncGuestCapacity(true);
                scheduleQuotePreview();
            });''')

rep(
'''            on(elements.quantity, "input", () => {
                clearFieldError("quantity");
                normalizeRoomQuantity();
                normalizeRoomUnitSelection();
                renderRoomUnitChoices(false);
                scheduleQuotePreview();
            });''',
'''            on(elements.quantity, "input", () => {
                clearFieldError("quantity");
                normalizeRoomQuantity();
                syncGuestCapacity(true);
                normalizeRoomUnitSelection();
                renderRoomUnitChoices(false);
                scheduleQuotePreview();
            });''')

rep(
'''            on(elements.adults, "input", () => { clampIntegerInput(elements.adults, 0, 1000); scheduleQuotePreview(); });
            on(elements.children, "input", () => { clampIntegerInput(elements.children, 0, 1000); scheduleQuotePreview(); });''',
'''            on(elements.adults, "input", () => { syncGuestCapacity(false, "adults"); scheduleQuotePreview(); });
            on(elements.children, "input", () => { syncGuestCapacity(false, "children"); scheduleQuotePreview(); });''')

rep(
'''            on(elements.paymentMethod, "change", () => {
                updateSmartSelectValue(elements.paymentMethod, elements.paymentSelectText);
                syncOtherFields();
                clearFieldError("paymentMethod");
                scheduleQuotePreview();
            });
            on(elements.paymentMethodOther, "input", () => { clearFieldError("paymentMethodOther"); scheduleQuotePreview(); });''',
'''            on(elements.paymentMethod, "change", () => {
                updateSmartSelectValue(elements.paymentMethod, elements.paymentSelectText);
                syncOtherFields();
                clearFieldError("paymentMethod");
                clearFieldError("ownerWallet");
                scheduleQuotePreview();
            });
            on(elements.ownerWalletId, "change", () => {
                updateSmartSelectValue(elements.ownerWalletId, elements.ownerWalletSelectText);
                clearFieldError("ownerWallet");
                scheduleQuotePreview();
            });
            on(elements.paymentMethodOther, "input", () => { clearFieldError("paymentMethodOther"); scheduleQuotePreview(); });''')

rep(
'''            initializeSmartSelect(elements.roomId, elements.roomSelectTrigger, elements.roomSelectMenu, elements.roomSelectText);
            initializeSmartSelect(elements.seating, elements.seatingSelectTrigger, elements.seatingSelectMenu, elements.seatingSelectText);
            initializeSmartSelect(elements.paymentMethod, elements.paymentSelectTrigger, elements.paymentSelectMenu, elements.paymentSelectText);''',
'''            initializeSmartSelect(elements.roomId, elements.roomSelectTrigger, elements.roomSelectMenu, elements.roomSelectText);
            initializeSmartSelect(elements.seating, elements.seatingSelectTrigger, elements.seatingSelectMenu, elements.seatingSelectText);
            initializeSmartSelect(elements.paymentMethod, elements.paymentSelectTrigger, elements.paymentSelectMenu, elements.paymentSelectText);
            initializeSmartSelect(elements.ownerWalletId, elements.ownerWalletSelectTrigger, elements.ownerWalletSelectMenu, elements.ownerWalletSelectText);''')

rep(
'''            if (elements.paymentMethodOther) elements.paymentMethodOther.value = "";
            elements.startDate.value = date;''',
'''            if (elements.paymentMethodOther) elements.paymentMethodOther.value = "";
            if (elements.ownerWalletId) elements.ownerWalletId.value = "";
            latestManualQuote = null;
            elements.startDate.value = date;''')

rep(
'''            refreshDynamicSmartSelect(elements.roomId);
            refreshDynamicSmartSelect(elements.seating);
            refreshDynamicSmartSelect(elements.paymentMethod);
            elements.paymentReference.value = "";''',
'''            refreshDynamicSmartSelect(elements.roomId);
            refreshDynamicSmartSelect(elements.seating);
            refreshDynamicSmartSelect(elements.paymentMethod);
            refreshDynamicSmartSelect(elements.ownerWalletId);
            elements.paymentReference.value = "";''')

rep(
'''            if (manualResourceKind === "package") {
                elements.quantity.value = "1";
            }
            clearFieldError("roomId");''',
'''            if (manualResourceKind === "package") {
                elements.quantity.value = "1";
            }
            if (manualOptions) syncGuestCapacity(true);
            clearFieldError("roomId");''')

rep(
'''            renderPackageCards();
            renderServices(Array.isArray(data.services) ? data.services : []);

            if (data.isClosedByPolicy || data.isClosedByException) {''',
'''            renderPackageCards();
            renderServices(Array.isArray(data.services) ? data.services : []);

            const wallets = Array.isArray(data.wallets)
                ? data.wallets
                : (Array.isArray(context?.wallets) ? context.wallets : []);
            if (elements.ownerWalletId) {
                const previousWallet = elements.ownerWalletId.value;
                elements.ownerWalletId.innerHTML = '<option value="">اختر المحفظة</option>' + wallets.map(wallet => {
                    const label = wallet.displayText || [wallet.walletName, wallet.accountNumber].filter(Boolean).join(" · ");
                    return `<option value="${wallet.ownerWalletId}">${escapeHtml(label || "محفظة")}</option>`;
                }).join("");
                if (wallets.some(wallet => String(wallet.ownerWalletId) === previousWallet)) {
                    elements.ownerWalletId.value = previousWallet;
                }
                refreshDynamicSmartSelect(elements.ownerWalletId);
            }

            if (data.isClosedByPolicy || data.isClosedByException) {''')

rep(
'''            renderRoomUnitChoices();
            syncManualTemplateUi();
            scheduleQuotePreview();''',
'''            renderRoomUnitChoices();
            syncManualTemplateUi();
            syncGuestCapacity(true);
            syncOtherFields();
            scheduleQuotePreview();''')

PATH.write_bytes(text.encode('utf-8-sig' if bom else 'utf-8'))
print('js patch 1 applied')
