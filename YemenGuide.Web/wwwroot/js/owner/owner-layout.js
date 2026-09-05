/* OWNER_LAYOUT_V3_20260816_1751 - Mobile sidebar backdrop and polished controls */
/* OWNER_PLACE_FIX_V3 - النسخة المصححة التي تنقل قائمة الهاتف خارج الهيدر */
/*
 * YemenGuide - Owner Layout
 * انسخ محتوى هذا الملف كاملًا إلى:
 * wwwroot/js/owner/owner-layout.js
 */

/**
 * =============================================================
 * Owner Layout Scripts
 * جميع وظائف الملف المشترك الأصلية محفوظة هنا:
 * - الوضع الليلي
 * - القائمة الجانبية على الكمبيوتر والجوال
 * - الطي التلقائي بعد الخمول
 * - البحث العام والبحث الفوري
 * - الانتقال العميق لتبويبات الحساب
 * - تبديل اللغة
 * بالإضافة إلى اختيار المنشأة النشطة.
 * =============================================================
 */

// =============================================================
//  1. الأحداث والتفاعلات الفورية عند اكتمال تحميل الصفحة
// =============================================================

document.addEventListener('DOMContentLoaded', function () {
    initDarkMode();
    initMobileSidebar();
    initGlobalSearch();
    initProfileTabDeepLinking();
});


// =============================================================
//  2. سهم طي القائمة الجانبية والإغلاق الذاتي عند الخمول
// =============================================================

document.addEventListener('DOMContentLoaded', function () {
    const desktopToggle = document.getElementById('desktopSidebarToggle');
    const sidebar = document.getElementById('dashboardSidebar');
    const mainContent = document.querySelector('.dashboard-main');

    if (desktopToggle && sidebar) {
        let inactivityTimer = null;
        const inactivityTimeoutDuration = 7000;

        function updateLayoutDimensions(isCollapsed) {
            if (!mainContent) return;

            mainContent.style.removeProperty('width');
            mainContent.style.removeProperty('margin-right');
            mainContent.style.removeProperty('margin-left');
        }

        function saveSidebarStateToServer(isCollapsed) {
            const uId = window.currentUserId || '0';
            document.cookie = `compactMode_${uId}=${isCollapsed ? 'enabled' : 'disabled'}; path=/; max-age=${365 * 24 * 60 * 60}`;

            const saveUrl = window.location.pathname.toLowerCase().includes('/owner/')
                ? '/Owner/Profile/SetPreference'
                : '/Admin/Profile/SavePreference';

            fetch(saveUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ Key: 'CompactMode', Value: isCollapsed })
            }).catch(() => console.warn('فشل حفظ حالة القائمة المدمجة'));
        }

        desktopToggle.addEventListener('click', function () {
            const isCollapsed = sidebar.classList.toggle('sidebar-collapsed');
            updateLayoutDimensions(isCollapsed);
            saveSidebarStateToServer(isCollapsed);

            if (isCollapsed) {
                clearTimeout(inactivityTimer);
            } else {
                resetInactivityTimer();
            }
        });

        function collapseSidebarOnInactivity() {
            if (!sidebar.classList.contains('sidebar-collapsed')) {
                sidebar.classList.add('sidebar-collapsed');
                updateLayoutDimensions(true);
                saveSidebarStateToServer(true);
                console.log('%c[YemenGuide]: تم طي القائمة تلقائياً بسبب خمول الماوس لتوسيع مساحة العمل.', 'color: #00A6DA; font-weight: bold;');
            }
        }

        function resetInactivityTimer() {
            clearTimeout(inactivityTimer);

            if (sidebar.classList.contains('sidebar-collapsed')) return;

            inactivityTimer = setTimeout(collapseSidebarOnInactivity, inactivityTimeoutDuration);
        }

        // أداء: لا نراقب mousemove/scroll بشكل مستمر.
        ['pointerdown', 'keydown', 'touchstart'].forEach(eventName => {
            document.addEventListener(
                eventName,
                resetInactivityTimer,
                { passive: eventName !== 'keydown' }
            );
        });

        sidebar.addEventListener('mouseenter', () => clearTimeout(inactivityTimer));
        sidebar.addEventListener('mouseleave', () => resetInactivityTimer());

        resetInactivityTimer();
    }
});


// =============================================================
//  3. نظام الوضع الليلي
// =============================================================

