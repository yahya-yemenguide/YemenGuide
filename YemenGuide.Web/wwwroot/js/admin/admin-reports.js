/* =============================================================
   Admin Reports — Rebuild V2
   Center + Details, بدون أي منطق موظفين أو إسناد قضايا.
   ============================================================= */
(() => {
    'use strict';

    const qs = (selector, root = document) => root.querySelector(selector);
    const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));

    const escapeHtml = (value) => String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');

    const encodeAttr = escapeHtml;

    const debounce = (fn, wait = 350) => {
        let timer;
        return (...args) => {
            window.clearTimeout(timer);
            timer = window.setTimeout(() => fn(...args), wait);
        };
    };

    const formatDateTime = (value) => {
        if (!value) return '—';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value);
        return new Intl.DateTimeFormat('ar-YE', {
            year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit'
        }).format(date);
    };

    const statusTone = (status) => {
        switch (String(status || '').toLowerCase()) {
            case 'pending': return 'new';
            case 'inreview': return 'review';
            case 'waitingforreporter':
            case 'waitingforreportedparty': return 'waiting';
            case 'escalated': return 'escalated';
            case 'resolved': return 'resolved';
            case 'rejected': return 'rejected';
            case 'closed': return 'closed';
            case 'withdrawn': return 'withdrawn';
            default: return 'neutral';
        }
    };

    const priorityTone = (priority) => {
        switch (String(priority || '').toLowerCase()) {
            case 'urgent': return 'urgent';
            case 'high': return 'high';
            case 'low': return 'low';
            default: return 'normal';
        }
    };

    const roleIcon = (role) =>
        String(role || '').toLowerCase() === 'owner' ? 'bi-building-fill' : 'bi-person-fill';

    /* =========================================================
       مركز البلاغات
       ========================================================= */

    function initReportsCenter() {
        const root = qs('#adminReportsCenter');
        if (!root) return;

        const listUrl = root.dataset.listUrl;
        const summaryUrl = root.dataset.summaryUrl;
        const detailsUrlBase = root.dataset.detailsUrlBase || '/Admin/Reports/Details/';
        const list = qs('#reportsList', root);
        const loading = qs('#reportsLoading', root);
        const pagination = qs('#reportsPagination', root);
        const resultCount = qs('#reportsResultCount', root);
        const sectionTitle = qs('#reportsSectionTitle', root);
        const scopeTitle = qs('#reportsScopeTitle', root);
        const searchInput = qs('#reportsSearch', root);
        const filterPanel = qs('#reportsFiltersPanel', root);
        const filterToggle = qs('#reportsFilterToggle', root);
        const activeFilterCountBadge = qs('[data-active-filter-count]', root);

        const state = {
            tab: root.dataset.activeTab || 'all',
            scope: root.dataset.activeScope || 'all',
            page: Number(root.dataset.currentPage || 1),
            pageSize: Number(root.dataset.pageSize || 15),
            search: searchInput?.value || '',
            status: qs('#filterStatus', root)?.value || '',
            reportType: qs('#filterType', root)?.value || '',
            priority: qs('#filterPriority', root)?.value || '',
            dateFrom: qs('#filterDateFrom', root)?.value || '',
            dateTo: qs('#filterDateTo', root)?.value || '',
            overdueOnly: Boolean(qs('#filterOverdue', root)?.checked),
            requestId: 0
        };

        const setLoading = (isLoading) => {
            if (loading) loading.hidden = !isLoading;
            if (list) list.style.opacity = isLoading ? '.38' : '1';
            qsa('button', root).forEach(btn => {
                if (btn.matches('[data-report-tab], [data-report-scope], [data-page], [data-apply-filters]')) {
                    btn.disabled = isLoading || btn.dataset.originalDisabled === 'true';
                }
            });
        };

        const queryString = () => {
            const params = new URLSearchParams();
            params.set('tab', state.tab);
            if (state.scope && state.scope !== 'all') params.set('Scope', state.scope);
            if (state.search.trim()) params.set('Search', state.search.trim());
            if (state.status) params.set('Status', state.status);
            if (state.reportType) params.set('ReportType', state.reportType);
            if (state.priority) params.set('Priority', state.priority);
            if (state.dateFrom) params.set('DateFrom', state.dateFrom);
            if (state.dateTo) params.set('DateTo', state.dateTo);
            if (state.overdueOnly) params.set('IsOverdueOnly', 'true');
            params.set('Page', String(state.page));
            params.set('PageSize', String(state.pageSize));
            return params.toString();
        };

        const updateUrl = () => {
            const params = new URLSearchParams(queryString());
            if (params.get('tab') === 'all') params.delete('tab');
            if (params.get('Page') === '1') params.delete('Page');
            if (params.get('PageSize') === '15') params.delete('PageSize');
            const url = `${window.location.pathname}${params.toString() ? `?${params}` : ''}`;
            window.history.replaceState({}, '', url);
        };

        const activeFilterCount = () => [
            state.status,
            state.reportType,
            state.priority,
            state.dateFrom,
            state.dateTo,
            state.overdueOnly ? '1' : ''
        ].filter(Boolean).length;

        const updateActiveFilterBadge = () => {
            if (!activeFilterCountBadge) return;
            const count = activeFilterCount();
            activeFilterCountBadge.textContent = String(count);
            activeFilterCountBadge.hidden = count === 0;
        };

        const renderCase = (item) => {
            const hasAgainst = item.againstUserId != null;
            const alertTitle = item.isOverdue
                ? 'انتهت مهلة الرد'
                : item.needsAdminAction
                    ? 'تحتاج إجراء من الإدارة'
                    : item.requiredActionLabel || '';

            const alertHtml = alertTitle ? `
                <div class="ar-case-alert ${item.isOverdue ? 'is-overdue' : ''}">
                    <i class="bi ${item.isOverdue ? 'bi-alarm-fill' : 'bi-lightning-charge-fill'}"></i>
                    <div>
                        <strong>${escapeHtml(alertTitle)}</strong>
                        ${item.requiredActionLabel && !item.needsAdminAction
                    ? `<small>${escapeHtml(item.requiredActionLabel)}</small>` : ''}
                    </div>
                </div>` : '';

            const warningHtml = Number(item.againstUserWarningCount || 0) > 0
                ? `<em><i class="bi bi-exclamation-triangle-fill"></i>${Number(item.againstUserWarningCount)} تحذير</em>`
                : '';

            const placeLine = item.placeName
                ? ` · ${escapeHtml(item.placeName)}`
                : '';

            return `
                <article class="ar-case-card ar-case-card--v3 ${item.needsAdminAction ? 'needs-action' : ''}" data-report-id="${Number(item.reportId)}">
                    <div class="ar-case-card__top">
                        <div class="ar-case-number"><span>القضية</span><strong>${escapeHtml(item.reportNumber)}</strong></div>
                        <div class="ar-case-badges">
                            <span class="ar-status ar-status--${statusTone(item.status)}">${escapeHtml(item.statusLabel)}</span>
                            <span class="ar-priority ar-priority--${priorityTone(item.priority)}">${escapeHtml(item.priorityLabel)}</span>
                        </div>
                    </div>

                    <div class="ar-case-card__heading ar-case-card__heading--v3">
                        <span class="ar-case-kind">${escapeHtml(item.scenarioLabel)}</span>
                        <h3>${escapeHtml(item.title)}</h3>
                        <small>${escapeHtml(item.referenceLabel)}${placeLine}</small>
                    </div>

                    <div class="ar-mini-direction">
                        <div class="ar-mini-person is-reporter">
                            <small>المبلّغ</small>
                            <strong title="${encodeAttr(item.reporterName)}">${escapeHtml(item.reporterName)}</strong>
                            <span>${escapeHtml(item.reporterRoleLabel)}</span>
                        </div>
                        <div class="ar-mini-arrow"><span>ضد</span><i class="bi bi-arrow-left"></i></div>
                        <div class="ar-mini-person is-against ${hasAgainst ? '' : 'is-empty'}">
                            <small>المبلّغ ضده</small>
                            <strong title="${encodeAttr(item.againstUserName)}">${escapeHtml(item.againstUserName)}</strong>
                            <span>${escapeHtml(item.againstUserRoleLabel)}</span>
                            ${warningHtml}
                        </div>
                    </div>

                    ${alertHtml}

                    <div class="ar-case-card__footer">
                        <div class="ar-case-flags">
                            ${item.hasEvidence ? '<span><i class="bi bi-paperclip"></i>يوجد دليل</span>' : ''}
                            <span><i class="bi bi-clock-history"></i>${escapeHtml(formatDateTime(item.updatedAt))}</span>
                        </div>
                        <a class="ar-open-case" href="${encodeAttr(detailsUrlBase)}${Number(item.reportId)}">
                            فتح القضية <i class="bi bi-arrow-left"></i>
                        </a>
                    </div>
                </article>`;
        };

        const renderEmpty = () => `
            <div class="ar-empty-state">
                <span><i class="bi bi-inbox"></i></span>
                <h3>لا توجد قضايا هنا</h3>
                <p>غيّر التبويب أو الفلاتر وستظهر النتائج المطابقة هنا.</p>
            </div>`;

        const renderPagination = (data) => {
            if (!pagination) return;
            if (!data.totalPages || data.totalPages <= 1) {
                pagination.innerHTML = '';
                return;
            }
            pagination.innerHTML = `
                <button type="button" data-page="${data.currentPage - 1}" ${data.hasPreviousPage ? '' : 'disabled'}>
                    <i class="bi bi-chevron-right"></i> السابق
                </button>
                <span>صفحة <strong>${data.currentPage}</strong> من <strong>${data.totalPages}</strong></span>
                <button type="button" data-page="${data.currentPage + 1}" ${data.hasNextPage ? '' : 'disabled'}>
                    التالي <i class="bi bi-chevron-left"></i>
                </button>`;
        };

        async function loadList() {
            if (!listUrl) return;
            const currentRequest = ++state.requestId;
            setLoading(true);
            try {
                const response = await fetch(`${listUrl}?${queryString()}`, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    credentials: 'same-origin'
                });
                if (!response.ok) throw new Error('تعذر تحميل القضايا.');
                const payload = await response.json();
                if (currentRequest !== state.requestId) return;
                if (!payload.success || !payload.data) throw new Error('تعذر تحميل القضايا.');

                const data = payload.data;
                list.innerHTML = Array.isArray(data.items) && data.items.length
                    ? data.items.map(renderCase).join('')
                    : renderEmpty();
                if (resultCount) resultCount.textContent = String(data.filteredCount ?? 0);
                state.page = Number(data.currentPage || 1);
                renderPagination(data);
                updateUrl();
            } catch (error) {
                list.innerHTML = `
                    <div class="ar-empty-state">
                        <span><i class="bi bi-wifi-off"></i></span>
                        <h3>تعذر تحديث القضايا</h3>
                        <p>${escapeHtml(error?.message || 'حاول مرة أخرى.')}</p>
                    </div>`;
            } finally {
                if (currentRequest === state.requestId) setLoading(false);
            }
        }

        async function refreshSummary() {
            if (!summaryUrl) return;
            try {
                const response = await fetch(summaryUrl, { credentials: 'same-origin' });
                if (!response.ok) return;
                const payload = await response.json();
                if (!payload.success || !payload.summary) return;
                const summary = payload.summary;
                const fields = ['totalCount', 'newCount', 'actionCount', 'waitingCount', 'escalatedCount', 'completedCount', 'urgentOpenCount', 'customerAgainstOwnerCount', 'ownerAgainstCustomerCount', 'platformCount'];
                fields.forEach(field => {
                    qsa(`[data-summary="${field}"]`, root).forEach(node => node.textContent = String(summary[field] ?? 0));
                });
                const tabMap = {
                    all: summary.totalCount,
                    new: summary.newCount,
                    action: summary.actionCount,
                    waiting: summary.waitingCount,
                    escalated: summary.escalatedCount,
                    completed: summary.completedCount
                };
                Object.entries(tabMap).forEach(([key, value]) => {
                    const badge = qs(`[data-tab-count="${key}"]`, root);
                    if (badge) badge.textContent = String(value ?? 0);
                });

                const scopeMap = {
                    all: summary.totalCount,
                    'customer-owner': summary.customerAgainstOwnerCount,
                    'owner-customer': summary.ownerAgainstCustomerCount,
                    platform: summary.platformCount
                };
                Object.entries(scopeMap).forEach(([key, value]) => {
                    const badge = qs(`[data-scope-count="${key}"]`, root);
                    if (badge) badge.textContent = String(value ?? 0);
                });
            } catch { /* summary is secondary */ }
        }

        qsa('[data-report-scope]', root).forEach(button => {
            button.addEventListener('click', () => {
                const scope = button.dataset.reportScope || 'all';
                if (state.scope === scope) return;
                state.scope = scope;
                state.page = 1;
                qsa('[data-report-scope]', root).forEach(node => {
                    const active = node === button;
                    node.classList.toggle('is-active', active);
                    node.setAttribute('aria-selected', active ? 'true' : 'false');
                });
                if (scopeTitle) {
                    const labels = {
                        all: 'كل القضايا',
                        'customer-owner': 'العميل ضد المالك',
                        'owner-customer': 'المالك ضد العميل',
                        platform: 'المنصة والتقني'
                    };
                    scopeTitle.textContent = labels[scope] || 'كل القضايا';
                }
                loadList();
            });
        });

        qsa('[data-report-tab]', root).forEach(button => {
            button.addEventListener('click', () => {
                const tab = button.dataset.reportTab || 'all';
                if (state.tab === tab) return;
                state.tab = tab;
                state.page = 1;
                qsa('[data-report-tab]', root).forEach(node => {
                    const active = node === button;
                    node.classList.toggle('is-active', active);
                    node.setAttribute('aria-selected', active ? 'true' : 'false');
                });
                if (sectionTitle) sectionTitle.textContent = qs('span', button)?.textContent?.trim() || 'القضايا';
                loadList();
            });
        });

        if (searchInput) {
            const onSearch = debounce(() => {
                state.search = searchInput.value;
                state.page = 1;
                loadList();
            }, 420);
            searchInput.addEventListener('input', onSearch);
        }

        qs('[data-clear-search]', root)?.addEventListener('click', () => {
            if (!searchInput) return;
            searchInput.value = '';
            state.search = '';
            state.page = 1;
            searchInput.focus();
            loadList();
        });

        const openFilters = () => {
            if (!filterPanel || !filterToggle) return;
            filterPanel.hidden = false;
            filterToggle.setAttribute('aria-expanded', 'true');
        };
        const closeFilters = () => {
            if (!filterPanel || !filterToggle) return;
            filterPanel.hidden = true;
            filterToggle.setAttribute('aria-expanded', 'false');
        };
        filterToggle?.addEventListener('click', () => filterPanel?.hidden ? openFilters() : closeFilters());
        qs('[data-close-filters]', root)?.addEventListener('click', closeFilters);

        qs('[data-apply-filters]', root)?.addEventListener('click', () => {
            state.status = qs('#filterStatus', root)?.value || '';
            state.reportType = qs('#filterType', root)?.value || '';
            state.priority = qs('#filterPriority', root)?.value || '';
            state.dateFrom = qs('#filterDateFrom', root)?.value || '';
            state.dateTo = qs('#filterDateTo', root)?.value || '';
            state.overdueOnly = Boolean(qs('#filterOverdue', root)?.checked);
            state.page = 1;
            updateActiveFilterBadge();
            closeFilters();
            loadList();
        });

        qs('[data-reset-filters]', root)?.addEventListener('click', () => {
            ['#filterStatus', '#filterType', '#filterPriority', '#filterDateFrom', '#filterDateTo'].forEach(selector => {
                const field = qs(selector, root);
                if (field) field.value = '';
            });
            const overdue = qs('#filterOverdue', root);
            if (overdue) overdue.checked = false;
            Object.assign(state, { status: '', reportType: '', priority: '', dateFrom: '', dateTo: '', overdueOnly: false, page: 1 });
            updateActiveFilterBadge();
            loadList();
        });

        qs('[data-show-overdue]', root)?.addEventListener('click', () => {
            const overdue = qs('#filterOverdue', root);
            if (overdue) overdue.checked = true;
            state.overdueOnly = true;
            state.page = 1;
            updateActiveFilterBadge();
            openFilters();
            loadList();
        });

        pagination?.addEventListener('click', event => {
            const button = event.target.closest('[data-page]');
            if (!button || button.disabled) return;
            state.page = Math.max(1, Number(button.dataset.page || 1));
            loadList().then(() => root.scrollIntoView({ behavior: 'smooth', block: 'start' }));
        });

        updateActiveFilterBadge();
        refreshSummary();
    }

    /* =========================================================
       تفاصيل القضية + نافذة الإجراءات
       ========================================================= */

    function initReportDetails() {
        const root = qs('#adminReportDetails');
        const modalLayer = qs('#reportActionModal');
        if (!root || !modalLayer) return;

        const modal = qs('.ar-modal', modalLayer);
        const form = qs('#reportActionForm', modalLayer);
        const fieldsHost = qs('#reportModalFields', modalLayer);
        const errorBox = qs('#reportModalError', modalLayer);
        const title = qs('#reportModalTitle', modalLayer);
        const eyebrow = qs('#reportModalEyebrow', modalLayer);
        const description = qs('#reportModalDescription', modalLayer);
        const icon = qs('#reportModalIcon i', modalLayer);
        const submit = qs('#reportModalSubmit', modalLayer);
        const submitText = qs('#reportModalSubmit span', modalLayer);
        const antiForgery = qs('#reportAntiForgeryForm input[name="__RequestVerificationToken"]')?.value || '';
        const reportId = Number(root.dataset.reportId || 0);
        let endpoint = '';
        let lastFocused = null;

        const toLocalInputValue = (hoursAhead = 24) => {
            const date = new Date(Date.now() + hoursAhead * 60 * 60 * 1000);
            const pad = value => String(value).padStart(2, '0');
            return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
        };

        const field = (label, name, type = 'textarea', options = {}) => {
            const required = options.required === false ? '' : 'required';
            const min = options.minLength ? `minlength="${options.minLength}"` : '';
            const max = options.maxLength ? `maxlength="${options.maxLength}"` : '';
            const placeholder = options.placeholder ? `placeholder="${encodeAttr(options.placeholder)}"` : '';
            const hint = options.hint ? `<small>${escapeHtml(options.hint)}</small>` : '';
            if (type === 'textarea') {
                return `<label class="ar-modal-field"><span>${escapeHtml(label)}</span><textarea name="${encodeAttr(name)}" ${required} ${min} ${max} ${placeholder}></textarea>${hint}</label>`;
            }
            return `<label class="ar-modal-field"><span>${escapeHtml(label)}</span><input type="${encodeAttr(type)}" name="${encodeAttr(name)}" ${required} ${min} ${max} ${placeholder} ${options.value ? `value="${encodeAttr(options.value)}"` : ''} />${hint}</label>`;
        };

        const configs = {
            'start-review': {
                title: 'بدء مراجعة القضية', eyebrow: 'الخطوة الأولى', icon: 'bi-play-circle-fill', submit: 'ابدأ المراجعة',
                description: 'ستتحول القضية من جديدة إلى قيد المتابعة وتبدأ الإدارة مراجعتها رسميًا.',
                fields: () => '<div class="ar-decision-intro"><i class="bi bi-info-circle-fill"></i><div><strong>لا يوجد شيء إضافي مطلوب</strong><p>بعد البدء ستظهر لك الخيارات المناسبة فقط لهذه القضية.</p></div></div>'
            },
            'request-clarification': {
                title: 'طلب توضيح من المبلّغ', eyebrow: 'معلومة ناقصة', icon: 'bi-chat-left-text-fill', submit: 'إرسال طلب التوضيح',
                description: 'اكتب بالضبط ما الذي تحتاجه من المبلّغ حتى يعرف ماذا يرسل لك.',
                fields: () => field('ما التوضيح المطلوب؟', 'Message', 'textarea', { minLength: 5, maxLength: 4000, placeholder: 'مثال: يرجى توضيح المبلغ وإرفاق إثبات الدفع...' }) +
                    field('مهلة الرد', 'Deadline', 'datetime-local', { value: toLocalInputValue(24), hint: 'بعد انتهاء المهلة ستظهر القضية كمتأخرة.' })
            },
            'request-owner-response': {
                title: 'طلب رد من المالك', eyebrow: 'إحالة للمالك', icon: 'bi-building-fill', submit: 'إرسال الطلب للمالك',
                description: 'سيصل للمالك إشعار بالقضية ويستطيع إرسال توضيحه ودليله داخل نفس القضية.',
                fields: () => field('ماذا تريد من المالك أن يوضح؟', 'Message', 'textarea', { minLength: 5, maxLength: 4000, placeholder: 'اكتب سؤال الإدارة للمالك بوضوح...' }) +
                    field('مهلة رد المالك', 'Deadline', 'datetime-local', { value: toLocalInputValue(24) })
            },
            'request-resolution-confirmation': {
                title: 'تأكيد الحل من العميل', eyebrow: 'قبل الإغلاق', icon: 'bi-patch-question-fill', submit: 'إرسال طلب التأكيد',
                description: 'سيُطلب من العميل تحديد هل تم حل المشكلة فعلًا أم لا.',
                fields: () => field('رسالة إضافية للعميل (اختياري)', 'Message', 'textarea', { required: false, maxLength: 1000, placeholder: 'مثال: تم تنفيذ الحل الموضح أعلاه، هل تؤكد أن المشكلة انتهت؟' }) +
                    field('مهلة التأكيد', 'Deadline', 'datetime-local', { value: toLocalInputValue(24) })
            },
            'change-priority': {
                title: 'تغيير أولوية القضية', eyebrow: 'أداة مساعدة', icon: 'bi-flag-fill', submit: 'حفظ الأولوية',
                description: 'الأولوية لترتيب العمل فقط ولا تغيّر نتيجة القضية.',
                fields: () => `
                    <label class="ar-modal-field"><span>الأولوية الجديدة</span>
                        <select name="Priority" required>
                            <option value="Low">منخفضة</option><option value="Normal" selected>عادية</option>
                            <option value="High">عالية</option><option value="Urgent">عاجلة</option>
                        </select>
                    </label>` + field('سبب التغيير', 'Reason', 'textarea', { minLength: 3, maxLength: 500, placeholder: 'لماذا تم تغيير الأولوية؟' })
            },
            'escalate': {
                title: 'تصعيد القضية', eyebrow: 'متابعة أعلى', icon: 'bi-arrow-up-right-circle-fill', submit: 'تصعيد القضية',
                description: 'استخدم التصعيد للقضايا الحساسة أو المهمة التي تحتاج متابعة أعلى.',
                fields: () => field('سبب التصعيد', 'Reason', 'textarea', { minLength: 5, maxLength: 1000, placeholder: 'اكتب سبب التصعيد بشكل واضح...' })
            },
            'internal-note': {
                title: 'إضافة ملاحظة داخلية', eyebrow: 'الإدارة فقط', icon: 'bi-eye-slash-fill', submit: 'حفظ الملاحظة',
                description: 'هذه الملاحظة لا تظهر للعميل ولا للمالك، وتبقى للإدارة فقط.',
                fields: () => field('الملاحظة الداخلية', 'NoteText', 'textarea', { minLength: 2, maxLength: 2000, placeholder: 'دوّن هنا معلومة داخلية تساعد الإدارة في متابعة القضية...' })
            },
            'resolve': {
                title: 'تسجيل حل القضية', eyebrow: 'قرار الإدارة', icon: 'bi-check-circle-fill', submit: 'تسجيل الحل',
                description: 'اكتب الحل بوضوح وما الإجراء الذي اتخذته الإدارة. تسجيل الحل لا يعني إغلاق القضية تلقائيًا.',
                fields: () => field('الحل المعتمد', 'Resolution', 'textarea', { minLength: 5, maxLength: 4000, placeholder: 'اكتب الحل الذي توصلت إليه الإدارة...' }) +
                    field('الإجراء المتخذ', 'ResolutionAction', 'text', { minLength: 2, maxLength: 100, placeholder: 'مثال: إعادة العربون / تصحيح بيانات / إجراء إداري' }) + `
                    <label class="ar-modal-check"><input type="checkbox" name="ReferToFinance" value="true"><span>إحالة الإجراء للمالية</span></label>
                    <label class="ar-modal-check"><input type="checkbox" name="ReferToTechnicalSupport" value="true"><span>إحالة الإجراء للدعم التقني</span></label>`
            },
            'reject': {
                title: 'رفض البلاغ', eyebrow: 'قرار الإدارة', icon: 'bi-x-circle-fill', submit: 'رفض البلاغ',
                description: 'سبب الرفض مهم وسيظهر للمبلّغ، لذلك اكتبه بشكل واضح ومهني.',
                fields: () => field('سبب رفض البلاغ', 'Reason', 'textarea', { minLength: 5, maxLength: 2000, placeholder: 'مثال: بعد مراجعة بيانات الحجز والمرفقات لم يثبت وجود مخالفة...' })
            },
            'confirm-customer-violation': {
                title: 'تثبيت مخالفة على العميل', eyebrow: 'بلاغ مالك ضد عميل', icon: 'bi-exclamation-triangle-fill', submit: 'تسجيل المخالفة والتحذير',
                description: 'لن يُطلب من العميل الرد. سيتم تسجيل التحذير وإرسال تنبيه بأن تكرار المخالفات قد يؤدي لإجراءات على الحساب.',
                fields: () => `<div class="ar-decision-intro ar-decision-intro--warning"><i class="bi bi-exclamation-triangle-fill"></i><div><strong>هذا الإجراء يسجل مخالفة فعلية</strong><p>تأكد من الأدلة قبل المتابعة. الحظر لا يتم من صفحة البلاغات.</p></div></div>` +
                    field('سبب ثبوت المخالفة', 'Reason', 'textarea', { minLength: 5, maxLength: 2000, placeholder: 'اكتب ما الذي أثبت المخالفة والسبب الإداري...' })
            },
            'dismiss-customer-violation': {
                title: 'المخالفة غير مثبتة', eyebrow: 'بلاغ مالك ضد عميل', icon: 'bi-shield-check', submit: 'رفض البلاغ',
                description: 'لن يسجل أي تحذير على العميل، وسيتم حفظ سبب عدم ثبوت المخالفة.',
                fields: () => field('سبب عدم ثبوت المخالفة', 'Reason', 'textarea', { minLength: 5, maxLength: 2000, placeholder: 'اكتب سبب القرار بعد مراجعة الأدلة...' })
            },
            'close': {
                title: 'إغلاق القضية رسميًا', eyebrow: 'الخطوة الأخيرة', icon: 'bi-lock-fill', submit: 'إغلاق القضية',
                description: 'بعد الإغلاق تعتبر القضية منتهية رسميًا. يمكن إعادة فتح نفس القضية لاحقًا عند وجود سبب مبرر.',
                fields: () => field('سبب الإغلاق', 'Reason', 'textarea', { minLength: 3, maxLength: 1000, placeholder: 'مثال: تم تنفيذ الحل وتأكيده وأصبحت القضية منتهية.' })
            },
            'reopen': {
                title: 'إعادة فتح القضية', eyebrow: 'نفس رقم القضية', icon: 'bi-arrow-counterclockwise', submit: 'إعادة فتح القضية',
                description: 'ستعود القضية للمراجعة بنفس رقمها وسجلها السابق، بدون إنشاء بلاغ جديد.',
                fields: () => field('سبب إعادة الفتح', 'Reason', 'textarea', { minLength: 5, maxLength: 1000, placeholder: 'مثال: ظهر دليل جديد يحتاج مراجعة الإدارة...' })
            }
        };

        const showToast = (message, isError = false) => {
            const stack = qs('#reportToastStack');
            if (!stack) return;
            const toast = document.createElement('div');
            toast.className = `ar-toast ${isError ? 'is-error' : ''}`;
            toast.innerHTML = `
                <span class="ar-toast__icon"><i class="bi ${isError ? 'bi-exclamation-triangle-fill' : 'bi-check2-circle'}"></i></span>
                <div><strong>${isError ? 'تعذر تنفيذ الإجراء' : 'تم بنجاح'}</strong><p>${escapeHtml(message)}</p></div>`;
            stack.appendChild(toast);
            requestAnimationFrame(() => toast.classList.add('is-visible'));
            window.setTimeout(() => {
                toast.classList.remove('is-visible');
                window.setTimeout(() => toast.remove(), 250);
            }, 3800);
        };

        const openModal = (button) => {
            const action = button.dataset.reportAction;
            const config = configs[action];
            if (!config) return;
            endpoint = button.dataset.endpoint || '';
            lastFocused = button;
            title.textContent = config.title;
            eyebrow.textContent = config.eyebrow;
            description.textContent = config.description;
            icon.className = `bi ${config.icon}`;
            submitText.textContent = config.submit;
            fieldsHost.innerHTML = config.fields();
            errorBox.hidden = true;
            errorBox.textContent = '';
            modalLayer.hidden = false;
            modalLayer.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            requestAnimationFrame(() => qs('textarea, input:not([type="hidden"]), select, button', fieldsHost)?.focus());
        };

        const closeModal = () => {
            modalLayer.hidden = true;
            modalLayer.setAttribute('aria-hidden', 'true');
            document.body.style.overflow = '';
            form.reset();
            endpoint = '';
            lastFocused?.focus?.();
        };

        qsa('[data-report-action]', root).forEach(button => button.addEventListener('click', () => openModal(button)));
        qsa('[data-close-report-modal]', modalLayer).forEach(button => button.addEventListener('click', closeModal));

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && !modalLayer.hidden) closeModal();
        });

        qs('[data-toggle-decisions]', root)?.addEventListener('click', event => {
            const actions = qs('[data-decision-actions]', root);
            const chevron = qs('[data-decision-chevron]', root);
            if (!actions) return;
            actions.hidden = !actions.hidden;
            chevron?.classList.toggle('bi-chevron-up', !actions.hidden);
            chevron?.classList.toggle('bi-chevron-down', actions.hidden);
        });

        qs('[data-toggle-tools]', root)?.addEventListener('click', event => {
            const body = qs('[data-tools-body]', root);
            const chevron = qs('[data-tools-chevron]', root);
            if (!body) return;
            body.hidden = !body.hidden;
            event.currentTarget.setAttribute('aria-expanded', body.hidden ? 'false' : 'true');
            chevron?.classList.toggle('bi-chevron-up', !body.hidden);
            chevron?.classList.toggle('bi-chevron-down', body.hidden);
        });

        qsa('[data-copy-text]', root).forEach(button => {
            button.addEventListener('click', async () => {
                try {
                    await navigator.clipboard.writeText(button.dataset.copyText || '');
                    showToast('تم نسخ رقم القضية.');
                } catch {
                    showToast('تعذر نسخ رقم القضية.', true);
                }
            });
        });

        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (!endpoint || !reportId) return;
            if (!form.reportValidity()) return;

            errorBox.hidden = true;
            errorBox.textContent = '';
            submit.disabled = true;
            submit.classList.add('is-loading');

            const data = new FormData(form);
            data.set('ReportId', String(reportId));
            if (antiForgery) data.set('__RequestVerificationToken', antiForgery);

            try {
                const response = await fetch(endpoint, {
                    method: 'POST',
                    body: data,
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                let payload = null;
                try { payload = await response.json(); } catch { /* ignore */ }

                if (!response.ok || !payload?.success) {
                    const modelErrors = payload?.errors
                        ? Object.values(payload.errors).flat().filter(Boolean).join(' • ')
                        : '';
                    throw new Error(modelErrors || payload?.message || 'تعذر تنفيذ الإجراء.');
                }

                showToast(payload.message || 'تم تنفيذ الإجراء بنجاح.');
                closeModal();
                window.setTimeout(() => {
                    if (payload.redirectUrl) window.location.href = payload.redirectUrl;
                    else window.location.reload();
                }, 520);
            } catch (error) {
                errorBox.textContent = error?.message || 'تعذر تنفيذ الإجراء.';
                errorBox.hidden = false;
                showToast(errorBox.textContent, true);
            } finally {
                submit.disabled = false;
                submit.classList.remove('is-loading');
            }
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        initReportsCenter();
        initReportDetails();
    });
})();
