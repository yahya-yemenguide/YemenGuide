/**
 * =========================================================================
 * YemenGuide - إدارة المنشآت: الملف المركزي والتبويبات
 * يحتوي الأدوات المشتركة، التبويبات، التحميل الديناميكي، والحالة النشطة.
 * تم فصله من manage-places.js دون تغيير الوظائف أو المسارات أو البيانات.
 * =========================================================================
 */

document.addEventListener("DOMContentLoaded", function () {

    // نطاق خاص بصفحة إدارة المنشآت فقط؛ يسمح بتوحيد مودالات التبويبات الأربعة
    // دون التأثير على الأدمن أو أي صفحة أخرى تستخدم نفس القوالب المشتركة.
    document.body.classList.add("owner-manage-places-screen");


    // بيانات الخريطة وفحص الصور أصبحت داخل add-place.js،
    // وهو المصدر المشترك الوحيد لفورم المالك والأدمن.

    // 🌟 دالة تأكيد أنيقة ومخصصة
    window.elegantConfirm = function (message, onConfirm) {
        if (document.getElementById('elegant-confirm-overlay')) return;

        const overlay = document.createElement('div');
        overlay.id = 'elegant-confirm-overlay';
        overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.5);z-index:999999;display:flex;align-items:center;justify-content:center;backdrop-filter:blur(4px);transition:all 0.3s ease; opacity:0;';

        const box = document.createElement('div');
        box.className = 'owner-manage-confirm-sheet';
        box.style.cssText = 'background:#fff;padding:25px;border-radius:20px;box-shadow:0 15px 30px rgba(0,0,0,0.2);text-align:center;max-width:320px;width:90%;transform:translateY(30px);transition:all 0.3s ease; direction:rtl;';

        box.innerHTML = `
            <div style="font-size:45px;color:#dc3545;margin-bottom:5px;"><i class="bi bi-trash3-fill"></i></div>
            <h5 style="font-weight:900;color:#2c3e50;margin-bottom:10px;">تأكيد الحذف</h5>
            <p style="font-size:14px;color:#6c757d;margin-bottom:20px;font-weight:bold;">${message}</p>
            <div style="display:flex;gap:10px;justify-content:center;">
                <button type="button" id="btnYesDelete" style="background:#dc3545;color:#fff;border:none;padding:10px 20px;border-radius:25px;font-weight:bold;cursor:pointer;flex:1;box-shadow:0 4px 10px rgba(220,53,69,0.3); transition:0.2s;">نعم، احذف</button>
                <button type="button" id="btnNoCancel" style="background:#f8f9fa;color:#495057;border:1px solid #dee2e6;padding:10px 20px;border-radius:25px;font-weight:bold;cursor:pointer;flex:1; transition:0.2s;">إلغاء</button>
            </div>
        `;

        overlay.appendChild(box);
        document.body.appendChild(overlay);

        const btnYes = document.getElementById('btnYesDelete');
        const btnNo = document.getElementById('btnNoCancel');

        setTimeout(() => {
            overlay.style.opacity = '1';
            box.style.transform = 'translateY(0)';
            // 🌟 التعديل هنا: التركيز أصبح على زر الحذف ليعمل مع الانتر فوراً
            btnYes.focus();
        }, 10);

        const trapKeyboard = function (e) {
            e.stopImmediatePropagation();
            if (e.key === 'Escape') closeDialog();
            if (e.key === 'Enter') {
                e.preventDefault();
                // 🌟 التعديل هنا: إذا ضغطت إنتر، سيقوم بالحذف، إلا إذا قمتِ أنتِ بالانتقال بالأسهم لزر الإلغاء
                if (document.activeElement === btnNo) btnNo.click();
                else btnYes.click();
            }
        };

        // التقاط الحدث لمنع تسربه إلى الفورم الأساسي
        document.addEventListener('keydown', trapKeyboard, true);

        const closeDialog = () => {
            overlay.style.opacity = '0';
            box.style.transform = 'translateY(30px)';
            document.removeEventListener('keydown', trapKeyboard, true);
            setTimeout(() => overlay.remove(), 300);
        };

        btnYes.onclick = function (e) {
            e.preventDefault();
            closeDialog();
            onConfirm();
        };

        btnNo.onclick = function (e) {
            e.preventDefault();
            closeDialog();
        };
    };

    window.normalizeArabic = function (text) {
        if (!text) return '';
        return text.replace(/[أإآ]/g, 'ا').replace(/ة/g, 'ه').trim();
    };

    // =========================================================================
    // 📁 القسم الثاني: نظام التبويبات الذكي السريع جداً (Smart Cache) 🚀
    // =========================================================================
    const tabButtons = document.querySelectorAll('.custom-tabs-container .tab-btn');
    const tabContents = document.querySelectorAll('.tab-pane-content');
    const pageActionHost = document.getElementById('managePlacesPageActions');
    const headerAddPlaceButton = document.getElementById('managePlacesHeaderAddPlace');
    const headerAddPromotionButton = document.getElementById('managePlacesHeaderAddPromotion');
    const currentUrl = new URL(window.location.href);
    const requestedTab = currentUrl.searchParams.get('placeTab');
    const knownTabs = new Set(Array.from(tabButtons).map(button => button.getAttribute('data-target')).filter(Boolean));
    const savedTab = knownTabs.has(requestedTab)
        ? requestedTab
        : (localStorage.getItem('activePlaceTab') || 'place-list-tab');

    /**
     * يحذف الرؤوس الكبيرة داخل التبويبات فعليًا من DOM، وليس مجرد إخفائها بالـCSS.
     * هذا هو المطلوب في قائمة المنشآت، إضافة منشأة، العروض، والخدمات والباقات.
     */
    function removeRedundantTabHeader(targetClass, targetContent) {
        if (!targetContent) return;

        if (targetClass === 'place-list-tab') {
            targetContent.querySelector('.owner-places-hero')?.remove();
            return;
        }

        if (targetClass === 'add-place-tab') {
            targetContent.querySelector('.add-place-entry__header')?.remove();
            return;
        }

        if (targetClass === 'promotions-manager-tab') {
            const promoHeader = targetContent.querySelector('.yg-promo-page-head');

            if (promoHeader) {
                // زر الإضافة الأصلي مرتبط بالـEvents داخل promotions-manager.js.
                // ننقله مخفيًا خارج الديف المحذوف ونبقيه كـhook فقط للزر الموجود أعلى الصفحة.
                const originalPromotionButton = promoHeader.querySelector('#openPromotionModal');
                if (originalPromotionButton) {
                    originalPromotionButton.hidden = true;
                    originalPromotionButton.style.display = 'none';
                    originalPromotionButton.setAttribute('aria-hidden', 'true');
                    targetContent.appendChild(originalPromotionButton);
                }

                promoHeader.remove();
            }

            // لا نحتاج بطاقات الإحصائيات الكبيرة هنا؛ تبويبات الحالات بالأسفل تعرض العدادات نفسها.
            targetContent.querySelector('.yg-promo-stats')?.remove();
            return;
        }

        if (targetClass === 'details-manager-tab') {
            targetContent.querySelector('.details-hero')?.remove();

            // إزالة شريط «جهّز تجربة الحجز من مكان واحد» بالكامل لتقليل الزحمة.
            targetContent.querySelector('.details-content-guide')?.remove();
        }
    }

    /**
     * يظهر فقط زر الإجراء المناسب للتبويب الحالي داخل الهيدر الرئيسي.
     */
    function syncManagePlacesHeaderActions(targetClass, targetContent) {
        if (headerAddPlaceButton) {
            headerAddPlaceButton.hidden = targetClass !== 'place-list-tab';
        }

        if (headerAddPromotionButton) {
            const promotionReady =
                targetClass === 'promotions-manager-tab' &&
                targetContent?.getAttribute('data-loaded') === 'true' &&
                !!targetContent.querySelector('#openPromotionModal');

            headerAddPromotionButton.hidden = !promotionReady;
        }

        if (pageActionHost) {
            const hasVisibleAction = Array.from(pageActionHost.children)
                .some(element => !element.hidden);
            pageActionHost.hidden = !hasVisibleAction;
        }
    }

    headerAddPlaceButton?.addEventListener('click', function () {
        const addPlaceTab = document.querySelector('.tab-btn[data-target="add-place-tab"]');
        addPlaceTab?.click();
    });

    headerAddPromotionButton?.addEventListener('click', function () {
        const promotionsPanel = document.querySelector('.promotions-manager-tab');
        const originalPromotionButton = promotionsPanel?.querySelector('#openPromotionModal');
        originalPromotionButton?.click();
    });

    // تحديث هادئ: نبقي المحتوى الحالي ظاهرًا أثناء جلب النسخة الجديدة من السيرفر،
    // بدل مسح التبويب وإظهار شاشة تحميل كاملة كل مرة.
    function beginSmoothRefresh(targetContent) {
        if (!targetContent || targetContent.querySelector(':scope > .manage-places-soft-refresh')) return;

        if (!document.getElementById('managePlacesSmoothRefreshStyles')) {
            const style = document.createElement('style');
            style.id = 'managePlacesSmoothRefreshStyles';
            style.textContent = `
                .tab-pane-content { position: relative; }
                .tab-pane-content.is-soft-refreshing { pointer-events: none; }
                .tab-pane-content.is-soft-refreshing > :not(.manage-places-soft-refresh) {
                    opacity: .72;
                    transition: opacity .18s ease;
                }
                .manage-places-soft-refresh {
                    position: absolute;
                    top: 10px;
                    inset-inline-start: 10px;
                    z-index: 50;
                    display: inline-flex;
                    align-items: center;
                    gap: 7px;
                    min-height: 34px;
                    padding: 7px 11px;
                    color: #187b9c;
                    background: rgba(255,255,255,.96);
                    border: 1px solid rgba(0,166,218,.18);
                    border-radius: 999px;
                    box-shadow: 0 8px 24px rgba(5,32,60,.10);
                    font-size: .72rem;
                    font-weight: 800;
                    backdrop-filter: blur(10px);
                }
                .manage-places-soft-refresh__spinner {
                    width: 14px;
                    height: 14px;
                    border: 2px solid rgba(0,166,218,.20);
                    border-top-color: #00A6DA;
                    border-radius: 50%;
                    animation: managePlacesSoftSpin .65s linear infinite;
                }
                @keyframes managePlacesSoftSpin { to { transform: rotate(360deg); } }
                body.dark-mode .manage-places-soft-refresh {
                    color: #d8f4ff;
                    background: rgba(16,42,64,.96);
                    border-color: rgba(0,166,218,.25);
                }
            `;
            document.head.appendChild(style);
        }

        targetContent.classList.add('is-soft-refreshing');

        const badge = document.createElement('div');
        badge.className = 'manage-places-soft-refresh';
        badge.setAttribute('role', 'status');
        badge.innerHTML = `
            <span class="manage-places-soft-refresh__spinner" aria-hidden="true"></span>
            <span>جاري التحديث...</span>
        `;
        targetContent.appendChild(badge);
    }

    function endSmoothRefresh(targetContent) {
        if (!targetContent) return;
        targetContent.classList.remove('is-soft-refreshing');
        targetContent.querySelector(':scope > .manage-places-soft-refresh')?.remove();
    }

    function animateFreshContent(targetContent) {
        if (!targetContent || typeof targetContent.animate !== 'function') return;
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        targetContent.animate(
            [
                { opacity: 0.82, transform: 'translateY(3px)' },
                { opacity: 1, transform: 'translateY(0)' }
            ],
            { duration: 180, easing: 'ease-out' }
        );
    }

    function activatePlaceTab(targetClass, isTabClick = false) {
        const targetBtn = document.querySelector(`.custom-tabs-container .tab-btn[data-target="${targetClass}"]`);
        let endpointUrl = targetBtn?.getAttribute('data-url') || '';

        // مصدر حقيقة واحد للحالة النشطة: active + aria-selected + tabindex.
        // هذا يمنع بقاء التبويب السابق ملونًا بعد اختيار تبويب جديد.
        tabButtons.forEach(btn => {
            const isActive = btn === targetBtn;
            btn.classList.toggle('active', isActive);
            btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
            btn.setAttribute('tabindex', isActive ? '0' : '-1');
        });

        tabContents.forEach(content => {
            content.style.display = 'none';
            content.classList.remove('show-smooth');
        });

        syncManagePlacesHeaderActions(targetClass, null);

        const targetContent = document.querySelector('.' + targetClass);
        if (!targetContent) return;

        targetContent.style.display = 'block';

        const alreadyLoaded = targetContent.getAttribute('data-loaded') === 'true';

        if (isTabClick && alreadyLoaded) {
            targetContent.classList.add('show-smooth');
            removeRedundantTabHeader(targetClass, targetContent);
            syncManagePlacesHeaderActions(targetClass, targetContent);
            return;
        }

        if (!endpointUrl) return;

        // عند التحديث بعد حفظ/حذف/تغيير حالة، لا نمسح المحتوى القديم.
        // نبقيه ظاهرًا مع مؤشر صغير حتى تصل البيانات الجديدة ثم نستبدلها دفعة واحدة.
        const isSoftRefresh = alreadyLoaded && !isTabClick;

        if (isSoftRefresh) {
            beginSmoothRefresh(targetContent);
        } else {
            targetContent.innerHTML = `
                <div class="text-center py-5">
                    <div class="spinner-border text-primary opacity-50 mb-3" role="status" style="width: 2.35rem; height: 2.35rem;"></div>
                    <h6 class="fw-bold text-muted">جاري تحميل البيانات...</h6>
                </div>
            `;
        }

        fetch(endpointUrl, { cache: 'no-store' })
            .then(response => {
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return response.text();
            })
            .then(htmlMarkup => {
                endSmoothRefresh(targetContent);
                targetContent.innerHTML = htmlMarkup;
                targetContent.classList.add('show-smooth');
                targetContent.setAttribute('data-loaded', 'true');

                const tabInitializers = {
                    'place-list-tab': window.initPlaceList,
                    'add-place-tab': window.initAddPlace,
                    'calendar-manager-tab': window.initPremiumCalendar,
                    'promotions-manager-tab': window.initPromotionsManager,
                    'details-manager-tab': window.initDetailsManager
                };

                const initializeTab = tabInitializers[targetClass];
                if (typeof initializeTab !== 'function') {
                    throw new Error(`دالة تهيئة التبويب غير محمّلة: ${targetClass}`);
                }

                initializeTab();
                removeRedundantTabHeader(targetClass, targetContent);
                syncManagePlacesHeaderActions(targetClass, targetContent);
                focusManagePlacesDeepLink(targetClass, targetContent);
                animateFreshContent(targetContent);
            })
            .catch(error => {
                endSmoothRefresh(targetContent);

                if (isSoftRefresh) {
                    // إذا فشل التحديث الهادئ نُبقي البيانات القديمة بدل تحويل الصفحة إلى شاشة خطأ.
                    syncManagePlacesHeaderActions(targetClass, targetContent);
                    if (typeof window.showSafeToast === 'function') {
                        window.showSafeToast('تعذر تحديث البيانات الآن. حاول مرة أخرى.', 'error');
                    } else {
                        console.error('فشل تحديث تبويب إدارة المنشآت:', error);
                    }
                    return;
                }

                syncManagePlacesHeaderActions('', null);
                targetContent.innerHTML = `<div class="text-center py-5 text-danger"><h6 class="fw-bold">عذراً، فشل تحديث البيانات.</h6><p class="small text-muted">${error.message}</p></div>`;
            });
    }

    function focusManagePlacesDeepLink(targetClass, targetContent) {
        const url = new URL(window.location.href);

        if (targetClass === 'place-list-tab') {
            const placeId = Number.parseInt(url.searchParams.get('focusPlace') || '0', 10);
            if (placeId > 0) {
                const card = targetContent.querySelector(`[data-place-card="${placeId}"], #owner-place-card-${placeId}`);
                if (card) {
                    card.classList.add('is-notification-focus');
                    card.scrollIntoView({
                        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                        block: 'center'
                    });
                    window.setTimeout(() => card.classList.remove('is-notification-focus'), 4200);
                    url.searchParams.delete('focusPlace');
                    url.searchParams.delete('placeTab');
                    window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}${url.hash}`);
                }
            }
        }
    }

    window.activatePlaceTab = activatePlaceTab;
    activatePlaceTab(savedTab, false);

    tabButtons.forEach(button => {
        button.addEventListener('click', function () {
            const targetClass = this.getAttribute('data-target');
            localStorage.setItem('activePlaceTab', targetClass);
            activatePlaceTab(targetClass, true);
        });
    });

});
