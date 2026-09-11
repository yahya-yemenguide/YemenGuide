(() => {
    "use strict";

    const buttons = document.querySelectorAll(
        ".js-customer-language"
    );

    if (!buttons.length) {
        return;
    }

    const desktopTrigger = document.getElementById(
        "customerLanguageDropdown"
    );

    let isSwitching = false;

    const setBusyState = (busy) => {
        isSwitching = busy;

        if (desktopTrigger) {
            desktopTrigger.classList.toggle(
                "is-switching",
                busy
            );

            desktopTrigger.setAttribute(
                "aria-busy",
                busy ? "true" : "false"
            );
        }

        buttons.forEach((button) => {
            button.disabled = busy;
        });
    };

    const showFailureState = (sourceButton) => {
        const target =
            sourceButton ||
            desktopTrigger;

        if (!target || typeof target.animate !== "function") {
            return;
        }

        target.animate(
            [
                { transform: "translateX(0)" },
                { transform: "translateX(-3px)" },
                { transform: "translateX(3px)" },
                { transform: "translateX(0)" }
            ],
            {
                duration: 260,
                easing: "ease-out"
            }
        );
    };

    buttons.forEach((button) => {
        button.addEventListener(
            "click",
            async () => {
                if (isSwitching) {
                    return;
                }

                const culture =
                    button.dataset.culture;

                if (
                    culture !== "ar" &&
                    culture !== "en"
                ) {
                    return;
                }

                if (
                    button.classList.contains(
                        "is-active"
                    )
                ) {
                    return;
                }

                setBusyState(true);

                try {
                    const response =
                        await fetch(
                            "/Language/SetLanguage",
                            {
                                method: "POST",
                                headers: {
                                    "Content-Type":
                                        "application/json",
                                    "X-Requested-With":
                                        "XMLHttpRequest"
                                },
                                body: JSON.stringify({
                                    Culture:
                                        culture
                                })
                            }
                        );

                    const data =
                        await response.json();

                    if (
                        !response.ok ||
                        data.success !== true
                    ) {
                        throw new Error(
                            data.message ||
                            "Unable to change language."
                        );
                    }

                    document.documentElement
                        .classList.add(
                            "yg-language-changing"
                        );

                    window.setTimeout(
                        () => {
                            window.location.reload();
                        },
                        90
                    );
                }
                catch (error) {
                    console.error(
                        "Language change failed:",
                        error
                    );

                    setBusyState(false);
                    showFailureState(button);
                }
            }
        );
    });
})();
