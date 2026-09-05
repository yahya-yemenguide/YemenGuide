(() => {
    'use strict';

    const App = {
        root: null,
        content: null,
        tabsHost: null,
        tabs: [],
        activeTab: 'overview',
        activeButton: null,
        controller: null,
        selectedTopUpFile: null,
        queries: new Map(),

        filters: {
            guarantee: {
                search: '',
                status: ''
            },

            wallets: {
                search: '',
                status: ''
            },

            documents: {
                search: '',
                type: ''
            }
        },

        endpoints: {},

        drag: {
            active: false,
            x: 0,
            scroll: 0
        },

        tabOrder: [
            'overview',
            'bookings',
            'guarantee',
            'topup',
            'transactions',
            'refunds',
            'wallets',
            'reports'
        ],

        init() {
            this.root =
                document.getElementById(
                    'ownerFinancePage'
                );

            if (!this.root) {
                return;
            }

            this.content =
                document.getElementById(
                    'financeTabContent'
                );

            this.tabsHost =
                document.getElementById(
                    'financeTabs'
                );

            this.tabs = [
                ...this.root.querySelectorAll(
                    '[data-finance-tab]'
                )
            ];

            if (
                !this.content ||
                !this.tabs.length
            ) {
                return;
            }

            this.readEndpoints();
            this.injectRuntimeStyles();
            this.bindEvents();
            this.prepareTabs();
            this.updateHeaderFromPlace();

            const url =
                new URL(
                    window.location.href
                );

            const initial =
                this.normalizeTab(
                    url.searchParams.get(
                        'financeTab'
                    )
                ) ||
                this.normalizeTab(
                    this.root.dataset.defaultTab
                ) ||
                'overview';

            this.openTab(
                initial,
                {
                    updateHistory: false,
                    replaceHistory: true
                }
            );
        },

        readEndpoints() {
            const data =
                this.root.dataset;

            this.endpoints = {
                overview:
                    data.overviewUrl ||
                    '',

                bookings:
                    data.bookingsUrl ||
                    '',

                guarantee:
                    data.guaranteeUrl ||
                    '',

                topup:
                    data.topupUrl ||
                    '',

                transactions:
                    data.transactionsUrl ||
                    '',

                refunds:
                    data.refundsUrl ||
                    '',

                wallets:
                    data.walletsUrl ||
                    '',

                reports:
                    data.reportsUrl ||
                    '',

                submitTopUp:
                    data.submitTopupUrl ||
                    '',

                cancelTopUp:
                    data.cancelTopupUrl ||
                    '',

                saveWallet:
                    data.saveWalletUrl ||
                    '',

                changeWalletStatus:
                    data.changeWalletStatusUrl ||
                    ''
            };
        },

        bindEvents() {
            document.addEventListener(
                'click',
                event =>
                    this.onClick(
                        event
                    )
            );

            document.addEventListener(
                'submit',
                event =>
                    this.onSubmit(
                        event
                    )
            );

            document.addEventListener(
                'input',
                event =>
                    this.onInput(
                        event
                    )
            );

            document.addEventListener(
                'change',
                event =>
                    this.onChange(
                        event
                    )
            );

            document.addEventListener(
                'keydown',
                event =>
                    this.onKeyDown(
                        event
                    )
            );

            document.addEventListener(
                'dragover',
                event =>
                    this.onDragOver(
                        event
                    )
            );

            document.addEventListener(
                'dragleave',
                event =>
                    this.onDragLeave(
                        event
                    )
            );

            document.addEventListener(
                'drop',
                event =>
                    this.onDrop(
                        event
                    )
            );

            window.addEventListener(
                'popstate',
                () => {
                    const url =
                        new URL(
                            window.location.href
                        );

                    const tab =
                        this.normalizeTab(
                            url.searchParams.get(
                                'financeTab'
                            )
                        ) ||
                        'overview';

                    this.openTab(
                        tab,
                        {
                            updateHistory: false
                        }
                    );
                }
            );
        },

        prepareTabs() {
            this.tabs.forEach(
                button => {
                    button.tabIndex =
                        button.classList.contains(
                            'active'
                        )
                            ? 0
                            : -1;

                    button.setAttribute(
                        'role',
                        'tab'
                    );
                }
            );

            if (!this.tabsHost) {
                return;
            }

          

            this.tabsHost.addEventListener(
                'wheel',
                event => {
                    if (
                        this.tabsHost.scrollWidth <=
                        this.tabsHost.clientWidth + 2
                    ) {
                        return;
                    }

                    if (
                        Math.abs(
                            event.deltaY
                        ) <=
                        Math.abs(
                            event.deltaX
                        )
                    ) {
                        return;
                    }

                    event.preventDefault();

                    this.tabsHost.scrollBy(
                        {
                            left:
                                event.deltaY,

                            behavior:
                                'smooth'
                        }
                    );
                },
                {
                    passive: false
                }
            );

            this.tabsHost.addEventListener(
                'pointerdown',
                event => {
                    if (
                        event.pointerType ===
                        'touch'
                    ) {
                        return;
                    }

                    if (
                        event.target.closest(
                            'button'
                        )
                    ) {
                        return;
                    }

                    this.drag.active =
                        true;

                    this.drag.x =
                        event.clientX;

                    this.drag.scroll =
                        this.tabsHost.scrollLeft;

                    this.tabsHost.classList.add(
                        'is-dragging'
                    );

                    this.tabsHost.setPointerCapture?.(
                        event.pointerId
                    );
                }
            );

            this.tabsHost.addEventListener(
                'pointermove',
                event => {
                    if (
                        !this.drag.active
                    ) {
                        return;
                    }

                    this.tabsHost.scrollLeft =
                        this.drag.scroll -
                        (
                            event.clientX -
                            this.drag.x
                        );
                }
            );

            const stopDragging =
                () => {
                    this.drag.active =
                        false;

                    this.tabsHost.classList.remove(
                        'is-dragging'
                    );
                };

            this.tabsHost.addEventListener(
                'pointerup',
                stopDragging
            );

            this.tabsHost.addEventListener(
                'pointercancel',
                stopDragging
            );

            this.tabsHost.addEventListener(
                'pointerleave',
                stopDragging
            );

        
        },

        onClick(event) {
            const target =
                event.target;

            const tabButton =
                target.closest(
                    '[data-finance-tab]'
                );

            if (
                tabButton &&
                this.root.contains(
                    tabButton
                )
            ) {
                event.preventDefault();

                this.openTab(
                    tabButton.dataset.financeTab,
                    {
                        button:
                            tabButton
                    }
                );

                return;
            }

            const openTabButton =
                target.closest(
                    '[data-open-finance-tab]'
                );

            if (
                openTabButton &&
                this.root.contains(
                    openTabButton
                )
            ) {
                event.preventDefault();

                this.openTabFromElement(
                    openTabButton
                );

                return;
            }

           

            if (
                target.closest(
                    '#refreshFinancePageBtn'
                )
            ) {
                event.preventDefault();

                this.refresh();

                return;
            }

            if (
                target.closest(
                    '#financePrintCurrentTabBtn, ' +
                    '[data-print-financial-summary]'
                )
            ) {
                event.preventDefault();

                this.printCurrentTab();

                return;
            }

            if (
                target.closest(
                    '#financeDrawerCloseBtn, ' +
                    '#financeDrawerBackdrop'
                )
            ) {
                event.preventDefault();

                this.closeDrawer();

                return;
            }

            const retryButton =
                target.closest(
                    '[data-finance-retry]'
                );

            if (retryButton) {
                event.preventDefault();

                this.load(
                    retryButton.dataset.financeRetry,
                    this.activeTab
                );

                return;
            }

            const clearInputButton =
                target.closest(
                    '[data-clear-input]'
                );

            if (clearInputButton) {
                event.preventDefault();

                const input =
                    document.querySelector(
                        clearInputButton.dataset.clearInput
                    );

                if (input) {
                    input.value =
                        '';

                    input.dispatchEvent(
                        new Event(
                            'input',
                            {
                                bubbles: true
                            }
                        )
                    );

                    input.focus();
                }

                return;
            }

            const pageButton =
                target.closest(
                    '[data-finance-page]'
                );

            if (
                pageButton &&
                !pageButton.disabled
            ) {
                event.preventDefault();

                this.goToPage(
                    pageButton
                );

                return;
            }

            const bookingMenuButton =
                target.closest(
                    '[data-booking-finance-menu]'
                );

            if (bookingMenuButton) {
                event.preventDefault();
                event.stopPropagation();

                this.toggleMenu(
                    bookingMenuButton,
                    '.booking-finance-more-menu'
                );

                return;
            }

            const reportMenuButton =
                target.closest(
                    '[data-report-document-menu]'
                );

            if (reportMenuButton) {
                event.preventDefault();
                event.stopPropagation();

                this.toggleMenu(
                    reportMenuButton,
                    '.report-document-more'
                );

                return;
            }

            if (
                !target.closest(
                    '.booking-finance-more-menu, ' +
                    '.report-document-more'
                )
            ) {
                this.closeMenus();
            }

            const copyButton =
                target.closest(
                    '[data-copy-value], ' +
                    '[data-copy-wallet-number]'
                );

            if (copyButton) {
                event.preventDefault();

                this.copy(
                    copyButton.dataset.copyValue ||
                    copyButton.dataset.copyWalletNumber ||
                    '',
                    copyButton
                );

                return;
            }

            const previewButton =
                target.closest(
                    '[data-preview-finance-document]'
                );

            if (previewButton) {
                event.preventDefault();

                this.previewDocument(
                    previewButton.dataset.documentUrl,
                    previewButton.dataset.documentTitle ||
                    'معاينة المستند'
                );

                return;
            }

            if (
                target.closest(
                    '[data-overview-period-filter]'
                )
            ) {
                event.preventDefault();

                this.periodDialog();

                return;
            }

            const bookingDetailsButton =
                target.closest(
                    '[data-open-booking-finance-details]'
                );

            if (bookingDetailsButton) {
                event.preventDefault();

                this.bookingDetails(
                    bookingDetailsButton
                );

                return;
            }

            const guaranteeDetailsButton =
                target.closest(
                    '[data-open-guarantee-account-details]'
                );

            if (guaranteeDetailsButton) {
                event.preventDefault();

                this.guaranteeDetails(
                    guaranteeDetailsButton
                );

                return;
            }

            const topUpDetailsButton =
                target.closest(
                    '[data-open-topup-details]'
                );

            if (topUpDetailsButton) {
                event.preventDefault();

                this.topUpDetails(
                    topUpDetailsButton
                );

                return;
            }

            const transactionDetailsButton =
                target.closest(
                    '[data-open-transaction-details]'
                );

            if (transactionDetailsButton) {
                event.preventDefault();

                this.transactionDetails(
                    transactionDetailsButton
                );

                return;
            }

            const refundDetailsButton =
                target.closest(
                    '[data-open-refund-details]'
                );

            if (refundDetailsButton) {
                event.preventDefault();

                this.refundDetails(
                    refundDetailsButton
                );

                return;
            }

            const reportDetailsButton =
                target.closest(
                    '[data-open-booking-report-details]'
                );

            if (reportDetailsButton) {
                event.preventDefault();

                this.reportDetails(
                    reportDetailsButton
                );

                return;
            }

            const printInvoiceButton =
                target.closest(
                    '[data-print-booking-invoice]'
                );

            if (printInvoiceButton) {
                event.preventDefault();

                this.printBooking(
                    printInvoiceButton,
                    'invoice'
                );

                return;
            }

            const printDepositButton =
                target.closest(
                    '[data-print-deposit-receipt]'
                );

            if (printDepositButton) {
                event.preventDefault();

                this.printBooking(
                    printDepositButton,
                    'deposit'
                );

                return;
            }

            const printCollectionButton =
                target.closest(
                    '[data-print-collection-receipt]'
                );

            if (printCollectionButton) {
                event.preventDefault();

                this.printBooking(
                    printCollectionButton,
                    'collection'
                );

                return;
            }

            if (
                target.closest(
                    '[data-export-financial-report]'
                )
            ) {
                event.preventDefault();

                this.exportReport();

                return;
            }

            if (
                target.closest(
                    '[data-reset-booking-finance-filters], ' +
                    '[data-reset-transactions-filters], ' +
                    '[data-reset-refunds-filters], ' +
                    '[data-reset-reports-filters]'
                )
            ) {
                event.preventDefault();

                this.resetServerFilters(
                    target.closest(
                        'button'
                    )
                );

                return;
            }

            const guaranteeStatusButton =
                target.closest(
                    '[data-guarantee-status-filter]'
                );

            if (guaranteeStatusButton) {
                event.preventDefault();

                this.filters.guarantee.status =
                    guaranteeStatusButton.dataset.guaranteeStatusFilter ||
                    '';

                guaranteeStatusButton
                    .closest(
                        '[data-finance-partial]'
                    )
                    ?.querySelectorAll(
                        '[data-guarantee-status-filter]'
                    )
                    .forEach(
                        button => {
                            button.classList.toggle(
                                'active',
                                button ===
                                guaranteeStatusButton
                            );
                        }
                    );

                this.filterGuarantee();

                return;
            }

            if (
                target.closest(
                    '[data-reset-guarantee-filter]'
                )
            ) {
                event.preventDefault();

                this.resetGuarantee();

                return;
            }

            const transactionStatusButton =
                target.closest(
                    '[data-transaction-status-filter]'
                );

            if (transactionStatusButton) {
                event.preventDefault();

                this.transactionStatus(
                    transactionStatusButton
                );

                return;
            }

            const walletStatusButton =
                target.closest(
                    '[data-owner-wallet-status-filter]'
                );

            if (walletStatusButton) {
                event.preventDefault();

                this.filters.wallets.status =
                    walletStatusButton.dataset.ownerWalletStatusFilter ||
                    '';

                walletStatusButton
                    .closest(
                        '[data-finance-partial]'
                    )
                    ?.querySelectorAll(
                        '[data-owner-wallet-status-filter]'
                    )
                    .forEach(
                        button => {
                            button.classList.toggle(
                                'active',
                                button ===
                                walletStatusButton
                            );
                        }
                    );

                this.filterWallets();

                return;
            }

            if (
                target.closest(
                    '[data-reset-owner-wallet-filter]'
                )
            ) {
                event.preventDefault();

                this.resetWallets();

                return;
            }

            const documentTypeButton =
                target.closest(
                    '[data-document-type-filter]'
                );

            if (documentTypeButton) {
                event.preventDefault();

                this.filters.documents.type =
                    documentTypeButton.dataset.documentTypeFilter ||
                    '';

                documentTypeButton
                    .closest(
                        '[data-finance-partial]'
                    )
                    ?.querySelectorAll(
                        '[data-document-type-filter]'
                    )
                    .forEach(
                        button => {
                            button.classList.toggle(
                                'active',
                                button ===
                                documentTypeButton
                            );
                        }
                    );

                this.filterDocuments();

                return;
            }

            if (
                target.closest(
                    '[data-reset-financial-document-filter]'
                )
            ) {
                event.preventDefault();

                this.resetDocuments();

                return;
            }

            const platformWalletCard =
                target.closest(
                    '[data-platform-wallet-card]'
                );

            if (
                platformWalletCard &&
                !target.closest(
                    '[data-copy-value]'
                )
            ) {
                event.preventDefault();

                this.selectPlatformWallet(
                    platformWalletCard
                );

                return;
            }

            if (
                target.closest(
                    '#topUpRemoveReceiptBtn'
                )
            ) {
                event.preventDefault();

                this.clearTopUpFile();

                return;
            }

            if (
                target.closest(
                    '#resetTopUpFormBtn'
                )
            ) {
                event.preventDefault();

                this.resetTopUp();

                return;
            }

            const cancelTopUpButton =
                target.closest(
                    '[data-cancel-topup]'
                );

            if (cancelTopUpButton) {
                event.preventDefault();

                this.cancelTopUp(
                    cancelTopUpButton
                );

                return;
            }

            const openWalletButton =
                target.closest(
                    '[data-open-owner-wallet-modal]'
                );

            if (
                openWalletButton &&
                !openWalletButton.disabled
            ) {
                event.preventDefault();

                this.openWalletModal(
                    openWalletButton
                );

                return;
            }

            const walletStatusChangeButton =
                target.closest(
                    '[data-change-owner-wallet-status]'
                );

            if (walletStatusChangeButton) {
                event.preventDefault();

                this.changeWalletStatus(
                    walletStatusChangeButton
                );

                return;
            }

            const closeFallbackModalButton =
                target.closest(
                    '[data-bs-dismiss="modal"]'
                );

            if (
                closeFallbackModalButton &&
                !window.bootstrap?.Modal
            ) {
                event.preventDefault();

                this.hideModal(
                    closeFallbackModalButton.closest(
                        '.modal'
                    )
                );
            }
        },

        onSubmit(event) {
            const form =
                event.target;

            if (
                form.matches(
                    '[data-finance-filter-form]'
                )
            ) {
                event.preventDefault();

                this.submitFilters(
                    form
                );

                return;
            }

            if (
                form.matches(
                    '[data-topup-request-form]'
                )
            ) {
                event.preventDefault();

                this.submitTopUp(
                    form
                );

                return;
            }

            if (
                form.matches(
                    '[data-owner-wallet-form]'
                )
            ) {
                event.preventDefault();

                this.saveWallet(
                    form
                );
            }
        },

        onInput(event) {
            const target =
                event.target;

            if (
                target.matches(
                    '[data-guarantee-search]'
                )
            ) {
                this.filters.guarantee.search =
                    target.value;

                this.filterGuarantee();

                return;
            }

            if (
                target.matches(
                    '[data-owner-wallet-search]'
                )
            ) {
                this.filters.wallets.search =
                    target.value;

                this.filterWallets();

                return;
            }

            if (
                target.matches(
                    '[data-financial-document-search]'
                )
            ) {
                this.filters.documents.search =
                    target.value;

                this.filterDocuments();

                return;
            }

            if (
                target.matches(
                    '[data-topup-amount]'
                )
            ) {
                this.updateTopUpCalculation();

                return;
            }

            if (
                target.id ===
                'topUpNotes'
            ) {
                const counter =
                    document.getElementById(
                        'topUpNotesCounter'
                    );

                if (counter) {
                    counter.textContent =
                        String(
                            target.value.length
                        );
                }
            }
        },

        onChange(event) {
            const target =
                event.target;

            if (
                target.id ===
                'financePlaceSelector'
            ) {
                this.updateHeaderFromPlace();

                this.queries.delete(
                    this.activeTab
                );

                this.openTab(
                    this.activeTab,
                    {
                        updateHistory: true
                    }
                );

                return;
            }

            if (
                target.matches(
                    '[data-platform-wallet-radio]'
                )
            ) {
                const card =
                    target.closest(
                        '[data-platform-wallet-card]'
                    );

                if (card) {
                    this.selectPlatformWallet(
                        card
                    );
                }

                return;
            }

            if (
                target.matches(
                    '[data-topup-place-select]'
                )
            ) {
                this.updateTopUpAccount();

                return;
            }

            if (
                target.matches(
                    '[data-topup-receipt-input]'
                )
            ) {
                this.setTopUpFile(
                    target.files?.[0] ||
                    null
                );
            }
        },

        onKeyDown(event) {
            if (
                event.key ===
                'Escape'
            ) {
                this.closeMenus();
                this.closeDrawer();
                this.closeRuntimeDialog();

                return;
            }

            const currentTab =
                event.target.closest(
                    '[data-finance-tab]'
                );

            if (
                !currentTab ||
                !this.tabsHost?.contains(
                    currentTab
                )
            ) {
                return;
            }

            const currentIndex =
                this.tabs.indexOf(
                    currentTab
                );

            let nextIndex =
                null;

            if (
                event.key ===
                'ArrowLeft'
            ) {
                nextIndex =
                    Math.min(
                        currentIndex + 1,
                        this.tabs.length - 1
                    );
            }

            if (
                event.key ===
                'ArrowRight'
            ) {
                nextIndex =
                    Math.max(
                        currentIndex - 1,
                        0
                    );
            }

            if (
                event.key ===
                'Home'
            ) {
                nextIndex =
                    0;
            }

            if (
                event.key ===
                'End'
            ) {
                nextIndex =
                    this.tabs.length - 1;
            }

            if (
                event.key ===
                'Enter' ||
                event.key ===
                ' '
            ) {
                event.preventDefault();

                this.openTab(
                    currentTab.dataset.financeTab,
                    {
                        button:
                            currentTab
                    }
                );

                return;
            }

            if (
                nextIndex ===
                null ||
                nextIndex ===
                currentIndex
            ) {
                return;
            }

            event.preventDefault();

            this.tabs[nextIndex].focus(
                {
                    preventScroll: true
                }
            );

            this.openTab(
                this.tabs[nextIndex].dataset.financeTab,
                {
                    button:
                        this.tabs[nextIndex]
                }
            );
        },

        onDragOver(event) {
            const dropZone =
                event.target.closest(
                    '[data-topup-receipt-dropzone]'
                );

            if (!dropZone) {
                return;
            }

            event.preventDefault();

            dropZone.classList.add(
                'dragover'
            );
        },

        onDragLeave(event) {
            event.target
                .closest(
                    '[data-topup-receipt-dropzone]'
                )
                ?.classList.remove(
                    'dragover'
                );
        },

        onDrop(event) {
            const dropZone =
                event.target.closest(
                    '[data-topup-receipt-dropzone]'
                );

            if (!dropZone) {
                return;
            }

            event.preventDefault();

            dropZone.classList.remove(
                'dragover'
            );

            const file =
                event.dataTransfer
                    ?.files
                ?.[0];

            if (!file) {
                return;
            }

            const input =
                dropZone.querySelector(
                    '[data-topup-receipt-input]'
                );

            if (
                input &&
                typeof DataTransfer !==
                'undefined'
            ) {
                const transfer =
                    new DataTransfer();

                transfer.items.add(
                    file
                );

                input.files =
                    transfer.files;
            }

            this.setTopUpFile(
                file
            );
        },

        normalizeTab(value) {
            const normalized =
                String(value || '')
                    .trim()
                    .toLowerCase()
                    .replace(
                        /^#/,
                        ''
                    )
                    .replace(
                        /^finance-tab-/,
                        ''
                    )
                    .replace(
                        /-tab$/,
                        ''
                    );

            const aliases = {
                overview:
                    'overview',

                dashboard:
                    'overview',

                summary:
                    'overview',

                bookings:
                    'bookings',

                bookingaccounts:
                    'bookings',

                'booking-accounts':
                    'bookings',

                guarantee:
                    'guarantee',

                commissionguarantee:
                    'guarantee',

                'commission-guarantee':
                    'guarantee',

                topup:
                    'topup',

                'top-up':
                    'topup',

                topupbalance:
                    'topup',

                'top-up-balance':
                    'topup',

                transactions:
                    'transactions',

                statement:
                    'transactions',

                ledger:
                    'transactions',

                refunds:
                    'refunds',

                cancellations:
                    'refunds',

                wallets:
                    'wallets',

                ownerwallets:
                    'wallets',

                'owner-wallets':
                    'wallets',

                reports:
                    'reports',

                invoices:
                    'reports',

                invoicesreports:
                    'reports',

                'invoices-reports':
                    'reports'
            };

            return (
                aliases[normalized] ||
                ''
            );
        },

        openTabFromElement(element) {
            const tab =
                this.normalizeTab(
                    element.dataset.openFinanceTab
                );

            if (!tab) {
                return;
            }

            const query = {};

            const placeId =
                String(
                    element.dataset.placeId ||
                    ''
                ).trim();

            /*
             * عندما يضغط المستخدم على:
             * شحن الرصيد أو كشف الحساب
             * من بطاقة منشأة داخل النظرة العامة،
             * نختار المنشأة في القائمة العليا أولًا.
             */
            if (placeId) {
                const selector =
                    document.getElementById(
                        'financePlaceSelector'
                    );

                const optionExists =
                    selector &&
                    Array.from(
                        selector.options
                    ).some(
                        option =>
                            option.value ===
                            placeId
                    );

                if (optionExists) {
                    selector.value =
                        placeId;

                    this.updateHeaderFromPlace();
                }
                else {
                    const placeParameter =
                        [
                            'overview',
                            'topup'
                        ].includes(
                            tab
                        )
                            ? 'placeId'
                            : 'PlaceId';

                    query[placeParameter] =
                        placeId;
                }
            }

            const mapping = {
                bookingId:
                    'BookingId',

                transactionType:
                    'TransactionType',

                status:
                    'Status',

                search:
                    'Search'
            };

            Object.entries(
                mapping
            ).forEach(
                ([datasetKey, queryKey]) => {
                    const value =
                        String(
                            element.dataset[
                            datasetKey
                            ] ||
                            ''
                        ).trim();

                    if (value) {
                        query[queryKey] =
                            value;
                    }
                }
            );

            if (
                Object.keys(
                    query
                ).length
            ) {
                query.PageNumber =
                    1;

                this.queries.set(
                    tab,
                    query
                );
            }

            this.openTab(
                tab
            );
        },

        async openTab(
            tab,
            options = {}
        ) {
            const key =
                this.normalizeTab(
                    tab
                );

            const button =
                options.button ||
                this.tabs.find(
                    item =>
                        this.normalizeTab(
                            item.dataset.financeTab
                        ) ===
                        key
                );

            if (
                !key ||
                !button
            ) {
                return;
            }

            this.activeTab =
                key;

            this.activeButton =
                button;

            this.activateTab(
                button
            );

            this.updateHeading(
                button
            );

            button.scrollIntoView(
                {
                    behavior:
                        'smooth',

                    block:
                        'nearest',

                    inline:
                        'center'
                }
            );

            let endpoint =
                options.url ||
                button.dataset.endpoint ||
                button.dataset.url ||
                this.endpoints[key];

            if (!endpoint) {
                this.error(
                    'رابط التبويب غير موجود.'
                );

                return;
            }

            const query = {
                ...(
                    this.queries.get(
                        key
                    ) ||
                    {}
                ),

                ...(
                    options.query ||
                    {}
                )
            };

            const placeId =
                document.getElementById(
                    'financePlaceSelector'
                )?.value ||
                '';

            if (
                [
                    'overview',
                    'bookings',
                    'topup',
                    'transactions',
                    'refunds',
                    'reports'
                ].includes(
                    key
                )
            ) {
                delete query.PlaceId;
                delete query.placeId;

                if (placeId) {
                    const parameterName =
                        [
                            'overview',
                            'topup'
                        ].includes(
                            key
                        )
                            ? 'placeId'
                            : 'PlaceId';

                    query[parameterName] =
                        placeId;
                }
            }

            endpoint =
                this.appendQuery(
                    endpoint,
                    query
                );

            if (
                options.updateHistory !==
                false
            ) {
                this.updateUrl(
                    key,
                    placeId,
                    options.replaceHistory
                );
            }
            else if (
                options.replaceHistory
            ) {
                this.updateUrl(
                    key,
                    placeId,
                    true
                );
            }

            await this.load(
                endpoint,
                key
            );
        },

        activateTab(activeButton) {
            this.tabs.forEach(
                button => {
                    const isActive =
                        button ===
                        activeButton;

                    button.classList.toggle(
                        'active',
                        isActive
                    );

                    button.setAttribute(
                        'aria-selected',
                        isActive
                            ? 'true'
                            : 'false'
                    );

                    button.tabIndex =
                        isActive
                            ? 0
                            : -1;
                }
            );
        },

        updateHeading(button) {
            this.setText(
                '#financeActiveTabTitle',
                button.dataset.title ||
                button.querySelector(
                    'strong'
                )?.textContent?.trim()
            );

            this.setText(
                '#financeActiveTabDescription',
                button.dataset.description ||
                ''
            );

            const icon =
                document.getElementById(
                    'financeActiveTabIcon'
                );

            if (icon) {
                icon.className =
                    `bi ${button.dataset.icon ||
                    'bi-grid-1x2-fill'
                    }`;
            }
        },

        updateUrl(
            tab,
            placeId,
            replace = false
        ) {
            const url =
                new URL(
                    window.location.href
                );

            url.searchParams.set(
                'financeTab',
                tab
            );

            if (placeId) {
                url.searchParams.set(
                    'placeId',
                    placeId
                );
            }
            else {
                url.searchParams.delete(
                    'placeId'
                );
            }

            window.history[
                replace
                    ? 'replaceState'
                    : 'pushState'
            ](
                {
                    financeTab:
                        tab
                },
                '',
                url
            );
        },

        async load(
            url,
            tab
        ) {
            this.controller?.abort();

            this.controller =
                new AbortController();

            this.setRefresh(
                true
            );

            this.content.setAttribute(
                'aria-busy',
                'true'
            );

            this.renderLoader();

            try {
                const response =
                    await fetch(
                        url,
                        {
                            credentials:
                                'same-origin',

                            cache:
                                'no-store',

                            headers: {
                                'X-Requested-With':
                                    'XMLHttpRequest',

                                Accept:
                                    'text/html, application/xhtml+xml'
                            },

                            signal:
                                this.controller.signal
                        }
                    );

                if (
                    response.status ===
                    401 ||
                    response.status ===
                    403
                ) {
                    throw new Error(
                        'انتهت الجلسة أو لا تملك صلاحية الوصول.'
                    );
                }

                if (!response.ok) {
                    const message =
                        await response.text();

                    throw new Error(
                        message ||
                        `خطأ ${response.status}`
                    );
                }

                const html =
                    await response.text();

                this.content.innerHTML =
                    html;

                this.content.dataset.loadedUrl =
                    url;

                this.content.dataset.loadedTab =
                    tab;

                this.content.setAttribute(
                    'aria-busy',
                    'false'
                );

                this.content.setAttribute(
                    'aria-labelledby',
                    `finance-tab-${tab}`
                );

                this.afterLoad();

                this.setText(
                    '#financeLastUpdatedAt',
                    new Date().toLocaleTimeString(
                        'en-US',
                        {
                            hour:
                                '2-digit',

                            minute:
                                '2-digit'
                        }
                    )
                );
            }
            catch (error) {
                if (
                    error.name !==
                    'AbortError'
                ) {
                    this.renderError(
                        error.message,
                        url
                    );
                }
            }
            finally {
                this.setRefresh(
                    false
                );

                this.controller =
                    null;
            }
        },

        renderLoader() {
            this.content.innerHTML = `
                <div class="finance-skeleton-page"
                     role="status">

                    <div class="finance-skeleton-kpis">

                        ${Array.from(
                {
                    length: 6
                },
                () =>
                    '<div class="finance-skeleton-card"></div>'
            ).join('')}

                    </div>

                    <div class="finance-skeleton-panel"></div>

                </div>
            `;
        },

        renderError(
            message,
            url
        ) {
            this.content.innerHTML = `
                <section class="finance-empty-state">

                    <span class="finance-empty-icon">

                        <i class="bi bi-exclamation-triangle-fill"></i>

                    </span>

                    <h3>
                        تعذر تحميل البيانات
                    </h3>

                    <p>
                        ${this.escape(message)}
                    </p>

                    <button type="button"
                            class="finance-empty-action"
                            data-finance-retry="${this.escape(url)}">

                        <i class="bi bi-arrow-clockwise"></i>

                        إعادة المحاولة

                    </button>

                </section>
            `;
        },

        afterLoad() {
            this.closeMenus();
            this.setDateLimits();
            this.initOverview();
            this.initTopUp();

            this.filters.guarantee = {
                search: '',
                status: ''
            };

            this.filters.wallets = {
                search: '',
                status: ''
            };

            this.filters.documents = {
                search: '',
                type: ''
            };

            this.filterGuarantee();
            this.filterWallets();
            this.filterDocuments();
            this.initCharts();
        },

        initOverview() {
            const overview =
                this.content?.querySelector(
                    '[data-finance-partial="overview"]'
                );

            if (!overview) {
                return;
            }

            const current =
                this.queries.get(
                    'overview'
                ) ||
                {};

            const fromDate =
                current.fromDate ||
                current.FromDate ||
                overview.dataset.periodStart ||
                '';

            const toDate =
                current.toDate ||
                current.ToDate ||
                overview.dataset.periodEnd ||
                '';

            /*
             * حفظ الفترة المعروضة حاليًا،
             * حتى تظهر تلقائيًا عند فتح نافذة تعديل الفترة.
             */
            this.queries.set(
                'overview',
                {
                    fromDate,
                    toDate
                }
            );

            this.content.setAttribute(
                'aria-labelledby',
                'finance-tab-overview'
            );

            /*
             * تحريك عدادات الحجوزات عند تحميل التبويب.
             */
            this.content
                .querySelectorAll(
                    '[data-count-up]'
                )
                .forEach(
                    element => {
                        const target =
                            Math.max(
                                0,
                                Math.round(
                                    this.number(
                                        element.dataset.countUp
                                    )
                                )
                            );

                        const duration =
                            500;

                        const startedAt =
                            performance.now();

                        const draw =
                            now => {
                                const progress =
                                    Math.min(
                                        1,
                                        (
                                            now -
                                            startedAt
                                        ) /
                                        duration
                                    );

                                const eased =
                                    1 -
                                    Math.pow(
                                        1 -
                                        progress,
                                        3
                                    );

                                element.textContent =
                                    new Intl.NumberFormat(
                                        'en-US'
                                    ).format(
                                        Math.round(
                                            target *
                                            eased
                                        )
                                    );

                                if (
                                    progress <
                                    1
                                ) {
                                    requestAnimationFrame(
                                        draw
                                    );
                                }
                            };

                        requestAnimationFrame(
                            draw
                        );
                    }
                );
        },
        initCharts() {
            this.content
                .querySelectorAll(
                    '[data-chart-options]'
                )
                .forEach(
                    element => {
                        if (
                            !window.ApexCharts ||
                            element.dataset.chartInitialized ===
                            'true'
                        ) {
                            return;
                        }

                        try {
                            const chart =
                                new ApexCharts(
                                    element,
                                    JSON.parse(
                                        element.dataset.chartOptions
                                    )
                                );

                            chart.render();

                            element.dataset.chartInitialized =
                                'true';
                        }
                        catch (error) {
                            console.warn(
                                'ApexCharts:',
                                error
                            );
                        }
                    }
                );
        },

        refresh() {
            this.openTab(
                this.activeTab,
                {
                    updateHistory: false
                }
            );
        },

        updateHeaderFromPlace() {
            const option =
                document.getElementById(
                    'financePlaceSelector'
                )?.selectedOptions?.[0];

            if (!option) {
                return;
            }

            this.setText(
                '#financeHeaderAvailableBalance',
                this.money(
                    this.number(
                        option.dataset.available
                    )
                )
            );

            this.setText(
                '#financeHeaderHeldBalance',
                this.money(
                    this.number(
                        option.dataset.held
                    )
                )
            );

            this.setText(
                '#financeHeaderDebtBalance',
                this.money(
                    this.number(
                        option.dataset.debt
                    )
                )
            );

            this.setText(
                '#financeHeaderAccountStatus',
                option.dataset.statusLabel ||
                'جميع المنشآت'
            );
        },

        submitFilters(form) {
            const query = {};

            for (
                const [key, value]
                of new FormData(
                    form
                )
            ) {
                if (
                    key !==
                    '__RequestVerificationToken' &&
                    String(value).trim()
                ) {
                    query[key] =
                        String(value).trim();
                }
            }

            query.PageNumber ||=
                1;

            this.queries.set(
                this.activeTab,
                query
            );

            this.openTab(
                this.activeTab,
                {
                    url:
                        form.action ||
                        undefined,

                    updateHistory:
                        false
                }
            );
        },

        resetServerFilters(button) {
            const form =
                button
                    ?.closest(
                        '[data-finance-partial]'
                    )
                    ?.querySelector(
                        '[data-finance-filter-form]'
                    );

            if (!form) {
                return;
            }

            form.reset();

            form.querySelectorAll(
                'input:not([type="hidden"]), textarea'
            ).forEach(
                input => {
                    input.value =
                        '';
                }
            );

            form.querySelectorAll(
                'select'
            ).forEach(
                select => {
                    select.selectedIndex =
                        0;
                }
            );

            if (
                this.activeTab !==
                'overview'
            ) {
                this.queries.delete(
                    this.activeTab
                );
            }

          

            this.submitFilters(
                form
            );
        },

        goToPage(button) {
            const form =
                button
                    .closest(
                        '[data-finance-partial]'
                    )
                    ?.querySelector(
                        '[data-finance-filter-form]'
                    );

            if (!form) {
                return;
            }

            let pageInput =
                form.querySelector(
                    '[name="PageNumber"]'
                );

            if (!pageInput) {
                pageInput =
                    document.createElement(
                        'input'
                    );

                pageInput.type =
                    'hidden';

                pageInput.name =
                    'PageNumber';

                form.appendChild(
                    pageInput
                );
            }

            pageInput.value =
                button.dataset.financePage;

            this.submitFilters(
                form
            );
        },

        transactionStatus(button) {
            const partial =
                button.closest(
                    '[data-finance-partial="transactions"]'
                );

            const form =
                partial?.querySelector(
                    '[data-finance-filter-form]'
                );

            if (!form) {
                return;
            }

            partial
                .querySelectorAll(
                    '[data-transaction-status-filter]'
                )
                .forEach(
                    item => {
                        item.classList.toggle(
                            'active',
                            item ===
                            button
                        );
                    }
                );

            const select =
                form.querySelector(
                    '[name="Status"]'
                );

            if (select) {
                select.value =
                    button.dataset.transactionStatusFilter ||
                    '';
            }

            this.submitFilters(
                form
            );
        },

        filterGuarantee() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="commission-guarantee"]'
                );

            if (!partial) {
                return;
            }

            const search =
                this.normalize(
                    this.filters.guarantee.search
                );

            const status =
                this.normalize(
                    this.filters.guarantee.status
                );

            let visibleCount =
                0;

            partial
                .querySelectorAll(
                    '[data-guarantee-account]'
                )
                .forEach(
                    card => {
                        const matchesSearch =
                            !search ||
                            this.normalize(
                                card.dataset.placeName
                            ).includes(
                                search
                            );

                        const matchesStatus =
                            !status ||
                            this.normalize(
                                card.dataset.accountStatus
                            ) ===
                            status;

                        const show =
                            matchesSearch &&
                            matchesStatus;

                        card.hidden =
                            !show;

                        if (show) {
                            visibleCount++;
                        }
                    }
                );

            const empty =
                partial.querySelector(
                    '#guaranteeFilterEmpty'
                );

            if (empty) {
                empty.hidden =
                    visibleCount >
                    0;
            }
        },

        resetGuarantee() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="commission-guarantee"]'
                );

            if (!partial) {
                return;
            }

            const input =
                partial.querySelector(
                    '[data-guarantee-search]'
                );

            if (input) {
                input.value =
                    '';
            }

            this.filters.guarantee = {
                search: '',
                status: ''
            };

            partial
                .querySelectorAll(
                    '[data-guarantee-status-filter]'
                )
                .forEach(
                    button => {
                        button.classList.toggle(
                            'active',
                            !button.dataset.guaranteeStatusFilter
                        );
                    }
                );

            this.filterGuarantee();
        },

        filterWallets() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="owner-wallets"]'
                );

            if (!partial) {
                return;
            }

            const search =
                this.normalize(
                    this.filters.wallets.search
                );

            const status =
                this.normalize(
                    this.filters.wallets.status
                );

            let visibleCount =
                0;

            partial
                .querySelectorAll(
                    '[data-owner-wallet-card]'
                )
                .forEach(
                    card => {
                        const haystack =
                            this.normalize(
                                [
                                    card.dataset.walletName,
                                    card.dataset.accountNumber,
                                    card.dataset.beneficiary
                                ].join(
                                    ' '
                                )
                            );

                        const matchesSearch =
                            !search ||
                            haystack.includes(
                                search
                            );

                        const matchesStatus =
                            !status ||
                            this.normalize(
                                card.dataset.walletStatus
                            ) ===
                            status;

                        const show =
                            matchesSearch &&
                            matchesStatus;

                        card.hidden =
                            !show;

                        if (show) {
                            visibleCount++;
                        }
                    }
                );

            const empty =
                partial.querySelector(
                    '#ownerWalletFilterEmpty'
                );

            if (empty) {
                empty.hidden =
                    visibleCount >
                    0;
            }
        },

        resetWallets() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="owner-wallets"]'
                );

            if (!partial) {
                return;
            }

            const input =
                partial.querySelector(
                    '[data-owner-wallet-search]'
                );

            if (input) {
                input.value =
                    '';
            }

            this.filters.wallets = {
                search: '',
                status: ''
            };

            partial
                .querySelectorAll(
                    '[data-owner-wallet-status-filter]'
                )
                .forEach(
                    button => {
                        button.classList.toggle(
                            'active',
                            !button.dataset.ownerWalletStatusFilter
                        );
                    }
                );

            this.filterWallets();
        },

        filterDocuments() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="invoices-reports"]'
                );

            if (!partial) {
                return;
            }

            const search =
                this.normalize(
                    this.filters.documents.search
                );

            const type =
                this.normalize(
                    this.filters.documents.type
                );

            let visibleCount =
                0;

            partial
                .querySelectorAll(
                    '[data-financial-document]'
                )
                .forEach(
                    item => {
                        const haystack =
                            this.normalize(
                                [
                                    item.dataset.documentNumber,
                                    item.dataset.bookingId,
                                    item.dataset.customerName,
                                    item.dataset.placeName
                                ].join(
                                    ' '
                                )
                            );

                        const matchesSearch =
                            !search ||
                            haystack.includes(
                                search
                            );

                        const matchesType =
                            !type ||
                            this.normalize(
                                item.dataset.documentType
                            ) ===
                            type;

                        const show =
                            matchesSearch &&
                            matchesType;

                        item.hidden =
                            !show;

                        if (show) {
                            visibleCount++;
                        }
                    }
                );

            const empty =
                partial.querySelector(
                    '#financialDocumentsFilterEmpty'
                );

            if (empty) {
                empty.hidden =
                    visibleCount >
                    0;
            }
        },

        resetDocuments() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="invoices-reports"]'
                );

            if (!partial) {
                return;
            }

            const input =
                partial.querySelector(
                    '[data-financial-document-search]'
                );

            if (input) {
                input.value =
                    '';
            }

            this.filters.documents = {
                search: '',
                type: ''
            };

            partial
                .querySelectorAll(
                    '[data-document-type-filter]'
                )
                .forEach(
                    button => {
                        button.classList.toggle(
                            'active',
                            !button.dataset.documentTypeFilter
                        );
                    }
                );

            this.filterDocuments();
        },

        initTopUp() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="topup-balance"]'
                );

            if (!partial) {
                return;
            }

            const selectedCard =
                partial.querySelector(
                    '[data-platform-wallet-card].selected, ' +
                    '[data-platform-wallet-card]'
                );

            if (selectedCard) {
                this.selectPlatformWallet(
                    selectedCard
                );
            }

            this.updateTopUpAccount();
            this.updateTopUpCalculation();
        },

        selectPlatformWallet(card) {
            const partial =
                card.closest(
                    '[data-finance-partial="topup-balance"]'
                );

            if (!partial) {
                return;
            }

            partial
                .querySelectorAll(
                    '[data-platform-wallet-card]'
                )
                .forEach(
                    item => {
                        const selected =
                            item ===
                            card;

                        item.classList.toggle(
                            'selected',
                            selected
                        );

                        item.classList.toggle(
                            'active',
                            selected
                        );

                        const radio =
                            item.querySelector(
                                '[data-platform-wallet-radio]'
                            );

                        if (radio) {
                            radio.checked =
                                selected;
                        }
                    }
                );

            const hiddenInput =
                partial.querySelector(
                    '#topUpPlatformWalletId'
                );

            if (hiddenInput) {
                hiddenInput.value =
                    card.dataset.platformWalletId ||
                    '';
            }
        },

        updateTopUpAccount() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="topup-balance"]'
                );

            const option =
                partial
                    ?.querySelector(
                        '[data-topup-place-select]'
                    )
                    ?.selectedOptions
                ?.[0];

            if (
                !partial ||
                !option
            ) {
                return;
            }

            const available =
                this.number(
                    option.dataset.available
                );

            const held =
                this.number(
                    option.dataset.held
                );

            const debt =
                this.number(
                    option.dataset.debt
                );

            this.setText(
                '#topUpSelectedPlaceName',
                option.dataset.placeName ||
                option.textContent.trim()
            );

            this.setText(
                '#topUpSelectedAccountStatus',
                option.dataset.accountStatus ||
                'غير محدد'
            );

            this.setText(
                '#topUpSelectedCurrentBalance',
                this.money(
                    available
                )
            );

            this.setText(
                '#topUpSelectedAvailableBalance',
                this.money(
                    available
                )
            );

            this.setText(
                '#topUpSelectedHeldBalance',
                this.money(
                    held
                )
            );

            this.setText(
                '#topUpSelectedDebtAmount',
                this.money(
                    debt
                )
            );

            const preview =
                partial.querySelector(
                    '#topUpSelectedAccountPreview'
                );

            if (preview) {
                preview.dataset.available =
                    available;

                preview.dataset.debt =
                    debt;
            }

            this.updateTopUpCalculation();
        },

        updateTopUpCalculation() {
            const partial =
                document.querySelector(
                    '[data-finance-partial="topup-balance"]'
                );

            if (!partial) {
                return;
            }

            const amount =
                this.number(
                    partial.querySelector(
                        '[data-topup-amount]'
                    )?.value
                );

            const preview =
                partial.querySelector(
                    '#topUpSelectedAccountPreview'
                );

            const available =
                this.number(
                    preview?.dataset.available
                );

            const debt =
                this.number(
                    preview?.dataset.debt
                );

            const debtPayment =
                Math.min(
                    Math.max(
                        amount,
                        0
                    ),
                    Math.max(
                        debt,
                        0
                    )
                );

            this.setText(
                '#topUpPreviewAmount',
                this.money(
                    amount
                )
            );

            this.setText(
                '#topUpPreviewDebtPayment',
                this.money(
                    debtPayment
                )
            );

            this.setText(
                '#topUpPreviewNetBalance',
                this.money(
                    available +
                    Math.max(
                        amount -
                        debtPayment,
                        0
                    )
                )
            );
        },

        setTopUpFile(file) {
            if (!file) {
                this.clearTopUpFile();

                return;
            }

            const extension =
                file.name
                    .split(
                        '.'
                    )
                    .pop()
                    ?.toLowerCase();

            if (
                ![
                    'jpg',
                    'jpeg',
                    'png',
                    'webp',
                    'pdf'
                ].includes(
                    extension
                )
            ) {
                this.error(
                    'الصيغة غير مدعومة. استخدم JPG أو PNG أو WEBP أو PDF.'
                );

                this.clearTopUpFile();

                return;
            }

            if (
                file.size >
                5 *
                1024 *
                1024
            ) {
                this.error(
                    'حجم السند يجب ألا يتجاوز 5 ميجابايت.'
                );

                this.clearTopUpFile();

                return;
            }

            this.selectedTopUpFile =
                file;

            const emptyState =
                document.getElementById(
                    'topUpUploaderEmptyState'
                );

            const preview =
                document.getElementById(
                    'topUpUploaderPreview'
                );

            const media =
                document.getElementById(
                    'topUpPreviewMedia'
                );

            if (emptyState) {
                emptyState.hidden =
                    true;
            }

            if (preview) {
                preview.hidden =
                    false;
            }

            this.setText(
                '#topUpPreviewFileName',
                file.name
            );

            this.setText(
                '#topUpPreviewFileSize',
                this.fileSize(
                    file.size
                )
            );

            if (!media) {
                return;
            }

            media.innerHTML =
                '';

            const objectUrl =
                URL.createObjectURL(
                    file
                );

            const element =
                extension ===
                    'pdf'
                    ? document.createElement(
                        'iframe'
                    )
                    : document.createElement(
                        'img'
                    );

            element.src =
                objectUrl;

            element.className =
                'topup-preview-media';

            element.addEventListener(
                'load',
                () => {
                    URL.revokeObjectURL(
                        objectUrl
                    );
                },
                {
                    once: true
                }
            );

            media.appendChild(
                element
            );
        },

        clearTopUpFile() {
            this.selectedTopUpFile =
                null;

            const input =
                document.querySelector(
                    '[data-topup-receipt-input]'
                );

            if (input) {
                input.value =
                    '';
            }

            const emptyState =
                document.getElementById(
                    'topUpUploaderEmptyState'
                );

            const preview =
                document.getElementById(
                    'topUpUploaderPreview'
                );

            if (emptyState) {
                emptyState.hidden =
                    false;
            }

            if (preview) {
                preview.hidden =
                    true;
            }

            const media =
                document.getElementById(
                    'topUpPreviewMedia'
                );

            if (media) {
                media.innerHTML =
                    '';
            }
        },

        resetTopUp() {
            const form =
                document.querySelector(
                    '[data-topup-request-form]'
                );

            if (!form) {
                return;
            }

            form.reset();

            this.clearTopUpFile();
            this.initTopUp();

            this.setText(
                '#topUpNotesCounter',
                '0'
            );
        },

        async submitTopUp(form) {
            const placeId =
                form.querySelector(
                    '[name="PlaceId"]'
                )?.value;

            const walletId =
                form.querySelector(
                    '[name="PlatformWalletId"]'
                )?.value;

            const amount =
                this.number(
                    form.querySelector(
                        '[name="Amount"]'
                    )?.value
                );

            const reference =
                form.querySelector(
                    '[name="ReferenceNumber"]'
                )?.value.trim();

            const transferDate =
                form.querySelector(
                    '[name="TransferDate"]'
                )?.value;

            const receiptFile =
                form.querySelector(
                    '[name="receiptFile"]'
                )?.files?.[0] ||
                this.selectedTopUpFile;

            if (!placeId) {
                this.error(
                    'اختر المنشأة.'
                );

                return;
            }

            if (!walletId) {
                this.error(
                    'اختر حساب المنصة.'
                );

                return;
            }

            if (
                !(amount > 0)
            ) {
                this.error(
                    'أدخل مبلغًا صحيحًا.'
                );

                return;
            }

            if (!reference) {
                this.error(
                    'أدخل الرقم المرجعي.'
                );

                return;
            }

            if (!transferDate) {
                this.error(
                    'حدد تاريخ التحويل.'
                );

                return;
            }

            if (!receiptFile) {
                this.error(
                    'أرفق سند التحويل.'
                );

                return;
            }

            const submitButton =
                form.querySelector(
                    '#submitTopUpRequestBtn'
                );

            this.loading(
                submitButton,
                true,
                '.topup-submit-default',
                '.topup-submit-loading'
            );

            try {
                const formData =
                    new FormData(
                        form
                    );

                if (
                    this.selectedTopUpFile &&
                    !form.querySelector(
                        '[name="receiptFile"]'
                    )?.files?.length
                ) {
                    formData.set(
                        'receiptFile',
                        this.selectedTopUpFile
                    );
                }

                const result =
                    await this.post(
                        form.action ||
                        this.endpoints.submitTopUp,
                        formData
                    );

                if (!result.success) {
                    throw new Error(
                        result.message ||
                        'تعذر إرسال الطلب.'
                    );
                }

                await this.success(
                    result.message ||
                    'تم إرسال طلب الشحن.'
                );

                this.openTab(
                    'topup',
                    {
                        updateHistory: false
                    }
                );
            }
            catch (error) {
                this.error(
                    error.message
                );
            }
            finally {
                this.loading(
                    submitButton,
                    false,
                    '.topup-submit-default',
                    '.topup-submit-loading'
                );
            }
        },

        async cancelTopUp(button) {
            const transactionId =
                button.dataset.transactionId;

            if (!transactionId) {
                return;
            }

            const confirmed =
                await this.confirm(
                    'إلغاء طلب الشحن؟',
                    'يمكن إلغاء الطلب إذا كان ما يزال قيد المراجعة.',
                    'إلغاء الطلب'
                );

            if (!confirmed) {
                return;
            }

            button.disabled =
                true;

            try {
                const formData =
                    this.postData(
                        button
                    );

                formData.set(
                    'commissionTransactionId',
                    transactionId
                );

                const result =
                    await this.post(
                        this.endpoints.cancelTopUp,
                        formData
                    );

                if (!result.success) {
                    throw new Error(
                        result.message ||
                        'تعذر الإلغاء.'
                    );
                }

                await this.success(
                    result.message ||
                    'تم إلغاء الطلب.'
                );

                this.openTab(
                    'topup',
                    {
                        updateHistory: false
                    }
                );
            }
            catch (error) {
                button.disabled =
                    false;

                this.error(
                    error.message
                );
            }
        },

        openWalletModal(button) {
            const modal =
                document.getElementById(
                    'ownerWalletModal'
                );

            const form =
                document.getElementById(
                    'ownerWalletForm'
                );

            if (
                !modal ||
                !form
            ) {
                this.error(
                    'تعذر فتح النموذج.'
                );

                return;
            }

            form.reset();

            const isEdit =
                button.dataset.walletMode ===
                'edit';

            this.value(
                '#ownerWalletId',
                isEdit
                    ? button.dataset.ownerWalletId
                    : ''
            );

            this.value(
                '#ownerWalletNetwork',
                button.dataset.walletId ||
                ''
            );

            this.value(
                '#ownerWalletAccountNumber',
                isEdit
                    ? button.dataset.accountNumber
                    : ''
            );

            this.value(
                '#ownerWalletBeneficiary',
                isEdit
                    ? button.dataset.beneficiary
                    : ''
            );

            const activeInput =
                document.getElementById(
                    'ownerWalletIsActive'
                );

            if (activeInput) {
                activeInput.checked =
                    isEdit
                        ? button.dataset.isActive ===
                        'true'
                        : true;
            }

            this.setText(
                '#ownerWalletModalTitle',
                isEdit
                    ? 'تعديل حساب الاستقبال'
                    : 'إضافة حساب استقبال'
            );

            this.showModal(
                modal
            );
        },

        async saveWallet(form) {
            const walletId =
                form.querySelector(
                    '[name="WalletId"]'
                )?.value;

            const accountNumber =
                form.querySelector(
                    '[name="AccountNumber"]'
                )?.value.trim();

            const beneficiary =
                form.querySelector(
                    '[name="BeneficiaryName"]'
                )?.value.trim();

            if (!walletId) {
                this.error(
                    'اختر شبكة الدفع.'
                );

                return;
            }

            if (
                !accountNumber ||
                accountNumber.length <
                3
            ) {
                this.error(
                    'أدخل رقم حساب صحيحًا.'
                );

                return;
            }

            if (
                !beneficiary ||
                beneficiary.length <
                2
            ) {
                this.error(
                    'أدخل اسم المستفيد.'
                );

                return;
            }

            const submitButton =
                form.querySelector(
                    '#ownerWalletSaveBtn'
                );

            this.loading(
                submitButton,
                true,
                '.owner-wallet-save-default',
                '.owner-wallet-save-loading'
            );

            try {
                const formData =
                    new FormData(
                        form
                    );

                formData.set(
                    'IsActive',
                    String(
                        form.querySelector(
                            '[name="IsActive"]'
                        )?.checked ===
                        true
                    )
                );

                const result =
                    await this.post(
                        form.action ||
                        this.endpoints.saveWallet,
                        formData
                    );

                if (!result.success) {
                    throw new Error(
                        result.message ||
                        'تعذر حفظ الحساب.'
                    );
                }

                this.hideModal(
                    document.getElementById(
                        'ownerWalletModal'
                    )
                );

                await this.success(
                    result.message ||
                    'تم حفظ الحساب.'
                );

                this.openTab(
                    'wallets',
                    {
                        updateHistory: false
                    }
                );
            }
            catch (error) {
                this.error(
                    error.message
                );
            }
            finally {
                this.loading(
                    submitButton,
                    false,
                    '.owner-wallet-save-default',
                    '.owner-wallet-save-loading'
                );
            }
        },

        async changeWalletStatus(button) {
            const ownerWalletId =
                button.dataset.ownerWalletId;

            const nextStatus =
                button.dataset.nextStatus ===
                'true';

            if (!ownerWalletId) {
                return;
            }

            const confirmed =
                await this.confirm(
                    nextStatus
                        ? 'تفعيل الحساب؟'
                        : 'تعطيل الحساب؟',

                    nextStatus
                        ? 'سيظهر الحساب للعملاء.'
                        : 'لن يظهر الحساب للعملاء.',

                    nextStatus
                        ? 'تفعيل'
                        : 'تعطيل'
                );

            if (!confirmed) {
                return;
            }

            button.disabled =
                true;

            try {
                const formData =
                    this.postData(
                        button
                    );

                formData.set(
                    'ownerWalletId',
                    ownerWalletId
                );

                formData.set(
                    'isActive',
                    String(
                        nextStatus
                    )
                );

                const result =
                    await this.post(
                        this.endpoints.changeWalletStatus,
                        formData
                    );

                if (!result.success) {
                    throw new Error(
                        result.message ||
                        'تعذر تغيير الحالة.'
                    );
                }

                await this.success(
                    result.message ||
                    'تم تغيير الحالة.'
                );

                this.openTab(
                    'wallets',
                    {
                        updateHistory: false
                    }
                );
            }
            catch (error) {
                button.disabled =
                    false;

                this.error(
                    error.message
                );
            }
        },

        bookingDetails(button) {
            const data =
                button.dataset;

            this.drawer(
                `الحجز #${data.bookingId || ''}`,
                `${data.customer || ''} — ${data.place || ''}`,
                'bi-journal-check',
                [
                    [
                        'رقم الحجز',
                        data.bookingId
                    ],
                    [
                        'العميل',
                        data.customer
                    ],
                    [
                        'الهاتف',
                        data.phone
                    ],
                    [
                        'المنشأة',
                        data.place
                    ],
                    [
                        'الوحدة',
                        data.unit
                    ],
                    [
                        'البداية',
                        data.start
                    ],
                    [
                        'النهاية',
                        data.end
                    ],
                    [
                        'حالة الحجز',
                        data.bookingState
                    ],
                    [
                        'حالة الدفع',
                        data.paymentStatus
                    ],
                    [
                        'الإجمالي',
                        data.total
                    ],
                    [
                        'العربون',
                        data.deposit
                    ],
                    [
                        'المدفوع إلكترونيًا',
                        data.paidOnline
                    ],
                    [
                        'المحصل عند المنشأة',
                        data.propertyCollected
                    ],
                    [
                        'المتبقي',
                        data.propertyRemaining
                    ],
                    [
                        'عمولة المنصة',
                        data.commission
                    ],
                    [
                        'صافي المالك',
                        data.ownerNet
                    ],
                    [
                        'حالة العمولة',
                        data.commissionStatus
                    ]
                ],
                data.paymentImage
                    ? [
                        {
                            label:
                                'عرض إشعار الدفع',

                            url:
                                data.paymentImage
                        }
                    ]
                    : []
            );
        },

        guaranteeDetails(button) {
            const data =
                button.dataset;

            this.drawer(
                `حساب ضمان ${data.placeName || ''}`,
                `الحساب #${data.accountId || ''}`,
                'bi-shield-lock-fill',
                [
                    [
                        'الحالة',
                        data.status
                    ],
                    [
                        'الرصيد المتاح',
                        data.available
                    ],
                    [
                        'المحجوز',
                        data.held
                    ],
                    [
                        'الدين',
                        data.debt
                    ],
                    [
                        'حد الدين',
                        data.debtLimit
                    ],
                    [
                        'حد التنبيه',
                        data.lowThreshold
                    ],
                    [
                        'القدرة القابلة للاستخدام',
                        data.usableCapacity
                    ],
                    [
                        'استقبال الحجوزات',
                        data.canBook ===
                            'true'
                            ? 'متاح'
                            : 'متوقف'
                    ],
                    [
                        'آخر شحن',
                        data.lastTopup
                    ]
                ]
            );
        },

        topUpDetails(button) {
            const data =
                button.dataset;

            this.drawer(
                `طلب شحن #${data.transactionId || ''}`,
                data.place ||
                '',
                'bi-wallet-fill',
                [
                    [
                        'المبلغ',
                        data.amount
                    ],
                    [
                        'الحالة',
                        data.statusLabel ||
                        data.status
                    ],
                    [
                        'حساب المنصة',
                        data.wallet
                    ],
                    [
                        'المرجع',
                        data.reference
                    ],
                    [
                        'تاريخ التحويل',
                        data.transferDate
                    ],
                    [
                        'تاريخ الإرسال',
                        data.created
                    ],
                    [
                        'تاريخ المراجعة',
                        data.reviewed
                    ],
                    [
                        'المراجع',
                        data.reviewer
                    ],
                    [
                        'سبب الرفض',
                        data.rejection
                    ],
                    [
                        'الملاحظات',
                        data.notes
                    ]
                ],
                data.receipt
                    ? [
                        {
                            label:
                                'عرض السند',

                            url:
                                data.receipt
                        }
                    ]
                    : []
            );
        },

        transactionDetails(button) {
            const data =
                button.dataset;

            this.drawer(
                `الحركة #${data.transactionId ||
                data.financeTransactionId ||
                ''
                }`,
                data.typeLabel ||
                data.type ||
                '',
                'bi-arrow-left-right',
                [
                    [
                        'المنشأة',
                        data.place
                    ],
                    [
                        'نوع الحركة',
                        data.typeLabel ||
                        data.type
                    ],
                    [
                        'الحالة',
                        data.statusLabel ||
                        data.status
                    ],
                    [
                        'المبلغ',
                        data.amount
                    ],
                    [
                        'رقم الحجز',
                        data.bookingId
                    ],
                    [
                        'المرجع',
                        data.reference
                    ],
                    [
                        'تاريخ الحركة',
                        data.createdAt
                    ],
                    [
                        'الرصيد المتاح قبل',
                    ],
                    [
                        'الرصيد المتاح بعد',
                        data.availableAfter
                    ],
                    [
                        'المحجوز قبل',
                        data.heldBefore
                    ],
                    [
                        'المحجوز بعد',
                        data.heldAfter
                    ],
                    [
                        'الدين قبل',
                        data.debtBefore
                    ],
                    [
                        'الدين بعد',
                        data.debtAfter
                    ],
                    [
                        'الملاحظات',
                        data.notes
                    ]
                ],
                data.receipt
                    ? [
                        {
                            label:
                                'عرض السند',

                            url:
                                data.receipt
                        }
                    ]
                    : []
            );
        },

        refundDetails(button) {
            const data =
                button.dataset;

            this.drawer(
                `الاسترداد #${data.refundId || ''}`,
                `${data.customer || ''} — ${data.place || ''}`,
                'bi-arrow-counterclockwise',
                [
                    [
                        'الحجز',
                        data.bookingId
                    ],
                    [
                        'الحالة',
                        data.statusLabel ||
                        data.status
                    ],
                    [
                        'العربون',
                        data.deposit
                    ],
                    [
                        'رسوم الإلغاء',
                        data.fee
                    ],
                    [
                        'المبلغ المسترد',
                        data.refund
                    ],
                    [
                        'حصة المنشأة',
                        data.ownerShare
                    ],
                    [
                        'عمولة المنصة',
                        data.commission
                    ],
                    [
                        'العمولة المحررة',
                        data.commissionReleased
                    ],
                    [
                        'المحتفظ به',
                        data.retained
                    ],
                    [
                        'السبب',
                        data.reason
                    ],
                    [
                        'المرجع',
                        data.reference
                    ],
                    [
                        'الملاحظات',
                        data.notes
                    ]
                ],
                data.proof
                    ? [
                        {
                            label:
                                'إثبات الاسترداد',

                            url:
                                data.proof
                        }
                    ]
                    : []
            );
        },

        reportDetails(button) {
            const data =
                button.dataset;

            this.drawer(
                `المستند ${data.documentNumber || ''}`,
                `${data.customer || ''} — ${data.place || ''}`,
                'bi-file-earmark-bar-graph-fill',
                [
                    [
                        'الحجز',
                        data.bookingId
                    ],
                    [
                        'تاريخ المستند',
                        data.documentDate
                    ],
                    [
                        'الإجمالي',
                        data.total
                    ],
                    [
                        'العربون',
                        data.deposit
                    ],
                    [
                        'المحصل إلكترونيًا',
                        data.online
                    ],
                    [
                        'المحصل عند المنشأة',
                        data.property
                    ],
                    [
                        'إجمالي المحصل',
                        data.collected
                    ],
                    [
                        'المتبقي',
                        data.remaining
                    ],
                    [
                        'عمولة المنصة',
                        data.commission
                    ],
                    [
                        'صافي المالك',
                        data.ownerNet
                    ]
                ]
            );
        },

        drawer(
            title,
            subtitle,
            icon,
            rows,
            actions = []
        ) {
            const drawer =
                document.getElementById(
                    'financeDetailsDrawer'
                );

            const backdrop =
                document.getElementById(
                    'financeDrawerBackdrop'
                );

            const body =
                document.getElementById(
                    'financeDrawerBody'
                );

            if (
                !drawer ||
                !backdrop ||
                !body
            ) {
                return;
            }

            this.setText(
                '#financeDrawerTitle',
                title
            );

            this.setText(
                '#financeDrawerSubtitle',
                subtitle
            );

            const iconElement =
                document.getElementById(
                    'financeDrawerIcon'
                );

            if (iconElement) {
                iconElement.className =
                    `bi ${icon}`;
            }

            body.innerHTML = `
                <div class="finance-drawer-details-grid">

                    ${rows
                    .filter(
                        ([, value]) =>
                            value
                    )
                    .map(
                        ([label, value]) => `
                                <div class="finance-drawer-detail-row">

                                    <span>
                                        ${this.escape(label)}
                                    </span>

                                    <strong>
                                        ${this.escape(value)}
                                    </strong>

                                </div>
                            `
                    )
                    .join('')}

                </div>

                <div class="finance-drawer-actions">

                    ${actions
                    .map(
                        (action, index) => `
                                <button type="button"
                                        data-drawer-doc="${index}">

                                    <i class="bi bi-file-earmark-image"></i>

                                    ${this.escape(action.label)}

                                </button>
                            `
                    )
                    .join('')}

                </div>
            `;

            actions.forEach(
                (action, index) => {
                    body
                        .querySelector(
                            `[data-drawer-doc="${index}"]`
                        )
                        ?.addEventListener(
                            'click',
                            () => {
                                this.previewDocument(
                                    action.url,
                                    action.label
                                );
                            }
                        );
                }
            );

            backdrop.hidden =
                false;

            drawer.classList.add(
                'is-open',
                'open',
                'active'
            );

            backdrop.classList.add(
                'is-visible',
                'show',
                'active'
            );

            drawer.setAttribute(
                'aria-hidden',
                'false'
            );

            document.body.classList.add(
                'finance-drawer-open'
            );
        },

        closeDrawer() {
            const drawer =
                document.getElementById(
                    'financeDetailsDrawer'
                );

            const backdrop =
                document.getElementById(
                    'financeDrawerBackdrop'
                );

            if (
                !drawer ||
                !backdrop
            ) {
                return;
            }

            drawer.classList.remove(
                'is-open',
                'open',
                'active'
            );

            backdrop.classList.remove(
                'is-visible',
                'show',
                'active'
            );

            drawer.setAttribute(
                'aria-hidden',
                'true'
            );

            document.body.classList.remove(
                'finance-drawer-open'
            );

            setTimeout(
                () => {
                    if (
                        !backdrop.classList.contains(
                            'show'
                        )
                    ) {
                        backdrop.hidden =
                            true;
                    }
                },
                250
            );
        },

        previewDocument(
            rawUrl,
            title
        ) {
            const url =
                this.safeUrl(
                    rawUrl
                );

            if (!url) {
                this.error(
                    'لا يوجد مستند متاح.'
                );

                return;
            }

            const modal =
                document.getElementById(
                    'financeDocumentPreviewModal'
                );

            const container =
                document.getElementById(
                    'financeDocumentPreviewContainer'
                );

            const openButton =
                document.getElementById(
                    'financeOpenDocumentBtn'
                );

            if (
                !modal ||
                !container ||
                !openButton
            ) {
                window.open(
                    url,
                    '_blank',
                    'noopener,noreferrer'
                );

                return;
            }

            this.setText(
                '#financeDocumentPreviewTitle',
                title
            );

            openButton.href =
                url;

            container.innerHTML =
                '';

            const isPdf =
                /\.pdf(?:$|\?)/i.test(
                    url
                );

            const element =
                isPdf
                    ? document.createElement(
                        'iframe'
                    )
                    : document.createElement(
                        'img'
                    );

            element.src =
                url;

            element.className =
                isPdf
                    ? 'finance-document-frame'
                    : 'finance-document-image';

            container.appendChild(
                element
            );

            this.showModal(
                modal
            );
        },

        periodDialog() {
            const overview =
                this.content?.querySelector(
                    '[data-finance-partial="overview"]'
                );

            if (!overview) {
                this.error(
                    'تعذر العثور على بيانات النظرة العامة.'
                );

                return;
            }

            const current =
                this.queries.get(
                    'overview'
                ) ||
                {};

            const fromValue =
                current.fromDate ||
                current.FromDate ||
                overview.dataset.periodStart ||
                '';

            const toValue =
                current.toDate ||
                current.ToDate ||
                overview.dataset.periodEnd ||
                '';

            const now =
                new Date();

            const today =
                new Date(
                    now.getTime() -
                    now.getTimezoneOffset() *
                    60000
                )
                    .toISOString()
                    .slice(
                        0,
                        10
                    );

            const html = `
        <div class="finance-period-dialog"
             dir="rtl">

            <label>

                <span>
                    من تاريخ
                </span>

                <input id="financePeriodFrom"
                       type="date"
                       max="${today}"
                       value="${this.escape(fromValue)}">

            </label>

            <label>

                <span>
                    إلى تاريخ
                </span>

                <input id="financePeriodTo"
                       type="date"
                       max="${today}"
                       value="${this.escape(toValue)}">

            </label>

        </div>
    `;

            this.formDialog(
                'تحديد فترة مؤشرات النظرة العامة',
                html,
                () => {
                    const fromDate =
                        document.getElementById(
                            'financePeriodFrom'
                        )?.value ||
                        '';

                    const toDate =
                        document.getElementById(
                            'financePeriodTo'
                        )?.value ||
                        '';

                    if (
                        !fromDate ||
                        !toDate
                    ) {
                        this.error(
                            'حدد تاريخ البداية وتاريخ النهاية.'
                        );

                        return false;
                    }

                    if (
                        fromDate >
                        toDate
                    ) {
                        this.error(
                            'تاريخ البداية يجب أن يسبق تاريخ النهاية.'
                        );

                        return false;
                    }

                    if (
                        fromDate >
                        today ||
                        toDate >
                        today
                    ) {
                        this.error(
                            'لا يمكن اختيار تاريخ في المستقبل.'
                        );

                        return false;
                    }

                    this.queries.set(
                        'overview',
                        {
                            fromDate,
                            toDate
                        }
                    );

                    this.openTab(
                        'overview',
                        {
                            updateHistory:
                                false
                        }
                    );

                    return true;
                }
            );
        },

        printCurrentTab() {
            if (
                !this.content
                    ?.textContent
                    .trim()
            ) {
                this.error(
                    'لا يوجد محتوى للطباعة.'
                );

                return;
            }

            const clone =
                this.content.cloneNode(
                    true
                );

            clone.querySelectorAll(
                'button, ' +
                '.finance-filter-panel, ' +
                '.finance-pagination, ' +
                '[hidden]'
            ).forEach(
                element => {
                    element.remove();
                }
            );

            this.printWindow(
                this.activeButton?.dataset.title ||
                'التقرير المالي',
                clone.innerHTML
            );
        },

        printBooking(
            button,
            type
        ) {
            const source =
                button
                    .closest(
                        '[data-booking-id], ' +
                        '[data-financial-document]'
                    )
                    ?.querySelector(
                        '[data-open-booking-finance-details], ' +
                        '[data-open-booking-report-details]'
                    ) ||
                button;

            const data =
                source.dataset;

            const titles = {
                invoice:
                    'فاتورة حجز',

                deposit:
                    'سند عربون',

                collection:
                    'سند تحصيل'
            };

            const rows = [
                [
                    'رقم الحجز',
                    data.bookingId ||
                    button.dataset.bookingId
                ],
                [
                    'العميل',
                    data.customer
                ],
                [
                    'المنشأة',
                    data.place
                ],
                [
                    'إجمالي الحجز',
                    data.total
                ],
                [
                    'العربون',
                    data.deposit
                ],
                [
                    'المحصل إلكترونيًا',
                    data.paidOnline ||
                    data.online
                ],
                [
                    'المحصل عند المنشأة',
                    data.propertyCollected ||
                    data.property
                ],
                [
                    'المتبقي',
                    data.propertyRemaining ||
                    data.remaining
                ],
                [
                    'عمولة المنصة',
                    data.commission
                ],
                [
                    'صافي المالك',
                    data.ownerNet
                ]
            ];

            const tableRows =
                rows
                    .filter(
                        ([, value]) =>
                            value
                    )
                    .map(
                        ([label, value]) => `
                            <tr>

                                <th>
                                    ${this.escape(label)}
                                </th>

                                <td>
                                    ${this.escape(value)}
                                </td>

                            </tr>
                        `
                    )
                    .join('');

            this.printWindow(
                titles[type],
                `
                    <table class="print-finance-table">

                        <tbody>
                            ${tableRows}
                        </tbody>

                    </table>
                `
            );
        },

        exportReport() {
            const items = [
                ...document.querySelectorAll(
                    '[data-financial-document]:not([hidden])'
                )
            ];

            if (!items.length) {
                this.error(
                    'لا توجد بيانات للتصدير.'
                );

                return;
            }

            const rows = [
                [
                    'رقم المستند',
                    'رقم الحجز',
                    'العميل',
                    'المنشأة',
                    'النوع'
                ]
            ];

            const seen =
                new Set();

            items.forEach(
                item => {
                    const key =
                        `${item.dataset.documentNumber
                        }|${item.dataset.bookingId
                        }`;

                    if (
                        seen.has(
                            key
                        )
                    ) {
                        return;
                    }

                    seen.add(
                        key
                    );

                    rows.push(
                        [
                            item.dataset.documentNumber ||
                            '',

                            item.dataset.bookingId ||
                            '',

                            item.dataset.customerName ||
                            '',

                            item.dataset.placeName ||
                            '',

                            item.dataset.documentType ||
                            ''
                        ]
                    );
                }
            );

            const csv =
                rows
                    .map(
                        row =>
                            row
                                .map(
                                    value =>
                                        `"${String(value).replace(
                                            /"/g,
                                            '""'
                                        )}"`
                                )
                                .join(
                                    ','
                                )
                    )
                    .join(
                        '\r\n'
                    );

            const objectUrl =
                URL.createObjectURL(
                    new Blob(
                        [
                            '\uFEFF',
                            csv
                        ],
                        {
                            type:
                                'text/csv;charset=utf-8'
                        }
                    )
                );

            const link =
                document.createElement(
                    'a'
                );

            link.href =
                objectUrl;

            link.download =
                `financial-report-${new Date()
                    .toISOString()
                    .slice(
                        0,
                        10
                    )
                }.csv`;

            link.click();

            URL.revokeObjectURL(
                objectUrl
            );
        },

        printWindow(
            title,
            body
        ) {
            const printWindow =
                window.open(
                    '',
                    '_blank',
                    'width=1100,height=820'
                );

            if (!printWindow) {
                this.error(
                    'اسمح بفتح النوافذ المنبثقة للطباعة.'
                );

                return;
            }

            printWindow.document.write(`
                <!doctype html>

                <html lang="ar"
                      dir="rtl">

                <head>

                    <meta charset="utf-8">

                    <title>
                        ${this.escape(title)}
                    </title>

                    <style>

                        * {
                            box-sizing: border-box;
                        }

                        body {
                            font-family: Tajawal, Tahoma, Arial, sans-serif;
                            padding: 28px;
                            color: #172033;
                        }

                        .head {
                            display: flex;
                            justify-content: space-between;
                            border-bottom: 3px solid #05203c;
                            padding-bottom: 16px;
                            margin-bottom: 20px;
                        }

                        table {
                            width: 100%;
                            border-collapse: collapse;
                        }

                        th,
                        td {
                            padding: 12px;
                            border: 1px solid #dbe3ec;
                            text-align: right;
                        }

                        th {
                            width: 40%;
                            background: #f8fafc;
                        }

                        button {
                            display: none !important;
                        }

                        @page {
                            size: A4;
                            margin: 13mm;
                        }

                    </style>

                </head>

                <body>

                    <div class="head">

                        <h1>
                            دليلك اليمني
                        </h1>

                        <h2>
                            ${this.escape(title)}
                        </h2>

                    </div>

                    ${body}

                </body>

                </html>
            `);

            printWindow.document.close();

            setTimeout(
                () => {
                    printWindow.print();
                },
                400
            );
        },

        toggleMenu(
            button,
            parentSelector
        ) {
            const parent =
                button.closest(
                    parentSelector
                );

            if (!parent) {
                return;
            }

            const shouldOpen =
                !parent.classList.contains(
                    'open'
                );

            this.closeMenus();

            parent.classList.toggle(
                'open',
                shouldOpen
            );

            parent.classList.toggle(
                'active',
                shouldOpen
            );
        },

        closeMenus() {
            document.querySelectorAll(
                '.booking-finance-more-menu.open, ' +
                '.booking-finance-more-menu.active, ' +
                '.report-document-more.open, ' +
                '.report-document-more.active'
            ).forEach(
                menu => {
                    menu.classList.remove(
                        'open',
                        'active'
                    );
                }
            );
        },

        async copy(
            text,
            button
        ) {
            if (
                !String(text).trim()
            ) {
                this.error(
                    'لا توجد قيمة للنسخ.'
                );

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
                }
                else {
                    const textarea =
                        document.createElement(
                            'textarea'
                        );

                    textarea.value =
                        text;

                    textarea.style.position =
                        'fixed';

                    textarea.style.opacity =
                        0;

                    document.body.appendChild(
                        textarea
                    );

                    textarea.select();

                    document.execCommand(
                        'copy'
                    );

                    textarea.remove();
                }

                const oldHtml =
                    button.innerHTML;

                button.innerHTML =
                    '<i class="bi bi-check2"></i>';

                setTimeout(
                    () => {
                        button.innerHTML =
                            oldHtml;
                    },
                    1000
                );

                this.toast(
                    'تم النسخ.',
                    'success'
                );
            }
            catch {
                this.error(
                    'تعذر النسخ.'
                );
            }
        },

        async post(
            url,
            formData
        ) {
            if (!url) {
                throw new Error(
                    'رابط العملية غير متوفر.'
                );
            }

            const response =
                await fetch(
                    url,
                    {
                        method:
                            'POST',

                        body:
                            formData,

                        credentials:
                            'same-origin',

                        headers: {
                            'X-Requested-With':
                                'XMLHttpRequest',

                            Accept:
                                'application/json'
                        }
                    }
                );

            const contentType =
                response.headers.get(
                    'content-type'
                ) ||
                '';

            let payload;

            if (
                contentType.includes(
                    'application/json'
                )
            ) {
                payload =
                    await response.json();
            }
            else {
                payload = {
                    success:
                        response.ok,

                    message:
                        await response.text()
                };
            }

            if (
                payload.success ===
                undefined
            ) {
                payload.success =
                    Boolean(
                        payload.isSuccess ??
                        payload.succeeded ??
                        response.ok
                    );
            }

            if (!response.ok) {
                payload.success =
                    false;
            }

            return payload;
        },

        postData(context) {
            const formData =
                new FormData();

            const token =
                context
                    .closest(
                        'form'
                    )
                    ?.querySelector(
                        '[name="__RequestVerificationToken"]'
                    )
                    ?.value ||

                document
                    .querySelector(
                        '#financeAntiForgeryForm ' +
                        '[name="__RequestVerificationToken"]'
                    )
                    ?.value ||

                document
                    .querySelector(
                        '[name="__RequestVerificationToken"]'
                    )
                    ?.value;

            if (token) {
                formData.set(
                    '__RequestVerificationToken',
                    token
                );
            }

            return formData;
        },

        loading(
            button,
            isLoading,
            normalSelector,
            loadingSelector
        ) {
            if (!button) {
                return;
            }

            button.disabled =
                isLoading;

            const normalElement =
                button.querySelector(
                    normalSelector
                );

            const loadingElement =
                button.querySelector(
                    loadingSelector
                );

            if (normalElement) {
                normalElement.hidden =
                    isLoading;
            }

            if (loadingElement) {
                loadingElement.hidden =
                    !isLoading;
            }
        },

        setRefresh(isLoading) {
            const button =
                document.getElementById(
                    'refreshFinancePageBtn'
                );

            if (!button) {
                return;
            }

            button.disabled =
                isLoading;

            button.classList.toggle(
                'is-loading',
                isLoading
            );
        },

        showModal(element) {
            if (!element) {
                return;
            }

            if (
                window.bootstrap
                    ?.Modal
            ) {
                window.bootstrap
                    .Modal
                    .getOrCreateInstance(
                        element
                    )
                    .show();

                return;
            }

            element.style.display =
                'block';

            element.classList.add(
                'show'
            );

            element.removeAttribute(
                'aria-hidden'
            );

            document.body.classList.add(
                'modal-open'
            );
        },

        hideModal(element) {
            if (!element) {
                return;
            }

            if (
                window.bootstrap
                    ?.Modal
            ) {
                window.bootstrap
                    .Modal
                    .getOrCreateInstance(
                        element
                    )
                    .hide();

                return;
            }

            element.style.display =
                'none';

            element.classList.remove(
                'show'
            );

            element.setAttribute(
                'aria-hidden',
                'true'
            );

            document.body.classList.remove(
                'modal-open'
            );
        },

        async confirm(
            title,
            text,
            confirmText
        ) {
            if (
                window.Swal
                    ?.fire
            ) {
                const result =
                    await Swal.fire(
                        {
                            title,
                            text,

                            icon:
                                'warning',

                            showCancelButton:
                                true,

                            confirmButtonText:
                                confirmText,

                            cancelButtonText:
                                'تراجع',

                            reverseButtons:
                                true,

                            confirmButtonColor:
                                '#dc2626',

                            cancelButtonColor:
                                '#64748b'
                        }
                    );

                return result.isConfirmed;
            }

            return this.runtimeConfirm(
                title,
                text,
                confirmText
            );
        },

        async success(message) {
            if (
                window.Swal
                    ?.fire
            ) {
                await Swal.fire(
                    {
                        icon:
                            'success',

                        title:
                            'تمت العملية بنجاح',

                        text:
                            message,

                        confirmButtonText:
                            'حسنًا',

                        confirmButtonColor:
                            '#059669'
                    }
                );

                return;
            }

            this.toast(
                message,
                'success',
                3200
            );
        },

        error(message) {
            if (
                window.Swal
                    ?.fire
            ) {
                Swal.fire(
                    {
                        icon:
                            'error',

                        title:
                            'تعذر تنفيذ العملية',

                        text:
                            message,

                        confirmButtonText:
                            'حسنًا',

                        confirmButtonColor:
                            '#dc2626'
                    }
                );
            }
            else {
                this.toast(
                    message,
                    'error',
                    4500
                );
            }

            return false;
        },

        toast(
            message,
            type = 'info',
            duration = 2500
        ) {
            const host =
                document.getElementById(
                    'financeToastContainer'
                ) ||
                document.body;

            const toast =
                document.createElement(
                    'div'
                );

            toast.className =
                `finance-runtime-toast finance-runtime-toast-${type}`;

            toast.innerHTML = `
                <i class="bi ${type ===
                    'success'
                    ? 'bi-check-circle-fill'
                    : type ===
                        'error'
                        ? 'bi-exclamation-octagon-fill'
                        : 'bi-info-circle-fill'
                }"></i>

                <span>
                    ${this.escape(message)}
                </span>
            `;

            host.appendChild(
                toast
            );

            requestAnimationFrame(
                () => {
                    toast.classList.add(
                        'show'
                    );
                }
            );

            setTimeout(
                () => {
                    toast.classList.remove(
                        'show'
                    );

                    setTimeout(
                        () => {
                            toast.remove();
                        },
                        200
                    );
                },
                duration
            );
        },

        runtimeConfirm(
            title,
            text,
            confirmText
        ) {
            return new Promise(
                resolve => {
                    this.closeRuntimeDialog();

                    const overlay =
                        document.createElement(
                            'div'
                        );

                    overlay.id =
                        'financeRuntimeDialog';

                    overlay.className =
                        'finance-runtime-dialog-overlay show';

                    overlay.innerHTML = `
                        <div class="finance-runtime-dialog"
                             dir="rtl">

                            <i class="bi bi-exclamation-triangle-fill"></i>

                            <h3>
                                ${this.escape(title)}
                            </h3>

                            <p>
                                ${this.escape(text)}
                            </p>

                            <div>

                                <button type="button"
                                        data-cancel>

                                    تراجع

                                </button>

                                <button type="button"
                                        data-confirm>

                                    ${this.escape(confirmText)}

                                </button>

                            </div>

                        </div>
                    `;

                    const finish =
                        value => {
                            overlay.remove();

                            resolve(
                                value
                            );
                        };

                    overlay
                        .querySelector(
                            '[data-cancel]'
                        )
                        .onclick =
                        () => {
                            finish(
                                false
                            );
                        };

                    overlay
                        .querySelector(
                            '[data-confirm]'
                        )
                        .onclick =
                        () => {
                            finish(
                                true
                            );
                        };

                    document.body.appendChild(
                        overlay
                    );
                }
            );
        },

        formDialog(
            title,
            html,
            onConfirm
        ) {
            this.closeRuntimeDialog();

            const overlay =
                document.createElement(
                    'div'
                );

            overlay.id =
                'financeRuntimeDialog';

            overlay.className =
                'finance-runtime-dialog-overlay show';

            overlay.innerHTML = `
                <div class="finance-runtime-dialog finance-runtime-form"
                     dir="rtl">

                    <h3>
                        ${this.escape(title)}
                    </h3>

                    ${html}

                    <div>

                        <button type="button"
                                data-cancel>

                            إلغاء

                        </button>

                        <button type="button"
                                data-confirm>

                            تطبيق

                        </button>

                    </div>

                </div>
            `;

            overlay
                .querySelector(
                    '[data-cancel]'
                )
                .onclick =
                () => {
                    overlay.remove();
                };

            overlay
                .querySelector(
                    '[data-confirm]'
                )
                .onclick =
                () => {
                    if (
                        onConfirm() !==
                        false
                    ) {
                        overlay.remove();
                    }
                };

            document.body.appendChild(
                overlay
            );
        },

        closeRuntimeDialog() {
            document
                .getElementById(
                    'financeRuntimeDialog'
                )
                ?.remove();
        },

        injectRuntimeStyles() {
            if (
                document.getElementById(
                    'financeRuntimeStyles'
                )
            ) {
                return;
            }

            const style =
                document.createElement(
                    'style'
                );

            style.id =
                'financeRuntimeStyles';

            style.textContent = `
                .finance-runtime-toast {
                    display: flex;
                    align-items: center;
                    gap: 10px;
                    padding: 13px 15px;
                    border-radius: 14px;
                    background: #fff;
                    color: #172033;
                    box-shadow: 0 18px 45px rgba(5, 32, 60, .22);
                    opacity: 0;
                    transform: translateY(-8px);
                    transition: .2s;
                    pointer-events: auto;
                }

                .finance-runtime-toast.show {
                    opacity: 1;
                    transform: none;
                }

                .finance-runtime-toast-success i {
                    color: #059669;
                }

                .finance-runtime-toast-error i {
                    color: #dc2626;
                }

                .finance-runtime-dialog-overlay {
                    position: fixed;
                    inset: 0;
                    z-index: 99999;
                    display: grid;
                    place-items: center;
                    padding: 18px;
                    background: rgba(3, 20, 38, .65);
                    backdrop-filter: blur(7px);
                }

                .finance-runtime-dialog {
                    width: min(450px, 100%);
                    padding: 24px;
                    border-radius: 22px;
                    background: #fff;
                    box-shadow: 0 30px 80px rgba(3, 20, 38, .35);
                    text-align: center;
                }

                .finance-runtime-dialog > i {
                    font-size: 2rem;
                    color: #d97706;
                }

                .finance-runtime-dialog h3 {
                    color: #05203c;
                }

                .finance-runtime-dialog p {
                    color: #64748b;
                    line-height: 1.8;
                }

                .finance-runtime-dialog > div:last-child {
                    display: flex;
                    justify-content: center;
                    gap: 9px;
                    margin-top: 18px;
                }

                .finance-runtime-dialog button {
                    min-width: 115px;
                    min-height: 42px;
                    border: 1px solid #e2e8f0;
                    border-radius: 12px;
                    background: #f8fafc;
                    font-weight: 900;
                }

                .finance-runtime-dialog [data-confirm] {
                    border: 0;
                    background: linear-gradient(135deg, #05203c, #008fc2);
                    color: #fff;
                }

                .finance-runtime-form {
                    text-align: right;
                }

                .finance-period-dialog {
                    display: grid;
                    grid-template-columns: 1fr 1fr;
                    gap: 10px;
                }

                .finance-period-dialog span {
                    display: block;
                    margin-bottom: 6px;
                }

                .finance-period-dialog input {
                    width: 100%;
                    min-height: 44px;
                    padding: 9px;
                    border: 1px solid #e2e8f0;
                    border-radius: 11px;
                }

                .finance-drawer-details-grid {
                    display: grid;
                    grid-template-columns: 1fr 1fr;
                    gap: 9px;
                }

                .finance-drawer-detail-row {
                    padding: 12px;
                    border: 1px solid #e2e8f0;
                    border-radius: 13px;
                    background: #f8fafc;
                }

                .finance-drawer-detail-row span {
                    display: block;
                    color: #64748b;
                    font-size: .72rem;
                }

                .finance-drawer-detail-row strong {
                    display: block;
                    margin-top: 4px;
                    color: #05203c;
                }

                .finance-drawer-actions {
                    display: flex;
                    gap: 8px;
                    margin-top: 15px;
                }

                .finance-drawer-actions button {
                    padding: 9px 13px;
                    border: 0;
                    border-radius: 12px;
                    background: linear-gradient(135deg, #05203c, #008fc2);
                    color: #fff;
                    font-weight: 900;
                }

                @media (max-width: 600px) {
                    .finance-period-dialog,
                    .finance-drawer-details-grid {
                        grid-template-columns: 1fr;
                    }
                }
            `;

            document.head.appendChild(
                style
            );
        },

        setDateLimits() {
            const now =
                new Date();

            const localDate =
                new Date(
                    now.getTime() -
                    now.getTimezoneOffset() *
                    60000
                ).toISOString();

            this.content
                .querySelectorAll(
                    'input[type="date"]'
                )
                .forEach(
                    input => {
                        input.max ||=
                            localDate.slice(
                                0,
                                10
                            );
                    }
                );

            this.content
                .querySelectorAll(
                    'input[type="datetime-local"]'
                )
                .forEach(
                    input => {
                        input.max ||=
                            localDate.slice(
                                0,
                                16
                            );
                    }
                );
        },

        appendQuery(
            url,
            values
        ) {
            const parsedUrl =
                new URL(
                    url,
                    window.location.origin
                );

            Object.entries(
                values ||
                {}
            ).forEach(
                ([key, value]) => {
                    if (
                        value !==
                        undefined &&
                        value !==
                        null &&
                        String(value).trim() !==
                        ''
                    ) {
                        parsedUrl.searchParams.set(
                            key,
                            value
                        );
                    }
                }
            );

            return (
                parsedUrl.origin ===
                    window.location.origin
                    ? `${parsedUrl.pathname}${parsedUrl.search}`
                    : parsedUrl.toString()
            );
        },

        safeUrl(value) {
            try {
                const url =
                    new URL(
                        String(value || ''),
                        window.location.origin
                    );

                if (
                    ![
                        'http:',
                        'https:'
                    ].includes(
                        url.protocol
                    )
                ) {
                    return '';
                }

                return (
                    url.origin ===
                        window.location.origin
                        ? `${url.pathname}${url.search}${url.hash}`
                        : url.toString()
                );
            }
            catch {
                return '';
            }
        },

        number(value) {
            const number =
                parseFloat(
                    String(
                        value ??
                        ''
                    )
                        .replace(
                            /[٬,]/g,
                            ''
                        )
                        .replace(
                            /٫/g,
                            '.'
                        )
                        .replace(
                            /[^0-9+\-.]/g,
                            ''
                        )
                );

            return Number.isFinite(
                number
            )
                ? number
                : 0;
        },

        money(value) {
            return `${new Intl.NumberFormat(
                'en-US',
                {
                    maximumFractionDigits: 2
                }
            ).format(
                Number(value) ||
                0
            )
                } ريال يمني`;
        },

        normalize(value) {
            return String(value || '')
                .trim()
                .toLowerCase()
                .normalize(
                    'NFKD'
                )
                .replace(
                    /[أإآ]/g,
                    'ا'
                )
                .replace(
                    /ة/g,
                    'ه'
                )
                .replace(
                    /ى/g,
                    'ي'
                )
                .replace(
                    /[\u064B-\u065F]/g,
                    ''
                );
        },

        fileSize(bytes) {
            if (
                bytes <
                1024
            ) {
                return `${bytes} B`;
            }

            if (
                bytes <
                1024 *
                1024
            ) {
                return `${(
                        bytes /
                        1024
                    ).toFixed(
                        1
                    )
                    } KB`;
            }

            return `${(
                    bytes /
                    1024 /
                    1024
                ).toFixed(
                    1
                )
                } MB`;
        },

        setText(
            selector,
            value
        ) {
            const element =
                document.querySelector(
                    selector
                );

            if (element) {
                element.textContent =
                    value ??
                    '';
            }
        },

        value(
            selector,
            value
        ) {
            const element =
                document.querySelector(
                    selector
                );

            if (element) {
                element.value =
                    value ??
                    '';
            }
        },

        escape(value) {
            return String(
                value ??
                ''
            )
                .replace(
                    /&/g,
                    '&amp;'
                )
                .replace(
                    /</g,
                    '&lt;'
                )
                .replace(
                    />/g,
                    '&gt;'
                )
                .replace(
                    /"/g,
                    '&quot;'
                )
                .replace(
                    /'/g,
                    '&#039;'
                );
        },

        debounce(
            callback,
            delay
        ) {
            let timeoutId;

            return (...args) => {
                clearTimeout(
                    timeoutId
                );

                timeoutId =
                    setTimeout(
                        () => {
                            callback(
                                ...args
                            );
                        },
                        delay
                    );
            };
        }
    };

    const start =
        () => {
            App.init();
        };

    if (
        document.readyState ===
        'loading'
    ) {
        document.addEventListener(
            'DOMContentLoaded',
            start,
            {
                once: true
            }
        );
    }
    else {
        start();
    }

    window.YemenGuideFinancials = {
        openTab:
            (
                tab,
                query = {}
            ) => {
                if (
                    Object.keys(
                        query
                    ).length
                ) {
                    App.queries.set(
                        App.normalizeTab(
                            tab
                        ),
                        query
                    );
                }

                return App.openTab(
                    tab
                );
            },

        refresh:
            () =>
                App.refresh(),

        previewDocument:
            (
                url,
                title
            ) =>
                App.previewDocument(
                    url,
                    title
                ),

        closeDrawer:
            () =>
                App.closeDrawer()
    };
})();