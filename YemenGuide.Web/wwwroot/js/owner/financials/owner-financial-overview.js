(() => {
    'use strict';

    window.OwnerFinancialTabs ??= {};

    const MODULE_KEY = 'overview';

    const Overview = {
        container: null,
        api: null,
        handlers: {},

        init(container, api) {
            this.destroy();

            this.container =
                container?.querySelector('[data-owner-financial-partial="overview"]') || null;
            this.api = api;

            if (!this.container) return;

            this.bindEvents();
            this.syncActivePeriod();
            this.animateMoneyFlow();
        },

        destroy() {
            if (this.container && this.handlers.click) {
                this.container.removeEventListener('click', this.handlers.click);
            }
            if (this.container && this.handlers.submit) {
                this.container.removeEventListener('submit', this.handlers.submit);
            }
            this.container = null;
            this.api = null;
            this.handlers = {};
        },

        bindEvents() {
            this.handlers.click = event => {
                const periodButton = event.target.closest('[data-overview-period]');
                if (!periodButton || !this.container.contains(periodButton)) return;

                event.preventDefault();
                const period = String(periodButton.dataset.overviewPeriod || '').toLowerCase();

                if (period === 'custom') {
                    this.toggleCustom(periodButton);
                    return;
                }

                this.applyPreset(period, periodButton);
            };

            this.handlers.submit = event => {
                const form = event.target.closest('[data-overview-custom-period]');
                if (!form || !this.container.contains(form)) return;

                event.preventDefault();
                this.applyCustom(form);
            };

            this.container.addEventListener('click', this.handlers.click);
            this.container.addEventListener('submit', this.handlers.submit);
        },

        syncActivePeriod() {
            const start = this.container.dataset.periodStart || '';
            const end = this.container.dataset.periodEnd || '';
            const today = this.toDateValue(new Date());
            const weekStartDate = new Date();
            weekStartDate.setDate(weekStartDate.getDate() - 6);
            const weekStart = this.toDateValue(weekStartDate);
            const monthDate = new Date();
            monthDate.setDate(1);
            const monthStart = this.toDateValue(monthDate);

            let key = 'custom';
            if (start === today && end === today) key = 'today';
            else if (start === weekStart && end === today) key = 'week';
            else if (start === monthStart && end === today) key = 'month';

            this.setActivePeriod(key);
        },

        setActivePeriod(key) {
            this.container.querySelectorAll('[data-overview-period]').forEach(button => {
                button.classList.toggle('is-active', button.dataset.overviewPeriod === key);
                button.setAttribute(
                    'aria-pressed',
                    button.dataset.overviewPeriod === key ? 'true' : 'false'
                );
            });
        },

        toggleCustom(button) {
            const form = this.container.querySelector('[data-overview-custom-period]');
            if (!form) return;

            const willOpen = form.hidden;
            form.hidden = !willOpen;
            button.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
            this.setActivePeriod('custom');

            if (willOpen) {
                window.setTimeout(() => form.querySelector('[name="fromDate"]')?.focus(), 30);
            }
        },

        applyPreset(period, button) {
            const today = new Date();
            let start = new Date(today);

            if (period === 'week') {
                start.setDate(today.getDate() - 6);
            } else if (period === 'month') {
                start.setDate(1);
            } else if (period !== 'today') {
                return;
            }

            this.setActivePeriod(period);
            this.pulseButton(button);

            const query = new URLSearchParams();
            query.set('fromDate', this.toDateValue(start));
            query.set('toDate', this.toDateValue(today));

            this.api?.reload({
                query,
                history: 'replace',
                keepScroll: true
            });
        },

        applyCustom(form) {
            const from = form.querySelector('[name="fromDate"]')?.value || '';
            const to = form.querySelector('[name="toDate"]')?.value || '';
            const today = this.toDateValue(new Date());

            if (!from || !to) {
                this.api?.showToast('حدد تاريخ البداية والنهاية.', 'warning');
                return;
            }
            if (from > to) {
                this.api?.showToast('تاريخ البداية يجب أن يسبق تاريخ النهاية.', 'warning');
                return;
            }
            if (from > today || to > today) {
                this.api?.showToast('لا يمكن اختيار تاريخ في المستقبل.', 'warning');
                return;
            }

            const query = new URLSearchParams();
            query.set('fromDate', from);
            query.set('toDate', to);
            this.setActivePeriod('custom');

            this.api?.reload({ query, history: 'replace', keepScroll: true });
        },

        animateMoneyFlow() {
            const track = this.container.querySelector('.owner-financial-overview__flow-rail > span');
            if (!track || window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches) return;

            const target = getComputedStyle(track).getPropertyValue('--overview-collection').trim();
            if (!target) return;

            track.style.setProperty('--overview-collection', '0%');
            requestAnimationFrame(() => {
                requestAnimationFrame(() => track.style.setProperty('--overview-collection', target));
            });
        },

        pulseButton(button) {
            if (!button || typeof button.animate !== 'function') return;
            button.animate(
                [
                    { transform: 'scale(1)' },
                    { transform: 'scale(.96)' },
                    { transform: 'scale(1)' }
                ],
                { duration: 220, easing: 'ease-out' }
            );
        },

        toDateValue(date) {
            const pad = value => String(value).padStart(2, '0');
            return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
        }
    };

    window.OwnerFinancialTabs[MODULE_KEY] = Overview;
})();
