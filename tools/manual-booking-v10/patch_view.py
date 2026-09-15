from pathlib import Path

PATH = Path('YemenGuide.Web/Areas/Owner/Views/Reservation/_CalendarManager.cshtml')
raw = PATH.read_bytes(); bom = raw.startswith(b'\xef\xbb\xbf'); text = raw.decode('utf-8-sig')

def rep(old, new):
    global text
    if new in text: return
    count = text.count(old)
    if count != 1: raise SystemExit(f'view expected 1 match, found {count}: {old[:100]!r}')
    text = text.replace(old, new, 1)

rep(
'''                    <div class="yg-mb9-card yg-mb9-guests-card">
                        <div class="yg-mb9-card-head"><span><i class="bi bi-people"></i></span><div><strong>عدد الأشخاص</strong><small>يتم التحقق من سعة الغرفة أو الباقة قبل الحفظ.</small></div></div>''',
'''                    <div class="yg-mb9-card yg-mb9-guests-card">
                        <div class="yg-mb9-card-head"><span><i class="bi bi-people"></i></span><div><strong>عدد الأشخاص</strong><small>يُحدد تلقائيًا من سعة الغرفة أو الباقة. يمكنك الإنقاص فقط ولا يمكن تجاوز السعة.</small></div></div>''')

rep(
'''                                    <div class="yg-v8-field yg-mb9-field" data-yg-field="paidAmount">
                                        <label for="ygManualPaidAmount">المبلغ المستلم <b>*</b></label>
                                        <div class="yg-v8-input-shell yg-mb9-input-shell"><i class="bi bi-cash-stack"></i><input type="number" id="ygManualPaidAmount" min="0" step="0.01" inputmode="decimal" value="0" /></div>
                                        <small class="yg-v8-field-error" data-yg-error="paidAmount"></small>
                                    </div>''',
'''                                    <div class="yg-v8-field yg-mb9-field" data-yg-field="paidAmount">
                                        <label for="ygManualPaidAmount">العربون المستلم <b>*</b></label>
                                        <div class="yg-v8-input-shell yg-mb9-input-shell"><i class="bi bi-cash-stack"></i><input type="number" id="ygManualPaidAmount" min="0" step="0.01" inputmode="decimal" value="0" /></div>
                                        <small>يُملأ تلقائيًا من سياسة العربون بعد حساب السعر.</small>
                                        <small class="yg-v8-field-error" data-yg-error="paidAmount"></small>
                                    </div>''')

rep(
'''                                <div class="yg-v8-field yg-v8-reveal-field yg-mb9-reveal" id="ygPaymentMethodOtherField" data-yg-field="paymentMethodOther" hidden>
                                    <label for="ygManualPaymentMethodOther">ما هي طريقة الدفع الأخرى؟ <b>*</b></label>
                                    <div class="yg-v8-input-shell yg-mb9-input-shell"><i class="bi bi-pencil-square"></i><input type="text" id="ygManualPaymentMethodOther" maxlength="30" placeholder="اكتب الطريقة المستخدمة" /></div>
                                    <small class="yg-v8-field-error" data-yg-error="paymentMethodOther"></small>
                                </div>
                                <div class="yg-v8-field yg-mb9-field" data-yg-field="paymentReference">''',
'''                                <div class="yg-v8-field yg-v8-reveal-field yg-mb9-reveal" id="ygPaymentMethodOtherField" data-yg-field="paymentMethodOther" hidden>
                                    <label for="ygManualPaymentMethodOther">ما هي طريقة الدفع الأخرى؟ <b>*</b></label>
                                    <div class="yg-v8-input-shell yg-mb9-input-shell"><i class="bi bi-pencil-square"></i><input type="text" id="ygManualPaymentMethodOther" maxlength="30" placeholder="اكتب الطريقة المستخدمة" /></div>
                                    <small class="yg-v8-field-error" data-yg-error="paymentMethodOther"></small>
                                </div>
                                <div class="yg-v8-field yg-v8-reveal-field yg-mb9-reveal" id="ygOwnerWalletField" data-yg-field="ownerWallet" hidden>
                                    <label>نوع المحفظة <b>*</b></label>
                                    <div class="yg-v8-smart-select yg-mb9-smart-select" data-yg-smart-select="owner-wallet">
                                        <select id="ygManualOwnerWalletId" class="yg-v8-select-source" tabindex="-1" aria-hidden="true">
                                            <option value="">اختر المحفظة</option>
                                        </select>
                                        <button type="button" class="yg-v8-select-trigger yg-mb9-select-trigger" id="ygOwnerWalletSelectTrigger"><span><i class="bi bi-wallet2"></i><b id="ygOwnerWalletSelectText">اختر المحفظة</b></span><i class="bi bi-chevron-down"></i></button>
                                        <div class="yg-v8-select-menu yg-mb9-select-menu" id="ygOwnerWalletSelectMenu" hidden></div>
                                    </div>
                                    <small>تظهر محافظ استقبال الدفعات المفعلة في حساب المالك فقط.</small>
                                    <small class="yg-v8-field-error" data-yg-error="ownerWallet"></small>
                                </div>
                                <div class="yg-v8-field yg-mb9-field" data-yg-field="paymentReference">''')

PATH.write_bytes(text.encode('utf-8-sig' if bom else 'utf-8'))
print('view patch applied')
