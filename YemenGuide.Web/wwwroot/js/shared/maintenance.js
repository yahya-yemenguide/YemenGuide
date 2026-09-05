(() => {
    "use strict";

    const root = document.querySelector("[data-maintenance-root]");

    if (!root) {
        return;
    }

    const pad = (value) =>
        String(Math.max(0, value)).padStart(2, "0");

    function parseDate(value) {
        if (!value) {
            return null;
        }

        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    function getRemaining(endDate) {
        const total = endDate.getTime() - Date.now();

        if (total <= 0) {
            return {
                total: 0,
                days: 0,
                hours: 0,
                minutes: 0,
                seconds: 0
            };
        }

        const totalSeconds = Math.floor(total / 1000);

        return {
            total,
            days: Math.floor(totalSeconds / 86400),
            hours: Math.floor((totalSeconds % 86400) / 3600),
            minutes: Math.floor((totalSeconds % 3600) / 60),
            seconds: totalSeconds % 60
        };
    }

    function initCountdown() {
        const countdown =
            document.querySelector("[data-countdown]");

        if (!countdown) {
            return;
        }

        const endDate =
            parseDate(countdown.dataset.endUtc);

        if (!endDate) {
            return;
        }

        const days =
            countdown.querySelector("[data-days]");

        const hours =
            countdown.querySelector("[data-hours]");

        const minutes =
            countdown.querySelector("[data-minutes]");

        const seconds =
            countdown.querySelector("[data-seconds]");

        const render = () => {
            const remaining = getRemaining(endDate);

            if (days) {
                days.textContent = pad(remaining.days);
            }

            if (hours) {
                hours.textContent = pad(remaining.hours);
            }

            if (minutes) {
                minutes.textContent = pad(remaining.minutes);
            }

            if (seconds) {
                seconds.textContent = pad(remaining.seconds);
            }

            if (remaining.total <= 0) {
                window.setTimeout(() => {
                    window.location.reload();
                }, 1800);

                return false;
            }

            return true;
        };

        if (!render()) {
            return;
        }

        const timer = window.setInterval(() => {
            if (!render()) {
                window.clearInterval(timer);
            }
        }, 1000);
    }

    function initRefresh() {
        document
            .querySelectorAll("[data-maintenance-refresh]")
            .forEach((button) => {
                button.addEventListener("click", () => {
                    if (button.classList.contains("is-loading")) {
                        return;
                    }

                    button.classList.add("is-loading");
                    button.setAttribute("aria-busy", "true");

                    const label = button.querySelector("span");

                    if (label) {
                        label.textContent = "جاري التحقق...";
                    }

                    window.setTimeout(() => {
                        window.location.reload();
                    }, 520);
                });
            });
    }

    function initContactPanel() {
        const toggle =
            document.querySelector("[data-contact-toggle]");

        const panel =
            document.querySelector("[data-contact-panel]");

        if (!toggle || !panel) {
            return;
        }

        toggle.addEventListener("click", () => {
            const willOpen = panel.hidden;

            panel.hidden = !willOpen;

            toggle.setAttribute(
                "aria-expanded",
                willOpen ? "true" : "false"
            );
        });
    }

    function rgbToHsl(r, g, b) {
        r /= 255;
        g /= 255;
        b /= 255;

        const max = Math.max(r, g, b);
        const min = Math.min(r, g, b);

        let h = 0;
        let s = 0;
        const l = (max + min) / 2;

        if (max !== min) {
            const d = max - min;

            s =
                l > 0.5
                    ? d / (2 - max - min)
                    : d / (max + min);

            switch (max) {
                case r:
                    h = (g - b) / d + (g < b ? 6 : 0);
                    break;
                case g:
                    h = (b - r) / d + 2;
                    break;
                default:
                    h = (r - g) / d + 4;
                    break;
            }

            h /= 6;
        }

        return {
            h: h * 360,
            s: s * 100,
            l: l * 100
        };
    }

    function hslToRgb(h, s, l) {
        h /= 360;
        s /= 100;
        l /= 100;

        if (s === 0) {
            const gray = Math.round(l * 255);
            return [gray, gray, gray];
        }

        const hue2rgb = (p, q, t) => {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6) return p + (q - p) * 6 * t;
            if (t < 1 / 2) return q;
            if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
            return p;
        };

        const q =
            l < 0.5
                ? l * (1 + s)
                : l + s - l * s;

        const p = 2 * l - q;

        return [
            Math.round(hue2rgb(p, q, h + 1 / 3) * 255),
            Math.round(hue2rgb(p, q, h) * 255),
            Math.round(hue2rgb(p, q, h - 1 / 3) * 255)
        ];
    }

    function chooseLogoColor(image) {
        const canvas = document.createElement("canvas");
        const context = canvas.getContext("2d", {
            willReadFrequently: true
        });

        if (!context) {
            return null;
        }

        canvas.width = 36;
        canvas.height = 36;

        try {
            context.drawImage(image, 0, 0, 36, 36);

            const pixels =
                context.getImageData(0, 0, 36, 36).data;

            let best = null;
            let bestScore = -1;

            for (let i = 0; i < pixels.length; i += 16) {
                const r = pixels[i];
                const g = pixels[i + 1];
                const b = pixels[i + 2];
                const a = pixels[i + 3];

                if (a < 90) {
                    continue;
                }

                const max = Math.max(r, g, b);
                const min = Math.min(r, g, b);
                const saturation = max - min;
                const brightness = (r + g + b) / 3;

                if (brightness > 238 || brightness < 28) {
                    continue;
                }

                const score =
                    saturation * 1.25 +
                    Math.min(brightness, 180) * 0.22;

                if (score > bestScore) {
                    bestScore = score;
                    best = [r, g, b];
                }
            }

            return best;
        } catch {
            return null;
        }
    }

    function applyDynamicBrandColor(rgb) {
        if (!rgb) {
            return;
        }

        const [r, g, b] = rgb;
        const hsl = rgbToHsl(r, g, b);

        const safeSaturation =
            Math.max(58, Math.min(86, hsl.s));

        const safeLightness =
            Math.max(48, Math.min(62, hsl.l));

        const brand =
            hslToRgb(
                hsl.h,
                safeSaturation,
                safeLightness
            );

        const accent =
            hslToRgb(
                (hsl.h + 42) % 360,
                Math.max(62, safeSaturation - 6),
                Math.min(64, safeLightness + 4)
            );

        document.documentElement.style.setProperty(
            "--yg-brand-rgb",
            brand.join(", ")
        );

        document.documentElement.style.setProperty(
            "--yg-accent-rgb",
            accent.join(", ")
        );
    }

    function initDynamicBrand() {
        const logoSrc =
            root.dataset.logoSrc;

        if (!logoSrc) {
            return;
        }

        const image = new Image();

        image.decoding = "async";
        image.src = logoSrc;

        image.addEventListener("load", () => {
            applyDynamicBrandColor(
                chooseLogoColor(image)
            );
        });
    }

    function formatInlineRemaining(remaining) {
        if (remaining.total <= 0) {
            return "انتهى الوقت المتوقع";
        }

        const parts = [];

        if (remaining.days > 0) {
            parts.push(
                `${remaining.days} يوم`
            );
        }

        if (remaining.hours > 0 ||
            remaining.days > 0) {
            parts.push(
                `${remaining.hours} ساعة`
            );
        }

        parts.push(
            `${remaining.minutes} دقيقة`
        );

        parts.push(
            `${remaining.seconds} ثانية`
        );

        return parts.join(" • ");
    }

    function initInlineCountdowns() {
        document
            .querySelectorAll(
                "[data-maintenance-countdown-inline]"
            )
            .forEach((element) => {
                const endDate =
                    parseDate(
                        element.dataset.endUtc
                    );

                if (!endDate) {
                    return;
                }

                const render = () => {
                    const remaining =
                        getRemaining(
                            endDate
                        );

                    element.textContent =
                        formatInlineRemaining(
                            remaining
                        );

                    if (remaining.total <= 0) {
                        window.setTimeout(() => {
                            window.location.reload();
                        }, 1800);

                        return false;
                    }

                    return true;
                };

                if (!render()) {
                    return;
                }

                const timer =
                    window.setInterval(() => {
                        if (!render()) {
                            window.clearInterval(
                                timer
                            );
                        }
                    }, 1000);
            });
    }

    initCountdown();
    initInlineCountdowns();
    initRefresh();
    initContactPanel();
    initDynamicBrand();
})();
