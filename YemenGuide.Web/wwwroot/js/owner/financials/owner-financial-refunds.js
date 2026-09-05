(() => {
    'use strict';

    window.OwnerFinancialTabs ??= {};

    const MODULE_KEY = 'refunds';
    const MAX_PROOF_SIZE = 5 * 1024 * 1024;
    const ALLOWED_EXTENSIONS = ['jpg', 'jpeg', 'png', 'webp'];

    const Refunds = {
        root: null,
        api: null,
        detailsDialog: null,
        transferDialog: null,
        previousFocus: null,
        handlers: {},

        init(container, api) {
            this.destroy();

            this.root =
                container?.querySelector(
                    '[data-owner-financial-partial="refunds"]'
                ) || null;

            this.api = api || null;

            if (!this.root) {
                return;
            }

            this.detailsDialog =
                this.root.querySelector(
                    '[data-refund-details-dialog]'
                );

            this.transferDialog =
                this.root.querySelector(
                    '[data-refund-transfer-dialog]'
                );

            this.bind();
            this.animateCards();
            this.openNotificationTarget();
        },

        openNotificationTarget() {
            let url;
            try {
                url = new URL(window.location.href);
            } catch {
                return;
            }

            const refundId = Number.parseInt(url.searchParams.get('focusRefundId') || '0', 10) || 0;
            const bookingId = Number.parseInt(url.searchParams.get('focusBookingId') || '0', 10) || 0;

            let card = null;
            if (refundId > 0) {
                card = this.root.querySelector(`[data-refund-card][data-refund-id="${refundId}"]`);
            }
            if (!card && bookingId > 0) {
                card = this.root.querySelector(`[data-refund-card][data-booking-id="${bookingId}"]`);
            }
            if (!card) {
                return;
            }

            const button = card.querySelector('[data-refund-open-details]');
            const id = button?.dataset.refundOpenDetails;
            if (!id) {
                return;
            }

            card.scrollIntoView({
                behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                block: 'center'
            });
            card.classList.add('is-notification-focus');
            window.setTimeout(() => card.classList.remove('is-notification-focus'), 4200);
            window.setTimeout(() => this.openDetails(id, button), 120);

            url.searchParams.delete('focusRefundId');
            url.searchParams.delete('focusBookingId');
            // BookingId نتركه كفلتر حتى يبقى الطلب المقصود وحده في القائمة.
            window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}${url.hash}`);
        },

        destroy() {
            if (this.root && this.handlers.click) {
                this.root.removeEventListener(
                    'click',
                    this.handlers.click
                );
            }

            if (this.root && this.handlers.submit) {
                this.root.removeEventListener(
                    'submit',
                    this.handlers.submit
                );
            }

            if (this.root && this.handlers.change) {
                this.root.removeEventListener(
                    'change',
                    this.handlers.change
                );
            }

            if (this.root && this.handlers.dragover) {
                this.root.removeEventListener(
                    'dragover',
                    this.handlers.dragover
                );
            }

            if (this.root && this.handlers.dragleave) {
                this.root.removeEventListener(
                    'dragleave',
                    this.handlers.dragleave
                );
            }

            if (this.root && this.handlers.drop) {
                this.root.removeEventListener(
                    'drop',
                    this.handlers.drop
                );
            }

            this.detailsDialog?.removeEventListener(
                'cancel',
                this.handlers.cancelDetails
            );

            this.transferDialog?.removeEventListener(
                'cancel',
                this.handlers.cancelTransfer
            );

            this.closeDetails(false);
            this.closeTransfer(false);

            this.root = null;
            this.api = null;
            this.detailsDialog = null;
            this.transferDialog = null;
            this.previousFocus = null;
            this.handlers = {};
        },

        bind() {
            this.handlers.click = event => {
                const advanced =
                    event.target.closest(
                        '[data-refund-toggle-advanced]'
                    );

                if (advanced && this.root.contains(advanced)) {
                    event.preventDefault();
                    this.toggleAdvanced(advanced);
                    return;
                }

                const clear =
                    event.target.closest(
                        '[data-refund-clear-filter]'
                    );

                if (clear && this.root.contains(clear)) {
                    event.preventDefault();
                    this.clearFilters();
                    return;
                }

                const page =
                    event.target.closest(
                        '[data-refund-page]'
                    );

                if (page && this.root.contains(page)) {
                    event.preventDefault();

                    if (page.disabled) {
                        return;
                    }

                    const number =
                        Number(
                            page.dataset.refundPage || 0
                        );

                    if (Number.isFinite(number) && number > 0) {
                        this.goToPage(number);
                    }

                    return;
                }

                const details =
                    event.target.closest(
                        '[data-refund-open-details]'
                    );

                if (details && this.root.contains(details)) {
                    event.preventDefault();

                    this.openDetails(
                        details.dataset.refundOpenDetails,
                        details
                    );

                    return;
                }

                const closeDetails =
                    event.target.closest(
                        '[data-refund-close-details]'
                    );

                if (closeDetails && this.root.contains(closeDetails)) {
                    event.preventDefault();
                    this.closeDetails();
                    return;
                }

                const transfer =
                    event.target.closest(
                        '[data-refund-open-transfer]'
                    );

                if (transfer && this.root.contains(transfer)) {
                    event.preventDefault();
                    this.openTransfer(transfer);
                    return;
                }

                const closeTransfer =
                    event.target.closest(
                        '[data-refund-close-transfer]'
                    );

                if (closeTransfer && this.root.contains(closeTransfer)) {
                    event.preventDefault();
                    this.closeTransfer();
                    return;
                }

                const copy =
                    event.target.closest(
                        '[data-refund-copy-account]'
                    );

                if (copy && this.root.contains(copy)) {
                    event.preventDefault();

                    const value =
                        this.transferDialog
                            ?.querySelector(
                                '[data-refund-transfer-account]'
                            )
                            ?.textContent
                            ?.trim() || '';

                    this.copyText(value, copy);
                }
            };

            this.handlers.submit = event => {
                const filterForm =
                    event.target.closest(
                        '[data-refund-filter-form]'
                    );

                if (filterForm && this.root.contains(filterForm)) {
                    event.preventDefault();
                    this.applyFilters(filterForm);
                    return;
                }

                const transferForm =
                    event.target.closest(
                        '[data-refund-transfer-form]'
                    );

                if (transferForm && this.root.contains(transferForm)) {
                    event.preventDefault();
                    this.submitTransfer(transferForm);
                }
            };

            this.handlers.change = event => {
                const proof =
                    event.target.closest(
                        'input[name="proofFile"]'
                    );

                if (proof && this.root.contains(proof)) {
                    this.updateProof(
                        proof.files?.[0] || null
                    );
                }
            };

            this.handlers.dragover = event => {
                const drop =
                    event.target.closest(
                        '[data-refund-proof-drop]'
                    );

                if (!drop || !this.root.contains(drop)) {
                    return;
                }

                event.preventDefault();
                drop.classList.add('is-dragging');
            };

            this.handlers.dragleave = event => {
                const drop =
                    event.target.closest(
                        '[data-refund-proof-drop]'
                    );

                if (!drop || !this.root.contains(drop)) {
                    return;
                }

                drop.classList.remove('is-dragging');
            };

            this.handlers.drop = event => {
                const drop =
                    event.target.closest(
                        '[data-refund-proof-drop]'
                    );

                if (!drop || !this.root.contains(drop)) {
                    return;
                }

                event.preventDefault();
                drop.classList.remove('is-dragging');

                const file =
                    event.dataTransfer?.files?.[0] || null;

                const input =
                    drop.querySelector(
                        'input[name="proofFile"]'
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

                    this.updateProof(file);
                }
            };

            this.handlers.cancelDetails = event => {
                event.preventDefault();
                this.closeDetails();
            };

            this.handlers.cancelTransfer = event => {
                event.preventDefault();
                this.closeTransfer();
            };

            this.root.addEventListener(
                'click',
                this.handlers.click
            );

            this.root.addEventListener(
                'submit',
                this.handlers.submit
            );

            this.root.addEventListener(
                'change',
                this.handlers.change
            );

            this.root.addEventListener(
                'dragover',
                this.handlers.dragover
            );

            this.root.addEventListener(
                'dragleave',
                this.handlers.dragleave
            );

            this.root.addEventListener(
                'drop',
                this.handlers.drop
            );

            this.detailsDialog?.addEventListener(
                'cancel',
                this.handlers.cancelDetails
            );

            this.transferDialog?.addEventListener(
                'cancel',
                this.handlers.cancelTransfer
            );
        },

        toggleAdvanced(button) {
            const panel =
                this.root.querySelector(
                    '[data-refund-advanced]'
                );

            if (!panel) {
                return;
            }

            const open =
                panel.hidden;

            panel.hidden =
                !open;

            button.setAttribute(
                'aria-expanded',
                open ? 'true' : 'false'
            );

            if (
                open &&
                !this.reducedMotion()
            ) {
                panel.animate?.(
                    [
                        {
                            opacity: 0,
                            transform: 'translateY(-5px)'
                        },
                        {
                            opacity: 1,
                            transform: 'translateY(0)'
                        }
                    ],
                    {
                        duration: 190,
                        easing:
                            'cubic-bezier(.22,.78,.24,1)'
                    }
                );
            }
        },

        applyFilters(form) {
            const from =
                form.querySelector(
                    '[name="FromDate"]'
                )?.value || '';

            const to =
                form.querySelector(
                    '[name="ToDate"]'
                )?.value || '';

            if (from && to && from > to) {
                this.toast(
                    'تاريخ البداية يجب أن يسبق تاريخ النهاية.',
                    'warning'
                );
                return;
            }

            const query =
                this.serializeForm(form);

            query.set(
                'PageNumber',
                '1'
            );

            this.reload(query);
        },

        clearFilters() {
            this.reload(
                new URLSearchParams()
            );
        },

        goToPage(page) {
            const form =
                this.root.querySelector(
                    '[data-refund-filter-form]'
                );

            const query =
                form
                    ? this.serializeForm(form)
                    : new URLSearchParams();

            query.set(
                'PageNumber',
                String(page)
            );

            this.reload(query);
        },

        serializeForm(form) {
            if (
                this.api &&
                typeof this.api.serializeForm === 'function'
            ) {
                return this.api.serializeForm(form);
            }

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
                            query.set(key, text);
                        }
                    }
                );

            return query;
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
                'تعذر تحديث تبويب الاستردادات خارج واجهة المالية.',
                'error'
            );
        },

        openDetails(id, trigger) {
            if (!id || !this.detailsDialog) {
                return;
            }

            const template =
                [...this.root.querySelectorAll(
                    '[data-refund-detail-template]'
                )]
                    .find(item =>
                        String(
                            item.dataset.refundDetailTemplate || ''
                        ) === String(id)
                    );

            const content =
                this.detailsDialog.querySelector(
                    '[data-refund-details-content]'
                );

            if (!template || !content) {
                this.toast(
                    'تعذر قراءة تفاصيل الاسترداد.',
                    'error'
                );
                return;
            }

            this.previousFocus =
                trigger ||
                document.activeElement;

            content.replaceChildren(
                template.content.cloneNode(true)
            );

            this.showDialog(
                this.detailsDialog
            );

            content.scrollTop = 0;
        },

        closeDetails(restoreFocus = true) {
            this.closeDialog(
                this.detailsDialog,
                restoreFocus
            );
        },

        openTransfer(button) {
            if (!this.transferDialog) {
                return;
            }

            this.previousFocus =
                button ||
                document.activeElement;

            const form =
                this.transferDialog.querySelector(
                    '[data-refund-transfer-form]'
                );

            form?.reset();

            const id =
                button.dataset.refundOpenTransfer || '';

            const amount =
                button.dataset.refundAmountText || '—';

            const customer =
                button.dataset.refundCustomer || '—';

            const wallet =
                button.dataset.refundWallet || '—';

            const account =
                button.dataset.refundAccount || '—';

            const beneficiary =
                button.dataset.refundBeneficiary || '—';

            this.setText(
                '[data-refund-transfer-id]',
                id,
                true
            );

            this.setText(
                '[data-refund-transfer-amount]',
                amount
            );

            this.setText(
                '[data-refund-transfer-customer]',
                customer
            );

            this.setText(
                '[data-refund-transfer-wallet]',
                wallet
            );

            this.setText(
                '[data-refund-transfer-account]',
                account
            );

            this.setText(
                '[data-refund-transfer-beneficiary]',
                beneficiary
            );

            this.setTransferMessage('');
            this.updateProof(null);

            this.showDialog(
                this.transferDialog
            );

            const body =
                this.transferDialog.querySelector(
                    '.refund-signature__dialog-body'
                );

            if (body) {
                body.scrollTop = 0;
            }
        },

        closeTransfer(restoreFocus = true) {
            this.closeDialog(
                this.transferDialog,
                restoreFocus
            );
        },

        showDialog(dialog) {
            if (!dialog) {
                return;
            }

            if (
                typeof dialog.showModal === 'function'
            ) {
                if (!dialog.open) {
                    dialog.showModal();
                }
            } else {
                dialog.setAttribute(
                    'open',
                    ''
                );
            }

            document.documentElement.classList.add(
                'owner-financial-dialog-open'
            );
        },

        closeDialog(dialog, restoreFocus = true) {
            if (!dialog) {
                return;
            }

            if (
                typeof dialog.close === 'function' &&
                dialog.open
            ) {
                dialog.close();
            } else {
                dialog.removeAttribute(
                    'open'
                );
            }

            const hasOpen =
                Boolean(
                    this.detailsDialog?.open ||
                    this.transferDialog?.open
                );

            document.documentElement.classList.toggle(
                'owner-financial-dialog-open',
                hasOpen
            );

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

        setText(selector, value, isInput = false) {
            const target =
                this.transferDialog
                    ?.querySelector(selector);

            if (!target) {
                return;
            }

            if (
                isInput &&
                'value' in target
            ) {
                target.value =
                    String(value ?? '');
            } else {
                target.textContent =
                    String(value ?? '—');
            }
        },

        async submitTransfer(form) {
            const id =
                Number(
                    form.querySelector(
                        '[name="RefundRequestId"]'
                    )?.value || 0
                );

            const reference =
                String(
                    form.querySelector(
                        '[name="ReferenceNumber"]'
                    )?.value || ''
                ).trim();

            const proof =
                form.querySelector(
                    '[name="proofFile"]'
                )?.files?.[0] || null;

            if (!Number.isFinite(id) || id <= 0) {
                this.setTransferMessage(
                    'طلب الاسترداد غير صحيح.'
                );
                return;
            }

            if (!reference) {
                this.setTransferMessage(
                    'أدخل رقم الحوالة.'
                );
                return;
            }

            if (reference.length > 100) {
                this.setTransferMessage(
                    'رقم الحوالة طويل جدًا.'
                );
                return;
            }

            const proofError =
                this.validateProof(proof);

            if (proofError) {
                this.setTransferMessage(
                    proofError
                );
                return;
            }

            const url =
                this.root.dataset.transferUrl;

            if (!url) {
                this.setTransferMessage(
                    'رابط تسجيل التحويل غير متاح.'
                );
                return;
            }

            const submit =
                form.querySelector(
                    '[data-refund-submit-transfer]'
                );

            const data =
                new FormData(form);

            try {
                this.setTransferMessage('');

                this.api?.setButtonBusy?.(
                    submit,
                    true,
                    'جاري التسجيل...'
                );

                const result =
                    this.api?.fetchJson
                        ? await this.api.fetchJson(
                            url,
                            {
                                method: 'POST',
                                body: data
                            }
                        )
                        : await this.fetchJsonFallback(
                            url,
                            {
                                method: 'POST',
                                body: data
                            }
                        );

                if (result?.success === false) {
                    throw new Error(
                        result.message ||
                        'تعذر تسجيل التحويل.'
                    );
                }

                this.toast(
                    result?.message ||
                    'تم تسجيل تحويل الاسترداد.',
                    'success'
                );

                this.closeTransfer(false);

                await this.reload(
                    this.currentFilterQuery()
                );
            } catch (error) {
                this.setTransferMessage(
                    error?.message ||
                    'تعذر تسجيل تحويل الاسترداد حاليًا.'
                );
            } finally {
                this.api?.setButtonBusy?.(
                    submit,
                    false
                );
            }
        },

        currentFilterQuery() {
            const form =
                this.root.querySelector(
                    '[data-refund-filter-form]'
                );

            return form
                ? this.serializeForm(form)
                : new URLSearchParams();
        },

        async fetchJsonFallback(url, options) {
            const response =
                await fetch(
                    url,
                    {
                        ...options,
                        credentials: 'same-origin',
                        cache: 'no-store',
                        headers: {
                            'X-Requested-With':
                                'XMLHttpRequest',
                            ...(options?.headers || {})
                        }
                    }
                );

            const type =
                response.headers
                    .get('content-type') || '';

            let body;

            if (type.includes('application/json')) {
                body =
                    await response.json();
            } else {
                body = {
                    success:
                        response.ok,
                    message:
                        (await response.text()) ||
                        ''
                };
            }

            if (!response.ok) {
                throw new Error(
                    body?.message ||
                    'فشل الطلب.'
                );
            }

            return body;
        },

        validateProof(file) {
            if (!file) {
                return 'ارفع صورة إثبات تحويل الاسترداد.';
            }

            if (file.size > MAX_PROOF_SIZE) {
                return 'حجم الإثبات يجب ألا يتجاوز 5 ميجابايت.';
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
                return 'الصيغة غير مدعومة. استخدم JPG أو PNG أو WEBP.';
            }

            return '';
        },

        updateProof(file) {
            const drop =
                this.transferDialog
                    ?.querySelector(
                        '[data-refund-proof-drop]'
                    );

            const title =
                this.transferDialog
                    ?.querySelector(
                        '[data-refund-proof-title]'
                    );

            const subtitle =
                this.transferDialog
                    ?.querySelector(
                        '[data-refund-proof-subtitle]'
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
                    'اضغط أو اسحب صورة السند هنا';

                subtitle.textContent =
                    'JPG / PNG / WEBP — حتى 5 MB';

                return;
            }

            const error =
                this.validateProof(file);

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
                `${this.formatSize(file.size)} — جاهز للرفع`;
        },

        setTransferMessage(message) {
            const target =
                this.transferDialog
                    ?.querySelector(
                        '[data-refund-transfer-message]'
                    );

            if (target) {
                target.textContent =
                    message || '';
            }
        },

        async copyText(text, button) {
            const value =
                String(text || '').trim();

            if (!value || value === '—') {
                return;
            }

            try {
                if (
                    navigator.clipboard &&
                    window.isSecureContext
                ) {
                    await navigator.clipboard.writeText(
                        value
                    );
                } else {
                    const area =
                        document.createElement(
                            'textarea'
                        );

                    area.value = value;
                    area.style.position = 'fixed';
                    area.style.opacity = '0';

                    document.body.appendChild(area);
                    area.select();

                    document.execCommand('copy');
                    area.remove();
                }

                const icon =
                    button.querySelector('i');

                if (icon) {
                    const old =
                        icon.className;

                    icon.className =
                        'bi bi-check-lg';

                    window.setTimeout(
                        () => {
                            icon.className = old;
                        },
                        900
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

        animateCards() {
            if (this.reducedMotion()) {
                return;
            }

            [
                ...this.root.querySelectorAll(
                    '[data-refund-card]'
                )
            ]
                .forEach(
                    (card, index) => {
                        card.animate?.(
                            [
                                {
                                    opacity: 0,
                                    transform:
                                        'translateY(7px)'
                                },
                                {
                                    opacity: 1,
                                    transform:
                                        'translateY(0)'
                                }
                            ],
                            {
                                duration: 280,
                                delay:
                                    Math.min(
                                        index * 30,
                                        180
                                    ),
                                easing:
                                    'cubic-bezier(.22,.78,.24,1)',
                                fill: 'both'
                            }
                        );
                    }
                );
        },

        toast(message, type = 'info') {
            if (
                this.api &&
                typeof this.api.showToast === 'function'
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

        reducedMotion() {
            return Boolean(
                window.matchMedia?.(
                    '(prefers-reduced-motion: reduce)'
                )?.matches
            );
        },

        formatSize(bytes) {
            if (!Number.isFinite(bytes) || bytes <= 0) {
                return '0 KB';
            }

            if (bytes < 1024 * 1024) {
                return `${Math.max(
                    1,
                    Math.round(bytes / 1024)
                )} KB`;
            }

            return `${(
                bytes /
                (1024 * 1024)
            ).toFixed(1)} MB`;
        }
    };

    window.OwnerFinancialTabs[
        MODULE_KEY
    ] = Refunds;
})();
