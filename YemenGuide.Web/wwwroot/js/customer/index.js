/**
 * ============================================================================
 * 👑 VIP INDEX.JS - قلب المنصة النابض والتفاعلات الديناميكية
 * ============================================================================
 */

let map;
let userMarker;
let nearbyMarkers = [];
let nearbyRoutes = [];
let nearbyMapType = localStorage.getItem('nearbyMapType') || 'satellite';
let nearbyLabelsVisible = localStorage.getItem('nearbyLabelsVisible') === 'true';
let nearbyPlacesVisible = localStorage.getItem('nearbyPlacesVisible') !== 'false';
let nearbyTrafficVisible = localStorage.getItem('nearbyTrafficVisible') === 'true';
let nearbyTrafficLayer;
let nearbyLastUserLocation;
let nearbyActiveMarker;
let nearbyActiveRoute;
let nearbyOpenInfoWindow;
window.isCompassMagic = false; // 👈 متغير لمعرفة من قام بطلب الخريطة (البوصلة أم الزر العادي)

// ============================================================================
// 🌟 0. تهيئة الخريطة المبدئية (Global Init)
// ============================================================================
window.initMap = function () {
    console.log("🗺️ تم تحميل مكتبة خرائط جوجل بنجاح.");
    window.googleMapsReady = true;
    window.dispatchEvent(new CustomEvent('googlemapsready'));
};

let googleMapsLoadPromise = null;

