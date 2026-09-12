(() => {
    "use strict";

    const config = window.YemenGuideHomeI18n;

    if (!config || config.language !== "en") {
        return;
    }

    const body = document.body;

    if (!body) {
        return;
    }

    const customerController =
        body.dataset.customerController || "";

    const customerAction =
        body.dataset.customerAction || "";

    const isHomePage =
        customerController.toLowerCase() === "home" &&
        customerAction.toLowerCase() === "index";

    const isDetailsPage =
        customerController.toLowerCase() === "places" &&
        customerAction.toLowerCase() === "details";

    const isSearchPage =
        customerController.toLowerCase() === "places" &&
        customerAction.toLowerCase() === "search";

    if (!isHomePage && !isDetailsPage && !isSearchPage) {
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

    const normalizeLookupText = value =>
        String(value ?? "")
            .replace(/\s+/g, " ")
            .trim();

    const normalizedExact = Object.fromEntries(
        Object.entries(exact).map(
            ([source, target]) => [
                normalizeLookupText(source),
                target
            ]
        )
    );

    const translatedExact = value => {
        const key = normalizeLookupText(value);

        return Object.prototype.hasOwnProperty.call(
            normalizedExact,
            key
        )
            ? normalizedExact[key]
            : null;
    };


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
        const normalized =
            normalizeLookupText(trimmed);

        if (!normalized) {
            return original;
        }

        const exactValue = translatedExact(normalized);

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

        if (isSearchPage) {
            if ((match = normalized.match(/^فتح منشأة\s+(.+)$/))) {
                translated = format(
                    formats.SearchOpenProperty,
                    match[1]
                );
            }
            else if ((match = normalized.match(/^الانتقال إلى الإعلان رقم\s+([0-9٠-٩۰-۹]+)$/))) {
                translated = format(
                    formats.SearchGoToAd,
                    toLatinDigits(match[1])
                );
            }
            else if ((match = normalized.match(/^عرض تفاصيل\s+(.+)$/))) {
                translated = format(
                    formats.SearchViewDetails,
                    match[1]
                );
            }
            else if ((match = normalized.match(/^صور إضافية لـ\s+(.+)$/))) {
                translated = format(
                    formats.SearchAdditionalPhotos,
                    match[1]
                );
            }
            else if ((match = normalized.match(/^صورة إضافية لـ\s+(.+)$/))) {
                translated = format(
                    formats.SearchAdditionalPhoto,
                    match[1]
                );
            }
            else if ((match = normalized.match(/^التقييم\s+([0-9٠-٩۰-۹.,]+)\s+من\s+5$/))) {
                translated = format(
                    formats.SearchRatingOutOf5,
                    toLatinDigits(match[1])
                );
            }
            else if ((match = normalized.match(/^([0-9٠-٩۰-۹]+)\s+تقييم$/))) {
                translated = format(
                    formats.SearchReviewsCount,
                    toLatinDigits(match[1])
                );
            }
            else if ((match = normalized.match(/^عرض متاح في\s+(.+)$/))) {
                translated = format(
                    formats.SearchOfferAvailableAt,
                    match[1]
                );
            }
            else if ((match = normalized.match(/^خصم\s*([0-9٠-٩۰-۹]+(?:[.,][0-9٠-٩۰-۹]+)?)\s*%$/))) {
                translated = format(
                    formats.SearchDiscountPercent,
                    toLatinDigits(match[1])
                );
            }
            else if ((match = normalized.match(/^خصم\s*([0-9٠-٩۰-۹]+(?:[.,][0-9٠-٩۰-۹]+)?)\s*%\s*·\s*(.+)$/))) {
                const offerText = format(
                    formats.SearchDiscountPercent,
                    toLatinDigits(match[1])
                );

                const targetText =
                    translatedExact(match[2]) ??
                    match[2];

                translated = format(
                    formats.SearchOfferTarget,
                    offerText,
                    targetText
                );
            }
        }

        if (translated === null && isDetailsPage) {
            if ((match = normalized.match(/^(\d+)\s+تقييم$/))) {
                translated = format(formats.DetailsReviewCount, match[1]);
            }
            else if ((match = normalized.match(/^بناءً على\s+(\d+)\s+تجربة$/))) {
                translated = format(formats.DetailsBasedOnExperiences, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+ميزة$/))) {
                translated = format(formats.DetailsFeatureCount, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+مساحة$/))) {
                translated = format(formats.DetailsSpaceCount, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+باقة$/))) {
                translated = format(formats.DetailsPackageCount, match[1]);
            }
            else if ((match = normalized.match(/^حتى\s+(\d+)\s+شخص$/))) {
                translated = format(formats.DetailsUpToPeople, match[1]);
            }
            else if ((match = normalized.match(/^سعة\s+(\d+)\s+شخص$/))) {
                translated = format(formats.DetailsCapacityPeople, match[1]);
            }
            else if ((match = normalized.match(/^عرض\s+(\d+)\s+من الردود$/))) {
                translated = format(formats.DetailsShowReplies, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+ث$/))) {
                translated = format(formats.DetailsSecondsAgo, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+د$/))) {
                translated = format(formats.DetailsMinutesAgo, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+س$/))) {
                translated = format(formats.DetailsHoursAgo, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+ي$/))) {
                translated = format(formats.DetailsDaysAgo, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+أ$/))) {
                translated = format(formats.DetailsWeeksAgo, match[1]);
            }
            else if ((match = normalized.match(/^التواصل مع\s+(.+)$/))) {
                translated = format(formats.DetailsContactWith, match[1]);
            }
            else if ((match = normalized.match(/^صور\s+(.+)$/))) {
                translated = format(formats.DetailsPhotosOf, match[1]);
            }
            else if ((match = normalized.match(/^صورة إضافية لـ\s+(.+)$/))) {
                translated = format(formats.DetailsAdditionalPhotoOf, match[1]);
            }
            else if ((match = normalized.match(/^صورة\s+(\d+)\s+من\s+(.+)$/))) {
                translated = format(formats.DetailsImageOf, match[1], match[2]);
            }
            else if ((match = normalized.match(/^عرض صور\s+(.+)$/))) {
                translated = format(formats.DetailsViewServicePhotos, match[1]);
            }
            else if ((match = normalized.match(/^خريطة موقع\s+(.+)$/))) {
                translated = format(formats.DetailsMapLocationOf, match[1]);
            }
            else if ((match = normalized.match(/^التقييم\s+([0-9٠-٩۰-۹.,]+)\s+من خمسة$/))) {
                translated = format(formats.DetailsRatingOutOfFive, toLatinDigits(match[1]));
            }
            else if ((match = normalized.match(/^([0-9٠-٩۰-۹.,]+)\s+من\s+5$/))) {
                translated = `${toLatinDigits(match[1])} out of 5`;
            }
            else if ((match = normalized.match(/^اسأل\s+(.+?)\s+عن التوفر والخدمات$/))) {
                translated = format(formats.DetailsAskOwner, match[1]);
            }
            else if ((match = normalized.match(/^تم نسخ كود العرض:\s*(.+)$/))) {
                translated = format(formats.DetailsCopiedPromoCode, match[1]);
            }
            else if ((match = normalized.match(/^لا يمكن أن يتجاوز العدد\s+(\d+)\s+أشخاص\.$/))) {
                translated = format(formats.DetailsMaxPeople, match[1]);
            }
            else if ((match = normalized.match(/^وصلت إلى الحد الأقصى\s*\((\d+)\s+أشخاص\)\.$/))) {
                translated = format(formats.DetailsMaxReached, match[1]);
            }
            else if ((match = normalized.match(/^رد على\s+(.+)$/))) {
                translated = format(formats.DetailsReplyTo, match[1]);
            }
            else if ((match = normalized.match(/^(.+?)\s*-\s*صورة\s+(\d+)$/))) {
                translated = format(formats.DetailsServiceImage, match[1], match[2]);
            }
            else if ((match = normalized.match(/^عرض الصورة رقم\s+(\d+)$/))) {
                translated = format(formats.DetailsViewImage, match[1]);
            }
            else if ((match = normalized.match(/^(.+?)\s*-\s*صورة مصغرة\s+(\d+)$/))) {
                translated = format(formats.DetailsThumbnailImage, match[1], match[2]);
            }
            else if ((match = normalized.match(/^لا يمكن الحجز لأكثر من\s+(\d+)\s+أشهر مقدمًا\.$/))) {
                translated = format(formats.DetailsBookMonthsAhead, match[1]);
            }
            else if ((match = normalized.match(/^يجب إجراء الحجز قبل الموعد بـ\s*(\d+)\s+ساعة على الأقل\.$/))) {
                translated = format(formats.DetailsMinNoticeHours, match[1]);
            }
            else if ((match = normalized.match(/^إلغاء مجاني قبل\s+(\d+)\s+ساعة\.$/))) {
                translated = format(formats.DetailsFreeCancellationHours, match[1]);
            }
            else if ((match = normalized.match(/^مدة معالجة الاسترداد حتى\s+(\d+)\s+أيام\.$/))) {
                translated = format(formats.DetailsRefundProcessingDays, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+ساعة من وقت الطلب\.$/))) {
                translated = format(formats.DetailsHourFromRequest, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+عروض متاحة$/))) {
                translated = format(formats.DetailsOffersAvailable, match[1]);
            }
            else if ((match = normalized.match(/^الدخول\s+(.+)$/))) {
                translated = format(formats.DetailsCheckInCompact, toLatinDigits(match[1]));
            }
            else if ((match = normalized.match(/^الخروج\s+(.+)$/))) {
                translated = format(formats.DetailsCheckOutCompact, toLatinDigits(match[1]));
            }
            else if ((match = normalized.match(/^إلغاء مجاني قبل\s+(\d+)\s+ساعة$/))) {
                translated = format(formats.DetailsFreeCancellationCompact, match[1]);
            }
            else if ((match = normalized.match(/^اسأل\s+(.+?)\s+عن التوفر والخدمات والتفاصيل قبل تأكيد طلبك\.$/))) {
                translated = format(formats.DetailsAskOwnerFull, match[1]);
            }
            else if ((match = normalized.match(/^خصم\s*([0-9٠-٩۰-۹]+(?:[.,][0-9٠-٩۰-۹]+)?)\s*%$/))) {
                translated = format(
                    formats.DetailsDiscountPercent,
                    toLatinDigits(match[1])
                );
            }
            else if ((match = normalized.match(/^([0-9٠-٩۰-۹]+(?:[.,][0-9٠-٩۰-۹]+)?)\s*%\s*خصم$/))) {
                translated = format(
                    formats.DetailsDiscountPercent,
                    toLatinDigits(match[1])
                );
            }
            else if ((match = normalized.match(/^فترة الاستحقاق\s+(.+)$/))) {
                translated = format(
                    formats.DetailsEligibilityPeriod,
                    toLatinDigits(match[1])
                );
            }
            else if ((match = normalized.match(/^وفر\s+(.+)$/))) {
                translated = format(
                    formats.DetailsSaveAmount,
                    normalizeCurrencySymbols(
                        toLatinDigits(match[1])
                    )
                );
            }
            else if ((match = normalized.match(/^(خصم\s*[0-9٠-٩۰-۹]+(?:[.,][0-9٠-٩۰-۹]+)?\s*%)\s+على\s+(.+)$/))) {
                translated = format(
                    formats.DetailsOfferOnTarget,
                    translateDynamic(match[1]).trim(),
                    match[2]
                );
            }
            else if ((match = normalized.match(/^نظام التشغيل:\s*(.+)$/))) {
                translated = `Operating mode: ${translateDynamic(match[1]).trim()}`;
            }
            else if ((match = normalized.match(/^وقت الدخول:\s*(.+)$/))) {
                translated = `Check-in time: ${toLatinDigits(match[1])}`;
            }
            else if ((match = normalized.match(/^وقت الخروج:\s*(.+)$/))) {
                translated = `Check-out time: ${toLatinDigits(match[1])}`;
            }
            else if ((match = normalized.match(/^مهلة السداد:\s*(\d+)\s+ساعة من وقت الطلب\.$/))) {
                translated = `Payment deadline: ${match[1]} hours from the request time.`;
            }
            else if ((match = normalized.match(/^متبقي\s+(\d+)\s+أيام$/))) {
                translated = format(formats.RemainingDays, match[1]);
            }
            else if ((match = normalized.match(/^ينتهي خلال\s+(\d+)\s+أيام$/))) {
                translated = `Ends in ${match[1]} days`;
            }
            else if ((match = normalized.match(/^من\s+(\d+)\s+ليلة$/))) {
                translated = format(formats.MinNights, match[1]);
            }
            else if ((match = normalized.match(/^من\s+(\d+)\s+أشخاص$/))) {
                translated = format(formats.MinPeople, match[1]);
            }
        }
        else if (isHomePage) {

            if ((match = normalized.match(/^عرض الشريحة\s+(\d+)$/))) {
                translated = format(formats.ShowSlide, match[1]);
            }
            else if ((match = normalized.match(/^عرض الفئة رقم\s+(\d+)$/))) {
                translated = format(formats.ShowCategory, match[1]);
            }
            else if ((match = normalized.match(/^استكشف فئة\s+(.+)$/))) {
                translated = format(formats.ExploreCategory, match[1]);
            }
            else if ((match = normalized.match(/^عرض تفاصيل\s+(.+)$/))) {
                translated = format(formats.ViewDetails, match[1]);
            }
            else if ((match = normalized.match(/^صور إضافية لـ\s+(.+)$/))) {
                translated = format(formats.MorePhotos, match[1]);
            }
            else if ((match = normalized.match(/^صورة إضافية لـ\s+(.+)$/))) {
                translated = format(formats.AdditionalPhoto, match[1]);
            }
            else if ((match = normalized.match(/^إضافة\s+(.+)\s+للمفضلة$/))) {
                translated = format(formats.AddFavorite, match[1]);
            }
            else if ((match = normalized.match(/^عرض متاح في\s+(.+)$/))) {
                translated = format(formats.OfferAt, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+تقييم موثق$/))) {
                translated = format(formats.VerifiedReviews, match[1]);
            }
            else if ((match = normalized.match(/^متبقي\s+(\d+)\s+أيام$/))) {
                translated = format(formats.RemainingDays, match[1]);
            }
            else if ((match = normalized.match(/^حتى\s+(.+)\s+عرض$/))) {
                translated = format(formats.UpToDiscount, match[1]);
            }
            else if ((match = normalized.match(/^حتى\s+(.+)$/))) {
                translated = format(formats.Until, match[1]);
            }
            else if ((match = normalized.match(/^من\s+(\d+)\s+ليلة$/))) {
                translated = format(formats.MinNights, match[1]);
            }
            else if ((match = normalized.match(/^من\s+(\d+)\s+أشخاص$/))) {
                translated = format(formats.MinPeople, match[1]);
            }
            else if ((match = normalized.match(/^(\d+(?:[.,]\d+)?)\s*كم$/))) {
                translated = format(formats.Km, match[1]);
            }
            else if ((match = normalized.match(/^(\d+(?:[.,]\d+)?)\s*م$/))) {
                translated = format(formats.Meters, match[1]);
            }
            else if ((match = normalized.match(/^(.+?)\s+عن موقعك$/))) {
                translated = format(formats.FromLocation, match[1]);
            }
            else if ((match = normalized.match(/^يبدأ من\s+(.+)$/))) {
                translated = format(formats.StartsFrom, match[1]);
            }
            else if ((match = normalized.match(/^وجدنا\s+(\d+)\s+منشآت مطابقة بالقرب منك$/))) {
                translated = format(formats.FoundPlaces, match[1]);
            }
            else if ((match = normalized.match(/^اكتشفنا\s+(\d+)\s+منشآت حولك!$/))) {
                translated = format(formats.DiscoveredPlaces, match[1]);
            }
            else if ((match = normalized.match(/^(\d+)\s+يوم\s*·\s*(.+)$/))) {
                translated = format(formats.CountdownDays, match[1], match[2]);
            }
            else if ((match = normalized.match(/^شعار\s+(.+)$/))) {
                translated = format(formats.Logo, match[1]);
            }
            else if ((match = normalized.match(/^(.+?)\s+موثوق وآمن$/))) {
                translated = format(formats.TrustedSafe, match[1]);
            }
            else if ((match = normalized.match(/^عرض\s+(.+)$/))) {
                translated = format(formats.View, match[1]);
            }

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
    // DOM translation
    // --------------------------------------------------------
    const translatableAttributes =
        isHomePage
            ? [
                "aria-label",
                "title",
                "placeholder",
                "data-name",
                "data-text"
            ]
            : [
                "aria-label",
                "title",
                "placeholder",
                "alt"
            ];

    const detailsProtectedTextSelector = [
        ".place-title",
        ".place-intro-description",
        ".place-address-text",
        ".technical-detail-card h3",
        ".technical-detail-points li span",
        ".amenity-item > span:last-child",
        ".space-item > span:last-child",
        ".service-name",
        ".package-card-copy h3",
        ".package-card-copy p",
        ".review-node-user",
        ".review-text-content",
        ".promotion-title",
        ".promotion-description",
        ".promotion-spotlight-card h3",
        ".promotion-card-description",
        ".offer-title",
        ".offer-description",
        "[data-owner-content]",
        "[data-user-content]"
    ].join(",");

    const isProtectedDetailsText = element => {
        if (!isDetailsPage || !element) {
            return false;
        }

        // These are system-generated promotion texts, not owner free text.
        if (
            element.closest?.(
                ".promotion-offer-summary, .promotion-repeat-note"
            )
        ) {
            return false;
        }

        return Boolean(
            element.closest?.(
                detailsProtectedTextSelector
            )
        );
    };


    const translateKnownProtectedDetailsText = input => {
        const original = String(input ?? "");
        const normalized =
            normalizeLookupText(original);

        if (!normalized) {
            return original;
        }

        // Exact known system labels are safe to translate even inside
        // fields that otherwise contain owner-entered data.
        const exactValue =
            translatedExact(normalized);

        if (exactValue !== null) {
            return preserveOuterWhitespace(
                original,
                normalizeCurrencySymbols(
                    toLatinDigits(exactValue)
                )
            );
        }

        // Standard builder items such as "غرف (20)".
        const counted =
            normalized.match(
                /^(.+?)\s*\(([0-9٠-٩۰-۹]+)\)$/
            );

        if (counted) {
            const baseTranslation =
                translatedExact(
                    counted[1]
                );

            if (baseTranslation !== null) {
                return preserveOuterWhitespace(
                    original,
                    `${baseTranslation} (${toLatinDigits(counted[2])})`
                );
            }
        }

        return original;
    };

    const processElement = element => {
        if (!(element instanceof Element)) {
            return;
        }

        // Google Maps is an external runtime component.
        // Never mutate its generated DOM in the English localization layer.
        if (
            element.matches("#map, .gm-style, .gm-style *") ||
            element.closest("#map, .gm-style")
        ) {
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
                "script, style, code, pre, textarea, #map, .gm-style, [data-no-home-i18n]"
            )
        ) {
            return;
        }

        if (isProtectedDetailsText(parent)) {
            const protectedTranslation =
                translateKnownProtectedDetailsText(
                    textNode.nodeValue
                );

            if (
                protectedTranslation !==
                textNode.nodeValue
            ) {
                textNode.nodeValue =
                    protectedTranslation;
            }

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


    const translateDetailsCompositeSystemText = () => {
        if (!isDetailsPage) {
            return;
        }

        // "خصم 44% على [اسم الباقة/المنشأة]"
        document
            .querySelectorAll(
                ".booking-mini-offer-copy strong"
            )
            .forEach(element => {
                const current =
                    element.textContent || "";

                const translated =
                    translateDynamic(current);

                if (translated !== current) {
                    element.textContent =
                        translated;
                }
            });

        // Contact sentence contains the owner name inside a fixed system sentence.
        document
            .querySelectorAll(
                ".place-chat-card-copy p"
            )
            .forEach(element => {
                const current =
                    element.textContent || "";

                const translated =
                    translateDynamic(current);

                if (translated !== current) {
                    element.textContent =
                        translated;
                }
            });
    };

    const normalizeEnglishVisuals = () => {
        if (isHomePage) {
            translateCompassLetters();

            const radius = document.getElementById(
                "nearbyRadiusValue"
            );

            if (radius) {
                radius.textContent =
                    translateDynamic(radius.textContent);
            }
        }

        document.documentElement.lang = "en";
        document.documentElement.dir = "ltr";

        if (isSearchPage) {
            const searchPage =
                document.querySelector(
                    ".search-luxury-page"
                );

            if (searchPage) {
                searchPage.setAttribute(
                    "dir",
                    "ltr"
                );
            }
        }

        translateDetailsCompositeSystemText();
    };

    const translateDetailsDocumentTitle = value => {
        const original = String(value ?? "");
        const separatorIndex = original.indexOf(" - ");

        if (separatorIndex < 0) {
            return translateEmbeddedExact(original);
        }

        const ownerTitle =
            original.slice(0, separatorIndex);

        const systemSuffix =
            original.slice(separatorIndex + 3);

        return `${ownerTitle} - ${translateEmbeddedExact(systemSuffix)}`;
    };

    translateTree(document.body);
    normalizeEnglishVisuals();

    document.title =
        isDetailsPage
            ? translateDetailsDocumentTitle(document.title)
            : translateEmbeddedExact(
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
        normalizeEnglishVisuals();
    };

    const scheduleRoot = root => {
        const element =
            root instanceof Element
                ? root
                : root?.parentElement;

        if (
            element &&
            (
                element.matches?.("#map, .gm-style, .gm-style *") ||
                element.closest?.("#map, .gm-style")
            )
        ) {
            return;
        }

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
            "alt",
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
