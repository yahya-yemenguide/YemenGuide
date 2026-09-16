(() => {
    "use strict";

    const root =
        document.getElementById(
            "backupRecoveryCenter"
        );

    if (!root) return;

    const endpoints = {
        dashboard:
            root.dataset.dashboardUrl,
        create:
            root.dataset.createUrl,
        saveSettings:
            root.dataset.saveSettingsUrl,
        downloadBase:
            root.dataset.downloadBase,
        restoreStored:
            root.dataset.restoreStoredUrl,
        restoreUploaded:
            root.dataset.restoreUploadedUrl,
        delete:
            root.dataset.deleteUrl
    };

    let loaded = false;
    let loading = false;
    let latestData = null;

    const getToken = () =>
        document.querySelector(
            'input[name="__RequestVerificationToken"]'
        )?.value || "";

    const esc = value => {
        const span =
            document.createElement("span");

        span.textContent =
            String(value ?? "");

        return span.innerHTML;
    };

    const setBusy = value => {
        loading = value;

        root.classList.toggle(
            "is-busy",
            value
        );
    };

    const postForm = async (
        url,
        formData
    ) => {
        const token =
            getToken();

        if (
            token &&
            !formData.has(
                "__RequestVerificationToken"
            )
        ) {
            formData.append(
                "__RequestVerificationToken",
                token
            );
        }

        const response =
            await fetch(
                url,
                {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Accept":
                            "application/json",
                        "X-Requested-With":
                            "XMLHttpRequest",
                        ...(token
                            ? {
                                "RequestVerificationToken":
                                    token
                            }
                            : {})
                    },
                    body: formData
                }
            );

        const text =
            await response.text();

        let payload = null;

        try {
            payload =
                text
                    ? JSON.parse(text)
                    : null;
        }
        catch {
            payload = null;
        }

        if (!response.ok) {
            throw new Error(
                payload?.message ||
                `فشل الطلب (HTTP ${response.status}).`
            );
        }

        return payload;
    };

    const post = async (
        url,
        values = {}
    ) => {
        const formData =
            new FormData();

        Object.entries(values)
            .forEach(([key, value]) => {
                formData.append(
                    key,
                    value ?? ""
                );
            });

        return postForm(
            url,
            formData
        );
    };

    const formatDate = value => {
        if (!value) return "—";

        const date =
            new Date(value);

        if (
            Number.isNaN(
                date.getTime()
            )
        ) {
            return "—";
        }

        return new Intl.DateTimeFormat(
            "ar-YE",
            {
                dateStyle: "medium",
                timeStyle: "short"
            }
        ).format(date);
    };

    const formatSize = bytes => {
        const value =
            Number(bytes);

        if (
            !Number.isFinite(value) ||
            value <= 0
        ) {
            return "غير متاح";
        }

        const units =
            ["B", "KB", "MB", "GB", "TB"];

        let size = value;
        let index = 0;

        while (
            size >= 1024 &&
            index < units.length - 1
        ) {
            size /= 1024;
            index++;
        }

        return `${size.toFixed(
            index >= 2 ? 1 : 0
        )} ${units[index]}`;
    };

    const getSourceLabel = source => {
        if (source === "PreRestore") {
            return "نسخة أمان";
        }

        if (source === "Automatic") {
            return "نسخة تلقائية";
        }

        return "نسخة يدوية";
    };

    const getDisplayName = item => {
        const value =
            String(item?.displayName || "").trim();

        return value ||
            getSourceLabel(item?.source);
    };

    const buildSuggestedFileName = () => {
        const now = new Date();

        const two = value =>
            String(value)
                .padStart(2, "0");

        const stamp =
            `${now.getFullYear()}` +
            `${two(now.getMonth() + 1)}` +
            `${two(now.getDate())}_` +
            `${two(now.getHours())}` +
            `${two(now.getMinutes())}` +
            `${two(now.getSeconds())}`;

        return `YemenGuide_FULL_${stamp}.bak`;
    };

    const chooseSaveHandle = async () => {
        if (
            typeof window.showSaveFilePicker !==
            "function"
        ) {
            return null;
        }

        try {
            return await window.showSaveFilePicker({
                suggestedName:
                    buildSuggestedFileName(),
                types: [
                    {
                        description:
                            "SQL Server Backup",
                        accept: {
                            "application/octet-stream":
                                [".bak"]
                        }
                    }
                ],
                excludeAcceptAllOption: false
            });
        }
        catch (error) {
            if (
                error?.name ===
                "AbortError"
            ) {
                return undefined;
            }

            throw error;
        }
    };

    const saveDownloadToHandle = async (
        downloadUrl,
        fileHandle
    ) => {
        const response =
            await fetch(
                downloadUrl,
                {
                    method: "GET",
                    credentials: "same-origin",
                    cache: "no-store"
                }
            );

        if (!response.ok) {
            throw new Error(
                "تم إنشاء النسخة، لكن تعذر قراءة ملفها من الخادم لتنزيله."
            );
        }

        const writable =
            await fileHandle.createWritable();

        if (
            response.body &&
            typeof response.body.pipeTo ===
            "function"
        ) {
            await response.body.pipeTo(
                writable
            );

            return;
        }

        const blob =
            await response.blob();

        await writable.write(blob);
        await writable.close();
    };

    const triggerNormalDownload = (
        downloadUrl,
        fileName
    ) => {
        const anchor =
            document.createElement("a");

        anchor.href =
            downloadUrl;

        anchor.download =
            fileName ||
            "YemenGuide_Backup.bak";

        anchor.style.display =
            "none";

        document.body.appendChild(
            anchor
        );

        anchor.click();
        anchor.remove();
    };

    const renderReadyState = data => {
        const capabilities =
            data?.capabilities || {};

        const readyBadge =
            document.getElementById(
                "backupReadyBadge"
            );

        const createButton =
            document.getElementById(
                "backupCreateNowBtn"
            );

        const restoreButton =
            document.getElementById(
                "backupRestoreFromFileBtn"
            );

        if (!capabilities.databaseReachable) {
            readyBadge.className =
                "backup-v3__ready is-danger";

            readyBadge.innerHTML =
                '<i class="bi bi-exclamation-circle-fill"></i> تعذر الاتصال بالقاعدة';

            createButton.disabled =
                true;

            restoreButton.disabled =
                true;

            return;
        }

        if (
            !capabilities.canBackup ||
            !capabilities.canDownloadBackupFiles
        ) {
            readyBadge.className =
                "backup-v3__ready is-danger";

            readyBadge.innerHTML =
                '<i class="bi bi-exclamation-triangle-fill"></i> يحتاج صلاحية';

            createButton.disabled =
                true;
        }
        else {
            readyBadge.className =
                "backup-v3__ready is-success";

            readyBadge.innerHTML =
                '<i class="bi bi-check-circle-fill"></i> جاهز';

            createButton.disabled =
                false;
        }

        restoreButton.disabled =
            capabilities.canRestore !==
            true;
    };

    const renderSummary = data => {
        const backups =
            Array.isArray(data?.backups)
                ? data.backups
                : [];

        const last =
            backups[0] || null;

        const title =
            document.getElementById(
                "backupLastBackupTitle"
            );

        const time =
            document.getElementById(
                "backupLastBackupTime"
            );

        const status =
            document.getElementById(
                "backupLastBackupStatus"
            );

        const size =
            document.getElementById(
                "backupLastBackupSize"
            );

        const count =
            document.getElementById(
                "backupCount"
            );

        count.textContent =
            String(backups.length);

        if (!last) {
            title.textContent =
                "لا توجد نسخة حتى الآن";

            time.textContent =
                "عند إنشاء النسخة ستظهر هنا مباشرة.";

            status.textContent =
                "—";

            status.className =
                "";

            size.textContent =
                "—";

            return;
        }

        title.textContent =
            getDisplayName(last);

        time.textContent =
            formatDate(
                last.createdAtUtc
            );

        size.textContent =
            formatSize(
                last.sizeBytes
            );

        status.textContent =
            last.verified
                ? "سليمة"
                : "غير معتمدة";

        status.className =
            last.verified
                ? "is-success"
                : "is-danger";
    };

    const renderStatus = item => {
        if (item.verified === true) {
            return `
                <span class="backup-v3__status is-success">
                    <i class="bi bi-check-circle-fill"></i>
                    سليمة
                </span>
            `;
        }

        return `
            <span class="backup-v3__status is-danger">
                <i class="bi bi-exclamation-circle-fill"></i>
                غير معتمدة
            </span>
        `;
    };

    const renderTable = data => {
        const body =
            document.getElementById(
                "backupTableBody"
            );

        const backups =
            Array.isArray(data?.backups)
                ? data.backups
                : [];

        const canRestore =
            data?.capabilities
                ?.canRestore === true;

        if (!backups.length) {
            body.innerHTML = `
                <tr>
                    <td colspan="5">
                        <div class="backup-v3__empty">
                            <i class="bi bi-archive"></i>
                            لا توجد نسخ حتى الآن.
                        </div>
                    </td>
                </tr>
            `;

            return;
        }

        body.innerHTML =
            backups
                .map(item => {
                    const id =
                        encodeURIComponent(
                            item.id
                        );

                    const downloadButton =
                        item.canDownload === true
                            ? `
                                <a class="backup-v3__row-action"
                                   href="${esc(
                                       endpoints.downloadBase +
                                       id
                                   )}"
                                   title="تنزيل النسخة">
                                    <i class="bi bi-download"></i>
                                </a>
                              `
                            : "";

                    const restoreButton =
                        canRestore
                            ? `
                                <button type="button"
                                        class="backup-v3__row-action is-restore"
                                        data-backup-action="restore"
                                        data-backup-id="${esc(item.id)}"
                                        data-backup-name="${esc(getDisplayName(item))}"
                                        title="استعادة هذه النسخة">
                                    <i class="bi bi-arrow-counterclockwise"></i>
                                </button>
                              `
                            : "";

                    return `
                        <tr>
                            <td>
                                <div class="backup-v3__file">
                                    <span class="backup-v3__file-icon">
                                        <i class="bi bi-database-check"></i>
                                    </span>

                                    <div>
                                        <strong>
                                            ${esc(
                                                getDisplayName(item)
                                            )}
                                        </strong>

                                        <small title="${esc(item.fileName)}">
                                            ${esc(getSourceLabel(item.source))}
                                        </small>
                                    </div>
                                </div>
                            </td>

                            <td>
                                ${esc(
                                    formatDate(
                                        item.createdAtUtc
                                    )
                                )}
                            </td>

                            <td>
                                ${esc(
                                    formatSize(
                                        item.sizeBytes
                                    )
                                )}
                            </td>

                            <td>
                                ${renderStatus(item)}
                            </td>

                            <td>
                                <div class="backup-v3__row-actions">
                                    ${downloadButton}
                                    ${restoreButton}

                                    <button type="button"
                                            class="backup-v3__row-action is-delete"
                                            data-backup-action="delete"
                                            data-backup-id="${esc(item.id)}"
                                            title="حذف النسخة من النظام">
                                        <i class="bi bi-trash3"></i>
                                    </button>
                                </div>
                            </td>
                        </tr>
                    `;
                })
                .join("");
    };

    const renderAutomation = data => {
        const settings =
            data?.settings || {};

        const toggle =
            document.getElementById(
                "backupAutoEnabled"
            );

        const timeInput =
            document.getElementById(
                "backupDailyTime"
            );

        const label =
            document.getElementById(
                "backupAutoLabel"
            );

        if (toggle) {
            toggle.checked =
                settings.autoBackupEnabled ===
                true;
        }

        if (timeInput) {
            timeInput.value =
                settings.dailyTime ||
                "02:00";
        }

        if (label) {
            label.textContent =
                settings.autoBackupEnabled === true
                    ? "مفعّل"
                    : "غير مفعّل";
        }
    };

    const render = data => {
        latestData = data;

        renderReadyState(data);
        renderSummary(data);
        renderAutomation(data);
        renderTable(data);
    };

    const load = async (
        force = false
    ) => {
        if (
            loading ||
            (loaded && !force)
        ) {
            return;
        }

        loading = true;

        try {
            const response =
                await fetch(
                    endpoints.dashboard,
                    {
                        credentials:
                            "same-origin",
                        headers: {
                            "Accept":
                                "application/json",
                            "X-Requested-With":
                                "XMLHttpRequest"
                        },
                        cache: "no-store"
                    }
                );

            if (!response.ok) {
                throw new Error(
                    `HTTP ${response.status}`
                );
            }

            const data =
                await response.json();

            render(data);
            loaded = true;
        }
        catch (error) {
            console.error(
                "Backup dashboard load failed.",
                error
            );

            Swal.fire({
                icon: "error",
                title:
                    "تعذر تحميل النسخ",
                text:
                    error?.message ||
                    "حدث خطأ غير متوقع.",
                confirmButtonColor:
                    "#00A6DA"
            });
        }
        finally {
            loading = false;
        }
    };

    const askRestoreConfirmation =
        async sourceText => {
            const result =
                await Swal.fire({
                    icon: "warning",
                    title:
                        "تأكيد الاستعادة الفعلية",
                    html:
                        `سيتم إرجاع قاعدة البيانات إلى حالة <b>${esc(sourceText)}</b>.<br>` +
                        `قبل الاستعادة سيأخذ النظام نسخة أمان للحالة الحالية تلقائيًا.<br><br>` +
                        `للتأكيد اكتب كلمة <b>استعادة</b>.`,
                    input: "text",
                    inputPlaceholder:
                        "اكتب: استعادة",
                    inputAttributes: {
                        autocomplete:
                            "off"
                    },
                    showCancelButton:
                        true,
                    confirmButtonText:
                        "استعادة",
                    cancelButtonText:
                        "إلغاء",
                    confirmButtonColor:
                        "#FF5A5F",
                    cancelButtonColor:
                        "#71808C",
                    reverseButtons: true,
                    focusCancel: true,
                    preConfirm: value => {
                        if (
                            String(value || "")
                                .trim() !==
                            "استعادة"
                        ) {
                            Swal.showValidationMessage(
                                "اكتب كلمة «استعادة» كما هي."
                            );

                            return false;
                        }

                        return value;
                    }
                });

            return result.isConfirmed
                ? result.value
                : null;
        };

    const createAndSaveBackup =
        async () => {
            if (
                loading ||
                latestData
                    ?.capabilities
                    ?.canBackup !== true
            ) {
                return;
            }

            let saveHandle = null;

            try {
                saveHandle =
                    await chooseSaveHandle();

                // undefined = المستخدم أغلق نافذة اختيار مكان الحفظ.
                if (
                    saveHandle ===
                    undefined
                ) {
                    return;
                }
            }
            catch (error) {
                await Swal.fire({
                    icon: "error",
                    title:
                        "تعذر فتح نافذة الحفظ",
                    text:
                        error?.message ||
                        "جرّب مرة أخرى.",
                    confirmButtonColor:
                        "#00A6DA"
                });

                return;
            }

            setBusy(true);

            Swal.fire({
                title:
                    "جاري إنشاء النسخة...",
                text:
                    "لا تغلق الصفحة حتى تنتهي العملية.",
                allowOutsideClick:
                    false,
                allowEscapeKey:
                    false,
                didOpen: () =>
                    Swal.showLoading()
            });

            try {
                const chosenFileName =
                    saveHandle?.name ||
                    buildSuggestedFileName();

                const result =
                    await post(
                        endpoints.create,
                        {
                            displayName:
                                chosenFileName
                        }
                    );

                if (!result?.success) {
                    throw new Error(
                        result?.message ||
                        "تعذر إنشاء النسخة."
                    );
                }

                if (!result.downloadUrl) {
                    throw new Error(
                        "تم إنشاء النسخة، لكن لم يرجع الخادم رابط التنزيل."
                    );
                }

                let savedToDevice = false;

                if (saveHandle) {
                    try {
                        await saveDownloadToHandle(
                            result.downloadUrl,
                            saveHandle
                        );

                        savedToDevice = true;
                    }
                    catch (saveError) {
                        console.error(
                            "Save picker write failed.",
                            saveError
                        );
                    }
                }

                if (!savedToDevice) {
                    triggerNormalDownload(
                        result.downloadUrl,
                        result.displayName
                            ? `${result.displayName}.bak`
                            : result.fileName
                    );
                }

                await load(true);

                await Swal.fire({
                    icon: "success",
                    title:
                        "تم إنشاء النسخة",
                    html:
                        savedToDevice
                            ? "تم إنشاء النسخة والتحقق منها وحفظ الملف في المكان الذي اخترته."
                            : "تم إنشاء النسخة والتحقق منها. بدأ المتصفح تنزيل ملف النسخة.",
                    confirmButtonColor:
                        "#00A6DA"
                });
            }
            catch (error) {
                await load(true);

                await Swal.fire({
                    icon: "error",
                    title:
                        "تعذر إنشاء النسخة",
                    text:
                        error?.message ||
                        "حدث خطأ أثناء العملية.",
                    confirmButtonColor:
                        "#00A6DA"
                });
            }
            finally {
                setBusy(false);
            }
        };

    const restoreFromStored =
        async (backupId, backupName) => {
            const confirmation =
                await askRestoreConfirmation(
                    backupName ||
                    "النسخة المختارة"
                );

            if (!confirmation) {
                return;
            }

            setBusy(true);

            Swal.fire({
                title:
                    "جاري الاستعادة...",
                text:
                    "سيتم أخذ نسخة أمان أولاً ثم استعادة النسخة المختارة.",
                allowOutsideClick:
                    false,
                allowEscapeKey:
                    false,
                didOpen: () =>
                    Swal.showLoading()
            });

            try {
                const result =
                    await post(
                        endpoints.restoreStored,
                        {
                            backupId,
                            confirmation
                        }
                    );

                await Swal.fire({
                    icon:
                        result?.success
                            ? "success"
                            : "error",
                    title:
                        result?.success
                            ? "تمت الاستعادة"
                            : "فشلت الاستعادة",
                    text:
                        result?.message ||
                        "",
                    confirmButtonColor:
                        "#00A6DA"
                });

                if (result?.success) {
                    window.location.reload();
                }
                else {
                    await load(true);
                }
            }
            catch (error) {
                await Swal.fire({
                    icon: "error",
                    title:
                        "فشلت الاستعادة",
                    text:
                        error?.message ||
                        "حدث خطأ أثناء الاستعادة.",
                    confirmButtonColor:
                        "#00A6DA"
                });
            }
            finally {
                setBusy(false);
            }
        };

    const restoreFromUploadedFile =
        async file => {
            if (!file) {
                return;
            }

            if (
                !file.name
                    .toLowerCase()
                    .endsWith(".bak")
            ) {
                await Swal.fire({
                    icon: "warning",
                    title:
                        "ملف غير صحيح",
                    text:
                        "اختر ملف نسخة SQL Server بصيغة .bak.",
                    confirmButtonColor:
                        "#00A6DA"
                });

                return;
            }

            const confirmation =
                await askRestoreConfirmation(
                    file.name
                );

            if (!confirmation) {
                return;
            }

            const formData =
                new FormData();

            formData.append(
                "backupFile",
                file,
                file.name
            );

            formData.append(
                "confirmation",
                confirmation
            );

            setBusy(true);

            Swal.fire({
                title:
                    "جاري رفع النسخة واستعادتها...",
                text:
                    "لا تغلق الصفحة حتى تنتهي العملية.",
                allowOutsideClick:
                    false,
                allowEscapeKey:
                    false,
                didOpen: () =>
                    Swal.showLoading()
            });

            try {
                const result =
                    await postForm(
                        endpoints.restoreUploaded,
                        formData
                    );

                await Swal.fire({
                    icon:
                        result?.success
                            ? "success"
                            : "error",
                    title:
                        result?.success
                            ? "تمت الاستعادة"
                            : "فشلت الاستعادة",
                    text:
                        result?.message ||
                        "",
                    confirmButtonColor:
                        "#00A6DA"
                });

                if (result?.success) {
                    window.location.reload();
                }
                else {
                    await load(true);
                }
            }
            catch (error) {
                await Swal.fire({
                    icon: "error",
                    title:
                        "فشلت الاستعادة",
                    text:
                        error?.message ||
                        "حدث خطأ أثناء رفع أو استعادة الملف.",
                    confirmButtonColor:
                        "#00A6DA"
                });
            }
            finally {
                setBusy(false);
            }
        };

    const deleteStoredBackup =
        async backupId => {
            const answer =
                await Swal.fire({
                    icon: "warning",
                    title:
                        "حذف النسخة من النظام؟",
                    text:
                        "سيُحذف الملف المحفوظ داخل النظام فقط. أي ملف حفظته على جهازك لن يُحذف.",
                    showCancelButton:
                        true,
                    confirmButtonText:
                        "حذف",
                    cancelButtonText:
                        "إلغاء",
                    confirmButtonColor:
                        "#FF5A5F",
                    cancelButtonColor:
                        "#71808C",
                    reverseButtons:
                        true
                });

            if (!answer.isConfirmed) {
                return;
            }

            setBusy(true);

            try {
                const result =
                    await post(
                        endpoints.delete,
                        { backupId }
                    );

                await load(true);

                await Swal.fire({
                    icon:
                        result?.success
                            ? "success"
                            : "error",
                    title:
                        result?.success
                            ? "تم الحذف"
                            : "تعذر الحذف",
                    text:
                        result?.message ||
                        "",
                    timer:
                        result?.success
                            ? 1800
                            : undefined,
                    showConfirmButton:
                        !result?.success,
                    confirmButtonColor:
                        "#00A6DA"
                });
            }
            catch (error) {
                await Swal.fire({
                    icon: "error",
                    title:
                        "تعذر الحذف",
                    text:
                        error?.message ||
                        "حدث خطأ غير متوقع.",
                    confirmButtonColor:
                        "#00A6DA"
                });
            }
            finally {
                setBusy(false);
            }
        };

    const saveAutomaticSettings =
        async () => {
            const toggle =
                document.getElementById(
                    "backupAutoEnabled"
                );

            const timeInput =
                document.getElementById(
                    "backupDailyTime"
                );

            if (!timeInput?.value) {
                await Swal.fire({
                    icon: "warning",
                    title: "حدد وقت النسخ",
                    text: "اختر وقتًا يوميًا للنسخ التلقائي.",
                    confirmButtonColor: "#00A6DA"
                });
                return;
            }

            setBusy(true);

            try {
                const result =
                    await post(
                        endpoints.saveSettings,
                        {
                            autoBackupEnabled:
                                toggle?.checked === true,
                            dailyTime:
                                timeInput.value
                        }
                    );

                if (!result?.success) {
                    throw new Error(
                        result?.message ||
                        "تعذر حفظ إعدادات النسخ التلقائي."
                    );
                }

                await load(true);

                await Swal.fire({
                    icon: "success",
                    title: "تم الحفظ",
                    text: result.message || "تم حفظ الإعدادات.",
                    timer: 1700,
                    showConfirmButton: false
                });
            }
            catch (error) {
                await Swal.fire({
                    icon: "error",
                    title: "تعذر الحفظ",
                    text:
                        error?.message ||
                        "حدث خطأ أثناء حفظ الإعدادات.",
                    confirmButtonColor: "#00A6DA"
                });
            }
            finally {
                setBusy(false);
            }
        };

    document.getElementById(
        "backupAutoEnabled"
    )?.addEventListener(
        "change",
        event => {
            const label =
                document.getElementById(
                    "backupAutoLabel"
                );

            if (label) {
                label.textContent =
                    event.currentTarget.checked
                        ? "مفعّل"
                        : "غير مفعّل";
            }
        }
    );

    document.getElementById(
        "backupSaveAutoBtn"
    )?.addEventListener(
        "click",
        saveAutomaticSettings
    );

    document.getElementById(
        "backupCreateNowBtn"
    )?.addEventListener(
        "click",
        createAndSaveBackup
    );

    document.getElementById(
        "backupRestoreFromFileBtn"
    )?.addEventListener(
        "click",
        () => {
            document.getElementById(
                "backupRestoreFileInput"
            )?.click();
        }
    );

    document.getElementById(
        "backupRestoreFileInput"
    )?.addEventListener(
        "change",
        async event => {
            const input =
                event.currentTarget;

            const file =
                input.files?.[0] ||
                null;

            input.value = "";

            await restoreFromUploadedFile(
                file
            );
        }
    );

    document.getElementById(
        "backupRefreshBtn"
    )?.addEventListener(
        "click",
        () => load(true)
    );

    document.getElementById(
        "backupTableBody"
    )?.addEventListener(
        "click",
        async event => {
            const button =
                event.target.closest(
                    "[data-backup-action]"
                );

            if (!button) {
                return;
            }

            const action =
                button.dataset.backupAction;

            const backupId =
                button.dataset.backupId;

            if (!backupId) {
                return;
            }

            if (action === "restore") {
                await restoreFromStored(
                    backupId,
                    button.dataset.backupName
                );

                return;
            }

            if (action === "delete") {
                await deleteStoredBackup(
                    backupId
                );
            }
        }
    );

    const tabButton =
        document.querySelector(
            '[data-target="backup-recovery-tab"]'
        );

    tabButton?.addEventListener(
        "click",
        () => load(true)
    );

    if (
        localStorage.getItem(
            "activeSettingsTab"
        ) === "backup-recovery-tab" ||
        tabButton
            ?.classList
            .contains("active")
    ) {
        void load();
    }
})();
