(() => {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        const settingsPane = document.getElementById('settings');
        if (!settingsPane) return;

        const path = (window.location.pathname || '').toLowerCase();
        const isOwnerProfile = path === '/owner/profile/ownerprofile';
        const isCustomerProfile = path === '/profile/customerprofile';

        if (!isOwnerProfile && !isCustomerProfile) return;

        const settingsCard = settingsPane.querySelector('.premium-glass-card');
        if (!settingsCard) return;

        let loadedPreferences = null;

        const getCheckboxValue = (id, fallback = false) => {
            const element = document.getElementById(id);
            return element ? !!element.checked : !!fallback;
        };

        const setCheckboxValue = (id, value) => {
            const element = document.getElementById(id);
            if (element) {
                element.checked = !!value;
            }
        };

        const getSelectedCurrency = () => {
            const checked = document.querySelector(
                'input[name="preferredCurrency"]:checked'
            );

            return checked?.value ||
                loadedPreferences?.preferredCurrency ||
                'ريال يمني';
        };

        const setSelectedCurrency = (currency) => {
            const input = Array.from(
                document.querySelectorAll(
                    'input[name="preferredCurrency"]'
                )
            ).find(item => item.value === currency);

            if (input) {
                input.checked = true;
            }
        };

        const showMessage = (options) => {
            if (window.Swal?.fire) {
                return window.Swal.fire(options);
            }

            window.alert(options.text || options.title || 'تم');
            return Promise.resolve();
        };

        const ensureSaveButton = () => {
            let button = document.getElementById('saveDisplaySettingsBtn');
            if (button) return button;

            const actions = document.createElement('div');
            actions.className =
                'form-actions mt-5 pt-4 border-top d-flex justify-content-end';

            actions.innerHTML = `
                <button type="button"
                        class="btn-premium-save"
                        id="saveDisplaySettingsBtn">
                    <i class="bi bi-floppy-fill me-2" aria-hidden="true"></i>
                    حفظ الإعدادات
                </button>`;

            settingsCard.appendChild(actions);

            return document.getElementById('saveDisplaySettingsBtn');
        };

        const loadSavedPreferences = async () => {
            try {
                const response = await fetch('/Profile/DisplayPreferences', {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    cache: 'no-store'
                });

                const data = await response.json().catch(() => ({}));

                if (!response.ok || data.success === false) {
                    return;
                }

                loadedPreferences = data;

                setCheckboxValue(
                    'newReservationAlert',
                    data.newReservationAlert
                );

                setCheckboxValue(
                    'newMessageAlert',
                    data.newMessageAlert
                );

                setCheckboxValue(
                    'newReviewAlert',
                    data.newReviewAlert
                );

                setCheckboxValue(
                    'soundAlert',
                    data.soundAlert
                );

                if (isCustomerProfile) {
                    setCheckboxValue(
                        'receiveOffersSwitch',
                        data.receiveOffers
                    );
                }

                if (data.preferredCurrency) {
                    setSelectedCurrency(data.preferredCurrency);
                }
            }
            catch (error) {
                console.warn(
                    'تعذر تحميل إعدادات العرض المحفوظة:',
                    error
                );
            }
        };

        const saveButton = ensureSaveButton();

        saveButton?.addEventListener('click', async () => {
            const languageSelect =
                document.getElementById('languageSelect');

            const darkModeSwitch =
                document.getElementById('profileDarkModeSwitch');

            const compactModeToggle =
                document.getElementById('compactModeToggle');

            const payload = {
                DarkModeEnabled:
                    darkModeSwitch
                        ? !!darkModeSwitch.checked
                        : !!loadedPreferences?.darkModeEnabled,

                CompactMode:
                    compactModeToggle
                        ? !!compactModeToggle.checked
                        : !!loadedPreferences?.compactMode,

                Language:
                    languageSelect?.value ||
                    loadedPreferences?.language ||
                    'ar',

                PreferredCurrency:
                    getSelectedCurrency(),

                NewReservationAlert:
                    getCheckboxValue(
                        'newReservationAlert',
                        loadedPreferences?.newReservationAlert
                    ),

                NewMessageAlert:
                    getCheckboxValue(
                        'newMessageAlert',
                        loadedPreferences?.newMessageAlert
                    ),

                NewReviewAlert:
                    getCheckboxValue(
                        'newReviewAlert',
                        loadedPreferences?.newReviewAlert
                    ),

                SoundAlert:
                    getCheckboxValue(
                        'soundAlert',
                        loadedPreferences?.soundAlert
                    ),

                ReceiveOffers:
                    isCustomerProfile
                        ? getCheckboxValue(
                            'receiveOffersSwitch',
                            loadedPreferences?.receiveOffers ?? true
                        )
                        : null
            };

            const originalHtml = saveButton.innerHTML;
            saveButton.disabled = true;
            saveButton.innerHTML = `
                <span class="spinner-border spinner-border-sm me-2"
                      role="status"
                      aria-hidden="true"></span>
                جاري الحفظ...`;

            try {
                const response = await fetch('/Profile/DisplayPreferences', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify(payload)
                });

                const data = await response.json().catch(() => ({}));

                if (!response.ok || data.success === false) {
                    throw new Error(
                        data.message ||
                        'تعذر حفظ إعدادات العرض.'
                    );
                }

                localStorage.setItem(
                    'darkMode',
                    payload.DarkModeEnabled
                        ? 'enabled'
                        : 'disabled'
                );

                localStorage.setItem(
                    isOwnerProfile
                        ? 'ownerCompactMode'
                        : 'customerCompactMode',
                    payload.CompactMode
                        ? 'enabled'
                        : 'disabled'
                );

                await showMessage({
                    icon: 'success',
                    title: 'تم الحفظ',
                    text: 'تم حفظ إعدادات العرض بنجاح.',
                    timer: 1400,
                    showConfirmButton: false
                });

                window.location.reload();
            }
            catch (error) {
                await showMessage({
                    icon: 'error',
                    title: 'تعذر الحفظ',
                    text:
                        error?.message ||
                        'حدث خطأ أثناء حفظ إعدادات العرض.'
                });
            }
            finally {
                saveButton.disabled = false;
                saveButton.innerHTML = originalHtml;
            }
        });

        void loadSavedPreferences();
    });
})();
