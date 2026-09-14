(() => {
    "use strict";

    const config = window.YemenGuideAdminI18n;
    if (!config) return;

    const isEnglish = config.language === "en";
    const exact = config.exact || {};
    const strings = config.strings || {};
    // لا نترجم أي محتوى ديناميكي من عندنا.
    // الترجمة فقط للنصوص المعروفة صراحةً، أو لحقول DB التي لها English field فعلي.

    const normalize = value =>
        String(value ?? "")
            .replace(/\s+/g, " ")
            .trim();

    const toLatinDigits = value =>
        String(value ?? "")
            .replace(/[٠-٩]/g, digit =>
                String("٠١٢٣٤٥٦٧٨٩".indexOf(digit)))
            .replace(/[۰-۹]/g, digit =>
                String("۰۱۲۳۴۵۶۷۸۹".indexOf(digit)));

    const orderedEntries = Object.entries(exact)
        .filter(([source, target]) =>
            Boolean(source) &&
            Boolean(target) &&
            source !== target)
        .sort((a, b) => b[0].length - a[0].length);

    const normalizedExact = Object.fromEntries(
        orderedEntries.map(([source, target]) => [
            normalize(source),
            String(target)
        ])
    );

    const get = (key, fallback = "") => {
        const value = strings[key];
        return value === undefined || value === null || value === ""
            ? String(fallback ?? "")
            : String(value);
    };

    const normalizeCurrencySymbols = value =>
        String(value ?? "")
            .replace(/\u0631\.?\s*\u064A/g, get("Admin_Currency_YER", "YER"))
            .replace(/\u0631\.?\s*\u0633/g, get("Admin_Currency_SAR", "SAR"))
            .replace(/\u062F\.?\s*\u0625/g, get("Admin_Currency_AED", "AED"));

    const preserveOuterWhitespace = (original, translated) => {
        const source = String(original ?? "");
        const leading = source.match(/^\s*/)?.[0] || "";
        const trailing = source.match(/\s*$/)?.[0] || "";
        return `${leading}${translated}${trailing}`;
    };

    const translateValue = value => {
        const original = String(value ?? "");

        if (!isEnglish) {
            return original;
        }

        const normalized = normalize(original);

        if (!normalized) {
            return original;
        }

        // 1) ترجمة صريحة فقط:
        //    نصوص النظام + القيم القادمة من حقول إنجليزية حقيقية في DB.
        if (Object.prototype.hasOwnProperty.call(
            normalizedExact,
            normalized
        )) {
            const translated =
                toLatinDigits(normalizedExact[normalized]);

            return preserveOuterWhitespace(
                original,
                translated
            );
        }

        // 2) رمز العملة ليس نصًا كتبه المستخدم.
        const currencyNormalized =
            normalizeCurrencySymbols(original);

        if (currencyNormalized !== original) {
            return toLatinDigits(currencyNormalized);
        }

        // 3) أي محتوى عربي غير معروف نتركه كما هو تمامًا.
        //    اسم منشأة / مالك / عميل / رسالة مستخدم ... إلخ.
        return original;
    };

    const translateJsonArray = rawValue => {
        const raw = String(rawValue ?? "");

        if (!isEnglish || !raw) return raw;

        try {
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return translateValue(raw);
            }

            return JSON.stringify(
                parsed.map(item =>
                    typeof item === "string"
                        ? translateValue(item)
                        : item)
            );
        } catch {
            return translateValue(raw);
        }
    };

    const translatedAttributes = [
        "placeholder",
        "title",
        "aria-label",
        "aria-description",
        "aria-valuetext",
        "data-bs-original-title",
        "alt"
    ];

    const jsonArrayDataAttributes = [
        "data-booking-labels",
        "data-revenue-labels",
        "data-place-status-labels",
        "data-finance-labels",
        "data-category-labels"
    ];

    const plainDataAttributes = [
        "data-greeting-morning",
        "data-greeting-afternoon",
        "data-greeting-evening",
        "data-booking-created-name",
        "data-booking-starting-name",
        "data-revenue-name",
        "data-currency-symbol",
        "data-category-name"
    ];

    const translateElementAttributes = element => {
        if (!isEnglish || !(element instanceof Element)) return;

        translatedAttributes.forEach(attributeName => {
            if (!element.hasAttribute(attributeName)) return;

            const current = element.getAttribute(attributeName);
            const next = translateValue(current);

            if (next !== current) {
                element.setAttribute(attributeName, next);
            }
        });

        jsonArrayDataAttributes.forEach(attributeName => {
            if (!element.hasAttribute(attributeName)) return;

            const current = element.getAttribute(attributeName);
            const next = translateJsonArray(current);

            if (next !== current) {
                element.setAttribute(attributeName, next);
            }
        });

        plainDataAttributes.forEach(attributeName => {
            if (!element.hasAttribute(attributeName)) return;

            const current = element.getAttribute(attributeName);
            const next = translateValue(current);

            if (next !== current) {
                element.setAttribute(attributeName, next);
            }
        });

        if (
            element instanceof HTMLInputElement &&
            ["button", "submit", "reset"].includes(
                String(element.type || "").toLowerCase()
            )
        ) {
            const next = translateValue(element.value);
            if (next !== element.value) {
                element.value = next;
            }
        }
    };

    const shouldSkipTextNode = node => {
        const parent = node?.parentElement;
        if (!parent) return true;

        return Boolean(
            parent.closest(
                "script,style,noscript,textarea," +
                "[data-admin-i18n-skip]"
            )
        );
    };

    const translateTextNode = node => {
        if (
            !isEnglish ||
            !node ||
            node.nodeType !== Node.TEXT_NODE ||
            shouldSkipTextNode(node)
        ) {
            return;
        }

        const next = translateValue(node.nodeValue);

        if (next !== node.nodeValue) {
            node.nodeValue = next;
        }
    };

    const translateTree = root => {
        if (!isEnglish || !root) return;

        if (root.nodeType === Node.TEXT_NODE) {
            translateTextNode(root);
            return;
        }

        if (
            !(root instanceof Element) &&
            root !== document.body &&
            root !== document.documentElement
        ) {
            return;
        }

        if (root instanceof Element) {
            translateElementAttributes(root);
        }

        const walker = document.createTreeWalker(
            root,
            NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT
        );

        let current = walker.currentNode;

        while (current) {
            if (current.nodeType === Node.TEXT_NODE) {
                translateTextNode(current);
            } else if (current instanceof Element) {
                translateElementAttributes(current);
            }

            current = walker.nextNode();
        }
    };

    const prepareEnglishDashboardData = () => {
        if (!isEnglish) return;

        const dataNode =
            document.getElementById("adminDashboardData");

        if (!dataNode) return;

        const code =
            String(dataNode.dataset.currencyCode || "")
                .toUpperCase();

        if (["YER", "SAR", "AED", "USD"].includes(code)) {
            dataNode.dataset.currencySymbol = code;
        }

        jsonArrayDataAttributes.forEach(attributeName => {
            if (!dataNode.hasAttribute(attributeName)) return;

            const current =
                dataNode.getAttribute(attributeName);

            dataNode.setAttribute(
                attributeName,
                translateJsonArray(current)
            );
        });

        plainDataAttributes.forEach(attributeName => {
            if (!dataNode.hasAttribute(attributeName)) return;

            const current =
                dataNode.getAttribute(attributeName);

            dataNode.setAttribute(
                attributeName,
                translateValue(current)
            );
        });
    };

    const translateDocumentTitle = () => {
        if (!isEnglish) return;
        document.title = translateValue(document.title);
    };

    const installSweetAlertBridge = () => {
        if (
            !isEnglish ||
            !window.Swal ||
            typeof window.Swal.fire !== "function" ||
            window.Swal.__adminI18nWrapped
        ) {
            return;
        }

        const originalFire =
            window.Swal.fire.bind(window.Swal);

        window.Swal.fire = (...args) => {
            if (
                args.length === 1 &&
                args[0] &&
                typeof args[0] === "object" &&
                !Array.isArray(args[0])
            ) {
                const options = { ...args[0] };

                [
                    "title",
                    "text",
                    "confirmButtonText",
                    "cancelButtonText",
                    "denyButtonText",
                    "footer"
                ].forEach(key => {
                    if (typeof options[key] === "string") {
                        options[key] =
                            translateValue(options[key]);
                    }
                });

                return originalFire(options);
            }

            const translatedArgs =
                args.map(arg =>
                    typeof arg === "string"
                        ? translateValue(arg)
                        : arg
                );

            return originalFire(...translatedArgs);
        };

        window.Swal.__adminI18nWrapped = true;
    };

    const runImmediate = () => {
        if (!isEnglish) return;

        document.documentElement.lang = "en";
        document.documentElement.dir = "ltr";

        if (document.body) {
            document.body.dataset.language = "en";
            document.body.dataset.direction = "ltr";
        }

        /*
         * This runs immediately because this file is loaded before
         * dashboard-overview.js. It guarantees the chart data is already
         * English before Chart.js reads it.
         */
        prepareEnglishDashboardData();
        translateDocumentTitle();

        if (document.body) {
            translateTree(document.body);
        }

        installSweetAlertBridge();
    };

    runImmediate();

    const observer = new MutationObserver(mutations => {
        if (!isEnglish) return;

        for (const mutation of mutations) {
            if (mutation.type === "characterData") {
                translateTextNode(mutation.target);
                continue;
            }

            if (mutation.type === "attributes") {
                translateElementAttributes(
                    mutation.target
                );
                continue;
            }

            mutation.addedNodes.forEach(node =>
                translateTree(node));
        }

        installSweetAlertBridge();
    });

    const startObserver = () => {
        if (!isEnglish || !document.body) return;

        observer.observe(document.body, {
            childList: true,
            subtree: true,
            characterData: true,
            attributes: true,
            attributeFilter: [
                ...translatedAttributes,
                ...jsonArrayDataAttributes,
                ...plainDataAttributes
            ]
        });
    };

    const finalSweep = () => {
        if (!isEnglish) return;

        prepareEnglishDashboardData();
        translateDocumentTitle();

        if (document.body) {
            translateTree(document.body);
        }

        installSweetAlertBridge();
    };

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            () => {
                finalSweep();
                startObserver();

                requestAnimationFrame(finalSweep);
                window.setTimeout(finalSweep, 80);
                window.setTimeout(finalSweep, 250);
            },
            { once: true }
        );
    } else {
        finalSweep();
        startObserver();

        requestAnimationFrame(finalSweep);
        window.setTimeout(finalSweep, 80);
        window.setTimeout(finalSweep, 250);
    }

    window.AdminI18n = Object.freeze({
        language: config.language,
        locale:
            config.locale ||
            (isEnglish ? "en-US" : "ar-YE"),
        direction:
            config.direction ||
            (isEnglish ? "ltr" : "rtl"),
        get,
        t: translateValue,
        translateTree,
        sweep: finalSweep
    });
})();
