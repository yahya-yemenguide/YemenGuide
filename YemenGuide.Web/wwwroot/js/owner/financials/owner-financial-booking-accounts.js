(() => {
    'use strict';

    window.OwnerFinancialTabs ??= {};

    const MODULE_KEY = 'bookings';
    const DEFAULT_PAGE_SIZE = '20';

    const BookingAccounts = {
        container: null,
        root: null,
        api: null,
        dialog: null,
        previousFocus: null,
        observer: null,
        scrollTimer: 0,
        handlers: {},

        init(container, api) {
            this.destroy();

            this.container = container || null;
            this.root = container?.querySelector('[data-owner-financial-partial="bookings"]') || null;
            this.api = api || null;
            this.dialog = this.root?.querySelector('#ownerBookingAccountDetailsDialog') || null;

            if (!this.root) return;

            this.bindEvents();
            this.prepareCards();
            this.animateCollectionRails();
        },

        destroy() {
            if (this.root && this.handlers.click) this.root.removeEventListener('click', this.handlers.click);
            if (this.root && this.handlers.submit) this.root.removeEventListener('submit', this.handlers.submit);
            if (this.root && this.handlers.change) this.root.removeEventListener('change', this.handlers.change);
            if (this.dialog && this.handlers.cancel) this.dialog.removeEventListener('cancel', this.handlers.cancel);
            if (this.dialog && this.handlers.dialogClick) this.dialog.removeEventListener('click', this.handlers.dialogClick);
            const scrollHost = this.dialog?.querySelector('[data-booking-dialog-scroll]');
            if (scrollHost && this.handlers.dialogScroll) {
                scrollHost.removeEventListener('scroll', this.handlers.dialogScroll);
            }
            window.clearTimeout(this.scrollTimer);
            this.scrollTimer = 0;

            this.observer?.disconnect();
            this.closeDialog(false);

            this.container = null;
            this.root = null;
            this.api = null;
            this.dialog = null;
            this.previousFocus = null;
            this.observer = null;
            this.handlers = {};
        },

        bindEvents() {
            this.handlers.click = event => {
                const clearSearch = event.target.closest('[data-booking-clear-search]');
                if (clearSearch && this.root.contains(clearSearch)) {
                    event.preventDefault();
                    const form = clearSearch.closest('form');
                    const search = form?.querySelector('[name="Search"]');
                    if (search) search.value = '';
                    this.submitForm(form);
                    return;
                }

                const advanced = event.target.closest('[data-booking-advanced-toggle]');
                if (advanced && this.root.contains(advanced)) {
                    event.preventDefault();
                    this.toggleAdvanced(advanced);
                    return;
                }

                const reset = event.target.closest('[data-booking-accounts-reset]');
                if (reset && this.root.contains(reset)) {
                    event.preventDefault();
                    this.resetFilters();
                    return;
                }

                const page = event.target.closest('[data-booking-accounts-page]');
                if (page && this.root.contains(page)) {
                    event.preventDefault();
                    if (page.disabled) return;
                    const number = Number(page.dataset.bookingAccountsPage || 0);
                    if (Number.isFinite(number) && number > 0) this.goToPage(number);
                    return;
                }

                const details = event.target.closest('[data-booking-account-details]');
                if (details && this.root.contains(details)) {
                    event.preventDefault();
                    this.openDetails(details.dataset.bookingId, details);
                    return;
                }

                const close = event.target.closest('[data-booking-account-dialog-close]');
                if (close && this.root.contains(close)) {
                    event.preventDefault();
                    this.closeDialog();
                }
            };

            this.handlers.submit = event => {
                const form = event.target.closest('#ownerBookingAccountsFilterForm');
                if (!form || !this.root.contains(form)) return;
                event.preventDefault();
                this.submitForm(form);
            };

            this.handlers.change = event => {
                const select = event.target.closest('[name="PaymentStatus"], [name="CommissionStatus"]');
                if (!select || !this.root.contains(select)) return;
                // الفلاتر السريعة تطبق فورًا بدون زر إضافي.
                this.submitForm(select.closest('form'));
            };

            this.handlers.cancel = event => {
                event.preventDefault();
                this.closeDialog();
            };

            this.handlers.dialogClick = event => {
                if (event.target === this.dialog) this.closeDialog();
            };

            this.handlers.dialogScroll = () => {
                if (!this.dialog) return;

                this.dialog.classList.add('is-scrolling');

                window.clearTimeout(this.scrollTimer);
                this.scrollTimer = window.setTimeout(() => {
                    this.dialog?.classList.remove('is-scrolling');
                }, 110);
            };

            this.root.addEventListener('click', this.handlers.click);
            this.root.addEventListener('submit', this.handlers.submit);
            this.root.addEventListener('change', this.handlers.change);
            this.dialog?.addEventListener('cancel', this.handlers.cancel);
            this.dialog?.addEventListener('click', this.handlers.dialogClick);

            const scrollHost = this.dialog?.querySelector('[data-booking-dialog-scroll]');
            scrollHost?.addEventListener('scroll', this.handlers.dialogScroll, { passive: true });
        },

        toggleAdvanced(button) {
            const panel = this.root.querySelector('[data-booking-advanced-panel]');
            if (!panel) return;

            const willOpen = panel.hidden;
            panel.hidden = !willOpen;
            button.setAttribute('aria-expanded', willOpen ? 'true' : 'false');

            if (willOpen) {
                panel.animate?.(
                    [
                        { opacity: 0, transform: 'translateY(-5px)' },
                        { opacity: 1, transform: 'translateY(0)' }
                    ],
                    { duration: 220, easing: 'cubic-bezier(.22,.78,.24,1)' }
                );
            }
        },

        submitForm(form) {
            if (!form) return;

            const from = form.querySelector('[name="FromDate"]')?.value || '';
            const to = form.querySelector('[name="ToDate"]')?.value || '';

            if (from && to && from > to) {
                this.toast('تاريخ البداية يجب أن يسبق تاريخ النهاية.', 'warning');
                return;
            }

            const pageInput = form.querySelector('[data-booking-accounts-page-input]');
            if (pageInput) pageInput.value = '1';

            const query = this.serializeForm(form);
            this.reload(query);
        },

        resetFilters() {
            const form = this.root.querySelector('#ownerBookingAccountsFilterForm');
            if (!form) return;

            const placeId = form.querySelector('[name="PlaceId"]')?.value || '';
            const query = new URLSearchParams();
            if (placeId) query.set('PlaceId', placeId);
            query.set('PageNumber', '1');
            query.set('PageSize', DEFAULT_PAGE_SIZE);

            this.reload(query);
        },

        goToPage(pageNumber) {
            const form = this.root.querySelector('#ownerBookingAccountsFilterForm');
            if (!form) return;

            const query = this.serializeForm(form);
            query.set('PageNumber', String(pageNumber));
            this.reload(query, true);
        },

        serializeForm(form) {
            if (this.api && typeof this.api.serializeForm === 'function') {
                return this.api.serializeForm(form);
            }

            const params = new URLSearchParams();
            new FormData(form).forEach((value, key) => {
                const text = String(value ?? '').trim();
                if (text) params.set(key, text);
            });
            return params;
        },

        async reload(query, keepScroll = true) {
            if (this.api && typeof this.api.reload === 'function') {
                await this.api.reload({
                    query,
                    history: 'replace',
                    keepScroll
                });
                return;
            }

            // Fallback آمن لو شُغّل الملف منفردًا خارج shell.
            const url = new URL(this.root.dataset.loadUrl || '/Owner/Financials/BookingAccounts', window.location.origin);
            query.forEach((value, key) => url.searchParams.set(key, value));

            try {
                this.root.setAttribute('aria-busy', 'true');
                this.root.style.opacity = '.55';
                this.root.style.pointerEvents = 'none';

                const response = await fetch(url, {
                    credentials: 'same-origin',
                    cache: 'no-store',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Accept': 'text/html'
                    }
                });

                if (!response.ok) throw new Error(await response.text() || 'تعذر تحميل حسابات الحجوزات.');
                const html = await response.text();
                const host = this.container || this.root.parentElement;
                if (!host) return;
                host.innerHTML = html;
                this.init(host, this.api);
            } catch (error) {
                this.toast(error?.message || 'تعذر تحديث حسابات الحجوزات.', 'error');
                this.root.style.opacity = '';
                this.root.style.pointerEvents = '';
                this.root.setAttribute('aria-busy', 'false');
            }
        },

        openDetails(bookingId, trigger) {
            if (!this.dialog || !bookingId) return;

            const template = [...this.root.querySelectorAll('[data-booking-details-template]')]
                .find(item => String(item.dataset.bookingId || '') === String(bookingId));
            const body = this.dialog.querySelector('[data-booking-dialog-content]');

            if (!template || !body) {
                this.toast('تعذر قراءة تفاصيل هذا الحساب.', 'error');
                return;
            }

            this.previousFocus = trigger instanceof HTMLElement ? trigger : null;
            body.replaceChildren(template.content.cloneNode(true));

            if (typeof this.dialog.showModal === 'function') {
                if (!this.dialog.open) this.dialog.showModal();
            } else {
                this.dialog.setAttribute('open', '');
            }

            document.documentElement.classList.add('owner-financial-dialog-open');

            const scrollHost = this.dialog.querySelector('[data-booking-dialog-scroll]');
            if (scrollHost) {
                scrollHost.scrollTop = 0;
            }

            window.setTimeout(
                () => this.dialog.querySelector('[data-booking-account-dialog-close]')?.focus(),
                30
            );
        },

        closeDialog(restoreFocus = true) {
            if (!this.dialog) return;

            if (typeof this.dialog.close === 'function' && this.dialog.open) this.dialog.close();
            else this.dialog.removeAttribute('open');

            document.documentElement.classList.remove('owner-financial-dialog-open');
            this.dialog.classList.remove('is-scrolling');
            window.clearTimeout(this.scrollTimer);
            this.scrollTimer = 0;

            if (restoreFocus && this.previousFocus instanceof HTMLElement) {
                this.previousFocus.focus({ preventScroll: true });
            }
            this.previousFocus = null;
        },

        prepareCards() {
            const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches === true;
            const cards = [...this.root.querySelectorAll('[data-booking-card]')];

            if (!reduceMotion && 'IntersectionObserver' in window) {
                this.observer = new IntersectionObserver(entries => {
                    entries.forEach(entry => {
                        if (!entry.isIntersecting) return;
                        const card = entry.target;
                        const index = Number(card.dataset.bookingRevealIndex || 0);
                        card.animate?.(
                            [
                                { opacity: 0, transform: 'translateY(13px) scale(.992)' },
                                { opacity: 1, transform: 'translateY(0) scale(1)' }
                            ],
                            {
                                duration: 420,
                                delay: Math.min(index * 38, 250),
                                easing: 'cubic-bezier(.22,.78,.24,1)',
                                fill: 'both'
                            }
                        );
                        this.observer.unobserve(card);
                    });
                }, { threshold: .08 });

                cards.forEach((card, index) => {
                    card.dataset.bookingRevealIndex = String(index);
                    this.observer.observe(card);
                });
            }

            if (reduceMotion) return;

            cards.forEach(card => {
                let frame = 0;

                const reset = () => {
                    cancelAnimationFrame(frame);
                    card.style.transform = '';
                    card.style.removeProperty('--mx');
                    card.style.removeProperty('--my');
                };

                card.addEventListener('pointermove', event => {
                    if (event.pointerType === 'touch' || window.innerWidth < 900) return;

                    cancelAnimationFrame(frame);
                    frame = requestAnimationFrame(() => {
                        const rect = card.getBoundingClientRect();
                        const x = (event.clientX - rect.left) / Math.max(1, rect.width);
                        const y = (event.clientY - rect.top) / Math.max(1, rect.height);
                        const rotateY = (x - .5) * 2.4;
                        const rotateX = (.5 - y) * 1.7;

                        card.style.setProperty('--mx', `${Math.round(x * 100)}%`);
                        card.style.setProperty('--my', `${Math.round(y * 100)}%`);
                        card.style.transform = `perspective(1250px) rotateX(${rotateX.toFixed(2)}deg) rotateY(${rotateY.toFixed(2)}deg) translateY(-1px)`;
                    });
                }, { passive: true });

                card.addEventListener('pointerleave', reset, { passive: true });
                card.addEventListener('pointercancel', reset, { passive: true });
            });
        },

        animateCollectionRails() {
            if (window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches) return;

            this.root.querySelectorAll('.owner-booking-dossier__collection-rail span').forEach((rail, index) => {
                const target = getComputedStyle(rail).getPropertyValue('--booking-rate').trim();
                if (!target) return;
                rail.style.setProperty('--booking-rate', '0%');
                window.setTimeout(() => rail.style.setProperty('--booking-rate', target), 100 + (index * 45));
            });
        },

        toast(message, type = 'info') {
            if (this.api && typeof this.api.showToast === 'function') {
                this.api.showToast(message, type);
                return;
            }
            console[type === 'error' ? 'error' : 'log'](message);
        }
    };

    window.OwnerFinancialTabs[MODULE_KEY] = BookingAccounts;
})();
