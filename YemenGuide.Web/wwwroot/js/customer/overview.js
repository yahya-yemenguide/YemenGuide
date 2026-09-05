/**
 * ============================================================================
 * YEMENGUIDE — CUSTOMER OVERVIEW
 * Luxury reveal, 3D pointer depth, stat counters, countdown and pearl toasts.
 * ============================================================================
 */

(() => {
    "use strict";

    const state = {
        initialized: false,
        countdownTimer: null
    };

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");

    const qsa = (selector, scope = document) =>
        Array.from(scope.querySelectorAll(selector));

    const qs = (selector, scope = document) =>
        scope.querySelector(selector);

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function showOverviewToast(message, icon = "bi-check-circle-fill") {
        const stack = document.getElementById("overviewToastStack");

        if (!stack || !message) {
            return;
        }

        const toast = document.createElement("div");
        toast.className = "overview-toast";
        toast.innerHTML = `
            <i class="bi ${icon}" aria-hidden="true"></i>
            <strong></strong>
        `;

        const label = toast.querySelector("strong");
        if (label) {
            label.textContent = String(message);
        }

        stack.appendChild(toast);

        requestAnimationFrame(() => {
            toast.classList.add("is-visible");
        });

        window.setTimeout(() => {
            toast.classList.remove("is-visible");
            window.setTimeout(() => toast.remove(), 360);
        }, 2600);
    }

    function initializeReveal() {
        const items = qsa(".js-reveal");

        if (!items.length) {
            return;
        }

        if (reducedMotion.matches || !("IntersectionObserver" in window)) {
            items.forEach(item => item.classList.add("is-visible"));
            return;
        }

        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) {
                    return;
                }

                const index = Number(entry.target.dataset.revealIndex || 0);
                entry.target.style.transitionDelay = `${Math.min(index * 55, 260)}ms`;
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, {
            threshold: 0.12,
            rootMargin: "0px 0px -35px 0px"
        });

        items.forEach((item, index) => {
            item.dataset.revealIndex = String(index);
            observer.observe(item);
        });
    }

    function initializeCounters() {
        const counters = qsa(".js-count-up[data-count]");

        if (!counters.length) {
            return;
        }

        const runCounter = element => {
            if (element.dataset.counted === "true") {
                return;
            }

            element.dataset.counted = "true";
            const target = Math.max(0, Number.parseInt(element.dataset.count || "0", 10) || 0);

            if (reducedMotion.matches || target === 0) {
                element.textContent = String(target);
                return;
            }

            const duration = clamp(620 + target * 12, 650, 1350);
            const startedAt = performance.now();

            const tick = now => {
                const progress = clamp((now - startedAt) / duration, 0, 1);
                const eased = 1 - Math.pow(1 - progress, 3);
                element.textContent = String(Math.round(target * eased));

                if (progress < 1) {
                    requestAnimationFrame(tick);
                } else {
                    element.textContent = String(target);
                }
            };

            requestAnimationFrame(tick);
        };

        if (!("IntersectionObserver" in window)) {
            counters.forEach(runCounter);
            return;
        }

        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    runCounter(entry.target);
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.45 });

        counters.forEach(counter => observer.observe(counter));
    }

    function initializePointerGlow() {
        if (!finePointer.matches || reducedMotion.matches) {
            return;
        }

        qsa(".overview-stat-card:not(.overview-stat-card--static), .overview-quick-card").forEach(card => {
            card.addEventListener("pointermove", event => {
                const rect = card.getBoundingClientRect();
                card.style.setProperty("--mouse-x", `${event.clientX - rect.left}px`);
                card.style.setProperty("--mouse-y", `${event.clientY - rect.top}px`);
            }, { passive: true });
        });
    }

    function initializeTilt() {
        if (!finePointer.matches || reducedMotion.matches) {
            return;
        }

        qsa(".js-tilt").forEach(element => {
            const strength = clamp(Number(element.dataset.tiltStrength || 3), 1, 8);
            let frame = 0;

            const reset = () => {
                cancelAnimationFrame(frame);
                element.style.transition = "transform .42s cubic-bezier(.22,1,.36,1)";
                element.style.transform = "perspective(1100px) rotateX(0deg) rotateY(0deg) translateY(0)";
            };

            element.addEventListener("pointermove", event => {
                const rect = element.getBoundingClientRect();
                const x = (event.clientX - rect.left) / rect.width - .5;
                const y = (event.clientY - rect.top) / rect.height - .5;

                cancelAnimationFrame(frame);
                frame = requestAnimationFrame(() => {
                    element.style.transition = "transform .12s linear";
                    element.style.transform = `perspective(1100px) rotateX(${(-y * strength).toFixed(2)}deg) rotateY(${(x * strength).toFixed(2)}deg) translateY(-2px)`;
                });
            }, { passive: true });

            element.addEventListener("pointerleave", reset, { passive: true });
            element.addEventListener("blur", reset, true);
        });
    }

    function formatCountdown(ms) {
        if (ms <= 0) {
            return "موعد الحجز بدأ";
        }

        const minute = 60 * 1000;
        const hour = 60 * minute;
        const day = 24 * hour;

        const days = Math.floor(ms / day);
        const hours = Math.floor((ms % day) / hour);
        const minutes = Math.floor((ms % hour) / minute);

        if (days > 0) {
            return `${days} يوم و ${hours} ساعة`;
        }

        if (hours > 0) {
            return `${hours} ساعة و ${minutes} دقيقة`;
        }

        return `${Math.max(1, minutes)} دقيقة`;
    }

    function initializeCountdown() {
        const countdown = qs("[data-countdown-to]");
        const output = countdown?.querySelector("[data-countdown-output]");

        if (!countdown || !output) {
            return;
        }

        const target = new Date(countdown.dataset.countdownTo || "");

        if (Number.isNaN(target.getTime())) {
            output.textContent = "موعد قريب";
            return;
        }

        const update = () => {
            output.textContent = formatCountdown(target.getTime() - Date.now());
        };

        update();
        state.countdownTimer = window.setInterval(update, 60 * 1000);
    }

    function initializeOfferAmbient() {
        const offers = qsa(".overview-offer-card");

        if (offers.length < 2 || reducedMotion.matches) {
            return;
        }

        let activeIndex = 0;

        window.setInterval(() => {
            offers.forEach((offer, index) => {
                offer.classList.toggle("is-ambient-active", index === activeIndex);
            });

            activeIndex = (activeIndex + 1) % offers.length;
        }, 3600);
    }

    function initialize() {
        if (state.initialized) {
            return;
        }

        state.initialized = true;

        initializeReveal();
        initializeCounters();
        initializePointerGlow();
        initializeTilt();
        initializeCountdown();
        initializeOfferAmbient();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
