/**
 * Owner Dashboard Pro
 * ApexCharts = الرسوم، FullCalendar = التقويم.
 * في حال لم تُحمّل مكتبة خارجية تبقى البيانات النصية تعمل بدون تعطل الصفحة.
 */
(function () {
    'use strict';

    const root = document.querySelector('[data-owner-dashboard]');
    const dataNode = document.getElementById('ownerDashboardData');
    if (!root || !dataNode) return;

    let data = {};
    try {
        data = JSON.parse(dataNode.textContent || '{}');
    } catch (error) {
        console.error('Owner dashboard data could not be parsed.', error);
        return;
    }

    const css = getComputedStyle(document.documentElement);
    const isDark = () => document.body.classList.contains('dark-mode');
    const ink = () => isDark() ? '#EAF2F5' : '#476779';
    const grid = () => isDark() ? 'rgba(255,255,255,.07)' : 'rgba(18,79,108,.07)';
    const primary = '#00A6DA';
    const success = '#20A66A';
    const danger = '#E85D68';
    const warning = '#F59E0B';

    const toNumbers = values => (Array.isArray(values) ? values : []).map(value => Number(value || 0));
    const last = (values, count) => values.slice(Math.max(0, values.length - count));

    let bookingChart = null;

    function renderBookingChart(days = 30) {
        const node = document.getElementById('ownerBookingChart');
        if (!node || typeof window.ApexCharts === 'undefined') return;

        const trend = data.bookingTrend || {};
        const labels = last(Array.isArray(trend.labels) ? trend.labels : [], days);
        const bookings = last(toNumbers(trend.bookings), days);
        const completed = last(toNumbers(trend.completed), days);
        const cancelled = last(toNumbers(trend.cancelled), days);

        const options = {
            chart: { type: 'area', height: 265, toolbar: { show: false }, zoom: { enabled: false }, fontFamily: 'Tajawal, sans-serif', animations: { enabled: true, easing: 'easeinout', speed: 950, animateGradually: { enabled: true, delay: 90 }, dynamicAnimation: { enabled: true, speed: 450 } } },
            series: [
                { name: 'الحجوزات', data: bookings },
                { name: 'المكتملة', data: completed },
                { name: 'الملغاة', data: cancelled }
            ],
            colors: [primary, success, danger],
            stroke: { curve: 'smooth', width: [3, 2.4, 2.1] },
            fill: { type: 'gradient', gradient: { shadeIntensity: 1, opacityFrom: .20, opacityTo: .015, stops: [0, 92, 100] } },
            dataLabels: { enabled: false },
            markers: { size: 0, hover: { sizeOffset: 4 } },
            xaxis: { categories: labels, labels: { style: { colors: ink(), fontSize: '13px' } }, axisBorder: { show: false }, axisTicks: { show: false }, tooltip: { enabled: false } },
            yaxis: { min: 0, forceNiceScale: true, labels: { style: { colors: ink(), fontSize: '13px' }, formatter: value => Math.round(value) } },
            grid: { borderColor: grid(), strokeDashArray: 4, padding: { left: 4, right: 4 } },
            legend: { position: 'top', horizontalAlign: 'right', fontSize: '14px', labels: { colors: ink() }, markers: { width: 8, height: 8, radius: 8 } },
            tooltip: { theme: isDark() ? 'dark' : 'light' }
        };

        if (bookingChart) {
            bookingChart.updateOptions({ xaxis: options.xaxis });
            bookingChart.updateSeries(options.series);
        } else {
            bookingChart = new ApexCharts(node, options);
            bookingChart.render();
        }
    }

    function renderFinanceChart() {
        const node = document.getElementById('ownerFinanceChart');
        if (!node || typeof window.ApexCharts === 'undefined') return;
        const finance = data.finance || {};
        new ApexCharts(node, {
            chart: { type: 'bar', height: 280, toolbar: { show: false }, fontFamily: 'Tajawal, sans-serif', animations: { enabled: true, easing: 'easeinout', speed: 1100, animateGradually: { enabled: true, delay: 120 } } },
            series: [
                { name: 'المحصل', data: toNumbers(finance.monthlyCollected) },
                { name: 'عمولة المنصة', data: toNumbers(finance.monthlyCommission) }
            ],
            colors: [primary, warning],
            plotOptions: { bar: { borderRadius: 8, columnWidth: '46%', distributed: false } },
            dataLabels: { enabled: false },
            xaxis: { categories: finance.monthlyLabels || [], labels: { style: { colors: ink(), fontSize: '13px' } }, axisBorder: { show: false }, axisTicks: { show: false } },
            yaxis: { labels: { style: { colors: ink(), fontSize: '13px' }, formatter: compactNumber } },
            grid: { borderColor: grid(), strokeDashArray: 4 },
            legend: { position: 'top', horizontalAlign: 'right', fontSize: '14px', labels: { colors: ink() } },
            tooltip: { theme: isDark() ? 'dark' : 'light', y: { formatter: value => `${formatNumber(value)} ${data.currency || ''}` } }
        }).render();
    }

    function renderGuaranteeChart() {
        const node = document.getElementById('ownerGuaranteeChart');
        if (!node || typeof window.ApexCharts === 'undefined') return;
        const guarantee = data.guarantee || {};
        const status = String(guarantee.accountStatus || 'NeedsTopUp');
        const chartColor = status === 'Active' ? success : (status === 'InDebt' || status === 'Suspended' ? danger : warning);
        new ApexCharts(node, {
            chart: { type: 'radialBar', height: 190, sparkline: { enabled: true }, fontFamily: 'Tajawal, sans-serif', animations: { enabled: true, easing: 'easeinout', speed: 1400 } },
            series: [Math.max(0, Math.min(100, Number(guarantee.fundingPercent || 0)))],
            colors: [chartColor],
            plotOptions: { radialBar: { startAngle: -130, endAngle: 130, hollow: { size: '62%' }, track: { background: isDark() ? 'rgba(255,255,255,.08)' : '#EAF1F4', strokeWidth: '100%' }, dataLabels: { name: { show: true, offsetY: 19, color: ink(), fontSize: '13px' }, value: { offsetY: -7, color: ink(), fontSize: '24px', fontWeight: 900, formatter: value => `${Math.round(value)}%` } } } },
            labels: ['تغطية الرصيد']
        }).render();
    }

    function renderReviewsChart() {
        const node = document.getElementById('ownerReviewsChart');
        if (!node || typeof window.ApexCharts === 'undefined') return;
        const reviews = data.reviews || {};
        const raw = toNumbers(reviews.starCounts);
        const counts = raw.length === 5 ? raw.slice().reverse() : [0, 0, 0, 0, 0];
        new ApexCharts(node, {
            chart: { type: 'donut', height: 185, fontFamily: 'Tajawal, sans-serif', animations: { enabled: true, easing: 'easeinout', speed: 1200, animateGradually: { enabled: true, delay: 130 } } },
            series: counts,
            labels: ['5 نجوم', '4 نجوم', '3 نجوم', 'نجمتان', 'نجمة'],
            colors: ['#E5A000', '#F3B837', '#7EC7DD', '#99AAB3', '#C8D3D8'],
            stroke: { width: 2, colors: [isDark() ? '#202A31' : '#FFFFFF'] },
            legend: { show: false },
            dataLabels: { enabled: false },
            plotOptions: { pie: { donut: { size: '68%' } } },
            tooltip: { theme: isDark() ? 'dark' : 'light', y: { formatter: value => `${value} تقييم` } }
        }).render();
    }

    function renderSparklines() {
        if (typeof window.ApexCharts === 'undefined') return;
        const trend = data.bookingTrend || {};
        document.querySelectorAll('[data-sparkline="bookings"]').forEach(node => {
            new ApexCharts(node, sparkOptions(last(toNumbers(trend.bookings), 10), primary)).render();
        });
        document.querySelectorAll('[data-sparkline="occupancy"]').forEach(node => {
            const values = last(toNumbers(trend.completed), 10);
            new ApexCharts(node, sparkOptions(values, '#3886C7')).render();
        });
    }

    function sparkOptions(values, color) {
        return {
            chart: { type: 'area', height: 38, width: 78, sparkline: { enabled: true }, animations: { enabled: false } },
            series: [{ data: values.length ? values : [0, 0, 0, 0, 0] }],
            colors: [color],
            stroke: { curve: 'smooth', width: 2 },
            fill: { type: 'gradient', gradient: { opacityFrom: .22, opacityTo: .01 } },
            tooltip: { enabled: false }
        };
    }

    function renderCalendar() {
        const node = document.getElementById('ownerMiniCalendar');
        if (!node || typeof window.FullCalendar === 'undefined') return;

        const events = (Array.isArray(data.calendarEvents) ? data.calendarEvents : []).map(item => ({
            id: String(item.bookingId || ''),
            title: item.title || 'حجز',
            start: item.start,
            end: item.end || undefined,
            url: item.url || undefined,
            extendedProps: { state: item.state || '', source: item.source || '' }
        }));

        const calendar = new FullCalendar.Calendar(node, {
            locale: 'ar',
            direction: 'rtl',
            initialView: 'dayGridMonth',
            height: 'auto',
            fixedWeekCount: false,
            dayMaxEvents: 2,
            headerToolbar: { start: 'title', center: '', end: 'prev,next' },
            events,
            eventDidMount(info) {
                const state = String(info.event.extendedProps.state || '');
                if (state === 'Completed') info.el.style.backgroundColor = success;
                else if (state === 'NoShow') info.el.style.backgroundColor = warning;
                else info.el.style.backgroundColor = primary;
                info.el.title = info.event.title;
            },
            eventClick(info) {
                if (info.event.url) {
                    info.jsEvent.preventDefault();
                    window.location.href = info.event.url;
                }
            }
        });
        calendar.render();
    }

    function bindRangeSwitch() {
        document.querySelectorAll('[data-booking-range]').forEach(button => {
            button.addEventListener('click', () => {
                const days = Math.max(7, Math.min(30, Number.parseInt(button.dataset.bookingRange || '30', 10)));
                document.querySelectorAll('[data-booking-range]').forEach(item => item.classList.toggle('is-active', item === button));
                renderBookingChart(days);
            });
        });
    }

    function animatePanels() {
        const items = document.querySelectorAll('.od-kpi, .od-panel, .od-command-card, .od-trial-banner, .od-day-command');
        if (!items.length || !('IntersectionObserver' in window) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('od-revealed', 'is-visible');
                entry.target.style.opacity = '';
                entry.target.style.transform = '';
                observer.unobserve(entry.target);
            });
        }, { threshold: .05 });
        items.forEach((item, index) => {
            item.style.opacity = '0';
            item.style.transform = 'translateY(12px)';
            item.style.transition = `opacity .35s ease ${Math.min(index * 20, 180)}ms, transform .35s ease ${Math.min(index * 20, 180)}ms`;
            observer.observe(item);
        });
        const style = document.createElement('style');
        style.textContent = '.od-revealed{opacity:1!important}';
        document.head.appendChild(style);
    }

    function compactNumber(value) {
        const number = Number(value || 0);
        if (Math.abs(number) >= 1000000) return `${(number / 1000000).toFixed(1)}M`;
        if (Math.abs(number) >= 1000) return `${(number / 1000).toFixed(0)}K`;
        return String(Math.round(number));
    }

    function formatNumber(value) {
        try { return new Intl.NumberFormat('ar-YE', { maximumFractionDigits: 0 }).format(Number(value || 0)); }
        catch (_) { return String(Math.round(Number(value || 0))); }
    }


    // =============================================================
    //  2.1. اللمسات الحية: الوقت، القصة، العدادات، 3D، تنبيهات التجربة
    // =============================================================

    function initLiveClock() {
        const clock = root.querySelector('[data-live-clock]');
        const date = root.querySelector('[data-live-date]');
        const greeting = root.querySelector('[data-live-greeting]');
        if (!clock && !date && !greeting) return;

        const update = () => {
            const now = new Date();
            const hour = now.getHours();
            if (clock) {
                clock.textContent = new Intl.DateTimeFormat('ar-YE', {
                    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true
                }).format(now);
            }
            if (date) {
                date.textContent = new Intl.DateTimeFormat('ar-YE', {
                    weekday: 'long', day: '2-digit', month: 'long'
                }).format(now);
            }
            if (greeting) {
                greeting.textContent = hour < 12
                    ? 'صباح الخير'
                    : hour < 18
                        ? 'مساء الخير'
                        : 'مساء النور';
            }
        };

        update();
        window.setInterval(update, 1000);
    }

    function initDashboardStory() {
        const storyNode = root.querySelector('[data-story-text]');
        const dots = Array.from(root.querySelectorAll('.od-story-board__dots i'));
        if (!storyNode) return;

        const kpis = data.kpis || {};
        const bookingSummary = data.bookingSummary || {};
        const marketing = data.marketing || {};
        const attentionCount = Number(data.attentionCount || 0);
        const ownerName = String(data.ownerName || '');
        const placeName = String(data.placeName || 'منشأتك');
        const stories = [];

        const todayBookings = Number(kpis.todayBookings || 0);
        const newBookings = Number(kpis.newBookingsToday || 0);
        const monthBookings = Number(bookingSummary.totalThisMonth || 0);
        const revenueChange = Number(kpis.revenueChangePercent || 0);
        const rating = Number(kpis.averageRating || 0);
        const favorites = Number(marketing.favorites || 0);

        if (newBookings > 0) {
            stories.push(`جميل${ownerName ? ` يا ${ownerName}` : ''} — لديك ${newBookings} حجز جديد اليوم في ${placeName}.`);
        } else {
            stories.push(`${placeName} هادئة الآن؛ لا توجد حجوزات جديدة مسجلة اليوم حتى هذه اللحظة.`);
        }

        if (attentionCount > 0) {
            stories.push(`هناك ${attentionCount} عنصر يحتاج انتباهك الآن. افتح قسم «يحتاج انتباهك» لإنهائها بسرعة.`);
        } else {
            stories.push('كل المؤشرات العاجلة تحت السيطرة الآن، ولا توجد إجراءات فورية معلقة.');
        }

        if (revenueChange > 0) {
            stories.push(`إيراد هذا الشهر أعلى بنحو ${Math.abs(revenueChange).toFixed(1)}% من الشهر السابق.`);
        } else if (revenueChange < 0) {
            stories.push(`إيراد هذا الشهر أقل بنحو ${Math.abs(revenueChange).toFixed(1)}% من الشهر السابق؛ راقب الحجوزات والعروض.`);
        } else {
            stories.push(`لديك ${monthBookings} حجز مسجل هذا الشهر، وأداء الإيراد مستقر مقارنة بالشهر السابق.`);
        }

        if (rating > 0) {
            stories.push(`رضا العملاء عند ${rating.toFixed(1)} من 5، والمنشأة موجودة في ${favorites} قائمة مفضلة.`);
        } else if (todayBookings > 0) {
            stories.push(`تشغيل اليوم يحتوي ${todayBookings} حجز يبدأ اليوم؛ راجع الجدول لضمان جاهزية التشغيل.`);
        } else {
            stories.push('ستتحول هذه اللوحة إلى قصة تشغيل يومية تلقائيًا كلما زادت الحجوزات والتقييمات والتفاعل.');
        }

        let index = 0;
        const showStory = () => {
            storyNode.classList.add('is-changing');
            window.setTimeout(() => {
                storyNode.textContent = stories[index % stories.length];
                dots.forEach((dot, dotIndex) => dot.classList.toggle('is-active', dotIndex === (index % dots.length)));
                storyNode.classList.remove('is-changing');
                index += 1;
            }, 180);
        };

        showStory();
        window.setInterval(showStory, 5200);
    }

    function initCounters() {
        const nodes = Array.from(root.querySelectorAll('[data-counter-value]'));
        if (!nodes.length || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        const animate = node => {
            const target = Number(node.dataset.counterValue || 0);
            const decimals = Math.max(0, Math.min(2, Number.parseInt(node.dataset.counterDecimals || '0', 10)));
            const suffix = String(node.dataset.counterSuffix || '');
            const duration = 850;
            const start = performance.now();

            const tick = time => {
                const progress = Math.min(1, (time - start) / duration);
                const eased = 1 - Math.pow(1 - progress, 3);
                const current = target * eased;
                node.textContent = new Intl.NumberFormat('ar-YE', {
                    minimumFractionDigits: decimals,
                    maximumFractionDigits: decimals
                }).format(current) + suffix;
                if (progress < 1) requestAnimationFrame(tick);
            };
            requestAnimationFrame(tick);
        };

        if (!('IntersectionObserver' in window)) {
            nodes.forEach(animate);
            return;
        }

        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                animate(entry.target);
                observer.unobserve(entry.target);
            });
        }, { threshold: .45 });

        nodes.forEach(node => observer.observe(node));
    }

    function initTilt() {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches ||
            !window.matchMedia('(pointer:fine)').matches) return;

        const cards = Array.from(root.querySelectorAll('[data-od-tilt]'));
        cards.forEach(card => {
            const max = card.matches('[data-od-hero]') ? 2.2 : 1.25;
            card.addEventListener('pointermove', event => {
                const rect = card.getBoundingClientRect();
                const x = (event.clientX - rect.left) / rect.width - .5;
                const y = (event.clientY - rect.top) / rect.height - .5;
                card.style.setProperty('--od-tilt-x', `${(-y * max).toFixed(2)}deg`);
                card.style.setProperty('--od-tilt-y', `${(x * max).toFixed(2)}deg`);
                card.style.setProperty('--od-glow-x', `${((x + .5) * 100).toFixed(1)}%`);
                card.style.setProperty('--od-glow-y', `${((y + .5) * 100).toFixed(1)}%`);
                card.classList.add('is-tilting');
            });
            card.addEventListener('pointerleave', () => {
                card.style.setProperty('--od-tilt-x', '0deg');
                card.style.setProperty('--od-tilt-y', '0deg');
                card.classList.remove('is-tilting');
            });
        });
    }

    function initDayCommand() {
        const day = root.querySelector('[data-day-command]');
        if (!day) return;

        const fill = day.querySelector('[data-day-fill]');
        const marker = day.querySelector('[data-day-marker]');
        const markerTime = day.querySelector('[data-day-marker-time]');
        const nowBadge = day.querySelector('[data-day-now]');
        const story = day.querySelector('[data-day-story] span');
        const operation = data.todayOperation || {};

        const update = () => {
            const now = new Date();
            const minutes = now.getHours() * 60 + now.getMinutes() + now.getSeconds() / 60;
            const percent = Math.max(0, Math.min(100, minutes / 1440 * 100));
            const timeText = new Intl.DateTimeFormat('ar-YE', {
                hour: '2-digit', minute: '2-digit', hour12: true
            }).format(now);

            if (fill) fill.style.width = `${percent.toFixed(3)}%`;
            if (marker) marker.style.left = `${percent.toFixed(3)}%`;
            if (markerTime) markerTime.textContent = timeText;
            if (nowBadge) nowBadge.textContent = timeText;

            if (story) {
                const hour = now.getHours();
                const arrivals = Number(operation.arrivalsToday || 0);
                const departures = Number(operation.departuresToday || 0);
                const active = Number(operation.activeNow || 0);
                if (hour < 6) story.textContent = `بداية يوم هادئة؛ ${active} حجز نشط الآن، واللوحة تتابع التشغيل تلقائيًا.`;
                else if (hour < 12) story.textContent = `فترة الصباح: ${arrivals} وصول اليوم و${active} حجز نشط الآن.`;
                else if (hour < 18) story.textContent = `منتصف يوم التشغيل: ${active} حجز نشط، و${departures} مغادرة مجدولة اليوم.`;
                else story.textContent = `الفترة المسائية: راقب ${departures} مغادرة اليوم واستعدادات حجوزات الغد.`;
            }
        };

        update();
        window.setInterval(update, 30000);

        if (!('IntersectionObserver' in window) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            day.classList.add('is-visible');
            return;
        }
        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                day.classList.add('is-visible');
                observer.disconnect();
            });
        }, { threshold: .15 });
        observer.observe(day);
    }

    function initTrialCountdownAndReminder() {
        const trialNode = root.querySelector('[data-trial-status]');
        if (!trialNode) return;

        const isActiveTrial = String(trialNode.dataset.trialActive || '').toLowerCase() === 'true';
        const isExpiredTrial = String(trialNode.dataset.trialExpired || '').toLowerCase() === 'true';
        const endingSoon = String(trialNode.dataset.trialEndingSoon || '').toLowerCase() === 'true';
        const needsTopUp = String(trialNode.dataset.trialNeedsTopup || '').toLowerCase() === 'true';
        const remainingDays = Number.parseInt(trialNode.dataset.trialRemainingDays || '0', 10);
        const endsAtRaw = String(trialNode.dataset.trialEndsAt || '');
        const reminderKey = String(trialNode.dataset.trialReminderKey || '');
        const currency = String(trialNode.dataset.trialCurrency || data.currency || 'ريال يمني');
        const missing = Number(trialNode.dataset.trialMissing || 0);
        const balanceUrl = String(trialNode.dataset.trialBalanceUrl || '/Owner/Financials?financeTab=balance');
        const countdownValue = trialNode.querySelector('[data-trial-countdown] strong');
        const endsAt = endsAtRaw ? new Date(endsAtRaw) : null;
        let countdownTimer = null;

        const updateCountdown = () => {
            if (!countdownValue || !endsAt || Number.isNaN(endsAt.getTime())) return;

            const diff = endsAt.getTime() - Date.now();

            if (diff <= 0) {
                countdownValue.textContent = 'انتهت الفترة التجريبية';
                trialNode.dataset.trialActive = 'false';
                trialNode.dataset.trialExpired = 'true';

                if (countdownTimer !== null) {
                    window.clearInterval(countdownTimer);
                    countdownTimer = null;
                }

                return;
            }

            const totalHours = Math.floor(diff / 3600000);
            const days = Math.floor(totalHours / 24);
            const hours = totalHours % 24;
            const minutes = Math.floor((diff % 3600000) / 60000);
            const seconds = Math.floor((diff % 60000) / 1000);

            countdownValue.textContent =
                `${days} يوم · ${hours} ساعة · ${minutes} دقيقة · ${seconds} ثانية`;
        };

        if (isActiveTrial) {
            updateCountdown();

            if (endsAt && endsAt.getTime() > Date.now()) {
                countdownTimer = window.setInterval(updateCountdown, 1000);
            }
        }

        const shouldShowPopup = isExpiredTrial || endingSoon;
        if (!shouldShowPopup || !reminderKey || typeof window.Swal === 'undefined') return;

        try {
            if (window.localStorage.getItem(reminderKey) === 'shown') return;
        } catch (_) { }

        const showReminder = () => {
            if (document.body.classList.contains('owner-trial-welcome-open')) {
                window.setTimeout(showReminder, 650);
                return;
            }

            let title = '';
            let html = '';
            let icon = 'info';
            let confirmText = 'فهمت';
            let showCancelButton = false;

            if (isExpiredTrial) {
                title = 'انتهت تجربتك المجانية';
                if (needsTopUp) {
                    icon = 'warning';
                    confirmText = 'شحن الرصيد الآن';
                    showCancelButton = true;
                    html = `انتهت الفترة التجريبية، والحجوزات الجديدة متوقفة مؤقتًا حتى يكتمل رصيد ضمان العمولات.${missing > 0 ? `<br><strong>المبلغ الناقص: ${formatNumber(missing)} ${currency}</strong>` : ''}`;
                } else {
                    icon = 'success';
                    html = 'انتهت الفترة التجريبية، ورصيد ضمان العمولات لديك مستوفٍ حاليًا. يمكنك مواصلة استقبال الحجوزات بصورة طبيعية.';
                }
            } else {
                title = remainingDays === 1
                    ? 'بقي يوم واحد فقط'
                    : remainingDays === 2
                        ? 'بقي يومان على انتهاء التجربة'
                        : 'بقي 3 أيام على انتهاء التجربة';
                icon = remainingDays === 1 ? 'warning' : 'info';
                showCancelButton = needsTopUp;
                confirmText = needsTopUp ? 'تجهيز رصيد الضمان' : 'حسنًا';
                html = `الفترة التجريبية تقترب من النهاية.${needsTopUp ? ` جهّز الرصيد الآن حتى لا تتوقف الحجوزات الجديدة.${missing > 0 ? `<br><strong>المبلغ المطلوب استكماله: ${formatNumber(missing)} ${currency}</strong>` : ''}` : ' رصيدك الحالي جاهز، وسنواصل متابعة الحالة معك.'}`;
            }

            window.Swal.fire({
                title,
                html,
                icon,
                confirmButtonText: confirmText,
                cancelButtonText: 'لاحقًا',
                showCancelButton,
                reverseButtons: true,
                focusConfirm: false,
                customClass: {
                    popup: 'od-trial-swal',
                    confirmButton: 'od-trial-swal__confirm',
                    cancelButton: 'od-trial-swal__cancel'
                }
            }).then(result => {
                try { window.localStorage.setItem(reminderKey, 'shown'); } catch (_) { }
                if (result.isConfirmed && needsTopUp) {
                    window.location.href = balanceUrl;
                }
            });
        };

        window.setTimeout(showReminder, 900);
    }

    function initPointerGlow() {
        if (!window.matchMedia('(pointer:fine)').matches) return;
        root.addEventListener('pointermove', event => {
            const rect = root.getBoundingClientRect();
            const x = Math.max(0, Math.min(100, ((event.clientX - rect.left) / Math.max(1, rect.width)) * 100));
            const y = Math.max(0, Math.min(100, ((event.clientY - rect.top) / Math.max(1, rect.height)) * 100));
            root.style.setProperty('--od-pointer-x', `${x}%`);
            root.style.setProperty('--od-pointer-y', `${y}%`);
        }, { passive: true });
    }

    initLiveClock();
    initDashboardStory();
    initCounters();
    initTilt();
    initDayCommand();
    initTrialCountdownAndReminder();
    initPointerGlow();

    bindRangeSwitch();
    renderBookingChart(30);
    renderFinanceChart();
    renderGuaranteeChart();
    renderReviewsChart();
    renderSparklines();
    renderCalendar();
    animatePanels();
})();