const loadGoogleMapsApi = () => {
    if (window.google?.maps) {
        return Promise.resolve(window.google.maps);
    }

    if (googleMapsLoadPromise) {
        return googleMapsLoadPromise;
    }

    googleMapsLoadPromise = new Promise((resolve, reject) => {
        const nearbySection = document.getElementById('nearby-section');
        const apiKey = nearbySection?.dataset.googleMapsKey || '';

        if (!apiKey) {
            googleMapsLoadPromise = null;
            reject(new Error('GoogleMapsMissingApiKey'));
            return;
        }

        const existing = document.querySelector('script[data-yg-google-maps]');
        if (existing) {
            existing.addEventListener('load', () => {
                window.googleMapsReady = Boolean(window.google?.maps);
                window.google?.maps ? resolve(window.google.maps) : reject(new Error('GoogleMapsLoadFailed'));
            }, { once: true });
            existing.addEventListener('error', () => reject(new Error('GoogleMapsLoadFailed')), { once: true });
            return;
        }

        const script = document.createElement('script');
        script.async = true;
        script.defer = true;
        script.dataset.ygGoogleMaps = 'true';
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places&v=weekly&language=ar&region=YE&loading=async`;
        script.onload = () => {
            if (!window.google?.maps) {
                googleMapsLoadPromise = null;
                reject(new Error('GoogleMapsLoadFailed'));
                return;
            }

            window.initMap?.();
            resolve(window.google.maps);
        };
        script.onerror = () => {
            googleMapsLoadPromise = null;
            reject(new Error('GoogleMapsLoadFailed'));
        };

        document.head.appendChild(script);
    });

    return googleMapsLoadPromise;
};

const waitForGoogleMaps = (timeout = 12000) => Promise.race([
    loadGoogleMapsApi(),
    new Promise((_, reject) => {
        window.setTimeout(() => reject(new Error('GoogleMapsLoadTimeout')), timeout);
    })
]);

const syncNearbyMapModeButtons = activeMode => {
    document.querySelectorAll('[data-map-mode]').forEach(button => {
        const isActive = button.dataset.mapMode === activeMode;
        button.classList.toggle('is-active', isActive);
        button.setAttribute('aria-pressed', String(isActive));
    });
};

const nearbyHiddenLabelsStyle = [
    { elementType: 'labels', stylers: [{ visibility: 'off' }] },
    { featureType: 'administrative', elementType: 'labels', stylers: [{ visibility: 'off' }] },
    { featureType: 'poi', elementType: 'labels', stylers: [{ visibility: 'off' }] },
    { featureType: 'road', elementType: 'labels', stylers: [{ visibility: 'off' }] },
    { featureType: 'transit', elementType: 'labels', stylers: [{ visibility: 'off' }] }
];

const getNearbyEffectiveMapType = () =>
    nearbyMapType === 'satellite' && nearbyLabelsVisible ? 'hybrid' : nearbyMapType;

const setNearbyToggleState = (element, active) => {
    if (!element) return;
    element.classList.toggle('is-active', active);
    element.setAttribute('aria-pressed', String(active));
};

const applyNearbyMapAppearance = () => {
    if (!map) return;

    const effectiveType = getNearbyEffectiveMapType();
    map.setMapTypeId(effectiveType);
    map.setTilt(effectiveType === 'hybrid' || effectiveType === 'satellite' ? 45 : 0);
    map.setOptions({ styles: nearbyLabelsVisible ? null : nearbyHiddenLabelsStyle });

    nearbyMarkers.forEach(marker => marker.setVisible(nearbyPlacesVisible));
    nearbyRoutes.forEach(route => route.setVisible(nearbyPlacesVisible));

    if (!nearbyTrafficLayer && window.google?.maps) {
        nearbyTrafficLayer = new google.maps.TrafficLayer();
    }
    nearbyTrafficLayer?.setMap(nearbyTrafficVisible ? map : null);
};

const createNearbyPhotoMarker = ({ position, image, title, onClick }) => {
    class NearbyPhotoOverlay extends google.maps.OverlayView {
        constructor() {
            super();
            this.position = position;
            this.image = image;
            this.title = title;
            this.onMarkerClick = onClick;
            this.visible = true;
            this.element = null;
        }

        onAdd() {
            const element = document.createElement('button');
            element.type = 'button';
            element.className = 'nearby-platform-pin';
            element.title = this.title;
            element.setAttribute('aria-label', `عرض ${this.title}`);
            element.innerHTML = `
                <span class="nearby-platform-pin__halo"></span>
                <span class="nearby-platform-pin__photo">
                    <img src="${escapeNearbyHtml(this.image)}" alt=""
                         onerror="this.onerror=null;this.src='/images/default-category.jpg';">
                </span>
                <span class="nearby-platform-pin__verified" aria-hidden="true"><i class="bi bi-check"></i></span>
                <span class="nearby-platform-pin__tip" aria-hidden="true"></span>`;
            element.addEventListener('click', event => {
                event.preventDefault();
                event.stopPropagation();
                this.onMarkerClick?.(this);
            });
            this.element = element;
            this.getPanes().overlayMouseTarget.appendChild(element);
        }

        draw() {
            if (!this.element) return;
            const point = this.getProjection().fromLatLngToDivPixel(
                new google.maps.LatLng(this.position));
            if (!point) return;
            this.element.style.left = `${point.x}px`;
            this.element.style.top = `${point.y}px`;
            this.element.hidden = !this.visible;
        }

        onRemove() {
            this.element?.remove();
            this.element = null;
        }

        setVisible(visible) {
            this.visible = visible;
            if (this.element) this.element.hidden = !visible;
        }

        setActive(active) {
            this.element?.classList.toggle('is-active', active);
        }
    }

    const overlay = new NearbyPhotoOverlay();
    overlay.setMap(map);
    return overlay;
};

// ============================================================================
// ❤️ 1. زر المفضلة الديناميكي 
// ============================================================================
window.toggleFavoriteVip = function (e, placeId, btnElement) {
    if (e) {
        e.preventDefault();
        e.stopPropagation();
    }

    const icon = btnElement.querySelector('i');

    fetch('/Places/ToggleFavorite', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ PlaceId: parseInt(placeId) })
    })
        .then(response => {
            const contentType = response.headers.get("content-type");
            if (contentType && contentType.indexOf("application/json") === -1) {
                throw new Error("RedirectedToLogin");
            }
            return response.json();
        })
        .then(result => {
            if (result.success) {
                if (result.isFavorited) {
                    icon.className = 'bi bi-heart-fill text-danger';
                    btnElement.classList.add('active');
                    if (typeof Swal !== 'undefined') {
                        Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: result.message, showConfirmButton: false, timer: 3000 });
                    }
                } else {
                    icon.className = 'bi bi-heart text-secondary';
                    btnElement.classList.remove('active');
                    if (typeof Swal !== 'undefined') {
                        Swal.fire({ toast: true, position: 'top-end', icon: 'info', title: result.message, showConfirmButton: false, timer: 3000 });
                    }
                }
            } else {
                if (typeof Swal !== 'undefined') {
                    Swal.fire({ icon: 'warning', title: 'تنبيه', text: result.message, confirmButtonText: 'حسناً', confirmButtonColor: '#0bacd3' });
                } else {
                    alert(result.message);
                }
            }
        })
        .catch(error => {
            console.error('Error:', error);
            if (typeof Swal !== 'undefined') {
                if (error.message === "RedirectedToLogin") {
                    Swal.fire({
                        icon: 'info',
                        title: 'تسجيل الدخول',
                        text: 'يرجى تسجيل الدخول أولاً لتتمكن من إضافة الأماكن إلى مفضلتك.',
                        confirmButtonText: 'تسجيل الدخول',
                        confirmButtonColor: '#FF5A5F'
                    }).then((result) => {
                        if (result.isConfirmed) {
                            window.location.href = '/Account/Login';
                        }
                    });
                } else {
                    Swal.fire({ toast: true, position: 'top-end', icon: 'error', title: 'حدث خطأ في الاتصال، يرجى المحاولة لاحقاً.', showConfirmButton: false, timer: 3000 });
                }
            }
        });
};

const escapeNearbyHtml = (value) => String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');

const calculateNearbyDistance = (lat1, lng1, lat2, lng2) => {
    const earthRadiusKm = 6371;
    const toRadians = value => value * Math.PI / 180;
    const deltaLat = toRadians(lat2 - lat1);
    const deltaLng = toRadians(lng2 - lng1);
    const a = Math.sin(deltaLat / 2) ** 2 +
        Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) *
        Math.sin(deltaLng / 2) ** 2;
    return earthRadiusKm * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
};

const formatNearbyDistance = distanceKm => {
    if (distanceKm < 1) return `${Math.max(1, Math.round(distanceKm * 1000))} م`;
    return `${distanceKm.toFixed(distanceKm < 10 ? 1 : 0)} كم`;
};

const formatNearbyPrice = place => {
    if (!place) return '';

    // السعر المهيأ من الخادم هو المصدر الأساسي لأنه يعرف:
    // عملة المستخدم + سوق مدينة المنشأة (صنعاء/عدن).
    const serverPriceText = String(place.priceText ?? '').trim();
    if (serverPriceText) {
        return escapeNearbyHtml(serverPriceText);
    }

    // توافق احتياطي فقط مع أي استجابة قديمة.
    const numericPrice = Number(place.price);
    if (!Number.isFinite(numericPrice) || numericPrice <= 0) return '';
    return `${numericPrice.toLocaleString('ar-YE', { maximumFractionDigits: 0 })} ر.ي`;
};

const renderNearbyResults = (places, userLocation) => {
    const resultsContainer = document.getElementById('nearbyResults');
    if (!resultsContainer) return;

    const rankedPlaces = places
        .map(place => ({
            ...place,
            distanceKm: Number(place.distanceKm) || calculateNearbyDistance(
                userLocation.lat,
                userLocation.lng,
                Number(place.lat),
                Number(place.lng))
        }))
        .slice(0, 3);

    if (!rankedPlaces.length) return;

    resultsContainer.innerHTML = rankedPlaces.map(place => {
        const placeId = Number(place.id) || 0;
        const name = escapeNearbyHtml(place.name || 'وجهة قريبة');
        const category = escapeNearbyHtml(place.category || 'وجهة');
        const city = escapeNearbyHtml(place.city || 'بالقرب منك');
        const image = escapeNearbyHtml(place.image || '/images/default-category.jpg');
        const distance = formatNearbyDistance(place.distanceKm);
        const price = formatNearbyPrice(place);

        return `
                <a class="nearby-v10-result" href="/Places/Details/${placeId}" aria-label="عرض ${name}">
                    <span class="nearby-v10-result-media">
                        <img src="${image}" alt="" loading="lazy"
                             onerror="this.onerror=null;this.src='/images/default-category.jpg';">
                    </span>
                    <span class="nearby-v10-result-copy">
                        <strong>${name}</strong>
                        <small>${category} · ${city}</small>
                        <span class="nearby-v10-result-meta">
                            <i class="bi bi-geo-alt-fill"></i>
                            ${distance} عن موقعك
                        </span>
                        ${price ? `<span class="nearby-v10-result-price"><i class="bi bi-tag-fill"></i> يبدأ من ${price}</span>` : ''}
                        ${place.hasActiveOffer ? `<span class="nearby-v10-result-offer"><i class="bi bi-percent"></i> عرض متاح</span>` : ''}
                    </span>
                </a>`;
    }).join('');

    resultsContainer.classList.remove('is-loaded');
    window.requestAnimationFrame(() => resultsContainer.classList.add('is-loaded'));
};

const loadNearbyPlaces = async (userLocation, options = {}) => {
    const nearbyGpsBtn = document.getElementById('nearbyGpsBtn');
    const nearbyStatus = document.getElementById('nearbyV10Status');
    const mapDiv = document.getElementById('map');
    if (!nearbyGpsBtn || !map || !window.google?.maps) return;

    const nearbyEndpoint = nearbyGpsBtn.dataset.nearbyUrl || '/Places/GetNearbyPlaces';
    const nearbyUrl = new URL(nearbyEndpoint, window.location.origin);
    nearbyUrl.searchParams.set('lat', String(userLocation.lat));
    nearbyUrl.searchParams.set('lng', String(userLocation.lng));
    nearbyUrl.searchParams.set('radiusKm', document.getElementById('nearbyRadius')?.value || '15');
    nearbyUrl.searchParams.set('sort', document.getElementById('nearbySort')?.value || 'distance');

    const categoryId = document.getElementById('nearbyCategory')?.value;
    if (categoryId) nearbyUrl.searchParams.set('categoryId', categoryId);

    const onlyWithOffers =
        document.getElementById('nearbyOffersToggle')
            ?.getAttribute('aria-pressed') === 'true';

    const onlyBookable =
        document.getElementById('nearbyBookableToggle')
            ?.getAttribute('aria-pressed') === 'true';

    nearbyUrl.searchParams.set('onlyWithOffers', String(onlyWithOffers));
    nearbyUrl.searchParams.set('onlyBookable', String(onlyBookable));

    if (nearbyStatus) nearbyStatus.textContent = 'جارٍ فلترة المنشآت القريبة...';

    const response = await fetch(nearbyUrl.toString(), {
        method: 'GET',
        credentials: 'same-origin',
        headers: {
            Accept: 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
        }
    });

    const responseText = await response.text();
    let payload;
    try {
        payload = responseText ? JSON.parse(responseText) : [];
    } catch {
        throw new Error(`NearbyInvalidJson:${response.status}`);
    }

    if (!response.ok || payload?.success === false) {
        throw new Error(payload?.message || `NearbyHttpError:${response.status}`);
    }

    const places = Array.isArray(payload)
        ? payload
        : Array.isArray(payload?.places) ? payload.places : [];

    nearbyOpenInfoWindow?.close();
    nearbyOpenInfoWindow = null;
    nearbyActiveMarker = null;
    nearbyActiveRoute = null;
    nearbyMarkers.forEach(marker => marker.setMap(null));
    nearbyRoutes.forEach(route => route.setMap(null));
    nearbyMarkers = [];
    nearbyRoutes = [];

    if (!places.length) {
        const resultsContainer = document.getElementById('nearbyResults');
        if (resultsContainer) {
            resultsContainer.innerHTML = `
                <article class="nearby-v10-result nearby-v10-result--placeholder">
                    <span class="nearby-v10-result-icon"><i class="bi bi-search"></i></span>
                    <span><strong>لا توجد نتائج في هذا النطاق</strong><small>وسّع المسافة أو اختر كل الفئات</small></span>
                </article>`;
        }
        if (nearbyStatus) nearbyStatus.textContent = 'لا توجد منشآت مطابقة ضمن النطاق المختار';
        if (!options.silent) {
            Swal.fire({ icon: 'info', title: 'لا توجد نتائج', text: 'جرّب توسيع نطاق البحث أو اختيار فئة أخرى.', confirmButtonColor: '#05203C' });
        }
        return [];
    }

    renderNearbyResults(places, userLocation);
    const bounds = new google.maps.LatLngBounds();
    bounds.extend(userLocation);

    places.forEach(place => {
        const placeLocation = { lat: Number(place.lat), lng: Number(place.lng) };
        if (!Number.isFinite(placeLocation.lat) || !Number.isFinite(placeLocation.lng)) return;

        const safeName = escapeNearbyHtml(place.name || 'وجهة قريبة');
        const safeCategory = escapeNearbyHtml(place.category || 'وجهة');
        const safeCity = escapeNearbyHtml(place.city || 'بالقرب منك');
        const safeAddress = escapeNearbyHtml(place.address || '');
        const safeImage = escapeNearbyHtml(place.image || '/images/default-category.jpg');
        const distance = formatNearbyDistance(Number(place.distanceKm) || calculateNearbyDistance(
            userLocation.lat, userLocation.lng, placeLocation.lat, placeLocation.lng));
        const price = formatNearbyPrice(place);

        const infoWindow = new google.maps.InfoWindow({
            disableAutoPan: false,
            pixelOffset: new google.maps.Size(0, -58),
            content: `
                <div class="nearby-map-card nearby-map-card--royal" dir="rtl">
                    <span class="nearby-map-card__shine" aria-hidden="true"></span>
                    <img src="${safeImage}" alt="" onerror="this.onerror=null;this.src='/images/default-category.jpg';">
                    <div class="nearby-map-card__copy">
                        <span class="nearby-map-card__verified"><i class="bi bi-patch-check-fill"></i> منشأة موثقة</span>
                        <strong>${safeName}</strong>
                        <small>${safeCategory} · ${safeCity}</small>
                        ${safeAddress ? `<p><i class="bi bi-geo-alt"></i> ${safeAddress}</p>` : ''}
                        <span class="nearby-map-card__distance"><i class="bi bi-sign-turn-slight-left"></i> ${distance} عن موقعك</span>
                        ${price ? `<b><i class="bi bi-tag-fill"></i> يبدأ من ${price}</b>` : ''}
                        <a href="/Places/Details/${Number(place.id) || 0}">استكشف المنشأة <i class="bi bi-arrow-left"></i></a>
                    </div>
                </div>`
        });

        const route = new google.maps.Polyline({
            path: [userLocation, placeLocation],
            map,
            visible: nearbyPlacesVisible,
            geodesic: true,
            strokeOpacity: 0,
            strokeWeight: 2,
            icons: [{
                icon: { path: 'M 0,-1 0,1', strokeOpacity: .72, strokeColor: '#00A6DA', scale: 2 },
                offset: '0',
                repeat: '13px'
            }]
        });

        const marker = createNearbyPhotoMarker({
            position: placeLocation,
            image: safeImage,
            title: place.name || 'وجهة قريبة',
            onClick: selectedMarker => {
                nearbyOpenInfoWindow?.close();
                nearbyActiveMarker?.setActive(false);
                nearbyActiveRoute?.setOptions({
                    strokeOpacity: 0,
                    strokeWeight: 2,
                    icons: [{
                        icon: { path: 'M 0,-1 0,1', strokeOpacity: .72, strokeColor: '#00A6DA', scale: 2 },
                        offset: '0',
                        repeat: '13px'
                    }]
                });

                nearbyActiveMarker = selectedMarker;
                nearbyActiveRoute = route;
                nearbyOpenInfoWindow = infoWindow;
                selectedMarker.setActive(true);
                route.setOptions({
                    visible: nearbyPlacesVisible,
                    strokeColor: '#ff755d',
                    strokeOpacity: .42,
                    strokeWeight: 4,
                    icons: [{
                        icon: {
                            path: google.maps.SymbolPath.FORWARD_CLOSED_ARROW,
                            fillColor: '#ff755d',
                            fillOpacity: 1,
                            strokeColor: '#ffffff',
                            strokeWeight: 1.2,
                            scale: 3.2
                        },
                        offset: '0',
                        repeat: '46px'
                    }]
                });
                map.panTo(placeLocation);
                if ((map.getZoom() || 0) < 15) map.setZoom(15);
                infoWindow.setPosition(placeLocation);
                infoWindow.open(map);
            }
        });
        marker.setVisible(nearbyPlacesVisible);
        nearbyMarkers.push(marker);
        nearbyRoutes.push(route);
        bounds.extend(placeLocation);
    });

    // نحافظ على حركة خطوط المسارات نفسها، لكن لا نستهلك CPU/GPU
    // عندما تكون الخريطة خارج الشاشة أو التبويب مخفياً.
    if (typeof window.nearbyRouteAnimationCleanup === 'function') {
        window.nearbyRouteAnimationCleanup();
    }

    if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        const mapAnimationTarget =
            document.getElementById('nearbyMapShell') ||
            document.getElementById('map');

        let routeOffset = 0;
        let animationFrameId = 0;
        let lastRouteFrameAt = 0;
        let routeAnimationVisible = true;

        const stopNearbyRouteAnimation = () => {
            if (animationFrameId) {
                window.cancelAnimationFrame(animationFrameId);
                animationFrameId = 0;
            }
        };

        const animateNearbyRoutes = timestamp => {
            animationFrameId = 0;

            if (document.hidden || !routeAnimationVisible) {
                return;
            }

            if (timestamp - lastRouteFrameAt >= 70) {
                lastRouteFrameAt = timestamp;
                routeOffset = (routeOffset + 1) % 100;

                nearbyRoutes.forEach(route => {
                    const icons = route.get('icons');
                    if (!icons?.length) return;
                    icons[0].offset = `${routeOffset}%`;
                    route.set('icons', icons);
                });
            }

            animationFrameId =
                window.requestAnimationFrame(animateNearbyRoutes);
        };

        const startNearbyRouteAnimation = () => {
            if (
                animationFrameId ||
                document.hidden ||
                !routeAnimationVisible ||
                nearbyRoutes.length === 0
            ) {
                return;
            }

            animationFrameId =
                window.requestAnimationFrame(animateNearbyRoutes);
        };

        const handleNearbyRouteVisibility = () => {
            if (document.hidden) {
                stopNearbyRouteAnimation();
            } else {
                startNearbyRouteAnimation();
            }
        };

        let routeObserver = null;

        if (mapAnimationTarget && 'IntersectionObserver' in window) {
            routeObserver = new IntersectionObserver(entries => {
                routeAnimationVisible =
                    entries.some(entry => entry.isIntersecting);

                if (routeAnimationVisible) {
                    startNearbyRouteAnimation();
                } else {
                    stopNearbyRouteAnimation();
                }
            }, { threshold: 0.04 });

            routeObserver.observe(mapAnimationTarget);
        }

        document.addEventListener(
            'visibilitychange',
            handleNearbyRouteVisibility
        );

        window.nearbyRouteAnimationCleanup = () => {
            stopNearbyRouteAnimation();
            routeObserver?.disconnect();
            document.removeEventListener(
                'visibilitychange',
                handleNearbyRouteVisibility
            );
        };

        startNearbyRouteAnimation();
    }

    map.fitBounds(bounds);
    google.maps.event.addListenerOnce(map, 'idle', () => {
        if (map.getZoom() > 17) map.setZoom(17);
    });
    applyNearbyMapAppearance();
    if (nearbyStatus) nearbyStatus.textContent = `وجدنا ${places.length} منشآت مطابقة بالقرب منك`;
    if (!options.silent) {
        Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: `اكتشفنا ${places.length} منشآت حولك!`, showConfirmButton: false, timer: 3500 });
    }
    if (!window.isCompassMagic && options.scroll !== false) {
        mapDiv?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
    return places;
};

// ============================================================================
// 🧭 2. وظيفة البوصلة السحرية (5 ثوانٍ دوران ثم النزول للخريطة الجاهزة)
// ============================================================================
window.scrollToNearby = function () {
    if (window.isCompassMagic) return;

    const compassBtn = document.getElementById('compassMagicBtn');
    const compassPortal = document.getElementById('dlxCompassPortal');
    const compassStatus = document.getElementById('dlxCompassStatus');
    const nearbyGpsBtn = document.getElementById('nearbyGpsBtn');

    if (compassBtn) compassBtn.classList.add('spin-compass-anim');
    if (compassPortal) {
        compassPortal.classList.add('spin-compass-anim');
        compassPortal.setAttribute('aria-busy', 'true');
    }
    if (compassStatus) {
        compassStatus.textContent = 'نحدد موقعك ونجهّز الأماكن القريبة...';
    }

    // تفعيل وضع "البوصلة السحرية" لمنع النزول الفوري
    window.isCompassMagic = true;

    // الضغط برمجياً على زر GPS ليبدأ التجهيز في الخلفية
    if (nearbyGpsBtn) {
        nearbyGpsBtn.click();
    }

    // ⏳ انتظار 5 ثوانٍ بالضبط قبل النزول (كما طلبت) ⏳
    setTimeout(() => {
        if (compassBtn) compassBtn.classList.remove('spin-compass-anim');
        if (compassPortal) {
            compassPortal.classList.remove('spin-compass-anim');
            compassPortal.removeAttribute('aria-busy');
        }
        if (compassStatus) {
            compassStatus.textContent = 'الأماكن القريبة جاهزة — انطلق الآن';
        }

        const mapDiv = document.getElementById('map');
        // النزول للخريطة (التي يفترض أنها فُتحت وجهزت خلال الـ 5 ثواني)
        if (mapDiv && mapDiv.style.display === 'block') {
            mapDiv.scrollIntoView({ behavior: 'smooth', block: 'center' });
        } else {
            const targetSection = document.getElementById('nearby-section');
            if (targetSection) targetSection.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }

        // إنهاء وضع البوصلة السحرية
        window.isCompassMagic = false;
    }, 5000);
};

document.addEventListener('DOMContentLoaded', function () {

    // ============================================================================
    // 🎭 3 + 💎 4. LUXURY CINEMATIC GATEWAY V4
    // Premium seasons + admin slides + dots + swipe + subtle motion
    // ============================================================================

    const heroV4 =
        document.getElementById(
            'homeHeroV4'
        );

    const seasonFieldV4 =
        document.getElementById(
            'heroSeasonField'
        );

    const slidesV4 =
        heroV4
            ? Array.from(
                heroV4.querySelectorAll(
                    '.dlx-slide, .hero-slider .slide'
                )
            )
            : [];

    const typedV4 =
        document.getElementById(
            'typed-text'
        );

    const mainTitleV4 =
        document.getElementById(
            'dynamicMainTitle'
        );

    const accentWrapperV4 =
        document.getElementById(
            'dynamicAccentWrapper'
        );

    const mainRowV4 =
        document.getElementById(
            'heroMainTitleRow'
        );

    const dynamicRowV4 =
        document.getElementById(
            'heroDynamicTitleRow'
        );

    const dotsHostV4 =
        document.getElementById(
            'heroSliderDots'
        );

    const sliderNavV4 =
        document.getElementById(
            'heroSliderNav'
        );

    const contextTextV42 =
        document.getElementById(
            'heroV42ContextText'
        );

    const slideCurrentV42 =
        document.getElementById(
            'heroV42SlideCurrent'
        );

    const slideTotalV42 =
        document.getElementById(
            'heroV42SlideTotal'
        );

    const progressFillV42 =
        document.getElementById(
            'heroV42ProgressFill'
        );

    const seasonSignatureV42 =
        document.getElementById(
            'heroV42SeasonSignature'
        );

    const seasonTextV42 =
        document.getElementById(
            'heroV42SeasonText'
        );

    const searchStatusV42 =
        document.getElementById(
            'heroV42SearchStatus'
        );

    const searchStatusTextV42 =
        document.getElementById(
            'heroV42StatusText'
        );

    const reduceV4 =
        window.matchMedia(
            '(prefers-reduced-motion: reduce)'
        );

    const finePointerV4 =
        window.matchMedia(
            '(hover:hover) and (pointer:fine)'
        );


    // ------------------------------------------------------------------------
    // Utilities
    // ------------------------------------------------------------------------

    function v4SafeHex(
        value,
        fallback
    ) {
        const color =
            String(value || '')
                .trim();

        return /^#[0-9a-fA-F]{6}$/.test(
            color
        )
            ? color
            : fallback;
    }


    function v4SeasonName() {
        if (!heroV4) {
            return 'Normal';
        }

        if (heroV4.dataset.season) {
            return heroV4.dataset.season;
        }

        const themeClass =
            Array
                .from(
                    heroV4.classList
                )
                .find(
                    item =>
                        item.startsWith(
                            'theme-'
                        )
                );

        return themeClass
            ? themeClass.slice(6)
            : 'Normal';
    }


    function syncV42SeasonSignature() {
        if (
            !seasonSignatureV42 ||
            !seasonTextV42
        ) {
            return;
        }

        const season =
            v4SeasonName();

        const labels = {
            Ramadan: 'RAMADAN EDITION',
            Eid: 'EID EDITION',
            Rain: 'RAIN EDITION',
            Snow: 'SNOW EDITION',
            Spring: 'SPRING EDITION',
            Autumn: 'AUTUMN EDITION',
            Gold: 'GOLD EDITION'
        };

        const label =
            labels[season];

        if (!label) {
            seasonSignatureV42.hidden = true;
            seasonTextV42.textContent = '';
            return;
        }

        seasonTextV42.textContent =
            label;

        seasonSignatureV42.hidden =
            false;
    }


    // ------------------------------------------------------------------------
    // Fit EXACTLY two title rows — never allow a third line.
    // ------------------------------------------------------------------------

    function fitV4Row(
        element,
        maxSize,
        minSize
    ) {
        if (!element) {
            return;
        }

        let size =
            maxSize;

        element.style.fontSize =
            `${size}px`;

        let guard = 0;

        while (
            element.scrollWidth >
            element.clientWidth + 1 &&
            size > minSize &&
            guard < 100
        ) {
            size -= .5;

            element.style.fontSize =
                `${size}px`;

            guard++;
        }
    }


    function fitV4Typography() {
        // الواجهتان V5/V6 تستخدمان قياساً متعدد الأسطر حتى لا يُقص نص الأدمن الطويل.
        if (heroV4?.classList.contains('hero-cinematic-v5') || heroV4?.classList.contains('dlx-hero')) {
            return;
        }

        const width =
            window.innerWidth;

        if (width <= 390) {
            fitV4Row(
                mainRowV4,
                29,
                18
            );

            fitV4Row(
                dynamicRowV4,
                29,
                17
            );

            return;
        }

        if (width <= 767) {
            fitV4Row(
                mainRowV4,
                33,
                20
            );

            fitV4Row(
                dynamicRowV4,
                33,
                19
            );

            return;
        }

        if (width <= 1199) {
            fitV4Row(
                mainRowV4,
                50,
                30
            );

            fitV4Row(
                dynamicRowV4,
                50,
                29
            );

            return;
        }

        fitV4Row(
            mainRowV4,
            65,
            38
        );

        fitV4Row(
            dynamicRowV4,
            65,
            36
        );
    }


    // ------------------------------------------------------------------------
    // Premium seasonal atmosphere — no emoji.
    // ------------------------------------------------------------------------

    function v4Particle(
        type,
        index
    ) {
        const el =
            document.createElement(
                'span'
            );

        el.className =
            `v4-season-particle v4-season-particle--${type}`;

        const x =
            (
                index * 43 +
                Math.random() * 16
            ) % 100;

        const duration =
            11 +
            Math.random() * 11;

        el.style.setProperty(
            '--x',
            `${x}%`
        );

        el.style.setProperty(
            '--duration',
            `${duration.toFixed(2)}s`
        );

        el.style.setProperty(
            '--delay',
            `${(-Math.random() * duration).toFixed(2)}s`
        );

        el.style.setProperty(
            '--opacity',
            (
                .12 +
                Math.random() * .20
            ).toFixed(2)
        );

        el.style.setProperty(
            '--drift',
            `${(
                -28 +
                Math.random() * 56
            ).toFixed(2)
            }px`
        );


        if (
            type === 'star' ||
            type === 'ramadan' ||
            type === 'gold' ||
            type === 'snow'
        ) {
            el.style.setProperty(
                '--size',
                `${(
                    2 +
                    Math.random() * 3.5
                ).toFixed(2)
                }px`
            );
        }


        if (type === 'eid') {
            const colors = [
                '#7DE6FF',
                '#FFFFFF',
                '#FFB266',
                '#FF7663'
            ];

            el.style.setProperty(
                '--particle-color',
                colors[
                index %
                colors.length
                ]
            );

            el.style.setProperty(
                '--w',
                `${(
                    2 +
                    Math.random() * 1.4
                ).toFixed(2)
                }px`
            );

            el.style.setProperty(
                '--h',
                `${(
                    6 +
                    Math.random() * 6
                ).toFixed(2)
                }px`
            );
        }


        if (type === 'rain') {
            el.style.setProperty(
                '--h',
                `${(
                    18 +
                    Math.random() * 18
                ).toFixed(2)
                }px`
            );

            el.style.setProperty(
                '--duration',
                `${(
                    .95 +
                    Math.random() * .75
                ).toFixed(2)
                }s`
            );
        }


        if (
            type === 'petal' ||
            type === 'leaf'
        ) {
            el.style.setProperty(
                '--w',
                `${(
                    5 +
                    Math.random() * 4
                ).toFixed(2)
                }px`
            );

            el.style.setProperty(
                '--h',
                `${(
                    8 +
                    Math.random() * 6
                ).toFixed(2)
                }px`
            );


            if (type === 'leaf') {
                const leafColors = [
                    '#EFA057',
                    '#D67840',
                    '#C76237'
                ];

                el.style.setProperty(
                    '--particle-color',
                    leafColors[
                    index %
                    leafColors.length
                    ]
                );
            }
        }


        return el;
    }


    function buildV4Season() {
        if (!seasonFieldV4) {
            return;
        }

        seasonFieldV4.replaceChildren();

        const season =
            v4SeasonName();

        const emblem =
            document.createElement(
                'div'
            );

        emblem.className =
            `v4-season-emblem v4-season-emblem--${season}`;

        const particles =
            document.createElement(
                'div'
            );

        particles.className =
            'v4-season-particles';

        seasonFieldV4.append(
            emblem,
            particles
        );


        if (reduceV4.matches) {
            return;
        }


        const mobile =
            window.innerWidth <=
            767;

        const tiny =
            window.innerWidth <=
            390;


        const configs = {

            Normal: [
                [
                    'star',
                    tiny
                        ? 3
                        : mobile
                            ? 4
                            : 6
                ]
            ],

            Ramadan: [
                [
                    'ramadan',
                    tiny
                        ? 5
                        : mobile
                            ? 7
                            : 10
                ]
            ],

            Eid: [
                [
                    'eid',
                    tiny
                        ? 6
                        : mobile
                            ? 8
                            : 12
                ]
            ],

            Rain: [
                [
                    'rain',
                    tiny
                        ? 10
                        : mobile
                            ? 13
                            : 18
                ]
            ],

            Snow: [
                [
                    'snow',
                    tiny
                        ? 8
                        : mobile
                            ? 11
                            : 16
                ]
            ],

            Spring: [
                [
                    'petal',
                    tiny
                        ? 5
                        : mobile
                            ? 7
                            : 10
                ]
            ],

            Autumn: [
                [
                    'leaf',
                    tiny
                        ? 5
                        : mobile
                            ? 7
                            : 10
                ]
            ],

            Gold: [
                [
                    'gold',
                    tiny
                        ? 7
                        : mobile
                            ? 9
                            : 13
                ]
            ]
        };


        const groups =
            configs[season] ||
            configs.Normal;

        let offset =
            0;

        groups.forEach(
            ([type, count]) => {

                for (
                    let index = 0;
                    index < count;
                    index++
                ) {
                    particles.appendChild(
                        v4Particle(
                            type,
                            offset + index
                        )
                    );
                }

                offset += count;
            }
        );
    }


    // ------------------------------------------------------------------------
    // Slider
    // ------------------------------------------------------------------------

    let activeV4 =
        Math.max(
            0,
            slidesV4.findIndex(
                slide =>
                    slide.classList.contains(
                        'active'
                    )
            )
        );

    let slideTimerV4 =
        null;

    let typingTimerV4 =
        null;

    let touchStartXV4 =
        0;

    let touchStartYV4 =
        0;

    let dlxUiColorsLockedV4 =
        false;


    function slideDataV4(
        slide
    ) {
        return {

            text:
                slide?.getAttribute(
                    'data-text'
                ) ||
                'أجمل الوجهات',

            main:
                v4SafeHex(
                    slide?.getAttribute(
                        'data-maincolor'
                    ),
                    '#ffffff'
                ),

            accent:
                v4SafeHex(
                    slide?.getAttribute(
                        'data-accentcolor'
                    ),
                    '#ff5a5f'
                )

        };
    }


    function applyV4Colors(
        data
    ) {
        if (!heroV4) {
            return;
        }

        if (heroV4.classList.contains('dlx-hero')) {
            const dlxTitle = heroV4.querySelector('.dlx-title');

            // لون النص الديناميكي وحده يتبع لون الشريحة كما اختاره الأدمن.
            dlxTitle?.style.setProperty('--dlx-accent', data.accent);

            // العنوان الرئيسي وبقية الواجهة يحتفظان بألوان أول شريحة ويبقيان ثابتين.
            if (!dlxUiColorsLockedV4) {
                dlxTitle?.style.setProperty('--dlx-main', data.main);
                heroV4.style.setProperty('--dlx-main', data.main);
                heroV4.style.setProperty('--dlx-accent', data.accent);
                dlxUiColorsLockedV4 = true;
            }
        } else {
            heroV4.style.setProperty(
                '--v4-main',
                data.main
            );

            heroV4.style.setProperty(
                '--v4-accent',
                data.accent
            );
        }


        if (
            mainTitleV4 &&
            !heroV4.classList.contains('dlx-hero')
        ) {
            mainTitleV4.style.color =
                data.main;
        }


        if (accentWrapperV4) {
            accentWrapperV4.style.color =
                data.accent;
        }

        // في تصميم DLX يبقى اسم المكان الجانبي ثابتاً؛ النص الديناميكي وحده يتغيّر.
        if (
            contextTextV42 &&
            !heroV4.classList.contains('dlx-hero')
        ) {
            contextTextV42.textContent =
                data.text ||
                'وجهات مختارة';
        }
    }


    function typeV4Text(
        text
    ) {
        if (!typedV4) {
            return;
        }

        window.clearTimeout(
            typingTimerV4
        );

        typedV4.textContent =
            '';

        if (reduceV4.matches) {
            typedV4.textContent =
                text;

            fitV4Typography();
            return;
        }


        let index =
            0;


        const step =
            () => {

                typedV4.textContent =
                    text.substring(
                        0,
                        index + 1
                    );

                index++;

                fitV4Typography();


                if (index < text.length) {
                    typingTimerV4 =
                        window.setTimeout(
                            step,
                            52
                        );
                }
            };


        step();
    }


    function syncV4Dots() {
        if (!dotsHostV4) {
            return;
        }

        dotsHostV4
            .querySelectorAll(
                '.hero-v4-dot'
            )
            .forEach(
                (dot, index) => {

                    const active =
                        index ===
                        activeV4;

                    dot.classList.toggle(
                        'is-active',
                        active
                    );

                    dot.setAttribute(
                        'aria-current',
                        active
                            ? 'true'
                            : 'false'
                    );
                }
            );

        if (slideCurrentV42) {
            slideCurrentV42.textContent =
                String(activeV4 + 1)
                    .padStart(2, '0');
        }

        if (slideTotalV42) {
            slideTotalV42.textContent =
                String(slidesV4.length || 1)
                    .padStart(2, '0');
        }
    }


    function restartV42Progress() {
        if (!progressFillV42) {
            return;
        }

        progressFillV42.classList.remove(
            'is-running'
        );

        void progressFillV42.offsetWidth;

        if (!reduceV4.matches) {
            progressFillV42.classList.add(
                'is-running'
            );
        }
    }


    function restartV4Auto() {
        window.clearInterval(
            slideTimerV4
        );

        if (
            slidesV4.length <= 1
        ) {
            return;
        }

        slideTimerV4 =
            window.setInterval(
                () => {

                    goV4Slide(
                        activeV4 + 1,
                        false
                    );

                },
                5000
            );
    }


    function goV4Slide(
        requested,
        userInitiated = true
    ) {
        if (!slidesV4.length) {
            return;
        }

        const next =
            (
                requested %
                slidesV4.length +
                slidesV4.length
            ) %
            slidesV4.length;


        if (
            next === activeV4 &&
            slidesV4[next]
                .classList
                .contains(
                    'active'
                )
        ) {
            if (userInitiated) {
                restartV4Auto();
            }

            return;
        }


        const previousSlide = slidesV4[activeV4];

        if (previousSlide?.tagName === 'VIDEO') {
            previousSlide.pause();
        }

        previousSlide
            ?.classList
            .remove(
                'active'
            );


        activeV4 =
            next;


        const slide =
            slidesV4[activeV4];

        slide.classList.add(
            'active'
        );

        // في V6.11 تتحرك الصورة والنص الديناميكي فقط؛ كل بقية عناصر الواجهة ثابتة.
        if (heroV4?.classList.contains('dlx-hero')) {
            heroV4.classList.remove('dlx-is-changing');
            void heroV4.offsetWidth;
            heroV4.classList.add('dlx-is-changing');
            window.setTimeout(() => heroV4.classList.remove('dlx-is-changing'), 900);
        }


        if (
            slide.tagName ===
            'VIDEO'
        ) {
            slide.currentTime =
                0;

            if (!document.hidden && !heroV4?.classList.contains('yg-perf-paused')) {
                slide
                    .play()
                    .catch(
                        () => { }
                    );
            } else {
                slide.pause();
            }
        }


        const data =
            slideDataV4(
                slide
            );


        applyV4Colors(
            data
        );


        if (accentWrapperV4) {
            accentWrapperV4
                .classList
                .add(
                    'is-changing'
                );
        }


        window.setTimeout(
            () => {

                typeV4Text(
                    data.text
                );

                accentWrapperV4
                    ?.classList
                    .remove(
                        'is-changing'
                    );

            },
            reduceV4.matches
                ? 0
                : 130
        );


        syncV4Dots();
        restartV42Progress();


        if (userInitiated) {
            restartV4Auto();
        }
    }


    function buildV4Dots() {
        if (
            !dotsHostV4 ||
            !sliderNavV4
        ) {
            return;
        }

        dotsHostV4.replaceChildren();


        slidesV4.forEach(
            (_, index) => {

                const dot =
                    document.createElement(
                        'button'
                    );

                dot.type =
                    'button';

                dot.className =
                    'hero-v4-dot';

                dot.setAttribute(
                    'aria-label',
                    `عرض الشريحة ${index + 1}`
                );


                dot.addEventListener(
                    'click',
                    () => {

                        goV4Slide(
                            index,
                            true
                        );

                    }
                );


                dotsHostV4.appendChild(
                    dot
                );
            }
        );


        sliderNavV4
            .classList
            .toggle(
                'is-single',
                slidesV4.length <= 1
            );


        syncV4Dots();
    }


    if (slidesV4.length) {

        const firstData =
            slideDataV4(
                slidesV4[activeV4]
            );

        applyV4Colors(
            firstData
        );

        typeV4Text(
            firstData.text
        );

        buildV4Dots();
        restartV42Progress();

        restartV4Auto();
    }


    // ------------------------------------------------------------------------
    // Swipe
    // ------------------------------------------------------------------------

    heroV4?.addEventListener(
        'touchstart',
        event => {

            const touch =
                event.touches?.[0];

            if (!touch) {
                return;
            }

            touchStartXV4 =
                touch.clientX;

            touchStartYV4 =
                touch.clientY;
        },
        {
            passive: true
        }
    );


    heroV4?.addEventListener(
        'touchend',
        event => {

            const touch =
                event.changedTouches?.[0];

            if (!touch) {
                return;
            }


            const dx =
                touch.clientX -
                touchStartXV4;

            const dy =
                touch.clientY -
                touchStartYV4;


            if (
                Math.abs(dx) < 42 ||
                Math.abs(dx) <
                Math.abs(dy) * 1.25
            ) {
                return;
            }


            if (dx < 0) {
                goV4Slide(
                    activeV4 + 1,
                    true
                );
            } else {
                goV4Slide(
                    activeV4 - 1,
                    true
                );
            }
        },
        {
            passive: true
        }
    );


    // ------------------------------------------------------------------------
    // Subtle desktop pointer depth
    // ------------------------------------------------------------------------

    if (
        heroV4 &&
        finePointerV4.matches &&
        !reduceV4.matches
    ) {
        const radar =
            heroV4.querySelector(
                '.hero-v4-radar'
            );


        heroV4.addEventListener(
            'pointermove',
            event => {

                const rect =
                    heroV4
                        .getBoundingClientRect();

                if (
                    !rect.width ||
                    !rect.height
                ) {
                    return;
                }


                const x =
                    Math.max(
                        0,
                        Math.min(
                            1,
                            (
                                event.clientX -
                                rect.left
                            ) /
                            rect.width
                        )
                    );

                const y =
                    Math.max(
                        0,
                        Math.min(
                            1,
                            (
                                event.clientY -
                                rect.top
                            ) /
                            rect.height
                        )
                    );


                const cx =
                    x - .5;

                const cy =
                    y - .5;


                heroV4.style.setProperty(
                    '--v4-pointer-x',
                    `${(x * 100).toFixed(1)}%`
                );

                heroV4.style.setProperty(
                    '--v4-pointer-y',
                    `${(y * 100).toFixed(1)}%`
                );

                heroV4.style.setProperty(
                    '--v4-bg-x',
                    `${(cx * -3.5).toFixed(2)}px`
                );

                heroV4.style.setProperty(
                    '--v4-bg-y',
                    `${(cy * -2.5).toFixed(2)}px`
                );


                if (radar) {
                    radar.style.setProperty(
                        '--radar-tilt-y',
                        `${(cx * 4.5).toFixed(2)}deg`
                    );

                    radar.style.setProperty(
                        '--radar-tilt-x',
                        `${(cy * -4).toFixed(2)}deg`
                    );
                }
            },
            {
                passive: true
            }
        );


        heroV4.addEventListener(
            'pointerleave',
            () => {

                heroV4.style.setProperty(
                    '--v4-pointer-x',
                    '50%'
                );

                heroV4.style.setProperty(
                    '--v4-pointer-y',
                    '42%'
                );

                heroV4.style.setProperty(
                    '--v4-bg-x',
                    '0px'
                );

                heroV4.style.setProperty(
                    '--v4-bg-y',
                    '0px'
                );


                if (radar) {
                    radar.style.setProperty(
                        '--radar-tilt-x',
                        '0deg'
                    );

                    radar.style.setProperty(
                        '--radar-tilt-y',
                        '0deg'
                    );
                }


                if (progressFillV42) {
                    progressFillV42.style.animationPlayState =
                        'running';
                }

                restartV4Auto();
            },
            {
                passive: true
            }
        );


        heroV4.addEventListener(
            'mouseenter',
            () => {
                window.clearInterval(
                    slideTimerV4
                );

                if (progressFillV42) {
                    progressFillV42.style.animationPlayState =
                        'paused';
                }
            }
        );
    }


    // ------------------------------------------------------------------------
    // Live discovery status for existing nearby function — visual only
    // ------------------------------------------------------------------------

    const compassButtonV42 =
        document.getElementById(
            'compassMagicBtn'
        );

    compassButtonV42?.addEventListener(
        'click',
        () => {
            if (searchStatusV42) {
                searchStatusV42.classList.add(
                    'is-locating'
                );
            }

            if (searchStatusTextV42) {
                searchStatusTextV42.textContent =
                    'جارٍ تحديد موقعك وتجهيز الأماكن القريبة...';
            }

            window.setTimeout(
                () => {
                    searchStatusV42
                        ?.classList
                        .remove(
                            'is-locating'
                        );

                    if (searchStatusTextV42) {
                        searchStatusTextV42.textContent =
                            'الاستكشاف القريب جاهز';
                    }
                },
                5000
            );
        }
    );


    syncV42SeasonSignature();

    // ------------------------------------------------------------------------
    // Fit after font loading / resize
    // ------------------------------------------------------------------------

    let resizeTimerV4 =
        null;


    window.addEventListener(
        'resize',
        () => {

            window.clearTimeout(
                resizeTimerV4
            );

            resizeTimerV4 =
                window.setTimeout(
                    () => {

                        fitV4Typography();

                        buildV4Season();

                    },
                    120
                );
        },
        {
            passive: true
        }
    );


    if (document.fonts?.ready) {
        document.fonts.ready.then(
            () => {
                fitV4Typography();
            }
        );
    } else {
        fitV4Typography();
    }


    buildV4Season();

    // ============================================================================
    // ⏰ 5. تحديث الساعة والتاريخ الحي
    // ============================================================================
    function updateLiveClock() {
        const now = new Date();
        const timeElement = document.getElementById('live-time');
        const dateElement = document.getElementById('live-date');
        if (timeElement && dateElement) {
            timeElement.textContent = now.toLocaleTimeString('ar-YE', { hour: '2-digit', minute: '2-digit', hour12: true });
            dateElement.textContent = now.toLocaleDateString('ar-YE', { weekday: 'long', month: 'long', day: 'numeric' });
        }
    }
    updateLiveClock();
    setInterval(updateLiveClock, 1000);

    // ============================================================================
    // 🔽 6. القوائم المنسدلة الزجاجية للبحث
    //    تثبيت ذكي ملاصق للحقل + فتح سريع وسلس + دعم RTL والهاتف
    // ============================================================================
    const dropdownStates = [];
    let activeDropdownState = null;
    let dropdownPositionFrame = 0;

    function clearDropdownPositionFrame() {
        if (dropdownPositionFrame) {
            window.cancelAnimationFrame(dropdownPositionFrame);
            dropdownPositionFrame = 0;
        }
    }

    function setDropdownSelectedContent(state, item) {
        if (!state.selectedContent || !item) return;

        const name = item.getAttribute('data-name') || item.textContent?.trim() || '';
        const iconClass = item.getAttribute('data-icon') || 'bi-circle';
        const color = item.getAttribute('data-color') || '#7DE6FF';

        const icon = document.createElement('i');
        icon.className = `bi ${iconClass}`;
        icon.style.color = color;

        const label = document.createElement('span');
        label.textContent = name;

        state.selectedContent.replaceChildren(icon, label);
    }

    function ensureDropdownPortal(state) {
        if (!state?.list) return;

        if (!state.list._ygDropdownOriginalParent) {
            state.list._ygDropdownOriginalParent = state.list.parentElement;
        }

        // نبقي القائمة في body بعد أول فتح. هذا يمنع نقلها ذهاباً وإياباً
        // في كل ضغطة ويقلل إعادة حساب الـ layout والاهتزاز البصري.
        if (state.list.parentElement !== document.body) {
            document.body.appendChild(state.list);
        }

        state.list._ygDropdownOwner = state.dropdown;
    }

    function positionDropdownMenu(state) {
        if (!state || !state.dropdown || !state.list) return;
        if (activeDropdownState !== state) return;

        const rect = state.dropdown.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return;

        ensureDropdownPortal(state);

        const list = state.list;
        const viewportMargin = 10;
        const gap = 8;
        const menuWidth = Math.max(1, rect.width);

        // قياس الارتفاع الحقيقي للقائمة قبل أول إظهار فقط.
        // أثناء scroll/resize تبقى القائمة ظاهرة ولا نعيد تشغيل حركة الفتح.
        const wasVisible = list.classList.contains('show');
        if (!wasVisible) {
            list.classList.add('yg-dropdown-measuring');
        }

        // القائمة Portal داخل body. نفرض fixed + الإحداثيات كـ important
        // حتى لا تستطيع أي قاعدة CSS قديمة تعطيل موضعها.
        list.style.setProperty('position', 'fixed', 'important');
        list.style.setProperty('right', 'auto', 'important');
        list.style.setProperty('bottom', 'auto', 'important');
        list.style.setProperty('width', `${menuWidth}px`, 'important');
        list.style.setProperty('left', `${Math.max(viewportMargin, Math.min(rect.left, window.innerWidth - menuWidth - viewportMargin))}px`, 'important');
        list.style.setProperty('top', '0px', 'important');
        list.style.setProperty('max-height', `${Math.max(96, window.innerHeight - (viewportMargin * 2))}px`, 'important');

        const naturalHeight = Math.ceil(
            Math.min(
                270,
                list.scrollHeight || list.getBoundingClientRect().height
            )
        );
        const availableBelow = Math.max(0, window.innerHeight - rect.bottom - viewportMargin - gap);
        const availableAbove = Math.max(0, rect.top - viewportMargin - gap);

        let placement = 'bottom';
        if (naturalHeight > availableBelow && availableAbove > availableBelow) {
            placement = 'top';
        }

        const availableSpace = placement === 'bottom' ? availableBelow : availableAbove;
        const finalMaxHeight = Math.max(96, Math.min(270, availableSpace || 96));
        list.style.setProperty('max-height', `${finalMaxHeight}px`, 'important');

        // بعد تحديد max-height نعيد القياس حتى تلتصق القائمة بالحقل فعلياً
        // ولا نستخدم رقماً ثابتاً مثل 250px كان يسبب ابتعادها عن الحقل.
        const finalHeight = Math.ceil(list.getBoundingClientRect().height);

        let top;
        if (placement === 'top') {
            top = rect.top - gap - finalHeight;
        } else {
            top = rect.bottom + gap;
        }

        top = Math.max(
            viewportMargin,
            Math.min(top, window.innerHeight - finalHeight - viewportMargin)
        );

        let left = rect.left;
        left = Math.max(
            viewportMargin,
            Math.min(left, window.innerWidth - menuWidth - viewportMargin)
        );

        list.dataset.placement = placement;
        list.style.setProperty('top', `${Math.round(top)}px`, 'important');
        list.style.setProperty('left', `${Math.round(left)}px`, 'important');
        list.style.setProperty('width', `${Math.round(menuWidth)}px`, 'important');

        if (!wasVisible) {
            list.classList.remove('yg-dropdown-measuring');
        }
    }

    function closeDropdown(state, { returnFocus = false } = {}) {
        if (!state) return;

        state.list.classList.remove('show', 'yg-dropdown-measuring');
        state.dropdown.classList.remove('is-open');
        state.header.setAttribute('aria-expanded', 'false');

        if (activeDropdownState === state) {
            activeDropdownState = null;
        }

        if (returnFocus) {
            state.header.focus({ preventScroll: true });
        }
    }

    function closeAllDropdowns(exceptState = null) {
        dropdownStates.forEach(state => {
            if (state !== exceptState) closeDropdown(state);
        });
    }

    function openDropdown(state) {
        if (!state) return;

        closeAllDropdowns(state);
        activeDropdownState = state;
        ensureDropdownPortal(state);

        state.dropdown.classList.add('is-open');
        state.header.setAttribute('aria-expanded', 'true');

        // نحدد المكان أولاً وهي مخفية، ثم نظهرها مباشرة بدون قفزة.
        positionDropdownMenu(state);
        state.list.classList.add('show');
    }

    function toggleDropdown(state) {
        if (!state) return;

        if (activeDropdownState === state && state.list.classList.contains('show')) {
            closeDropdown(state);
            return;
        }

        openDropdown(state);
    }

    document.querySelectorAll('.custom-dropdown').forEach(dropdown => {
        const header = dropdown.querySelector('.dropdown-header');
        const list = dropdown.querySelector('.dropdown-list');
        const input = dropdown.querySelector('input[type="hidden"]');
        const selectedContent = dropdown.querySelector('div[id$="SelectedContent"]');

        if (!header || !list || !input || !selectedContent) return;

        const state = {
            dropdown,
            header,
            list,
            input,
            selectedContent,
            suppressNextClick: false
        };

        dropdownStates.push(state);

        header.setAttribute('aria-haspopup', 'listbox');
        header.setAttribute('aria-expanded', 'false');
        list.setAttribute('role', 'listbox');

        // الكمبيوتر فقط: نفتح من pointerdown بدل انتظار click.
        // هذا يمنع ضياع أول ضغطة عندما يكون الـ Hero تحت تأثير 3D / transform.
        // الهاتف يبقى على click الطبيعي لأنه أصلًا سريع ومستقر.
        header.addEventListener('pointerdown', event => {
            const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)').matches;
            if (!finePointer) return;
            if (event.button !== undefined && event.button !== 0) return;
            if (event.pointerType && event.pointerType !== 'mouse') return;

            event.preventDefault();
            event.stopPropagation();

            state.suppressNextClick = true;
            toggleDropdown(state);

            // احتياط فقط إذا لم يولّد المتصفح click بعد pointerdown.
            window.setTimeout(() => {
                state.suppressNextClick = false;
            }, 450);
        });

        header.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();

            // بعد فتح الكمبيوتر من pointerdown سيصل click مباشرة بعده.
            // نتجاهله مرة واحدة حتى لا يفتح ثم يغلق في نفس الضغطة.
            if (state.suppressNextClick) {
                state.suppressNextClick = false;
                return;
            }

            toggleDropdown(state);
        });

        header.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ' || event.key === 'ArrowDown') {
                event.preventDefault();
                event.stopPropagation();
                openDropdown(state);

                const firstItem = list.querySelector('li');
                firstItem?.focus({ preventScroll: true });
                return;
            }

            if (event.key === 'Escape') {
                event.preventDefault();
                closeDropdown(state);
            }
        });

        const items = Array.from(list.querySelectorAll('li'));
        items.forEach((item, index) => {
            item.tabIndex = -1;
            item.setAttribute('role', 'option');

            item.addEventListener('click', event => {
                event.preventDefault();
                event.stopPropagation();

                const value = item.getAttribute('data-value') ?? '';
                input.value = value;
                setDropdownSelectedContent(state, item);

                items.forEach(option => option.setAttribute('aria-selected', 'false'));
                item.setAttribute('aria-selected', 'true');

                closeDropdown(state, { returnFocus: true });
            });

            item.addEventListener('keydown', event => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    item.click();
                    return;
                }

                if (event.key === 'Escape') {
                    event.preventDefault();
                    closeDropdown(state, { returnFocus: true });
                    return;
                }

                if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                    event.preventDefault();
                    const direction = event.key === 'ArrowDown' ? 1 : -1;
                    const nextIndex = (index + direction + items.length) % items.length;
                    items[nextIndex]?.focus({ preventScroll: true });
                }
            });
        });
    });

    document.addEventListener('click', event => {
        if (!activeDropdownState) return;

        const { dropdown, list } = activeDropdownState;
        if (dropdown.contains(event.target) || list.contains(event.target)) return;

        closeDropdown(activeDropdownState);
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && activeDropdownState) {
            closeDropdown(activeDropdownState, { returnFocus: true });
        }
    });

    const scheduleActiveDropdownPosition = () => {
        if (!activeDropdownState) return;

        clearDropdownPositionFrame();
        dropdownPositionFrame = window.requestAnimationFrame(() => {
            dropdownPositionFrame = 0;
            if (!activeDropdownState) return;
            positionDropdownMenu(activeDropdownState);
            activeDropdownState.list.classList.add('show');
        });
    };

    window.addEventListener('resize', scheduleActiveDropdownPosition, { passive: true });
    window.addEventListener('scroll', event => {
        if (
            activeDropdownState &&
            (event.target === activeDropdownState.list || activeDropdownState.list.contains(event.target))
        ) {
            return;
        }

        scheduleActiveDropdownPosition();
    }, { passive: true, capture: true });

    // ============================================================================
    // ➕ 7. زر عرض المزيد / إخفاء للأماكن المميزة
    // ============================================================================
    const loadMoreBtn = document.getElementById('loadMoreBtn');
    if (loadMoreBtn) {
        let currentlyShown = 4;
        const cards = document.querySelectorAll('.featured-place-item');
        const totalCards = cards.length;

        loadMoreBtn.addEventListener('click', function (e) {
            e.preventDefault();
            if (this.classList.contains('hide-mode')) {
                cards.forEach((card, index) => {
                    if (index >= 4) card.classList.add('d-none');
                });
                currentlyShown = 4;
                this.innerHTML = 'عرض المزيد <i class="bi bi-chevron-down ms-1"></i>';
                this.classList.remove('hide-mode');
                document.getElementById('featured-section').scrollIntoView({ behavior: 'smooth', block: 'start' });
            } else {
                const hiddenCards = document.querySelectorAll('.featured-place-item.d-none');
                for (let i = 0; i < 4 && i < hiddenCards.length; i++) {
                    hiddenCards[i].classList.remove('d-none');
                }
                currentlyShown += 4;

                if (currentlyShown >= totalCards) {
                    this.innerHTML = 'إخفاء <i class="bi bi-chevron-up ms-1"></i>';
                    this.classList.add('hide-mode');
                }
            }
        });
    }

    // ============================================================================
    // PERFORMANCE CLEANUP: حُذفت محركات Categories V7/V8 القديمة لأنها غير مرسومة.
    // ============================================================================
    // 👑 8.6 تاج القمريات للفئات — Coverflow دائري ومتجاوب بلا تحديث للصفحة
    // ============================================================================
    const qamariyaCarousel = document.querySelector('[data-qamariya-carousel]');

    if (qamariyaCarousel) {
        const qamariyaSection = qamariyaCarousel.closest('.qamariya-categories');
        const qamariyaStage = qamariyaCarousel.querySelector('#qamariyaStage');
        const qamariyaCards = Array.from(qamariyaCarousel.querySelectorAll('[data-qamariya-card]'));
        const qamariyaNames = Array.from(qamariyaCarousel.querySelectorAll('[data-qamariya-name]'));
        const qamariyaDots = Array.from(qamariyaCarousel.querySelectorAll('[data-qamariya-target]'));
        const qamariyaPrev = qamariyaCarousel.querySelector('#qamariyaPrev');
        const qamariyaNext = qamariyaCarousel.querySelector('#qamariyaNext');
        const qamariyaCurrent = qamariyaCarousel.querySelector('#qamariyaCurrent');
        const qamariyaProgress = qamariyaCarousel.querySelector('#qamariyaProgress');
        const qamariyaTimer = qamariyaCarousel.querySelector('#qamariyaTimer');
        const qamariyaReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
        const qamariyaDesktopMode = window.matchMedia('(min-width: 768px)');
        const qamariyaInterval = 4000;

        let qamariyaIndex = 0;
        let qamariyaAutoTimer = null;
        let qamariyaVisible = true;
        let qamariyaPointerActive = false;
        let qamariyaPointerStartX = 0;
        let qamariyaPointerStartY = 0;
        let qamariyaDidSwipe = false;
        let qamariyaResizeFrame = 0;

        const normalizeQamariyaIndex = (index) => {
            if (!qamariyaCards.length) return 0;
            return (index + qamariyaCards.length) % qamariyaCards.length;
        };

        const fitQamariyaNames = () => {
            qamariyaNames.forEach((name) => {
                name.style.removeProperty('font-size');

                const availableWidth = name.parentElement?.clientWidth || 0;
                let fontSize = Number.parseFloat(window.getComputedStyle(name).fontSize) || 24;

                while (availableWidth > 0 && name.scrollWidth > availableWidth && fontSize > 13) {
                    fontSize -= 0.5;
                    name.style.fontSize = `${fontSize}px`;
                }
            });
        };

        const stopQamariyaAuto = () => {
            if (qamariyaAutoTimer) {
                window.clearInterval(qamariyaAutoTimer);
                qamariyaAutoTimer = null;
            }
            qamariyaTimer?.classList.remove('is-running');
        };

        const restartQamariyaTimer = () => {
            if (!qamariyaTimer) return;

            qamariyaTimer.classList.remove('is-running');
            void qamariyaTimer.offsetWidth;

            if (!qamariyaReducedMotion.matches && qamariyaCards.length > 1) {
                qamariyaTimer.classList.add('is-running');
            }
        };

        const renderQamariya = (requestedIndex) => {
            if (!qamariyaCards.length) return;

            qamariyaIndex = normalizeQamariyaIndex(requestedIndex);
            const total = qamariyaCards.length;

            qamariyaCards.forEach((card, cardIndex) => {
                const relative = (cardIndex - qamariyaIndex + total) % total;
                const cardLink = card.querySelector('.qamariya-card-link');

                card.classList.remove(
                    'is-active',
                    'is-prev',
                    'is-next',
                    'is-far-prev',
                    'is-far-next'
                );

                if (relative === 0) card.classList.add('is-active');
                else if (relative === 1) card.classList.add('is-next');
                else if (relative === total - 1) card.classList.add('is-prev');

                const visible = relative === 0 || relative === 1 || relative === total - 1;
                card.setAttribute('aria-hidden', visible ? 'false' : 'true');
                if (cardLink) cardLink.tabIndex = visible ? 0 : -1;
            });

            qamariyaDots.forEach((dot, dotIndex) => {
                const active = dotIndex === qamariyaIndex;
                dot.classList.toggle('is-active', active);
                dot.setAttribute('aria-current', active ? 'true' : 'false');
            });

            const activeCard = qamariyaCards[qamariyaIndex];
            const liveAccent = activeCard?.style.getPropertyValue('--qamariya-accent').trim() ||
                'var(--v4-accent, var(--color-orange))';

            qamariyaSection?.style.setProperty('--qamariya-live-accent', liveAccent);
            qamariyaCarousel.style.setProperty('--qamariya-live-accent', liveAccent);

            if (qamariyaCurrent) {
                qamariyaCurrent.textContent = String(qamariyaIndex + 1).padStart(2, '0');
            }

            if (qamariyaProgress) {
                qamariyaProgress.style.width = `${((qamariyaIndex + 1) / total) * 100}%`;
            }

            restartQamariyaTimer();
        };

        const startQamariyaAuto = () => {
            stopQamariyaAuto();

            if (
                qamariyaCards.length <= 1 ||
                qamariyaReducedMotion.matches ||
                document.hidden ||
                !qamariyaVisible ||
                qamariyaPointerActive
            ) return;

            restartQamariyaTimer();
            qamariyaAutoTimer = window.setInterval(() => {
                const qamariyaAutoStep = -1; // نفس اتجاه التقلب التلقائي في الكمبيوتر والهاتف
                renderQamariya(qamariyaIndex + qamariyaAutoStep);
            }, qamariyaInterval);
        };

        const chooseQamariya = (nextIndex) => {
            renderQamariya(nextIndex);
            startQamariyaAuto();
        };

        qamariyaPrev?.addEventListener('click', () => chooseQamariya(qamariyaIndex - 1));
        qamariyaNext?.addEventListener('click', () => chooseQamariya(qamariyaIndex + 1));

        qamariyaDots.forEach((dot) => {
            dot.addEventListener('click', () => {
                const targetIndex = Number(dot.dataset.qamariyaTarget);
                if (Number.isFinite(targetIndex)) chooseQamariya(targetIndex);
            });
        });

        qamariyaCards.forEach((card) => {
            card.addEventListener('click', (event) => {
                if (qamariyaDidSwipe) {
                    event.preventDefault();
                }
            });
        });

        qamariyaStage?.addEventListener('pointerdown', (event) => {
            if (event.pointerType === 'mouse' && event.button !== 0) return;
            qamariyaPointerActive = true;
            qamariyaDidSwipe = false;
            qamariyaPointerStartX = event.clientX;
            qamariyaPointerStartY = event.clientY;
            stopQamariyaAuto();
        }, { passive: true });

        qamariyaStage?.addEventListener('pointermove', (event) => {
            if (!qamariyaPointerActive) return;
            const deltaX = event.clientX - qamariyaPointerStartX;
            const deltaY = event.clientY - qamariyaPointerStartY;
            if (Math.abs(deltaX) > 12 && Math.abs(deltaX) > Math.abs(deltaY)) {
                qamariyaDidSwipe = true;
            }
        }, { passive: true });

        const finishQamariyaPointer = (event) => {
            if (!qamariyaPointerActive) return;

            const deltaX = event.clientX - qamariyaPointerStartX;
            const deltaY = event.clientY - qamariyaPointerStartY;
            qamariyaPointerActive = false;

            if (Math.abs(deltaX) >= 44 && Math.abs(deltaX) > Math.abs(deltaY)) {
                // اتجاه اللمس الطبيعي: إذا سحب المستخدم البطاقات يميناً تتحرك يميناً،
                // وإذا سحبها يساراً تتحرك يساراً. في تصميم RTL الحالي كانت الإشارة معكوسة.
                chooseQamariya(deltaX > 0 ? qamariyaIndex + 1 : qamariyaIndex - 1);
            } else {
                startQamariyaAuto();
            }

            window.setTimeout(() => {
                qamariyaDidSwipe = false;
            }, 80);
        };

        qamariyaStage?.addEventListener('pointerup', finishQamariyaPointer, { passive: true });
        qamariyaStage?.addEventListener('pointercancel', () => {
            // عند إلغاء اللمس بسبب تمرير الصفحة لا ننفذ انتقالاً وهمياً للجهة المعاكسة.
            if (!qamariyaPointerActive) return;
            qamariyaPointerActive = false;
            startQamariyaAuto();
            window.setTimeout(() => {
                qamariyaDidSwipe = false;
            }, 80);
        }, { passive: true });

        qamariyaCarousel.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowLeft') {
                event.preventDefault();
                chooseQamariya(qamariyaIndex + 1);
            } else if (event.key === 'ArrowRight') {
                event.preventDefault();
                chooseQamariya(qamariyaIndex - 1);
            }
        });

        document.addEventListener('visibilitychange', () => {
            if (document.hidden) stopQamariyaAuto();
            else startQamariyaAuto();
        });

        window.addEventListener('resize', () => {
            window.cancelAnimationFrame(qamariyaResizeFrame);
            qamariyaResizeFrame = window.requestAnimationFrame(fitQamariyaNames);
        }, { passive: true });

        document.fonts?.ready?.then(fitQamariyaNames);

        const handleQamariyaMotionPreference = () => {
            renderQamariya(qamariyaIndex);
            startQamariyaAuto();
        };

        if (typeof qamariyaReducedMotion.addEventListener === 'function') {
            qamariyaReducedMotion.addEventListener('change', handleQamariyaMotionPreference);
        } else {
            qamariyaReducedMotion.addListener(handleQamariyaMotionPreference);
        }

        if ('IntersectionObserver' in window) {
            const qamariyaObserver = new IntersectionObserver((entries) => {
                qamariyaVisible = entries.some((entry) => entry.isIntersecting);
                if (qamariyaVisible) startQamariyaAuto();
                else stopQamariyaAuto();
            }, { threshold: .12 });

            qamariyaObserver.observe(qamariyaCarousel);
        }

        if (qamariyaCards.length <= 1) {
            qamariyaCarousel.classList.add('is-single');
        }

        renderQamariya(0);
        window.requestAnimationFrame(fitQamariyaNames);
        startQamariyaAuto();
    }

    // ============================================================================
    // 🗺️ 9. محرك الخريطة الملكي وتحديد الموقع (VIP Map & GPS) - مع النبض السحري
    // ============================================================================

    const refreshNearbyResults = () => {
        if (!nearbyLastUserLocation || !map) return;

        loadNearbyPlaces(
            nearbyLastUserLocation,
            { silent: true, scroll: false }
        ).catch(error => {
            console.error('Nearby filter request failed:', error);
            Swal.fire({
                icon: 'error',
                title: 'تعذر تحديث النتائج',
                text: error.message || 'حاول مرة أخرى.',
                confirmButtonColor: '#05203C'
            });
        });
    };

    const setNearbyFilterChipState = (button, active) => {
        if (!button) return;
        button.classList.toggle('is-active', active);
        button.setAttribute('aria-pressed', active ? 'true' : 'false');
    };

    const nearbyRadiusInput = document.getElementById('nearbyRadius');
    const nearbyRadiusValue = document.getElementById('nearbyRadiusValue');

    const syncNearbyRadiusLabel = () => {
        if (!nearbyRadiusInput || !nearbyRadiusValue) return;

        const value =
            Number.parseInt(nearbyRadiusInput.value || '15', 10) || 15;

        nearbyRadiusValue.textContent = `${value} كم`;

        const progress =
            Math.max(0, Math.min(100, ((value - 1) / 99) * 100));

        nearbyRadiusInput.style.setProperty(
            '--nearby-range-progress',
            `${progress}%`
        );
    };

    syncNearbyRadiusLabel();

    let nearbyRadiusDebounce = 0;

    nearbyRadiusInput?.addEventListener('input', () => {
        syncNearbyRadiusLabel();

        window.clearTimeout(nearbyRadiusDebounce);

        nearbyRadiusDebounce =
            window.setTimeout(
                refreshNearbyResults,
                180
            );
    });

    const nearbyOffersToggle =
        document.getElementById('nearbyOffersToggle');

    const nearbyBookableToggle =
        document.getElementById('nearbyBookableToggle');

    [nearbyOffersToggle, nearbyBookableToggle].forEach(button => {
        button?.addEventListener('click', () => {
            const nextState =
                button.getAttribute('aria-pressed') !== 'true';

            setNearbyFilterChipState(button, nextState);
            refreshNearbyResults();
        });
    });

    const nearbyGpsBtn = document.getElementById('nearbyGpsBtn');

    document.querySelectorAll('[data-map-mode]').forEach(button => {
        button.addEventListener('click', function () {
            const requestedMode = this.dataset.mapMode;
            if (!['roadmap', 'satellite', 'terrain'].includes(requestedMode)) return;

            nearbyMapType = requestedMode;
            localStorage.setItem('nearbyMapType', nearbyMapType);
            syncNearbyMapModeButtons(requestedMode);
            applyNearbyMapAppearance();
        });
    });
    syncNearbyMapModeButtons(nearbyMapType);

    const labelsToggle = document.getElementById('nearbyLabelsToggle');
    const placesToggle = document.getElementById('nearbyPlacesToggle');
    const trafficToggle = document.getElementById('nearbyTrafficToggle');
    setNearbyToggleState(labelsToggle, nearbyLabelsVisible);
    setNearbyToggleState(placesToggle, nearbyPlacesVisible);
    setNearbyToggleState(trafficToggle, nearbyTrafficVisible);

    labelsToggle?.addEventListener('click', () => {
        nearbyLabelsVisible = !nearbyLabelsVisible;
        localStorage.setItem('nearbyLabelsVisible', String(nearbyLabelsVisible));
        setNearbyToggleState(labelsToggle, nearbyLabelsVisible);
        applyNearbyMapAppearance();
    });

    placesToggle?.addEventListener('click', () => {
        nearbyPlacesVisible = !nearbyPlacesVisible;
        localStorage.setItem('nearbyPlacesVisible', String(nearbyPlacesVisible));
        setNearbyToggleState(placesToggle, nearbyPlacesVisible);
        applyNearbyMapAppearance();
    });

    trafficToggle?.addEventListener('click', () => {
        nearbyTrafficVisible = !nearbyTrafficVisible;
        localStorage.setItem('nearbyTrafficVisible', String(nearbyTrafficVisible));
        setNearbyToggleState(trafficToggle, nearbyTrafficVisible);
        applyNearbyMapAppearance();
    });

    ['nearbyCategory', 'nearbySort'].forEach(controlId => {
        document.getElementById(controlId)?.addEventListener(
            'change',
            refreshNearbyResults
        );
    });

    if (nearbyGpsBtn) {
        nearbyGpsBtn.addEventListener('click', function () {
            const nearbyStatus = document.getElementById('nearbyV10Status');
            const originalStatus = nearbyStatus?.textContent || '';
            this.classList.add('is-locating');
            this.setAttribute('aria-busy', 'true');
            if (nearbyStatus) nearbyStatus.textContent = 'جارٍ تحديد موقعك وتجهيز النتائج...';
            this.disabled = true;

            if (navigator.geolocation) {
                navigator.geolocation.getCurrentPosition(
                    function (position) {
                        nearbyGpsBtn.classList.remove('is-locating');
                        nearbyGpsBtn.removeAttribute('aria-busy');
                        nearbyGpsBtn.disabled = false;

                        const lat = position.coords.latitude;
                        const lng = position.coords.longitude;
                        const userLocation = { lat: lat, lng: lng };
                        nearbyLastUserLocation = userLocation;

                        const mapDiv = document.getElementById('map');
                        if (mapDiv) {
                            if (nearbyStatus) nearbyStatus.textContent = 'تم تحديد موقعك — جارٍ تشغيل الخريطة...';

                            waitForGoogleMaps().then(() => {
                                document.getElementById('nearbyMapShell')?.classList.add('is-ready');
                                if (nearbyStatus) nearbyStatus.textContent = 'تم تحديد موقعك — نرتب الأقرب الآن';
                                const userMapIcon = {
                                    path: google.maps.SymbolPath.CIRCLE,
                                    scale: 10,
                                    fillColor: "#00A6DA",
                                    fillOpacity: 1,
                                    strokeWeight: 4,
                                    strokeColor: "#ffffff"
                                };

                                if (!map) {
                                    map = new google.maps.Map(mapDiv, {
                                        center: userLocation,
                                        zoom: 16,
                                        tilt: getNearbyEffectiveMapType() === 'hybrid' || getNearbyEffectiveMapType() === 'satellite' ? 45 : 0,
                                        mapTypeId: getNearbyEffectiveMapType(),
                                        mapTypeControl: false,
                                        zoomControl: true,
                                        zoomControlOptions: {
                                            position: google.maps.ControlPosition.LEFT_BOTTOM
                                        },
                                        streetViewControl: true,
                                        streetViewControlOptions: {
                                            position: google.maps.ControlPosition.LEFT_BOTTOM
                                        },
                                        fullscreenControl: true,
                                        fullscreenControlOptions: {
                                            position: google.maps.ControlPosition.LEFT_TOP
                                        },
                                        rotateControl: true,
                                        scaleControl: true,
                                        gestureHandling: 'cooperative',
                                        styles: nearbyLabelsVisible ? null : nearbyHiddenLabelsStyle
                                    });
                                }

                                google.maps.event.trigger(map, 'resize');
                                map.setCenter(userLocation);
                                applyNearbyMapAppearance();

                                if (userMarker) userMarker.setMap(null);
                                userMarker = new google.maps.Marker({
                                    position: userLocation,
                                    map: map,
                                    title: "موقعك الحالي",
                                    icon: userMapIcon
                                });

                                // ✨ إطلاق موجات الرادار من موقع المستخدم (نبض إشعاعي) ✨
                                if (typeof window.radarPulseCleanup === 'function') window.radarPulseCleanup();
                                if (window.radarPulseInterval) clearInterval(window.radarPulseInterval);
                                if (window.pulseCircle) window.pulseCircle.setMap(null);

                                window.pulseCircle = new google.maps.Circle({
                                    strokeColor: "#00A6DA",
                                    strokeOpacity: 0.8,
                                    strokeWeight: 2,
                                    fillColor: "#00A6DA",
                                    fillOpacity: 0.4,
                                    map: map,
                                    center: userLocation,
                                    radius: 0
                                });

                                let pulseRadius = 0;
                                let pulseFrame = 0;
                                let pulseLastFrameAt = 0;
                                let pulseVisible = true;
                                const pulseTarget = document.getElementById('nearbyMapShell') || mapDiv;

                                const stopRadarPulse = () => {
                                    if (pulseFrame) {
                                        window.cancelAnimationFrame(pulseFrame);
                                        pulseFrame = 0;
                                    }
                                };

                                const animateRadarPulse = timestamp => {
                                    pulseFrame = 0;
                                    if (document.hidden || !pulseVisible || !window.pulseCircle) return;

                                    // نفس إيقاع 35ms القديم تقريباً، لكن متزامن مع الرسم ولا يعمل خارج الشاشة.
                                    if (timestamp - pulseLastFrameAt >= 35) {
                                        pulseLastFrameAt = timestamp;
                                        pulseRadius += 2;
                                        if (pulseRadius > 100) pulseRadius = 0;

                                        window.pulseCircle.setRadius(pulseRadius);
                                        window.pulseCircle.setOptions({
                                            strokeOpacity: Math.max(0, 0.8 - (pulseRadius / 100)),
                                            fillOpacity: Math.max(0, 0.4 - (pulseRadius / 250))
                                        });
                                    }

                                    pulseFrame = window.requestAnimationFrame(animateRadarPulse);
                                };

                                const startRadarPulse = () => {
                                    if (pulseFrame || document.hidden || !pulseVisible) return;
                                    pulseFrame = window.requestAnimationFrame(animateRadarPulse);
                                };

                                let pulseObserver = null;
                                if (pulseTarget && 'IntersectionObserver' in window) {
                                    pulseObserver = new IntersectionObserver(entries => {
                                        pulseVisible = entries.some(entry => entry.isIntersecting);
                                        if (pulseVisible) startRadarPulse();
                                        else stopRadarPulse();
                                    }, { threshold: 0.02 });
                                    pulseObserver.observe(pulseTarget);
                                }

                                const pulseVisibilityHandler = () => {
                                    if (document.hidden) stopRadarPulse();
                                    else startRadarPulse();
                                };
                                document.addEventListener('visibilitychange', pulseVisibilityHandler);

                                window.radarPulseCleanup = () => {
                                    stopRadarPulse();
                                    pulseObserver?.disconnect();
                                    document.removeEventListener('visibilitychange', pulseVisibilityHandler);
                                };

                                // توافق مع أي كود قديم كان يفحص هذا المتغير.
                                window.radarPulseInterval = null;
                                startRadarPulse();

                                loadNearbyPlaces(userLocation, { silent: false, scroll: true })
                                    .catch(error => {
                                        console.error('Nearby places request failed:', error);
                                        if (nearbyStatus) nearbyStatus.textContent = 'الخريطة تعمل، لكن تعذر جلب بيانات المنشآت';
                                        Swal.fire({
                                            icon: 'error',
                                            title: 'تعذر تحميل المنشآت',
                                            text: error?.message && !error.message.startsWith('Nearby')
                                                ? error.message
                                                : 'الخريطة تعمل، لكن الخادم لم يُرجع بيانات المنشآت بصورة صحيحة.',
                                            confirmButtonColor: '#05203C'
                                        });
                                    });

                            }).catch(() => {
                                document.getElementById('nearbyMapShell')?.classList.remove('is-ready');
                                if (nearbyStatus) nearbyStatus.textContent = 'تعذّر تشغيل الخريطة — تحقق من مفتاح Google Maps';
                                Swal.fire({
                                    icon: 'error',
                                    title: 'الخريطة غير متاحة',
                                    text: 'تعذّر تحميل خرائط Google. تأكد من مفتاح GoogleMaps:ApiKey واتصال الإنترنت ثم حاول مرة أخرى.',
                                    confirmButtonColor: '#05203C'
                                });
                            });
                        }
                    },
                    function (error) {
                        nearbyGpsBtn.classList.remove('is-locating');
                        nearbyGpsBtn.removeAttribute('aria-busy');
                        nearbyGpsBtn.disabled = false;
                        if (nearbyStatus) nearbyStatus.textContent = originalStatus;
                        let errorMsg = "تعذر تحديد موقعك الفعلي.";
                        if (error.code === 1) errorMsg = "تم رفض صلاحية الموقع.";
                        if (error.code === 2) errorMsg = "تأكد من تفعيل خدمة الـ GPS في جهازك.";
                        if (error.code === 3) errorMsg = "انتهى وقت الطلب لضعف الشبكة.";
                        Swal.fire({ icon: 'error', title: 'خطأ', text: errorMsg, confirmButtonColor: '#05203C' });
                    },
                    { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 }
                );
            } else {
                nearbyGpsBtn.classList.remove('is-locating');
                nearbyGpsBtn.removeAttribute('aria-busy');
                nearbyGpsBtn.disabled = false;
                if (nearbyStatus) nearbyStatus.textContent = originalStatus;
                Swal.fire({ icon: 'error', title: 'غير مدعوم', text: 'متصفحك لا يدعم تحديد الموقع.', confirmButtonColor: '#05203C' });
            }
        });
    }

    // ============================================================================
    // 📊 10. عدادات الإحصائيات (Intersection Observer)
    // ============================================================================
    const statNumbers = document.querySelectorAll('.stat-number');
    let hasCounted = false;

    function startCounters() {
        statNumbers.forEach(numberElement => {
            const target = parseInt(numberElement.getAttribute('data-target'));
            if (isNaN(target)) return;

            const duration = 2000;
            const increment = target / (duration / 30);
            let current = 0;

            const timer = setInterval(() => {
                current += increment;
                if (current >= target) {
                    current = target;
                    clearInterval(timer);
                    if (target > 100) {
                        numberElement.innerText = "+" + Math.floor(current).toLocaleString();
                    } else {
                        numberElement.innerText = Math.floor(current);
                    }
                } else {
                    numberElement.innerText = Math.floor(current).toLocaleString();
                }
            }, 30);
        });
    }

    const statsSection = document.getElementById('stats-section');
    if (statsSection && statNumbers.length > 0) {
        const observer = new IntersectionObserver((entries) => {
            if (entries[0].isIntersecting && !hasCounted) {
                hasCounted = true;
                startCounters();
            }
        }, { threshold: 0.5 });
        observer.observe(statsSection);
    }

    // ============================================================================
    // ✨ 10.1 ظهور مسار الرحلة وختم الثقة عند الوصول إليهما
    // ============================================================================
    const revealExperienceStage = stage => {
        if (!stage) return;
        if (!('IntersectionObserver' in window)) {
            stage.classList.add('is-visible');
            return;
        }

        const stageObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.01, rootMargin: '900px 0px 900px 0px' });

        stageObserver.observe(stage);
    };

    revealExperienceStage(document.querySelector('[data-journey-stage]'));
    revealExperienceStage(document.querySelector('[data-trust-stage]'));

    // ============================================================================
    // 📸 11. معرض صور الأماكن المميزة — يعمل فقط مع البطاقة المتفاعَل معها
    // Desktop: عند الوقوف بالمؤشر على البطاقة فقط.
    // Mobile: عند استقرار البطاقة بوضوح داخل الشاشة فقط، ويتوقف فور مغادرتها.
    // ============================================================================
    function initFeaturedSmartGallery() {
        const cards = Array.from(document.querySelectorAll('.featured-wow-card'));
        if (!cards.length) return;

        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)').matches;

        cards.forEach((card, cardIndex) => {
            if (card.dataset.smartGalleryInitialized === 'true') return;
            card.dataset.smartGalleryInitialized = 'true';

            const gallery = card.querySelector('.featured-wow-gallery');
            if (!gallery) return;

            let imagesList = [];
            try {
                imagesList = JSON.parse(gallery.getAttribute('data-images') || '[]') || [];
            } catch {
                imagesList = [];
            }

            imagesList = imagesList.filter(Boolean);

            const imageContainers = Array.from(gallery.querySelectorAll('.featured-wow-thumb'));
            imageContainers.forEach(container => {
                container.setAttribute('aria-hidden', 'true');
                container.querySelectorAll('img').forEach(image => {
                    image.draggable = false;
                    image.setAttribute('draggable', 'false');
                });
            });

            if (reduceMotion || imagesList.length <= 3 || imageContainers.length === 0) return;

            let intervalId = null;
            let startTimeoutId = null;
            let currentImageIndex = 3;
            let currentBoxIndex = 0;
            let isEngaged = false;

            const intervalDelay = finePointer ? 3600 : 5000;
            const fadeDuration = finePointer ? 700 : 820;
            const initialDelay = finePointer ? 650 : 900 + ((cardIndex % 3) * 220);

            const crossFadeImage = (container, image, newSource) => {
                const clone = image.cloneNode(true);
                clone.removeAttribute('data-original');
                clone.style.position = 'absolute';
                clone.style.inset = '0';
                clone.style.width = '100%';
                clone.style.height = '100%';
                clone.style.objectFit = 'cover';
                clone.style.zIndex = '3';
                clone.style.opacity = '1';
                clone.style.transition = `opacity ${fadeDuration}ms ease`;
                clone.style.pointerEvents = 'none';
                clone.draggable = false;

                container.appendChild(clone);
                image.setAttribute('src', newSource);

                requestAnimationFrame(() => {
                    requestAnimationFrame(() => {
                        clone.style.opacity = '0';
                    });
                });

                window.setTimeout(() => clone.remove(), fadeDuration + 80);
            };

            const changeNextImage = () => {
                if (!isEngaged || document.hidden || !imagesList.length) return;

                const container = imageContainers[currentBoxIndex];
                const currentImage = container?.querySelector('img.magic-image');

                if (!currentImage) {
                    currentBoxIndex = (currentBoxIndex + 1) % imageContainers.length;
                    return;
                }

                let attempts = 0;
                let nextSource = imagesList[currentImageIndex % imagesList.length];

                while (
                    nextSource &&
                    currentImage.getAttribute('src') === nextSource &&
                    attempts < imagesList.length
                ) {
                    currentImageIndex = (currentImageIndex + 1) % imagesList.length;
                    nextSource = imagesList[currentImageIndex];
                    attempts++;
                }

                if (!nextSource) return;

                container.classList.add('is-gallery-changing');
                crossFadeImage(container, currentImage, nextSource);

                window.setTimeout(() => {
                    container.classList.remove('is-gallery-changing');
                }, fadeDuration + 100);

                currentImageIndex = (currentImageIndex + 1) % imagesList.length;
                currentBoxIndex = (currentBoxIndex + 1) % imageContainers.length;
            };

            const stopRotation = () => {
                isEngaged = false;

                if (startTimeoutId) {
                    window.clearTimeout(startTimeoutId);
                    startTimeoutId = null;
                }

                if (intervalId) {
                    window.clearInterval(intervalId);
                    intervalId = null;
                }
            };

            const startRotation = () => {
                if (document.hidden || reduceMotion || isEngaged) return;
                isEngaged = true;

                startTimeoutId = window.setTimeout(() => {
                    if (!isEngaged || document.hidden) return;
                    changeNextImage();
                    intervalId = window.setInterval(changeNextImage, intervalDelay);
                }, initialDelay);
            };

            if (finePointer) {
                card.addEventListener('pointerenter', startRotation, { passive: true });
                card.addEventListener('pointerleave', stopRotation, { passive: true });
                card.addEventListener('blur', stopRotation, true);
            } else if ('IntersectionObserver' in window) {
                const observer = new IntersectionObserver(entries => {
                    entries.forEach(entry => {
                        const active = entry.isIntersecting && entry.intersectionRatio >= 0.62;
                        if (active) startRotation();
                        else stopRotation();
                    });
                }, {
                    threshold: [0, 0.35, 0.62, 0.85],
                    rootMargin: '-10% 0px -10% 0px'
                });

                observer.observe(card);
            }

            document.addEventListener('visibilitychange', () => {
                if (document.hidden) {
                    stopRotation();
                }
            });
        });
    }

    initFeaturedSmartGallery();


    // ============================================================================
    // 🎁 12. بطاقات العروض 3D — الميل، الإضاءة، البارالاكس، والدخول التدريجي
    // ============================================================================
    function initHomePromotions3D() {
        const stages = Array.from(document.querySelectorAll('.home-promo-stage'));
        const cards = Array.from(document.querySelectorAll('[data-promo-3d-card]'));

        if (!stages.length || !cards.length) return;

        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)').matches;

        // ظهور البطاقات تدريجياً عند دخول القسم إلى الشاشة.
        if (!reduceMotion && 'IntersectionObserver' in window) {
            const revealObserver = new IntersectionObserver((entries, observer) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('is-visible');
                    observer.unobserve(entry.target);
                });
            }, {
                threshold: 0.01,
                rootMargin: '900px 0px 900px 0px'
            });

            stages.forEach(stage => revealObserver.observe(stage));
        } else {
            stages.forEach(stage => stage.classList.add('is-visible'));
        }

        if (!finePointer || reduceMotion) return;

        cards.forEach(card => {
            const image = card.querySelector('.home-promo-image-shell img');
            let frameId = 0;
            let pointerRect = null;

            const refreshPointerRect = () => {
                pointerRect = card.getBoundingClientRect();
            };

            const resetCard = () => {
                pointerRect = null;
                if (frameId) cancelAnimationFrame(frameId);
                card.style.setProperty('--promo-rotate-x', '0deg');
                card.style.setProperty('--promo-rotate-y', '0deg');
                card.style.setProperty('--promo-mx', '50%');
                card.style.setProperty('--promo-my', '45%');
                if (image) image.style.transform = 'scale(1.035) translate3d(0, 0, 0)';
            };

            card.addEventListener('pointerenter', refreshPointerRect, { passive: true });
            card.addEventListener('pointermove', event => {
                const rect = pointerRect || card.getBoundingClientRect();
                const px = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
                const py = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));

                // ميل صغير ومريح حتى يبقى النص واضحاً.
                const rotateY = (px - 0.5) * 9;
                const rotateX = (0.5 - py) * 7;

                if (frameId) cancelAnimationFrame(frameId);
                frameId = requestAnimationFrame(() => {
                    card.style.setProperty('--promo-rotate-x', `${rotateX.toFixed(2)}deg`);
                    card.style.setProperty('--promo-rotate-y', `${rotateY.toFixed(2)}deg`);
                    card.style.setProperty('--promo-mx', `${(px * 100).toFixed(1)}%`);
                    card.style.setProperty('--promo-my', `${(py * 100).toFixed(1)}%`);

                    if (image) {
                        const imageX = (0.5 - px) * 8;
                        const imageY = (0.5 - py) * 6;
                        image.style.transform = `scale(1.07) translate3d(${imageX.toFixed(1)}px, ${imageY.toFixed(1)}px, 0)`;
                    }
                });
            });

            card.addEventListener('pointerleave', resetCard);
            card.addEventListener('blur', resetCard, true);
        });
    }

    initHomePromotions3D();

    // ============================================================================
    // 💎 13. الأماكن المميزة — Luxury Motion 3D
    // ============================================================================
    function initFeaturedPlaces3D() {
        const stages = Array.from(document.querySelectorAll('.featured-wow-stage'));
        const cards = Array.from(document.querySelectorAll('[data-featured-3d-card]'));
        if (!stages.length || !cards.length) return;

        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)').matches;

        // دخول واحد وناعم عند ظهور القسم. لا يتكرر مع كل لمس أو تمرير.
        if (!reduceMotion && 'IntersectionObserver' in window) {
            const observer = new IntersectionObserver((entries, currentObserver) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('is-visible');
                    currentObserver.unobserve(entry.target);
                });
            }, { threshold: 0.01, rootMargin: '900px 0px 900px 0px' });

            stages.forEach(stage => observer.observe(stage));
        } else {
            stages.forEach(stage => stage.classList.add('is-visible'));
        }

        // اللمس: لا Tilt ولا Parallax ولا حركة بسبب الضغط.
        if (!finePointer || reduceMotion) return;

        cards.forEach(card => {
            const mainImage = card.querySelector('.featured-main-photo');
            let frameId = 0;
            let pointerRect = null;

            const refreshPointerRect = () => {
                pointerRect = card.getBoundingClientRect();
            };

            const reset = () => {
                pointerRect = null;
                if (frameId) cancelAnimationFrame(frameId);
                card.style.setProperty('--featured-rx', '0deg');
                card.style.setProperty('--featured-ry', '0deg');
                card.style.setProperty('--featured-mx', '50%');
                card.style.setProperty('--featured-my', '35%');

                if (mainImage) {
                    mainImage.style.transform = 'scale(1.025) translate3d(0,0,0)';
                }
            };

            card.addEventListener('pointerenter', refreshPointerRect, { passive: true });
            card.addEventListener('pointermove', event => {
                if (event.pointerType && event.pointerType !== 'mouse') return;

                const rect = pointerRect || card.getBoundingClientRect();
                const px = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
                const py = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));

                // الميل مقصود أن يكون واضحاً وفخماً، لكن ليس عنيفاً.
                const rotateY = (px - 0.5) * 8.5;
                const rotateX = (0.5 - py) * 6.2;

                if (frameId) cancelAnimationFrame(frameId);
                frameId = requestAnimationFrame(() => {
                    card.style.setProperty('--featured-rx', `${rotateX.toFixed(2)}deg`);
                    card.style.setProperty('--featured-ry', `${rotateY.toFixed(2)}deg`);
                    card.style.setProperty('--featured-mx', `${(px * 100).toFixed(1)}%`);
                    card.style.setProperty('--featured-my', `${(py * 100).toFixed(1)}%`);

                    if (mainImage) {
                        const x = (0.5 - px) * 7.5;
                        const y = (0.5 - py) * 5;
                        mainImage.style.transform = `scale(1.065) translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, 0)`;
                    }
                });
            });

            card.addEventListener('pointerleave', reset);
            card.addEventListener('pointercancel', reset);
        });
    }

    initFeaturedPlaces3D();

    // ============================================================================
    // ✨ 14. تحسينات الواجهة السينمائية V5/V6
    // نص ديناميكي آمن + عمق 3D + حركة هاتف خفيفة + وصول بلوحة المفاتيح
    // ============================================================================
    function initHeroCinematicV5() {
        if (!heroV4 || (!heroV4.classList.contains('hero-cinematic-v5') && !heroV4.classList.contains('dlx-hero'))) return;

        let fitFrame = 0;
        let mobileFrame = 0;
        let resizeFitTimer = 0;

        const fitWithinLines = (element, maxSize, minSize, maxLines) => {
            if (!element) return false;

            let size = maxSize;
            element.style.fontSize = `${size}px`;

            const overflows = () => {
                const styles = window.getComputedStyle(element);
                const lineHeight = parseFloat(styles.lineHeight) || size * 1.1;
                const paddingBlock = (parseFloat(styles.paddingTop) || 0) +
                    (parseFloat(styles.paddingBottom) || 0);
                const maximumHeight = (lineHeight * maxLines) + paddingBlock + 3;
                return element.scrollHeight > maximumHeight ||
                    element.scrollWidth > element.clientWidth + 1;
            };

            let guard = 0;
            while (overflows() && size > minSize && guard < 120) {
                size -= 0.5;
                element.style.fontSize = `${size}px`;
                guard++;
            }

            return overflows();
        };

        const fitDynamicHeroText = () => {
            fitFrame = 0;
            const width = window.innerWidth;
            let mainOverflow = false;
            let dynamicOverflow = false;

            if (width <= 390) {
                mainOverflow = fitWithinLines(mainRowV4, 28, 14, 1);
                dynamicOverflow = fitWithinLines(dynamicRowV4, 30, 14, 1);
            } else if (width <= 767) {
                mainOverflow = fitWithinLines(mainRowV4, 32, 15, 1);
                dynamicOverflow = fitWithinLines(dynamicRowV4, 34, 15, 1);
            } else if (width <= 1199) {
                mainOverflow = fitWithinLines(mainRowV4, 60, 30, 2);
                dynamicOverflow = fitWithinLines(dynamicRowV4, 60, 28, 2);
            } else {
                mainOverflow = fitWithinLines(mainRowV4, 75, 38, 2);
                dynamicOverflow = fitWithinLines(dynamicRowV4, 75, 35, 2);
            }

            heroV4.classList.toggle('is-v5-extra-long', mainOverflow || dynamicOverflow);
        };

        const scheduleTextFit = () => {
            if (fitFrame) cancelAnimationFrame(fitFrame);
            fitFrame = requestAnimationFrame(fitDynamicHeroText);
        };

        if (typedV4) {
            const textObserver = new MutationObserver(scheduleTextFit);
            textObserver.observe(typedV4, { childList: true, characterData: true, subtree: true });
        }

        heroV4.querySelectorAll('.dropdown-header[role="button"]').forEach(header => {
            header.addEventListener('keydown', event => {
                if (event.key !== 'Enter' && event.key !== ' ') return;
                event.preventDefault();
                header.click();
            });
        });

        if (finePointerV4.matches && !reduceV4.matches) {
            let heroPointerRect = null;
            const refreshHeroPointerRect = () => {
                heroPointerRect = heroV4.getBoundingClientRect();
            };

            heroV4.addEventListener('pointerenter', refreshHeroPointerRect, { passive: true });
            heroV4.addEventListener('pointermove', event => {
                const rect = heroPointerRect || heroV4.getBoundingClientRect();
                if (!rect.width || !rect.height) return;

                const x = Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width));
                const y = Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height));
                const cx = x - 0.5;
                const cy = y - 0.5;

                heroV4.style.setProperty('--v5-pointer-x', `${(x * 100).toFixed(1)}%`);
                heroV4.style.setProperty('--v5-pointer-y', `${(y * 100).toFixed(1)}%`);
                heroV4.style.setProperty('--v5-tilt-x', `${(cy * -1.8).toFixed(2)}deg`);
                heroV4.style.setProperty('--v5-tilt-y', `${(cx * 2.2).toFixed(2)}deg`);
                heroV4.style.setProperty('--v5-depth-x', `${(cx * -7).toFixed(2)}px`);
                heroV4.style.setProperty('--v5-depth-y', `${(cy * -5).toFixed(2)}px`);
                heroV4.style.setProperty('--dlx-mx', `${(x * 100).toFixed(1)}%`);
                heroV4.style.setProperty('--dlx-my', `${(y * 100).toFixed(1)}%`);
                heroV4.style.setProperty('--dlx-depth-x', `${(cx * -7).toFixed(2)}px`);
                heroV4.style.setProperty('--dlx-depth-y', `${(cy * -5).toFixed(2)}px`);
            }, { passive: true });

            heroV4.addEventListener('pointerleave', () => {
                heroPointerRect = null;
                heroV4.style.setProperty('--v5-pointer-x', '50%');
                heroV4.style.setProperty('--v5-pointer-y', '42%');
                heroV4.style.setProperty('--v5-tilt-x', '0deg');
                heroV4.style.setProperty('--v5-tilt-y', '0deg');
                heroV4.style.setProperty('--v5-depth-x', '0px');
                heroV4.style.setProperty('--v5-depth-y', '0px');
                heroV4.style.setProperty('--dlx-mx', '50%');
                heroV4.style.setProperty('--dlx-my', '42%');
                heroV4.style.setProperty('--dlx-depth-x', '0px');
                heroV4.style.setProperty('--dlx-depth-y', '0px');
            });
        } else if (!reduceV4.matches) {
            const updateMobileDepth = () => {
                mobileFrame = 0;
                const rect = heroV4.getBoundingClientRect();
                if (rect.bottom < 0 || rect.top > window.innerHeight) return;
                const travel = Math.max(-12, Math.min(12, rect.top * -0.025));
                heroV4.style.setProperty('--v5-depth-y', `${travel.toFixed(2)}px`);
                heroV4.style.setProperty('--dlx-depth-y', `${travel.toFixed(2)}px`);
            };

            window.addEventListener('scroll', () => {
                if (mobileFrame) return;
                mobileFrame = requestAnimationFrame(updateMobileDepth);
            }, { passive: true });
            updateMobileDepth();
        }

        window.addEventListener('resize', () => {
            window.clearTimeout(resizeFitTimer);
            resizeFitTimer = window.setTimeout(scheduleTextFit, 180);
        }, { passive: true });
        scheduleTextFit();

        if (document.fonts?.ready) {
            document.fonts.ready.then(scheduleTextFit);
        }
    }

    initHeroCinematicV5();

    // ============================================================================
    // 🧠 PREWARM + SCROLL RENDER STABILIZER
    // يجهّز صور القسم قبل ظهوره، ويوقف الحركات الزخرفية الثقيلة فقط أثناء
    // حركة الـscroll نفسها ثم يعيدها فور التوقف. لا يحذف أي حركة أو تصميم.
    // ============================================================================
    function initHomeRenderStabilizer() {
        const prewarmTargets = [
            document.querySelector('.qamariya-categories'),
            document.querySelector('.featured-wow-section'),
            document.querySelector('.home-promotions-section'),
            document.querySelector('.nearby-v10'),
            document.querySelector('.journey-v10'),
            document.querySelector('.trust-v10')
        ].filter(Boolean);

        const prewarmSection = section => {
            if (!section || section.dataset.ygPrewarmed === 'true') return;
            section.dataset.ygPrewarmed = 'true';
            section.classList.add('yg-prewarmed');

            const images = Array.from(section.querySelectorAll('img'));
            images.forEach(img => {
                try {
                    img.decoding = 'async';
                    if (img.loading === 'lazy') img.loading = 'eager';
                    if ('fetchPriority' in img && img.fetchPriority !== 'high') {
                        img.fetchPriority = 'low';
                    }

                    const decodeImage = () => {
                        if (typeof img.decode !== 'function') return;
                        img.decode().catch(() => { });
                    };

                    if (img.complete) decodeImage();
                    else img.addEventListener('load', decodeImage, { once: true, passive: true });
                } catch (_) {
                    // فشل decode لا يؤثر على العرض الطبيعي للصورة.
                }
            });
        };

        if ('IntersectionObserver' in window) {
            const prewarmObserver = new IntersectionObserver(entries => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    prewarmSection(entry.target);
                    prewarmObserver.unobserve(entry.target);
                });
            }, {
                threshold: 0.01,
                rootMargin: '1400px 0px 1400px 0px'
            });

            prewarmTargets.forEach(section => prewarmObserver.observe(section));
        } else {
            prewarmTargets.forEach(prewarmSection);
        }

        let scrollEndTimer = 0;
        let scrollClassApplied = false;

        const onWindowScroll = () => {
            if (!scrollClassApplied) {
                scrollClassApplied = true;
                document.body.classList.add('yg-home-scrolling');
            }

            window.clearTimeout(scrollEndTimer);
            scrollEndTimer = window.setTimeout(() => {
                document.body.classList.remove('yg-home-scrolling');
                scrollClassApplied = false;
            }, 120);
        };

        window.addEventListener('scroll', onWindowScroll, { passive: true });
        window.addEventListener('pagehide', () => {
            window.clearTimeout(scrollEndTimer);
            document.body.classList.remove('yg-home-scrolling');
        }, { once: true });
    }

    initHomeRenderStabilizer();

    // ============================================================================
    // ⚡ PERFORMANCE GOVERNOR
    // لا يحذف أي حركة مرئية: يوقف الحركات/الفيديو فقط عندما تكون خارج الشاشة،
    // ويعيدها قبل دخول القسم إلى مجال الرؤية. كما يجهّز Google Maps قرب القسم فقط.
    // ============================================================================
    function initHomePerformanceGovernor() {
        const motionTargets = [
            document.getElementById('homeHeroV4'),
            document.querySelector('.qamariya-categories'),
            document.querySelector('.featured-wow-section'),
            document.querySelector('.home-promotions-section'),
            document.querySelector('.nearby-v10'),
            document.querySelector('.journey-v10'),
            document.querySelector('.trust-v10')
        ].filter(Boolean);

        if ('IntersectionObserver' in window) {
            const motionObserver = new IntersectionObserver(entries => {
                entries.forEach(entry => {
                    entry.target.classList.toggle('yg-perf-paused', !entry.isIntersecting);

                    if (entry.target === heroV4) {
                        if (entry.isIntersecting) {
                            restartV4Auto();
                        } else {
                            window.clearInterval(slideTimerV4);
                        }
                    }
                });
            }, {
                threshold: 0.01,
                rootMargin: '900px 0px 900px 0px'
            });

            motionTargets.forEach(target => motionObserver.observe(target));
        }

        const hero = document.getElementById('homeHeroV4');
        const heroVideos = hero ? Array.from(hero.querySelectorAll('video.dlx-slide')) : [];
        let heroVisible = true;

        const syncHeroVideos = () => {
            heroVideos.forEach(video => {
                const shouldPlay = heroVisible && !document.hidden && video.classList.contains('active');
                if (shouldPlay) {
                    video.play().catch(() => { });
                } else {
                    video.pause();
                }
            });
        };

        heroVideos.forEach(video => {
            if (!video.classList.contains('active')) video.pause();
        });

        if (hero && heroVideos.length && 'IntersectionObserver' in window) {
            const videoObserver = new IntersectionObserver(entries => {
                heroVisible = entries.some(entry => entry.isIntersecting);
                syncHeroVideos();
            }, { threshold: 0.03, rootMargin: '120px 0px' });
            videoObserver.observe(hero);
        }

        document.addEventListener('visibilitychange', syncHeroVideos);

        const nearbySection = document.getElementById('nearby-section');
        if (nearbySection && 'IntersectionObserver' in window) {
            const mapsObserver = new IntersectionObserver(entries => {
                if (!entries.some(entry => entry.isIntersecting)) return;
                mapsObserver.disconnect();
                loadGoogleMapsApi().catch(() => {
                    // يبقى زر تحديد الموقع قادراً على إعادة المحاولة لاحقاً.
                });
            }, { rootMargin: '1200px 0px', threshold: 0.01 });
            mapsObserver.observe(nearbySection);
        }
    }

    initHomePerformanceGovernor();


});

/* =============================================================================
   العروض فقط — عدّاد مستقل يعتمد على EndDate الحقيقي
   معزول عن بقية وظائف الصفحة ولا يعيد تحميلها.
   ============================================================================= */
(() => {
    'use strict';

    const startPromotionCountdowns = () => {
        const countdowns = Array.from(document.querySelectorAll('[data-promo-countdown]'));
        if (!countdowns.length) return;

        const arabicNumber = new Intl.NumberFormat('ar-YE', {
            useGrouping: false,
            minimumIntegerDigits: 2
        });

        const formatPart = value => arabicNumber.format(Math.max(0, value));

        const updateCountdown = element => {
            const output = element.querySelector('[data-promo-countdown-value]');
            const endValue = element.dataset.end;
            if (!output || !endValue) return;

            const endTime = new Date(endValue).getTime();
            if (!Number.isFinite(endTime)) return;

            const remaining = endTime - Date.now();
            if (remaining <= 0) {
                output.textContent = 'انتهى العرض';
                element.classList.add('is-expired');
                element.classList.remove('is-urgent');
                return;
            }

            const totalSeconds = Math.floor(remaining / 1000);
            const days = Math.floor(totalSeconds / 86400);
            const hours = Math.floor((totalSeconds % 86400) / 3600);
            const minutes = Math.floor((totalSeconds % 3600) / 60);
            const seconds = totalSeconds % 60;

            output.textContent = days > 0
                ? `${formatPart(days)} يوم · ${formatPart(hours)}:${formatPart(minutes)}:${formatPart(seconds)}`
                : `${formatPart(hours)}:${formatPart(minutes)}:${formatPart(seconds)}`;

            element.classList.toggle('is-urgent', remaining <= 86400000);
            element.classList.remove('is-expired');
        };

        const updateAll = () => countdowns.forEach(updateCountdown);
        updateAll();

        const intervalId = window.setInterval(updateAll, 1000);
        window.addEventListener('pagehide', () => window.clearInterval(intervalId), { once: true });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startPromotionCountdowns, { once: true });
    } else {
        startPromotionCountdowns();
    }
})();

/* =============================================================================
   V6 — مسارات أفقية للأماكن المميزة والعروض
   صف واحد، أسهم ذكية، سحب باللمس، ولوحة مفاتيح؛ بدون تغيير بيانات البطاقات.
   ============================================================================= */
(() => {
    'use strict';

    const initLuxuryHorizontalCarousels = () => {
        const carousels = Array.from(document.querySelectorAll('[data-horizontal-carousel]'));
        if (!carousels.length) return;

        carousels.forEach(carousel => {
            if (carousel.dataset.carouselReady === 'true') return;
            carousel.dataset.carouselReady = 'true';

            const viewport = carousel.querySelector('[data-carousel-viewport]');
            const previousButton = carousel.querySelector('[data-carousel-prev]');
            const nextButton = carousel.querySelector('[data-carousel-next]');
            const progress = carousel.querySelector('[data-carousel-progress]');
            if (!viewport || !previousButton || !nextButton) return;

            const progressTrack = progress?.parentElement || null;

            const isRtl = getComputedStyle(viewport).direction === 'rtl';
            let updateFrame = 0;

            const getStep = () => {
                const firstCard = viewport.querySelector('.featured-wow-stage, .home-promo-stage');
                if (!firstCard) return viewport.clientWidth * .88;
                const styles = getComputedStyle(viewport.querySelector('.featured-wow-grid, .home-promotions-grid'));
                const gap = Number.parseFloat(styles.columnGap || styles.gap || '0') || 0;
                return firstCard.getBoundingClientRect().width + gap;
            };

            const getPosition = () => Math.abs(viewport.scrollLeft);

            const updateState = () => {
                updateFrame = 0;
                viewport.style.setProperty('--carousel-width', `${viewport.clientWidth}px`);
                const maximum = Math.max(0, viewport.scrollWidth - viewport.clientWidth);
                const position = Math.min(maximum, getPosition());
                const tolerance = 4;
                const hasOverflow = maximum > tolerance;

                previousButton.hidden = true;
                nextButton.hidden = true;
                previousButton.disabled = !hasOverflow || position <= tolerance;
                nextButton.disabled = !hasOverflow || position >= maximum - tolerance;

                if (progress && progressTrack) {
                    const ratio = maximum > 0 ? position / maximum : 0;
                    const trackWidth = Math.max(1, progressTrack.clientWidth - 8);
                    const visibleRatio = Math.min(1, viewport.clientWidth / Math.max(viewport.scrollWidth, 1));
                    const thumbWidth = Math.max(48, trackWidth * visibleRatio);
                    const travel = Math.max(0, trackWidth - thumbWidth);
                    const offset = isRtl ? -(travel * ratio) : travel * ratio;

                    progress.style.setProperty('--carousel-thumb-width', `${thumbWidth}px`);
                    progress.style.setProperty('--carousel-thumb-offset', `${offset.toFixed(2)}px`);
                    progressTrack.hidden = !hasOverflow;
                }
            };

            const scheduleUpdate = () => {
                if (updateFrame) return;
                updateFrame = requestAnimationFrame(updateState);
            };

            const move = direction => {
                const logicalDirection = isRtl ? -direction : direction;
                viewport.scrollBy({ left: logicalDirection * getStep(), behavior: 'smooth' });
            };

            previousButton.addEventListener('click', () => move(-1));
            nextButton.addEventListener('click', () => move(1));
            viewport.addEventListener('scroll', scheduleUpdate, { passive: true });

            const setNormalizedPosition = value => {
                const maximum = Math.max(0, viewport.scrollWidth - viewport.clientWidth);
                const nextValue = Math.max(0, Math.min(maximum, value));
                viewport.scrollLeft = isRtl ? -nextValue : nextValue;
            };

            if (progressTrack && progress) {
                let dragging = false;
                let dragStartX = 0;
                let dragStartPosition = 0;

                progressTrack.addEventListener('pointerdown', event => {
                    if (event.button !== undefined && event.button !== 0) return;
                    const trackRect = progressTrack.getBoundingClientRect();
                    const thumbRect = progress.getBoundingClientRect();
                    const clickedThumb = event.clientX >= thumbRect.left && event.clientX <= thumbRect.right;

                    if (!clickedThumb) {
                        const maximum = Math.max(0, viewport.scrollWidth - viewport.clientWidth);
                        const usableWidth = Math.max(1, trackRect.width - thumbRect.width);
                        const rawOffset = isRtl
                            ? trackRect.right - event.clientX - (thumbRect.width / 2)
                            : event.clientX - trackRect.left - (thumbRect.width / 2);
                        setNormalizedPosition((Math.max(0, Math.min(usableWidth, rawOffset)) / usableWidth) * maximum);
                    }

                    dragging = true;
                    dragStartX = event.clientX;
                    dragStartPosition = getPosition();
                    progressTrack.setPointerCapture?.(event.pointerId);
                    event.preventDefault();
                });

                progressTrack.addEventListener('pointermove', event => {
                    if (!dragging) return;
                    const maximum = Math.max(0, viewport.scrollWidth - viewport.clientWidth);
                    const usableWidth = Math.max(1, progressTrack.clientWidth - progress.offsetWidth - 8);
                    const delta = event.clientX - dragStartX;
                    const normalizedDelta = (isRtl ? -delta : delta) * (maximum / usableWidth);
                    setNormalizedPosition(dragStartPosition + normalizedDelta);
                });

                const stopDragging = event => {
                    if (!dragging) return;
                    dragging = false;
                    progressTrack.releasePointerCapture?.(event.pointerId);
                };

                progressTrack.addEventListener('pointerup', stopDragging);
                progressTrack.addEventListener('pointercancel', stopDragging);
            }

            carousel.addEventListener('keydown', event => {
                if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
                event.preventDefault();
                move(event.key === 'ArrowLeft' ? 1 : -1);
            });

            if ('ResizeObserver' in window) {
                const resizeObserver = new ResizeObserver(scheduleUpdate);
                resizeObserver.observe(viewport);
                const track = viewport.firstElementChild;
                if (track) resizeObserver.observe(track);
            } else {
                window.addEventListener('resize', scheduleUpdate, { passive: true });
            }

            requestAnimationFrame(updateState);
            window.addEventListener('load', updateState, { once: true });
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initLuxuryHorizontalCarousels, { once: true });
    } else {
        initLuxuryHorizontalCarousels();
    }
})();