function initDarkMode() {
    const toggles = document.querySelectorAll('.dark-mode-switch');
    const isDark = document.cookie.includes('darkMode=enabled');

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
    const backdrop = document.getElementById('mobileSidebarBackdrop');

    if (!mobileToggle || !sidebar) return;

    function setBackdrop(open) {
        if (!backdrop) return;
        backdrop.classList.toggle('is-visible', open);
        backdrop.setAttribute('aria-hidden', open ? 'false' : 'true');
    }

    function openSidebar() {
        sidebar.classList.add('mobile-open');
        mobileToggle.setAttribute('aria-expanded', 'true');
        document.body.classList.add('mobile-sidebar-open');
        setBackdrop(true);
    }

    function closeSidebar(restoreFocus = false) {
        sidebar.classList.remove('mobile-open');
        mobileToggle.setAttribute('aria-expanded', 'false');
        document.body.classList.remove('mobile-sidebar-open');
        setBackdrop(false);

        if (restoreFocus) {
            mobileToggle.focus({ preventScroll: true });
        }
    }

    mobileToggle.addEventListener('click', function (event) {
        event.preventDefault();
        event.stopPropagation();

        if (sidebar.classList.contains('mobile-open')) {
            closeSidebar(false);
        } else {
            openSidebar();
        }
    });

    if (mobileClose) {
        mobileClose.addEventListener('click', function () {
            closeSidebar(true);
        });
    }

    if (backdrop) {
        backdrop.addEventListener('click', function () {
            closeSidebar(false);
        });
    }

    document.addEventListener('keydown', function (event) {
        if (
            event.key === 'Escape' &&
            window.innerWidth < 992 &&
            sidebar.classList.contains('mobile-open')
        ) {
            closeSidebar(true);
        }
    });

    window.addEventListener('resize', function () {
        if (window.innerWidth >= 992 && sidebar.classList.contains('mobile-open')) {
            closeSidebar(false);
        }
    }, { passive: true });
}

// =============================================================
//  5. محرك البحث الأساسي
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
//  6. الانتقال الذكي المباشر لتبويبات الحساب
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
//  7. تبديل لغة النظام
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
//  8. محرك البحث الفوري الذكي
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


// =============================================================
//  9. اختيار المنشأة النشطة في هيدر المالك
// =============================================================