// =============================================================
//  3. منبثقة الترحيب بالفترة التجريبية
// =============================================================

(function () {
    'use strict';

    function initOwnerTrialWelcome() {
        const root = document.querySelector('[data-owner-trial-welcome]');

        if (!root) {
            return;
        }

        // ننقل المنبثقة مباشرة إلى body حتى لا تبقى محبوسة داخل
        // stacking context الخاص بمحتوى لوحة التحكم. بهذه الطريقة
        // يغطي الـ backdrop الهيدر والسايدبار ولوحة المحتوى كلها.
        if (root.parentElement !== document.body) {
            document.body.appendChild(root);
        }

        const shouldShow =
            String(root.dataset.showTrialWelcome || '')
                .trim()
                .toLowerCase() === 'true';

        const storageKey =
            String(root.dataset.storageKey || '').trim();

        if (!shouldShow || !storageKey) {
            return;
        }

        const dialog = root.querySelector('.owner-trial-welcome__dialog');
        const dismissButtons = Array.from(
            root.querySelectorAll('[data-owner-trial-dismiss]')
        );
        const balanceLink = root.querySelector('[data-owner-trial-balance]');

        let previousActiveElement = null;
        let isOpen = false;

        function hasSeenWelcome() {
            try {
                return window.localStorage.getItem(storageKey) === 'seen';
            } catch (_) {
                return false;
            }
        }

        function rememberWelcome() {
            try {
                window.localStorage.setItem(storageKey, 'seen');
            } catch (_) {
                // في حال منع التخزين المحلي لا نوقف عمل الواجهة.
            }
        }

        function getFocusableElements() {
            return Array.from(
                root.querySelectorAll(
                    'button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
                )
            ).filter(element =>
                !element.hasAttribute('hidden') &&
                element.getAttribute('aria-hidden') !== 'true'
            );
        }

        function openWelcome() {
            if (isOpen || hasSeenWelcome()) {
                return;
            }

            isOpen = true;
            previousActiveElement = document.activeElement;

            root.hidden = false;
            root.setAttribute('aria-hidden', 'false');
            document.body.classList.add('owner-trial-welcome-open');

            window.requestAnimationFrame(function () {
                root.classList.add('is-visible');

                window.setTimeout(function () {
                    const focusables = getFocusableElements();
                    const preferred = root.querySelector(
                        '.owner-trial-welcome__primary'
                    );

                    if (preferred instanceof HTMLElement) {
                        preferred.focus({ preventScroll: true });
                    } else if (focusables[0] instanceof HTMLElement) {
                        focusables[0].focus({ preventScroll: true });
                    } else if (dialog instanceof HTMLElement) {
                        dialog.focus({ preventScroll: true });
                    }
                }, 120);
            });
        }

        function closeWelcome(options) {
            if (!isOpen) {
                return;
            }

            const settings = Object.assign(
                {
                    remember: true,
                    restoreFocus: true
                },
                options || {}
            );

            if (settings.remember) {
                rememberWelcome();
            }

            isOpen = false;
            root.classList.remove('is-visible');
            root.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('owner-trial-welcome-open');

            window.setTimeout(function () {
                root.hidden = true;

                if (
                    settings.restoreFocus &&
                    previousActiveElement instanceof HTMLElement &&
                    document.contains(previousActiveElement)
                ) {
                    previousActiveElement.focus({ preventScroll: true });
                }
            }, 280);
        }

        dismissButtons.forEach(button => {
            button.addEventListener('click', function () {
                closeWelcome({
                    remember: true,
                    restoreFocus: true
                });
            });
        });

        if (balanceLink) {
            balanceLink.addEventListener('click', function () {
                // لا نمنع الانتقال؛ فقط نسجل أن رسالة الترحيب تمت مشاهدتها.
                rememberWelcome();
            });
        }

        document.addEventListener('keydown', function (event) {
            if (!isOpen) {
                return;
            }

            if (event.key === 'Escape') {
                event.preventDefault();
                closeWelcome({
                    remember: true,
                    restoreFocus: true
                });
                return;
            }

            if (event.key !== 'Tab') {
                return;
            }

            const focusables = getFocusableElements();

            if (!focusables.length) {
                event.preventDefault();
                dialog?.focus({ preventScroll: true });
                return;
            }

            const first = focusables[0];
            const last = focusables[focusables.length - 1];

            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        });

        /*
         * لا نغلق المنبثقة عند الضغط على الخلفية؛ الرسالة مالية مهمة
         * ويجب أن يختار المالك بوضوح "فهمت" أو ينتقل لتجهيز الرصيد.
         */

        if (!hasSeenWelcome()) {
            window.setTimeout(openWelcome, 260);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener(
            'DOMContentLoaded',
            initOwnerTrialWelcome,
            { once: true }
        );
    } else {
        initOwnerTrialWelcome();
    }
}());
