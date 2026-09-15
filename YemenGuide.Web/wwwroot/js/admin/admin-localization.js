(() => {
    "use strict";

    const config = window.YemenGuideAdminI18n;
    if (!config) return;

    const isEnglish = config.language === "en";

    const isPlaceCreateScope =
        /\/Admin\/Places\/(Create|FillSurvey)(?:\/|$)/i.test(
            window.location.pathname
        );

    const isAdvertisementsScope =
        /\/Admin\/Advertisements(?:\/|$)/i.test(
            window.location.pathname
        );

    const isReportsScope =
        /\/Admin\/Reports(?:\/|$)/i.test(
            window.location.pathname
        );

    const isFinanceScope =
        /\/Admin\/Financials(?:\/|$)/i.test(
            window.location.pathname
        ) ||
        Boolean(document.getElementById("adminFinancialsRoot")) ||
        Boolean(document.body?.classList.contains("afp"));

    const isMasterEntitiesScope =
        /\/Admin\/MasterEntities(?:\/|$)/i.test(
            window.location.pathname
        ) ||
        Boolean(document.querySelector("[data-master-entities-root]"));

    const isProfileScope =
        /\/Admin\/Profile\/AdminProfile(?:\/|$)/i.test(
            window.location.pathname
        ) ||
        Boolean(document.querySelector("[data-admin-profile-root]"));

    const isNotificationsPage =
        /\/Admin\/Notifications(?:\/|$)/i.test(
            window.location.pathname
        ) ||
        Boolean(document.getElementById("adminNotificationCenterPage"));

    const hasAdminNotificationUi = () =>
        Boolean(document.getElementById("adminNotificationCenter")) ||
        Boolean(document.getElementById("adminNotificationCenterPage"));


    const exact = {
        ...(config.exact || {}),
        ...(isPlaceCreateScope
            ? (config.placeCreateExact || {})
            : {}),
        ...(isAdvertisementsScope
            ? (config.advertisementsExact || {})
            : {}),
        ...(isReportsScope
            ? (config.reportsExact || {})
            : {}),
        ...(isFinanceScope
            ? (config.financeExact || {})
            : {}),
        ...(isMasterEntitiesScope
            ? (config.masterEntitiesExact || {})
            : {}),
        ...(isProfileScope
            ? (config.profileExact || {})
            : {}),
        ...(isNotificationsPage
            ? (config.notificationsExact || {})
            : {})
    };

    const notificationEntries = Object.entries(
        config.notificationsExact || {}
    )
        .filter(([source, target]) =>
            Boolean(source) && Boolean(target) && source !== target)
        .sort((a, b) => b[0].length - a[0].length);

    const notificationNormalizedExact = Object.fromEntries(
        notificationEntries.map(([source, target]) => [
            String(source ?? "").replace(/\s+/g, " ").trim(),
            String(target)
        ])
    );

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

    const exactToken = value => {
        const normalized = normalize(value);
        if (!normalized) return String(value ?? "");

        if (Object.prototype.hasOwnProperty.call(
            normalizedExact,
            normalized
        )) {
            return toLatinDigits(
                normalizedExact[normalized]
            );
        }

        return String(value ?? "");
    };

    const translateKnownFragments = value => {
        let result = String(value ?? "");

        if (!isEnglish || !result) {
            return result;
        }

        result = normalizeCurrencySymbols(result);

        for (const [source, target] of orderedEntries) {
            if (!source || !target || source === target) continue;
            if (!result.includes(source)) continue;

            result = result.split(source).join(target);
        }

        return toLatinDigits(result);
    };

    const reportsFallback = () =>
        get(
            "Admin_ReportsZero_Fallback",
            "Content unavailable in English"
        );

    const financeFallback = () =>
        get(
            "Admin_Finance_Fallback",
            "Financial Information"
        );

    const profileFallback = () =>
        get(
            "Admin_Profile_Fallback",
            "Profile Information"
        );

    const notificationFallback = () =>
        get(
            "Admin_Notifications_Fallback",
            "Notification"
        );

    const notificationDetailsFallback = () =>
        get(
            "Admin_Notifications_DetailsFallback",
            "Open the notification to view details."
        );

    const translateNotificationUiValue = value => {
        const original = String(value ?? "");
        if (!isEnglish || !original) return original;

        const normalized = normalize(original);
        if (Object.prototype.hasOwnProperty.call(
            notificationNormalizedExact, normalized
        )) {
            return preserveOuterWhitespace(
                original,
                toLatinDigits(notificationNormalizedExact[normalized])
            );
        }

        let result = original;
        for (const [source, target] of notificationEntries) {
            if (!source || !target || source === target) continue;
            if (!result.includes(source)) continue;
            result = result.split(source).join(target);
        }
        return toLatinDigits(result);
    };

    const userContentNotificationTypes = new Set([
        "message", "chatmessage",
        "reviewcreated", "reviewreply",
        "reportcreated", "reportreply"
    ]);

    const translateNotificationContent = (value, type, field = "text") => {
        const original = String(value ?? "");
        if (!isEnglish || !original) return original;

        let translated = translateNotificationUiValue(original);
        if (translated !== original) return translated;

        const normalized = toLatinDigits(normalize(original));
        let match;

        match = normalized.match(/^لديك\s+(\d+)\s+إشعارات إدارية جديدة\.$/);
        if (match) return `You have ${match[1]} new admin notifications.`;

        match = normalized.match(/^آخر\s+(\d+)\s+إشعارات\s*•\s*تحديث تلقائي$/);
        if (match) return `Latest ${match[1]} notifications • Auto-refresh`;

        match = normalized.match(/^فتح مركز الإشعارات، لديك\s+(\d+)\s+غير مقروءة$/);
        if (match) return `Open Notification Center, ${match[1]} unread`;

        match = normalized.match(/^(\d+)\s+نتيجة$/);
        if (match) return `${match[1]} ${match[1] === "1" ? "result" : "results"}`;

        match = normalized.match(/^منذ\s+(\d+)\s+دقائق$/);
        if (match) return `${match[1]} minutes ago`;

        match = normalized.match(/^منذ\s+(\d+)\s+ساعات$/);
        if (match) return `${match[1]} hours ago`;

        match = normalized.match(/^منذ\s+(\d+)\s+أيام$/);
        if (match) return `${match[1]} days ago`;

        const typeKey = String(type || "general")
            .replace(/[^a-z0-9]/gi, "")
            .toLowerCase();

        // User-authored message/review/report bodies are the explicit exception:
        // preserve them exactly when no system wrapper is recognized.
        if (field === "text" && userContentNotificationTypes.has(typeKey)) {
            return original;
        }

        if (field === "title") {
            return containsArabic(original)
                ? notificationFallback()
                : original;
        }

        return containsArabic(original)
            ? notificationDetailsFallback()
            : original;
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

        // 3) صيغ رقمية ثابتة وآمنة خاصة بالصفحة، مثل "30 يوم".
        // لا نطبق أي استبدال عام داخل نصوص المستخدم.
        const latin = toLatinDigits(normalized);

        if (isAdvertisementsScope) {
            let match = latin.match(/^(\d+)\s+يوم$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${get("Admin_Ads_DayUnit", "day")}${match[1] === "1" ? "" : "s"}`
                );
            }

            match = latin.match(/^(\d+)\s+إعلان$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${get("Admin_Ads_AdUnit", "advertisement")}${match[1] === "1" ? "" : "s"}`
                );
            }
        }

        if (isReportsScope) {
            let match = latin.match(/^صفحة\s+(\d+)\s+من\s+(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${get("Admin_Reports_Page", "Page")} ${match[1]} ${get("Admin_Reports_Of", "of")} ${match[2]}`
                );
            }

            match = latin.match(/^(\d+)\s+تحذير$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${get("Admin_Reports_Warning", "warning")}${match[1] === "1" ? "" : "s"}`
                );
            }

            match = latin.match(/^(\d+)\s+مخالفة مثبتة$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} confirmed violation${match[1] === "1" ? "" : "s"}`
                );
            }
        }

        // 4) تراكيب نظام ثابتة تحتوي أرقامًا/تاريخًا ديناميكيًا.
        if (isReportsScope) {
            let match = latin.match(/^(\d+)\s+قضية تجاوزت مهلة الرد$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${match[1] === "1" ? "case has" : "cases have"} exceeded the response deadline`
                );
            }

            match = latin.match(/^(\d+)\s+قضية سابقة ضده$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} previous ${match[1] === "1" ? "case" : "cases"} against this user`
                );
            }

            match = latin.match(/^تحذيرات الحساب:\s*(\d+)\s*·\s*مخالفات مثبتة:\s*(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Account warnings: ${match[1]} · Confirmed violations: ${match[2]}`
                );
            }

            match = latin.match(/^آخر تحديث\s+(.+)$/);
            if (match) {
                return preserveOuterWhitespace(original, `Last update ${match[1]}`);
            }

            match = latin.match(/^القضية\s+(.+)$/);
            if (match && document.title.includes(normalized)) {
                return preserveOuterWhitespace(original, `Case ${match[1]}`);
            }
        }

        if (isAdvertisementsScope) {
            let match = latin.match(/^عرض المزيد\s*\((\d+)\)$/);
            if (match) {
                return preserveOuterWhitespace(original, `Show more (${match[1]})`);
            }

            match = latin.match(/^عرض أقل\s*\((\d+)\)$/);
            if (match) {
                return preserveOuterWhitespace(original, `Show less (${match[1]})`);
            }
        }

        if (isReportsScope) {
            let match = latin.match(/^(\d+)\s+سوابق$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} previous ${match[1] === "1" ? "case" : "cases"}`
                );
            }

            match = latin.match(/^(?:الحجز|حجز)\s*#\s*(\d+)(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Booking #${match[1]}${match[2] || ""}`
                );
            }

            match = latin.match(/^(?:التقييم|تقييم)\s*#\s*(\d+)(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Review #${match[1]}${match[2] || ""}`
                );
            }

            match = latin.match(/^(?:العرض|عرض)\s*#\s*(\d+)(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Promotion #${match[1]}${match[2] || ""}`
                );
            }

            match = latin.match(/^(?:الإعلان|إعلان)\s*#\s*(\d+)(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Advertisement #${match[1]}${match[2] || ""}`
                );
            }

            match = latin.match(/^(?:المنشأة|منشأة)\s*#\s*(\d+)(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Place #${match[1]}${match[2] || ""}`
                );
            }

            match = latin.match(/^بدون مرجع(\s*·\s*.*)?$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `No reference${match[1] || ""}`
                );
            }

            match = latin.match(/^من\s+(\d{4}\/\d{2}\/\d{2})(?:\s+إلى\s+(\d{4}\/\d{2}\/\d{2}))?$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    match[2]
                        ? `From ${match[1]} to ${match[2]}`
                        : `From ${match[1]}`
                );
            }

            match = latin.match(/^التقييم:\s*(\d+)\/5$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Rating: ${match[1]}/5`
                );
            }

            match = normalized.match(/^(مالك|عميل|الإدارة|مستخدم)\s+ضد\s+(مالك|عميل|الإدارة|مستخدم)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${exactToken(match[1])} vs ${exactToken(match[2])}`
                );
            }

            match = normalized.match(/^(مالك|عميل|الإدارة|مستخدم)\s+يبلّغ عن مشكلة في المنصة$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${exactToken(match[1])} reports a platform issue`
                );
            }

            match = normalized.match(/^المبلّغ:\s*(مالك|عميل|الإدارة|مستخدم)\s+—\s+لا يوجد طرف مبلّغ ضده$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Reporter: ${exactToken(match[1])} — No reported party`
                );
            }

            match = normalized.match(/^(مالك|عميل|الإدارة|مستخدم)\s+·\s+الحساب\s+(.+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${exactToken(match[1])} · Account ${exactToken(match[2])}`
                );
            }

            match = normalized.match(/^سوابق وتحذيرات\s+(مالك|عميل|الإدارة|مستخدم)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `History and warnings for ${exactToken(match[1])}`
                );
            }

            match = normalized.match(/^لا توجد سوابق مسجلة على هذا\s+(مالك|عميل|الإدارة|مستخدم)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `No recorded history for this ${exactToken(match[1]).toLowerCase()}`
                );
            }

            match = normalized.match(/^(.+?)\s+·\s+(الإدارة|مالك|عميل|مستخدم)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} · ${exactToken(match[2])}`
                );
            }

            match = normalized.match(/^(.+?)\s+·\s+(\d{4}\/\d{2}\/\d{2})$/);
            if (match) {
                const status = exactToken(match[1]);
                if (status !== match[1]) {
                    return preserveOuterWhitespace(
                        original,
                        `${status} · ${match[2]}`
                    );
                }
            }

            match = normalized.match(/^تم تغيير الأولوية إلى\s+(.+?)\.\s*السبب:\s*(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Priority changed to ${exactToken(match[1])}. Reason: ${match[2]}`
                );
            }

            match = normalized.match(/^تم تصعيد القضية\.\s*السبب:\s*(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Case escalated. Reason: ${match[1]}`
                );
            }

            match = normalized.match(/^تم رفض القضية\.\s*السبب:\s*(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Case rejected. Reason: ${match[1]}`
                );
            }

            match = normalized.match(/^تم إثبات مخالفة العميل وإصدار تنبيه\.\s*السبب:\s*(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Customer violation confirmed and a warning was issued. Reason: ${match[1]}`
                );
            }

            match = normalized.match(/^لم تثبت مخالفة العميل\.\s*السبب:\s*(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Customer violation was not confirmed. Reason: ${match[1]}`
                );
            }

            match = normalized.match(/^تم إغلاق القضية إداريًا\.\s*الملخص:\s*(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Case closed administratively. Summary: ${match[1]}`
                );
            }

            match = normalized.match(/^تمت إعادة فتح القضية\.\s*السبب:\s*(.*)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Case reopened. Reason: ${match[1]}`
                );
            }

            match = normalized.match(/^تم طلب توضيح من المبلّغ حتى\s+(.+)\.$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Reporter clarification requested until ${match[1]}.`
                );
            }

            match = normalized.match(/^تم طلب رد من المالك حتى\s+(.+)\.$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Owner response requested until ${match[1]}.`
                );
            }

            match = normalized.match(/^تم طلب تأكيد الحل من العميل حتى\s+(.+)\.$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Customer resolution confirmation requested until ${match[1]}.`
                );
            }

            const replySuffix = "يمكن الرد بنص أو صورة أو الاثنين معًا.";
            if (normalized.endsWith(replySuffix)) {
                const prefix = original.slice(
                    0,
                    original.lastIndexOf(replySuffix)
                ).replace(/\s+$/, "");

                return `${prefix}\nYou can reply with text, an image, or both.`;
            }

            match = normalized.match(/^سجلت الإدارة حلًا للقضية\s+(.+?)\.\s+هل تم حل المشكلة؟\s+اختر نعم أو لا من صفحة القضية\.$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Administration recorded a resolution for case ${match[1]}. Was the issue resolved? Choose Yes or No on the case page.`
                );
            }
        }

        if (isFinanceScope) {
            let match = latin.match(/^صفحة\s+(\d+)\s+من\s+(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Page ${match[1]} of ${match[2]}`
                );
            }

            match = latin.match(/^(\d+)\s+صف$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${match[1] === "1" ? "row" : "rows"}`
                );
            }

            match = latin.match(/^(\d+)\s+طلب$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${match[1] === "1" ? "request" : "requests"}`
                );
            }

            match = latin.match(/^(\d+)\s+منشأة$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${match[1] === "1" ? "place" : "places"}`
                );
            }

            match = latin.match(/^(\d+)\s+حجز$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${match[1] === "1" ? "booking" : "bookings"}`
                );
            }

            match = latin.match(/^(\d+)\s+حركة$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${match[1] === "1" ? "transaction" : "transactions"}`
                );
            }

            match = latin.match(/^(\d+)\s+سجل$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${match[1] === "1" ? "record" : "records"}`
                );
            }

            match = latin.match(/^القسم\s+(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Section ${match[1]}`
                );
            }

            match = latin.match(/^مكرر\s*×\s*(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Duplicate × ${match[1]}`
                );
            }

            match = latin.match(/^حجز\s*#\s*(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Booking #${match[1]}`
                );
            }

            match = latin.match(/^حساب\s*#\s*(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Account #${match[1]}`
                );
            }

            match = normalized.match(/^دين:\s*(.+)$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Debt: ${toLatinDigits(normalizeCurrencySymbols(match[1]))}`
                );
            }

            match = latin.match(/^المطبق الآن:\s*([\d.,]+)%$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Currently applied: ${match[1]}%`
                );
            }

            match = latin.match(/^المطبق بعد الحفظ:\s*([\d.,]+)%$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `Applied after saving: ${match[1]}%`
                );
            }

            // Server errors with a numeric HTTP code.
            match = latin.match(/^حدث خطأ\s*\((\d+)\)\.?$/);
            if (match) {
                return preserveOuterWhitespace(
                    original,
                    `An error occurred (${match[1]}).`
                );
            }
        }

        if (isProfileScope) {
            let match = latin.match(/^المدير:\s*(\d+)$/);
            if (match) {
                return preserveOuterWhitespace(original, `Admin: ${match[1]}`);
            }

            match = normalized.match(/^الحالة:\s*(نشط|معلق|موقوف)$/);
            if (match) {
                const status = match[1] === "نشط" ? "Active" : "Suspended";
                return preserveOuterWhitespace(original, `Status: ${status}`);
            }

            match = normalized.match(/^العملة الحالية:\s*(.+)$/);
            if (match) {
                const currency = translateKnownFragments(match[1]);
                return preserveOuterWhitespace(original, `Current currency: ${currency}`);
            }

            match = normalized.match(/^جاري تثبيت\s+(.+)\.\.\.$/);
            if (match) {
                const currency = translateKnownFragments(match[1]);
                return preserveOuterWhitespace(original, `Saving ${currency}...`);
            }
        }

        if (isNotificationsPage || hasAdminNotificationUi()) {
            const notificationTranslated = translateNotificationUiValue(original);
            if (notificationTranslated !== original) {
                return notificationTranslated;
            }
        }

        if (isMasterEntitiesScope) {
            let match = latin.match(/^(\d+)\s+منشأة$/);
            if (match) {
                const count = Number(match[1]);
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${count === 1 ? "place" : "places"}`
                );
            }

            match = latin.match(/^(\d+)\s+فئة$/);
            if (match) {
                const count = Number(match[1]);
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${count === 1 ? "category" : "categories"}`
                );
            }

            match = latin.match(/^(\d+)\s+مدينة$/);
            if (match) {
                const count = Number(match[1]);
                return preserveOuterWhitespace(
                    original,
                    `${match[1]} ${count === 1 ? "city" : "cities"}`
                );
            }
        }

        return original;
    };

    const containsArabic = value =>
        /[\u0600-\u06FF]/.test(String(value ?? ""));

    const translateSystemValue = (
        value,
        fallback = "Unable to complete the operation."
    ) => {
        const original = String(value ?? "");
        const translated = translateValue(original);

        if (!isEnglish || !containsArabic(translated)) {
            return translated;
        }

        const fragmentsTranslated =
            translateKnownFragments(translated);

        if (!containsArabic(fragmentsTranslated)) {
            return fragmentsTranslated;
        }

        return String(
            fallback ??
            reportsFallback() ??
            "Content unavailable in English"
        );
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
        "data-category-name",
        "data-package-example"
    ];

    const translateElementAttributes = element => {
        if (!isEnglish || !(element instanceof Element)) return;

        translatedAttributes.forEach(attributeName => {
            if (!element.hasAttribute(attributeName)) return;

            const current = element.getAttribute(attributeName);
            let next = translateValue(current);

            if (
                isReportsScope &&
                next === current &&
                containsArabic(current) &&
                !isProtectedUserElement(element)
            ) {
                next = translateSystemValue(
                    current,
                    reportsFallback()
                );
            }

            if (
                isMasterEntitiesScope &&
                next === current &&
                containsArabic(current)
            ) {
                // Translate only known system/DB-backed fragments and preserve
                // any unknown dynamic text exactly as stored.
                next = translateKnownFragments(current);
            }

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

    const shouldSkipBaseTextNode = node => {
        const parent = node?.parentElement;
        if (!parent) return true;

        return Boolean(
            parent.closest(
                "script,style,noscript,textarea," +
                "[data-admin-i18n-skip]"
            )
        );
    };

    const isProtectedReportsUserElement = element => {
        if (
            !isReportsScope ||
            !(element instanceof Element)
        ) {
            return false;
        }

        return Boolean(
            element.closest(
                ".ar-case-card__heading h3," +
                ".ar-case-hero__main h1," +
                ".ar-report-text," +
                ".ar-mini-person strong," +
                ".ar-person-scene__content h3," +
                ".ar-reference-copy h3," +
                ".ar-reference-copy p," +
                ".ar-reference-copy span," +
                ".ar-case-card__heading small," +
                ".ar-timeline-card > p," +
                ".ar-timeline-card__head small," +
                ".ar-internal-notes__list p," +
                ".ar-internal-notes__list article > span"
            )
        );
    };

    const isProtectedAdvertisementsUserElement = element => {
        if (
            !isAdvertisementsScope ||
            !(element instanceof Element)
        ) {
            return false;
        }

        return Boolean(
            element.closest(
                ".admx-ad-row__title h3," +
                ".admx-ad-row__main > p," +
                ".admx-plan-description," +
                ".admx-pricing-card__head h3," +
                ".admx-luxury-review__visual-content h3," +
                ".admx-luxury-review__visual-content p," +
                "#admxTopAdsTable tbody td:nth-child(1) strong," +
                "#admxTopAdsTable tbody td:nth-child(1) small," +
                "#admxTopAdsTable tbody td:nth-child(2) strong"
            )
        );
    };

    const isProtectedFinanceUserElement = element => {
        if (
            !isFinanceScope ||
            !(element instanceof Element)
        ) {
            return false;
        }

        return Boolean(
            element.closest(
                // Names and place names entered/stored by users.
                ".afo-place-cell strong," +
                ".afo-place-cell small," +
                ".acg-place__text strong," +
                ".acg-place__text small," +
                "[data-acg-place-name]," +
                "[data-acg-owner-name]," +
                ".atr-place strong," +
                ".atr-place small," +
                "[data-topup-place]," +
                "[data-topup-owner]," +
                "[data-topup-wallet]," +
                "[data-topup-beneficiary]," +
                "[data-topup-reference]," +
                ".aft-place strong," +
                ".aft-place small," +
                "[data-transaction-place]," +
                "[data-transaction-owner]," +
                "[data-transaction-created-by]," +
                "[data-transaction-reviewed-by]," +
                ".afm-wallet-card__brand strong," +
                ".afm-wallet-card__brand small," +
                ".afm-wallet-card__number strong," +

                // Actual owner/place/wallet options from DB. Empty options are system UI.
                "select[name='OwnerId'] option:not([value=''])," +
                "select[name='PlaceId'] option:not([value=''])," +
                "select[name='PlatformWalletId'] option:not([value=''])," +
                "#platformWalletType option:not([value=''])," +

                // User/admin free text. Known system prefixes are translated before
                // this guard; unknown Arabic here must remain exactly as stored.
                "[data-transaction-notes]," +
                ".afr-cell," +
                ".afp-table td," +
                ".afp-field strong," +
                ".afp-notes p," +
                ".afp-generated-box > div:nth-child(2) strong," +
                ".afp-authenticity > div:first-child strong"
            )
        );
    };

    const isProtectedProfileUserElement = element => {
        if (!isProfileScope || !(element instanceof Element)) return false;
        return Boolean(
            element.closest(
                ".profile-name," +
                "#mainAvatarInitial," +
                "#modalAvatarInitial"
            )
        );
    };

    const isNotificationContentElement = element =>
        Boolean(
            element instanceof Element &&
            element.closest(
                ".admin-notification-item__title," +
                ".admin-notification-item__text," +
                ".admin-notification-center-item__title-wrap strong," +
                ".admin-notification-center-item__text"
            )
        );

    const isProtectedUserElement = element =>
        isProtectedReportsUserElement(element) ||
        isProtectedAdvertisementsUserElement(element) ||
        isProtectedFinanceUserElement(element) ||
        isProtectedProfileUserElement(element);


    const translateTextNode = node => {
        if (
            !isEnglish ||
            !node ||
            node.nodeType !== Node.TEXT_NODE ||
            shouldSkipBaseTextNode(node)
        ) {
            return;
        }

        const parent = node.parentElement;
        const original = String(node.nodeValue ?? "");

        if (!normalize(original)) return;

        // Notification title/body can contain real user-written content.
        // Leave these nodes untouched here; strictNotificationsSweep handles
        // them with the notification type and preserves user text safely.
        if (
            (isNotificationsPage || hasAdminNotificationUi()) &&
            isNotificationContentElement(parent)
        ) {
            return;
        }

        const isSystemMessageSurface = Boolean(
            parent?.closest(
                ".swal2-validation-message," +
                ".ar-modal-error," +
                ".ar-toast"
            )
        );

        let next = isSystemMessageSurface
            ? translateSystemValue(
                original,
                reportsFallback()
              )
            : translateValue(original);

        if (next !== original) {
            node.nodeValue = next;
            return;
        }

        if (!containsArabic(original)) {
            return;
        }

        // The only Arabic allowed to remain in English Reports is
        // free text entered by a user/admin/owner/customer.
        if (isProtectedUserElement(parent)) {
            return;
        }

        if (isMasterEntitiesScope) {
            const fragments = translateKnownFragments(original);
            if (fragments !== original) {
                node.nodeValue = fragments;
                return;
            }
        }

        if (isProfileScope) {
            const fragments = translateKnownFragments(original);
            node.nodeValue = containsArabic(fragments)
                ? profileFallback()
                : fragments;
            return;
        }

        if (isReportsScope) {
            node.nodeValue = translateSystemValue(
                original,
                reportsFallback()
            );
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

        const translated = translateValue(
            document.title
        );

        document.title =
            isReportsScope && containsArabic(translated)
                ? translateSystemValue(
                    translated,
                    "Reports | Admin Dashboard"
                  )
                : isFinanceScope && containsArabic(translated)
                    ? "Financial Management | Admin Dashboard"
                    : isMasterEntitiesScope && containsArabic(translated)
                        ? translateKnownFragments(translated)
                        : isProfileScope && containsArabic(translated)
                            ? "Admin Profile | Admin Dashboard"
                            : isNotificationsPage && containsArabic(translated)
                                ? "Admin Notification Center | Admin Dashboard"
                                : translated;
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

        const originalValidation =
            typeof window.Swal.showValidationMessage === "function"
                ? window.Swal.showValidationMessage.bind(window.Swal)
                : null;

        if (originalValidation) {
            window.Swal.showValidationMessage = message =>
                originalValidation(
                    translateSystemValue(
                        message,
                        "Please review the entered data."
                    )
                );
        }

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
                    "confirmButtonText",
                    "cancelButtonText",
                    "denyButtonText",
                    "footer",
                    "inputLabel",
                    "inputPlaceholder"
                ].forEach(key => {
                    if (typeof options[key] === "string") {
                        options[key] =
                            translateValue(options[key]);
                    }
                });

                // SweetAlert text in Ads/Reports is a system message, not user content.
                if (typeof options.text === "string") {
                    options.text =
                        (isAdvertisementsScope || isReportsScope)
                            ? translateSystemValue(
                                options.text,
                                "Unable to complete the operation."
                              )
                            : translateValue(options.text);
                }

                const originalDidOpen = options.didOpen;
                options.didOpen = popup => {
                    popup?.setAttribute("dir", "ltr");

                    if (typeof originalDidOpen === "function") {
                        originalDidOpen(popup);
                    }

                    translateTree(popup);

                    window.setTimeout(() => translateTree(popup), 0);
                    window.setTimeout(() => translateTree(popup), 40);
                };

                const originalDidRender = options.didRender;
                options.didRender = popup => {
                    popup?.setAttribute("dir", "ltr");

                    if (typeof originalDidRender === "function") {
                        originalDidRender(popup);
                    }

                    translateTree(popup);
                };

                const result = originalFire(options);

                queueMicrotask(() => {
                    const popup = window.Swal?.getPopup?.();
                    if (popup) {
                        popup.setAttribute("dir", "ltr");
                        translateTree(popup);
                    }
                });

                return result;
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


    const syncScopedPageDirection = () => {
        if (!isEnglish) return;

        [
            document.getElementById("adminAdvertisementsApp"),
            document.getElementById("adminReportsCenter"),
            document.getElementById("adminReportDetails"),
            document.querySelector("[data-master-entities-root]"),
            document.getElementById("addCategoryOffcanvas"),
            document.getElementById("addCityOffcanvas")
        ].filter(Boolean).forEach(element => {
            element.setAttribute("dir", "ltr");
        });
    };

    const strictReportsZeroArabicSweep = root => {
        if (
            !isEnglish ||
            !isReportsScope ||
            !root
        ) {
            return;
        }

        const scope =
            root instanceof Element
                ? root
                : document.body;

        if (!scope) return;

        const walker = document.createTreeWalker(
            scope,
            NodeFilter.SHOW_TEXT
        );

        let node = walker.nextNode();

        while (node) {
            const parent = node.parentElement;
            const value = String(node.nodeValue ?? "");

            if (
                parent &&
                normalize(value) &&
                containsArabic(value) &&
                !shouldSkipBaseTextNode(node) &&
                !isProtectedUserElement(parent)
            ) {
                node.nodeValue = translateSystemValue(
                    value,
                    reportsFallback()
                );
            }

            node = walker.nextNode();
        }

        scope.querySelectorAll?.("*").forEach(element => {
            translatedAttributes.forEach(attributeName => {
                if (!element.hasAttribute(attributeName)) return;

                const value = element.getAttribute(attributeName);

                if (
                    containsArabic(value) &&
                    !isProtectedUserElement(element)
                ) {
                    element.setAttribute(
                        attributeName,
                        translateSystemValue(
                            value,
                            reportsFallback()
                        )
                    );
                }
            });
        });
    };

    const ensureFinanceLtrStyle = () => {
        if (!isEnglish || !isFinanceScope) return;

        if (!document.getElementById("admin-finance-i18n-ltr-style")) {
            const style = document.createElement("style");
            style.id = "admin-finance-i18n-ltr-style";
            style.textContent = `
                html[dir="ltr"] #adminFinancialsRoot,
                html[dir="ltr"] .acg-modal,
                html[dir="ltr"] .atr-modal,
                html[dir="ltr"] .aft-modal,
                html[dir="ltr"] .afm-modal,
                html[dir="ltr"] .af-management-drawer,
                html[dir="ltr"] body.afp {
                    direction: ltr !important;
                    text-align: left;
                }

                html[dir="ltr"] .acg-modal__dialog,
                html[dir="ltr"] .atr-modal__dialog,
                html[dir="ltr"] .aft-modal__dialog,
                html[dir="ltr"] .afm-modal__dialog {
                    direction: ltr !important;
                    text-align: left !important;
                }

                html[dir="ltr"] .acg-modal input,
                html[dir="ltr"] .acg-modal textarea,
                html[dir="ltr"] .acg-modal select,
                html[dir="ltr"] .atr-modal input,
                html[dir="ltr"] .atr-modal textarea,
                html[dir="ltr"] .atr-modal select,
                html[dir="ltr"] .aft-modal input,
                html[dir="ltr"] .aft-modal textarea,
                html[dir="ltr"] .aft-modal select,
                html[dir="ltr"] .afm-modal input,
                html[dir="ltr"] .afm-modal textarea,
                html[dir="ltr"] .afm-modal select {
                    direction: ltr !important;
                    text-align: left !important;
                }

                /* English sidebar is on the left, so Finance Settings drawer
                   opens from the OPPOSITE side: the RIGHT. */
                html[dir="ltr"] body.dashboard-body .af-management-drawer {
                    left: auto !important;
                    right: -100vw !important;
                    transition: right .28s cubic-bezier(.2,.8,.2,1),
                                opacity .20s ease,
                                visibility 0s linear .28s !important;
                }

                html[dir="ltr"] body.dashboard-body .af-management-drawer.is-open,
                html[dir="ltr"] body.dashboard-body .af-management-drawer[aria-hidden="false"] {
                    left: auto !important;
                    right: 12px !important;
                    transition: right .28s cubic-bezier(.2,.8,.2,1),
                                opacity .20s ease,
                                visibility 0s !important;
                }

                @media (max-width: 700px) {
                    html[dir="ltr"] body.dashboard-body .af-management-drawer {
                        left: auto !important;
                        right: -100vw !important;
                    }

                    html[dir="ltr"] body.dashboard-body .af-management-drawer.is-open,
                    html[dir="ltr"] body.dashboard-body .af-management-drawer[aria-hidden="false"] {
                        left: auto !important;
                        right: 8px !important;
                    }
                }

                html[dir="ltr"] .acg-commission-row {
                    text-align: left !important;
                }
            `;
            document.head.appendChild(style);
        }

        document
            .querySelectorAll(
                "#adminFinancialsRoot," +
                ".acg-modal,.atr-modal,.aft-modal,.afm-modal," +
                ".af-management-drawer"
            )
            .forEach(element => {
                element.setAttribute("dir", "ltr");
            });
    };

    const strictFinanceZeroArabicSweep = root => {
        if (
            !isEnglish ||
            !isFinanceScope ||
            !root
        ) {
            return;
        }

        ensureFinanceLtrStyle();

        const scope =
            root instanceof Element
                ? root
                : document.body;

        if (!scope) return;

        const walker = document.createTreeWalker(
            scope,
            NodeFilter.SHOW_TEXT
        );

        let node = walker.nextNode();

        while (node) {
            const parent = node.parentElement;
            const original = String(node.nodeValue ?? "");

            if (
                parent &&
                normalize(original) &&
                containsArabic(original) &&
                !shouldSkipBaseTextNode(node)
            ) {
                // Try an exact / numeric Finance translation first.
                const translated = translateValue(original);

                if (translated !== original) {
                    node.nodeValue = translated;
                } else if (!isProtectedFinanceUserElement(parent)) {
                    // A SYSTEM-owned finance string must never remain Arabic.
                    const fragments = translateKnownFragments(original);
                    node.nodeValue = containsArabic(fragments)
                        ? financeFallback()
                        : fragments;
                }
            }

            node = walker.nextNode();
        }

        scope.querySelectorAll?.("*").forEach(element => {
            translatedAttributes.forEach(attributeName => {
                if (!element.hasAttribute(attributeName)) return;

                const original = element.getAttribute(attributeName);
                if (!containsArabic(original)) return;

                const translated = translateValue(original);

                if (translated !== original) {
                    element.setAttribute(attributeName, translated);
                } else if (!isProtectedFinanceUserElement(element)) {
                    const fragments = translateKnownFragments(original);
                    element.setAttribute(
                        attributeName,
                        containsArabic(fragments)
                            ? financeFallback()
                            : fragments
                    );
                }
            });
        });
    };

    const ensureMasterEntitiesLtrStyle = () => {
        if (!isEnglish || !isMasterEntitiesScope) return;

        if (!document.getElementById("admin-master-entities-i18n-ltr-style")) {
            const style = document.createElement("style");
            style.id = "admin-master-entities-i18n-ltr-style";
            style.textContent = `
                html[dir="ltr"] [data-master-entities-root],
                html[dir="ltr"] #addCategoryOffcanvas,
                html[dir="ltr"] #addCityOffcanvas {
                    direction: ltr !important;
                    text-align: left !important;
                }

                html[dir="ltr"] #addCategoryOffcanvas.offcanvas-end,
                html[dir="ltr"] #addCityOffcanvas.offcanvas-end {
                    left: auto !important;
                    right: 0 !important;
                    transform: translateX(100%);
                }

                html[dir="ltr"] #addCategoryOffcanvas.offcanvas-end.show,
                html[dir="ltr"] #addCategoryOffcanvas.offcanvas-end.showing,
                html[dir="ltr"] #addCityOffcanvas.offcanvas-end.show,
                html[dir="ltr"] #addCityOffcanvas.offcanvas-end.showing {
                    transform: none !important;
                }

                html[dir="ltr"] #addCategoryOffcanvas .category-name-input,
                html[dir="ltr"] #addCityOffcanvas .city-name-input {
                    direction: rtl !important;
                    text-align: right !important;
                }

                html[dir="ltr"] #addCategoryOffcanvas .category-name-en-input,
                html[dir="ltr"] #addCityOffcanvas .city-name-en-input {
                    direction: ltr !important;
                    text-align: left !important;
                }
            `;
            document.head.appendChild(style);
        }

        [
            document.querySelector("[data-master-entities-root]"),
            document.getElementById("addCategoryOffcanvas"),
            document.getElementById("addCityOffcanvas")
        ].filter(Boolean).forEach(element => element.setAttribute("dir", "ltr"));
    };

    const ensureProfileNotificationsLtrStyle = () => {
        if (!isEnglish || (!isProfileScope && !hasAdminNotificationUi())) return;

        if (!document.getElementById("admin-profile-notifications-i18n-ltr-style")) {
            const style = document.createElement("style");
            style.id = "admin-profile-notifications-i18n-ltr-style";
            style.textContent = `
                html[dir="ltr"] [data-admin-profile-root],
                html[dir="ltr"] #avatarManageModal .modal-content,
                html[dir="ltr"] #adminNotificationCenterPage,
                html[dir="ltr"] #adminNotificationCenter .admin-notification-panel {
                    direction: ltr !important;
                    text-align: left !important;
                }

                html[dir="ltr"] #avatarManageModal .modal-header,
                html[dir="ltr"] #avatarManageModal .modal-body {
                    direction: ltr !important;
                    text-align: left !important;
                }

                html[dir="ltr"] #adminNotificationCenterPage .admin-notification-center-item__arrow,
                html[dir="ltr"] #adminNotificationCenter .admin-notification-item__arrow,
                html[dir="ltr"] #adminNotificationCenter .admin-notification-view-all i {
                    transform: scaleX(-1);
                }

                html[dir="ltr"] [data-admin-profile-root] #fullName,
                html[dir="ltr"] [data-admin-profile-root] #city,
                html[dir="ltr"] [data-admin-profile-root] #bio {
                    text-align: left;
                }
            `;
            document.head.appendChild(style);
        }

        document.querySelector("[data-admin-profile-root]")?.setAttribute("dir", "ltr");
        document.getElementById("avatarManageModal")?.setAttribute("dir", "ltr");
        document.getElementById("adminNotificationCenterPage")?.setAttribute("dir", "ltr");
        document.getElementById("adminNotificationCenter")?.setAttribute("dir", "ltr");
    };

    const strictProfileZeroArabicSweep = root => {
        if (!isEnglish || !isProfileScope || !root) return;
        const scope = root instanceof Element ? root : document.body;
        if (!scope) return;

        const walker = document.createTreeWalker(scope, NodeFilter.SHOW_TEXT);
        let node = walker.nextNode();
        while (node) {
            const parent = node.parentElement;
            const original = String(node.nodeValue ?? "");
            if (
                parent && normalize(original) && containsArabic(original) &&
                !shouldSkipBaseTextNode(node) &&
                !isProtectedProfileUserElement(parent)
            ) {
                const translated = translateValue(original);
                if (translated !== original) {
                    node.nodeValue = translated;
                } else {
                    const fragments = translateKnownFragments(original);
                    node.nodeValue = containsArabic(fragments)
                        ? profileFallback()
                        : fragments;
                }
            }
            node = walker.nextNode();
        }

        scope.querySelectorAll?.("*").forEach(element => {
            translatedAttributes.forEach(attributeName => {
                if (!element.hasAttribute(attributeName)) return;
                const original = element.getAttribute(attributeName);
                if (!containsArabic(original) || isProtectedProfileUserElement(element)) return;
                const translated = translateValue(original);
                const fragments = translated === original
                    ? translateKnownFragments(original)
                    : translated;
                element.setAttribute(
                    attributeName,
                    containsArabic(fragments) ? profileFallback() : fragments
                );
            });
        });
    };

    const strictNotificationsSweep = root => {
        if (!isEnglish || !hasAdminNotificationUi() || !root) return;
        const scope = root instanceof Element ? root : document.body;
        if (!scope) return;

        const notificationRoot =
            scope.closest?.("#adminNotificationCenterPage, #adminNotificationCenter") ||
            (scope.matches?.("#adminNotificationCenterPage, #adminNotificationCenter") ? scope : null);

        const roots = notificationRoot
            ? [notificationRoot]
            : [
                scope.querySelector?.("#adminNotificationCenterPage"),
                scope.querySelector?.("#adminNotificationCenter")
              ].filter(Boolean);

        roots.forEach(currentRoot => {
            const walker = document.createTreeWalker(currentRoot, NodeFilter.SHOW_TEXT);
            let node = walker.nextNode();
            while (node) {
                const parent = node.parentElement;
                const original = String(node.nodeValue ?? "");
                if (!parent || !normalize(original) || !containsArabic(original) || shouldSkipBaseTextNode(node)) {
                    node = walker.nextNode();
                    continue;
                }

                const item = parent.closest("[data-notification-type]");
                if (item && isNotificationContentElement(parent)) {
                    const field = parent.closest(
                        ".admin-notification-item__title," +
                        ".admin-notification-center-item__title-wrap strong"
                    ) ? "title" : "text";
                    node.nodeValue = translateNotificationContent(
                        original,
                        item.getAttribute("data-notification-type"),
                        field
                    );
                } else {
                    const translated = translateNotificationUiValue(original);
                    node.nodeValue = translated !== original
                        ? translated
                        : notificationFallback();
                }
                node = walker.nextNode();
            }

            currentRoot.querySelectorAll?.("*").forEach(element => {
                translatedAttributes.forEach(attributeName => {
                    if (!element.hasAttribute(attributeName)) return;
                    const original = element.getAttribute(attributeName);
                    if (!containsArabic(original)) return;
                    const translated = translateNotificationUiValue(original);
                    element.setAttribute(
                        attributeName,
                        translated !== original ? translated : notificationFallback()
                    );
                });
            });
        });
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
        syncScopedPageDirection();
        translateDocumentTitle();

        if (document.body) {
            translateTree(document.body);
            strictReportsZeroArabicSweep(
                document.body
            );
            strictFinanceZeroArabicSweep(
                document.body
            );
            strictProfileZeroArabicSweep(
                document.body
            );
            strictNotificationsSweep(
                document.body
            );
        }

        ensureFinanceLtrStyle();
        ensureMasterEntitiesLtrStyle();
        ensureProfileNotificationsLtrStyle();
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

            mutation.addedNodes.forEach(node => {
                translateTree(node);

                if (
                    isReportsScope &&
                    node instanceof Element
                ) {
                    strictReportsZeroArabicSweep(node);
                }
            });
        }

        if (isReportsScope && document.body) {
            strictReportsZeroArabicSweep(
                document.body
            );
        }

        if (isFinanceScope && document.body) {
            strictFinanceZeroArabicSweep(
                document.body
            );
        }

        if (isProfileScope && document.body) {
            strictProfileZeroArabicSweep(document.body);
        }

        if (hasAdminNotificationUi() && document.body) {
            strictNotificationsSweep(document.body);
        }

        ensureFinanceLtrStyle();
        ensureMasterEntitiesLtrStyle();
        ensureProfileNotificationsLtrStyle();
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
        syncScopedPageDirection();
        translateDocumentTitle();

        if (document.body) {
            translateTree(document.body);
            strictReportsZeroArabicSweep(
                document.body
            );
            strictFinanceZeroArabicSweep(
                document.body
            );
            strictProfileZeroArabicSweep(
                document.body
            );
            strictNotificationsSweep(
                document.body
            );
        }

        ensureFinanceLtrStyle();
        ensureMasterEntitiesLtrStyle();
        ensureProfileNotificationsLtrStyle();
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
        system: translateSystemValue,
        translateTree,
        strictReportsZeroArabicSweep,
        strictFinanceZeroArabicSweep,
        strictProfileZeroArabicSweep,
        strictNotificationsSweep,
        notification: translateNotificationContent,
        sweep: finalSweep
    });
})();
