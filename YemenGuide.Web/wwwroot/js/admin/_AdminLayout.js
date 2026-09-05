// =============================================================
// 0. تهيئة بيانات الـ Layout من خصائص HTML غير التنفيذية
// =============================================================

window.currentUserId =
    document.body?.dataset.currentUserId || '0';

// يضمن تطابق class الوضع الليلي مع الكوكيز عند أي تحديث للحالة.
(function syncInitialDarkModeClass() {
    const isDark = document.cookie.includes('darkMode=enabled');
    document.body?.classList.toggle('dark-mode', isDark);
    try {
        localStorage.removeItem('darkMode');
    } catch (_) {
        // لا نوقف بقية الواجهة إذا كان التخزين المحلي غير متاح.
    }
})();

/**
* =============================================================
* Admin Layout Scripts - Stable Sidebar & Dark Mode (Inactivity Auto-Collapse)
* =============================================================
*/

// =============================================================
//  1. الأحداث والتفاعلات الفورية عند اكتمال تحميل الصفحة
// =============================================================

document.addEventListener('DOMContentLoaded', function () {
    initDarkMode();
    initMobileSidebar();
    initGlobalSearch();
    initProfileTabDeepLinking(); // تفعيل حركة تنقل التبويبات العميقة من الهيدر
    initAdminHeaderImageFallbacks();
});


// =============================================================
//  2. تشغيل سهم الطي الأنيق للقائمة الجانبية مع ميزة الإغلاق الذاتي عند الخمول
// =============================================================

document.addEventListener('DOMContentLoaded', function () {
    const desktopToggle = document.getElementById('desktopSidebarToggle');
    const sidebar = document.getElementById('dashboardSidebar');
    const mainContent = document.querySelector('.dashboard-main');

    if (desktopToggle && sidebar) {

        let inactivityTimer = null;
        const inactivityTimeoutDuration = 7000; // ⏱️ مدة الخمول: 7 ثوانٍ (تقفل تلقائياً إذا تركت الماوس)

        // دالة موحدة لتحديث الأبعاد رسومياً بناءً على حالة القائمة
        function updateLayoutDimensions(isCollapsed) {
            if (!mainContent) return;

            /*
             * لا نكتب العرض أو الهوامش داخل HTML.
             * CSS هو المسؤول عن:
             * 280px عند فتح القائمة على الكمبيوتر،
             * 80px عند طيها،
             * و100% على الهاتف والتابلت.
             */
            mainContent.style.removeProperty('width');
            mainContent.style.removeProperty('margin-right');
            mainContent.style.removeProperty('margin-left');
        }

        // دالة لإرسال التفضيلات وحفظها في السيرفر (والحفظ المحلي الفوري)
        function saveSidebarStateToServer(isCollapsed) {
            // ✨ 1. الحفظ المحلي الفوري (السحر هنا لمنع ارتداد القائمة عند التنقل السريع بين التبويبات)
            const uId = window.currentUserId || '0';
            document.cookie = `compactMode_${uId}=${isCollapsed ? 'enabled' : 'disabled'}; path=/; max-age=${365 * 24 * 60 * 60}`;

            // 2. المزامنة مع السيرفر لتبقى محفوظة لو سجل دخوله من جهاز آخر
            const saveUrl = window.location.pathname.toLowerCase().includes('/owner/')
                ? '/Owner/Profile/SetPreference'
                : '/Admin/Profile/SavePreference';

            fetch(saveUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ Key: 'CompactMode', Value: isCollapsed })
            }).catch(() => console.warn('فشل حفظ حالة القائمة المدمجة'));
        }

        // 🔘 حدث الضغط اليدوي على السهم الأصلي 
        desktopToggle.addEventListener('click', function () {
            const isCollapsed = sidebar.classList.toggle('sidebar-collapsed');
            updateLayoutDimensions(isCollapsed);
            saveSidebarStateToServer(isCollapsed);

            // إدارة العداد بناءً على حركة السهم
            if (isCollapsed) {
                clearTimeout(inactivityTimer); // إذا أغلقت يدوياً نوقف العداد
            } else {
                resetInactivityTimer(); // إذا فتحت يدوياً نبدأ عداد الخمول من جديد
            }
        });

        // 💤 ميزة الإغلاق التلقائي عند الخمول
        function collapseSidebarOnInactivity() {
            // تقفل من تلقاء نفسها فقط وفقط إذا كانت مفتوحة بالكامل
            if (!sidebar.classList.contains('sidebar-collapsed')) {
                sidebar.classList.add('sidebar-collapsed');
                updateLayoutDimensions(true);
                saveSidebarStateToServer(true);
                console.log('%c[YemenGuide]: تم طي القائمة تلقائياً بسبب خمول الماوس لتوسيع مساحة العمل.', 'color: #00A6DA; font-weight: bold;');
            }
        }

        // دالة تصفير وإعادة تشغيل العداد عند حركة المستخدم
        function resetInactivityTimer() {
            clearTimeout(inactivityTimer);

            // إذا كانت القائمة مغلقة أصلاً، لا نحتاج لتشغيل العداد
            if (sidebar.classList.contains('sidebar-collapsed')) return;

            inactivityTimer = setTimeout(collapseSidebarOnInactivity, inactivityTimeoutDuration);
        }

        // الاستماع لحركات الماوس واللوحة لتصفير العداد 
        ['mousemove', 'mousedown', 'keypress', 'touchstart', 'scroll'].forEach(eventName => {
            document.addEventListener(eventName, function () {
                resetInactivityTimer();
            });
        });

        // حماية ذكية: إذا كان الماوس حركياً داخل القائمة الجانبية نفسها، نوقف العداد تماماً ولا نغلقها
        sidebar.addEventListener('mouseenter', () => clearTimeout(inactivityTimer));
        sidebar.addEventListener('mouseleave', () => resetInactivityTimer());

        // تشغيل المحرك الذكي عند بداية فتح الصفحة
        resetInactivityTimer();
    }
});


// =============================================================
//  2.1 معالجة صور الهيدر بدون onerror داخل Razor
// =============================================================

function initAdminHeaderImageFallbacks() {
    const logo = document.querySelector('.sidebar-brand-logo');

    if (logo) {
        const fallbackSrc = logo.dataset.fallbackSrc || '/images/logo1.png';

        const applyLogoFallback = function () {
            if (logo.getAttribute('src') !== fallbackSrc) {
                logo.setAttribute('src', fallbackSrc);
            }
        };

        logo.addEventListener('error', applyLogoFallback, { once: true });

        if (logo.complete && logo.naturalWidth === 0) {
            applyLogoFallback();
        }
    }

    document.querySelectorAll('.header-avatar-image').forEach(function (image) {
        const circle = image.closest('.header-avatar-circle');
        if (!circle) return;

        const showInitial = function () {
            image.hidden = true;
            circle.classList.remove('has-image');
            circle.classList.add('is-initial');
        };

        image.addEventListener('error', showInitial, { once: true });

        if (image.complete && image.naturalWidth === 0) {
            showInitial();
        }
    });
}

// =============================================================
//  3. نظام الوضع الليلي
// =============================================================

function initDarkMode() {
    const toggles = document.querySelectorAll('.dark-mode-switch');
    const isDark = document.cookie.includes('darkMode=enabled');

    document.body.classList.toggle('dark-mode', isDark);
    toggles.forEach(t => t.checked = isDark);

    document.addEventListener('change', function (e) {
        if (e.target && e.target.classList.contains('dark-mode-switch')) {
            const shouldEnableDark = e.target.checked;
            document.querySelectorAll('.dark-mode-switch').forEach(t => t.checked = shouldEnableDark);

            if (shouldEnableDark) {
                document.body.classList.add('dark-mode');
                document.cookie = "darkMode=enabled; path=/; max-age=" + (365 * 24 * 60 * 60);
            } else {
                document.body.classList.remove('dark-mode');
                document.cookie = "darkMode=disabled; path=/; max-age=" + (365 * 24 * 60 * 60);
            }

            fetch('/Admin/Profile/SavePreference', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ Key: 'DarkModeEnabled', Value: shouldEnableDark })
            }).catch(() => { });
        }
    });
}


// =============================================================
//  4. القائمة الجانبية للموبايل
// =============================================================

