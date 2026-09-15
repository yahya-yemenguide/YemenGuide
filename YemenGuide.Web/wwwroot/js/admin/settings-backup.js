(() => {
    "use strict";

    const root = document.getElementById("backupRecoveryCenter");
    if (!root) return;

    const endpoints = {
        dashboard: root.dataset.dashboardUrl,
        create: root.dataset.createUrl,
        verify: root.dataset.verifyUrl,
        testRestore: root.dataset.testRestoreUrl,
        delete: root.dataset.deleteUrl,
        saveSettings: root.dataset.saveSettingsUrl,
        downloadBase: root.dataset.downloadBase,
        liveRestore: root.dataset.liveRestoreUrl
    };

    let loaded = false;
    let loading = false;
    let latestData = null;

    const getToken = () =>
        document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const esc = value => {
        const el = document.createElement("span");
        el.textContent = String(value ?? "");
        return el.innerHTML;
    };

    const post = async (url, data = {}) => {
        const formData = new FormData();
        Object.entries(data).forEach(([key, value]) => {
            formData.append(key, value ?? "");
        });

        const token = getToken();
        if (token) {
            formData.append("__RequestVerificationToken", token);
        }

        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json",
                "X-Requested-With": "XMLHttpRequest",
                ...(token ? { "RequestVerificationToken": token } : {})
            },
            body: formData
        });

        const text = await response.text();
        let payload = null;

        try {
            payload = text ? JSON.parse(text) : null;
        } catch {
            payload = null;
        }

        if (!response.ok) {
            throw new Error(payload?.message || `HTTP ${response.status}`);
        }

        return payload;
    };

    const showBusy = value => root.classList.toggle("is-busy", value);

    const formatDate = value => {
        if (!value) return "—";
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return "—";

        return new Intl.DateTimeFormat("ar-YE", {
            dateStyle: "medium",
            timeStyle: "short"
        }).format(date);
    };

    const formatSize = bytes => {
        const value = Number(bytes);
        if (!Number.isFinite(value) || value <= 0) return "غير متاح";

        const units = ["B", "KB", "MB", "GB"];
        let size = value;
        let index = 0;

        while (size >= 1024 && index < units.length - 1) {
            size /= 1024;
            index++;
        }

        return `${size.toFixed(index >= 2 ? 1 : 0)} ${units[index]}`;
    };

    const statusChip = (kind, icon, text) => `
        <span class="backup-status ${kind}">
            <i class="bi ${icon}"></i>
            ${esc(text)}
        </span>
    `;

    const getVerifyChip = item => {
        if (item.verified === true) {
            return statusChip("is-good", "bi-check-circle-fill", "تم التحقق");
        }

        if (String(item.status || "").toLowerCase().includes("verificationfailed")) {
            return statusChip("is-bad", "bi-x-circle-fill", "فشل التحقق");
        }

        return statusChip("is-warn", "bi-hourglass-split", "غير متحقق");
    };

    const getRestoreChip = item => {
        if (item.testRestorePassed === true) {
            return statusChip("is-good", "bi-database-check", "ناجح");
        }

        if (item.testRestorePassed === false) {
            return statusChip("is-bad", "bi-database-x", "فشل");
        }

        return statusChip("is-warn", "bi-dash-circle", "لم يُختبر");
    };

    const renderTable = data => {
        const body = document.getElementById("backupTableBody");
        if (!body) return;

        const backups = Array.isArray(data?.backups) ? data.backups : [];

        if (!backups.length) {
            body.innerHTML = `
                <tr>
                    <td colspan="7">
                        <div class="backup-empty-row">
                            <i class="bi bi-database"></i>
                            لا توجد نسخ أنشأها النظام حتى الآن.
                        </div>
                    </td>
                </tr>`;
            return;
        }

        const liveEnabled = data?.capabilities?.canLiveRestore === true;

        body.innerHTML = backups.map(item => {
            const id = encodeURIComponent(item.id);

            const downloadButton = item.canDownload === true
                ? `<a class="backup-row-action"
                      href="${esc(endpoints.downloadBase + id)}"
                      title="تنزيل النسخة">
                       <i class="bi bi-download"></i>
                   </a>`
                : "";

            const liveButton = liveEnabled
                ? `<button type="button"
                           class="backup-row-action is-live"
                           data-backup-action="live-restore"
                           data-backup-id="${esc(item.id)}"
                           title="استعادة فعلية">
                       <i class="bi bi-arrow-counterclockwise"></i>
                   </button>`
                : "";

            return `
                <tr>
                    <td>
                        <div class="backup-file">
                            <strong title="${esc(item.fileName)}">${esc(item.fileName)}</strong>
                            <small>Full Backup</small>
                        </div>
                    </td>
                    <td>${esc(formatDate(item.createdAtUtc))}</td>
                    <td>${esc(formatSize(item.sizeBytes))}</td>
                    <td>${getVerifyChip(item)}</td>
                    <td>${getRestoreChip(item)}</td>
                    <td>${statusChip(
                        item.source === "Automatic" ? "is-good" : "is-warn",
                        item.source === "Automatic" ? "bi-robot" : "bi-person-check",
                        item.source === "Automatic" ? "تلقائي" : "يدوي"
                    )}</td>
                    <td>
                        <div class="backup-row-actions">
                            <button type="button" class="backup-row-action"
                                    data-backup-action="verify"
                                    data-backup-id="${esc(item.id)}"
                                    title="التحقق">
                                <i class="bi bi-shield-check"></i>
                            </button>
                            <button type="button" class="backup-row-action"
                                    data-backup-action="test-restore"
                                    data-backup-id="${esc(item.id)}"
                                    title="اختبار الاستعادة">
                                <i class="bi bi-database-check"></i>
                            </button>
                            ${downloadButton}
                            ${liveButton}
                            <button type="button" class="backup-row-action is-danger"
                                    data-backup-action="delete"
                                    data-backup-id="${esc(item.id)}"
                                    title="حذف">
                                <i class="bi bi-trash3"></i>
                            </button>
                        </div>
                    </td>
                </tr>`;
        }).join("");
    };

    const renderSummary = data => {
        latestData = data;

        const capabilities = data?.capabilities || {};
        const settings = data?.settings || {};
        const backups = Array.isArray(data?.backups) ? data.backups : [];
        const lastBackup = backups[0] || null;
        const lastTested = backups.find(item => item.testRestorePassed === true) || null;

        const ring = document.getElementById("backupHealthRing");
        ring?.classList.remove("is-good", "is-bad");

        if (capabilities.databaseReachable && capabilities.canBackup) {
            ring?.classList.add("is-good");
            document.getElementById("backupHealthIcon").innerHTML = '<i class="bi bi-check2-circle"></i>';
            document.getElementById("backupHealthTitle").textContent = "جاهز للحماية";
            document.getElementById("backupHealthText").textContent = "SQL Server متصل";
        } else {
            ring?.classList.add("is-bad");
            document.getElementById("backupHealthIcon").innerHTML = '<i class="bi bi-exclamation-triangle"></i>';
            document.getElementById("backupHealthTitle").textContent = "يحتاج مراجعة";
            document.getElementById("backupHealthText").textContent = "راجع الصلاحيات";
        }

        document.getElementById("backupLastBackup").textContent =
            lastBackup ? formatDate(lastBackup.createdAtUtc) : "لا توجد نسخة";

        document.getElementById("backupLastBackupSub").textContent =
            lastBackup ? lastBackup.fileName : "أنشئ أول نسخة كاملة";

        document.getElementById("backupVerifyStatus").textContent =
            lastBackup?.verified ? "سليمة" : lastBackup ? "لم تُعتمد" : "—";

        document.getElementById("backupVerifySub").textContent =
            lastBackup?.verified ? "SQL Server تحقق منها" : "RESTORE VERIFYONLY";

        document.getElementById("backupAutoStatus").textContent =
            settings.autoBackupEnabled ? "مفعّل" : "متوقف";

        document.getElementById("backupAutoSub").textContent =
            settings.autoBackupEnabled
                ? `يوميًا ${settings.dailyTime || "02:30"}`
                : "يمكن تشغيله من هنا";

        document.getElementById("backupRestoreTestStatus").textContent =
            lastTested ? "ناجح" : "لم يُجرّب";

        document.getElementById("backupRestoreTestSub").textContent =
            lastTested ? formatDate(lastTested.testRestoreAtUtc) : "قاعدة اختبار منفصلة";

        document.getElementById("backupDatabaseName").textContent =
            capabilities.databaseName || "—";

        document.getElementById("backupRecoveryModel").textContent =
            capabilities.recoveryModel || "—";

        const permission = document.getElementById("backupPermissionStatus");
        permission.textContent = capabilities.canBackup ? "متاح" : "غير متاح";
        permission.className = capabilities.canBackup ? "is-good" : "is-bad";

        const testPermission = document.getElementById("backupTestPermissionStatus");
        testPermission.textContent = capabilities.canCreateTestDatabase ? "متاح" : "غير متاح";
        testPermission.className = capabilities.canCreateTestDatabase ? "is-good" : "is-bad";

        document.getElementById("backupServerNote").textContent =
            capabilities.error || (
                capabilities.canDownloadBackupFiles
                    ? "ملفات النسخ متاحة كذلك للتنزيل من هذه الاستضافة."
                    : "يمكن لـ SQL Server إنشاء النسخ، لكن ملف .bak قد يكون على خادم قاعدة البيانات ولا يمكن تنزيله مباشرة من التطبيق."
            );

        document.getElementById("backupAutoEnabled").checked =
            settings.autoBackupEnabled === true;

        document.getElementById("backupDailyTime").value =
            settings.dailyTime || "02:30";

        document.getElementById("backupRetentionCount").value =
            settings.retentionCount ?? 7;

        document.getElementById("backupVerifyAfter").checked =
            settings.verifyAfterBackup !== false;

        const liveBadge = document.getElementById("backupLiveRestoreBadge");

        if (capabilities.canLiveRestore === true) {
            liveBadge.classList.add("is-enabled");
            liveBadge.innerHTML = '<i class="bi bi-unlock-fill"></i> متاحة بصلاحية عليا';
        } else {
            liveBadge.classList.remove("is-enabled");
            liveBadge.innerHTML = '<i class="bi bi-lock-fill"></i> مقفلة';
        }

        renderTable(data);
    };

    const load = async (force = false) => {
        if (loading || (loaded && !force)) return;
        loading = true;

        try {
            const response = await fetch(endpoints.dashboard, {
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                cache: "no-store"
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();
            renderSummary(data);
            loaded = true;
        } catch (error) {
            console.error("Backup dashboard load failed.", error);

            Swal.fire({
                icon: "error",
                title: "تعذر قراءة حالة النسخ",
                text: error?.message || "حدث خطأ غير متوقع.",
                confirmButtonColor: "#05203C"
            });
        } finally {
            loading = false;
        }
    };

    const runOperation = async options => {
        const confirmation = await Swal.fire({
            icon: options.icon || "question",
            title: options.title,
            html: options.html || options.text || "",
            showCancelButton: true,
            confirmButtonText: options.confirmText || "متابعة",
            cancelButtonText: "رجوع",
            confirmButtonColor: options.danger ? "#dc3545" : "#00A6DA",
            cancelButtonColor: "#64748b",
            reverseButtons: true,
            focusCancel: options.danger === true
        });

        if (!confirmation.isConfirmed) return;

        showBusy(true);

        Swal.fire({
            title: options.loadingTitle || "جاري التنفيذ...",
            text: options.loadingText || "لا تغلق الصفحة حتى تنتهي العملية.",
            allowOutsideClick: false,
            allowEscapeKey: false,
            didOpen: () => Swal.showLoading()
        });

        try {
            const result = await post(options.url, options.data || {});
            await load(true);

            await Swal.fire({
                icon: result?.success ? "success" : "error",
                title: result?.success ? "تمت العملية" : "تعذر التنفيذ",
                text: result?.message || "",
                confirmButtonColor: "#05203C"
            });
        } catch (error) {
            await Swal.fire({
                icon: "error",
                title: "تعذر التنفيذ",
                text: error?.message || "حدث خطأ أثناء الاتصال بالخادم.",
                confirmButtonColor: "#05203C"
            });
        } finally {
            showBusy(false);
        }
    };

    document.getElementById("backupCreateNowBtn")?.addEventListener("click", () =>
        runOperation({
            url: endpoints.create,
            title: "إنشاء نسخة كاملة الآن؟",
            html: "سيقوم SQL Server بإنشاء <b>Full Backup</b> ثم التحقق من سلامتها تلقائيًا.",
            confirmText: "إنشاء النسخة",
            loadingTitle: "جاري إنشاء النسخة...",
            loadingText: "قد تستغرق العملية بعض الوقت حسب حجم قاعدة البيانات."
        })
    );

    document.getElementById("backupRefreshBtn")?.addEventListener("click", () => load(true));

    document.getElementById("backupSaveSettingsBtn")?.addEventListener("click", async () => {
        const time = document.getElementById("backupDailyTime")?.value || "02:30";
        const retention = Number(document.getElementById("backupRetentionCount")?.value || 7);

        showBusy(true);

        try {
            const result = await post(endpoints.saveSettings, {
                autoBackupEnabled: document.getElementById("backupAutoEnabled")?.checked === true,
                dailyTime: time,
                retentionCount: retention,
                verifyAfterBackup: document.getElementById("backupVerifyAfter")?.checked === true
            });

            await load(true);

            await Swal.fire({
                icon: "success",
                title: "تم حفظ الجدولة",
                text: result?.message || "تم حفظ الإعدادات.",
                timer: 1800,
                showConfirmButton: false
            });
        } catch (error) {
            await Swal.fire({
                icon: "error",
                title: "تعذر حفظ الإعدادات",
                text: error?.message || "حدث خطأ غير متوقع.",
                confirmButtonColor: "#05203C"
            });
        } finally {
            showBusy(false);
        }
    });

    document.getElementById("backupTableBody")?.addEventListener("click", async event => {
        const button = event.target.closest("[data-backup-action]");
        if (!button) return;

        const backupId = button.dataset.backupId;
        const action = button.dataset.backupAction;
        if (!backupId) return;

        if (action === "verify") {
            await runOperation({
                url: endpoints.verify,
                data: { backupId },
                title: "التحقق من هذه النسخة؟",
                text: "سيستخدم SQL Server أمر RESTORE VERIFYONLY لفحص النسخة.",
                confirmText: "ابدأ التحقق",
                loadingTitle: "جاري التحقق..."
            });
            return;
        }

        if (action === "test-restore") {
            await runOperation({
                url: endpoints.testRestore,
                data: { backupId },
                title: "اختبار الاستعادة؟",
                html: "سيتم استعادة النسخة إلى <b>قاعدة اختبار مؤقتة</b> ثم تشغيل DBCC CHECKDB وحذف قاعدة الاختبار. قاعدة المشروع الأصلية لن تتغير.",
                confirmText: "ابدأ الاختبار",
                loadingTitle: "جاري اختبار الاستعادة...",
                loadingText: "هذه العملية قد تستغرق أكثر من إنشاء النسخة."
            });
            return;
        }

        if (action === "delete") {
            await runOperation({
                url: endpoints.delete,
                data: { backupId },
                title: "حذف النسخة؟",
                text: "سيتم حذف ملف النسخة نهائيًا إذا كان متاحًا على نفس خادم التطبيق.",
                confirmText: "نعم، حذف",
                danger: true,
                icon: "warning",
                loadingTitle: "جاري حذف النسخة..."
            });
            return;
        }

        if (action === "live-restore") {
            const dbName = latestData?.capabilities?.databaseName || "";
            const expected = `RESTORE:${dbName}`;

            const confirmation = await Swal.fire({
                icon: "warning",
                title: "استعادة قاعدة البيانات الفعلية",
                html: `هذه العملية تستبدل قاعدة البيانات الحالية بالنسخة المختارة.<br><br>اكتب:<br><b dir="ltr">${esc(expected)}</b>`,
                input: "text",
                inputAttributes: { dir: "ltr", autocomplete: "off" },
                showCancelButton: true,
                confirmButtonText: "استعادة فعلية",
                cancelButtonText: "إلغاء",
                confirmButtonColor: "#dc3545",
                reverseButtons: true,
                focusCancel: true,
                preConfirm: value => {
                    if (String(value || "").trim() !== expected) {
                        Swal.showValidationMessage("عبارة التأكيد غير مطابقة.");
                        return false;
                    }
                    return value;
                }
            });

            if (!confirmation.isConfirmed) return;

            showBusy(true);
            Swal.fire({
                title: "جاري الاستعادة الفعلية...",
                text: "لا تغلق الصفحة ولا توقف التطبيق.",
                allowOutsideClick: false,
                allowEscapeKey: false,
                didOpen: () => Swal.showLoading()
            });

            try {
                const result = await post(endpoints.liveRestore, {
                    backupId,
                    confirmation: confirmation.value
                });

                await Swal.fire({
                    icon: result?.success ? "success" : "error",
                    title: result?.success ? "تمت الاستعادة" : "فشلت الاستعادة",
                    text: result?.message || "",
                    confirmButtonColor: "#05203C"
                });

                if (result?.success) window.location.reload();
            } catch (error) {
                await Swal.fire({
                    icon: "error",
                    title: "فشلت الاستعادة",
                    text: error?.message || "حدث خطأ غير متوقع.",
                    confirmButtonColor: "#05203C"
                });
            } finally {
                showBusy(false);
            }
        }
    });

    const backupTabButton = document.querySelector('[data-target="backup-recovery-tab"]');
    backupTabButton?.addEventListener("click", () => void load(true));

    if (
        localStorage.getItem("activeSettingsTab") === "backup-recovery-tab" ||
        backupTabButton?.classList.contains("active")
    ) {
        void load();
    }
})();
