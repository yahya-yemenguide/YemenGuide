(function (window, document) {
    "use strict";

    const root = document.getElementById("adminFinancialsRoot");
    const isEnglish = (root?.dataset.uiLang || document.documentElement.lang || "ar")
        .toLowerCase()
        .startsWith("en");

    function t(ar, en) {
        return isEnglish ? en : ar;
    }

    function normalizeDigits(value) {
        return String(value ?? "")
            .replace(/[٠-٩]/g, function (digit) {
                return String("٠١٢٣٤٥٦٧٨٩".indexOf(digit));
            })
            .replace(/[۰-۹]/g, function (digit) {
                return String("۰۱۲۳۴۵۶۷۸۹".indexOf(digit));
            });
    }

    function digitsOnly(value) {
        return normalizeDigits(value).replace(/[^0-9]/g, "");
    }

    /* =========================================================
       Reports: compact collapsible filters
       ========================================================= */
    function initReports(scope) {
        const container = scope || document;
        const reportsRoot = container.matches?.("[data-reports-root]")
            ? container
            : container.querySelector?.("[data-reports-root]");

        if (!reportsRoot) return;

        const form = reportsRoot.querySelector("[data-report-filter-form]");
        if (!form) return;

        const head = form.querySelector(".afr-filter-card__head");
        if (!head) return;

        form.classList.add("is-collapsed");

        let toggle = head.querySelector("[data-report-filter-toggle]");
        if (!toggle) {
            toggle = document.createElement("button");
            toggle.type = "button";
            toggle.className = "afr-filter-toggle";
            toggle.setAttribute("data-report-filter-toggle", "");
            toggle.setAttribute("aria-expanded", "false");
            toggle.innerHTML = `
                <i class="bi bi-chevron-down" aria-hidden="true"></i>
                <span>${t("عرض الفلاتر", "Show filters")}</span>
            `;

            const clearButton = head.querySelector("[data-report-clear-filters]");
            if (clearButton) {
                head.insertBefore(toggle, clearButton);
            } else {
                head.appendChild(toggle);
            }
        }

        function syncToggle() {
            const collapsed = form.classList.contains("is-collapsed");
            toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
            const label = toggle.querySelector("span");
            if (label) {
                label.textContent = collapsed
                    ? t("عرض الفلاتر", "Show filters")
                    : t("إخفاء الفلاتر", "Hide filters");
            }
        }

        if (!toggle.dataset.boundPhase3) {
            toggle.dataset.boundPhase3 = "true";
            toggle.addEventListener("click", function () {
                form.classList.toggle("is-collapsed");
                syncToggle();
            });
        }

        syncToggle();
    }

    /* =========================================================
       Platform wallet account number: digits only
       Supports Arabic/Persian numerals and normalizes to 0-9.
       ========================================================= */
    function prepareAccountNumberInput(input) {
        if (!input || input.dataset.phase3NumericReady === "true") return;

        input.dataset.phase3NumericReady = "true";
        input.inputMode = "numeric";
        input.pattern = "[0-9]+";
        input.autocomplete = "off";
        input.spellcheck = false;
        input.setAttribute("aria-describedby", "platformAccountNumberNumericHint");

        let hint = input.parentElement?.querySelector("#platformAccountNumberNumericHint");
        if (!hint && input.parentElement) {
            hint = document.createElement("small");
            hint.id = "platformAccountNumberNumericHint";
            hint.className = "afm-field__hint afm-field__hint--numeric";
            hint.textContent = t("أرقام فقط بدون حروف أو رموز.", "Digits only; letters and symbols are not allowed.");
            input.insertAdjacentElement("afterend", hint);
        }

        input.addEventListener("beforeinput", function (event) {
            if (!event.data || event.inputType.startsWith("delete")) return;
            const normalized = normalizeDigits(event.data);
            if (/[^0-9]/.test(normalized)) {
                event.preventDefault();
            }
        });

        input.addEventListener("paste", function (event) {
            const clipboard = event.clipboardData?.getData("text") ?? "";
            const normalized = normalizeDigits(clipboard);
            const clean = digitsOnly(normalized);

            event.preventDefault();

            const start = input.selectionStart ?? input.value.length;
            const end = input.selectionEnd ?? input.value.length;
            const current = digitsOnly(input.value);
            const next = (current.slice(0, start) + clean + current.slice(end))
                .slice(0, Number(input.maxLength) > 0 ? Number(input.maxLength) : 120);

            input.value = next;
            const caret = Math.min(start + clean.length, next.length);
            input.setSelectionRange?.(caret, caret);
            input.dispatchEvent(new Event("input", { bubbles: true }));
        });

        input.addEventListener("input", function () {
            const clean = digitsOnly(input.value);
            if (input.value !== clean) {
                input.value = clean;
            }
            input.setCustomValidity("");
        });

        input.addEventListener("invalid", function () {
            if (!input.value) return;
            input.setCustomValidity(
                t(
                    "رقم الحساب يجب أن يحتوي على أرقام فقط.",
                    "The account number must contain digits only."
                )
            );
        });
    }

    function initManagement(scope) {
        const container = scope || document;
        const inputs = [];

        if (container.matches?.("[data-platform-wallet-number]")) {
            inputs.push(container);
        }

        container.querySelectorAll?.("[data-platform-wallet-number]")
            .forEach(function (input) {
                inputs.push(input);
            });

        inputs.forEach(prepareAccountNumberInput);

        const form = container.matches?.("[data-platform-wallet-form]")
            ? container
            : container.querySelector?.("[data-platform-wallet-form]");

        if (form && form.dataset.phase3NumericSubmit !== "true") {
            form.dataset.phase3NumericSubmit = "true";
            form.addEventListener("submit", function (event) {
                const input = form.querySelector("[data-platform-wallet-number]");
                if (!input) return;

                const raw = normalizeDigits(input.value);
                const clean = digitsOnly(raw);
                input.value = clean;

                if (!clean || clean !== raw) {
                    event.preventDefault();
                    input.setCustomValidity(
                        t(
                            "رقم الحساب يجب أن يحتوي على أرقام فقط بدون حروف أو رموز.",
                            "The account number must contain digits only, with no letters or symbols."
                        )
                    );
                    input.reportValidity();
                    input.focus();
                } else {
                    input.setCustomValidity("");
                }
            }, true);
        }
    }


    function installPhase3ToastTranslation() {
        if (!isEnglish) return;
        const Finance = window.AdminFinance;
        if (!Finance || Finance.__phase3ToastInstalled || typeof Finance.showToast !== "function") return;

        Finance.__phase3ToastInstalled = true;
        const original = Finance.showToast;
        const translations = new Map([
            ["رقم الحساب يجب أن يحتوي على أرقام فقط بدون حروف أو رموز أو مسافات.", "The account number must contain digits only, with no letters, symbols, or spaces."],
            ["رقم الحساب يجب أن يحتوي على أرقام فقط بدون حروف أو رموز.", "The account number must contain digits only, with no letters or symbols."],
            ["رقم الحساب يجب أن يحتوي على أرقام فقط.", "The account number must contain digits only."]
        ]);

        Finance.showToast = function (message, type, title, duration) {
            const translated = translations.get(String(message || "")) || message;
            return original.call(this, translated, type, title, duration);
        };
    }

    function initCurrentContent(scope) {
        const target = scope || document;
        initReports(target);
        initManagement(target);
        installPhase3ToastTranslation();
    }

    document.addEventListener("adminfinance:tabloaded", function (event) {
        initCurrentContent(event.detail?.host || document.getElementById("adminFinancialTabHost"));
    });

    document.addEventListener("adminfinance:managementloaded", function (event) {
        initManagement(event.detail?.host || document.querySelector("[data-finance-management-host]"));
    });

    document.addEventListener("focusin", function (event) {
        if (event.target?.matches?.("[data-platform-wallet-number]")) {
            prepareAccountNumberInput(event.target);
        }
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            initCurrentContent(document);
        }, { once: true });
    } else {
        initCurrentContent(document);
    }
})(window, document);
