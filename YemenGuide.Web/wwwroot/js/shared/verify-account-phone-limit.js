/**
 * YemenGuide - Verify Account Yemen phone length guard.
 * Visual/input guard only. Existing intl-tel-input validation remains the source of validity.
 */
(function (window, document) {
    "use strict";

    function start() {
        const input = document.getElementById("AdminPhoneDisplay");
        if (!input || !window.intlTelInput) return;

        function getInstance() {
            try {
                return typeof window.intlTelInput.getInstance === "function"
                    ? window.intlTelInput.getInstance(input)
                    : null;
            } catch {
                return null;
            }
        }

        function isYemenSelected() {
            const instance = getInstance();
            const iso2 = instance?.getSelectedCountryData?.()?.iso2;
            if (iso2) return String(iso2).toLowerCase() === "ye";

            const selectedCountry =
                input.closest(".iti")?.querySelector(".iti__selected-country");
            const title = selectedCountry?.getAttribute("title") || "";
            return /\+967\b/.test(title);
        }

        function digitsOnly(value) {
            return String(value || "").replace(/\D/g, "");
        }

        function syncLimit() {
            if (isYemenSelected()) {
                input.setAttribute("maxlength", "9");
                input.setAttribute("inputmode", "numeric");
                input.setAttribute("pattern", "[0-9]*");

                const clean = digitsOnly(input.value).slice(0, 9);
                if (input.value !== clean) {
                    input.value = clean;
                }
            } else {
                input.removeAttribute("maxlength");
                input.setAttribute("inputmode", "tel");
                input.removeAttribute("pattern");
            }
        }

        // Capture phase: clamp before the existing verify-account.js input validator runs.
        input.addEventListener("input", syncLimit, true);
        input.addEventListener("paste", function () {
            window.setTimeout(syncLimit, 0);
        }, true);
        input.addEventListener("countrychange", function () {
            window.setTimeout(syncLimit, 0);
        }, true);

        input.addEventListener("beforeinput", function (event) {
            if (!isYemenSelected()) return;
            if (!event.data || !/\d/.test(event.data)) return;

            const start = input.selectionStart ?? input.value.length;
            const end = input.selectionEnd ?? input.value.length;
            const selectedLength = Math.max(0, end - start);
            const currentDigits = digitsOnly(input.value).length;

            if (currentDigits - selectedLength >= 9) {
                event.preventDefault();
            }
        }, true);

        // intl-tel-input creates/updates its state during the same DOM-ready cycle.
        window.setTimeout(syncLimit, 0);
        window.setTimeout(syncLimit, 250);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})(window, document);
