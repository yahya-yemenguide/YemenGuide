from pathlib import Path

PATH = Path('YemenGuide.Web/wwwroot/js/owner/calendar-manager.js')
raw = PATH.read_bytes(); bom = raw.startswith(b'\xef\xbb\xbf'); text = raw.decode('utf-8-sig')

def rep(old, new):
    global text
    if new in text: return
    count = text.count(old)
    if count != 1: raise SystemExit(f'js2 expected 1 match, found {count}: {old[:100]!r}')
    text = text.replace(old, new, 1)

rep(
'''        function renderRoomUnitChoices(resetFloor = true) {''',
'''        function syncGuestCapacity(resetToPolicyMaximum = false, changedKind = "") {
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

        function renderRoomUnitChoices(resetFloor = true) {''')

rep(
'''            const paymentOther = elements.paymentMethod?.value === "Other";
            if (elements.paymentMethodOtherField) elements.paymentMethodOtherField.hidden = !paymentOther;
            if (!paymentOther && elements.paymentMethodOther) {
                elements.paymentMethodOther.value = "";
                clearFieldError("paymentMethodOther");
            }

            const seatingOther = elements.seating?.value === "Other";''',
'''            const paymentOther = elements.paymentMethod?.value === "Other";
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

            const seatingOther = elements.seating?.value === "Other";''')

rep(
'''        function syncPaymentFields() {
            const collected = getPaymentMode() === "Collected";
            elements.collectedFields.hidden = !collected;
            if (!collected) {
                elements.paidAmount.value = "0";
                elements.paymentReference.value = "";
                if (elements.paymentMethodOther) elements.paymentMethodOther.value = "";
                clearFieldError("paidAmount");
                clearFieldError("paymentMethod");
                clearFieldError("paymentMethodOther");
            }
            syncOtherFields();
        }''',
'''        function syncPaymentFields() {
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
            }
            syncOtherFields();
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
        }''')

rep(
'''            const effectivePaymentMethod = selectedPaymentMethod === "Other"
                ? (elements.paymentMethodOther?.value || "").trim()
                : selectedPaymentMethod;

            return {''',
'''            const effectivePaymentMethod = selectedPaymentMethod === "Other"
                ? (elements.paymentMethodOther?.value || "").trim()
                : selectedPaymentMethod;
            const ownerWalletId = selectedPaymentMethod === "Wallet"
                ? Number(elements.ownerWalletId?.value || 0) || null
                : null;

            return {''')

rep(
'''                paidAmount: paymentMode === "Collected" ? Math.max(0, Number(elements.paidAmount.value || 0)) : 0,
                paymentMethod: paymentMode === "Collected" ? effectivePaymentMethod || null : null,
                paymentReferenceNumber: paymentMode === "Collected" ? elements.paymentReference.value.trim() || null : null''',
'''                paidAmount: paymentMode === "Collected" ? Math.max(0, Number(elements.paidAmount.value || 0)) : 0,
                paymentMethod: paymentMode === "Collected" ? effectivePaymentMethod || null : null,
                ownerWalletId: paymentMode === "Collected" ? ownerWalletId : null,
                paymentReferenceNumber: paymentMode === "Collected" ? elements.paymentReference.value.trim() || null : null''')

rep(
'''            if (step === 3 && request.paymentMode === "Collected") {
                if (request.paidAmount <= 0) invalid("paidAmount", "أدخل المبلغ الذي تم استلامه فعليًا.");
                if (!request.paymentMethod) invalid("paymentMethod", "حدد طريقة الدفع.");
                if (elements.paymentMethod?.value === "Other" && !elements.paymentMethodOther?.value?.trim()) {
                    invalid("paymentMethodOther", "اكتب طريقة الدفع الأخرى.");
                }
            }''',
'''            if (step === 3 && request.paymentMode === "Collected") {
                if (request.paidAmount < 0) invalid("paidAmount", "قيمة العربون غير صحيحة.");
                if (!request.paymentMethod) invalid("paymentMethod", "حدد طريقة الدفع.");
                if (elements.paymentMethod?.value === "Wallet" && !request.ownerWalletId) {
                    invalid("ownerWallet", "حدد نوع المحفظة التي استلمت عليها العربون.");
                }
                if (elements.paymentMethod?.value === "Other" && !elements.paymentMethodOther?.value?.trim()) {
                    invalid("paymentMethodOther", "اكتب طريقة الدفع الأخرى.");
                }
            }''')

rep(
'''            else if (text.includes("المبلغ المستلم") || text.includes("أقل من العربون") || text.includes("يتجاوز إجمالي")) { step = 3; key = "paidAmount"; }
            else if (text.includes("طريقة دفع")) { step = 3; key = elements.paymentMethod?.value === "Other" ? "paymentMethodOther" : "paymentMethod"; }''',
'''            else if (text.includes("المبلغ المستلم") || text.includes("العربون المستلم") || text.includes("أقل من العربون") || text.includes("يتجاوز إجمالي")) { step = 3; key = "paidAmount"; }
            else if (text.includes("المحفظة")) { step = 3; key = "ownerWallet"; }
            else if (text.includes("طريقة دفع")) { step = 3; key = elements.paymentMethod?.value === "Other" ? "paymentMethodOther" : "paymentMethod"; }''')

rep(
'''        function renderQuote(data) {
            elements.quotePlaceholder.hidden = true;''',
'''        function renderQuote(data) {
            latestManualQuote = data;
            syncCollectedDepositFromQuote(data);
            elements.quotePlaceholder.hidden = true;''')

rep(
'''        function clearQuote(full = true) {
            elements.quotePlaceholder.hidden = false;
            elements.quoteContent.hidden = true;
            if (full) {
                elements.quoteDiscountRow.hidden = true;''',
'''        function clearQuote(full = true) {
            elements.quotePlaceholder.hidden = false;
            elements.quoteContent.hidden = true;
            if (full) {
                latestManualQuote = null;
                elements.quoteDiscountRow.hidden = true;''')

rep(
'''        async function createManualBooking() {
            const request = buildManualRequest();
            const validation = validateManualRequest(request, true);
            if (validation) return;

            setButtonLoading(elements.create, true, "جاري التثبيت…");
            try {
                const preview = await previewManualBooking(true);
                if (!preview) return;

                const confirmed = await confirmCreate(request, preview);
                if (!confirmed) return;

                const result = await postJson(ENDPOINTS.create, request);''',
'''        async function createManualBooking() {
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

                const result = await postJson(ENDPOINTS.create, request);''')

PATH.write_bytes(text.encode('utf-8-sig' if bom else 'utf-8'))
print('js patch 2 applied')
