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
    const currentUrl = new URL(window.location.href);
    const requestedTab = currentUrl.searchParams.get('placeTab');
    const knownTabs = new Set(Array.from(tabButtons).map(button => button.getAttribute('data-target')).filter(Boolean));
    const savedTab = knownTabs.has(requestedTab)
        ? requestedTab
        : (localStorage.getItem('activePlaceTab') || 'place-list-tab');

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

        const targetContent = document.querySelector('.' + targetClass);
        if (!targetContent) return;

        targetContent.style.display = 'block';

        if (isTabClick && targetContent.getAttribute('data-loaded') === 'true') {
            targetContent.classList.add('show-smooth');
            return;
        }

        if (!endpointUrl) return;

        targetContent.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-primary opacity-50 mb-3" role="status" style="width: 3rem; height: 3rem;"></div>
                <h6 class="fw-bold text-muted">جاري بناء الواجهة...</h6>
            </div>
        `;

        fetch(endpointUrl)
            .then(response => {
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return response.text();
            })
            .then(htmlMarkup => {
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
                focusManagePlacesDeepLink(targetClass, targetContent);
            })
            .catch(error => {
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
