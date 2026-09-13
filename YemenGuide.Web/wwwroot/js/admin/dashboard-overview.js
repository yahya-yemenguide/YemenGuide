(() => {
    "use strict";

    const root =
        document.querySelector(
            ".admin-command-center"
        );

    const dataNode =
        document.getElementById(
            "adminDashboardData"
        );

    if (!root || !dataNode) {
        return;
    }

    const locale =
        dataNode.dataset.locale ||
        (
            document.documentElement.lang
                ?.toLowerCase()
                .startsWith("en")
                ? "en-US"
                : "ar-YE"
        );

    const isRtl =
        String(
            dataNode.dataset.isRtl ||
            ""
        ).toLowerCase() === "true";

    const currencyCode =
        dataNode.dataset.currencyCode ||
        "YER";

    const currencySymbol =
        dataNode.dataset.currencySymbol ||
        "ر.ي";

    const countFormat =
        new Intl.NumberFormat(
            locale,
            {
                maximumFractionDigits: 0
            }
        );

    const moneyFormat =
        new Intl.NumberFormat(
            locale,
            {
                minimumFractionDigits:
                    currencyCode === "YER"
                        ? 0
                        : 2,

                maximumFractionDigits:
                    currencyCode === "YER"
                        ? 0
                        : 2
            }
        );

    const isDark = () =>
        document.body
            .classList
            .contains("dark-mode");

    const css = (name, fallback) =>
        getComputedStyle(
            document.documentElement
        )
            .getPropertyValue(name)
            .trim() ||
        fallback;

    const chartFontFamily = () =>
        getComputedStyle(root)
            .fontFamily
            .split(",")[0]
            .trim() ||
        "Tajawal";

    const readArray = name => {
        try {
            const value =
                JSON.parse(
                    dataNode.dataset[name] ||
                    "[]"
                );

            return Array.isArray(value)
                ? value
                : [];
        }
        catch {
            return [];
        }
    };

    function formatMoney(value) {
        const formatted =
            moneyFormat.format(
                Number(value || 0)
            );

        return currencyCode === "USD"
            ? `$${formatted}`
            : `${formatted} ${currencySymbol}`;
    }

    function greeting() {
        const hour =
            new Date().getHours();

        if (hour >= 5 && hour < 12) {
            return (
                dataNode.dataset
                    .greetingMorning ||
                (
                    isRtl
                        ? "صباح الخير"
                        : "Good morning"
                )
            );
        }

        if (hour >= 12 && hour < 18) {
            return (
                dataNode.dataset
                    .greetingAfternoon ||
                (
                    isRtl
                        ? "مساء الخير"
                        : "Good afternoon"
                )
            );
        }

        return (
            dataNode.dataset
                .greetingEvening ||
            (
                isRtl
                    ? "أهلًا بعودتك"
                    : "Welcome back"
            )
        );
    }

    function updateClock() {
        const now =
            new Date();

        const clock =
            root.querySelector(
                "[data-live-clock]"
            );

        const greetingNode =
            root.querySelector(
                "[data-greeting]"
            );

        if (clock) {
            clock.textContent =
                now.toLocaleTimeString(
                    locale,
                    {
                        hour: "2-digit",
                        minute: "2-digit"
                    }
                );
        }

        if (greetingNode) {
            greetingNode.textContent =
                greeting();
        }
    }

    function revealSections() {
        const nodes =
            Array.from(
                root.querySelectorAll(
                    ".acc-reveal"
                )
            );

        const reduceMotion =
            matchMedia(
                "(prefers-reduced-motion: reduce)"
            ).matches;

        if (
            reduceMotion ||
            !("IntersectionObserver" in window)
        ) {
            nodes.forEach(node =>
                node.classList.add(
                    "is-visible"
                )
            );

            return;
        }

        const observer =
            new IntersectionObserver(
                entries => {
                    entries.forEach(
                        entry => {
                            if (
                                !entry
                                    .isIntersecting
                            ) {
                                return;
                            }

                            entry.target
                                .classList
                                .add(
                                    "is-visible"
                                );

                            observer.unobserve(
                                entry.target
                            );
                        }
                    );
                },
                {
                    threshold: 0.05
                }
            );

        nodes.forEach(
            (node, index) => {
                node.style.animationDelay =
                    `${Math.min(
                        index * 20,
                        80
                    )}ms`;

                observer.observe(node);
            }
        );
    }

    function animateCounts() {
        const nodes =
            Array.from(
                root.querySelectorAll(
                    "[data-count]"
                )
            );

        const reduceMotion =
            matchMedia(
                "(prefers-reduced-motion: reduce)"
            ).matches;

        nodes.forEach(node => {
            const target =
                Number(
                    node.dataset.count ||
                    0
                );

            if (
                !Number.isFinite(target)
            ) {
                return;
            }

            if (
                reduceMotion ||
                target === 0
            ) {
                node.textContent =
                    countFormat.format(
                        target
                    );

                return;
            }

            const start =
                performance.now();

            const duration = 320;

            const step = time => {
                const progress =
                    Math.min(
                        1,
                        (time - start) /
                        duration
                    );

                const eased =
                    1 -
                    Math.pow(
                        1 - progress,
                        3
                    );

                node.textContent =
                    countFormat.format(
                        Math.round(
                            target * eased
                        )
                    );

                if (progress < 1) {
                    requestAnimationFrame(
                        step
                    );
                }
            };

            requestAnimationFrame(step);
        });
    }

    function theme() {
        return {
            text:
                isDark()
                    ? "#b8cad5"
                    : "#6f8490",

            grid:
                isDark()
                    ? "rgba(151,202,227,.08)"
                    : "rgba(17,74,101,.055)",

            card:
                isDark()
                    ? "#0f2232"
                    : "#ffffff",

            blue:
                css(
                    "--acc-primary",
                    "#00a6da"
                ),

            orange:
                css(
                    "--acc-orange",
                    "#f59e0b"
                ),

            green:
                css(
                    "--acc-green",
                    "#18a66a"
                ),

            purple:
                css(
                    "--acc-purple",
                    "#7c3aed"
                ),

            red:
                css(
                    "--acc-red",
                    "#e4555f"
                )
        };
    }

    let charts = [];

    function destroyCharts() {
        charts.forEach(
            chart =>
                chart?.destroy?.()
        );

        charts = [];
    }

    function hexToRgb(hex) {
        const raw =
            String(hex)
                .replace("#", "");

        if (raw.length !== 6) {
            return "rgb(0,166,218)";
        }

        const number =
            parseInt(
                raw,
                16            );

        return `rgb(${(number >> 16) & 255},${(number >> 8) & 255},${number & 255})`;
    }

    const gradient = (
        ctx,
        color,
        alpha = .20
    ) => {
        const result =
            ctx.createLinearGradient(
                0,
                0,
                0,
                220
            );

        result.addColorStop(
            0,
            color
                .replace(
                    "rgb(",
                    "rgba("
                )
                .replace(
                    ")",
                    `,${alpha})`
                )
        );

        result.addColorStop(
            1,
            color
                .replace(
                    "rgb(",
                    "rgba("
                )
                .replace(
                    ")",
                    ",0)"
                )
        );

        return result;
    };

    function colorForPlaceStatus(
        status,
        colors
    ) {
        switch (
        String(status || "")
            .trim()
            .toLowerCase()
        ) {
            case "active":
            case "approved":
                return colors.green;

            case "pending":
                return colors.orange;

            case "rejected":
                return colors.red;

            case "inactive":
                return "#8ca0aa";

            default:
                return colors.blue;
        }
    }

    function colorForFinanceStatus(
        status,
        colors
    ) {
        switch (
        String(status || "")
            .trim()
            .toLowerCase()
        ) {
            case "charged":
                return colors.green;

            case "held":
                return colors.orange;

            case "calculated":
                return colors.blue;

            case "debt":
                return colors.red;

            case "released":
                return colors.purple;

            default:
                return "#8ca0aa";
        }
    }

    function commonOptions() {
        const colors =
            theme();

        const font =
            chartFontFamily();

        return {
            responsive: true,
            maintainAspectRatio: false,

            animation: {
                duration: 340,
                easing: "easeOutQuart"
            },

            plugins: {
                legend: {
                    display: false
                },

                tooltip: {
                    rtl: isRtl,

                    textDirection:
                        isRtl
                            ? "rtl"
                            : "ltr",

                    backgroundColor:
                        isDark()
                            ? "rgba(7,18,26,.97)"
                            : "rgba(5,32,60,.94)",

                    padding: 10,

                    titleFont: {
                        family: font,
                        size: 11
                    },

                    bodyFont: {
                        family: font,
                        size: 10
                    },

                    displayColors: true,
                    cornerRadius: 10
                }
            },

            scales: {
                x: {
                    grid: {
                        display: false
                    },

                    border: {
                        display: false
                    },

                    ticks: {
                        color:
                            colors.text,

                        font: {
                            family: font,
                            size: 9,
                            weight: "700"
                        }
                    }
                },

                y: {
                    beginAtZero: true,

                    grid: {
                        color:
                            colors.grid
                    },

                    border: {
                        display: false
                    },

                    ticks: {
                        color:
                            colors.text,

                        precision: 0,

                        font: {
                            family: font,
                            size: 9
                        }
                    }
                }
            }
        };
    }

    function createCharts() {
        if (!window.Chart) {
            return;
        }

        destroyCharts();

        const colors =
            theme();

        const bookingCanvas =
            document.getElementById(
                "adminBookingsChart"
            );

        if (bookingCanvas) {
            const ctx =
                bookingCanvas
                    .getContext("2d");

            const blueRgb =
                hexToRgb(
                    colors.blue
                );

            const orangeRgb =
                hexToRgb(
                    colors.orange
                );

            charts.push(
                new Chart(
                    ctx,
                    {
                        type: "line",

                        data: {
                            labels:
                                readArray(
                                    "bookingLabels"
                                ),

                            datasets: [
                                {
                                    label:
                                        dataNode
                                            .dataset
                                            .bookingCreatedName ||
                                        "Bookings created",

                                    data:
                                        readArray(
                                            "bookingCreated"
                                        ),

                                    borderColor:
                                        colors.blue,

                                    backgroundColor:
                                        gradient(
                                            ctx,
                                            blueRgb,
                                            .18
                                        ),

                                    fill: true,
                                    tension: .38,
                                    borderWidth: 2.2,
                                    pointRadius: 2.5,
                                    pointHoverRadius: 5,
                                    pointBackgroundColor:
                                        colors.card,
                                    pointBorderWidth: 2
                                },

                                {
                                    label:
                                        dataNode
                                            .dataset
                                            .bookingStartingName ||
                                        "Bookings starting",

                                    data:
                                        readArray(
                                            "bookingStarting"
                                        ),

                                    borderColor:
                                        colors.orange,

                                    backgroundColor:
                                        gradient(
                                            ctx,
                                            orangeRgb,
                                            .09
                                        ),

                                    fill: true,
                                    tension: .38,
                                    borderWidth: 2,
                                    pointRadius: 2.2,
                                    pointHoverRadius: 4.5,
                                    pointBackgroundColor:
                                        colors.card,
                                    pointBorderWidth: 2
                                }
                            ]
                        },

                        options: {
                            ...commonOptions(),

                            interaction: {
                                mode: "index",
                                intersect: false
                            }
                        }
                    }
                )
            );
        }

        const placeCanvas =
            document.getElementById(
                "adminPlacesChart"
            );

        if (placeCanvas) {
            const placeKeys =
                readArray(
                    "placeStatusKeys"
                );

            const placeValues =
                readArray(
                    "placeStatusValues"
                );

            charts.push(
                new Chart(
                    placeCanvas,
                    {
                        type: "doughnut",

                        data: {
                            labels:
                                readArray(
                                    "placeStatusLabels"
                                ),

                            datasets: [
                                {
                                    data:
                                        placeValues,

                                    backgroundColor:
                                        placeKeys.map(
                                            key =>
                                                colorForPlaceStatus(
                                                    key,
                                                    colors
                                                )
                                        ),

                                    borderColor:
                                        colors.card,

                                    borderWidth: 3,
                                    spacing: 2,
                                    hoverOffset: 4
                                }
                            ]
                        },

                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            cutout: "73%",

                            animation: {
                                duration: 340,
                                easing: "easeOutQuart"
                            },

                            plugins: {
                                legend: {
                                    display: false
                                },

                                tooltip:
                                    commonOptions()
                                        .plugins
                                        .tooltip
                            }
                        }
                    }                )
            );
        }

        const revenueCanvas =
            document.getElementById(
                "adminRevenueChart"
            );

        if (revenueCanvas) {
            const ctx =
                revenueCanvas
                    .getContext("2d");

            const greenRgb =
                hexToRgb(
                    colors.green
                );

            const options =
                commonOptions();

            options.plugins
                .tooltip
                .callbacks = {
                label:
                    context =>
                        ` ${formatMoney(
                            Number(
                                context.raw ||
                                0
                            )
                        )}`
            };

            charts.push(
                new Chart(
                    ctx,
                    {
                        type: "line",

                        data: {
                            labels:
                                readArray(
                                    "revenueLabels"
                                ),

                            datasets: [
                                {
                                    label:
                                        dataNode
                                            .dataset
                                            .revenueName ||
                                        "Revenue",

                                    data:
                                        readArray(
                                            "revenueValues"
                                        ),

                                    borderColor:
                                        colors.green,

                                    backgroundColor:
                                        gradient(
                                            ctx,
                                            greenRgb,
                                            .17
                                        ),

                                    fill: true,
                                    tension: .40,
                                    borderWidth: 2.2,
                                    pointRadius: 2.4,
                                    pointBackgroundColor:
                                        colors.card,
                                    pointBorderWidth: 2
                                }
                            ]
                        },

                        options
                    }
                )
            );
        }

        const financeCanvas =
            document.getElementById(
                "adminFinanceChart"
            );

        if (financeCanvas) {
            const labels =
                readArray(
                    "financeLabels"
                );

            const keys =
                readArray(
                    "financeKeys"
                );

            const values =
                readArray(
                    "financeValues"
                );

            const options =
                commonOptions();

            options.indexAxis = "y";

            options.scales
                .x
                .ticks
                .callback =
                value =>
                    moneyFormat.format(
                        Number(
                            value ||
                            0
                        )
                    );

            options.plugins
                .tooltip
                .callbacks = {
                label:
                    context =>
                        ` ${formatMoney(
                            Number(
                                context.raw ||
                                0
                            )
                        )}`
            };

            charts.push(
                new Chart(
                    financeCanvas,
                    {
                        type: "bar",

                        data: {
                            labels,

                            datasets: [
                                {
                                    data:
                                        values,

                                    backgroundColor:
                                        keys.map(
                                            key =>
                                                colorForFinanceStatus(
                                                    key,
                                                    colors
                                                )
                                        ),

                                    borderRadius: 7,
                                    borderSkipped: false,
                                    barThickness: 9
                                }
                            ]
                        },

                        options
                    }
                )
            );
        }
    }

    function watchTheme() {
        const observer =
            new MutationObserver(() => {
                clearTimeout(
                    watchTheme.timer
                );

                watchTheme.timer =
                    setTimeout(
                        createCharts,
                        80
                    );
            });

        observer.observe(
            document.body,
            {
                attributes: true,
                attributeFilter: [
                    "class"
                ]
            }
        );
    }

    updateClock();

    window.setInterval(
        updateClock,
        30000
    );

    revealSections();
    animateCounts();
    createCharts();
    watchTheme();
})();

