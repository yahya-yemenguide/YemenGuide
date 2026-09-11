(() => {
    "use strict";

    const config = window.YemenGuideHomeI18n;

    if (!config || config.language !== "en") {
        return;
    }

    const body = document.body;

    if (
        !body ||
        body.dataset.customerController !== "Home" ||
        body.dataset.customerAction !== "Index"
    ) {
        return;
    }

    const exact = config.exact || {};
    const formats = config.formats || {};
    const englishLocale = config.locale || "en-US";

    const format = (template, ...args) =>
        String(template || "").replace(
            /\{(\d+)\}/g,
            (_, index) => args[Number(index)] ?? ""
        );

    const toLatinDigits = (value) =>
        String(value ?? "")
            .replace(/[٠-٩]/g, digit =>
                String("٠١٢٣٤٥٦٧٨٩".indexOf(digit))
            )
            .replace(/[۰-۹]/g, digit =>
                String("۰۱۲۳۴۵۶۷۸۹".indexOf(digit))
            );

    const normalizeCurrencySymbols = (value) =>
        String(value ?? "")
            .replace(/ر\.?\s*ي/g, "YER")
            .replace(/ر\.?\s*س/g, "SAR")
            .replace(/د\.?\s*إ/g, "AED");

    const preserveOuterWhitespace = (original, translated) => {
        const source = String(original ?? "");
        const leading = source.match(/^\s*/)?.[0] || "";
        const trailing = source.match(/\s*$/)?.[0] || "";
        return `${leading}${translated}${trailing}`;
    };

    const translatedExact = (trimmed) =>
        Object.prototype.hasOwnProperty.call(exact, trimmed)
            ? exact[trimmed]
            : null;


    const translateEmbeddedExact = (input) => {
        let output = String(input ?? "");

        Object.entries(exact)
            .filter(([source, target]) =>
                source &&
                target &&
                source !== target &&
                output.includes(source)
            )
            .sort((a, b) => b[0].length - a[0].length)
            .forEach(([source, target]) => {
                output = output.split(source).join(target);
            });

        return normalizeCurrencySymbols(
            toLatinDigits(output)
        );
    };

    const translateDynamic = (input) => {
        const original = String(input ?? "");
        const trimmed = original.trim();

        if (!trimmed) {
            return original;
        }

        const exactValue = translatedExact(trimmed);

        if (exactValue !== null) {
            return preserveOuterWhitespace(
                original,
                normalizeCurrencySymbols(
                    toLatinDigits(exactValue)
                )
            );
        }

        let match;
        let translated = null;

        if ((match = trimmed.match(/^عرض الشريحة\s+(\d+)$/))) {
            translated = format(formats.ShowSlide, match[1]);
        }
        else if ((match = trimmed.match(/^عرض الفئة رقم\s+(\d+)$/))) {
            translated = format(formats.ShowCategory, match[1]);
        }
        else if ((match = trimmed.match(/^استكشف فئة\s+(.+)$/))) {
            translated = format(formats.ExploreCategory, match[1]);
        }
        else if ((match = trimmed.match(/^عرض تفاصيل\s+(.+)$/))) {
            translated = format(formats.ViewDetails, match[1]);
        }
        else if ((match = trimmed.match(/^صور إضافية لـ\s+(.+)$/))) {
            translated = format(formats.MorePhotos, match[1]);
        }
        else if ((match = trimmed.match(/^صورة إضافية لـ\s+(.+)$/))) {
            translated = format(formats.AdditionalPhoto, match[1]);
        }
        else if ((match = trimmed.match(/^إضافة\s+(.+)\s+للمفضلة$/))) {
            translated = format(formats.AddFavorite, match[1]);
        }
        else if ((match = trimmed.match(/^عرض متاح في\s+(.+)$/))) {
            translated = format(formats.OfferAt, match[1]);
        }
        else if ((match = trimmed.match(/^(\d+)\s+تقييم موثق$/))) {
            translated = format(formats.VerifiedReviews, match[1]);
        }
        else if ((match = trimmed.match(/^متبقي\s+(\d+)\s+أيام$/))) {
            translated = format(formats.RemainingDays, match[1]);
        }
        else if ((match = trimmed.match(/^حتى\s+(.+)\s+عرض$/))) {
            translated = format(formats.UpToDiscount, match[1]);
        }
        else if ((match = trimmed.match(/^حتى\s+(.+)$/))) {
            translated = format(formats.Until, match[1]);
        }
        else if ((match = trimmed.match(/^من\s+(\d+)\s+ليلة$/))) {
            translated = format(formats.MinNights, match[1]);
        }
        else if ((match = trimmed.match(/^من\s+(\d+)\s+أشخاص$/))) {
            translated = format(formats.MinPeople, match[1]);
        }
        else if ((match = trimmed.match(/^(\d+(?:[.,]\d+)?)\s*كم$/))) {
            translated = format(formats.Km, match[1]);
        }
        else if ((match = trimmed.match(/^(\d+(?:[.,]\d+)?)\s*م$/))) {
            translated = format(formats.Meters, match[1]);
        }
        else if ((match = trimmed.match(/^(.+?)\s+عن موقعك$/))) {
            translated = format(formats.FromLocation, match[1]);
        }
        else if ((match = trimmed.match(/^يبدأ من\s+(.+)$/))) {
            translated = format(formats.StartsFrom, match[1]);
        }
        else if ((match = trimmed.match(/^وجدنا\s+(\d+)\s+منشآت مطابقة بالقرب منك$/))) {
            translated = format(formats.FoundPlaces, match[1]);
        }
        else if ((match = trimmed.match(/^اكتشفنا\s+(\d+)\s+منشآت حولك!$/))) {
            translated = format(formats.DiscoveredPlaces, match[1]);
        }
        else if ((match = trimmed.match(/^(\d+)\s+يوم\s*·\s*(.+)$/))) {
            translated = format(formats.CountdownDays, match[1], match[2]);
        }
        else if ((match = trimmed.match(/^شعار\s+(.+)$/))) {
            translated = format(formats.Logo, match[1]);
        }
        else if ((match = trimmed.match(/^(.+?)\s+موثوق وآمن$/))) {
            translated = format(formats.TrustedSafe, match[1]);
        }
        else if ((match = trimmed.match(/^عرض\s+(.+)$/))) {
            translated = format(formats.View, match[1]);
        }

        if (translated === null) {
            // Do not translate free text written by admins/owners or DB names here.
            // That data is intentionally deferred to the bilingual-data phase.
            return original;
        }

        translated = normalizeCurrencySymbols(
            toLatinDigits(translated)
        );

        return preserveOuterWhitespace(
            original,
            translated
        );
    };

    // --------------------------------------------------------
    // Google Maps language
    // index.js dynamically creates the Maps script.
    // This hook runs before index.js and changes ar -> en.
    // --------------------------------------------------------
    const normalizeGoogleMapsScript = node => {
        if (!(node instanceof HTMLScriptElement)) {
            return;
        }

        const source = node.getAttribute("src") || "";

        if (!source.includes("maps.googleapis.com/maps/api/js")) {
            return;
        }

        try {
            const url = new URL(source, window.location.origin);
            url.searchParams.set("language", "en");
            url.searchParams.set("region", "YE");
            node.setAttribute("src", url.toString());
        }
        catch (_) {
            node.setAttribute(
                "src",
                source.replace(/([?&])language=ar(?=&|$)/i, "$1language=en")
            );
        }
    };

    const originalAppendChild = Node.prototype.appendChild;
    Node.prototype.appendChild = function (child) {
        normalizeGoogleMapsScript(child);
        return originalAppendChild.call(this, child);
    };

    const originalInsertBefore = Node.prototype.insertBefore;
    Node.prototype.insertBefore = function (newNode, referenceNode) {
        normalizeGoogleMapsScript(newNode);
        return originalInsertBefore.call(this, newNode, referenceNode);
    };

    const originalAppend = Element.prototype.append;
    Element.prototype.append = function (...nodes) {
        nodes.forEach(normalizeGoogleMapsScript);
        return originalAppend.apply(this, nodes);
    };

    // --------------------------------------------------------
    // Force Home JS date/number output to English when the old
    // index.js explicitly asks for ar-YE.
    // --------------------------------------------------------
    const replaceArabicLocale = locales => {
        if (
            locales === undefined ||
            locales === null ||
            locales === ""
        ) {
            return englishLocale;
        }

        if (Array.isArray(locales)) {
            return locales.map(locale =>
                /^ar(?:-|$)/i.test(String(locale))
                    ? englishLocale
                    : locale
            );
        }

        return /^ar(?:-|$)/i.test(String(locales))
            ? englishLocale
            : locales;
    };

    const nativeDateLocaleTime =
        Date.prototype.toLocaleTimeString;

    Date.prototype.toLocaleTimeString =
        function (locales, options) {
            return nativeDateLocaleTime.call(
                this,
                replaceArabicLocale(locales),
                options
            );
        };

    const nativeDateLocaleDate =
        Date.prototype.toLocaleDateString;

    Date.prototype.toLocaleDateString =
        function (locales, options) {
            return nativeDateLocaleDate.call(
                this,
                replaceArabicLocale(locales),
                options
            );
        };

    const nativeNumberLocaleString =
        Number.prototype.toLocaleString;

    Number.prototype.toLocaleString =
        function (locales, options) {
            return nativeNumberLocaleString.call(
                this,
                replaceArabicLocale(locales),
                options
            );
        };

    const NativeNumberFormat = Intl.NumberFormat;

    Intl.NumberFormat = function (locales, options) {
        return new NativeNumberFormat(
            replaceArabicLocale(locales),
            options
        );
    };

    Intl.NumberFormat.prototype =
        NativeNumberFormat.prototype;

    // --------------------------------------------------------
    // DOM translation
    // --------------------------------------------------------
    const translatableAttributes = [
        "aria-label",
        "title",
        "placeholder",
        "data-name",
        "data-text"
    ];

    const processElement = element => {
        if (!(element instanceof Element)) {
            return;
        }

        if (
            element.hasAttribute("dir") &&
            element.getAttribute("dir") === "rtl" &&
            !element.matches(
                "input[type='tel'], .phone-wrapper-ltr, [data-force-rtl]"
            )
        ) {
            element.setAttribute("dir", "ltr");
        }

        translatableAttributes.forEach(attribute => {
            if (!element.hasAttribute(attribute)) {
                return;
            }

            const current =
                element.getAttribute(attribute) || "";

            const translated =
                translateDynamic(current);

            if (translated !== current) {
                element.setAttribute(
                    attribute,
                    translated
                );
            }
        });
    };

    const processText = textNode => {
        if (
            !(textNode instanceof Text) ||
            !textNode.parentElement
        ) {
            return;
        }

        const parent = textNode.parentElement;

        if (
            parent.closest(
                "script, style, code, pre, textarea, [data-no-home-i18n]"
            )
        ) {
            return;
        }

        const translated =
            translateDynamic(textNode.nodeValue);

        if (translated !== textNode.nodeValue) {
            textNode.nodeValue = translated;
        }
    };

    const translateTree = root => {
        if (!root) {
            return;
        }

        if (root instanceof Text) {
            processText(root);
            return;
        }

        if (!(root instanceof Element) &&
            root !== document.body) {
            return;
        }

        if (root instanceof Element) {
            processElement(root);
        }

        const walker =
            document.createTreeWalker(
                root,
                NodeFilter.SHOW_ELEMENT |
                NodeFilter.SHOW_TEXT
            );

        let node;

        while ((node = walker.nextNode())) {
            if (node instanceof Element) {
                processElement(node);
            }
            else if (node instanceof Text) {
                processText(node);
            }
        }
    };

    const translateCompassLetters = () => {
        const values = {
            ".nearby-v10-compass-cardinal--n": "N",
            ".nearby-v10-compass-cardinal--e": "E",
            ".nearby-v10-compass-cardinal--s": "S",
            ".nearby-v10-compass-cardinal--w": "W"
        };

        Object.entries(values).forEach(
            ([selector, value]) => {
                document
                    .querySelectorAll(selector)
                    .forEach(element => {
                        element.textContent = value;
                    });
            }
        );
    };

    const normalizeHomeEnglishVisuals = () => {
        translateCompassLetters();

        const radius = document.getElementById(
            "nearbyRadiusValue"
        );

        if (radius) {
            radius.textContent =
                translateDynamic(radius.textContent);
        }

        document.documentElement.lang = "en";
        document.documentElement.dir = "ltr";
    };

    translateTree(document.body);
    normalizeHomeEnglishVisuals();

    // عنوان المتصفح قد يحتوي أكثر من قيمة، مثل:
    // "العنوان الرئيسي - اسم المنصة | ..."
    // لذلك نسمح بالاستبدال الجزئي هنا فقط، وليس داخل محتوى المستخدمين.
    document.title =
        translateEmbeddedExact(
            translateDynamic(document.title)
        );

    let translationFrame = 0;
    const pendingRoots = new Set();

    const flushPending = () => {
        translationFrame = 0;

        pendingRoots.forEach(root => {
            translateTree(root);
        });

        pendingRoots.clear();
        normalizeHomeEnglishVisuals();
    };

    const scheduleRoot = root => {
        pendingRoots.add(root);

        if (translationFrame) {
            return;
        }

        translationFrame =
            window.requestAnimationFrame(
                flushPending
            );
    };

    const observer =
        new MutationObserver(mutations => {
            mutations.forEach(mutation => {
                if (
                    mutation.type === "characterData"
                ) {
                    scheduleRoot(mutation.target);
                    return;
                }

                if (
                    mutation.type === "attributes"
                ) {
                    scheduleRoot(mutation.target);
                    return;
                }

                mutation.addedNodes.forEach(node => {
                    scheduleRoot(node);
                });
            });
        });

    observer.observe(document.body, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true,
        attributeFilter: [
            "aria-label",
            "title",
            "placeholder",
            "data-name",
            "data-text",
            "dir"
        ]
    });

    window.addEventListener(
        "pagehide",
        () => {
            observer.disconnect();

            if (translationFrame) {
                window.cancelAnimationFrame(
                    translationFrame
                );
            }
        },
        { once: true }
    );
})();
