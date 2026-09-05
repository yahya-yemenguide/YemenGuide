(function () {
    'use strict';

    const root = document.getElementById('ownerNotificationCenterPage');
    if (!root) return;

    const list = document.getElementById('ownerNotificationCenterList');
    const panel = document.getElementById('ownerNotificationCenterPanel');
    const loading = document.getElementById('ownerNotificationCenterLoading');
    const pagination = document.getElementById('ownerNotificationCenterPagination');
    const markAll = document.getElementById('ownerNotificationCenterMarkAll');
    const sectionTitle = document.getElementById('ownerNotificationCenterSectionTitle');
    const resultCount = document.getElementById('ownerNotificationCenterResultCount');
    const tabs = Array.from(root.querySelectorAll('[data-owner-notification-tab]'));
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const endpoint = root.dataset.centerDataEndpoint || '/Owner/Notifications/Data';
    const markAllEndpoint = root.dataset.markAllEndpoint || '/Notifications/MarkAllAsRead';
    const pageSize = positiveInt(root.dataset.pageSize) || 12;

    let currentFilter = normalizeFilter(root.dataset.currentFilter) || 'all';
    let currentPage = positiveInt(root.dataset.currentPage) || 1;
    let controller = null;

    const presentation = {
        bookingcreated: ['bi-calendar-plus-fill', 'cyan', 'حجز'],
        bookingaccepted: ['bi-calendar-check-fill', 'green', 'حجز'],
        bookingconfirmed: ['bi-patch-check-fill', 'green', 'حجز'],
        bookingrejected: ['bi-calendar-x-fill', 'coral', 'حجز'],
        bookingcancelled: ['bi-x-octagon-fill', 'coral', 'حجز'],
        bookingexpired: ['bi-hourglass-split', 'amber', 'حجز'],
        paymentrequired: ['bi-wallet2', 'amber', 'دفع'],
        paymentreceived: ['bi-receipt-cutoff', 'cyan', 'دفع'],
        paymentapproved: ['bi-check-circle-fill', 'green', 'دفع'],
        paymentrejected: ['bi-exclamation-circle-fill', 'coral', 'دفع'],
        refundrequested: ['bi-arrow-counterclockwise', 'amber', 'استرداد'],
        refundreadyfortransfer: ['bi-lightning-charge-fill', 'green', 'استرداد'],
        refundtransferred: ['bi-send-check-fill', 'cyan', 'استرداد'],
        refundcompleted: ['bi-cash-coin', 'green', 'استرداد'],
        reviewcreated: ['bi-star-fill', 'violet', 'تقييم'],
        reviewreply: ['bi-chat-square-quote-fill', 'violet', 'تقييم'],
        message: ['bi-chat-dots-fill', 'cyan', 'رسالة'],
        chatmessage: ['bi-chat-dots-fill', 'cyan', 'رسالة'],
        reportcreated: ['bi-shield-exclamation', 'amber', 'بلاغ'],
        reportreply: ['bi-shield-check', 'cyan', 'بلاغ'],
        reportescalated: ['bi-arrow-up-circle-fill', 'amber', 'بلاغ'],
        reportwithdrawn: ['bi-arrow-return-left', 'cyan', 'بلاغ'],
        reportresolved: ['bi-check-circle-fill', 'green', 'بلاغ'],
        reportrejected: ['bi-shield-x', 'coral', 'بلاغ'],
        advertisement: ['bi-megaphone-fill', 'orange', 'إعلان'],
        promotion: ['bi-percent', 'orange', 'عرض'],
        account: ['bi-person-check-fill', 'cyan', 'الحساب'],
        security: ['bi-shield-lock-fill', 'amber', 'أمان'],
        general: ['bi-bell-fill', 'cyan', 'إشعار']
    };

    function normalizeFilter(value) {
        const key = String(value || '').trim().toLowerCase();
        return ['all', 'unread', 'booking', 'payment', 'review', 'message', 'report'].includes(key) ? key : 'all';
    }

    function positiveInt(value) {
        const n = Number.parseInt(String(value || ''), 10);
        return Number.isFinite(n) && n > 0 ? n : 0;
    }

    function numberOf(data, camel, pascal) {
        const n = Number(data?.[camel] ?? data?.[pascal] ?? 0);
        return Number.isFinite(n) && n >= 0 ? Math.trunc(n) : 0;
    }

    function arrayOf(data, camel, pascal) {
        const value = data?.[camel] ?? data?.[pascal];
        return Array.isArray(value) ? value : [];
    }

    function valueOf(item, camel, pascal, fallback = '') {
        return item?.[camel] ?? item?.[pascal] ?? fallback;
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function safeLocalUrl(value, id) {
        const url = String(value || '').trim();
        if (url.startsWith('/') && !url.startsWith('//')) return url;
        return `/Notifications/Open/${positiveInt(id)}`;
    }

    function presentationFor(type) {
        return presentation[String(type || 'general').trim().toLowerCase()] || presentation.general;
    }

    function formatDate(value) {
        if (!value) return 'الآن';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return 'الآن';
        try {
            return new Intl.DateTimeFormat('ar-YE-u-nu-latn', {
                year: 'numeric', month: '2-digit', day: '2-digit',
                hour: '2-digit', minute: '2-digit'
            }).format(date);
        } catch {
            return date.toLocaleString();
        }
    }

    function sectionTitleFor(filter) {
        return {
            all: 'جميع الإشعارات',
            unread: 'غير المقروءة',
            booking: 'الحجوزات',
            payment: 'الدفع والاسترداد',
            review: 'التقييمات',
            message: 'الرسائل',
            report: 'البلاغات والقضايا'
        }[filter] || 'جميع الإشعارات';
    }

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element) element.textContent = String(value);
    }

    function updateCounts(data) {
        const total = numberOf(data, 'totalCount', 'TotalCount');
        const unread = numberOf(data, 'unreadCount', 'UnreadCount');
        setText('ownerNotificationCenterTotalHero', total);
        setText('ownerNotificationCenterAllCount', total);
        setText('ownerNotificationCenterUnreadHero', unread);
        setText('ownerNotificationCenterUnreadTab', unread);
        setText('ownerNotificationCenterBookingCount', numberOf(data, 'bookingCount', 'BookingCount'));
        setText('ownerNotificationCenterPaymentCount', numberOf(data, 'paymentCount', 'PaymentCount'));
        setText('ownerNotificationCenterReviewCount', numberOf(data, 'reviewCount', 'ReviewCount'));
        setText('ownerNotificationCenterMessageCount', numberOf(data, 'messageCount', 'MessageCount'));
        setText('ownerNotificationCenterReportCount', numberOf(data, 'reportCount', 'ReportCount'));

        if (markAll) {
            markAll.dataset.unreadCount = String(unread);
            markAll.disabled = unread <= 0;
            markAll.classList.toggle('is-disabled', unread <= 0);
        }

        if (window.OwnerNotifications?.setUnreadCount) {
            window.OwnerNotifications.setUnreadCount(unread);
        }
    }

    function setActiveTab(filter) {
        tabs.forEach(tab => {
            const active = normalizeFilter(tab.dataset.filter) === filter;
            tab.classList.toggle('active', active);
            tab.setAttribute('aria-current', active ? 'page' : 'false');
        });
    }

    function renderItems(items) {
        if (!list) return;
        if (!items.length) {
            list.innerHTML = `
                <div class="owner-notification-center-empty">
                    <span><i class="bi bi-bell-slash"></i></span>
                    <h3>لا توجد إشعارات في هذا القسم</h3>
                    <p>كل شيء هادئ الآن. جرّب قسمًا آخر أو اعرض جميع الإشعارات.</p>
                    ${currentFilter !== 'all' ? '<button type="button" data-owner-empty-all><i class="bi bi-grid-fill"></i>عرض الكل</button>' : ''}
                </div>`;
            return;
        }

        list.innerHTML = items.map((item, index) => {
            const id = positiveInt(valueOf(item, 'notificationId', 'NotificationId'));
            const title = valueOf(item, 'notificationTitle', 'NotificationTitle', 'إشعار');
            const text = valueOf(item, 'notificationText', 'NotificationText', '');
            const type = valueOf(item, 'notificationType', 'NotificationType', 'General');
            const isRead = Boolean(valueOf(item, 'isRead', 'IsRead', false));
            const createdAt = valueOf(item, 'createdAt', 'CreatedAt', null);
            const openUrl = safeLocalUrl(valueOf(item, 'openUrl', 'OpenUrl', ''), id);
            const [icon, tone, label] = presentationFor(type);
            return `
                <a href="${escapeHtml(openUrl)}"
                   class="owner-notification-center-item ${isRead ? '' : 'is-unread'}"
                   data-owner-notification-center-item
                   data-notification-id="${id}"
                   data-tone="${tone}"
                   style="--onc-delay:${Math.min(index * 34, 238)}ms">
                    <span class="owner-notification-center-item__icon"><i class="bi ${icon}"></i></span>
                    <span class="owner-notification-center-item__content">
                        <span class="owner-notification-center-item__top">
                            <span class="owner-notification-center-item__title-wrap">
                                <strong>${escapeHtml(title)}</strong>
                                ${isRead ? '' : '<span class="owner-notification-center-item__dot" aria-label="غير مقروء"></span>'}
                            </span>
                            <span class="owner-notification-center-item__type">${escapeHtml(label)}</span>
                        </span>
                        <span class="owner-notification-center-item__text">${escapeHtml(text)}</span>
                        <span class="owner-notification-center-item__meta"><i class="bi bi-clock"></i>${escapeHtml(formatDate(createdAt))}</span>
                    </span>
                    <span class="owner-notification-center-item__arrow"><i class="bi bi-chevron-left"></i></span>
                </a>`;
        }).join('');
    }

    function renderPagination(data) {
        if (!pagination) return;
        const page = Math.max(1, numberOf(data, 'currentPage', 'CurrentPage') || 1);
        const totalPages = numberOf(data, 'totalPages', 'TotalPages');
        currentPage = page;

        if (totalPages <= 1) {
            pagination.hidden = true;
            pagination.innerHTML = '';
            return;
        }

        const parts = [];
        for (let i = 1; i <= totalPages; i += 1) {
            if (i === 1 || i === totalPages || Math.abs(i - page) <= 1) {
                parts.push(`<button type="button" class="${i === page ? 'active' : ''}" data-owner-notification-page="${i}">${i}</button>`);
            } else if (i === page - 2 || i === page + 2) {
                parts.push('<span>…</span>');
            }
        }

        pagination.hidden = false;
        pagination.innerHTML = `
            <button type="button" class="owner-notification-center-pagination__arrow" data-owner-notification-page="${page - 1}" ${page <= 1 ? 'disabled' : ''}><i class="bi bi-chevron-right"></i></button>
            <div class="owner-notification-center-pagination__pages">${parts.join('')}</div>
            <button type="button" class="owner-notification-center-pagination__arrow" data-owner-notification-page="${page + 1}" ${page >= totalPages ? 'disabled' : ''}><i class="bi bi-chevron-left"></i></button>`;
    }

    function setLoading(state) {
        if (loading) loading.hidden = !state;
        if (panel) panel.classList.toggle('is-loading', state);
    }

    function updateAddress(filter, page, mode) {
        const url = new URL(window.location.href);
        url.pathname = '/Owner/Notifications';
        url.searchParams.set('filter', filter);
        url.searchParams.set('page', String(page));
        url.searchParams.set('pageSize', String(pageSize));
        url.searchParams.delete('focus');
        const method = mode === 'replace' ? 'replaceState' : 'pushState';
        window.history[method]({ ownerNotificationCenter: true, filter, page }, '', url);
    }

    async function load(filter, page, options = {}) {
        filter = normalizeFilter(filter);
        page = Math.max(1, positiveInt(page) || 1);
        controller?.abort();
        controller = new AbortController();
        setLoading(true);

        try {
            const url = new URL(endpoint, window.location.origin);
            url.searchParams.set('filter', filter);
            url.searchParams.set('page', String(page));
            url.searchParams.set('pageSize', String(pageSize));

            const response = await fetch(url, {
                method: 'GET', credentials: 'same-origin', cache: 'no-store',
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                signal: controller.signal
            });
            if (!response.ok) throw new Error('تعذر تحميل الإشعارات.');
            const data = await response.json();

            currentFilter = normalizeFilter(valueOf(data, 'filter', 'Filter', filter));
            currentPage = numberOf(data, 'currentPage', 'CurrentPage') || page;
            root.dataset.currentFilter = currentFilter;
            root.dataset.currentPage = String(currentPage);

            setActiveTab(currentFilter);
            updateCounts(data);
            renderItems(arrayOf(data, 'items', 'Items'));
            renderPagination(data);
            if (sectionTitle) sectionTitle.textContent = sectionTitleFor(currentFilter);
            if (resultCount) resultCount.innerHTML = `<i class="bi bi-collection-fill"></i>${numberOf(data, 'filteredCount', 'FilteredCount')} نتيجة`;

            if (options.history !== false) updateAddress(currentFilter, currentPage, options.replace ? 'replace' : 'push');
            if (options.scroll !== false) panel?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        } catch (error) {
            if (error?.name === 'AbortError') return;
            if (list) {
                list.innerHTML = `<div class="owner-notification-center-error"><i class="bi bi-wifi-off"></i><strong>تعذر تحديث الإشعارات</strong><span>${escapeHtml(error?.message || 'حاول مرة أخرى.')}</span><button type="button" data-owner-retry>إعادة المحاولة</button></div>`;
            }
        } finally {
            setLoading(false);
        }
    }

    async function markAllRead() {
        if (!markAll || markAll.disabled) return;
        markAll.disabled = true;
        markAll.classList.add('is-busy');
        try {
            const body = new FormData();
            if (token) body.append('__RequestVerificationToken', token);
            const response = await fetch(markAllEndpoint, {
                method: 'POST', credentials: 'same-origin', cache: 'no-store',
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }, body
            });
            if (!response.ok) throw new Error('تعذر تعليم الإشعارات كمقروءة.');
            if (window.OwnerNotifications?.setUnreadCount) window.OwnerNotifications.setUnreadCount(0);
            await load(currentFilter, currentFilter === 'unread' ? 1 : currentPage, { replace: true, scroll: false });
        } catch (error) {
            window.OwnerNotifications?.toast?.(error?.message || 'تعذر تنفيذ العملية.', 'error');
        } finally {
            markAll.classList.remove('is-busy');
        }
    }

    root.addEventListener('click', event => {
        const tab = event.target.closest('[data-owner-notification-tab]');
        if (tab && root.contains(tab)) {
            event.preventDefault();
            load(normalizeFilter(tab.dataset.filter), 1);
            return;
        }
        const pageButton = event.target.closest('[data-owner-notification-page]');
        if (pageButton && !pageButton.disabled) {
            event.preventDefault();
            load(currentFilter, positiveInt(pageButton.dataset.ownerNotificationPage));
            return;
        }
        if (event.target.closest('[data-owner-empty-all]')) {
            event.preventDefault();
            load('all', 1);
            return;
        }
        if (event.target.closest('[data-owner-retry]')) {
            event.preventDefault();
            load(currentFilter, currentPage, { history: false, scroll: false });
        }
    });

    markAll?.addEventListener('click', event => {
        event.preventDefault();
        markAllRead();
    });

    window.addEventListener('popstate', () => {
        const url = new URL(window.location.href);
        load(normalizeFilter(url.searchParams.get('filter')), positiveInt(url.searchParams.get('page')) || 1, { history: false, scroll: false });
    });

    if (markAll) {
        const initialUnread = Number.parseInt(markAll.dataset.unreadCount || '0', 10) || 0;
        markAll.disabled = initialUnread <= 0;
        markAll.classList.toggle('is-disabled', initialUnread <= 0);
    }

    const focused = root.querySelector('.owner-notification-center-item.is-focused');
    if (focused) {
        window.setTimeout(() => focused.scrollIntoView({ behavior: 'smooth', block: 'center' }), 180);
    }
}());