function initMobileSidebar() {
    const mobileToggle = document.getElementById('mobileSidebarToggle');
    const mobileClose = document.getElementById('mobileSidebarClose');
    const sidebar = document.getElementById('dashboardSidebar');

    if (!mobileToggle || !sidebar) return;

    function openSidebar() {
        sidebar.classList.add('mobile-open');
        mobileToggle.setAttribute('aria-expanded', 'true');
    }

    function closeSidebar() {
        sidebar.classList.remove('mobile-open');
        mobileToggle.setAttribute('aria-expanded', 'false');
    }

    mobileToggle.addEventListener('click', function () {
        if (sidebar.classList.contains('mobile-open')) {
            closeSidebar();
        } else {
            openSidebar();
        }
    });

    if (mobileClose) {
        mobileClose.addEventListener('click', function () {
            closeSidebar();
            mobileToggle.focus();
        });
    }

    document.addEventListener('click', function (event) {
        if (window.innerWidth < 992) {
            const clickedOutsideSidebar =
                !sidebar.contains(event.target);

            const clickedOutsideToggle =
                !mobileToggle.contains(event.target);

            if (clickedOutsideSidebar && clickedOutsideToggle) {
                closeSidebar();
            }
        }
    });

    document.addEventListener('keydown', function (event) {
        if (
            event.key === 'Escape' &&
            window.innerWidth < 992 &&
            sidebar.classList.contains('mobile-open')
        ) {
            closeSidebar();
            mobileToggle.focus();
        }
    });
}

// =============================================================
//  5. محرك البحث الأساسي (Basic)
// =============================================================

function initGlobalSearch() {
    const searchInput = document.getElementById('globalSearch');
    if (searchInput) {
        searchInput.addEventListener('keyup', function () {
            if (this.value.length > 2) {
                // كود البحث الفعلي هنا
            }
        });
    }
}


// =============================================================
//  6. دالة الانتقال الذكي المباشر للتبويبات (Profile Tab Deep Linking)
// =============================================================

function initProfileTabDeepLinking() {
    function activateTabFromHash() {
        const hash = window.location.hash;
        if (hash) {
            const tabButtonId = hash.replace('#', '') + '-tab';
            const tabButton = document.getElementById(tabButtonId);
            if (tabButton) tabButton.click();
        }
    }
    activateTabFromHash();
    window.addEventListener('hashchange', activateTabFromHash);
}


// =============================================================
//  7. تبديل لغة النظام (Localization)
// =============================================================

const languageSelect = document.getElementById('languageSelect');
if (languageSelect) {
    languageSelect.addEventListener('change', function () {
        const selectedCulture = this.value;

        Swal.fire({
            title: 'جاري تغيير لغة النظام...',
            text: 'يرجى الانتظار لحين إعادة تهيئة الواجهة',
            allowOutsideClick: false,
            showConfirmButton: false,
            didOpen: () => { Swal.showLoading(); }
        });

        fetch('/Language/SetLanguage', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Culture: selectedCulture })
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    window.location.reload();
                } else {
                    Swal.fire('خطأ', data.message || 'حدث خطأ غير متوقع', 'error');
                }
            })
            .catch(() => { Swal.fire('خطأ', 'فشل الاتصال بالخادم', 'error'); });
    });
}


// =============================================================
//  8. محرك البحث الفوري الذكي (Live Global Search)
// =============================================================

const searchInputLive = document.getElementById('globalSearch');
const searchWrapper = document.querySelector('.search-wrapper');

if (searchInputLive && searchWrapper) {
    const resultsDropdown = document.createElement('div');
    resultsDropdown.className = 'search-results-dropdown';
    resultsDropdown.style.display = 'none';
    searchWrapper.appendChild(resultsDropdown);

    let timeout = null;

    searchInputLive.addEventListener('input', function () {
        clearTimeout(timeout);
        const query = this.value.trim();

        if (query.length < 2) {
            resultsDropdown.style.display = 'none';
            return;
        }

        timeout = setTimeout(() => {
            fetch(`/Admin/AdminDashboard/GlobalSearch?query=${encodeURIComponent(query)}`)
                .then(res => res.json())
                .then(data => {
                    resultsDropdown.innerHTML = '';

                    if (data.success && data.results.length > 0) {
                        data.results.forEach(item => {
                            const itemHtml = `
                                    <a href="${item.url}" class="search-result-item">
                                        <i class="bi ${item.icon} result-icon"></i>
                                        <div class="result-info">
                                            <span class="result-title">${item.title}</span>
                                            <span class="result-type">${item.type}</span>
                                        </div>
                                    </a>`;
                            resultsDropdown.insertAdjacentHTML('beforeend', itemHtml);
                        });
                    } else {
                        resultsDropdown.innerHTML = '<div class="search-no-results"><i class="bi bi-search-heart me-2"></i> لا توجد نتائج مطابقة للبحث...</div>';
                    }
                    resultsDropdown.style.display = 'block';
                })
                .catch(() => console.warn('فشل جلب نتائج البحث الفوري.'));
        }, 300);
    });

    document.addEventListener('click', function (e) {
        if (!searchWrapper.contains(e.target)) {
            resultsDropdown.style.display = 'none';
        }
    });

    searchInputLive.addEventListener('focus', function () {
        if (this.value.trim().length >= 2) {
            resultsDropdown.style.display = 'block';
        }
    });
}


