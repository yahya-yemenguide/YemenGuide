(() => {
    'use strict';

    window.OwnerFinancialTabs ??= {};

    const MODULE_KEY = 'balance';
    const MAX_RECEIPT_SIZE = 5 * 1024 * 1024;
    const ALLOWED_EXTENSIONS = ['jpg', 'jpeg', 'png', 'webp', 'pdf'];

    const Balance = {
        container: null,
        api: null,
        topUpDialog: null,
        movementDialog: null,
        previousFocus: null,
        handlers: {},

        init(container, api) {
            this.destroy();

            this.container =
                container?.querySelector(
                    '[data-owner-financial-partial="balance"]'
                ) || null;

            this.api = api || null;

            if (!this.container) {
                return;
            }

            this.topUpDialog =
                this.container.querySelector(
                    '[data-balance-topup-dialog]'
                );

            this.movementDialog =
                this.container.querySelector(
                    '[data-balance-movement-dialog]'
                );

            this.bindEvents();
            this.prepareEntryAnimation();
            this.animateWalletRail();
            this.openNotificationMovement();
        },

        openNotificationMovement() {
            let url;
            try {
                url = new URL(window.location.href);
            } catch {
                return;
            }

            const id = Number.parseInt(url.searchParams.get('focusTransactionId') || '0', 10) || 0;
            if (id <= 0) {
                return;
            }

            const button = this.container.querySelector(`[data-balance-movement-details="${id}"]`);
            if (!button) {
                return;
            }

            button.scrollIntoView({
                behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                block: 'center'
            });
            window.setTimeout(() => this.openMovementDialog(id, button), 120);

            url.searchParams.delete('focusTransactionId');
            window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}${url.hash}`);
        },

        destroy() {
            if (this.container && this.handlers.click) {
                this.container.removeEventListener(
                    'click',
                    this.handlers.click
                );
            }

            if (this.container && this.handlers.submit) {
                this.container.removeEventListener(
                    'submit',
                    this.handlers.submit
                );
            }

            if (this.container && this.handlers.change) {
                this.container.removeEventListener(
                    'change',
                    this.handlers.change
                );
            }

            if (this.container && this.handlers.paste) {
                this.container.removeEventListener(
                    'paste',
                    this.handlers.paste
                );
            }

            if (this.container && this.handlers.keydown) {
                this.container.removeEventListener(
                    'keydown',
                    this.handlers.keydown
                );
            }

            if (this.container && this.handlers.dragover) {
                this.container.removeEventListener(
                    'dragover',
                    this.handlers.dragover
                );
            }

            if (this.container && this.handlers.dragleave) {
                this.container.removeEventListener(
                    'dragleave',
                    this.handlers.dragleave
                );
            }

            if (this.container && this.handlers.drop) {
                this.container.removeEventListener(
                    'drop',
                    this.handlers.drop
                );
            }

            if (this.topUpDialog && this.handlers.cancelTopUpDialog) {
                this.topUpDialog.removeEventListener(
                    'cancel',
                    this.handlers.cancelTopUpDialog
                );
            }

            if (this.movementDialog && this.handlers.cancelMovementDialog) {
                this.movementDialog.removeEventListener(
                    'cancel',
                    this.handlers.cancelMovementDialog
                );
            }

            this.closeTopUpDialog(false);
            this.closeMovementDialog(false);

            this.container = null;
            this.api = null;
            this.topUpDialog = null;
            this.movementDialog = null;
            this.previousFocus = null;
            this.handlers = {};
        },

        bindEvents() {
            this.handlers.click = event => {
                const openTopUp =
                    event.target.closest(
                        '[data-balance-open-topup]'
                    );

                if (
                    openTopUp &&
                    this.container.contains(openTopUp)
                ) {
                    event.preventDefault();
                    this.openTopUpDialog(openTopUp);
                    return;
                }

                const closeTopUp =
                    event.target.closest(
                        '[data-balance-close-topup]'
                    );

                if (
                    closeTopUp &&
                    this.container.contains(closeTopUp)
                ) {
                    event.preventDefault();
                    this.closeTopUpDialog();
                    return;
                }

                const copyAccount =
                    event.target.closest(
                        '[data-balance-copy-account]'
                    );

                if (
                    copyAccount &&
                    this.container.contains(copyAccount)
                ) {
                    event.preventDefault();
                    event.stopPropagation();

                    this.copyText(
                        copyAccount.dataset.balanceCopyAccount || '',
                        copyAccount
                    );
                    return;
                }

                const toggleFilter =
                    event.target.closest(
                        '[data-balance-toggle-filter]'
                    );

                if (
                    toggleFilter &&
                    this.container.contains(toggleFilter)
                ) {
                    event.preventDefault();
                    this.toggleFilters(toggleFilter);
                    return;
                }

                const clearFilter =
                    event.target.closest(
                        '[data-balance-clear-filter]'
                    );

                if (
                    clearFilter &&
                    this.container.contains(clearFilter)
                ) {
                    event.preventDefault();
                    this.clearFilters();
                    return;
                }

                const pageButton =
                    event.target.closest(
                        '[data-balance-page]'
                    );

                if (
                    pageButton &&
                    this.container.contains(pageButton)
                ) {
                    event.preventDefault();

                    if (pageButton.disabled) {
                        return;
                    }

                    const page =
                        Number(
                            pageButton.dataset.balancePage || 0
                        );

                    if (
                        Number.isFinite(page) &&
                        page > 0
                    ) {
                        this.goToPage(page);
                    }

                    return;
                }

                const movementDetails =
                    event.target.closest(
                        '[data-balance-movement-details]'
                    );

                if (
                    movementDetails &&
                    this.container.contains(movementDetails)
                ) {
                    event.preventDefault();

                    this.openMovementDialog(
                        movementDetails.dataset.balanceMovementDetails,
                        movementDetails
                    );
                    return;
                }

                const closeMovement =
                    event.target.closest(
                        '[data-balance-close-movement]'
                    );

                if (
                    closeMovement &&
                    this.container.contains(closeMovement)
                ) {
                    event.preventDefault();
                    this.closeMovementDialog();
                    return;
                }

                const cancelTopUp =
                    event.target.closest(
                        '[data-balance-cancel-topup]'
                    );

                if (
                    cancelTopUp &&
                    this.container.contains(cancelTopUp)
                ) {
                    event.preventDefault();

                    const id =
                        Number(
                            cancelTopUp.dataset.balanceCancelTopup || 0
                        );

                    if (
                        Number.isFinite(id) &&
                        id > 0
                    ) {
                        this.cancelTopUp(
                            id,
                            cancelTopUp
                        );
                    }
                }
            };

            this.handlers.submit = event => {
                const filterForm =
                    event.target.closest(
                        '[data-balance-filter-form]'
                    );

                if (
                    filterForm &&
                    this.container.contains(filterForm)
                ) {
                    event.preventDefault();
                    this.applyFilters(filterForm);
                    return;
                }

                const topUpForm =
                    event.target.closest(
                        '[data-balance-topup-form]'
                    );

                if (
                    topUpForm &&
                    this.container.contains(topUpForm)
                ) {
                    event.preventDefault();
                    this.submitTopUp(topUpForm);
                }
            };

            this.handlers.change = event => {
                const receipt =
                    event.target.closest(
                        'input[name="receiptFile"]'
                    );

                if (
                    receipt &&
                    this.container.contains(receipt)
                ) {
                    this.updateReceiptState(
                        receipt.files?.[0] || null
                    );
                }
            };

            this.handlers.paste = event => {
                const amountInput = event.target.closest(
                    '[data-balance-positive-money]'
                );

                if (
                    amountInput &&
                    this.container.contains(amountInput)
                ) {
                    const pasted =
                        String(
                            event.clipboardData?.getData('text') || ''
                        ).trim();

                    if (!/^\d{1,16}(?:\.\d{0,2})?$/.test(pasted)) {
                        event.preventDefault();
                        this.setTopUpMessage(
                            'مبلغ الشحن يقبل أرقامًا موجبة ومنزلتين عشريتين فقط.'
                        );
                    }
                }
            };

            this.handlers.keydown = event => {
                const amountInput = event.target.closest(
                    '[data-balance-positive-money]'
                );

                if (
                    amountInput &&
                    this.container.contains(amountInput) &&
                    ['-', '+', 'e', 'E', ' '].includes(event.key)
                ) {
                    event.preventDefault();
                }
            };

            this.handlers.dragover = event => {
                const drop =
                    event.target.closest(
                        '[data-balance-receipt-drop]'
                    );

                if (
                    !drop ||
                    !this.container.contains(drop)
                ) {
                    return;
                }

                event.preventDefault();
                drop.classList.add('is-dragging');
            };

            this.handlers.dragleave = event => {
                const drop =
                    event.target.closest(
                        '[data-balance-receipt-drop]'
                    );

                if (
                    !drop ||
                    !this.container.contains(drop)
                ) {
                    return;
                }

                drop.classList.remove('is-dragging');
            };

            this.handlers.drop = event => {
                const drop =
                    event.target.closest(
                        '[data-balance-receipt-drop]'
                    );

                if (
                    !drop ||
                    !this.container.contains(drop)
                ) {
                    return;
                }

                event.preventDefault();
                drop.classList.remove('is-dragging');

                const file =
                    event.dataTransfer?.files?.[0] || null;

                const input =
                    drop.querySelector(
                        'input[name="receiptFile"]'
                    );

                if (
                    file &&
                    input &&
                    typeof DataTransfer !== 'undefined'
                ) {
                    const transfer =
                        new DataTransfer();

                    transfer.items.add(file);
                    input.files = transfer.files;

                    this.updateReceiptState(file);
                }
            };

            this.handlers.cancelTopUpDialog = event => {
                event.preventDefault();
                this.closeTopUpDialog();
            };

            this.handlers.cancelMovementDialog = event => {
                event.preventDefault();
                this.closeMovementDialog();
            };

            this.container.addEventListener(
                'click',
                this.handlers.click
            );

            this.container.addEventListener(
                'submit',
                this.handlers.submit
            );

            this.container.addEventListener(
                'change',
                this.handlers.change
            );

            this.container.addEventListener(
                'paste',
                this.handlers.paste
            );

            this.container.addEventListener(
                'keydown',
                this.handlers.keydown
            );

            this.container.addEventListener(
                'dragover',
                this.handlers.dragover
            );

            this.container.addEventListener(
                'dragleave',
                this.handlers.dragleave
            );

            this.container.addEventListener(
                'drop',
                this.handlers.drop
            );

            this.topUpDialog?.addEventListener(
                'cancel',
                this.handlers.cancelTopUpDialog
            );

            this.movementDialog?.addEventListener(
                'cancel',
                this.handlers.cancelMovementDialog
            );
        },

        toggleFilters(button) {
            const form =
                this.container.querySelector(
                    '[data-balance-filter-form]'
                );

            if (!form) {
                return;
            }

            const willOpen = form.hidden;
            form.hidden = !willOpen;

            button.setAttribute(
                'aria-expanded',
                willOpen ? 'true' : 'false'
            );

            if (
                willOpen &&
                !window.matchMedia?.(
                    '(prefers-reduced-motion: reduce)'
                )?.matches
            ) {
                form.animate?.(
                    [
                        {
                            opacity: 0,
                            transform: 'translateY(-6px)'
                        },
                        {
                            opacity: 1,
                            transform: 'translateY(0)'
                        }
                    ],
                    {
                        duration: 220,
                        easing:
                            'cubic-bezier(.22,.78,.24,1)'
                    }
                );
            }
        },

        applyFilters(form) {
            const fromDate =
                form.querySelector(
                    '[name="FromDate"]'
                )?.value || '';

            const toDate =
                form.querySelector(
                    '[name="ToDate"]'
                )?.value || '';

            if (
                fromDate &&
                toDate &&
                fromDate > toDate
            ) {
                this.toast(
                    'تاريخ البداية يجب أن يسبق تاريخ النهاية.',
                    'warning'
                );
                return;
            }

            const query =
                this.api?.serializeForm(
                    form,
                    { PageNumber: 1 }
                ) ||
                this.serializeForm(form);

            query.set(
                'PageNumber',
                '1'
            );

            this.reload(query);
        },

        clearFilters() {
            const form =
                this.container.querySelector(
                    '[data-balance-filter-form]'
                );

            form?.reset();

            this.reload(
                new URLSearchParams()
            );
        },

        goToPage(page) {
            const form =
                this.container.querySelector(
                    '[data-balance-filter-form]'
                );

            const query =
                form
                    ? (
                        this.api?.serializeForm(
                            form,
                            { PageNumber: page }
                        ) ||
                        this.serializeForm(form)
                    )
                    : new URLSearchParams();

            query.set(
                'PageNumber',
                String(page)
            );

            this.reload(query);
        },

        async reload(query) {
            if (
                this.api &&
                typeof this.api.reload === 'function'
            ) {
                await this.api.reload({
                    query,
                    history: 'replace',
                    keepScroll: true
                });
                return;
            }

            this.toast(
                'تعذر تحديث القسم خارج واجهة المالية.',
                'error'
            );
        },

        openTopUpDialog(trigger) {
            if (!this.topUpDialog) {
                this.toast(
                    'نافذة الشحن غير متاحة.',
                    'error'
                );
                return;
            }

            this.previousFocus =
                trigger ||
                document.activeElement;

            const form =
                this.topUpDialog.querySelector(
                    '[data-balance-topup-form]'
                );

            const transferDate =
                form?.querySelector(
                    'input[name="TransferDate"]'
                );

            if (
                transferDate &&
                !transferDate.value
            ) {
                transferDate.value =
                    this.toLocalDateTimeValue(
                        new Date()
                    );
            }

            this.setTopUpMessage('');
            this.updateReceiptState(null);

            if (
                typeof this.topUpDialog.showModal ===
                'function'
            ) {
                if (!this.topUpDialog.open) {
                    this.topUpDialog.showModal();
                }
            } else {
                this.topUpDialog.setAttribute(
                    'open',
                    ''
                );
            }

            document.documentElement.classList.add(
                'owner-financial-dialog-open'
            );

            const scroll =
                this.topUpDialog.querySelector(
                    '[data-balance-dialog-scroll]'
                );

            if (scroll) {
                scroll.scrollTop = 0;
            }
        },

        closeTopUpDialog(restoreFocus = true) {
            if (!this.topUpDialog) {
                return;
            }

            if (
                typeof this.topUpDialog.close ===
                'function' &&
                this.topUpDialog.open
            ) {
                this.topUpDialog.close();
            } else {
                this.topUpDialog.removeAttribute(
                    'open'
                );
            }

            this.syncDialogLock();

            if (
                restoreFocus &&
                this.previousFocus instanceof HTMLElement
            ) {
                this.previousFocus.focus({
                    preventScroll: true
                });
            }

            this.previousFocus = null;
        },

        openMovementDialog(id, trigger) {
            if (
                !this.movementDialog ||
                !id
            ) {
                return;
            }

            const template =
                [
                    ...this.container.querySelectorAll(
                        '[data-balance-movement-template]'
                    )
                ]
                    .find(
                        item =>
                            String(
                                item.dataset.balanceMovementTemplate ||
                                ''
                            ) ===
                            String(id)
                    );

            const body =
                this.movementDialog.querySelector(
                    '[data-balance-movement-content]'
                );

            if (
                !template ||
                !body
            ) {
                this.toast(
                    'تعذر قراءة تفاصيل الحركة.',
                    'error'
                );
                return;
            }

            this.previousFocus =
                trigger ||
                document.activeElement;

            body.replaceChildren(
                template.content.cloneNode(true)
            );

            if (
                typeof this.movementDialog.showModal ===
                'function'
            ) {
                if (!this.movementDialog.open) {
                    this.movementDialog.showModal();
                }
            } else {
                this.movementDialog.setAttribute(
                    'open',
                    ''
                );
            }

            document.documentElement.classList.add(
                'owner-financial-dialog-open'
            );

            body.scrollTop = 0;
        },

        closeMovementDialog(restoreFocus = true) {
            if (!this.movementDialog) {
                return;
            }

            if (
                typeof this.movementDialog.close ===
                'function' &&
                this.movementDialog.open
            ) {
                this.movementDialog.close();
            } else {
                this.movementDialog.removeAttribute(
                    'open'
                );
            }

            this.syncDialogLock();

            if (
                restoreFocus &&
                this.previousFocus instanceof HTMLElement
            ) {
                this.previousFocus.focus({
                    preventScroll: true
                });
            }

            this.previousFocus = null;
        },

        syncDialogLock() {
            const hasOpenDialog =
                Boolean(
                    this.topUpDialog?.open ||
                    this.movementDialog?.open
                );

            document.documentElement.classList.toggle(
                'owner-financial-dialog-open',
                hasOpenDialog
            );
        },

        async submitTopUp(form) {
            const submitButton =
                form.querySelector(
                    '[data-balance-submit-topup]'
                ) ||
                form.querySelector(
                    'button[type="submit"]'
                );

            const walletId =
                Number(
                    form.querySelector(
                        'input[name="PlatformWalletId"]:checked'
                    )?.value || 0
                );

            const amountInput =
                form.querySelector(
                    '[name="Amount"]'
                );

            const amountText =
                String(amountInput?.value || '').trim();

            const amount =
                Number(
                    amountText || 0
                );

            const minimum =
                Number(
                    amountInput?.min || 0
                );

            const reference =
                String(
                    form.querySelector(
                        '[name="ReferenceNumber"]'
                    )?.value || ''
                ).trim();

            const transferDate =
                String(
                    form.querySelector(
                        '[name="TransferDate"]'
                    )?.value || ''
                ).trim();

            const receipt =
                form.querySelector(
                    'input[name="receiptFile"]'
                )?.files?.[0] || null;

            if (
                !Number.isFinite(walletId) ||
                walletId <= 0
            ) {
                this.setTopUpMessage(
                    'اختر حساب المنصة الذي حوّلت إليه.'
                );
                return;
            }

            if (
                !/^(?=.*[1-9])\d{1,16}(?:\.\d{1,2})?$/.test(amountText) ||
                !Number.isFinite(amount) ||
                amount <= 0 ||
                amount > 9999999999999999.99
            ) {
                this.setTopUpMessage(
                    'أدخل مبلغًا موجبًا بالأرقام فقط وبمنزلتين عشريتين كحد أقصى.'
                );
                return;
            }

            if (
                Number.isFinite(minimum) &&
                minimum > 0 &&
                amount < minimum
            ) {
                this.setTopUpMessage(
                    `مبلغ الشحن أقل من الحد الأدنى (${minimum}).`
                );
                return;
            }

            if (
                !/^[A-Za-z0-9/-]{4,50}$/.test(
                    reference
                )
            ) {
                this.setTopUpMessage(
                    'رقم العملية غير صحيح. استخدم حروفًا إنجليزية وأرقامًا و - أو /.'
                );
                return;
            }

            if (!transferDate) {
                this.setTopUpMessage(
                    'أدخل تاريخ ووقت التحويل.'
                );
                return;
            }

            if (!receipt) {
                this.setTopUpMessage(
                    'أرفق سند التحويل.'
                );
                return;
            }

            const fileError =
                this.validateReceipt(receipt);

            if (fileError) {
                this.setTopUpMessage(
                    fileError
                );
                return;
            }

            const url =
                this.container.dataset.topupUrl;

            if (!url) {
                this.setTopUpMessage(
                    'رابط إرسال طلب الشحن غير متاح.'
                );
                return;
            }

            this.setTopUpMessage('');

            const formData =
                new FormData(form);

            try {
                this.api?.setButtonBusy(
                    submitButton,
                    true,
                    'جاري الإرسال...'
                );

                const result =
                    await this.api.fetchJson(
                        url,
                        {
                            method: 'POST',
                            body: formData
                        }
                    );

                if (
                    result?.success === false
                ) {
                    throw new Error(
                        result.message ||
                        'تعذر إرسال طلب الشحن.'
                    );
                }

                this.toast(
                    result?.message ||
                    'تم إرسال طلب الشحن بنجاح.',
                    'success'
                );

                form.reset();
                this.updateReceiptState(null);
                this.closeTopUpDialog(false);

                await this.api?.reload({
                    history: 'replace',
                    keepScroll: true
                });
            } catch (error) {
                this.setTopUpMessage(
                    error?.message ||
                    'تعذر إرسال طلب الشحن حاليًا.'
                );
            } finally {
                this.api?.setButtonBusy(
                    submitButton,
                    false
                );
            }
        },

        async cancelTopUp(transactionId, button) {
            const confirmed =
                await this.api?.confirm(
                    'سيتم إلغاء طلب الشحن المعلّق فقط. هل تريد المتابعة؟',
                    'إلغاء طلب الشحن'
                );

            if (!confirmed) {
                return;
            }

            const baseUrl =
                this.container.dataset.cancelTopupBaseUrl ||
                '';

            if (!baseUrl) {
                this.toast(
                    'رابط إلغاء طلب الشحن غير متاح.',
                    'error'
                );
                return;
            }

            const url =
                `${baseUrl.replace(/\/+$/, '')}/${transactionId}`;

            const token =
                this.api?.getAntiForgeryToken() ||
                '';

            const body =
                new URLSearchParams();

            if (token) {
                body.set(
                    '__RequestVerificationToken',
                    token
                );
            }

            try {
                this.api?.setButtonBusy(
                    button,
                    true,
                    'جاري الإلغاء...'
                );

                const result =
                    await this.api.fetchJson(
                        url,
                        {
                            method: 'POST',
                            headers: {
                                'Content-Type':
                                    'application/x-www-form-urlencoded; charset=UTF-8'
                            },
                            body:
                                body.toString()
                        }
                    );

                this.toast(
                    result?.message ||
                    'تم إلغاء طلب الشحن.',
                    'success'
                );

                await this.api?.reload({
                    history: 'replace',
                    keepScroll: true
                });
            } catch (error) {
                this.toast(
                    error?.message ||
                    'تعذر إلغاء طلب الشحن.',
                    'error'
                );
            } finally {
                this.api?.setButtonBusy(
                    button,
                    false
                );
            }
        },

        updateReceiptState(file) {
            const drop =
                this.container?.querySelector(
                    '[data-balance-receipt-drop]'
                );

            const title =
                this.container?.querySelector(
                    '[data-balance-receipt-title]'
                );

            const subtitle =
                this.container?.querySelector(
                    '[data-balance-receipt-subtitle]'
                );

            if (!drop || !title || !subtitle) {
                return;
            }

            drop.classList.remove(
                'has-file',
                'has-error'
            );

            if (!file) {
                title.textContent =
                    'اسحب السند هنا أو اضغط للاختيار';

                subtitle.textContent =
                    'JPG, PNG, WEBP أو PDF — حتى 5 MB';

                return;
            }

            const error =
                this.validateReceipt(file);

            if (error) {
                drop.classList.add(
                    'has-error'
                );

                title.textContent =
                    file.name;

                subtitle.textContent =
                    error;

                return;
            }

            drop.classList.add(
                'has-file'
            );

            title.textContent =
                file.name;

            subtitle.textContent =
                `${this.formatFileSize(file.size)} — جاهز للرفع`;
        },

        validateReceipt(file) {
            if (!file) {
                return 'أرفق سند التحويل.';
            }

            if (
                file.size >
                MAX_RECEIPT_SIZE
            ) {
                return 'حجم سند التحويل يجب ألا يتجاوز 5 ميجابايت.';
            }

            const extension =
                String(
                    file.name
                        .split('.')
                        .pop() || ''
                )
                    .trim()
                    .toLowerCase();

            if (
                !ALLOWED_EXTENSIONS.includes(
                    extension
                )
            ) {
                return 'نوع الملف غير مدعوم.';
            }

            return '';
        },

        setTopUpMessage(message) {
            const target =
                this.container?.querySelector(
                    '[data-balance-topup-message]'
                );

            if (!target) {
                return;
            }

            target.textContent =
                message || '';
        },

        async copyText(value, button) {
            const text =
                String(value || '').trim();

            if (!text) {
                return;
            }

            try {
                if (
                    navigator.clipboard &&
                    window.isSecureContext
                ) {
                    await navigator.clipboard.writeText(
                        text
                    );
                } else {
                    const textarea =
                        document.createElement(
                            'textarea'
                        );

                    textarea.value = text;
                    textarea.style.position =
                        'fixed';
                    textarea.style.opacity =
                        '0';

                    document.body.appendChild(
                        textarea
                    );

                    textarea.select();

                    document.execCommand(
                        'copy'
                    );

                    textarea.remove();
                }

                const icon =
                    button?.querySelector(
                        'i'
                    );

                if (icon) {
                    const original =
                        icon.className;

                    icon.className =
                        'bi bi-check-lg';

                    window.setTimeout(
                        () => {
                            icon.className =
                                original;
                        },
                        1100
                    );
                }

                this.toast(
                    'تم نسخ رقم الحساب.',
                    'success'
                );
            } catch {
                this.toast(
                    'تعذر نسخ رقم الحساب.',
                    'error'
                );
            }
        },

        serializeForm(form) {
            const query =
                new URLSearchParams();

            new FormData(form)
                .forEach(
                    (value, key) => {
                        const text =
                            String(
                                value ?? ''
                            ).trim();

                        if (text) {
                            query.set(
                                key,
                                text
                            );
                        }
                    }
                );

            return query;
        },

        prepareEntryAnimation() {
            if (
                window.matchMedia?.(
                    '(prefers-reduced-motion: reduce)'
                )?.matches
            ) {
                return;
            }

            const elements =
                [
                    ...this.container.querySelectorAll(
                        '.owner-balance-center__movement-card'
                    )
                ];

            elements.forEach(
                (element, index) => {
                    element.animate?.(
                        [
                            {
                                opacity: 0,
                                transform:
                                    'translateY(8px)'
                            },
                            {
                                opacity: 1,
                                transform:
                                    'translateY(0)'
                            }
                        ],
                        {
                            duration: 330,
                            delay:
                                Math.min(
                                    index * 34,
                                    220
                                ),
                            easing:
                                'cubic-bezier(.22,.78,.24,1)',
                            fill: 'both'
                        }
                    );
                }
            );
        },

        animateWalletRail() {
            if (
                window.matchMedia?.(
                    '(prefers-reduced-motion: reduce)'
                )?.matches
            ) {
                return;
            }

            this.container
                ?.querySelectorAll(
                    '.owner-balance-center__wallet-rail span'
                )
                .forEach(
                    (bar, index) => {
                        const target =
                            getComputedStyle(bar)
                                .getPropertyValue(
                                    '--wallet-part'
                                )
                                .trim();

                        if (!target) {
                            return;
                        }

                        bar.style.setProperty(
                            '--wallet-part',
                            '0%'
                        );

                        window.setTimeout(
                            () => {
                                bar.style.setProperty(
                                    '--wallet-part',
                                    target
                                );
                            },
                            120 + index * 90
                        );
                    }
                );
        },

        toast(message, type = 'info') {
            if (
                this.api &&
                typeof this.api.showToast ===
                'function'
            ) {
                this.api.showToast(
                    message,
                    type
                );
                return;
            }

            console[
                type === 'error'
                    ? 'error'
                    : 'log'
            ](message);
        },

        formatFileSize(bytes) {
            if (
                !Number.isFinite(bytes) ||
                bytes <= 0
            ) {
                return '0 KB';
            }

            if (
                bytes <
                1024 * 1024
            ) {
                return `${Math.max(
                    1,
                    Math.round(bytes / 1024)
                )} KB`;
            }

            return `${(
                bytes /
                (1024 * 1024)
            ).toFixed(1)} MB`;
        },

        toLocalDateTimeValue(date) {
            const pad =
                value =>
                    String(value)
                        .padStart(2, '0');

            return [
                date.getFullYear(),
                '-',
                pad(date.getMonth() + 1),
                '-',
                pad(date.getDate()),
                'T',
                pad(date.getHours()),
                ':',
                pad(date.getMinutes())
            ].join('');
        }
    };

    window.OwnerFinancialTabs[
        MODULE_KEY
    ] = Balance;
})();
