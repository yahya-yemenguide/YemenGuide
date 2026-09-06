(() => {
    "use strict";

    const CONFIG_ID = "customerAdvertisementTracking";
    const HERO_SLIDE_SELECTOR = "[data-customer-ad-hero-slide]";
    const HERO_CALLOUT_SELECTOR = "[data-customer-hero-ad-callout]";
    const AD_SELECTOR = "[data-customer-ad]";
    const CLICK_SELECTOR = "[data-customer-ad-click]";
    const SEARCH_SLIDE_SELECTOR = "[data-search-ad-slide]";

    const impressionSent = new Set();
    const impressionTimers = new Map();
    let observer = null;

    function createRequestKey() {
        try {
            if (window.crypto && typeof window.crypto.randomUUID === "function") {
                return window.crypto.randomUUID();
            }
        } catch (_) {
            // fallback below
        }

        return `ad_${Date.now()}_${Math.random().toString(36).slice(2, 12)}`;
    }

    function getConfig() {
        const host = document.getElementById(CONFIG_ID);
        if (!host) return null;

        const token = host.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
        const impressionUrl = host.dataset.impressionUrl || "";
        const clickUrl = host.dataset.clickUrl || "";
        const bookingStartUrl = host.dataset.bookingStartUrl || "";

        if (!token || !impressionUrl || !clickUrl) return null;

        return {
            token,
            impressionUrl,
            clickUrl,
            bookingStartUrl
        };
    }

    function adData(element) {
        if (!(element instanceof Element)) return null;

        const advertisementId = Number.parseInt(element.dataset.advertisementId || "0", 10);
        const placementType = String(element.dataset.placementType || "").trim();

        if (!Number.isFinite(advertisementId) || advertisementId <= 0 || !placementType) {
            return null;
        }

        return { advertisementId, placementType };
    }

    function adKey(element) {
        const data = adData(element);
        return data ? `${data.advertisementId}:${data.placementType}` : "";
    }

    async function postEvent(url, payload, config, keepalive = false) {
        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            keepalive,
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "RequestVerificationToken": config.token,
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(`AdvertisementTrackingHttp${response.status}`);
        }

        const contentType = response.headers.get("content-type") || "";
        if (!contentType.includes("application/json")) {
            throw new Error("AdvertisementTrackingInvalidResponse");
        }

        return response.json();
    }

    async function sendImpression(data, config) {
        if (!data || !config) return false;

        const key = `${data.advertisementId}:${data.placementType}`;
        if (impressionSent.has(key)) return true;

        impressionSent.add(key);

        try {
            await postEvent(
                config.impressionUrl,
                {
                    advertisementId: data.advertisementId,
                    placementType: data.placementType,
                    requestKey: createRequestKey()
                },
                config,
                true
            );
            return true;
        } catch (error) {
            impressionSent.delete(key);
            console.debug("Customer advertisement impression was not stored.", error);
            return false;
        }
    }

    function trackDynamicImpression(advertisementId, placementType, config) {
        const id = Number.parseInt(String(advertisementId || "0"), 10);
        const placement = String(placementType || "").trim();

        if (!Number.isFinite(id) || id <= 0 || !placement) return;

        void sendImpression(
            { advertisementId: id, placementType: placement },
            config
        );
    }

    async function recordBookingStarted(advertisementId, placeId, config) {
        const adId = Number.parseInt(String(advertisementId || "0"), 10);
        const targetPlaceId = Number.parseInt(String(placeId || "0"), 10);

        if (!config?.bookingStartUrl || adId <= 0 || targetPlaceId <= 0) {
            return false;
        }

        try {
            const result = await postEvent(
                config.bookingStartUrl,
                {
                    advertisementId: adId,
                    placeId: targetPlaceId,
                    requestKey: createRequestKey()
                },
                config,
                true
            );

            return result?.success === true || result?.alreadyRecorded === true;
        } catch (error) {
            console.debug("Customer advertisement booking start was not stored.", error);
            return false;
        }
    }

    function cancelImpressionTimer(element) {
        const timer = impressionTimers.get(element);
        if (timer) {
            window.clearTimeout(timer);
            impressionTimers.delete(element);
        }
    }

    function scheduleImpression(element, config, delay = 700) {
        const data = adData(element);
        if (!data) return;

        const key = adKey(element);
        if (!key || impressionSent.has(key) || impressionTimers.has(element)) return;

        const timer = window.setTimeout(async () => {
            impressionTimers.delete(element);

            if (!element.isConnected || element.hidden) return;

            // إعلان Hero يجب أن يكون فعلاً الشريحة النشطة عند لحظة الاحتساب.
            if (element.matches(HERO_CALLOUT_SELECTOR)) {
                const activeSlide = document.querySelector(`${HERO_SLIDE_SELECTOR}.active`);
                if (!activeSlide ||
                    activeSlide.dataset.advertisementId !== String(data.advertisementId)) {
                    return;
                }
            }

            // في سلايدر البحث لا نحتسب إلا الإعلان الظاهر فعلياً،
            // لأن الشرائح الأخرى موجودة في DOM لكنها مخفية فوق نفس المساحة.
            if (element.matches(SEARCH_SLIDE_SELECTOR) &&
                (!element.classList.contains("is-active") ||
                 element.getAttribute("aria-hidden") === "true")) {
                return;
            }

            await sendImpression(data, config);
        }, delay);

        impressionTimers.set(element, timer);
    }

    function initRegularImpressions(config) {
        const elements = Array.from(document.querySelectorAll(AD_SELECTOR))
            .filter(element =>
                !element.matches(HERO_CALLOUT_SELECTOR) &&
                !element.matches(SEARCH_SLIDE_SELECTOR));

        if (!elements.length) return;

        if (!("IntersectionObserver" in window)) {
            elements.forEach(element => scheduleImpression(element, config, 900));
            return;
        }

        observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                const element = entry.target;
                if (entry.isIntersecting && entry.intersectionRatio >= 0.52) {
                    scheduleImpression(element, config, 650);
                } else {
                    cancelImpressionTimer(element);
                }
            });
        }, {
            threshold: [0, 0.25, 0.52, 0.75],
            rootMargin: "0px"
        });

        elements.forEach(element => observer.observe(element));
    }

    function initSearchSliderImpressions(config) {
        const slides = Array.from(document.querySelectorAll(SEARCH_SLIDE_SELECTOR));
        if (!slides.length) return;

        /*
         * المشاهدة الفعلية:
         * لا يكفي أن تكون الشريحة Active داخل DOM.
         * يجب أن تكون ظاهرة فعلاً بنسبة 52% على الأقل لمدة 650ms.
         */
        const visibleRatios = new WeakMap();

        const sync = () => {
            slides.forEach(slide => {
                const active =
                    slide.classList.contains("is-active") &&
                    slide.getAttribute("aria-hidden") !== "true";

                const visibleEnough =
                    !("IntersectionObserver" in window) ||
                    (visibleRatios.get(slide) || 0) >= 0.52;

                if (active && visibleEnough && !document.hidden) {
                    scheduleImpression(slide, config, 650);
                } else {
                    cancelImpressionTimer(slide);
                }
            });
        };

        if ("MutationObserver" in window) {
            const mutationObserver = new MutationObserver(sync);

            slides.forEach(slide => mutationObserver.observe(slide, {
                attributes: true,
                attributeFilter: ["class", "aria-hidden"]
            }));

            window.addEventListener(
                "pagehide",
                () => mutationObserver.disconnect(),
                { once: true }
            );
        }

        if ("IntersectionObserver" in window) {
            const visibilityObserver = new IntersectionObserver(entries => {
                entries.forEach(entry => {
                    visibleRatios.set(
                        entry.target,
                        entry.isIntersecting ? entry.intersectionRatio : 0
                    );
                });

                sync();
            }, {
                threshold: [0, 0.25, 0.52, 0.75, 1]
            });

            slides.forEach(slide => visibilityObserver.observe(slide));

            window.addEventListener(
                "pagehide",
                () => visibilityObserver.disconnect(),
                { once: true }
            );
        } else {
            sync();
        }

        document.addEventListener("visibilitychange", sync);

        window.addEventListener(
            "pagehide",
            () => document.removeEventListener("visibilitychange", sync),
            { once: true }
        );
    }

    function initHeroSponsoredSlide(config) {
        const slides = Array.from(document.querySelectorAll(HERO_SLIDE_SELECTOR));
        const callout = document.querySelector(HERO_CALLOUT_SELECTOR);

        if (!slides.length || !callout) return;

        const sync = () => {
            const active = slides.find(slide => slide.classList.contains("active")) || null;
            const calloutId = callout.dataset.advertisementId || "";
            const shouldShow = Boolean(
                active &&
                active.dataset.advertisementId === calloutId
            );

            callout.hidden = !shouldShow;

            if (shouldShow) {
                scheduleImpression(callout, config, 800);
            } else {
                cancelImpressionTimer(callout);
            }
        };

        slides.forEach(slide => {
            const mutationObserver = new MutationObserver(mutations => {
                if (mutations.some(mutation => mutation.attributeName === "class")) {
                    sync();
                }
            });

            mutationObserver.observe(slide, {
                attributes: true,
                attributeFilter: ["class"]
            });
        });

        // الـindex.js يجهز الشريحة الحالية في نفس DOMContentLoaded.
        // تأخير صغير يضمن أننا نقرأ الحالة النهائية للسلايدر الأصلي.
        window.setTimeout(sync, 80);
    }

    function initDynamicTracking(config) {
        document.addEventListener("customer-ad:impression", event => {
            const detail = event?.detail || {};
            trackDynamicImpression(
                detail.advertisementId,
                detail.placementType,
                config
            );
        });

        // API صغيرة تستخدمها صفحة التفاصيل لتسجيل بدء رحلة الحجز بعد
        // التحقق الخادمي من أن الزيارة جاءت من نقرة إعلانية صحيحة.
        window.CustomerAdvertisements = Object.assign(
            {},
            window.CustomerAdvertisements || {},
            {
                trackImpression(advertisementId, placementType) {
                    trackDynamicImpression(
                        advertisementId,
                        placementType,
                        config
                    );
                },
                recordBookingStarted(advertisementId, placeId) {
                    return recordBookingStarted(
                        advertisementId,
                        placeId,
                        config
                    );
                }
            }
        );
    }

    function initClicks(config) {
        document.addEventListener("click", async event => {
            const target = event.target.closest(CLICK_SELECTOR);
            if (!(target instanceof Element)) return;

            const data = adData(target);
            if (!data) return;

            const fallbackUrl = target.dataset.fallbackUrl ||
                (target instanceof HTMLAnchorElement ? target.href : "/");

            event.preventDefault();
            // Search.js يملك أيضاً معالجاً لفتح الشريحة. نوقفه هنا حتى لا
            // ينتقل المتصفح قبل أن يصل حدث Click إلى الخادم.
            event.stopImmediatePropagation();

            if (target.dataset.adClickPending === "true") return;
            target.dataset.adClickPending = "true";
            target.setAttribute("aria-busy", "true");

            try {
                const response = await postEvent(
                    config.clickUrl,
                    {
                        advertisementId: data.advertisementId,
                        placementType: data.placementType,
                        requestKey: createRequestKey()
                    },
                    config
                );

                const redirectUrl = response?.redirectUrl || fallbackUrl;
                window.location.assign(redirectUrl);
            } catch (error) {
                console.debug("Customer advertisement click was not stored.", error);
                window.location.assign(fallbackUrl);
            } finally {
                target.dataset.adClickPending = "false";
                target.removeAttribute("aria-busy");
            }
        });
    }

    function init() {
        const config = getConfig();
        if (!config) return;

        initRegularImpressions(config);
        initSearchSliderImpressions(config);
        initHeroSponsoredSlide(config);
        initDynamicTracking(config);
        initClicks(config);

        window.addEventListener("pagehide", () => {
            observer?.disconnect();
            impressionTimers.forEach(timer => window.clearTimeout(timer));
            impressionTimers.clear();
        }, { once: true });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})();