/* === CHATGPT DASHBOARD ENHANCEMENT START === */
/* =============================================================================
   CHATGPT DASHBOARD UI ENHANCEMENT
   إضافات بصرية فقط — لا تغيير للبيانات أو الطلبات
   ============================================================================= */
(() => {
    "use strict";

    const root = document.querySelector(".admin-command-center");

    if (!root) {
        return;
    }

    const reduceMotion = window.matchMedia(
        "(prefers-reduced-motion: reduce)"
    );

    const finePointer = window.matchMedia(
        "(hover: hover) and (pointer: fine)"
    );

    function addPointerGlow() {
        if (reduceMotion.matches || !finePointer.matches) {
            return;
        }

        const cards = root.querySelectorAll([
            ".acc-hero",
            ".acc-kpi",
            ".acc-panel",
            ".acc-attention-item",
            ".acc-recent-item",
            ".acc-quick a",
            ".acc-hero__pulse article"
        ].join(","));

        cards.forEach(card => {
            if (card.dataset.dashboardGlowReady === "1") {
                return;
            }

            card.dataset.dashboardGlowReady = "1";
            card.classList.add("acc-js-glow");

            card.addEventListener(
                "pointermove",
                event => {
                    const rect = card.getBoundingClientRect();

                    card.style.setProperty(
                        "--mx",
                        `${event.clientX - rect.left}px`
                    );

                    card.style.setProperty(
                        "--my",
                        `${event.clientY - rect.top}px`
                    );
                },
                { passive: true }
            );
        });
    }

    function enhanceCharts() {
        if (!window.Chart || !window.Chart.instances) {
            return;
        }

        const mobile = window.matchMedia(
            "(max-width: 767.98px)"
        ).matches;

        const family =
            getComputedStyle(root).fontFamily ||
            "system-ui";

        Object.values(window.Chart.instances).forEach(chart => {
            if (!chart?.options) {
                return;
            }

            const tooltip = chart.options.plugins?.tooltip;

            if (tooltip) {
                tooltip.padding = mobile ? 10 : 12;
                tooltip.cornerRadius = 11;

                tooltip.titleFont = {
                    ...(tooltip.titleFont || {}),
                    family,
                    size: mobile ? 12 : 13,
                    weight: "700"
                };

                tooltip.bodyFont = {
                    ...(tooltip.bodyFont || {}),
                    family,
                    size: mobile ? 11 : 12,
                    weight: "600"
                };
            }

            ["x", "y"].forEach(axisName => {
                const axis = chart.options.scales?.[axisName];

                if (!axis?.ticks) {
                    return;
                }

                axis.ticks.padding = mobile ? 6 : 8;
                axis.ticks.font = {
                    ...(axis.ticks.font || {}),
                    family,
                    size: mobile ? 10 : 11,
                    weight: axisName === "x"
                        ? "700"
                        : "600"
                };
            });

            if (Array.isArray(chart.data?.datasets)) {
                chart.data.datasets.forEach(dataset => {
                    if (chart.config?.type === "line") {
                        dataset.borderWidth = Math.max(
                            Number(dataset.borderWidth || 0),
                            2.4
                        );

                        dataset.pointRadius = Math.max(
                            Number(dataset.pointRadius || 0),
                            mobile ? 2.6 : 3
                        );

                        dataset.pointHoverRadius = Math.max(
                            Number(dataset.pointHoverRadius || 0),
                            mobile ? 4.5 : 5.5
                        );
                    }

                    if (chart.config?.type === "bar") {
                        dataset.barThickness = Math.max(
                            Number(dataset.barThickness || 0),
                            mobile ? 10 : 12
                        );
                    }
                });
            }

            try {
                chart.update("none");
            }
            catch {
                chart.update();
            }
        });
    }

    function refreshEnhancements() {
        addPointerGlow();
        enhanceCharts();
    }

    [0, 120, 450, 1000].forEach(delay => {
        window.setTimeout(refreshEnhancements, delay);
    });

    let resizeTimer;

    window.addEventListener(
        "resize",
        () => {
            window.clearTimeout(resizeTimer);

            resizeTimer = window.setTimeout(
                enhanceCharts,
                160
            );
        },
        { passive: true }
    );

    const themeObserver = new MutationObserver(() => {
        window.setTimeout(enhanceCharts, 140);
    });

    themeObserver.observe(
        document.body,
        {
            attributes: true,
            attributeFilter: ["class"]
        }
    );
})();
/* === CHATGPT DASHBOARD ENHANCEMENT END === */
