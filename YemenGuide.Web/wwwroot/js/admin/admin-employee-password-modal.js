(() => {
    "use strict";

    const gate =
        document.getElementById(
            "employeePasswordGate"
        );

    if (!gate) return;

    const i18n =
        window.AdminI18n || null;

    const text = (key, fallback) =>
        i18n?.get
            ? i18n.get(key, fallback)
            : fallback;

    const form =
        document.getElementById(
            "employeePasswordGateForm"
        );

    const newPassword =
        document.getElementById(
            "employeeNewPassword"
        );

    const confirmPassword =
        document.getElementById(
            "employeeConfirmPassword"
        );

    const submitButton =
        document.getElementById(
            "employeePasswordGateSubmit"
        );

    const submitText =
        submitButton?.querySelector(
            ".employee-password-gate__submit-text"
        );

    const spinner =
        submitButton?.querySelector(
            ".employee-password-gate__spinner"
        );

    const errorBox =
        document.getElementById(
            "employeePasswordGateError"
        );

    const rules = {
        length:
            gate.querySelector(
                '[data-rule="length"]'
            ),
        letter:
            gate.querySelector(
                '[data-rule="letter"]'
            ),
        number:
            gate.querySelector(
                '[data-rule="number"]'
            ),
        match:
            gate.querySelector(
                '[data-rule="match"]'
            )
    };

    document.body.classList.add(
        "employee-password-gate-open"
    );

    document.addEventListener(
        "keydown",
        event => {
            if (event.key === "Escape") {
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        },
        true
    );

    gate.querySelector(
        ".employee-password-gate__backdrop"
    )?.addEventListener(
        "click",
        event => {
            event.preventDefault();
            newPassword?.focus();
        }
    );

    gate.querySelectorAll(
        "[data-password-toggle]"
    ).forEach(button => {
        button.addEventListener(
            "click",
            () => {
                const targetId =
                    button.getAttribute(
                        "data-password-toggle"
                    );

                const input =
                    document.getElementById(
                        targetId
                    );

                const icon =
                    button.querySelector("i");

                if (!input) return;

                const shouldShow =
                    input.type === "password";

                input.type =
                    shouldShow
                        ? "text"
                        : "password";

                if (icon) {
                    icon.className =
                        shouldShow
                            ? "bi bi-eye-slash"
                            : "bi bi-eye";
                }
            }
        );
    });

    function getState() {
        const value =
            newPassword?.value ?? "";

        const confirmValue =
            confirmPassword?.value ?? "";

        return {
            length: value.length >= 8,
            letter: /[A-Za-z]/.test(value),
            number: /\d/.test(value),
            match:
                value.length > 0 &&
                confirmValue.length > 0 &&
                value === confirmValue
        };
    }

    function renderRules() {
        const state = getState();

        Object.entries(state)
            .forEach(([key, valid]) => {
                rules[key]?.classList.toggle(
                    "is-valid",
                    valid
                );
            });

        const allValid =
            Object.values(state)
                .every(Boolean);

        if (submitButton) {
            submitButton.disabled =
                !allValid;
        }

        if (
            errorBox &&
            !errorBox.hidden
        ) {
            errorBox.hidden = true;
            errorBox.textContent = "";
        }

        return allValid;
    }

    newPassword?.addEventListener(
        "input",
        renderRules
    );

    confirmPassword?.addEventListener(
        "input",
        renderRules
    );

    function setLoading(loading) {
        if (!submitButton) return;

        submitButton.disabled =
            loading ||
            !Object.values(
                getState()
            ).every(Boolean);

        if (spinner) {
            spinner.hidden = !loading;
        }

        if (submitText) {
            submitText.textContent =
                loading
                    ? text(
                        "Admin_Password_Loading",
                        "Securing account..."
                    )
                    : text(
                        "Admin_Password_SecureContinue",
                        "Secure account and continue"
                    );
        }
    }

    function showError(message) {
        if (!errorBox) return;

        const safeMessage =
            i18n?.t
                ? i18n.t(
                    message ||
                    text(
                        "Admin_Password_SaveFailed",
                        "Could not save the new password."
                    )
                )
                : (
                    message ||
                    text(
                        "Admin_Password_SaveFailed",
                        "Could not save the new password."
                    )
                );

        errorBox.textContent =
            safeMessage;

        errorBox.hidden = false;
    }

    form?.addEventListener(
        "submit",
        async event => {
            event.preventDefault();

            if (!renderRules()) {
                showError(
                    text(
                        "Admin_Password_CheckRules",
                        "Review the password requirements and make sure both passwords match."
                    )
                );
                return;
            }

            setLoading(true);

            try {
                const response =
                    await fetch(
                        "/Account/ForceChangePassword",
                        {
                            method: "POST",
                            body:
                                new FormData(
                                    form
                                ),
                            credentials:
                                "same-origin",
                            headers: {
                                "X-Requested-With":
                                    "XMLHttpRequest"
                            }
                        }
                    );

                const result =
                    await response.json();

                if (
                    !response.ok ||
                    result.success !== true
                ) {
                    showError(
                        result.message ||
                        text(
                            "Admin_Password_SaveFailed",
                            "Could not save the new password."
                        )
                    );

                    setLoading(false);
                    return;
                }

                if (submitText) {
                    submitText.textContent =
                        text(
                            "Admin_Password_Secured",
                            "Account secured"
                        );
                }

                if (spinner) {
                    spinner.hidden = true;
                }

                submitButton?.classList.add(
                    "is-success"
                );

                window.setTimeout(
                    () => {
                        window.location.href =
                            result.url ||
                            window.location.href;
                    },
                    450
                );
            } catch {
                showError(
                    text(
                        "Admin_Password_ServerFailed",
                        "Could not connect to the server. Try again."
                    )
                );

                setLoading(false);
            }
        }
    );

    window.setTimeout(
        () => newPassword?.focus(),
        120
    );
})();