(function () {
    "use strict";

    const COOKIE_DAYS = 365;

    function getCookie(name) {
        const encodedName = encodeURIComponent(name) + "=";
        const cookies = document.cookie ? document.cookie.split(";") : [];

        for (let index = 0; index < cookies.length; index += 1) {
            const cookie = cookies[index].trim();

            if (cookie.indexOf(encodedName) === 0) {
                return decodeURIComponent(cookie.substring(encodedName.length));
            }
        }

        return "";
    }

    function setCookie(name, value, days) {
        const expiresAt = new Date();
        expiresAt.setTime(expiresAt.getTime() + (days * 24 * 60 * 60 * 1000));

        let cookie = encodeURIComponent(name)
            + "="
            + encodeURIComponent(value)
            + "; expires="
            + expiresAt.toUTCString()
            + "; path=/; SameSite=Lax";

        if (window.location.protocol === "https:") {
            cookie += "; Secure";
        }

        document.cookie = cookie;
    }

    function normalizeSearchValue(value) {
        return (value || "")
            .toString()
            .trim()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u064B-\u065F\u0670]/g, "")
            .replace(/[أإآ]/g, "ا")
            .replace(/ة/g, "ه")
            .replace(/ى/g, "ي")
            .replace(/\s+/g, " ");
    }

    function createSwitchingScreen(placeName) {
        const oldScreen = document.getElementById("ownerPlaceSwitchingScreen");

        if (oldScreen) {
            oldScreen.remove();
        }

        const screen = document.createElement("div");
        screen.id = "ownerPlaceSwitchingScreen";
        screen.className = "owner-place-switching-screen";
        screen.setAttribute("role", "status");
        screen.setAttribute("aria-live", "polite");

        const card = document.createElement("div");
        card.className = "owner-place-switching-card";

        const icon = document.createElement("span");
        icon.className = "owner-place-switching-icon";
        icon.innerHTML = "<i class=\"bi bi-buildings\"></i><span></span>";

        const copy = document.createElement("span");
        copy.className = "owner-place-switching-copy";

        const small = document.createElement("small");
        small.textContent = "جاري تغيير نطاق لوحة التحكم";

        const strong = document.createElement("strong");
        strong.textContent = placeName;

        copy.appendChild(small);
        copy.appendChild(strong);
        card.appendChild(icon);
        card.appendChild(copy);
        screen.appendChild(card);
        document.body.appendChild(screen);

        window.requestAnimationFrame(function () {
            screen.classList.add("is-visible");
        });
    }


    /**
     * يجعل منشأة معينة هي المنشأة النشطة في هيدر المالك.
     * تستخدمه صفحة إضافة المنشأة بعد نجاح الحفظ دون تغيير وظائف الهيدر الحالية.
     */
    window.setOwnerActivePlace = function (placeId, placeName, options) {
        const normalizedPlaceId = Number.parseInt(placeId || "0", 10);

        if (!normalizedPlaceId) {
            return false;
        }

        const settings = Object.assign({
            reload: true,
            delay: 420,
            showScreen: true
        }, options || {});

        const switcher = document.getElementById("ownerPlaceSwitcher");
        let cookieName = switcher?.dataset.cookieName || "";

        if (!cookieName) {
            const existingCookie = (document.cookie || "")
                .split(";")
                .map(cookie => cookie.trim())
                .find(cookie => decodeURIComponent(cookie).startsWith("ownerActivePlace_"));

            if (existingCookie) {
                cookieName = decodeURIComponent(existingCookie.split("=")[0]);
            }
        }

        if (!cookieName && window.currentUserId) {
            cookieName = `ownerActivePlace_${window.currentUserId}`;
        }

        if (!cookieName) {
            cookieName = "ownerActivePlace";
        }

        setCookie(cookieName, normalizedPlaceId.toString(), COOKIE_DAYS);

        const resolvedPlaceName = placeName || "المنشأة المختارة";

        document.dispatchEvent(new CustomEvent("owner:place-changing", {
            detail: {
                placeId: normalizedPlaceId.toString(),
                placeName: resolvedPlaceName
            }
        }));

        if (settings.showScreen) {
            createSwitchingScreen(resolvedPlaceName);
        }

        if (settings.reload) {
            window.setTimeout(function () {
                window.location.reload();
            }, Math.max(0, Number.parseInt(settings.delay || "0", 10)));
        }

        return true;
    };

    function initializeOwnerPlaceSwitcher() {
        const switcher = document.getElementById("ownerPlaceSwitcher");

        if (!switcher) {
            return;
        }

        const trigger = document.getElementById("ownerPlaceTrigger");
        const menu = document.getElementById("ownerPlaceMenu");
        const closeButton = document.getElementById("ownerPlaceMenuClose");
        const backdrop = document.getElementById("ownerPlaceBackdrop");
        const searchInput = document.getElementById("ownerPlaceSearch");
        const emptySearch = document.getElementById("ownerPlaceEmptySearch");
        const options = Array.from(switcher.querySelectorAll(".owner-place-option"));
        const cookieName = switcher.dataset.cookieName || "ownerActivePlace";
        const selectedPlaceId = switcher.dataset.selectedPlaceId || "";
        const placeCount = Number.parseInt(switcher.dataset.placeCount || "0", 10);

        // إذا كانت قيمة الكوكي قديمة أو غير صحيحة، يعتمد الهيدر أول منشأة صحيحة
        // ثم يصحح قيمة التفضيل تلقائيًا.
        if (selectedPlaceId && getCookie(cookieName) !== selectedPlaceId) {
            setCookie(cookieName, selectedPlaceId, COOKIE_DAYS);
        }

        if (!trigger || !menu || placeCount <= 1) {
            return;
        }

        let isChangingPlace = false;
        const originalMenuParent = menu.parentNode;
        const originalMenuNextSibling = menu.nextSibling;

        function syncMenuContainer() {
            const shouldUseMobileSheet = window.innerWidth <= 767;

            if (shouldUseMobileSheet) {
                if (menu.parentNode !== document.body) {
                    document.body.appendChild(menu);
                }

                menu.classList.add("is-mobile-sheet");
                return;
            }

            menu.classList.remove("is-mobile-sheet");

            if (menu.parentNode !== originalMenuParent) {
                originalMenuParent.insertBefore(menu, originalMenuNextSibling);
            }
        }

        function isOpen() {
            return switcher.classList.contains("is-open");
        }

        function openMenu() {
            if (isChangingPlace) {
                return;
            }

            syncMenuContainer();
            switcher.classList.add("is-open");
            menu.classList.add("is-open");
            trigger.setAttribute("aria-expanded", "true");
            menu.setAttribute("aria-hidden", "false");
            document.body.classList.add("owner-place-menu-open");

            if (backdrop) {
                backdrop.classList.add("is-visible");
                backdrop.setAttribute("aria-hidden", "false");
            }

            const selectedOption = menu.querySelector(".owner-place-option.is-selected");

            window.setTimeout(function () {
                if (searchInput && window.innerWidth >= 768) {
                    searchInput.focus();
                    return;
                }

                if (selectedOption) {
                    selectedOption.focus({ preventScroll: true });
                }
            }, 80);
        }

        function closeMenu(restoreFocus) {
            switcher.classList.remove("is-open");
            menu.classList.remove("is-open");
            trigger.setAttribute("aria-expanded", "false");
            menu.setAttribute("aria-hidden", "true");
            document.body.classList.remove("owner-place-menu-open");

            if (backdrop) {
                backdrop.classList.remove("is-visible");
                backdrop.setAttribute("aria-hidden", "true");
            }

            if (restoreFocus) {
                trigger.focus({ preventScroll: true });
            }
        }

        function filterPlaces() {
            const query = normalizeSearchValue(searchInput ? searchInput.value : "");
            let visibleCount = 0;

            options.forEach(function (option) {
                const searchableText = normalizeSearchValue(option.dataset.searchText || option.textContent);
                const isVisible = !query || searchableText.indexOf(query) !== -1;

                option.hidden = !isVisible;

                if (isVisible) {
                    visibleCount += 1;
                }
            });

            if (emptySearch) {
                emptySearch.hidden = visibleCount !== 0;
            }
        }

        function selectPlace(option) {
            if (isChangingPlace || !option) {
                return;
            }

            const placeId = option.dataset.placeId || "";
            const placeName = option.dataset.placeName || "المنشأة المختارة";

            if (!placeId) {
                return;
            }

            if (placeId === selectedPlaceId) {
                closeMenu(true);
                return;
            }

            isChangingPlace = true;
            switcher.classList.add("is-loading");

            options.forEach(function (item) {
                item.disabled = true;
            });

            setCookie(cookieName, placeId, COOKIE_DAYS);
            closeMenu(false);
            createSwitchingScreen(placeName);

            document.dispatchEvent(new CustomEvent("owner:place-changing", {
                detail: {
                    placeId: placeId,
                    placeName: placeName
                }
            }));

            window.setTimeout(function () {
                window.location.reload();
            }, 480);
        }

        trigger.addEventListener("click", function (event) {
            event.preventDefault();
            event.stopPropagation();

            if (isOpen()) {
                closeMenu(false);
            } else {
                openMenu();
            }
        });

        if (closeButton) {
            closeButton.addEventListener("click", function () {
                closeMenu(true);
            });
        }

        if (backdrop) {
            backdrop.addEventListener("click", function () {
                closeMenu(false);
            });
        }

        if (searchInput) {
            searchInput.addEventListener("input", filterPlaces);
            searchInput.addEventListener("search", filterPlaces);
        }

        options.forEach(function (option) {
            option.addEventListener("click", function () {
                selectPlace(option);
            });
        });

        document.addEventListener("click", function (event) {
            if (
                isOpen()
                && !switcher.contains(event.target)
                && !menu.contains(event.target)
            ) {
                closeMenu(false);
            }
        });

        document.addEventListener("keydown", function (event) {
            if (!isOpen()) {
                return;
            }

            if (event.key === "Escape") {
                event.preventDefault();
                closeMenu(true);
                return;
            }

            if (event.key !== "ArrowDown" && event.key !== "ArrowUp") {
                return;
            }

            const visibleOptions = options.filter(function (option) {
                return !option.hidden && !option.disabled;
            });

            if (!visibleOptions.length) {
                return;
            }

            event.preventDefault();

            const currentIndex = visibleOptions.indexOf(document.activeElement);
            const direction = event.key === "ArrowDown" ? 1 : -1;
            let nextIndex = currentIndex + direction;

            if (currentIndex === -1) {
                nextIndex = direction > 0 ? 0 : visibleOptions.length - 1;
            } else if (nextIndex < 0) {
                nextIndex = visibleOptions.length - 1;
            } else if (nextIndex >= visibleOptions.length) {
                nextIndex = 0;
            }

            visibleOptions[nextIndex].focus();
        });

        window.addEventListener("resize", function () {
            syncMenuContainer();

            if (isOpen() && window.innerWidth > 767 && backdrop) {
                backdrop.classList.remove("is-visible");
                backdrop.setAttribute("aria-hidden", "true");
            } else if (isOpen() && backdrop) {
                backdrop.classList.add("is-visible");
                backdrop.setAttribute("aria-hidden", "false");
            }
        });
    }

    function initialize() {
        initializeOwnerPlaceSwitcher();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize);
    } else {
        initialize();
    }
}());


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
            const previousIndex = Number.parseInt(sessionStorage.getItem('ygOwnerPrevNavIndex') || '', 10);

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

            sessionStorage.setItem('ygOwnerPrevNavIndex', String(currentIndex));
            activeLink.classList.add('is-arriving');
            window.setTimeout(() => activeLink.classList.remove('is-arriving'), 900);

            navLinks.forEach((link, index) => {
                link.addEventListener('click', () => {
                    sessionStorage.setItem('ygOwnerPrevNavIndex', String(currentIndex));
                    sessionStorage.setItem('ygOwnerNextNavIndex', String(index));
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
        // 2) تم تعطيل Parallax المتتبع للمؤشر لتحسين السلاسة.

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
// OWNER GLOBAL OVERLAY PORTAL FIX
// ينقل Bootstrap Modal/Offcanvas إلى body لحظة الفتح حتى لا يبقى داخل
// stacking context خاص بمحتوى الصفحة ويظهر خلف الهيدر الثابت.
// ============================================================================
(function initOwnerGlobalOverlayPortal() {
    "use strict";

    function moveOverlayToBody(element) {
        if (!element || element.parentElement === document.body) return;
        document.body.appendChild(element);
    }

    document.addEventListener("show.bs.modal", function (event) {
        const modal = event.target;
        if (modal && modal.classList && modal.classList.contains("modal")) {
            moveOverlayToBody(modal);
        }
    });

    document.addEventListener("show.bs.offcanvas", function (event) {
        const offcanvas = event.target;
        if (offcanvas && offcanvas.classList && offcanvas.classList.contains("offcanvas")) {
            moveOverlayToBody(offcanvas);
        }
    });
}());

// ============================================================================
// OWNER NOTIFICATIONS - PHASE 1
// جرس المالك: غير المقروء فقط + العداد + تعليم الكل + انتقال لمركز الإشعارات.
// ============================================================================
(function initOwnerNotificationsModule() {
    'use strict';

    const root = document.getElementById('ownerNotifications');
    if (!root) return;

    const toggle = document.getElementById('ownerNotificationToggle');
    const menu = document.getElementById('ownerNotificationMenu');
    const body = document.getElementById('ownerNotificationBody');
    const badge = document.getElementById('ownerNotificationBadge');
    const liveDot = document.getElementById('ownerNotificationLiveDot');
    const unreadPill = document.getElementById('ownerNotificationUnreadPill');
    const markAllButton = document.getElementById('ownerNotificationMarkAll');
    const progress = document.getElementById('ownerNotificationProgress');
    const token = document.querySelector('#ownerNotificationTokenForm input[name="__RequestVerificationToken"]')?.value || '';

    const endpoints = {
        header: root.dataset.headerEndpoint || '/Notifications/Header',
        unread: root.dataset.unreadEndpoint || '/Notifications/UnreadCount',
        markAll: root.dataset.markAllEndpoint || '/Notifications/MarkAllAsRead'
    };

    const take = Math.max(1, Math.min(20, Number.parseInt(root.dataset.take || '7', 10) || 7));
    let loading = false;
    let loadedOnce = false;
    let activeController = null;
    let unreadCount = 0;
    let pollTimer = null;

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

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function positiveInt(value) {
        const n = Number.parseInt(String(value || ''), 10);
        return Number.isFinite(n) && n > 0 ? n : 0;
    }

    function nonNegativeInt(value) {
        const n = Number.parseInt(String(value ?? 0), 10);
        return Number.isFinite(n) && n >= 0 ? n : 0;
    }

    function valueOf(item, camel, pascal, fallback = '') {
        return item?.[camel] ?? item?.[pascal] ?? fallback;
    }

    function presentationFor(type) {
        return presentation[String(type || 'general').trim().toLowerCase()] || presentation.general;
    }

    function safeLocalUrl(value, id) {
        const url = String(value || '').trim();
        if (url.startsWith('/') && !url.startsWith('//')) return url;
        return `/Notifications/Open/${positiveInt(id)}`;
    }

    function formatDate(value) {
        if (!value) return 'الآن';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return 'الآن';
        const diff = Date.now() - date.getTime();
        const minute = 60 * 1000;
        const hour = 60 * minute;
        const day = 24 * hour;
        if (diff >= 0 && diff < minute) return 'الآن';
        if (diff >= minute && diff < hour) return `منذ ${Math.max(1, Math.floor(diff / minute))} د`;
        if (diff >= hour && diff < day) return `منذ ${Math.max(1, Math.floor(diff / hour))} س`;
        try {
            return new Intl.DateTimeFormat('ar-YE-u-nu-latn', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date);
        } catch {
            return date.toLocaleString();
        }
    }

    function setUnreadCount(count) {
        unreadCount = nonNegativeInt(count);
        if (badge) {
            badge.textContent = unreadCount > 99 ? '99+' : String(unreadCount);
            badge.hidden = unreadCount <= 0;
        }
        if (liveDot) liveDot.hidden = unreadCount <= 0;
        if (unreadPill) unreadPill.textContent = `${unreadCount} غير مقروء`;
        if (markAllButton) markAllButton.disabled = unreadCount <= 0;
        if (toggle) {
            toggle.setAttribute('aria-label', unreadCount > 0 ? `الإشعارات، ${unreadCount} غير مقروء` : 'الإشعارات، لا توجد إشعارات غير مقروءة');
        }
    }

    function setLoading(state) {
        loading = state;
        if (progress) progress.hidden = !state;
        root.classList.toggle('is-loading', state);
    }

    function renderLoading() {
        if (!body) return;
        body.innerHTML = `
            <div class="owner-notification-loading">
                <span class="owner-notification-loading__spinner"></span>
                <strong>جاري تحميل الإشعارات...</strong>
            </div>`;
    }

    function renderEmpty() {
        if (!body) return;
        body.innerHTML = `
            <div class="owner-notification-empty">
                <span><i class="bi bi-bell-slash"></i></span>
                <strong>لا توجد إشعارات جديدة</strong>
                <small>أنت متابع لكل جديد حتى الآن.</small>
            </div>`;
    }

    function renderError() {
        if (!body) return;
        body.innerHTML = `
            <div class="owner-notification-empty is-error">
                <span><i class="bi bi-wifi-off"></i></span>
                <strong>تعذر تحميل الإشعارات</strong>
                <button type="button" data-owner-notification-retry>إعادة المحاولة</button>
            </div>`;
    }

    function renderItems(items) {
        if (!body) return;
        if (!Array.isArray(items) || items.length === 0) {
            renderEmpty();
            return;
        }

        body.innerHTML = items.map(item => {
            const id = positiveInt(valueOf(item, 'notificationId', 'NotificationId'));
            const title = valueOf(item, 'notificationTitle', 'NotificationTitle', 'إشعار');
            const text = valueOf(item, 'notificationText', 'NotificationText', '');
            const type = valueOf(item, 'notificationType', 'NotificationType', 'General');
            const createdAt = valueOf(item, 'createdAt', 'CreatedAt', null);
            const openUrl = safeLocalUrl(valueOf(item, 'openUrl', 'OpenUrl', ''), id);
            const [icon, tone, label] = presentationFor(type);
            return `
                <a class="owner-notification-item"
                   href="${escapeHtml(openUrl)}"
                   data-owner-notification-item
                   data-notification-id="${id}"
                   data-tone="${tone}">
                    <span class="owner-notification-item__icon"><i class="bi ${icon}"></i></span>
                    <span class="owner-notification-item__copy">
                        <span class="owner-notification-item__top">
                            <strong>${escapeHtml(title)}</strong>
                            <em>${escapeHtml(label)}</em>
                        </span>
                        <span class="owner-notification-item__text">${escapeHtml(text)}</span>
                        <span class="owner-notification-item__time"><i class="bi bi-clock"></i>${escapeHtml(formatDate(createdAt))}</span>
                    </span>
                    <span class="owner-notification-item__arrow"><i class="bi bi-chevron-left"></i></span>
                </a>`;
        }).join('');
    }

    async function loadHeader(options = {}) {
        if (loading) return;
        activeController?.abort();
        activeController = new AbortController();
        setLoading(true);
        if (!loadedOnce && options.keepContent !== true) renderLoading();

        try {
            const url = new URL(endpoints.header, window.location.origin);
            url.searchParams.set('take', String(take));
            const response = await fetch(url, {
                method: 'GET', credentials: 'same-origin', cache: 'no-store',
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                signal: activeController.signal
            });
            if (!response.ok) throw new Error('header-load-failed');
            const data = await response.json();
            const count = nonNegativeInt(data?.unreadCount ?? data?.UnreadCount ?? 0);
            const items = data?.items ?? data?.Items ?? [];
            setUnreadCount(count);
            renderItems(Array.isArray(items) ? items : []);
            loadedOnce = true;
        } catch (error) {
            if (error?.name === 'AbortError') return;
            renderError();
        } finally {
            setLoading(false);
        }
    }

    async function refreshUnreadCount() {
        if (document.hidden) return;
        try {
            const response = await fetch(endpoints.unread, {
                method: 'GET', credentials: 'same-origin', cache: 'no-store',
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) return;
            const data = await response.json();
            const next = nonNegativeInt(data?.unreadCount ?? data?.UnreadCount ?? 0);
            const changed = next !== unreadCount;
            setUnreadCount(next);
            if (changed && menu?.classList.contains('show')) loadHeader({ keepContent: true });
        } catch { }
    }

    async function markAllRead() {
        if (!markAllButton || markAllButton.disabled || loading) return;
        markAllButton.disabled = true;
        markAllButton.classList.add('is-busy');
        try {
            const formData = new FormData();
            if (token) formData.append('__RequestVerificationToken', token);
            const response = await fetch(endpoints.markAll, {
                method: 'POST', credentials: 'same-origin', cache: 'no-store',
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: formData
            });
            if (!response.ok) throw new Error('mark-all-failed');
            setUnreadCount(0);
            renderEmpty();
            loadedOnce = true;
        } catch {
            markAllButton.disabled = unreadCount <= 0;
            toast('تعذر تعليم الإشعارات كمقروءة.', 'error');
        } finally {
            markAllButton.classList.remove('is-busy');
        }
    }

    function toast(message, type = 'info') {
        let host = document.getElementById('ownerNotificationToastHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'ownerNotificationToastHost';
            host.className = 'owner-notification-toast-host';
            document.body.appendChild(host);
        }
        const item = document.createElement('div');
        item.className = `owner-notification-toast is-${type}`;
        item.textContent = message;
        host.appendChild(item);
        window.setTimeout(() => item.remove(), 3200);
    }

    root.addEventListener('shown.bs.dropdown', () => loadHeader({ keepContent: loadedOnce }));
    menu?.addEventListener('click', event => event.stopPropagation());
    body?.addEventListener('click', event => {
        if (event.target.closest('[data-owner-notification-retry]')) {
            event.preventDefault();
            loadHeader();
        }
    });
    markAllButton?.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();
        markAllRead();
    });

    function stopOwnerNotificationPolling() {
        if (pollTimer) {
            window.clearInterval(pollTimer);
            pollTimer = null;
        }
    }

    function startOwnerNotificationPolling() {
        stopOwnerNotificationPolling();
        if (document.hidden) return;
        pollTimer = window.setInterval(refreshUnreadCount, 60000);
    }

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            stopOwnerNotificationPolling();
        } else {
            refreshUnreadCount();
            startOwnerNotificationPolling();
        }
    });

    setUnreadCount(0);
    refreshUnreadCount();
    startOwnerNotificationPolling();

    window.addEventListener('pagehide', () => {
        stopOwnerNotificationPolling();
        activeController?.abort();
    }, { once: true });

    window.OwnerNotifications = {
        refresh: () => loadHeader({ keepContent: true }),
        refreshUnreadCount,
        setUnreadCount,
        toast
    };
}());
