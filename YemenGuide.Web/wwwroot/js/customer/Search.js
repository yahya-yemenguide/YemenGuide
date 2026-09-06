/**
 * =============================================================================
 * SEARCH.JS — Luxury Cinematic 3D
 * يحافظ على البحث AJAX والمفضلة وحركة الصور، ويضيف 3D فقط للأجهزة ذات الماوس.
 * =============================================================================
 */

(function () {
    "use strict";

    const finePointerQuery = window.matchMedia("(hover: hover) and (pointer: fine)");
    const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");

    document.addEventListener("DOMContentLoaded", function () {
        setupDragToScroll("categoriesWrapper");
        setupDragToScroll("advancedSearchPanel", ".custom-chip-container");
        setupCategoryKeyboard();
        setupAdvancedFilterState();
        setupCardActions();
        initPageReveal();
        initializeSearchResults(document);
        initSearchAdvertisementSlider(document);
    });

    /* =========================================================================
       1. Horizontal drag scroll
       ========================================================================= */
    function setupDragToScroll(containerId, selector = null) {
        const container = document.getElementById(containerId);
        const slider = selector && container ? container.querySelector(selector) : container;
        if (!slider || !finePointerQuery.matches) return;

        let isDown = false;
        let startX = 0;
        let scrollLeft = 0;

        slider.addEventListener("mousedown", function (event) {
            if (event.button !== 0) return;
            isDown = true;
            startX = event.pageX - slider.offsetLeft;
            scrollLeft = slider.scrollLeft;
            slider.classList.add("is-dragging");
        });

        window.addEventListener("mouseup", function () {
            isDown = false;
            slider.classList.remove("is-dragging");
        });

        window.addEventListener("mousemove", function (event) {
            if (!isDown) return;
            event.preventDefault();
            const x = event.pageX - slider.offsetLeft;
            slider.scrollLeft = scrollLeft - ((x - startX) * 1.25);
        });
    }

    /* =========================================================================
       2. Categories keyboard support
       ========================================================================= */
    function setupCategoryKeyboard() {
        const categoryItems = Array.from(document.querySelectorAll(".category-item"));

        categoryItems.forEach(function (item, index) {
            item.addEventListener("keydown", function (event) {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    item.click();
                    return;
                }

                if (event.key === "ArrowRight" && index > 0) {
                    event.preventDefault();
                    categoryItems[index - 1].focus();
                }

                if (event.key === "ArrowLeft" && index < categoryItems.length - 1) {
                    event.preventDefault();
                    categoryItems[index + 1].focus();
                }
            });
        });
    }

    /* =========================================================================
       3. Advanced filter state
       ========================================================================= */
    function setupAdvancedFilterState() {
        const panel = document.getElementById("advancedSearchPanel");
        const button = document.querySelector(".search-filter-toggle");
        if (!panel || !button) return;

        panel.addEventListener("show.bs.collapse", function () {
            button.classList.add("is-open");
        });

        panel.addEventListener("hide.bs.collapse", function () {
            button.classList.remove("is-open");
        });

        panel.addEventListener("change", updateActiveFilterCount);
        updateActiveFilterCount();
    }

    function updateActiveFilterCount() {
        const panel = document.getElementById("advancedSearchPanel");
        const badge = document.getElementById("activeFilterCount");
        const button = document.querySelector(".search-filter-toggle");
        if (!panel || !badge || !button) return;

        const activeCount = panel.querySelectorAll('input[type="checkbox"]:checked').length;
        badge.textContent = String(activeCount);
        badge.hidden = activeCount === 0;
        button.classList.toggle("has-active-filters", activeCount > 0);
    }


    /* =========================================================================
       4.5 Favorite action only
       الروابط تظل روابط HTML طبيعية حتى لا تتأثر بحركة 3D.
       القلب فقط نعزله عن التنقل ويعمل بعد AJAX.
       ========================================================================= */
    function setupCardActions() {
        if (document.documentElement.dataset.searchCardActionsBound === "true") return;
        document.documentElement.dataset.searchCardActionsBound = "true";

        document.addEventListener("click", function (event) {
            const favoriteButton = event.target.closest?.("[data-search-favorite]");
            if (!favoriteButton) return;

            event.preventDefault();
            event.stopPropagation();

            const placeId = Number.parseInt(favoriteButton.dataset.placeId || "0", 10);
            if (placeId > 0) {
                window.toggleFavoriteVip(null, placeId, favoriteButton);
            }
        });
    }

    /* =========================================================================
       4. One-time page reveal
       ========================================================================= */
    function initPageReveal() {
        const items = document.querySelectorAll(".search-reveal-item");
        if (!items.length) return;

        if (reducedMotionQuery.matches || !("IntersectionObserver" in window)) {
            items.forEach(item => item.classList.add("is-visible"));
            return;
        }

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.12 });

        items.forEach(item => observer.observe(item));
    }

    /* =========================================================================
       5. Initialize cards after initial load or AJAX replacement
       ========================================================================= */
    function initializeSearchResults(root) {
        initCardReveal(root);
        initSearchCard3D(root);
        initMagicImageSliders(root);
        lockThumbnailInteraction(root);
    }

    function initCardReveal(root) {
        const stages = root.querySelectorAll ? root.querySelectorAll("[data-search-stage]") : [];
        if (!stages.length) return;

        if (reducedMotionQuery.matches || !("IntersectionObserver" in window)) {
            stages.forEach(stage => stage.classList.add("is-visible"));
            return;
        }

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.10, rootMargin: "50px 0px" });

        stages.forEach(stage => observer.observe(stage));
    }

    /* =========================================================================
       6. Fine-pointer cinematic depth + spotlight
       لا ندوّر عنصر البطاقة نفسه حتى تبقى الروابط والأزرار قابلة للنقر دائماً.
       ========================================================================= */
    function initSearchCard3D(root) {
        const cards = root.querySelectorAll ? root.querySelectorAll("[data-search-card]") : [];
        if (!cards.length) return;

        cards.forEach(function (card) {
            if (card.dataset.tiltInitialized === "true") return;
            card.dataset.tiltInitialized = "true";

            const mainImage = card.querySelector(".featured-main-photo");
            let frameId = 0;

            const reset = function () {
                if (frameId) cancelAnimationFrame(frameId);
                card.style.setProperty("--featured-rx", "0deg");
                card.style.setProperty("--featured-ry", "0deg");
                card.style.setProperty("--featured-mx", "50%");
                card.style.setProperty("--featured-my", "35%");

                if (mainImage) {
                    mainImage.style.transform = "scale(1.025) translate3d(0,0,0)";
                }

            };

            if (!finePointerQuery.matches || reducedMotionQuery.matches) {
                reset();
                return;
            }

            card.addEventListener("pointermove", function (event) {
                if (event.pointerType && event.pointerType !== "mouse") return;

                const rect = card.getBoundingClientRect();
                const px = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
                const py = Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));
                const rotateY = (px - 0.5) * 8.5;
                const rotateX = (0.5 - py) * 6.2;

                if (frameId) cancelAnimationFrame(frameId);
                frameId = requestAnimationFrame(function () {
                    card.style.setProperty("--featured-rx", `${rotateX.toFixed(2)}deg`);
                    card.style.setProperty("--featured-ry", `${rotateY.toFixed(2)}deg`);
                    card.style.setProperty("--featured-mx", `${(px * 100).toFixed(1)}%`);
                    card.style.setProperty("--featured-my", `${(py * 100).toFixed(1)}%`);

                    if (mainImage) {
                        const x = (0.5 - px) * 7.5;
                        const y = (0.5 - py) * 5;
                        mainImage.style.transform = `scale(1.065) translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, 0)`;
                    }

                });
            });

            card.addEventListener("pointerleave", reset);
            card.addEventListener("pointercancel", reset);
        });
    }

    /* =========================================================================
       7. Smart thumbnail gallery — automatic only, never reacts to click/touch
       ========================================================================= */
    function initMagicImageSliders(root) {
        const cards = root.querySelectorAll ? root.querySelectorAll("[data-search-card]") : [];
        if (!cards.length) return;

        cards.forEach(function (card, cardIndex) {
            if (card.dataset.sliderInitialized === "true") return;
            card.dataset.sliderInitialized = "true";

            const gallery = card.querySelector(".featured-wow-gallery");
            if (!gallery) return;

            let imagesList = [];
            try {
                imagesList = JSON.parse(gallery.getAttribute("data-images") || "[]") || [];
            } catch {
                imagesList = [];
            }

            imagesList = imagesList.filter(Boolean);

            const imageContainers = Array.from(gallery.querySelectorAll(".featured-wow-thumb"));
            if (reducedMotionQuery.matches || imagesList.length <= 3 || imageContainers.length === 0) {
                return;
            }

            let intervalId = null;
            let startTimeoutId = null;
            let currentImageIndex = 3;
            let currentBoxIndex = 0;
            let isVisible = true;

            // الهاتف أهدأ، والكمبيوتر أسرع قليلًا بدون إزعاج.
            const intervalDelay = finePointerQuery.matches ? 3600 : 5000;
            const fadeDuration = finePointerQuery.matches ? 700 : 820;
            const initialDelay = 900 + ((cardIndex % 3) * 420);

            const changeNextImage = function () {
                if (!isVisible || document.hidden || !imagesList.length) return;

                const container = imageContainers[currentBoxIndex];
                const currentImage = container ? container.querySelector("img.magic-image") : null;
                if (!currentImage) {
                    currentBoxIndex = (currentBoxIndex + 1) % imageContainers.length;
                    return;
                }

                let attempts = 0;
                let nextSource = imagesList[currentImageIndex % imagesList.length];
                while (
                    nextSource &&
                    currentImage.getAttribute("src") === nextSource &&
                    attempts < imagesList.length
                ) {
                    currentImageIndex = (currentImageIndex + 1) % imagesList.length;
                    nextSource = imagesList[currentImageIndex];
                    attempts++;
                }

                if (!nextSource) return;

                container.classList.add("is-gallery-changing");
                crossFadeImage(container, currentImage, nextSource, fadeDuration);

                window.setTimeout(function () {
                    container.classList.remove("is-gallery-changing");
                }, fadeDuration + 100);

                currentImageIndex = (currentImageIndex + 1) % imagesList.length;
                currentBoxIndex = (currentBoxIndex + 1) % imageContainers.length;
            };

            const stopRotation = function () {
                if (startTimeoutId) {
                    window.clearTimeout(startTimeoutId);
                    startTimeoutId = null;
                }

                if (intervalId) {
                    window.clearInterval(intervalId);
                    intervalId = null;
                }
            };

            const startRotation = function () {
                stopRotation();

                if (!isVisible || document.hidden || reducedMotionQuery.matches) return;

                startTimeoutId = window.setTimeout(function () {
                    changeNextImage();
                    intervalId = window.setInterval(changeNextImage, intervalDelay);
                }, initialDelay);
            };

            /*
             * لا نربط المعرض بالـ hover أو click.
             * الصور تتبدل بهدوء فقط طالما البطاقة ظاهرة،
             * لذلك الضغط عليها أو على البطاقة لا يغير شكلها ولا موضعها.
             */

            if ("IntersectionObserver" in window) {
                const observer = new IntersectionObserver(function (entries) {
                    entries.forEach(function (entry) {
                        if (entry.target !== card) return;

                        isVisible = entry.isIntersecting && entry.intersectionRatio > 0.18;

                        if (isVisible) {
                            startRotation();
                        } else {
                            stopRotation();
                        }
                    });
                }, {
                    threshold: [0, 0.18, 0.5]
                });

                observer.observe(card);
            } else {
                startRotation();
            }

            document.addEventListener("visibilitychange", function () {
                if (document.hidden) {
                    stopRotation();
                } else if (isVisible) {
                    startRotation();
                }
            });
        });
    }

    function crossFadeImage(container, image, newSource, duration) {
        const clone = image.cloneNode(true);
        clone.removeAttribute("data-original");
        clone.style.position = "absolute";
        clone.style.inset = "0";
        clone.style.width = "100%";
        clone.style.height = "100%";
        clone.style.objectFit = "cover";
        clone.style.zIndex = "3";
        clone.style.opacity = "1";
        clone.style.transition = `opacity ${duration}ms ease`;
        clone.style.pointerEvents = "none";

        container.appendChild(clone);
        image.setAttribute("src", newSource);

        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                clone.style.opacity = "0";
            });
        });

        window.setTimeout(function () {
            clone.remove();
        }, duration + 80);
    }


    /* =========================================================================
       7.1 Lock thumbnail interaction state
       الصور المصغرة للعرض فقط: لا Drag ولا ضغط بصري ولا تفاعل منفصل.
       النقر يمر للرابط الموجود خلف المعرض بشكل طبيعي.
       ========================================================================= */
    function lockThumbnailInteraction(root) {
        const galleries = root.querySelectorAll ? root.querySelectorAll(".search-featured-card .featured-wow-thumbs") : [];
        galleries.forEach(function (gallery) {
            gallery.setAttribute("aria-hidden", "true");
            gallery.querySelectorAll("img").forEach(function (image) {
                image.draggable = false;
                image.setAttribute("draggable", "false");
            });
        });
    }


    /* =========================================================================
       7.2 WOW unified Search advertisement slider
       - كل الإعلانات المؤهلة تدور داخل Slot واحد.
       - هذا الملف مسؤول عن الحركة والتنقل فقط.
       - تسجيل المشاهدة والنقرة موحد في customer/advertisements.js
         حتى لا يصل نفس الحدث للخادم من ملفين مختلفين.
       ========================================================================= */

    let activeSearchAdCleanup = null;

    function initSearchAdvertisementSlider(root = document) {
        if (typeof activeSearchAdCleanup === "function") {
            activeSearchAdCleanup();
            activeSearchAdCleanup = null;
        }

        const slider = root.querySelector?.("[data-search-ad-slider]");
        if (!slider) return;

        const slides = Array.from(slider.querySelectorAll("[data-search-ad-slide]"));
        if (!slides.length) return;

        const previousButton = slider.querySelector("[data-search-ad-prev]");
        const nextButton = slider.querySelector("[data-search-ad-next]");
        const dots = Array.from(slider.querySelectorAll("[data-search-ad-dot]"));
        const currentOutput = slider.querySelector("[data-search-ad-current]");

        const autoplayMs = Math.max(
            4500,
            Number.parseInt(slider.dataset.autoplayMs || "6000", 10) || 6000
        );

        slider.style.setProperty("--ad-duration", `${autoplayMs}ms`);

        const swipeThreshold = Math.max(
            40,
            Number.parseInt(slider.dataset.swipeThreshold || "52", 10) || 52
        );

        let currentIndex = Math.max(
            0,
            slides.findIndex(slide => slide.classList.contains("is-active"))
        );
        let autoplayTimer = 0;
        let transitionTimer = 0;
        let destroyed = false;
        let transitioning = false;
        let pointerStartX = null;
        let pointerStartY = null;
        let pointerMoved = false;
        let suppressClickUntil = 0;

        function pauseSlideMedia(slide) {
            slide?.querySelectorAll("video").forEach(video => {
                try { video.pause(); } catch { }
            });
        }

        function playSlideMedia(slide) {
            slide?.querySelectorAll("video").forEach(video => {
                try {
                    video.muted = true;
                    const result = video.play();
                    result?.catch?.(() => { });
                } catch { }
            });
        }

        function updateUi() {
            dots.forEach((dot, index) => {
                const active = index === currentIndex;
                dot.classList.toggle("is-active", active);
                dot.setAttribute("aria-selected", active ? "true" : "false");
            });

            if (currentOutput) {
                currentOutput.textContent =
                    String(currentIndex + 1).padStart(2, "0");
            }
        }

        function clearTransitionClasses(slide) {
            slide?.classList.remove(
                "is-enter-next",
                "is-enter-prev",
                "is-leaving-next",
                "is-leaving-prev"
            );
        }

        function stopAutoplay() {
            if (!autoplayTimer) return;
            window.clearInterval(autoplayTimer);
            autoplayTimer = 0;
        }

        function restartAutoplay() {
            stopAutoplay();

            if (
                slides.length <= 1 ||
                document.hidden ||
                reducedMotionQuery.matches ||
                destroyed
            ) {
                return;
            }

            autoplayTimer = window.setInterval(() => {
                showSlide(currentIndex + 1, {
                    restart: false,
                    direction: "next"
                });
            }, autoplayMs);
        }

        function showSlide(index, { restart = true, direction = "next" } = {}) {
            if (destroyed || transitioning || !slides.length) return;

            const normalized =
                ((index % slides.length) + slides.length) % slides.length;

            if (
                normalized === currentIndex &&
                slides[currentIndex]?.classList.contains("is-active")
            ) {
                updateUi();
                if (restart) restartAutoplay();
                return;
            }

            transitioning = true;

            const outgoing = slides[currentIndex];
            const incoming = slides[normalized];

            slides.forEach(clearTransitionClasses);
            tiltCards.forEach(resetTilt);

            if (outgoing && outgoing !== incoming) {
                pauseSlideMedia(outgoing);
                outgoing.classList.remove("is-active");
                outgoing.classList.add(
                    direction === "prev"
                        ? "is-leaving-prev"
                        : "is-leaving-next"
                );
                outgoing.setAttribute("aria-hidden", "true");
                outgoing.setAttribute("tabindex", "-1");
            }

            incoming.classList.add(
                "is-active",
                direction === "prev"
                    ? "is-enter-prev"
                    : "is-enter-next"
            );
            incoming.setAttribute("aria-hidden", "false");
            incoming.setAttribute("tabindex", "0");

            currentIndex = normalized;
            updateUi();
            playSlideMedia(incoming);

            window.clearTimeout(transitionTimer);
            transitionTimer = window.setTimeout(() => {
                slides.forEach(clearTransitionClasses);
                transitioning = false;
            }, 850);

            if (restart) restartAutoplay();
        }

        function goPrevious(event) {
            event?.preventDefault?.();
            event?.stopPropagation?.();

            if (transitioning) return;

            showSlide(currentIndex - 1, {
                direction: "prev"
            });
        }

        function goNext(event) {
            event?.preventDefault?.();
            event?.stopPropagation?.();

            if (transitioning) return;

            showSlide(currentIndex + 1, {
                direction: "next"
            });
        }

        function onVisibilityChange() {
            if (document.hidden) stopAutoplay();
            else restartAutoplay();
        }

        function onKeyDown(event) {
            if (event.target.closest("a,button,input,select,textarea")) return;

            if (event.key === "ArrowRight") {
                goPrevious(event);
            } else if (event.key === "ArrowLeft") {
                goNext(event);
            }
        }

        const tiltCards = Array.from(
            slider.querySelectorAll("[data-search-ad-tilt]")
        );

        function resetTilt(card) {
            if (!card) return;
            card.style.setProperty("--tilt-x", "0deg");
            card.style.setProperty("--tilt-y", "0deg");
            card.style.setProperty("--glow-x", "50%");
            card.style.setProperty("--glow-y", "50%");
        }

        function onTiltMove(event) {
            if (!finePointerQuery.matches || reducedMotionQuery.matches) return;

            const card = event.currentTarget;
            const slide = card.closest("[data-search-ad-slide]");
            if (!slide?.classList.contains("is-active")) return;

            const rect = card.getBoundingClientRect();
            const x = Math.min(Math.max((event.clientX - rect.left) / rect.width, 0), 1);
            const y = Math.min(Math.max((event.clientY - rect.top) / rect.height, 0), 1);

            const rotateY = (x - .5) * 3.6;
            const rotateX = (.5 - y) * 2.4;

            card.style.setProperty("--tilt-x", `${rotateX.toFixed(2)}deg`);
            card.style.setProperty("--tilt-y", `${rotateY.toFixed(2)}deg`);
            card.style.setProperty("--glow-x", `${(x * 100).toFixed(1)}%`);
            card.style.setProperty("--glow-y", `${(y * 100).toFixed(1)}%`);
        }

        function onTiltLeave(event) {
            resetTilt(event.currentTarget);
        }

        tiltCards.forEach(card => {
            card.addEventListener("pointermove", onTiltMove, { passive: true });
            card.addEventListener("pointerleave", onTiltLeave, { passive: true });
        });

        function onPointerDown(event) {
            if (slides.length <= 1) return;
            if (event.pointerType === "mouse" && event.button !== 0) return;

            if (event.target.closest(
                "[data-search-ad-prev],[data-search-ad-next]," +
                "[data-search-ad-dot],[data-search-ad-click]," +
                "a,button,input,select,textarea,label,video"
            )) {
                return;
            }

            pointerStartX = event.clientX;
            pointerStartY = event.clientY;
            pointerMoved = false;
            stopAutoplay();
        }

        function onPointerMove(event) {
            if (pointerStartX === null || pointerStartY === null) return;

            const dx = event.clientX - pointerStartX;
            const dy = event.clientY - pointerStartY;

            if (Math.abs(dx) > 12 && Math.abs(dx) > Math.abs(dy)) {
                pointerMoved = true;
                slider.classList.add("is-swiping");
            }
        }

        function finishPointer(event, cancelled = false) {
            if (pointerStartX === null || pointerStartY === null) {
                restartAutoplay();
                return;
            }

            const dx = event.clientX - pointerStartX;
            const dy = event.clientY - pointerStartY;

            if (!cancelled) {
                const horizontalSwipe =
                    Math.abs(dx) >= swipeThreshold &&
                    Math.abs(dx) > Math.abs(dy) * 1.15;

                if (horizontalSwipe && !transitioning) {
                    if (dx < 0) {
                        showSlide(currentIndex + 1, {
                            direction: "next"
                        });
                    } else {
                        showSlide(currentIndex - 1, {
                            direction: "prev"
                        });
                    }

                    suppressClickUntil = Date.now() + 450;
                } else {
                    restartAutoplay();
                }
            } else {
                restartAutoplay();
            }

            pointerStartX = null;
            pointerStartY = null;
            pointerMoved = false;
            slider.classList.remove("is-swiping");
        }

        function bindWholeSlideOpen(slide) {
            /*
             * لا نسجل Click هنا. advertisements.js هو المسؤول الوحيد عن
             * التسجيل ثم التحويل. هنا فقط نمنع النقر العرضي بعد السحب،
             * ونحوّل Enter/Space إلى click طبيعي على الشريحة.
             */
            slide.addEventListener("click", event => {
                if (!slide.classList.contains("is-active")) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    return;
                }

                if (Date.now() < suppressClickUntil || pointerMoved) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                }
            });

            slide.addEventListener("keydown", event => {
                if (!slide.classList.contains("is-active")) return;
                if (event.target !== slide) return;
                if (event.key !== "Enter" && event.key !== " ") return;

                event.preventDefault();
                slide.click();
            });
        }

        previousButton?.addEventListener("click", goPrevious);
        nextButton?.addEventListener("click", goNext);

        dots.forEach(dot => {
            dot.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();

                if (transitioning) return;

                const targetIndex = Number.parseInt(
                    dot.dataset.searchAdDot || "0",
                    10
                );

                if (targetIndex === currentIndex) {
                    restartAutoplay();
                    return;
                }

                const forwardDistance =
                    (targetIndex - currentIndex + slides.length) % slides.length;
                const backwardDistance =
                    (currentIndex - targetIndex + slides.length) % slides.length;

                showSlide(targetIndex, {
                    direction:
                        forwardDistance <= backwardDistance
                            ? "next"
                            : "prev"
                });
            });
        });

        slides.forEach(bindWholeSlideOpen);

        slider.addEventListener("mouseenter", stopAutoplay);
        slider.addEventListener("mouseleave", restartAutoplay);
        slider.addEventListener("focusin", stopAutoplay);
        slider.addEventListener("focusout", restartAutoplay);
        slider.addEventListener("keydown", onKeyDown);
        slider.addEventListener("pointerdown", onPointerDown, { passive: true });
        slider.addEventListener("pointermove", onPointerMove, { passive: true });
        slider.addEventListener(
            "pointerup",
            event => finishPointer(event, false),
            { passive: true }
        );
        slider.addEventListener(
            "pointercancel",
            event => finishPointer(event, true),
            { passive: true }
        );

        document.addEventListener("visibilitychange", onVisibilityChange);

        updateUi();
        playSlideMedia(slides[currentIndex]);
        restartAutoplay();

        activeSearchAdCleanup = function cleanupSearchAdvertisementSlider() {
            destroyed = true;
            stopAutoplay();
            window.clearTimeout(transitionTimer);

            previousButton?.removeEventListener("click", goPrevious);
            nextButton?.removeEventListener("click", goNext);

            slider.removeEventListener("mouseenter", stopAutoplay);
            slider.removeEventListener("mouseleave", restartAutoplay);
            slider.removeEventListener("focusin", stopAutoplay);
            slider.removeEventListener("focusout", restartAutoplay);
            slider.removeEventListener("keydown", onKeyDown);
            slider.removeEventListener("pointerdown", onPointerDown);
            slider.removeEventListener("pointermove", onPointerMove);

            tiltCards.forEach(card => {
                card.removeEventListener("pointermove", onTiltMove);
                card.removeEventListener("pointerleave", onTiltLeave);
                resetTilt(card);
            });

            document.removeEventListener(
                "visibilitychange",
                onVisibilityChange
            );
        };
    }

    /* =========================================================================
       8. AJAX search
       ========================================================================= */
    window.performAjaxSearch = function performAjaxSearch() {
        const form = document.getElementById("searchForm");
        if (!form) return;

        // الفلاتر لا تحرك الصفحة ولا تغير موضع المستخدم.
        const fixedScrollX = window.scrollX;
        const fixedScrollY = window.scrollY;

        const url = new URL(form.action, window.location.origin);
        const formData = new FormData(form);
        const params = new URLSearchParams(formData);
        url.search = params.toString();

        const container = document.getElementById("resultsContainer");
        if (container) container.classList.add("is-loading");

        fetch(url.toString(), {
            method: "GET",
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                return response.text();
            })
            .then(function (html) {
                const parser = new DOMParser();
                const documentFragment = parser.parseFromString(html, "text/html");
                const newResults = documentFragment.getElementById("resultsContainer");
                const currentResults = document.getElementById("resultsContainer");

                if (newResults && currentResults) {
                    currentResults.innerHTML = newResults.innerHTML;
                    currentResults.classList.remove("is-loading");
                    initializeSearchResults(currentResults);
                }

                /*
                 * الإعلان أعلى الصفحة ثابت أثناء الفلترة.
                 * لا نستبدل searchHeroSlot عند تغيير الفئة/المدينة/البحث؛
                 * فقط نتائج البحث والعداد والفلاتر تتحدث.
                 */
                const newTracking = documentFragment.getElementById("customerAdvertisementTracking");
                const currentTracking = document.getElementById("customerAdvertisementTracking");

                if (newTracking && currentTracking) {
                    currentTracking.replaceWith(newTracking);
                } else if (newTracking && !currentTracking) {
                    document.body.appendChild(newTracking);
                } else if (!newTracking && currentTracking) {
                    currentTracking.remove();
                }

                const newCount = documentFragment.getElementById("resultsCount");
                const currentCount = document.getElementById("resultsCount");
                if (newCount && currentCount) {
                    currentCount.textContent = newCount.textContent || "0";
                }

                window.history.replaceState({}, "", url.toString());

                // حماية إضافية من أي قفزة ناتجة عن تحديث DOM.
                window.scrollTo({
                    left: fixedScrollX,
                    top: fixedScrollY,
                    behavior: "auto"
                });
            })
            .catch(function (error) {
                console.error("Search request failed:", error);
                if (container) container.classList.remove("is-loading");

                if (window.Swal) {
                    Swal.fire({
                        icon: "error",
                        title: "تعذر تحديث النتائج",
                        text: "حدث خطأ أثناء البحث. حاول مرة أخرى.",
                        confirmButtonText: "حسنًا",
                        confirmButtonColor: "#00A6DA"
                    });
                }
            });
    };

    let debounceTimer = null;

    window.debounceSearch = function debounceSearch() {
        if (debounceTimer) window.clearTimeout(debounceTimer);
        debounceTimer = window.setTimeout(function () {
            window.resetPageAndSearch();
        }, 430);
    };

    window.togglePriceFilter = function togglePriceFilter(clickedId, otherId) {
        const clicked = document.getElementById(clickedId);
        const other = document.getElementById(otherId);

        if (clicked && other && clicked.checked) {
            other.checked = false;
        }

        window.resetPageAndSearch();
    };

    window.changePage = function changePage(pageNumber) {
        const pageInput = document.getElementById("currentPage");
        if (!pageInput || pageNumber < 1) return;

        pageInput.value = pageNumber;
        window.performAjaxSearch();

        const anchor = document.getElementById("searchResultsAnchor");
        if (anchor) {
            anchor.scrollIntoView({
                behavior: reducedMotionQuery.matches ? "auto" : "smooth",
                block: "start"
            });
        }
    };

    window.resetPageAndSearch = function resetPageAndSearch() {
        const pageInput = document.getElementById("currentPage");
        if (pageInput) pageInput.value = "1";
        updateActiveFilterCount();
        window.performAjaxSearch();
    };

    window.selectCategory = function selectCategory(categoryId, element) {
        const categoryInput = document.getElementById("selectedCategoryId");
        if (categoryInput) categoryInput.value = categoryId || "";

        document.querySelectorAll(".category-item").forEach(function (item) {
            item.classList.remove("active-category");
        });

        if (element) {
            element.classList.add("active-category");
        }

        window.resetPageAndSearch();
    };

    window.resetAdvancedFilters = function resetAdvancedFilters() {
        const panel = document.getElementById("advancedSearchPanel");
        const pageInput = document.getElementById("currentPage");
        if (!panel) return;

        panel.querySelectorAll('input[type="checkbox"]').forEach(function (checkbox) {
            checkbox.checked = false;
        });

        if (pageInput) pageInput.value = "1";
        updateActiveFilterCount();
        window.performAjaxSearch();
    };

    window.resetSearchFilters = function resetSearchFilters() {
        const form = document.getElementById("searchForm");
        if (!form) return;

        const queryInput = form.querySelector('input[name="q"]');
        const citySelect = form.querySelector('select[name="cityId"]');
        const categoryInput = document.getElementById("selectedCategoryId");
        const pageInput = document.getElementById("currentPage");

        if (queryInput) queryInput.value = "";
        if (citySelect) citySelect.value = "";
        if (categoryInput) categoryInput.value = "";
        if (pageInput) pageInput.value = "1";

        form.querySelectorAll('input[type="checkbox"]').forEach(function (checkbox) {
            checkbox.checked = false;
        });

        document.querySelectorAll(".category-item").forEach(function (item) {
            item.classList.toggle("active-category", item.dataset.catId === "");
        });

        updateActiveFilterCount();
        window.performAjaxSearch();
    };

    /* =========================================================================
       9. Favorites
       ========================================================================= */
    window.toggleFavoriteVip = function toggleFavoriteVip(event, placeId, button) {
        if (event) {
            event.preventDefault();
            event.stopPropagation();
        }

        if (!button || button.disabled) return;

        const icon = button.querySelector("i");
        const originalState = button.classList.contains("active");

        button.disabled = true;
        applyFavoriteState(button, icon, !originalState);

        fetch("/Places/ToggleFavorite", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ PlaceId: Number.parseInt(placeId, 10) || 0 })
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const contentType = response.headers.get("content-type") || "";
                if (!contentType.includes("application/json")) {
                    throw new Error("RedirectedToLogin");
                }
                return response.json();
            })
            .then(function (result) {
                button.disabled = false;

                if (!result.success) {
                    applyFavoriteState(button, icon, originalState);

                    if (window.Swal) {
                        Swal.fire({
                            icon: "warning",
                            title: "تنبيه",
                            text: result.message || "تعذر تحديث المفضلة.",
                            confirmButtonText: "حسنًا",
                            confirmButtonColor: "#00A6DA"
                        });
                    }
                    return;
                }

                applyFavoriteState(button, icon, Boolean(result.isFavorited));

                if (window.Swal) {
                    Swal.fire({
                        toast: true,
                        position: "top-end",
                        icon: result.isFavorited ? "success" : "info",
                        title: result.message || "تم تحديث المفضلة.",
                        showConfirmButton: false,
                        timer: 2200
                    });
                }
            })
            .catch(function (error) {
                button.disabled = false;
                applyFavoriteState(button, icon, originalState);

                if (window.Swal && error.message === "RedirectedToLogin") {
                    Swal.fire({
                        icon: "info",
                        title: "تسجيل الدخول",
                        text: "يرجى تسجيل الدخول أولًا لاستخدام المفضلة.",
                        confirmButtonText: "تسجيل الدخول",
                        showCancelButton: true,
                        cancelButtonText: "لاحقًا",
                        confirmButtonColor: "#00A6DA"
                    }).then(function (result) {
                        if (result.isConfirmed) {
                            window.location.href = "/Account/Login";
                        }
                    });
                    return;
                }

                if (window.Swal) {
                    Swal.fire({
                        toast: true,
                        position: "top-end",
                        icon: "error",
                        title: "تعذر تحديث المفضلة الآن.",
                        showConfirmButton: false,
                        timer: 2400
                    });
                }
            });
    };

    function applyFavoriteState(button, icon, active) {
        if (!button || !icon) return;

        button.classList.toggle("active", active);
        button.setAttribute("aria-pressed", active ? "true" : "false");
        button.setAttribute("title", active ? "إزالة من المفضلة" : "إضافة للمفضلة");
        button.setAttribute("aria-label", active ? "إزالة من المفضلة" : "إضافة للمفضلة");
        icon.className = active ? "bi bi-heart-fill" : "bi bi-heart";

        if (active && finePointerQuery.matches && !reducedMotionQuery.matches) {
            icon.animate(
                [
                    { transform: "scale(1)" },
                    { transform: "scale(1.28)" },
                    { transform: "scale(1)" }
                ],
                { duration: 280, easing: "ease-out" }
            );
        }
    }
})();
