/**
 * =============================================================================
 * YEMENGUIDE — CUSTOMER FAVORITES / LUXURY 3D V1
 * - حذف حقيقي من المفضلة بدون فتح البطاقة
 * - تحديث العداد فوراً
 * - اختفاء البطاقة بسلاسة
 * - Smart Gallery هادئة مثل البحث
 * - 3D / Spotlight للماوس فقط، بدون حركات لمس مزعجة
 * =============================================================================
 */

(function () {
    "use strict";

    const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
    const finePointerQuery = window.matchMedia("(hover: hover) and (pointer: fine)");

    document.addEventListener("DOMContentLoaded", function () {
        initializeFavoritesPage(document);
        bindFavoriteRemoval();
        updateFavoritesCount();
    });

    function initializeFavoritesPage(root) {
        initCardReveal(root);
        initFavoriteCardDepth(root);
        initSmartFavoriteGalleries(root);
    }

    /* =========================================================================
       1. دخول البطاقات بهدوء
       ========================================================================= */
    function initCardReveal(root) {
        const stages = root.querySelectorAll
            ? root.querySelectorAll("[data-favorite-stage]")
            : [];

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
        }, {
            threshold: 0.10,
            rootMargin: "50px 0px"
        });

        stages.forEach(stage => observer.observe(stage));
    }

    /* =========================================================================
       2. Depth / Spotlight للماوس فقط
       لا نستخدم click أو pointerdown حتى تبقى الروابط والقلب طبيعيين.
       ========================================================================= */
    function initFavoriteCardDepth(root) {
        const cards = root.querySelectorAll
            ? root.querySelectorAll("[data-favorite-card]")
            : [];

        cards.forEach(function (card) {
            if (card.dataset.favoriteDepthInitialized === "true") return;
            card.dataset.favoriteDepthInitialized = "true";

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
                if (!rect.width || !rect.height) return;

                const px = Math.min(1, Math.max(0,
                    (event.clientX - rect.left) / rect.width));
                const py = Math.min(1, Math.max(0,
                    (event.clientY - rect.top) / rect.height));

                if (frameId) cancelAnimationFrame(frameId);

                frameId = requestAnimationFrame(function () {
                    card.style.setProperty(
                        "--featured-mx",
                        `${(px * 100).toFixed(1)}%`);
                    card.style.setProperty(
                        "--featured-my",
                        `${(py * 100).toFixed(1)}%`);

                    if (mainImage) {
                        const x = (0.5 - px) * 7.0;
                        const y = (0.5 - py) * 4.5;
                        mainImage.style.transform =
                            `scale(1.055) translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, 0)`;
                    }
                });
            });

            card.addEventListener("pointerleave", reset);
            card.addEventListener("pointercancel", reset);
        });
    }

    /* =========================================================================
       3. Smart Gallery
       صورة واحدة تتبدل كل مرة. لا Click / Hover / Touch يغير المصغرات.
       ========================================================================= */
    function initSmartFavoriteGalleries(root) {
        const cards = root.querySelectorAll
            ? root.querySelectorAll("[data-favorite-card]")
            : [];

        cards.forEach(function (card, cardIndex) {
            if (card.dataset.favoriteGalleryInitialized === "true") return;
            card.dataset.favoriteGalleryInitialized = "true";

            const gallery = card.querySelector(".favorites-smart-gallery");
            if (!gallery) return;

            let imagesList = [];

            try {
                imagesList = JSON.parse(
                    gallery.getAttribute("data-images") || "[]") || [];
            } catch {
                imagesList = [];
            }

            imagesList = imagesList.filter(Boolean);

            const imageContainers = Array.from(
                gallery.querySelectorAll(".favorites-thumb"));

            if (
                reducedMotionQuery.matches ||
                imagesList.length <= 3 ||
                imageContainers.length === 0
            ) {
                return;
            }

            let intervalId = null;
            let startTimeoutId = null;
            let currentImageIndex = 3;
            let currentBoxIndex = 0;
            let isVisible = true;

            const intervalDelay = finePointerQuery.matches ? 3600 : 5000;
            const fadeDuration = finePointerQuery.matches ? 700 : 820;
            const initialDelay = 950 + ((cardIndex % 3) * 420);

            const changeNextImage = function () {
                if (!isVisible || document.hidden || !imagesList.length) return;

                const container = imageContainers[currentBoxIndex];
                const currentImage = container
                    ? container.querySelector("img.magic-image")
                    : null;

                if (!currentImage) {
                    currentBoxIndex =
                        (currentBoxIndex + 1) % imageContainers.length;
                    return;
                }

                let attempts = 0;
                let nextSource = imagesList[
                    currentImageIndex % imagesList.length];

                while (
                    nextSource &&
                    currentImage.getAttribute("src") === nextSource &&
                    attempts < imagesList.length
                ) {
                    currentImageIndex =
                        (currentImageIndex + 1) % imagesList.length;
                    nextSource = imagesList[currentImageIndex];
                    attempts++;
                }

                if (!nextSource) return;

                container.classList.add("is-gallery-changing");
                crossFadeImage(
                    container,
                    currentImage,
                    nextSource,
                    fadeDuration);

                window.setTimeout(function () {
                    container.classList.remove("is-gallery-changing");
                }, fadeDuration + 100);

                currentImageIndex =
                    (currentImageIndex + 1) % imagesList.length;
                currentBoxIndex =
                    (currentBoxIndex + 1) % imageContainers.length;
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

                if (!isVisible || document.hidden || reducedMotionQuery.matches) {
                    return;
                }

                startTimeoutId = window.setTimeout(function () {
                    changeNextImage();
                    intervalId = window.setInterval(
                        changeNextImage,
                        intervalDelay);
                }, initialDelay);
            };

            if ("IntersectionObserver" in window) {
                const observer = new IntersectionObserver(function (entries) {
                    entries.forEach(function (entry) {
                        if (entry.target !== card) return;

                        isVisible =
                            entry.isIntersecting &&
                            entry.intersectionRatio > 0.18;

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
       4. إزالة من المفضلة
       Capture listener حتى لا يمر الضغط إلى أي رابط أسفل القلب.
       ========================================================================= */
    function bindFavoriteRemoval() {
        document.addEventListener("click", function (event) {
            const button = event.target.closest?.("[data-remove-favorite]");
            if (!button) return;

            event.preventDefault();
            event.stopPropagation();
            event.stopImmediatePropagation();

            const placeId = Number.parseInt(
                button.dataset.placeId || "0",
                10);

            if (!placeId || button.disabled) return;

            removeFavorite(placeId, button);
        }, true);
    }

    async function removeFavorite(placeId, button) {
        const wrapper = document.getElementById(`fav-card-${placeId}`);
        const icon = button.querySelector("i");

        button.disabled = true;
        button.classList.add("is-pulsing");

        window.setTimeout(function () {
            button.classList.remove("is-pulsing");
        }, 420);

        try {
            const response = await fetch("/Places/ToggleFavorite", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    PlaceId: Number.parseInt(placeId, 10)
                })
            });

            const contentType = response.headers.get("content-type") || "";

            if (!response.ok) {
                throw new Error(`Http${response.status}`);
            }

            if (!contentType.includes("application/json")) {
                throw new Error("RedirectedToLogin");
            }

            const result = await response.json();

            if (!result || !result.success) {
                throw new Error(result?.message || "FavoriteRequestFailed");
            }

            /* ToggleFavorite المفروض يرجع false بعد الحذف من هذه الصفحة. */
            if (result.isFavorited === true) {
                button.disabled = false;
                showToast(
                    "info",
                    "المكان بقي في المفضلة؛ أعد المحاولة إذا أردت إزالته.");
                return;
            }

            if (icon) {
                icon.className = "bi bi-heart";
            }

            animateAndRemoveCard(wrapper);
            showToast("info", result.message || "تمت الإزالة من المفضلة");
        } catch (error) {
            button.disabled = false;

            if (icon) {
                icon.className = "bi bi-heart-fill";
            }

            console.error("Favorite removal error:", error);

            if (error.message === "RedirectedToLogin") {
                showLoginExpiredDialog();
                return;
            }

            showToast(
                "error",
                error.message &&
                    error.message !== "FavoriteRequestFailed" &&
                    !error.message.startsWith("Http")
                    ? error.message
                    : "تعذر إزالة المكان الآن، حاول مرة أخرى.");
        }
    }

    function animateAndRemoveCard(wrapper) {
        if (!wrapper) {
            updateFavoritesCount();
            return;
        }

        wrapper.classList.add("is-removing");

        const firstDelay = reducedMotionQuery.matches ? 10 : 390;
        const secondDelay = reducedMotionQuery.matches ? 10 : 420;

        window.setTimeout(function () {
            wrapper.style.maxHeight = `${wrapper.offsetHeight}px`;

            requestAnimationFrame(function () {
                wrapper.classList.add("is-collapsing");
            });

            window.setTimeout(function () {
                wrapper.remove();
                updateFavoritesCount();
            }, secondDelay);
        }, firstDelay);
    }

    /* =========================================================================
       5. العداد والحالة الفارغة
       ========================================================================= */
    function updateFavoritesCount() {
        const count = document.querySelectorAll(
            ".favorite-card-wrapper:not(.is-removing):not(.is-collapsing)"
        ).length;

        const countElement = document.getElementById("favoritesCount");
        const labelElement = document.getElementById("favoritesCountLabel");
        const grid = document.getElementById("favoritesGrid");
        const heading = document.getElementById("favoritesSectionHeading");
        const emptyState = document.getElementById("emptyFavState");

        if (countElement) {
            animateNumber(countElement, count);
        }

        if (labelElement) {
            labelElement.textContent = count === 1
                ? "مكان محفوظ"
                : "أماكن محفوظة";
        }

        if (count <= 0) {
            grid?.classList.add("d-none");
            heading?.classList.add("d-none");

            if (emptyState) {
                emptyState.classList.remove("d-none");
                emptyState.style.opacity = "0";
                emptyState.style.transform = "translateY(18px)";

                requestAnimationFrame(function () {
                    emptyState.style.transition =
                        reducedMotionQuery.matches
                            ? "none"
                            : "opacity .45s ease, transform .45s ease";
                    emptyState.style.opacity = "1";
                    emptyState.style.transform = "translateY(0)";
                });
            }
        } else {
            grid?.classList.remove("d-none");
            heading?.classList.remove("d-none");
            emptyState?.classList.add("d-none");
        }
    }

    function animateNumber(element, value) {
        const current = Number.parseInt(element.textContent || "0", 10) || 0;

        if (current === value || reducedMotionQuery.matches) {
            element.textContent = String(value);
            return;
        }

        element.animate(
            [
                { transform: "translateY(0)", opacity: 1 },
                { transform: "translateY(-6px)", opacity: .25 },
                { transform: "translateY(5px)", opacity: .25 },
                { transform: "translateY(0)", opacity: 1 }
            ],
            {
                duration: 310,
                easing: "ease"
            }
        );

        window.setTimeout(function () {
            element.textContent = String(value);
        }, 135);
    }

    /* =========================================================================
       6. رسائل
       ========================================================================= */
    function showToast(icon, message) {
        if (typeof Swal !== "undefined") {
            Swal.fire({
                toast: true,
                position: "top-end",
                icon: icon,
                title: message,
                showConfirmButton: false,
                timer: 2600,
                timerProgressBar: true
            });
            return;
        }

        console.log(message);
    }

    function showLoginExpiredDialog() {
        if (typeof Swal === "undefined") {
            window.location.href = "/Account/Login";
            return;
        }

        Swal.fire({
            icon: "info",
            title: "تسجيل الدخول",
            text: "انتهت جلستك، سجّل الدخول مرة أخرى للمتابعة.",
            confirmButtonText: "تسجيل الدخول",
            confirmButtonColor: "#00A6DA"
        }).then(function (result) {
            if (result.isConfirmed) {
                window.location.href = "/Account/Login";
            }
        });
    }
})();
