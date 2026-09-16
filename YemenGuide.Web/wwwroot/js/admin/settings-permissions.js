(() => {
    "use strict";

    const page =
        document.querySelector(".system-settings-page");

    if (!page) {
        return;
    }

    const permissionKeys =
        new Set(
            Array.isArray(window.YG_ADMIN_PERMISSION_KEYS)
                ? window.YG_ADMIN_PERMISSION_KEYS
                : []
        );

    const has = key =>
        permissionKeys.has(key);

    const hasAny = (...keys) =>
        keys.some(has);

    const hasPrefix = prefix =>
        [...permissionKeys]
            .some(key =>
                String(key)
                    .startsWith(prefix)
            );

    const permissions = {
        brandingView:
            hasAny(
                "Settings.Branding.View",
                "Settings.Branding.Manage"
            ),

        brandingManage:
            has("Settings.Branding.Manage"),

        heroView:
            hasAny(
                "Settings.HeroSlides.View",
                "Settings.ManageHeroSlides"
            ),

        heroManage:
            has("Settings.ManageHeroSlides"),

        policiesView:
            hasAny(
                "Settings.Policies.View",
                "Settings.Policies.Manage",
                "Finance.ManageSettings"
            ),

        policiesManage:
            has("Settings.Policies.Manage"),

        financeManage:
            has("Finance.ManageSettings"),

        contactView:
            hasAny(
                "Settings.Contact.View",
                "Settings.Contact.Manage"
            ),

        contactManage:
            has("Settings.Contact.Manage"),

        maintenanceView:
            hasAny(
                "Settings.Maintenance.View",
                "Settings.Maintenance.Manage"
            ),

        maintenanceManage:
            has("Settings.Maintenance.Manage"),

        walletsView:
            has("Settings.PaymentNetworks.View") ||
            hasPrefix("Settings.PaymentNetworks."),

        walletAdd:
            has("Settings.PaymentNetworks.Add"),

        walletEdit:
            has("Settings.PaymentNetworks.Edit"),

        walletStatus:
            has("Settings.PaymentNetworks.ChangeStatus"),

        walletDelete:
            has("Settings.PaymentNetworks.Delete"),

        walletDefaultLogo:
            has("Settings.PaymentNetworks.ManageDefaultLogo"),

        backupsView:
            has("Settings.Backups.View") ||
            hasPrefix("Settings.Backups."),

        backupCreate:
            has("Settings.Backups.Create"),

        backupDownload:
            has("Settings.Backups.Download"),

        backupRestore:
            has("Settings.Backups.Restore"),

        backupDelete:
            has("Settings.Backups.Delete"),

        backupSchedule:
            has("Settings.Backups.ManageSchedule"),

        backupCloud:
            has("Settings.Backups.ManageCloud")
    };

    const setHidden = (
        element,
        hidden
    ) => {
        if (!element) return;

        element.hidden = hidden;

        if (hidden) {
            element.style.setProperty(
                "display",
                "none",
                "important"
            );
        }
        else {
            element.style.removeProperty(
                "display"
            );
        }
    };

    const disableControl = control => {
        if (!control) return;

        control.disabled = true;
        control.setAttribute(
            "aria-disabled",
            "true"
        );
    };

    const disableControls = (
        container,
        selector =
            "input, select, textarea, button"
    ) => {
        if (!container) return;

        container
            .querySelectorAll(selector)
            .forEach(disableControl);
    };

    const hideSaveActions = pane => {
        pane
            ?.querySelectorAll(
                ".settings-save-actions"
            )
            .forEach(element =>
                setHidden(
                    element,
                    true
                )
            );
    };

    const permissionByTab = {
        "branding-tab":
            permissions.brandingView ||
            permissions.heroView,

        "policies-tab":
            permissions.policiesView,

        "contact-tab":
            permissions.contactView,

        "maintenance-tab":
            permissions.maintenanceView,

        "wallets-tab":
            permissions.walletsView,

        "backup-recovery-tab":
            permissions.backupsView
    };

    function applyTabVisibility() {
        Object.entries(
            permissionByTab
        ).forEach(
            ([target, allowed]) => {
                const button =
                    document.querySelector(
                        `.tab-btn[data-target="${target}"]`
                    );

                const pane =
                    document.querySelector(
                        `.${target}`
                    );

                setHidden(
                    button,
                    !allowed
                );

                if (!allowed) {
                    button?.classList.remove(
                        "active"
                    );

                    if (pane) {
                        pane.style.setProperty(
                            "display",
                            "none",
                            "important"
                        );
                    }
                }
            }
        );

        const saved =
            localStorage.getItem(
                "activeSettingsTab"
            );

        const savedAllowed =
            !!saved &&
            permissionByTab[saved] === true;

        const firstAllowed =
            Object.entries(
                permissionByTab
            )
                .find(([, allowed]) =>
                    allowed === true
                )
                ?.[0];

        const target =
            savedAllowed
                ? saved
                : firstAllowed;

        if (target) {
            localStorage.setItem(
                "activeSettingsTab",
                target
            );
        }
    }

    function applyBrandingPermissions() {
        const pane =
            document.querySelector(
                ".branding-tab"
            );

        if (!pane) return;

        const brandingControlIds = [
            "siteName",
            "siteNameEn",
            "siteDescription",
            "siteDescriptionEn",
            "logoFile",
            "faviconFile",
            "heroTitleMain",
            "heroTitleMainEn",
            "heroTitleSub",
            "heroTitleSubEn",
            "seasonTheme",
            "isBannerActive",
            "bannerText",
            "bannerTextEn"
        ];

        if (!permissions.brandingView) {
            brandingControlIds
                .forEach(id => {
                    const control =
                        document.getElementById(
                            id
                        );

                    const card =
                        control?.closest(
                            ".settings-card"
                        );

                    setHidden(
                        card,
                        true
                    );
                });
        }
        else if (!permissions.brandingManage) {
            brandingControlIds
                .forEach(id =>
                    disableControl(
                        document.getElementById(
                            id
                        )
                    )
                );
        }

        const heroForm =
            document.getElementById(
                "addHeroSlideForm"
            );

        const heroSection =
            document.getElementById(
                "heroSlidesTableBody"
            )?.closest(
                ".settings-section"
            );

        if (!permissions.heroView) {
            setHidden(
                heroSection,
                true
            );
        }
        else if (!permissions.heroManage) {
            setHidden(
                heroForm?.closest(
                    ".settings-card"
                ),
                true
            );
        }

        if (!permissions.brandingManage) {
            hideSaveActions(pane);
        }

        enforceHeroActions();
    }

    function enforceHeroActions() {
        const body =
            document.getElementById(
                "heroSlidesTableBody"
            );

        if (
            !body ||
            permissions.heroManage
        ) {
            return;
        }

        body
            .querySelectorAll(
                ".btn-sort-arrow, .btn-edit-slide, .btn-delete-wallet"
            )
            .forEach(element =>
                element.remove()
            );

        body
            .querySelectorAll(
                'input[type="checkbox"][onchange*="toggleHeroSlideStatus"]'
            )
            .forEach(control => {
                disableControl(control);
                control.removeAttribute(
                    "onchange"
                );
                control.style.cursor =
                    "default";
            });
    }

    function applyPoliciesPermissions() {
        const pane =
            document.querySelector(
                ".policies-tab"
            );

        if (!pane) return;

        if (!permissions.policiesManage) {
            [
                "autoApprovePlaces",
                "allowRegistration",
                "maxImageSize",
                "maxImageCount"
            ].forEach(id =>
                disableControl(
                    document.getElementById(
                        id
                    )
                )
            );
        }

        if (!permissions.financeManage) {
            [
                "commissionPercentage",
                "defaultCurrency",
                "autoUpdateExchangeRates",
                "sanaaUsdRate",
                "adenUsdRate",
                "sanaaSarRate",
                "adenSarRate",
                "refreshExchangeRatesNowBtn"
            ].forEach(id =>
                disableControl(
                    document.getElementById(
                        id
                    )
                )
            );
        }

        if (
            !permissions.policiesManage &&
            !permissions.financeManage
        ) {
            hideSaveActions(pane);
        }
    }

    function applyContactPermissions() {
        const pane =
            document.querySelector(
                ".contact-tab"
            );

        if (
            !pane ||
            permissions.contactManage
        ) {
            return;
        }

        disableControls(
            pane,
            "input, select, textarea"
        );

        hideSaveActions(pane);
    }

    function applyMaintenancePermissions() {
        const pane =
            document.querySelector(
                ".maintenance-tab"
            );

        if (
            !pane ||
            permissions.maintenanceManage
        ) {
            return;
        }

        disableControls(
            pane,
            "input, select, textarea"
        );

        pane
            .querySelectorAll(
                ".maintenance-save-button, .settings-save-actions"
            )
            .forEach(element =>
                setHidden(
                    element,
                    true
                )
            );
    }

    function applyWalletPermissions() {
        const form =
            document.getElementById(
                "addSupportedWalletForm"
            );

        if (!permissions.walletAdd) {
            setHidden(
                form?.closest(
                    ".settings-card"
                ),
                true
            );
        }

        enforceWalletActions();
    }

    function enforceWalletActions() {
        const body =
            document.getElementById(
                "supportedWalletsTableBody"
            );

        if (!body) return;

        if (!permissions.walletStatus) {
            body
                .querySelectorAll(
                    'input[onchange*="toggleWalletStateDynamically"]'
                )
                .forEach(control => {
                    disableControl(control);
                    control.removeAttribute(
                        "onchange"
                    );
                    control.style.cursor =
                        "default";
                });
        }

        if (!permissions.walletEdit) {
            body
                .querySelectorAll(
                    '[onclick*="editSupportedWalletNames"]'
                )
                .forEach(element =>
                    element.remove()
                );
        }

        if (!permissions.walletDelete) {
            body
                .querySelectorAll(
                    '[onclick*="deletePlatformWalletDynamically"]'
                )
                .forEach(element =>
                    element.remove()
                );
        }
    }

    function applyBackupPermissions() {
        const root =
            document.getElementById(
                "backupRecoveryCenter"
            );

        if (!root) return;

        if (!permissions.backupCreate) {
            setHidden(
                document.getElementById(
                    "backupCreateNowBtn"
                ),
                true
            );
        }

        if (!permissions.backupRestore) {
            setHidden(
                document.getElementById(
                    "backupRestoreFromFileBtn"
                ),
                true
            );

            setHidden(
                document.getElementById(
                    "backupRestoreFileInput"
                ),
                true
            );
        }

        if (!permissions.backupSchedule) {
            disableControl(
                document.getElementById(
                    "backupAutoEnabled"
                )
            );

            disableControl(
                document.getElementById(
                    "backupDailyTime"
                )
            );

            setHidden(
                document.getElementById(
                    "backupSaveAutoBtn"
                ),
                true
            );
        }

        if (!permissions.backupCloud) {
            [
                "backupGoogleDriveConnectBtn",
                "backupGoogleDriveSyncBtn",
                "backupGoogleDriveDisconnectBtn"
            ].forEach(id =>
                setHidden(
                    document.getElementById(
                        id
                    ),
                    true
                )
            );
        }

        enforceBackupDynamicActions();
    }

    function enforceBackupDynamicActions() {
        const root =
            document.getElementById(
                "backupRecoveryCenter"
            );

        if (!root) return;

        if (!permissions.backupDownload) {
            root
                .querySelectorAll(
                    'a[href*="/Admin/Backup/Download/"]'
                )
                .forEach(element =>
                    element.remove()
                );
        }

        if (!permissions.backupRestore) {
            root
                .querySelectorAll(
                    '[data-backup-action="restore"]'
                )
                .forEach(element =>
                    element.remove()
                );
        }

        if (!permissions.backupDelete) {
            root
                .querySelectorAll(
                    '[data-backup-action="delete"]'
                )
                .forEach(element =>
                    element.remove()
                );
        }

        if (!permissions.backupCloud) {
            [
                "backupGoogleDriveConnectBtn",
                "backupGoogleDriveSyncBtn",
                "backupGoogleDriveDisconnectBtn"
            ].forEach(id =>
                setHidden(
                    document.getElementById(
                        id
                    ),
                    true
                )
            );
        }
    }

    function installObservers() {
        const heroBody =
            document.getElementById(
                "heroSlidesTableBody"
            );

        if (heroBody) {
            new MutationObserver(
                enforceHeroActions
            ).observe(
                heroBody,
                {
                    childList: true,
                    subtree: true
                }
            );
        }

        const walletBody =
            document.getElementById(
                "supportedWalletsTableBody"
            );

        if (walletBody) {
            new MutationObserver(
                enforceWalletActions
            ).observe(
                walletBody,
                {
                    childList: true,
                    subtree: true
                }
            );
        }

        const backupRoot =
            document.getElementById(
                "backupRecoveryCenter"
            );

        if (backupRoot) {
            new MutationObserver(
                enforceBackupDynamicActions
            ).observe(
                backupRoot,
                {
                    childList: true,
                    subtree: true
                }
            );
        }
    }

    function applyAll() {
        applyTabVisibility();
        applyBrandingPermissions();
        applyPoliciesPermissions();
        applyContactPermissions();
        applyMaintenancePermissions();
        applyWalletPermissions();
        applyBackupPermissions();
        installObservers();
    }

    if (
        document.readyState ===
        "loading"
    ) {
        document.addEventListener(
            "DOMContentLoaded",
            applyAll,
            { once: true }
        );
    }
    else {
        applyAll();
    }
})();
