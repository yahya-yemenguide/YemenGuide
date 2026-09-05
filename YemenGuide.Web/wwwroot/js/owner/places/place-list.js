/**
 * =========================================================================
 * YemenGuide - إدارة المنشآت: قائمة منشآتي
 * يحتوي الموافقة والرفض والتعديل والحذف والحالة وإدارة صور المنشأة ووضع العرض فقط.
 * تم فصله من manage-places.js دون تغيير الوظائف أو المسارات أو البيانات.
 * =========================================================================
 */

document.addEventListener("DOMContentLoaded", function () {


    window.activatePlace = function (placeId) {
        Swal.fire({
            title: 'تأكيد الموافقة',
            text: 'بموافقتك، ستقر بأن هذه المنشأة تابعة لك لتتمكن من إدارتها. هل أنت متأكد؟',
            icon: 'question',
            showCancelButton: true,
            confirmButtonColor: '#198754',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'نعم، أوافق!',
            cancelButtonText: 'تأجيل'
        }).then((result) => {
            if (result.isConfirmed) {

                // إظهار شاشة تحميل صغيرة أثناء الإرسال
                Swal.fire({ title: 'جاري الموافقة...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

                // إرسال طلب الموافقة للسيرفر
                fetch('/Owner/Place/ApprovePlaceByOwner?placeId=' + placeId, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' }
                })
                    .then(response => response.json())
                    .then(data => {
                        if (data.success) {

                            // 🌟 السر هنا: إذا وافق السيرفر وأخبرنا أنه لا توجد سياسات 🌟
                            if (data.hasPolicies === false) {
                                Swal.close(); // إغلاق نافذة التحميل
                                window.showPolicyOptions(placeId); // ⬅️ فتح نافذة إعداد السياسات فوراً!
                            }
                            else {
                                // إذا كانت السياسات موجودة مسبقاً أو لا يحتاج سياسات
                                Swal.fire('تم بنجاح!', data.message, 'success').then(() => location.reload());
                            }

                        } else {
                            Swal.fire('خطأ!', data.message, 'error');
                        }
                    })
                    .catch(error => {
                        Swal.fire('خطأ!', 'حدث خطأ في الاتصال بالخادم.', 'error');
                    });
            }
        });
    };


    window.approvePlaceByOwner = window.activatePlace;


    window.approvePlaceByOwner = window.activatePlace;

    window.rejectPlaceByOwner = function (id) {
        Swal.fire({
            title: 'رفض المنشأة',
            text: 'الرجاء كتابة سبب الرفض (مثال: البيانات خاطئة، المنشأة لا تتبعني):',
            input: 'text',
            inputPlaceholder: 'اكتب السبب هنا...',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#d33',
            cancelButtonColor: '#05203C',
            confirmButtonText: 'تأكيد الرفض',
            cancelButtonText: 'إلغاء',
            customClass: { popup: 'rounded-4 shadow-lg' },
            inputValidator: (value) => { if (!value) return 'يجب كتابة سبب الرفض لإرساله للإدارة!'; }
        }).then((result) => {
            if (result.isConfirmed) {
                const reason = result.value;
                Swal.fire({ title: 'جاري الإرسال...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });
                fetch('/Owner/Place/RejectPlaceByOwner', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: id, reason: reason })
                })
                    .then(res => res.json())
                    .then(data => {
                        if (data.success) {
                            Swal.fire({ icon: 'success', title: 'تم الرفض!', text: 'تمت إعادة المنشأة للإدارة مع توضيح السبب.', confirmButtonColor: '#00A6DA' }).then(() => {
                                window.activatePlaceTab(localStorage.getItem('activePlaceTab') || 'place-list-tab');
                            });
                        } else {
                            Swal.fire('خطأ', data.message, 'error');
                        }
                    });
            }
        });
    };

    function initPlaceList() { console.log("تم تهيئة تبويبة قائمة المنشآت."); }

    // تعديل بيانات المنشأة أصبح داخل المحرر الموحد place-editor.js.




    // 🌟 دالة حذف المنشأة (النسخة المضادة للرصاص)
    window.deletePlace = function (placeId) {
        window.elegantConfirm('سيتم حذف المنشأة وجميع بياناتها نهائياً! هل أنت متأكد؟', function () {

            Swal.fire({ title: 'جاري الحذف...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

            fetch(`/Owner/Place/DeletePlaceConfirmed?placeId=${placeId}`, { method: 'POST' })
                .then(async res => {
                    // السحر هنا: نقرأ الرد كنص أولاً لمنع انهيار الجافاسكريبت
                    const text = await res.text();
                    let isSuccess = res.ok;
                    let errorMsg = 'فشل الحذف من الخادم';

                    try {
                        const data = JSON.parse(text); // نحاول قراءته كـ JSON
                        if (data.success !== undefined) isSuccess = data.success;
                        if (data.message) errorMsg = data.message;
                    } catch (e) {
                        // إذا لم يكن JSON (يعني السيرفر عمل Redirect) نعتبره ناجحاً
                    }

                    if (isSuccess) {
                        Swal.close();
                        window.showSafeToast('تم حذف المنشأة بنجاح.', 'success');
                        window.activatePlaceTab(localStorage.getItem('activePlaceTab') || 'place-list-tab');
                    } else {
                        Swal.fire('خطأ', errorMsg, 'error');
                    }
                }).catch(err => {
                    Swal.fire('خطأ', 'حدث خطأ في الاتصال بالخادم', 'error');
                });
        });
    };

    // 🌟 دالة حذف الوحدات والخدمات (النسخة المضادة للرصاص)
    window.deleteItem = function (type, id, placeId) {
        window.elegantConfirm('هل أنت متأكد من الحذف؟ لن يمكن التراجع.', function () {

            Swal.fire({ title: 'جاري الحذف...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

            let endpoint = type === 'package' ? `/Owner/Place/DeletePlacePackage?id=${id}` : type === 'service' ? `/Owner/Place/DeleteAdditionalService?id=${id}` : type === 'room' ? `/Owner/Place/DeleteRoom?id=${id}` : `/Owner/Place/DeletePlaceAmenity?id=${id}`;

            fetch(endpoint, { method: 'POST' })
                .then(async res => {
                    const text = await res.text();
                    let isSuccess = res.ok;
                    let errorMsg = 'فشل الحذف من الخادم';

                    try {
                        const data = JSON.parse(text);
                        if (data.success !== undefined) isSuccess = data.success;
                        if (data.message) errorMsg = data.message;
                    } catch (e) { }

                    if (isSuccess) {
                        Swal.close();
                        window.showSafeToast('تم الحذف بنجاح.', 'success');
                        if (typeof window.refreshDetailsView === 'function') {
                            window.refreshDetailsView(placeId);
                        }
                    } else {
                        Swal.fire('خطأ', errorMsg, 'error');
                    }
                }).catch(err => {
                    Swal.fire('خطأ', 'حدث خطأ في الاتصال بالخادم', 'error');
                });
        });
    };


    window.togglePlaceStatus = function (placeId, isCurrentlyAvailable) {
        const newStatus = !isCurrentlyAvailable;
        fetch(`/Owner/Place/TogglePlaceStatus?placeId=${placeId}&isAvailable=${newStatus}`, { method: 'POST' }).then(res => res.json()).then(res => {
            if (res.success) {
                window.activatePlaceTab(localStorage.getItem('activePlaceTab') || 'place-list-tab');
            } else { Swal.fire('خطأ', res.message, 'error'); }
        });
    };

    window.resubmitPlaceForReview = function (placeId, hasPolicies = 'true') {
        if (hasPolicies === 'false' || hasPolicies === false) {
            Swal.fire({
                icon: 'error',
                title: 'البيانات غير مكتملة!',
                text: 'عفواً، لا يمكنك إرسال المنشأة للإدارة قبل الدخول لتبويبة (الخدمات والباقات) بالأسفل، وتعبئة السياسات المالية وإلغاء الحجز.',
                confirmButtonColor: '#d33',
                confirmButtonText: 'حسناً، سأقوم بتعبئتها'
            });
            return;
        }

        Swal.fire({
            title: 'إعادة إرسال للإدارة؟',
            text: 'هل قمت بإصلاح الأسباب المطلوبة ومتأكد من رغبتك في إعادة إرسال المنشأة للمراجعة؟',
            icon: 'question',
            showCancelButton: true,
            confirmButtonColor: '#00A6DA',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'نعم، أرسلها الآن',
            cancelButtonText: 'تراجع',
            customClass: { popup: 'rounded-4 shadow-lg' }
        }).then((result) => {
            if (result.isConfirmed) {
                Swal.fire({ title: 'جاري الإرسال...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

                fetch(`/Owner/Place/ResubmitPlace?placeId=${placeId}`, { method: 'POST' })
                    .then(res => res.json())
                    .then(data => {
                        if (data.success) {
                            Swal.fire({
                                icon: 'success',
                                title: 'تم بنجاح!',
                                text: data.message,
                                confirmButtonColor: '#00A6DA'
                            }).then(() => {
                                window.activatePlaceTab(localStorage.getItem('activePlaceTab') || 'place-list-tab');
                            });
                        } else {
                            Swal.fire('خطأ!', data.message, 'error');
                        }
                    })
                    .catch(err => Swal.fire('خطأ!', 'فشل الاتصال بالسيرفر.', 'error'));
            }
        });
    };


    // إدارة صور المنشأة أصبحت داخل المحرر الموحد place-editor.js.

    // =========================================================
    // الإصدار الفولاذي المخصص لـ (SweetAlert2) 🚀
    // =========================================================

    window.viewOnlyPlace = function (placeId) {
        document.body.classList.add('view-only-mode');
        injectViewOnlyStyles();

        if (typeof openEditPlaceModal === 'function') {
            openEditPlaceModal(placeId);
        }

        freezeModalElements();
    };

    window.viewOnlyImagesModal = function (placeId) {
        document.body.classList.add('view-only-mode');
        injectViewOnlyStyles();

        if (typeof openManagePlaceImagesModal === 'function') {
            openManagePlaceImagesModal(placeId);
        }

        freezeModalElements();
    };

    function freezeModalElements() {
        let attempts = 0;
        let freezeInterval = setInterval(() => {
            attempts++;
            let activeModal = document.querySelector('.swal2-popup');

            if (activeModal) {
                let inputs = activeModal.querySelectorAll('input, select, textarea');
                inputs.forEach(el => {
                    el.setAttribute('disabled', 'true');
                    el.style.backgroundColor = '#f8f9fa';
                    el.style.cursor = 'not-allowed';
                });

                let actionButtons = activeModal.querySelectorAll('.swal2-confirm, input[type="file"], button.btn-danger, .text-danger, .bg-danger, [onclick*="delete"]');
                actionButtons.forEach(btn => {
                    btn.style.setProperty('display', 'none', 'important');
                });
            }

            if (attempts > 6) clearInterval(freezeInterval);
        }, 500);
    }

    function injectViewOnlyStyles() {
        if (!document.getElementById('viewOnlyNuclearStyles')) {
            const style = document.createElement('style');
            style.id = 'viewOnlyNuclearStyles';
            style.innerHTML = `
/* إظهار علامة المنع على الحقول فقط */
body.view-only-mode .swal2-popup input,
body.view-only-mode .swal2-popup select,
body.view-only-mode .swal2-popup textarea { 
    cursor: not-allowed !important; 
}
/* تجميد الخريطة لمنع تغيير الموقع بالخطأ */
body.view-only-mode #googleMapPicker {
    pointer-events: none !important;
    opacity: 0.8;
}
`;
            document.head.appendChild(style);
        }
    }

    setInterval(() => {
        if (document.body.classList.contains('view-only-mode') && !document.body.classList.contains('swal2-shown')) {
            document.body.classList.remove('view-only-mode');

            let inputs = document.querySelectorAll('.swal2-popup input, .swal2-popup select, .swal2-popup textarea');
            inputs.forEach(el => el.removeAttribute('disabled'));
        }
    }, 800);

    // تصدير دوال التهيئة للملف المركزي بعد الفصل
    window.initPlaceList = initPlaceList;

});

/* ==========================================================================
 * طبقة التصميم الفخم لقائمة منشآتي
 * لا تغيّر أي Endpoint أو FormData أو منطق حفظ/تعديل/حذف.
 * ========================================================================== */
(function () {
    "use strict";

    function isElementVisible(element) {
        if (!element) return false;
        const rect = element.getBoundingClientRect();
        return rect.top >= 0 && rect.bottom <= window.innerHeight;
    }

    function initializeLuxuryPlaceList() {
        const activeCard = document.querySelector('[data-owner-active-place="true"]');

        if (activeCard && activeCard.dataset.activePlaceFocused !== "true") {
            activeCard.dataset.activePlaceFocused = "true";

            window.setTimeout(function () {
                if (!isElementVisible(activeCard)) {
                    activeCard.scrollIntoView({ behavior: "smooth", block: "center" });
                }
            }, 180);
        }

        document.querySelectorAll('.owner-place-more[open]').forEach(function (openedDetails) {
            openedDetails.removeAttribute('open');
        });
    }

    function bytesToMegabytes(bytes) {
        return (bytes / (1024 * 1024)).toFixed(2);
    }

    function enhanceGalleryFileInput() {
        const input = document.getElementById('new-place-images');

        if (!input || input.dataset.ownerLuxuryUpload === "true") return;

        input.dataset.ownerLuxuryUpload = "true";
        input.classList.add('owner-place-native-file');

        const shell = document.createElement('div');
        shell.className = 'owner-place-upload-shell';
        shell.innerHTML = `
            <span class="owner-place-upload-shell__icon"><i class="bi bi-cloud-arrow-up"></i></span>
            <strong>إضافة صور جديدة للمنشأة</strong>
            <p>اختر عدة صور واضحة، والحد الإجمالي المسموح 10 ميجابايت.</p>
            <button type="button"><i class="bi bi-images me-1"></i> اختيار صور من الجهاز</button>
            <div class="owner-place-upload-summary">لم يتم اختيار صور جديدة بعد.</div>
            <div class="owner-place-upload-preview" aria-live="polite"></div>`;

        input.parentNode.insertBefore(shell, input);

        const chooseButton = shell.querySelector('button');
        const summary = shell.querySelector('.owner-place-upload-summary');
        const preview = shell.querySelector('.owner-place-upload-preview');

        chooseButton.addEventListener('click', function () {
            input.click();
        });

        input.addEventListener('change', function () {
            const files = Array.from(input.files || []);
            const totalBytes = files.reduce(function (sum, file) { return sum + file.size; }, 0);

            preview.innerHTML = '';

            if (!files.length) {
                summary.textContent = 'لم يتم اختيار صور جديدة بعد.';
                return;
            }

            summary.textContent = `تم اختيار ${files.length} صورة — الحجم الإجمالي ${bytesToMegabytes(totalBytes)} ميجابايت.`;

            files.slice(0, 8).forEach(function (file) {
                if (!file.type.startsWith('image/')) return;

                const image = document.createElement('img');
                image.alt = file.name;
                image.src = URL.createObjectURL(file);
                image.addEventListener('load', function () {
                    URL.revokeObjectURL(image.src);
                }, { once: true });
                preview.appendChild(image);
            });
        });
    }

    function addModalClasses() {
        const popup = document.querySelector('.swal2-popup');
        if (!popup || !document.body.classList.contains('owner-place-modal-context')) return;

        popup.classList.add('owner-place-luxury-popup');
        enhanceGalleryFileInput();
    }

    function monitorModalLifecycle() {
        if (window.__ownerPlaceModalObserver) return;

        window.__ownerPlaceModalObserver = new MutationObserver(function () {
            addModalClasses();

            if (!document.querySelector('.swal2-container')) {
                document.body.classList.remove(
                    'owner-place-modal-context',
                    'owner-place-edit-modal',
                    'owner-place-images-modal',
                    'owner-place-confirm-modal'
                );
            }
        });

        window.__ownerPlaceModalObserver.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function wrapModalFunction(functionName, modalClass) {
        const originalFunction = window[functionName];

        if (typeof originalFunction !== 'function' || originalFunction.__ownerPlaceLuxuryWrapped) return;

        const wrappedFunction = function () {
            document.body.classList.add('owner-place-modal-context', modalClass);
            const result = originalFunction.apply(this, arguments);
            window.setTimeout(addModalClasses, 0);
            return result;
        };

        wrappedFunction.__ownerPlaceLuxuryWrapped = true;
        window[functionName] = wrappedFunction;
    }

    function initializeModalWrappers() {
        wrapModalFunction('openEditPlaceModal', 'owner-place-edit-modal');
        wrapModalFunction('viewOnlyPlace', 'owner-place-edit-modal');
        wrapModalFunction('openManagePlaceImagesModal', 'owner-place-images-modal');
        wrapModalFunction('viewOnlyImagesModal', 'owner-place-images-modal');
        wrapModalFunction('deletePlace', 'owner-place-confirm-modal');
        wrapModalFunction('activatePlace', 'owner-place-confirm-modal');
        wrapModalFunction('rejectPlaceByOwner', 'owner-place-confirm-modal');
        wrapModalFunction('resubmitPlaceForReview', 'owner-place-confirm-modal');
    }

    function closeOtherActionMenus(event) {
        document.querySelectorAll('.owner-place-more[open]').forEach(function (details) {
            if (!details.contains(event.target)) details.removeAttribute('open');
        });
    }

    document.addEventListener('click', closeOtherActionMenus);

    document.addEventListener('DOMContentLoaded', function () {
        monitorModalLifecycle();
        initializeModalWrappers();

        const previousInitPlaceList = window.initPlaceList;
        window.initPlaceList = function () {
            if (typeof previousInitPlaceList === 'function') previousInitPlaceList();
            initializeLuxuryPlaceList();
            initializeModalWrappers();
        };

        initializeLuxuryPlaceList();
    });
}());
