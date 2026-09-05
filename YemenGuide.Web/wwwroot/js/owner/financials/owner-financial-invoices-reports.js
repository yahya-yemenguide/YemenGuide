(() => {
    'use strict';

    window.OwnerFinancialTabs ??= {};

    const MODULE_KEY = 'reports';

    const Reports = {
        container: null,
        api: null,
        dialog: null,
        previousFocus: null,
        handlers: {},

        init(container, api) {
            this.destroy();

            this.container =
                container?.querySelector(
                    '[data-owner-financial-partial="reports"]'
                ) || null;

            this.api = api || null;

            if (!this.container) {
                return;
            }

            this.dialog =
                this.container.querySelector(
                    '[data-ofir-document-dialog]'
                );

            this.bindEvents();
            this.syncReportTypeVisuals();
            this.syncQuickRangeVisuals();
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

            if (this.dialog && this.handlers.cancel) {
                this.dialog.removeEventListener(
                    'cancel',
                    this.handlers.cancel
                );
            }

            this.closeDocument(false);

            this.container = null;
            this.api = null;
            this.dialog = null;
            this.previousFocus = null;
            this.handlers = {};
        },

        bindEvents() {
            this.handlers.click = event => {
                const reportChip =
                    event.target.closest(
                        '[data-ofir-report-chip]'
                    );

                if (
                    reportChip &&
                    this.container.contains(reportChip)
                ) {
                    event.preventDefault();
                    this.changeReport(reportChip);
                    return;
                }

                const rangeButton =
                    event.target.closest(
                        '[data-ofir-range]'
                    );

                if (
                    rangeButton &&
                    this.container.contains(rangeButton)
                ) {
                    event.preventDefault();
                    this.applyQuickRange(
                        rangeButton.dataset.ofirRange
                    );
                    return;
                }

                const clearButton =
                    event.target.closest(
                        '[data-ofir-clear-filters]'
                    );

                if (
                    clearButton &&
                    this.container.contains(clearButton)
                ) {
                    event.preventDefault();
                    this.clearFilters();
                    return;
                }

                const pageButton =
                    event.target.closest(
                        '[data-ofir-page]'
                    );

                if (
                    pageButton &&
                    this.container.contains(pageButton)
                ) {
                    event.preventDefault();

                    if (pageButton.disabled) {
                        return;
                    }

                    const pageNumber =
                        Number(pageButton.dataset.ofirPage);

                    if (
                        Number.isFinite(pageNumber) &&
                        pageNumber > 0
                    ) {
                        this.goToPage(pageNumber);
                    }

                    return;
                }

                const openDocument =
                    event.target.closest(
                        '[data-ofir-open-document]'
                    );

                if (
                    openDocument &&
                    this.container.contains(openDocument)
                ) {
                    event.preventDefault();
                    this.openDocument(openDocument);
                    return;
                }

                const closeDocument =
                    event.target.closest(
                        '[data-ofir-close-dialog]'
                    );

                if (
                    closeDocument &&
                    this.container.contains(closeDocument)
                ) {
                    event.preventDefault();
                    this.closeDocument();
                }
            };

            this.handlers.submit = event => {
                const form =
                    event.target.closest(
                        '[data-ofir-filter-form]'
                    );

                if (
                    !form ||
                    !this.container.contains(form)
                ) {
                    return;
                }

                const submitter = event.submitter;
                const exportFormat =
                    submitter?.getAttribute('name') ===
                    'ExportFormat';

                const submitterAction =
                    submitter?.getAttribute(
                        'formaction'
                    );

                const normalAction =
                    form.getAttribute('action') || '';

                if (
                    exportFormat ||
                    (
                        submitterAction &&
                        submitterAction !== normalAction
                    )
                ) {
                    if (!this.validateDates(form, true)) {
                        event.preventDefault();
                    }

                    return;
                }

                event.preventDefault();
                this.applyFilters(form);
            };

            this.handlers.cancel = event => {
                event.preventDefault();
                this.closeDocument();
            };

            this.container.addEventListener(
                'click',
                this.handlers.click
            );

            this.container.addEventListener(
                'submit',
                this.handlers.submit
            );

            this.dialog?.addEventListener(
                'cancel',
                this.handlers.cancel
            );

            this.dialog?.addEventListener(
                'click',
                event => {
                    if (event.target === this.dialog) {
                        this.closeDocument();
                    }
                }
            );
        },

        changeReport(button) {
            const form =
                this.container.querySelector(
                    '[data-ofir-filter-form]'
                );

            const reportType =
                String(
                    button.dataset.reportType || ''
                ).trim();

            if (
                !form ||
                !reportType ||
                button.classList.contains('is-active')
            ) {
                return;
            }

            const field =
                form.querySelector(
                    '[name="ReportType"]'
                );

            if (field) {
                field.value = reportType;
            }

            const query =
                this.serializeForm(
                    form,
                    { PageNumber: 1 }
                );

            query.set('ReportType', reportType);
            query.set('PageNumber', '1');

            this.reload(query);
        },

        applyFilters(form) {
            if (!this.validateDates(form)) {
                return;
            }

            const query =
                this.serializeForm(
                    form,
                    { PageNumber: 1 }
                );

            this.reload(query);
        },

        clearFilters() {
            const form =
                this.container.querySelector(
                    '[data-ofir-filter-form]'
                );

            if (!form) {
                return;
            }

            const reportType =
                form.querySelector(
                    '[name="ReportType"]'
                )?.value || '';

            const placeId =
                form.querySelector(
                    '[name="PlaceId"]'
                )?.value || '';

            const query =
                new URLSearchParams();

            if (reportType) {
                query.set(
                    'ReportType',
                    reportType
                );
            }

            if (placeId) {
                query.set(
                    'PlaceId',
                    placeId
                );
            }

            query.set('PageNumber', '1');
            query.set('PageSize', '25');

            this.reload(query);
        },

        applyQuickRange(range) {
            const form =
                this.container.querySelector(
                    '[data-ofir-filter-form]'
                );

            if (!form) {
                return;
            }

            const fromInput =
                form.querySelector(
                    '[name="FromDate"]'
                );

            const toInput =
                form.querySelector(
                    '[name="ToDate"]'
                );

            if (!fromInput || !toInput) {
                return;
            }

            if (range === 'all') {
                fromInput.value = '';
                toInput.value = '';
            }
            else {
                const today = this.todayLocal();
                let from = new Date(today);
                let to = new Date(today);

                if (range === '7days') {
                    from.setDate(
                        from.getDate() - 6
                    );
                }
                else if (range === 'month') {
                    from = new Date(
                        today.getFullYear(),
                        today.getMonth(),
                        1
                    );
                }

                fromInput.value =
                    this.formatDateInput(from);

                toInput.value =
                    this.formatDateInput(to);
            }

            this.syncQuickRangeVisuals(range);
            this.applyFilters(form);
        },

        syncQuickRangeVisuals(forcedRange = '') {
            if (!this.container) {
                return;
            }

            const buttons =
                Array.from(
                    this.container.querySelectorAll(
                        '[data-ofir-range]'
                    )
                );

            if (forcedRange) {
                buttons.forEach(button => {
                    button.classList.toggle(
                        'is-active',
                        button.dataset.ofirRange ===
                            forcedRange
                    );
                });

                return;
            }

            const form =
                this.container.querySelector(
                    '[data-ofir-filter-form]'
                );

            if (!form) {
                return;
            }

            const from =
                form.querySelector(
                    '[name="FromDate"]'
                )?.value || '';

            const to =
                form.querySelector(
                    '[name="ToDate"]'
                )?.value || '';

            const today = this.todayLocal();
            const todayText =
                this.formatDateInput(today);

            const monthStart =
                this.formatDateInput(
                    new Date(
                        today.getFullYear(),
                        today.getMonth(),
                        1
                    )
                );

            const sevenStartDate =
                new Date(today);

            sevenStartDate.setDate(
                sevenStartDate.getDate() - 6
            );

            const sevenStart =
                this.formatDateInput(
                    sevenStartDate
                );

            let active = '';

            if (!from && !to) {
                active = 'all';
            }
            else if (
                from === todayText &&
                to === todayText
            ) {
                active = 'today';
            }
            else if (
                from === sevenStart &&
                to === todayText
            ) {
                active = '7days';
            }
            else if (
                from === monthStart &&
                to === todayText
            ) {
                active = 'month';
            }

            buttons.forEach(button => {
                button.classList.toggle(
                    'is-active',
                    Boolean(active) &&
                    button.dataset.ofirRange === active
                );
            });
        },

        goToPage(pageNumber) {
            const form =
                this.container.querySelector(
                    '[data-ofir-filter-form]'
                );

            const query = form
                ? this.serializeForm(
                    form,
                    { PageNumber: pageNumber }
                )
                : new URLSearchParams();

            query.set(
                'PageNumber',
                String(pageNumber)
            );

            this.reload(query);
        },

        validateDates(form, quiet = false) {
            const from =
                form.querySelector(
                    '[name="FromDate"]'
                )?.value || '';

            const to =
                form.querySelector(
                    '[name="ToDate"]'
                )?.value || '';

            const validation =
                this.container.querySelector(
                    '[data-ofir-validation-for="dateRange"]'
                );

            const alert =
                this.container.querySelector(
                    '[data-ofir-filter-alert]'
                );

            if (
                from &&
                to &&
                from > to
            ) {
                if (validation) {
                    validation.textContent =
                        'تاريخ البداية يجب أن يسبق تاريخ النهاية.';
                }

                if (alert) {
                    alert.hidden = false;
                    alert.textContent =
                        'راجع الفترة الزمنية قبل إنشاء التقرير.';
                }

                if (!quiet) {
                    this.api?.showToast?.(
                        'راجع الفترة الزمنية المحددة.',
                        'warning'
                    );
                }

                return false;
            }

            if (validation) {
                validation.textContent = '';
            }

            if (alert) {
                alert.hidden = true;
                alert.textContent = '';
            }

            return true;
        },

        openDocument(trigger) {
            if (!this.dialog) {
                return;
            }

            const url =
                this.safeUrl(
                    trigger.dataset.documentUrl
                );

            if (!url) {
                this.api?.showToast?.(
                    'رابط المستند غير متاح.',
                    'error'
                );

                return;
            }

            this.previousFocus = trigger;

            const frame =
                this.dialog.querySelector(
                    '[data-ofir-document-frame]'
                );

            const loader =
                this.dialog.querySelector(
                    '[data-ofir-frame-loading]'
                );

            const newTab =
                this.dialog.querySelector(
                    '[data-ofir-open-document-new-tab]'
                );

            const title =
                this.dialog.querySelector(
                    '#ownerFinancialDocumentPreviewTitle'
                );

            if (title) {
                title.textContent =
                    trigger.dataset.documentTitle ||
                    'المستند المالي';
            }

            if (newTab) {
                newTab.href = url;
            }

            if (loader) {
                loader.hidden = false;
            }

            if (frame) {
                frame.onload = () => {
                    if (loader) {
                        loader.hidden = true;
                    }
                };

                frame.src = url;
            }

            if (
                typeof this.dialog.showModal ===
                'function'
            ) {
                if (!this.dialog.open) {
                    this.dialog.showModal();
                }
            }
            else {
                this.dialog.setAttribute(
                    'open',
                    ''
                );
            }
        },

        closeDocument(restoreFocus = true) {
            if (!this.dialog) {
                return;
            }

            const frame =
                this.dialog.querySelector(
                    '[data-ofir-document-frame]'
                );

            if (frame) {
                frame.onload = null;
                frame.src = 'about:blank';
            }

            if (
                typeof this.dialog.close ===
                    'function' &&
                this.dialog.open
            ) {
                this.dialog.close();
            }
            else {
                this.dialog.removeAttribute(
                    'open'
                );
            }

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

        syncReportTypeVisuals() {
            const field =
                this.container.querySelector(
                    '[data-ofir-report-type]'
                );

            if (!field) {
                return;
            }

            this.container.dataset.activeReportType =
                field.value ||
                'Comprehensive';
        },

        serializeForm(form, overrideValues = {}) {
            if (
                this.api &&
                typeof this.api.serializeForm ===
                    'function'
            ) {
                return this.api.serializeForm(
                    form,
                    overrideValues
                );
            }

            const query =
                new URLSearchParams(
                    new FormData(form)
                );

            Object.entries(overrideValues)
                .forEach(([key, value]) => {
                    query.set(
                        key,
                        String(value)
                    );
                });

            return query;
        },

        reload(query) {
            if (
                this.api &&
                typeof this.api.reload ===
                    'function'
            ) {
                this.api.reload({
                    query,
                    history: 'replace',
                    keepScroll: true
                });

                return;
            }

            const url =
                new URL(
                    window.location.href
                );

            url.search =
                query.toString();

            window.location.href =
                url.toString();
        },

        todayLocal() {
            const now = new Date();

            return new Date(
                now.getFullYear(),
                now.getMonth(),
                now.getDate()
            );
        },

        formatDateInput(date) {
            const year =
                date.getFullYear();

            const month =
                String(date.getMonth() + 1)
                    .padStart(2, '0');

            const day =
                String(date.getDate())
                    .padStart(2, '0');

            return `${year}-${month}-${day}`;
        },

        safeUrl(value) {
            const raw =
                String(value || '')
                    .trim();

            if (!raw) {
                return '';
            }

            try {
                const url =
                    new URL(
                        raw,
                        window.location.origin
                    );

                if (
                    url.origin !==
                    window.location.origin
                ) {
                    return '';
                }

                return url.toString();
            }
            catch {
                return '';
            }
        }
    };

    window.OwnerFinancialTabs[MODULE_KEY] =
        Reports;
})();
