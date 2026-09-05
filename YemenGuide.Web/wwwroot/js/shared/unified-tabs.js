/* ============================================================================
 * YemenGuide — Unified Tabs Keyboard & Mobile Navigation
 * المسار: wwwroot/js/shared/unified-tabs.js
 *
 * مسؤولية هذا الملف:
 * - توحيد الحالة البصرية للتبويب النشط.
 * - ضمان وجود تبويب نشط واحد فقط.
 * - مزامنة active + aria-selected + tabindex.
 * - دعم الأسهم + Home + End.
 * - توسيط التبويب النشط بسلاسة على الهاتف.
 *
 * ملاحظة:
 * هذا الملف لا يجلب محتوى التبويبات ولا يغيّر data-url.
 * منطق AJAX يبقى داخل ملف الصفحة مثل manage-places-shell.js.
 * ========================================================================== */
(function (window, document) {
    "use strict";

    const CONTAINER_SELECTOR = "[data-yg-tabs], .yg-unified-tabs";
    const TAB_SELECTOR = ".tab-btn, [role='tab']";

    function isRtl(element) {
        return (
            element.closest("[dir]")?.getAttribute("dir") ||
            document.documentElement.dir ||
            "rtl"
        ) === "rtl";
    }

    function centerTab(tab, smooth = true) {
        if (!tab) return;

        const reduceMotion = window.matchMedia?.(
            "(prefers-reduced-motion: reduce)"
        )?.matches;

        tab.scrollIntoView({
            behavior: smooth && !reduceMotion ? "smooth" : "auto",
            block: "nearest",
            inline: "center"
        });
    }

    function syncActiveState(tabs, activeTab) {
        if (!tabs.length || !activeTab) return;

        tabs.forEach(tab => {
            const isActive = tab === activeTab;

            // مهم جدًا: لا نترك aria-selected قديمة على تبويب سابق.
            tab.classList.toggle("active", isActive);
            tab.setAttribute("aria-selected", isActive ? "true" : "false");
            tab.setAttribute("tabindex", isActive ? "0" : "-1");
        });
    }

    function initializeTabs(container) {
        if (!container || container.dataset.ygTabsReady === "true") return;

        const tabs = Array.from(container.querySelectorAll(TAB_SELECTOR));
        if (tabs.length < 2) return;

        container.dataset.ygTabsReady = "true";

        if (!container.hasAttribute("role")) {
            container.setAttribute("role", "tablist");
        }

        // عند التحميل نعتمد active فقط كمصدر الحقيقة.
        // إذا لم يوجد، نفعّل أول تبويب.
        const initialActive =
            tabs.find(tab => tab.classList.contains("active")) || tabs[0];

        syncActiveState(tabs, initialActive);

        tabs.forEach((tab, index) => {
            if (!tab.hasAttribute("role")) {
                tab.setAttribute("role", "tab");
            }

            tab.addEventListener("click", function () {
                // التفعيل بصريًا يتم فورًا قبل أي AJAX أو تحميل محتوى.
                // هكذا يستحيل بقاء تبويبين نشطين في نفس الوقت.
                syncActiveState(tabs, this);

                if (window.innerWidth < 992) {
                    centerTab(this, true);
                }
            });

            tab.addEventListener("keydown", function (event) {
                if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
                    return;
                }

                event.preventDefault();

                const rtl = isRtl(container);
                let nextIndex = index;

                if (event.key === "Home") {
                    nextIndex = 0;
                } else if (event.key === "End") {
                    nextIndex = tabs.length - 1;
                } else if (event.key === "ArrowLeft") {
                    nextIndex = rtl
                        ? (index + 1) % tabs.length
                        : (index - 1 + tabs.length) % tabs.length;
                } else if (event.key === "ArrowRight") {
                    nextIndex = rtl
                        ? (index - 1 + tabs.length) % tabs.length
                        : (index + 1) % tabs.length;
                }

                const nextTab = tabs[nextIndex];
                if (!nextTab) return;

                nextTab.focus({ preventScroll: true });
                centerTab(nextTab, true);
                nextTab.click();
            });
        });
    }

    function initializeAll(root = document) {
        root.querySelectorAll?.(CONTAINER_SELECTOR).forEach(initializeTabs);
    }

    function start() {
        initializeAll(document);

        const observer = new MutationObserver(records => {
            for (const record of records) {
                record.addedNodes.forEach(node => {
                    if (!(node instanceof Element)) return;

                    if (node.matches?.(CONTAINER_SELECTOR)) {
                        initializeTabs(node);
                    }

                    initializeAll(node);
                });
            }
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})(window, document);
