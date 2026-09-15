(function () {
    "use strict";

    const T = value =>
        window.AdminI18n?.t
            ? window.AdminI18n.t(value)
            : value;

    document.addEventListener("DOMContentLoaded", function () {
        const host = document.querySelector("[data-admin-place-wizard]");
        const source = document.getElementById("adminSharedPlaceWizardSource");
        const wizard = window.YemenGuidePlaceWizard;
        let config = {};

        if (host) {
            host.setAttribute(
                "dir",
                document.documentElement.dir || "rtl"
            );
        }

        // Translate the shared Owner form only while it is hosted
        // inside the Admin Create/FillSurvey flow.
        window.AdminI18n?.sweep?.();

        try {
            config = JSON.parse(host?.dataset.wizardConfig || "{}");
        } catch {
            config = {};
        }

        if (!source || !wizard || typeof wizard.openHtml !== "function") {
            const redirectUrl =
                config.redirectUrl ||
                "/Admin/Places/PlaceManagement";

            if (window.Swal) {
                Swal.fire({
                    icon: "error",
                    title: T("تعذر تشغيل النموذج"),
                    text: T("لم يتم تحميل ملف نموذج إضافة المنشأة المشترك."),
                    confirmButtonColor: "#00A6DA"
                }).then(function () {
                    window.location.href = redirectUrl;
                });
            } else {
                window.location.href = redirectUrl;
            }
            return;
        }

        // Final sweep before taking source.innerHTML so categories, cities,
        // labels and placeholders are already localized.
        window.AdminI18n?.translateTree?.(source);

        const html = source.innerHTML;
        source.remove();

        wizard.openHtml(html, config);
    });
})();
