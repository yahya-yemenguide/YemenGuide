(function (window, document) {
    "use strict";

    const root = document.getElementById("adminFinancialsRoot");
    const isEnglish = (root?.dataset.uiLang || document.documentElement.lang || "ar")
        .toLowerCase().startsWith("en");

    if (root) {
        root.dir = isEnglish ? "ltr" : "rtl";
    }

    const exact = new Map(Object.entries({
        "الإدارة المالية": "Financial Management",
        "العملة:": "Currency:",
        "آخر تحديث:": "Last updated:",
        "تحديث": "Refresh",
        "إدارة المالية": "Finance Settings",
        "نظرة عامة": "Overview",
        "العمولات والضمان": "Commission & Guarantee",
        "طلبات الشحن": "Top-up Requests",
        "كشف الحركات": "Transactions",
        "التقارير والمستندات": "Reports & Documents",
        "جارٍ تجهيز البيانات المالية": "Loading financial data",
        "لحظات بسيطة...": "Just a moment...",
        "الفلاتر": "Filters",
        "تطبيق": "Apply",
        "مسح": "Clear",
        "بحث": "Search",
        "الكل": "All",
        "المالك": "Owner",
        "المنشأة": "Place",
        "من": "From",
        "إلى": "To",
        "الحالة": "Status",
        "الإجراءات": "Actions",
        "التفاصيل": "Details",
        "إغلاق": "Close",
        "حفظ": "Save",
        "حفظ التغييرات": "Save changes",
        "إلغاء": "Cancel",
        "قبول": "Approve",
        "رفض": "Reject",
        "مراجعة": "Review",
        "طباعة": "Print",
        "عرض": "View",
        "العملة": "Currency",
        "الإعدادات": "Settings",
        "الإعدادات العامة": "General settings",
        "التجربة والضمان": "Trial & guarantee",
        "عمولات الفئات": "Category commissions",
        "حسابات الاستقبال": "Receiving accounts",
        "حسابات استقبال التحويلات": "Receiving accounts",
        "الإعدادات المالية": "Financial settings",
        "نسبة العمولة": "Commission rate",
        "الحد الأدنى للشحن": "Minimum top-up",
        "حد انخفاض الرصيد": "Low-balance threshold",
        "حد الدين": "Debt limit",
        "مهلة سداد الدين": "Debt grace period",
        "السماح بالدين": "Allow negative balance",
        "إيقاف الحجوزات عند عدم كفاية الرصيد": "Suspend bookings when balance is insufficient",
        "إخفاء المنشأة عند الإيقاف المالي": "Hide place when financially suspended",
        "الفترة التجريبية": "Free trial",
        "رصيد ضمان التفعيل": "Activation guarantee balance",
        "المطلوب بعد انتهاء التجربة": "Required after the trial ends",
        "بدء التشغيل": "Start operation",
        "انتهاء التجربة": "Trial ends",
        "تفعيل الضمان": "Activate guarantee",
        "اسم الفئة": "Category",
        "النسبة": "Rate",
        "حسابات المنصة": "Platform accounts",
        "الحساب الأساسي": "Primary account",
        "نشط": "Active",
        "غير نشط": "Inactive",
        "رقم الحساب": "Account number",
        "اسم المستفيد": "Beneficiary name",
        "التعليمات": "Instructions",
        "تعيين كأساسي": "Set as primary",
        "تفعيل": "Activate",
        "تعطيل": "Disable",
        "الشحنات المعتمدة": "Approved top-ups",
        "المتاح": "Available",
        "المحجوز": "Held",
        "الدين": "Debt",
        "الرصيد المتاح": "Available balance",
        "الرصيد المحجوز": "Held balance",
        "إجمالي الدين": "Total debt",
        "العمولات المخصومة": "Charged commissions",
        "عمولات المنصة المخصومة": "Charged platform commissions",
        "إجمالي العمولات": "Total commissions",
        "العمولات المحجوزة": "Held commissions",
        "العمولات المحررة": "Released commissions",
        "أحدث الحركات": "Recent transactions",
        "التنبيهات": "Alerts",
        "لا توجد تنبيهات مالية حالياً.": "No financial alerts right now.",
        "لا توجد حركات مالية.": "No financial transactions.",
        "لا توجد بيانات": "No data",
        "لا توجد نتائج مطابقة.": "No matching results.",
        "المرجع": "Reference",
        "رقم الحوالة": "Transfer reference",
        "تاريخ التحويل": "Transfer date",
        "السند": "Receipt",
        "المبلغ": "Amount",
        "مبلغ الشحن": "Top-up amount",
        "قيد المراجعة": "Pending review",
        "مقبول": "Approved",
        "مرفوض": "Rejected",
        "مكتمل": "Completed",
        "فشل": "Failed",
        "ملغي": "Cancelled",
        "معلق": "Pending",
        "شحن رصيد": "Top-up",
        "حجز عمولة": "Commission hold",
        "خصم عمولة": "Commission charge",
        "تحرير عمولة": "Commission release",
        "تسجيل دين": "Debt registered",
        "سداد دين": "Debt payment",
        "تعديل": "Adjustment",
        "الحجز": "Booking",
        "العمولة": "Commission",
        "قيمة الحجز": "Booking value",
        "حالة العمولة": "Commission status",
        "تاريخ الحجز": "Booking date",
        "عدد الحجوزات": "Bookings",
        "عمولات بحالة دين": "Commission debt",
        "الحسابات منخفضة الرصيد": "Low-balance accounts",
        "الحسابات الموقوفة": "Suspended accounts",
        "الحسابات المديونة": "Accounts in debt",
        "الحسابات النشطة": "Active accounts",
        "يحتاج شحن": "Needs top-up",
        "منخفض": "Low",
        "مديون": "In debt",
        "موقوف": "Suspended",
        "تقرير مالي رسمي": "Official financial report",
        "تاريخ الإنشاء": "Generated at",
        "أُنشئ بواسطة": "Generated by",
        "إجمالي السجلات": "Total records",
        "نطاق التقرير:": "Report scope:",
        "القسم": "Section",
        "سجل": "record",
        "لا توجد بيانات في هذا القسم.": "No data in this section.",
        "مستند مولد إلكترونيًا من الإدارة المالية.": "Electronically generated by Financial Management.",
        "رقم المستند": "Document number",
        "تاريخ المستند:": "Document date:",
        "تاريخ الإنشاء:": "Generated at:",
        "المبالغ": "Amounts",
        "ملاحظات": "Notes",
        "مستند إلكتروني موثق من النظام": "System-verified electronic document",
        "تم إنشاء هذا المستند إلكترونيًا من الإدارة المالية.": "This document was generated electronically by Financial Management.",
        "تم بنجاح": "Success",
        "تعذر التنفيذ": "Unable to complete",
        "تنبيه": "Notice",
        "معلومة": "Information",
        "إغلاق الرسالة": "Close message"
    }));

    const phrasePairs = [
        ["طلبات شحن تنتظر المراجعة", "Top-up requests awaiting review"],
        ["راجع السندات واعتمد الطلبات الصحيحة.", "Review receipts and approve valid requests."],
        ["حسابات ضمان موقوفة", "Suspended guarantee accounts"],
        ["وصلت بعض المنشآت إلى حالة إيقاف مالي.", "Some places reached financial suspension."],
        ["حركات مالية فاشلة", "Failed financial transactions"],
        ["افتح سجل الحركات لمراجعة العمليات الفاشلة.", "Open the transaction log to review failed operations."],
        ["تاريخ إرسال الطلب", "Request date"],
        ["تاريخ المراجعة", "Review date"],
        ["تاريخ الإنشاء", "Created at"],
        ["تاريخ آخر حركة", "Last transaction"],
        ["لا توجد", "No"],
        ["نتائج مطابقة", "matching results"],
        ["الإدارة المالية", "Financial Management"]
    ];

    function translateString(value) {
        if (!isEnglish || !value) return value;
        const source = String(value);
        const trimmed = source.trim();
        if (!trimmed) return source;
        if (exact.has(trimmed)) {
            return source.replace(trimmed, exact.get(trimmed));
        }
        let translated = source;
        for (const [ar, en] of phrasePairs) {
            if (translated.includes(ar)) translated = translated.split(ar).join(en);
        }
        return translated;
    }

    function translateElement(element) {
        if (!isEnglish || !element || element.nodeType !== 1) return;
        ["placeholder", "title", "aria-label"].forEach(attr => {
            if (element.hasAttribute?.(attr)) {
                const current = element.getAttribute(attr);
                const next = translateString(current);
                if (next !== current) element.setAttribute(attr, next);
            }
        });
    }

    function translateTree(node) {
        if (!isEnglish || !node) return;
        if (node.nodeType === Node.TEXT_NODE) {
            const next = translateString(node.nodeValue);
            if (next !== node.nodeValue) node.nodeValue = next;
            return;
        }
        if (node.nodeType !== Node.ELEMENT_NODE && node.nodeType !== Node.DOCUMENT_FRAGMENT_NODE) return;
        if (node.nodeType === Node.ELEMENT_NODE) translateElement(node);
        const walker = document.createTreeWalker(node, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
        let current;
        while ((current = walker.nextNode())) {
            if (current.nodeType === Node.TEXT_NODE) {
                const next = translateString(current.nodeValue);
                if (next !== current.nodeValue) current.nodeValue = next;
            } else {
                translateElement(current);
            }
        }
    }

    function installToastTranslation() {
        const Finance = window.AdminFinance;
        if (!Finance || Finance.__phase2ToastInstalled) return;
        Finance.__phase2ToastInstalled = true;
        const original = Finance.showToast;
        if (typeof original !== "function") return;
        Finance.showToast = function (message, type, title, duration) {
            return original.call(this,
                translateString(message),
                type,
                translateString(title),
                duration);
        };
    }

    function syncDirection() {
        if (!root) return;
        root.setAttribute("dir", isEnglish ? "ltr" : "rtl");
        document.querySelectorAll(".af-management-drawer, .afm, .acg-modal, .atr-modal, .aft-modal, .afm-modal")
            .forEach(el => el.setAttribute("dir", isEnglish ? "ltr" : "rtl"));
    }

    function init() {
        syncDirection();
        translateTree(document.body);
        installToastTranslation();

        const observer = new MutationObserver(mutations => {
            for (const mutation of mutations) {
                mutation.addedNodes.forEach(translateTree);
            }
            syncDirection();
            installToastTranslation();
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    document.addEventListener("adminfinance:tabloaded", function () {
        translateTree(document.getElementById("adminFinancialTabHost"));
        syncDirection();
    });
    document.addEventListener("adminfinance:managementloaded", function () {
        translateTree(document.querySelector("[data-financial-management-root]"));
        syncDirection();
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})(window, document);
