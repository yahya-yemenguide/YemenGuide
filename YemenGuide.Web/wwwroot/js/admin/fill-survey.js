(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        const host = document.querySelector("[data-admin-place-wizard]");
        const source = document.getElementById("adminSharedPlaceWizardSource");
        const wizard = window.YemenGuidePlaceWizard;
        let config = {};

        try {
            config = JSON.parse(host?.dataset.wizardConfig || "{}");
        } catch {
            config = {};
        }

        if (!source || !wizard || typeof wizard.openHtml !== "function") {
            const redirectUrl = config.redirectUrl || "/Admin/Places/PlaceManagement";

            if (window.Swal) {
                Swal.fire({
                    icon: "error",
                    title: "تعذر تشغيل النموذج",
                    text: "لم يتم تحميل ملف نموذج إضافة المنشأة المشترك.",
                    confirmButtonColor: "#00A6DA"
                }).then(function () {
                    window.location.href = redirectUrl;
                });
            } else {
                window.location.href = redirectUrl;
            }
            return;
        }

        const html = source.innerHTML;
        source.remove();
        wizard.openHtml(html, config);
    });
})();
