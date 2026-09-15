from pathlib import Path

PATH = Path('YemenGuide.Web/wwwroot/js/owner/calendar-manager.js')
raw = PATH.read_bytes(); bom = raw.startswith(b'\xef\xbb\xbf'); text = raw.decode('utf-8-sig')

def rep(old, new):
    global text
    if new in text: return
    count = text.count(old)
    if count != 1: raise SystemExit(f'js3 expected 1 match, found {count}: {old[:100]!r}')
    text = text.replace(old, new, 1)

rep(
'''            const remaining = Math.max(0, Number(remainingAmount || 0));
            const isRemainingPayment = paymentMode === "remaining";
            let amount = "";
            let method = "Cash";
            let reference = "";''',
'''            const remaining = Math.max(0, Number(remainingAmount || 0));
            const isRemainingPayment = paymentMode === "remaining";
            const ownerWallets = Array.isArray(context?.wallets) ? context.wallets : [];
            let amount = "";
            let method = "Cash";
            let ownerWalletId = null;
            let reference = "";''')

rep(
'''                            <label style="font-size:.75rem;font-weight:800;color:#42566A">الرقم المرجعي (إن وجد)
                                <input id="ygSwalRef" type="text" maxlength="100" class="swal2-input" style="margin:6px 0 0;width:100%" />
                            </label>
                        </div>`,
                    showCancelButton: true,
                    confirmButtonText: isRemainingPayment ? "تسجيل التحصيل" : "تسجيل وتأكيد",''',
'''                            <label id="ygSwalWalletWrap" style="font-size:.75rem;font-weight:800;color:#42566A;display:none">نوع المحفظة
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
                    confirmButtonText: isRemainingPayment ? "تسجيل التحصيل" : "تسجيل وتأكيد",''')

rep(
'''                    confirmButtonColor: "#10B981",
                    preConfirm: () => {''',
'''                    confirmButtonColor: "#10B981",
                    didOpen: () => {
                        const methodSelect = document.getElementById("ygSwalMethod");
                        const walletWrap = document.getElementById("ygSwalWalletWrap");
                        const syncWallet = () => {
                            if (walletWrap) walletWrap.style.display = methodSelect?.value === "Wallet" ? "block" : "none";
                        };
                        methodSelect?.addEventListener("change", syncWallet);
                        syncWallet();
                    },
                    preConfirm: () => {''')

rep(
'''                        return {
                            amount: paid,
                            method: document.getElementById("ygSwalMethod")?.value || "Cash",
                            reference: document.getElementById("ygSwalRef")?.value?.trim() || ""
                        };''',
'''                        const selectedMethod = document.getElementById("ygSwalMethod")?.value || "Cash";
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
                        };''')

rep(
'''                amount = response.value.amount;
                method = response.value.method;
                reference = response.value.reference;''',
'''                amount = response.value.amount;
                method = response.value.method;
                ownerWalletId = response.value.ownerWalletId || null;
                reference = response.value.reference;''')

rep(
'''                    paidAmount: Number(amount),
                    paymentMethod: method,
                    paymentReferenceNumber: reference || null''',
'''                    paidAmount: Number(amount),
                    paymentMethod: method,
                    ownerWalletId,
                    paymentReferenceNumber: reference || null''')

PATH.write_bytes(text.encode('utf-8-sig' if bom else 'utf-8'))
print('js patch 3 applied')
