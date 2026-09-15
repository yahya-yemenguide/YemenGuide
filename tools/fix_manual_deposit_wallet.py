from pathlib import Path

path = Path('YemenGuide.Web/wwwroot/js/owner/calendar-manager.js')
text = path.read_text(encoding='utf-8-sig')

replacements = []

replacements.append((
'''            on(elements.paymentMethod, "change", () => {\n                updateSmartSelectValue(elements.paymentMethod, elements.paymentSelectText);\n                syncOtherFields();\n                clearFieldError("paymentMethod");\n                clearFieldError("ownerWallet");\n                scheduleQuotePreview();\n            });''',
'''            on(elements.paymentMethod, "change", () => {\n                updateSmartSelectValue(elements.paymentMethod, elements.paymentSelectText);\n                // إعادة مزامنة لوحة الدفع كاملة حتى يظهر اختيار نوع المحفظة فورًا\n                // عند اختيار Wallet من الـ smart select، وليس فقط في تحديثات الحقول الأخرى.\n                syncPaymentFields();\n                clearFieldError("paymentMethod");\n                clearFieldError("ownerWallet");\n                scheduleQuotePreview();\n            });'''
))

replacements.append((
'''        function syncPaymentFields() {\n            const collected = getPaymentMode() === "Collected";\n            elements.collectedFields.hidden = !collected;\n            if (!collected) {\n                elements.paidAmount.value = "0";\n                elements.paidAmount.readOnly = false;\n                elements.paymentReference.value = "";\n                if (elements.paymentMethodOther) elements.paymentMethodOther.value = "";\n                clearFieldError("paidAmount");\n                clearFieldError("paymentMethod");\n                clearFieldError("paymentMethodOther");\n                clearFieldError("ownerWallet");\n            } else {\n                syncCollectedDepositFromQuote();\n            }\n            syncOtherFields();\n        }''',
'''        function syncPaymentFields() {\n            const collected = getPaymentMode() === "Collected";\n            elements.collectedFields.hidden = !collected;\n            if (!collected) {\n                elements.paidAmount.value = "0";\n                elements.paidAmount.readOnly = false;\n                elements.paymentReference.value = "";\n                if (elements.paymentMethodOther) elements.paymentMethodOther.value = "";\n                clearFieldError("paidAmount");\n                clearFieldError("paymentMethod");\n                clearFieldError("paymentMethodOther");\n                clearFieldError("ownerWallet");\n            } else {\n                syncCollectedDepositFromQuote();\n                syncOwnerWalletChoices();\n            }\n            syncOtherFields();\n        }\n\n        function syncOwnerWalletChoices() {\n            if (!elements.ownerWalletId) return;\n\n            const wallets = Array.isArray(manualOptions?.wallets)\n                ? manualOptions.wallets\n                : (Array.isArray(context?.wallets) ? context.wallets : []);\n\n            const currentValue = elements.ownerWalletId.value;\n            const currentStillExists = wallets.some(wallet =>\n                String(wallet.ownerWalletId) === String(currentValue));\n\n            elements.ownerWalletId.innerHTML =\n                '<option value="">اختر المحفظة</option>' +\n                wallets.map(wallet => {\n                    const label = wallet.displayText ||\n                        [wallet.walletName, wallet.accountNumber].filter(Boolean).join(" · ");\n                    return `<option value="${wallet.ownerWalletId}">${escapeHtml(label || "محفظة")}</option>`;\n                }).join("");\n\n            if (currentStillExists) {\n                elements.ownerWalletId.value = currentValue;\n            } else if (wallets.length === 1) {\n                // إذا كان لدى المالك حساب استقبال واحد فقط فاختياره تلقائيًا أوضح وأسرع.\n                elements.ownerWalletId.value = String(wallets[0].ownerWalletId);\n            } else {\n                elements.ownerWalletId.value = "";\n            }\n\n            refreshDynamicSmartSelect(elements.ownerWalletId);\n            updateSmartSelectValue(elements.ownerWalletId, elements.ownerWalletSelectText);\n        }'''
))

replacements.append((
'''            const wallets = Array.isArray(data.wallets)\n                ? data.wallets\n                : (Array.isArray(context?.wallets) ? context.wallets : []);\n            if (elements.ownerWalletId) {\n                const previousWallet = elements.ownerWalletId.value;\n                elements.ownerWalletId.innerHTML = '<option value="">اختر المحفظة</option>' + wallets.map(wallet => {\n                    const label = wallet.displayText || [wallet.walletName, wallet.accountNumber].filter(Boolean).join(" · ");\n                    return `<option value="${wallet.ownerWalletId}">${escapeHtml(label || "محفظة")}</option>`;\n                }).join("");\n                if (wallets.some(wallet => String(wallet.ownerWalletId) === previousWallet)) {\n                    elements.ownerWalletId.value = previousWallet;\n                }\n                refreshDynamicSmartSelect(elements.ownerWalletId);\n            }''',
'''            syncOwnerWalletChoices();'''
))

replacements.append((
'''        async function nextManualStep() {\n            if (manualStep >= 4) return;\n            if (!validateManualStep(manualStep)) return;\n\n            if (manualStep === 3) {\n                const preview = await previewManualBooking(true);\n                if (!preview) return;\n            }\n\n            setManualStep(manualStep + 1);\n        }''',
'''        async function nextManualStep() {\n            if (manualStep >= 4) return;\n            if (!validateManualStep(manualStep)) return;\n\n            // قبل فتح خطوة الدفع نحتاج السعر والعربون الآن، لا في خطوة المراجعة فقط.\n            // بهذه المعاينة المبكرة يصبح latestManualQuote جاهزًا، وعند اختيار\n            // "تم استلام مبلغ" يُملأ العربون مباشرة من سياسة المنشأة.\n            if (manualStep === 2) {\n                const pricingPreview = await previewManualBooking(true);\n                if (!pricingPreview) return;\n            }\n\n            if (manualStep === 3) {\n                const preview = await previewManualBooking(true);\n                if (!preview) return;\n            }\n\n            setManualStep(manualStep + 1);\n\n            if (manualStep === 3) {\n                syncPaymentFields();\n            }\n        }'''
))

for old, new in replacements:
    if old not in text:
        raise SystemExit('Expected block not found:\n' + old[:180])
    text = text.replace(old, new, 1)

path.write_text(text, encoding='utf-8-sig')
print('calendar-manager.js patched successfully')