// ============================================================================
// YemenGuide Living Luxe V4
// تحسين بصري/حركي فقط. لا يغير روابط القائمة أو اختيار المنشأة أو البحث الحالي.
// ============================================================================
(function () {
    "use strict";

    function initLivingLuxeV4() {
        const body = document.body;
        const sidebar = document.getElementById('dashboardSidebar');
        const orb = document.getElementById('ygLuxeNavOrb');
        const rail = document.getElementById('ygLuxeNavRail');
        const trail = document.getElementById('ygLuxeNavTrail');
        const navLinks = sidebar ? Array.from(sidebar.querySelectorAll('.sidebar-nav .nav-link')) : [];
        const activeLink = navLinks.find(link => link.classList.contains('active')) || null;

        // 1) الدائرة تتحرك فعليًا إلى مركز الصفحة النشطة، ومن موضع الصفحة السابقة بعد الـ reload.
        if (sidebar && orb && rail && activeLink) {
            let moveTimer = null;

            function getCenterY(link) {
                const railRect = rail.getBoundingClientRect();
                const linkRect = link.getBoundingClientRect();
                const orbHeight = orb.offsetHeight || 18;
                return Math.max(0, (linkRect.top - railRect.top) + (linkRect.height / 2) - (orbHeight / 2));
            }

            function moveOrbTo(link, animate, fromY) {
                if (!link) return;
                const nextY = getCenterY(link);
                const currentTransform = orb.style.transform;
                let currentY = Number.isFinite(fromY) ? fromY : parseFloat(orb.dataset.y || nextY);

                if (!animate) {
                    orb.style.transition = 'none';
                    orb.style.transform = `translate3d(-50%, ${nextY}px, 0)`;
                    orb.dataset.y = String(nextY);
                    requestAnimationFrame(() => orb.style.removeProperty('transition'));
                    return;
                }

                // Trail between old and new positions.
                if (trail && Number.isFinite(currentY) && Math.abs(nextY - currentY) > 3) {
                    const start = Math.min(currentY, nextY) + 8;
                    const distance = Math.abs(nextY - currentY);
                    trail.style.top = `${start}px`;
                    trail.style.height = `${Math.max(10, distance)}px`;
                    trail.classList.remove('is-moving');
                    void trail.offsetWidth;
                    trail.classList.add('is-moving');
                    clearTimeout(moveTimer);
                    moveTimer = setTimeout(() => trail.classList.remove('is-moving'), 760);
                }

                orb.style.transform = `translate3d(-50%, ${nextY}px, 0)`;
                orb.dataset.y = String(nextY);
            }

            const currentIndex = Math.max(0, navLinks.indexOf(activeLink));
            const previousIndex = Number.parseInt(sessionStorage.getItem('ygAdminPrevNavIndex') || '', 10);

            if (Number.isInteger(previousIndex) && previousIndex >= 0 && previousIndex < navLinks.length && previousIndex !== currentIndex) {
                const previousY = getCenterY(navLinks[previousIndex]);
                orb.style.transition = 'none';
                orb.style.transform = `translate3d(-50%, ${previousY}px, 0)`;
                orb.dataset.y = String(previousY);
                requestAnimationFrame(() => {
                    requestAnimationFrame(() => {
                        orb.style.removeProperty('transition');
                        moveOrbTo(activeLink, true, previousY);
                    });
                });
            } else {
                moveOrbTo(activeLink, false);
            }

            sessionStorage.setItem('ygAdminPrevNavIndex', String(currentIndex));
            activeLink.classList.add('is-arriving');
            window.setTimeout(() => activeLink.classList.remove('is-arriving'), 900);

            navLinks.forEach((link, index) => {
                link.addEventListener('click', () => {
                    sessionStorage.setItem('ygAdminPrevNavIndex', String(currentIndex));
                    sessionStorage.setItem('ygAdminNextNavIndex', String(index));
                }, { passive: true });
            });

            let resizeRaf = 0;
            const refresh = () => {
                cancelAnimationFrame(resizeRaf);
                resizeRaf = requestAnimationFrame(() => moveOrbTo(activeLink, false));
            };
            window.addEventListener('resize', refresh, { passive: true });
            window.addEventListener('orientationchange', refresh, { passive: true });

            // إعادة حساب الموضع بعد فتح/غلق القائمة أو انتهاء انتقالها.
            sidebar.addEventListener('transitionend', function (event) {
                if (event.propertyName === 'width' || event.propertyName === 'transform') refresh();
            });
        }

        // 2) نفس Parallax الكمبيوتر + التفاعل باللمس على الهاتف.
        const scene = sidebar ? sidebar.querySelector('.yg-luxe-sidebar-scene') : null;
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const desktopParallax = window.matchMedia('(min-width: 992px) and (hover: hover) and (pointer: fine)').matches;
        const mobileParallax = window.matchMedia('(max-width: 991.98px)').matches;

        if (sidebar && scene && !reduceMotion) {
            let raf = 0;

            function moveScene(clientX, clientY) {
                const rect = sidebar.getBoundingClientRect();
                const nx = ((clientX - rect.left) / Math.max(1, rect.width)) - .5;
                const ny = ((clientY - rect.top) / Math.max(1, rect.height)) - .5;

                cancelAnimationFrame(raf);
                raf = requestAnimationFrame(function () {
                    scene.style.setProperty('--yg-scene-x', `${(nx * 6).toFixed(2)}px`);
                    scene.style.setProperty('--yg-scene-y', `${(ny * 8).toFixed(2)}px`);
                });
            }

            function resetScene() {
                scene.style.setProperty('--yg-scene-x', '0px');
                scene.style.setProperty('--yg-scene-y', '0px');
            }

            if (desktopParallax) {
                sidebar.addEventListener('pointermove', function (event) {
                    moveScene(event.clientX, event.clientY);
                }, { passive: true });

                sidebar.addEventListener('pointerleave', resetScene, { passive: true });
            }

            if (mobileParallax) {
                sidebar.addEventListener('touchstart', function (event) {
                    const touch = event.touches && event.touches[0];
                    if (touch) moveScene(touch.clientX, touch.clientY);
                }, { passive: true });

                sidebar.addEventListener('touchmove', function (event) {
                    const touch = event.touches && event.touches[0];
                    if (touch) moveScene(touch.clientX, touch.clientY);
                }, { passive: true });

                sidebar.addEventListener('touchend', resetScene, { passive: true });
                sidebar.addEventListener('touchcancel', resetScene, { passive: true });
            }
        }

        // 3) بحث الهاتف: Esc يعيده أيقونة فورًا، بلا مودالات أو تأخير.
        const globalSearch = document.getElementById('globalSearch');
        if (globalSearch) {
            globalSearch.addEventListener('keydown', function (event) {
                if (event.key === 'Escape') {
                    globalSearch.value = '';
                    globalSearch.blur();
                }
            });
        }

        // 4) Ctrl/Cmd + K سريع للبحث، دون تغيير محرك البحث الموجود.
        document.addEventListener('keydown', function (event) {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k' && globalSearch) {
                event.preventDefault();
                globalSearch.focus({ preventScroll: true });
            }
        });

        // marker for debugging / future scoped refinements
        body.dataset.ygLivingLuxe = 'v4';
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initLivingLuxeV4, { once: true });
    } else {
        initLivingLuxeV4();
    }
}());


// ============================================================================
// Admin header layout
// مساحة الهيدر أصبحت CSS-only في admin-layout.css.
// لا يوجد MutationObserver ولا قياس متكرر للهيدر، لتقليل Reflow وتحسين السلاسة.
// ============================================================================


// ============================================================================
// Admin mobile sidebar backdrop sync - إضافة بصرية فقط
// ============================================================================
(function () {
    "use strict";

    function initAdminBackdropSync() {
        const sidebar = document.getElementById("dashboardSidebar");
        const toggle = document.getElementById("mobileSidebarToggle");
        const backdrop = document.getElementById("mobileSidebarBackdrop");

        if (!sidebar || !backdrop) return;

        function sync() {
            const open =
                window.innerWidth < 992 &&
                sidebar.classList.contains("mobile-open");

            backdrop.classList.toggle("is-visible", open);
            backdrop.setAttribute("aria-hidden", open ? "false" : "true");
            document.body.classList.toggle("mobile-sidebar-open", open);
        }

        const observer = new MutationObserver(sync);
        observer.observe(sidebar, {
            attributes: true,
            attributeFilter: ["class"]
        });

        backdrop.addEventListener("click", function () {
            sidebar.classList.remove("mobile-open");

            if (toggle) {
                toggle.setAttribute("aria-expanded", "false");
                toggle.focus({ preventScroll: true });
            }

            sync();
        });

        window.addEventListener("resize", sync, { passive: true });
        window.addEventListener("orientationchange", sync, { passive: true });

        sync();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAdminBackdropSync, { once: true });
    } else {
        initAdminBackdropSync();
    }
}());


// =============================================================================
// 10. مركز إشعارات الإدارة — Phase 1
// نفس تجربة العميل مع هوية الأدمن والصلاحيات الموزعة من الخادم.
// =============================================================================
(function () {
    'use strict';

    const ADMIN_NOTIFICATION_POLL_MS = 60000;

    const adminNotifications = {
        center: null,
        toggle: null,
        panel: null,
        body: null,
        badge: null,
        liveDot: null,
        unreadPill: null,
        unreadText: null,
        markAllButton: null,
        refreshButton: null,
        progress: null,
        footText: null,
        liveRegion: null,
        antiForgeryToken: '',
        initialized: false,
        loading: false,
        loadedOnce: false,
        unreadCount: 0,
        lastKnownUnreadCount: null,
        pollTimer: null,
        activeController: null,

        endpoints: {
            header: '/Notifications/Header',
            unread: '/Notifications/UnreadCount',
            markRead: '/Notifications/MarkAsRead',
            markAll: '/Notifications/MarkAllAsRead'
        },

        typePresentation: {
            reportcreated: { icon: 'bi-shield-exclamation', tone: 'cyan', label: 'قضية' },
            reportreply: { icon: 'bi-chat-left-text', tone: 'cyan', label: 'قضية' },
            reportescalated: { icon: 'bi-arrow-up-circle-fill', tone: 'amber', label: 'قضية' },
            reportwithdrawn: { icon: 'bi-arrow-return-left', tone: 'slate', label: 'قضية' },
            reportresolved: { icon: 'bi-check2-circle', tone: 'green', label: 'قضية' },
            reportrejected: { icon: 'bi-shield-x', tone: 'coral', label: 'قضية' },
            admincaseactionrequired: { icon: 'bi-shield-exclamation', tone: 'amber', label: 'إجراء قضية' },

            paymentrequired: { icon: 'bi-wallet2', tone: 'amber', label: 'مالية' },
            paymentreceived: { icon: 'bi-receipt-cutoff', tone: 'cyan', label: 'مالية' },
            paymentapproved: { icon: 'bi-check-circle-fill', tone: 'green', label: 'مالية' },
            paymentrejected: { icon: 'bi-exclamation-circle-fill', tone: 'coral', label: 'مالية' },
            refundrequested: { icon: 'bi-arrow-counterclockwise', tone: 'amber', label: 'استرداد' },
            refundreadyfortransfer: { icon: 'bi-lightning-charge-fill', tone: 'amber', label: 'استرداد' },
            refundtransferred: { icon: 'bi-send-check-fill', tone: 'cyan', label: 'استرداد' },
            refundcompleted: { icon: 'bi-cash-coin', tone: 'green', label: 'استرداد' },
            adminfinanceactionrequired: { icon: 'bi-cash-stack', tone: 'amber', label: 'إجراء مالي' },

            advertisement: { icon: 'bi-megaphone-fill', tone: 'amber', label: 'إعلان' },
            promotion: { icon: 'bi-percent', tone: 'amber', label: 'إعلان' },
            adminadvertisementactionrequired: { icon: 'bi-megaphone-fill', tone: 'amber', label: 'مراجعة إعلان' },

            adminplaceactionrequired: { icon: 'bi-buildings-fill', tone: 'cyan', label: 'منشأة' },
            adminaccountactionrequired: { icon: 'bi-person-gear', tone: 'cyan', label: 'حساب' },
            account: { icon: 'bi-person-circle', tone: 'cyan', label: 'حساب' },

            security: { icon: 'bi-shield-lock-fill', tone: 'coral', label: 'أمان' },
            adminsystemalert: { icon: 'bi-gear-wide-connected', tone: 'coral', label: 'النظام' },
            general: { icon: 'bi-bell-fill', tone: 'slate', label: 'إشعار' },

            bookingcreated: { icon: 'bi-calendar-plus-fill', tone: 'cyan', label: 'حجز' },
            bookingaccepted: { icon: 'bi-calendar-check-fill', tone: 'green', label: 'حجز' },
            bookingconfirmed: { icon: 'bi-patch-check-fill', tone: 'green', label: 'حجز' },
            bookingrejected: { icon: 'bi-x-circle-fill', tone: 'coral', label: 'حجز' },
            bookingcancelled: { icon: 'bi-calendar-x-fill', tone: 'coral', label: 'حجز' },
            bookingexpired: { icon: 'bi-hourglass-split', tone: 'amber', label: 'حجز' },
            reviewcreated: { icon: 'bi-star-fill', tone: 'amber', label: 'تقييم' },
            reviewreply: { icon: 'bi-chat-square-quote-fill', tone: 'cyan', label: 'تقييم' },
            message: { icon: 'bi-chat-dots-fill', tone: 'cyan', label: 'رسالة' },
            chatmessage: { icon: 'bi-chat-dots-fill', tone: 'cyan', label: 'رسالة' }
        },

        init() {
            this.center = document.getElementById('adminNotificationCenter');

            if (!this.center || this.initialized) {
                return;
            }

            this.initialized = true;
            this.toggle = document.getElementById('adminNotificationToggle');
            this.panel = document.getElementById('adminNotificationPanel');
            this.body = document.getElementById('adminNotificationBody');
            this.badge = document.getElementById('adminNotificationBadge');
            this.liveDot = document.getElementById('adminNotificationLiveDot');
            this.unreadPill = document.getElementById('adminNotificationUnreadPill');
            this.unreadText = document.getElementById('adminNotificationUnreadText');
            this.markAllButton = document.getElementById('adminNotificationMarkAll');
            this.refreshButton = document.getElementById('adminNotificationRefresh');
            this.progress = document.getElementById('adminNotificationProgress');
            this.footText = document.getElementById('adminNotificationFootText');
            this.liveRegion = document.getElementById('adminNotificationLiveRegion');

            this.endpoints.header =
                this.center.dataset.headerEndpoint || this.endpoints.header;
            this.endpoints.unread =
                this.center.dataset.unreadEndpoint || this.endpoints.unread;
            this.endpoints.markRead =
                this.center.dataset.markReadEndpoint || this.endpoints.markRead;
            this.endpoints.markAll =
                this.center.dataset.markAllEndpoint || this.endpoints.markAll;

            const tokenInput =
                this.center.querySelector('input[name="__RequestVerificationToken"]');

            this.antiForgeryToken =
                tokenInput instanceof HTMLInputElement
                    ? tokenInput.value
                    : '';

            this.bindEvents();
            this.showSkeleton();

            const isCenterPage =
                Boolean(document.getElementById('adminNotificationCenterPage'));

            if (!isCenterPage) {
                void this.load({ animateNew: true });
            } else {
                void this.refreshUnreadCount();
            }

            this.startPolling();
        },

        bindEvents() {
            this.center?.addEventListener('shown.bs.dropdown', () => {
                void this.load({ animateNew: false, force: true });
            });

            this.refreshButton?.addEventListener('click', event => {
                event.preventDefault();
                event.stopPropagation();
                void this.load({ animateNew: false, force: true });
            });

            this.markAllButton?.addEventListener('click', event => {
                event.preventDefault();
                event.stopPropagation();
                void this.markAllAsRead();
            });

            this.body?.addEventListener('click', event => {
                const retry = event.target.closest('[data-admin-notification-retry]');
                if (retry) {
                    event.preventDefault();
                    void this.load({ animateNew: false, force: true });
                    return;
                }

                const item = event.target.closest('[data-admin-notification-id]');
                if (!(item instanceof HTMLAnchorElement)) {
                    return;
                }

                if (item.classList.contains('is-unread')) {
                    item.classList.remove('is-unread');
                    item.querySelector('.admin-notification-unread-dot')?.remove();
                    this.setUnreadCount(Math.max(0, this.unreadCount - 1), {
                        celebrate: false
                    });
                }
            });

            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible') {
                    void this.refreshUnreadCount();
                    this.startPolling();
                } else {
                    this.stopPolling();
                }
            });

            window.addEventListener('pagehide', () => {
                this.stopPolling();
                this.activeController?.abort();
            });
        },

        startPolling() {
            this.stopPolling();

            if (document.visibilityState !== 'visible') {
                return;
            }

            this.pollTimer = window.setInterval(() => {
                void this.refreshUnreadCount();
            }, ADMIN_NOTIFICATION_POLL_MS);
        },

        stopPolling() {
            if (this.pollTimer) {
                window.clearInterval(this.pollTimer);
                this.pollTimer = null;
            }
        },

        async refreshUnreadCount() {
            try {
                const response = await fetch(this.endpoints.unread, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        Accept: 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    cache: 'no-store'
                });

                if (!response.ok) {
                    return;
                }

                const data = await response.json();
                const count = this.toNonNegativeInteger(
                    data.unreadCount ?? data.UnreadCount
                );

                const shouldCelebrate =
                    this.lastKnownUnreadCount !== null &&
                    count > this.lastKnownUnreadCount;

                if (shouldCelebrate) {
                    const delta = count - this.lastKnownUnreadCount;
                    this.announce(
                        delta === 1
                            ? 'لديك إشعار إداري جديد.'
                            : `لديك ${delta} إشعارات إدارية جديدة.`
                    );
                }

                this.setUnreadCount(count, {
                    celebrate: shouldCelebrate
                });
                this.lastKnownUnreadCount = count;
            } catch {
                // التحديث الدوري صامت عند ضعف الشبكة.
            }
        },

        async load(options = {}) {
            if (this.loading && !options.force) {
                return;
            }

            this.activeController?.abort();
            this.activeController = new AbortController();
            this.loading = true;
            this.setLoadingState(true);

            if (!this.loadedOnce) {
                this.showSkeleton();
            }

            try {
                const response = await fetch(`${this.endpoints.header}?take=7`, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        Accept: 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    cache: 'no-store',
                    signal: this.activeController.signal
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();
                const items = Array.isArray(data.items)
                    ? data.items
                    : (Array.isArray(data.Items) ? data.Items : []);

                const count = this.toNonNegativeInteger(
                    data.unreadCount ?? data.UnreadCount
                );

                const shouldCelebrate =
                    options.animateNew === true &&
                    this.lastKnownUnreadCount !== null &&
                    count > this.lastKnownUnreadCount;

                this.setUnreadCount(count, {
                    celebrate: shouldCelebrate
                });
                this.lastKnownUnreadCount = count;
                this.renderItems(items);
                this.loadedOnce = true;
                this.setFootMessage(
                    items.length > 0
                        ? `آخر ${items.length} إشعارات • تحديث تلقائي`
                        : 'لا توجد تنبيهات إدارية حاليًا'
                );
            } catch (error) {
                if (error?.name === 'AbortError') {
                    return;
                }

                console.warn('تعذر تحميل إشعارات الإدارة.', error);
                this.renderError();
                this.setFootMessage('تعذر التحديث مؤقتًا');
            } finally {
                this.loading = false;
                this.setLoadingState(false);
            }
        },

        async markAllAsRead() {
            if (this.unreadCount <= 0 || this.loading) {
                return;
            }

            this.markAllButton?.setAttribute('disabled', 'disabled');
            this.setLoadingState(true);

            try {
                const response = await fetch(this.endpoints.markAll, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: this.buildPostHeaders(),
                    body: ''
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                this.setUnreadCount(0, { celebrate: false });
                this.lastKnownUnreadCount = 0;

                this.body
                    ?.querySelectorAll('.admin-notification-item.is-unread')
                    .forEach(item => {
                        item.classList.remove('is-unread');
                        item.querySelector('.admin-notification-unread-dot')?.remove();
                    });

                this.setFootMessage('تم تعليم جميع الإشعارات كمقروءة');
                this.flashMarkAllSuccess();
                this.announce('تم تعليم جميع الإشعارات الإدارية كمقروءة.');

                window.dispatchEvent(
                    new CustomEvent('admin:notifications-read-all')
                );
            } catch (error) {
                console.warn('تعذر تنفيذ قراءة الكل.', error);
                this.setFootMessage('تعذر تنفيذ قراءة الكل');
            } finally {
                this.markAllButton?.removeAttribute('disabled');
                this.setLoadingState(false);
            }
        },

        renderItems(items) {
            if (!this.body) {
                return;
            }

            if (!items.length) {
                this.body.innerHTML = this.emptyStateTemplate();
                this.body.setAttribute('aria-busy', 'false');
                return;
            }

            const list = document.createElement('div');
            list.className = 'admin-notification-list';

            items.forEach((rawItem, index) => {
                const item = this.normalizeItem(rawItem);
                const presentation = this.getPresentation(item.notificationType);
                const anchor = document.createElement('a');

                anchor.className =
                    `admin-notification-item${item.isRead ? '' : ' is-unread'}`;
                anchor.dataset.adminNotificationId = String(item.notificationId);
                anchor.dataset.tone = presentation.tone;
                anchor.style.setProperty(
                    '--admin-notification-delay',
                    `${Math.min(index * 52, 260)}ms`
                );
                anchor.href =
                    item.openUrl ||
                    `/Notifications/Open/${encodeURIComponent(item.notificationId)}`;

                const safeTitle = this.escapeHtml(item.notificationTitle || 'إشعار');
                const safeText = this.escapeHtml(item.notificationText || '');
                const safeLabel = this.escapeHtml(presentation.label);
                const safeTime = this.escapeHtml(this.formatRelativeTime(item.createdAt));

                anchor.setAttribute(
                    'aria-label',
                    `${item.notificationTitle}. ${item.notificationText}`
                );

                anchor.innerHTML = `
                    <span class="admin-notification-item__icon" aria-hidden="true">
                        <i class="bi ${presentation.icon}"></i>
                    </span>

                    <span class="admin-notification-item__main">
                        <span class="admin-notification-item__title-row">
                            <span class="admin-notification-item__title">${safeTitle}</span>
                            ${item.isRead
                        ? ''
                        : '<span class="admin-notification-unread-dot" aria-hidden="true"></span>'}
                        </span>

                        <span class="admin-notification-item__text">${safeText}</span>

                        <span class="admin-notification-item__meta">
                            <span><i class="bi bi-clock"></i>${safeTime}</span>
                            <span aria-hidden="true">•</span>
                            <span>${safeLabel}</span>
                        </span>
                    </span>

                    <span class="admin-notification-item__arrow" aria-hidden="true">
                        <i class="bi bi-chevron-left"></i>
                    </span>
                `;

                list.appendChild(anchor);
            });

            this.body.replaceChildren(list);
            this.body.setAttribute('aria-busy', 'false');
        },

        renderError() {
            if (!this.body) {
                return;
            }

            this.body.innerHTML = `
                <div class="admin-notification-state">
                    <div class="admin-notification-state-card">
                        <div class="admin-notification-state-icon">
                            <i class="bi bi-cloud-slash"></i>
                        </div>
                        <h6 class="admin-notification-state-title">تعذر تحميل الإشعارات</h6>
                        <p class="admin-notification-state-text">
                            تعذر الوصول إلى مركز الإشعارات مؤقتًا. بقية لوحة الإدارة تعمل بشكل طبيعي.
                        </p>
                        <button type="button"
                                class="admin-notification-retry"
                                data-admin-notification-retry>
                            <i class="bi bi-arrow-clockwise"></i>
                            إعادة المحاولة
                        </button>
                    </div>
                </div>
            `;
            this.body.setAttribute('aria-busy', 'false');
        },

        showSkeleton() {
            if (!this.body) {
                return;
            }

            this.body.setAttribute('aria-busy', 'true');
            this.body.innerHTML = `
                <div class="admin-notification-skeletons" aria-hidden="true">
                    ${Array.from({ length: 4 }).map(() => `
                        <div class="admin-notification-skeleton">
                            <span class="admin-notification-skeleton__icon"></span>
                            <span class="admin-notification-skeleton__lines">
                                <span></span><span></span><span></span>
                            </span>
                        </div>
                    `).join('')}
                </div>
            `;
        },

        emptyStateTemplate() {
            return `
                <div class="admin-notification-state">
                    <div class="admin-notification-state-card">
                        <div class="admin-notification-state-icon">
                            <i class="bi bi-bell-slash"></i>
                        </div>
                        <h6 class="admin-notification-state-title">كل شيء هادئ</h6>
                        <p class="admin-notification-state-text">
                            لا توجد إشعارات مرتبطة بصلاحياتك حاليًا. عندما يحتاج قسمك إجراءً سيظهر هنا مباشرة.
                        </p>
                    </div>
                </div>
            `;
        },

        setUnreadCount(count, options = {}) {
            const normalized = this.toNonNegativeInteger(count);
            const previous = this.unreadCount;
            this.unreadCount = normalized;
            const displayValue = normalized > 99 ? '99+' : String(normalized);

            if (this.badge) {
                this.badge.textContent = displayValue;
                this.badge.hidden = normalized === 0;
            }

            if (this.liveDot) {
                this.liveDot.hidden = normalized === 0;
            }

            if (this.unreadPill) {
                this.unreadPill.hidden = normalized === 0;
            }

            if (this.unreadText) {
                this.unreadText.textContent = displayValue;
            }

            if (this.markAllButton) {
                this.markAllButton.hidden = normalized === 0;
            }

            if (this.toggle) {
                this.toggle.classList.toggle('has-unread', normalized > 0);
                this.toggle.setAttribute(
                    'aria-label',
                    normalized > 0
                        ? `فتح مركز الإشعارات، لديك ${normalized} غير مقروءة`
                        : 'فتح مركز الإشعارات'
                );
            }

            if (options.celebrate && normalized > previous) {
                this.celebrateBell();
            }
        },

        celebrateBell() {
            if (!this.toggle) {
                return;
            }

            this.toggle.classList.remove('is-celebrating');
            void this.toggle.offsetWidth;
            this.toggle.classList.add('is-celebrating');

            window.setTimeout(() => {
                this.toggle?.classList.remove('is-celebrating');
            }, 1000);
        },

        flashMarkAllSuccess() {
            if (!this.markAllButton) {
                return;
            }

            const originalHtml = this.markAllButton.innerHTML;
            this.markAllButton.innerHTML =
                '<i class="bi bi-check2-circle"></i><span>تمت القراءة</span>';

            window.setTimeout(() => {
                if (this.markAllButton) {
                    this.markAllButton.innerHTML = originalHtml;
                }
            }, 1400);
        },

        setLoadingState(isLoading) {
            this.progress?.classList.toggle('is-active', Boolean(isLoading));
            this.refreshButton?.classList.toggle('is-spinning', Boolean(isLoading));
        },

        setFootMessage(message) {
            if (this.footText) {
                this.footText.textContent = message;
            }
        },

        announce(message) {
            if (!this.liveRegion) {
                return;
            }

            this.liveRegion.textContent = '';
            window.setTimeout(() => {
                if (this.liveRegion) {
                    this.liveRegion.textContent = message;
                }
            }, 20);
        },

        normalizeItem(item) {
            return {
                notificationId: Number(
                    item.notificationId ?? item.NotificationId ?? 0
                ),
                notificationTitle: String(
                    item.notificationTitle ?? item.NotificationTitle ?? 'إشعار'
                ),
                notificationText: String(
                    item.notificationText ?? item.NotificationText ?? ''
                ),
                notificationType: String(
                    item.notificationType ?? item.NotificationType ?? 'General'
                ),
                isRead: Boolean(
                    item.isRead ?? item.IsRead ??
                    String(
                        item.notificationState ?? item.NotificationState ?? ''
                    ).toLowerCase() === 'read'
                ),
                createdAt: item.createdAt ?? item.CreatedAt ?? null,
                openUrl: item.openUrl ?? item.OpenUrl ?? null
            };
        },

        getPresentation(type) {
            const key = String(type || 'General')
                .replace(/[^a-z0-9]/gi, '')
                .toLowerCase();

            return this.typePresentation[key] || this.typePresentation.general;
        },

        formatRelativeTime(value) {
            if (!value) {
                return 'الآن';
            }

            const date = new Date(value);
            if (Number.isNaN(date.getTime())) {
                return 'الآن';
            }

            const seconds = Math.max(
                0,
                Math.floor((Date.now() - date.getTime()) / 1000)
            );

            if (seconds < 45) return 'الآن';

            const minutes = Math.floor(seconds / 60);
            if (minutes < 60) {
                return minutes === 1
                    ? 'منذ دقيقة'
                    : `منذ ${this.toArabicNumber(minutes)} دقائق`;
            }

            const hours = Math.floor(minutes / 60);
            if (hours < 24) {
                return hours === 1
                    ? 'منذ ساعة'
                    : `منذ ${this.toArabicNumber(hours)} ساعات`;
            }

            const days = Math.floor(hours / 24);
            if (days === 1) return 'أمس';
            if (days < 7) return `منذ ${this.toArabicNumber(days)} أيام`;

            try {
                return new Intl.DateTimeFormat('ar-YE', {
                    day: 'numeric',
                    month: 'short'
                }).format(date);
            } catch {
                return date.toLocaleDateString();
            }
        },

        toArabicNumber(value) {
            try {
                return new Intl.NumberFormat('ar-YE', {
                    useGrouping: false
                }).format(value);
            } catch {
                return String(value);
            }
        },

        toNonNegativeInteger(value) {
            const number = Number(value);
            return Number.isFinite(number)
                ? Math.max(0, Math.floor(number))
                : 0;
        },

        buildPostHeaders() {
            const headers = {
                Accept: 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            };

            if (this.antiForgeryToken) {
                headers.RequestVerificationToken = this.antiForgeryToken;
            }

            return headers;
        },

        escapeHtml(value) {
            const element = document.createElement('div');
            element.textContent = String(value ?? '');
            return element.innerHTML;
        }
    };

    window.AdminNotifications = adminNotifications;

    function initAdminNotificationCenterPage() {
        const page = document.getElementById('adminNotificationCenterPage');
        if (!page) {
            return;
        }

        const endpoint =
            page.dataset.centerDataEndpoint || '/Admin/Notifications/Data';
        const markAllEndpoint =
            page.dataset.markAllEndpoint || '/Notifications/MarkAllAsRead';
        const pageSize = Math.max(1, Number(page.dataset.pageSize || 12));
        const list = document.getElementById('adminNotificationCenterList');
        const pagination = document.getElementById('adminNotificationCenterPagination');
        const sectionTitle = document.getElementById('adminNotificationCenterSectionTitle');
        const resultCount = document.getElementById('adminNotificationCenterResultCount');
        const markAllButton = document.getElementById('adminNotificationCenterMarkAll');
        const tabs = Array.from(page.querySelectorAll('[data-admin-notification-tab]'));
        const token =
            page.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

        const titleByFilter = {
            all: 'جميع الإشعارات',
            unread: 'غير المقروءة',
            case: 'البلاغات والقضايا',
            finance: 'المالية',
            advertisement: 'الإعلانات',
            management: 'المنشآت والحسابات',
            system: 'النظام والأمان'
        };

        let currentFilter = normalizeFilter(
            page.dataset.currentFilter ||
            new URLSearchParams(window.location.search).get('filter') ||
            'all'
        );
        let currentPage = Math.max(
            1,
            Number(
                page.dataset.currentPage ||
                new URLSearchParams(window.location.search).get('page') ||
                1
            )
        );
        let requestController = null;
        let requestSequence = 0;

        function normalizeFilter(value) {
            const normalized = String(value || '').trim().toLowerCase();
            return Object.prototype.hasOwnProperty.call(titleByFilter, normalized)
                ? normalized
                : 'all';
        }

        function getValue(data, camel, pascal) {
            return data?.[camel] ?? data?.[pascal];
        }

        function getCount(data, camel, pascal) {
            return adminNotifications.toNonNegativeInteger(
                getValue(data, camel, pascal) ?? 0
            );
        }

        function setText(id, value) {
            const element = document.getElementById(id);
            if (element) {
                element.textContent = String(value);
            }
        }

        function setActiveTab(filter) {
            tabs.forEach(tab => {
                const active =
                    normalizeFilter(tab.dataset.filter) === filter;
                tab.classList.toggle('active', active);
                tab.setAttribute('aria-current', active ? 'page' : 'false');

                if (active) {
                    tab.scrollIntoView({
                        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches
                            ? 'auto'
                            : 'smooth',
                        block: 'nearest',
                        inline: 'center'
                    });
                }
            });
        }

        function updateCounts(data) {
            const total = getCount(data, 'totalCount', 'TotalCount');
            const unread = getCount(data, 'unreadCount', 'UnreadCount');
            const cases = getCount(data, 'caseCount', 'CaseCount');
            const finance = getCount(data, 'financeCount', 'FinanceCount');
            const advertisements = getCount(data, 'advertisementCount', 'AdvertisementCount');
            const management = getCount(data, 'managementCount', 'ManagementCount');
            const system = getCount(data, 'systemCount', 'SystemCount');

            setText('adminNotificationCenterAllCount', total);
            setText('adminNotificationCenterUnreadTab', unread);
            setText('adminNotificationCenterCaseCount', cases);
            setText('adminNotificationCenterFinanceCount', finance);
            setText('adminNotificationCenterAdvertisementCount', advertisements);
            setText('adminNotificationCenterManagementCount', management);
            setText('adminNotificationCenterSystemCount', system);
            setText('adminNotificationCenterUnreadHero', unread);
            setText('adminNotificationCenterTotalHero', total);

            if (markAllButton) {
                markAllButton.classList.toggle('is-disabled', unread <= 0);
                markAllButton.disabled = unread <= 0;
                markAllButton.dataset.unreadCount = String(unread);
            }

            adminNotifications.setUnreadCount(unread, { celebrate: false });
            adminNotifications.lastKnownUnreadCount = unread;
        }

        function normalizeItem(rawItem) {
            return adminNotifications.normalizeItem(rawItem);
        }

        function renderItems(rawItems) {
            if (!list) return;

            const items = Array.isArray(rawItems) ? rawItems : [];
            if (!items.length) {
                list.innerHTML = `
                    <div class="admin-notification-center-empty">
                        <span class="admin-notification-center-empty__icon">
                            <i class="bi bi-bell-slash"></i>
                        </span>
                        <h3>لا توجد إشعارات في هذا القسم</h3>
                        <p>عندما يصل حدث مرتبط بصلاحياتك سيظهر هنا مباشرة.</p>
                    </div>
                `;
                return;
            }

            list.innerHTML = items.map((rawItem, index) => {
                const item = normalizeItem(rawItem);
                const presentation = adminNotifications.getPresentation(item.notificationType);
                const title = adminNotifications.escapeHtml(item.notificationTitle);
                const text = adminNotifications.escapeHtml(item.notificationText);
                const label = adminNotifications.escapeHtml(presentation.label);
                const time = adminNotifications.escapeHtml(
                    adminNotifications.formatRelativeTime(item.createdAt)
                );
                const href = adminNotifications.escapeHtml(
                    item.openUrl || `/Notifications/Open/${item.notificationId}`
                );

                return `
                    <a href="${href}"
                       class="admin-notification-center-item${item.isRead ? '' : ' is-unread'}"
                       data-admin-notification-center-item
                       data-notification-id="${item.notificationId}"
                       data-tone="${presentation.tone}"
                       style="--admin-center-delay:${Math.min(index * 38, 260)}ms">
                        <span class="admin-notification-center-item__icon" aria-hidden="true">
                            <i class="bi ${presentation.icon}"></i>
                        </span>
                        <span class="admin-notification-center-item__content">
                            <span class="admin-notification-center-item__top">
                                <span class="admin-notification-center-item__title-wrap">
                                    <strong>${title}</strong>
                                    ${item.isRead ? '' : '<span class="admin-notification-center-item__unread-dot" aria-hidden="true"></span>'}
                                </span>
                                <span class="admin-notification-center-item__type">${label}</span>
                            </span>
                            <span class="admin-notification-center-item__text">${text}</span>
                            <span class="admin-notification-center-item__meta">
                                <i class="bi bi-clock"></i>${time}
                            </span>
                        </span>
                        <span class="admin-notification-center-item__arrow" aria-hidden="true">
                            <i class="bi bi-chevron-left"></i>
                        </span>
                    </a>
                `;
            }).join('');
        }

        function renderPagination(data) {
            if (!pagination) return;

            const totalPages = getCount(data, 'totalPages', 'TotalPages');
            currentPage = Math.max(1, getCount(data, 'currentPage', 'CurrentPage') || 1);

            if (totalPages <= 1) {
                pagination.innerHTML = '';
                pagination.hidden = true;
                pagination.classList.add('is-hidden');
                return;
            }

            pagination.hidden = false;
            pagination.classList.remove('is-hidden');
            const numbers = [];
            for (let pageNumber = 1; pageNumber <= totalPages; pageNumber += 1) {
                if (
                    pageNumber === 1 ||
                    pageNumber === totalPages ||
                    Math.abs(pageNumber - currentPage) <= 1
                ) {
                    numbers.push(`
                        <button type="button"
                                class="admin-notification-center-pagination__page${pageNumber === currentPage ? ' active' : ''}"
                                data-admin-notification-page="${pageNumber}">
                            ${pageNumber}
                        </button>
                    `);
                } else if (
                    pageNumber === currentPage - 2 ||
                    pageNumber === currentPage + 2
                ) {
                    numbers.push('<span class="admin-notification-center-pagination__dots">…</span>');
                }
            }

            pagination.innerHTML = `
                <button type="button"
                        class="admin-notification-center-pagination__arrow${currentPage <= 1 ? ' is-disabled' : ''}"
                        data-admin-notification-page="${currentPage - 1}"
                        ${currentPage <= 1 ? 'disabled' : ''}>
                    <i class="bi bi-chevron-right"></i>
                </button>
                <div class="admin-notification-center-pagination__pages">
                    ${numbers.join('')}
                </div>
                <button type="button"
                        class="admin-notification-center-pagination__arrow${currentPage >= totalPages ? ' is-disabled' : ''}"
                        data-admin-notification-page="${currentPage + 1}"
                        ${currentPage >= totalPages ? 'disabled' : ''}>
                    <i class="bi bi-chevron-left"></i>
                </button>
            `;
        }

        function updateUrl(filter, pageNumber, replace = false) {
            const url = new URL(window.location.href);
            url.pathname = '/Admin/Notifications';
            url.searchParams.set('filter', filter);
            url.searchParams.set('page', String(pageNumber));
            url.searchParams.set('pageSize', String(pageSize));
            url.searchParams.delete('focus');

            history[replace ? 'replaceState' : 'pushState'](
                { adminNotificationFilter: filter, adminNotificationPage: pageNumber },
                '',
                url
            );
        }

        function showCenterLoading() {
            page.classList.add('is-loading');
            list?.setAttribute('aria-busy', 'true');
        }

        function hideCenterLoading() {
            page.classList.remove('is-loading');
            list?.setAttribute('aria-busy', 'false');
        }

        async function loadCenter(filter, pageNumber, options = {}) {
            filter = normalizeFilter(filter);
            pageNumber = Math.max(1, Number(pageNumber || 1));

            requestController?.abort();
            requestController = new AbortController();
            const sequence = ++requestSequence;
            showCenterLoading();

            try {
                const url = new URL(endpoint, window.location.origin);
                url.searchParams.set('filter', filter);
                url.searchParams.set('page', String(pageNumber));
                url.searchParams.set('pageSize', String(pageSize));

                const response = await fetch(url, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: {
                        Accept: 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    cache: 'no-store',
                    signal: requestController.signal
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();
                if (sequence !== requestSequence) {
                    return;
                }

                currentFilter = normalizeFilter(
                    getValue(data, 'filter', 'Filter') || filter
                );
                currentPage = Math.max(
                    1,
                    Number(getValue(data, 'currentPage', 'CurrentPage') || pageNumber)
                );

                updateCounts(data);
                setActiveTab(currentFilter);
                renderItems(getValue(data, 'items', 'Items') || []);
                renderPagination(data);

                if (sectionTitle) {
                    sectionTitle.textContent = titleByFilter[currentFilter];
                }

                if (resultCount) {
                    resultCount.innerHTML =
                        `<i class="bi bi-collection"></i>${getCount(data, 'filteredCount', 'FilteredCount')} نتيجة`;
                }

                if (options.updateHistory !== false) {
                    updateUrl(currentFilter, currentPage, options.replaceHistory === true);
                }
            } catch (error) {
                if (error?.name === 'AbortError') {
                    return;
                }

                console.warn('تعذر تحميل مركز إشعارات الإدارة.', error);
                if (list) {
                    list.innerHTML = `
                        <div class="admin-notification-center-empty is-error">
                            <span class="admin-notification-center-empty__icon">
                                <i class="bi bi-cloud-slash"></i>
                            </span>
                            <h3>تعذر تحميل الإشعارات</h3>
                            <p>تحقق من الاتصال ثم أعد المحاولة.</p>
                            <button type="button" data-admin-center-retry>
                                <i class="bi bi-arrow-clockwise"></i> إعادة المحاولة
                            </button>
                        </div>
                    `;
                }
            } finally {
                if (sequence === requestSequence) {
                    hideCenterLoading();
                }
            }
        }

        tabs.forEach(tab => {
            tab.addEventListener('click', event => {
                event.preventDefault();
                const filter = normalizeFilter(tab.dataset.filter);
                void loadCenter(filter, 1);
            });
        });

        pagination?.addEventListener('click', event => {
            const button = event.target.closest('[data-admin-notification-page]');
            if (!(button instanceof HTMLButtonElement) || button.disabled) {
                return;
            }

            const pageNumber = Number(button.dataset.adminNotificationPage);
            if (Number.isFinite(pageNumber) && pageNumber > 0) {
                void loadCenter(currentFilter, pageNumber);
            }
        });

        list?.addEventListener('click', event => {
            const retry = event.target.closest('[data-admin-center-retry]');
            if (retry) {
                event.preventDefault();
                void loadCenter(currentFilter, currentPage, { updateHistory: false });
                return;
            }

            const item = event.target.closest('[data-admin-notification-center-item]');
            if (item?.classList.contains('is-unread')) {
                item.classList.remove('is-unread');
                item.querySelector('.admin-notification-center-item__unread-dot')?.remove();
            }
        });

        markAllButton?.addEventListener('click', async () => {
            if (markAllButton.disabled || markAllButton.classList.contains('is-disabled')) {
                return;
            }

            markAllButton.disabled = true;
            markAllButton.classList.add('is-busy');

            try {
                const headers = {
                    Accept: 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                };
                if (token) {
                    headers.RequestVerificationToken = token;
                }

                const response = await fetch(markAllEndpoint, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers,
                    body: ''
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                adminNotifications.setUnreadCount(0, { celebrate: false });
                adminNotifications.lastKnownUnreadCount = 0;
                await loadCenter(currentFilter, currentPage, { updateHistory: false });
                void adminNotifications.load({ animateNew: false, force: true });
            } catch (error) {
                console.warn('تعذر تعليم إشعارات الإدارة كمقروءة.', error);
            } finally {
                markAllButton.classList.remove('is-busy');
            }
        });

        window.addEventListener('admin:notifications-read-all', () => {
            void loadCenter(currentFilter, currentPage, { updateHistory: false });
        });

        window.addEventListener('popstate', () => {
            const params = new URLSearchParams(window.location.search);
            void loadCenter(
                normalizeFilter(params.get('filter') || 'all'),
                Math.max(1, Number(params.get('page') || 1)),
                { updateHistory: false }
            );
        });

        setActiveTab(currentFilter);

        const focusNotificationId = Number(page.dataset.focusNotification || 0);
        if (focusNotificationId > 0) {
            window.setTimeout(() => {
                const target = page.querySelector(
                    `[data-notification-id="${focusNotificationId}"]`
                );
                if (target) {
                    target.classList.add('is-focused');
                    target.scrollIntoView({
                        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches
                            ? 'auto'
                            : 'smooth',
                        block: 'center'
                    });
                    window.setTimeout(() => {
                        target.classList.remove('is-focused');
                    }, 5200);
                }
            }, 260);
        }
    }

    function bootAdminNotifications() {
        adminNotifications.init();
        initAdminNotificationCenterPage();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bootAdminNotifications, {
            once: true
        });
    } else {
        bootAdminNotifications();
    }
}());

// ============================================================================
// Admin notification deep focus - Phase 2
// يبرز العنصر الحقيقي القادم من الإشعار بعد تحميل الصفحات/الـ partials ديناميكيًا.
// لا ينفذ أي إجراء إداري ولا يضغط أزرارًا تلقائيًا؛ يحدد العنصر ويمرر حدثًا فقط.
// ============================================================================
(function () {
    "use strict";

    const search = new URLSearchParams(window.location.search);
    const focusCandidates = [
        {
            kind: "topup",
            param: "focusTopUp",
            selectors: id => [
                `[data-topup-id="${id}"]`,
                `[data-commission-transaction-id="${id}"]`,
                `[data-transaction-id="${id}"]`,
                `[data-top-up-id="${id}"]`
            ]
        },
        {
            kind: "transaction",
            param: "focusTransaction",
            selectors: id => [
                `[data-commission-transaction-id="${id}"]`,
                `[data-transaction-id="${id}"]`
            ]
        },
        {
            kind: "advertisement",
            param: "focusAdvertisement",
            selectors: id => [
                `[data-advertisement-id="${id}"]`,
                `[data-ad-id="${id}"]`
            ]
        },
        {
            kind: "user",
            param: "focusUser",
            selectors: id => [
                `[data-user-id="${id}"]`
            ]
        },
        {
            kind: "place",
            param: "focusPlace",
            selectors: id => [
                `[data-place-id="${id}"]`
            ]
        }
    ];

    const requested = focusCandidates
        .map(item => ({ ...item, id: Number(search.get(item.param) || 0) }))
        .find(item => Number.isInteger(item.id) && item.id > 0);

    if (!requested) {
        return;
    }

    // إدارة المنشآت لديها Deep Link خاص بها داخل places-actions.js.
    // عند focusPlace نترك صفحة PlaceManagement لهذا المعالج فقط حتى لا
    // يضيف النظام العام Marker داخل <tr> ويكسر بنية الجدول وتوزيع الأعمدة.
    const currentPath = window.location.pathname.replace(/\/+$/, '').toLowerCase();
    if (
        requested.kind === 'place' &&
        currentPath === '/admin/places/placemanagement'
    ) {
        return;
    }

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const startedAt = Date.now();
    const maxWatchMs = 12000;
    let observer = null;
    let completed = false;
    let retryTimer = 0;

    function isElementVisible(element) {
        if (!(element instanceof HTMLElement)) return false;
        const style = window.getComputedStyle(element);
        return style.display !== 'none' &&
            style.visibility !== 'hidden' &&
            element.getClientRects().length > 0;
    }

    function findTarget() {
        for (const selector of requested.selectors(requested.id)) {
            const matches = Array.from(document.querySelectorAll(selector));
            const visible = matches.find(isElementVisible);
            if (visible) return visible;
            if (matches[0]) return matches[0];
        }

        // دعم آمن للروابط/الأزرار التي تحمل الرقم داخل href بدون الاعتماد على data-*.
        const routeHints = {
            topup: [`commissionTransactionId=${requested.id}`, `/TopUps/Details/${requested.id}`],
            transaction: [`commissionTransactionId=${requested.id}`, `/Transactions/Details/${requested.id}`],
            advertisement: [`advertisementId=${requested.id}`, `/Advertisements/Details/${requested.id}`],
            user: [`userId=${requested.id}`],
            place: [`placeId=${requested.id}`]
        }[requested.kind] || [];

        for (const hint of routeHints) {
            const links = Array.from(document.querySelectorAll('a[href], button[data-url], [data-href]'));
            const source = links.find(element => {
                const value = element.getAttribute('href') ||
                    element.getAttribute('data-url') ||
                    element.getAttribute('data-href') || '';
                return value.includes(hint);
            });

            if (source) {
                return source.closest('tr, .card, .list-group-item, .table-row, [role="row"]') || source;
            }
        }

        // بعض شاشات الأدمن القديمة تمرر المعرف داخل data-id أو onclick.
        // نقصر data-id على صفحة الوحدة الحالية حتى لا نبرز عنصرًا من نوع آخر بالخطأ.
        const path = window.location.pathname.toLowerCase();
        const pathMatchesKind =
            (requested.kind === 'topup' && path.includes('/admin/financials')) ||
            (requested.kind === 'transaction' && path.includes('/admin/financials')) ||
            (requested.kind === 'advertisement' && path.includes('/admin/advertisements')) ||
            (requested.kind === 'user' && path.includes('/admin/users')) ||
            (requested.kind === 'place' && path.includes('/admin/places'));

        if (pathMatchesKind) {
            const dataIdTarget = document.querySelector(`[data-id="${requested.id}"]`);
            if (dataIdTarget) {
                return dataIdTarget.closest('tr, .card, .list-group-item, .table-row, [role="row"]') || dataIdTarget;
            }

            const numericBoundary = new RegExp(`(?:^|\\D)${requested.id}(?:\\D|$)`);
            const inlineActions = Array.from(document.querySelectorAll('[onclick]'));
            const inlineTarget = inlineActions.find(element =>
                numericBoundary.test(element.getAttribute('onclick') || '')
            );

            if (inlineTarget) {
                return inlineTarget.closest('tr, .card, .list-group-item, .table-row, [role="row"]') || inlineTarget;
            }
        }

        return null;
    }

    function closestScrollable(element) {
        let current = element.parentElement;
        while (current && current !== document.body) {
            const style = window.getComputedStyle(current);
            const overflowY = style.overflowY;
            if ((overflowY === 'auto' || overflowY === 'scroll') &&
                current.scrollHeight > current.clientHeight + 8) {
                return current;
            }
            current = current.parentElement;
        }
        return null;
    }

    function addMarker(target) {
        if (target.querySelector(':scope > .yg-admin-notification-focus-marker')) {
            return;
        }

        const marker = document.createElement('span');
        marker.className = 'yg-admin-notification-focus-marker';
        marker.innerHTML = '<i class="bi bi-bell-fill" aria-hidden="true"></i><span>من الإشعار</span>';
        marker.setAttribute('aria-label', 'العنصر المفتوح من الإشعار');
        target.appendChild(marker);
    }

    function focusTarget(target) {
        completed = true;
        observer?.disconnect();
        window.clearTimeout(retryTimer);

        target.classList.add('yg-admin-notification-focus', 'yg-admin-notification-focus-pulse');
        addMarker(target);

        const scrollContainer = closestScrollable(target);
        const behavior = reduceMotion ? 'auto' : 'smooth';

        if (scrollContainer) {
            const containerRect = scrollContainer.getBoundingClientRect();
            const targetRect = target.getBoundingClientRect();
            const desiredTop =
                scrollContainer.scrollTop +
                (targetRect.top - containerRect.top) -
                Math.max(18, (scrollContainer.clientHeight - targetRect.height) / 2);

            scrollContainer.scrollTo({
                top: Math.max(0, desiredTop),
                behavior
            });
        } else {
            target.scrollIntoView({
                behavior,
                block: 'center',
                inline: 'nearest'
            });
        }

        try {
            target.setAttribute('tabindex', target.getAttribute('tabindex') || '-1');
            target.focus({ preventScroll: true });
        } catch (_) {
            // التركيز البصري كافٍ إذا لم يقبل العنصر focus.
        }

        window.dispatchEvent(new CustomEvent('yg:admin-notification-focus', {
            detail: {
                kind: requested.kind,
                id: requested.id,
                target
            }
        }));

        window.setTimeout(() => {
            target.classList.remove('yg-admin-notification-focus-pulse');
        }, reduceMotion ? 100 : 5200);
    }

    function tryResolve() {
        if (completed) return;

        const target = findTarget();
        if (target) {
            // ننتظر إطارين حتى تستقر أبعاد الـ partial/modal/table بعد التحميل.
            requestAnimationFrame(() => {
                requestAnimationFrame(() => focusTarget(target));
            });
            return;
        }

        if (Date.now() - startedAt >= maxWatchMs) {
            observer?.disconnect();
            return;
        }

        window.clearTimeout(retryTimer);
        retryTimer = window.setTimeout(tryResolve, 280);
    }

    function boot() {
        observer = new MutationObserver(() => {
            if (!completed) tryResolve();
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['class', 'hidden', 'style']
        });

        tryResolve();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot, { once: true });
    } else {
        boot();
    }
}());
